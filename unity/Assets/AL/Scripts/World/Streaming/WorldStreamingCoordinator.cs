using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AL.Data.Catalogs.WorldStreaming;

namespace AL.World.Streaming
{
    public interface IWorldChunkLoader
    {
        IReadOnlyCollection<string> LoadedChunkIds { get; }

        // Implementations must fail closed: a false request must leave spatial
        // content hidden even if the returned task faults.
        Task SetSpatialVisibilityAsync(
            bool visible,
            CancellationToken cancellationToken);

        Task LoadAsync(
            WorldChunkDefinition chunk,
            CancellationToken cancellationToken);

        Task UnloadAsync(
            WorldChunkDefinition chunk,
            CancellationToken cancellationToken);
    }

    public sealed class WorldStreamingCoordinator
    {
        private readonly WorldStreamingSnapshot snapshot;
        private readonly IWorldChunkLoader loader;
        private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);

        public WorldStreamingCoordinator(
            WorldStreamingSnapshot snapshot,
            IWorldChunkLoader loader,
            string initialWorldId = null)
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            if (initialWorldId != null && snapshot.GetWorld(initialWorldId) == null)
            {
                throw new ArgumentException("Unknown initial world.", nameof(initialWorldId));
            }
            if (initialWorldId != null)
            {
                foreach (string chunkId in loader.LoadedChunkIds)
                {
                    WorldChunkDefinition chunk = snapshot.GetChunk(chunkId) ??
                        throw new InvalidOperationException(
                            "The chunk loader reported an unknown loaded chunk: " + chunkId);
                    if (!string.Equals(chunk.WorldId, initialWorldId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Initial residency does not match the claimed active world.");
                    }
                }
            }

            ActiveWorldId = initialWorldId;
        }

        public string ActiveWorldId { get; private set; }

        public async Task<WorldResidencyPlan> FocusAsync(
            string worldId,
            string focusChunkId,
            CancellationToken cancellationToken)
        {
            await operationGate.WaitAsync(cancellationToken);
            try
            {
                return await FocusCoreAsync(worldId, focusChunkId, cancellationToken);
            }
            finally
            {
                operationGate.Release();
            }
        }

        private async Task<WorldResidencyPlan> FocusCoreAsync(
            string worldId,
            string focusChunkId,
            CancellationToken cancellationToken)
        {
            string previousWorldId = ActiveWorldId;
            string[] previousResidency = loader.LoadedChunkIds.ToArray();
            bool hasForeignResidency = previousResidency.Any(chunkId =>
            {
                WorldChunkDefinition loadedChunk = snapshot.GetChunk(chunkId) ??
                    throw new InvalidOperationException(
                        "The chunk loader reported an unknown loaded chunk: " + chunkId);
                return !string.Equals(
                    loadedChunk.WorldId,
                    worldId,
                    StringComparison.Ordinal);
            });
            WorldResidencyPlan plan = WorldResidencyPlanner.Plan(
                snapshot,
                worldId,
                focusChunkId,
                loader.LoadedChunkIds);
            bool switchingWorlds = hasForeignResidency ||
                ActiveWorldId != null &&
                !string.Equals(ActiveWorldId, worldId, StringComparison.Ordinal);
            bool requiresHiddenLoadingShell = switchingWorlds || ActiveWorldId == null;
            if (requiresHiddenLoadingShell)
            {
                try
                {
                    await loader.SetSpatialVisibilityAsync(false, cancellationToken);
                    ActiveWorldId = null;
                    if (switchingWorlds)
                    {
                        string[] oldChunkIds = loader.LoadedChunkIds
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray();
                        foreach (string chunkId in oldChunkIds)
                        {
                            WorldChunkDefinition chunk = snapshot.GetChunk(chunkId) ??
                                throw new InvalidOperationException(
                                    "The chunk loader reported an unknown loaded chunk: " + chunkId);
                            await loader.UnloadAsync(chunk, cancellationToken);
                        }
                        plan = WorldResidencyPlanner.Plan(
                            snapshot,
                            worldId,
                            focusChunkId,
                            loader.LoadedChunkIds);
                    }
                }
                catch
                {
                    await RestorePreviousWorldOrHideAsync(
                        previousResidency,
                        previousWorldId);
                    throw;
                }
            }

            try
            {
                foreach (string chunkId in plan.LoadChunkIds)
                {
                    WorldChunkDefinition chunk = snapshot.GetChunk(chunkId);
                    await loader.LoadAsync(chunk, cancellationToken);
                }

                foreach (string chunkId in plan.UnloadChunkIds)
                {
                    await loader.UnloadAsync(snapshot.GetChunk(chunkId), cancellationToken);
                }

                await loader.SetSpatialVisibilityAsync(true, cancellationToken);
                ActiveWorldId = worldId;
                return plan;
            }
            catch
            {
                await RestorePreviousWorldOrHideAsync(
                    previousResidency,
                    previousWorldId);
                throw;
            }
        }

        private async Task RestorePreviousWorldOrHideAsync(
            IEnumerable<string> previousResidency,
            string previousWorldId)
        {
            ActiveWorldId = null;
            try
            {
                await loader.SetSpatialVisibilityAsync(false, CancellationToken.None);
            }
            catch
            {
                // The loader contract requires failed hide requests to remain hidden.
            }

            bool restored = await RestoreResidencyAsync(previousResidency);
            bool revealPreviousWorld = restored && previousWorldId != null;
            try
            {
                await loader.SetSpatialVisibilityAsync(
                    revealPreviousWorld,
                    CancellationToken.None);
                if (revealPreviousWorld)
                {
                    ActiveWorldId = previousWorldId;
                }
            }
            catch
            {
                ActiveWorldId = null;
                try
                {
                    await loader.SetSpatialVisibilityAsync(false, CancellationToken.None);
                }
                catch
                {
                    // The loader contract requires failed hide requests to remain hidden.
                }
                throw;
            }
        }

        private async Task<bool> RestoreResidencyAsync(IEnumerable<string> requiredChunkIds)
        {
            var required = new HashSet<string>(requiredChunkIds, StringComparer.Ordinal);
            foreach (string chunkId in loader.LoadedChunkIds
                         .Where(value => !required.Contains(value))
                         .OrderBy(value => value, StringComparer.Ordinal)
                         .ToArray())
            {
                try
                {
                    await loader.UnloadAsync(snapshot.GetChunk(chunkId), CancellationToken.None);
                }
                catch
                {
                    // Continue compensating other mutations before declaring safe-shell state.
                }
            }

            var loaded = new HashSet<string>(loader.LoadedChunkIds, StringComparer.Ordinal);
            foreach (string chunkId in required
                         .Where(value => !loaded.Contains(value))
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                try
                {
                    await loader.LoadAsync(snapshot.GetChunk(chunkId), CancellationToken.None);
                }
                catch
                {
                    // Exact-set verification below decides whether restoration succeeded.
                }
            }

            return new HashSet<string>(loader.LoadedChunkIds, StringComparer.Ordinal)
                .SetEquals(required);
        }
    }
}
