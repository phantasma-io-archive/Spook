using System;
using System.Collections.Generic;
using System.Threading;
using Phantasma.Blockchain.Consensus;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Core.Log;
using Phantasma.Tests;
using Phantasma.Core.Utils;

namespace Phantasma.CLI
{
    class Program
    {
        private static readonly string[] validatorWIFs = new string[]
        {
            "L1sEB8Z6h5Y7aQKqxbAkrzQLY5DodmPacjqSkFPmcR82qdmHEEdY", // PGBinkbZA3Q6BxMnL2HnJSBubNvur3iC6GtQpEThDnvrr
            "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", //P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr
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

        static void Main(string[] args)
        {
            var log = new ConsoleLogger();
            var seeds = new List<string>();

            Console.ForegroundColor = ConsoleColor.DarkGray;

            var settings = new Arguments(args);

            string wif = settings.GetValue("wif");
            var nexusName = settings.GetValue("nexus", "simnet");
            var genesisAddress = Address.FromText(settings.GetValue("genesis", KeyPair.FromWIF(validatorWIFs[0]).Address.Text));

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

            int port = int.Parse(settings.GetValue("port", defaultPort));

            var node_keys = KeyPair.FromWIF(wif);

            Nexus nexus;

            if (wif == validatorWIFs[0])
            {
                var simulator = new ChainSimulator(node_keys, 1234);
                nexus = simulator.Nexus;

                for (int i=0; i< 1000; i++)
                {
                    simulator.GenerateRandomBlock();
                }
            }
            else
            {
                nexus = new Nexus(nexusName, genesisAddress, log);
                seeds.Add("127.0.0.1:7073");
            }

            var node = new Node(nexus, node_keys, port, seeds, log);

            bool running = true;

            log.Message("Phantasma Node address: " + node_keys.Address.Text);
            node.Start();

            Console.CancelKeyPress += delegate {
                running = false;
                log.Message("Phantasma Node stopping...");
                node.Stop();
            };

            while (running)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
