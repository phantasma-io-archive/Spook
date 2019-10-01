using Phantasma.Numerics;
using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using Phantasma.Blockchain;

namespace Phantasma.Spook.Swaps
{
    public enum ChainSwapStatus
    {
        Invalid,
        Pending,
        Sending,
        Settle,
        Broker,
        Receive,
        Platform,
        Finished
    }

    public class InteropException : Exception
    {
        public readonly ChainSwapStatus SwapStatus;

        public InteropException(string msg, ChainSwapStatus status) : base(msg)
        {
            this.SwapStatus = status;
        }
    }

    public abstract class ChainInterop
    {
        public readonly Nexus Nexus;

        public abstract string Name { get; }
        public abstract string LocalAddress { get; }
        public abstract string PrivateKey { get; }

        protected PhantasmaKeys Keys { get; private set; }

        public Address ExternalAddress { get; private set; }

        public BigInteger currentHeight { get; protected set; }

        public ChainInterop(Nexus nexus, PhantasmaKeys keys, BigInteger currentBlock)
        {
            this.Nexus = nexus;
            this.Keys = InteropUtils.GenerateInteropKeys(keys, this.Name);
            this.ExternalAddress = Keys.Address;
            this.currentHeight = currentBlock;
        }

        public abstract IEnumerable<ChainSwap> Update();

        // adds/mints funds in destination chain
        public abstract Hash ReceiveFunds(ChainSwap swap);

        // only required for Phantasma
        public abstract BrokerResult PrepareBroker(ChainSwap swap, out Hash brokerHash);
        public abstract Hash SettleTransaction(Hash destinationHash, string destinationPlatform);
    }
}