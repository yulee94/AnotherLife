using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;
using AL.Services.Relationships;
using NUnit.Framework;

namespace AL.Tests.EditMode.Relationships
{
    public class RelationshipPlanApplierTests
    {
        [Test]
        public void ValidPlanAppliesToFakeCandidateWithoutSaveOrEvent()
        {
            InjectedRelationshipIdentityResolver identities =
                RelationshipTestFixtures.Identities();
            InjectedRelationshipPolicyResolver policies = RelationshipTestFixtures.Policies();
            var target = new InMemoryRelationshipMutationTarget(
                RelationshipRawState.EmptyWritable());
            var ledger = new InMemoryRelationshipOperationLedger();
            RelationshipSnapshot before = target.GetCurrentSnapshot(identities, policies);
            RelationshipPlanningResult planned = RelationshipTestFixtures.Planner(
                    identities,
                    policies)
                .Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        5f,
                        RelationshipTestFixtures.CorrelationId,
                        RelationshipTestFixtures.OperationId,
                        RelationshipTestFixtures.SourceSystemId),
                    before);

            RelationshipApplyResult applied = target.Apply(
                planned.Plan,
                identities,
                policies,
                ledger);

            Assert.AreEqual(RelationshipApplyStatus.Applied, applied.Status);
            Assert.AreEqual(
                5f,
                applied.SnapshotAfter.NpcAffinityDomain.SupportedValuesByCanonicalNpcId[
                    RelationshipTestFixtures.Valerius]);
            Assert.AreEqual(
                0,
                before.NpcAffinityDomain.SupportedValuesByCanonicalNpcId.Count);
            Assert.AreNotEqual(
                before.NpcAffinityDomain.Fingerprint,
                applied.SnapshotAfter.NpcAffinityDomain.Fingerprint);
        }

        [Test]
        public void StaleRevisionAndChangedValueRejectAndLeaveCandidateUnchanged()
        {
            InjectedRelationshipIdentityResolver identities =
                RelationshipTestFixtures.Identities();
            InjectedRelationshipPolicyResolver policies = RelationshipTestFixtures.Policies();
            var target = new InMemoryRelationshipMutationTarget(
                RelationshipRawState.EmptyWritable());
            RelationshipPreparedPlan plan = RelationshipTestFixtures.Planner(
                    identities,
                    policies)
                .Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        5f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-stale-apply",
                        RelationshipTestFixtures.SourceSystemId),
                    target.GetCurrentSnapshot(identities, policies)).Plan;

            target.Apply(
                plan,
                identities,
                policies,
                new InMemoryRelationshipOperationLedger());
            string afterFirst = target.GetCurrentSnapshot(identities, policies)
                .NpcAffinityDomain.Fingerprint;

            RelationshipApplyResult stale = target.Apply(
                plan,
                identities,
                policies,
                new InMemoryRelationshipOperationLedger());

            Assert.AreEqual(RelationshipApplyStatus.RejectedStalePlan, stale.Status);
            Assert.AreEqual(
                afterFirst,
                target.GetCurrentSnapshot(identities, policies).NpcAffinityDomain.Fingerprint);
        }

        [Test]
        public void DuplicateOperationReturnsAlreadyAppliedThroughFakeLedger()
        {
            InjectedRelationshipIdentityResolver identities =
                RelationshipTestFixtures.Identities();
            InjectedRelationshipPolicyResolver policies = RelationshipTestFixtures.Policies();
            var target = new InMemoryRelationshipMutationTarget(
                RelationshipRawState.EmptyWritable());
            var ledger = new InMemoryRelationshipOperationLedger();
            RelationshipPreparedPlan plan = RelationshipTestFixtures.Planner(
                    identities,
                    policies)
                .Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        5f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-dup",
                        RelationshipTestFixtures.SourceSystemId),
                    target.GetCurrentSnapshot(identities, policies)).Plan;

            Assert.AreEqual(
                RelationshipApplyStatus.Applied,
                target.Apply(plan, identities, policies, ledger).Status);
            RelationshipApplyResult duplicate = target.Apply(
                plan,
                identities,
                policies,
                ledger);

            Assert.AreEqual(
                RelationshipApplyStatus.RejectedAlreadyApplied,
                duplicate.Status);
            Assert.AreEqual(
                5f,
                target.GetCurrentSnapshot(identities, policies)
                    .NpcAffinityDomain.SupportedValuesByCanonicalNpcId[
                        RelationshipTestFixtures.Valerius]);
        }

        [Test]
        public void CorrelationConflictAndPolicyChangeReject()
        {
            InjectedRelationshipIdentityResolver identities =
                RelationshipTestFixtures.Identities();
            InjectedRelationshipPolicyResolver policies = RelationshipTestFixtures.Policies();
            var target = new InMemoryRelationshipMutationTarget(
                RelationshipRawState.EmptyWritable());
            var ledger = new InMemoryRelationshipOperationLedger();
            RelationshipMutationPlanner planner = RelationshipTestFixtures.Planner(
                identities,
                policies);
            RelationshipSnapshot snapshot = target.GetCurrentSnapshot(identities, policies);
            RelationshipPreparedPlan first = planner.Plan(
                RelationshipMutationRequest.Affinity(
                    RelationshipTestFixtures.Valerius,
                    5f,
                    "corr-shared",
                    "op-one",
                    RelationshipTestFixtures.SourceSystemId),
                snapshot).Plan;
            Assert.AreEqual(
                RelationshipApplyStatus.Applied,
                target.Apply(first, identities, policies, ledger).Status);

            var otherTarget = new InMemoryRelationshipMutationTarget(
                RelationshipRawState.EmptyWritable());
            RelationshipPreparedPlan second = planner.Plan(
                RelationshipMutationRequest.Faction(
                    RelationshipTestFixtures.VeilWatch,
                    1,
                    "corr-shared",
                    "op-two",
                    RelationshipTestFixtures.SourceSystemId),
                otherTarget.GetCurrentSnapshot(identities, policies)).Plan;
            RelationshipApplyResult conflict = otherTarget.Apply(
                second,
                identities,
                policies,
                ledger);
            Assert.AreEqual(
                RelationshipApplyStatus.RejectedCorrelationConflict,
                conflict.Status);
            Assert.AreEqual(
                0,
                otherTarget.GetCurrentSnapshot(identities, policies)
                    .FactionDomain.SupportedValuesByCanonicalFactionId.Count);

            RelationshipPolicySnapshot drifted =
                InjectedRelationshipPolicyResolver.CreateLegacyFixturePolicy(
                    "drifted-identity");
            drifted = new RelationshipPolicySnapshot(
                drifted.SchemaVersion,
                drifted.ContentVersion,
                drifted.SourceRevision,
                drifted.IdentityCatalogRevision,
                "drifted-policy",
                drifted.AffinityMinimum,
                drifted.AffinityMaximum,
                drifted.AffinityBands,
                drifted.FactionBands,
                drifted.SupportedPersonaTraits,
                drifted.PersonaUniqueContentReference,
                drifted.PersonaTieContentReference,
                drifted.PersonaAllZeroContentReference,
                drifted.PersonaUnavailableContentReference,
                drifted.PersonaMalformedContentReference);
            RelationshipApplyResult policy = otherTarget.Apply(
                second,
                identities,
                new InjectedRelationshipPolicyResolver(
                    RelationshipCatalogAvailability.Available,
                    drifted),
                new InMemoryRelationshipOperationLedger());
            Assert.AreEqual(RelationshipApplyStatus.RejectedStalePlan, policy.Status);
        }

        [Test]
        public void PersonaApplyUpdatesIsolatedCandidateOnly()
        {
            InjectedRelationshipIdentityResolver identities =
                RelationshipTestFixtures.Identities();
            InjectedRelationshipPolicyResolver policies = RelationshipTestFixtures.Policies();
            var target = new InMemoryRelationshipMutationTarget(
                RelationshipRawState.EmptyWritable());
            RelationshipPreparedPlan plan = RelationshipTestFixtures.Planner(
                    identities,
                    policies)
                .Plan(
                    RelationshipMutationRequest.Persona(
                        PersonaTrait.Diplomat,
                        3,
                        RelationshipTestFixtures.CorrelationId,
                        "op-persona",
                        RelationshipTestFixtures.SourceSystemId),
                    target.GetCurrentSnapshot(identities, policies)).Plan;

            RelationshipApplyResult applied = target.Apply(
                plan,
                identities,
                policies,
                new InMemoryRelationshipOperationLedger());

            Assert.AreEqual(RelationshipApplyStatus.Applied, applied.Status);
            Assert.AreEqual(3, applied.SnapshotAfter.PersonaDomain.Values.Diplomat);
            Assert.AreEqual(
                PersonaClassificationStatus.UniqueDominant,
                applied.SnapshotAfter.PersonaDomain.Classification.Status);
            Assert.AreEqual(
                PersonaTrait.Diplomat,
                applied.SnapshotAfter.PersonaDomain.Classification.DominantTrait);
        }

        [Test]
        public void MalformedTargetRejects()
        {
            InjectedRelationshipIdentityResolver identities =
                RelationshipTestFixtures.Identities();
            InjectedRelationshipPolicyResolver policies = RelationshipTestFixtures.Policies();
            var target = new InMemoryRelationshipMutationTarget(
                RelationshipRawState.EmptyWritable().WithNpcRows(
                    new[] { RelationshipNpcAffinityRow.NullEntry() }));
            RelationshipPreparedPlan validPlan = RelationshipTestFixtures.Planner(
                    identities,
                    policies)
                .Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        1f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-mal-target",
                        RelationshipTestFixtures.SourceSystemId),
                    RelationshipTestFixtures.Snapshot()).Plan;

            RelationshipApplyResult result = target.Apply(
                validPlan,
                identities,
                policies,
                new InMemoryRelationshipOperationLedger());

            Assert.AreEqual(RelationshipApplyStatus.RejectedStalePlan, result.Status);
        }
    }
}
