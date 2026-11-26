using UnityEngine;

namespace Lucidite
{
    /// <summary>Panneau d'interface avec fondu (temps non mis à l'échelle, fonctionne en pause).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        public float fadeTime = 0.25f;
        CanvasGroup _cg;
        bool _visible;

        protected CanvasGroup Group => _cg ? _cg : (_cg = GetComponent<CanvasGroup>());
        public bool IsVisible => _visible;

        public virtual void Show()
        {
            gameObject.SetActive(true);
            _visible = true;
            Group.blocksRaycasts = true;
            Group.interactable = true;
        }

        public virtual void Hide()
        {
            _visible = false;
            Group.blocksRaycasts = false;
            Group.interactable = false;
        }

        public void HideInstant()
        {
            Hide();
            Group.alpha = 0f;
            gameObject.SetActive(false);
        }

        protected virtual void Update()
        {
            float target = _visible ? 1f : 0f;
            Group.alpha = Mathf.MoveTowards(Group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
            if (!_visible && Group.alpha <= 0f) gameObject.SetActive(false);
        }
    }
}
