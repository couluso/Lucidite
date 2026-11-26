using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Lucidite.EditorTools.Lib;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// "psx maiden" (CC-BY 4.0, petya-petyavich) transformée en Dépression :
    /// texture grisée et abîmée (ecchymoses, coupures, sang, cernes, yeux éteints), squelette posé assis.
    /// Le fichier glTF est importé par le package glTFast.
    /// </summary>
    public static partial class ThirdParty
    {
        const string Maiden = Dir + "/Maiden";
        const string MaidenGltf = Maiden + "/maiden.gltf";
        static Material s_maiden;

        // ================================================================== Préparation

        static void CopyMaidenFromDocuments(string src)
        {
            string s = Path.Combine(src, "psx_maiden");
            if (!Directory.Exists(s) || File.Exists(MaidenGltf)) return;
            AssetFactory.EnsureFolder(Maiden);
            Directory.CreateDirectory($"{Maiden}/textures");
            File.Copy(Path.Combine(s, "scene.bin"), $"{Maiden}/scene.bin", true);
            File.Copy(Path.Combine(s, "license.txt"), $"{Maiden}/license.txt", true);
            File.Copy(Path.Combine(s, "textures", ".001_baseColor.png"), $"{Maiden}/textures/maiden_baseColor.png", true);
            var json = File.ReadAllText(Path.Combine(s, "scene.gltf")).Replace("textures/.001_baseColor.png", "textures/maiden_baseColor.png");
            File.WriteAllText(MaidenGltf, json);
        }

        /// <summary>Crée la texture "Dépression" à partir de la texture d'origine et le matériau PSX.</summary>
        static void PrepareMaiden()
        {
            string src = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AssetsUnity");
            CopyMaidenFromDocuments(src);
            string texPath = $"{Maiden}/textures/maiden_baseColor.png";
            if (!File.Exists(texPath)) return;
            ConfigureTexture(texPath, false);

            var original = new Texture2D(2, 2);
            original.LoadImage(File.ReadAllBytes(texPath));
            var canvas = new TextureFactory.Canvas(original.width, original.height, Color.black);
            var px = original.GetPixels();
            for (int i = 0; i < px.Length; i++) canvas.px[i] = px[i];
            UnityEngine.Object.DestroyImmediate(original);

            // Classes de pixels calculées sur l'original (avant grisage)
            var kinds = new Px[canvas.px.Length];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = Classify(canvas.px[i]);

            // Base : grisée et déjà un peu marquée. Les blessures fortes sont dans la carte de dégâts (progressive).
            Depressionize(canvas, Choices.maidenBruises * 0.35f, Choices.maidenBlood * 0.25f);
            var tex = TextureFactory.Save(canvas, $"{Maiden}/textures/maiden_depression.png", point: true, repeat: false);
            var dmg = BuildDamageOverlay(kinds, canvas.w, canvas.h, 91, 1f);
            var dmgTex = TextureFactory.Save(dmg, $"{Maiden}/textures/maiden_damage.png", point: true, repeat: false, alpha: true);
            s_maiden = AssetFactory.Lit("M_TP_MaidenDepression", Color.white, tex, 0.15f);
            s_maiden.SetTexture("_DamageMap", dmgTex);
            AssetFactory.DoubleSided(s_maiden);
        }

        /// <summary>
        /// Carte de blessures pour le shader PSX : RGB = couleur de la marque, A = 1 - seuil d'apparition.
        /// Les marques "fortes" (grosses ecchymoses) apparaissent tôt, les coupures et le sang au fil des dégâts.
        /// </summary>
        static TextureFactory.Canvas BuildDamageOverlay(Px[] kind, int w, int h, int seed, float scale)
        {
            var c = new TextureFactory.Canvas(w, h, new Color(0, 0, 0, 0));
            var rng = new System.Random(seed);
            var bruise = Palette.Hex("3E2440");
            var bruiseEdge = Palette.Hex("5A5236");
            void Put(int x, int y, Color col, float appearAt)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = y * w + x;
                float a = 1f - Mathf.Clamp(appearAt, 0.02f, 1f);
                if (a > c.px[i].a) c.px[i] = new Color(col.r, col.g, col.b, a);
            }
            bool Skin(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && kind[y * w + x] == Px.Skin;

            float fs = 0.035f / Mathf.Max(0.25f, scale);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var k = kind[y * w + x];
                    if (k == Px.Skin)
                    {
                        float n = Fbm(x * fs + 300f, y * fs + 40f);
                        if (n > 0.5f)
                        {
                            float appear = Mathf.Lerp(0.75f, 0.05f, Mathf.InverseLerp(0.5f, 0.74f, n));
                            Put(x, y, n < 0.54f ? bruiseEdge : Color.Lerp(bruise, Palette.Hex("2A1520"), Mathf.InverseLerp(0.54f, 0.8f, n)), appear);
                        }
                    }
                    else if (k != Px.Eye)
                    {
                        float n = Fbm(x * 0.03f + 500f, y * 0.03f + 77f);
                        if (n > 0.6f) Put(x, y, n > 0.66f ? Palette.BloodDark : Palette.Blood, Mathf.Lerp(1f, 0.3f, Mathf.InverseLerp(0.6f, 0.76f, n)));
                    }
                }

            // Coupures et griffures, puis coulures de sang (apparaissent plus tard)
            int cuts = Mathf.RoundToInt(170 * scale), placed = 0, tries = 0;
            while (placed < cuts && tries < cuts * 80)
            {
                tries++;
                int x0 = rng.Next(w), y0 = rng.Next(h);
                if (!Skin(x0, y0)) continue;
                placed++;
                float appear = 0.12f + (float)rng.NextDouble() * 0.85f;
                float ang = (float)(rng.NextDouble() * Mathf.PI);
                int len = Mathf.RoundToInt((6 + rng.Next(16)) * Mathf.Max(0.4f, scale));
                for (int s = 0; s < len; s++)
                {
                    int x = x0 + Mathf.RoundToInt(Mathf.Cos(ang) * s), y = y0 + Mathf.RoundToInt(Mathf.Sin(ang) * s);
                    if (!Skin(x, y)) continue;
                    Put(x, y, Palette.BloodLight, appear);
                    Put(x, y - 1, Palette.BloodDark, appear);
                }
                if (rng.NextDouble() < 0.6)
                {
                    int x = x0, drip = Mathf.RoundToInt((8 + rng.Next(40)) * Mathf.Max(0.4f, scale));
                    for (int s = 0; s < drip; s++)
                    {
                        int y = y0 - s;
                        if (rng.NextDouble() < 0.15) x += rng.Next(-1, 2);
                        Put(x, y, s < drip * 0.6f ? Palette.Blood : Palette.BloodDark, Mathf.Min(1f, appear + 0.08f + s * 0.004f));
                    }
                }
            }
            return c;
        }

        /// <summary>Carte de blessures pour une texture entièrement de peau (bras de la joueuse, Dépression générée).</summary>
        public static Texture2D SkinDamageOverlay(string path, int size, int seed)
        {
            var kinds = new Px[size * size];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = Px.Skin;
            var c = BuildDamageOverlay(kinds, size, size, seed, size / 1024f * 3f);
            return TextureFactory.Save(c, path, point: true, repeat: true, alpha: true);
        }

        // ================================================================== Texture

        enum Px : byte { Other, Skin, Hair, White, Dark, Eye }

        static Px Classify(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (h > 0.5f && h < 0.72f && s > 0.3f && v > 0.45f) return Px.Eye;
            if (v < 0.32f) return Px.Dark;
            if (h > 0.09f && h < 0.19f && s > 0.32f && v > 0.35f) return Px.Hair;
            // La peau est pâle mais chaude (rouge > bleu) ; le tissu blanc est neutre ou bleuté.
            if (c.r > c.b + 0.035f && v > 0.45f && s < 0.6f && (h < 0.12f || h > 0.92f)) return Px.Skin;
            if (s < 0.2f && v > 0.65f) return Px.White;
            return Px.Other;
        }

        static float Lum(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

        static float Fbm(float x, float y)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < 4; i++) { v += a * Mathf.PerlinNoise(x * f + 13.1f * i, y * f + 7.7f * i); a *= 0.5f; f *= 2.1f; }
            return v;
        }

        /// <summary>
        /// Grise et abîme la texture : peau cendrée, cheveux ternes, tissus délavés et sales, ecchymoses,
        /// griffures et coupures qui saignent (coulures vers le bas), taches de sang, cernes, yeux sans vie.
        /// </summary>
        static void Depressionize(TextureFactory.Canvas c, float bruises, float blood)
        {
            int w = c.w, h = c.h;
            var kind = new Px[w * h];
            for (int i = 0; i < kind.Length; i++) kind[i] = Classify(c.px[i]);

            var ash = Palette.Ash;
            var bruise = Palette.Hex("4B3550");
            var bruiseEdge = Palette.Hex("5E5A3A");
            var bloodDark = Palette.BloodDark;
            var bloodCol = Palette.Blood;

            // 1. Désaturation par matière
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var col = c.px[i];
                    float l = Lum(col);
                    float grime = Fbm(x * 0.015f, y * 0.015f);
                    switch (kind[i])
                    {
                        case Px.Skin:
                            col = new Color(ash.r, ash.g * 0.98f, ash.b * 1.03f) * Mathf.Lerp(0.7f, 1.12f, l);
                            col = Color.Lerp(col, Palette.Hex("6E6A72"), (grime - 0.5f) * 0.6f);
                            break;
                        case Px.Hair:
                            col = new Color(l * 0.62f, l * 0.6f, l * 0.58f);
                            break;
                        case Px.White:
                            col = Color.Lerp(new Color(l * 0.72f, l * 0.7f, l * 0.68f), Palette.Hex("5A5048"), Mathf.Clamp01(grime - 0.45f) * 1.2f);
                            break;
                        case Px.Eye:
                            col = Color.Lerp(new Color(l * 0.35f, l * 0.33f, l * 0.34f), bloodDark, 0.35f);
                            break;
                        default:
                            col = Color.Lerp(col, new Color(l, l, l), 0.75f) * 0.9f;
                            break;
                    }
                    col.a = c.px[i].a;
                    c.px[i] = col;
                }

            // 2. Cernes : assombrit la peau autour des yeux (surtout en dessous)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (kind[y * w + x] != Px.Eye) continue;
                    for (int dy = -7; dy <= 3; dy++)
                        for (int dx = -6; dx <= 6; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                            int j = yy * w + xx;
                            if (kind[j] != Px.Skin) continue;
                            float k = 0.08f * (1f - (dx * dx + dy * dy) / 85f);
                            if (k > 0f) c.px[j] = Color.Lerp(c.px[j], Palette.Hex("2A1C26"), k);
                        }
                }

            var rng = new System.Random(77);
            bool IsSkin(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && kind[y * w + x] == Px.Skin;

            // 3. Ecchymoses (taches violacées à bord jaunâtre, seulement sur la peau)
            if (bruises > 0f)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (kind[i] != Px.Skin) continue;
                        float n = Fbm(x * 0.035f + 50f, y * 0.035f + 20f);
                        float th = Mathf.Lerp(0.74f, 0.52f, bruises);
                        if (n > th - 0.05f && n <= th) c.px[i] = Color.Lerp(c.px[i], bruiseEdge, 0.35f);
                        else if (n > th) c.px[i] = Color.Lerp(c.px[i], bruise, Mathf.Clamp01((n - th) * 6f) * 0.75f);
                    }

            // 4. Griffures et coupures qui saignent (le sang coule vers le bas de la texture)
            int cuts = Mathf.RoundToInt(Mathf.Lerp(20, 140, Mathf.Max(bruises, blood)));
            int placed = 0, tries = 0;
            while (placed < cuts && tries < cuts * 60)
            {
                tries++;
                int x0 = rng.Next(w), y0 = rng.Next(h);
                if (!IsSkin(x0, y0)) continue;
                placed++;
                float ang = (float)(rng.NextDouble() * Mathf.PI);
                int len = 6 + rng.Next(14);
                for (int s = 0; s < len; s++)
                {
                    int x = x0 + Mathf.RoundToInt(Mathf.Cos(ang) * s), y = y0 + Mathf.RoundToInt(Mathf.Sin(ang) * s);
                    if (!IsSkin(x, y)) continue;
                    c.px[y * w + x] = Color.Lerp(c.px[y * w + x], Palette.BloodLight, 0.85f);
                    if (IsSkin(x, y - 1)) c.px[(y - 1) * w + x] = Color.Lerp(c.px[(y - 1) * w + x], bloodDark, 0.5f);
                }
                if (rng.NextDouble() < blood)
                {
                    // Coulure
                    int x = x0, drip = 8 + rng.Next(40);
                    for (int s = 0; s < drip; s++)
                    {
                        int y = y0 - s;
                        if (y < 0) break;
                        if (rng.NextDouble() < 0.15) x += rng.Next(-1, 2);
                        if (x < 0 || x >= w) break;
                        float k = 0.9f * (1f - s / (float)drip * 0.6f);
                        c.px[y * w + x] = Color.Lerp(c.px[y * w + x], bloodCol, k);
                        if (rng.NextDouble() < 0.5 && x + 1 < w) c.px[y * w + x + 1] = Color.Lerp(c.px[y * w + x + 1], bloodDark, k * 0.6f);
                    }
                }
            }

            // 5. Taches de sang (tablier, col, robe) et éclaboussures
            if (blood > 0f)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        var k = kind[i];
                        if (k == Px.Eye) continue;
                        float n = Fbm(x * 0.03f + 200f, y * 0.03f + 90f);
                        float th = Mathf.Lerp(0.82f, 0.64f, blood) + (k == Px.White ? -0.04f : 0.03f);
                        if (n > th) c.px[i] = Color.Lerp(c.px[i], n > th + 0.04f ? bloodDark : bloodCol, Mathf.Clamp01((n - th) * 8f) * 0.85f);
                    }
            int drops = Mathf.RoundToInt(200 * blood);
            for (int d = 0; d < drops; d++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                if (kind[y * w + x] == Px.Eye) continue;
                c.Disc(x, y, 0.8f + (float)rng.NextDouble() * 2f, bloodDark.WithAlpha(0.85f), 0.6f);
            }
        }

        // ================================================================== Squelette

        static List<Transform> Chain(Transform start)
        {
            var list = new List<Transform>();
            var t = start;
            while (t)
            {
                list.Add(t);
                if (t.childCount != 1) break;
                t = t.GetChild(0);
            }
            return list;
        }

        static int MaxChildren(Transform t)
        {
            int m = t.childCount;
            for (int i = 0; i < t.childCount; i++) m = Mathf.Max(m, MaxChildren(t.GetChild(i)));
            return m;
        }

        static Bounds BakedBounds(GameObject root)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh();
                smr.BakeMesh(m, false);
                foreach (var v in m.vertices)
                {
                    // BakeMesh(useScale: false) : sommets déjà à l'échelle monde, exprimés dans le repère
                    // (position + rotation) du renderer — ne pas réappliquer l'échelle.
                    var p = smr.transform.position + smr.transform.rotation * v;
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
                UnityEngine.Object.DestroyImmediate(m);
            }
            return b;
        }

        /// <summary>Remplace le corps de la Dépression par la servante posée assise. Renvoie l'os de la tête ou null.</summary>
        public static Transform TryMaiden(Transform body, OpponentView view)
        {
            if (!Choices || !Choices.useMaiden || !s_maiden) return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MaidenGltf);
            if (!asset)
            {
                Debug.LogWarning("[Lucidité] maiden.gltf non importé : le package glTFast est-il installé ? (Window > Package Manager). Dépression générée conservée.");
                return null;
            }
            var h = Spawn(asset, body, "MaidenModel");
            var root = h.transform;

            Transform rootJoint = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("_rootJoint", StringComparison.Ordinal) || t.name == "_rootJoint") { rootJoint = t; break; }
            if (!rootJoint || rootJoint.childCount < 3)
            {
                Debug.LogWarning("[Lucidité] Squelette de la servante non reconnu : Dépression générée conservée.");
                UnityEngine.Object.DestroyImmediate(h);
                return null;
            }

            // Topologie : la colonne est la branche qui contient l'embranchement à 3 enfants (bras + cou)
            Transform spineRoot = null;
            var legs = new List<Transform>();
            for (int i = 0; i < rootJoint.childCount; i++)
            {
                var c = rootJoint.GetChild(i);
                if (spineRoot == null && MaxChildren(c) >= 3) spineRoot = c; else legs.Add(c);
            }
            var spine = Chain(spineRoot);
            var chest = spine[spine.Count - 1];
            if (chest.childCount < 3 || legs.Count < 2) { UnityEngine.Object.DestroyImmediate(h); return null; }
            var branches = new List<List<Transform>>();
            for (int i = 0; i < chest.childCount; i++) branches.Add(Chain(chest.GetChild(i)));
            // Cou = la seule branche qui MONTE (les bras pendent). Sur ce squelette, bras et cou partent du même point :
            // on compare donc l'extrémité de chaque chaîne, pas sa racine.
            branches.Sort((a, b) => b[b.Count - 1].position.y.CompareTo(a[a.Count - 1].position.y));
            var neck = branches[0];
            var armA = branches[1];
            var armB = branches[2];
            var legA = Chain(legs[0]);
            var legB = Chain(legs[1]);

            // Orientation : le pied pointe vers la joueuse (+Z du corps)
            var footA = legA.Count >= 4 ? legA[3] : legA[legA.Count - 1];
            var toeA = legA.Count >= 5 ? legA[4] : null;
            if (toeA)
            {
                var fwd = toeA.position - footA.position; fwd.y = 0;
                if (fwd.sqrMagnitude > 1e-8f) root.rotation = Quaternion.FromToRotation(fwd.normalized, body.forward) * root.rotation;
            }

            // Échelle : mesurée sur le squelette debout (l'os de la tête est à ~87 % de la taille)
            var headBone = neck.Count >= 3 ? neck[2] : neck[neck.Count - 1];
            float boneHeight = headBone.position.y - Mathf.Min(footA.position.y, toeA ? toeA.position.y : footA.position.y);
            if (boneHeight > 1e-5f) root.localScale *= Choices.maidenHeight * 0.87f / boneHeight;
            else
            {
                var bb = BakedBounds(h);
                if (bb.size.y > 1e-4f) root.localScale *= Choices.maidenHeight / bb.size.y;
            }
            Debug.Log($"[Lucidité] Servante : hauteur os tête mesurée {boneHeight:0.####} → échelle {root.localScale.x:0.#####}");

            // Axes mesurés en pose de repos (avant de la plier) : visage vers la joueuse, paumes vers le bas.
            // Sur ce squelette, neck[1] (Bone.018) porte la tête ; neck[2] n'est que le sommet du crâne / les couettes.
            var headPivot = neck.Count >= 3 ? neck[1] : neck[neck.Count - 1];
            view.faceAxis = headPivot.InverseTransformDirection(body.forward);
            Vector3 PalmOf(List<Transform> arm) => arm.Count >= 4 ? arm[3].InverseTransformDirection(-body.up) : Vector3.down;
            var palmA = PalmOf(armA);
            var palmB = PalmOf(armB);

            // Pose assise
            Vector3 D(float x, float y, float z) => body.TransformDirection(new Vector3(x, y, z));
            for (int i = 0; i < spine.Count - 1; i++) Aim(spine[i], spine[i + 1], D(0, 1, 0.12f + i * 0.06f));
            foreach (var leg in new[] { legA, legB })
            {
                if (leg.Count < 4) continue;
                float side = Mathf.Sign(body.InverseTransformPoint(leg[1].position).x);
                Aim(leg[1], leg[2], D(side * 0.1f, -0.1f, 1f));   // cuisse
                Aim(leg[2], leg[3], D(0, -1f, 0.1f));            // tibia
            }
            foreach (var arm in new[] { armA, armB })
            {
                if (arm.Count < 4) continue;
                float side = Mathf.Sign(body.InverseTransformPoint(arm[1].position).x);
                Aim(arm[1], arm[2], D(side * 0.22f, -0.65f, 0.7f));  // bras
                Aim(arm[2], arm[3], D(-side * 0.5f, 0.25f, 0.82f)); // avant-bras vers les cartes
            }
            if (neck.Count >= 3)
            {
                Aim(neck[0], neck[1], D(0, 1, 0.12f));
                Aim(neck[1], neck[2], D(0, 1, -0.12f)); // tête relevée : on voit son visage
            }
            var head = headPivot;

            // Bassin posé sur la chaise
            var hip = body.InverseTransformPoint(spineRoot.position);
            root.position += body.TransformVector(new Vector3(-hip.x, -hip.y + 0.03f, -hip.z - 0.03f));
            // Pieds au sol si la taille l'impose
            var after = BakedBounds(h);
            float floorY = body.parent ? body.parent.position.y : 0f;
            float lift = floorY - after.min.y;
            if (lift > 0.02f && lift < 0.3f) root.position += Vector3.up * lift; // garde-fou : ignore une mesure aberrante

            foreach (var smr in h.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = s_maiden;
                smr.sharedMaterials = mats;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            view.headMotion = 0.35f;
            view.bleedPoints.Add(neck.Count >= 3 ? neck[2] : head);

            // Bras posés en jeu sur les bords de son éventail (la HandView est branchée par PrefabFactory)
            if (armA.Count >= 4 && armB.Count >= 4)
            {
                var ik = view.gameObject.AddComponent<OpponentArmsIK>();
                ik.body = body;
                OpponentArmsIK.Arm MakeArm(List<Transform> arm, Vector3 palm) => new OpponentArmsIK.Arm
                {
                    upper = arm[1], forearm = arm[2], hand = arm[3],
                    handTip = arm.Count >= 5 ? arm[4] : null,
                    palmLocal = palm
                };
                ik.left = MakeArm(armA, palmA);
                ik.right = MakeArm(armB, palmB);
            }
            if (armA.Count >= 3) view.bleedPoints.Add(armA[2]); // coudes (pas les mains : elles tiennent ses cartes)
            if (armB.Count >= 3) view.bleedPoints.Add(armB[2]);
            Debug.Log("[Lucidité] Dépression : « psx maiden » (CC-BY 4.0, petya-petyavich), grisée, blessée, posée assise.");
            return head;
        }

        /// <summary>Sang au sol sous sa chaise et gouttes sur la table, de son côté.</summary>
        public static void AddMaidenBlood(Transform opponentRoot)
        {
            var b = new MeshBuilder();
            b.Plane(new Vector3(0.05f, 0.004f, 0.12f), Vector3.up, Vector3.forward, new Vector2(0.55f, 0.42f));
            b.Plane(new Vector3(-0.22f, 0.005f, 0.35f), Vector3.up, Vector3.forward, new Vector2(0.16f, 0.12f));
            Part("FloorBlood", opponentRoot, b, BloodPool, false);
            var drops = new MeshBuilder();
            var rng = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                float r = 0.006f + (float)rng.NextDouble() * 0.014f;
                drops.Ellipsoid(new Vector3((float)rng.NextDouble() * 0.5f - 0.25f, 0.7808f, 0.38f + (float)rng.NextDouble() * 0.12f), new Vector3(r * 0.5f, 0.0006f, r * 0.6f), 2, 8);
            }
            Part("TableBlood", opponentRoot, drops, BloodPool, false);
        }
    }
}
