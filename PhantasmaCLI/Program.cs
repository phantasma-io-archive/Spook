using System;
using System.Collections.Generic;
using System.Threading;
using Phantasma.Blockchain.Consensus;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Core.Log;
using Phantasma.Tests;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Tokens;
using LunarLabs.Parser.JSON;
using Phantasma.API;

namespace Phantasma.CLI
{
    public class TPSPlugin : IChainPlugin
    {
        private int periodInSeconds;
        private int txCount;
        private DateTime lastTime = DateTime.UtcNow;
        private Logger log;

        public TPSPlugin(Logger log, int periodInSeconds)
        {
            this.log = log;
            this.periodInSeconds = periodInSeconds;
        }

        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
        {
            txCount++;
            
            var currentTime = DateTime.UtcNow;
            var diff = (currentTime - lastTime).TotalSeconds;

            if (diff >= periodInSeconds)
            {
                lastTime = currentTime;
                var tps = txCount / (float)periodInSeconds;
                log.Message($"{tps.ToString("0.##")} TPS");
                txCount = 0;
            }
        }
    }

    class Program
    {
        private static bool running = false;

        private static readonly string[] validatorWIFs = new string[]
        {
            "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", //P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr
            "L1sEB8Z6h5Y7aQKqxbAkrzQLY5DodmPacjqSkFPmcR82qdmHEEdY", // PGBinkbZA3Q6BxMnL2HnJSBubNvur3iC6GtQpEThDnvrr
            "KxWUCAD2wECLfA7diT7sV7V3jcxAf9GSKqZy3cvAt79gQLHQ2Qo8", // PDiqQHDwe6MTcP6TH6DYjq7FTUouvy2YEkDXz2chCABCb
        };

        static void PrintChain(Chain chain)
        {
            Console.WriteLine("Listing blocks...");
            foreach (var block in chain.Blocks)
            {
                Console.WriteLine("Block #" + block.Height);
                Console.WriteLine("\tHash: " + block.Hash);

                Console.WriteLine("\tTransactions: ");
                int index = 0;
                foreach (var hash in block.TransactionHashes)
                {
                    var tx = chain.FindTransactionByHash(hash);
                    Console.WriteLine("\t\tTransaction #" + index);
                    Console.WriteLine("\t\tHash: " + tx.Hash);
                    Console.WriteLine();

                    index++;
                }

                Console.WriteLine("\tEvents: ");
                index = 0;
                foreach (var hash in block.TransactionHashes)
                {
                    var events = block.GetEventsForTransaction(hash);

                    foreach (var evt in events)
                    {
                        Console.WriteLine("\t\tEvent #" + index);
                        Console.WriteLine("\t\tKind: " + evt.Kind);
                        Console.WriteLine("\t\tTarget: " + evt.Address.Text);
                        Console.WriteLine();

                        index++;
                    }
                }

                Console.WriteLine();
            }
        }

        private static Hash SendTransfer(JSONRPC_Client rpc, Logger log, string host, KeyPair from, Address to, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(from.Address, 1, 9999).TransferTokens("SOUL", from.Address, to, amount).SpendGas(from.Address).EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30), 0);
            tx.Sign(from);

            var bytes = tx.ToByteArray(true);

            //log.Debug("RAW: " + Base16.Encode(bytes));

            var response = rpc.SendRequest(RequestType.POST, host, "sendRawTransaction", Base16.Encode(bytes));
            if (response == null)
            {
                if (log != null)
                {
                    log.Error("Transfer request failed");
                }
                return Hash.Null;
            }

            var hash = response.GetString("hash");
            if (string.IsNullOrEmpty(hash))
            {
                if (log != null)
                {
                    log.Error("Hash not found");
                }
                return Hash.Null;
            }

            return Hash.Parse(hash);
        }

        static bool ConfirmTransaction(JSONRPC_Client rpc, Logger log, string host, Hash hash, int maxTries = 99999)
        {
            var hashStr = hash.ToString();

            int tryCount = 0;

            do
            {
                var response = rpc.SendRequest(RequestType.POST, host, "getConfirmations", hashStr);
                if (response == null)
                {
                    if (log != null)
                    {
                        log.Error("Transfer request failed");
                    }
                    return false;
                }

                var confirmations = response.GetInt32("confirmations");
                if (confirmations > 0)
                {
                    if (log != null)
                    {
                        log.Success("Confirmations: " + confirmations);
                    }
                    return true;
                }

                tryCount--;
                if (tryCount >= maxTries)
                {
                    return false;
                }

                Thread.Sleep(1000);
            } while (true);
        }

        static void SenderSpawn(Logger log, string wif, string host)
        {
            Thread.CurrentThread.IsBackground = true;

            var currentKey = KeyPair.Generate();

            var masterKeys = KeyPair.FromWIF(wif);
            log.Message($"Connecting to host: {host} with address {masterKeys.Address.Text}");

            var rpc = new JSONRPC_Client();

            var amount = TokenUtils.ToBigInteger(1000000, Nexus.NativeTokenDecimals);
            var hash = SendTransfer(rpc, log, host, masterKeys, currentKey.Address, amount);
            if (hash == Hash.Null)
            {
                return;
            }

            ConfirmTransaction(rpc, log, host, hash);

            var rnd = new Random();

            int totalTxs = 0;

            var confirming = new Dictionary<Address, Hash>();

            while (running)
            {
                KeyPair destKey;

                do
                {
                    destKey = KeyPair.Generate();
                } while (destKey == currentKey);

                amount = 1;

                var txHash = SendTransfer(rpc, null, host, currentKey, destKey.Address, amount);
                if (txHash == Hash.Null)
                {
                    log.Error($"Error sending {amount} SOUL from {currentKey.Address} to {destKey.Address}...");
                    return;
                }

                totalTxs++;
                currentKey = destKey;

                if (totalTxs % 10 == 0)
                {
                    log.Message($"Sent {totalTxs} transactions");
                }
            }
        }

        static void RunSender(Logger log, string wif, string host, int threadCount)
        {
            log.Message("Running in sender mode.");

            running = true;
            Console.CancelKeyPress += delegate {
                running = false;
                log.Message("Stopping sender...");
            };

            for (int i=1; i<= threadCount; i++)
            {
                log.Message($"Starting thread #{i}...");
                try
                {
                    new Thread(() => { SenderSpawn(log, wif, host); }).Start();
                }
                catch (Exception e) {
                    break;
                }
            }

            while (running)
            {
                Thread.Sleep(1000);
            }
        }

        static void Main(string[] args)
        {
            var seeds = new List<string>();

            var logger = new ConsoleOutput();

            var settings = new Arguments(args);

            string mode = settings.GetString("node.mode", "validator");

            bool hasRPC = settings.GetBool("rpc.enabled", false);

            string wif = settings.GetString("node.wif");

            var nexusName = settings.GetString("nexus.name", "simnet");
            var genesisAddress = Address.FromText(settings.GetString("nexus.genesis", KeyPair.FromWIF(validatorWIFs[0]).Address.Text));

            switch (mode)
            {
                case "sender":
                    string host = settings.GetString("sender.host");
                    int threadCount = settings.GetInt("sender.threads", 8);
                    RunSender(logger, wif, host, threadCount);
                    Console.WriteLine("Sender finished operations.");
                    return;

                case "validator": break;
                default:
                    {
                        logger.Error("Unknown mode: " + mode);
                        return;
                    }
            }

            string defaultPort = null;
            for (int i=0; i<validatorWIFs.Length; i++)
            {
                if (validatorWIFs[i] == wif)
                {
                    defaultPort = (7073 + i).ToString();
                }
            }

            if (defaultPort == null)
            {
                defaultPort = (7073 + validatorWIFs.Length).ToString();
            }

            int port = int.Parse(settings.GetString("node.port", defaultPort));

            var node_keys = KeyPair.FromWIF(wif);

            Nexus nexus;

            if (wif == validatorWIFs[0])
            {
                var simulator = new ChainSimulator(node_keys, 1234, logger);
                nexus = simulator.Nexus;

                for (int i=0; i< 10; i++)
                {
                    simulator.GenerateRandomBlock();
                }
            }
            else
            {
                nexus = new Nexus(nexusName, genesisAddress, logger);
                seeds.Add("127.0.0.1:7073");
            }

            running = true;

            // mempool setup
            var mempool = new Mempool(node_keys, nexus);
            mempool.Start();

            // RPC setup
            if (hasRPC)
            {
                var api = new NexusAPI(nexus, mempool);
                var webLogger = new LunarLabs.WebServer.Core.NullLogger(); // TODO ConsoleLogger is not working properly here, why?
                var rpcServer = new RPCServer(api, "rpc", 7077, webLogger);
                new Thread(() => { rpcServer.Start(); }).Start();
            }

            // node setup
            var node = new Node(nexus, node_keys, port, seeds, logger);           
            logger.Message("Phantasma Node address: " + node_keys.Address.Text);
            node.Start();

            nexus.AddPlugin(new TPSPlugin(logger, 10));

            Console.CancelKeyPress += delegate {
                running = false;
                logger.Message("Phantasma Node stopping...");
                node.Stop();
                mempool.Stop();
            };

            logger.MakeReady();
            while (running)
            {
                logger.Update();
                Thread.Sleep(100);
            }
        }
    }
}
