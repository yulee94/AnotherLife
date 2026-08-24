#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    /// <summary>
    /// Deterministic #150 ShellFoundation Player builder. The batch entry deliberately delegates all
    /// policy to the committed descriptor and the non-mutating Build Settings validator, cleans only
    /// the one guarded validation-output directory, and fails closed on incomplete build evidence.
    /// </summary>
    public static class ProductionPlayerBuilder
    {
        public const string RequiredUnityVersion = "6000.3.22f1";
        public const string OutputRelativeDirectory = "Builds/Validation/Windows64";
        public const string ExecutableFileName = "AnotherLifeUnity.exe";
        public const string DataDirectoryName = "AnotherLifeUnity_Data";
        public const string SummaryRelativePath = "Logs/ProductionPlayerBuildSummary.json";

        /// <summary>Canonical Unity -executeMethod entry. An exception is intentional non-zero batch evidence.</summary>
        public static void BuildWindows64Development()
        {
            PlayerBuildSummary summary = Execute(new UnityProductionPlayerBuildEnvironment());
            Debug.Log("[AL-PLAYER-BUILD-SUMMARY] " + summary.Summarize());

            if (!summary.Succeeded)
            {
                throw new BuildFailedException(summary.SummaryMessage);
            }
        }

        /// <summary>
        /// Testable orchestration seam. Preflight is completed before stale-output cleanup or the Unity
        /// build call, so every rejected preflight is observably non-mutating.
        /// </summary>
        internal static PlayerBuildSummary Execute(IProductionPlayerBuildEnvironment environment)
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
                outputDirectory = NormalizeFullPath(Path.Combine(projectRoot, "Builds", "Validation", "Windows64"));
                summaryPath = NormalizeFullPath(Path.Combine(projectRoot, "Logs", "ProductionPlayerBuildSummary.json"));
            }
            catch (Exception exception)
            {
                return PlayerBuildSummary.PreflightFailure(
                    string.Empty,
                    string.Empty,
                    Array.Empty<string>(),
                    environment.UnityVersion,
                    startedAtUtc,
                    SafeUtcNow(environment),
                    "Project/output path resolution failed: " + exception.GetType().Name);
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
                    "Build Settings validation threw " + exception.GetType().Name + ": " + exception.Message);
            }

            bool outputIgnored = SafeBoolean(() => environment.IsIgnoredPath(outputDirectory));
            bool summaryIgnored = SafeBoolean(() => environment.IsIgnoredPath(summaryPath));
            bool outputHasReparsePoint = SafeBooleanFailureClosed(() => environment.HasReparsePoint(outputDirectory));
            bool summaryHasReparsePoint = SafeBooleanFailureClosed(
                () => environment.HasReparsePoint(summaryPath));

            PlayerBuildPlan plan = CreatePlan(
                projectRoot,
                outputDirectory,
                environment.UnityVersion,
                environment.IsCompiling,
                environment.HasCompilationErrors,
                validation,
                outputIgnored,
                summaryIgnored,
                outputHasReparsePoint,
                summaryHasReparsePoint);

            if (!plan.IsValid)
            {
                PlayerBuildSummary failure = PlayerBuildSummary.PreflightFailure(
                    plan.OutputPath,
                    plan.Target.ToString(),
                    plan.ScenePaths,
                    plan.UnityVersion,
                    startedAtUtc,
                    SafeUtcNow(environment),
                    plan.SummarizeFailures());
                return FinalizeSummary(environment, plan, failure);
            }

            try
            {
                if (environment.DirectoryExists(plan.OutputDirectory))
                {
                    // The plan's exact-path and reparse-point gates have already succeeded. No other
                    // directory can reach this destructive operation.
                    environment.DeleteDirectory(plan.OutputDirectory);
                }

                if (environment.DirectoryExists(plan.OutputDirectory))
                {
                    throw new IOException("Guarded validation output still exists after cleanup.");
                }

                environment.CreateDirectory(plan.OutputDirectory);
                if (!environment.DirectoryExists(plan.OutputDirectory))
                {
                    throw new IOException("Guarded validation output could not be created.");
                }
            }
            catch (Exception exception)
            {
                PlayerBuildSummary failure = PlayerBuildSummary.PreparationFailure(
                    plan,
                    startedAtUtc,
                    SafeUtcNow(environment),
                    exception.GetType().Name + ": " + exception.Message);
                return FinalizeSummary(environment, plan, failure);
            }

            PlayerBuildReportSnapshot report;
            try
            {
                report = environment.BuildPlayer(plan.CreateBuildPlayerOptions()) ??
                    PlayerBuildReportSnapshot.NotRun(
                        plan.Target.ToString(),
                        plan.OutputPath,
                        "BuildPipeline returned no BuildReport.");
            }
            catch (Exception exception)
            {
                report = PlayerBuildReportSnapshot.Exception(
                    plan.Target.ToString(),
                    plan.OutputPath,
                    exception.GetType().Name + ": " + exception.Message);
            }

            bool executableExists = SafeBoolean(() => environment.FileExists(plan.OutputPath));
            bool dataDirectoryExists = SafeBoolean(() => environment.DirectoryExists(plan.DataDirectoryPath));
            PlayerBuildSummary completion = EvaluateCompletion(
                plan,
                report,
                startedAtUtc,
                SafeUtcNow(environment),
                executableExists,
                dataDirectoryExists);
            return FinalizeSummary(environment, plan, completion);
        }

        /// <summary>Pure preflight/option planning seam used by EditMode tests.</summary>
        internal static PlayerBuildPlan CreatePlan(
            string projectRoot,
            string outputDirectory,
            string unityVersion,
            bool isCompiling,
            bool hasCompilationErrors,
            BuildValidationSnapshot validation,
            bool outputIgnored,
            bool summaryIgnored,
            bool outputHasReparsePoint,
            bool summaryHasReparsePoint)
        {
            var failures = new List<string>();
            string normalizedRoot = TryNormalize(projectRoot);
            string normalizedOutput = TryNormalize(outputDirectory);
            string summaryPath = normalizedRoot.Length == 0
                ? string.Empty
                : TryNormalize(Path.Combine(normalizedRoot, "Logs", "ProductionPlayerBuildSummary.json"));

            if (normalizedRoot.Length == 0)
            {
                failures.Add("Project root is missing or invalid.");
            }

            if (!string.Equals(unityVersion, RequiredUnityVersion, StringComparison.Ordinal))
            {
                failures.Add("Exact Unity version required: " + RequiredUnityVersion + "; actual: " +
                    (string.IsNullOrEmpty(unityVersion) ? "<missing>" : unityVersion) + ".");
            }

            if (isCompiling)
            {
                failures.Add("Unity script compilation is still in progress.");
            }

            if (hasCompilationErrors)
            {
                failures.Add("Unity reports script compilation errors.");
            }

            if (validation == null || !validation.IsValid)
            {
                failures.Add("ShellFoundation Build Settings/scene validation failed: " +
                    (validation == null ? "no report" : validation.Summary));
            }

            if (!IsGuardedOutputDirectory(normalizedRoot, normalizedOutput))
            {
                failures.Add("Player output is not the exact guarded Builds/Validation/Windows64 directory outside Assets.");
            }

            if (!outputIgnored)
            {
                failures.Add("Guarded Player output is not covered by the Unity project ignore policy.");
            }

            if (outputHasReparsePoint)
            {
                failures.Add("Guarded Player output contains a reparse-point/symlink boundary.");
            }

            if (!IsGuardedSummaryPath(normalizedRoot, summaryPath) || !summaryIgnored)
            {
                failures.Add("Build summary path is not the exact ignored Logs path outside Assets.");
            }

            if (summaryHasReparsePoint)
            {
                failures.Add("Build summary directory contains a reparse-point/symlink boundary.");
            }

            ProductionSceneRecord[] records = ProductionSceneDescriptor.ShellFoundationOrdered.ToArray();
            if (records.Length != 5 ||
                records.Any(record => record == null || !record.IsProductionScene || !record.IsInShellFoundation) ||
                records.Any(record =>
                    string.Equals(record.SceneId, ProductionSceneDescriptor.TestSceneId, StringComparison.Ordinal)) ||
                records.Select(record => record.AssetPath).Distinct(StringComparer.Ordinal).Count() != records.Length)
            {
                failures.Add("ShellFoundation descriptor is not five unique production scenes with Test excluded.");
            }

            string[] scenePaths = records
                .Where(record => record != null)
                .Select(record => record.AssetPath ?? string.Empty)
                .ToArray();
            string outputPath = normalizedOutput.Length == 0
                ? string.Empty
                : Path.Combine(normalizedOutput, ExecutableFileName);
            return new PlayerBuildPlan(
                normalizedRoot,
                normalizedOutput,
                outputPath,
                summaryPath,
                unityVersion ?? string.Empty,
                scenePaths,
                failures,
                IsGuardedSummaryPath(normalizedRoot, summaryPath) &&
                summaryIgnored &&
                !summaryHasReparsePoint);
        }

        /// <summary>Pure BuildReport/artifact evaluation seam used by EditMode tests.</summary>
        internal static PlayerBuildSummary EvaluateCompletion(
            PlayerBuildPlan plan,
            PlayerBuildReportSnapshot report,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            bool executableExists,
            bool dataDirectoryExists)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (report == null)
            {
                report = PlayerBuildReportSnapshot.NotRun(
                    plan.Target.ToString(),
                    plan.OutputPath,
                    "BuildPipeline returned no report snapshot.");
            }

            if (!string.Equals(report.Result, BuildResult.Succeeded.ToString(), StringComparison.Ordinal))
            {
                return PlayerBuildSummary.BuildFailure(
                    plan,
                    report,
                    startedAtUtc,
                    endedAtUtc,
                    "BuildPipeline result was " + report.Result + ". " + report.ReportSummary);
            }

            if (report.ErrorCount != 0)
            {
                return PlayerBuildSummary.BuildFailure(
                    plan,
                    report,
                    startedAtUtc,
                    endedAtUtc,
                    "BuildReport reported " + report.ErrorCount +
                    " error(s) despite a Succeeded result; postflight fails closed.");
            }

            if (!string.Equals(report.Target, plan.Target.ToString(), StringComparison.Ordinal))
            {
                return PlayerBuildSummary.BuildFailure(
                    plan,
                    report,
                    startedAtUtc,
                    endedAtUtc,
                    "BuildReport target did not match the guarded plan.");
            }

            if (!SamePath(report.OutputPath, plan.OutputPath))
            {
                return PlayerBuildSummary.BuildFailure(
                    plan,
                    report,
                    startedAtUtc,
                    endedAtUtc,
                    "BuildReport output path did not match the guarded plan.");
            }

            if (!executableExists || !dataDirectoryExists)
            {
                var missing = new List<string>(2);
                if (!executableExists)
                {
                    missing.Add(ExecutableFileName);
                }

                if (!dataDirectoryExists)
                {
                    missing.Add(DataDirectoryName);
                }

                return PlayerBuildSummary.ArtifactFailure(
                    plan,
                    report,
                    startedAtUtc,
                    endedAtUtc,
                    "BuildReport succeeded but required current-run artifacts are missing: " +
                    string.Join(", ", missing) + ".");
            }

            return PlayerBuildSummary.Success(plan, report, startedAtUtc, endedAtUtc);
        }

        /// <summary>Fixed-order, invariant-culture JSON serializer; does not depend on JsonUtility field order.</summary>
        internal static string SerializeSummary(PlayerBuildSummary summary)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            var json = new StringBuilder(768);
            json.Append("{\n");
            AppendJsonProperty(json, "status", summary.Status.ToString(), comma: true);
            AppendJsonProperty(json, "target", summary.Target, comma: true);
            AppendJsonProperty(json, "unityVersion", summary.UnityVersion, comma: true);
            AppendJsonProperty(json, "outputPath", summary.OutputPath, comma: true);
            json.Append("  \"scenePaths\": [");
            for (int index = 0; index < summary.ScenePaths.Count; index++)
            {
                if (index > 0)
                {
                    json.Append(',');
                }

                json.Append("\n    \"").Append(EscapeJson(summary.ScenePaths[index])).Append('"');
            }

            if (summary.ScenePaths.Count > 0)
            {
                json.Append('\n').Append("  ");
            }

            json.Append("],\n");
            AppendJsonProperty(json, "startedAtUtc", FormatUtc(summary.StartedAtUtc), comma: true);
            AppendJsonProperty(json, "endedAtUtc", FormatUtc(summary.EndedAtUtc), comma: true);
            AppendJsonProperty(json, "totalTime", summary.TotalTime.ToString("c", CultureInfo.InvariantCulture), comma: true);
            AppendJsonNumber(json, "totalSize", summary.TotalSize.ToString(CultureInfo.InvariantCulture), comma: true);
            AppendJsonNumber(json, "warningCount", summary.WarningCount.ToString(CultureInfo.InvariantCulture), comma: true);
            AppendJsonNumber(json, "errorCount", summary.ErrorCount.ToString(CultureInfo.InvariantCulture), comma: true);
            AppendJsonProperty(json, "buildResult", summary.BuildResult, comma: true);
            AppendJsonProperty(json, "summaryMessage", summary.SummaryMessage, comma: false);
            json.Append("}\n");
            return json.ToString();
        }

        internal static bool IsGuardedOutputDirectory(string projectRoot, string candidateOutputDirectory)
        {
            string root = TryNormalize(projectRoot);
            string candidate = TryNormalize(candidateOutputDirectory);
            if (root.Length == 0 || candidate.Length == 0)
            {
                return false;
            }

            string expected = TryNormalize(Path.Combine(root, "Builds", "Validation", "Windows64"));
            string assets = TryNormalize(Path.Combine(root, "Assets"));
            return SamePath(candidate, expected) && !IsSameOrDescendant(candidate, assets);
        }

        internal static bool IsGuardedSummaryPath(string projectRoot, string candidateSummaryPath)
        {
            string root = TryNormalize(projectRoot);
            string candidate = TryNormalize(candidateSummaryPath);
            if (root.Length == 0 || candidate.Length == 0)
            {
                return false;
            }

            string expected = TryNormalize(Path.Combine(root, "Logs", "ProductionPlayerBuildSummary.json"));
            string assets = TryNormalize(Path.Combine(root, "Assets"));
            return SamePath(candidate, expected) && !IsSameOrDescendant(candidate, assets);
        }

        private static PlayerBuildSummary FinalizeSummary(
            IProductionPlayerBuildEnvironment environment,
            PlayerBuildPlan plan,
            PlayerBuildSummary summary)
        {
            if (!plan.CanWriteSummary)
            {
                Debug.LogError("[AL-PLAYER-BUILD-SUMMARY-NOT-WRITTEN] " + summary.Summarize());
                return summary;
            }

            try
            {
                environment.CreateDirectory(Path.GetDirectoryName(plan.SummaryPath));
                string json = SerializeSummary(summary);
                environment.WriteAllText(plan.SummaryPath, json);
                Debug.Log("[AL-PLAYER-BUILD-SUMMARY-PATH] " + plan.SummaryPath);
                Debug.Log("[AL-PLAYER-BUILD-REPORT] " + summary.Summarize());
                return summary;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AL-PLAYER-BUILD-SUMMARY-WRITE-FAILED] " + exception);
                return PlayerBuildSummary.SummaryWriteFailure(
                    summary,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static DateTime SafeUtcNow(IProductionPlayerBuildEnvironment environment)
        {
            try
            {
                return ToUtc(environment.UtcNow());
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static bool SafeBoolean(Func<bool> read)
        {
            try
            {
                return read();
            }
            catch
            {
                return false;
            }
        }

        private static bool SafeBooleanFailureClosed(Func<bool> read)
        {
            try
            {
                return read();
            }
            catch
            {
                return true;
            }
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            string full = Path.GetFullPath(path);
            string volumeRoot = Path.GetPathRoot(full) ?? string.Empty;
            return full.Length > volumeRoot.Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }

        private static string TryNormalize(string path)
        {
            try
            {
                return NormalizeFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
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
            string normalizedCandidate = TryNormalize(candidate);
            string normalizedParent = TryNormalize(parent);
            if (normalizedCandidate.Length == 0 || normalizedParent.Length == 0)
            {
                return false;
            }

            if (string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string prefix = normalizedParent + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }

            return value.ToUniversalTime();
        }

        private static string FormatUtc(DateTime value) =>
            ToUtc(value).ToString("O", CultureInfo.InvariantCulture);

        private static void AppendJsonProperty(StringBuilder json, string name, string value, bool comma)
        {
            json.Append("  \"").Append(name).Append("\": \"")
                .Append(EscapeJson(value ?? string.Empty)).Append('"');
            json.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJsonNumber(StringBuilder json, string name, string value, bool comma)
        {
            json.Append("  \"").Append(name).Append("\": ").Append(value);
            json.Append(comma ? ",\n" : "\n");
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var escaped = new StringBuilder(value.Length + 16);
            foreach (char character in value)
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
                        else
                        {
                            escaped.Append(character);
                        }
                        break;
                }
            }

            return escaped.ToString();
        }
    }

    internal interface IProductionPlayerBuildEnvironment
    {
        string ProjectRoot { get; }
        string UnityVersion { get; }
        bool IsCompiling { get; }
        bool HasCompilationErrors { get; }
        DateTime UtcNow();
        BuildValidationSnapshot ValidateCurrentShellFoundation();
        bool IsIgnoredPath(string fullPath);
        bool HasReparsePoint(string fullPath);
        bool DirectoryExists(string fullPath);
        bool FileExists(string fullPath);
        void DeleteDirectory(string fullPath);
        void CreateDirectory(string fullPath);
        PlayerBuildReportSnapshot BuildPlayer(BuildPlayerOptions options);
        void WriteAllText(string fullPath, string contents);
    }

    internal sealed class UnityProductionPlayerBuildEnvironment : IProductionPlayerBuildEnvironment
    {
        public string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        public string UnityVersion => Application.unityVersion;
        public bool IsCompiling => EditorApplication.isCompiling;
        public bool HasCompilationErrors => EditorUtility.scriptCompilationFailed;

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
            string root = ProductionPlayerBuilderPath.Normalize(ProjectRoot);
            string candidate = ProductionPlayerBuilderPath.Normalize(fullPath);
            if (!ProductionPlayerBuilderPath.IsDescendant(candidate, root))
            {
                return false;
            }

            string repositoryRoot = FindRepositoryRoot(root);
            if (repositoryRoot.Length == 0 || !ProductionPlayerBuilderPath.IsDescendant(candidate, repositoryRoot))
            {
                return false;
            }

            string relative = candidate.Substring(repositoryRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
            GitCommandResult ignored = RunGit(repositoryRoot,
                "check-ignore --no-index -q -- " + QuoteGitArgument(relative));
            if (ignored.ExitCode != 0)
            {
                return false;
            }

            GitCommandResult tracked = RunGit(repositoryRoot,
                "ls-files -- " + QuoteGitArgument(relative));
            return tracked.ExitCode == 0 && string.IsNullOrWhiteSpace(tracked.StandardOutput);
        }

        public bool HasReparsePoint(string fullPath)
        {
            string root = ProductionPlayerBuilderPath.Normalize(ProjectRoot);
            string candidate = ProductionPlayerBuilderPath.Normalize(fullPath);
            if (!ProductionPlayerBuilderPath.IsDescendantOrSame(candidate, root))
            {
                return true;
            }

            string cursor = candidate;
            while (!string.IsNullOrEmpty(cursor))
            {
                try
                {
                    if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (
                    exception is FileNotFoundException ||
                    exception is DirectoryNotFoundException)
                {
                    // A planned leaf or intermediate directory may not exist yet. Its first existing
                    // ancestor is still inspected, including the canonical project root itself.
                }
                catch
                {
                    // Inaccessible or otherwise uninspectable path state is not safe build evidence.
                    return true;
                }

                if (string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string parent = Path.GetDirectoryName(cursor);
                if (string.IsNullOrEmpty(parent) ||
                    string.Equals(parent, cursor, StringComparison.OrdinalIgnoreCase) ||
                    !ProductionPlayerBuilderPath.IsDescendantOrSame(parent, root))
                {
                    return true;
                }

                cursor = ProductionPlayerBuilderPath.Normalize(parent);
            }

            return true;
        }

        public bool DirectoryExists(string fullPath) => Directory.Exists(fullPath);
        public bool FileExists(string fullPath) => File.Exists(fullPath);
        public void DeleteDirectory(string fullPath) => Directory.Delete(fullPath, recursive: true);
        public void CreateDirectory(string fullPath) => Directory.CreateDirectory(fullPath);

        public PlayerBuildReportSnapshot BuildPlayer(BuildPlayerOptions options)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null)
            {
                return null;
            }

            BuildSummary summary = report.summary;
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    string evidence = "[AL-PLAYER-BUILD-MESSAGE] step=" + SingleLine(step.name) +
                                      "; type=" + message.type +
                                      "; content=" + SingleLine(message.content);
                    if (message.type == LogType.Warning)
                    {
                        Debug.LogWarning(evidence);
                    }
                    else if (message.type == LogType.Error ||
                             message.type == LogType.Assert ||
                             message.type == LogType.Exception)
                    {
                        Debug.LogError(evidence);
                    }
                }
            }

            string reportSummary = string.Format(
                CultureInfo.InvariantCulture,
                "result={0}; target={1}; outputPath={2}; totalTime={3}; totalSize={4}; warnings={5}; errors={6}",
                summary.result,
                summary.platform,
                summary.outputPath,
                summary.totalTime.ToString("c", CultureInfo.InvariantCulture),
                summary.totalSize,
                summary.totalWarnings,
                summary.totalErrors);
            return new PlayerBuildReportSnapshot(
                summary.result.ToString(),
                summary.platform.ToString(),
                summary.outputPath,
                summary.totalTime,
                summary.totalSize,
                summary.totalWarnings,
                summary.totalErrors,
                reportSummary);
        }

        private static string SingleLine(string value)
        {
            return (value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        public void WriteAllText(string fullPath, string contents)
        {
            string root = ProductionPlayerBuilderPath.Normalize(ProjectRoot);
            string destination = ProductionPlayerBuilderPath.Normalize(fullPath);
            if (!ProductionPlayerBuilder.IsGuardedSummaryPath(root, destination) ||
                HasReparsePoint(destination))
            {
                throw new IOException("Build summary destination is not the exact guarded non-reparse path.");
            }

            string temporary = destination + ".tmp-" + System.Diagnostics.Process.GetCurrentProcess().Id;
            byte[] payload = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(contents ?? string.Empty);
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

                // Replace the directory entry atomically when a prior summary exists. This also
                // severs an ordinary hard link without ever opening its other name for write.
                // Reparse leaves were rejected above and are checked again for every write.
                if (File.Exists(destination))
                {
                    File.Replace(temporary, destination, null);
                }
                else
                {
                    File.Move(temporary, destination);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static string FindRepositoryRoot(string startPath)
        {
            var cursor = new DirectoryInfo(startPath);
            while (cursor != null)
            {
                string marker = Path.Combine(cursor.FullName, ".git");
                if (Directory.Exists(marker) || File.Exists(marker))
                {
                    return ProductionPlayerBuilderPath.Normalize(cursor.FullName);
                }

                cursor = cursor.Parent;
            }

            return string.Empty;
        }

        private static GitCommandResult RunGit(string repositoryRoot, string arguments)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = repositoryRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return GitCommandResult.Failed();
                    }

                    var standardOutput = new StringBuilder();
                    var standardError = new StringBuilder();
                    process.OutputDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data != null)
                        {
                            lock (standardOutput)
                            {
                                standardOutput.AppendLine(eventArgs.Data);
                            }
                        }
                    };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data != null)
                        {
                            lock (standardError)
                            {
                                standardError.AppendLine(eventArgs.Data);
                            }
                        }
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(10000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // The preflight remains failed regardless of cleanup outcome.
                        }

                        return GitCommandResult.Failed();
                    }

                    // A parameterless wait after the timed wait lets asynchronous stream callbacks
                    // flush without reopening an unbounded wait on a still-running Git process.
                    process.WaitForExit();
                    string output;
                    lock (standardOutput)
                    {
                        output = standardOutput.ToString();
                    }

                    return new GitCommandResult(process.ExitCode, output);
                }
            }
            catch
            {
                return GitCommandResult.Failed();
            }
        }

        private static string QuoteGitArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private sealed class GitCommandResult
        {
            internal GitCommandResult(int exitCode, string standardOutput)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
            }

            internal int ExitCode { get; }
            internal string StandardOutput { get; }

            internal static GitCommandResult Failed() => new GitCommandResult(-1, string.Empty);
        }
    }

    /// <summary>Small path helper shared only by the production environment; no policy duplication.</summary>
    internal static class ProductionPlayerBuilderPath
    {
        internal static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string full = Path.GetFullPath(path);
            string volumeRoot = Path.GetPathRoot(full) ?? string.Empty;
            return full.Length > volumeRoot.Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }

        internal static bool IsDescendant(string candidate, string parent)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(parent))
            {
                return false;
            }

            return candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDescendantOrSame(string candidate, string parent) =>
            string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase) || IsDescendant(candidate, parent);
    }

    internal sealed class BuildValidationSnapshot
    {
        internal BuildValidationSnapshot(bool isValid, string summary)
        {
            IsValid = isValid;
            Summary = summary ?? string.Empty;
        }

        internal bool IsValid { get; }
        internal string Summary { get; }

        internal static BuildValidationSnapshot Invalid(string summary) =>
            new BuildValidationSnapshot(false, summary);
    }

    internal sealed class PlayerBuildPlan
    {
        private readonly ReadOnlyCollection<string> _scenePaths;
        private readonly ReadOnlyCollection<string> _failures;

        internal PlayerBuildPlan(
            string projectRoot,
            string outputDirectory,
            string outputPath,
            string summaryPath,
            string unityVersion,
            IEnumerable<string> scenePaths,
            IEnumerable<string> failures,
            bool canWriteSummary)
        {
            ProjectRoot = projectRoot ?? string.Empty;
            OutputDirectory = outputDirectory ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            SummaryPath = summaryPath ?? string.Empty;
            UnityVersion = unityVersion ?? string.Empty;
            _scenePaths = Array.AsReadOnly((scenePaths ?? Array.Empty<string>()).ToArray());
            _failures = Array.AsReadOnly((failures ?? Array.Empty<string>()).ToArray());
            CanWriteSummary = canWriteSummary;
        }

        internal string ProjectRoot { get; }
        internal string OutputDirectory { get; }
        internal string OutputPath { get; }
        internal string DataDirectoryPath => Path.Combine(OutputDirectory, ProductionPlayerBuilder.DataDirectoryName);
        internal string SummaryPath { get; }
        internal string UnityVersion { get; }
        internal IReadOnlyList<string> ScenePaths => _scenePaths;
        internal IReadOnlyList<string> Failures => _failures;
        internal BuildTarget Target => BuildTarget.StandaloneWindows64;
        internal BuildOptions Options => BuildOptions.Development;
        internal bool IsValid => _failures.Count == 0;
        internal bool CanWriteSummary { get; }

        internal BuildPlayerOptions CreateBuildPlayerOptions()
        {
            return new BuildPlayerOptions
            {
                scenes = _scenePaths.ToArray(),
                locationPathName = OutputPath,
                target = Target,
                options = Options
            };
        }

        internal string SummarizeFailures() =>
            _failures.Count == 0 ? string.Empty : string.Join(" ", _failures);
    }

    internal sealed class PlayerBuildReportSnapshot
    {
        internal PlayerBuildReportSnapshot(
            string result,
            string target,
            string outputPath,
            TimeSpan totalTime,
            ulong totalSize,
            int warningCount,
            int errorCount,
            string reportSummary)
        {
            Result = result ?? BuildResult.Unknown.ToString();
            Target = target ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            TotalTime = totalTime < TimeSpan.Zero ? TimeSpan.Zero : totalTime;
            TotalSize = totalSize;
            WarningCount = Math.Max(0, warningCount);
            ErrorCount = Math.Max(0, errorCount);
            ReportSummary = reportSummary ?? string.Empty;
        }

        internal string Result { get; }
        internal string Target { get; }
        internal string OutputPath { get; }
        internal TimeSpan TotalTime { get; }
        internal ulong TotalSize { get; }
        internal int WarningCount { get; }
        internal int ErrorCount { get; }
        internal string ReportSummary { get; }

        internal static PlayerBuildReportSnapshot NotRun(string target, string outputPath, string message) =>
            new PlayerBuildReportSnapshot("NotRun", target, outputPath, TimeSpan.Zero, 0UL, 0, 1, message);

        internal static PlayerBuildReportSnapshot Exception(string target, string outputPath, string message) =>
            new PlayerBuildReportSnapshot("Exception", target, outputPath, TimeSpan.Zero, 0UL, 0, 1, message);
    }

    internal enum PlayerBuildStatus
    {
        PreflightFailed,
        PreparationFailed,
        BuildFailed,
        ArtifactsMissing,
        SummaryWriteFailed,
        Succeeded
    }

    internal sealed class PlayerBuildSummary
    {
        private readonly ReadOnlyCollection<string> _scenePaths;

        private PlayerBuildSummary(
            PlayerBuildStatus status,
            string target,
            string unityVersion,
            string outputPath,
            IEnumerable<string> scenePaths,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            TimeSpan totalTime,
            ulong totalSize,
            int warningCount,
            int errorCount,
            string buildResult,
            string summaryMessage)
        {
            Status = status;
            Target = target ?? string.Empty;
            UnityVersion = unityVersion ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            _scenePaths = Array.AsReadOnly((scenePaths ?? Array.Empty<string>()).ToArray());
            StartedAtUtc = startedAtUtc.Kind == DateTimeKind.Utc
                ? startedAtUtc
                : DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc);
            EndedAtUtc = endedAtUtc.Kind == DateTimeKind.Utc
                ? endedAtUtc
                : DateTime.SpecifyKind(endedAtUtc, DateTimeKind.Utc);
            TotalTime = totalTime < TimeSpan.Zero ? TimeSpan.Zero : totalTime;
            TotalSize = totalSize;
            WarningCount = Math.Max(0, warningCount);
            ErrorCount = Math.Max(0, errorCount);
            BuildResult = buildResult ?? string.Empty;
            SummaryMessage = summaryMessage ?? string.Empty;
        }

        internal PlayerBuildStatus Status { get; }
        internal string Target { get; }
        internal string UnityVersion { get; }
        internal string OutputPath { get; }
        internal IReadOnlyList<string> ScenePaths => _scenePaths;
        internal DateTime StartedAtUtc { get; }
        internal DateTime EndedAtUtc { get; }
        internal TimeSpan TotalTime { get; }
        internal ulong TotalSize { get; }
        internal int WarningCount { get; }
        internal int ErrorCount { get; }
        internal string BuildResult { get; }
        internal string SummaryMessage { get; }
        internal bool Succeeded => Status == PlayerBuildStatus.Succeeded;

        internal string Summarize()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "status={0}; target={1}; unityVersion={2}; outputPath={3}; scenes=[{4}]; startedAtUtc={5}; endedAtUtc={6}; totalTime={7}; totalSize={8}; warnings={9}; errors={10}; buildResult={11}; message={12}",
                Status,
                Target,
                UnityVersion,
                OutputPath,
                string.Join(",", _scenePaths),
                StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                EndedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                TotalTime.ToString("c", CultureInfo.InvariantCulture),
                TotalSize,
                WarningCount,
                ErrorCount,
                BuildResult,
                SummaryMessage);
        }

        internal static PlayerBuildSummary PreflightFailure(
            string outputPath,
            string target,
            IEnumerable<string> scenes,
            string unityVersion,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            string message) =>
            new PlayerBuildSummary(
                PlayerBuildStatus.PreflightFailed,
                target,
                unityVersion,
                outputPath,
                scenes,
                startedAtUtc,
                endedAtUtc,
                TimeSpan.Zero,
                0UL,
                0,
                0,
                "NotRun",
                message);

        internal static PlayerBuildSummary PreparationFailure(
            PlayerBuildPlan plan,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            string message) =>
            FromPlan(PlayerBuildStatus.PreparationFailed, plan, null, startedAtUtc, endedAtUtc, "NotRun", message);

        internal static PlayerBuildSummary BuildFailure(
            PlayerBuildPlan plan,
            PlayerBuildReportSnapshot report,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            string message) =>
            FromPlan(PlayerBuildStatus.BuildFailed, plan, report, startedAtUtc, endedAtUtc, report.Result, message);

        internal static PlayerBuildSummary ArtifactFailure(
            PlayerBuildPlan plan,
            PlayerBuildReportSnapshot report,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            string message) =>
            FromPlan(PlayerBuildStatus.ArtifactsMissing, plan, report, startedAtUtc, endedAtUtc, report.Result, message);

        internal static PlayerBuildSummary Success(
            PlayerBuildPlan plan,
            PlayerBuildReportSnapshot report,
            DateTime startedAtUtc,
            DateTime endedAtUtc) =>
            FromPlan(
                PlayerBuildStatus.Succeeded,
                plan,
                report,
                startedAtUtc,
                endedAtUtc,
                report.Result,
                "Windows64 Development Player build succeeded and required artifacts were verified.");

        internal static PlayerBuildSummary SummaryWriteFailure(PlayerBuildSummary source, string message) =>
            new PlayerBuildSummary(
                PlayerBuildStatus.SummaryWriteFailed,
                source.Target,
                source.UnityVersion,
                source.OutputPath,
                source.ScenePaths,
                source.StartedAtUtc,
                source.EndedAtUtc,
                source.TotalTime,
                source.TotalSize,
                source.WarningCount,
                Math.Max(1, source.ErrorCount),
                source.BuildResult,
                "Build summary could not be written: " + message);

        private static PlayerBuildSummary FromPlan(
            PlayerBuildStatus status,
            PlayerBuildPlan plan,
            PlayerBuildReportSnapshot report,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            string buildResult,
            string message) =>
            new PlayerBuildSummary(
                status,
                plan.Target.ToString(),
                plan.UnityVersion,
                plan.OutputPath,
                plan.ScenePaths,
                startedAtUtc,
                endedAtUtc,
                report?.TotalTime ?? TimeSpan.Zero,
                report?.TotalSize ?? 0UL,
                report?.WarningCount ?? 0,
                report?.ErrorCount ?? (status == PlayerBuildStatus.Succeeded ? 0 : 1),
                buildResult,
                message);
    }
}
#endif
