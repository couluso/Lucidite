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
        public int maxHandSize = 10;

        [Header("Dépression")]
        public int enemyBaseHp = 42;
        public int enemyHpPerRound = 12;
        [Tooltip("Multiplicateur de dégâts gagné à chaque manche (0.2 = +20 % par manche).")]
        public float enemyDamageScalePerRound = 0.2f;
        [Tooltip("Énergie de la Dépression à la première manche : chacune de ses cartes coûte de l'énergie.")]
        public int enemyEnergyBase = 1;
        [Tooltip("Énergie gagnée par manche (1 : 1 → 2 → 3 → 4...).")]
        public float enemyEnergyPerRound = 1f;
        public int enemyMaxEnergy = 5;
        [Tooltip("Nombre maximum de cartes qu'elle joue en un tour.")]
        public int enemyMaxCardsPerTurn = 3;
        [Tooltip("Renforcements qu'elle « achète » entre deux manches (+1 si elle a gagné la manche).")]
        public int enemyPerksPerRound = 1;

        [Header("Gouttes de sang")]
        public int startingBlood = 60;
        public int rewardRoundWin = 80;
        public int rewardRoundLoss = 50;
        [Tooltip("Bonus : 1 goutte par tranche de X Lucidité restante en fin de manche gagnée.")]
        public int lucidityPerBonusBlood = 2;
        [Tooltip("Gouttes de sang gagnées par point de dégât infligé à la Dépression.")]
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

        [Header("Objets")]
        public int relicSlots = 4;
        public int itemSlots = 3;

        [Header("Rythme (secondes)")]
        public float enemyActDelay = 0.6f;
        public float cardResolveDelay = 0.25f;
    }
}
