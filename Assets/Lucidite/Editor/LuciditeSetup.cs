using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using static Lucidite.EditorTools.Lib;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Menu "Lucidité > Construire le jeu" : génère polices, textures, matériaux (shader PS1), données,
    /// modèles 3D, interface, post-process et la scène jouable complète. Relançable sans risque.
    /// </summary>
    public static class LuciditeSetup
    {
        public const string ScenePath = AssetFactory.SceneDir + "/Lucidite.unity";

        [MenuItem("Lucidité/Construire le jeu (setup complet)", priority = 0)]
        public static void BuildAllMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorUtility.DisplayDialog("Lucidité", "Le jeu a été construit.\nScène : " + ScenePath + "\n\nAppuie sur Play !", "OK");
        }

        [MenuItem("Lucidité/Ouvrir la scène", priority = 1)]
        public static void OpenScene() => EditorSceneManager.OpenScene(ScenePath);

        [MenuItem("Lucidité/Regénérer uniquement les cartes et objets", priority = 20)]
        public static void RebuildData()
        {
            AssetFactory.EnsureFolders();
            DataFactory.BuildConfig();
            DataFactory.BuildDatabase();
            AssetDatabase.SaveAssets();
            Debug.Log("[Lucidité] Cartes, actions et objets regénérés.");
        }

        public static void BuildAll()
        {
            Random.InitState(4242);
            try
            {
                Step("Dossiers", 0.02f);
                AssetFactory.EnsureFolders();
                AssetFactory.EnsureFolder(AssetFactory.Root + "/Imported");
                AssetFactory.EnsureFolders();

                Step("Polices", 0.06f);
                BuildFonts();
                Step("Textures", 0.12f);
                BuildTextures();
                Step("Matériaux", 0.3f);
                BuildMaterials();
                Step("Assets importés (Quaternius, Apfelgarten, PSX)", 0.36f);
                ThirdParty.Prepare();
                ThirdParty.ApplyWoodToGeneratedMaterials();
                // Blessures progressives sur la peau (bras de la joueuse, Dépression générée)
                var skinDmg = ThirdParty.SkinDamageOverlay($"{AssetFactory.TexDir}/T_SkinDamage.png", 128, 33);
                foreach (var m in new[] { PlayerSkin, GraySkin, DepressionFace })
                    if (m) { m.SetTexture("_DamageMap", skinDmg); EditorUtility.SetDirty(m); }

                Step("Cartes et objets", 0.4f);
                var cfg = DataFactory.BuildConfig();
                DataFactory.BuildDatabase();

                Step("Modèles 3D", 0.5f);
                PrefabFactory.BuildCard();
                PrefabFactory.BuildCandle();
                var drip = PrefabFactory.BuildBloodDrip();
                PrefabFactory.BuildTable();
                PrefabFactory.BuildLamp();
                PrefabFactory.BuildRoom();
                PrefabFactory.BuildPlayerArms(drip);
                PrefabFactory.BuildOpponent();

                Step("Interface", 0.7f);
                var offerCard = UIFactory.CardUI("CardUI_Offer", 1.05f);
                var smallCard = UIFactory.CardUI("CardUI_Small", 0.8f);
                var itemOffer = UIFactory.ItemUI("ItemUI_Offer", false);
                var itemButton = UIFactory.ItemUI("ItemUI_Button", true);
                UIFactory.PSXOutput();
                UIFactory.GameUI(offerCard, smallCard, itemOffer, itemButton);

                Step("Post-process", 0.8f);
                BuildVolumeProfile();

                Step("Scène", 0.9f);
                AssetDatabase.SaveAssets();
                BuildScene(cfg);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Lucidité] Construction terminée : " + ScenePath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Step(string label, float p) => EditorUtility.DisplayProgressBar("Lucidité", label, p);

        // ------------------------------------------------------------------ Polices
        static void BuildFonts()
        {
            FontTitle = MakeFont("IMFellEnglishSC-Regular.ttf", "Font_IMFell");
            FontBody = MakeFont("SpecialElite-Regular.ttf", "Font_SpecialElite");
            FontMono = MakeFont("VT323-Regular.ttf", "Font_VT323");
        }

        static TMP_FontAsset MakeFont(string ttf, string assetName)
        {
            string dir = AssetFactory.Root + "/Fonts";
            string assetPath = $"{dir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{dir}/{ttf}");
            if (!font)
            {
                Debug.LogWarning($"[Lucidité] Police introuvable : {dir}/{ttf} — police par défaut utilisée.");
                return TMP_Settings.defaultFontAsset;
            }
            var fa = TMP_FontAsset.CreateFontAsset(font, 72, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (!fa) return TMP_Settings.defaultFontAsset;
            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, assetPath);
            if (fa.atlasTextures != null)
                foreach (var tex in fa.atlasTextures)
                    if (tex) { tex.name = assetName + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            if (fa.material) { fa.material.name = assetName + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            if (TMP_Settings.defaultFontAsset) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ------------------------------------------------------------------ Textures
        static Texture2D Tex(TextureFactory.Canvas c, string name, bool repeat = true, bool alpha = false) =>
            TextureFactory.Save(c, $"{AssetFactory.TexDir}/{name}.png", repeat: repeat, alpha: alpha);

        static Sprite Spr(TextureFactory.Canvas c, string name, bool point = false)
        {
            string path = $"{AssetFactory.TexDir}/UI/{name}.png";
            TextureFactory.Save(c, path, point: point, repeat: false, sprite: true, alpha: true);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D TexWoodV2, TexWoodDarkV2, TexFloorV2, TexWallpaperV2, TexFace, TexHair, TexTorso, TexSleeveNavy, TexSleeveGray,
            TexSkirt, TexCollar, TexBlanket, TexMattress, TexBooks, TexCardboard, TexMetal, TexRust, TexPlayerSkin, TexGraySkinV2, TexPaperV2;

        static void BuildTextures()
        {
            AssetFactory.EnsureFolder(AssetFactory.TexDir + "/UI");
            AssetFactory.EnsureFolder(AssetFactory.TexDir + "/Sigils");
            TexWoodV2 = Tex(TexturePainter.WornWood(256, Palette.WoodLight, 3, 6), "T_Wood");
            TexWoodDarkV2 = Tex(TexturePainter.WornWood(128, Palette.Wood, 9, 2), "T_WoodDark");
            TexFloorV2 = Tex(TexturePainter.WornWood(256, Palette.Hex("3A2C20"), 21, 4), "T_Floor");
            TexWallpaperV2 = Tex(TexturePainter.Wallpaper(256, 5), "T_Wallpaper");
            TexConcrete = Tex(TextureFactory.Concrete(128, 11), "T_Concrete");
            TexCardFront = Tex(TextureFactory.CardFront(), "T_CardFront", false);
            TexCardBack = Tex(TextureFactory.CardBack(), "T_CardBack", false);
            TexPlayerSkin = Tex(TexturePainter.Skin(Palette.Skin, 1, true, false), "T_Skin");
            TexGraySkinV2 = Tex(TexturePainter.Skin(Palette.Ash, 2, true, true), "T_GraySkin");
            TexFace = Tex(TexturePainter.DepressionFace(Palette.Ash), "T_DepressionFace");
            TexHair = Tex(TexturePainter.Hair(4), "T_Hair");
            TexTorso = Tex(TexturePainter.SailorTorso(Palette.Hex("4E4A48"), Palette.Hex("8A8682"), Palette.Blood, 6), "T_SailorGray");
            TexSleeveNavy = Tex(TexturePainter.Sleeve(Palette.Uniform, Palette.Bone, 7), "T_SleeveNavy");
            TexSleeveGray = Tex(TexturePainter.Sleeve(Palette.Hex("4E4A48"), Palette.Hex("8A8682"), 8), "T_SleeveGray");
            TexSkirt = Tex(TexturePainter.Fabric(64, Palette.Hex("3A3634"), 10), "T_Skirt");
            TexCollar = Tex(TexturePainter.Fabric(64, Palette.Hex("8A8682"), 11), "T_Collar");
            TexBlanket = Tex(TexturePainter.Fabric(128, Palette.Hex("5A4E48"), 12, 0.15f), "T_Blanket");
            TexMattress = Tex(TexturePainter.Fabric(128, Palette.Hex("8A7E70"), 13, 0.05f), "T_Mattress");
            TexBooks = Tex(TexturePainter.Books(14), "T_Books");
            TexCardboard = Tex(TexturePainter.Cardboard(15), "T_Cardboard");
            TexMetal = Tex(TexturePainter.Metal(Palette.Hex("4A4A48"), 16), "T_Metal");
            TexRust = Tex(TexturePainter.Metal(Palette.Hex("5A3A2A"), 17), "T_Rust");
            TexPaperV2 = Tex(TexturePainter.Paper(128, 18, true), "T_Paper");
            TexBlood = Tex(TextureFactory.Noise(32, Palette.Blood, 0.4f, 13, 0.2f), "T_Blood");
            TexWound = Tex(TextureFactory.Wound(1), "T_Wound", false, true);
            TexCrack = Tex(TextureFactory.Crack(4), "T_Crack", false, true);
            TexEyeBags = Tex(TextureFactory.EyeBags(), "T_EyeBags", false, true);
            TexDot = TextureFactory.Save(TextureFactory.SoftDot(), $"{AssetFactory.TexDir}/FX_Dot.png", point: false, repeat: false, alpha: true);

            SpriteDrop = Spr(TextureFactory.DropIcon(), "UI_Drop");
            SpriteFlame = Spr(TextureFactory.FlameIcon(), "UI_Flame");
            SpriteOrb = Spr(TexturePainter.Orb(128), "UI_Orb");
            SpriteVignette = Spr(TexturePainter.RadialVignette(256), "UI_Vignette");
            SpriteGradient = Spr(TexturePainter.Gradient(256, 8, false), "UI_Gradient");
            SpriteScratch = Spr(TexturePainter.Scratch(3), "UI_Scratch");
            SpritePage = Spr(TexturePainter.NotebookPage(500, 460, 9), "UI_NotebookPage");
            SpriteCardFrame = Spr(TexturePainter.UICardFrame(5), "UI_CardFrame");
            SpriteSword = Spr(TexturePainter.IntentIcon(TexturePainter.Icon.Sword), "UI_IntentAttack");
            SpriteShield = Spr(TexturePainter.IntentIcon(TexturePainter.Icon.Shield), "UI_IntentDefend");
            SpriteEye = Spr(TexturePainter.IntentIcon(TexturePainter.Icon.Eye), "UI_IntentCurse");
            SpriteUp = Spr(TexturePainter.IntentIcon(TexturePainter.Icon.Up), "UI_IntentBuff");

            ArtLibrary = AssetFactory.LoadOrCreate<CardArtLibrary>($"{AssetFactory.DataDir}/CardArtLibrary.asset");
            ArtLibrary.entries = new List<CardArtLibrary.Entry>();
            foreach (CardTheme theme in System.Enum.GetValues(typeof(CardTheme)))
            {
                var tex = TextureFactory.Save(TextureFactory.Sigil(theme), $"{AssetFactory.TexDir}/Sigils/Sigil_{theme}.png", repeat: false);
                ArtLibrary.entries.Add(new CardArtLibrary.Entry { theme = theme, texture = tex });
            }
            ArtLibrary.fallback = ArtLibrary.entries[0].texture;
            ArtLibrary.cardBack = TexCardBack;
            ArtLibrary.bloodDropIcon = SpriteDrop ? SpriteDrop.texture : null;
            ArtLibrary.lucidityIcon = SpriteFlame ? SpriteFlame.texture : null;
            EditorUtility.SetDirty(ArtLibrary);
        }

        // ------------------------------------------------------------------ Matériaux
        static void BuildMaterials()
        {
            // Cartes : URP/Lit (pas de tremblement de sommets, pour rester lisibles)
            AssetFactory.UsePSX = false;
            CardFront = AssetFactory.LitEmissive("M_CardFront", Color.white, Palette.Hex("1A1818"), TexCardFront, TexCardFront);
            CardBack = AssetFactory.Lit("M_CardBack", Color.white, TexCardBack, 0.3f);
            CardArt = AssetFactory.LitEmissive("M_CardArt", Color.white, Palette.Hex("2A2626"), ArtLibrary.fallback, ArtLibrary.fallback);
            AssetFactory.UsePSX = true;

            Highlight = AssetFactory.Unlit("M_CardHighlight", Palette.BloodLight);
            CostOrb = AssetFactory.Unlit("M_CostOrb", Palette.BloodDark, SpriteOrb ? SpriteOrb.texture : null);
            CostOrb.SetFloat("_AlphaClip", 1f); CostOrb.SetFloat("_Cutoff", 0.5f); CostOrb.EnableKeyword("_ALPHATEST_ON");
            CostOrb.renderQueue = (int)RenderQueue.AlphaTest;

            Wood = AssetFactory.Lit("M_Wood", Color.white, TexWoodV2, 0.25f);
            WoodDark = AssetFactory.Lit("M_WoodDark", Color.white, TexWoodDarkV2, 0.1f);
            Wainscot = AssetFactory.Lit("M_Wainscot", Palette.Hex("8A7A6A"), TexWoodDarkV2, 0.1f);
            Floor = AssetFactory.Lit("M_Floor", Color.white, TexFloorV2, 0.2f);
            Wallpaper = AssetFactory.Lit("M_Wallpaper", Color.white, TexWallpaperV2, 0.05f);
            Ceiling = AssetFactory.Lit("M_Ceiling", Color.white, TexConcrete, 0.05f);
            Baseboard = AssetFactory.Lit("M_Baseboard", Palette.Hex("7A6A5A"), TexWoodDarkV2, 0.2f);
            PlayerSkin = AssetFactory.Lit("M_Skin", Color.white, TexPlayerSkin, 0.3f);
            Skin = PlayerSkin;
            GraySkin = AssetFactory.Lit("M_SkinDepression", Color.white, TexGraySkinV2, 0.25f);
            DepressionFace = AssetFactory.Lit("M_DepressionFace", Color.white, TexFace, 0.25f);
            Hair = AssetFactory.Lit("M_Hair", Color.white, TexHair, 0.35f);
            SailorGray = AssetFactory.Lit("M_SailorGray", Color.white, TexTorso, 0.05f);
            SleeveNavy = AssetFactory.Lit("M_SleeveNavy", Color.white, TexSleeveNavy, 0.05f);
            SleeveGray = AssetFactory.Lit("M_SleeveGray", Color.white, TexSleeveGray, 0.05f);
            Skirt = AssetFactory.Lit("M_Skirt", Color.white, TexSkirt, 0.05f);
            Collar = AssetFactory.Lit("M_Collar", Color.white, TexCollar, 0.05f);
            Scarf = AssetFactory.Lit("M_Scarf", Palette.Blood, TexSkirt, 0.1f);
            UniformGray = AssetFactory.Lit("M_Clothes", Color.white, TexBlanket, 0.05f);
            Eye = AssetFactory.LitEmissive("M_DepressionEye", Palette.Void, Palette.BloodLight * 0.3f);
            EyeBags = AssetFactory.LitCutout("M_EyeBags", TexEyeBags, Color.white, 0.3f);
            Wound = AssetFactory.LitCutout("M_Wound", TexWound, Color.white, 0.35f);
            Crack = AssetFactory.LitCutout("M_Crack", TexCrack, Color.white, 0.5f);
            Blood = AssetFactory.Lit("M_Blood", Color.white, TexBlood, 0.85f);
            BloodPool = AssetFactory.Lit("M_BloodPool", Palette.Hex("8A1C1C"), TexBlood, 0.9f);
            Wax = AssetFactory.Lit("M_Wax", Palette.Hex("C8BEAC"), TexPaperV2, 0.35f);
            Wick = AssetFactory.Lit("M_Wick", Palette.Void, null, 0f);
            Flame = AssetFactory.LitEmissive("M_Flame", Palette.Bulb, Palette.Bulb * 3f);
            LampShade = AssetFactory.DoubleSided(AssetFactory.Lit("M_LampShade", Palette.Hex("3A4A3E"), TexMetal, 0.4f));
            Cord = AssetFactory.Lit("M_Cord", Palette.Void, null, 0.2f);
            Bulb = AssetFactory.LitEmissive("M_Bulb", Palette.Bulb, Palette.Bulb * 3f);
            Glass = AssetFactory.Lit("M_Glass", Palette.Hex("3E4A44"), null, 0.9f);
            Plastic = AssetFactory.Lit("M_Plastic", Palette.Hex("8A5A22"), null, 0.6f);
            Paper = AssetFactory.Lit("M_Paper", Color.white, TexPaperV2, 0.05f);
            Mattress = AssetFactory.Lit("M_Mattress", Color.white, TexMattress, 0.05f);
            Blanket = AssetFactory.Lit("M_Blanket", Color.white, TexBlanket, 0.05f);
            Books = AssetFactory.Lit("M_Books", Color.white, TexBooks, 0.1f);
            Cardboard = AssetFactory.Lit("M_Cardboard", Color.white, TexCardboard, 0.05f);
            Rust = AssetFactory.Lit("M_Rust", Color.white, TexRust, 0.3f);
            Metal = AssetFactory.Lit("M_Metal", Color.white, TexMetal, 0.6f);
            Void = AssetFactory.Lit("M_Void", Palette.Void, null, 0f);
            DoorGap = AssetFactory.Unlit("M_DoorGap", Palette.Bulb * 0.6f);
            WindowGap = AssetFactory.Unlit("M_WindowGap", Palette.Hex("1E2430"));
            ParticleDust = AssetFactory.Particle("M_FX_Dust", TexDot, Color.white, true);
            ParticleSmoke = AssetFactory.Particle("M_FX_Smoke", TexDot, Color.white, false);

            var vhsShader = Shader.Find("Lucidite/VHS");
            string vhsPath = $"{AssetFactory.MatDir}/M_VHS.mat";
            VHSMaterial = AssetDatabase.LoadAssetAtPath<Material>(vhsPath);
            if (!VHSMaterial) { VHSMaterial = new Material(vhsShader); AssetDatabase.CreateAsset(VHSMaterial, vhsPath); }
            else VHSMaterial.shader = vhsShader;
            VHSMaterial.SetFloat("_ColorDepth", 40f);
            EditorUtility.SetDirty(VHSMaterial);
        }

        // ------------------------------------------------------------------ Post-process
        static void BuildVolumeProfile()
        {
            string path = $"{AssetFactory.SettingsDir}/PostFX_Lucidite.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path)) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f); bloom.intensity.Override(0.6f); bloom.scatter.Override(0.6f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.3f); vig.smoothness.Override(0.5f); vig.color.Override(Color.black);
            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.06f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin2); grain.intensity.Override(0.2f); grain.response.Override(0.7f);
            var col = profile.Add<ColorAdjustments>(true);
            col.postExposure.Override(0.3f); col.contrast.Override(12f); col.saturation.Override(-25f); col.colorFilter.Override(Palette.Hex("F2E6DC"));
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Scène
        static GameObject Inst(GameObject prefab, Transform parent, Vector3 pos, Vector3 euler)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent) g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            return g;
        }

        static T LoadPrefab<T>(string dir, string name) where T : Component
        {
            string path = $"{dir}/{name}.prefab";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) throw new System.Exception($"[Lucidité] Prefab introuvable : {path}");
            var c = go.GetComponent<T>();
            if (!c) throw new System.Exception($"[Lucidité] Composant {typeof(T).Name} absent de {path}");
            return c;
        }

        static Transform Point(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            return g.transform;
        }

        static void BuildScene(GameConfig cfg)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Changer de scène décharge les assets fraîchement créés : on les recharge depuis le disque.
            string pd = AssetFactory.PrefabDir, ud = AssetFactory.UIPrefabDir;
            var card = LoadPrefab<CardView>(pd, "Card");
            var candle = LoadPrefab<Candle>(pd, "Candle");
            var table = LoadPrefab<Transform>(pd, "Table").gameObject;
            var lamp = LoadPrefab<HangingLamp>(pd, "HangingLamp");
            var room = LoadPrefab<Transform>(pd, "Room").gameObject;
            var arms = LoadPrefab<PlayerArms>(pd, "PlayerArms");
            var opponent = LoadPrefab<OpponentView>(pd, "Depression");
            var psxOutput = LoadPrefab<Transform>(ud, "PSXOutput").gameObject;
            var gameUI = LoadPrefab<UIManager>(ud, "GameUI");
            cfg = AssetDatabase.LoadAssetAtPath<GameConfig>($"{AssetFactory.DataDir}/GameConfig.asset");
            var db = AssetDatabase.LoadAssetAtPath<CardDatabase>($"{AssetFactory.DataDir}/CardDatabase.asset");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{AssetFactory.SettingsDir}/PostFX_Lucidite.asset");
            var dustMat = AssetDatabase.LoadAssetAtPath<Material>($"{AssetFactory.MatDir}/M_FX_Dust.mat");

            // Éclairage : une seule ampoule, le reste dans la pénombre
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Palette.Hex("221C1A");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.2f;
            RenderSettings.fogColor = Palette.Void;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.15f;

            // Toute la pièce sous une racine qui peut trembler
            var envGo = new GameObject("Environment");
            var shake = envGo.AddComponent<RoomShake>();
            var env = envGo.transform;
            Inst(room, env, Vector3.zero, Vector3.zero);
            Inst(table, env, Vector3.zero, Vector3.zero);
            var lampGo = Inst(lamp.gameObject, env, new Vector3(0, 2.7f, 0.04f), Vector3.zero);
            shake.lamp = lampGo.GetComponent<HangingLamp>();
            var oppGo = Inst(opponent.gameObject, env, new Vector3(0, 0, 0.64f), new Vector3(0, 180, 0)); // assez près pour que ses jambes soient sous la table
            ParticleDust = dustMat;
            var ceilingDust = PrefabFactory.CeilingDust(env);

            // --- Destruction de la pièce : collisions (gravats), fissures cachées, matériaux des débris
            Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{AssetFactory.MatDir}/{n}.mat");
            var physics = new GameObject("Collisions").transform;
            physics.SetParent(env, false);
            void Box(string n, Vector3 c, Vector3 s)
            {
                var g = new GameObject(n);
                g.layer = 2; // Ignore Raycast : ne gêne pas le survol des cartes
                g.transform.SetParent(physics, false);
                g.transform.localPosition = c;
                g.AddComponent<BoxCollider>().size = s;
            }
            Box("FloorCollider", new Vector3(0, -0.05f, 0), new Vector3(4.4f, 0.1f, 4.4f));
            Box("TableCollider", new Vector3(0, 0.755f, 0), new Vector3(1.3f, 0.05f, 0.9f));
            Box("WallN", new Vector3(0, 1.35f, 2.2f), new Vector3(4.4f, 2.7f, 0.1f));
            Box("WallS", new Vector3(0, 1.35f, -2.2f), new Vector3(4.4f, 2.7f, 0.1f));
            Box("WallE", new Vector3(2.2f, 1.35f, 0), new Vector3(0.1f, 2.7f, 4.4f));
            Box("WallW", new Vector3(-2.2f, 1.35f, 0), new Vector3(0.1f, 2.7f, 4.4f));
            shake.debrisMaterials = new[] { Mat("M_Ceiling"), Mat("M_Wallpaper"), Mat("M_WoodDark") };
            var crackMat = Mat("M_Crack");
            var cracksRoot = new GameObject("Cracks").transform;
            cracksRoot.SetParent(env, false);
            var crackSpots = new (Vector3 pos, Vector3 facing, float size)[]
            {
                (new Vector3(0.1f, 2.695f, 0.2f), Vector3.down, 0.9f), (new Vector3(-0.8f, 2.695f, 0.9f), Vector3.down, 0.7f),
                (new Vector3(0.9f, 2.695f, -0.4f), Vector3.down, 0.8f), (new Vector3(-0.3f, 1.9f, 2.144f), Vector3.back, 0.7f),
                (new Vector3(1.6f, 1.6f, 2.144f), Vector3.back, 0.6f), (new Vector3(-2.144f, 2.1f, -0.5f), Vector3.right, 0.8f),
                (new Vector3(2.144f, 1.4f, 0.9f), Vector3.left, 0.7f), (new Vector3(0.6f, 2.695f, 1.4f), Vector3.down, 0.6f),
                (new Vector3(-1.4f, 1.2f, 2.144f), Vector3.back, 0.5f), (new Vector3(-2.144f, 1.0f, 1.2f), Vector3.right, 0.6f),
                (new Vector3(0.0f, 2.695f, -1.0f), Vector3.down, 0.9f), (new Vector3(2.144f, 2.2f, -1.0f), Vector3.left, 0.7f)
            };
            int ci = 0;
            foreach (var (pos, facing, size) in crackSpots)
            {
                var c = Decal($"Crack{ci++}", cracksRoot, pos, facing, Vector2.one * size, crackMat, Random.Range(0f, 360f));
                shake.cracks.Add(c);
            }

            // --- Poussière soulevée quand une carte est plaquée sur la table
            var tableDust = PrefabFactory.CeilingDust(env);
            tableDust.name = "TableDust";
            tableDust.transform.localPosition = new Vector3(0, 0.79f, 0);
            {
                var main = tableDust.main;
                main.startLifetime = 0.9f; main.gravityModifier = -0.02f; main.startSpeed = 0.08f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
                main.startColor = new Color(0.75f, 0.7f, 0.6f, 0.5f);
                var em = tableDust.emission; em.enabled = false;
                var sh = tableDust.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.03f;
            }

            // --- Sang : gouttes et flaques (jusqu'à la manche suivante)
            var bloodGo = new GameObject("BloodSystem");
            bloodGo.transform.SetParent(env, false);
            var blood = bloodGo.AddComponent<BloodSystem>();
            blood.dropMaterial = Mat("M_Blood");
            blood.puddleMaterial = Mat("M_BloodPool");
            blood.puddleRoot = env;

            // Bougies : une par manche (6 emplacements, le nombre actif suit la configuration)
            var candlesRoot = new GameObject("RoundCandles").transform;
            candlesRoot.SetParent(env, false);
            void Row(string name, Side side, Vector3 start, Vector3 step, int active)
            {
                var r = new GameObject(name);
                r.transform.SetParent(candlesRoot, false);
                var lc = r.AddComponent<LucidityCandles>();
                lc.side = side;
                lc.candles = new Candle[6];
                for (int i = 0; i < 6; i++)
                {
                    var c = Inst(candle.gameObject, r.transform, start + step * i + new Vector3(Random.Range(-0.008f, 0.008f), 0, Random.Range(-0.012f, 0.012f)), new Vector3(0, Random.Range(0, 360f), 0));
                    lc.candles[i] = c.GetComponent<Candle>();
                    c.SetActive(i < active);
                }
            }
            Row("PlayerCandles", Side.Player, new Vector3(-0.58f, 0.78f, 0.02f), new Vector3(0.075f, 0, 0.012f), cfg.playerCandles);
            Row("EnemyCandles", Side.Opponent, new Vector3(0.58f, 0.78f, 0.32f), new Vector3(-0.075f, 0, 0.0f), cfg.enemyCandles);

            // Caméra : rig > shake > caméra
            var rigGo = new GameObject("CameraRig");
            var rig = rigGo.AddComponent<CameraRig>();
            var poses = new GameObject("CameraPoses").transform;
            rig.titlePose = Point("TitlePose", poses, new Vector3(-1.15f, 1.6f, -1.3f), Quaternion.Euler(16f, 38f, 0f));
            rig.seatedPose = Point("SeatedPose", poses, new Vector3(0f, 1.25f, -0.56f), Quaternion.Euler(22f, 0f, 0f));
            rig.tablePose = Point("TablePose", poses, new Vector3(0f, 1.26f, -0.44f), Quaternion.Euler(42f, 0f, 0f));
            rigGo.transform.SetPositionAndRotation(rig.titlePose.position, rig.titlePose.rotation);
            var shakeRoot = new GameObject("Shake").transform;
            shakeRoot.SetParent(rigGo.transform, false);
            rig.shakeRoot = shakeRoot;
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(shakeRoot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 80f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 15f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Void;
            cam.allowMSAA = false;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<AudioListener>();
            var armsGo = Inst(arms.gameObject, camGo.transform, Vector3.zero, Vector3.zero);
            armsGo.SetActive(false);

            var screenCamGo = new GameObject("ScreenCamera");
            var screenCam = screenCamGo.AddComponent<Camera>();
            screenCam.cullingMask = 0;
            screenCam.clearFlags = CameraClearFlags.SolidColor;
            screenCam.backgroundColor = Color.black;
            screenCam.depth = 10;
            screenCam.allowMSAA = false;
            screenCamGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

            var psxGo = Inst(psxOutput, null, Vector3.zero, Vector3.zero);
            psxGo.GetComponent<PSXScreen>().worldCamera = cam;
            Inst(gameUI.gameObject, null, Vector3.zero, Vector3.zero);
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();

            var volGo = new GameObject("PostFX");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            // Plateau (dans l'environnement : il tremble avec la pièce)
            var boardGo = new GameObject("Board");
            boardGo.transform.SetParent(env, false);
            var board = boardGo.AddComponent<BoardView>();
            var faceUp = Quaternion.Euler(90f, 0f, 0f);
            var faceDown = faceUp * Quaternion.Euler(0f, 180f, 0f);
            board.cardPrefab = card;
            board.playerDeckPoint = Point("PlayerDeck", boardGo.transform, new Vector3(0.36f, 0.8f, -0.06f), faceDown);
            board.playerDiscardPoint = Point("PlayerDiscard", boardGo.transform, new Vector3(-0.3f, 0.783f, -0.12f), faceUp);
            board.exhaustPoint = Point("Exhaust", boardGo.transform, new Vector3(0f, 0.95f, 0.05f), Quaternion.identity);
            board.playPoint = Point("PlayPoint", boardGo.transform, new Vector3(0f, 0.98f, -0.32f), Quaternion.Euler(16f, 0f, 0f));
            // Carte jouée par la Dépression : posée sur la table devant elle, plus en avant que les tiennes
            board.intentPoint = Point("IntentPoint", boardGo.transform, new Vector3(0f, 0.79f, 0.2f), Quaternion.Euler(78f, 0f, 0f));
            board.playRowPoint = Point("PlayRow", boardGo.transform, new Vector3(0f, 0.7818f, 0.05f), faceUp);
            board.tableDust = tableDust;
            board.intentAttackPoint = Point("IntentAttack", boardGo.transform, new Vector3(0f, 1.02f, -0.2f), Quaternion.Euler(16f, 0f, 0f));
            board.playerHand = armsGo.GetComponentInChildren<HandView>(true);
            board.opponentHand = oppGo.GetComponentInChildren<HandView>(true);
            board.opponentHeldCards = 5; // elle en pose 1 à 3 par tour puis repioche : ses mains suivent l'éventail
            board.cameraRig = rig;
            board.playerArms = armsGo.GetComponent<PlayerArms>();
            board.tableCardScale = 0.065f;
            var picker = boardGo.AddComponent<CardPicker>();
            picker.playerHand = board.playerHand;

            // Systèmes
            var systems = new GameObject("Systems");
            var gm = systems.AddComponent<GameManager>();
            gm.config = cfg;
            gm.database = db;
            gm.board = board;
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(systems.transform, false);
            var audio = audioGo.AddComponent<AudioManager>();
            var bulb = lampGo.transform.Find("Bulb");
            if (bulb) audio.lampHumSource = bulb.GetComponent<AudioSource>();
            var fb = systems.AddComponent<FeedbackDirector>();
            fb.cameraRig = rig;
            fb.lamp = lampGo.GetComponent<HangingLamp>();
            fb.roomShake = shake;
            fb.ceilingDust = ceilingDust;
            fb.postVolume = vol;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
