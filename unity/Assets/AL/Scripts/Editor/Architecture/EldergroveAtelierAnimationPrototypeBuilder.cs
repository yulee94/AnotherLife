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

namespace AL.Editor.Architecture
{
    public static class EldergroveAtelierAnimationPrototypeBuilder
    {
        public const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/" +
            "Eldergrove_Atelier_AnimationPrototype.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "EldergroveAtelierAnimationPrototype.unity";
        private const string ProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Eldergrove_Atelier_ConstructionProfile.asset";
        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Materials";

        [MenuItem("Another Life/Architecture/Build Eldergrove Atelier Animation Prototype")]
        public static void Build()
        {
            EnsureFolders();
            MaterialSet materials = CreateMaterials();
            ArchitectureConstructionAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<ArchitectureConstructionAnimationProfile>(
                    ProfilePath);
            if (profile == null || !profile.IsConfigured)
            {
                throw new InvalidOperationException(
                    "The approved Eldergrove construction profile is missing or invalid.");
            }

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
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        public static void RenderStillFromCommandLine()
        {
            OpenPreview(
                out Camera camera,
                out ArchitectureConstructionAnimationController controller);
            controller.SetPreviewTime(16f);
            WriteFrame(
                camera,
                768,
                768,
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    ".omx",
                    "state",
                    "eldergrove-graybox-preview.png"));
        }

        public static void RenderProcessSheetFromCommandLine()
        {
            OpenPreview(
                out Camera camera,
                out ArchitectureConstructionAnimationController controller);
            const int frameWidth = 512;
            const int frameHeight = 384;
            var target = new RenderTexture(frameWidth, frameHeight, 24);
            var frame = new Texture2D(
                frameWidth,
                frameHeight,
                TextureFormat.RGB24,
                false);
            var sheet = new Texture2D(
                frameWidth * 3,
                frameHeight * 2,
                TextureFormat.RGB24,
                false);

            try
            {
                camera.targetTexture = target;
                for (int stateIndex = 0; stateIndex < 6; stateIndex++)
                {
                    if (stateIndex < 5)
                    {
                        controller.SetConstructionState(
                            (ArchitectureConstructionState)stateIndex);
                    }
                    else
                    {
                        controller.SetPreviewTime(10.2f);
                    }

                    camera.Render();
                    RenderTexture.active = target;
                    frame.ReadPixels(
                        new Rect(0f, 0f, frameWidth, frameHeight),
                        0,
                        0);
                    frame.Apply();
                    int column = stateIndex % 3;
                    int row = 1 - stateIndex / 3;
                    sheet.SetPixels(
                        column * frameWidth,
                        row * frameHeight,
                        frameWidth,
                        frameHeight,
                        frame.GetPixels());
                }

                sheet.Apply();
                string outputPath = Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "..",
                        "..",
                        ".omx",
                        "state",
                        "eldergrove-graybox",
                        "process-sheet.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(sheet);
                Object.DestroyImmediate(frame);
                Object.DestroyImmediate(target);
            }
        }

        private static GameObject BuildPrototype(
            MaterialSet materials,
            ArchitectureConstructionAnimationProfile profile)
        {
            var root = new GameObject("Eldergrove_Atelier_AnimationPrototype");

            Transform plot = Group(root.transform, "PlotPrepared");
            Block(plot, "DrainedFootprint", new Vector3(0f, 0.12f, 0f),
                new Vector3(6.3f, 0.24f, 4.9f), materials.PaleStone);
            Block(plot, "EntranceStep", new Vector3(0f, 0.18f, -2.65f),
                new Vector3(1.9f, 0.18f, 0.72f), materials.PaleStone);
            Block(plot, "DrainageChannel", new Vector3(0f, 0.255f, 0.9f),
                new Vector3(4.6f, 0.035f, 0.16f), materials.Bronze);
            Vector3[] socketPositions =
            {
                new Vector3(-2.25f, 0.33f, -1.1f),
                new Vector3(2.25f, 0.33f, -1.1f),
                new Vector3(-2.25f, 0.33f, 1.25f),
                new Vector3(2.25f, 0.33f, 1.25f)
            };
            for (int index = 0; index < socketPositions.Length; index++)
            {
                Cylinder(
                    plot,
                    $"BronzeRootSocket_{index:D2}",
                    socketPositions[index],
                    new Vector3(0.42f, 0.11f, 0.42f),
                    materials.Bronze);
            }
            Block(plot, "TimberSupply", new Vector3(2.25f, 0.34f, -2.0f),
                new Vector3(1.25f, 0.22f, 0.52f), materials.Timber);

            Transform frame = Group(root.transform, "CraftFrameSet");
            Block(frame, "StonePlinth", new Vector3(0f, 0.47f, 0f),
                new Vector3(5.7f, 0.62f, 4.25f), materials.PaleStone);
            Block(frame, "BackMasonry", new Vector3(0f, 1.38f, 1.85f),
                new Vector3(5.15f, 1.45f, 0.42f), materials.PaleStone);
            Block(frame, "WestMasonry", new Vector3(-2.38f, 1.28f, 0.35f),
                new Vector3(0.48f, 1.3f, 2.65f), materials.PaleStone);
            Block(frame, "EastMasonry", new Vector3(2.38f, 1.28f, 0.35f),
                new Vector3(0.48f, 1.3f, 2.65f), materials.PaleStone);
            Transform guideFrame = Group(frame, "TemporaryGuideFrame");
            CreateGuideFrame(guideFrame, materials.Timber);

            Transform guidedRoots = Group(root.transform, "GuidedRootGrowth");
            Vector3[] leftLower =
            {
                new Vector3(-2.3f, 0.42f, -1.35f),
                new Vector3(-2.27f, 1.18f, -1.35f),
                new Vector3(-1.98f, 1.92f, -1.32f),
                new Vector3(-1.52f, 2.56f, -1.28f)
            };
            Vector3[] rightLower =
            {
                new Vector3(2.3f, 0.42f, -1.35f),
                new Vector3(2.27f, 1.18f, -1.35f),
                new Vector3(1.98f, 1.92f, -1.32f),
                new Vector3(1.52f, 2.56f, -1.28f)
            };
            CreateRootPath(
                guidedRoots,
                "RootGrowth_Left",
                leftLower,
                0.38f,
                materials.Bark);
            CreateRootPath(
                guidedRoots,
                "RootGrowth_Right",
                rightLower,
                0.38f,
                materials.Bark);
            CreateRootBase(guidedRoots, "RootBase_Left", leftLower[0], materials.Bark);
            CreateRootBase(guidedRoots, "RootBase_Right", rightLower[0], materials.Bark);

            Transform settledRoots = Group(root.transform, "RootVaultSettled");
            Vector3[] leftUpper =
            {
                leftLower[leftLower.Length - 1],
                new Vector3(-1.02f, 3.03f, -1.25f),
                new Vector3(-0.46f, 3.34f, -1.22f),
                new Vector3(0f, 3.42f, -1.2f)
            };
            Vector3[] rightUpper =
            {
                rightLower[rightLower.Length - 1],
                new Vector3(1.02f, 3.03f, -1.25f),
                new Vector3(0.46f, 3.34f, -1.22f),
                new Vector3(0f, 3.42f, -1.2f)
            };
            CreateRootPath(
                settledRoots,
                "RootSettled_Left",
                leftUpper,
                0.34f,
                materials.Bark);
            CreateRootPath(
                settledRoots,
                "RootSettled_Right",
                rightUpper,
                0.34f,
                materials.Bark);
            Cylinder(settledRoots, "BronzeGraftLeft",
                new Vector3(-1.52f, 2.56f, -1.28f),
                new Vector3(0.46f, 0.14f, 0.46f), materials.Bronze);
            Cylinder(settledRoots, "BronzeGraftRight",
                new Vector3(1.52f, 2.56f, -1.28f),
                new Vector3(0.46f, 0.14f, 0.46f), materials.Bronze);
            Block(settledRoots, "VaultJoin",
                new Vector3(0f, 3.42f, -1.2f),
                new Vector3(0.88f, 0.24f, 0.52f), materials.Bronze);

            Transform roof = Group(root.transform, "RoofAndLanternSet");
            Transform roofWest = Group(roof, "RoofWestOcclusion");
            Block(roofWest, "RoofWest", new Vector3(-1.32f, 3.48f, 0.28f),
                new Vector3(3.05f, 0.22f, 4.35f), materials.LeafRoof,
                Quaternion.Euler(0f, 0f, 19f));
            Transform roofEast = Group(roof, "RoofEastOcclusion");
            Block(roofEast, "RoofEast", new Vector3(1.32f, 3.48f, 0.28f),
                new Vector3(3.05f, 0.22f, 4.35f), materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -19f));
            AddRoofRibs(roofWest, "West", -1.32f, 19f, materials.Timber);
            AddRoofRibs(roofEast, "East", 1.32f, -19f, materials.Timber);
            Block(roofWest, "WestOuterEave",
                new Vector3(-2.73f, 3.0f, 0.28f),
                new Vector3(0.14f, 0.16f, 4.62f), materials.Timber);
            Block(roofEast, "EastOuterEave",
                new Vector3(2.73f, 3.0f, 0.28f),
                new Vector3(0.14f, 0.16f, 4.62f), materials.Timber);
            Transform lantern = Group(roof, "LanternOcclusion");
            Cylinder(lantern, "LanternBase", new Vector3(0f, 3.98f, 0.28f),
                new Vector3(0.76f, 0.12f, 0.76f), materials.Bronze);
            CreateLanternPosts(lantern, materials.Timber);
            Block(lantern, "LanternCapWest",
                new Vector3(-0.35f, 4.48f, 0.28f),
                new Vector3(0.92f, 0.14f, 1.38f), materials.LeafRoof,
                Quaternion.Euler(0f, 0f, 18f));
            Block(lantern, "LanternCapEast",
                new Vector3(0.35f, 4.48f, 0.28f),
                new Vector3(0.92f, 0.14f, 1.38f), materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -18f));
            Cylinder(lantern, "LanternFinial", new Vector3(0f, 4.78f, 0.28f),
                new Vector3(0.11f, 0.24f, 0.11f), materials.Bronze);

            Transform fitout = Group(root.transform, "CultivationOperational");
            Cylinder(fitout, "CultivationBasin", new Vector3(0f, 0.72f, 0.25f),
                new Vector3(1.18f, 0.3f, 1.18f), materials.Bronze);
            Renderer core = Cylinder(
                fitout,
                "CultivationCore",
                new Vector3(0f, 0.92f, 0.25f),
                new Vector3(0.82f, 0.08f, 0.82f),
                materials.Sap);
            Transform ripple = Cylinder(
                fitout,
                "WaterRipple",
                new Vector3(0f, 1.025f, 0.25f),
                new Vector3(0.64f, 0.018f, 0.64f),
                materials.Water).transform;
            Block(fitout, "WestWorkbench", new Vector3(-1.55f, 0.78f, 0.95f),
                new Vector3(1.45f, 0.22f, 0.68f), materials.Timber);
            Block(fitout, "EastPlanter", new Vector3(1.55f, 0.72f, 0.98f),
                new Vector3(1.25f, 0.42f, 0.72f), materials.PaleStone);
            Transform leaf = Group(fitout, "ProtectedLeaf");
            Block(leaf, "LeafBlade", new Vector3(1.45f, 1.18f, 0.88f),
                new Vector3(0.18f, 0.55f, 0.38f), materials.Leaf);

            var sapRenderers = new List<Renderer> { core };
            sapRenderers.AddRange(CreateSapPath(
                fitout,
                new[]
                {
                    new Vector3(-1.52f, 2.56f, -1.26f),
                    new Vector3(-0.85f, 2.25f, -0.72f),
                    new Vector3(-0.25f, 1.45f, 0.2f),
                    new Vector3(0f, 1.02f, 0.25f)
                },
                materials.Sap));

            var lightObject = new GameObject("LocalizedCultivationLight");
            lightObject.transform.SetParent(fitout, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.35f, 0.25f);
            Light cultivationLight = lightObject.AddComponent<Light>();
            cultivationLight.type = LightType.Point;
            cultivationLight.color = new Color(0.35f, 1f, 0.24f);
            cultivationLight.range = 3.1f;
            cultivationLight.intensity = 0f;
            cultivationLight.shadows = LightShadows.None;

            var activity = root.AddComponent<EldergroveAtelierStableActivity>();
            activity.Configure(
                sapRenderers.ToArray(),
                fitout,
                profile.StageDuration * 4f,
                guideFrame,
                profile.StageDuration,
                profile.StageDuration * 3f,
                ripple,
                leaf,
                cultivationLight);

            var controller =
                root.AddComponent<ArchitectureConstructionAnimationController>();
            controller.Configure(
                profile,
                new[]
                {
                    new[] { plot },
                    new[] { frame },
                    new[] { guidedRoots },
                    new[] { settledRoots },
                    new[] { roofWest, roofEast, lantern }
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
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.GetComponent<ArchitectureConstructionAnimationController>()
                .ConfigurePlayback(true, true, false);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "PrototypeGround";
            ground.transform.localScale = new Vector3(1.35f, 1f, 1.35f);
            ConfigureRenderer(ground.GetComponent<Renderer>(), materials.Ground);
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.014f, 0.026f, 0.018f);
            camera.transform.position = new Vector3(6.3f, 5.35f, -10.8f);
            camera.transform.LookAt(new Vector3(0f, 1.72f, -0.2f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.82f, 1f, 0.72f);
            key.intensity = 1.42f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(44f, -34f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.20f, 0.14f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateGuideFrame(Transform parent, Material material)
        {
            Vector3[] posts =
            {
                new Vector3(-2.05f, 1.65f, -1.45f),
                new Vector3(2.05f, 1.65f, -1.45f),
                new Vector3(-2.05f, 1.65f, 1.35f),
                new Vector3(2.05f, 1.65f, 1.35f)
            };
            for (int index = 0; index < posts.Length; index++)
            {
                Block(parent, $"GuidePost_{index:D2}", posts[index],
                    new Vector3(0.16f, 2.05f, 0.16f), material);
            }
            Beam(parent, "GuideBeamFront",
                posts[0] + Vector3.up, posts[1] + Vector3.up, 0.09f, material);
            Beam(parent, "GuideBeamRear",
                posts[2] + Vector3.up, posts[3] + Vector3.up, 0.09f, material);
        }

        private static void CreateLanternPosts(
            Transform parent,
            Material material)
        {
            Vector3[] positions =
            {
                new Vector3(-0.42f, 4.22f, -0.12f),
                new Vector3(0.42f, 4.22f, -0.12f),
                new Vector3(-0.42f, 4.22f, 0.68f),
                new Vector3(0.42f, 4.22f, 0.68f)
            };
            for (int index = 0; index < positions.Length; index++)
            {
                Block(
                    parent,
                    $"LanternPost_{index:D2}",
                    positions[index],
                    new Vector3(0.1f, 0.48f, 0.1f),
                    material);
            }
        }

        private static void CreateRootBase(
            Transform parent,
            string name,
            Vector3 center,
            Material material)
        {
            Transform root = Group(parent, name);
            for (int index = 0; index < 4; index++)
            {
                float angle = index * 90f * Mathf.Deg2Rad;
                Vector3 end = center + new Vector3(
                    Mathf.Cos(angle) * 0.65f,
                    -0.08f,
                    Mathf.Sin(angle) * 0.65f);
                Beam(root, $"GroundedTendril_{index:D2}", center, end, 0.12f, material);
            }
        }

        private static void CreateRootPath(
            Transform parent,
            string name,
            Vector3[] points,
            float radius,
            Material material)
        {
            Transform path = Group(parent, name);
            for (int index = 0; index < points.Length - 1; index++)
            {
                Beam(
                    path,
                    $"AuthoredSegment_{index:D2}",
                    points[index],
                    points[index + 1],
                    Mathf.Max(0.12f, radius - index * 0.025f),
                    material);
            }
        }

        private static IEnumerable<Renderer> CreateSapPath(
            Transform parent,
            Vector3[] points,
            Material material)
        {
            Transform path = Group(parent, "AuthoredSapPath");
            var renderers = new List<Renderer>();
            for (int index = 0; index < points.Length - 1; index++)
            {
                renderers.Add(Beam(
                    path,
                    $"SapSegment_{index:D2}",
                    points[index],
                    points[index + 1],
                    0.045f,
                    material));
            }
            return renderers;
        }

        private static void AddRoofRibs(
            Transform parent,
            string side,
            float centerX,
            float pitch,
            Material material)
        {
            for (int index = 0; index < 5; index++)
            {
                float z = Mathf.Lerp(-1.75f, 1.75f, index / 4f);
                Block(
                    parent,
                    $"{side}RoofRib_{index:D2}",
                    new Vector3(centerX, 3.55f, z),
                    new Vector3(3.12f, 0.08f, 0.11f),
                    material,
                    Quaternion.Euler(0f, 0f, pitch));
            }
        }

        private static Renderer Beam(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float radius,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = (start + end) * 0.5f;
            beam.transform.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                direction.normalized);
            beam.transform.localScale =
                new Vector3(radius, direction.magnitude * 0.5f, radius);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            Renderer renderer = beam.GetComponent<Renderer>();
            ConfigureRenderer(renderer, material);
            return renderer;
        }

        private static Renderer Block(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion? rotation = null)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.transform.localRotation = rotation ?? Quaternion.identity;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            Renderer renderer = block.GetComponent<Renderer>();
            ConfigureRenderer(renderer, material);
            return renderer;
        }

        private static Renderer Cylinder(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject cylinder =
                GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localScale = scale;
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            Renderer renderer = cylinder.GetComponent<Renderer>();
            ConfigureRenderer(renderer, material);
            return renderer;
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void ConfigureRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void OpenPreview(
            out Camera camera,
            out ArchitectureConstructionAnimationController controller)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            camera = Object.FindObjectOfType<Camera>();
            GameObject prototype =
                GameObject.Find("Eldergrove_Atelier_AnimationPrototype");
            if (camera == null || prototype == null)
            {
                throw new InvalidOperationException(
                    "Prototype camera or atelier is missing.");
            }
            controller =
                prototype.GetComponent<ArchitectureConstructionAnimationController>();
        }

        private static void WriteFrame(
            Camera camera,
            int width,
            int height,
            string path)
        {
            var target = new RenderTexture(width, height, 24);
            var capture = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                capture.Apply();
                string outputPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(target);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/AL/Art/Generated/Architecture", "Eldergrove");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Eldergrove",
                "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static MaterialSet CreateMaterials()
        {
            return new MaterialSet
            {
                PaleStone = Material(
                    "PaleStone", new Color(0.34f, 0.35f, 0.29f), 0.03f),
                Bark = Material(
                    "RootBark", new Color(0.12f, 0.075f, 0.038f), 0.01f),
                Timber = Material(
                    "DarkTimber", new Color(0.18f, 0.10f, 0.045f), 0.01f),
                Bronze = Material(
                    "WeatheredBronze", new Color(0.28f, 0.19f, 0.055f), 0.58f),
                LeafRoof = Material(
                    "LeafRoof", new Color(0.095f, 0.15f, 0.055f), 0.01f),
                Leaf = Material(
                    "LivingLeaf", new Color(0.16f, 0.32f, 0.075f), 0.01f),
                Sap = Material(
                    "LivingSap",
                    new Color(0.18f, 0.48f, 0.07f),
                    0f,
                    new Color(0.12f, 0.7f, 0.05f)),
                Water = Material(
                    "BasinWater", new Color(0.08f, 0.28f, 0.22f), 0.05f),
                Ground = Material(
                    "Ground", new Color(0.045f, 0.07f, 0.04f), 0f)
            };
        }

        private static Material Material(
            string name,
            Color color,
            float metallic,
            Color? emission = null)
        {
            string path = $"{MaterialFolder}/MAT_Eldergrove_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard") ??
                    Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.enableInstancing = true;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private sealed class MaterialSet
        {
            public Material PaleStone;
            public Material Bark;
            public Material Timber;
            public Material Bronze;
            public Material LeafRoof;
            public Material Leaf;
            public Material Sap;
            public Material Water;
            public Material Ground;
        }
    }
}
