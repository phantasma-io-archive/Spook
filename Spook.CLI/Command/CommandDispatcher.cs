using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Phantasma.Spook.Modules;

namespace Phantasma.Spook.Command
{
    public class CommandException: Exception
    {
        public CommandException(string msg): base(msg)
        {
        }
    }

    public partial class CommandDispatcher
    {
        private readonly Dictionary<string, List<ConsoleCommandMethod>> _verbs = new Dictionary<string, List<ConsoleCommandMethod>>();

        public List<string> Verbs { get { return _verbs.Keys.ToList(); } }

        private readonly Dictionary<string, object> _instances = new Dictionary<string, object>();

        private readonly Dictionary<Type, Func<List<CommandToken>, bool, object>> _handlers
            = new Dictionary<Type, Func<List<CommandToken>, bool, object>>();

        private CLI _cli;

        public CommandDispatcher(CLI cli)
        {

            _cli = cli;
            RegisterCommandHandler<string>((args, canConsumeAll) =>
                    {
                    return CommandToken.ReadString(args, canConsumeAll);
                    });

            RegisterCommandHandler<string[]>((args, canConsumeAll) =>
                    {
                    if (canConsumeAll)
                    {
                    var ret = CommandToken.ToString(args);
                    args.Clear();
                    return ret.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    else
                    {
                    return CommandToken.ReadString(args, false).Split(',', ' ');
                    }
                    });

            RegisterCommandHandler<string, byte>(false, (str) => byte.Parse(str));
            RegisterCommandHandler<string, bool>(false, (str) => str == "1" || str == "yes" || str == "y" || bool.Parse(str));
            RegisterCommandHandler<string, ushort>(false, (str) => ushort.Parse(str));
            RegisterCommandHandler<string, uint>(false, (str) => uint.Parse(str));

            RegisterCommandHandler<string, JObject>((str) => JObject.Parse(str));
            //RegisterCommandHandler<JObject, JArray>((obj) => (JArray)obj);

            RegisterCommand(this);
        }

        public void RegisterCommand(object instance, string name = null)
        {
            if (!string.IsNullOrEmpty(name))
            {
                _instances.Add(name.ToLowerInvariant(), instance);
            }

            var methodInfo = _cli.NexusAPI.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methodInfo)
            {
                var attribute = new ConsoleCommandAttribute(method.Name.ToLower(), "NexusAPI", "api command");
                var command = new ConsoleCommandMethod(instance, method, attribute);
                if (!_verbs.TryGetValue(command.Key, out var commands))
                {
                    _verbs.Add(command.Key, new List<ConsoleCommandMethod>(new[] { command }));
                }
                else
                {
                    commands.Add(command);
                }
            }

            methodInfo = typeof(WalletModule).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methodInfo)
            {

                foreach (var attribute in method.GetCustomAttributes<ConsoleCommandAttribute>())
                {
                    var command = new ConsoleCommandMethod(instance, method, attribute);
                    if (!_verbs.TryGetValue(command.Key, out var commands))
                    {
                        _verbs.Add(command.Key, new List<ConsoleCommandMethod>(new[] { command }));
                    }
                    else
                    {
                        commands.Add(command);
                    }
                }
            }

            methodInfo = typeof(ScriptModule).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methodInfo)
            {
                var attribute = new ConsoleCommandAttribute(method.Name.ToLower(), "Script", "Script commands");

                var command = new ConsoleCommandMethod(instance, method, attribute);
                if (!_verbs.TryGetValue(command.Key, out var commands))
                {
                    _verbs.Add(command.Key, new List<ConsoleCommandMethod>(new[] { command }));
                }
                else
                {
                    commands.Add(command);
                }
            }

            methodInfo = typeof(NexusModule).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methodInfo)
            {
                var attribute = new ConsoleCommandAttribute(method.Name.ToLower(), "Nexus", "Nexus commands");

                var command = new ConsoleCommandMethod(instance, method, attribute);
                if (!_verbs.TryGetValue(command.Key, out var commands))
                {
                    _verbs.Add(command.Key, new List<ConsoleCommandMethod>(new[] { command }));
                }
                else
                {
                    commands.Add(command);
                }
            }

            foreach (var method in instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var attribute in method.GetCustomAttributes<ConsoleCommandAttribute>())
                {
                    // Check handlers

                    if (!method.GetParameters().All(u => u.ParameterType.IsEnum || _handlers.ContainsKey(u.ParameterType)))
                    {
                        throw new ArgumentException("Handler not found for the command: " + method.ToString());
                    }

                    // Add command

                    var command = new ConsoleCommandMethod(instance, method, attribute);

                    if (!_verbs.TryGetValue(command.Key, out var commands))
                    {
                        _verbs.Add(command.Key, new List<ConsoleCommandMethod>(new[] { command }));
                    }
                    else
                    {
                        commands.Add(command);
                    }
                }
            }
        }

        public bool OnCommand(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine))
            {
                return true;
            }

            string possibleHelp = null;
            var commandArgs = CommandToken.Parse(commandLine).ToArray();
            var availableCommands = new List<(ConsoleCommandMethod Command, object[] Arguments)>();

            foreach (var entries in _verbs.Values)
            {
                foreach (var command in entries)
                {
                    if (command.IsThisCommand(commandArgs, out var consumedArgs))
                    {
                        var arguments = new List<object>();
                        var args = commandArgs.Skip(consumedArgs).ToList();

                        CommandSpaceToken.Trim(args);

                        try
                        {
                            var parameters = command.Method.GetParameters();

                            foreach (var arg in parameters)
                            {
                                // Parse argument

                                if (TryProcessValue(arg.ParameterType, args, arg == parameters.Last(), out var value))
                                {
                                    arguments.Add(value);
                                }
                                else
                                {
                                    if (arg.HasDefaultValue)
                                    {
                                        arguments.Add(arg.DefaultValue);
                                    }
                                    else
                                    {
                                        throw new ArgumentException(arg.Name);
                                    }
                                }
                            }

                            availableCommands.Add((command, arguments.ToArray()));
                        }
                        catch
                        {
                            // Skip parse errors
                            possibleHelp = command.Key;
                        }
                    }
                }
            }

            switch (availableCommands.Count)
            {
                case 0:
                    {
                        if (!string.IsNullOrEmpty(possibleHelp))
                        {
                            OnHelpCommand(possibleHelp);
                            return true;
                        }

                        return false;
                    }
                case 1:
                    {
                        var (command, arguments) = availableCommands[0];
                        if (command.HelpCategory.Equals("NexusAPI"))
                        {
                            try
                            {

                                string[] args = arguments.Where(x => x != null)
                                                    .Select(x => x.ToString())
                                                    .ToArray();
                                _cli.ExecuteAPI(command.Method.Name, args);
                                return true;
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("invalid command");
                            }
                            return true;
                        }
                        command.Method.Invoke(command.Instance, arguments);
                        return true;
                    }
                default:
                    {
                        // Show Ambiguous call

                        throw new ArgumentException("Ambiguous calls for: " + string.Join(',', availableCommands.Select(u => u.Command.Key).Distinct()));
                    }
            }
        }

        public void RegisterCommandHandler<T, TRet>(Func<T, object> handler)
        {
            _handlers[typeof(TRet)] = (args, cosumeAll) =>
            {
                var value = (T)_handlers[typeof(T)](args, cosumeAll);
                return handler(value);
            };
        }

        public void RegisterCommandHandler<T, TRet>(bool canConsumeAll, Func<T, object> handler)
        {
            _handlers[typeof(TRet)] = (args, cosumeAll) =>
            {
                var value = (T)_handlers[typeof(T)](args, canConsumeAll);
                return handler(value);
            };
        }

        private void RegisterCommandHandler<TRet>(Func<List<CommandToken>, bool, object> handler)
        {
            _handlers[typeof(TRet)] = handler;
        }

        private void RegisterCommandHandler<TRet>(Func<List<CommandToken>, bool, object> handler)
        {
            _handlers[typeof(TRet)] = handler;
        }

        public void RegisterCommandHandler<T, TRet>(bool canConsumeAll, Func<T, object> handler)
        {
            _handlers[typeof(TRet)] = (args, cosumeAll) =>
            {
                var value = (T)_handlers[typeof(T)](args, canConsumeAll);
                return handler(value);
            };
        }

        public void RegisterCommandHandler<T, TRet>(Func<T, object> handler)
        {
            _handlers[typeof(TRet)] = (args, cosumeAll) =>
            {
                var value = (T)_handlers[typeof(T)](args, cosumeAll);
                return handler(value);
            };
        }

        private bool TryProcessValue(Type parameterType, List<CommandToken> args, bool canConsumeAll, out object value)
        {
            if (args.Count > 0)
            {
                if (_handlers.TryGetValue(parameterType, out var handler))
                {
                    value = handler(args, canConsumeAll);
                    return true;
                }

                if (parameterType.IsEnum)
                {
                    var arg = CommandToken.ReadString(args, canConsumeAll);
                    value = Enum.Parse(parameterType, arg.Trim(), true);
                    return true;
                }
            }

            value = null;
            return false;
        }

        [ConsoleCommand("help", Category = "Base Commands")]
        protected void OnHelpCommand(string key)
        {
            var withHelp = new List<ConsoleCommandMethod>();

            // Try to find a plugin with this name

            if (_instances.TryGetValue(key.Trim().ToLowerInvariant(), out var instance))
            {
                // Filter only the help of this plugin

                key = "";
                foreach (var commands in _verbs.Values.Select(u => u))
                {
                    withHelp.AddRange
                        (
                         commands.Where(u => !string.IsNullOrEmpty(u.HelpCategory) && u.Instance == instance)
                        );
                }
            }
            else
            {
                // Fetch commands

                foreach (var commands in _verbs.Values.Select(u => u))
                {
                    withHelp.AddRange(commands.Where(u => !string.IsNullOrEmpty(u.HelpCategory)));
                }
            }

            // Sort and show

            withHelp.Sort((a, b) =>
                    {
                    var cate = a.HelpCategory.CompareTo(b.HelpCategory);
                    if (cate == 0)
                    {
                    cate = a.Key.CompareTo(b.Key);
                    }
                    return cate;
                    });

            if (string.IsNullOrEmpty(key) || key.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                string last = null;
                foreach (var command in withHelp)
                {
                    if (last != command.HelpCategory)
                    {
                        Console.WriteLine($"{command.HelpCategory}:");
                        last = command.HelpCategory;
                    }

                    Console.Write($"\t{command.Key}");
                    Console.WriteLine(" " + string.Join(' ',
                                command.Method.GetParameters()
                                .Select(u => u.HasDefaultValue ? $"[{u.Name}={(u.DefaultValue == null ? "null" : u.DefaultValue.ToString())}]" : $"<{u.Name}>"))
                            );
                }
            }
            else
            {
                // Show help for this specific command

                string last = null;
                string lastKey = null;
                bool found = false;

                foreach (var command in withHelp.Where(u => u.Key == key))
                {
                    found = true;

                    if (last != command.HelpMessage)
                    {
                        Console.WriteLine($"{command.HelpMessage}");
                        last = command.HelpMessage;
                    }

                    if (lastKey != command.Key)
                    {
                        Console.WriteLine($"You can call this command like this:");
                        lastKey = command.Key;
                    }

                    Console.Write($"\t{command.Key}");
                    Console.WriteLine(" " + string.Join(' ',
                                command.Method.GetParameters()
                                .Select(u => u.HasDefaultValue ? $"[{u.Name}={u.DefaultValue?.ToString() ?? "null"}]" : $"<{u.Name}>"))
                            );
                }

                if (!found)
                {
                    throw new ArgumentException($"Command not found.");
                }
            }
        }
    }
}
