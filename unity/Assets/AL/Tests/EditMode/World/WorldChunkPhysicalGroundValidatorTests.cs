using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;
using AL.World.Streaming;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Tests.EditMode.World
{
    public sealed class WorldChunkPhysicalGroundValidatorTests
    {
        [Test]
        public void RendererAndIncidentalColliderWithoutAuthorityFailClosed()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fixture.Adopt(visual);

                WorldChunkPhysicalGroundReadiness readiness = fixture.Evaluate();

                AssertDiagnostic(
                    readiness,
                    WorldChunkLoadFailureCodes.PhysicalGroundAuthorityMissing);
            }
        }

        [Test]
        public void DisabledTerrainColliderIsRejected()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                var terrainData = new TerrainData();
                TerrainCollider collider = fixture.RootObject
                    .AddComponent<TerrainCollider>();
                try
                {
                    collider.terrainData = terrainData;
                    collider.enabled = false;
                    fixture.ConfigureAuthority(
                        WorldChunkGroundSourceKind.TerrainHeightfield,
                        string.Empty,
                        new Collider[] { collider },
                        Array.Empty<WorldChunkEdgeSafetyBinding>());

                    AssertDiagnostic(
                        fixture.Evaluate(),
                        WorldChunkLoadFailureCodes.GroundColliderDisabled);
                }
                finally
                {
                    collider.terrainData = null;
                    UnityEngine.Object.DestroyImmediate(terrainData);
                }
            }
        }

        [Test]
        public void TerrainColliderWithoutTerrainDataIsRejectedAsUnbound()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                TerrainCollider collider = fixture.RootObject
                    .AddComponent<TerrainCollider>();
                fixture.ConfigureAuthority(
                    WorldChunkGroundSourceKind.TerrainHeightfield,
                    string.Empty,
                    new Collider[] { collider },
                    Array.Empty<WorldChunkEdgeSafetyBinding>());

                AssertDiagnostic(
                    fixture.Evaluate(),
                    WorldChunkLoadFailureCodes.GroundColliderUnbound);
            }
        }

        [Test]
        public void ColliderOutsideCatalogChunkHierarchyIsRejectedAsUnbound()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                var external = new GameObject("ExternalGround");
                SceneManager.MoveGameObjectToScene(external, fixture.Scene);
                BoxCollider collider = external.AddComponent<BoxCollider>();
                fixture.ConfigureAuthority(
                    WorldChunkGroundSourceKind.SolidColliderAssembly,
                    "test-reviewed-external-ground-v1",
                    new Collider[] { collider },
                    Array.Empty<WorldChunkEdgeSafetyBinding>());

                AssertDiagnostic(
                    fixture.Evaluate(),
                    WorldChunkLoadFailureCodes.GroundColliderUnbound);
            }
        }

        [Test]
        public void RenderMeshCannotBeReusedAsGroundCollisionAuthority()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fixture.Adopt(visual);
                UnityEngine.Object.DestroyImmediate(
                    visual.GetComponent<BoxCollider>());
                MeshCollider collider = visual.AddComponent<MeshCollider>();
                collider.sharedMesh = visual.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = true;
                fixture.ConfigureAuthority(
                    WorldChunkGroundSourceKind.DedicatedCollisionMesh,
                    "test-reviewed-dedicated-mesh-v1",
                    new Collider[] { collider },
                    Array.Empty<WorldChunkEdgeSafetyBinding>());

                AssertDiagnostic(
                    fixture.Evaluate(),
                    WorldChunkLoadFailureCodes.GroundRenderMeshReused);
            }
        }

        [Test]
        public void GroundThatDoesNotCoverChunkEnvelopeEdgesIsRejected()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                BoxCollider ground = fixture.AddGroundBox(4f);
                fixture.ConfigureAuthority(
                    WorldChunkGroundSourceKind.SolidColliderAssembly,
                    "test-reviewed-small-ground-v1",
                    new Collider[] { ground },
                    fixture.ContinuousEdges(ground));

                WorldChunkPhysicalGroundReadiness readiness = fixture.Evaluate();

                AssertDiagnostic(
                    readiness,
                    WorldChunkLoadFailureCodes.ChunkEdgeUnsafe);
                Assert.That(
                    readiness.Diagnostics.Count(value =>
                        value.Code == WorldChunkLoadFailureCodes.ChunkEdgeUnsafe),
                    Is.EqualTo(4));
            }
        }

        [Test]
        public void ReviewedSolidGroundCoveringAllCatalogSeamsIsReady()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                BoxCollider ground = fixture.AddGroundBox(fixture.ChunkSpanMeters);
                fixture.ConfigureAuthority(
                    WorldChunkGroundSourceKind.SolidColliderAssembly,
                    "test-reviewed-solid-ground-v1",
                    new Collider[] { ground },
                    fixture.ContinuousEdges(ground));

                WorldChunkPhysicalGroundReadiness readiness = fixture.Evaluate();

                Assert.That(
                    readiness.IsReady,
                    Is.True,
                    string.Join("\n", readiness.Diagnostics.Select(
                        value => value.Fingerprint)));
            }
        }

        [Test]
        public void LocalEdgeCoverageWithoutCrossSceneReceiptIsUnproven()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                BoxCollider ground = fixture.AddGroundBox(fixture.ChunkSpanMeters);
                WorldChunkEdgeSafetyBinding[] edges = fixture.ContinuousEdges(ground);
                edges[0] = new WorldChunkEdgeSafetyBinding(
                    WorldChunkEdge.North,
                    WorldChunkEdgeSafetyMode.ContinuousNeighbor,
                    fixture.CardinalNeighborId(0, 1),
                    ground);
                fixture.ConfigureAuthority(
                    WorldChunkGroundSourceKind.SolidColliderAssembly,
                    "test-reviewed-solid-ground-v1",
                    new Collider[] { ground },
                    edges);

                AssertDiagnostic(
                    fixture.Evaluate(),
                    WorldChunkLoadFailureCodes.ChunkSeamContinuityUnproven);
            }
        }

        [Test]
        public void NonConvexMeshRequiresExplicitCaveOrModularReviewMode()
        {
            using (var fixture = new ChunkSceneFixture())
            {
                var collisionObject = new GameObject("ReviewedNonConvexCollision");
                fixture.Adopt(collisionObject);
                var mesh = new Mesh { name = "TestClosedCollisionMesh" };
                try
                {
                    mesh.vertices = new[]
                    {
                        Vector3.zero,
                        Vector3.right,
                        Vector3.forward,
                        Vector3.up
                    };
                    mesh.triangles = new[]
                    {
                        0, 2, 1,
                        0, 1, 3,
                        1, 2, 3,
                        2, 0, 3
                    };
                    mesh.RecalculateBounds();
                    MeshCollider collider = collisionObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = false;
                    WorldChunkEdgeSafetyBinding[] reviewedEdges =
                        fixture.ReviewedPortalEdges(collider);
                    fixture.ConfigureAuthority(
                        WorldChunkGroundSourceKind.DedicatedCollisionMesh,
                        "test-reviewed-dedicated-mesh-v1",
                        new Collider[] { collider },
                        reviewedEdges);

                    AssertDiagnostic(
                        fixture.Evaluate(),
                        WorldChunkLoadFailureCodes.GroundColliderInvalid);

                    fixture.ConfigureAuthority(
                        WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision,
                        "test-reviewed-cave-collision-v1",
                        new Collider[] { collider },
                        reviewedEdges);
                    Assert.That(
                        fixture.Evaluate().IsReady,
                        Is.True,
                        "The explicit reviewed cave/modular escape hatch must remain available.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        private static void AssertDiagnostic(
            WorldChunkPhysicalGroundReadiness readiness,
            string code)
        {
            Assert.That(readiness.IsReady, Is.False);
            Assert.That(
                readiness.Diagnostics.Select(value => value.Code),
                Does.Contain(code),
                string.Join("\n", readiness.Diagnostics.Select(
                    value => value.Fingerprint)));
        }

        private sealed class ChunkSceneFixture : IDisposable
        {
            private readonly WorldStreamingSnapshot snapshot;
            private readonly WorldChunkDefinition chunk;
            private WorldChunkPhysicalGroundAuthority authority;

            internal ChunkSceneFixture()
            {
                snapshot = AcceptedSnapshot();
                chunk = snapshot.GetChunk("chunk_ring_slot_01_capital_core");
                WorldInstanceDefinition world = snapshot.GetWorld(chunk.WorldId);
                WorldDimensionDefinition dimension = snapshot.GetDimension(
                    world.DimensionId);
                ChunkSpanMeters = dimension.ChunkSpanMeters;
                Scene = EditorSceneManager.NewPreviewScene();
                RootObject = new GameObject("CatalogChunkRoot");
                SceneManager.MoveGameObjectToScene(RootObject, Scene);
                RootObject.transform.position = new Vector3(
                    chunk.GridX * ChunkSpanMeters,
                    0f,
                    chunk.GridZ * ChunkSpanMeters);
                WorldChunkRoot root = RootObject.AddComponent<WorldChunkRoot>();
                root.Configure(
                    dimension.Id,
                    world.Id,
                    chunk.Id,
                    chunk.BlockoutArchetype,
                    ChunkSpanMeters);
            }

            internal Scene Scene { get; }
            internal GameObject RootObject { get; }
            internal float ChunkSpanMeters { get; }

            internal void Adopt(GameObject value)
            {
                SceneManager.MoveGameObjectToScene(value, Scene);
                value.transform.SetParent(RootObject.transform, false);
            }

            internal BoxCollider AddGroundBox(float horizontalSize)
            {
                BoxCollider ground = RootObject.AddComponent<BoxCollider>();
                ground.center = new Vector3(0f, -1f, 0f);
                ground.size = new Vector3(horizontalSize, 2f, horizontalSize);
                Physics.SyncTransforms();
                return ground;
            }

            internal void ConfigureAuthority(
                WorldChunkGroundSourceKind sourceKind,
                string reviewReceiptId,
                IEnumerable<Collider> colliders,
                IEnumerable<WorldChunkEdgeSafetyBinding> edges)
            {
                if (authority == null)
                {
                    authority = RootObject
                        .AddComponent<WorldChunkPhysicalGroundAuthority>();
                }
                authority.Configure(
                    sourceKind,
                    reviewReceiptId,
                    colliders,
                    edges);
            }

            internal WorldChunkEdgeSafetyBinding[] ContinuousEdges(
                Collider ground)
            {
                return new[]
                {
                    ContinuousEdge(WorldChunkEdge.North, 0, 1, ground),
                    ContinuousEdge(WorldChunkEdge.East, 1, 0, ground),
                    ContinuousEdge(WorldChunkEdge.South, 0, -1, ground),
                    ContinuousEdge(WorldChunkEdge.West, -1, 0, ground)
                };
            }

            internal WorldChunkEdgeSafetyBinding[] ReviewedPortalEdges(
                Collider collider)
            {
                return ((WorldChunkEdge[])Enum.GetValues(typeof(WorldChunkEdge)))
                    .Select(edge => new WorldChunkEdgeSafetyBinding(
                        edge,
                        WorldChunkEdgeSafetyMode.ReviewedPortal,
                        string.Empty,
                        collider,
                        "test-reviewed-portal-v1"))
                    .ToArray();
            }

            internal WorldChunkPhysicalGroundReadiness Evaluate()
            {
                Physics.SyncTransforms();
                return WorldChunkPhysicalGroundValidator.Evaluate(
                    Scene,
                    snapshot,
                    chunk);
            }

            public void Dispose()
            {
                if (Scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(Scene);
                }
            }

            private WorldChunkEdgeSafetyBinding ContinuousEdge(
                WorldChunkEdge edge,
                int deltaX,
                int deltaZ,
                Collider ground)
            {
                return new WorldChunkEdgeSafetyBinding(
                    edge,
                    WorldChunkEdgeSafetyMode.ContinuousNeighbor,
                    CardinalNeighborId(deltaX, deltaZ),
                    ground,
                    "test-reviewed-continuous-seam-v1");
            }

            internal string CardinalNeighborId(int deltaX, int deltaZ)
            {
                return chunk.NeighborIds
                    .Select(snapshot.GetChunk)
                    .Where(value => value != null)
                    .Single(value =>
                        value.GridX - chunk.GridX == deltaX &&
                        value.GridZ - chunk.GridZ == deltaZ)
                    .Id;
            }

            private static WorldStreamingSnapshot AcceptedSnapshot()
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(
                    Application.dataPath,
                    "AL/StreamingAssets/GameData/al_world_streaming_catalog.json"));
                WorldStreamingLoadResult result =
                    WorldStreamingCatalogLoader.Validate(bytes);
                Assert.That(
                    result.Status,
                    Is.EqualTo(WorldStreamingLoadStatus.Accepted),
                    string.Join("\n", result.Diagnostics.Select(
                        value => value.Fingerprint)));
                return result.Snapshot;
            }
        }
    }
}
