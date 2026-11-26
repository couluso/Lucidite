using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>
    /// Boutique entre les manches, épurée : 3 cartes, 2 objets, 3 services (ouvrent le sélecteur de deck)
    /// et "manche suivante". Pas de fenêtre : les éléments flottent sur le noir.
    /// </summary>
    public class ShopPanel : UIPanel
    {
        [Header("Prefabs")]
        public CardUIView offerCardPrefab;
        public ItemSlotUI itemOfferPrefab;

        [Header("Conteneurs")]
        public Transform cardOffersContainer;
        public Transform itemOffersContainer;

        [Header("Textes")]
        public TMP_Text bloodText;
        public TMP_Text feedbackText;

        [Header("Services")]
        public Button upgradeButton;
        public Button removeButton;
        public Button transformButton;
        public Button continueButton;
        public DeckPickerPanel deckPicker;

        readonly List<GameObject> _spawned = new List<GameObject>();
        GameManager GM => GameManager.Instance;
        ShopSystem Shop => GM ? GM.Shop : null;

        void Awake()
        {
            upgradeButton.onClick.AddListener(() => Pick(DeckPickerPanel.Mode.Upgrade));
            removeButton.onClick.AddListener(() => Pick(DeckPickerPanel.Mode.Remove));
            transformButton.onClick.AddListener(() => Pick(DeckPickerPanel.Mode.Transform));
            continueButton.onClick.AddListener(() => { Hide(); GM.CloseShop(); });
        }

        public override void Show()
        {
            base.Show();
            Feedback(string.IsNullOrEmpty(EnemyNews)
                ? "Dépense tes gouttes de sang. Ou garde-les."
                : $"<color=#B3262D>Pendant ce temps, ta Dépression se renforce :</color> {EnemyNews}");
            EnemyNews = null;
            Refresh();
        }

        void Pick(DeckPickerPanel.Mode mode)
        {
            if (!deckPicker) return;
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

        /// <summary>Achats de la Dépression à annoncer à l'ouverture de la boutique.</summary>
        public string EnemyNews { get; set; }

        public void Refresh()
        {
            if (!GM || Shop == null) return;
            foreach (var g in _spawned) if (g) Destroy(g);
            _spawned.Clear();
            bloodText.text = $"{GM.Blood} gouttes de sang";

            foreach (var data in new List<CardData>(Shop.cardOffers))
            {
                var v = Instantiate(offerCardPrefab, cardOffersContainer);
                int price = Shop.CardPrice(data);
                v.Bind(data, $"{price}");
                v.OnClick(_ =>
                {
                    string err = Shop.BuyCard(data);
                    if (err != null) { AudioManager.Play(Sfx.Error); Feedback(err); }
                    else { AudioManager.Play(Sfx.Buy); Feedback($"« {data.cardName} » rejoint ton deck."); GM.Stats.cardsBought++; GM.NotifyDeckChanged(); }
                    Refresh();
                });
                if (v.button) v.button.interactable = GM.Blood >= price;
                _spawned.Add(v.gameObject);
            }

            foreach (var item in new List<ItemData>(Shop.itemOffers))
            {
                var v = Instantiate(itemOfferPrefab, itemOffersContainer);
                int price = Shop.ItemPrice(item);
                v.Bind(item, $"{price}");
                v.OnClick(_ =>
                {
                    string err = Shop.BuyItem(item);
                    if (err != null) { AudioManager.Play(Sfx.Error); Feedback(err); }
                    else { AudioManager.Play(Sfx.Buy); Feedback($"{item.itemName} est à toi."); GM.NotifyItemsChanged(); }
                    Refresh();
                });
                v.SetInteractable(GM.Blood >= price && Shop.CanHoldItem(item));
                _spawned.Add(v.gameObject);
            }

            bool hasCrisis = false;
            foreach (var c in GM.Player.deck.All) if (c.CanTransform) { hasCrisis = true; break; }
            SetService(upgradeButton, $"Faire évoluer une carte  —  {Shop.UpgradeCost}", !Shop.upgradeUsed && GM.Blood >= Shop.UpgradeCost);
            SetService(removeButton, $"Brûler une carte  —  {Shop.RemoveCost}", !Shop.removeUsed && GM.Blood >= Shop.RemoveCost);
            transformButton.gameObject.SetActive(hasCrisis || Shop.transformUsed);
            SetService(transformButton, $"Transformer une Crise  —  {Shop.TransformCost}", !Shop.transformUsed && GM.Blood >= Shop.TransformCost);
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
