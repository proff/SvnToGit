using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SvnToGit
{
    public class StateStorage:IStateStorage
    {
        private State _state;

        public StateStorage()
        {
            _state = new State();
            if (File.Exists("c:/git/state.json"))
            {
                _state = JsonConvert.DeserializeObject<State>(File.ReadAllText("c:/git/state.json"));
            }

        }

        public State Load()
        {
            return _state;
        }

        public void Save(string description)
        {
            try
            {
            }
            finally
            {
                File.WriteAllText("c:/git/state.json", JsonConvert.SerializeObject(_state, Formatting.Indented));
            }
        }
    }
}
