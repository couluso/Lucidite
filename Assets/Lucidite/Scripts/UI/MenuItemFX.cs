using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>
    /// Entrée de menu "texte seul" : au survol elle s'enflamme en rouge, glisse un peu, une marque apparaît
    /// à côté et le texte tremble légèrement. Aucun cadre de fenêtre.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuItemFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public TMP_Text label;
        public Graphic marker;
        public Color normalColor = new Color(0.79f, 0.75f, 0.68f);
        public Color hoverColor = new Color(0.64f, 0.09f, 0.1f);
        public Color disabledColor = new Color(0.3f, 0.28f, 0.27f);
        public float slide = 14f;

        Button _button;
        RectTransform _labelRt;
        Vector2 _basePos;
        float _hover;
        bool _over;

        void Awake()
        {
            _button = GetComponent<Button>();
            if (label) { _labelRt = label.rectTransform; _basePos = _labelRt.anchoredPosition; }
            if (marker) marker.canvasRenderer.SetAlpha(0f);
        }

        public void OnPointerEnter(PointerEventData e) { if (_button.interactable) { _over = true; AudioManager.Play(Sfx.Hover, 0.6f); } }
        public void OnPointerExit(PointerEventData e) => _over = false;
        public void OnSelect(BaseEventData e) => _over = true;
        public void OnDeselect(BaseEventData e) => _over = false;

        void OnDisable() { _over = false; _hover = 0f; Apply(); }

        void Update()
        {
            _hover = Mathf.MoveTowards(_hover, _over && _button.interactable ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            Apply();
        }

        void Apply()
        {
            if (!label) return;
            bool on = _button == null || _button.interactable;
            label.color = on ? Color.Lerp(normalColor, hoverColor, _hover) : disabledColor;
            if (_labelRt)
            {
                float jitter = _hover > 0.9f && Random.value < 0.08f ? Random.Range(-1.5f, 1.5f) : 0f;
                _labelRt.anchoredPosition = _basePos + new Vector2(slide * _hover + jitter, jitter * 0.5f);
            }
            if (marker) marker.canvasRenderer.SetAlpha(_hover);
        }
    }
}
