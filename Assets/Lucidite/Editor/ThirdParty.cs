using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Lucidite.EditorTools.Lib;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Intègre les packs téléchargés (Documents/AssetsUnity) :
    /// copie, réglages d'import, mise à l'échelle et orientation automatiques, UV générées (les modèles Quaternius
    /// n'en ont pas), matériaux PSX (bois Apfelgarten), décor de la pièce, pose assise du personnage.
    /// </summary>
    public static partial class ThirdParty
    {
        public const string Dir = AssetFactory.Root + "/ThirdParty";
        const string Quat = Dir + "/Quaternius";
        const string Apfel = Dir + "/Apfelgarten";
        const string Chara = Dir + "/HighSchoolChara";

        public static ImportChoices Choices { get; private set; }
        static Material s_wood, s_woodDark, s_wardrobe, s_plant, s_character;

        // ================================================================== Préparation

        /// <summary>Copie les fichiers depuis Documents/AssetsUnity s'ils manquent, règle les imports, crée les matériaux.</summary>
        public static void Prepare()
        {
            Choices = AssetFactory.LoadOrCreate<ImportChoices>($"{AssetFactory.DataDir}/ImportChoices.asset");
            CopyFromDocuments();
            AssetDatabase.Refresh();

            foreach (var path in Directory.GetFiles(Quat, "*.fbx")) ConfigureModel(path.Replace('\\', '/'), false);
            if (File.Exists($"{Chara}/HS_Chara_Rig.fbx")) ConfigureModel($"{Chara}/HS_Chara_Rig.fbx", true);
            if (File.Exists(FpsHandsFbx)) { ConfigureModel(FpsHandsFbx, true); ConfigureTexture($"{FpsHands}/FPSarm_256px.png", false); }
            foreach (var ext in new[] { "*.png", "*.jpg" })
            {
                if (Directory.Exists(Apfel)) foreach (var p in Directory.GetFiles(Apfel, ext)) ConfigureTexture(p.Replace('\\', '/'), true);
                if (Directory.Exists(Chara)) foreach (var p in Directory.GetFiles(Chara, ext)) ConfigureTexture(p.Replace('\\', '/'), false);
            }

            var w1 = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Apfel}/wood01_tex_128x128.png");
            var w6 = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Apfel}/wood06_tex_128x128.png");
            var wr = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Apfel}/wood_wardrobe01_128x128.png");
            bool apf = Choices.useApfelgartenWood && w1 && w6;
            s_wood = apf ? AssetFactory.Lit("M_TP_Wood", Color.white, w1, 0.2f) : Wood;
            s_woodDark = apf ? AssetFactory.Lit("M_TP_WoodDark", new Color(0.8f, 0.78f, 0.76f), w6, 0.15f) : WoodDark;
            s_wardrobe = apf && wr ? AssetFactory.Lit("M_TP_Wardrobe", Color.white, wr, 0.15f) : s_woodDark;
            s_plant = AssetFactory.Lit("M_TP_DeadPlant", Palette.Hex("4A4630"), null, 0.1f);
            var charTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Chara}/HS_Chara_Texture.jpg");
            s_character = AssetFactory.Lit("M_TP_Character", Choices.characterTint, charTex, 0.2f);
            PrepareMaiden();
        }

        /// <summary>Remplace les textures du bois généré par celles d'Apfelgarten (même matériaux, même UV au mètre).</summary>
        public static void ApplyWoodToGeneratedMaterials()
        {
            if (!Choices || !Choices.useApfelgartenWood) return;
            var w1 = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Apfel}/wood01_tex_128x128.png");
            var w6 = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Apfel}/wood06_tex_128x128.png");
            if (w1 && Wood) { Wood.SetTexture("_BaseMap", w1); Wood.SetTextureScale("_BaseMap", Vector2.one * 1.6f); EditorUtility.SetDirty(Wood); }
            if (w6 && WoodDark) { WoodDark.SetTexture("_BaseMap", w6); WoodDark.SetTextureScale("_BaseMap", Vector2.one * 2f); EditorUtility.SetDirty(WoodDark); }
        }

        static void CopyFromDocuments()
        {
            string src = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AssetsUnity");
            AssetFactory.EnsureFolder(Quat); AssetFactory.EnsureFolder(Apfel); AssetFactory.EnsureFolder(Chara);
            if (!Directory.Exists(src)) return;
            void Copy(string from, string to)
            {
                if (File.Exists(to) || !File.Exists(from)) return;
                File.Copy(from, to);
            }
            foreach (var f in new[] { "Table", "Chair", "Closet", "BookCaseBooks", "Stool", "Lamp", "Plant", "Vase" })
                Copy(Path.Combine(src, "Furniture Pack by @Quaternius", "FBX", f + ".fbx"), $"{Quat}/{f}.fbx");
            string tex = Path.Combine(src, "Chairs&Tables_PSX_Asset_Pack", "128_textures");
            if (Directory.Exists(tex)) foreach (var p in Directory.GetFiles(tex, "*.png")) Copy(p, $"{Apfel}/{Path.GetFileName(p)}");
            Copy(Path.Combine(src, "PSX HIGH SCHOOL CHARA", "New", "HS_CHARA_RIG [New].fbx"), $"{Chara}/HS_Chara_Rig.fbx");
            Copy(Path.Combine(src, "PSX HIGH SCHOOL CHARA", "New", "HS_CHARA_TEXTURE.jpg"), $"{Chara}/HS_Chara_Texture.jpg");
        }

        static void ConfigureModel(string path, bool rigged)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!imp) return;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.importAnimation = false;
            imp.animationType = rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.isReadable = true;
            imp.SaveAndReimport();
        }

        static void ConfigureTexture(string path, bool repeat)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!imp) return;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
        }

        static GameObject Asset(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // ================================================================== Placement générique

        static GameObject Spawn(GameObject asset, Transform parent, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetParent(holder.transform, false);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(a);
            foreach (var a in inst.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(a);
            return holder;
        }

        static Bounds WorldBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Recadre : base à y = 0, centré en x/z, à l'échelle demandée (uniforme ou non).</summary>
        static void Fit(GameObject holder, Vector3 target, bool uniformByHeight)
        {
            var b = WorldBounds(holder);
            var s = holder.transform.localScale;
            if (uniformByHeight) s *= target.y / Mathf.Max(0.001f, b.size.y);
            else s = Vector3.Scale(s, new Vector3(target.x / Mathf.Max(0.001f, b.size.x), target.y / Mathf.Max(0.001f, b.size.y), target.z / Mathf.Max(0.001f, b.size.z)));
            holder.transform.localScale = s;
            b = WorldBounds(holder);
            var parentPos = holder.transform.parent ? holder.transform.parent.position : Vector3.zero;
            holder.transform.position += new Vector3(parentPos.x - b.center.x, parentPos.y - b.min.y, parentPos.z - b.center.z);
        }

        /// <summary>Tourne l'objet pour que sa partie haute (dossier) soit côté -Z local du parent.</summary>
        static void OrientBackTo(GameObject holder, Vector3 desiredBack)
        {
            var b = WorldBounds(holder);
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    if (w.y > b.min.y + b.size.y * 0.7f) { sum += w - b.center; n++; }
                }
            }
            if (n == 0) return;
            var back = new Vector3(sum.x, 0, sum.z);
            if (back.sqrMagnitude < 1e-6f) return;
            // Aligne sur l'axe dominant pour éviter les angles bizarres
            back = Mathf.Abs(back.x) > Mathf.Abs(back.z) ? new Vector3(Mathf.Sign(back.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(back.z));
            var desired = holder.transform.parent ? holder.transform.parent.TransformDirection(desiredBack) : desiredBack;
            holder.transform.rotation = Quaternion.FromToRotation(back, desired) * holder.transform.rotation;
        }

        /// <summary>Tourne de 90° si le côté long n'est pas aligné avec l'axe X.</summary>
        static void LongSideAlongX(GameObject holder, bool alongX)
        {
            var b = WorldBounds(holder);
            if ((b.size.z > b.size.x) == alongX) holder.transform.Rotate(0, 90, 0, Space.World);
        }

        /// <summary>UV projetées en boîte (en mètres) + matériaux PSX choisis selon le nom des matériaux d'origine.</summary>
        static void Retexture(GameObject holder, float tileMeters, Func<string, Material> pick)
        {
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
                mesh.name = mf.sharedMesh.name + "_psx";
                ProjectUVs(mesh, mf.transform, tileMeters);
                mf.sharedMesh = MeshStore.Save(mesh);
                Remap(mf.GetComponent<Renderer>(), pick);
            }
            foreach (var smr in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Remap(smr, pick);
        }

        static void Remap(Renderer r, Func<string, Material> pick)
        {
            if (!r) return;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = pick(mats[i] ? mats[i].name.ToLowerInvariant() : "");
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        static void ProjectUVs(Mesh mesh, Transform t, float tile)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            if (n == null || n.Length != v.Length) { mesh.RecalculateNormals(); n = mesh.normals; }
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var p = t.TransformPoint(v[i]);
                var nn = t.TransformDirection(n[i]);
                float ax = Mathf.Abs(nn.x), ay = Mathf.Abs(nn.y), az = Mathf.Abs(nn.z);
                if (ax >= ay && ax >= az) uv[i] = new Vector2(p.z, p.y) / tile;
                else if (ay >= az) uv[i] = new Vector2(p.x, p.z) / tile;
                else uv[i] = new Vector2(p.x, p.y) / tile;
            }
            mesh.uv = uv;
        }

        static Material FurnitureMat(string n)
        {
            if (n.Contains("darkwood") || n.Contains("darkbrown") || n.Contains("dark")) return s_woodDark;
            if (n.Contains("wood")) return s_wood;
            if (n.Contains("metal")) return Metal;
            if (n.Contains("cover")) return Books;
            if (n.Contains("page") || n.Contains("paper") || n.Contains("white")) return Paper;
            if (n.Contains("top") || n.Contains("cushion") || n.Contains("fabric") || n.Contains("cloth")) return Blanket;
            if (n.Contains("green") || n.Contains("leaf") || n.Contains("plant")) return s_plant;
            if (n.Contains("vase") || n.Contains("glass")) return Glass;
            return s_wood;
        }

        // ================================================================== Mobilier

        public static bool TryTable(Transform parent)
        {
            var a = Asset($"{Quat}/Table.fbx");
            if (!Choices || !Choices.useQuaterniusFurniture || !a) return false;
            var h = Spawn(a, parent, "TableModel");
            LongSideAlongX(h, true);
            Fit(h, new Vector3(1.3f, 0.78f, 0.9f), false);
            Retexture(h, 0.45f, FurnitureMat);
            Debug.Log("[Lucidité] Table : modèle Quaternius + bois Apfelgarten.");
            return true;
        }

        public static bool TryChair(Transform parent)
        {
            var a = Asset($"{Quat}/Chair.fbx");
            if (!Choices || !Choices.useQuaterniusFurniture || !a) return false;
            var h = Spawn(a, parent, "ChairModel");
            OrientBackTo(h, Vector3.back);
            Fit(h, new Vector3(0, 0.95f, 0), true);
            Retexture(h, 0.35f, FurnitureMat);
            return true;
        }

        /// <summary>Meubles de décor contre les murs de la pièce.</summary>
        public static void AddDecor(Transform room)
        {
            if (!Choices || !Choices.useQuaterniusDecor) return;
            var decor = new GameObject("Decor").transform;
            decor.SetParent(room, false);
            const float wall = 2.13f;
            void Place(string file, Vector3 pos, float height, Vector3 wallNormal, Func<string, Material> mat)
            {
                var a = Asset($"{Quat}/{file}.fbx");
                if (!a) return;
                var anchor = new GameObject(file).transform;
                anchor.SetParent(decor, false);
                anchor.localPosition = pos;
                var h = Spawn(a, anchor, file + "Model");
                if (wallNormal != Vector3.zero)
                {
                    // Côté le plus fin contre le mur
                    var b = WorldBounds(h);
                    bool thinAlongZ = b.size.z < b.size.x;
                    bool wallAlongZ = Mathf.Abs(wallNormal.z) > 0.5f;
                    if (thinAlongZ != wallAlongZ) h.transform.Rotate(0, 90, 0, Space.World);
                }
                Fit(h, new Vector3(0, height, 0), true);
                if (wallNormal != Vector3.zero)
                {
                    var b = WorldBounds(h);
                    if (Mathf.Abs(wallNormal.z) > 0.5f) h.transform.position += new Vector3(0, 0, Mathf.Sign(-wallNormal.z) * wall - (wallNormal.z < 0 ? b.max.z : b.min.z));
                    else h.transform.position += new Vector3(Mathf.Sign(-wallNormal.x) * wall - (wallNormal.x < 0 ? b.max.x : b.min.x), 0, 0);
                }
                Retexture(h, 0.45f, mat);
            }
            Place("BookCaseBooks", new Vector3(-0.55f, 0, 1.9f), 1.55f, Vector3.back, FurnitureMat);
            Place("Closet", new Vector3(1.85f, 0, -1.4f), 1.9f, Vector3.left, n => n.Contains("wood") ? s_wardrobe : FurnitureMat(n));
            Place("Stool", new Vector3(-0.95f, 0, 0.95f), 0.48f, Vector3.zero, FurnitureMat);
            Place("Lamp", new Vector3(1.85f, 0, 0.25f), 1.5f, Vector3.left, FurnitureMat);
            Place("Plant", new Vector3(0.25f, 0, 1.95f), 0.75f, Vector3.zero, FurnitureMat);
            Place("Vase", new Vector3(-0.95f, 0.48f, 0.95f), 0.22f, Vector3.zero, FurnitureMat);
            Debug.Log("[Lucidité] Décor Quaternius ajouté à la pièce.");
        }

        // ================================================================== Personnage

        static Transform Bone(Transform root, string suffix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(suffix, StringComparison.Ordinal) && (t.name.Length == suffix.Length || t.name[t.name.Length - suffix.Length - 1] == ':'))
                    return t;
            return null;
        }

        static void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (!bone || !child) return;
            var cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(cur, dir.normalized) * bone.rotation;
        }

        /// <summary>
        /// Remplace le corps de la Dépression par le personnage PSX : orienté vers la joueuse, mis à l'échelle,
        /// posé assis, avant-bras vers la table, tête baissée, teinte grise. Renvoie la tête (os) ou null.
        /// </summary>
        public static Transform TryCharacter(Transform body, OpponentView view)
        {
            var a = Asset($"{Chara}/HS_Chara_Rig.fbx");
            if (!Choices || !Choices.useHighSchoolCharacter || !a) return null;
            var h = Spawn(a, body, "CharacterModel");
            var root = h.transform;

            var hips = Bone(root, "Hips");
            var spine = Bone(root, "Spine"); var spine1 = Bone(root, "Spine1"); var spine2 = Bone(root, "Spine2");
            var neck = Bone(root, "Neck"); var head = Bone(root, "Head"); var headTop = Bone(root, "HeadTop_End");
            var lUp = Bone(root, "LeftUpLeg"); var lLeg = Bone(root, "LeftLeg"); var lFoot = Bone(root, "LeftFoot"); var lToe = Bone(root, "LeftToeBase");
            var rUp = Bone(root, "RightUpLeg"); var rLeg = Bone(root, "RightLeg"); var rFoot = Bone(root, "RightFoot");
            var lArm = Bone(root, "LeftArm"); var lFore = Bone(root, "LeftForeArm"); var lHand = Bone(root, "LeftHand");
            var rArm = Bone(root, "RightArm"); var rFore = Bone(root, "RightForeArm"); var rHand = Bone(root, "RightHand");
            if (!hips || !head || !lUp || !lArm)
            {
                Debug.LogWarning("[Lucidité] Squelette du personnage non reconnu : Dépression générée conservée.");
                UnityEngine.Object.DestroyImmediate(h);
                return null;
            }

            // Orientation : les orteils regardent vers +Z (la joueuse, dans l'espace du corps)
            if (lToe && lFoot)
            {
                var fwd = lToe.position - lFoot.position; fwd.y = 0;
                if (fwd.sqrMagnitude > 1e-6f) root.rotation = Quaternion.FromToRotation(fwd.normalized, body.forward) * root.rotation;
            }
            // Échelle
            float minY = Mathf.Min(lFoot ? lFoot.position.y : hips.position.y, lToe ? lToe.position.y : hips.position.y);
            float height = (headTop ? headTop.position.y : head.position.y + 0.1f) - minY;
            if (height > 0.01f) root.localScale *= Choices.characterHeight / height;

            // Pose assise (directions dans l'espace du corps : +Z = vers la joueuse)
            Vector3 D(float x, float y, float z) => body.TransformDirection(new Vector3(x, y, z));
            Aim(spine, spine1 ? spine1 : neck, D(0, 1, 0.12f));
            Aim(spine1, spine2 ? spine2 : neck, D(0, 1, 0.2f));
            Aim(spine2, neck, D(0, 1, 0.25f));
            Aim(lUp, lLeg, D(0.12f, -0.08f, 1f)); Aim(rUp, rLeg, D(-0.12f, -0.08f, 1f));
            Aim(lLeg, lFoot, D(0, -1, 0.08f)); Aim(rLeg, rFoot, D(0, -1, 0.08f));
            // Le côté "Left" du personnage est à sa gauche : face à +Z, sa gauche est vers +X dans l'espace du corps ?
            float lSide = Mathf.Sign(body.InverseTransformPoint(lArm.position).x);
            Aim(lArm, lFore, D(lSide * 0.25f, -0.6f, 0.75f)); Aim(rArm, rFore, D(-lSide * 0.25f, -0.6f, 0.75f));
            Aim(lFore, lHand, D(-lSide * 0.55f, 0.3f, 0.78f)); Aim(rFore, rHand, D(lSide * 0.55f, 0.3f, 0.78f));
            Aim(head, headTop, D(0, 1, 0.35f));

            // Hanches posées sur la chaise (origine du corps)
            var hipLocal = body.InverseTransformPoint(hips.position);
            root.position += body.TransformVector(new Vector3(-hipLocal.x, -hipLocal.y + 0.04f, -hipLocal.z - 0.02f));

            foreach (var smr in h.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = s_character;
                smr.sharedMaterials = mats;
            }

            // Fissures sur le visage et le torse
            if (view != null && headTop)
            {
                var center = (head.position + headTop.position) * 0.5f;
                float r = Vector3.Distance(head.position, headTop.position) * 0.55f;
                var fwd = body.forward;
                var right = body.right;
                var cracks = new[]
                {
                    Decal("Crack0", null, center + fwd * r + Vector3.up * r * 0.3f - right * r * 0.3f, fwd, Vector2.one * r * 0.7f, Crack),
                    Decal("Crack1", null, center + fwd * r * 0.95f + right * r * 0.35f, fwd + right * 0.4f, Vector2.one * r * 0.6f, Crack, 40f),
                    Decal("Crack2", null, center + fwd * r * 0.9f - Vector3.up * r * 0.3f - right * r * 0.4f, fwd - right * 0.4f, Vector2.one * r * 0.55f, Crack, 160f)
                };
                foreach (var c in cracks)
                {
                    // Decal() place en local : on recale en monde
                    c.transform.SetParent(head, true);
                }
                view.cracks.AddRange(cracks);
            }
            Debug.Log("[Lucidité] Dépression : personnage PSX High School (posé assis).");
            return head;
        }
    }
}
