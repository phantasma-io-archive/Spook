using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.Spook
{
    public class CommandException: Exception
    {
        public CommandException(string msg): base(msg)
        {
        }
    }

    public class CommandDispatcher
    {
        private Dictionary<string, ConsoleCommand> _commands = new Dictionary<string, ConsoleCommand>();

        public void RegisterCommand(string name, string description, Action<string[]> action)
        {
            _commands[name.ToLower()] = new ConsoleCommand(name, description, action);
        }

        public void ExecuteCommand(string input)
        {
            var args = input.Split(' ');
            var name = args[0].ToLower();

            if (_commands.ContainsKey(name))
            {
                args = args.Skip(1).ToArray();
                _commands[name].Action(args);
            }
            else
            {
                throw new CommandException("Unknown command: "+name);
            }
        }
    }
}
