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

namespace StorageDump
{
    class ConsoleLogger : Logger
    {
        public override void Write(LogEntryKind kind, string msg)
        {
            Console.WriteLine(msg);
        }
    }

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

    public struct TxEntry
    {
        public BigInteger height;
        public string hash;
        public uint timestamp;
        public decimal fee;

        public TxEntry(BigInteger height, string hash, uint timestamp, decimal fee)
        {
            this.height = height;
            this.hash = hash;
            this.timestamp = timestamp;
            this.fee = fee;
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
            if (!token.IsFungible())
            {
                return; // unsupported for now
            }

            var symbol = token.Symbol;

            Console.WriteLine($"Analysing {symbol} balances on {chain.Name} chain");

            var list = new List<BalanceEntry>();
            balances[symbol] = list;

            var prefix = BalanceSheet.MakePrefix(symbol);

            var store = chain.Storage;

            
            var stakeMapKey = SmartContract.GetKeyForField(NativeContractKind.Stake, "_stakeMap", true);
            var stakeMap = new StorageMap(stakeMapKey, store);
            var stakeCount = stakeMap.Count();

            var addresses = new HashSet<Address>();

            decimal total = 0;
            store.Visit((key, value) =>
            {
                if (HasPrefix(prefix, key))
                {
                    var bytes = new byte[key.Length - prefix.Length];
                    ByteArrayUtils.CopyBytes(key, prefix.Length, bytes, 0, bytes.Length);

                    var addr = Address.FromBytes(bytes);

                    BigInteger amount;

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

        private void DumpBlocks(Chain chain)
        {
            Console.WriteLine($"Analysing blocks on {chain.Name} chain");

            var blockList = new List<BlockEntry>();
            var txList = new List<TxEntry>();
            var eventList = new List<EventEntry>();
            var addresses = new Dictionary<string, AddressEntry>();

            for (uint i=1; i<chain.Height; i++)
            {
                var blockHash = chain.GetBlockHashAtHeight(i);
                var block = chain.GetBlockByHash(blockHash);

                blockList.Add(new BlockEntry(block.Height, block.Hash.ToString(), block.Timestamp.Value, block.TransactionCount));

                foreach (var txHash in block.TransactionHashes)
                {
                    var tx = chain.GetTransactionByHash(txHash);

                    var fee = chain.GetTransactionFee(tx);
                    txList.Add(new TxEntry(block.Height, tx.Hash.ToString(), block.Timestamp.Value, UnitConversion.ToDecimal(fee, DomainSettings.FuelTokenDecimals)));

                    var events = block.GetEventsForTransaction(txHash);
                    foreach (var evt in events)
                    {
                        var addr = evt.Address.Text;

                        eventList.Add(new EventEntry(block.Height, tx.Hash.ToString(), block.Timestamp.Value, evt.Kind, addr, evt.Contract, Base16.Encode(evt.Data)));

                        if (!addresses.ContainsKey(addr))
                        {
                            addresses[addr] = new AddressEntry(addr, block.Timestamp.Value);
                        }
                    }
                }
            }

            var lines = new List<string>();
            blockList.ForEach(x => lines.Add($"{x.height},{x.hash},{x.timestamp},{x.txCount}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_blocks.csv", lines);

            lines.Clear();
            txList.ForEach(x => lines.Add($"{x.height},{x.hash},{x.timestamp},{x.fee}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_transactions.csv", lines);

            lines.Clear();
            eventList.ForEach(x => lines.Add($"{x.height},{x.hash},{x.timestamp},{x.kind},{x.contract},{x.address},{x.data}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_events.csv", lines);

            var addressList = addresses.Values.ToList();
            addressList.Sort((x, y) => x.timestamp.CompareTo(y.timestamp));
            lines.Clear();
            addressList.ForEach(x => lines.Add($"{x.timestamp},{x.address}"));
            File.WriteAllLines($"{outputFolder}/{chain.Name}_addresses.csv", lines);
        }

        private void Execute()
        {
            var logger = new ConsoleLogger();

            this.nexus = new Nexus("mainnet", logger,
                (name) => new DBPartition(logger, "New/" + name));

            var chains = nexus.GetChains(nexus.RootStorage).Select(x => nexus.GetChainByName(x)).ToArray();
            var tokens = nexus.GetTokens(nexus.RootStorage).Select(x => nexus.GetTokenInfo(nexus.RootStorage, x)).ToArray();

            foreach (var chain in chains)
            {
                DumpBlocks(chain);

                foreach (var token in tokens)
                {
                    DumpBalances(chain, token);
                }
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
