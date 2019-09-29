using System;
using System.Collections.Generic;
using System.Threading;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Blockchain.Contracts;
using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.VM.Utils;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Tokens;
using Phantasma.Domain;
using Phantasma.Pay;
using Phantasma.Blockchain.Swaps;

namespace Phantasma.Spook.Swaps
{
    public class PhantasmaInterop : ChainInterop
    {
        public override string LocalAddress => Keys.Address.Text;
        public override string Name => DomainSettings.PlatformName;
        public override string PrivateKey => Keys.ToWIF();

        private NexusAPI api;

        public PhantasmaInterop(TokenSwapper swapper, KeyPair keys, BigInteger blockHeight, NexusAPI api) : base(swapper, keys, blockHeight)
        {
            this.api = api;
        }

        public override IEnumerable<ChainSwap> Update()
        {
            var swaps = new List<ChainSwap>();

            var nexus = Swapper.Nexus;
            var chain = nexus.RootChain;

            while (currentHeight <= chain.Height)
            {
                if (currentHeight < 1)
                {
                    currentHeight = 1;
                }

                var blockHash = chain.GetBlockHashAtHeight(currentHeight);
                var block = chain.GetBlockByHash(blockHash);

                foreach (var hash in block.TransactionHashes)
                {
                    var events = block.GetEventsForTransaction(hash);

                    foreach (var evt in events)
                    {
                        switch(evt.Kind){
                            case EventKind.BrokerRequest:
                                {
                                    var target = evt.GetContent<Address>();
                                    ProcessBrokerRequest(hash, evt.Address, target, events, swaps);
                                    break;
                                }

                            case EventKind.AddressLink:
                                {
                                    if (evt.Contract == "interop")
                                    {
                                        var target = evt.GetContent<Address>();
                                        var pendingSwaps = Swapper.GetPendingSwaps(target, ChainSwapStatus.Link);
                                        swaps.AddRange(pendingSwaps);
                                    }
                                    break;
                                }
                        }
                    }
                }

                currentHeight++;
            }

            return swaps;
        }

        private void ProcessBrokerRequest(Hash hash, Address from, Address target, IEnumerable<Event> events, List<ChainSwap> swaps)
        {
            string symbol = null;
            BigInteger amount = 0;

            foreach (var evt in events)
            {
                if (evt.Kind == EventKind.TokenBurn)
                {
                    var data = evt.GetContent<TokenEventData>();
                    symbol = data.symbol;
                    amount = data.value;
                    break;
                }
            }

            if (symbol == null || amount <= 0)
            {
                return;
            }

            TokenInfo tokenInfo;
            
            if (!Swapper.FindTokenBySymbol(symbol, out tokenInfo))
            {
                return;
            }

            string destinationAddress;
            string destinationPlatform;

            WalletUtils.DecodePlatformAndAddress(target, out destinationPlatform, out destinationAddress);

            var swap = new ChainSwap()
            {
                symbol = symbol,
                amount = amount,
                destinationAddress = target,
                destinationPlatform = destinationPlatform,
                sourceHash = hash,
                sourceAddress = from,
                sourcePlatform = DomainSettings.PlatformName,
                status = ChainSwapStatus.Pending
            };

            swaps.Add(swap);
        }

        public override BrokerResult PrepareBroker(ChainSwap swap, out Hash hash)
        {
            var nexus = Swapper.Nexus;

            hash = Hash.Null;

            var brokerAddress = nexus.RootChain.InvokeContract(nexus.RootStorage, "interop", "GetBroker", swap.destinationPlatform, swap.sourceHash).AsAddress();
            if (brokerAddress == Swapper.Keys.Address)
            {
                return BrokerResult.Ready;
            }
            else
            if (!brokerAddress.IsNull)
            {
                return BrokerResult.Skip;
            }

            TokenInfo token;
            if (!Swapper.FindTokenBySymbol(swap.symbol, out token))
            {
                return BrokerResult.Error;
            }

            var platform = nexus.GetPlatformInfo(swap.destinationPlatform);

            var brokerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, Swapper.Keys.Address);

            var minBalance = UnitConversion.GetUnitValue(DomainSettings.FuelTokenDecimals);
            if (brokerBalance < minBalance)
            {
                Swapper.Logger.Warning($"Not enough {DomainSettings.FuelTokenSymbol} balance to do broker operations");
                return BrokerResult.Error;
            }

            var script = new ScriptBuilder().AllowGas(Swapper.Keys.Address, Address.Null, Swapper.MinimumFee, 9999).CallContract("interop", "SetBroker", Swapper.Keys.Address, swap.sourceHash).SpendGas(Swapper.Keys.Address).EndScript();

            var tx = new Transaction(api.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
            tx.Sign(Swapper.Keys);

            var bytes = tx.ToByteArray(true);

            var txData = Base16.Encode(bytes);
            var result = api.SendRawTransaction(txData);
            if (result is SingleResult)
            {
                var hashText = (string)((SingleResult)result).value;

                Swapper.Logger.Message("Waiting for transaction confirmation: " + hash);
                do
                {
                    Thread.Sleep(2000);

                    result = api.GetTransaction(hashText);

                    if (result is TransactionResult)
                    {
                        hash = Hash.Parse(hashText);
                        break;
                    }

                } while (true);

                return BrokerResult.Ready;
            }

            return BrokerResult.Error;
        }

        public override Hash SettleTransaction(Hash sourceHash, string sourcePlatform)
        {
            var script = new ScriptBuilder().AllowGas(Swapper.Keys.Address, Address.Null, Swapper.MinimumFee, 9999).CallContract("interop", "SettleTransaction", Swapper.Keys.Address, sourcePlatform, sourceHash).SpendGas(Swapper.Keys.Address).EndScript();

            var tx = new Transaction(Swapper.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
            tx.Sign(Swapper.Keys);

            var bytes = tx.ToByteArray(true);

            var txData = Base16.Encode(bytes);
            var result = this.api.SendRawTransaction(txData);
            if (result is SingleResult)
            {
                var hash = (string)((SingleResult)result).value;

                Swapper.Logger.Message("Waiting for transaction confirmation: " + hash);
                do
                {
                    Thread.Sleep(2000);

                    result = this.api.GetTransaction(hash);

                    if (result is TransactionResult)
                    {
                        break;
                    }

                } while (true);

                return Hash.Parse(hash);
            }

            return Hash.Null;
        }

        public override Hash ReceiveFunds(ChainSwap swap)
        {
            return SettleTransaction(swap.sourceHash, swap.sourcePlatform);
        }
    }
}
