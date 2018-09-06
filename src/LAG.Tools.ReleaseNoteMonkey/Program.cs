using McMaster.Extensions.CommandLineUtils;

namespace LAG.Tools.ReleaseNoteMonkey
{
    [Command(Name = "rnm", Description = "release monkey cli", ThrowOnUnexpectedArgument = false)]
    [Subcommand("create", typeof(Create))]
    [HelpOption]
    class Program
    {
        public static int Main(string[] args)
        {
            var state = CommandLineApplication.Execute<Program>(args);
            //Console.ReadLine();
            return state;
        }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
