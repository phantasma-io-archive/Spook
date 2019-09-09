using System;
using System.Collections.Generic;
using Phantasma.Numerics;

namespace Phantasma.Spook.Swaps
{
    public class EthereumInterop : ChainInterop
    {
        public EthereumInterop(TokenSwapper swapper, string wif, BigInteger currentBlock) : base(swapper, wif, currentBlock)
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

        public override string ReceiveFunds(string address, TokenInfo token, decimal amount)
        {
            throw new NotImplementedException();
        }
    }
}
