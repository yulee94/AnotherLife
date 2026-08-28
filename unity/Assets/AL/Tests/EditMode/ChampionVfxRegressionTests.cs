using System;
using System.Linq;
using System.Reflection;
using AL.World.Streaming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public class ChampionVfxRegressionTests
    {
        [Test]
        public void BurstParticleReuseStopsBeforeDurationReconfiguration()
        {
            var effect = new GameObject("VFX_Test_PlayingBurst");
            try
            {
                var particles = effect.AddComponent<ParticleSystem>();
                particles.Play(true);
                Assert.True(particles.isPlaying);

                MethodInfo resetMethod = GetRuntimeType("AL.ChampionMode.Skills.SkillEffectFactory").GetMethod(
                    "ResetBurstParticlesForReuse",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(resetMethod, "Expected burst particle reset helper.");

                resetMethod.Invoke(null, new object[] { particles });

                Assert.False(particles.isPlaying);
                Assert.AreEqual(0, particles.particleCount);

                var main = particles.main;
                main.duration = 0.75f;
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(effect);
            }
        }

        [Test]
        public void BurstParticlesUsePackagedSupportedSoftMaterialInsteadOfErrorFallback()
        {
            MethodInfo createMethod = GetRuntimeType("AL.ChampionMode.Skills.SkillEffectFactory").GetMethod(
                "CreateBurstObject",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(createMethod, "Expected burst creation helper.");

            var effect = (GameObject)createMethod.Invoke(null, new object[] { "VFX_Test_SoftBurst" });
            try
            {
                Assert.NotNull(effect);
                var renderer = effect.GetComponent<ParticleSystemRenderer>();
                Assert.NotNull(renderer);
                Assert.True(renderer.enabled, "Supported packaged shader should keep burst rendering enabled.");
                Assert.NotNull(renderer.sharedMaterial);
                Assert.NotNull(renderer.sharedMaterial.shader);
                Assert.AreEqual("AnotherLife/Runtime/SoftParticle", renderer.sharedMaterial.shader.name);
                Assert.True(renderer.sharedMaterial.shader.isSupported);
                StringAssert.DoesNotContain("InternalErrorShader", renderer.sharedMaterial.shader.name);

                Material initialMaterial = renderer.sharedMaterial;
                Assert.True(AL.Utilities.RuntimeParticleMaterialFactory.EnsureSoftMaterial(
                    effect.GetComponent<ParticleSystem>(),
                    "MAT_RuntimeCombatBurst"));
                Assert.AreSame(
                    initialMaterial,
                    renderer.sharedMaterial,
                    "Reconfiguration should reuse the bounded pooled material instead of allocating again.");

                var texture = renderer.sharedMaterial.mainTexture as Texture2D;
                Assert.NotNull(texture, "Burst material should own its bounded soft particle texture.");
                Assert.AreEqual(32, texture.width);
                Assert.AreEqual(32, texture.height);
                Assert.Greater(texture.GetPixel(16, 16).a, 0.9f);
                Assert.Less(texture.GetPixel(0, 0).a, 0.01f);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(effect);
            }
        }

        [TestCase("CreateGroundRingObject", null)]
        [TestCase("CreatePrimitiveEffectObject", PrimitiveType.Sphere)]
        [TestCase("CreateTrailObject", null)]
        public void RuntimeVisualFactoriesHaveNoPhysicsAuthority(
            string methodName,
            PrimitiveType? primitiveType)
        {
            Type factoryType = GetRuntimeType("AL.ChampionMode.Skills.SkillEffectFactory");
            MethodInfo createMethod = factoryType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(createMethod, "Expected runtime VFX factory " + methodName + ".");

            object[] arguments = primitiveType.HasValue
                ? new object[] { primitiveType.Value }
                : Array.Empty<object>();
            var effect = (GameObject)createMethod.Invoke(null, arguments);
            try
            {
                Assert.NotNull(effect);
                Collider collider = effect.GetComponent<Collider>();
                Assert.That(
                    collider == null || !collider.enabled,
                    Is.True,
                    "A presentation-only effect must be absent from physics immediately, not one frame later.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(effect);
            }
        }

        [Test]
        public void RuntimeGroundingUsesElevatedTerrainAndSkipsActorCollision()
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(16f, 6f, 16f)
            };
            var heights = new float[33, 33];
            for (int z = 0; z < heights.GetLength(0); z++)
            {
                for (int x = 0; x < heights.GetLength(1); x++)
                {
                    heights[z, x] = 0.5f;
                }
            }
            terrainData.SetHeights(0, 0, heights);

            GameObject terrain = Terrain.CreateTerrainGameObject(terrainData);
            var actor = new GameObject("VFX_GroundProbeActor");
            var actorNoise = new GameObject[24];
            try
            {
                terrain.transform.position = new Vector3(0f, 2f, 0f);
                actor.transform.position = new Vector3(8f, 5f, 8f);
                var actorCollider = actor.AddComponent<CharacterController>();
                actorCollider.height = 2f;
                actorCollider.center = Vector3.up;

                // Saturate the bounded non-alloc physics buffer with gameplay actors.
                // Terrain sampling must remain authoritative even when the ray query
                // cannot return every collider and the caller flattened Y to zero.
                for (int index = 0; index < actorNoise.Length; index++)
                {
                    GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dummy.name = "Dummy_GroundingNoise_" + index;
                    dummy.transform.position = new Vector3(8f, 5.08f + index * 0.025f, 8f);
                    dummy.transform.localScale = new Vector3(1.5f, 0.015f, 1.5f);
                    actorNoise[index] = dummy;
                }

                Physics.SyncTransforms();

                MethodInfo groundedMethod = GetRuntimeType(
                        "AL.ChampionMode.Skills.SkillEffectFactory")
                    .GetMethod("Grounded", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(groundedMethod);

                var grounded = (Vector3)groundedMethod.Invoke(
                    null,
                    new object[] { new Vector3(8f, 0f, 8f) });

                Assert.That(grounded.x, Is.EqualTo(8f).Within(0.001f));
                Assert.That(grounded.z, Is.EqualTo(8f).Within(0.001f));
                Assert.That(
                    grounded.y,
                    Is.EqualTo(5.05f).Within(0.02f),
                    "Skill telegraphs must follow elevated TerrainCollider support instead of world Y=0 or actor capsules.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                for (int index = 0; index < actorNoise.Length; index++)
                {
                    UnityEngine.Object.DestroyImmediate(actorNoise[index]);
                }

                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(terrain);
                UnityEngine.Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void RuntimeGroundingAcceptsStaticModularFloorAboveTerrain()
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(16f, 4f, 16f)
            };
            terrainData.SetHeights(0, 0, new float[33, 33]);

            GameObject terrain = Terrain.CreateTerrainGameObject(terrainData);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var actorNoise = new GameObject[24];
            try
            {
                terrain.transform.position = new Vector3(0f, 2f, 0f);
                floor.name = "ModularBridgeCollision";
                floor.transform.position = new Vector3(8f, 6.75f, 8f);
                floor.transform.localScale = new Vector3(4f, 0.5f, 4f);
                var authority = floor.AddComponent<WorldChunkPhysicalGroundAuthority>();
                authority.Configure(
                    WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision,
                    "TEST-MODULAR-BRIDGE-COLLISION-REVIEW",
                    new[] { floor.GetComponent<Collider>() },
                    Array.Empty<WorldChunkEdgeSafetyBinding>());
                for (int index = 0; index < actorNoise.Length; index++)
                {
                    GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dummy.name = "UnboundActorCollision_" + index;
                    dummy.transform.position = new Vector3(8f, 7.08f + index * 0.025f, 8f);
                    dummy.transform.localScale = new Vector3(1.5f, 0.015f, 1.5f);
                    actorNoise[index] = dummy;
                }
                Physics.SyncTransforms();

                MethodInfo groundedMethod = GetRuntimeType(
                        "AL.ChampionMode.Skills.SkillEffectFactory")
                    .GetMethod("Grounded", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(groundedMethod);

                var grounded = (Vector3)groundedMethod.Invoke(
                    null,
                    new object[] { new Vector3(8f, 7f, 8f) });

                Assert.That(
                    grounded.y,
                    Is.EqualTo(7.05f).Within(0.02f),
                    "Static modular floors may ground telegraphs above the TerrainCollider authority.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                for (int index = 0; index < actorNoise.Length; index++)
                {
                    UnityEngine.Object.DestroyImmediate(actorNoise[index]);
                }

                UnityEngine.Object.DestroyImmediate(floor);
                UnityEngine.Object.DestroyImmediate(terrain);
                UnityEngine.Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void RuntimeGroundingDoesNotSnapUpToAuthorizedCeiling()
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(16f, 6f, 16f)
            };
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++)
            {
                for (int x = 0; x < 33; x++)
                {
                    heights[z, x] = 0.5f;
                }
            }
            terrainData.SetHeights(0, 0, heights);

            GameObject terrain = Terrain.CreateTerrainGameObject(terrainData);
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                terrain.transform.position = new Vector3(0f, 2f, 0f);
                ceiling.name = "AuthorizedCeilingCollision";
                ceiling.transform.position = new Vector3(8f, 8.75f, 8f);
                ceiling.transform.localScale = new Vector3(4f, 0.5f, 4f);
                var authority = ceiling.AddComponent<WorldChunkPhysicalGroundAuthority>();
                authority.Configure(
                    WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision,
                    "TEST-CEILING-COLLISION-REVIEW",
                    new[] { ceiling.GetComponent<Collider>() },
                    Array.Empty<WorldChunkEdgeSafetyBinding>());
                Physics.SyncTransforms();

                Vector3 grounded = InvokeGrounded(new Vector3(8f, 5f, 8f));

                Assert.That(grounded.y, Is.EqualTo(5.05f).Within(0.02f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ceiling);
                UnityEngine.Object.DestroyImmediate(terrain);
                UnityEngine.Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void RuntimeGroundingUsesReviewedCaveFloorThroughTerrainHole()
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(16f, 6f, 16f)
            };
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++)
            {
                for (int x = 0; x < 33; x++)
                {
                    heights[z, x] = 0.5f;
                }
            }
            terrainData.SetHeights(0, 0, heights);
            var holes = new bool[terrainData.holesResolution, terrainData.holesResolution];
            for (int z = 0; z < terrainData.holesResolution; z++)
            {
                for (int x = 0; x < terrainData.holesResolution; x++)
                {
                    holes[z, x] = true;
                }
            }
            int center = terrainData.holesResolution / 2;
            holes[center, center] = false;
            terrainData.SetHoles(0, 0, holes);

            GameObject terrain = Terrain.CreateTerrainGameObject(terrainData);
            GameObject caveFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                terrain.transform.position = new Vector3(0f, 2f, 0f);
                caveFloor.name = "ReviewedCaveFloorCollision";
                caveFloor.transform.position = new Vector3(8f, 0.75f, 8f);
                caveFloor.transform.localScale = new Vector3(3f, 0.5f, 3f);
                var authority = caveFloor.AddComponent<WorldChunkPhysicalGroundAuthority>();
                authority.Configure(
                    WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision,
                    "TEST-CAVE-FLOOR-COLLISION-REVIEW",
                    new[] { caveFloor.GetComponent<Collider>() },
                    Array.Empty<WorldChunkEdgeSafetyBinding>());
                Physics.SyncTransforms();

                Vector3 grounded = InvokeGrounded(new Vector3(8f, 1f, 8f));

                Assert.That(
                    grounded.y,
                    Is.EqualTo(1.05f).Within(0.02f),
                    "Terrain height samples over holes must not hide reviewed cave collision.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(caveFloor);
                UnityEngine.Object.DestroyImmediate(terrain);
                UnityEngine.Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void RuntimeGroundingRefreshesOverlappingStreamedTerrains()
        {
            TerrainData lowData = FlatTerrainData(16f, 4f, 0f);
            GameObject low = Terrain.CreateTerrainGameObject(lowData);
            TerrainData highData = null;
            GameObject high = null;
            try
            {
                Assert.That(
                    InvokeGrounded(new Vector3(8f, 0f, 8f)).y,
                    Is.EqualTo(0.05f).Within(0.02f));

                highData = FlatTerrainData(16f, 4f, 0f);
                high = Terrain.CreateTerrainGameObject(highData);
                high.transform.position = new Vector3(0f, 4f, 0f);
                Physics.SyncTransforms();

                Assert.That(
                    InvokeGrounded(new Vector3(8f, 0f, 8f)).y,
                    Is.EqualTo(4.05f).Within(0.02f),
                    "An overlapping streamed tile must be visible immediately instead of losing to a stale terrain cache.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(high);
                UnityEngine.Object.DestroyImmediate(highData);
                UnityEngine.Object.DestroyImmediate(low);
                UnityEngine.Object.DestroyImmediate(lowData);
            }
        }

        private static TerrainData FlatTerrainData(
            float width,
            float height,
            float normalizedHeight)
        {
            var data = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(width, height, width)
            };
            var heights = new float[33, 33];
            if (normalizedHeight > 0f)
            {
                for (int z = 0; z < 33; z++)
                {
                    for (int x = 0; x < 33; x++)
                    {
                        heights[z, x] = normalizedHeight;
                    }
                }
            }
            data.SetHeights(0, 0, heights);
            return data;
        }

        private static Vector3 InvokeGrounded(Vector3 position)
        {
            MethodInfo groundedMethod = GetRuntimeType(
                    "AL.ChampionMode.Skills.SkillEffectFactory")
                .GetMethod("Grounded", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(groundedMethod);
            return (Vector3)groundedMethod.Invoke(null, new object[] { position });
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Expected loaded runtime type {typeName}.");
            return type;
        }
    }
}
