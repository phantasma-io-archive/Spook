using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;

// NOTE: Phantasma has no Nuget packages yet.
// In order to get this to compile, download the chain repository
// put that repository in your disk at same folder level as the Spook repository
// https://github.com/phantasma-io/PhantasmaChain
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain;
using Phantasma.Core.Types;

namespace TxSender
{
    class Program
    {
        static BigInteger gasFee = 100000;
        static string nexusName = null;
        static PhantasmaKeys signerKeys;

        private static byte[] CreateTxScript(string symbol, int decimals, string from, Dictionary<string, decimal> destinations)
        {
            var fromAddress = Address.FromText(from);

            var gasLimit = 9999;

            var sb = new ScriptBuilder().AllowGas(signerKeys.Address, Address.Null, gasFee, gasLimit);
            foreach (var entry in destinations)
            {
                var targetAddress = Address.FromText(entry.Key);
                var amount = UnitConversion.ToBigInteger(entry.Value, decimals);
                sb.TransferTokens(symbol, fromAddress, targetAddress, amount);
            }

            var script = sb.SpendGas(signerKeys.Address).
                EndScript();

            return script;
        }


        static void Main(string[] args)
        {
            // eg: mainnet or testnet
            Console.Write($"Enter nexus name: ");
            nexusName = Console.ReadLine();

            Console.Write($"Enter WIF for signing transaction: ");
            var wif = Console.ReadLine();
            signerKeys = PhantasmaKeys.FromWIF(wif);

            var targets = new Dictionary<string, decimal>();
            targets["P2KCmWd4iYXed7i9HmMbANeYNA8HFeSJ1aar5yiCjz96tjt"] = 123.0m;

            var from = signerKeys.Address.Text;

            byte[] script = CreateTxScript("SOUL", 8, from, targets);


            if (script == null)
            {
                Console.WriteLine("Transaction generation failed...");
                return;
            }

            // has to be a date a few minutes in the future
            var expiration = new Timestamp(Timestamp.Now.Value + 1000);

            // can be any valid id, used to identify what app generated the tx
            var appID = "TXCC1.0";

            // only existing chain now is the main chain
            var chainName = "main";

            var tx = new Transaction(nexusName, chainName, script, expiration, appID);
            tx.Sign(signerKeys);

            // encode the transaction into a byte array
            var rawTx = tx.ToByteArray(true);

            // convert the byte array to an hex string
            var hexRawTx = Base16.Encode(rawTx);


            Console.Write("Node REST URL: ");
            var url = Console.ReadLine();

            if (!url.StartsWith("http"))
            {
                url = "http://" + url;
            }

            // check for port
            if (!url.Replace("http://", "").Contains(":"))
            {
                var defaultPort = 7078;
                Console.WriteLine("No port specified, using defaul port " + defaultPort);
                url += ":" + defaultPort;
            }

            var baseUrl = url;

            url = baseUrl + "/api/sendRawTransaction/" + hexRawTx;

            if (url.Length > 2040)
            {
                Console.WriteLine("Script is too big");
                return;
            }

            Hash txHash;

            using (var wb = new WebClient())
            {
                var response = wb.DownloadString(url);

                Console.WriteLine(response);

                if (response.StartsWith("\""))
                {
                    response = response.Substring(1, response.Length - 2);
                }

                if (!Hash.TryParse(response, out txHash))
                {
                    Console.WriteLine("Failed...");
                    return;
                }
            }

            Console.WriteLine("Confirming...");
            Thread.Sleep(1000 * 20);

            url = baseUrl + "/api/getTransaction/" + txHash.ToString();
            using (var wb = new WebClient())
            {
                var response = wb.DownloadString(url);

                Console.WriteLine(response);
            }

            Console.WriteLine("Finished.");
            Console.ReadLine();
        }
    }
}
