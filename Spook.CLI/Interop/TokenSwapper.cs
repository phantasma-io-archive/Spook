using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Core.Log;
using Phantasma.Neo.Core;
using Phantasma.Blockchain;
using Phantasma.Domain;
using Phantasma.Pay.Chains;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Types;

using Phantasma.API;
using Phantasma.Storage.Context;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using Phantasma.Spook.Chains;
using EthereumKey = Phantasma.Ethereum.EthereumKey;
using Nethereum.RPC.Eth.DTOs;

namespace Phantasma.Spook.Interop
{
    public enum SwapStatus
    {
        InProgress,
        Settle,
        Confirm,
        Finished
    }

    public struct PendingSwap: ISerializable
    {
        public string platform;
        public Hash hash;
        public Address source;
        public Address destination;

        public PendingSwap(string platform, Hash hash, Address source, Address destination)
        {
            this.platform = platform;
            this.hash = hash;
            this.source = source;
            this.destination = destination;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(platform);
            writer.WriteHash(hash);
            writer.WriteAddress(source);
            writer.WriteAddress(destination);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.platform = reader.ReadVarString();
            this.hash = reader.ReadHash();
            this.source = reader.ReadAddress();
            this.destination = reader.ReadAddress();
        }
    }

    public struct PendingFee : ISerializable
    {
        public Hash sourceHash;
        public Hash destinationHash;
        public Hash settleHash;
        public Timestamp time;
        public SwapStatus status;

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteHash(sourceHash);
            writer.WriteHash(destinationHash);
            writer.WriteHash(settleHash);
            writer.Write(time.Value);
            writer.Write((byte)status);
        }

        public void UnserializeData(BinaryReader reader)
        {
            sourceHash = reader.ReadHash();
            destinationHash = reader.ReadHash();
            settleHash = reader.ReadHash();
            time = new Timestamp(reader.ReadUInt32());
            this.status = (SwapStatus)reader.ReadByte();
        }
    }

    public abstract class ChainWatcher
    {
        public readonly string PlatformName;
        public readonly TokenSwapper Swapper;
        public readonly string LocalAddress;

        protected ChainWatcher(TokenSwapper swapper, string wif, string platformName)
        {
            Swapper = swapper;
            this.PlatformName = platformName;
            this.LocalAddress = swapper.FindAddress(platformName);

            // for testing with mainnet swap address
            //this.LocalAddress = "AbFdbvacCeBrncvwYnPEtfKqyr5KU9SWAU"; //swapper.FindAddress(platformName);

            if (string.IsNullOrEmpty(LocalAddress))
            {
                throw new SwapException($"Invalid address for {platformName} swaps");
            }

            var localKeys = GetAvailableAddress(wif);
            if (localKeys == LocalAddress)
            {
                Swapper.logger.Message($"Listening for {platformName} swaps at address {LocalAddress.ToLower()}");
            }
            else
            {
                Swapper.logger.Error($"Expected {platformName} keys to {LocalAddress}, instead got keys to {localKeys}");
            }
        }

        protected abstract string GetAvailableAddress(string wif);
        public abstract IEnumerable<PendingSwap> Update();
        public abstract void ResyncBlock(System.Numerics.BigInteger blockId);
    }

    public class TokenSwapper : ITokenSwapper
    {
        public readonly NexusAPI NexusAPI;
        public Nexus Nexus => NexusAPI.Nexus;
        public readonly Logger logger;
        public Dictionary<string, string> SwapAddresses = new Dictionary<string,string>();

        internal readonly PhantasmaKeys SwapKeys;

        private readonly SpookSettings _settings;
        private StorageContext Storage;
        private readonly BigInteger MinimumFee;
        private readonly NeoAPI neoAPI;
        private readonly EthAPI ethAPI;
        private OracleReader OracleReader;
        private readonly string _txIdentifier;

        private readonly Dictionary<string, BigInteger> interopBlocks;
        private PlatformInfo[] platforms;
        private Dictionary<string, string> wifs = new Dictionary<string, string>(); 
        private Dictionary<string, ChainWatcher> _finders = new Dictionary<string, ChainWatcher>();

        public TokenSwapper(SpookSettings settings, PhantasmaKeys swapKey, NexusAPI nexusAPI, NeoAPI neoAPI, EthAPI ethAPI, BigInteger minFee, Logger logger)
        {
            this.logger = logger;
            this._settings = settings;
            this.SwapKeys = swapKey;
            this.NexusAPI = nexusAPI;
            this.OracleReader = Nexus.GetOracleReader();
            this.MinimumFee = minFee;
            this.neoAPI = neoAPI;
            this.ethAPI = ethAPI;
            this._txIdentifier = "SPK" + Assembly.GetAssembly(typeof(CLI)).GetVersion();

            this.Storage = new KeyStoreStorage(Nexus.CreateKeyStoreAdapter("swaps"));

            this.interopBlocks = new Dictionary<string, BigInteger>();

            interopBlocks["phantasma"] = BigInteger.Parse(_settings.Oracle.PhantasmaInteropHeight);
            interopBlocks["neo"] = BigInteger.Parse(_settings.Oracle.NeoInteropHeight);
            interopBlocks["ethereum"] = BigInteger.Parse(_settings.Oracle.EthInteropHeight);

            InitWIF("neo");
            InitWIF("ethereum");
        }

        private void InitWIF(string platformName)
        {
            var wif = _settings.GetInteropWif(this.Nexus, this.SwapKeys, platformName);
            wifs[platformName] = wif;
        }

        internal IToken FindTokenByHash(string asset, string platform)
        {
            var hash = Hash.FromUnpaddedHex(asset);
            var symbols = Nexus.GetTokens(Nexus.RootStorage);

            foreach (var symbol in symbols)
            {
                var otherHash = Nexus.GetTokenPlatformHash(symbol, platform, Nexus.RootStorage);
                if (hash == otherHash)
                {
                    return Nexus.GetTokenInfo(Nexus.RootStorage, symbol);
                }
            }

            return null;
        }

        internal string FindAddress(string platformName)
        {
            //TODO use last address for now, needs to be fixed in the future
            return platforms.Where(x => x.Name == platformName).Select(x => x.InteropAddresses[x.InteropAddresses.Length-1].ExternalAddress).FirstOrDefault();
        }

        private const string SettlementTag = ".settled";
        private const string PendingTag = ".pending";
        private const string InProgressTag = ".inprogress";

        private Dictionary<Hash, PendingSwap> _pendingSwaps = new Dictionary<Hash, PendingSwap>();
        private Dictionary<Address, List<Hash>> _swapAddressMap = new Dictionary<Address, List<Hash>>();
        private Dictionary<string, Task<IEnumerable<PendingSwap>>> taskDict = new Dictionary<string, Task<IEnumerable<PendingSwap>>>();

        private void MapSwap(Address address, Hash hash)
        {
            List<Hash> list;

            if (_swapAddressMap.ContainsKey(address))
            {
                list = _swapAddressMap[address];
            }
            else
            {
                list = new List<Hash>();
                _swapAddressMap[address] = list;
            }

            list.Add(hash);
        }

        public void ResyncBlockOnChain(string platform, string blockId)
        {
            if (_finders.TryGetValue(platform, out ChainWatcher finder) 
                    && System.Numerics.BigInteger.TryParse(blockId, out var bigIntBlock))
            {
                this.logger.Message($"TokenSwapper: Resync block {blockId} on platform {platform}");
                finder.ResyncBlock(bigIntBlock);
            }
        }

        public void Update()
        {
            try
            {
                if (this.platforms == null)
                {
                    if (!Nexus.HasGenesis)
                    {
                        return;
                    }

                    var platforms = Nexus.GetPlatforms(Nexus.RootStorage);
                    this.platforms = platforms.Select(x => Nexus.GetPlatformInfo(Nexus.RootStorage, x)).ToArray();

                    if (this.platforms.Length == 0)
                    {
                        logger.Warning("No interop platforms found. Make sure that the Nexus was created correctly.");
                        return;
                    }

                    _finders["neo"] = new NeoInterop(this, neoAPI, wifs["neo"], interopBlocks["neo"], OracleReader,
                            _settings.Oracle.NeoQuickSync, logger);
                    SwapAddresses["neo"] = _finders["neo"].LocalAddress;

                    _finders["ethereum"] = new EthereumInterop(this, ethAPI, wifs["ethereum"], interopBlocks["ethereum"],
                            OracleReader, Nexus.GetPlatformTokenHashes("ethereum", Nexus.RootStorage).Select(x => x.ToString().Substring(0, 40)).ToArray(), _settings.Oracle.EthConfirmations,
                            Nexus, logger);
                    SwapAddresses["ethereum"] = _finders["ethereum"].LocalAddress;

                    logger.Message("Available swap addresses:");
                    foreach (var x in SwapAddresses)
                    {
                        logger.Message("platform: " + x.Key + " address: " + x.Value);
                    }
                }

                if (this.platforms.Length == 0)
                {
                    return;
                }
                else
                {
                    if (taskDict.Count == 0)
                    {
                        foreach (var platform in this.platforms)
                        {
                            taskDict.Add(platform.Name, null);
                        }
                    }
                }

                var pendingList = new StorageList(PendingTag, this.Storage);
                int i = 0;
                var count = pendingList.Count();

                while (i < count)
                {
                    var settlement = pendingList.Get<PendingFee>(i);
                    if (UpdatePendingSettle(pendingList, i))
                    {
                        pendingList.RemoveAt<PendingFee>(i);
                        count--;
                    }
                    else
                    {
                        i++;
                    }
                }

                ProcessCompletedTasks();

                for (var j = 0; j < taskDict.Count; j++)
                {
                    var platform = taskDict.Keys.ElementAt(j);
                    var task = taskDict[platform];
                    if (task == null)
                    {
                        if (_finders.TryGetValue(platform, out ChainWatcher finder))
                        {
                            taskDict[platform] = new Task<IEnumerable<PendingSwap>>(() =>
                                                    {
                                                        return finder.Update();
                                                    });
                        }
                    }
                }

                // start new tasks
                foreach (var entry in taskDict)
                {
                    var task = entry.Value;
                    if (task != null && task.Status.Equals(TaskStatus.Created))
                    {
                        task.ContinueWith(t => { Console.WriteLine($"===> task {task.ToString()} failed"); }, TaskContinuationOptions.OnlyOnFaulted);
                        task.Start();
                    }
                }
            }
            catch (Exception e)
            {
                var logMessage = "TokenSwapper.Update() exception caught:\n" + e.Message;
                var inner = e.InnerException;
                while (inner != null)
                {
                    logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;
                    inner = inner.InnerException;
                }
                logMessage += "\n\n" + e.StackTrace;

                logger.Error(logMessage);
            }
        }

        public void ProcessCompletedTasks()
        {
            for (var i = 0; i < taskDict.Count; i++)
            {
                var platform = taskDict.Keys.ElementAt(i);
                var task = taskDict[platform];
                if (task != null)
                {
                    if (task.IsCompleted)
                    {
                        if (task.IsFaulted)
                        {
                            taskDict[platform] = null;
                            continue;
                        }
                        else
                        {
                            var swaps = task.Result;
                            foreach (var swap in swaps)
                            {
                                if (_pendingSwaps.ContainsKey(swap.hash))
                                {

                                    logger.Message($"Already known swap, ignore {swap.platform} swap: {swap.source} => {swap.destination}");
                                    continue;
                                }

                                logger.Message($"Detected {swap.platform} swap: {swap.source} => {swap.destination} hash: {swap.hash}");
                                _pendingSwaps[swap.hash] = swap;
                                MapSwap(swap.source, swap.hash);
                                MapSwap(swap.destination, swap.hash);
                            }
                            taskDict[platform] = null;
                        }
                    }
                }
            }
        }

        public Hash SettleSwap(string sourcePlatform, string destPlatform, Hash sourceHash)
        {
            logger.Message("settleSwap called " + sourceHash);
            logger.Message("dest platform " + destPlatform);
            logger.Message("src platform " + sourcePlatform);

            var settleHash = GetSettleHash(sourcePlatform, sourceHash);
            logger.Message("settleHash in settleswap: " + settleHash);

            if (settleHash != Hash.Null)
            {
                return settleHash;
            }

            if (destPlatform == PhantasmaWallet.PhantasmaPlatform)
            {
                return SettleTransaction(sourcePlatform, sourcePlatform, sourceHash);
            }

            if (sourcePlatform != PhantasmaWallet.PhantasmaPlatform)
            {
                throw new SwapException("Invalid source platform");
            }

            switch (destPlatform)
            {
                case NeoWallet.NeoPlatform:
                    return SettleSwapToNeo(sourceHash);
                case EthereumWallet.EthereumPlatform:
                    return SettleSwapToEth(sourceHash);

                default:
                    return Hash.Null;
            }
        }

        public Hash GetSettleHash(string sourcePlatform, Hash sourceHash)
        {
            var settlements = new StorageMap(SettlementTag, this.Storage);

            if (settlements.ContainsKey<Hash>(sourceHash))
            {
                return settlements.Get<Hash, Hash>(sourceHash);
            }

            var pendingList = new StorageList(PendingTag, this.Storage);
            var count = pendingList.Count();
            for (int i = 0; i < count; i++)
            {
                var settlement = pendingList.Get<PendingFee>(i);
                if (settlement.sourceHash == sourceHash)
                {
                    return settlement.destinationHash;
                }
            }

            var hash = (Hash)Nexus.RootChain.InvokeContract(Nexus.RootStorage, "interop", nameof(InteropContract.GetSettlement), sourcePlatform, sourceHash).ToObject();
            if (hash != Hash.Null && !settlements.ContainsKey<Hash>(sourceHash))
            {
                settlements.Set<Hash, Hash>(sourceHash, hash);
            }
            return hash;
        }

        private Hash SettleTransaction(string sourcePlatform, string chain, Hash txHash)
        {
            var script = new ScriptBuilder().
                AllowGas(SwapKeys.Address, Address.Null, MinimumFee, 9999).
                CallContract("interop", nameof(InteropContract.SettleTransaction), SwapKeys.Address, sourcePlatform, chain, txHash).
                SpendGas(SwapKeys.Address).
                EndScript();

            var tx = new Blockchain.Transaction(Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5), _txIdentifier);
            tx.Sign(SwapKeys);

            var bytes = tx.ToByteArray(true);

            var txData = Base16.Encode(bytes);
            var result = this.NexusAPI.SendRawTransaction(txData);
            if (result is SingleResult)
            {
                return tx.Hash;
            }

            return Hash.Null;
        }

        public IEnumerable<ChainSwap> GetPendingSwaps(Address address)
        {
            logger.Message($"Getting pending swaps for {address} now.");
            var dict = new Dictionary<Hash, ChainSwap>();

            if (_swapAddressMap.ContainsKey(address))
            {
                logger.Message($"Address  exists in swap address map");

                var swaps = _swapAddressMap[address].
                    Select(x => _pendingSwaps[x]).
                    Select(x => new ChainSwap(x.platform, x.platform, x.hash, DomainSettings.PlatformName, DomainSettings.RootChainName, Hash.Null));

                foreach (var entry in swaps)
                {
                    logger.Message($"Adding hash {entry.sourceHash} to dict.");
                    dict[entry.sourceHash] = entry;
                }

                var keys = dict.Keys.ToArray();
                foreach (var hash in keys)
                {
                    var entry = dict[hash];
                    if (entry.destinationHash == Hash.Null)
                    {
                        var settleHash = GetSettleHash(entry.sourcePlatform, hash);
                        logger.Message($"settleHash: {settleHash}.");
                        if (settleHash != Hash.Null)
                        {
                            entry.destinationHash = settleHash;
                            dict[hash] = entry;
                        }
                    }
                }

            }

            var hashes = Nexus.RootChain.GetSwapHashesForAddress(Nexus.RootChain.Storage, address);
            logger.Message($"Have {hashes.Length} for address {address}.");
            foreach (var hash in hashes)
            {
                if (dict.ContainsKey(hash))
                {
                    logger.Message($"Ignoring hash {hash}");
                    continue;
                }

                var swap = Nexus.RootChain.GetSwap(Nexus.RootChain.Storage, hash);
                if (swap.destinationHash != Hash.Null)
                {
                    logger.Message($"Ignoring swap with hash {swap.sourceHash}");
                    continue;
                }

                var settleHash = GetSettleHash(DomainSettings.PlatformName, hash);
                if (settleHash != Hash.Null)
                {
                    logger.Message($"settleHash null");
                    continue;
                }

                dict[hash] = swap;
            }

            logger.Message($"Getting pending swaps for {address} done, found {dict.Count()} swaps.");
            return dict.Values;
        }

        private Hash VerifyNeoTx(Hash sourceHash, string txHash)
        {
            var counter = 0;
            do {
                Thread.Sleep(15 * 1000); // wait 15 seconds

                if (txHash.StartsWith("0x"))
                {
                    txHash = txHash.Substring(2);
                }
                var temp = this.neoAPI.GetTransactionHeight(txHash);
                logger.Message("neo tx included in block: " + temp);

                int height;

                if (int.TryParse(temp, out height) && height > 0)
                {
                    return Hash.Parse(txHash);
                }
                else
                {
                    counter++;
                    if (counter > 5)
                    {
                        var settleMap = new StorageMap(InProgressTag, this.Storage);
                        settleMap.Remove<Hash>(sourceHash);
                        return Hash.Null;
                    }
                }

            } while (true);
        }

        private Hash VerifyEthTx(Hash sourceHash, string txHash)
        {
            TransactionReceipt txr = null;
            var settleMap = new StorageMap(InProgressTag, this.Storage);
            try
            {
                txr = ethAPI.GetTransactionReceipt(txHash);

                if (txr == null)
                {
                    logger.Error($"Ethereum transaction {txHash} not mined yet.");
                    var tx = ethAPI.GetTransaction(txHash);

                    // This check doesn't work as expected, it triggers Remove() call
                    // but tx-es are minted in the end
                    /*if (tx == null)
                    {
                        logger.Error($"Ethereum transaction {txHash} does not exist anymore.");
                        settleMap.Remove<Hash>(sourceHash);
                    }*/

                    return Hash.Null;
                }
            }
            catch (Exception e)
            {
                logger.Error($"Exception during polling for receipt: {e}");
                return Hash.Null;
            }

            if (txr.Status.Value == 0) // Status == 0 = error
            {
                // remove from settleMap because tx has failed.
                settleMap.Remove<Hash>(sourceHash);
                logger.Error($"EthAPI error, tx {txr.TransactionHash} ");
                return Hash.Null;
            }

            return Hash.Parse(txr.TransactionHash.Substring(2));
        }

        private Hash SettleSwapToEth(Hash sourceHash)
        {
            return SettleSwapToExternal(EthereumWallet.EthereumPlatform, sourceHash, (sourceHash, destination, token, amount) =>
            {
                // check if tx was sent but not minded yet
                Hash txHash = Hash.Null;
                string tx = null;
                var settleMap = new StorageMap(InProgressTag, this.Storage);
                if (settleMap.ContainsKey<Hash>(sourceHash))
                {
                    tx = settleMap.Get<Hash, string>(sourceHash);
                    if (!string.IsNullOrEmpty(tx))
                    {
                        return VerifyEthTx(sourceHash, tx);
                    }
                }
                else
                {
                    // SettleSwap called for the first time 
                    settleMap.Set<Hash, string>(sourceHash, null);
                }

                var total = UnitConversion.ToDecimal(amount, token.Decimals);

                var wif = wifs["ethereum"];
                var ethKeys = EthereumKey.FromWIF(wif);

                var destAddress = EthereumWallet.DecodeAddress(destination);

                try
                {
                    logger.Message($"ETHSWAP: Trying transfer of {total} {token.Symbol} from {ethKeys.Address} to {destAddress}");
                    tx = ethAPI.TransferAsset(token.Symbol, destAddress, total, token.Decimals);
                    settleMap.Set<Hash, string>(sourceHash, tx);
                }
                catch (Exception e)
                {
                    logger.Error($"Exception during transfer: {e}");
                    settleMap.Remove<Hash>(sourceHash);
                    return Hash.Null;
                }

                return VerifyEthTx(sourceHash, tx);
            });
        }

        private Hash SettleSwapToNeo(Hash sourceHash)
        {
            return SettleSwapToExternal(NeoWallet.NeoPlatform, sourceHash, (sourceHash, destination, token, amount) =>
            {
                Hash txHash = Hash.Null;
                string txStr = null;
                var settleMap = new StorageMap(InProgressTag, this.Storage);
                if (settleMap.ContainsKey<Hash>(sourceHash))
                {
                    txStr = settleMap.Get<Hash, string>(sourceHash);
                    if (!string.IsNullOrEmpty(txStr))
                    {
                        return VerifyNeoTx(sourceHash, txStr);
                    }
                }
                else
                {
                    // SettleSwap called for the first time 
                    settleMap.Set<Hash, string>(sourceHash, null);
                }
                var total = UnitConversion.ToDecimal(amount, token.Decimals);

                var wif = wifs["neo"];
                var neoKeys = Phantasma.Neo.Core.NeoKeys.FromWIF(wif);

                var destAddress = NeoWallet.DecodeAddress(destination);

                logger.Message($"NEOSWAP: Trying transfer of {total} {token.Symbol} from {neoKeys.Address} to {destAddress}");

                var nonce = sourceHash.ToByteArray();

                Neo.Core.Transaction tx;
                if (token.Symbol == "NEO" || token.Symbol == "GAS")
                {
                    tx = neoAPI.SendAsset(neoKeys, destAddress, token.Symbol, total);
                }
                else
                {
                    var nep5 = neoAPI.GetToken(token.Symbol);
                    tx = nep5.Transfer(neoKeys, destAddress, total, nonce);
                }

                logger.Message("broadcasted neo tx: " + tx);

                if (tx == null)
                {
                    logger.Error("NeoAPI error: " + neoAPI.LastError);
                    settleMap.Remove<Hash>(sourceHash);
                    return Hash.Null;
                }

                var strHash = tx.Hash.ToString();

                return VerifyNeoTx(sourceHash, strHash);

            });
        }

        private Hash SettleSwapToExternal(string destinationPlatform, Hash sourceHash, Func<Hash, Address, IToken, BigInteger, Hash> generator)
        {
            var swap = OracleReader.ReadTransaction(DomainSettings.PlatformName, DomainSettings.RootChainName, sourceHash);
            var transfers = swap.Transfers.Where(x => x.destinationAddress.IsInterop).ToArray();

            // TODO not support yet
            if (transfers.Length != 1)
            {
                logger.Warning($"Not implemented: Swap support for multiple transfers in a single transaction");
                return Hash.Null;
            }

            var transfer = transfers[0];

            var token = Nexus.GetTokenInfo(Nexus.RootStorage, transfer.Symbol);

            var destHash = generator(sourceHash, transfer.destinationAddress, token, transfer.Value);

            // if the asset transfer was sucessfull, we prepare a fee settlement on the mainnet
            if (destHash != Hash.Null)
            {
                var pendingList = new StorageList(PendingTag, this.Storage);
                var settle = new PendingFee() { sourceHash = sourceHash, destinationHash = destHash, settleHash = Hash.Null, time = DateTime.UtcNow, status = SwapStatus.Settle };
                pendingList.Add<PendingFee>(settle);
            
                // We have a pending fee settle now, so we don't care about the settleMap entry anymore.
                var settleMap = new StorageMap(InProgressTag, this.Storage);
                settleMap.Remove<Hash>(sourceHash);
            }

            return destHash;
        }

        private bool UpdatePendingSettle(StorageList list, int index)
        {
            var swap = list.Get<PendingFee>(index);
            var prevStatus = swap.status;
            switch (swap.status)
            {
                case SwapStatus.Settle:
                    {
                        var diff = Timestamp.Now - swap.time;
                        if (diff >= 60)
                        {
                            swap.settleHash = SettleTransaction(DomainSettings.PlatformName, DomainSettings.RootChainName, swap.sourceHash);
                            if (swap.settleHash != Hash.Null)
                            {
                                swap.status = SwapStatus.Confirm;
                            }
                        }
                        break;
                    }

                case SwapStatus.Confirm:
                    {
                        var result = this.NexusAPI.GetTransaction(swap.settleHash.ToString());
                        if (result is TransactionResult)
                        {
                            var tx = (TransactionResult)result;
                            swap.status = SwapStatus.Finished;
                        }
                        else
                        if (result is ErrorResult)
                        {
                            var error = ((ErrorResult)result).error;
                            if (error != "pending")
                            {
                                swap.settleHash = Hash.Null;
                                swap.time = Timestamp.Now;
                                swap.status = SwapStatus.Settle;
                            }
                        }
                        break;
                    }

                default: return false;
            }

            if (swap.status == SwapStatus.Finished)
            {
                var settlements = new StorageMap(SettlementTag, this.Storage);
                settlements.Set<Hash, Hash>(swap.sourceHash, swap.destinationHash);
                return true;
            }

            if (swap.status != prevStatus)
            {
                list.Replace<PendingFee>(index, swap);
            }

            return false;
        }
    }
}
