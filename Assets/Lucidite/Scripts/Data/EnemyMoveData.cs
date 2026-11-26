using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Action de la Dépression. Elle "pose" la carte face visible au début de ton tour (intention),
    /// puis l'exécute pendant son tour. Les valeurs sont augmentées à chaque manche.
    /// </summary>
    [CreateAssetMenu(menuName = "Lucidité/Action de la Dépression", fileName = "Move_")]
    public class EnemyMoveData : ScriptableObject
    {
        public string moveName = "Action";
        public string arcana = "XV";
        public CardTheme theme = CardTheme.Depression;
        [TextArea(2, 4)] public string flavorText = "";

        [Header("Effets")]
        public int damage;
        [Min(1)] public int hits = 1;
        public int block;
        public int strengthGain;
        public int applyWeak;
        public int applyVulnerable;
        [Tooltip("Nombre de cartes Crise glissées dans ton deck.")]
        public int crisisCount;
        [Tooltip("Vrai : dans ta pioche (tu les reverras vite). Faux : dans ta défausse.")]
        public bool crisisToDrawPile;
        public CardData specificCrisis;

        [Header("Coût")]
        [Tooltip("Énergie dépensée par la Dépression pour jouer cette carte.")]
        [Range(0, 4)] public int cost = 1;
        [Tooltip("Carte qu'elle n'obtient qu'en l'« achetant » entre les manches.")]
        public bool shopOnly;

        [Header("Choix")]
        [Tooltip("Disponible à partir de cette manche.")]
        public int minRound = 1;
        [Min(0)] public float weight = 1f;

        public IntentKind Intent
        {
            get
            {
                bool atk = damage > 0;
                if (atk && crisisCount > 0) return IntentKind.AttaqueMalediction;
                if (atk && block > 0) return IntentKind.AttaqueDefense;
                if (atk) return IntentKind.Attaque;
                if (crisisCount > 0 || applyWeak > 0 || applyVulnerable > 0) return IntentKind.Malediction;
                if (block > 0) return IntentKind.Defense;
                return IntentKind.Renfort;
            }
        }
    }
}
