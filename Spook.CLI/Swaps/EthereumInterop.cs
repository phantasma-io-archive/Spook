using System;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Spook.Swaps
{
    public class EthereumInterop : ChainInterop
    {
        public EthereumInterop(TokenSwapper swapper, KeyPair keys, BigInteger currentBlock) : base(swapper, keys, currentBlock)
        {
        }

        public override string LocalAddress => throw new NotImplementedException();

        public override string Name => throw new NotImplementedException();

        public override void Update(Action<IEnumerable<ChainSwap>> callback)
        {
            throw new NotImplementedException();
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
