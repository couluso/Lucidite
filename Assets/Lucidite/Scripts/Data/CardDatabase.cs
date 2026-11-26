using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>Regroupe cartes, actions de la Dépression et objets.</summary>
    [CreateAssetMenu(menuName = "Lucidité/Base de données", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        [Header("Joueuse")]
        public List<CardData> playerStarterDeck = new List<CardData>();
        [Tooltip("Deck de départ : un exemplaire de chacune de ces cartes dans chaque classe.")]
        public List<CardData> starterPerSuit = new List<CardData>();
        [Tooltip("Deck de départ : ces cartes, toutes dans une même classe tirée au hasard.")]
        public List<CardData> starterRandomSuit = new List<CardData>();
        [Tooltip("Cartes achetables en boutique.")]
        public List<CardData> shopPool = new List<CardData>();

        [Header("Dépression")]
        public List<EnemyMoveData> enemyMoves = new List<EnemyMoveData>();
        [Tooltip("Cartes Crise glissées dans ton deck.")]
        public List<CardData> crisisCards = new List<CardData>();

        [Header("Objets")]
        public List<ItemData> items = new List<ItemData>();

        public CardData RandomCrisis()
        {
            if (crisisCards.Count == 0) return null;
            return crisisCards[Random.Range(0, crisisCards.Count)];
        }
    }
}
