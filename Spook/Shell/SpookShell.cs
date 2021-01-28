using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Spook.Command;
using Phantasma.Spook.Utils;

namespace Phantasma.Spook.Shell
{
    class SpookShell
    {
        private PhantasmaKeys keyPair {get; set;} = null;

        private String prompt { get; set; } = "spook> ";

        private CommandDispatcher _dispatcher;
        private Spook _node;

        public SpookShell(string[] args, SpookSettings conf, Spook node)
        {
            _node = node;
            _node.Start();
            _dispatcher = new CommandDispatcher(_node);

            List<string> completionList = new List<string>(); 

            string version = Assembly.GetAssembly(typeof(Spook)).GetVersion();
            if (!string.IsNullOrEmpty(_node.Settings.App.Prompt))
            {
                prompt = _node.Settings.App.Prompt;
            }

            var startupMsg =  "Spook shell " + version + "\nLogs are stored in " 
                + _node.LogPath + "\nTo exit use <ctrl-c> or \"exit\"!\n";

            Prompt.Run(
                ((command, listCmd, list) =>
                {
                    string command_main = command.Trim().Split(new char[] { ' ' }).First();

                    if (!_dispatcher.OnCommand(command))
                    {
                        Console.WriteLine("error: Command not found");
                    }

                    return "";
                }), prompt, PromptGenerator,  startupMsg, Path.GetTempPath() + conf.App.History, _dispatcher.Verbs);
        }

        private string PromptGenerator()
        {
            var height = _node.ExecuteAPIR("getBlockHeight", new string[] {"main"});
            return string.Format(prompt, height.Trim( new char[] {'"'} ));
        }

        private void Wallet(string[] obj)
        {
            GetLoginKey();
        }

        private PhantasmaKeys GetLoginKey(bool changeWallet=false)
        {
            if (keyPair == null && !changeWallet) 
            {
                var wif = Console.ReadLine();
                var kPair = PhantasmaKeys.FromWIF(wif);
                keyPair = kPair;
            }
            return keyPair;
        }

        private void Output(string message = "", int maxWidth = 80)
        {
            if (message.Length > 0) Console.Write(message);
            var spacesToErase = maxWidth - message.Length;
            if (spacesToErase < 0) spacesToErase = 0;
            Console.WriteLine(new string(' ', spacesToErase));
        }


    }
}
