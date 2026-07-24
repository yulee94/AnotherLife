using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class UmbralVeilwrightAnimationPrototypeTests
    {
        private const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/" +
            "Umbral_Veilwright_AnimationPrototype.prefab";

        private const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "UmbralVeilwrightAnimationPrototype.unity";

        private const string ControllerTypeName =
            "AL.Kingdom.Visuals.Architecture." +
            "ArchitectureConstructionAnimationController";

        private const string ActivityTypeName =
            "AL.Kingdom.Visuals.Architecture." +
            "UmbralVeilwrightStableActivity";

        [Test]
        public void GeneratedPrefabUsesSharedControllerAndFourAuthoredAnchors()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            MonoBehaviour controller = RequireComponent(
                prefab,
                ControllerTypeName);
            MonoBehaviour activity = RequireComponent(
                prefab,
                ActivityTypeName);

            Assert.That(ReadProperty<int>(controller, "StageCount"), Is.EqualTo(6));
            Assert.That(
                ReadProperty<int>(controller, "PersistentStageCount"),
                Is.EqualTo(5));
            Assert.That(
                ReadProperty<string>(controller, "ProfileId"),
                Is.EqualTo("umbral.veilwright"));
            Assert.That(
                ReadProperty<string>(controller, "RealmId"),
                Is.EqualTo("umbral"));
            Assert.That(
                ReadProperty<int>(activity, "AnchorCount"),
                Is.EqualTo(4));
            Assert.That(
                ReadProperty<bool>(activity, "SupportsReducedMotion"),
                Is.True);
            Assert.That(
                prefab.GetComponentsInChildren<Animator>(true),
                Is.Empty,
                "Static architecture must not receive independent always-running Animators.");
            Assert.That(
                prefab.GetComponentsInChildren<Light>(true),
                Has.Length.EqualTo(1),
                "The prototype may use only one localized activity light.");
            Assert.That(
                prefab.GetComponentsInChildren<Renderer>(true).Length,
                Is.LessThanOrEqualTo(200),
                "The mobile graybox must retain a bounded renderer count.");
            Assert.That(
                ReadProperty<float>(activity, "EventEnd"),
                Is.LessThan(
                    ReadProperty<float>(controller, "PresentationDuration")),
                "The authored activity must return to a silent hold.");
        }

        [Test]
        public void DeterministicPreviewReachesCompleteStableState()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            GameObject instance = Object.Instantiate(prefab);

            try
            {
                MonoBehaviour controller = RequireComponent(
                    instance,
                    ControllerTypeName);
                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));

                Assert.That(
                    ReadProperty<object>(controller, "CurrentState").ToString(),
                    Is.EqualTo("Operational"));
                AssertStageActive(instance, "BoundaryMarked");
                AssertStageActive(instance, "OffsetShellRaised");
                AssertStageActive(instance, "VeilAnchorsBound");
                AssertStageActive(instance, "RoofOcclusionWest");
                AssertStageActive(instance, "RoofOcclusionEast");
                AssertStageActive(instance, "WardChimney");
                AssertStageActive(instance, "ReliquariesGrounded");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CutawayHidesOnlyAuthoredRoofOwnershipGroups()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            GameObject instance = Object.Instantiate(prefab);

            try
            {
                MonoBehaviour controller = RequireComponent(
                    instance,
                    ControllerTypeName);
                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));
                Invoke(controller, "SetCutaway", true);

                AssertStageInactive(instance, "RoofOcclusionWest");
                AssertStageInactive(instance, "RoofOcclusionEast");
                AssertStageActive(instance, "WardChimney");
                Assert.That(
                    instance.transform
                        .Find("OffsetShellRaised/FrontFacadeOcclusion")
                        .gameObject.activeSelf,
                    Is.False);
                AssertStageActive(instance, "BoundaryMarked");
                AssertStageActive(instance, "OffsetShellRaised");
                AssertStageActive(instance, "VeilAnchorsBound");
                AssertStageActive(instance, "ReliquariesGrounded");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ReducedMotionRemovesTravelWhileRetainingFinalArchitecture()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            GameObject instance = Object.Instantiate(prefab);

            try
            {
                MonoBehaviour controller = RequireComponent(
                    instance,
                    ControllerTypeName);
                Invoke(controller, "SetReducedMotion", true);
                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));

                Assert.That(
                    ReadProperty<bool>(controller, "ReducedMotion"),
                    Is.True);
                Assert.That(
                    instance.transform
                        .Find("ReliquariesGrounded/ConvergenceOrb")
                        .gameObject.activeSelf,
                    Is.False);
                AssertStageActive(instance, "OffsetShellRaised");
                AssertStageActive(instance, "ReliquariesGrounded");
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

        private static void AssertStageActive(GameObject root, string path)
        {
            Transform stage = root.transform.Find(path);
            Assert.That(stage, Is.Not.Null, $"Missing stage {path}.");
            Assert.That(stage.gameObject.activeSelf, Is.True, path);
        }

        private static void AssertStageInactive(GameObject root, string path)
        {
            Transform stage = root.transform.Find(path);
            Assert.That(stage, Is.Not.Null, $"Missing stage {path}.");
            Assert.That(stage.gameObject.activeSelf, Is.False, path);
        }

        private static MonoBehaviour RequireComponent(
            GameObject target,
            string componentTypeName)
        {
            MonoBehaviour component = target
                .GetComponents<MonoBehaviour>()
                .SingleOrDefault(item =>
                    item != null &&
                    item.GetType().FullName == componentTypeName);
            Assert.That(
                component,
                Is.Not.Null,
                $"Missing component {componentTypeName}.");
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
            Type[] parameterTypes = arguments
                .Select(argument => argument.GetType())
                .ToArray();
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
