using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
                "Assets/AL/Scenes/Kingdom.unity"
            }, options.scenes);
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
        public void SuccessfulExecutionCleansOnlyGuardedOutputAndRestoresSettings()
        {
            EnvironmentState state = ValidEnvironment();
            state.OutputDirectoryExists = true;

            object summary = Execute(state);

            Assert.AreEqual("Succeeded", Prop(summary, "Status").ToString(), PropString(summary, "SummaryMessage"));
            Assert.AreEqual(1, state.DeleteCallCount);
            CollectionAssert.AreEqual(new[] { ExpectedOutput(state.ProjectRoot) }, state.DeletedPaths);
            Assert.AreEqual(1, state.CaptureCallCount);
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
            Assert.AreEqual(1, state.CaptureCallCount);
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
            bool buildAppBundle) => Create(
                SettingsType,
                backend,
                architectures,
                minimumApi,
                exportProject,
                buildAppBundle);

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

        private static void CreateValidTree(string root)
        {
            WriteFixture(root, "settings.gradle", "include ':launcher', ':unityLibrary'");
            WriteFixture(root, "build.gradle", "// root");
            WriteFixture(root, "gradle.properties", "org.gradle.jvmargs=-Xmx4096m");
            WriteFixture(
                root,
                "unityLibrary/build.gradle",
                "apply plugin: 'com.android.library'\ndefaultConfig { minSdkVersion 24 }");
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
            WriteFixtureBytes(root, "unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so", elf);
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
                        return SamePath((string)args[0], ExpectedOutput(State.ProjectRoot))
                            ? State.OutputHasReparsePoint
                            : State.SummaryHasReparsePoint;
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
                    case "CaptureBuildSettings":
                        State.CaptureCallCount++;
                        return State.Settings;
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
            public object Validation;
            public bool OutputIgnored;
            public bool OutputTracked;
            public bool OutputHasReparsePoint;
            public bool SummaryIgnored;
            public bool SummaryTracked;
            public bool SummaryHasReparsePoint;
            public bool OutputDirectoryExists;
            public object Settings;
            public object Report;
            public object Artifacts;
            public bool ThrowOnApply;
            public bool ThrowOnBuild;
            public bool ThrowOnInspect;
            public bool ThrowOnRestore;
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
            public int UtcCallCount;
            public object RestoredSettings;
            public BuildPlayerOptions? CapturedOptions;
            public readonly List<string> DeletedPaths = new List<string>();
            public string WrittenPath;
            public string WrittenContents;
            public DateTime UtcBase;
        }
    }
}
