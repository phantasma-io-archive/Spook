using LunarLabs.Parser.JSON;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using Phantasma.Pay.Chains;

namespace Phantasma.Spook.Oracles
{
    public class NeoScanUtils
    {
        public static byte[] ReadOracle(string[] input)
        {
            if (input == null || input.Length != 2)
            {
                throw new OracleException("missing oracle input");
            }

            var cmd = input[0].ToLower();
            switch (cmd)
            {
                case "tx":
                    return NeoScanUtils.ReadTransaction(input[1]);

                case "block":
                    return NeoScanUtils.ReadBlock(input[1]);

                default:
                    throw new OracleException("unknown neo oracle");
            }
        }

        private static byte[] PackEvent(object content)
        {
            var bytes = content == null ? new byte[0] : Serialization.Serialize(content);
            return bytes;
        }

        public static byte[] ReadTransaction(string hashText)
        {
            var url = $"https://api.neoscan.io/api/main_net/v1/get_transaction/{hashText}";

            string json;

            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                }

                var chainName = "NEO";
                var chainAddress = InteropUtils.GetInteropAddress(chainName);

                var tx = new InteropTransaction();
                tx.ChainName = chainName;
                tx.ChainAddress = chainAddress;
                tx.Hash = Hash.Parse(hashText);

                var root = JSONReader.ReadFromString(json);

                var eventList = new List<Event>();

                var vins = root.GetNode("vins");

                string inputAsset = null;
                string inputSource = null;

                BigInteger inputAmount = 0;

                foreach (var input in vins.Children)
                {
                    var addrText = input.GetString("address_hash");
                    if (inputSource == null)
                    {
                        inputSource = addrText;
                    }
                    else
                    if (inputSource != addrText)
                    {
                        throw new OracleException("transaction with multiple input sources, unsupported for now");
                    }

                    var assetSymbol = input.GetString("asset");

                    if (inputAsset == null)
                    {
                        inputAsset = assetSymbol;
                    }
                    else
                    if (inputAsset != assetSymbol)
                    {
                        throw new OracleException("transaction with multiple input assets, unsupported for now");
                    }
                }

                if (inputAsset == null || inputSource == null || inputAmount <= 0)
                {
                    throw new OracleException("transaction with invalid inputs, something failed");
                }

                var vouts = root.GetNode("vouts");
                foreach (var output in vouts.Children)
                {
                    var addrText = output.GetString("address_hash");

                    var assetSymbol = output.GetString("asset");
                    var destination = NeoWallet.DecodeAddress(addrText);
                    var value = output.GetFloat("value");
                    value *= (float)Math.Pow(10, 8);
                    var amount = new BigInteger((long)value);

                    if (addrText == inputSource)
                    {
                        inputAmount -= amount;
                        continue;
                    }

                    var evt = new Event(EventKind.TokenReceive, destination, PackEvent(new TokenEventData() { chainAddress = chainAddress, value = amount, symbol = assetSymbol }));
                    eventList.Add(evt);
                }

                var source = NeoWallet.DecodeAddress(inputSource);
                var sendEvt = new Event(EventKind.TokenSend, source, PackEvent(new TokenEventData() { chainAddress = chainAddress, value = inputAmount, symbol = inputAsset }));
                eventList.Add(sendEvt);

                tx.Events = eventList.ToArray();
                return Serialization.Serialize(tx);
            }
            catch (Exception e)
            {
                throw new OracleException(e.Message);
            }
        }

        public static byte[] ReadBlock(string blockText)
        {
            var url = $"https://api.neoscan.io/api/main_net/v1/get_block/{blockText}";

            string json;

            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                }

                var chainName = "NEO";
                var chainAddress = InteropUtils.GetInteropAddress(chainName);

                var block = new InteropBlock();
                block.ChainName = chainName;
                block.ChainAddress = chainAddress;
                block.Hash = Hash.Parse(blockText);

                var root = JSONReader.ReadFromString(json);

                var transactions = root.GetNode("transactions");
                var hashes = new List<Hash>();

                foreach (var entry in transactions.Children)
                {
                    var hash = Hash.Parse(entry.Value);
                    hashes.Add(hash);
                }

                block.Transactions = hashes.ToArray();
                return Serialization.Serialize(block);
            }
            catch (Exception e)
            {
                throw new OracleException(e.Message);
            }
        }
    }
}
