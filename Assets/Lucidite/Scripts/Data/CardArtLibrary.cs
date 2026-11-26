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

        public List<Entry> entries = new List<Entry>();
        public Texture2D fallback;
        public Texture2D cardBack;
        public Texture2D bloodDropIcon;
        public Texture2D lucidityIcon;

        public Texture2D Get(CardTheme theme)
        {
            foreach (var e in entries) if (e.theme == theme && e.texture) return e.texture;
            return fallback;
        }
    }
}
