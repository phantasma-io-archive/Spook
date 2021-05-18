using System;
using LunarLabs.Parser.JSON;

using Logger = Phantasma.Core.Log.Logger;

namespace Phantasma.Spook.Oracles
{
    public static class CryptoCompareUtils
    {
        
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, string APIKey, PricerSupportedToken[] supportedTokens, Logger logger)
        {

            string baseticker = "";

            foreach (var token in supportedTokens)
            {
                if (token.ticker == baseSymbol)
                {
                    baseticker = token.cryptocompareId;
                    break;
                }
            }

            if (String.IsNullOrEmpty(baseticker))
                return 0;

            var url = $"https://min-api.cryptocompare.com/data/price?fsym={baseticker}&tsyms={quoteSymbol}&api_key={APIKey}";

            string json;

            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                }

                if (String.IsNullOrEmpty(json))
                    return 0;

                var root = JSONReader.ReadFromString(json);
                var price = root.GetDecimal(quoteSymbol);

                return price;
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                logger.Error($"Error while trying to query {baseticker} price from CryptoCompare API: {errorMsg}");
                return 0;
            }
        }
    }

}
