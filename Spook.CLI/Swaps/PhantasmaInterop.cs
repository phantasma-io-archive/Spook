using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Storage;
using Phantasma.API;
using Phantasma.Blockchain.Plugins;
using System.Linq;
using Phantasma.Blockchain;

namespace Phantasma.Spook.Swaps
{
    public class PhantasmaInterop : ChainInterop
    {
        public override string LocalAddress => keys.Address.Text;

        public override string Name => "Phantasma";

        private KeyPair keys;

        public PhantasmaInterop(TokenSwapper swapper, string baseWif, BigInteger blockHeight) : base(swapper, baseWif, blockHeight)
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
                if (evt.Kind == EventKind.Metadata)
                {
                    var eventData = evt.GetContent<MetadataEventData>();
                    var interopTag = "interop.";
                    if (eventData.type == "account" && eventData.metadata.key.StartsWith(interopTag))
                    {
                        var chainName = eventData.metadata.key.Substring(interopTag.Length);
                        var interop = Swapper.FindInterop(chainName);

                        var localAddress = eventData.metadata.value;
                        interop.RegisterMapping(localAddress, evt.Address);
                        Console.WriteLine($"Registed mapping: {localAddress} ({chainName}) => {evt.Address}");
                    }
                }
                else
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
                                destinationAddress = interop.FromExternalToLocal(sourceAddress);

                                symbol = eventData.symbol;

                                TokenInfo tokenInfo;
                                if (Swapper.FindTokenBySymbol(symbol, out tokenInfo))
                                {
                                    amount = UnitConversion.ToDecimal(eventData.value, tokenInfo.decimals);
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

            var nexus = Swapper.API.Nexus;
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

        public override string ReceiveFunds(string address, TokenInfo token, decimal amount)
        {
            throw new NotImplementedException();
            // must mint here
        }

        private string ExecuteTransaction()
        {
            throw new NotImplementedException();
        }
    }
}
