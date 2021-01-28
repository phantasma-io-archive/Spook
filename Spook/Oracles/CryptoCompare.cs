using LunarLabs.Parser.JSON;

namespace Phantasma.Spook.Oracles
{
    public static class CryptoCompareUtils
    {
        public static decimal GetCoinRate(string baseSymbol, string quoteSymbol, string APIKey)
        {
            var url = $"https://min-api.cryptocompare.com/data/price?fsym={baseSymbol}&tsyms={quoteSymbol}&api_key={APIKey}";

            string json;

            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                }

                var root = JSONReader.ReadFromString(json);

                var price = root.GetDecimal(quoteSymbol);

                return price;
            }
            catch
            {
                return 0;
            }
        }
    }

}
