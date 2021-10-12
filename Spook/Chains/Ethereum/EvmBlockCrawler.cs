using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;

using Phantasma.Core.Log;
using Phantasma.Domain;
using Phantasma.Pay.Chains;
using Phantasma.Cryptography;
using Phantasma.Spook.Interop;

using PBigInteger = Phantasma.Numerics.BigInteger;
using InteropTransfers = System.Collections.Generic.Dictionary<string,
      System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Phantasma.Domain.InteropTransfer>>>;

namespace Phantasma.Spook.Chains
{
    public class CrawledBlock
    {
        public Hash Hash { get; }
        public InteropTransfers Transfers { get; }

        public CrawledBlock(Hash hash, InteropTransfers transfers)
        {
            Hash = hash;
            Transfers = transfers;
        }
    }

    public class EvmBlockCrawler
    {
        private string[] addressesToWatch;
        private BlockchainProcessor processor;
        private CancellationToken cancellationToken;
        private List<TransactionReceiptVO> transactions = new List<TransactionReceiptVO>();
        private Web3 web3;
        private Logger logger;
        private string platform;
        private Func<string, Address> encodeHandler;
        private Func<Nethereum.RPC.Eth.DTOs.Transaction, Address> addressExtractor;

        public List<TransactionReceiptVO> Result => transactions;

        public EvmBlockCrawler(Logger logger, string[] addresses, uint blockConfirmations, EthAPI api,
                Func<string, Address> encodeHandler, Func<Nethereum.RPC.Eth.DTOs.Transaction, Address> extractor, string platform)
        {
            this.addressesToWatch = addresses;
            this.web3 = api.GetWeb3Client();
            this.logger = logger;
            this.encodeHandler = encodeHandler;
            this.platform = platform;
            this.addressExtractor = extractor;

            processor = web3.Processing.Blocks.CreateBlockProcessor(steps =>
                {
                    steps.TransactionStep.SetMatchCriteria(t => t.Transaction.IsToAny(addressesToWatch));
                    steps.TransactionReceiptStep.AddSynchronousProcessorHandler(tx => AddTxrVO(tx));
               }, 
               blockConfirmations // block confirmations count
            );
            cancellationToken = new CancellationToken();
        }

        private void AddTxrVO(TransactionReceiptVO txr)
        {
            lock (transactions)
            {
                transactions.Add(txr);
            }
        }

        public void Fetch(BigInteger height)
        {
            Fetch(height, BigInteger.Zero);
        }

        public void Fetch(BigInteger from, BigInteger to)
        {
            EthUtils.RunSync(() => 
                processor.ExecuteAsync(
                        startAtBlockNumberIfNotProcessed: from,
                        toBlockNumber: (to != BigInteger.Zero) ? to : from,
                        cancellationToken: cancellationToken)
                    );
        }

        public InteropTransfers ExtractInteropTransfers(Blockchain.Nexus nexus, Logger logger, string[] swapAddresses)
        {
            var interopTransfers = new InteropTransfers();
            lock (transactions)
            {
                foreach(var txVo in transactions)
                {
                    var block = txVo.Block;
                    var txr = txVo.TransactionReceipt;
                    var tx = txVo.Transaction;

                    Address interopAddress;
                    try
                    {
                        interopAddress = EthereumInterop.ExtractInteropAddress(tx, logger);
                    }
                    catch(Exception e)
                    {
                        if (e.ToString().Contains("Header byte out of range"))
                        {
                            // Ignoring this exception and skipping this tx.
                            // RecoverFromSignature() crashed and we cannot avoid it atm.
                            // Related to EIP-1559, example of probematic tx: https://etherscan.io/tx/0xb022c146d8d1e684de0c1faae43e7ce36afb6969719adfdcafcc5bb7d5913185
                            // TODO Fix by updating to new Nethereum and dealing with EIP-1559 better.
                            logger.Debug("Warning: Skipping 'Header byte out of range' tx: " + tx.TransactionHash);
                            continue;
                        }
                        else
                        {
                            throw;
                        }
                    }
                    var transferEvents = txr.DecodeAllEvents<TransferEventDTO>();
                    //var swapEvents = txr.DecodeAllEvents<SwapEventDTO>();
                    var nodeSwapAddresses = swapAddresses.Select(x => encodeHandler(x)).ToList();
                    //var nodeSwapAddresses = encodeHandler(swapAddress);

                    if (transferEvents.Count > 0 || tx.Value != null && tx.Value.Value > 0)
                    {
                        if (!interopTransfers.ContainsKey(block.BlockHash))
                        {
                            interopTransfers.Add(block.BlockHash, new Dictionary<string, List<InteropTransfer>>());
                        }
                    }

                    if (transferEvents.Count > 0)
                    {
                        var blockId = block.Number.ToString();
                        var hash = txr.TransactionHash;

                        foreach(var evt in transferEvents)
                        {
                            var targetAddress = encodeHandler(evt.Event.To);

                            // If it's not our address, skip immediatly, don't log it
                            if (!nodeSwapAddresses.Contains(targetAddress))
                            {
                                continue;
                            }

                            var asset = EthUtils.FindSymbolFromAsset(this.platform, nexus, evt.Log.Address);
                            logger.Debug($@"Found {this.platform} swap: {blockId} hash: {hash} to: {evt.Event.To}
                                from: {evt.Event.From} value: {evt.Event.Value} asset: {asset}");
                            if (asset == null)
                            {
                                logger.Debug($"Asset [{evt.Log.Address}] not supported");
                                continue;
                            }

                            
                            var sourceAddress = encodeHandler(evt.Event.From);
                            var amount = PBigInteger.Parse(evt.Event.Value.ToString());

                            //logger.Message("nodeSwapAddress: " + nodeSwapAddress);
                            //logger.Message("sourceAddress: " + sourceAddress);
                            //logger.Message("targetAddress: " + targetAddress);
                            //logger.Message("amount: " + amount);

                            if (!interopTransfers[block.BlockHash].ContainsKey(evt.Log.TransactionHash))
                            {
                                interopTransfers[block.BlockHash].Add(evt.Log.TransactionHash, new List<InteropTransfer>());
                            }

                            interopTransfers[block.BlockHash][evt.Log.TransactionHash].Add
                                (
                                 new InteropTransfer
                                 (
                                  this.platform,
                                  sourceAddress,
                                  DomainSettings.PlatformName,
                                  targetAddress,
                                  interopAddress, // interop address
                                  asset,
                                  amount
                                 )
                                );
                        }
                    }

                    if (tx.Value != null && tx.Value.Value > 0)
                    {
                        //logger.Message("ETH:");
                        //logger.Message(block.Number.ToString());
                        //logger.Message(tx.TransactionHash);
                        //logger.Message(tx.To);
                        //logger.Message(tx.From);
                        //logger.Message(tx.Value.ToString());

                        var targetAddress = encodeHandler(tx.To);
                        Console.WriteLine("target eth: " + targetAddress);

                        if (!nodeSwapAddresses.Contains(targetAddress ))
                        {
                            continue;
                        }

                        if (!interopTransfers[block.BlockHash].ContainsKey(tx.TransactionHash))
                        {
                            interopTransfers[block.BlockHash].Add(tx.TransactionHash, new List<InteropTransfer>());
                        }

                        var sourceAddress = encodeHandler(tx.From);
                        var amount = PBigInteger.Parse(tx.Value.ToString());

                        var nativeSymbol = (this.platform == "ethereum") ? "ETH" : "BNB";

                        interopTransfers[block.BlockHash][tx.TransactionHash].Add
                            (
                             new InteropTransfer
                             (
                              this.platform,
                              sourceAddress,
                              DomainSettings.PlatformName,
                              targetAddress,
                              interopAddress, // interop address
                              nativeSymbol,
                              amount
                             )
                            );
                    }
                }

                transactions.Clear();
            }

            // clear transactions after extraction was done
            return interopTransfers;
        }
    }
}
