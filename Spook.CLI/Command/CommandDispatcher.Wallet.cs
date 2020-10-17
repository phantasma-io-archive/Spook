using Phantasma.Spook.Modules;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using System;
using Phantasma.Blockchain;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("wallet balance", Category = "Wallet")]
        protected void OnWalletBalanceCommand(string[] args)
        {
            WalletModule.Balance(_cli.NexusAPI, _cli.Settings.Node.RestPort, _cli.NeoScanAPI, args);
        }

        [ConsoleCommand("wallet transfer", Category = "Wallet")]
        protected void OnWalletTransferCommand(string[] args)
        {
            BigInteger minFee = new BigInteger(_cli.Settings.Node.MinimumFee);
            WalletModule.Transfer(_cli.NexusAPI, minFee, _cli.NeoAPI, args);
        }

        [ConsoleCommand("wallet stake", Category = "Wallet")]
        protected void OnWalletStakeCommand(string[] args)
        {
            BigInteger minFee = new BigInteger(_cli.Settings.Node.MinimumFee);
            WalletModule.Stake(_cli.NexusAPI, minFee, args);
        }

        [ConsoleCommand("wallet airdrop", Category = "Wallet")]
        protected void OnWalletAirdropCommand(string[] args)
        {
            BigInteger minFee = new BigInteger(_cli.Settings.Node.MinimumFee);
            WalletModule.Airdrop(args, _cli.NexusAPI, minFee);
        }

        [ConsoleCommand("wallet migrate", Category = "Wallet", Description = "Migrate a validator wallet")]
        protected void OnWalletMigrateCommand(string[] args)
        {
            BigInteger minFee = new BigInteger(_cli.Settings.Node.MinimumFee);
            WalletModule.Migrate(args, _cli.NexusAPI, minFee);

            if (_cli.Mempool != null)
            {
                _cli.Mempool.SetKeys(WalletModule.Keys);
            }
        }

        [ConsoleCommand("wallet deploy", Category = "Wallet", Description = "Deploy a contract using a wallet")]
        protected void OnWalletDeployCommand(string[] args)
        {
            BigInteger minFee = new BigInteger(_cli.Settings.Node.MinimumFee);
            WalletModule.Deploy(args, _cli.NexusAPI, minFee);
        }

        [ConsoleCommand("wallet migrate", Category = "Wallet", Description = "Migrate a validator wallet")]
        protected void OnWalletRelayCommand(string[] args)
        {
            var script = Base16.Decode(args[0]);
            var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
            var tx = new Phantasma.Blockchain.Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, CLI.Identifier);
            tx.Sign(WalletModule.Keys);

            if (_cli.Mempool != null)
            {
                _cli.Mempool.Submit(tx);
            }
            else
            {
            throw new CommandException("no mempool available");
            }
        }
    }
}
