using System;
using System.IO;
using System.Linq;
using Phantasma.Spook.Utils;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("clear", Category = "Builtins")]
        protected void OnClearCommand()
        {
            Console.Clear();
        }

        [ConsoleCommand("exit", Category = "Builtins")]
        [ConsoleCommand("quit", Category = "Builtins")]
        protected void OnQuitCommand()
        {
            _cli.Terminate();
        }

        [ConsoleCommand("pwd", Category = "Builtins")]
        protected void OnPWDCommand()
        {
            Console.WriteLine(Directory.GetCurrentDirectory());
        }

        [ConsoleCommand("cd ", Category = "Builtins")]
        protected void OnCdCommand(string path)
        {
            string envVar = "";
            if (path.Equals("~"))
            {
                envVar = Environment.GetEnvironmentVariable("HOME");
            }

            if (string.IsNullOrEmpty(envVar))
            {
                Directory.SetCurrentDirectory(path);
            }
            else
            {
                Directory.SetCurrentDirectory(envVar);
            }
        }

        [ConsoleCommand("ls", Category = "Builtins")]
        protected void OnLsCommand()
        {
            try
            {
                var currentDir = Directory.GetCurrentDirectory();
                var files = Directory.GetFiles(currentDir);
                var dirs = Directory.GetDirectories(currentDir);

                var all = files.Concat(dirs);

                foreach (var entry in all)
                {
                    Console.WriteLine(entry);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Fetching directory content failed, permission denied!");
            }
        }

        //TODO
        //[ConsoleCommand("|", Category = "Builtins")]
        //protected void OnPipeCommand(string input)
        //{
        //    try
        //    {
        //        var currentDir = Directory.GetCurrentDirectory();
        //        var files = Directory.GetFiles(currentDir);
        //        var dirs = Directory.GetDirectories(currentDir);

        //        var all = files.Concat(dirs);

        //        foreach (var entry in all)
        //        {
        //            Console.WriteLine(entry);
        //        }
        //    }
        //    catch (Exception)
        //    {
        //        Console.WriteLine("Fetching directory content failed, permission denied!");
        //    }
        //}

        [ConsoleCommand("edit config", Category = "Builtins")]
        [ConsoleCommand("edit history", Category = "Builtins")]
        [ConsoleCommand("edit", Category = "Builtins")]
        protected void OnEditHistCommand(string file)
        {
            try
            {
                if (file.Equals("history"))
                {

                    file = _cli.Settings.App.History;
                }

                if (file.Equals("config"))
                {
                    file = _cli.Settings.App.Config;

                    if (string.IsNullOrEmpty(file))
                    {
                        Console.WriteLine("Enter path to config file:");
                        file = Console.ReadLine();
                    }
                }

                String editor = Environment.GetEnvironmentVariable("EDITOR");
                var exec = SpookUtils.LocateExec(editor);
                Console.WriteLine("Editor: " + exec);

                using(System.Diagnostics.Process pProcess = new System.Diagnostics.Process())
                {
                    pProcess.StartInfo.FileName = exec;
                    pProcess.StartInfo.Arguments = file; //argument
                    pProcess.StartInfo.UseShellExecute = true;
                    pProcess.StartInfo.RedirectStandardOutput = false;
                    pProcess.Start();
                    pProcess.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
