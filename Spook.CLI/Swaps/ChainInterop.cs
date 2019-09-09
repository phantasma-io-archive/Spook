using Phantasma.Numerics;
using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;

namespace Phantasma.Spook.Swaps
{
    public struct ChainSwap
    {
        public const string DummyHash = "none";

        public string sourceHash;
        public string sourceChain;
        public string sourceAddress;
        public string sendHash;
        public string receiveHash;
        public string destinationAddress;
        public string destinationChain;
        public string symbol;
        public decimal amount;
    }

    public struct TokenInfo
    {
        public string chain;
        public string symbol;
        public string hash;
        public int decimals;
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
        public Address ExternalAddress { get; private set; }

        public string WIF { get; private set; }
        public BigInteger currentHeight { get; protected set; }

        // from Phantasma to local
        private Dictionary<Address, string> fromMap = new Dictionary<Address, string>();

        // from local to Phantasma 
        private Dictionary<string, Address> toMap = new Dictionary<string, Address>();

        private string MapFileName => Name + "_map.csv";

        public ChainInterop(TokenSwapper swapper, string baseWif, BigInteger currentBlock)
        {
            this.Swapper = swapper;
            var temp = this.Name + "!" + baseWif;
            var privateKey = CryptoExtensions.Sha256(temp);
            var key = new KeyPair(privateKey);
            this.WIF = key.ToWIF();
            this.ExternalAddress = key.Address;
            this.currentHeight = currentBlock;
        }

        public abstract void Update(Action<IEnumerable<ChainSwap>> callback);

        // removes/burns funds from source chain
        public abstract string SendFunds(string address, TokenInfo token, decimal amount);

        // adds/mints funds in destination chain
        public abstract string ReceiveFunds(string address, TokenInfo token, decimal amount);

        public void RegisterMapping(string localAddress, Address externalAddress)
        {
            fromMap[externalAddress] = localAddress;
            toMap[localAddress] = externalAddress;
            File.AppendAllText(MapFileName, $"{Environment.NewLine}");
        }

        internal string FromExternalToLocal(Address sourceAddress)
        {
            if (fromMap.ContainsKey(sourceAddress))
            {
                return fromMap[sourceAddress];
            }

            throw new InteropException($"Could not map Phantasma address {sourceAddress} to {Name} address");
        }

        internal Address FromLocalToExternal(string sourceAddress)
        {
            if (toMap.ContainsKey(sourceAddress))
            {
                return toMap[sourceAddress];
            }

            throw new InteropException($"Could not map {Name} address {sourceAddress} to Phantasma address");
        }
    }
}
