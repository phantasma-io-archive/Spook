using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.API;
using Phantasma.Blockchain.Plugins;
using System.Linq;
using Phantasma.Blockchain;
using Phantasma.VM.Utils;
using Phantasma.Core.Types;
using System.Threading;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Spook.Swaps
{
    public class PhantasmaInterop : ChainInterop
    {
        public override string LocalAddress => keys.Address.Text;

        public override string Name => "Phantasma";

        private KeyPair keys;

        public PhantasmaInterop(TokenSwapper swapper, KeyPair keys, BigInteger blockHeight) : base(swapper, keys, blockHeight)
        {
            this.keys = KeyPair.FromWIF(this.WIF);
        }

        private void ProcessTransaction(Transaction tx, IEnumerable<Event> events, List<ChainSwap> swaps)
        {
            string symbol = null;
            Address sourceAddress = Address.Null;
            string destinationChain = null;
            string destinationAddress = null;
            decimal amount = 0;

            foreach (var evt in events)
            {
                if (evt.Kind == EventKind.TokenReceive)
                {
                    var chainName = Swapper.FindInteropByAddress(evt.Address);
                    if (chainName != null)
                    {
                        destinationChain = chainName;
                        foreach (var otherEvt in events)
                        {
                            if (otherEvt.Kind == EventKind.TokenSend)
                            {
                                var eventData = otherEvt.GetContent<TokenEventData>();
                                sourceAddress = otherEvt.Address;

                                var interop = Swapper.FindInterop(chainName);
                                destinationAddress = Swapper.FromExternalToLocal(sourceAddress, chainName);

                                symbol = eventData.symbol;

                                TokenInfo tokenInfo;
                                if (Swapper.FindTokenBySymbol(symbol, out tokenInfo))
                                {
                                    amount = UnitConversion.ToDecimal(eventData.value, tokenInfo.Decimals);
                                }
                                else
                                {
                                    amount = 0;
                                }

                            }
                        }

                    }

                    break;
                }
            }

            if (amount > 0)
            {
                var swap = new ChainSwap()
                {
                    sourceHash = tx.Hash.ToString(),
                    sourceChain = this.Name,
                    sourceAddress = sourceAddress.Text,
                    destinationChain = destinationChain,
                    destinationAddress = destinationAddress,
                    amount = amount,
                    symbol = symbol,
                };
                swaps.Add(swap);
            }
        }

        public override void Update(Action<IEnumerable<ChainSwap>> callback)
        {
            var swaps = new List<ChainSwap>();

            var nexus = Swapper.nexusAPI.Nexus;
            var plugin = nexus.GetPlugin<AddressTransactionsPlugin>();

            var entries = plugin.GetAddressTransactions(this.ExternalAddress)
                .Select(hash =>
                {
                    var tx = nexus.FindTransactionByHash(hash);
                    var block = nexus.FindBlockByTransaction(tx);
                    return new KeyValuePair<Block, Transaction>(block, tx);
                }).Where(x => x.Key.Height > currentHeight).OrderBy(x => x.Key.Timestamp.Value);

            foreach (var entry in entries)
            {
                var block = entry.Key;
                var tx = entry.Value;
                var evts = block.GetEventsForTransaction(tx.Hash);

                currentHeight = block.Height;
                ProcessTransaction(tx, evts, swaps);
            }

            callback(swaps);
        }

        public override string SendFunds(string address, TokenInfo token, decimal amount)
        {
            throw new NotImplementedException();
            // must burn here
        }

        public override string ReceiveFunds(string sourceChain, Hash sourceHash, string address, TokenInfo token, decimal amount)
        {
            var targetAddress = Address.FromText(address);
            var targetAmount = UnitConversion.ToBigInteger(amount, token.Decimals);
            var script = new ScriptBuilder().AllowGas(Swapper.Keys.Address, Address.Null, 1, 9999).CallContract("interop", "SettleTransaction", Swapper.Keys.Address, sourceChain, sourceHash).SpendGas(Swapper.Keys.Address).EndScript();

            var tx = new Transaction(Swapper.nexusAPI.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
            tx.Sign(Swapper.Keys);

            var bytes = tx.ToByteArray(true);

            var txData = Base16.Encode(bytes);
            var result = this.Swapper.nexusAPI.SendRawTransaction(txData);
            if (result is SingleResult)
            {
                var hash = (string)((SingleResult)result).value;

                Swapper.logger.Message("Waiting for transaction confirmation: " + hash);
                do
                {
                    Thread.Sleep(2000);

                    result = this.Swapper.nexusAPI.GetTransaction(hash);

                    if (result is TransactionResult)
                    {
                        break;
                    }

                } while (true);

                return hash;
            }

            return null;
        }

        private string ExecuteTransaction()
        {
            throw new NotImplementedException();
        }
    }
}
