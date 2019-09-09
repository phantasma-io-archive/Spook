using System.Linq;

using Phantasma.Blockchain;
using Phantasma.Numerics;

namespace Phantasma.Spook.Oracles
{
    public class SpookOracle : OracleReader
    {
        private const string interopTag = "interop://";
        private const string priceTag = "price://";

        public readonly CLI CLI;

        public SpookOracle(CLI cli)
        {
            this.CLI = cli;
        }

        protected override byte[] PullData(string url)
        {
            if (url.StartsWith(interopTag))
            {
                url = url.Substring(interopTag.Length);
                var args = url.Split('/');

                var chainName = args[0];
                args = args.Skip(1).ToArray();

                switch (chainName)
                {
                    case "neo":
                        return CLI.NeoScanAPI.ReadOracle(args);

                    default:
                        throw new OracleException("invalid oracle chain: " + chainName);
                }
            }
            else
            if (url.StartsWith(priceTag))
            {
                url = url.Substring(priceTag.Length);
                var symbols = url.Split('/');

                if (symbols.Length < 1 || symbols.Length > 2)
                {
                    throw new OracleException("invalid oracle price request");
                }

                var baseSymbol = symbols[0];
                var quoteSymbol = symbols.Length>1? symbols[1]: Nexus.FiatTokenSymbol;

                if (CLI.cryptoCompareAPIKey != null)
                {
                    var price = CryptoCompareUtils.GetCoinRate(baseSymbol, quoteSymbol, CLI.cryptoCompareAPIKey);
                    var val = UnitConversion.ToBigInteger(price, 8);
                    return val.ToUnsignedByteArray();
                }

                throw new OracleException("No support for prices");
            }
            else
            {
                throw new OracleException("unknown oracle protocol");
            }
        }
    }
}
