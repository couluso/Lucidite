using System.Collections.Generic;
using UnityEngine;
using static Lucidite.EditorTools.Lib;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Statuettes des reliques, une par relique, dans le thème de la dépression : pleureuse, cloches de verre
    /// (« La Cloche de détresse »), visage recollé, silhouettes recroquevillées, vanités, rose fanée...
    /// Low-poly, matières de bibelots (plâtre, porcelaine, bronze, étain, cire, verre).
    /// Origine au pied de la statuette, face visible vers -Z (vers la joueuse). Prefabs dans Prefabs/Relics.
    /// Aussi : le pilulier posé sur la table (couvercle « Lid » articulé).
    /// </summary>
    public static class RelicFactory
    {
        const float Scale = 1.45f; // un peu plus grandes que nature pour rester lisibles sur la table
        static Material Bronze, Gold, Porcelain, Plaster, Tin, VotiveWax, Onyx, Lacquer, Bone, DarkWood, Sand,
            GlassShell, Flesh, Crepe, Wool, Moon, Stem, DryRose, Wax, Paper, PillPlastic, PillInside;

        static void Materials()
        {
            Bronze = AssetFactory.Lit("M_Relic_Bronze", Palette.Hex("5E5032"), null, 0.55f);
            Gold = AssetFactory.Lit("M_Relic_Gold", Palette.Hex("A8873E"), null, 0.65f);
            Porcelain = AssetFactory.Lit("M_Relic_Porcelain", Palette.Hex("D9D3C6"), null, 0.7f);
            Plaster = AssetFactory.Lit("M_Relic_Plaster", Palette.Hex("A9A397"), null, 0.1f);
            Tin = AssetFactory.Lit("M_Relic_Tin", Palette.Hex("8E8C86"), null, 0.6f);
            VotiveWax = AssetFactory.Lit("M_Relic_VotiveWax", Palette.Hex("8E2A26"), null, 0.35f);
            Onyx = AssetFactory.Lit("M_Relic_Onyx", Palette.Hex("1A1717"), null, 0.6f);
            Lacquer = AssetFactory.Lit("M_Relic_Lacquer", Palette.Hex("8A1A1A"), null, 0.6f);
            Bone = AssetFactory.Lit("M_Relic_Bone", Palette.Hex("C2B79C"), null, 0.25f);
            DarkWood = AssetFactory.Lit("M_Relic_DarkWood", Palette.Hex("3A2618"), null, 0.3f);
            Sand = AssetFactory.Lit("M_Relic_Sand", Palette.Hex("8A3A22"), null, 0.1f);
            Flesh = AssetFactory.Lit("M_Relic_Flesh", Palette.Hex("A8646A"), null, 0.45f);
            Crepe = AssetFactory.Lit("M_Relic_Crepe", Palette.Hex("231E26"), null, 0.2f);
            Wool = AssetFactory.Lit("M_Relic_Wool", Palette.Hex("4E5566"), null, 0.05f);
            Stem = AssetFactory.Lit("M_Relic_Stem", Palette.Hex("39432F"), null, 0.2f);
            DryRose = AssetFactory.Lit("M_Relic_DryRose", Palette.Hex("5C1A20"), null, 0.3f);
            Wax = AssetFactory.Lit("M_Relic_Wax", Palette.Hex("CFC5AE"), null, 0.35f);
            Paper = AssetFactory.Lit("M_Relic_Paper", Palette.Hex("B9AE94"), null, 0.05f);
            Moon = AssetFactory.LitEmissive("M_Relic_Moon", Palette.Hex("D9CFA0"), Palette.Hex("5A5230"));
            PillPlastic = AssetFactory.Lit("M_PillBox", Palette.Hex("D5D0C4"), null, 0.55f);
            PillInside = AssetFactory.Lit("M_PillBoxInside", Palette.Hex("2A2624"), null, 0.2f);
            // Verre : coque transparente (sans éclairage) + reflets opaques
            GlassShell = AssetFactory.Particle("M_Relic_Glass", null, new Color(0.78f, 0.86f, 0.9f, 0.16f), false);
        }

        // ------------------------------------------------------------------ Outils
        static MeshBuilder B() => new MeshBuilder();
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector3 R3(float r) => new Vector3(r, r, r);
        static Vector2 R(float r) => new Vector2(r, r);
        static List<Vector2> Prof(params float[] xy) { var l = new List<Vector2>(); for (int i = 0; i < xy.Length; i += 2) l.Add(new Vector2(xy[i], xy[i + 1])); return l; }
        static List<Vector3> Path(params float[] xyz) { var l = new List<Vector3>(); for (int i = 0; i < xyz.Length; i += 3) l.Add(new Vector3(xyz[i], xyz[i + 1], xyz[i + 2])); return l; }

        static Transform s_root;
        static int s_part;
        static GameObject P(string name, MeshBuilder b, Material m) => Part($"{name}_{s_part++}", s_root, b, m, false);

        /// <summary>Membre / tige : tube passant par les points, rayon interpolé de r0 à r1.</summary>
        static MeshBuilder Limb(MeshBuilder b, float r0, float r1, params float[] xyz)
        {
            var path = Path(xyz);
            var radii = new List<Vector2>();
            for (int i = 0; i < path.Count; i++) radii.Add(R(Mathf.Lerp(r0, r1, path.Count > 1 ? i / (path.Count - 1f) : 0f)));
            var dir = path.Count > 1 ? (path[1] - path[0]).normalized : Vector3.up;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.forward)) > 0.8f ? Vector3.up : Vector3.forward;
            return b.Tube(path, radii, 6, up, true, true);
        }

        static MeshBuilder Ball(MeshBuilder b, float x, float y, float z, float rx, float ry, float rz, int lat = 5, int lon = 8) =>
            b.Ellipsoid(V(x, y, z), V(rx, ry, rz), lat, lon);

        /// <summary>Socle rond mouluré.</summary>
        static void RoundPlinth(float r, float h, Material m) =>
            P("Socle", B().Lathe(Prof(0f, 0f, r, 0f, r, h * 0.55f, r * 0.9f, h * 0.7f, r * 0.88f, h, 0f, h), 16, capBottom: true), m);

        /// <summary>Socle carré avec plaque de laiton à l'avant.</summary>
        static void SquarePlinth(float w, float d, float h, Material m)
        {
            P("Socle", B().Box(V(0, h * 0.5f, 0), V(w, h, d), 20f).Box(V(0, h + 0.0012f, 0), V(w * 0.9f, 0.0024f, d * 0.9f), 20f), m);
            P("Plaque", B().Box(V(0, h * 0.5f, -d * 0.5f - 0.0004f), V(w * 0.5f, h * 0.45f, 0.0008f)), Gold);
        }

        /// <summary>Ligne posée à la surface d'un ellipsoïde (fêlures dorées, coutures) : points en (latitude, longitude) en degrés.</summary>
        static MeshBuilder SurfaceLine(MeshBuilder b, Vector3 c, Vector3 radii, float r, params float[] latLon)
        {
            var pts = new List<float>();
            for (int i = 0; i < latLon.Length; i += 2)
            {
                float la = latLon[i] * Mathf.Deg2Rad, lo = latLon[i + 1] * Mathf.Deg2Rad;
                var p = c + Vector3.Scale(radii * 1.04f, new Vector3(Mathf.Cos(la) * Mathf.Sin(lo), Mathf.Sin(la), -Mathf.Cos(la) * Mathf.Cos(lo)));
                pts.Add(p.x); pts.Add(p.y); pts.Add(p.z);
            }
            return Limb(b, r, r * 0.6f, pts.ToArray());
        }

        // ------------------------------------------------------------------ Construction

        /// <summary>Construit (ou reconstruit) la statuette d'une relique et renvoie son prefab.</summary>
        public static GameObject Build(string id)
        {
            if (Bronze == null) Materials();
            AssetFactory.EnsureFolder(AssetFactory.PrefabDir + "/Relics");
            MeshStore.Begin("Relic_" + id);
            var root = new GameObject(id);
            s_root = root.transform;
            s_part = 0;
            switch (id)
            {
                case "R_Pleureuse": Pleureuse(); break;
                case "R_ClocheCoeur": Cloche(CardSuit.Coeur); break;
                case "R_ClochePoumons": Cloche(CardSuit.Souffle); break;
                case "R_ClocheOeil": Cloche(CardSuit.Regard); break;
                case "R_ClocheMain": Cloche(CardSuit.Main); break;
                case "R_ExVotoCoeur": ExVoto(CardSuit.Coeur); break;
                case "R_ExVotoPoumons": ExVoto(CardSuit.Souffle); break;
                case "R_ExVotoOeil": ExVoto(CardSuit.Regard); break;
                case "R_ExVotoMain": ExVoto(CardSuit.Main); break;
                case "R_VisageRecolle": VisageRecolle(); break;
                case "R_Couverture": Couverture(); break;
                case "R_Atlas": Atlas(); break;
                case "R_Lacrymatoire": Lacrymatoire(); break;
                case "R_Penseur": Penseur(); break;
                case "R_Danseuse": Danseuse(); break;
                case "R_Sablier": Sablier(); break;
                case "R_Corbeau": Corbeau(); break;
                case "R_CoeurPerce": CoeurPerce(); break;
                case "R_Vanite": Vanite(); break;
                case "R_Metronome": Metronome(); break;
                case "R_VoileNoir": VoileNoir(); break;
                case "R_Reliquaire": Reliquaire(); break;
                case "R_BrasCroises": BrasCroises(); break;
                case "R_Veilleuse": Veilleuse(); break;
                case "R_RoseFanee": RoseFanee(); break;
                default: RoundPlinth(0.02f, 0.01f, Plaster); break;
            }
            root.transform.localScale = Vector3.one * Scale;
            return SavePrefab(root, $"{AssetFactory.PrefabDir}/Relics/{id}.prefab");
        }

        /// <summary>Pilulier en plastique blanc, croix rouge sur le couvercle articulé (enfant « Lid », charnière à l'arrière).</summary>
        public static GameObject BuildPillBox()
        {
            if (Bronze == null) Materials();
            MeshStore.Begin("PillBox");
            var root = new GameObject("PillBox");
            s_root = root.transform;
            s_part = 0;
            const float w = 0.056f, d = 0.036f, h = 0.014f, t = 0.0018f;
            var body = B();
            body.Box(V(0, t * 0.5f, 0), V(w, t, d));
            body.Box(V(-w * 0.5f + t * 0.5f, h * 0.5f, 0), V(t, h, d));
            body.Box(V(w * 0.5f - t * 0.5f, h * 0.5f, 0), V(t, h, d));
            body.Box(V(0, h * 0.5f, -d * 0.5f + t * 0.5f), V(w, h, t));
            body.Box(V(0, h * 0.5f, d * 0.5f - t * 0.5f), V(w, h, t));
            P("Boite", body, PillPlastic);
            P("Fond", B().Box(V(0, t + 0.0003f, 0), V(w - t * 2f, 0.0006f, d - t * 2f)), PillInside);
            // Couvercle : pivot sur la charnière arrière, il s'étend vers l'avant (-Z)
            var lid = new GameObject("Lid");
            lid.transform.SetParent(root.transform, false);
            lid.transform.localPosition = V(0, h, d * 0.5f);
            var lb = B().Box(V(0, 0.0012f, -d * 0.5f), V(w + 0.001f, 0.0024f, d + 0.001f));
            lb.Box(V(0, 0.0004f, -d + 0.0012f), V(w * 0.3f, 0.002f, 0.0024f)); // languette
            Part("Couvercle", lid.transform, lb, PillPlastic, false);
            var cross = B();
            cross.Box(V(0, 0.0026f, -d * 0.5f), V(0.016f, 0.0006f, 0.005f));
            cross.Box(V(0, 0.0026f, -d * 0.5f), V(0.005f, 0.0006f, 0.016f));
            Part("Croix", lid.transform, cross, Lacquer, false);
            // Charnières
            P("Charnieres", B().Box(V(-w * 0.3f, h, d * 0.5f + 0.0008f), V(0.008f, 0.003f, 0.002f)).Box(V(w * 0.3f, h, d * 0.5f + 0.0008f), V(0.008f, 0.003f, 0.002f)), Tin);
            return SavePrefab(root, $"{AssetFactory.PrefabDir}/PillBox.prefab");
        }

        // ------------------------------------------------------------------ Modèles

        /// <summary>La Pleureuse : agenouillée, le visage dans les mains, des larmes de sang entre les doigts.</summary>
        static void Pleureuse()
        {
            RoundPlinth(0.026f, 0.008f, DarkWood);
            float y0 = 0.008f;
            var b = B();
            // Robe drapée sur les jambes repliées (assise sur les talons)
            b.Ellipsoid(V(0, y0 + 0.009f, 0.004f), V(0.017f, 0.011f, 0.019f), 6, 12, d => V(1f, 1f + Mathf.Sin(d.x * 12f) * 0.06f, 1f));
            // Buste penché en avant
            Limb(b, 0.0085f, 0.0075f, 0, y0 + 0.016f, 0.006f, 0, y0 + 0.03f, -0.002f, 0, y0 + 0.04f, -0.008f);
            Ball(b, 0, y0 + 0.033f, -0.003f, 0.0095f, 0.011f, 0.0075f);
            // Bras repliés vers le visage
            foreach (float s in new[] { -1f, 1f })
                Limb(b, 0.0028f, 0.0022f, s * 0.008f, y0 + 0.04f, -0.006f, s * 0.0095f, y0 + 0.029f, -0.017f, s * 0.004f, y0 + 0.043f, -0.021f);
            P("Corps", b, Plaster);
            // Tête baissée dans les mains, cheveux longs sur le dos
            var head = B();
            Ball(head, 0, y0 + 0.046f, -0.016f, 0.0068f, 0.0075f, 0.0068f);
            foreach (float s in new[] { -1f, 1f }) Ball(head, s * 0.0035f, y0 + 0.045f, -0.0215f, 0.0034f, 0.0045f, 0.0022f, 4, 6); // mains
            P("Tete", head, Porcelain);
            var hair = B();
            hair.Ellipsoid(V(0, y0 + 0.047f, -0.013f), V(0.0075f, 0.0078f, 0.0072f), 5, 10);
            Limb(hair, 0.0058f, 0.0035f, 0, y0 + 0.046f, -0.01f, 0, y0 + 0.038f, -0.002f, 0, y0 + 0.028f, 0.004f);
            P("Cheveux", hair, Onyx);
            // Larmes de sang et petite flaque
            var tears = B();
            Ball(tears, -0.002f, y0 + 0.037f, -0.0225f, 0.0012f, 0.0022f, 0.0011f, 3, 5);
            Ball(tears, 0.003f, y0 + 0.034f, -0.022f, 0.0011f, 0.0019f, 0.001f, 3, 5);
            Ball(tears, 0.001f, y0 + 0.0006f, -0.02f, 0.006f, 0.0006f, 0.0045f, 3, 8);
            P("Larmes", tears, Lacquer);
        }

        /// <summary>Cloche de verre (« La Cloche de détresse ») sur un socle de bois : un organe conservé dessous.</summary>
        static void Cloche(CardSuit s)
        {
            P("Socle", B().Lathe(Prof(0f, 0f, 0.025f, 0f, 0.025f, 0.004f, 0.023f, 0.006f, 0.021f, 0.009f, 0f, 0.009f), 18, capBottom: true), DarkWood);
            float y0 = 0.009f;
            P("Coussin", B().Lathe(Prof(0f, y0, 0.012f, y0, 0.013f, y0 + 0.002f, 0.011f, y0 + 0.004f, 0f, y0 + 0.004f), 12), Lacquer);
            float c = y0 + 0.022f;
            switch (s)
            {
                case CardSuit.Coeur:
                {
                    var h = B();
                    h.At(V(0, c, 0), Quaternion.Euler(0, 0, -18), Vector3.one).Ellipsoid(Vector3.zero, V(0.0105f, 0.013f, 0.0095f), 6, 10,
                        d => V(1f, d.y < 0f ? 1.25f : 1f, 1f));
                    h.Reset();
                    Limb(h, 0.0034f, 0.0028f, 0.002f, c + 0.009f, 0, 0.003f, c + 0.019f, 0, 0.009f, c + 0.019f, 0, 0.01f, c + 0.012f, 0); // crosse aortique
                    Limb(h, 0.0026f, 0.0018f, -0.004f, c + 0.009f, 0.001f, -0.006f, c + 0.017f, 0.002f);
                    Limb(h, 0.0022f, 0.0015f, -0.008f, c + 0.004f, 0.003f, -0.013f, c + 0.009f, 0.004f);
                    P("Coeur", h, VotiveWax);
                    break;
                }
                case CardSuit.Souffle:
                {
                    var l = B();
                    foreach (float sx in new[] { -1f, 1f })
                        l.Ellipsoid(V(sx * 0.0075f, c - 0.002f, 0), V(0.0068f, 0.0125f, 0.0062f), 6, 10, d => V(d.y < 0f ? 1.15f : 0.95f, 1f, 1f));
                    P("Poumons", l, Flesh);
                    var t = B();
                    Limb(t, 0.0018f, 0.0016f, 0, c + 0.017f, 0, 0, c + 0.006f, 0);
                    Limb(t, 0.0014f, 0.001f, 0, c + 0.006f, 0, -0.005f, c + 0.001f, 0);
                    Limb(t, 0.0014f, 0.001f, 0, c + 0.006f, 0, 0.005f, c + 0.001f, 0);
                    P("Trachee", t, Bone);
                    break;
                }
                case CardSuit.Regard:
                {
                    var e = B();
                    Ball(e, 0, c, 0, 0.0105f, 0.0105f, 0.0105f, 8, 12);
                    P("Oeil", e, Porcelain);
                    P("Iris", B().Ellipsoid(V(0, c + 0.001f, -0.0098f), V(0.0048f, 0.0048f, 0.0012f), 4, 10), AssetFactory.Lit("M_Relic_Iris", Palette.Hex("3E5560"), null, 0.7f));
                    P("Pupille", B().Ellipsoid(V(0, c + 0.001f, -0.0108f), V(0.0022f, 0.0022f, 0.0006f), 3, 8), Onyx);
                    P("Nerf", Limb(B(), 0.0024f, 0.0012f, 0, c - 0.004f, 0.008f, 0, c - 0.012f, 0.009f, 0, y0 + 0.004f, 0.004f), Flesh);
                    var veins = B();
                    SurfaceLine(veins, V(0, c, 0), R3(0.0105f), 0.00035f, 30, 40, 18, 25, 10, 32);
                    SurfaceLine(veins, V(0, c, 0), R3(0.0105f), 0.00035f, -25, -40, -15, -28, -8, -34);
                    P("Veines", veins, Lacquer);
                    break;
                }
                default:
                {
                    // Main de plâtre dressée, doigts légèrement repliés
                    var m = B();
                    m.Box(V(0, c - 0.004f, 0), V(0.0125f, 0.014f, 0.0045f));
                    Limb(m, 0.0034f, 0.003f, 0, y0 + 0.004f, 0, 0, c - 0.01f, 0);
                    float[] xs = { -0.0047f, -0.0016f, 0.0016f, 0.0047f }, ls = { 0.009f, 0.0115f, 0.012f, 0.0102f };
                    for (int i = 0; i < 4; i++)
                        Limb(m, 0.0016f, 0.0013f, xs[i], c + 0.003f, 0, xs[i] * 1.05f, c + 0.003f + ls[i] * 0.6f, -0.0008f, xs[i] * 1.08f, c + 0.003f + ls[i], -0.0026f);
                    Limb(m, 0.0019f, 0.0014f, -0.006f, c - 0.008f, 0, -0.0105f, c - 0.002f, -0.002f, -0.0115f, c + 0.004f, -0.0035f);
                    P("Main", m, Plaster);
                    break;
                }
            }
            // Coque de verre, filet de laiton et bouton
            float top = y0 + 0.052f;
            P("Verre", B().Lathe(Prof(0.0185f, y0, 0.0188f, y0 + 0.03f, 0.0175f, y0 + 0.04f, 0.0135f, top - 0.004f, 0.007f, top, 0f, top + 0.0008f), 18), GlassShell);
            var rim = B();
            Limb(rim, 0.0006f, 0.0006f, -0.012f, y0 + 0.006f, -0.0145f, -0.0145f, y0 + 0.026f, -0.012f, -0.0135f, y0 + 0.04f, -0.01f); // reflet
            rim.Lathe(Prof(0.0192f, y0, 0.0194f, y0 + 0.0018f, 0.0186f, y0 + 0.0018f), 18);
            P("Reflets", rim, Gold);
            P("Bouton", B().Ellipsoid(V(0, top + 0.002f, 0), V(0.0026f, 0.0028f, 0.0026f), 4, 8), Gold);
        }

        /// <summary>Ex-voto : plaque d'étain clouée, l'organe en relief, entouré de rayons.</summary>
        static void ExVoto(CardSuit s)
        {
            P("Pied", B().Box(V(0, 0.004f, 0.004f), V(0.03f, 0.008f, 0.024f), 20f), DarkWood);
            var tilt = Quaternion.Euler(-8f, 0f, 0f);
            var plaque = B().At(V(0, 0.008f, 0.004f), tilt, Vector3.one);
            plaque.Box(V(0, 0.032f, 0), V(0.05f, 0.064f, 0.003f));
            plaque.Ellipsoid(V(0, 0.064f, 0), V(0.025f, 0.012f, 0.0016f), 4, 12);
            P("Plaque", plaque, Tin);
            // Rayons gravés et clou
            var rays = B().At(V(0, 0.008f, 0.004f), tilt, Vector3.one);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                rays.Box(V(Mathf.Cos(a) * 0.019f, 0.036f + Mathf.Sin(a) * 0.022f, -0.0018f), V(0.0015f, 0.0015f, 0.0008f));
            }
            rays.Ellipsoid(V(0, 0.068f, -0.002f), V(0.0022f, 0.0022f, 0.0012f), 3, 6);
            P("Rayons", rays, Onyx);
            var r = B().At(V(0, 0.008f, 0.004f), tilt, Vector3.one);
            var m = Gold;
            switch (s)
            {
                case CardSuit.Coeur:
                    r.Ellipsoid(V(-0.0075f, 0.039f, -0.003f), V(0.009f, 0.009f, 0.003f), 4, 8);
                    r.Ellipsoid(V(0.0075f, 0.039f, -0.003f), V(0.009f, 0.009f, 0.003f), 4, 8);
                    r.Ellipsoid(V(0, 0.03f, -0.003f), V(0.0105f, 0.013f, 0.003f), 4, 8);
                    r.Ellipsoid(V(0, 0.054f, -0.002f), V(0.003f, 0.007f, 0.002f), 3, 6);
                    m = VotiveWax;
                    break;
                case CardSuit.Souffle:
                    r.Ellipsoid(V(-0.0095f, 0.032f, -0.003f), V(0.0085f, 0.016f, 0.003f), 4, 8);
                    r.Ellipsoid(V(0.0095f, 0.032f, -0.003f), V(0.0085f, 0.016f, 0.003f), 4, 8);
                    r.Box(V(0, 0.052f, -0.003f), V(0.004f, 0.014f, 0.004f));
                    break;
                case CardSuit.Regard:
                    r.Ellipsoid(V(0, 0.036f, -0.0025f), V(0.018f, 0.0085f, 0.002f), 4, 10);
                    P("Iris", B().At(V(0, 0.008f, 0.004f), tilt, Vector3.one).Ellipsoid(V(0, 0.036f, -0.0048f), V(0.0055f, 0.0055f, 0.0015f), 4, 8), Onyx);
                    break;
                default:
                    r.Box(V(0, 0.028f, -0.003f), V(0.017f, 0.017f, 0.003f));
                    float[] xs = { -0.0063f, -0.0021f, 0.0021f, 0.0063f }, ls = { 0.012f, 0.016f, 0.017f, 0.014f };
                    for (int i = 0; i < 4; i++) r.Box(V(xs[i], 0.0365f + ls[i] * 0.5f, -0.003f), V(0.0034f, ls[i], 0.003f));
                    r.At(V(0, 0.008f, 0.004f), tilt * Quaternion.Euler(0, 0, 40f), Vector3.one).Box(V(-0.012f, 0.016f, -0.003f), V(0.0036f, 0.012f, 0.003f));
                    break;
            }
            P("Relief", r, m);
        }

        /// <summary>Le visage recollé : buste de porcelaine brisé puis réparé à l'or (kintsugi), les yeux clos.</summary>
        static void VisageRecolle()
        {
            SquarePlinth(0.036f, 0.032f, 0.012f, DarkWood);
            float y0 = 0.012f;
            var b = B();
            b.Lathe(Prof(0.016f, y0, 0.0165f, y0 + 0.004f, 0.014f, y0 + 0.012f, 0.0065f, y0 + 0.02f, 0.0055f, y0 + 0.026f, 0f, y0 + 0.027f), 14, capBottom: true);
            var hc = V(0, y0 + 0.037f, 0);
            var hr = V(0.0105f, 0.0135f, 0.0115f);
            b.Ellipsoid(hc, hr, 7, 12, d => V(1f, 1f, d.y < -0.5f && d.z < 0f ? 1.12f : 1f));
            Ball(b, 0, y0 + 0.036f, -0.0118f, 0.0018f, 0.0035f, 0.0022f, 3, 6); // nez
            Ball(b, 0, y0 + 0.0285f, -0.0098f, 0.0035f, 0.0012f, 0.0016f, 3, 6); // lèvres
            P("Buste", b, Porcelain);
            var eyes = B();
            foreach (float s in new[] { -1f, 1f }) Limb(eyes, 0.0005f, 0.0005f, s * 0.0062f, y0 + 0.0395f, -0.0099f, s * 0.004f, y0 + 0.0388f, -0.0108f, s * 0.0018f, y0 + 0.0392f, -0.011f);
            P("Paupieres", eyes, Onyx);
            // Fêlures dorées
            var k = B();
            SurfaceLine(k, hc, hr, 0.0006f, 80, 10, 50, -10, 30, 15, 5, -5, -20, 20, -40, 5);
            SurfaceLine(k, hc, hr, 0.0005f, 35, 70, 20, 40, 0, 50, -10, 80);
            SurfaceLine(k, hc, hr, 0.0005f, 10, -60, -5, -35, -25, -50);
            Limb(k, 0.0005f, 0.0004f, 0.004f, y0 + 0.006f, -0.0155f, 0.007f, y0 + 0.003f, -0.0145f, 0.009f, y0 + 0.008f, -0.013f);
            P("Kintsugi", k, Gold);
        }

        /// <summary>Sous la couverture : une silhouette recroquevillée, enroulée dans une couverture ; seul le visage dépasse.</summary>
        static void Couverture()
        {
            RoundPlinth(0.025f, 0.007f, Plaster);
            float y0 = 0.007f;
            var b = B();
            b.Ellipsoid(V(0, y0 + 0.02f, 0.002f), V(0.0185f, 0.022f, 0.017f), 8, 14,
                d => V(1f + Mathf.Sin(d.y * 9f + d.x * 5f) * 0.05f, 1f, 1f + Mathf.Sin(d.x * 11f) * 0.04f));
            b.Ellipsoid(V(0, y0 + 0.044f, -0.001f), V(0.0105f, 0.011f, 0.0105f), 6, 10);
            Limb(b, 0.0045f, 0.0012f, -0.012f, y0 + 0.004f, -0.008f, -0.02f, y0 + 0.001f, -0.012f); // pan qui traîne
            P("Couverture", b, Wool);
            var face = B();
            Ball(face, 0, y0 + 0.043f, -0.008f, 0.0058f, 0.0066f, 0.0045f);
            foreach (float s in new[] { -1f, 1f }) Ball(face, s * 0.0045f, y0 + 0.031f, -0.016f, 0.0028f, 0.0023f, 0.0024f, 3, 6); // mains qui serrent
            foreach (float s in new[] { -1f, 1f }) Ball(face, s * 0.005f, y0 + 0.0022f, -0.0165f, 0.0028f, 0.0022f, 0.0042f, 3, 6); // pieds nus
            P("Visage", face, Porcelain);
            var eyes = B();
            foreach (float s in new[] { -1f, 1f }) Ball(eyes, s * 0.0022f, y0 + 0.0445f, -0.0122f, 0.0012f, 0.0007f, 0.0006f, 3, 5);
            P("Yeux", eyes, Onyx);
            var hem = new List<Vector3>(); var rr = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float a = i / 16f * Mathf.PI * 2f; hem.Add(V(Mathf.Cos(a) * 0.0068f, y0 + 0.043f + Mathf.Sin(a) * 0.0076f, -0.0095f)); rr.Add(R(0.0016f)); }
            P("Capuche", B().Tube(hem, rr, 4, Vector3.forward, false, false), Wool);
        }

        /// <summary>Atlas : un genou à terre, il porte sur ses épaules une sphère noire, trop lourde.</summary>
        static void Atlas()
        {
            SquarePlinth(0.04f, 0.034f, 0.009f, DarkWood);
            float y0 = 0.009f;
            var b = B();
            // Jambes : genou gauche à terre, pied droit en avant
            Limb(b, 0.0036f, 0.003f, -0.005f, y0 + 0.02f, 0.004f, -0.006f, y0 + 0.003f, -0.005f, -0.006f, y0 + 0.002f, 0.012f);
            Limb(b, 0.0038f, 0.003f, 0.005f, y0 + 0.02f, 0.004f, 0.007f, y0 + 0.016f, -0.012f, 0.007f, y0 + 0.002f, -0.013f);
            // Buste courbé
            Limb(b, 0.0068f, 0.0078f, 0, y0 + 0.021f, 0.004f, 0, y0 + 0.031f, 0.001f, 0, y0 + 0.04f, -0.003f);
            Ball(b, 0, y0 + 0.036f, -0.001f, 0.009f, 0.008f, 0.0068f);
            Ball(b, 0, y0 + 0.041f, -0.0105f, 0.0042f, 0.0047f, 0.0044f); // tête baissée sous le poids
            // Bras levés qui retiennent la sphère
            foreach (float s in new[] { -1f, 1f })
                Limb(b, 0.0027f, 0.0021f, s * 0.008f, y0 + 0.041f, -0.002f, s * 0.0145f, y0 + 0.049f, -0.002f, s * 0.0125f, y0 + 0.059f, 0.001f);
            P("Atlas", b, Bronze);
            P("Poids", B().Ellipsoid(V(0, y0 + 0.064f, 0.002f), R3(0.0175f), 8, 14,
                d => Vector3.one * (1f + Mathf.Sin(d.x * 13f) * Mathf.Sin(d.y * 11f) * 0.025f)), Onyx);
        }

        /// <summary>Lacrymatoire : fiole à larmes au long col, presque pleine (de sang), sur un trépied de bronze.</summary>
        static void Lacrymatoire()
        {
            RoundPlinth(0.019f, 0.006f, DarkWood);
            float y0 = 0.006f;
            var t = B();
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Limb(t, 0.0011f, 0.0011f, Mathf.Cos(a) * 0.012f, y0, Mathf.Sin(a) * 0.012f, Mathf.Cos(a) * 0.0105f, y0 + 0.016f, Mathf.Sin(a) * 0.0105f);
            }
            t.Lathe(Prof(0.0115f, y0 + 0.015f, 0.0118f, y0 + 0.017f, 0.0098f, y0 + 0.017f), 12);
            P("Trepied", t, Bronze);
            float b0 = y0 + 0.012f;
            var prof = Prof(0f, b0, 0.0055f, b0 + 0.002f, 0.0098f, b0 + 0.012f, 0.0088f, b0 + 0.022f, 0.0035f, b0 + 0.03f, 0.0024f, b0 + 0.046f, 0.0036f, b0 + 0.052f, 0.0042f, b0 + 0.054f);
            P("Fiole", B().Lathe(prof, 14), GlassShell);
            P("Larmes", B().Lathe(Prof(0f, b0 + 0.001f, 0.0052f, b0 + 0.003f, 0.0091f, b0 + 0.012f, 0.0083f, b0 + 0.02f, 0f, b0 + 0.02f), 12), Lacquer);
            P("Bouchon", B().Lathe(Prof(0.0026f, b0 + 0.05f, 0.0036f, b0 + 0.057f, 0f, b0 + 0.059f), 8), Bone);
            P("Reflet", Limb(B(), 0.0005f, 0.0004f, -0.0072f, b0 + 0.006f, -0.006f, -0.0078f, b0 + 0.016f, -0.0045f), Porcelain);
        }

        /// <summary>Le penseur voûté : assis sur un rocher, la tête prise entre les deux mains.</summary>
        static void Penseur()
        {
            P("Rocher", B().Ellipsoid(V(0, 0.011f, 0.006f), V(0.019f, 0.012f, 0.017f), 5, 9,
                d => V(1f + Mathf.Sin(d.z * 7f) * 0.08f, 1f + Mathf.Sin(d.x * 6f) * 0.08f, 1f)), Plaster);
            var b = B();
            float y0 = 0.02f;
            // Cuisses vers l'avant, jambes qui descendent
            foreach (float s in new[] { -1f, 1f })
                Limb(b, 0.0037f, 0.003f, s * 0.0055f, y0 + 0.002f, 0.006f, s * 0.006f, y0 + 0.004f, -0.008f, s * 0.0058f, y0 - 0.016f, -0.012f);
            // Dos rond, très penché
            Limb(b, 0.0062f, 0.0074f, 0, y0 + 0.002f, 0.008f, 0, y0 + 0.016f, 0.006f, 0, y0 + 0.025f, -0.002f);
            Ball(b, 0, y0 + 0.02f, 0.002f, 0.0085f, 0.0085f, 0.0065f);
            // Coudes sur les genoux, mains sur les tempes
            foreach (float s in new[] { -1f, 1f })
                Limb(b, 0.0026f, 0.002f, s * 0.0078f, y0 + 0.024f, -0.001f, s * 0.0068f, y0 + 0.007f, -0.008f, s * 0.0052f, y0 + 0.021f, -0.0125f);
            Ball(b, 0, y0 + 0.022f, -0.0125f, 0.0044f, 0.0049f, 0.0046f); // tête dans les mains
            P("Penseur", b, Bronze);
        }

        /// <summary>Danseuse de boîte à musique, un bras cassé, qui tourne encore.</summary>
        static void Danseuse()
        {
            var box = B();
            box.Box(V(0, 0.009f, 0), V(0.042f, 0.018f, 0.03f), 30f);
            box.Box(V(0, 0.0185f, 0), V(0.044f, 0.0015f, 0.032f), 30f);
            P("Boite", box, DarkWood);
            P("Miroir", B().Box(V(0, 0.0195f, 0.004f), V(0.03f, 0.0006f, 0.016f)), Tin);
            P("Manivelle", Limb(B(), 0.0009f, 0.0009f, 0.021f, 0.009f, 0, 0.026f, 0.009f, 0, 0.026f, 0.014f, 0, 0.029f, 0.014f, 0), Gold);
            float y0 = 0.02f;
            P("Pivot", B().Lathe(Prof(0.004f, y0, 0.004f, y0 + 0.002f, 0f, y0 + 0.002f), 10), Gold);
            var b = B();
            Limb(b, 0.0019f, 0.0012f, 0, y0 + 0.002f, 0, 0, y0 + 0.024f, 0);                                    // jambe d'appui (pointe)
            Limb(b, 0.002f, 0.0013f, 0, y0 + 0.024f, 0.001f, 0, y0 + 0.026f, 0.011f, 0, y0 + 0.031f, 0.02f);        // jambe en arabesque
            b.Ellipsoid(V(0, y0 + 0.032f, 0), V(0.0042f, 0.0075f, 0.0034f), 4, 8);
            b.Ellipsoid(V(0, y0 + 0.044f, -0.0005f), V(0.0037f, 0.0042f, 0.0037f), 4, 8);
            Limb(b, 0.0014f, 0.0011f, 0.0035f, y0 + 0.037f, 0, 0.009f, y0 + 0.046f, 0, 0.004f, y0 + 0.053f, 0);    // bras levé
            Limb(b, 0.0015f, 0.0015f, -0.0035f, y0 + 0.037f, 0, -0.006f, y0 + 0.035f, 0);                          // moignon du bras cassé
            P("Danseuse", b, Porcelain);
            P("Tutu", B().Lathe(Prof(0f, y0 + 0.026f, 0.0155f, y0 + 0.0265f, 0.0135f, y0 + 0.029f, 0f, y0 + 0.03f), 16), Plaster);
            P("Felure", Limb(B(), 0.0004f, 0.0003f, -0.0035f, y0 + 0.038f, -0.0034f, -0.001f, y0 + 0.033f, -0.0038f, -0.002f, y0 + 0.028f, -0.003f), Onyx);
            P("Brisure", B().Ellipsoid(V(-0.016f, 0.0198f, -0.008f), V(0.002f, 0.0008f, 0.0012f), 3, 5), Porcelain); // le bras, tombé sur le couvercle
        }

        /// <summary>Sablier aux montants tournés : presque tout le sable est déjà tombé.</summary>
        static void Sablier()
        {
            var w = B();
            w.Lathe(Prof(0f, 0f, 0.019f, 0f, 0.019f, 0.005f, 0f, 0.005f), 4, capBottom: true);
            w.Lathe(Prof(0f, 0.075f, 0.019f, 0.075f, 0.019f, 0.08f, 0f, 0.08f), 4, capTop: true);
            foreach (var x in new[] { -0.0135f, 0.0135f })
                foreach (var z in new[] { -0.0135f, 0.0135f })
                    w.At(V(x, 0, z)).Lathe(Prof(0.0018f, 0.005f, 0.0026f, 0.012f, 0.0016f, 0.025f, 0.0026f, 0.04f, 0.0016f, 0.055f, 0.0026f, 0.068f, 0.0018f, 0.075f), 6);
            w.Reset();
            P("Bois", w, DarkWood);
            P("Verre", B().Lathe(Prof(0f, 0.005f, 0.011f, 0.008f, 0.012f, 0.02f, 0.0025f, 0.04f, 0.012f, 0.06f, 0.011f, 0.072f, 0f, 0.075f), 12), GlassShell);
            P("Sable", B().Lathe(Prof(0.0108f, 0.007f, 0.0098f, 0.016f, 0.004f, 0.021f, 0f, 0.023f), 12, capBottom: true), Sand);
            P("Filet", B().Lathe(Prof(0.0004f, 0.023f, 0.0004f, 0.04f), 4), Sand);
            P("SableHaut", B().Lathe(Prof(0.0035f, 0.041f, 0f, 0.0425f), 8, capBottom: true), Sand);
            P("Reflet", Limb(B(), 0.0005f, 0.0004f, -0.008f, 0.012f, -0.007f, -0.0085f, 0.02f, -0.006f), Porcelain);
        }

        /// <summary>Le corbeau, perché sur une pile de livres qu'on ne lit plus.</summary>
        static void Corbeau()
        {
            var books = B();
            books.Box(V(0, 0.004f, 0), V(0.036f, 0.008f, 0.026f));
            books.At(V(0.001f, 0.012f, 0.001f), V(0, 9, 0)).Box(Vector3.zero, V(0.032f, 0.008f, 0.024f));
            books.Reset();
            P("Couvertures", books, Lacquer);
            var pages = B();
            pages.Box(V(0, 0.004f, -0.0002f), V(0.034f, 0.0062f, 0.025f));
            pages.At(V(0.001f, 0.012f, 0.001f), V(0, 9, 0)).Box(V(0, 0, -0.0002f), V(0.03f, 0.0062f, 0.0232f));
            P("Pages", pages, Paper);
            float y0 = 0.016f;
            var b = B();
            b.At(V(0, y0 + 0.016f, 0.002f), V(-30, 0, 0)).Ellipsoid(Vector3.zero, V(0.0085f, 0.0145f, 0.008f), 6, 10);
            b.Reset();
            Ball(b, 0, y0 + 0.03f, -0.006f, 0.0058f, 0.006f, 0.0062f);
            b.At(V(0, y0 + 0.006f, 0.014f), V(55, 0, 0)).Box(Vector3.zero, V(0.008f, 0.0016f, 0.012f)); // queue
            b.Reset();
            foreach (float s in new[] { -1f, 1f })
            {
                b.At(V(s * 0.0072f, y0 + 0.017f, 0.004f), V(-25, 0, s * 8)).Ellipsoid(Vector3.zero, V(0.0022f, 0.0115f, 0.008f), 4, 8);
                b.Reset();
                Limb(b, 0.0009f, 0.0008f, s * 0.003f, y0 + 0.006f, 0.001f, s * 0.003f, y0, -0.002f);
            }
            P("Corbeau", b, Onyx);
            P("Bec", Limb(B(), 0.0019f, 0.0003f, 0, y0 + 0.03f, -0.011f, 0, y0 + 0.0285f, -0.019f), Tin);
            var eyes = B();
            foreach (float s in new[] { -1f, 1f }) Ball(eyes, s * 0.0045f, y0 + 0.032f, -0.0085f, 0.0011f, 0.0011f, 0.0008f, 3, 5);
            P("Yeux", eyes, Gold);
        }

        /// <summary>Cœur percé : cœur de cire transpercé d'une dague, couronné de flammes, qui goutte sur son socle.</summary>
        static void CoeurPerce()
        {
            RoundPlinth(0.018f, 0.007f, Bronze);
            P("Tige", B().Lathe(Prof(0.0035f, 0.007f, 0.0022f, 0.012f, 0.0022f, 0.02f, 0.004f, 0.022f), 8), Bronze);
            float c = 0.036f;
            var h = B();
            foreach (float s in new[] { -1f, 1f }) Ball(h, s * 0.0068f, c + 0.004f, 0, 0.0088f, 0.0088f, 0.0072f, 6, 10);
            h.Ellipsoid(V(0, c - 0.004f, 0), V(0.0122f, 0.0125f, 0.0072f), 6, 10, d => d.y < 0f ? V(1f + d.y * 0.45f, 1.4f, 1f + d.y * 0.3f) : Vector3.one);
            P("Coeur", h, VotiveWax);
            var f = B();
            Ball(f, 0, c + 0.019f, 0, 0.0032f, 0.0075f, 0.0028f, 4, 6);
            Ball(f, -0.005f, c + 0.016f, 0, 0.0024f, 0.0052f, 0.0022f, 3, 6);
            Ball(f, 0.005f, c + 0.016f, 0, 0.0024f, 0.0052f, 0.0022f, 3, 6);
            f.Lathe(Prof(0.004f, c + 0.011f, 0.0045f, c + 0.013f, 0f, c + 0.013f), 8);
            P("Flammes", f, Gold);
            var dag = B().At(V(0, c + 0.002f, 0), Quaternion.Euler(0, 0, 38f), Vector3.one);
            dag.Box(V(0, -0.004f, 0), V(0.0026f, 0.034f, 0.0009f));
            P("Lame", dag, Tin);
            var hilt = B().At(V(0, c + 0.002f, 0), Quaternion.Euler(0, 0, 38f), Vector3.one);
            hilt.Box(V(0, 0.014f, 0), V(0.008f, 0.0016f, 0.0018f));
            hilt.Box(V(0, 0.019f, 0), V(0.0018f, 0.009f, 0.0018f));
            hilt.Ellipsoid(V(0, 0.0245f, 0), R3(0.0017f), 3, 6);
            P("Garde", hilt, Gold);
            var drip = B();
            Limb(drip, 0.0012f, 0.0008f, 0.002f, c - 0.012f, -0.006f, 0.0025f, c - 0.02f, -0.004f);
            Ball(drip, 0.0025f, c - 0.021f, -0.004f, 0.0013f, 0.002f, 0.0013f, 3, 5);
            Ball(drip, -0.004f, 0.0072f, -0.012f, 0.0042f, 0.0005f, 0.003f, 3, 6);
            P("Sang", drip, Lacquer);
        }

        /// <summary>Vanité : crâne posé sur un livre, une bougie fondue sur le sommet.</summary>
        static void Vanite()
        {
            P("Livre", B().Box(V(0, 0.004f, 0.002f), V(0.04f, 0.008f, 0.03f)), DarkWood);
            P("Pages", B().Box(V(0.0005f, 0.004f, 0.0016f), V(0.038f, 0.0064f, 0.0292f)), Paper);
            float y0 = 0.008f;
            var b = B();
            b.Ellipsoid(V(0, y0 + 0.017f, 0.003f), V(0.0145f, 0.0138f, 0.0172f), 7, 12);
            b.Ellipsoid(V(0, y0 + 0.009f, -0.0075f), V(0.0105f, 0.0085f, 0.0085f), 5, 9);
            b.Box(V(0, y0 + 0.0025f, -0.0085f), V(0.0125f, 0.005f, 0.012f));
            foreach (float s in new[] { -1f, 1f }) Ball(b, s * 0.0118f, y0 + 0.011f, -0.003f, 0.0028f, 0.0045f, 0.0045f, 3, 6); // pommettes
            P("Crane", b, Bone);
            var e = B();
            foreach (float s in new[] { -1f, 1f }) Ball(e, s * 0.0054f, y0 + 0.0125f, -0.0137f, 0.0037f, 0.0033f, 0.0019f, 4, 6);
            Ball(e, 0, y0 + 0.0065f, -0.0148f, 0.0015f, 0.0023f, 0.0012f, 3, 5);
            for (int i = 0; i < 5; i++) e.Box(V(-0.0044f + i * 0.0022f, y0 + 0.0024f, -0.0146f), V(0.0011f, 0.0024f, 0.0008f));
            P("Orbites", e, Onyx);
            float c0 = y0 + 0.029f;
            var cw = B();
            cw.Lathe(Prof(0.007f, c0 - 0.002f, 0.0062f, c0, 0.0048f, c0 + 0.002f, 0.0045f, c0 + 0.018f, 0.0038f, c0 + 0.02f, 0f, c0 + 0.0195f), 10);
            Limb(cw, 0.0012f, 0.0016f, 0.004f, c0 + 0.016f, -0.002f, 0.0058f, c0 + 0.008f, -0.003f, 0.0082f, c0 - 0.003f, -0.004f);
            Limb(cw, 0.001f, 0.0014f, -0.0035f, c0 + 0.012f, -0.003f, -0.006f, c0 + 0.002f, -0.005f, -0.0095f, c0 - 0.006f, -0.008f);
            P("Bougie", cw, Wax);
            P("Meche", Limb(B(), 0.0005f, 0.0004f, 0, c0 + 0.0195f, 0, 0.0008f, c0 + 0.0235f, 0), Onyx);
        }

        /// <summary>Métronome : il donne le tempo, même lent.</summary>
        static void Metronome()
        {
            var b = B();
            b.At(V(0, 0, 0), Quaternion.Euler(0, 45, 0), Vector3.one).Lathe(Prof(0f, 0f, 0.025f, 0f, 0.025f, 0.006f, 0.0085f, 0.066f, 0.006f, 0.068f, 0f, 0.068f), 4, capBottom: true);
            b.Reset();
            P("Corps", b, DarkWood);
            P("Cadran", B().At(V(0, 0.024f, -0.0102f), V(-11.5f, 0, 0)).Box(Vector3.zero, V(0.014f, 0.034f, 0.0008f)), Paper);
            var pend = B().At(V(0, 0.01f, -0.0118f), Quaternion.Euler(-11.5f, 0, 16f), Vector3.one);
            pend.Box(V(0, 0.027f, 0), V(0.0011f, 0.054f, 0.0008f));
            P("Balancier", pend, Tin);
            var wgt = B().At(V(0, 0.01f, -0.0118f), Quaternion.Euler(-11.5f, 0, 16f), Vector3.one);
            wgt.Box(V(0, 0.037f, -0.0006f), V(0.005f, 0.0055f, 0.0022f));
            P("Poids", wgt, Gold);
            P("Cle", Limb(B(), 0.0012f, 0.0012f, 0.0105f, 0.014f, 0, 0.0145f, 0.014f, 0), Gold);
        }

        /// <summary>Le voile noir : un buste dont la tête est entièrement recouverte d'un voile de crêpe.</summary>
        static void VoileNoir()
        {
            SquarePlinth(0.034f, 0.03f, 0.011f, Plaster);
            float y0 = 0.011f;
            var b = B();
            b.Ellipsoid(V(0, y0 + 0.009f, 0.001f), V(0.0165f, 0.011f, 0.0115f), 6, 14,
                d => V(1f, d.y < 0f ? 1f : 1f + Mathf.Sin(Mathf.Atan2(d.z, d.x) * 9f) * 0.05f, 1f));
            b.Ellipsoid(V(0, y0 + 0.031f, 0), V(0.0105f, 0.0135f, 0.0112f), 7, 14,
                d => { float f = d.y < -0.3f ? 1.12f + Mathf.Sin(Mathf.Atan2(d.z, d.x) * 11f) * 0.08f : 1f; return V(f, 1f, f); });
            Ball(b, 0, y0 + 0.0315f, -0.0112f, 0.0019f, 0.0034f, 0.0022f, 3, 6); // le nez sous le voile
            P("Voile", b, Crepe);
            var lace = new List<Vector3>(); var rr = new List<Vector2>();
            for (int i = 0; i <= 18; i++) { float a = i / 18f * Mathf.PI * 2f; lace.Add(V(Mathf.Cos(a) * 0.0168f, y0 + 0.004f + Mathf.Sin(a * 6f) * 0.0006f, Mathf.Sin(a) * 0.0118f + 0.001f)); rr.Add(R(0.0007f)); }
            P("Dentelle", B().Tube(lace, rr, 4, Vector3.up, false, false), Onyx);
        }

        /// <summary>Reliquaire : châsse dorée à fenêtre de verre ; dedans, un os sur un coussin rouge.</summary>
        static void Reliquaire()
        {
            float y0 = 0.004f;
            var b = B();
            foreach (float x in new[] { -0.016f, 0.016f })
                foreach (float z in new[] { -0.009f, 0.009f })
                    Ball(b, x, 0.002f, z, 0.0022f, 0.0022f, 0.0022f, 3, 6);
            b.Box(V(0, y0 + 0.0015f, 0), V(0.04f, 0.003f, 0.024f));
            b.Box(V(0, y0 + 0.022f, 0), V(0.04f, 0.0025f, 0.024f));
            foreach (float x in new[] { -0.019f, 0.019f })
                foreach (float z in new[] { -0.011f, 0.011f })
                    b.Box(V(x, y0 + 0.012f, z), V(0.0026f, 0.019f, 0.0026f));
            b.Box(V(0, y0 + 0.012f, 0.0112f), V(0.038f, 0.019f, 0.0012f));
            b.At(V(0, y0 + 0.0235f, 0), Quaternion.Euler(0, 45, 0), Vector3.one).Lathe(Prof(0.019f, 0f, 0.0015f, 0.011f, 0f, 0.011f), 4);
            b.Reset();
            b.Box(V(0, y0 + 0.039f, 0), V(0.0016f, 0.009f, 0.0016f));
            b.Box(V(0, y0 + 0.04f, 0), V(0.0065f, 0.0016f, 0.0016f));
            P("Chasse", b, Gold);
            P("Coussin", B().Box(V(0, y0 + 0.0052f, 0.001f), V(0.032f, 0.0045f, 0.018f)), Lacquer);
            var bone = B();
            Limb(bone, 0.0016f, 0.0013f, -0.009f, y0 + 0.009f, 0, 0.009f, y0 + 0.0095f, 0);
            foreach (float x in new[] { -0.0095f, 0.0095f }) Ball(bone, x, y0 + 0.0092f, 0, 0.0024f, 0.0021f, 0.0021f, 3, 6);
            P("Os", bone, Bone);
            P("Vitre", B().Box(V(0, y0 + 0.012f, -0.0112f), V(0.036f, 0.018f, 0.0006f)), GlassShell);
        }

        /// <summary>Les bras croisés : debout, elle s'étreint elle-même, la tête penchée.</summary>
        static void BrasCroises()
        {
            RoundPlinth(0.016f, 0.006f, DarkWood);
            float y0 = 0.006f;
            var b = B();
            b.Lathe(Prof(0.0112f, y0, 0.0098f, y0 + 0.012f, 0.0076f, y0 + 0.03f, 0.0068f, y0 + 0.042f, 0.0078f, y0 + 0.05f, 0.0062f, y0 + 0.056f, 0f, y0 + 0.058f), 12, capBottom: true);
            Limb(b, 0.0022f, 0.0021f, 0, y0 + 0.056f, 0, 0, y0 + 0.061f, -0.0008f);
            // Bras qui se croisent devant la poitrine et serrent les épaules
            Limb(b, 0.0021f, 0.0017f, -0.0072f, y0 + 0.053f, 0, -0.0085f, y0 + 0.044f, -0.005f, 0.002f, y0 + 0.046f, -0.0092f, 0.0068f, y0 + 0.052f, -0.004f);
            Limb(b, 0.0021f, 0.0017f, 0.0072f, y0 + 0.053f, 0, 0.0085f, y0 + 0.041f, -0.004f, -0.002f, y0 + 0.042f, -0.0094f, -0.0066f, y0 + 0.05f, -0.0045f);
            P("Corps", b, Plaster);
            var head = B();
            head.At(V(0.0012f, y0 + 0.066f, -0.0016f), V(14, 0, -16)).Ellipsoid(Vector3.zero, V(0.0047f, 0.0057f, 0.005f), 5, 9);
            head.Reset();
            P("Tete", head, Porcelain);
            var hair = B();
            hair.At(V(0.0012f, y0 + 0.0668f, -0.0004f), V(14, 0, -16)).Ellipsoid(Vector3.zero, V(0.0051f, 0.0056f, 0.0051f), 5, 9);
            hair.Reset();
            Ball(hair, 0.001f, y0 + 0.064f, 0.0045f, 0.0028f, 0.0028f, 0.0028f, 3, 6); // chignon
            P("Cheveux", hair, Onyx);
        }

        /// <summary>La veilleuse : un croissant de lune qui luit doucement, une étoile pendue à un fil.</summary>
        static void Veilleuse()
        {
            RoundPlinth(0.017f, 0.006f, DarkWood);
            P("Tige", Limb(B(), 0.0012f, 0.0012f, 0, 0.006f, 0.004f, 0, 0.03f, 0.004f), Tin);
            var moon = new List<Vector3>(); var rr = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f, a = Mathf.Lerp(-140f, 140f, t) * Mathf.Deg2Rad;
                moon.Add(V(Mathf.Sin(a) * 0.017f + 0.004f, 0.047f - Mathf.Cos(a) * 0.017f, 0.004f));
                rr.Add(R(Mathf.Lerp(0.0007f, 0.0062f, Mathf.Sin(t * Mathf.PI))));
            }
            P("Lune", B().Tube(moon, rr, 8, Vector3.forward, true, true), Moon);
            var face = B();
            Limb(face, 0.0005f, 0.0004f, -0.0085f, 0.046f, -0.0018f, -0.0108f, 0.0452f, -0.0014f); // œil fermé
            P("Paupiere", face, Bronze);
            P("Fil", Limb(B(), 0.0002f, 0.0002f, 0.012f, 0.058f, 0.004f, 0.012f, 0.044f, 0.004f), Tin);
            var star = B();
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Limb(star, 0.0011f, 0.0002f, 0.012f, 0.0405f, 0.004f, 0.012f + Mathf.Sin(a) * 0.0042f, 0.0405f + Mathf.Cos(a) * 0.0042f, 0.004f);
            }
            P("Etoile", star, Gold);
        }

        /// <summary>Rose fanée dans un soliflore : la tête tombe, les pétales sont sur le socle, les épines restent.</summary>
        static void RoseFanee()
        {
            RoundPlinth(0.02f, 0.005f, Plaster);
            float y0 = 0.005f;
            P("Vase", B().Lathe(Prof(0f, y0, 0.0055f, y0, 0.0075f, y0 + 0.008f, 0.0062f, y0 + 0.02f, 0.0028f, y0 + 0.034f, 0.0026f, y0 + 0.04f, 0.0036f, y0 + 0.043f, 0.0028f, y0 + 0.043f), 12, capBottom: true), Porcelain);
            var stem = B();
            Limb(stem, 0.0011f, 0.0009f, 0, y0 + 0.04f, 0, 0.001f, y0 + 0.054f, -0.001f, 0.006f, y0 + 0.062f, -0.004f, 0.012f, y0 + 0.058f, -0.007f, 0.014f, y0 + 0.05f, -0.008f);
            // Épines
            foreach (var (x, y, z, dx) in new[] { (0.0004f, y0 + 0.047f, -0.0005f, -1f), (0.003f, y0 + 0.059f, -0.0025f, 1f), (0.0105f, y0 + 0.0595f, -0.0062f, 1f) })
                Limb(stem, 0.0006f, 0.0001f, x, y, z, x + dx * 0.0022f, y + 0.0012f, z - 0.0008f);
            // Feuille sèche
            stem.At(V(0.0018f, y0 + 0.052f, -0.0015f), V(0, 30, -50)).Ellipsoid(Vector3.zero, V(0.0045f, 0.0008f, 0.0022f), 3, 6);
            stem.Reset();
            P("Tige", stem, Stem);
            var rose = B();
            Ball(rose, 0.0145f, y0 + 0.045f, -0.0085f, 0.0046f, 0.0052f, 0.0046f, 5, 8);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                rose.At(V(0.0145f + Mathf.Cos(a) * 0.0035f, y0 + 0.0465f, -0.0085f + Mathf.Sin(a) * 0.0035f), V(Mathf.Sin(a) * 35f, 0, -Mathf.Cos(a) * 35f))
                    .Ellipsoid(Vector3.zero, V(0.0036f, 0.0042f, 0.0011f), 3, 6);
            }
            rose.Reset();
            // Pétales tombés
            Ball(rose, 0.012f, y0 + 0.0006f, -0.012f, 0.0034f, 0.0005f, 0.0024f, 3, 6);
            Ball(rose, -0.009f, y0 + 0.0006f, -0.011f, 0.0029f, 0.0005f, 0.0021f, 3, 6);
            Ball(rose, 0.006f, y0 + 0.0006f, 0.012f, 0.0027f, 0.0005f, 0.002f, 3, 6);
            P("Rose", rose, DryRose);
        }
    }
}
