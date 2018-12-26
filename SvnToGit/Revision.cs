using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnToGit
{
    public class Revision
    {
        public Revision()
        {
            Data = new Dictionary<string, BranchData>();
        }

        public string Message { get; set; }
        public string Author { get; set; }
        public Dictionary<string, BranchData> Data { get; set; }
        public DateTime Time { get; set; }
    }
}
