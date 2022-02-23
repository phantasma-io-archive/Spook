using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Storage;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Ethereum;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;
using Phantasma.RocksDB;
using Phantasma.Spook.Interop;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.VM;
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
                .AllowGas(_cli.NodeKeys.Address, Address.Null, 100000, 15000)
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

        [ConsoleCommand("drop swap", Category = "Node", Description = "Drop a stuck swap")]
        protected void OnDropInProgressSwap(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Source hash needs to be set!");
                return;
            }

            var sourceHash = Hash.Parse(args[0]);

            var inProgressMap = new StorageMap(TokenSwapper.InProgressTag, _cli.TokenSwapper.Storage);

            if (inProgressMap.ContainsKey<Hash>(sourceHash))
            {
                inProgressMap.Remove<Hash>(sourceHash);
            }

            Console.WriteLine($"Removed hash {sourceHash} from in progress map!");
        }
        [ConsoleCommand("create platform", Category = "Node", Description = "Create a token, foreign or native")]
        protected void OnCreatePlatform(string[] args)
        {
            var platform = args[0];

            if (string.IsNullOrEmpty(platform)) 
            {
                Console.WriteLine("platform has to be set!");
                return;
            }

            var nativeCurrency = args[1];
            if (string.IsNullOrEmpty(nativeCurrency)) 
            {
                Console.WriteLine("Native currency has to be set!");
                return;
            }

            var platformKeys = InteropUtils.GenerateInteropKeys(_cli.NodeKeys, _cli.Nexus.GetGenesisHash(_cli.Nexus.RootStorage), platform);
            var platformText = Phantasma.Ethereum.EthereumKey.FromWIF(platformKeys.ToWIF()).Address;
            var platformAddress = Phantasma.Pay.Chains.BSCWallet.EncodeAddress(platformText);

            var script = ScriptUtils.BeginScript()
                .AllowGas(_cli.NodeKeys.Address, Address.Null, 100000, 9999)
                .CallInterop("Nexus.CreatePlatform", _cli.NodeKeys.Address, platform, platformText, platformAddress, nativeCurrency.ToUpper())
                .SpendGas(_cli.NodeKeys.Address).EndScript();

            var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
            var tx = new Phantasma.Blockchain.Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, Spook.TxIdentifier);

            if (tx != null)
            {
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

                Console.WriteLine($"Platform \"{platform}\" created.");
            }
        }

        [ConsoleCommand("create token", Category = "Node", Description = "Create a token, foreign or native")]
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

            if (args.Length < 1)
            {
                Console.WriteLine("Symbol argument is missing!");
                return;
            }

            var symbol = args[0];
            if (string.IsNullOrEmpty(symbol)) 
            {
                Console.WriteLine("Symbol has to be set!");
                return;
            }

            if (args.Length < 2)
            {
                Console.WriteLine("Platform argument is missing!");
                return;
            }

            var platform = args[1];
            if (string.IsNullOrEmpty(platform))
            {
                Console.WriteLine("Platform has to be set!");
                return;
            }

            Transaction tx;
            if (platform == DomainSettings.PlatformName)
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Native token name argument is missing!");
                    return;
                }

                var name = args[2];
                if (string.IsNullOrEmpty(name)) 
                {
                    Console.WriteLine("Native token needs a name");
                    return;
                }

                if (args.Length < 4)
                {
                    Console.WriteLine("Native token decimals argument is missing!");
                    return;
                }

                var success = int.TryParse(args[3], out var decimals);
                if (!success) 
                {
                    Console.WriteLine("Native token needs decimals");
                    return;
                }

                if (args.Length < 5)
                {
                    Console.WriteLine("Native token max supply argument is missing!");
                    return;
                }

                success = int.TryParse(args[4], out var maxSupply);
                if (!success) 
                {
                    Console.WriteLine("Native token needs max supply");
                    return;
                }

                var flags = TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Burnable;

                if (decimals > 0)
                {
                    flags |= TokenFlags.Divisible;
                }

                if (maxSupply > 0)
                {
                    flags |= TokenFlags.Finite;
                }

                tx = GenerateToken(_cli.NodeKeys, symbol, name, maxSupply, decimals, flags);
            }
            else
            {
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

                var val = args.ElementAtOrDefault(3);
                var nativeToken = !string.IsNullOrEmpty(val) && bool.Parse(val);
                Hash hash;
                try
                {
                    hash = Hash.FromUnpaddedHex(hashStr);
                }
                catch (Exception e)
                {
                    if (nativeToken)
                    {
                        hash = Hash.FromString(hashStr.ToUpper());
                    }
                    else
                    {
                        Console.WriteLine("Parsing hash failed: " + e.Message);
                        return;
                    }
                }

                var script = ScriptUtils.BeginScript()
                    .AllowGas(_cli.NodeKeys.Address, Address.Null, 100000, 1500)
                    .CallInterop("Nexus.SetPlatformTokenHash", symbol.ToUpper(), platform, hash)
                    .SpendGas(_cli.NodeKeys.Address).EndScript();

                var expire = Timestamp.Now + TimeSpan.FromMinutes(2);
                tx = new Transaction(_cli.Nexus.Name, _cli.Nexus.RootChain.Name, script, expire, Spook.TxIdentifier);
            }

            if (tx != null)
            {
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

        public Transaction GenerateToken(PhantasmaKeys owner, string symbol, string name, BigInteger totalSupply,
                int decimals, TokenFlags flags, byte[] tokenScript = null, Dictionary<string, int> labels = null, 
                IEnumerable<ContractMethod> customMethods = null, uint seriesID = 0)
        {
            var version = _cli.Nexus.GetGovernanceValue(_cli.Nexus.RootStorage, Nexus.NexusProtocolVersionTag);
            labels ??= new Dictionary<string, int>();

            if (tokenScript == null)
            {
                // small script that restricts minting of tokens to transactions where the owner is a witness
                var addressStr = Base16.Encode(owner.Address.ToByteArray());
                string[] scriptString;

                if (version >= 4)
                {
                    scriptString = new[] {
                    $"alias r3, $result",
                    $"alias r4, $owner",
                    $"@{AccountTrigger.OnMint}: nop",
                    $"load $owner 0x{addressStr}",
                    "push $owner",
                    "extcall \"Address()\"",
                    "extcall \"Runtime.IsWitness\"",
                    "pop $result",
                    $"jmpif $result, @end",
                    $"load r0 \"invalid witness\"",
                    $"throw r0",

                    $"@getOwner: nop",
                    $"load $owner 0x{addressStr}",
                    "push $owner",
                    $"jmp @end",

                    $"@getSymbol: nop",
                    $"load r0 \""+symbol+"\"",
                    "push r0",
                    $"jmp @end",

                    $"@getName: nop",
                    $"load r0 \""+name+"\"",
                    "push r0",
                    $"jmp @end",

                    $"@getMaxSupply: nop",
                    $"load r0 "+totalSupply+"",
                    "push r0",
                    $"jmp @end",

                    $"@getDecimals: nop",
                    $"load r0 "+decimals+"",
                    "push r0",
                    $"jmp @end",

                    $"@getTokenFlags: nop",
                    $"load r0 "+(int)flags+"",
                    "push r0",
                    $"jmp @end",

                    $"@end: ret"
                    };
                }
                else {
                    scriptString = new string[] {
                    $"alias r1, $triggerMint",
                    $"alias r2, $currentTrigger",
                    $"alias r3, $result",
                    $"alias r4, $owner",

                    $@"load $triggerMint, ""{AccountTrigger.OnMint}""",
                    $"pop $currentTrigger",

                    $"equal $triggerMint, $currentTrigger, $result",
                    $"jmpif $result, @mintHandler",
                    $"jmp @end",

                    $"@mintHandler: nop",
                    $"load $owner 0x{addressStr}",
                    "push $owner",
                    "extcall \"Address()\"",
                    "extcall \"Runtime.IsWitness\"",
                    "pop $result",
                    $"jmpif $result, @end",
                    $"load r0 \"invalid witness\"",
                    $"throw r0",

                    $"@end: ret"
                    };
                }
                DebugInfo debugInfo;
                tokenScript = AssemblerUtils.BuildScript(scriptString, "GenerateToken",  out debugInfo, out labels);
            }

            var sb = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 100000, 9999);

            if (version >= 4)
            {
                var triggerMap = new Dictionary<AccountTrigger, int>();

                var onMintLabel = AccountTrigger.OnMint.ToString();
                if (labels.ContainsKey(onMintLabel))
                {
                    triggerMap[AccountTrigger.OnMint] = labels[onMintLabel];
                }

                var methods = AccountContract.GetTriggersForABI(triggerMap);

                if (version >= 6)
                {
                    methods = methods.Concat(new ContractMethod[] {
                        new ("getOwner", VMType.Object, labels, new ContractParameter[0]),
                        new ("getSymbol", VMType.String, labels, new ContractParameter[0]),
                        new ("getName", VMType.String, labels, new ContractParameter[0]),
                        new ("getDecimals", VMType.Number, labels, new ContractParameter[0]),
                        new ("getMaxSupply", VMType.Number, labels, new ContractParameter[0]),
                        new ("getTokenFlags", VMType.Enum, labels, new ContractParameter[0]),
                    }) ;
                }

                if (customMethods != null)
                {
                    methods = methods.Concat(customMethods);
                }

                var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
                var abiBytes = abi.ToByteArray();

                object[] args;

                if (version >= 6)
                {
                    args = new object[] { owner.Address, tokenScript, abiBytes };
                }
                else
                {
                    args = new object[] { owner.Address, symbol, name, totalSupply, decimals, flags, tokenScript, abiBytes };
                }

                sb.CallInterop("Nexus.CreateToken", args);
            }
            else
            {
                sb.CallInterop("Nexus.CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags, tokenScript);
            }

            if (!flags.HasFlag(TokenFlags.Fungible))
            {
                ContractInterface nftABI;
                byte[] nftScript;
                Blockchain.Tokens.TokenUtils.GenerateNFTDummyScript(symbol, name, name, "http://simulator/nft/*", "http://simulator/img/*", out nftScript, out nftABI);
                sb.CallInterop("Nexus.CreateTokenSeries", owner.Address, symbol, new BigInteger(seriesID), totalSupply, TokenSeriesMode.Unique, nftScript, nftABI.ToByteArray());
            }

            sb.SpendGas(owner.Address);
            
            var script = sb.EndScript();

            var tx = MakeTransaction(new IKeyPair[]{_cli.NodeKeys}, ProofOfWork.Minimal, _cli.Nexus.RootChain, script);

            return tx;
        }

        private Transaction MakeTransaction(IEnumerable<IKeyPair> signees, ProofOfWork pow, Chain chain, byte[] script)
        {

            Throw.If(!signees.Any(), "at least one signer required");

            var tx = new Transaction(_cli.Nexus.Name, chain.Name, script, Timestamp.Now + TimeSpan.FromSeconds(Mempool.MaxExpirationTimeDifferenceInSeconds / 2));

            return tx;
        }
    }
}
