using System;
using System.Collections.Generic;
using System.IO;

using Phantasma.CodeGen.Core;
using Phantasma.VM.Utils;
using Phantasma.VM;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Log;
using Phantasma.Spook.Command;
using Phantasma.Cryptography;
using Phantasma.API;
using Phantasma.Numerics;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;

namespace Phantasma.Spook.Modules
{
    [Module("sale")]
    public static class SaleModule
    {
        public static Logger logger => Spook.Logger;


        [ConsoleCommand("buyers", "sale", "Get list of buyers")]
        public static void GetBuyers(string[] args, Nexus nexus)
        {
            string hashStr = null;

            try
            {
                hashStr = args[0];
            }
            catch
            {
                throw new CommandException("Could not obtain sale hash");
            }

            Hash saleHash;
            if (!Hash.TryParse(hashStr, out saleHash))
            {
                throw new CommandException("Invalid sale hash");
            }

            var sale = (SaleInfo)nexus.RootChain.InvokeContract(nexus.RootChain.Storage, "sale", nameof(SaleContract.GetSale), saleHash).ToObject();

            var participants = (Address[])nexus.RootChain.InvokeContract(nexus.RootChain.Storage, "sale", nameof(SaleContract.GetSaleParticipants), saleHash).ToObject();

            var token = nexus.GetTokenInfo(nexus.RootStorage, sale.SellSymbol);

            if (participants.Length > 0)
            {
                logger.Message($"Found {participants.Length} addresses");
                foreach (var addr in participants)
                {
                    var amount = nexus.RootChain.InvokeContract(nexus.RootChain.Storage, "sale", nameof(SaleContract.GetPurchasedAmount), saleHash, addr).AsNumber();
                    logger.Message($"{addr} {UnitConversion.ToDecimal(amount, token.Decimals)} {sale.SellSymbol}");
                }
            }
            else
            {
                logger.Warning($"Not participants found.");
            }
        }

        [ConsoleCommand("finish", "sale", "Finishes a sale")]
        public static void Finish(string[] args, SpookSettings settings, NexusAPI api, BigInteger minFee)
        {
            string hashStr = null;

            try
            {
                hashStr = args[0];
            }
            catch
            {
                throw new CommandException("Could not obtain sale hash");
            }

            Hash saleHash;
            if (!Hash.TryParse(hashStr, out saleHash))
            {
                throw new CommandException("Invalid sale hash");
            }

            WalletModule.DoChecks(api);


            var script = ScriptUtils.BeginScript().
                AllowGas(WalletModule.Keys.Address, Address.Null, minFee, 9999).
                CallContract(Domain.NativeContractKind.Sale, nameof(SaleContract.CloseSale), WalletModule.Keys.Address, saleHash).
                SpendGas(WalletModule.Keys.Address).
                EndScript();

            WalletModule.ExecuteTransaction(settings, api, script, ProofOfWork.None, WalletModule.Keys);
        }

    }
}
