using UnityEngine;
using Canvas = Lucidite.EditorTools.TextureFactory.Canvas;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Textures "peintes" plus détaillées (look Fears to Fathom / PS1 réaliste) :
    /// visage, cheveux, uniformes, bois usé, papier, livres, carton, métal, et sprites d'interface.
    /// </summary>
    public static class TexturePainter
    {
        static float Fbm(float x, float y, int oct = 5)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++) { v += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i); a *= 0.5f; f *= 2.03f; }
            return v;
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        // ================================================================== Matières

        /// <summary>Bois de planches usé : veinage, nœuds, saleté dans les joints, rayures.</summary>
        public static Canvas WornWood(int size, Color baseCol, int seed, int planks = 4)
        {
            var c = new Canvas(size, size, baseCol);
            var rng = new System.Random(seed);
            int plank = size / planks;
            var knots = new Vector3[6];
            for (int i = 0; i < knots.Length; i++) knots[i] = new Vector3(rng.Next(size), rng.Next(size), rng.Next(3, 7));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int p = y / plank;
                    float lx = x + p * 37f, ly = y;
                    float warp = 0f;
                    foreach (var k in knots)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(k.x, k.y));
                        warp += k.z * 6f / (d + 4f);
                    }
                    float grain = Mathf.Sin((ly * 0.9f + Fbm(lx * 0.015f, ly * 0.08f) * 18f + warp) * 1.1f) * 0.5f + 0.5f;
                    float fine = Fbm(lx * 0.3f, ly * 1.2f, 2);
                    float dirt = Fbm(x * 0.02f + seed, y * 0.02f);
                    float k2 = 0.62f + grain * 0.22f + fine * 0.18f - dirt * 0.22f + ((p * 7919) % 5) * 0.03f;
                    var col = Mul(baseCol, k2);
                    int py = y % plank;
                    if (py == 0 || py == plank - 1) col = Mul(baseCol, 0.25f);
                    else if (py == 1 || py == plank - 2) col = Mul(col, 0.75f);
                    c.Set(x, y, col);
                }
            foreach (var k in knots) { c.Disc(k.x, k.y, k.z * 0.6f, Mul(baseCol, 0.35f), 2f); c.Ring(k.x, k.y, k.z, 1f, Mul(baseCol, 0.5f)); }
            for (int i = 0; i < 22; i++)
            {
                float x0 = rng.Next(size), y0 = rng.Next(size);
                c.Line(x0, y0, x0 + rng.Next(-30, 30), y0 + rng.Next(-3, 3), 1f, Mul(baseCol, 1.5f).WithAlpha(0.25f));
            }
            for (int i = 0; i < 8; i++)
                c.Disc(rng.Next(size), rng.Next(size), rng.Next(6, 22), Palette.Void.WithAlpha(0.18f), 14f);
            return c;
        }

        /// <summary>Papier peint fleuri délavé, auréoles d'humidité, moisissures, lés décollés.</summary>
        public static Canvas Wallpaper(int size, int seed)
        {
            var baseCol = Palette.Hex("4A4238");
            var c = new Canvas(size, size, baseCol);
            var rng = new System.Random(seed);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float stripe = (x % 32) < 2 ? 0.8f : ((x % 32) < 16 ? 1.0f : 0.94f);
                    float n = Fbm(x * 0.03f + seed, y * 0.03f);
                    float stain = Mathf.Clamp01((y / (float)size - 0.45f) * 1.8f) * Fbm(x * 0.02f, y * 0.008f + 3f);
                    var col = Mul(baseCol, stripe * (0.72f + n * 0.45f));
                    col = Color.Lerp(col, Palette.Hex("5C4A2A"), stain * 0.7f);
                    c.Set(x, y, col);
                }
            // Motif floral (losanges de fleurs)
            for (int y = 8; y < size; y += 32)
                for (int x = 8 + ((y / 32) % 2) * 16; x < size; x += 32)
                {
                    var petal = Palette.Hex("6A5E50").WithAlpha(0.55f);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * Mathf.PI / 2f;
                        c.Disc(x + Mathf.Cos(a) * 4, y + Mathf.Sin(a) * 4, 2.6f, petal, 1f);
                    }
                    c.Disc(x, y, 1.6f, Palette.Hex("3A2E22").WithAlpha(0.6f), 1f);
                }
            for (int i = 0; i < 30; i++)
                c.Disc(rng.Next(size), rng.Next(size / 3), rng.Next(1, 5), Palette.Hex("1A1C16").WithAlpha(0.55f), 3f);
            for (int i = 0; i < 3; i++)
            {
                int x0 = rng.Next(size - 40), y0 = rng.Next(size - 40);
                for (int y = y0; y < y0 + 30 + rng.Next(20); y++)
                    for (int x = x0; x < x0 + 16 + rng.Next(20); x++)
                        if (Fbm(x * 0.15f, y * 0.15f) > 0.47f) c.Set(x, y, Mul(Palette.Hex("6E665C"), 0.75f + Fbm(x * 0.6f, y * 0.6f) * 0.3f));
            }
            return c;
        }

        public static Canvas Fabric(int size, Color baseCol, int seed, float weave = 0.08f)
        {
            var c = new Canvas(size, size, baseCol);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float w = ((x + y) % 2 == 0 ? 1f : 1f - weave);
                    float folds = Fbm(x * 0.04f + seed, y * 0.015f);
                    float n = Fbm(x * 0.4f, y * 0.4f, 2);
                    c.Set(x, y, Mul(baseCol, w * (0.7f + folds * 0.45f + n * 0.1f)));
                }
            return c;
        }

        /// <summary>Uniforme marin (torse). u = tour du corps (0.5 = devant), v = hanches → cou.</summary>
        public static Canvas SailorTorso(Color body, Color collar, Color scarf, int seed)
        {
            var c = Fabric(128, body, seed);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = x / 127f, v = y / 127f;
                    float du = Mathf.Abs(u - 0.5f);
                    // Col en V devant
                    float vLine = 0.62f + du * 2.2f;
                    if (v > vLine && du < 0.22f) c.Set(x, y, Mul(collar, 0.85f + Fbm(x * 0.2f, y * 0.2f) * 0.2f));
                    // Liseré du col
                    if (Mathf.Abs(v - (vLine + 0.04f)) < 0.012f && du < 0.2f) c.Set(x, y, Palette.Bone);
                    // Col dans le dos (u proche de 0 ou 1)
                    float back = Mathf.Min(u, 1f - u);
                    if (v > 0.7f && back < 0.2f) c.Set(x, y, Mul(collar, 0.85f));
                    if (Mathf.Abs(v - 0.72f) < 0.01f && back < 0.2f) c.Set(x, y, Palette.Bone);
                    // Couture des côtés
                    if (Mathf.Abs(du - 0.25f) < 0.005f) c.Set(x, y, Mul(body, 0.6f));
                }
            // Foulard
            c.Disc(64, 82, 7, scarf, 2f);
            c.Line(64, 82, 58, 64, 5f, Mul(scarf, 0.85f));
            c.Line(64, 82, 70, 64, 5f, Mul(scarf, 0.8f));
            return c;
        }

        /// <summary>Manche d'uniforme avec deux liserés au poignet (v = 0 au coude → 1 au poignet).</summary>
        public static Canvas Sleeve(Color body, Color stripe, int seed)
        {
            var c = Fabric(64, body, seed, 0.1f);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float v = y / 63f;
                    if (v > 0.86f) c.Set(x, y, Mul(body, 0.8f));
                    if (Mathf.Abs(v - 0.9f) < 0.012f || Mathf.Abs(v - 0.95f) < 0.012f) c.Set(x, y, stripe);
                }
            return c;
        }

        /// <summary>Peau : teint, légères taches, veines bleutées (avant-bras).</summary>
        public static Canvas Skin(Color baseCol, int seed, bool veins, bool bruises)
        {
            var c = new Canvas(128, 128, baseCol);
            var rng = new System.Random(seed);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float n = Fbm(x * 0.06f + seed, y * 0.06f);
                    float pores = Fbm(x * 0.8f, y * 0.8f, 2);
                    c.Set(x, y, Mul(baseCol, 0.88f + n * 0.2f + pores * 0.05f));
                }
            if (veins)
                for (int i = 0; i < 4; i++)
                {
                    float x = 40 + rng.Next(48), y = 0;
                    for (int s = 0; s < 12; s++)
                    {
                        float nx = x + rng.Next(-6, 7), ny = y + 11;
                        c.Line(x, y, nx, ny, 1.4f, Palette.Hex("6A7A9A").WithAlpha(0.18f));
                        x = nx; y = ny;
                    }
                }
            if (bruises)
                for (int i = 0; i < 6; i++)
                    c.Disc(rng.Next(128), rng.Next(128), rng.Next(5, 12), Palette.Hex("4B3550").WithAlpha(0.35f), 8f);
            return c;
        }

        /// <summary>Visage de la Dépression, à plaquer sur une sphère UV (u = 0.5 devant).</summary>
        public static Canvas DepressionFace(Color skin)
        {
            var c = Skin(skin, 41, false, true);
            float cx = 64f;
            var shadow = Palette.Hex("2A1C26");
            // Orbites creusées
            foreach (int sx in new[] { -1, 1 })
            {
                float ex = cx + sx * 9f, ey = 70f;
                c.Disc(ex, ey - 2, 7.5f, shadow.WithAlpha(0.55f), 5f);
                // Œil en amande
                for (int y = -3; y <= 3; y++)
                    for (int x = -6; x <= 6; x++)
                        if (x * x / 36f + y * y / 6f < 1f) c.Set((int)ex + x, (int)ey + y, Palette.Hex("140C0E"));
                c.Disc(ex + sx * 0.5f, ey, 1.8f, Palette.Hex("6E0B0E"), 0.8f);
                c.Disc(ex - 1, ey + 1, 0.7f, Palette.Hex("C9BFAE").WithAlpha(0.6f), 0.5f);
                // Sourcils tristes (extrémités intérieures relevées)
                c.Line(ex - sx * 6f, ey + 9f, ex + sx * 6f, ey + 6f, 1.6f, Palette.Hex("1A1414").WithAlpha(0.85f));
                // Cernes et larmes
                c.Disc(ex, ey - 6, 5f, Palette.Hex("3B2433").WithAlpha(0.45f), 4f);
                c.Line(ex + sx * 1f, ey - 4, ex + sx * 2f, ey - 22, 1.2f, Palette.Hex("8E8A86").WithAlpha(0.5f));
            }
            // Nez (ombre) et bouche tombante
            c.Line(cx + 1, 64, cx + 2, 55, 1.2f, Mul(skin, 0.7f).WithAlpha(0.7f));
            c.Disc(cx, 54, 2.5f, Mul(skin, 0.75f).WithAlpha(0.5f), 2f);
            c.Line(cx - 7, 44, cx, 46, 1.4f, Palette.Hex("3A2026"));
            c.Line(cx, 46, cx + 7, 44, 1.4f, Palette.Hex("3A2026"));
            c.Line(cx - 5, 43, cx + 5, 43, 1f, Palette.Hex("5A3A40").WithAlpha(0.5f));
            // Coupure au front et ecchymose à la pommette
            c.Line(cx - 10, 86, cx - 2, 83, 1.2f, Palette.BloodLight);
            c.Disc(cx + 15, 58, 6, Palette.Hex("4B3550").WithAlpha(0.55f), 5f);
            return c;
        }

        /// <summary>Cheveux noirs à mèches.</summary>
        public static Canvas Hair(int seed)
        {
            var baseCol = Palette.Hex("151313");
            var c = new Canvas(64, 64, baseCol);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float strand = Mathf.Sin(x * 1.7f + Fbm(x * 0.1f, y * 0.05f + seed) * 6f) * 0.5f + 0.5f;
                    float k = 0.7f + strand * 0.4f + Fbm(x * 0.3f, y * 0.3f, 2) * 0.15f;
                    c.Set(x, y, Mul(Palette.Hex("231E1E"), k));
                }
            return c;
        }

        public static Canvas Paper(int size, int seed, bool lined)
        {
            var c = new Canvas(size, size, Palette.Hex("BDB3A0"));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Fbm(x * 0.03f + seed, y * 0.03f);
                    var col = Mul(Palette.Hex("C2B8A4"), 0.82f + n * 0.25f);
                    if (lined && y % 12 == 0) col = Color.Lerp(col, Palette.Hex("6A7A9A"), 0.3f);
                    c.Set(x, y, col);
                }
            var rng = new System.Random(seed);
            for (int i = 0; i < 4; i++) c.Ring(rng.Next(size), rng.Next(size), rng.Next(8, 20), 2f, Palette.Hex("7A5A30").WithAlpha(0.25f));
            return c;
        }

        public static Canvas Books(int seed)
        {
            var c = new Canvas(64, 64, Palette.Hex("2A2420"));
            var rng = new System.Random(seed);
            var cols = new[] { "4A1E1E", "1E2A3A", "3A3A2A", "5A4A3A", "2A3A2A", "6A5A4A" };
            int x = 0;
            while (x < 64)
            {
                int w = 4 + rng.Next(6);
                var col = Palette.Hex(cols[rng.Next(cols.Length)]);
                for (int xx = x; xx < Mathf.Min(64, x + w); xx++)
                    for (int y = 0; y < 64; y++)
                        c.Set(xx, y, Mul(col, 0.8f + Fbm(xx * 0.3f, y * 0.1f, 2) * 0.3f));
                for (int y = 10; y < 13; y++) for (int xx = x + 1; xx < Mathf.Min(64, x + w - 1); xx++) c.Set(xx, y, Palette.Hex("A89060"));
                x += w;
            }
            return c;
        }

        public static Canvas Cardboard(int seed)
        {
            var c = Fabric(64, Palette.Hex("6A5034"), seed, 0.02f);
            c.Rect(0, 30, 63, 33, Palette.Hex("8A7A5A").WithAlpha(0.8f));
            return c;
        }

        public static Canvas Metal(Color baseCol, int seed)
        {
            var c = new Canvas(64, 64, baseCol);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float rust = Mathf.Clamp01(Fbm(x * 0.08f + seed, y * 0.08f) * 1.8f - 0.75f);
                    var col = Mul(baseCol, 0.8f + Fbm(x * 0.5f, y * 0.05f, 2) * 0.3f);
                    c.Set(x, y, Color.Lerp(col, Palette.Hex("5A2E1A"), rust));
                }
            return c;
        }

        // ================================================================== Interface

        /// <summary>Page de carnet déchirée (sprite des règles).</summary>
        public static Canvas NotebookPage(int w, int h, int seed)
        {
            var c = new Canvas(w, h, new Color(0, 0, 0, 0));
            var rng = new System.Random(seed);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float edge = Fbm(y * 0.08f, seed) * 14f;
                    float edgeTop = Fbm(x * 0.06f, seed + 5) * 10f;
                    if (x > w - 6 - edge || y > h - 4 - edgeTop || x < 4 || y < 4) continue;
                    float n = Fbm(x * 0.01f + seed, y * 0.01f);
                    var col = Mul(Palette.Hex("CFC6B2"), 0.78f + n * 0.25f);
                    if (y % 34 == 0 && x > 40) col = Color.Lerp(col, Palette.Hex("7A8AA8"), 0.35f);
                    if (x == 70 || x == 71) col = Color.Lerp(col, Palette.Hex("A83A3A"), 0.45f);
                    float burn = Mathf.Clamp01(1f - Mathf.Min(Mathf.Min(x, w - x), Mathf.Min(y, h - y)) / 40f);
                    col = Color.Lerp(col, Palette.Hex("5A4630"), burn * 0.5f);
                    c.Set(x, y, col);
                }
            for (int i = 0; i < 3; i++) c.Ring(rng.Next(w), rng.Next(h), rng.Next(30, 60), 4f, Palette.Hex("7A5A30").WithAlpha(0.18f));
            c.Disc(rng.Next(w), rng.Next(h / 3), 14, Palette.Blood.WithAlpha(0.25f), 10f);
            return c;
        }

        /// <summary>Cadre de carte d'interface (tarot) : fond sombre texturé et filets.</summary>
        public static Canvas UICardFrame(int seed)
        {
            int w = 220, h = 330;
            var c = new Canvas(w, h, Palette.Hex("1A1616"));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Fbm(x * 0.03f + seed, y * 0.03f);
                    c.Set(x, y, Mul(Palette.Hex("D8D0C0"), 0.65f + n * 0.3f));
                }
            var ink = Palette.Hex("1A1414");
            c.Frame(4, 3, ink);
            c.Frame(11, 1, ink.WithAlpha(0.7f));
            foreach (var p in new[] { new Vector2(12, 12), new Vector2(w - 13, 12), new Vector2(12, h - 13), new Vector2(w - 13, h - 13) })
                c.Disc(p.x, p.y, 4, ink, 1f);
            return c;
        }

        public static Canvas RadialVignette(int size)
        {
            var c = new Canvas(size, size, new Color(0, 0, 0, 0));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size * 0.5f);
                    c.Set(x, y, new Color(0, 0, 0, Mathf.Clamp01(Mathf.Pow(d, 1.6f))));
                }
            return c;
        }

        public static Canvas Gradient(int w, int h, bool vertical)
        {
            var c = new Canvas(w, h, Color.white);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    c.Set(x, y, new Color(1, 1, 1, vertical ? y / (float)(h - 1) : x / (float)(w - 1)));
            return c;
        }

        public static Canvas Orb(int size)
        {
            var c = new Canvas(size, size, new Color(1, 1, 1, 0));
            float r = size / 2f - 2;
            c.Disc(size / 2f, size / 2f, r, Color.white, 1.5f);
            c.Ring(size / 2f, size / 2f, r - 3, 2f, new Color(0.5f, 0.5f, 0.5f, 1f));
            return c;
        }

        /// <summary>Trait au pinceau sous une entrée de menu.</summary>
        public static Canvas Scratch(int seed)
        {
            var c = new Canvas(256, 32, new Color(1, 1, 1, 0));
            var rng = new System.Random(seed);
            float y = 16;
            for (int x = 6; x < 250; x += 2)
            {
                float t = x / 256f;
                float th = Mathf.Sin(t * Mathf.PI) * 5f + 1f;
                y += (float)(rng.NextDouble() - 0.5) * 0.8f;
                c.Disc(x, y, th, Color.white.WithAlpha(0.9f), 1f);
            }
            return c;
        }

        public enum Icon { Sword, Shield, Eye, Up }

        public static Canvas IntentIcon(Icon icon)
        {
            var c = new Canvas(64, 64, new Color(1, 1, 1, 0));
            var w = Color.white;
            switch (icon)
            {
                case Icon.Sword:
                    c.Line(16, 16, 50, 50, 6f, w);
                    c.Line(10, 30, 30, 10, 5f, w);
                    c.Line(8, 8, 16, 16, 5f, w);
                    break;
                case Icon.Shield:
                    for (int y = 10; y < 56; y++)
                    {
                        float half = y > 30 ? 20f : Mathf.Lerp(4f, 20f, (y - 10) / 20f);
                        half = y > 30 ? 20f : half;
                        if (y <= 30) half = Mathf.Lerp(20f, 22f, (y - 10) / 20f);
                        if (y > 30) half = Mathf.Lerp(22f, 0f, (y - 30) / 26f) ;
                        for (int x = (int)(32 - half); x <= 32 + half; x++) c.Set(x, 64 - y, w);
                    }
                    break;
                case Icon.Eye:
                    c.Ring(32, 20, 26, 5f, w, 40, 140);
                    c.Ring(32, 44, 26, 5f, w, 220, 320);
                    c.Disc(32, 32, 8, w);
                    break;
                default:
                    c.Line(32, 10, 32, 50, 7f, w);
                    c.Line(32, 54, 16, 36, 6f, w);
                    c.Line(32, 54, 48, 36, 6f, w);
                    break;
            }
            return c;
        }
    }
}
