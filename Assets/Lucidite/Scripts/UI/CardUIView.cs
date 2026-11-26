using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Carte en version interface (boutique, deck). Grossit légèrement au survol.</summary>
    public class CardUIView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public CardArtLibrary artLibrary;
        public Image frame;
        public Image typeBand;
        public RawImage art;
        public TMP_Text costText;
        public GameObject costRoot;
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

        public void Bind(CardInstance c, string footer = "")
        {
            Instance = c;
            Data = c.data;
            Fill(c.data, c.Name, c.Cost, c.Describe(), footer);
        }

        public void Bind(CardData d, string footer = "")
        {
            Instance = null;
            Data = d;
            var tmp = new CardInstance(d);
            Fill(d, d.cardName, tmp.Cost, tmp.Describe(), footer);
        }

        void Fill(CardData d, string n, int cost, string rules, string footer)
        {
            if (nameText) nameText.text = n;
            if (typeText) typeText.text = $"{CardData.TypeLabel(d.type)}  ·  {d.arcana}";
            if (costRoot) costRoot.SetActive(!d.unplayable);
            if (costText) costText.text = cost.ToString();
            if (rulesText) rulesText.text = rules;
            if (footerText) { footerText.text = footer; footerText.gameObject.SetActive(!string.IsNullOrEmpty(footer)); }
            if (typeBand) typeBand.color = Palette.ForType(d.type);
            if (frame) frame.color = Color.Lerp(Palette.ForType(d.type), Color.white, 0.35f);
            if (art && artLibrary) art.texture = artLibrary.Get(d.theme);
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
