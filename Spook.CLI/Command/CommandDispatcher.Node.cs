using System;
using System.Collections;
using System.IO;
using System.Text;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
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
            _cli.Mempool.Start();
            _cli.Node.Start();
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
            if (string.IsNullOrEmpty(symbol)) 
            {
                Console.WriteLine("Platform has to be set!");
                return;
            }

            var hashStr = args[2];
            if (string.IsNullOrEmpty(symbol)) 
            {
                Console.WriteLine("Hash has to be set!");
                return;
            }

            if (hashStr.StartsWith("0x"))
            {
                hashStr = hashStr.Substring(2);
            }

            var hash = Hash.FromUnpaddedHex(hashStr);

            var script = ScriptUtils.BeginScript()
                .AllowGas(_cli.NodeKeys.Address, Address.Null, 1, 99999)
                .CallInterop("Nexus.SetTokenPlatformHash", symbol.ToUpper(), platform, hash)
                .SpendGas(_cli.NodeKeys.Address).EndScript();

            var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
            var tx = new Phantasma.Blockchain.Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, CLI.Identifier);

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

            Console.WriteLine("Token {symbol}/{platform} created.");
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
                => new DBPartition(_cli.Logger, dbStoragePath);

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
                _cli.Logger.Message("Starting copying archives...");
                fileStorageArchives.Visit((key, value) =>
                {
                    count++;
                    dbStorageArchives.Set(key, value);
                    var val = dbStorageArchives.Get(key);
                    if (!CompareArchive(val, value))
                    {
                        _cli.Logger.Message($"Archives: NewValue: {value.Hash} and oldValue: {val.Hash} differ, fail now!");
                        Environment.Exit(-1);
                    }
                });
                _cli.Logger.Message($"Finished copying {count} archives...");
                count = 0;
            }

            _cli.Logger.Message("Starting copying content items...");
            fileStorageContents.Visit((key, value) =>
            {
                count++;
                dbStorageContents.Set(key, value);
                var val = dbStorageContents.Get(key);
                _cli.Logger.Message("COUNT: " + count);
                if (!CompareBA(val, value))
                {
                    _cli.Logger.Message($"CONTENTS: NewValue: {Encoding.UTF8.GetString(val)} and oldValue: {Encoding.UTF8.GetString(value)} differ, fail now!");
                    Environment.Exit(-1);
                }
            });

            _cli.Logger.Message("Starting copying root...");
            fileStorageRoot.Visit((key, value) =>
            {
                count++;
                StorageKey stKey = new StorageKey(key);
                dbStorageRoot.Put(stKey, value);
                _cli.Logger.Message("COUNT: " + count);
                var val = dbStorageRoot.Get(stKey);
                if (!CompareBA(val, value))
                {
                    _cli.Logger.Message($"ROOT: NewValue: {Encoding.UTF8.GetString(val)} and oldValue: {Encoding.UTF8.GetString(value)} differ, fail now!");
                    Environment.Exit(-1);
                }
            });
            _cli.Logger.Message($"Finished copying {count} root items...");
            count = 0;

            if (!string.IsNullOrEmpty(verificationPath))
            {
                _cli.Logger.Message($"Create verification stores");

                if (includeArchives > 0)
                {
                    _cli.Logger.Message("Start writing verify archives...");
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
                    _cli.Logger.Message($"Finished writing {count} archives...");
                    count = 0;
                }

                _cli.Logger.Message("Start writing content items...");
                dbStorageContents.Visit((key, value) =>
                {
                    count++;
                    _cli.Logger.Message ($"Content: {count}");
                    fileStorageContentVerify.Set(key, value);
                });
                _cli.Logger.Message($"Finished writing {count} content items...");
                count = 0;

                _cli.Logger.Message("Starting writing root...");
                dbStorageRoot.Visit((key, value) =>
                {
                    count++;
                    StorageKey stKey = new StorageKey(key);
                    fileStorageRootVerify.Put(stKey, value);
                    _cli.Logger.Message ($"Wrote: {count}");
                });
                _cli.Logger.Message($"Finished writing {count} root items...");
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
