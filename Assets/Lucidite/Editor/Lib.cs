using TMPro;
using UnityEditor;
using UnityEngine;

namespace Lucidite.EditorTools
{
    /// <summary>Bibliothèque partagée par les fabriques (matériaux, textures) + helpers de construction.</summary>
    public static class Lib
    {
        // Textures
        public static Texture2D TexWood, TexWoodDark, TexWallpaper, TexFloor, TexConcrete, TexCardFront, TexCardBack,
            TexSkin, TexGraySkin, TexFabric, TexUniform, TexBlood, TexWound, TexCrack, TexEyeBags, TexPaper,
            TexDrop, TexFlame, TexDot;

        // Matériaux
        public static Material Wood, WoodDark, Wallpaper, Floor, Ceiling, Baseboard, CardFront, CardBack, CardArt, Highlight,
            Skin, GraySkin, Hair, Uniform, UniformGray, Collar, Scarf, Cuff, Eye, EyeBags, Wound, Crack, Blood, BloodPool,
            Wax, Flame, Wick, LampShade, Cord, Bulb, Glass, Plastic, Paper, Mattress, Void, DoorGap, WindowGap,
            ParticleDust, ParticleSmoke, Metal;
        // Matériaux v2
        public static Material Wainscot, DepressionFace, SailorGray, SleeveNavy, SleeveGray, Skirt, Blanket, Books, Cardboard,
            Rust, CostOrb, PlayerSkin, CardIcon, CardBonus;

        public static CardArtLibrary ArtLibrary;
        public static Mesh LampShadeMesh;
        public static Material VHSMaterial;
        public static Sprite SpriteDrop, SpriteFlame, SpriteOrb, SpriteVignette, SpriteGradient, SpriteScratch, SpritePage,
            SpriteCardFrame, SpriteSword, SpriteShield, SpriteEye, SpriteUp, SpriteDot;
        public static TMP_FontAsset FontTitle, FontBody, FontMono;

        // ------------------------------------------------------------------ Construction

        public static GameObject Empty(string name, Transform parent, Vector3 pos, Vector3 euler = default)
        {
            var g = new GameObject(name);
            if (parent) g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            return g;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);
            if (parent) g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            if (r && mat) r.sharedMaterial = mat;
            return g;
        }

        /// <summary>Place un capsule/cylindre (hauteur 2 unités) entre deux points locaux.</summary>
        public static GameObject Limb(PrimitiveType type, string name, Transform parent, Vector3 from, Vector3 to, float radius, Material mat)
        {
            var g = Prim(type, name, parent, (from + to) * 0.5f, Vector3.one, mat);
            var dir = to - from;
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            float len = dir.magnitude;
            g.transform.localScale = new Vector3(radius * 2f, type == PrimitiveType.Capsule ? (len + radius * 2f) * 0.5f : len * 0.5f, radius * 2f);
            return g;
        }

        /// <summary>Quad dont la face visible est orientée vers <paramref name="facing"/> (direction locale).</summary>
        public static GameObject Decal(string name, Transform parent, Vector3 pos, Vector3 facing, Vector2 size, Material mat, float roll = 0f)
        {
            var g = Prim(PrimitiveType.Quad, name, parent, pos, new Vector3(size.x, size.y, 1f), mat);
            // Le quad Unity est visible depuis -Z : on oriente +Z à l'opposé de la direction voulue.
            g.transform.localRotation = Quaternion.LookRotation(-facing.normalized) * Quaternion.Euler(0f, 0f, roll);
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static TextMeshPro Text3D(Transform parent, string name, Vector3 pos, Vector2 size, string text, Color color, float maxSize, FontStyles style = FontStyles.Normal, TMP_FontAsset font = null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var t = g.AddComponent<TextMeshPro>();
            t.font = font ? font : (FontBody ? FontBody : TMP_Settings.defaultFontAsset);
            t.text = text;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.05f;
            t.fontSizeMax = maxSize;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = size;
            g.transform.localPosition = pos;
            var r = g.GetComponent<MeshRenderer>();
            if (r) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        public static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Objet portant un maillage généré (sauvé dans le conteneur courant de <see cref="MeshStore"/>).</summary>
        public static GameObject Part(string name, Transform parent, MeshBuilder b, Material mat, bool castShadows = true)
        {
            var g = new GameObject(name);
            if (parent) g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = MeshStore.Save(b, name);
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        /// <summary>
        /// Modèle importé optionnel : Assets/Lucidite/Imported/&lt;name&gt;.prefab/.fbx/.obj/.glb remplace le modèle généré.
        /// </summary>
        public static GameObject TryImported(string name, Transform parent)
        {
            foreach (var ext in new[] { ".prefab", ".fbx", ".FBX", ".obj", ".glb", ".gltf" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetFactory.Root}/Imported/{name}{ext}");
                if (!asset) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                g.name = "Imported_" + name;
                g.transform.SetParent(parent, false);
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                Debug.Log($"[Lucidité] Modèle importé utilisé : {name}{ext}");
                return g;
            }
            return null;
        }

        public static void NoShadows(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
