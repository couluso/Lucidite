using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Gouttes de sang qui tombent (gravité) et laissent de petites flaques sur la table ou le sol.
    /// Les flaques restent jusqu'à la manche suivante. Utilisé par les cartes sélectionnées,
    /// les blessures de la joueuse et celles de la Dépression.
    /// </summary>
    public class BloodSystem : MonoBehaviour
    {
        public static BloodSystem Instance { get; private set; }

        public Material dropMaterial;
        public Material puddleMaterial;
        [Tooltip("Parent des flaques (racine de la pièce, pour qu'elles tremblent avec elle).")]
        public Transform puddleRoot;

        [Header("Surfaces")]
        public float tableY = 0.7812f;
        public Vector2 tableHalfSize = new Vector2(0.63f, 0.43f);
        public float floorY = 0.003f;

        [Header("Limites")]
        public int maxDrops = 80;
        public int maxPuddles = 350;
        public float maxPuddleRadius = 0.055f;

        class Drop { public Transform t; public Vector3 vel; public float size; }
        class Puddle { public Transform t; public float radius, target, grow; }

        readonly List<Drop> _drops = new List<Drop>();
        readonly Stack<Transform> _dropPool = new Stack<Transform>();
        readonly List<Puddle> _puddles = new List<Puddle>();
        Mesh _discMesh, _dropMesh;

        void Awake()
        {
            Instance = this;
            _discMesh = BuildDisc(12);
            _dropMesh = BuildDropMesh();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (GameManager.Instance) GameManager.Instance.OnRoundStarted += _ => ClearPuddles();
        }

        // ------------------------------------------------------------------ API

        public void SpawnDrop(Vector3 worldPos, float size = 1f, Vector3 initialVelocity = default)
        {
            if (_drops.Count >= maxDrops) return;
            var t = _dropPool.Count > 0 ? _dropPool.Pop() : CreateDropObject();
            t.gameObject.SetActive(true);
            t.position = worldPos;
            float s = 0.004f * size * Random.Range(0.8f, 1.25f);
            t.localScale = new Vector3(s, s * 1.6f, s);
            _drops.Add(new Drop { t = t, vel = initialVelocity, size = size });
        }

        /// <summary>Éclaboussure : plusieurs gouttes projetées depuis un point.</summary>
        public void Splatter(Vector3 worldPos, int count, float speed = 0.6f)
        {
            for (int i = 0; i < count; i++)
            {
                var v = Random.insideUnitSphere * speed;
                v.y = Mathf.Abs(v.y) * 0.6f;
                SpawnDrop(worldPos, Random.Range(0.7f, 1.4f), v);
            }
        }

        public void ClearPuddles()
        {
            foreach (var p in _puddles) if (p.t) Destroy(p.t.gameObject);
            _puddles.Clear();
        }

        // ------------------------------------------------------------------ Simulation

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                var p = d.t.position;
                d.vel += Physics.gravity * dt;
                var next = p + d.vel * dt;
                bool overTable = Mathf.Abs(next.x) < tableHalfSize.x && Mathf.Abs(next.z) < tableHalfSize.y;
                float surface = overTable && p.y >= tableY - 0.01f ? tableY : floorY;
                if (next.y <= surface)
                {
                    AddPuddle(new Vector3(next.x, surface, next.z), 0.004f + 0.004f * d.size);
                    Recycle(d, i);
                    continue;
                }
                d.t.position = next;
                if (d.vel.sqrMagnitude > 0.0001f) d.t.rotation = Quaternion.FromToRotation(Vector3.down, d.vel.normalized);
                if (next.y < -1f) Recycle(d, i);
            }

            for (int i = 0; i < _puddles.Count; i++)
            {
                var pu = _puddles[i];
                if (!pu.t || pu.radius >= pu.target) continue;
                pu.radius = Mathf.MoveTowards(pu.radius, pu.target, pu.grow * dt);
                pu.t.localScale = new Vector3(pu.radius * 2f, 1f, pu.radius * 2.2f);
            }
        }

        void Recycle(Drop d, int index)
        {
            d.t.gameObject.SetActive(false);
            _dropPool.Push(d.t);
            _drops.RemoveAt(index);
        }

        void AddPuddle(Vector3 worldPos, float radius)
        {
            // Fusionne avec une flaque proche : elle s'étale au lieu d'en créer une nouvelle
            foreach (var p in _puddles)
            {
                if (!p.t) continue;
                var d = p.t.position - worldPos; d.y = 0;
                if (d.magnitude < p.target * 0.9f)
                {
                    p.target = Mathf.Min(maxPuddleRadius, Mathf.Sqrt(p.target * p.target + radius * radius * 0.6f));
                    p.grow = 0.02f;
                    return;
                }
            }
            if (_puddles.Count >= maxPuddles)
            {
                if (_puddles[0].t) Destroy(_puddles[0].t.gameObject);
                _puddles.RemoveAt(0);
            }
            var g = new GameObject("BloodPuddle");
            g.transform.SetParent(puddleRoot, true);
            g.transform.position = worldPos + Vector3.up * Random.Range(0.0002f, 0.0008f);
            g.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            g.AddComponent<MeshFilter>().sharedMesh = _discMesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = puddleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.localScale = Vector3.one * 0.001f;
            _puddles.Add(new Puddle { t = g.transform, radius = 0.0005f, target = radius, grow = 0.08f });
        }

        Transform CreateDropObject()
        {
            var g = new GameObject("BloodDrop");
            g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = _dropMesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = dropMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        // ------------------------------------------------------------------ Maillages

        static Mesh BuildDisc(int seg)
        {
            var m = new Mesh { name = "BloodDisc" };
            var v = new Vector3[seg + 1];
            var uv = new Vector2[seg + 1];
            var t = new int[seg * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float r = 0.5f * (0.82f + 0.18f * Mathf.PerlinNoise(i * 0.9f, 3.1f)); // bord irrégulier
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                uv[i + 1] = new Vector2(0.5f + v[i + 1].x, 0.5f + v[i + 1].z);
                t[i * 3] = 0; t[i * 3 + 1] = (i + 1) % seg + 1; t[i * 3 + 2] = i + 1;
            }
            m.vertices = v; m.uv = uv; m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildDropMesh()
        {
            // Petite goutte : octaèdre étiré
            var m = new Mesh { name = "BloodDropMesh" };
            m.vertices = new[] { new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, -1, 0) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
