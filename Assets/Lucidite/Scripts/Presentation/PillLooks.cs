using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Apparence des pilules (consommables) : deux moitiés de couleur et un motif. Tirée au hasard à chaque
    /// partie : pendant une partie, une pilule garde toujours le même aspect ; d'une partie à l'autre, il change.
    /// Textures générées à la volée : image d'interface (gélule couchée) et texture de la gélule 3D.
    /// </summary>
    public static class PillLooks
    {
        public struct Look
        {
            public Color a, b, ink;
            public int pattern; // 0 uni, 1 rayures, 2 pois, 3 mouchetée, 4 bague
        }

        static readonly Color[] Colors =
        {
            Palette.Hex("E6E0D2"), Palette.Hex("D8A7A2"), Palette.Hex("C8694B"), Palette.Hex("A2262C"),
            Palette.Hex("D6B34B"), Palette.Hex("8DB38D"), Palette.Hex("3F7E78"), Palette.Hex("8EAEC7"),
            Palette.Hex("2F3E67"), Palette.Hex("9B84B7"), Palette.Hex("6A4932"), Palette.Hex("383636")
        };
        const int Patterns = 5;

        static readonly Dictionary<ItemData, Look> s_looks = new Dictionary<ItemData, Look>();
        static readonly Dictionary<ItemData, Texture2D> s_ui = new Dictionary<ItemData, Texture2D>();
        static readonly Dictionary<ItemData, Texture2D> s_capsule = new Dictionary<ItemData, Texture2D>();
        static System.Random s_rng = new System.Random(1);
        static readonly HashSet<int> s_used = new HashSet<int>();

        /// <summary>Nouvelle partie : chaque pilule reçoit un aspect unique, différent de la partie précédente.</summary>
        public static void NewRun(int seed, List<ItemData> items)
        {
            foreach (var t in s_ui.Values) if (t) Object.Destroy(t);
            foreach (var t in s_capsule.Values) if (t) Object.Destroy(t);
            s_looks.Clear(); s_ui.Clear(); s_capsule.Clear(); s_used.Clear();
            s_rng = new System.Random(seed);
            if (items == null) return;
            foreach (var i in items) if (i && i.kind == ItemKind.Consommable) Get(i);
        }

        public static Look Get(ItemData d)
        {
            if (s_looks.TryGetValue(d, out var l)) return l;
            int key;
            int tries = 0;
            do
            {
                int a = s_rng.Next(Colors.Length), b = s_rng.Next(Colors.Length), p = s_rng.Next(Patterns);
                key = (a * Colors.Length + b) * Patterns + p;
                l = new Look { a = Colors[a], b = Colors[b], pattern = p };
            } while (s_used.Contains(key) && ++tries < 200);
            s_used.Add(key);
            // Motif : clair sur fond sombre, sombre sur fond clair
            float lum = (l.a.r + l.a.g + l.a.b + l.b.r + l.b.g + l.b.b) / 6f;
            l.ink = lum > 0.5f ? Palette.Hex("2A2222") : Palette.Hex("EDE6D8");
            s_looks[d] = l;
            return l;
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }

        /// <summary>Couleur de la surface de la pilule en (u le long, v autour), u dans [0, 1].</summary>
        static Color Surface(Look l, float u, float v, int px, int py)
        {
            bool first = u < 0.5f;
            var c = first ? l.a : l.b;
            switch (l.pattern)
            {
                case 1: // rayures sur la seconde moitié
                    if (!first && Mathf.Repeat(u * 14f, 1f) < 0.38f) c = Color.Lerp(c, l.ink, 0.8f);
                    break;
                case 2: // pois
                {
                    float gu = Mathf.Repeat(u * 9f, 1f) - 0.5f, gv = Mathf.Repeat(v * 4f + (Mathf.Floor(u * 9f) % 2) * 0.5f, 1f) - 0.5f;
                    if (gu * gu + gv * gv < 0.06f) c = Color.Lerp(c, l.ink, 0.85f);
                    break;
                }
                case 3: // mouchetée
                    if (Hash(px, py) > 0.86f) c = Color.Lerp(c, l.ink, 0.7f);
                    break;
                case 4: // bague à la jointure
                    if (Mathf.Abs(u - 0.5f) < 0.07f) c = Color.Lerp(c, l.ink, 0.85f);
                    break;
            }
            // Jointure des deux moitiés
            if (Mathf.Abs(u - 0.5f) < 0.012f) c *= 0.7f;
            c.a = 1f;
            return c;
        }

        /// <summary>Image d'interface : gélule couchée, ombrée, sur fond transparent (128 x 64).</summary>
        public static Texture2D UI(ItemData d)
        {
            if (!d) return null;
            if (s_ui.TryGetValue(d, out var tex) && tex) return tex;
            var l = Get(d);
            const int W = 128, H = 64;
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Pill_" + d.name };
            var px = new Color[W * H];
            float x0 = 30f, x1 = 98f, r = 22f, cy = 32f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float qx = Mathf.Clamp(fx, x0, x1);
                    float dist = Mathf.Sqrt((fx - qx) * (fx - qx) + (fy - cy) * (fy - cy)) - r;
                    float a = Mathf.Clamp01(0.5f - dist);
                    if (a <= 0f) { px[y * W + x] = new Color(0, 0, 0, 0); continue; }
                    float u = Mathf.InverseLerp(x0 - r, x1 + r, fx);
                    float ny = Mathf.Clamp((fy - cy) / r, -1f, 1f);
                    var c = Surface(l, u, ny * 0.5f + 0.5f, x, y);
                    // Volume : lumière d'en haut, reflet, contour sombre
                    float shade = 0.62f + 0.38f * (ny * 0.5f + 0.5f);
                    c *= shade;
                    float hl = Mathf.Exp(-Mathf.Pow((ny - 0.5f) * 5f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 1.6f);
                    c = Color.Lerp(c, Color.white, hl * 0.35f);
                    if (dist > -2.5f) c *= 0.45f;
                    c.a = a;
                    px[y * W + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            s_ui[d] = tex;
            return tex;
        }

        /// <summary>
        /// Gélule 3D (capsule couchée dans son repère : axe le long de Y, à tourner de 90° pour la poser) avec
        /// l'aspect de la pilule pour cette partie. Sans collider.
        /// </summary>
        public static GameObject MakePill(ItemData d, Material mat, float length)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            g.name = d ? d.itemName : "Pilule";
            var col = g.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            float r = length * 0.22f;
            g.transform.localScale = new Vector3(r * 2f, length * 0.5f, r * 2f);
            var mr = g.GetComponent<MeshRenderer>();
            if (mat) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            var tex = Capsule(d);
            if (tex) { mpb.SetTexture("_BaseMap", tex); mpb.SetTexture("_EmissionMap", tex); }
            mr.SetPropertyBlock(mpb);
            return g;
        }

        /// <summary>Texture de la gélule 3D (capsule Unity : v le long de la capsule, u autour).</summary>
        public static Texture2D Capsule(ItemData d)
        {
            if (!d) return null;
            if (s_capsule.TryGetValue(d, out var tex) && tex) return tex;
            var l = Get(d);
            const int W = 64, H = 128;
            tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat, name = "Capsule_" + d.name };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = Surface(l, 1f - (y + 0.5f) / H, (x + 0.5f) / W, x, y);
            tex.SetPixels(px);
            tex.Apply();
            s_capsule[d] = tex;
            return tex;
        }
    }
}
