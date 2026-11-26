using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>
    /// HUD de combat volontairement minimal : Lucidité + bouclier (bas gauche), énergie,
    /// bouton "finir le tour" et piles (bas droite), consommables. Rien d'autre pendant la manche.
    /// </summary>
    public class HUDPanel : UIPanel
    {
        [Header("Lucidité")]
        public TMP_Text lucidityText;
        public RectTransform lucidityBar;
        public RectTransform lucidityBarGhost;
        public TMP_Text blockText;
        public GameObject blockRoot;
        public TMP_Text statusText;

        [Header("Énergie")]
        public TMP_Text energyText;
        public Image energyOrb;

        [Header("Tour")]
        public Button endTurnButton;
        public TMP_Text endTurnLabel;
        public Button drawPileButton;
        public Button discardPileButton;
        public TMP_Text drawPileText;
        public TMP_Text discardPileText;

        [Header("Objets")]
        public Transform itemsContainer;
        public ItemSlotUI itemButtonPrefab;

        float _ghost = 1f;
        int _lastHp = int.MaxValue, _lastEnergy = -1;
        float _hpShake, _energyPunch;
        Vector2? _hpBase;
        int _itemsSignature = -1;
        readonly List<ItemSlotUI> _spawned = new List<ItemSlotUI>();

        protected override void Update()
        {
            base.Update();
            var gm = GameManager.Instance;
            if (!gm || gm.Player == null) return;
            var p = gm.Player;

            float frac = p.maxHp > 0 ? p.hp / (float)p.maxHp : 0f;
            if (lucidityText) lucidityText.text = $"{p.hp}<size=55%>/{p.maxHp}</size>";
            if (lucidityBar) lucidityBar.localScale = new Vector3(frac, 1f, 1f);
            _ghost = Mathf.MoveTowards(_ghost, frac, Time.unscaledDeltaTime * (frac < _ghost ? 0.35f : 5f));
            if (lucidityBarGhost) lucidityBarGhost.localScale = new Vector3(Mathf.Max(_ghost, frac), 1f, 1f);
            if (blockRoot) blockRoot.SetActive(p.block > 0);
            if (blockText) blockText.text = p.block.ToString();
            if (statusText) statusText.text = p.StatusText();

            // Juice : la Lucidité tressaute quand elle baisse, l'orbe d'énergie "pulse" quand elle change
            int avail = gm.AvailableEnergy;
            if (p.hp < _lastHp) _hpShake = 1f;
            if (avail != _lastEnergy) _energyPunch = 1f;
            _lastHp = p.hp; _lastEnergy = avail;
            _hpShake = Mathf.MoveTowards(_hpShake, 0f, Time.unscaledDeltaTime * 2.5f);
            _energyPunch = Mathf.MoveTowards(_energyPunch, 0f, Time.unscaledDeltaTime * 4f);
            if (lucidityText)
            {
                if (_hpBase == null) _hpBase = lucidityText.rectTransform.anchoredPosition;
                lucidityText.rectTransform.anchoredPosition = _hpBase.Value + Random.insideUnitCircle * 14f * _hpShake;
                lucidityText.color = Color.Lerp(Palette.Bone, Palette.BloodLight, _hpShake);
            }

            if (energyText) energyText.text = $"{avail}/{p.maxEnergy}";
            if (energyOrb)
            {
                energyOrb.color = avail > 0 ? Palette.Hope : Palette.AshDark;
                energyOrb.rectTransform.localScale = Vector3.one * (1f + 0.18f * _energyPunch);
            }

            bool canAct = gm.CanAct;
            if (endTurnButton) endTurnButton.interactable = canAct;
            if (endTurnLabel)
            {
                bool nothingPlayable = canAct && (gm.Selected.Count > 0 || !p.hand.Exists(c => gm.IsPlayable(c, out _)));
                int sel = gm.Selected.Count;
                endTurnLabel.text = gm.Phase == GamePhase.EnemyTurn ? "SON TOUR..." : sel > 0 ? $"JOUER LE TOUR ({sel})  [E]" : "PASSER LE TOUR  [E]";
                if (nothingPlayable) endTurnLabel.alpha = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f);
                else endTurnLabel.alpha = 1f;
            }
            if (drawPileText) drawPileText.text = $"pioche  {p.deck.DrawCount}";
            if (discardPileText) discardPileText.text = $"défausse  {p.deck.DiscardCount}";

            int sig = 17;
            foreach (var o in p.items) sig = sig * 31 + o.GetHashCode();
            if (sig != _itemsSignature) { _itemsSignature = sig; RebuildItems(gm); }
            foreach (var s in _spawned) if (s && s.Owned != null) s.SetInteractable(gm.CanUseItem(s.Owned));
        }

        void RebuildItems(GameManager gm)
        {
            foreach (var s in _spawned) if (s) Destroy(s.gameObject);
            _spawned.Clear();
            if (!itemsContainer || !itemButtonPrefab) return;
            foreach (var o in gm.Player.items)
            {
                var ui = Instantiate(itemButtonPrefab, itemsContainer);
                ui.Bind(o);
                var owned = o;
                ui.OnClick(_ => gm.UseItem(owned));
                _spawned.Add(ui);
            }
        }
    }
}
