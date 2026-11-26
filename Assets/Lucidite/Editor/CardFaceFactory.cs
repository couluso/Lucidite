using System;
using System.Collections.Generic;
using UnityEngine;
using Canvas = Lucidite.EditorTools.TextureFactory.Canvas;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Design des cartes : faces par type (fond noir, contour extérieur, bordure intérieure arrondie),
    /// rond du coût / de la valeur, une grande icône par carte (contour épais, blanc, teinté par le type)
    /// et les petites icônes de bonus (pleines, en couleur). Origine des textures en bas à gauche.
    /// </summary>
    public static class CardFaceFactory
    {
        // Repères de la face, en pixels (256 px = largeur de la carte, origine en bas à gauche).
        // Le prefab (PrefabFactory.BuildCard) et la carte UI placent les textes et les ronds aux mêmes endroits.
        public const int FaceW = 256, FaceH = 384;
        /// <summary>Rayon des coins de la carte, en fraction de sa largeur (repris pour le dos et le contour lumineux).</summary>
        public const float CornerFrac = 0.07f;
        const float Outer = 7f, InnerInset = 17f, InnerRadius = 18f, InnerThick = 3f;
        // Grande icône (centre, taille) et rangée des bonus (premier centre, pas, taille)
        const float IconCX = 128f, IconCY = 243f, IconSize = 118f;
        const float StatX0 = 37f, StatY = 40f, StatStep = 29f, StatSize = 27f;

        public static string IconPath(string id) => $"{AssetFactory.TexDir}/CardIcons/{id}.png";
        public static string FacePath(string id) => $"{AssetFactory.TexDir}/CardFaces/Cards/{id}.png";

        /// <summary>Génère et sauvegarde l'icône centrale seule (panneau d'examen).</summary>
        public static Texture2D SaveIcon(string id)
        {
            AssetFactory.EnsureFolder(AssetFactory.TexDir + "/CardIcons");
            return TextureFactory.Save(Icon(id), IconPath(id), point: false, repeat: false, alpha: true, mips: true);
        }

        /// <summary>
        /// Face complète d'une carte : fond, contours, grande icône et icônes de bonus cuits dans une seule texture
        /// opaque (des quads découpés à l'alpha disparaissaient de loin : les mipmaps « mangeaient » les traits).
        /// </summary>
        public static Texture2D SaveFace(string id, string iconId, Color col, bool enemy, List<CardStat> stats)
        {
            AssetFactory.EnsureFolder(AssetFactory.TexDir + "/CardFaces");
            AssetFactory.EnsureFolder(AssetFactory.TexDir + "/CardFaces/Cards");
            var c = Face(col, enemy);
            // Icône teintée de la couleur du type, légèrement éclaircie pour ressortir sur le noir
            var ink = Color.Lerp(col, Color.white, 0.12f);
            Stamp(c, Icon(iconId), IconCX, IconCY, IconSize, ink);
            if (stats != null)
                for (int i = 0; i < stats.Count && i < CardFace.MaxStats; i++)
                    Stamp(c, StatIcon(stats[i]), StatX0 + i * StatStep, StatY, StatSize, null);
            return TextureFactory.Save(c, FacePath(id), point: false, repeat: false, alpha: true, mips: true);
        }

        /// <summary>Colle une image (redimensionnée) centrée en (cx, cy). tint : teinte d'une icône blanche.</summary>
        static void Stamp(Canvas dst, Canvas src, float cx, float cy, float size, Color? tint)
        {
            int x0 = Mathf.FloorToInt(cx - size / 2f), y0 = Mathf.FloorToInt(cy - size / 2f), n = Mathf.CeilToInt(size);
            float k = src.w / size;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 3x3 échantillons par pixel : réduction propre de l'image source
                    Color acc = new Color(0, 0, 0, 0);
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 3f) * k, v = (y + (sy + 0.5f) / 3f) * k;
                            var q = src.Get(Mathf.Clamp((int)u, 0, src.w - 1), Mathf.Clamp((int)v, 0, src.h - 1));
                            if (u >= src.w || v >= src.h) q = new Color(0, 0, 0, 0);
                            acc += new Color(q.r * q.a, q.g * q.a, q.b * q.a, q.a);
                        }
                    acc /= 9f;
                    if (acc.a <= 0.001f) continue;
                    var rgb = new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, 1f);
                    if (tint.HasValue) rgb = new Color(rgb.r * tint.Value.r, rgb.g * tint.Value.g, rgb.b * tint.Value.b, 1f);
                    int px = x0 + x, py = y0 + y;
                    var d = dst.Get(px, py);
                    var o = Color.Lerp(d, rgb, acc.a);
                    o.a = d.a;
                    dst.Set(px, py, o);
                }
        }

        // ================================================================== Face

        public static Canvas Face(Color col, bool enemy)
        {
            var c = new Canvas(FaceW, FaceH, Color.black);
            var bg = enemy ? Palette.Hex("170B0B") : Palette.Hex("121111");
            float cx = FaceW / 2f, cy = FaceH / 2f;
            float hx = cx - InnerInset, hy = cy - InnerInset;
            float corner = CornerFrac * FaceW;
            for (int y = 0; y < FaceH; y++)
                for (int x = 0; x < FaceW; x++)
                {
                    float px = x + 0.5f - cx, py = y + 0.5f - cy;
                    float n = Mathf.PerlinNoise(x * 0.07f + (enemy ? 40f : 0f), y * 0.07f);
                    float grain = (Hash(x, y) - 0.5f) * 0.025f;
                    var p = bg * (0.85f + n * 0.3f);
                    // Halo très léger de la couleur du type le long de la bordure intérieure
                    float d = SdRoundBox(px, py, hx, hy, InnerRadius);
                    p = Color.Lerp(p, col * 0.5f, 0.14f * Mathf.Exp(-Mathf.Abs(d) / 9f));
                    p += new Color(grain, grain, grain, 0f);
                    // Bordure intérieure (fine, coins arrondis)
                    float a = Mathf.Clamp01(InnerThick * 0.5f - Mathf.Abs(d) + 0.5f);
                    p = Color.Lerp(p, Worn(col, x, y), a);
                    // Contour extérieur, qui suit les coins arrondis de la carte
                    float e = SdRoundBox(px, py, cx, cy, corner);
                    float border = Mathf.Clamp01(e + Outer + 0.5f);
                    p = Color.Lerp(p, Worn(col, x, y), border);
                    p.a = Mathf.Clamp01(0.5f - e); // coins arrondis : transparents (découpe alpha du matériau)
                    c.Set(x, y, p);
                }
            return c;
        }

        /// <summary>Rend transparents les coins d'une image (dos de carte, contour lumineux).</summary>
        public static Canvas RoundCorners(Canvas c, float radiusFrac)
        {
            float cx = c.w / 2f, cy = c.h / 2f, r = radiusFrac * c.w;
            for (int y = 0; y < c.h; y++)
                for (int x = 0; x < c.w; x++)
                {
                    float e = SdRoundBox(x + 0.5f - cx, y + 0.5f - cy, cx, cy, r);
                    var p = c.Get(x, y);
                    p.a = Mathf.Min(p.a, Mathf.Clamp01(0.5f - e));
                    c.Set(x, y, p);
                }
            return c;
        }

        static Color Worn(Color col, int x, int y)
        {
            float n = Mathf.PerlinNoise(x * 0.21f + 7.3f, y * 0.21f + 1.1f);
            return col * (0.8f + n * 0.25f);
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }

        static float SdRoundBox(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - (hx - r), qy = Mathf.Abs(py) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>
        /// Masque de sang d'une carte (128 x 192) pour le shader PSXLit (_DamageMap) : RGB = couleur du sang,
        /// A = seuil d'apparition. Les valeurs sont égalisées : à _Damage = 0.4, ~40 % de la carte est couverte.
        /// Taches qui s'étendent depuis quelques impacts + gouttelettes isolées ; le sang le plus ancien est plus sombre.
        /// </summary>
        public static Canvas BloodMask(int seed)
        {
            const int W = 128, H = 192;
            var rng = new System.Random(seed * 7919 + 13);
            float R() => (float)rng.NextDouble();
            int nSplat = 6 + rng.Next(4);
            var splats = new Vector3[nSplat];
            for (int i = 0; i < nSplat; i++) splats[i] = new Vector3(R() * W, R() * H, 14f + R() * 30f);
            var specks = new Vector3[40];
            for (int i = 0; i < specks.Length; i++) specks[i] = new Vector3(R() * W, R() * H, 1.2f + R() * 2.8f);
            float ox = R() * 100f, oy = R() * 100f;

            var field = new float[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float f = 0f;
                    // Bords déchiquetés : la distance aux impacts est déformée par du bruit
                    float wob = (Mathf.PerlinNoise(x * 0.09f + ox, y * 0.09f + oy) - 0.5f) * 22f;
                    foreach (var s in splats)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(s.x, s.y)) + wob;
                        f = Mathf.Max(f, Mathf.Exp(-(d * d) / (s.z * s.z)));
                    }
                    foreach (var s in specks)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(s.x, s.y));
                        if (d < s.z) f = Mathf.Max(f, 0.75f + 0.2f * (1f - d / s.z));
                    }
                    f += Mathf.PerlinNoise(x * 0.05f + oy, y * 0.05f + ox) * 0.35f;
                    field[y * W + x] = f;
                }
            // Égalisation : rang de chaque pixel -> seuil uniforme
            var order = new int[field.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => field[a].CompareTo(field[b]));
            var c = new Canvas(W, H, Color.black);
            var fresh = Palette.Hex("7A0A12");
            var dried = Palette.Hex("2E0406");
            for (int r = 0; r < order.Length; r++)
            {
                int i = order[r];
                float a = Mathf.Max(0.006f, (r + 1f) / order.Length);
                int x = i % W, y = i / W;
                float n = Mathf.PerlinNoise(x * 0.2f + ox, y * 0.2f);
                var col = Color.Lerp(fresh, dried, a * 0.85f) * (0.8f + n * 0.4f);
                col.a = a;
                c.Set(x, y, col);
            }
            return c;
        }

        /// <summary>Rond blanc (teinté par le type) cerclé de noir pour se détacher de la bordure.</summary>
        public static Canvas Orb()
        {
            int s = 64;
            var c = new Canvas(s, s, new Color(0, 0, 0, 0));
            float m = s / 2f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - m) * (x + 0.5f - m) + (y + 0.5f - m) * (y + 0.5f - m));
                    float outer = Mathf.Clamp01(31f - d + 0.5f);
                    if (outer <= 0f) continue;
                    float inner = Mathf.Clamp01(27f - d + 0.5f);
                    float shade = Mathf.Lerp(0.78f, 1f, (y + 0.5f) / s); // un peu plus sombre en bas
                    var col = Color.Lerp(new Color(0.04f, 0.035f, 0.035f), Color.white * shade, inner);
                    col.a = outer;
                    c.Set(x, y, col);
                }
            return c;
        }

        // ================================================================== Icônes de bonus (pleines)

        public static Color StatColor(CardStat s)
        {
            switch (s)
            {
                case CardStat.Force: return Palette.Hex("B8902E");          // étoile dorée
                case CardStat.Faiblesse: return Palette.Hex("7E9A5A");      // flèche vers le bas, vert maladif
                case CardStat.Vulnerable: return Palette.Hex("A35A8A");     // losange fissuré
                case CardStat.Pioche: return Palette.Hex("6FA3A0");         // carte « + »
                case CardStat.Energie: return Palette.Hex("D2BE62");        // éclair
                case CardStat.Ponction: return Palette.Hex("8A3A30");       // éclair barré
                case CardStat.Soin: return Palette.Hex("5E9E62");           // croix
                case CardStat.Sang: return Palette.Hex("9E1E28");           // goutte
                case CardStat.Bouclier: return Palette.Hex("4F7FA8");       // écu
                case CardStat.Degats: return Palette.Hex("B0303A");         // dague
                case CardStat.Epuiser: return Palette.Hex("C06A2A");        // flamme
                case CardStat.Ephemere: return Palette.Hex("9C94C8");       // croissant de lune
                case CardStat.Crise: return Palette.Hex("E0DCD4");          // œil
                case CardStat.Retenue: return Palette.Hex("8E8A86");        // sablier
                case CardStat.LuciditeBasse: return Palette.Bone;           // bougie
                default: return Palette.Hex("D8B8C0");                      // Purge : étincelle
            }
        }

        public static Canvas StatIcon(CardStat s)
        {
            const int S = 48;
            var c = new Canvas(S, S, new Color(0, 0, 0, 0));
            Func<Vector2, float> sd;
            switch (s)
            {
                case CardStat.Force: sd = p => SdPoly(p, Star(24, 24, 21, 9, 5)); break;
                case CardStat.Faiblesse: sd = p => SdPoly(p, V(24, 4, 43, 23, 31, 23, 31, 44, 17, 44, 17, 23, 5, 23)); break;
                case CardStat.Vulnerable:
                    sd = p => Mathf.Max(SdPoly(p, V(24, 3, 45, 24, 24, 45, 3, 24)), -(SdPolyline(p, V(24, 46, 20, 32, 28, 22, 22, 8)) - 1.7f)); break;
                case CardStat.Pioche:
                    sd = p => Mathf.Max(SdRoundBox(p.x - 24, p.y - 24, 12, 18, 3),
                        -Mathf.Min(SdRoundBox(p.x - 24, p.y - 24, 6.5f, 1.8f, 0.5f), SdRoundBox(p.x - 24, p.y - 24, 1.8f, 6.5f, 0.5f))); break;
                case CardStat.Energie: sd = p => SdPoly(p, Bolt); break;
                case CardStat.Ponction: sd = p => Mathf.Max(SdPoly(p, Bolt), -(SdPolyline(p, V(6, 42, 42, 6)) - 2.2f)); break;
                case CardStat.Soin: sd = p => Mathf.Min(SdRoundBox(p.x - 24, p.y - 24, 6.5f, 19, 1), SdRoundBox(p.x - 24, p.y - 24, 19, 6.5f, 1)); break;
                case CardStat.Sang: sd = p => Mathf.Min(Circle(p, 24, 17, 12), SdPoly(p, V(13.5f, 23, 24, 45, 34.5f, 23))); break;
                case CardStat.Bouclier: sd = p => SdPoly(p, V(24, 45, 42, 39, 40, 18, 24, 3, 8, 18, 6, 39)); break;
                case CardStat.Degats:
                    sd = p => Mathf.Min(SdPoly(p, V(24, 46, 30, 20, 18, 20)),
                        Mathf.Min(SdRoundBox(p.x - 24, p.y - 17, 11, 2.5f, 1), SdRoundBox(p.x - 24, p.y - 8, 3, 6, 1))); break;
                case CardStat.Epuiser: sd = p => SdPoly(p, V(24, 46, 30, 37, 37, 27, 37, 14, 30, 5, 18, 5, 11, 14, 11, 27, 17, 36, 21, 30)); break;
                case CardStat.Ephemere: sd = p => Mathf.Max(Circle(p, 24, 24, 20), -Circle(p, 33, 31, 16)); break;
                case CardStat.Crise:
                    sd = p => Mathf.Max(Mathf.Max(Circle(p, 24, 4, 30), Circle(p, 24, 44, 30)), -Circle(p, 24, 24, 5.5f)); break;
                case CardStat.Retenue: sd = p => SdPoly(p, V(8, 45, 40, 45, 26, 24, 40, 3, 8, 3, 22, 24)); break;
                case CardStat.LuciditeBasse:
                    sd = p => Mathf.Min(SdRoundBox(p.x - 24, p.y - 14, 7, 11, 1.5f), Mathf.Min(Circle(p, 24, 33, 5), SdPoly(p, V(19.6f, 35, 24, 46, 28.4f, 35)))); break;
                default: sd = p => SdPoly(p, V(24, 46, 28.5f, 28.5f, 46, 24, 28.5f, 19.5f, 24, 2, 19.5f, 19.5f, 2, 24, 19.5f, 28.5f)); break;
            }
            var col = StatColor(s);
            var rim = new Color(0.03f, 0.025f, 0.025f, 1f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = sd(new Vector2(x + 0.5f, y + 0.5f));
                    float aRim = Mathf.Clamp01(2.6f - d + 0.5f);
                    if (aRim <= 0f) continue;
                    float aIn = Mathf.Clamp01(0.5f - d);
                    // Léger dégradé vertical pour garder un peu de volume
                    var fill = col * Mathf.Lerp(0.8f, 1.08f, (y + 0.5f) / S);
                    var p = Color.Lerp(rim, fill, aIn);
                    p.a = aRim;
                    c.Set(x, y, p);
                }
            return c;
        }

        static readonly Vector2[] Bolt = V(30, 46, 10, 21, 22, 21, 16, 2, 38, 28, 26, 28);

        // ================================================================== Classes (cœur, souffle, regard, main)

        // Symbole de classe : sous le rond de coût, la partie de la carte qui reste visible quand la main est en éventail
        const float SuitCX = 38f, SuitCY = 297f, SuitSize = 34f;

        static float SdCapsule(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            Vector2 a = new Vector2(ax, ay), ab = new Vector2(bx, by) - a, ap = p - a;
            float h = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
            return (ap - ab * h).magnitude - r;
        }

        static float SdEllipse(Vector2 p, float cx, float cy, float rx, float ry)
        {
            var q = new Vector2((p.x - cx) / rx, (p.y - cy) / ry);
            return (q.magnitude - 1f) * Mathf.Min(rx, ry);
        }

        /// <summary>Forme pleine d'une classe (distance signée, toile 128 x 128).</summary>
        static float SuitShape(CardSuit s, Vector2 p)
        {
            switch (s)
            {
                case CardSuit.Coeur:
                {
                    // Cœur anatomique : masse inclinée, aorte en crosse et vaisseaux
                    var body = V(58, 10, 84, 22, 100, 48, 98, 74, 82, 90, 58, 94, 38, 86, 28, 64, 34, 38);
                    float d = SdPoly(p, body) - 4f;
                    d = Mathf.Min(d, SdCapsule(p, 52, 86, 48, 116, 7.5f));
                    d = Mathf.Min(d, SdCapsule(p, 64, 90, 84, 112, 7f));
                    d = Mathf.Min(d, SdCapsule(p, 84, 112, 96, 98, 6f));
                    d = Mathf.Min(d, SdCapsule(p, 72, 92, 72, 118, 5f));
                    d = Mathf.Max(d, -SdCapsule(p, 46, 44, 70, 78, 2.2f)); // sillon
                    return d;
                }
                case CardSuit.Souffle:
                {
                    float d = SdEllipse(p, 38, 52, 22, 36);
                    d = Mathf.Min(d, SdEllipse(p, 90, 52, 22, 36));
                    d = Mathf.Max(d, -SdCapsule(p, 64, 20, 64, 70, 9f)); // espace entre les lobes
                    d = Mathf.Min(d, SdCapsule(p, 64, 122, 64, 78, 6.5f));
                    d = Mathf.Min(d, SdCapsule(p, 64, 80, 46, 64, 5f));
                    d = Mathf.Min(d, SdCapsule(p, 64, 80, 82, 64, 5f));
                    return d;
                }
                case CardSuit.Regard:
                {
                    // Amande (deux disques qui se croisent), iris évidé, pupille pleine, cils
                    float lens = Mathf.Max(Circle(p, 64, 64 - 46, 70), Circle(p, 64, 64 + 46, 70));
                    float ring = Mathf.Abs(Circle(p, 64, 64, 15f)) - 5f;
                    float d = Mathf.Max(lens, -ring);
                    d = Mathf.Max(d, -(Mathf.Abs(lens + 5f) - 2f)); // liseré intérieur
                    foreach (var (x, y) in new[] { (40f, 96f), (64f, 104f), (88f, 96f) }) d = Mathf.Min(d, SdCapsule(p, x, y - 10f, x + (x - 64f) * 0.25f, y, 3.5f));
                    return d;
                }
                default:
                {
                    // Main ouverte, paume vers soi
                    float d = SdPoly(p, V(40, 18, 88, 18, 92, 62, 36, 62)) - 6f;
                    d = Mathf.Min(d, SdCapsule(p, 44, 62, 40, 100, 7f));
                    d = Mathf.Min(d, SdCapsule(p, 57, 64, 56, 114, 7f));
                    d = Mathf.Min(d, SdCapsule(p, 70, 64, 72, 112, 7f));
                    d = Mathf.Min(d, SdCapsule(p, 84, 60, 90, 96, 6.5f));
                    d = Mathf.Min(d, SdCapsule(p, 38, 36, 16, 62, 7.5f));
                    return d;
                }
            }
        }

        /// <summary>Symbole plein d'une classe (blanc sur transparent, 128 x 128).</summary>
        public static Canvas SuitIcon(CardSuit s)
        {
            var c = new Canvas(128, 128, new Color(1f, 1f, 1f, 0f));
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float a = Mathf.Clamp01(0.5f - SuitShape(s, new Vector2(x + 0.5f, y + 0.5f)) * 0.8f);
                    if (a > 0f) c.Set(x, y, new Color(1f, 1f, 1f, a));
                }
            return c;
        }

        /// <summary>
        /// Calque de classe de la face (256 x 384, RGBA) : symbole sous le coût + motif de fond très discret
        /// (veines, alvéoles, iris, empreinte), absent de la zone de la grande icône. Mêlé à la face par le shader.
        /// </summary>
        public static Canvas SuitOverlay(CardSuit s)
        {
            var motif = new Canvas(FaceW, FaceH, new Color(1f, 1f, 1f, 0f));
            var rng = new System.Random(17 + (int)s * 31);
            float R() => (float)rng.NextDouble();
            var ink = Color.white;
            switch (s)
            {
                case CardSuit.Coeur:
                    // Veines qui se ramifient depuis les bords
                    void Vein(Vector2 p, Vector2 dir, float w, int depth)
                    {
                        for (int i = 0; i < 26 && w > 0.5f; i++)
                        {
                            dir = (Quaternion.Euler(0, 0, (R() - 0.5f) * 40f) * dir).normalized;
                            var q = p + dir * 7f;
                            motif.Line(p.x, p.y, q.x, q.y, w, ink);
                            p = q; w *= 0.965f;
                            if (depth < 3 && R() < 0.12f) Vein(p, Quaternion.Euler(0, 0, (R() < 0.5f ? -1f : 1f) * (30f + R() * 30f)) * dir, w * 0.7f, depth + 1);
                        }
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        bool left = i % 2 == 0;
                        Vein(new Vector2(left ? 20f : FaceW - 20f, 30f + R() * 320f), new Vector2(left ? 1f : -1f, (R() - 0.5f)), 3.2f, 0);
                    }
                    break;
                case CardSuit.Souffle:
                    // Alvéoles en grappes
                    for (int g = 0; g < 16; g++)
                    {
                        var cpt = new Vector2(24f + R() * (FaceW - 48f), 24f + R() * (FaceH - 48f));
                        int n = 4 + rng.Next(5);
                        for (int i = 0; i < n; i++)
                        {
                            float a = R() * Mathf.PI * 2f, dd = R() * 13f;
                            motif.Ring(cpt.x + Mathf.Cos(a) * dd, cpt.y + Mathf.Sin(a) * dd, 3.5f + R() * 4f, 1.6f, ink);
                        }
                    }
                    break;
                case CardSuit.Regard:
                    // Petites amandes qui regardent, en quinconce
                    for (int gy = 0; gy < 9; gy++)
                        for (int gx = 0; gx < 6; gx++)
                        {
                            float cx = 22f + gx * 43f + (gy % 2) * 21f, cy = 26f + gy * 42f;
                            for (int k = -1; k <= 1; k += 2)
                            {
                                float px = 0, py = 0;
                                for (int i = 0; i <= 12; i++)
                                {
                                    float t = i / 12f, x = cx - 9f + t * 18f, y = cy + k * Mathf.Sin(t * Mathf.PI) * 5f;
                                    if (i > 0) motif.Line(px, py, x, y, 1.5f, ink);
                                    px = x; py = y;
                                }
                            }
                            motif.Disc(cx, cy, 2.2f, ink);
                        }
                    break;
                default:
                    // Empreinte digitale : arcs concentriques ondulés
                    foreach (var o in new[] { new Vector2(40f, 70f), new Vector2(210f, 330f) })
                        for (int r = 8; r < 170; r += 7)
                        {
                            float px = 0, py = 0;
                            for (int i = 0; i <= 90; i++)
                            {
                                float a = i / 90f * Mathf.PI * 2f, rr = r + Mathf.Sin(a * 3f + r * 0.3f) * 1.6f;
                                float x = o.x + Mathf.Cos(a) * rr, y = o.y + Mathf.Sin(a) * rr * 1.25f;
                                if (i > 0 && (i + r) % 23 != 0) motif.Line(px, py, x, y, 1.4f, ink);
                                px = x; py = y;
                            }
                        }
                    break;
            }

            var c = new Canvas(FaceW, FaceH, new Color(0f, 0f, 0f, 0f));
            var motifCol = Palette.Hex("8C8273");
            float hx = FaceW / 2f - InnerInset - 4f, hy = FaceH / 2f - InnerInset - 4f;
            for (int y = 0; y < FaceH; y++)
                for (int x = 0; x < FaceW; x++)
                {
                    float a = motif.Get(x, y).a;
                    if (a <= 0f) continue;
                    // À l'intérieur de la bordure, loin de la grande icône et du symbole
                    float px = x + 0.5f - FaceW / 2f, py = y + 0.5f - FaceH / 2f;
                    float inside = Mathf.Clamp01(-SdRoundBox(px, py, hx, hy, InnerRadius) / 4f);
                    float icon = Mathf.Clamp01((Vector2.Distance(new Vector2(x, y), new Vector2(IconCX, IconCY)) - 66f) / 14f);
                    float suit = Mathf.Clamp01((Vector2.Distance(new Vector2(x, y), new Vector2(SuitCX, SuitCY)) - 22f) / 6f);
                    float k = a * 0.13f * inside * icon * suit;
                    if (k > 0f) c.Set(x, y, motifCol.WithAlpha(k));
                }
            // Symbole (os clair sur un petit disque sombre qui le détache de la bordure)
            var icn = SuitIcon(s);
            float k0 = icn.w / SuitSize;
            int x0 = Mathf.FloorToInt(SuitCX - SuitSize / 2f), y0 = Mathf.FloorToInt(SuitCY - SuitSize / 2f);
            var bone = Palette.Hex("D8CDB6");
            c.Disc(SuitCX, SuitCY, SuitSize * 0.68f, Palette.Hex("120E10").WithAlpha(0.55f), 5f);
            for (int y = 0; y < SuitSize; y++)
                for (int x = 0; x < SuitSize; x++)
                {
                    float acc = 0f; // suréchantillonnage 3 x 3
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            acc += icn.Get(Mathf.FloorToInt((x + (sx + 0.5f) / 3f) * k0), Mathf.FloorToInt((y + (sy + 0.5f) / 3f) * k0)).a;
                    acc /= 9f;
                    if (acc <= 0f) continue;
                    var d = c.Get(x0 + x, y0 + y);
                    var o = Color.Lerp(d, bone, acc);
                    o.a = Mathf.Max(d.a, acc);
                    c.Set(x0 + x, y0 + y, o);
                }
            return c;
        }

        public static Texture2D SaveSuitOverlay(CardSuit s) =>
            TextureFactory.Save(SuitOverlay(s), $"{AssetFactory.TexDir}/CardFaces/Suit_{s}.png", point: false, repeat: false, alpha: true, mips: true);

        public static Texture2D SaveSuitIcon(CardSuit s) =>
            TextureFactory.Save(SuitIcon(s), $"{AssetFactory.TexDir}/CardFaces/SuitIcon_{s}.png", point: false, repeat: false, alpha: true, mips: true);

        // ================================================================== Grandes icônes (contour)

        /// <summary>Tracé au pinceau rond sur fond transparent (blanc, teinté par le matériau).</summary>
        class Pen
        {
            readonly Canvas c;
            public float t = 8f;
            static readonly Color Ink = Color.white;
            public Pen(Canvas c) { this.c = c; }

            public void L(float x0, float y0, float x1, float y1) => c.Line(x0, y0, x1, y1, t, Ink);
            public void Dot(float x, float y, float r) => c.Disc(x, y, r, Ink);

            public void Poly(bool closed, params float[] xy)
            {
                int n = xy.Length / 2;
                for (int i = 0; i < n - 1; i++) L(xy[i * 2], xy[i * 2 + 1], xy[i * 2 + 2], xy[i * 2 + 3]);
                if (closed) L(xy[n * 2 - 2], xy[n * 2 - 1], xy[0], xy[1]);
            }

            public void Pts(bool closed, Vector2[] p)
            {
                for (int i = 0; i < p.Length - 1; i++) L(p[i].x, p[i].y, p[i + 1].x, p[i + 1].y);
                if (closed) L(p[p.Length - 1].x, p[p.Length - 1].y, p[0].x, p[0].y);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, float a0 = 0f, float a1 = 360f)
            {
                int n = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 360f * 72f));
                float px = 0, py = 0;
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                    float x = cx + Mathf.Cos(a) * rx, y = cy + Mathf.Sin(a) * ry;
                    if (i > 0) L(px, py, x, y);
                    px = x; py = y;
                }
            }

            public void Arc(float cx, float cy, float r, float a0 = 0f, float a1 = 360f) => Ellipse(cx, cy, r, r, a0, a1);

            public void Bez(float x0, float y0, float x1, float y1, float x2, float y2)
            {
                float px = x0, py = y0;
                for (int i = 1; i <= 24; i++)
                {
                    float u = i / 24f, v = 1f - u;
                    float x = v * v * x0 + 2f * v * u * x1 + u * u * x2;
                    float y = v * v * y0 + 2f * v * u * y1 + u * u * y2;
                    L(px, py, x, y);
                    px = x; py = y;
                }
            }

            /// <summary>Courbe lisse (Catmull-Rom) passant par les points.</summary>
            public void Curve(bool closed, params float[] xy)
            {
                var p = V(xy);
                int n = p.Length, segs = closed ? n : n - 1;
                Vector2 prev = p[0];
                for (int s = 0; s < segs; s++)
                {
                    Vector2 p0 = p[closed ? (s - 1 + n) % n : Mathf.Max(s - 1, 0)], p1 = p[s % n],
                            p2 = p[(s + 1) % n], p3 = p[closed ? (s + 2) % n : Mathf.Min(s + 2, n - 1)];
                    for (int i = 1; i <= 12; i++)
                    {
                        float u = i / 12f, u2 = u * u, u3 = u2 * u;
                        var q = 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
                        L(prev.x, prev.y, q.x, q.y);
                        prev = q;
                    }
                }
            }

            public void RRect(float x0, float y0, float x1, float y1, float r)
            {
                L(x0 + r, y0, x1 - r, y0); L(x1, y0 + r, x1, y1 - r); L(x1 - r, y1, x0 + r, y1); L(x0, y1 - r, x0, y0 + r);
                Arc(x1 - r, y0 + r, r, 270, 360); Arc(x1 - r, y1 - r, r, 0, 90); Arc(x0 + r, y1 - r, r, 90, 180); Arc(x0 + r, y0 + r, r, 180, 270);
            }

            public void Rect(float x0, float y0, float x1, float y1) => Poly(true, x0, y0, x1, y0, x1, y1, x0, y1);

            /// <summary>Contour d'une forme définie par une distance signée (unions de cercles...).</summary>
            public void Sdf(Func<Vector2, float> sd)
            {
                for (int y = 0; y < c.h; y++)
                    for (int x = 0; x < c.w; x++)
                    {
                        float a = Mathf.Clamp01(t * 0.5f - Mathf.Abs(sd(new Vector2(x + 0.5f, y + 0.5f))) + 0.5f);
                        if (a > 0f) c.Blend(x, y, Ink.WithAlpha(a));
                    }
            }

            public void Heart(float cx, float cy, float s)
            {
                var p = new Vector2[64];
                for (int i = 0; i < p.Length; i++)
                {
                    float a = i / (float)p.Length * Mathf.PI * 2f;
                    float x = 16f * Mathf.Pow(Mathf.Sin(a), 3f);
                    float y = 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a);
                    p[i] = new Vector2(cx + x * s, cy + y * s);
                }
                Pts(true, p);
            }

            public void Star4(float cx, float cy, float r)
            {
                var p = new Vector2[8];
                for (int i = 0; i < 8; i++)
                {
                    float a = (90f + i * 45f) * Mathf.Deg2Rad, rr = i % 2 == 0 ? r : r * 0.38f;
                    p[i] = new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr);
                }
                Pts(true, p);
            }

            public void Eye(float x, float y, float w, float h)
            {
                Bez(x - w, y, x, y + h * 2f, x + w, y);
                Bez(x - w, y, x, y - h * 2f, x + w, y);
                Arc(x, y, h * 0.6f);
            }

            /// <summary>Entaille avec points de suture perpendiculaires.</summary>
            public void Scar(float x0, float y0, float x1, float y1)
            {
                float keep = t;
                L(x0, y0, x1, y1);
                var d = new Vector2(x1 - x0, y1 - y0).normalized;
                var n = new Vector2(-d.y, d.x) * 9f;
                t = 4f;
                foreach (float f in new[] { 0.3f, 0.55f, 0.8f })
                {
                    var m = new Vector2(Mathf.Lerp(x0, x1, f), Mathf.Lerp(y0, y1, f));
                    L(m.x - n.x, m.y - n.y, m.x + n.x, m.y + n.y);
                }
                t = keep;
            }
        }

        public static Canvas Icon(string id)
        {
            var c = new Canvas(128, 128, new Color(1f, 1f, 1f, 0f));
            var p = new Pen(c);
            switch (id)
            {
                // ---------------- Départ
                case "P_SeLever": // lit + flèche vers le haut
                    p.L(18, 22, 18, 78); p.RRect(18, 28, 110, 46, 6); p.Ellipse(38, 56, 13, 6);
                    p.L(80, 58, 80, 112); p.L(64, 96, 80, 112); p.L(96, 96, 80, 112);
                    break;
                case "P_Respirer": // poumons
                    p.Ellipse(42, 56, 17, 32); p.Ellipse(86, 56, 17, 32);
                    p.L(64, 114, 64, 80); p.L(64, 80, 52, 68); p.L(64, 80, 76, 68);
                    break;
                case "P_Crier": // bouche ouverte et ondes
                    p.Ellipse(46, 64, 20, 28); p.Arc(46, 64, 44, -35, 35); p.Arc(46, 64, 60, -30, 30);
                    break;

                // ---------------- Attaques
                case "S_MarcherDehors": // empreintes de pas
                    p.Ellipse(42, 60, 13, 20); p.Ellipse(42, 24, 10, 8);
                    p.Ellipse(86, 96, 13, 20); p.Ellipse(86, 60, 10, 8);
                    break;
                case "S_PoingsSerres": // poing serré
                    p.RRect(26, 22, 102, 62, 12); p.RRect(26, 62, 102, 96, 10);
                    p.L(45, 96, 45, 68); p.L(64, 96, 64, 68); p.L(83, 96, 83, 68);
                    p.Bez(26, 46, 56, 52, 62, 36); p.L(44, 22, 44, 8); p.L(84, 22, 84, 8);
                    break;
                case "S_NommerDouleur": // bulle de parole fissurée
                    p.RRect(16, 44, 112, 108, 16); p.Poly(false, 36, 44, 28, 18, 58, 44);
                    p.t = 6; p.Poly(false, 34, 76, 52, 88, 62, 64, 80, 86, 94, 72);
                    break;
                case "S_ColereSourde": // anneau dentelé
                    p.Pts(true, Star(64, 64, 50, 37, 9)); p.Arc(64, 64, 14);
                    break;
                case "S_RageDeVivre": // flamme
                    p.Curve(true, 64, 118, 86, 90, 96, 62, 88, 34, 64, 14, 40, 34, 32, 62, 46, 86);
                    p.Curve(true, 64, 72, 76, 50, 64, 30, 52, 50);
                    break;
                case "S_CriLiberateur": // chaîne qui se brise
                    p.Ellipse(32, 64, 22, 13, 40, 320); p.Ellipse(96, 64, 22, 13, 220, 500);
                    p.t = 6; p.L(60, 82, 54, 98); p.L(68, 82, 74, 98); p.L(64, 84, 64, 102);
                    p.L(60, 46, 54, 30); p.L(68, 46, 74, 30); p.L(64, 44, 64, 26);
                    break;
                case "S_Aube": // soleil levant
                    p.L(12, 44, 116, 44); p.Arc(64, 44, 26, 0, 180);
                    foreach (float a in new[] { 20f, 55f, 90f, 125f, 160f })
                    {
                        float r = a * Mathf.Deg2Rad;
                        p.L(64 + Mathf.Cos(r) * 38, 44 + Mathf.Sin(r) * 38, 64 + Mathf.Cos(r) * 54, 44 + Mathf.Sin(r) * 54);
                    }
                    p.t = 6; p.L(30, 28, 98, 28); p.L(46, 14, 82, 14);
                    break;

                // ---------------- Défenses
                case "S_Therapie": // nœud qui se dénoue (infini)
                {
                    var pts = new Vector2[96];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        float a = i / (float)pts.Length * Mathf.PI * 2f, s = Mathf.Sin(a), k = 1f + s * s;
                        pts[i] = new Vector2(64 + 50 * Mathf.Cos(a) / k, 64 + 50 * s * Mathf.Cos(a) / k);
                    }
                    p.Pts(true, pts);
                    break;
                }
                case "S_TenirBon": // chronomètre
                    p.Arc(64, 56, 40); p.RRect(54, 104, 74, 116, 3); p.L(64, 96, 64, 104);
                    p.L(64, 56, 64, 82); p.L(64, 56, 84, 46); p.L(96, 92, 104, 100);
                    break;
                case "S_Ancrage": // ancre
                    p.Arc(64, 102, 9); p.L(64, 93, 64, 22); p.L(44, 80, 84, 80);
                    p.Arc(64, 54, 32, 200, 340); p.L(34, 43, 24, 54); p.L(94, 43, 104, 54);
                    break;
                case "S_Couverture": // couverture sur le lit, lune
                    p.L(12, 28, 116, 28); p.Bez(22, 28, 30, 76, 62, 70); p.Bez(62, 70, 96, 64, 108, 28);
                    p.L(56, 50, 68, 34); p.L(82, 52, 90, 36);
                    p.t = 6; p.Arc(100, 102, 11, 90, 330);
                    break;
                case "S_Traitement": // gélule
                    p.L(25.3f, 50.7f, 77.3f, 102.7f); p.L(50.7f, 25.3f, 102.7f, 77.3f);
                    p.Arc(90, 90, 18, -45, 135); p.Arc(38, 38, 18, 135, 315); p.L(51.3f, 76.7f, 76.7f, 51.3f);
                    break;
                case "S_Reconstruire": // briques
                    p.t = 6;
                    p.Rect(16, 18, 64, 40); p.Rect(64, 18, 112, 40);
                    p.Rect(16, 40, 40, 62); p.Rect(40, 40, 88, 62); p.Rect(88, 40, 112, 62);
                    p.Rect(40, 78, 88, 100); p.L(64, 106, 64, 116);
                    break;
                case "S_Accepter": // lotus
                    p.Bez(64, 24, 40, 62, 64, 102); p.Bez(64, 24, 88, 62, 64, 102);
                    p.Bez(64, 24, 28, 28, 18, 70); p.Bez(18, 70, 50, 60, 64, 24);
                    p.Bez(64, 24, 100, 28, 110, 70); p.Bez(110, 70, 78, 60, 64, 24);
                    p.t = 6; p.L(24, 14, 104, 14);
                    break;

                // ---------------- Tourments
                case "S_Ecrire": // crayon
                    p.L(31.6f, 48.4f, 89.6f, 106.4f); p.L(44.4f, 35.6f, 102.4f, 93.6f); p.L(89.6f, 106.4f, 102.4f, 93.6f);
                    p.L(79.7f, 96.5f, 92.5f, 83.7f); p.L(31.6f, 48.4f, 22.4f, 26.4f); p.L(44.4f, 35.6f, 22.4f, 26.4f);
                    p.t = 5; p.Bez(40, 14, 70, 24, 110, 14);
                    break;
                case "S_AppelerAmie": // combiné de téléphone
                    p.Arc(64, 40, 40, 30, 150); p.RRect(12, 50, 40, 68, 6); p.RRect(88, 50, 116, 68, 6);
                    p.RRect(34, 12, 94, 34, 8);
                    p.t = 6; p.Arc(64, 40, 62, 62, 118); p.Arc(64, 40, 76, 72, 108);
                    break;
                case "S_Lucidite": // œil grand ouvert
                    p.Bez(12, 64, 64, 108, 116, 64); p.Bez(12, 64, 64, 20, 116, 64); p.Arc(64, 64, 17); p.Dot(64, 64, 5);
                    p.t = 6; p.L(64, 90, 64, 102); p.L(40, 84, 33, 94); p.L(88, 84, 95, 94);
                    break;
                case "S_SouvenirEte": // coquillage
                    p.Arc(64, 24, 52, 15, 165);
                    foreach (float a in new[] { 15f, 45f, 75f, 105f, 135f, 165f })
                        p.L(64, 24, 64 + Mathf.Cos(a * Mathf.Deg2Rad) * 52, 24 + Mathf.Sin(a * Mathf.Deg2Rad) * 52);
                    p.RRect(52, 10, 76, 24, 4);
                    break;
                case "S_SecondSouffle": // souffle de vent
                    p.L(14, 82, 80, 82); p.Arc(80, 94, 12, -90, 90);
                    p.L(14, 58, 96, 58); p.Arc(96, 46, 12, -90, 90);
                    p.L(30, 30, 72, 30);
                    break;
                case "S_SePardonner": // cœur recousu
                    p.Heart(64, 70, 3.1f);
                    p.t = 6; p.Poly(false, 64, 86, 54, 72, 70, 58, 58, 42, 64, 26);
                    p.t = 4; p.L(48, 76, 60, 82); p.L(62, 52, 76, 60);
                    break;

                // ---------------- Crises
                case "C_Doute": // point d'interrogation
                    p.t = 9; p.Arc(64, 80, 24, -60, 180); p.L(76, 59, 64, 48); p.L(64, 48, 64, 38); p.Dot(64, 20, 6);
                    break;
                case "C_Rechute": // escalier qui descend
                    p.Poly(false, 12, 106, 38, 106, 38, 84, 62, 84, 62, 62, 86, 62, 86, 40, 112, 40);
                    p.L(20, 66, 46, 26); p.L(46, 26, 30, 30); p.L(46, 26, 48, 42);
                    break;
                case "C_NuitBlanche": // réveil
                    p.Arc(64, 56, 36); p.Arc(32, 94, 14, 45, 225); p.Arc(96, 94, 14, -45, 135);
                    p.L(40, 26, 30, 12); p.L(88, 26, 98, 12); p.L(64, 56, 64, 78); p.L(64, 56, 80, 56);
                    break;
                case "C_Honte": // visage qui baisse les yeux
                    p.Arc(64, 64, 44); p.Arc(48, 74, 9, 200, 340); p.Arc(80, 74, 9, 200, 340); p.Arc(64, 36, 14, 30, 150);
                    p.t = 5; p.Curve(true, 46, 62, 50, 53, 46, 49, 42, 53);
                    break;

                // ---------------- Transformations
                case "T_Questionner": // loupe
                    p.Arc(54, 74, 30); p.t = 11; p.L(76, 52, 108, 20);
                    p.t = 5; p.Arc(54, 80, 9, -60, 180); p.L(58.5f, 72.2f, 54, 68); p.Dot(54, 62, 3);
                    break;
                case "T_LeconRechute": // pousse qui sort d'une fissure
                    p.Poly(false, 12, 30, 52, 30, 60, 20, 68, 30, 116, 30); p.Bez(64, 30, 58, 58, 64, 86);
                    p.Bez(62, 66, 42, 90, 26, 76); p.Bez(26, 76, 44, 64, 62, 66);
                    p.Bez(64, 82, 84, 106, 102, 92); p.Bez(102, 92, 86, 80, 64, 82);
                    break;
                case "T_SommeilRetrouve": // croissant de lune
                    p.Arc(58, 64, 42, 50, 310); p.Bez(85, 96.2f, 46, 64, 85, 31.8f);
                    p.t = 5; p.Star4(102, 104, 10); p.Star4(110, 76, 7); p.Poly(false, 96, 54, 110, 54, 96, 40, 110, 40);
                    break;
                case "T_Pardon": // miroir et cœur
                    p.Ellipse(64, 78, 28, 36); p.t = 10; p.L(64, 42, 64, 12);
                    p.t = 6; p.Heart(64, 80, 1.1f);
                    break;

                // ---------------- La Dépression
                case "M_IdeesNoires": // nuage d'orage
                    p.Sdf(q => Mathf.Max(Mathf.Min(Mathf.Min(Circle(q, 40, 70, 16), Circle(q, 62, 82, 22)), Mathf.Min(Circle(q, 88, 72, 17), Circle(q, 70, 64, 16))), 54f - q.y));
                    p.L(40, 42, 32, 24); p.L(62, 42, 54, 24); p.L(84, 42, 76, 24);
                    break;
                case "M_Spirale":
                {
                    var pts = new Vector2[220];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        float a = i / (float)(pts.Length - 1) * Mathf.PI * 6f, r = 3f + 2.55f * a;
                        pts[i] = new Vector2(64 + Mathf.Cos(a) * r, 64 + Mathf.Sin(a) * r);
                    }
                    p.Pts(false, pts);
                    break;
                }
                case "M_PoidsDuMatin": // poids
                    p.Poly(true, 22, 18, 106, 18, 92, 80, 36, 80); p.Arc(64, 80, 22, 0, 180);
                    break;
                case "M_PenseesIntrusives": // hameçon
                    p.L(72, 106, 72, 46); p.Arc(56, 46, 16, 180, 360); p.L(40, 46, 40, 62); p.L(40, 62, 48, 54);
                    p.t = 6; p.Arc(72, 113, 6);
                    break;
                case "M_Insomnie": // ampoule qui pend
                    p.t = 6; p.L(64, 124, 64, 98); p.t = 8;
                    p.RRect(54, 82, 74, 98, 3); p.Arc(64, 58, 24, 120, 420);
                    p.t = 6; p.L(24, 58, 14, 58); p.L(104, 58, 114, 58); p.L(36, 28, 28, 20); p.L(92, 28, 100, 20);
                    break;
                case "M_Murmure": // oreille et chuchotement
                    p.Curve(false, 54, 92, 66, 110, 90, 104, 98, 82, 86, 62, 80, 42, 68, 26, 54, 30);
                    p.Curve(false, 66, 86, 76, 92, 84, 82, 76, 70, 74, 56);
                    p.t = 6; p.Arc(46, 64, 18, 140, 220); p.Arc(46, 64, 32, 145, 215);
                    break;
                case "M_LeVide": // anneaux brisés
                    for (int k = 0; k < 12; k++) p.Arc(64, 64, 46, k * 30, k * 30 + 16);
                    for (int k = 0; k < 8; k++) p.Arc(64, 64, 24, k * 45 + 20, k * 45 + 40);
                    p.Dot(64, 64, 4);
                    break;
                case "M_Culpabilite": // marteau de juge
                    p.Poly(true, 73.5f, 91.7f, 54.7f, 109.7f, 18.5f, 72.3f, 37.3f, 54.3f);
                    p.t = 10; p.L(55.4f, 73f, 104, 26); p.t = 8; p.RRect(10, 14, 58, 26, 4);
                    break;
                case "M_Rechute": // flèche qui fait demi-tour vers le bas
                    p.L(40, 30, 40, 80); p.Arc(64, 80, 24, 0, 180); p.L(88, 80, 88, 30); p.L(88, 24, 74, 40); p.L(88, 24, 102, 40);
                    break;
                case "M_Honte": // regards des autres
                    p.t = 6; p.Eye(64, 94, 24, 10); p.Eye(32, 46, 22, 9); p.Eye(96, 46, 22, 9);
                    break;
                case "M_Gouffre": // précipice
                    p.Poly(false, 10, 94, 46, 94, 54, 64, 46, 36, 54, 12); p.Poly(false, 118, 94, 82, 94, 76, 66, 84, 38, 74, 12);
                    p.t = 5; p.L(64, 88, 64, 76); p.L(64, 64, 64, 54); p.L(64, 42, 64, 34); p.L(56, 28, 64, 20); p.L(72, 28, 64, 20);
                    break;
                case "M_Cicatrices": // entailles recousues
                    p.t = 7; p.Scar(28, 100, 58, 26); p.Scar(50, 104, 80, 30); p.Scar(72, 108, 102, 34);
                    break;
                case "M_Desespoir": // fleur fanée
                    p.Bez(48, 14, 50, 92, 84, 94); p.Arc(88, 76, 10);
                    p.t = 6;
                    foreach (float a in new[] { 180f, 225f, 270f, 315f, 0f })
                        p.Arc(88 + Mathf.Cos(a * Mathf.Deg2Rad) * 21, 76 + Mathf.Sin(a * Mathf.Deg2Rad) * 21, 8);
                    p.Arc(96, 26, 6); p.Bez(49, 40, 26, 54, 24, 40); p.Bez(24, 40, 36, 32, 49, 40); p.L(22, 14, 78, 14);
                    break;
                case "M_CriseAngoisse": // cœur et tracé qui s'emballe
                    p.Heart(64, 72, 2.8f);
                    p.t = 6; p.Poly(false, 6, 58, 34, 58, 42, 72, 52, 30, 62, 96, 72, 46, 80, 58, 122, 58);
                    break;
                case "M_Effondrement": // colonne qui se brise
                    p.RRect(32, 98, 96, 110, 2); p.L(42, 98, 42, 64); p.L(86, 98, 86, 72);
                    p.Poly(false, 42, 64, 54, 72, 62, 62, 74, 74, 86, 72);
                    p.Poly(false, 38, 52, 50, 60, 58, 50, 70, 62, 82, 60); p.L(38, 52, 38, 26); p.L(82, 60, 82, 26);
                    p.RRect(28, 14, 94, 26, 2);
                    p.t = 5; p.Arc(104, 42, 4); p.Arc(112, 26, 5); p.Arc(18, 36, 4);
                    break;
                case "M_EmpriseTotale": // griffes
                    p.Bez(28, 108, 24, 58, 48, 24); p.Bez(56, 114, 52, 62, 70, 22); p.Bez(86, 110, 86, 64, 102, 30);
                    p.Bez(22, 104, 58, 126, 94, 106);
                    break;
                case "M_Isolement": // téléphone muet
                    p.RRect(38, 12, 90, 116, 10); p.t = 5; p.L(56, 104, 72, 104); p.Arc(64, 24, 4);
                    p.t = 7; p.L(52, 46, 76, 86); p.L(76, 46, 52, 86);
                    break;

                // ---------------- Classes et saignement
                case "S_Battements": // cœur et tracé d'électrocardiogramme
                    p.Heart(64, 70, 2.6f); p.t = 6; p.Poly(false, 8, 30, 36, 30, 46, 10, 58, 54, 68, 22, 78, 30, 120, 30);
                    break;
                case "S_SouffleCourt": // souffle haché
                    p.L(18, 84, 62, 84); p.L(74, 84, 92, 84); p.L(26, 62, 50, 62); p.L(62, 62, 104, 62); p.L(18, 40, 70, 40); p.Arc(98, 52, 12, -90, 120);
                    break;
                case "S_RegardNoir": // œil barré d'une ombre
                    p.Eye(64, 64, 46, 18); p.Dot(64, 64, 9); p.L(20, 104, 108, 24);
                    break;
                case "S_GorgeNouee": // nœud
                    p.Curve(false, 14, 54, 44, 52, 70, 80, 84, 60, 64, 40, 46, 64, 60, 86, 88, 78, 114, 74);
                    break;
                case "S_MainTendue": // main ouverte tendue
                    p.RRect(38, 16, 86, 60, 10); p.L(46, 60, 44, 100); p.L(58, 60, 58, 110); p.L(70, 60, 72, 108); p.L(82, 58, 88, 94); p.L(38, 36, 18, 58);
                    break;
                case "S_Apnee": // surface de l'eau et bulles
                    p.Curve(false, 10, 92, 30, 100, 50, 92, 70, 100, 90, 92, 118, 100); p.Arc(52, 64, 9); p.Arc(70, 42, 7); p.Arc(56, 22, 5);
                    break;
                case "S_Hemorragie": // grosse goutte et éclaboussures
                    p.Curve(true, 64, 118, 84, 80, 90, 54, 64, 30, 38, 54, 44, 80); p.Dot(22, 30, 6); p.Dot(104, 24, 5); p.Dot(98, 104, 4);
                    break;
                case "S_Garrot": // bras serré par un lien
                    p.L(30, 10, 46, 118); p.L(82, 10, 98, 118); p.t = 10; p.L(26, 62, 102, 70); p.t = 6; p.L(100, 70, 116, 50); p.L(100, 70, 118, 84);
                    break;
                case "S_SaignerABlanc": // lame et goutte
                    p.Poly(true, 24, 104, 92, 36, 100, 44, 32, 112); p.L(84, 28, 108, 52); p.L(96, 40, 112, 24); p.Curve(true, 40, 50, 48, 34, 40, 22, 32, 34);
                    break;
                case "S_Transfusion": // poche de sang et tubulure
                    p.RRect(30, 52, 98, 116, 12); p.L(30, 92, 98, 92); p.L(64, 52, 64, 30); p.Curve(false, 64, 30, 64, 16, 90, 12, 110, 24);
                    break;
                case "S_Palpitations": // cœur traversé d'un éclair
                    p.Heart(64, 66, 3f); p.t = 7; p.Poly(false, 70, 112, 52, 70, 72, 70, 56, 22);
                    break;
                case "S_Echo": // ondes dans les quatre directions
                    p.Dot(64, 64, 8); p.Arc(64, 64, 26, 20, 70); p.Arc(64, 64, 26, 110, 160); p.Arc(64, 64, 26, 200, 250); p.Arc(64, 64, 26, 290, 340);
                    p.Arc(64, 64, 46, 25, 65); p.Arc(64, 64, 46, 115, 155); p.Arc(64, 64, 46, 205, 245); p.Arc(64, 64, 46, 295, 335);
                    break;

                default:
                    p.Arc(64, 64, 40); p.Arc(64, 64, 16);
                    break;
            }
            return c;
        }

        // ================================================================== Géométrie

        static Vector2[] V(params float[] xy)
        {
            var v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return v;
        }

        static Vector2[] Star(float cx, float cy, float rOut, float rIn, int points)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < v.Length; i++)
            {
                float a = (90f + i * 180f / points) * Mathf.Deg2Rad, r = i % 2 == 0 ? rOut : rIn;
                v[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return v;
        }

        static float Circle(Vector2 p, float cx, float cy, float r) => Vector2.Distance(p, new Vector2(cx, cy)) - r;

        /// <summary>Distance signée à un polygone (négative à l'intérieur).</summary>
        static float SdPoly(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]), s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Distance (non signée) à une ligne brisée.</summary>
        static float SdPolyline(Vector2 p, Vector2[] v)
        {
            float d = float.MaxValue;
            for (int i = 0; i < v.Length - 1; i++)
            {
                Vector2 e = v[i + 1] - v[i], w = p - v[i];
                d = Mathf.Min(d, (w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e))).magnitude);
            }
            return d;
        }
    }
}
