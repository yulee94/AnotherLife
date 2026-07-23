#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.Core.Scenes;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AL.EditorTools
{
    public enum ProductionIosExportStatus
    {
        Succeeded,
        PreflightFailed,
        ExportFailed,
        MissingXcodeProject,
        Exception
    }

    /// <summary>Read-only iOS identity/signing snapshot. The export path never changes these settings.</summary>
    public sealed class ProductionIosSigningSnapshot
    {
        public ProductionIosSigningSnapshot(
            string bundleIdentifier,
            string bundleVersion,
            string targetOsVersion,
            string developerTeamId,
            bool automaticSigning)
        {
            BundleIdentifier = bundleIdentifier ?? string.Empty;
            BundleVersion = bundleVersion ?? string.Empty;
            TargetOsVersion = targetOsVersion ?? string.Empty;
            DeveloperTeamId = developerTeamId ?? string.Empty;
            AutomaticSigning = automaticSigning;
        }

        public string BundleIdentifier { get; }
        public string BundleVersion { get; }
        public string TargetOsVersion { get; }
        public string DeveloperTeamId { get; }
        public bool AutomaticSigning { get; }
        public bool HasUnityTeamConfiguration => BundleIdentifier.Length > 0 && DeveloperTeamId.Length > 0;

        public string Summarize()
        {
            return $"bundleIdentifier='{BundleIdentifier}' bundleVersion='{BundleVersion}' " +
                   $"targetOS='{TargetOsVersion}' teamConfigured={DeveloperTeamId.Length > 0} " +
                   $"automaticSigning={AutomaticSigning}";
        }
    }

    public sealed class ProductionIosPreflightReport
    {
        public ProductionIosPreflightReport(
            ProductionBuildSettingsValidationReport buildSettings,
            ProductionIosSigningSnapshot signing,
            bool unityVersionValid,
            bool compilationValid,
            bool iosModuleAvailable,
            bool outputPathValid,
            bool outputPathIgnored,
            bool xcodeInstalled,
            IEnumerable<string> failures,
            IEnumerable<string> notices)
        {
            BuildSettings = buildSettings;
            Signing = signing;
            UnityVersionValid = unityVersionValid;
            CompilationValid = compilationValid;
            IosModuleAvailable = iosModuleAvailable;
            OutputPathValid = outputPathValid;
            OutputPathIgnored = outputPathIgnored;
            XcodeInstalled = xcodeInstalled;
            Failures = (failures ?? Array.Empty<string>()).ToList().AsReadOnly();
            Notices = (notices ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public ProductionBuildSettingsValidationReport BuildSettings { get; }
        public ProductionIosSigningSnapshot Signing { get; }
        public bool UnityVersionValid { get; }
        public bool CompilationValid { get; }
        public bool IosModuleAvailable { get; }
        public bool OutputPathValid { get; }
        public bool OutputPathIgnored { get; }
        public bool XcodeInstalled { get; }
        public IReadOnlyList<string> Failures { get; }
        public IReadOnlyList<string> Notices { get; }
        public bool IsValid => BuildSettings != null && BuildSettings.IsValid && Failures.Count == 0;

        public string Summarize()
        {
            var builder = new StringBuilder("[AL-IOS-PREFLIGHT] valid=").Append(IsValid);
            if (BuildSettings != null)
            {
                builder.Append('\n').Append(BuildSettings.Summarize());
            }

            if (Signing != null)
            {
                builder.Append('\n').Append("  signing: ").Append(Signing.Summarize());
            }

            foreach (string failure in Failures)
            {
                builder.Append('\n').Append("  failure: ").Append(failure);
            }

            foreach (string notice in Notices)
            {
                builder.Append('\n').Append("  notice: ").Append(notice);
            }

            return builder.ToString();
        }
    }

    public sealed class ProductionIosExportResult
    {
        internal ProductionIosExportResult(
            ProductionIosExportStatus status,
            string unityVersion,
            string outputPath,
            IEnumerable<string> scenePaths,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            TimeSpan totalTime,
            ulong totalSize,
            int warningCount,
            int errorCount,
            BuildResult buildResult,
            string summaryMessage,
            ProductionIosSigningSnapshot signing,
            BuildReport report)
        {
            Status = status;
            UnityVersion = unityVersion ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            ScenePaths = (scenePaths ?? Array.Empty<string>()).ToList().AsReadOnly();
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            TotalTime = totalTime;
            TotalSize = totalSize;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            BuildResult = buildResult;
            SummaryMessage = summaryMessage ?? string.Empty;
            Signing = signing;
            Report = report;
        }

        public ProductionIosExportStatus Status { get; }
        public BuildTarget Target => BuildTarget.iOS;
        public string UnityVersion { get; }
        public string OutputPath { get; }
        public IReadOnlyList<string> ScenePaths { get; }
        public DateTime StartedAtUtc { get; }
        public DateTime EndedAtUtc { get; }
        public TimeSpan TotalTime { get; }
        public ulong TotalSize { get; }
        public int WarningCount { get; }
        public int ErrorCount { get; }
        public BuildResult BuildResult { get; }
        public string SummaryMessage { get; }
        public ProductionIosSigningSnapshot Signing { get; }
        public BuildReport Report { get; }
        public bool Succeeded => Status == ProductionIosExportStatus.Succeeded &&
                                 BuildResult == UnityEditor.Build.Reporting.BuildResult.Succeeded;

        public string Summarize()
        {
            return $"[AL-IOS-EXPORT-RESULT] status={Status} target={Target} unity={UnityVersion} " +
                   $"buildResult={BuildResult} output='{OutputPath}' scenes={ScenePaths.Count} " +
                   $"duration={TotalTime.TotalSeconds:0.###}s size={TotalSize} warnings={WarningCount} " +
                   $"errors={ErrorCount} teamConfigured={Signing != null && Signing.HasUnityTeamConfiguration} " +
                   $"message='{SummaryMessage}'";
        }
    }

    /// <summary>
    /// Signing-neutral iOS Development exporter. It creates an Xcode project from the committed
    /// ShellFoundation profile and never changes bundle identity, Team ID, provisioning, or Player Settings.
    /// </summary>
    public static class ProductionIosBuilder
    {
        public const string RelativeOutputPath = "Builds/Validation/iOS/Xcode";
        public const string RelativeArtifactDirectoryPath = "Builds/Validation/iOS";
        public const string SummaryFileName = "IosDevelopmentExport.summary.json";
        public const string ReportFileName = "IosDevelopmentExport.report.txt";

        internal static Func<BuildPlayerOptions, BuildReport> BuildPlayerOverride;

        public static string OutputPath => Path.GetFullPath(Path.Combine(ProjectRoot(), RelativeOutputPath));
        public static string ArtifactDirectoryPath =>
            Path.GetFullPath(Path.Combine(ProjectRoot(), RelativeArtifactDirectoryPath));

        /// <summary>Unity -executeMethod entry. Any non-success throws and fails batch mode.</summary>
        public static void ExportIosDevelopmentXcodeProject()
        {
            ProductionIosExportResult result = ExportIosDevelopment();
            Debug.Log(result.Summarize());
            if (!result.Succeeded)
            {
                throw new BuildFailedException(result.Summarize());
            }
        }

        public static ProductionIosExportResult ExportIosDevelopment()
        {
            DateTime startedAtUtc = DateTime.UtcNow;
            string[] scenes = ProductionSceneDescriptor.ShellFoundationOrdered.Select(record => record.AssetPath).ToArray();

            ProductionIosPreflightReport preflight = ValidatePreflight();
            if (!preflight.IsValid)
            {
                ProductionIosExportResult failure = Result(
                    ProductionIosExportStatus.PreflightFailed,
                    scenes,
                    startedAtUtc,
                    DateTime.UtcNow,
                    TimeSpan.Zero,
                    0,
                    0,
                    1,
                    BuildResult.Unknown,
                    preflight.Summarize(),
                    preflight.Signing,
                    null);
                WriteArtifacts(failure);
                return failure;
            }

            BuildReport report;
            try
            {
                CleanExactOutputDirectory();
                BuildPlayerOptions options = CreateIosDevelopmentOptions();
                report = BuildPlayerOverride != null
                    ? BuildPlayerOverride(options)
                    : BuildPipeline.BuildPlayer(options);
            }
            catch (Exception exception)
            {
                ProductionIosExportResult failure = Result(
                    ProductionIosExportStatus.Exception,
                    scenes,
                    startedAtUtc,
                    DateTime.UtcNow,
                    DateTime.UtcNow - startedAtUtc,
                    0,
                    0,
                    1,
                    BuildResult.Failed,
                    exception.ToString(),
                    preflight.Signing,
                    null);
                WriteArtifacts(failure);
                return failure;
            }

            if (report == null)
            {
                ProductionIosExportResult failure = Result(
                    ProductionIosExportStatus.ExportFailed,
                    scenes,
                    startedAtUtc,
                    DateTime.UtcNow,
                    DateTime.UtcNow - startedAtUtc,
                    0,
                    0,
                    1,
                    BuildResult.Unknown,
                    "BuildPipeline returned no BuildReport.",
                    preflight.Signing,
                    null);
                WriteArtifacts(failure);
                return failure;
            }

            BuildSummary summary = report.summary;
            bool projectExists = RequiredXcodeProjectFilesExist(OutputPath);
            ProductionIosExportStatus status = ClassifyExport(summary.result, projectExists);
            string message = status == ProductionIosExportStatus.Succeeded
                ? "Unsigned iOS Development Xcode project exported successfully."
                : $"iOS export validation failed: report={summary.result}, requiredXcodeFiles={projectExists}.";

            ProductionIosExportResult result = new ProductionIosExportResult(
                status,
                Application.unityVersion,
                OutputPath,
                scenes,
                summary.buildStartedAt.ToUniversalTime(),
                summary.buildEndedAt.ToUniversalTime(),
                summary.totalTime,
                summary.totalSize,
                summary.totalWarnings,
                summary.totalErrors,
                summary.result,
                message,
                preflight.Signing,
                report);
            WriteArtifacts(result);
            return result;
        }

        public static ProductionIosPreflightReport ValidatePreflight()
        {
            var failures = new List<string>();
            var notices = new List<string>();
            ProductionBuildSettingsValidationReport buildSettings =
                ProductionBuildSettingsValidator.ValidateCurrentShellFoundation();
            ProductionIosSigningSnapshot signing = CurrentSigningSnapshot();
            bool unityVersionValid = string.Equals(
                Application.unityVersion,
                ProductionPlayerBuilder.RequiredUnityVersion,
                StringComparison.Ordinal);
            bool compilationValid = !EditorUtility.scriptCompilationFailed && !EditorApplication.isCompiling;
            bool iosModuleAvailable = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);
            bool outputPathValid = IsOutputPathSafe(OutputPath);
            bool outputPathIgnored = IsOutputPathIgnored();
            bool xcodeInstalled = Directory.Exists("/Applications/Xcode.app/Contents/Developer");

            if (!unityVersionValid)
            {
                failures.Add($"Unity version '{Application.unityVersion}' does not match required '{ProductionPlayerBuilder.RequiredUnityVersion}'.");
            }

            if (!compilationValid)
            {
                failures.Add("Unity scripts have compile errors or compilation is still active.");
            }

            if (!iosModuleAvailable)
            {
                failures.Add("Unity iOS Build Support is not installed for this editor.");
            }

            if (!outputPathValid)
            {
                failures.Add("iOS output is not the exact safe validation path outside Assets.");
            }

            if (!outputPathIgnored)
            {
                failures.Add("iOS validation output is not covered by the Unity project .gitignore.");
            }

            if (!buildSettings.IsValid)
            {
                failures.Add("Production Build Settings validation failed.");
            }

            if (!xcodeInstalled)
            {
                notices.Add("Full Xcode is not installed; Unity can export, but this Mac cannot compile/archive the project.");
            }

            if (!signing.HasUnityTeamConfiguration)
            {
                notices.Add("Apple Team ID/signing is not configured; export remains unsigned and cannot install on a device.");
            }

            return new ProductionIosPreflightReport(
                buildSettings,
                signing,
                unityVersionValid,
                compilationValid,
                iosModuleAvailable,
                outputPathValid,
                outputPathIgnored,
                xcodeInstalled,
                failures,
                notices);
        }

        public static ProductionIosSigningSnapshot CurrentSigningSnapshot()
        {
            return new ProductionIosSigningSnapshot(
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS),
                PlayerSettings.bundleVersion,
                PlayerSettings.iOS.targetOSVersionString,
                PlayerSettings.iOS.appleDeveloperTeamID,
                PlayerSettings.iOS.appleEnableAutomaticSigning);
        }

        internal static BuildPlayerOptions CreateIosDevelopmentOptions()
        {
            return new BuildPlayerOptions
            {
                scenes = ProductionSceneDescriptor.ShellFoundationOrdered.Select(record => record.AssetPath).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.Development
            };
        }

        internal static ProductionIosExportStatus ClassifyExport(BuildResult buildResult, bool requiredProjectFilesExist)
        {
            if (buildResult != BuildResult.Succeeded)
            {
                return ProductionIosExportStatus.ExportFailed;
            }

            return requiredProjectFilesExist
                ? ProductionIosExportStatus.Succeeded
                : ProductionIosExportStatus.MissingXcodeProject;
        }

        internal static string DescribeExportIntent()
        {
            ProductionIosSigningSnapshot signing = CurrentSigningSnapshot();
            return "profile=ShellFoundation target=iOS options=Development signingMutation=false " +
                   "scenes=[" + string.Join(",", ProductionSceneDescriptor.ShellFoundationOrdered.Select(record => record.AssetPath)) + "] " +
                   "excluded=[Assets/Test.unity,Assets/AL/Scenes/ChampionArena.unity] " +
                   "output='" + OutputPath + "' " + signing.Summarize();
        }

        private static ProductionIosExportResult Result(
            ProductionIosExportStatus status,
            IEnumerable<string> scenes,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            TimeSpan totalTime,
            ulong totalSize,
            int warningCount,
            int errorCount,
            BuildResult buildResult,
            string message,
            ProductionIosSigningSnapshot signing,
            BuildReport report)
        {
            return new ProductionIosExportResult(
                status,
                Application.unityVersion,
                OutputPath,
                scenes,
                startedAtUtc,
                endedAtUtc,
                totalTime,
                totalSize,
                warningCount,
                errorCount,
                buildResult,
                message,
                signing,
                report);
        }

        private static bool RequiredXcodeProjectFilesExist(string root)
        {
            return File.Exists(Path.Combine(root, "Unity-iPhone.xcodeproj", "project.pbxproj")) &&
                   File.Exists(Path.Combine(root, "Info.plist")) &&
                   Directory.Exists(Path.Combine(root, "Classes")) &&
                   Directory.Exists(Path.Combine(root, "Libraries"));
        }

        private static void CleanExactOutputDirectory()
        {
            if (!IsOutputPathSafe(OutputPath))
            {
                throw new InvalidOperationException("Refusing to clean an unsafe iOS output directory.");
            }

            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, recursive: true);
            }
        }

        private static bool IsOutputPathSafe(string outputPath)
        {
            string expected = Path.GetFullPath(Path.Combine(ProjectRoot(), RelativeOutputPath));
            string assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            string actual = Path.GetFullPath(outputPath ?? string.Empty);
            return string.Equals(actual, expected, PathComparison()) &&
                   !actual.StartsWith(assets, PathComparison());
        }

        private static bool IsOutputPathIgnored()
        {
            string ignorePath = Path.Combine(ProjectRoot(), ".gitignore");
            if (!File.Exists(ignorePath))
            {
                return false;
            }

            string text = File.ReadAllText(ignorePath);
            return text.IndexOf("/[Bb]uilds/", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("/Builds/", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("/Builds/Validation/", StringComparison.Ordinal) >= 0;
        }

        private static StringComparison PathComparison()
        {
            return Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void WriteArtifacts(ProductionIosExportResult result)
        {
            Directory.CreateDirectory(ArtifactDirectoryPath);
            var json = new ProductionIosExportSummaryJson
            {
                status = result.Status.ToString(),
                target = result.Target.ToString(),
                unityVersion = result.UnityVersion,
                outputPath = result.OutputPath,
                scenePaths = result.ScenePaths.ToArray(),
                startedAtUtc = result.StartedAtUtc.ToString("O"),
                endedAtUtc = result.EndedAtUtc.ToString("O"),
                totalTime = result.TotalTime.ToString(),
                totalSize = result.TotalSize.ToString(),
                warningCount = result.WarningCount,
                errorCount = result.ErrorCount,
                BuildResult = result.BuildResult.ToString(),
                summaryMessage = result.SummaryMessage,
                bundleIdentifier = result.Signing?.BundleIdentifier ?? string.Empty,
                bundleVersion = result.Signing?.BundleVersion ?? string.Empty,
                targetOsVersion = result.Signing?.TargetOsVersion ?? string.Empty,
                developerTeamConfigured = result.Signing != null && result.Signing.DeveloperTeamId.Length > 0,
                automaticSigning = result.Signing != null && result.Signing.AutomaticSigning
            };
            File.WriteAllText(
                Path.Combine(ArtifactDirectoryPath, SummaryFileName),
                JsonUtility.ToJson(json, prettyPrint: true));
            File.WriteAllText(
                Path.Combine(ArtifactDirectoryPath, ReportFileName),
                BuildReportText(result));
        }

        private static string BuildReportText(ProductionIosExportResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine(result.Summarize());
            builder.AppendLine(DescribeExportIntent());
            builder.AppendLine("Xcode installed: " + Directory.Exists("/Applications/Xcode.app/Contents/Developer"));
            builder.AppendLine("Device compilation/signing performed: false");

            if (result.Report == null)
            {
                builder.AppendLine("BuildReport: unavailable");
                return builder.ToString();
            }

            foreach (BuildStep step in result.Report.steps)
            {
                builder.Append("STEP ").Append(step.name).Append(" duration=").AppendLine(step.duration.ToString());
                foreach (BuildStepMessage message in step.messages)
                {
                    builder.Append("  ").Append(message.type).Append(": ").AppendLine(message.content);
                }
            }

            foreach (BuildFile file in result.Report.GetFiles())
            {
                builder.Append("FILE ").Append(file.path).Append(" role=").Append(file.role)
                    .Append(" size=").AppendLine(file.size.ToString());
            }

            return builder.ToString();
        }

        [Serializable]
        private sealed class ProductionIosExportSummaryJson
        {
            public string status;
            public string target;
            public string unityVersion;
            public string outputPath;
            public string[] scenePaths;
            public string startedAtUtc;
            public string endedAtUtc;
            public string totalTime;
            public string totalSize;
            public int warningCount;
            public int errorCount;
            public string BuildResult;
            public string summaryMessage;
            public string bundleIdentifier;
            public string bundleVersion;
            public string targetOsVersion;
            public bool developerTeamConfigured;
            public bool automaticSigning;
        }
    }
}
#endif
