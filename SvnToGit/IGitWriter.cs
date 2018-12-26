using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace SvnToGit
{
    public interface IGitWriter:IDisposable
  {
      void StartCommit(string branch, string sha);
      void AddFile(string path, Func<Stream> data);
      void RemoveFile(string path);
      void RemoveDir(string path);
      string EndCommit(string author, string message, DateTime time, List<string> shas);
      void RenameBranch(string name, string newName);
      void Pack();
      string GetTip(string branch);
      IEnumerable<Tuple<string, Blob>> GetFiles(string sha, string path);
      void AddFile(string path, Blob file);
      Task<List<Tuple<string, Blob>>> GetFilesAsync(string sha, string path);
      Task<List<Tuple<string, long>>> GetFilesSizes(string sha, string path);
      string Revert(string revertCommit, string revertOnto, State allowed, string mainBranch,
          List<string> commitsNeeded);
      string AddCommit(string originCommit, string newCommit, string currentCommit, string message);
      string GetParent(string sha);
      List<string> ChangedPaths(string sha);
      string Base(string commit1, string commit2);
      List<string> CommitsToBase(string commit, string @base, State allowed, string mainBranch,
          List<string> commitsNeeded);
      void CreateBranch(string name, string sha);
      bool IsEmpty(string sha);
  }
}
