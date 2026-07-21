#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AL.EditorTools
{
    public enum SceneValidationStatus
    {
        Valid,
        Missing,
        Drifted
    }

    /// <summary>
    /// Drift validator for the committed production scenes (#223 "Generator/committed-scene drift
    /// validator", spec section 10). It loads each expected scene by PATH and never touches Build
    /// Settings. It fails on missing path, asset/internal name mismatch, duplicate names, missing or
    /// duplicated required controller, missing or duplicated EventSystem, missing/duplicated startup
    /// marker or marker field drift, a Bootloader arrangement contradicting the accepted #153 per-scene
    /// owner contract, missing scripts, transition-string case/value drift, GUID mismatch, Test treated
    /// as production, and descriptor/validator inventory disagreement.
    /// </summary>
    public static class ProductionSceneValidator
    {
        /// <summary>Validates every committed production scene at its descriptor path (no Build Settings access).</summary>
        public static SceneValidationReport Validate()
        {
            var results = new List<SceneValidationResult>();
            var inventoryFailures = new List<string>();

            // Global inventory checks that do not require opening a scene.
            var productionNames = new List<string>();
            var productionPaths = new List<string>();
            foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ProductionScenes)
            {
                productionNames.Add(record.SceneName);
                productionPaths.Add(record.AssetPath);

                if (string.Equals(record.AssetPath, "Assets/Test.unity", StringComparison.Ordinal) ||
                    !record.IsProductionScene)
                {
                    inventoryFailures.Add($"{record.SceneId}: representative test scene must not be treated as production");
                }
            }

            foreach (string duplicate in productionNames.GroupBy(n => n, StringComparer.Ordinal)
                         .Where(g => g.Count() > 1).Select(g => g.Key))
            {
                inventoryFailures.Add($"duplicate production scene name '{duplicate}' in descriptor");
            }

            foreach (string duplicate in productionPaths.GroupBy(p => p, StringComparer.Ordinal)
                         .Where(g => g.Count() > 1).Select(g => g.Key))
            {
                inventoryFailures.Add($"duplicate production scene path '{duplicate}' in descriptor");
            }

            // Descriptor/validator inventory agreement: the validator authors from the same descriptor set
            // the generator does; assert the Test record is present in All but absent from ProductionScenes.
            if (ProductionSceneDescriptor.All.Count != ProductionSceneDescriptor.ProductionScenes.Count + 1)
            {
                inventoryFailures.Add("descriptor All/ProductionScenes inventory disagreement (expected exactly one test-only record)");
            }

            foreach (ProductionSceneRecord record in ProductionSceneDescriptor.ProductionScenes)
            {
                results.Add(ValidateScene(record));
            }

            return new SceneValidationReport(results, inventoryFailures);
        }

        /// <summary>Validates a single production scene by opening its descriptor path additively.</summary>
        public static SceneValidationResult ValidateScene(ProductionSceneRecord record)
        {
            string path = record.AssetPath;
            if (!File.Exists(path))
            {
                return SceneValidationResult.MissingPath(record);
            }

            string actualGuid = AssetDatabase.AssetPathToGUID(path);

            bool wasOpen = false;
            Scene scene = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (string.Equals(candidate.path, path, StringComparison.Ordinal) && candidate.isLoaded)
                {
                    scene = candidate;
                    wasOpen = true;
                    break;
                }
            }

            if (!wasOpen)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                return ValidateOpenScene(scene, record, path, actualGuid);
            }
            finally
            {
                if (!wasOpen && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        /// <summary>
        /// Pure structural validation of an already-open scene against a descriptor record. Performs no
        /// scene loading and no Build Settings access, so tests can drive it against in-memory fixtures.
        /// </summary>
        public static SceneValidationResult ValidateOpenScene(
            Scene scene,
            ProductionSceneRecord record,
            string actualAssetPath,
            string actualAssetGuid)
        {
            var diffs = new List<string>();
            var rootNames = new List<string>();
            var transitionValues = new Dictionary<string, string>(StringComparer.Ordinal);

            GameObject[] roots = scene.IsValid() ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
            foreach (GameObject root in roots)
            {
                rootNames.Add(root.name);
            }

            // Asset path / GUID / name identity.
            if (!string.Equals(actualAssetPath, record.AssetPath, StringComparison.Ordinal))
            {
                diffs.Add($"asset path '{actualAssetPath}' != descriptor path '{record.AssetPath}'");
            }

            if (!string.IsNullOrEmpty(record.AssetGuid) &&
                !string.IsNullOrEmpty(actualAssetGuid) &&
                !string.Equals(actualAssetGuid, record.AssetGuid, StringComparison.Ordinal))
            {
                diffs.Add($"asset GUID '{actualAssetGuid}' != descriptor GUID '{record.AssetGuid}'");
            }

            string fileName = Path.GetFileNameWithoutExtension(actualAssetPath);
            if (!string.Equals(fileName, record.SceneName, StringComparison.Ordinal))
            {
                diffs.Add($"file name '{fileName}' != descriptor scene name '{record.SceneName}' (exact case)");
            }

            if (scene.IsValid() && !string.Equals(scene.name, record.SceneName, StringComparison.Ordinal))
            {
                diffs.Add($"internal scene name '{scene.name}' != descriptor scene name '{record.SceneName}' (exact case)");
            }

            // Required controller present exactly once.
            int controllerCount = 0;
            Component controller = null;
            Type controllerType = ResolveType(record.RequiredControllerType);
            if (controllerType == null)
            {
                diffs.Add($"required controller type '{record.RequiredControllerType}' could not be resolved");
            }
            else
            {
                List<Component> controllers = FindComponents(roots, controllerType);
                controllerCount = controllers.Count;
                controller = controllers.FirstOrDefault();
                if (controllerCount != 1)
                {
                    diffs.Add($"expected exactly one {controllerType.Name}, found {controllerCount}");
                }
            }

            // Exactly one EventSystem.
            int eventSystemCount = FindComponents(roots, typeof(EventSystem)).Count;
            if (eventSystemCount != 1)
            {
                diffs.Add($"expected exactly one EventSystem, found {eventSystemCount}");
            }

            // Exactly one Bootloader owner (accepted #153/#241 per-scene owner contract).
            Type bootloaderType = ResolveType("AL.Core.Bootloader");
            int bootloaderCount = bootloaderType == null ? 0 : FindComponents(roots, bootloaderType).Count;
            if (bootloaderType == null)
            {
                diffs.Add("Bootloader type could not be resolved");
            }
            else if (bootloaderCount != 1)
            {
                diffs.Add($"expected exactly one Bootloader owner, found {bootloaderCount}");
            }

            // Boot additionally carries BootController on the same owner arrangement.
            if (string.Equals(record.SceneId, ProductionSceneDescriptor.BootSceneId, StringComparison.Ordinal))
            {
                Type bootControllerType = ResolveType("AL.UI.BootController");
                int bootControllerCount = bootControllerType == null ? 0 : FindComponents(roots, bootControllerType).Count;
                if (bootControllerCount != 1)
                {
                    diffs.Add($"Boot scene expected exactly one BootController, found {bootControllerCount}");
                }
            }

            // Exactly one startup marker whose serialized fields match the descriptor.
            List<Component> markers = FindComponents(roots, typeof(SceneStartupMarker));
            int markerCount = markers.Count;
            if (markerCount != 1)
            {
                diffs.Add($"expected exactly one SceneStartupMarker, found {markerCount}");
            }
            else
            {
                var marker = (SceneStartupMarker)markers[0];
                if (!string.Equals(marker.SceneId, record.SceneId, StringComparison.Ordinal))
                {
                    diffs.Add($"marker sceneId '{marker.SceneId}' != descriptor '{record.SceneId}'");
                }

                if (!string.Equals(marker.ExpectedAssetPath, record.AssetPath, StringComparison.Ordinal))
                {
                    diffs.Add($"marker expectedAssetPath '{marker.ExpectedAssetPath}' != descriptor '{record.AssetPath}'");
                }

                if (!string.Equals(marker.Role, record.Role, StringComparison.Ordinal))
                {
                    diffs.Add($"marker role '{marker.Role}' != descriptor '{record.Role}'");
                }

                if (!string.Equals(marker.SourceVersion, ProductionSceneDescriptor.SourceVersion, StringComparison.Ordinal))
                {
                    diffs.Add($"marker sourceVersion '{marker.SourceVersion}' != descriptor '{ProductionSceneDescriptor.SourceVersion}'");
                }
            }

            // Missing scripts anywhere in the scene.
            int missingScripts = 0;
            foreach (GameObject root in roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
            }

            if (missingScripts != 0)
            {
                diffs.Add($"found {missingScripts} missing script component(s)");
            }

            // Serialized transition strings for active, field-backed transitions must match exactly.
            if (controller != null)
            {
                var serialized = new SerializedObject(controller);
                foreach (SceneTransition transition in record.TransitionTargets)
                {
                    if (transition.Status != TransitionStatus.Active || !transition.HasSerializedField)
                    {
                        continue;
                    }

                    SerializedProperty property = serialized.FindProperty(transition.SerializedFieldName);
                    if (property == null)
                    {
                        diffs.Add($"controller missing serialized transition field '{transition.SerializedFieldName}'");
                        continue;
                    }

                    transitionValues[transition.SerializedFieldName] = property.stringValue;
                    if (!string.Equals(property.stringValue, transition.SerializedValue, StringComparison.Ordinal))
                    {
                        diffs.Add($"transition field '{transition.SerializedFieldName}' = '{property.stringValue}' != descriptor '{transition.SerializedValue}' (exact case)");
                    }
                }
            }

            return new SceneValidationResult(
                record.SceneId,
                record.AssetPath,
                actualAssetPath,
                scene.IsValid() ? scene.name : string.Empty,
                record.AssetGuid,
                actualAssetGuid,
                diffs.Count == 0 ? SceneValidationStatus.Valid : SceneValidationStatus.Drifted,
                rootNames,
                controllerCount,
                eventSystemCount,
                markerCount,
                bootloaderCount,
                missingScripts,
                transitionValues,
                diffs);
        }

        private static List<Component> FindComponents(GameObject[] roots, Type type)
        {
            var found = new List<Component>();
            foreach (GameObject root in roots)
            {
                foreach (Component component in root.GetComponentsInChildren(type, true))
                {
                    if (component != null)
                    {
                        found.Add(component);
                    }
                }
            }

            return found;
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
    }

    /// <summary>Aggregate validation report across the production scene inventory.</summary>
    public sealed class SceneValidationReport
    {
        public SceneValidationReport(IEnumerable<SceneValidationResult> scenes, IEnumerable<string> inventoryFailures)
        {
            Scenes = (scenes ?? Array.Empty<SceneValidationResult>()).ToList().AsReadOnly();
            InventoryFailures = (inventoryFailures ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public IReadOnlyList<SceneValidationResult> Scenes { get; }
        public IReadOnlyList<string> InventoryFailures { get; }

        public bool IsValid =>
            InventoryFailures.Count == 0 &&
            Scenes.All(scene => scene.Status == SceneValidationStatus.Valid);

        public IReadOnlyList<SceneValidationResult> Missing =>
            Scenes.Where(scene => scene.Status == SceneValidationStatus.Missing).ToList().AsReadOnly();

        public IReadOnlyList<SceneValidationResult> Drifted =>
            Scenes.Where(scene => scene.Status == SceneValidationStatus.Drifted).ToList().AsReadOnly();

        public string Summarize()
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("[AL-SCENE-VALIDATION] valid=").Append(IsValid);
            foreach (SceneValidationResult scene in Scenes)
            {
                builder.Append('\n').Append("  ").Append(scene.SceneId).Append(": ").Append(scene.Status);
                foreach (string diff in scene.Diffs)
                {
                    builder.Append('\n').Append("    - ").Append(diff);
                }
            }

            foreach (string failure in InventoryFailures)
            {
                builder.Append('\n').Append("  inventory: ").Append(failure);
            }

            return builder.ToString();
        }
    }

    /// <summary>Per-scene validation detail (path/GUID/root/component/transition inventory + diffs).</summary>
    public sealed class SceneValidationResult
    {
        public SceneValidationResult(
            string sceneId,
            string expectedAssetPath,
            string actualAssetPath,
            string sceneName,
            string expectedAssetGuid,
            string actualAssetGuid,
            SceneValidationStatus status,
            IEnumerable<string> rootNames,
            int controllerCount,
            int eventSystemCount,
            int markerCount,
            int bootloaderCount,
            int missingScriptCount,
            IReadOnlyDictionary<string, string> transitionValues,
            IEnumerable<string> diffs)
        {
            SceneId = sceneId ?? string.Empty;
            ExpectedAssetPath = expectedAssetPath ?? string.Empty;
            ActualAssetPath = actualAssetPath ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            ExpectedAssetGuid = expectedAssetGuid ?? string.Empty;
            ActualAssetGuid = actualAssetGuid ?? string.Empty;
            Status = status;
            RootNames = (rootNames ?? Array.Empty<string>()).ToList().AsReadOnly();
            ControllerCount = controllerCount;
            EventSystemCount = eventSystemCount;
            MarkerCount = markerCount;
            BootloaderCount = bootloaderCount;
            MissingScriptCount = missingScriptCount;
            TransitionValues = new Dictionary<string, string>(
                transitionValues ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            Diffs = (diffs ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public string SceneId { get; }
        public string ExpectedAssetPath { get; }
        public string ActualAssetPath { get; }
        public string SceneName { get; }
        public string ExpectedAssetGuid { get; }
        public string ActualAssetGuid { get; }
        public SceneValidationStatus Status { get; }
        public IReadOnlyList<string> RootNames { get; }
        public int ControllerCount { get; }
        public int EventSystemCount { get; }
        public int MarkerCount { get; }
        public int BootloaderCount { get; }
        public int MissingScriptCount { get; }
        public IReadOnlyDictionary<string, string> TransitionValues { get; }
        public IReadOnlyList<string> Diffs { get; }

        public static SceneValidationResult MissingPath(ProductionSceneRecord record)
        {
            return new SceneValidationResult(
                record.SceneId,
                record.AssetPath,
                string.Empty,
                string.Empty,
                record.AssetGuid,
                string.Empty,
                SceneValidationStatus.Missing,
                Array.Empty<string>(),
                0,
                0,
                0,
                0,
                0,
                new Dictionary<string, string>(),
                new[] { $"scene asset not found at '{record.AssetPath}'" });
        }
    }
}
#endif
