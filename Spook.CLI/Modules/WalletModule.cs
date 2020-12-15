using System;
using System.Threading;
using System.Collections.Generic;
using Phantasma.VM.Utils;
using Phantasma.Cryptography;
using Phantasma.API;
using Phantasma.Core.Log;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Pay.Chains;
using Phantasma.Pay;
using Phantasma.Neo.Core;
using Phantasma.Spook.Oracles;
using Phantasma.Spook.Command;
using Phantasma.Blockchain;
using Phantasma.Domain;
using System.IO;
using System.Reflection;
using System.Linq;
using Phantasma.VM;
using Phantasma.Blockchain.Contracts;

namespace Phantasma.Spook.Modules
{
    [Module("wallet")]
    public static class WalletModule
    {
        public static PhantasmaKeys Keys;

        private static Logger logger => ModuleLogger.Instance;

        [ConsoleCommand("wallet open", "Wallet", "Open a wallet, requires WIF")]
        public static void Open(string[] args)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Expected args: WIF");
            }

            var wif = args[0];

            try
            {
                Keys = PhantasmaKeys.FromWIF(wif);
                logger.Message($"Opened wallet with address: {Keys.Address}");
            }
            catch (Exception)
            {
                logger.Message($"Failed to open wallet. Make sure you valid a correct WIF key.");
            }

        }

        [ConsoleCommand("wallet create", "Wallet", "Create a new wallet")]
        public static void Create(string[] args)
        {
            if (args.Length != 0)
            {
                throw new CommandException("Unexpected args");
            }

            try
            {
                Keys = PhantasmaKeys.Generate();
                logger.Message($"Generate wallet with address: {Keys.Address}");
                logger.Message($"WIF: {Keys.ToWIF()}");
                logger.Message($"Save this wallet WIF, it won't be displayed again and it is necessary to access the wallet!");
            }
            catch (Exception)
            {
                logger.Message($"Failed to create a new wallet.");
            }

        }

        public static void Balance(NexusAPI api, int phantasmaRestPort, NeoScanAPI neoScanAPI, string[] args)
        {
            if (phantasmaRestPort <= 0)
            {
                throw new CommandException("Please enable REST API on this node to use this feature");
            }

            Address address;

            if (args.Length == 1)
            {
                address = Address.FromText(args[0]);
            }
            else
            {
                address = Keys.Address;
            }

            logger.Message("Fetching balances...");
            var wallets = new List<CryptoWallet>();
            wallets.Add(new PhantasmaWallet(Keys, $"http://localhost:{phantasmaRestPort}/api"));
            wallets.Add(new NeoWallet(Keys, neoScanAPI.URL));

            foreach (var wallet in wallets)
            {
                wallet.SyncBalances((success) =>
                {
                    var temp = wallet.Name != wallet.Address ? $" ({wallet.Address})":"";
                    logger.Message($"{wallet.Platform} balance for {wallet.Name}"+temp);
                    if (success)
                    {
                        bool empty = true;
                        foreach (var entry in wallet.Balances)
                        {
                            empty = false;
                            logger.Message($"{entry.Amount} {entry.Symbol} @ {entry.Chain}");
                        }
                        if (empty)
                        {
                            logger.Message("Empty wallet.");
                        }
                    }
                    else
                    {
                        logger.Message("Failed to fetch balances!");
                    }
                });
            }
        }

        private static Hash NeoTransfer(NeoKeys neoKeys, string toAddress, string tokenSymbol, decimal tempAmount, NeoAPI neoAPI)
        {
            Neo.Core.Transaction neoTx;

            logger.Message($"Sending {tempAmount} {tokenSymbol} to {toAddress}...");

            Thread.Sleep(500);

            try {
                if (tokenSymbol == "NEO" || tokenSymbol == "GAS")
                {
                    neoTx = neoAPI.SendAsset(neoKeys, toAddress, tokenSymbol, tempAmount);
                }
                else
                {
                    var nep5 = neoAPI.GetToken(tokenSymbol);
                    if (nep5 == null)
                    {
                        throw new CommandException($"Could not find interface for NEP5: {tokenSymbol}");
                    }
                    neoTx = nep5.Transfer(neoKeys, toAddress, tempAmount);
                }

                logger.Success($"Waiting for confirmations, could take up to a minute...");
                Thread.Sleep(45000);
                logger.Success($"Sent transaction with hash {neoTx.Hash}!");

                var hash = Hash.Parse(neoTx.Hash.ToString());
                return hash;
            }
            catch (Exception e)
            {
                logger.Message("Error sending NEO transaction: " + e);
                return Hash.Null;
            }

        }

        public static Hash ExecuteTransaction(NexusAPI api, byte[] script, ProofOfWork proofOfWork, IKeyPair keys)
        {
            return ExecuteTransaction(api, script, proofOfWork, new IKeyPair[] { keys });
        }

        private static Dictionary<string, TransactionResult> _transactionResults = new Dictionary<string, TransactionResult>();

        public static Hash ExecuteTransaction(NexusAPI api, byte[] script, ProofOfWork proofOfWork, params IKeyPair[] keys)
        {
            var identifier = "SPK" + Assembly.GetAssembly(typeof(CLI)).GetVersion();
            var tx = new Blockchain.Transaction(api.Nexus.Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromMinutes(5), identifier);

            if (proofOfWork != ProofOfWork.None)
            {
                logger.Message($"Mining proof of work with difficulty: {proofOfWork}...");
                tx.Mine(proofOfWork);
            }

            logger.Message("Signing message...");
            foreach (var keyPair in keys)
            {
                tx.Sign(keyPair);
            }

            var rawTx = tx.ToByteArray(true);

            var encodedRawTx = Base16.Encode(rawTx);
            try
            {
                api.SendRawTransaction(encodedRawTx);
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            Thread.Sleep(4000);
            var hash = tx.Hash.ToString();
            do
            {
                var result = api.GetTransaction(hash);
                if (result is TransactionResult)
                {
                    _transactionResults[hash] = (TransactionResult)result;
                }
                else
                if (result is ErrorResult)
                {
                    var temp = (ErrorResult)result;
                    if (temp.error.Contains("pending"))
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    else
                    {
                        throw new CommandException(temp.error);
                    }
                }
                else
                {
                    throw new Exception("Something weird happened with transaction " + hash);
                }

                break;
            } while (true);
            logger.Success($"Sent transaction with hash {hash}!");

            return Hash.Parse(hash);
        }

        private static IEnumerable<EventResult> GetTransactionEvents(Hash hash)
        {
            var hashStr = hash.ToString();
            if (_transactionResults.ContainsKey(hashStr))
            {
                return _transactionResults[hashStr].events;
            }

            return Enumerable.Empty<EventResult>();
        }

        private static void SettleSwap(NexusAPI api, BigInteger minimumFee, string platform, string swapSymbol, Hash extHash, IKeyPair externalKeys, Address targetAddress)
        {
            var outputAddress = Address.FromKey(externalKeys);

            var script = new ScriptBuilder()
                .CallContract("interop", "SettleTransaction", outputAddress, platform, extHash)
                .CallContract("swap", "SwapFee", outputAddress, swapSymbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
                .TransferBalance(swapSymbol, outputAddress, targetAddress)
                .AllowGas(outputAddress, Address.Null, minimumFee, 500)
                .SpendGas(outputAddress).EndScript();

            logger.Message("Settling swap on Phantasma");
            ExecuteTransaction(api, script, ProofOfWork.None, externalKeys);
            logger.Success($"Swap of {swapSymbol} is complete!");
        }


        private static void DoChecks(NexusAPI api)
        {
            if (Keys == null)
            {
                throw new CommandException("Please open a wallet first");
            }

            if (api.Mempool == null)
            {
                throw new CommandException("No mempool enabled");
            }
        }

        public static void Transfer(NexusAPI api, BigInteger minimumFee, NeoAPI neoAPI, string[] args)
        {
            if (args.Length != 4)
            {
                throw new CommandException("Expected args: source_address target_address amount symbol");
            }

            DoChecks(api);

            var tempAmount = decimal.Parse(args[2]);
            var tokenSymbol = args[3];

            TokenResult tokenInfo;
            try
            {
                var result = api.GetToken(tokenSymbol);
                tokenInfo = (TokenResult)result;
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            if (!tokenInfo.flags.Contains("Fungible"))
            {
                throw new CommandException("Token must be fungible!");
            }

            var amount = UnitConversion.ToBigInteger(tempAmount, tokenInfo.decimals);

            var sourceName = args[0];
            string sourcePlatform;

            if (Address.IsValidAddress(sourceName))
            {
                sourcePlatform = PhantasmaWallet.PhantasmaPlatform;
            }
            else
            if (NeoWallet.IsValidAddress(sourceName))
            {
                sourcePlatform = NeoWallet.NeoPlatform;
            }
            else
            {
                throw new CommandException("Invalid source address " + sourceName);
            }

            var destName = args[1];
            string destPlatform;

            if (Address.IsValidAddress(destName))
            {
                destPlatform = PhantasmaWallet.PhantasmaPlatform;
            }
            else
            if (NeoWallet.IsValidAddress(destName))
            {
                destPlatform = NeoWallet.NeoPlatform;
            }
            else
            {
                throw new CommandException("Invalid destination address " + destName);
            }

            if (destName == sourceName)
            {
                throw new CommandException("Cannot transfer to same address");
            }

            if (sourcePlatform != PhantasmaWallet.PhantasmaPlatform)
            {
                if (destPlatform != PhantasmaWallet.PhantasmaPlatform)
                {
                    if (sourcePlatform != destPlatform)
                    {
                        throw new CommandException($"Cannot transfer directly from {sourcePlatform} to {destPlatform}");
                    }
                    else
                    {
                        switch (destPlatform)
                        {
                            case NeoWallet.NeoPlatform:
                                {
                                    var neoKeys = new NeoKeys(Keys.PrivateKey);

                                    if (sourceName != neoKeys.Address)
                                    {
                                        throw new CommandException("The current open wallet does not have keys that match address " + sourceName);
                                    }

                                    var neoHash = NeoTransfer(neoKeys, destName, tokenSymbol, tempAmount, neoAPI);
                                    return;
                                }

                            default:
                                throw new CommandException($"Not implemented yet :(");
                        }
                    }
                }
                else
                {
                    logger.Message($"Source is {sourcePlatform} address, a swap will be performed using an interop address.");

                    IPlatform platformInfo = api.Nexus.GetPlatformInfo(api.Nexus.RootStorage, sourcePlatform);

                    Hash extHash;
                    IKeyPair extKeys;

                    switch (sourcePlatform)
                    {
                        case NeoWallet.NeoPlatform:
                            {
                                try
                                {
                                    var neoKeys = new NeoKeys(Keys.PrivateKey);

                                    if (sourceName != neoKeys.Address)
                                    {
                                        throw new CommandException("The current open wallet does not have keys that match address " + sourceName);
                                    }

                                    extHash = NeoTransfer(neoKeys, platformInfo.InteropAddresses[0].ExternalAddress, tokenSymbol, tempAmount, neoAPI);

                                    if (extHash == Hash.Null)
                                    {
                                        return;
                                    }

                                    extKeys = neoKeys;
                                }
                                catch (Exception e)
                                {
                                    logger.Message($"{sourcePlatform} error: " + e.Message);
                                    return;
                                }

                                break;
                            }

                        default:
                            logger.Message($"Transactions using platform {sourcePlatform} are not supported yet");
                            return;
                    }

                    var destAddress = Address.FromText(destName);
                    SettleSwap(api, minimumFee, sourcePlatform, tokenSymbol, extHash, extKeys, destAddress);
                }
                return;
            }
            else
            {
                Address destAddress;

                if (destPlatform != PhantasmaWallet.PhantasmaPlatform)
                {

                    switch (destPlatform)
                    {
                        case NeoWallet.NeoPlatform:
                            destAddress = NeoWallet.EncodeAddress(destName);
                            break;

                        default:
                            logger.Message($"Transactions to platform {destPlatform} are not supported yet");
                            return;
                    }

                    logger.Message($"Target is {destPlatform} address, a swap will be performed through interop address {destAddress}.");
                }
                else
                {
                    destAddress = Address.FromText(destName);
                }

                var script = ScriptUtils.BeginScript().
                    CallContract("swap", "SwapFee", Keys.Address, tokenSymbol, UnitConversion.ToBigInteger(0.01m, DomainSettings.FuelTokenDecimals)).
                    AllowGas(Keys.Address, Address.Null, minimumFee, 900).
                    TransferTokens(tokenSymbol, Keys.Address, destAddress, amount).
                    SpendGas(Keys.Address).
                    EndScript();

                logger.Message($"Sending {tempAmount} {tokenSymbol} to {destAddress.Text}...");
                ExecuteTransaction(api, script, ProofOfWork.None, Keys);
            }
        }

        public static void Stake(NexusAPI api, BigInteger minFee, string[] args)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Expected args: amount");
            }

            DoChecks(api);

            var tempAmount = decimal.Parse(args[0]);
            var tokenSymbol = DomainSettings.StakingTokenSymbol;

            TokenResult tokenInfo;
            try
            {
                var result = api.GetToken(tokenSymbol);
                tokenInfo = (TokenResult)result;
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            var amount = UnitConversion.ToBigInteger(tempAmount, tokenInfo.decimals);

            var script = ScriptUtils.BeginScript().
                AllowGas(Keys.Address, Address.Null, minFee, 9999).
                CallContract("stake", "Stake", Keys.Address, amount).
                SpendGas(Keys.Address).
                EndScript();

            var hash = ExecuteTransaction(api, script, ProofOfWork.None, Keys);

            if (hash != Hash.Null)
            {
                var events = GetTransactionEvents(hash);

                if (events.Any(x => x.kind == EventKind.TokenStake.ToString()))
                {
                    logger.Message($"Staked succesfully {tempAmount} {tokenSymbol} at {Keys.Address.Text}");
                }
                else
                {
                    throw new CommandException("Transaction was confirmed but missing stake event?");
                }
            }
        }

        public static void Airdrop(string[] args, NexusAPI api, BigInteger minFee)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Invalid number of arguments, expected airdrop filename");
            }

            DoChecks(api);

            var fileName = args[0];
            if (!File.Exists(fileName))
            {
                throw new CommandException("Airdrop file does not exist");
            }

            var lines = File.ReadAllLines(fileName);
            var sb = new ScriptBuilder();

            var expectedLimit = 100 + 600 * lines.Length;

            var expectedGas = UnitConversion.ToDecimal(expectedLimit * minFee, DomainSettings.FuelTokenDecimals);
            logger.Message($"This airdrop will require at least {expectedGas} {DomainSettings.FuelTokenSymbol}");

            sb.AllowGas(Keys.Address, Address.Null, minFee, expectedLimit);

            int addressCount = 0;
            foreach (var line in lines)
            {
                var temp = line.Split(',');

                if (!Address.IsValidAddress(temp[0]))
                {
                    continue;
                }

                addressCount++;

                var target = Address.FromText(temp[0]);
                var symbol = temp[1];
                var amount = BigInteger.Parse(temp[2]);

                sb.TransferTokens(symbol, Keys.Address, target, amount);
            }

            sb.SpendGas(Keys.Address);
            var script = sb.EndScript();

            logger.Message($"Sending airdrop to {addressCount} addresses...");
            ExecuteTransaction(api, script, ProofOfWork.None, Keys);
        }

        public static void Migrate(string[] args, NexusAPI api, BigInteger minFee)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Invalid number of arguments, expected new wif");
            }

            DoChecks(api);

            var newWIF = args[0];
            var newKeys = PhantasmaKeys.FromWIF(newWIF);

            var expectedLimit = 800;

            var sb = new ScriptBuilder();

            sb.AllowGas(Keys.Address, Address.Null, minFee, expectedLimit);
            sb.CallContract("validator", "Migrate", Keys.Address, newKeys.Address);
            sb.SpendGas(Keys.Address);
            var script = sb.EndScript();

            var hash = ExecuteTransaction(api, script, ProofOfWork.None, Keys/*, newKeys*/);
            if (hash != Hash.Null)
            {
                logger.Message($"Migrated to " + newKeys.Address);
                Keys = newKeys;
            }
        }

        private static VMObject ExecuteScript(byte[] script, ContractInterface abi, string methodName)
        {
            var method = abi.FindMethod(methodName);

            if (method == null)
            {
                throw new Exception("ABI is missing: " + method.name);
            }

            var vm = new GasMachine(script, (uint) method.offset);
            var result = vm.Execute();
            if (result == ExecutionState.Halt)
            {
                return vm.Stack.Pop();
            }

            throw new Exception("Script execution failed for: " + method.name);
        }

        private static void DeployOrUpgrade(string[] args, NexusAPI api, BigInteger minFee, bool isUpgrade)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Invalid number of arguments, expected file name");
            }

            DoChecks(api);

            var fileName = args[0];

            if (!File.Exists(fileName))
            {
                throw new CommandException("Provided file does not exist");
            }

            var extension = ScriptModule.ScriptExtension;

            if (!fileName.EndsWith(extension))
            {
                throw new CommandException($"Provided file is not a compiled {extension} script");
            }

            var abiFile = fileName.Replace(extension, ".abi");
            if (!File.Exists(abiFile))
            {
                throw new CommandException("Not ABI file that matches provided script file");
            }

            var contractName = Path.GetFileNameWithoutExtension(fileName);

            var contractScript = File.ReadAllBytes(fileName);
            var abiBytes = File.ReadAllBytes(abiFile);

            var abi = ContractInterface.FromBytes(abiBytes);

            var sb = new ScriptBuilder();

            bool isToken = ValidationUtils.IsValidTicker(contractName);
            var availableFlags = Enum.GetValues(typeof(TokenFlags)).Cast<TokenFlags>().ToArray();

            sb.AllowGas(Keys.Address, Address.Null, minFee, 9999);

            if (isUpgrade)
            {
                // check for modification in flags
                if (isToken)
                {
                    var symbol = contractName;
                    var apiResult = api.GetToken(symbol);

                    if (apiResult is TokenResult)
                    {
                        var oldToken = (TokenResult)apiResult;

                        var oldFlags = TokenFlags.None;
                        var splitFlags = oldToken.flags.Split(',');
                        foreach (var entry in splitFlags)
                        {
                            TokenFlags flag;
                            
                            if (Enum.TryParse<TokenFlags>(entry, true, out flag))
                            {
                                oldFlags |= flag;
                            }
                        }

                        foreach (var flag in availableFlags)
                        {
                            var propName = "is" + flag;
                            if (abi.HasMethod(propName))
                            {
                                var isSet = ExecuteScript(contractScript, abi, propName).AsBool();
                                var wasSet = oldFlags.HasFlag(flag);
                                if (isSet != wasSet)
                                {
                                    throw new CommandException($"Detected '{flag}' flag change: {wasSet} => {isSet}");
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new CommandException("could not find any deployed token contract for " + symbol);
                    }
                }

                sb.CallInterop("Runtime.UpgradeContract", Keys.Address, contractName, contractScript, abiBytes);
            }
            else
            if (isToken)
            {
                if (!abi.HasMethod("getName"))
                {
                    throw new CommandException("token contract is missing required 'name' property");
                }

                var symbol = contractName;
                var name = ExecuteScript(contractScript, abi, "getName").AsString();

                if (string.IsNullOrEmpty(name)) {
                    throw new CommandException("token contract 'name' property is returning an empty value");
                }

                BigInteger maxSupply = abi.HasMethod("getMaxSupply") ? ExecuteScript(contractScript, abi, "getMaxSupply").AsNumber() : 0;
                BigInteger decimals = abi.HasMethod("getDecimals") ? ExecuteScript(contractScript, abi, "getDecimals").AsNumber() : 0;

                TokenFlags flags = TokenFlags.None;

                foreach (var flag in availableFlags)
                {
                    var propName = "is" + flag;
                    if (abi.HasMethod(propName) && ExecuteScript(contractScript, abi, propName).AsBool())
                    {
                        flags |= flag;
                    }
                }

                sb.CallInterop("Nexus.CreateToken", Keys.Address, symbol, name, maxSupply, decimals, flags, contractScript, abiBytes);

                contractName = symbol;
            }
            else
            {
                sb.CallInterop("Runtime.DeployContract", Keys.Address, contractName, contractScript, abiBytes);
            }

            sb.SpendGas(Keys.Address);
            var script = sb.EndScript();

            if (!isUpgrade)
            {
                var upgradeTrigger = AccountContract.GetTriggerForABI(AccountTrigger.OnUpgrade);
                if (abi.Implements(upgradeTrigger))
                {
                    logger.Message($"{contractName} implements proper triggers, and can be upgraded later.");
                }
                else
                {
                    logger.Warning($"{contractName} does not implements proper triggers, can't be upgraded later.");
                }
            }

            var hash = ExecuteTransaction(api, script, ProofOfWork.Minimal, Keys);
            if (hash != Hash.Null)
            {
                var expectedEvent = isUpgrade ? EventKind.ContractUpgrade : (isToken ? EventKind.TokenCreate : EventKind.ContractDeploy);
                var expectedEventStr = expectedEvent.ToString();

                var events = GetTransactionEvents(hash);
                if (events.Any(x => x.kind == expectedEventStr))
                {
                    var contractAddress = SmartContract.GetAddressForName(contractName);

                    string action = isUpgrade ? "Upgraded" : "Deployed";

                    logger.Message($"{action} {contractName} at {contractAddress}");
                }
                else
                {
                    throw new CommandException("Transaction was confirmed but deployment event is missing!");
                }
            }
        }

        public static void Deploy(string[] args, NexusAPI api, BigInteger minFee)
        {
            DeployOrUpgrade(args, api, minFee, false);
        }

        public static void Upgrade(string[] args, NexusAPI api, BigInteger minFee)
        {
            DeployOrUpgrade(args, api, minFee, true);
        }
    }
}
