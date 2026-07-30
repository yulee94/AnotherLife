using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AL.Terrestrials.Slagfall;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.Terrestrials
{
    public sealed class SlagfallRepresentativeSliceAssetTests
    {
        private const string ProfilePath =
            "Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/" +
            "Environment/Prefabs/Slagfall_RepresentativeSlice_Profile.asset";
        private const string SlicePrefabPath =
            "Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/" +
            "Environment/Prefabs/Slagfall_RepresentativeSlice.prefab";
        private const string ScenePath =
            "Assets/AL/Scenes/Prototype/Terrestrials/" +
            "SlagfallQuarryRepresentativeSlice.unity";

        [Test]
        public void ProfilePreservesApprovedSourceAndProductionBudgets()
        {
            SlagfallRepresentativeSliceProfile profile = LoadProfile();

            Assert.IsTrue(
                profile.Validate(out string diagnostic),
                diagnostic);
            Assert.AreEqual(
                SlagfallSourceAuthority.SourceVersion,
                profile.SourceVersion);
            Assert.AreEqual(
                SlagfallSourceAuthority.HabitatSourceId,
                profile.HabitatSourceId);
            Assert.AreEqual(
                SlagfallSourceAuthority.HabitatSourceSha256,
                profile.HabitatSourceSha256);
            Assert.AreEqual(
                SlagfallSourceAuthority.SlagwhistleIdentitySha256,
                profile.SlagwhistleIdentitySha256);
            Assert.AreEqual(
                SlagfallSourceAuthority.SlagwhistleMotionSha256,
                profile.SlagwhistleMotionSha256);
            Assert.AreEqual(new Vector2(128f, 128f), profile.CellSizeMeters);
            Assert.AreEqual(8, profile.HabitatFamilies.Count);
            Assert.AreEqual(6, profile.SlagwhistleClips.Count);
            Assert.AreEqual(3, profile.HabitatTextureSet.Count);
            Assert.AreEqual(3, profile.SlagwhistleTextureSet.Count);
            Assert.That(profile.SlagwhistleLod0Triangles, Is.InRange(8000, 10000));
            Assert.That(
                profile.SlagwhistleLod1Triangles /
                (float)profile.SlagwhistleLod0Triangles,
                Is.InRange(0.55f, 0.60f));
            Assert.That(
                profile.SlagwhistleLod2Triangles /
                (float)profile.SlagwhistleLod0Triangles,
                Is.InRange(0.20f, 0.25f));
            Assert.That(
                profile.SlagwhistleImpostorTriangles /
                (float)profile.SlagwhistleLod0Triangles,
                Is.InRange(0.06f, 0.08f));
            Assert.That(profile.SlagwhistleBoneCount, Is.InRange(34, 42));
            Assert.That(profile.SlagwhistleMaterialSlots, Is.InRange(1, 2));
            Assert.LessOrEqual(
                profile.HabitatSourceBytes,
                12L * 1024L * 1024L);
            Assert.LessOrEqual(
                profile.SlagwhistleSourceBytes,
                7L * 1024L * 1024L);
        }

        [Test]
        public void ApprovedSourceFilesStillMatchPinnedHashes()
        {
            const string sourceFolder =
                "Docs/Terrestrials/Ecosystems/SlagfallQuarryV002/" +
                "ConceptSheets";

            AssertFileSha256(
                $"{sourceFolder}/" +
                "tdf_habitat_stonehold_slagfall_quarry_master_v002.png",
                SlagfallSourceAuthority.HabitatSourceSha256);
            AssertFileSha256(
                $"{sourceFolder}/" +
                "tdf_fauna_stonehold_slagwhistle_burrower_identity_v002.png",
                SlagfallSourceAuthority.SlagwhistleIdentitySha256);
            AssertFileSha256(
                $"{sourceFolder}/" +
                "tdf_fauna_stonehold_slagwhistle_burrower_motion_contact_v002.png",
                SlagfallSourceAuthority.SlagwhistleMotionSha256);
        }

        [Test]
        public void HabitatKitContainsEveryRequiredFamilyAndLod()
        {
            SlagfallRepresentativeSliceProfile profile = LoadProfile();
            string[] familyIds = profile.HabitatFamilies
                .Select(family => family.FamilyId)
                .ToArray();

            CollectionAssert.AreEquivalent(
                SlagfallSourceAuthority.HabitatFamilyIds,
                familyIds);
            Assert.AreEqual(familyIds.Length, familyIds.Distinct().Count());

            for (int familyIndex = 0;
                familyIndex < profile.HabitatFamilies.Count;
                familyIndex++)
            {
                SlagfallHabitatFamilyEntry family =
                    profile.HabitatFamilies[familyIndex];
                int expectedVariants = familyIndex < 3 ? 3 : 1;
                Assert.AreEqual(
                    expectedVariants,
                    family.Variants.Count,
                    family.FamilyId);

                for (int variantIndex = 0;
                    variantIndex < family.Variants.Count;
                    variantIndex++)
                {
                    GameObject variant = family.Variants[variantIndex];
                    SlagfallHabitatAsset habitat =
                        variant.GetComponent<SlagfallHabitatAsset>();
                    Assert.NotNull(habitat, variant.name);
                    Assert.AreEqual(family.FamilyId, habitat.FamilyId);
                    Assert.AreEqual(variantIndex, habitat.VariantIndex);
                    Assert.NotNull(habitat.LodGroup);
                    Assert.AreEqual(
                        4,
                        habitat.LodGroup.GetLODs().Length,
                        variant.name);
                }
            }
        }

        [Test]
        public void ProductionAssetsAreSelfContainedAndReviewSceneIsExcluded()
        {
            SlagfallRepresentativeSliceProfile profile = LoadProfile();

            foreach (Texture2D texture in profile.HabitatTextureSet)
            {
                Assert.AreEqual(2048, texture.width, texture.name);
                Assert.AreEqual(2048, texture.height, texture.name);
                AssertTextureImporter(texture);
            }

            foreach (Texture2D texture in profile.SlagwhistleTextureSet)
            {
                Assert.AreEqual(1024, texture.width, texture.name);
                Assert.AreEqual(1024, texture.height, texture.name);
                AssertTextureImporter(texture);
            }

            string[] dependencies =
                AssetDatabase.GetDependencies(SlicePrefabPath, true);
            Assert.IsFalse(
                dependencies.Any(
                    path =>
                        path.IndexOf(
                            "/Docs/",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf(
                            "concept",
                            StringComparison.OrdinalIgnoreCase) >= 0),
                "Production prefabs must not reference concept sheets or documentation assets.");

            GameObject slice =
                AssetDatabase.LoadAssetAtPath<GameObject>(SlicePrefabPath);
            Assert.NotNull(slice);
            Assert.AreEqual(
                0,
                slice.GetComponentsInChildren<ParticleSystem>(true).Length,
                "The review slice must remain valid with effects fully off.");
            Assert.AreEqual(
                0,
                slice.GetComponentsInChildren<Light>(true).Length,
                "Production content must not depend on dynamic lights.");

            EditorBuildSettingsScene reviewScene =
                EditorBuildSettings.scenes.SingleOrDefault(
                    scene => scene.path == ScenePath);
            Assert.IsNull(
                reviewScene,
                "The profiling scene must remain absent from Player build settings.");
        }

        [Test]
        public void SlagwhistlePrefabPreservesEveryRequiredRepresentationAndIdentityMarker()
        {
            SlagfallRepresentativeSliceProfile profile = LoadProfile();
            GameObject prefab = profile.SlagwhistlePrefab;
            Assert.NotNull(prefab);

            SlagwhistlePresentation presentation =
                prefab.GetComponent<SlagwhistlePresentation>();
            Assert.NotNull(presentation);
            Assert.IsTrue(
                presentation.ValidateRequiredRepresentations(
                    out string diagnostic),
                diagnostic);

            Transform identityRoot =
                prefab.transform.Find("ProtectedIdentityMarkers");
            Assert.NotNull(identityRoot);
            foreach (string feature in
                SlagfallSourceAuthority.ProtectedSlagwhistleFeatures)
            {
                Assert.NotNull(
                    identityRoot.Find(feature),
                    $"Missing protected Slagwhistle feature marker: {feature}");
            }
        }

        [Test]
        public void SlagwhistlePresentationRejectsMissingCheapTier()
        {
            var root = new GameObject("SlagwhistleValidationRoot");
            try
            {
                SlagwhistlePresentation presentation =
                    root.AddComponent<SlagwhistlePresentation>();
                var full = new GameObject("Full");
                var medium = new GameObject("Medium");
                var impostor = new GameObject("Impostor");
                full.transform.SetParent(root.transform);
                medium.transform.SetParent(root.transform);
                impostor.transform.SetParent(root.transform);

                presentation.Configure(
                    full,
                    medium,
                    null,
                    impostor,
                    Array.Empty<Animator>(),
                    Array.Empty<Transform>());

                Assert.IsFalse(
                    presentation.ValidateRequiredRepresentations(
                        out string diagnostic));
                Assert.AreEqual(
                    "missing_slagwhistle_low_detail",
                    diagnostic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static SlagfallRepresentativeSliceProfile LoadProfile()
        {
            SlagfallRepresentativeSliceProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    SlagfallRepresentativeSliceProfile>(ProfilePath);
            Assert.NotNull(profile, ProfilePath);
            return profile;
        }

        private static void AssertTextureImporter(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.NotNull(importer, path);
            Assert.IsTrue(importer.mipmapEnabled, path);
            Assert.IsFalse(importer.isReadable, path);
            Assert.AreEqual(
                TextureImporterCompression.CompressedHQ,
                importer.textureCompression,
                path);
        }

        private static void AssertFileSha256(
            string projectRelativePath,
            string expectedSha256)
        {
            string unityRoot =
                Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsNotNull(unityRoot);
            string path = Path.Combine(unityRoot, projectRelativePath);
            Assert.IsTrue(File.Exists(path), path);

            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            string actual = BitConverter
                .ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            Assert.AreEqual(expectedSha256, actual, path);
        }
    }
}
