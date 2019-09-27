using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using LunarLabs.Parser.JSON;
using LunarLabs.Parser;
using Phantasma.Neo.Core;
using Phantasma.Pay.Chains;
using Phantasma.Blockchain.Tokens;
using Phantasma.Domain;

namespace Phantasma.Spook.Swaps
{
    public class NeoInterop : ChainInterop
    {
        private NeoAPI api;
        private NeoKey neoKeys;

        public NeoInterop(TokenSwapper swapper, Phantasma.Cryptography.KeyPair keys, BigInteger blockHeight, NeoAPI neoAPI) : base(swapper, keys, blockHeight)
        {
            this.neoKeys = new NeoKey(this.Keys.PrivateKey);
            this.api = neoAPI;
        }

        public override string LocalAddress => neoKeys.address.ToString();
        public override string Name => NeoWallet.NeoPlatform;
        public override string PrivateKey => Keys.ToWIF();

        public override IEnumerable<ChainSwap>  Update()
        {
            var result = new List<ChainSwap>();

            int maxPages = 1;
            {
                var json = Swapper.neoscanAPI.ExecuteRequest($"get_address_abstracts/{LocalAddress}/1");
                if (json == null)
                {
                    throw new SwapException("failed to fetch address page");
                }

                var root = JSONReader.ReadFromString(json);
                maxPages = root.GetInt32("total_pages");
            }

            for (int page = maxPages; page>=1; page--)
            {
                var json = Swapper.neoscanAPI.ExecuteRequest($"get_address_abstracts/{LocalAddress}/{page}");
                if (json == null)
                {
                    throw new SwapException("failed to fetch address page");
                }

                var root = JSONReader.ReadFromString(json);

                var entries = root.GetNode("entries");

                for (int i = entries.ChildCount-1; i>=0; i--)
                {
                    var entry = entries.GetNodeByIndex(i);

                    var temp = entry.GetString("block_height");
                    var height = BigInteger.Parse(temp);

                    if (height > currentHeight)
                    {
                        currentHeight = height;

                        ProcessTransaction(entry, result);
                    }
                }
            }

            return result;
        }

        private void ProcessTransaction(DataNode entry, List<ChainSwap> result)
        {
            var destinationAddress = entry.GetString("address_to");
            if (destinationAddress != this.LocalAddress)
            {
                return;
            }

            var sourceAddress = entry.GetString("address_from");
            var asset = entry.GetString("asset");
            var hash = entry.GetString("txid");
            var amount = entry.GetDecimal("amount");

            TokenInfo token;

            if (!Swapper.FindTokenByHash(asset, out token))
            {
                return;
            }

            var destChain = "phantasma";
            var interop = Swapper.FindInterop(destChain);

            destinationAddress = Swapper.FromLocalToExternal(sourceAddress, this.Name);            

            var swap = new ChainSwap()
            {
                sourceHash = hash,
                sourcePlatform = this.Name,
                sourceAddress = sourceAddress,
                amount = amount,
                destinationAddress = destinationAddress,
                destinationPlatform = destChain,
                symbol = token.Symbol,
                status = ChainSwapStatus.Pending,
            };

            result.Add(swap);
        }

        public override string ReceiveFunds(ChainSwap swap)
        {
            Transaction tx;

            if (swap.symbol == "NEO" || swap.symbol == "GAS")
            {
                tx = api.SendAsset(neoKeys, swap.destinationAddress, swap.symbol, swap.amount);
            }
            else
            {
                TokenInfo token;
                if (!Swapper.FindTokenBySymbol(swap.symbol, out token))
                {
                    return null;
                }

                var scriptHash = token.Hash.ToString().Substring(0, 40);

                var nep5 = new NEP5(api, scriptHash);
                tx = nep5.Transfer(neoKeys, swap.destinationAddress, swap.amount);
            }

            if (tx == null)
            {
                throw new InteropException(this.Name + " transfer failed", ChainSwapStatus.Receive);
            }

            return tx.Hash.ToString();
        }
    }
}
