using System;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using Newtonsoft.Json;

namespace SvnToGit
{
    public class GitStateStorage:IStateStorage
    {
        private Repository _git;
        private State _state;

        public GitStateStorage()
        {
            _git = new Repository(@"c:\git");
        }

        public State Load()
        {
            if (_state == null)
            {
                var branch = _git.Branches.SingleOrDefault(a => a.FriendlyName == "SvnToGitState");
                if (branch != null)
                {
                    var blob = branch.Tip.Tree["SvnToGitState.json"].Target.Peel<Blob>();
                    var s = blob.GetContentText();
                    _state = JsonConvert.DeserializeObject<State>(s);
                }

                if (_state == null)
                {
                    _state = new State();
                }
            }

            return _state;
        }

        public void Save(string description)
        {
            var state = JsonConvert.SerializeObject(_state, Formatting.Indented);
            var blob = _git.ObjectDatabase.CreateBlob(new MemoryStream(Encoding.UTF8.GetBytes(state)), "SvnToGitState.json");
            var treeDef = new TreeDefinition();
            treeDef.Add("SvnToGitState.json", blob, Mode.NonExecutableFile);
            var tree = _git.ObjectDatabase.CreateTree(treeDef);
            var signature=new Signature("SvnToGit", "SvnToGit", DateTimeOffset.Now);
            var branch = _git.Branches.SingleOrDefault(a => a.FriendlyName == "SvnToGitState");
            var parents = branch == null ? new Commit[0] : new[]{branch.Tip};
            var commit = _git.ObjectDatabase.CreateCommit(signature, signature, description, tree, parents, true);
            _git.Branches.Add("SvnToGitState", commit, true);
        }
    }
}
