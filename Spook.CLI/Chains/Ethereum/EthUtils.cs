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

        public static string FindSymbolFromAsset(string assetID)
        {
            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Substring(2) ;
            }

            switch (assetID)
            {
                case "4c2af2fb374b988363deb535bf0ff2d1eb7b2106": return "SOUL";
                case "a9858f0e2037c18dd6a0b4bc082d41b0536d47e2": return "KCAL";
                //case "ETH": return "ETH";
                default: 
                    Console.WriteLine("assset not found: " + assetID);
                    return null;
            }
        }
    }
}
