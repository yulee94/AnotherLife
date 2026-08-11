#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AL.Development;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    [TestFixture]
    public sealed class GameTestModePlannerTests
    {
        private readonly List<EditorGameTestModePlan> _plans =
            new List<EditorGameTestModePlan>();
        private FieldInfo _saveFactoryField;
        private object _originalSaveFactory;

        [SetUp]
        public void SetUp()
        {
            EditorGameTestModeBootstrap.Disarm();
            Type stackType = typeof(AL.Core.Bootloader).Assembly.GetType(
                "AL.Core.OfflineServiceStack",
                throwOnError: true);
            _saveFactoryField = stackType.GetField(
                "SaveGameFactoryOverride",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(_saveFactoryField);
            _originalSaveFactory = _saveFactoryField.GetValue(null);
            _saveFactoryField.SetValue(null, null);
            _plans.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                EditorGameTestModeBootstrap.Disarm();
                foreach (EditorGameTestModePlan plan in _plans)
                {
                    if (!Directory.Exists(plan.IsolatedSaveRoot))
                    {
                        continue;
                    }

                    string cleanupMessage = string.Empty;
                    if (!EditorGameTestModeBootstrap.TryValidateOwnedRoot(
                            plan,
                            requireFreshRoot: false,
                            out _,
                            out string validationMessage) ||
                        !EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                            plan,
                            out _,
                            out cleanupMessage))
                    {
                        TestContext.Out.WriteLine(
                            "Retained isolated test evidence: " +
                            (string.IsNullOrWhiteSpace(validationMessage)
                                ? cleanupMessage
                                : validationMessage));
                    }
                }
            }
            finally
            {
                _saveFactoryField.SetValue(null, _originalSaveFactory);
            }
        }

        [Test]
        public void CanonicalFreshPlanIsAccepted()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);

            Assert.IsTrue(EditorGameTestModeBootstrap.TryValidateOwnedRoot(
                plan,
                requireFreshRoot: true,
                out EditorGameTestModeFailure failure,
                out string message), message);
            Assert.AreEqual(EditorGameTestModeFailure.None, failure);
            StringAssert.EndsWith(plan.SessionId, plan.IsolatedSaveRoot);
        }

        [TestCase(false, true, EditorGameTestModeFailure.FullDomainReloadRequired)]
        [TestCase(true, false, EditorGameTestModeFailure.FullSceneReloadRequired)]
        [TestCase(false, false, EditorGameTestModeFailure.FullDomainReloadRequired)]
        public void DisabledReloadPolicyRejectsWithoutChangingSettings(
            bool fullDomainReload,
            bool fullSceneReload,
            EditorGameTestModeFailure expected)
        {
            string sessionId = Guid.NewGuid().ToString("N");
            string tempRoot = Path.GetTempPath();
            string isolatedRoot = EditorGameTestModeBootstrap.BuildExpectedIsolatedRoot(
                tempRoot,
                sessionId);

            Assert.IsFalse(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                tempRoot,
                Path.Combine(tempRoot, "developer-profile-" + Guid.NewGuid().ToString("N")),
                isolatedRoot,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload,
                fullSceneReload,
                out _,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(expected, failure);
            Assert.IsFalse(Directory.Exists(isolatedRoot));
        }

        [TestCase("not-a-guid")]
        [TestCase("00000000000000000000000000000000")]
        [TestCase("ABCDEF0123456789ABCDEF0123456789")]
        public void InvalidSessionIdRejects(string sessionId)
        {
            string tempRoot = Path.GetTempPath();
            Assert.IsFalse(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                tempRoot,
                Application.persistentDataPath,
                Path.Combine(tempRoot, "AnotherLife", "GameTestMode", sessionId),
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out _,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.InvalidSessionId, failure);
        }

        [Test]
        public void RootMustBeTheExactGuidOwnedTemporaryPath()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            string tempRoot = Path.GetTempPath();
            Assert.IsFalse(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                tempRoot,
                Application.persistentDataPath,
                Path.Combine(tempRoot, "wrong", sessionId),
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out _,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.IsolatedRootMismatch, failure);
        }

        [Test]
        public void CallerSuppliedPersistentRootRejectsAgainstLiveEnvironment()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            string tempRoot = Path.GetTempPath();
            string isolatedRoot = EditorGameTestModeBootstrap.BuildExpectedIsolatedRoot(
                tempRoot,
                sessionId);

            Assert.IsFalse(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                tempRoot,
                isolatedRoot,
                isolatedRoot,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out _,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.EnvironmentBindingMismatch, failure);
        }

        [Test]
        public void CallerSuppliedTemporaryRootCannotRedirectThePlan()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            string falseTemporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "not-the-live-temp-" + Guid.NewGuid().ToString("N"));
            string isolatedRoot = EditorGameTestModeBootstrap.BuildExpectedIsolatedRoot(
                falseTemporaryRoot,
                sessionId);

            Assert.IsFalse(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                falseTemporaryRoot,
                Application.persistentDataPath,
                isolatedRoot,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out _,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.EnvironmentBindingMismatch, failure);
            Assert.IsFalse(Directory.Exists(isolatedRoot));
        }

        [Test]
        public void MarkerMissingOrChangedRejects()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);
            string markerPath = Path.Combine(
                plan.IsolatedSaveRoot,
                EditorGameTestModeBootstrap.MarkerFileName);

            try
            {
                File.Delete(markerPath);
                Assert.IsFalse(EditorGameTestModeBootstrap.TryValidateOwnedRoot(
                    plan,
                    requireFreshRoot: true,
                    out EditorGameTestModeFailure missingFailure,
                    out _));
                Assert.AreEqual(EditorGameTestModeFailure.OwnershipMarkerMissing, missingFailure);

                File.WriteAllText(markerPath, "wrong-session");
                Assert.IsFalse(EditorGameTestModeBootstrap.TryValidateOwnedRoot(
                    plan,
                    requireFreshRoot: true,
                    out EditorGameTestModeFailure mismatchFailure,
                    out _));
                Assert.AreEqual(EditorGameTestModeFailure.OwnershipMarkerMismatch, mismatchFailure);
            }
            finally
            {
                RestoreTestMarker(plan);
            }
        }

        [Test]
        public void ExtraFileRejectsFreshFirstUserRoot()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);
            File.WriteAllText(Path.Combine(plan.IsolatedSaveRoot, "unexpected.bin"), "x");

            Assert.IsFalse(EditorGameTestModeBootstrap.TryValidateOwnedRoot(
                plan,
                requireFreshRoot: true,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.IsolatedRootNotFresh, failure);
        }

        [Test]
        public void GuardedFactoryRevalidatesMarkerOnEveryInvocation()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(plan, out _, out string message), message);

            var factory = (Delegate)_saveFactoryField.GetValue(null);
            object first = factory.DynamicInvoke();
            Assert.IsNotNull(first);

            try
            {
                File.WriteAllText(
                    Path.Combine(plan.IsolatedSaveRoot, EditorGameTestModeBootstrap.MarkerFileName),
                    "tampered");
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => factory.DynamicInvoke());
                Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            }
            finally
            {
                RestoreTestMarker(plan);
            }
        }

        [Test]
        public void ForeignFactoryIsNeverReplaced()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);
            Func<object> foreignFactory = () => new object();
            _saveFactoryField.SetValue(null, foreignFactory);

            Assert.IsFalse(EditorGameTestModeBootstrap.TryArm(
                plan,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.ForeignSaveFactoryPresent, failure);
            Assert.AreSame(foreignFactory, _saveFactoryField.GetValue(null));
        }

        [Test]
        public void FailClosedGuardRestoresAFactoryDisplacedDuringRecovery()
        {
            Func<object> foreignFactory = () => new object();
            _saveFactoryField.SetValue(null, foreignFactory);

            Assert.DoesNotThrow(() => EditorGameTestModeBootstrap.EnterFailClosedState(
                Guid.NewGuid().ToString("N"),
                "forced recovery failure"));
            Assert.IsNull(GameObject.Find("[AL] Isolated Game Test Mode"));
            Assert.AreNotSame(foreignFactory, _saveFactoryField.GetValue(null));

            var failClosedFactory = (Delegate)_saveFactoryField.GetValue(null);
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => failClosedFactory.DynamicInvoke());
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            StringAssert.Contains(
                "AL-ISOLATED-TEST-FAIL-CLOSED: forced recovery failure",
                exception.InnerException.Message);

            EditorGameTestModeBootstrap.Disarm();
            Assert.AreSame(foreignFactory, _saveFactoryField.GetValue(null));
        }

        [Test]
        public void DifferentSessionCannotReplaceArmedSession()
        {
            EditorGameTestModePlan first = CreatePlan(createRoot: true);
            EditorGameTestModePlan second = CreatePlan(createRoot: true);
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(first, out _, out string message), message);

            Assert.IsFalse(EditorGameTestModeBootstrap.TryArm(
                second,
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.DifferentSessionAlreadyArmed, failure);
            Assert.AreEqual(first.SessionId, EditorGameTestModeBootstrap.ActiveSessionId);
        }

        [Test]
        public void InvalidCleanupMarkerRetainsEvidence()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);
            try
            {
                File.WriteAllText(
                    Path.Combine(plan.IsolatedSaveRoot, EditorGameTestModeBootstrap.MarkerFileName),
                    "not-owned");

                Assert.IsFalse(EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                    plan,
                    out EditorGameTestModeFailure failure,
                    out _));
                Assert.AreEqual(EditorGameTestModeFailure.OwnershipMarkerMismatch, failure);
                Assert.IsTrue(Directory.Exists(plan.IsolatedSaveRoot));
            }
            finally
            {
                RestoreTestMarker(plan);
            }
        }

        [Test]
        public void ValidCleanupDeletesOnlyTheExactOwnedRoot()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);
            string sibling = plan.IsolatedSaveRoot + "-sibling";
            Directory.CreateDirectory(sibling);
            File.WriteAllText(Path.Combine(sibling, "sentinel.txt"), "preserve");

            try
            {
                Assert.IsTrue(EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                    plan,
                    out _,
                    out string message), message);
                Assert.IsFalse(Directory.Exists(plan.IsolatedSaveRoot));
                Assert.AreEqual("preserve", File.ReadAllText(Path.Combine(sibling, "sentinel.txt")));
            }
            finally
            {
                if (Directory.Exists(sibling))
                {
                    string sentinel = Path.Combine(sibling, "sentinel.txt");
                    if (File.Exists(sentinel))
                    {
                        File.Delete(sentinel);
                    }

                    Directory.Delete(sibling, recursive: false);
                }
            }
        }

        [Test]
        public void CleanupRejectsAnInventoryAboveTheBoundWithoutDeletingIt()
        {
            EditorGameTestModePlan plan = CreatePlan(createRoot: true);
            var generated = new List<string>();
            for (int index = 0; index < 256; index++)
            {
                string path = Path.Combine(plan.IsolatedSaveRoot, "entry-" + index.ToString("D3") + ".tmp");
                File.WriteAllBytes(path, Array.Empty<byte>());
                generated.Add(path);
            }

            try
            {
                Assert.IsFalse(EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                    plan,
                    out EditorGameTestModeFailure failure,
                    out _));
                Assert.AreEqual(EditorGameTestModeFailure.CleanupInventoryTooLarge, failure);
                Assert.IsTrue(Directory.Exists(plan.IsolatedSaveRoot));
            }
            finally
            {
                Assert.IsTrue(EditorGameTestModeBootstrap.TryValidateOwnedRoot(
                    plan,
                    requireFreshRoot: false,
                    out _,
                    out string cleanupMessage), cleanupMessage);
                string validatedRoot = Path.GetFullPath(plan.IsolatedSaveRoot);
                foreach (string path in generated)
                {
                    string validatedPath = Path.GetFullPath(path);
                    Assert.AreEqual(validatedRoot, Path.GetDirectoryName(validatedPath));
                    var file = new FileInfo(validatedPath);
                    file.Refresh();
                    if (!file.Exists)
                    {
                        continue;
                    }

                    Assert.IsFalse(
                        (file.Attributes & FileAttributes.ReparsePoint) != 0,
                        "Test cleanup refused to follow a replaced inventory entry.");
                    file.Delete();
                }
            }
        }

        private EditorGameTestModePlan CreatePlan(bool createRoot)
        {
            string sessionId = Guid.NewGuid().ToString("N");
            string tempRoot = Path.GetTempPath();
            string isolatedRoot = EditorGameTestModeBootstrap.BuildExpectedIsolatedRoot(
                tempRoot,
                sessionId);
            Assert.IsTrue(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                tempRoot,
                Application.persistentDataPath,
                isolatedRoot,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out EditorGameTestModePlan plan,
                out _,
                out string message), message);
            _plans.Add(plan);

            if (createRoot)
            {
                Assert.IsTrue(EditorGameTestModeBootstrap.TryCreateOwnedRoot(
                    plan,
                    out _,
                    out string creationMessage), creationMessage);
            }

            return plan;
        }

        private static void RestoreTestMarker(EditorGameTestModePlan plan)
        {
            var root = new DirectoryInfo(plan.IsolatedSaveRoot);
            root.Refresh();
            if (!root.Exists || (root.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "The test-owned isolated root was replaced; its marker was not rewritten.");
            }

            string markerPath = Path.Combine(
                plan.IsolatedSaveRoot,
                EditorGameTestModeBootstrap.MarkerFileName);
            var marker = new FileInfo(markerPath);
            marker.Refresh();
            if (marker.Exists && (marker.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "The test ownership marker was replaced by a reparse point.");
            }

            File.WriteAllBytes(
                markerPath,
                Encoding.UTF8.GetBytes(
                    EditorGameTestModeBootstrap.BuildMarkerContents(plan.SessionId)));
        }
    }
}
#endif
