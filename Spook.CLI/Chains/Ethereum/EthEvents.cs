using System.Numerics;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Phantasma.Spook.Chains
{
    [Event("Swap")]
    public class SwapEventDTO : IEventDTO
    {
        [Parameter("address", "_from", 1, true)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 2, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_value", 3, false)]
        public virtual BigInteger Value { get; set; }
    }
}
