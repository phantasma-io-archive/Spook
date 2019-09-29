using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain.Swaps;
using Phantasma.Cryptography;
using Phantasma.Ethereum;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;

namespace Phantasma.Spook.Swaps
{
    public class EthereumInterop : ChainInterop
    {
        private EthereumKey ethKeys;

        public EthereumInterop(TokenSwapper swapper, PhantasmaKeys keys, BigInteger currentBlock) : base(swapper, keys, currentBlock)
        {
            this.ethKeys = new EthereumKey(this.Keys.PrivateKey);
        }

        public override string LocalAddress => ethKeys.address;
        public override string Name => EthereumWallet.EthereumPlatform;
        public override string PrivateKey => Base16.Encode(this.Keys.PrivateKey);

        public override IEnumerable<ChainSwap> Update()
        {
            // TODO
            return Enumerable.Empty<ChainSwap>();
        }

        public override Hash ReceiveFunds(ChainSwap swap)
        {
            throw new NotImplementedException();
        }

        public override BrokerResult PrepareBroker(ChainSwap swap, out Hash brokerHash)
        {
            throw new NotImplementedException();
        }

        public override Hash SettleTransaction(Hash destinationHash, string destinationPlatform)
        {
            throw new NotImplementedException();
        }
    }
}
