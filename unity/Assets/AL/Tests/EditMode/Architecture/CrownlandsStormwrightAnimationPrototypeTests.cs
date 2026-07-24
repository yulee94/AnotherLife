using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class CrownlandsStormwrightAnimationPrototypeTests
    {
        private const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/" +
            "Crownlands_Stormwright_AnimationPrototype.prefab";

        private const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "CrownlandsStormwrightAnimationPrototype.unity";

        private const string ControllerTypeName =
            "AL.Kingdom.Visuals.Architecture." +
            "ArchitectureConstructionAnimationController";

        private const string ActivityTypeName =
            "AL.Kingdom.Visuals.Architecture." +
            "CrownlandsStormwrightStableActivity";

        private static readonly string[] ApprovedProfilePaths =
        {
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
                "Stonehold_Workshop_ConstructionProfile.asset",
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
                "Eldergrove_Atelier_ConstructionProfile.asset",
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
                "Crownlands_Stormwright_ConstructionProfile.asset",
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
                "Umbral_Veilwright_ConstructionProfile.asset"
        };

        [Test]
        public void GeneratedPrefabContainsCentralizedMobileAnimationController()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            MonoBehaviour controller = RequireController(prefab);
            Assert.That(controller, Is.Not.Null);
            Assert.That(ReadProperty<int>(controller, "StageCount"), Is.EqualTo(6));
            Assert.That(
                ReadProperty<int>(controller, "PersistentStageCount"),
                Is.EqualTo(5));
            Assert.That(
                ReadProperty<string>(controller, "ProfileId"),
                Is.EqualTo("crownlands.stormwright"));
            Assert.That(
                ReadProperty<string>(controller, "RealmId"),
                Is.EqualTo("crownlands"));

            MonoBehaviour activity = RequireComponent(prefab, ActivityTypeName);
            Assert.That(
                ReadProperty<int>(activity, "PulseRouteNodeCount"),
                Is.GreaterThanOrEqualTo(5));
            Assert.That(ReadProperty<bool>(controller, "SupportsReducedMotion"), Is.True);
            Assert.That(
                prefab.GetComponentsInChildren<Animator>(true),
                Is.Empty,
                "Static modules must not receive independent always-running Animators.");
        }

        [Test]
        public void DeterministicPreviewReachesStableAndReducedMotionStates()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            GameObject instance = Object.Instantiate(prefab);

            try
            {
                MonoBehaviour controller = RequireController(instance);
                Invoke(controller, "SetReducedMotion", true);
                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));

                Assert.That(
                    ReadProperty<object>(controller, "CurrentState").ToString(),
                    Is.EqualTo("Operational"));
                Assert.That(ReadProperty<bool>(controller, "ReducedMotion"), Is.True);
                Assert.That(instance.transform.Find("PlotPrepared").gameObject.activeSelf, Is.True);
                Assert.That(
                    instance.transform.Find("CivicFrameRaised").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("SilverRibFront").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("RoofWingWest").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("CalibrationEngine").gameObject.activeSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CutawayHidesOnlyRoofAndLanternOwnershipGroups()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            GameObject instance = Object.Instantiate(prefab);

            try
            {
                MonoBehaviour controller = RequireController(instance);
                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));
                Invoke(controller, "SetCutaway", true);

                Assert.That(
                    instance.transform.Find("RoofWingWest").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find("RoofWingEast").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find("LanternOcclusion").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find("CivicFrameRaised").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("CalibrationEngine").gameObject.activeSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ApprovedRealmProfilesUseOneReusableConstructionContract()
        {
            foreach (string profilePath in ApprovedProfilePaths)
            {
                ScriptableObject profile =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(profilePath);
                Assert.That(profile, Is.Not.Null, $"Missing profile {profilePath}.");
                Assert.That(
                    profile.GetType().FullName,
                    Is.EqualTo(
                        "AL.Kingdom.Visuals.Architecture." +
                        "ArchitectureConstructionAnimationProfile"));
                Assert.That(
                    ReadProperty<bool>(profile, "IsConfigured"),
                    Is.True,
                    $"Invalid profile {profilePath}.");
                Assert.That(
                    ReadProperty<int>(profile, "StageMotionCount"),
                    Is.EqualTo(5));
            }
        }

        [Test]
        public void PrototypeSceneIsExcludedFromProductionBuildSettings()
        {
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Not.Contain(ScenePath));
        }

        private static MonoBehaviour RequireController(GameObject target)
        {
            return RequireComponent(target, ControllerTypeName);
        }

        private static MonoBehaviour RequireComponent(
            GameObject target,
            string componentTypeName)
        {
            MonoBehaviour controller = target
                .GetComponents<MonoBehaviour>()
                .SingleOrDefault(component =>
                    component != null &&
                    component.GetType().FullName == componentTypeName);
            Assert.That(
                controller,
                Is.Not.Null,
                $"Missing component {componentTypeName}.");
            return controller;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property {propertyName}.");
            return (T)property.GetValue(target);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            Type[] parameterTypes = arguments.Select(argument => argument.GetType()).ToArray();
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
