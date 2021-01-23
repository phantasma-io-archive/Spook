using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Phantasma.RocksDB;
using Phantasma.Core.Log;
using System.IO;
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

    public class Analyser
    {
        private Dictionary<string, List<BalanceEntry>> balances = new Dictionary<string, List<BalanceEntry>>();
        private Dictionary<string, decimal> totals = new Dictionary<string, decimal>();

        private Nexus nexus;

        private const string outputFolder = "Output";

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

        private void DumpTransactions(Chain chain)
        {
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
                foreach (var token in tokens)
                {
                    DumpBalances(chain, token);
                }
            }
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            Directory.CreateDirectory(outputFolder);

            var analyser = new Analyser();

            analyser.Execute();
        }
    }
}
