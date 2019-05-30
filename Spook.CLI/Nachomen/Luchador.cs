using Phantasma.Numerics;

using System;
using System.Collections.Generic;
using Phantasma.Cryptography;

using Phantasma.Blockchain.Contracts.Native;

namespace Phantasma.Spook.Nachomen
{
    public enum LuchadorDescriptionKind
    {
        Short,
        Normal,
        Full,
    }

    public struct LuchadorPiece
    {
        public byte Variation;
        public sbyte Shade;
        public Hue PrimaryHue;
        public Hue SecondaryHue;
        public Hue TertiaryHue;

        public LuchadorPiece(byte[] bytes, int index, int min, int max)
        {
            this.Variation = (byte)(min + (bytes[index + 0] % (1 + max - min))); // TODO 16 is the current max variation ID
            this.Shade = (sbyte)(-1 + (bytes[index + 1] % 4));
            this.PrimaryHue = (Hue)(bytes[index + 2] % 10);
            this.SecondaryHue = (Hue)(bytes[index + 3] % 10);
            this.TertiaryHue = (Hue)(bytes[index + 4] % 10);
        }
    }

    public class Luchador : ICollectable
    {
        public BigInteger ID { get; private set; }
        public Address Owner => data.owner;

        public bool IsTransferable => !data.flags.HasFlag(WrestlerFlags.Locked);

        private NachoWrestler _data;
        public NachoWrestler data
        {
            get
            {
                return _data;
            }

            set
            {
                _data = value;
                _elements.Clear();
            }
        }

        public void Copy(IIndexable other)
        {
            var otherLuchador = (Luchador)other;
            this.ID = otherLuchador.ID;
            this.data = otherLuchador.data;
        }

        public bool HasItem => data.itemID > 0;

        public WrestlingMove PrimaryMove => Rules.GetPrimaryMoveFromGenes(data.genes);
        public WrestlingMove SecondaryMove => Rules.GetSecondaryMoveFromGenes(data.genes);
        public WrestlingMove TertiaryMove => Rules.GetTertiaryMoveFromGenes(data.genes);
        public WrestlingMove StanceMove => Rules.GetStanceMoveFromGenes(data.genes);
        public WrestlingMove TagMove => Rules.GetTagMoveFromGenes(data.genes);

        private readonly Dictionary<BodyPart, LuchadorPiece> _elements = new Dictionary<BodyPart, LuchadorPiece>();

        private Luchador(BigInteger ID)
        {
            this.ID = ID;
        }

        public static Luchador FromID(BigInteger ID)
        {
            return new Luchador(ID);
        }

        public string GetTier()
        {
            return Rarity.ToString().ToLower();
        }

        public static byte[] MineBotGenes(Random rnd, PraticeLevel level)
        {
            Hue hue;
            sbyte shade;

            GetDummyVisuals(level, out hue, out shade);

            var headVariation = (byte)(level) - 1;

            byte targetStat = (byte)(Formulas.BaseStatSplit + ((byte)level) * 5);

            var isOdd = ((int)level) % 2 == 1;

            //var movesList = allowedMoves != null ? allowedMoves.ToArray() : null;

            return MineGenes(rnd, x =>
            {
                if (x.Rarity != Rarity.Bot)
                {
                    return false;
                }

                if (x.GetBodyPart(BodyPart.Head).Variation != headVariation)
                {
                    return false;
                }

                if (x.SkinHue != hue || x.SkinShade != shade)
                {
                    return false;
                }

                if (Math.Abs(x.BaseStamina - targetStat) > 1
                 || Math.Abs(x.BaseAttack - targetStat) > 10
                 || Math.Abs(x.BaseDefense - targetStat) > 10)
                {
                    return false;
                }

                if (x.BaseAttack < x.BaseStamina != isOdd)
                {
                    return false;
                }

                return true;
            },
            (genes) =>
            {
                genes[NachoConstants.GENE_RARITY] = (byte)((genes[NachoConstants.GENE_RARITY] / 6) * 6);
                genes[NachoConstants.GENE_STAMINA] = (byte)(targetStat - 5 + rnd.Next() % 10);
                genes[NachoConstants.GENE_ATK] = (byte)(targetStat - 5 + rnd.Next() % 10);
                genes[NachoConstants.GENE_DEF] = (byte)(targetStat - 5 + rnd.Next() % 10);
                genes[NachoConstants.GENE_COUNTRY] = (byte)Country.Mexico;

                /*if (movesList != null)
                {
                    var index = genes[GENE_MOVE] % movesList.Length;
                    var targetMove = movesList[index];
                    genes[GENE_MOVE] = (byte)((int)targetMove - (1 + (int)WrestlingMove.Custom));
                }*/
            }
            );
        }

        private byte[] _hash = null;

        private void GeneratePart(BodyPart part)
        {
            if (_hash == null)
            {
                _hash = data.genes.Sha256();
            }

            int index = (int)part;

            int max;

            switch (part)
            {
                case BodyPart.Head:
                    {
                        if (Rarity == Rarity.Legendary)
                        {
                            max = 128;
                        }
                        else
                        {
                            max = 64;
                        }

                        break;
                    }


                default:
                    max = 32;
                    break;
            }

            var element = new LuchadorPiece(_hash, index, part == BodyPart.Head ? 0 : 1, max);

            if (this.Rarity == Rarity.Bot)
            {
                if (part != BodyPart.Head)
                {
                    element.Variation = 0;
                }

                element.PrimaryHue = SkinHue;
                element.SecondaryHue = SkinHue;
                element.TertiaryHue = SkinHue;
                element.Shade = SkinShade;
            }

            _elements[part] = element;
            index = (index + 5) % (_hash.Length - 5);
        }

        public LuchadorPiece GetBodyPart(BodyPart part)
        {
            if (!_elements.ContainsKey(part))
            {
                GeneratePart(part);
            }

            return _elements[part];
        }

        public string Name
        {
            get
            {
                if (ID <= 8)
                {
                    return ((PraticeLevel)((int)ID)).ToString() + " dummy";
                }

                if (!string.IsNullOrEmpty(data.nickname))
                {
                    return data.nickname;
                }

                switch (this.Rarity)
                {
                    case Rarity.Legendary:
                        return NameGenerator.GenerateLegendaryName(this.data.genes, GetBodyPart(BodyPart.Head).Variation);

                    default:
                        {
                            var body = GetBodyPart(BodyPart.Body).Variation;
                            return NameGenerator.GenerateCommonName(this.data.genes, body > 20);
                        }
                }

            }
        }

        public bool HasMove(WrestlingMove move)
        {
            if (move == WrestlingMove.Unknown)
            {
                return false;
            }

            if (move == WrestlingMove.Idle || move == WrestlingMove.Counter || move == WrestlingMove.Smash)
            {
                return true;
            }

            if (Rules.IsPrimaryMove(move))
            {
                return move == this.PrimaryMove;
            }

            if (Rules.IsSecondaryMove(move))
            {
                return move == this.SecondaryMove;
            }

            if (Rules.IsTertiaryMove(move))
            {
                return move == this.TertiaryMove;
            }

            if (Rules.IsStanceMove(move))
            {
                return move == this.StanceMove;
            }

            if (Rules.IsTagMove(move))
            {
                return move == this.TagMove;
            }

            return false;
        }

        public bool HasMove(string moveName)
        {
            moveName = moveName.Replace(" ", "_");

            WrestlingMove move;

            if (Enum.TryParse(moveName, true, out move))
            {
                return HasMove(move);
            }

            return false;
        }

        public int Level => Formulas.CalculateWrestlerLevel((int)data.experience);
        public Rarity Rarity
        {
            get
            {
                if (data.genes == null)
                {
                    return Rarity.Common;
                }

                var n = (data.genes[NachoConstants.GENE_RARITY] % 6);
                return (Rarity)n;
            }
        }

        public LuchadorHoroscope HoroscopeSign => Formulas.GetHoroscopeSign(data.genes);

        public int BaseStamina => Formulas.CalculateBaseStat(data.genes, StatKind.Stamina);
        public int BaseAttack => Formulas.CalculateBaseStat(data.genes, StatKind.Attack);
        public int BaseDefense => Formulas.CalculateBaseStat(data.genes, StatKind.Defense);

        public int Stamina => (int)Formulas.CalculateWrestlerStat(this.Level, BaseStamina, data.gymBoostStamina);
        public int Attack => (int)Formulas.CalculateWrestlerStat(this.Level, BaseAttack, data.gymBoostAtk);
        public int Defense => (int)Formulas.CalculateWrestlerStat(this.Level, BaseDefense, data.gymBoostDef);

        public static byte[] MineGenes(Random rnd, Func<Luchador, bool> filter, Action<byte[]> geneSplicer = null, int limit = 0)
        {
            int amount = 0;
            var luchador = new Luchador(NachoConstants.BASE_LUCHADOR_ID);
            do
            {
                var temp = luchador.data;
                var genes = GenerateGenes(rnd);

                if (geneSplicer != null)
                {
                    geneSplicer(genes);
                }

                temp.genes = genes;

                luchador._hash = null;
                luchador.data = temp;

                if (luchador.HasValidMoveset() && (filter == null || filter(luchador)))
                {
                    return temp.genes;
                }

                amount++;
                if (limit > 0 && amount >= limit)
                {
                    return null;
                }
            } while (true);
        }

        private bool HasValidMoveset()
        {
            if (PrimaryMove == WrestlingMove.Unknown)
            {
                return false;
            }

            if (SecondaryMove == WrestlingMove.Unknown)
            {
                return false;
            }

            if (TertiaryMove == WrestlingMove.Unknown)
            {
                return false;
            }

            if (StanceMove == WrestlingMove.Unknown)
            {
                return false;
            }

            /*if (TagMove == WrestlingMove.Unknown)
            {
                return false;
            }*/

            return true;
        }

        public static Luchador FromGenes(BigInteger n, byte[] genes)
        {
            var luchador = new Luchador(n);
            var data = new NachoWrestler();
            data.genes = genes;
            luchador.data = data;
            return luchador;
        }

        public static Luchador FromData(BigInteger n, NachoWrestler data)
        {
            var luchador = new Luchador(n);
            luchador.data = data;
            return luchador;
        }


        public static byte[] MineGenes(Random rnd, Rarity rarity)
        {
            return MineGenes(rnd, x => x.Rarity == rarity);
        }

        public static byte[] GenerateGenes(Random rnd)
        {
            var genes = new byte[10];
            for (int i = 0; i < genes.Length; i++)
            {
                genes[i] = (byte)(rnd.Next() % 256);
            }
            return genes;
        }

        public static Dictionary<int, int> CalculateExperienceMap()
        {
            var map = new Dictionary<int, int>
            {
                [0] = 0,
                [1] = 0
            };

            int XP = 0;
            int curLevel = NachoConstants.MIN_LEVEL;
            while (curLevel < NachoConstants.MAX_LEVEL)
            {
                XP++;
                int oldLevel = curLevel;
                curLevel = Formulas.CalculateWrestlerLevel(XP);
                if (oldLevel != curLevel)
                {
                    map[curLevel] = XP;
                }
            }

            return map;
        }

        public static int GetExperienceAt(int level)
        {
            if (level > NachoConstants.MAX_LEVEL)
            {
                level = NachoConstants.MAX_LEVEL;
            }

            return NachoConstants.EXPERIENCE_MAP[level];
        }

        public PraticeLevel praticeLevel => data.praticeLevel;

        public Hue SkinHue
        {
            get
            {
                if (Rarity == Rarity.Bot)
                {
                    return (Hue)((data.genes[NachoConstants.GENE_SKIN] + data.genes[NachoConstants.GENE_RARITY]) % 10);
                }

                var pal = GetSkinPalette(this.Country);
                return pal[(data.genes[NachoConstants.GENE_SKIN] + data.genes[NachoConstants.GENE_RARITY]) % pal.Length];
            }
        }
        public sbyte SkinShade => (sbyte)(-1 + ((data.genes[NachoConstants.GENE_SKIN] + data.genes[NachoConstants.GENE_RANDOM]) % 4));

        public LeagueRank League => LeagueRank.None;

        public Country Country
        {
            get
            {
                var result = (Country)data.genes[NachoConstants.GENE_COUNTRY];
                if (IsInvalidCountry(result))
                {
                    return Country.United_States;
                }

                return result;
            }
        }

        public static readonly Hue[] MixedHues = new Hue[] {
            Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige,
            Hue.Brown, Hue.Brown, Hue.Brown, Hue.Brown,
            Hue.Pink, Hue.Pink,
            Hue.Red, Hue.Red,
            Hue.Purple,
            Hue.Gray };

        public static readonly Hue[] WesternEuropeanHues = new Hue[] {
            Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige,
            Hue.Brown, Hue.Brown, Hue.Brown,
            Hue.Pink, Hue.Pink,
            Hue.Red};

        public static readonly Hue[] EasternEuropeanHues = new Hue[] {
            Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige,
            Hue.Pink, Hue.Pink,
            Hue.Gray};

        public static readonly Hue[] GreenHues = new Hue[] {
            Hue.Green, Hue.Lime
        };

        public static readonly Hue[] SnowHues = new Hue[] {
            Hue.Beige, Hue.Gray
        };

        public static readonly Hue[] IceHues = new Hue[] {
            Hue.Cyan
        };

        public static readonly Hue[] AfricanHues = new Hue[] {
            Hue.Brown, Hue.Brown,Hue.Brown, Hue.Brown, Hue.Brown,
            Hue.Purple, Hue.Purple,
            Hue.Red
        };

        public static readonly Hue[] ArabicHues = new Hue[] {
            Hue.Beige, Hue.Beige, Hue.Beige,
            Hue.Brown, Hue.Brown,Hue.Brown,
            Hue.Red
        };

        public static readonly Hue[] SouthAmericanHues = new Hue[] {
            Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige,
            Hue.Brown, Hue.Brown,Hue.Brown,Hue.Brown,
            Hue.Red, Hue.Red,
            Hue.Pink, Hue.Pink
        };

        public static readonly Hue[] OceaniaHues = new Hue[] {
            Hue.Beige, Hue.Beige, Hue.Beige, Hue.Beige,
            Hue.Brown,
            Hue.Purple,
            Hue.Red
        };

        public static readonly Hue[] WesternAsianHues = new Hue[] {
            Hue.Brown, Hue.Brown,
            Hue.Purple,
            Hue.Beige,
        };

        public static readonly Hue[] EasternAsianHues = new Hue[] {
            Hue.Beige, Hue.Beige,Hue.Beige,Hue.Beige,Hue.Beige, Hue.Beige,
            Hue.Red, Hue.Red, Hue.Red,
            Hue.Pink, Hue.Pink,
        };

        private bool IsInvalidCountry(Country country)
        {
            var hues = GetSkinPalette(country);
            return hues == null;
            /*
            switch (country)
            {
                case Country.Anguilla:
                case Country.Antarctica:
                case Country.Antigua_and_Barbuda:
                case Country.Aruba:
                case Country.Barbados:
                case Country.Benin:
                case Country.Bermuda:
                case Country.Bouvet_Island:
                case Country.Bosnia_and_Herzegovina:
                case Country.Brunei:
                case Country.Bahrain:

                case Country.Central_African_Republic:
                case Country.Comoros:
                case Country.Cook_Islands:

                case Country.Dominica:
                case Country.Dominican_Republic:

                case Country.Equatorial_Guinea:
                case Country.Eritrea:
                case Country.El_Salvador:

                case Country.Faroe_Islands:
                case Country.Fiji:

                case Country.Gabon:
                case Country.Gambia:

                case Country.Grenada:
                case Country.Guadeloupe:
                case Country.Guam:
                case Country.Guernsey:

                case Country.Guinea:
                case Country.Guinea_Bissau:
                case Country.Guyana:
                case Country.Haiti:
                case Country.Isle_of_Man:

                case Country.Jersey:
                case Country.Jordan:
                case Country.Kiribati:

                case Country.Lebanon:
                case Country.Lesotho:
                case Country.Liberia:
                case Country.Malawi:


                case Country.Mali:
                case Country.Marshall_Islands:
                case Country.Martinique:
                case Country.Mauritania:
                case Country.Mauritius:
                case Country.Mayotte:
                case Country.Micronesia:

                case Country.Montserrat:
                case Country.Nauru:

                case Country.New_Caledonia:
                case Country.Niger:
                case Country.Niue:
                case Country.Norfolk_Island:

                case Country.Palau:
                case Country.Papua_New_Guinea:
                case Country.Pitcairn:
                case Country.Reunion:


                case Country.Rwanda:
                case Country.Somalia:
                case Country.San_Marino:
                case Country.Sao_Tome_and_Principe:

                case Country.Sierra_Leone:
                case Country.Solomon_Islands:
                case Country.South_Sudan:
                case Country.Suriname:
                case Country.Swaziland:

                case Country.Tanzania:

                case Country.Togo:
                case Country.Tokelau:
                case Country.Tonga:
                case Country.Trinidad_and_Tobago:

                case Country.Tuvalu:
                case Country.Vanuatu:
                case Country.Western_Sahara:

                case Country.Oman:
                case Country.Nicaragua:
                case Country.Samoa:
                case Country.Sudan:
                case Country.Syria:
                case Country.Timor_Leste:

                    return true;
                default: return false;
            }*/
        }

        public int GetBaseStat(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.Attack: return BaseAttack;
                case StatKind.Defense: return BaseDefense;
                case StatKind.Stamina: return BaseStamina;
                default: return 0;
            }
        }

        public int GetTrainStat(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.Attack: return data.gymBoostAtk;
                case StatKind.Defense: return data.gymBoostDef;
                case StatKind.Stamina: return data.gymBoostStamina;
                default: return 0;
            }
        }

        public int GetStat(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.Attack: return this.Attack;
                case StatKind.Defense: return this.Defense;
                case StatKind.Stamina: return this.Stamina;
                default: return 0;
            }
        }

        private Hue[] GetSkinPalette(Country country)
        {
            switch (country)
            {
                case Country.Albania:
                case Country.Andorra:
                case Country.Austria:
                case Country.Belgium:
                case Country.Bulgaria:
                case Country.Canada:
                case Country.Croatia:
                case Country.Cyprus:
                case Country.Czech_Republic:
                case Country.Denmark:
                case Country.Estonia:
                case Country.Finland:
                case Country.Germany:
                case Country.Greece:
                case Country.Vatican:
                case Country.Hungary:
                case Country.Ireland:
                case Country.Israel:
                case Country.Italy:
                case Country.Liechtenstein:
                case Country.Lithuania:
                case Country.Luxembourg:
                case Country.Montenegro:
                case Country.Netherlands:
                case Country.Norway:
                case Country.Portugal:
                case Country.Spain:
                case Country.Macedonia:
                case Country.Malta:
                case Country.Monaco:
                case Country.Moldova:
                case Country.Romania:
                case Country.Switzerland:
                    return WesternEuropeanHues;

                case Country.Iceland:
                    return SnowHues;

                case Country.Greenland:
                    return GreenHues;

                case Country.American_Samoa:

                case Country.Bahamas:
                case Country.Jamaica:
                case Country.Maldives:
                case Country.Philippines:
                case Country.Cayman_Islands:
                case Country.Seychelles:

                case Country.Afghanistan:
                case Country.Armenia:
                case Country.Belarus:
                case Country.Azerbaijan:
                case Country.Georgia:
                case Country.Iran:
                case Country.Iraq:
                case Country.Kazakhstan:
                case Country.Latvia:
                case Country.Poland:
                case Country.Serbia:
                case Country.Slovakia:
                case Country.Slovenia:
                case Country.Russia:
                case Country.Ukraine:
                case Country.Uzbekistan:
                case Country.Tajikistan:
                case Country.Turkmenistan:
                    return EasternEuropeanHues;

                case Country.Angola:
                case Country.Botswana:
                case Country.Burkina_Faso:
                case Country.Burundi:
                case Country.Cameroon:
                case Country.Cabo_Verde:
                case Country.Chad:
                case Country.Djibouti:
                case Country.Ethiopia:
                case Country.Ghana:
                case Country.Kenya:
                case Country.Madagascar:
                case Country.Mozambique:
                case Country.Senegal:
                case Country.South_Africa:
                case Country.Uganda:
                case Country.Zambia:
                case Country.Zimbabwe:
                case Country.Nigeria:
                case Country.Namibia:
                case Country.France:
                case Country.Sweden:
                    return AfricanHues;

                case Country.Algeria:
                case Country.Egypt:
                case Country.Gibraltar:
                case Country.Libya:
                case Country.Kuwait:
                case Country.Morocco:
                case Country.Saudi_Arabia:
                case Country.Qatar:
                case Country.Yemen:
                case Country.Tunisia:
                case Country.Turkey:
                case Country.United_Arab_Emirates:
                    return ArabicHues;

                case Country.Argentina:
                case Country.Brazil:
                case Country.Bolivia:
                case Country.Belize:
                case Country.Chile:
                case Country.Colombia:
                case Country.Congo:
                case Country.Costa_Rica:
                case Country.Cuba:
                case Country.Ecuador:
                case Country.Guatemala:
                case Country.Honduras:
                case Country.Mexico:
                case Country.Panama:
                case Country.Paraguay:
                case Country.Peru:
                case Country.Puerto_Rico:
                case Country.Uruguay:
                case Country.Venezuela:
                    return SouthAmericanHues;

                case Country.Australia:
                case Country.New_Zealand:
                    return OceaniaHues;

                case Country.Bangladesh:
                case Country.India:
                case Country.Indonesia:
                case Country.Pakistan:
                    return WesternAsianHues;

                case Country.Bhutan:
                case Country.Cambodia:
                case Country.China:
                case Country.Hong_Kong:
                case Country.Japan:
                case Country.North_Korea:
                case Country.South_Korea:
                case Country.Kyrgyzstan:
                case Country.Lao:
                case Country.Macao:
                case Country.Mongolia:
                case Country.Malaysia:
                case Country.Myanmar:
                case Country.Taiwan:
                case Country.Nepal:
                case Country.Viet_Nam:
                case Country.Sri_Lanka:
                case Country.Singapore:
                case Country.Thailand:
                    return EasternAsianHues;


                case Country.United_Kingdom:
                case Country.United_States:
                    return MixedHues;

                default: return null;
            }
        }

        public uint Generation => (uint)(1 + ((this.ID - NachoConstants.BASE_LUCHADOR_ID) / NachoConstants.LUCHADOR_GENERATION_SIZE));

        public string GetDescription(LuchadorDescriptionKind kind)
        {
            switch (kind)
            {
                case LuchadorDescriptionKind.Full:
                    return $"Lv: {Level} / Gen {this.Generation} / {Rarity}";

                case LuchadorDescriptionKind.Normal:
                    return $"Lv: {Level} / {Rarity}";

                default:
                    return $"Lv: {Level}";
            }
        }

        public static void GetDummyVisuals(PraticeLevel level, out Hue hue, out sbyte shade)
        {
            switch (level)
            {
                case PraticeLevel.Wood:
                    {
                        hue = Hue.Brown;
                        shade = 0;
                        break;
                    }

                case PraticeLevel.Iron:
                    {
                        hue = Hue.Gray;
                        shade = -1;
                        break;
                    }

                case PraticeLevel.Steel:
                    {
                        hue = Hue.Blue;
                        shade = 0;
                        break;
                    }

                case PraticeLevel.Silver:
                    {
                        hue = Hue.Gray;
                        shade = 1;
                        break;
                    }

                case PraticeLevel.Gold:
                    {
                        hue = Hue.Brown;
                        shade = 2;
                        break;
                    }

                case PraticeLevel.Ruby:
                    {
                        hue = Hue.Pink;
                        shade = 0;
                        break;
                    }

                case PraticeLevel.Emerald:
                    {
                        hue = Hue.Green;
                        shade = 2;
                        break;
                    }

                case PraticeLevel.Diamond:
                    {
                        hue = Hue.Cyan;
                        shade = 1;
                        break;
                    }

                default:
                    {
                        hue = Hue.Purple;
                        shade = 0;
                        break;
                    }
            }
        }

        private float GetStatRating(StatKind kind)
        {
            var valBase = GetBaseStat(kind);
            var maxBase = Formulas.MaxBaseStat;

            var ratingBase = valBase / (float)maxBase;
            if (ratingBase > 1) ratingBase = 1;

            var valGym = GetTrainStat(kind);
            var maxGym = (Formulas.MaxTrainStat / 3.0f);

            var ratingGym = valGym / (float)maxGym;
            if (ratingGym > 1) ratingGym = 1;

            return (ratingBase * 3 + ratingGym) / 4.0f;
        }

        public int GetMarketRating()
        {
            var stat_avg = (GetStatRating(StatKind.Stamina) + GetStatRating(StatKind.Attack) + GetStatRating(StatKind.Defense)) / 3.0f;

            var avg = stat_avg * 0.8f + 0.2f;

            var result = (int)(avg * 100);
            if (result > 100)
            {
                result = 100;
            }

            //UnityEngine.Debug.Log("avg: " + avg);

            return result;
        }

        public override string ToString()
        {
            return this.Name;
        }

        //public Gender Gender => WrestlerValidation.IsFemale(this.GetBodyPart(BodyPart.Head).Variation, this.Rarity) ? Gender.Female : Gender.Male;

        public bool IsShiny     => data.flags == WrestlerFlags.Shine;

        public static int GetMaxPossibleStatAtLevel(StatKind stat, int level)
        {
            return Formulas.CalculateWrestlerStat(level, Formulas.MaxBaseStat, Formulas.MaxTrainStat);
        }
    }
}
