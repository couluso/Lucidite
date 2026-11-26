using System.IO;
using UnityEditor;
using UnityEngine;
using static Lucidite.EditorTools.Lib;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Bras de la joueuse : "PSX FPS Hands" (CC0, NiKo Gillsky), squelette type Rigify,
    /// mis à l'échelle et orientés devant la caméra, posés en jeu par <see cref="FpsArmsIK"/>.
    /// </summary>
    public static partial class ThirdParty
    {
        const string FpsHands = Dir + "/FPSHands";
        const string FpsHandsFbx = FpsHands + "/FPS_Psx_Hands.fbx";

        static void CopyFpsHandsFromDocuments(string src)
        {
            string s = Path.Combine(src, "FPS_Psx_Hands");
            if (!Directory.Exists(s) || File.Exists(FpsHandsFbx)) return;
            AssetFactory.EnsureFolder(FpsHands);
            foreach (var f in Directory.GetFiles(s, "*.fbx")) { File.Copy(f, FpsHandsFbx, true); break; }
            var png = Path.Combine(s, "FPSarm_256px.png");
            if (File.Exists(png)) File.Copy(png, $"{FpsHands}/FPSarm_256px.png", true);
            AssetDatabase.Refresh();
        }

        /// <summary>Construit les bras importés sous "tremor". Renvoie false si l'asset est absent ou non reconnu.</summary>
        public static bool TryFpsArms(Transform tremor, HandView hv, PlayerArms arms, Transform root)
        {
            if (!Choices || !Choices.useFpsHands) return false;
            CopyFpsHandsFromDocuments(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "AssetsUnity"));
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FpsHandsFbx);
            if (!asset) return false;

            var holder = Spawn(asset, tremor, "FPSArmsModel");
            var L = FpsArmsIK.Bind(holder.transform, "L");
            var R = FpsArmsIK.Bind(holder.transform, "R");
            if (!L.upper || !L.forearm || !L.hand || !R.upper || !R.forearm || !R.hand)
            {
                Debug.LogWarning("[Lucidité] Squelette des PSX FPS Hands non reconnu : bras générés conservés.");
                Object.DestroyImmediate(holder);
                return false;
            }

            // Échelle : avant-bras d'environ 25 cm
            float fore = Vector3.Distance(L.forearm.position, L.hand.position);
            if (fore > 1e-5f) holder.transform.localScale *= 0.25f / fore;

            // Orientation : les mains vers l'avant de la caméra (+Z local de "tremor")
            var shoulders = (L.upper.position + R.upper.position) * 0.5f;
            var hands = (L.hand.position + R.hand.position) * 0.5f;
            var fwd = Vector3.ProjectOnPlane(hands - shoulders, tremor.up);
            if (fwd.sqrMagnitude > 1e-8f)
                holder.transform.rotation = Quaternion.FromToRotation(fwd.normalized, tremor.forward) * holder.transform.rotation;

            // Épaules juste sous / derrière la caméra (hors champ)
            shoulders = (L.upper.position + R.upper.position) * 0.5f;
            var targetShoulders = tremor.TransformPoint(new Vector3(0f, -0.33f, -0.02f));
            holder.transform.position += targetShoulders - shoulders;

            // Gauche/droite à l'écran (on ne se fie pas aux suffixes .L/.R)
            if (tremor.InverseTransformPoint(L.upper.position).x > tremor.InverseTransformPoint(R.upper.position).x) (L, R) = (R, L);

            // Matériau PSX + blessures progressives
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FpsHands}/FPSarm_256px.png");
            // Teinte assombrie : sous la lampe, la peau claire se confondait avec la table
            var mat = AssetFactory.Lit("M_TP_FPSArms", new Color(0.68f, 0.63f, 0.6f), tex, 0.25f);
            var dmg = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AssetFactory.TexDir}/T_SkinDamage.png");
            if (dmg) mat.SetTexture("_DamageMap", dmg);
            AssetFactory.DoubleSided(mat);
            foreach (var smr in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                smr.sharedMaterials = mats;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Points de saignement le long des avant-bras (ils suivent les os)
            foreach (var a in new[] { L, R })
                for (int i = 0; i < 3; i++)
                {
                    var p = Empty($"Bleed{i}", a.forearm, Vector3.zero).transform;
                    p.position = Vector3.Lerp(a.forearm.position, a.hand.position, 0.35f + i * 0.22f);
                    arms.bleedPoints.Add(p);
                }

            var ik = root.gameObject.AddComponent<FpsArmsIK>();
            ik.hand = hv;
            ik.space = tremor;
            ik.left = L;
            ik.right = R;
            Debug.Log("[Lucidité] Bras : PSX FPS Hands (CC0, NiKo Gillsky), posés par cinématique inverse.");
            return true;
        }
    }
}
