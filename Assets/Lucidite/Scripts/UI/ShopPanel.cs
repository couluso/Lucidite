using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>
    /// Boutique entre les manches, en sections titrées : CARTES (avec leur classe), PILULES (gélules filmées en 3D,
    /// aspect tiré au hasard à chaque partie), RELIQUES (statuettes qui tournent, posées sur la table une fois
    /// achetées), SERVICES (répétables, prix croissant) et RELANCER. En bas : ton pilulier, pour avaler en boutique
    /// les pilules qui le permettent. Le Reliquaire ouvre un choix de la relique à magnifier.
    /// </summary>
    public class ShopPanel : UIPanel
    {
        [Header("Prefabs")]
        public CardUIView offerCardPrefab;
        public ItemSlotUI goodPrefab;
        public ItemSlotUI ownedPillPrefab;

        [Header("Conteneurs")]
        public Transform cardOffersContainer;
        public Transform pillOffersContainer;
        public Transform relicOffersContainer;
        public Transform ownedPillsContainer;

        [Header("Textes")]
        public TMP_Text bloodText;
        public TMP_Text feedbackText;
        public TMP_Text pillsInfoText;
        public TMP_Text relicsInfoText;
        public TMP_Text levelsText;

        [Header("Services")]
        public Button upgradeButton;
        public Button removeButton;
        public Button transformButton;
        public Button rerollButton;
        public Button continueButton;
        public DeckPickerPanel deckPicker;

        [Header("Reliquaire : choix de la relique à magnifier")]
        public GameObject chooserRoot;
        public Transform chooserContainer;

        readonly List<GameObject> _spawned = new List<GameObject>();
        GameManager GM => GameManager.Instance;
        ShopSystem Shop => GM ? GM.Shop : null;
        bool _choosing;
        int _stageSlot;

        void Awake()
        {
            upgradeButton.onClick.AddListener(() => Pick(DeckPickerPanel.Mode.Upgrade));
            removeButton.onClick.AddListener(() => Pick(DeckPickerPanel.Mode.Remove));
            transformButton.onClick.AddListener(() => Pick(DeckPickerPanel.Mode.Transform));
            if (rerollButton) rerollButton.onClick.AddListener(Reroll);
            continueButton.onClick.AddListener(() => { if (_choosing) return; Hide(); GM.CloseShop(); });
        }

        public override void Show()
        {
            base.Show();
            if (RelicStage.Instance) RelicStage.Instance.SetActive(true);
            _choosing = false;
            Feedback("");
            Refresh();
        }

        public override void Hide()
        {
            base.Hide();
            if (RelicStage.Instance) { RelicStage.Instance.SetActive(false); RelicStage.Instance.Clear(); }
        }

        void Reroll()
        {
            if (_choosing) return;
            string err = Shop.Reroll();
            if (err != null) { AudioManager.Play(Sfx.Error); Feedback(err); }
            else { AudioManager.Play(Sfx.CardSlide); Feedback("Nouvelles offres."); }
            Refresh();
        }

        void Pick(DeckPickerPanel.Mode mode)
        {
            if (!deckPicker || _choosing) return;
            deckPicker.Open(mode, card =>
            {
                string err;
                switch (mode)
                {
                    case DeckPickerPanel.Mode.Upgrade: err = Shop.Upgrade(card); break;
                    case DeckPickerPanel.Mode.Remove: err = Shop.Remove(card); break;
                    default: err = Shop.Transform(card); break;
                }
                if (err != null) { AudioManager.Play(Sfx.Error); Feedback(err); }
                else
                {
                    AudioManager.Play(Sfx.Buy);
                    Feedback(mode == DeckPickerPanel.Mode.Remove ? "La carte disparaît de ton deck." :
                             mode == DeckPickerPanel.Mode.Upgrade ? $"« {card.Name} » a évolué." : $"La Crise devient « {card.Name} ».");
                    GM.NotifyDeckChanged();
                }
                Refresh();
            });
        }

        void Feedback(string msg) { if (feedbackText) feedbackText.text = msg; }

        public void Refresh()
        {
            if (!GM || Shop == null) return;
            foreach (var g in _spawned) if (g) Destroy(g);
            _spawned.Clear();
            if (RelicStage.Instance) RelicStage.Instance.Clear();
            _stageSlot = 0;
            bloodText.text = $"{GM.Blood} gouttes de sang";

            // Cartes
            foreach (var offer in new List<CardInstance>(Shop.cardOffers))
            {
                var v = Instantiate(offerCardPrefab, cardOffersContainer);
                int price = Shop.CardPrice(offer.data);
                v.BindOffer(offer, $"{price}");
                v.OnClick(_ =>
                {
                    if (_choosing) return;
                    string err = Shop.BuyCard(offer);
                    if (err != null) { AudioManager.Play(Sfx.Error); Feedback(err); }
                    else { AudioManager.Play(Sfx.Buy); Feedback($"« {offer.data.cardName} » ({CardData.SuitLabel(offer.suit)}) rejoint ton deck."); GM.Stats.cardsBought++; GM.NotifyDeckChanged(); }
                    Refresh();
                });
                if (v.button) v.button.interactable = GM.Blood >= price && !_choosing;
                _spawned.Add(v.gameObject);
            }

            // Pilules et reliques (filmées en 3D dans le studio)
            Goods(Shop.pillOffers, pillOffersContainer, false);
            Goods(Shop.relicOffers, relicOffersContainer, true);
            if (pillsInfoText) pillsInfoText.text = $"pilulier  {GM.Player.items.Count} / {GM.config.itemSlots}";
            if (relicsInfoText) relicsInfoText.text = $"sur ta table  {GM.Player.relics.Count} / {GM.config.relicSlots}";

            // Ton pilulier (certaines pilules s'avalent aussi en boutique)
            if (ownedPillsContainer && ownedPillPrefab)
                foreach (var o in new List<OwnedItem>(GM.Player.items))
                {
                    var v = Instantiate(ownedPillPrefab, ownedPillsContainer);
                    v.Bind(o);
                    if (RelicStage.Instance) v.SetPreview(RelicStage.Instance.ShowPill(_stageSlot++, o.data));
                    v.OnClick(s =>
                    {
                        if (_choosing) return;
                        string why = GM.ItemBlockReason(s.Owned);
                        if (why != null) { AudioManager.Play(Sfx.Error); Feedback(why); return; }
                        GM.UseItem(s.Owned); Feedback($"Tu avales : {o.data.itemName}."); Invoke(nameof(Refresh), 0.05f);
                    });
                    v.SetInteractable(GM.CanUseItem(o) && !_choosing);
                    _spawned.Add(v.gameObject);
                }

            // Niveaux de classe
            if (levelsText)
            {
                var parts = new List<string>();
                for (int s = 1; s <= 4; s++)
                    parts.Add($"{CardData.SuitLabel((CardSuit)s)} {CardInstance.SuitLevels[s]}");
                levelsText.text = "niveaux   " + string.Join("   ·   ", parts);
            }

            bool hasCrisis = false;
            foreach (var c in GM.Player.deck.All) if (c.CanTransform) { hasCrisis = true; break; }
            SetService(upgradeButton, $"Faire évoluer une carte  —  {Shop.UpgradeCost}", GM.Blood >= Shop.UpgradeCost && !_choosing);
            SetService(removeButton, $"Brûler une carte  —  {Shop.RemoveCost}", GM.Blood >= Shop.RemoveCost && !_choosing);
            transformButton.gameObject.SetActive(hasCrisis);
            SetService(transformButton, $"Transformer une Crise  —  {Shop.TransformCost}", GM.Blood >= Shop.TransformCost && !_choosing);
            if (rerollButton) SetService(rerollButton, Shop.FreeReroll ? "Relancer la boutique  —  gratuit" : $"Relancer la boutique  —  {Shop.RerollCost}", GM.Blood >= Shop.RerollCost && !_choosing);
            continueButton.interactable = !_choosing;

            BuildChooser();
        }

        void Goods(List<ItemData> offers, Transform container, bool relic)
        {
            if (!container || !goodPrefab) return;
            foreach (var item in new List<ItemData>(offers))
            {
                var v = Instantiate(goodPrefab, container);
                int price = Shop.ItemPrice(item);
                v.Bind(item, $"{price}");
                if (RelicStage.Instance)
                    v.SetPreview(relic ? RelicStage.Instance.Show(_stageSlot++, item.model) : RelicStage.Instance.ShowPill(_stageSlot++, item));
                v.OnClick(_ =>
                {
                    if (_choosing) return;
                    string err = Shop.BuyItem(item);
                    if (err != null) { AudioManager.Play(Sfx.Error); Feedback(err); }
                    else
                    {
                        AudioManager.Play(Sfx.Buy);
                        Feedback(relic ? $"{item.itemName} t'attend sur la table." : $"{item.itemName} rejoint ton pilulier.");
                        GM.NotifyItemBought(Side.Player, item);
                        if (item.effect == ItemEffect.Reliquaire) { _choosing = true; Feedback("Choisis la relique que le Reliquaire va magnifier."); }
                        GM.NotifyItemsChanged();
                    }
                    Refresh();
                });
                v.SetInteractable(GM.Blood >= price && Shop.CanHoldItem(item) && !_choosing);
                _spawned.Add(v.gameObject);
            }
        }

        /// <summary>Reliquaire acheté : tes autres reliques, au choix (obligatoire avant de continuer).</summary>
        void BuildChooser()
        {
            if (!chooserRoot) { _choosing = false; return; }
            chooserRoot.SetActive(_choosing);
            if (!_choosing || !chooserContainer) return;
            foreach (var r in GM.Player.relics)
            {
                if (r.effect == ItemEffect.Reliquaire) continue;
                var v = Instantiate(goodPrefab, chooserContainer);
                v.Bind(r, "");
                if (RelicStage.Instance) v.SetPreview(RelicStage.Instance.Show(_stageSlot++, r.model));
                var pick = r;
                v.OnClick(_ =>
                {
                    GM.Player.buffTarget = pick;
                    _choosing = false;
                    AudioManager.Play(Sfx.Buy);
                    Feedback($"Le Reliquaire magnifie « {pick.itemName} » : effet doublé.");
                    GM.NotifyItemsChanged();
                    Refresh();
                });
                _spawned.Add(v.gameObject);
            }
        }

        static void SetService(Button b, string label, bool on)
        {
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t) t.text = label;
            b.interactable = on;
        }

        protected override void Update()
        {
            base.Update();
            if (IsVisible && GM && bloodText) bloodText.text = $"{GM.Blood} gouttes de sang";
        }
    }
}
