using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.Data.Catalogs.WorldTerrain;
using AL.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace AL.Tests.EditMode.World
{
    public sealed class FirstSessionAuthoredWorldBuilderTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _spawned.Count; index++)
            {
                if (_spawned[index] != null)
                {
                    Object.DestroyImmediate(_spawned[index]);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void RuntimeCatalogAdmitsHallRiggedChampionGuardianPbrAndFourRealmStructures()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.HasRequiredAssets(), Is.True);
            Assert.That(catalog.CovenantHallPrefab, Is.Not.Null);
            Assert.That(catalog.CovenantHallPrefab
                .GetComponentsInChildren<MeshFilter>(true).Length, Is.GreaterThanOrEqualTo(10));
            Assert.That(catalog.ChampionBodyPrefab
                .GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.GreaterThan(0));
            Assert.That(catalog.GuardianPrefab
                .GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.GreaterThan(0));
            Assert.That(catalog.GuardianBaseColor.width, Is.EqualTo(1024));
            Assert.That(catalog.GuardianNormal.width, Is.EqualTo(1024));
            Assert.That(catalog.GuardianMetallic.width, Is.EqualTo(1024));
            Assert.That(catalog.GuardianRoughness.width, Is.EqualTo(1024));
            Assert.That(catalog.GuardianEmission.width, Is.EqualTo(1024));
            Assert.That(catalog.GuardianLocomotionClip, Is.Not.Null);
            Assert.That(catalog.GuardianLocomotionClip.length, Is.GreaterThan(0f));
            Assert.That(catalog.FloorMaterial.shader.name, Is.EqualTo("Standard"));
            Assert.That(catalog.WallMaterial.shader.name, Is.EqualTo("Standard"));
            Assert.That(catalog.TrimMaterial.shader.name, Is.EqualTo("Standard"));
            Assert.That(catalog.FirstSessionTerrainCatalog, Is.Not.Null);
            FirstSessionTerrainLoadResult terrainLoad =
                FirstSessionTerrainCatalogLoader.Validate(
                    catalog.FirstSessionTerrainCatalog.bytes);
            Assert.That(terrainLoad.IsAccepted, Is.True,
                terrainLoad.Diagnostics.Count == 0
                    ? string.Empty
                    : terrainLoad.Diagnostics[0].Fingerprint);

            foreach (RealmId realm in Realms())
            {
                Assert.That(catalog.TryResolveRealmVisual(realm, out FirstSessionRealmVisualAsset visual),
                    Is.True,
                    realm.ToString());
                Assert.That(visual.LandmarkPrefab.GetComponentInChildren<LODGroup>(true), Is.Not.Null);
                Assert.That(visual.PanoramicSky, Is.Not.Null, realm.ToString());
                Assert.That(
                    visual.PanoramicSky.width,
                    Is.EqualTo(visual.PanoramicSky.height * 2),
                    realm.ToString());
                Assert.That(
                    catalog.TryResolveFirstSessionRealm(realm, out GameObject realmPrefab),
                    Is.True,
                    realm.ToString());
                Assert.That(realmPrefab, Is.Not.Null, realm.ToString());
                FirstSessionAuthoredRealmRoute route =
                    realmPrefab.GetComponent<FirstSessionAuthoredRealmRoute>();
                Assert.That(route, Is.Not.Null, realm.ToString());
                Renderer landscape = route.transform
                    .Find(FirstSessionAuthoredRealmRoute.LandscapeName)
                    .GetComponent<Renderer>();
                Assert.That(
                    landscape.sharedMaterial.color.grayscale,
                    Is.GreaterThanOrEqualTo(0.18f),
                    realm + " landscape must remain readable under mobile lighting.");
                Assert.That(
                    realmPrefab.GetComponentsInChildren<Transform>(true)
                        .All(transform => !transform.name.Contains("TEMPORARY")),
                    Is.True,
                    realm.ToString());
            }
        }

        [Test]
        public void RuntimeCatalogResolvesMaleAndFemaleAuthoredChampionBases()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);

            foreach (string bodyBaseId in new[] { "male", "female" })
            {
                Assert.That(
                    catalog.TryResolveChampionBase(
                        bodyBaseId,
                        out GameObject prefab,
                        out AnimationClip locomotion),
                    Is.True,
                    bodyBaseId);
                Assert.That(prefab, Is.Not.Null, bodyBaseId);
                Assert.That(
                    prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    Is.GreaterThan(0),
                    bodyBaseId);
                Assert.That(locomotion, Is.Not.Null, bodyBaseId);
                Assert.That(locomotion.length, Is.GreaterThan(0f), bodyBaseId);
            }
        }

        [Test]
        public void AdmittedFirstSessionRealmPrefabsContainNoCompetingColliders()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);

            Assert.That(catalog, Is.Not.Null);
            foreach (RealmId realm in Realms())
            {
                Assert.That(
                    catalog.TryResolveFirstSessionRealm(realm, out GameObject prefab),
                    Is.True,
                    realm.ToString());
                Assert.That(prefab, Is.Not.Null, realm.ToString());
                Assert.That(
                    prefab.GetComponentsInChildren<Collider>(true),
                    Is.Empty,
                    realm + " prefab asset may not compete with TerrainCollider authority.");
            }
        }

        [Test]
        public void RuntimeAdmissionRejectsRealmPrefabWithAnyCompetingCollider()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryResolveRealmVisual(
                    RealmId.Crownlands,
                    out FirstSessionRealmVisualAsset visual),
                Is.True);
            System.Reflection.FieldInfo prefabField =
                typeof(FirstSessionRealmVisualAsset).GetField(
                    "firstSessionRealmPrefab",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.That(prefabField, Is.Not.Null);
            var original = (GameObject)prefabField.GetValue(visual);
            GameObject invalid = Object.Instantiate(original);
            invalid.name = "ColliderBearingFirstSessionRealmForTest";
            invalid.AddComponent<BoxCollider>();
            InnerRealmWorldBuildResult unexpected = null;
            try
            {
                prefabField.SetValue(visual, invalid);

                System.InvalidOperationException exception =
                    Assert.Throws<System.InvalidOperationException>(() =>
                        unexpected = FirstSessionAuthoredWorldBuilder.Build(
                            LoadLayout(),
                            "crownlands"));

                Assert.That(exception.Message, Does.Contain("competing Collider"));
            }
            finally
            {
                prefabField.SetValue(visual, original);
                if (unexpected?.Root != null)
                {
                    Object.DestroyImmediate(unexpected.Root.gameObject);
                }

                Object.DestroyImmediate(invalid);
            }
        }

        [Test]
        public void EveryFirstSessionRealmBuildsImportedStructuralIdentityWithoutPrimitiveMeshes()
        {
            InnerRealmWorldLayout layout = LoadLayout();
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            var structuralRoots = new HashSet<string>();

            foreach (RealmId realm in Realms())
            {
                string realmId = realm.ToString().ToLowerInvariant();
                InnerRealmWorldBuildResult built =
                    FirstSessionAuthoredWorldBuilder.Build(layout, realmId);
                _spawned.Add(built.Root.gameObject);

                FirstSessionAuthoredWorldMarker marker =
                    built.Root.GetComponent<FirstSessionAuthoredWorldMarker>();
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.Realm, Is.EqualTo(realm));
                Assert.That(catalog.TryResolveRealmVisual(realm, out FirstSessionRealmVisualAsset visual),
                    Is.True,
                    realm.ToString());
                Assert.That(RenderSettings.skybox, Is.Not.Null, realm.ToString());
                Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("Skybox/Panoramic"),
                    realm.ToString());
                Assert.That(RenderSettings.skybox.GetTexture("_MainTex"),
                    Is.SameAs(visual.PanoramicSky),
                    realm.ToString());
                if (realm == RealmId.Crownlands)
                {
                    Assert.That(
                        RenderSettings.skybox.GetFloat("_Exposure"),
                        Is.LessThanOrEqualTo(0.55f),
                        realm.ToString());
                }
                Assert.That(marker.ImportedRendererCount, Is.GreaterThanOrEqualTo(12));
                Assert.That(built.Root.name, Is.EqualTo(FirstSessionAuthoredWorldBuilder.RootName));
                Assert.That(built.Root.Find(FirstSessionAuthoredWorldBuilder.HallName), Is.Not.Null);

                string structuralName =
                    FirstSessionAuthoredWorldBuilder.StructuralIdentityPrefix + realm;
                Transform structural = built.Root.Find(structuralName);
                Assert.That(structural, Is.Not.Null, realm.ToString());
                Assert.That(structuralRoots.Add(structural.name), Is.True);
                Assert.That(built.Root.GetComponentsInChildren<Collider>(true).Length,
                    Is.GreaterThanOrEqualTo(1));

                Renderer[] representativeRenderers = built.Root
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .ToArray();
                Assert.That(representativeRenderers.Length, Is.LessThanOrEqualTo(200));
                Assert.That(CountTriangles(representativeRenderers), Is.LessThanOrEqualTo(500000));
                Assert.That(representativeRenderers
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Where(material => material != null)
                        .Distinct()
                        .Count(),
                    Is.LessThanOrEqualTo(24));

                MeshFilter[] filters = built.Root.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.GreaterThanOrEqualTo(10));
                Assert.That(filters.All(filter =>
                    filter.sharedMesh != null &&
                    !IsUnityPrimitive(filter.sharedMesh.name)),
                    Is.True,
                    realm.ToString());

                Object.DestroyImmediate(built.Root.gameObject);
                _spawned.RemoveAt(_spawned.Count - 1);
            }

            Assert.That(structuralRoots.Count, Is.EqualTo(4));
        }

        [Test]
        public void CanonicalTerrainCatalogAcceptsSealedLandmarkAndRejectsInvalidRanges()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            FirstSessionTerrainLoadResult accepted =
                FirstSessionTerrainCatalogLoader.Validate(
                    catalog.FirstSessionTerrainCatalog.bytes);

            Assert.That(accepted.IsAccepted, Is.True);
            Assert.That(accepted.Profile.SourceMode, Is.EqualTo("runtime_procedural_mvp"));
            Assert.That(
                accepted.Profile.FutureBakeContract,
                Is.EqualTo("terrain_data_height_slope_biome_splat_v1"));
            Assert.That(
                accepted.Profile.Navigation.TraversalProbeRadiusMeters,
                Is.LessThan(accepted.Profile.Generation.SafeCourtyardRadiusMeters));
            Assert.That(
                accepted.Profile.Collision.LandmarkEntranceWidthMeters,
                Is.Zero,
                "The admitted imported landmarks have visually closed doors, so their provisional front collision must be explicitly sealed.");

            string invalid = catalog.FirstSessionTerrainCatalog.text.Replace(
                "\"heightmapResolution\": 65",
                "\"heightmapResolution\": 62");
            FirstSessionTerrainLoadResult rejected =
                FirstSessionTerrainCatalogLoader.Validate(
                    Encoding.UTF8.GetBytes(invalid));
            Assert.That(rejected.IsAccepted, Is.False);
            Assert.That(rejected.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "AL-TERRAIN-RESOLUTION-INVALID"), Is.True);

            string negativeEntrance = catalog.FirstSessionTerrainCatalog.text.Replace(
                "\"landmarkEntranceWidthMeters\": 0",
                "\"landmarkEntranceWidthMeters\": -1");
            FirstSessionTerrainLoadResult rejectedEntrance =
                FirstSessionTerrainCatalogLoader.Validate(
                    Encoding.UTF8.GetBytes(negativeEntrance));
            Assert.That(rejectedEntrance.IsAccepted, Is.False);
            Assert.That(rejectedEntrance.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "AL-TERRAIN-RANGE-INVALID" &&
                diagnostic.Path ==
                    "$.profile.collision.landmarkEntranceWidthMeters"), Is.True);
        }

        [Test]
        public void EveryFirstSessionRealmBuildsRealTerrainColliderAndBoundedSafeCourtyard()
        {
            InnerRealmWorldLayout layout = LoadLayout();
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            FirstSessionTerrainLoadResult terrainLoad =
                FirstSessionTerrainCatalogLoader.Validate(
                    catalog.FirstSessionTerrainCatalog.bytes);
            Assert.That(terrainLoad.IsAccepted, Is.True);
            FirstSessionTerrainProfile profile = terrainLoad.Profile;

            foreach (RealmId realm in Realms())
            {
                string realmId = realm.ToString().ToLowerInvariant();
                InnerRealmWorldBuildResult built =
                    FirstSessionAuthoredWorldBuilder.Build(layout, realmId);
                _spawned.Add(built.Root.gameObject);
                Physics.SyncTransforms();

                Vector3 spawn = built.PlayerSpawn;
                Transform terrainTransform = built.Root.Find(
                    FirstSessionAuthoredWorldBuilder.TerrainName);
                Assert.That(terrainTransform, Is.Not.Null,
                    realm + " has no physical Unity Terrain.");
                Terrain terrain = terrainTransform.GetComponent<Terrain>();
                TerrainCollider terrainCollider =
                    terrainTransform.GetComponent<TerrainCollider>();
                Assert.That(terrain, Is.Not.Null, realm.ToString());
                Assert.That(terrainCollider, Is.Not.Null, realm.ToString());
                Assert.That(terrainCollider.isTrigger, Is.False, realm.ToString());
                Assert.That(terrainCollider.terrainData, Is.SameAs(terrain.terrainData));
                Assert.That(
                    built.Root.GetComponentsInChildren<Terrain>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    built.Root.GetComponentsInChildren<TerrainCollider>(true),
                    Has.Length.EqualTo(1));

                Assert.That(
                    terrain.terrainData.size.x,
                    Is.EqualTo(profile.Dimensions.SizeXMeters).Within(0.001f));
                Assert.That(
                    terrain.terrainData.size.y,
                    Is.EqualTo(profile.Dimensions.HeightMeters).Within(0.001f));
                Assert.That(
                    terrain.terrainData.size.z,
                    Is.EqualTo(profile.Dimensions.SizeZMeters).Within(0.001f));
                Assert.That(
                    terrain.terrainData.heightmapResolution,
                    Is.EqualTo(profile.Dimensions.HeightmapResolution));
                Assert.That(
                    terrainTransform.position.x + terrain.terrainData.size.x * 0.5f,
                    Is.EqualTo(built.WalkableInner.CapitalPosition.x).Within(0.001f));
                Assert.That(
                    terrainTransform.position.z + terrain.terrainData.size.z * 0.5f,
                    Is.EqualTo(built.WalkableInner.CapitalPosition.z).Within(0.001f));

                FirstSessionTerrainRuntimeMarker marker =
                    terrainTransform.GetComponent<FirstSessionTerrainRuntimeMarker>();
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.ProfileId, Is.EqualTo(profile.Id));
                Assert.That(marker.GenerationSeed, Is.EqualTo(profile.Generation.Seed));
                Assert.That(marker.ReplacementSocketId,
                    Is.EqualTo(profile.ReplacementSocketId));
                Assert.That(
                    built.Root.Find(profile.ReplacementSocketId),
                    Is.Not.Null,
                    realm + " has no terrain replacement socket.");

                TerrainLayer[] layers = terrain.terrainData.terrainLayers;
                Assert.That(layers, Has.Length.EqualTo(1));
                Assert.That(layers[0].diffuseTexture.width,
                    Is.EqualTo(profile.Surface.TextureResolution));
                Assert.That(layers[0].diffuseTexture.height,
                    Is.EqualTo(profile.Surface.TextureResolution));
                Assert.That(layers[0].diffuseTexture.wrapMode,
                    Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(layers[0].tileSize.x,
                    Is.EqualTo(profile.Surface.TileSizeMeters).Within(0.001f));

                BoxCollider[] boxColliders = built.Root
                    .GetComponentsInChildren<BoxCollider>(true);
                Assert.That(
                    boxColliders.Any(collider => collider.bounds.Contains(spawn)),
                    Is.False,
                    realm + " player spawn may not begin inside collision.");
                Assert.That(
                    boxColliders.Any(collider =>
                        collider.bounds.min.x <= spawn.x &&
                        collider.bounds.max.x >= spawn.x &&
                        collider.bounds.min.z <= spawn.z &&
                        collider.bounds.max.z >= spawn.z &&
                        collider.bounds.max.y < spawn.y),
                    Is.False,
                    realm + " may not disguise a box collider as its terrain floor.");

                Vector3[] directions =
                {
                    Vector3.forward,
                    Vector3.back,
                    Vector3.left,
                    Vector3.right,
                    new Vector3(1f, 0f, 1f).normalized,
                    new Vector3(-1f, 0f, 1f).normalized,
                    new Vector3(1f, 0f, -1f).normalized,
                    new Vector3(-1f, 0f, -1f).normalized
                };
                foreach (Vector3 direction in directions)
                {
                    Vector3 probe = built.WalkableInner.CapitalPosition +
                                    direction *
                                    profile.Navigation.TraversalProbeRadiusMeters;
                    // Terrain.SampleHeight returns height relative to the Terrain
                    // object's Y origin, not an absolute world-space Y value.
                    float sampledHeight = terrain.transform.position.y +
                                          terrain.SampleHeight(probe);
                    Assert.That(
                        sampledHeight,
                        Is.EqualTo(built.WalkableInner.CapitalPosition.y).Within(0.015f),
                        realm + " safe courtyard is not flat at " + direction + ".");
                    var ray = new Ray(probe + Vector3.up * 8f, Vector3.down);
                    Assert.That(
                        terrainCollider.Raycast(ray, out RaycastHit hit, 20f),
                        Is.True,
                        realm + " TerrainCollider missed " + direction + ".");
                    Assert.That(
                        hit.point.y,
                        Is.EqualTo(built.WalkableInner.CapitalPosition.y).Within(0.015f));
                }

                Transform collisionRoot = built.Root.Find(
                    profile.Navigation.CollisionCollectionName);
                Assert.That(collisionRoot, Is.Not.Null);
                Assert.That(
                    collisionRoot.GetComponentsInChildren<BoxCollider>(true)
                        .Count(collider =>
                            collider.name.StartsWith(
                                "COL_FirstSessionTerrainBoundary_")),
                    Is.EqualTo(4));

                Transform structural = built.Root.Find(
                    FirstSessionAuthoredWorldBuilder.StructuralIdentityPrefix + realm);
                Bounds landmarkBounds = CalculateBounds(structural.gameObject);
                Assert.That(
                    landmarkBounds.min.y,
                    Is.EqualTo(built.WalkableInner.CapitalPosition.y).Within(0.01f),
                    realm + " authored world must be grounded independently of spawn height.");
                Assert.That(
                    landmarkBounds.min.z,
                    Is.EqualTo(
                        built.WalkableInner.CapitalPosition.z +
                        profile.Placement.LandmarkFrontOffsetMeters).Within(0.015f),
                    realm + " landmark front must align to the catalogued threshold.");
                Transform compound = collisionRoot.Find(
                    FirstSessionAuthoredWorldBuilder.LandmarkCollisionRootName);
                Assert.That(compound, Is.Not.Null);
                BoxCollider[] compoundColliders =
                    compound.GetComponentsInChildren<BoxCollider>(true);
                Assert.That(
                    compoundColliders.Length,
                    Is.GreaterThanOrEqualTo(4),
                    realm + " landmark collision has only " +
                    compoundColliders.Length + " proxies for visible width " +
                    landmarkBounds.size.x + "m.");
                Assert.That(compoundColliders.All(collider =>
                    collider.name.StartsWith("COL_")), Is.True);
                if (profile.Collision.LandmarkEntranceWidthMeters <= Mathf.Epsilon)
                {
                    BoxCollider sealedFront = compoundColliders.Single(collider =>
                        collider.name == "COL_Landmark_Front");
                    Assert.That(
                        sealedFront.bounds.size.x,
                        Is.EqualTo(landmarkBounds.size.x).Within(0.02f),
                        realm + " sealed landmark front must cover its full visible width.");
                    Assert.That(
                        compoundColliders.Any(collider =>
                            collider.name == "COL_Landmark_FrontLeft" ||
                            collider.name == "COL_Landmark_FrontRight"),
                        Is.False,
                        realm + " sealed landmark may not retain an invisible portal seam.");
                }
                else
                {
                    BoxCollider frontLeft = compoundColliders.Single(collider =>
                        collider.name == "COL_Landmark_FrontLeft");
                    BoxCollider frontRight = compoundColliders.Single(collider =>
                        collider.name == "COL_Landmark_FrontRight");
                    Assert.That(
                        frontRight.bounds.min.x - frontLeft.bounds.max.x,
                        Is.EqualTo(profile.Collision.LandmarkEntranceWidthMeters)
                            .Within(0.02f),
                        realm + " compound proxy must preserve its entrance seam.");
                }
                Assert.That(
                    built.Root.GetComponentsInChildren<MeshCollider>(true),
                    Is.Empty,
                    realm + " may not cook stripped imported meshes at runtime.");

                TerrainData generatedTerrainData = terrain.terrainData;
                TerrainLayer generatedTerrainLayer = layers[0];
                Texture2D generatedGridTexture = layers[0].diffuseTexture;
                Object.DestroyImmediate(built.Root.gameObject);
                _spawned.RemoveAt(_spawned.Count - 1);
                Assert.That(generatedTerrainData == null, Is.True,
                    realm + " runtime TerrainData leaked after world teardown.");
                Assert.That(generatedTerrainLayer == null, Is.True,
                    realm + " runtime TerrainLayer leaked after world teardown.");
                Assert.That(generatedGridTexture == null, Is.True,
                    realm + " runtime terrain grid texture leaked after world teardown.");
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        [Test]
        public void FirstSessionAuthoredRealmUsesCompleteRouteAboveSoleTerrainGround()
        {
            InnerRealmWorldLayout layout = LoadLayout();
            InnerRealmWorldBuildResult built =
                FirstSessionAuthoredWorldBuilder.Build(layout, "crownlands");
            _spawned.Add(built.Root.gameObject);

            Transform hall = built.Root.Find(FirstSessionAuthoredWorldBuilder.HallName);
            Assert.That(hall, Is.Not.Null);
            FirstSessionAuthoredRealmRoute route =
                hall.GetComponentInChildren<FirstSessionAuthoredRealmRoute>(true);
            Assert.That(route, Is.Not.Null);
            Assert.That(route.HasCompleteRoute(), Is.True);
            Assert.That(
                HorizontalDistance(route.PlayerSpawn.position, built.PlayerSpawn),
                Is.LessThan(0.01f));
            Assert.That(
                HorizontalDistance(route.PlayerSpawn.position, route.LordshipDestination.position),
                Is.GreaterThanOrEqualTo(70f));

            Renderer landscape = hall.GetComponentsInChildren<Renderer>(true)
                .Single(renderer =>
                    renderer.transform.name == FirstSessionAuthoredRealmRoute.LandscapeName);
            Assert.That(
                landscape.enabled,
                Is.False,
                "The imported landscape is presentation reference only; real Terrain owns ground.");
            Assert.That(hall.GetComponentsInChildren<MeshCollider>(true), Is.Empty);

            Terrain terrain = built.Root.GetComponentsInChildren<Terrain>(true).Single();
            TerrainCollider terrainCollider =
                built.Root.GetComponentsInChildren<TerrainCollider>(true).Single();
            Assert.That(terrainCollider.terrainData, Is.SameAs(terrain.terrainData));
            foreach (Transform anchor in new[]
            {
                route.PlayerSpawn,
                route.CaptainValerius,
                route.GuardianTrial,
                route.CovenantSite,
                route.LordshipDestination
            })
            {
                float terrainY = terrain.SampleHeight(anchor.position) +
                                 terrain.transform.position.y;
                Assert.That(
                    Mathf.Abs(anchor.position.y - terrainY),
                    Is.LessThanOrEqualTo(0.05f),
                    "Authored route anchor is not grounded: " + anchor.name);
            }

            Assert.That(
                built.Root.GetComponentsInChildren<Transform>(true).Any(transform =>
                    transform.name.StartsWith("FloorModule_Courtyard")),
                Is.False);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static bool IsUnityPrimitive(string name)
        {
            return name == "Cube" || name == "Sphere" || name == "Capsule" ||
                   name == "Cylinder" || name == "Plane" || name == "Quad";
        }

        private static long CountTriangles(IEnumerable<Renderer> renderers)
        {
            long triangles = 0;
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter == null ? null : filter.sharedMesh;
                }
                if (mesh != null)
                {
                    triangles += mesh.triangles.LongLength / 3;
                }
            }

            return triangles;
        }

        private static RealmId[] Realms()
        {
            return new[]
            {
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Crownlands,
                RealmId.Umbral
            };
        }

        private static InnerRealmWorldLayout LoadLayout()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);
            Assert.That(result.IsAccepted, Is.True);
            return InnerRealmWorldLayout.FromSnapshot(result.Snapshot);
        }
    }
}
