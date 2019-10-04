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

namespace Phantasma.Spook.Swaps
{
    public class NeoInterop : ChainWatcher
    {
        private NeoAPI neoAPI;
        private Logger logger;
        private NeoScanAPI neoscanAPI;
        private BigInteger _blockHeight;

        public NeoInterop(TokenSwapper swapper, BigInteger blockHeight, NeoAPI neoAPI, NeoScanAPI neoscanAPI, Logger logger) : base(swapper, "neo")
        {
            this._blockHeight = blockHeight;

            this.neoscanAPI = neoscanAPI;
            this.neoAPI = neoAPI;

            this.logger = logger;
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

                    if (height > _blockHeight)
                    {
                        _blockHeight = height;

                        ProcessTransaction(entry, result);
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
                return;
            }

            var neoSourceAddress = entry.GetString("address_from");
            var amount = entry.GetDecimal("amount");

            var transaction = neoAPI.GetTransaction(hash);

            var destAddress = transaction.ExtractInteropAddress();
            var sourceAddress = NeoWallet.EncodeAddress(neoSourceAddress);

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
