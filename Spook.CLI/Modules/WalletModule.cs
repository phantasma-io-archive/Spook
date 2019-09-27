using System;
using System.Collections.Generic;
using Phantasma.VM.Utils;
using Phantasma.Cryptography;
using Phantasma.Blockchain;
using Phantasma.API;
using Phantasma.Core.Log;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using System.Threading;
using Phantasma.Pay.Chains;
using Phantasma.Pay;
using Phantasma.Neo.Core;
using Phantasma.Spook.Oracles;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;

namespace Phantasma.Spook.Modules
{
    [Module("wallet")]
    public static class WalletModule
    {
        public static KeyPair Keys;

        public static void Open(Logger logger, string[] args)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Expected args: WIF");
            }

            var wif = args[0];

            try
            {
                Keys = KeyPair.FromWIF(wif);
                logger.Success($"Opened wallet with address: {Keys.Address}");
            }
            catch (Exception e)
            {
                logger.Error($"Failed to open wallet. Make sure you valid a correct WIF key.");
            }

        }

        public static void Create(Logger logger, string[] args)
        {
            if (args.Length != 0)
            {
                throw new CommandException("Unexpected args");
            }

            try
            {
                Keys = KeyPair.Generate();
                logger.Success($"Generate wallet with address: {Keys.Address}");
                logger.Success($"WIF: {Keys.ToWIF()}");
                logger.Warning($"Save this wallet WIF, it won't be displayed again and it is necessary to access the wallet!");
            }
            catch (Exception e)
            {
                logger.Error($"Failed to create a new wallet.");
            }

        }

        public static void Balance(NexusAPI api, Logger logger, int phantasmaRestPort, NeoScanAPI neoScanAPI, string[] args)
        {
            if (phantasmaRestPort <= 0)
            {
                throw new CommandException("Please enable REST API on this node to use this feature");
            }

            Address address;

            if (args.Length == 1)
            {
                address = Address.FromText(args[0]);
            }
            else
            {
                address = Keys.Address;
            }

            logger.Message("Fetching balances...");
            var wallets = new List<CryptoWallet>();
            wallets.Add(new PhantasmaWallet(Keys, $"http://localhost:{phantasmaRestPort}/api"));
            wallets.Add(new NeoWallet(Keys, neoScanAPI.URL));

            foreach (var wallet in wallets)
            {
                wallet.SyncBalances((success) =>
                {
                    var temp = wallet.Name != wallet.Address ? $" ({wallet.Address})":"";
                    logger.Message($"{wallet.Platform} balance for {wallet.Name}"+temp);
                    if (success)
                    {
                        bool empty = true;
                        foreach (var entry in wallet.Balances)
                        {
                            empty = false;
                            logger.Message($"{entry.Amount} {entry.Symbol} @ {entry.Chain}");
                        }
                        if (empty)
                        {
                            logger.Warning("Empty wallet.");
                        }
                    }
                    else
                    {
                        logger.Warning("Failed to fetch balances!");
                    }
                });
            }
        }

        private static void NeoTransfer(NeoKey neoKeys, string toAddress, string tokenSymbol, decimal tempAmount, NeoAPI neoAPI, Logger logger)
        {
            Neo.Core.Transaction neoTx;

            logger.Message($"Sending {tempAmount} {tokenSymbol} to {toAddress}...");

            if (tokenSymbol == "NEO" || tokenSymbol == "GAS")
            {
                neoTx = neoAPI.SendAsset(neoKeys, toAddress, tokenSymbol, tempAmount);
            }
            else
            {
                var nep5 = neoAPI.GetToken(tokenSymbol);
                if (nep5 == null)
                {
                    throw new CommandException($"Could not find interface for NEP5: {tokenSymbol}");
                }
                neoTx = nep5.Transfer(neoKeys, toAddress, tempAmount);
            }

            logger.Success($"Sent transaction with hash {neoTx.Hash}!");
        }

        public static void Transfer(NexusAPI api, Logger logger, NeoAPI neoAPI, string[] args)
        {
            if (args.Length != 4)
            {
                throw new CommandException("Expected args: source_address target_address amount symbol");
            }

            var tempAmount = decimal.Parse(args[2]);
            var tokenSymbol = args[3];

            TokenResult tokenInfo;
            try
            {
                var result = api.GetToken(tokenSymbol);
                tokenInfo = (TokenResult)result;
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            if (!tokenInfo.flags.Contains("Fungible"))
            {
                throw new CommandException("Token must be fungible!");
            }

            var amount = UnitConversion.ToBigInteger(tempAmount, tokenInfo.decimals);

            var sourceName = args[0];
            Address sourceAddress;

            if (Address.IsValidAddress(sourceName))
            {
                sourceAddress = Address.FromText(sourceName);
                if (sourceName != Keys.Address.Text)
                {
                    throw new CommandException("The current open wallet does not have keys that match address "+sourceName);
                }
            }
            else
            if (NeoWallet.IsValidAddress(sourceName))
            {
                sourceAddress = NeoWallet.EncodeAddress(sourceName);
            }

            var destName = args[1];
            Address destAddress;

            if (Address.IsValidAddress(destName))
            {
                destAddress = Address.FromText(destName);
            }
            else
            if (NeoWallet.IsValidAddress(destName))
            {
                destAddress = NeoWallet.EncodeAddress(destName);
            }

            if (destAddress.Text == sourceAddress.Text)
            {
                throw new CommandException("Cannot transfer to same address");
            }

            if (sourceAddress.IsInterop)
            {
                string sourcePlatform;
                string fromAddress;

                WalletUtils.DecodePlatformAndAddress(sourceAddress, out sourcePlatform, out fromAddress);

                if (destAddress.IsInterop)
                {
                    string destPlatform;
                    string toAddress;

                    WalletUtils.DecodePlatformAndAddress(destAddress, out destPlatform, out toAddress);

                    if (sourcePlatform != destPlatform)
                    {
                        throw new CommandException($"Cannot transfer directly from {sourcePlatform} to {destPlatform}");
                    }
                    else
                    {
                        switch (destPlatform)
                        {
                            case NeoWallet.NeoPlatform:
                                {
                                    var neoKeys = new NeoKey(Keys.PrivateKey);
                                    NeoTransfer(neoKeys, toAddress, tokenSymbol, tempAmount, neoAPI, logger);
                                }
                                break;

                            default:
                                throw new CommandException($"Not implemented yet :(");
                        }
                    }
                }
                else
                {
                    logger.Warning($"Source is {sourcePlatform} address, a swap will be performed using an interop address.");

                    //api.GetAccount(sourceAddress);
                    throw new CommandException($"Not implemented yet :(");
                }

                return;
            }
            else
            if (destAddress.IsInterop)
            {
                string platformName;
                string outAddress;

                WalletUtils.DecodePlatformAndAddress(destAddress, out platformName, out outAddress);
                logger.Warning($"Target is {platformName} address, a swap will be performed through interop address {destAddress}.");
            }

            var script = ScriptUtils.BeginScript().
                AllowGas(Keys.Address, Address.Null, 1, 9999).
                CallContract("token", "TransferTokens", Keys.Address, destAddress, tokenSymbol, amount).
                SpendGas(Keys.Address).
                EndScript();
            var tx = new Phantasma.Blockchain.Transaction(api.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
            tx.Sign(Keys);
            var rawTx = tx.ToByteArray(true);

            logger.Message($"Sending {tempAmount} {tokenSymbol} to {destAddress.Text}...");
            try
            {
                api.SendRawTransaction(Base16.Encode(rawTx));
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            Thread.Sleep(3000);
            var hash = tx.Hash.ToString();
            do
            {
                try
                {
                    var result = api.GetTransaction(hash);
                }
                catch (Exception e)
                {
                    throw new CommandException(e.Message);
                }
                /*if (result is ErrorResult)
                {
                    var temp = (ErrorResult)result;
                    if (temp.error.Contains("pending"))
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        throw new CommandException(temp.error);
                    }
                }
                else*/
                {
                    break;
                }
            } while (true);
            logger.Success($"Sent transaction with hash {hash}!");
        }

        public static void Stake(NexusAPI api, Logger logger, string[] args)
        {
            if (args.Length != 3)
            {
                throw new CommandException("Expected args: target_address amount");
            }

            // TODO more arg validation
            var dest = Address.FromText(args[0]);

            if (dest.Text == Keys.Address.Text)
            {
                throw new CommandException("Cannot transfer to same address");
            }

            var tempAmount = decimal.Parse(args[1]);
            var tokenSymbol = DomainSettings.StakingTokenSymbol;

            TokenResult tokenInfo;
            try
            {
                var result = api.GetToken(tokenSymbol);
                tokenInfo = (TokenResult)result;
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            var amount = UnitConversion.ToBigInteger(tempAmount, tokenInfo.decimals);

            var script = ScriptUtils.BeginScript().
                AllowGas(Keys.Address, Address.Null, 1, 9999).
                CallContract("energy", "Stake", Keys.Address, dest, amount).
                SpendGas(Keys.Address).
                EndScript();
            var tx = new Phantasma.Blockchain.Transaction(api.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
            tx.Sign(Keys);
            var rawTx = tx.ToByteArray(true);

            logger.Message($"Staking {tempAmount} {tokenSymbol} with {dest.Text}...");
            try
            {
                api.SendRawTransaction(Base16.Encode(rawTx));
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            Thread.Sleep(3000);
            var hash = tx.Hash.ToString();
            do
            {
                try
                {
                    var result = api.GetTransaction(hash);
                }
                catch (Exception e)
                {
                    throw new CommandException(e.Message);
                }
                /*if (result is ErrorResult)
                {
                    var temp = (ErrorResult)result;
                    if (temp.error.Contains("pending"))
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        throw new CommandException(temp.error);
                    }
                }
                else*/
                {
                    break;
                }
            } while (true);
            logger.Success($"Sent transaction with hash {hash}!");
        }
    }
}
