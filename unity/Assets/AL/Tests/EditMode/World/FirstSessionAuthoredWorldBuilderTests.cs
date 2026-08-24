using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.World;
using NUnit.Framework;
using UnityEngine;

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
                    Is.GreaterThanOrEqualTo(2));

                Renderer[] representativeRenderers = built.Root
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .ToArray();
                Assert.That(representativeRenderers.Length, Is.LessThanOrEqualTo(6));
                Assert.That(CountTriangles(representativeRenderers), Is.LessThanOrEqualTo(12000));
                Assert.That(representativeRenderers
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Where(material => material != null)
                        .Distinct()
                        .Count(),
                    Is.LessThanOrEqualTo(5));

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
