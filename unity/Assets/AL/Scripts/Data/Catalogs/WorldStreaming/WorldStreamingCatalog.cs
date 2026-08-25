using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Data.Catalogs.WorldStreaming
{
    public static class WorldStreamingContract
    {
        public const string SupportedVersion = "0.1.0";
        public const string CatalogId = "al_world_streaming_catalog";
        public const string LayoutAuthority = "topology_only_provisional_coordinates";
        public const string CanonicalSpatialContractSha256 =
            "bcca7a8dd36d6fb48b8408b2451e7714b5053e0e93645919db0d8e48fa4034dd";
        public const int MaximumBytes = 128 * 1024;
        public const int MaximumDiagnostics = 128;
    }

    public enum WorldStreamingLoadStatus
    {
        Accepted,
        Rejected,
        UnsupportedVersion
    }

    public sealed class WorldStreamingDiagnostic
    {
        public WorldStreamingDiagnostic(string code, string path, string relatedId, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            RelatedId = relatedId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string RelatedId { get; }
        public string Message { get; }
        public string Fingerprint => string.Join("|", Code, Path, RelatedId, Message);
    }

    public sealed class WorldStreamingLoadResult
    {
        internal WorldStreamingLoadResult(
            WorldStreamingLoadStatus status,
            WorldStreamingSnapshot snapshot,
            IList<WorldStreamingDiagnostic> diagnostics)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostics = Array.AsReadOnly(
                (diagnostics ?? Array.Empty<WorldStreamingDiagnostic>()).ToArray());
        }

        public WorldStreamingLoadStatus Status { get; }
        public WorldStreamingSnapshot Snapshot { get; }
        public IReadOnlyList<WorldStreamingDiagnostic> Diagnostics { get; }
        public bool IsAccepted =>
            Status == WorldStreamingLoadStatus.Accepted && Snapshot != null;
    }

    public sealed class WorldDimensionDefinition
    {
        internal WorldDimensionDefinition(
            string id,
            string mode,
            float chunkSpanMeters,
            bool exclusive,
            IList<string> worldIds)
        {
            Id = id;
            Mode = mode;
            ChunkSpanMeters = chunkSpanMeters;
            Exclusive = exclusive;
            WorldIds = Frozen(worldIds);
        }

        public string Id { get; }
        public string Mode { get; }
        public float ChunkSpanMeters { get; }
        public bool Exclusive { get; }
        public IReadOnlyList<string> WorldIds { get; }

        private static IReadOnlyList<T> Frozen<T>(IList<T> values) =>
            Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
    }

    public sealed class WorldInstanceDefinition
    {
        internal WorldInstanceDefinition(
            string id,
            string dimensionId,
            string usage,
            string accessPolicy,
            string variantBindingStatus,
            string topologyNodeId,
            string seedChunkId,
            IList<string> chunkIds)
        {
            Id = id;
            DimensionId = dimensionId;
            Usage = usage;
            AccessPolicy = accessPolicy;
            VariantBindingStatus = variantBindingStatus;
            TopologyNodeId = topologyNodeId;
            SeedChunkId = seedChunkId;
            ChunkIds = Array.AsReadOnly(
                (chunkIds ?? Array.Empty<string>()).ToArray());
        }

        public string Id { get; }
        public string DimensionId { get; }
        public string Usage { get; }
        public string AccessPolicy { get; }
        public string VariantBindingStatus { get; }
        public string TopologyNodeId { get; }
        public string SeedChunkId { get; }
        public IReadOnlyList<string> ChunkIds { get; }
    }

    public sealed class WorldChunkDefinition
    {
        internal WorldChunkDefinition(
            string id,
            string worldId,
            string scenePath,
            string blockoutArchetype,
            int gridX,
            int gridZ,
            IList<string> neighborIds,
            IList<string> replacementSocketIds)
        {
            Id = id;
            WorldId = worldId;
            ScenePath = scenePath;
            BlockoutArchetype = blockoutArchetype;
            GridX = gridX;
            GridZ = gridZ;
            NeighborIds = Array.AsReadOnly(
                (neighborIds ?? Array.Empty<string>()).ToArray());
            ReplacementSocketIds = Array.AsReadOnly(
                (replacementSocketIds ?? Array.Empty<string>()).ToArray());
        }

        public string Id { get; }
        public string WorldId { get; }
        public string ScenePath { get; }
        public string BlockoutArchetype { get; }
        public int GridX { get; }
        public int GridZ { get; }
        public IReadOnlyList<string> NeighborIds { get; }
        public IReadOnlyList<string> ReplacementSocketIds { get; }
    }

    public sealed class WorldTraversalProfileDefinition
    {
        internal WorldTraversalProfileDefinition(
            string id,
            string dimensionId,
            string routeType,
            float referenceSpeedMetersPerSecond,
            int minimumSeconds,
            int maximumSeconds)
        {
            Id = id;
            DimensionId = dimensionId;
            RouteType = routeType;
            ReferenceSpeedMetersPerSecond = referenceSpeedMetersPerSecond;
            MinimumSeconds = minimumSeconds;
            MaximumSeconds = maximumSeconds;
        }

        public string Id { get; }
        public string DimensionId { get; }
        public string RouteType { get; }
        public float ReferenceSpeedMetersPerSecond { get; }
        public int MinimumSeconds { get; }
        public int MaximumSeconds { get; }
        public float MinimumDistanceMeters =>
            ReferenceSpeedMetersPerSecond * MinimumSeconds;
        public float MaximumDistanceMeters =>
            ReferenceSpeedMetersPerSecond * MaximumSeconds;
    }

    public sealed class WorldStreamingSnapshot
    {
        private readonly IReadOnlyDictionary<string, WorldDimensionDefinition> dimensionsById;
        private readonly IReadOnlyDictionary<string, WorldInstanceDefinition> worldsById;
        private readonly IReadOnlyDictionary<string, WorldChunkDefinition> chunksById;
        private readonly IReadOnlyDictionary<string, WorldTraversalProfileDefinition>
            traversalProfilesById;

        internal WorldStreamingSnapshot(
            string version,
            IList<WorldDimensionDefinition> dimensions,
            IList<WorldInstanceDefinition> worlds,
            IList<WorldChunkDefinition> chunks,
            IList<WorldTraversalProfileDefinition> traversalProfiles)
        {
            Version = version;
            Dimensions = Array.AsReadOnly(dimensions.ToArray());
            Worlds = Array.AsReadOnly(worlds.ToArray());
            Chunks = Array.AsReadOnly(chunks.ToArray());
            TraversalProfiles = Array.AsReadOnly(traversalProfiles.ToArray());
            dimensionsById = Index(Dimensions, value => value.Id);
            worldsById = Index(Worlds, value => value.Id);
            chunksById = Index(Chunks, value => value.Id);
            traversalProfilesById = Index(TraversalProfiles, value => value.Id);
        }

        public string Version { get; }
        public IReadOnlyList<WorldDimensionDefinition> Dimensions { get; }
        public IReadOnlyList<WorldInstanceDefinition> Worlds { get; }
        public IReadOnlyList<WorldChunkDefinition> Chunks { get; }
        public IReadOnlyList<WorldTraversalProfileDefinition> TraversalProfiles { get; }

        public WorldDimensionDefinition GetDimension(string id) =>
            Get(dimensionsById, id);

        public WorldInstanceDefinition GetWorld(string id) =>
            Get(worldsById, id);

        public WorldChunkDefinition GetChunk(string id) =>
            Get(chunksById, id);

        public WorldTraversalProfileDefinition GetTraversalProfile(string id) =>
            Get(traversalProfilesById, id);

        private static IReadOnlyDictionary<string, T> Index<T>(
            IEnumerable<T> values,
            Func<T, string> keySelector)
        {
            return new ReadOnlyDictionary<string, T>(
                values.ToDictionary(keySelector, StringComparer.Ordinal));
        }

        private static T Get<T>(IReadOnlyDictionary<string, T> values, string id)
            where T : class
        {
            return !string.IsNullOrWhiteSpace(id) && values.TryGetValue(id, out T value)
                ? value
                : null;
        }
    }

    public sealed class WorldStreamingCatalogQuery
    {
        private readonly WorldStreamingSnapshot snapshot;

        public WorldStreamingCatalogQuery(WorldStreamingSnapshot snapshot)
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public WorldDimensionDefinition GetDimension(string id) =>
            snapshot.GetDimension(id);

        public WorldInstanceDefinition GetWorld(string id) =>
            snapshot.GetWorld(id);

        public WorldChunkDefinition GetChunk(string id) =>
            snapshot.GetChunk(id);
    }

    public static class WorldStreamingCatalogLoader
    {
        public static WorldStreamingLoadResult Validate(byte[] bytes)
        {
            var diagnostics = new List<WorldStreamingDiagnostic>();
            StrictJsonObject root;
            try
            {
                root = StrictJsonDocument.Parse(
                    bytes,
                    WorldStreamingContract.MaximumBytes) as StrictJsonObject;
            }
            catch (StrictJsonException error)
            {
                return Reject(
                    WorldStreamingLoadStatus.Rejected,
                    diagnostics,
                    "AL-WORLD-SCHEMA-INVALID",
                    error.Path,
                    string.Empty,
                    error.Code);
            }
            catch (Exception)
            {
                return Reject(
                    WorldStreamingLoadStatus.Rejected,
                    diagnostics,
                    "AL-WORLD-SCHEMA-INVALID",
                    "$",
                    string.Empty,
                    "parse_failed");
            }

            if (root == null)
            {
                return Reject(
                    WorldStreamingLoadStatus.Rejected,
                    diagnostics,
                    "AL-WORLD-SCHEMA-INVALID",
                    "$",
                    string.Empty,
                    "root_not_object");
            }

            ValidateAllowedProperties(
                root,
                "$",
                new[]
                {
                    "version",
                    "catalogId",
                    "idFormat",
                    "layoutAuthority",
                    "dimensions",
                    "traversalProfiles"
                },
                diagnostics);

            string version = RequiredString(root, "version", "$", diagnostics);
            if (!string.Equals(
                    version,
                    WorldStreamingContract.SupportedVersion,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WorldStreamingLoadStatus.UnsupportedVersion,
                    diagnostics,
                    "AL-WORLD-VERSION-UNSUPPORTED",
                    "$.version",
                    version,
                    "unsupported version");
            }

            RequireEqual(
                root,
                "catalogId",
                WorldStreamingContract.CatalogId,
                "$",
                diagnostics);
            RequireEqual(root, "idFormat", "lowercase_snake_case", "$", diagnostics);
            RequireEqual(
                root,
                "layoutAuthority",
                WorldStreamingContract.LayoutAuthority,
                "$",
                diagnostics);

            var dimensions = new List<WorldDimensionDefinition>();
            var worlds = new List<WorldInstanceDefinition>();
            var chunks = new List<WorldChunkDefinition>();
            var traversalProfiles = new List<WorldTraversalProfileDefinition>();
            ParseDimensions(root, dimensions, worlds, chunks, diagnostics);
            ParseTraversalProfiles(root, traversalProfiles, diagnostics);
            ValidateReferences(dimensions, worlds, chunks, diagnostics);
            ValidateCanonicalDimensions(dimensions, diagnostics);
            ValidateTraversalProfiles(dimensions, traversalProfiles, diagnostics);
            ValidateAnotherLifePartition(dimensions, worlds, chunks, diagnostics);
            ValidateCanonicalSpatialContract(chunks, diagnostics);
            SortDiagnostics(diagnostics);

            if (diagnostics.Count != 0)
            {
                return new WorldStreamingLoadResult(
                    WorldStreamingLoadStatus.Rejected,
                    null,
                    diagnostics.Take(WorldStreamingContract.MaximumDiagnostics).ToArray());
            }

            return new WorldStreamingLoadResult(
                WorldStreamingLoadStatus.Accepted,
                new WorldStreamingSnapshot(
                    version,
                    dimensions,
                    worlds,
                    chunks,
                    traversalProfiles),
                diagnostics);
        }

        private static void ParseTraversalProfiles(
            StrictJsonObject root,
            List<WorldTraversalProfileDefinition> profiles,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            StrictJsonArray array = RequiredArray(
                root,
                "traversalProfiles",
                "$",
                diagnostics);
            ParseObjects(array, "$.traversalProfiles", diagnostics, (value, path) =>
            {
                ValidateAllowedProperties(
                    value,
                    path,
                    new[]
                    {
                        "id",
                        "dimensionId",
                        "routeType",
                        "referenceSpeedMetersPerSecond",
                        "minimumSeconds",
                        "maximumSeconds"
                    },
                    diagnostics);
                profiles.Add(new WorldTraversalProfileDefinition(
                    RequiredString(value, "id", path, diagnostics),
                    RequiredString(value, "dimensionId", path, diagnostics),
                    RequiredString(value, "routeType", path, diagnostics),
                    RequiredNumber(
                        value,
                        "referenceSpeedMetersPerSecond",
                        path,
                        diagnostics),
                    RequiredInteger(value, "minimumSeconds", path, diagnostics),
                    RequiredInteger(value, "maximumSeconds", path, diagnostics)));
            });
        }

        private static void ParseDimensions(
            StrictJsonObject root,
            List<WorldDimensionDefinition> dimensions,
            List<WorldInstanceDefinition> worlds,
            List<WorldChunkDefinition> chunks,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            StrictJsonArray array = RequiredArray(root, "dimensions", "$", diagnostics);
            ParseObjects(array, "$.dimensions", diagnostics, (dimensionObject, dimensionPath) =>
            {
                ValidateAllowedProperties(
                    dimensionObject,
                    dimensionPath,
                    new[] { "id", "mode", "chunkSpanMeters", "exclusive", "worlds" },
                    diagnostics);
                string dimensionId = RequiredString(
                    dimensionObject,
                    "id",
                    dimensionPath,
                    diagnostics);
                string mode = RequiredString(
                    dimensionObject,
                    "mode",
                    dimensionPath,
                    diagnostics);
                float chunkSpanMeters = RequiredNumber(
                    dimensionObject,
                    "chunkSpanMeters",
                    dimensionPath,
                    diagnostics);
                bool exclusive = RequiredBoolean(
                    dimensionObject,
                    "exclusive",
                    dimensionPath,
                    diagnostics);
                var worldIds = new List<string>();
                StrictJsonArray worldArray = RequiredArray(
                    dimensionObject,
                    "worlds",
                    dimensionPath,
                    diagnostics);
                ParseObjects(worldArray, dimensionPath + ".worlds", diagnostics, (worldObject, worldPath) =>
                {
                    ValidateAllowedProperties(
                        worldObject,
                        worldPath,
                        new[]
                        {
                            "id",
                            "usage",
                            "accessPolicy",
                            "variantBindingStatus",
                            "topologyNodeId",
                            "seedChunkId",
                            "chunks"
                        },
                        diagnostics);
                    string worldId = RequiredString(worldObject, "id", worldPath, diagnostics);
                    string usage = RequiredString(worldObject, "usage", worldPath, diagnostics);
                    string accessPolicy = RequiredString(
                        worldObject,
                        "accessPolicy",
                        worldPath,
                        diagnostics);
                    string binding = RequiredString(
                        worldObject,
                        "variantBindingStatus",
                        worldPath,
                        diagnostics);
                    string topologyNodeId = RequiredString(
                        worldObject,
                        "topologyNodeId",
                        worldPath,
                        diagnostics);
                    string seedChunkId = RequiredString(
                        worldObject,
                        "seedChunkId",
                        worldPath,
                        diagnostics);
                    var chunkIds = new List<string>();
                    StrictJsonArray chunkArray = RequiredArray(
                        worldObject,
                        "chunks",
                        worldPath,
                        diagnostics);
                    ParseObjects(chunkArray, worldPath + ".chunks", diagnostics, (chunkObject, chunkPath) =>
                    {
                        ValidateAllowedProperties(
                            chunkObject,
                            chunkPath,
                            new[]
                            {
                                "id",
                                "scenePath",
                                "blockoutArchetype",
                                "gridX",
                                "gridZ",
                                "neighbors",
                                "replacementSocketIds"
                            },
                            diagnostics);
                        string chunkId = RequiredString(chunkObject, "id", chunkPath, diagnostics);
                        chunkIds.Add(chunkId);
                        chunks.Add(new WorldChunkDefinition(
                            chunkId,
                            worldId,
                            RequiredString(chunkObject, "scenePath", chunkPath, diagnostics),
                            RequiredString(
                                chunkObject,
                                "blockoutArchetype",
                                chunkPath,
                                diagnostics),
                            RequiredInteger(chunkObject, "gridX", chunkPath, diagnostics),
                            RequiredInteger(chunkObject, "gridZ", chunkPath, diagnostics),
                            RequiredStrings(chunkObject, "neighbors", chunkPath, diagnostics),
                            RequiredStrings(
                                chunkObject,
                                "replacementSocketIds",
                                chunkPath,
                                diagnostics)));
                    });

                    worldIds.Add(worldId);
                    worlds.Add(new WorldInstanceDefinition(
                        worldId,
                        dimensionId,
                        usage,
                        accessPolicy,
                        binding,
                        topologyNodeId,
                        seedChunkId,
                        chunkIds));
                });

                dimensions.Add(new WorldDimensionDefinition(
                    dimensionId,
                    mode,
                    chunkSpanMeters,
                    exclusive,
                    worldIds));
            });
        }

        private static void ValidateReferences(
            IList<WorldDimensionDefinition> dimensions,
            IList<WorldInstanceDefinition> worlds,
            IList<WorldChunkDefinition> chunks,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            HashSet<string> dimensionIds = Unique(
                dimensions.Select(value => value.Id),
                "$.dimensions",
                diagnostics);
            HashSet<string> worldIds = Unique(
                worlds.Select(value => value.Id),
                "$.dimensions[].worlds",
                diagnostics);
            HashSet<string> chunkIds = Unique(
                chunks.Select(value => value.Id),
                "$.dimensions[].worlds[].chunks",
                diagnostics);
            var chunksById = chunks
                .Where(value => ValidId(value.Id))
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

            foreach (WorldDimensionDefinition dimension in dimensions)
            {
                if (!dimension.Exclusive)
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-DIMENSION-NOT-EXCLUSIVE",
                        "$.dimensions",
                        dimension.Id,
                        "spatial dimensions must be mutually exclusive");
                }

                if (dimension.ChunkSpanMeters <= 0f)
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-CHUNK-SPAN-INVALID",
                        "$.dimensions",
                        dimension.Id,
                        "chunk span must be positive");
                }

                foreach (string worldId in dimension.WorldIds)
                {
                    Reference(worldIds, worldId, "$.dimensions", dimension.Id, diagnostics);
                }
            }

            var scenePaths = new HashSet<string>(StringComparer.Ordinal);
            var socketIds = new HashSet<string>(StringComparer.Ordinal);
            var allowedArchetypes = new HashSet<string>(
                new[]
                {
                    "accordant_castle",
                    "accordant_descent",
                    "accordant_entrance",
                    "accordant_sealed_bridge",
                    "accordant_surface",
                    "accordant_wish_dragon_cavern",
                    "dragon_cave_descent",
                    "dragon_cave_entrance",
                    "dragon_cave_lair",
                    "kingdom_area",
                    "kingdom_castle",
                    "realm_area",
                    "realm_capital",
                    "realm_gate",
                    "warzone_bridge",
                    "warzone_crossroads",
                    "warzone_gate_approach",
                    "warzone_sector"
                },
                StringComparer.Ordinal);
            foreach (WorldInstanceDefinition world in worlds)
            {
                Reference(dimensionIds, world.DimensionId, "$.dimensions[].worlds", world.Id, diagnostics);
                Reference(chunkIds, world.SeedChunkId, "$.dimensions[].worlds", world.Id, diagnostics);
                if (chunksById.TryGetValue(world.SeedChunkId, out WorldChunkDefinition seed) &&
                    !string.Equals(seed.WorldId, world.Id, StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-SEED-CROSS-WORLD",
                        "$.dimensions[].worlds",
                        world.Id,
                        "seed chunk belongs to another world");
                }
            }

            foreach (WorldChunkDefinition chunk in chunks)
            {
                Reference(worldIds, chunk.WorldId, "$.dimensions[].worlds[].chunks", chunk.Id, diagnostics);
                if (!allowedArchetypes.Contains(chunk.BlockoutArchetype))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-BLOCKOUT-ARCHETYPE-INVALID",
                        "$.dimensions[].worlds[].chunks",
                        chunk.Id,
                        "blockout archetype is not supported by the deterministic generator");
                }
                if (string.IsNullOrWhiteSpace(chunk.ScenePath) ||
                    !chunk.ScenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !chunk.ScenePath.EndsWith(".unity", StringComparison.Ordinal) ||
                    !scenePaths.Add(chunk.ScenePath))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-SCENE-PATH-INVALID",
                        "$.dimensions[].worlds[].chunks",
                        chunk.Id,
                        "scene path must be unique and Assets-relative");
                }
                if (!chunk.ScenePath.StartsWith(
                        "Assets/AL/Worlds/Generated/",
                        StringComparison.Ordinal) ||
                    chunk.ScenePath.Contains("/../") ||
                    chunk.ScenePath.Contains("/./") ||
                    chunk.ScenePath.Contains("//") ||
                    chunk.ScenePath.Contains("\\"))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-SCENE-PATH-OUTSIDE-GENERATED-ROOT",
                        "$.dimensions[].worlds[].chunks",
                        chunk.Id,
                        "scene path must remain under Assets/AL/Worlds/Generated");
                }

                foreach (string neighborId in chunk.NeighborIds)
                {
                    Reference(chunkIds, neighborId, "$.dimensions[].worlds[].chunks", chunk.Id, diagnostics);
                    if (chunksById.TryGetValue(neighborId, out WorldChunkDefinition neighbor) &&
                        !string.Equals(neighbor.WorldId, chunk.WorldId, StringComparison.Ordinal))
                    {
                        Add(
                            diagnostics,
                            "AL-WORLD-NEIGHBOR-CROSS-WORLD",
                            "$.dimensions[].worlds[].chunks",
                            chunk.Id,
                            "additive neighbors must belong to one world instance");
                    }
                    else if (neighbor != null && !neighbor.NeighborIds.Contains(chunk.Id))
                    {
                        Add(
                            diagnostics,
                            "AL-WORLD-NEIGHBOR-ASYMMETRIC",
                            "$.dimensions[].worlds[].chunks",
                            chunk.Id,
                            "neighbor relationship must be symmetric");
                    }
                }

                foreach (string socketId in chunk.ReplacementSocketIds)
                {
                    if (!ValidId(socketId) || !socketIds.Add(socketId))
                    {
                        Add(
                            diagnostics,
                            "AL-WORLD-SOCKET-ID-INVALID",
                            "$.dimensions[].worlds[].chunks",
                            socketId,
                            "replacement socket IDs must be valid and globally unique");
                    }
                }
            }
        }

        private static void ValidateAnotherLifePartition(
            IList<WorldDimensionDefinition> dimensions,
            IList<WorldInstanceDefinition> worlds,
            IList<WorldChunkDefinition> chunks,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            var worldIndex = worlds
                .Where(value => ValidId(value.Id))
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
            var chunkIndex = chunks
                .Where(value => ValidId(value.Id))
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

            foreach (WorldChunkDefinition chunk in chunks)
            {
                string expectedArchetype = CanonicalArchetypeForChunk(chunk.Id);
                if (expectedArchetype == null ||
                    !string.Equals(
                        chunk.BlockoutArchetype,
                        expectedArchetype,
                        StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-CANONICAL-ARCHETYPE-INVALID",
                        "$.dimensions[].worlds[].chunks",
                        chunk.Id,
                        "canonical chunks must use their required generator archetype");
                }
            }

            string[] requiredWorldIds =
            {
                "world_adventure_outer_warzone",
                "world_adventure_stonehold_dragon_cave",
                "world_adventure_eldergrove_dragon_cave",
                "world_adventure_crownlands_dragon_cave",
                "world_adventure_umbral_dragon_cave",
                "world_kingdom_private",
                "world_event_accordant_isle"
            };
            foreach (string requiredWorldId in requiredWorldIds)
            {
                if (!worldIndex.ContainsKey(requiredWorldId))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-REQUIRED-PARTITION-MISSING",
                        "$.dimensions[].worlds",
                        requiredWorldId,
                        "required AnotherLife world partition is missing");
                }
            }

            ValidateCanonicalWorld(
                worldIndex,
                "world_adventure_outer_warzone",
                "dimension_adventure_3d",
                "shared_warzone",
                "warzone_gate",
                "shared",
                "outer_warzone_ring",
                "chunk_warzone_crossroads",
                diagnostics);

            var requiredWarzoneChunks = new List<string>
            {
                "chunk_warzone_crossroads"
            };
            for (int slot = 1; slot <= 4; slot++)
            {
                requiredWarzoneChunks.Add("chunk_warzone_sector_" + slot.ToString("00"));
                requiredWarzoneChunks.Add("chunk_warzone_gate_approach_" + slot.ToString("00"));
            }
            string[] ringPairs = { "01_02", "02_03", "03_04", "04_01" };
            foreach (string ringPair in ringPairs)
            {
                requiredWarzoneChunks.Add("chunk_warzone_bridge_" + ringPair + "_a");
                requiredWarzoneChunks.Add("chunk_warzone_bridge_" + ringPair + "_b");
            }
            ValidateExactWorldChunks(
                requiredWarzoneChunks,
                "world_adventure_outer_warzone",
                worldIndex,
                chunkIndex,
                diagnostics);

            var requiredCenterBridges = new List<string>();
            for (int slot = 1; slot <= 4; slot++)
            {
                requiredCenterBridges.Add(
                    "chunk_accordant_center_bridge_ring_slot_" + slot.ToString("00"));
            }
            ValidateRequiredChunks(
                requiredCenterBridges,
                "world_event_accordant_isle",
                chunkIndex,
                diagnostics);

            for (int slot = 1; slot <= 4; slot++)
            {
                string slotId = "ring_slot_" + slot.ToString("00");
                string worldId = "world_adventure_" + slotId + "_inner";
                if (!worldIndex.TryGetValue(worldId, out WorldInstanceDefinition world) ||
                    !string.Equals(world.TopologyNodeId, slotId, StringComparison.Ordinal) ||
                    !string.Equals(world.VariantBindingStatus, "unresolved", StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-REALM-PLACEMENT-INVALID",
                        "$.dimensions[].worlds",
                        worldId,
                        "ring-slot worlds must remain unresolved");
                }

                string suffix = slot.ToString("00");
                ValidateExactWorldChunks(
                    new[]
                    {
                        "chunk_ring_slot_" + suffix + "_capital_core",
                        "chunk_ring_slot_" + suffix + "_area_01",
                        "chunk_ring_slot_" + suffix + "_area_02",
                        "chunk_ring_slot_" + suffix + "_area_03",
                        "chunk_ring_slot_" + suffix + "_area_04",
                        "chunk_ring_slot_" + suffix + "_main_gate"
                    },
                    worldId,
                    worldIndex,
                    chunkIndex,
                    diagnostics);
                ValidateCanonicalWorld(
                    worldIndex,
                    worldId,
                    "dimension_adventure_3d",
                    "inner_realm",
                    "realm_members",
                    "unresolved",
                    slotId,
                    "chunk_ring_slot_" + suffix + "_capital_core",
                    diagnostics);
                string gateApproachId = "chunk_warzone_gate_approach_" + suffix;
                if (!chunkIndex.TryGetValue(
                        gateApproachId,
                        out WorldChunkDefinition gateApproach) ||
                    !gateApproach.ReplacementSocketIds.Contains(
                        "socket_ring_slot_" + suffix + "_controlled_transition") ||
                    !gateApproach.ReplacementSocketIds.Contains(
                        "socket_ring_slot_" + suffix + "_outer_wall") ||
                    !gateApproach.ReplacementSocketIds.Contains(
                        "socket_ring_slot_" + suffix + "_warzone_entry"))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-REQUIRED-PARTITION-MISSING",
                        "$.dimensions[].worlds[].chunks",
                        gateApproachId,
                        "gate approach must preserve transition, outer wall, and warzone entry");
                }
            }

            foreach (string realmId in new[]
                     {
                         "stonehold",
                         "eldergrove",
                         "crownlands",
                         "umbral"
                     })
            {
                string prefix = "chunk_" + realmId + "_dragon_cave_";
                ValidateExactWorldChunks(
                    new[] { prefix + "entrance", prefix + "descent", prefix + "lair" },
                    "world_adventure_" + realmId + "_dragon_cave",
                    worldIndex,
                    chunkIndex,
                    diagnostics);
                ValidateCanonicalWorld(
                    worldIndex,
                    "world_adventure_" + realmId + "_dragon_cave",
                    "dimension_adventure_3d",
                    "realm_dragon_cave",
                    "realm_members",
                    realmId,
                    "under_realm_capital",
                    prefix + "entrance",
                    diagnostics);
            }

            var kingdomChunks = new List<string> { "chunk_kingdom_castle_core" };
            for (int area = 1; area <= 12; area++)
            {
                kingdomChunks.Add("chunk_kingdom_area_" + area.ToString("00"));
            }
            ValidateExactWorldChunks(
                kingdomChunks,
                "world_kingdom_private",
                worldIndex,
                chunkIndex,
                diagnostics);
            ValidateCanonicalWorld(
                worldIndex,
                "world_kingdom_private",
                "dimension_kingdom_25d",
                "private_kingdom",
                "owner_only",
                "selected_realm",
                "inner_realm_only",
                "chunk_kingdom_castle_core",
                diagnostics);

            var accordantChunks = new List<string>
            {
                "chunk_accordant_surface",
                "chunk_accordant_castle",
                "chunk_accordant_cavern_descent",
                "chunk_accordant_wish_dragon_cavern"
            };
            for (int entrance = 1; entrance <= 4; entrance++)
            {
                string suffix = entrance.ToString("00");
                accordantChunks.Add("chunk_accordant_entrance_" + suffix);
                accordantChunks.Add("chunk_accordant_center_bridge_ring_slot_" + suffix);
            }
            ValidateExactWorldChunks(
                accordantChunks,
                "world_event_accordant_isle",
                worldIndex,
                chunkIndex,
                diagnostics);
            ValidateCanonicalWorld(
                worldIndex,
                "world_event_accordant_isle",
                "dimension_special_event_3d",
                "accordant_isle_event",
                "event_only",
                "shared",
                "center_slot",
                "chunk_accordant_surface",
                diagnostics);

            if (!worldIndex.TryGetValue("world_kingdom_private", out WorldInstanceDefinition kingdom) ||
                !string.Equals(kingdom.DimensionId, "dimension_kingdom_25d", StringComparison.Ordinal) ||
                !string.Equals(kingdom.AccessPolicy, "owner_only", StringComparison.Ordinal) ||
                kingdom.ChunkIds.Any(IsForbiddenKingdomId))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-KINGDOM-CONTAINMENT-INVALID",
                    "$.dimensions[].worlds",
                    "world_kingdom_private",
                    "private kingdom must remain owner-only and inner-only");
            }

            if (!worldIndex.TryGetValue("world_event_accordant_isle", out WorldInstanceDefinition accordant) ||
                !string.Equals(accordant.DimensionId, "dimension_special_event_3d", StringComparison.Ordinal) ||
                !string.Equals(accordant.AccessPolicy, "event_only", StringComparison.Ordinal) ||
                !string.Equals(accordant.TopologyNodeId, "center_slot", StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-ACCORDANT-CONTAINMENT-INVALID",
                    "$.dimensions[].worlds",
                    "world_event_accordant_isle",
                    "Accordant Isle must remain event-only and dimension-isolated");
            }
        }

        private static void ValidateCanonicalSpatialContract(
            IEnumerable<WorldChunkDefinition> chunks,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            var projection = new StringBuilder();
            foreach (WorldChunkDefinition chunk in chunks
                         .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                AppendSpatialContractPart(projection, chunk.Id);
                AppendSpatialContractPart(projection, chunk.WorldId);
                AppendSpatialContractPart(projection, chunk.ScenePath);
                AppendSpatialContractPart(projection, chunk.BlockoutArchetype);
                AppendSpatialContractPart(
                    projection,
                    chunk.GridX.ToString(System.Globalization.CultureInfo.InvariantCulture));
                AppendSpatialContractPart(
                    projection,
                    chunk.GridZ.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (string neighborId in chunk.NeighborIds
                             .OrderBy(value => value, StringComparer.Ordinal))
                {
                    AppendSpatialContractPart(projection, neighborId);
                }
                AppendSpatialContractPart(projection, "#");
                foreach (string socketId in chunk.ReplacementSocketIds
                             .OrderBy(value => value, StringComparer.Ordinal))
                {
                    AppendSpatialContractPart(projection, socketId);
                }
                AppendSpatialContractPart(projection, "#");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(projection.ToString());
            string signature;
            using (SHA256 sha256 = SHA256.Create())
            {
                signature = BitConverter.ToString(sha256.ComputeHash(bytes))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
            if (!string.Equals(
                    signature,
                    WorldStreamingContract.CanonicalSpatialContractSha256,
                    StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-CANONICAL-TOPOLOGY-INVALID",
                    "$.dimensions[].worlds[].chunks",
                    signature,
                    "canonical scene paths, coordinates, neighbors, and replacement sockets are required");
            }
        }

        private static void AppendSpatialContractPart(StringBuilder builder, string value)
        {
            string safeValue = value ?? string.Empty;
            builder.Append(safeValue.Length)
                .Append(':')
                .Append(safeValue)
                .Append('|');
        }

        private static void ValidateCanonicalDimensions(
            IList<WorldDimensionDefinition> dimensions,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            ValidateCanonicalDimension(
                dimensions,
                "dimension_adventure_3d",
                "adventure_3d",
                1200f,
                new[]
                {
                    "world_adventure_ring_slot_01_inner",
                    "world_adventure_ring_slot_02_inner",
                    "world_adventure_ring_slot_03_inner",
                    "world_adventure_ring_slot_04_inner",
                    "world_adventure_outer_warzone",
                    "world_adventure_stonehold_dragon_cave",
                    "world_adventure_eldergrove_dragon_cave",
                    "world_adventure_crownlands_dragon_cave",
                    "world_adventure_umbral_dragon_cave"
                },
                diagnostics);
            ValidateCanonicalDimension(
                dimensions,
                "dimension_kingdom_25d",
                "kingdom_25d",
                128f,
                new[] { "world_kingdom_private" },
                diagnostics);
            ValidateCanonicalDimension(
                dimensions,
                "dimension_special_event_3d",
                "special_event_3d",
                800f,
                new[] { "world_event_accordant_isle" },
                diagnostics);

            var canonicalIds = new HashSet<string>(
                new[]
                {
                    "dimension_adventure_3d",
                    "dimension_kingdom_25d",
                    "dimension_special_event_3d"
                },
                StringComparer.Ordinal);
            foreach (WorldDimensionDefinition dimension in dimensions)
            {
                if (!canonicalIds.Contains(dimension.Id))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-CANONICAL-DIMENSION-INVALID",
                        "$.dimensions",
                        dimension.Id,
                        "only the three canonical purpose-specific dimensions are allowed");
                }
            }
        }

        private static string CanonicalArchetypeForChunk(string chunkId)
        {
            if (chunkId.StartsWith("chunk_ring_slot_", StringComparison.Ordinal))
            {
                if (chunkId.EndsWith("_capital_core", StringComparison.Ordinal)) return "realm_capital";
                if (chunkId.Contains("_area_")) return "realm_area";
                if (chunkId.EndsWith("_main_gate", StringComparison.Ordinal)) return "realm_gate";
            }
            if (chunkId.StartsWith("chunk_warzone_", StringComparison.Ordinal))
            {
                if (chunkId.Contains("_bridge_")) return "warzone_bridge";
                if (chunkId.Contains("_sector_")) return "warzone_sector";
                if (chunkId.Contains("_gate_approach_")) return "warzone_gate_approach";
                if (chunkId.EndsWith("_crossroads", StringComparison.Ordinal)) return "warzone_crossroads";
            }
            if (chunkId.Contains("_dragon_cave_"))
            {
                if (chunkId.EndsWith("_entrance", StringComparison.Ordinal)) return "dragon_cave_entrance";
                if (chunkId.EndsWith("_descent", StringComparison.Ordinal)) return "dragon_cave_descent";
                if (chunkId.EndsWith("_lair", StringComparison.Ordinal)) return "dragon_cave_lair";
            }
            if (chunkId.StartsWith("chunk_kingdom_", StringComparison.Ordinal))
            {
                return chunkId.EndsWith("_castle_core", StringComparison.Ordinal)
                    ? "kingdom_castle"
                    : "kingdom_area";
            }
            if (chunkId.StartsWith("chunk_accordant_center_bridge_", StringComparison.Ordinal))
                return "accordant_sealed_bridge";
            if (chunkId.StartsWith("chunk_accordant_entrance_", StringComparison.Ordinal))
                return "accordant_entrance";
            if (chunkId == "chunk_accordant_surface") return "accordant_surface";
            if (chunkId == "chunk_accordant_castle") return "accordant_castle";
            if (chunkId == "chunk_accordant_cavern_descent") return "accordant_descent";
            if (chunkId == "chunk_accordant_wish_dragon_cavern")
                return "accordant_wish_dragon_cavern";
            return null;
        }

        private static void ValidateCanonicalDimension(
            IEnumerable<WorldDimensionDefinition> dimensions,
            string id,
            string mode,
            float spanMeters,
            IEnumerable<string> worldIds,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            WorldDimensionDefinition dimension = dimensions.FirstOrDefault(
                value => string.Equals(value.Id, id, StringComparison.Ordinal));
            string[] expectedWorldIds = worldIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (dimension == null ||
                !string.Equals(dimension.Mode, mode, StringComparison.Ordinal) ||
                Math.Abs(dimension.ChunkSpanMeters - spanMeters) > 0.001f ||
                !dimension.Exclusive ||
                !dimension.WorldIds.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expectedWorldIds, StringComparer.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-CANONICAL-DIMENSION-INVALID",
                    "$.dimensions",
                    id,
                    "canonical dimension mode, span, exclusivity, and world placement are required");
            }
        }

        private static void ValidateRequiredChunks(
            IEnumerable<string> requiredChunkIds,
            string expectedWorldId,
            IDictionary<string, WorldChunkDefinition> chunkIndex,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            foreach (string requiredChunkId in requiredChunkIds)
            {
                if (!chunkIndex.TryGetValue(requiredChunkId, out WorldChunkDefinition chunk) ||
                    !string.Equals(chunk.WorldId, expectedWorldId, StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-REQUIRED-PARTITION-MISSING",
                        "$.dimensions[].worlds[].chunks",
                        requiredChunkId,
                        "required AnotherLife chunk is missing or belongs to the wrong world");
                }
            }
        }

        private static void ValidateExactWorldChunks(
            IEnumerable<string> requiredChunkIds,
            string expectedWorldId,
            IDictionary<string, WorldInstanceDefinition> worldIndex,
            IDictionary<string, WorldChunkDefinition> chunkIndex,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            string[] required = requiredChunkIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            ValidateRequiredChunks(required, expectedWorldId, chunkIndex, diagnostics);
            if (!worldIndex.TryGetValue(expectedWorldId, out WorldInstanceDefinition world) ||
                !world.ChunkIds.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(required, StringComparer.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-REQUIRED-PARTITION-MISSING",
                    "$.dimensions[].worlds[].chunks",
                    expectedWorldId,
                    "world must contain exactly its canonical chunk inventory");
            }
        }

        private static void ValidateCanonicalWorld(
            IDictionary<string, WorldInstanceDefinition> worldIndex,
            string id,
            string dimensionId,
            string usage,
            string accessPolicy,
            string variantBindingStatus,
            string topologyNodeId,
            string seedChunkId,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (!worldIndex.TryGetValue(id, out WorldInstanceDefinition world) ||
                !string.Equals(world.DimensionId, dimensionId, StringComparison.Ordinal) ||
                !string.Equals(world.Usage, usage, StringComparison.Ordinal) ||
                !string.Equals(world.AccessPolicy, accessPolicy, StringComparison.Ordinal) ||
                !string.Equals(
                    world.VariantBindingStatus,
                    variantBindingStatus,
                    StringComparison.Ordinal) ||
                !string.Equals(world.TopologyNodeId, topologyNodeId, StringComparison.Ordinal) ||
                !string.Equals(world.SeedChunkId, seedChunkId, StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-CANONICAL-WORLD-INVALID",
                    "$.dimensions[].worlds",
                    id,
                    "canonical world ownership, usage, access, binding, topology, and seed are required");
            }
        }

        private static void ValidateTraversalProfiles(
            IList<WorldDimensionDefinition> dimensions,
            IList<WorldTraversalProfileDefinition> profiles,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            var dimensionIds = new HashSet<string>(
                dimensions.Select(value => value.Id),
                StringComparer.Ordinal);
            Unique(
                profiles.Select(value => value.Id),
                "$.traversalProfiles",
                diagnostics);
            foreach (WorldTraversalProfileDefinition profile in profiles)
            {
                Reference(
                    dimensionIds,
                    profile.DimensionId,
                    "$.traversalProfiles",
                    profile.Id,
                    diagnostics);
                if (profile.ReferenceSpeedMetersPerSecond <= 0f ||
                    profile.MinimumSeconds <= 0 ||
                    profile.MaximumSeconds < profile.MinimumSeconds)
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-TRAVERSAL-PROFILE-INVALID",
                        "$.traversalProfiles",
                        profile.Id,
                        "speed and ordered positive duration range are required");
                }
            }

            ValidateCanonicalTraversalProfile(
                profiles,
                "traversal_gate_to_nearest_warzone_fortress",
                "main_gate_to_nearest_warzone_fortress",
                600,
                600,
                diagnostics);
            ValidateCanonicalTraversalProfile(
                profiles,
                "traversal_gate_to_nearest_adjacent_bridge_crossing",
                "main_gate_to_nearest_adjacent_bridge_crossing",
                900,
                900,
                diagnostics);
            ValidateCanonicalTraversalProfile(
                profiles,
                "traversal_gate_to_nearest_opposing_warzone_fortress",
                "main_gate_to_nearest_opposing_warzone_fortress",
                1200,
                1500,
                diagnostics);

            var canonicalIds = new HashSet<string>(
                new[]
                {
                    "traversal_gate_to_nearest_warzone_fortress",
                    "traversal_gate_to_nearest_adjacent_bridge_crossing",
                    "traversal_gate_to_nearest_opposing_warzone_fortress"
                },
                StringComparer.Ordinal);
            foreach (WorldTraversalProfileDefinition profile in profiles)
            {
                if (!canonicalIds.Contains(profile.Id))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-CANONICAL-TRAVERSAL-INVALID",
                        "$.traversalProfiles",
                        profile.Id,
                        "only the three adventure traversal authorities are allowed");
                }
            }
        }

        private static void ValidateCanonicalTraversalProfile(
            IEnumerable<WorldTraversalProfileDefinition> profiles,
            string id,
            string routeType,
            int minimumSeconds,
            int maximumSeconds,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            WorldTraversalProfileDefinition profile = profiles.FirstOrDefault(
                value => string.Equals(value.Id, id, StringComparison.Ordinal));
            if (profile == null ||
                !string.Equals(
                    profile.DimensionId,
                    "dimension_adventure_3d",
                    StringComparison.Ordinal) ||
                !string.Equals(profile.RouteType, routeType, StringComparison.Ordinal) ||
                Math.Abs(profile.ReferenceSpeedMetersPerSecond - 6f) > 0.001f ||
                profile.MinimumSeconds != minimumSeconds ||
                profile.MaximumSeconds != maximumSeconds)
            {
                Add(
                    diagnostics,
                    "AL-WORLD-CANONICAL-TRAVERSAL-INVALID",
                    "$.traversalProfiles",
                    id,
                    "canonical adventure speed and traversal-time budget are required");
            }
        }

        private static bool IsForbiddenKingdomId(string value)
        {
            return value.Contains("warzone") ||
                   value.Contains("bridge") ||
                   value.Contains("accordant") ||
                   value.Contains("outer");
        }

        private static void ValidateAllowedProperties(
            StrictJsonObject owner,
            string path,
            IEnumerable<string> allowedNames,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (owner == null)
            {
                return;
            }

            var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (StrictJsonProperty property in owner.Properties)
            {
                if (!allowed.Contains(property.Name))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-SCHEMA-UNKNOWN-PROPERTY",
                        path + "." + property.Name,
                        property.Name,
                        "property is not part of the world streaming schema");
                }
            }
        }

        private static StrictJsonArray RequiredArray(
            StrictJsonObject owner,
            string name,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonArray array)
            {
                return array;
            }

            Add(
                diagnostics,
                "AL-WORLD-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "array required");
            return null;
        }

        private static string RequiredString(
            StrictJsonObject owner,
            string name,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonString text &&
                !string.IsNullOrWhiteSpace(text.Value))
            {
                return text.Value;
            }

            Add(
                diagnostics,
                "AL-WORLD-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "nonblank string required");
            return string.Empty;
        }

        private static bool RequiredBoolean(
            StrictJsonObject owner,
            string name,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonBoolean boolean)
            {
                return boolean.Value;
            }

            Add(
                diagnostics,
                "AL-WORLD-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "boolean required");
            return false;
        }

        private static int RequiredInteger(
            StrictJsonObject owner,
            string name,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonNumber number &&
                number.HasFiniteDoubleValue &&
                Math.Abs(number.Value - Math.Round(number.Value)) < 0.000001d &&
                number.Value >= int.MinValue &&
                number.Value <= int.MaxValue)
            {
                return (int)number.Value;
            }

            Add(
                diagnostics,
                "AL-WORLD-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "integer required");
            return 0;
        }

        private static float RequiredNumber(
            StrictJsonObject owner,
            string name,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonNumber number &&
                number.HasFiniteDoubleValue &&
                number.Value >= -float.MaxValue &&
                number.Value <= float.MaxValue)
            {
                return (float)number.Value;
            }

            Add(
                diagnostics,
                "AL-WORLD-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "finite number required");
            return 0f;
        }

        private static List<string> RequiredStrings(
            StrictJsonObject owner,
            string name,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            StrictJsonArray array = RequiredArray(owner, name, path, diagnostics);
            var values = new List<string>();
            if (array == null)
            {
                return values;
            }

            for (int index = 0; index < array.Items.Count; index++)
            {
                if (array.Items[index] is StrictJsonString text &&
                    !string.IsNullOrWhiteSpace(text.Value))
                {
                    values.Add(text.Value);
                }
                else
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-SCHEMA-INVALID",
                        path + "." + name + "[" + index + "]",
                        string.Empty,
                        "nonblank string required");
                }
            }

            return values;
        }

        private static void RequireEqual(
            StrictJsonObject owner,
            string name,
            string expected,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            string actual = RequiredString(owner, name, path, diagnostics);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-SOURCE-MISMATCH",
                    path + "." + name,
                    actual,
                    "authority identity mismatch");
            }
        }

        private static void ParseObjects(
            StrictJsonArray array,
            string path,
            List<WorldStreamingDiagnostic> diagnostics,
            Action<StrictJsonObject, string> action)
        {
            if (array == null)
            {
                return;
            }

            for (int index = 0; index < array.Items.Count; index++)
            {
                if (array.Items[index] is StrictJsonObject value)
                {
                    action(value, path + "[" + index + "]");
                }
                else
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-SCHEMA-INVALID",
                        path + "[" + index + "]",
                        string.Empty,
                        "object required");
                }
            }
        }

        private static HashSet<string> Unique(
            IEnumerable<string> ids,
            string path,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (!ValidId(id) || !values.Add(id))
                {
                    Add(
                        diagnostics,
                        "AL-WORLD-ID-INVALID",
                        path,
                        id,
                        "ID must be unique lowercase snake case");
                }
            }

            return values;
        }

        private static bool ValidId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool underscore = false;
            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_';
                if (!valid || (character == '_' && underscore))
                {
                    return false;
                }

                underscore = character == '_';
            }

            return !underscore;
        }

        private static void Reference(
            ISet<string> ids,
            string id,
            string path,
            string relatedId,
            List<WorldStreamingDiagnostic> diagnostics)
        {
            if (!ids.Contains(id))
            {
                Add(
                    diagnostics,
                    "AL-WORLD-REFERENCE-MISSING",
                    path,
                    relatedId,
                    "missing reference: " + id);
            }
        }

        private static void Add(
            List<WorldStreamingDiagnostic> diagnostics,
            string code,
            string path,
            string relatedId,
            string message)
        {
            diagnostics.Add(new WorldStreamingDiagnostic(code, path, relatedId, message));
        }

        private static void SortDiagnostics(List<WorldStreamingDiagnostic> diagnostics)
        {
            diagnostics.Sort((left, right) =>
            {
                int comparison = string.CompareOrdinal(left.Code, right.Code);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = string.CompareOrdinal(left.Path, right.Path);
                return comparison != 0
                    ? comparison
                    : string.CompareOrdinal(left.RelatedId, right.RelatedId);
            });
        }

        private static WorldStreamingLoadResult Reject(
            WorldStreamingLoadStatus status,
            List<WorldStreamingDiagnostic> diagnostics,
            string code,
            string path,
            string relatedId,
            string message)
        {
            Add(diagnostics, code, path, relatedId, message);
            SortDiagnostics(diagnostics);
            return new WorldStreamingLoadResult(
                status,
                null,
                diagnostics.Take(WorldStreamingContract.MaximumDiagnostics).ToArray());
        }
    }
}
