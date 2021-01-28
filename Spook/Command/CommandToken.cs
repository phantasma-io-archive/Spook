using System;
using System.Collections.Generic;
using System.Text;

namespace Phantasma.Spook.Command
{   
    public enum CommandTokenType : byte
    {
        String,
        Space,
        Quote,
    }

    public abstract class CommandToken
    {
        public int Offset { get; }

        public CommandTokenType Type { get; }

        public string Value { get; protected set; }

        public CommandToken(CommandTokenType type, int offset)
        {
            Type = type;
            Offset = offset;
        }

        public static IEnumerable<CommandToken> Parse(string commandLine)
        {
            CommandToken lastToken = null;

            for (int index = 0, count = commandLine.Length; index < count;)
            {
                switch (commandLine[index])
                {
                    case ' ':
                        {
                            lastToken = CommandSpaceToken.Parse(commandLine, ref index);
                            yield return lastToken;
                            break;
                        }
                    case '"':
                    case '\'':
                        {
                            // "'"
                            if (lastToken is CommandQuoteToken quote && quote.Value[0] != commandLine[index])
                            {
                                goto default;
                            }

                            lastToken = CommandQuoteToken.Parse(commandLine, ref index);
                            yield return lastToken;
                            break;
                        }
                    default:
                        {
                            lastToken = CommandStringToken.Parse(commandLine, ref index,
                                lastToken is CommandQuoteToken quote ? quote : null);

                            yield return lastToken;
                            break;
                        }
                }
            }
        }

        public static string[] ToArguments(IEnumerable<CommandToken> tokens, bool removeEscape = true)
        {
            var list = new List<string>();

            CommandToken lastToken = null;

            foreach (var token in tokens)
            {
                if (token is CommandStringToken str)
                {
                    if (removeEscape && lastToken is CommandQuoteToken quote)
                    {
                        // Remove escape

                        list.Add(str.Value.Replace("\\" + quote.Value, quote.Value));
                    }
                    else
                    {
                        list.Add(str.Value);
                    }
                }

                lastToken = token;
            }

            return list.ToArray();
        }

        public static string ToString(IEnumerable<CommandToken> tokens)
        {
            var sb = new StringBuilder();

            foreach (var token in tokens)
            {
                sb.Append(token.Value);
            }

            return sb.ToString();
        }

        public static void Trim(List<CommandToken> args)
        {
            // Trim start

            while (args.Count > 0 && args[0].Type == CommandTokenType.Space)
            {
                args.RemoveAt(0);
            }

            // Trim end

            while (args.Count > 0 && args[args.Count - 1].Type == CommandTokenType.Space)
            {
                args.RemoveAt(args.Count - 1);
            }
        }

        public static string ReadString(List<CommandToken> args, bool consumeAll)
        {
            Trim(args);

            var quoted = false;

            if (args.Count > 0 && args[0].Type == CommandTokenType.Quote)
            {
                quoted = true;
                args.RemoveAt(0);
            }
            else
            {
                if (consumeAll)
                {
                    // Return all if it's not quoted

                    var ret = ToString(args);
                    args.Clear();

                    return ret;
                }
            }

            if (args.Count > 0)
            {
                switch (args[0])
                {
                    case CommandQuoteToken _:
                        {
                            if (quoted)
                            {
                                args.RemoveAt(0);
                                return "";
                            }

                            throw new ArgumentException();
                        }
                    case CommandSpaceToken _: throw new ArgumentException();
                    case CommandStringToken str:
                        {
                            args.RemoveAt(0);

                            if (quoted && args.Count > 0 && args[0].Type == CommandTokenType.Quote)
                            {
                                // Remove last quote

                                args.RemoveAt(0);
                            }

                            return str.Value;
                        }
                }
            }

            return null;
        }
    }

    internal class CommandStringToken : CommandToken
    {
        public bool RequireQuotes { get; }

        public CommandStringToken(int offset, string value) : base(CommandTokenType.String, offset)
        {
            Value = value;
            RequireQuotes = value.IndexOfAny(new char[] { '\'', '"' }) != -1;
        }

        internal static CommandStringToken Parse(string commandLine, ref int index, CommandQuoteToken quote)
        {
            int end;
            int offset = index;

            if (quote != null)
            {
                var ix = index;

                do
                {
                    end = commandLine.IndexOf(quote.Value[0], ix + 1);

                    if (end == -1)
                    {
                        throw new ArgumentException("String not closed");
                    }

                    if (IsScaped(commandLine, end - 1))
                    {
                        ix = end;
                        end = -1;
                    }
                }
                while (end < 0);
            }
            else
            {
                end = commandLine.IndexOf(' ', index + 1);
            }

            if (end == -1)
            {
                end = commandLine.Length;
            }

            var ret = new CommandStringToken(offset, commandLine.Substring(index, end - index));
            index += end - index;
            return ret;
        }

        private static bool IsScaped(string commandLine, int index)
        {
            // TODO: Scape the scape

            return (commandLine[index] == '\\');
        }
    }

    internal class CommandSpaceToken : CommandToken
    {
        public int Count { get; }

        public CommandSpaceToken(int offset, int count) : base(CommandTokenType.Space, offset)
        {
            Value = "".PadLeft(count, ' ');
            Count = count;
        }

        internal static CommandSpaceToken Parse(string commandLine, ref int index)
        {
            int offset = index;
            int count = 0;

            for (int ix = index, max = commandLine.Length; ix < max; ix++)
            {
                if (commandLine[ix] == ' ')
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (count == 0) throw new ArgumentException("No spaces found");

            index += count;
            return new CommandSpaceToken(offset, count);
        }
    }

    internal class CommandQuoteToken : CommandToken
    {
        public CommandQuoteToken(int offset, char value) : base(CommandTokenType.Quote, offset)
        {
            if (value != '\'' && value != '"')
            {
                throw new ArgumentException("Not valid quote");
            }

            Value = value.ToString();
        }

        internal static CommandQuoteToken Parse(string commandLine, ref int index)
        {
            var c = commandLine[index];

            if (c == '\'' || c == '"')
            {
                index++;
                return new CommandQuoteToken(index - 1, c);
            }

            throw new ArgumentException("No quote found");
        }
    }
}
