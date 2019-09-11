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
            Keys = KeyPair.FromWIF(wif);

            logger.Success($"Opened wallet with address {Keys.Address}.");
        }

        public static void Balance(NexusAPI api, Logger logger, string[] args)
        {
            Address address;

            if (args.Length == 1)
            {
                address = Address.FromText(args[0]);
            }
            else
            {
                address = Keys.Address;
            }

            var account = (AccountResult) api.GetAccount(address.Text);

            logger.Message($"Balance for {account.name} ({address.Text})");
            if (account.balances.Any())
            {
                foreach (var entry in account.balances)
                {
                    var amount = BigInteger.Parse(entry.amount);
                    logger.Success($"{entry.chain} => {UnitConversion.ToDecimal(amount, (int)entry.decimals)} {entry.symbol}");
                }
            }
            else
            {
                logger.Warning("Empty wallet.");
            }
        }

        public static void Transfer(NexusAPI api, Logger logger, string[] args)
        {
            if (args.Length != 3)
            {
                throw new CommandException("Expected args: target_address amount symbol");
            }

            // TODO more arg validation
            var dest = Address.FromText(args[0]);

            if (dest.Text == Keys.Address.Text)
            {
                throw new CommandException("Cannot transfer to same address");
            }

            var tempAmount = decimal.Parse(args[1]);
            var tokenSymbol = args[2];

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

            var script = ScriptUtils.BeginScript().
                AllowGas(Keys.Address, Address.Null, 1, 9999).
                CallContract("token", "TransferTokens", Keys.Address, dest, tokenSymbol, amount).
                SpendGas(Keys.Address).
                EndScript();
            var tx = new Transaction(api.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
            tx.Sign(Keys);
            var rawTx = tx.ToByteArray(true);

            logger.Message($"Sending {tempAmount} {tokenSymbol} to {dest.Text}...");
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
            var tokenSymbol = Nexus.StakingTokenSymbol;

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
            var tx = new Transaction(api.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
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
