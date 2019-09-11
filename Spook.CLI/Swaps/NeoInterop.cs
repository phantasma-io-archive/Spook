using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using LunarLabs.Parser.JSON;
using LunarLabs.Parser;
using Phantasma.Neo.Core;
using Phantasma.Neo.Cryptography;
using Phantasma.Pay.Chains;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Spook.Swaps
{
    public class NeoInterop : ChainInterop
    {
        private RemoteRPCNode api;
        private NeoKey neoKeys;

        public NeoInterop(TokenSwapper swapper, Phantasma.Cryptography.KeyPair keys, BigInteger blockHeight) : base(swapper, keys, blockHeight)
        {
            this.neoKeys = new NeoKey(this.Keys.PrivateKey);
            api = new RemoteRPCNode("http://neoscan.io", "http://seed6.ngd.network:10332", "http://seed.neoeconomy.io:10332");
        }

        public override string LocalAddress => neoKeys.address.ToString();
        public override string Name => NeoWallet.NeoPlatform;
        public override string PrivateKey => Keys.ToWIF();

        public override void Update(Action<IEnumerable<ChainSwap>> callback)
        {
            var result = new List<ChainSwap>();

            try
            {
                int maxPages = 1;
                string json;

                {
                    var url = Swapper.neoscanAPI.GetRequestURL($"get_address_abstracts/{LocalAddress}/1");

                    using (var wc = new System.Net.WebClient())
                    {
                        json = wc.DownloadString(url);
                    }

                    var root = JSONReader.ReadFromString(json);
                    maxPages = root.GetInt32("total_pages");
                }

                for (int page = maxPages; page>=1; page--)
                {
                    var url = Swapper.neoscanAPI.GetRequestURL($"get_address_abstracts/{LocalAddress}/{page}");

                    using (var wc = new System.Net.WebClient())
                    {
                        json = wc.DownloadString(url);
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

                callback(result);
            }
            catch (Exception e)
            {
                throw e;
            }
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

            var temp = Swapper.FromLocalToExternal(sourceAddress, this.Name);
            destinationAddress = temp.Text;

            var swap = new ChainSwap()
            {
                sourceHash = hash,
                sourceChain = this.Name,
                sourceAddress = sourceAddress,
                amount = amount,
                destinationAddress = destinationAddress,
                destinationChain = destChain,
                symbol = token.Symbol,
            };

            result.Add(swap);
        }

        public override string SendFunds(string address, TokenInfo token, decimal amount)
        {
            return ChainSwap.DummyHash;
        }

        public override string ReceiveFunds(string sourceChain, Phantasma.Cryptography.Hash sourceHash, string address, TokenInfo token, decimal amount)
        {
            Transaction tx;

            if (token.Symbol == "NEO" || token.Symbol == "GAS")
            {
                tx = api.SendAsset(neoKeys, address, token.Symbol, amount);
            }
            else
            {
                var nep5 = new NEP5(api, token.Hash.ToString());
                tx = nep5.Transfer(neoKeys, address, amount);
            }

            if (tx == null)
            {
                throw new InteropException(this.Name + " transfer failed");
            }

            return tx.Hash.ToString();
        }
    }
}
