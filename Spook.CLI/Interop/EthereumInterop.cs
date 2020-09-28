using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Phantasma.Blockchain;
using Phantasma.Spook.Interop;
using Phantasma.Core.Log;
using Phantasma.Spook.Chains;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;
using Phantasma.Domain;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using EthereumKey = Phantasma.Ethereum.EthereumKey;
using PBigInteger = Phantasma.Numerics.BigInteger;
using Phantasma.Core.Utils;
using System.Net;
using System.Text.Json;

namespace Phantasma.Spook.Interop
{
    public class EthereumInterop: ChainWatcher
    {
        private Logger logger;
        private EthAPI ethAPI;
        private OracleReader oracleReader;
        private Nexus _nexus;
        private List<string> contracts;
        private uint confirmations;
        private static bool initialStart = true;

        public EthereumInterop(TokenSwapper swapper, EthAPI ethAPI, string wif, PBigInteger interopBlockHeight
            ,OracleReader oracleReader, string[] contracts, uint confirmations, Nexus nexus, Logger logger)
                : base(swapper, wif, EthereumWallet.EthereumPlatform)
        {
            string lastBlockHeight = oracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform);
            if(string.IsNullOrEmpty(lastBlockHeight))
                oracleReader.SetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, new BigInteger(interopBlockHeight.ToSignedByteArray()).ToString());

            logger.Message($"interopHeight: {oracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform)}");

            this.contracts = contracts.ToList();

            // add local swap address to contracts
            this.contracts.Add(LocalAddress);

            this.confirmations = confirmations;
            this.ethAPI = ethAPI;
            this.oracleReader = oracleReader;
            this._nexus = nexus;
            this.logger = logger;
        }

        protected override string GetAvailableAddress(string wif)
        {
            var ethKeys = EthereumKey.FromWIF(wif);
            return ethKeys.Address;
        }

        public override IEnumerable<PendingSwap> Update()
        {
            // wait another 10s to execute eth interop
            //Task.Delay(10000).Wait();
            logger.Message("Running eth interop update now");
            try
            {
                lock (String.Intern("PendingSetCurrentHeight_" + EthereumWallet.EthereumPlatform))
                {
                    var result = new List<PendingSwap>();

                    // initial start, we have to verify all processed swaps
                    if (initialStart)
                    {
                        logger.Message($"Read all ethereum blocks now.");
                        var allInteropBlocks = oracleReader.ReadAllBlocks(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform);

                        logger.Message($"Found {allInteropBlocks.Count} blocks");

                        foreach (var block in allInteropBlocks)
                        {
                            ProcessBlock(block, ref result);
                        }

                        initialStart = false;

                        // return after the initial start to be able to process all swaps that happend in the mean time.
                        return result;
                    }

                    var currentHeight = ethAPI.GetBlockHeight();
                    var _interopBlockHeight = BigInteger.Parse(oracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform));
                    logger.Message($"current eth height: {currentHeight} interop eth height {_interopBlockHeight}");

                    var blocksProcessedInOneBatch = 0;
                    while (blocksProcessedInOneBatch < 50)
                    {
                        blocksProcessedInOneBatch++;

                        var blockDifference = currentHeight - _interopBlockHeight;
                        if (blockDifference < confirmations)
                        {
                            // no need to query the node yet
                            break;
                        }

                        //TODO quick sync not done yet, requieres a change to the oracle impl to fetch multiple blocks
                        //var nextHeight = (blockDifference > 50) ? 50 : blockDifference; //TODO

                        //var transfers = new Dictionary<string, Dictionary<string, List<InteropTransfer>>>();

                        //if (nextHeight > 1)
                        //{
                        //    var blockCrawler = new EthBlockCrawler(logger, contracts.ToArray(), 0/*confirmations*/, ethAPI); //TODO settings confirmations

                        //    blockCrawler.Fetch(currentHeight, nextHeight);
                        //    transfers = blockCrawler.ExtractInteropTransfers(logger, LocalAddress);
                        //    foreach (var entry in transfers)
                        //    {
                        //        foreach (var txInteropTransfer in entry.Value)
                        //        {
                        //            foreach (var interopTransfer in txInteropTransfer.Value)
                        //            {
                        //                result.Add(new PendingSwap(
                        //                    this.PlatformName
                        //                    ,Hash.Parse(entry.Key)
                        //                    ,interopTransfer.sourceAddress
                        //                    ,interopTransfer.interopAddress)
                        //                );
                        //            }
                        //        }
                        //    }

                        //    _interopBlockHeight = nextHeight;
                        //    oracleReader.SetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, _interopBlockHeight.ToString());
                        //}
                        //else
                        //{

                        /* Future improvement, implement oracle call to fetch multiple blocks */
                        var url = DomainExtensions.GetOracleBlockURL(
                                EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, PBigInteger.FromUnsignedArray(_interopBlockHeight.ToByteArray(), true));

                        var interopBlock = oracleReader.Read<InteropBlock>(DateTime.Now, url);

                        ProcessBlock(interopBlock, ref result);

                        _interopBlockHeight++;
                        //}
                    }

                    oracleReader.SetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, _interopBlockHeight.ToString());

                    logger.Message($"found { result.Count() } swaps");
                    return result;
                }
            }
            catch (Exception e)
            {
                var logMessage = "EthereumInterop.Update() exception caught:\n" + e.Message;
                var inner = e.InnerException;
                while (inner != null)
                {
                    logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;
                    inner = inner.InnerException;
                }
                logMessage += "\n\n" + e.StackTrace;

                logger.Error(logMessage);

                return new List<PendingSwap>();
            }
        }

        TimeSpan TimeAction(Action blockingAction)
        {
            Stopwatch stopWatch = System.Diagnostics.Stopwatch.StartNew();
            blockingAction();
            stopWatch.Stop();
            return stopWatch.Elapsed;
        }

        private List<Task<InteropBlock>> CreateTaskList(BigInteger batchCount, BigInteger currentHeight, BigInteger[] blockIds = null)
        {
            List<Task<InteropBlock>> taskList = new List<Task<InteropBlock>>();
            if (blockIds == null)
            {
                var _interopBlockHeight = BigInteger.Parse(oracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform));
                var nextCurrentBlockHeight = _interopBlockHeight + batchCount;

                if (nextCurrentBlockHeight > currentHeight)
                {
                    nextCurrentBlockHeight = currentHeight;
                }
                
                for (var i = _interopBlockHeight; i <= nextCurrentBlockHeight; i++)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, PBigInteger.FromUnsignedArray(i.ToByteArray(), true));
                
                    taskList.Add(CreateTask(url));
                }
            }
            else
            {
                foreach (var blockId in blockIds)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, PBigInteger.FromUnsignedArray(blockId.ToByteArray(), true));
                    taskList.Add(CreateTask(url));
                }
            }

            return taskList;
        }

        private Task<InteropBlock> CreateTask(string url)
        {
            return new Task<InteropBlock>(() =>
                   {
                       var delay = 1000;

                       while (true)
                       {
                           try
                           {
                               return oracleReader.Read<InteropBlock>(DateTime.Now, url);
                           }
                           catch (Exception e)
                           {
                               var logMessage = "oracleReader.Read() exception caught:\n" + e.Message;
                               var inner = e.InnerException;
                               while (inner != null)
                               {
                                   logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;
                                   inner = inner.InnerException;
                               }
                               logMessage += "\n\n" + e.StackTrace;

                               logger.Message(logMessage.Contains("Ethereum block is null") ? "oracleReader.Read(): Ethereum block is null, possible connection failure" : logMessage);
                           }

                           Thread.Sleep(delay);
                           if (delay >= 60000) // Once we reach 1 minute, we stop increasing delay and just repeat every minute.
                               delay = 60000;
                           else
                               delay *= 2;
                       }
                   });
        }

        private void ProcessBlock(InteropBlock block, ref List<PendingSwap> result)
        {
            foreach (var txHash in block.Transactions)
            {
                var interopTx = oracleReader.ReadTransaction(EthereumWallet.EthereumPlatform, "ethethereum", txHash);

                foreach (var interopTransfer in interopTx.Transfers)
                {
                    result.Add(
                                new PendingSwap(
                                                 this.PlatformName
                                                ,txHash
                                                ,interopTransfer.sourceAddress
                                                ,interopTransfer.interopAddress)
                            );
                }
            }
        }

        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(Logger logger, BlockWithTransactions block, EthAPI api
                , string swapAddress)
        {
            //TODO
            return null;
        }

        public static Address ExtractInteropAddress(Nethereum.RPC.Eth.DTOs.Transaction tx)
        {
            //Using the transanction from RPC to build a txn for signing / signed
            var transaction = Nethereum.Signer.TransactionFactory.CreateTransaction(tx.To, tx.Gas, tx.GasPrice, tx.Value, tx.Input, tx.Nonce,
                tx.R, tx.S, tx.V);
            
            //Get the account sender recovered
            Nethereum.Signer.EthECKey accountSenderRecovered = null;
            if (transaction is Nethereum.Signer.TransactionChainId)
            {
                var txnChainId = transaction as Nethereum.Signer.TransactionChainId;
                accountSenderRecovered = Nethereum.Signer.EthECKey.RecoverFromSignature(transaction.Signature, transaction.RawHash, txnChainId.GetChainIdAsBigInteger());
            }
            else
            {
                accountSenderRecovered = Nethereum.Signer.EthECKey.RecoverFromSignature(transaction.Signature, transaction.RawHash);
            }
            var pubKey = accountSenderRecovered.GetPubKey();

            var bytes = new byte[34];
            bytes[0] = (byte)AddressKind.User;
            ByteArrayUtils.CopyBytes(pubKey, 0, bytes, 1, 33);

            return Address.FromBytes(bytes);
        }

        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(Nexus nexus, Logger logger, EthAPI api
                , BigInteger height, string[] contracts, uint confirmations, string swapAddress)
        {
            Hash blockHash = Hash.Null;
            var interopTransactions = new List<InteropTransaction>();

            //TODO HACK
            var combinedAddresses = contracts.ToList();
            combinedAddresses.Add(swapAddress);

            var crawler = new EthBlockCrawler(logger, combinedAddresses.ToArray(), confirmations, api);

            // fetch blocks
            crawler.Fetch(height);

            var transfers = crawler.ExtractInteropTransfers(nexus, logger, swapAddress);

            if (transfers.Count == 0)
            {
                var emptyBlock =  new InteropBlock(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, Hash.Null, new Hash[]{});
                return Tuple.Create(emptyBlock, interopTransactions.ToArray());
            }

            blockHash = Hash.Parse(transfers.FirstOrDefault().Key);

            foreach (var block in transfers)
            {
                var txTransferDict  = block.Value;
                foreach (var tx in txTransferDict)
                {
                    var interopTx = MakeInteropTx(logger, tx.Key, tx.Value);
                    if (interopTx.Hash != Hash.Null)
                    {
                        interopTransactions.Add(interopTx);
                    }
                }
            }

            var hashes = interopTransactions.Select(x => x.Hash).ToArray() ;

            InteropBlock interopBlock = (interopTransactions.Count() > 0)
                ? new InteropBlock(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, blockHash, hashes)
                : new InteropBlock(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, Hash.Null, hashes);

            return Tuple.Create(interopBlock, interopTransactions.ToArray());
        }

        private static Dictionary<string, List<InteropTransfer>> GetInteropTransfers(Nexus nexus, Logger logger,
                TransactionReceipt txr, EthAPI api, string swapAddress)
        {
            logger.Message($"get interop transfers for tx {txr.TransactionHash}");
            var interopTransfers = new Dictionary<string, List<InteropTransfer>>();

            // tx to get the eth transfer if any
            var tx = api.GetTransaction(txr.TransactionHash);

            logger.Message("Transaction status: " + txr.Status.Value);
            // check if tx has failed
            if (txr.Status.Value == 0)
            {
                logger.Error($"tx {txr.TransactionHash} failed");
                return interopTransfers;
            }

            var nodeSwapAddress = EthereumWallet.EncodeAddress(swapAddress);
            var events = txr.DecodeAllEvents<TransferEventDTO>();
            var interopAddress = ExtractInteropAddress(tx);

            // ERC20
            foreach(var evt in events)
            {
                var asset = EthUtils.FindSymbolFromAsset(nexus, evt.Log.Address);
                if (asset == null)
                {
                    logger.Message($"Asset [{evt.Log.Address}] not supported");
                    continue;
                }

                var targetAddress = EthereumWallet.EncodeAddress(evt.Event.To);
                var sourceAddress = EthereumWallet.EncodeAddress(evt.Event.From);
                var amount = PBigInteger.Parse(evt.Event.Value.ToString());

                if (targetAddress.Equals(nodeSwapAddress))
                {
                    if (!interopTransfers.ContainsKey(evt.Log.TransactionHash))
                    {
                        interopTransfers.Add(evt.Log.TransactionHash, new List<InteropTransfer>());
                    }

                    interopTransfers[evt.Log.TransactionHash].Add
                    (
                        new InteropTransfer
                        (
                            EthereumWallet.EthereumPlatform,
                            sourceAddress,
                            DomainSettings.PlatformName,
                            targetAddress,
                            interopAddress,
                            asset,
                            amount
                        )
                    );
                }
            }

            if (tx.Value != null && tx.Value.Value > 0)
            {
                var targetAddress = EthereumWallet.EncodeAddress(tx.To);
                var sourceAddress = EthereumWallet.EncodeAddress(tx.From);

                if (targetAddress.Equals(nodeSwapAddress))
                {
                    var amount = PBigInteger.Parse(tx.Value.ToString());

                    if (!interopTransfers.ContainsKey(tx.TransactionHash))
                    {
                        interopTransfers.Add(tx.TransactionHash, new List<InteropTransfer>());
                    }

                    interopTransfers[tx.TransactionHash].Add
                    (
                        new InteropTransfer
                        (
                            EthereumWallet.EthereumPlatform,
                            sourceAddress,
                            DomainSettings.PlatformName,
                            targetAddress,
                            interopAddress,
                            "ETH", // TODO use const
                            amount
                        )
                    );
                }
            }


            return interopTransfers;
        }

        public static InteropTransaction MakeInteropTx(Logger logger, string txHash, List<InteropTransfer> transfers)
        {
            return ((transfers.Count() > 0)
                ? new InteropTransaction(Hash.Parse(txHash), transfers.ToArray())
                : new InteropTransaction(Hash.Null, transfers.ToArray()));
        }

        public static InteropTransaction MakeInteropTx(Nexus nexus, Logger logger, TransactionReceipt txr, EthAPI api, string swapAddress)
        {
            logger.Message("checking tx: " + txr.TransactionHash);

            IList<InteropTransfer> interopTransfers = new List<InteropTransfer>();

            interopTransfers = GetInteropTransfers(nexus, logger, txr, api, swapAddress).SelectMany(x => x.Value).ToList();
            logger.Message($"Found {interopTransfers.Count} interop transfers!");

            return ((interopTransfers.Count() > 0)
                ? new InteropTransaction(Hash.Parse(txr.TransactionHash), interopTransfers.ToArray())
                : new InteropTransaction(Hash.Null, interopTransfers.ToArray()));

        }

        public static decimal GetNormalizedFee(string[] urls)
        {
            var taskList = new List<Task<decimal>>();

            foreach (var url in urls)
            {
                taskList.Add(
                        new Task<decimal>(() => 
                        {
                            return GetFee(url);
                        })
                );
            }

            Parallel.ForEach(taskList, (task) =>
            {
                task.Start();
            });

            Task.WaitAll(taskList.ToArray());

            var results = new List<decimal>();
            foreach (var task in taskList)
            {
                results.Add(task.Result);
            }

            var median = GetMedian<decimal>(results.ToArray());

            return median;
        }

        public static decimal GetFee(string url)
        {
            decimal fee = 0;

            try
            {
                using (WebClient wc = new WebClient())
                {
                    var json = wc.DownloadString(url);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("standard", out var prop)) // TODO config?
                    {
                        fee = decimal.Parse(prop.ToString().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Getting fee failed: " + e);
            }

            return fee;
        }

        public static T GetMedian<T>(T[] sourceArray) where T : IComparable<T>
        {
            if (sourceArray == null || sourceArray.Length == 0)
                throw new ArgumentException("Median of empty array not defined.");

            T[] sortedArray = sourceArray;
            Array.Sort(sortedArray);

            //get the median
            int size = sortedArray.Length;
            int mid = size / 2;
            if (size % 2 != 0)
            {
                return sortedArray[mid];
            }

            dynamic value1 = sortedArray[mid];
            dynamic value2 = sortedArray[mid - 1];

            return (sortedArray[mid] + value2) * 0.5;
        }
    }
}
