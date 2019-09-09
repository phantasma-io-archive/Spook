using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using LunarLabs.Parser.JSON;
using LunarLabs.Parser;
using Neo.Lux.Core;
using Neo.Lux.Cryptography;

namespace Phantasma.Spook.Swaps
{
    public class NeoInterop : ChainInterop
    {
        private RemoteRPCNode api;
        private KeyPair keys;

        public NeoInterop(TokenSwapper swapper, string baseWif, BigInteger blockHeight) : base(swapper, baseWif, blockHeight)
        {
            keys = KeyPair.FromWIF(this.WIF);
            api = new RemoteRPCNode("http://neoscan.io", "http://seed6.ngd.network:10332", "http://seed.neoeconomy.io:10332");
        }

        public override string LocalAddress => keys.address.ToString();
        public override string Name => "NEO";

        public override void Update(Action<IEnumerable<ChainSwap>> callback)
        {
            var result = new List<ChainSwap>();

            try
            {
                int maxPages = 1;
                string json;

                {
                    var url = $"https://api.neoscan.io/api/main_net/v1/get_address_abstracts/{LocalAddress}/1";

                    using (var wc = new System.Net.WebClient())
                    {
                        json = wc.DownloadString(url);
                    }

                    var root = JSONReader.ReadFromString(json);
                    maxPages = root.GetInt32("total_pages");
                }

                for (int page = maxPages; page>=1; page--)
                {
                    var url = $"https://api.neoscan.io/api/main_net/v1/get_address_abstracts/{LocalAddress}/{page}";

                    using (var wc = new System.Net.WebClient())
                    {
                        json = wc.DownloadString(url);
                    }

                    var root = JSONReader.ReadFromString(json);

                    var entries = root.GetNode("entries");

                    for (int i = entries.ChildCount-1; i>=0; i++)
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
            var destination = entry.GetString("address_to");
            if (destination != this.LocalAddress)
            {
                return;
            }

            var source = entry.GetString("address_from");
            var asset = entry.GetString("asset");
            var hash = entry.GetString("txid");
            var amount = entry.GetDecimal("amount");

            TokenInfo token;

            if (!Swapper.FindTokenByHash(asset, out token))
            {
                return;
            }

            var swap = new ChainSwap()
            {
                sourceHash = hash,
                sourceChain = this.Name,
                amount = amount,
                destinationAddress = destination,
                sourceAddress = source,
                symbol = token.symbol,
            };

            result.Add(swap);
        }

        public override string SendFunds(string address, TokenInfo token, decimal amount)
        {
            return ChainSwap.DummyHash;
        }

        public override string ReceiveFunds(string address, TokenInfo token, decimal amount)
        {
            Transaction tx;

            if (token.symbol == "NEO" || token.symbol == "GAS")
            {
                tx = api.SendAsset(keys, address, token.symbol, amount);
            }
            else
            {
                var nep5 = new NEP5(api, token.hash);
                tx = nep5.Transfer(keys, address, amount);
            }

            if (tx == null)
            {
                throw new InteropException(this.Name + " transfer failed");
            }

            return tx.Hash.ToString();
        }
    }
}
