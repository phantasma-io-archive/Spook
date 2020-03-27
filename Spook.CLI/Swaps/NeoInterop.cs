using System;
using System.Collections.Generic;
using LunarLabs.Parser.JSON;
using LunarLabs.Parser;
using Phantasma.Numerics;
using Phantasma.Neo.Core;
using Phantasma.Domain;
using Phantasma.Cryptography;
using Phantasma.Spook.Oracles;
using Phantasma.Core.Log;
using System.Linq;

namespace Phantasma.Spook.Swaps
{
    public class NeoInterop : ChainWatcher
    {
        private Logger logger;
        private NeoScanAPI neoscanAPI;
        private NeoAPI neoAPI;
        private BigInteger _blockHeight;
        private int maxPageAllowed;
        private DateTime lastScan;

        public NeoInterop(TokenSwapper swapper, string wif, BigInteger blockHeight, NeoScanAPI neoscanAPI, NeoAPI neoAPI, Logger logger) : base(swapper, wif, "neo")
        {
            this._blockHeight = blockHeight;

            this.neoscanAPI = neoscanAPI;
            this.neoAPI = neoAPI;
            this.maxPageAllowed = 9999;

            this.lastScan = DateTime.UtcNow.AddYears(-1);;

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

            var delta = DateTime.UtcNow - lastScan;
            if (delta.TotalSeconds < 10)
            {
                return Enumerable.Empty<PendingSwap>();
            }

            logger.Message($"Update NeoInterop." + lastScan);
            var json = neoAPI.GetNep5Transfers(LocalAddress, lastScan);
            if (json == null)
            {
                logger.Warning("failed to fetch address page");
                return Enumerable.Empty<PendingSwap>();
            }

            var root = JSONReader.ReadFromString(json);

            var all = root.GetNode("result");
            var allTx = all.GetNode("sent");
            var received = all.GetNode("received");
            var address = all.GetString("address");

            for (int i = received.ChildCount - 1; i >= 0; i--)
            {
                allTx.AddNode(received.GetNodeByIndex(i));
            }

            logger.Message($"entries: {allTx.ChildCount}");
            for (int i = allTx.ChildCount - 1; i >= 0; i--)
            {
                var entry = allTx.GetNodeByIndex(i);

                var temp = entry.GetString("block_index");
                var height = BigInteger.Parse(temp);
                //logger.Message($"block_height: {_blockHeight.ToString()} height: {height}");

                if (height >= _blockHeight)
                {
                    try
                    {
                        ProcessTransaction(entry, result, address);
                        _blockHeight = height;
                    }
                    catch (Exception e)
                    {
                        logger.Error("error: " + e.ToString());
                    }
                }
            }

            maxPageAllowed = 1;
            lastScan = DateTime.UtcNow;

            return result;
        }

        private void ProcessTransaction(DataNode entry, List<PendingSwap> result, string address)
        {
            var destinationAddress = address;
            if (destinationAddress != this.LocalAddress)
            {
                return;
            }

            var asset = entry.GetString("asset_hash");
            var hash = entry.GetString("tx_hash");

            var token = Swapper.FindTokenByHash(asset, "neo");
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
