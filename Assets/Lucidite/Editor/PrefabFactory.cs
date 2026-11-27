using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static Lucidite.EditorTools.Lib;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Prefabs 3D. Les modèles sont modélisés en code (tours, tubes, boîtes à UV au mètre) dans un style
    /// PS1 "réaliste". Chaque modèle peut être remplacé par un fichier déposé dans Assets/Lucidite/Imported.
    /// </summary>
    public static class PrefabFactory
    {
        static string P(string name) => $"{AssetFactory.PrefabDir}/{name}.prefab";
        static Vector2 V2(float r, float y) => new Vector2(r, y);

        // ================================================================== CARTE
        public static CardView BuildCard()
        {
            MeshStore.Begin("Card");
            var root = new GameObject("Card");
            var view = root.AddComponent<CardView>();
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1.5f, 0.05f);

            // Face : texture propre à chaque carte (fond, contours, grande icône, bonus), coins arrondis découpés.
            // Repères : CardFaceFactory (256 px = 1 unité). Seuls le coût, la valeur et les textes sont dynamiques.
            var front = Prim(PrimitiveType.Quad, "Front", root.transform, Vector3.zero, new Vector3(1f, 1.5f, 1f), CardFront);
            var back = Prim(PrimitiveType.Quad, "Back", root.transform, new Vector3(0, 0, 0.004f), new Vector3(1f, 1.5f, 1f), CardBack, new Vector3(0, 180, 0));
            var hl = Prim(PrimitiveType.Quad, "Highlight", root.transform, new Vector3(0, 0, 0.012f), new Vector3(1.15f, 1.65f, 1f), Highlight);

            float z = -0.006f;
            // Coût (haut gauche) et valeur (bas droite) : ronds posés sur les coins de la bordure intérieure
            var orb = Prim(PrimitiveType.Quad, "CostOrb", root.transform, new Vector3(-0.37f, 0.62f, -0.004f), new Vector3(0.2f, 0.2f, 1f), CostOrb);
            view.costOrb = orb;
            view.costOrbRenderer = orb.GetComponent<MeshRenderer>();
            view.costText = Text3D(root.transform, "Cost", new Vector3(-0.37f, 0.625f, z), new Vector2(0.15f, 0.15f), "1", Palette.Bone, 1.8f, FontStyles.Bold, FontTitle);
            var valueOrb = Prim(PrimitiveType.Quad, "ValueOrb", root.transform, new Vector3(0.37f, -0.62f, -0.004f), new Vector3(0.2f, 0.2f, 1f), CostOrb);
            view.valueOrb = valueOrb;
            view.valueOrbRenderer = valueOrb.GetComponent<MeshRenderer>();
            view.valueText = Text3D(root.transform, "Value", new Vector3(0.37f, -0.615f, z), new Vector2(0.17f, 0.14f), "6", Palette.Bone, 1.6f, FontStyles.Bold, FontTitle);
            view.valueText.textWrappingMode = TextWrappingModes.NoWrap;
            NoShadows(root);
            front.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Textes agrandis : nom en haut, règles sous l'icône (au-dessus de la rangée des bonus)
            view.nameText = Text3D(root.transform, "Name", new Vector3(0.07f, 0.615f, z), new Vector2(0.68f, 0.14f), "NOM", Palette.Bone, 1.6f, FontStyles.Bold, FontTitle);
            view.nameText.fontSizeMin = 0.6f;
            view.rulesText = Text3D(root.transform, "Rules", new Vector3(0, -0.28f, z), new Vector2(0.82f, 0.44f), "", Palette.Bone, 1.05f, FontStyles.Normal, FontBody);
            view.rulesText.fontSizeMin = 0.45f;
            view.rulesText.lineSpacing = -10f;

            view.artLibrary = ArtLibrary;
            view.frameRenderer = front.GetComponent<MeshRenderer>();
            view.backRenderer = back.GetComponent<MeshRenderer>();
            view.highlight = hl;
            view.highlightRenderer = hl.GetComponent<MeshRenderer>();
            hl.SetActive(false);
            return SavePrefab(root, P("Card")).GetComponent<CardView>();
        }

        // ================================================================== BOUGIE
        public static Candle BuildCandle()
        {
            MeshStore.Begin("Candle");
            var root = new GameObject("Candle");
            var candle = root.AddComponent<Candle>();

            var dish = new MeshBuilder().Lathe(new[] { V2(0f, 0f), V2(0.042f, 0f), V2(0.048f, 0.006f), V2(0.044f, 0.011f), V2(0.018f, 0.012f), V2(0.017f, 0.022f), V2(0.0f, 0.022f) }, 12);
            Part("Holder", root.transform, dish, Metal);

            float h = Random.Range(0.06f, 0.1f);
            var wax = new MeshBuilder().Lathe(new[] { V2(0.014f, 0.02f), V2(0.0145f, 0.02f + h * 0.5f), V2(0.0142f, 0.02f + h), V2(0.011f, 0.024f + h), V2(0.007f, 0.019f + h), V2(0.0f, 0.018f + h) }, 10);
            var drips = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float y = 0.02f + h * Random.Range(0.45f, 0.95f);
                drips.Ellipsoid(new Vector3(Mathf.Sin(a) * 0.0145f, y, Mathf.Cos(a) * 0.0145f), new Vector3(0.0035f, Random.Range(0.008f, 0.018f), 0.0035f), 4, 6);
            }
            drips.Ellipsoid(new Vector3(0, 0.024f, 0), new Vector3(0.02f, 0.004f, 0.02f), 3, 10);
            Part("Wax", root.transform, wax, Wax);
            Part("Drips", root.transform, drips, Wax, false);
            Part("Wick", root.transform, new MeshBuilder().Box(new Vector3(0, 0.022f + h, 0), new Vector3(0.0015f, 0.01f, 0.0015f)), Wick, false);

            var flame = Empty("Flame", root.transform, new Vector3(0, 0.026f + h, 0));
            Part("FlameMesh", flame.transform, new MeshBuilder().Lathe(new[] { V2(0f, 0f), V2(0.004f, 0.004f), V2(0.0055f, 0.01f), V2(0.003f, 0.018f), V2(0f, 0.026f) }, 8), Flame, false);
            var lightGo = Empty("Light", flame.transform, new Vector3(0, 0.012f, 0));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Palette.Bulb;
            // Atténuation en 1/d² : à quelques cm de la table, une intensité de 0.35 inondait le plateau
            // (les 6 bougies s'allument au lancement de la partie). Halo local seulement.
            light.range = 0.3f;
            light.intensity = 0.06f;
            light.shadows = LightShadows.None;

            var smokeGo = Empty("Smoke", root.transform, new Vector3(0, 0.03f + h, 0));
            var ps = smokeGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 1f;
            main.startLifetime = 2.2f; main.startSpeed = 0.05f; main.startSize = 0.03f;
            main.startColor = new Color(0.55f, 0.52f, 0.5f, 0.5f);
            main.gravityModifier = -0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.004f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.5f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.6f, 0), new GradientAlphaKey(0f, 1) });
            col.color = grad;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.03f;
            smokeGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleSmoke;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            candle.flame = flame;
            candle.flameLight = light;
            candle.smoke = ps;
            candle.lightIntensity = 0.06f;
            return SavePrefab(root, P("Candle")).GetComponent<Candle>();
        }

        // ================================================================== SANG
        public static BloodDrip BuildBloodDrip()
        {
            var root = new GameObject("BloodDrip");
            var d = root.AddComponent<BloodDrip>();
            var stream = Prim(PrimitiveType.Cube, "Stream", root.transform, Vector3.zero, new Vector3(0.0025f, 0.01f, 0.0025f), Blood);
            var pool = Prim(PrimitiveType.Cylinder, "Pool", root.transform, Vector3.zero, new Vector3(0.01f, 0.0008f, 0.01f), BloodPool);
            NoShadows(root);
            d.stream = stream.transform;
            d.pool = pool.transform;
            return SavePrefab(root, P("BloodDrip")).GetComponent<BloodDrip>();
        }

        // ================================================================== TABLE
        public static GameObject BuildTable()
        {
            MeshStore.Begin("Table");
            var root = new GameObject("Table");
            if (!TryImported("Table", root.transform) && !ThirdParty.TryTable(root.transform))
            {
                var top = new MeshBuilder();
                int planks = 6;
                float depth = 0.9f, width = 1.3f, pw = depth / planks;
                for (int i = 0; i < planks; i++)
                {
                    float z = -depth / 2f + pw * (i + 0.5f);
                    top.At(new Vector3(0, 0.76f + Random.Range(-0.0015f, 0.0015f), z), new Vector3(Random.Range(-0.3f, 0.3f), 0, 0))
                       .Box(Vector3.zero, new Vector3(width + Random.Range(-0.01f, 0.01f), 0.04f, pw - 0.005f), 1f, new Vector2(Random.value, i * 0.37f));
                }
                top.Reset().Box(new Vector3(0, 0.735f, 0), new Vector3(width - 0.04f, 0.012f, depth - 0.04f));
                Part("Top", root.transform, top, Wood);

                var frame = new MeshBuilder();
                frame.Box(new Vector3(0, 0.68f, 0.4f), new Vector3(1.14f, 0.09f, 0.022f), 2f);
                frame.Box(new Vector3(0, 0.68f, -0.4f), new Vector3(1.14f, 0.09f, 0.022f), 2f);
                frame.Box(new Vector3(0.58f, 0.68f, 0), new Vector3(0.022f, 0.09f, 0.78f), 2f);
                frame.Box(new Vector3(-0.58f, 0.68f, 0), new Vector3(0.022f, 0.09f, 0.78f), 2f);
                // Tiroir côté joueuse
                frame.Box(new Vector3(0.25f, 0.68f, -0.412f), new Vector3(0.34f, 0.065f, 0.008f), 2f);
                Part("Apron", root.transform, frame, WoodDark);
                var knob = new MeshBuilder().At(new Vector3(0.25f, 0.68f, -0.418f), new Vector3(-90, 0, 0)).Lathe(new[] { V2(0.006f, 0f), V2(0.01f, 0.006f), V2(0.008f, 0.014f), V2(0f, 0.015f) }, 8);
                Part("Knob", root.transform, knob, Metal, false);

                var legs = new MeshBuilder();
                var legProfile = new[] { V2(0.024f, 0f), V2(0.02f, 0.02f), V2(0.019f, 0.2f), V2(0.028f, 0.26f), V2(0.016f, 0.31f), V2(0.026f, 0.36f), V2(0.021f, 0.5f), V2(0.03f, 0.56f), V2(0.03f, 0.735f), V2(0f, 0.735f) };
                foreach (var sx in new[] { -1, 1 })
                    foreach (var sz in new[] { -1, 1 })
                        legs.At(new Vector3(sx * 0.57f, 0, sz * 0.39f)).Lathe(legProfile, 8, 1f, 2f, true);
                Part("Legs", root.transform, legs, WoodDark);
            }

            // Objets abandonnés sur la table
            var props = Empty("Props", root.transform, Vector3.zero);
            Part("PillBottle", props.transform, new MeshBuilder().At(new Vector3(0.52f, 0.78f, 0.36f)).Lathe(new[] { V2(0f, 0f), V2(0.018f, 0f), V2(0.019f, 0.002f), V2(0.019f, 0.055f), V2(0.016f, 0.06f), V2(0.016f, 0.066f), V2(0f, 0.066f) }, 10), Plastic);
            Part("PillCap", props.transform, new MeshBuilder().At(new Vector3(0.52f, 0.846f, 0.36f)).Lathe(new[] { V2(0f, 0f), V2(0.02f, 0f), V2(0.02f, 0.012f), V2(0f, 0.012f) }, 10), Paper);
            var pills = new MeshBuilder();
            for (int i = 0; i < 4; i++) pills.Ellipsoid(new Vector3(0.46f + Random.Range(-0.03f, 0.03f), 0.783f, 0.31f + Random.Range(-0.03f, 0.03f)), new Vector3(0.007f, 0.0035f, 0.004f), 3, 6);
            Part("Pills", props.transform, pills, Paper, false);
            var mug = new MeshBuilder().At(new Vector3(0.56f, 0.78f, 0.06f)).Lathe(new[] { V2(0f, 0f), V2(0.035f, 0f), V2(0.038f, 0.09f), V2(0.034f, 0.09f), V2(0.031f, 0.01f), V2(0f, 0.01f) }, 12, 2f);
            mug.At(new Vector3(0.6f, 0.825f, 0.06f), new Vector3(0, 0, 90)).Tube(new[] { new Vector3(0, -0.025f, 0), new Vector3(0.018f, -0.015f, 0), new Vector3(0.018f, 0.015f, 0), new Vector3(0, 0.025f, 0) }, new[] { V2(0.005f, 0.005f), V2(0.005f, 0.005f), V2(0.005f, 0.005f), V2(0.005f, 0.005f) }, 6, Vector3.forward);
            Part("Mug", props.transform, mug, Glass);
            var paperBall = new MeshBuilder().Ellipsoid(new Vector3(-0.5f, 0.8f, 0.16f), new Vector3(0.03f, 0.024f, 0.028f), 6, 8,
                d => Vector3.one * (0.75f + Mathf.PerlinNoise(d.x * 5f + 3f, d.y * 5f + d.z * 3f) * 0.5f));
            Part("CrumpledPaper", props.transform, paperBall, Paper);
            var sheets = new MeshBuilder();
            sheets.At(new Vector3(-0.2f, 0.7806f, 0.33f), new Vector3(0, 23, 0)).Grid(Vector3.zero, new Vector2(0.21f, 0.297f), 4, 5, (u, w) => Mathf.Sin(u * 3f) * 0.002f, 4f);
            sheets.At(new Vector3(-0.17f, 0.7812f, 0.3f), new Vector3(0, -8, 0)).Grid(Vector3.zero, new Vector2(0.21f, 0.297f), 4, 5, (u, w) => w * 0.004f, 4f);
            Part("Sheets", props.transform, sheets, Paper, false);
            Part("OldStain", props.transform, new MeshBuilder().Ellipsoid(new Vector3(0.15f, 0.7802f, -0.24f), new Vector3(0.05f, 0.0005f, 0.038f), 2, 12, d => Vector3.one * (0.8f + Mathf.PerlinNoise(d.x * 3f + 5f, d.z * 3f) * 0.4f)), BloodPool, false);

            // (pioche et défausse : PileView, créées avec le plateau dans LuciditeSetup)
            return SavePrefab(root, P("Table"));
        }

        // ================================================================== CHAISE
        static void BuildChair(Transform parent, Vector3 pos, float yRot, string meshPrefix)
        {
            var c = Empty("Chair", parent, pos, new Vector3(0, yRot, 0));
            if (TryImported("Chair", c.transform) || ThirdParty.TryChair(c.transform)) return;
            var b = new MeshBuilder();
            for (int i = 0; i < 4; i++) b.Box(new Vector3(0, 0.45f, -0.16f + i * 0.106f), new Vector3(0.42f, 0.03f, 0.1f), 2f, new Vector2(i * 0.3f, 0));
            var legProfile = new[] { V2(0.016f, 0f), V2(0.014f, 0.2f), V2(0.02f, 0.25f), V2(0.016f, 0.44f), V2(0f, 0.44f) };
            foreach (var sx in new[] { -1, 1 })
            {
                b.At(new Vector3(sx * 0.18f, 0, 0.18f)).Lathe(legProfile, 6, 1, 2, true);
                b.At(new Vector3(sx * 0.18f, 0, -0.18f)).Lathe(new[] { V2(0.016f, 0f), V2(0.014f, 0.2f), V2(0.02f, 0.25f), V2(0.016f, 0.98f), V2(0.012f, 1.0f), V2(0f, 1.0f) }, 6, 1, 2, true);
            }
            b.Reset();
            b.Box(new Vector3(0, 0.22f, 0.18f), new Vector3(0.34f, 0.025f, 0.02f), 2f);
            b.Box(new Vector3(0, 0.22f, -0.18f), new Vector3(0.34f, 0.025f, 0.02f), 2f);
            for (int i = 0; i < 3; i++) b.Box(new Vector3(0, 0.62f + i * 0.13f, -0.18f), new Vector3(0.34f, 0.06f, 0.018f), 2f);
            Part(meshPrefix + "Chair", c.transform, b, WoodDark);
        }

        // ================================================================== LAMPE
        public static HangingLamp BuildLamp()
        {
            MeshStore.Begin("Lamp");
            var root = new GameObject("HangingLamp");
            var lamp = root.AddComponent<HangingLamp>();
            float drop = 1.32f; // un peu plus basse qu'avant (1.2) pour mieux la voir
            if (!TryImported("Lamp", root.transform))
            {
                Part("CeilingRose", root.transform, new MeshBuilder().Lathe(new[] { V2(0.06f, 0f), V2(0.06f, -0.012f), V2(0.03f, -0.025f), V2(0.008f, -0.03f), V2(0f, -0.03f) }, 12), Metal);
                var cord = new List<Vector3>();
                var radii = new List<Vector2>();
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    cord.Add(new Vector3(Mathf.Sin(t * 3f) * 0.004f, -0.03f - t * (drop - 0.05f), Mathf.Cos(t * 5f) * 0.003f));
                    radii.Add(V2(0.0035f, 0.0035f));
                }
                Part("Cord", root.transform, new MeshBuilder().Tube(cord, radii, 5, Vector3.forward), Cord, false);
                Part("Socket", root.transform, new MeshBuilder().Lathe(new[] { V2(0f, -drop + 0.03f), V2(0.012f, -drop + 0.03f), V2(0.02f, -drop + 0.01f), V2(0.021f, -drop - 0.035f), V2(0.017f, -drop - 0.045f), V2(0f, -drop - 0.045f) }, 10), Metal);
                Part("Shade", root.transform, new MeshBuilder().Lathe(new[] { V2(0.024f, -drop), V2(0.05f, -drop - 0.012f), V2(0.11f, -drop - 0.05f), V2(0.18f, -drop - 0.1f), V2(0.205f, -drop - 0.118f), V2(0.21f, -drop - 0.124f) }, 16, 1f, 3f), LampShade);
            }
            var bulb = new GameObject("Bulb");
            bulb.transform.SetParent(root.transform, false);
            bulb.AddComponent<MeshFilter>().sharedMesh = MeshStore.Save(new MeshBuilder().Lathe(new[] { V2(0.015f, -drop - 0.045f), V2(0.017f, -drop - 0.06f), V2(0.031f, -drop - 0.08f), V2(0.034f, -drop - 0.1f), V2(0.026f, -drop - 0.122f), V2(0f, -drop - 0.13f) }, 12), "Bulb");
            var br = bulb.AddComponent<MeshRenderer>();
            br.sharedMaterial = Bulb;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var spotGo = Empty("SpotLight", root.transform, new Vector3(0, -drop - 0.07f, 0), new Vector3(90, 0, 0));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 120f;
            spot.innerSpotAngle = 70f;
            spot.range = 4.5f;
            spot.intensity = 4f;
            spot.color = Palette.Bulb;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.9f;
            spot.shadowNearPlane = 0.05f;

            var fillGo = Empty("FillLight", root.transform, new Vector3(0, -drop - 0.16f, 0));
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 3.6f;
            fill.intensity = 0.5f;
            fill.color = Palette.Bulb;
            fill.shadows = LightShadows.None;

            var hum = bulb.AddComponent<AudioSource>();
            hum.playOnAwake = false;
            hum.spatialBlend = 1f;

            lamp.spotLight = spot;
            lamp.fillLight = fill;
            lamp.bulbRenderer = br;
            lamp.cordLength = drop + 0.1f;
            lamp.idleSway = 1f;        // calme hors secousses (2.6° bougeait trop)
            lamp.damping = 0.38f;      // avant 0.5 : se balance plus longtemps après un choc, sans osciller indéfiniment
            lamp.impulseScale = 1.6f;
            // Plus sobre : la table claire, vue en grand angle, saturait sous la lampe.
            // Lampe descendue de 12 cm (0.65 -> 0.53 m de la table) : intensités x (0.53/0.65)² ≈ 0.67 pour garder le même éclairage
            lamp.spotIntensity = 4f;
            lamp.fillIntensity = 0.5f;
            return SavePrefab(root, P("HangingLamp")).GetComponent<HangingLamp>();
        }

        // ================================================================== PIÈCE
        public static GameObject BuildRoom()
        {
            MeshStore.Begin("Room");
            var root = new GameObject("Room");
            if (TryImported("Room", root.transform)) return SavePrefab(root, P("Room"));
            float half = 2.2f, height = 2.7f, wain = 0.95f;

            Part("Floor", root.transform, new MeshBuilder().Box(new Vector3(0, -0.05f, 0), new Vector3(half * 2, 0.1f, half * 2), 0.5f), Floor);
            Part("Ceiling", root.transform, new MeshBuilder().Box(new Vector3(0, height + 0.05f, 0), new Vector3(half * 2, 0.1f, half * 2), 0.6f), Ceiling);

            var walls = new MeshBuilder();
            walls.Box(new Vector3(0, (height + wain) / 2, half), new Vector3(half * 2, height - wain, 0.1f), 0.8f);
            walls.Box(new Vector3(0, (height + wain) / 2, -half), new Vector3(half * 2, height - wain, 0.1f), 0.8f);
            walls.Box(new Vector3(half, (height + wain) / 2, 0), new Vector3(0.1f, height - wain, half * 2), 0.8f);
            walls.Box(new Vector3(-half, (height + wain) / 2, 0), new Vector3(0.1f, height - wain, half * 2), 0.8f);
            Part("Walls", root.transform, walls, Wallpaper);

            // Lambris bas, cimaise, plinthes, corniche
            var wood = new MeshBuilder();
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0, wain / 2, half - 0.005f), new Vector3(half * 2, wain, 0.1f)),
                (new Vector3(0, wain / 2, -half + 0.005f), new Vector3(half * 2, wain, 0.1f)),
                (new Vector3(half - 0.005f, wain / 2, 0), new Vector3(0.1f, wain, half * 2)),
                (new Vector3(-half + 0.005f, wain / 2, 0), new Vector3(0.1f, wain, half * 2))
            }) wood.Box(pos, size, 1.2f);
            Part("Wainscot", root.transform, wood, Wainscot);
            var trims = new MeshBuilder();
            float inner = half - 0.05f;
            foreach (var y in new[] { wain, 0.06f, height - 0.05f })
            {
                float t = y < 0.1f ? 0.12f : 0.05f, d = y < 0.1f ? 0.025f : 0.035f;
                trims.Box(new Vector3(0, y, inner - d / 2), new Vector3(half * 2, t, d), 2f);
                trims.Box(new Vector3(0, y, -inner + d / 2), new Vector3(half * 2, t, d), 2f);
                trims.Box(new Vector3(inner - d / 2, y, 0), new Vector3(d, t, half * 2), 2f);
                trims.Box(new Vector3(-inner + d / 2, y, 0), new Vector3(d, t, half * 2), 2f);
            }
            // Panneaux de lambris (moulures)
            for (int i = 0; i < 8; i++)
            {
                float x = -half + 0.3f + i * 0.52f;
                trims.Box(new Vector3(x, wain * 0.5f, inner - 0.004f), new Vector3(0.4f, 0.6f, 0.01f), 2f);
                trims.Box(new Vector3(x, wain * 0.5f, -inner + 0.004f), new Vector3(0.4f, 0.6f, 0.01f), 2f);
                trims.Box(new Vector3(inner - 0.004f, wain * 0.5f, x), new Vector3(0.01f, 0.6f, 0.4f), 2f);
                trims.Box(new Vector3(-inner + 0.004f, wain * 0.5f, x), new Vector3(0.01f, 0.6f, 0.4f), 2f);
            }
            Part("Trims", root.transform, trims, Baseboard);

            // Porte entrouverte derrière la Dépression, filet de lumière dessous
            var door = Empty("Door", root.transform, new Vector3(0.95f, 0, inner));
            var df = new MeshBuilder();
            df.Box(new Vector3(-0.48f, 1.06f, 0), new Vector3(0.07f, 2.12f, 0.09f), 2f);
            df.Box(new Vector3(0.48f, 1.06f, 0), new Vector3(0.07f, 2.12f, 0.09f), 2f);
            df.Box(new Vector3(0, 2.15f, 0), new Vector3(1.03f, 0.08f, 0.09f), 2f);
            Part("DoorFrame", door.transform, df, WoodDark);
            var slab = new MeshBuilder();
            slab.Box(new Vector3(0, 1.03f, -0.02f), new Vector3(0.88f, 2.04f, 0.04f), 1.5f);
            foreach (var y in new[] { 0.5f, 1.45f })
                foreach (var x in new[] { -0.2f, 0.2f })
                    slab.Box(new Vector3(x, y, -0.043f), new Vector3(0.3f, y < 1f ? 0.65f : 0.85f, 0.01f), 1.5f);
            Part("DoorSlab", door.transform, slab, WoodDark);
            Part("Knob", door.transform, new MeshBuilder().At(new Vector3(-0.36f, 1.0f, -0.045f), new Vector3(-90, 0, 0)).Lathe(new[] { V2(0.02f, 0f), V2(0.01f, 0.01f), V2(0.025f, 0.035f), V2(0.02f, 0.05f), V2(0f, 0.052f) }, 10), Metal, false);
            Prim(PrimitiveType.Cube, "LightGap", door.transform, new Vector3(0, 0.005f, -0.03f), new Vector3(0.86f, 0.01f, 0.01f), DoorGap);

            // Fenêtre condamnée (mur ouest)
            var win = Empty("BoardedWindow", root.transform, new Vector3(-inner, 1.55f, 0.4f), new Vector3(0, 90, 0));
            var wf = new MeshBuilder();
            wf.Box(new Vector3(0, 0.42f, 0), new Vector3(1.0f, 0.06f, 0.08f), 2f);
            wf.Box(new Vector3(0, -0.42f, 0), new Vector3(1.0f, 0.06f, 0.08f), 2f);
            wf.Box(new Vector3(0.47f, 0, 0), new Vector3(0.06f, 0.9f, 0.08f), 2f);
            wf.Box(new Vector3(-0.47f, 0, 0), new Vector3(0.06f, 0.9f, 0.08f), 2f);
            wf.Box(new Vector3(0, -0.47f, -0.05f), new Vector3(1.1f, 0.03f, 0.12f), 2f);
            Part("WindowFrame", win.transform, wf, WoodDark);
            Prim(PrimitiveType.Cube, "Glow", win.transform, new Vector3(0, 0, 0.02f), new Vector3(0.9f, 0.8f, 0.01f), WindowGap);
            var planks = new MeshBuilder();
            for (int i = 0; i < 4; i++)
                planks.At(new Vector3(0, -0.3f + i * 0.2f, -0.05f), new Vector3(0, 0, Random.Range(-9f, 9f))).Box(Vector3.zero, new Vector3(1.15f, 0.14f, 0.025f), 1.5f, new Vector2(i * 0.31f, 0));
            Part("Planks", win.transform, planks, Wood);

            // Lit défait au sol (coin nord-est)
            var bed = Empty("Bed", root.transform, new Vector3(1.45f, 0, 1.15f), new Vector3(0, 4, 0));
            var bedFrame = new MeshBuilder();
            bedFrame.Box(new Vector3(0, 0.1f, 0), new Vector3(1.0f, 0.12f, 2.0f), 2f);
            foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 }) bedFrame.Box(new Vector3(sx * 0.46f, 0.03f, sz * 0.95f), new Vector3(0.06f, 0.06f, 0.06f), 2f);
            bedFrame.Box(new Vector3(0, 0.45f, 0.99f), new Vector3(1.0f, 0.6f, 0.05f), 2f);
            Part("BedFrame", bed.transform, bedFrame, WoodDark);
            Part("Mattress", bed.transform, new MeshBuilder().Box(new Vector3(0, 0.235f, 0), new Vector3(0.94f, 0.15f, 1.94f), 1.5f), Mattress);
            Part("Blanket", bed.transform, new MeshBuilder().Grid(new Vector3(0, 0.31f, -0.25f), new Vector2(1.06f, 1.3f), 10, 12,
                (u, w) => Mathf.PerlinNoise(u * 4f, w * 4f) * 0.08f + Mathf.Sin(u * 9f + w * 3f) * 0.015f - Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 3f) * 0.12f, 1.5f), Blanket);
            Part("Pillow", bed.transform, new MeshBuilder().Ellipsoid(new Vector3(0.05f, 0.35f, 0.72f), new Vector3(0.3f, 0.06f, 0.16f), 5, 10,
                d => new Vector3(1f, 1f - Mathf.Abs(d.x) * 0.4f, 1f)), Paper);

            // Étagère avec livres et flacons
            var shelf = Empty("Shelf", root.transform, new Vector3(inner - 0.14f, 1.35f, -0.5f));
            var sb = new MeshBuilder();
            sb.Box(Vector3.zero, new Vector3(0.24f, 0.025f, 1.0f), 2f);
            sb.Box(new Vector3(0, -0.07f, -0.4f), new Vector3(0.18f, 0.12f, 0.02f), 2f);
            sb.Box(new Vector3(0, -0.07f, 0.4f), new Vector3(0.18f, 0.12f, 0.02f), 2f);
            Part("Board", shelf.transform, sb, WoodDark);
            var books = new MeshBuilder();
            float bz = -0.45f;
            for (int i = 0; i < 11 && bz < 0.1f; i++)
            {
                float th = Random.Range(0.025f, 0.05f), hgt = Random.Range(0.16f, 0.24f);
                float tilt = i == 9 ? 25f : 0f;
                books.At(new Vector3(0.0f, 0.0125f + hgt / 2f, bz + th / 2f), new Vector3(tilt, 0, 0)).Box(Vector3.zero, new Vector3(0.16f, hgt, th), 3f, new Vector2(Random.value, Random.value));
                bz += th + 0.002f;
            }
            Part("Books", shelf.transform, books, Books);
            var bottles = new MeshBuilder();
            for (int i = 0; i < 3; i++)
                bottles.At(new Vector3(Random.Range(-0.04f, 0.04f), 0.0125f, 0.2f + i * 0.09f)).Lathe(new[] { V2(0f, 0f), V2(0.03f, 0f), V2(0.031f, 0.12f), V2(0.012f, 0.17f), V2(0.01f, 0.22f), V2(0f, 0.22f) }, 8);
            Part("Bottles", shelf.transform, bottles, Glass);

            // Cartons, vêtements, radiateur, horloge, affiches, papiers
            var boxes = new MeshBuilder();
            boxes.At(new Vector3(-1.75f, 0.2f, 1.7f), new Vector3(0, 12, 0)).Box(Vector3.zero, new Vector3(0.5f, 0.4f, 0.4f), 1.5f);
            boxes.At(new Vector3(-1.7f, 0.55f, 1.68f), new Vector3(0, -6, 0)).Box(Vector3.zero, new Vector3(0.38f, 0.3f, 0.34f), 1.5f);
            boxes.At(new Vector3(1.8f, 0.17f, -1.6f), new Vector3(0, 30, 0)).Box(Vector3.zero, new Vector3(0.45f, 0.34f, 0.36f), 1.5f);
            Part("Boxes", root.transform, boxes, Cardboard);
            var clothes = new MeshBuilder();
            for (int i = 0; i < 5; i++)
                clothes.Ellipsoid(new Vector3(0.9f + Random.Range(-0.2f, 0.2f), 0.04f, 1.7f + Random.Range(-0.15f, 0.15f)), new Vector3(0.22f, 0.06f, 0.16f), 4, 8,
                    d => Vector3.one * (0.7f + Mathf.PerlinNoise(d.x * 4f + i, d.z * 4f) * 0.6f));
            Part("Clothes", root.transform, clothes, UniformGray);
            var rad = new MeshBuilder();
            for (int i = 0; i < 12; i++) rad.Box(new Vector3(-0.55f + i * 0.1f, 0.42f, 0), new Vector3(0.06f, 0.6f, 0.09f), 2f);
            rad.Box(new Vector3(0, 0.12f, 0), new Vector3(1.2f, 0.04f, 0.04f), 2f);
            rad.Box(new Vector3(0, 0.72f, 0), new Vector3(1.2f, 0.04f, 0.04f), 2f);
            var radiator = Part("Radiator", root.transform, rad, Rust);
            radiator.transform.localPosition = new Vector3(-inner + 0.12f, 0, -1.2f);
            radiator.transform.localEulerAngles = new Vector3(0, 90, 0);

            var clock = Empty("Clock", root.transform, new Vector3(-0.7f, 2.0f, inner - 0.01f), new Vector3(-90, 0, 0));
            Part("ClockBody", clock.transform, new MeshBuilder().Lathe(new[] { V2(0f, 0f), V2(0.16f, 0f), V2(0.17f, 0.03f), V2(0.15f, 0.045f), V2(0f, 0.045f) }, 16), Paper);
            var hands = new MeshBuilder();
            hands.At(new Vector3(0, 0.05f, 0), new Vector3(0, 37, 0)).Box(new Vector3(0, 0, 0.05f), new Vector3(0.008f, 0.004f, 0.1f));
            hands.At(new Vector3(0, 0.052f, 0), new Vector3(0, 160, 0)).Box(new Vector3(0, 0, 0.035f), new Vector3(0.01f, 0.004f, 0.07f));
            Part("ClockHands", clock.transform, hands, Void, false);

            var deco = new MeshBuilder();
            deco.Plane(new Vector3(-1.25f, 1.7f, inner - 0.006f), Vector3.back, new Vector3(0.05f, 1, 0), new Vector2(0.42f, 0.6f));
            deco.Plane(new Vector3(inner - 0.006f, 1.75f, 0.7f), Vector3.left, new Vector3(0, 1, -0.08f), new Vector2(0.4f, 0.55f));
            Part("Posters", root.transform, deco, Paper, false);
            var floorPapers = new MeshBuilder();
            for (int i = 0; i < 20; i++)
            {
                var pos = new Vector3(Random.Range(-1.9f, 1.9f), 0.003f, Random.Range(-1.9f, 1.9f));
                if (Mathf.Abs(pos.x) < 0.8f && Mathf.Abs(pos.z) < 0.6f) continue;
                floorPapers.At(pos, new Vector3(0, Random.Range(0, 360f), 0)).Grid(Vector3.zero, new Vector2(0.21f, 0.297f), 2, 3, (u, w) => (u * w) * 0.01f, 4f);
            }
            Part("FloorPapers", root.transform, floorPapers, Paper, false);
            var floorBottles = new MeshBuilder();
            for (int i = 0; i < 6; i++)
            {
                var pos = new Vector3(Random.Range(-1.8f, 1.8f), 0.035f, Random.Range(0.7f, 1.9f));
                floorBottles.At(pos, new Vector3(90, Random.Range(0, 360f), 0)).Lathe(new[] { V2(0f, -0.12f), V2(0.033f, -0.12f), V2(0.034f, 0.04f), V2(0.012f, 0.09f), V2(0.01f, 0.13f), V2(0f, 0.13f) }, 8);
            }
            Part("FloorBottles", root.transform, floorBottles, Glass);
            var scrawl = new MeshBuilder().Plane(new Vector3(-inner + 0.006f, 1.25f, -0.55f), Vector3.right, Vector3.up, new Vector2(0.7f, 0.45f));
            Part("Scrawl", root.transform, scrawl, Crack, false);

            // Chaise de la joueuse (hors champ, projette une ombre)
            BuildChair(root.transform, new Vector3(0, 0, -0.68f), 0f, "Player");
            ThirdParty.AddDecor(root.transform);

            // Poussière en suspension sous la lampe
            var dust = Empty("DustMotes", root.transform, new Vector3(0, 1.2f, 0));
            var ps = dust.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = 9f; main.startSpeed = 0.01f; main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.008f);
            main.startColor = new Color(0.9f, 0.85f, 0.7f, 0.3f); main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 12f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.4f, 1.2f, 1.2f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.02f; noise.frequency = 0.3f;
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleDust;
            return SavePrefab(root, P("Room"));
        }

        /// <summary>Poussière qui tombe du plafond quand la pièce tremble.</summary>
        public static ParticleSystem CeilingDust(Transform parent)
        {
            var go = Empty("CeilingDust", parent, new Vector3(0, 2.65f, 0));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 0.5f;
            // Plus grosse et plus claire qu'avant : visible dans la pénombre quand elle traverse la lumière de la lampe
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f); main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.73f, 0.65f, 0.9f), new Color(0.55f, 0.5f, 0.45f, 0.75f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.08f, 0.2f); main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
            // Boîte tournée de 90° (émission vers le bas) : X/Y de l'échelle = surface du plafond, Z = épaisseur.
            // Toute la pièce (murs à ±2.13 m) pour donner l'impression d'un plafond qui s'effondre.
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(3.9f, 3.9f, 0.02f);
            sh.rotation = new Vector3(90f, 0f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.08f; noise.frequency = 0.6f; // tourbillonne en tombant
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleDust;
            return ps;
        }

        // ================================================================== ANATOMIE (helpers)

        /// <summary>Bras : épaule → coude → poignet, manche jusqu'au coude ou au poignet.</summary>
        static void BuildArm(Transform parent, string name, Vector3 shoulder, Vector3 elbow, Vector3 wrist, Material sleeveMat, Material skinMat,
            bool longSleeve, float scale = 1f)
        {
            var upper = new MeshBuilder().Tube(
                new[] { shoulder, Vector3.Lerp(shoulder, elbow, 0.5f), elbow },
                new[] { V2(0.047f, 0.045f) * scale, V2(0.04f, 0.038f) * scale, V2(0.035f, 0.033f) * scale }, 8, Vector3.forward, true, true, 1f, 1f);
            Part(name + "_Upper", parent, upper, sleeveMat);
            var path = new List<Vector3>();
            var radii = new List<Vector2>();
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                path.Add(Vector3.Lerp(elbow, wrist, t));
                float r = Mathf.Lerp(0.033f, 0.022f, t) * scale;
                radii.Add(V2(r * (1f + 0.15f * Mathf.Sin(t * Mathf.PI)), r * 0.82f));
            }
            var fore = new MeshBuilder().Tube(path, radii, 8, Vector3.up, true, true, 1f, 1f / Vector3.Distance(elbow, wrist));
            Part(name + "_Forearm", parent, fore, longSleeve ? sleeveMat : skinMat);
            var elbowBall = new MeshBuilder().Ellipsoid(elbow, Vector3.one * 0.036f * scale, 4, 8);
            Part(name + "_Elbow", parent, elbowBall, sleeveMat);
        }

        /// <summary>Main : paume + quatre doigts (deux phalanges) + pouce. <paramref name="curl"/> plie les doigts.</summary>
        static void BuildHand(Transform parent, string name, Vector3 wrist, Quaternion rot, Material skin, float side, float curl, float scale = 1f,
            float curlDir = -1f, float thumbForward = 0f)
        {
            var b = new MeshBuilder().At(wrist, rot, Vector3.one * scale);
            b.Tube(new[] { new Vector3(0, 0, 0), new Vector3(0, 0.04f, 0), new Vector3(0, 0.075f, 0) },
                new[] { V2(0.022f, 0.011f), V2(0.03f, 0.012f), V2(0.031f, 0.011f) }, 8, Vector3.forward);
            for (int f = 0; f < 4; f++)
            {
                float x = (-0.021f + f * 0.014f) * side;
                float len = f == 0 || f == 3 ? 0.03f : 0.036f;
                var a = new Vector3(x, 0.075f, 0);
                var mid = a + new Vector3(0, len, curlDir * len * curl);
                var tip = mid + new Vector3(0, len * (1f - curl * 0.6f), curlDir * len * curl * 1.3f);
                b.Tube(new[] { a, mid, tip }, new[] { V2(0.0065f, 0.006f), V2(0.006f, 0.0055f), V2(0.0045f, 0.0045f) }, 5, Vector3.forward);
            }
            var t0 = new Vector3(0.026f * side, 0.02f, -0.006f - thumbForward);
            b.Tube(new[] { t0, t0 + new Vector3(0.012f * side, 0.026f, -0.012f), t0 + new Vector3(0.006f * side, 0.05f, -0.022f) },
                new[] { V2(0.009f, 0.008f), V2(0.0075f, 0.007f), V2(0.006f, 0.006f) }, 5, Vector3.right);
            Part(name, parent, b, skin);
        }

        /// <summary>Tête de jeune fille aux cheveux noirs (coupe au carré, frange), visage peint.</summary>
        static Transform BuildHead(Transform parent, Vector3 neckTop, Material face, Material hair, Material skin)
        {
            var head = Empty("Head", parent, neckTop);
            var skull = new MeshBuilder().Ellipsoid(new Vector3(0, 0.1f, 0), new Vector3(0.082f, 0.105f, 0.095f), 12, 18, d =>
            {
                // Mâchoire plus étroite, menton léger, arrière du crâne plus rond
                float jaw = d.y < 0 ? Mathf.Lerp(1f, 0.72f, -d.y) : 1f;
                float back = d.z < 0 ? 1.06f : 1f;
                return new Vector3(jaw, 1f, back * (d.y < -0.6f ? 0.85f : 1f));
            });
            Part("Skull", head.transform, skull, face);
            var ears = new MeshBuilder();
            ears.Ellipsoid(new Vector3(-0.08f, 0.095f, -0.005f), new Vector3(0.008f, 0.02f, 0.013f), 4, 6);
            ears.Ellipsoid(new Vector3(0.08f, 0.095f, -0.005f), new Vector3(0.008f, 0.02f, 0.013f), 4, 6);
            Part("Ears", head.transform, ears, skin);

            var h = new MeshBuilder();
            // Calotte
            h.Ellipsoid(new Vector3(0, 0.122f, -0.008f), new Vector3(0.091f, 0.1f, 0.103f), 10, 18, d =>
            {
                if (d.y < -0.1f && d.z > 0.2f) return new Vector3(0.9f, 0.4f, 0.7f); // dégage le visage
                return Vector3.one;
            });
            // Carré : rideau autour de la tête jusqu'à la mâchoire
            for (int i = 0; i < 14; i++)
            {
                float a = Mathf.Lerp(-150f, 150f, i / 13f) * Mathf.Deg2Rad;
                if (Mathf.Abs(a) < 55f * Mathf.Deg2Rad) continue;
                var top = new Vector3(Mathf.Sin(a) * 0.088f, 0.16f, Mathf.Cos(a) * 0.098f);
                var bottom = new Vector3(Mathf.Sin(a) * 0.098f, 0.0f + Random.Range(-0.015f, 0.01f), Mathf.Cos(a) * 0.105f);
                h.Tube(new[] { top, (top + bottom) * 0.5f + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.012f, bottom },
                    new[] { V2(0.024f, 0.012f), V2(0.026f, 0.012f), V2(0.014f, 0.006f) }, 5, Vector3.up);
            }
            // Frange
            for (int i = 0; i < 9; i++)
            {
                float x = Mathf.Lerp(-0.062f, 0.062f, i / 8f);
                float len = 0.07f + Random.Range(-0.01f, 0.012f);
                var a = new Vector3(x * 0.8f, 0.2f, 0.07f);
                var b = new Vector3(x, 0.2f - len * 0.5f, 0.1f);
                var c = new Vector3(x * 1.05f, 0.2f - len, 0.097f);
                h.Tube(new[] { a, b, c }, new[] { V2(0.011f, 0.006f), V2(0.01f, 0.005f), V2(0.004f, 0.003f) }, 4, Vector3.forward);
            }
            // Mèches devant les oreilles
            foreach (var sx in new[] { -1f, 1f })
                h.Tube(new[] { new Vector3(sx * 0.075f, 0.16f, 0.055f), new Vector3(sx * 0.088f, 0.07f, 0.07f), new Vector3(sx * 0.082f, -0.01f, 0.062f) },
                    new[] { V2(0.014f, 0.008f), V2(0.015f, 0.008f), V2(0.006f, 0.004f) }, 5, Vector3.forward);
            Part("Hair", head.transform, h, hair);
            return head.transform;
        }

        static void BuildTorso(Transform parent, Material uniform, Material skirt, Material collar)
        {
            var path = new[] { new Vector3(0, 0.0f, 0), new Vector3(0, 0.1f, 0.0f), new Vector3(0, 0.2f, 0.01f), new Vector3(0, 0.3f, 0.015f), new Vector3(0, 0.38f, 0.01f), new Vector3(0, 0.43f, 0.0f), new Vector3(0, 0.47f, 0.0f) };
            var radii = new[] { V2(0.15f, 0.1f), V2(0.13f, 0.085f), V2(0.125f, 0.085f), V2(0.14f, 0.095f), V2(0.165f, 0.09f), V2(0.17f, 0.08f), V2(0.06f, 0.05f) };
            Part("Torso", parent, new MeshBuilder().Tube(path, radii, 14, Vector3.forward, true, true, 1f, 2.1f, null, 0.5f), uniform);
            // Col marin dans le dos
            Part("SailorFlap", parent, new MeshBuilder().At(new Vector3(0, 0.39f, -0.09f), new Vector3(-12, 0, 0)).Box(Vector3.zero, new Vector3(0.26f, 0.14f, 0.012f), 3f), collar);
            // Nœud du foulard
            var knot = new MeshBuilder();
            knot.Ellipsoid(new Vector3(0, 0.36f, 0.11f), new Vector3(0.022f, 0.016f, 0.012f), 4, 6);
            knot.Tube(new[] { new Vector3(-0.01f, 0.35f, 0.112f), new Vector3(-0.025f, 0.3f, 0.12f), new Vector3(-0.03f, 0.26f, 0.115f) }, new[] { V2(0.012f, 0.004f), V2(0.014f, 0.004f), V2(0.008f, 0.003f) }, 4, Vector3.forward);
            knot.Tube(new[] { new Vector3(0.01f, 0.35f, 0.112f), new Vector3(0.022f, 0.3f, 0.12f), new Vector3(0.028f, 0.27f, 0.116f) }, new[] { V2(0.012f, 0.004f), V2(0.014f, 0.004f), V2(0.008f, 0.003f) }, 4, Vector3.forward);
            Part("Scarf", parent, knot, Scarf);
            // Jupe plissée
            var sk = new MeshBuilder().Tube(new[] { new Vector3(0, 0.02f, 0.01f), new Vector3(0, -0.1f, 0.05f), new Vector3(0, -0.2f, 0.12f) },
                new[] { V2(0.16f, 0.11f), V2(0.2f, 0.16f), V2(0.24f, 0.21f) }, 24, Vector3.forward, false, false, 3f, 3f,
                (r, s) => r == 0 ? 1f : 1f + ((s % 2 == 0) ? 0.05f : -0.03f));
            Part("Skirt", parent, sk, skirt);
            Part("Neck", parent, new MeshBuilder().Tube(new[] { new Vector3(0, 0.44f, 0.005f), new Vector3(0, 0.53f, 0.012f) }, new[] { V2(0.036f, 0.034f), V2(0.033f, 0.032f) }, 8, Vector3.forward), GraySkin);
        }

        // ================================================================== BRAS DE LA JOUEUSE
        /// <summary>Bras à la première personne (coordonnées locales à la caméra).</summary>
        public static PlayerArms BuildPlayerArms(BloodDrip dripPrefab)
        {
            MeshStore.Begin("PlayerArms");
            var root = new GameObject("PlayerArms");
            var arms = root.AddComponent<PlayerArms>();
            root.AddComponent<DamageVisuals>().side = Side.Player;
            var tremor = Empty("Tremor", root.transform, Vector3.zero);
            arms.tremorRoot = tremor.transform;
            arms.bloodDripPrefab = dripPrefab;
            arms.tableHeight = 0.781f;

            var handAnchor = Empty("PlayerHand", tremor.transform, new Vector3(0, -0.125f, 0.385f), new Vector3(-8f, 0, 0));
            var hv = handAnchor.AddComponent<HandView>();
            hv.side = Side.Player;
            hv.radius = 0.55f; hv.maxAngleStep = 8f; hv.maxSpread = 44f; hv.cardScale = 0.066f;
            hv.hoverLift = 0.03f; hv.hoverForward = 0.08f; hv.hoverScale = 1.5f;

            var wounds = new List<(GameObject, Transform)>();
            if (!TryImported("PlayerArms", tremor.transform) && !ThirdParty.TryFpsArms(tremor.transform, hv, arms, root.transform))
            {
                // Chaque main tient une extrémité de l'éventail (paume sous les cartes, doigts derrière, pouce devant,
                // côté intérieur). Les positions par défaut correspondent à 5 cartes ; HandRig les adapte en jeu.
                var lHand = new Vector3(-0.16f, -0.21f, 0.405f); var lElbow = new Vector3(-0.34f, -0.42f, 0.12f);
                var rHand = new Vector3(0.16f, -0.215f, 0.405f); var rElbow = new Vector3(0.34f, -0.42f, 0.12f);
                var lPivot = Empty("LeftPivot", tremor.transform, lElbow).transform;
                var rPivot = Empty("RightPivot", tremor.transform, rElbow).transform;
                var lArm = BuildFirstPersonArm(tremor.transform, "Left", lHand, lElbow, 1f, wounds, 0.6f, 0.035f);
                var rArm = BuildFirstPersonArm(tremor.transform, "Right", rHand, rElbow, -1f, wounds, 0.6f, 0.035f);
                lArm.SetParent(lPivot, true);
                rArm.SetParent(rPivot, true);
                var rig = root.AddComponent<HandRig>();
                rig.hand = hv;
                rig.space = tremor.transform;
                rig.leftPivot = lPivot; rig.leftElbow = lElbow; rig.leftDefaultGrip = lHand;
                rig.rightPivot = rPivot; rig.rightElbow = rElbow; rig.rightDefaultGrip = rHand;
            }
            int[] order = { 0, 4, 1, 5, 2, 6, 3, 7 };
            foreach (int i in order)
            {
                if (i >= wounds.Count) continue;
                arms.wounds.Add(wounds[i].Item1);
                arms.bleedPoints.Add(wounds[i].Item2);
            }
            NoShadows(root);
            return SavePrefab(root, P("PlayerArms")).GetComponent<PlayerArms>();
        }

        static Transform BuildFirstPersonArm(Transform parent, string name, Vector3 hand, Vector3 elbow, float side, List<(GameObject, Transform)> wounds,
            float curl, float thumbForward)
        {
            var arm = Empty(name + "Arm", parent, Vector3.zero);
            var dir = (hand - elbow).normalized;
            var wrist = hand - dir * 0.02f;
            // Manche (jusqu'à mi-avant-bras) + avant-bras nu
            var sleeveEnd = Vector3.Lerp(elbow, wrist, 0.38f);
            Part(name + "_Sleeve", arm.transform, new MeshBuilder().Tube(
                new[] { elbow - dir * 0.18f, elbow, sleeveEnd },
                new[] { V2(0.048f, 0.044f), V2(0.045f, 0.042f), V2(0.041f, 0.039f) }, 10, Vector3.up, true, true, 1f, 1f / 0.3f), SleeveNavy);
            var path = new List<Vector3>();
            var radii = new List<Vector2>();
            for (int i = 0; i <= 5; i++)
            {
                float t = i / 5f;
                path.Add(Vector3.Lerp(elbow, wrist, t));
                float r = Mathf.Lerp(0.034f, 0.021f, t);
                radii.Add(V2(r * (1f + 0.12f * Mathf.Sin(t * Mathf.PI)), r * 0.8f));
            }
            Part(name + "_Forearm", arm.transform, new MeshBuilder().Tube(path, radii, 10, Vector3.up, false, true), PlayerSkin);
            // Main qui tient l'éventail : paume derrière les cartes, pouce devant
            // Doigts vers le haut, légèrement inclinés vers le centre de l'éventail ; paume tournée vers la caméra
            var handRot = Quaternion.LookRotation(Vector3.forward, new Vector3(side * 0.35f, 1f, 0.08f).normalized);
            // curlDir = +1 : les doigts se replient vers l'avant (derrière les cartes), pas vers la caméra
            BuildHand(arm.transform, name + "_Hand", wrist + new Vector3(0, 0, 0.012f), handRot, PlayerSkin, side, curl, 1f, 1f, thumbForward);

            // Marques (cachées au départ) sur le dessus de l'avant-bras, face à la caméra
            float[] ts = { 0.45f, 0.6f, 0.73f, 0.86f };
            for (int i = 0; i < ts.Length; i++)
            {
                var p = Vector3.Lerp(elbow, wrist, ts[i]);
                var toCam = (Vector3.zero - p).normalized;
                var n = Vector3.ProjectOnPlane(toCam, dir).normalized;
                float r = Mathf.Lerp(0.034f, 0.021f, ts[i]) * 0.9f;
                var pos = p + n * (r + 0.0015f);
                var w = Decal($"Wound{i}", arm.transform, pos, n, new Vector2(0.038f, 0.02f), Wound, Random.Range(-35f, 35f) + 90f);
                w.SetActive(false);
                var bleed = Empty($"Bleed{i}", arm.transform, pos + Vector3.down * 0.008f);
                wounds.Add((w, bleed.transform));
            }
            return arm.transform;
        }

        // ================================================================== LA DÉPRESSION
        public static OpponentView BuildOpponent()
        {
            MeshStore.Begin("Depression");
            var root = new GameObject("Depression");
            var view = root.AddComponent<OpponentView>();
            root.AddComponent<DamageVisuals>().side = Side.Opponent;
            BuildChair(root.transform, new Vector3(0, 0, -0.06f), 0f, "Opponent");

            var body = Empty("Body", root.transform, new Vector3(0, 0.48f, 0));
            view.body = body.transform;
            var torso = Empty("Torso", body.transform, Vector3.zero, new Vector3(9f, 0, 0));
            var imported = TryImported("Depression", torso.transform);
            Transform head = imported ? null : ThirdParty.TryMaiden(body.transform, view);
            if (head) ThirdParty.AddMaidenBlood(root.transform);
            else if (!imported) head = ThirdParty.TryCharacter(body.transform, view);
            bool thirdParty = head != null;
            if (thirdParty) view.leftArm = view.rightArm = torso.transform;
            if (!imported && !thirdParty)
            {
                BuildTorso(torso.transform, SailorGray, Skirt, Collar);
                head = BuildHead(torso.transform, new Vector3(0, 0.52f, 0.015f), DepressionFace, Hair, GraySkin);

                // Bras posés sur la table, mains tenant les cartes
                var lShoulder = new Vector3(-0.17f, 0.41f, 0.0f);
                var rShoulder = new Vector3(0.17f, 0.41f, 0.0f);
                var lElbow = new Vector3(-0.21f, 0.27f, 0.3f);
                var rElbow = new Vector3(0.21f, 0.27f, 0.3f);
                var lWrist = new Vector3(-0.07f, 0.36f, 0.33f);
                var rWrist = new Vector3(0.07f, 0.36f, 0.33f);
                view.leftArm = Empty("LeftArm", torso.transform, Vector3.zero).transform;
                view.rightArm = Empty("RightArm", torso.transform, Vector3.zero).transform;
                BuildArm(view.leftArm, "L", lShoulder, lElbow, lWrist, SleeveGray, GraySkin, false, 0.95f);
                BuildArm(view.rightArm, "R", rShoulder, rElbow, rWrist, SleeveGray, GraySkin, false, 0.95f);
                BuildHand(view.leftArm, "LHand", lWrist, Quaternion.LookRotation(Vector3.forward, (lWrist - lElbow).normalized) * Quaternion.Euler(-60, 0, 0), GraySkin, -1f, 0.5f, 0.95f);
                BuildHand(view.rightArm, "RHand", rWrist, Quaternion.LookRotation(Vector3.forward, (rWrist - rElbow).normalized) * Quaternion.Euler(-60, 0, 0), GraySkin, 1f, 0.5f, 0.95f);
                view.bleedPoints.Add(head);
                view.bleedPoints.Add(Empty("BleedL", view.leftArm, lElbow).transform);
                view.bleedPoints.Add(Empty("BleedR", view.rightArm, rElbow).transform);
                foreach (var (arm, e, w) in new[] { (view.leftArm, lElbow, lWrist), (view.rightArm, rElbow, rWrist) })
                    for (int i = 0; i < 3; i++)
                    {
                        var p = Vector3.Lerp(e, w, 0.3f + i * 0.2f);
                        Decal("Scar", arm, p + Vector3.up * 0.023f, Vector3.up + Vector3.forward * 0.3f, new Vector2(0.035f, 0.018f), Wound, 90f + i * 25f);
                    }

                // Yeux qui luisent faiblement
                var eyes = Empty("Eyes", head, Vector3.zero);
                foreach (var sx in new[] { -1f, 1f })
                {
                    var e = Prim(PrimitiveType.Sphere, sx < 0 ? "EyeL" : "EyeR", eyes.transform, new Vector3(sx * 0.03f, 0.112f, 0.087f), new Vector3(0.008f, 0.006f, 0.004f), Eye);
                    view.eyeRenderers.Add(e.GetComponent<Renderer>());
                }
                // Fissures (révélées quand elle perd des manches)
                view.cracks.AddRange(new[]
                {
                    Decal("Crack0", head, new Vector3(-0.03f, 0.15f, 0.093f), Vector3.forward, new Vector2(0.06f, 0.06f), Crack),
                    Decal("Crack1", head, new Vector3(0.045f, 0.08f, 0.088f), new Vector3(0.35f, 0, 1f), new Vector2(0.055f, 0.055f), Crack, 40f),
                    Decal("Crack2", torso.transform, new Vector3(0.06f, 0.3f, 0.1f), Vector3.forward, new Vector2(0.09f, 0.09f), Crack, 80f),
                    Decal("Crack3", head, new Vector3(-0.055f, 0.06f, 0.08f), new Vector3(-0.45f, 0, 1f), new Vector2(0.05f, 0.05f), Crack, 160f),
                    Decal("Crack4", torso.transform, new Vector3(-0.07f, 0.16f, 0.12f), Vector3.forward, new Vector2(0.1f, 0.1f), Crack, 200f),
                    Decal("Crack5", head, new Vector3(0.02f, 0.18f, 0.085f), Vector3.forward, new Vector2(0.05f, 0.05f), Crack, 300f)
                });
            }
            else if (imported)
            {
                head = Empty("Head", torso.transform, new Vector3(0, 0.52f, 0)).transform;
                view.leftArm = view.rightArm = torso.transform;
            }
            view.head = head;

            // Éventail tenu haut (sous le menton), ~17 cm au-dessus de la table : ses poignets et avant-bras
            // restent au-dessus du plateau (OpponentArmsIK relève encore l'éventail quand elle se penche)
            var handAnchor = Empty("OpponentHand", torso.transform, new Vector3(0, 0.6f, 0.32f), new Vector3(8f, 0, 0));
            var hv = handAnchor.AddComponent<HandView>();
            hv.side = Side.Opponent;
            hv.radius = 0.45f; hv.maxAngleStep = 10f; hv.cardScale = 0.062f; hv.maxSpread = 50f;
            var oppIk = root.GetComponent<OpponentArmsIK>();
            if (oppIk) oppIk.hand = hv;

            // Liseré rouge derrière elle (ambiance surnaturelle)
            var rimGo = Empty("RimLight", root.transform, new Vector3(0, 1.25f, -0.45f));
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Point; rim.color = Palette.Blood; rim.intensity = 1.4f; rim.range = 1.4f; rim.shadows = LightShadows.None;

            return SavePrefab(root, P("Depression")).GetComponent<OpponentView>();
        }
    }
}
