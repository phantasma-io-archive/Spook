using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.CodeGen.Core;
using Phantasma.VM.Utils;
using Phantasma.VM;
using Phantasma.CodeGen.Assembler;
using Phantasma.Cryptography;
using Phantasma.Blockchain;
using Phantasma.API;
using System.Linq;
using Phantasma.Core.Log;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using System.Threading;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;

namespace Phantasma.Spook.Modules
{
    [Module("nexus")]
    public static class NexusModule
    {
        public static Logger logger => ModuleLogger.Instance;

        public static void Rescan(Nexus oldNexus, PhantasmaKeys owner, string[] args)
        {
            /*if (args.Length != 1)
            {
                throw new CommandException("Expected args: file_path");
            }*/

            var genesisAddress = oldNexus.GetGenesisAddress(oldNexus.RootStorage);
            if (owner.Address != genesisAddress)
            {
                throw new CommandException("Invalid owner key");
            }

            var oldGenesisBlock = oldNexus.GetGenesisBlock();

            var newNexus = new Nexus();
            newNexus.CreateGenesisBlock(oldNexus.Name, owner, oldGenesisBlock.Timestamp);

            var oldRootChain = oldNexus.RootChain;
            var newRootChain = newNexus.RootChain;

            var height = oldRootChain.Height;
            BigInteger minFee = 0;
            Hash previousHash = Hash.Null;
            for (int i=1; i<=height; i++)
            {
                logger.Message($"Processing block {i} out of {height}");
                var oldBlockHash = oldRootChain.GetBlockHashAtHeight(i);
                var oldBlock = oldRootChain.GetBlockByHash(oldBlockHash);

                var transactions = oldBlock.TransactionHashes.Select(x => oldRootChain.GetTransactionByHash(x));

                try
                {
                    var newBlock = new Block(oldBlock.Height, oldBlock.ChainAddress, oldBlock.Timestamp, oldBlock.TransactionHashes, previousHash, oldBlock.Protocol, owner.Address, oldBlock.Payload);
                    var changeSet = newRootChain.ValidateBlock(newBlock, transactions, minFee);
                    newBlock.Sign(owner);
                    newRootChain.AddBlock(newBlock, transactions, minFee, changeSet);
                }
                catch (Exception e)
                {
                    throw new CommandException("Block validation failed: "+e.Message);
                }
            }

        }
    }
}
