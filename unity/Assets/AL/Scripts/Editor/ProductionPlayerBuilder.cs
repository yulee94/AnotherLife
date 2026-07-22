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
    public enum ProductionBuildValidationStatus
    {
        Valid,
        MissingBuildSettings,
        EmptyBuildSettings,
        WrongEntryScene,
        MissingRequiredScene,
        UnexpectedScene,
        DeferredSceneEnabled,
        TestSceneEnabled,
        DisabledStaleScene,
        MissingPath,
        DuplicatePath,
        DuplicateName,
        GuidMismatch,
        DescriptorDrift,
        TransitionUnavailable,
        DeferredTransitionReachable
    }

    /// <summary>Immutable, testable projection of one serialized Build Settings entry.</summary>
    public sealed class ProductionBuildSceneEntry
    {
        public ProductionBuildSceneEntry(
            string path,
            bool enabled,
            string serializedGuid,
            string resolvedGuid,
            bool assetExists)
        {
            Path = path ?? string.Empty;
            Enabled = enabled;
            SerializedGuid = serializedGuid ?? string.Empty;
            ResolvedGuid = resolvedGuid ?? string.Empty;
            AssetExists = assetExists;
        }

        public string Path { get; }
        public bool Enabled { get; }
        public string SerializedGuid { get; }
        public string ResolvedGuid { get; }
        public bool AssetExists { get; }
        public string SceneName => System.IO.Path.GetFileNameWithoutExtension(Path) ?? string.Empty;
    }

    public sealed class ProductionBuildValidationIssue
    {
        public ProductionBuildValidationIssue(ProductionBuildValidationStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public ProductionBuildValidationStatus Status { get; }
        public string Message { get; }
    }

    /// <summary>Complete deterministic validation result; a valid report contains only Valid.</summary>
    public sealed class ProductionBuildSettingsReport
    {
        public ProductionBuildSettingsReport(IEnumerable<ProductionBuildValidationIssue> issues)
        {
            var normalized = (issues ?? Array.Empty<ProductionBuildValidationIssue>())
                .Where(issue => issue != null)
                .Where(issue => issue.Status != ProductionBuildValidationStatus.Valid)
                .ToList();

            if (normalized.Count == 0)
            {
                normalized.Add(new ProductionBuildValidationIssue(
                    ProductionBuildValidationStatus.Valid,
                    "Build Settings match the ShellFoundation descriptor."));
            }

            Issues = normalized.AsReadOnly();
            Outcomes = normalized.Select(issue => issue.Status).Distinct().ToList().AsReadOnly();
        }

        public IReadOnlyList<ProductionBuildValidationIssue> Issues { get; }
        public IReadOnlyList<ProductionBuildValidationStatus> Outcomes { get; }
        public bool IsValid => Outcomes.Count == 1 && Outcomes[0] == ProductionBuildValidationStatus.Valid;

        public ProductionBuildSettingsReport WithIssue(ProductionBuildValidationStatus status, string message)
        {
            return new ProductionBuildSettingsReport(Issues.Concat(new[] { new ProductionBuildValidationIssue(status, message) }));
        }

        public string Summarize()
        {
            var builder = new StringBuilder();
            builder.Append("[AL-PRODUCTION-BUILD-SETTINGS] valid=").Append(IsValid);
            foreach (ProductionBuildValidationIssue issue in Issues)
            {
                builder.Append('\n').Append("  ").Append(issue.Status).Append(": ").Append(issue.Message);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Strict ShellFoundation Build Settings validator. The production descriptor owns the scene list;
    /// this class checks its committed serialization, GUIDs, structure, and transition applicability.
    /// Validation never rewrites Build Settings.
    /// </summary>
    public static class ProductionBuildSettingsValidator
    {
        public static ProductionBuildSettingsReport ValidateCurrent()
        {
            string settingsPath = Path.Combine(ProjectRoot(), "ProjectSettings", "EditorBuildSettings.asset");
            if (!File.Exists(settingsPath))
            {
                return Report(ProductionBuildValidationStatus.MissingBuildSettings,
                    $"Build Settings asset is missing at '{settingsPath}'.");
            }

            EditorBuildSettingsScene[] scenes;
            try
            {
                scenes = EditorBuildSettings.scenes;
            }
            catch (Exception exception)
            {
                return Report(ProductionBuildValidationStatus.MissingBuildSettings,
                    "Build Settings could not be read: " + exception.Message);
            }

            var entries = (scenes ?? Array.Empty<EditorBuildSettingsScene>())
                .Select(scene => ToEntry(scene))
                .ToArray();

            ProductionBuildSettingsReport report = ValidateEntries(entries);
            SceneValidationReport sceneReport = ProductionSceneValidator.Validate();
            if (!sceneReport.IsValid)
            {
                report = report.WithIssue(
                    ProductionBuildValidationStatus.DescriptorDrift,
                    "Production scene structure/marker validation failed. " + sceneReport.Summarize());
            }

            return report;
        }

        /// <summary>Pure entry validation used by EditMode tests and the current-settings adapter.</summary>
        public static ProductionBuildSettingsReport ValidateEntries(IEnumerable<ProductionBuildSceneEntry> entries)
        {
            if (entries == null)
            {
                return Report(ProductionBuildValidationStatus.MissingBuildSettings,
                    "Build Settings entries are unavailable.");
            }

            List<ProductionBuildSceneEntry> actual = entries.ToList();
            if (actual.Count == 0)
            {
                return Report(ProductionBuildValidationStatus.EmptyBuildSettings,
                    "Build Settings contain no scenes.");
            }

            var issues = new List<ProductionBuildValidationIssue>();
            IReadOnlyList<ProductionSceneRecord> expected = ProductionSceneDescriptor.ShellFoundationOrdered;
            ValidateDescriptor(expected, issues);

            if (!string.Equals(actual[0].Path, expected[0].AssetPath, StringComparison.Ordinal))
            {
                Add(issues, ProductionBuildValidationStatus.WrongEntryScene,
                    $"Build index 0 is '{actual[0].Path}', expected '{expected[0].AssetPath}'.");
            }

            var expectedByPath = expected.ToDictionary(record => record.AssetPath, StringComparer.Ordinal);
            var expectedPaths = new HashSet<string>(expectedByPath.Keys, StringComparer.Ordinal);

            foreach (IGrouping<string, ProductionBuildSceneEntry> duplicate in actual
                         .GroupBy(entry => entry.Path, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                Add(issues, ProductionBuildValidationStatus.DuplicatePath,
                    $"Build Settings contain duplicate path '{duplicate.Key}'.");
            }

            foreach (IGrouping<string, ProductionBuildSceneEntry> duplicate in actual
                         .GroupBy(entry => entry.SceneName, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                Add(issues, ProductionBuildValidationStatus.DuplicateName,
                    $"Build Settings contain duplicate scene name '{duplicate.Key}'.");
            }

            ProductionSceneRecord testRecord = Record(ProductionSceneDescriptor.TestSceneId);
            ProductionSceneRecord deferredRecord = Record(ProductionSceneDescriptor.ChampionArenaSceneId);

            for (int index = 0; index < actual.Count; index++)
            {
                ProductionBuildSceneEntry entry = actual[index];
                if (!entry.Enabled)
                {
                    Add(issues, ProductionBuildValidationStatus.DisabledStaleScene,
                        $"Disabled/stale Build Settings entry at index {index}: '{entry.Path}'.");
                }

                if (string.Equals(entry.Path, testRecord.AssetPath, StringComparison.Ordinal))
                {
                    Add(issues, ProductionBuildValidationStatus.TestSceneEnabled,
                        $"Representative test scene is listed in Build Settings: '{entry.Path}'.");
                }

                if (string.Equals(entry.Path, deferredRecord.AssetPath, StringComparison.Ordinal))
                {
                    Add(issues, ProductionBuildValidationStatus.DeferredSceneEnabled,
                        $"Deferred ChampionArena scene is listed before its applicability gates: '{entry.Path}'.");
                }

                if (!expectedPaths.Contains(entry.Path))
                {
                    Add(issues, ProductionBuildValidationStatus.UnexpectedScene,
                        $"Unexpected Build Settings scene at index {index}: '{entry.Path}'.");
                }
                else
                {
                    ProductionSceneRecord record = expectedByPath[entry.Path];
                    if (!string.Equals(entry.ResolvedGuid, record.AssetGuid, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(entry.SerializedGuid) &&
                         !string.Equals(entry.SerializedGuid, record.AssetGuid, StringComparison.Ordinal)))
                    {
                        Add(issues, ProductionBuildValidationStatus.GuidMismatch,
                            $"GUID mismatch for '{entry.Path}': serialized='{entry.SerializedGuid}', resolved='{entry.ResolvedGuid}', expected='{record.AssetGuid}'.");
                    }
                }

                if (!entry.AssetExists)
                {
                    Add(issues, ProductionBuildValidationStatus.MissingPath,
                        $"Build Settings scene asset is missing: '{entry.Path}'.");
                }

                if (index < expected.Count && !string.Equals(entry.Path, expected[index].AssetPath, StringComparison.Ordinal))
                {
                    Add(issues, ProductionBuildValidationStatus.UnexpectedScene,
                        $"Wrong ShellFoundation order at index {index}: found '{entry.Path}', expected '{expected[index].AssetPath}'.");
                }
            }

            foreach (ProductionSceneRecord required in expected)
            {
                if (!actual.Any(entry => string.Equals(entry.Path, required.AssetPath, StringComparison.Ordinal)))
                {
                    Add(issues, ProductionBuildValidationStatus.MissingRequiredScene,
                        $"Required ShellFoundation scene is absent: '{required.AssetPath}'.");
                }
            }

            ValidateTransitions(expected, issues);
            return new ProductionBuildSettingsReport(issues);
        }

        private static void ValidateDescriptor(
            IReadOnlyList<ProductionSceneRecord> expected,
            ICollection<ProductionBuildValidationIssue> issues)
        {
            IReadOnlyList<ProductionSceneRecord> profileRecords = ProductionSceneDescriptor.All
                .Where(record => record.BuildProfiles.Contains(ProductionSceneDescriptor.ShellFoundationProfile))
                .ToList();

            bool profileMatches = expected != null &&
                                  expected.Count > 0 &&
                                  profileRecords.Select(record => record.SceneId)
                                      .SequenceEqual(expected.Select(record => record.SceneId), StringComparer.Ordinal);
            if (!profileMatches)
            {
                Add(issues, ProductionBuildValidationStatus.DescriptorDrift,
                    "ShellFoundationOrdered does not match descriptor profile applicability.");
                return;
            }

            if (!string.Equals(expected[0].SceneId, ProductionSceneDescriptor.BootSceneId, StringComparison.Ordinal) ||
                expected.Any(record => !record.IsProductionScene ||
                                       !string.Equals(record.Status, ProductionSceneDescriptor.StatusCommittedActive, StringComparison.Ordinal)) ||
                expected.Select(record => record.AssetPath).Distinct(StringComparer.Ordinal).Count() != expected.Count ||
                expected.Select(record => record.SceneName).Distinct(StringComparer.Ordinal).Count() != expected.Count ||
                expected.Select(record => record.AssetGuid).Distinct(StringComparer.Ordinal).Count() != expected.Count)
            {
                Add(issues, ProductionBuildValidationStatus.DescriptorDrift,
                    "ShellFoundation descriptor invariants (Boot entry, active production status, or uniqueness) are invalid.");
            }

            ProductionSceneRecord test = Record(ProductionSceneDescriptor.TestSceneId);
            ProductionSceneRecord champion = Record(ProductionSceneDescriptor.ChampionArenaSceneId);
            if (test.BuildProfiles.Count != 0 || test.IsProductionScene ||
                champion.BuildProfiles.Count != 0 ||
                !string.Equals(champion.Status, ProductionSceneDescriptor.StatusCommittedDeferred, StringComparison.Ordinal))
            {
                Add(issues, ProductionBuildValidationStatus.DescriptorDrift,
                    "Test/Champion descriptor applicability no longer matches the ShellFoundation policy.");
            }
        }

        private static void ValidateTransitions(
            IReadOnlyList<ProductionSceneRecord> expected,
            ICollection<ProductionBuildValidationIssue> issues)
        {
            var shellIds = new HashSet<string>(expected.Select(record => record.SceneId), StringComparer.Ordinal);
            foreach (ProductionSceneRecord source in ProductionSceneDescriptor.ProductionScenes)
            {
                foreach (SceneTransition transition in source.TransitionTargets)
                {
                    if (!ProductionSceneDescriptor.TryGetById(transition.TargetSceneId, out ProductionSceneRecord target) ||
                        !string.Equals(transition.SerializedValue, target.SceneName, StringComparison.Ordinal))
                    {
                        Add(issues, ProductionBuildValidationStatus.TransitionUnavailable,
                            $"Transition '{source.SceneId}' -> '{transition.TargetSceneId}' does not resolve exactly in the descriptor.");
                        continue;
                    }

                    if (transition.Status == TransitionStatus.Active &&
                        shellIds.Contains(source.SceneId) &&
                        !shellIds.Contains(target.SceneId))
                    {
                        Add(issues, ProductionBuildValidationStatus.TransitionUnavailable,
                            $"Active ShellFoundation transition '{source.SceneId}' targets unavailable scene '{target.SceneId}'.");
                    }

                    if (transition.Status == TransitionStatus.Deferred &&
                        (shellIds.Contains(target.SceneId) || transition.HasSerializedField))
                    {
                        Add(issues, ProductionBuildValidationStatus.DeferredTransitionReachable,
                            $"Deferred transition '{source.SceneId}' -> '{target.SceneId}' is represented as reachable.");
                    }
                }
            }

            if (ProductionSceneDescriptor.IsResetToBootProductionReachable)
            {
                Add(issues, ProductionBuildValidationStatus.DeferredTransitionReachable,
                    "Unsafe reset-to-Boot is marked production reachable.");
            }
        }

        private static ProductionBuildSceneEntry ToEntry(EditorBuildSettingsScene scene)
        {
            string path = scene?.path ?? string.Empty;
            string serializedGuid = scene == null ? string.Empty : scene.guid.ToString();
            string resolvedGuid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            bool exists = !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
            return new ProductionBuildSceneEntry(path, scene != null && scene.enabled, serializedGuid, resolvedGuid, exists);
        }

        private static ProductionSceneRecord Record(string sceneId)
        {
            if (!ProductionSceneDescriptor.TryGetById(sceneId, out ProductionSceneRecord record))
            {
                throw new InvalidOperationException("Production descriptor is missing required record '" + sceneId + "'.");
            }

            return record;
        }

        private static ProductionBuildSettingsReport Report(ProductionBuildValidationStatus status, string message)
        {
            return new ProductionBuildSettingsReport(new[] { new ProductionBuildValidationIssue(status, message) });
        }

        private static void Add(
            ICollection<ProductionBuildValidationIssue> issues,
            ProductionBuildValidationStatus status,
            string message)
        {
            issues.Add(new ProductionBuildValidationIssue(status, message));
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }

    public enum ProductionPlayerBuildStatus
    {
        Succeeded,
        PreflightFailed,
        BuildFailed,
        MissingOutput,
        Exception
    }

    public sealed class ProductionPlayerBuildPreflightReport
    {
        public ProductionPlayerBuildPreflightReport(
            ProductionBuildSettingsReport buildSettings,
            bool unityVersionValid,
            bool compilationValid,
            bool buildTargetSupported,
            bool outputPathValid,
            bool outputPathIgnored,
            IEnumerable<string> failures)
        {
            BuildSettings = buildSettings;
            UnityVersionValid = unityVersionValid;
            CompilationValid = compilationValid;
            BuildTargetSupported = buildTargetSupported;
            OutputPathValid = outputPathValid;
            OutputPathIgnored = outputPathIgnored;
            Failures = (failures ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public ProductionBuildSettingsReport BuildSettings { get; }
        public bool UnityVersionValid { get; }
        public bool CompilationValid { get; }
        public bool BuildTargetSupported { get; }
        public bool OutputPathValid { get; }
        public bool OutputPathIgnored { get; }
        public IReadOnlyList<string> Failures { get; }
        public bool IsValid => BuildSettings != null && BuildSettings.IsValid && Failures.Count == 0;

        public string Summarize()
        {
            var builder = new StringBuilder("[AL-PLAYER-BUILD-PREFLIGHT] valid=").Append(IsValid);
            if (BuildSettings != null)
            {
                builder.Append('\n').Append(BuildSettings.Summarize());
            }

            foreach (string failure in Failures)
            {
                builder.Append('\n').Append("  - ").Append(failure);
            }

            return builder.ToString();
        }
    }

    public sealed class ProductionPlayerBuildResult
    {
        internal ProductionPlayerBuildResult(
            ProductionPlayerBuildStatus status,
            BuildTarget target,
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
            BuildReport report)
        {
            Status = status;
            Target = target;
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
            Report = report;
        }

        public ProductionPlayerBuildStatus Status { get; }
        public BuildTarget Target { get; }
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
        public BuildReport Report { get; }
        public bool Succeeded => Status == ProductionPlayerBuildStatus.Succeeded && BuildResult == BuildResult.Succeeded;

        public string Summarize()
        {
            return $"[AL-PLAYER-BUILD-RESULT] status={Status} target={Target} unity={UnityVersion} " +
                   $"buildResult={BuildResult} output='{OutputPath}' scenes={ScenePaths.Count} " +
                   $"duration={TotalTime.TotalSeconds:0.###}s size={TotalSize} warnings={WarningCount} errors={ErrorCount} " +
                   $"message='{SummaryMessage}'";
        }
    }

    public enum ProductionPlayerLaunchValidationStatus
    {
        Passed,
        ProfileIsolationFailed,
        MissingBootMarker,
        MissingBootSequence,
        MissingFreshProfileTransition,
        MissingRealmSelectionMarker,
        WrongOrder,
        WrongProfileBranch,
        ForbiddenSceneActivated,
        SevereLog,
        ProcessExitedEarly
    }

    public sealed class ProductionPlayerLaunchValidationResult
    {
        public ProductionPlayerLaunchValidationResult(
            IEnumerable<ProductionPlayerLaunchValidationStatus> outcomes,
            IEnumerable<string> failures,
            bool externalTerminationAllowed)
        {
            var failureList = (failures ?? Array.Empty<string>()).ToList();
            var statusList = (outcomes ?? Array.Empty<ProductionPlayerLaunchValidationStatus>())
                .Where(status => status != ProductionPlayerLaunchValidationStatus.Passed)
                .Distinct()
                .ToList();
            if (statusList.Count == 0)
            {
                statusList.Add(ProductionPlayerLaunchValidationStatus.Passed);
            }

            Outcomes = statusList.AsReadOnly();
            Failures = failureList.AsReadOnly();
            ExternalTerminationAllowed = externalTerminationAllowed &&
                                         statusList.Count == 1 &&
                                         statusList[0] == ProductionPlayerLaunchValidationStatus.Passed;
        }

        public IReadOnlyList<ProductionPlayerLaunchValidationStatus> Outcomes { get; }
        public IReadOnlyList<string> Failures { get; }
        public bool IsValid => Outcomes.Count == 1 && Outcomes[0] == ProductionPlayerLaunchValidationStatus.Passed;
        public bool ExternalTerminationAllowed { get; }
        public string TerminationDisposition => ExternalTerminationAllowed
            ? "process may be terminated externally for validation; no graceful quit/save claim"
            : "external termination is not authorized before successful transition evidence";
    }

    /// <summary>Pure ordered-log validator used by the external disposable-profile launch harness.</summary>
    public static class ProductionPlayerLaunchLogValidator
    {
        private const string BootMarker = "[AL-SCENE-ACTIVE] id=al_scene_boot ";
        private const string BootSequence = "AL Boot Sequence Started...";
        private const string FreshProfileTransition = "No Realm Selected. Transitioning to Realm Selection...";
        private const string RealmSelectionMarker = "[AL-SCENE-ACTIVE] id=al_scene_realm_selection ";

        private static readonly string[] SevereTokens =
        {
            "ArgumentException",
            "MissingReferenceException",
            "MissingMethodException",
            "NullReferenceException",
            "Assertion failed",
            "Unhandled Exception",
            "Scene couldn't be loaded",
            "is not in the build settings",
            "[AL-SCENE-ACTIVE-MISMATCH]"
        };

        public static ProductionPlayerLaunchValidationResult Evaluate(
            string logText,
            bool processExited,
            bool isolatedProfileVerified)
        {
            string log = logText ?? string.Empty;
            var statuses = new List<ProductionPlayerLaunchValidationStatus>();
            var failures = new List<string>();

            if (!isolatedProfileVerified)
            {
                Add(statuses, failures, ProductionPlayerLaunchValidationStatus.ProfileIsolationFailed,
                    "Disposable Player profile isolation was not verified.");
            }

            int boot = log.IndexOf(BootMarker, StringComparison.Ordinal);
            int sequence = log.IndexOf(BootSequence, StringComparison.Ordinal);
            int transition = log.IndexOf(FreshProfileTransition, StringComparison.Ordinal);
            int realm = log.IndexOf(RealmSelectionMarker, StringComparison.Ordinal);

            Require(boot, ProductionPlayerLaunchValidationStatus.MissingBootMarker, "Boot startup marker is missing.", statuses, failures);
            Require(sequence, ProductionPlayerLaunchValidationStatus.MissingBootSequence, "Boot sequence log is missing.", statuses, failures);
            Require(transition, ProductionPlayerLaunchValidationStatus.MissingFreshProfileTransition,
                "Fresh-profile transition log is missing.", statuses, failures);
            Require(realm, ProductionPlayerLaunchValidationStatus.MissingRealmSelectionMarker,
                "RealmSelection startup marker is missing.", statuses, failures);

            if (boot >= 0 && sequence >= 0 && transition >= 0 && realm >= 0 &&
                !(boot < sequence && sequence < transition && transition < realm))
            {
                Add(statuses, failures, ProductionPlayerLaunchValidationStatus.WrongOrder,
                    "Required Boot -> boot sequence -> fresh transition -> RealmSelection evidence is out of order.");
            }

            if (log.Contains("[AL-SCENE-ACTIVE] id=al_scene_kingdom "))
            {
                Add(statuses, failures, ProductionPlayerLaunchValidationStatus.WrongProfileBranch,
                    "Kingdom activated during the fresh-profile smoke.");
            }

            if (log.Contains("[AL-SCENE-ACTIVE] id=al_scene_champion_arena ") ||
                log.Contains("[AL-SCENE-ACTIVE] id=al_scene_test_representative "))
            {
                Add(statuses, failures, ProductionPlayerLaunchValidationStatus.ForbiddenSceneActivated,
                    "A deferred or representative test scene activated during the ShellFoundation smoke.");
            }

            foreach (string token in SevereTokens.Where(log.Contains))
            {
                Add(statuses, failures, ProductionPlayerLaunchValidationStatus.SevereLog,
                    "Severe Player log token found: " + token);
            }

            bool reachedRealm = realm >= 0 && boot >= 0 && realm > boot;
            if (processExited && !reachedRealm)
            {
                Add(statuses, failures, ProductionPlayerLaunchValidationStatus.ProcessExitedEarly,
                    "Player exited before the RealmSelection marker was observed.");
            }

            return new ProductionPlayerLaunchValidationResult(statuses, failures, reachedRealm);
        }

        private static void Require(
            int index,
            ProductionPlayerLaunchValidationStatus status,
            string message,
            ICollection<ProductionPlayerLaunchValidationStatus> statuses,
            ICollection<string> failures)
        {
            if (index < 0)
            {
                Add(statuses, failures, status, message);
            }
        }

        private static void Add(
            ICollection<ProductionPlayerLaunchValidationStatus> statuses,
            ICollection<string> failures,
            ProductionPlayerLaunchValidationStatus status,
            string message)
        {
            statuses.Add(status);
            failures.Add(message);
        }
    }

    /// <summary>Fail-closed Windows64 Development Player builder for the ShellFoundation profile.</summary>
    public static class ProductionPlayerBuilder
    {
        public const string ExpectedUnityVersion = "2022.3.62f3";
        public const string RelativeOutputPath = "Builds/Validation/Windows64/AnotherLifeUnity.exe";
        public const string SummaryFileName = "PlayerBuildWindows64.summary.json";
        public const string ReportFileName = "PlayerBuildWindows64.report.txt";

        // Test seam. Production leaves this null and invokes BuildPipeline.BuildPlayer directly.
        internal static Func<BuildPlayerOptions, BuildReport> BuildPlayerOverride;

        public static string OutputPath => Path.GetFullPath(Path.Combine(ProjectRoot(), RelativeOutputPath));

        /// <summary>Unity -executeMethod entry. Any non-success throws and therefore fails batch mode.</summary>
        public static void BuildWindows64Development()
        {
            ProductionPlayerBuildResult result = BuildWindows64DevelopmentPlayer();
            Debug.Log(result.Summarize());
            if (!result.Succeeded)
            {
                throw new BuildFailedException(result.Summarize());
            }
        }

        public static ProductionPlayerBuildResult BuildWindows64DevelopmentPlayer()
        {
            DateTime startedAtUtc = DateTime.UtcNow;
            string[] scenes = ProductionSceneDescriptor.ShellFoundationOrdered.Select(record => record.AssetPath).ToArray();

            if (IsOutputPathSafe(OutputPath))
            {
                CleanExactOutputDirectory();
            }

            ProductionPlayerBuildPreflightReport preflight = ValidatePreflight();
            if (!preflight.IsValid)
            {
                ProductionPlayerBuildResult failed = Result(
                    ProductionPlayerBuildStatus.PreflightFailed,
                    scenes,
                    startedAtUtc,
                    DateTime.UtcNow,
                    TimeSpan.Zero,
                    0,
                    0,
                    1,
                    BuildResult.Unknown,
                    preflight.Summarize(),
                    null);
                WriteArtifacts(failed);
                return failed;
            }

            BuildReport report;
            try
            {
                BuildPlayerOptions options = CreateWindows64DevelopmentOptions();
                report = BuildPlayerOverride != null
                    ? BuildPlayerOverride(options)
                    : BuildPipeline.BuildPlayer(options);
            }
            catch (Exception exception)
            {
                ProductionPlayerBuildResult failed = Result(
                    ProductionPlayerBuildStatus.Exception,
                    scenes,
                    startedAtUtc,
                    DateTime.UtcNow,
                    DateTime.UtcNow - startedAtUtc,
                    0,
                    0,
                    1,
                    BuildResult.Failed,
                    exception.ToString(),
                    null);
                WriteArtifacts(failed);
                return failed;
            }

            if (report == null)
            {
                ProductionPlayerBuildResult missingReport = Result(
                    ProductionPlayerBuildStatus.BuildFailed,
                    scenes,
                    startedAtUtc,
                    DateTime.UtcNow,
                    DateTime.UtcNow - startedAtUtc,
                    0,
                    0,
                    1,
                    BuildResult.Unknown,
                    "BuildPipeline returned no BuildReport.",
                    null);
                WriteArtifacts(missingReport);
                return missingReport;
            }

            BuildSummary summary = report.summary;
            string dataDirectory = Path.Combine(Path.GetDirectoryName(OutputPath) ?? string.Empty, "AnotherLifeUnity_Data");
            bool executableExists = File.Exists(OutputPath);
            bool dataDirectoryExists = Directory.Exists(dataDirectory);
            ProductionPlayerBuildStatus status = ClassifyBuildResult(summary.result, executableExists, dataDirectoryExists);
            string message = status == ProductionPlayerBuildStatus.Succeeded
                ? "Windows64 Development Player built successfully with required executable and data directory."
                : $"Build validation failed: report={summary.result}, executableExists={executableExists}, dataDirectoryExists={dataDirectoryExists}.";

            ProductionPlayerBuildResult result = new ProductionPlayerBuildResult(
                status,
                BuildTarget.StandaloneWindows64,
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
                report);
            WriteArtifacts(result);
            return result;
        }

        public static ProductionPlayerBuildPreflightReport ValidatePreflight()
        {
            var failures = new List<string>();
            ProductionBuildSettingsReport buildSettings = ProductionBuildSettingsValidator.ValidateCurrent();
            bool unityVersionValid = string.Equals(Application.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal);
            bool compilationValid = !EditorUtility.scriptCompilationFailed && !EditorApplication.isCompiling;
            bool buildTargetSupported = BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            bool outputPathValid = IsOutputPathSafe(OutputPath);
            bool outputPathIgnored = IsOutputPathIgnored();

            if (!unityVersionValid)
            {
                failures.Add($"Unity version '{Application.unityVersion}' does not match required '{ExpectedUnityVersion}'.");
            }

            if (!compilationValid)
            {
                failures.Add("Unity scripts have compile errors or compilation is still active.");
            }

            if (!buildTargetSupported)
            {
                failures.Add("StandaloneWindows64 build support is not installed for this Unity editor.");
            }

            if (!outputPathValid)
            {
                failures.Add("Player output path is not the exact safe validation path outside Assets.");
            }

            if (!outputPathIgnored)
            {
                failures.Add("Player validation output is not covered by the Unity project .gitignore.");
            }

            if (!buildSettings.IsValid)
            {
                failures.Add("Production Build Settings validation failed.");
            }

            return new ProductionPlayerBuildPreflightReport(
                buildSettings,
                unityVersionValid,
                compilationValid,
                buildTargetSupported,
                outputPathValid,
                outputPathIgnored,
                failures);
        }

        internal static BuildPlayerOptions CreateWindows64DevelopmentOptions()
        {
            return new BuildPlayerOptions
            {
                scenes = ProductionSceneDescriptor.ShellFoundationOrdered.Select(record => record.AssetPath).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
        }

        internal static ProductionPlayerBuildStatus ClassifyBuildResult(
            BuildResult buildResult,
            bool executableExists,
            bool dataDirectoryExists)
        {
            if (buildResult != BuildResult.Succeeded)
            {
                return ProductionPlayerBuildStatus.BuildFailed;
            }

            return executableExists && dataDirectoryExists
                ? ProductionPlayerBuildStatus.Succeeded
                : ProductionPlayerBuildStatus.MissingOutput;
        }

        internal static string DescribeBuildIntent()
        {
            return "profile=ShellFoundation target=StandaloneWindows64 options=Development " +
                   "scenes=[" + string.Join(",", ProductionSceneDescriptor.ShellFoundationOrdered.Select(record => record.AssetPath)) + "] " +
                   "excluded=[Assets/Test.unity,Assets/AL/Scenes/ChampionArena.unity] output='" + OutputPath + "'";
        }

        private static ProductionPlayerBuildResult Result(
            ProductionPlayerBuildStatus status,
            IEnumerable<string> scenes,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            TimeSpan totalTime,
            ulong totalSize,
            int warningCount,
            int errorCount,
            BuildResult buildResult,
            string message,
            BuildReport report)
        {
            return new ProductionPlayerBuildResult(
                status,
                BuildTarget.StandaloneWindows64,
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
                report);
        }

        private static void CleanExactOutputDirectory()
        {
            string directory = Path.GetDirectoryName(OutputPath);
            if (string.IsNullOrEmpty(directory) || !IsOutputPathSafe(OutputPath))
            {
                throw new InvalidOperationException("Refusing to clean an unsafe Player output directory.");
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static bool IsOutputPathSafe(string outputPath)
        {
            string projectRoot = ProjectRoot();
            string expected = Path.GetFullPath(Path.Combine(projectRoot, RelativeOutputPath));
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

        private static void WriteArtifacts(ProductionPlayerBuildResult result)
        {
            string directory = Path.GetDirectoryName(OutputPath) ?? ProjectRoot();
            Directory.CreateDirectory(directory);

            var json = new ProductionPlayerBuildSummaryJson
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
                summaryMessage = result.SummaryMessage
            };
            File.WriteAllText(Path.Combine(directory, SummaryFileName), JsonUtility.ToJson(json, prettyPrint: true));
            File.WriteAllText(Path.Combine(directory, ReportFileName), BuildReportText(result));
        }

        private static string BuildReportText(ProductionPlayerBuildResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine(result.Summarize());
            builder.AppendLine(DescribeBuildIntent());
            builder.AppendLine("Test excluded: Assets/Test.unity");
            builder.AppendLine("Champion deferred/excluded: Assets/AL/Scenes/ChampionArena.unity");

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
        private sealed class ProductionPlayerBuildSummaryJson
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
        }
    }
}
#endif
