using System;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.IO;

using LunarLabs.Parser.JSON;
using LunarLabs.WebServer.Core;

using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Spook.GUI;
using Phantasma.API;
using Phantasma.Network.P2P;
using Phantasma.Spook.Modules;
using Phantasma.Spook.Plugins;
using Phantasma.Blockchain.Contracts;
using Phantasma.Simulator;
using Phantasma.Blockchain.Plugins;
using Phantasma.CodeGen.Assembler;
using Phantasma.VM.Utils;
using Phantasma.Core;
using Phantasma.Network.P2P.Messages;
using Phantasma.RocksDB;
using Phantasma.Spook.Nachomen;
using Phantasma.Storage;
using Logger = Phantasma.Core.Log.Logger;
using ConsoleLogger = Phantasma.Core.Log.ConsoleLogger;
using System.Globalization;
using Phantasma.Spook.Oracles;
using Phantasma.Spook.Swaps;
using Phantasma.Domain;
using Phantasma.Blockchain.Swaps;

namespace Phantasma.Spook
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ModuleAttribute : Attribute
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
            "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", // PKtRvkhUhAiHsg4YnaxSM9dyyLBogTpHwtUEYvMYDKuV8
            "L1sEB8Z6h5Y7aQKqxbAkrzQLY5DodmPacjqSkFPmcR82qdmHEEdY", // PZR3AFPJkqSpxXSZkFTxbdQi7dRZAnvGBkwN96P7zJt78
            "KxWUCAD2wECLfA7diT7sV7V3jcxAf9GSKqZy3cvAt79gQLHQ2Qo8", // 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
    };

        private readonly Node node;
        private readonly Logger logger;
        private readonly Mempool mempool;
        private readonly TokenSwapService tokenSwapper;
        private bool running = false;
        private bool nodeReady = false;
        
        public NeoScanAPI NeoScanAPI { get; private set; }
        public Neo.Core.NeoAPI NeoAPI { get; private set; }

        public int rpcPort { get; private set; }
        public int restPort { get; private set; }
        
        private Nexus nexus;
        private NexusAPI api;

        private bool autoRestart;

        private ConsoleGUI gui;

        private NexusSimulator simulator;
        private bool useSimulator;

        private List<IPlugin> plugins = new List<IPlugin>();

        public string cryptoCompareAPIKey { get; private set; } = null;

        private bool showWebLogs;

        private static BigInteger FetchBalance(JSONRPC_Client rpc, Logger logger, string host, Address address)
        {
            var response = rpc.SendRequest(logger, host, "getAccount", address.ToString());
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

                if (symbol == DomainSettings.FuelTokenSymbol)
                {
                    total += BigInteger.Parse(entry.GetString("amount"));
                }
            }

            return total;
        }

        private static Hash SendTransfer(JSONRPC_Client rpc, Logger logger, string host, KeyPair from, Address to, BigInteger amount)
        {
            Throw.IfNull(rpc, nameof(rpc));
            Throw.IfNull(logger, nameof(logger));

            var script = ScriptUtils.BeginScript().AllowGas(from.Address, Address.Null, 1, 9999).TransferTokens("SOUL", from.Address, to, amount).SpendGas(from.Address).EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30));
            tx.Sign(from);

            var bytes = tx.ToByteArray(true);

            //log.Debug("RAW: " + Base16.Encode(bytes));

            var response = rpc.SendRequest(logger, host, "sendRawTransaction", Base16.Encode(bytes));
            if (response == null)
            {
                logger.Error($"Error sending {amount} {DomainSettings.FuelTokenSymbol} from {from.Address} to {to}...");
                return Hash.Null;
            }

            if (response.HasNode("error"))
            {
                var error = response.GetString("error");
                logger.Error("Error: " + error);
                return Hash.Null;
            }

            var hash = response.Value;
            return Hash.Parse(hash);
        }

        private static bool ConfirmTransaction(JSONRPC_Client rpc, Logger logger, string host, Hash hash, int maxTries = 99999)
        {
            var hashStr = hash.ToString();

            int tryCount = 0;

            int delay = 250;
            do
            {
                var response = rpc.SendRequest(logger, host, "getConfirmations", hashStr);
                if (response == null)
                {
                    logger.Error("Transfer request failed");
                    return false;
                }

                var confirmations = response.GetInt32("confirmations");
                if (confirmations > 0)
                {
                    logger.Success("Confirmations: " + confirmations);
                    return true;
                }

                tryCount--;
                if (tryCount >= maxTries)
                {
                    return false;
                }

                Thread.Sleep(delay);
                delay *= 2;
            } while (true);
        }

        private void SenderSpawn(int ID, KeyPair masterKeys, string host, BigInteger initialAmount, int addressesListSize)
        {
            Throw.IfNull(logger, nameof(logger));

            Thread.CurrentThread.IsBackground = true;

            BigInteger fee = 9999; // TODO calculate the real fee

            BigInteger amount = initialAmount;

            var tcp = new TcpClient("localhost", 7073);
            var peer = new TCPPeer(tcp.Client);

            var peerKey = KeyPair.Generate();
            logger.Message($"#{ID}: Connecting to peer: {host} with address {peerKey.Address.Text}");
            var request = new RequestMessage(RequestKind.None, "simnet", peerKey.Address);
            request.Sign(peerKey);
            peer.Send(request);

            int batchCount = 0;

            var rpc = new JSONRPC_Client();
            {
                logger.Message($"#{ID}: Sending funds to address {peerKey.Address.Text}");
                var hash = SendTransfer(rpc, logger, host, masterKeys, peerKey.Address, initialAmount);
                if (hash == Hash.Null)
                {
                    logger.Error($"#{ID}:Stopping, fund transfer failed");
                    return;
                }

                if (!ConfirmTransaction(rpc, logger, host, hash))
                {
                    logger.Error($"#{ID}:Stopping, fund confirmation failed");
                    return;
                }
            }

            logger.Message($"#{ID}: Beginning send mode");
            bool returnPhase = false;
            var txs = new List<Transaction>();

            var addressList = new List<KeyPair>();
            int waveCount = 0;
            while (true)
            {
                bool shouldConfirm;

                try
                {
                    txs.Clear();


                    if (returnPhase)
                    {
                        foreach (var target in addressList)
                        {
                            var script = ScriptUtils.BeginScript().AllowGas(target.Address, Address.Null, 1, 9999).TransferTokens("SOUL", target.Address, peerKey.Address, 1).SpendGas(target.Address).EndScript();
                            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30));
                            tx.Sign(target);
                            txs.Add(tx);
                        }

                        addressList.Clear();
                        returnPhase = false;
                        waveCount = 0;
                        shouldConfirm = true;
                    }
                    else
                    {
                        amount -= fee * 2 * addressesListSize;
                        if (amount <= 0)
                        {
                            break;
                        }

                        for (int j = 0; j < addressesListSize; j++)
                        {
                            var target = KeyPair.Generate();
                            addressList.Add(target);

                            var script = ScriptUtils.BeginScript().AllowGas(peerKey.Address, Address.Null, 1, 9999).TransferTokens("SOUL", peerKey.Address, target.Address, 1 + fee).SpendGas(peerKey.Address).EndScript();
                            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30));
                            tx.Sign(peerKey);
                            txs.Add(tx);
                        }

                        waveCount++;
                        if (waveCount > 10)
                        {
                            returnPhase = true;
                            shouldConfirm = true;
                        }
                        else
                        {
                            shouldConfirm = false;
                        }
                    }

                    returnPhase = !returnPhase;

                    var msg = new MempoolAddMessage(peerKey.Address, txs);
                    msg.Sign(peerKey);
                    peer.Send(msg);
                }
                catch (Exception e)
                {
                    logger.Error($"#{ID}:Fatal error : {e}");
                    return;
                }

                if (txs.Any())
                {
                    if (shouldConfirm)
                    {
                        var confirmation = ConfirmTransaction(rpc, logger, host, txs.Last().Hash);
                        if (!confirmation)
                        {
                            logger.Error($"#{ID}:Confirmation failed, aborting...");
                        }
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }

                    batchCount++;
                    logger.Message($"#{ID}:Sent {txs.Count} transactions (batch #{batchCount})");
                }
                else
                {
                    logger.Message($"#{ID}: No transactions left");
                    return;
                }

            }

            logger.Message($"#{ID}: Thread ran out of funds");
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
            logger.Message($"Fetch initial balance from {masterKeys.Address}...");
            BigInteger initialAmount = FetchBalance(rpc, logger, host, masterKeys.Address);
            if (initialAmount <= 0)
            {
                logger.Message($"Could not obtain funds");
                return;
            }

            logger.Message($"Initial balance: {UnitConversion.ToDecimal(initialAmount, DomainSettings.FuelTokenDecimals)} SOUL");

            initialAmount /= 10; // 10%
            initialAmount /= threadCount;

            logger.Message($"Estimated amount per thread: {UnitConversion.ToDecimal(initialAmount, DomainSettings.FuelTokenDecimals)} SOUL");

            for (int i = 1; i <= threadCount; i++)
            {
                logger.Message($"Starting thread #{i}...");
                try
                {
                    new Thread(() => { SenderSpawn(i, masterKeys, host, initialAmount, addressesListSize); }).Start();
                    Thread.Sleep(200);
                }
                catch (Exception e)
                {
                    logger.Error(e.ToString());
                    break;
                }
            }

            this.Run();
        }

        private void WebLogMapper(string channel, LogLevel level, string text)
        {
            if (!showWebLogs)
            {
                return;
            }

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

        public CLI(string[] args)
        {
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            var seeds = new List<string>();

            var settings = new Arguments(args);

            // this line needs to be here, to be executed as soon as possible
            InteropUtils.Seed = settings.GetString("swaps.seed", InteropUtils.Seed);

            /*
            var rnd = new Random();
            var publicKey = new byte[32];
            rnd.NextBytes(publicKey);
            for (byte opcode=0; opcode<255; opcode++)
            {
                var bytes = ByteArrayUtils.ConcatBytes(new byte[] { opcode }, publicKey);
                var text = Base58.Encode(bytes);

                Console.WriteLine(opcode + " => "+text);
            }

            Console.ReadLine();


            for (int i = 0; i < 200; i++)
            {
                var k = KeyPair.Generate();
                Console.WriteLine(k.ToWIF() + " => " + k.Address.Text);
            }
            Console.ReadLine();*/ 

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

            autoRestart = settings.GetBool("node.reboot", false);

            showWebLogs = settings.GetBool("web.log", false);
            bool apiLog = settings.GetBool("api.log", true);

            bool hasSync = settings.GetBool("sync.enabled", true);
            bool hasMempool = settings.GetBool("mempool.enabled", true);
            bool hasEvents = settings.GetBool("events.enabled", true);
            bool hasRelay = settings.GetBool("relay.enabled", true);
            bool hasArchive = settings.GetBool("archive.enabled", true);
            bool hasRPC = settings.GetBool("rpc.enabled", false);
            bool hasREST = settings.GetBool("rest.enabled", false);

            string wif = settings.GetString("node.wif");

            var nexusName = settings.GetString("nexus.name", "simnet");

            switch (mode)
            {
                case "sender":
                    string host = settings.GetString("sender.host");
                    int threadCount = settings.GetInt("sender.threads", 8);
                    int addressesPerSender = settings.GetInt("sender.addressCount", 100);
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

            int defaultPort = 0;
            for (int i = 0; i < validatorWIFs.Length; i++)
            {
                if (validatorWIFs[i] == wif)
                {
                    defaultPort = (7073 + i);
                }
            }

            if (defaultPort == 0)
            {
                defaultPort = (7073 + validatorWIFs.Length);
            }

            int port = settings.GetInt("node.port", defaultPort);
            var defaultStoragePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Storage";
            var storagePath = settings.GetString("storage.path", defaultStoragePath);
            var storageBackend = settings.GetString("storage.backend", "file");

            logger.Message("Storage backend: " + storageBackend);

            storagePath = storagePath.Replace("\\", "/");
            if (!storagePath.EndsWith('/'))
            {
                storagePath += '/';
            }

            var storageFix = settings.GetBool("storage.fix", false);

            // TODO remove this later
            if (storageFix)
            {
                if (Directory.Exists(storagePath))
                {
                    logger.Warning("Storage fix enabled... Cleaning up all storage...");
                    var di = new DirectoryInfo(storagePath);
                    foreach (FileInfo file in di.EnumerateFiles())
                    {
                        file.Delete();
                    }
                }
            }

            logger.Message("Storage path: " + storagePath);

            var node_keys = KeyPair.FromWIF(wif);
            WalletModule.Keys = KeyPair.FromWIF(wif);

            if (storageBackend == "file")
            {
                nexus = new Nexus(logger, 
                        (name) => new BasicDiskStore(storagePath + name + ".csv"),
                        (n) => new SpookOracle(this, n)
                        );
            }
            else if (storageBackend == "db")
            {
                nexus = new Nexus(logger, 
                        (name) => new DBPartition(storagePath + name),
                        (n) => new SpookOracle(this, n)
                        );
            }
            else
            {
                throw new Exception("Backend has to be set to either \"db\" or \"file\"");
            }

            bool bootstrap = false;

            if (wif == validatorWIFs[0])
            {
                if (!nexus.HasGenesis)
                {
                    logger.Debug("Boostraping nexus...");
                    bootstrap = true;
                    if (!nexus.CreateGenesisBlock(nexusName, node_keys, Timestamp.Now))
                    {
                        throw new ChainException("Genesis block failure");
                    }

                    logger.Debug("Genesis block created: " + nexus.GenesisHash);
                }
            }
            else
            {
                //nexus = new Nexus(nexusName, genesisAddress, logger);
                nexus = new Nexus(logger);
                seeds.Add("127.0.0.1:7073");
            }

            // TODO this should be later optional to enable
            nexus.AddPlugin(new ChainAddressesPlugin());
            nexus.AddPlugin(new TokenTransactionsPlugin());
            nexus.AddPlugin(new AddressTransactionsPlugin());
            nexus.AddPlugin(new UnclaimedTransactionsPlugin());

            running = true;

            // mempool setup
            int blockTime = settings.GetInt("node.blocktime", Mempool.MinimumBlockTime);

            int minimumFee;
            try
            {
                minimumFee = settings.GetInt("mempool.fee", 100000);
                if (minimumFee < 1)
                {
                    logger.Error("Invalid mempool fee value. Expected a positive value.");
                }
            }
            catch (Exception e)
            {
                logger.Error("Invalid mempool fee value. Expected something in fixed point format.");
                return;
            }

            int minimumPow;
            try
            {
                minimumPow = settings.GetInt("mempool.pow", 0);
                int maxPow = 5;
                if (minimumPow < 0 || minimumPow > maxPow)
                {
                    logger.Error($"Invalid mempool pow value. Expected a value between 0 and {maxPow}.");
                }
            }
            catch (Exception e)
            {
                logger.Error("Invalid mempool fee value. Expected something in fixed point format.");
                return;
            }


            if (hasMempool)
            {
                this.mempool = new Mempool(node_keys, nexus, blockTime, minimumFee);

                var mempoolLogging = settings.GetBool("mempool.log", true);
                if (mempoolLogging)
                {
                    mempool.OnTransactionFailed += Mempool_OnTransactionFailed;
                    mempool.OnTransactionAdded += (tx) => logger.Message($"Received transaction {tx.Hash}");
                    mempool.OnTransactionCommitted += (tx) => logger.Message($"Commited transaction {tx.Hash}");
                    mempool.OnTransactionDiscarded += (tx) => logger.Message($"Discarded transaction {tx.Hash}");
                }

                mempool.Start(ThreadPriority.AboveNormal);
            }
            else
            {
                this.mempool = null;
            }

            PeerCaps caps = PeerCaps.None;
            if (hasSync) { caps |= PeerCaps.Sync; }
            if (hasMempool) { caps |= PeerCaps.Mempool; }
            if (hasEvents) { caps |= PeerCaps.Events; }
            if (hasRelay) { caps |= PeerCaps.Relay; }
            if (hasArchive) { caps |= PeerCaps.Archive; }
            if (hasRPC) { caps |= PeerCaps.RPC; }
            if (hasREST) { caps |= PeerCaps.REST; }

            var possibleCaps = Enum.GetValues(typeof(PeerCaps)).Cast<PeerCaps>().ToArray();
            foreach (var cap in possibleCaps)
            {
                if (cap != PeerCaps.None && caps.HasFlag(cap))
                {
                    logger.Message("Feature enabled: " + cap);
                }
            }

            try
            {
                this.node = new Node(nexus, mempool, node_keys, port, caps, seeds, logger);
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return;
            }

            var useAPICache = settings.GetBool("api.cache", true);
            logger.Message($"API cache is {(useAPICache ? "enabled" : "disabled")}.");
            api = new NexusAPI(nexus, useAPICache, apiLog ? logger : null);
            api.Mempool = mempool;
            api.Node = node;

            // RPC setup
            if (hasRPC)
            {
                rpcPort = settings.GetInt("rpc.port", 7077);
                logger.Message($"RPC server listening on port {rpcPort}...");
                var rpcServer = new RPCServer(api, "/rpc", rpcPort, (level, text) => WebLogMapper("rpc", level, text));
                rpcServer.Start(ThreadPriority.AboveNormal);
            }
            else
            {
                rpcPort = 0;
            }

            // REST setup
            if (hasREST)
            {
                restPort = settings.GetInt("rest.port", 7078);
                logger.Message($"REST server listening on port {restPort}...");
                var restServer = new RESTServer(api, "/api", restPort, (level, text) => WebLogMapper("rest", level, text));
                restServer.Start(ThreadPriority.AboveNormal);
            }
            else
            {
                restPort = 0;
            }

            var neoScanURL = settings.GetString("neoscan.url", "https://api.neoscan.io");
            this.NeoScanAPI = new NeoScanAPI(neoScanURL, logger, nexus, node_keys);
            var rpcList = settings.GetString("neo.rpc", "http://seed6.ngd.network:10332,http://seed.neoeconomy.io:10332");
            var neoRpcURLs = rpcList.Split(',');
            this.NeoAPI = new Neo.Core.RemoteRPCNode(neoScanURL, neoRpcURLs);
            this.NeoAPI.SetLogger((s) => logger.Message(s));

            cryptoCompareAPIKey = settings.GetString("cryptocompare.apikey", "");
            if (!string.IsNullOrEmpty(cryptoCompareAPIKey))
            {
                logger.Message($"CryptoCompare API enabled...");
            }

            node.Start();

            if (gui != null)
            {
                int pluginPeriod = settings.GetInt("plugin.refresh", 1); // in seconds

                if (settings.GetBool("plugin.tps", false))
                {
                    RegisterPlugin(new TPSPlugin(logger, pluginPeriod));
                }

                if (settings.GetBool("plugin.ram", false))
                {
                    RegisterPlugin(new RAMPlugin(logger, pluginPeriod));
                }

                if (settings.GetBool("plugin.mempool", false))
                {
                    RegisterPlugin(new MempoolPlugin(mempool, logger, pluginPeriod));
                }
            }

            Console.CancelKeyPress += delegate {
                Terminate();
            };

            useSimulator = settings.GetBool("simulator.enabled", false);

            var dispatcher = new CommandDispatcher();
            SetupCommands(dispatcher);


            if (wif == validatorWIFs[0] && settings.GetBool("swaps.enabled"))
            {
                tokenSwapper = new TokenSwapper(node_keys, api, NeoScanAPI, NeoAPI, minimumFee, logger, settings);
                api.SwapService = tokenSwapper;

                logger.Message("Starting token swapping service...");
                new Thread(() =>
                {
                    while (node.IsRunning)
                    {
                        if (nodeReady)
                        {
                            tokenSwapper.Run();
                        }
                        else
                        {
                            Thread.Sleep(2000);
                        }
                    }
                }).Start();
            }

            if (useSimulator && bootstrap)
            {
                new Thread(() =>
                {
                    logger.Message("Initializing simulator...");
                    simulator = new NexusSimulator(this.nexus, node_keys, 1234);
                    simulator.MinimumFee = minimumFee;

                    logger.Message("Bootstrapping validators");
                    simulator.BeginBlock();
                    for (int i = 1; i < validatorWIFs.Length; i++)
                    {
                        simulator.GenerateTransfer(node_keys, Address.FromWIF(validatorWIFs[i]), this.nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals));
                    }
                    simulator.EndBlock();

                    bool fillMarket = settings.GetBool("nacho.market", false);

                    NachoServer.InitNachoServer(nexus, simulator, node_keys, fillMarket, logger);
                    MakeReady(dispatcher);

                    bool genBlocks = settings.GetBool("simulator.blocks", false);
                    if (genBlocks)
                    {
                        int blockNumber = 0;
                        while (running)
                        {
                            Thread.Sleep(5000);
                            blockNumber++;
                            logger.Message("Generating sim block #" + blockNumber);
                            try
                            {
                                simulator.CurrentTime = DateTime.UtcNow;
                                simulator.GenerateRandomBlock();
                            }
                            catch (Exception e)
                            {
                                logger.Error("Fatal error: " + e.ToString());
                                Environment.Exit(-1);
                            }
                        }
                    }
                }).Start();

            }
            else
            {
                MakeReady(dispatcher);
            }

            this.Run();
        }

        private void MakeReady(CommandDispatcher dispatcher)
        {
            logger.Success("Node is ready");
            nodeReady = true;
            gui?.MakeReady(dispatcher);
        }

        private void Mempool_OnTransactionFailed(Transaction tx)
        {
            var status = mempool.GetTransactionStatus(tx.Hash, out string reason);
            logger.Warning($"Rejected transaction {tx.Hash} => " + reason);
        }

        private void Run()
        {
            var startTime = DateTime.UtcNow;

            while (running)
            {
                if (gui != null)
                {
                    gui.Update();
                }
                else
                {
                    Thread.Sleep(1000);
                }
                this.plugins.ForEach(x => x.Update());

                if (autoRestart)
                {
                    var diff = DateTime.UtcNow - startTime;
                    if (diff.TotalMinutes >= 10)
                    {
                        this.Terminate();
                    }
                }
            }
        }

        private void Terminate()
        {
            running = false;

            logger.Message("Termination started...");

            if (mempool.IsRunning)
            {
                logger.Message("Stopping mempool...");
                mempool.Stop();
            }

            if (node.IsRunning)
            {
                logger.Message("Stopping node...");
                node.Stop();
            }

            logger.Message("Termination complete...");
            Thread.Sleep(3000);
            Environment.Exit(0);
        }

        private void RegisterPlugin(IPlugin plugin)
        {
            var name = plugin.GetType().Name.Replace("Plugin", "");

            if (this.gui == null)
            {
                logger.Warning("GUI mode required, plugin disabled: " + name);
                return;
            }

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
            logger.Message(json);
        }

        private void SetupCommands(CommandDispatcher dispatcher)
        {
            ModuleLogger.Init(logger, gui);

            dispatcher.RegisterCommand("quit", "Stops the node and exits", (args) => Terminate());

            if (gui != null)
            {
                dispatcher.RegisterCommand("gui.log", "Switches the gui to log view", (args) => gui.ShowLog(args));
                dispatcher.RegisterCommand("gui.graph", "Switches the gui to graph view", (args) => gui.ShowGraph(args));
            }

            dispatcher.RegisterCommand("help", "Lists available commands", (args) => dispatcher.Commands.ToList().ForEach(x => logger.Message($"{x.Name}\t{x.Description}")));

            foreach (var method in api.Methods)
            {
                dispatcher.RegisterCommand("api." + method.Name, "API CALL", (args) => ExecuteAPI(method.Name, args));
            }

            dispatcher.RegisterCommand("script.assemble", "Assembles a .asm file into Phantasma VM script format",
                (args) => ScriptModule.AssembleFile(args));

            dispatcher.RegisterCommand("script.disassemble", $"Disassembles a {ScriptFormat.Extension} file into readable Phantasma assembly",
                (args) => ScriptModule.DisassembleFile(args));

            dispatcher.RegisterCommand("script.compile", "Compiles a .sol file into Phantasma VM script format",
                (args) => ScriptModule.CompileFile(args));

            dispatcher.RegisterCommand("wallet.open", "Opens a wallet from a WIF key",
            (args) => WalletModule.Open(args));

            dispatcher.RegisterCommand("wallet.link", "Links thewallet NEO address to the Phantasma address",
            (args) => WalletModule.Link(api, this.mempool != null ? mempool.MinimumFee : 1, args));

            dispatcher.RegisterCommand("wallet.create", "Creates new a wallet",
            (args) => WalletModule.Create(args));

            dispatcher.RegisterCommand("wallet.balance", "Shows the current wallet balance",
                (args) => WalletModule.Balance(api, restPort, NeoScanAPI, args));

            dispatcher.RegisterCommand("wallet.transfer", "Generates a new transfer transaction",
                (args) => WalletModule.Transfer(api, this.mempool != null ? mempool.MinimumFee : 1, NeoAPI, args));

            dispatcher.RegisterCommand("wallet.stake", $"Stakes {DomainSettings.StakingTokenSymbol}",
                (args) => WalletModule.Stake(api, args));

            dispatcher.RegisterCommand("file.upload", "Uploads a file into Phantasma",
                (args) => FileModule.Upload(WalletModule.Keys, api, args));

            if (mempool != null)
            {
                dispatcher.RegisterCommand("mempool.size", "Shows size of mempool",
                    (args) => logger.Message("Size: "+mempool.Size));
            }

            dispatcher.RegisterCommand("neo.deploy", "Deploys a contract into NEO",
            (args) =>
            {
                if (args.Length != 2)
                {
                    throw new CommandException("Expected: WIF avm_path");
                }
                var avmPath = args[1];
                if (!File.Exists(avmPath))
                {
                    throw new CommandException("path for avm not found");
                }

                var keys = Neo.Core.NeoKey.FromWIF(args[0]);
                var script = File.ReadAllBytes(avmPath);
                var scriptHash = Neo.Utils.CryptoUtils.ToScriptHash(script);
                logger.Message("Deploying contract " + scriptHash);

                try
                {
                    var tx = NeoAPI.DeployContract(keys, script, Base16.Decode("0710"), 0x05, Neo.Core.ContractProperties.HasStorage | Neo.Core.ContractProperties.Payable, "Contract", "1.0", "Author", "email@gmail.com", "Description");
                    logger.Success("Deployed contract via transaction: " + tx.Hash);
                }
                catch (Exception e)
                {
                    logger.Error("Failed to deploy contract: " + e.Message);
                }
            });

            if (useSimulator)
            {
                dispatcher.RegisterCommand("simulator.timeskip", $"Skips minutse in simulator",
                    (args) =>
                    {
                        if (args.Length != 1)
                        {
                            throw new CommandException("Expected: minutes");
                        }
                        var minutes = int.Parse(args[0]);
                        simulator.CurrentTime += TimeSpan.FromMinutes(minutes);
                        logger.Success($"Simulator time advanced by {minutes}");
                    });
            }
        }
    }
}
