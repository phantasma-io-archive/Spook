using System;
using NativeBigInt = System.Numerics.BigInteger;
using System.Collections.Generic;
using Phantasma.Blockchain;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Pay.Chains;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using Phantasma.Spook.Interop;
using Logger = Phantasma.Core.Log.Logger;
using NeoBlock = Phantasma.Neo.Core.Block;
using NeoTx = Phantasma.Neo.Core.Transaction;

namespace Phantasma.Spook.Oracles
{
    public class SpookOracle : OracleReader, IOracleObserver
    {
        public readonly CLI CLI;

        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;

        private Dictionary<string, IKeyValueStoreAdapter> _keystoreCache =
           new Dictionary<string, IKeyValueStoreAdapter>();

        private Dictionary<string, object> _keyValueStore =
           new Dictionary<string, object>();

        private KeyValueStore<string, string> platforms;

        private Logger logger;

        private bool storageInitialized;

        enum StorageConst
        {
            CurrentHeight,
            Block,
            Transaction,
            Platform
        }

        public SpookOracle(CLI cli, Nexus nexus, Logger logger, Func<string, IKeyValueStoreAdapter> adapterFactory = null) : base(nexus)
        {
            this.CLI = cli;
            this._adapterFactory = adapterFactory;
            nexus.Attach(this);
            platforms = new KeyValueStore<string, string>(CreateKeyStoreAdapter(StorageConst.Platform.ToString()));

            logger.Message("Platform count: " + platforms.Count);
            platforms.Visit((key, _) =>
    		{
                logger.Message("Adding: " + key);
                _keyValueStore.Add(key + StorageConst.Block, new KeyValueStore<string, InteropBlock>(
                                                    CreateKeyStoreAdapter(key + StorageConst.Block)
                                                )
                                            );

                _keyValueStore.Add(key + StorageConst.Transaction, new KeyValueStore<string, InteropTransaction>(
                                                    CreateKeyStoreAdapter(key + StorageConst.Transaction)
                                                )
                                            );

                _keyValueStore.Add(key + StorageConst.CurrentHeight, new KeyValueStore<string, string>(
                                                    CreateKeyStoreAdapter(key + StorageConst.CurrentHeight)
                                                )
                                            );
    		});
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

            IKeyValueStoreAdapter result;

            if (_adapterFactory != null)
            {
                result = _adapterFactory(name);

                if (result == null)
                {
                    throw new Exception("keystore adapter factory failed");
                }
            }
            else
            {
                result = new MemoryStore();
            }

            if (!_keystoreCache.ContainsKey(name))
            {
                _keystoreCache[name] = result;
            }

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
                else
                {
                    logger.Message($"no data found for key { storageKey }");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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

            return false;
        }

        protected override decimal PullPrice(Timestamp time, string symbol)
        {
            if (!string.IsNullOrEmpty(CLI.cryptoCompareAPIKey))
            {
                if (symbol == DomainSettings.FuelTokenSymbol)
                {
                    var result = PullPrice(time, DomainSettings.StakingTokenSymbol);
                    return result / 5;
                }

                var price = CryptoCompareUtils.GetCoinRate(symbol, DomainSettings.FiatTokenSymbol, CLI.cryptoCompareAPIKey);
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
                        neoBlock = CLI.neoAPI.GetBlock(new UInt256(LuxUtils.ReverseHex(hash.ToString()).HexToBytes()));
                    }
                    else
                    {
                        neoBlock = CLI.neoAPI.GetBlock(height);
                    }

                    interopTuple = NeoInterop.MakeInteropBlock(neoBlock, CLI.neoAPI, CLI.tokenSwapper.swapAddress);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            if (interopTuple.Item1.Hash != Hash.Null)
            {

                var persisted = Persist<InteropBlock>(platformName, chainName, interopTuple.Item1.Hash, StorageConst.Block, interopTuple.Item1);

                if (persisted)
                {
                    var transactions = interopTuple.Item2;

                    foreach (var tx in transactions)
                    {
                        var txPersisted = Persist<InteropTransaction>(platformName, chainName, tx.Hash, StorageConst.Transaction, tx);
                    }
                }
                else 
                {
                    logger.Error($"Persisting oracle block { interopTuple.Item1.Hash } on platform { platformName } failed!");
                }
            }

            return interopTuple.Item1;
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            InteropTransaction tx = Read<InteropTransaction>(platformName, chainName, hash, StorageConst.Transaction);

            if (tx != null && tx.Hash != null)
            {
                return tx;
            }

            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    NeoTx neoTx;
                    UInt256 uHash = new UInt256(LuxUtils.ReverseHex(hash.ToString()).HexToBytes());
                    neoTx = CLI.neoAPI.GetTransaction(uHash);
                    tx = NeoInterop.MakeInteropTx(neoTx, CLI.neoAPI, CLI.tokenSwapper.swapAddress);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            if (!Persist<InteropTransaction>(platformName, chainName, tx.Hash, StorageConst.Transaction, tx))
            {
                logger.Error($"Persisting oracle transaction { hash } on platform { platformName } failed!");
            }

            return tx;
        }

        protected override T PullData<T>(Timestamp time, string url)
        {
            throw new OracleException("unknown oracle url");
        }
    }
}
