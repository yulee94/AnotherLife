using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Startup-marker contract exercised through the Evaluate/Emit seam (#223 marker requirements,
    /// spec section 9): a valid activation emits exactly one [AL-SCENE-ACTIVE] success line; a path/name
    /// mismatch produces one Invalid report and exactly one [AL-SCENE-ACTIVE-MISMATCH] error with no
    /// success line; and Emit is guarded to fire at most once.
    /// </summary>
    public sealed class ProductionSceneMarkerTests
    {
        private readonly List<string> _logs = new List<string>();

        [SetUp]
        public void Subscribe()
        {
            _logs.Clear();
            Application.logMessageReceived += Capture;
        }

        [TearDown]
        public void Unsubscribe()
        {
            Application.logMessageReceived -= Capture;
        }

        private void Capture(string condition, string stackTrace, LogType type)
        {
            _logs.Add(condition);
        }

        [Test]
        public void MismatchedPathAndNameEmitsOneErrorAndNoSuccessLine()
        {
            var host = new GameObject("MarkerMismatch");
            try
            {
                object marker = ConfigureBootMarker(host);

                LogAssert.Expect(LogType.Error, new Regex(@"\[AL-SCENE-ACTIVE-MISMATCH\] id=al_scene_boot"));
                object report = R.Invoke(marker, "Emit", "Assets/AL/Scenes/Wrong.unity", "Wrong");

                Assert.IsFalse(R.PropBool(report, "IsValid"), "Mismatched activation must yield an Invalid report.");
                Assert.IsTrue(R.PropBool(report, "Emitted"), "First Emit must count as emitted.");
                Assert.IsNotEmpty(R.AsStrings(R.Prop(report, "Failures")), "Invalid report must list failures.");

                Assert.IsFalse(SuccessLines().Any(), "A mismatch must not emit an [AL-SCENE-ACTIVE] success line.");
                Assert.AreEqual(1, MismatchLines().Count(), "Exactly one mismatch error line expected.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ValidActivationEmitsExactlyOneSuccessLine()
        {
            var host = new GameObject("MarkerValid");
            try
            {
                object marker = ConfigureBootMarker(host);
                object report = R.Invoke(marker, "Emit", "Assets/AL/Scenes/Boot.unity", "Boot");

                Assert.IsTrue(R.PropBool(report, "IsValid"), "Correct activation must yield a valid report.");
                Assert.AreEqual(1, SuccessLines().Count(), "Exactly one [AL-SCENE-ACTIVE] success line expected.");
                Assert.IsFalse(MismatchLines().Any());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EmitIsGuardedToFireAtMostOnce()
        {
            var host = new GameObject("MarkerOnce");
            try
            {
                object marker = ConfigureBootMarker(host);

                object first = R.Invoke(marker, "Emit", "Assets/AL/Scenes/Boot.unity", "Boot");
                object second = R.Invoke(marker, "Emit", "Assets/AL/Scenes/Boot.unity", "Boot");

                Assert.IsTrue(R.PropBool(first, "Emitted"), "First Emit must emit.");
                Assert.IsFalse(R.PropBool(second, "Emitted"), "Second Emit must report AlreadyEmitted.");
                Assert.AreEqual(1, SuccessLines().Count(), "The once-guard must allow exactly one success line.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static object ConfigureBootMarker(GameObject host)
        {
            object marker = host.AddComponent(R.Runtime(R.MarkerType));
            R.SetField(marker, "_sceneId", "al_scene_boot");
            R.SetField(marker, "_expectedAssetPath", "Assets/AL/Scenes/Boot.unity");
            R.SetField(marker, "_role", "production_entry");
            R.SetField(marker, "_sourceVersion", R.SourceVersion());
            return marker;
        }

        private IEnumerable<string> SuccessLines()
        {
            return _logs.Where(l => l.StartsWith("[AL-SCENE-ACTIVE] id=", System.StringComparison.Ordinal));
        }

        private IEnumerable<string> MismatchLines()
        {
            return _logs.Where(l => l.Contains("[AL-SCENE-ACTIVE-MISMATCH]"));
        }
    }
}
