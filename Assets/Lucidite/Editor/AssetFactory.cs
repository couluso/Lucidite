using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lucidite.EditorTools
{
    /// <summary>Création de matériaux URP, maillages et utilitaires d'assets.</summary>
    public static class AssetFactory
    {
        public const string Root = "Assets/Lucidite";
        public const string TexDir = Root + "/Art/Textures";
        public const string MatDir = Root + "/Art/Materials";
        public const string MeshDir = Root + "/Art/Meshes";
        public const string PrefabDir = Root + "/Prefabs";
        public const string UIPrefabDir = Root + "/Prefabs/UI";
        public const string DataDir = Root + "/Data";
        public const string CardDir = Root + "/Data/Cards";
        public const string ItemDir = Root + "/Data/Items";
        public const string SceneDir = Root + "/Scenes";
        public const string SettingsDir = Root + "/Settings";

        public static void EnsureFolders()
        {
            foreach (var d in new[] { TexDir, MatDir, MeshDir, PrefabDir, UIPrefabDir, DataDir, CardDir, ItemDir, SceneDir, SettingsDir })
                EnsureFolder(d);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static T SaveAsset<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                Object.DestroyImmediate(asset, true);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Crée ou récupère un ScriptableObject (conserve le GUID pour ne pas casser les références).</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // ------------------------------------------------------------------ Matériaux

        static Shader UrpLit => Shader.Find("Universal Render Pipeline/Lit");

        /// <summary>Vrai : les matériaux "Lit" utilisent le shader PS1 (sinon URP/Lit).</summary>
        public static bool UsePSX = true;

        static Shader LitShader
        {
            get
            {
                if (!UsePSX) return UrpLit;
                var psx = Shader.Find("Lucidite/PSXLit");
                if (psx == null || ShaderUtil.ShaderHasError(psx))
                {
                    if (psx != null) Debug.LogWarning("[Lucidité] Le shader PSXLit a des erreurs : repli sur URP/Lit.");
                    return UrpLit;
                }
                return psx;
            }
        }
        static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit");
        static Shader ParticleShader => Shader.Find("Universal Render Pipeline/Particles/Unlit");

        static Material LoadOrNew(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            return m;
        }

        public static Material Lit(string name, Color color, Texture2D tex = null, float smoothness = 0.15f, Vector2? tiling = null)
        {
            var m = LoadOrNew(name, LitShader);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 1f);
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material LitEmissive(string name, Color color, Color emission, Texture2D tex = null, Texture2D emissionMap = null)
        {
            var m = Lit(name, color, tex);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            if (emissionMap) m.SetTexture("_EmissionMap", emissionMap);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material LitCutout(string name, Texture2D tex, Color color, float cutoff = 0.4f)
        {
            var m = Lit(name, color, tex, 0.3f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material DoubleSided(Material m)
        {
            m.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Unlit(string name, Color color, Texture2D tex = null)
        {
            var m = LoadOrNew(name, UnlitShader);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Particle(string name, Texture2D tex, Color color, bool additive)
        {
            var m = LoadOrNew(name, ParticleShader);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ Maillages

        /// <summary>Abat-jour conique (double face), pointe vers le haut.</summary>
        public static Mesh LampShade(int segments = 10)
        {
            string path = $"{MeshDir}/LampShade.asset";
            var mesh = new Mesh { name = "LampShade" };
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            float rTop = 0.035f, rBottom = 0.17f, hgt = 0.14f;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * rTop, 0f, Mathf.Sin(a) * rTop));
                verts.Add(new Vector3(Mathf.Cos(a) * rBottom, -hgt, Mathf.Sin(a) * rBottom));
                uvs.Add(new Vector2(i / (float)segments, 1f));
                uvs.Add(new Vector2(i / (float)segments, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.AddRange(new[] { a, c, b, b, c, d }); // l'intérieur est visible grâce au matériau double face (_Cull = Off)
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveAsset(mesh, path);
        }
    }
}
