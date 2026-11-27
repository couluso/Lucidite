using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>Illustrations (sigils façon tarot) par thématique, générées par l'outil de setup.</summary>
    [CreateAssetMenu(menuName = "Lucidité/Bibliothèque d'illustrations", fileName = "CardArtLibrary")]
    public class CardArtLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public CardTheme theme;
            public Texture2D texture;
        }

        [System.Serializable]
        public struct StatEntry
        {
            public CardStat stat;
            public Texture2D texture;
        }

        public List<Entry> entries = new List<Entry>();
        [Header("Design des cartes")]
        public Texture2D faceAttack, faceDefense, faceTorment, faceCrisis, faceEnemy;
        [Tooltip("Rond du coût et de la valeur (blanc, teinté par la couleur du type).")]
        public Texture2D orb;
        public Texture2D iconFallback;
        public List<StatEntry> statIcons = new List<StatEntry>();
        [Tooltip("Masques de sang des cartes (RGB = sang, A = seuil d'apparition), un au hasard par carte.")]
        public List<Texture2D> cardBlood = new List<Texture2D>();
        [Tooltip("Calque de classe de la face (symbole + motif), indexé par CardSuit (0 = aucune).")]
        public List<Texture2D> suitOverlays = new List<Texture2D>();
        [Tooltip("Symbole seul de chaque classe (interface), indexé par CardSuit.")]
        public List<Texture2D> suitIcons = new List<Texture2D>();
        public Texture2D fallback;
        public Texture2D cardBack;
        public Texture2D bloodDropIcon;
        public Texture2D lucidityIcon;

        public Texture2D Get(CardTheme theme)
        {
            foreach (var e in entries) if (e.theme == theme && e.texture) return e.texture;
            return fallback;
        }

        public Texture2D Face(CardType t)
        {
            switch (t)
            {
                case CardType.Attaque: return faceAttack;
                case CardType.Defense: return faceDefense;
                case CardType.Crise: return faceCrisis;
                default: return faceTorment;
            }
        }

        public Texture2D Stat(CardStat s)
        {
            foreach (var e in statIcons) if (e.stat == s) return e.texture;
            return null;
        }

        public Texture2D Icon(Texture2D own) => own ? own : iconFallback;

        public Texture2D SuitOverlay(CardSuit s) => (int)s > 0 && (int)s < suitOverlays.Count ? suitOverlays[(int)s] : null;
        public Texture2D SuitIcon(CardSuit s) => (int)s > 0 && (int)s < suitIcons.Count ? suitIcons[(int)s] : null;

        public Texture2D CardBlood(int seed) => cardBlood.Count > 0 ? cardBlood[Mathf.Abs(seed) % cardBlood.Count] : null;
    }
}
