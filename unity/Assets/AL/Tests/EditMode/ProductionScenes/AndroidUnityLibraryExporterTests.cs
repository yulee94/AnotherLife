using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Contract tests for the tooling-only Android unityLibrary export boundary. Editor tooling is
    /// loaded through reflection so the runtime test assembly does not acquire an AL.Editor edge.
    /// </summary>
    public sealed class AndroidUnityLibraryExporterTests
    {
        private static Type ExporterType => Runtime("AL.EditorTools.AndroidUnityLibraryExporter");
        private static Type ValidationType => Runtime("AL.EditorTools.BuildValidationSnapshot");
        private static Type SettingsType => Runtime("AL.EditorTools.AndroidUnityLibraryBuildSettingsSnapshot");
        private static Type ReportType => Runtime("AL.EditorTools.AndroidUnityLibraryBuildReportSnapshot");
        private static Type ArtifactsType => Runtime("AL.EditorTools.AndroidUnityLibraryArtifactSnapshot");
        private static Type EnvironmentType => Runtime("AL.EditorTools.IAndroidUnityLibraryExportEnvironment");

        [Test]
        public void PlanPinsExactGuardedAndroidDevelopmentExportProfile()
        {
            string root = ProjectRoot();
            object plan = CreatePlan(root, ExpectedOutput(root), ExpectedSummary(root));

            Assert.IsTrue(PropBool(plan, "IsValid"), Failures(plan));
            Assert.AreEqual("2022.3.62f3", PropString(plan, "UnityVersion"));
            Assert.AreEqual("IL2CPP", PropString(plan, "ScriptingBackend"));
            Assert.AreEqual("ARM64", PropString(plan, "TargetArchitectures"));
            Assert.AreEqual(24, PropInt(plan, "MinimumApiLevel"));
            Assert.AreEqual(ExpectedOutput(root), PropString(plan, "OutputDirectory"));
            Assert.AreEqual(ExpectedSummary(root), PropString(plan, "SummaryPath"));

            var options = (BuildPlayerOptions)Invoke(plan, "CreateBuildPlayerOptions");
            Assert.AreEqual(BuildTarget.Android, options.target);
            Assert.AreEqual(ExpectedOutput(root), options.locationPathName);
            Assert.AreEqual(
                BuildOptions.Development | BuildOptions.AcceptExternalModificationsToPlayer,
                options.options);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/AL/Scenes/Boot.unity",
                "Assets/AL/Scenes/RealmSelection.unity",
                "Assets/AL/Scenes/CharacterCreation.unity",
                "Assets/AL/Scenes/ChampionArena.unity",
                "Assets/AL/Scenes/Kingdom.unity"
            }, options.scenes);
        }

        [Test]
        public void PlanPinsDistinctNonDevelopmentReleaseProfile()
        {
            FieldInfo profile = ExporterType.GetField(
                "developmentExport",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(profile);
            object original = profile.GetValue(null);
            try
            {
                profile.SetValue(null, false);
                string root = ProjectRoot();
                object plan = CreatePlan(root, ExpectedOutput(root), ExpectedSummary(root));
                var options = (BuildPlayerOptions)Invoke(plan, "CreateBuildPlayerOptions");

                Assert.AreEqual(
                    BuildOptions.AcceptExternalModificationsToPlayer,
                    options.options);
            }
            finally
            {
                profile.SetValue(null, original);
            }
        }

        [Test]
        public void CleanupAndSummaryGuardsAcceptOnlyExactIgnoredDestinations()
        {
            string root = ProjectRoot();
            string expectedOutput = ExpectedOutput(root);
            string expectedSummary = ExpectedSummary(root);

            Assert.IsTrue((bool)Static("IsGuardedOutputDirectory", root, expectedOutput));
            Assert.IsTrue((bool)Static("IsGuardedSummaryPath", root, expectedSummary));

            string[] rejectedOutputs =
            {
                string.Empty,
                root,
                Path.Combine(root, "Assets"),
                Path.Combine(root, "Assets", "AndroidExport"),
                Path.Combine(root, "Builds"),
                Path.Combine(root, "Builds", "AndroidExport2"),
                Path.Combine(root, "Builds", "AndroidExport", "unityLibrary"),
                Path.Combine(Path.GetDirectoryName(root) ?? root, "AndroidExport")
            };
            foreach (string candidate in rejectedOutputs)
            {
                Assert.IsFalse(
                    (bool)Static("IsGuardedOutputDirectory", root, candidate),
                    candidate);
            }

            Assert.IsFalse((bool)Static(
                "IsGuardedSummaryPath",
                root,
                Path.Combine(root, "Logs", "AndroidUnityLibraryExportSummary-copy.json")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CleanupRejectsPreexistingAndReracedDescendantReparseEntriesWithoutRecursion(
            bool raceAfterScan)
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-cleanup-race-" + Guid.NewGuid().ToString("N"));
            string child = Path.Combine(temporary, "child.bin");
            try
            {
                Directory.CreateDirectory(temporary);
                File.WriteAllText(child, "must-remain");
                bool raced = false;
                var attributes = new Func<string, FileAttributes>(path =>
                {
                    FileAttributes actual = File.GetAttributes(path);
                    return PathsEqual(path, child) && (!raceAfterScan || raced)
                        ? actual | FileAttributes.ReparsePoint
                        : actual;
                });
                var hook = new Action<string, string>((stage, _) =>
                {
                    if (raceAfterScan && stage == "after-scan") raced = true;
                });

                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => Static(
                    "DeleteTreeWithoutFollowingReparsePoints",
                    temporary,
                    32,
                    32,
                    attributes,
                    hook));

                Assert.IsInstanceOf<IOException>(thrown.InnerException);
                Assert.IsTrue(File.Exists(child));
                Assert.AreEqual("must-remain", File.ReadAllText(child));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void CleanupRejectsRegularFileMoveAndReplacementRaceWithoutDeletingEitherSentinel()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-cleanup-file-replacement-" + Guid.NewGuid().ToString("N"));
            string output = Path.Combine(temporary, "output");
            string original = Path.Combine(output, "child.bin");
            string displaced = Path.Combine(temporary, "displaced.bin");
            string preparedReplacement = Path.Combine(temporary, "replacement.bin");
            try
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(original, "original-must-remain");
                File.WriteAllText(preparedReplacement, "replacement-must-remain");
                var hook = new Action<string, string>((stage, _) =>
                {
                    if (stage != "after-scan") return;
                    File.Move(original, displaced);
                    File.Move(preparedReplacement, original);
                });

                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => Static(
                    "DeleteTreeWithoutFollowingReparsePoints",
                    output,
                    32,
                    32,
                    new Func<string, FileAttributes>(File.GetAttributes),
                    hook));

                Assert.IsInstanceOf<IOException>(thrown.InnerException);
                Assert.IsTrue(File.Exists(original));
                Assert.AreEqual("original-must-remain", File.ReadAllText(original));
                Assert.IsTrue(File.Exists(preparedReplacement));
                Assert.AreEqual("replacement-must-remain", File.ReadAllText(preparedReplacement));
                Assert.IsFalse(File.Exists(displaced));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void CleanupRejectsRegularDirectoryMoveAndReplacementRaceWithoutDeletingEitherSentinel()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-cleanup-directory-replacement-" + Guid.NewGuid().ToString("N"));
            string output = Path.Combine(temporary, "output");
            string original = Path.Combine(output, "child");
            string originalSentinel = Path.Combine(original, "original.txt");
            string displaced = Path.Combine(temporary, "displaced");
            string preparedReplacement = Path.Combine(temporary, "replacement");
            string replacementSentinel = Path.Combine(preparedReplacement, "replacement.txt");
            try
            {
                Directory.CreateDirectory(original);
                Directory.CreateDirectory(preparedReplacement);
                File.WriteAllText(originalSentinel, "original-must-remain");
                File.WriteAllText(replacementSentinel, "replacement-must-remain");
                var hook = new Action<string, string>((stage, _) =>
                {
                    if (stage != "after-scan") return;
                    Directory.Move(original, displaced);
                    Directory.Move(preparedReplacement, original);
                });

                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => Static(
                    "DeleteTreeWithoutFollowingReparsePoints",
                    output,
                    32,
                    32,
                    new Func<string, FileAttributes>(File.GetAttributes),
                    hook));

                Assert.IsInstanceOf<IOException>(thrown.InnerException);
                Assert.IsTrue(File.Exists(originalSentinel));
                Assert.AreEqual("original-must-remain", File.ReadAllText(originalSentinel));
                Assert.IsTrue(File.Exists(replacementSentinel));
                Assert.AreEqual("replacement-must-remain", File.ReadAllText(replacementSentinel));
                Assert.IsFalse(Directory.Exists(displaced));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void CleanupFailsClosedWhenNewDescendantAppearsAfterScanWithoutRecursiveFallback()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-cleanup-new-descendant-" + Guid.NewGuid().ToString("N"));
            string lateSentinel = Path.Combine(temporary, "late.txt");
            try
            {
                Directory.CreateDirectory(temporary);
                var hook = new Action<string, string>((stage, _) =>
                {
                    if (stage == "after-scan") File.WriteAllText(lateSentinel, "must-remain");
                });

                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => Static(
                    "DeleteTreeWithoutFollowingReparsePoints",
                    temporary,
                    32,
                    32,
                    new Func<string, FileAttributes>(File.GetAttributes),
                    hook));

                Assert.IsInstanceOf<IOException>(thrown.InnerException);
                Assert.IsTrue(Directory.Exists(temporary));
                Assert.IsTrue(File.Exists(lateSentinel));
                Assert.AreEqual("must-remain", File.ReadAllText(lateSentinel));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void CleanupRejectsDuplicateHardLinkIdentityWithoutDeletingEitherName()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-cleanup-hard-link-" + Guid.NewGuid().ToString("N"));
            string first = Path.Combine(temporary, "first.bin");
            string second = Path.Combine(temporary, "second.bin");
            try
            {
                Directory.CreateDirectory(temporary);
                File.WriteAllText(first, "must-remain");
                Assert.IsTrue(CreateHardLink(second, first, IntPtr.Zero),
                    "CreateHardLinkW failed with Win32 " + Marshal.GetLastWin32Error() + ".");

                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => Static(
                    "DeleteTreeWithoutFollowingReparsePoints",
                    temporary,
                    32,
                    32,
                    new Func<string, FileAttributes>(File.GetAttributes),
                    null));

                Assert.IsInstanceOf<IOException>(thrown.InnerException);
                Assert.IsTrue(File.Exists(first));
                Assert.IsTrue(File.Exists(second));
                Assert.AreEqual("must-remain", File.ReadAllText(first));
                Assert.AreEqual("must-remain", File.ReadAllText(second));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void CleanupDeletesExactRegularTreeOnlyThroughRetainedHandles()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-cleanup-handle-success-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(temporary, "one", "two"));
                File.WriteAllText(Path.Combine(temporary, "root.bin"), "root");
                File.WriteAllText(Path.Combine(temporary, "one", "child.bin"), "child");
                File.WriteAllText(Path.Combine(temporary, "one", "two", "leaf.bin"), "leaf");

                Static(
                    "DeleteTreeWithoutFollowingReparsePoints",
                    temporary,
                    32,
                    32,
                    new Func<string, FileAttributes>(File.GetAttributes),
                    null);

                Assert.IsFalse(Directory.Exists(temporary));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void SummaryWriteRejectsParentIdentityReplacementAfterCreation()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-summary-parent-race-" + Guid.NewGuid().ToString("N"));
            string summary = Path.Combine(temporary, "Logs", "AndroidUnityLibraryExportSummary.json");
            string displaced = Path.Combine(temporary, "Logs-displaced");
            try
            {
                var hook = new Action<string, string>((stage, _) =>
                {
                    if (stage != "after-parent-attest") return;
                    Directory.Move(Path.Combine(temporary, "Logs"), displaced);
                    Directory.CreateDirectory(Path.Combine(temporary, "Logs"));
                });

                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => Static(
                    "WriteAllTextGuarded",
                    temporary,
                    summary,
                    "{}",
                    new Func<string, bool>(_ => true),
                    new Func<string, bool>(_ => false),
                    new Func<string, bool>(_ => false),
                    hook));

                Assert.IsInstanceOf<IOException>(thrown.InnerException);
                Assert.IsFalse(File.Exists(summary));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [TestCase("before-temporary-create")]
        [TestCase("before-commit")]
        public void SummaryWriteRevalidatesPolicyAtBothMutationCheckpoints(string raceStage)
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-summary-guard-race-" + Guid.NewGuid().ToString("N"));
            string summary = Path.Combine(temporary, "Logs", "AndroidUnityLibraryExportSummary.json");
            try
            {
                bool raced = false;
                var hook = new Action<string, string>((stage, _) =>
                {
                    if (stage == raceStage) raced = true;
                });

                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => Static(
                    "WriteAllTextGuarded",
                    temporary,
                    summary,
                    "{}",
                    new Func<string, bool>(_ => true),
                    new Func<string, bool>(_ => false),
                    new Func<string, bool>(path => raced && PathsEqual(path, summary)),
                    hook));

                Assert.IsInstanceOf<IOException>(thrown.InnerException);
                Assert.IsFalse(File.Exists(summary));
                if (Directory.Exists(Path.GetDirectoryName(summary)))
                {
                    Assert.IsEmpty(Directory.GetFiles(
                        Path.GetDirectoryName(summary),
                        "AndroidUnityLibraryExportSummary.json.tmp-*"));
                }
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [TestCase("version")]
        [TestCase("compiling")]
        [TestCase("compile-errors")]
        [TestCase("android-module")]
        [TestCase("active-target")]
        [TestCase("settings")]
        [TestCase("output-ignore")]
        [TestCase("output-tracked")]
        [TestCase("output-reparse")]
        [TestCase("summary-ignore")]
        [TestCase("summary-tracked")]
        [TestCase("summary-reparse")]
        public void EveryPreflightFailureIsNonMutating(string failedGate)
        {
            EnvironmentState state = ValidEnvironment();
            switch (failedGate)
            {
                case "version": state.UnityVersion = "2022.3.61f1"; break;
                case "compiling": state.IsCompiling = true; break;
                case "compile-errors": state.HasCompilationErrors = true; break;
                case "android-module": state.IsAndroidBuildSupported = false; break;
                case "active-target": state.IsAndroidActiveBuildTarget = false; break;
                case "settings": state.Validation = NewValidation(false, "scene drift"); break;
                case "output-ignore": state.OutputIgnored = false; break;
                case "output-tracked": state.OutputTracked = true; break;
                case "output-reparse": state.OutputHasReparsePoint = true; break;
                case "summary-ignore": state.SummaryIgnored = false; break;
                case "summary-tracked": state.SummaryTracked = true; break;
                case "summary-reparse": state.SummaryHasReparsePoint = true; break;
            }

            if (failedGate == "summary-ignore" ||
                failedGate == "summary-tracked" ||
                failedGate == "summary-reparse")
            {
                LogAssert.Expect(
                    LogType.Error,
                    new Regex(
                        @"^\[AL-ANDROID-UNITY-EXPORT-SUMMARY-NOT-WRITTEN\] PreflightFailed;",
                        RegexOptions.CultureInvariant));
            }
            object summary = Execute(state);

            Assert.AreEqual("PreflightFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual(0, state.DeleteCallCount);
            Assert.AreEqual(0, state.CaptureCallCount);
            Assert.AreEqual(0, state.ApplyCallCount);
            Assert.AreEqual(0, state.BuildCallCount);
            Assert.AreEqual(0, state.RestoreCallCount);
        }

        [Test]
        public void HostWithoutStrongNamespaceAttestationFailsBeforeAnyMutationOrSummaryWrite()
        {
            EnvironmentState state = ValidEnvironment();
            state.IsStrongPathAttestationSupported = false;

            object summary = Execute(state);

            Assert.AreEqual("PreflightFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual(0, state.CaptureCallCount);
            Assert.AreEqual(0, state.DeleteCallCount);
            Assert.AreEqual(0, state.BuildCallCount);
            Assert.IsNull(state.WrittenPath);
            StringAssert.Contains("Windows-only", PropString(summary, "SummaryMessage"));
        }

        [TestCase(RuntimePlatform.WindowsEditor, true)]
        [TestCase(RuntimePlatform.LinuxEditor, false)]
        [TestCase(RuntimePlatform.OSXEditor, false)]
        public void StrongNamespaceAttestationSupportIsExplicitPerEditorHost(
            RuntimePlatform host,
            bool expected)
        {
            Assert.AreEqual(expected, (bool)Static("SupportsStrongPathAttestation", host));
        }

        [Test]
        public void SuccessfulExecutionCleansOnlyGuardedOutputAndRestoresSettings()
        {
            EnvironmentState state = ValidEnvironment();
            state.OutputDirectoryExists = true;

            object summary = Execute(state);

            Assert.AreEqual("Succeeded", Prop(summary, "Status").ToString(), PropString(summary, "SummaryMessage"));
            Assert.AreEqual(1, state.DeleteCallCount);
            CollectionAssert.AreEqual(new[] { ExpectedOutput(state.ProjectRoot) }, state.DeletedPaths);
            Assert.AreEqual(2, state.CaptureCallCount);
            Assert.AreEqual(1, state.ApplyCallCount);
            Assert.AreEqual(1, state.BuildCallCount);
            Assert.AreEqual(1, state.InspectCallCount);
            Assert.AreEqual(1, state.RestoreCallCount);
            Assert.Less(state.ApplySequence, state.BuildSequence);
            Assert.Less(state.BuildSequence, state.RestoreSequence);
            Assert.AreSame(state.Settings, state.RestoredSettings);
            Assert.AreEqual(ExpectedSummary(state.ProjectRoot), state.WrittenPath);
            Assert.That(state.WrittenContents, Does.Contain("\"status\": \"Succeeded\""));
        }

        [TestCase("apply")]
        [TestCase("build")]
        [TestCase("inspect")]
        public void AnyPostSnapshotFailureStillRestoresOriginalSettings(string failedStage)
        {
            EnvironmentState state = ValidEnvironment();
            state.ThrowOnApply = failedStage == "apply";
            state.ThrowOnBuild = failedStage == "build";
            state.ThrowOnInspect = failedStage == "inspect";

            object summary = Execute(state);

            Assert.AreNotEqual("Succeeded", Prop(summary, "Status").ToString());
            Assert.AreEqual(2, state.CaptureCallCount);
            Assert.AreEqual(1, state.RestoreCallCount, "Settings restoration must run exactly once in finally.");
            Assert.AreSame(state.Settings, state.RestoredSettings);
            Assert.Greater(state.RestoreSequence, state.ApplySequence);
        }

        [Test]
        public void RestoreFailureOverridesOtherwiseSuccessfulExport()
        {
            EnvironmentState state = ValidEnvironment();
            state.ThrowOnRestore = true;

            object summary = Execute(state);

            Assert.AreEqual("SettingsRestoreFailed", Prop(summary, "Status").ToString());
            Assert.IsFalse(PropBool(summary, "Succeeded"));
            Assert.AreEqual(1, state.BuildCallCount);
            Assert.AreEqual(1, state.RestoreCallCount);
            StringAssert.Contains("restoration", PropString(summary, "SummaryMessage").ToLowerInvariant());
        }

        [TestCase("backend")]
        [TestCase("architectures")]
        [TestCase("minimum-api")]
        [TestCase("export-project")]
        [TestCase("app-bundle")]
        [TestCase("il2cpp-compiler-configuration")]
        [TestCase("managed-stripping-level")]
        public void RestoreMustRecaptureAndMatchEveryOriginalSetting(string drift)
        {
            EnvironmentState state = ValidEnvironment();
            state.PostRestoreSettings = NewSettings(
                drift == "backend" ? "IL2CPP" : "Mono2x",
                drift == "architectures" ? "ARM64" : "ARMv7",
                drift == "minimum-api" ? 24 : 22,
                drift == "export-project" || false,
                drift != "app-bundle",
                drift == "il2cpp-compiler-configuration" ? "Release" : "Master",
                drift == "managed-stripping-level" ? "Medium" : "Minimal");

            object summary = Execute(state);

            Assert.AreEqual("SettingsRestoreFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual(2, state.CaptureCallCount);
            Assert.AreEqual(1, state.RestoreCallCount);
            StringAssert.Contains(drift, PropString(summary, "SummaryMessage").ToLowerInvariant());
        }

        [Test]
        public void RestoreRecaptureExceptionFailsClosed()
        {
            EnvironmentState state = ValidEnvironment();
            state.ThrowOnPostRestoreCapture = true;

            object summary = Execute(state);

            Assert.AreEqual("SettingsRestoreFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual(2, state.CaptureCallCount);
            StringAssert.Contains("recapture", PropString(summary, "SummaryMessage").ToLowerInvariant());
        }

        [TestCase(2, 0)]
        [TestCase(3, 0)]
        public void OutputPathRaceAfterCreationOrImmediatelyBeforeBuildFailsClosed(
            int reparseCheckThatDrifts,
            int expectedBuildCalls)
        {
            EnvironmentState state = ValidEnvironment();
            state.OutputReparseOnCheck = reparseCheckThatDrifts;

            object summary = Execute(state);

            Assert.AreNotEqual("Succeeded", Prop(summary, "Status").ToString());
            Assert.AreEqual(expectedBuildCalls, state.BuildCallCount);
            Assert.GreaterOrEqual(state.OutputReparseCheckCount, reparseCheckThatDrifts);
        }

        [Test]
        public void OutputDirectoryIdentityChangeBeforeMutationLeaseFailsClosed()
        {
            EnvironmentState state = ValidEnvironment();
            state.OutputIdentityBeforeBuild = "replacement-directory";

            object summary = Execute(state);

            Assert.AreNotEqual("Succeeded", Prop(summary, "Status").ToString());
            Assert.AreEqual(0, state.BuildCallCount);
            Assert.AreEqual(0, state.InspectCallCount);
        }

        [TestCase("report-result")]
        [TestCase("report-target")]
        [TestCase("report-output")]
        [TestCase("report-errors")]
        [TestCase("not-inspected")]
        [TestCase("missing")]
        [TestCase("extra-abi")]
        [TestCase("empty-hash")]
        [TestCase("invalid-artifact")]
        public void BuildAndArtifactDriftFailsClosed(string failedGate)
        {
            EnvironmentState state = ValidEnvironment();
            switch (failedGate)
            {
                case "report-result": state.Report = NewReport("Failed", "Android", ExpectedOutput(state.ProjectRoot), 1); break;
                case "report-target": state.Report = NewReport("Succeeded", "StandaloneWindows64", ExpectedOutput(state.ProjectRoot), 0); break;
                case "report-output": state.Report = NewReport("Succeeded", "Android", Path.Combine(state.ProjectRoot, "wrong"), 0); break;
                case "report-errors": state.Report = NewReport("Succeeded", "Android", ExpectedOutput(state.ProjectRoot), 1); break;
                case "not-inspected": state.Artifacts = NewArtifacts(false, Array.Empty<string>(), new[] { "arm64-v8a" }, Hash64()); break;
                case "missing": state.Artifacts = NewArtifacts(true, new[] { "unityLibrary/libs/unity-classes.jar" }, new[] { "arm64-v8a" }, Hash64()); break;
                case "extra-abi": state.Artifacts = NewArtifacts(true, Array.Empty<string>(), new[] { "arm64-v8a", "armeabi-v7a" }, Hash64()); break;
                case "empty-hash": state.Artifacts = NewArtifacts(true, Array.Empty<string>(), new[] { "arm64-v8a" }, string.Empty); break;
                case "invalid-artifact": state.Artifacts = NewArtifacts(
                    true,
                    Array.Empty<string>(),
                    new[] { "arm64-v8a" },
                    Hash64(),
                    new[] { "unityLibrary/libs/unity-classes.jar (empty)" }); break;
            }

            object summary = Execute(state);

            Assert.AreNotEqual("Succeeded", Prop(summary, "Status").ToString());
            Assert.AreEqual(1, state.RestoreCallCount);
        }

        [Test]
        public void TraversalAppliesBoundsBeforeSortingAndRejectsReparseAttributes()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "al-android-export-bound-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                object bounded = Static("InspectExportTree", temporary, 2, 32, 1024L * 1024L);

                Assert.IsFalse(PropBool(bounded, "Inspected"));
                StringAssert.Contains("file inspection bound", PropString(bounded, "Summary"));
                Assert.IsTrue((bool)Static("IsSafeArtifactAttributes", FileAttributes.Directory));
                Assert.IsFalse((bool)Static(
                    "IsSafeArtifactAttributes",
                    FileAttributes.Directory | FileAttributes.ReparsePoint));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void RequiredArtifactsMustBeNonemptyAndHaveExpectedBinarySignatures()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "al-android-export-shape-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                WriteFixtureBytes(temporary, "unityLibrary/libs/unity-classes.jar", Array.Empty<byte>());
                object emptyJar = Static("InspectExportTree", temporary);
                Assert.IsFalse(PropBool(emptyJar, "IsValid"));
                Assert.That(
                    string.Join(" ", AsStrings(Prop(emptyJar, "InvalidArtifacts"))),
                    Does.Contain("unity-classes.jar (empty)"));

                WriteFixtureBytes(
                    temporary,
                    "unityLibrary/libs/unity-classes.jar",
                    new byte[] { 0x50, 0x4b, 0x03, 0x04, 1 });
                WriteFixture(temporary, "unityLibrary/src/main/jniLibs/arm64-v8a/libunity.so", "not-elf");
                object corruptSo = Static("InspectExportTree", temporary);
                Assert.IsFalse(PropBool(corruptSo, "IsValid"));
                Assert.That(
                    string.Join(" ", AsStrings(Prop(corruptSo, "InvalidArtifacts"))),
                    Does.Contain("libunity.so (missing ELF signature)"));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void ExportStageRequiresBoundedIl2CppSourceAndToolchainWithoutPrebuiltLibrary()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-export-il2cpp-stage-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                string packagedLibrary = Path.Combine(
                    temporary,
                    "unityLibrary",
                    "src",
                    "main",
                    "jniLibs",
                    "arm64-v8a",
                    "libil2cpp.so");

                Assert.IsFalse(
                    File.Exists(packagedLibrary),
                    "The exported Gradle stage must not require a native library compiled later by Gradle.");
                object staged = Static("InspectExportTree", temporary);
                Assert.IsTrue(PropBool(staged, "IsValid"), PropString(staged, "Summary"));

                string generatedRegistration = Path.Combine(
                    temporary,
                    "unityLibrary",
                    "src",
                    "main",
                    "Il2CppOutputProject",
                    "Source",
                    "il2cppOutput",
                    "Il2CppCodeRegistration.cpp");
                File.Delete(generatedRegistration);
                object missingSource = Static("InspectExportTree", temporary);
                Assert.IsFalse(PropBool(missingSource, "IsValid"));
                Assert.That(
                    string.Join(" ", AsStrings(Prop(missingSource, "MissingArtifacts"))),
                    Does.Contain("Il2CppCodeRegistration.cpp"));

                WriteFixture(
                    temporary,
                    "unityLibrary/src/main/Il2CppOutputProject/Source/il2cppOutput/Il2CppCodeRegistration.cpp",
                    "// generated registration");
                string toolchain = Path.Combine(
                    temporary,
                    "unityLibrary",
                    "src",
                    "main",
                    "Il2CppOutputProject",
                    "IL2CPP",
                    "build",
                    "deploy",
                    "il2cpp.exe");
                File.Delete(toolchain);
                object missingToolchain = Static("InspectExportTree", temporary);
                Assert.IsFalse(PropBool(missingToolchain, "IsValid"));
                Assert.That(
                    string.Join(" ", AsStrings(Prop(missingToolchain, "MissingArtifacts"))),
                    Does.Contain("IL2CPP/build/deploy/il2cpp.exe"));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void ArtifactReadUsesRemainingBudgetPlusOneToProveOverflow()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-export-byte-bound-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                object bounded = Static("InspectExportTree", temporary, 8192, 8192, 1L);

                Assert.IsFalse(PropBool(bounded, "Inspected"));
                StringAssert.Contains("byte inspection bound", PropString(bounded, "Summary"));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [TestCase(RuntimePlatform.WindowsEditor, "il2cpp.exe", true, 0x4d, 0x5a, true)]
        [TestCase(RuntimePlatform.WindowsEditor, "il2cpp", true, 0x7f, 0x45, false)]
        [TestCase(RuntimePlatform.WindowsEditor, "il2cpp.exe", true, 0x00, 0x00, false)]
        [TestCase(RuntimePlatform.LinuxEditor, "il2cpp", true, 0x7f, 0x45, true)]
        [TestCase(RuntimePlatform.LinuxEditor, "il2cpp.exe", true, 0x4d, 0x5a, false)]
        [TestCase(RuntimePlatform.LinuxEditor, "il2cpp", false, 0x7f, 0x45, false)]
        [TestCase(RuntimePlatform.OSXEditor, "il2cpp", true, 0xcf, 0xfa, true)]
        [TestCase(RuntimePlatform.OSXEditor, "il2cpp.exe", true, 0x4d, 0x5a, false)]
        public void Il2CppToolMustMatchCurrentHostNameExecutionAndBinaryFormat(
            RuntimePlatform host,
            string toolName,
            bool executable,
            byte first,
            byte second,
            bool expectedValid)
        {
            string relative =
                "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/" + toolName;
            byte[] prefix = expectedValid && host == RuntimePlatform.WindowsEditor
                ? ValidPeHeader()
                : expectedValid && host == RuntimePlatform.LinuxEditor
                    ? new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }
                    : expectedValid && host == RuntimePlatform.OSXEditor
                        ? new byte[] { 0xcf, 0xfa, 0xed, 0xfe }
                        : new byte[] { first, second, 0x01, 0x02 };

            string failure = (string)Static(
                "ValidateIl2CppToolForHost",
                relative,
                prefix,
                executable,
                host);

            Assert.AreEqual(expectedValid, failure.Length == 0, failure);
        }

        [Test]
        public void WindowsIl2CppToolRejectsTruncatedAndSpoofedPortableExecutables()
        {
            const string relative =
                "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp.exe";
            byte[] invalidOffset = new byte[128];
            invalidOffset[0] = (byte)'M';
            invalidOffset[1] = (byte)'Z';
            invalidOffset[0x3c] = 0xff;
            invalidOffset[0x3d] = 0xff;

            Assert.That(
                (string)Static(
                    "ValidateIl2CppToolForHost",
                    relative,
                    new byte[] { (byte)'M', (byte)'Z' },
                    true,
                    RuntimePlatform.WindowsEditor),
                Is.Not.Empty);
            Assert.That(
                (string)Static(
                    "ValidateIl2CppToolForHost",
                    relative,
                    invalidOffset,
                    true,
                    RuntimePlatform.WindowsEditor),
                Is.Not.Empty);
            Assert.That(
                (string)Static(
                    "ValidateIl2CppToolForHost",
                    relative,
                    new byte[128],
                    true,
                    RuntimePlatform.WindowsEditor),
                Is.Not.Empty);
        }

        [Test]
        public void ExportRejectsAmbiguousWrongHostIl2CppToolAlongsideExpectedTool()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-export-il2cpp-ambiguous-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                WriteFixtureBytes(
                    temporary,
                    "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp",
                    new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' });

                object artifacts = Static("InspectExportTree", temporary);

                Assert.IsFalse(PropBool(artifacts, "IsValid"));
                Assert.That(
                    string.Join(" ", AsStrings(Prop(artifacts, "InvalidArtifacts"))),
                    Does.Contain("wrong tool name"));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void InspectionKeepsEachArtifactSingleOpenAndRejectsConcurrentMutation()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-export-race-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                string target = Path.Combine(temporary, "settings.gradle");
                var hook = new Action<string, string>((stage, relative) =>
                {
                    if (stage == "after-open" && relative == "settings.gradle")
                    {
                        File.WriteAllText(target, "include ':attacker'");
                    }
                });

                object raced = Static(
                    "InspectExportTree",
                    temporary,
                    8192,
                    8192,
                    2L * 1024L * 1024L * 1024L,
                    hook);

                Assert.IsFalse(PropBool(raced, "Inspected"));
                StringAssert.Contains("inspection failed", PropString(raced, "Summary").ToLowerInvariant());
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void ExportStageRequiresGradleToDeclareDeferredIl2CppNativeCompilation()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "al-android-export-il2cpp-gradle-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                WriteFixture(
                    temporary,
                    "unityLibrary/build.gradle",
                    "apply plugin: 'com.android.library'\ndefaultConfig { minSdkVersion 24 }");

                object missingGeneration = Static("InspectExportTree", temporary);

                Assert.IsFalse(PropBool(missingGeneration, "IsValid"));
                Assert.That(
                    string.Join(" ", AsStrings(Prop(missingGeneration, "InvalidArtifacts"))),
                    Does.Contain("staged IL2CPP Gradle generation is missing"));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void RealTreeInspectionRequiresUnityLibraryIl2CppArm64ShapeAndIsDeterministic()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "al-android-export-fixture-" + Guid.NewGuid().ToString("N"));
            try
            {
                CreateValidTree(temporary);
                object first = Static("InspectExportTree", temporary);
                object second = Static("InspectExportTree", temporary);

                Assert.IsTrue(PropBool(first, "IsValid"), PropString(first, "Summary"));
                Assert.IsTrue(PropBool(first, "Inspected"));
                Assert.AreEqual(PropString(first, "InventorySha256"), PropString(second, "InventorySha256"));
                Assert.AreEqual(64, PropString(first, "InventorySha256").Length);
                CollectionAssert.AreEqual(new[] { "arm64-v8a" }, AsStrings(Prop(first, "AbiDirectories")));
                Assert.IsEmpty(AsStrings(Prop(first, "MissingArtifacts")));

                WriteFixture(temporary, "unityLibrary/src/main/jniLibs/armeabi-v7a/libunity.so", "wrong-abi");
                object drifted = Static("InspectExportTree", temporary);
                Assert.IsFalse(PropBool(drifted, "IsValid"));
                CollectionAssert.AreEqual(
                    new[] { "arm64-v8a", "armeabi-v7a" },
                    AsStrings(Prop(drifted, "AbiDirectories")));
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            }
        }

        [Test]
        public void SummaryJsonHasFixedOrderAndInvariantValues()
        {
            EnvironmentState state = ValidEnvironment();
            object summary = Execute(state);

            CultureInfo previous = CultureInfo.CurrentCulture;
            string first;
            string second;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                first = (string)Static("SerializeSummary", summary);
                CultureInfo.CurrentCulture = new CultureInfo("ko-KR");
                second = (string)Static("SerializeSummary", summary);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.AreEqual(first, second);
            string[] fields =
            {
                "\"status\"", "\"target\"", "\"unityVersion\"", "\"scriptingBackend\"",
                "\"targetArchitectures\"", "\"minimumApiLevel\"", "\"outputDirectory\"",
                "\"scenePaths\"", "\"startedAtUtc\"", "\"endedAtUtc\"", "\"totalTime\"",
                "\"totalSize\"", "\"warningCount\"", "\"errorCount\"", "\"buildResult\"",
                "\"artifactFileCount\"", "\"artifactBytes\"", "\"inventorySha256\"",
                "\"abiDirectories\"", "\"summaryMessage\""
            };
            int prior = -1;
            foreach (string field in fields)
            {
                int current = first.IndexOf(field, StringComparison.Ordinal);
                Assert.Greater(current, prior, field);
                prior = current;
            }
            Assert.That(first, Does.Contain("\"minimumApiLevel\": 24"));
            Assert.That(first, Does.Contain("\"inventorySha256\": \"" + Hash64() + "\""));
        }

        private static EnvironmentState ValidEnvironment()
        {
            string root = ProjectRoot();
            var state = new EnvironmentState
            {
                ProjectRoot = root,
                UnityVersion = "2022.3.62f3",
                IsAndroidBuildSupported = true,
                IsAndroidActiveBuildTarget = true,
                IsStrongPathAttestationSupported = true,
                Validation = NewValidation(true, "valid"),
                OutputIgnored = true,
                SummaryIgnored = true,
                Settings = NewSettings("Mono2x", "ARMv7", 22, false, true),
                UtcBase = new DateTime(2026, 8, 6, 1, 2, 3, DateTimeKind.Utc)
            };
            state.Report = NewReport("Succeeded", "Android", ExpectedOutput(root), 0);
            state.Artifacts = NewArtifacts(true, Array.Empty<string>(), new[] { "arm64-v8a" }, Hash64());
            return state;
        }

        private static object Execute(EnvironmentState state)
        {
            object proxy = CreateDispatchProxy(EnvironmentType, typeof(ScriptedEnvironmentProxy));
            ((ScriptedEnvironmentProxy)proxy).State = state;
            return Static("Execute", proxy);
        }

        private static object CreatePlan(string root, string output, string summary) =>
            Static(
                "CreatePlan",
                root,
                output,
                summary,
                "2022.3.62f3",
                false,
                false,
                true,
                true,
                NewValidation(true, "valid"),
                true,
                false,
                false,
                true,
                false,
                false);

        private static object NewValidation(bool valid, string summary) => Create(
            ValidationType,
            valid,
            summary);

        private static object NewSettings(
            string backend,
            string architectures,
            int minimumApi,
            bool exportProject,
            bool buildAppBundle) => NewSettings(
                backend,
                architectures,
                minimumApi,
                exportProject,
                buildAppBundle,
                "Master",
                "Minimal");

        private static object NewSettings(
            string backend,
            string architectures,
            int minimumApi,
            bool exportProject,
            bool buildAppBundle,
            string il2CppCompilerConfiguration,
            string managedStrippingLevel) => Create(
                SettingsType,
                backend,
                architectures,
                minimumApi,
                exportProject,
                buildAppBundle,
                il2CppCompilerConfiguration,
                managedStrippingLevel);

        private static object NewReport(string result, string target, string output, int errors) => Create(
            ReportType,
            result,
            target,
            output,
            TimeSpan.FromSeconds(2.5),
            123456UL,
            2,
            errors,
            "scripted report");

        private static object NewArtifacts(
            bool inspected,
            IEnumerable<string> missing,
            IEnumerable<string> abiDirectories,
            string hash,
            IEnumerable<string> invalid = null) => Create(
                ArtifactsType,
                inspected,
                12,
                654321L,
                hash,
                missing.ToArray(),
                (invalid ?? Array.Empty<string>()).ToArray(),
                abiDirectories.ToArray(),
                inspected ? "scripted inspection" : "inspection unavailable");

        private static object Create(Type type, params object[] args) => Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: args,
            culture: CultureInfo.InvariantCulture);

        private static object Static(string method, params object[] args)
        {
            MethodInfo candidate = ExporterType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(item => item.Name == method && item.GetParameters().Length == (args?.Length ?? 0));
            return candidate.Invoke(null, args);
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo candidate = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(item => item.Name == method && item.GetParameters().Length == (args?.Length ?? 0));
            return candidate.Invoke(target, args);
        }

        private static object CreateDispatchProxy(Type interfaceType, Type proxyType)
        {
            MethodInfo create = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Create" && method.GetGenericArguments().Length == 2);
            return create.MakeGenericMethod(interfaceType, proxyType).Invoke(null, null);
        }

        private static Type Runtime(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, throwOnError: false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, "Missing runtime type: " + fullName);
            return type;
        }

        private static object Prop(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, target.GetType().FullName + "." + name);
            return property.GetValue(target);
        }

        private static bool PropBool(object target, string name) => Convert.ToBoolean(Prop(target, name));
        private static int PropInt(object target, string name) => Convert.ToInt32(Prop(target, name));
        private static string PropString(object target, string name) => Prop(target, name)?.ToString() ?? string.Empty;
        private static string[] AsStrings(object value) =>
            ((IEnumerable)value).Cast<object>().Select(item => item?.ToString() ?? string.Empty).ToArray();
        private static string Failures(object plan) => string.Join(" ", AsStrings(Prop(plan, "Failures")));

        private static string ProjectRoot() => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static string ExpectedOutput(string root) => Path.GetFullPath(Path.Combine(root, "Builds", "AndroidExport"));
        private static string ExpectedSummary(string root) =>
            Path.GetFullPath(Path.Combine(root, "Logs", "AndroidUnityLibraryExportSummary.json"));
        private static string Hash64() => new string('a', 64);
        private static bool PathsEqual(string left, string right) => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

        private static byte[] ValidPeHeader()
        {
            var bytes = new byte[128];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            bytes[0x3c] = 0x40;
            bytes[0x40] = (byte)'P';
            bytes[0x41] = (byte)'E';
            return bytes;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(
            string fileName,
            string existingFileName,
            IntPtr securityAttributes);

        private static void CreateValidTree(string root)
        {
            WriteFixture(root, "settings.gradle", "include ':launcher', ':unityLibrary'");
            WriteFixture(root, "build.gradle", "// root");
            WriteFixture(root, "gradle.properties", "org.gradle.jvmargs=-Xmx4096m");
            WriteFixture(
                root,
                "unityLibrary/build.gradle",
                "apply plugin: 'com.android.library'\n" +
                "defaultConfig { minSdkVersion 24 }\n" +
                "def generated = 'src/main/Il2CppOutputProject'\n" +
                "def packaged = 'src/main/jniLibs/arm64-v8a/libil2cpp.so'");
            WriteFixture(root, "unityLibrary/src/main/AndroidManifest.xml", "<manifest />");
            WriteFixtureBytes(
                root,
                "unityLibrary/libs/unity-classes.jar",
                new byte[] { 0x50, 0x4b, 0x03, 0x04, 1 });
            WriteFixture(root, "unityLibrary/proguard-unity.txt", "-keep class com.unity3d.** { *; }");
            WriteFixture(root, "unityLibrary/src/main/assets/bin/Data/globalgamemanagers", "data");
            byte[] elf = { 0x7f, (byte)'E', (byte)'L', (byte)'F', 1 };
            WriteFixtureBytes(root, "unityLibrary/src/main/jniLibs/arm64-v8a/libmain.so", elf);
            WriteFixtureBytes(root, "unityLibrary/src/main/jniLibs/arm64-v8a/libunity.so", elf);
            WriteFixtureBytes(
                root,
                "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp.exe",
                ValidPeHeader());
            WriteFixture(
                root,
                "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/libil2cpp/il2cpp-api.cpp",
                "// il2cpp api");
            WriteFixture(
                root,
                "unityLibrary/src/main/Il2CppOutputProject/Source/il2cppOutput/Il2CppCodeRegistration.cpp",
                "// generated registration");
        }

        private static void WriteFixture(string root, string relative, string contents)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            File.WriteAllText(path, contents);
        }

        private static void WriteFixtureBytes(string root, string relative, byte[] contents)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            File.WriteAllBytes(path, contents);
        }

        public class ScriptedEnvironmentProxy : DispatchProxy
        {
            public EnvironmentState State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod.Name)
                {
                    case "get_ProjectRoot": return State.ProjectRoot;
                    case "get_UnityVersion": return State.UnityVersion;
                    case "get_IsCompiling": return State.IsCompiling;
                    case "get_HasCompilationErrors": return State.HasCompilationErrors;
                    case "get_IsAndroidBuildSupported": return State.IsAndroidBuildSupported;
                    case "get_IsAndroidActiveBuildTarget": return State.IsAndroidActiveBuildTarget;
                    case "get_IsStrongPathAttestationSupported":
                        return State.IsStrongPathAttestationSupported;
                    case "UtcNow": return State.UtcBase.AddSeconds(State.UtcCallCount++);
                    case "ValidateCurrentShellFoundation": return State.Validation;
                    case "IsIgnoredPath":
                        return SamePath((string)args[0], ExpectedOutput(State.ProjectRoot))
                            ? State.OutputIgnored
                            : State.SummaryIgnored;
                    case "IsTrackedPath":
                        return SamePath((string)args[0], ExpectedOutput(State.ProjectRoot))
                            ? State.OutputTracked
                            : State.SummaryTracked;
                    case "HasReparsePoint":
                        if (SamePath((string)args[0], ExpectedOutput(State.ProjectRoot)))
                        {
                            State.OutputReparseCheckCount++;
                            return State.OutputHasReparsePoint ||
                                State.OutputReparseOnCheck == State.OutputReparseCheckCount;
                        }
                        State.SummaryReparseCheckCount++;
                        return State.SummaryHasReparsePoint;
                    case "DirectoryExists":
                        return SamePath((string)args[0], ExpectedOutput(State.ProjectRoot)) &&
                            State.OutputDirectoryExists;
                    case "DeleteDirectory":
                        State.Sequence++;
                        State.DeleteCallCount++;
                        State.DeletedPaths.Add(Path.GetFullPath((string)args[0]));
                        State.OutputDirectoryExists = false;
                        return null;
                    case "CreateDirectory":
                        State.OutputDirectoryExists = true;
                        return null;
                    case "AttestOutputDirectory":
                        State.OutputReparseCheckCount++;
                        if (State.OutputHasReparsePoint ||
                            State.OutputReparseOnCheck == State.OutputReparseCheckCount)
                        {
                            throw new IOException("scripted post-create reparse race");
                        }
                        return State.OutputIdentity;
                    case "AcquireOutputMutationLease":
                        State.OutputReparseCheckCount++;
                        if (State.OutputHasReparsePoint ||
                            State.OutputReparseOnCheck == State.OutputReparseCheckCount)
                        {
                            throw new IOException("scripted pre-build reparse race");
                        }
                        string actualIdentity = State.OutputIdentityBeforeBuild ?? State.OutputIdentity;
                        if (!string.Equals(actualIdentity, (string)args[1], StringComparison.Ordinal))
                        {
                            throw new IOException("scripted output identity replacement");
                        }
                        return new ScriptedLease();
                    case "CaptureBuildSettings":
                        State.CaptureCallCount++;
                        if (State.CaptureCallCount > 1 && State.ThrowOnPostRestoreCapture)
                        {
                            throw new InvalidOperationException("scripted recapture failure");
                        }
                        return State.CaptureCallCount == 1 || State.PostRestoreSettings == null
                            ? State.Settings
                            : State.PostRestoreSettings;
                    case "ApplyRequiredBuildSettings":
                        State.ApplyCallCount++;
                        State.ApplySequence = ++State.Sequence;
                        if (State.ThrowOnApply) throw new InvalidOperationException("scripted apply failure");
                        return null;
                    case "RestoreBuildSettings":
                        State.RestoreCallCount++;
                        State.RestoreSequence = ++State.Sequence;
                        State.RestoredSettings = args[0];
                        if (State.ThrowOnRestore) throw new InvalidOperationException("scripted restore failure");
                        return null;
                    case "BuildPlayer":
                        State.BuildCallCount++;
                        State.BuildSequence = ++State.Sequence;
                        State.CapturedOptions = (BuildPlayerOptions)args[0];
                        if (State.ThrowOnBuild) throw new InvalidOperationException("scripted build failure");
                        return State.Report;
                    case "InspectExport":
                        State.InspectCallCount++;
                        if (State.ThrowOnInspect) throw new InvalidOperationException("scripted inspect failure");
                        return State.Artifacts;
                    case "WriteAllText":
                        State.WrittenPath = Path.GetFullPath((string)args[0]);
                        State.WrittenContents = (string)args[1];
                        return null;
                    default:
                        throw new MissingMethodException(targetMethod.Name);
                }
            }

            private static bool SamePath(string left, string right) => string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        public sealed class EnvironmentState
        {
            public string ProjectRoot;
            public string UnityVersion;
            public bool IsCompiling;
            public bool HasCompilationErrors;
            public bool IsAndroidBuildSupported;
            public bool IsAndroidActiveBuildTarget;
            public bool IsStrongPathAttestationSupported;
            public object Validation;
            public bool OutputIgnored;
            public bool OutputTracked;
            public bool OutputHasReparsePoint;
            public bool SummaryIgnored;
            public bool SummaryTracked;
            public bool SummaryHasReparsePoint;
            public bool OutputDirectoryExists;
            public object Settings;
            public object PostRestoreSettings;
            public string OutputIdentity = "scripted-output-identity";
            public string OutputIdentityBeforeBuild;
            public object Report;
            public object Artifacts;
            public bool ThrowOnApply;
            public bool ThrowOnBuild;
            public bool ThrowOnInspect;
            public bool ThrowOnRestore;
            public bool ThrowOnPostRestoreCapture;
            public int CaptureCallCount;
            public int ApplyCallCount;
            public int BuildCallCount;
            public int InspectCallCount;
            public int RestoreCallCount;
            public int DeleteCallCount;
            public int Sequence;
            public int ApplySequence;
            public int BuildSequence;
            public int RestoreSequence;
            public int OutputReparseCheckCount;
            public int SummaryReparseCheckCount;
            public int OutputReparseOnCheck;
            public int UtcCallCount;
            public object RestoredSettings;
            public BuildPlayerOptions? CapturedOptions;
            public readonly List<string> DeletedPaths = new List<string>();
            public string WrittenPath;
            public string WrittenContents;
            public DateTime UtcBase;
        }

        private sealed class ScriptedLease : IDisposable
        {
            public void Dispose() { }
        }
    }
}
