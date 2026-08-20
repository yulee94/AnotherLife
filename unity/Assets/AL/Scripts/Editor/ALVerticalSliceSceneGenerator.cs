#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Core.Scenes;
using AL.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AL.EditorTools
{
    public enum SceneGenerationStatus
    {
        AllValid,
        CreatedMissing,
        DriftBlocked,
        SerializationModeInvalid,
        SaveFailed,
        PostGenerationValidationFailed,
        RegenerationTokenRejected,
        MetaIdentityChanged
    }

    /// <summary>
    /// Non-destructive, deterministic authoring path for the committed production scenes (#223
    /// "Required architecture"/"Safe generation policy"/"Determinism"). Responsibilities are split so
    /// default authoring never mutates Build Settings:
    ///   - InspectProductionScenes: report Missing/Valid/Drifted per scene.
    ///   - GenerateMissingProductionScenes: create only missing scenes; block on any drift; typed result.
    ///   - ValidateProductionScenes: drift validator (delegates to ProductionSceneValidator).
    ///   - ApplyProductionBuildSettings: owned/invoked ONLY by #150 (no menu item, not called here).
    ///   - RegenerateProductionScenes(confirmToken): explicit reviewed overwrite preserving .meta identity.
    /// The old destructive Generate()/UpdateBuildSettings() menu path is removed entirely.
    /// </summary>
    public static class ALVerticalSliceSceneGenerator
    {
        public const string RegenerateConfirmToken = "REGENERATE-PRODUCTION-SCENES-223";

        // Testable save seam. Production authoring leaves this null and uses EditorSceneManager.SaveScene.
        internal static Func<Scene, string, bool> SaveSceneOverride;

        [MenuItem("Another Life/Inspect Production Scenes")]
        public static void InspectMenu()
        {
            Debug.Log(InspectProductionScenes().Summarize());
        }

        [MenuItem("Another Life/Generate Missing Production Scenes")]
        public static void GenerateMissingMenu()
        {
            SceneGenerationResult result = GenerateMissingProductionScenes();
            Debug.Log(result.Summarize());
        }

        [MenuItem("Another Life/Validate Production Scenes")]
        public static void ValidateMenu()
        {
            Debug.Log(ValidateProductionScenes().Summarize());
        }

        // -----------------------------------------------------------------
        // Inspection
        // -----------------------------------------------------------------

        public static SceneInspectionReport InspectProductionScenes()
        {
            SceneValidationReport validation = ProductionSceneValidator.Validate();
            var entries = new List<SceneInspectionEntry>();
            foreach (SceneValidationResult scene in validation.Scenes)
            {
                entries.Add(new SceneInspectionEntry(scene.SceneId, scene.ExpectedAssetPath, scene.Status, scene.Diffs));
            }

            return new SceneInspectionReport(entries, validation.InventoryFailures);
        }

        // -----------------------------------------------------------------
        // Generation decision (pure; unit-tested without file I/O)
        // -----------------------------------------------------------------

        internal static GenerationPlan DecideGeneration(SceneInspectionReport inspection)
        {
            var drifted = inspection.Entries
                .Where(entry => entry.Status == SceneValidationStatus.Drifted)
                .Select(entry => entry.SceneId)
                .ToList();

            if (drifted.Count > 0)
            {
                return GenerationPlan.Blocked(drifted);
            }

            var missing = inspection.Entries
                .Where(entry => entry.Status == SceneValidationStatus.Missing)
                .Select(entry => entry.SceneId)
                .ToList();

            return GenerationPlan.Create(missing);
        }

        // -----------------------------------------------------------------
        // Generate missing (never overwrites an existing scene)
        // -----------------------------------------------------------------

        public static SceneGenerationResult GenerateMissingProductionScenes()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                return SceneGenerationResult.Failure(
                    SceneGenerationStatus.SerializationModeInvalid,
                    $"Force Text serialization is required; current mode is {EditorSettings.serializationMode}.");
            }

            SceneInspectionReport inspection = InspectProductionScenes();
            GenerationPlan plan = DecideGeneration(inspection);
            if (plan.IsBlocked)
            {
                return SceneGenerationResult.Failure(
                    SceneGenerationStatus.DriftBlocked,
                    "Existing scenes are drifted; refusing to author over them: " + string.Join(", ", plan.DriftedSceneIds));
            }

            EnsureSceneFolder();

            var created = new List<string>();
            foreach (string sceneId in plan.MissingSceneIds)
            {
                if (!ProductionSceneDescriptor.TryGetById(sceneId, out ProductionSceneRecord record))
                {
                    continue;
                }

                try
                {
                    Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    PopulateProductionScene(scene, record);
                    scene.name = record.SceneName;
                    if (!SaveScene(scene, record.AssetPath))
                    {
                        return SceneGenerationResult.SaveFailure(record.AssetPath, created);
                    }

                    created.Add(record.AssetPath);
                }
                catch (Exception ex)
                {
                    return SceneGenerationResult.SaveFailure($"{record.AssetPath}: {ex.Message}", created);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneValidationReport validation = ProductionSceneValidator.Validate();
            if (!validation.IsValid)
            {
                return new SceneGenerationResult(
                    SceneGenerationStatus.PostGenerationValidationFailed,
                    created,
                    new[] { "Post-generation validation failed.", validation.Summarize() },
                    validation);
            }

            SceneGenerationStatus status = created.Count == 0
                ? SceneGenerationStatus.AllValid
                : SceneGenerationStatus.CreatedMissing;
            return new SceneGenerationResult(status, created, new[] { validation.Summarize() }, validation);
        }

        // -----------------------------------------------------------------
        // Explicit reviewed regeneration (preserves file/.meta GUID identity)
        // -----------------------------------------------------------------

        public static SceneGenerationResult RegenerateProductionScenes(string confirmToken)
        {
            if (!string.Equals(confirmToken, RegenerateConfirmToken, StringComparison.Ordinal))
            {
                return SceneGenerationResult.Failure(
                    SceneGenerationStatus.RegenerationTokenRejected,
                    "Regeneration requires the exact reviewed confirmation token.");
            }

            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                return SceneGenerationResult.Failure(
                    SceneGenerationStatus.SerializationModeInvalid,
                    $"Force Text serialization is required; current mode is {EditorSettings.serializationMode}.");
            }

            EnsureSceneFolder();

            var guidsBefore = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ProductionScenes)
            {
                guidsBefore[record.AssetPath] = AssetDatabase.AssetPathToGUID(record.AssetPath);
            }

            // Validate every scene model in memory before writing ANY scene, so one scene's structural
            // failure never leaves another already rewritten (same validate-all-before-save-any guarantee
            // as GenerateMissing).
            var preSaveFailures = new List<string>();
            foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ProductionScenes)
            {
                Scene model = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PopulateProductionScene(model, record);
                model.name = record.SceneName;
                SceneValidationResult modelResult = ProductionSceneValidator.ValidateOpenScene(
                    model, record, record.AssetPath, record.AssetGuid);
                if (modelResult.Status != SceneValidationStatus.Valid)
                {
                    preSaveFailures.Add($"{record.SceneId}: {string.Join("; ", modelResult.Diffs)}");
                }
            }

            if (preSaveFailures.Count > 0)
            {
                return SceneGenerationResult.Failure(
                    SceneGenerationStatus.PostGenerationValidationFailed,
                    "Pre-save model validation failed; no scene was rewritten: " + string.Join(" | ", preSaveFailures));
            }

            var rewritten = new List<string>();
            foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ProductionScenes)
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PopulateProductionScene(scene, record);
                scene.name = record.SceneName;
                if (!SaveScene(scene, record.AssetPath))
                {
                    return SceneGenerationResult.SaveFailure(record.AssetPath, rewritten);
                }

                rewritten.Add(record.AssetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ProductionScenes)
            {
                string before = guidsBefore[record.AssetPath];
                string after = AssetDatabase.AssetPathToGUID(record.AssetPath);
                if (!string.IsNullOrEmpty(before) && !string.Equals(before, after, StringComparison.Ordinal))
                {
                    return new SceneGenerationResult(
                        SceneGenerationStatus.MetaIdentityChanged,
                        rewritten,
                        new[] { $"Regeneration changed the .meta GUID for {record.AssetPath} ({before} -> {after})." },
                        null);
                }
            }

            SceneValidationReport validation = ProductionSceneValidator.Validate();
            if (!validation.IsValid)
            {
                return new SceneGenerationResult(
                    SceneGenerationStatus.PostGenerationValidationFailed,
                    rewritten,
                    new[] { "Post-regeneration validation failed.", validation.Summarize() },
                    validation);
            }

            return new SceneGenerationResult(SceneGenerationStatus.CreatedMissing, rewritten, new[] { validation.Summarize() }, validation);
        }

        // -----------------------------------------------------------------
        // Validation
        // -----------------------------------------------------------------

        public static SceneValidationReport ValidateProductionScenes()
        {
            return ProductionSceneValidator.Validate();
        }

        // -----------------------------------------------------------------
        // Build Settings (owned by #150 only; NO menu item, never called by authoring)
        // -----------------------------------------------------------------

        /// <summary>
        /// Applies the ShellFoundation Build Settings list (Boot/RealmSelection/CharacterCreation/
        /// ChampionArena/Kingdom, enabled; Test absent) from the descriptor. This is the ONLY method that writes
        /// EditorBuildSettings and it is invoked exclusively by the later #150 build entry. It has no menu
        /// item and no batch entry and is never called by inspection/generation/validation.
        /// </summary>
        public static void ApplyProductionBuildSettings()
        {
            var scenes = ProductionSceneDescriptor.ShellFoundationOrdered
                .Select(record => new EditorBuildSettingsScene(record.AssetPath, true))
                .ToArray();
            EditorBuildSettings.scenes = scenes;
        }

        // -----------------------------------------------------------------
        // Scene authoring (deterministic root/component order)
        // -----------------------------------------------------------------

        /// <summary>
        /// Builds the fixed minimum structure for a production scene into an open, empty scene.
        /// Boot: "Boot" root (Bootloader + BootController) then SceneStartupMarker root then EventSystem root.
        /// Others: "Bootloader" root then controller root then SceneStartupMarker root then EventSystem root.
        /// Deterministic order, no timestamps/random IDs.
        /// </summary>
        internal static void PopulateProductionScene(Scene scene, ProductionSceneRecord record)
        {
            if (string.Equals(record.SceneId, ProductionSceneDescriptor.BootSceneId, StringComparison.Ordinal))
            {
                var bootRoot = NewRoot(scene, "Boot");
                bootRoot.AddComponent<Bootloader>();
                bootRoot.AddComponent<BootController>();
            }
            else
            {
                var bootloaderRoot = NewRoot(scene, "Bootloader");
                bootloaderRoot.AddComponent<Bootloader>();

                var controllerRoot = NewRoot(scene, ControllerRootName(record));
                Type controllerType = Type.GetType(record.RequiredControllerType + ", Assembly-CSharp")
                    ?? ResolveType(record.RequiredControllerType);
                if (controllerType != null)
                {
                    controllerRoot.AddComponent(controllerType);
                }
            }

            var markerRoot = NewRoot(scene, "SceneStartupMarker");
            SceneStartupMarker marker = markerRoot.AddComponent<SceneStartupMarker>();
            ConfigureMarker(marker, record);

            var eventSystemRoot = NewRoot(scene, "EventSystem");
            eventSystemRoot.AddComponent<EventSystem>();
            eventSystemRoot.AddComponent<StandaloneInputModule>();
        }

        private static GameObject NewRoot(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static string ControllerRootName(ProductionSceneRecord record)
        {
            int lastDot = record.RequiredControllerType.LastIndexOf('.');
            return lastDot >= 0 ? record.RequiredControllerType.Substring(lastDot + 1) : record.RequiredControllerType;
        }

        private static void ConfigureMarker(SceneStartupMarker marker, ProductionSceneRecord record)
        {
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("_sceneId").stringValue = record.SceneId;
            serialized.FindProperty("_expectedAssetPath").stringValue = record.AssetPath;
            serialized.FindProperty("_role").stringValue = record.Role;
            serialized.FindProperty("_sourceVersion").stringValue = ProductionSceneDescriptor.SourceVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool SaveScene(Scene scene, string path)
        {
            if (SaveSceneOverride != null)
            {
                return SaveSceneOverride(scene, path);
            }

            return EditorSceneManager.SaveScene(scene, path);
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AL"))
            {
                AssetDatabase.CreateFolder("Assets", "AL");
            }

            if (!AssetDatabase.IsValidFolder(ProductionSceneDescriptor.SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets/AL", "Scenes");
            }
        }

        private static Type ResolveType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        // -----------------------------------------------------------------
        // Batch entries (map typed status to process exit code)
        // -----------------------------------------------------------------

        public static void GenerateMissingProductionScenesBatch()
        {
            SceneGenerationResult result = GenerateMissingProductionScenes();
            Debug.Log($"[AL-SCENEGEN-RESULT] status={result.Status}");
            Debug.Log(result.Summarize());
            LogSceneGuids();
            EditorApplication.Exit(ToExitCode(result.Status));
        }

        public static void ValidateProductionScenesBatch()
        {
            SceneValidationReport report = ValidateProductionScenes();
            Debug.Log($"[AL-SCENE-VALIDATION-RESULT] valid={report.IsValid}");
            Debug.Log(report.Summarize());
            LogSceneGuids();
            EditorApplication.Exit(report.IsValid ? 0 : 1);
        }

        internal static int ToExitCode(SceneGenerationStatus status)
        {
            switch (status)
            {
                case SceneGenerationStatus.AllValid:
                case SceneGenerationStatus.CreatedMissing:
                    return 0;
                default:
                    return 1;
            }
        }

        private static void LogSceneGuids()
        {
            foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ProductionScenes)
            {
                string guid = AssetDatabase.AssetPathToGUID(record.AssetPath);
                Debug.Log($"[AL-SCENEGEN-GUID] path={record.AssetPath} guid={guid}");
            }
        }
    }

    // ---------------------------------------------------------------------
    // Typed result / plan / inspection types
    // ---------------------------------------------------------------------

    internal sealed class GenerationPlan
    {
        private GenerationPlan(bool isBlocked, IReadOnlyList<string> missing, IReadOnlyList<string> drifted)
        {
            IsBlocked = isBlocked;
            MissingSceneIds = missing ?? Array.Empty<string>();
            DriftedSceneIds = drifted ?? Array.Empty<string>();
        }

        public bool IsBlocked { get; }
        public IReadOnlyList<string> MissingSceneIds { get; }
        public IReadOnlyList<string> DriftedSceneIds { get; }

        public static GenerationPlan Create(IReadOnlyList<string> missing)
        {
            return new GenerationPlan(false, missing, Array.Empty<string>());
        }

        public static GenerationPlan Blocked(IReadOnlyList<string> drifted)
        {
            return new GenerationPlan(true, Array.Empty<string>(), drifted);
        }
    }

    public sealed class SceneInspectionEntry
    {
        public SceneInspectionEntry(string sceneId, string assetPath, SceneValidationStatus status, IEnumerable<string> diffs)
        {
            SceneId = sceneId ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Status = status;
            Diffs = (diffs ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public string SceneId { get; }
        public string AssetPath { get; }
        public SceneValidationStatus Status { get; }
        public IReadOnlyList<string> Diffs { get; }
    }

    public sealed class SceneInspectionReport
    {
        public SceneInspectionReport(IEnumerable<SceneInspectionEntry> entries, IEnumerable<string> inventoryFailures)
        {
            Entries = (entries ?? Array.Empty<SceneInspectionEntry>()).ToList().AsReadOnly();
            InventoryFailures = (inventoryFailures ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public IReadOnlyList<SceneInspectionEntry> Entries { get; }
        public IReadOnlyList<string> InventoryFailures { get; }

        public string Summarize()
        {
            var builder = new System.Text.StringBuilder("[AL-SCENE-INSPECTION]");
            foreach (SceneInspectionEntry entry in Entries)
            {
                builder.Append('\n').Append("  ").Append(entry.SceneId).Append(": ").Append(entry.Status);
            }

            foreach (string failure in InventoryFailures)
            {
                builder.Append('\n').Append("  inventory: ").Append(failure);
            }

            return builder.ToString();
        }
    }

    public sealed class SceneGenerationResult
    {
        public SceneGenerationResult(
            SceneGenerationStatus status,
            IEnumerable<string> createdScenes,
            IEnumerable<string> messages,
            SceneValidationReport validation)
        {
            Status = status;
            CreatedScenes = (createdScenes ?? Array.Empty<string>()).ToList().AsReadOnly();
            Messages = (messages ?? Array.Empty<string>()).ToList().AsReadOnly();
            Validation = validation;
        }

        public SceneGenerationStatus Status { get; }
        public IReadOnlyList<string> CreatedScenes { get; }
        public IReadOnlyList<string> Messages { get; }
        public SceneValidationReport Validation { get; }

        public bool Succeeded =>
            Status == SceneGenerationStatus.AllValid || Status == SceneGenerationStatus.CreatedMissing;

        public static SceneGenerationResult Failure(SceneGenerationStatus status, string message, SceneValidationReport validation = null)
        {
            return new SceneGenerationResult(status, Array.Empty<string>(), new[] { message }, validation);
        }

        public static SceneGenerationResult SaveFailure(string failedPath, IReadOnlyList<string> createdSoFar)
        {
            return new SceneGenerationResult(
                SceneGenerationStatus.SaveFailed,
                createdSoFar,
                new[] { $"Failed to save scene '{failedPath}'. No existing scene was overwritten." },
                null);
        }

        public string Summarize()
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("[AL-SCENEGEN] status=").Append(Status);
            builder.Append(" created=").Append(CreatedScenes.Count);
            foreach (string path in CreatedScenes)
            {
                builder.Append('\n').Append("  + ").Append(path);
            }

            foreach (string message in Messages)
            {
                builder.Append('\n').Append("  ").Append(message);
            }

            return builder.ToString();
        }
    }
}
#endif
