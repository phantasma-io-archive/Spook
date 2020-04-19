using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Ethereum;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;
using Phantasma.Spook.Swaps;

namespace Phantasma.Spook.Interop
{
    public class EthereumInterop : ChainWatcher
    {
        public EthereumInterop(TokenSwapper swapper, string wif, BigInteger currentBlock) : base(swapper, wif, "ethereum")
        {
        }

        protected override string GetAvailableAddress(string wif)
        {
            var keys = Phantasma.Ethereum.EthereumKey.FromWIF(wif);
            return keys.Address;
        }

        public override IEnumerable<PendingSwap> Update()
        {
            // TODO
            return Enumerable.Empty<PendingSwap>();
        }
    }
}
