using System;
using System.Collections.Generic;
using System.Linq;

using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Contracts.Native;
using Phantasma.Simulator;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;
using Phantasma.Storage;
using Phantasma.VM.Utils;
using Phantasma.Core;
using Phantasma.Domain;
using Phantasma.Contracts.Extra;

namespace Phantasma.Spook.Nachomen
{
    class NachoServer
    {
        private static int _legendaryWrestlerDelay = 0;
        private static int _legendaryItemDelay = 0;

        private static Dictionary<Rarity, Queue<NachoItem>>     _itemQueue      = new Dictionary<Rarity, Queue<NachoItem>>();
        private static Dictionary<Rarity, Queue<NachoWrestler>> _wrestlerQueue  = new Dictionary<Rarity, Queue<NachoWrestler>>();

        private static Random _rnd = new Random();

        private static BigInteger _nextItemID;

        public static void InitNachoServer(Nexus nexus, NexusSimulator simulator, PhantasmaKeys ownerKeys, bool fillMarket, Logger logger)
        {
            /*
            var keys = KeyPair.FromWIF("L2sbKk7TJTkbwbwJ2EX7qM23ycShESGhQhLNyAaKxVHEqqBhFMk3");
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(new KeyPair[] { keys, ownerKeys }, ProofOfWork.None, () => new ScriptBuilder().AllowGas(ownerKeys.Address, Address.Null, simulator.MinimumFee, 999).CallContract("interop", "RegisterLink", keys.Address, NeoWallet.EncodeAddress("AbZJjZ5F1x82VybfsqM7zi4nkWoX8uwepy")).SpendGas(ownerKeys.Address).EndScript());
            simulator.EndBlock();*/

            //GenerateBotGenes(ownerKeys.Address, logger);

            //InitialNachoFill();

            if (fillMarket)
            {
                GenerateTokens(nexus, simulator, ownerKeys, logger);
                FillNachoMarket(nexus, simulator, ownerKeys, logger);
            }
        }

        public static void GenerateTokens(Nexus nexus, NexusSimulator simulator, PhantasmaKeys ownerKeys, Logger logger)
        {
            simulator.BeginBlock();
            simulator.GenerateChain(ownerKeys, nexus.RootChain, "nacho", "nacho", "market");
            simulator.EndBlock();

            var nachoAddress = Address.FromText("PWx9mn1hEtQCNxBEhKPj32L3yjJZFiEcLEGVJtY7xg8Ss");
            
            var nachoFuel = UnitConversion.ToBigInteger(5, DomainSettings.FuelTokenDecimals);
            var nachoChain = nexus.GetChainByName("nacho");

            simulator.BeginBlock();
            simulator.GenerateSideChainSend(ownerKeys, DomainSettings.FuelTokenSymbol, nexus.RootChain, ownerKeys.Address, nachoChain, nachoFuel, 0);
            simulator.GenerateSideChainSend(ownerKeys, DomainSettings.FuelTokenSymbol, nexus.RootChain, nachoAddress, nachoChain, nachoFuel, 9999);

            //_chainSimulator.GenerateSideChainSend(_ownerKeys, Nexus.FuelTokenSymbol, _nexus.RootChain, nachoAddress2, nachoChain, nachoFuel, 9999);
            var blockA = simulator.EndBlock().First();

            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(ownerKeys, nexus.RootChain, nachoChain, blockA.Hash);
            simulator.EndBlock();

            simulator.BeginBlock();

            var nachoSupply = UnitConversion.ToBigInteger(10000, Constants.NACHO_TOKEN_DECIMALS);
            simulator.GenerateToken(ownerKeys, Constants.NACHO_SYMBOL, "Nachomen Token", DomainSettings.PlatformName, Hash.FromString(Constants.NACHO_SYMBOL), nachoSupply, Constants.NACHO_TOKEN_DECIMALS, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible);
            simulator.MintTokens(ownerKeys, ownerKeys.Address, Constants.NACHO_SYMBOL, nachoSupply);

            var wrestlerTokenScript = new []
            {
                "alias r2 $address",
                "alias r3 $tokenID",

                "pop $address",
                "pop $tokenID",

                "load r0 \"OnSend\"",
                "equal r0, r1, r0",
                "jmpif r0, @execTrigger",
                "ret",

                "@execTrigger:",
                "load r0 \"nacho\"",
                "ctx r0 r1",
                "load r0 \"OnSendWrestlerTrigger\"",
                "push $address",
                "push $tokenID",
                "switch r1",
                "ret"
            };

            var wrestlerCallScript = AssemblerUtils.BuildScript(wrestlerTokenScript);

            simulator.GenerateToken(ownerKeys, Constants.WRESTLER_SYMBOL, "Nachomen Luchador", DomainSettings.PlatformName, Hash.FromString(Constants.WRESTLER_SYMBOL), 0, 0, TokenFlags.Transferable, wrestlerCallScript);

            var itemTokenScript = new[]
            {
                "alias r2 $address",
                "alias r3 $tokenID",

                "pop $address",
                "pop $tokenID",

                "load r0 \"OnSend\"",
                "equal r0, r1, r0",
                "jmpif r0, @execTrigger",
                "ret",

                "@execTrigger:",
                "load r0 \"nacho\"",
                "ctx r0 r1",
                "load r0 \"OnSendItemTrigger\"",
                "push $address",
                "push $tokenID",
                "switch r1",
                "ret"
            };

            var itemCallScript = AssemblerUtils.BuildScript(itemTokenScript);

            simulator.GenerateToken(ownerKeys, Constants.ITEM_SYMBOL, "Nachomen Item", DomainSettings.PlatformName, Hash.FromString(Constants.ITEM_SYMBOL), 0, 0, TokenFlags.Transferable, itemCallScript);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateSideChainSend(ownerKeys, Constants.NACHO_SYMBOL, nexus.RootChain, nachoAddress, nachoChain, UnitConversion.ToBigInteger(1000, Constants.NACHO_TOKEN_DECIMALS), 1);
            simulator.GenerateSideChainSend(ownerKeys, Constants.NACHO_SYMBOL, nexus.RootChain, ownerKeys.Address, nachoChain, UnitConversion.ToBigInteger(1000, Constants.NACHO_TOKEN_DECIMALS), 1);
            //_chainSimulator.GenerateSideChainSend(_ownerKeys, Constants.NACHO_SYMBOL, _nexus.RootChain, nachoAddress2, nachoChain, UnitConversion.ToBigInteger(1000, 10), 1);
            var blockB = simulator.EndBlock().First();

            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(ownerKeys, nexus.RootChain, nachoChain, blockB.Hash);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateSetTokenMetadata(ownerKeys, Constants.WRESTLER_SYMBOL, "details", "https://nacho.men/luchador/*");
            simulator.GenerateSetTokenMetadata(ownerKeys, Constants.WRESTLER_SYMBOL, "viewer", "https://nacho.men/luchador/body/*");
            simulator.EndBlock();
        }

        private static void InitialNachoFill()
        {
            //if (contract.debugMode)
            //{
            //    var validCustomMoves = new HashSet<WrestlingMove>(Enum.GetValues(typeof(WrestlingMove)).Cast<WrestlingMove>().Where(x => Rules.CanBeInMoveset(x)));

            //    foreach (var entry in bannedMoves)
            //    {
            //        validCustomMoves.Remove(entry);
            //    }

            //    foreach (var move in validCustomMoves)
            //    {
            //        Console.WriteLine("Generating wrestler with " + move);

            //        var genes = Luchador.MineGenes(rnd, p => WrestlerValidation.IsValidWrestler(p) && p.HasMove(move) && ((p.Rarity == Rarity.Common && move != WrestlingMove.Block) || (move == WrestlingMove.Block && p.Rarity == Rarity.Legendary)));

            //        var rand = new Random();
            //        var isWrapped = rand.Next(0, 100) < 50;

            //        var tx = this.CallContract(owner_keys, "GenerateWrestler", new object[] { owner_keys.Address, genes, (uint)0, isWrapped });
            //        if (tx.content != null)
            //        {
            //            var ID = Serialization.Unserialize<BigInteger>(tx.content);

            //            var luchador = Luchador.FromGenes(0, genes);
            //            Console.WriteLine($"\t\tPrimary Move: {luchador.PrimaryMove}");
            //            Console.WriteLine($"\t\tSecondary Move: {luchador.SecondaryMove}");
            //            Console.WriteLine($"\t\tSupport Move: {luchador.TertiaryMove}");
            //            Console.WriteLine($"\t\tStance Move: {luchador.StanceMove}");

            //            BigInteger price = TokenUtils.ToBigInteger(1);

            //            //var random          = new Random();
            //            //var randomInt       = random.Next(1, 101);
            //            //var randomCurrency  = AuctionCurrency.SOUL;

            //            //if (randomInt > 66)
            //            //{
            //            //    randomCurrency = AuctionCurrency.USD;
            //            //}
            //            //else if (randomInt > 33)
            //            //{
            //            //    randomCurrency = AuctionCurrency.NACHO;
            //            //}

            //            CallContract(owner_keys, "SellWrestler", new object[] { owner_keys.Address, ID, price, price, AuctionCurrency.NACHO, Constants.MAXIMUM_AUCTION_SECONDS_DURATION, "" });
            //        }
            //        else
            //        {
            //            Console.WriteLine("Fatal error: Could not generate wrestler");
            //            Environment.Exit(-1);
            //        }
            //    }

            //    ITEMS

            //   var itemValues = Enum.GetValues(typeof(ItemKind)).Cast<ItemKind>().Where(x => x != ItemKind.None && Rules.IsReleasedItem(x)).ToArray();
            //    var itemExpectedSet = new HashSet<ItemKind>(itemValues);
            //    var itemGeneratedSet = new HashSet<ItemKind>();
            //    var itemIDs = new List<BigInteger>();

            //    while (itemGeneratedSet.Count < itemExpectedSet.Count)
            //    {
            //        var kind = Formulas.GetItemKind(lastItemID);
            //        if (itemExpectedSet.Contains(kind) && !itemGeneratedSet.Contains(kind))
            //        {
            //            Console.WriteLine("Generated item: " + kind);
            //            itemGeneratedSet.Add(kind);
            //            itemIDs.Add(lastItemID);
            //        }
            //        else
            //        if (Rules.IsReleasedItem(kind))
            //        {
            //            EnqueueItem(lastItemID, Rules.GetItemRarity(kind));
            //        }

            //        lastItemID++;
            //    }

            //    foreach (var itemID in itemIDs)
            //    {
            //        var rand = new Random();
            //        var isWrapped = rand.Next(0, 100) < 50;

            //        var tx = this.CallContract(owner_keys, "GenerateItem", new object[] { owner_keys.Address, itemID, isWrapped });
            //        BigInteger price = TokenUtils.ToBigInteger(5);

            //        //var random = new Random();
            //        //var randomInt = random.Next(1, 101);
            //        //var randomCurrency = AuctionCurrency.SOUL;

            //        //if (randomInt > 66)
            //        //{
            //        //    randomCurrency = AuctionCurrency.USD;
            //        //}
            //        //else if (randomInt > 33)
            //        //{
            //        //    randomCurrency = AuctionCurrency.NACHO;
            //        //}

            //        CallContract(owner_keys, "SellItem", new object[] { owner_keys.Address, itemID, price, price, AuctionCurrency.NACHO, Constants.MAXIMUM_AUCTION_SECONDS_DURATION, "" });
            //    }

            //    lastItemID = itemIDs[itemIDs.Count - 1];
            //}
        }

        private static void FillNachoMarket(Nexus nexus, NexusSimulator simulator, PhantasmaKeys ownerKeys, Logger logger)
        {
            logger.Message("Filling initial nacho market");

            var nachoChain = simulator.Nexus.GetChainByName("nacho");
            //var nachoChain = simulator.Nexus.RootChain;

            //_logger.Message("token owner: " + _ownerKeys.Address.Text + " | test user: " + testUser.Address.Text);

            var luchadorCounts = new Dictionary<Rarity, int>
            {
                [Rarity.Common] = 65,
                [Rarity.Uncommon] = 25,
                [Rarity.Rare] = 9,
                [Rarity.Epic] = 1,
                [Rarity.Legendary] = 1
            };

            //var testUser = KeyPair.FromWIF("L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25"); // => PKtRvkhUhAiHsg4YnaxSM9dyyLBogTpHwtUEYvMYDKuV8
            var testUser = PhantasmaKeys.FromWIF("Kwgg5tbcgDmZ5UFgpwbv96CvduBA2T5kSVSmEYiqmW8QdvGHKH25"); // => PXZiNZJarPaErRjJZZuAvAHSCN8oyJUy5Gec1jv4k6eEJ

            // Transfer Fuel Tokens to the test user address
            simulator.BeginBlock();
            simulator.GenerateTransfer(ownerKeys, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000);
            simulator.EndBlock();

            // WRESTLERS

            logger.Message("Filling the market with luchadores...");

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            var createdAuctions = 0;

            foreach (var rarity in luchadorCounts.Keys)
            {
                if (rarity == Rarity.Legendary)
                {
                    if (_legendaryWrestlerDelay > 0)
                    {
                        _legendaryWrestlerDelay--;
                        continue;
                    }

                    _legendaryWrestlerDelay = 10;                    
                }

                var count = luchadorCounts[rarity];
                logger.Message($"Generating {count} {rarity} luchadores...");

                var wrestlerToken = simulator.Nexus.GetTokenInfo(Constants.WRESTLER_SYMBOL);
                Throw.If(!nexus.TokenExists(Constants.WRESTLER_SYMBOL), "Can't find the token symbol");

                for (var i = 1; i <= count; i++)
                {
                    var wrestler        = DequeueNachoWrestler(ownerKeys, rarity);
                    var wrestlerBytes   = wrestler.Serialize();

                    var rand        = new Random();
                    var isWrapped   = rand.Next(0, 100) < 50; // TODO update logic for the lootboxes (1 wrestler lootbox = 1 wrapped wrestler)

                    // Mint a new Wrestler Token directly on the user
                    var tokenROM = new byte[0];     //wrestlerBytes;
                    var tokenRAM = wrestlerBytes;   //new byte[0];

                    simulator.BeginBlock();
                    var mintTx = simulator.MintNonFungibleToken(ownerKeys, ownerKeys.Address, Constants.WRESTLER_SYMBOL, tokenROM, tokenRAM);
                    var blockA = simulator.EndBlock().First();

                    var tokenID = BigInteger.Zero;

                    if (blockA != null)
                    {
                        Throw.IfNull(mintTx, nameof(mintTx));

                        var txEvents = blockA.GetEventsForTransaction(mintTx.Hash);
                        Throw.If(!txEvents.Any(x => x.Kind == EventKind.TokenMint), "missing mint event");

                        foreach (var evt in txEvents)
                        {
                            if (evt.Kind != EventKind.TokenMint) continue;

                            var eventData = evt.GetContent<TokenEventData>();

                            tokenID = eventData.Value;
                        }
                    }

                    var fuelAmount = UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals);
                    var extraFee = UnitConversion.ToBigInteger(0.0001m, DomainSettings.FuelTokenDecimals);

                    // transfer wrestler nft from main chain to nacho chain
                    simulator.BeginBlock();
                    simulator.GenerateSideChainSend(ownerKeys, DomainSettings.FuelTokenSymbol, nexus.RootChain, ownerKeys.Address, nachoChain, fuelAmount, 0);
                    simulator.GenerateNftSidechainTransfer(ownerKeys, ownerKeys.Address, nexus.RootChain, nachoChain, wrestlerToken.Symbol, tokenID);
                    var blockB = simulator.EndBlock().First();

                    simulator.BeginBlock();
                    simulator.GenerateSideChainSettlement(ownerKeys, nexus.RootChain, nachoChain, blockB.Hash);
                    simulator.EndBlock();

                    // Create auction
                    decimal minPrice, maxPrice;

                    GetLuchadorPriceRange(rarity, out minPrice, out maxPrice);
                    var diff    = (int)(maxPrice - minPrice);
                    var price   = (int)(minPrice + _rnd.Next() % diff);

                    if (price < 0) price *= -1; // HACK

                    var finalPrice = UnitConversion.ToBigInteger(price, Constants.NACHO_TOKEN_DECIMALS);

                    createdAuctions++;

                    Timestamp endWrestlerAuctionDate = simulator.CurrentTime + TimeSpan.FromDays(2);

                    simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(ownerKeys, ProofOfWork.None, nachoChain, () =>
                        ScriptUtils.
                            BeginScript().
                            AllowGas(ownerKeys.Address, Address.Null, simulator.MinimumFee, 9999).
                            CallContract("market", "SellToken", ownerKeys.Address, wrestlerToken.Symbol, Constants.NACHO_SYMBOL, tokenID, finalPrice, endWrestlerAuctionDate).
                            SpendGas(ownerKeys.Address).
                            EndScript()
                    );
                    simulator.EndBlock();
                }
            }

            auctions = (MarketAuction[])nachoChain.InvokeContract(nachoChain.Storage, "market", "GetAuctions").ToObject();
            Throw.If(auctions.Length != createdAuctions + previousAuctionCount, "wrestler auction ids missing");

            // ITEMS

            logger.Message("Generating items for market...");

            //var itemCounts = new Dictionary<Rarity, int>
            //{
            //    [Rarity.Common]     = 16,
            //    [Rarity.Uncommon]   = 12,
            //    [Rarity.Rare]       = 8,
            //    [Rarity.Epic]       = 2,
            //    [Rarity.Legendary]  = 1
            //};

            MineItems(logger);

            //foreach (var rarity in itemCounts.Keys)
            foreach (var rarity in _itemQueue.Keys)
            {
                if (rarity == Rarity.Legendary)
                {
                    if (_legendaryItemDelay > 0)
                    {
                        _legendaryItemDelay--;
                        continue;
                    }

                    _legendaryItemDelay = 10;
                }

                //var count = itemCounts[rarity];
                var count = _itemQueue[rarity].Count;

                logger.Message($"Generating {count} {rarity} items...");


                var itemToken = simulator.Nexus.GetTokenInfo(Constants.ITEM_SYMBOL);
                Throw.If(!nexus.TokenExists(Constants.ITEM_SYMBOL), "Can't find the token symbol");

                for (var i = 1; i <= count; i++)
                {
                    var item        = DequeueItem(rarity, logger);
                    var itemBytes   = item.Serialize();

                    var rand        = new Random();
                    var isWrapped   = rand.Next(0, 100) < 50; // TODO update logic for the lootboxes (1 item lootbox = 1 wrapped item)

                    // Mint a new Item Token directly on the user
                    var tokenROM = new byte[0]; //itemBytes;
                    var tokenRAM = itemBytes;   //new byte[0];

                    simulator.BeginBlock();
                    var mintTx = simulator.MintNonFungibleToken(ownerKeys, ownerKeys.Address, Constants.ITEM_SYMBOL, tokenROM, tokenRAM);
                    var blockA = simulator.EndBlock().First();

                    var tokenID = BigInteger.Zero;

                    if (blockA != null)
                    {
                        Throw.IfNull(mintTx == null, nameof(mintTx));

                        var txEvents = blockA.GetEventsForTransaction(mintTx.Hash);
                        Throw.If(!txEvents.Any(x => x.Kind == EventKind.TokenMint), "mint event missing");

                        foreach (var evt in txEvents)
                        {
                            if (evt.Kind != EventKind.TokenMint) continue;

                            var eventData = evt.GetContent<TokenEventData>();

                            tokenID = eventData.Value;
                        }
                    }

                    var fuelAmount = UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals);
                    var extraFee = UnitConversion.ToBigInteger(0.0001m, DomainSettings.FuelTokenDecimals);

                    // transfer wrestler nft from main chain to nacho chain
                    simulator.BeginBlock();
                    simulator.GenerateSideChainSend(ownerKeys, DomainSettings.FuelTokenSymbol, nexus.RootChain, ownerKeys.Address, nachoChain, fuelAmount, 0);
                    simulator.GenerateNftSidechainTransfer(ownerKeys, ownerKeys.Address, nexus.RootChain, nachoChain, itemToken.Symbol, tokenID);
                    var blockB = simulator.EndBlock().First();

                    simulator.BeginBlock();
                    simulator.GenerateSideChainSettlement(ownerKeys, nexus.RootChain, nachoChain, blockB.Hash);
                    simulator.EndBlock();

                    // Create auction
                    decimal minPrice, maxPrice;

                    GetItemPriceRange(rarity, out minPrice, out maxPrice);
                    var diff    = (int)(maxPrice - minPrice);
                    var price   = (int)(minPrice + _rnd.Next() % diff);

                    if (price < 0) price *= -1; // HACK

                    var finalPrice = UnitConversion.ToBigInteger(price, Constants.NACHO_TOKEN_DECIMALS);

                    createdAuctions++;

                    Timestamp endItemAuctionDate = simulator.CurrentTime + TimeSpan.FromDays(2);

                    simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(ownerKeys, ProofOfWork.None, nachoChain, () =>
                        ScriptUtils.
                            BeginScript().
                            AllowGas(ownerKeys.Address, Address.Null, simulator.MinimumFee, 9999).
                            CallContract("market", "SellToken", ownerKeys.Address, itemToken.Symbol, Constants.NACHO_SYMBOL, tokenID, finalPrice, endItemAuctionDate).
                            SpendGas(ownerKeys.Address).
                            EndScript()
                    );
                    simulator.EndBlock();
                }
            }

            auctions = (MarketAuction[])nachoChain.InvokeContract(nachoChain.Storage, "market", "GetAuctions").ToObject();
            Throw.If(auctions.Length != createdAuctions + previousAuctionCount, "items auction ids missing");

            logger.Success("Nacho Market is ready!");
        }

        /*
        private static void GenerateBotGenes(Address owner, Logger logger)
        {
            logger.Message("Generate genes for bots");

            var rnd = new Random();

            for (var n = 1; n <= 8; n++)
            {
                var level = (PracticeLevel)n;

                HashSet<WrestlingMove> wantedMoves;

                switch (level)
                {
                    case PracticeLevel.Wood:     wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Bash }); break;
                    case PracticeLevel.Iron:     wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Block }); break;
                    case PracticeLevel.Steel:    wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Corkscrew }); break;
                    case PracticeLevel.Silver:   wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Bulk }); break;
                    case PracticeLevel.Gold:     wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Chicken_Wing }); break;
                    case PracticeLevel.Ruby:     wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Refresh }); break;
                    case PracticeLevel.Emerald:  wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Rhino_Charge }); break;
                    case PracticeLevel.Diamond:  wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Razor_Jab }); break;
                    default:                    throw new ContractException("No wanted moves for the bot: " + level);
                }

                //level = PracticeLevel.Wood;
                //logger.Message("Mining bot: " + level);

                var genes = Luchador.MineBotGenes(rnd, level/*, wantedMoves*);
                var genes = Luchador.MineBotGenes(rnd, level/*, wantedMoves*);

                //for (var i = 0; i < genes.Length; i++)
                //{
                //    logger.Message(i + ", ");
                //}

                var bb = Luchador.FromGenes(n, genes);
                var temp = bb.data;
                bb.data = temp;

                logger.Message(bb.Name);
                //logger.Message("Kind: " + bb.Rarity);
                //logger.Message("Head: " + bb.GetBodyPart(BodyPart.Head).Variation);
                //logger.Message("Primary Move: " + bb.PrimaryMove);
                //logger.Message("Secondary Move: " + bb.SecondaryMove);
                //logger.Message("Support Move: " + bb.TertiaryMove);
                //logger.Message("Stance Move: " + bb.StanceMove);
                //logger.Message("Base STA: " + bb.BaseStamina);
                //logger.Message("Base ATK: " + bb.BaseAttack);
                //logger.Message("Base DEF: " + bb.BaseDefense);
                //logger.Message("------------------");
            }
        }
        */

        public static void GetLuchadorPriceRange(Rarity level, out decimal min, out decimal max)
        {
            switch (level)
            {
                case Rarity.Common: min = 7; max = 10; break;
                case Rarity.Uncommon: min = 75m; max = 100; break;
                case Rarity.Rare: min = 750m; max = 1000; break;
                case Rarity.Epic: min = 6500m; max = 7000; break;
                case Rarity.Legendary: min = 30000m; max = 35000; break;
                default: min = 7.5m; max = 10; break;
            }
        }

        public static void GetItemPriceRange(Rarity level, out decimal min, out decimal max)
        {
            switch (level)
            {
                case Rarity.Common: min = 3; max = 5; break;
                case Rarity.Uncommon: min = 35m; max = 50; break;
                case Rarity.Rare: min = 350m; max = 500; break;
                case Rarity.Epic: min = 3000m; max = 3500; break;
                case Rarity.Legendary: min = 15000m; max = 20000; break;
                default: min = 3.5m; max = 30; break;
            }
        }

        /// <summary>
        /// Mine all released items. One item of each kind
        /// </summary>
        private static void MineItems(Logger logger)
        {
            var itemValues = Enum.GetValues(typeof(ItemKind)).Cast<ItemKind>().ToArray();
            foreach (var kind in itemValues)
            {
                if (!Rules.IsReleasedItem(kind))
                {
                    //logger.Message("skip: " + kind);
                    continue;
                }

                //logger.Message("generated item: " + kind);
                var item = new NachoItem()
                {
                    kind        = kind,
                    flags       = ItemFlags.None,
                    location    = ItemLocation.None,
                    wrestlerID  = 0
                };

                var rarity = Rules.GetItemRarity(kind);
                EnqueueItem(item, rarity, logger);
            }
        }

        private static void MineRandomItems(int amount, Logger logger)
        {
            while (amount > 0)
            {
                _nextItemID++;

                //var itemKind = Formulas.GetItemKind(_lastItemID);
                //var itemKind = GetItem(nexus, ownerKeys, _lastItemID).kind;

                var itemKind = (ItemKind) (int)_nextItemID;

                logger.Message("next item id: " + _nextItemID + " | kind: " + itemKind + " | amount: " + amount);

                if (_nextItemID > Enum.GetValues(typeof(ItemKind)).Length) break;

                if (!Rules.IsReleasedItem(itemKind)) continue;

                var item = new NachoItem()
                {
                    kind        = itemKind,
                    flags       =  ItemFlags.None,
                    location    = ItemLocation.None,
                    wrestlerID  = 0
                };

                var rarity = Rules.GetItemRarity(itemKind);
                EnqueueItem(item, rarity, logger);
                amount--;
            }
        }

        private static void EnqueueItem(NachoItem nachoItem, Rarity rarity, Logger logger)
        {
            // -------------------------
            // Old code

            //Queue<NachoItem> queue;

            //if (_itemQueue.ContainsKey(rarity))
            //{
            //    //logger.Message("ENQUEUE contains rarity: " + rarity + " | add item kind: " + nachoItem.kind);
            //    queue = _itemQueue[rarity];
            //}
            //else
            //{
            //    //logger.Message("ENQUEUE NOT contains rarity: " + rarity + " | add item kind: " + nachoItem.kind);

            //    queue = new Queue<NachoItem>();
            //}

            //queue.Enqueue(nachoItem);
            //_itemQueue[rarity] = queue;

            // ----------------------------

            if (!_itemQueue.ContainsKey(rarity))
            {
                _itemQueue[rarity] = new Queue<NachoItem>();
            }

            _itemQueue[rarity].Enqueue(nachoItem);
        }

        private static NachoItem DequeueItem(Rarity rarity, Logger logger)
        {
            //logger.Message("DEQUEUE item / rarity: " + rarity + " | item not contains: " + !_itemQueue.ContainsKey(rarity) + " | next item id: " + (int)_nextItemID);

            while (!_itemQueue.ContainsKey(rarity) || _itemQueue[rarity].Count == 0)
            {
                //logger.Message("rarity: " + rarity + " | item not contains: " + !_itemQueue.ContainsKey(rarity));

                //if (_itemQueue.ContainsKey(rarity))
                //{
                //    logger.Message("rarity: " + rarity + " | count: " + _itemQueue[rarity].Count);
                //}

                MineRandomItems(10, logger);
                //MineRandomItems(Enum.GetValues(typeof(ItemKind)).Length - 1, logger);
            }

            return _itemQueue[rarity].Dequeue();
        }

        private static void MineRandomLuchadores(PhantasmaKeys ownerKey, int amount)
        {
            while (amount > 0)
            {
                var wrestler = new NachoWrestler()
                {
                    battleCount = 0,
                    comments = new string[0],
                    currentMojo = 10,
                    experience = 10000,
                    flags = WrestlerFlags.None,
                    genes = Luchador.MineGenes(_rnd, null),
                    gymBoostAtk = byte.MaxValue,
                    gymBoostDef = byte.MaxValue,
                    gymBoostStamina = byte.MaxValue,
                    gymTime = 0,
                    itemID = 0,
                    location = WrestlerLocation.None,
                    maskOverrideCheck = byte.MaxValue,
                    maskOverrideID = byte.MaxValue,
                    maskOverrideRarity = byte.MaxValue,
                    maxMojo = 10,
                    mojoTime = 0,
                    moveOverrides = new byte[0],
                    nickname = string.Empty,
                    perfumeTime = 0,
                    practiceLevel = PracticeLevel.Gold,
                    roomTime = 0,
                    score = 0,
                    stakeAmount = 0,
                    trainingStat = StatKind.None
                };

                var luchador = Luchador.FromData(1, ownerKey.Address, wrestler);

                if (WrestlerValidation.IsValidWrestler(luchador))
                {
                    amount--;
                    EnqueueNachoWrestler(wrestler);
                }
            }
        }

        private HashSet<WrestlingMove> bannedMoves = new HashSet<WrestlingMove>(new WrestlingMove[]
        {
            WrestlingMove.Pray,
            WrestlingMove.Hyper_Slam,
        });

        private static void EnqueueNachoWrestler(NachoWrestler wrestler)
        {
            Queue<NachoWrestler> queue;

            var rarity = GetWrestlerRarity(wrestler);
            if (_wrestlerQueue.ContainsKey(rarity))
            {
                queue = _wrestlerQueue[rarity];
            }
            else
            {
                queue = new Queue<NachoWrestler>();
                _wrestlerQueue[rarity] = queue;
            }

            queue.Enqueue(wrestler);
        }

        private static NachoWrestler DequeueNachoWrestler(PhantasmaKeys ownerKeys, Rarity rarity)
        {
            while (!_wrestlerQueue.ContainsKey(rarity) || _wrestlerQueue[rarity].Count == 0)
            {
                MineRandomLuchadores(ownerKeys, 10);
            }

            return _wrestlerQueue[rarity].Dequeue();
        }

        public static Rarity GetWrestlerRarity(NachoWrestler wrestler)
        {
            if (wrestler.genes == null)
            {
                return Rarity.Common;
            }

            var n = (wrestler.genes[Constants.GENE_RARITY] % 6);
            return (Rarity)n;
        }

        // TODO error handling when item not exist
        private static NachoItem GetItem(Nexus nexus, PhantasmaKeys ownerKeys, BigInteger ID)
        {
            var nft = nexus.GetNFT(Constants.ITEM_SYMBOL, ID);

            var item = Serialization.Unserialize<NachoItem>(nft.RAM);

            if (item.location == ItemLocation.Wrestler)
            {
                if (item.wrestlerID != 0)
                {
                    var wrestler = GetWrestler(nexus, ownerKeys, item.wrestlerID);
                    if (wrestler.itemID != ID)
                    {
                        item.location = ItemLocation.None;
                    }
                }
            }

            return item;
        }


        private static NachoWrestler GetWrestler(Nexus nexus, PhantasmaKeys ownerKeys, BigInteger wrestlerID)
        {
            Throw.If(wrestlerID <= 0, "null or negative id");

            if (wrestlerID < Constants.BASE_LUCHADOR_ID)
            {
                return GetBot(ownerKeys, (int)wrestlerID);
            }

            var nft = nexus.GetNFT(Constants.WRESTLER_SYMBOL, wrestlerID);

            var wrestler = Serialization.Unserialize<NachoWrestler>(nft.RAM);
            if (wrestler.moveOverrides == null || wrestler.moveOverrides.Length < Constants.MOVE_OVERRIDE_COUNT)
            {
                var temp = wrestler.moveOverrides;
                wrestler.moveOverrides = new byte[Constants.MOVE_OVERRIDE_COUNT];

                if (temp != null)
                {
                    for (int i = 0; i < temp.Length; i++)
                    {
                        wrestler.moveOverrides[i] = temp[i];
                    }
                }
            }

            if (nft.CurrentOwner.IsSystem)
            {
                wrestler.location = WrestlerLocation.Market;
            }

            if (wrestler.genes == null || wrestler.genes.Length == 0)
            {
                wrestler.genes = new byte[10];
            }

            if (wrestler.stakeAmount == null)
            {
                wrestler.stakeAmount = 0;
            }

            if (wrestler.itemID != 0)
            {
                //var itemKind = Formulas.GetItemKind(wrestler.itemID);
                var itemKind = GetItem(nexus, ownerKeys, wrestler.itemID).kind;

                // todo confirmar apagar este código. este tryparse já não sentido acho eu
                //int n;
                //if (int.TryParse(itemKind.ToString(), out n))
                //{
                //    wrestler.itemID = 0;
                //}
            }

            if (!IsValidMaskOverride(wrestler))
            {
                wrestler.maskOverrideID = 0;
                wrestler.maskOverrideRarity = 0;
                wrestler.maskOverrideCheck = 0;
            }

            return wrestler;
        }

        private static void IncreaseWrestlerEV(ref NachoWrestler wrestler, StatKind statKind, int obtainedEV)
        {
            var totalEV = wrestler.gymBoostStamina + wrestler.gymBoostAtk + wrestler.gymBoostDef;

            if (totalEV + obtainedEV > Formulas.MaxTrainStat)
            {
                obtainedEV = Formulas.MaxTrainStat - totalEV;
            }

            if (obtainedEV > 0)
            {
                if (statKind == StatKind.Stamina)
                {
                    var newStaminaBoost = wrestler.gymBoostStamina + obtainedEV;
                    if (newStaminaBoost > Constants.MAX_GYM_BOOST)
                    {
                        newStaminaBoost = Constants.MAX_GYM_BOOST;
                    }
                    wrestler.gymBoostStamina = (byte)newStaminaBoost;
                }
                else
                if (statKind == StatKind.Attack)
                {
                    var newAttackBoost = wrestler.gymBoostAtk + obtainedEV;
                    if (newAttackBoost > Constants.MAX_GYM_BOOST)
                    {
                        newAttackBoost = Constants.MAX_GYM_BOOST;
                    }
                    wrestler.gymBoostAtk = (byte)newAttackBoost;
                }
                else
                if (statKind == StatKind.Defense)
                {
                    var newDefenseBoost = wrestler.gymBoostDef + obtainedEV;
                    if (newDefenseBoost > Constants.MAX_GYM_BOOST)
                    {
                        newDefenseBoost = Constants.MAX_GYM_BOOST;
                    }
                    wrestler.gymBoostDef = (byte)newDefenseBoost;
                }
            }
        }

        private static bool IsValidMaskOverride(NachoWrestler wrestler)
        {
            var checksum = (byte)((wrestler.maskOverrideID * 11 * wrestler.maskOverrideRarity * 7) % 256);
            return checksum == wrestler.maskOverrideCheck;
        }

        private static NachoWrestler GetBot(PhantasmaKeys ownerKeys, int botID)
        {
            byte[] genes;
            int level;
            BigInteger botItemID;
            string introText = "";

            var botLevel = (PracticeLevel)(botID);
            switch (botLevel)
            {
                case PracticeLevel.Wood - (int)PracticeLevel.Wood * 2: // PracticeLevel.Wood = -1
                    level = 1; botItemID = 0; genes = new byte[] { 120, 46, 40, 40, 131, 93, 80, 221, 68, 155, };
                    introText = "Beep boop... amigo, entrena conmigo!";
                    break;

                case PracticeLevel.Iron - (int)PracticeLevel.Iron * 2: // PracticeLevel.Iron = -2
                    level = 4; botItemID = 0; genes = new byte[] { 222, 50, 52, 48, 131, 88, 144, 8, 51, 104, };
                    introText = "I'm made from iron and because of that, I'm stronger than my wood brother!";
                    break;

                case PracticeLevel.Steel - (int)PracticeLevel.Steel * 2: // PracticeLevel.Steel = -3
                    level = 6; botItemID = 0; genes = new byte[] { 114, 50, 53, 59, 131, 123, 122, 223, 181, 184, };
                    introText = "Get ready.. because I'm faster and stronger than my iron brother!";
                    break;

                case PracticeLevel.Silver - (int)PracticeLevel.Silver * 2: // PracticeLevel.Silver = -4
                    level = 8; botItemID = 0; genes = new byte[] { 72, 59, 61, 64, 131, 115, 18, 108, 11, 195, };
                    introText = "Counters are for plebs!";
                    break;

                case PracticeLevel.Gold - (int)PracticeLevel.Gold * 2: // PracticeLevel.Gold = -5
                    level = 10; botItemID = 0; genes = new byte[] { 138, 66, 65, 61, 131, 51, 148, 143, 99, 55, };
                    introText = "Luchador... My congratulations for getting so far!";
                    break;

                case PracticeLevel.Ruby - (int)PracticeLevel.Ruby * 2: // PracticeLevel.Ruby = -6
                    level = 13; botItemID = 0; genes = new byte[] { 12, 65, 68, 65, 131, 110, 146, 11, 100, 111 };
                    introText = "Amigo... I'm too strong to fail!";
                    break;

                case PracticeLevel.Emerald - (int)PracticeLevel.Emerald * 2: // PracticeLevel.Emerald = -7
                    level = 16; botItemID = 329390; genes = new byte[] { 240, 76, 73, 79, 131, 68, 218, 145, 232, 20 };
                    introText = "Beep...Beep...My hobby is wasting time in asian basket weaving foruns...";
                    break;

                case PracticeLevel.Diamond - (int)PracticeLevel.Diamond * 2: // PracticeLevel.Diamond = -8
                    level = 20; botItemID = 35808; genes = new byte[] { 144, 76, 77, 76, 131, 46, 168, 202, 141, 188, };
                    introText = "Beep... boop... I am become Death, the destroyer of worlds!";
                    break;

                default:
                    switch (botID)
                    {
                        case -9: level = 1; botItemID = 0; genes = new byte[] { 169, 149, 19, 125, 210, 41, 238, 87, 66, 103, }; break;
                        case -10: level = 1; botItemID = 0; genes = new byte[] { 229, 67, 21, 113, 126, 40, 125, 193, 141, 185, }; break;
                        case -11: level = 1; botItemID = 0; introText = "you should give me your coins if you lose..."; genes = new byte[] { 157, 46, 74, 54, 216, 55, 81, 190, 42, 81, }; break;
                        case -12: level = 2; botItemID = 0; genes = new byte[] { 253, 187, 122, 153, 122, 254, 115, 83, 50, 56, }; break;
                        case -13: level = 2; botItemID = 0; introText = "To hold or no?"; genes = new byte[] { 139, 255, 58, 213, 143, 24, 97, 217, 108, 210, }; break;
                        case -14: level = 3; botItemID = 0; genes = new byte[] { 169, 249, 77, 77, 75, 64, 166, 137, 85, 165, }; break;
                        case -15: level = 3; botItemID = 96178; genes = new byte[] { 187, 61, 210, 174, 9, 149, 2, 180, 127, 46, }; break;
                        case -16: level = 3; botItemID = 0; introText = "I like potatoes with burgers"; genes = new byte[] { 145, 219, 94, 119, 72, 246, 162, 232, 47, 182, }; break;
                        case -17: level = 3; botItemID = 0; genes = new byte[] { 86, 57, 97, 203, 29, 225, 123, 174, 239, 104, }; break;
                        case -18: level = 4; botItemID = 0; genes = new byte[] { 139, 16, 224, 44, 177, 157, 131, 245, 82, 179, }; break;
                        case -19: level = 4; botItemID = 0; introText = "Im all in neo since antshares lol"; genes = new byte[] { 31, 235, 54, 221, 2, 248, 247, 165, 216, 148, }; break;
                        case -20: level = 4; botItemID = 0; genes = new byte[] { 68, 40, 37, 184, 149, 169, 67, 163, 104, 242, }; break;
                        case -21: level = 5; botItemID = 0; introText = "Derp derp derp.."; genes = new byte[] { 115, 24, 16, 61, 155, 239, 232, 59, 116, 109, }; break;
                        case -22: level = 5; botItemID = 0; genes = new byte[] { 73, 79, 227, 227, 138, 103, 98, 1, 255, 106, }; break;
                        case -23: level = 5; botItemID = 0; genes = new byte[] { 134, 103, 6, 7, 106, 172, 149, 135, 18, 36, }; break;
                        case -24: level = 6; botItemID = 30173; genes = new byte[] { 31, 85, 236, 135, 191, 87, 212, 70, 139, 202, }; break;
                        case -25: level = 6; botItemID = 0; introText = "Fugg you mann"; genes = new byte[] { 79, 171, 219, 185, 190, 234, 170, 161, 223, 103, }; break;
                        case -26: level = 6; botItemID = 0; genes = new byte[] { 32, 85, 113, 69, 127, 170, 193, 248, 233, 245, }; break;
                        case -27: level = 7; botItemID = 84882; introText = "Self proclaimed bitcoin maximalist"; genes = new byte[] { 115, 43, 166, 208, 198, 146, 2, 130, 231, 31, }; break;
                        case -28: level = 7; botItemID = 138905; genes = new byte[] { 169, 0, 145, 179, 144, 214, 165, 83, 22, 218, }; break;
                        case -29: level = 7; botItemID = 0; genes = new byte[] { 67, 33, 45, 42, 168, 35, 94, 3, 34, 237, }; break;
                        case -30: level = 7; botItemID = 32478; genes = new byte[] { 169, 172, 84, 63, 74, 69, 60, 65, 15, 20, }; break;
                        case -31: level = 8; botItemID = 0; introText = "SOUL goes 100x if I win"; genes = new byte[] { 235, 14, 247, 227, 158, 106, 178, 5, 25, 240, }; break;
                        case -32: level = 8; botItemID = 0; genes = new byte[] { 73, 204, 196, 177, 33, 2, 87, 242, 33, 219, }; break;
                        case -33: level = 9; botItemID = 329390; introText = "Bantasma fan number one!!"; genes = new byte[] { 25, 188, 160, 127, 57, 106, 143, 248, 79, 84, }; break;
                        case -34: level = 9; botItemID = 0; genes = new byte[] { 121, 215, 5, 48, 178, 2, 231, 109, 183, 226, }; break;
                        case -35: level = 9; botItemID = 63217; genes = new byte[] { 7, 156, 157, 29, 234, 28, 226, 214, 29, 191, }; break;
                        case -36: level = 10; botItemID = 0; introText = "How is babby formed?"; genes = new byte[] { 49, 251, 234, 105, 253, 80, 196, 238, 220, 153, }; break;
                        case -37: level = 10; botItemID = 0; genes = new byte[] { 229, 130, 158, 161, 191, 170, 82, 147, 21, 163, }; break;
                        case -38: level = 11; botItemID = 56842; introText = "Show bobs pls"; genes = new byte[] { 205, 45, 173, 101, 40, 78, 165, 195, 56, 37, }; break;
                        case -39: level = 11; botItemID = 0; genes = new byte[] { 224, 238, 2, 27, 102, 10, 250, 125, 225, 252, }; break;
                        case -40: level = 12; botItemID = 110988; genes = new byte[] { 205, 45, 173, 101, 40, 78, 165, 195, 56, 37, }; break;
                        case -41: level = 12; botItemID = 0; genes = new byte[] { 145, 129, 73, 79, 223, 110, 69, 225, 50, 177 }; break;
                        case -42: level = 12; botItemID = 0; genes = new byte[] { 75, 189, 32, 0, 161, 182, 202, 214, 66, 70, }; break;
                        case -43: level = 13; botItemID = 0; introText = "Hey hey hey"; genes = new byte[] { 145, 203, 122, 65, 201, 98, 29, 100, 247, 240 }; break;
                        case -44: level = 13; botItemID = 0; genes = new byte[] { 135, 51, 219, 37, 241, 111, 81, 148, 183, 245, }; break;
                        case -45: level = 13; botItemID = 0; genes = new byte[] { 21, 27, 0, 194, 231, 32, 19, 240, 72, 250, }; break;
                        case -46: level = 14; botItemID = 0; genes = new byte[] { 55, 246, 253, 29, 244, 91, 52, 229, 33, 242, }; break;
                        case -47: level = 14; botItemID = 0; introText = "My wife still doest not believe me"; genes = new byte[] { 235, 125, 252, 144, 205, 158, 37, 109, 95, 0, }; break;
                        case -48: level = 14; botItemID = 0; genes = new byte[] { 14, 14, 153, 133, 202, 193, 247, 77, 226, 24, }; break;
                        case -49: level = 15; botItemID = 0; introText = "Wasasasa wasa wasa"; genes = new byte[] { 97, 186, 117, 13, 47, 141, 188, 190, 231, 98, }; break;
                        case -50: level = 15; botItemID = 0; genes = new byte[] { 187, 85, 182, 157, 197, 58, 43, 171, 14, 148, }; break;
                        case -51: level = 15; botItemID = 0; genes = new byte[] { 61, 214, 97, 16, 173, 52, 55, 218, 218, 23, }; break;
                        case -52: level = 15; botItemID = 0; introText = "PM me for nachos"; genes = new byte[] { 21, 43, 3, 20, 205, 239, 157, 121, 148, 200, }; break;
                        case -53: level = 16; botItemID = 0; genes = new byte[] { 122, 126, 4, 86, 138, 161, 173, 188, 217, 9, }; break;
                        case -54: level = 16; botItemID = 0; genes = new byte[] { 31, 178, 25, 47, 197, 24, 91, 18, 36, 165, }; break;
                        case -55: level = 16; botItemID = 0; introText = "Cold nachos or hot nachos?"; genes = new byte[] { 236, 166, 41, 184, 74, 99, 53, 178, 237, 145, }; break;
                        case -56: level = 16; botItemID = 0; genes = new byte[] { 181, 62, 101, 177, 50, 199, 105, 21, 5, 215 }; break;
                        case -57: level = 16; botItemID = 0; introText = "Just get rekt man"; genes = new byte[] { 218, 98, 58, 113, 15, 35, 6, 184, 0, 52, }; break;
                        case -58: level = 16; botItemID = 0; genes = new byte[] { 218, 224, 182, 214, 13, 108, 167, 3, 114, 109, }; break;
                        case -59: level = 16; botItemID = 0; genes = new byte[] { 226, 50, 168, 123, 194, 11, 117, 193, 18, 5, }; break;
                        case -60: level = 16; botItemID = 0; genes = new byte[] { 25, 119, 165, 120, 137, 252, 108, 184, 63, 154, }; break;
                        case -61: level = 16; botItemID = 0; genes = new byte[] { 235, 82, 164, 247, 121, 136, 242, 77, 222, 251, }; break;
                        case -62: level = 16; botItemID = 0; genes = new byte[] { 163, 32, 214, 236, 118, 198, 228, 182, 98, 125 }; break;

                        default:
                            // todo remove this hack. implement for bot id = [63,99] ?
                            if (botID < 100)
                            {
                                level = 16; botItemID = 0; genes = new byte[] { 163, 32, 214, 236, 118, 198, 228, 182, 98, 125 }; break;
                            }
                            else
                            {
                                throw new ContractException("invalid bot");
                            }
                    }
                    break;
            }

            var bot = new NachoWrestler()
            {
                genes           = genes,
                experience      = Constants.EXPERIENCE_MAP[level],
                nickname        = "",
                score           = Constants.DEFAULT_SCORE,
                location        = WrestlerLocation.None,
                itemID          = botItemID,
                comments        = new string[Constants.LUCHADOR_COMMENT_MAX],
                moveOverrides   = new byte[Constants.MOVE_OVERRIDE_COUNT],                
            };

            bot.comments[Constants.LUCHADOR_COMMENT_INTRO] = introText;

            return bot;
        }
    }
}
