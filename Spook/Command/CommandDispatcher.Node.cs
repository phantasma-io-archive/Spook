using System;
using System.Collections;
using System.IO;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Ethereum;
using Phantasma.Pay.Chains;
using Phantasma.RocksDB;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.VM.Utils;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("node start", Category = "Node")]
        protected void OnStartCommand()
        {
            Console.WriteLine("Node starting...");
            _cli.Mempool.StartInThread();
            _cli.Node.StartInThread();
            Console.WriteLine("Node started");
        }

        [ConsoleCommand("node stop", Category = "Node")]
        protected void OnStopCommand()
        {
            Console.WriteLine("Node stopping...");
            _cli.Stop();
            Console.WriteLine("Node stopped");
        }

        [ConsoleCommand("node bounce", Category = "Node", Description = "Bounce a node to reload configuration")]
        protected void OnBounceCommand()
        {
            _cli.Stop();
            _cli.Start();
            Console.WriteLine("Node bounced");
        }

        [ConsoleCommand("show keys", Category = "Node", Description = "Show public address and private key for given platform")]
        protected void onShowInteropKeys(string[] args)
        {
            var wif = args[0];
            if (string.IsNullOrEmpty(wif))
            {
                Console.WriteLine("Wif cannot be empty");
                return;
            }

            var platformName = args[1];
            if (string.IsNullOrEmpty(platformName))
            {
                Console.WriteLine("Wif cannot be empty");
                return;
            }

            var genesisHash = _cli.Nexus.GetGenesisHash(_cli.Nexus.RootStorage);
            var interopKeys = InteropUtils.GenerateInteropKeys(PhantasmaKeys.FromWIF(wif), genesisHash, platformName);

            switch(platformName)
            {
                case EthereumWallet.EthereumPlatform:
                    var ethKeys = EthereumKey.FromWIF(interopKeys.ToWIF());
                    Console.WriteLine($"Platfrom:    {platformName}");
                    Console.WriteLine($"WIF:         {ethKeys.GetWIF()}");
                    Console.WriteLine($"Private key: {ethKeys.PrivateKey.ToHex()}");
                    Console.WriteLine($"Address:     {ethKeys.Address}");
                    break;
                case NeoWallet.NeoPlatform:
                    Console.WriteLine($"Not yet added, feel free to add.");
                    break;
            }
        }

        [ConsoleCommand("get value", Category = "Node", Description = "Show governance value")]
        protected void OnGetValue(string[] args)
        {
            var name = args[0];
            var value = _cli.Nexus.GetGovernanceValue(_cli.Nexus.RootStorage, name);

            Console.WriteLine($"Value: {value}");
        }

        [ConsoleCommand("set value", Category = "Node", Description = "Set governance value")]
        protected void OnSetValue(string[] args)
        {
            var chain = _cli.Nexus.GetChainByName(_cli.Nexus.RootChain.Name);
            var fuelToken = _cli.Nexus.GetTokenInfo(_cli.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var balance = chain.GetTokenBalance(chain.Storage, fuelToken, _cli.NodeKeys.Address);

            if (balance == 0)
            {
                Console.WriteLine("Node wallet needs gas to create a platform token!");
                return;
            }

            var key = args[0];

            if (string.IsNullOrEmpty(key)) 
            {
                Console.WriteLine("Key has to be set!");
                return;
            }

            Phantasma.Numerics.BigInteger value;
            try
            {
                value = Phantasma.Numerics.BigInteger.Parse(args[1]);
            }
            catch
            {
                Console.WriteLine("Value has to be set!");
                return;
            }

            var script = ScriptUtils.BeginScript()
                .AllowGas(_cli.NodeKeys.Address, Address.Null, 100000, 1500)
                .CallContract("governance", "SetValue", key, value)
                .SpendGas(_cli.NodeKeys.Address).EndScript();

            var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
            var tx = new Phantasma.Blockchain.Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, Spook.TxIdentifier);

            tx.Mine((int)ProofOfWork.Minimal);
            tx.Sign(_cli.NodeKeys);

            if (_cli.Mempool != null)
            {
                _cli.Mempool.Submit(tx);
                Console.WriteLine($"Transaction {tx.Hash} submitted to mempool.");
            }
            else
            {
                Console.WriteLine("No mempool available");
                return;
            }

            Console.WriteLine($"SetValue {key}:{value} ts: {tx.Hash}");
        }

        [ConsoleCommand("create token", Category = "Node", Description = "Bounce a node to reload configuration")]
        protected void OnCreatePlatformToken(string[] args)
        {

            var chain = _cli.Nexus.GetChainByName(_cli.Nexus.RootChain.Name);
            var fuelToken = _cli.Nexus.GetTokenInfo(_cli.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var balance = chain.GetTokenBalance(chain.Storage, fuelToken, _cli.NodeKeys.Address);

            if (balance == 0)
            {
                Console.WriteLine("Node wallet needs gas to create a platform token!");
                return;
            }

            var symbol = args[0];

            if (string.IsNullOrEmpty(symbol)) 
            {
                Console.WriteLine("Symbol has to be set!");
                return;
            }

            var platform = args[1];
            if (string.IsNullOrEmpty(platform)) 
            {
                Console.WriteLine("Platform has to be set!");
                return;
            }

            var hashStr = args[2];
            if (string.IsNullOrEmpty(hashStr)) 
            {
                Console.WriteLine("Hash has to be set!");
                return;
            }

            if (hashStr.StartsWith("0x"))
            {
                hashStr = hashStr.Substring(2);
            }

            Hash hash;

            try
            {
                hash = Hash.FromUnpaddedHex(hashStr);
            }
            catch (Exception e)
            {
                Console.WriteLine("Parsing hash failed: " + e.Message);
                return;
            }

            var script = ScriptUtils.BeginScript()
                .AllowGas(_cli.NodeKeys.Address, Address.Null, 100000, 1500)
                .CallInterop("Nexus.SetTokenPlatformHash", symbol.ToUpper(), platform, hash)
                .SpendGas(_cli.NodeKeys.Address).EndScript();

            var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
            var tx = new Phantasma.Blockchain.Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, Spook.TxIdentifier);

            tx.Mine((int)ProofOfWork.Minimal);
            tx.Sign(_cli.NodeKeys);

            if (_cli.Mempool != null)
            {
                _cli.Mempool.Submit(tx);
            }
            else
            {
                Console.WriteLine("No mempool available");
                return;
            }

            Console.WriteLine($"Token {symbol}/{platform} created.");
        }

        [ConsoleCommand("node convert", Category = "Node", Description = "")]
        protected void OnConvertCommand(string[] args)
        {
            // TODO, could actually run in a background thread, with updates written out to console.
            // TODO2, not necessary, it's a one time thing...

            // TODO ugly quickfix, add additional command handler to support commands with multiple args
            string fileStoragePath = null;
            string dbStoragePath = null;
            string verificationPath = null;
            int includeArchives = 0;

            if (args.Length == 2)
            {
                fileStoragePath = args[0];
                dbStoragePath = args[1];
            }
            else if (args.Length == 3)
            {
                fileStoragePath = args[0];
                dbStoragePath = args[1];
                verificationPath = args[2];
            }
            else if (args.Length == 4)
            {
                fileStoragePath = args[0];
                dbStoragePath = args[1];
                verificationPath = args[2];
                includeArchives = Int32.Parse(args[3]);
            }
            
            Func<string, IKeyValueStoreAdapter> fileStorageFactory  = (name)
                => new BasicDiskStore(fileStoragePath);

            Func<string, IKeyValueStoreAdapter> dbStorageFactory    = (name)
                => new DBPartition(Spook.Logger, dbStoragePath);

            Func<string, IKeyValueStoreAdapter> verificationStorageFactory = null;
            if (!string.IsNullOrEmpty(verificationPath))
            {
                verificationStorageFactory = (name) => new BasicDiskStore(verificationPath);
            }

            KeyValueStore<Hash, Archive> fileStorageArchives = null;
            if (includeArchives > 0)
            {
                fileStorageArchives = new KeyValueStore<Hash, Archive>(fileStorageFactory("archives"));
            }

            KeyValueStore<Hash, byte[]> fileStorageContents = new KeyValueStore<Hash, byte[]>(fileStorageFactory("contents"));
            KeyStoreStorage fileStorageRoot     = new KeyStoreStorage(fileStorageFactory("chain.main"));

            KeyValueStore<Hash, Archive> dbStorageArchives = new KeyValueStore<Hash, Archive>(dbStorageFactory("archives"));
            KeyValueStore<Hash, byte[]> dbStorageContents = new KeyValueStore<Hash, byte[]>(dbStorageFactory("contents"));
            KeyStoreStorage dbStorageRoot    = new KeyStoreStorage(dbStorageFactory("chain.main"));

            KeyValueStore<Hash, Archive> fileStorageArchiveVerify = new KeyValueStore<Hash, Archive>(verificationStorageFactory("archives.verify"));
            KeyValueStore<Hash, byte[]> fileStorageContentVerify = new KeyValueStore<Hash, byte[]>(verificationStorageFactory("contents.verify"));
            KeyStoreStorage fileStorageRootVerify = new KeyStoreStorage(verificationStorageFactory("chain.main.verify"));

            int count = 0;

            if (includeArchives > 0)
            {
                Spook.Logger.Message("Starting copying archives...");
                fileStorageArchives.Visit((key, value) =>
                {
                    count++;
                    dbStorageArchives.Set(key, value);
                    var val = dbStorageArchives.Get(key);
                    if (!CompareArchive(val, value))
                    {
                        Spook.Logger.Message($"Archives: NewValue: {value.Hash} and oldValue: {val.Hash} differ, fail now!");
                        Environment.Exit(-1);
                    }
                });
                Spook.Logger.Message($"Finished copying {count} archives...");
                count = 0;
            }

            Spook.Logger.Message("Starting copying content items...");
            fileStorageContents.Visit((key, value) =>
            {
                count++;
                dbStorageContents.Set(key, value);
                var val = dbStorageContents.Get(key);
                Spook.Logger.Message("COUNT: " + count);
                if (!CompareBA(val, value))
                {
                    Spook.Logger.Message($"CONTENTS: NewValue: {Encoding.UTF8.GetString(val)} and oldValue: {Encoding.UTF8.GetString(value)} differ, fail now!");
                    Environment.Exit(-1);
                }
            });

            Spook.Logger.Message("Starting copying root...");
            fileStorageRoot.Visit((key, value) =>
            {
                count++;
                StorageKey stKey = new StorageKey(key);
                dbStorageRoot.Put(stKey, value);
                Spook.Logger.Message("COUNT: " + count);
                var val = dbStorageRoot.Get(stKey);
                if (!CompareBA(val, value))
                {
                    Spook.Logger.Message($"ROOT: NewValue: {Encoding.UTF8.GetString(val)} and oldValue: {Encoding.UTF8.GetString(value)} differ, fail now!");
                    Environment.Exit(-1);
                }
            });
            Spook.Logger.Message($"Finished copying {count} root items...");
            count = 0;

            if (!string.IsNullOrEmpty(verificationPath))
            {
                Spook.Logger.Message($"Create verification stores");

                if (includeArchives > 0)
                {
                    Spook.Logger.Message("Start writing verify archives...");
                    dbStorageArchives.Visit((key, value) =>
                    {
                        count++;
                        // very ugly and might not always work, but should be ok for now
                        byte[] bytes = value.Size.ToUnsignedByteArray();
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(bytes);
                        int size = BitConverter.ToInt32(bytes, 0);

                        var ms = new MemoryStream(new byte[size]);
                        var bw = new BinaryWriter(ms);
                        value.SerializeData(bw);
                        fileStorageContentVerify.Set(key, ms.ToArray());
                    });
                    Spook.Logger.Message($"Finished writing {count} archives...");
                    count = 0;
                }

                Spook.Logger.Message("Start writing content items...");
                dbStorageContents.Visit((key, value) =>
                {
                    count++;
                    Spook.Logger.Message ($"Content: {count}");
                    fileStorageContentVerify.Set(key, value);
                });
                Spook.Logger.Message($"Finished writing {count} content items...");
                count = 0;

                Spook.Logger.Message("Starting writing root...");
                dbStorageRoot.Visit((key, value) =>
                {
                    count++;
                    StorageKey stKey = new StorageKey(key);
                    fileStorageRootVerify.Put(stKey, value);
                    Spook.Logger.Message ($"Wrote: {count}");
                });
                Spook.Logger.Message($"Finished writing {count} root items...");
            }
        }

        static bool CompareArchive(Archive a1, Archive a2)
        {
            return a1.Hash.Equals(a2.Hash);
        }

        static bool CompareBA(byte[] ba1, byte[] ba2)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(ba1, ba2);
        }

    }
}
