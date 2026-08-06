#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using AL.Core.Scenes;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AL.EditorTools
{
    /// <summary>
    /// Generates one guarded, ignored Unity 2022.3 Android Gradle export for later native-app
    /// packaging work. It deliberately does not modify the native Gradle project or register a
    /// bridge route. All temporary Android PlayerSettings are restored in a finally block.
    /// </summary>
    public static class AndroidUnityLibraryExporter
    {
        public const string RequiredUnityVersion = "2022.3.62f3";
        public const string OutputRelativeDirectory = "Builds/AndroidExport";
        public const string SummaryRelativePath = "Logs/AndroidUnityLibraryExportSummary.json";
        public const string RequiredScriptingBackend = "IL2CPP";
        public const string RequiredTargetArchitectures = "ARM64";
        public const int RequiredMinimumApiLevel = 24;
        public const int MaximumArtifactFiles = 8192;
        public const int MaximumArtifactDirectories = 8192;
        public const long MaximumArtifactBytes = 2L * 1024L * 1024L * 1024L;
        private const int MaximumStructuralTextBytes = 64 * 1024;

        private static readonly string[] RequiredArtifactPaths =
        {
            "build.gradle",
            "gradle.properties",
            "settings.gradle",
            "unityLibrary/build.gradle",
            "unityLibrary/libs/unity-classes.jar",
            "unityLibrary/proguard-unity.txt",
            "unityLibrary/src/main/AndroidManifest.xml",
            "unityLibrary/src/main/assets/bin/Data/globalgamemanagers",
            "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/libil2cpp/il2cpp-api.cpp",
            "unityLibrary/src/main/Il2CppOutputProject/Source/il2cppOutput/Il2CppCodeRegistration.cpp",
            "unityLibrary/src/main/jniLibs/arm64-v8a/libmain.so",
            "unityLibrary/src/main/jniLibs/arm64-v8a/libunity.so"
        };

        private const string Il2CppToolDirectory =
            "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/";
        private const string WindowsIl2CppToolPath = Il2CppToolDirectory + "il2cpp.exe";
        private const string UnixIl2CppToolPath = Il2CppToolDirectory + "il2cpp";
        private const int ArtifactReadBufferBytes = 64 * 1024;

        /// <summary>Unity -executeMethod entry. A rejected export intentionally exits non-zero.</summary>
        public static void ExportDevelopmentArm64Il2Cpp()
        {
            AndroidUnityLibraryExportSummary summary = Execute(
                new UnityAndroidUnityLibraryExportEnvironment());
            Debug.Log("[AL-ANDROID-UNITY-EXPORT-SUMMARY] " + summary.Summarize());
            if (!summary.Succeeded)
            {
                throw new BuildFailedException(summary.SummaryMessage);
            }
        }

        internal static AndroidUnityLibraryExportSummary Execute(
            IAndroidUnityLibraryExportEnvironment environment)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            DateTime startedAtUtc = SafeUtcNow(environment);
            string projectRoot;
            string outputDirectory;
            string summaryPath;
            string outputIdentity;
            try
            {
                projectRoot = NormalizeFullPath(environment.ProjectRoot);
                outputDirectory = NormalizeFullPath(Path.Combine(
                    projectRoot,
                    "Builds",
                    "AndroidExport"));
                summaryPath = NormalizeFullPath(Path.Combine(
                    projectRoot,
                    "Logs",
                    "AndroidUnityLibraryExportSummary.json"));
            }
            catch (Exception exception)
            {
                return AndroidUnityLibraryExportSummary.PreflightFailure(
                    string.Empty,
                    string.Empty,
                    Array.Empty<string>(),
                    environment.UnityVersion,
                    startedAtUtc,
                    SafeUtcNow(environment),
                    "Project/output path resolution failed: " + exception.GetType().Name + ".");
            }

            if (!environment.IsStrongPathAttestationSupported)
            {
                // BuildPipeline accepts only a pathname. On Unix, an open directory descriptor
                // does not deny rename/unlink, so a swap-write-restore race cannot be excluded.
                // Do not write even the summary until a capability-relative or namespace-locked
                // export boundary exists for that host.
                return AndroidUnityLibraryExportSummary.PreflightFailure(
                    outputDirectory,
                    BuildTarget.Android.ToString(),
                    ProductionSceneDescriptor.ShellFoundationOrdered
                        .Where(record => record != null)
                        .Select(record => record.AssetPath ?? string.Empty)
                        .ToArray(),
                    environment.UnityVersion,
                    startedAtUtc,
                    SafeUtcNow(environment),
                    "Strong no-follow output namespace attestation is unavailable on this Editor host; " +
                    "Android unityLibrary export is currently Windows-only.");
            }

            BuildValidationSnapshot validation;
            try
            {
                validation = environment.ValidateCurrentShellFoundation() ??
                    BuildValidationSnapshot.Invalid("Build Settings validator returned no report.");
            }
            catch (Exception exception)
            {
                validation = BuildValidationSnapshot.Invalid(
                    "Build Settings validation threw " + exception.GetType().Name + ": " +
                    exception.Message);
            }

            bool outputIgnored = SafeBoolean(() => environment.IsIgnoredPath(outputDirectory));
            bool outputTracked = SafeBooleanFailureClosed(() => environment.IsTrackedPath(outputDirectory));
            bool outputHasReparsePoint = SafeBooleanFailureClosed(
                () => environment.HasReparsePoint(outputDirectory));
            bool summaryIgnored = SafeBoolean(() => environment.IsIgnoredPath(summaryPath));
            bool summaryTracked = SafeBooleanFailureClosed(() => environment.IsTrackedPath(summaryPath));
            bool summaryHasReparsePoint = SafeBooleanFailureClosed(
                () => environment.HasReparsePoint(summaryPath));

            AndroidUnityLibraryExportPlan plan = CreatePlan(
                projectRoot,
                outputDirectory,
                summaryPath,
                environment.UnityVersion,
                environment.IsCompiling,
                environment.HasCompilationErrors,
                environment.IsAndroidBuildSupported,
                environment.IsAndroidActiveBuildTarget,
                validation,
                outputIgnored,
                outputTracked,
                outputHasReparsePoint,
                summaryIgnored,
                summaryTracked,
                summaryHasReparsePoint);

            if (!plan.IsValid)
            {
                return FinalizeSummary(
                    environment,
                    plan,
                    AndroidUnityLibraryExportSummary.PreflightFailure(
                        plan.OutputDirectory,
                        plan.Target.ToString(),
                        plan.ScenePaths,
                        plan.UnityVersion,
                        startedAtUtc,
                        SafeUtcNow(environment),
                        plan.SummarizeFailures()));
            }

            AndroidUnityLibraryBuildSettingsSnapshot originalSettings;
            try
            {
                originalSettings = environment.CaptureBuildSettings();
                if (originalSettings == null)
                {
                    throw new InvalidOperationException("Build settings snapshot was null.");
                }
            }
            catch (Exception exception)
            {
                return FinalizeSummary(
                    environment,
                    plan,
                    AndroidUnityLibraryExportSummary.PreparationFailure(
                        plan,
                        startedAtUtc,
                        SafeUtcNow(environment),
                        "Android build settings snapshot failed: " + exception.GetType().Name +
                        ": " + exception.Message));
            }

            try
            {
                if (environment.DirectoryExists(plan.OutputDirectory))
                {
                    // The exact-path, ignore, tracked-path, and reparse gates already passed. The
                    // concrete environment repeats them before this destructive operation.
                    environment.DeleteDirectory(plan.OutputDirectory);
                }

                if (environment.DirectoryExists(plan.OutputDirectory))
                {
                    throw new IOException("Guarded Android export output still exists after cleanup.");
                }

                environment.CreateDirectory(plan.OutputDirectory);
                if (!environment.DirectoryExists(plan.OutputDirectory))
                {
                    throw new IOException("Guarded Android export output could not be created.");
                }
                outputIdentity = environment.AttestOutputDirectory(plan.OutputDirectory);
                if (string.IsNullOrEmpty(outputIdentity))
                {
                    throw new IOException("Guarded Android export output identity is unavailable.");
                }
            }
            catch (Exception exception)
            {
                return FinalizeSummary(
                    environment,
                    plan,
                    AndroidUnityLibraryExportSummary.PreparationFailure(
                        plan,
                        startedAtUtc,
                        SafeUtcNow(environment),
                        exception.GetType().Name + ": " + exception.Message));
            }

            AndroidUnityLibraryBuildReportSnapshot report =
                AndroidUnityLibraryBuildReportSnapshot.NotRun(
                    plan.Target.ToString(),
                    plan.OutputDirectory,
                    "Build was not started.");
            AndroidUnityLibraryArtifactSnapshot artifacts =
                AndroidUnityLibraryArtifactSnapshot.NotInspected("Export was not inspected.");
            string operationFailure = string.Empty;
            string restorationFailure = string.Empty;

            try
            {
                environment.ApplyRequiredBuildSettings();
                using (IDisposable outputLease = environment.AcquireOutputMutationLease(
                           plan.OutputDirectory,
                           outputIdentity))
                {
                    if (outputLease == null)
                    {
                        throw new IOException("Guarded Android export output mutation lease is unavailable.");
                    }
                    report = environment.BuildPlayer(plan.CreateBuildPlayerOptions()) ??
                        AndroidUnityLibraryBuildReportSnapshot.NotRun(
                            plan.Target.ToString(),
                            plan.OutputDirectory,
                            "BuildPipeline returned no BuildReport.");
                    artifacts = environment.InspectExport(plan.OutputDirectory) ??
                        AndroidUnityLibraryArtifactSnapshot.NotInspected(
                            "Export inspection returned no result.");
                }
            }
            catch (Exception exception)
            {
                operationFailure = exception.GetType().Name + ": " + exception.Message;
                report = AndroidUnityLibraryBuildReportSnapshot.Exception(
                    plan.Target.ToString(),
                    plan.OutputDirectory,
                    operationFailure);
                artifacts = AndroidUnityLibraryArtifactSnapshot.NotInspected(operationFailure);
            }
            finally
            {
                var restoreFailures = new List<string>();
                try
                {
                    environment.RestoreBuildSettings(originalSettings);
                }
                catch (Exception exception)
                {
                    restoreFailures.Add(exception.GetType().Name + ": " + exception.Message);
                }

                try
                {
                    AndroidUnityLibraryBuildSettingsSnapshot restored =
                        environment.CaptureBuildSettings();
                    string mismatch = DescribeBuildSettingsMismatch(originalSettings, restored);
                    if (mismatch.Length > 0) restoreFailures.Add(mismatch);
                }
                catch (Exception exception)
                {
                    restoreFailures.Add(
                        "Post-restore settings recapture failed: " + exception.GetType().Name +
                        ": " + exception.Message);
                }

                restorationFailure = string.Join(" | ", restoreFailures);
            }

            AndroidUnityLibraryExportSummary completion;
            if (!string.IsNullOrEmpty(restorationFailure))
            {
                completion = AndroidUnityLibraryExportSummary.SettingsRestoreFailure(
                    plan,
                    report,
                    artifacts,
                    startedAtUtc,
                    SafeUtcNow(environment),
                    "Temporary Android build settings restoration failed: " + restorationFailure);
            }
            else if (!string.IsNullOrEmpty(operationFailure))
            {
                completion = AndroidUnityLibraryExportSummary.BuildFailure(
                    plan,
                    report,
                    artifacts,
                    startedAtUtc,
                    SafeUtcNow(environment),
                    "Android Gradle export failed: " + operationFailure);
            }
            else
            {
                completion = EvaluateCompletion(
                    plan,
                    report,
                    artifacts,
                    startedAtUtc,
                    SafeUtcNow(environment));
            }

            return FinalizeSummary(environment, plan, completion);
        }

        internal static AndroidUnityLibraryExportPlan CreatePlan(
            string projectRoot,
            string outputDirectory,
            string summaryPath,
            string unityVersion,
            bool isCompiling,
            bool hasCompilationErrors,
            bool isAndroidBuildSupported,
            bool isAndroidActiveBuildTarget,
            BuildValidationSnapshot validation,
            bool outputIgnored,
            bool outputTracked,
            bool outputHasReparsePoint,
            bool summaryIgnored,
            bool summaryTracked,
            bool summaryHasReparsePoint)
        {
            var failures = new List<string>();
            string normalizedRoot = TryNormalize(projectRoot);
            string normalizedOutput = TryNormalize(outputDirectory);
            string normalizedSummary = TryNormalize(summaryPath);

            if (normalizedRoot.Length == 0)
            {
                failures.Add("Project root is missing or invalid.");
            }

            if (!string.Equals(unityVersion, RequiredUnityVersion, StringComparison.Ordinal))
            {
                failures.Add("Exact Unity version required: " + RequiredUnityVersion +
                    "; actual: " + (string.IsNullOrEmpty(unityVersion) ? "<missing>" : unityVersion) + ".");
            }

            if (isCompiling) failures.Add("Unity script compilation is still in progress.");
            if (hasCompilationErrors) failures.Add("Unity reports script compilation errors.");
            if (!isAndroidBuildSupported) failures.Add("Unity Android Build Support is unavailable.");
            if (!isAndroidActiveBuildTarget)
            {
                failures.Add("Active build target must already be Android; invoke Unity with -buildTarget Android.");
            }

            if (validation == null || !validation.IsValid)
            {
                failures.Add("ShellFoundation Build Settings/scene validation failed: " +
                    (validation == null ? "no report" : validation.Summary));
            }

            if (!IsGuardedOutputDirectory(normalizedRoot, normalizedOutput))
            {
                failures.Add("Export output is not the exact guarded Builds/AndroidExport directory outside Assets.");
            }
            if (!outputIgnored) failures.Add("Guarded Android export output is not ignored.");
            if (outputTracked) failures.Add("Guarded Android export output contains a tracked path.");
            if (outputHasReparsePoint)
            {
                failures.Add("Guarded Android export output contains a reparse-point/symlink boundary.");
            }

            if (!IsGuardedSummaryPath(normalizedRoot, normalizedSummary) || !summaryIgnored)
            {
                failures.Add("Export summary is not the exact ignored Logs path outside Assets.");
            }
            if (summaryTracked)
            {
                failures.Add("Export summary path is tracked and must not be overwritten.");
            }
            if (summaryHasReparsePoint)
            {
                failures.Add("Export summary path contains a reparse-point/symlink boundary.");
            }

            ProductionSceneRecord[] records = ProductionSceneDescriptor.ShellFoundationOrdered.ToArray();
            if (records.Length != 3 ||
                records.Any(record => record == null || !record.IsProductionScene || !record.IsInShellFoundation) ||
                records.Any(record =>
                    string.Equals(record.SceneId, ProductionSceneDescriptor.TestSceneId, StringComparison.Ordinal) ||
                    string.Equals(record.SceneId, ProductionSceneDescriptor.ChampionArenaSceneId, StringComparison.Ordinal)) ||
                records.Select(record => record.AssetPath).Distinct(StringComparer.Ordinal).Count() != records.Length)
            {
                failures.Add("ShellFoundation descriptor is not three unique production scenes with Test/Champion excluded.");
            }

            string[] scenes = records
                .Where(record => record != null)
                .Select(record => record.AssetPath ?? string.Empty)
                .ToArray();
            bool canWriteSummary = IsGuardedSummaryPath(normalizedRoot, normalizedSummary) &&
                summaryIgnored && !summaryTracked && !summaryHasReparsePoint;
            return new AndroidUnityLibraryExportPlan(
                normalizedRoot,
                normalizedOutput,
                normalizedSummary,
                unityVersion ?? string.Empty,
                scenes,
                failures,
                canWriteSummary);
        }

        internal static AndroidUnityLibraryExportSummary EvaluateCompletion(
            AndroidUnityLibraryExportPlan plan,
            AndroidUnityLibraryBuildReportSnapshot report,
            AndroidUnityLibraryArtifactSnapshot artifacts,
            DateTime startedAtUtc,
            DateTime endedAtUtc)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            report = report ?? AndroidUnityLibraryBuildReportSnapshot.NotRun(
                plan.Target.ToString(),
                plan.OutputDirectory,
                "Build report was missing.");
            artifacts = artifacts ?? AndroidUnityLibraryArtifactSnapshot.NotInspected(
                "Artifact inspection was missing.");

            if (!string.Equals(report.Result, BuildResult.Succeeded.ToString(), StringComparison.Ordinal) ||
                report.ErrorCount != 0 ||
                !string.Equals(report.Target, plan.Target.ToString(), StringComparison.Ordinal) ||
                !SamePath(report.OutputPath, plan.OutputDirectory))
            {
                return AndroidUnityLibraryExportSummary.BuildFailure(
                    plan,
                    report,
                    artifacts,
                    startedAtUtc,
                    endedAtUtc,
                    "BuildReport did not prove a successful exact Android export. " + report.ReportSummary);
            }

            if (!artifacts.IsValid)
            {
                return AndroidUnityLibraryExportSummary.ArtifactFailure(
                    plan,
                    report,
                    artifacts,
                    startedAtUtc,
                    endedAtUtc,
                    artifacts.Summary);
            }

            return AndroidUnityLibraryExportSummary.Success(
                plan,
                report,
                artifacts,
                startedAtUtc,
                endedAtUtc);
        }

        internal static AndroidUnityLibraryArtifactSnapshot InspectExportTree(string outputDirectory) =>
            InspectExportTree(
                outputDirectory,
                MaximumArtifactFiles,
                MaximumArtifactDirectories,
                MaximumArtifactBytes,
                inspectionHook: null);

        internal static AndroidUnityLibraryArtifactSnapshot InspectExportTree(
            string outputDirectory,
            int maximumFiles,
            int maximumDirectories,
            long maximumBytes) => InspectExportTree(
                outputDirectory,
                maximumFiles,
                maximumDirectories,
                maximumBytes,
                inspectionHook: null);

        internal static AndroidUnityLibraryArtifactSnapshot InspectExportTree(
            string outputDirectory,
            int maximumFiles,
            int maximumDirectories,
            long maximumBytes,
            Action<string, string> inspectionHook)
        {
            try
            {
                if (!SupportsStrongPathAttestation(Application.platform))
                {
                    return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                        "Strong no-follow artifact namespace attestation is unavailable on this Editor host; " +
                        "inspection is currently Windows-only.");
                }
                string root = NormalizeFullPath(outputDirectory);
                if (!Directory.Exists(root))
                {
                    return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                        "Export output directory does not exist.");
                }

                if (maximumFiles <= 0 || maximumFiles > MaximumArtifactFiles ||
                    maximumDirectories <= 0 || maximumDirectories > MaximumArtifactDirectories ||
                    maximumBytes <= 0 || maximumBytes > MaximumArtifactBytes)
                {
                    return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                        "Artifact inspection bounds are outside the production limits.");
                }

                FileAttributes rootAttributes = File.GetAttributes(root);
                if ((rootAttributes & FileAttributes.Directory) == 0 ||
                    !IsSafeArtifactAttributes(rootAttributes))
                {
                    return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                        "Export root is not a regular non-reparse directory.");
                }

                string expectedTool = ExpectedIl2CppToolPath(Application.platform);
                if (expectedTool.Length == 0)
                {
                    return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                        "Current Unity Editor host is unsupported for IL2CPP tool validation: " +
                        Application.platform + ".");
                }

                var pendingDirectories = new Queue<string>();
                var acceptedFiles = new List<AttestedArtifactFile>();
                var relativeDirectories = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
                pendingDirectories.Enqueue(root);
                int directoryCount = 1;
                long totalBytes = 0;
                while (pendingDirectories.Count > 0)
                {
                    string directory = pendingDirectories.Dequeue();
                    using (IDisposable directoryLease = AcquireArtifactDirectoryLease(directory))
                    {
                        foreach (string entry in Directory.EnumerateFileSystemEntries(
                                     directory,
                                     "*",
                                     SearchOption.TopDirectoryOnly))
                        {
                            string fullEntry = NormalizeFullPath(entry);
                            if (!IsSameOrDescendant(fullEntry, root))
                            {
                                return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                                    "Artifact entry escaped the export root.");
                            }

                            string relative = RelativePath(root, fullEntry);
                            FileAttributes attributes = File.GetAttributes(fullEntry);
                            if (!IsSafeArtifactAttributes(attributes))
                            {
                                return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                                    "Artifact tree contains a reparse-point/symlink entry: " +
                                    relative + ".");
                            }

                            if ((attributes & FileAttributes.Directory) != 0)
                            {
                                directoryCount++;
                                if (directoryCount > maximumDirectories)
                                {
                                    return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                                        "Export exceeds the " + maximumDirectories +
                                        " directory inspection bound.");
                                }
                                relativeDirectories.Add(relative);
                                pendingDirectories.Enqueue(fullEntry);
                                continue;
                            }

                            if (acceptedFiles.Count >= maximumFiles)
                            {
                                return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                                    "Export exceeds the " + maximumFiles +
                                    " file inspection bound.");
                            }

                            AttestedArtifactFile snapshot = ReadAttestedArtifact(
                                fullEntry,
                                relative,
                                maximumBytes - totalBytes,
                                Application.platform,
                                inspectionHook);
                            if (snapshot.Length < 0 || totalBytes > maximumBytes - snapshot.Length)
                            {
                                return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                                    "Export exceeds the " + maximumBytes +
                                    " byte inspection bound.");
                            }
                            totalBytes += snapshot.Length;
                            acceptedFiles.Add(snapshot);
                        }
                    }
                }

                AttestedArtifactFile[] files = acceptedFiles
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .ToArray();
                var inventory = new StringBuilder(Math.Max(256, files.Length * 128));
                var snapshots = new Dictionary<string, AttestedArtifactFile>(StringComparer.Ordinal);
                foreach (AttestedArtifactFile file in files)
                {
                    if (snapshots.ContainsKey(file.RelativePath))
                    {
                        return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                            "Export contains a duplicate normalized artifact path: " +
                            file.RelativePath + ".");
                    }
                    snapshots.Add(file.RelativePath, file);
                    inventory.Append(file.RelativePath).Append('\0')
                        .Append(file.Length.ToString(CultureInfo.InvariantCulture)).Append('\0')
                        .Append(file.Sha256).Append('\n');
                }

                var missingArtifacts = RequiredArtifactPaths
                    .Where(required => !snapshots.ContainsKey(required))
                    .ToList();
                if (!snapshots.ContainsKey(expectedTool))
                {
                    missingArtifacts.Add(expectedTool);
                }
                string[] missing = missingArtifacts
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string[] invalid = RequiredArtifactPaths
                    .Where(relative => snapshots.ContainsKey(relative))
                    .Concat(new[] { WindowsIl2CppToolPath, UnixIl2CppToolPath }
                        .Where(relative => snapshots.ContainsKey(relative)))
                    .Select(relative => ValidateRequiredArtifact(
                        relative,
                        snapshots[relative],
                        Application.platform))
                    .Where(failure => failure.Length > 0)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                const string jniPrefix = "unityLibrary/src/main/jniLibs/";
                string[] abiDirectories = relativeDirectories
                    .Where(relative => relative.StartsWith(jniPrefix, StringComparison.Ordinal))
                    .Select(relative => relative.Substring(jniPrefix.Length))
                    .Where(relative => relative.Length > 0 && relative.IndexOf('/') < 0)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                string inventoryHash = ComputeSha256(
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(inventory.ToString()));
                var concerns = new List<string>();
                if (missing.Length > 0)
                {
                    concerns.Add("Required export artifacts are missing: " +
                        string.Join(", ", missing) + ".");
                }
                if (invalid.Length > 0)
                {
                    concerns.Add("Required export artifacts are invalid: " +
                        string.Join(", ", invalid) + ".");
                }
                if (!abiDirectories.SequenceEqual(new[] { "arm64-v8a" }, StringComparer.Ordinal))
                {
                    concerns.Add("Export ABI directories must be exactly arm64-v8a; actual=" +
                        string.Join(",", abiDirectories) + ".");
                }
                string summary = concerns.Count == 0
                    ? "Inspected " + files.Length + " file(s), " + totalBytes +
                      " byte(s), directories=" + directoryCount + ", ABIs=arm64-v8a."
                    : string.Join(" | ", concerns);
                return new AndroidUnityLibraryArtifactSnapshot(
                    inspected: true,
                    fileCount: files.Length,
                    totalBytes: totalBytes,
                    inventorySha256: inventoryHash,
                    missingArtifacts: missing,
                    invalidArtifacts: invalid,
                    abiDirectories: abiDirectories,
                    summary: summary);
            }
            catch (Exception exception)
            {
                return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                    "Export inspection failed: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        internal static bool IsSafeArtifactAttributes(FileAttributes attributes) =>
            (attributes & FileAttributes.ReparsePoint) == 0;

        private static string ValidateRequiredArtifact(
            string relativePath,
            AttestedArtifactFile artifact,
            RuntimePlatform host)
        {
            if (artifact == null || artifact.Length <= 0) return relativePath + " (empty)";

            if (string.Equals(relativePath, WindowsIl2CppToolPath, StringComparison.Ordinal) ||
                string.Equals(relativePath, UnixIl2CppToolPath, StringComparison.Ordinal))
            {
                return ValidateIl2CppToolForHost(
                    relativePath,
                    artifact.PrefixBytes,
                    artifact.IsUnixExecutable,
                    host);
            }

            if (string.Equals(
                    relativePath,
                    "unityLibrary/libs/unity-classes.jar",
                    StringComparison.Ordinal) &&
                !HasZipSignature(artifact.PrefixBytes))
            {
                return relativePath + " (missing ZIP/JAR signature)";
            }

            if (relativePath.EndsWith(".so", StringComparison.Ordinal) &&
                !StartsWithBytes(artifact.PrefixBytes, new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }))
            {
                return relativePath + " (missing ELF signature)";
            }

            if (string.Equals(relativePath, "settings.gradle", StringComparison.Ordinal) &&
                !PrefixContainsText(artifact.PrefixBytes, "unityLibrary"))
            {
                return relativePath + " (unityLibrary module is not included)";
            }

            if (string.Equals(relativePath, "unityLibrary/build.gradle", StringComparison.Ordinal) &&
                (!PrefixContainsText(artifact.PrefixBytes, "com.android.library") ||
                 !PrefixContainsText(artifact.PrefixBytes, "minSdkVersion 24") ||
                 !PrefixContainsText(artifact.PrefixBytes, "Il2CppOutputProject") ||
                 !PrefixContainsText(artifact.PrefixBytes, "libil2cpp.so")))
            {
                return relativePath +
                    " (library plugin, minimum API 24, or staged IL2CPP Gradle generation is missing)";
            }

            if (string.Equals(
                    relativePath,
                    "unityLibrary/src/main/AndroidManifest.xml",
                    StringComparison.Ordinal) &&
                !PrefixContainsText(artifact.PrefixBytes, "<manifest"))
            {
                return relativePath + " (manifest root is missing)";
            }

            return string.Empty;
        }

        internal static string ValidateIl2CppToolForHost(
            string relativePath,
            byte[] prefixBytes,
            bool unixExecutable,
            RuntimePlatform host)
        {
            string expected = ExpectedIl2CppToolPath(host);
            if (expected.Length == 0)
            {
                return relativePath + " (unsupported Editor host " + host + ")";
            }
            if (!string.Equals(relativePath, expected, StringComparison.Ordinal))
            {
                return relativePath + " (wrong tool name for " + host + "; expected " + expected + ")";
            }

            byte[] prefix = prefixBytes ?? Array.Empty<byte>();
            if (host == RuntimePlatform.WindowsEditor)
            {
                return HasCrediblePortableExecutableHeader(prefix)
                    ? string.Empty
                    : relativePath + " (invalid or truncated PE executable)";
            }
            if (!unixExecutable)
            {
                return relativePath + " (tool is not executable on the Unix host)";
            }
            if (host == RuntimePlatform.LinuxEditor)
            {
                return StartsWithBytes(prefix, new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' })
                    ? string.Empty
                    : relativePath + " (invalid or truncated ELF executable)";
            }
            if (host == RuntimePlatform.OSXEditor)
            {
                return HasMachOOrFatMagic(prefix)
                    ? string.Empty
                    : relativePath + " (invalid or truncated Mach-O executable)";
            }
            return relativePath + " (unsupported Editor host " + host + ")";
        }

        internal static bool SupportsStrongPathAttestation(RuntimePlatform host) =>
            host == RuntimePlatform.WindowsEditor;

        private static string ExpectedIl2CppToolPath(RuntimePlatform host)
        {
            if (host == RuntimePlatform.WindowsEditor) return WindowsIl2CppToolPath;
            if (host == RuntimePlatform.LinuxEditor || host == RuntimePlatform.OSXEditor)
            {
                return UnixIl2CppToolPath;
            }
            return string.Empty;
        }

        private static bool HasCrediblePortableExecutableHeader(IReadOnlyList<byte> bytes)
        {
            if (bytes == null || bytes.Count < 0x40 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
            {
                return false;
            }
            int peOffset = bytes[0x3c] |
                (bytes[0x3d] << 8) |
                (bytes[0x3e] << 16) |
                (bytes[0x3f] << 24);
            return peOffset >= 0x40 && peOffset <= bytes.Count - 4 &&
                bytes[peOffset] == (byte)'P' && bytes[peOffset + 1] == (byte)'E' &&
                bytes[peOffset + 2] == 0 && bytes[peOffset + 3] == 0;
        }

        private static bool HasMachOOrFatMagic(IReadOnlyList<byte> bytes)
        {
            if (bytes == null || bytes.Count < 4) return false;
            uint magic = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) |
                ((uint)bytes[2] << 8) | bytes[3];
            return magic == 0xfeedface || magic == 0xcefaedfe ||
                magic == 0xfeedfacf || magic == 0xcffaedfe ||
                magic == 0xcafebabe || magic == 0xbebafeca ||
                magic == 0xcafebabf || magic == 0xbfbafeca;
        }

        private static bool HasZipSignature(IReadOnlyList<byte> bytes)
        {
            byte[][] signatures =
            {
                new byte[] { 0x50, 0x4b, 0x03, 0x04 },
                new byte[] { 0x50, 0x4b, 0x05, 0x06 },
                new byte[] { 0x50, 0x4b, 0x07, 0x08 }
            };
            return signatures.Any(signature => StartsWithBytes(bytes, signature));
        }

        private static bool StartsWithBytes(IReadOnlyList<byte> bytes, IReadOnlyList<byte> expected)
        {
            if (bytes == null || expected == null || bytes.Count < expected.Count) return false;
            for (int index = 0; index < expected.Count; index++)
            {
                if (bytes[index] != expected[index]) return false;
            }
            return true;
        }

        private static bool PrefixContainsText(byte[] bytes, string expected)
        {
            string text = new UTF8Encoding(false, true).GetString(bytes ?? Array.Empty<byte>());
            return text.IndexOf(expected, StringComparison.Ordinal) >= 0;
        }

        private static IDisposable AcquireArtifactDirectoryLease(string directory)
        {
            FileAttributes attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.Directory) == 0 || !IsSafeArtifactAttributes(attributes))
            {
                throw new IOException("Artifact directory is not a regular non-reparse directory: " + directory + ".");
            }

            if (!SupportsStrongPathAttestation(Application.platform))
            {
                throw new PlatformNotSupportedException(
                    "Artifact directory leases are currently supported only on Windows Editor.");
            }

            SafeFileHandle handle = OpenWindowsPathNoFollow(directory, directory: true, writable: false);
            try
            {
                WindowsFileIdentity identity = ReadWindowsIdentity(handle, directory);
                if (!identity.IsDirectory || identity.IsReparsePoint)
                {
                    throw new IOException("Artifact directory handle is not a regular non-reparse directory: " +
                        directory + ".");
                }
                AssertWindowsHandlePath(handle, directory);
                return new SafeHandleLease(handle);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        private static AttestedArtifactFile ReadAttestedArtifact(
            string fullPath,
            string relativePath,
            long remainingBytes,
            RuntimePlatform host,
            Action<string, string> inspectionHook)
        {
            if (remainingBytes < 0)
            {
                throw new IOException("Export exceeds the cumulative artifact byte inspection bound.");
            }
            if (!SupportsStrongPathAttestation(host))
            {
                throw new PlatformNotSupportedException(
                    "Artifact reads are currently supported only on Windows Editor.");
            }
            return ReadWindowsAttestedArtifact(
                fullPath,
                relativePath,
                remainingBytes,
                inspectionHook);
        }

        private static AttestedArtifactFile ReadWindowsAttestedArtifact(
            string fullPath,
            string relativePath,
            long remainingBytes,
            Action<string, string> inspectionHook)
        {
            using (SafeFileHandle handle = OpenWindowsPathNoFollow(
                       fullPath,
                       directory: false,
                       writable: false))
            {
                WindowsFileIdentity before = ReadWindowsIdentity(handle, fullPath);
                if (before.IsDirectory || before.IsReparsePoint)
                {
                    throw new IOException("Artifact file handle is not a regular non-reparse file: " +
                        relativePath + ".");
                }
                AssertWindowsHandlePath(handle, fullPath);

                using (var stream = new FileStream(
                           handle,
                           FileAccess.Read,
                           ArtifactReadBufferBytes,
                           isAsync: false))
                {
                    inspectionHook?.Invoke("after-open", relativePath);
                    StreamAttestation attestation = ReadBoundedStream(stream, remainingBytes, relativePath);
                    inspectionHook?.Invoke("after-read", relativePath);

                    WindowsFileIdentity after = ReadWindowsIdentity(handle, fullPath);
                    if (!before.Equals(after) || attestation.Length != before.Length)
                    {
                        throw new IOException(
                            "Artifact identity or length drifted while its single attested handle was read: " +
                            relativePath + ".");
                    }

                    return new AttestedArtifactFile(
                        relativePath,
                        attestation.Length,
                        attestation.Sha256,
                        attestation.PrefixBytes,
                        isUnixExecutable: false);
                }
            }
        }

        private static StreamAttestation ReadBoundedStream(
            Stream stream,
            long remainingBytes,
            string relativePath)
        {
            var buffer = new byte[ArtifactReadBufferBytes];
            var prefix = new byte[MaximumStructuralTextBytes];
            int prefixCount = 0;
            long observedBytes = 0;
            using (SHA256 algorithm = SHA256.Create())
            {
                while (true)
                {
                    long allowanceWithProbe = remainingBytes - observedBytes + 1L;
                    if (allowanceWithProbe <= 0)
                    {
                        throw new IOException(
                            "Export exceeds the cumulative artifact byte inspection bound while reading " +
                            relativePath + ".");
                    }
                    int requested = (int)Math.Min(buffer.Length, allowanceWithProbe);
                    int read = stream.Read(buffer, 0, requested);
                    if (read <= 0) break;
                    if (observedBytes > remainingBytes - read)
                    {
                        throw new IOException(
                            "Export exceeds the cumulative artifact byte inspection bound while reading " +
                            relativePath + ".");
                    }

                    int prefixRead = Math.Min(read, prefix.Length - prefixCount);
                    if (prefixRead > 0)
                    {
                        Buffer.BlockCopy(buffer, 0, prefix, prefixCount, prefixRead);
                        prefixCount += prefixRead;
                    }
                    algorithm.TransformBlock(buffer, 0, read, buffer, 0);
                    observedBytes += read;
                }
                algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                if (prefixCount != prefix.Length) Array.Resize(ref prefix, prefixCount);
                return new StreamAttestation(observedBytes, Hex(algorithm.Hash), prefix);
            }
        }

        private sealed class AttestedArtifactFile
        {
            public string RelativePath { get; }
            public long Length { get; }
            public string Sha256 { get; }
            public byte[] PrefixBytes { get; }
            public bool IsUnixExecutable { get; }

            public AttestedArtifactFile(
                string relativePath,
                long length,
                string sha256,
                byte[] prefixBytes,
                bool isUnixExecutable)
            {
                RelativePath = relativePath ?? string.Empty;
                Length = length;
                Sha256 = sha256 ?? string.Empty;
                PrefixBytes = prefixBytes ?? Array.Empty<byte>();
                IsUnixExecutable = isUnixExecutable;
            }
        }

        private sealed class StreamAttestation
        {
            public long Length { get; }
            public string Sha256 { get; }
            public byte[] PrefixBytes { get; }

            public StreamAttestation(long length, string sha256, byte[] prefixBytes)
            {
                Length = length;
                Sha256 = sha256 ?? string.Empty;
                PrefixBytes = prefixBytes ?? Array.Empty<byte>();
            }
        }

        internal static string ReadDirectoryIdentityNoFollow(string directory)
        {
            string normalized = NormalizeFullPath(directory);
            FileAttributes attributes = File.GetAttributes(normalized);
            if ((attributes & FileAttributes.Directory) == 0 || !IsSafeArtifactAttributes(attributes))
            {
                throw new IOException("Path is not a regular non-reparse directory: " + normalized + ".");
            }

            if (SupportsStrongPathAttestation(Application.platform))
            {
                using (SafeFileHandle handle = OpenWindowsPathNoFollow(
                           normalized,
                           directory: true,
                           writable: false))
                {
                    WindowsFileIdentity identity = ReadWindowsIdentity(handle, normalized);
                    if (!identity.IsDirectory || identity.IsReparsePoint)
                    {
                        throw new IOException(
                            "Directory identity handle is not a regular non-reparse directory.");
                    }
                    AssertWindowsHandlePath(handle, normalized);
                    return identity.StableId;
                }
            }

            throw new PlatformNotSupportedException(
                "Strong no-follow directory identity is currently supported only on Windows Editor.");
        }

        internal static IDisposable AcquireDirectoryIdentityLease(
            string directory,
            string expectedIdentity)
        {
            string normalized = NormalizeFullPath(directory);
            if (string.IsNullOrEmpty(expectedIdentity))
            {
                throw new IOException("Expected directory identity is missing.");
            }

            if (!SupportsStrongPathAttestation(Application.platform))
            {
                throw new PlatformNotSupportedException(
                    "Strong no-follow directory leases are currently supported only on Windows Editor.");
            }

            SafeFileHandle handle = OpenWindowsPathNoFollow(
                normalized,
                directory: true,
                writable: false);
            try
            {
                WindowsFileIdentity identity = ReadWindowsIdentity(handle, normalized);
                if (!identity.IsDirectory || identity.IsReparsePoint ||
                    !string.Equals(identity.StableId, expectedIdentity, StringComparison.Ordinal))
                {
                    throw new IOException("Directory identity changed before the guarded mutation.");
                }
                AssertWindowsHandlePath(handle, normalized);
                return new SafeHandleLease(handle);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        private static SafeFileHandle OpenWindowsPathNoFollow(
            string path,
            bool directory,
            bool writable)
        {
            uint desiredAccess = writable ? GenericWrite : (directory ? FileReadAttributes : GenericRead);
            uint share = directory ? FileShareRead | FileShareWrite : FileShareRead;
            uint flags = FileFlagOpenReparsePoint |
                (directory ? FileFlagBackupSemantics : FileFlagSequentialScan);
            SafeFileHandle handle = WindowsCreateFile(
                path,
                desiredAccess,
                share,
                IntPtr.Zero,
                OpenExisting,
                flags,
                IntPtr.Zero);
            if (handle == null || handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle?.Dispose();
                throw new IOException("No-follow open failed for guarded path (Win32 " + error + "): " +
                    path + ".");
            }
            return handle;
        }

        private static WindowsFileIdentity ReadWindowsIdentity(SafeFileHandle handle, string path)
        {
            if (!WindowsGetFileInformationByHandle(handle, out WindowsByHandleFileInformation info))
            {
                throw new IOException("File identity read failed (Win32 " + Marshal.GetLastWin32Error() +
                    "): " + path + ".");
            }
            return new WindowsFileIdentity(info);
        }

        private static void AssertWindowsHandlePath(SafeFileHandle handle, string expectedPath)
        {
            var buffer = new StringBuilder(32768);
            uint length = WindowsGetFinalPathNameByHandle(handle, buffer, buffer.Capacity, 0);
            if (length == 0 || length >= buffer.Capacity)
            {
                throw new IOException("Final path attestation failed (Win32 " +
                    Marshal.GetLastWin32Error() + "): " + expectedPath + ".");
            }
            string actual = buffer.ToString();
            if (actual.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                actual = @"\\" + actual.Substring(8);
            }
            else if (actual.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            {
                actual = actual.Substring(4);
            }
            if (!SamePath(actual, expectedPath))
            {
                throw new IOException("Opened handle resolved to a different path than requested.");
            }
        }

        private sealed class SafeHandleLease : IDisposable
        {
            private SafeFileHandle _handle;

            public SafeHandleLease(SafeFileHandle handle)
            {
                _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            }

            public void Dispose()
            {
                SafeFileHandle handle = _handle;
                _handle = null;
                handle?.Dispose();
            }
        }

        private sealed class GuardedCleanupHandle : IDisposable
        {
            private SafeFileHandle _handle;
            private readonly WindowsFileIdentity _identity;

            public string Path { get; }
            public bool IsDirectory { get; }
            public string StableId => _identity.StableId;

            public GuardedCleanupHandle(
                string path,
                bool isDirectory,
                SafeFileHandle handle,
                WindowsFileIdentity identity)
            {
                Path = path ?? throw new ArgumentNullException(nameof(path));
                IsDirectory = isDirectory;
                _handle = handle ?? throw new ArgumentNullException(nameof(handle));
                _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            }

            public void AssertCurrentIdentity()
            {
                if (_handle == null || _handle.IsClosed || _handle.IsInvalid)
                {
                    throw new IOException("Guarded cleanup handle is unavailable: " + Path + ".");
                }

                WindowsFileIdentity current = ReadWindowsIdentity(_handle, Path);
                if (!string.Equals(current.StableId, _identity.StableId, StringComparison.Ordinal) ||
                    current.IsDirectory != IsDirectory ||
                    current.IsReparsePoint)
                {
                    throw new IOException("Guarded cleanup handle identity changed: " + Path + ".");
                }
                AssertWindowsHandlePath(_handle, Path);
            }

            public void DeleteThroughHandle()
            {
                AssertCurrentIdentity();
                var disposition = new WindowsFileDispositionInformation { DeleteFile = true };
                if (!WindowsSetFileInformationByHandle(
                        _handle,
                        FileDispositionInfo,
                        ref disposition,
                        (uint)Marshal.SizeOf(typeof(WindowsFileDispositionInformation))))
                {
                    throw new IOException(
                        "Guarded handle deletion failed (Win32 " + Marshal.GetLastWin32Error() + "): " +
                        Path + ".");
                }

                SafeFileHandle handle = _handle;
                _handle = null;
                handle.Dispose();
                bool stillExists = IsDirectory ? Directory.Exists(Path) : File.Exists(Path);
                if (stillExists)
                {
                    throw new IOException("Guarded handle deletion did not remove the exact path: " + Path + ".");
                }
            }

            public void Dispose()
            {
                SafeFileHandle handle = _handle;
                _handle = null;
                handle?.Dispose();
            }
        }

        private sealed class WindowsFileIdentity : IEquatable<WindowsFileIdentity>
        {
            public uint Attributes { get; }
            public uint VolumeSerial { get; }
            public uint FileIndexHigh { get; }
            public uint FileIndexLow { get; }
            public long Length { get; }
            public long LastWriteTime { get; }
            public bool IsDirectory => (Attributes & FileAttributeDirectory) != 0;
            public bool IsReparsePoint => (Attributes & FileAttributeReparsePoint) != 0;
            public string StableId => "win:" + VolumeSerial.ToString("x8", CultureInfo.InvariantCulture) +
                ":" + FileIndexHigh.ToString("x8", CultureInfo.InvariantCulture) +
                FileIndexLow.ToString("x8", CultureInfo.InvariantCulture);

            public WindowsFileIdentity(WindowsByHandleFileInformation info)
            {
                Attributes = info.FileAttributes;
                VolumeSerial = info.VolumeSerialNumber;
                FileIndexHigh = info.FileIndexHigh;
                FileIndexLow = info.FileIndexLow;
                Length = ((long)info.FileSizeHigh << 32) | info.FileSizeLow;
                LastWriteTime = ((long)info.LastWriteTimeHigh << 32) | info.LastWriteTimeLow;
            }

            public bool Equals(WindowsFileIdentity other) => other != null &&
                Attributes == other.Attributes &&
                VolumeSerial == other.VolumeSerial &&
                FileIndexHigh == other.FileIndexHigh &&
                FileIndexLow == other.FileIndexLow &&
                Length == other.Length &&
                LastWriteTime == other.LastWriteTime;

            public override bool Equals(object obj) => Equals(obj as WindowsFileIdentity);
            public override int GetHashCode() => StableId.GetHashCode();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsByHandleFileInformation
        {
            public uint FileAttributes;
            public uint CreationTimeLow;
            public uint CreationTimeHigh;
            public uint LastAccessTimeLow;
            public uint LastAccessTimeHigh;
            public uint LastWriteTimeLow;
            public uint LastWriteTimeHigh;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsFileDispositionInformation
        {
            [MarshalAs(UnmanagedType.I1)]
            public bool DeleteFile;
        }

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint DeleteAccess = 0x00010000;
        private const uint FileReadAttributes = 0x00000080;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileFlagSequentialScan = 0x08000000;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileAttributeDirectory = 0x00000010;
        private const uint FileAttributeReparsePoint = 0x00000400;
        private const int FileDispositionInfo = 4;

        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateFileW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern SafeFileHandle WindowsCreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "GetFileInformationByHandle",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WindowsGetFileInformationByHandle(
            SafeFileHandle handle,
            out WindowsByHandleFileInformation information);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "GetFinalPathNameByHandleW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern uint WindowsGetFinalPathNameByHandle(
            SafeFileHandle handle,
            StringBuilder filePath,
            int filePathLength,
            uint flags);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "SetFileInformationByHandle",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WindowsSetFileInformationByHandle(
            SafeFileHandle handle,
            int fileInformationClass,
            ref WindowsFileDispositionInformation fileInformation,
            uint bufferSize);

        private static GuardedCleanupHandle AcquireGuardedCleanupHandle(
            string fullPath,
            bool directory)
        {
            if (!SupportsStrongPathAttestation(Application.platform))
            {
                throw new PlatformNotSupportedException(
                    "Guarded handle cleanup is currently supported only on Windows Editor.");
            }

            SafeFileHandle handle = WindowsCreateFile(
                fullPath,
                FileReadAttributes | DeleteAccess,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOpenReparsePoint |
                    (directory ? FileFlagBackupSemantics : FileFlagSequentialScan),
                IntPtr.Zero);
            if (handle == null || handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle?.Dispose();
                throw new IOException(
                    "No-follow cleanup open failed (Win32 " + error + "): " + fullPath + ".");
            }

            try
            {
                WindowsFileIdentity identity = ReadWindowsIdentity(handle, fullPath);
                if (identity.IsDirectory != directory || identity.IsReparsePoint)
                {
                    throw new IOException(
                        "Guarded cleanup opened an unexpected or reparse object: " + fullPath + ".");
                }
                AssertWindowsHandlePath(handle, fullPath);
                return new GuardedCleanupHandle(fullPath, directory, handle, identity);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }


        internal static void DeleteTreeWithoutFollowingReparsePoints(string directory) =>
            DeleteTreeWithoutFollowingReparsePoints(
                directory,
                MaximumArtifactFiles,
                MaximumArtifactDirectories,
                File.GetAttributes,
                mutationHook: null);

        internal static void DeleteTreeWithoutFollowingReparsePoints(
            string directory,
            int maximumFiles,
            int maximumDirectories,
            Func<string, FileAttributes> attributeReader,
            Action<string, string> mutationHook)
        {
            string root = NormalizeFullPath(directory);
            if (maximumFiles <= 0 || maximumFiles > MaximumArtifactFiles ||
                maximumDirectories <= 0 || maximumDirectories > MaximumArtifactDirectories)
            {
                throw new IOException("Guarded cleanup bounds are outside the production limits.");
            }
            if (attributeReader == null) throw new ArgumentNullException(nameof(attributeReader));

            if (!SupportsStrongPathAttestation(Application.platform))
            {
                throw new PlatformNotSupportedException(
                    "Guarded handle cleanup is currently supported only on Windows Editor.");
            }

            var pending = new Queue<GuardedCleanupHandle>();
            var directories = new List<GuardedCleanupHandle>();
            var files = new List<GuardedCleanupHandle>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                FileAttributes rootAttributes = attributeReader(root);
                if ((rootAttributes & FileAttributes.Directory) == 0 ||
                    !IsSafeArtifactAttributes(rootAttributes))
                {
                    throw new IOException(
                        "Guarded cleanup found a non-directory or reparse root: " + root + ".");
                }

                GuardedCleanupHandle rootHandle = AcquireGuardedCleanupHandle(root, directory: true);
                directories.Add(rootHandle);
                pending.Enqueue(rootHandle);
                paths.Add(root);
                identities.Add(rootHandle.StableId);

                while (pending.Count > 0)
                {
                    GuardedCleanupHandle current = pending.Dequeue();
                    current.AssertCurrentIdentity();
                    foreach (string entry in Directory.EnumerateFileSystemEntries(
                                 current.Path,
                                 "*",
                                 SearchOption.TopDirectoryOnly))
                    {
                        string fullEntry = NormalizeFullPath(entry);
                        if (!IsSameOrDescendant(fullEntry, root) || !paths.Add(fullEntry))
                        {
                            throw new IOException(
                                "Guarded cleanup found an escaped or duplicate case-normalized path.");
                        }

                        FileAttributes attributes = attributeReader(fullEntry);
                        if (!IsSafeArtifactAttributes(attributes))
                        {
                            throw new IOException(
                                "Guarded cleanup refuses descendant reparse entry: " +
                                RelativePath(root, fullEntry) + ".");
                        }

                        bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                        if (isDirectory && directories.Count >= maximumDirectories)
                        {
                            throw new IOException(
                                "Guarded cleanup exceeds the directory inspection bound.");
                        }
                        if (!isDirectory && files.Count >= maximumFiles)
                        {
                            throw new IOException("Guarded cleanup exceeds the file inspection bound.");
                        }

                        GuardedCleanupHandle entryHandle = null;
                        try
                        {
                            entryHandle = AcquireGuardedCleanupHandle(fullEntry, isDirectory);
                            if (!identities.Add(entryHandle.StableId))
                            {
                                throw new IOException(
                                    "Guarded cleanup found a duplicate filesystem identity.");
                            }

                            if (isDirectory)
                            {
                                directories.Add(entryHandle);
                                pending.Enqueue(entryHandle);
                            }
                            else
                            {
                                files.Add(entryHandle);
                            }
                            entryHandle = null;
                        }
                        finally
                        {
                            entryHandle?.Dispose();
                        }
                    }
                }

                mutationHook?.Invoke("after-scan", root);
                foreach (GuardedCleanupHandle file in files
                             .OrderByDescending(item => item.Path.Length)
                             .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
                {
                    mutationHook?.Invoke("before-file-delete", file.Path);
                    FileAttributes attributes = attributeReader(file.Path);
                    if ((attributes & FileAttributes.Directory) != 0 ||
                        !IsSafeArtifactAttributes(attributes))
                    {
                        throw new IOException("Guarded cleanup file identity changed before deletion.");
                    }
                    file.DeleteThroughHandle();
                }

                foreach (GuardedCleanupHandle child in directories
                             .OrderByDescending(item => item.Path.Count(character =>
                                 character == Path.DirectorySeparatorChar ||
                                 character == Path.AltDirectorySeparatorChar))
                             .ThenByDescending(item => item.Path.Length)
                             .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
                {
                    mutationHook?.Invoke("before-directory-delete", child.Path);
                    FileAttributes attributes = attributeReader(child.Path);
                    if ((attributes & FileAttributes.Directory) == 0 ||
                        !IsSafeArtifactAttributes(attributes))
                    {
                        throw new IOException("Guarded cleanup directory identity changed before deletion.");
                    }
                    // Mark the already-attested exact handle, then close it. A newly introduced
                    // descendant makes the directory disposition fail; there is no recursive or
                    // path-based fallback.
                    child.DeleteThroughHandle();
                }
            }
            finally
            {
                foreach (GuardedCleanupHandle file in files) file.Dispose();
                foreach (GuardedCleanupHandle child in directories) child.Dispose();
            }
        }

        internal static bool IsGuardedOutputDirectory(string projectRoot, string candidate)
        {
            string root = TryNormalize(projectRoot);
            string path = TryNormalize(candidate);
            if (root.Length == 0 || path.Length == 0) return false;
            string expected = TryNormalize(Path.Combine(root, "Builds", "AndroidExport"));
            string assets = TryNormalize(Path.Combine(root, "Assets"));
            return SamePath(path, expected) && !IsSameOrDescendant(path, assets);
        }

        internal static bool IsGuardedSummaryPath(string projectRoot, string candidate)
        {
            string root = TryNormalize(projectRoot);
            string path = TryNormalize(candidate);
            if (root.Length == 0 || path.Length == 0) return false;
            string expected = TryNormalize(Path.Combine(
                root,
                "Logs",
                "AndroidUnityLibraryExportSummary.json"));
            string assets = TryNormalize(Path.Combine(root, "Assets"));
            return SamePath(path, expected) && !IsSameOrDescendant(path, assets);
        }

        internal static void WriteAllTextGuarded(
            string projectRoot,
            string fullPath,
            string contents,
            Func<string, bool> isIgnored,
            Func<string, bool> isTracked,
            Func<string, bool> hasReparsePoint,
            Action<string, string> mutationHook)
        {
            if (isIgnored == null) throw new ArgumentNullException(nameof(isIgnored));
            if (isTracked == null) throw new ArgumentNullException(nameof(isTracked));
            if (hasReparsePoint == null) throw new ArgumentNullException(nameof(hasReparsePoint));
            string root = NormalizeFullPath(projectRoot);
            string destination = NormalizeFullPath(fullPath);
            AssertGuardedSummaryDestination(
                root,
                destination,
                isIgnored,
                isTracked,
                hasReparsePoint);
            string parent = NormalizeFullPath(Path.GetDirectoryName(destination) ?? root);
            Directory.CreateDirectory(parent);
            string parentIdentity = ReadDirectoryIdentityNoFollow(parent);
            mutationHook?.Invoke("after-parent-attest", destination);
            AssertGuardedSummaryDestination(
                root,
                destination,
                isIgnored,
                isTracked,
                hasReparsePoint);

            string temporary = destination + ".tmp-" + Process.GetCurrentProcess().Id + "-" +
                Guid.NewGuid().ToString("N");
            byte[] payload = new UTF8Encoding(false).GetBytes(contents ?? string.Empty);
            try
            {
                using (IDisposable parentLease = AcquireDirectoryIdentityLease(parent, parentIdentity))
                {
                    mutationHook?.Invoke("before-temporary-create", destination);
                    AssertGuardedSummaryDestination(
                        root,
                        destination,
                        isIgnored,
                        isTracked,
                        hasReparsePoint);
                    if (!SamePath(Path.GetDirectoryName(temporary), parent) ||
                        File.Exists(temporary) ||
                        Directory.Exists(temporary) ||
                        hasReparsePoint(temporary))
                    {
                        throw new IOException("Refusing to create an unsafe export-summary temporary file.");
                    }

                    using (var stream = new FileStream(
                               temporary,
                               FileMode.CreateNew,
                               FileAccess.Write,
                               FileShare.None))
                    {
                        stream.Write(payload, 0, payload.Length);
                        stream.Flush(flushToDisk: true);
                    }

                    mutationHook?.Invoke("before-commit", destination);
                    AssertGuardedSummaryDestination(
                        root,
                        destination,
                        isIgnored,
                        isTracked,
                        hasReparsePoint);
                    if (!string.Equals(
                            ReadDirectoryIdentityNoFollow(parent),
                            parentIdentity,
                            StringComparison.Ordinal))
                    {
                        throw new IOException("Export-summary parent identity changed before commit.");
                    }
                    FileAttributes temporaryAttributes = File.GetAttributes(temporary);
                    if ((temporaryAttributes & FileAttributes.Directory) != 0 ||
                        !IsSafeArtifactAttributes(temporaryAttributes))
                    {
                        throw new IOException("Export-summary temporary path is not a regular file.");
                    }
                    if (File.Exists(destination))
                    {
                        FileAttributes destinationAttributes = File.GetAttributes(destination);
                        if ((destinationAttributes & FileAttributes.Directory) != 0 ||
                            !IsSafeArtifactAttributes(destinationAttributes))
                        {
                            throw new IOException("Export-summary destination is not a regular file.");
                        }
                        File.Replace(temporary, destination, null);
                    }
                    else
                    {
                        File.Move(temporary, destination);
                    }
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    FileAttributes attributes = File.GetAttributes(temporary);
                    if ((attributes & FileAttributes.Directory) == 0 &&
                        IsSafeArtifactAttributes(attributes) &&
                        SamePath(Path.GetDirectoryName(temporary), parent))
                    {
                        File.Delete(temporary);
                    }
                }
            }
        }

        private static void AssertGuardedSummaryDestination(
            string projectRoot,
            string destination,
            Func<string, bool> isIgnored,
            Func<string, bool> isTracked,
            Func<string, bool> hasReparsePoint)
        {
            if (!IsGuardedSummaryPath(projectRoot, destination) ||
                !isIgnored(destination) ||
                isTracked(destination) ||
                hasReparsePoint(destination))
            {
                throw new IOException("Refusing to write an unguarded Android export summary.");
            }
        }

        internal static string DescribeBuildSettingsMismatch(
            AndroidUnityLibraryBuildSettingsSnapshot expected,
            AndroidUnityLibraryBuildSettingsSnapshot actual)
        {
            if (expected == null) return "Original build settings snapshot is missing.";
            if (actual == null) return "Post-restore build settings recapture is missing.";
            var mismatches = new List<string>();
            if (!string.Equals(
                    expected.ScriptingBackend,
                    actual.ScriptingBackend,
                    StringComparison.Ordinal))
            {
                mismatches.Add("backend");
            }
            if (!string.Equals(
                    expected.TargetArchitectures,
                    actual.TargetArchitectures,
                    StringComparison.Ordinal))
            {
                mismatches.Add("architectures");
            }
            if (expected.MinimumApiLevel != actual.MinimumApiLevel)
            {
                mismatches.Add("minimum-api");
            }
            if (expected.ExportAsGoogleAndroidProject != actual.ExportAsGoogleAndroidProject)
            {
                mismatches.Add("export-project");
            }
            if (expected.BuildAppBundle != actual.BuildAppBundle)
            {
                mismatches.Add("app-bundle");
            }
            return mismatches.Count == 0
                ? string.Empty
                : "Post-restore build settings mismatch: " + string.Join(", ", mismatches) + ".";
        }

        internal static string SerializeSummary(AndroidUnityLibraryExportSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            var json = new StringBuilder(1024);
            json.Append("{\n");
            AppendJsonProperty(json, "status", summary.Status.ToString(), comma: true);
            AppendJsonProperty(json, "target", summary.Target, comma: true);
            AppendJsonProperty(json, "unityVersion", summary.UnityVersion, comma: true);
            AppendJsonProperty(json, "scriptingBackend", summary.ScriptingBackend, comma: true);
            AppendJsonProperty(json, "targetArchitectures", summary.TargetArchitectures, comma: true);
            AppendJsonNumber(json, "minimumApiLevel", summary.MinimumApiLevel.ToString(CultureInfo.InvariantCulture), true);
            AppendJsonProperty(json, "outputDirectory", summary.OutputDirectory, comma: true);
            AppendJsonArray(json, "scenePaths", summary.ScenePaths, comma: true);
            AppendJsonProperty(json, "startedAtUtc", FormatUtc(summary.StartedAtUtc), comma: true);
            AppendJsonProperty(json, "endedAtUtc", FormatUtc(summary.EndedAtUtc), comma: true);
            AppendJsonProperty(json, "totalTime", summary.TotalTime.ToString("c", CultureInfo.InvariantCulture), true);
            AppendJsonNumber(json, "totalSize", summary.TotalSize.ToString(CultureInfo.InvariantCulture), true);
            AppendJsonNumber(json, "warningCount", summary.WarningCount.ToString(CultureInfo.InvariantCulture), true);
            AppendJsonNumber(json, "errorCount", summary.ErrorCount.ToString(CultureInfo.InvariantCulture), true);
            AppendJsonProperty(json, "buildResult", summary.BuildResult, comma: true);
            AppendJsonNumber(json, "artifactFileCount", summary.ArtifactFileCount.ToString(CultureInfo.InvariantCulture), true);
            AppendJsonNumber(json, "artifactBytes", summary.ArtifactBytes.ToString(CultureInfo.InvariantCulture), true);
            AppendJsonProperty(json, "inventorySha256", summary.InventorySha256, comma: true);
            AppendJsonArray(json, "abiDirectories", summary.AbiDirectories, comma: true);
            AppendJsonProperty(json, "summaryMessage", summary.SummaryMessage, comma: false);
            json.Append("}\n");
            return json.ToString();
        }

        private static AndroidUnityLibraryExportSummary FinalizeSummary(
            IAndroidUnityLibraryExportEnvironment environment,
            AndroidUnityLibraryExportPlan plan,
            AndroidUnityLibraryExportSummary summary)
        {
            if (plan == null || !plan.CanWriteSummary)
            {
                Debug.LogError("[AL-ANDROID-UNITY-EXPORT-SUMMARY-NOT-WRITTEN] " + summary.Summarize());
                return summary;
            }

            try
            {
                environment.WriteAllText(plan.SummaryPath, SerializeSummary(summary));
                Debug.Log("[AL-ANDROID-UNITY-EXPORT-SUMMARY-PATH] " + plan.SummaryPath);
                return summary;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AL-ANDROID-UNITY-EXPORT-SUMMARY-WRITE-FAILED] " + exception);
                return AndroidUnityLibraryExportSummary.SummaryWriteFailure(
                    summary,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static DateTime SafeUtcNow(IAndroidUnityLibraryExportEnvironment environment)
        {
            try { return ToUtc(environment.UtcNow()); }
            catch { return DateTime.UtcNow; }
        }

        private static bool SafeBoolean(Func<bool> read)
        {
            try { return read(); }
            catch { return false; }
        }

        private static bool SafeBooleanFailureClosed(Func<bool> read)
        {
            try { return read(); }
            catch { return true; }
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
            string full = Path.GetFullPath(path);
            string volumeRoot = Path.GetPathRoot(full) ?? string.Empty;
            return full.Length > volumeRoot.Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }

        private static string TryNormalize(string path)
        {
            try { return NormalizeFullPath(path); }
            catch { return string.Empty; }
        }

        private static bool SamePath(string left, string right)
        {
            string normalizedLeft = TryNormalize(left);
            string normalizedRight = TryNormalize(right);
            return normalizedLeft.Length > 0 && normalizedRight.Length > 0 &&
                string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameOrDescendant(string candidate, string parent)
        {
            string path = TryNormalize(candidate);
            string root = TryNormalize(parent);
            if (path.Length == 0 || root.Length == 0) return false;
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string RelativePath(string root, string fullPath)
        {
            string normalizedRoot = NormalizeFullPath(root);
            string normalizedPath = NormalizeFullPath(fullPath);
            if (!IsSameOrDescendant(normalizedPath, normalizedRoot))
            {
                throw new IOException("Artifact escaped the export root.");
            }
            return normalizedPath.Substring(normalizedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string ComputeSha256(byte[] payload)
        {
            using (var algorithm = SHA256.Create()) return Hex(algorithm.ComputeHash(payload));
        }

        private static string Hex(IEnumerable<byte> bytes) => string.Concat(
            bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

        private static bool IsLowerHexSha256(string value) =>
            value != null && value.Length == 64 && value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.ToUniversalTime();
        }

        private static string FormatUtc(DateTime value) =>
            ToUtc(value).ToString("O", CultureInfo.InvariantCulture);

        private static void AppendJsonProperty(StringBuilder json, string name, string value, bool comma)
        {
            json.Append("  \"").Append(name).Append("\": \"")
                .Append(EscapeJson(value ?? string.Empty)).Append('"')
                .Append(comma ? ",\n" : "\n");
        }

        private static void AppendJsonNumber(StringBuilder json, string name, string value, bool comma)
        {
            json.Append("  \"").Append(name).Append("\": ").Append(value)
                .Append(comma ? ",\n" : "\n");
        }

        private static void AppendJsonArray(
            StringBuilder json,
            string name,
            IReadOnlyList<string> values,
            bool comma)
        {
            json.Append("  \"").Append(name).Append("\": [");
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0) json.Append(',');
                json.Append("\n    \"").Append(EscapeJson(values[index] ?? string.Empty)).Append('"');
            }
            if (values.Count > 0) json.Append('\n').Append("  ");
            json.Append(']').Append(comma ? ",\n" : "\n");
        }

        private static string EscapeJson(string value)
        {
            var escaped = new StringBuilder(value?.Length ?? 0);
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': escaped.Append("\\\""); break;
                    case '\\': escaped.Append("\\\\"); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            escaped.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else escaped.Append(character);
                        break;
                }
            }
            return escaped.ToString();
        }
    }

    internal sealed class AndroidUnityLibraryExportPlan
    {
        public string ProjectRoot { get; }
        public string OutputDirectory { get; }
        public string SummaryPath { get; }
        public string UnityVersion { get; }
        public BuildTarget Target => BuildTarget.Android;
        public string ScriptingBackend => AndroidUnityLibraryExporter.RequiredScriptingBackend;
        public string TargetArchitectures => AndroidUnityLibraryExporter.RequiredTargetArchitectures;
        public int MinimumApiLevel => AndroidUnityLibraryExporter.RequiredMinimumApiLevel;
        public IReadOnlyList<string> ScenePaths { get; }
        public IReadOnlyList<string> Failures { get; }
        public bool IsValid => Failures.Count == 0;
        public bool CanWriteSummary { get; }

        internal AndroidUnityLibraryExportPlan(
            string projectRoot,
            string outputDirectory,
            string summaryPath,
            string unityVersion,
            IEnumerable<string> scenes,
            IEnumerable<string> failures,
            bool canWriteSummary)
        {
            ProjectRoot = projectRoot ?? string.Empty;
            OutputDirectory = outputDirectory ?? string.Empty;
            SummaryPath = summaryPath ?? string.Empty;
            UnityVersion = unityVersion ?? string.Empty;
            ScenePaths = new ReadOnlyCollection<string>((scenes ?? Array.Empty<string>()).ToArray());
            Failures = new ReadOnlyCollection<string>((failures ?? Array.Empty<string>()).ToArray());
            CanWriteSummary = canWriteSummary;
        }

        public BuildPlayerOptions CreateBuildPlayerOptions() => new BuildPlayerOptions
        {
            scenes = ScenePaths.ToArray(),
            locationPathName = OutputDirectory,
            target = BuildTarget.Android,
            options = BuildOptions.Development | BuildOptions.AcceptExternalModificationsToPlayer
        };

        public string SummarizeFailures() => Failures.Count == 0
            ? "No preflight failures."
            : string.Join(" | ", Failures);
    }

    internal sealed class AndroidUnityLibraryBuildSettingsSnapshot
    {
        public string ScriptingBackend { get; }
        public string TargetArchitectures { get; }
        public int MinimumApiLevel { get; }
        public bool ExportAsGoogleAndroidProject { get; }
        public bool BuildAppBundle { get; }

        internal AndroidUnityLibraryBuildSettingsSnapshot(
            string scriptingBackend,
            string targetArchitectures,
            int minimumApiLevel,
            bool exportAsGoogleAndroidProject,
            bool buildAppBundle)
        {
            ScriptingBackend = scriptingBackend ?? string.Empty;
            TargetArchitectures = targetArchitectures ?? string.Empty;
            MinimumApiLevel = minimumApiLevel;
            ExportAsGoogleAndroidProject = exportAsGoogleAndroidProject;
            BuildAppBundle = buildAppBundle;
        }
    }

    internal sealed class AndroidUnityLibraryBuildReportSnapshot
    {
        public string Result { get; }
        public string Target { get; }
        public string OutputPath { get; }
        public TimeSpan TotalTime { get; }
        public ulong TotalSize { get; }
        public int WarningCount { get; }
        public int ErrorCount { get; }
        public string ReportSummary { get; }

        internal AndroidUnityLibraryBuildReportSnapshot(
            string result,
            string target,
            string outputPath,
            TimeSpan totalTime,
            ulong totalSize,
            int warningCount,
            int errorCount,
            string reportSummary)
        {
            Result = result ?? string.Empty;
            Target = target ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            TotalTime = totalTime;
            TotalSize = totalSize;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            ReportSummary = reportSummary ?? string.Empty;
        }

        public static AndroidUnityLibraryBuildReportSnapshot NotRun(
            string target,
            string output,
            string summary) => new AndroidUnityLibraryBuildReportSnapshot(
                "NotRun", target, output, TimeSpan.Zero, 0UL, 0, 1, summary);

        public static AndroidUnityLibraryBuildReportSnapshot Exception(
            string target,
            string output,
            string summary) => new AndroidUnityLibraryBuildReportSnapshot(
                "Exception", target, output, TimeSpan.Zero, 0UL, 0, 1, summary);
    }

    internal sealed class AndroidUnityLibraryArtifactSnapshot
    {
        public bool Inspected { get; }
        public int FileCount { get; }
        public long TotalBytes { get; }
        public string InventorySha256 { get; }
        public IReadOnlyList<string> MissingArtifacts { get; }
        public IReadOnlyList<string> InvalidArtifacts { get; }
        public IReadOnlyList<string> AbiDirectories { get; }
        public string Summary { get; }
        public bool IsValid =>
            Inspected &&
            FileCount > 0 && FileCount <= AndroidUnityLibraryExporter.MaximumArtifactFiles &&
            TotalBytes > 0 && TotalBytes <= AndroidUnityLibraryExporter.MaximumArtifactBytes &&
            MissingArtifacts.Count == 0 &&
            InvalidArtifacts.Count == 0 &&
            AbiDirectories.SequenceEqual(new[] { "arm64-v8a" }, StringComparer.Ordinal) &&
            IsHash(InventorySha256);

        internal AndroidUnityLibraryArtifactSnapshot(
            bool inspected,
            int fileCount,
            long totalBytes,
            string inventorySha256,
            IEnumerable<string> missingArtifacts,
            IEnumerable<string> invalidArtifacts,
            IEnumerable<string> abiDirectories,
            string summary)
        {
            Inspected = inspected;
            FileCount = fileCount;
            TotalBytes = totalBytes;
            InventorySha256 = inventorySha256 ?? string.Empty;
            MissingArtifacts = new ReadOnlyCollection<string>(
                (missingArtifacts ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            InvalidArtifacts = new ReadOnlyCollection<string>(
                (invalidArtifacts ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            AbiDirectories = new ReadOnlyCollection<string>(
                (abiDirectories ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            Summary = summary ?? string.Empty;
        }

        public static AndroidUnityLibraryArtifactSnapshot NotInspected(string summary) =>
            new AndroidUnityLibraryArtifactSnapshot(
                false,
                0,
                0,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                summary);

        private static bool IsHash(string value) => value != null && value.Length == 64 &&
            value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));
    }

    internal enum AndroidUnityLibraryExportStatus
    {
        PreflightFailed,
        PreparationFailed,
        BuildFailed,
        SettingsRestoreFailed,
        ArtifactsMissing,
        SummaryWriteFailed,
        Succeeded
    }

    internal sealed class AndroidUnityLibraryExportSummary
    {
        public AndroidUnityLibraryExportStatus Status { get; }
        public string Target { get; }
        public string UnityVersion { get; }
        public string ScriptingBackend { get; }
        public string TargetArchitectures { get; }
        public int MinimumApiLevel { get; }
        public string OutputDirectory { get; }
        public IReadOnlyList<string> ScenePaths { get; }
        public DateTime StartedAtUtc { get; }
        public DateTime EndedAtUtc { get; }
        public TimeSpan TotalTime => EndedAtUtc - StartedAtUtc;
        public ulong TotalSize { get; }
        public int WarningCount { get; }
        public int ErrorCount { get; }
        public string BuildResult { get; }
        public int ArtifactFileCount { get; }
        public long ArtifactBytes { get; }
        public string InventorySha256 { get; }
        public IReadOnlyList<string> AbiDirectories { get; }
        public string SummaryMessage { get; }
        public bool Succeeded => Status == AndroidUnityLibraryExportStatus.Succeeded;

        private AndroidUnityLibraryExportSummary(
            AndroidUnityLibraryExportStatus status,
            string target,
            string unityVersion,
            string outputDirectory,
            IEnumerable<string> scenes,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            AndroidUnityLibraryBuildReportSnapshot report,
            AndroidUnityLibraryArtifactSnapshot artifacts,
            string summaryMessage)
        {
            Status = status;
            Target = target ?? string.Empty;
            UnityVersion = unityVersion ?? string.Empty;
            ScriptingBackend = AndroidUnityLibraryExporter.RequiredScriptingBackend;
            TargetArchitectures = AndroidUnityLibraryExporter.RequiredTargetArchitectures;
            MinimumApiLevel = AndroidUnityLibraryExporter.RequiredMinimumApiLevel;
            OutputDirectory = outputDirectory ?? string.Empty;
            ScenePaths = new ReadOnlyCollection<string>((scenes ?? Array.Empty<string>()).ToArray());
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            TotalSize = report?.TotalSize ?? 0UL;
            WarningCount = report?.WarningCount ?? 0;
            ErrorCount = report?.ErrorCount ?? 0;
            BuildResult = report?.Result ?? "NotRun";
            ArtifactFileCount = artifacts?.FileCount ?? 0;
            ArtifactBytes = artifacts?.TotalBytes ?? 0L;
            InventorySha256 = artifacts?.InventorySha256 ?? string.Empty;
            AbiDirectories = new ReadOnlyCollection<string>(
                (artifacts?.AbiDirectories ?? Array.Empty<string>()).ToArray());
            SummaryMessage = summaryMessage ?? string.Empty;
        }

        public string Summarize() => Status + "; target=" + Target + "; output=" +
            OutputDirectory + "; result=" + BuildResult + "; artifacts=" + ArtifactFileCount +
            "; bytes=" + ArtifactBytes + "; message=" + SummaryMessage;

        public static AndroidUnityLibraryExportSummary PreflightFailure(
            string output,
            string target,
            IEnumerable<string> scenes,
            string unityVersion,
            DateTime started,
            DateTime ended,
            string message) => new AndroidUnityLibraryExportSummary(
                AndroidUnityLibraryExportStatus.PreflightFailed,
                target,
                unityVersion,
                output,
                scenes,
                started,
                ended,
                null,
                null,
                message);

        public static AndroidUnityLibraryExportSummary PreparationFailure(
            AndroidUnityLibraryExportPlan plan,
            DateTime started,
            DateTime ended,
            string message) => FromPlan(
                AndroidUnityLibraryExportStatus.PreparationFailed,
                plan,
                null,
                null,
                started,
                ended,
                message);

        public static AndroidUnityLibraryExportSummary BuildFailure(
            AndroidUnityLibraryExportPlan plan,
            AndroidUnityLibraryBuildReportSnapshot report,
            AndroidUnityLibraryArtifactSnapshot artifacts,
            DateTime started,
            DateTime ended,
            string message) => FromPlan(
                AndroidUnityLibraryExportStatus.BuildFailed,
                plan,
                report,
                artifacts,
                started,
                ended,
                message);

        public static AndroidUnityLibraryExportSummary SettingsRestoreFailure(
            AndroidUnityLibraryExportPlan plan,
            AndroidUnityLibraryBuildReportSnapshot report,
            AndroidUnityLibraryArtifactSnapshot artifacts,
            DateTime started,
            DateTime ended,
            string message) => FromPlan(
                AndroidUnityLibraryExportStatus.SettingsRestoreFailed,
                plan,
                report,
                artifacts,
                started,
                ended,
                message);

        public static AndroidUnityLibraryExportSummary ArtifactFailure(
            AndroidUnityLibraryExportPlan plan,
            AndroidUnityLibraryBuildReportSnapshot report,
            AndroidUnityLibraryArtifactSnapshot artifacts,
            DateTime started,
            DateTime ended,
            string message) => FromPlan(
                AndroidUnityLibraryExportStatus.ArtifactsMissing,
                plan,
                report,
                artifacts,
                started,
                ended,
                message);

        public static AndroidUnityLibraryExportSummary Success(
            AndroidUnityLibraryExportPlan plan,
            AndroidUnityLibraryBuildReportSnapshot report,
            AndroidUnityLibraryArtifactSnapshot artifacts,
            DateTime started,
            DateTime ended) => FromPlan(
                AndroidUnityLibraryExportStatus.Succeeded,
                plan,
                report,
                artifacts,
                started,
                ended,
                "Guarded Android unityLibrary export completed and passed structural verification.");

        public static AndroidUnityLibraryExportSummary SummaryWriteFailure(
            AndroidUnityLibraryExportSummary source,
            string message) => new AndroidUnityLibraryExportSummary(
                AndroidUnityLibraryExportStatus.SummaryWriteFailed,
                source.Target,
                source.UnityVersion,
                source.OutputDirectory,
                source.ScenePaths,
                source.StartedAtUtc,
                source.EndedAtUtc,
                new AndroidUnityLibraryBuildReportSnapshot(
                    source.BuildResult,
                    source.Target,
                    source.OutputDirectory,
                    source.TotalTime,
                    source.TotalSize,
                    source.WarningCount,
                    Math.Max(1, source.ErrorCount),
                    source.SummaryMessage),
                new AndroidUnityLibraryArtifactSnapshot(
                    source.ArtifactFileCount > 0,
                    source.ArtifactFileCount,
                    source.ArtifactBytes,
                    source.InventorySha256,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    source.AbiDirectories,
                    source.SummaryMessage),
                "Summary write failed: " + message);

        private static AndroidUnityLibraryExportSummary FromPlan(
            AndroidUnityLibraryExportStatus status,
            AndroidUnityLibraryExportPlan plan,
            AndroidUnityLibraryBuildReportSnapshot report,
            AndroidUnityLibraryArtifactSnapshot artifacts,
            DateTime started,
            DateTime ended,
            string message) => new AndroidUnityLibraryExportSummary(
                status,
                plan.Target.ToString(),
                plan.UnityVersion,
                plan.OutputDirectory,
                plan.ScenePaths,
                started,
                ended,
                report,
                artifacts,
                message);
    }

    internal interface IAndroidUnityLibraryExportEnvironment
    {
        string ProjectRoot { get; }
        string UnityVersion { get; }
        bool IsCompiling { get; }
        bool HasCompilationErrors { get; }
        bool IsAndroidBuildSupported { get; }
        bool IsAndroidActiveBuildTarget { get; }
        bool IsStrongPathAttestationSupported { get; }
        DateTime UtcNow();
        BuildValidationSnapshot ValidateCurrentShellFoundation();
        bool IsIgnoredPath(string fullPath);
        bool IsTrackedPath(string fullPath);
        bool HasReparsePoint(string fullPath);
        bool DirectoryExists(string fullPath);
        void DeleteDirectory(string fullPath);
        void CreateDirectory(string fullPath);
        string AttestOutputDirectory(string fullPath);
        IDisposable AcquireOutputMutationLease(string fullPath, string expectedIdentity);
        AndroidUnityLibraryBuildSettingsSnapshot CaptureBuildSettings();
        void ApplyRequiredBuildSettings();
        void RestoreBuildSettings(AndroidUnityLibraryBuildSettingsSnapshot snapshot);
        AndroidUnityLibraryBuildReportSnapshot BuildPlayer(BuildPlayerOptions options);
        AndroidUnityLibraryArtifactSnapshot InspectExport(string outputDirectory);
        void WriteAllText(string fullPath, string contents);
    }

    internal sealed class UnityAndroidUnityLibraryExportEnvironment :
        IAndroidUnityLibraryExportEnvironment
    {
        public string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        public string UnityVersion => Application.unityVersion;
        public bool IsCompiling => EditorApplication.isCompiling;
        public bool HasCompilationErrors => EditorUtility.scriptCompilationFailed;
        public bool IsAndroidBuildSupported => BuildPipeline.IsBuildTargetSupported(
            BuildTargetGroup.Android,
            BuildTarget.Android);
        public bool IsAndroidActiveBuildTarget =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        public bool IsStrongPathAttestationSupported =>
            AndroidUnityLibraryExporter.SupportsStrongPathAttestation(Application.platform);

        public DateTime UtcNow() => DateTime.UtcNow;

        public BuildValidationSnapshot ValidateCurrentShellFoundation()
        {
            var report = ProductionBuildSettingsValidator.ValidateCurrentShellFoundation();
            return report == null
                ? BuildValidationSnapshot.Invalid("ProductionBuildSettingsValidator returned no report.")
                : new BuildValidationSnapshot(report.IsValid, report.Summarize());
        }

        public bool IsIgnoredPath(string fullPath)
        {
            GitResult result = RunGitForPath("check-ignore --no-index -q -- ", fullPath);
            return result.ExitCode == 0;
        }

        public bool IsTrackedPath(string fullPath)
        {
            GitResult result = RunGitForPath("ls-files -- ", fullPath);
            return result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.StandardOutput);
        }

        public bool HasReparsePoint(string fullPath)
        {
            string root = Normalize(ProjectRoot);
            string candidate = Normalize(fullPath);
            if (!DescendantOrSame(candidate, root)) return true;
            string cursor = candidate;
            while (!string.IsNullOrEmpty(cursor))
            {
                try
                {
                    if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) return true;
                }
                catch (Exception exception) when (
                    exception is FileNotFoundException ||
                    exception is DirectoryNotFoundException)
                {
                    // Planned leafs can be absent; their first existing ancestor is still checked.
                }
                catch
                {
                    return true;
                }

                if (string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase)) return false;
                string parent = Path.GetDirectoryName(cursor);
                if (string.IsNullOrEmpty(parent) ||
                    string.Equals(parent, cursor, StringComparison.OrdinalIgnoreCase) ||
                    !DescendantOrSame(parent, root)) return true;
                cursor = Normalize(parent);
            }
            return true;
        }

        public bool DirectoryExists(string fullPath) => Directory.Exists(fullPath);

        public void DeleteDirectory(string fullPath)
        {
            string root = Normalize(ProjectRoot);
            string candidate = Normalize(fullPath);
            if (!AndroidUnityLibraryExporter.IsGuardedOutputDirectory(root, candidate) ||
                !IsIgnoredPath(candidate) ||
                IsTrackedPath(candidate) ||
                HasReparsePoint(candidate))
            {
                throw new IOException("Refusing cleanup outside the exact ignored, untracked, non-reparse export path.");
            }
            AndroidUnityLibraryExporter.DeleteTreeWithoutFollowingReparsePoints(candidate);
        }

        public void CreateDirectory(string fullPath)
        {
            if (!AndroidUnityLibraryExporter.IsGuardedOutputDirectory(ProjectRoot, fullPath))
            {
                throw new IOException("Refusing to create an unguarded Android export directory.");
            }
            Directory.CreateDirectory(fullPath);
        }

        public string AttestOutputDirectory(string fullPath)
        {
            string candidate = Normalize(fullPath);
            AssertGuardedOutputDirectory(candidate);
            return AndroidUnityLibraryExporter.ReadDirectoryIdentityNoFollow(candidate);
        }

        public IDisposable AcquireOutputMutationLease(string fullPath, string expectedIdentity)
        {
            string candidate = Normalize(fullPath);
            AssertGuardedOutputDirectory(candidate);
            return AndroidUnityLibraryExporter.AcquireDirectoryIdentityLease(
                candidate,
                expectedIdentity);
        }

        public AndroidUnityLibraryBuildSettingsSnapshot CaptureBuildSettings() =>
            new AndroidUnityLibraryBuildSettingsSnapshot(
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString(),
                PlayerSettings.Android.targetArchitectures.ToString(),
                (int)PlayerSettings.Android.minSdkVersion,
                EditorUserBuildSettings.exportAsGoogleAndroidProject,
                EditorUserBuildSettings.buildAppBundle);

        public void ApplyRequiredBuildSettings()
        {
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        public void RestoreBuildSettings(AndroidUnityLibraryBuildSettingsSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!Enum.TryParse(snapshot.ScriptingBackend, out ScriptingImplementation backend) ||
                !Enum.TryParse(snapshot.TargetArchitectures, out AndroidArchitecture architectures) ||
                !Enum.IsDefined(typeof(AndroidSdkVersions), snapshot.MinimumApiLevel))
            {
                throw new InvalidOperationException("Captured Android build settings cannot be restored exactly.");
            }

            // Restore every independent setting even if an earlier setter throws. The caller must
            // receive a failure, but one failed setter must not strand the remaining temporary
            // export flags in their applied state.
            var failures = new List<string>();
            TryRestore("scriptingBackend", () =>
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend), failures);
            TryRestore("targetArchitectures", () =>
                PlayerSettings.Android.targetArchitectures = architectures, failures);
            TryRestore("minimumApiLevel", () =>
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)snapshot.MinimumApiLevel,
                failures);
            TryRestore("exportAsGoogleAndroidProject", () =>
                EditorUserBuildSettings.exportAsGoogleAndroidProject =
                    snapshot.ExportAsGoogleAndroidProject, failures);
            TryRestore("buildAppBundle", () =>
                EditorUserBuildSettings.buildAppBundle = snapshot.BuildAppBundle, failures);
            TryRestore("saveAssets", AssetDatabase.SaveAssets, failures);
            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "One or more Android build settings could not be restored: " +
                    string.Join(" | ", failures));
            }
        }

        public AndroidUnityLibraryBuildReportSnapshot BuildPlayer(BuildPlayerOptions options)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null) return null;
            BuildSummary summary = report.summary;
            return new AndroidUnityLibraryBuildReportSnapshot(
                summary.result.ToString(),
                summary.platform.ToString(),
                summary.outputPath,
                summary.totalTime,
                summary.totalSize,
                summary.totalWarnings,
                summary.totalErrors,
                "result=" + summary.result + "; target=" + summary.platform +
                "; output=" + summary.outputPath + "; warnings=" + summary.totalWarnings +
                "; errors=" + summary.totalErrors + ".");
        }

        public AndroidUnityLibraryArtifactSnapshot InspectExport(string outputDirectory) =>
            AndroidUnityLibraryExporter.InspectExportTree(outputDirectory);

        public void WriteAllText(string fullPath, string contents)
        {
            AndroidUnityLibraryExporter.WriteAllTextGuarded(
                ProjectRoot,
                fullPath,
                contents,
                IsIgnoredPath,
                IsTrackedPath,
                HasReparsePoint,
                mutationHook: null);
        }

        private void AssertGuardedOutputDirectory(string candidate)
        {
            if (!AndroidUnityLibraryExporter.IsGuardedOutputDirectory(ProjectRoot, candidate) ||
                !Directory.Exists(candidate) ||
                !IsIgnoredPath(candidate) ||
                IsTrackedPath(candidate) ||
                HasReparsePoint(candidate))
            {
                throw new IOException(
                    "Guarded Android export output changed identity or policy before mutation.");
            }
        }

        private GitResult RunGitForPath(string command, string fullPath)
        {
            string projectRoot = Normalize(ProjectRoot);
            string repositoryRoot = FindRepositoryRoot(projectRoot);
            if (repositoryRoot.Length == 0 || !DescendantOrSame(fullPath, repositoryRoot))
            {
                return new GitResult(-1, string.Empty);
            }
            string relative = Normalize(fullPath).Substring(repositoryRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
            return RunGit(repositoryRoot, command + Quote(relative));
        }

        private static void TryRestore(string name, Action restore, ICollection<string> failures)
        {
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                failures.Add(name + "=" + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static string FindRepositoryRoot(string start)
        {
            var cursor = new DirectoryInfo(start);
            while (cursor != null)
            {
                string marker = Path.Combine(cursor.FullName, ".git");
                if (Directory.Exists(marker) || File.Exists(marker)) return Normalize(cursor.FullName);
                cursor = cursor.Parent;
            }
            return string.Empty;
        }

        private static GitResult RunGit(string workingDirectory, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(start))
                {
                    if (process == null) return new GitResult(-1, string.Empty);
                    var output = new StringBuilder();
                    var error = new StringBuilder();
                    process.OutputDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data == null) return;
                        lock (output) output.AppendLine(eventArgs.Data);
                    };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data == null) return;
                        lock (error) error.AppendLine(eventArgs.Data);
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(10000))
                    {
                        try { process.Kill(); }
                        catch { /* Preflight remains failed even if Git cannot be killed. */ }
                        return new GitResult(-1, string.Empty);
                    }

                    // Flush asynchronous stream callbacks only after the bounded wait proved exit.
                    process.WaitForExit();
                    lock (output) return new GitResult(process.ExitCode, output.ToString());
                }
            }
            catch
            {
                return new GitResult(-1, string.Empty);
            }
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty)
            .Replace("\"", "\\\"") + "\"";

        private static string Normalize(string path) => Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static bool DescendantOrSame(string candidate, string parent)
        {
            string path = Normalize(candidate);
            string root = Normalize(parent);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct GitResult
        {
            public int ExitCode { get; }
            public string StandardOutput { get; }

            public GitResult(int exitCode, string standardOutput)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
            }
        }
    }
}
#endif
