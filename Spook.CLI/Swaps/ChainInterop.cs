using Phantasma.Numerics;
using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Spook.Swaps
{
    public struct ChainSwap
    {
        public const string DummyHash = "none";

        public string sourceHash;
        public string sourcePlatform;
        public string sourceAddress;
        public string destinationHash;
        public string destinationPlatform;
        public string destinationAddress;
        public string symbol;
        public decimal amount;
    }

    public class InteropException : Exception
    {
        public InteropException(string msg) : base(msg)
        {
        }
    }

    public abstract class ChainInterop
    {
        public readonly TokenSwapper Swapper;

        public abstract string Name { get; }
        public abstract string LocalAddress { get; }
        public abstract string PrivateKey { get; }

        protected KeyPair Keys { get; private set; }

        public Address ExternalAddress { get; private set; }

        public BigInteger currentHeight { get; protected set; }

        public ChainInterop(TokenSwapper swapper, KeyPair keys, BigInteger currentBlock)
        {
            this.Swapper = swapper;
            this.Keys = InteropUtils.GenerateInteropKeys(keys, this.Name);
            this.ExternalAddress = Keys.Address;
            this.currentHeight = currentBlock;
        }

        public abstract void Update(Action<IEnumerable<ChainSwap>> callback);

        // adds/mints funds in destination chain
        public abstract string ReceiveFunds(ChainSwap swap);
    }
}
