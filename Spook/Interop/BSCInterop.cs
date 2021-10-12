using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Numerics;

using Phantasma.Blockchain;
using Phantasma.Core.Log;
using Phantasma.Spook.Chains;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;
using Phantasma.Domain;
using Phantasma.Core.Utils;

using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Hex.HexTypes;

using EthereumKey = Phantasma.Ethereum.EthereumKey;
using PBigInteger = Phantasma.Numerics.BigInteger;
using Phantasma.Storage.Context;

namespace Phantasma.Spook.Interop
{
    public class BSCInterop: ChainSwapper
    {
        private EthAPI ethAPI;
        private List<string> contracts;
        private uint confirmations;
        private List<BigInteger> _resyncBlockIds = new List<BigInteger>();
        private static bool initialStart = true;

        public BSCInterop(TokenSwapper swapper, EthAPI ethAPI, PBigInteger interopBlockHeight, string[] contracts, uint confirmations)
                : base(swapper, BSCWallet.BSCPlatform)
        {
            string lastBlockHeight = OracleReader.GetCurrentHeight(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform);
            if(string.IsNullOrEmpty(lastBlockHeight))
                OracleReader.SetCurrentHeight(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, new BigInteger(interopBlockHeight.ToSignedByteArray()).ToString());

            Logger.Message($"interopHeight: {OracleReader.GetCurrentHeight(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform)}");
            Console.WriteLine("encoded bsc: " + BSCWallet.EncodeAddress("0x44E8743A6CAC3E59594C19DD462863A5AA5E06BB"));
            Console.WriteLine("encoded eth: " + EthereumWallet.EncodeAddress("0x44E8743A6CAC3E59594C19DD462863A5AA5E06BB"));

            Console.WriteLine("from encoded bsc: " + BSCWallet.EncodeAddress("0xA89a34c37Da826085E458c17067DA2F38b6e4763"));
            Console.WriteLine("from encoded eth: " + EthereumWallet.EncodeAddress("0xA89a34c37Da826085E458c17067DA2F38b6e4763"));

            this.contracts = contracts.ToList();

            // add local swap address to contracts
            this.contracts.Add(LocalAddress);

            this.confirmations = confirmations;
            this.ethAPI = ethAPI;
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
            try
            {
                lock (String.Intern("PendingSetCurrentHeight_" + BSCWallet.BSCPlatform))
                {
                    var result = new List<PendingSwap>();

                    // initial start, we have to verify all processed swaps
                    if (initialStart)
                    {
                        Logger.Debug($"Read all bsc blocks now.");
                        var allInteropBlocks = OracleReader.ReadAllBlocks(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform);

                        Logger.Debug($"Found {allInteropBlocks.Count} blocks");

                        foreach (var block in allInteropBlocks)
                        {
                            ProcessBlock(block, ref result);
                        }

                        initialStart = false;

                        // return after the initial start to be able to process all swaps that happend in the mean time.
                        return result;
                    }

                    var currentHeight = ethAPI.GetBlockHeight();
                    var _interopBlockHeight = BigInteger.Parse(OracleReader.GetCurrentHeight(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform));
                    Logger.Debug($"Swaps: Current Eth chain height: {currentHeight}, interop: {_interopBlockHeight}, delta: {currentHeight - _interopBlockHeight}");

                    var blocksProcessedInOneBatch = 0;
                    while (blocksProcessedInOneBatch < 50)
                    {
                        if (_resyncBlockIds.Any())
                        {
                            for (var i = 0; i < _resyncBlockIds.Count; i++)
                            {
                                var blockId = _resyncBlockIds.ElementAt(i);
                                if (blockId > _interopBlockHeight)
                                {
                                    this.Logger.Warning($"EthInterop:Update() resync block {blockId} higher than current interop height, can't resync.");
                                    _resyncBlockIds.RemoveAt(i);
                                    continue;
                                }

                                try
                                {
                                    this.Logger.Debug($"EthInterop:Update() resync block {blockId} now.");
                                    var block = GetInteropBlock(blockId);
                                    ProcessBlock(block, ref result);
                                }
                                catch (Exception e)
                                {
                                    this.Logger.Error($"EthInterop:Update() resync block {blockId} failed: " + e);
                                }
                                _resyncBlockIds.RemoveAt(i);
                            }
                        }

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
                        //    oracleReader.SetCurrentHeight(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, _interopBlockHeight.ToString());
                        //}
                        //else
                        //{

                        /* Future improvement, implement oracle call to fetch multiple blocks */
                        var url = DomainExtensions.GetOracleBlockURL(
                                BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, PBigInteger.FromUnsignedArray(_interopBlockHeight.ToByteArray(), true));

                        var interopBlock = OracleReader.Read<InteropBlock>(DateTime.Now, url);

                        ProcessBlock(interopBlock, ref result);

                        _interopBlockHeight++;
                        //}
                    }

                    OracleReader.SetCurrentHeight(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, _interopBlockHeight.ToString());

                    var total = result.Count();
                    if (total > 0)
                    {
                        Logger.Message($"startup: found {total} bsc swaps");
                    }
                    else
                    {
                        Logger.Debug($"did not find any bsc swaps");
                    }
                    return result;
                }
            }
            catch (Exception e)
            {
                var logMessage = "BSCInterop.Update() exception caught:\n" + e.Message;
                var inner = e.InnerException;
                while (inner != null)
                {
                    logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;
                    inner = inner.InnerException;
                }
                logMessage += "\n\n" + e.StackTrace;

                Logger.Error(logMessage);

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


        public override void ResyncBlock(BigInteger blockId)
        {
            lock(_resyncBlockIds)
            {
                _resyncBlockIds.Add(blockId);
            }
        }

        private List<Task<InteropBlock>> CreateTaskList(BigInteger batchCount, BigInteger currentHeight, BigInteger[] blockIds = null)
        {
            List<Task<InteropBlock>> taskList = new List<Task<InteropBlock>>();
            if (blockIds == null)
            {
                var _interopBlockHeight = BigInteger.Parse(OracleReader.GetCurrentHeight(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform));
                var nextCurrentBlockHeight = _interopBlockHeight + batchCount;

                if (nextCurrentBlockHeight > currentHeight)
                {
                    nextCurrentBlockHeight = currentHeight;
                }
                
                for (var i = _interopBlockHeight; i <= nextCurrentBlockHeight; i++)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, PBigInteger.FromUnsignedArray(i.ToByteArray(), true));
                
                    taskList.Add(CreateTask(url));
                }
            }
            else
            {
                foreach (var blockId in blockIds)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, PBigInteger.FromUnsignedArray(blockId.ToByteArray(), true));
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
                               return OracleReader.Read<InteropBlock>(DateTime.Now, url);
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

                               Logger.Error(logMessage.Contains("BSC block is null") ? "oracleReader.Read(): BSC block is null, possible connection failure" : logMessage);
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
                var interopTx = OracleReader.ReadTransaction(BSCWallet.BSCPlatform, "bsc", txHash);

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

        private InteropBlock GetInteropBlock(BigInteger blockId)
        {
            var url = DomainExtensions.GetOracleBlockURL(
                BSCWallet.BSCPlatform, BSCWallet.BSCPlatform,
                PBigInteger.FromUnsignedArray(blockId.ToByteArray(), true));

            return OracleReader.Read<InteropBlock>(DateTime.Now, url);
        }


        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(Logger logger, BlockWithTransactions block, EthAPI api
                , string[] swapAddress)
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
                Console.WriteLine("if " + txnChainId.GetChainIdAsBigInteger());
                accountSenderRecovered = Nethereum.Signer.EthECKey.RecoverFromSignature(transaction.Signature, transaction.RawHash, txnChainId.GetChainIdAsBigInteger());
            }
            else
            {
                Console.WriteLine("else ");
                accountSenderRecovered = Nethereum.Signer.EthECKey.RecoverFromSignature(transaction.Signature, transaction.RawHash);
            }
            Console.WriteLine("!!!!!!!!!!!!!!1 accountSenderREcovered: " + accountSenderRecovered);
            var pubKey = accountSenderRecovered.GetPubKey();

            var point = Cryptography.ECC.ECPoint.DecodePoint(pubKey, Cryptography.ECC.ECCurve.Secp256k1);
            pubKey = point.EncodePoint(true);

            var bytes = new byte[34];
            bytes[0] = (byte)AddressKind.User;
            ByteArrayUtils.CopyBytes(pubKey, 0, bytes, 1, 33);

            var address = Address.FromBytes(bytes);
            Console.WriteLine("!!!!!!!!!!!!!!1 accountSenderREcovered: " + address);

            return address;
        }

        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(Nexus nexus, Logger logger, EthAPI api
                , BigInteger height, string[] contracts, uint confirmations, string[] swapAddress)
        {
            Hash blockHash = Hash.Null;
            var interopTransactions = new List<InteropTransaction>();

            //TODO HACK
            var combinedAddresses = contracts.ToList();
            combinedAddresses.AddRange(swapAddress);

            Dictionary<string, Dictionary<string, List<InteropTransfer>>> transfers = new Dictionary<string, Dictionary<string, List<InteropTransfer>>>();
            try
            {
                // TODO pass from outside to not instantiate for each call to MakeInteropBlock
                Func<string, Address> addressEncoder = (address) => { return BSCWallet.EncodeAddress(address); };
                Func<Nethereum.RPC.Eth.DTOs.Transaction, Address> addressExtractor = (tx) => { return BSCInterop.ExtractInteropAddress(tx); };

                var crawler = new EvmBlockCrawler(logger, combinedAddresses.ToArray(), confirmations, api,
                        addressEncoder, addressExtractor, BSCWallet.BSCPlatform);
                // fetch blocks
                crawler.Fetch(height);
                transfers = crawler.ExtractInteropTransfers(nexus, logger, swapAddress);
            }
            catch (Exception e)
            {
                logger.Error("Failed to fetch eth blocks: " + e);
            }

            if (transfers.Count == 0)
            {
                var emptyBlock =  new InteropBlock(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, Hash.Null, new Hash[]{});
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
                ? new InteropBlock(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, blockHash, hashes)
                : new InteropBlock(BSCWallet.BSCPlatform, BSCWallet.BSCPlatform, Hash.Null, hashes);

            return Tuple.Create(interopBlock, interopTransactions.ToArray());
        }

        private static string FetchTokenURI(string contractAddress, BigInteger tokenID)
        {
            throw new NotImplementedException();
            /*
            Nethereum.Web3.Web3 web3 = null; ????
            abi = ??
            var contract = web3.Eth.GetContract(abi, contractAddress);
            var function = contract.GetFunction("tokenURI");
            object[] args = new object[] { tokenID };
            var result = function.CallAsync<string>(args);
            return result;*/
        }

        private static Dictionary<string, List<InteropTransfer>> GetInteropTransfers(Nexus nexus, Logger logger,
                TransactionReceipt txr, EthAPI api, string[] swapAddresses)
        {
            logger.Debug($"get interop transfers for tx {txr.TransactionHash}");
            var interopTransfers = new Dictionary<string, List<InteropTransfer>>();

            Nethereum.RPC.Eth.DTOs.Transaction tx = null;
            try
            {
                // tx to get the eth transfer if any
                tx = api.GetTransaction(txr.TransactionHash);
            }
            catch (Exception e)
            {
                logger.Error("Getting eth tx failed: " + e.Message);
            }

            Console.WriteLine("txr: " + txr.Status);
            logger.Debug("Transaction status: " + txr.Status.Value);
            // check if tx has failed
            if (txr.Status.Value == 0)
            {
                logger.Error($"tx {txr.TransactionHash} failed");
                return interopTransfers;
            }

            foreach (var a in swapAddresses)
            {
                Console.WriteLine("swap address: " + a);

            }
            var nodeSwapAddresses = swapAddresses.Select(x => BSCWallet.EncodeAddress(x));
            var interopAddress = ExtractInteropAddress(tx);
            Console.WriteLine("interop address: " + interopAddress);

            // ERC721 (NFT)
            // TODO currently this code block is mostly copypaste from BEP20 block, later make a single method for both...
            //var erc721_events = txr.DecodeAllEvents<Nethereum.StandardNonFungibleTokenERC721.ContractDefinition.TransferEventDTOBase>();
            //foreach (var evt in erc721_events)
            //{
            //    var asset = EthUtils.FindSymbolFromAsset(nexus, evt.Log.Address);
            //    if (asset == null)
            //    {
            //        logger.Warning($"Asset [{evt.Log.Address}] not supported");
            //        continue;
            //    }

            //    var targetAddress = BSCWallet.EncodeAddress(evt.Event.To);
            //    var sourceAddress = BSCWallet.EncodeAddress(evt.Event.From);
            //    var tokenID = PBigInteger.Parse(evt.Event.TokenId.ToString());

            //    if (nodeSwapAddresses.Contains(targetAddress))
            //    {
            //        if (!interopTransfers.ContainsKey(evt.Log.TransactionHash))
            //        {
            //            interopTransfers.Add(evt.Log.TransactionHash, new List<InteropTransfer>());
            //        }

            //        string tokenURI = FetchTokenURI(evt.Log.Address, evt.Event.TokenId);

            //        interopTransfers[evt.Log.TransactionHash].Add
            //        (
            //            new InteropTransfer
            //            (
            //                BSCWallet.BSCPlatform,
            //                sourceAddress,
            //                DomainSettings.PlatformName,
            //                targetAddress,
            //                interopAddress,
            //                asset,
            //                tokenID,
            //                System.Text.Encoding.UTF8.GetBytes(tokenURI)
            //            )
            //        );
            //    }
            //}

            // BEP20
            var bep20_events = txr.DecodeAllEvents<TransferEventDTO>();
            foreach (var evt in bep20_events)
            {
                var asset = EthUtils.FindSymbolFromAsset(BSCWallet.BSCPlatform, nexus, evt.Log.Address);
                if (asset == null)
                {
                    logger.Warning($"Asset [{evt.Log.Address}] not supported");
                    continue;
                }

                var targetAddress = BSCWallet.EncodeAddress(evt.Event.To);
                var sourceAddress = BSCWallet.EncodeAddress(evt.Event.From);
                var amount = PBigInteger.Parse(evt.Event.Value.ToString());

                if (nodeSwapAddresses.Contains(targetAddress))
                {
                    if (!interopTransfers.ContainsKey(evt.Log.TransactionHash))
                    {
                        interopTransfers.Add(evt.Log.TransactionHash, new List<InteropTransfer>());
                    }

                    interopTransfers[evt.Log.TransactionHash].Add
                    (
                        new InteropTransfer
                        (
                            BSCWallet.BSCPlatform,
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

            Console.WriteLine("value: " + tx.Value);
            Console.WriteLine("value: " + tx.Value.Value);
            if (tx.Value != null && tx.Value.Value > 0)
            {
                var targetAddress = BSCWallet.EncodeAddress(tx.To);
                var sourceAddress = BSCWallet.EncodeAddress(tx.From);

                foreach (var a in nodeSwapAddresses)
                {
                    Console.WriteLine("node swap address: " + a);
                }
                Console.WriteLine("target address: " + targetAddress);

                if (nodeSwapAddresses.Contains(targetAddress))
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
                            BSCWallet.BSCPlatform,
                            sourceAddress,
                            DomainSettings.PlatformName,
                            targetAddress,
                            interopAddress,
                            "BSC", // TODO use const
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

        public static InteropTransaction MakeInteropTx(Nexus nexus, Logger logger, TransactionReceipt txr, EthAPI api, string[] swapAddresses)
        {
            logger.Debug("checking tx: " + txr.TransactionHash);

            IList<InteropTransfer> interopTransfers = new List<InteropTransfer>();

            interopTransfers = GetInteropTransfers(nexus, logger, txr, api, swapAddresses).SelectMany(x => x.Value).ToList();
            logger.Debug($"Found {interopTransfers.Count} interop transfers!");

            return ((interopTransfers.Count() > 0)
                ? new InteropTransaction(Hash.Parse(txr.TransactionHash), interopTransfers.ToArray())
                : new InteropTransaction(Hash.Null, interopTransfers.ToArray()));
        }

        private Hash VerifyBscTx(Hash sourceHash, string txHash)
        {
            TransactionReceipt txr;

            try
            {
                var retries = 0;
                do
                {
                    // Checking if tx is mined.
                    txr = ethAPI.GetTransactionReceipt(txHash);
                    if (txr == null)
                    {
                        if (retries == 12) // Waiting for 1 minute max.
                            break;

                        Thread.Sleep(5000);
                        retries++;
                    }
                } while (txr == null);

                if (txr == null)
                {
                    Logger.Error($"BSC transaction {txHash} not mined yet.");
                    return Hash.Null;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Exception during polling for receipt: {e}");
                return Hash.Null;
            }

            if (txr.Status.Value == 0) // Status == 0 = error
            {
                Logger.Error($"Possible failed bsc swap sourceHash: {sourceHash} txHash: {txHash}");

                Logger.Error($"EthAPI error, tx {txr.TransactionHash} ");
                return Hash.Null;
            }

            return Hash.Parse(txr.TransactionHash.Substring(2));
        }

        internal override Hash VerifyExternalTx(Hash sourceHash, string txStr)
        {
            return VerifyBscTx(sourceHash, txStr);
        }



        // NOTE no locks happen here because this callback is called from within a lock
        internal override Hash SettleSwap(Hash sourceHash, Address destination, IToken token, Numerics.BigInteger amount)
        {
            // check if tx was sent but not minded yet
            string tx = null;

            var inProgressMap = new StorageMap(TokenSwapper.InProgressTag, Swapper.Storage);

            if (inProgressMap.ContainsKey<Hash>(sourceHash))
            {
                tx = inProgressMap.Get<Hash, string>(sourceHash);

                if (!string.IsNullOrEmpty(tx))
                {
                    return VerifyBscTx(sourceHash, tx);
                }
            }

            var total = Numerics.UnitConversion.ToDecimal(amount, token.Decimals);

            var bscKeys = EthereumKey.FromWIF(this.WIF);

            var destAddress = BSCWallet.DecodeAddress(destination);

            try
            {
                Logger.Debug($"BSCSWAP: Trying transfer of {total} {token.Symbol} from {bscKeys.Address} to {destAddress}");
                var transferResult = ethAPI.TryTransferAsset(BSCWallet.BSCPlatform, token.Symbol, destAddress, total, token.Decimals, out tx);

                if (transferResult == EthTransferResult.Success)
                {
                    // persist resulting tx hash as in progress
                    inProgressMap.Set<Hash, string>(sourceHash, tx);
                    Logger.Debug("broadcasted eth tx: " + tx);
                }
                else
                {
                    Logger.Error($"BSCSWAP: Transfer of {total} {token.Symbol} from {bscKeys.Address} to {destAddress} failed, no tx generated");
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Exception during transfer: {e}");
                // we don't know if the transfer happend or not, therefore can't delete from inProgressMap yet.
                return Hash.Null;
            }

            return VerifyBscTx(sourceHash, tx);
        }

    }
}
