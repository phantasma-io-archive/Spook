using System;
using Phantasma.Cryptography;
using Phantasma.Neo.Utils;
using Phantasma.Pay.Chains;
using Phantasma.Blockchain.Contracts;
using Phantasma.Spook.Modules;
using Phantasma.VM.Utils;
using Phantasma.Contracts.Native;

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
                _cli.Logger.Message($"Height {args[2]} is set for platform {args[0]}, chain {args[1]}");
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
            var script = new VM.Utils.ScriptBuilder()
                .AllowGas(_cli.NodeKeys.Address, Address.Null, minimumFee, 500)
                .CallContract("interop", nameof(InteropContract.RegisterAddress), _cli.NodeKeys.Address, platform, localAddress, externalAddress)
                .SpendGas(_cli.NodeKeys.Address).ToScript();

            WalletModule.ExecuteTransaction(_cli.NexusAPI, script, ProofOfWork.None, _cli.NodeKeys);
        }

    }
}
