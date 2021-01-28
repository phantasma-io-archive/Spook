using System;
using System.Linq;

namespace Phantasma.Spook.Command
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string[] Verbs { get; }

        public string Category { get; set; }

        public string Description { get; set; }

        public ConsoleCommandAttribute(string verbs)
        {
            Verbs = verbs.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(u => u.ToLowerInvariant()).ToArray();
        }

        public ConsoleCommandAttribute(string verbs, string category, string description)
        {
            Verbs = verbs.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(u => u.ToLowerInvariant()).ToArray();
            Category = category;
            Description = description;
        }
    }
}
