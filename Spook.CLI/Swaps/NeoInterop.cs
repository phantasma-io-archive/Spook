using System.Linq;
using System.Collections.Generic;
using LunarLabs.Parser.JSON;
using LunarLabs.Parser;
using Phantasma.Numerics;
using Phantasma.Neo.Core;
using Phantasma.Pay.Chains;
using Phantasma.Blockchain.Tokens;
using Phantasma.Domain;
using Phantasma.Cryptography;
using Phantasma.Spook.Oracles;
using Phantasma.Blockchain;
using Phantasma.Core.Log;
using System;

namespace Phantasma.Spook.Swaps
{
    public class NeoInterop : ChainWatcher
    {
        private NeoAPI neoAPI;
        private Logger logger;
        private NeoScanAPI neoscanAPI;
        private BigInteger _blockHeight;

        public NeoInterop(TokenSwapper swapper, string wif, BigInteger blockHeight, NeoAPI neoAPI, NeoScanAPI neoscanAPI, Logger logger) : base(swapper, wif, "neo")
        {
            this._blockHeight = blockHeight;

            this.neoscanAPI = neoscanAPI;
            this.neoAPI = neoAPI;

            this.logger = logger;
        }

        protected override string GetAvailableAddress(string wif)
        {
            var neoKeys = Phantasma.Neo.Core.NeoKeys.FromWIF(wif);
            return neoKeys.Address;
        }

        public override IEnumerable<PendingSwap> Update()
        {
            var result = new List<PendingSwap>();

            int maxPages = 1;
            {
                var json = neoscanAPI.ExecuteRequest($"get_address_abstracts/{LocalAddress}/1");
                if (json == null)
                {
                    return result; // it will try again later
                }

                var root = JSONReader.ReadFromString(json);
                maxPages = root.GetInt32("total_pages");
            }

            for (int page = maxPages; page >= 1; page--)
            {
                var json = neoscanAPI.ExecuteRequest($"get_address_abstracts/{LocalAddress}/{page}");
                if (json == null)
                {
                    logger.Warning("failed to fetch address page");
                    break;
                }

                var root = JSONReader.ReadFromString(json);

                var entries = root.GetNode("entries");

                for (int i = entries.ChildCount - 1; i >= 0; i--)
                {
                    var entry = entries.GetNodeByIndex(i);

                    var temp = entry.GetString("block_height");
                    var height = BigInteger.Parse(temp);

                    if (height >= _blockHeight)
                    {
                        try
                        {
                            ProcessTransaction(entry, result);
                            _blockHeight = height;
                        }
                        catch (Exception e)
                        {
                            logger.Error("error: " + e.ToString());
                        }
                    }
                }
            }

            return result;
        }

        private void ProcessTransaction(DataNode entry, List<PendingSwap> result)
        {
            var destinationAddress = entry.GetString("address_to");
            if (destinationAddress != this.LocalAddress)
            {
                return;
            }

            var asset = entry.GetString("asset");
            var hash = entry.GetString("txid");

            var token = Swapper.FindTokenByHash(asset);
            if (token == null)
            {
                logger.Warning("Someone tried to swap unsupported asset: " + asset);
                return;
            }

            var reader = Swapper.Nexus.CreateOracleReader();
            var interopTx = reader.ReadTransaction("neo", "neo", Hash.Parse(hash));

            if (interopTx.Transfers.Length != 1)
            {
                throw new OracleException("neo transfers with multiple sources or tokens not supported yet");
            }

            var transfer = interopTx.Transfers[0];

            var destAddress = transfer.interopAddress;
            var sourceAddress = transfer.sourceAddress;

            var swap = new PendingSwap(this.PlatformName, Hash.Parse(hash), sourceAddress, destAddress);
            result.Add(swap);
        }

        /*
                      public override Hash ReceiveFunds(ChainSwap swap)
             {
                 throw new System.NotImplementedException();
                 Transaction tx;

                 TokenInfo token;
                 if (!Swapper.FindTokenBySymbol(swap.symbol, out token))
                 {
                     return Hash.Null;
                 }

                 byte platID;
                 byte[] publicKey;

                 swap.destinationAddress.DecodeInterop(out platID, out publicKey);

                 var destAddress = NeoKeys.PublicKeyToAddress(publicKey);
                 var amount = UnitConversion.ToDecimal(swap.amount, token.Decimals);

                 if (swap.symbol == "NEO" || swap.symbol == "GAS")
                 {
                     tx = neoAPI.SendAsset(neoKeys, destAddress, swap.symbol, amount);
                 }
                 else
                 {
                     var scriptHash = token.Hash.ToString().Substring(0, 40);

                     var nep5 = new NEP5(neoAPI, scriptHash);
                     tx = nep5.Transfer(neoKeys, destAddress, amount);
                 }

                 if (tx == null)
                 {
                     throw new InteropException(this.Name + " transfer failed", ChainSwapStatus.Receive);
                 }

                 var hashText = tx.Hash.ToString();
                 return Hash.Parse(hashText);
    }*/
    }
}
