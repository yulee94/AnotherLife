using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;
using AL.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Editor.World
{
    public enum WorldAuthoringPreflightSeverity
    {
        Information,
        Warning,
        Error
    }

    public sealed class WorldAuthoringPreflightIssue
    {
        internal WorldAuthoringPreflightIssue(
            WorldAuthoringPreflightSeverity severity,
            string code,
            string relatedId,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            RelatedId = relatedId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public WorldAuthoringPreflightSeverity Severity { get; }
        public string Code { get; }
        public string RelatedId { get; }
        public string Message { get; }
        public string Fingerprint => string.Join(
            "|",
            Severity,
            Code,
            RelatedId,
            Message);
    }

    public sealed class WorldAuthoringChunkInspection
    {
        public WorldAuthoringChunkInspection(
            string chunkId,
            string scenePath,
            bool sceneExists,
            int chunkRootCount,
            bool chunkRootMatchesCatalog,
            int solidColliderCount,
            bool hasNavigationData,
            IList<string> replacementSocketIds)
            : this(
                chunkId,
                scenePath,
                sceneExists,
                chunkRootCount,
                chunkRootMatchesCatalog,
                solidColliderCount,
                hasNavigationData,
                replacementSocketIds,
                new[]
                {
                    new WorldChunkPhysicalGroundDiagnostic(
                        WorldChunkLoadFailureCodes.PhysicalGroundAuthorityMissing,
                        chunkId,
                        "No explicit physical-ground authority was inspected.")
                })
        {
        }

        public WorldAuthoringChunkInspection(
            string chunkId,
            string scenePath,
            bool sceneExists,
            int chunkRootCount,
            bool chunkRootMatchesCatalog,
            int solidColliderCount,
            bool hasNavigationData,
            IList<string> replacementSocketIds,
            IList<WorldChunkPhysicalGroundDiagnostic> physicalGroundDiagnostics)
        {
            ChunkId = chunkId ?? string.Empty;
            ScenePath = scenePath ?? string.Empty;
            SceneExists = sceneExists;
            ChunkRootCount = chunkRootCount;
            ChunkRootMatchesCatalog = chunkRootMatchesCatalog;
            SolidColliderCount = solidColliderCount;
            HasNavigationData = hasNavigationData;
            ReplacementSocketIds = Array.AsReadOnly(
                (replacementSocketIds ?? Array.Empty<string>()).ToArray());
            PhysicalGroundDiagnostics = Array.AsReadOnly(
                (physicalGroundDiagnostics ??
                    Array.Empty<WorldChunkPhysicalGroundDiagnostic>())
                .Where(value => value != null)
                .ToArray());
        }

        public string ChunkId { get; }
        public string ScenePath { get; }
        public bool SceneExists { get; }
        public int ChunkRootCount { get; }
        public bool ChunkRootMatchesCatalog { get; }
        public int SolidColliderCount { get; }
        public bool HasNavigationData { get; }
        public IReadOnlyList<string> ReplacementSocketIds { get; }
        public IReadOnlyList<WorldChunkPhysicalGroundDiagnostic>
            PhysicalGroundDiagnostics { get; }
        public bool HasSafePhysicalGround => PhysicalGroundDiagnostics.Count == 0;
    }

    public sealed class WorldAuthoringDependencyStatus
    {
        public WorldAuthoringDependencyStatus(
            bool hasProductionChunkLoader,
            bool hasPlayFromHereBridge,
            bool hasCatalogBackedContentBudgets)
        {
            HasProductionChunkLoader = hasProductionChunkLoader;
            HasPlayFromHereBridge = hasPlayFromHereBridge;
            HasCatalogBackedContentBudgets = hasCatalogBackedContentBudgets;
        }

        public bool HasProductionChunkLoader { get; }
        public bool HasPlayFromHereBridge { get; }
        public bool HasCatalogBackedContentBudgets { get; }
    }

    public sealed class WorldAuthoringPreflightReport
    {
        internal WorldAuthoringPreflightReport(
            WorldAuthoringSelectionContext context,
            IList<WorldAuthoringChunkInspection> inspections,
            IList<WorldAuthoringPreflightIssue> issues)
        {
            Context = context;
            Inspections = Array.AsReadOnly(inspections.ToArray());
            Issues = Array.AsReadOnly(issues.ToArray());
        }

        public WorldAuthoringSelectionContext Context { get; }
        public IReadOnlyList<WorldAuthoringChunkInspection> Inspections { get; }
        public IReadOnlyList<WorldAuthoringPreflightIssue> Issues { get; }
        public bool IsReadyForPlay => Issues.All(
            value => value.Severity != WorldAuthoringPreflightSeverity.Error);
    }

    public sealed class WorldAuthoringPlayRequest
    {
        internal WorldAuthoringPlayRequest(WorldAuthoringSelectionContext context)
        {
            DimensionId = context.Dimension.Id;
            WorldId = context.World.Id;
            FocusChunkId = context.Focus.Id;
            RequiredScenePaths = Array.AsReadOnly(
                context.FocusAndNeighbors.Select(value => value.ScenePath).ToArray());
        }

        public string DimensionId { get; }
        public string WorldId { get; }
        public string FocusChunkId { get; }
        public IReadOnlyList<string> RequiredScenePaths { get; }
    }

    public interface IWorldAuthoringPlayFromHereBridge
    {
        bool TryPrepare(
            WorldAuthoringPlayRequest request,
            out string failureMessage);
    }

    public static class WorldAuthoringPreflight
    {
        public const string MissingProductionLoaderCode =
            "AL-WORLD-AUTHORING-PRODUCTION-LOADER-MISSING";
        public const string MissingPlayBridgeCode =
            "AL-WORLD-AUTHORING-PLAY-BRIDGE-MISSING";
        public const string MissingContentBudgetCode =
            "AL-WORLD-AUTHORING-CONTENT-BUDGET-MISSING";
        public const string MissingSceneCode =
            "AL-WORLD-AUTHORING-SCENE-MISSING";
        public const string InvalidChunkRootCode =
            "AL-WORLD-AUTHORING-CHUNK-ROOT-INVALID";
        public const string MissingColliderCode =
            "AL-WORLD-AUTHORING-COLLIDER-MISSING";
        public const string MissingNavigationCode =
            "AL-WORLD-AUTHORING-NAVIGATION-MISSING";
        public const string MissingPhysicalGroundAuthorityCode =
            "AL-WORLD-AUTHORING-PHYSICAL-GROUND-AUTHORITY-MISSING";
        public const string GroundColliderMissingCode =
            "AL-WORLD-AUTHORING-GROUND-COLLIDER-MISSING";
        public const string GroundColliderDisabledCode =
            "AL-WORLD-AUTHORING-GROUND-COLLIDER-DISABLED";
        public const string GroundColliderUnboundCode =
            "AL-WORLD-AUTHORING-GROUND-COLLIDER-UNBOUND";
        public const string GroundColliderInvalidCode =
            "AL-WORLD-AUTHORING-GROUND-COLLIDER-INVALID";
        public const string GroundRenderMeshReusedCode =
            "AL-WORLD-AUTHORING-GROUND-RENDER-MESH-REUSED";
        public const string GroundReviewMissingCode =
            "AL-WORLD-AUTHORING-GROUND-REVIEW-MISSING";
        public const string UnsafeChunkEdgeCode =
            "AL-WORLD-AUTHORING-CHUNK-EDGE-UNSAFE";
        public const string UnprovenChunkSeamCode =
            "AL-WORLD-AUTHORING-CHUNK-SEAM-CONTINUITY-UNPROVEN";
        public const string ReplacementSocketMismatchCode =
            "AL-WORLD-AUTHORING-SOCKET-ID-MISMATCH";

        public static WorldAuthoringPreflightReport Evaluate(
            WorldAuthoringSelectionContext context,
            IEnumerable<WorldAuthoringChunkInspection> inspections,
            WorldAuthoringDependencyStatus dependencies)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            var inspectionList = (inspections ??
                    Array.Empty<WorldAuthoringChunkInspection>())
                .Where(value => value != null)
                .ToList();
            var byChunkId = inspectionList
                .GroupBy(value => value.ChunkId, StringComparer.Ordinal)
                .ToDictionary(
                    value => value.Key,
                    value => value.First(),
                    StringComparer.Ordinal);
            var issues = new List<WorldAuthoringPreflightIssue>();
            if (!dependencies.HasProductionChunkLoader)
            {
                Add(
                    issues,
                    WorldAuthoringPreflightSeverity.Error,
                    MissingProductionLoaderCode,
                    context.World.Id,
                    "No non-test runtime IWorldChunkLoader implementation is available.");
            }
            if (!dependencies.HasPlayFromHereBridge)
            {
                Add(
                    issues,
                    WorldAuthoringPreflightSeverity.Error,
                    MissingPlayBridgeCode,
                    context.Focus.Id,
                    "No Editor bridge can hand the selected catalog IDs to the runtime bootstrap.");
            }
            if (!dependencies.HasCatalogBackedContentBudgets)
            {
                Add(
                    issues,
                    WorldAuthoringPreflightSeverity.Warning,
                    MissingContentBudgetCode,
                    context.Dimension.Id,
                    "No approved catalog defines per-chunk content or performance budgets; the overlay shows only the authoritative spatial envelope and declared seam counts.");
            }

            foreach (WorldChunkDefinition chunk in context.FocusAndNeighbors)
            {
                if (!byChunkId.TryGetValue(
                        chunk.Id,
                        out WorldAuthoringChunkInspection inspection) ||
                    !inspection.SceneExists)
                {
                    Add(
                        issues,
                        WorldAuthoringPreflightSeverity.Error,
                        MissingSceneCode,
                        chunk.Id,
                        "The catalog scene is not available for inspection: " +
                        chunk.ScenePath);
                    continue;
                }

                if (inspection.ChunkRootCount != 1 ||
                    !inspection.ChunkRootMatchesCatalog)
                {
                    Add(
                        issues,
                        WorldAuthoringPreflightSeverity.Error,
                        InvalidChunkRootCode,
                        chunk.Id,
                        "The scene must contain exactly one WorldChunkRoot whose dimension, world, chunk, archetype, span, and provisional origin match the catalog.");
                }
                if (inspection.SolidColliderCount == 0)
                {
                    Add(
                        issues,
                        WorldAuthoringPreflightSeverity.Error,
                        MissingColliderCode,
                        chunk.Id,
                        "The scene has no enabled, active, non-trigger 3D collider.");
                }
                if (!inspection.HasNavigationData)
                {
                    Add(
                        issues,
                        WorldAuthoringPreflightSeverity.Error,
                        MissingNavigationCode,
                        chunk.Id,
                        "The scene has no discoverable baked NavMeshData or populated NavMeshSurface.");
                }
                foreach (WorldChunkPhysicalGroundDiagnostic diagnostic in
                         inspection.PhysicalGroundDiagnostics)
                {
                    Add(
                        issues,
                        WorldAuthoringPreflightSeverity.Error,
                        AuthoringPhysicalGroundCode(diagnostic.Code),
                        chunk.Id,
                        string.IsNullOrWhiteSpace(diagnostic.RelatedObject)
                            ? diagnostic.Message
                            : diagnostic.RelatedObject + ": " + diagnostic.Message);
                }

                string[] expectedSockets = chunk.ReplacementSocketIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string[] actualSockets = inspection.ReplacementSocketIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (!expectedSockets.SequenceEqual(actualSockets, StringComparer.Ordinal))
                {
                    Add(
                        issues,
                        WorldAuthoringPreflightSeverity.Error,
                        ReplacementSocketMismatchCode,
                        chunk.Id,
                        "Scene replacement-socket IDs do not exactly match the catalog-owned stable ID set.");
                }
            }

            WorldAuthoringPreflightIssue[] sorted = issues
                .OrderByDescending(value => value.Severity)
                .ThenBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.RelatedId, StringComparer.Ordinal)
                .ThenBy(value => value.Message, StringComparer.Ordinal)
                .ToArray();
            return new WorldAuthoringPreflightReport(
                context,
                inspectionList,
                sorted);
        }

        private static void Add(
            ICollection<WorldAuthoringPreflightIssue> issues,
            WorldAuthoringPreflightSeverity severity,
            string code,
            string relatedId,
            string message)
        {
            issues.Add(new WorldAuthoringPreflightIssue(
                severity,
                code,
                relatedId,
                message));
        }

        private static string AuthoringPhysicalGroundCode(string runtimeCode)
        {
            switch (runtimeCode)
            {
                case WorldChunkLoadFailureCodes.PhysicalGroundAuthorityMissing:
                    return MissingPhysicalGroundAuthorityCode;
                case WorldChunkLoadFailureCodes.GroundColliderMissing:
                    return GroundColliderMissingCode;
                case WorldChunkLoadFailureCodes.GroundColliderDisabled:
                    return GroundColliderDisabledCode;
                case WorldChunkLoadFailureCodes.GroundColliderUnbound:
                    return GroundColliderUnboundCode;
                case WorldChunkLoadFailureCodes.GroundRenderMeshReused:
                    return GroundRenderMeshReusedCode;
                case WorldChunkLoadFailureCodes.GroundReviewMissing:
                    return GroundReviewMissingCode;
                case WorldChunkLoadFailureCodes.ChunkEdgeUnsafe:
                    return UnsafeChunkEdgeCode;
                case WorldChunkLoadFailureCodes.ChunkSeamContinuityUnproven:
                    return UnprovenChunkSeamCode;
                default:
                    return GroundColliderInvalidCode;
            }
        }
    }

    public static class WorldAuthoringDependencyDiscovery
    {
        public static WorldAuthoringDependencyStatus Discover()
        {
            return new WorldAuthoringDependencyStatus(
                HasProductionChunkLoader(),
                TryGetPlayBridge(out _, out _),
                false);
        }

        public static bool TryGetPlayBridge(
            out IWorldAuthoringPlayFromHereBridge bridge,
            out string failureMessage)
        {
            Type[] candidates = TypeCache
                .GetTypesDerivedFrom<IWorldAuthoringPlayFromHereBridge>()
                .Where(value => value.IsClass && !value.IsAbstract)
                .OrderBy(value => value.FullName, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length != 1)
            {
                bridge = null;
                failureMessage = candidates.Length == 0
                    ? "No play-from-here bridge is registered."
                    : "Multiple play-from-here bridges are registered; ownership is ambiguous.";
                return false;
            }

            try
            {
                bridge = (IWorldAuthoringPlayFromHereBridge)
                    Activator.CreateInstance(candidates[0]);
                failureMessage = string.Empty;
                return bridge != null;
            }
            catch (Exception error)
            {
                bridge = null;
                failureMessage = "The play-from-here bridge could not be created: " +
                    error.GetType().Name;
                return false;
            }
        }

        private static bool HasProductionChunkLoader()
        {
            return TypeCache.GetTypesDerivedFrom<IWorldChunkLoader>()
                .Any(value =>
                    value.IsClass &&
                    !value.IsAbstract &&
                    IsProductionRuntimeAssembly(value.Assembly.GetName().Name));
        }

        private static bool IsProductionRuntimeAssembly(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return false;
            }

            return assemblyName.IndexOf("Tests", StringComparison.OrdinalIgnoreCase) < 0 &&
                !assemblyName.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assemblyName, "AL.Editor", StringComparison.Ordinal);
        }
    }

    public static class WorldAuthoringSceneInspector
    {
        public static IReadOnlyList<WorldAuthoringChunkInspection> Inspect(
            WorldStreamingSnapshot snapshot,
            WorldAuthoringSelectionContext context)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return Array.AsReadOnly(
                context.FocusAndNeighbors
                    .Select(value => Inspect(snapshot, value))
                    .ToArray());
        }

        public static WorldAuthoringChunkInspection Inspect(
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            if (!SceneAssetExists(chunk.ScenePath))
            {
                return Missing(chunk);
            }

            Scene scene = SceneManager.GetSceneByPath(chunk.ScenePath);
            bool openedForInspection = !scene.IsValid() || !scene.isLoaded;
            Scene previousActiveScene = SceneManager.GetActiveScene();
            if (openedForInspection)
            {
                scene = EditorSceneManager.OpenScene(
                    chunk.ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                WorldChunkRoot[] chunkRoots = roots
                    .SelectMany(value =>
                        value.GetComponentsInChildren<WorldChunkRoot>(true))
                    .ToArray();
                WorldReplacementSocket[] sockets = roots
                    .SelectMany(value =>
                        value.GetComponentsInChildren<WorldReplacementSocket>(true))
                    .ToArray();
                int solidColliderCount = roots
                    .SelectMany(value => value.GetComponentsInChildren<Collider>(true))
                    .Count(value =>
                        value != null &&
                        value.enabled &&
                        !value.isTrigger &&
                        value.gameObject.activeInHierarchy);
                WorldChunkPhysicalGroundReadiness physicalGround =
                    WorldChunkPhysicalGroundValidator.Evaluate(
                        scene,
                        snapshot,
                        chunk);

                return new WorldAuthoringChunkInspection(
                    chunk.Id,
                    chunk.ScenePath,
                    true,
                    chunkRoots.Length,
                    chunkRoots.Length == 1 &&
                        RootMatches(snapshot, chunk, chunkRoots[0]),
                    solidColliderCount,
                    HasNavigationData(chunk.ScenePath, roots),
                    sockets.Select(value => value.SocketId).ToArray(),
                    physicalGround.Diagnostics.ToArray());
            }
            finally
            {
                if (openedForInspection && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        private static bool SceneAssetExists(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return false;
            }

            string absolutePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                scenePath));
            return File.Exists(absolutePath);
        }

        private static WorldAuthoringChunkInspection Missing(
            WorldChunkDefinition chunk)
        {
            return new WorldAuthoringChunkInspection(
                chunk.Id,
                chunk.ScenePath,
                false,
                0,
                false,
                0,
                false,
                Array.Empty<string>());
        }

        private static bool RootMatches(
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            WorldChunkRoot root)
        {
            WorldInstanceDefinition world = snapshot.GetWorld(chunk.WorldId);
            WorldDimensionDefinition dimension = world == null
                ? null
                : snapshot.GetDimension(world.DimensionId);
            if (world == null || dimension == null || root == null)
            {
                return false;
            }

            Vector3 expectedOrigin = new Vector3(
                chunk.GridX * dimension.ChunkSpanMeters,
                0f,
                chunk.GridZ * dimension.ChunkSpanMeters);
            return string.Equals(
                       root.DimensionId,
                       dimension.Id,
                       StringComparison.Ordinal) &&
                string.Equals(root.WorldId, world.Id, StringComparison.Ordinal) &&
                string.Equals(root.ChunkId, chunk.Id, StringComparison.Ordinal) &&
                string.Equals(
                    root.BlockoutArchetype,
                    chunk.BlockoutArchetype,
                    StringComparison.Ordinal) &&
                root.ProvisionalCoordinates &&
                Mathf.Abs(root.ChunkSpanMeters - dimension.ChunkSpanMeters) <= 0.001f &&
                Vector3.Distance(root.transform.position, expectedOrigin) <= 0.001f;
        }

        private static bool HasNavigationData(
            string scenePath,
            IEnumerable<GameObject> roots)
        {
            foreach (string dependencyPath in AssetDatabase.GetDependencies(
                         scenePath,
                         true))
            {
                Type dependencyType =
                    AssetDatabase.GetMainAssetTypeAtPath(dependencyPath);
                if (dependencyType != null &&
                    string.Equals(
                        dependencyType.FullName,
                        "UnityEngine.AI.NavMeshData",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (Component component in roots
                         .SelectMany(value =>
                             value.GetComponentsInChildren<Component>(true))
                         .Where(value => value != null &&
                             string.Equals(
                                 value.GetType().Name,
                                 "NavMeshSurface",
                                 StringComparison.Ordinal)))
            {
                using (var serialized = new SerializedObject(component))
                {
                    SerializedProperty data =
                        serialized.FindProperty("m_NavMeshData");
                    if (data != null && data.objectReferenceValue != null)
                    {
                        return true;
                    }
                }
            }

            return SceneTextReferencesNavigationData(scenePath);
        }

        private static bool SceneTextReferencesNavigationData(string scenePath)
        {
            string absolutePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                scenePath));
            try
            {
                foreach (string line in File.ReadLines(absolutePath))
                {
                    string value = line.TrimStart();
                    if (value.StartsWith(
                            "m_NavMeshData:",
                            StringComparison.Ordinal) &&
                        !value.Contains("{fileID: 0}", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch (IOException)
            {
                // Fail closed when a serialized scene cannot be inspected.
            }
            catch (UnauthorizedAccessException)
            {
                // Fail closed when a serialized scene cannot be inspected.
            }

            return false;
        }
    }
}
