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
    /// <summary>
    /// Builds an isolated, deterministic Umbral architecture graybox. The
    /// prototype proves construction ownership and bounded fourth-realm motion;
    /// it is not production geometry or a gameplay integration.
    /// </summary>
    public static class UmbralVeilwrightAnimationPrototypeBuilder
    {
        public const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/" +
            "Umbral_Veilwright_AnimationPrototype.prefab";

        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "UmbralVeilwrightAnimationPrototype.unity";

        public const string ProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Umbral_Veilwright_ConstructionProfile.asset";

        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Umbral/Materials";

        private const int PreviewSize = 640;
        private const int PreviewFrameRate = 15;
        private const string PreviewArgument = "-alUmbralPreviewOutput";

        [MenuItem("Another Life/Architecture/Build Umbral Veilwright Animation Prototype")]
        public static void Build()
        {
            EnsureFolders();
            MaterialSet materials = CreateMaterials();
            ArchitectureConstructionAnimationProfile profile =
                CreateOrUpdateProfile();
            GameObject prototype = BuildPrototype(materials, profile);

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
                "[AL-ARCHITECTURE] Built the Umbral veilwright graybox prefab " +
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
                    "The Umbral animation prototype scene is missing its controller or camera.");
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
                    File.WriteAllBytes(
                        Path.Combine(
                            outputDirectory,
                            $"umbral_veilwright_{frameIndex:D4}.png"),
                        png);
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(frameTexture);
                controller.SetPreviewTime(controller.PresentationDuration);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"[AL-ARCHITECTURE] Rendered {frameCount} Umbral preview frames " +
                $"to {outputDirectory}.");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/AL/Art/Generated/Architecture",
                "Assets/AL/Art/Generated/Architecture/Umbral",
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

        private static ArchitectureConstructionAnimationProfile
            CreateOrUpdateProfile()
        {
            ArchitectureConstructionAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    ArchitectureConstructionAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<
                    ArchitectureConstructionAnimationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.name = Path.GetFileNameWithoutExtension(ProfilePath);
            profile.Configure(
                "umbral.veilwright",
                "umbral",
                "veilwright_atelier",
                16f,
                1.55f,
                9.1f,
                3,
                new Vector2(9.05f, 12.95f),
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
                });
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
                GraphiteStone = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_GraphiteStone.mat",
                    new Color(0.115f, 0.115f, 0.145f),
                    0.04f,
                    0.31f),
                AshMortar = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_AshMortar.mat",
                    new Color(0.34f, 0.32f, 0.35f),
                    0.02f,
                    0.27f),
                SmokedIron = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_SmokedIron.mat",
                    new Color(0.12f, 0.12f, 0.15f),
                    0.78f,
                    0.48f),
                TarnishedBrass = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_TarnishedBrass.mat",
                    new Color(0.31f, 0.19f, 0.075f),
                    0.70f,
                    0.46f),
                Obsidian = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_Obsidian.mat",
                    new Color(0.055f, 0.035f, 0.085f),
                    0.28f,
                    0.72f),
                AshTimber = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_AshTimber.mat",
                    new Color(0.13f, 0.095f, 0.09f),
                    0.02f,
                    0.25f),
                Aubergine = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_Aubergine.mat",
                    new Color(0.20f, 0.07f, 0.20f),
                    0.02f,
                    0.34f),
                Violet = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_WardViolet.mat",
                    new Color(0.28f, 0.08f, 0.58f),
                    0.14f,
                    0.72f,
                    new Color(0.020f, 0.005f, 0.055f)),
                Ground = CreateMaterial(
                    $"{MaterialFolder}/MAT_Umbral_Ground.mat",
                    new Color(0.012f, 0.014f, 0.026f),
                    0f,
                    0.24f)
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
            SetFloatIfPresent(material, "_Metallic", metallic);
            SetFloatIfPresent(material, "_Glossiness", smoothness);
            SetFloatIfPresent(material, "_Smoothness", smoothness);

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static GameObject BuildPrototype(
            MaterialSet materials,
            ArchitectureConstructionAnimationProfile profile)
        {
            var root = new GameObject("Umbral_Veilwright_AnimationPrototype");

            Transform boundary = CreateGroup(root.transform, "BoundaryMarked");
            CreateBlock(
                boundary,
                "BlackenedStoneFootprint",
                new Vector3(0f, 0.10f, 0f),
                new Vector3(6.60f, 0.20f, 5.35f),
                materials.GraphiteStone);
            CreateBlock(
                boundary,
                "ObliqueEntranceStep",
                new Vector3(0.15f, 0.17f, -2.92f),
                new Vector3(1.90f, 0.18f, 0.68f),
                materials.AshMortar,
                new Vector3(0f, -3f, 0f));
            CreateBlock(
                boundary,
                "FoundationFrontEdge",
                new Vector3(0f, 0.24f, -2.61f),
                new Vector3(6.55f, 0.20f, 0.14f),
                materials.AshMortar);
            CreateBlock(
                boundary,
                "FoundationWestEdge",
                new Vector3(-3.23f, 0.24f, 0f),
                new Vector3(0.14f, 0.20f, 5.20f),
                materials.AshMortar);
            CreateBlock(
                boundary,
                "FoundationEastEdge",
                new Vector3(3.23f, 0.24f, 0f),
                new Vector3(0.14f, 0.20f, 5.20f),
                materials.AshMortar);
            for (int index = -2; index <= 2; index++)
            {
                CreateBlock(
                    boundary,
                    $"FloorJointVertical_{index + 2:D2}",
                    new Vector3(index * 1.08f, 0.216f, 0f),
                    new Vector3(0.045f, 0.022f, 4.70f),
                    materials.AshMortar);
                CreateBlock(
                    boundary,
                    $"FloorJointHorizontal_{index + 2:D2}",
                    new Vector3(0f, 0.218f, index * 0.86f),
                    new Vector3(5.85f, 0.022f, 0.045f),
                    materials.AshMortar);
            }
            CreateRing(
                boundary,
                "DormantCoreBoundary",
                new Vector3(0f, 0.26f, 0.12f),
                1.02f,
                14,
                0.075f,
                materials.TarnishedBrass);

            Vector3[] anchorPositions =
            {
                new Vector3(-1.78f, 0.34f, -1.35f),
                new Vector3(1.82f, 0.34f, -1.28f),
                new Vector3(1.62f, 0.34f, 1.48f),
                new Vector3(-1.58f, 0.34f, 1.56f)
            };
            var anchorPoints = new Transform[anchorPositions.Length];
            var anchorRenderers = new Renderer[anchorPositions.Length];
            for (int index = 0; index < anchorPositions.Length; index++)
            {
                Transform socket = CreateGroup(
                    boundary,
                    $"WardAnchor_{index:D2}");
                socket.localPosition = anchorPositions[index];
                GameObject socketBase = CreateCylinder(
                    socket,
                    "PhysicalSocket",
                    Vector3.zero,
                    new Vector3(0.34f, 0.075f, 0.34f),
                    materials.TarnishedBrass);
                GameObject socketCore = CreateCylinder(
                    socket,
                    "DormantWardInset",
                    new Vector3(0f, 0.105f, 0f),
                    new Vector3(0.18f, 0.035f, 0.18f),
                    materials.Violet);
                socketBase.transform.localEulerAngles =
                    new Vector3(0f, index * 17f, 0f);
                anchorPoints[index] = socket;
                anchorRenderers[index] =
                    socketCore.GetComponent<Renderer>();
            }

            Transform shell = CreateGroup(root.transform, "OffsetShellRaised");
            CreateBlock(
                shell,
                "RearMasonryWall",
                new Vector3(0f, 1.55f, 2.25f),
                new Vector3(5.88f, 2.62f, 0.42f),
                materials.GraphiteStone);
            CreateBlock(
                shell,
                "WestMasonryWall",
                new Vector3(-2.88f, 1.50f, 0.16f),
                new Vector3(0.46f, 2.52f, 4.35f),
                materials.GraphiteStone);
            CreateBlock(
                shell,
                "EastMasonryWall",
                new Vector3(2.88f, 1.50f, 0.16f),
                new Vector3(0.46f, 2.52f, 4.35f),
                materials.GraphiteStone);
            Transform frontFacadeOcclusion = CreateGroup(
                shell,
                "FrontFacadeOcclusion");
            CreateBlock(
                frontFacadeOcclusion,
                "WestFrontFacade",
                new Vector3(-2.02f, 1.56f, -2.12f),
                new Vector3(1.42f, 2.66f, 0.46f),
                materials.GraphiteStone);
            CreateBlock(
                frontFacadeOcclusion,
                "EastFrontFacade",
                new Vector3(2.02f, 1.56f, -2.12f),
                new Vector3(1.42f, 2.66f, 0.46f),
                materials.GraphiteStone);
            CreateBlock(
                shell,
                "RearAshCourse",
                new Vector3(0f, 2.60f, 2.02f),
                new Vector3(5.92f, 0.14f, 0.48f),
                materials.AshMortar);
            CreateBlock(
                shell,
                "WestAshCourse",
                new Vector3(-2.89f, 2.52f, 0.16f),
                new Vector3(0.50f, 0.14f, 4.38f),
                materials.AshMortar);
            CreateBlock(
                shell,
                "EastAshCourse",
                new Vector3(2.89f, 2.52f, 0.16f),
                new Vector3(0.50f, 0.14f, 4.38f),
                materials.AshMortar);

            CreatePointedArch(
                frontFacadeOcclusion,
                "GrandObliqueEntrance",
                new Vector3(0.05f, 0.28f, -2.38f),
                1.05f,
                1.75f,
                2.90f,
                0.30f,
                materials.TarnishedBrass);
            CreatePointedArch(
                frontFacadeOcclusion,
                "WestGothicWindow",
                new Vector3(-2.03f, 0.75f, -2.38f),
                0.40f,
                0.76f,
                1.35f,
                0.24f,
                materials.TarnishedBrass);
            CreatePointedArch(
                frontFacadeOcclusion,
                "EastGothicWindow",
                new Vector3(2.03f, 0.75f, -2.38f),
                0.40f,
                0.76f,
                1.35f,
                0.24f,
                materials.TarnishedBrass);

            Vector3[] buttressPositions =
            {
                new Vector3(-3.12f, 0f, -2.05f),
                new Vector3(3.12f, 0f, -2.05f),
                new Vector3(-3.12f, 0f, 2.05f),
                new Vector3(3.12f, 0f, 2.05f),
                new Vector3(-1.55f, 0f, 2.34f),
                new Vector3(1.55f, 0f, 2.34f)
            };
            for (int index = 0; index < buttressPositions.Length; index++)
            {
                CreateButtress(
                    shell,
                    $"MasonryButtress_{index:D2}",
                    buttressPositions[index],
                    materials);
            }

            Transform anchorFrames = CreateGroup(
                root.transform,
                "VeilAnchorsBound");
            Vector3[] anchorFramePositions =
            {
                new Vector3(-2.58f, 0.15f, -1.62f),
                new Vector3(2.58f, 0.15f, -1.58f),
                new Vector3(2.58f, 0.15f, 1.58f),
                new Vector3(-2.58f, 0.15f, 1.62f)
            };
            for (int index = 0; index < anchorPositions.Length; index++)
            {
                Vector3 basePosition = anchorFramePositions[index];
                Transform frame = CreateGroup(
                    anchorFrames,
                    $"BoundAnchorFrame_{index:D2}");
                frame.localPosition = basePosition;
                CreateBlock(
                    frame,
                    "WardPylon",
                    new Vector3(0f, 0.66f, 0f),
                    new Vector3(0.18f, 1.32f, 0.18f),
                    materials.TarnishedBrass,
                    new Vector3(0f, 0f, index % 2 == 0 ? -8f : 8f));
                CreateBlock(
                    frame,
                    "ObsidianWardHead",
                    new Vector3(0f, 1.30f, 0f),
                    new Vector3(0.38f, 0.26f, 0.38f),
                    materials.Violet,
                    new Vector3(0f, index * 21f, 0f));
                CreateBeam(
                    frame,
                    "OuterGroundBrace",
                    new Vector3(0f, 0.20f, 0f),
                    new Vector3(
                        index < 2 ? -0.42f : 0.42f,
                        0.02f,
                        index % 2 == 0 ? 0.36f : -0.36f),
                    0.10f,
                    materials.SmokedIron);
                CreateBeam(
                    frame,
                    "InnerWardBrace",
                    new Vector3(0f, 1.20f, 0f),
                    new Vector3(
                        index < 2 ? 0.32f : -0.32f,
                        0.58f,
                        index % 2 == 0 ? -0.24f : 0.24f),
                    0.075f,
                    materials.TarnishedBrass);
            }

            Transform roofWest = CreateGroup(
                root.transform,
                "RoofOcclusionWest");
            CreateBlock(
                roofWest,
                "WestOuterRoofPlane",
                new Vector3(-2.26f, 3.48f, -0.08f),
                new Vector3(1.84f, 0.18f, 5.12f),
                materials.Obsidian,
                new Vector3(0f, -2f, 33f));
            CreateRoofBattens(
                roofWest,
                "WestOuter",
                new Vector3(-2.26f, 3.48f, -0.08f),
                33f,
                4.98f,
                materials.TarnishedBrass);
            CreateBlock(
                roofWest,
                "WestInnerRoofPlane",
                new Vector3(-0.80f, 3.50f, -0.04f),
                new Vector3(1.82f, 0.18f, 5.05f),
                materials.Obsidian,
                new Vector3(0f, 1f, -34f));
            CreateRoofBattens(
                roofWest,
                "WestInner",
                new Vector3(-0.80f, 3.50f, -0.04f),
                -34f,
                4.92f,
                materials.TarnishedBrass);
            CreateBlock(
                roofWest,
                "WestRoofRidge",
                new Vector3(-1.53f, 4.02f, -0.06f),
                new Vector3(0.16f, 0.17f, 5.18f),
                materials.AshMortar,
                new Vector3(0f, -1f, 0f));
            CreateGableFrame(
                roofWest,
                "WestFrontGable",
                new Vector3(-1.53f, 0f, -2.62f),
                1.55f,
                2.82f,
                4.05f,
                materials);
            CreateSideCanopy(
                roofWest,
                "WestAubergineCanopy",
                new Vector3(-3.36f, 1.90f, -0.65f),
                -8f,
                materials);

            Transform roofEast = CreateGroup(
                root.transform,
                "RoofOcclusionEast");
            CreateBlock(
                roofEast,
                "EastInnerRoofPlane",
                new Vector3(0.76f, 3.35f, 0.16f),
                new Vector3(1.72f, 0.18f, 4.82f),
                materials.Obsidian,
                new Vector3(0f, -2f, 34f));
            CreateRoofBattens(
                roofEast,
                "EastInner",
                new Vector3(0.76f, 3.35f, 0.16f),
                34f,
                4.70f,
                materials.TarnishedBrass);
            CreateBlock(
                roofEast,
                "EastOuterRoofPlane",
                new Vector3(2.20f, 3.33f, 0.20f),
                new Vector3(1.80f, 0.18f, 4.88f),
                materials.Obsidian,
                new Vector3(0f, 2f, -33f));
            CreateRoofBattens(
                roofEast,
                "EastOuter",
                new Vector3(2.20f, 3.33f, 0.20f),
                -33f,
                4.76f,
                materials.TarnishedBrass);
            CreateBlock(
                roofEast,
                "EastRoofRidge",
                new Vector3(1.48f, 3.89f, 0.18f),
                new Vector3(0.16f, 0.17f, 4.96f),
                materials.AshMortar,
                new Vector3(0f, 1f, 0f));
            CreateGableFrame(
                roofEast,
                "EastFrontGable",
                new Vector3(1.48f, 0f, -2.48f),
                1.48f,
                2.78f,
                3.92f,
                materials);
            CreateSideCanopy(
                roofEast,
                "EastAubergineCanopy",
                new Vector3(3.32f, 1.82f, -0.35f),
                8f,
                materials);

            Transform fitout = CreateGroup(root.transform, "ReliquariesGrounded");
            Transform chimney = CreateGroup(root.transform, "WardChimney");
            CreateBlock(
                chimney,
                "OffsetChimneyBody",
                new Vector3(2.05f, 3.55f, 1.45f),
                new Vector3(0.62f, 1.92f, 0.66f),
                materials.GraphiteStone,
                new Vector3(0f, 4f, 0f));
            CreateBlock(
                chimney,
                "ChimneyIronBand",
                new Vector3(2.05f, 4.02f, 1.45f),
                new Vector3(0.72f, 0.16f, 0.76f),
                materials.TarnishedBrass,
                new Vector3(0f, 4f, 0f));
            CreateBlock(
                chimney,
                "ChimneyCap",
                new Vector3(2.05f, 4.45f, 1.45f),
                new Vector3(0.82f, 0.18f, 0.86f),
                materials.SmokedIron,
                new Vector3(0f, 4f, 0f));
            Renderer chimneyRenderer = CreateCylinder(
                chimney,
                "ChimneyWardInset",
                new Vector3(2.05f, 4.60f, 1.45f),
                new Vector3(0.24f, 0.08f, 0.24f),
                materials.Violet).GetComponent<Renderer>();
            Transform chimneyPoint = CreateGroup(chimney, "ChimneyConfirmPoint");
            chimneyPoint.localPosition = new Vector3(2.05f, 4.72f, 1.45f);

            Transform darkglassCore = CreateGroup(fitout, "DarkglassCore");
            darkglassCore.localPosition = new Vector3(0f, 0f, 0.12f);
            CreateCylinder(
                darkglassCore,
                "OuterSealingDais",
                new Vector3(0f, 0.40f, 0f),
                new Vector3(1.28f, 0.14f, 1.28f),
                materials.GraphiteStone);
            CreateCylinder(
                darkglassCore,
                "BrassSealingCollar",
                new Vector3(0f, 0.56f, 0f),
                new Vector3(1.06f, 0.08f, 1.06f),
                materials.TarnishedBrass);
            CreateCylinder(
                darkglassCore,
                "DarkglassTable",
                new Vector3(0f, 0.72f, 0f),
                new Vector3(0.80f, 0.14f, 0.80f),
                materials.Obsidian);
            Renderer coreRenderer = CreateSphere(
                darkglassCore,
                "GroundedCore",
                new Vector3(0f, 1.02f, 0f),
                new Vector3(0.36f, 0.28f, 0.36f),
                materials.Violet).GetComponent<Renderer>();
            Transform corePoint = CreateGroup(darkglassCore, "CorePoint");
            corePoint.localPosition = new Vector3(0f, 1.02f, 0f);
            Transform eclipseRing = CreateRing(
                darkglassCore,
                "EclipseRing",
                new Vector3(0f, 0.94f, 0f),
                1.04f,
                11,
                0.14f,
                materials.Violet);
            CreateBeam(
                eclipseRing,
                "EclipseDirectionNeedle",
                new Vector3(0f, 0f, 0.72f),
                new Vector3(0f, 0f, 1.16f),
                0.11f,
                materials.TarnishedBrass);

            CreateWorkstation(
                fitout,
                "WestReliquaryBench",
                new Vector3(-1.80f, 0f, 1.55f),
                materials);
            CreateWorkstation(
                fitout,
                "EastReliquaryBench",
                new Vector3(1.72f, 0f, 1.55f),
                materials);
            CreateBlock(
                fitout,
                "RearReliquaryShelfWest",
                new Vector3(-1.52f, 1.48f, 2.00f),
                new Vector3(1.35f, 1.28f, 0.20f),
                materials.AshTimber);
            CreateBlock(
                fitout,
                "RearReliquaryShelfEast",
                new Vector3(1.52f, 1.48f, 2.00f),
                new Vector3(1.35f, 1.28f, 0.20f),
                materials.AshTimber);
            CreateBlock(
                fitout,
                "RearAubergineDrape",
                new Vector3(0f, 2.20f, 1.98f),
                new Vector3(1.75f, 0.10f, 0.32f),
                materials.Aubergine,
                new Vector3(-10f, 0f, 0f));

            var routeRenderers = new List<Renderer>();
            Vector3 coreRoutePosition =
                darkglassCore.localPosition + corePoint.localPosition;
            for (int index = 0; index < anchorPositions.Length; index++)
            {
                Vector3 routeStart = anchorPositions[index];
                routeStart.y = 0.29f;
                Vector3 routeEnd = coreRoutePosition;
                routeEnd.y = 0.31f;
                routeRenderers.Add(
                    CreateBeam(
                        fitout,
                        $"GroundedWardRoute_{index:D2}",
                        routeStart,
                        routeEnd,
                        0.10f,
                        materials.Violet).GetComponent<Renderer>());
            }

            GameObject convergenceOrbObject = CreateSphere(
                fitout,
                "ConvergenceOrb",
                coreRoutePosition,
                new Vector3(0.38f, 0.38f, 0.38f),
                materials.Violet);
            Transform convergenceOrb = convergenceOrbObject.transform;
            Renderer convergenceOrbRenderer =
                convergenceOrbObject.GetComponent<Renderer>();
            var convergenceLight = convergenceOrbObject.AddComponent<Light>();
            convergenceLight.type = LightType.Point;
            convergenceLight.color = new Color(0.34f, 0.10f, 1f);
            convergenceLight.range = 3.8f;
            convergenceLight.intensity = 0f;
            convergenceLight.shadows = LightShadows.None;
            convergenceOrb.gameObject.SetActive(false);

            var activity =
                root.AddComponent<UmbralVeilwrightStableActivity>();
            activity.Configure(
                anchorPoints,
                corePoint,
                chimneyPoint,
                convergenceOrb,
                eclipseRing,
                anchorRenderers,
                routeRenderers.ToArray(),
                new[] { coreRenderer, convergenceOrbRenderer },
                new[] { chimneyRenderer },
                convergenceLight);

            var controller =
                root.AddComponent<ArchitectureConstructionAnimationController>();
            controller.Configure(
                profile,
                new[]
                {
                    new[] { boundary },
                    new[] { shell },
                    new[] { anchorFrames },
                    new[] { roofWest, roofEast },
                    new[] { fitout, chimney }
                },
                new MonoBehaviour[] { activity });
            controller.ConfigureCutawayGroups(
                new[] { frontFacadeOcclusion });
            controller.ConfigurePlayback(false, false, false);
            controller.SetPreviewTime(controller.PresentationDuration);

            return root;
        }

        private static Transform CreatePointedArch(
            Transform parent,
            string name,
            Vector3 localPosition,
            float halfWidth,
            float shoulderHeight,
            float apexHeight,
            float depth,
            Material material)
        {
            Transform arch = CreateGroup(parent, name);
            arch.localPosition = localPosition;
            float pierThickness = Mathf.Max(0.14f, halfWidth * 0.16f);

            CreateBlock(
                arch,
                "WestPier",
                new Vector3(-halfWidth, shoulderHeight * 0.5f, 0f),
                new Vector3(pierThickness, shoulderHeight, depth),
                material);
            CreateBlock(
                arch,
                "EastPier",
                new Vector3(halfWidth, shoulderHeight * 0.5f, 0f),
                new Vector3(pierThickness, shoulderHeight, depth),
                material);
            CreateBeam(
                arch,
                "WestPointedArch",
                new Vector3(-halfWidth, shoulderHeight, 0f),
                new Vector3(0f, apexHeight, 0f),
                pierThickness,
                material);
            CreateBeam(
                arch,
                "EastPointedArch",
                new Vector3(halfWidth, shoulderHeight, 0f),
                new Vector3(0f, apexHeight, 0f),
                pierThickness,
                material);
            CreateBlock(
                arch,
                "ApexFinial",
                new Vector3(0f, apexHeight + 0.15f, 0f),
                new Vector3(pierThickness * 0.72f, 0.34f, depth * 0.72f),
                material);
            return arch;
        }

        private static Transform CreateButtress(
            Transform parent,
            string name,
            Vector3 localPosition,
            MaterialSet materials)
        {
            Transform buttress = CreateGroup(parent, name);
            buttress.localPosition = localPosition;
            CreateBlock(
                buttress,
                "Foot",
                new Vector3(0f, 0.42f, 0f),
                new Vector3(0.62f, 0.74f, 0.62f),
                materials.GraphiteStone);
            CreateBlock(
                buttress,
                "Shaft",
                new Vector3(0f, 1.40f, 0f),
                new Vector3(0.42f, 1.65f, 0.44f),
                materials.GraphiteStone);
            CreateBlock(
                buttress,
                "BrassCap",
                new Vector3(0f, 2.30f, 0f),
                new Vector3(0.52f, 0.18f, 0.54f),
                materials.TarnishedBrass);
            CreateBlock(
                buttress,
                "Finial",
                new Vector3(0f, 2.58f, 0f),
                new Vector3(0.16f, 0.42f, 0.16f),
                materials.SmokedIron,
                new Vector3(0f, 0f, 45f));
            return buttress;
        }

        private static Transform CreateGableFrame(
            Transform parent,
            string name,
            Vector3 localPosition,
            float halfWidth,
            float shoulderHeight,
            float apexHeight,
            MaterialSet materials)
        {
            Transform gable = CreateGroup(parent, name);
            gable.localPosition = localPosition;
            CreateBeam(
                gable,
                "WestGableTrim",
                new Vector3(-halfWidth, shoulderHeight, 0f),
                new Vector3(0f, apexHeight, 0f),
                0.13f,
                materials.TarnishedBrass);
            CreateBeam(
                gable,
                "EastGableTrim",
                new Vector3(halfWidth, shoulderHeight, 0f),
                new Vector3(0f, apexHeight, 0f),
                0.13f,
                materials.TarnishedBrass);
            CreateBeam(
                gable,
                "CenterGableMullion",
                new Vector3(0f, shoulderHeight + 0.08f, 0f),
                new Vector3(0f, apexHeight - 0.14f, 0f),
                0.10f,
                materials.SmokedIron);
            CreateVerticalRing(
                gable,
                "GableOculus",
                new Vector3(0f, shoulderHeight + 0.42f, -0.02f),
                0.32f,
                10,
                0.07f,
                materials.TarnishedBrass);
            CreateBlock(
                gable,
                "GableFinial",
                new Vector3(0f, apexHeight + 0.20f, 0f),
                new Vector3(0.14f, 0.42f, 0.14f),
                materials.TarnishedBrass,
                new Vector3(0f, 0f, 45f));
            return gable;
        }

        private static void CreateRoofBattens(
            Transform parent,
            string namePrefix,
            Vector3 planeCenter,
            float zRotation,
            float roofLength,
            Material material)
        {
            float radians = zRotation * Mathf.Deg2Rad;
            Vector3 localAcross = new Vector3(
                Mathf.Cos(radians),
                Mathf.Sin(radians),
                0f);
            float[] offsets = { -0.52f, 0f, 0.52f };

            for (int index = 0; index < offsets.Length; index++)
            {
                CreateBlock(
                    parent,
                    $"{namePrefix}RoofBatten_{index:D2}",
                    planeCenter +
                        localAcross * offsets[index] +
                        new Vector3(0f, 0.075f, 0f),
                    new Vector3(0.065f, 0.065f, roofLength),
                    material,
                    new Vector3(0f, 0f, zRotation));
            }
        }

        private static Transform CreateSideCanopy(
            Transform parent,
            string name,
            Vector3 localPosition,
            float zRotation,
            MaterialSet materials)
        {
            Transform canopy = CreateGroup(parent, name);
            canopy.localPosition = localPosition;
            CreateBlock(
                canopy,
                "AubergineCloth",
                Vector3.zero,
                new Vector3(1.32f, 0.12f, 2.25f),
                materials.Aubergine,
                new Vector3(0f, 0f, zRotation));
            float postDirection = Mathf.Sign(zRotation);
            for (int index = 0; index < 2; index++)
            {
                CreateBlock(
                    canopy,
                    $"CanopyPost_{index:D2}",
                    new Vector3(
                        postDirection * 0.50f,
                        -0.78f,
                        index == 0 ? -0.86f : 0.86f),
                    new Vector3(0.10f, 1.58f, 0.10f),
                    materials.TarnishedBrass);
            }

            return canopy;
        }

        private static void CreateWorkstation(
            Transform parent,
            string name,
            Vector3 localPosition,
            MaterialSet materials)
        {
            Transform workstation = CreateGroup(parent, name);
            workstation.localPosition = localPosition;
            CreateBlock(
                workstation,
                "BenchTop",
                new Vector3(0f, 0.72f, 0f),
                new Vector3(1.12f, 0.16f, 0.62f),
                materials.AshTimber);
            CreateBlock(
                workstation,
                "BenchBase",
                new Vector3(0f, 0.38f, 0f),
                new Vector3(0.88f, 0.52f, 0.48f),
                materials.GraphiteStone);
            CreateCylinder(
                workstation,
                "GroundedReliquary",
                new Vector3(0.18f, 0.94f, 0f),
                new Vector3(0.18f, 0.23f, 0.18f),
                materials.Obsidian);
            CreateBlock(
                workstation,
                "IronClamp",
                new Vector3(-0.28f, 0.92f, 0f),
                new Vector3(0.18f, 0.30f, 0.18f),
                materials.SmokedIron);
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
            camera.orthographicSize = 4.85f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.018f, 0.028f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(-6.4f, 5.7f, -10.8f);
            camera.transform.LookAt(new Vector3(0f, 1.70f, -0.05f));

            var keyLightObject = new GameObject("CoolKeyLight");
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.63f, 0.70f, 1f);
            keyLight.intensity = 1.72f;
            keyLight.shadows = LightShadows.None;
            keyLightObject.transform.rotation = Quaternion.Euler(44f, -38f, 0f);

            var warmFillObject = new GameObject("WarmEdgeFill");
            Light warmFill = warmFillObject.AddComponent<Light>();
            warmFill.type = LightType.Directional;
            warmFill.color = new Color(1f, 0.58f, 0.36f);
            warmFill.intensity = 0.42f;
            warmFill.shadows = LightShadows.None;
            warmFillObject.transform.rotation = Quaternion.Euler(25f, 138f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.145f, 0.19f);
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Transform CreateVerticalRing(
            Transform parent,
            string name,
            Vector3 localPosition,
            float radius,
            int segmentCount,
            float thickness,
            Material material)
        {
            Transform ring = CreateGroup(parent, name);
            ring.localPosition = localPosition;

            for (int index = 0; index < segmentCount; index++)
            {
                float firstAngle =
                    index / (float)segmentCount * Mathf.PI * 2f;
                float secondAngle =
                    (index + 1) / (float)segmentCount * Mathf.PI * 2f;
                Vector3 start = new Vector3(
                    Mathf.Cos(firstAngle) * radius,
                    Mathf.Sin(firstAngle) * radius,
                    0f);
                Vector3 end = new Vector3(
                    Mathf.Cos(secondAngle) * radius,
                    Mathf.Sin(secondAngle) * radius,
                    0f);
                CreateBeam(
                    ring,
                    $"OculusSegment_{index:D2}",
                    start,
                    end,
                    thickness,
                    material);
            }

            return ring;
        }

        private static Transform CreateRing(
            Transform parent,
            string name,
            Vector3 localPosition,
            float radius,
            int segmentCount,
            float thickness,
            Material material)
        {
            Transform ring = CreateGroup(parent, name);
            ring.localPosition = localPosition;

            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);
                CreateBlock(
                    ring,
                    $"RingSegment_{index:D2}",
                    position,
                    new Vector3(thickness, 0.055f, radius * 0.46f),
                    material,
                    new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
            }

            return ring;
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
            public Material GraphiteStone;
            public Material AshMortar;
            public Material SmokedIron;
            public Material TarnishedBrass;
            public Material Obsidian;
            public Material AshTimber;
            public Material Aubergine;
            public Material Violet;
            public Material Ground;
        }
    }
}
