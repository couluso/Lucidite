using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Textures procédurales basse résolution (look PS1) : bois, papier peint, sol, cartes façon tarot,
    /// sigils par thème, blessures, fissures, sang, icônes et particules. Sauvegardées en PNG.
    /// </summary>
    public static class TextureFactory
    {
        // ------------------------------------------------------------------ Canvas

        public class Canvas
        {
            public readonly int w, h;
            public readonly Color[] px;
            public Canvas(int w, int h, Color fill)
            {
                this.w = w; this.h = h;
                px = new Color[w * h];
                for (int i = 0; i < px.Length; i++) px[i] = fill;
            }
            public Color Get(int x, int y) => px[Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1)];
            public void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                px[y * w + x] = c;
            }
            public void Blend(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = y * w + x;
                var d = px[i];
                float a = c.a;
                px[i] = new Color(Mathf.Lerp(d.r, c.r, a), Mathf.Lerp(d.g, c.g, a), Mathf.Lerp(d.b, c.b, a), Mathf.Max(d.a, a));
            }
            public void Disc(float cx, float cy, float r, Color c, float soft = 0.8f)
            {
                for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                    for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = Mathf.Clamp01((r - d) / soft + 0.5f);
                        if (a > 0f) Blend(x, y, c.WithAlpha(c.a * a));
                    }
            }
            public void Ring(float cx, float cy, float r, float thick, Color c, float fromDeg = 0f, float toDeg = 360f)
            {
                for (int y = (int)(cy - r - thick - 1); y <= cy + r + thick + 1; y++)
                    for (int x = (int)(cx - r - thick - 1); x <= cx + r + thick + 1; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(thick * 0.5f - Mathf.Abs(d - r) + 0.5f);
                        if (a <= 0f) continue;
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (ang < 0) ang += 360f;
                        if (fromDeg <= toDeg ? (ang < fromDeg || ang > toDeg) : (ang < fromDeg && ang > toDeg)) continue;
                        Blend(x, y, c.WithAlpha(c.a * a));
                    }
            }
            public void Line(float x0, float y0, float x1, float y1, float thick, Color c)
            {
                float len = Mathf.Max(1f, Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
                int steps = Mathf.CeilToInt(len * 2f);
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Disc(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), thick * 0.5f, c, 0.6f);
                }
            }
            public void Rect(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Blend(x, y, c);
            }
            public void Frame(int inset, int thick, Color c)
            {
                Rect(inset, inset, w - 1 - inset, inset + thick - 1, c);
                Rect(inset, h - inset - thick, w - 1 - inset, h - 1 - inset, c);
                Rect(inset, inset, inset + thick - 1, h - 1 - inset, c);
                Rect(w - inset - thick, inset, w - 1 - inset, h - 1 - inset, c);
            }
        }

        static float Fbm(float x, float y, int oct = 4)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++) { v += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i); a *= 0.5f; f *= 2f; }
            return v;
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        // ------------------------------------------------------------------ Sauvegarde

        public static Texture2D Save(Canvas c, string path, bool point = true, bool repeat = true, bool sprite = false, bool alpha = false, bool mips = false)
        {
            var tex = new Texture2D(c.w, c.h, TextureFormat.RGBA32, false);
            tex.SetPixels(c.px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.mipmapEnabled = mips;   // mips : traits fins (cartes) qui scintilleraient de loin
            imp.alphaIsTransparency = alpha;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Matières

        public static Canvas Wood(int size, Color baseCol, bool planksHorizontal, int seed)
        {
            var c = new Canvas(size, size, baseCol);
            var rng = new System.Random(seed);
            int plank = size / 4;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int u = planksHorizontal ? x : y, v = planksHorizontal ? y : x;
                    int p = v / plank;
                    float grain = Mathf.Sin((u * 0.08f + Fbm(u * 0.02f, v * 0.25f + p * 7f) * 6f) * 3f) * 0.5f + 0.5f;
                    float n = Fbm(x * 0.05f + seed, y * 0.05f);
                    float k = 0.7f + grain * 0.25f + (n - 0.5f) * 0.3f + (p % 2 == 0 ? 0.05f : -0.04f);
                    var col = Mul(baseCol, k);
                    if (v % plank == 0 || v % plank == plank - 1) col = Mul(baseCol, 0.35f);
                    c.Set(x, y, col);
                }
            // Taches et rayures
            for (int i = 0; i < 6; i++)
            {
                float cx = rng.Next(size), cy = rng.Next(size), r = rng.Next(4, size / 6);
                c.Disc(cx, cy, r, Mul(Palette.Void, 1f).WithAlpha(0.25f), r * 0.8f);
            }
            for (int i = 0; i < 10; i++)
            {
                float x0 = rng.Next(size), y0 = rng.Next(size);
                c.Line(x0, y0, x0 + rng.Next(-20, 20), y0 + rng.Next(-6, 6), 1f, Mul(baseCol, 1.5f).WithAlpha(0.35f));
            }
            return c;
        }

        public static Canvas Wallpaper(int size, int seed)
        {
            var baseCol = Palette.Hex("3A3532");
            var c = new Canvas(size, size, baseCol);
            var rng = new System.Random(seed);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float stripe = (x % 16) < 3 ? 0.82f : 1f;
                    float motif = ((x / 8 + y / 8) % 2 == 0 && (x % 8 - 4) * (x % 8 - 4) + (y % 8 - 4) * (y % 8 - 4) < 5) ? 0.85f : 1f;
                    float n = Fbm(x * 0.04f + seed, y * 0.04f);
                    float stain = Mathf.Clamp01((y / (float)size - 0.55f) * 2f) * Fbm(x * 0.03f, y * 0.01f) * 0.6f; // dégât des eaux en haut
                    float k = stripe * motif * (0.75f + n * 0.4f) - stain * 0.4f;
                    var col = Mul(baseCol, k);
                    col = Color.Lerp(col, Palette.Hex("4A3A22"), stain * 0.6f);
                    c.Set(x, y, col);
                }
            // Moisissures
            for (int i = 0; i < 14; i++)
                c.Disc(rng.Next(size), rng.Next(size / 3), rng.Next(1, 4), Palette.Hex("1C2018").WithAlpha(0.6f), 2f);
            // Papier arraché
            for (int i = 0; i < 2; i++)
            {
                int x0 = rng.Next(size - 30), y0 = rng.Next(size - 30);
                for (int y = y0; y < y0 + 20 + rng.Next(10); y++)
                    for (int x = x0; x < x0 + 14 + rng.Next(14); x++)
                        if (Fbm(x * 0.2f, y * 0.2f) > 0.45f) c.Set(x, y, Mul(Palette.Hex("595048"), 0.8f + Fbm(x * 0.5f, y * 0.5f) * 0.3f));
            }
            return c;
        }

        public static Canvas Noise(int size, Color baseCol, float amount, int seed, float scale = 0.08f)
        {
            var c = new Canvas(size, size, baseCol);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Fbm(x * scale + seed, y * scale + seed * 0.37f);
                    c.Set(x, y, Mul(baseCol, 1f + (n - 0.5f) * amount * 2f));
                }
            return c;
        }

        public static Canvas Concrete(int size, int seed)
        {
            var c = Noise(size, Palette.Hex("2C2927"), 0.5f, seed, 0.1f);
            var rng = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                float x = rng.Next(size), y = rng.Next(size);
                for (int s = 0; s < 8; s++)
                {
                    float nx = x + rng.Next(-8, 9), ny = y + rng.Next(-8, 9);
                    c.Line(x, y, nx, ny, 1f, Palette.Void.WithAlpha(0.7f));
                    x = nx; y = ny;
                }
            }
            return c;
        }

        public static Canvas Skin(Color baseCol, bool bruised, int seed)
        {
            var c = Noise(64, baseCol, 0.12f, seed, 0.12f);
            if (bruised)
            {
                var rng = new System.Random(seed);
                for (int i = 0; i < 5; i++)
                    c.Disc(rng.Next(64), rng.Next(64), rng.Next(4, 9), Palette.Hex("4B3550").WithAlpha(0.35f), 5f);
            }
            return c;
        }

        // ------------------------------------------------------------------ Cartes

        /// <summary>Face de carte neutre (teintée par type via _BaseColor) avec cadre ornementé façon tarot.</summary>
        public static Canvas CardFront()
        {
            int w = 128, h = 192;
            var c = new Canvas(w, h, new Color(0.78f, 0.76f, 0.72f));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Fbm(x * 0.07f, y * 0.07f);
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) / 20f;
                    float k = (0.8f + n * 0.35f) * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(edge));
                    c.Set(x, y, Mul(c.Get(x, y), k));
                }
            var ink = new Color(0.12f, 0.1f, 0.1f, 1f);
            c.Frame(3, 2, ink);
            c.Frame(7, 1, ink.WithAlpha(0.8f));
            // Coins en losange
            foreach (var p in new[] { new Vector2(8, 8), new Vector2(w - 9, 8), new Vector2(8, h - 9), new Vector2(w - 9, h - 9) })
                for (int i = -4; i <= 4; i++) { c.Line(p.x - 4 + Mathf.Abs(i), p.y + i, p.x + 4 - Mathf.Abs(i), p.y + i, 1f, ink); }
            // Cadre de l'illustration (zone haute) et cartouche de texte
            c.Frame(0, 0, ink);
            c.Rect(14, 60, w - 15, 61, ink);
            c.Rect(14, 138, w - 15, 139, ink.WithAlpha(0.7f));
            c.Rect(14, 24, w - 15, 25, ink.WithAlpha(0.7f));
            return c;
        }

        public static Canvas CardBack()
        {
            int w = 128, h = 192;
            var c = new Canvas(w, h, Palette.Void);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(w / 2f, h / 2f)) / 90f;
                    float pattern = ((x + y) % 12 == 0 || (x - y + 600) % 12 == 0) ? 0.5f : 0f;
                    c.Set(x, y, Color.Lerp(Palette.BloodDark, Palette.Void, d) + Palette.BloodDark * pattern * 0.5f);
                }
            var red = Palette.Blood;
            c.Frame(4, 2, red);
            c.Frame(9, 1, red.WithAlpha(0.7f));
            float cx = w / 2f, cy = h / 2f;
            c.Ring(cx, cy, 34, 2f, red);
            c.Ring(cx, cy, 26, 1f, red.WithAlpha(0.7f));
            // Œil central
            c.Ring(cx, cy - 18, 26, 2f, Palette.BloodLight, 50, 130);
            c.Ring(cx, cy + 18, 26, 2f, Palette.BloodLight, 230, 310);
            c.Disc(cx, cy, 7, Palette.BloodLight);
            c.Disc(cx, cy, 3, Palette.Void);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                c.Line(cx + Mathf.Cos(a) * 38, cy + Mathf.Sin(a) * 38, cx + Mathf.Cos(a) * 52, cy + Mathf.Sin(a) * 52, 1.5f, red);
            }
            return c;
        }

        /// <summary>Illustration de carte (sigil) selon la thématique.</summary>
        public static Canvas Sigil(CardTheme theme)
        {
            int s = 128;
            var c = new Canvas(s, s, Palette.Void);
            float cx = s / 2f, cy = s / 2f;
            bool hopeful = theme >= CardTheme.Soutien;
            var glow = hopeful ? Palette.Hope : Palette.Blood;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / (s * 0.6f);
                    float n = Fbm(x * 0.06f + (int)theme * 3f, y * 0.06f);
                    c.Set(x, y, Color.Lerp(Mul(glow, 0.45f + n * 0.3f), Palette.Void, Mathf.Clamp01(d)));
                }
            var ink = Palette.Bone;
            var acc = hopeful ? Palette.Hope : Palette.BloodLight;
            switch (theme)
            {
                case CardTheme.Depression: // nuage de pluie et larme
                    c.Disc(cx - 18, cy + 22, 14, ink); c.Disc(cx, cy + 28, 18, ink); c.Disc(cx + 20, cy + 22, 13, ink);
                    c.Rect((int)cx - 30, (int)cy + 12, (int)cx + 32, (int)cy + 20, ink);
                    for (int i = -2; i <= 2; i++) c.Line(cx + i * 12, cy + 6, cx + i * 12 - 4, cy - 26, 1.5f, ink.WithAlpha(0.8f));
                    c.Disc(cx + 2, cy - 36, 6, acc); c.Line(cx + 2, cy - 30, cx + 2, cy - 22, 3f, acc);
                    break;
                case CardTheme.Anxiete: // spirale
                    {
                        float px = cx, py = cy;
                        for (float a = 0; a < Mathf.PI * 7f; a += 0.08f)
                        {
                            float r = 2f + a * 2.4f;
                            float nx = cx + Mathf.Cos(a) * r, ny = cy + Mathf.Sin(a) * r;
                            c.Line(px, py, nx, ny, 2f, ink); px = nx; py = ny;
                        }
                        c.Disc(cx, cy, 4, acc);
                    }
                    break;
                case CardTheme.Desespoir: // gouffre
                    for (int i = 0; i < 6; i++) c.Ring(cx, cy - i * 4, 44 - i * 7, 2f, Color.Lerp(ink, acc, i / 6f));
                    c.Disc(cx, cy - 24, 6, Palette.Void);
                    break;
                case CardTheme.VideSolitude: // cercle vide et silhouette seule
                    c.Ring(cx, cy + 8, 38, 2.5f, ink);
                    c.Disc(cx + 22, cy - 34, 4, ink); c.Line(cx + 22, cy - 38, cx + 22, cy - 52, 2.5f, ink);
                    break;
                case CardTheme.Culpabilite: // chaînes
                    for (int i = 0; i < 4; i++) c.Ring(cx - 30 + i * 20, cy + (i % 2 == 0 ? 6 : -6), 12, 3f, i % 2 == 0 ? ink : acc);
                    break;
                case CardTheme.Dissociation: // visage dédoublé
                    c.Ring(cx - 8, cy, 28, 2.5f, ink); c.Ring(cx + 10, cy + 4, 28, 1.5f, acc.WithAlpha(0.7f));
                    c.Disc(cx - 18, cy + 6, 3, ink); c.Disc(cx + 2, cy + 6, 3, ink);
                    c.Line(cx - 18, cy - 12, cx + 2, cy - 12, 2f, ink);
                    break;
                case CardTheme.PenseesIntrusives: // œil et rayons
                    c.Ring(cx, cy - 22, 36, 2.5f, ink, 40, 140); c.Ring(cx, cy + 22, 36, 2.5f, ink, 220, 320);
                    c.Disc(cx, cy, 10, acc); c.Disc(cx, cy, 4, Palette.Void);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI / 5f;
                        c.Line(cx + Mathf.Cos(a) * 42, cy + Mathf.Sin(a) * 42, cx + Mathf.Cos(a) * 56, cy + Mathf.Sin(a) * 56, 1.5f, ink.WithAlpha(0.7f));
                    }
                    break;
                case CardTheme.Insomnie: // croissant de lune et horloge
                    c.Disc(cx - 10, cy + 14, 26, ink); c.Disc(cx + 2, cy + 22, 24, c.Get((int)cx + 2, (int)cy + 22));
                    c.Ring(cx + 22, cy - 26, 14, 2f, acc); c.Line(cx + 22, cy - 26, cx + 22, cy - 16, 2f, acc); c.Line(cx + 22, cy - 26, cx + 30, cy - 26, 2f, acc);
                    break;
                case CardTheme.Traumatismes: // miroir brisé
                    c.Ring(cx, cy, 40, 3f, ink);
                    {
                        var rng = new System.Random(5);
                        for (int i = 0; i < 7; i++)
                        {
                            float a = i * Mathf.PI * 2f / 7f + (float)rng.NextDouble() * 0.4f;
                            float mx = cx + 10 + Mathf.Cos(a + 0.3f) * 20, my = cy - 6 + Mathf.Sin(a + 0.3f) * 20;
                            c.Line(cx + 10, cy - 6, mx, my, 1.5f, acc);
                            c.Line(mx, my, cx + Mathf.Cos(a) * 40, cy + Mathf.Sin(a) * 40, 1.5f, acc);
                        }
                    }
                    break;
                case CardTheme.Soutien: // anneaux entrelacés + cœur
                    c.Ring(cx - 14, cy, 22, 3f, ink); c.Ring(cx + 14, cy, 22, 3f, acc);
                    c.Disc(cx - 4, cy - 36, 6, acc); c.Disc(cx + 4, cy - 36, 6, acc); c.Line(cx - 9, cy - 38, cx, cy - 48, 4f, acc); c.Line(cx + 9, cy - 38, cx, cy - 48, 4f, acc);
                    break;
                case CardTheme.Acceptation: // cercle ouvert (enso)
                    c.Ring(cx, cy, 36, 5f, ink, 110, 70);
                    c.Disc(cx, cy, 5, acc);
                    break;
                case CardTheme.Resilience: // pousse
                    c.Line(cx - 40, cy - 34, cx + 40, cy - 34, 2f, ink.WithAlpha(0.7f));
                    c.Line(cx, cy - 34, cx, cy + 20, 3f, ink);
                    c.Disc(cx - 12, cy + 8, 9, acc); c.Disc(cx + 12, cy + 18, 9, acc);
                    break;
                default: // Espoir : flamme / soleil
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 6f;
                        c.Line(cx + Mathf.Cos(a) * 26, cy + Mathf.Sin(a) * 26, cx + Mathf.Cos(a) * 46, cy + Mathf.Sin(a) * 46, 2f, acc);
                    }
                    c.Disc(cx, cy, 20, acc); c.Disc(cx, cy - 4, 10, ink);
                    break;
            }
            return c;
        }

        // ------------------------------------------------------------------ Marques & sang (alpha cutout)

        public static Canvas Wound(int seed)
        {
            var c = new Canvas(64, 32, new Color(0, 0, 0, 0));
            var rng = new System.Random(seed);
            // hématome
            c.Disc(32, 16, 12, Palette.Hex("4B2A3A").WithAlpha(0.55f), 8f);
            int n = 3 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float y = 6 + i * (20f / n) + rng.Next(-2, 3);
                float x0 = 8 + rng.Next(8), x1 = 50 + rng.Next(8);
                c.Line(x0, y, x1, y + rng.Next(-3, 4), 2.2f, Palette.BloodLight);
                c.Line(x0 + 2, y + 1, x1 - 2, y + 1, 1f, Palette.BloodDark);
            }
            return c;
        }

        public static Canvas Crack(int seed)
        {
            var c = new Canvas(64, 64, new Color(0, 0, 0, 0));
            var rng = new System.Random(seed);
            for (int b = 0; b < 4; b++)
            {
                float x = 32, y = 32;
                float a = (float)(rng.NextDouble() * Mathf.PI * 2);
                for (int s = 0; s < 7; s++)
                {
                    a += (float)(rng.NextDouble() - 0.5) * 1.2f;
                    float nx = x + Mathf.Cos(a) * 5, ny = y + Mathf.Sin(a) * 5;
                    c.Line(x, y, nx, ny, 2f - s * 0.2f, new Color(0.03f, 0.02f, 0.02f, 1f));
                    x = nx; y = ny;
                }
            }
            c.Disc(32, 32, 3, Palette.BloodDark);
            return c;
        }

        public static Canvas EyeBags()
        {
            var c = new Canvas(64, 32, new Color(0, 0, 0, 0));
            c.Disc(18, 16, 11, Palette.Hex("2A1C26").WithAlpha(0.9f), 4f);
            c.Disc(46, 16, 11, Palette.Hex("2A1C26").WithAlpha(0.9f), 4f);
            c.Line(18, 6, 16, 0, 2f, Palette.Ash.WithAlpha(0.8f)); // traces de larmes
            c.Line(46, 6, 48, 0, 2f, Palette.Ash.WithAlpha(0.8f));
            return c;
        }

        // ------------------------------------------------------------------ Icônes & particules

        public static Canvas DropIcon()
        {
            var c = new Canvas(64, 64, new Color(1, 1, 1, 0));
            c.Disc(32, 24, 16, Color.white, 1.2f);
            for (int y = 24; y < 58; y++)
            {
                float half = Mathf.Lerp(16f, 0f, (y - 24) / 34f);
                for (int x = (int)(32 - half); x <= 32 + half; x++) c.Set(x, y, Color.white);
            }
            return c;
        }

        public static Canvas FlameIcon()
        {
            var c = new Canvas(64, 64, new Color(1, 1, 1, 0));
            c.Disc(32, 22, 14, Color.white, 1.2f);
            for (int y = 22; y < 60; y++)
            {
                float half = Mathf.Lerp(14f, 0f, Mathf.Pow((y - 22) / 38f, 0.8f));
                float off = Mathf.Sin(y * 0.15f) * 2f;
                for (int x = (int)(32 - half + off); x <= 32 + half + off; x++) c.Set(x, y, Color.white);
            }
            return c;
        }

        public static Canvas SoftDot(int size = 32)
        {
            var c = new Canvas(size, size, new Color(1, 1, 1, 0));
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    c.Set(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            return c;
        }

        public static Canvas Flat(int size, Color col) => new Canvas(size, size, col);
    }
}
