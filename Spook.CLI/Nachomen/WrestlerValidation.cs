using Phantasma.Contracts.Extra;

namespace Phantasma.Spook.Nachomen
{
    public static class WrestlerValidation
    {
        public static bool IsFemale(byte head, Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:
                    return (head >= 29);

                case Rarity.Uncommon:
                    return (head >= 21);

                case Rarity.Legendary:
                    return (head == 56 || head == 48);

                default:
                    return false;
            }
        }

        private static bool HasValidMoveset(Luchador luchador)
        {
            if (luchador.SecondaryMove == WrestlingMove.Pray) return false;
            if (luchador.SecondaryMove == WrestlingMove.Hyper_Slam) return false;
            return true;
        }

        private static bool HasValidVisual(Luchador luchador)
        {
            var legs = luchador.GetBodyPart(BodyPart.Legs).Variation;
            if (legs > 7) return false;

            var body = luchador.GetBodyPart(BodyPart.Body).Variation;
            if (body > 28) return false;

            var head = luchador.GetBodyPart(BodyPart.Head).Variation;
            var isFemale = WrestlerValidation.IsFemale(head, luchador.Rarity);
            if (body > 20 && !isFemale)
            {
                return false;
            }
            if (body <= 20 && isFemale)
            {
                return false;
            }

            var hips = luchador.GetBodyPart(BodyPart.Hips).Variation;
            if (hips > 16) return false;

            var arms = luchador.GetBodyPart(BodyPart.Arms).Variation;
            if (arms > 7) return false;

            var hands = luchador.GetBodyPart(BodyPart.Hands).Variation;
            if (hands > 16) return false;

            var feet = luchador.GetBodyPart(BodyPart.Feet).Variation;
            if (feet > 16) return false;

            switch (luchador.Rarity)
            {
                case Rarity.Bot: if (head > 8) return false; break;
                case Rarity.Common: if (head > 35) return false; break;
                case Rarity.Uncommon: if (head > 22) return false; break;
                case Rarity.Rare: if (head > 20) return false; break;
                case Rarity.Epic: if (head > 24) return false; break;
                case Rarity.Legendary: if (head > 82) return false; break;
            }

            return true;
        }

        public static bool IsValidWrestler(Luchador luchador)
        {
            return HasValidMoveset(luchador) && HasValidVisual(luchador);
        }

    }
}
