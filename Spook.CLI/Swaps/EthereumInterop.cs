using System;
using System.Collections.Generic;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Ethereum;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;

namespace Phantasma.Spook.Swaps
{
    public class EthereumInterop : ChainInterop
    {
        private EthereumKey ethKeys;

        public EthereumInterop(TokenSwapper swapper, KeyPair keys, BigInteger currentBlock) : base(swapper, keys, currentBlock)
        {
            this.ethKeys = new EthereumKey(this.Keys.PrivateKey);
        }

        public override string LocalAddress => ethKeys.address;
        public override string Name => EthereumWallet.EthereumPlatform;
        public override string PrivateKey => Base16.Encode(this.Keys.PrivateKey);

        public override void Update(Action<IEnumerable<ChainSwap>> callback)
        {
            // TODO
        }

        public override string ReceiveFunds(ChainSwap swap)
        {
            throw new NotImplementedException();
        }
    }
}
