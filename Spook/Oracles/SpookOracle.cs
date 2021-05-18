using System;
using System.Linq;
using System.Collections.Generic;

using Phantasma.Blockchain;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Pay.Chains;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using Phantasma.Spook.Interop;

using NeoBlock = Phantasma.Neo.Core.Block;
using NeoTx = Phantasma.Neo.Core.Transaction;
using Logger = Phantasma.Core.Log.Logger;
using NativeBigInt = System.Numerics.BigInteger;

namespace Phantasma.Spook.Oracles
{
    public class SpookOracle : OracleReader, IOracleObserver
    {
        private readonly Spook _cli;

        private Dictionary<string, IKeyValueStoreAdapter> _keystoreCache = new Dictionary<string, IKeyValueStoreAdapter>();
        private Dictionary<string, CachedFee> _feeCache = new Dictionary<string, CachedFee>();
        private Dictionary<string, object> _keyValueStore = new Dictionary<string, object>();
        private KeyValueStore<string, string> platforms;

        private Logger logger;

        enum StorageConst
        {
            CurrentHeight,
            Block,
            Transaction,
            Platform
        }

        public SpookOracle(Spook cli, Nexus nexus, Logger logger) : base(nexus)
        {
            this._cli = cli;
            nexus.Attach(this);
            platforms = new KeyValueStore<string, string>(CreateKeyStoreAdapter(StorageConst.Platform.ToString()));
            this.logger = logger;

            logger.Message("Platform count: " + platforms.Count);

            var nexusPlatforms = (nexus as Nexus).GetPlatforms(nexus.RootStorage);
            foreach (var nexusPlatform in nexusPlatforms)
            {
                if (!platforms.ContainsKey(nexusPlatform))
                {
                    platforms.Set(nexusPlatform, nexusPlatform);
                }

                _keyValueStore.Add(nexusPlatform + StorageConst.Block, new KeyValueStore<string, InteropBlock>(CreateKeyStoreAdapter(nexusPlatform + StorageConst.Block)));
                _keyValueStore.Add(nexusPlatform + StorageConst.Transaction, new KeyValueStore<string, InteropTransaction>(CreateKeyStoreAdapter(nexusPlatform + StorageConst.Transaction)));
                _keyValueStore.Add(nexusPlatform + StorageConst.CurrentHeight, new KeyValueStore<string, string>(CreateKeyStoreAdapter(nexusPlatform + StorageConst.CurrentHeight)));
            }
        }

        public void Update(INexus nexus, StorageContext storage)
        {
            var nexusPlatforms = (nexus as Nexus).GetPlatforms(storage);
            foreach (var platform in nexusPlatforms)
            {
                if (_keyValueStore.ContainsKey(platform + StorageConst.Block) || _keyValueStore.ContainsKey(platform + StorageConst.Transaction))
                {
                    continue;
                }
                platforms.Set(platform, platform);

                _keyValueStore.Add(platform + StorageConst.Block, new KeyValueStore<string, InteropBlock>(CreateKeyStoreAdapter(platform + StorageConst.Block)));
                _keyValueStore.Add(platform + StorageConst.Transaction, new KeyValueStore<string, InteropTransaction>(CreateKeyStoreAdapter(platform + StorageConst.Transaction)));
                _keyValueStore.Add(platform + StorageConst.CurrentHeight, new KeyValueStore<string, string>(CreateKeyStoreAdapter(platform + StorageConst.CurrentHeight)));
            }
        }

        private IKeyValueStoreAdapter CreateKeyStoreAdapter(string name)
        {
            if (_keystoreCache.ContainsKey(name))
            {
                return _keystoreCache[name];
            }

            IKeyValueStoreAdapter result = Nexus.CreateKeyStoreAdapter(name);
            _keystoreCache[name] = result;

            return result;
        }

        private T Read<T>(string platform, string chainName, Hash hash, StorageConst type)
        {
            var storageKey = type + chainName + hash.ToString();
            var keyStore = _keyValueStore[platform + type] as KeyValueStore<string, T>;

            try
            {
                if(keyStore.TryGet(storageKey, out T data))
                {
                    return data;
                }
            }
            catch (Exception e)
            {
                logger.Error(e.ToString());
                return default(T);
            }
            return default(T);
        }

        public override List<InteropBlock> ReadAllBlocks(string platformName, string chainName)
        {
            var blockList = new List<InteropBlock>();
            var keyStore = _keyValueStore[platformName + StorageConst.Block] as KeyValueStore<string, InteropBlock>;

            keyStore.Visit((key, value) =>
    		{
                blockList.Add(value);
    		});

    		return blockList;
        }

        public override string GetCurrentHeight(string platformName, string chainName)
        {
            var storageKey = StorageConst.CurrentHeight + platformName + chainName;
            var keyStore = _keyValueStore[platformName + StorageConst.CurrentHeight] as KeyValueStore<string, string>;
            if (keyStore.TryGet(storageKey, out string height))
            {
                return height; 
            }

            return "";
        }

        public override void SetCurrentHeight(string platformName, string chainName, string height)
        {
            var storageKey = StorageConst.CurrentHeight + platformName + chainName;
            var keyStore = _keyValueStore[platformName + StorageConst.CurrentHeight] as KeyValueStore<string, string>;

            keyStore.Set(storageKey, height);
        }

        private bool Persist<T>(string platform, string chainName, Hash hash, StorageConst type, T data)
        {
            var storageKey = type + chainName + hash.ToString();
            var keyStore = _keyValueStore[platform + type] as KeyValueStore<string, T>;

            if(!keyStore.ContainsKey(storageKey))
            {
                keyStore.Set(storageKey, data);
                return true;
            }

            keyStore.Set(storageKey, data);
            logger.Error("storageKey " + storageKey + " updated!");
            return false;
        }

        protected override Phantasma.Numerics.BigInteger PullFee(Timestamp time, string platform)
        {
            platform = platform.ToLower();

            switch (platform)
            {
                case NeoWallet.NeoPlatform:
                    return Phantasma.Numerics.UnitConversion.ToBigInteger(0.1m, DomainSettings.FiatTokenDecimals);

                case EthereumWallet.EthereumPlatform:

                    CachedFee fee;
                    if (_feeCache.TryGetValue(platform, out fee))
                    {
                        if ((Timestamp.Now - fee.Time) < 60)
                        {
                            var logMessage = $"PullFee({platform}): Cached fee pulled: {fee.Value}, GAS limit: {_cli.Settings.Oracle.EthGasLimit}, calculated fee: {fee.Value * _cli.Settings.Oracle.EthGasLimit}";
                            logger.Debug(logMessage);

                            return fee.Value * _cli.Settings.Oracle.EthGasLimit;
                        }
                    }

                    var newFee = EthereumInterop.GetNormalizedFee(_cli.Settings.Oracle.EthFeeURLs.ToArray());
                    fee = new CachedFee(Timestamp.Now, UnitConversion.ToBigInteger(newFee, 9)); // 9 for GWEI
                    _feeCache[platform] = fee;

                    var logMessage2 = $"PullFee({platform}): New fee pulled: {fee.Value}, GAS limit: {_cli.Settings.Oracle.EthGasLimit}, calculated fee: {fee.Value * _cli.Settings.Oracle.EthGasLimit}";
                    logger.Debug(logMessage2);

                    return fee.Value * _cli.Settings.Oracle.EthGasLimit;

                default:
                    throw new OracleException($"Support for {platform} fee not implemented in this node");
            }
        }

        protected override decimal PullPrice(Timestamp time, string symbol)
        {
            var apiKey = _cli.CryptoCompareAPIKey;
            if (!string.IsNullOrEmpty(apiKey))
            {
                if (symbol == DomainSettings.FuelTokenSymbol)
                {
                    var result = PullPrice(time, DomainSettings.StakingTokenSymbol);
                    return result / 5;
                }

                var price = CryptoCompareUtils.GetCoinRate(symbol, DomainSettings.FiatTokenSymbol, apiKey);
                return price;
            }

            throw new OracleException("No support for oracle prices in this node");
        }

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, NativeBigInt height = new NativeBigInt())
        {
            if (hash == null && height == null)
            {
                throw new OracleException($"Fetching block not possible without hash or height");
            }

            InteropBlock block = Read<InteropBlock>(platformName, chainName, hash, StorageConst.Block);

            if (height == null && block.Hash != null && block.Hash != Hash.Null)
            {
                return block;
            }

            Tuple<InteropBlock, InteropTransaction[]> interopTuple;
            switch (platformName)
            {
                case NeoWallet.NeoPlatform:

                    NeoBlock neoBlock;

                    if (height == 0)
                    {
                        neoBlock = _cli.NeoAPI.GetBlock(new UInt256(LuxUtils.ReverseHex(hash.ToString()).HexToBytes()));
                    }
                    else
                    {
                        neoBlock = _cli.NeoAPI.GetBlock(height);
                    }

                    if (neoBlock == null)
                    {
                        throw new OracleException($"Neo block is null");
                    }

                    var coldStorage = _cli.Settings.Oracle.SwapColdStorageNeo;
                    interopTuple = NeoInterop.MakeInteropBlock(logger, neoBlock, _cli.NeoAPI,
                            _cli.TokenSwapper.SwapAddresses[SwapPlatformChain.Neo], coldStorage);
                    break;

                case EthereumWallet.EthereumPlatform:

                    {
                        //BlockWithTransactions ethBlock;
                        //if (height == 0)
                        //{
                        //    //TODO MakeInteropBlock for a full block not done yet
                        //    //ethBlock = _cli.EthAPI.GetBlock(hash.ToString());
                        //    //interopTuple = EthereumInterop.MakeInteropBlock(logger, ethBlock, _cli.EthAPI, _cli.TokenSwapper.swapAddress);
                        //}
                        //else
                        //{
                        //}

                        var hashes = _cli.Nexus.GetPlatformTokenHashes(EthereumWallet.EthereumPlatform, _cli.Nexus.RootStorage)
                            .Select(x => x.ToString().Substring(0, 40)).ToArray();

                        interopTuple = EthereumInterop.MakeInteropBlock(_cli.Nexus, logger, _cli.EthAPI, height,
                                hashes, _cli.Settings.Oracle.EthConfirmations, _cli.TokenSwapper.SwapAddresses[SwapPlatformChain.Ethereum]);
                        break;
                    }

                case BSCWallet.BSCPlatform:
                    {
                        var hashes = _cli.Nexus.GetPlatformTokenHashes(EthereumWallet.EthereumPlatform, _cli.Nexus.RootStorage)
                            .Select(x => x.ToString().Substring(0, 40)).ToArray();

                        interopTuple = EthereumInterop.MakeInteropBlock(_cli.Nexus, logger, _cli.EthAPI, height,
                                hashes, _cli.Settings.Oracle.EthConfirmations, _cli.TokenSwapper.SwapAddresses[SwapPlatformChain.BSC]);
                        break;
                    }


                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            if (interopTuple.Item1.Hash != Hash.Null)
            {

                var initialStore = Persist<InteropBlock>(platformName, chainName, interopTuple.Item1.Hash, StorageConst.Block,
                        interopTuple.Item1);
                var transactions = interopTuple.Item2;

                if (!initialStore)
                {
                    logger.Debug($"Oracle block { interopTuple.Item1.Hash } on platform { platformName } updated!");
                }

                foreach (var tx in transactions)
                {
                    var txInitialStore = Persist<InteropTransaction>(platformName, chainName, tx.Hash, StorageConst.Transaction, tx);
                    if (!txInitialStore)
                    {
                        logger.Debug($"Oracle block { interopTuple.Item1.Hash } on platform { platformName } updated!");
                    }
                }

            }

            return interopTuple.Item1;
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            logger.Debug($"{platformName} pull tx: {hash}");
            InteropTransaction tx = Read<InteropTransaction>(platformName, chainName, hash, StorageConst.Transaction);
            if (tx != null && tx.Hash != null)
            {
                logger.Debug($"Found tx {hash} in oracle storage");
                return tx;
            }

            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    NeoTx neoTx;
                    UInt256 uHash = new UInt256(LuxUtils.ReverseHex(hash.ToString()).HexToBytes());
                    neoTx = _cli.NeoAPI.GetTransaction(uHash);
                    var coldStorage = _cli.Settings.Oracle.SwapColdStorageNeo;
                    tx = NeoInterop.MakeInteropTx(logger, neoTx, _cli.NeoAPI, _cli.TokenSwapper.SwapAddresses[SwapPlatformChain.Neo], coldStorage);
                    break;

                case EthereumWallet.EthereumPlatform:
                    {
                        var txRcpt = _cli.EthAPI.GetTransactionReceipt(hash.ToString());
                        tx = EthereumInterop.MakeInteropTx(_cli.Nexus, logger, txRcpt, _cli.EthAPI, _cli.TokenSwapper.SwapAddresses[SwapPlatformChain.Ethereum]);
                        break;
                    }

                case BSCWallet.BSCPlatform:
                    {
                        var txRcpt = _cli.BSCAPI.GetTransactionReceipt(hash.ToString());
                        tx = EthereumInterop.MakeInteropTx(_cli.Nexus, logger, txRcpt, _cli.BSCAPI, _cli.TokenSwapper.SwapAddresses[SwapPlatformChain.BSC]);
                        break;
                    }

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            if (!Persist<InteropTransaction>(platformName, chainName, tx.Hash, StorageConst.Transaction, tx))
            {
                logger.Error($"Oracle transaction { hash } on platform { platformName } updated!");
            }

            return tx;
        }

        protected override T PullData<T>(Timestamp time, string url)
        {
            throw new OracleException("unknown oracle url");
        }

        protected override InteropNFT PullPlatformNFT(string platformName, string symbol, BigInteger tokenID)
        {
            // TODO NFT support
            throw new NotImplementedException();
        }
    }

    struct CachedFee
    {
        public Timestamp Time;
        public BigInteger Value;

        public CachedFee(Timestamp time, BigInteger value)
        {
            this.Time = time;
            this.Value = value;
        }
    }
}
