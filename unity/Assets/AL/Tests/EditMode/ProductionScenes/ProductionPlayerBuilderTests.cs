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
    /// Reflection-based #150 Player-builder tests. Editor tooling lives in Assembly-CSharp-Editor, so
    /// these tests exercise its typed pure seams without adding a production assembly dependency.
    /// </summary>
    public sealed class ProductionPlayerBuilderTests
    {
        private static Type BuilderType => Runtime("AL.EditorTools.ProductionPlayerBuilder");
        private static Type ValidationType => Runtime("AL.EditorTools.BuildValidationSnapshot");
        private static Type ReportType => Runtime("AL.EditorTools.PlayerBuildReportSnapshot");
        private static Type EnvironmentType => Runtime("AL.EditorTools.IProductionPlayerBuildEnvironment");

        [Test]
        public void SharedGameDataBuildProcessorInjectsTheCanonicalSourceAtTheRuntimePath()
        {
            Type processor = Runtime("AL.EditorTools.SharedGameDataStreamingAssetsBuildProcessor");
            Assert.That(
                typeof(UnityEditor.Build.BuildPlayerProcessor).IsAssignableFrom(processor),
                Is.True,
                "The shared GameData registration must run for every Player build entry point.");

            MethodInfo resolveSource = processor.GetMethod(
                "ResolveSourceDirectory",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo resolveCatalogs = processor.GetMethod(
                "ResolveCatalogFiles",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo resolveRegistrations = processor.GetMethod(
                "ResolveCatalogRegistrations",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo destination = processor.GetField(
                "DestinationPath",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(resolveSource, Is.Not.Null);
            Assert.That(resolveCatalogs, Is.Not.Null);
            Assert.That(resolveRegistrations, Is.Not.Null);
            Assert.That(destination, Is.Not.Null);

            string source = (string)resolveSource.Invoke(null, new object[] { Application.dataPath });
            Assert.That(
                source,
                Is.EqualTo(Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "AL",
                    "StreamingAssets",
                    "GameData"))));
            Assert.That(Directory.Exists(source), Is.True);
            Assert.That(File.Exists(Path.Combine(source, "al_realm_catalog.json")), Is.True);
            Assert.That(destination.GetRawConstantValue(), Is.EqualTo("GameData"));

            string[] catalogs = (string[])resolveCatalogs.Invoke(null, new object[] { Application.dataPath });
            Assert.That(catalogs, Is.Not.Empty);
            Assert.That(catalogs, Does.Contain(Path.Combine(source, "al_realm_catalog.json")));
            CollectionAssert.AreEqual(
                catalogs.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                catalogs,
                "Catalog injection order must be deterministic and ordinal.");
            Assert.That(catalogs.Select(Path.GetExtension), Has.All.EqualTo(".json"));
            Assert.That(catalogs.Select(Path.GetFileName), Is.Unique);
            Assert.That(catalogs, Has.None.EndsWith(".meta"));

            Array registrations = (Array)resolveRegistrations.Invoke(
                null,
                new object[] { Application.dataPath });
            string[] registeredSources = registrations
                .Cast<object>()
                .Select(registration => PropString(registration, "Key"))
                .ToArray();
            string[] registeredDestinations = registrations
                .Cast<object>()
                .Select(registration => PropString(registration, "Value"))
                .ToArray();
            CollectionAssert.AreEqual(catalogs, registeredSources);
            CollectionAssert.AreEqual(
                catalogs.Select(path => "GameData/" + Path.GetFileName(path)).ToArray(),
                registeredDestinations,
                "PrepareForBuild must consume this exact source/destination registration plan.");
        }

        [Test]
        public void SharedGameDataBuildProcessorRejectsMissingDuplicateEmptyAndOverBoundedSources()
        {
            Type processor = Runtime("AL.EditorTools.SharedGameDataStreamingAssetsBuildProcessor");
            MethodInfo resolveCatalogs = processor.GetMethod(
                "ResolveCatalogFiles",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SharedGameDataBuildTests",
                Guid.NewGuid().ToString("N"));
            string assetsRoot = Path.Combine(root, "Assets");
            string source = Path.Combine(assetsRoot, "AL", "StreamingAssets", "GameData");
            string duplicate = Path.Combine(assetsRoot, "StreamingAssets", "GameData");
            try
            {
                AssertBuildRegistrationRejected(resolveCatalogs, assetsRoot, "source directory is missing");

                Directory.CreateDirectory(source);
                AssertBuildRegistrationRejected(resolveCatalogs, assetsRoot, "within 1..32");

                File.WriteAllText(Path.Combine(source, "catalog-00.json"), "{}");
                Directory.CreateDirectory(duplicate);
                AssertBuildRegistrationRejected(resolveCatalogs, assetsRoot, "Duplicate GameData");

                Directory.Delete(duplicate, true);
                for (int i = 1; i < 33; i++)
                {
                    File.WriteAllText(
                        Path.Combine(source, "catalog-" + i.ToString("00", CultureInfo.InvariantCulture) + ".json"),
                        "{}");
                }
                AssertBuildRegistrationRejected(resolveCatalogs, assetsRoot, "within 1..32");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void PlanUsesDescriptorScenesExactWindows64DevelopmentOptionsAndGuardedOutput()
        {
            string root = ProjectRoot();
            string outputDirectory = ExpectedOutputDirectory(root);
            object plan = CreatePlan(root, outputDirectory);

            Assert.IsTrue(PropBool(plan, "IsValid"), Failures(plan));
            var options = (BuildPlayerOptions)Invoke(plan, "CreateBuildPlayerOptions");
            Assert.AreEqual(BuildTarget.StandaloneWindows64, options.target);
            Assert.AreEqual(BuildOptions.Development, options.options);
            Assert.AreEqual(ExpectedExecutable(root), options.locationPathName);
            Assert.That(options.extraScriptingDefines, Is.Empty,
                "The normal Player must remain structurally unflavored.");
            CollectionAssert.AreEqual(new[]
            {
                "Assets/AL/Scenes/Boot.unity",
                "Assets/AL/Scenes/RealmSelection.unity",
                "Assets/AL/Scenes/CharacterCreation.unity",
                "Assets/AL/Scenes/ChampionArena.unity",
                "Assets/AL/Scenes/Kingdom.unity"
            }, options.scenes);
            Assert.That(options.scenes, Has.None.EqualTo("Assets/Test.unity"));
            Assert.That(options.scenes, Has.Some.EqualTo("Assets/AL/Scenes/ChampionArena.unity"));
            Assert.AreEqual("StandaloneWindows64", Prop(plan, "Target").ToString());
            Assert.AreEqual(BuildOptions.Development, (BuildOptions)Prop(plan, "Options"));
        }

        [Test]
        public void ApprovalBuildPlanUsesDedicatedGuardedOutputSummaryAndExactDefine()
        {
            string root = ProjectRoot();
            string outputDirectory = ExpectedApprovalOutputDirectory(root);
            object plan = Static(
                "CreateMvpApprovalPlan",
                root,
                outputDirectory,
                "6000.3.22f1",
                false,
                false,
                NewValidation(true, "valid"),
                true,
                true,
                false,
                false);

            Assert.That(PropBool(plan, "IsValid"), Is.True, Failures(plan));
            var options = (BuildPlayerOptions)Invoke(plan, "CreateBuildPlayerOptions");
            Assert.That(options.locationPathName, Is.EqualTo(ExpectedApprovalExecutable(root)));
            CollectionAssert.AreEqual(
                new[] { "AL_MVP_APPROVAL_SLOT" },
                options.extraScriptingDefines);
            Assert.That(PropString(plan, "SummaryPath"), Is.EqualTo(ExpectedApprovalSummaryPath(root)));
            CollectionAssert.AreEqual(new[]
            {
                "Assets/AL/Scenes/Boot.unity",
                "Assets/AL/Scenes/RealmSelection.unity",
                "Assets/AL/Scenes/CharacterCreation.unity",
                "Assets/AL/Scenes/ChampionArena.unity",
                "Assets/AL/Scenes/Kingdom.unity"
            }, options.scenes);
            Assert.That(
                (bool)Static("IsGuardedMvpApprovalOutputDirectory", root, outputDirectory),
                Is.True);
            Assert.That(
                (bool)Static("IsGuardedMvpApprovalOutputDirectory", root, ExpectedOutputDirectory(root)),
                Is.False);
            Assert.That(
                (bool)Static("IsGuardedOutputDirectory", root, outputDirectory),
                Is.False);
        }

        [Test]
        public void GuardRejectsAssetsArbitraryBuildFoldersAndProjectRoot()
        {
            string root = ProjectRoot();
            Assert.IsTrue((bool)Static("IsGuardedOutputDirectory", root, ExpectedOutputDirectory(root)));
            Assert.IsFalse((bool)Static("IsGuardedOutputDirectory", root, Path.Combine(root, "Assets", "Build")));
            Assert.IsFalse((bool)Static("IsGuardedOutputDirectory", root, Path.Combine(root, "Builds")));
            Assert.IsFalse((bool)Static("IsGuardedOutputDirectory", root, root));

            Assert.IsTrue((bool)Static("IsGuardedSummaryPath", root, ExpectedSummaryPath(root)));
            Assert.IsFalse((bool)Static(
                "IsGuardedSummaryPath",
                root,
                Path.Combine(root, "Assets", "ProductionPlayerBuildSummary.json")));
        }

        [Test]
        public void PlanFailsClosedForUnsafeOutputEvenWhenEveryOtherGatePasses()
        {
            string root = ProjectRoot();
            object plan = CreatePlan(root, Path.Combine(root, "Assets", "UnsafeBuild"));
            Assert.IsFalse(PropBool(plan, "IsValid"));
            StringAssert.Contains("exact guarded", Failures(plan));
        }

        [TestCase("version")]
        [TestCase("compiling")]
        [TestCase("compile-errors")]
        [TestCase("settings")]
        [TestCase("output-ignore")]
        [TestCase("output-reparse")]
        [TestCase("summary-ignore")]
        [TestCase("summary-reparse")]
        public void EveryPreflightFailurePreventsBuildAndCleanup(string failedGate)
        {
            EnvironmentState state = ValidEnvironment();
            switch (failedGate)
            {
                case "version": state.UnityVersion = "6000.3.21f1"; break;
                case "compiling": state.IsCompiling = true; break;
                case "compile-errors": state.HasCompilationErrors = true; break;
                case "settings": state.Validation = NewValidation(false, "settings drift"); break;
                case "output-ignore": state.OutputIgnored = false; break;
                case "output-reparse": state.OutputHasReparsePoint = true; break;
                case "summary-ignore": state.SummaryIgnored = false; break;
                case "summary-reparse": state.SummaryHasReparsePoint = true; break;
            }

            if (failedGate == "summary-ignore" || failedGate == "summary-reparse")
            {
                LogAssert.Expect(LogType.Error, new Regex("\\[AL-PLAYER-BUILD-SUMMARY-NOT-WRITTEN\\]"));
            }

            object summary = Execute(state);
            Assert.AreEqual("PreflightFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual(0, state.BuildCallCount, "BuildPipeline seam must not be called after failed preflight.");
            Assert.IsEmpty(state.DeletedPaths, "Failed preflight must not clean stale output.");
        }

        [Test]
        public void ValidExecutionCleansOnlyExactGuardedOutputThenBuildsOnce()
        {
            EnvironmentState state = ValidEnvironment();
            state.OutputDirectoryExists = true;

            object summary = Execute(state);

            Assert.AreEqual("Succeeded", Prop(summary, "Status").ToString(), PropString(summary, "SummaryMessage"));
            Assert.AreEqual(1, state.BuildCallCount);
            CollectionAssert.AreEqual(new[] { ExpectedOutputDirectory(state.ProjectRoot) }, state.DeletedPaths);
            Assert.NotNull(state.CapturedOptions);
            Assert.AreEqual(BuildTarget.StandaloneWindows64, state.CapturedOptions.Value.target);
            Assert.AreEqual(BuildOptions.Development, state.CapturedOptions.Value.options);
            Assert.AreEqual(ExpectedExecutable(state.ProjectRoot), state.CapturedOptions.Value.locationPathName);
            Assert.AreEqual(ExpectedSummaryPath(state.ProjectRoot), state.WrittenPath);
            Assert.That(state.WrittenContents, Does.Contain("\"status\": \"Succeeded\""));
        }

        [TestCase("Failed")]
        [TestCase("Cancelled")]
        [TestCase("Unknown")]
        public void NonSuccessBuildResultsFailAndRetainReportCounts(string buildResult)
        {
            EnvironmentState state = ValidEnvironment();
            state.Report = NewReport(
                buildResult,
                "StandaloneWindows64",
                ExpectedExecutable(state.ProjectRoot),
                TimeSpan.FromSeconds(12.5),
                1234UL,
                7,
                2,
                "scripted report");

            object summary = Execute(state);

            Assert.AreEqual("BuildFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual(buildResult, PropString(summary, "BuildResult"));
            Assert.AreEqual(7, PropInt(summary, "WarningCount"));
            Assert.AreEqual(2, PropInt(summary, "ErrorCount"));
            Assert.AreEqual(1, state.BuildCallCount);
        }

        [Test]
        public void SucceededReportWithErrorsFailsClosed()
        {
            EnvironmentState state = ValidEnvironment();
            state.Report = NewReport(
                "Succeeded",
                "StandaloneWindows64",
                ExpectedExecutable(state.ProjectRoot),
                TimeSpan.FromSeconds(1),
                10UL,
                0,
                1,
                "nominal result with an error");

            object summary = Execute(state);

            Assert.AreEqual("BuildFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual(1, PropInt(summary, "ErrorCount"));
            StringAssert.Contains("despite a Succeeded result", PropString(summary, "SummaryMessage"));
        }

        [TestCase(false, true, "AnotherLifeUnity.exe")]
        [TestCase(true, false, "AnotherLifeUnity_Data")]
        [TestCase(false, false, "AnotherLifeUnity.exe")]
        public void SuccessReportWithoutRequiredCurrentRunArtifactsFails(
            bool executableExists,
            bool dataDirectoryExists,
            string expectedMissing)
        {
            EnvironmentState state = ValidEnvironment();
            state.ExecutableExists = executableExists;
            state.DataDirectoryExists = dataDirectoryExists;

            object summary = Execute(state);

            Assert.AreEqual("ArtifactsMissing", Prop(summary, "Status").ToString());
            StringAssert.Contains(expectedMissing, PropString(summary, "SummaryMessage"));
            Assert.IsFalse(PropBool(summary, "Succeeded"));
        }

        [Test]
        public void SuccessRequiresMatchingReportAndBothArtifacts()
        {
            EnvironmentState state = ValidEnvironment();
            object summary = Execute(state);

            Assert.AreEqual("Succeeded", Prop(summary, "Status").ToString());
            Assert.IsTrue(PropBool(summary, "Succeeded"));
            Assert.AreEqual("Succeeded", PropString(summary, "BuildResult"));
            Assert.AreEqual(3, PropInt(summary, "WarningCount"));
            Assert.AreEqual(0, PropInt(summary, "ErrorCount"));
            Assert.AreEqual(987654UL, Convert.ToUInt64(Prop(summary, "TotalSize")));
            CollectionAssert.AreEqual(new[]
            {
                "Assets/AL/Scenes/Boot.unity",
                "Assets/AL/Scenes/RealmSelection.unity",
                "Assets/AL/Scenes/CharacterCreation.unity",
                "Assets/AL/Scenes/ChampionArena.unity",
                "Assets/AL/Scenes/Kingdom.unity"
            }, AsStrings(Prop(summary, "ScenePaths")));
        }

        [TestCase("StandaloneWindows", null)]
        [TestCase("StandaloneWindows64", "wrong-output")]
        public void SuccessfulReportWithTargetOrOutputDriftFails(string target, string outputMode)
        {
            EnvironmentState state = ValidEnvironment();
            string reportOutput = outputMode == null
                ? ExpectedExecutable(state.ProjectRoot)
                : Path.Combine(state.ProjectRoot, "Builds", "Wrong", "AnotherLifeUnity.exe");
            state.Report = NewReport(
                "Succeeded",
                target,
                reportOutput,
                TimeSpan.FromSeconds(1),
                1UL,
                0,
                0,
                "mismatch");

            object summary = Execute(state);
            Assert.AreEqual("BuildFailed", Prop(summary, "Status").ToString());
        }

        [Test]
        public void BuildInvocationExceptionIsTypedFailureAndStillWritesSummary()
        {
            EnvironmentState state = ValidEnvironment();
            state.ThrowOnBuild = true;

            object summary = Execute(state);

            Assert.AreEqual("BuildFailed", Prop(summary, "Status").ToString());
            Assert.AreEqual("Exception", PropString(summary, "BuildResult"));
            StringAssert.Contains("scripted build exception", PropString(summary, "SummaryMessage"));
            Assert.AreEqual(ExpectedSummaryPath(state.ProjectRoot), state.WrittenPath);
        }

        [Test]
        public void SummaryJsonHasFixedFieldOrderInvariantFormattingAndEscaping()
        {
            EnvironmentState state = ValidEnvironment();
            object summary = Execute(state);
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            string first;
            string second;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = new CultureInfo("ko-KR");
                first = (string)Static("SerializeSummary", summary);
                second = (string)Static("SerializeSummary", summary);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }

            Assert.AreEqual(first, second);
            string[] orderedFields =
            {
                "\"status\"",
                "\"target\"",
                "\"unityVersion\"",
                "\"outputPath\"",
                "\"scenePaths\"",
                "\"startedAtUtc\"",
                "\"endedAtUtc\"",
                "\"totalTime\"",
                "\"totalSize\"",
                "\"warningCount\"",
                "\"errorCount\"",
                "\"buildResult\"",
                "\"summaryMessage\""
            };
            int previousIndex = -1;
            foreach (string field in orderedFields)
            {
                int index = first.IndexOf(field, StringComparison.Ordinal);
                Assert.Greater(index, previousIndex, field + " must retain fixed serializer order.");
                previousIndex = index;
            }

            Assert.That(first, Does.Contain("\"totalTime\": \"00:00:02.5000000\""));
            Assert.That(first, Does.Contain("\"totalSize\": 987654"));
            Assert.That(first, Does.Contain("2026-07-22T01:02:03.0000000Z"));
            Assert.That(first, Does.Not.Contain("Assets/Test.unity"));
            Assert.That(first, Does.Contain("ChampionArena.unity"));
        }

        [Test]
        public void CompletionSerializationEscapesUntrustedReportText()
        {
            string root = ProjectRoot();
            object plan = CreatePlan(root, ExpectedOutputDirectory(root));
            object report = NewReport(
                "Failed",
                "StandaloneWindows64",
                ExpectedExecutable(root),
                TimeSpan.Zero,
                0UL,
                0,
                1,
                "quoted \"message\"\nnext");
            object summary = Static(
                "EvaluateCompletion",
                plan,
                report,
                new DateTime(2026, 7, 22, 1, 2, 3, DateTimeKind.Utc),
                new DateTime(2026, 7, 22, 1, 2, 4, DateTimeKind.Utc),
                false,
                false);

            string json = (string)Static("SerializeSummary", summary);
            Assert.That(json, Does.Contain("quoted \\\"message\\\"\\nnext"));
            Assert.That(json, Does.Not.Contain("\"message\"\nnext"));
        }

        private static EnvironmentState ValidEnvironment()
        {
            string root = ProjectRoot();
            var state = new EnvironmentState
            {
                ProjectRoot = root,
                UnityVersion = "6000.3.22f1",
                Validation = NewValidation(true, "valid"),
                OutputIgnored = true,
                SummaryIgnored = true,
                ExecutableExists = true,
                DataDirectoryExists = true,
                UtcBase = new DateTime(2026, 7, 22, 1, 2, 3, DateTimeKind.Utc)
            };
            state.Report = NewReport(
                "Succeeded",
                "StandaloneWindows64",
                ExpectedExecutable(root),
                TimeSpan.FromSeconds(2.5),
                987654UL,
                3,
                0,
                "scripted success");
            return state;
        }

        private static object Execute(EnvironmentState state)
        {
            object proxy = CreateDispatchProxy(EnvironmentType, typeof(ScriptedBuildEnvironmentProxy));
            ((ScriptedBuildEnvironmentProxy)proxy).State = state;
            return Static("Execute", proxy);
        }

        private static object CreatePlan(string root, string outputDirectory)
        {
            return Static(
                "CreatePlan",
                root,
                outputDirectory,
                "6000.3.22f1",
                false,
                false,
                NewValidation(true, "valid"),
                true,
                true,
                false,
                false);
        }

        private static object NewValidation(bool valid, string summary) =>
            Activator.CreateInstance(
                ValidationType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { valid, summary },
                culture: CultureInfo.InvariantCulture);

        private static object NewReport(
            string result,
            string target,
            string outputPath,
            TimeSpan totalTime,
            ulong totalSize,
            int warningCount,
            int errorCount,
            string summary) =>
            Activator.CreateInstance(
                ReportType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[]
                {
                    result,
                    target,
                    outputPath,
                    totalTime,
                    totalSize,
                    warningCount,
                    errorCount,
                    summary
                },
                culture: CultureInfo.InvariantCulture);

        private static object Static(string methodName, params object[] args)
        {
            MethodInfo method = BuilderType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(candidate =>
                    candidate.Name == methodName && candidate.GetParameters().Length == (args?.Length ?? 0));
            return method.Invoke(null, args);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(candidate =>
                    candidate.Name == methodName && candidate.GetParameters().Length == (args?.Length ?? 0));
            return method.Invoke(target, args);
        }

        private static void AssertBuildRegistrationRejected(
            MethodInfo resolver,
            string assetsRoot,
            string expectedMessage)
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => resolver.Invoke(null, new object[] { assetsRoot }));
            Assert.That(exception.InnerException, Is.Not.Null);
            Assert.That(
                exception.InnerException.GetType().FullName,
                Is.EqualTo("UnityEditor.Build.BuildFailedException"));
            Assert.That(exception.InnerException.Message, Does.Contain(expectedMessage));
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

        private static string ProjectRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static string ExpectedOutputDirectory(string root) =>
            Path.GetFullPath(Path.Combine(root, "Builds", "Validation", "Windows64"));

        private static string ExpectedExecutable(string root) =>
            Path.Combine(ExpectedOutputDirectory(root), "AnotherLifeUnity.exe");

        private static string ExpectedApprovalOutputDirectory(string root) =>
            Path.GetFullPath(Path.Combine(root, "Builds", "Validation", "Windows64MvpApproval"));

        private static string ExpectedApprovalExecutable(string root) =>
            Path.Combine(ExpectedApprovalOutputDirectory(root), "AnotherLifeUnity.exe");

        private static string ExpectedSummaryPath(string root) =>
            Path.GetFullPath(Path.Combine(root, "Logs", "ProductionPlayerBuildSummary.json"));

        private static string ExpectedApprovalSummaryPath(string root) =>
            Path.GetFullPath(Path.Combine(root, "Logs", "MvpApprovalPlayerBuildSummary.json"));

        public class ScriptedBuildEnvironmentProxy : DispatchProxy
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
                    case "UtcNow":
                        return State.UtcBase.AddSeconds(State.UtcCallCount++);
                    case "ValidateCurrentShellFoundation": return State.Validation;
                    case "IsIgnoredPath":
                        return string.Equals(
                            Path.GetFullPath((string)args[0]),
                            ExpectedOutputDirectory(State.ProjectRoot),
                            StringComparison.OrdinalIgnoreCase)
                            ? State.OutputIgnored
                            : State.SummaryIgnored;
                    case "HasReparsePoint":
                        return string.Equals(
                            Path.GetFullPath((string)args[0]),
                            ExpectedOutputDirectory(State.ProjectRoot),
                            StringComparison.OrdinalIgnoreCase)
                            ? State.OutputHasReparsePoint
                            : State.SummaryHasReparsePoint;
                    case "DirectoryExists":
                    {
                        string path = Path.GetFullPath((string)args[0]);
                        if (string.Equals(
                                path,
                                Path.Combine(ExpectedOutputDirectory(State.ProjectRoot), "AnotherLifeUnity_Data"),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return State.DataDirectoryExists;
                        }

                        if (string.Equals(path, ExpectedOutputDirectory(State.ProjectRoot), StringComparison.OrdinalIgnoreCase))
                        {
                            return State.OutputDirectoryExists;
                        }

                        return State.CreatedDirectories.Contains(path, StringComparer.OrdinalIgnoreCase);
                    }
                    case "FileExists": return State.ExecutableExists;
                    case "DeleteDirectory":
                        State.DeletedPaths.Add(Path.GetFullPath((string)args[0]));
                        State.OutputDirectoryExists = false;
                        return null;
                    case "CreateDirectory":
                    {
                        string path = Path.GetFullPath((string)args[0]);
                        State.CreatedDirectories.Add(path);
                        if (string.Equals(path, ExpectedOutputDirectory(State.ProjectRoot), StringComparison.OrdinalIgnoreCase))
                        {
                            State.OutputDirectoryExists = true;
                        }

                        return null;
                    }
                    case "BuildPlayer":
                        State.BuildCallCount++;
                        State.CapturedOptions = (BuildPlayerOptions)args[0];
                        if (State.ThrowOnBuild)
                        {
                            throw new InvalidOperationException("scripted build exception");
                        }

                        return State.Report;
                    case "WriteAllText":
                        State.WrittenPath = Path.GetFullPath((string)args[0]);
                        State.WrittenContents = (string)args[1];
                        return null;
                    default:
                        throw new NotSupportedException("Unexpected environment member: " + targetMethod.Name);
                }
            }
        }

        public sealed class EnvironmentState
        {
            public string ProjectRoot;
            public string UnityVersion;
            public bool IsCompiling;
            public bool HasCompilationErrors;
            public object Validation;
            public bool OutputIgnored;
            public bool SummaryIgnored;
            public bool OutputHasReparsePoint;
            public bool SummaryHasReparsePoint;
            public bool OutputDirectoryExists;
            public bool ExecutableExists;
            public bool DataDirectoryExists;
            public bool ThrowOnBuild;
            public object Report;
            public DateTime UtcBase;
            public int UtcCallCount;
            public int BuildCallCount;
            public BuildPlayerOptions? CapturedOptions;
            public readonly List<string> DeletedPaths = new List<string>();
            public readonly List<string> CreatedDirectories = new List<string>();
            public string WrittenPath;
            public string WrittenContents;
        }
    }
}
