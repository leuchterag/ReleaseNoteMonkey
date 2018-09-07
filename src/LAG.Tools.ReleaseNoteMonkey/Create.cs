using HandlebarsDotNet;
using LibGit2Sharp;
using McMaster.Extensions.CommandLineUtils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LAG.Tools.ReleaseNoteMonkey
{
    class Create
    {
        [Option(Description = "from", ShortName = "from")]
        public string From { get; } = string.Empty;

        [Option(Description = "to", ShortName = "to")]
        public string To { get; } = "master";

        [Option(Description = "output", LongName = "output", ShortName = "o")]
        public string Output { get; } = "./RELEASENOTES.md";

        [Option(Description = "versionprefix", LongName = "versionprefix", ShortName = "vp")]
        public string VersionPrefix { get; } = string.Empty;
        
        [Option(Description = "template", LongName = "template", ShortName = "t")]
        public string TemplatePath { get; } = "./releasenotes.tmpl";

        [Argument(0, Description = "The positories root path", Name = "Repository")]
        public string Repository { get; set; } = ".";

        protected async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            console.WriteLine($"Aggregating commits in Repository {Repository}, from {From} to {To} into output file: {Output}");

            if (string.IsNullOrEmpty(Repository))
            {
                Repository = ".";
            }
            string repoPath;
            if (Path.IsPathRooted(Repository))
            {
                repoPath = Repository;
            }
            else
            {
                repoPath = Path.Combine(Directory.GetCurrentDirectory(), Repository);
            }

            using (var repo = new Repository(repoPath))
            {
                console.Out.WriteLine("The following tags were found:");
                var semVerRegex = new Regex("^" + VersionPrefix + @"(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)-?(?<suffix>([\w\d-\.+]+){0,2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var messageRegex = new Regex(@"^.*$\n\n((\w+\s*:\s*.+?(?=($)))(\n|$))+", RegexOptions.Compiled | RegexOptions.Multiline);
                var kvRegex = new Regex(@"(?<key>\w+)\s*:\s*(?<value>.+?(?=(\n|$)))", RegexOptions.Compiled | RegexOptions.Singleline);

                var versionTags = repo
                    .Tags
                    .Select(x => semVerRegex.Match(x.FriendlyName))
                    .Where(x => x.Success)
                    .Select(x =>
                    new SemVersion
                    {
                        Original = x.Value,
                        Major = int.Parse(x.Groups["major"].Value),
                        Minor = int.Parse(x.Groups["minor"].Value),
                        Patch = int.Parse(x.Groups["patch"].Value),
                        PreRelease = x.Groups["suffix"].Value
                    })
                    .OrderBy(x => x.Major)
                    .ThenBy(x => x.Minor)
                    .ThenBy(x => x.Patch)
                    .ThenBy(x => x.PreRelease)
                    .ToArray();

                var versionHistory = new Dictionary<SemVersion, IEnumerable<(Commit Commit, IDictionary<string, string> Values)>>();

                for (int i = 1; i < versionTags.Length; i++)
                {
                    var from = repo.Tags.First(x => x.FriendlyName.Equals(versionTags[i - 1].Original));
                    var to = repo.Tags.First(x => x.FriendlyName.Equals(versionTags[i].Original));

                    await console.Out.WriteLineAsync($"Getting ReleaseNotes from: {from.FriendlyName} until {to.FriendlyName}");

                    var filter = new CommitFilter
                    {
                        ExcludeReachableFrom = from,       // formerly "Since"
                        IncludeReachableFrom = to,  // formerly "Until"
                    };

                    var rnCommits = repo
                        .Commits
                        .QueryBy(filter)
                        .Where(x => messageRegex.IsMatch(x.Message)) // Filter out messages without release notes section
                        .Select(x => (Commit: x, Match: kvRegex.Matches(x.Message))) // Run regex over rn section to get all KV pairs
                        .Where(x => x.Match.Any()) // Filter out all commits that do not have release-note format.
                        .Select(x =>
                        {
                            var dict = x.Match.ToDictionary(
                                y => y.Groups["key"].Value,
                                y => y.Groups["value"].Value);

                            return (Commit: x.Commit, Values: dict as IDictionary<string, string>);
                        }).ToArray();

                    versionHistory[versionTags[i]] = rnCommits;
                }

                string templatePath;
                if (Path.IsPathRooted(TemplatePath))
                {
                    templatePath = TemplatePath;
                }
                else
                {
                    templatePath = Path.Combine(Directory.GetCurrentDirectory(), TemplatePath);
                }

                if (!File.Exists(templatePath))
                {
                    await console.Error.WriteLineAsync($"Cannot find template file: {templatePath}");
                }

                string source = await File.ReadAllTextAsync(templatePath);

                var template = Handlebars.Compile(source);

                var data = new
                {
                    versions = versionHistory
                        .Select(x =>
                        new
                        {
                            Version = x.Key,
                            Notes = x.Value
                                .Select(y => new { Commit = y.Commit, Data = y.Values}).ToArray()
                        }).ToArray()
                };

                var result = template(data);

                string outputPath;
                if (Path.IsPathRooted(Output))
                {
                    outputPath = Output;
                }
                else
                {
                    outputPath = Path.Combine(Directory.GetCurrentDirectory(), Output);
                }

                await File.WriteAllTextAsync(outputPath, result, Encoding.UTF8);
            }

            return 0;
        }
    }
}
