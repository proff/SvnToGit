using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SharpSvn;

namespace SvnToGit
{
    public class SharpSvnReader : ISvnReader
    {
        private readonly ObjectPool<SvnClient> _client = new ObjectPool<SvnClient>(NewClient);

        private static SvnClient NewClient()
        {
            var client = new SvnClient();
            client.Authentication.DefaultCredentials = new NetworkCredential("test", "test");
            return client;
        }

        private readonly Uri _svnPath;

        public SharpSvnReader()
        {
            _svnPath = new Uri("file:///D:/svn/");
        }

        public long GetFirstRevision(string path, long? limit)
        {
            var uri = new Uri(_svnPath, path);
            var client = _client.GetObject();
            client.GetLog(uri,
                new SvnLogArgs(new SvnRevisionRange(SvnRevision.Zero, limit == null ? SvnRevision.Head : new SvnRevision(limit.Value)))
                {
                    RetrieveAllProperties = false,
                    RetrieveMergedRevisions = false,
                    RetrieveChangedPaths = false,
                    StrictNodeHistory = true,
                    OperationalRevision = limit == null ? SvnRevision.Head : new SvnRevision(limit.Value)
                },
                out var items);
            _client.PutObject(client);
            return items[0].Revision;
        }

        public List<long> GetRevisions(string path, long? limit)
        {
            var uri = new Uri(_svnPath, path);
            var client = _client.GetObject();
            client.GetLog(uri,
                new SvnLogArgs(new SvnRevisionRange(SvnRevision.Zero, limit == null ? SvnRevision.Head : new SvnRevision(limit.Value)))
                {
                    RetrieveAllProperties = false,
                    RetrieveMergedRevisions = false,
                    RetrieveChangedPaths = false,
                    StrictNodeHistory = true,
                    OperationalRevision = limit == null ? SvnRevision.Head : new SvnRevision(limit.Value)
                },
                out var items);
            _client.PutObject(client);
            return items.Select(a=>a.Revision).ToList();
        }

        public Revision Read(List<string> branches, long revision)
        {
            branches = branches.ToList();
            var data = new Revision();
            var args = new SvnLogArgs { Start = new SvnRevision(revision), End = new SvnRevision(revision) };
            Collection<SvnLogEventArgs> items;
            try
            {
                var client = _client.GetObject();
                client.GetLog(_svnPath, args, out items);
                _client.PutObject(client);
            }
            catch (SvnFileSystemException e)
            {
                if (e.SvnErrorCode == SvnErrorCode.SVN_ERR_FS_NO_SUCH_REVISION)
                    return null;
                throw;
            }

            data.Author = items[0].Author;
            data.Message = items[0].LogMessage;
            data.Time = items[0].Time;
            if (items[0].ChangedPaths == null)
                return data;
            foreach (var path in items[0].ChangedPaths)
            {
                string branch;
                RevisionAction action = null;
                if (path.NodeKind == SvnNodeKind.Directory && path.CopyFromPath != null)
                {
                    branch = branches.SingleOrDefault(a => path.CopyFromPath.Remove(0, 1) == a);
                    if (branch != null)
                    {
                        if (!branches.Any(a => path.Path.Remove(0, 1).StartsWith(a + "/")))
                        {
                            var value = GetBranchData(data.Data, path.Path.Remove(0, 1));
                            value.CreateBranchFrom = path.CopyFromPath.Remove(0, 1);
                            value.CreateBranchFromRevision = path.CopyFromRevision;
                            branches.Add(path.Path.Remove(0, 1));
                            continue;
                        }
                    }

                    branch = branches.SingleOrDefault(a => a.StartsWith(path.Path.Remove(0, 1) + "/"));
                    if (branch != null)
                    {
                        var value = GetBranchData(data.Data, branch);
                        value.ParentRenameFrom = path.CopyFromPath.Remove(0, 1);
                        value.ParentRenameFromRevision = path.CopyFromRevision;
                        value.ParentRenameTo = path.Path.Remove(0, 1);

                        continue;
                    }
                }

                branch = branches.SingleOrDefault(a =>
                    path.Path.Remove(0, 1).StartsWith(a + "/") || path.Path.Remove(0, 1) == a);
                if (branch == null)
                    continue;

                GetBranchData(data.Data, branch);

                if (path.Path.Remove(0, 1) == branch && path.Action != SvnChangeAction.Delete)
                {
                    var targetNew = new SvnUriTarget(new Uri(_svnPath, path.Path.Remove(0, 1)), revision);
                    var client = _client.GetObject();
                    client.GetAppliedMergeInfo(targetNew, new SvnGetAppliedMergeInfoArgs(){}, out var infoNew);

                    if (infoNew != null && infoNew.AppliedMerges.Count > 0)
                    {
                        var targetOld = new SvnUriTarget(new Uri(_svnPath, path.Path.Remove(0, 1)), revision - 1);
                        client.GetAppliedMergeInfo(targetOld, new SvnGetAppliedMergeInfoArgs(){}, out var infoOld);
                        foreach (var merge in infoNew.AppliedMerges)
                        {
                            var p = merge.Uri.AbsoluteUri.Remove(0, _svnPath.AbsoluteUri.Length);
                            if (branches.Contains(p))
                            {
                                var haveMerges = false;
                                foreach (var range in merge.MergeRanges)
                                {
                                    if (infoOld != null && infoOld.AppliedMerges.Any(a =>
                                            a.Uri == merge.Uri && a.MergeRanges.Any(b =>
                                                b.Start == range.Start && b.End == range.End)))
                                        continue;
                                    haveMerges = true;
                                    /*var d = GetBranchData(data.Data, branch);
                                    if (!d.Merges.TryGetValue(p, out var v))
                                    {
                                        v = new List<Tuple<long, long>>();
                                        d.Merges[p] = v;
                                    }

                                    v.Add(Tuple.Create(range.Start + 1, range.End));*/
                                }

                                if (haveMerges)
                                {
                                    var source = new SvnUriTarget(new Uri(_svnPath, p), revision);
                                    client.GetMergesEligible(targetOld, source, new SvnMergesEligibleArgs() {Depth = SvnDepth.Empty},
                                        out var listOld);
                                    client.GetMergesEligible(targetNew, source, new SvnMergesEligibleArgs() {Depth = SvnDepth.Empty},
                                        out var listNew);

                                    var merged = listOld.Where(a => listNew.All(b => a.Revision != b.Revision)).Select(a=>a.Revision).OrderBy(a=>a).ToList();
                                    var notMerged = listNew.Select(a => a.Revision).OrderBy(a => a).ToList();
                                    if (merged.Count > 0)
                                    {
                                        var d = GetBranchData(data.Data, branch);
                                        d.Merges[p]=new Tuple<List<long>, List<long>>(merged, notMerged);
                                    }
                                }
                            }
                        }
                    }
                    _client.PutObject(client);

                }

                var gitPath = path.Path.Remove(0, branch.Length + 1);
                if (gitPath.Length > 0)
                    gitPath = gitPath.Remove(0, 1);
                action = new RevisionAction { GitPath = gitPath, SvnPath = path.Path.Remove(0, 1) };

                if (path.CopyFromPath != null && action.GitPath == "")
                {
                    var d = GetBranchData(data.Data, branch);
                    d.CreateBranchFrom = path.CopyFromPath.Remove(0, 1);
                    d.CreateBranchFromRevision = path.CopyFromRevision;
                    continue;
                }

                /*if (path.CopyFromPath != null && (!path.CopyFromPath.Remove(0, 1).StartsWith(branch)))
                    throw new NotImplementedException();*/
                switch (path.NodeKind)
                {
                    case SvnNodeKind.File:
                        switch (path.Action)
                        {
                            case SvnChangeAction.Add:
                                action.Type = ActionType.AddFile;
                                break;
                            case SvnChangeAction.Replace:
                            case SvnChangeAction.Modify:
                                action.Type = ActionType.EditFile;
                                break;
                            case SvnChangeAction.Delete:
                                action.Type = ActionType.RemoveFile;
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        break;
                    case SvnNodeKind.Directory:
                        switch (path.Action)
                        {
                            case SvnChangeAction.Delete:
                            case SvnChangeAction.Replace:
                                if (action.GitPath == "")
                                {
                                    var value = GetBranchData(data.Data, branch);
                                    value.RemoveBranch = true;
                                    action = null;
                                }
                                else
                                    action.Type = ActionType.RemoveDir;

                                if (path.Action == SvnChangeAction.Replace)
                                    goto add;
                                break;
                            case SvnChangeAction.Add:
                                add:
                                if (path.CopyFromPath != null)
                                {
                                    var b = branches.SingleOrDefault(a => path.CopyFromPath.Remove(0, 1).StartsWith(a));
                                    if (b != null)
                                    {
                                        action.Type = ActionType.CopyDir;
                                        action.FromPath = b == path.CopyFromPath.Remove(0, 1)
                                            ? ""
                                            : path.CopyFromPath.Remove(0, b.Length + 2);
                                        action.FromRevision = path.CopyFromRevision;
                                        action.Branch = b;
                                    }
                                    else
                                    {
                                        action.Type = ActionType.CopyDirFromExternal;
                                        action.FromPath = path.CopyFromPath.Remove(0, 1);
                                        action.FromRevision = path.CopyFromRevision;
                                    }
                                }
                                else if(path.Action!=SvnChangeAction.Replace)
                                {
                                    action = null;
                                }
                                break;
                            case SvnChangeAction.Modify:
                                action = null;
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        break;
                }

                if (action != null)
                {
                    Add(data.Data, branch, action);
                }
            }

            return data;
        }

        private void Add(Dictionary<string, BranchData> data, string branch, RevisionAction action)
        {
            var value = GetBranchData(data, branch);
            value.Data.Add(action);
        }

        private static BranchData GetBranchData(Dictionary<string, BranchData> data, string branch)
        {
            if (!data.TryGetValue(branch, out var value))
            {
                value = new BranchData() { Data = new List<RevisionAction>() };
                data[branch] = value;
            }

            return value;
        }

        public string GetStartBranch(string branch)
        {
            var args = new SvnLogArgs { Start = new SvnRevision(0), End = new SvnRevision(SvnRevisionType.Head) };
            var uri = new Uri(_svnPath, branch);
            var client = _client.GetObject();
            client.GetLog(uri, args, out var items);
            _client.PutObject(client);
            return items[0].ChangedPaths[0].Path.Remove(0, 1);
        }

        public Stream GetObject(string svnPath, long revision)
        {
            var path = Guid.NewGuid().ToString();
            var svnUriTarget = new SvnUriTarget(new Uri(_svnPath, svnPath.Replace("#", "%23")), revision);
            File.Delete(path);
            var client = _client.GetObject();
            client.Export(svnUriTarget, path);
            _client.PutObject(client);
            var ms = new MemoryStream(File.ReadAllBytes(path));
            File.Delete(path);
            return ms;
        }

        public bool Exists(string path, long revision)
        {
            var exists = true;
            try
            {
                var client = _client.GetObject();
                client.GetInfo(
                    new SvnUriTarget(_svnPath + "/" + path, revision),
                    out var info);
                _client.PutObject(client);
            }
            catch (SvnRepositoryIOException)
            {
                exists = false;
            }

            return exists;

        }

        public Tuple<List<string>, List<string>> ChangedPaths(string path, long revision)
        {
            var client = _client.GetObject();
            var args = new SvnLogArgs
            {
                Start = new SvnRevision(revision), End = new SvnRevision(revision),
                OperationalRevision = new SvnRevision(revision)
            };
            client.GetLog(new Uri(_svnPath, path), args, out var items);
            _client.PutObject(client);
            return Tuple.Create(
                items[0].ChangedPaths.Where(a => a.NodeKind == SvnNodeKind.File)
                    .Select(a => a.Path.Remove(0, path.Length + 2)).ToList(),
                items[0].ChangedPaths.Where(a => a.NodeKind == SvnNodeKind.Directory && (a.Action != SvnChangeAction.Add || a.CopyFromPath!=null))
                    .Select(a => a.Path.Remove(0, path.Length + 2)).ToList());
        }

        public async Task<List<Tuple<string, long>>> SvnFiles(string uri, long revision, string path)
        {
            await Task.Yield();
            var result = new List<Tuple<string, long>>();
            var client = _client.GetObject();
            client.GetList(new SvnUriTarget(uri.Replace("#", "%23"), revision),
                new SvnListArgs()
                {
                    Depth = SvnDepth.Children,
                    Revision = revision,
                    IncludeExternals = false,
                    RetrieveEntries = SvnDirEntryItems.Size,
                    RetrieveLocks = false
                }, out var l);
            _client.PutObject(client);

            var tasks = new List<Task<List<Tuple<string, long>>>>();
            for (var i = 1; i < l.Count; i++)
            {
                var item = l[i];
                if (item.Entry.NodeKind == SvnNodeKind.Directory)
                    tasks.Add(
                        SvnFiles(uri + "/" + item.Name, revision, path == "" ? item.Name : path + "/" + item.Name));
                else
                    result.Add(Tuple.Create(path == "" ? item.Path : path + "/" + item.Path, item.Entry.FileSize));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var task in tasks)
            {
                result.AddRange(await task.ConfigureAwait(false));
            }
            
            return result;
        }

    }
}
