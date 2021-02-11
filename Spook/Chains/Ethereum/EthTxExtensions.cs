using Nethereum.Util;

namespace Nethereum.RPC.Eth.DTOs
{
    public static class EthTXExtension
    {
        public static bool IsToAny(this Transaction txn, string[] addresses)
        {
            foreach(var address in addresses)
            {
                string toAddress = null;
                if (txn.To.StartsWith("0x"))
                {
                    toAddress = txn.To.Substring(2);
                }
                else
                {
                    toAddress = txn.To;
                }

                if (toAddress.IsTheSameAddress(address))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
