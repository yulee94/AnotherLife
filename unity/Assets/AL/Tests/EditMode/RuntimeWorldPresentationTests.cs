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

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void SurfaceTexture_IsCachedAndBounded()
        {
            var baseColor = new Color(0.08f, 0.10f, 0.12f);
            var variation = new Color(0.18f, 0.22f, 0.20f);

            Texture2D first = RuntimeWorldPresentation.GetSurfaceTexture(baseColor, variation);
            Texture2D second = RuntimeWorldPresentation.GetSurfaceTexture(baseColor, variation);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.width, Is.EqualTo(128));
            Assert.That(first.height, Is.EqualTo(128));
            Assert.That(first.mipmapCount, Is.GreaterThan(1));
            Assert.That(first.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
        }

        [TestCase(true, 625, 16, 14, 7)]
        [TestCase(false, 2401, 34, 28, 11)]
        public void ArenaBackdrop_UsesDeterministicQualityBudgets(
            bool reducedQuality,
            int expectedVertices,
            int expectedRocks,
            int expectedTrees,
            int expectedTowers)
        {
            _root = new GameObject("A7_TestRoot");

            RuntimeWorldPresentation.BuildArenaBackdrop(_root.transform, reducedQuality);
            RuntimeWorldPresentation.BuildArenaBackdrop(_root.transform, !reducedQuality);

            Transform backdrop = _root.transform.Find("WorldPresentation_Backdrop");
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(_root.transform.Cast<Transform>().Count(child => child.name == "WorldPresentation_Backdrop"), Is.EqualTo(1));

            var terrain = backdrop.Find("CitadelBasin_Terrain");
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(expectedVertices));
            Assert.That(terrain.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(terrain.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("WeatheredMonolith_")), Is.EqualTo(expectedRocks));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("WindcarvedCypress_")), Is.EqualTo(expectedTrees));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("DistantCitadelTower_")), Is.EqualTo(expectedTowers));
            Assert.That(backdrop.Cast<Transform>().Count(child => child.name.StartsWith("CitadelCurtainWall_")), Is.EqualTo(expectedTowers - 1));
        }

        [Test]
        public void BeveledCube_IsCachedAndUsesChamferedGeometry()
        {
            Mesh first = RuntimeWorldPresentation.GetBeveledCubeMesh();
            Mesh second = RuntimeWorldPresentation.GetBeveledCubeMesh();

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.vertexCount, Is.GreaterThan(24));
            Assert.That(first.bounds.size.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(first.bounds.size.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(first.bounds.size.z, Is.EqualTo(1f).Within(0.001f));
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
    }
}
