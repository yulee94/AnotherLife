using System;
using UnityEditor;
using UnityEngine;

namespace AL.Editor.World
{
    [FilePath(
        "Library/AnotherLife/WorldAuthoringWorkspaceState.asset",
        FilePathAttribute.Location.ProjectFolder)]
    public sealed class WorldAuthoringWorkspaceState :
        ScriptableSingleton<WorldAuthoringWorkspaceState>
    {
        [SerializeField] private string selectedDimensionId = string.Empty;
        [SerializeField] private string selectedWorldId = string.Empty;
        [SerializeField] private string selectedChunkId = string.Empty;
        [SerializeField] private bool showSceneOverlay = true;
        [SerializeField] private bool showNeighborLabels = true;

        public string SelectedDimensionId => selectedDimensionId;
        public string SelectedWorldId => selectedWorldId;
        public string SelectedChunkId => selectedChunkId;
        public bool ShowSceneOverlay => showSceneOverlay;
        public bool ShowNeighborLabels => showNeighborLabels;

        public WorldAuthoringSelection Selection => new WorldAuthoringSelection(
            selectedDimensionId,
            selectedWorldId,
            selectedChunkId);

        public bool ApplySelection(
            WorldAuthoringSelection selection,
            string undoLabel = "Select World Authoring Chunk")
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }
            if (string.Equals(
                    selectedDimensionId,
                    selection.DimensionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    selectedWorldId,
                    selection.WorldId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    selectedChunkId,
                    selection.ChunkId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            int undoGroup = BeginUndoGroup(undoLabel);
            selectedDimensionId = selection.DimensionId;
            selectedWorldId = selection.WorldId;
            selectedChunkId = selection.ChunkId;
            Save(true);
            Undo.CollapseUndoOperations(undoGroup);
            return true;
        }

        public bool ApplyOverlayOptions(
            bool showOverlay,
            bool showLabels,
            string undoLabel = "Change World Authoring Overlay")
        {
            if (showSceneOverlay == showOverlay &&
                showNeighborLabels == showLabels)
            {
                return false;
            }

            int undoGroup = BeginUndoGroup(undoLabel);
            showSceneOverlay = showOverlay;
            showNeighborLabels = showLabels;
            Save(true);
            Undo.CollapseUndoOperations(undoGroup);
            return true;
        }

        public void PersistAfterUndo()
        {
            Save(true);
        }

        private int BeginUndoGroup(string undoLabel)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoLabel);
            Undo.RegisterCompleteObjectUndo(this, undoLabel);
            return undoGroup;
        }
    }
}
