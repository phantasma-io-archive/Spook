using System;
using Neo.SmartContract;
using Phantasma.Numerics;
using Phantasma.Spook.Chains;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("neo deploy", Category = "Neo", Description="Deploys a contract to Neo")]
        protected void OnNeoDeployCommand(string[] args)
        {
            if (args.Length != 2)
            {
            throw new CommandException("Expected: WIF avm_path");
            }
            var avmPath = args[1];
            if (!System.IO.File.Exists(avmPath))
            {
            throw new CommandException("path for avm not found");
            }

            var keys = Neo.Core.NeoKeys.FromWIF(args[0]);
            var script = System.IO.File.ReadAllBytes(avmPath);
            var scriptHash = script.ToScriptHash();
            Console.WriteLine("Deploying contract " + scriptHash);

            try
            {
            var tx = _cli.NeoAPI.DeployContract(keys, script, Base16.Decode("0710"), 0x05, ContractProperties.HasStorage | ContractProperties.Payable, "Contract", "1.0", "Author", "email@gmail.com", "Description");
            Console.WriteLine("Deployed contract via transaction: " + tx.Hash);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to deploy contract: " + e.Message);
            }
        }
    }
}
