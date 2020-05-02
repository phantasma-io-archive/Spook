using System;
using System.Linq;
using Phantasma.Cryptography;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Spook.Command;

namespace Phantasma.Spook.Shell
{
    class SpookShell
    {
        private PhantasmaKeys keyPair {get; set;} = null;

        private String prompt { get; set; } = "spook> ";

        private CommandDispatcher _dispatcher;
        private CLI _cli;

        public SpookShell(string[] args, SpookSettings conf, CLI cli)
        {
            _cli = cli;
            _cli.Start();
            _dispatcher = new CommandDispatcher(_cli);

            List<string> completionList = new List<string>(); 

            string version = Assembly.GetAssembly(typeof(CLI)).GetVersion();
            if (!string.IsNullOrEmpty(_cli.Settings.App.Prompt))
            {
                prompt = _cli.Settings.App.Prompt;
            }

            var startupMsg =  "Spook shell" + version;

            // TODO autocompletion needs to be reworked, not everything can be completed with anything, 
            // completion source could be built like a B-tree index to search for possible candidates.
            Prompt.Run(
                ((command, listCmd, list) =>
                {
                    string command_main = command.Trim().Split(new char[] { ' ' }).First();

                    if (!_dispatcher.OnCommand(command))
                    {
                        Console.WriteLine("error: Command not found");
                    }

                    return "";
                }), prompt, PromptGenerator,  startupMsg, _cli.Settings.App.History, _dispatcher.Verbs);
        }

        private string PromptGenerator()
        {
            var height = _cli.ExecuteAPIR("getblockheight", new string[] {"main"});
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
