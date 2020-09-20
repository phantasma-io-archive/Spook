using System;
using System.Threading.Tasks;

namespace Phantasma.Spook.Chains
{
    public static class EthUtils
    {
        public static void RunSync(Func<Task> method)
        {
            var task = method();
            task.GetAwaiter().GetResult();
        }

        public static TResult RunSync<TResult>(Func<Task<TResult>> method)
        {
            var task = method();
            return task.GetAwaiter().GetResult();
        }

        public static string FindSymbolFromAsset(Blockchain.Nexus nexus, string assetID)
        {
            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Substring(2);
            }

            var symbol = nexus.GetPlatformTokenByHash(Cryptography.Hash.FromUnpaddedHex(assetID), "ethereum", nexus.RootStorage);

            if (String.IsNullOrEmpty(symbol))
            {
                Console.WriteLine("assset not found: " + assetID);
                return null;
            }

            return symbol;
        }
    }
}
