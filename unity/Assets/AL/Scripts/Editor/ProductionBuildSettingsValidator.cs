#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AL.Core.Scenes;
using UnityEditor;
using UnityEngine;

namespace AL.EditorTools
{
    /// <summary>
    /// Stable outcomes for the ShellFoundation Build Settings contract. Names and ordering follow
    /// Production_Scene_Player_Build_Spec.md section 10 so automation can consume them verbatim.
    /// </summary>
    public enum ProductionBuildSettingsValidationStatus
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

    /// <summary>
    /// Immutable, editor-independent representation of one serialized Build Settings entry. The
    /// public snapshot type keeps validation policy pure and makes malformed settings testable
    /// without writing ProjectSettings/EditorBuildSettings.asset.
    /// </summary>
    public sealed class ProductionBuildSettingsSnapshotEntry
    {
        public ProductionBuildSettingsSnapshotEntry(
            string path,
            string sceneName,
            string assetGuid,
            bool enabled,
            bool pathExists)
        {
            Path = path ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            Enabled = enabled;
            PathExists = pathExists;
        }

        public string Path { get; }
        public string SceneName { get; }
        public string AssetGuid { get; }
        public bool Enabled { get; }
        public bool PathExists { get; }
    }

    /// <summary>One deterministic, immutable Build Settings validation finding.</summary>
    public sealed class ProductionBuildSettingsDiagnostic
    {
        public ProductionBuildSettingsDiagnostic(
            ProductionBuildSettingsValidationStatus status,
            int entryIndex,
            string scenePath,
            string message)
        {
            Status = status;
            EntryIndex = entryIndex;
            ScenePath = scenePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ProductionBuildSettingsValidationStatus Status { get; }
        public int EntryIndex { get; }
        public string ScenePath { get; }
        public string Message { get; }
    }

    /// <summary>Immutable aggregate returned by every ShellFoundation validation path.</summary>
    public sealed class ProductionBuildSettingsValidationReport
    {
        internal ProductionBuildSettingsValidationReport(
            IEnumerable<ProductionBuildSettingsDiagnostic> diagnostics,
            IEnumerable<string> scenePaths)
        {
            Diagnostics = new ReadOnlyCollection<ProductionBuildSettingsDiagnostic>(
                (diagnostics ?? Array.Empty<ProductionBuildSettingsDiagnostic>()).ToArray());
            ScenePaths = new ReadOnlyCollection<string>(
                (scenePaths ?? Array.Empty<string>()).Select(path => path ?? string.Empty).ToArray());
        }

        public bool IsValid => Diagnostics.Count == 0;

        public ProductionBuildSettingsValidationStatus PrimaryStatus =>
            IsValid ? ProductionBuildSettingsValidationStatus.Valid : Diagnostics[0].Status;

        public IReadOnlyList<ProductionBuildSettingsDiagnostic> Diagnostics { get; }
        public IReadOnlyList<string> ScenePaths { get; }

        public string Summarize()
        {
            var builder = new StringBuilder();
            builder.Append("[AL-BUILD-SETTINGS-VALIDATION] valid=")
                .Append(IsValid)
                .Append(" primary=")
                .Append(PrimaryStatus)
                .Append(" scenes=")
                .Append(ScenePaths.Count);

            foreach (ProductionBuildSettingsDiagnostic diagnostic in Diagnostics)
            {
                builder.Append('\n')
                    .Append("  ")
                    .Append(diagnostic.Status)
                    .Append(" entry=")
                    .Append(diagnostic.EntryIndex)
                    .Append(" path='")
                    .Append(diagnostic.ScenePath)
                    .Append("': ")
                    .Append(diagnostic.Message);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Read-only validator for the first production profile. It derives the required list from
    /// ProductionSceneDescriptor, never assigns EditorBuildSettings.scenes, and never writes assets.
    /// </summary>
    public static class ProductionBuildSettingsValidator
    {
        private const string BuildSettingsRelativePath = "ProjectSettings/EditorBuildSettings.asset";

        /// <summary>
        /// Reads the current Unity Build Settings and validates the exact ShellFoundation profile.
        /// This method performs no mutation; scene structure validity is delegated to the existing
        /// read-only #223 validator.
        /// </summary>
        public static ProductionBuildSettingsValidationReport ValidateCurrentShellFoundation()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string buildSettingsPath = Path.Combine(projectRoot, BuildSettingsRelativePath);
            bool buildSettingsPresent = File.Exists(buildSettingsPath);

            EditorBuildSettingsScene[] currentScenes = buildSettingsPresent
                ? EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>()
                : Array.Empty<EditorBuildSettingsScene>();

            var snapshot = new List<ProductionBuildSettingsSnapshotEntry>(currentScenes.Length);
            foreach (EditorBuildSettingsScene scene in currentScenes)
            {
                string path = scene.path ?? string.Empty;
                string sceneName = SceneNameFromPath(path);
                string physicalPath = AssetPathToPhysicalPath(projectRoot, path);
                snapshot.Add(new ProductionBuildSettingsSnapshotEntry(
                    path,
                    sceneName,
                    scene.guid.ToString(),
                    scene.enabled,
                    physicalPath.Length > 0 && File.Exists(physicalPath)));
            }

            CurrentTransitionReachability reachability = InspectCurrentTransitionReachability(projectRoot);
            bool descriptorValid = ProductionSceneValidator.Validate().IsValid && reachability.InspectionSucceeded;
            return ValidateSnapshot(
                buildSettingsPresent,
                snapshot,
                descriptorValid,
                reachability.DeferredChampionHandlerReachable,
                reachability.ResetToBootReachable);
        }

        /// <summary>
        /// Pure validation seam. It reads only the supplied immutable values and the immutable
        /// ProductionSceneDescriptor; it does not access or mutate Build Settings, assets, scenes,
        /// services, saves, or navigation state.
        /// </summary>
        public static ProductionBuildSettingsValidationReport ValidateSnapshot(
            bool buildSettingsPresent,
            IEnumerable<ProductionBuildSettingsSnapshotEntry> snapshotEntries,
            bool descriptorValid)
        {
            return ValidateSnapshot(
                buildSettingsPresent,
                snapshotEntries,
                descriptorValid,
                deferredChampionHandlerReachable: false,
                resetToBootReachable: false);
        }

        /// <summary>
        /// Pure transition-policy seam. The final two values represent implementation evidence from
        /// the accepted ShellFoundation controllers, allowing fail-closed reachability regressions to
        /// be tested without rewriting source or scene assets.
        /// </summary>
        public static ProductionBuildSettingsValidationReport ValidateSnapshot(
            bool buildSettingsPresent,
            IEnumerable<ProductionBuildSettingsSnapshotEntry> snapshotEntries,
            bool descriptorValid,
            bool deferredChampionHandlerReachable,
            bool resetToBootReachable)
        {
            var entries = (snapshotEntries ?? Array.Empty<ProductionBuildSettingsSnapshotEntry>())
                .Select(entry => entry ?? new ProductionBuildSettingsSnapshotEntry(
                    string.Empty, string.Empty, string.Empty, false, false))
                .ToList();
            var diagnostics = new List<ProductionBuildSettingsDiagnostic>();

            if (!descriptorValid || !DescriptorProfileIsValid())
            {
                Add(diagnostics, ProductionBuildSettingsValidationStatus.DescriptorDrift, -1, string.Empty,
                    "The production scene descriptor or committed scene validation does not match ShellFoundation authority.");
            }

            if (!buildSettingsPresent)
            {
                Add(diagnostics, ProductionBuildSettingsValidationStatus.MissingBuildSettings, -1, string.Empty,
                    "ProjectSettings/EditorBuildSettings.asset is missing.");
                return BuildReport(entries, diagnostics);
            }

            if (entries.Count == 0)
            {
                Add(diagnostics, ProductionBuildSettingsValidationStatus.EmptyBuildSettings, -1, string.Empty,
                    "Build Settings contains no scene entries.");
                return BuildReport(entries, diagnostics);
            }

            IReadOnlyList<ProductionSceneRecord> expected = ProductionSceneDescriptor.ShellFoundationOrdered;
            ProductionSceneRecord boot = RecordById(ProductionSceneDescriptor.BootSceneId);
            ProductionSceneRecord champion = RecordById(ProductionSceneDescriptor.ChampionArenaSceneId);
            ProductionSceneRecord representativeTest = RecordById(ProductionSceneDescriptor.TestSceneId);

            if (boot == null || !string.Equals(entries[0].Path, boot.AssetPath, StringComparison.Ordinal))
            {
                Add(diagnostics, ProductionBuildSettingsValidationStatus.WrongEntryScene, 0, entries[0].Path,
                    "Build index 0 must be the exact descriptor Boot path.");
            }

            ValidateDuplicateValues(entries, diagnostics);

            for (int index = 0; index < entries.Count; index++)
            {
                ProductionBuildSettingsSnapshotEntry entry = entries[index];

                if (string.IsNullOrWhiteSpace(entry.Path) || !entry.PathExists)
                {
                    Add(diagnostics, ProductionBuildSettingsValidationStatus.MissingPath, index, entry.Path,
                        "The serialized scene path is blank or does not resolve to a file.");
                }

                if (!entry.Enabled)
                {
                    Add(diagnostics, ProductionBuildSettingsValidationStatus.DisabledStaleScene, index, entry.Path,
                        "ShellFoundation permits no disabled or stale Build Settings entries.");
                }

                bool expectedPath = expected.Any(record =>
                    string.Equals(record.AssetPath, entry.Path, StringComparison.Ordinal));
                bool isRepresentativeTest = MatchesIdentity(entry, representativeTest);
                bool isChampion = MatchesIdentity(entry, champion);
                if (isRepresentativeTest)
                {
                    Add(diagnostics, ProductionBuildSettingsValidationStatus.TestSceneEnabled, index, entry.Path,
                        "The representative Test scene is prohibited even as a disabled entry.");
                }
                else if (isChampion && entry.Enabled)
                {
                    Add(diagnostics, ProductionBuildSettingsValidationStatus.DeferredSceneEnabled, index, entry.Path,
                        "ChampionArena is deferred and cannot be enabled in ShellFoundation.");
                }
                else if (!expectedPath)
                {
                    Add(diagnostics, ProductionBuildSettingsValidationStatus.UnexpectedScene, index, entry.Path,
                        "The scene is not in the ShellFoundation descriptor profile.");
                }
            }

            for (int expectedIndex = 0; expectedIndex < expected.Count; expectedIndex++)
            {
                ProductionSceneRecord record = expected[expectedIndex];
                var matches = entries
                    .Select((entry, index) => new { Entry = entry, Index = index })
                    .Where(item => string.Equals(item.Entry.Path, record.AssetPath, StringComparison.Ordinal))
                    .ToList();

                if (matches.Count == 0)
                {
                    Add(diagnostics, ProductionBuildSettingsValidationStatus.MissingRequiredScene, expectedIndex,
                        record.AssetPath, $"Required descriptor scene '{record.SceneId}' is absent.");
                    continue;
                }

                foreach (var match in matches)
                {
                    if (!string.Equals(match.Entry.SceneName, record.SceneName, StringComparison.Ordinal))
                    {
                        Add(diagnostics, ProductionBuildSettingsValidationStatus.DescriptorDrift, match.Index,
                            match.Entry.Path,
                            $"Scene name '{match.Entry.SceneName}' does not exactly match descriptor '{record.SceneName}'.");
                    }

                    if (!string.Equals(match.Entry.AssetGuid, record.AssetGuid, StringComparison.Ordinal))
                    {
                        Add(diagnostics, ProductionBuildSettingsValidationStatus.GuidMismatch, match.Index,
                            match.Entry.Path,
                            $"Scene GUID '{match.Entry.AssetGuid}' does not match descriptor '{record.AssetGuid}'.");
                    }
                }

                if (expectedIndex < entries.Count &&
                    !string.Equals(entries[expectedIndex].Path, record.AssetPath, StringComparison.Ordinal))
                {
                    Add(diagnostics, ProductionBuildSettingsValidationStatus.DescriptorDrift, expectedIndex,
                        entries[expectedIndex].Path,
                        $"Build index {expectedIndex} must be descriptor scene '{record.SceneId}'.");
                }
            }

            ValidateTransitionApplicability(
                entries,
                diagnostics,
                deferredChampionHandlerReachable,
                resetToBootReachable);
            return BuildReport(entries, diagnostics);
        }

        private static void ValidateDuplicateValues(
            IReadOnlyList<ProductionBuildSettingsSnapshotEntry> entries,
            ICollection<ProductionBuildSettingsDiagnostic> diagnostics)
        {
            foreach (IGrouping<string, ProductionBuildSettingsSnapshotEntry> duplicate in entries
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                         .GroupBy(entry => entry.Path, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                int index = IndexOfPath(entries, duplicate.Key);
                Add(diagnostics, ProductionBuildSettingsValidationStatus.DuplicatePath, index, duplicate.Key,
                    "The same exact scene path appears more than once.");
            }

            foreach (IGrouping<string, ProductionBuildSettingsSnapshotEntry> duplicate in entries
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.SceneName))
                         .GroupBy(entry => entry.SceneName, StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                int index = IndexOfName(entries, duplicate.Key);
                Add(diagnostics, ProductionBuildSettingsValidationStatus.DuplicateName, index,
                    index >= 0 ? entries[index].Path : string.Empty,
                    $"Scene name '{duplicate.Key}' appears more than once.");
            }
        }

        private static void ValidateTransitionApplicability(
            IReadOnlyList<ProductionBuildSettingsSnapshotEntry> entries,
            ICollection<ProductionBuildSettingsDiagnostic> diagnostics,
            bool deferredChampionHandlerReachable,
            bool resetToBootReachable)
        {
            var enabledPaths = new HashSet<string>(
                entries.Where(entry => entry.Enabled).Select(entry => entry.Path),
                StringComparer.Ordinal);
            var shellIds = new HashSet<string>(
                ProductionSceneDescriptor.ShellFoundationOrdered.Select(record => record.SceneId),
                StringComparer.Ordinal);

            foreach (ProductionSceneRecord source in ProductionSceneDescriptor.ProductionScenes)
            {
                bool sourceIsActive = shellIds.Contains(source.SceneId);
                foreach (SceneTransition transition in source.TransitionTargets)
                {
                    ProductionSceneRecord target = RecordById(transition.TargetSceneId);
                    if (target == null ||
                        !string.Equals(transition.SerializedValue, target.SceneName, StringComparison.Ordinal))
                    {
                        Add(diagnostics, ProductionBuildSettingsValidationStatus.TransitionUnavailable, -1,
                            source.AssetPath,
                            $"Transition from '{source.SceneId}' does not resolve exactly to '{transition.TargetSceneId}'.");
                        continue;
                    }

                    if (!sourceIsActive)
                    {
                        // Excluded scenes still need valid targets in the complete descriptor inventory,
                        // but their transitions are not active-profile reachability.
                        continue;
                    }

                    if (transition.Status == TransitionStatus.Active &&
                        (!shellIds.Contains(target.SceneId) || !enabledPaths.Contains(target.AssetPath)))
                    {
                        Add(diagnostics, ProductionBuildSettingsValidationStatus.TransitionUnavailable, -1,
                            source.AssetPath,
                            $"Active transition target '{target.SceneId}' is unavailable in ShellFoundation.");
                    }

                    if (transition.Status == TransitionStatus.Deferred &&
                        (transition.HasSerializedField || enabledPaths.Contains(target.AssetPath)))
                    {
                        Add(diagnostics, ProductionBuildSettingsValidationStatus.DeferredTransitionReachable, -1,
                            source.AssetPath,
                            $"Deferred transition target '{target.SceneId}' is reachable in ShellFoundation.");
                    }

                    if (transition.Status == TransitionStatus.BlockedUnsafe)
                    {
                        Add(diagnostics, ProductionBuildSettingsValidationStatus.DeferredTransitionReachable, -1,
                            source.AssetPath,
                            $"Blocked unsafe transition target '{target.SceneId}' remains in active descriptor reachability.");
                    }
                }
            }

            if (deferredChampionHandlerReachable)
            {
                Add(diagnostics, ProductionBuildSettingsValidationStatus.DeferredTransitionReachable, -1,
                    RecordById(ProductionSceneDescriptor.KingdomSceneId)?.AssetPath ?? string.Empty,
                    "The deferred Kingdom-to-Champion route has an active production handler.");
            }

            if (resetToBootReachable || ProductionSceneDescriptor.IsResetToBootProductionReachable)
            {
                Add(diagnostics, ProductionBuildSettingsValidationStatus.DeferredTransitionReachable, -1,
                    string.Empty, "The unsafe reset-to-Boot route is production reachable.");
            }
        }

        private static CurrentTransitionReachability InspectCurrentTransitionReachability(string projectRoot)
        {
            bool deferredChampionHandlerReachable = false;
            bool resetToBootReachable = false;

            try
            {
                foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ShellFoundationOrdered)
                {
                    Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(assembly => assembly.GetType(record.RequiredControllerType, throwOnError: false))
                        .FirstOrDefault(type => type != null);
                    string controllerAssetPath = FindMonoScriptAssetPath(controllerType);
                    string controllerPhysicalPath = AssetPathToPhysicalPath(projectRoot, controllerAssetPath);
                    if (controllerType == null ||
                        string.IsNullOrWhiteSpace(controllerAssetPath) ||
                        string.IsNullOrWhiteSpace(controllerPhysicalPath) ||
                        !File.Exists(controllerPhysicalPath))
                    {
                        return CurrentTransitionReachability.Invalid();
                    }

                    string source = StripComments(File.ReadAllText(controllerPhysicalPath));
                    bool isKingdomController = string.Equals(
                        record.SceneId,
                        ProductionSceneDescriptor.KingdomSceneId,
                        StringComparison.Ordinal);
                    deferredChampionHandlerReachable |=
                        (isKingdomController && ContainsSceneLoadCall(source)) ||
                        ContainsSceneLoadTarget(source, "ChampionArena");
                    resetToBootReachable |= ContainsSceneLoadTarget(source, "Boot");
                }

                return CurrentTransitionReachability.Valid(
                    deferredChampionHandlerReachable,
                    resetToBootReachable);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return CurrentTransitionReachability.Invalid();
            }
        }

        private static string FindMonoScriptAssetPath(Type controllerType)
        {
            if (controllerType == null)
            {
                return string.Empty;
            }

            foreach (string guid in AssetDatabase.FindAssets(controllerType.Name + " t:MonoScript")
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (script != null && script.GetClass() == controllerType)
                {
                    return assetPath;
                }
            }

            return string.Empty;
        }

        private static bool ContainsSceneLoadCall(string source)
        {
            return Regex.IsMatch(
                source ?? string.Empty,
                "\\bSceneManager\\s*\\.\\s*LoadScene(?:Async)?\\s*\\(",
                RegexOptions.CultureInvariant);
        }

        private static bool ContainsSceneLoadTarget(string source, string targetSceneName)
        {
            string code = source ?? string.Empty;
            string target = Regex.Escape(targetSceneName ?? string.Empty);
            string callPrefix = "\\bSceneManager\\s*\\.\\s*LoadScene(?:Async)?\\s*\\(\\s*";
            if (Regex.IsMatch(
                    code,
                    callPrefix + "@?\\\"" + target + "\\\"",
                    RegexOptions.CultureInvariant))
            {
                return true;
            }

            if (string.Equals(targetSceneName, "Boot", StringComparison.Ordinal) &&
                Regex.IsMatch(
                    code,
                    callPrefix + "ProductionSceneDescriptor\\s*\\.\\s*BootSceneId\\b",
                    RegexOptions.CultureInvariant))
            {
                return true;
            }

            MatchCollection assignments = Regex.Matches(
                code,
                "\\b(?<identifier>[_A-Za-z][_A-Za-z0-9]*)\\s*=\\s*@?\\\"" + target + "\\\"",
                RegexOptions.CultureInvariant);
            foreach (Match assignment in assignments)
            {
                string identifier = Regex.Escape(assignment.Groups["identifier"].Value);
                if (Regex.IsMatch(
                        code,
                        callPrefix + identifier + "\\b",
                        RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripComments(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            var output = new StringBuilder(source.Length);
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inVerbatimString = false;
            bool inCharacter = false;
            bool escaped = false;

            for (int index = 0; index < source.Length; index++)
            {
                char current = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\r' || current == '\n')
                    {
                        inLineComment = false;
                        output.Append(current);
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        index++;
                    }
                    else if (current == '\r' || current == '\n')
                    {
                        output.Append(current);
                    }
                    continue;
                }

                if (inString)
                {
                    output.Append(current);
                    if (inVerbatimString && current == '"' && next == '"')
                    {
                        output.Append(next);
                        index++;
                        continue;
                    }
                    if ((!inVerbatimString && !escaped && current == '"') ||
                        (inVerbatimString && current == '"'))
                    {
                        inString = false;
                        inVerbatimString = false;
                    }
                    escaped = !inVerbatimString && !escaped && current == '\\';
                    if (current != '\\')
                    {
                        escaped = false;
                    }
                    continue;
                }

                if (inCharacter)
                {
                    output.Append(current);
                    if (!escaped && current == '\'')
                    {
                        inCharacter = false;
                    }
                    escaped = !escaped && current == '\\';
                    if (current != '\\')
                    {
                        escaped = false;
                    }
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    inLineComment = true;
                    index++;
                    continue;
                }
                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    index++;
                    continue;
                }
                if (current == '"')
                {
                    inString = true;
                    inVerbatimString = index > 0 && source[index - 1] == '@';
                    output.Append(current);
                    continue;
                }
                if (current == '\'')
                {
                    inCharacter = true;
                    output.Append(current);
                    continue;
                }

                output.Append(current);
            }

            return output.ToString();
        }

        private sealed class CurrentTransitionReachability
        {
            private CurrentTransitionReachability(
                bool inspectionSucceeded,
                bool deferredChampionHandlerReachable,
                bool resetToBootReachable)
            {
                InspectionSucceeded = inspectionSucceeded;
                DeferredChampionHandlerReachable = deferredChampionHandlerReachable;
                ResetToBootReachable = resetToBootReachable;
            }

            internal bool InspectionSucceeded { get; }
            internal bool DeferredChampionHandlerReachable { get; }
            internal bool ResetToBootReachable { get; }

            internal static CurrentTransitionReachability Invalid() =>
                new CurrentTransitionReachability(false, true, true);

            internal static CurrentTransitionReachability Valid(
                bool deferredChampionHandlerReachable,
                bool resetToBootReachable) =>
                new CurrentTransitionReachability(
                    true,
                    deferredChampionHandlerReachable,
                    resetToBootReachable);
        }

        private static bool DescriptorProfileIsValid()
        {
            IReadOnlyList<ProductionSceneRecord> shell = ProductionSceneDescriptor.ShellFoundationOrdered;
            IReadOnlyList<ProductionSceneRecord> all = ProductionSceneDescriptor.All;
            IReadOnlyList<ProductionSceneRecord> production = ProductionSceneDescriptor.ProductionScenes;
            string[] requiredOrder =
            {
                ProductionSceneDescriptor.BootSceneId,
                ProductionSceneDescriptor.RealmSelectionSceneId,
                ProductionSceneDescriptor.KingdomSceneId
            };
            string[] requiredInventory =
            {
                ProductionSceneDescriptor.BootSceneId,
                ProductionSceneDescriptor.RealmSelectionSceneId,
                ProductionSceneDescriptor.KingdomSceneId,
                ProductionSceneDescriptor.ChampionArenaSceneId,
                ProductionSceneDescriptor.TestSceneId
            };

            if (shell.Count != requiredOrder.Length ||
                all.Count != requiredInventory.Length ||
                production.Count != requiredInventory.Length - 1 ||
                all.Any(record => record == null) ||
                production.Any(record => record == null) ||
                !requiredInventory.All(sceneId => all.Any(record =>
                    string.Equals(record.SceneId, sceneId, StringComparison.Ordinal))))
            {
                return false;
            }

            for (int index = 0; index < requiredOrder.Length; index++)
            {
                ProductionSceneRecord record = shell[index];
                if (record == null ||
                    !string.Equals(record.SceneId, requiredOrder[index], StringComparison.Ordinal) ||
                    !record.IsProductionScene ||
                    !record.IsInShellFoundation ||
                    string.IsNullOrWhiteSpace(record.AssetPath) ||
                    string.IsNullOrWhiteSpace(record.SceneName) ||
                    record.AssetGuid.Length != 32)
                {
                    return false;
                }
            }

            ProductionSceneRecord champion = RecordById(ProductionSceneDescriptor.ChampionArenaSceneId);
            ProductionSceneRecord representativeTest = RecordById(ProductionSceneDescriptor.TestSceneId);
            if (champion == null ||
                !champion.IsProductionScene ||
                champion.IsInShellFoundation ||
                !string.Equals(champion.Status, ProductionSceneDescriptor.StatusCommittedDeferred, StringComparison.Ordinal) ||
                representativeTest == null ||
                representativeTest.IsProductionScene ||
                representativeTest.IsInShellFoundation ||
                !string.Equals(representativeTest.Status, ProductionSceneDescriptor.StatusTestOnly, StringComparison.Ordinal))
            {
                return false;
            }

            return shell.Select(record => record.SceneId).Distinct(StringComparer.Ordinal).Count() == shell.Count &&
                   shell.Select(record => record.AssetPath).Distinct(StringComparer.Ordinal).Count() == shell.Count &&
                   shell.Select(record => record.SceneName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == shell.Count &&
                   shell.Select(record => record.AssetGuid).Distinct(StringComparer.Ordinal).Count() == shell.Count &&
                   all.Select(record => record.SceneId).Distinct(StringComparer.Ordinal).Count() == all.Count &&
                   all.Select(record => record.AssetPath).Distinct(StringComparer.Ordinal).Count() == all.Count &&
                   all.Select(record => record.SceneName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == all.Count &&
                   all.Select(record => record.AssetGuid).Distinct(StringComparer.Ordinal).Count() == all.Count &&
                   !shell.Any(record =>
                       string.Equals(record.SceneId, ProductionSceneDescriptor.ChampionArenaSceneId, StringComparison.Ordinal) ||
                       string.Equals(record.SceneId, ProductionSceneDescriptor.TestSceneId, StringComparison.Ordinal));
        }

        private static bool MatchesIdentity(
            ProductionBuildSettingsSnapshotEntry entry,
            ProductionSceneRecord record)
        {
            return record != null &&
                   (string.Equals(entry.Path, record.AssetPath, StringComparison.Ordinal) ||
                    string.Equals(entry.SceneName, record.SceneName, StringComparison.Ordinal) ||
                    string.Equals(entry.AssetGuid, record.AssetGuid, StringComparison.Ordinal));
        }

        private static ProductionSceneRecord RecordById(string sceneId)
        {
            ProductionSceneDescriptor.TryGetById(sceneId, out ProductionSceneRecord record);
            return record;
        }

        private static int IndexOfPath(IReadOnlyList<ProductionBuildSettingsSnapshotEntry> entries, string path)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].Path, path, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int IndexOfName(IReadOnlyList<ProductionBuildSettingsSnapshotEntry> entries, string name)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].SceneName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string SceneNameFromPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(path.Replace('\\', '/')) ?? string.Empty;
        }

        private static string AssetPathToPhysicalPath(string projectRoot, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return string.Empty;
            }
        }

        private static ProductionBuildSettingsValidationReport BuildReport(
            IEnumerable<ProductionBuildSettingsSnapshotEntry> entries,
            IEnumerable<ProductionBuildSettingsDiagnostic> diagnostics)
        {
            ProductionBuildSettingsDiagnostic[] ordered = diagnostics
                .OrderBy(diagnostic => (int)diagnostic.Status)
                .ThenBy(diagnostic => diagnostic.EntryIndex)
                .ThenBy(diagnostic => diagnostic.ScenePath, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToArray();
            return new ProductionBuildSettingsValidationReport(
                ordered,
                entries.Select(entry => entry.Path));
        }

        private static void Add(
            ICollection<ProductionBuildSettingsDiagnostic> diagnostics,
            ProductionBuildSettingsValidationStatus status,
            int entryIndex,
            string scenePath,
            string message)
        {
            if (diagnostics.Any(existing =>
                    existing.Status == status &&
                    existing.EntryIndex == entryIndex &&
                    string.Equals(existing.ScenePath, scenePath ?? string.Empty, StringComparison.Ordinal) &&
                    string.Equals(existing.Message, message ?? string.Empty, StringComparison.Ordinal)))
            {
                return;
            }

            diagnostics.Add(new ProductionBuildSettingsDiagnostic(status, entryIndex, scenePath, message));
        }
    }
}
#endif
