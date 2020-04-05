using Phantasma.Blockchain;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Pay.Chains;
using Phantasma.Storage;
using System.IO;

namespace Phantasma.Spook.Oracles
{
    public class SpookOracle : OracleReader
    {
        public readonly CLI CLI;

        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
        private Dictionary<string, IKeyValueStoreAdapter> _keystoreCache =
                new Dictionary<string, IKeyValueStoreAdapter>();

        enum StorageConst
        {
            CurrentBlock,
            Block,
            Transaction
        }

        public SpookOracle(CLI cli, Nexus nexus, Func<string, IKeyValueStoreAdapter> adapterFactory = null,) : base(nexus)
        {
            this.CLI = cli;
            this._adapterFactory = adapterFactory;

            var platforms = nexus.GetPlatforms(nexus.RootStorage);                                                                                                                                                                            
            foreach (var platform in platforms) 
            {
                _keystoreCache.Add(platform, new KeyValueStore<Hash, InteropBlock>(CreateKeyStoreAdapter(platform + StorageConst.Block)));
                _keystoreCache.Add(platform, new KeyValueStore<Hash, InteropTransaction>(CreateKeyStoreAdapter(platform + StorageConst.Transaction)));
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
                Throw.If(result == null, "keystore adapter factory failed");
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

        private Nullable<T> Read(string platformName, string chainName, Hash hash, StorageConst type)
        {
            var storageKey = type + chainName + hash.ToString();
            IKeyValueStoreAdapter keyStore = _keystoreCache[platformName + type];

            try
            {
                T data = keyStore.Get(storageKey);
                return data;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public InteropBlock GetCurrentBlock(string platformName, string chainName)
        {
            var storageKey = StorageConst.CurrentBlock + chainName + hash.ToString();
            IKeyValueStoreAdapter keyStore = _keystoreCache[platformName + StorageConst.Block];

            return keyStore.Get(storageKey);
        }

        public void SetCurrentBlock(string platformName, string chainName, InteropBlock block)
        {
            var storageKey = StorageConst.CurrentBlock + chainName + hash.ToString();
            IKeyValueStoreAdapter keyStore = _keystoreCache[platformName + StorageConst.Block];

            keyStore.Set(storageKey, block);
        }

        private bool Persist<T>(string platformName, string chainName, Hash hash, StorageConst type, T data)
        {
            var storageKey = type + chainName + hash.ToString();
            IKeyValueStoreAdapter keyStore = _keystoreCache[platformName + type];

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

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, BigInteger height = null)
        {
            InteropBlock block;

            if (hash == null && height == null)
            {
                throw new OracleException($"Fetching block not possible without hash or height");
            }

            if (height == null && (block = Read(platformName, chainName, hash, StorageConst.Block)) != null)
            {
                return block;
            }

            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    block = CLI.neoAPI.GetBlock((height == null) ? hash : height);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }
            // TODO, maybe check if block is of interest (contains swaps)
            // Otherwise we would store all blocks, which is not what we might want.
            
            if (!Persist<InteropBlock>(platformName, chainName, hash, block, StorageConst.Block))
            {
                throw new OracleException($"Persisting oracle block { hash } on platform { platformName } failed!");
            }

            return block;
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            InteropTransaction tx;

            if ((tx= Read(platformName, chainName, hash, StorageConst.Transaction)) != null)
            {
                return tx;
            }

            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    tx = CLI.neoAPI.GetTransaction(hash);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            if (!Persist<InteropTransaction>(platformName, chainName, hash, tx, StorageConst.Transaction))
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
