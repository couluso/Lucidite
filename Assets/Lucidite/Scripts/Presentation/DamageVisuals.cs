using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Pilote la propriété _Damage du shader PSX sur tous les renderers d'un personnage :
    /// les ecchymoses, coupures et coulures de sang de sa texture apparaissent au fil des dégâts.
    /// </summary>
    public class DamageVisuals : MonoBehaviour
    {
        public Side side;
        [Tooltip("Vitesse d'apparition des marques.")]
        public float speed = 1.5f;
        [Tooltip("Marques minimales (déjà là au début de la manche).")]
        [Range(0f, 1f)] public float baseline = 0.08f;

        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        float _current, _target;
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
            if (gm) gm.OnStatsChanged += HandleStats;
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm) gm.OnStatsChanged -= HandleStats;
        }

        void HandleStats()
        {
            var gm = GameManager.Instance;
            var f = gm ? (side == Side.Player ? gm.Player : gm.Enemy) : null;
            if (f == null || f.maxHp <= 0) return;
            float missing = 1f - f.hp / (float)f.maxHp;
            // Les manches perdues laissent des traces : le minimum remonte avec les bougies éteintes
            float scars = f.maxCandles > 0 ? (f.maxCandles - f.candles) / (float)f.maxCandles * 0.35f : 0f;
            _target = Mathf.Clamp01(Mathf.Max(baseline + scars, missing * 1.05f));
        }

        void LateUpdate()
        {
            if (Mathf.Abs(_current - _target) < 0.001f) return;
            _current = Mathf.MoveTowards(_current, _target, Time.deltaTime * speed * (_target > _current ? 1f : 0.3f));
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
