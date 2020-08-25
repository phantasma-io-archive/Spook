using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Phantasma.Domain;

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
        private List<WebSocketClient> clients = new List<WebSocketClient>();
        private List<Web3> web3Clients = new List<Web3>();
        private Blockchain.Nexus Nexus;
        private SpookSettings _settings;

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

            var clientURLs = this._settings.Oracle.EthRpcNodes;
            this.urls = clientURLs;
            if (clientURLs.Count == 0)
            {
                throw new ArgumentNullException("Need at least one RPC node");
            }

            foreach(var url in clientURLs)
            {
                clients.Add(new WebSocketClient("ws://"+url));
                web3Clients.Add(new Web3(account, "http://"+url));
            }
        }

        public virtual void SetLogger(Action<string> logger = null)
        {
            this._logger = logger;
        }

        private void DummyLogger(string s)
        {

        }

        internal static Dictionary<string, string> GetAssetsInfo()
        {
            if (_systemAssets == null)
            {
                _systemAssets = new Dictionary<string, string>();
                AddAsset("NEO", "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b");
                AddAsset("GAS", "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7");
            }

            return _systemAssets;
        }

        private static void AddAsset(string symbol, string hash)
        {
            _systemAssets[symbol] = hash;
        }

        public static byte[] GetAssetID(string symbol)
        {
            var info = GetAssetsInfo();
            foreach (var entry in info)
            {
                if (entry.Key == symbol)
                {
                    return new byte[0]; //LuxUtils.ReverseHex(entry.Value).HexToBytes();
                }
            }

            return null;
        }

        public static IEnumerable<KeyValuePair<string, string>> Assets
        {
            get
            {
                var info = GetAssetsInfo();
                return info;
            }
        }

        public BigInteger GetBlockHeight()
        {
            //while (true)
            //{
            //    try
            //    {
            //        var ethBlockNumber = new EthBlockNumber(GetClient());
            //        Console.WriteLine("get height start");
            //        var height = EthUtils.RunSync(() => ethBlockNumber.SendRequestAsync()).Value;
            //        Console.WriteLine("get height done: " + height);
            //        return height;
            //    }
            //    catch
            //    {
            //        Console.WriteLine("request failed, retrying");
            //    }
            //}

            var ethBlockNumber = new EthBlockNumber(GetClient());
            var height = Task.Run(async() => await ethBlockNumber.SendRequestAsync()).Result.Value;
            return height;
        }

        public BlockWithTransactions GetBlock(BigInteger height)
        {
            return GetBlock(new HexBigInteger(height));
        }

        public BlockWithTransactions GetBlock(HexBigInteger height)
        {
            var ethGetBlockByNumber = new EthGetBlockWithTransactionsByNumber(GetClient());
            return EthUtils.RunSync(() => ethGetBlockByNumber.SendRequestAsync(new BlockParameter(height)));
                    
        }

        public BlockWithTransactions GetBlock(string hash)
        {
            var ethGetBlockByHash = new EthGetBlockWithTransactionsByHash(GetClient());
            return EthUtils.RunSync(() => ethGetBlockByHash.SendRequestAsync(hash));
        }

        public Transaction GetTransaction(string tx)
        {
            var getTransaction = new EthGetTransactionByHash(GetClient());
            var transaction = EthUtils.RunSync(() => getTransaction.SendRequestAsync(tx));

            return transaction;
        }

        public TransactionReceipt GetTransactionReceipt(string tx)
        {
            var getTransactionReceipt = new EthGetTransactionReceipt(GetClient());

            TransactionReceipt receipt = null;
            if (!tx.StartsWith("0x"))
            {
                tx = "0x"+tx.ToLower();
            }
            while (receipt == null)
            {
                receipt = EthUtils.RunSync(() => getTransactionReceipt.SendRequestAsync(tx));
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
                    //test
                    //var balanceOfFunctionMessage = new BalanceOfFunction()
                    //{
                    //    Owner = "0x18d891f9a6bf84bc1aa431e643fc17c466d9ecd6",
                    //};
                    //
                    //var balanceHandler = GetWeb3Client().Eth.GetContractQueryHandler<BalanceOfFunction>();
                    //var balance = balanceHandler.QueryAsync<BigInteger>(contractAddress, balanceOfFunctionMessage).Result;
                    //Console.WriteLine("Before balance: :::: " + balance);

                    var transferHandler = GetWeb3Client().Eth.GetContractTransactionHandler<TransferFunction>();

                    var result = EthUtils.RunSync(() => transferHandler
                            .SendRequestAndWaitForReceiptAsync(contractAddress, transfer));

                    //balance = balanceHandler.QueryAsync<BigInteger>(contractAddress, balanceOfFunctionMessage).Result;
                    //Console.WriteLine("After balance: :::: " + balance);
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

        public IClient GetClient()
        {
            int idx = rnd.Next(clients.Count);
            return clients[idx];
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
