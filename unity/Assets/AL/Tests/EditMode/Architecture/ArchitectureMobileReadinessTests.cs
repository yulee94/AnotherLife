using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class ArchitectureMobileReadinessTests
    {
        private static readonly string[] PrototypePrefabPaths =
        {
            "Assets/AL/Art/Generated/Architecture/Eldergrove/" +
                "Eldergrove_Atelier_AnimationPrototype.prefab",
            "Assets/AL/Art/Generated/Architecture/Stonehold/" +
                "Stonehold_Workshop_AnimationPrototype.prefab",
            "Assets/AL/Art/Generated/Architecture/Crownlands/" +
                "Crownlands_Stormwright_AnimationPrototype.prefab",
            "Assets/AL/Art/Generated/Architecture/Umbral/" +
                "Umbral_Veilwright_AnimationPrototype.prefab",
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
                "Eldergrove_Workshop_Level01_Blockout.prefab",
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
                "Eldergrove_Workshop_Level06_Blockout.prefab",
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
                "Eldergrove_Workshop_Level10_Blockout.prefab"
        };

        private static readonly string[] PrototypeScenePaths =
        {
            "Assets/AL/Scenes/Prototypes/" +
                "EldergroveAtelierAnimationPrototype.unity",
            "Assets/AL/Scenes/Prototypes/" +
                "StoneholdWorkshopAnimationPrototype.unity",
            "Assets/AL/Scenes/Prototypes/" +
                "CrownlandsStormwrightAnimationPrototype.unity",
            "Assets/AL/Scenes/Prototypes/" +
                "UmbralVeilwrightAnimationPrototype.unity",
            "Assets/AL/Scenes/Prototypes/" +
                "EldergroveWorkshopLevelBlockout.unity",
            "Assets/AL/Scenes/Prototypes/" +
                "EldergroveWorkshopProductionModel.unity",
            "Assets/AL/Scenes/Prototypes/" +
                "StoneholdWorkshopProductionModel.unity",
            "Assets/AL/Scenes/Prototypes/" +
                "CrownlandsStormwrightProductionModel.unity"
        };

        private const string ConceptSheetFolder =
            "Assets/AL/Art/Architecture/ConceptSheets";

        private const string EldergroveWorkshopLevelProgressionPath =
            ConceptSheetFolder +
            "/architecture_eldergrove_workshop_level_progression_v001.png";

        [TestCaseSource(nameof(PrototypePrefabPaths))]
        public void PrototypeUsesBoundedPlatformNeutralRendering(
            string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            Renderer[] renderers =
                prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(
                renderers.Length,
                Is.LessThanOrEqualTo(200),
                $"{prefabPath} exceeds the approved graybox renderer ceiling.");
            Assert.That(
                prefab.GetComponentsInChildren<Animator>(true),
                Is.Empty,
                "Static modules must not receive per-object Animators.");
            Assert.That(
                prefab.GetComponentsInChildren<ParticleSystem>(true),
                Is.Empty,
                "The mobile proof uses transform and material response, not particles.");
            Assert.That(
                prefab.GetComponentsInChildren<AudioSource>(true),
                Is.Empty,
                "The isolated mobile proof must not keep audio sources alive.");
            Assert.That(
                prefab.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Graybox review geometry must not add physics cost.");

            Material[] materials = renderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            Assert.That(
                materials.Length,
                Is.LessThanOrEqualTo(10),
                "The graybox must retain a small shared material set.");
            Assert.That(
                materials.All(material => material.enableInstancing),
                Is.True,
                "Every shared graybox material must permit GPU instancing.");
            Assert.That(
                materials.SelectMany(GetAssignedTextures),
                Is.Empty,
                "Concept sheets and preview textures cannot leak into runtime prefabs.");

            foreach (Renderer targetRenderer in renderers)
            {
                Assert.That(
                    targetRenderer.motionVectorGenerationMode,
                    Is.EqualTo(MotionVectorGenerationMode.ForceNoMotion),
                    targetRenderer.name);
                Assert.That(
                    targetRenderer.lightProbeUsage,
                    Is.EqualTo(LightProbeUsage.Off),
                    targetRenderer.name);
                Assert.That(
                    targetRenderer.reflectionProbeUsage,
                    Is.EqualTo(ReflectionProbeUsage.Off),
                    targetRenderer.name);
            }

            Light[] lights = prefab.GetComponentsInChildren<Light>(true);
            Assert.That(
                lights.Length,
                Is.LessThanOrEqualTo(1),
                "Each prototype may contain at most one localized activity light.");
            Assert.That(
                lights.All(light => light.shadows == LightShadows.None),
                Is.True,
                "The optional mobile activity light must not cast dynamic shadows.");
        }

        [Test]
        public void PrototypeScenesAndConceptSheetsStayOutOfPlayerBuilds()
        {
            string[] buildScenePaths =
                EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();
            Assert.That(
                buildScenePaths,
                Has.None.Matches<string>(
                    path => PrototypeScenePaths.Contains(path)));

            foreach (string prefabPath in PrototypePrefabPaths)
            {
                string[] dependencies =
                    AssetDatabase.GetDependencies(prefabPath, true);
                Assert.That(
                    dependencies,
                    Has.None.StartsWith(ConceptSheetFolder),
                    $"{prefabPath} depends on source-only concept art.");
            }
        }

        [Test]
        public void ConceptSheetsRemainNonReadableSourceReferences()
        {
            string[] conceptSheetGuids =
                AssetDatabase.FindAssets(
                    "t:Texture2D",
                    new[] { ConceptSheetFolder });
            Assert.That(
                conceptSheetGuids,
                Has.Length.GreaterThanOrEqualTo(13));

            foreach (string guid in conceptSheetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var importer =
                    AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, assetPath);
                Assert.That(importer.isReadable, Is.False, assetPath);
                Assert.That(importer.mipmapEnabled, Is.False, assetPath);
                Assert.That(
                    importer.npotScale,
                    Is.EqualTo(TextureImporterNPOTScale.None),
                    assetPath);
            }
        }

        [Test]
        public void EldergroveWorkshopLevelProgressionRemainsAValidatedSourceOnlyAsset()
        {
            Texture2D sheet =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    EldergroveWorkshopLevelProgressionPath);
            Assert.That(sheet, Is.Not.Null, EldergroveWorkshopLevelProgressionPath);
            Assert.That(sheet.width, Is.EqualTo(1773));
            Assert.That(sheet.height, Is.EqualTo(887));

            var importer =
                AssetImporter.GetAtPath(EldergroveWorkshopLevelProgressionPath)
                    as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(
                importer.npotScale,
                Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.assetBundleName, Is.Null.Or.Empty);

            string[] playerDependencies = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .SelectMany(
                    scene => AssetDatabase.GetDependencies(scene.path, true))
                .Concat(
                    PrototypePrefabPaths.SelectMany(
                        prefabPath =>
                            AssetDatabase.GetDependencies(prefabPath, true)))
                .Distinct()
                .ToArray();
            Assert.That(
                playerDependencies,
                Has.None.EqualTo(EldergroveWorkshopLevelProgressionPath),
                "The review sheet must not become a runtime texture dependency.");
        }

        private static IEnumerable<Texture> GetAssignedTextures(
            Material material)
        {
            foreach (string propertyName in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    yield return texture;
                }
            }
        }
    }
}
