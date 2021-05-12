using System;
using LunarLabs.Parser.JSON;
using System.Net.Http;

namespace Phantasma.Spook.Oracles
{
    public static class CoinGeckoUtils
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, PricerSupportedToken[] supportedTokens)
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

                using (var httpClient = new HttpClient())
                {
                    json = httpClient.GetStringAsync(new Uri(url)).Result;
                }

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
                return 0;
            }
        }
    }
}
