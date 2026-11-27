using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Gouttes de sang qui tombent (gravité) et laissent de petites flaques sur la table ou le sol.
    /// Les flaques restent toute la partie : la table se couvre de sang au fil des manches
    /// (les plus anciennes disparaissent au-delà de maxPuddles). Utilisé par les blessures et les coups
    /// de la joueuse et de la Dépression.
    /// </summary>
    public class BloodSystem : MonoBehaviour
    {
        public static BloodSystem Instance { get; private set; }

        public Material dropMaterial;
        public Material puddleMaterial;
        [Tooltip("Parent des flaques (racine de la pièce, pour qu'elles tremblent avec elle).")]
        public Transform puddleRoot;

        [Header("Surfaces (repère de puddleRoot : la pièce qui tremble)")]
        public float tableY = 0.7812f;
        public Vector2 tableHalfSize = new Vector2(0.63f, 0.43f);
        public float floorY = 0.003f;

        [Header("Limites")]
        public int maxDrops = 260;
        public int maxPuddles = 700;
        public float maxPuddleRadius = 0.055f;

        [Header("Cartes")]
        [Tooltip("Sang reçu par une carte posée sur la table pour chaque goutte qui la touche (1 = recouverte : Écarlate).")]
        public float cardBloodPerDrop = 0.03f;

        class Drop { public Transform t; public Vector3 vel; public float size; public CardView target; }
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

        /// <summary>
        /// Projections qui retombent sur une carte de la joueuse posée sur la table : chaque goutte suit un arc
        /// balistique jusqu'à un point au hasard de la carte, puis la tache (sang de la carte -> Écarlate).
        /// </summary>
        public void SprayOnto(Vector3 worldPos, CardView card, int count)
        {
            if (!card) return;
            for (int i = 0; i < count && _drops.Count < maxDrops; i++)
            {
                var target = card.transform.TransformPoint(new Vector3(Random.Range(-0.42f, 0.42f), Random.Range(-0.65f, 0.65f), 0f));
                var from = worldPos + Random.insideUnitSphere * 0.03f;
                float T = Random.Range(0.35f, 0.6f);
                var v = (target - from) / T - 0.5f * Physics.gravity * T;
                SpawnDrop(from, Random.Range(0.8f, 1.4f), v);
                _drops[_drops.Count - 1].target = card;
            }
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

        /// <summary>
        /// Giclée projetée vers l'avant (coup encaissé) : les gouttelettes partent en cône dans la direction donnée,
        /// retombent en arc et tachent la table. power (0 à 1) : nombre, vitesse et taille des gouttes.
        /// </summary>
        public void Spray(Vector3 worldPos, Vector3 direction, float power)
        {
            power = Mathf.Clamp01(power);
            var fwd = Vector3.ProjectOnPlane(direction, Vector3.up);
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            // Peu de gouttes pour un petit coup, beaucoup pour un coup violent
            int count = Mathf.RoundToInt(Mathf.Lerp(3f, 40f, power));
            // Éventail large (pas un cône) : chaque goutte part dans sa propre direction, l'ouverture s'élargit avec la puissance
            float maxYaw = Mathf.Lerp(55f, 80f, power);
            for (int i = 0; i < count; i++)
            {
                float yaw = Random.Range(-maxYaw, maxYaw);
                float pitch = Random.Range(8f, 55f);
                var dir = Quaternion.AngleAxis(yaw, Vector3.up) * fwd;
                dir = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, dir)) * dir;
                // Vitesses très dispersées : certaines gouttes tombent près, d'autres traversent la table
                float speed = Mathf.Lerp(1.1f, 2.8f, power) * Random.Range(0.35f, 1.1f);
                float size = Mathf.Lerp(0.9f, 1.7f, power) * Random.Range(0.6f, 1.3f);
                SpawnDrop(worldPos + Random.insideUnitSphere * 0.03f, size, dir * speed);
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
                // Surfaces testées dans le repère de la pièce : quand elle tremble, la table bouge avec elle
                var lp = Local(p);
                var ln = Local(next);
                bool overTable = Mathf.Abs(ln.x) < tableHalfSize.x && Mathf.Abs(ln.z) < tableHalfSize.y;
                float surface = overTable && lp.y >= tableY - 0.01f ? tableY : floorY;
                // Rayon de la goutte : elle s'arrête quand son bas touche la surface (pas de goutte à moitié dans le bois)
                float radius = d.t.localScale.y * 0.5f;
                if (ln.y - radius <= surface)
                {
                    // Une carte de la joueuse posée sur la table boit la goutte (elle se couvre de sang peu à peu)
                    if (d.target && d.target.isActiveAndEnabled && d.target.Card != null)
                    {
                        d.target.Stain(cardBloodPerDrop * d.size);
                        Recycle(d, i);
                        continue;
                    }
                    if (surface == tableY && CardView.AbsorbBlood(World(new Vector3(ln.x, surface, ln.z)), cardBloodPerDrop * d.size))
                    {
                        Recycle(d, i);
                        continue;
                    }
                    AddPuddle(new Vector3(ln.x, surface, ln.z), 0.004f + 0.004f * d.size);
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
            d.target = null;
            _dropPool.Push(d.t);
            _drops.RemoveAt(index);
        }

        Vector3 Local(Vector3 world) => puddleRoot ? puddleRoot.InverseTransformPoint(world) : world;
        Vector3 World(Vector3 local) => puddleRoot ? puddleRoot.TransformPoint(local) : local;

        /// <summary>
        /// Grande flaque qui s'étale lentement (corps effondré) : plusieurs lobes qui grandissent à des vitesses
        /// différentes autour de worldPos, posés sur la table ou le sol (selon onTable).
        /// </summary>
        public void Pool(Vector3 worldPos, float radius, float duration, bool onTable)
        {
            var lp = Local(worldPos);
            lp.y = onTable ? tableY : floorY;
            for (int i = 0; i < 6; i++)
            {
                float r = radius * (i == 0 ? 0.75f : Random.Range(0.35f, 0.6f));
                var off = i == 0 ? Vector3.zero : Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * radius * Random.Range(0.25f, 0.55f);
                var p = lp + new Vector3(off.x, 0f, off.z);
                if (onTable) { p.x = Mathf.Clamp(p.x, -tableHalfSize.x + r * 0.5f, tableHalfSize.x - r * 0.5f); p.z = Mathf.Clamp(p.z, -tableHalfSize.y + r * 0.5f, tableHalfSize.y - r * 0.5f); }
                // Assez haut pour recouvrir les feuilles posées sur la table (elles bombent jusqu'à ~5 mm)
                var g = NewPuddle(p + Vector3.up * ((onTable ? 0.0058f : 0.0035f) + i * 0.0003f));
                _puddles.Add(new Puddle { t = g.transform, radius = 0.002f, target = r, grow = r / (duration * Random.Range(0.7f, 1.2f)) });
            }
        }

        /// <summary>localPos : dans le repère de puddleRoot (la pièce), posé sur la surface.</summary>
        void AddPuddle(Vector3 localPos, float radius)
        {
            // Fusionne avec une flaque proche : elle s'étale au lieu d'en créer une nouvelle
            foreach (var p in _puddles)
            {
                if (!p.t) continue;
                var d = p.t.localPosition - localPos; d.y = 0;
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
            // Légèrement au-dessus du bois (sinon elle disparaît par endroits dans la table)
            var g = NewPuddle(localPos + Vector3.up * Random.Range(0.0012f, 0.0025f));
            _puddles.Add(new Puddle { t = g.transform, radius = 0.0005f, target = radius, grow = 0.08f });
        }

        GameObject NewPuddle(Vector3 localPos)
        {
            var g = new GameObject("BloodPuddle");
            g.transform.SetParent(puddleRoot, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            g.AddComponent<MeshFilter>().sharedMesh = _discMesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = puddleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.localScale = Vector3.one * 0.001f;
            return g;
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
