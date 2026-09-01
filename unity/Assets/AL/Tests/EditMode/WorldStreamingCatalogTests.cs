using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.Data.Catalogs.WorldStreaming;
using AL.World.Streaming;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class WorldStreamingCatalogTests
    {
        private static byte[] CanonicalBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_streaming_catalog.json"));
        }

        [Test]
        public void CanonicalCatalogDefinesExclusiveDimensionsWorldsAndChunks()
        {
            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(CanonicalBytes());

            Assert.That(
                result.Status,
                Is.EqualTo(WorldStreamingLoadStatus.Accepted),
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(result.Snapshot.Version, Is.EqualTo("0.1.0"));
            Assert.That(result.Snapshot.Dimensions, Has.Count.EqualTo(3));
            Assert.That(result.Snapshot.Worlds, Has.Count.EqualTo(11));
            Assert.That(result.Snapshot.Chunks, Has.Count.EqualTo(78));
            Assert.That(result.Snapshot.Dimensions.All(value => value.Exclusive), Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void AdventurePartitionRetainsTopologyWithoutInventingRealmPlacement()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var query = new WorldStreamingCatalogQuery(snapshot);
            WorldDimensionDefinition adventure = query.GetDimension("dimension_adventure_3d");

            Assert.That(adventure, Is.Not.Null);
            Assert.That(adventure.WorldIds, Has.Count.EqualTo(9));
            for (int slot = 1; slot <= 4; slot++)
            {
                string slotId = $"ring_slot_{slot:00}";
                WorldInstanceDefinition world = query.GetWorld($"world_adventure_{slotId}_inner");
                Assert.That(world, Is.Not.Null, slotId);
                Assert.That(world.TopologyNodeId, Is.EqualTo(slotId));
                Assert.That(world.VariantBindingStatus, Is.EqualTo("unresolved"));
                Assert.That(world.ChunkIds, Has.Count.EqualTo(6));
            }

            WorldInstanceDefinition warzone = query.GetWorld("world_adventure_outer_warzone");
            Assert.That(warzone.ChunkIds, Has.Count.EqualTo(17));
            Assert.That(
                warzone.ChunkIds.Count(id => id.Contains("bridge_", StringComparison.Ordinal)),
                Is.EqualTo(8));
            Assert.That(
                warzone.ChunkIds.Count(id => id.Contains("gate_approach_", StringComparison.Ordinal)),
                Is.EqualTo(4));
        }


        [Test]
        public void PrivateKingdomIsOwnerOnlyAndContainsOnlyInnerAreas()
        {
            var query = new WorldStreamingCatalogQuery(AcceptedSnapshot());
            WorldInstanceDefinition kingdom = query.GetWorld("world_kingdom_private");

            Assert.That(kingdom.DimensionId, Is.EqualTo("dimension_kingdom_25d"));
            Assert.That(kingdom.AccessPolicy, Is.EqualTo("owner_only"));
            Assert.That(kingdom.VariantBindingStatus, Is.EqualTo("selected_realm"));
            Assert.That(kingdom.ChunkIds, Has.Count.EqualTo(13));
            Assert.That(kingdom.ChunkIds, Does.Contain("chunk_kingdom_castle_core"));
            Assert.That(
                kingdom.ChunkIds.Count(id => id.StartsWith("chunk_kingdom_area_", StringComparison.Ordinal)),
                Is.EqualTo(12));
            Assert.That(
                kingdom.ChunkIds.Any(id => id.Contains("warzone", StringComparison.Ordinal) ||
                                           id.Contains("bridge", StringComparison.Ordinal) ||
                                           id.Contains("accordant", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void AccordantIsleIsIsolatedInEventDimension()
        {
            var query = new WorldStreamingCatalogQuery(AcceptedSnapshot());
            WorldInstanceDefinition accordant = query.GetWorld("world_event_accordant_isle");

            Assert.That(accordant.DimensionId, Is.EqualTo("dimension_special_event_3d"));
            Assert.That(accordant.AccessPolicy, Is.EqualTo("event_only"));
            Assert.That(accordant.TopologyNodeId, Is.EqualTo("center_slot"));
            Assert.That(accordant.ChunkIds, Has.Count.EqualTo(12));
            Assert.That(accordant.ChunkIds, Does.Contain("chunk_accordant_wish_dragon_cavern"));
        }

        [Test]
        public void AdventureTraversalProfilesPreserveUserTimingAtChampionRunSpeed()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();

            AssertTraversal(
                snapshot,
                "traversal_gate_to_nearest_warzone_fortress",
                600,
                600,
                3600f,
                3600f);
            AssertTraversal(
                snapshot,
                "traversal_gate_to_nearest_adjacent_bridge_crossing",
                900,
                900,
                5400f,
                5400f);
            AssertTraversal(
                snapshot,
                "traversal_gate_to_nearest_opposing_warzone_fortress",
                1200,
                1500,
                7200f,
                9000f);
        }

        [Test]
        public void AccordantWorldContainsFourDistinctSealedCenterBridgeHooks()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldInstanceDefinition accordant = snapshot.GetWorld("world_event_accordant_isle");

            WorldChunkDefinition[] bridges = accordant.ChunkIds
                .Select(snapshot.GetChunk)
                .Where(value => value.BlockoutArchetype == "accordant_sealed_bridge")
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "chunk_accordant_center_bridge_ring_slot_01",
                    "chunk_accordant_center_bridge_ring_slot_02",
                    "chunk_accordant_center_bridge_ring_slot_03",
                    "chunk_accordant_center_bridge_ring_slot_04"
                },
                bridges.Select(value => value.Id).ToArray());
            Assert.That(
                bridges.All(value => value.NeighborIds.Contains("chunk_accordant_surface")),
                Is.True);
            Assert.That(
                bridges.SelectMany(value => value.ReplacementSocketIds).Distinct().Count(),
                Is.EqualTo(4));
        }

        [Test]
        public void GateAndWarzoneApproachSocketsPreserveTheFullBoundarySequence()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            for (int slot = 1; slot <= 4; slot++)
            {
                string suffix = slot.ToString("00");
                WorldChunkDefinition innerGate = snapshot.GetChunk(
                    "chunk_ring_slot_" + suffix + "_main_gate");
                WorldChunkDefinition approach = snapshot.GetChunk(
                    "chunk_warzone_gate_approach_" + suffix);

                Assert.That(
                    innerGate.ReplacementSocketIds,
                    Does.Contain("socket_ring_slot_" + suffix + "_inner_wall"));
                Assert.That(
                    innerGate.ReplacementSocketIds,
                    Does.Contain("socket_ring_slot_" + suffix + "_main_gate"));
                Assert.That(
                    approach.ReplacementSocketIds,
                    Does.Contain("socket_ring_slot_" + suffix + "_controlled_transition"));
                Assert.That(
                    approach.ReplacementSocketIds,
                    Does.Contain("socket_ring_slot_" + suffix + "_outer_wall"));
                Assert.That(
                    approach.ReplacementSocketIds,
                    Does.Contain("socket_ring_slot_" + suffix + "_warzone_entry"));
            }
        }

        [Test]
        public void DimensionsDeclarePurposeAppropriateChunkSpans()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();

            Assert.That(
                snapshot.GetDimension("dimension_adventure_3d").ChunkSpanMeters,
                Is.EqualTo(1200f));
            Assert.That(
                snapshot.GetDimension("dimension_kingdom_25d").ChunkSpanMeters,
                Is.EqualTo(128f));
            Assert.That(
                snapshot.GetDimension("dimension_special_event_3d").ChunkSpanMeters,
                Is.EqualTo(800f));
        }

        [Test]
        public void ResidencyPlanLoadsOnlyFocusAndNeighborsFromActiveWorld()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();

            WorldResidencyPlan plan = WorldResidencyPlanner.Plan(
                snapshot,
                "world_adventure_ring_slot_01_inner",
                "chunk_ring_slot_01_capital_core",
                new[]
                {
                    "chunk_ring_slot_01_area_01",
                    "chunk_warzone_crossroads",
                    "chunk_kingdom_castle_core"
                });

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "chunk_ring_slot_01_capital_core",
                    "chunk_ring_slot_01_area_01",
                    "chunk_ring_slot_01_area_02",
                    "chunk_ring_slot_01_area_03",
                    "chunk_ring_slot_01_area_04"
                },
                plan.RequiredChunkIds);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "chunk_ring_slot_01_capital_core",
                    "chunk_ring_slot_01_area_02",
                    "chunk_ring_slot_01_area_03",
                    "chunk_ring_slot_01_area_04"
                },
                plan.LoadChunkIds);
            CollectionAssert.AreEquivalent(
                new[] { "chunk_warzone_crossroads", "chunk_kingdom_castle_core" },
                plan.UnloadChunkIds);
            Assert.That(
                plan.RequiredChunkIds.All(id => snapshot.GetChunk(id).WorldId == plan.WorldId),
                Is.True);
        }

        [Test]
        public void UnknownOrCrossWorldNeighborRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"chunk_ring_slot_01_area_01\"",
                "\"chunk_warzone_crossroads\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(
                result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-NEIGHBOR-CROSS-WORLD"));
        }

        [Test]
        public void ScenePathOutsideGeneratedWorldRootRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "Assets/AL/Worlds/Generated/Adventure3D/world_adventure_ring_slot_01_inner/chunk_ring_slot_01_capital_core.unity",
                "../../outside-project.unity",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(
                result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-SCENE-PATH-OUTSIDE-GENERATED-ROOT"));
        }

        [Test]
        public void UnknownRootPropertyRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"version\": \"0.1.0\"",
                "\"unknownRoot\": true,\n  \"version\": \"0.1.0\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-SCHEMA-UNKNOWN-PROPERTY"));
        }

        [Test]
        public void UnknownDimensionPropertyRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"id\": \"dimension_adventure_3d\"",
                "\"unknownDimension\": true, \"id\": \"dimension_adventure_3d\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-SCHEMA-UNKNOWN-PROPERTY"));
        }

        [Test]
        public void UnknownWorldPropertyRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"id\": \"world_adventure_ring_slot_01_inner\"",
                "\"unknownWorld\": true, \"id\": \"world_adventure_ring_slot_01_inner\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-SCHEMA-UNKNOWN-PROPERTY"));
        }

        [Test]
        public void UnknownChunkPropertyRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"id\": \"chunk_ring_slot_01_capital_core\"",
                "\"unknownChunk\": true, \"id\": \"chunk_ring_slot_01_capital_core\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-SCHEMA-UNKNOWN-PROPERTY"));
        }

        [Test]
        public void UnknownTraversalProfilePropertyRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"id\": \"traversal_gate_to_nearest_warzone_fortress\"",
                "\"unknownProfile\": true, \"id\": \"traversal_gate_to_nearest_warzone_fortress\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-SCHEMA-UNKNOWN-PROPERTY"));
        }

        [Test]
        public void MissingRequiredWarzonePartitionRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"world_adventure_outer_warzone\"",
                "\"world_adventure_removed_warzone\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(
                result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-REQUIRED-PARTITION-MISSING"));
        }

        [Test]
        public void NonCanonicalDimensionRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "dimension_adventure_3d",
                "dimension_unknown_3d",
                4);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-CANONICAL-DIMENSION-INVALID"));
        }

        [Test]
        public void UnknownBlockoutArchetypeRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"blockoutArchetype\": \"realm_capital\"",
                "\"blockoutArchetype\": \"unknown_archetype\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-BLOCKOUT-ARCHETYPE-INVALID"));
        }


        [TestCase("chunk_warzone_sector_01", 8)]
        [TestCase("chunk_ring_slot_01_main_gate", 3)]
        [TestCase("chunk_kingdom_area_12", 4)]
        [TestCase("chunk_accordant_entrance_04", 4)]
        [TestCase("chunk_stonehold_dragon_cave_lair", 3)]
        public void RenamedMandatoryChunkRejectsCatalog(
            string chunkId,
            int occurrences)
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                chunkId,
                chunkId + "_removed",
                occurrences);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-REQUIRED-PARTITION-MISSING"));
        }

        [Test]
        public void CanonicalWorldMetadataMismatchRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"usage\": \"inner_realm\"",
                "\"usage\": \"realm_dragon_cave\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-CANONICAL-WORLD-INVALID"));
        }

        [Test]
        public void CanonicalChunkArchetypeMismatchRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"blockoutArchetype\": \"realm_capital\"",
                "\"blockoutArchetype\": \"realm_area\"",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-CANONICAL-ARCHETYPE-INVALID"));
        }

        [TestCase(
            "chunk_ring_slot_01_capital_core.unity",
            "chunk_ring_slot_01_capital_shifted.unity")]
        [TestCase(
            "\"gridX\": 0",
            "\"gridX\": 1")]
        [TestCase(
            "socket_ring_slot_01_capital",
            "socket_ring_slot_01_capital_changed")]
        public void CanonicalSpatialContractMutationRejectsCatalog(
            string oldValue,
            string newValue)
        {
            byte[] bytes = Replace(CanonicalBytes(), oldValue, newValue, 1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-CANONICAL-TOPOLOGY-INVALID"));
        }

        [Test]
        public void CanonicalTraversalProfileValueMismatchRejectsCatalog()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"referenceSpeedMetersPerSecond\": 6",
                "\"referenceSpeedMetersPerSecond\": 7",
                1);

            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-WORLD-CANONICAL-TRAVERSAL-INVALID"));
        }

        [Test]
        public void ScenePathsAndIdsAreGloballyUnique()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();

            Assert.That(snapshot.Worlds.Select(value => value.Id), Is.Unique);
            Assert.That(snapshot.Chunks.Select(value => value.Id), Is.Unique);
            Assert.That(snapshot.Chunks.Select(value => value.ScenePath), Is.Unique);
        }


        private static WorldStreamingSnapshot AcceptedSnapshot()
        {
            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(CanonicalBytes());
            Assert.That(
                result.Status,
                Is.EqualTo(WorldStreamingLoadStatus.Accepted),
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            return result.Snapshot;
        }

        private static void AssertTraversal(
            WorldStreamingSnapshot snapshot,
            string profileId,
            int minimumSeconds,
            int maximumSeconds,
            float minimumDistanceMeters,
            float maximumDistanceMeters)
        {
            WorldTraversalProfileDefinition profile = snapshot.GetTraversalProfile(profileId);
            Assert.That(profile, Is.Not.Null, profileId);
            Assert.That(profile.DimensionId, Is.EqualTo("dimension_adventure_3d"));
            Assert.That(profile.ReferenceSpeedMetersPerSecond, Is.EqualTo(6f));
            Assert.That(profile.MinimumSeconds, Is.EqualTo(minimumSeconds));
            Assert.That(profile.MaximumSeconds, Is.EqualTo(maximumSeconds));
            Assert.That(profile.MinimumDistanceMeters, Is.EqualTo(minimumDistanceMeters));
            Assert.That(profile.MaximumDistanceMeters, Is.EqualTo(maximumDistanceMeters));
        }

        private static byte[] Replace(
            byte[] source,
            string oldValue,
            string newValue,
            int maximumReplacements)
        {
            string text = Encoding.UTF8.GetString(source);
            int start = 0;
            int replacements = 0;
            while (replacements < maximumReplacements)
            {
                int index = text.IndexOf(oldValue, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                text = text.Substring(0, index) +
                       newValue +
                       text.Substring(index + oldValue.Length);
                start = index + newValue.Length;
                replacements++;
            }

            Assert.That(replacements, Is.EqualTo(maximumReplacements));
            return Encoding.UTF8.GetBytes(text);
        }
    }
}
