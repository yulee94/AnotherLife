using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class StoneholdWorkshopAnimationPrototypeTests
    {
        private const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/" +
            "Stonehold_Workshop_AnimationPrototype.prefab";
        private const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "StoneholdWorkshopAnimationPrototype.unity";

        [Test]
        public void PrototypeUsesApprovedCentralizedLifecycleAndFunctionalActivity()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            MonoBehaviour controller = RequireComponent(
                prefab,
                "AL.Kingdom.Visuals.Architecture." +
                "ArchitectureConstructionAnimationController");
            Assert.That(ReadProperty<string>(controller, "ProfileId"),
                Is.EqualTo("stonehold.workshop"));
            Assert.That(ReadProperty<string>(controller, "RealmId"),
                Is.EqualTo("stonehold"));
            Assert.That(ReadProperty<int>(controller, "StageCount"), Is.EqualTo(6));
            Assert.That(ReadProperty<int>(controller, "PersistentStageCount"),
                Is.EqualTo(5));
            Assert.That(ReadProperty<bool>(controller, "SupportsReducedMotion"), Is.True);

            MonoBehaviour activity = RequireComponent(
                prefab,
                "AL.Kingdom.Visuals.Architecture.StoneholdWorkshopStableActivity");
            Assert.That(ReadProperty<bool>(activity, "HasBellows"), Is.True);
            Assert.That(ReadProperty<bool>(activity, "HasHammer"), Is.True);
            Assert.That(ReadProperty<int>(activity, "ForgeRendererCount"), Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty);
        }

        [Test]
        public void PersistentStatesAreDirectlyAddressableAndOperationalMassStaysFixed()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                MonoBehaviour controller = RequireComponent(
                    instance,
                    "AL.Kingdom.Visuals.Architecture." +
                    "ArchitectureConstructionAnimationController");
                Invoke(controller, "SetReducedMotion", true);
                Invoke(
                    controller,
                    "SetPreviewTime",
                    ReadProperty<float>(controller, "PresentationDuration"));

                Assert.That(
                    ReadProperty<object>(controller, "CurrentState").ToString(),
                    Is.EqualTo("Operational"));
                Assert.That(instance.transform.Find("PlotPrepared").gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.Find("FoundationSeated").gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.Find("WallShellLocked").gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.Find("RoofAndChimneySet").gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.Find("FittedOut").gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CutawayHidesRoofWithoutMovingLoadBearingStructure()
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
                    instance.transform.Find("RoofAndChimneySet").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndChimneySet/RoofWestRigidGroup").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndChimneySet/RoofEastRigidGroup").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndChimneySet/RoofRidgeRigidGroup").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find(
                        "RoofAndChimneySet/ChimneyRigidGroup").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    instance.transform.Find("FoundationSeated").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("WallShellLocked").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    instance.transform.Find("FittedOut").gameObject.activeSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void RoofPlanesRiseFromOuterEavesToCentralRidge()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform west = prefab.transform.Find(
                "RoofAndChimneySet/RoofWestRigidGroup/RoofSlabWest");
            Transform east = prefab.transform.Find(
                "RoofAndChimneySet/RoofEastRigidGroup/RoofSlabEast");
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
        public void RoofStageUsesIndependentRigidGroupsAndBoundedRibDetail()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform roof = prefab.transform.Find("RoofAndChimneySet");
            Assert.That(roof, Is.Not.Null);
            Assert.That(roof.Find("RoofWestRigidGroup"), Is.Not.Null);
            Assert.That(roof.Find("RoofEastRigidGroup"), Is.Not.Null);
            Assert.That(roof.Find("RoofRidgeRigidGroup"), Is.Not.Null);
            Assert.That(roof.Find("ChimneyRigidGroup"), Is.Not.Null);

            int roofRibCount = roof
                .GetComponentsInChildren<Transform>(true)
                .Count(transform => transform.name.Contains("RoofRib_"));
            Assert.That(roofRibCount, Is.EqualTo(12));
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
