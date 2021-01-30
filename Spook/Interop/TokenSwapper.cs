using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
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

        private readonly Dictionary<string, BigInteger> interopBlocks;
        private PlatformInfo[] platforms;
        private Dictionary<string, string> wifs = new Dictionary<string, string>(); 
        private Dictionary<string, ChainWatcher> _finders = new Dictionary<string, ChainWatcher>();

        private HashSet<string> _supportedPlatforms = new HashSet<string>();

        public TokenSwapper(Spook node, PhantasmaKeys swapKey, NexusAPI nexusAPI, NeoAPI neoAPI, EthAPI ethAPI, BigInteger minFee, string[] supportedPlatforms, Logger logger)
        {
            this.logger = logger;
            this._settings = node.Settings;
            this.SwapKeys = swapKey;
            this.NexusAPI = nexusAPI;
            this.OracleReader = Nexus.GetOracleReader();
            this.MinimumFee = minFee;
            this.neoAPI = neoAPI;
            this.ethAPI = ethAPI;

            this.Storage = new KeyStoreStorage(Nexus.CreateKeyStoreAdapter("swaps"));

            this.interopBlocks = new Dictionary<string, BigInteger>();

            interopBlocks[DomainSettings.PlatformName] = BigInteger.Parse(_settings.Oracle.PhantasmaInteropHeight);
            interopBlocks["neo"] = BigInteger.Parse(_settings.Oracle.NeoInteropHeight);
            interopBlocks["ethereum"] = BigInteger.Parse(_settings.Oracle.EthInteropHeight);

            _supportedPlatforms.Add(DomainSettings.PlatformName);
            foreach (var entry in supportedPlatforms)
            {
                if (_supportedPlatforms.Contains(entry))
                {
                    throw new SwapException($"Duplicated swap platform {entry}, check config");
                }

                if (!interopBlocks.ContainsKey(entry))
                {
                    throw new SwapException($"Unknown swap platform {entry}, check config");
                }

                _supportedPlatforms.Add(entry);
            }

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
        private const string UsedRpcTag = ".usedrpc";

        // This lock added to protect from reading wrong state (settled, pending, inprogress)
        // while it's being modified by concurrent thread.
        private static object StateModificationLock = new object();

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
            else
            {
                this.logger.Error($"TokenSwapper: Resync block {blockId} on platform {platform} failed.");
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

                lock (StateModificationLock)
                {
                    var pendingList = new StorageList(PendingTag, this.Storage);

                    int i = 0;
                    var count = pendingList.Count();

                    while (i < count)
                    {
                        var settlement = pendingList.Get<PendingFee>(i);
                        if (UpdatePendingSettle(pendingList, i))
                        {
                            pendingList.RemoveAt(i);
                            count--;
                        }
                        else
                        {
                            i++;
                        }
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

        private void ProcessCompletedTasks()
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
            logger.Debug("settleSwap called " + sourceHash);
            logger.Debug("dest platform " + destPlatform);
            logger.Debug("src platform " + sourcePlatform);

            // This code is preventing us from doing double swaps.
            // We must ensure that states (settled, pending, inprogress) are locked
            // during this check and can't be changed by concurrent thread,
            // making this check inconsistent.
            lock (StateModificationLock)
            {
                // First thing, check if the sourceHash is already known, if so, return
                var inProgressMap = new StorageMap(InProgressTag, this.Storage);
                if (inProgressMap.ContainsKey(sourceHash))
                {
                    logger.Debug("Hash already known, swap currently in progress: " + sourceHash);
                    return Hash.Null;
                }
                else
                {
                    var settleHash = GetSettleHash(sourcePlatform, sourceHash);
                    logger.Debug("settleHash in settleswap: " + settleHash);

                    if (settleHash != Hash.Null)
                    {
                        return settleHash;
                    }
                    else
                    {
                        // sourceHash not known, create an entry to store it, from here on,
                        // every call to SettleSwap will return Hash.Null until the swap is finished.
                        logger.Debug("Unknown hash, create in progress entry: " + sourceHash);
                        inProgressMap.Set<Hash, string>(sourceHash, null);
                    }
                }
            }

            if (destPlatform == PhantasmaWallet.PhantasmaPlatform)
            {
                return SettleTransaction(sourcePlatform, sourcePlatform, sourceHash);
            }

            if (sourcePlatform != PhantasmaWallet.PhantasmaPlatform)
            {
                throw new SwapException("Invalid source platform");
            }

            return SettleSwapToExternal(sourceHash, destPlatform);
        }


        // should only be called from inside lock block
        private Hash GetSettleHash(string sourcePlatform, Hash sourceHash)
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
                // This modification should be locked when GetSettleHash() is called from SettleSwap(),
                // so we lock it in SettleSwap().
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

            var tx = new Blockchain.Transaction(Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5), Spook.TxIdentifier);
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
            logger.Debug($"Getting pending swaps for {address} now.");
            var dict = new Dictionary<Hash, ChainSwap>();

            if (_swapAddressMap.ContainsKey(address))
            {
                logger.Debug($"Address exists in swap address map");

                var swaps = _swapAddressMap[address].
                    Select(x => _pendingSwaps[x]).
                    Select(x => new ChainSwap(x.platform, x.platform, x.hash, DomainSettings.PlatformName, DomainSettings.RootChainName, Hash.Null));

                foreach (var entry in swaps)
                {
                    logger.Debug($"Adding hash {entry.sourceHash} to dict.");
                    dict[entry.sourceHash] = entry;
                }

                var keys = dict.Keys.ToArray();
                foreach (var hash in keys)
                {
                    var entry = dict[hash];
                    if (entry.destinationHash == Hash.Null)
                    {
                        lock (StateModificationLock)
                        {
                            var settleHash = GetSettleHash(entry.sourcePlatform, hash);
                            logger.Debug($"settleHash: {settleHash}.");
                            if (settleHash != Hash.Null)
                            {
                                entry.destinationHash = settleHash;
                                dict[hash] = entry;
                            }
                        }
                    }
                }

            }

            var hashes = Nexus.RootChain.GetSwapHashesForAddress(Nexus.RootChain.Storage, address);
            logger.Debug($"Have {hashes.Length} for address {address}.");
            foreach (var hash in hashes)
            {
                if (dict.ContainsKey(hash))
                {
                    logger.Debug($"Ignoring hash {hash}");
                    continue;
                }

                var swap = Nexus.RootChain.GetSwap(Nexus.RootChain.Storage, hash);
                if (swap.destinationHash != Hash.Null)
                {
                    logger.Debug($"Ignoring swap with hash {swap.sourceHash}");
                    continue;
                }

                lock (StateModificationLock)
                {
                    var settleHash = GetSettleHash(DomainSettings.PlatformName, hash);
                    if (settleHash != Hash.Null)
                    {
                        logger.Debug($"settleHash null");
                        continue;
                    }
                }

                dict[hash] = swap;
            }

            logger.Debug($"Getting pending swaps for {address} done, found {dict.Count()} swaps.");
            return dict.Values;
        }

        private bool TxInBlockNeo(string txHash)
        {
            string strHeight;
            try
            {
                strHeight = this.neoAPI.GetTransactionHeight(txHash);
                logger.Debug("neo tx included in block: " + strHeight);
            }
            catch (Exception e)
            {
                logger.Error("Error during neo api call: " + e);
                return false;
            }

            int height;

            if (int.TryParse(strHeight, out height) && height > 0)
            {
                return true;
            }

            return false;
        }

        private Hash VerifyNeoTx(Hash sourceHash, string txHash)
        {
            var counter = 0;
            do {

                Thread.Sleep(15000); // wait 15 seconds

                if (txHash.StartsWith("0x"))
                {
                    txHash = txHash.Substring(2);
                }

                if (TxInBlockNeo(txHash))
                {
                    return Hash.Parse(txHash);
                }
                else
                {
                    counter++;
                    if (counter > 5)
                    {
                        string node = null;

                        var rpcMap = new StorageMap(UsedRpcTag, this.Storage);
                        node = rpcMap.Get<Hash, string>(sourceHash);

                        // tx could still be in mempool
                        bool inMempool = true;
                        try
                        {
                            inMempool = this.neoAPI.CheckMempool(node, txHash);
                        }
                        catch (Exception e)
                        {
                            // If we can't check mempool, we are unable to verify if the tx has gone through or not,
                            // therefore we have to wait until we are able to check this nodes mempool again, or find 
                            // the tx in a block in the next round.
                            logger.Error("Exception during mempool check: " + e);
                            return Hash.Null;
                        }

                        if (inMempool)
                        {
                            // tx still in mempool, do nothing
                            return Hash.Null;
                        }
                        else
                        {
                            // to make sure it wasn't moved out from mempool and is already processed check again if the tx is already added to a block
                            if (TxInBlockNeo(txHash))
                            {
                                return Hash.Parse(txHash);
                            }
                            else
                            {
                                // tx is neither in a block, nor in mempool, either dropped out of mempool or mempool was full already
                                logger.Error($"Possible failed neo swap sourceHash: {sourceHash} txHash: {txHash}");
                            }
                        }
                        return Hash.Null;
                    }
                }

            } while (true);
        }

        private Hash VerifyEthTx(Hash sourceHash, string txHash)
        {
            TransactionReceipt txr;

            try
            {
                var retries = 0;
                do
                {
                    // Checking if tx is mined.
                    txr = ethAPI.GetTransactionReceipt(txHash);
                    if (txr == null)
                    {
                        if (retries == 12) // Waiting for 1 minute max.
                            break;

                        Thread.Sleep(5000);
                        retries++;
                    }
                } while (txr == null);

                if (txr == null)
                {
                    logger.Error($"Ethereum transaction {txHash} not mined yet.");
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
                logger.Error($"Possible failed eth swap sourceHash: {sourceHash} txHash: {txHash}");
                
                logger.Error($"EthAPI error, tx {txr.TransactionHash} ");
                return Hash.Null;
            }

            return Hash.Parse(txr.TransactionHash.Substring(2));
        }

        // NOTE no locks happen here because this callback is called from within a lock
        private Hash SettleSwapToEth(Hash sourceHash, Address destination, IToken token, BigInteger amount)
        {
            // check if tx was sent but not minded yet
            string tx = null;
                
            var inProgressMap = new StorageMap(InProgressTag, this.Storage);

            if (inProgressMap.ContainsKey<Hash>(sourceHash))
            {
                tx = inProgressMap.Get<Hash, string>(sourceHash);

                if (!string.IsNullOrEmpty(tx))
                {
                    return VerifyEthTx(sourceHash, tx);
                }
            }

            var total = UnitConversion.ToDecimal(amount, token.Decimals);

            var wif = wifs["ethereum"];
            var ethKeys = EthereumKey.FromWIF(wif);

            var destAddress = EthereumWallet.DecodeAddress(destination);

            try
            {
                logger.Debug($"ETHSWAP: Trying transfer of {total} {token.Symbol} from {ethKeys.Address} to {destAddress}");
                tx = ethAPI.TransferAsset(token.Symbol, destAddress, total, token.Decimals);

                // persist resulting tx hash as in progress
                inProgressMap.Set<Hash, string>(sourceHash, tx);
                logger.Debug("broadcasted eth tx: " + tx);
            }
            catch (Exception e)
            {
                logger.Error($"Exception during transfer: {e}");
                // we don't know if the transfer happend or not, therefore can't delete from inProgressMap yet.
                return Hash.Null;
            }

            return VerifyEthTx(sourceHash, tx);
        }

        // NOTE no locks happen here because this callback is called from within a lock
        private Hash SettleSwapToNeo(Hash sourceHash, Address destination, IToken token, BigInteger amount)
        {
            Hash txHash = Hash.Null;
            string txStr = null;
                
            var inProgressMap = new StorageMap(InProgressTag, this.Storage);
            var rpcMap = new StorageMap(UsedRpcTag, this.Storage);

            if (inProgressMap.ContainsKey<Hash>(sourceHash))
            {
                txStr = inProgressMap.Get<Hash, string>(sourceHash);

                if (!string.IsNullOrEmpty(txStr))
                {
                    return VerifyNeoTx(sourceHash, txStr);
                }
            }

            var total = UnitConversion.ToDecimal(amount, token.Decimals);

            var wif = wifs["neo"];
            var neoKeys = NeoKeys.FromWIF(wif);

            var destAddress = NeoWallet.DecodeAddress(destination);

            logger.Debug($"NEOSWAP: Trying transfer of {total} {token.Symbol} from {neoKeys.Address} to {destAddress}");

            var nonce = sourceHash.ToByteArray();

            Neo.Core.Transaction tx = null;
            string usedRpc = null;
            try
            {
                if (token.Symbol == "NEO" || token.Symbol == "GAS")
                {
                    tx = neoAPI.SendAsset(neoKeys, destAddress, token.Symbol, total, out usedRpc);
                }
                else
                {
                    var nep5 = neoAPI.GetToken(token.Symbol);
                    tx = nep5.Transfer(neoKeys, destAddress, total, nonce, x => usedRpc = x);
                }

                // persist resulting tx hash as in progress
                inProgressMap.Set<Hash, string>(sourceHash, tx.Hash.ToString());
                rpcMap.Set<Hash, string>(sourceHash, usedRpc);

                logger.Debug("broadcasted neo tx: " + tx);
            }
            catch (Exception e)
            {
                logger.Error("Error during transfering {token.Symbol}: " + e);
                return Hash.Null;
            }

            if (tx == null)
            {
                logger.Error($"NeoAPI error {neoAPI.LastError} or possible failed neo swap sourceHash: {sourceHash} no transfer happend.");
                return Hash.Null;
            }

            var strHash = tx.Hash.ToString();

            return VerifyNeoTx(sourceHash, strHash);
        }


        private Hash SettleSwapToExternal(Hash sourceHash, string destPlatform)
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

            lock (StateModificationLock)
            {
                var destHash = GetSettleHash(DomainSettings.PlatformName, sourceHash);
                logger.Debug("settleHash in settleswap: " + destHash);

                if (destHash != Hash.Null)
                {
                    return destHash;
                }

                switch (destPlatform)
                {
                    case NeoWallet.NeoPlatform:
                        destHash = SettleSwapToNeo(sourceHash, transfer.destinationAddress, token, transfer.Value);
                        break;

                    case EthereumWallet.EthereumPlatform:
                        destHash = SettleSwapToEth(sourceHash, transfer.destinationAddress, token, transfer.Value);
                        break;

                    default:
                        return Hash.Null;
                }

                // if the asset transfer was sucessfull, we prepare a fee settlement on the mainnet
                if (destHash != Hash.Null)
                {
                    var pendingList = new StorageList(PendingTag, this.Storage);
                    var settle = new PendingFee() { sourceHash = sourceHash, destinationHash = destHash, settleHash = Hash.Null, time = DateTime.UtcNow, status = SwapStatus.Settle };
                    pendingList.Add<PendingFee>(settle);
                }

                return destHash;
            }
        }

        // NOTE no locks here because we call this from within a lock already
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

                // swap is finished, it's safe to remove it from inProgressMap
                var inProgressMap = new StorageMap(InProgressTag, this.Storage);
                inProgressMap.Remove<Hash>(swap.sourceHash);
                return true;
            }

            if (swap.status != prevStatus)
            {
                list.Replace<PendingFee>(index, swap);
            }

            return false;
        }

        public bool SupportsSwap(string sourcePlatform, string destPlatform)
        {
            return (sourcePlatform != destPlatform) && _supportedPlatforms.Contains(sourcePlatform) && _supportedPlatforms.Contains(destPlatform) && (sourcePlatform == DomainSettings.PlatformName || destPlatform == DomainSettings.PlatformName);
        }
    }
}
