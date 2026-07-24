using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class UmbralVeilwrightStableActivityTests
    {
        private const string ActivityTypeName =
            "AL.Kingdom.Visuals.Architecture." +
            "UmbralVeilwrightStableActivity";

        [Test]
        public void AuthoredConvergenceUsesFourAnchorsAndReturnsToSleep()
        {
            ActivityFixture fixture = CreateFixture();

            try
            {
                Assert.That(
                    ReadProperty<int>(fixture.Activity, "AnchorCount"),
                    Is.EqualTo(4));
                Assert.That(
                    ReadProperty<bool>(
                        fixture.Activity,
                        "SupportsReducedMotion"),
                    Is.True);

                Invoke(
                    fixture.Activity,
                    "EvaluateActivity",
                    10.65f,
                    false);
                Assert.That(fixture.Orb.activeSelf, Is.True);

                Invoke(
                    fixture.Activity,
                    "EvaluateActivity",
                    ReadProperty<float>(
                        fixture.Activity,
                        "EventEnd") + 0.1f,
                    false);
                Assert.That(fixture.Orb.activeSelf, Is.False);
                Assert.That(
                    fixture.Ring.transform.localRotation,
                    Is.EqualTo(Quaternion.identity));
                Assert.That(
                    fixture.Ring.transform.localScale,
                    Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void ReducedMotionRemovesTravelAndRingRotation()
        {
            ActivityFixture fixture = CreateFixture();

            try
            {
                Invoke(
                    fixture.Activity,
                    "EvaluateActivity",
                    11.65f,
                    false);
                Assert.That(
                    fixture.Ring.transform.localRotation,
                    Is.Not.EqualTo(Quaternion.identity));

                Invoke(
                    fixture.Activity,
                    "EvaluateActivity",
                    11.65f,
                    true);
                Assert.That(fixture.Orb.activeSelf, Is.False);
                Assert.That(
                    fixture.Ring.transform.localRotation,
                    Is.EqualTo(Quaternion.identity));
                Assert.That(
                    fixture.Ring.transform.localScale,
                    Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void EvaluationIsDeterministicAtAnyPresentationTime()
        {
            ActivityFixture fixture = CreateFixture();

            try
            {
                Invoke(
                    fixture.Activity,
                    "EvaluateActivity",
                    10.65f,
                    false);
                Vector3 expectedPosition = fixture.Orb.transform.position;

                Invoke(
                    fixture.Activity,
                    "EvaluateActivity",
                    12.7f,
                    false);
                Invoke(
                    fixture.Activity,
                    "EvaluateActivity",
                    10.65f,
                    false);

                Assert.That(fixture.Orb.activeSelf, Is.True);
                Assert.That(
                    fixture.Orb.transform.position,
                    Is.EqualTo(expectedPosition));
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        private static ActivityFixture CreateFixture()
        {
            var root = new GameObject("UmbralVeilwrightActivityFixture");
            Type activityType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly =>
                    assembly.GetType(
                        ActivityTypeName,
                        false))
                .FirstOrDefault(type => type != null);
            Assert.That(
                activityType,
                Is.Not.Null,
                $"Missing runtime type {ActivityTypeName}.");
            var activity =
                (MonoBehaviour)root.AddComponent(activityType);
            var anchors = new Transform[4];
            for (int index = 0; index < anchors.Length; index++)
            {
                var anchor = new GameObject($"Anchor_{index}");
                anchor.transform.SetParent(root.transform, false);
                anchor.transform.position = new Vector3(
                    index - 1.5f,
                    0f,
                    index % 2 == 0 ? -1f : 1f);
                anchors[index] = anchor.transform;
            }

            var core = new GameObject("Core");
            core.transform.SetParent(root.transform, false);
            core.transform.position = Vector3.zero;

            var chimney = new GameObject("Chimney");
            chimney.transform.SetParent(root.transform, false);
            chimney.transform.position = new Vector3(0.8f, 3f, 0.4f);

            var orb = new GameObject("ConvergenceOrb");
            orb.transform.SetParent(root.transform, false);

            var ring = new GameObject("EclipseRing");
            ring.transform.SetParent(root.transform, false);

            Invoke(
                activity,
                "Configure",
                anchors,
                core.transform,
                chimney.transform,
                orb.transform,
                ring.transform,
                null,
                null,
                null,
                null,
                null);

            return new ActivityFixture(
                root,
                activity,
                orb,
                ring);
        }

        private static T ReadProperty<T>(
            object target,
            string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                property,
                Is.Not.Null,
                $"Missing property {propertyName}.");
            return (T)property.GetValue(target);
        }

        private static void Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                method,
                Is.Not.Null,
                $"Missing method {methodName}.");
            method.Invoke(target, arguments);
        }

        private readonly struct ActivityFixture
        {
            public ActivityFixture(
                GameObject root,
                MonoBehaviour activity,
                GameObject orb,
                GameObject ring)
            {
                Root = root;
                Activity = activity;
                Orb = orb;
                Ring = ring;
            }

            public GameObject Root { get; }
            public MonoBehaviour Activity { get; }
            public GameObject Orb { get; }
            public GameObject Ring { get; }
        }
    }
}
