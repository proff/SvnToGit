namespace SvnToGit
{
    public class RevisionAction
    {
        public ActionType Type { get; set; }
        public string SvnPath { get; set; }
        public string GitPath { get; set; }
        public string FromPath { get; set; }
        public long FromRevision { get; set; }
        public string Branch { get; set; }
    }
}
