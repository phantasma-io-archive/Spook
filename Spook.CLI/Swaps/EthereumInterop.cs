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
            this.ethKeys = EthereumKey.FromWIF(this.WIF);
        }

        public override string LocalAddress => ethKeys.address;

        public override string Name => EthereumWallet.EthereumPlatform;

        public override void Update(Action<IEnumerable<ChainSwap>> callback)
        {
            // TODO
        }

        public override string SendFunds(string address, TokenInfo token, decimal amount)
        {
            return ChainSwap.DummyHash;
        }

        public override string ReceiveFunds(string sourceChain, Hash sourceHash, string address, TokenInfo token, decimal amount)
        {
            throw new NotImplementedException();
        }
    }
}
