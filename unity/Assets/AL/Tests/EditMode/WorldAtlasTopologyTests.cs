using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public void CanonicalV003AddsVersionedProtectedSubzoneAuthorityWithoutChangingV002Topology()
        {
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(CanonicalBytes());

            Assert.That(
                result.Status,
                Is.EqualTo(WorldAtlasLoadStatus.Accepted),
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(result.Snapshot.Version, Is.EqualTo("0.3.0"));
            Assert.That(result.Snapshot.TopologyId, Is.EqualTo("topology_launch_world_ring_v001"));
            Assert.That(result.Snapshot.Nodes.Count, Is.EqualTo(5));
            Assert.That(result.Snapshot.Adjacencies.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.Bridges.Count, Is.EqualTo(12));
            Assert.That(result.Snapshot.Endpoints.Count, Is.EqualTo(24));
            Assert.That(result.Snapshot.Boundaries.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.Zones.Count, Is.EqualTo(11));
            Assert.That(result.Snapshot.Objectives.Count, Is.EqualTo(5));
            Assert.That(result.Snapshot.ProtectedZonePolicies.Count, Is.EqualTo(3));
            Assert.That(result.Snapshot.ProtectedSubzones.Count, Is.EqualTo(12));
            CollectionAssert.AreEqual(
                new[]
                {
                    "ring_slot_01",
                    "ring_slot_02",
                    "ring_slot_03",
                    "ring_slot_04",
                    "center_slot"
                },
                result.Snapshot.Nodes.Select(value => value.Id));
            CollectionAssert.AreEqual(
                new[]
                {
                    "zone_policy_city_safe_v001",
                    "zone_policy_beginner_safe_v001",
                    "zone_policy_town_safe_v001"
                },
                result.Snapshot.ProtectedZonePolicies.Select(value => value.Id));
            CollectionAssert.AreEqual(
                new[]
                {
                    "zone_protected_crownlands_city",
                    "zone_protected_crownlands_beginner",
                    "zone_protected_crownlands_town",
                    "zone_protected_stonehold_city",
                    "zone_protected_stonehold_beginner",
                    "zone_protected_stonehold_town",
                    "zone_protected_eldergrove_city",
                    "zone_protected_eldergrove_beginner",
                    "zone_protected_eldergrove_town",
                    "zone_protected_umbral_city",
                    "zone_protected_umbral_beginner",
                    "zone_protected_umbral_town"
                },
                result.Snapshot.ProtectedSubzones.Select(value => value.Id));
            Assert.That(result.Snapshot.SourceSha256, Has.Length.EqualTo(64));
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void UnsupportedVersionIsRejectedWithoutSnapshot()
        {
            byte[] bytes = Replace(CanonicalBytes(), "\"version\": \"0.3.0\"", "\"version\": \"9.0.0\"");

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
        public void DuplicateReverseAndUnknownBoundariesFailClosed()
        {
            byte[] duplicate = Replace(
                CanonicalBytes(),
                "\"id\": \"boundary_stonehold_safe_to_warzone\"",
                "\"id\": \"boundary_crownlands_safe_to_warzone\"",
                1);
            byte[] reverse = Replace(
                CanonicalBytes(),
                "\"protected_inner_safe_zone\",\n        \"inner_wall\",\n        \"controlled_main_gate_transition\",\n        \"outer_wall\",\n        \"outer_warzone\"",
                "\"outer_warzone\",\n        \"outer_wall\",\n        \"controlled_main_gate_transition\",\n        \"inner_wall\",\n        \"protected_inner_safe_zone\"",
                1);
            byte[] unknown = Replace(
                CanonicalBytes(),
                "\"innerAtlasZoneId\": \"zone_inner_crownlands\"",
                "\"innerAtlasZoneId\": \"zone_inner_unknown\"",
                1);

            WorldAtlasLoadResult duplicateResult = WorldAtlasTopologyLoader.Validate(duplicate);
            WorldAtlasLoadResult reverseResult = WorldAtlasTopologyLoader.Validate(reverse);
            WorldAtlasLoadResult unknownResult = WorldAtlasTopologyLoader.Validate(unknown);

            Assert.That(duplicateResult.IsAccepted, Is.False);
            Assert.That(duplicateResult.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-ID-DUPLICATE"));
            Assert.That(reverseResult.IsAccepted, Is.False);
            Assert.That(reverseResult.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-BOUNDARY-INVALID"));
            Assert.That(unknownResult.IsAccepted, Is.False);
            Assert.That(unknownResult.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-REFERENCE-MISSING"));
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
            Assert.Throws<NotSupportedException>(() => ((IList)snapshot.ProtectedZonePolicies).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList)snapshot.ProtectedSubzones).Clear());
            Assert.That(snapshot.Nodes[0].GetType().GetFields().Length, Is.Zero);
            Assert.That(snapshot.Bridges[0].GetType().GetFields().Length, Is.Zero);
            Assert.That(
                typeof(WorldAtlasProtectedZonePolicy).GetProperties().Where(value => value.CanWrite),
                Is.Empty);
            Assert.That(
                typeof(WorldAtlasProtectedSubzone).GetFields(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
        }

        [Test]
        public void ProtectedZoneQueriesAreTypedImmutableAndDeterministic()
        {
            var query = new WorldAtlasTopologyQuery(WorldAtlasTopologyLoader.Validate(CanonicalBytes()).Snapshot);

            WorldAtlasQueryResult<WorldAtlasProtectedZonePolicy> policy =
                query.GetProtectedZonePolicy("zone_policy_city_safe_v001");
            WorldAtlasQueryResult<WorldAtlasProtectedSubzone> subzone =
                query.GetProtectedSubzone("zone_protected_crownlands_city");
            WorldAtlasQueryResult<System.Collections.Generic.IReadOnlyList<WorldAtlasProtectedSubzone>> realm =
                query.GetProtectedSubzonesForRealm("crownlands");

            Assert.That(policy.Status, Is.EqualTo(WorldAtlasQueryStatus.Found));
            Assert.That(policy.Value.Protection, Is.EqualTo("forced_non_pvp"));
            Assert.That(policy.Value.AppliesTo, Is.EqualTo("all_player_harmful_effects"));
            Assert.That(policy.Value.ApplicationRecheck, Is.EqualTo("required"));
            Assert.That(policy.Value.WarOverride, Is.EqualTo("blocked"));
            Assert.That(policy.Value.EnforcementStatus, Is.EqualTo("contract_only"));
            Assert.That(policy.Value.MutationAuthority, Is.EqualTo("none"));
            Assert.That(subzone.Status, Is.EqualTo(WorldAtlasQueryStatus.Found));
            Assert.That(subzone.Value.ParentAtlasZoneId, Is.EqualTo("zone_inner_crownlands"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "zone_protected_crownlands_city",
                    "zone_protected_crownlands_beginner",
                    "zone_protected_crownlands_town"
                },
                realm.Value.Select(value => value.Id));
            Assert.Throws<NotSupportedException>(() => ((IList)realm.Value).Clear());
            Assert.That(query.GetProtectedZonePolicy(null).Status, Is.EqualTo(WorldAtlasQueryStatus.InvalidId));
            Assert.That(query.GetProtectedSubzone("zone_missing").Status, Is.EqualTo(WorldAtlasQueryStatus.UnknownId));
            Assert.That(query.GetProtectedSubzonesForRealm("missing").Status, Is.EqualTo(WorldAtlasQueryStatus.UnknownId));
        }

        [Test]
        public void ProtectedZoneAuthorityRejectsDuplicateUnknownAndMutableClaims()
        {
            byte[] duplicate = Replace(
                CanonicalBytes(),
                "\"zone_protected_stonehold_city\"",
                "\"zone_protected_crownlands_city\"",
                1);
            byte[] unknownPolicy = Replace(
                CanonicalBytes(),
                "\"policyId\": \"zone_policy_city_safe_v001\"",
                "\"policyId\": \"zone_policy_unknown\"",
                1);
            byte[] unknownParent = Replace(
                CanonicalBytes(),
                "\"parentAtlasZoneId\": \"zone_inner_crownlands\"",
                "\"parentAtlasZoneId\": \"zone_inner_unknown\"",
                1);
            byte[] mutable = Replace(
                CanonicalBytes(),
                "\"warOverride\": \"blocked\"",
                "\"warOverride\": \"allowed\"",
                1);

            WorldAtlasLoadResult duplicateResult = WorldAtlasTopologyLoader.Validate(duplicate);
            WorldAtlasLoadResult unknownPolicyResult = WorldAtlasTopologyLoader.Validate(unknownPolicy);
            WorldAtlasLoadResult unknownParentResult = WorldAtlasTopologyLoader.Validate(unknownParent);
            WorldAtlasLoadResult mutableResult = WorldAtlasTopologyLoader.Validate(mutable);

            Assert.That(duplicateResult.IsAccepted, Is.False);
            Assert.That(duplicateResult.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-ID-DUPLICATE"));
            Assert.That(unknownPolicyResult.IsAccepted, Is.False);
            Assert.That(unknownPolicyResult.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-REFERENCE-MISSING"));
            Assert.That(unknownParentResult.IsAccepted, Is.False);
            Assert.That(unknownParentResult.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-REFERENCE-MISSING"));
            Assert.That(mutableResult.IsAccepted, Is.False);
            Assert.That(mutableResult.Diagnostics.Select(value => value.Code), Does.Contain("AL-ATLAS-PROTECTED-ZONE-INVALID"));
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
