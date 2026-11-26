using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// ART BIBLE — source unique des couleurs du projet.
    /// Style : huis clos PSX low-poly, textures basse résolution filtrées en "point", grain VHS, une seule ampoule nue.
    /// Palette : noir, rouge sombre, gris cendre (+ un ambre "espoir" réservé à la lumière et aux victoires).
    /// Ambiance : une ampoule qui pend au-dessus de la table, tout le reste se noie dans le noir.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Void       = Hex("0A0909"); // ombres, fond, fog
        public static readonly Color Soot       = Hex("1A1716"); // murs dans le noir
        public static readonly Color AshDark    = Hex("4A4644"); // tissus, uniforme gris
        public static readonly Color Ash        = Hex("8E8A86"); // gris cendre : peau de la Dépression
        public static readonly Color Bone       = Hex("C9BFAE"); // papier, texte clair
        public static readonly Color Wood       = Hex("3B2A1E"); // table
        public static readonly Color WoodLight  = Hex("5A4130");
        public static readonly Color BloodDark  = Hex("3E0608"); // sang séché
        public static readonly Color Blood      = Hex("6E0B0E"); // rouge sombre principal
        public static readonly Color BloodLight = Hex("A3161A"); // sang frais / dégâts
        public static readonly Color Skin       = Hex("C7A893"); // peau de l'étudiante
        public static readonly Color Uniform    = Hex("1E2233"); // uniforme marine
        public static readonly Color Bulb       = Hex("E8C48A"); // lumière de l'ampoule
        public static readonly Color Hope       = Hex("D9A441"); // accent : espoir / victoire

        // Couleurs de type de carte
        public static readonly Color TypeAttack  = Hex("8C1A1A");
        public static readonly Color TypeDefense = Hex("4C5A66");
        public static readonly Color TypeCrisis  = Hex("2A1E2E");
        public static readonly Color TypeTorment = Hex("5B3A1E");

        public static Color ForType(CardType t)
        {
            switch (t)
            {
                case CardType.Attaque: return TypeAttack;
                case CardType.Defense: return TypeDefense;
                case CardType.Crise: return TypeCrisis;
                default: return TypeTorment;
            }
        }

        public static string HexOf(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
    }
}
