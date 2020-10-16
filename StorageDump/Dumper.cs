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

namespace StorageDump
{
    class Dumper
    {
        const bool PrettyPrint = false;

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

        static byte[] GetKeyForField<T>(string fieldName) where T: SmartContract
        {
            var contractName = typeof(T).Name.Replace("Contract", "").ToLower();
            return Encoding.UTF8.GetBytes($".{contractName}.{fieldName}");
        }

        private static BasicDiskStore store;

        static void DumpBalances(string symbol, int decimals)
        {
            var prefix = BalanceSheet.MakePrefix(symbol);

            var storage = new KeyStoreStorage(store);

            var stakeMapKey = GetKeyForField<StakeContract>("_stakes");
            var stakeMap = new StorageMap(stakeMapKey, storage);
            var stakeCount = stakeMap.Count();

            var addresses = new HashSet<Address>();

            //Console.WriteLine($"************ BALANCE LIST FOR {symbol} ************");

            decimal total = 0;
            store.Visit((key, value) =>
            {
                if (HasPrefix(prefix, key))
                {
                    var bytes = new byte[key.Length - prefix.Length];
                    ByteArrayUtils.CopyBytes(key, prefix.Length, bytes, 0, bytes.Length);

                    var addr = Address.FromBytes(bytes);
                    if (addr.IsSystem || addr.Text == "P2KFNXEbt65rQiWqogAzqkVGMqFirPmqPw8mQyxvRKsrXV8")
                    {
                        return;
                    }

                    BigInteger amount;

                    if (value.Length > 0)
                    {
                        amount = BigInteger.FromSignedArray(value);
                    }
                    else
                    {
                        amount = 0;
                    }

                    /*if (symbol == DomainSettings.StakingTokenSymbol && stakeMap.ContainsKey<Address>(addr))
                    {
                        var temp = stakeMap.Get<Address, EnergyStake>(addr);
                        amount += temp.stakeAmount;
                    }*/

                    addresses.Add(addr);

                    string s;

                    if (PrettyPrint)
                    {
                        var dec = UnitConversion.ToDecimal(amount, decimals);
                        total += dec;
                        s = dec.ToString();
                    }
                    else
                    {
                        s = amount.ToString();
                    }

                    Console.WriteLine($"{addr.Text},{symbol},{s}");
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

            if (PrettyPrint)
            {
                Console.WriteLine($"Total,{symbol},{total}");
            }
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            store = new BasicDiskStore(@"chain.main.csv");

            store.Visit((key, val) =>
            {
                var name = Encoding.UTF8.GetString(key);
                Console.WriteLine(name);
                Console.ReadKey();
            }, uint.MaxValue, new byte[0]);
            /*
            DumpBalances("SOUL", DomainSettings.StakingTokenDecimals);
            DumpBalances("NEO", 0);
            DumpBalances("GAS", 8);
            DumpBalances("MKNI", 0);
            DumpBalances("KCAL", DomainSettings.FuelTokenDecimals);*/

            Console.ReadKey();
        }
    }
}
