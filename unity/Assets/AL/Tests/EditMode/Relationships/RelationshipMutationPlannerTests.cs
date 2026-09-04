using System;
using System.Collections;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;
using AL.Services.Relationships;
using NUnit.Framework;

namespace AL.Tests.EditMode.Relationships
{
    public class RelationshipMutationPlannerTests
    {
        [Test]
        public void SparseKnownNpcStagesCreateFromZeroWithoutMutatingSnapshot()
        {
            RelationshipSnapshot snapshot = RelationshipTestFixtures.Snapshot();
            string before = snapshot.NpcAffinityDomain.Fingerprint;
            RelationshipPlanningResult result = RelationshipTestFixtures.Planner().Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.Valerius,
                    5f,
                    RelationshipTestFixtures.CorrelationId,
                    RelationshipTestFixtures.OperationId,
                    RelationshipTestFixtures.SourceSystemId),
                snapshot);

            Assert.AreEqual(RelationshipPreparationStatus.Prepared, result.Status);
            Assert.AreEqual(RelationshipRowOperation.Create, result.Plan.RowOperation);
            Assert.AreEqual(0d, result.Plan.PreviousValue);
            Assert.AreEqual(5d, result.Plan.NewValue);
            Assert.AreEqual(5d, result.Plan.AppliedDelta);
            Assert.AreEqual(before, snapshot.NpcAffinityDomain.Fingerprint);
            Assert.AreEqual(0, snapshot.NpcAffinityDomain.SupportedValuesByCanonicalNpcId.Count);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.Plan.Diagnostics).Clear());
        }

        [Test]
        public void ApprovedValeriusPlusFiveUsesCanonicalIdNotAndroidPreview()
        {
            RelationshipPlanningResult result = RelationshipTestFixtures.Planner().Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.ValeriusAlias,
                    5f,
                    RelationshipTestFixtures.CorrelationId,
                    RelationshipTestFixtures.OperationId,
                    RelationshipTestFixtures.SourceSystemId),
                RelationshipTestFixtures.Snapshot());

            Assert.AreEqual(RelationshipPreparationStatus.Prepared, result.Status);
            Assert.AreEqual(RelationshipTestFixtures.Valerius, result.Plan.CanonicalTargetId);
            Assert.AreEqual(5d, result.Plan.NewValue);
        }

        [TestCase(10f, 10f, RelationshipPreparationStatus.Prepared, false)]
        [TestCase(0f, 0f, RelationshipPreparationStatus.NoChange, false)]
        [TestCase(-3f, -3f, RelationshipPreparationStatus.Prepared, false)]
        public void AffinityDeltaPaths(
            float delta,
            float applied,
            RelationshipPreparationStatus status,
            bool clamped)
        {
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, 40f)
                });
            RelationshipPlanningResult result = RelationshipTestFixtures.Planner().Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.Valerius,
                    delta,
                    RelationshipTestFixtures.CorrelationId,
                    RelationshipTestFixtures.OperationId,
                    RelationshipTestFixtures.SourceSystemId),
                RelationshipTestFixtures.Snapshot(raw));

            Assert.AreEqual(status, result.Status);
            if (status == RelationshipPreparationStatus.NoChange)
            {
                Assert.AreEqual(RelationshipRowOperation.None, result.Plan.RowOperation);
            }
            else
            {
                Assert.AreEqual(applied, result.Plan.AppliedDelta);
                Assert.AreEqual(clamped, result.Plan.WasClamped);
            }
        }

        [Test]
        public void AffinityClampExposesRequestedVersusApplied()
        {
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, 90f)
                });
            RelationshipPlanningResult upper = RelationshipTestFixtures.Planner().Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.Valerius,
                    25f,
                    RelationshipTestFixtures.CorrelationId,
                    "op-upper",
                    RelationshipTestFixtures.SourceSystemId),
                RelationshipTestFixtures.Snapshot(raw));
            RelationshipRawState lowRaw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, -90f)
                });
            RelationshipPlanningResult lower = RelationshipTestFixtures.Planner().Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.Valerius,
                    -25f,
                    RelationshipTestFixtures.CorrelationId,
                    "op-lower",
                    RelationshipTestFixtures.SourceSystemId),
                RelationshipTestFixtures.Snapshot(lowRaw));

            Assert.AreEqual(RelationshipPreparationStatus.PreparedClamped, upper.Status);
            Assert.AreEqual(100d, upper.Plan.NewValue);
            Assert.AreEqual(10d, upper.Plan.AppliedDelta);
            Assert.AreEqual(25d, upper.Plan.RequestedDelta);
            Assert.AreEqual(RelationshipPreparationStatus.PreparedClamped, lower.Status);
            Assert.AreEqual(-100d, lower.Plan.NewValue);
        }

        [Test]
        public void NonFiniteAndUnknownAndMalformedAffinityReject()
        {
            RelationshipMutationPlanner planner = RelationshipTestFixtures.Planner();
            RelationshipSnapshot valid = RelationshipTestFixtures.Snapshot();
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedInvalidDelta,
                planner.Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        float.NaN,
                        RelationshipTestFixtures.CorrelationId,
                        "op-nan",
                        RelationshipTestFixtures.SourceSystemId),
                    valid).Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedInvalidDelta,
                planner.Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        float.PositiveInfinity,
                        RelationshipTestFixtures.CorrelationId,
                        "op-inf",
                        RelationshipTestFixtures.SourceSystemId),
                    valid).Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedUnknownId,
                planner.Plan(
                    RelationshipMutationRequest.Affinity(
                        "npc_future_unknown",
                        1f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-unknown",
                        RelationshipTestFixtures.SourceSystemId),
                    valid).Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedUnknownId,
                planner.Plan(
                    RelationshipMutationRequest.Affinity(
                        "npc_vaeloryn",
                        1f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-disabled",
                        RelationshipTestFixtures.SourceSystemId),
                    valid).Status);

            RelationshipRawState malformed = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[] { RelationshipNpcAffinityRow.NullEntry() });
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedMalformedDomain,
                planner.Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        1f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-malformed",
                        RelationshipTestFixtures.SourceSystemId),
                    RelationshipTestFixtures.Snapshot(malformed)).Status);
        }

        [Test]
        public void ExtremelyLargeFiniteDeltaProducesBoundedFiniteResult()
        {
            RelationshipPlanningResult huge = RelationshipTestFixtures.Planner().Plan(
                new RelationshipMutationRequest(
                    RelationshipDomain.NpcAffinity,
                    RelationshipTestFixtures.Valerius,
                    null,
                    double.MaxValue,
                    RelationshipTestFixtures.CorrelationId,
                    "op-huge",
                    RelationshipTestFixtures.SourceSystemId,
                    DateTime.UtcNow,
                    string.Empty),
                RelationshipTestFixtures.Snapshot());
            RelationshipPlanningResult nonFinite = RelationshipTestFixtures.Planner().Plan(
                new RelationshipMutationRequest(
                    RelationshipDomain.NpcAffinity,
                    RelationshipTestFixtures.Valerius,
                    null,
                    double.PositiveInfinity,
                    RelationshipTestFixtures.CorrelationId,
                    "op-inf-delta",
                    RelationshipTestFixtures.SourceSystemId,
                    DateTime.UtcNow,
                    string.Empty),
                RelationshipTestFixtures.Snapshot());

            Assert.AreEqual(RelationshipPreparationStatus.PreparedClamped, huge.Status);
            Assert.AreEqual(100d, huge.Plan.NewValue);
            Assert.IsTrue(float.IsFinite((float)huge.Plan.NewValue));
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedInvalidDelta,
                nonFinite.Status);
        }

        [Test]
        public void MissingCorrelationRejects()
        {
            RelationshipPlanningResult result = RelationshipTestFixtures.Planner().Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.Valerius,
                    1f,
                    string.Empty,
                    RelationshipTestFixtures.OperationId,
                    RelationshipTestFixtures.SourceSystemId),
                RelationshipTestFixtures.Snapshot());

            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedCorrelationRequired,
                result.Status);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void FactionCheckedArithmeticRejectsOverflow()
        {
            RelationshipRawState max = RelationshipRawState.EmptyWritable().WithFactionRows(
                new[]
                {
                    RelationshipFactionRow.Value(
                        RelationshipTestFixtures.VeilWatch,
                        int.MaxValue)
                });
            RelationshipRawState min = RelationshipRawState.EmptyWritable().WithFactionRows(
                new[]
                {
                    RelationshipFactionRow.Value(
                        RelationshipTestFixtures.VeilWatch,
                        int.MinValue)
                });
            RelationshipMutationPlanner planner = RelationshipTestFixtures.Planner();

            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedOverflow,
                planner.Plan(
                    RelationshipMutationRequest.Faction(
                        RelationshipTestFixtures.VeilWatch,
                        1,
                        RelationshipTestFixtures.CorrelationId,
                        "op-ovf",
                        RelationshipTestFixtures.SourceSystemId),
                    RelationshipTestFixtures.Snapshot(max)).Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedOverflow,
                planner.Plan(
                    RelationshipMutationRequest.Faction(
                        RelationshipTestFixtures.VeilWatch,
                        -1,
                        RelationshipTestFixtures.CorrelationId,
                        "op-und",
                        RelationshipTestFixtures.SourceSystemId),
                    RelationshipTestFixtures.Snapshot(min)).Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.Prepared,
                planner.Plan(
                    RelationshipMutationRequest.Faction(
                        RelationshipTestFixtures.VeilWatch,
                        -12,
                        RelationshipTestFixtures.CorrelationId,
                        "op-ok",
                        RelationshipTestFixtures.SourceSystemId),
                    RelationshipTestFixtures.Snapshot()).Status);
        }

        [Test]
        public void PersonaUndefinedAndOverflowReject()
        {
            RelationshipMutationPlanner planner = RelationshipTestFixtures.Planner();
            RelationshipPlanningResult undefined = planner.Plan(
                new RelationshipMutationRequest(
                    RelationshipDomain.PersonaTrait,
                    string.Empty,
                    (PersonaTrait)99,
                    1d,
                    RelationshipTestFixtures.CorrelationId,
                    "op-undef",
                    RelationshipTestFixtures.SourceSystemId,
                    DateTime.UtcNow,
                    string.Empty),
                RelationshipTestFixtures.Snapshot());
            RelationshipRawState max = RelationshipRawState.EmptyWritable().WithPersona(
                RelationshipPersonaValues.From(int.MaxValue, 0, 0, 0));
            RelationshipPlanningResult overflow = planner.Plan(
                RelationshipMutationRequest.Persona(
                    PersonaTrait.Warlord,
                    1,
                    RelationshipTestFixtures.CorrelationId,
                    "op-povf",
                    RelationshipTestFixtures.SourceSystemId),
                RelationshipTestFixtures.Snapshot(max));

            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedInvalidTrait,
                undefined.Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedOverflow,
                overflow.Status);
        }

        [Test]
        public void ReadOnlyAndNoSaveRejectWithoutPlan()
        {
            RelationshipMutationPlanner planner = RelationshipTestFixtures.Planner();
            RelationshipSnapshot readOnly = RelationshipTestFixtures.Snapshot(
                RelationshipRawState.EmptyWritable().WithWritable(false));
            RelationshipSnapshot noSave = RelationshipTestFixtures.Snapshot(
                RelationshipRawState.NoSave());

            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedReadOnlyProfile,
                planner.Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        1f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-ro",
                        RelationshipTestFixtures.SourceSystemId),
                    readOnly).Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedNoCurrentSave,
                planner.Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        1f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-ns",
                        RelationshipTestFixtures.SourceSystemId),
                    noSave).Status);
        }

        [Test]
        public void StaleExpectedRevisionRejects()
        {
            RelationshipPlanningResult result = RelationshipTestFixtures.Planner().Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.Valerius,
                    1f,
                    RelationshipTestFixtures.CorrelationId,
                    "op-stale",
                    RelationshipTestFixtures.SourceSystemId,
                    "not-the-fingerprint"),
                RelationshipTestFixtures.Snapshot());

            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedStaleSnapshot,
                result.Status);
        }
    }
}
