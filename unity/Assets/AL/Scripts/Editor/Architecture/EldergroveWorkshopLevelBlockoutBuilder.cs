using System;
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
    /// <summary>
    /// Creates review-only static blockouts for the owner-approved Eldergrove
    /// Workshop level progression. These assets prove cumulative form and
    /// mobile hierarchy only; they are not live kingdom prefabs.
    /// </summary>
    public static class EldergroveWorkshopLevelBlockoutBuilder
    {
        public const string Level01PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "Eldergrove_Workshop_Level01_Blockout.prefab";
        public const string Level06PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "Eldergrove_Workshop_Level06_Blockout.prefab";
        public const string Level10PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "Eldergrove_Workshop_Level10_Blockout.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "EldergroveWorkshopLevelBlockout.unity";

        private const string SourcePrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/" +
            "Eldergrove_Atelier_AnimationPrototype.prefab";
        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Materials";

        [MenuItem("Another Life/Architecture/Build Eldergrove Workshop Level Blockouts")]
        public static void Build()
        {
            EnsureFolders();
            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    "The approved Eldergrove animation prototype is missing.");
            }

            MaterialSet materials = LoadMaterials();
            BuildAndSave(source, materials, 1, Level01PrefabPath);
            BuildAndSave(source, materials, 6, Level06PrefabPath);
            BuildAndSave(source, materials, 10, Level10PrefabPath);
            CreatePreviewScene(materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        public static void RenderFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "The Eldergrove level review camera is missing.");
            }

            WriteFrame(
                camera,
                1536,
                768,
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    ".omx",
                    "state",
                    "eldergrove-workshop-level-blockout",
                    "render.png"));
        }

        private static void BuildAndSave(
            GameObject source,
            MaterialSet materials,
            int level,
            string path)
        {
            GameObject blockout = CreateStableLevelOne(source, level);
            try
            {
                if (level >= 2)
                {
                    AddLevel02(blockout.transform, materials);
                }
                if (level >= 3)
                {
                    AddLevel03(blockout.transform, materials);
                }
                if (level >= 4)
                {
                    AddLevel04(blockout.transform, materials);
                }
                if (level >= 5)
                {
                    AddLevel05(blockout.transform, materials);
                }
                if (level >= 6)
                {
                    AddLevel06(blockout.transform, materials);
                }
                if (level >= 7)
                {
                    AddLevel07(blockout.transform, materials);
                }
                if (level >= 8)
                {
                    AddLevel08(blockout.transform, materials);
                }
                if (level >= 9)
                {
                    AddLevel09(blockout.transform, materials);
                }
                if (level >= 10)
                {
                    AddLevel10(blockout.transform, materials);
                }

                PrefabUtility.SaveAsPrefabAsset(blockout, path);
            }
            finally
            {
                Object.DestroyImmediate(blockout);
            }
        }

        private static GameObject CreateStableLevelOne(
            GameObject source,
            int level)
        {
            GameObject root = Object.Instantiate(source);
            root.name = $"Eldergrove_Workshop_Level{level:D2}_Blockout";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            ArchitectureConstructionAnimationController controller =
                root.GetComponent<ArchitectureConstructionAnimationController>();
            if (controller == null)
            {
                throw new InvalidOperationException(
                    "The Eldergrove source controller is missing.");
            }
            controller.SetPreviewTime(controller.PresentationDuration);

            EldergroveAtelierStableActivity activity =
                root.GetComponent<EldergroveAtelierStableActivity>();
            if (activity != null)
            {
                Object.DestroyImmediate(activity);
            }
            Object.DestroyImmediate(controller);

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(light.gameObject);
            }

            DestroyChild(
                root.transform,
                "CraftFrameSet/TemporaryGuideFrame");
            DestroyChild(root.transform, "PlotPrepared/TimberSupply");
            DestroyChild(
                root.transform,
                "RoofAndLanternSet/LanternOcclusion");

            Transform foundational = Group(root.transform, "L01_Foundational");
            string[] sourceGroups =
            {
                "PlotPrepared",
                "CraftFrameSet",
                "GuidedRootGrowth",
                "RootVaultSettled",
                "RoofAndLanternSet",
                "CultivationOperational"
            };
            foreach (string sourceGroup in sourceGroups)
            {
                Transform child = root.transform.Find(sourceGroup);
                if (child == null)
                {
                    throw new InvalidOperationException(
                        $"The Eldergrove source group {sourceGroup} is missing.");
                }
                child.SetParent(foundational, false);
                child.gameObject.SetActive(true);
            }

            return root;
        }

        private static void AddLevel02(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L02_Reinforced");
            Beam(
                delta,
                "L02_RootBraceWest",
                new Vector3(-2.7f, 0.42f, 0.9f),
                new Vector3(-2.48f, 3.02f, 0.62f),
                0.22f,
                materials.Bark);
            Beam(
                delta,
                "L02_RootBraceEast",
                new Vector3(2.7f, 0.42f, 0.9f),
                new Vector3(2.48f, 3.02f, 0.62f),
                0.22f,
                materials.Bark);
            Block(
                delta,
                "L02_WeatherShieldWest",
                new Vector3(-2.7f, 2.82f, -0.35f),
                new Vector3(0.9f, 0.14f, 3.1f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, 18f));
            Block(
                delta,
                "L02_WeatherShieldEast",
                new Vector3(2.7f, 2.82f, -0.35f),
                new Vector3(0.9f, 0.14f, 3.1f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -18f));
            Block(
                delta,
                "L02_DrainageEdge",
                new Vector3(0f, 0.32f, 2.35f),
                new Vector3(5.9f, 0.14f, 0.22f),
                materials.PaleStone);
        }

        private static void AddLevel03(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L03_Expanded");
            Block(
                delta,
                "L03_AnnexPlinth",
                new Vector3(-3.35f, 0.42f, 0.55f),
                new Vector3(1.45f, 0.58f, 2.8f),
                materials.PaleStone);
            Block(
                delta,
                "L03_AnnexBack",
                new Vector3(-3.35f, 1.45f, 1.72f),
                new Vector3(1.35f, 1.55f, 0.28f),
                materials.Timber);
            Block(
                delta,
                "L03_AnnexSide",
                new Vector3(-3.92f, 1.42f, 0.55f),
                new Vector3(0.24f, 1.5f, 2.25f),
                materials.Timber);
            Block(
                delta,
                "L03_AnnexRoof",
                new Vector3(-3.32f, 2.35f, 0.52f),
                new Vector3(1.7f, 0.18f, 2.85f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -9f));
            Block(
                delta,
                "L03_StorageBay",
                new Vector3(-3.3f, 0.92f, 0.6f),
                new Vector3(0.82f, 0.72f, 1.2f),
                materials.Bronze);
        }

        private static void AddLevel04(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L04_Established");
            Cylinder(
                delta,
                "L04_VentBase",
                new Vector3(0f, 4.03f, 0.28f),
                new Vector3(0.58f, 0.14f, 0.58f),
                materials.Bronze);
            for (int index = 0; index < 4; index++)
            {
                float angle = index * Mathf.PI * 0.5f;
                Block(
                    delta,
                    $"L04_VentPost_{index:D2}",
                    new Vector3(
                        Mathf.Cos(angle) * 0.38f,
                        4.35f,
                        0.28f + Mathf.Sin(angle) * 0.38f),
                    new Vector3(0.1f, 0.55f, 0.1f),
                    materials.Timber);
            }
            Block(
                delta,
                "L04_VentCapWest",
                new Vector3(-0.32f, 4.72f, 0.28f),
                new Vector3(0.82f, 0.14f, 1.2f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, 18f));
            Block(
                delta,
                "L04_VentCapEast",
                new Vector3(0.32f, 4.72f, 0.28f),
                new Vector3(0.82f, 0.14f, 1.2f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -18f));
            Block(
                delta,
                "L04_RoofRidge",
                new Vector3(0f, 3.96f, 0.32f),
                new Vector3(0.18f, 0.16f, 4.45f),
                materials.Bronze);
        }

        private static void AddLevel05(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L05_DistrictAnchor");
            Block(
                delta,
                "L05_PublicWorkbench",
                new Vector3(2.1f, 0.72f, -2.15f),
                new Vector3(2.15f, 0.28f, 0.8f),
                materials.Timber);
            Block(
                delta,
                "L05_WorkbenchPlinth",
                new Vector3(2.1f, 0.34f, -2.15f),
                new Vector3(2.45f, 0.22f, 1.1f),
                materials.PaleStone);
            Block(
                delta,
                "L05_ApproachCanopy",
                new Vector3(2.1f, 2.1f, -2.05f),
                new Vector3(2.55f, 0.14f, 1.35f),
                materials.LeafRoof,
                Quaternion.Euler(7f, 0f, 0f));
            Block(
                delta,
                "L05_CanopyPostWest",
                new Vector3(1.15f, 1.25f, -2.2f),
                new Vector3(0.14f, 1.6f, 0.14f),
                materials.Timber);
            Block(
                delta,
                "L05_CanopyPostEast",
                new Vector3(3.05f, 1.25f, -2.2f),
                new Vector3(0.14f, 1.6f, 0.14f),
                materials.Timber);
        }

        private static void AddLevel06(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L06_Advanced");
            Block(
                delta,
                "L06_UpperGraftBay",
                new Vector3(2.72f, 2.08f, 1.22f),
                new Vector3(1.42f, 1.6f, 1.65f),
                materials.Timber);
            Beam(
                delta,
                "L06_UpperRootLock",
                new Vector3(2.18f, 1.1f, 1.18f),
                new Vector3(2.72f, 3.15f, 1.18f),
                0.2f,
                materials.Bark);
            Block(
                delta,
                "L06_SecondaryRoofWest",
                new Vector3(2.38f, 3.08f, 1.22f),
                new Vector3(1.1f, 0.16f, 1.95f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, 16f));
            Block(
                delta,
                "L06_SecondaryRoofEast",
                new Vector3(3.05f, 3.08f, 1.22f),
                new Vector3(1.1f, 0.16f, 1.95f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -16f));
            Cylinder(
                delta,
                "L06_GraftCollar",
                new Vector3(2.72f, 2.45f, 0.37f),
                new Vector3(0.38f, 0.14f, 0.38f),
                materials.Bronze);
        }

        private static void AddLevel07(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L07_Signature");
            Beam(
                delta,
                "L07_GrowthFrameWest",
                new Vector3(-3.75f, 0.58f, -1.15f),
                new Vector3(-2.45f, 3.38f, -0.55f),
                0.24f,
                materials.Bark);
            Beam(
                delta,
                "L07_GrowthFrameEast",
                new Vector3(3.72f, 0.58f, -1.15f),
                new Vector3(2.45f, 3.38f, -0.55f),
                0.24f,
                materials.Bark);
            Beam(
                delta,
                "L07_GrowthFrameCrown",
                new Vector3(-2.45f, 3.38f, -0.55f),
                new Vector3(2.45f, 3.38f, -0.55f),
                0.19f,
                materials.Bark);
            Cylinder(
                delta,
                "L07_CirculationCollarWest",
                new Vector3(-2.45f, 3.38f, -0.55f),
                new Vector3(0.38f, 0.14f, 0.38f),
                materials.Bronze);
            Cylinder(
                delta,
                "L07_CirculationCollarEast",
                new Vector3(2.45f, 3.38f, -0.55f),
                new Vector3(0.38f, 0.14f, 0.38f),
                materials.Bronze);
        }

        private static void AddLevel08(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L08_Masterwork");
            for (int index = 0; index < 4; index++)
            {
                float z = Mathf.Lerp(-1.35f, 1.35f, index / 3f);
                Block(
                    delta,
                    $"L08_RepairArcadePost_{index:D2}",
                    new Vector3(-4.15f, 1.3f, z),
                    new Vector3(0.18f, 1.75f, 0.18f),
                    materials.PaleStone);
            }
            Block(
                delta,
                "L08_RepairArcadeRoof",
                new Vector3(-4.15f, 2.32f, 0f),
                new Vector3(0.78f, 0.16f, 3.35f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -12f));
            Block(
                delta,
                "L08_GutterSpine",
                new Vector3(-2.76f, 3.02f, 0.38f),
                new Vector3(0.16f, 0.16f, 4.38f),
                materials.Bronze);
            Block(
                delta,
                "L08_BronzeJoinery",
                new Vector3(-3.35f, 1.55f, 1.78f),
                new Vector3(1.38f, 0.18f, 0.18f),
                materials.Bronze);
        }

        private static void AddLevel09(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L09_Prestige");
            Block(
                delta,
                "L09_LogisticsPlinth",
                new Vector3(3.65f, 0.38f, 1.25f),
                new Vector3(1.7f, 0.36f, 2.2f),
                materials.PaleStone);
            Block(
                delta,
                "L09_LogisticsBay",
                new Vector3(3.65f, 1.22f, 1.35f),
                new Vector3(1.45f, 1.25f, 1.75f),
                materials.Timber);
            Block(
                delta,
                "L09_LogisticsRoof",
                new Vector3(3.65f, 2.02f, 1.35f),
                new Vector3(1.78f, 0.15f, 2.15f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, 8f));
            Block(
                delta,
                "L09_ServiceApproach",
                new Vector3(3.7f, 0.18f, -1.0f),
                new Vector3(1.75f, 0.16f, 2.45f),
                materials.PaleStone);
            Block(
                delta,
                "L09_CourtyardStorage",
                new Vector3(3.65f, 0.72f, -0.55f),
                new Vector3(1.05f, 0.76f, 0.92f),
                materials.Bronze);
        }

        private static void AddLevel10(Transform root, MaterialSet materials)
        {
            Transform delta = Group(root, "L10_Landmark");
            Cylinder(
                delta,
                "L10_SeedLanternBase",
                new Vector3(0f, 4.88f, 0.28f),
                new Vector3(0.76f, 0.18f, 0.76f),
                materials.Bronze);
            for (int index = 0; index < 6; index++)
            {
                float angle = index * Mathf.PI / 3f;
                Vector3 basePosition = new Vector3(
                    Mathf.Cos(angle) * 0.62f,
                    5.02f,
                    0.28f + Mathf.Sin(angle) * 0.62f);
                Vector3 crownPosition = new Vector3(
                    Mathf.Cos(angle) * 0.3f,
                    6.05f,
                    0.28f + Mathf.Sin(angle) * 0.3f);
                Beam(
                    delta,
                    $"L10_SeedLanternRib_{index:D2}",
                    basePosition,
                    crownPosition,
                    0.075f,
                    materials.Bronze);
            }
            Cylinder(
                delta,
                "L10_SeedLanternCore",
                new Vector3(0f, 5.45f, 0.28f),
                new Vector3(0.34f, 0.48f, 0.34f),
                materials.Sap);
            Block(
                delta,
                "L10_CrownRoofWest",
                new Vector3(-0.42f, 6.05f, 0.28f),
                new Vector3(1.1f, 0.15f, 1.5f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, 22f));
            Block(
                delta,
                "L10_CrownRoofEast",
                new Vector3(0.42f, 6.05f, 0.28f),
                new Vector3(1.1f, 0.15f, 1.5f),
                materials.LeafRoof,
                Quaternion.Euler(0f, 0f, -22f));
            Beam(
                delta,
                "L10_FinalRootLockWest",
                new Vector3(-2.65f, 3.1f, 0.82f),
                new Vector3(-0.68f, 4.88f, 0.5f),
                0.17f,
                materials.Bark);
            Beam(
                delta,
                "L10_FinalRootLockEast",
                new Vector3(2.65f, 3.1f, 0.82f),
                new Vector3(0.68f, 4.88f, 0.5f),
                0.17f,
                materials.Bark);
            Cylinder(
                delta,
                "L10_CapstoneFinial",
                new Vector3(0f, 6.42f, 0.28f),
                new Vector3(0.12f, 0.26f, 0.12f),
                materials.Bronze);
        }

        private static void CreatePreviewScene(MaterialSet materials)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            InstantiateAt(Level01PrefabPath, new Vector3(-8.3f, 0f, 0f));
            InstantiateAt(Level06PrefabPath, Vector3.zero);
            InstantiateAt(Level10PrefabPath, new Vector3(8.7f, 0f, 0f));

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ReviewGround";
            ground.transform.localScale = new Vector3(3.1f, 1f, 1.05f);
            ConfigureRenderer(ground.GetComponent<Renderer>(), materials.Ground);
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.052f, 0.068f);
            camera.transform.position = new Vector3(14.8f, 10.6f, -24.8f);
            camera.transform.LookAt(new Vector3(0f, 2.25f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.82f, 0.56f);
            key.intensity = 1.72f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.42f, 0.58f, 0.4f);
            fill.intensity = 0.62f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(28f, 146f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.19f, 0.2f, 0.145f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void InstantiateAt(string prefabPath, Vector3 position)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"The level blockout {prefabPath} is missing.");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
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

        private static void DestroyChild(Transform root, string path)
        {
            Transform child = root.Find(path);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void ConfigureRenderer(
            Renderer renderer,
            Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
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
            const string parent =
                "Assets/AL/Art/Generated/Architecture/Eldergrove";
            const string path = parent + "/Production";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, "Production");
            }
        }

        private static MaterialSet LoadMaterials()
        {
            return new MaterialSet
            {
                PaleStone = RequireMaterial("PaleStone"),
                Bark = RequireMaterial("RootBark"),
                Timber = RequireMaterial("DarkTimber"),
                Bronze = RequireMaterial("WeatheredBronze"),
                LeafRoof = RequireMaterial("LeafRoof"),
                Sap = RequireMaterial("LivingSap"),
                Ground = RequireMaterial("Ground")
            };
        }

        private static Material RequireMaterial(string name)
        {
            string path = $"{MaterialFolder}/MAT_Eldergrove_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"The approved Eldergrove material {path} is missing.");
            }
            return material;
        }

        private sealed class MaterialSet
        {
            public Material PaleStone;
            public Material Bark;
            public Material Timber;
            public Material Bronze;
            public Material LeafRoof;
            public Material Sap;
            public Material Ground;
        }
    }
}
