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
using Phantasma.Storage;
using System.Linq;
using Phantasma.CodeGen.Assembler;
using Phantasma.VM;
using System.IO;

namespace TxSender
{
    class Program
    {
        static BigInteger MinimumFee = 100000;
        static string nexusName = null;
        static PhantasmaKeys signerKeys;


        static bool FetchAnswer(string question)
        {
            Console.Write($"{question}?: (y/n)");

            do
            {
                var ch = Console.ReadLine().ToLower();

                switch (ch)
                {
                    case "y": return true;
                    case "n": return false;
                }
            } while (true);
        }

        static byte[] GenDAOTransfer()
        {
            Console.Write("Organization address? ");
            string orgStr = Console.ReadLine();
            var orgAddress = Address.FromText(orgStr);

            Console.Write("Target address? ");
            string targetStr = Console.ReadLine();
            var targetAddress = Address.FromText(targetStr);

            var symbol = "SOUL";

            Console.Write($"Amount of {symbol} to transfer from {orgAddress} to {targetStr}?: ");

            decimal amount;
            if (!decimal.TryParse(Console.ReadLine(), out amount) || amount <= 0)
            {
                Console.Write("Invalid amount");
                return null;
            }

            bool needUnstake = FetchAnswer("Is unstaking necessary for this operation");

            //S3dGUjVwYa31AxdthdpsuyBKgX1N65FnoQhUkSgYbUEdRp4
            //S3dDUXfgGosu3urCrNSUAZKx7xsLTrxDzcn4CDYQEogUbao




            var transfers = new Dictionary<string, BigInteger>();

            //transfers["P2KCmWd4iYXed7i9HmMbANeYNA8HFeSJ1aar5yiCjz96tjt"] = UnitConversion.ToBigInteger(35000, DomainSettings.StakingTokenDecimals);
            //transfers["P2K61GfcUbfWqCur644iLECZ62NAefuKgBkB6FrpMsqYHv6"] = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
            //transfers["P2KFNXEbt65rQiWqogAzqkVGMqFirPmqPw8mQyxvRKsrXV8"] = UnitConversion.ToBigInteger(128866, DomainSettings.StakingTokenDecimals);

            transfers[targetAddress.Text] = UnitConversion.ToBigInteger(amount, DomainSettings.StakingTokenDecimals);


            var sb = new ScriptBuilder().AllowGas(signerKeys.Address, Address.Null, 100000, 99999);

            foreach (var entry in transfers)
            {
                var target = Address.FromText(entry.Key);
                if (needUnstake)
                {
                    sb.CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), orgAddress, entry.Value);
                }

                if (target != orgAddress)
                {
                    sb.TransferTokens(DomainSettings.StakingTokenSymbol, orgAddress, target, entry.Value);
                }
            }

            var script = sb.SpendGas(signerKeys.Address).
                EndScript();

            return script;
        }

        static byte[] GenCreateToken()
        {
            Console.Write("Token symbol? ");
            string symbol = Console.ReadLine();

            if (!ValidationUtils.IsValidTicker(symbol))
            {
                Console.Write("Invalid token symbol");
                return null;
            }

            Console.Write("Token name? ");
            string name = Console.ReadLine();

            TokenFlags flags = TokenFlags.Transferable;

            var possibleValues = new[] { TokenFlags.Burnable, TokenFlags.Divisible, TokenFlags.Finite, TokenFlags.Fungible };

            foreach (var val in possibleValues)
            {
                if (FetchAnswer($"Is {symbol} {val}?"))
                {
                    flags |= val;
                }
            }

            int decimals;

            if (flags.HasFlag(TokenFlags.Divisible))
            {
                Console.Write($"How many decimals {symbol} has?: ");
                if (!int.TryParse(Console.ReadLine(), out decimals) || decimals < 0 || decimals > 18)
                {
                    Console.Write("Invalid decimals");
                    return null;
                }
            }
            else
            {
                decimals = 0;
            }

            BigInteger maxSupply;
            if (flags.HasFlag(TokenFlags.Finite))
            {
                Console.Write($"What is the max supply of {symbol}?: ");

                decimal val;
                if (!decimal.TryParse(Console.ReadLine(), out val) || val <= 0)
                {
                    Console.Write("Invalid decimals");
                    return null;
                }

                maxSupply = UnitConversion.ToBigInteger(val, decimals);
            }
            else
            {
                maxSupply = 0;
            }


            var labels = new Dictionary<string, int>();
            var addressStr = Base16.Encode(signerKeys.Address.ToByteArray());
            string[] scriptString;

            scriptString = new string[] {
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

                $"@end: ret"
                };

            DebugInfo debugInfo;
            var tokenScript = AssemblerUtils.BuildScript(scriptString, "GenerateToken", out debugInfo, out labels);

            var sb = ScriptUtils.
                BeginScript().
                AllowGas(signerKeys.Address, Address.Null, MinimumFee, 9999);

            var triggerMap = new Dictionary<AccountTrigger, int>();

            var onMintLabel = AccountTrigger.OnMint.ToString();
            if (labels.ContainsKey(onMintLabel))
            {
                triggerMap[AccountTrigger.OnMint] = labels[onMintLabel];
            }

            var methods = AccountContract.GetTriggersForABI(triggerMap);

            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
            var abiBytes = abi.ToByteArray();

            sb.CallInterop("Nexus.CreateToken", signerKeys.Address, symbol, name, maxSupply, decimals, flags, tokenScript, abiBytes);

            if (!flags.HasFlag(TokenFlags.Fungible))
            {
                Console.Write("NFT deployment not supported yet");
                return null;
            }

            sb.SpendGas(signerKeys.Address);

            var script = sb.EndScript();


            return script;
        }

        static byte[] GenMintToken()
        {
            Console.Write("Token symbol? ");
            string symbol = Console.ReadLine();

            if (!ValidationUtils.IsValidTicker(symbol))
            {
                Console.Write("Invalid token symbol");
                return null;
            }

            int decimals;
            Console.Write($"How many decimals {symbol} has?: ");
            if (!int.TryParse(Console.ReadLine(), out decimals) || decimals < 0 || decimals > 18)
            {
                Console.Write("Invalid decimals");
                return null;
            }

            Console.Write($"Token amount? ");
            BigInteger amount;

            decimal val;
            if (!decimal.TryParse(Console.ReadLine(), out val) || val <= 0)
            {
                Console.Write("Invalid amount");
                return null;
            }

            amount = UnitConversion.ToBigInteger(val, decimals);


            var sb = ScriptUtils.
                BeginScript().
                AllowGas(signerKeys.Address, Address.Null, MinimumFee, 9999);
            sb.MintTokens(symbol, signerKeys.Address, signerKeys.Address, amount);
            sb.SpendGas(signerKeys.Address);

            var script = sb.EndScript();


            return script;
        }


        static byte[] GenCreateSale()
        {
            Console.Write("Sale name? ");
            string name = Console.ReadLine();

            //Console.Write("Whitelist? ");
            SaleFlags flags = SaleFlags.Whitelist;

            Console.Write("Start date?: ");
            uint startDate;

            if (!uint.TryParse(Console.ReadLine(), out startDate))
            {
                Console.Write("Invalid start date");
                return null;
            }
            var startTimeStamp = (Timestamp)startDate;
            Console.WriteLine($"Start time set to {startTimeStamp}");

            Console.Write("End date?: ");
            uint endDate;
            if (!uint.TryParse(Console.ReadLine(), out endDate) || endDate <= startDate)
            {
                Console.Write("Invalid end date");
                return null;
            }
            var endTimeStamp = (Timestamp)endDate;
            Console.WriteLine($"End time set to {endTimeStamp}");

            string receiveSymbol = "SOUL";

            Console.Write("What token symbol will be sold?: ");
            string sellSymbol = Console.ReadLine();

            if (sellSymbol == DomainSettings.StakingTokenSymbol || receiveSymbol == DomainSettings.FuelTokenSymbol || !ValidationUtils.IsValidTicker(receiveSymbol))
            {
                Console.Write("Invalid token symbol");
                return null;
            }

            int decimals;
            Console.Write($"How many decimals {sellSymbol} has?: ");
            if (!int.TryParse(Console.ReadLine(), out decimals) || decimals < 0 || decimals > 18)
            {
                Console.Write("Invalid decimals");
                return null;
            }

            Console.Write($"Price: How many {sellSymbol} per 1 {receiveSymbol}? (must be integer number): ");
            int price;
            if (!int.TryParse(Console.ReadLine(), out price) || price <= 0)
            {
                Console.Write("Invalid decimals");
                return null;
            }

            Console.Write($"Softcap: How many {sellSymbol} to sell minimum for sale to be succesful? (Or zero if no soft-cap): ");
            decimal globalSoftCap;
            if (!decimal.TryParse(Console.ReadLine(), out globalSoftCap) || globalSoftCap < 0)
            {
                Console.Write("Invalid softcap");
                return null;
            }

            Console.Write($"Hardcap: How many {sellSymbol} maximum?: ");
            decimal globalHardCap;
            if (!decimal.TryParse(Console.ReadLine(), out globalHardCap) || globalHardCap <= 0)
            {
                Console.Write("Invalid hardcap");
                return null;
            }

            Console.Write($"How many {sellSymbol} must a user buy minimum? (Or zero if no minimum): ");
            decimal userSoftCap;
            if (!decimal.TryParse(Console.ReadLine(), out userSoftCap) || userSoftCap < 0)
            {
                Console.Write("Invalid user minimum");
                return null;
            }

            Console.Write($"What is the maximum {sellSymbol} a user can buy?: ");
            decimal userHardCap;
            if (!decimal.TryParse(Console.ReadLine(), out userHardCap) || userHardCap < userSoftCap)
            {
                Console.Write("Invalid hardcap");
                return null;
            }

            var sb = new ScriptBuilder().AllowGas(signerKeys.Address, Address.Null, 100000, 99999);

            sb.CallContract(NativeContractKind.Sale, nameof(SaleContract.CreateSale), signerKeys.Address, name, flags, startTimeStamp, endTimeStamp, sellSymbol, receiveSymbol, price, UnitConversion.ToBigInteger(globalSoftCap, decimals), UnitConversion.ToBigInteger(globalHardCap, decimals), UnitConversion.ToBigInteger(userSoftCap, decimals), UnitConversion.ToBigInteger(userHardCap, decimals));

            var script = sb.SpendGas(signerKeys.Address).
                EndScript();

            return script;
        }

        static byte[] GenWhitelist()
        {
            Console.Write("Sale hash? ");
            Hash hash;

            if (!Hash.TryParse(Console.ReadLine(), out hash))
            {
                Console.Write("Invalid sale hash");
                return null;
            }

            Console.Write("Addresses (separated by comma or newline, or filename): ");
            var str = Console.ReadLine();

            if (str.Contains("."))
            {
                if (File.Exists(str))
                {
                    str = File.ReadAllText(str);
                }
                else
                {
                    Console.Write("File not found");
                    return null;
                }
            }


            var addresses = str.Replace("\n", ",").Split(',').Select(x => x.Trim()).Select(x => Address.FromText(x)).ToArray();


            var sb = new ScriptBuilder().AllowGas(signerKeys.Address, Address.Null, 100000, 99999);

            foreach (var addr in addresses)
            {
                sb.CallContract(NativeContractKind.Sale, nameof(SaleContract.AddToWhitelist), signerKeys.Address, hash, addr);
            }

            var script = sb.SpendGas(signerKeys.Address).
                EndScript();

            return script;
        }


        static void Main(string[] args)
        {
            /*var obj = Serialization.Unserialize<VMObject>(Base16.Decode("0821204DBC2216A0EA109AA3436D23A67F381AE98282C040E5F117CDFDA80108733D04"));
            var hash = obj.AsInterop<Hash>();
            Console.WriteLine(hash);*/

            Console.Write($"Enter nexus name: ");
            nexusName = Console.ReadLine();

            Console.Write($"Enter WIF for signing transaction: ");
            var wif = Console.ReadLine();
            signerKeys = PhantasmaKeys.FromWIF(wif);

            Console.WriteLine("Select operation: ");
            Console.WriteLine("0 - Exit");
            Console.WriteLine("1 - DAO Transfer");
            Console.WriteLine("2 - Create token");
            Console.WriteLine("3 - Mint tokens");
            Console.WriteLine("4 - Create sale");
            Console.WriteLine("5 - Whitelist sale address");

            var str = Console.ReadLine();

            int option;

            if (!int.TryParse(str, out option))
            {
                Console.WriteLine("Invalid option...");
                return;
            }

            byte[] script;

            switch (option)
            {
                case 0:
                    return;

                case 1:
                    script = GenDAOTransfer();
                    break;

                case 2:
                    script = GenCreateToken();
                    break;

                case 3:
                    script = GenMintToken();
                    break;

                case 4:
                    script = GenCreateSale();
                    break;

                case 5:
                    script = GenWhitelist();
                    break;

                case 6:
                    {

                        var sb = new ScriptBuilder().AllowGas(signerKeys.Address, Address.Null, 100000, 99999);

                            sb.CallContract(NativeContractKind.Sale, nameof(SaleContract.EditSalePrice), signerKeys.Address, Hash.Parse("8D02B87F8C132BE7FC5DA25E80E3222EF183EFC3AB9F8F20B5455CB402A7A4E6"), 4);

                        script = sb.SpendGas(signerKeys.Address).
                            EndScript();
                        break;
                    }

                default:
                    Console.WriteLine("Unsupported option...");
                    return;
            }

            if (script == null)
            {
                Console.WriteLine("Transaction generation failed...");
                return;
            }

            var expiration = new Timestamp(Timestamp.Now.Value + 1000);
            var tx = new Transaction(nexusName, "main", script, expiration, "TXSNDER1.0");
            tx.Mine(ProofOfWork.Minimal);
            tx.Sign(signerKeys);

            var rawTx = tx.ToByteArray(true);

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
