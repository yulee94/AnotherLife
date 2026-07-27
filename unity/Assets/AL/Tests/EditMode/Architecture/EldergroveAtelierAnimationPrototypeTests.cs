using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class EldergroveAtelierAnimationPrototypeTests
    {
        private const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/" +
            "Eldergrove_Atelier_AnimationPrototype.prefab";
        private const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "EldergroveAtelierAnimationPrototype.unity";

        [Test]
        public void PrototypeUsesApprovedLifecycleAndContainedCultivationActivity()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            MonoBehaviour controller = RequireComponent(
                prefab,
                "AL.Kingdom.Visuals.Architecture." +
                "ArchitectureConstructionAnimationController");
            Assert.That(ReadProperty<string>(controller, "ProfileId"),
                Is.EqualTo("eldergrove.atelier"));
            Assert.That(ReadProperty<string>(controller, "RealmId"),
                Is.EqualTo("eldergrove"));
            Assert.That(ReadProperty<int>(controller, "StageCount"), Is.EqualTo(6));
            Assert.That(ReadProperty<int>(controller, "PersistentStageCount"),
                Is.EqualTo(5));
            Assert.That(ReadProperty<bool>(controller, "SupportsReducedMotion"), Is.True);

            MonoBehaviour activity = RequireComponent(
                prefab,
                "AL.Kingdom.Visuals.Architecture.EldergroveAtelierStableActivity");
            Assert.That(ReadProperty<int>(activity, "SapRendererCount"), Is.EqualTo(4));
            Assert.That(ReadProperty<bool>(activity, "HasWaterRipple"), Is.True);
            Assert.That(ReadProperty<bool>(activity, "HasProtectedLeaf"), Is.True);
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty);
        }

        [Test]
        public void PersistentStagesAndOperationalFitoutResolveFromTimeline()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                MonoBehaviour controller = RequireComponent(
                    instance,
                    "AL.Kingdom.Visuals.Architecture." +
                    "ArchitectureConstructionAnimationController");

                Invoke(controller, "SetPreviewTime", 1.54f);
                Assert.That(
                    instance.transform.Find("PlotPrepared").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("CraftFrameSet").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find("CultivationOperational").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndLanternSet/RoofWestOcclusion").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndLanternSet/RoofEastOcclusion").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndLanternSet/LanternOcclusion").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "CraftFrameSet/TemporaryGuideFrame").gameObject.activeSelf,
                    Is.False);

                Invoke(controller, "SetPreviewTime", 2.1f);
                Assert.That(
                    instance.transform.Find(
                        "CraftFrameSet/TemporaryGuideFrame").gameObject.activeSelf,
                    Is.True);

                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));
                Assert.That(
                    ReadProperty<object>(controller, "CurrentState").ToString(),
                    Is.EqualTo("Operational"));
                Assert.That(
                    instance.transform.Find("CraftFrameSet").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("GuidedRootGrowth").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("RootVaultSettled").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("RoofAndLanternSet").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("CultivationOperational").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find(
                        "CraftFrameSet/TemporaryGuideFrame").gameObject.activeSelf,
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void StructuralRootsUseFixedAuthoredPathsAndGroundedBases()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform[] transforms =
                prefab.GetComponentsInChildren<Transform>(true);

            Assert.That(
                transforms.Count(transform =>
                    transform.name.StartsWith(
                        "AuthoredSegment_",
                        StringComparison.Ordinal)),
                Is.EqualTo(12));
            Assert.That(
                transforms.Count(transform =>
                    transform.name.StartsWith(
                        "GroundedTendril_",
                        StringComparison.Ordinal)),
                Is.EqualTo(8));
            Assert.That(
                transforms.Select(transform => transform.name),
                Has.None.Contains("Random"));
            Assert.That(
                prefab.transform.Find("GuidedRootGrowth/RootBase_Left"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("GuidedRootGrowth/RootBase_Right"),
                Is.Not.Null);
        }

        [Test]
        public void RoofPlanesRiseFromOuterEavesToLanternRidge()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform west = prefab.transform.Find(
                "RoofAndLanternSet/RoofWestOcclusion/RoofWest");
            Transform east = prefab.transform.Find(
                "RoofAndLanternSet/RoofEastOcclusion/RoofEast");
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
        public void CutawayRemovesRoofAndLanternWithoutMovingSettledRoots()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                MonoBehaviour controller = RequireComponent(
                    instance,
                    "AL.Kingdom.Visuals.Architecture." +
                    "ArchitectureConstructionAnimationController");
                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));
                Invoke(controller, "SetCutaway", true);

                Assert.That(
                    instance.transform.Find(
                        "RoofAndLanternSet/RoofWestOcclusion").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndLanternSet/RoofEastOcclusion").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndLanternSet/LanternOcclusion").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find("CraftFrameSet").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("RootVaultSettled").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("CultivationOperational").gameObject.activeSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PrototypeSceneIsExcludedFromProductionBuildSettings()
        {
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Not.Contain(ScenePath));
        }

        private static MonoBehaviour RequireComponent(
            GameObject target,
            string componentTypeName)
        {
            MonoBehaviour component = target
                .GetComponents<MonoBehaviour>()
                .SingleOrDefault(candidate =>
                    candidate != null &&
                    candidate.GetType().FullName == componentTypeName);
            Assert.That(component, Is.Not.Null, $"Missing {componentTypeName}.");
            return component;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property {propertyName}.");
            return (T)property.GetValue(target);
        }

        private static void Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            Type[] parameterTypes =
                arguments.Select(argument => argument.GetType()).ToArray();
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
            method.Invoke(target, arguments);
        }
    }
}
