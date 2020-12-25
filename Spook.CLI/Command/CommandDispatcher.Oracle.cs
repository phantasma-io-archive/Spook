using System;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;
using Phantasma.Blockchain.Contracts;
using Phantasma.VM.Utils;
using Phantasma.Core.Types;
using System.Linq;
using Phantasma.Blockchain;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("oracle read", Category = "Oracle", Description="Read a transaction from an oracle")]
        protected void OnOracleReadCommand(string[] args)
        {
            // currently neo only, revisit for eth 
            var hash = Hash.Parse(args[0]);
            var reader = _cli.Nexus.GetOracleReader();
            var tx = reader.ReadTransaction("neo", "neo", hash);

            // not sure if that's exactly what we want, probably needs more output...
            Console.WriteLine(tx.Transfers[0].interopAddress.Text);
        }

        [ConsoleCommand("platform height get", Category = "Oracle", Description = "Get platform height")]
        protected void OnPlatformHeightGet(string[] args)
        {
            var reader = _cli.Nexus.GetOracleReader();

            Console.WriteLine($"Platform {args[0]} [chain {args[1]}] current height: {reader.GetCurrentHeight(args[0], args[1])}");
        }

        [ConsoleCommand("platform height set", Category = "Oracle", Description = "Set platform height")]
        protected void OnPlatformHeightSet(string[] args)
        {
            Console.WriteLine($"Setting platform {args[0]} [chain {args[1]}] height {args[2]} ()...");
            lock (String.Intern("PendingSetCurrentHeight_" + args[0]))
            {
                var reader = _cli.Nexus.GetOracleReader();
                reader.SetCurrentHeight(args[0], args[1], args[2]);

                Console.WriteLine($"Height {args[2]} is set for platform {args[0]}, chain {args[1]}");
                CLI.Logger.Message($"Height {args[2]} is set for platform {args[0]}, chain {args[1]}");
            }
        }

        [ConsoleCommand("platform address list", Category = "Oracle", Description = "Get list of swap addresses for platform")]
        protected void OnPlatformAddressList(string[] args)
        {
            var platform = _cli.Nexus.GetPlatformInfo(_cli.Nexus.RootStorage, args[0]);

            for (int i=0; i<platform.InteropAddresses.Length; i++)
            {
                var entry = platform.InteropAddresses[i];
                Console.WriteLine($"#{i} => {entry.LocalAddress} / {entry.ExternalAddress}");
            }
        }

        [ConsoleCommand("resync block", Category = "Oracle", Description = "resync certain blocks on a psecific platform")]
        protected void OnResyncBlock(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Platform and block height needed!");
            }

            var platform = args.ElementAtOrDefault(0);
            var blockId = args.ElementAtOrDefault(1);

            _cli.TokenSwapper.ResyncBlockOnChain(platform, blockId);

            Console.WriteLine($"Enqueued height {blockId} on chain {platform} to resync.");
        }



        [ConsoleCommand("platform address add", Category = "Oracle", Description = "Add swap address to platform")]
        protected void OnPlatformAddressAdd(string[] args)
        {
            var platform = args[0];
            var externalAddress = args[1];

            Address localAddress;

            switch (platform)
            {
                case NeoWallet.NeoPlatform:
                    localAddress = NeoWallet.EncodeAddress(externalAddress);
                    break;

                case EthereumWallet.EthereumPlatform:
                    localAddress = EthereumWallet.EncodeAddress(externalAddress);
                    break;

                default:
                    throw new Exception("Unknown platform: " + platform);
            }

            var minimumFee = _cli.Settings.Node.MinimumFee;
            var script = ScriptUtils.BeginScript()
                .AllowGas(_cli.NodeKeys.Address, Address.Null, minimumFee, 1500)
                .CallContract("interop", nameof(InteropContract.RegisterAddress), _cli.NodeKeys.Address, platform, localAddress, externalAddress)
                .SpendGas(_cli.NodeKeys.Address).EndScript();

            var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
            var tx = new Phantasma.Blockchain.Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, CLI.Identifier);

            tx.Mine((int)ProofOfWork.Minimal);
            tx.Sign(_cli.NodeKeys);

            if (_cli.Mempool != null)
            {
                _cli.Mempool.Submit(tx);
                Console.WriteLine($"Transaction {tx.Hash} submitted to mempool.");
            }
            else
            {
                Console.WriteLine("No mempool available");
                return;
            }
            Console.WriteLine($"Added address {externalAddress} to {platform}");
            CLI.Logger.Message($"Added address {externalAddress} to {platform}");
        }
    }
}
