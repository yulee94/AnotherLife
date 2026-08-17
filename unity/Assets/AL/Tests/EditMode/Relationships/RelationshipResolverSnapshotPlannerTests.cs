using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AL.Core.Relationships;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Relationships
{
    public sealed class RelationshipResolverSnapshotPlannerTests
    {
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json";

        [Test]
        public void ExactCatalogResolvesCanonicalAndAliasIdsWithoutGuessing()
        {
            byte[] bytes = ReadCatalogBytes();
            var resolver = new RelationshipCatalogResolver(bytes);

            RelationshipIdentityResolution canonical = resolver.ResolveNpc("npc_valerius");
            RelationshipIdentityResolution alias = resolver.ResolveNpc("NPC_VALERIUS");

            Assert.AreEqual(RelationshipResolutionStatus.Found, canonical.Status);
            Assert.AreEqual("npc_valerius", canonical.Identity.CanonicalId);
            Assert.AreEqual(RelationshipResolutionStatus.AliasResolved, alias.Status);
            Assert.AreEqual("npc_valerius", alias.Identity.CanonicalId);
            Assert.AreEqual("affinity_legacy_five_band", alias.Identity.ClassificationProfileId);
            Assert.IsTrue(alias.Identity.RelationshipEnabled);
            CollectionAssert.AreEqual(
                new[] { "NPC_VALERIUS", "ADVISOR_VALERIUS" },
                canonical.Identity.LegacyAliases);
            Assert.AreEqual(RelationshipCatalogResolver.ExpectedSourceSha256, resolver.PolicyRevision);
        }

        [TestCase(null, RelationshipResolutionStatus.InvalidId)]
        [TestCase("", RelationshipResolutionStatus.InvalidId)]
        [TestCase(" npc_valerius", RelationshipResolutionStatus.UnknownId)]
        [TestCase("NPC_valerius", RelationshipResolutionStatus.UnknownId)]
        [TestCase("Captain Valerius", RelationshipResolutionStatus.UnknownId)]
        [TestCase("npc_future", RelationshipResolutionStatus.UnknownId)]
        public void InvalidAndUnknownIdsFailClosed(string id, RelationshipResolutionStatus expected)
        {
            var resolver = NewResolver();

            RelationshipIdentityResolution result = resolver.ResolveNpc(id);

            Assert.AreEqual(expected, result.Status);
            Assert.IsNull(result.Identity);
        }

        [Test]
        public void InvalidOrMutatedCatalogPublishesNothing()
        {
            byte[] bytes = ReadCatalogBytes();
            bytes[bytes.Length - 2] ^= 1;
            var invalid = new RelationshipCatalogResolver(bytes);
            var unavailable = new RelationshipCatalogResolver(null);

            Assert.AreEqual(RelationshipResolutionStatus.InvalidRecord, invalid.ResolveNpc("npc_valerius").Status);
            Assert.AreEqual(RelationshipResolutionStatus.CatalogUnavailable, unavailable.ResolveNpc("npc_valerius").Status);
            Assert.IsEmpty(invalid.NpcIds);
            Assert.IsEmpty(invalid.FactionIds);
        }

        [Test]
        public void PublishedCatalogViewsAreImmutableAndIsolatedFromInputBytes()
        {
            byte[] bytes = ReadCatalogBytes();
            var resolver = new RelationshipCatalogResolver(bytes);
            RelationshipIdentity before = resolver.ResolveNpc("npc_valerius").Identity;

            Array.Clear(bytes, 0, bytes.Length);

            Assert.AreSame(before, resolver.ResolveNpc("npc_valerius").Identity);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)resolver.NpcIds).Add("npc_injected"));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)before.LegacyAliases).Add("NPC_INJECTED"));
        }

        [Test]
        public void SnapshotCanonicalizesAliasesSortsValuesAndHasDeterministicRevision()
        {
            var resolver = NewResolver();
            var firstRows = new[]
            {
                new RelationshipNumericRow("NPC_VALERIUS", 12),
                new RelationshipNumericRow("npc_gruff", -3),
                new RelationshipNumericRow("npc_future", 77)
            };
            var secondRows = firstRows.Reverse().ToArray();

            RelationshipNumericSnapshot first = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver, firstRows, true);
            RelationshipNumericSnapshot second = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver, secondRows, true);

            Assert.AreEqual(RelationshipSnapshotStatus.ValidWithPreservedUnknown, first.Status);
            CollectionAssert.AreEqual(new[] { "npc_gruff", "npc_valerius" }, first.Values.Keys);
            CollectionAssert.AreEqual(new[] { "npc_future" }, first.PreservedUnknownIds);
            Assert.AreEqual(first.SnapshotRevision, second.SnapshotRevision);
            Assert.AreEqual(12d, first.Values["npc_valerius"]);
        }

        [Test]
        public void SnapshotIsolatedFromRowsAndReturnedCollectionsAreReadOnly()
        {
            var resolver = NewResolver();
            var rows = new List<RelationshipNumericRow>
            {
                new RelationshipNumericRow("npc_valerius", 5)
            };
            RelationshipNumericSnapshot snapshot = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver, rows, true);

            rows[0] = new RelationshipNumericRow("npc_valerius", 99);
            rows.Clear();

            Assert.AreEqual(5d, snapshot.Values["npc_valerius"]);
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, double>)snapshot.Values).Add("npc_gruff", 1));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)snapshot.Diagnostics).Add("mutated"));
        }

        [TestCase(null, RelationshipSnapshotStatus.MalformedNullRow)]
        [TestCase("", RelationshipSnapshotStatus.MalformedBlankId)]
        public void SnapshotRejectsNullRowsAndBlankIds(string id, RelationshipSnapshotStatus expected)
        {
            RelationshipNumericRow[] rows = id == null
                ? new RelationshipNumericRow[] { null }
                : new[] { new RelationshipNumericRow(id, 1) };

            RelationshipNumericSnapshot snapshot = RelationshipSnapshotBuilder.BuildNpcAffinity(
                NewResolver(), rows, true);

            Assert.AreEqual(expected, snapshot.Status);
            Assert.IsFalse(snapshot.CanPlan);
        }

        [Test]
        public void SnapshotRejectsDuplicateCanonicalIdsAndInvalidAffinity()
        {
            var duplicate = RelationshipSnapshotBuilder.BuildNpcAffinity(
                NewResolver(),
                new[]
                {
                    new RelationshipNumericRow("npc_valerius", 1),
                    new RelationshipNumericRow("NPC_VALERIUS", 2)
                },
                true);
            var nonFinite = RelationshipSnapshotBuilder.BuildNpcAffinity(
                NewResolver(),
                new[] { new RelationshipNumericRow("npc_valerius", double.NaN) },
                true);

            Assert.AreEqual(RelationshipSnapshotStatus.MalformedDuplicateId, duplicate.Status);
            Assert.AreEqual(RelationshipSnapshotStatus.MalformedNonFinite, nonFinite.Status);
            Assert.IsEmpty(duplicate.Values);
            Assert.IsEmpty(nonFinite.Values);
        }

        [Test]
        public void AffinityPlannerCreatesSparseRowsClampsAndBindsRevision()
        {
            RelationshipNumericSnapshot snapshot = RelationshipSnapshotBuilder.BuildNpcAffinity(
                NewResolver(), Array.Empty<RelationshipNumericRow>(), true);

            RelationshipMutationPlan plan = RelationshipPlanner.PlanAffinity(
                NewResolver(), snapshot, "NPC_VALERIUS", 150, "operation-1", "correlation-1");

            Assert.AreEqual(RelationshipPlanStatus.PreparedClamped, plan.Status);
            Assert.AreEqual(RelationshipRowOperation.Create, plan.RowOperation);
            Assert.AreEqual("npc_valerius", plan.CanonicalTargetId);
            Assert.AreEqual(0d, plan.PreviousValue);
            Assert.AreEqual(100d, plan.NewValue);
            Assert.AreEqual(100d, plan.AppliedDelta);
            Assert.AreEqual(snapshot.SnapshotRevision, plan.ExpectedSnapshotRevision);
            Assert.AreEqual(snapshot.PolicyRevision, plan.PolicyRevision);
        }

        [Test]
        public void PlannerRejectsUnknownInvalidDeltaMalformedAndReadOnlySnapshots()
        {
            var resolver = NewResolver();
            RelationshipNumericSnapshot valid = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver, Array.Empty<RelationshipNumericRow>(), true);
            RelationshipNumericSnapshot malformed = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver, new RelationshipNumericRow[] { null }, true);
            RelationshipNumericSnapshot readOnly = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver, Array.Empty<RelationshipNumericRow>(), false);

            Assert.AreEqual(RelationshipPlanStatus.RejectedUnknownId,
                RelationshipPlanner.PlanAffinity(resolver, valid, "npc_future", 1, "op", "corr").Status);
            Assert.AreEqual(RelationshipPlanStatus.RejectedInvalidDelta,
                RelationshipPlanner.PlanAffinity(resolver, valid, "npc_valerius", double.PositiveInfinity, "op", "corr").Status);
            Assert.AreEqual(RelationshipPlanStatus.RejectedMalformedSnapshot,
                RelationshipPlanner.PlanAffinity(resolver, malformed, "npc_valerius", 1, "op", "corr").Status);
            Assert.AreEqual(RelationshipPlanStatus.RejectedReadOnly,
                RelationshipPlanner.PlanAffinity(resolver, readOnly, "npc_valerius", 1, "op", "corr").Status);
        }

        [Test]
        public void FactionPlannerUsesCheckedInt32Arithmetic()
        {
            var resolver = NewResolver();
            RelationshipNumericSnapshot snapshot = RelationshipSnapshotBuilder.BuildFactionReputation(
                resolver,
                new[] { new RelationshipNumericRow("faction_veil_watch", int.MaxValue) },
                true);

            RelationshipMutationPlan plan = RelationshipPlanner.PlanFaction(
                resolver, snapshot, "FACTION_VEIL_WATCH", 1, "op", "corr");

            Assert.AreEqual(RelationshipPlanStatus.RejectedOverflow, plan.Status);
        }

        [Test]
        public void PersonaSnapshotClassifiesAllZeroTieAndUniqueDeterministically()
        {
            RelationshipPersonaSnapshot allZero = RelationshipSnapshotBuilder.BuildPersona(0, 0, 0, 0, true);
            RelationshipPersonaSnapshot tie = RelationshipSnapshotBuilder.BuildPersona(4, 1, 4, -2, true);
            RelationshipPersonaSnapshot unique = RelationshipSnapshotBuilder.BuildPersona(-3, -2, -1, -4, true);

            Assert.AreEqual(RelationshipPersonaClassification.AllZero, allZero.Classification);
            Assert.AreEqual(RelationshipPersonaClassification.Tie, tie.Classification);
            CollectionAssert.AreEqual(new[] { "warlord", "sage" }, tie.DominantTraitIds);
            Assert.AreEqual(RelationshipPersonaClassification.UniqueDominant, unique.Classification);
            CollectionAssert.AreEqual(new[] { "sage" }, unique.DominantTraitIds);
            Assert.AreEqual(unique.SnapshotRevision,
                RelationshipSnapshotBuilder.BuildPersona(-3, -2, -1, -4, true).SnapshotRevision);
        }

        private static RelationshipCatalogResolver NewResolver()
        {
            return new RelationshipCatalogResolver(ReadCatalogBytes());
        }

        private static byte[] ReadCatalogBytes()
        {
            return File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath));
        }
    }
}
