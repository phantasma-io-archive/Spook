using System;
using LunarLabs.Parser.JSON;
using System.Net.Http;

using Logger = Phantasma.Core.Log.Logger;

namespace Phantasma.Spook.Oracles
{
    public static class CoinGeckoUtils
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, PricerSupportedToken[] supportedTokens, Logger logger)
        {

            string json;
            string baseticker = "";

            foreach (var token in supportedTokens)
            {
                if(token.ticker == baseSymbol)
                {
                    baseticker = token.coingeckoId;
                    break;
                }
            }

            if (String.IsNullOrEmpty(baseticker))
                return 0;

            var url = $"https://api.coingecko.com/api/v3/simple/price?ids={baseticker}&vs_currencies={quoteSymbol}";


            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                }

                if (String.IsNullOrEmpty(json))
                    return 0;

                var root = JSONReader.ReadFromString(json);

                // hack for goati price .10
                if (baseticker == "GOATI")
                {
                    var price = 0.10m;
                    return price;
                }
                else
                {
                    root = root[baseticker];
                    var price = root.GetDecimal(quoteSymbol.ToLower());
                    return price;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                logger.Error($"Error while trying to query {baseticker} price from CoinGecko API: {errorMsg}");
                return 0;
            }
        }
    }
}
