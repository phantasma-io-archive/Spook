using System.Numerics;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Phantasma.Spook.Chains
{
    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public string Owner { get; set; }
    }

    [Function("swapOut", "bool")]
    public class SwapOutFunction : FunctionMessage
    {
        [Parameter("address", "source", 1)]
        public string Source { get; set; }

        [Parameter("address", "target", 2)]
        public string Target { get; set; }

        [Parameter("uint256", "amount", 3)]
        public BigInteger Amount { get; set; }
    }

    [Function("swapIn", "bool")]
    public class SwapInFunction : FunctionMessage
    {
        [Parameter("address", "source", 1)]
        public string Source { get; set; }

        [Parameter("address", "target", 2)]
        public string Target { get; set; }

        [Parameter("uint256", "amount", 3)]
        public BigInteger Amount { get; set; }
    }

    [Function("transfer", "bool")]
    public class TransferFunction : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public string To { get; set; }

        [Parameter("uint256", "_value", 2)]
        public BigInteger TokenAmount { get; set; }
    }
}
