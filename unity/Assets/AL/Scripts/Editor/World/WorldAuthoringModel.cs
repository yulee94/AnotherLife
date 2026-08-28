using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;
using UnityEngine;

namespace AL.Editor.World
{
    public sealed class WorldAuthoringCatalogRead
    {
        internal WorldAuthoringCatalogRead(
            WorldStreamingSnapshot snapshot,
            IList<string> diagnostics)
        {
            Snapshot = snapshot;
            Diagnostics = Array.AsReadOnly(
                (diagnostics ?? Array.Empty<string>()).ToArray());
        }

        public WorldStreamingSnapshot Snapshot { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsAccepted => Snapshot != null && Diagnostics.Count == 0;
    }

    public static class WorldAuthoringCatalogProvider
    {
        public const string CatalogAssetPath =
            "Assets/AL/StreamingAssets/GameData/al_world_streaming_catalog.json";

        private static WorldAuthoringCatalogRead cachedRead;
        private static long cachedWriteTicks = long.MinValue;
        private static long cachedLength = long.MinValue;

        public static WorldAuthoringCatalogRead LoadCanonical(bool forceReload = false)
        {
            string absolutePath = AbsoluteCatalogPath();
            var file = new FileInfo(absolutePath);
            long writeTicks = file.Exists ? file.LastWriteTimeUtc.Ticks : long.MinValue;
            long length = file.Exists ? file.Length : long.MinValue;
            if (!forceReload &&
                cachedRead != null &&
                writeTicks == cachedWriteTicks &&
                length == cachedLength)
            {
                return cachedRead;
            }

            cachedWriteTicks = writeTicks;
            cachedLength = length;
            if (!file.Exists)
            {
                cachedRead = Rejected(
                    "AL-WORLD-AUTHORING-CATALOG-MISSING|" + CatalogAssetPath);
                return cachedRead;
            }

            try
            {
                WorldStreamingLoadResult result =
                    WorldStreamingCatalogLoader.Validate(File.ReadAllBytes(absolutePath));
                if (!result.IsAccepted)
                {
                    cachedRead = Rejected(
                        result.Diagnostics.Select(value => value.Fingerprint));
                    return cachedRead;
                }

                cachedRead = new WorldAuthoringCatalogRead(
                    result.Snapshot,
                    Array.Empty<string>());
                return cachedRead;
            }
            catch (Exception error)
            {
                cachedRead = Rejected(
                    "AL-WORLD-AUTHORING-CATALOG-READ-FAILED|" +
                    error.GetType().Name);
                return cachedRead;
            }
        }

        public static void Invalidate()
        {
            cachedRead = null;
            cachedWriteTicks = long.MinValue;
            cachedLength = long.MinValue;
        }

        private static string AbsoluteCatalogPath()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_streaming_catalog.json"));
        }

        private static WorldAuthoringCatalogRead Rejected(params string[] diagnostics)
        {
            return Rejected((IEnumerable<string>)diagnostics);
        }

        private static WorldAuthoringCatalogRead Rejected(
            IEnumerable<string> diagnostics)
        {
            return new WorldAuthoringCatalogRead(
                null,
                (diagnostics ?? Array.Empty<string>()).ToArray());
        }
    }

    public sealed class WorldAuthoringSelection
    {
        public WorldAuthoringSelection(
            string dimensionId,
            string worldId,
            string chunkId)
        {
            DimensionId = dimensionId ?? string.Empty;
            WorldId = worldId ?? string.Empty;
            ChunkId = chunkId ?? string.Empty;
        }

        public string DimensionId { get; }
        public string WorldId { get; }
        public string ChunkId { get; }
    }

    public sealed class WorldAuthoringChunkEnvelope
    {
        internal WorldAuthoringChunkEnvelope(
            WorldChunkDefinition chunk,
            WorldDimensionDefinition dimension,
            Bounds bounds)
        {
            Chunk = chunk;
            Dimension = dimension;
            Bounds = bounds;
        }

        public WorldChunkDefinition Chunk { get; }
        public WorldDimensionDefinition Dimension { get; }
        public Bounds Bounds { get; }
    }

    public sealed class WorldAuthoringSelectionContext
    {
        internal WorldAuthoringSelectionContext(
            WorldAuthoringSelection selection,
            WorldDimensionDefinition dimension,
            WorldInstanceDefinition world,
            WorldChunkDefinition focus,
            IList<WorldChunkDefinition> neighbors)
        {
            Selection = selection;
            Dimension = dimension;
            World = world;
            Focus = focus;
            Neighbors = Array.AsReadOnly(neighbors.ToArray());
            FocusAndNeighbors = Array.AsReadOnly(
                new[] { focus }.Concat(neighbors).ToArray());
        }

        public WorldAuthoringSelection Selection { get; }
        public WorldDimensionDefinition Dimension { get; }
        public WorldInstanceDefinition World { get; }
        public WorldChunkDefinition Focus { get; }
        public IReadOnlyList<WorldChunkDefinition> Neighbors { get; }
        public IReadOnlyList<WorldChunkDefinition> FocusAndNeighbors { get; }
    }

    public static class WorldAuthoringSelectionResolver
    {
        public static WorldAuthoringSelection Resolve(
            WorldStreamingSnapshot snapshot,
            string preferredDimensionId,
            string preferredWorldId,
            string preferredChunkId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (snapshot.Dimensions.Count == 0)
            {
                throw new InvalidOperationException(
                    "The world streaming catalog has no dimensions.");
            }

            WorldDimensionDefinition dimension =
                snapshot.GetDimension(preferredDimensionId) ?? snapshot.Dimensions[0];
            WorldInstanceDefinition world = snapshot.GetWorld(preferredWorldId);
            if (world == null ||
                !string.Equals(
                    world.DimensionId,
                    dimension.Id,
                    StringComparison.Ordinal))
            {
                world = dimension.WorldIds
                    .Select(snapshot.GetWorld)
                    .FirstOrDefault(value => value != null) ??
                    throw new InvalidOperationException(
                        "The selected dimension has no valid world instances.");
            }

            WorldChunkDefinition chunk = snapshot.GetChunk(preferredChunkId);
            if (chunk == null ||
                !string.Equals(chunk.WorldId, world.Id, StringComparison.Ordinal))
            {
                chunk = snapshot.GetChunk(world.SeedChunkId) ?? world.ChunkIds
                    .Select(snapshot.GetChunk)
                    .FirstOrDefault(value => value != null) ??
                    throw new InvalidOperationException(
                        "The selected world has no valid chunks.");
            }

            return new WorldAuthoringSelection(dimension.Id, world.Id, chunk.Id);
        }

        public static WorldAuthoringSelectionContext BuildContext(
            WorldStreamingSnapshot snapshot,
            WorldAuthoringSelection selection)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            WorldDimensionDefinition dimension =
                snapshot.GetDimension(selection.DimensionId) ??
                throw new ArgumentException(
                    "Unknown selected dimension.",
                    nameof(selection));
            WorldInstanceDefinition world = snapshot.GetWorld(selection.WorldId) ??
                throw new ArgumentException(
                    "Unknown selected world.",
                    nameof(selection));
            WorldChunkDefinition focus = snapshot.GetChunk(selection.ChunkId) ??
                throw new ArgumentException(
                    "Unknown selected chunk.",
                    nameof(selection));
            if (!string.Equals(world.DimensionId, dimension.Id, StringComparison.Ordinal) ||
                !string.Equals(focus.WorldId, world.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The selected dimension, world, and chunk do not share ownership.",
                    nameof(selection));
            }

            var neighbors = new List<WorldChunkDefinition>();
            foreach (string neighborId in focus.NeighborIds)
            {
                WorldChunkDefinition neighbor = snapshot.GetChunk(neighborId);
                if (neighbor != null &&
                    string.Equals(neighbor.WorldId, world.Id, StringComparison.Ordinal) &&
                    neighbors.All(value =>
                        !string.Equals(value.Id, neighbor.Id, StringComparison.Ordinal)))
                {
                    neighbors.Add(neighbor);
                }
            }

            return new WorldAuthoringSelectionContext(
                selection,
                dimension,
                world,
                focus,
                neighbors);
        }

        public static WorldAuthoringChunkEnvelope BuildEnvelope(
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

            WorldInstanceDefinition world = snapshot.GetWorld(chunk.WorldId) ??
                throw new ArgumentException("Unknown chunk world.", nameof(chunk));
            WorldDimensionDefinition dimension =
                snapshot.GetDimension(world.DimensionId) ??
                throw new ArgumentException("Unknown chunk dimension.", nameof(chunk));
            float span = dimension.ChunkSpanMeters;
            var center = new Vector3(chunk.GridX * span, 0f, chunk.GridZ * span);
            var size = new Vector3(span, Mathf.Max(2f, span * 0.005f), span);
            return new WorldAuthoringChunkEnvelope(
                chunk,
                dimension,
                new Bounds(center, size));
        }
    }
}
