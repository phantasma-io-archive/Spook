using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;

namespace Phantasma.Spook.Oracles
{
    public class SpookOracle : OracleReader
    {
        public readonly CLI CLI;

        public SpookOracle(CLI cli, Nexus nexus) : base(nexus)
        {
            this.CLI = cli;
        }

        protected override decimal PullPrice(string baseSymbol, string quoteSymbol)
        {
            if (CLI.cryptoCompareAPIKey != null)
            {
                var price = CryptoCompareUtils.GetCoinRate(baseSymbol, quoteSymbol, CLI.cryptoCompareAPIKey);
                return price;
            }

            throw new OracleException("No support for prices");
        }

        protected override InteropBlock PullPlatformBlock(string platformName, Hash hash)
        {
            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    return CLI.NeoScanAPI.ReadBlock(hash);

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, Hash hash)
        {
            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    return CLI.NeoScanAPI.ReadTransaction(hash);

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }
        }

        protected override byte[] PullData(string url)
        {
            throw new OracleException("unknown oracle url");
        }
    }
}
