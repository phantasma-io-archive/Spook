using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Network.P2P;
using Phantasma.Simulator;
using Phantasma.RocksDB;
using Phantasma.Storage;
using Phantasma.Spook.Oracles;
using Phantasma.Spook.Interop;
using Phantasma.Domain;
using Phantasma.Spook.Shell;
using Phantasma.Spook.Command;
using Phantasma.Spook.Chains;
using Phantasma.Pay.Chains;
using Phantasma.Spook.Utils;
using Phantasma.Core.Log;
using Nethereum.Hex.HexConvertors.Extensions;

using Logger = Phantasma.Core.Log.Logger;
using ConsoleLogger = Phantasma.Core.Log.ConsoleLogger;
using NeoAPI = Phantasma.Neo.Core.NeoAPI;
using EthAccount = Nethereum.Web3.Accounts.Account;

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

    public class Spook
    {
        public static readonly int Protocol = 5;

        public readonly string LogPath;
        public readonly SpookSettings Settings;

        public bool Running { get; private set; }
        public static string Version { get; private set; }
        public static string TxIdentifier => $"SPK{Version}";

        private Nexus _nexus;
        private NexusAPI _nexusApi;
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
        private Thread _tokenSwapperThread;

        public NexusAPI NexusAPI { get { return _nexusApi; } }
        public Nexus Nexus { get { return _nexus; } }
        public NeoAPI NeoAPI { get { return _neoAPI; } }
        public EthAPI EthAPI { get { return _ethAPI; } }
        public NeoScanAPI NeoScanAPI { get { return _neoScanAPI; } }
        public string CryptoCompareAPIKey  { get { return _cryptoCompareAPIKey; } }
        public TokenSwapper TokenSwapper { get { return _tokenSwapper; } }
        public Mempool Mempool { get { return _mempool; } }
        public Node Node { get { return _node; } }
        public NexusSimulator Simulator { get { return _simulator; } }
        public PhantasmaKeys NodeKeys { get { return _nodeKeys; } }
        public static Logger Logger { get; private set; }

        private void WebLogMapper(string protocol, LunarLabs.WebServer.Core.LogLevel level, string text)
        {
            if (!Settings.Node.WebLogs)
            {
                return;
            }

            text = $"{protocol} {text}";

            switch (level)
            {
                case LunarLabs.WebServer.Core.LogLevel.Debug: Logger.Debug(text); break;
                case LunarLabs.WebServer.Core.LogLevel.Error: Logger.Error(text); break;
                case LunarLabs.WebServer.Core.LogLevel.Warning: Logger.Warning(text); break;
                default: Logger.Message(text); break;
            }
        }

        public void Start()
        {
            Console.CancelKeyPress += delegate
            {
                this.Terminate();
            };

            ValidateConfig();

            _nodeKeys = SetupNodeKeys();

            Running = SetupNexus();

            SetupOracleApis();

            if (Settings.Node.HasMempool)
            {
                _mempool = SetupMempool();
            }

            _peerCaps = SetupPeerCaps();

            _node = SetupNode();

            _nexusApi = SetupNexusApi();


            if (_node != null && Settings.App.NodeStart)
            {
                _node.Start();
            }

            _commandDispatcher = SetupCommandDispatcher();

            if (Settings.Simulator.Enabled)
            {
                StartSimulator(_commandDispatcher);
            }
            else
            {
                MakeReady(_commandDispatcher);
            }

            if (Settings.Oracle.Swaps)
            {
                _tokenSwapper = StartTokenSwapper();
            }

            this.Run();
        }

        public TokenSwapper StartTokenSwapper()
        {
            var minimumFee = Settings.Node.MinimumFee;
            var oracleSettings = Settings.Oracle;
            var tokenSwapper = new TokenSwapper(this, _nodeKeys, _nexusApi, _neoAPI, _ethAPI, minimumFee, Logger);
            _nexusApi.TokenSwapper = tokenSwapper;

            _tokenSwapperThread = new Thread(() =>
            {
                Logger.Message("Running token swapping service...");
                while (Running)
                {
                    Logger.Debug("Update TokenSwapper now");
                    Task.Delay(5000).Wait();
                    if (_nodeReady)
                    {
                        tokenSwapper.Update();
                    }
                }
            });

            _tokenSwapperThread.Start();

            return tokenSwapper;
        }

        private void StartSimulator(CommandDispatcher dispatcher)
        {
            new Thread(() =>
            {
                Logger.Message("Initializing simulator...");
                _simulator = new NexusSimulator(this._nexus, _nodeKeys, 1234);
                _simulator.MinimumFee = Settings.Node.MinimumFee;

                bool genBlocks = Settings.Simulator.Blocks;
                if (genBlocks)
                {
                    int blockNumber = 0;
                    while (Running)
                    {
                        Thread.Sleep(5000);
                        blockNumber++;
                        Logger.Debug("Generating sim block #" + blockNumber);
                        try
                        {
                            _simulator.CurrentTime = DateTime.UtcNow;
                            _simulator.GenerateRandomBlock();
                        }
                        catch (Exception e)
                        {
                            Logger.Error("Fatal error: " + e.ToString());
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
            return dispatcher;
        }

        public void MakeReady(CommandDispatcher dispatcher)
        {
            var nodeMode = Settings.Node.NodeMode;
            Logger.Success($"Node is now running in {nodeMode} mode!");
            _nodeReady = true;
        }

        private void SetupOracleApis()
        {
            var neoScanURL = Settings.Oracle.NeoscanUrl;

            var neoRpcList = Settings.Oracle.NeoRpcNodes;
            this._neoAPI = new Neo.Core.RemoteRPCNode(neoScanURL, neoRpcList.ToArray());
            this._neoAPI.SetLogger((s) => Logger.Message(s));

            var ethRpcList = Settings.Oracle.EthRpcNodes;
            
            var ethWIF = Settings.GetInteropWif(Nexus, _nodeKeys, EthereumWallet.EthereumPlatform);
            var ethKeys = PhantasmaKeys.FromWIF(ethWIF);
            
            this._ethAPI = new EthAPI(this.Nexus, this.Settings, new EthAccount(ethKeys.PrivateKey.ToHex()), Logger);

            this._neoScanAPI = new NeoScanAPI(neoScanURL, Logger, _nexus, _nodeKeys);

            this._cryptoCompareAPIKey = Settings.Oracle.CryptoCompareAPIKey;
            if (!string.IsNullOrEmpty(this._cryptoCompareAPIKey))
            {
                Logger.Message($"CryptoCompare API enabled...");
            }
        }

        private Node SetupNode()
        {
            Node node = null;
            if (Settings.Node.HasSync)
            {
                if (this._mempool != null)
                {
                    this._mempool.SetKeys(_nodeKeys);
                }

                Spook.Version = Assembly.GetAssembly(typeof(Spook)).GetVersion();

                node = new Node("Spook v" + Version
                        , _nexus
                        , _mempool
                        , _nodeKeys
                        , Settings.Node.NodePort
                        , _peerCaps
                        , Settings.Node.Seeds
                        , Logger);

                if (!_nexus.HasGenesis)
                {
                    if (Settings.Node.Validator)
                    {
                        var nexusName = Settings.Node.NexusName;
                        if (Settings.Node.NexusBootstrap)
                        {
                            if (!ValidationUtils.IsValidIdentifier(nexusName))
                            {
                                Logger.Error("Invalid nexus name: " + nexusName);
                                this.Terminate();
                            }

                            Logger.Debug($"Boostraping {nexusName} nexus using {_nodeKeys.Address}...");

                            var genesisTimestamp = Settings.Node.GenesisTimestamp;

                            if (!_nexus.CreateGenesisBlock(_nodeKeys, genesisTimestamp, Phantasma.Spook.Spook.Protocol))
                            {
                                throw new ChainException("Genesis block failure");
                            }

                            Logger.Debug("Genesis block created: " + _nexus.GetGenesisHash(_nexus.RootStorage));
                        }
                        else
                        {
                            Logger.Error("No Nexus found.");
                            this.Terminate();
                        }
                    }
                    else
                    {
                        _mempool.SubmissionCallback = (tx, chain) =>
                        {
                            Logger.Message($"Relaying tx {tx.Hash} to other node");
                            //this.node.
                        };
                    }
                }
                else
                {
                    var genesisAddress = _nexus.GetGenesisAddress(_nexus.RootStorage);
                    if (Settings.Node.Validator && _nodeKeys.Address != genesisAddress && !Settings.Node.Readonly)
                    {
                        throw new Exception("Specified node key does not match genesis address " + genesisAddress.Text);
                    }
                    else
                    {
                        Logger.Success("Loaded Nexus with genesis " + _nexus.GetGenesisHash(_nexus.RootStorage));
                    }
                }
            }

            return node;
        }

        private PhantasmaKeys SetupNodeKeys()
        {
            PhantasmaKeys nodeKeys = PhantasmaKeys.FromWIF(Settings.Node.NodeWif);;
            //TODO wallet module?

            return nodeKeys;
        }

        private PeerCaps SetupPeerCaps()
        {
            PeerCaps caps = PeerCaps.None;
            if (Settings.Node.HasSync) { caps |= PeerCaps.Sync; }
            if (Settings.Node.HasMempool) { caps |= PeerCaps.Mempool; }
            if (Settings.Node.HasEvents) { caps |= PeerCaps.Events; }
            if (Settings.Node.HasRelay) { caps |= PeerCaps.Relay; }
            if (Settings.Node.HasArchive) { caps |= PeerCaps.Archive; }
            if (Settings.Node.HasRpc) { caps |= PeerCaps.RPC; }
            if (Settings.Node.HasRest) { caps |= PeerCaps.REST; }

            var possibleCaps = Enum.GetValues(typeof(PeerCaps)).Cast<PeerCaps>().ToArray();
            foreach (var cap in possibleCaps)
            {
                if (cap != PeerCaps.None && caps.HasFlag(cap))
                {
                    Logger.Message("Feature enabled: " + cap);
                }
            }

            return caps;
        }

        private bool SetupNexus()
        {
            var storagePath = Settings.Node.StoragePath;
            var oraclePath = Settings.Node.OraclePath;
            var nexusName = Settings.Node.NexusName;

            switch (Settings.Node.StorageBackend)
            {
                case StorageBackendType.CSV:
                    _nexus = new Nexus(nexusName, Logger, (name) => new BasicDiskStore(storagePath + name + ".csv"));
                    break;

                case StorageBackendType.RocksDB:
                    _nexus = new Nexus(nexusName, Logger, (name) => new DBPartition(Logger, storagePath + name));
                    break;
                default:
                    throw new Exception("Backend has to be set to either \"db\" or \"file\"");
            }

            _nexus.SetOracleReader(new SpookOracle(this, _nexus, Logger));

            return true;
        }

        private Mempool SetupMempool()
        {
            var mempool = new Mempool(_nexus
                    , Settings.Node.BlockTime
                    , Settings.Node.MinimumFee
                    , System.Text.Encoding.UTF8.GetBytes(TxIdentifier)
                    , 0
                    , Logger
                    , Settings.Node.ProfilerPath
                    );

            if (Settings.Node.MempoolLog)
            {
                mempool.OnTransactionFailed += Mempool_OnTransactionFailed;
                mempool.OnTransactionAdded += (hash) => Logger.Message($"Received transaction {hash}");
                mempool.OnTransactionCommitted += (hash) => Logger.Message($"Commited transaction {hash}");
                mempool.OnTransactionDiscarded += (hash) => Logger.Message($"Discarded transaction {hash}");
            }
            if (Settings.App.NodeStart)
            {
                mempool.Start(ThreadPriority.AboveNormal);
            }
            return mempool;

        }

        private void Mempool_OnTransactionFailed(Hash hash)
        {
            if (!Running || _mempool == null)
            {
                return;
            }

            var status = _mempool.GetTransactionStatus(hash, out string reason);
            Logger.Warning($"Rejected transaction {hash} => " + reason);
        }

        private NexusAPI SetupNexusApi()
        {
            var apiCache = Settings.Node.ApiCache;
            var apiLog = Settings.Node.ApiLog;
            var apiProxyURL = Settings.Node.ApiProxyUrl;
            var readOnlyMode = Settings.Node.Readonly;
            var hasRPC = Settings.Node.HasRpc;
            var hasREST = Settings.Node.HasRest;

            NexusAPI nexusApi = new NexusAPI(_nexus, apiCache, apiLog ? Logger : null);
            nexusApi.Mempool = _mempool;

            if (apiProxyURL != null)
            {
                nexusApi.ProxyURL = apiProxyURL;
                Logger.Message($"API will be acting as proxy for {apiProxyURL}");
            }
            else
            {
                nexusApi.Node = _node;
            }

            if (readOnlyMode)
            {
                nexusApi.acceptTransactions = false;
                Logger.Warning($"Node will be running in read-only mode.");
            }

            // RPC setup
            if (hasRPC)
            {
                var rpcPort = Settings.Node.RpcPort;
                Logger.Message($"RPC server listening on port {rpcPort}...");
                var rpcServer = new RPCServer(nexusApi, "/rpc", rpcPort, (level, text) => WebLogMapper("rpc", level, text));
                rpcServer.Start(ThreadPriority.AboveNormal);
            }

            // REST setup
            if (hasREST)
            {
                var restPort = Settings.Node.RestPort;
                Logger.Message($"REST server listening on port {restPort}...");
                var restServer = new RESTServer(nexusApi, "/api", restPort, (level, text) => WebLogMapper("rest", level, text));
                restServer.Start(ThreadPriority.AboveNormal);
            }

            return nexusApi;
        }

        private void ValidateConfig()
        {
            if (Settings.Node.ApiProxyUrl != null)
            {
                if (!Settings.Node.ApiCache)
                {
                    throw new Exception("A proxy node must have api cache enabled.");
                }

                if (Settings.Node.Validator)
                {
                    throw new Exception("A validator node cannot have a proxy url specified.");
                }

                if (!Settings.Node.HasRpc && !Settings.Node.HasRest)
                {
                    throw new Exception("API proxy must have REST or RPC enabled.");
                }
            }
            else
            {
                if (!Settings.Node.Validator && !Settings.Node.HasSync )
                {
                    throw new Exception("Non-validator nodes require sync to be enabled");
                }
            }

            if (!Settings.Node.Validator && Settings.Oracle.Swaps) 
            {
                    throw new Exception("Non-validator nodes cannot have swaps enabled");
            }


            // TODO to be continued...
        }

        public Spook(string[] args)
        {
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            this.Settings = new SpookSettings(args);

            var loggers = new List<Logger>();

            this.LogPath = Settings.Log.LogPath + Settings.Log.LogName;

            loggers.Add(new ConsoleLogger(Settings.Log.ShellLevel));
            loggers.Add(new FileLogger(LogPath, Settings.Log.FileLevel));
            Logger = new MultiLogger(loggers);

            if (!Settings.App.UseShell)
            {
                this.Start();
            }
        }

        private void Run()
        {
            Thread.Sleep(1000);
            // Do nothing in this thread...
        }

        public void Stop()
        {
            Running = false;

            Logger.Message("Termination started...");

            if (_mempool != null && _mempool.IsRunning)
            {
                Logger.Message("Stopping mempool...");
                _mempool.Stop();
            }

            if (_node != null && _node.IsRunning)
            {
                Logger.Message("Stopping node...");
                _node.Stop();
            }
        }

        public void Terminate()
        {
            if (!Running)
            {
                Logger.Message("Termination already in progress...");
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
            
            Logger.Message("Termination complete...");
            Environment.Exit(0);
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
                Logger.Warning("API returned null value...");
                return;
            }

            Logger.Message(result);
        }
    }
}
