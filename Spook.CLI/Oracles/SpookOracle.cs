using System;
using NativeBigInt = System.Numerics.BigInteger;
using System.Collections.Generic;
using Phantasma.Blockchain;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Pay.Chains;
using Phantasma.Storage;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using Phantasma.Spook.Interop;
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
        private bool storageInitialized;

        enum StorageConst
        {
            CurrentBlock,
            Block,
            Transaction
        }

        public SpookOracle(CLI cli, Nexus nexus, Func<string, IKeyValueStoreAdapter> adapterFactory = null) : base(nexus)
        {
            this.CLI = cli;
            this._adapterFactory = adapterFactory;

            //var platforms = nexus.GetPlatforms(nexus.RootStorage);
            //Console.WriteLine("platform count:" + platforms.Length);
            //Console.WriteLine("STACK: " + new System.Diagnostics.StackTrace(true).ToString());
            //foreach (var platform in platforms)
            //{
            //    Console.WriteLine("Adding: " + platform + StorageConst.Block);
            //    Console.WriteLine("Adding: " + platform + StorageConst.Transaction);
            //    _keyValueStore.Add(platform + StorageConst.Block, new KeyValueStore<string, InteropBlock>(CreateKeyStoreAdapter(platform + StorageConst.Block)));
            //    _keyValueStore.Add(platform + StorageConst.Transaction, new KeyValueStore<string, InteropTransaction>(CreateKeyStoreAdapter(platform + StorageConst.Transaction)));
            //}
            //Console.WriteLine("AFTER COUNT: " + _keyValueStore.Count);
        }

        public void Update(INexus nexus)
        {
            var platforms = (nexus as Nexus).GetPlatforms((nexus as Nexus).RootStorage);
            foreach (var platform in platforms)
            {
                Console.WriteLine("Adding: " + platform + StorageConst.Block);
                Console.WriteLine("Adding: " + platform + StorageConst.Transaction);
                _keyValueStore.Add(platform + StorageConst.Block, new KeyValueStore<string, InteropBlock>(CreateKeyStoreAdapter(platform + StorageConst.Block)));
                _keyValueStore.Add(platform + StorageConst.Transaction, new KeyValueStore<string, InteropTransaction>(CreateKeyStoreAdapter(platform + StorageConst.Transaction)));
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
                Console.WriteLine("NAME:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::: " + name);
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
            Console.WriteLine("COUNT: " + _keyValueStore.Count);
            var keyStore = _keyValueStore[platform + type] as KeyValueStore<string, T>;
            var storageKey = type + chainName + hash.ToString();

            try
            {
                T data = keyStore.Get(storageKey);
                return data;
            }
            catch
            {
                return default(T);
            }
        }

        public InteropBlock GetCurrentBlock(string platformName, string chainName)
        {
            var storageKey = StorageConst.CurrentBlock + chainName;
            var keyStore = _keyValueStore[platformName + StorageConst.Block] as KeyValueStore<string, InteropBlock>;

            return keyStore.Get(storageKey);
        }

        public void SetCurrentBlock(string platformName, string chainName, InteropBlock block)
        {
            var storageKey = StorageConst.CurrentBlock + chainName;
            var keyStore = _keyValueStore[platformName + StorageConst.Block] as KeyValueStore<string, InteropBlock>;

            keyStore.Set(storageKey, block);
        }

        private bool Persist<T>(string platformName, string chainName, Hash hash, StorageConst type, T data)
        {
            var storageKey = type + chainName + hash.ToString();
            Console.WriteLine("storageKey: " + storageKey);
            var keyStore = _keyValueStore[platformName + type] as KeyValueStore<string, T>;
            Console.WriteLine("storageKey: " + platformName + type);

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

            if (height == null && block.Hash != null)
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
                    //block = CLI.neoAPI.GetBlock((height == 0)
                    //        ? new UInt256(LuxUtils.ReverseHex(hash.ToString()).HexToBytes())
                    //        : height);

                    interopTuple = NeoInterop.MakeInteropBlock(neoBlock, CLI.neoAPI, CLI.tokenSwapper.swapAddress);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }


            //TODO store transactions --> interopTuple.Item2
            Console.WriteLine("Store: " + interopTuple.Item1.Hash);

            if (interopTuple.Item1.Hash != null && !Persist<InteropBlock>(platformName, chainName, hash, StorageConst.Block, interopTuple.Item1))
            {
                throw new OracleException($"Persisting oracle block { hash } on platform { platformName } failed!");
            }

            return block;
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            InteropTransaction tx = Read<InteropTransaction>(platformName, chainName, hash, StorageConst.Transaction);

            if (tx.Hash != null)
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

            if (!Persist<InteropTransaction>(platformName, chainName, hash, StorageConst.Transaction, tx))
            {
                throw new OracleException($"Persisting oracle transaction { hash } on platform { platformName } failed!");
            }

            return tx;
        }

        protected override byte[] PullData(Timestamp time, string url)
        {
            throw new OracleException("unknown oracle url");
        }
    }
}
