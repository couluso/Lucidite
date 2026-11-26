using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Carte physique 3D (prefab Card). Affiche une carte de la joueuse ou une intention de la Dépression.
    /// Se déplace en douceur vers une cible locale.
    /// Design : fond noir, contours de la couleur du type, grande icône centrale, coût (haut gauche),
    /// valeur (bas droite) et petites icônes de bonus (bas gauche).
    /// </summary>
    public class CardView : MonoBehaviour
    {
        [Header("Références (prefab)")]
        public CardArtLibrary artLibrary;
        public Renderer frameRenderer;
        [Tooltip("Grande icône centrale.")]
        public Renderer artRenderer;
        public Renderer backRenderer;
        public GameObject highlight;
        public Renderer highlightRenderer;
        public GameObject costOrb;
        public Renderer costOrbRenderer;
        public TMP_Text costText;
        public GameObject valueOrb;
        public Renderer valueOrbRenderer;
        public TMP_Text valueText;
        [Tooltip("Petites icônes de bonus, de gauche à droite.")]
        public Renderer[] statRenderers;
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
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int DamageId = Shader.PropertyToID("_Damage");
        static readonly int DamageMapId = Shader.PropertyToID("_DamageMap");
        static readonly int SuitMapId = Shader.PropertyToID("_SuitMap");
        static readonly int SuitMixId = Shader.PropertyToID("_SuitMix");
        static readonly List<CardView> s_all = new List<CardView>();
        readonly List<CardStat> _stats = new List<CardStat>();
        Color _typeColor = Palette.TypeAttack;
        bool _affordable = true;

        void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); }
        void OnDisable() { s_all.Remove(this); }

        /// <summary>
        /// Une goutte de sang atterrit en worldPos : si elle tombe sur une carte de la joueuse posée à plat
        /// (sur la table), la carte la boit. Renvoie vrai si une carte l'a reçue (pas de flaque dessous).
        /// </summary>
        public static bool AbsorbBlood(Vector3 worldPos, float amount)
        {
            CardView best = null;
            float bestH = float.MinValue;
            foreach (var v in s_all)
            {
                if (!v || v.Card == null) continue;
                var t = v.transform;
                if (Mathf.Abs(Vector3.Dot(t.forward, Vector3.up)) < 0.75f) continue; // seulement les cartes à plat
                var lp = t.InverseTransformPoint(worldPos);
                if (Mathf.Abs(lp.x) > 0.5f || Mathf.Abs(lp.y) > 0.75f) continue;
                if (Mathf.Abs(Vector3.Dot(worldPos - t.position, t.forward)) > 0.03f) continue;
                if (t.position.y > bestH) { bestH = t.position.y; best = v; } // la carte du dessus de la pile
            }
            if (!best) return false;
            best.AddBlood(amount);
            return true;
        }

        /// <summary>Cartes de la joueuse posées à plat sur la table (celles de la Dépression n'ont pas de CardInstance).</summary>
        public static void PlayerTableCards(List<CardView> into)
        {
            into.Clear();
            foreach (var v in s_all)
                if (v && v.Card != null && Mathf.Abs(Vector3.Dot(v.transform.forward, Vector3.up)) >= 0.75f)
                    into.Add(v);
        }

        /// <summary>Une goutte visée sur cette carte l'a touchée.</summary>
        public void Stain(float amount) => AddBlood(amount);

        void AddBlood(float amount)
        {
            if (Card == null) return;
            var gm0 = GameManager.Instance;
            if (gm0 && gm0.Player != null) amount *= 1f + gm0.Player.RelicValue(ItemEffect.SangCartes) / 100f; // relique « Calice »
            bool scarlet = Card.AddBlood(amount);
            ApplyBlood();
            if (!scarlet) return;
            // Totalement recouverte : la carte devient Écarlate
            Punch(0.35f);
            AudioManager.Play(Sfx.BloodDrop, 0.9f);
            var gm = GameManager.Instance;
            if (gm) gm.Message($"« {Card.data.cardName} » est couverte de sang : elle devient Écarlate !");
            Refresh();
        }

        void ApplyBlood()
        {
            if (!frameRenderer) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            frameRenderer.GetPropertyBlock(_mpb);
            float b = Card != null ? Card.blood : 0f;
            _mpb.SetFloat(DamageId, b);
            // Classe : symbole et motif par-dessus la face
            var suit = artLibrary && Card != null ? artLibrary.SuitOverlay(Card.suit) : null;
            _mpb.SetFloat(SuitMixId, suit ? 1f : 0f);
            if (suit) _mpb.SetTexture(SuitMapId, suit);
            var map = artLibrary && Card != null ? artLibrary.CardBlood(Card.uid) : null;
            if (map) _mpb.SetTexture(DamageMapId, map);
            frameRenderer.SetPropertyBlock(_mpb);
        }

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
            _affordable = true;
            RefreshIntent();
        }

        void RefreshIntent()
        {
            var gm = GameManager.Instance;
            if (Intent == null) return;
            var m = Intent.move;
            if (rulesText) rulesText.text = gm ? Intent.Describe(gm.Enemy, gm.Player) : "";
            if (costOrb) costOrb.SetActive(true);
            if (costText) costText.text = m.cost.ToString();
            CardFace.Stats(Intent, _stats);
            SetFrame(Palette.TypeEnemy, m.face ? m.face : artLibrary ? artLibrary.faceEnemy : null, m.icon,
                CardFace.Value(Intent, gm ? gm.Enemy : null, gm ? gm.Player : null));
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
            bool inHand = gm && gm.Player != null && gm.Player.hand.Contains(Card);
            if (rulesText) rulesText.text = inHand ? Card.Describe(gm.Player, gm.Enemy) : Card.Describe();
            CardFace.Stats(Card, _stats);
            SetFrame(Palette.ForType(d.type), Card.Face ? Card.Face : artLibrary ? artLibrary.Face(d.type) : null, d.icon,
                inHand ? CardFace.Value(Card, gm.Player, gm.Enemy) : CardFace.Value(Card));
        }

        void SetFrame(Color typeColor, Texture2D face, Texture2D icon, string value)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _typeColor = typeColor;
            if (frameRenderer && face)
            {
                frameRenderer.GetPropertyBlock(_mpb);
                _mpb.SetTexture(BaseMapId, face);
                _mpb.SetTexture(EmissionMapId, face);
                _mpb.SetColor(BaseColorId, Color.white);
                frameRenderer.SetPropertyBlock(_mpb);
                ApplyBlood();
            }
            if (artRenderer)
            {
                var tex = artLibrary ? artLibrary.Icon(icon) : icon;
                artRenderer.GetPropertyBlock(_mpb);
                if (tex) { _mpb.SetTexture(BaseMapId, tex); _mpb.SetTexture(EmissionMapId, tex); }
                _mpb.SetColor(BaseColorId, typeColor);
                _mpb.SetColor(EmissionColorId, typeColor * 0.6f);
                artRenderer.SetPropertyBlock(_mpb);
            }
            bool hasValue = !string.IsNullOrEmpty(value);
            if (valueOrb) valueOrb.SetActive(hasValue);
            if (valueText) { valueText.text = value; valueText.color = Palette.OnType(typeColor); }
            Tint(valueOrbRenderer, typeColor);
            ApplyCostColor();

            if (statRenderers == null) return;
            for (int i = 0; i < statRenderers.Length; i++)
            {
                var r = statRenderers[i];
                if (!r) continue;
                var tex = i < _stats.Count && artLibrary ? artLibrary.Stat(_stats[i]) : null;
                r.gameObject.SetActive(tex);
                if (!tex) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetTexture(BaseMapId, tex);
                _mpb.SetTexture(EmissionMapId, tex);
                r.SetPropertyBlock(_mpb);
            }
        }

        void Tint(Renderer r, Color c)
        {
            if (!r) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
        }

        void ApplyCostColor()
        {
            // Pas assez d'énergie : le rond du coût s'éteint
            Tint(costOrbRenderer, _affordable ? _typeColor : Color.Lerp(_typeColor, Palette.AshDark, 0.75f));
            if (costText) costText.color = _affordable ? Palette.OnType(_typeColor) : Palette.BloodLight;
        }

        /// <summary>Indique si la carte est jouable (rond du coût allumé / éteint).</summary>
        public void SetAffordable(bool affordable)
        {
            if (affordable == _affordable) return;
            _affordable = affordable;
            ApplyCostColor();
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

        /// <summary>Contour lumineux. glow > 2 : contour en HDR (le bloom le fait briller).</summary>
        public void SetHighlight(bool on, Color color, float glow = 2f)
        {
            if (!highlight) return;
            if (highlight.activeSelf != on) highlight.SetActive(on);
            if (on && highlightRenderer)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                highlightRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, color * Mathf.Max(1f, glow * 0.5f));
                _mpb.SetColor(EmissionColorId, color * glow);
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

        /// <summary>Petit "saut" d'échelle (sélection, révélation).</summary>
        public void Punch(float amount = 0.25f) => _punch = Mathf.Max(_punch, amount);
        float _punch;
    }
}
