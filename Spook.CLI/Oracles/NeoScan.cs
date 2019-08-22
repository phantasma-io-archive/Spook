using LunarLabs.Parser.JSON;

namespace Phantasma.Spook.Oracles
{
    public class NeoScanUtils
    {
        public static bool IsTransactionValid(string hash)
        {
            var url = $"https://api.neoscan.io/api/main_net/v1/get_transaction/{hash}";

            string json;

            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                }

                var root = JSONReader.ReadFromString(json);

                return root.HasNode("vouts");
            }
            catch
            {
                return false;
            }
        }
    }
}
