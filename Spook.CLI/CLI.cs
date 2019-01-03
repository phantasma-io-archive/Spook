using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Tests;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Tokens;
using LunarLabs.Parser.JSON;
using Phantasma.API;
using Phantasma.Network.P2P;
using Phantasma.Spook.Modules;
using Phantasma.Spook.Plugins;
using Phantasma.Spook.GUI;
using LunarLabs.WebServer.Core;
using Logger = Phantasma.Core.Log.Logger;
using ConsoleLogger = Phantasma.Core.Log.ConsoleLogger;

namespace Phantasma.Spook
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ModuleAttribute: Attribute
    {
        public readonly string Name;

        public ModuleAttribute(string name)
        {
            Name = name;
        }
    }

    public interface IPlugin
    {
        string Channel { get; }
        void Update();
    }

    public class CLI
    {
        static void Main(string[] args)
        {
            new CLI(args);
        }

        // testnet validators, hardcoded for now
        private static readonly string[] validatorWIFs = new string[]
        {
            "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", //P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr
            "L1sEB8Z6h5Y7aQKqxbAkrzQLY5DodmPacjqSkFPmcR82qdmHEEdY", // PGBinkbZA3Q6BxMnL2HnJSBubNvur3iC6GtQpEThDnvrr
            "KxWUCAD2wECLfA7diT7sV7V3jcxAf9GSKqZy3cvAt79gQLHQ2Qo8", // PDiqQHDwe6MTcP6TH6DYjq7FTUouvy2YEkDXz2chCABCb
        };

        private readonly Node node;
        private readonly Logger logger;
        private readonly Mempool mempool;
        private bool running = false;

        private Nexus nexus;
        private NexusAPI api;

        private ConsoleGUI gui;

        private List<IPlugin> plugins = new List<IPlugin>();

        private static BigInteger FetchBalance(JSONRPC_Client rpc, Logger logger, string host, Address address)
        {
            var response = rpc.SendRequest(logger, RequestType.POST, host, "getAccount", address.ToString());
            if (response == null)
            {
                logger.Error($"Error fetching balance of {address}...");
                return 0;
            }

            var balances = response["balances"];
            if (balances == null)
            {
                logger.Error($"Error fetching balance of {address}...");
                return 0;
            }

            BigInteger total = 0;

            foreach (var entry in balances.Children)
            {
                var chain = entry.GetString("chain");
                var symbol = entry.GetString("symbol");

                if (symbol == "SOUL")
                {
                    total += TokenUtils.ToBigInteger(entry.GetDecimal("amount"), Nexus.NativeTokenDecimals);
                }
            }

            return total;
        }


        private static Hash SendTransfer(JSONRPC_Client rpc, Logger logger, string host, KeyPair from, Address to, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(from.Address, 1, 9999).TransferTokens("SOUL", from.Address, to, amount).SpendGas(from.Address).EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30), 0);
            tx.Sign(from);

            var bytes = tx.ToByteArray(true);

            //log.Debug("RAW: " + Base16.Encode(bytes));

            var response = rpc.SendRequest(logger, RequestType.POST, host, "sendRawTransaction", Base16.Encode(bytes));
            if (response == null)
            {
                if (logger != null)
                {
                    logger.Error($"Error sending {amount} SOUL from {from.Address} to {to}...");
                }
                return Hash.Null;
            }

            var hash = response.GetString("hash");
            if (string.IsNullOrEmpty(hash))
            {
                if (logger != null)
                {
                    logger.Error("Hash not found");
                }
                return Hash.Null;
            }

            return Hash.Parse(hash);
        }

        private static bool ConfirmTransaction(JSONRPC_Client rpc, Logger logger, string host, Hash hash, int maxTries = 99999)
        {
            var hashStr = hash.ToString();

            int tryCount = 0;

            do
            {
                var response = rpc.SendRequest(logger, RequestType.POST, host, "getConfirmations", hashStr);
                if (response == null)
                {
                    if (logger != null)
                    {
                        logger.Error("Transfer request failed");
                    }
                    return false;
                }

                var confirmations = response.GetInt32("confirmations");
                if (confirmations > 0)
                {
                    if (logger != null)
                    {
                        logger.Success("Confirmations: " + confirmations);
                    }
                    return true;
                }

                tryCount--;
                if (tryCount >= maxTries)
                {
                    return false;
                }

                Thread.Sleep(1000);
            } while (true);
        }

        private void SenderSpawn(KeyPair masterKeys, string host, BigInteger initialAmount, int addressesListSize)
        {
            Thread.CurrentThread.IsBackground = true;

            var addressList = new Queue<KeyPair>();

            for (int j = 0; j < addressesListSize; j++)
            {
                var key = KeyPair.Generate();
                addressList.Enqueue(key);
            }

            var currentKey = addressList.Dequeue();

            logger.Message($"Connecting to host: {host} with address {masterKeys.Address.Text}");

            var rpc = new JSONRPC_Client();

            var hash = SendTransfer(rpc, logger, host, masterKeys, currentKey.Address, initialAmount);
            if (hash == Hash.Null)
            {
                return;
            }

            ConfirmTransaction(rpc, logger, host, hash);

            int totalTxs = 0;

            addressList.Enqueue(currentKey);

            BigInteger fee = 9999; // TODO calculate the real fee

            while (true)
            {
                var destKey = addressList.Dequeue();

                BigInteger amount = initialAmount - fee;

                var txHash = SendTransfer(rpc, null, host, currentKey, destKey.Address, amount);
                if (txHash == Hash.Null)
                {
                    logger.Error($"Error sending {amount} SOUL from {currentKey.Address} to {destKey.Address}...");
                    return;
                }

                totalTxs++;

                addressList.Enqueue(currentKey);
                currentKey = destKey;

                Thread.Sleep(100);

                var confirmation = ConfirmTransaction(rpc, null, host, hash);

                if (totalTxs % 10 == 0)
                {
                    logger.Message($"Sent {totalTxs} transactions");
                }
            }

            logger.Message($"*** Thread ran out of funds");
        }

        private void RunSender(string wif, string host, int threadCount, int addressesListSize)
        {
            logger.Message("Running in sender mode.");

            running = true;
            Console.CancelKeyPress += delegate {
                running = false;
                logger.Message("Stopping sender...");
            };

            var masterKeys = KeyPair.FromWIF(wif);

            var rpc = new JSONRPC_Client();
            logger.Message($"Fetch initial balance...");
            BigInteger initialAmount = FetchBalance(rpc, logger, host, masterKeys.Address);
            if (initialAmount <= 0)
            {
                logger.Message($"Could not obtain funds");
                return;
            }

            logger.Message($"Initial balance: {TokenUtils.ToDecimal(initialAmount, Nexus.NativeTokenDecimals)} SOUL");

            initialAmount /= 4; // 25%
            initialAmount /= threadCount;

            for (int i=1; i<= threadCount; i++)
            {
                logger.Message($"Starting thread #{i}...");
                try
                {
                    new Thread(() => { SenderSpawn(masterKeys, host, initialAmount, addressesListSize); }).Start();
                }
                catch (Exception e) {
                    logger.Error(e.ToString());
                    break;
                }
            }

            this.Run();
        }

        private void WebLogMapper(string channel, LogLevel level, string text)
        {
            if (gui != null)
            {
                switch (level)
                {
                    case LogLevel.Debug: gui.WriteToChannel(channel, Core.Log.LogEntryKind.Debug, text); break;
                    case LogLevel.Error: gui.WriteToChannel(channel, Core.Log.LogEntryKind.Error, text); break;
                    case LogLevel.Warning: gui.WriteToChannel(channel, Core.Log.LogEntryKind.Warning, text); break;
                    default: gui.WriteToChannel(channel, Core.Log.LogEntryKind.Message, text); break;
                }

                return;
            }

            switch (level)
            {
                case LogLevel.Debug: logger.Debug(text); break;
                case LogLevel.Error: logger.Error(text); break;
                case LogLevel.Warning: logger.Warning(text); break;
                default: logger.Message(text); break;
            }
        }

        public CLI(string[] args) { 
            var seeds = new List<string>();

            var settings = new Arguments(args);

            var useGUI = settings.GetBool("gui.enabled", true);

            if (useGUI)
            {
                gui = new ConsoleGUI();
                logger = gui;
            }
            else
            {
                gui = null;
                logger = new ConsoleLogger();
            }

            string mode = settings.GetString("node.mode", "validator");

            bool hasRPC = settings.GetBool("rpc.enabled", false);

            string wif = settings.GetString("node.wif");

            var nexusName = settings.GetString("nexus.name", "simnet");
            var genesisAddress = Address.FromText(settings.GetString("nexus.genesis", KeyPair.FromWIF(validatorWIFs[0]).Address.Text));

            switch (mode)
            {
                case "sender":
                    string host = settings.GetString("sender.host");
                    int threadCount = settings.GetInt("sender.threads", 8);
                    int addressesPerSender = settings.GetInt("sender.addressCount", 20);
                    RunSender(wif, host, threadCount, addressesPerSender);
                    Console.WriteLine("Sender finished operations.");
                    return;

                case "validator": break;
                default:
                    {
                        logger.Error("Unknown mode: " + mode);
                        return;
                    }
            }

            string defaultPort = null;
            for (int i=0; i<validatorWIFs.Length; i++)
            {
                if (validatorWIFs[i] == wif)
                {
                    defaultPort = (7073 + i).ToString();
                }
            }

            if (defaultPort == null)
            {
                defaultPort = (7073 + validatorWIFs.Length).ToString();
            }

            int port = int.Parse(settings.GetString("node.port", defaultPort));

            var node_keys = KeyPair.FromWIF(wif);

            if (wif == validatorWIFs[0])
            {
                var simulator = new ChainSimulator(node_keys, 1234, logger);
                nexus = simulator.Nexus;

                for (int i=0; i< 100; i++)
                {
                    simulator.GenerateRandomBlock();
                }
            }
            else
            {
                nexus = new Nexus(nexusName, genesisAddress, logger);
                seeds.Add("127.0.0.1:7073");
            }

            running = true;

            // mempool setup
            this.mempool = new Mempool(node_keys, nexus);
            mempool.Start();

            api = new NexusAPI(nexus, mempool);

            // RPC setup
            if (hasRPC)
            {
                int rpcPort = settings.GetInt("rpc.port", 7077);

                logger.Message($"RPC server listening on port {rpcPort}...");
                var rpcServer = new RPCServer(api, "rpc", rpcPort, (level, text) => WebLogMapper("rpc", level, text));
                new Thread(() => { rpcServer.Start(); }).Start();
            }

            // node setup
            this.node = new Node(nexus, node_keys, port, seeds, logger);           
            node.Start();

            int pluginPeriod = settings.GetInt("plugin.refresh", 10); // in seconds
            RegisterPlugin(new TPSPlugin(logger, pluginPeriod));
            RegisterPlugin(new RAMPlugin(logger, pluginPeriod));
            RegisterPlugin(new MempoolPlugin(mempool, logger, pluginPeriod));

            Console.CancelKeyPress += delegate {
                Terminate();
            };

            logger.Success("Node is ready");

            var dispatcher = new CommandDispatcher();
            SetupCommands(dispatcher);

            if (gui != null)
            {
                gui.MakeReady(dispatcher);
            }

            this.Run();
        }

        private void Run()
        {
            if (gui != null)
            {
                while (running)
                {
                    gui.Update();
                    this.plugins.ForEach(x => x.Update());
                }
            }
            else
            {
                while (running)
                {
                    Thread.Sleep(1000);
                    this.plugins.ForEach(x => x.Update());
                }
            }
        }

        private void Terminate()
        {
            running = false;
            logger.Message("Phantasma Node stopping...");

            if (node.IsRunning)
            {
                node.Stop();
            }

            if (mempool.IsRunning)
            {
                mempool.Stop();
            }
        }

        private void RegisterPlugin(IPlugin plugin)
        {
            var name = plugin.GetType().Name.Replace("Plugin", "");
            logger.Message("Plugin enabled: " + name);
            plugins.Add(plugin);

            if (nexus != null)
            {
                var nexusPlugin = plugin as IChainPlugin;
                if (nexusPlugin != null)
                {
                    nexus.AddPlugin(nexusPlugin);
                }
            }
        }

        private void ExecuteAPI(string name, string[] args)
        {
            var result = api.Execute(name, args);
            if (result == null)
            {
                logger.Warning("API returned null value...");
                return;
            }

            var node = APIUtils.FromAPIResult(result);
            var json = JSONWriter.WriteToString(node);
            var lines = json.Split('\n');
            foreach (var line in lines)
            {
                logger.Message(line);
            }
        }

        private void SetupCommands(CommandDispatcher dispatcher)
        {
            dispatcher.RegisterCommand("quit", "Stops the node and exits", (args) => Terminate());

            if (gui != null)
            {
                dispatcher.RegisterCommand("gui.log", "Switches the gui to log view", (args) => gui.ShowLog(args));
                dispatcher.RegisterCommand("gui.graph", "Switches the gui to graph view", (args) => gui.ShowGraph(args));
            }

            dispatcher.RegisterCommand("help", "Lists available commands", (args) => dispatcher.Commands.ToList().ForEach(x => logger.Message($"{x.Name}\t{x.Description}")));

            foreach (var method in api.Methods)
            {
                dispatcher.RegisterCommand("api."+method.Name, "API CALL", (args) => ExecuteAPI(method.Name, args));
            }

            dispatcher.RegisterCommand("script.assemble", "Assembles a .asm file into Phantasma VM script format",
                (args) => ScriptModule.AssembleFile(args));

            dispatcher.RegisterCommand("script.disassemble", $"Disassembles a {AssemblerLib.Format.Extension} file into readable Phantasma assembly",
                (args) => ScriptModule.DisassembleFile(args));

            dispatcher.RegisterCommand("script.compile", "Compiles a .sol file into Phantasma VM script format",
                (args) => ScriptModule.CompileFile(args));
        }
    }
}
