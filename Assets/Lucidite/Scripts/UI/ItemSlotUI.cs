using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Objet / relique en interface (offre de boutique ou bouton d'utilisation dans le HUD).</summary>
    public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text descText;
        public TMP_Text footerText;
        public Button button;

        public ItemData Data { get; private set; }
        public OwnedItem Owned { get; private set; }

        public void Bind(ItemData d, string footer = "")
        {
            Data = d;
            Owned = null;
            Fill(footer);
        }

        public void Bind(OwnedItem o)
        {
            Data = o.data;
            Owned = o;
            Fill("");
        }

        void Fill(string footer)
        {
            if (nameText) nameText.text = Data.itemName;
            if (descText) descText.text = Data.Describe();
            if (footerText) { footerText.text = footer; footerText.gameObject.SetActive(!string.IsNullOrEmpty(footer)); }
            if (icon) icon.color = Data.tint;
        }

        public void OnClick(Action<ItemSlotUI> cb)
        {
            if (!button) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { AudioManager.Play(Sfx.UIClick); cb?.Invoke(this); });
        }

        public void SetInteractable(bool on) { if (button) button.interactable = on; }

        public void OnPointerEnter(PointerEventData e)
        {
            _over = true;
            if (Data && UIManager.Instance && UIManager.Instance.inspector)
                UIManager.Instance.inspector.ShowText(Data.itemName, ItemData.KindLabel(Data.kind),
                    $"{Data.Describe()}\n\n<i><color=#{Palette.HexOf(Palette.Ash)}>{Data.flavorText}</color></i>");
        }

        bool _over;

        public void OnPointerExit(PointerEventData e)
        {
            _over = false;
            if (UIManager.Instance && UIManager.Instance.inspector) UIManager.Instance.inspector.HideText();
        }

        // Détruit/désactivé pendant le survol (objet acheté) : on libère le panneau d'examen
        void OnDisable()
        {
            if (_over && UIManager.Instance && UIManager.Instance.inspector) UIManager.Instance.inspector.HideText();
            _over = false;
        }
    }
}
