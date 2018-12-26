using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnToGit
{
    public class BranchData
    {
        public bool RemoveBranch { get; set; }
        public string CreateBranchFrom { get; set; }
        public long CreateBranchFromRevision { get; set; }
        public string ParentRenameFrom { get; set; }
        public string ParentRenameTo { get; set; }
        public long ParentRenameFromRevision { get; set; }
        public List<RevisionAction> Data { get; set; } = new List<RevisionAction>();

        public Dictionary<string, Tuple<List<long>,List<long>>> Merges { get; set; } =
            new Dictionary<string, Tuple<List<long>, List<long>>>();
    }
}
