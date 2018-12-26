using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnToGit
{
    public interface IStateStorage
    {
        State Load();
        void Save(string description);
    }
}
