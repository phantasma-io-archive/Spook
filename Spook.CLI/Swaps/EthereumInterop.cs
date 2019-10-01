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
    public class EthereumInterop : SwapFinder
    {
        public EthereumInterop(BigInteger currentBlock) 
        {
        }

        public override IEnumerable<ChainSwap> Update()
        {
            // TODO
            return Enumerable.Empty<ChainSwap>();
        }
    }
}
