using Phantasma.Core.Types;
using System.Collections.Generic;
using Phantasma.Domain;
using NativeBigInt = System.Numerics.BigInteger;
using System;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain
{
    public class DummyOracle : OracleReader
    {

        public DummyOracle(Nexus nexus) : base(nexus)
        {
        }

        public override T Read<T>(Timestamp time, string url) 
        {
            throw new NotImplementedException();
        }

        public override List<InteropBlock> ReadAllBlocks(string platformName, string chainName)
        {
            throw new NotImplementedException();
        }

        public override string GetCurrentHeight(string platformName, string chainName)
        {
            throw new NotImplementedException();
        }

        public override void SetCurrentHeight(string platformName, string chainName, string height)
        {
            throw new NotImplementedException();
        }

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, NativeBigInt height = new NativeBigInt())
        {
            throw new NotImplementedException();
        }

        protected override Phantasma.Numerics.BigInteger PullFee(Timestamp time, string platform)
        {
            throw new NotImplementedException();
        }

        protected override decimal PullPrice(Timestamp time, string symbol)
        {
            throw new NotImplementedException();
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            throw new NotImplementedException();
        }

        protected override InteropNFT PullPlatformNFT(string platformName, string symbol, Numerics.BigInteger tokenID)
        {
            throw new NotImplementedException();
        }

        protected override T PullData<T>(Timestamp time, string url)
        {
            throw new NotImplementedException();
        }

        public new void Clear()
        {
            throw new NotImplementedException();
        }
    }
}
