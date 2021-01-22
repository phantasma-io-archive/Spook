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

    class Analyser
    {
        static Dictionary<string, List<BalanceEntry>> balances = new Dictionary<string, List<BalanceEntry>>();
        static Dictionary<string, decimal> totals = new Dictionary<string, decimal>();

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

        private static IKeyValueStoreAdapter store;

        static void DumpBalances(string symbol, int decimals)
        {
            var list = new List<BalanceEntry>();
            balances[symbol] = list;

            var prefix = BalanceSheet.MakePrefix(symbol);

            var storage = new KeyStoreStorage(store);

            
            var stakeMapKey = SmartContract.GetKeyForField(NativeContractKind.Stake, "_stakeMap", true);
            var stakeMap = new StorageMap(stakeMapKey, storage);
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

                    var dec = UnitConversion.ToDecimal(amount, decimals);
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
            //Dump($"Total,{symbol},{total}");


            list.Sort((x, y) => y.value.CompareTo(x.value));

            var lines = new List<string>();
            list.ForEach(x => lines.Add($"{x.address},{symbol},{x.value}"));
            File.WriteAllLines($"balances_{symbol}.csv", lines);
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var logger = new ConsoleLogger();
            store = new DBPartition(logger, "New/chain.main");

            DumpBalances("SOUL", DomainSettings.StakingTokenDecimals);
            DumpBalances("KCAL", DomainSettings.FuelTokenDecimals);
            DumpBalances("ETH", 18);
            DumpBalances("NEO", 0);
            DumpBalances("GAS", 8);
            DumpBalances("MKNI", 0);
        }
    }
}
