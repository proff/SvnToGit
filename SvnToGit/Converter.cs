using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SvnToGit
{
    public class Converter:IDisposable
    {
        private readonly string _root;
        private readonly ISvnReader _reader;
        private IGitWriter _writer;
        private readonly IStateStorage _storage;
        private State _state;
        private readonly string _prefix;
        private readonly string _descriptionSuffix;
        private readonly bool _ignoreBranches;
        private readonly bool _master;
        private Random _random = new Random();

        public Converter(string root, ISvnReader reader, IGitWriter writer, IStateStorage storage, State state, string prefix, string descriptionSuffix, bool ignoreBranches, bool master)
        {
            _root = root;
            _reader = reader;
            _writer = writer;
            _storage = storage;
            _state = state;
            _prefix = prefix;
            _descriptionSuffix = descriptionSuffix;
            _ignoreBranches = ignoreBranches;
            _master = master;
        }

        public string Convert()
        {
            string result = null;
            if (_state == null)
                _state = _storage.Load();
            if (_state.StartBranch == null)
            {
                _state.StartBranch = _root;// rdr.GetStartBranch(trunk);
                _state.Branches = new List<string>() { _state.StartBranch };
                if (!_ignoreBranches)
                    _state.Revision = _reader.GetFirstRevision(_state.StartBranch, null);
            }

            List<long> revs = null;
            var revsIndex = 0;
            if (_ignoreBranches)
            {
                revs = _reader.GetRevisions(_root, null);
                if (_state.Revision == 0)
                {
                    _state.Revision = revs[0];
                }
                else
                {
                    revsIndex = revs.IndexOf(_state.Revision);
                }
            }

            bool anyData = false;
            while (true)
            {
                var data = _reader.Read(_state.Branches, _state.Revision);
                if (data == null)
                {
                    var sha = _state.Commits.OrderByDescending(a=>a.Key).SelectMany(a=>a.Value).First(a => a.Item1==_root).Item2;
                    //var sha = _state.Commits[_root][_state.Commits[_root].Keys.Max()];
                    result = sha;
                    break;
                }

                anyData = false;
                foreach (var branch in data.Data)
                {
                    var mergeString = "";
                    string sha = null;
                    List<string> shas = new List<String>();
                    if (false && branch.Value.Merges.Count > 0)
                    {
                        foreach (var newMerge in branch.Value.Merges)
                        {
                            var toMerge = newMerge.Value.Item1;
                            var notMerged = newMerge.Value.Item2;
                            for (int i = notMerged.Count - 1; i >= 0; i--)
                            {
                                if (_writer.IsEmpty(_state.Commits[notMerged[i]].Single().Item2))
                                //if (_writer.IsEmpty(_state.Commits[newMerge.Key][notMerged[i]]))
                                    notMerged.RemoveAt(i);
                            }

                            //Console.WriteLine($"processing merge\n{string.Join(", ", toMerge)}\nwithout\n{string.Join(", ", notMerged.Where(a=>a<toMerge[toMerge.Count-1]))}\nfrom {newMerge.Key} to {branch.Key}");

                            if (notMerged.Count == 0)
                            {
                                shas.Add(_state.Commits.OrderByDescending(a=>a.Key).SelectMany(a=>a.Value).First(a => a.Item1==newMerge.Key).Item2);
                                //shas.Add(_state.Commits[newMerge.Key][_state.Commits[newMerge.Key].Max(a=>a.Key)]);
                                continue;
                            }

                            if (toMerge[toMerge.Count-1] < notMerged[0])
                            {
                                shas.Add(_state.Commits.Where(a=>a.Key<notMerged[0]).OrderByDescending(a=>a.Key).SelectMany(a=>a.Value).First(a => a.Item1==newMerge.Key).Item2);
                                //shas.Add(_state.Commits[newMerge.Key][_state.Commits[newMerge.Key].Where(a=>a.Key<notMerged[0]).Max(a=>a.Key)]);
                                continue;
                            }
                            continue;

                            for (int i = 0; i < toMerge.Count; i++)
                            {
                                if (toMerge[i] > notMerged[0])
                                {
                                    if (i == 0)
                                        break;
                                    shas.Add(_state.Commits[toMerge[i-1]].Single().Item2);
                                    //shas.Add(_state.Commits[newMerge.Key][toMerge[i-1]]);
                                    toMerge.RemoveRange(0, i);
                                    break;
                                }
                            }

                            try
                            {

                                var commitsNeeded = toMerge.Union(notMerged).OrderBy(a => a)
                                    .Select(a => _state.Commits[a]).Select(a => a.Single().Item2).ToList();
                                /*var q = _state.Commits
                                    .Where(a => a.Key < Math.Min(toMerge[0], notMerged[0]))
                                    .OrderByDescending(a => a.Key).SelectMany(a => a.Value)
                                    .Where(a => a.Item1 == newMerge.Key).Select(a => a.Item2).Take(1);
                                var commitsNeeded = q.Concat(toMerge.Union(notMerged).OrderBy(a => a)
                                        .Select(a => _state.Commits[a]).Select(a => a.Single().Item2)).ToList();*/

                                var commit = _state.Commits.OrderByDescending(a => a.Key).SelectMany(a => a.Value)
                                    .First(a => a.Item1 == newMerge.Key).Item2;
                                //var commit = _state.Commits[newMerge.Key][_state.Commits[newMerge.Key].Max(a => a.Key)];
                                var ba = _writer.Base(
                                    _state.Commits.OrderByDescending(a => a.Key).SelectMany(a => a.Value)
                                        .First(a => a.Item1 == branch.Key).Item2, commit);
                                commitsNeeded.Insert(0, ba);
                                //var ba = _writer.Base(_state.Commits[branch.Key][_state.Commits[branch.Key].Max(a => a.Key)],commit);
                                /*var allowed = _state.Commits.SelectMany(a => a.Value)
                                    .Where(a => a.Item1 == newMerge.Key).Select(a => a.Item2).ToList();*/
                                //var allowed = _state.Commits[newMerge.Key].Select(a => a.Value).ToList();
                                var commits = _writer.CommitsToBase(commit, ba, _state, newMerge.Key /*branch.Key*/ /*null*/, commitsNeeded);
                                commits.Reverse();
                                commits.Add(commit);
                                //var commitsAndAllowed = allowed.Union(commits).ToList();
                                //var merged = 0;

                                List<Tuple<string, Task<string>>> tasks = new List<Tuple<string, Task<string>>>();

                                for (int i = 0; i < commits.Count; i++)
                                {
                                    var rev = _state.Commits.Single(a => a.Value.Any(b => b.Item2 == commits[i])).Key;
                                    /*var arr = _state.Commits[newMerge.Key].Where(a => a.Value == commits[i]).ToArray();
                                    var rev = arr.Single().Key;*/
                                    if (toMerge.Contains(rev))
                                    {
                                        var i1 = i;
                                        Func<string>f=() =>
                                        {
                                            var s = commits[i1];
                                            for (int j = i1 - 1; j >= 0; j--)
                                            {
                                                var rev2 = _state.Commits
                                                    .Single(a => a.Value.Any(b => b.Item2 == commits[j])).Key;
                                                //var rev2 = _state.Commits[newMerge.Key].Where(a => a.Value == commits[j]).Single().Key;
                                                if (!toMerge.Contains(rev2))
                                                {
                                                    //s = _writer.Revert(commits[j], s, commits);
                                                    s = _writer.Revert(commits[j], s, _state, newMerge.Key, commitsNeeded);
                                                }
                                            }

                                            return s;
                                        };
                                        tasks.Add(Tuple.Create(commit, Task.Run(f)));

                                        /*ba = _writer.AddCommit(commits[i], s, ba,
                                            $"partial merge from {newMerge.Key} {rev} to {branch.Key}");
                                        if (!CheckPartial(newMerge.Key, rev, ba))
                                        {
                                            _writer.CreateBranch("LastError", ba);
                                            throw new InvalidOperationException();
                                        }

                                        merged++;*/
                                        /*if (toMerge.Count == merged)
                                            break;*/
                                    }
                                }

                                ;

                                foreach (var task in tasks)
                                {
                                    var rev = _state.Commits.Single(a => a.Value.Any(b => b.Item2 == task.Item1)).Key;
                                    ba = _writer.AddCommit(task.Item1, task.Item2.Result, ba,
                                        $"partial merge from {newMerge.Key} {rev} to {branch.Key}");
                                    if (!CheckPartial(newMerge.Key, rev, ba))
                                    {
                                        _writer.CreateBranch("LastError", ba);
                                        throw new InvalidOperationException();
                                    }
                                }

                                shas.Add(ba);
                            }
                            catch (RevertErrorException)
                            {
                                mergeString = $"merge {string.Join(", ", toMerge)} from {newMerge.Key}\n";
                            }
                            catch (AggregateException e)
                            {
                                if (e.InnerExceptions.First() is RevertErrorException)
                                    mergeString = $"merge {string.Join(", ", toMerge)} from {newMerge.Key}\n";
                                else
                                    throw;
                            }

                        /*List<long> oldMerges;
                        if (!_state.Merges.TryGetValue(branch.Key, out var oldMergesRoot))
                        {
                            oldMergesRoot = new Dictionary<string, List<long>>();
                            _state.Merges[branch.Key] = oldMergesRoot;
                        }
                        if (!oldMergesRoot.TryGetValue(newMerge.Key, out oldMerges))
                        {
                            oldMerges = new List<long>();
                            oldMergesRoot[newMerge.Key] = oldMerges;
                        }

                        List<KeyValuePair<long, string>> notMerged = new List<KeyValuePair<long, string>>();
                        if (_state.Commits.TryGetValue(newMerge.Key, out var com))
                        {
                            var min = _state.Commits[branch.Key].Min(a => a.Key);
                            com = com.Where(a => a.Key > min).ToDictionary(a => a.Key, a => a.Value);
                            notMerged = com.Where(a => !oldMerges.Contains(a.Key))
                                .OrderBy(a => a.Key).ToList();
                        }

                        List<long> toMerge = new List<long>();
                        foreach (var tuple in newMerge.Value)
                        {
                                var s = notMerged.Where(a => a.Key >= tuple.Item1 && a.Key <= tuple.Item2)
                                    .Select(a => a.Key).ToList();
                                
                                toMerge.AddRange(s);
                        }
                        if (toMerge.Count == notMerged.Count && toMerge.Count > 0)
                        {
                            shas.Add(com[com.Max(a=>a.Key)]);
                        }
                        else if(toMerge.Count>0)
                        {
                            try
                            {
                                var commit =
                                    _state.Commits[newMerge.Key][_state.Commits[newMerge.Key].Max(a => a.Key)];
                                var ba = _writer.Base(
                                    _state.Commits[branch.Key][_state.Commits[branch.Key].Max(a => a.Key)],
                                    commit);
                                var allowed = _state.Commits[newMerge.Key].Select(a => a.Value).ToList();
                                var commits = _writer.CommitsToBase(commit, ba,
                                    allowed);
                                commits.Reverse();
                                commits.Add(commit);
                                var merged = 0;
                                for (int i = 0; i < commits.Count; i++)
                                {
                                    var rev = _state.Commits[newMerge.Key].Where(a => a.Value == commits[i])
                                        .Single().Key;
                                    if (toMerge.Contains(rev))
                                    {
                                        var s = commits[i];
                                        for (int j = i - 1; j >= 0; j--)
                                        {
                                            var rev2 = _state.Commits[newMerge.Key]
                                                .Where(a => a.Value == commits[j]).Single().Key;
                                            if (!toMerge.Contains(rev2))
                                            {
                                                s = _writer.Revert(commits[j], s, allowed);
                                            }
                                        }

                                        ba = _writer.AddCommit(commits[i], s, ba,
                                            $"partial merge from {newMerge.Key} {rev} to {branch.Key}");
                                        if (!CheckPartial(newMerge.Key, rev, ba))
                                        {
                                            _writer.CreateBranch("LastError", ba);
                                            throw new InvalidOperationException();
                                        }

                                        merged++;
                                        if (toMerge.Count == merged)
                                            break;
                                    }
                                }

                                shas.Add(ba);
                            }
                            catch (RevertErrorException)
                            {
                                mergeString = $"merge {string.Join(", ", toMerge)} from {newMerge.Key}\n";
                                shas.Clear();
                            }

                        }
                        oldMerges.AddRange(toMerge);*/
                        }
                    }
                    if (branch.Value.CreateBranchFrom != null)
                    {
                        if (_state.Commits.SelectMany(a => a.Value).All(a => a.Item1 != branch.Value.CreateBranchFrom))
                        //if (!_state.Commits.TryGetValue(branch.Value.CreateBranchFrom, out var cc))
                        {
                            var storage = _storage;
                            var key = branch.Value.CreateBranchFrom + "|" + branch.Value.CreateBranchFromRevision;
                            if (!_state.SubStates.TryGetValue(key, out var s))
                            {
                                s = new State();
                                _state.SubStates[key] = s;

                            }

                            using (var converter = new Converter(branch.Value.CreateBranchFrom,
                                new LimitedReader(branch.Value.CreateBranchFromRevision, _reader), new GitWriter(),
                                storage, s, _prefix,
                                $"create branch \"{branch.Key}\" from \"{branch.Value.CreateBranchFrom}\" revision \"{branch.Value.CreateBranchFromRevision}\"\n" +
                                _descriptionSuffix, _ignoreBranches, _master && branch.Key == _root))
                            {
                                sha = converter.Convert();
                            }

                            /*foreach (var commit in storage.State.Commits)
                            {
                                if (!_state.Commits.ContainsKey(commit.Key))
                                    _state.Commits[commit.Key] = new Dictionary<long, string>();
                                foreach (var commit1 in commit.Value)
                                {
                                    if (!_state.Commits[commit.Key].ContainsKey(commit1.Key))
                                        _state.Commits[commit.Key][commit1.Key] = commit1.Value;
                                }
                            }*/
                        }
                        else
                        {
                            sha = _state.Commits.OrderByDescending(a => a.Key)
                                .Where(a => a.Key <= branch.Value.CreateBranchFromRevision).SelectMany(a => a.Value)
                                .First(a => a.Item1 == branch.Value.CreateBranchFrom).Item2;
                            //sha = cc[cc.Keys.Where(a => a <= branch.Value.CreateBranchFromRevision).Max()];
                            /*if (branch.Value.Data.Count() == 0)
                            {
                                _state.Merges[branch.Key] = new Dictionary<string, List<long>>()
                                    {{branch.Value.CreateBranchFrom, new List<long>() {_state.Revision}}};
                            }*/
                        }

                        _state.BranchParents[branch.Key] = branch.Value.CreateBranchFrom;

                        if (!_state.Branches.Contains(branch.Key))
                            _state.Branches.Add(branch.Key);
                        /*if(_state.Commits.ContainsKey(branch.Key))
                            throw new InvalidOperationException();
                        if (_state.Commits.ContainsKey(branch.Value.CreateBranchFrom))
                            _state.Commits[branch.Key] = _state.Commits[branch.Value.CreateBranchFrom]
                                .ToDictionary(a => a.Key, a => a.Value);*/
                    }
                    else if (branch.Value.ParentRenameFrom != null)
                    {
                        var exists = _reader.Exists(branch.Key, _state.Revision);
                        if (exists)
                        {
                            var storage = _storage;
                            var b = branch.Value.ParentRenameFrom +
                                    branch.Key.Remove(0, branch.Value.ParentRenameTo.Length);
                            var key = b + "|" + branch.Value.ParentRenameFromRevision;
                            if (!_state.SubStates.TryGetValue(key, out var s))
                            {
                                s = new State();
                                _state.SubStates[key] = s;

                            }

                            using (var converter = new Converter(b,
                                new LimitedReader(branch.Value.ParentRenameFromRevision, _reader), new GitWriter(),
                                storage, s, _prefix,
                                $"create branch \"{branch.Key}\" by renaming directory \"{branch.Value.ParentRenameFrom}\" revision \"{branch.Value.CreateBranchFromRevision}\" to branch parent \"{branch.Value.ParentRenameTo}\"\n" +
                                _descriptionSuffix, _ignoreBranches, _master && branch.Key == _root))
                            {
                                sha = converter.Convert();
                            }

                            /*foreach (var commit in storage.State.Commits)
                            {
                                if (!_state.Commits.ContainsKey(commit.Key))
                                    _state.Commits[commit.Key] = new Dictionary<long, string>();
                                foreach (var commit1 in commit.Value)
                                {
                                    if (!_state.Commits[commit.Key].ContainsKey(commit1.Key))
                                        _state.Commits[commit.Key][commit1.Key] = commit1.Value;
                                }
                            }*/
                        }
                    }
                    else
                    {
                        sha = _state.Commits.OrderByDescending(a => a.Key).SelectMany(a => a.Value)
                            .Where(a => a.Item1 == branch.Key).Select(a => a.Item2).FirstOrDefault();
                        /*if (_state.Commits.TryGetValue(branch.Key, out var a))
                        {
                            if (a.Count > 0)
                                sha = a[a.Keys.Max()];
                        }*/
                    }

                    //if (branch.Value.Data.Any() || branch.Value.CreateBranchFrom != null)
                    {
                        var n = branch.Key;
                        if (_prefix != null)
                            n = _prefix + "/" + n;
                        if (_ignoreBranches)
                            n = null;
                        Console.WriteLine("branch " + branch.Key + " " + _state.Revision);
                        _writer.StartCommit(_master && branch.Key == _root ? "master" : n, sha);
                        foreach (var action in branch.Value.Data)
                        {
                            action.GitPath = ApplyPrefix(action.GitPath);
                            switch (action.Type)
                            {
                                case ActionType.AddFile:
                                case ActionType.EditFile:
                                    _writer.AddFile(action.GitPath, ()=>_reader.GetObject(action.SvnPath, _state.Revision));

                                    var t = action.Type == ActionType.AddFile ? "A" : "M";
                                    Console.WriteLine($"{t} {action.GitPath}");
                                    break;
                                case ActionType.RemoveFile:
                                    _writer.RemoveFile(action.GitPath);
                                    Console.WriteLine($"D {action.GitPath}");
                                    break;
                                case ActionType.RemoveDir:
                                    _writer.RemoveDir(action.GitPath);
                                    Console.WriteLine($"D {action.GitPath}");
                                    break;
                                case ActionType.CopyDir:
                                    _writer.RemoveDir(action.GitPath);
                                    var sha1 = GetSha(action.Branch, action.FromRevision);
                                    var pp = action.FromPath;
                                    if (_prefix != null)
                                        pp = _prefix + "/" + action.FromPath;
                                    var files = _writer.GetFiles(sha1, pp);//todo: change to tree
                                    foreach (var file in files)
                                    {
                                        var p = file.Item1.Remove(0, pp.Length+1);
                                        _writer.AddFile(action.GitPath + "/" + p, file.Item2);
                                        Console.WriteLine($"A {file.Item1}");
                                    }
                                    break;
                                case ActionType.CopyDirFromExternal:
                                    var key = action.FromPath + "|" + action.FromRevision;
                                    if (!_state.SubStates.TryGetValue(key, out var s))
                                    {
                                        s = new State();
                                        _state.SubStates[key] = s;

                                    }

                                    using (var converter = new Converter(action.FromPath,
                                        new LimitedReader(action.FromRevision, _reader), new GitWriter(),
                                        _storage, s, action.GitPath,
                                        $"copy external directory \"{action.FromPath}\" revision {action.FromRevision} to path \"{action.GitPath}\" of branch \"{branch.Key}\"\n" +
                                        _descriptionSuffix, true, false))
                                    {
                                        sha1 = converter.Convert();
                                    }

                                    files = _writer.GetFiles(sha1, action.GitPath);
                                    foreach (var file in files)
                                    {
                                        _writer.AddFile(file.Item1, file.Item2);
                                        Console.WriteLine($"A {file.Item1}");
                                    }

                                    shas.Add(sha1);
                                    break;
                                default:
                                    throw new NotImplementedException();
                            }
                        }//);

                        sha = _writer.EndCommit(data.Author,
                            data.Message + $"\n\nbranch {branch.Key} revision {_state.Revision}\n{mergeString}{_descriptionSuffix}",
                            data.Time, shas);
                    }

                    if (branch.Value.RemoveBranch)
                    {
                        _state.Branches.Remove(branch.Key);
                        var n = "/" + branch.Key;
                        if (_prefix != null)
                            n = "/" + _prefix + n;
                        _writer.RenameBranch(n, "removed" + n + "_" + _state.Revision);
                        continue;
                    }

                    /*if (!_state.Commits.TryGetValue(branch.Key, out var c))
                    {
                        c = new Dictionary<long, string>();
                        _state.Commits[branch.Key] = c;
                    }*/

                    if (sha != null)
                    {
                        if (!_state.Commits.TryGetValue(_state.Revision, out var v))
                        {
                            v = new List<Tuple<string, string>>();
                            _state.Commits[_state.Revision] = v;
                        }
                        v.Add(Tuple.Create(branch.Key, sha));
                        //c[_state.Revision] = sha;
                        if (!Check(branch.Key, _state.Revision, sha))
                        {
                            _writer.CreateBranch("LastError", sha);
                            throw new InvalidOperationException();
                        }

                        anyData = true;
                    }
                }

                if (anyData)
                    Console.WriteLine("end " + _state.Revision);
                if (_ignoreBranches)
                {
                    if (revsIndex >= revs.Count - 1)
                        _state.Revision++;
                    else
                        _state.Revision = revs[++revsIndex];
                }
                else
                    _state.Revision++;

                    //_writer.Pack();
                if (anyData)
                    _storage.Save("revision: " + (_state.Revision-1) + "\n" + _descriptionSuffix);
                if (anyData && _random.Next(100) == 0)
                    throw new NeedGcException();
            }
            if (!anyData)
                _storage.Save("revision: " + _state.Revision + "\n" + _descriptionSuffix);
            return result;
        }

        private bool CheckPartial(string branch, long revision, string sha)
        {
            return true;
            /*if (revision < 16546)
                return true;*/
            var svnPaths = _reader.ChangedPaths(branch, revision);
            var gitPaths = _writer.ChangedPaths(sha);
            gitPaths = gitPaths.Where(a => !svnPaths.Item2.Any(b => a.StartsWith(b + "/"))).ToList();
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            var result = true;
            foreach (var svnPath in svnPaths.Item1)
            {
                if (!gitPaths.Contains(svnPath))
                {
                    Console.WriteLine(svnPath + " is not exists on git");
                    result = false;
                }
            }
            foreach (var gitPath in gitPaths)
            {
                if (!svnPaths.Item1.Contains(gitPath))
                {
                    Console.WriteLine(gitPath + " is not exists on svn");
                    result = false;
                }
            }
            Console.ForegroundColor = color;
            return result;
        }

        private string ApplyPrefix(string path)
        {
            if (_prefix == null)
                return path;
            return _prefix + "/" + path;
        }


        private bool Check(string branch, long revision, string sha)
        {
            return true;
            /*if (revision < 34853)
                return true;*/
            var svnFiles = _reader.SvnFiles("file:///D:/svn/" + branch, revision, "").Result.ToDictionary(a=>a.Item1, a=>a.Item2);
            /*var svn = new SvnClient();
            svn.Authentication.DefaultCredentials = new NetworkCredential("test", "test");
            svn.GetList(new SvnUriTarget("file:///C:/svn/" + branch, revision),
                new SvnListArgs()
                {
                    Depth = SvnDepth.Infinity,
                    Revision = revision,
                    IncludeExternals = false,
                    RetrieveEntries = SvnDirEntryItems.Size,
                    RetrieveLocks = false
                }, out var l);
            var svnFiles = l.Where(a => a.Entry.NodeKind == SvnNodeKind.File)
                .Select(a => new {Path = a.Path, Size = a.Entry.FileSize}).ToList();*/
            var gitFiles = _writer.GetFilesSizes(sha, "").Result.ToDictionary(a=>a.Item1, a=>a.Item2);

            var result = true;
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
          
            foreach (var svnFile in svnFiles)
            {
                var path = svnFile.Key;
                var gitPath = path;
                if (_prefix != null)
                    gitPath = _prefix+"/" + path;
                if (!gitFiles.TryGetValue(gitPath, out var gitFile))
                {
                    Console.WriteLine($"file \"git/{path}\" does not exists");
                    result = false;
                    continue;
                }

                gitFiles.Remove(gitPath);

                if (svnFile.Value != gitFile)
                {
                    Console.WriteLine($"files at path \"{path}\" does not match");
                    result = false;
                }
            }

            foreach (var gitFile in gitFiles)
            {
                var path = gitFile.Key;
                Console.WriteLine($"file \"svn/{path}\" does not exists");
                result = false;
            }
            Console.ForegroundColor = color;
            return result;
        }

        private string GetSha(string branch, long revision)
        {
            return _state.Commits.OrderByDescending(a => a.Key).Where(a => a.Key <= revision).SelectMany(a => a.Value)
                .Where(a => a.Item1 == branch).Select(a=>a.Item2).FirstOrDefault();
            /*if (_state.Commits.TryGetValue(branch, out var value))
            {
                return value[value.Keys.Where(a => a <= revision).Max()];
            }
            return null;*/
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
        }
    }
}
