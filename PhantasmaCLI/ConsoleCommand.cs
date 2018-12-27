using System;

namespace Phantasma.CLI
{
    public class ConsoleCommand
    {
        public readonly string Name;
        public readonly string Description;
        public readonly Action<string[]> Action;

        public ConsoleCommand(string name, string description, Action<string[]> action)
        {
            Name = name;
            Description = description;
            Action = action;
        }
    }
}
