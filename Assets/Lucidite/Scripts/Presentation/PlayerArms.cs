using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Bras de l'étudiante en vue à la première personne.
    /// Chaque coup encaissé fait apparaître une marque et peut faire couler un filet de sang jusqu'à la table.
    /// Les mains tremblent de plus en plus quand la Lucidité baisse.
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
        Vector3 _tremorBase;
        readonly List<BloodDrip> _drips = new List<BloodDrip>();

        void Awake()
        {
            foreach (var w in wounds) if (w) w.SetActive(false);
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
            // Les blessures ouvertes saignent d'autant plus que la Lucidité est basse
            foreach (var e in _emitters) if (e) e.rate = missing < 0.05f ? 0f : 0.3f + missing * 2.5f;
        }

        void HandleDamaged(Side side, int lost, int blocked)
        {
            if (side != Side.Player || lost <= 0) return;
            AddWound(lost >= bleedThreshold);
        }

        readonly List<BloodEmitter> _emitters = new List<BloodEmitter>();

        public void AddWound(bool bleed)
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
            if (bleed) Bleed(index);
            else if (_emitters.Count > 0) _emitters[Random.Range(0, _emitters.Count)].Burst(3);
        }

        void Bleed(int index)
        {
            // Gros coup : giclée de gouttes depuis la blessure
            if (index >= 0 && index < bleedPoints.Count && bleedPoints[index] && BloodSystem.Instance)
                BloodSystem.Instance.Splatter(bleedPoints[index].position, 8, 0.35f);
            foreach (var e in _emitters) if (e) e.Burst(2);
        }

        void LateUpdate()
        {
            if (!tremorRoot) return;
            float t = Time.time * 18f;
            tremorRoot.localPosition = _tremorBase + new Vector3(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * _tremor,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * _tremor, 0f);
        }
    }
}
