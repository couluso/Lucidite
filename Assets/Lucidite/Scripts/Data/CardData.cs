using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Définition d'une carte (inspirée du tarot). Système à énergie : coût, dégâts, bouclier, effets.
    /// Les cartes possédées en partie sont des <see cref="CardInstance"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Lucidité/Carte", fileName = "Card_")]
    public class CardData : ScriptableObject
    {
        [Header("Identité")]
        public string cardName = "Nouvelle carte";
        [Tooltip("Numéro d'arcane affiché sur la carte (ex : XIII).")]
        public string arcana = "0";
        public CardType type = CardType.Attaque;
        public CardRarity rarity = CardRarity.Commune;
        public CardTheme theme = CardTheme.Depression;
        [TextArea(2, 4)] public string flavorText = "";

        [Header("Coût")]
        [Range(0, 5)] public int cost = 1;
        [Tooltip("La carte ne peut pas être jouée (Crises).")]
        public bool unplayable;

        [Header("Effets")]
        public int damage;
        [Min(1)] public int hits = 1;
        public int block;
        public int draw;
        public int energyGain;
        public int heal;
        public int applyWeak;
        public int applyVulnerable;
        public int strengthGain;
        public int bloodGain;
        public CardSpecial special = CardSpecial.None;
        public int specialValue;

        [Header("Mots-clés")]
        [Tooltip("Épuiser : retirée pour le reste de la manche une fois jouée.")]
        public bool exhaust;
        [Tooltip("Éphémère : épuisée si elle est encore en main à la fin du tour.")]
        public bool ethereal;
        [Tooltip("Fin du tour, si en main : perds X Lucidité.")]
        public int loseLucidityIfHeld;
        [Tooltip("Quand elle est piochée : perds X énergie.")]
        public int energyLossOnDraw;

        [Header("Amélioration (carte +)")]
        public int upCost;
        public int upDamage;
        public int upBlock;
        public int upDraw;
        [Tooltip("Ajouté à Faiblesse / Vulnérable / Force / Soin.")]
        public int upMagic;
        public bool upRemovesExhaust;

        [Header("Progression")]
        [Tooltip("0 = prix calculé selon la rareté.")]
        public int priceOverride;
        [Tooltip("Si renseigné, l'amélioration fait évoluer la carte en celle-ci.")]
        public CardData evolvesInto;
        [Tooltip("Transformation en boutique (surtout pour les Crises).")]
        public CardData transformsInto;
        public bool inShop = true;

        public static string TypeLabel(CardType t)
        {
            switch (t)
            {
                case CardType.Attaque: return "ATTAQUE";
                case CardType.Defense: return "DÉFENSE";
                case CardType.Crise: return "CRISE";
                default: return "TOURMENT";
            }
        }

        public static string RarityLabel(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Depart: return "Départ";
                case CardRarity.Commune: return "Commune";
                case CardRarity.PeuCommune: return "Peu commune";
                case CardRarity.Rare: return "Rare";
                default: return "Crise";
            }
        }

        public static string ThemeLabel(CardTheme t)
        {
            switch (t)
            {
                case CardTheme.Depression: return "Dépression";
                case CardTheme.Anxiete: return "Anxiété";
                case CardTheme.Desespoir: return "Désespoir";
                case CardTheme.VideSolitude: return "Vide & solitude";
                case CardTheme.Culpabilite: return "Culpabilité";
                case CardTheme.Dissociation: return "Dissociation";
                case CardTheme.PenseesIntrusives: return "Pensées intrusives";
                case CardTheme.Insomnie: return "Insomnie";
                case CardTheme.Traumatismes: return "Traumatismes";
                case CardTheme.Soutien: return "Soutien";
                case CardTheme.Acceptation: return "Acceptation";
                case CardTheme.Resilience: return "Résilience";
                default: return "Espoir";
            }
        }
    }
}
