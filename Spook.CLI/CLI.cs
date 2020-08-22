using System;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.IO;

using LunarLabs.Parser.JSON;
using LunarLabs.WebServer.Core;

using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Spook.GUI;
using Phantasma.API;
using Phantasma.Network.P2P;
using Phantasma.Spook.Modules;
using Phantasma.Spook.Plugins;
using Phantasma.Spook.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Simulator;
using Phantasma.VM.Utils;
using Phantasma.Core;
using Phantasma.Network.P2P.Messages;
using Phantasma.RocksDB;
using Phantasma.Storage;
using Phantasma.Spook.Oracles;
using Phantasma.Spook.Swaps;
using Phantasma.Domain;
using Logger = Phantasma.Core.Log.Logger;
using ConsoleLogger = Phantasma.Core.Log.ConsoleLogger;
using System.Globalization;
using NeoAPI = Phantasma.Neo.Core.NeoAPI;
using System.Reflection;
using Phantasma.Spook.Shell;
using Phantasma.Spook.Command;
using Phantasma.Spook.Chains;
using System.Threading.Tasks;
using EthAccount = Nethereum.Web3.Accounts.Account;
using Nethereum.Hex.HexConvertors.Extensions;

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
        public static readonly string Identifier = "SPK" + Assembly.GetAssembly(typeof(CLI)).GetVersion();

        private SpookSettings _settings;
        private Logger logger;
        private ConsoleGUI _gui;
        private List<IPlugin> plugins = new List<IPlugin>();
        private Nexus nexus;
        private NexusAPI _nexusApi;
        private bool _running;
        private Mempool _mempool = null;
        private PeerCaps _peerCaps;
        private PhantasmaKeys _nodeKeys;
        private Node _node;
        private bool _nodeReady = false;
        private List<string> _seeds = new List<string>();
        private NeoAPI _neoAPI;
        private EthAPI _ethAPI;
        private NeoScanAPI _neoScanAPI;
        private CommandDispatcher _commandDispatcher;
        private TokenSwapper _tokenSwapper;

        private NexusSimulator _simulator;

        private string _cryptoCompareAPIKey = null;

        public NexusAPI NexusAPI { get { return _nexusApi; } }
        public Nexus Nexus { get { return nexus; } }
        public NeoAPI NeoAPI { get { return _neoAPI; } }
        public EthAPI EthAPI { get { return _ethAPI; } }
        public NeoScanAPI NeoScanAPI { get { return _neoScanAPI; } }
        public SpookSettings Settings { get { return _settings; } }
        public string CryptoCompareAPIKey  { get { return _cryptoCompareAPIKey; } }
        public TokenSwapper TokenSwapper { get { return _tokenSwapper; } }
        public ConsoleGUI GUI { get { return _gui; } }
        public Mempool Mempool { get { return _mempool; } }
        public Node Node { get { return _node; } }
        public NexusSimulator Simulator { get { return _simulator; } }
        public Logger Logger { get { return logger; } }

        private void SenderSpawn(int ID, PhantasmaKeys masterKeys, string nexusName, string host, BigInteger initialAmount, int addressesListSize)
        {
            Throw.IfNull(logger, nameof(logger));

            Thread.CurrentThread.IsBackground = true;

            BigInteger fee = 9999; // TODO calculate the real fee

            BigInteger amount = initialAmount;

            var tcp = new TcpClient("localhost", 7073);
            var peer = new TCPPeer(tcp.Client);

            var peerKey = PhantasmaKeys.Generate();
            logger.Message($"#{ID}: Connecting to peer: {host} with address {peerKey.Address.Text}");
            var request = new RequestMessage(RequestKind.None, nexusName, peerKey.Address);
            request.Sign(peerKey);
            peer.Send(request);

            int batchCount = 0;

            var rpc = new JSONRPC_Client();
            {
                logger.Message($"#{ID}: Sending funds to address {peerKey.Address.Text}");
                var hash = WalletUtils.SendTransfer(rpc, logger, nexusName, host, masterKeys, peerKey.Address, initialAmount);
                if (hash == Hash.Null)
                {
                    logger.Error($"#{ID}:Stopping, fund transfer failed");
                    return;
                }

                if (!WalletUtils.ConfirmTransaction(rpc, logger, host, hash))
                {
                    logger.Error($"#{ID}:Stopping, fund confirmation failed");
                    return;
                }
            }

            logger.Message($"#{ID}: Beginning send mode");
            bool returnPhase = false;
            var txs = new List<Transaction>();

            var addressList = new List<PhantasmaKeys>();
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
                            var tx = new Transaction(nexusName, "main", script, Timestamp.Now + TimeSpan.FromMinutes(30));
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
                            var target = PhantasmaKeys.Generate();
                            addressList.Add(target);

                            var script = ScriptUtils.BeginScript().AllowGas(peerKey.Address, Address.Null, 1, 9999).TransferTokens("SOUL", peerKey.Address, target.Address, 1 + fee).SpendGas(peerKey.Address).EndScript();
                            var tx = new Transaction(nexusName, "main", script, Timestamp.Now + TimeSpan.FromMinutes(30));
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
                        var confirmation = WalletUtils.ConfirmTransaction(rpc, logger, host, txs.Last().Hash);
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

        private void RunSender(string wif, string nexusName, string host, int threadCount, int addressesListSize)
        {
            logger.Message("Running in sender mode.");

            _running = true;
            Console.CancelKeyPress += delegate
            {
                _running = false;
                logger.Message("Stopping sender...");
            };

            var masterKeys = PhantasmaKeys.FromWIF(wif);

            var rpc = new JSONRPC_Client();
            logger.Message($"Fetch initial balance from {masterKeys.Address}...");
            BigInteger initialAmount = WalletUtils.FetchBalance(rpc, logger, host, masterKeys.Address);
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
                    new Thread(() => { SenderSpawn(i, masterKeys, nexusName, host, initialAmount, addressesListSize); }).Start();
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
            if (!_settings.Node.WebLogs)
            {
                return;
            }

            if (_gui != null)
            {
                switch (level)
                {
                    case LogLevel.Debug: _gui.WriteToChannel(channel, Core.Log.LogEntryKind.Debug, text); break;
                    case LogLevel.Error: _gui.WriteToChannel(channel, Core.Log.LogEntryKind.Error, text); break;
                    case LogLevel.Warning: _gui.WriteToChannel(channel, Core.Log.LogEntryKind.Warning, text); break;
                    default: _gui.WriteToChannel(channel, Core.Log.LogEntryKind.Message, text); break;
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

        private static string FixPath(string path)
        {
            path = path.Replace("\\", "/");

            if (!path.EndsWith('/'))
            {
                path += '/';
            }

            return path;
        }

        public void Start()
        {
            Console.CancelKeyPress += delegate
            {
                this.Terminate();
            };

            ValidateConfig();

            _nodeKeys = SetupNodeKeys();

            _running = SetupNexus();

            SetupOracleApis();

            if (_settings.Node.HasMempool)
            {
                _mempool = SetupMempool();
            }

            _peerCaps = SetupPeerCaps();

            _node = SetupNode();

            _nexusApi = SetupNexusApi();


            if (_node != null && _settings.App.NodeStart)
            {
                _node.Start();
            }

            // TODO move to own function
            if (_settings.App.UseGui)
            {
                int pluginInterval = _settings.Plugins.PluginInterval; // in seconds

                if (_settings.Plugins.TPSPlugin)
                {
                    RegisterPlugin(new TPSPlugin(logger, pluginInterval));
                }

                if (_settings.Plugins.RAMPlugin)
                {
                    RegisterPlugin(new RAMPlugin(logger, pluginInterval));
                }

                if (_settings.Plugins.MempoolPlugin)
                {
                    RegisterPlugin(new MempoolPlugin(_mempool, logger, pluginInterval));
                }
            }

            _commandDispatcher = SetupCommandDispatcher();

            if (_settings.Simulator.Enabled)
            {
                StartSimulator(_commandDispatcher);
            }
            else
            {
                MakeReady(_commandDispatcher);
            }

            if (_settings.Oracle.Swaps)
            {
                _tokenSwapper = StartTokenSwapper();
            }

            this.Run();
        }

        private TokenSwapper StartTokenSwapper()
        {
            var minimumFee = _settings.Node.MinimumFee;
            var oracleSettings = _settings.Oracle;
            var tokenSwapper = new TokenSwapper(_settings, _nodeKeys, _nexusApi, _neoAPI, _ethAPI, minimumFee, logger);
            _nexusApi.TokenSwapper = tokenSwapper;

            new Thread(() =>
            {
                logger.Message("Running token swapping service...");
                while (_running)
                {
                    //Thread.Sleep(5000);
                    //Thread.Sleep(8000);
                    Task.Delay(5000).Wait();
                    if (_nodeReady)
                    {
                        tokenSwapper.Update();
                    }

                }
            }).Start();

            return tokenSwapper;
        }

        private void StartSimulator(CommandDispatcher dispatcher)
        {
            new Thread(() =>
            {
                logger.Message("Initializing simulator...");
                _simulator = new NexusSimulator(this.nexus, _nodeKeys, 1234);
                _simulator.MinimumFee = _settings.Node.MinimumFee;

                /*
                logger.Message("Bootstrapping validators");
                simulator.BeginBlock();
                for (int i = 1; i < validatorWIFs.Length; i++)
                {
                    simulator.GenerateTransfer(node_keys, Address.FromWIF(validatorWIFs[i]), this.nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals));
                }
                simulator.EndBlock();*/

                string[] dapps = _settings.Simulator.Dapps.ToArray();

                //DappServer.InitDapps(nexus, _simulator, _nodeKeys, dapps, _settings.Node.MinimumFee, logger);

                bool genBlocks = _settings.Simulator.Blocks;
                if (genBlocks)
                {
                    int blockNumber = 0;
                    while (_running)
                    {
                        Thread.Sleep(5000);
                        blockNumber++;
                        logger.Message("Generating sim block #" + blockNumber);
                        try
                        {
                            _simulator.CurrentTime = DateTime.UtcNow;
                            _simulator.GenerateRandomBlock();
                        }
                        catch (Exception e)
                        {
                            logger.Error("Fatal error: " + e.ToString());
                            Environment.Exit(-1);
                        }
                    }
                }

                MakeReady(dispatcher);
            }).Start();
        }

        private CommandDispatcher SetupCommandDispatcher()
        {
            var dispatcher = new CommandDispatcher(this);
            ModuleLogger.Init(logger, _gui);
            return dispatcher;
        }

        public void MakeReady(CommandDispatcher dispatcher)
        {
            var nodeMode = _settings.Node.NodeMode;
            logger.Success($"Node is now running in {nodeMode} mode!");
            _nodeReady = true;
            _gui?.MakeReady(dispatcher);
        }

        private void SetupOracleApis()
        {
            var neoScanURL = _settings.Oracle.NeoscanUrl;

            var neoRpcList = _settings.Oracle.NeoRpcNodes;
            this._neoAPI = new Neo.Core.RemoteRPCNode(neoScanURL, neoRpcList.ToArray());
            this._neoAPI.SetLogger((s) => logger.Message(s));

            var ethRpcList = _settings.Oracle.EthRpcNodes;
            var interopKeys = InteropUtils.GenerateInteropKeys(_nodeKeys, Nexus.GetGenesisHash(Nexus.RootStorage), "ethereum");

            this._ethAPI = new EthAPI(this._settings, new EthAccount(interopKeys.PrivateKey.ToHex()));
            this._ethAPI.SetLogger((s) => logger.Message(s));

            this._neoScanAPI = new NeoScanAPI(neoScanURL, logger, nexus, _nodeKeys);

            this._cryptoCompareAPIKey = _settings.Oracle.CryptoCompareAPIKey;
            if (!string.IsNullOrEmpty(this._cryptoCompareAPIKey))
            {
                logger.Message($"CryptoCompare API enabled...");
            }
        }

        private Node SetupNode()
        {
            Node node = null;
            if (_settings.Node.HasSync)
            {
                if (this._mempool != null)
                {
                    this._mempool.SetKeys(_nodeKeys);
                }

                node = new Node("Spook v" + Assembly.GetAssembly(typeof(CLI)).GetVersion()
                        , nexus
                        , _mempool
                        , _nodeKeys
                        , _settings.Node.NodePort
                        , _peerCaps
                        , _settings.Node.Seeds
                        , logger);

                if (!nexus.HasGenesis)
                {
                    if (_settings.Node.Validator)
                    {
                        var nexusName = _settings.Node.NexusName;
                        if (_settings.Node.NexusBootstrap)
                        {
                            if (!ValidationUtils.IsValidIdentifier(nexusName))
                            {
                                logger.Error("Invalid nexus name: " + nexusName);
                                this.Terminate();
                            }

                            logger.Debug($"Boostraping {nexusName} nexus using {_nodeKeys.Address}...");

                            var genesisTimestamp = _settings.Node.GenesisTimestamp;

                            if (!nexus.CreateGenesisBlock(_nodeKeys, genesisTimestamp))
                            {
                                throw new ChainException("Genesis block failure");
                            }

                            logger.Debug("Genesis block created: " + nexus.GetGenesisHash(nexus.RootStorage));
                        }
                        else
                        {
                            logger.Error("No Nexus found.");
                            this.Terminate();
                        }
                    }
                    else
                    {
                        _mempool.SubmissionCallback = (tx, chain) =>
                        {
                            logger.Message($"Relaying tx {tx.Hash} to other node");
                            //this.node.
                        };
                    }
                }
                else
                {
                    var genesisAddress = nexus.GetGenesisAddress(nexus.RootStorage);
                    if (_settings.Node.Validator && _nodeKeys.Address != genesisAddress)
                    {
                        throw new Exception("Specified node key does not match genesis address " + genesisAddress.Text);
                    }
                    else
                    {
                        logger.Success("Loaded Nexus with genesis " + nexus.GetGenesisHash(nexus.RootStorage));
                    }
                }
            }

            return node;
        }

        private PhantasmaKeys SetupNodeKeys()
        {
            Console.WriteLine("NodeWif: " + _settings.Node.NodeWif);
            PhantasmaKeys nodeKeys = PhantasmaKeys.FromWIF(_settings.Node.NodeWif);;
            //TODO wallet module?

            return nodeKeys;
        }

        private PeerCaps SetupPeerCaps()
        {
            PeerCaps caps = PeerCaps.None;
            if (_settings.Node.HasSync) { caps |= PeerCaps.Sync; }
            if (_settings.Node.HasMempool) { caps |= PeerCaps.Mempool; }
            if (_settings.Node.HasEvents) { caps |= PeerCaps.Events; }
            if (_settings.Node.HasRelay) { caps |= PeerCaps.Relay; }
            if (_settings.Node.HasArchive) { caps |= PeerCaps.Archive; }
            if (_settings.Node.HasRpc) { caps |= PeerCaps.RPC; }
            if (_settings.Node.HasRest) { caps |= PeerCaps.REST; }

            var possibleCaps = Enum.GetValues(typeof(PeerCaps)).Cast<PeerCaps>().ToArray();
            foreach (var cap in possibleCaps)
            {
                if (cap != PeerCaps.None && caps.HasFlag(cap))
                {
                    logger.Message("Feature enabled: " + cap);
                }
            }

            return caps;
        }

        private bool SetupNexus()
        {
            var storagePath = _settings.Node.StoragePath;
            var oraclePath = _settings.Node.OraclePath;
            var dbstoragePath = _settings.Node.DbStoragePath; // maybe we can get rid of dbstoragepath?
            var nexusName = _settings.Node.NexusName;

            switch (_settings.Node.StorageBackend)
            {
                case "file":
                    nexus = new Nexus(nexusName, logger, (name) => new BasicDiskStore(storagePath + name + ".csv"));
                    break;

                case "db":
                    nexus = new Nexus(nexusName, logger, (name) => new DBPartition(logger, dbstoragePath + name));
                    break;
                default:
                    throw new Exception("Backend has to be set to either \"db\" or \"file\"");
            }

            nexus.SetOracleReader(new SpookOracle(this, nexus, logger));

            return true;
        }

        private Mempool SetupMempool()
        {
            var mempool = new Mempool(nexus
                    , _settings.Node.BlockTime
                    , _settings.Node.MinimumFee
                    , System.Text.Encoding.UTF8.GetBytes(Identifier)
                    , 0
                    , logger
                    , _settings.Node.ProfilerPath
                    );

            if (_settings.Node.MempoolLog)
            {
                mempool.OnTransactionFailed += Mempool_OnTransactionFailed;
                mempool.OnTransactionAdded += (hash) => logger.Message($"Received transaction {hash}");
                mempool.OnTransactionCommitted += (hash) => logger.Message($"Commited transaction {hash}");
                mempool.OnTransactionDiscarded += (hash) => logger.Message($"Discarded transaction {hash}");
            }
            if (_settings.App.NodeStart)
            {
                mempool.Start(ThreadPriority.AboveNormal);
            }
            return mempool;

        }

        private void Mempool_OnTransactionFailed(Hash hash)
        {
            if (!_running || _mempool == null)
            {
                return;
            }

            var status = _mempool.GetTransactionStatus(hash, out string reason);
            logger.Warning($"Rejected transaction {hash} => " + reason);
        }

        private NexusAPI SetupNexusApi()
        {
            var apiCache = _settings.Node.ApiCache;
            var apiLog = _settings.Node.ApiLog;
            var apiProxyURL = _settings.Node.ApiProxyUrl;
            var readOnlyMode = _settings.Node.Readonly;
            var hasRPC = _settings.Node.HasRpc;
            var hasREST = _settings.Node.HasRest;

            NexusAPI nexusApi = new NexusAPI(nexus, apiCache, apiLog ? logger : null);
            nexusApi.Mempool = _mempool;

            if (apiProxyURL != null)
            {
                nexusApi.ProxyURL = apiProxyURL;
                logger.Message($"API will be acting as proxy for {apiProxyURL}");

                if (readOnlyMode)
                {
                    nexusApi.acceptTransactions = false;
                    logger.Warning($"Node will be running in read-only mode.");
                }
            }
            else
            {
                nexusApi.Node = _node;
            }

            // RPC setup
            if (hasRPC)
            {
                var rpcPort = _settings.Node.RpcPort;
                logger.Message($"RPC server listening on port {rpcPort}...");
                var rpcServer = new RPCServer(nexusApi, "/rpc", rpcPort, (level, text) => WebLogMapper("rpc", level, text));
                rpcServer.Start(ThreadPriority.AboveNormal);
            }

            // REST setup
            if (hasREST)
            {
                var restPort = _settings.Node.RestPort;
                logger.Message($"REST server listening on port {restPort}...");
                var restServer = new RESTServer(nexusApi, "/api", restPort, (level, text) => WebLogMapper("rest", level, text));
                restServer.Start(ThreadPriority.AboveNormal);
            }

            return nexusApi;
        }

        private void ValidateConfig()
        {
            if (_settings.Node.ApiProxyUrl != null)
            {
                if (!_settings.Node.ApiCache)
                {
                    throw new Exception("A proxy node must have api cache enabled.");
                }

                if (_settings.Node.Validator)
                {
                    throw new Exception("A validator node cannot have a proxy url specified.");
                }

                if (!_settings.Node.HasRpc && !_settings.Node.HasRest)
                {
                    throw new Exception("API proxy must have REST or RPC enabled.");
                }
            }
            else
            {
                if (!_settings.Node.Validator && !_settings.Node.HasSync )
                {
                    throw new Exception("Non-validator nodes require sync to be enabled");
                }
            }

            if (!_settings.Node.Validator && _settings.Oracle.Swaps) 
            {
                    throw new Exception("Non-validator nodes cannot have swaps enabled");
            }


            // TODO to be continued...
        }

        public CLI(string[] args, SpookSettings settings)
        {
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            this._settings = settings;

            if (_settings.App.UseShell)
            {
                logger = new ShellLogger(Path.GetTempPath() + settings.App.LogFile);
            }
            else
            if (_settings.App.UseGui)
            {
                _gui = new ConsoleGUI(_settings.App.UseGui); // if we use GUI we want to update
                logger = _gui;
            }
            else
            {
                logger = new ConsoleLogger();
            }

            if (!_settings.App.UseShell)
            {
                this.Start();
            }
        }

        private void Run()
        {
            // UI thread
            new Thread(() =>
            {
                while (_running)
                {
                    if (_gui != null)
                    {
                        _gui.Update();
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                    this.plugins.ForEach(x => x.Update());
                }
            }).Start();
        }

        public void Stop()
        {
            _running = false;

            logger.Message("Termination started...");

            if (_mempool != null && _mempool.IsRunning)
            {
                logger.Message("Stopping mempool...");
                _mempool.Stop();
            }

            if (_node != null && _node.IsRunning)
            {
                logger.Message("Stopping node...");
                _node.Stop();
            }
        }

        public void Terminate()
        {
            if (!_running)
            {
                logger.Message("Termination already in progress...");
            }

            if (Prompt.running)
            {
                Prompt.running = false;
            }

            this.Stop();

            //Thread.Sleep(3000);
            if (Prompt.running)
            {
                Prompt.running = false;
            }
            logger.Message("Termination complete...");
            Environment.Exit(0);
        }

        private void RegisterPlugin(IPlugin plugin)
        {
            var name = plugin.GetType().Name.Replace("Plugin", "");

            if (_gui == null)
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

        public string ExecuteAPIR(string name, string[] args)
        {
            var result = _nexusApi.Execute(name, args);
            if (result == null)
            {
                return "";
            }

            return result;
        }

        public void ExecuteAPI(string name, string[] args)
        {
            var result = _nexusApi.Execute(name, args);
            if (result == null)
            {
                logger.Warning("API returned null value...");
                return;
            }

            logger.Shell(result);
        }
    }
}
