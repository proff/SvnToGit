using System;
using System.Diagnostics;
using System.IO;

namespace SvnToGit
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            var host = args.Length == 0;
            while (true)
            {
                try
                {
                    if (host && !Debugger.IsAttached)
                    {
                        var process = Process.Start(new ProcessStartInfo("SvnToGit", "1")
                            {WorkingDirectory = Directory.GetCurrentDirectory(), UseShellExecute = false});
                        process.WaitForExit();
                        if (process.ExitCode == 1)
                            throw new NeedGcException();
                    }
                    else
                    {

                        using (var converter = new Converter("Source/trunk", new SharpSvnReader(), new GitWriter(),
                            new GitStateStorage(), null, null, "", false, true))
                        {
                            converter.Convert();
                        }
                    }
                    break;

                }
                catch (NeedGcException)
                {
                    if (host)
                    {
                        Process.Start(new ProcessStartInfo("git", "prune")
                            {WorkingDirectory = "c:\\git", UseShellExecute = false}).WaitForExit();
                        Process.Start(new ProcessStartInfo("git", "gc --auto")
                            {WorkingDirectory = "c:\\git", UseShellExecute = false}).WaitForExit();
                    }
                    else
                    {
                        return 1;
                    }
                }
            }

            return 0;
        }
    }
}