using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace LivingThing.LiveBlazor
{
    public static class CLIExtension
    {
        public static Task<(int ExitCode, string StdOut)> CLI(this string command)
        {
            return Task.Run(() =>
            {
                //Console.WriteLine($"Executing \"{command}\"");
                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                cmd.StandardInput.WriteLine(command);
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();
                string response = cmd.StandardOutput.ReadToEnd();
                //Console.WriteLine($"Executed with code \"{cmd.ExitCode}\" -> \"{response}\"");
                return (cmd.ExitCode, response);
            });
        } 
    }
}
