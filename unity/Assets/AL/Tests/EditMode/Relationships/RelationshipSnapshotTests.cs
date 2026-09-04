using System.Collections;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;
using AL.Services.Relationships;
using NUnit.Framework;

namespace AL.Tests.EditMode.Relationships
{
    public class RelationshipSnapshotTests
    {
        [Test]
        public void EmptySaveIsValidSparseZeroForKnownMissingIds()
        {
            RelationshipSnapshot snapshot = RelationshipTestFixtures.Snapshot();
            RelationshipQueryResult query = RelationshipSnapshotBuilder.QueryNpcAffinity(
                snapshot,
                RelationshipTestFixtures.Identities(),
                RelationshipTestFixtures.Valerius);

            Assert.AreEqual(
                RelationshipDomainValidationStatus.ValidSparse,
                snapshot.NpcAffinityDomain.Status);
            Assert.AreEqual(RelationshipQueryStatus.AvailableSparseZero, query.Status);
            Assert.AreEqual(0d, query.Value);
            Assert.AreEqual(RelationshipTestFixtures.Valerius, query.CanonicalId);
        }

        [Test]
        public void NullTopLevelListIsCompatibleNormalized()
        {
            var raw = new RelationshipRawState(
                true,
                true,
                true,
                true,
                true,
                new[] { RelationshipNpcAffinityRow.Value("should-not-count", 1f) },
                new[] { RelationshipFactionRow.Value("should-not-count", 1) },
                RelationshipPersonaValues.From(1, 2, 3, 4));
            RelationshipSnapshot snapshot = RelationshipTestFixtures.Snapshot(raw);

            Assert.AreEqual(
                RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel,
                snapshot.NpcAffinityDomain.Status);
            Assert.AreEqual(0, snapshot.NpcAffinityDomain.SourceRecordCount);
            Assert.AreEqual(
                PersonaClassificationStatus.Unavailable,
                snapshot.PersonaDomain.Classification.Status);
        }

        [TestCase(80f, "affinity_exalted")]
        [TestCase(100f, "affinity_exalted")]
        [TestCase(50f, "affinity_friendly")]
        [TestCase(79.9f, "affinity_friendly")]
        [TestCase(0f, "affinity_neutral")]
        [TestCase(-50f, "affinity_hostile")]
        [TestCase(-49.9f, "affinity_hostile")]
        [TestCase(-100f, "affinity_nemesis")]
        [TestCase(-50.1f, "affinity_nemesis")]
        public void AffinityClassificationUsesExactLegacyBoundaries(
            float affinity,
            string classificationId)
        {
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(
                        RelationshipTestFixtures.Valerius,
                        affinity)
                });
            RelationshipSnapshot snapshot = RelationshipTestFixtures.Snapshot(raw);
            RelationshipClassificationQueryResult result =
                RelationshipSnapshotBuilder.ClassifyNpcAffinity(
                    snapshot,
                    RelationshipTestFixtures.Policies(),
                    RelationshipTestFixtures.Identities(),
                    RelationshipTestFixtures.Valerius);

            Assert.AreEqual(classificationId, result.ClassificationId);
            Assert.IsFalse(string.IsNullOrEmpty(result.ContentReference));
            Assert.AreNotEqual("Exalted", result.ClassificationId);
        }

        [TestCase(500, "faction_ally")]
        [TestCase(int.MaxValue, "faction_ally")]
        [TestCase(100, "faction_supporter")]
        [TestCase(0, "faction_neutral")]
        [TestCase(-100, "faction_opponent")]
        [TestCase(-500, "faction_enemy")]
        [TestCase(int.MinValue, "faction_enemy")]
        public void FactionClassificationUsesExactLegacyBoundaries(
            int reputation,
            string classificationId)
        {
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithFactionRows(
                new[]
                {
                    RelationshipFactionRow.Value(
                        RelationshipTestFixtures.VeilWatch,
                        reputation)
                });
            RelationshipSnapshot snapshot = RelationshipTestFixtures.Snapshot(raw);
            RelationshipClassificationQueryResult result =
                RelationshipSnapshotBuilder.ClassifyFactionReputation(
                    snapshot,
                    RelationshipTestFixtures.Policies(),
                    RelationshipTestFixtures.Identities(),
                    RelationshipTestFixtures.VeilWatch);

            Assert.AreEqual(classificationId, result.ClassificationId);
        }

        [Test]
        public void UnknownNonblankIdIsPreservedAndExcluded()
        {
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value("npc_future_unknown", 12f),
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, 5f)
                });
            RelationshipSnapshot snapshot = RelationshipTestFixtures.Snapshot(raw);

            Assert.AreEqual(
                RelationshipDomainValidationStatus.PreservedUnknown,
                snapshot.NpcAffinityDomain.Status);
            Assert.AreEqual(1, snapshot.NpcAffinityDomain.PreservedUnknownIds.Count);
            Assert.AreEqual(
                5f,
                snapshot.NpcAffinityDomain.SupportedValuesByCanonicalNpcId[
                    RelationshipTestFixtures.Valerius]);
            Assert.IsFalse(
                snapshot.NpcAffinityDomain.SupportedValuesByCanonicalNpcId.ContainsKey(
                    "npc_future_unknown"));
        }

        [Test]
        public void NullBlankDuplicateAndNonFiniteRowsMalformedWithoutRepair()
        {
            RelationshipRawState nullRow = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[] { RelationshipNpcAffinityRow.NullEntry() });
            RelationshipRawState blank = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[] { RelationshipNpcAffinityRow.Value(string.Empty, 1f) });
            RelationshipRawState duplicate = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, 1f),
                    RelationshipNpcAffinityRow.Value(
                        RelationshipTestFixtures.ValeriusAlias,
                        2f)
                });
            RelationshipRawState nan = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(
                        RelationshipTestFixtures.Valerius,
                        float.NaN)
                });
            RelationshipRawState inf = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(
                        RelationshipTestFixtures.Valerius,
                        float.PositiveInfinity)
                });
            RelationshipRawState outOfRange = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(
                        RelationshipTestFixtures.Valerius,
                        100.1f)
                });

            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedNullEntry,
                RelationshipTestFixtures.Snapshot(nullRow).NpcAffinityDomain.Status);
            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedBlankId,
                RelationshipTestFixtures.Snapshot(blank).NpcAffinityDomain.Status);
            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedDuplicateId,
                RelationshipTestFixtures.Snapshot(duplicate).NpcAffinityDomain.Status);
            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedNonFinite,
                RelationshipTestFixtures.Snapshot(nan).NpcAffinityDomain.Status);
            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedNonFinite,
                RelationshipTestFixtures.Snapshot(inf).NpcAffinityDomain.Status);
            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedOutOfRange,
                RelationshipTestFixtures.Snapshot(outOfRange).NpcAffinityDomain.Status);
            Assert.AreEqual(
                0,
                RelationshipTestFixtures.Snapshot(duplicate)
                    .NpcAffinityDomain.SupportedValuesByCanonicalNpcId.Count);
        }

        [Test]
        public void QueryIsPureImmutableAndDeterministic()
        {
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, 60f)
                });
            RelationshipSnapshot first = RelationshipTestFixtures.Snapshot(raw);
            RelationshipSnapshot second = RelationshipTestFixtures.Snapshot(raw);

            Assert.AreEqual(first.SnapshotRevision, second.SnapshotRevision);
            Assert.AreEqual(
                first.NpcAffinityDomain.Fingerprint,
                second.NpcAffinityDomain.Fingerprint);
            Assert.Throws<System.NotSupportedException>(() =>
                ((IDictionary)first.NpcAffinityDomain.SupportedValuesByCanonicalNpcId).Clear());
        }

        [Test]
        public void PersonaAllZeroTieAndUniqueAreHonest()
        {
            PersonaClassificationResult allZero =
                RelationshipTestFixtures.Snapshot().PersonaDomain.Classification;
            PersonaClassificationResult unique = RelationshipTestFixtures.Snapshot(
                    RelationshipRawState.EmptyWritable().WithPersona(
                        RelationshipPersonaValues.From(3, 1, 1, 1)))
                .PersonaDomain.Classification;
            PersonaClassificationResult twoWay = RelationshipTestFixtures.Snapshot(
                    RelationshipRawState.EmptyWritable().WithPersona(
                        RelationshipPersonaValues.From(4, 4, 1, 0)))
                .PersonaDomain.Classification;
            PersonaClassificationResult fourWay = RelationshipTestFixtures.Snapshot(
                    RelationshipRawState.EmptyWritable().WithPersona(
                        RelationshipPersonaValues.From(-2, -2, -2, -2)))
                .PersonaDomain.Classification;

            Assert.AreEqual(PersonaClassificationStatus.AllZero, allZero.Status);
            Assert.IsNull(allZero.DominantTrait);
            Assert.AreEqual(PersonaClassificationStatus.UniqueDominant, unique.Status);
            Assert.AreEqual(PersonaTrait.Warlord, unique.DominantTrait);
            Assert.AreEqual(PersonaClassificationStatus.Tie, twoWay.Status);
            Assert.AreEqual(2, twoWay.TiedTraits.Count);
            Assert.AreEqual(PersonaTrait.Warlord, twoWay.TiedTraits[0]);
            Assert.AreEqual(PersonaTrait.Diplomat, twoWay.TiedTraits[1]);
            Assert.AreEqual(PersonaClassificationStatus.Tie, fourWay.Status);
            Assert.AreEqual(4, fourWay.TiedTraits.Count);
            Assert.IsNull(fourWay.DominantTrait);
        }

        [Test]
        public void NoSaveSnapshotDoesNotInventRows()
        {
            RelationshipSnapshot snapshot = RelationshipTestFixtures.Snapshot(
                RelationshipRawState.NoSave());

            Assert.AreEqual(
                RelationshipDomainValidationStatus.UnavailableNoCurrentSave,
                snapshot.NpcAffinityDomain.Status);
            Assert.AreEqual(
                RelationshipQueryStatus.UnavailableNoSave,
                RelationshipSnapshotBuilder.QueryNpcAffinity(
                    snapshot,
                    RelationshipTestFixtures.Identities(),
                    RelationshipTestFixtures.Valerius).Status);
        }
    }
}
