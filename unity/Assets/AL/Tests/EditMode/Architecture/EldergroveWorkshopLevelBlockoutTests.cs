using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class EldergroveWorkshopLevelBlockoutTests
    {
        private const string Level01PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "Eldergrove_Workshop_Level01_Blockout.prefab";
        private const string Level06PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "Eldergrove_Workshop_Level06_Blockout.prefab";
        private const string Level10PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "Eldergrove_Workshop_Level10_Blockout.prefab";
        private const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "EldergroveWorkshopLevelBlockout.unity";
        private const string ConceptSheetFolder =
            "Assets/AL/Art/Architecture/ConceptSheets";

        private static readonly string[] OrderedLevelGroups =
        {
            "L01_Foundational",
            "L02_Reinforced",
            "L03_Expanded",
            "L04_Established",
            "L05_DistrictAnchor",
            "L06_Advanced",
            "L07_Signature",
            "L08_Masterwork",
            "L09_Prestige",
            "L10_Landmark"
        };

        [TestCase(Level01PrefabPath, 1)]
        [TestCase(Level06PrefabPath, 6)]
        [TestCase(Level10PrefabPath, 10)]
        public void ReviewAnchorContainsOnlyItsCumulativeLevelGroups(
            string prefabPath,
            int confirmedLevel)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(prefab.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));

            for (int index = 0; index < OrderedLevelGroups.Length; index++)
            {
                Transform group =
                    prefab.transform.Find(OrderedLevelGroups[index]);
                if (index < confirmedLevel)
                {
                    Assert.That(
                        group,
                        Is.Not.Null,
                        $"{prefabPath} is missing {OrderedLevelGroups[index]}.");
                    Assert.That(group.gameObject.activeSelf, Is.True);
                }
                else
                {
                    Assert.That(
                        group,
                        Is.Null,
                        $"{prefabPath} includes future group " +
                        $"{OrderedLevelGroups[index]}.");
                }
            }
        }

        [Test]
        public void ReviewAnchorsGrowMonotonicallyWithinMobileCeiling()
        {
            int[] rendererCounts =
            {
                RendererCount(Level01PrefabPath),
                RendererCount(Level06PrefabPath),
                RendererCount(Level10PrefabPath)
            };

            Assert.That(rendererCounts[1], Is.GreaterThan(rendererCounts[0]));
            Assert.That(rendererCounts[2], Is.GreaterThan(rendererCounts[1]));
            Assert.That(
                rendererCounts,
                Has.All.LessThanOrEqualTo(120),
                "Eldergrove common-building anchors exceed the review ceiling.");
        }

        [TestCase(Level01PrefabPath)]
        [TestCase(Level06PrefabPath)]
        [TestCase(Level10PrefabPath)]
        public void ReviewAnchorIsStaticAndRuntimeAgnostic(string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(
                prefab.GetComponentsInChildren<MonoBehaviour>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<Animator>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<ParticleSystem>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<AudioSource>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<Light>(true),
                Is.Empty);

            Renderer[] renderers =
                prefab.GetComponentsInChildren<Renderer>(true);
            Material[] materials = renderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            Assert.That(materials, Has.Length.LessThanOrEqualTo(8));
            Assert.That(
                materials.All(material => material.enableInstancing),
                Is.True);

            foreach (Renderer targetRenderer in renderers)
            {
                Assert.That(
                    targetRenderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off),
                    targetRenderer.name);
                Assert.That(
                    targetRenderer.receiveShadows,
                    Is.False,
                    targetRenderer.name);
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
        }

        [Test]
        public void LevelOneRoofStillRisesFromEavesToRidge()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(Level01PrefabPath);
            Transform west = prefab.transform.Find(
                "L01_Foundational/RoofAndLanternSet/" +
                "RoofWestOcclusion/RoofWest");
            Transform east = prefab.transform.Find(
                "L01_Foundational/RoofAndLanternSet/" +
                "RoofEastOcclusion/RoofEast");
            Assert.That(west, Is.Not.Null);
            Assert.That(east, Is.Not.Null);

            float westOuterHeight =
                west.TransformPoint(new Vector3(-0.5f, 0f, 0f)).y;
            float westRidgeHeight =
                west.TransformPoint(new Vector3(0.5f, 0f, 0f)).y;
            float eastRidgeHeight =
                east.TransformPoint(new Vector3(-0.5f, 0f, 0f)).y;
            float eastOuterHeight =
                east.TransformPoint(new Vector3(0.5f, 0f, 0f)).y;

            Assert.That(westRidgeHeight, Is.GreaterThan(westOuterHeight));
            Assert.That(eastRidgeHeight, Is.GreaterThan(eastOuterHeight));
        }

        [Test]
        public void ReviewAssetsStayOutsideRuntimeAndSourceDependencies()
        {
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Not.Contain(ScenePath));

            foreach (string prefabPath in new[]
                     {
                         Level01PrefabPath,
                         Level06PrefabPath,
                         Level10PrefabPath
                     })
            {
                string[] dependencies =
                    AssetDatabase.GetDependencies(prefabPath, true);
                Assert.That(
                    dependencies,
                    Has.None.StartsWith(ConceptSheetFolder),
                    $"{prefabPath} depends on source-only concept art.");
            }
        }

        private static int RendererCount(string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            return prefab.GetComponentsInChildren<Renderer>(true).Length;
        }
    }
}
