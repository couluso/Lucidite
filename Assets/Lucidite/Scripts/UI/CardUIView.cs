using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Carte en version interface (boutique, deck), même design que la carte 3D. Grossit légèrement au survol.</summary>
    public class CardUIView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public CardArtLibrary artLibrary;
        public RawImage face;
        [Tooltip("Calque de la classe (symbole + motif) par-dessus la face.")]
        public RawImage suitOverlay;
        public RawImage art;
        public TMP_Text costText;
        public GameObject costRoot;
        public RawImage costOrb;
        public GameObject valueRoot;
        public RawImage valueOrb;
        public TMP_Text valueText;
        public RawImage[] stats;
        public TMP_Text nameText;
        public TMP_Text typeText;
        public TMP_Text rulesText;
        public TMP_Text footerText;
        public Button button;
        public GameObject selectedMark;

        public CardInstance Instance { get; private set; }
        public CardData Data { get; private set; }

        float _hover;
        bool _over;
        readonly List<CardStat> _stats = new List<CardStat>();

        public void Bind(CardInstance c, string footer = "")
        {
            Instance = c;
            Data = c.data;
            Fill(c, c.Name, footer);
        }

        public void Bind(CardData d, string footer = "")
        {
            Instance = null;
            Data = d;
            Fill(new CardInstance(d), d.cardName, footer);
        }

        /// <summary>Offre de boutique : une carte avec sa classe, pas encore dans le deck.</summary>
        public void BindOffer(CardInstance c, string footer)
        {
            Instance = c;
            Data = c.data;
            Fill(c, c.Name, footer);
        }

        void Fill(CardInstance c, string n, string footer)
        {
            var d = c.data;
            var col = Palette.ForType(d.type);
            if (nameText) nameText.text = n;
            if (typeText) typeText.text = d.arcana;
            if (costRoot) costRoot.SetActive(!d.unplayable);
            if (costOrb) costOrb.color = col;
            if (costText) { costText.text = c.Cost.ToString(); costText.color = Palette.OnType(col); }
            string value = CardFace.Value(c);
            if (valueRoot) valueRoot.SetActive(value.Length > 0);
            if (valueOrb) valueOrb.color = col;
            if (valueText) { valueText.text = value; valueText.color = Palette.OnType(col); }
            if (rulesText) rulesText.text = c.Describe();
            if (footerText) { footerText.text = footer; footerText.gameObject.SetActive(!string.IsNullOrEmpty(footer)); }
            if (artLibrary)
            {
                if (face) face.texture = c.Face ? c.Face : artLibrary.Face(d.type);
                if (face) face.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.45f), c.blood * 0.7f); // tachée de sang
                if (suitOverlay)
                {
                    var so = artLibrary.SuitOverlay(c.suit);
                    suitOverlay.gameObject.SetActive(so);
                    suitOverlay.texture = so;
                }
                if (art) { art.texture = artLibrary.Icon(d.icon); art.color = col; }
                CardFace.Stats(c, _stats);
                if (stats != null)
                    for (int i = 0; i < stats.Length; i++)
                    {
                        var tex = i < _stats.Count ? artLibrary.Stat(_stats[i]) : null;
                        stats[i].gameObject.SetActive(tex);
                        stats[i].texture = tex;
                    }
            }
            SetSelected(false);
        }

        public void SetSelected(bool on) { if (selectedMark) selectedMark.SetActive(on); }

        public void OnClick(Action<CardUIView> cb)
        {
            if (!button) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { AudioManager.Play(Sfx.UIClick); cb?.Invoke(this); });
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _over = true;
            if (button && button.interactable) AudioManager.Play(Sfx.Hover, 0.4f);
            var ui = UIManager.Instance;
            if (ui && ui.inspector)
            {
                if (Instance != null) ui.inspector.ShowCard(Instance);
                else if (Data) ui.inspector.ShowCard(new CardInstance(Data));
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            _over = false;
            var ui = UIManager.Instance;
            if (ui && ui.inspector) ui.inspector.HideText();
        }

        void OnDisable()
        {
            // Détruite pendant le survol (carte achetée) : on libère le panneau d'examen
            if (_over && UIManager.Instance && UIManager.Instance.inspector) UIManager.Instance.inspector.HideText();
            _over = false; _hover = 0f; transform.localScale = Vector3.one;
        }

        void Update()
        {
            _hover = Mathf.MoveTowards(_hover, _over ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            transform.localScale = Vector3.one * (1f + 0.06f * _hover);
        }
    }
}
