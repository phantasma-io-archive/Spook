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

        static void Main(string[] args)
        {
            var log = new ConsoleLogger();
            var seeds = new List<string>();

            var settings = new Arguments(args);

            string wif = settings.GetValue("wif");
            int port = int.Parse(settings.GetValue("port", "7073"));

            var node_keys = KeyPair.FromWIF(wif);

            Nexus nexus;

            if (wif == validatorWIFs[0])
            {
                var simulator = new ChainSimulator(node_keys, 1234);
                nexus = simulator.Nexus;
            }
            else
            {
                nexus = new Nexus("simnet", log);
                if (!nexus.CreateGenesisBlock(node_keys))
                {
                    throw new ChainException("Genesis block failure");
                }

                seeds.Add("127.0.0.1:7073");
            }

            var node = new Node(nexus, node_keys, port, seeds, log);

            bool running = true;

            Console.WriteLine("Phantasma Node starting...");
            node.Start();

            Console.CancelKeyPress += delegate {
                running = false;
                Console.WriteLine("Phantasma Node stopping...");
                node.Stop();
            };

            while (running)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
