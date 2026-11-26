using System.Collections;
using TMPro;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Carte physique 3D (prefab Card). Affiche une carte de la joueuse ou une intention de la Dépression.
    /// Se déplace en douceur vers une cible locale.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        [Header("Références (prefab)")]
        public CardArtLibrary artLibrary;
        public Renderer frameRenderer;
        public Renderer artRenderer;
        public Renderer backRenderer;
        public GameObject highlight;
        public Renderer highlightRenderer;
        public GameObject costOrb;
        public TMP_Text costText;
        public TMP_Text nameText;
        public TMP_Text arcanaText;
        public TMP_Text typeText;
        public TMP_Text rulesText;

        [Header("Mouvement")]
        public float moveSharpness = 14f;

        public CardInstance Card { get; private set; }
        public EnemyIntent Intent { get; private set; }
        public bool FaceUp { get; set; } = true;
        public bool IsHovered { get; set; }

        Vector3 _targetPos;
        Quaternion _targetRot = Quaternion.identity;
        Vector3 _targetScale = Vector3.one;
        Vector3 _shakeOffset;
        MaterialPropertyBlock _mpb;
        int _lastSignature;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _targetPos = transform.localPosition;
            _targetRot = transform.localRotation;
            _targetScale = transform.localScale;
            SetHighlight(false, Palette.BloodLight);
        }

        public void Bind(CardInstance card)
        {
            Card = card;
            Intent = null;
            Refresh();
        }

        public void BindIntent(EnemyIntent intent)
        {
            Intent = intent;
            Card = null;
            var m = intent.move;
            if (nameText) nameText.text = m.moveName;
            if (arcanaText) arcanaText.text = m.arcana;
            if (typeText) typeText.text = "LA DÉPRESSION";
            if (costOrb) costOrb.SetActive(false);
            SetFrame(Palette.BloodDark, m.theme);
            RefreshIntent();
        }

        void RefreshIntent()
        {
            var gm = GameManager.Instance;
            if (Intent == null || rulesText == null) return;
            rulesText.text = gm ? Intent.Describe(gm.Enemy, gm.Player) : "";
        }

        public void Refresh()
        {
            if (Intent != null) { RefreshIntent(); return; }
            if (Card == null) return;
            var d = Card.data;
            var gm = GameManager.Instance;
            if (nameText) nameText.text = Card.Name;
            if (arcanaText) arcanaText.text = d.arcana;
            if (typeText) typeText.text = CardData.TypeLabel(d.type);
            if (costOrb) costOrb.SetActive(!Card.Unplayable);
            if (costText) costText.text = Card.Cost.ToString();
            if (rulesText) rulesText.text = gm && gm.Player != null && gm.Player.hand.Contains(Card)
                ? Card.Describe(gm.Player, gm.Enemy) : Card.Describe();
            SetFrame(Palette.ForType(d.type), d.theme);
        }

        void SetFrame(Color typeColor, CardTheme theme)
        {
            if (frameRenderer)
            {
                frameRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color.Lerp(typeColor, Color.white, 0.25f));
                frameRenderer.SetPropertyBlock(_mpb);
            }
            if (artRenderer && artLibrary)
            {
                var tex = artLibrary.Get(theme);
                artRenderer.GetPropertyBlock(_mpb);
                if (tex) _mpb.SetTexture(BaseMapId, tex);
                _mpb.SetColor(BaseColorId, Color.white);
                artRenderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Indique si la carte est jouable (coût en vert / rouge).</summary>
        public void SetAffordable(bool affordable)
        {
            if (costText) costText.color = affordable ? Palette.Bone : Palette.BloodLight;
        }

        public void SetTarget(Vector3 localPos, Quaternion localRot, Vector3 localScale, bool instant = false)
        {
            _targetPos = localPos;
            _targetRot = localRot;
            _targetScale = localScale;
            if (instant)
            {
                transform.localPosition = localPos;
                transform.localRotation = localRot;
                transform.localScale = localScale;
            }
        }

        public void SetHighlight(bool on, Color color)
        {
            if (!highlight) return;
            if (highlight.activeSelf != on) highlight.SetActive(on);
            if (on && highlightRenderer)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                highlightRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, color);
                _mpb.SetColor(EmissionColorId, color * 2f);
                highlightRenderer.SetPropertyBlock(_mpb);
            }
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-moveSharpness * Time.unscaledDeltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, _targetPos + _shakeOffset, k);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRot, k);
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale * (1f + _punch), k);

            // Les valeurs affichées suivent la Force, la Faiblesse, etc.
            var gm = GameManager.Instance;
            if (gm && gm.Player != null && FaceUp)
            {
                int sig = gm.Player.strength * 131 + gm.Player.weak * 17 + gm.Enemy.vulnerable * 7 + gm.Enemy.strength * 3 + gm.Player.vulnerable * 5 + (gm.Player.IsLowLucidity ? 1 : 0) + gm.Player.block * 1009;
                if (sig != _lastSignature) { _lastSignature = sig; Refresh(); }
            }
        }

        public IEnumerator ShakeRoutine(float duration, float amplitude)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = amplitude * (1f - t / duration);
                _shakeOffset = new Vector3(Random.Range(-a, a), Random.Range(-a, a), Random.Range(-a, a) * 0.3f);
                yield return null;
            }
            _shakeOffset = Vector3.zero;
        }

        public void DestroyAfter(float delay) => Destroy(gameObject, delay);

        BloodEmitter _bleed;

        /// <summary>Carte sélectionnée : du sang goutte de son bord inférieur jusqu'à la table.</summary>
        public void SetBleeding(bool on)
        {
            if (on && !_bleed)
            {
                _bleed = gameObject.AddComponent<BloodEmitter>();
                _bleed.source = transform;
                _bleed.localOffset = new Vector3(Random.Range(-0.3f, 0.3f), -0.74f, 0f);
                _bleed.rate = 3.5f;
                _bleed.jitter = 0.002f;
                _bleed.dropSize = 0.9f;
                _bleed.Burst(4);
            }
            else if (!on && _bleed)
            {
                Destroy(_bleed);
                _bleed = null;
            }
        }

        /// <summary>Petit "saut" d'échelle (sélection, révélation).</summary>
        public void Punch(float amount = 0.25f) => _punch = Mathf.Max(_punch, amount);
        float _punch;
    }
}
