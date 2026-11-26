using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Petit modeleur procédural : boîtes à UV "au mètre", tours (lathe), tubes elliptiques le long d'un chemin,
    /// ellipsoïdes déformables, grilles déplacées. Produit des maillages low-poly façon PS1.
    /// L'orientation des triangles est vérifiée automatiquement à partir d'une normale indicative.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int> _t = new List<int>();
        Matrix4x4 _m = Matrix4x4.identity;

        public MeshBuilder At(Vector3 pos, Quaternion rot, Vector3 scale) { _m = Matrix4x4.TRS(pos, rot, scale); return this; }
        public MeshBuilder At(Vector3 pos, Vector3 euler) => At(pos, Quaternion.Euler(euler), Vector3.one);
        public MeshBuilder At(Vector3 pos) => At(pos, Quaternion.identity, Vector3.one);
        public MeshBuilder Reset() { _m = Matrix4x4.identity; return this; }

        int V(Vector3 p, Vector2 uv)
        {
            _v.Add(_m.MultiplyPoint3x4(p));
            _uv.Add(uv);
            return _v.Count - 1;
        }

        void Tri(int a, int b, int c, Vector3 localHint)
        {
            var n = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
            var hint = _m.MultiplyVector(localHint);
            if (Vector3.Dot(n, hint) < 0f) { _t.Add(a); _t.Add(c); _t.Add(b); }
            else { _t.Add(a); _t.Add(b); _t.Add(c); }
        }

        void Quad(int a, int b, int c, int d, Vector3 hint)
        {
            Tri(a, b, c, hint);
            Tri(a, c, d, hint);
        }

        // ------------------------------------------------------------------ Primitives

        /// <summary>Boîte centrée. UV en mètres × uvScale (pas d'étirement de texture).</summary>
        public MeshBuilder Box(Vector3 c, Vector3 s, float uvScale = 1f, Vector2 uvOffset = default)
        {
            Vector3 h = s * 0.5f;
            void Face(Vector3 n, Vector3 u, Vector3 w, float du, float dw)
            {
                var o = c + Vector3.Scale(n, h);
                var a = V(o - u * du * 0.5f - w * dw * 0.5f, uvOffset + new Vector2(0, 0));
                var b = V(o + u * du * 0.5f - w * dw * 0.5f, uvOffset + new Vector2(du * uvScale, 0));
                var cc = V(o + u * du * 0.5f + w * dw * 0.5f, uvOffset + new Vector2(du * uvScale, dw * uvScale));
                var d = V(o - u * du * 0.5f + w * dw * 0.5f, uvOffset + new Vector2(0, dw * uvScale));
                Quad(a, b, cc, d, n);
            }
            Face(Vector3.forward, Vector3.right, Vector3.up, s.x, s.y);
            Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
            Face(Vector3.left, Vector3.forward, Vector3.up, s.z, s.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
            Face(Vector3.down, Vector3.right, Vector3.forward, s.x, s.z);
            return this;
        }

        /// <summary>Révolution d'un profil (rayon, hauteur) autour de l'axe Y.</summary>
        public MeshBuilder Lathe(IList<Vector2> profile, int segments, float uTile = 1f, float vScale = 1f, bool capBottom = false, bool capTop = false)
        {
            int rings = profile.Count;
            var idx = new int[rings, segments + 1];
            float vAcc = 0f;
            for (int r = 0; r < rings; r++)
            {
                if (r > 0) vAcc += Vector2.Distance(profile[r], profile[r - 1]) * vScale;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Sin(a) * profile[r].x, profile[r].y, Mathf.Cos(a) * profile[r].x);
                    idx[r, s] = V(p, new Vector2(s / (float)segments * uTile, vAcc));
                }
            }
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < segments; s++)
                {
                    float a = (s + 0.5f) / segments * Mathf.PI * 2f;
                    var hint = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    if (profile[r].x < 1e-4f && profile[r + 1].x < 1e-4f) continue;
                    Quad(idx[r, s], idx[r, s + 1], idx[r + 1, s + 1], idx[r + 1, s], hint);
                }
            if (capBottom) Cap(profile[0], segments, Vector3.down);
            if (capTop) Cap(profile[rings - 1], segments, Vector3.up);
            return this;
        }

        void Cap(Vector2 ring, int segments, Vector3 n)
        {
            int center = V(new Vector3(0, ring.y, 0), new Vector2(0.5f, 0.5f));
            var ids = new int[segments + 1];
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                ids[s] = V(new Vector3(Mathf.Sin(a) * ring.x, ring.y, Mathf.Cos(a) * ring.x), new Vector2(0.5f + Mathf.Sin(a) * 0.5f, 0.5f + Mathf.Cos(a) * 0.5f));
            }
            for (int s = 0; s < segments; s++) Tri(center, ids[s], ids[s + 1], n);
        }

        /// <summary>Tube à sections elliptiques le long d'un chemin (membres, torse, mèches...).</summary>
        public MeshBuilder Tube(IList<Vector3> path, IList<Vector2> radii, int segments, Vector3 upHint, bool capStart = true, bool capEnd = true,
            float uTile = 1f, float vScale = 1f, Func<int, int, float> radiusMod = null, float uOffset = 0f)
        {
            int rings = path.Count;
            var idx = new int[rings, segments + 1];
            var centers = new int[2];
            float vAcc = 0f;
            Vector3 prevSide = Vector3.zero;
            for (int r = 0; r < rings; r++)
            {
                var fwd = (r < rings - 1 ? path[r + 1] - path[r] : path[r] - path[r - 1]).normalized;
                if (r > 0 && r < rings - 1) fwd = (path[r + 1] - path[r - 1]).normalized;
                var side = Vector3.Cross(upHint, fwd);
                if (side.sqrMagnitude < 1e-6f) side = prevSide.sqrMagnitude > 0 ? prevSide : Vector3.Cross(Vector3.forward, fwd);
                side.Normalize();
                prevSide = side;
                var up = Vector3.Cross(fwd, side).normalized;
                if (r > 0) vAcc += Vector3.Distance(path[r], path[r - 1]) * vScale;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float k = radiusMod != null ? radiusMod(r, s) : 1f;
                    var off = side * Mathf.Sin(a) * radii[r].x * k + up * Mathf.Cos(a) * radii[r].y * k;
                    idx[r, s] = V(path[r] + off, new Vector2(uOffset + s / (float)segments * uTile, vAcc));
                }
            }
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < segments; s++)
                {
                    var mid = (path[r] + path[r + 1]) * 0.5f;
                    var p = (_m.inverse.MultiplyPoint3x4(_v[idx[r, s]]) + _m.inverse.MultiplyPoint3x4(_v[idx[r + 1, s + 1]])) * 0.5f;
                    Quad(idx[r, s], idx[r, s + 1], idx[r + 1, s + 1], idx[r + 1, s], p - mid);
                }
            if (capStart) TubeCap(path[0], idx, 0, segments, path[0] - path[1]);
            if (capEnd) TubeCap(path[rings - 1], idx, rings - 1, segments, path[rings - 1] - path[rings - 2]);
            return this;
        }

        void TubeCap(Vector3 center, int[,] idx, int ring, int segments, Vector3 n)
        {
            int c = V(center, new Vector2(0.5f, 0.5f));
            for (int s = 0; s < segments; s++) Tri(c, idx[ring, s], idx[ring, s + 1], n);
        }

        /// <summary>Ellipsoïde UV (u = 0.5 face à +Z). La déformation reçoit une direction unitaire et renvoie un facteur par axe.</summary>
        public MeshBuilder Ellipsoid(Vector3 c, Vector3 radii, int lat, int lon, Func<Vector3, Vector3> deform = null)
        {
            var idx = new int[lat + 1, lon + 1];
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, i / (float)lat);
                for (int j = 0; j <= lon; j++)
                {
                    float ph = -Mathf.PI + j / (float)lon * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Sin(th), Mathf.Cos(ph) * Mathf.Cos(th));
                    var k = deform != null ? deform(dir) : Vector3.one;
                    var p = c + Vector3.Scale(Vector3.Scale(dir, radii), k);
                    idx[i, j] = V(p, new Vector2(j / (float)lon, i / (float)lat));
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    float th = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (i + 0.5f) / lat);
                    float ph = -Mathf.PI + (j + 0.5f) / lon * Mathf.PI * 2f;
                    var hint = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Sin(th), Mathf.Cos(ph) * Mathf.Cos(th));
                    Quad(idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j], hint);
                }
            return this;
        }

        /// <summary>Grille horizontale déplacée (draps, couverture, tas de vêtements).</summary>
        public MeshBuilder Grid(Vector3 c, Vector2 size, int nx, int nz, Func<float, float, float> height, float uvScale = 1f)
        {
            var idx = new int[nx + 1, nz + 1];
            for (int x = 0; x <= nx; x++)
                for (int z = 0; z <= nz; z++)
                {
                    float u = x / (float)nx, w = z / (float)nz;
                    var p = c + new Vector3((u - 0.5f) * size.x, height(u, w), (w - 0.5f) * size.y);
                    idx[x, z] = V(p, new Vector2(u * size.x * uvScale, w * size.y * uvScale));
                }
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                    Quad(idx[x, z], idx[x + 1, z], idx[x + 1, z + 1], idx[x, z + 1], Vector3.up);
            return this;
        }

        /// <summary>Quad simple (décalcomanies, affiches). Face visible orientée vers <paramref name="facing"/>.</summary>
        public MeshBuilder Plane(Vector3 c, Vector3 facing, Vector3 upDir, Vector2 size)
        {
            var n = facing.normalized;
            var right = Vector3.Cross(upDir, n).normalized;
            var up = Vector3.Cross(n, right).normalized;
            int a = V(c - right * size.x * 0.5f - up * size.y * 0.5f, new Vector2(0, 0));
            int b = V(c + right * size.x * 0.5f - up * size.y * 0.5f, new Vector2(1, 0));
            int cc = V(c + right * size.x * 0.5f + up * size.y * 0.5f, new Vector2(1, 1));
            int d = V(c - right * size.x * 0.5f + up * size.y * 0.5f, new Vector2(0, 1));
            Quad(a, b, cc, d, n);
            return this;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _uv);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    /// <summary>Range les maillages générés dans un asset conteneur par modèle.</summary>
    public static class MeshStore
    {
        static string s_path;
        static bool s_first;

        public static void Begin(string name)
        {
            s_path = $"{AssetFactory.MeshDir}/{name}.asset";
            AssetDatabase.DeleteAsset(s_path);
            s_first = true;
        }

        public static Mesh Save(Mesh m)
        {
            if (s_first) { AssetDatabase.CreateAsset(m, s_path); s_first = false; }
            else AssetDatabase.AddObjectToAsset(m, s_path);
            return m;
        }

        public static Mesh Save(MeshBuilder b, string name) => Save(b.Build(name));
    }
}
