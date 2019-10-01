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

namespace Phantasma.Spook.Swaps
{
    public class NeoInterop : ChainInterop
    {
        private NeoAPI neoAPI;
        private NeoScanAPI neoscanAPI;
        private NeoKeys neoKeys;

        public NeoInterop(Nexus nexus, PhantasmaKeys keys, BigInteger blockHeight, NeoAPI neoAPI, NeoScanAPI neoscanAPI) : base(nexus, keys, blockHeight)
        {
            this.neoKeys = new NeoKeys(this.Keys.PrivateKey);
            this.neoscanAPI = neoscanAPI;
            this.neoAPI = neoAPI;
        }

        public override string LocalAddress => neoKeys.Address.ToString();
        public override string Name => NeoWallet.NeoPlatform;
        public override string PrivateKey => Keys.ToWIF();

        public override IEnumerable<ChainSwap>  Update()
        {
            var result = new List<ChainSwap>();

            int maxPages = 1;
            {
                var json = neoscanAPI.ExecuteRequest($"get_address_abstracts/{LocalAddress}/1");
                if (json == null)
                {
                    throw new SwapException("failed to fetch address page");
                }

                var root = JSONReader.ReadFromString(json);
                maxPages = root.GetInt32("total_pages");
            }

            for (int page = maxPages; page>=1; page--)
            {
                var json = neoscanAPI.ExecuteRequest($"get_address_abstracts/{LocalAddress}/{page}");
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

            var neoSourceAddress = entry.GetString("address_from");
            var asset = entry.GetString("asset");
            var hash = entry.GetString("txid");
            var amount = entry.GetDecimal("amount");

            TokenInfo token;

            /*
            if (!Swapper.FindTokenByHash(asset, out token))
            {
                return;
            }

            var destChain = "phantasma";
            var interop = Swapper.FindInterop(destChain);

            var transaction = neoAPI.GetTransaction(hash);
            var witness = transaction.witnesses[0];

            var destAddress = witness.ExtractAddress();
            var sourceAddress = Address.FromInterop(1, destAddress.ToByteArray().Skip(1).ToArray());

            var swap = new ChainSwap()
            {
                sourceHash = Hash.Parse(hash),
                sourcePlatform = this.Name,
                sourceAddress = sourceAddress,
                amount = UnitConversion.ToBigInteger(amount, token.Decimals),
                destinationAddress = destAddress,
                destinationPlatform = destChain,
                symbol = token.Symbol,
                status = ChainSwapStatus.Pending,
            };

            result.Add(swap);*/
        }

        public override Hash ReceiveFunds(ChainSwap swap)
        {
            throw new System.NotImplementedException();
            /*
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
            return Hash.Parse(hashText);*/
        }

        public override BrokerResult PrepareBroker(ChainSwap swap, out Hash brokerHash)
        {
            throw new System.NotImplementedException();
        }

        public override Hash SettleTransaction(Hash destinationHash, string destinationPlatform)
        {
            throw new System.NotImplementedException();
        }
    }
}
