using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Garde un bloc d'interface à sa taille de conception et le met à l'échelle pour qu'il tienne entièrement
    /// dans son parent, quel que soit le format de l'écran (évite que les sections de la boutique se chevauchent).
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public class UIFitInParent : MonoBehaviour
    {
        public Vector2 designSize = new Vector2(1840f, 1040f);
        [Range(0.2f, 2f)] public float maxScale = 1f;

        void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (!parent) return;
            var size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float k = Mathf.Min(maxScale, Mathf.Min(size.x / designSize.x, size.y / designSize.y));
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = designSize;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = new Vector3(k, k, 1f);
        }
    }
}
