using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace LivingThing.LiveBlazor
{
    public static class CLIExtension
    {
        public static Task<(int ExitCode, string StdOut)> CLI(this string command, bool runDirect = false)
        {
            return Task.Run(() =>
            {
                Process process = new System.Diagnostics.Process();
                ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/c " + command;
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                process.StartInfo = startInfo;
                process.Start();
                var response = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return (process.ExitCode, response);

                ////Console.WriteLine($"Executing \"{command}\"");
                //Process cmd = new Process();
                //cmd.StartInfo.FileName = !runDirect ? "cmd.exe" : command;
                //cmd.StartInfo.RedirectStandardInput = true;
                //cmd.StartInfo.RedirectStandardOutput = true;
                //cmd.StartInfo.CreateNoWindow = true;
                //cmd.StartInfo.UseShellExecute = false;
                //cmd.Start();

                //if (!runDirect)
                //{
                //    cmd.StandardInput.WriteLine(command);
                //}
                //cmd.StandardInput.Flush();
                //cmd.StandardInput.Close();
                //cmd.WaitForExit();
                //string response = cmd.StandardOutput.ReadToEnd();
                ////Console.WriteLine($"Executed with code \"{cmd.ExitCode}\" -> \"{response}\"");
                //return (cmd.ExitCode, response);
            });
        } 
    }
}
