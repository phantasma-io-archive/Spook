using System;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain;
using Phantasma.Core.Types;
using System.Net;
using System.Threading;

namespace TxSender
{
    class Program
    {
        static void Main(string[] args)
        {
            //var orgAddress = Address.FromText("S3dGUjVwYa31AxdthdpsuyBKgX1N65FnoQhUkSgYbUEdRp4");
            var orgAddress = Address.FromText("S3dDUXfgGosu3urCrNSUAZKx7xsLTrxDzcn4CDYQEogUbao"); 
            var signerAddress = Address.FromText("P2KFNXEbt65rQiWqogAzqkVGMqFirPmqPw8mQyxvRKsrXV8");

            Console.WriteLine($"Enter WIF for {signerAddress.Text}");
            var wif = Console.ReadLine();
            var signerKeys = PhantasmaKeys.FromWIF(wif);

            if (signerKeys.Address.Text != signerAddress.Text)
            {
                Console.WriteLine("Invalid WIF");
                return;
            }

            var transfers = new Dictionary<string, BigInteger>();

            //transfers["P2KCmWd4iYXed7i9HmMbANeYNA8HFeSJ1aar5yiCjz96tjt"] = UnitConversion.ToBigInteger(35000, DomainSettings.StakingTokenDecimals);
            //transfers["P2K61GfcUbfWqCur644iLECZ62NAefuKgBkB6FrpMsqYHv6"] = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
            transfers["P2KFNXEbt65rQiWqogAzqkVGMqFirPmqPw8mQyxvRKsrXV8"] = UnitConversion.ToBigInteger(128866, DomainSettings.StakingTokenDecimals);
            
            BigInteger gasPrice = 100000;

            var sb = new ScriptBuilder().AllowGas(signerAddress, Address.Null, 100000, 99999);

            foreach (var entry in transfers)
            {
                var targetAddress = Address.FromText(entry.Key);
                sb.CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), orgAddress, entry.Value);
                sb.TransferTokens(DomainSettings.StakingTokenSymbol, orgAddress, targetAddress, entry.Value);
            }

            var script = sb.SpendGas(signerAddress).
                EndScript();

            var expiration = new Timestamp(Timestamp.Now.Value + 1000);
            var tx = new Transaction("mainnet", "main", script, expiration, "TXSNDER1.0");
            tx.Sign(signerKeys);

            var rawTx = tx.ToByteArray(true);

            var hexRawTx = Base16.Encode(rawTx);


            Console.Write("URL: ");
            var url = Console.ReadLine();

            if (!url.StartsWith("http"))
            {
                url = "http://" + url;
            }

            var baseUrl = url;

            url = baseUrl + "/api/sendRawTransaction/" + hexRawTx;

            if (url.Length > 2000)
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
