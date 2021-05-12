using System;
using Phantasma.Domain;

namespace Phantasma.Spook.Oracles
{
    public static class Pricer
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, string cryptoCompApiKey, bool cgEnabled, PricerSupportedToken[] supportedTokens)
        {
            try
            {
                Decimal cGeckoPrice = 0;
                Decimal cCompare = 0;

                cCompare = CryptoCompareUtils.GetCoinRate(baseSymbol, quoteSymbol, cryptoCompApiKey, supportedTokens);

                if (cgEnabled)
                {
                    cGeckoPrice = CoinGeckoUtils.GetCoinRate(baseSymbol, quoteSymbol, supportedTokens);
                }

                if ((cGeckoPrice > 0) && (cCompare > 0))
                {
                    return ((cGeckoPrice + cCompare) / 2);
                }
                if((cGeckoPrice > 0) && (cCompare <= 0))
                {
                    return (cGeckoPrice);
                }
                if ((cCompare > 0) && (cGeckoPrice <= 0))
                {
                    return (cCompare);
                }
                return 0;

            }
            catch (Exception ex)
            {
                return 0;
            }
        }
    }
}
