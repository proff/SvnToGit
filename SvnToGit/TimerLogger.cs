using System;
using System.Diagnostics;
using System.IO;

namespace SvnToGit
{
    public class TimerLogger : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _timer;

        public TimerLogger(string name)
        {
            _name = name;
            _timer = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            File.AppendAllText("log", _name + ": " + _timer.Elapsed.TotalSeconds + " seconds" + Environment.NewLine);
        }
    }
}