using System;
using Phantasma.Domain;

using Logger = Phantasma.Core.Log.Logger;

namespace Phantasma.Spook.Oracles
{
    public static class Pricer
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, string cryptoCompApiKey, bool cgEnabled, PricerSupportedToken[] supportedTokens, Logger logger)
        {
            try
            {
                Decimal cGeckoPrice = 0;
                Decimal cComparePrice = 0;

                cComparePrice = CryptoCompareUtils.GetCoinRate(baseSymbol, quoteSymbol, cryptoCompApiKey, supportedTokens, logger);

                if (cgEnabled)
                {
                    cGeckoPrice = CoinGeckoUtils.GetCoinRate(baseSymbol, quoteSymbol, supportedTokens, logger);
                }

                if ((cGeckoPrice > 0) && (cComparePrice > 0))
                {
                    return ((cGeckoPrice + cComparePrice) / 2);
                }
                if((cGeckoPrice > 0) && (cComparePrice <= 0))
                {
                    return (cGeckoPrice);
                }
                if ((cComparePrice > 0) && (cGeckoPrice <= 0))
                {
                    return (cComparePrice);
                }
                return 0;

            }
            catch (Exception ex)
            {
                var errorMsg = ex.ToString();
                logger.Error($"Pricer error: {errorMsg}");
                return 0;
            }
        }
    }
}
