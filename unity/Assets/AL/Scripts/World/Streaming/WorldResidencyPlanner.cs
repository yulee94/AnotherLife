using System;
using System.Collections.Generic;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;

namespace AL.World.Streaming
{
    public sealed class WorldResidencyPlan
    {
        internal WorldResidencyPlan(
            string worldId,
            IList<string> requiredChunkIds,
            IList<string> loadChunkIds,
            IList<string> unloadChunkIds)
        {
            WorldId = worldId;
            RequiredChunkIds = Array.AsReadOnly(requiredChunkIds.ToArray());
            LoadChunkIds = Array.AsReadOnly(loadChunkIds.ToArray());
            UnloadChunkIds = Array.AsReadOnly(unloadChunkIds.ToArray());
        }

        public string WorldId { get; }
        public IReadOnlyList<string> RequiredChunkIds { get; }
        public IReadOnlyList<string> LoadChunkIds { get; }
        public IReadOnlyList<string> UnloadChunkIds { get; }
    }

    public static class WorldResidencyPlanner
    {
        public static WorldResidencyPlan Plan(
            WorldStreamingSnapshot snapshot,
            string activeWorldId,
            string focusChunkId,
            IEnumerable<string> currentlyLoadedChunkIds)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            WorldInstanceDefinition world = snapshot.GetWorld(activeWorldId) ??
                throw new ArgumentException("Unknown active world.", nameof(activeWorldId));
            WorldChunkDefinition focus = snapshot.GetChunk(focusChunkId) ??
                throw new ArgumentException("Unknown focus chunk.", nameof(focusChunkId));
            if (!string.Equals(focus.WorldId, world.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Focus chunk does not belong to the active world.",
                    nameof(focusChunkId));
            }

            var required = new List<string> { focus.Id };
            foreach (string neighborId in focus.NeighborIds)
            {
                WorldChunkDefinition neighbor = snapshot.GetChunk(neighborId);
                if (neighbor != null &&
                    string.Equals(neighbor.WorldId, world.Id, StringComparison.Ordinal) &&
                    !required.Contains(neighbor.Id))
                {
                    required.Add(neighbor.Id);
                }
            }

            var current = new HashSet<string>(
                currentlyLoadedChunkIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var requiredSet = new HashSet<string>(required, StringComparer.Ordinal);
            List<string> load = required
                .Where(id => !current.Contains(id))
                .ToList();
            List<string> unload = current
                .Where(id => !requiredSet.Contains(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            return new WorldResidencyPlan(world.Id, required, load, unload);
        }
    }
}
