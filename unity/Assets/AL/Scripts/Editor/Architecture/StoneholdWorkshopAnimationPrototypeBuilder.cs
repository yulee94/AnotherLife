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
    public static class StoneholdWorkshopAnimationPrototypeBuilder
    {
        public const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/" +
            "Stonehold_Workshop_AnimationPrototype.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "StoneholdWorkshopAnimationPrototype.unity";
        private const string ProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Stonehold_Workshop_ConstructionProfile.asset";
        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Materials";

        [MenuItem("Another Life/Architecture/Build Stonehold Workshop Animation Prototype")]
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
                    "The approved Stonehold construction profile is missing or invalid.");
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
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("Prototype preview camera is missing.");
            }

            GameObject prototype =
                GameObject.Find("Stonehold_Workshop_AnimationPrototype");
            prototype
                .GetComponent<ArchitectureConstructionAnimationController>()
                .SetPreviewTime(16f);

            var target = new RenderTexture(768, 768, 24);
            var capture = new Texture2D(768, 768, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0f, 0f, 768f, 768f), 0, 0);
                capture.Apply();
                string outputPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "..",
                        ".omx", "state", "stonehold-graybox-preview.png"));
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

        public static void RenderProcessSheetFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Object.FindObjectOfType<Camera>();
            GameObject prototype =
                GameObject.Find("Stonehold_Workshop_AnimationPrototype");
            if (camera == null || prototype == null)
            {
                throw new InvalidOperationException(
                    "Prototype camera or workshop is missing.");
            }

            var controller =
                prototype.GetComponent<ArchitectureConstructionAnimationController>();
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
                        controller.SetPreviewTime(10.6f);
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
                        "stonehold-graybox",
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
            var root = new GameObject("Stonehold_Workshop_AnimationPrototype");

            Transform plot = Group(root.transform, "PlotPrepared");
            Block(plot, "SteppedFootprint", new Vector3(0f, 0.12f, 0f),
                new Vector3(6.2f, 0.24f, 4.8f), materials.Basalt);
            Block(plot, "EntranceStep", new Vector3(0f, 0.18f, -2.65f),
                new Vector3(1.8f, 0.18f, 0.72f), materials.Basalt);
            Block(plot, "IronSupplyWest", new Vector3(-2.25f, 0.34f, -1.9f),
                new Vector3(0.75f, 0.28f, 0.5f), materials.Iron);
            Block(plot, "TimberSupplyEast", new Vector3(2.2f, 0.34f, -1.85f),
                new Vector3(1.0f, 0.22f, 0.45f), materials.Timber);

            Transform foundation = Group(root.transform, "FoundationSeated");
            Block(foundation, "FoundationCourse", new Vector3(0f, 0.42f, 0f),
                new Vector3(5.65f, 0.55f, 4.25f), materials.Basalt);
            Block(foundation, "WestPlinth", new Vector3(-2.55f, 0.68f, 0f),
                new Vector3(0.7f, 0.72f, 4.3f), materials.Basalt);
            Block(foundation, "EastPlinth", new Vector3(2.55f, 0.68f, 0f),
                new Vector3(0.7f, 0.72f, 4.3f), materials.Basalt);

            Transform walls = Group(root.transform, "WallShellLocked");
            Block(walls, "BackWall", new Vector3(0f, 1.65f, 1.85f),
                new Vector3(5.15f, 2.2f, 0.48f), materials.Stone);
            Block(walls, "WestWall", new Vector3(-2.35f, 1.55f, 0f),
                new Vector3(0.55f, 2.05f, 3.55f), materials.Stone);
            Block(walls, "EastWall", new Vector3(2.35f, 1.55f, 0f),
                new Vector3(0.55f, 2.05f, 3.55f), materials.Stone);
            Block(walls, "WestButtress", new Vector3(-2.78f, 1.25f, 0.95f),
                new Vector3(0.5f, 1.65f, 0.85f), materials.Basalt);
            Block(walls, "EastButtress", new Vector3(2.78f, 1.25f, 0.95f),
                new Vector3(0.5f, 1.65f, 0.85f), materials.Basalt);
            Block(walls, "EntranceArchWestJamb",
                new Vector3(-2.0f, 1.42f, -1.93f),
                new Vector3(0.42f, 1.65f, 0.52f), materials.Stone);
            Block(walls, "EntranceArchEastJamb",
                new Vector3(-0.62f, 1.42f, -1.93f),
                new Vector3(0.42f, 1.65f, 0.52f), materials.Stone);
            Block(walls, "EntranceArchWestShoulder",
                new Vector3(-1.86f, 2.22f, -1.93f),
                new Vector3(0.58f, 0.38f, 0.54f), materials.Stone,
                Quaternion.Euler(0f, 0f, -28f));
            Block(walls, "EntranceArchCrown",
                new Vector3(-1.31f, 2.47f, -1.93f),
                new Vector3(0.72f, 0.38f, 0.54f), materials.Stone);
            Block(walls, "EntranceArchEastShoulder",
                new Vector3(-0.76f, 2.22f, -1.93f),
                new Vector3(0.58f, 0.38f, 0.54f), materials.Stone,
                Quaternion.Euler(0f, 0f, 28f));
            Block(walls, "EntranceLintel", new Vector3(0f, 2.15f, -1.92f),
                new Vector3(2.15f, 0.45f, 0.62f), materials.Iron);
            Block(walls, "WestIronCatch", new Vector3(-1.25f, 1.62f, -1.96f),
                new Vector3(0.22f, 2.25f, 0.2f), materials.Iron);
            Block(walls, "EastIronCatch", new Vector3(1.25f, 1.62f, -1.96f),
                new Vector3(0.22f, 2.25f, 0.2f), materials.Iron);

            Transform roof = Group(root.transform, "RoofAndChimneySet");
            Transform roofWest = Group(roof, "RoofWestRigidGroup");
            Block(roofWest, "RoofSlabWest", new Vector3(-1.25f, 3.05f, 0f),
                new Vector3(2.85f, 0.35f, 4.35f), materials.Iron,
                Quaternion.Euler(0f, 0f, 13f));
            CreateRoofRibs(
                roofWest,
                "West",
                -1.25f,
                13f,
                materials.Basalt);
            Transform roofEast = Group(roof, "RoofEastRigidGroup");
            Block(roofEast, "RoofSlabEast", new Vector3(1.25f, 3.05f, 0f),
                new Vector3(2.85f, 0.35f, 4.35f), materials.Iron,
                Quaternion.Euler(0f, 0f, -13f));
            CreateRoofRibs(
                roofEast,
                "East",
                1.25f,
                -13f,
                materials.Basalt);
            Transform roofRidge = Group(roof, "RoofRidgeRigidGroup");
            Block(roofRidge, "RoofRidge", new Vector3(0f, 3.42f, 0f),
                new Vector3(0.42f, 0.42f, 4.45f), materials.Basalt);
            Transform chimney = Group(roof, "ChimneyRigidGroup");
            Block(chimney, "Chimney", new Vector3(1.55f, 4.0f, 1.05f),
                new Vector3(0.9f, 2.1f, 0.9f), materials.Stone);
            Block(chimney, "ChimneyCap", new Vector3(1.55f, 5.08f, 1.05f),
                new Vector3(1.15f, 0.22f, 1.15f), materials.Iron);

            Transform fitout = Group(root.transform, "FittedOut");
            Block(fitout, "ForgeBody", new Vector3(1.3f, 0.92f, 0.92f),
                new Vector3(1.45f, 1.1f, 1.2f), materials.Basalt);
            Renderer forgeCore = Block(
                fitout, "ForgeCore", new Vector3(1.3f, 1.05f, 0.28f),
                new Vector3(0.72f, 0.42f, 0.08f), materials.Forge);
            Block(fitout, "Workbench", new Vector3(-1.15f, 0.78f, 0.75f),
                new Vector3(1.8f, 0.22f, 0.8f), materials.Timber);
            Block(fitout, "Anvil", new Vector3(-0.85f, 0.92f, -0.35f),
                new Vector3(0.8f, 0.52f, 0.45f), materials.Iron);
            Transform bellows = Group(fitout, "Bellows");
            Block(bellows, "BellowsBody", new Vector3(1.55f, 0.82f, -0.55f),
                new Vector3(0.75f, 0.48f, 1.05f), materials.Leather);
            Transform hammer = Group(fitout, "Hammer");
            Block(hammer, "Handle", new Vector3(-0.85f, 1.45f, -0.25f),
                new Vector3(0.12f, 0.9f, 0.12f), materials.Timber,
                Quaternion.Euler(0f, 0f, -28f));
            Block(hammer, "Head", new Vector3(-1.03f, 1.83f, -0.25f),
                new Vector3(0.65f, 0.22f, 0.28f), materials.Iron,
                Quaternion.Euler(0f, 0f, -28f));

            var forgeLightObject = new GameObject("LocalizedForgeLight");
            forgeLightObject.transform.SetParent(fitout, false);
            forgeLightObject.transform.localPosition = new Vector3(1.3f, 1.15f, -0.05f);
            Light forgeLight = forgeLightObject.AddComponent<Light>();
            forgeLight.type = LightType.Point;
            forgeLight.color = new Color(1f, 0.24f, 0.04f);
            forgeLight.range = 3.0f;
            forgeLight.intensity = 0f;
            forgeLight.shadows = LightShadows.None;

            var activity = root.AddComponent<StoneholdWorkshopStableActivity>();
            activity.Configure(bellows, hammer, new[] { forgeCore }, forgeLight);

            var controller =
                root.AddComponent<ArchitectureConstructionAnimationController>();
            controller.Configure(
                profile,
                new[]
                {
                    new[] { plot },
                    new[] { foundation },
                    new[] { walls },
                    new[] { roofWest, roofEast, roofRidge, chimney },
                    new[] { fitout }
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
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.021f, 0.018f);
            camera.transform.position = new Vector3(7.2f, 5.7f, -10.2f);
            camera.transform.LookAt(new Vector3(0f, 1.65f, 0f));

            var lightObject = new GameObject("KeyLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.78f, 0.58f);
            light.intensity = 1.45f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.21f, 0.185f, 0.16f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateRoofRibs(
            Transform parent,
            string side,
            float centerX,
            float pitch,
            Material material)
        {
            for (int index = 0; index < 6; index++)
            {
                float z = Mathf.Lerp(-1.82f, 1.82f, index / 5f);
                Block(
                    parent,
                    $"{side}RoofRib_{index:D2}",
                    new Vector3(centerX, 3.27f, z),
                    new Vector3(2.92f, 0.085f, 0.12f),
                    material,
                    Quaternion.Euler(0f, 0f, pitch));
            }
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
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/AL/Art/Generated/Architecture", "Stonehold");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Stonehold",
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
                Basalt = Material("Basalt", new Color(0.13f, 0.12f, 0.115f), 0.05f),
                Stone = Material("Stone", new Color(0.24f, 0.22f, 0.20f), 0.04f),
                Iron = Material("DarkIron", new Color(0.09f, 0.085f, 0.08f), 0.62f),
                Timber = Material("Timber", new Color(0.22f, 0.12f, 0.07f), 0.02f),
                Leather = Material("Leather", new Color(0.16f, 0.07f, 0.035f), 0.01f),
                Forge = Material(
                    "ForgeAmber",
                    new Color(0.7f, 0.08f, 0.01f),
                    0.0f,
                    new Color(1.5f, 0.16f, 0.01f)),
                Ground = Material("Ground", new Color(0.07f, 0.06f, 0.05f), 0.0f)
            };
        }

        private static Material Material(
            string name,
            Color color,
            float metallic,
            Color? emission = null)
        {
            string path = $"{MaterialFolder}/MAT_Stonehold_{name}.mat";
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
            public Material Basalt;
            public Material Stone;
            public Material Iron;
            public Material Timber;
            public Material Leather;
            public Material Forge;
            public Material Ground;
        }
    }
}
