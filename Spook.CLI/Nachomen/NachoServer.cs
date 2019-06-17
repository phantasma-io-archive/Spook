using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Utils;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM.Utils;

namespace Phantasma.Spook.Nachomen
{
    class NachoServer
    {
        private static Nexus _nexus;
        private static ChainSimulator _chainSimulator;
        private static Chain _nachoChain;
        private static KeyPair _ownerKeys;
        private static Logger _logger;

        private static int _legendaryWrestlerDelay = 0;
        private static int _legendaryItemDelay = 0;

        private static Dictionary<Rarity, Queue<BigInteger>> _itemQueue = new Dictionary<Rarity, Queue<BigInteger>>();
        private static Dictionary<Rarity, Queue<NachoWrestler>> _wrestlerQueue = new Dictionary<Rarity, Queue<NachoWrestler>>();

        private static Random _rnd = new Random();

        private static BigInteger _lastItemID;

        public static void InitNachoServer(Nexus nexus, ChainSimulator chainSimulator, KeyPair ownerKeys, Logger logger)
        {
            _nexus = nexus;
            _chainSimulator = chainSimulator;
            _nachoChain = chainSimulator.Nexus.FindChainByName("nacho");
            _ownerKeys = ownerKeys;

            _logger = logger;

            GenerateTokens();

            GenerateBotGenes();

            //InitialNachoFill();

            FillNachoMarket();
        }

        public static void GenerateTokens()
        {
            var nachoAddress = Address.FromText("PGasVpbFYdu7qERihCsR22nTDQp1JwVAjfuJ38T8NtrCB");
            //var nachoAddress2   = Address.FromText("P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr");

            var nachoFuel = UnitConversion.ToBigInteger(5, Nexus.FuelTokenDecimals);
            var nachoChain = _nexus.FindChainByName("nacho");

            _chainSimulator.BeginBlock();
            _chainSimulator.GenerateSideChainSend(_ownerKeys, Nexus.FuelTokenSymbol, _nexus.RootChain, _ownerKeys.Address, nachoChain, nachoFuel, 0);
            _chainSimulator.GenerateSideChainSend(_ownerKeys, Nexus.FuelTokenSymbol, _nexus.RootChain, nachoAddress, nachoChain, nachoFuel, 9999);

            //_chainSimulator.GenerateSideChainSend(_ownerKeys, Nexus.FuelTokenSymbol, _nexus.RootChain, nachoAddress2, nachoChain, nachoFuel, 9999);
            var blockA = _chainSimulator.EndBlock().First();

            _chainSimulator.BeginBlock();
            _chainSimulator.GenerateSideChainSettlement(_ownerKeys, _nexus.RootChain, nachoChain, blockA.Hash);
            _chainSimulator.EndBlock();

            _chainSimulator.BeginBlock();
            _chainSimulator.GenerateAppRegistration(_ownerKeys, "nachomen", "https://nacho.men", "Collect, train and battle against other players in Nacho Men!");

            var nachoSupply = UnitConversion.ToBigInteger(10000, Constants.NACHO_TOKEN_DECIMALS);
            _chainSimulator.GenerateToken(_ownerKeys, Constants.NACHO_SYMBOL, "Nachomen Token", nachoSupply, Constants.NACHO_TOKEN_DECIMALS, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible);
            _chainSimulator.MintTokens(_ownerKeys, Constants.NACHO_SYMBOL, nachoSupply);

            _chainSimulator.GenerateToken(_ownerKeys, Constants.WRESTLER_SYMBOL, "Nachomen Luchador", 0, 0, TokenFlags.Transferable);
            _chainSimulator.GenerateToken(_ownerKeys, Constants.ITEM_SYMBOL, "Nachomen Item", 0, 0, TokenFlags.Transferable);
            _chainSimulator.EndBlock();

            _chainSimulator.BeginBlock();
            _chainSimulator.GenerateSideChainSend(_ownerKeys, Constants.NACHO_SYMBOL, _nexus.RootChain, nachoAddress, nachoChain, UnitConversion.ToBigInteger(1000, Constants.NACHO_TOKEN_DECIMALS), 1);
            //_chainSimulator.GenerateSideChainSend(_ownerKeys, Constants.NACHO_SYMBOL, _nexus.RootChain, nachoAddress2, nachoChain, UnitConversion.ToBigInteger(1000, 10), 1);
            var blockB = _chainSimulator.EndBlock().First();

            _chainSimulator.BeginBlock();
            _chainSimulator.GenerateSideChainSettlement(_ownerKeys, _nexus.RootChain, nachoChain, blockB.Hash);
            _chainSimulator.EndBlock();

            _chainSimulator.BeginBlock();
            _chainSimulator.GenerateSetTokenMetadata(_ownerKeys, Constants.WRESTLER_SYMBOL, "details", "https://nacho.men/luchador/*");
            _chainSimulator.GenerateSetTokenMetadata(_ownerKeys, Constants.WRESTLER_SYMBOL, "viewer", "https://nacho.men/luchador/body/*");
            _chainSimulator.EndBlock();

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

        private static void FillNachoMarket()
        {
            Console.WriteLine("Filling initial market");

            var testUser        = KeyPair.Generate();

            Console.WriteLine("token owner: " + _ownerKeys.Address.Text + " | test user: " + testUser.Address.Text);

            var nachoUser       = KeyPair.FromWIF("L3ydJBTWrKwRLZ5PxpygUnxkeJ4gxGHUDs3d3bDZkLTnB6Bpga87"); // => PGasVpbFYdu7qERihCsR22nTDQp1JwVAjfuJ38T8NtrCB

            var luchadorCounts = new Dictionary<Rarity, int>
            {
                [Rarity.Common] = 65,
                [Rarity.Uncommon] = 25,
                [Rarity.Rare] = 9,
                [Rarity.Epic] = 1,
                [Rarity.Legendary] = 1
            };

            // Transfer Fuel Tokens to the test user address
            _chainSimulator.BeginBlock();
            _chainSimulator.GenerateTransfer(_ownerKeys, testUser.Address, _nexus.RootChain, Nexus.FuelTokenSymbol, 1000000);
            _chainSimulator.GenerateTransfer(_ownerKeys, nachoUser.Address, _nexus.RootChain, Nexus.FuelTokenSymbol, 1000000);
            _chainSimulator.EndBlock();

            // WRESTLERS

            _logger.Message("Filling the market with luchadores...");

            var auctions = (MarketAuction[])_chainSimulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
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

                // Transfer Fuel Tokens to the test user address
                _chainSimulator.BeginBlock();
                _chainSimulator.GenerateTransfer(_ownerKeys, testUser.Address, _nexus.RootChain, Nexus.FuelTokenSymbol, 1000000);
                _chainSimulator.EndBlock();

                for (var i = 1; i <= count; i++)
                {
                    var wrestler        = DequeueNachoWrestler(_ownerKeys, rarity);
                    var wrestlerBytes   = wrestler.Serialize();

                    var rand        = new Random();
                    var isWrapped   = rand.Next(0, 100) < 50; // TODO update logic for the lootboxes (1 wrestler lootbox = 1 wrapped wrestler)

                    var wrestlerToken = _chainSimulator.Nexus.GetTokenInfo(Constants.WRESTLER_SYMBOL);
                    Assert.IsTrue(_nexus.TokenExists(Constants.WRESTLER_SYMBOL), "Can't find the token symbol");

                    // verify nft presence on the user pre-mint
                    var ownerships      = new OwnershipSheet(Constants.WRESTLER_SYMBOL);
                    var ownedTokenList  = ownerships.Get(_nexus.RootChain.Storage, testUser.Address);
                    Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a Wrestler Token?");

                    // Mint a new Wrestler Token directly on the user
                    var tokenROM = wrestlerBytes;
                    var tokenRAM = new byte[0];

                    _chainSimulator.BeginBlock();
                    var mintTx = _chainSimulator.MintNonFungibleToken(_ownerKeys, testUser.Address, Constants.WRESTLER_SYMBOL, tokenROM, tokenRAM, 0);
                    var blockA = _chainSimulator.EndBlock().First();

                    var tokenID = BigInteger.Zero;

                    if (blockA != null)
                    {
                        Assert.IsTrue(mintTx != null);

                        var txEvents = blockA.GetEventsForTransaction(mintTx.Hash);
                        Assert.IsTrue(txEvents.Any(x => x.Kind == EventKind.TokenMint));

                        foreach (var evt in txEvents)
                        {
                            if (evt.Kind != EventKind.TokenMint) continue;

                            var eventData = evt.GetContent<TokenEventData>();

                            tokenID = eventData.value;
                        }
                    }

                    // verify nft presence on the user post-mint
                    ownedTokenList = ownerships.Get(_nexus.RootChain.Storage, testUser.Address);
                    Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
                    
                    //verify that the present nft is the same we actually tried to create
                    var nft = _nexus.GetNFT(Constants.WRESTLER_SYMBOL, tokenID);
                    Assert.IsTrue(nft.ROM.SequenceEqual(wrestlerBytes) || nft.RAM.SequenceEqual(wrestlerBytes), "And why is this NFT different than expected? Not the same data");

                    // verify nft presence on the receiver pre-transfer
                    ownedTokenList = ownerships.Get(_nachoChain.Storage, nachoUser.Address);
                    Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a Wrestler Token?");

                    // transfer that nft from sender to receiver
                    _chainSimulator.BeginBlock();
                    _chainSimulator.GenerateNftSidechainTransfer(testUser, nachoUser.Address, _nexus.RootChain, _nachoChain, Constants.WRESTLER_SYMBOL, tokenID);
                    var blockB = _chainSimulator.EndBlock().First();
                    
                    // finish the chain transfer
                    _chainSimulator.BeginBlock();
                    _chainSimulator.GenerateSideChainSettlement(nachoUser, _nexus.RootChain, _nachoChain, blockB.Hash);
                    Assert.IsTrue(_chainSimulator.EndBlock().Any());

                    // verify the sender no longer has it
                    ownedTokenList = ownerships.Get(_nexus.RootChain.Storage, testUser.Address);
                    Assert.IsTrue(!ownedTokenList.Any(), "How does the sender still have one?");

                    // verify nft presence on the receiver post-transfer
                    ownedTokenList = ownerships.Get(_nachoChain.Storage, nachoUser.Address);
                    Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

                    //verify that the transferred nft is the same we actually tried to create
                    tokenID = ownedTokenList.ElementAt(0);
                    nft     = _nexus.GetNFT(Constants.WRESTLER_SYMBOL, tokenID);
                    Assert.IsTrue(nft.ROM.SequenceEqual(wrestlerBytes) || nft.RAM.SequenceEqual(wrestlerBytes), "And why is this NFT different than expected? Not the same data");

                    // Create auction
                    decimal minPrice, maxPrice;

                    GetLuchadorPriceRange(rarity, out minPrice, out maxPrice);
                    var diff    = (int)(maxPrice - minPrice);
                    var price   = (int)(minPrice + _rnd.Next() % diff);

                    if (price < 0) price *= -1; // HACK

                    createdAuctions++;

                    Timestamp endWrestlerAuctionDate = _chainSimulator.CurrentTime + TimeSpan.FromDays(2);

                    _chainSimulator.BeginBlock();
                    _chainSimulator.GenerateCustomTransaction(nachoUser, _nachoChain, () =>
                        ScriptUtils.
                            BeginScript().
                            AllowGas(nachoUser.Address, Address.Null, 1, 9999).
                            CallContract("market", "SellToken", nachoUser.Address, wrestlerToken.Symbol, Nexus.FuelTokenSymbol, tokenID, price, endWrestlerAuctionDate).
                            SpendGas(nachoUser.Address).
                            EndScript()
                    );
                    _chainSimulator.EndBlock();
                }
            }

            auctions = (MarketAuction[])_nachoChain.InvokeContract("market", "GetAuctions");
            Assert.IsTrue(auctions.Length == createdAuctions + previousAuctionCount, "wrestler auction ids missing");

            // ITEMS

            var itemCounts = new Dictionary<Rarity, int>
            {
                [Rarity.Common]     = 16,
                [Rarity.Uncommon]   = 12,
                [Rarity.Rare]       = 8,
                [Rarity.Epic]       = 2,
                [Rarity.Legendary]  = 1
            };

            _logger.Message("Generating items for market...");

            foreach (var rarity in itemCounts.Keys)
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

                var count = itemCounts[rarity];

                for (var i = 1; i <= count; i++)
                {
                    var itemID      = DequeueItem(rarity);
                    var itemBytes   = itemID.Serialize();

                    var rand        = new Random();
                    var isWrapped   = rand.Next(0, 100) < 50; // TODO update logic for the lootboxes (1 item lootbox = 1 wrapped item)

                    var itemToken = _chainSimulator.Nexus.GetTokenInfo(Constants.ITEM_SYMBOL);
                    Assert.IsTrue(_nexus.TokenExists(Constants.ITEM_SYMBOL), "Can't find the token symbol");

                    // verify nft presence on the user pre-mint
                    var ownerships = new OwnershipSheet(Constants.ITEM_SYMBOL);
                    var ownedTokenList = ownerships.Get(_nachoChain.Storage, testUser.Address);
                    Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a Item Token?");

                    // Mint a new Item Token directly on the user
                    var tokenROM = itemBytes;
                    var tokenRAM = new byte[0];

                    _chainSimulator.BeginBlock();
                    var mintTx = _chainSimulator.MintNonFungibleToken(_ownerKeys, testUser.Address, Constants.ITEM_SYMBOL, tokenROM, tokenRAM, 0);
                    var blockA = _chainSimulator.EndBlock().First();

                    var tokenID = BigInteger.Zero;

                    if (blockA != null)
                    {
                        Assert.IsTrue(mintTx != null);

                        var txEvents = blockA.GetEventsForTransaction(mintTx.Hash);
                        Assert.IsTrue(txEvents.Any(x => x.Kind == EventKind.TokenMint));

                        foreach (var evt in txEvents)
                        {
                            if (evt.Kind != EventKind.TokenMint) continue;

                            var eventData = evt.GetContent<TokenEventData>();

                            tokenID = eventData.value;
                        }
                    }

                    // verify nft presence on the user post-mint
                    ownedTokenList = ownerships.Get(_nexus.RootChain.Storage, testUser.Address);
                    Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

                    //verify that the present nft is the same we actually tried to create
                    var nft = _nexus.GetNFT(Constants.ITEM_SYMBOL, tokenID);
                    Assert.IsTrue(nft.ROM.SequenceEqual(itemBytes) || nft.RAM.SequenceEqual(itemBytes), "And why is this NFT different than expected? Not the same data");

                    // verify nft presence on the receiver pre-transfer
                    ownedTokenList = ownerships.Get(_nachoChain.Storage, nachoUser.Address);
                    Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a Wrestler Token?");

                    // transfer that nft from sender to receiver
                    _chainSimulator.BeginBlock();
                    _chainSimulator.GenerateNftSidechainTransfer(testUser, nachoUser.Address, _nexus.RootChain, _nachoChain, Constants.ITEM_SYMBOL, tokenID);
                    var blockB = _chainSimulator.EndBlock().First();

                    // finish the chain transfer
                    _chainSimulator.BeginBlock();
                    _chainSimulator.GenerateSideChainSettlement(nachoUser, _nexus.RootChain, _nachoChain, blockB.Hash);
                    Assert.IsTrue(_chainSimulator.EndBlock().Any());

                    // verify the sender no longer has it
                    ownedTokenList = ownerships.Get(_nexus.RootChain.Storage, testUser.Address);
                    Assert.IsTrue(!ownedTokenList.Any(), "How does the sender still have one?");

                    // verify nft presence on the receiver post-transfer
                    ownedTokenList = ownerships.Get(_nachoChain.Storage, nachoUser.Address);
                    Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

                    //verify that the transferred nft is the same we actually tried to create
                    tokenID = ownedTokenList.ElementAt(0);
                    nft     = _nexus.GetNFT(Constants.ITEM_SYMBOL, tokenID);
                    Assert.IsTrue(nft.ROM.SequenceEqual(itemBytes) || nft.RAM.SequenceEqual(itemBytes), "And why is this NFT different than expected? Not the same data");

                    // Create auction
                    decimal minPrice, maxPrice;

                    GetItemPriceRange(rarity, out minPrice, out maxPrice);
                    var diff    = (int)(maxPrice - minPrice);
                    var price   = (int)(minPrice + _rnd.Next() % diff);

                    if (price < 0) price *= -1; // HACK

                    createdAuctions++;

                    Timestamp endItemAuctionDate = _chainSimulator.CurrentTime + TimeSpan.FromDays(2);

                    _chainSimulator.BeginBlock();
                    _chainSimulator.GenerateCustomTransaction(nachoUser, _nachoChain, () =>
                        ScriptUtils.
                            BeginScript().
                            AllowGas(nachoUser.Address, Address.Null, 1, 9999).
                            CallContract("market", "SellToken", nachoUser.Address, itemToken.Symbol, Nexus.FuelTokenSymbol, tokenID, price, endItemAuctionDate).
                            SpendGas(nachoUser.Address).
                            EndScript()
                    );
                    _chainSimulator.EndBlock();
                }
            }

            auctions = (MarketAuction[])_nachoChain.InvokeContract("market", "GetAuctions");
            Assert.IsTrue(auctions.Length == createdAuctions + previousAuctionCount, "items auction ids missing");

            _logger.Success("Nacho Market is ready!");
        }

        private static void GenerateBotGenes()
        {
            return;

            Console.WriteLine("Generate genes for bots");

            var rnd = new System.Random();

            for (int n = 1; n <= 8; n++)
            {
                var level = (PraticeLevel)n;

                /*HashSet<WrestlingMove> wantedMoves;

                switch (level)
                {
                    case PraticeLevel.Wood: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Bash }); break;
                    case PraticeLevel.Iron: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Block }); break;
                    case PraticeLevel.Steel: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Corkscrew }); break;
                    case PraticeLevel.Silver: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Bulk }); break;
                    case PraticeLevel.Gold: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Chicken_Wing}); break;
                    case PraticeLevel.Ruby: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Refresh }); break;
                    case PraticeLevel.Emerald: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Rhino_Charge }); break;
                    case PraticeLevel.Diamond: wantedMoves = new HashSet<WrestlingMove>(new WrestlingMove[] { WrestlingMove.Razor_Jab }); break;
                    default: wantedMoves = new HashSet<WrestlingMove>(validMoves); break;
                }*/

                //level = PraticeLevel.Wood;
                Console.WriteLine("Mining bot: " + level);

                //var genes = Luchador.MineBotGenes(rnd, level/*, wantedMoves*/);

                //for (int i = 0; i < genes.Length; i++)
                //{
                //    Console.Write(genes[i] + ", ");
                //}

                //var bb = Luchador.FromGenes(n, genes);
                //var temp = bb.data;
                //bb.data = temp;

                //Console.WriteLine();
                //Console.WriteLine(bb.Name);
                ////Console.WriteLine("Kind: " + bb.Rarity);
                ////Console.WriteLine("Head: " + bb.GetBodyPart(BodyPart.Head).Variation);
                //Console.WriteLine("Primary Move: " + bb.PrimaryMove);
                //Console.WriteLine("Secondary Move: " + bb.SecondaryMove);
                //Console.WriteLine("Support Move: " + bb.TertiaryMove);
                //Console.WriteLine("Stance Move: " + bb.StanceMove);
                //Console.WriteLine("Base STA: " + bb.BaseStamina);
                //Console.WriteLine("Base ATK: " + bb.BaseAttack);
                //Console.WriteLine("Base DEF: " + bb.BaseDefense);
                Console.WriteLine("------------------");
            }
        }

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

        private static void MineRandomItems(int amount)
        {
            while (amount > 0)
            {
                _lastItemID++;
                var obtained = Formulas.GetItemKind(_lastItemID);

                if (Rules.IsReleasedItem(obtained))
                {
                    var rarity = Rules.GetItemRarity(obtained);
                    EnqueueItem(_lastItemID, rarity);
                    amount--;
                }
            }
        }

        private static void EnqueueItem(BigInteger ID, Rarity rarity)
        {
            Queue<BigInteger> queue;

            if (_itemQueue.ContainsKey(rarity))
            {
                queue = _itemQueue[rarity];
            }
            else
            {
                queue = new Queue<BigInteger>();
                _itemQueue[rarity] = queue;
            }

            queue.Enqueue(ID);
        }

        private static BigInteger DequeueItem(Rarity rarity)
        {
            while (!_itemQueue.ContainsKey(rarity) || _itemQueue[rarity].Count == 0)
            {
                MineRandomItems(10);
            }

            return _itemQueue[rarity].Dequeue();
        }

        private static void MineRandomLuchadores(KeyPair ownerKey, int amount)
        {
            while (amount > 0)
            {
                var wrestler = new NachoWrestler()
                {
                    auctionID = 0,
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
                    owner = ownerKey.Address,
                    perfumeTime = 0,
                    praticeLevel = PraticeLevel.Gold,
                    roomTime = 0,
                    score = 0,
                    stakeAmount = 0,
                    trainingStat = StatKind.None,
                    ua1 = byte.MaxValue,
                    ua2 = byte.MaxValue,
                    ua3 = byte.MaxValue,
                    us1 = byte.MaxValue,
                    us2 = byte.MaxValue,
                    us3 = byte.MaxValue
                };

                var luchador = Luchador.FromData(1, wrestler);

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

        private static NachoWrestler DequeueNachoWrestler(KeyPair ownerKeys, Rarity rarity)
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

    }
}
