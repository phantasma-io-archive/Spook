using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace Nacho.Men.Core.Gameplay
{
    public enum EWRESTLER_NAME_ERRORS
    {
        NONE,
        EMPTY_NAME,
        NAME_TOO_SHORT,
        NAME_TOO_BIG,
        INVALID_CHARACTERS,
        FORBIDDEN_NAME
    }

    public class NameGenerator
    {
        public static readonly Dictionary<EWRESTLER_NAME_ERRORS, string> WRESTLER_NAME_ERRORS_MESSAGES = new Dictionary<EWRESTLER_NAME_ERRORS, string>
        {
            { EWRESTLER_NAME_ERRORS.NONE,               "Valid" },
            { EWRESTLER_NAME_ERRORS.EMPTY_NAME,         "Sorry, the name must not be empty."},
            { EWRESTLER_NAME_ERRORS.NAME_TOO_SHORT,     "Sorry, the name must have at least " + MIN_NAME_CHAR_LENGTH + " characters."},
            { EWRESTLER_NAME_ERRORS.NAME_TOO_BIG,       "Sorry, the name can only have up to " + MAX_NAME_CHAR_LENGTH + " characters." },
            { EWRESTLER_NAME_ERRORS.INVALID_CHARACTERS, "Sorry, names cannot contain numbers." },
            { EWRESTLER_NAME_ERRORS.FORBIDDEN_NAME,     "Sorry, you have entered a forbidden name." }
        };

        public static readonly string[] namesMale = new string[] {
            "Bane", "Barrage", "Beast", "Birdman", "Bizarre", "Blade", "Blitz", "Blueprint", "Bombardment",
            "Boomboom", "Boulderfist", "Boycott", "Brawn", "Bullet", "Buster", "Canine", "Carnage", "Ceasar",
            "Chaos", "Classified", "Clobber", "Crazy Eyes", "Cyclone", "Daemon", "Deluge", "Diablo", "Diesel",
            "Digger", "Domino", "Drake", "Dread", "Dynamite", "Earthquake", "Extinction", "Fatal Fury", "Fear",
            "Gargoyle", "Genie", "Ghost", "Ghost Kicker", "Grand Master", "Grimace", "Grimes", "Guts", "Havoc",
            "Hazard", "Heckler", "Hellfire", "Hellhound", "Hercules", "Hunter", "Ironman", "Jekyll", "Jericho",
            "Jester", "Jitters", "Karma", "King", "Knocker", "Kong", "Macho Monster", "Magician", "Mayhem",
            "Mercury", "Mirage", "Morpheus", "Destructor", "Mutiny", "Nightmare", "Onyx", "Paragon",
            "Payne", "Pegasus", "Perfection", "Pincher", "Pitbull", "Predator", "Price", "Prince", "Puppeteer",
            "Quicksilver", "Raptor", "Ravager", "Raze", "Razor", "Retribution", "Rhino", "Riot", "Rocket", "Rogue",
            "Rowdy", "Sandman", "Scarface", "Sergeant", "Slayer", "Smasher", "Smiley", "Smite", "Snake", "Sphinx",
            "Striker", "Suave", "Swagger", "Swine", "Taboo", "Tank", "The Ambassador", "The Anaconda", "The Archetype",
            "The Badger", "The Barbarian", "The Bodyguard", "Boulder", "The Brute", "The Bull", "Bulldozer",
            "Butcher", "The Clam", "The Clown", "Creature", "The Crippler", "The Crow",
            "Demolisher", "Destroyer", "Devourer", "The Duke", "The Edge", "The Fiend", "The Flash",
            "The Flea", "The Fluke", "The Gambler", "The Goon", "The Gorilla", "The Governor", "The Gun", "The Hallowed",
            "The Hippo", "The Hood", "The Hound", "The Hurricane", "The Hyena", "The Immortal", "The Innovator",
            "The Jackal", "The King", "The Legend", "The Maestro", "The Man", "The Marauder", "The Martyr",
            "The Messenger", "The Mongrel", "The Mountain", "The Oak", "The Ogre", "The Punisher", "The Punk",
            "The Pursuer", "Quake", "The Savage", "The Scout", "The Sorceror", "The Stalker", "The Storm",
            "The Strangler", "The Surgeon", "The Terror", "The Thunder", "The Tormentor", "The Torrent",
            "The Typhoon", "The Unforgiving", "The Viper", "The Void", "The Volcano", "The Wrath", "Thruster",
            "Thump", "Thunderbolt", "Thundercrack", "Tremor", "Twister", "Undertaker", "Ursus", "Whiz", "Wicked",
            "Wolf", "Wrath-hog", "Nacho", "Lobo Loco", "Corredor", "El Rojo", "Comando", "El Gigante", "Demonio",
            "Gigolo", "Mendigo", "Violento", "Macho Rose", "Sangriento", "Ridiculoso", "Chupacabra", "Matador Negro",
            "El Tio", "Fuerte", "La Muerte", "Gringo", "Burro", "Sangre Mio", "Vaca Loca", "Guerrero", "El Presidente",
            "Bandoneon", "Toro", "Mayor", "Vigilante", "El Coyote", "El Fuego", "Vampiro", "Gallo Negro", "Gallo Rojo",
            "El Vaquero", "Bestia", "Chimichanga", "Pantera", "Picante", "Caballo", "Piratero", "Burrito", "Bam Bam",
            "Mucoso", "Toxico", "El Pedo", "Mano de Fuego", "El Padre", "El Pollo", "Macaco", "Capitan", "El Gato",
            "Supremo", "Rosa Blanco", "El Guapo", "Amigo", "El Cubano", "Macho Rosa", "Juanito", "Pablo Peruda",
            "Perro Alto", "El Roboto", "El Nariz", "Huevo Blanco", "El Cerebro", "Segurito", "Fresa", "Chido", "Tacho",
            "Tinto", "Tombo", "Chamba", "Gallego", "Erasmo", "Abuelo", "Abraxas", "Eugenio", "Bartolo", "Gervasio",
            "Godofredo", "Honorato", "Paco", "Pancho", "Joselito", "Lazaro", "Lioncello", "Manolito", "Ulysses",
            "Xavier", "Yago", "Chorizo", "El Queso", "Dr.Taco", "Carne Asada", "Cabrito", "Guacamole", "Chilli", "Lobito",
            "Platano", "El Pepino", "Tomato", "Clandestino", "Mariachi", "Taco King", "Mr.Coca", "Torito", "El Sombrero",
            "Dusty", "Brick", "Joker", "Vince", "Victor", "Arnold", "Maynard", "Griffin", "Denzell", "Arsenio", "Thor",
            "Phoenix", "Gabriel", "Takio", "Zale", "Kano", "Edric", "Ryker", "Martin", "Napoleon", "Guevara",
            "Asoka", "Julius", "Dr.Eden",
            "Bomb","Adamantine","Adamantium","Artillery","Assassin","Asteroid","Atom Bomb","Atom Power","Avalanche",
            "Barbarian", "Ram Master","Bazooka","Bedrock","Behemoth","Berserker",
            "Big Boy","The Knife","Bigfoot","Blitzkrieg","Blood","Bloodshot",
            "Bolt","Bonecrusher","Booby Trap","Boogieman","Boom",
            "Bounty","Brash","Dr.Brawn","Brickman","Buckshot","Dr.Bull",
            "Calamity", "Mr.Cannon","Cannonball","Cataclysm","Cement",
            "Chutzpah","Colossus","Concrete","Crisscross","Deathblow","Detonation",
            "Detonator","Mr.Devil","Diamond","El Dino","Mr.Doom","Downpour","Dozer",
            "Explosion","Explosive","Fever","Dr.Fire","Firebolt","Fireworker","Mr.Fury","Flux","Furymon",
            "Fuse","Fusillade","Fusion","Giant","Grenade","Dr.Bomba","Jackhammer","Dr.Hammer","Martillo",
            "Hitman","Hooligan","Howitzer","Hurricane","Ignition","Ironhand",
            "Ironclad","Mr.Iron","The Juke","Jokeman", "The Hook","Leviathan","Lightning",
            "Mammoth","Maniac","Marauder","Marvel", "Merciless","Dr.Mercy","Metalfist","Meteor","Minotaur",
            "Miracle","Monsoon","The Mortal","Mortar", "Moonman", "Muleman",
            "Outburst", "Pain", "Painkiller", "Phantasm", "Phantom","Phenomenon", "Prodigy",
            "Rainmaker", "Rascal", "Razorblade","Samurai","Richochet", "Podengo",
            "Roar","Rumble","Salvo","Savage","Seism","Shadowstep","Shatterman","Potato",
            "The Fuse","Shotgun","Sidestep","Smackdown","Mr.Smash", "Smirk","Stallion","Steel",
            "Stonefist","Storm","Stunner","Surge","Swerve","Dr.TNT","Thunder", "Buttcrack",
            "Everstorm","Titan","Titanium","Mr.Torpedo","Torrent","Twinkleboy","Typhoon",
            "Unbreakable","Volley","Wallop","Warhead","El Kongo",
             "Acid", "Antman", "Arturo", "The Aspect", "Auro", "Bait", "Bandage", "Bargain", "Basis", "The Bat",
            "Beam", "Mr.Bee", "Beetle", "Bold", "Bone", "Bookworm", "Bootfoot", "Dog Eater",
            "Brass", "The Bribe", "Bright", "Brush", "Cable", "Mr.Ape", "Cash", "Cell", "Chain", "Chalk",
            "Chart", "Checkmate", "Tick", "Clocker", "Cloud", "Coil", "Complexo", "Crash", "Craven",
            "Cross", "Cycle", "Dare", "Designer", "Dishmaker", "Disk", "Double", "Dynamo", "Edge",
            "Mr.Ego", "Elite", "Eternity", "Fearless", "Feedback", "Fluke", "Friction", "Frost",
            "Gamer", "Dr.Gear", "Gene", "Glove", "Grave", "Grim", "Habit", "Hack", "Heat", "Hide",
            "Hollow", "Mr.Hook", "Iceman", "Impulse", "Inkman", "Jumbo", "Junior", "Dr.Law", "Light", "Link", "Lock",
            "Luck", "Mathwiz", "Mellow", "Memory", "Mirror", "Mouse", "Nemo", "Night", "Nightowl", "Nimble",
            "Noise", "Note", "Nova", "Number", "Omen", "Owlman", "Panther", "Parcel", "Pathfinder", "Patriot",
            "Phase", "Piece", "Pitch", "Poison", "Prime", "Print", "Quote", "Ranger", "Rebel", "Requiem",
            "Riddle", "Risk", "Route", "Sable", "Scene", "Score", "Session", "Shade", "Shallow", "Shift",
            "Shiny", "Signal", "Silver", "Slice", "Slide", "Spark", "Spring", "Status", "Stitch", "Stranger",
            "Stretch", "Survey", "Switch", "Thrill", "Trick", "Tune", "Unit", "Venom", "Virus", "Ward",
            "Dr.Zen", "Zero", "Zigzag", "Zone", "Vulture", "Troll", "Gipsy", "Rebozuelo", "Goblin", "Zombie",
            "El Lambo", "Terminator", "Sailor", "Cyclone", "Pretzel", "Mamba", "Bambam", "Biggie",
            "Dealer", "Captain", "Fuzzy", "Shark", "Bingo", "Dimple", "Hound", "Diamond", "Mad Dog",
            "Goose", "Jackal", "Gonzo", "Blackjack", "Serpent", "Hawk", "Rogue", "Monk",
            "Bullseye", "Dragon", "Dhampir", "Chopula", "Egomaniac", "Bigshot", "Spike",
            "Kobra", "Freak", "Braveheart", "Cryogen", "Lion", "Elephant", "Hippo", "El Rhino",
            "Admiral", "Zorro", "The Beard", "Black Hole", "Silence", "The Uncle", "Quasar",
            "The Scourge", "Elder Rat", "Fatman", "Machette", "El Mexicano", "Eyepatch", "The Suit",
            "Tarzan", "The Enforcer", "Humpback", "Revolver", "Riffleman", "Wonderboy",
            "Mad Eye", "Mr.Grin", "Dentist", "The Brain", "Caesar", "Oddfather", "Giacomo", "Lello",
            "Adalfredo", "Abel", "Bandana", "Fat Bat", "Xander", "Ignazio", "The Banker",
            "Greybeard", "Sonic", "Knuckles", "The Boss", "Cornelius", "Cirano", "Vodingo",
            "Cowboy", "Shaggy Blue", "The Fork", "Umbrello", "Old Jew", "Vomit", "Ogre", "Sandoza",
            "Ratonzo", "Panther", "Scorpio", "Satyr", "Ass Bass", "Pinecone", "Mushroom",
            "The Shrimp", "Telephone", "Garlic", "Hongo Kongo", "Black Elf", "The Lobster", "Wolfheart",
            "Compressor", "Dr.Muscle", "Toothpaste", "Candyman", "Evil Beggar", "Bumfuzzle", "Gubbins",
            "Lollygag", "Raccoon", "Pizzicato", "Jellyman", "Fry", "Hobson", "Shepherd", "Bacchus",
            "Claude", "Wilson", "Raziel", "Jeremias", "Alfredo", "Jesus", "Facundo", "Roberto", "Juan",
            "Pascual", "Axel", "Bautista", "Alonzo",
        };

        public static readonly string[] namesFemale = new string[] {
            "Anemone", "Angel", "Aries", "Aura", "Black Widow", "Blitz", "Blue Rose",
            "Camille", "Caramel", "Celeste", "Chance", "Charity", "Coral", "Curtains",
            "Desire", "Destiny", "Diamond", "Divine", "Drama", "Elastic", "Electric",
            "Enigma", "Essence", "Eternity", "Feline", "Fortuna", "Fortune", "Gale",
            "The Gem", "Gemini", "Ginger", "Gloria", "Harpy", "Hope", "Iconiq", "Ivory",
            "The Ivy", "Jade", "Jasmine", "Jewel", "Karma", "Katana", "Kayo", "Masterlock",
            "Knock Out", "Libra", "Lights", "Lioness", "Luna", "Maneater", "Mantis",
            "Mantra", "Melody", "Miss Fortune", "Missy", "Mistral", "Moxie", "Mystique",
            "Nourisha", "Obsession", "Onyxia", "Onyxis", "Phobia", "Promise", "Raven", "Remedy",
            "Rogue", "Rose", "Ruby", "Saffron", "Sanguine", "Sapphire", "Scarlet", "Serenity",
            "Succubus", "Tazz", "Tempest", "The Amazon", "The Cat", "The Oracle", "The Smile",
            "The Witch", "Tigress", "Twinkle", "Vanity", "Velvet", "Venus", "Violette", "Virus",
            "Wildflower", "Willow", "La Cobra", "Dragon Rosa", "La Cabra", "La Coliflor",
            "La Nina", "Princesa", "Rabina", "Kinki", "Orgasm", "Abuela", "Enchilada", "Fajita",
            "Tortilla", "Salsa", "Sopa", "Tequilla", "Manzana", "Cherry", "Oliva", "Granada",
            "Wish", "Razorblade", "Lady","Troll", "Queen", "Shopper",
            "Fartura", "Beauty", "Aurora", "Bait", "Bondage", "Bargain", "Basis", "Breast",
            "Beam", "Beebee", "Beetle", "Bold", "Bone", "Belladonna", "Bookworm", "Catlady",
            "Brass", "Bright", "Brush", "Mrs.Ape", "Cash", "Cell", "Chain", "Chalk", "Crayon",
            "Checkmate", "Clocky", "Cloud", "Coil", "Complexa", "Crash", "Vulture",
            "Crossy", "Bicycle", "Dare", "Fiesta", "Washer", "Circle", "Cicada", "Dynamite", "Edge",
            "Cook", "Elite", "The Fear", "Feedback", "Fluke", "Friction", "Frost",
            "Gamergirl", "Red Gear", "Geneva", "Glove", "Panty", "Garterbelt", "Grave", "Grim",
            "Habit", "Hack", "Heart", "Hollow", "Impulse", "Inked", "Jumbo", "Junior", "Dr.Law", "Light", "Link", "Lock",
            "Luck", "Mathwiz", "Mellow", "Memory", "Mirror", "Rata", "Empress", "Night", "Dusk", "Nimble",
            "Noise", "Note", "Nova", "Number", "Omen", "Violin", "Panther", "Cello", "Flute", "Patriot",
            "Phase", "Puzzle", "Pitch", "Toxic", "Poison", "Futura", "Quote", "The Bow", "Rebel",
            "Minuet", "The Aria", "Route", "Witch", "Round", "Spank", "Shade", "Shallow", "Lace",
            "Mistress", "Silver", "Slice", "Boots", "Spark", "Spring", "Winter", "Stitch", "Stranger",
            "Stretch", "Survey", "Switch", "Thrill", "Trick", "Tune", "Unit",   "Autumn", "Zero", "Zigzag",
            "Summer", "Lucky", "Cookie", "Serpent", "Peanut", "Rogue", "Mistletoe", "Tigress", "Gigi",
            "Sugarbolt", "Juliet", "Xena", "Hally", "Consuela", "Nina", "Luciana", "Milena",
            "Lola", "Monika", "Esmeralda"
        };

        public const int MIN_NAME_CHAR_LENGTH = 4;
        public const int MAX_NAME_CHAR_LENGTH = 15;

        public static uint CalculateDNA(byte[] genes)
        {
            if (genes == null || genes.Length < 3)
            {
                return 0;
            }
            return (uint)(genes[0] + genes[1] * 0xFF + genes[2] * 0xFFFF + genes[1]);
        }

        public static string GenerateCommonName(byte[] genes, bool isFemale)
        {
            var n = CalculateDNA(genes);
            if (isFemale)
                return namesFemale[(uint)(n % namesFemale.Length)];
            else
                return namesMale[(uint)(n % namesMale.Length)];
        }

        public static string[] GetLegendaryNames(int ID)
        {
            switch (ID)
            {
                case 0: return new string[] { "Trumper", "The Donald", "Americano", "Deal" };
                case 1: return new string[] { "Bitalik", "Vuterin", "Skelleton" };
                case 2: return new string[] { "Redsec", "The Fernando", "Cyberman" };
                case 3: return new string[] { "Heyhey", "The Matos", "The Connect" };
                case 4: return new string[] { "Zhao", "Chang Peng", "Binancer" };
                case 5: return new string[] { "Antsharer", "The Hong", "FeiFei" };
                case 6: return new string[] { "Steemer", "Darimer", "Dr.EOS" };
                case 7: return new string[] { "The Dumper", "Igor", "Bognoff" };
                case 8: return new string[] { "The Pumper", "Grichka", "The Bogg" };
                case 9: return new string[] { "Balina", "Tokometric", "Made Man" };
                case 10: return new string[] { "John Shill", "Unhackable", "Antivirus" };
                case 11: return new string[] { "Bustin", "The Sun", "Tronmaster" };
                case 12: return new string[] { "Malcolm", "Lerider" };
                case 13: return new string[] { "Neeraj", "Murarka" };
                case 14: return new string[] { "Satoshi", "Nakamoto", "Bitfather", "Doriano" };
                case 15: return new string[] { "The Serge", "Nazarovo", "Chainer", "Linkmaster" };
                case 16: return new string[] { "Sminem", "Astoroid", "Crypto King" };
                case 17: return new string[] { "Ettiene", "Tigre" };
                case 18: return new string[] { "Buffete", "Warren" };
                case 19: return new string[] { "Lee Kai" };
                case 20: return new string[] { "El Rafa" };
                case 21: return new string[] { "Bernard" };
                case 22: return new string[] { "El Bruno" };
                case 23: return new string[] { "Mr.Alex" };
                case 24: return new string[] { "Mr.Miguel" };
                case 25: return new string[] { "Mr.Sergio" };
                case 26: return new string[] { "Flash Gordon" };
                case 27: return new string[] { "Appleman", "Steve Hobo" };
                case 28: return new string[] { "Don Puerta", "Moneyman" };
                case 29: return new string[] { "Ironmask", "Putovisky" };
                case 30: return new string[] { "Zhanger", "Neo Jedi" };
                case 31: return new string[] { "Canesino", "Zionist" };
                case 32: return new string[] { "Suppo", "Mansoup" };
                case 33: return new string[] { "Abraham", "Lumberjack" };
                case 34: return new string[] { "Borato" };
                case 35: return new string[] { "Donaldo", "Dr. Nueve", "El Comandante" };
                case 36: return new string[] { "Chart Guy" };
                case 37: return new string[] { "Chris-Dunn" };
                case 38: return new string[] { "Boxminer" };
                case 39: return new string[] { "CryptO" };
                case 40: return new string[] { "Crypto Bobby" };
                case 41: return new string[] { "Crypto Bud" };
                case 42: return new string[] { "Crypto-Daily" };
                case 43: return new string[] { "Like-Martin" };
                case 44: return new string[] { "Data-Dash" };
                case 45: return new string[] { "David-Hay" };
                case 46: return new string[] { "Doug-Polk" };
                case 47: return new string[] { "Like-Martin" };
                case 48: return new string[] { "Victoria" };
                case 49: return new string[] { "Tone-Vays" };
                case 50: return new string[] { "Bandhi" };
                case 51: return new string[] { "Yaya" };
                case 52: return new string[] { "Zao Zao" };
                case 53: return new string[] { "Lammao" };
                case 54: return new string[] { "Cage", "Thomas" };
                case 55: return new string[] { "Pop King", "Jackal", "Thriller" };
                case 56: return new string[] { "Nini" };
                case 57: return new string[] { "Baraka", "Obamacares" };
                case 58: return new string[] { "Peterlin" };
                case 59: return new string[] { "Einstein", "Onestone" };
                case 60: return new string[] { "Rock King", "Dalvis" };
                case 61: return new string[] { "Binkboss", "The Twin" };
                case 62: return new string[] { "Wanderbots" };
                case 63: return new string[] { "IGP" };
                case 64: return new string[] { "Rami", "Ismail" };
                case 65: return new string[] { "TobyGames" };
                case 66: return new string[] { "FilmTheory" };
                case 67: return new string[] { "Ali-A" };
                case 68: return new string[] { "Cepticeye" };
                case 69: return new string[] { "DanTDM" };
                case 70: return new string[] { "NinjaPlays" };
                case 71: return new string[] { "Markiplier" };
                case 72: return new string[] { "PewPew" };
                case 73: return new string[] { "Alan Fong" };
                case 74: return new string[] { "The Craig", "Faketoshi" };
                case 75: return new string[] { "Gavin", "Andresen" };
                case 76: return new string[] { "Newton" };
                case 77: return new string[] { "El Joe" };
                case 78: return new string[] { "Lennon" };
                case 79: return new string[] { "Bessi" };
                case 80: return new string[] { "Bitjesus", "B-Cash" };
                case 81: return new string[] { "Zuckerberg", "The Berg" };
                case 82: return new string[] { "Chaplin" };

                default: return null;
            }
        }

        public static string GenerateLegendaryName(byte[] genes, int variation)
        {
            var n = CalculateDNA(genes);

            string[] names = GetLegendaryNames(variation);
            if (names == null)
            {
                return GenerateCommonName(genes, false);
            }

            return names[(uint)(n % names.Length)];
        }

        private static string[] bannedPatterns = new string[]
        {
            "ASSHOLE",
            "BASTARD",
            "*BITCH",
            "BLOWJOB",
            "BOLLOCKS",
            "COCKSUCK",
            "CUMSHOT",
            "*CUNT*",
            "DILDO",
            "*FAG*",
            "*FUCK*",
            "*HOMO*",
            "JESUSSUCKS",
            "MAST?RBATION",
            "MILF",
            "*NIGGA*",
            "*NIGGER*",
            "PAEDO*",
            "PEDO*",
            "*PENIS",
            "*PIMP*",
            "PUSSY",
            "RIMJOB",
            "SEX",
            "SHIT*",
            "*SLUT*",
            "SPASTIC",
            "TWAT",
            "VULVA",
            "WANK",
            "XXX",
            "9*11",
            "ARSE",
            "BASTARD",
            "BITCH",
            "B1TCH",
            "CHINK",
            "CREAMPIE",
            "DAGO",
            "*DICK*",
            "*D1CK*",
            "DIRTY",
            "DOUCHE",
            "DUNG",
            "DYKE",
            "FCK",
            "FECES",
            "HOOTERS",
            "HUMP",
            "JIGAB",
            "LESBIAN",
            "*LESBO*",
            "MORNINGWOOD",
            "*PISS*",
            "PUSSIES",
            "*PUSSY*",
            "RECTUM",
            "RETARD",
            "SACRAMENT",
            "*SPIC*",
            "STUPID",
            "TWAT",
            "VAGIN*",
            "*WEED*",
            "WHOR*",
            "GLAND",
            "*FICK*",
            "*F1CK*",
            "F!CK",
            "FOTZE",
            "GEIL",
            "HITLER",
            "HOLOCAUST",
            "HURENSOHN",
            "KACKBRATZE",
            "MISSGEBURT",
            "MUSCHI",
            "*NAZI*",
            "*NEGER*",
            "NUTTE",
            "SCHEISS",
            "SCHLAMPE",
            "SCHWUCHTEL",
            "SIEGHEIL",
            "STRICHER",
            "TITTEN",
            "VERDAMMT",
            "WIXER",
            "ABORTUS",
            "ACHTERLIJK",
            "KANKER",
            "LULHANNES",
            "*NEGER*",
            "BOLLERA",
            "*CABRON*",
            "CAPULL?",
            "CHOCHO",
            "COJONES",
            "FOLLAR",
            "*JODER*",
        };

        private static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).
            Replace("\\*", ".*").
            Replace("\\?", ".") + "$";
        }


        private static Regex[] nameFilters = null;

        public static EWRESTLER_NAME_ERRORS ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return EWRESTLER_NAME_ERRORS.EMPTY_NAME;

            if (name.Length < MIN_NAME_CHAR_LENGTH) return EWRESTLER_NAME_ERRORS.NAME_TOO_SHORT;

            if (name.Length > MAX_NAME_CHAR_LENGTH) return EWRESTLER_NAME_ERRORS.NAME_TOO_BIG;

            var bytes = Encoding.UTF8.GetBytes(name);
            var index = 0;

            while (index < bytes.Length)
            {
                var prev = index > 0 ? bytes[index - 1] : (byte)0;
                var c = bytes[index];
                index++;

                // can contain spaces but cant' start or end with a space and cant have a space followed by another space
                if (c == 32 && index > 0 && index < bytes.Length - 1 && prev != 32) continue;

                // lowercase letters
                if (c >= 97 && c <= 122) continue;

                // uppercase letters
                if (c >= 65 && c <= 90) continue;

                // hyphen
                if (c == 45) continue;

                // dot
                if (c == 46) continue;

                return EWRESTLER_NAME_ERRORS.INVALID_CHARACTERS;
            }

            if (IsBanned(name)) return EWRESTLER_NAME_ERRORS.FORBIDDEN_NAME;

            return EWRESTLER_NAME_ERRORS.NONE;
        }

        public static bool IsBanned(string name)
        {
            if (nameFilters == null)
            {
                nameFilters = new Regex[bannedPatterns.Length];
                for (int i = 0; i < nameFilters.Length; i++)
                {
                    var pattern = WildcardToRegex(bannedPatterns[i]);
                    nameFilters[i] = new Regex(pattern);
                }
            }

            name = name.ToUpperInvariant();

            foreach (var filter in nameFilters)
            {
                if (filter.IsMatch(name)) return true;
            }

            return false;
        }
    }
}
