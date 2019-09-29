using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Domain;
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

        protected override decimal PullPrice(string symbol)
        {
            if (CLI.cryptoCompareAPIKey != null)
            {
                if (symbol == DomainSettings.FuelTokenSymbol)
                {
                    var result = PullPrice(DomainSettings.StakingTokenSymbol);
                    return result / 5;
                }
             
                var price = CryptoCompareUtils.GetCoinRate(symbol, DomainSettings.FiatTokenSymbol, CLI.cryptoCompareAPIKey);
                return price;
            }

            throw new OracleException("No support for prices");
        }

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash)
        {
            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    return CLI.neoScanAPI.ReadBlock(hash);

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    return CLI.neoScanAPI.ReadTransaction(hash);

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
