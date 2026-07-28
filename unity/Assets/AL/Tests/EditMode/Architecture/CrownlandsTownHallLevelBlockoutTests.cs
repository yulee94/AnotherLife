using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class CrownlandsTownHallLevelBlockoutTests
    {
        private const string Level01PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Production/" +
            "TownHall/Crownlands_TownHall_Level01_Blockout.prefab";
        private const string Level06PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Production/" +
            "TownHall/Crownlands_TownHall_Level06_Blockout.prefab";
        private const string Level10PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Production/" +
            "TownHall/Crownlands_TownHall_Level10_Blockout.prefab";
        private const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "CrownlandsTownHallLevelBlockout.unity";
        private const string ConceptSheetFolder =
            "Assets/AL/Art/Architecture/ConceptSheets";

        private static readonly string[] OrderedLevelGroups =
        {
            "L01_OperationalHall",
            "L02_Grounded",
            "L03_WorkingWing",
            "L04_PublicThreshold",
            "L05_RealmStructure",
            "L06_DistrictCapacity",
            "L07_UpperAuthority",
            "L08_ServiceIntegration",
            "L09_CivicIntegration",
            "L10_ConcordMeridian"
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

        [TestCase(Level01PrefabPath)]
        [TestCase(Level06PrefabPath)]
        [TestCase(Level10PrefabPath)]
        public void StableSpatialIdentityDoesNotMoveWithLevel(string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(
                prefab.transform.Find("Entrance").localPosition,
                Is.EqualTo(new Vector3(0f, 0f, -4.65f)));
            Assert.That(
                prefab.transform.Find("CameraFocus").localPosition,
                Is.EqualTo(new Vector3(0f, 3.55f, 0f)));
            Assert.That(prefab.transform.Find("Activity_00"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Output_00"), Is.Not.Null);
        }

        [Test]
        public void ReviewAnchorsGrowMonotonicallyWithinGrayboxCeiling()
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
                "Crownlands Town Hall anchors exceed the static review ceiling.");
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
            Assert.That(materials, Has.Length.LessThanOrEqualTo(6));
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
        public void ReviewAnchorsStayInsideApprovedSpatialEnvelopes()
        {
            AssertEnvelope(Level01PrefabPath, 9.8f, 9.2f, 7f);
            AssertEnvelope(Level06PrefabPath, 13f, 11.5f, 9.4f);
            AssertEnvelope(Level10PrefabPath, 15.2f, 14.2f, 12.6f);
        }

        [Test]
        public void LevelOneRoofRisesFromEavesToRidge()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(Level01PrefabPath);
            Transform west = prefab.transform.Find(
                "L01_OperationalHall/CivicRoof_Occlusion/" +
                "MainCivicRoofWestGroup/MainCivicRoofWest");
            Transform east = prefab.transform.Find(
                "L01_OperationalHall/CivicRoof_Occlusion/" +
                "MainCivicRoofEastGroup/MainCivicRoofEast");
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

        [TestCase(Level06PrefabPath)]
        [TestCase(Level10PrefabPath)]
        public void PairedGroundedCivicTowersRemainCountable(string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Transform towers = prefab.transform.Find(
                "L06_DistrictCapacity/PairedGroundedCivicTowers");
            Assert.That(towers, Is.Not.Null);
            Assert.That(towers.childCount, Is.EqualTo(2));
            Assert.That(
                towers.Cast<Transform>()
                    .Select(child => child.name)
                    .OrderBy(name => name),
                Is.EqualTo(new[]
                {
                    "EastCivicTower",
                    "WestCivicTower"
                }));
        }

        [Test]
        public void ConcordMeridianIsSupportedStaticAndUnmarked()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(Level10PrefabPath);
            Transform supports = prefab.transform.Find(
                "L10_ConcordMeridian/MeridianSupports");
            Transform meridian = prefab.transform.Find(
                "L10_ConcordMeridian/ConcordMeridian_Occlusion");
            Assert.That(supports, Is.Not.Null);
            Assert.That(meridian, Is.Not.Null);
            Assert.That(supports.childCount, Is.EqualTo(2));
            Assert.That(
                meridian.Cast<Transform>()
                    .Count(child =>
                        child.name.StartsWith(
                            "SilverMeridianSegment_",
                            StringComparison.Ordinal)),
                Is.EqualTo(10));
            Assert.That(
                meridian.Find("SolidUnmarkedApexDial"),
                Is.Not.Null);

            string[] forbiddenNames =
            {
                "Clock",
                "Orb",
                "Energy",
                "Lightning",
                "Throne",
                "Portal"
            };
            string[] transformNames = prefab
                .GetComponentsInChildren<Transform>(true)
                .Select(child => child.name)
                .ToArray();
            foreach (string forbiddenName in forbiddenNames)
            {
                Assert.That(
                    transformNames,
                    Has.None.Contains(forbiddenName),
                    $"The Town Hall reintroduced {forbiddenName}.");
            }
        }

        [Test]
        public void ColliderReviewVolumesCoverTheFinalMassWithoutRuntimeColliders()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(Level10PrefabPath);
            Bounds visualBounds = RendererBounds(prefab);
            Transform preview =
                prefab.transform.Find("SelectionColliderPreview");
            Assert.That(preview, Is.Not.Null);

            var previewBounds = new Bounds(
                preview.position,
                preview.lossyScale);
            Assert.That(previewBounds.Contains(visualBounds.min), Is.True);
            Assert.That(previewBounds.Contains(visualBounds.max), Is.True);
            Assert.That(
                prefab.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Review volumes must not become live collider authority.");
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

        private static void AssertEnvelope(
            string prefabPath,
            float maximumWidth,
            float maximumDepth,
            float maximumHeight)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Bounds bounds = RendererBounds(prefab);
            Assert.That(
                bounds.size.x,
                Is.LessThanOrEqualTo(maximumWidth),
                $"{prefabPath} exceeds its width envelope.");
            Assert.That(
                bounds.size.z,
                Is.LessThanOrEqualTo(maximumDepth),
                $"{prefabPath} exceeds its depth envelope.");
            Assert.That(
                bounds.size.y,
                Is.LessThanOrEqualTo(maximumHeight),
                $"{prefabPath} exceeds its height envelope.");
        }

        private static Bounds RendererBounds(GameObject prefab)
        {
            Renderer[] renderers =
                prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
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
