using System.Collections.Generic;
using System.Reflection;

namespace Phantasma.Spook.Command
{
    public class ConsoleCommandMethod
    {
        public string[] Verbs { get; }

        public string Key => string.Join(' ', Verbs);

        public string HelpCategory { get; set; }

        public string HelpMessage { get; set; }

        public object Instance { get; }

        public MethodInfo Method { get; }

        public ConsoleCommandMethod(object instance, MethodInfo method, ConsoleCommandAttribute attribute)
        {
            Method = method;
            Instance = instance;
            Verbs = attribute.Verbs;
            HelpCategory = attribute.Category;
            HelpMessage = attribute.Description;
        }

        public bool IsThisCommand(CommandToken[] tokens, out int consumedArgs)
        {
            int checks = Verbs.Length;
            bool quoted = false;
            var tokenList = new List<CommandToken>(tokens);

            while (checks > 0 && tokenList.Count > 0)
            {
                switch (tokenList[0])
                {
                    case CommandSpaceToken _:
                        {
                            tokenList.RemoveAt(0);
                            break;
                        }
                    case CommandQuoteToken _:
                        {
                            quoted = !quoted;
                            tokenList.RemoveAt(0);
                            break;
                        }
                    case CommandStringToken str:
                        {
                            if (Verbs[^checks] != str.Value.ToLowerInvariant())
                            {
                                consumedArgs = 0;
                                return false;
                            }

                            checks--;
                            tokenList.RemoveAt(0);
                            break;
                        }
                }
            }

            if (quoted && tokenList.Count > 0 && tokenList[0].Type == CommandTokenType.Quote)
            {
                tokenList.RemoveAt(0);
            }

            while (tokenList.Count > 0 && tokenList[0].Type == CommandTokenType.Space) tokenList.RemoveAt(0);

            consumedArgs = tokens.Length - tokenList.Count;
            return checks == 0;
        }
    }
}
