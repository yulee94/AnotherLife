using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Tests.EditMode.Terrestrials
{
    public sealed class SlagfallEnvironmentKitProductionTests
    {
        private const string EnvironmentRoot =
            "Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Environment";
        private const string ModelRoot = EnvironmentRoot + "/Models";
        private const string TextureRoot = EnvironmentRoot + "/Textures";
        private const string PrefabRoot =
            EnvironmentRoot + "/Prefabs";
        private const string MaterialPath =
            EnvironmentRoot +
            "/Materials/tdf_mat_stonehold_slagfall_environment_atlas_v001.mat";
        private const string ReviewScenePath =
            "Assets/AL/Scenes/Review/Terrestrials/SlagfallEnvironmentKitReview.unity";

        private static readonly string[] FamilyIds =
        {
            "irregular_fracture_raft",
            "broken_fracture_raft",
            "undercut_extraction_ledge",
            "talus_apron",
            "collapsed_gallery_mouth",
            "diagonal_fault_slab",
            "braided_runoff_pool",
            "iron_soil_wedge"
        };

        [Test]
        public void EachFamilyOwnsFourLodPrefab()
        {
            Assert.That(FamilyIds, Has.Length.EqualTo(8));
            foreach (string familyId in FamilyIds)
            {
                string path = $"{PrefabRoot}/tdf_prop_stonehold_slagfall_{familyId}_v001.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);

                LODGroup group = prefab.GetComponent<LODGroup>();
                Assert.That(group, Is.Not.Null, familyId);
                LOD[] lods = group.GetLODs();
                Assert.That(lods, Has.Length.EqualTo(4), familyId);
                Assert.That(
                    Array.ConvertAll(lods, lod => lod.renderers.Length),
                    Is.EqualTo(new[] { 1, 1, 1, 1 }),
                    familyId);
            }
        }

        [Test]
        public void SharedMaterialUsesStandardPbrAtlases()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Standard"));
            Assert.That(material.enableInstancing, Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(material.GetTexture("_MainTex")),
                Is.EqualTo(
                    TextureRoot +
                    "/tdf_atlas_stonehold_slagfall_environment_basecolor_v001.png"));
            Assert.That(
                AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")),
                Is.EqualTo(
                    TextureRoot +
                    "/tdf_atlas_stonehold_slagfall_environment_normal_v001.png"));
            Assert.That(
                AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")),
                Is.EqualTo(
                    TextureRoot +
                    "/tdf_atlas_stonehold_slagfall_environment_metallic_smoothness_v001.png"));
        }

        [Test]
        public void EachPrefabUsesLowestLodForStaticCollision()
        {
            foreach (string familyId in FamilyIds)
            {
                GameObject prefab = RequirePrefab(familyId);
                LOD[] lods = prefab.GetComponent<LODGroup>().GetLODs();
                Mesh lowestLod = lods[3].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                MeshCollider collider = prefab.GetComponent<MeshCollider>();

                Assert.That(collider, Is.Not.Null, familyId);
                Assert.That(collider.sharedMesh, Is.SameAs(lowestLod), familyId);
                Assert.That(collider.convex, Is.False, familyId);
                Assert.That(collider.isTrigger, Is.False, familyId);

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(prefab);
                Assert.That(flags.HasFlag(StaticEditorFlags.BatchingStatic), Is.True, familyId);
                Assert.That(flags.HasFlag(StaticEditorFlags.OccludeeStatic), Is.True, familyId);
                Assert.That(flags.HasFlag(StaticEditorFlags.NavigationStatic), Is.False, familyId);
            }
        }

        [Test]
        public void LodTriangleRatiosStayWithinProfilingBudget()
        {
            foreach (string familyId in FamilyIds)
            {
                LOD[] lods = RequirePrefab(familyId).GetComponent<LODGroup>().GetLODs();
                long lod0 = TriangleCount(lods[0].renderers[0].GetComponent<MeshFilter>().sharedMesh);
                long lod1 = TriangleCount(lods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh);
                long lod2 = TriangleCount(lods[2].renderers[0].GetComponent<MeshFilter>().sharedMesh);
                long lod3 = TriangleCount(lods[3].renderers[0].GetComponent<MeshFilter>().sharedMesh);

                Assert.That(lod0, Is.InRange(7000L, 12000L), familyId);
                Assert.That((double)lod1 / lod0, Is.InRange(0.50d, 0.60d), familyId);
                Assert.That((double)lod2 / lod0, Is.InRange(0.20d, 0.30d), familyId);
                Assert.That((double)lod3 / lod0, Is.InRange(0.05d, 0.10d), familyId);
            }
        }

        [Test]
        public void ModelImportersAreCompressedAndGpuOnly()
        {
            foreach (string familyId in FamilyIds)
            {
                ModelImporter importer = AssetImporter.GetAtPath(ModelPath(familyId)) as ModelImporter;
                Assert.That(importer, Is.Not.Null, familyId);
                Assert.That(importer.meshCompression, Is.EqualTo(ModelImporterMeshCompression.Medium));
                Assert.That(importer.isReadable, Is.False, familyId);
                Assert.That(importer.importAnimation, Is.False, familyId);
                Assert.That(
                    importer.materialImportMode,
                    Is.EqualTo(ModelImporterMaterialImportMode.None),
                    familyId);
            }
        }

        [Test]
        public void ImportedMeshesUseUnityYAsHeight()
        {
            foreach (string familyId in FamilyIds)
            {
                Mesh mesh = RequirePrefab(familyId)
                    .GetComponent<LODGroup>()
                    .GetLODs()[0]
                    .renderers[0]
                    .GetComponent<MeshFilter>()
                    .sharedMesh;
                Vector3 size = mesh.bounds.size;

                Assert.That(
                    Mathf.Max(size.x, size.z),
                    Is.EqualTo(4f).Within(0.02f),
                    familyId);
                Assert.That(size.y, Is.LessThanOrEqualTo(1.5f), familyId);
            }
        }

        [Test]
        public void AtlasImportersUseStreamingMipContract()
        {
            TextureImporter baseColor = RequireTextureImporter(
                TextureRoot + "/tdf_atlas_stonehold_slagfall_environment_basecolor_v001.png");
            TextureImporter normal = RequireTextureImporter(
                TextureRoot + "/tdf_atlas_stonehold_slagfall_environment_normal_v001.png");
            TextureImporter packed = RequireTextureImporter(
                TextureRoot +
                "/tdf_atlas_stonehold_slagfall_environment_metallic_smoothness_v001.png");

            Assert.That(baseColor.sRGBTexture, Is.True);
            Assert.That(normal.textureType, Is.EqualTo(TextureImporterType.NormalMap));
            Assert.That(packed.sRGBTexture, Is.False);
            Assert.That(packed.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            foreach (TextureImporter importer in new[] { baseColor, normal, packed })
            {
                Assert.That(importer.mipmapEnabled, Is.True);
                Assert.That(importer.streamingMipmaps, Is.True);
                Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.CompressedHQ));
            }
        }

        [Test]
        public void RuntimePrefabsExcludeDocumentationDependencies()
        {
            foreach (string familyId in FamilyIds)
            {
                string prefabPath = PrefabPath(familyId);
                foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
                {
                    StringAssert.DoesNotContain("/Docs/", dependency, familyId);
                    StringAssert.DoesNotContain("GenerationInputs", dependency, familyId);
                }
            }
        }

        [Test]
        public void ReviewSceneReferencesAllEightPrefabsOutsideBuild()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ReviewScenePath);
            Assert.That(sceneAsset, Is.Not.Null, ReviewScenePath);
            Assert.That(
                Array.Exists(EditorBuildSettings.scenes, scene => scene.path == ReviewScenePath),
                Is.False,
                "The profiling review scene must stay out of player builds.");

            Scene scene = EditorSceneManager.OpenScene(ReviewScenePath, OpenSceneMode.Additive);
            try
            {
                int groupCount = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    groupCount += root.GetComponentsInChildren<LODGroup>(true).Length;
                }

                Assert.That(groupCount, Is.EqualTo(FamilyIds.Length));
                foreach (string familyId in FamilyIds)
                {
                    string expectedPath = PrefabPath(familyId);
                    bool found = false;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
                        {
                            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(
                                group.gameObject);
                            if (source != null && AssetDatabase.GetAssetPath(source) == expectedPath)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                        {
                            break;
                        }
                    }

                    Assert.That(found, Is.True, expectedPath);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ImporterCleanupContractRunsAfterFailure()
        {
            Type importerType = Type.GetType(
                "AL.Editor.Terrestrials.SlagfallEnvironmentKitImport, AL.Editor");
            Assert.That(importerType, Is.Not.Null);
            MethodInfo executeWithCleanup = importerType.GetMethod(
                "ExecuteWithCleanup",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(executeWithCleanup, Is.Not.Null);

            bool cleanupRan = false;
            Action operation = () => throw new InvalidOperationException("expected failure");
            Action cleanup = () => cleanupRan = true;

            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                () => executeWithCleanup.Invoke(null, new object[] { operation, cleanup }));
            Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(cleanupRan, Is.True);
        }

        private static GameObject RequirePrefab(string familyId)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(familyId));
            Assert.That(prefab, Is.Not.Null, familyId);
            return prefab;
        }

        private static TextureImporter RequireTextureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            return importer;
        }

        private static long TriangleCount(Mesh mesh)
        {
            long indexCount = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                indexCount += (long)mesh.GetIndexCount(subMesh);
            }

            return indexCount / 3L;
        }

        private static string ModelPath(string familyId)
        {
            return ModelRoot + $"/tdf_prop_stonehold_slagfall_{familyId}_v001.fbx";
        }

        private static string PrefabPath(string familyId)
        {
            return PrefabRoot + $"/tdf_prop_stonehold_slagfall_{familyId}_v001.prefab";
        }
    }
}
