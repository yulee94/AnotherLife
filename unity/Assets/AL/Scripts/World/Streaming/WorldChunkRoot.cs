using UnityEngine;

namespace AL.World.Streaming
{
    [DisallowMultipleComponent]
    public sealed class WorldChunkRoot : MonoBehaviour
    {
        [SerializeField] private string dimensionId = string.Empty;
        [SerializeField] private string worldId = string.Empty;
        [SerializeField] private string chunkId = string.Empty;
        [SerializeField] private string blockoutArchetype = string.Empty;
        [SerializeField] private float chunkSpanMeters;
        [SerializeField] private bool provisionalCoordinates = true;

        public string DimensionId => dimensionId;
        public string WorldId => worldId;
        public string ChunkId => chunkId;
        public string BlockoutArchetype => blockoutArchetype;
        public float ChunkSpanMeters => chunkSpanMeters;
        public bool ProvisionalCoordinates => provisionalCoordinates;

        public void Configure(
            string configuredDimensionId,
            string configuredWorldId,
            string configuredChunkId,
            string configuredBlockoutArchetype,
            float configuredChunkSpanMeters)
        {
            dimensionId = configuredDimensionId ?? string.Empty;
            worldId = configuredWorldId ?? string.Empty;
            chunkId = configuredChunkId ?? string.Empty;
            blockoutArchetype = configuredBlockoutArchetype ?? string.Empty;
            chunkSpanMeters = Mathf.Max(1f, configuredChunkSpanMeters);
            provisionalCoordinates = true;
        }
    }
}