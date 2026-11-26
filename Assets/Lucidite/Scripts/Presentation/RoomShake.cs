using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Fait trembler toute la pièce comme si elle était en train d'être détruite :
    /// secousse de la racine "Environment", objets qui s'entrechoquent et restent de travers,
    /// gravats qui tombent du plafond (et restent au sol), fissures qui apparaissent sur les murs et le plafond,
    /// la lampe qui part en balancier. Tout reste en place jusqu'à la fin de la partie.
    /// </summary>
    public class RoomShake : MonoBehaviour
    {
        public static RoomShake Instance { get; private set; }

        [Header("Secousse globale")]
        public float maxOffset = 0.045f;
        public float maxAngle = 1.8f;
        public float decay = 1.1f;
        public float frequency = 24f;
        public HangingLamp lamp;

        [Header("Destruction")]
        public Material[] debrisMaterials;
        public Vector3 ceilingCenter = new Vector3(0, 2.62f, 0.2f);
        public Vector2 ceilingHalfSize = new Vector2(1.9f, 1.9f);
        public int maxDebris = 140;
        [Tooltip("Fissures cachées, révélées au fil des coups.")]
        public List<GameObject> cracks = new List<GameObject>();
        [Tooltip("Objets qui s'entrechoquent (remplis automatiquement si vide).")]
        public List<Transform> rattles = new List<Transform>();

        Vector3 _basePos;
        Quaternion _baseRot;
        float _trauma;
        float _seed;
        float _damageAccum;
        int _cracksShown;
        readonly List<GameObject> _debris = new List<GameObject>();

        class RattleState { public Transform t; public Vector3 pos; public Quaternion rot; public Vector3 drift; public float energy; public float seed; }
        readonly List<RattleState> _rattle = new List<RattleState>();

        void Awake()
        {
            Instance = this;
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
            _seed = Random.value * 100f;
            foreach (var c in cracks) if (c) c.SetActive(false);
            if (rattles.Count == 0) CollectRattles();
            foreach (var t in rattles)
                if (t) _rattle.Add(new RattleState { t = t, pos = t.localPosition, rot = t.localRotation, seed = Random.value * 50f });
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void CollectRattles()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                var n = r.name;
                if (n.Contains("Wall") || n.Contains("Floor") || n.Contains("Ceiling") || n.Contains("Wainscot") || n.Contains("Trims")
                    || n == "Top" || n == "Legs" || n == "Apron" || n.Contains("Board") || r.GetComponentInParent<CardView>() || r.GetComponentInParent<Candle>()
                    || r.GetComponentInParent<OpponentView>() || r.GetComponentInParent<HangingLamp>() || n.Contains("BloodPuddle"))
                    continue;
                if (r.bounds.size.magnitude > 2.2f) continue;
                // Maillages regroupés (pivot loin de l'objet) : les faire pivoter les décrocherait des murs
                if (Vector3.Distance(r.bounds.center, r.transform.position) > 0.35f) continue;
                rattles.Add(r.transform);
            }
        }

        /// <summary>
        /// amount ~ 0..1. destructive = false pour une petite secousse (carte plaquée) sans gravats ni fissures.
        /// </summary>
        public void Shake(float amount, bool destructive = true)
        {
            _trauma = Mathf.Clamp01(_trauma + amount * CameraRig.ShakeMultiplier);
            if (lamp) { lamp.Impulse(30f * amount); lamp.FlickerBurst(0.3f + amount * 0.6f); }
            foreach (var r in _rattle) r.energy = Mathf.Min(1.5f, r.energy + amount * Random.Range(0.6f, 1.4f));
            if (!destructive || amount < 0.25f) return;

            // Les objets restent un peu de travers après chaque gros coup
            foreach (var r in _rattle)
                if (Random.value < amount * 0.5f)
                    r.drift = Vector3.ClampMagnitude(r.drift + Random.insideUnitSphere * amount * 4f, 7f);

            SpawnDebris(Mathf.RoundToInt(4 + amount * 14));
            _damageAccum += amount;
            while (_damageAccum >= 0.8f && _cracksShown < cracks.Count)
            {
                _damageAccum -= 0.8f;
                var c = cracks[_cracksShown++];
                if (c) c.SetActive(true);
            }
            AudioManager.Play(Sfx.Rumble, 0.5f + amount * 0.5f);
        }

        void SpawnDebris(int count)
        {
            if (debrisMaterials == null || debrisMaterials.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                if (_debris.Count >= maxDebris)
                {
                    if (_debris[0]) Destroy(_debris[0]);
                    _debris.RemoveAt(0);
                }
                var g = GameObject.CreatePrimitive(Random.value < 0.7f ? PrimitiveType.Cube : PrimitiveType.Sphere);
                g.name = "Debris";
                g.layer = 2; // Ignore Raycast : ne bloque pas le survol des cartes
                g.transform.SetParent(transform, true);
                g.transform.position = transform.TransformPoint(ceilingCenter + new Vector3(Random.Range(-ceilingHalfSize.x, ceilingHalfSize.x), 0f, Random.Range(-ceilingHalfSize.y, ceilingHalfSize.y)));
                g.transform.rotation = Random.rotation;
                float s = Random.Range(0.012f, 0.06f);
                g.transform.localScale = new Vector3(s, s * Random.Range(0.3f, 1f), s * Random.Range(0.5f, 1.3f));
                var mr = g.GetComponent<MeshRenderer>();
                mr.sharedMaterial = debrisMaterials[Random.Range(0, debrisMaterials.Length)];
                var rb = g.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.linearDamping = 0.2f;
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                _debris.Add(g);
            }
        }

        void LateUpdate()
        {
            float s = _trauma * _trauma;
            float t = Time.time * frequency;
            if (_trauma > 0f)
            {
                float nx = Mathf.PerlinNoise(_seed, t) * 2f - 1f;
                float ny = Mathf.PerlinNoise(_seed + 3f, t) * 2f - 1f;
                float nz = Mathf.PerlinNoise(_seed + 7f, t) * 2f - 1f;
                transform.localPosition = _basePos + new Vector3(nx, ny * 0.6f, nz) * maxOffset * s;
                transform.localRotation = _baseRot * Quaternion.Euler(ny * maxAngle * s, nz * maxAngle * s * 0.5f, nx * maxAngle * s);
                _trauma = Mathf.Max(0f, _trauma - decay * Time.deltaTime);
            }
            else
            {
                transform.localPosition = _basePos;
                transform.localRotation = _baseRot;
            }

            // Objets qui vibrent et s'entrechoquent
            foreach (var r in _rattle)
            {
                if (!r.t) continue;
                r.energy = Mathf.MoveTowards(r.energy, 0f, Time.deltaTime * 1.6f);
                float e = r.energy * r.energy;
                float a = Mathf.PerlinNoise(r.seed, t * 1.7f) * 2f - 1f;
                float b = Mathf.PerlinNoise(r.seed + 9f, t * 1.7f) * 2f - 1f;
                r.t.localPosition = r.pos + new Vector3(a, Mathf.Abs(b) * 0.5f, b) * 0.008f * e;
                r.t.localRotation = r.rot * Quaternion.Euler(r.drift + new Vector3(a, b, a * b) * 4f * e);
            }
        }
    }
}
