using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SvnToGit
{
    public interface ISvnReader
    {
        Revision Read(List<string> branches, long revision);
        string GetStartBranch(string branch);
        Stream GetObject(string svnPath, long revision);
        bool Exists(string path, long revision);
        long GetFirstRevision(string path, long? limit);
        List<long> GetRevisions(string path, long? limit);
        Task<List<Tuple<string, long>>> SvnFiles(string uri, long revision, string path);
        Tuple<List<string>, List<string>> ChangedPaths(string path, long revision);
    }
}
