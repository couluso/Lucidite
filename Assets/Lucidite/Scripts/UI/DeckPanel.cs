using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Consultation d'une pile pendant la manche (pioche triée, défausse) ou du deck complet.</summary>
    public class DeckPanel : UIPanel
    {
        public enum Pile { Deck, Draw, Discard }

        public CardUIView cardPrefab;
        public Transform container;
        public TMP_Text headerText;
        public Button closeButton;

        readonly List<GameObject> _spawned = new List<GameObject>();

        void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(Hide);
        }

        public void ShowPile(Pile pile)
        {
            Show();
            foreach (var g in _spawned) if (g) Destroy(g);
            _spawned.Clear();
            var gm = GameManager.Instance;
            if (!gm || gm.Player == null) return;
            var d = gm.Player.deck;
            List<CardInstance> cards;
            switch (pile)
            {
                case Pile.Draw: cards = new List<CardInstance>(d.DrawPile); headerText.text = $"Pioche — {cards.Count} cartes (ordre caché)"; break;
                case Pile.Discard: cards = new List<CardInstance>(d.DiscardPile); headerText.text = $"Défausse — {cards.Count} cartes"; break;
                default: cards = new List<CardInstance>(d.All); headerText.text = $"Ton deck — {cards.Count} cartes"; break;
            }
            cards.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : string.CompareOrdinal(a.data.cardName, b.data.cardName));
            foreach (var c in cards)
            {
                var v = Instantiate(cardPrefab, container);
                v.Bind(c);
                if (v.button) v.button.interactable = false;
                _spawned.Add(v.gameObject);
            }
        }
    }
}
