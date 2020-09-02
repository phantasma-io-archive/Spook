using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Phantasma.Core.Log;

namespace Phantasma.Spook.Chains
{
    public class BlockIterator
    {
        public BigInteger currentBlock;
        public uint currentTransaction;

        public BlockIterator(EthAPI api)
        {
            this.currentBlock = api.GetBlockHeight();
            this.currentTransaction = 0;
        }

        public override string ToString()
        {
            return $"{currentBlock}/{currentTransaction}";
        }
    }

    public class EthereumException : Exception
    {
        public EthereumException(string msg) : base(msg)
        {

        }

        public EthereumException(string msg, Exception cause) : base(msg, cause)
        {

        }
    }

    public class EthAPI
    {
        private static Dictionary<string, string> _systemAssets = null;
        public string LastError { get; protected set; }
        private List<string> urls = new List<string>();
        private List<Web3> web3Clients = new List<Web3>();
        private Blockchain.Nexus Nexus;
        private SpookSettings _settings;
        private Account _account;

        private static Random rnd = new Random();

        private Action<string> _logger;
        public Action<string> Logger
        {
            get
            {
                return _logger != null ? _logger : DummyLogger;
            }
        }

        public EthAPI(Blockchain.Nexus nexus, SpookSettings settings, Account account)
        {
            this.Nexus = nexus;
            this._settings = settings;
            this._account = account;

            this.urls = this._settings.Oracle.EthRpcNodes;
            if (this.urls.Count == 0)
            {
                throw new ArgumentNullException("Need at least one RPC node");
            }

            foreach (var url in this.urls)
            {
                web3Clients.Add(new Web3(_account, "https://"+url));
            }

        }

        public virtual void SetLogger(Action<string> logger = null)
        {
            this._logger = logger;
        }

        private void DummyLogger(string s)
        {

        }

        public BigInteger GetBlockHeight()
        {
            var height = Task.Run(async() => await GetWeb3Client().Eth.Blocks.GetBlockNumber.SendRequestAsync()).Result.Value;
            return height;
        }

        public BlockWithTransactions GetBlock(BigInteger height)
        {
            return GetBlock(new HexBigInteger(height));
        }

        public BlockWithTransactions GetBlock(HexBigInteger height)
        {
            return EthUtils.RunSync(() => GetWeb3Client().Eth.Blocks.GetBlockWithTransactionsByNumber
                    .SendRequestAsync(new BlockParameter(height)));
                    
        }

        public BlockWithTransactions GetBlock(string hash)
        {
            return EthUtils.RunSync(() => GetWeb3Client().Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(hash));
        }

        public Transaction GetTransaction(string tx)
        {
            var transaction = EthUtils.RunSync(() => GetWeb3Client().Eth.Transactions.GetTransactionByHash.SendRequestAsync(tx));
            return transaction;
        }

        public TransactionReceipt GetTransactionReceipt(string tx)
        {
            TransactionReceipt receipt = null;
            if (!tx.StartsWith("0x"))
            {
                tx = "0x"+tx.ToLower();
            }

            while (receipt == null)
            {
                receipt = EthUtils.RunSync(() => GetWeb3Client().Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tx));
            }

            return receipt;
        }

        public List<EventLog<TransferEventDTO>> GetTransferEvents(string contract, BigInteger start, BigInteger end)
        {
            return GetTransferEvents(contract, new HexBigInteger(start), new HexBigInteger(end));
        }

        public List<EventLog<TransferEventDTO>> GetTransferEvents(string contract, HexBigInteger start, HexBigInteger end)
        {

            var transferEventHandler = GetWeb3Client().Eth.GetEvent<TransferEventDTO>(contract);

            var filter = transferEventHandler.CreateFilterInput(
                    fromBlock: new BlockParameter(start),
                    toBlock: new BlockParameter(end));

            var logs = EthUtils.RunSync(() => transferEventHandler.GetAllChanges(filter));

            return logs;

        }

        public TransactionReceipt TransferAsset(string symbol, string toAddress, decimal amount, int decimals)
        {
            Console.WriteLine($"Transferring {amount} {symbol} to {toAddress}!");
            if (symbol.Equals("ETH", StringComparison.InvariantCultureIgnoreCase))
            {
                return EthUtils.RunSync(() => GetWeb3Client().Eth.GetEtherTransferService()
                        .TransferEtherAndWaitForReceiptAsync(toAddress, amount));
            }
            else
            {

                var transfer = new TransferFunction()
                {
                    To = toAddress,
                    TokenAmount = Nethereum.Util.UnitConversion.Convert.ToWei(amount, decimals)
                };

                string contractAddress;
                var hash = Nexus.GetTokenPlatformHash(symbol, "ethereum", Nexus.RootStorage);
                if (!hash.IsNull)
                {
                    contractAddress = hash.ToString().Substring(0, 40);
                    var transferHandler = GetWeb3Client().Eth.GetContractTransactionHandler<TransferFunction>();

                    var result = EthUtils.RunSync(() => transferHandler
                            .SendRequestAndWaitForReceiptAsync(contractAddress, transfer));
                    return result;

                }

                return null;
            }
        }

        public Web3 GetWeb3Client()
        {
            int idx = rnd.Next(web3Clients.Count);
            return web3Clients[idx];
        }

        public string GetURL()
        {
            int idx = rnd.Next(urls.Count);
            return "http://" + urls[idx];
        }
    }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public string Owner { get; set; }
    }

    [Function("transfer", "bool")]
    public class TransferFunction : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public string To { get; set; }

        [Parameter("uint256", "_value", 2)]
        public BigInteger TokenAmount { get; set; }
    }
}
