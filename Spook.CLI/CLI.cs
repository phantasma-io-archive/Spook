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
using Phantasma.API;
using Phantasma.Network.P2P;
using Phantasma.Spook.Utils;
using Phantasma.Simulator;
using Phantasma.VM.Utils;
using Phantasma.Core;
using Phantasma.Network.P2P.Messages;
using Phantasma.RocksDB;
using Phantasma.Storage;
using Phantasma.Spook.Oracles;
using Phantasma.Spook.Interop;
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
using Phantasma.Pay.Chains;

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

    public class CLI
    {
        public static readonly string Identifier = "SPK" + Assembly.GetAssembly(typeof(CLI)).GetVersion();
        public static readonly int Protocol = 3;

        private SpookSettings _settings;
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
        private Thread tokenSwapperThread;

        public NexusAPI NexusAPI { get { return _nexusApi; } }
        public Nexus Nexus { get { return nexus; } }
        public NeoAPI NeoAPI { get { return _neoAPI; } }
        public EthAPI EthAPI { get { return _ethAPI; } }
        public NeoScanAPI NeoScanAPI { get { return _neoScanAPI; } }
        public SpookSettings Settings { get { return _settings; } }
        public string CryptoCompareAPIKey  { get { return _cryptoCompareAPIKey; } }
        public TokenSwapper TokenSwapper { get { return _tokenSwapper; } }
        public Mempool Mempool { get { return _mempool; } }
        public Node Node { get { return _node; } }
        public NexusSimulator Simulator { get { return _simulator; } }
        public PhantasmaKeys NodeKeys { get { return _nodeKeys; } }
        public static Logger Logger { get; private set; }

        private void WebLogMapper(string channel, LogLevel level, string text)
        {
            if (!_settings.Node.WebLogs)
            {
                return;
            }

            switch (level)
            {
                case LogLevel.Debug: Logger.Debug(text); break;
                case LogLevel.Error: Logger.Error(text); break;
                case LogLevel.Warning: Logger.Warning(text); break;
                default: Logger.Message(text); break;
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

        public TokenSwapper StartTokenSwapper()
        {
            var minimumFee = _settings.Node.MinimumFee;
            var oracleSettings = _settings.Oracle;
            var tokenSwapper = new TokenSwapper(_settings, _nodeKeys, _nexusApi, _neoAPI, _ethAPI, minimumFee, Logger);
            _nexusApi.TokenSwapper = tokenSwapper;

            tokenSwapperThread = new Thread(() =>
            {
                Logger.Message("Running token swapping service...");
                while (_running)
                {
                    Logger.Message("Update TokenSwapper now");
                    Task.Delay(5000).Wait();
                    if (_nodeReady)
                    {
                        tokenSwapper.Update();
                    }
                }
            });

            tokenSwapperThread.Start();

            return tokenSwapper;
        }

        private void StartSimulator(CommandDispatcher dispatcher)
        {
            new Thread(() =>
            {
                Logger.Message("Initializing simulator...");
                _simulator = new NexusSimulator(this.nexus, _nodeKeys, 1234);
                _simulator.MinimumFee = _settings.Node.MinimumFee;

                bool genBlocks = _settings.Simulator.Blocks;
                if (genBlocks)
                {
                    int blockNumber = 0;
                    while (_running)
                    {
                        Thread.Sleep(5000);
                        blockNumber++;
                        Logger.Message("Generating sim block #" + blockNumber);
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
            var nodeMode = _settings.Node.NodeMode;
            Logger.Success($"Node is now running in {nodeMode} mode!");
            _nodeReady = true;
        }

        private void SetupOracleApis()
        {
            var neoScanURL = _settings.Oracle.NeoscanUrl;

            var neoRpcList = _settings.Oracle.NeoRpcNodes;
            this._neoAPI = new Neo.Core.RemoteRPCNode(neoScanURL, neoRpcList.ToArray());
            this._neoAPI.SetLogger((s) => Logger.Message(s));

            var ethRpcList = _settings.Oracle.EthRpcNodes;
            
            var ethWIF = _settings.GetInteropWif(Nexus, _nodeKeys, EthereumWallet.EthereumPlatform);
            var ethKeys = PhantasmaKeys.FromWIF(ethWIF);
            
            this._ethAPI = new EthAPI(this.Nexus, this._settings, new EthAccount(ethKeys.PrivateKey.ToHex()));
            this._ethAPI.SetLogger((s) => Logger.Message(s));

            this._neoScanAPI = new NeoScanAPI(neoScanURL, Logger, nexus, _nodeKeys);

            this._cryptoCompareAPIKey = _settings.Oracle.CryptoCompareAPIKey;
            if (!string.IsNullOrEmpty(this._cryptoCompareAPIKey))
            {
                Logger.Message($"CryptoCompare API enabled...");
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
                        , Logger);

                if (!nexus.HasGenesis)
                {
                    if (_settings.Node.Validator)
                    {
                        var nexusName = _settings.Node.NexusName;
                        if (_settings.Node.NexusBootstrap)
                        {
                            if (!ValidationUtils.IsValidIdentifier(nexusName))
                            {
                                Logger.Error("Invalid nexus name: " + nexusName);
                                this.Terminate();
                            }

                            Logger.Debug($"Boostraping {nexusName} nexus using {_nodeKeys.Address}...");

                            var genesisTimestamp = _settings.Node.GenesisTimestamp;

                            if (!nexus.CreateGenesisBlock(_nodeKeys, genesisTimestamp, Spook.CLI.Protocol))
                            {
                                throw new ChainException("Genesis block failure");
                            }

                            Logger.Debug("Genesis block created: " + nexus.GetGenesisHash(nexus.RootStorage));
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
                    var genesisAddress = nexus.GetGenesisAddress(nexus.RootStorage);
                    if (_settings.Node.Validator && _nodeKeys.Address != genesisAddress && !_settings.Node.Readonly)
                    {
                        throw new Exception("Specified node key does not match genesis address " + genesisAddress.Text);
                    }
                    else
                    {
                        Logger.Success("Loaded Nexus with genesis " + nexus.GetGenesisHash(nexus.RootStorage));
                    }
                }
            }

            return node;
        }

        private PhantasmaKeys SetupNodeKeys()
        {
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
                    Logger.Message("Feature enabled: " + cap);
                }
            }

            return caps;
        }

        private bool SetupNexus()
        {
            var storagePath = _settings.Node.StoragePath;
            var oraclePath = _settings.Node.OraclePath;
            var nexusName = _settings.Node.NexusName;

            switch (_settings.Node.StorageBackend)
            {
                case StorageBackendType.CSV:
                    nexus = new Nexus(nexusName, Logger, (name) => new BasicDiskStore(storagePath + name + ".csv"));
                    break;

                case StorageBackendType.RocksDB:
                    nexus = new Nexus(nexusName, Logger, (name) => new DBPartition(Logger, storagePath + name));
                    break;
                default:
                    throw new Exception("Backend has to be set to either \"db\" or \"file\"");
            }

            nexus.SetOracleReader(new SpookOracle(this, nexus, Logger));

            return true;
        }

        private Mempool SetupMempool()
        {
            var mempool = new Mempool(nexus
                    , _settings.Node.BlockTime
                    , _settings.Node.MinimumFee
                    , System.Text.Encoding.UTF8.GetBytes(Identifier)
                    , 0
                    , Logger
                    , _settings.Node.ProfilerPath
                    );

            if (_settings.Node.MempoolLog)
            {
                mempool.OnTransactionFailed += Mempool_OnTransactionFailed;
                mempool.OnTransactionAdded += (hash) => Logger.Message($"Received transaction {hash}");
                mempool.OnTransactionCommitted += (hash) => Logger.Message($"Commited transaction {hash}");
                mempool.OnTransactionDiscarded += (hash) => Logger.Message($"Discarded transaction {hash}");
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
            Logger.Warning($"Rejected transaction {hash} => " + reason);
        }

        private NexusAPI SetupNexusApi()
        {
            var apiCache = _settings.Node.ApiCache;
            var apiLog = _settings.Node.ApiLog;
            var apiProxyURL = _settings.Node.ApiProxyUrl;
            var readOnlyMode = _settings.Node.Readonly;
            var hasRPC = _settings.Node.HasRpc;
            var hasREST = _settings.Node.HasRest;

            NexusAPI nexusApi = new NexusAPI(nexus, apiCache, apiLog ? Logger : null);
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
                var rpcPort = _settings.Node.RpcPort;
                Logger.Message($"RPC server listening on port {rpcPort}...");
                var rpcServer = new RPCServer(nexusApi, "/rpc", rpcPort, (level, text) => WebLogMapper("rpc", level, text));
                rpcServer.Start(ThreadPriority.AboveNormal);
            }

            // REST setup
            if (hasREST)
            {
                var restPort = _settings.Node.RestPort;
                Logger.Message($"REST server listening on port {restPort}...");
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
                Logger = new ShellLogger(Path.GetTempPath() + settings.App.LogFile);
            }
            else
            {
                Logger = new ConsoleLogger();
            }

            if (!_settings.App.UseShell)
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
            _running = false;

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
            if (!_running)
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
