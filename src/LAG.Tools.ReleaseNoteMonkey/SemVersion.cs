using System.Diagnostics;
    
namespace LAG.Tools.ReleaseNoteMonkey
{
    [DebuggerDisplay("{Original}")]
    struct SemVersion
    {
        public string Original { get; set; }

        public int Major { get; set; }

        public int Minor { get; set; }

        public int Patch { get; set; }

        public string PreRelease { get; set; }
    }
}
