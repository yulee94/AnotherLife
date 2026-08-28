using AL.Data.Catalogs.WorldStreaming;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.Editor.World
{
    [InitializeOnLoad]
    public static class WorldAuthoringSceneOverlay
    {
        private static readonly Color FocusColor =
            new Color(0.18f, 0.88f, 1f, 0.96f);
        private static readonly Color NeighborColor =
            new Color(1f, 0.72f, 0.20f, 0.72f);

        private static GUIStyle focusLabelStyle;
        private static GUIStyle neighborLabelStyle;

        static WorldAuthoringSceneOverlay()
        {
            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
        }

        public static void Repaint()
        {
            SceneView.RepaintAll();
        }

        private static GUIStyle CreateLabelStyle(Color color, FontStyle fontStyle)
        {
            return new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = fontStyle,
                normal = { textColor = color },
                padding = new RectOffset(6, 6, 4, 4)
            };
        }

        private static void Draw(SceneView sceneView)
        {
            WorldAuthoringWorkspaceState state =
                WorldAuthoringWorkspaceState.instance;
            if (!state.ShowSceneOverlay)
            {
                return;
            }

            WorldAuthoringCatalogRead read =
                WorldAuthoringCatalogProvider.LoadCanonical();
            if (!read.IsAccepted)
            {
                return;
            }
            EnsureLabelStyles();

            WorldAuthoringSelection selection =
                WorldAuthoringSelectionResolver.Resolve(
                    read.Snapshot,
                    state.SelectedDimensionId,
                    state.SelectedWorldId,
                    state.SelectedChunkId);
            WorldAuthoringSelectionContext context =
                WorldAuthoringSelectionResolver.BuildContext(
                    read.Snapshot,
                    selection);

            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            try
            {
                foreach (WorldChunkDefinition neighbor in context.Neighbors)
                {
                    DrawEnvelope(
                        read.Snapshot,
                        neighbor,
                        NeighborColor,
                        state.ShowNeighborLabels ? neighborLabelStyle : null,
                        false);
                }

                DrawEnvelope(
                    read.Snapshot,
                    context.Focus,
                    FocusColor,
                    focusLabelStyle,
                    true);
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private static void EnsureLabelStyles()
        {
            if (focusLabelStyle != null && neighborLabelStyle != null)
            {
                return;
            }

            focusLabelStyle = CreateLabelStyle(FocusColor, FontStyle.Bold);
            neighborLabelStyle = CreateLabelStyle(
                NeighborColor,
                FontStyle.Normal);
        }

        private static void DrawEnvelope(
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            Color color,
            GUIStyle labelStyle,
            bool focus)
        {
            WorldAuthoringChunkEnvelope envelope =
                WorldAuthoringSelectionResolver.BuildEnvelope(snapshot, chunk);
            Bounds bounds = envelope.Bounds;
            float half = bounds.size.x * 0.5f;
            Vector3 center = bounds.center;
            Vector3[] corners =
            {
                center + new Vector3(-half, 0f, -half),
                center + new Vector3(-half, 0f, half),
                center + new Vector3(half, 0f, half),
                center + new Vector3(half, 0f, -half),
                center + new Vector3(-half, 0f, -half)
            };
            Handles.color = color;
            Handles.DrawAAPolyLine(focus ? 4f : 2f, corners);
            if (labelStyle == null)
            {
                return;
            }

            string role = focus ? "FOCUS" : "NEIGHBOR";
            string label = string.Join(
                "\n",
                role + "  " + chunk.Id,
                chunk.BlockoutArchetype + "  •  " +
                envelope.Dimension.ChunkSpanMeters.ToString("0") + " m envelope",
                chunk.NeighborIds.Count + " neighbors  •  " +
                chunk.ReplacementSocketIds.Count + " stable replacement sockets",
                "content budget: not catalogued");
            Handles.Label(corners[1] + Vector3.up * 2f, label, labelStyle);
        }
    }
}
