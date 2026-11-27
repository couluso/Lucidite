using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Pilote la propriété _Damage du shader PSX sur tous les renderers d'un personnage :
    /// les ecchymoses, coupures et coulures de sang de sa texture apparaissent au fil des dégâts.
    /// Chaque coup laisse une marque durable (d'autant plus profonde que le coup est fort) qui reste
    /// d'une manche à l'autre ; la Lucidité manquante s'y ajoute pendant la manche.
    /// </summary>
    public class DamageVisuals : MonoBehaviour
    {
        public Side side;
        [Tooltip("Vitesse d'apparition des marques.")]
        public float speed = 1.5f;
        [Tooltip("Marques minimales (déjà là au début de la manche).")]
        [Range(0f, 1f)] public float baseline = 0.08f;
        [Tooltip("Marque durable laissée par un coup, par fraction de la vie max perdue (doublée pour un coup très fort).")]
        public float scarPerHp = 0.4f;
        [Tooltip("Plafond des marques durables.")]
        [Range(0f, 1f)] public float maxScar = 0.8f;
        [Tooltip("Poids de la vie manquante dans les marques visibles pendant la manche.")]
        public float missingWeight = 0.8f;

        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        float _current, _target, _scar, _rush = 1f;

        /// <summary>Niveau de blessures affiché (0 à 1) : sert aussi à doser les saignements.</summary>
        public float Level => _current;
        static readonly int DamageId = Shader.PropertyToID("_Damage");

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _current = _target = baseline;
            Apply();
        }

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm) { gm.OnStatsChanged += HandleStats; gm.OnDamaged += HandleDamaged; }
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm) { gm.OnStatsChanged -= HandleStats; gm.OnDamaged -= HandleDamaged; }
        }

        void HandleDamaged(Side s, int lost, int blocked)
        {
            if (s != side || lost <= 0) return;
            var gm = GameManager.Instance;
            var f = gm ? (side == Side.Player ? gm.Player : gm.Enemy) : null;
            if (f == null || f.maxHp <= 0) return;
            float power = FeedbackDirector.HitPower(lost);
            _scar = Mathf.Min(maxScar, _scar + lost / (float)f.maxHp * scarPerHp * (1f + power));
            _rush = 1f + power * 4f; // un gros coup marque presque instantanément
            HandleStats();
        }

        void HandleStats()
        {
            var gm = GameManager.Instance;
            var f = gm ? (side == Side.Player ? gm.Player : gm.Enemy) : null;
            if (f == null || f.maxHp <= 0) return;
            float missing = 1f - f.hp / (float)f.maxHp;
            // Les manches perdues laissent aussi des traces
            float candles = f.maxCandles > 0 ? (f.maxCandles - f.candles) / (float)f.maxCandles * 0.15f : 0f;
            _target = Mathf.Clamp01(baseline + _scar + candles + missing * missingWeight);
        }

        void LateUpdate()
        {
            _rush = Mathf.MoveTowards(_rush, 1f, Time.deltaTime * 2f);
            if (Mathf.Abs(_current - _target) < 0.001f) return;
            _current = Mathf.MoveTowards(_current, _target, Time.deltaTime * speed * (_target > _current ? _rush : 0.3f));
            Apply();
        }

        void Apply()
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(DamageId, _current);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
