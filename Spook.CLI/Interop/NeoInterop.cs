using System;
using System.Collections.Generic;
using LunarLabs.Parser;
using PBigInteger = Phantasma.Numerics.BigInteger;
using System.Numerics;
using Phantasma.Blockchain;
using Phantasma.Neo.Core;
using Phantasma.Neo.Utils;
using Phantasma.Neo.Cryptography;
using NeoBlock = Phantasma.Neo.Core.Block;
using NeoTx = Phantasma.Neo.Core.Transaction;
using Phantasma.Domain;
using Phantasma.Pay;
using Phantasma.Pay.Chains;
using Phantasma.Cryptography;
using Phantasma.Spook.Swaps;
using Phantasma.Core.Log;
using System.Linq;
using System.Threading;

namespace Phantasma.Spook.Interop
{
    public class NeoInterop : ChainWatcher
    {
        private Logger logger;
        private NeoAPI neoAPI;
        private BigInteger _interopBlockHeight;
        private BigInteger _currentBlockHeight;
        private OracleReader oracleReader;
        private DateTime lastScan;

        public static Dictionary<string, CryptoCurrencyInfo> NeoTokenInfo = new Dictionary<string, CryptoCurrencyInfo>()
        {
            // symbol name dec plat caps
            { "NEO", new CryptoCurrencyInfo("NEO", "NEO", 0, NeoWallet.NeoPlatform, CryptoCurrencyCaps.Balance) },
            { "GAS", new CryptoCurrencyInfo("GAS", "GAS", 8, NeoWallet.NeoPlatform, CryptoCurrencyCaps.Balance) },
            { "SOUL", new CryptoCurrencyInfo("SOUL", "Phantasma Stake", 8, NeoWallet.NeoPlatform, CryptoCurrencyCaps.Balance) },
        };

        public NeoInterop(TokenSwapper swapper, NeoAPI neoAPI, string wif, PBigInteger interopBlockHeight
            ,OracleReader oracleReader, Logger logger)
                : base(swapper, wif, "neo")
        { 
            BigInteger currentBlockHeight = 0;

            this._interopBlockHeight = new BigInteger(interopBlockHeight.ToUnsignedByteArray()); // currently necessary, neo uses native C# bigint
            this._currentBlockHeight = currentBlockHeight;
            this.neoAPI = neoAPI;

            this.oracleReader = oracleReader;

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
            //////////////////////////////////////////////////////////////////////////////////


            var blockIterator = new BlockIterator(neoAPI);
            Console.WriteLine($"Need to get { blockIterator.currentBlock - _interopBlockHeight } blocks");
            while (blockIterator.currentBlock > _interopBlockHeight)
            {
                Console.WriteLine("Getting block: " + _interopBlockHeight);
                //var url = DomainExtensions.GetOracleBlockURL("neo", "neo", PBigInteger.FromUnsignedArray(_interopBlockHeight.ToByteArray(), true));
                //var url = DomainExtensions.GetOracleBlockURL("neo", "neo", PBigInteger.Parse("5377712"));
                var url = DomainExtensions.GetOracleBlockURL("neo", "neo", PBigInteger.Parse("5377704"));
                Console.WriteLine(url);
                var bytesBlock = oracleReader.Read(DateTime.Now, url);
                Console.Write("got block");
                Thread.Sleep(100000);
                //Console.WriteLine("Get block: " + _interopBlockHeight);
                //TODO get block from chain
                //InteropBlock block = SpookOracle.PullBlock(oracleReader,
                //        NeoWallet.NeoPlatform, NeoWallet.NeoPlatform, null, _interopBlockHeight
                //        );
                _interopBlockHeight++;
            }

            //////////////////////////////////////////////////////////////////////////////////
            //if (json == null)
            //{
            //    logger.Warning("failed to fetch address page");
            //    return Enumerable.Empty<PendingSwap>();
            //}

            //var root = JSONReader.ReadFromString(json);

            //var all = root.GetNode("result");
            //var allTx = all.GetNode("sent");
            //var received = all.GetNode("received");
            //var address = all.GetString("address");

            //for (int i = received.ChildCount - 1; i >= 0; i--)
            //{
            //    allTx.AddNode(received.GetNodeByIndex(i));
            //}

            //logger.Message($"entries: {allTx.ChildCount}");
            //for (int i = allTx.ChildCount - 1; i >= 0; i--)
            //{
            //    var entry = allTx.GetNodeByIndex(i);

            //    var temp = entry.GetString("block_index");
            //    var height = BigInteger.Parse(temp);
            //    //logger.Message($"block_height: {_blockHeight.ToString()} height: {height}");

            //    if (height >= _blockHeight)
            //    {
            //        try
            //        {
            //            ProcessTransaction(entry, result, address);
            //            _blockHeight = height;
            //        }
            //        catch (Exception e)
            //        {
            //            logger.Error("error: " + e.ToString());
            //        }
            //    }
            //}

            //lastScan = DateTime.UtcNow;

            return result;
        }

        private static string FindSymbolFromAsset(string assetID)
        {
            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Remove(0,2) ;
            }
            switch (assetID)
            {
                case "ed07cffad18f1308db51920d99a2af60ac66a7b3": return "SOUL";
                case "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b": return "NEO";
                case "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7": return "GAS";
                default: return null;
            }
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

            var reader = Swapper.Nexus.GetOracleReader();
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

        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(NeoBlock block, NeoAPI api, string swapAddress)
        {
            List<Hash> hashes = new List<Hash>();

            // if the block has no swap tx, it's currently not of interest
            bool blockOfInterest = false;
            List<InteropTransaction> interopTransactions = new List<InteropTransaction>();
            foreach (var tx in block.transactions)
            {
                if (tx.type == TransactionType.InvocationTransaction
                    || tx.type == TransactionType.ContractTransaction)
                {
                    var interopTx = MakeInteropTx(tx, api, swapAddress);
                    if (interopTx.Hash != null)
                    {
                        interopTransactions.Add(interopTx);
                        hashes.Add(Hash.FromBytes(tx.Hash.ToArray()));
                        blockOfInterest = true;
                    }
                }
            }

            InteropBlock iBlock = (blockOfInterest) 
                ? new InteropBlock("neo", "neo", Hash.FromBytes(block.Hash.ToArray()), hashes.ToArray())
                : new InteropBlock("neo", "neo", Hash.Null, hashes.ToArray());

            return Tuple.Create(iBlock, interopTransactions.ToArray());
        }

        public static InteropTransaction MakeInteropTx(NeoTx tx, NeoAPI api, string swapAddress)
        {
            List<InteropTransfer> interopTransfers = new List<InteropTransfer>();
            Console.WriteLine("##### " + tx.Hash);
            //if (tx.Hash.ToString() == "0x9fe68e6c3adac42d53832c76d714fc2c8e5eb96a7edc59ec345c80e03718a019"
            //        || tx.Hash.ToString() == "0x62fdebb396c596c1451df99b0f7d14e809ed686455eeeec73dfce656a0bf39bc")
            //{
            Console.WriteLine("##### 11   " + tx.Hash);

            PBigInteger amount;
            var sourceScriptHash = CryptoUtils.Hash160(tx.witnesses[0].verificationScript);
            var sourceAddress = NeoWallet.EncodeByteArray(sourceScriptHash);
            Console.WriteLine("interop sourceAddress: " + sourceAddress);
            Console.WriteLine("neo sourceAddress: " + NeoWallet.DecodeAddress(sourceAddress));

            if (sourceAddress.ToString() == swapAddress)
            {
                //todo phantasma -> neo swap
            }

            if (tx.outputs.Length > 0)
            {
                Console.WriteLine("UTXO Transaction!!!!!!!!!!!!!!!!");
                foreach (var output in tx.outputs)
                {
                    var targetAddress = NeoWallet.EncodeByteArray(output.scriptHash.ToArray());
                    Console.WriteLine("interop targetAddress : " + targetAddress);
                    Console.WriteLine("neo targetAddress: " + NeoWallet.DecodeAddress(targetAddress));

                    var swpAddress = NeoWallet.EncodeAddress(swapAddress);
                    Console.WriteLine("interop swpAddress: " + swpAddress);
                    Console.WriteLine("neo swpAddress: " + NeoWallet.DecodeAddress(swpAddress));
                    if (targetAddress.ToString() == swapAddress)
                    {
                        var token = FindSymbolFromAsset(new UInt256(output.assetID).ToString());
                        CryptoCurrencyInfo tokenInfo;
                        if (NeoTokenInfo.TryGetValue(token, out tokenInfo))
                        {
                            amount = Phantasma.Numerics.UnitConversion.ToBigInteger(
                                    output.value, tokenInfo.Decimals);
                        }
                        else
                        {
                            // asset not swapable at the moment...
                            return default(InteropTransaction);
                        }

                        Console.WriteLine(amount);
                        interopTransfers.Add
                        (
                            new InteropTransfer
                            (
                                NeoWallet.NeoPlatform,
                                sourceAddress,
                                DomainSettings.PlatformName,
                                targetAddress,
                                Address.Null, // interop address
                                token.ToString(),
                                amount
                            )
                        );
                    }
                }
            }
            //}

            //var script = NeoDisassembler.Disassemble(tx.script);
            //int pos = 0;

            //foreach (var entry in script.lines)
            //{
            //    pos++;
            //    if (pos > 3)
            //    {
            //        // we are only interested in the first three elements
            //        break;
            //    }

            //    if (pos == 1)
            //    {
            //        amount = new BigInteger(entry.data);
            //        Console.WriteLine("Amount: " + amount/new BigInteger(100000000));
            //    }
            //    if (pos == 2 || pos == 3)
            //    {
            //        if (pos ==2)
            //        {
            //            var targetScriptHash = new UInt160(entry.data);
            //            Console.WriteLine("targetScriptHash: " + targetScriptHash);
            //            var targetAddress = NeoWallet.EncodeByteArray(entry.data);
            //            Console.WriteLine("targetAddress: " + targetAddress);
            //            Console.WriteLine("SwapAddress: " + swapAddress);
            //            if (targetScriptHash.ToString() == swapAddress)
            //            {
            //                // found a swap, call getapplicationlog now to get transaction details and verify the tx was actually processed.
            //                ApplicationLog[] appLogs = api.GetApplicationLog(tx.Hash);
            //                for (var i = 0; i < appLogs.Length; i++)
            //                {
            //                    var token = FindSymbolFromAsset(appLogs[i].contract);
            //                    interopTransfers.Add
            //                    (
            //                        new InteropTransfer
            //                        (
            //                            "neo", // todo Pay.Chains.NeoWallet.NeoPlatform
            //                            NeoWallet.EncodeByteArray(appLogs[i].sourceAddress.ToArray()),
            //                            DomainSettings.PlatformName,
            //                            NeoWallet.EncodeByteArray(appLogs[i].targetAddress.ToArray()),
            //                            Address.Null, // interop address
            //                            token.ToString(),
            //                            appLogs[i].amount
            //                        )
            //                    );
            //                }
            //            }
            //        }
            //        else
            //        {
            //            //TODO reverse swap
            //            var sourceScriptHash = new UInt160(entry.data);
            //            var sourceAddress = NeoWallet.EncodeByteArray(sourceScriptHash.ToArray());
            //        }
            //    }
            //}

            return ((interopTransfers.Count() > 0)
                ? new InteropTransaction(Hash.FromBytes(tx.Hash.ToArray()), interopTransfers.ToArray())
                : new InteropTransaction(Hash.Null, interopTransfers.ToArray()));
        }
    }
}
