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

        public readonly string cachePath;
        public SpookOracle(CLI cli, Nexus nexus, string cachePath) : base(nexus)
        {
            this.CLI = cli;
            this.cachePath = cachePath;

            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }
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

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash)
        {
            InteropBlock block;
            byte[] bytes;

            var fileName = GetCacheFileName(platformName, chainName, hash.ToString(), "blk");
            if (File.Exists(fileName))
            {
                bytes = File.ReadAllBytes(fileName);
                block = Serialization.Unserialize<InteropBlock>(bytes);
                return block;
            }

            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    block = CLI.neoScanAPI.ReadBlock(hash);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            bytes = Serialization.Serialize(block);
            File.WriteAllBytes(fileName, bytes);

            return block;
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            InteropTransaction tx;
            byte[] bytes;

            var fileName = GetCacheFileName(platformName, chainName, hash.ToString(), "tx");
            if (File.Exists(fileName))
            {
                bytes = File.ReadAllBytes(fileName);
                tx = Serialization.Unserialize<InteropTransaction>(bytes);
                return tx;
            }

            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    tx = CLI.neoScanAPI.ReadTransaction(hash);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            bytes = Serialization.Serialize(tx);
            File.WriteAllBytes(fileName, bytes);

            return tx;
        }

        protected override byte[] PullData(Timestamp time, string url)
        {
            throw new OracleException("unknown oracle url");
        }

        private string GetCacheFileName(string platform, string chain, string hash, string extension)
        {
            var fileName = $"{cachePath}/{platform}_{chain}_{hash}.{extension}";
            return fileName;
        }
    }
}
