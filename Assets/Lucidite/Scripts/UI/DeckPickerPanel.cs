using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Choisir une carte du deck (faire évoluer, brûler, transformer).</summary>
    public class DeckPickerPanel : UIPanel
    {
        public enum Mode { Upgrade, Remove, Transform }

        public CardUIView cardPrefab;
        public Transform container;
        public TMP_Text titleText;
        public Button cancelButton;

        readonly List<GameObject> _spawned = new List<GameObject>();
        Action<CardInstance> _onPick;

        void Awake()
        {
            if (cancelButton) cancelButton.onClick.AddListener(Hide);
        }

        public void Open(Mode mode, Action<CardInstance> onPick)
        {
            _onPick = onPick;
            Show();
            foreach (var g in _spawned) if (g) Destroy(g);
            _spawned.Clear();
            var gm = GameManager.Instance;
            titleText.text = mode == Mode.Upgrade ? "Quelle carte doit évoluer ?" : mode == Mode.Remove ? "Quelle carte brûler ?" : "Quelle Crise transformer ?";

            var cards = new List<CardInstance>(gm.Player.deck.All);
            cards.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : string.CompareOrdinal(a.data.cardName, b.data.cardName));
            foreach (var c in cards)
            {
                bool eligible = mode == Mode.Upgrade ? c.CanUpgrade : mode == Mode.Transform ? c.CanTransform : true;
                if (!eligible) continue;
                var v = Instantiate(cardPrefab, container);
                string footer = mode == Mode.Upgrade && c.data.evolvesInto ? $"→ {c.data.evolvesInto.cardName}" :
                                mode == Mode.Transform ? $"→ {c.data.transformsInto.cardName}" : "";
                v.Bind(c, footer);
                v.OnClick(view => { Hide(); _onPick?.Invoke(view.Instance); });
                _spawned.Add(v.gameObject);
            }
        }
    }
}
