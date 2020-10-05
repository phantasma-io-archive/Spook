using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Phantasma.Blockchain;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Spook
{
    public class SpookSettings
    {
        public RPCSettings RPC { get; }
        public NodeSettings Node { get; }
        public AppSettings App { get; }
        public OracleSettings Oracle { get; }
        public PluginSettings Plugins { get; }
        public string PluginURL { get; }
        public SimulatorSettings Simulator { get; }
        private Arguments _settings { get; }
        private string configFile;

        public SpookSettings(string[] args)
        {
            _settings = new Arguments(args);

            this.configFile = _settings.GetString("conf", "config.json");
            var section = new ConfigurationBuilder().AddJsonFile(configFile)
                .Build().GetSection("ApplicationConfiguration");

            this.Node = new NodeSettings(_settings, section.GetSection("Node"));
            this.Simulator = new SimulatorSettings(_settings, section.GetSection("Simulator"));
            this.Oracle = new OracleSettings(_settings, section.GetSection("Oracle"));
            this.Plugins = new PluginSettings(_settings, section.GetSection("Plugins"));
            this.App = new AppSettings(_settings, section.GetSection("App"));
            this.RPC = new RPCSettings(_settings, section.GetSection("RPC"));
        }

        public string GetInteropWif(Nexus nexus, PhantasmaKeys nodeKeys, string platformName)
        {
            var genesisHash = nexus.GetGenesisHash(nexus.RootStorage);
            var interopKeys = InteropUtils.GenerateInteropKeys(nodeKeys, genesisHash, platformName);
            var defaultWif = interopKeys.ToWIF();

            string customWIF = null;

            switch (platformName)
            {
                case "neo":
                    customWIF = this.Oracle.NeoWif;
                    break;


                case "ethereum":
                    customWIF = this.Oracle.EthWif;
                    break;
            }

            var result = !string.IsNullOrEmpty(customWIF) ? customWIF: defaultWif;

            if (result != null && result.Length == 64)
            {
                var temp = new PhantasmaKeys(Base16.Decode(result));
                result = temp.ToWIF();
            }

            return result;
        }
    }

    public class NodeSettings
    {
        public string ApiProxyUrl { get; }
        public string NexusName { get; }
        public string ProfilerPath { get; }
        public string NodeMode { get; }
        public string NodeWif { get; }

        public string StoragePath { get; }
        public string DbStoragePath { get; }
        public string OraclePath { get; }
        public string StorageBackend { get; }

        public bool StorageConversion { get; }
        public string VerifyStoragePath { get; }

        public bool RandomSwapData { get; } = false;

        public int NodePort { get; }

        public bool Validator { get; }
        public bool HasSync { get; }
        public bool HasMempool { get; }
        public bool MempoolLog { get; }
        public bool HasEvents { get; }
        public bool HasRelay { get; }
        public bool HasArchive { get; }
        public bool HasRpc { get; }
        public int RpcPort { get; } = 7077;
        public List<string> Seeds { get; }

        public bool HasRest { get; }
        public int RestPort { get; } = 7078;

        public bool NexusBootstrap { get; }
        public uint GenesisTimestampUint { get; }
        public Timestamp GenesisTimestamp { get; }
        public bool ApiCache { get; }
        public bool ApiLog { get; }
        public bool Readonly { get; }

        public string SenderHost { get; } = "localhost";
        public uint SenderThreads { get; } = 8;
        public uint SenderAddressCount { get; } = 100;

        public int BlockTime { get; } = Mempool.MinimumBlockTime;
        public int MinimumFee { get; } = 100000;
        public int MinimumPow { get; } = 0;

        public bool WebLogs { get; }

        public NodeSettings(Arguments settings, IConfigurationSection section)
        {
            this.WebLogs = settings.GetBool("web.log", section.GetValue<bool>("web.logs"));

            this.BlockTime = settings.GetInt("block.time", section.GetValue<int>("block.time"));
            this.MinimumFee = settings.GetInt("minimum.fee", section.GetValue<int>("minimum.fee"));
            this.MinimumPow = settings.GetInt("minimum.pow", section.GetValue<int>("minimum.pow"));

            int maxPow = 5; // should be a constanct like MinimumBlockTime
            if (this.MinimumPow < 0 || this.MinimumPow > maxPow)
            {
                throw new Exception("Proof-Of-Work difficulty has to be between 1 and 5");
            }

            this.ApiProxyUrl = settings.GetString("api.proxy.url", section.GetValue<string>("api.proxy.url"));

            if (string.IsNullOrEmpty(this.ApiProxyUrl))
            {
                this.ApiProxyUrl = null;
            }

            this.Seeds = section.GetSection("seeds").AsEnumerable()
                                            .Where(p => p.Value != null)
                                            .Select(p => p.Value)
                                            .ToList();


            this.NexusName = settings.GetString("nexus.name", section.GetValue<string>("nexus.name"));
            this.NodeMode = settings.GetString("node.mode", section.GetValue<string>("node.mode"));
            this.NodeWif = settings.GetString("node.wif", section.GetValue<string>("node.wif"));
            this.StorageConversion = settings.GetBool("convert.storage", section.GetValue<bool>("convert.storage"));
            this.ApiLog = settings.GetBool("api.log", section.GetValue<bool>("api.log"));

            this.NodePort = settings.GetInt("node.port", section.GetValue<int>("node.port"));

            this.ProfilerPath = settings.GetString("profiler.path", section.GetValue<string>("profiler.path"));
            if (string.IsNullOrEmpty(this.ProfilerPath)) this.ProfilerPath = null;

            this.Validator = (this.NodeMode == "validator") ? true : false;

            this.HasSync = settings.GetBool("has.sync", section.GetValue<bool>("has.sync"));
            this.HasMempool = settings.GetBool("has.mempool", section.GetValue<bool>("has.mempool"));
            this.MempoolLog = settings.GetBool("mempool.log", section.GetValue<bool>("mempool.log"));
            this.HasEvents = settings.GetBool("has.events", section.GetValue<bool>("has.events"));
            this.HasRelay = settings.GetBool("has.relay", section.GetValue<bool>("has.relay"));
            this.HasArchive = settings.GetBool("has.archive", section.GetValue<bool>("has.archive"));

            this.HasRpc = settings.GetBool("has.rpc", section.GetValue<bool>("has.rpc"));
            this.RpcPort = settings.GetInt("rpc.port", section.GetValue<int>("rpc.port"));
            this.HasRest = settings.GetBool("has.rest", section.GetValue<bool>("has.rest"));
            this.RestPort = settings.GetInt("rest.port", section.GetValue<int>("rest.port"));

            this.NexusBootstrap = settings.GetBool("nexus.bootstrap", section.GetValue<bool>("nexus.bootstrap"));
            this.GenesisTimestampUint = settings.GetUInt("genesis.timestamp", section.GetValue<uint>("genesis.timestamp"));
            this.GenesisTimestamp = new Timestamp((this.GenesisTimestampUint == 0) ? Timestamp.Now.Value : this.GenesisTimestampUint);
            this.ApiCache = settings.GetBool("api.cache", section.GetValue<bool>("api.cache"));
            this.Readonly = settings.GetBool("readonly", section.GetValue<bool>("readonly"));

            this.SenderHost = settings.GetString("sender.host", section.GetValue<string>("sender.host"));
            this.SenderThreads = settings.GetUInt("sender.threads", section.GetValue<uint>("sender.threads"));
            this.SenderAddressCount = settings.GetUInt("sender.address.count", section.GetValue<uint>("sender.address.count"));

            var defaultStoragePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Storage/";
            var defaultDbStoragePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Storage/db/";
            var defaultOraclePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Oracle/";

            this.StoragePath = settings.GetString("storage.path", section.GetValue<string>("storage.path"));
            if (string.IsNullOrEmpty(this.StoragePath))
            {
                this.StoragePath = defaultStoragePath;
            }

            this.VerifyStoragePath = settings.GetString("verify.storage.path", section.GetValue<string>("verify.storage.path"));
            if (string.IsNullOrEmpty(this.VerifyStoragePath))
            {
                this.VerifyStoragePath = defaultStoragePath;
            }

            this.DbStoragePath = settings.GetString("db.storage.path", section.GetValue<string>("db.storage.path"));
            if (string.IsNullOrEmpty(this.DbStoragePath))
            {
                this.DbStoragePath = defaultDbStoragePath;
            }
            Console.WriteLine("dbstoragePath: " + DbStoragePath);

            this.OraclePath = settings.GetString("oracle.path", section.GetValue<string>("oracle.path"));
            if (string.IsNullOrEmpty(this.OraclePath))
            {
                this.OraclePath = defaultOraclePath;
            }

            this.StorageBackend = settings.GetString("storage.backend", section.GetValue<string>("storage.backend"));

            if (this.StorageConversion)
            {
                this.RandomSwapData = section.GetValue<bool>("random.Swap.data");
            }
        }
    }

    public class Contract
    {
        public string symbol { get; set; }
        public string hash { get; set; }
    }

    public class FeeUrl
    {
        public string url { get; set; }
        public string feeHeight { get; set; }
        public uint feeIncrease { get; set; }
    }

    public class OracleSettings
    {
        public string NeoscanUrl { get; }
        public List<string> NeoRpcNodes { get; }
        public List<string> EthRpcNodes { get; }
        public List<FeeUrl> EthFeeURLs { get; }
        public string CryptoCompareAPIKey { get; }
        public bool Swaps { get; }
        public string PhantasmaInteropHeight { get; } = "0";
        public string NeoInteropHeight { get; } = "4261049";
        public string EthInteropHeight { get; }
        public string NeoWif { get; }
        public string EthWif { get; }
        public uint EthConfirmations { get; }
        public uint EthGasLimit { get; }
        public bool NeoQuickSync { get; } = true;

        public OracleSettings(Arguments settings, IConfigurationSection section)
        {
            this.NeoscanUrl = settings.GetString("neoscan.api", section.GetValue<string>("neoscan.api"));
            this.NeoRpcNodes = section.GetSection("neo.rpc.specific.nodes").AsEnumerable()
                        .Where(p => p.Value != null)
                        .Select(p => p.Value)
                        .ToList();

            if (this.NeoRpcNodes.Count() == 0)
            {
                this.NeoRpcNodes = section.GetSection("neo.rpc.nodes").AsEnumerable()
                            .Where(p => p.Value != null)
                            .Select(p => p.Value)
                            .ToList();
                this.NeoQuickSync = false;
            }

            this.EthRpcNodes = section.GetSection("eth.rpc.nodes").AsEnumerable()
                        .Where(p => p.Value != null)
                        .Select(p => p.Value)
                        .ToList();

            this.EthFeeURLs = section.GetSection("eth.fee.urls").Get<List<FeeUrl>>();

            var sectionEthContracts = section.GetSection("eth.contracts");

            this.EthConfirmations = settings.GetUInt("eth.block.confirmations", section.GetValue<uint>("eth.block.confirmations"));
            this.EthGasLimit = settings.GetUInt("eth.gas.limit", section.GetValue<uint>("eth.gas.limit"));
            this.CryptoCompareAPIKey = settings.GetString("crypto.compare.key", section.GetValue<string>("crypto.compare.key"));
            this.Swaps = settings.GetBool("swaps.enabled", section.GetValue<bool>("swaps.enabled"));
            this.PhantasmaInteropHeight = settings.GetString("phantasma.interop.height", section.GetValue<string>("phantasma.interop.height"));
            this.NeoInteropHeight = settings.GetString("neo.interop.height", section.GetValue<string>("neo.interop.height"));
            this.EthInteropHeight = settings.GetString("eth.interop.height", section.GetValue<string>("eth.interop.height"));
            this.NeoWif = settings.GetString("neo.wif", section.GetValue<string>("neo.wif"));
            if (string.IsNullOrEmpty(this.NeoWif))
            {
                this.NeoWif = null;
            }
            this.EthWif = settings.GetString("eth.wif", section.GetValue<string>("eth.wif"));
            if (string.IsNullOrEmpty(this.EthWif))
            {
                this.EthWif = null;
            }
        }
    }

    public class PluginSettings
    {
        public int PluginInterval { get; }
        // TODO idea, configure plugins in a list and find classes through reflection?
        public bool TPSPlugin { get; }
        public bool RAMPlugin { get; }
        public bool MempoolPlugin { get; }

        public PluginSettings(Arguments settings, IConfigurationSection section)
        {
            this.PluginInterval = settings.GetInt("plugin.refresh.interval", section.GetValue<int>("plugin.refresh.interval"));
            this.TPSPlugin = settings.GetBool("tps.plugin", section.GetValue<bool>("tps.plugin"));
            this.RAMPlugin = settings.GetBool("ram.plugin", section.GetValue<bool>("ram.plugin"));
            this.MempoolPlugin = settings.GetBool("mempool.plugin", section.GetValue<bool>("mempool.plugin"));
        }
    }

    public class SimulatorSettings
    {
        public bool Enabled { get; }
        public List<string> Dapps { get; }
        public bool Blocks { get; }

        public SimulatorSettings(Arguments settings, IConfigurationSection section)
        {
            this.Enabled = settings.GetBool("simulator.enabled", section.GetValue<bool>("simulator.enabled"));
            this.Dapps = section.GetSection("simulator.dapps").AsEnumerable()
                                                            .Where(p => p.Value != null)
                                                            .Select(p => p.Value)
                                                            .ToList();
            this.Blocks = settings.GetBool("simulator.generate.blocks", section.GetValue<bool>("simulator.generate.blocks"));
        }
    }

    public class AppSettings
    {
        public bool UseGui { get; }
        public bool UseShell { get; }
        public string AppName { get; }
        public bool NodeStart { get; }
        public string History { get; }
        public string Config { get; }
        public string Prompt { get; }
        public string LogFile { get; }

        public AppSettings(Arguments settings, IConfigurationSection section)
        {
            this.UseGui = settings.GetBool("gui.enabled", section.GetValue<bool>("gui.enabled"));
            this.UseShell = settings.GetBool("shell.enabled", section.GetValue<bool>("shell.enabled"));
            this.AppName = settings.GetString("app.name", section.GetValue<string>("app.name"));
            this.NodeStart = settings.GetBool("node.start", section.GetValue<bool>("node.start"));
            this.History = settings.GetString("history", section.GetValue<string>("history"));
            this.Config = settings.GetString("config", section.GetValue<string>("config"));
            this.Prompt = settings.GetString("prompt", section.GetValue<string>("prompt"));
            this.LogFile = settings.GetString("log.file", section.GetValue<string>("log.file"));
        }
    }

    public class RPCSettings
    {
        public string Address { get; }
        public uint Port { get; }

        public RPCSettings(Arguments settings, IConfigurationSection section)
        {
            this.Address = settings.GetString("rpc.address", section.GetValue<string>("rpc.address"));
            this.Port = settings.GetUInt("rpc.port", section.GetValue<uint>("rpc.port"));
        }
    }
}
