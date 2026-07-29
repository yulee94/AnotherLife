using System.Linq;
using System.Reflection;
using AL.ChampionMode.Camera;
using AL.ChampionMode.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class RuntimeWorldPresentationTests
    {
        private GameObject _root;
        private RuntimeWorldPresentation.SceneLease _presentationLease;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        [Test]
        public void SurfaceTexture_IsCachedAndBounded()
        {
            _presentationLease = RuntimeWorldPresentation.BeginScenePresentation();
            var baseColor = new Color(0.08f, 0.10f, 0.12f);
            var variation = new Color(0.18f, 0.22f, 0.20f);

            Texture2D first = RuntimeWorldPresentation.GetSurfaceTexture(baseColor, variation);
            Texture2D second = RuntimeWorldPresentation.GetSurfaceTexture(baseColor, variation);
            Texture2D third = RuntimeWorldPresentation.GetSurfaceTexture(Color.red, Color.blue);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first, Is.SameAs(third));
            Assert.That(first.width, Is.EqualTo(64));
            Assert.That(first.height, Is.EqualTo(64));
            Assert.That(first.mipmapCount, Is.GreaterThan(1));
            Assert.That(first.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(first.isReadable, Is.False);
            Assert.That(RuntimeWorldPresentation.CachedSurfaceTextureCount, Is.EqualTo(1));
        }

        [TestCase(true, 625, 16, 14, 7, 51)]
        [TestCase(false, 2401, 34, 28, 11, 106)]
        public void ArenaBackdrop_UsesDeterministicQualityBudgets(
            bool reducedQuality,
            int expectedVertices,
            int expectedRocks,
            int expectedTrees,
            int expectedTowers,
            int expectedRenderers)
        {
            _root = new GameObject("A7_TestRoot");
            _presentationLease = RuntimeWorldPresentation.BeginScenePresentation();

            RuntimeWorldPresentation.BuildArenaBackdrop(_root.transform, reducedQuality);
            RuntimeWorldPresentation.BuildArenaBackdrop(_root.transform, !reducedQuality);

            Transform backdrop = _root.transform.Find("WorldPresentation_Backdrop");
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(_root.transform.Cast<Transform>().Count(child => child.name == "WorldPresentation_Backdrop"), Is.EqualTo(1));

            var terrain = backdrop.Find("CitadelBasin_Terrain");
            Assert.That(terrain, Is.Not.Null);
            Mesh terrainMesh = terrain.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(terrainMesh.vertexCount, Is.EqualTo(expectedVertices));
            Assert.That(terrain.GetComponent<Collider>(), Is.Null);
            Assert.That(
                terrainMesh.vertices[terrainMesh.vertexCount / 2].y,
                Is.LessThanOrEqualTo(-1.55f));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("WeatheredMonolith_")), Is.EqualTo(expectedRocks));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("WindcarvedCypress_")), Is.EqualTo(expectedTrees));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("DistantCitadelTower_")), Is.EqualTo(expectedTowers));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("CitadelCurtainWall_")), Is.EqualTo(expectedTowers - 1));
            Renderer[] renderers = backdrop.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.EqualTo(expectedRenderers));
            Assert.That(
                renderers.Select(entry => entry.sharedMaterial).Distinct().Count(),
                Is.EqualTo(2));
            Assert.That(RuntimeWorldPresentation.CachedSurfaceTextureCount, Is.EqualTo(1));
            Assert.That(RuntimeWorldPresentation.CachedSurfaceMaterialCount, Is.EqualTo(2));
        }

        [Test]
        public void BeveledCube_IsCachedAndUsesChamferedGeometry()
        {
            _presentationLease = RuntimeWorldPresentation.BeginScenePresentation();
            Mesh first = RuntimeWorldPresentation.GetBeveledCubeMesh();
            Mesh second = RuntimeWorldPresentation.GetBeveledCubeMesh();

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.vertexCount, Is.GreaterThan(24));
            Assert.That(first.bounds.size.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(first.bounds.size.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(first.bounds.size.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SurfaceMaterialFamily_SharesMaterialsAndKeepsPerRendererColor()
        {
            _root = new GameObject("A7_SharedMaterialRoot");
            _presentationLease = RuntimeWorldPresentation.BeginScenePresentation();
            var firstObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var secondObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var emissiveObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            firstObject.transform.SetParent(_root.transform);
            secondObject.transform.SetParent(_root.transform);
            emissiveObject.transform.SetParent(_root.transform);
            Renderer first = firstObject.GetComponent<Renderer>();
            Renderer second = secondObject.GetComponent<Renderer>();
            Renderer emissive = emissiveObject.GetComponent<Renderer>();

            RuntimeWorldPresentation.ApplySurfaceMaterial(first, Color.red, 0.1f, 0.3f);
            RuntimeWorldPresentation.ApplySurfaceMaterial(second, Color.blue, 0.2f, 0.4f);
            RuntimeWorldPresentation.ApplySurfaceMaterial(emissive, Color.cyan, 0f, 0.8f, 1.2f);

            Assert.That(first.sharedMaterial, Is.SameAs(second.sharedMaterial));
            Assert.That(emissive.sharedMaterial, Is.Not.SameAs(first.sharedMaterial));
            Assert.That(RuntimeWorldPresentation.CachedSurfaceMaterialCount, Is.EqualTo(2));
            Assert.That(RuntimeWorldPresentation.CachedSurfaceTextureCount, Is.EqualTo(1));

            var properties = new MaterialPropertyBlock();
            first.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_Color"), Is.EqualTo(Color.red));
            second.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_Color"), Is.EqualTo(Color.blue));
        }

        [Test]
        public void CameraCollision_PullsDesiredPositionInFrontOfObstacle()
        {
            _root = new GameObject("A7_CameraCollisionRoot");
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_root.transform);
            var follow = cameraObject.AddComponent<CameraFollow>();

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CameraBlockingWall";
            wall.transform.SetParent(_root.transform);
            wall.transform.position = new Vector3(0f, 1.5f, -4f);
            wall.transform.localScale = new Vector3(5f, 5f, 0.3f);
            Physics.SyncTransforms();

            MethodInfo method = typeof(CameraFollow).GetMethod(
                "ResolveCameraCollision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var pivot = new Vector3(0f, 1.5f, 0f);
            var desired = new Vector3(0f, 1.5f, -8f);
            var resolved = (Vector3)method.Invoke(follow, new object[] { pivot, desired });

            Assert.That(Vector3.Distance(pivot, resolved), Is.LessThan(Vector3.Distance(pivot, desired)));
            Assert.That(resolved.z, Is.GreaterThan(-4f));
        }

        [Test]
        public void CameraCollision_CloseWallNeverForcesCameraThroughObstacle()
        {
            _root = new GameObject("A7_CloseWallRoot");
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_root.transform);
            var follow = cameraObject.AddComponent<CameraFollow>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_root.transform);
            wall.transform.position = new Vector3(0f, 1.5f, -0.36f);
            wall.transform.localScale = new Vector3(5f, 5f, 0.10f);
            Physics.SyncTransforms();

            Vector3 pivot = new Vector3(0f, 1.5f, 0f);
            Vector3 desired = new Vector3(0f, 1.5f, -8f);
            Vector3 resolved = InvokeCollision(follow, pivot, desired);

            Assert.That(Vector3.Distance(pivot, resolved), Is.LessThan(0.31f));
            Assert.That(resolved.z, Is.GreaterThan(-0.31f));
        }

        [Test]
        public void CameraCollision_RecoversToDesiredPositionAfterObstacleClears()
        {
            _root = new GameObject("A7_CameraRecoveryRoot");
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_root.transform);
            var follow = cameraObject.AddComponent<CameraFollow>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_root.transform);
            wall.transform.position = new Vector3(0f, 1.5f, -4f);
            wall.transform.localScale = new Vector3(5f, 5f, 0.3f);
            Physics.SyncTransforms();

            Vector3 pivot = new Vector3(0f, 1.5f, 0f);
            Vector3 desired = new Vector3(0f, 1.5f, -8f);
            Assert.That(InvokeCollision(follow, pivot, desired), Is.Not.EqualTo(desired));

            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            Assert.That(InvokeCollision(follow, pivot, desired), Is.EqualTo(desired));
        }

        [Test]
        public void CameraCollision_IsAppliedAfterShakeOffset()
        {
            _root = new GameObject("A7_CameraShakeCollisionRoot");
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_root.transform);
            var follow = cameraObject.AddComponent<CameraFollow>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_root.transform);
            wall.transform.position = new Vector3(0f, 1.5f, -4f);
            wall.transform.localScale = new Vector3(5f, 5f, 0.3f);
            Physics.SyncTransforms();

            MethodInfo method = typeof(CameraFollow).GetMethod(
                "ResolveFinalCameraPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var pivot = new Vector3(0f, 1.5f, 0f);
            var smoothed = new Vector3(0f, 1.5f, -3.4f);
            var shake = new Vector3(0f, 0f, -2.2f);
            var resolved = (Vector3)method.Invoke(
                follow,
                new object[] { pivot, smoothed, shake });

            Assert.That(resolved.z, Is.GreaterThan(-4f));
        }

        [TestCase("mobile_low", true)]
        [TestCase("mobile_standard", true)]
        [TestCase("desktop_low", true)]
        [TestCase("desktop_standard", false)]
        public void QualityTier_UsesMeasuredBackdropBudget(string tier, bool expectedReduced)
        {
            Assert.That(
                RuntimeWorldPresentation.UsesReducedQualityTier(tier),
                Is.EqualTo(expectedReduced));
        }

        [Test]
        public void SceneLease_RestoresRenderSettingsAndReleasesResources()
        {
            Material previousSkybox = RenderSettings.skybox;
            bool previousFog = RenderSettings.fog;
            Color previousFogColor = RenderSettings.fogColor;
            _root = new GameObject("A7_LifecycleRoot");
            _presentationLease = RuntimeWorldPresentation.BeginScenePresentation();
            RuntimeWorldPresentation.BuildArenaBackdrop(_root.transform, true);

            Assert.That(RuntimeWorldPresentation.CachedSurfaceTextureCount, Is.EqualTo(1));
            Assert.That(RuntimeWorldPresentation.CachedSurfaceMaterialCount, Is.EqualTo(2));
            Assert.That(RuntimeWorldPresentation.OwnedMeshCount, Is.GreaterThan(0));

            _presentationLease.Dispose();
            _presentationLease = null;

            Assert.That(RenderSettings.skybox, Is.SameAs(previousSkybox));
            Assert.That(RenderSettings.fog, Is.EqualTo(previousFog));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(previousFogColor));
            Assert.That(RuntimeWorldPresentation.CachedSurfaceTextureCount, Is.Zero);
            Assert.That(RuntimeWorldPresentation.CachedSurfaceMaterialCount, Is.Zero);
            Assert.That(RuntimeWorldPresentation.OwnedMeshCount, Is.Zero);
        }

        private static Vector3 InvokeCollision(
            CameraFollow follow,
            Vector3 pivot,
            Vector3 desired)
        {
            MethodInfo method = typeof(CameraFollow).GetMethod(
                "ResolveCameraCollision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Vector3)method.Invoke(follow, new object[] { pivot, desired });
        }
    }
}
