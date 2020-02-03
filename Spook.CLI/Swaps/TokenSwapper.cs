using System.Collections.Generic;
using System.Linq;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Spook.Oracles;
using Phantasma.Core.Log;
using Phantasma.Core.Utils;
using Phantasma.Neo.Core;
using Phantasma.Blockchain;
using Phantasma.Domain;
using Phantasma.Pay.Chains;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Contracts.Native;
using Phantasma.Core.Types;
using System;
using Phantasma.API;
using Phantasma.Storage.Context;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System.IO;

namespace Phantasma.Spook.Swaps
{
    public enum SwapStatus
    {
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

    public struct PendingSettle : ISerializable
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

        private readonly string wif;

        protected ChainWatcher(TokenSwapper swapper, string wif, string platformName)
        {
            Swapper = swapper;
            this.PlatformName = platformName;
            this.LocalAddress = swapper.FindAddress(platformName);

            if (string.IsNullOrEmpty(LocalAddress))
            {
                throw new SwapException($"Invalid address for {platformName} swaps");
            }

            var localKeys = GetAvailableAddress(wif);
            if (localKeys == LocalAddress)
            {
                Swapper.logger.Message($"Listening for {platformName} swaps at address {LocalAddress}");
            }
            else
            {
                Swapper.logger.Error($"Expected {platformName} keys to {LocalAddress}, instead got keys to {localKeys}");
            }
        }

        protected abstract string GetAvailableAddress(string wif);
        public abstract IEnumerable<PendingSwap> Update();
    }

    public class TokenSwapper : ITokenSwapper
    {
        public readonly NexusAPI NexusAPI;
        public Nexus Nexus => NexusAPI.Nexus;
        public readonly Logger logger;
        private StorageContext Storage;
        internal readonly PhantasmaKeys SwapKeys;
        private readonly BigInteger MinimumFee;
        private readonly NeoAPI neoAPI;
        private readonly NeoScanAPI neoscanAPI;

        private readonly Dictionary<string, BigInteger> interopBlocks;
        private PlatformInfo[] platforms;

        private Dictionary<string, string> wifs = new Dictionary<string, string>(); 

        private Dictionary<string, ChainWatcher> _finders = new Dictionary<string, ChainWatcher>();

        public TokenSwapper(PhantasmaKeys swapKey, NexusAPI nexusAPI, NeoScanAPI neoscanAPI, NeoAPI neoAPI, BigInteger minFee, Logger logger, Arguments arguments)
        {
            this.SwapKeys = swapKey;
            this.NexusAPI = nexusAPI;
            this.MinimumFee = minFee;

            this.neoAPI = neoAPI;
            this.neoscanAPI = neoscanAPI;
            this.logger = logger;

            this.Storage = new KeyStoreStorage(Nexus.CreateKeyStoreAdapter("swaps"));

            this.interopBlocks = new Dictionary<string, BigInteger>();

            interopBlocks["phantasma"] = BigInteger.Parse(arguments.GetString("interop.phantasma.height", "0"));
            interopBlocks["neo"] = BigInteger.Parse(arguments.GetString("interop.neo.height", "4261049"));
            //interopBlocks["ethereum"] = BigInteger.Parse(arguments.GetString("interop.ethereum.height", "4261049"));

            InitWIF("neo", arguments);

            /*
            foreach (var entry in interopBlocks)
            {
                BigInteger blockHeight = entry.Value;

                ChainInterop interop;

                switch (entry.Key)
                {
                    case "phantasma":
                        interop = new PhantasmaInterop(this, swapKey, blockHeight, nexusAPI);
                        break;

                    case "neo":
                        interop = new NeoInterop(this, swapKey, blockHeight, neoAPI, neoscanAPI);
                        break;

                    case "ethereum":
                        interop = new EthereumInterop(this, swapKey, blockHeight);
                        break;

                    default:
                        interop = null;
                        break;
                }

                if (interop != null)
                {
                    bool shouldAdd = true;

                    if (!(interop is PhantasmaInterop))
                    {
                        logger.Message($"{interop.Name}.Swap.Private: {interop.PrivateKey}");
                        logger.Message($"{interop.Name}.Swap.{interop.Name}: {interop.LocalAddress}");
                        logger.Message($"{interop.Name}.Swap.Phantasma: {interop.ExternalAddress}");

                        for (int i = 0; i < platforms.Length; i++)
                        {
                            var temp = platforms[i];
                            if (temp.platform == interop.Name)
                            {
                                if (temp.address != interop.LocalAddress)
                                {
                                    logger.Error($"{interop.Name} address mismatch, should be {temp.address}. Make sure you are using the proper swap seed.");
                                    shouldAdd = false;
                                }
                            }
                        }
                    }

                    if (shouldAdd)
                    {
                        AddInterop(interop);
                    }
                }
            }*/
        }

        private void InitWIF(string platformName, Arguments arguments)
        {
            var genesisHash = Nexus.GetGenesisHash(Nexus.RootStorage);
            var interopKeys = InteropUtils.GenerateInteropKeys(SwapKeys, genesisHash, platformName);
            var defaultWif = interopKeys.ToWIF();

            var wif = arguments.GetString($"{platformName}.wif", defaultWif);

            wifs[platformName] = wif;
        }

        internal IToken FindTokenByHash(string asset)
        {
            var hash = Hash.FromUnpaddedHex(asset);
            var symbols = Nexus.GetTokens(Nexus.RootStorage);
            return symbols.Select(x => Nexus.GetTokenInfo(Nexus.RootStorage, x)).Where(x => x.Hash == hash).FirstOrDefault();
        }

        internal string FindAddress(string platformName)
        {
            return platforms.Where(x => x.Name == platformName).Select(x => x.InteropAddresses[0].ExternalAddress).FirstOrDefault();
        }

        private const string SettlementTag = ".settled";
        private const string PendingTag = ".pending";

        private Dictionary<Hash, PendingSwap> _pendingSwaps = new Dictionary<Hash, PendingSwap>();
        private Dictionary<Address, List<Hash>> _swapAddressMap = new Dictionary<Address, List<Hash>>();
       // private Dictionary<Hash, Hash> _settlements = new Dictionary<Hash, Hash>();    
        //private List<PendingSettle> _pendingSettles = new List<PendingSettle>();

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

        public void Update()
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

                _finders["neo"] = new NeoInterop(this, wifs["neo"], interopBlocks["neo"], neoscanAPI, logger);
            }

            if (this.platforms.Length == 0)
            {
                return;
            }

            var pendingList = new StorageList(PendingTag, this.Storage);
            int i = 0;
            var count = pendingList.Count();
            while (i < count)
            {
                var settlement = pendingList.Get<PendingSettle>(i);
                if (UpdatePendingSettle(pendingList, i))
                {
                    pendingList.RemoveAt<PendingSettle>(i);
                    count--;
                }
                else
                {
                    i++;
                }
            }

            foreach (var finder in _finders.Values)
            {
                var swaps = finder.Update();

                foreach (var swap in swaps)
                {
                    if (_pendingSwaps.ContainsKey(swap.hash))
                    {
                        continue;
                    }

                    logger.Message($"Detected {finder.PlatformName} swap: {swap.source} => {swap.destination}");
                    _pendingSwaps[swap.hash] = swap;
                    MapSwap(swap.source, swap.hash);
                    MapSwap(swap.destination, swap.hash);
                }
            }
        }

        public Hash SettleSwap(string sourcePlatform, string destPlatform, Hash sourceHash)
        {
            var settleHash = GetSettleHash(sourcePlatform, sourceHash);
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
                var settlement = pendingList.Get<PendingSettle>(i);
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

            var tx = new Blockchain.Transaction(Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5), CLI.Identifier);
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
            var dict = new Dictionary<Hash, ChainSwap>();

            if (_swapAddressMap.ContainsKey(address))
            {
                var swaps = _swapAddressMap[address].
                    Select(x => _pendingSwaps[x]).
                    Select(x => new ChainSwap(x.platform, x.platform, x.hash, DomainSettings.PlatformName, DomainSettings.RootChainName, Hash.Null));

                foreach (var entry in swaps)
                {
                    dict[entry.sourceHash] = entry;
                }

                var keys = dict.Keys.ToArray();
                foreach (var hash in keys)
                {
                    var entry = dict[hash];
                    if (entry.destinationHash == Hash.Null)
                    {
                        var settleHash = GetSettleHash(entry.sourcePlatform, hash);
                        if (settleHash != Hash.Null)
                        {
                            entry.destinationHash = settleHash;
                            dict[hash] = entry;
                        }
                    }
                }

            }

            var hashes = Nexus.RootChain.GetSwapHashesForAddress(Nexus.RootChain.Storage, address);
            foreach (var hash in hashes)
            {
                if (dict.ContainsKey(hash))
                {
                    continue;
                }

                var swap = Nexus.RootChain.GetSwap(Nexus.RootChain.Storage, hash);
                if (swap.destinationHash != Hash.Null)
                {
                    continue;
                }

                var settleHash = GetSettleHash(DomainSettings.PlatformName, hash);
                if (settleHash != Hash.Null)
                {
                    continue;
                }

                dict[hash] = swap;
            }

            return dict.Values;
        }

        private Hash SettleSwapToNeo(Hash sourceHash)
        {
            return SettleSwapToExternal(NeoWallet.NeoPlatform, sourceHash, (destination, token, amount) =>
            {
                var total = UnitConversion.ToDecimal(amount, token.Decimals);

                var wif = wifs["neo"];
                var neoKeys = Phantasma.Neo.Core.NeoKeys.FromWIF(wif);

                var destAddress = NeoWallet.DecodeAddress(destination);

                Neo.Core.Transaction tx;
                if (token.Symbol == "NEO" || token.Symbol == "GAS")
                {
                    tx = neoAPI.SendAsset(neoKeys, destAddress, token.Symbol, total);
                }
                else
                {
                    var nep5 = neoAPI.GetToken(token.Symbol);
                    tx = nep5.Transfer(neoKeys, destAddress, total);
                }

                if (tx == null)
                {
                    logger.Error("NeoAPI error: " + neoAPI.LastError);
                    return Hash.Null;
                }

                var txHash = Hash.Parse(tx.Hash.ToString());
                return txHash;
            });
        }

        private Hash SettleSwapToExternal(string destinationPlatform, Hash sourceHash, Func<Address, IToken, BigInteger, Hash> generator)
        {
            var oracleReader = Nexus.CreateOracleReader();
            var swap = oracleReader.ReadTransaction(DomainSettings.PlatformName, DomainSettings.RootChainName, sourceHash);

            var transfers = swap.Transfers.Where(x => x.destinationAddress.IsInterop).ToArray();

            // TODO not support yet
            if (transfers.Length != 1)
            {
                logger.Warning($"Not implemented: Swap support for multiple transfers in a single transaction");
                return Hash.Null;
            }

            var transfer = transfers[0];

            var token = Nexus.GetTokenInfo(Nexus.RootStorage, transfer.Symbol);

            var destHash = generator(transfer.destinationAddress, token, transfer.Value);
            if (destHash != Hash.Null)
            {
                var pendingList = new StorageList(PendingTag, this.Storage);
                var settle = new PendingSettle() { sourceHash = sourceHash, destinationHash = destHash, settleHash = Hash.Null, time = DateTime.UtcNow, status = SwapStatus.Settle };
                pendingList.Add<PendingSettle>(settle);
            }

            return destHash;
        }

        private bool UpdatePendingSettle(StorageList list, int index)
        {
            var swap = list.Get<PendingSettle>(index);
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
                list.Replace<PendingSettle>(index, swap);
            }

            return false;
        }
    }
}
