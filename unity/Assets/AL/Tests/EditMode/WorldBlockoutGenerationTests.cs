using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AL.Data.Catalogs.WorldStreaming;
using AL.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Tests.EditMode
{
    public sealed class WorldBlockoutGenerationTests
    {
        private const string GenerateMenu =
            "AnotherLife/World/Generate Full Modular World Blockout";
        private const string RenderPreviewsMenu =
            "AnotherLife/World/Render Representative World Blockout Previews";

        [Test]
        public void GeneratorCreatesEveryCatalogChunkAsSubstitutionReadyScene()
        {
            WorldStreamingSnapshot snapshot = LoadSnapshot();

            Assert.That(
                EditorApplication.ExecuteMenuItem(GenerateMenu),
                Is.True,
                "The full-world generator menu must exist and execute.");

            foreach (WorldChunkDefinition chunk in snapshot.Chunks)
            {
                Assert.That(File.Exists(chunk.ScenePath), Is.True, chunk.ScenePath);
                Scene scene = EditorSceneManager.OpenScene(
                    chunk.ScenePath,
                    OpenSceneMode.Single);
                GameObject[] roots = scene.GetRootGameObjects();
                GameObject root = roots.SingleOrDefault(value => value.name == "WorldChunkRoot");
                Assert.That(root, Is.Not.Null, chunk.Id);
                Assert.That(root.GetComponent("WorldChunkRoot"), Is.Not.Null, chunk.Id);
                Assert.That(
                    root.GetComponentsInChildren<WorldReplacementSocket>(true).Length,
                    Is.EqualTo(chunk.ReplacementSocketIds.Count),
                    chunk.Id);
                Assert.That(
                    roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)),
                    Is.Empty,
                    chunk.Id + " must not own a gameplay camera.");
            }
        }

        [TestCase(
            "Assets/AL/Worlds/Generated/Adventure3D/world_adventure_ring_slot_01_inner/chunk_ring_slot_01_capital_core.unity",
            "CapitalKeepBlockout")]
        [TestCase(
            "Assets/AL/Worlds/Generated/Adventure3D/world_adventure_outer_warzone/chunk_warzone_bridge_01_02_a.unity",
            "BridgeDeckBlockout")]
        [TestCase(
            "Assets/AL/Worlds/Generated/Kingdom25D/world_kingdom_private/chunk_kingdom_castle_core.unity",
            "KingdomCastleBlockout")]
        [TestCase(
            "Assets/AL/Worlds/Generated/SpecialEvent3D/world_event_accordant_isle/chunk_accordant_wish_dragon_cavern.unity",
            "WishDragonCavernBlockout")]
        public void RepresentativeChunksExposePurposeSpecificSilhouette(
            string scenePath,
            string requiredObjectName)
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<Transform>(true))
                    .Any(value => value.name == requiredObjectName),
                Is.True,
                scenePath);
        }

        [Test]
        public void GeneratedChunkScenesRemainOutsideProductionBuildSettings()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            string[] generatedScenePaths = LoadSnapshot().Chunks
                .Select(value => value.ScenePath)
                .ToArray();
            string[] buildPaths = EditorBuildSettings.scenes
                .Where(value => value.enabled)
                .Select(value => value.path)
                .ToArray();

            CollectionAssert.IsEmpty(generatedScenePaths.Intersect(buildPaths));
        }

        [Test]
        public void RepresentativePreviewRendererExportsInspectableImages()
        {
            RequirePreviewGraphics();
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            Assert.That(EditorApplication.ExecuteMenuItem(RenderPreviewsMenu), Is.True);

            string[] paths =
            {
                "Assets/AL/Worlds/Generated/Previews/adventure_capital.png",
                "Assets/AL/Worlds/Generated/Previews/warzone_bridge.png",
                "Assets/AL/Worlds/Generated/Previews/accordant_center_bridge.png",
                "Assets/AL/Worlds/Generated/Previews/kingdom_castle.png",
                "Assets/AL/Worlds/Generated/Previews/wish_dragon_cavern.png"
            };
            foreach (string path in paths)
            {
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(1024), path);
            }
        }

        [Test]
        public void AdventureCapitalPreviewKeepsTerrainReadableInFrame()
        {
            RequirePreviewGraphics();
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            Assert.That(EditorApplication.ExecuteMenuItem(RenderPreviewsMenu), Is.True);

            byte[] bytes = File.ReadAllBytes(
                "Assets/AL/Worlds/Generated/Previews/adventure_capital.png");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(image.LoadImage(bytes), Is.True);
                Color32[] pixels = image.GetPixels32();
                Color32 background = pixels[0];
                int scenePixels = 0;
                int minX = image.width;
                int minY = image.height;
                int maxX = -1;
                int maxY = -1;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    int distanceFromBackground =
                        Mathf.Abs(pixel.r - background.r) +
                        Mathf.Abs(pixel.g - background.g) +
                        Mathf.Abs(pixel.b - background.b);
                    if (distanceFromBackground <= 24)
                    {
                        continue;
                    }

                    scenePixels++;
                    int x = index % image.width;
                    int y = index / image.width;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }

                float sceneCoverage = (float)scenePixels / pixels.Length;
                Assert.That(
                    sceneCoverage,
                    Is.InRange(0.15f, 0.75f),
                    "Capital framing must keep substantial scene geometry visible without cropping " +
                    "the frame down to geometry alone.");
                Assert.That(
                    (float)(maxX - minX + 1) / image.width,
                    Is.GreaterThan(0.60f),
                    "Capital terrain and landmarks must remain readable across the frame width.");
                Assert.That(
                    (float)(maxY - minY + 1) / image.height,
                    Is.GreaterThan(0.30f),
                    "Capital terrain and landmarks must retain readable depth in the frame.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void WishDragonCavernUsesOpenSegmentedShellAndInvisibleFlightVolume()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            EditorSceneManager.OpenScene(
                "Assets/AL/Worlds/Generated/SpecialEvent3D/world_event_accordant_isle/chunk_accordant_wish_dragon_cavern.unity",
                OpenSceneMode.Single);

            GameObject cavern = GameObject.Find("WishDragonCavernBlockout");
            Assert.That(cavern, Is.Not.Null);
            Assert.That(cavern.GetComponent<Renderer>(), Is.Null);
            Assert.That(
                cavern.GetComponentsInChildren<Renderer>(true)
                    .Count(value => value.name.StartsWith("CavernWallSegmentBlockout_", StringComparison.Ordinal)),
                Is.GreaterThanOrEqualTo(8));

            GameObject flightVolume = GameObject.Find("WishDragonFlightVolumeBlockout");
            Assert.That(flightVolume, Is.Not.Null);
            Assert.That(flightVolume.GetComponent<Renderer>().enabled, Is.False);
        }

        [Test]
        public void WishDragonFlightVolumeHasNoSolidCollider()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            EditorSceneManager.OpenScene(
                "Assets/AL/Worlds/Generated/SpecialEvent3D/world_event_accordant_isle/chunk_accordant_wish_dragon_cavern.unity",
                OpenSceneMode.Single);

            GameObject flightVolume = GameObject.Find("WishDragonFlightVolumeBlockout");
            Assert.That(flightVolume, Is.Not.Null);
            Assert.That(flightVolume.GetComponent<Collider>(), Is.Null);
        }

        [Test]
        public void WarzoneGateApproachBuildsControlledTransitionAndOuterWall()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            EditorSceneManager.OpenScene(
                "Assets/AL/Worlds/Generated/Adventure3D/world_adventure_outer_warzone/chunk_warzone_gate_approach_01.unity",
                OpenSceneMode.Single);

            Assert.That(GameObject.Find("ControlledTransitionZoneBlockout"), Is.Not.Null);
            Assert.That(GameObject.Find("OuterWallLeftBlockout"), Is.Not.Null);
            Assert.That(GameObject.Find("OuterWallRightBlockout"), Is.Not.Null);
            Assert.That(GameObject.Find("WarzoneEntryThresholdBlockout"), Is.Not.Null);
        }

        [Test]
        public void WarzoneBridgeRailsStayLateralForEveryOrientation()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            foreach (WorldChunkDefinition chunk in LoadSnapshot().Chunks
                         .Where(value => value.BlockoutArchetype == "warzone_bridge"))
            {
                EditorSceneManager.OpenScene(chunk.ScenePath, OpenSceneMode.Single);
                Transform deck = GameObject.Find("BridgeDeckBlockout").transform;
                Transform left = GameObject.Find("BridgeRailLeft").transform;
                Transform right = GameObject.Find("BridgeRailRight").transform;
                Vector3 leftDelta = left.position - deck.position;
                Vector3 rightDelta = right.position - deck.position;
                float leftLateral = Vector3.Dot(leftDelta, deck.right);
                float rightLateral = Vector3.Dot(rightDelta, deck.right);

                Assert.That(Mathf.Abs(leftLateral), Is.GreaterThan(50f), chunk.Id);
                Assert.That(Mathf.Abs(rightLateral), Is.GreaterThan(50f), chunk.Id);
                Assert.That(leftLateral * rightLateral, Is.LessThan(0f), chunk.Id);
                Assert.That(Mathf.Abs(Vector3.Dot(leftDelta, deck.forward)), Is.LessThan(1f), chunk.Id);
                Assert.That(Mathf.Abs(Vector3.Dot(rightDelta, deck.forward)), Is.LessThan(1f), chunk.Id);
            }
        }

        [Test]
        public void GeneratedMeterScalePrimitivesNeverNestUnderScaledRenderer()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            foreach (WorldChunkDefinition chunk in LoadSnapshot().Chunks)
            {
                Scene scene = EditorSceneManager.OpenScene(chunk.ScenePath, OpenSceneMode.Single);
                Renderer[] renderers = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<Renderer>(true))
                    .ToArray();
                foreach (Renderer renderer in renderers)
                {
                    Transform parent = renderer.transform.parent;
                    if (parent == null || parent.GetComponent<Renderer>() == null)
                    {
                        continue;
                    }

                    Assert.That(
                        parent.localScale,
                        Is.EqualTo(Vector3.one),
                        chunk.Id + ": " + renderer.name +
                        " multiplies meter dimensions under scaled renderer " + parent.name);
                }
            }
        }

        [Test]
        public void GeneratedObjectNamesContainNoRandomGuids()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);
            var guidSuffix = new Regex("[0-9a-f]{32}$", RegexOptions.CultureInvariant);
            foreach (WorldChunkDefinition chunk in LoadSnapshot().Chunks)
            {
                Scene scene = EditorSceneManager.OpenScene(chunk.ScenePath, OpenSceneMode.Single);
                string[] randomNames = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<Transform>(true))
                    .Select(value => value.name)
                    .Where(value => guidSuffix.IsMatch(value))
                    .ToArray();

                Assert.That(
                    randomNames,
                    Is.Empty,
                    chunk.Id + " contains nondeterministic generated object names.");
            }
        }

        [Test]
        public void GeneratorRestoresTheEditorsLoadedSceneSetup()
        {
            const string sentinel = "Assets/AL/Scenes/Boot.unity";
            const string stamp =
                "Assets/AL/Worlds/Generated/world_blockout_catalog.sha256.txt";
            EditorSceneManager.OpenScene(sentinel, OpenSceneMode.Single);
            File.Delete(stamp);

            Assert.That(EditorApplication.ExecuteMenuItem(GenerateMenu), Is.True);

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(sentinel));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        [Test]
        public void PreviewRendererRestoresTheEditorsLoadedSceneSetup()
        {
            RequirePreviewGraphics();
            const string sentinel = "Assets/AL/Scenes/Boot.unity";
            EditorSceneManager.OpenScene(sentinel, OpenSceneMode.Single);

            Assert.That(EditorApplication.ExecuteMenuItem(RenderPreviewsMenu), Is.True);

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(sentinel));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        [Test]
        public void PreviewRendererFailsClosedBeforeOverwritingUnderNullGraphics()
        {
            if (SystemInfo.graphicsDeviceType !=
                UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "This guard is specific to command-line runs using -nographics.");
            }

            const string previewPath =
                "Assets/AL/Worlds/Generated/Previews/adventure_capital.png";
            byte[] retained = File.ReadAllBytes(previewPath);

            Type rendererType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "AL.Editor.World.WorldBlockoutPreviewRenderer"))
                .FirstOrDefault(type => type != null);
            Assert.NotNull(rendererType);
            System.Reflection.MethodInfo renderMethod = rendererType.GetMethod(
                "RenderRepresentativePreviews",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);
            Assert.NotNull(renderMethod);
            System.Reflection.TargetInvocationException invocation =
                Assert.Throws<System.Reflection.TargetInvocationException>(
                    () => renderMethod.Invoke(null, null));
            var error = invocation.InnerException as InvalidOperationException;
            Assert.NotNull(error);

            StringAssert.Contains("requires a graphics device", error.Message);
            CollectionAssert.AreEqual(
                retained,
                File.ReadAllBytes(previewPath),
                "A headless tooling invocation must not replace an inspectable preview with a NullGfx clear frame.");
        }

        private static void RequirePreviewGraphics()
        {
            if (SystemInfo.graphicsDeviceType ==
                UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "Image-framing validation requires a real graphics device; run this test without -nographics.");
            }
        }

        private static WorldStreamingSnapshot LoadSnapshot()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_streaming_catalog.json");
            WorldStreamingLoadResult result =
                WorldStreamingCatalogLoader.Validate(File.ReadAllBytes(path));
            Assert.That(
                result.Status,
                Is.EqualTo(WorldStreamingLoadStatus.Accepted),
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            return result.Snapshot;
        }
    }
}
