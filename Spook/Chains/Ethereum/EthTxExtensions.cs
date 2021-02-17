using Nethereum.Util;

namespace Nethereum.RPC.Eth.DTOs
{
    public static class EthTXExtension
    {
        public static bool IsToAny(this Transaction txn, string[] addresses)
        {
            if (string.IsNullOrEmpty(txn.To))
            {
                return false;
            }

            string toAddress;
            if (txn.To.StartsWith("0x"))
            {
                toAddress = txn.To.Substring(2);
            }
            else
            {
                toAddress = txn.To;
            }

            foreach (var address in addresses)
            {
                if (toAddress.IsTheSameAddress(address))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
