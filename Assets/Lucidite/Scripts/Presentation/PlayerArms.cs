using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Bras de l'étudiante en vue à la première personne.
    /// Chaque coup encaissé fait apparaître une marque, fait couler du sang et projette une giclée vers l'avant
    /// sur la table, d'autant plus que le coup est fort. Les blessures restent ouvertes d'une manche à l'autre
    /// (leur saignement suit DamageVisuals). Les mains tremblent de plus en plus quand la Lucidité baisse.
    /// </summary>
    public class PlayerArms : MonoBehaviour
    {
        [Tooltip("Marques (désactivées au départ), dans l'ordre d'apparition.")]
        public List<GameObject> wounds = new List<GameObject>();
        [Tooltip("Points d'où le sang s'écoule, un par marque (même ordre).")]
        public List<Transform> bleedPoints = new List<Transform>();
        public BloodDrip bloodDripPrefab;
        public float tableHeight = 0.78f;
        public int maxDrips = 6;
        [Tooltip("Lucidité perdue d'un coup à partir de laquelle le sang coule.")]
        public int bleedThreshold = 6;

        [Header("Tremblement")]
        public Transform tremorRoot;
        public float maxTremor = 0.004f;

        int _shown;
        float _tremor;
        DamageVisuals _dv;
        Vector3 _tremorBase;
        readonly List<BloodDrip> _drips = new List<BloodDrip>();

        void Awake()
        {
            foreach (var w in wounds) if (w) w.SetActive(false);
            _dv = GetComponent<DamageVisuals>();
            if (tremorRoot) _tremorBase = tremorRoot.localPosition;
        }

        void OnEnable() => Subscribe();
        void Start() => Subscribe();

        void OnDisable()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnDamaged -= HandleDamaged;
            gm.OnStatsChanged -= HandleStats;
        }

        void Subscribe()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnDamaged -= HandleDamaged; gm.OnDamaged += HandleDamaged;
            gm.OnStatsChanged -= HandleStats; gm.OnStatsChanged += HandleStats;
        }

        void HandleStats()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.Player.maxHp <= 0) return;
            float missing = 1f - gm.Player.hp / (float)gm.Player.maxHp;
            _tremor = missing * maxTremor;
        }

        void HandleDamaged(Side side, int lost, int blocked)
        {
            if (side != Side.Player || lost <= 0) return;
            float power = FeedbackDirector.HitPower(lost);
            AddWound(lost >= bleedThreshold, power);
            if (power > 0.6f) AddWound(true, power); // un coup violent ouvre une seconde plaie
            // Giclée vers l'avant, sur la table (depuis la poitrine, juste sous la caméra)
            var cam = Camera.main;
            if (cam && BloodSystem.Instance)
            {
                var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                BloodSystem.Instance.Spray(cam.transform.position + fwd * 0.06f - Vector3.up * 0.28f, fwd, power);
            }
        }

        readonly List<BloodEmitter> _emitters = new List<BloodEmitter>();

        public void AddWound(bool bleed, float power = 0.3f)
        {
            int index;
            // Avec les bras importés il n'y a pas de décalcomanies de blessure : les points de saignement suffisent
            if (_shown < Mathf.Max(wounds.Count, bleedPoints.Count))
            {
                index = _shown++;
                if (index < wounds.Count && wounds[index]) wounds[index].SetActive(true);
                // Chaque nouvelle blessure se met à goutter
                if (index < bleedPoints.Count && bleedPoints[index])
                {
                    var e = gameObject.AddComponent<BloodEmitter>();
                    e.source = bleedPoints[index];
                    e.rate = 0.5f;
                    e.jitter = 0.003f;
                    e.dropSize = 0.9f;
                    _emitters.Add(e);
                }
            }
            else index = Random.Range(0, Mathf.Max(1, wounds.Count));
            if (bleed) Bleed(index, power);
            else if (_emitters.Count > 0) _emitters[Random.Range(0, _emitters.Count)].Burst(3 + Mathf.RoundToInt(power * 4f));
        }

        void Bleed(int index, float power)
        {
            // Gros coup : giclée de gouttes depuis la blessure (plus ou moins selon ton saignement du moment)
            var gm = GameManager.Instance;
            float bleed = gm ? gm.BleedMultiplier(Side.Player) : 1f;
            if (index >= 0 && index < bleedPoints.Count && bleedPoints[index] && BloodSystem.Instance)
                BloodSystem.Instance.Splatter(bleedPoints[index].position, Mathf.RoundToInt((5 + power * 16f) * bleed), 0.3f + power * 0.4f);
            foreach (var e in _emitters) if (e) e.Burst(Mathf.RoundToInt((2 + power * 4f) * bleed));
        }

        void LateUpdate()
        {
            // Les blessures ouvertes saignent selon leur gravité, y compris d'une manche à l'autre
            float level = _dv ? _dv.Level : 0f;
            foreach (var e in _emitters) if (e) e.rate = level < 0.1f ? 0f : 0.3f + level * 3.5f;
            if (!tremorRoot) return;
            float t = Time.time * 18f;
            tremorRoot.localPosition = _tremorBase + new Vector3(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * _tremor,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * _tremor, 0f);
        }
    }
}
