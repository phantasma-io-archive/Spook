using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Phantasma.Numerics;
using Phantasma.API;
using Phantasma.Cryptography;
using Phantasma.Spook.Oracles;
using Phantasma.Core.Log;
using Phantasma.Core.Utils;
using Phantasma.Pay;
using Phantasma.Blockchain.Tokens;
using Phantasma.Neo.Core;
using System.Linq;
using Phantasma.Blockchain;
using Phantasma.Domain;

namespace Phantasma.Spook.Swaps
{
    public class TokenSwapper
    {
        public readonly KeyPair Keys;
        public readonly NexusAPI nexusAPI;
        public readonly NeoScanAPI neoscanAPI;
        public readonly Logger logger;
        public readonly BigInteger MinFee;

        public Dictionary<string, ChainSwap> swapMap = new Dictionary<string, ChainSwap>();

        public Dictionary<string, ChainInterop> interopMap = new Dictionary<string, ChainInterop>();

        private static readonly string swapFile = "swaps.csv";

        private bool ready = false;



        public TokenSwapper(KeyPair swapKey, NexusAPI nexusAPI, NeoScanAPI neoscanAPI, NeoAPI neoAPI, BigInteger minFee, Logger logger, Arguments arguments)
        {
            this.Keys = swapKey;
            this.nexusAPI = nexusAPI;
            this.neoscanAPI = neoscanAPI;
            this.logger = logger;
            this.MinFee = minFee;

            var interopBlocks = new Dictionary<string, BigInteger>();

            interopBlocks["phantasma"] = BigInteger.Parse(arguments.GetString("interop.phantasma.height", "0"));
            interopBlocks["neo"] = BigInteger.Parse(arguments.GetString("interop.neo.height", "4261049"));
            interopBlocks["ethereum"] = BigInteger.Parse(arguments.GetString("interop.ethereum.height", "4261049"));

            var platforms = ((ArrayResult)nexusAPI.GetPlatforms()).values.Select(x => (PlatformResult)x).ToArray();

            foreach (var entry in interopBlocks)
            {
                BigInteger blockHeight = entry.Value;

                ChainInterop interop;

                switch (entry.Key)
                {
                    case "phantasma":
                        interop = new PhantasmaInterop(this, swapKey, blockHeight);
                        break;

                    case "neo":
                        interop = new NeoInterop(this, swapKey, blockHeight, neoAPI);
                        break;

                    case "ethereum":
                        interop = new EthereumInterop(this, swapKey, blockHeight);
                        break;

                    default:
                        interop = null;
                        break;
                }

                if (interop != null)
                {
                    bool shouldAdd = true;

                    if (!(interop is PhantasmaInterop))
                    {
                        logger.Message($"{interop.Name}.Swap.Private: {interop.PrivateKey}");
                        logger.Message($"{interop.Name}.Swap.{interop.Name}: {interop.LocalAddress}");
                        logger.Message($"{interop.Name}.Swap.Phantasma: {interop.ExternalAddress}");

                        for (int i=0; i<platforms.Length; i++)
                        {
                            var temp = platforms[i];
                            if (temp.platform == interop.Name)
                            {
                                if (temp.address != interop.LocalAddress)
                                {
                                    logger.Error($"{interop.Name} address mismatch, should be {temp.address}. Make sure you are using the proper swap seed.");
                                    shouldAdd = false;
                                }
                            }
                        }
                    }

                    if (shouldAdd)
                    {
                        interopMap[entry.Key] = interop;
                    }
                }
            }

            bool interopsAvailable = false;
            foreach (var entry in interopMap)
            {
                if (entry.Key != DomainSettings.PlatformName)
                {
                    interopsAvailable = true;
                    break;
                }
            }

            if (!interopsAvailable)
            {
                logger.Error($"No interops available, disabling token swapping.");
                return;
            }

            if (File.Exists(swapFile))
            {
                var lines = File.ReadAllLines(swapFile);
                foreach (var line in lines)
                {
                    var entries = line.Split(',');
                    var swap = new ChainSwap();
                    swap.sourceHash = entries[0];
                    swap.sourcePlatform = entries[1];
                    swap.sourceAddress = entries[2];
                    swap.destinationHash = entries[3];
                    swap.destinationPlatform = entries[4];
                    swap.destinationAddress = entries[5];
                    swap.symbol = entries[6];
                    swap.amount = decimal.Parse(entries[7]);

                    swapMap[swap.sourceHash] = swap;
                }
            }

            ready = true;
        }

        public void Run()
        {
            Thread.Sleep(5000);

            if (!ready)
            {
                return;
            }

            try
            {
                foreach (var interop in interopMap.Values)
                {
                    interop.Update((swaps) => ProcessSwaps(interop, swaps));
                }
            }
            catch (Exception e)
            {
                logger.Error("Swapper exception: " + e.Message);
                Thread.Sleep(5000);
            }
        }

        // finds which blockchain interop address matches the supplied address
        public string FindInteropByAddress(Address address)
        {
            foreach (var interop in interopMap.Values)
            {
                if (interop is PhantasmaInterop)
                {
                    continue;
                }

                if (interop.ExternalAddress == address)
                {
                    return interop.Name;
                }
            }

            return null;
        }

        public bool FindTokenByHash(string hashText, out TokenInfo token)
        {
            var nexus = nexusAPI.Nexus;

            var hash = Hash.FromUnpaddedHex(hashText);

            foreach (var symbol in nexus.Tokens)
            {
                var info = nexus.GetTokenInfo(symbol);
                if (hash.Equals(info.Hash))
                {
                    token = info;
                    return true;
                }
            }

            token = new TokenInfo();
            return false;
        }

        public bool FindTokenBySymbol(string symbol, out TokenInfo token)
        {
            var nexus = nexusAPI.Nexus;

            if (nexus.TokenExists(symbol))
            {
                token = nexus.GetTokenInfo(symbol);
                return true;
            }

            token = new TokenInfo();
            return false;
        }

        public ChainInterop FindInterop(string chainName)
        {
            if (interopMap.ContainsKey(chainName))
            {
                return interopMap[chainName];
            }

            throw new InteropException("Could not find interop for " + chainName);
        }

        internal string FromExternalToLocal(Address sourceAddress, string chainName)
        {
            var temp = nexusAPI.GetSwapAddress(sourceAddress.Text, chainName);
            if (temp is SingleResult)
            {
                var addrText = (string)((SingleResult)temp).value;
                var address = Address.FromText(addrText);
                string resultChainName;
                string resultAddress;
                WalletUtils.DecodePlatformAndAddress(address, out resultChainName, out resultAddress);

                if (resultChainName != chainName)
                {
                    throw new InteropException($"Something went wrong, chain names dont match, {chainName} vs {resultChainName}");
                }

                return resultAddress;
            }

            throw new InteropException($"Could not map address {sourceAddress} to a {chainName} address");
        }

        internal Address FromLocalToExternal(string sourceAddress, string chainName)
        {
            var tempAddress = WalletUtils.EncodeAddress(sourceAddress, chainName);
            var temp = nexusAPI.GetSwapAddress(tempAddress.Text, "phantasma");
            if (temp is SingleResult)
            {
                var addrText = (string)((SingleResult)temp).value;
                var address = Address.FromText(addrText);
                return address;
            }

            throw new InteropException($"Could not map address {sourceAddress} to a Phantasma address");
        }

        private void ProcessSwaps(ChainInterop interop, IEnumerable<ChainSwap> swaps)
        {
            foreach (var temp in swaps)
            {
                var swap = temp;

                if (swapMap.ContainsKey(swap.sourceHash))
                {
                    continue;
                }

                if (interopMap.ContainsKey(swap.destinationPlatform))
                {
                    logger.Message($"Executing {swap.sourcePlatform} swap: {swap.sourceAddress} sent {swap.amount} {swap.symbol}");

                    var sourceInterop = interop;
                    var destinationInterop = FindInterop(swap.destinationPlatform);

                    if (sourceInterop is PhantasmaInterop)
                    {
                        var phantasma = (PhantasmaInterop)sourceInterop;
                        string brokerHash;
                        
                        var brokerResult = phantasma.PrepareBroker(swap, out brokerHash);
                        if (brokerResult ==  BrokerResult.Error || string.IsNullOrEmpty(brokerHash))
                        {
                            throw new InteropException("Failed broker transaction for swap with hash " + swap.sourceHash);
                        }

                        if (brokerResult == BrokerResult.Skip)
                        {
                            continue;
                        }
                    }

                    swap.destinationHash = destinationInterop.ReceiveFunds(swap);
                    if (string.IsNullOrEmpty(swap.destinationHash))
                    {
                        throw new InteropException("Failed destination transaction for swap with hash " + swap.sourceHash);
                    }

                    if (sourceInterop is PhantasmaInterop)
                    {
                        logger.Message($"Waiting for {swap.destinationPlatform} transaction confirmation: "+ swap.destinationHash);
                        Thread.Sleep(60 * 1000);
                        var phantasma = (PhantasmaInterop)sourceInterop;
                        var settleHash = phantasma.SettleTransaction(swap.destinationHash, swap.destinationPlatform);
                        if (string.IsNullOrEmpty(settleHash))
                        {
                            throw new InteropException("Failed settle transaction for swap with hash " + swap.sourceHash);
                        }
                    }

                    File.AppendAllText(swapFile, $"{swap.sourceHash},{swap.sourcePlatform},{swap.sourceAddress},{swap.destinationHash},{swap.destinationPlatform},{swap.destinationAddress},{swap.symbol},{swap.amount}{Environment.NewLine}");
                    swapMap[swap.sourceHash] = swap;

                    logger.Success($"Finished {swap.sourcePlatform} swap: {swap.destinationAddress} received {swap.amount} {swap.symbol}");
                }
                else
                {
                    throw new InteropException("Unknown interop: " + swap.destinationPlatform);
                }
            }
        }
    }
}
