using System;
using System.Collections.Generic;
using LunarLabs.Parser.JSON;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Domain;
using Phantasma.Neo.Core;

namespace Phantasma.Spook.Oracles
{
    public class NeoScanAPI
    {
        public readonly string URL;
        public readonly Logger logger;

        private readonly Address platformAddress;
        private static readonly string platformName = NeoWallet.NeoPlatform;

        private readonly Nexus nexus;
        private NeoAPI neoAPI;

        public NeoScanAPI(string url, Logger logger, Nexus nexus, NeoAPI neoAPI, PhantasmaKeys keys)
        {
            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }

            this.URL = url;
            this.logger = logger;
            this.nexus = nexus;
            this.neoAPI = neoAPI;

            var key = InteropUtils.GenerateInteropKeys(keys, platformName);
            this.platformAddress = key.Address;
        }

        public string ExecuteRequest(string request)
        {
            Throw.If(request.StartsWith("/"), "request malformed");
            var url = $"{URL}/api/main_net/v1/{request}";

            try
            {
                string json;
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                    return json;
                }
            }
            catch (Exception e)
            {
                logger.Error("Neoscan request failed: " + url);
                return null;
            }
        }

        public InteropTransaction ReadTransaction(Hash hash)
        {
            var hashText = hash.ToString();

            var json = ExecuteRequest($"get_transaction/{hashText}");
            if (json == null)
            {
                throw new OracleException("Network read failure");
            }

            try
            {
                var root = JSONReader.ReadFromString(json);

                var vins = root.GetNode("vin");
                Throw.IfNull(vins, nameof(vins));

                string inputSource = null;

                foreach (var input in vins.Children)
                {
                    var addrText = input.GetString("address_hash");
                    if (inputSource == null)
                    {
                        inputSource = addrText;
                        break;
                    }
                    else
                    if (inputSource != addrText)
                    {
                        throw new OracleException("transaction with multiple input sources, unsupported for now");
                    }
                }

                return FillTransaction(hashText, inputSource);
            }
            catch (Exception e)
            {
                throw new OracleException(e.Message);
            }
        }

        private InteropTransaction FillTransaction(string hashText, string inputAddress)
        {
            int page = 1;
            int maxPages = 9999;

            while (page <= maxPages)
            {
                var json = ExecuteRequest($"get_address_abstracts/{inputAddress}/{page}");
                if (json == null)
                {
                    throw new OracleException("Network read failure");
                }

                var root = JSONReader.ReadFromString(json);
                var entries = root.GetNode("entries");

                for (int i = 0; i < entries.ChildCount; i++)
                {
                    var entry = entries.GetNodeByIndex(i);
                    var txId = entry.GetString("txid");
                    if (hashText.Equals(txId, StringComparison.OrdinalIgnoreCase))
                    {
                        var inputAsset = entry.GetString("asset");
                        var symbol = FindSymbolFromAsset(inputAsset);

                        if (symbol == null)
                        {
                            throw new OracleException("transaction contains unknown asset: " + inputAsset);
                        }

                        var inputAmount = entry.GetDecimal("amount");

                        var sourceAddress = entry.GetString("address_from");
                        var destAddress = entry.GetString("address_to");

                        var neoTx = neoAPI.GetTransaction(hashText);

                        var interopAddress = neoTx.ExtractInteropAddress();

                        var info = nexus.GetTokenInfo(symbol);
                        var amount = UnitConversion.ToBigInteger(inputAmount, info.Decimals);

                        var txHash = Hash.Parse(hashText);
                        var tx = new InteropTransaction(txHash, new InteropTransfer[]{
                            new InteropTransfer("neo", NeoWallet.EncodeAddress(sourceAddress), "neo", NeoWallet.EncodeAddress(destAddress), interopAddress, symbol, amount)
                        });
                        return tx;
                    }
                }

                page++;
            }

            throw new Exception("could not fill oracle transaction: " + hashText);
        }

        private string FindSymbolFromAsset(string assetID)
        {
            switch (assetID)
            {
                case "ed07cffad18f1308db51920d99a2af60ac66a7b3": return "SOUL";
                case "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b": return "NEO";
                case "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7": return "GAS";
                default: return null;
            }
        }

        public InteropBlock ReadBlock(Hash hash)
        {
            var blockText = hash.ToString();

            if (blockText.StartsWith("0x"))
            {
                blockText = blockText.Substring(2);
            }

            var json = ExecuteRequest($"get_block/{blockText}");
            if (json == null)
            {
                throw new OracleException("Network read failure");
            }

            try
            {
                var root = JSONReader.ReadFromString(json);

                var transactions = root.GetNode("transactions");
                var hashes = new List<Hash>();

                foreach (var entry in transactions.Children)
                {
                    var txHash = Hash.Parse(entry.Value);
                    hashes.Add(txHash);
                }

                var block = new InteropBlock(platformName, "main", Hash.Parse(blockText), hashes.ToArray());
                return block;
            }
            catch (Exception e)
            {
                throw new OracleException(e.Message);
            }
        }
    }
}
