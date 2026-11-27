using System.Collections;
using TMPro;
using UnityEngine;

namespace Lucidite
{
    /// <summary>Chiffres flottants (dégâts, bouclier, soins) au-dessus de la Dépression ou au bord bas de l'écran.</summary>
    public class DamagePopups : MonoBehaviour
    {
        public TMP_Text popupPrefab;
        public RectTransform playerAnchor;
        public Vector3 enemyWorldOffset = new Vector3(0, 0.05f, 0);

        Transform _enemyAnchor;
        Canvas _canvas;

        public static DamagePopups Instance { get; private set; }
        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Texte flottant au-dessus d'un point de la scène (ex. une relique qui fait effet).</summary>
        public void SpawnWorld(Vector3 world, string label, Color color, float scale)
        {
            if (!popupPrefab || !_canvas || !PSXScreen.Instance || !PSXScreen.Instance.worldCamera) return;
            var vp = PSXScreen.Instance.worldCamera.WorldToViewportPoint(world);
            if (vp.z <= 0f) return;
            var size = ((RectTransform)_canvas.transform).rect.size;
            var t = Instantiate(popupPrefab, transform);
            t.gameObject.SetActive(true);
            t.text = label;
            t.color = color;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = new Vector2(vp.x * size.x, vp.y * size.y);
            rt.localScale = Vector3.one * scale;
            StartCoroutine(Animate(t));
        }

        void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            var opp = FindAnyObjectByType<OpponentView>();
            if (opp) _enemyAnchor = opp.head ? opp.head : opp.transform;
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnDamaged += (side, lost, blocked) =>
            {
                if (lost > 0) Spawn(side, $"-{lost}", side == Side.Player ? Palette.BloodLight : Palette.Bone, 1f + Mathf.Min(lost, 20) / 25f);
                else if (blocked > 0) Spawn(side, "bloqué", Palette.Ash, 0.7f);
            };
            gm.OnHealed += (side, amount) => Spawn(side, $"+{amount}", Palette.Hope, 0.9f);
            gm.OnBlockGained += (side, amount) => Spawn(side, $"+{amount} bouclier", Palette.Ash, 0.7f);
        }

        Vector2 PositionFor(Side side)
        {
            var size = ((RectTransform)_canvas.transform).rect.size;
            if (side == Side.Opponent && _enemyAnchor && PSXScreen.Instance && PSXScreen.Instance.worldCamera)
            {
                var vp = PSXScreen.Instance.worldCamera.WorldToViewportPoint(_enemyAnchor.position + enemyWorldOffset);
                return new Vector2(vp.x * size.x, vp.y * size.y);
            }
            if (playerAnchor) return playerAnchor.anchoredPosition;
            return new Vector2(size.x * 0.25f, size.y * 0.25f);
        }

        void Spawn(Side side, string label, Color color, float scale)
        {
            if (!popupPrefab || !_canvas) return;
            var t = Instantiate(popupPrefab, transform);
            t.gameObject.SetActive(true);
            t.text = label;
            t.color = color;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = PositionFor(side) + new Vector2(Random.Range(-40f, 40f), Random.Range(-10f, 20f));
            rt.localScale = Vector3.one * scale;
            StartCoroutine(Animate(t));
        }

        IEnumerator Animate(TMP_Text t)
        {
            var rt = t.rectTransform;
            Vector2 start = rt.anchoredPosition;
            float dur = 1.1f, time = 0f;
            Vector3 baseScale = rt.localScale;
            while (time < dur && t)
            {
                time += Time.unscaledDeltaTime;
                float p = time / dur;
                rt.anchoredPosition = start + new Vector2(Mathf.Sin(p * 9f) * 3f, 70f * p);
                rt.localScale = baseScale * (p < 0.1f ? Mathf.Lerp(1.6f, 1f, p / 0.1f) : 1f);
                t.alpha = 1f - Mathf.Clamp01((p - 0.6f) / 0.4f);
                yield return null;
            }
            if (t) Destroy(t.gameObject);
        }
    }
}
