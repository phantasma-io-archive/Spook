using Nethereum.Util;

namespace Nethereum.RPC.Eth.DTOs
{
    public static class EthTXExtension
    {
        public static bool IsToAny(this Transaction txn, string[] addresses)
        {
            foreach(var address in addresses)
            {
                //System.Console.WriteLine("txn.To: " + txn.To);
                if (txn.To.IsTheSameAddress(address))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
