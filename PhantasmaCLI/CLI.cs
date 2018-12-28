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
using Phantasma.Network.P2P;

namespace Phantasma.CLI
{
    public class CLI
    {
        static void Main(string[] args)
        {
            new CLI(args);
        }

        private static readonly string[] validatorWIFs = new string[]
        {
            "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", //P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr
            "L1sEB8Z6h5Y7aQKqxbAkrzQLY5DodmPacjqSkFPmcR82qdmHEEdY", // PGBinkbZA3Q6BxMnL2HnJSBubNvur3iC6GtQpEThDnvrr
            "KxWUCAD2wECLfA7diT7sV7V3jcxAf9GSKqZy3cvAt79gQLHQ2Qo8", // PDiqQHDwe6MTcP6TH6DYjq7FTUouvy2YEkDXz2chCABCb
        };

        private readonly Node node;
        private readonly Logger logger;
        private readonly Mempool mempool;
        private bool running = false;

        private NexusAPI api;

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

        private void SenderSpawn(string wif, string host)
        {
            Thread.CurrentThread.IsBackground = true;

            var currentKey = KeyPair.Generate();

            var masterKeys = KeyPair.FromWIF(wif);
            logger.Message($"Connecting to host: {host} with address {masterKeys.Address.Text}");

            var rpc = new JSONRPC_Client();

            var amount = TokenUtils.ToBigInteger(1000000, Nexus.NativeTokenDecimals);
            var hash = SendTransfer(rpc, logger, host, masterKeys, currentKey.Address, amount);
            if (hash == Hash.Null)
            {
                return;
            }

            ConfirmTransaction(rpc, logger, host, hash);

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
                    logger.Error($"Error sending {amount} SOUL from {currentKey.Address} to {destKey.Address}...");
                    return;
                }

                totalTxs++;
                currentKey = destKey;

                if (totalTxs % 10 == 0)
                {
                    logger.Message($"Sent {totalTxs} transactions");
                }
            }
        }

        private void RunSender(string wif, string host, int threadCount)
        {
            logger.Message("Running in sender mode.");

            running = true;
            Console.CancelKeyPress += delegate {
                running = false;
                logger.Message("Stopping sender...");
            };

            for (int i=1; i<= threadCount; i++)
            {
                logger.Message($"Starting thread #{i}...");
                try
                {
                    new Thread(() => { SenderSpawn(wif, host); }).Start();
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

        public CLI(string[] args) { 
            var seeds = new List<string>();

            ConsoleGUI gui;

            var settings = new Arguments(args);

            var useGUI = settings.GetBool("gui.enabled", true);

            if (useGUI)
            {
                gui = new ConsoleGUI();
                logger = gui;
            }
            else
            {
                gui = null;
                logger = new ConsoleLogger();
            }

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
                    RunSender(wif, host, threadCount);
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
            this.mempool = new Mempool(node_keys, nexus);
            mempool.Start();

            api = new NexusAPI(nexus, mempool);

            // RPC setup
            if (hasRPC)
            {
                var webLogger = new LunarLabs.WebServer.Core.NullLogger(); // TODO ConsoleLogger is not working properly here, why?
                var rpcServer = new RPCServer(api, "rpc", 7077, webLogger);
                new Thread(() => { rpcServer.Start(); }).Start();
            }

            // node setup
            this.node = new Node(nexus, node_keys, port, seeds, gui);           
            logger.Message("Phantasma Node address: " + node_keys.Address.Text);
            node.Start();

            nexus.AddPlugin(new TPSPlugin(gui, 10));

            Console.CancelKeyPress += delegate {
                Terminate();
            };

            logger.Success("Node is ready");

            var dispatcher = new CommandDispatcher();
            SetupCommands(dispatcher);

            if (gui != null)
            {
                gui.MakeReady(dispatcher);

                while (running)
                {
                    gui.Update();
                }
            }
            else {
                while (running)
                {
                    Thread.Sleep(5000);
                }
            }
        }

        private void Terminate()
        {
            running = false;
            logger.Message("Phantasma Node stopping...");

            if (node.IsRunning)
            {
                node.Stop();
            }

            if (mempool.IsRunning)
            {
                mempool.Stop();
            }
        }

        private void ExecuteAPI(string name, string[] args)
        {
            var result = api.Execute(name, args);
            if (result == null)
            {
                logger.Warning("API returned null value...");
                return;
            }

            var json = JSONWriter.WriteToString(result);
            var lines = json.Split('\n');
            foreach (var line in lines)
            {
                logger.Message(line);
            }
        }

        private void SetupCommands(CommandDispatcher dispatcher)
        {
            dispatcher.RegisterCommand("quit", "Stops the node and exits", (args) => Terminate());

            foreach (var method in api.Methods)
            {
                dispatcher.RegisterCommand("api."+method.Name, "API CALL", (args) => ExecuteAPI(method.Name, args));
            }
        }
    }
}
