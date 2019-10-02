using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Ethereum;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;

namespace Phantasma.Spook.Swaps
{
    public class EthereumInterop : ChainWatcher
    {
        public EthereumInterop(TokenSwapper swapper, BigInteger currentBlock) : base(swapper, "ethereum")
        {
        }

        public override IEnumerable<PendingSwap> Update()
        {
            // TODO
            return Enumerable.Empty<PendingSwap>();
        }
    }
}
