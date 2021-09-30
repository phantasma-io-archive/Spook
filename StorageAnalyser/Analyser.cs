using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using Phantasma.RocksDB;
using Phantasma.Core.Log;
using Phantasma.Blockchain;
using Phantasma.Storage;

namespace StorageDump
{
    public struct BlockEntry
    {
        public BigInteger height;
        public string hash;
        public uint timestamp;
        public int txCount;

        public BlockEntry(BigInteger height, string hash, uint timestamp, int txCount)
        {
            this.height = height;
            this.hash = hash;
            this.timestamp = timestamp;
            this.txCount = txCount;
        }
    }

    public enum TxType
    {
        Transfer,
        Stake,
        Claim,
        SwapOut,
        SwapIn,
        SwapFee,
        SwapCosmic,
        Mint,
        Burn,
        Name,
        Market,
        Other
    }

    public struct TxEntry
    {
        public BigInteger height;
        public string hash;
        public uint timestamp;
        public decimal fee;
        public TxType type;

        public TxEntry(BigInteger height, string hash, uint timestamp, decimal fee, TxType type)
        {
            this.height = height;
            this.hash = hash;
            this.timestamp = timestamp;
            this.fee = fee;
            this.type = type;
        }
    }

    public struct EventEntry
    {
        public BigInteger height;
        public string hash;
        public uint timestamp;
        public EventKind kind;
        public string address;
        public string contract;
        public string data;

        public EventEntry(BigInteger height, string hash, uint timestamp, EventKind kind, string address, string contract, string data)
        {
            this.height = height;
            this.hash = hash;
            this.timestamp = timestamp;
            this.kind = kind;
            this.address = address;
            this.contract = contract;
            this.data = data;
        }
    }

    public struct TransferEntry
    {
        public BigInteger height;
        public string hash;
        public uint timestamp;
        public EventKind kind;
        public string address;
        public string symbol;
        public string amount;
        public string balance;
        public string chainBalance;

        public TransferEntry(BigInteger height, string hash, uint timestamp, EventKind kind, string address,
                string symbol, string amount, string balance, string chainBalance)
        {
            this.height = height;
            this.hash = hash;
            this.timestamp = timestamp;
            this.kind = kind;
            this.address = address;
            this.symbol = symbol;
            this.amount = amount;
            this.balance = balance;
            this.chainBalance = chainBalance;
        }
    }

    public struct BalanceEntry
    {
        public readonly string address;
        public readonly decimal value;

        public BalanceEntry(string address, decimal value)
        {
            this.address = address;
            this.value = value;
        }
    }

    public struct AddressEntry
    {
        public readonly string address;
        public readonly uint timestamp;

        public AddressEntry(string address, uint timestamp)
        {
            this.address = address;
            this.timestamp = timestamp;
        }
    }

    public class Analyser
    {
        private Dictionary<string, List<BalanceEntry>> balances = new Dictionary<string, List<BalanceEntry>>();
        private Dictionary<string, decimal> totals = new Dictionary<string, decimal>();

        private Nexus nexus;

        private string outputFolder;

        public Analyser(string outputFolder)
        {
            this.outputFolder = outputFolder;
        }

        static bool HasPrefix(byte[] prefix, byte[] key)
        {
            if (key.Length < prefix.Length)
            {
                return false;
            }

            for (int i=0; i<prefix.Length; i++)
            {
                if (key[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void DumpBalances(Chain chain, IToken token)
        {
            var symbol = token.Symbol;

            Console.WriteLine($"Analysing {symbol} balances on {chain.Name} chain");

            var list = new List<BalanceEntry>();
            balances[symbol] = list;

            var fungible = token.IsFungible();

            var prefix = fungible ? BalanceSheet.MakePrefix(symbol) : System.Text.Encoding.UTF8.GetBytes($".ids.{symbol}");

            var store = chain.Storage;
            
            var stakeMapKey = SmartContract.GetKeyForField(NativeContractKind.Stake, "_stakeMap", true);
            var stakeMap = new StorageMap(stakeMapKey, store);
            //var stakeCount = stakeMap.Count();

            var addresses = new HashSet<Address>();

            decimal total = 0;
            store.Visit((key, value) =>
            {
                if (HasPrefix(prefix, key))
                {
                    var diff = key.Length - prefix.Length;
                    if (diff < Address.LengthInBytes)
                    {
                        return;
                    }

                    var bytes = new byte[Address.LengthInBytes];

                    ByteArrayUtils.CopyBytes(key, prefix.Length, bytes, 0, bytes.Length);

                    var addr = Address.FromBytes(bytes);

                    BigInteger amount;

                    if (!fungible)
                    {
                        if (addresses.Contains(addr))
                        {
                            return; // already visited
                        }

                        var newKey = ByteArrayUtils.ConcatBytes(prefix, bytes);
                        var map = new StorageMap(newKey, store);

                        amount = map.Count();
                    }
                    else
                    {
                        if (value.Length > 0)
                        {
                            amount = BigInteger.FromSignedArray(value);
                        }
                        else
                        {
                            amount = 0;
                        }

                        if (symbol == DomainSettings.StakingTokenSymbol && stakeMap.ContainsKey<Address>(addr))
                        {
                            var temp = stakeMap.Get<Address, EnergyStake>(addr);
                            amount += temp.stakeAmount;
                        }
                    }

                    if (amount < 1)
                    {
                        return; 
                    }

                    addresses.Add(addr);

                    var dec = UnitConversion.ToDecimal(amount, token.Decimals);
                    total += dec;


                    list.Add(new BalanceEntry(addr.Text, dec));
                }
            }, uint.MaxValue, new byte[0]);

            /*
            if (symbol == DomainSettings.StakingTokenSymbol)
            {
                var masterListKey = GetKeyForField<StakeContract>("_mastersList");
                var masterList = new StorageList(masterListKey, storage);
                var masterCount = masterList.Count();
                for (int i = 0; i < masterCount; i++)
                {
                    var master = masterList.Get<EnergyMaster>(i);
                    if (addresses.Contains(master.address))
                    {
                        continue;
                    }

                    var temp = stakeMap.Get<Address, EnergyAction>(master.address);

                    string s;

                    if (PrettyPrint)
                    {
                        var stake = UnitConversion.ToDecimal(temp.totalAmount, decimals);
                        s = stake.ToString();
                        total += stake;
                    }
                    else
                    {
                        s = temp.totalAmount.ToString();
                    }

                    Console.WriteLine($"{master.address.Text},{symbol},{s}");
                }
            }*/

            totals[symbol] = total;

            if (list.Count == 0)
            {
                return;
            }

            list.Sort((x, y) => y.value.CompareTo(x.value));

            var lines = new List<string>();
            list.ForEach(x => lines.Add($"{x.address},{symbol},{x.value}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_balances_{symbol}.csv", lines);
        }

        private TxType DetectTransactionType(IEnumerable<Event> events)
        {
            if (events.Any())
            {
                var first = events.First();

                switch (first.Contract)
                {
                    case "stake":
                        switch (first.Kind)
                        {
                            case EventKind.TokenClaim:
                            case EventKind.TokenMint:
                                return TxType.Claim;

                            case EventKind.TokenStake:
                                return TxType.Stake;
                        }
                        break;

                    case "interop":
                        switch (first.Kind)
                        {
                            case EventKind.TokenSend:
                            case EventKind.TokenStake:
                                return TxType.SwapIn;

                            case EventKind.TokenClaim:
                            case EventKind.TokenMint:
                                return TxType.SwapOut;

                        }
                        break;

                    case "entry":
                        switch (first.Kind)
                        {
                            case EventKind.TokenSend:
                                return TxType.Transfer;

                            case EventKind.TokenMint:
                                return TxType.Mint;

                            case EventKind.TokenBurn:
                                return TxType.Burn;
                        }
                        break;

                    case "account":
                        switch (first.Kind)
                        {
                            case EventKind.AddressRegister:
                                return TxType.Name;
                        }
                        break;

                    case "market":
                            return TxType.Market;

                    case "swap":
                        switch (first.Kind)
                        {
                            case EventKind.TokenStake:
                                return TxType.SwapCosmic;
                        }
                        break;

                    default:
                        switch (first.Kind)
                        {
                            case EventKind.TokenMint:
                                return TxType.Mint;

                            case EventKind.TokenBurn:
                                return TxType.Burn;
                        }
                        break;
                }


                return TxType.Other;
            }
            else
            {
                return TxType.SwapFee;
            }
        }

        private void DumpBlocks(Chain chain)
        {
            Console.WriteLine($"Analysing blocks on {chain.Name} chain");

            var blockList = new List<BlockEntry>();
            var txList = new List<TxEntry>();
            var eventList = new List<EventEntry>();
            var transferList = new List<TransferEntry>();
            var addresses = new Dictionary<string, AddressEntry>();
            var balances = new Dictionary<string, BigInteger>();

            for (uint i=1; i<chain.Height; i++)
            {
                var blockHash = chain.GetBlockHashAtHeight(i);
                var block = chain.GetBlockByHash(blockHash);

                blockList.Add(new BlockEntry(block.Height, block.Hash.ToString(), block.Timestamp.Value, block.TransactionCount));

                foreach (var txHash in block.TransactionHashes)
                {
                    var tx = chain.GetTransactionByHash(txHash);

                    var fee = chain.GetTransactionFee(tx);
                    var events = block.GetEventsForTransaction(txHash);

                    var txType = DetectTransactionType(events.Where(x => x.Contract != "gas"));
                    txList.Add(new TxEntry(block.Height, tx.Hash.ToString(), block.Timestamp.Value, UnitConversion.ToDecimal(fee, DomainSettings.FuelTokenDecimals), txType));

                    foreach (var evt in events)
                    {
                        var addr = evt.Address.Text;

                        eventList.Add(new EventEntry(block.Height, tx.Hash.ToString(), block.Timestamp.Value, evt.Kind, addr, evt.Contract, Base16.Encode(evt.Data)));

                        if (!addresses.ContainsKey(addr))
                        {
                            addresses[addr] = new AddressEntry(addr, block.Timestamp.Value);
                        }

                        int mult = 0;
                        switch (evt.Kind)
                        {
                            case EventKind.TokenReceive:
                            case EventKind.TokenClaim:
                            case EventKind.TokenMint:
                                mult = 1;
                                break;

                            case EventKind.TokenStake:
                            case EventKind.TokenBurn:
                            case EventKind.TokenSend:
                                mult = -1;
                                break;
                        }

                        if (mult != 0)
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data);
                            var amount = data.Value * mult;
                            var key = addr + data.Symbol;

                            BigInteger balance = balances.ContainsKey(key) ? balances[key] : 0;
                            var chainBalance = chain.GetTokenBalance(chain.Storage, data.Symbol, Address.FromText(addr));

                            transferList.Add(new TransferEntry(block.Height, tx.Hash.ToString(), block.Timestamp.Value, evt.Kind, addr, data.Symbol, amount.ToString(), balance.ToString(), chainBalance.ToString()));

                            balance += amount;
                            balances[key] = balance;
                        }
                    }
                }
            }

            var lines = new List<string>();
            blockList.ForEach(x => lines.Add($"{x.height},{x.hash},{x.timestamp},{x.txCount}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_blocks.csv", lines);

            lines.Clear();
            txList.ForEach(x => lines.Add($"{x.height},{x.hash},{x.timestamp},{x.fee},{x.type}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_transactions.csv", lines);

            lines.Clear();
            eventList.ForEach(x => lines.Add($"{x.height},{x.hash},{x.timestamp},{x.kind},{x.contract},{x.address},{x.data}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_events.csv", lines);

            lines.Clear();
            transferList.ForEach(x => lines.Add($"{x.height},{x.hash},{x.timestamp},{x.kind},{x.address},{x.symbol},{x.amount},{x.balance},{x.chainBalance}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_transfers.csv", lines);

            var addressList = addresses.Values.ToList();
            addressList.Sort((x, y) => x.timestamp.CompareTo(y.timestamp));
            lines.Clear();
            addressList.ForEach(x => lines.Add($"{x.timestamp},{x.address}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_addresses.csv", lines);

            /*string connstring = string.Format("Server={0}; database={1}; UID={2}; password={3}", "localhost", "phantasma", "root", "root");
            var connection = new MySqlConnection(connstring);
            connection.Open();

            ExecuteQuery(connection, "DROP TABLE IF EXISTS `events`;);*/
        }

        /*private void ExecuteQuery(MySqlConnection connection, string query)
        {

        }*/

        private void Execute()
        {
            var logger = new ConsoleLogger(LogLevel.Maximum);

            var path = @"Storage/";

            this.nexus = new Nexus("mainnet", logger,
                (name) => new DBPartition(logger, path + name));

            if (!nexus.HasGenesis)
            {
                throw new Exception("Genesis block not found, check storage path");
            }

            this.nexus.SetOracleReader(new DummyOracle(this.nexus));

            var chains = nexus.GetChains(nexus.RootStorage).Select(x => nexus.GetChainByName(x)).ToArray();
            var tokens = nexus.GetTokens(nexus.RootStorage).Select(x => nexus.GetTokenInfo(nexus.RootStorage, x)).ToArray();

            foreach (var chain in chains)
            {
                foreach (var token in tokens)
                {
                    DumpBalances(chain, token);
                }

                DumpBlocks(chain);
            }
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var outputFolder = args.Length > 0 ? args[0] : "Output";
            Directory.CreateDirectory(outputFolder);

            var analyser = new Analyser(outputFolder);

            analyser.Execute();
        }
    }
}
