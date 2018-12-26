using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace SvnToGit
{
    public class GitWriter : IGitWriter, IDisposable
    {
        private Repository _git;
        private string _branchName;
        private TreeDefinition _treeDefinition;
        private List<string> _added = new List<string>();
        private object _lock = new object();
        private Tree _tree;
        private Commit _commit;
        private List<Task> _tasks = new List<Task>();

        public GitWriter()
        {
            _git = new Repository("c:\\git");
        }

        public void StartCommit(string branch, string sha)
        {
            _branchName = branch;
            if (sha != null)
            {
                _commit = _git.Lookup(sha).Peel<Commit>();
                _tree = _commit.Tree;
                _treeDefinition = TreeDefinition.From(_tree);
            }
            else
            {
                _treeDefinition = new TreeDefinition();
            }

            _added.Clear();
            _tasks.Clear();
        }

        public void AddFile(string path, Func<Stream> data)
        {
            _tasks.Add(Task.Run(() =>
            {
                using (var f = data())
                {
                    var blob = _git.ObjectDatabase.CreateBlob(f, path);
                    AddFile(path, blob);
                    _added.Add(path);
                }
            }));
        }

        public void RemoveFile(string path)
        {
            lock (_lock)
            {
                _treeDefinition.Remove(path);
            }
        }

        public void RemoveDir(string path)
        {
            lock (_lock)
            {
                if (_tree == null)
                    throw new InvalidOperationException();
                var arr = GetEntries(path, _tree, "").ToArray();
                _treeDefinition.Remove(arr.Select(a => a.Item1));
                foreach (var p in _added.Where(a => a.StartsWith(path + "/")))
                {
                    _treeDefinition.Remove(p);
                }
            }
        }

        private IEnumerable<Tuple<string, Blob>> GetTree(string path, Tree tree, string path1)
        {
            var arr = path.Split(new[] { '/' }, 2);
            var entry = tree.SingleOrDefault(a => a.Path == arr[0]);
            if (entry == null)
                return Array.Empty<Tuple<string, Blob>>();
            if (arr.Length > 1)
                return GetTree(arr[1], entry.Target.Peel<Tree>(), path1 + "/" + arr[0]);
            return GetEntries(entry.Target.Peel<Tree>(), path1 + "/" + entry.Name);
        }

        private IEnumerable<Tuple<string, Blob>> GetEntries(string path, Tree tree, string path1)
        {
            if (path == "")
                return GetEntries(tree, path1);
            
            var arr = path.Split(new[] { '/' }, 2);
            var entry = tree.SingleOrDefault(a => a.Path == arr[0]);
            if (entry == null)
                return Array.Empty<Tuple<string, Blob>>();
            if (arr.Length > 1)
                return GetEntries(arr[1], entry.Target.Peel<Tree>(), path1 + (path1==""?"":"/") + arr[0]);
            return GetEntries(entry.Target.Peel<Tree>(), path1 + (path1==""?"":"/") + entry.Name);
        }

        private async Task<List<Tuple<string, Blob>>> GetEntriesAsync(string path, Tree tree, string path1)
        {
            if (path == "")
                return await GetEntriesAsync(tree, path1);
            
            var arr = path.Split(new[] { '/' }, 2);
            var entry = tree.SingleOrDefault(a => a.Path == arr[0]);
            if (entry == null)
                return new List<Tuple<string, Blob>>();
            if (arr.Length > 1)
                return await GetEntriesAsync(arr[1], entry.Target.Peel<Tree>(), path1 + (path1==""?"":"/") + arr[0]);
            return await GetEntriesAsync(entry.Target.Peel<Tree>(), path1 + (path1==""?"":"/") + entry.Name);
        }

        private async Task<List<Tuple<string, Blob>>> GetEntriesAsync(Tree tree, string path)
        {
            await Task.Yield();
            var result = new List<Tuple<string, Blob>>();
            var tasks = new List<Task<List<Tuple<string, Blob>>>>();
            foreach (var entry in tree)
            {
                switch (entry.Mode)
                {
                    case Mode.Directory:
                        tasks.Add(GetEntriesAsync(entry.Target.Peel<Tree>(), path + (path==""?"":"/") + entry.Name));
                        break;
                    case Mode.NonExecutableFile:
                        result.Add(Tuple.Create(path + (path == "" ? "" : "/") + entry.Name,
                            entry.Target.Peel<Blob>()));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                result.AddRange(await task);
            }

            return result;
        }


        private IEnumerable<Tuple<string, Blob>> GetEntries(Tree tree, string path)
        {
            foreach (var entry in tree)
            {
                switch (entry.Mode)
                {
                    case Mode.Directory:
                        foreach (var e in GetEntries(entry.Target.Peel<Tree>(), path + (path==""?"":"/") + entry.Name))
                        {
                            yield return e;
                        }
                        break;
                    case Mode.NonExecutableFile:
                        yield return Tuple.Create(path + (path==""?"":"/") + entry.Name, entry.Target.Peel<Blob>());
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public void CreateBranch(string name, string sha)
        {
            _git.Branches.Add(name, _git.Lookup<Commit>(sha), true);
        }

        public bool IsEmpty(string sha)
        {
            var commit = _git.Lookup<Commit>(sha);
            var result = true;
            foreach (var parent in commit.Parents)
            {
                if (commit.Tree.Sha != parent.Tree.Sha)
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        public string EndCommit(string author, string message, DateTime time, List<string> shas)
        {
            Task.WhenAll(_tasks).Wait();
            List<Commit> commits = new List<Commit>();
            if (_commit != null)
            {
                commits.Add(_commit);
            }

            if (shas != null)
            {
                foreach (var sha in shas)
                {
                    commits.Add(_git.Lookup<Commit>(sha));
                }
            }

            commits = commits.GroupBy(a => a).Select(a => a.First()).ToList();

            var tree = _git.ObjectDatabase.CreateTree(_treeDefinition);
            //var commit = _git.Commits.Last();
            var signature = new Signature(author, author, time);
            var commit = _git.ObjectDatabase.CreateCommit(signature,
                signature, message, tree, commits, true);
            //if (b==null)

            if (_branchName != null)
                _git.Branches.Add(_branchName.Replace("[", "_").Replace("]", "_").Replace(" ", "_"), commit, true);

            return commit.Sha;
        }

        public void RenameBranch(string name, string newName)
        {
            _git.Branches.Rename(name.Replace("[", "_").Replace("]", "_").Replace(" ", "_"), newName.Replace("[", "_").Replace("]", "_").Replace(" ", "_"), true);
        }

        public void Pack()
        {
            /*_git.Dispose();
            Process.Start(new ProcessStartInfo("git", "gc") {WorkingDirectory = "d:\\git"}).WaitForExit();
            _git = new Repository(@"d:\\git");*/
        }

        public string GetTip(string branch)
        {
            return _git.Branches[branch].Tip.Sha;
        }

        public async Task<List<Tuple<string, Blob>>> GetFilesAsync(string sha, string path)
        {
            var commit = _git.Lookup<Commit>(sha);
            var files = await GetEntriesAsync(path, commit.Tree, "");
            return files;
        }

        public async Task<List<Tuple<string, long>>> GetFilesSizes(string sha, string path)
        {
            var files = await GetFilesAsync(sha, path);
            var tasks = new List<Task<Tuple<string, long>>>();
            foreach (var file in files)
            {
                tasks.Add(GetSize(file));
            }

            await Task.WhenAll(tasks);

            var result = new List<Tuple<string, long>>();
            foreach (var task in tasks)
            {
                result.Add(await task);
            }

            return result;
        }

        private async Task<Tuple<string, long>> GetSize(Tuple<string, Blob> file)
        {
            await Task.Yield();
            return Tuple.Create(file.Item1, file.Item2.Size);
        }

        public IEnumerable<Tuple<string, Blob>> GetFiles(string sha, string path)
        {
            var commit = _git.Lookup<Commit>(sha);
            var files = GetEntries(path, commit.Tree, "");
            return files;
        }

        public void AddFile(string path, Blob file)
        {
            lock (_lock)
            {
                _treeDefinition.Add(path, file, Mode.NonExecutableFile);
                _added.Add(path);
            }
        }

        public void Dispose()
        {
            if (_git != null)
            {
                _git.Dispose();
                _git = null;
            }

        }

        public string Revert(string revertCommit, string revertOnto, State state, string mainBranch,
            List<string> commitsNeeded)
        {
            var r = _git.Lookup<Commit>(revertCommit);
            var p = r.Parents.ToList();
            var m = 0;
            if (p.Count > 1)
            {
                var c = SelectParent(state, r, p, mainBranch, commitsNeeded);
                m = p.IndexOf(c) + 1;
            }

            var result = _git.ObjectDatabase.RevertCommit(r,
                _git.Lookup<Commit>(revertOnto), m,
                new MergeTreeOptions() {/*FailOnConflict = true*/});
            if (result.Status == MergeTreeStatus.Conflicts)
                throw new RevertErrorException();
            var signature = new Signature("tmp", "tmp", DateTimeOffset.Now);
            var commit = _git.ObjectDatabase.CreateCommit(signature,
                signature, "tmp", result.Tree, new Commit[0], true);
            return commit.Sha;
        }

        public string GetParent(string sha)
        {
            return _git.Lookup<Commit>(sha).Parents.Single().Sha;
        }

        public List<string> ChangedPaths(string sha)
        {
            var commit = _git.Lookup<Commit>(sha);
            var parent = commit.Parents.Single();
            var entries = GetEntriesAsync(commit.Tree, "").Result.ToDictionary(a => a.Item1, a => a.Item2.Sha);;
            var entriesParent = GetEntriesAsync(parent.Tree, "").Result.ToDictionary(a => a.Item1, a => a.Item2.Sha);
            var result = new List<string>();
            foreach (var entry in entries)
            {
                entriesParent.TryGetValue(entry.Key, out var entryParent);
                if (entryParent == null || entry.Value!= entryParent)
                    result.Add(entry.Key);
            }

            result.AddRange(entriesParent.Where(a => !entries.ContainsKey(a.Key)).Select(a=>a.Key));
            return result;
        }

        public string AddCommit(string originCommit, string newCommit, string currentCommit, string message)
        {
            var origin = _git.Lookup<Commit>(originCommit);
            var @new = _git.Lookup<Commit>(newCommit);
            var current = _git.Lookup<Commit>(currentCommit);

            var result=_git.ObjectDatabase.CreateCommit(origin.Author, origin.Committer, origin.Message+"\n"+message, @new.Tree,
                new[] {current}, true);
            return result.Sha;
        }

        public string Base(string commit1, string commit2)
        {
            var с1 = _git.Lookup<Commit>(commit1);
            var с2 = _git.Lookup<Commit>(commit2);
            var b = _git.ObjectDatabase.FindMergeBase(с1, с2);
            return b?.Sha;
        }

        public List<string> CommitsToBase(string commit, string @base, State state, string mainBranch,
            List<string> commitsNeeded)
        {
            var result = new List<string>();
            var c = _git.Lookup<Commit>(commit);
            while (true)
            {
                var parents = c.Parents.ToList();
                if (parents.Count > 1)
                {
                    c = SelectParent(state, c, parents, mainBranch, commitsNeeded);
                    /*parents = parents.Where(a => Base(a.Sha, @base) == @base).ToList();
                    if (parents.Count > 1)
                    {
                        c = SelectParent(state, c, parents);*/
                        //parents = parents.GroupBy(a => a.Sha).Select(a => a.First()).ToList();//todo: fix duplicates in parents
                        /*c = parents.SingleOrDefault(a => allowed.Any(b => (b == a.Sha)  && !a.Message.Contains("partial merge"))); //todo: support work without metadata in messages
                        if (c == null)
                            c = parents.Single(a => allowed.Any(b =>
                                (b == a.Sha || Base(b, @base) == @base) &&
                                !a.Message.Contains(
                                    "partial merge"))); //todo: support work without metadata in messages*/
                        //throw new InvalidOperationException();
                    /*}
                    else
                    {
                        c = parents[0];
                    }*/
                }
                else
                    c = parents[0];

                commitsNeeded.Remove(c.Sha);

                if (c.Sha == @base)
                    return result;
                result.Add(c.Sha);
            }
        }

        private Commit SelectParent(State state, Commit commit, List<Commit> parents, string mainBranch,
            List<string> commitsNeeded)
        {
            parents = parents.Where(a => state.Commits.SelectMany(b => b.Value).Any(b => b.Item2 == a.Sha)).ToList();
            if (parents.Count == 1)
                return parents[0];

            /*var a1=_git.ObjectDatabase.FindMergeBase(new Commit[]{parents[0]}.Concat(commitsNeeded.Skip(1).Select(a=>_git.Lookup<Commit>(a))), MergeBaseFindingStrategy.Octopus);
            var a2=_git.ObjectDatabase.FindMergeBase(new Commit[]{parents[1]}.Concat(commitsNeeded.Skip(1).Select(a=>_git.Lookup<Commit>(a))), MergeBaseFindingStrategy.Octopus);*/

            /*var l=commitsNeeded.Select(b => parents.Where(a => Base(a.Sha, b) == b).ToList())
                .ToList();
            var t = _git.Lookup<Commit>(commitsNeeded[0]);*/
            //var l=parents.Select(a => commitsNeeded.Where(b => Base(a.Sha, b) == b).Count()).ToList();
            /*foreach (var c in commitsNeeded)
            {
                var p = parents.Where(a => Base(a.Sha, c)==c).ToList();
                if (p.Count == 1)
                    return p[0];
            }*/
            {
                var p = parents.Where(a => Base(a.Sha, commitsNeeded.Last())==commitsNeeded.Last()).ToList();
                if (p.Count == 1)
                    return p[0];
            }

            Commit result = null;
            while (mainBranch != null)
            {
                result = parents.SingleOrDefault(a =>
                    state.Commits.SelectMany(b => b.Value).Any(b => b.Item1 == mainBranch && a.Sha == b.Item2));
                if (result != null)
                    break;
                state.BranchParents.TryGetValue(mainBranch, out mainBranch);
            }

            if (result == null)
                throw new InvalidOperationException();
            return result;

            /*if (mainBranch == null)
                mainBranch = state.Commits.SelectMany(a => a.Value).Where(a => a.Item2 == commit.Sha)
                    .Select(a => a.Item1)
                    .Single();

            if (result == null)
            {
                throw new InvalidOperationException();
            }
            /*if (result == null)
            {
                var data = state.Commits.SelectMany(a => a.Value).Single(a => a.Item2 == commit.Sha);
                result = parents.Single(a => state.Commits.SelectMany(b => b.Value).Any(b => b.Item1 == data.Item1 && a.Sha == b.Item2));
            }*/

            //return result;
        }
    }
}
