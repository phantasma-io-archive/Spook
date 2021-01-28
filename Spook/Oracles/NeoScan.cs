using System;
using System.Collections.Generic;
using System.Text;

using LunarLabs.Parser.JSON;
using Newtonsoft.Json;

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

        public NeoScanAPI(string url, Logger logger, Nexus nexus, PhantasmaKeys keys)
        {
            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }

            this.URL = url;
            this.logger = logger;
            this.nexus = nexus;

            var key = InteropUtils.GenerateInteropKeys(keys, nexus.GetGenesisHash(nexus.RootStorage),  platformName);
            this.platformAddress = key.Address;
        }

        public string ExecuteRequest(string request)
        {
            Throw.If(request.StartsWith("/"), "request malformed");
            var url = $"{URL}/api/main_net/v1/{request}";

            logger.Message($"[Neoscan] ExecuteRequest: {request} url: {url}");
            try
            {
                string json;
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                    logger.Message($"[Neoscan] json: {JsonConvert.SerializeObject(json, Formatting.Indented)}");
                    return json;
                }
            }
            catch (Exception)
            {
                logger.Error("Neoscan request failed: " + url);
                return null;
            }
        }

        public InteropTransaction ReadTransaction(Hash hash)
        {
            var hashText = hash.ToString();

            var apiCall = $"get_transaction/{hashText}";
            var json = ExecuteRequest(apiCall);
            if (json == null)
            {
                throw new OracleException("Network read failure: "+ apiCall);
            }

            string inputSource = null;

            try
            {
                var root = JSONReader.ReadFromString(json);

                var scripts = root.GetNode("scripts");
                Throw.IfNull(scripts, nameof(scripts));

                if (scripts.ChildCount != 1)
                {
                    throw new OracleException("Transactions with multiple sources not supported yet");
                }

                Address interopAddress = Address.Null;
                foreach (var scriptEntry in scripts.Children)
                {
                    var vs = scriptEntry.GetNode("verification");
                    if (vs == null)
                    {
                        continue;
                    }

                    var verificationScript = Base16.Decode(vs.Value);
                    var pubKey = new byte[33];
                    Core.Utils.ByteArrayUtils.CopyBytes(verificationScript, 1, pubKey, 0, 33);

                    var signatureScript = NeoKeys.CreateSignatureScript(pubKey);
                    var signatureHash = Neo.Utils.CryptoUtils.ToScriptHash(signatureScript);
                    inputSource = Neo.Utils.CryptoUtils.ToAddress(signatureHash);

                    pubKey = Core.Utils.ByteArrayUtils.ConcatBytes((byte)AddressKind.User, pubKey);
                    interopAddress = Address.FromBytes(pubKey);
                    break;
                }

                if (interopAddress.IsNull)
                {
                    throw new OracleException("Could not fetch public key from transaction");
                }

                if (string.IsNullOrEmpty(inputSource))
                {
                    throw new OracleException("Could not fetch source address from transaction");
                }

                var attrNodes = root.GetNode("attributes");
                if (attrNodes != null)
                {
                    foreach (var entry in attrNodes.Children)
                    {
                        var kind = entry.GetString("usage");
                        if (kind == "Description")
                        {
                            var data = entry.GetString("data");
                            var bytes = Base16.Decode(data);

                            var text = Encoding.UTF8.GetString(bytes);
                            if (Address.IsValidAddress(text))
                            {
                                interopAddress = Address.FromText(text);
                            }
                        }
                    }
                }

                return FillTransaction(hashText, inputSource, interopAddress);
            }
            catch (Exception e)
            {
                throw new OracleException(e.Message);
            }
        }

        private InteropTransaction FillTransaction(string hashText, string inputAddress, Address interopAddress)
        {
            int page = 1;
            int maxPages = 9999;

            while (page <= maxPages)
            {
                var apiCall = $"get_address_abstracts/{inputAddress}/{page}";
                var json = ExecuteRequest(apiCall);
                if (json == null)
                {
                    throw new OracleException("Network read failure: " + apiCall);
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

                        var info = nexus.GetTokenInfo(nexus.RootStorage, symbol);
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

            var apiCall = $"get_block/{blockText}";
            var json = ExecuteRequest(apiCall);
            if (json == null)
            {
                throw new OracleException("Network read failure: " + apiCall);
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
