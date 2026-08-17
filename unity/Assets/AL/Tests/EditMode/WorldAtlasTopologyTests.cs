using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using AL.Data.Catalogs.WorldAtlas;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class WorldAtlasTopologyTests
    {
        private static byte[] CanonicalBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
        }

        [Test]
        public void CanonicalV002LoadsIntoImmutableSnapshot()
        {
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(CanonicalBytes());

            Assert.That(
                result.Status,
                Is.EqualTo(WorldAtlasLoadStatus.Accepted),
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(result.Snapshot.Version, Is.EqualTo("0.2.0"));
            Assert.That(result.Snapshot.TopologyId, Is.EqualTo("topology_launch_world_ring_v001"));
            Assert.That(result.Snapshot.Nodes.Count, Is.EqualTo(5));
            Assert.That(result.Snapshot.Adjacencies.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.Bridges.Count, Is.EqualTo(12));
            Assert.That(result.Snapshot.Endpoints.Count, Is.EqualTo(24));
            Assert.That(result.Snapshot.Boundaries.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.Zones.Count, Is.EqualTo(11));
            Assert.That(result.Snapshot.Objectives.Count, Is.EqualTo(5));
            Assert.That(result.Snapshot.SourceSha256, Has.Length.EqualTo(64));
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void UnsupportedVersionIsRejectedWithoutSnapshot()
        {
            byte[] bytes = Replace(CanonicalBytes(), "\"version\": \"0.2.0\"", "\"version\": \"9.0.0\"");

            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldAtlasLoadStatus.UnsupportedVersion));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-VERSION-UNSUPPORTED"));
        }

        [Test]
        public void UnknownBridgeNodeIsRejectedWithoutRepairingInput()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"nodeBId\": \"ring_slot_02\"",
                "\"nodeBId\": \"ring_slot_99\"",
                1);
            byte[] before = (byte[])bytes.Clone();

            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldAtlasLoadStatus.Rejected));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-REFERENCE-MISSING"));
            CollectionAssert.AreEqual(before, bytes);
        }

        [Test]
        public void BridgeWithoutDeclaredAdjacencyIsRejected()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"nodeBId\": \"ring_slot_02\"",
                "\"nodeBId\": \"ring_slot_03\"",
                1);

            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldAtlasLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-TOPOLOGY-INVALID"));
        }

        [Test]
        public void ReorderedBoundaryStagesAreRejected()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"protected_inner_safe_zone\",\n        \"inner_wall\"",
                "\"inner_wall\",\n        \"protected_inner_safe_zone\"",
                1);

            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldAtlasLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-BOUNDARY-INVALID"));
        }

        [Test]
        public void UnknownObjectiveZoneReferenceIsRejected()
        {
            byte[] bytes = Replace(
                CanonicalBytes(),
                "\"requiredZoneIds\": [\n        \"zone_crossroads_bridges\"\n      ]",
                "\"requiredZoneIds\": [\n        \"zone_missing\"\n      ]",
                1);

            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);

            Assert.That(result.Status, Is.EqualTo(WorldAtlasLoadStatus.Rejected));
            Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-REFERENCE-MISSING"));
        }

        [Test]
        public void SnapshotCollectionsAndRecordsCannotBeMutated()
        {
            WorldAtlasSnapshot snapshot = WorldAtlasTopologyLoader.Validate(CanonicalBytes()).Snapshot;

            Assert.Throws<NotSupportedException>(() => ((IList)snapshot.Nodes).Add(null));
            Assert.Throws<NotSupportedException>(() => ((IList)snapshot.Bridges).RemoveAt(0));
            Assert.That(snapshot.Nodes[0].GetType().GetFields().Length, Is.Zero);
            Assert.That(snapshot.Bridges[0].GetType().GetFields().Length, Is.Zero);
        }

        [Test]
        public void QueriesUseDeterministicCanonicalOrderingAndRepresentativeLookups()
        {
            WorldAtlasSnapshot snapshot = WorldAtlasTopologyLoader.Validate(CanonicalBytes()).Snapshot;
            var query = new WorldAtlasTopologyQuery(snapshot);

            CollectionAssert.AreEqual(
                new[] { "ring_slot_02", "ring_slot_04", "center_slot" },
                query.GetNeighborNodeIds("ring_slot_01"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "bridge_ring_01_02_01",
                    "bridge_ring_01_02_02",
                    "bridge_ring_01_04_01",
                    "bridge_ring_01_04_02",
                    "bridge_center_ring_01_01"
                },
                query.GetBridgesForNode("ring_slot_01").Select(value => value.Id));
            Assert.That(query.TryGetNode("center_slot", out WorldAtlasNode center), Is.True);
            Assert.That(center.AtlasZoneId, Is.EqualTo("zone_accordant_isle"));
            Assert.That(query.TryGetBoundary("crownlands", out WorldAtlasBoundary boundary), Is.True);
            Assert.That(boundary.InnerAtlasZoneId, Is.EqualTo("zone_inner_crownlands"));
            Assert.That(query.TryGetZone("zone_accordant_isle", out WorldAtlasZone zone), Is.True);
            Assert.That(zone.RealmId, Is.EqualTo("shared"));
            Assert.That(query.TryGetBridge("missing", out _), Is.False);
        }

        [Test]
        public void RealmSpecificTopologyRemainsExplicitlyUnavailableWhilePlacementIsUnresolved()
        {
            var query = new WorldAtlasTopologyQuery(WorldAtlasTopologyLoader.Validate(CanonicalBytes()).Snapshot);

            WorldAtlasQueryResult<WorldAtlasNode> result = query.GetNodeForRealm("crownlands");

            Assert.That(result.Status, Is.EqualTo(WorldAtlasQueryStatus.PlacementUnresolved));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.DiagnosticCode, Is.EqualTo("AL-ATLAS-REALM-PLACEMENT-UNRESOLVED"));
        }

        [Test]
        public void ValidationDiagnosticsAreDeterministic()
        {
            byte[] bytes = Replace(
                Replace(CanonicalBytes(), "\"nodeBId\": \"ring_slot_02\"", "\"nodeBId\": \"ring_slot_99\"", 1),
                "\"outerWallId\": \"wall_crownlands_outer\"",
                "\"outerWallId\": \"wall_missing\"",
                1);

            WorldAtlasLoadResult first = WorldAtlasTopologyLoader.Validate(bytes);
            WorldAtlasLoadResult second = WorldAtlasTopologyLoader.Validate(bytes);

            CollectionAssert.AreEqual(
                first.Diagnostics.Select(value => value.Fingerprint),
                second.Diagnostics.Select(value => value.Fingerprint));
        }

        private static byte[] Replace(byte[] source, string oldValue, string newValue, int maximumReplacements = int.MaxValue)
        {
            string text = Encoding.UTF8.GetString(source);
            int start = 0;
            int replacements = 0;
            while (replacements < maximumReplacements)
            {
                int index = text.IndexOf(oldValue, start, StringComparison.Ordinal);
                if (index < 0) break;
                text = text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
                start = index + newValue.Length;
                replacements++;
            }

            Assert.That(replacements, Is.GreaterThan(0), "The fixture token was not found.");
            return Encoding.UTF8.GetBytes(text);
        }
    }
}
