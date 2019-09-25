using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.VM.Utils;
using Phantasma.Core.Types;
using System.Threading;
using Phantasma.Blockchain.Tokens;
using Phantasma.Domain;
using Phantasma.Pay;

namespace Phantasma.Spook.Swaps
{
    public enum BrokerResult
    {
        Ready,
        Skip,
        Error
    }

    public class PhantasmaInterop : ChainInterop
    {
        public override string LocalAddress => Keys.Address.Text;
        public override string Name => "Phantasma";
        public override string PrivateKey => Keys.ToWIF();

        public PhantasmaInterop(TokenSwapper swapper, KeyPair keys, BigInteger blockHeight) : base(swapper, keys, blockHeight)
        {
        }

        public override void Update(Action<IEnumerable<ChainSwap>> callback)
        {
            var swaps = new List<ChainSwap>();

            var nexus = Swapper.nexusAPI.Nexus;
            var chain = nexus.RootChain;

            while (currentHeight <= chain.BlockHeight)
            {
                if (currentHeight < 1)
                {
                    currentHeight = 1;
                }

                var block = chain.FindBlockByHeight(currentHeight);

                foreach (var hash in block.TransactionHashes)
                {
                    var events = block.GetEventsForTransaction(hash);

                    foreach (var evt in events)
                    {
                        if (evt.Kind == EventKind.BrokerRequest)
                        {
                            var target = evt.GetContent<Address>();
                            ProcessBrokerRequest(hash, evt.Address, target, events, swaps);
                        }
                    }
                }

                currentHeight++;
            }

            callback(swaps);
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
                amount = UnitConversion.ToDecimal(amount, tokenInfo.Decimals),
                destinationAddress = destinationAddress,
                destinationPlatform = destinationPlatform,
                sourceHash = hash.ToString(),
                sourceAddress = from.Text,
                sourcePlatform = Nexus.PlatformName,
            };

            swaps.Add(swap);
        }

        public BrokerResult PrepareBroker(ChainSwap swap, out string hash)
        {
            var sourceHash = Hash.Parse(swap.sourceHash);
            var nexus = Swapper.nexusAPI.Nexus;

            hash = ChainSwap.DummyHash;

            var brokerAddress = nexus.RootChain.InvokeContract("interop", "GetBroker", swap.destinationPlatform, sourceHash).AsAddress();
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

            var script = new ScriptBuilder().AllowGas(Swapper.Keys.Address, Address.Null, Swapper.MinFee, 9999).CallContract("interop", "SetBroker", Swapper.Keys.Address, sourceHash).SpendGas(Swapper.Keys.Address).EndScript();

            var tx = new Transaction(Swapper.nexusAPI.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5));
            tx.Sign(Swapper.Keys);

            var bytes = tx.ToByteArray(true);

            var txData = Base16.Encode(bytes);
            var result = this.Swapper.nexusAPI.SendRawTransaction(txData);
            if (result is SingleResult)
            {
                hash = (string)((SingleResult)result).value;

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

                return BrokerResult.Ready;
            }

            return BrokerResult.Error;
        }

        public string SettleTransaction(string sourceHashText, string sourcePlatform)
        {
            var sourceHash = Hash.Parse(sourceHashText);

            var script = new ScriptBuilder().AllowGas(Swapper.Keys.Address, Address.Null, Swapper.MinFee, 9999).CallContract("interop", "SettleTransaction", Swapper.Keys.Address, sourcePlatform, sourceHash).SpendGas(Swapper.Keys.Address).EndScript();

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

        public override string ReceiveFunds(ChainSwap swap)
        {
            return SettleTransaction(swap.sourceHash, swap.sourcePlatform);
        }
    }
}
