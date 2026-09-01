using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AL.ChampionMode.Presentation;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.World
{
    public sealed class ChampionCustomizationBaseAssetTests
    {
        private const string Root =
            "Assets/AL/Art/Production/FirstUserOnboarding/Characters/";

        [Test]
        public void MaleAndFemaleBasesImportAsDistinctCustomizableSkinnedAssets()
        {
            GameObject male = LoadModel(
                Root + "Crownlands_Champion_Male_Base_Meshy6_Rigged_v001.fbx");
            GameObject female = LoadModel(
                Root + "Crownlands_Champion_Female_Base_Meshy6_Rigged_v001.fbx");

            Assert.That(male, Is.Not.SameAs(female));
            AssertCustomizable(male, "male");
            AssertCustomizable(female, "female");
        }

        [Test]
        public void WalkClipsBindToMatchingChampionSkeletonHierarchy()
        {
            const string malePath =
                Root + "Crownlands_Champion_Male_Base_Meshy6_Rigged_v001.fbx";
            const string femalePath =
                Root + "Crownlands_Champion_Female_Base_Meshy6_Rigged_v001.fbx";
            AssertClipHierarchy(
                LoadModel(malePath),
                LoadClip(malePath),
                "male");
            AssertClipHierarchy(
                LoadModel(femalePath),
                LoadClip(femalePath),
                "female");
        }

        [Test]
        public void ChampionPbrMapsUseDataCorrectImportSettings()
        {
            foreach (string bodyBase in new[] { "Male", "Female" })
            {
                string textureRoot = Root + "Crownlands_Champion_" + bodyBase +
                                     "_Base_Meshy6_v001_textures/";
                AssertTexture(textureRoot + "base_color.png", TextureImporterType.Default, true);
                AssertTexture(textureRoot + "emission.png", TextureImporterType.Default, true);
                AssertTexture(textureRoot + "normal.png", TextureImporterType.NormalMap, false);
                AssertTexture(textureRoot + "metallic.png", TextureImporterType.Default, false);
                AssertTexture(textureRoot + "roughness.png", TextureImporterType.Default, false);
            }
        }

        [Test]
        public void FirstSessionPbrPacketsUsePackedSmoothnessAndLinearDataImports()
        {
            string packetRoot =
                "Assets/AL/Art/Production/FirstUserOnboarding/";
            string[] textureRoots =
            {
                packetRoot + "Characters/Crownlands_Champion_Male_Base_Meshy6_v001_textures/",
                packetRoot + "Characters/Crownlands_Champion_Female_Base_Meshy6_v001_textures/",
                packetRoot + "Enemies/Covenant_Sentinel_Meshy6_v001_textures/",
                packetRoot + "Environment/Stonehold_CapitalHall_Meshy6_v001_textures/",
                packetRoot + "Environment/Eldergrove_CapitalHall_Meshy6_v001_textures/",
                packetRoot + "Environment/Crownlands_CapitalHall_Meshy6_v001_textures/",
                packetRoot + "Environment/Umbral_CapitalHall_Meshy6_v001_textures/"
            };

            foreach (string textureRoot in textureRoots)
            {
                AssertTexture(textureRoot + "base_color.png", TextureImporterType.Default, true);
                AssertTexture(textureRoot + "emission.png", TextureImporterType.Default, true);
                AssertTexture(textureRoot + "normal.png", TextureImporterType.NormalMap, false);
                AssertTexture(textureRoot + "roughness.png", TextureImporterType.Default, false);
                AssertPackedMetallicSmoothness(textureRoot + "metallic_smoothness.png");
            }

            string floorRoot = packetRoot + "Environment/Neutral_Covenant_Flagstone_";
            AssertTexture(
                floorRoot + "Normal_Derived_v001.png",
                TextureImporterType.NormalMap,
                false);
            AssertTexture(
                floorRoot + "Roughness_Derived_v001.png",
                TextureImporterType.Default,
                false);
            AssertPackedMetallicSmoothness(
                floorRoot + "MetallicSmoothness_Derived_v001.png");
        }

        [Test]
        public void RealmPanoramicSkyboxesImportAsRepeatableTwoToOneTextures()
        {
            foreach (string realm in new[] { "Stonehold", "Eldergrove", "Crownlands", "Umbral" })
            {
                string path =
                    "Assets/AL/Art/Production/FirstUserOnboarding/Environment/" +
                    realm + "_PanoramicSky_Meshy_v001.png";
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, realm);
                Assert.That(texture.width, Is.EqualTo(texture.height * 2), realm);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, realm);
                Assert.That(importer.sRGBTexture, Is.True, realm);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), realm);
                Assert.That(importer.mipmapEnabled, Is.True, realm);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(2048), realm);
            }
        }

        [Test]
        public void RuntimeCatalogResolvesDistinctMaleAndFemaleBases()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryResolveChampionBase("male", out GameObject male, out AnimationClip maleWalk),
                Is.True);
            Assert.That(
                catalog.TryResolveChampionBase("female", out GameObject female, out AnimationClip femaleWalk),
                Is.True);
            Assert.That(male, Is.Not.SameAs(female));
            Assert.That(maleWalk, Is.Not.SameAs(femaleWalk));
            Assert.That(male.name, Does.Contain("Male"));
            Assert.That(female.name, Does.Contain("Female"));
            Assert.That(
                catalog.TryResolveChampionBase(null, out GameObject legacyNull, out _),
                Is.True);
            Assert.That(legacyNull, Is.SameAs(male));
            Assert.That(
                catalog.TryResolveChampionBase(string.Empty, out GameObject legacyEmpty, out _),
                Is.True);
            Assert.That(legacyEmpty, Is.SameAs(male));
            Assert.That(
                catalog.TryResolveChampionBase("unknown", out _, out _),
                Is.False);
        }

        [Test]
        public void AuthoredBinderAppliesSavedFemaleBaseMaterialsAndSlimBlendshape()
        {
            var player = new GameObject("FemaleBinderTestPlayer");
            try
            {
                var appearance = new ChampionCustomizationState
                {
                    BodyBaseId = "female",
                    BodyPresetId = "slim",
                    PrimaryR = 0.24f,
                    PrimaryG = 0.32f,
                    PrimaryB = 0.58f,
                    HairR = 0.12f,
                    HairG = 0.08f,
                    HairB = 0.06f,
                    SkinR = 0.68f,
                    SkinG = 0.50f,
                    SkinB = 0.44f,
                    AccentR = 0.72f,
                    AccentG = 0.52f,
                    AccentB = 0.20f
                };

                Assert.That(
                    FirstSessionAuthoredVisualBinder.TryBindChampion(
                        player,
                        RealmId.Crownlands,
                        appearance,
                        out string diagnostic),
                    Is.True,
                    diagnostic);

                Transform authored = player.transform.Find(
                    FirstSessionAuthoredVisualBinder.ChampionVisualName);
                Assert.That(authored, Is.Not.Null);
                Assert.That(authored.GetComponentsInChildren<Transform>(true)
                        .Any(transform => transform.name == "ImportedAuthoredChampion_female"),
                    Is.True);

                SkinnedMeshRenderer renderer =
                    authored.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(4));
                Assert.That(renderer.sharedMaterials.Select(material => material.name),
                    Is.EquivalentTo(new[]
                    {
                        "ChampionRuntime_female_Cloth",
                        "ChampionRuntime_female_Hair",
                        "ChampionRuntime_female_Metal",
                        "ChampionRuntime_female_Skin"
                    }));

                int slim = renderer.sharedMesh.GetBlendShapeIndex("Body_Slim");
                Assert.That(slim, Is.GreaterThanOrEqualTo(0));
                Assert.That(renderer.GetBlendShapeWeight(slim), Is.EqualTo(100f));
            }
            finally
            {
                FirstSessionAuthoredVisualBinder.ReleaseMotionGraphs(player);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void AuthoredBinderReplacesMaleWithFemaleWithoutDuplicateVisualRoots()
        {
            var player = new GameObject("BodyBaseRebindTestPlayer");
            try
            {
                var male = new ChampionCustomizationState
                {
                    BodyBaseId = "male",
                    BodyPresetId = "broad"
                };
                var female = new ChampionCustomizationState
                {
                    BodyBaseId = "female",
                    BodyPresetId = "tall"
                };

                Assert.That(
                    FirstSessionAuthoredVisualBinder.TryBindChampion(
                        player,
                        RealmId.Crownlands,
                        male,
                        out string maleDiagnostic),
                    Is.True,
                    maleDiagnostic);
                Assert.That(
                    FirstSessionAuthoredVisualBinder.TryBindChampion(
                        player,
                        RealmId.Crownlands,
                        female,
                        out string femaleDiagnostic),
                    Is.True,
                    femaleDiagnostic);

                Transform[] authoredRoots = player.GetComponentsInChildren<Transform>(true)
                    .Where(transform =>
                        transform.parent == player.transform &&
                        transform.name == FirstSessionAuthoredVisualBinder.ChampionVisualName)
                    .ToArray();
                Assert.That(authoredRoots.Length, Is.EqualTo(1));
                Assert.That(authoredRoots[0].GetComponentsInChildren<Transform>(true)
                        .Any(transform => transform.name == "ImportedAuthoredChampion_female"),
                    Is.True);
            }
            finally
            {
                FirstSessionAuthoredVisualBinder.ReleaseMotionGraphs(player);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void AuthoredBinderRejectsUnknownNonEmptyBodyBaseId()
        {
            var player = new GameObject("UnknownBodyBaseBinderTestPlayer");
            try
            {
                Assert.That(
                    FirstSessionAuthoredVisualBinder.TryBindChampion(
                        player,
                        RealmId.Crownlands,
                        new ChampionCustomizationState { BodyBaseId = "unknown" },
                        out string diagnostic),
                    Is.False);
                Assert.That(diagnostic, Is.EqualTo("authored_champion_base_missing:unknown"));
                Assert.That(
                    player.transform.Find(FirstSessionAuthoredVisualBinder.ChampionVisualName),
                    Is.Null);
            }
            finally
            {
                FirstSessionAuthoredVisualBinder.ReleaseMotionGraphs(player);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ArenaCompatibleBinderReadsCommittedFemaleAppearance()
        {
            IDictionary services = (IDictionary)typeof(ServiceLocator)
                .GetField("Services", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null);
            Assert.That(services, Is.Not.Null);
            Type serviceType = typeof(ISaveGameService);
            bool hadPrevious = services.Contains(serviceType);
            object previous = hadPrevious ? services[serviceType] : null;
            var player = new GameObject("CommittedFemaleBinderTestPlayer");
            try
            {
                ServiceLocator.Register<ISaveGameService>(
                    new FakeSaveService(new SaveGameData
                    {
                        SelectedRealm = RealmId.Crownlands,
                        ChampionCustomization = new ChampionCustomizationState
                        {
                            BodyBaseId = "female",
                            BodyPresetId = "tall"
                        }
                    }));

                Assert.That(
                    FirstSessionAuthoredVisualBinder.TryBindChampion(
                        player,
                        RealmId.Crownlands,
                        out string diagnostic),
                    Is.True,
                    diagnostic);
                Assert.That(player.GetComponentsInChildren<Transform>(true)
                        .Any(transform => transform.name == "ImportedAuthoredChampion_female"),
                    Is.True);
            }
            finally
            {
                if (hadPrevious)
                {
                    services[serviceType] = previous;
                }
                else
                {
                    services.Remove(serviceType);
                }

                FirstSessionAuthoredVisualBinder.ReleaseMotionGraphs(player);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void AuthoredMotionCanReleasePlayableGraphBeforeStaticCaptureCleanup()
        {
            var player = new GameObject("MotionReleaseTestPlayer");
            try
            {
                Assert.That(
                    FirstSessionAuthoredVisualBinder.TryBindChampion(
                        player,
                        RealmId.Crownlands,
                        new ChampionCustomizationState { BodyBaseId = "male" },
                        out string diagnostic),
                    Is.True,
                    diagnostic);
                AuthoredGuardianMotion motion =
                    player.GetComponentInChildren<AuthoredGuardianMotion>(true);
                Assert.That(motion, Is.Not.Null);
                Assert.That(motion.IsPlaying, Is.True);

                FirstSessionAuthoredVisualBinder.ReleaseMotionGraphs(player);

                Assert.That(motion.IsPlaying, Is.False);
            }
            finally
            {
                FirstSessionAuthoredVisualBinder.ReleaseMotionGraphs(player);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void AssertCustomizable(GameObject model, string label)
        {
            SkinnedMeshRenderer renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null, label);
            Assert.That(renderer.sharedMaterials.Select(material => material.name),
                Is.EquivalentTo(new[]
                {
                    "M_Champion_Cloth",
                    "M_Champion_Hair",
                    "M_Champion_Metal",
                    "M_Champion_Skin"
                }),
                label);

            foreach (string shape in new[]
                     {
                         "Body_Slim",
                         "Body_Broad",
                         "Body_Tall",
                         "Body_Stout"
                     })
            {
                Assert.That(renderer.sharedMesh.GetBlendShapeIndex(shape),
                    Is.GreaterThanOrEqualTo(0),
                    label + ":" + shape);
            }

            foreach (string socket in new[]
                     {
                         "Socket_WeaponMain",
                         "Socket_Offhand",
                         "Socket_Head",
                         "Socket_Back"
                     })
            {
                Assert.That(model.GetComponentsInChildren<Transform>(true)
                        .Any(transform => string.Equals(
                            transform.name,
                            socket,
                            StringComparison.Ordinal)),
                    Is.True,
                    label + ":" + socket);
            }
        }

        private static GameObject LoadModel(string path)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, path);
            return model;
        }

        private static AnimationClip LoadClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__preview__", StringComparison.Ordinal));
            Assert.That(clip, Is.Not.Null, path);
            return clip;
        }

        private static void AssertClipHierarchy(
            GameObject model,
            AnimationClip clip,
            string label)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Length, Is.GreaterThan(0), label);
            string[] missing = bindings
                .Select(binding => binding.path)
                .Where(path => !string.IsNullOrEmpty(path) && model.transform.Find(path) == null)
                .Distinct()
                .ToArray();
            Assert.That(missing, Is.Empty, label + ": " + string.Join(", ", missing));
        }

        private static void AssertTexture(
            string path,
            TextureImporterType textureType,
            bool sRgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(textureType), path);
            Assert.That(importer.sRGBTexture, Is.EqualTo(sRgb), path);
            Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(2048), path);
        }

        private static void AssertPackedMetallicSmoothness(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default), path);
            Assert.That(importer.sRGBTexture, Is.False, path);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), path);
            Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True, path);
            Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(2048), path);
        }

        private sealed class FakeSaveService : ISaveGameService
        {
            public FakeSaveService(SaveGameData currentSave)
            {
                CurrentSave = currentSave;
            }

            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.SavedPrimary;
            public string LastSaveMessage => string.Empty;
            public void Save() { }
            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }
            public void DeleteSave()
            {
                CurrentSave = null;
            }
        }
    }
}
