using UnityEngine;

namespace Lucidite
{
    /// <summary>Réglages d'équilibrage centralisés.</summary>
    [CreateAssetMenu(menuName = "Lucidité/Configuration", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Manches (bougies)")]
        [Tooltip("Bougies de la joueuse : perdre une manche en éteint une, à zéro c'est perdu.")]
        public int playerCandles = 3;
        [Tooltip("Bougies de la Dépression.")]
        public int enemyCandles = 3;

        [Header("Joueuse")]
        public int playerMaxLucidity = 50;
        public int energyPerTurn = 3;
        public int drawPerTurn = 5;
        [Tooltip("Limite de main : la pioche normale (début de tour) s'arrête à ce nombre de cartes.")]
        public int maxHandSize = 7;
        [Tooltip("Plafond absolu : les pioches bonus (cartes « Pioche », objets, reliques) peuvent dépasser la limite jusqu'ici.")]
        public int absoluteMaxHandSize = 12;

        [Header("Dépression")]
        public int enemyBaseHp = 52;
        public int enemyHpPerRound = 9;
        [Tooltip("Multiplicateur de dégâts gagné à chaque manche (0.09 = +9 % par manche).")]
        public float enemyDamageScalePerRound = 0.09f;
        [Tooltip("Énergie de la Dépression à la première manche : chacune de ses cartes coûte de l'énergie.")]
        public int enemyEnergyBase = 1;
        [Tooltip("Énergie gagnée par manche (1 : 1 → 2 → 3 → 4...).")]
        public float enemyEnergyPerRound = 1f;
        [Tooltip("Plafond de l'énergie gagnée avec les manches (hors énergie achetée dans sa boutique).")]
        public int enemyMaxEnergy = 2;
        [Tooltip("Nombre maximum de cartes qu'elle joue en un tour.")]
        public int enemyMaxCardsPerTurn = 3;

        [Header("Dépression : sa boutique (gouttes de sang, achats secrets)")]
        [Tooltip("Elle gagne ses gouttes comme la joueuse : récompense de manche (gagnée / perdue) + sang qu'elle a fait couler.")]
        public int enemyStartingBlood = 60;
        [Tooltip("Multiplicateur de ses gains de gouttes (1 = autant que la joueuse).")]
        public float enemyBloodScale = 1f;
        public int enemyPriceHp = 50;
        public int enemyPerkHp = 8;
        public int enemyPriceStrength = 80;
        public int enemyPriceBlock = 50;
        public int enemyPerkBlock = 6;
        public int enemyPriceSharpen = 55;
        [Tooltip("Bonus de dégâts d'une carte affûtée (0.25 = +25 %), deux fois au plus par carte.")]
        public float enemyPerkSharpen = 0.25f;
        public int enemyPriceNewMove = 70;
        public int enemyPriceEnergy = 150;
        public int enemyMaxBonusEnergy = 1;

        [Header("Gouttes de sang")]
        public int startingBlood = 60;
        public int rewardRoundWin = 80;
        public int rewardRoundLoss = 50;
        [Tooltip("Bonus : 1 goutte par tranche de X Lucidité restante en fin de manche gagnée.")]
        public int lucidityPerBonusBlood = 2;
        [Tooltip("Gouttes de sang gagnées par point de saignement de l'adversaire (Lucidité perdue × multiplicateur de saignement).")]
        public float bloodPerDamage = 0.6f;

        [Header("Boutique")]
        public int cardOffers = 3;
        public int priceCommon = 45;
        public int priceUncommon = 70;
        public int priceRare = 110;
        public int upgradeCost = 50;
        public int removeCostBase = 60;
        public int removeCostIncrease = 25;
        public int transformCost = 40;
        [Tooltip("Hausse du prix d'un service (évoluer, transformer) à chaque utilisation dans la même boutique.")]
        public int serviceCostIncrease = 25;
        public int rerollCost = 10;
        public int rerollCostIncrease = 5;
        public int pillOffers = 3;
        public int relicOffers = 2;

        [Header("Classes")]
        [Tooltip("Bonus par niveau de classe aux dégâts, au bouclier et au soin des cartes de cette classe.")]
        public int suitLevelBonus = 2;

        [Header("Objets")]
        public int relicSlots = 4;
        public int itemSlots = 3;

        [Header("Rythme (secondes)")]
        public float enemyActDelay = 0.6f;
        public float cardResolveDelay = 0.25f;
    }
}
