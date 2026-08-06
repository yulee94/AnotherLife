#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        private static readonly string[] RequiredIl2CppToolPaths =
        {
            "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp",
            "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp.exe"
        };

        private const string MissingIl2CppToolDescription =
            "unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp(.exe)";

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
                report = environment.BuildPlayer(plan.CreateBuildPlayerOptions()) ??
                    AndroidUnityLibraryBuildReportSnapshot.NotRun(
                        plan.Target.ToString(),
                        plan.OutputDirectory,
                        "BuildPipeline returned no BuildReport.");
                artifacts = environment.InspectExport(plan.OutputDirectory) ??
                    AndroidUnityLibraryArtifactSnapshot.NotInspected(
                        "Export inspection returned no result.");
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
                try
                {
                    environment.RestoreBuildSettings(originalSettings);
                }
                catch (Exception exception)
                {
                    restorationFailure = exception.GetType().Name + ": " + exception.Message;
                }
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
                MaximumArtifactBytes);

        internal static AndroidUnityLibraryArtifactSnapshot InspectExportTree(
            string outputDirectory,
            int maximumFiles,
            int maximumDirectories,
            long maximumBytes)
        {
            try
            {
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

                var pendingDirectories = new Queue<string>();
                var acceptedFiles = new List<string>();
                pendingDirectories.Enqueue(root);
                int directoryCount = 1;
                while (pendingDirectories.Count > 0)
                {
                    string directory = pendingDirectories.Dequeue();
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

                        FileAttributes attributes = File.GetAttributes(fullEntry);
                        if (!IsSafeArtifactAttributes(attributes))
                        {
                            return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                                "Artifact tree contains a reparse-point/symlink entry: " +
                                RelativePath(root, fullEntry) + ".");
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
                            pendingDirectories.Enqueue(fullEntry);
                            continue;
                        }

                        acceptedFiles.Add(fullEntry);
                        if (acceptedFiles.Count > maximumFiles)
                        {
                            return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                                "Export exceeds the " + maximumFiles +
                                " file inspection bound.");
                        }
                    }
                }

                string[] files = acceptedFiles
                    .OrderBy(path => RelativePath(root, path), StringComparer.Ordinal)
                    .ToArray();
                long totalBytes = 0;
                var inventory = new StringBuilder(Math.Max(256, files.Length * 128));
                var relativeFiles = new HashSet<string>(StringComparer.Ordinal);
                var fullPaths = new Dictionary<string, string>(StringComparer.Ordinal);
                var lengths = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (string file in files)
                {
                    string relative = RelativePath(root, file);
                    long length = new FileInfo(file).Length;
                    if (length < 0 || totalBytes > maximumBytes - length)
                    {
                        return AndroidUnityLibraryArtifactSnapshot.NotInspected(
                            "Export exceeds the " + maximumBytes + " byte inspection bound.");
                    }
                    totalBytes += length;
                    relativeFiles.Add(relative);
                    fullPaths[relative] = file;
                    lengths[relative] = length;
                    inventory.Append(relative).Append('\0')
                        .Append(length.ToString(CultureInfo.InvariantCulture)).Append('\0')
                        .Append(ComputeFileSha256(file)).Append('\n');
                }

                var missingArtifacts = RequiredArtifactPaths
                    .Where(required => !relativeFiles.Contains(required))
                    .ToList();
                if (!RequiredIl2CppToolPaths.Any(path =>
                        relativeFiles.Contains(path) && lengths[path] > 0))
                {
                    missingArtifacts.Add(MissingIl2CppToolDescription);
                }
                string[] missing = missingArtifacts
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string[] invalid = RequiredArtifactPaths
                    .Where(relative => relativeFiles.Contains(relative))
                    .Concat(RequiredIl2CppToolPaths.Where(relative => relativeFiles.Contains(relative)))
                    .Select(relative => ValidateRequiredArtifact(
                        relative,
                        fullPaths[relative],
                        lengths[relative]))
                    .Where(failure => failure.Length > 0)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string jniRoot = Path.Combine(
                    root,
                    "unityLibrary",
                    "src",
                    "main",
                    "jniLibs");
                string[] abiDirectories = Directory.Exists(jniRoot)
                    ? Directory.GetDirectories(jniRoot)
                        .Select(Path.GetFileName)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray()
                    : Array.Empty<string>();
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
            string fullPath,
            long length)
        {
            if (length <= 0) return relativePath + " (empty)";

            if (string.Equals(
                    relativePath,
                    "unityLibrary/libs/unity-classes.jar",
                    StringComparison.Ordinal) &&
                !HasZipSignature(fullPath))
            {
                return relativePath + " (missing ZIP/JAR signature)";
            }

            if (relativePath.EndsWith(".so", StringComparison.Ordinal) &&
                !StartsWithBytes(fullPath, new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }))
            {
                return relativePath + " (missing ELF signature)";
            }

            if (string.Equals(relativePath, "settings.gradle", StringComparison.Ordinal) &&
                !PrefixContainsText(fullPath, "unityLibrary"))
            {
                return relativePath + " (unityLibrary module is not included)";
            }

            if (string.Equals(relativePath, "unityLibrary/build.gradle", StringComparison.Ordinal) &&
                (!PrefixContainsText(fullPath, "com.android.library") ||
                 !PrefixContainsText(fullPath, "minSdkVersion 24") ||
                 !PrefixContainsText(fullPath, "Il2CppOutputProject") ||
                 !PrefixContainsText(fullPath, "libil2cpp.so")))
            {
                return relativePath +
                    " (library plugin, minimum API 24, or staged IL2CPP Gradle generation is missing)";
            }

            if (string.Equals(
                    relativePath,
                    "unityLibrary/src/main/AndroidManifest.xml",
                    StringComparison.Ordinal) &&
                !PrefixContainsText(fullPath, "<manifest"))
            {
                return relativePath + " (manifest root is missing)";
            }

            return string.Empty;
        }

        private static bool HasZipSignature(string path)
        {
            byte[][] signatures =
            {
                new byte[] { 0x50, 0x4b, 0x03, 0x04 },
                new byte[] { 0x50, 0x4b, 0x05, 0x06 },
                new byte[] { 0x50, 0x4b, 0x07, 0x08 }
            };
            return signatures.Any(signature => StartsWithBytes(path, signature));
        }

        private static bool StartsWithBytes(string path, IReadOnlyList<byte> expected)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < expected.Count) return false;
                for (int index = 0; index < expected.Count; index++)
                {
                    if (stream.ReadByte() != expected[index]) return false;
                }
                return true;
            }
        }

        private static bool PrefixContainsText(string path, string expected)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int count = (int)Math.Min(stream.Length, MaximumStructuralTextBytes);
                var bytes = new byte[count];
                int offset = 0;
                while (offset < count)
                {
                    int read = stream.Read(bytes, offset, count - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                string text = new UTF8Encoding(false, true).GetString(bytes, 0, offset);
                return text.IndexOf(expected, StringComparison.Ordinal) >= 0;
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

        private static string ComputeFileSha256(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Hex(algorithm.ComputeHash(stream));
            }
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
        DateTime UtcNow();
        BuildValidationSnapshot ValidateCurrentShellFoundation();
        bool IsIgnoredPath(string fullPath);
        bool IsTrackedPath(string fullPath);
        bool HasReparsePoint(string fullPath);
        bool DirectoryExists(string fullPath);
        void DeleteDirectory(string fullPath);
        void CreateDirectory(string fullPath);
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
            Directory.Delete(candidate, recursive: true);
        }

        public void CreateDirectory(string fullPath)
        {
            if (!AndroidUnityLibraryExporter.IsGuardedOutputDirectory(ProjectRoot, fullPath))
            {
                throw new IOException("Refusing to create an unguarded Android export directory.");
            }
            Directory.CreateDirectory(fullPath);
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
            string destination = Normalize(fullPath);
            if (!AndroidUnityLibraryExporter.IsGuardedSummaryPath(ProjectRoot, destination) ||
                !IsIgnoredPath(destination) ||
                IsTrackedPath(destination) ||
                HasReparsePoint(destination))
            {
                throw new IOException("Refusing to write an unguarded Android export summary.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ProjectRoot);
            string temporary = destination + ".tmp-" + Process.GetCurrentProcess().Id;
            byte[] payload = new UTF8Encoding(false).GetBytes(contents ?? string.Empty);
            try
            {
                using (var stream = new FileStream(
                           temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(flushToDisk: true);
                }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
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
