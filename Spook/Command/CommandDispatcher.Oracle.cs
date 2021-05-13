using System;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;
using Phantasma.Blockchain.Contracts;
using Phantasma.VM.Utils;
using Phantasma.Core.Types;
using Phantasma.Blockchain;
using Phantasma.Storage.Context;
using Phantasma.Spook.Oracles;
using Phantasma.Domain;
using System.Text;
using System.IO;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {

        [ConsoleCommand("oracle get price", Category = "Oracle", Description = "Get current token price from an oracle")]
        protected void OnOracleGetPriceCommand(string[] args)
        {
            var apiKey = _cli.CryptoCompareAPIKey;
            var pricerCGEnabled = _cli.Settings.Oracle.PricerCoinGeckoEnabled;
            var pricerSupportedTokens = _cli.Settings.Oracle.PricerSupportedTokens.ToArray();


            Console.WriteLine($"Supported tokens:");
            Console.WriteLine($"---------------------------");

            foreach (var token in pricerSupportedTokens) {
                Console.WriteLine($"{token.ticker}: {token.cryptocompareId}: {token.coingeckoId}");
            }
            Console.WriteLine($"---------------------------");

            if(pricerCGEnabled) { 
                var cgprice = CoinGeckoUtils.GetCoinRate(args[0], DomainSettings.FiatTokenSymbol, pricerSupportedTokens, Spook.Logger);
                Console.WriteLine($"Oracle Coingecko Price for token {args[0]} is: {cgprice}");
            }
            var price = CryptoCompareUtils.GetCoinRate(args[0], DomainSettings.FiatTokenSymbol, apiKey, pricerSupportedTokens, Spook.Logger);
            Console.WriteLine($"Oracle CryptoCompare Price for token {args[0]} is: {price}");

            var gprice = Pricer.GetCoinRate(args[0], DomainSettings.FiatTokenSymbol, apiKey, pricerCGEnabled, pricerSupportedTokens, Spook.Logger);
            Console.WriteLine($"Oracle Global Price for token {args[0]} is: {gprice}");
        }

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
                Spook.Logger.Message($"Height {args[2]} is set for platform {args[0]}, chain {args[1]}");
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
                Console.WriteLine("Platform and one or more block heights needed!");
            }

            var platform = args.ElementAtOrDefault(0);

            // start at index 1, 0 is platform
            for (var i = 1; i < args.Count(); i++)
            {
                var blockId = args.ElementAtOrDefault(1);

                if (string.IsNullOrEmpty(blockId))
                {
                    continue;
                }

                _cli.TokenSwapper.ResyncBlockOnChain(platform, blockId);
            }
        }

        [ConsoleCommand("export inprogress", Category = "Oracle", Description = "resync certain blocks on a psecific platform")]
        protected void onExportInProgress(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("File path needs to be given!");
            }

            var filePath = args[0];

            var InProgressTag = ".inprogress";
            var storage = new KeyStoreStorage(_cli.Nexus.CreateKeyStoreAdapter("swaps"));
            var inProgressMap = new StorageMap(InProgressTag, storage);
            var csv = new StringBuilder();

            inProgressMap.Visit<Hash, string>((key, value) => {
                var line = $"{key.ToString()},{value}";
                csv.AppendLine(line);
            });

            System.IO.File.WriteAllText(filePath, csv.ToString());
        }

        [ConsoleCommand("import inprogress", Category = "Oracle", Description = "resync certain blocks on a psecific platform")]
        protected void onImportInProgress(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("File path needs to be given!");
            }
            var InProgressTag = ".inprogress";
            var storage = new KeyStoreStorage(_cli.Nexus.CreateKeyStoreAdapter("swaps"));
            var inProgressMap = new StorageMap(InProgressTag, storage);

            var filePath = args[0];
            using(var reader = new StreamReader(filePath))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    var hash = Hash.FromString(values[0]);
                    if (!inProgressMap.ContainsKey<Hash>(hash))
                    {
                        inProgressMap.Set<Hash,string>(Hash.FromString(values[0]), values[1]);
                    }
                }
            }
        }

        [ConsoleCommand("remove swap", Category = "Oracle", Description = "resync certain blocks on a psecific platform")]
        protected void onRemoveSwap(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Only source hahs necessary");
            }

            var sourceHashStr = args.ElementAtOrDefault(0);

            if (string.IsNullOrEmpty(sourceHashStr))
            {
                Console.WriteLine("SourceHash is null or empty!");
            }

            var sourceHash = Hash.Parse(sourceHashStr);

            var InProgressTag = ".inprogress";
            var storage = new KeyStoreStorage(_cli.Nexus.CreateKeyStoreAdapter("swaps"));
            var inProgressMap = new StorageMap(InProgressTag, storage);

            if (inProgressMap.ContainsKey(sourceHash))
            {
                inProgressMap.Remove(sourceHash);
                Console.WriteLine($"SourceHash {sourceHash} has been removed from in progress swaps");
            }
            else
            {
                Console.WriteLine($"Swap with sourceHash {sourceHash} not in progress");
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
            var script = ScriptUtils.BeginScript()
                .AllowGas(_cli.NodeKeys.Address, Address.Null, minimumFee, 1500)
                .CallContract("interop", nameof(InteropContract.RegisterAddress), _cli.NodeKeys.Address, platform, localAddress, externalAddress)
                .SpendGas(_cli.NodeKeys.Address).EndScript();

            var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
            var tx = new Phantasma.Blockchain.Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, Spook.TxIdentifier);

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
            Spook.Logger.Message($"Added address {externalAddress} to {platform}");
        }
    }
}
