using System;
using System.Collections.Generic;
using System.IO;
using AL.Kingdom.Visuals.Architecture;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AL.EditorTools.Architecture
{
    public static class CrownlandsStormwrightAnimationPrototypeBuilder
    {
        public const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/" +
            "Crownlands_Stormwright_AnimationPrototype.prefab";

        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "CrownlandsStormwrightAnimationPrototype.unity";

        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Materials";

        public const string CrownlandsProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Crownlands_Stormwright_ConstructionProfile.asset";

        public const string StoneholdProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Stonehold_Workshop_ConstructionProfile.asset";

        public const string EldergroveProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Eldergrove_Atelier_ConstructionProfile.asset";

        public const string UmbralProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Umbral_Veilwright_ConstructionProfile.asset";

        private const int PreviewSize = 640;
        private const int PreviewFrameRate = 15;
        private const string PreviewArgument = "-alPreviewOutput";

        [MenuItem("Another Life/Architecture/Build Crownlands Stormwright Animation Prototype")]
        public static void Build()
        {
            EnsureFolders();
            MaterialSet materials = CreateMaterials();
            ProfileSet profiles = CreateProfiles();
            GameObject prototype = BuildPrototype(materials, profiles.Crownlands);

            try
            {
                PrefabUtility.SaveAsPrefabAsset(prototype, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(prototype);
            }

            CreatePreviewScene(materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[AL-ARCHITECTURE] Built the Crownlands stormwright graybox prefab " +
                "and isolated animation preview scene.");
        }

        public static void BuildAndRenderFromCommandLine()
        {
            Build();
            string outputDirectory = GetCommandLineValue(PreviewArgument);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException(
                    $"{PreviewArgument} must provide an output directory.");
            }

            RenderPreviewFrames(outputDirectory);
        }

        public static void RenderPreviewFrames(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller =
                Object.FindObjectOfType<ArchitectureConstructionAnimationController>();
            Camera camera = Camera.main;

            if (controller == null || camera == null)
            {
                throw new InvalidOperationException(
                    "The Crownlands animation prototype scene is missing its controller or camera.");
            }

            controller.ConfigurePlayback(false, false, false);
            int frameCount = Mathf.CeilToInt(
                controller.PresentationDuration * PreviewFrameRate);
            var renderTexture = new RenderTexture(
                PreviewSize,
                PreviewSize,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var frameTexture = new Texture2D(
                PreviewSize,
                PreviewSize,
                TextureFormat.RGB24,
                false,
                false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                renderTexture.antiAliasing = 1;
                renderTexture.Create();
                camera.targetTexture = renderTexture;

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float time = frameIndex / (float)(frameCount - 1) *
                        controller.PresentationDuration;
                    controller.SetPreviewTime(time);
                    camera.Render();

                    RenderTexture.active = renderTexture;
                    frameTexture.ReadPixels(
                        new Rect(0, 0, PreviewSize, PreviewSize),
                        0,
                        0,
                        false);
                    frameTexture.Apply(false, false);
                    byte[] png = frameTexture.EncodeToPNG();
                    string framePath = Path.Combine(
                        outputDirectory,
                        $"crownlands_stormwright_{frameIndex:D4}.png");
                    File.WriteAllBytes(framePath, png);
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(frameTexture);
                controller.SetPreviewTime(
                    controller.PresentationDuration);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"[AL-ARCHITECTURE] Rendered {frameCount} Crownlands preview frames " +
                $"to {outputDirectory}.");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/AL/Art/Generated/Architecture",
                "Assets/AL/Art/Generated/Architecture/Crownlands",
                "Assets/AL/Art/Generated/Architecture/Profiles",
                MaterialFolder,
                "Assets/AL/Scenes/Prototypes"
            };

            foreach (string folder in folders)
            {
                Directory.CreateDirectory(folder);
            }

            AssetDatabase.Refresh();
        }

        private static ProfileSet CreateProfiles()
        {
            return new ProfileSet
            {
                Stonehold = CreateOrUpdateProfile(
                    StoneholdProfilePath,
                    "stonehold.workshop",
                    "stonehold",
                    "workshop",
                    3,
                    new[]
                    {
                        Motion(
                            new Vector3(0f, -0.20f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(1f, 0.10f, 1f)),
                        Motion(
                            new Vector3(0f, 1.35f, 0f),
                            new Vector3(0f, 0.06f, 0f),
                            Vector3.zero,
                            new Vector3(0f, 0f, -7f),
                            new Vector3(1f, 0.18f, 1f)),
                        Motion(
                            new Vector3(0f, 0.45f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(0f, 0f, -5f),
                            Vector3.one),
                        Motion(
                            new Vector3(0f, 1.20f, 0f),
                            new Vector3(0f, 0.06f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.one),
                        Motion(
                            new Vector3(0f, -0.25f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.one * 0.70f)
                    }),
                Eldergrove = CreateOrUpdateProfile(
                    EldergroveProfilePath,
                    "eldergrove.atelier",
                    "eldergrove",
                    "cultivation_atelier",
                    4,
                    new[]
                    {
                        Motion(
                            new Vector3(0f, -0.18f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(1f, 0.10f, 1f)),
                        Motion(
                            new Vector3(0f, -1.05f, 0f),
                            new Vector3(0f, -0.05f, 0f),
                            Vector3.zero,
                            new Vector3(0f, 0f, -5f),
                            new Vector3(1f, 0.16f, 1f)),
                        Motion(
                            new Vector3(0f, -0.42f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(0f, 0f, -18f),
                            new Vector3(0.45f, 0.08f, 0.45f)),
                        Motion(
                            new Vector3(0f, -0.18f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(0f, 0f, -7f),
                            new Vector3(0.72f, 0.72f, 0.72f)),
                        Motion(
                            new Vector3(0f, 1.10f, 0f),
                            new Vector3(0f, 0.05f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.one)
                    }),
                Crownlands = CreateOrUpdateProfile(
                    CrownlandsProfilePath,
                    "crownlands.stormwright",
                    "crownlands",
                    "stormwright_atelier",
                    3,
                    new[]
                    {
                        Motion(
                            new Vector3(0f, -0.24f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(1f, 0.08f, 1f)),
                        Motion(
                            new Vector3(0f, -1.45f, 0f),
                            new Vector3(0f, -0.08f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(1f, 0.12f, 1f)),
                        Motion(
                            new Vector3(0f, -0.32f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(0f, 0f, -48f),
                            Vector3.one),
                        Motion(
                            new Vector3(0f, 1.15f, 0f),
                            new Vector3(0f, 0.08f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.one),
                        Motion(
                            new Vector3(0f, -0.30f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.one * 0.65f)
                    }),
                Umbral = CreateOrUpdateProfile(
                    UmbralProfilePath,
                    "umbral.veilwright",
                    "umbral",
                    "veilwright_atelier",
                    3,
                    new[]
                    {
                        Motion(
                            new Vector3(0f, -0.22f, 0f),
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            new Vector3(1f, 0.08f, 1f)),
                        Motion(
                            new Vector3(-0.55f, -1.10f, 0.15f),
                            new Vector3(0.18f, -0.05f, 0f),
                            new Vector3(0f, -8f, 0f),
                            new Vector3(0f, 0f, -6f),
                            new Vector3(1f, 0.12f, 1f)),
                        Motion(
                            new Vector3(0f, -0.25f, 0.40f),
                            new Vector3(0.06f, 0f, 0f),
                            new Vector3(0f, 32f, 0f),
                            new Vector3(0f, 0f, -18f),
                            Vector3.one),
                        Motion(
                            new Vector3(0.35f, 1.30f, -0.25f),
                            new Vector3(-0.12f, 0.08f, 0.05f),
                            new Vector3(0f, -8f, 0f),
                            new Vector3(0f, 0f, -12f),
                            Vector3.one),
                        Motion(
                            new Vector3(0f, -0.35f, 0f),
                            new Vector3(0.04f, 0f, -0.04f),
                            new Vector3(0f, 20f, 0f),
                            new Vector3(0f, 28f, 0f),
                            Vector3.one * 0.55f)
                    })
            };
        }

        private static ArchitectureConstructionAnimationProfile
            CreateOrUpdateProfile(
                string assetPath,
                string profileId,
                string realmId,
                string buildingArchetype,
                int cutawayStageIndex,
                ArchitectureConstructionStageMotion[] motions)
        {
            ArchitectureConstructionAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    ArchitectureConstructionAnimationProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<
                    ArchitectureConstructionAnimationProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.name = Path.GetFileNameWithoutExtension(assetPath);
            profile.Configure(
                profileId,
                realmId,
                buildingArchetype,
                16f,
                1.55f,
                9.1f,
                cutawayStageIndex,
                new Vector2(13.35f, 14.8f),
                motions);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static ArchitectureConstructionStageMotion Motion(
            Vector3 entryOffset,
            Vector3 perPartOffset,
            Vector3 entryEuler,
            Vector3 alternatingEuler,
            Vector3 entryScaleMultiplier)
        {
            return ArchitectureConstructionStageMotion.Create(
                entryOffset,
                perPartOffset,
                entryEuler,
                alternatingEuler,
                entryScaleMultiplier);
        }

        private static MaterialSet CreateMaterials()
        {
            return new MaterialSet
            {
                Stone = CreateMaterial(
                    $"{MaterialFolder}/MAT_Crownlands_Stormwright_Stone.mat",
                    new Color(0.48f, 0.50f, 0.55f),
                    0.03f,
                    0.38f),
                Silver = CreateMaterial(
                    $"{MaterialFolder}/MAT_Crownlands_Stormwright_Silver.mat",
                    new Color(0.62f, 0.68f, 0.78f),
                    0.72f,
                    0.62f),
                BlueSlate = CreateMaterial(
                    $"{MaterialFolder}/MAT_Crownlands_Stormwright_BlueSlate.mat",
                    new Color(0.035f, 0.09f, 0.28f),
                    0.15f,
                    0.45f),
                Bronze = CreateMaterial(
                    $"{MaterialFolder}/MAT_Crownlands_Stormwright_Bronze.mat",
                    new Color(0.34f, 0.22f, 0.09f),
                    0.62f,
                    0.44f),
                Indigo = CreateMaterial(
                    $"{MaterialFolder}/MAT_Crownlands_Stormwright_Indigo.mat",
                    new Color(0.12f, 0.18f, 0.62f),
                    0.2f,
                    0.68f,
                    new Color(0.05f, 0.08f, 0.24f)),
                Ground = CreateMaterial(
                    $"{MaterialFolder}/MAT_Crownlands_Stormwright_Ground.mat",
                    new Color(0.018f, 0.026f, 0.045f),
                    0f,
                    0.26f)
            };
        }

        private static Material CreateMaterial(
            string assetPath,
            Color color,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard") ??
                    Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "No supported lit shader is available for the animation prototype.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.name = Path.GetFileNameWithoutExtension(assetPath);
            material.color = color;

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildPrototype(
            MaterialSet materials,
            ArchitectureConstructionAnimationProfile profile)
        {
            var root = new GameObject("Crownlands_Stormwright_AnimationPrototype");

            Transform plot = CreateGroup(root.transform, "PlotPrepared");
            CreateBlock(
                plot,
                "Foundation",
                new Vector3(0f, 0.12f, 0f),
                new Vector3(5.4f, 0.24f, 4.3f),
                materials.Stone);
            CreateBlock(
                plot,
                "EntranceStep",
                new Vector3(0f, 0.16f, -2.35f),
                new Vector3(1.45f, 0.18f, 0.55f),
                materials.Stone);
            CreateBlock(
                plot,
                "GroundChannelNorthSouth",
                new Vector3(0f, 0.26f, 0f),
                new Vector3(0.08f, 0.025f, 3.7f),
                materials.Bronze);
            CreateBlock(
                plot,
                "GroundChannelEastWest",
                new Vector3(0f, 0.265f, 0f),
                new Vector3(3.9f, 0.025f, 0.08f),
                materials.Bronze);

            Transform civicFrame = CreateGroup(root.transform, "CivicFrameRaised");
            CreateBlock(
                civicFrame,
                "BackWall",
                new Vector3(0f, 1.22f, 1.78f),
                new Vector3(4.65f, 2.05f, 0.38f),
                materials.Stone);
            CreateBlock(
                civicFrame,
                "WestWall",
                new Vector3(-2.28f, 1.18f, 0.08f),
                new Vector3(0.42f, 1.95f, 3.55f),
                materials.Stone);
            CreateBlock(
                civicFrame,
                "EastWall",
                new Vector3(2.28f, 1.18f, 0.08f),
                new Vector3(0.42f, 1.95f, 3.55f),
                materials.Stone);
            CreateBlock(
                civicFrame,
                "WestPier",
                new Vector3(-2.15f, 1.72f, -1.70f),
                new Vector3(0.72f, 3.05f, 0.72f),
                materials.Stone);
            CreateBlock(
                civicFrame,
                "EastPier",
                new Vector3(2.15f, 1.72f, -1.70f),
                new Vector3(0.72f, 3.05f, 0.72f),
                materials.Stone);
            CreateBlock(
                civicFrame,
                "WestPierCap",
                new Vector3(-2.15f, 3.35f, -1.70f),
                new Vector3(0.90f, 0.26f, 0.90f),
                materials.Silver);
            CreateBlock(
                civicFrame,
                "EastPierCap",
                new Vector3(2.15f, 3.35f, -1.70f),
                new Vector3(0.90f, 0.26f, 0.90f),
                materials.Silver);

            Transform frontRib = CreateArch(
                root.transform,
                "SilverRibFront",
                new Vector3(0f, 1.2f, -1.9f),
                2.22f,
                13,
                0.20f,
                materials.Silver);
            Transform rearRib = CreateArch(
                root.transform,
                "SilverRibRear",
                new Vector3(0f, 1.65f, 1.25f),
                1.62f,
                11,
                0.14f,
                materials.Silver);
            CreateBeam(
                frontRib,
                "WestConductor",
                new Vector3(-2.12f, 1.36f, 0f),
                new Vector3(0f, 3.44f, 0f),
                0.08f,
                materials.Bronze);
            CreateBeam(
                frontRib,
                "EastConductor",
                new Vector3(2.12f, 1.36f, 0f),
                new Vector3(0f, 3.44f, 0f),
                0.08f,
                materials.Bronze);

            Transform westRoof = CreateGroup(root.transform, "RoofWingWest");
            CreateBlock(
                westRoof,
                "WestSlateHigh",
                new Vector3(-0.58f, 3.02f, 0.02f),
                new Vector3(1.20f, 0.22f, 3.44f),
                materials.BlueSlate);
            CreateBlock(
                westRoof,
                "WestSlateMiddle",
                new Vector3(-1.30f, 2.84f, 0.02f),
                new Vector3(1.42f, 0.24f, 3.58f),
                materials.BlueSlate);
            CreateBlock(
                westRoof,
                "WestSlateLow",
                new Vector3(-2.02f, 2.64f, 0.02f),
                new Vector3(0.62f, 0.26f, 3.70f),
                materials.BlueSlate);
            Transform eastRoof = CreateGroup(root.transform, "RoofWingEast");
            CreateBlock(
                eastRoof,
                "EastSlateHigh",
                new Vector3(0.58f, 3.02f, 0.02f),
                new Vector3(1.20f, 0.22f, 3.44f),
                materials.BlueSlate);
            CreateBlock(
                eastRoof,
                "EastSlateMiddle",
                new Vector3(1.30f, 2.84f, 0.02f),
                new Vector3(1.42f, 0.24f, 3.58f),
                materials.BlueSlate);
            CreateBlock(
                eastRoof,
                "EastSlateLow",
                new Vector3(2.02f, 2.64f, 0.02f),
                new Vector3(0.62f, 0.26f, 3.70f),
                materials.BlueSlate);
            CreateBlock(
                westRoof,
                "RoofRidge",
                new Vector3(0f, 3.18f, 0.02f),
                new Vector3(0.24f, 0.24f, 3.60f),
                materials.Silver);
            Transform lantern = CreateGroup(root.transform, "LanternOcclusion");
            CreateCylinder(
                lantern,
                "LanternBase",
                new Vector3(0f, 3.18f, 0.1f),
                new Vector3(1.02f, 0.15f, 1.02f),
                materials.Silver);
            CreateSphere(
                lantern,
                "LanternDome",
                new Vector3(0f, 3.56f, 0.1f),
                new Vector3(0.92f, 0.48f, 0.92f),
                materials.Silver);
            Renderer lanternCore = CreateSphere(
                lantern,
                "LanternCore",
                new Vector3(0f, 3.52f, 0.1f),
                new Vector3(0.22f, 0.22f, 0.22f),
                materials.Indigo).GetComponent<Renderer>();
            CreateCylinder(
                lantern,
                "LanternFinial",
                new Vector3(0f, 4.08f, 0.1f),
                new Vector3(0.10f, 0.28f, 0.10f),
                materials.Bronze);

            Transform engine = CreateGroup(root.transform, "CalibrationEngine");
            CreateCylinder(
                engine,
                "EnginePlinth",
                new Vector3(0f, 0.48f, 0f),
                new Vector3(0.85f, 0.22f, 0.85f),
                materials.Stone);
            CreateCylinder(
                engine,
                "EngineMetalRing",
                new Vector3(0f, 0.72f, 0f),
                new Vector3(0.68f, 0.12f, 0.68f),
                materials.Silver);
            Renderer engineCore = CreateSphere(
                engine,
                "EngineCore",
                new Vector3(0f, 0.93f, 0f),
                new Vector3(0.28f, 0.28f, 0.28f),
                materials.Indigo).GetComponent<Renderer>();
            Transform instrumentRing = CreateInstrumentRing(engine, materials.Bronze);

            Transform westBench = CreateGroup(root.transform, "WestWorkstation");
            CreateBlock(
                westBench,
                "WestBench",
                new Vector3(-1.55f, 0.62f, 0.72f),
                new Vector3(1.15f, 0.18f, 0.62f),
                materials.Bronze);
            CreateBlock(
                westBench,
                "WestCabinet",
                new Vector3(-1.55f, 0.38f, 0.72f),
                new Vector3(0.95f, 0.46f, 0.52f),
                materials.BlueSlate);
            Transform eastBench = CreateGroup(root.transform, "EastWorkstation");
            CreateBlock(
                eastBench,
                "EastBench",
                new Vector3(1.55f, 0.62f, 0.72f),
                new Vector3(1.15f, 0.18f, 0.62f),
                materials.Bronze);
            CreateBlock(
                eastBench,
                "EastCabinet",
                new Vector3(1.55f, 0.38f, 0.72f),
                new Vector3(0.95f, 0.46f, 0.52f),
                materials.BlueSlate);
            CreateBeam(
                engine,
                "EngineToLanternConductor",
                new Vector3(0f, 1.12f, 0.05f),
                new Vector3(0f, 3.28f, 0.1f),
                0.055f,
                materials.Bronze);
            CreateBeam(
                engine,
                "EngineToWestBench",
                new Vector3(-0.24f, 0.45f, 0f),
                new Vector3(-1.55f, 0.45f, 0.72f),
                0.045f,
                materials.Bronze);
            CreateBeam(
                engine,
                "EngineToEastBench",
                new Vector3(0.24f, 0.45f, 0f),
                new Vector3(1.55f, 0.45f, 0.72f),
                0.045f,
                materials.Bronze);

            Transform[] pulseRoute = CreatePulseRoute(root.transform);
            GameObject pulseOrbObject = CreateSphere(
                root.transform,
                "CalibrationPulse",
                pulseRoute[0].localPosition,
                new Vector3(0.22f, 0.22f, 0.22f),
                materials.Indigo);
            Transform pulseOrb = pulseOrbObject.transform;
            Renderer pulseOrbRenderer = pulseOrbObject.GetComponent<Renderer>();
            var pulseLight = pulseOrbObject.AddComponent<Light>();
            pulseLight.type = LightType.Point;
            pulseLight.color = new Color(0.16f, 0.26f, 1f);
            pulseLight.range = 3.4f;
            pulseLight.intensity = 0f;
            pulseLight.shadows = LightShadows.None;
            pulseOrb.gameObject.SetActive(false);

            var activity =
                root.AddComponent<CrownlandsStormwrightStableActivity>();
            activity.Configure(
                instrumentRing,
                pulseOrb,
                pulseRoute,
                new[] { engineCore, lanternCore, pulseOrbRenderer },
                pulseLight);

            var controller =
                root.AddComponent<ArchitectureConstructionAnimationController>();
            controller.Configure(
                profile,
                new[]
                {
                    new[] { plot },
                    new[] { civicFrame },
                    new[] { frontRib, rearRib },
                    new[] { westRoof, eastRoof, lantern },
                    new[] { engine, westBench, eastBench }
                },
                new MonoBehaviour[] { activity });
            controller.ConfigurePlayback(false, false, false);
            controller.SetPreviewTime(controller.PresentationDuration);

            return root;
        }

        private static void CreatePreviewScene(MaterialSet materials)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Failed to load generated prefab at {PrefabPath}.");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            var controller =
                instance.GetComponent<ArchitectureConstructionAnimationController>();
            controller.ConfigurePlayback(true, true, false);
            controller.SetPreviewTime(controller.PresentationDuration);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "PrototypeGround";
            ground.transform.position = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = new Vector3(1.25f, 1f, 1.25f);
            ground.GetComponent<Renderer>().sharedMaterial = materials.Ground;
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.014f, 0.028f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(5.0f, 5.5f, -10.6f);
            camera.transform.LookAt(new Vector3(0f, 1.48f, -0.15f));

            var keyLightObject = new GameObject("KeyLight");
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.72f, 0.80f, 1f);
            keyLight.intensity = 1.35f;
            keyLight.shadows = LightShadows.Soft;
            keyLightObject.transform.rotation = Quaternion.Euler(42f, -36f, 0f);

            var warmFillObject = new GameObject("WarmFill");
            Light warmFill = warmFillObject.AddComponent<Light>();
            warmFill.type = LightType.Directional;
            warmFill.color = new Color(1f, 0.72f, 0.42f);
            warmFill.intensity = 0.34f;
            warmFill.shadows = LightShadows.None;
            warmFillObject.transform.rotation = Quaternion.Euler(28f, 142f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.16f, 0.22f);
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Transform[] CreatePulseRoute(Transform parent)
        {
            Vector3[] positions =
            {
                new Vector3(0f, 0.93f, 0f),
                new Vector3(-1.65f, 2.22f, -1.86f),
                new Vector3(0f, 3.55f, 0.1f),
                new Vector3(1.65f, 2.22f, -1.86f),
                new Vector3(0f, 0.93f, 0f)
            };
            var route = new Transform[positions.Length];

            for (int index = 0; index < positions.Length; index++)
            {
                Transform node = CreateGroup(parent, $"PulseRoute_{index:D2}");
                node.localPosition = positions[index];
                route[index] = node;
            }

            return route;
        }

        private static Transform CreateInstrumentRing(Transform parent, Material material)
        {
            Transform ring = CreateGroup(parent, "CalibratedInstrumentRing");
            ring.localPosition = new Vector3(0f, 0.98f, 0f);
            const int segmentCount = 10;

            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * 0.48f,
                    0f,
                    Mathf.Sin(angle) * 0.48f);
                CreateBlock(
                    ring,
                    $"InstrumentTick_{index:D2}",
                    position,
                    new Vector3(0.10f, 0.08f, 0.22f),
                    material,
                    new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
            }

            return ring;
        }

        private static Transform CreateArch(
            Transform parent,
            string name,
            Vector3 center,
            float radius,
            int segmentCount,
            float thickness,
            Material material)
        {
            Transform arch = CreateGroup(parent, name);

            for (int index = 0; index < segmentCount; index++)
            {
                float firstAngle = index / (float)segmentCount * Mathf.PI;
                float secondAngle = (index + 1) / (float)segmentCount * Mathf.PI;
                Vector3 start = center + new Vector3(
                    Mathf.Cos(firstAngle) * radius,
                    Mathf.Sin(firstAngle) * radius,
                    0f);
                Vector3 end = center + new Vector3(
                    Mathf.Cos(secondAngle) * radius,
                    Mathf.Sin(secondAngle) * radius,
                    0f);
                CreateBeam(
                    arch,
                    $"ArchSegment_{index:D2}",
                    start - center,
                    end - center,
                    thickness,
                    material);
            }

            arch.localPosition = center;
            return arch;
        }

        private static GameObject CreateBeam(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject beam = CreateBlock(
                parent,
                name,
                (start + end) * 0.5f,
                new Vector3(thickness, thickness, direction.magnitude),
                material);
            beam.transform.localRotation = Quaternion.FromToRotation(
                Vector3.forward,
                direction.normalized);
            return beam;
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Vector3? localEulerAngles = null)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            block.transform.localEulerAngles = localEulerAngles ?? Vector3.zero;
            ConfigurePrototypeRenderer(
                block.GetComponent<Renderer>(),
                material);
            Object.DestroyImmediate(block.GetComponent<Collider>());
            return block;
        }

        private static GameObject CreateCylinder(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = localScale;
            ConfigurePrototypeRenderer(
                cylinder.GetComponent<Renderer>(),
                material);
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            return cylinder;
        }

        private static GameObject CreateSphere(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localScale = localScale;
            ConfigurePrototypeRenderer(
                sphere.GetComponent<Renderer>(),
                material);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            return sphere;
        }

        private static void ConfigurePrototypeRenderer(
            Renderer targetRenderer,
            Material material)
        {
            targetRenderer.sharedMaterial = material;
            targetRenderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            targetRenderer.lightProbeUsage = LightProbeUsage.Off;
            targetRenderer.reflectionProbeUsage =
                ReflectionProbeUsage.Off;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static string GetCommandLineValue(string argument)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argument, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }

        private sealed class MaterialSet
        {
            public Material Stone;
            public Material Silver;
            public Material BlueSlate;
            public Material Bronze;
            public Material Indigo;
            public Material Ground;
        }

        private sealed class ProfileSet
        {
            public ArchitectureConstructionAnimationProfile Stonehold { get; set; }
            public ArchitectureConstructionAnimationProfile Eldergrove { get; set; }
            public ArchitectureConstructionAnimationProfile Crownlands { get; set; }
            public ArchitectureConstructionAnimationProfile Umbral { get; set; }
        }
    }
}
