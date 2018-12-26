using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnToGit
{
    public class State
    {
        public string StartBranch { get; set; }
        public long Revision { get; set; }
        public List<string> Branches { get; set; } = new List<string>();

        /*public Dictionary<string, Dictionary<long, string>> Commits { get; set; } =
            new Dictionary<string, Dictionary<long, string>>();*/

        /*public Dictionary<string, Dictionary<string, List<long>>> Merges { get; set; } =
            new Dictionary<string, Dictionary<string, List<long>>>();*/
        
        public Dictionary<long, List<Tuple<string, string>>> Commits { get; set; } =
            new Dictionary<long, List<Tuple<string, string>>>();

        public Dictionary<string, State> SubStates { get; set; } = new Dictionary<string, State>();
        [Obsolete]
        public Dictionary<string, string> BranchParents { get; set; } = new Dictionary<string, string>();
    }
}
