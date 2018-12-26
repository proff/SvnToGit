using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnToGit
{
    class LimitedReader:ISvnReader
    {
        private readonly long _limit;
        private readonly ISvnReader _redaer;

        public LimitedReader(long limit, ISvnReader redaer)
        {
            _limit = limit;
            _redaer = redaer;
        }

        public Revision Read(List<string> branches, long revision)
        {
            if (revision > _limit)
                return null;
            return _redaer.Read(branches, revision);
        }

        public string GetStartBranch(string branch)
        {
            return _redaer.GetStartBranch(branch);
        }

        public Stream GetObject(string svnPath, long revision)
        {
            return _redaer.GetObject(svnPath, revision);
        }

        public bool Exists(string path, long revision)
        {
            return _redaer.Exists(path, revision);
        }

        public long GetFirstRevision(string path, long? limit)
        {
            return _redaer.GetFirstRevision(path, limit??_limit);
        }

        public List<long> GetRevisions(string path, long? limit)
        {
            return _redaer.GetRevisions(path, limit??_limit);
        }

        public Task<List<Tuple<string, long>>> SvnFiles(string uri, long revision, string path)
        {
            return _redaer.SvnFiles(uri, revision, path);
        }

        public Tuple<List<string>, List<string>> ChangedPaths(string path, long revision)
        {
            return _redaer.ChangedPaths(path, revision);
        }
    }
}
