using Phantasma.Numerics;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Cryptography;

namespace Phantasma.Spook.Nachomen
{
    public interface IIndexable
    {
        BigInteger ID { get; }
        void Copy(IIndexable other);
    }

    public interface ICollectable :IIndexable
    {
        string Name { get; }
        Rarity Rarity { get; }
        Address Owner { get; }
        int GetMarketRating(); // 0 to 100
    }

    public static class CollectableUtils
    {
        public static Hue GetHue(this Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return Hue.Lime;
                case Rarity.Rare: return Hue.Purple;
                case Rarity.Epic: return Hue.Cyan;
                case Rarity.Legendary: return Hue.Red;
                case Rarity.Bot: return Hue.Beige;
                default: return Hue.Brown;
            }
        }
    }


}