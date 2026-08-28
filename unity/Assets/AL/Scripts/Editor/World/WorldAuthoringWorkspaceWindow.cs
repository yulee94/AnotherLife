using System;
using System.Collections.Generic;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;
using UnityEditor;
using UnityEngine;

namespace AL.Editor.World
{
    public sealed class WorldAuthoringWorkspaceWindow : EditorWindow
    {
        private const string MenuPath =
            "AnotherLife/World/World Authoring Workspace";

        private Vector2 scrollPosition;
        private WorldAuthoringPreflightReport lastReport;
        private string lastExecutionFailure = string.Empty;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<WorldAuthoringWorkspaceWindow>();
            window.titleContent = new GUIContent("World Authoring");
            window.minSize = new Vector2(440f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EnsureValidSelection();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnGUI()
        {
            DrawHeader();
            WorldAuthoringCatalogRead read =
                WorldAuthoringCatalogProvider.LoadCanonical();
            if (!read.IsAccepted)
            {
                DrawCatalogFailure(read);
                return;
            }

            WorldStreamingSnapshot snapshot = read.Snapshot;
            WorldAuthoringSelection selection = ResolveAndPersist(snapshot);
            WorldAuthoringSelectionContext context =
                WorldAuthoringSelectionResolver.BuildContext(snapshot, selection);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawSelectionBrowser(snapshot, ref selection, ref context);
            EditorGUILayout.Space(8f);
            DrawSelectedSet(context);
            EditorGUILayout.Space(8f);
            DrawOverlayControls();
            EditorGUILayout.Space(8f);
            DrawReadOnlySceneActions(snapshot, context);
            EditorGUILayout.Space(8f);
            DrawPreflight(snapshot, context);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.LabelField(
                "World Authoring Foundation",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Catalog IDs and topology are authoritative. Assets/AL/Worlds/Generated is derived blockout output: this workspace visualizes and inspects it, but never edits or saves generated scenes.",
                MessageType.Info);
        }

        private void DrawCatalogFailure(WorldAuthoringCatalogRead read)
        {
            EditorGUILayout.HelpBox(
                "The canonical world streaming catalog is unavailable or rejected. No fallback topology will be synthesized.",
                MessageType.Error);
            foreach (string diagnostic in read.Diagnostics)
            {
                EditorGUILayout.SelectableLabel(
                    diagnostic,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            if (GUILayout.Button("Reload Canonical Catalog"))
            {
                WorldAuthoringCatalogProvider.LoadCanonical(true);
                Repaint();
                WorldAuthoringSceneOverlay.Repaint();
            }
        }

        private void DrawSelectionBrowser(
            WorldStreamingSnapshot snapshot,
            ref WorldAuthoringSelection selection,
            ref WorldAuthoringSelectionContext context)
        {
            EditorGUILayout.LabelField("Catalog Browser", EditorStyles.boldLabel);

            WorldDimensionDefinition[] dimensions = snapshot.Dimensions.ToArray();
            int dimensionIndex = IndexOf(
                dimensions.Select(value => value.Id),
                selection.DimensionId);
            int nextDimensionIndex = EditorGUILayout.Popup(
                "Dimension",
                dimensionIndex,
                dimensions.Select(value => value.Id).ToArray());
            if (nextDimensionIndex != dimensionIndex)
            {
                selection = WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    dimensions[nextDimensionIndex].Id,
                    string.Empty,
                    string.Empty);
                ApplySelection(selection, "Select World Authoring Dimension");
                context = WorldAuthoringSelectionResolver.BuildContext(
                    snapshot,
                    selection);
            }

            WorldInstanceDefinition[] worlds = context.Dimension.WorldIds
                .Select(snapshot.GetWorld)
                .Where(value => value != null)
                .ToArray();
            int worldIndex = IndexOf(
                worlds.Select(value => value.Id),
                selection.WorldId);
            int nextWorldIndex = EditorGUILayout.Popup(
                "World",
                worldIndex,
                worlds.Select(value => value.Id).ToArray());
            if (nextWorldIndex != worldIndex)
            {
                selection = WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    context.Dimension.Id,
                    worlds[nextWorldIndex].Id,
                    string.Empty);
                ApplySelection(selection, "Select World Authoring World");
                context = WorldAuthoringSelectionResolver.BuildContext(
                    snapshot,
                    selection);
            }

            WorldChunkDefinition[] chunks = context.World.ChunkIds
                .Select(snapshot.GetChunk)
                .Where(value => value != null)
                .ToArray();
            int chunkIndex = IndexOf(
                chunks.Select(value => value.Id),
                selection.ChunkId);
            int nextChunkIndex = EditorGUILayout.Popup(
                "Focus chunk",
                chunkIndex,
                chunks.Select(value => value.Id).ToArray());
            if (nextChunkIndex != chunkIndex)
            {
                selection = WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    context.Dimension.Id,
                    context.World.Id,
                    chunks[nextChunkIndex].Id);
                ApplySelection(selection, "Select World Authoring Chunk");
                context = WorldAuthoringSelectionResolver.BuildContext(
                    snapshot,
                    selection);
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Mode", context.Dimension.Mode);
            EditorGUILayout.LabelField("Usage", context.World.Usage);
            EditorGUILayout.LabelField("Access", context.World.AccessPolicy);
            EditorGUILayout.LabelField("Archetype", context.Focus.BlockoutArchetype);
            EditorGUILayout.LabelField(
                "Provisional origin",
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "({0:0}, 0, {1:0}) m",
                    context.Focus.GridX * context.Dimension.ChunkSpanMeters,
                    context.Focus.GridZ * context.Dimension.ChunkSpanMeters));
            EditorGUILayout.LabelField(
                "Spatial envelope",
                context.Dimension.ChunkSpanMeters.ToString("0") + " m square");
            EditorGUILayout.LabelField(
                "Performance budget",
                "Not present in an approved catalog");
        }

        private void DrawSelectedSet(WorldAuthoringSelectionContext context)
        {
            EditorGUILayout.LabelField(
                "Selected + Same-World Neighbors",
                EditorStyles.boldLabel);
            DrawChunkRow(context.Focus, "FOCUS", true);
            foreach (WorldChunkDefinition neighbor in context.Neighbors)
            {
                DrawChunkRow(neighbor, "NEIGHBOR", false);
            }
        }

        private void DrawChunkRow(
            WorldChunkDefinition chunk,
            string role,
            bool selected)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(role, GUILayout.Width(72f));
                GUILayout.Label(chunk.Id, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    chunk.ReplacementSocketIds.Count + " sockets",
                    EditorStyles.miniLabel,
                    GUILayout.Width(68f));
                if (!selected && GUILayout.Button("Focus", GUILayout.Width(50f)))
                {
                    WorldAuthoringCatalogRead read =
                        WorldAuthoringCatalogProvider.LoadCanonical();
                    if (read.IsAccepted)
                    {
                        WorldAuthoringSelection next =
                            WorldAuthoringSelectionResolver.Resolve(
                                read.Snapshot,
                                WorldAuthoringWorkspaceState.instance
                                    .SelectedDimensionId,
                                chunk.WorldId,
                                chunk.Id);
                        ApplySelection(next, "Focus Neighbor World Chunk");
                    }
                }
            }
        }

        private static void DrawOverlayControls()
        {
            EditorGUILayout.LabelField("Scene View", EditorStyles.boldLabel);
            WorldAuthoringWorkspaceState state =
                WorldAuthoringWorkspaceState.instance;
            bool showOverlay = EditorGUILayout.Toggle(
                "Show chunk envelopes",
                state.ShowSceneOverlay);
            using (new EditorGUI.DisabledScope(!showOverlay))
            {
                bool showLabels = EditorGUILayout.Toggle(
                    "Show neighbor labels",
                    state.ShowNeighborLabels);
                if (showOverlay != state.ShowSceneOverlay ||
                    showLabels != state.ShowNeighborLabels)
                {
                    state.ApplyOverlayOptions(showOverlay, showLabels);
                    WorldAuthoringSceneOverlay.Repaint();
                }
            }
        }

        private static void DrawReadOnlySceneActions(
            WorldStreamingSnapshot snapshot,
            WorldAuthoringSelectionContext context)
        {
            EditorGUILayout.LabelField(
                "Read-Only Generated Preview",
                EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame Focus Bounds"))
                {
                    WorldAuthoringChunkEnvelope envelope =
                        WorldAuthoringSelectionResolver.BuildEnvelope(
                            snapshot,
                            context.Focus);
                    SceneView view = SceneView.lastActiveSceneView;
                    if (view != null)
                    {
                        view.Frame(envelope.Bounds, false);
                    }
                }
                if (GUILayout.Button("Ping Generated Scene"))
                {
                    SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        context.Focus.ScenePath);
                    if (scene != null)
                    {
                        EditorGUIUtility.PingObject(scene);
                    }
                }
            }
        }

        private void DrawPreflight(
            WorldStreamingSnapshot snapshot,
            WorldAuthoringSelectionContext context)
        {
            EditorGUILayout.LabelField(
                "Validate / Play From Here",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preflight inspects the focus chunk and its catalog neighbors read-only. Play Mode starts only after production loading, runtime handoff, navigation, collision, and stable scene IDs are all verified.",
                MessageType.None);
            if (GUILayout.Button("Run Preflight and Play From Here", GUILayout.Height(28f)))
            {
                RunPreflightAndMaybePlay(snapshot, context);
            }

            if (!string.IsNullOrWhiteSpace(lastExecutionFailure))
            {
                EditorGUILayout.HelpBox(lastExecutionFailure, MessageType.Error);
            }
            if (lastReport == null)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                lastReport.IsReadyForPlay
                    ? "Preflight accepted every required dependency."
                    : "Preflight blocked Play From Here. Resolve every error before retrying.",
                lastReport.IsReadyForPlay ? MessageType.Info : MessageType.Error);
            foreach (WorldAuthoringPreflightIssue issue in lastReport.Issues)
            {
                EditorGUILayout.HelpBox(
                    issue.Code + "\n" + issue.RelatedId + "\n" + issue.Message,
                    MessageTypeFor(issue.Severity));
            }

            EditorGUILayout.Space(3f);
            foreach (WorldAuthoringChunkInspection inspection in lastReport.Inspections)
            {
                EditorGUILayout.LabelField(
                    inspection.ChunkId,
                    "roots " + inspection.ChunkRootCount +
                    "  •  colliders " + inspection.SolidColliderCount +
                    "  •  nav " + (inspection.HasNavigationData ? "ready" : "missing") +
                    "  •  sockets " + inspection.ReplacementSocketIds.Count);
            }
        }

        private void RunPreflightAndMaybePlay(
            WorldStreamingSnapshot snapshot,
            WorldAuthoringSelectionContext context)
        {
            lastExecutionFailure = string.Empty;
            try
            {
                IReadOnlyList<WorldAuthoringChunkInspection> inspections =
                    WorldAuthoringSceneInspector.Inspect(snapshot, context);
                WorldAuthoringDependencyStatus dependencies =
                    WorldAuthoringDependencyDiscovery.Discover();
                lastReport = WorldAuthoringPreflight.Evaluate(
                    context,
                    inspections,
                    dependencies);
            }
            catch (Exception error)
            {
                lastReport = null;
                lastExecutionFailure =
                    "Read-only scene inspection failed: " +
                    error.GetType().Name + ": " + error.Message;
                return;
            }

            foreach (WorldAuthoringPreflightIssue issue in lastReport.Issues)
            {
                Debug.Log(
                    "World Authoring preflight: " + issue.Fingerprint);
            }
            if (!lastReport.IsReadyForPlay)
            {
                return;
            }

            if (!WorldAuthoringDependencyDiscovery.TryGetPlayBridge(
                    out IWorldAuthoringPlayFromHereBridge bridge,
                    out string discoveryFailure))
            {
                lastExecutionFailure = discoveryFailure;
                return;
            }
            if (!bridge.TryPrepare(
                    new WorldAuthoringPlayRequest(context),
                    out string preparationFailure))
            {
                lastExecutionFailure = string.IsNullOrWhiteSpace(preparationFailure)
                    ? "The play-from-here bridge rejected the request."
                    : preparationFailure;
                return;
            }

            EditorApplication.isPlaying = true;
        }

        private WorldAuthoringSelection ResolveAndPersist(
            WorldStreamingSnapshot snapshot)
        {
            WorldAuthoringWorkspaceState state =
                WorldAuthoringWorkspaceState.instance;
            WorldAuthoringSelection selection =
                WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    state.SelectedDimensionId,
                    state.SelectedWorldId,
                    state.SelectedChunkId);
            state.ApplySelection(
                selection,
                "Resolve World Authoring Catalog Selection");
            return selection;
        }

        private void EnsureValidSelection()
        {
            WorldAuthoringCatalogRead read =
                WorldAuthoringCatalogProvider.LoadCanonical();
            if (read.IsAccepted)
            {
                ResolveAndPersist(read.Snapshot);
            }
        }

        private void ApplySelection(
            WorldAuthoringSelection selection,
            string undoLabel)
        {
            if (WorldAuthoringWorkspaceState.instance.ApplySelection(
                    selection,
                    undoLabel))
            {
                lastReport = null;
                lastExecutionFailure = string.Empty;
                Repaint();
                WorldAuthoringSceneOverlay.Repaint();
            }
        }

        private void OnUndoRedo()
        {
            WorldAuthoringWorkspaceState.instance.PersistAfterUndo();
            lastReport = null;
            lastExecutionFailure = string.Empty;
            Repaint();
            WorldAuthoringSceneOverlay.Repaint();
        }

        private static int IndexOf(IEnumerable<string> values, string selectedId)
        {
            string[] array = values.ToArray();
            int index = Array.FindIndex(array, value => string.Equals(
                value,
                selectedId,
                StringComparison.Ordinal));
            return Mathf.Max(0, index);
        }

        private static MessageType MessageTypeFor(
            WorldAuthoringPreflightSeverity severity)
        {
            switch (severity)
            {
                case WorldAuthoringPreflightSeverity.Error:
                    return MessageType.Error;
                case WorldAuthoringPreflightSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }
    }
}
