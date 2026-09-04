using System;
using System.Collections.Generic;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;
using AL.Services.Relationships;
using NUnit.Framework;

namespace AL.Tests.EditMode.Relationships
{
    public class RelationshipStandaloneAdapterTests
    {
        [Test]
        public void SnapshotQueryAndPlanReadClonedCandidatesWithoutPersisting()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            RelationshipDurableService service = CreateService(persistence);
            RelationshipSnapshot snapshot = service.Snapshot();
            RelationshipQueryResult query = service.QueryNpcAffinity(
                RelationshipTestFixtures.Valerius);
            RelationshipPlanningResult planned = service.Plan(
                AffinityRequest("op-plan-only", 5f));

            Assert.AreEqual(
                RelationshipDomainValidationStatus.ValidSparse,
                snapshot.NpcAffinityDomain.Status);
            Assert.AreEqual(RelationshipQueryStatus.AvailableSparseZero, query.Status);
            Assert.AreEqual(0d, query.Value);
            Assert.AreEqual(RelationshipPreparationStatus.Prepared, planned.Status);
            Assert.AreEqual(0, persistence.AttemptCount);
            Assert.AreEqual(
                0,
                service.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
        }

        [Test]
        public void StandaloneValidMutationPersistsOnceAndPublishes()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            var events = new RecordingRelationshipCommitEventSink();
            RelationshipDurableService service = CreateService(persistence, events: events);

            RelationshipStandaloneCommitResult committed = service.Commit(
                AffinityRequest("op-persist-once", 5f));

            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.AppliedCommitted,
                committed.Status);
            Assert.AreEqual(1, committed.PersistAttemptCount);
            Assert.AreEqual(1, persistence.AttemptCount);
            Assert.AreEqual(
                5d,
                service.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
            Assert.AreEqual(1, events.Published.Count);
            Assert.AreEqual(5d, events.Published[0].NewValue);
            Assert.AreEqual(5d, events.Published[0].AppliedDelta);
            Assert.IsFalse(events.Published[0].WasClamped);
        }

        [Test]
        public void ZeroAndRejectedOperationsSaveZeroTimes()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            RelationshipDurableService service = CreateService(persistence);

            RelationshipStandaloneCommitResult zero = service.Commit(
                AffinityRequest("op-zero", 0f));
            RelationshipStandaloneCommitResult unknown = service.Commit(
                RelationshipMutationRequest.Affinity(
                    "not_a_catalog_npc",
                    5f,
                    "corr-unknown",
                    "op-unknown",
                    RelationshipTestFixtures.SourceSystemId));

            Assert.AreEqual(RelationshipStandaloneCommitStatus.NoChange, zero.Status);
            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.RejectedValidation,
                unknown.Status);
            Assert.AreEqual(0, zero.PersistAttemptCount);
            Assert.AreEqual(0, unknown.PersistAttemptCount);
            Assert.AreEqual(0, persistence.AttemptCount);
            Assert.AreEqual(
                0d,
                service.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
        }

        [Test]
        public void PersistenceFailurePreservesPriorPublishedState()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence
            {
                FailNext = true
            };
            var events = new RecordingRelationshipCommitEventSink();
            RelationshipDurableService service = CreateService(persistence, events: events);

            RelationshipStandaloneCommitResult failed = service.Commit(
                AffinityRequest("op-persist-fail", 5f));

            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.PersistenceFailedPreviousPreserved,
                failed.Status);
            Assert.AreEqual(1, failed.PersistAttemptCount);
            Assert.AreEqual(1, persistence.AttemptCount);
            Assert.AreEqual(
                0d,
                service.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
            Assert.AreEqual(0, events.Published.Count);
        }

        [Test]
        public void ReloadPreservesExactCommittedValues()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            RelationshipDurableService first = CreateService(persistence);
            first.Commit(AffinityRequest("op-reload", 5f));

            var reloaded = new RelationshipDurableService(
                RelationshipTestFixtures.Identities(),
                RelationshipTestFixtures.Policies(),
                persistence,
                new InMemoryRelationshipOperationLedger(),
                new RecordingRelationshipCommitEventSink(),
                null,
                RelationshipRawState.EmptyWritable());
            reloaded.Reload();

            Assert.AreEqual(
                5d,
                reloaded.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
            Assert.AreEqual(
                first.Snapshot().NpcAffinityDomain.Fingerprint,
                reloaded.Snapshot().NpcAffinityDomain.Fingerprint);
        }

        [Test]
        public void CommitEventFiresOnceAfterPersistenceAndIsolatesSubscriberFailure()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            var events = new RecordingRelationshipCommitEventSink();
            events.Subscribers.Add(_ => throw new InvalidOperationException("subscriber"));
            RelationshipDurableService service = CreateService(persistence, events: events);

            RelationshipStandaloneCommitResult committed = service.Commit(
                AffinityRequest("op-event-once", 5f));

            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.AppliedCommitted,
                committed.Status);
            Assert.AreEqual(1, events.Published.Count);
            Assert.AreEqual(
                5d,
                service.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
            Assert.IsTrue(
                HasCode(committed.Diagnostics, RelationshipDiagnosticCodes.EventHandler));
        }

        [Test]
        public void NotificationEnqueueFailureDoesNotChangeCommittedResult()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            var events = new RecordingRelationshipCommitEventSink();
            var outbox = new FailingRelationshipNotificationOutbox();
            RelationshipDurableService service = CreateService(
                persistence,
                events: events,
                notifications: outbox);

            RelationshipStandaloneCommitResult committed = service.Commit(
                AffinityRequest("op-notify-fail", 5f));

            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.NotificationFailedAfterCommit,
                committed.Status);
            Assert.AreEqual(1, committed.PersistAttemptCount);
            Assert.AreEqual(
                5d,
                service.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
            Assert.AreEqual(1, events.Published.Count);
            Assert.IsNotNull(committed.CommittedChange);
            Assert.IsTrue(
                HasCode(committed.Diagnostics, RelationshipDiagnosticCodes.Notification));
        }

        [Test]
        public void UnknownFutureRowsArePreservedAndNotRepaired()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value("future_unknown_npc", 12f)
                });
            RelationshipDurableService service = CreateService(persistence, raw);

            RelationshipStandaloneCommitResult committed = service.Commit(
                AffinityRequest("op-preserve-unknown", 5f));

            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.AppliedCommitted,
                committed.Status);
            Assert.AreEqual(
                5d,
                service.QueryNpcAffinity(RelationshipTestFixtures.Valerius).Value);
            CollectionAssert.Contains(
                service.Snapshot().NpcAffinityDomain.PreservedUnknownIds,
                "future_unknown_npc");
            RelationshipRawState published = persistence.LoadPublished();
            Assert.AreEqual(2, published.NpcAffinityRows.Count);
            Assert.AreEqual("future_unknown_npc", published.NpcAffinityRows[0].NpcId);
            Assert.AreEqual(12f, published.NpcAffinityRows[0].Affinity);
            Assert.AreEqual(RelationshipTestFixtures.Valerius, published.NpcAffinityRows[1].NpcId);
            Assert.AreEqual(5f, published.NpcAffinityRows[1].Affinity);
        }

        [Test]
        public void DuplicateMalformedDomainRejectsWithoutPersistingOrRepairing()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            RelationshipRawState raw = RelationshipRawState.EmptyWritable().WithNpcRows(
                new[]
                {
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, 4f),
                    RelationshipNpcAffinityRow.Value(RelationshipTestFixtures.Valerius, 9f)
                });
            RelationshipDurableService service = CreateService(persistence, raw);

            RelationshipStandaloneCommitResult committed = service.Commit(
                AffinityRequest("op-duplicate", 5f));

            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.RejectedValidation,
                committed.Status);
            Assert.AreEqual(0, persistence.AttemptCount);
            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedDuplicateId,
                service.Snapshot().NpcAffinityDomain.Status);
        }

        [Test]
        public void LegacyWrappersMapOntoStandaloneAdapterUntilRemoved()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            RelationshipDurableService service = CreateService(persistence);
            var legacy = new RelationshipLegacyCompatibilityAdapter(service);

            legacy.ChangeAffinity(RelationshipTestFixtures.Valerius, 5f);
            legacy.AdjustReputation(RelationshipTestFixtures.VeilWatch, 3);
            legacy.AdjustTrait(PersonaTrait.Diplomat, 2);
            int savesAfterValid = persistence.AttemptCount;
            legacy.ChangeAffinity(RelationshipTestFixtures.Valerius, 0f);

            Assert.AreEqual(3, savesAfterValid);
            Assert.AreEqual(3, persistence.AttemptCount);
            Assert.AreEqual(5f, legacy.GetAffinity(RelationshipTestFixtures.Valerius));
            Assert.AreEqual(3, legacy.GetReputation(RelationshipTestFixtures.VeilWatch));
            Assert.AreEqual(2, legacy.GetTraitValue(PersonaTrait.Diplomat));
            Assert.AreEqual("Neutral", legacy.GetAffinityRank(RelationshipTestFixtures.Valerius));
            Assert.AreEqual(
                "Neutral",
                legacy.GetFactionAffiliation(RelationshipTestFixtures.VeilWatch));
            Assert.AreEqual(PersonaTrait.Diplomat, legacy.GetDominantTrait());
        }

        [Test]
        public void Issue183PendingCatalogRefusesDurableCommit()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            var service = new RelationshipDurableService(
                RelationshipTestFixtures.Identities(RelationshipCatalogAvailability.Pending),
                RelationshipTestFixtures.Policies(RelationshipCatalogAvailability.Pending),
                persistence,
                new InMemoryRelationshipOperationLedger(),
                new RecordingRelationshipCommitEventSink(),
                null,
                RelationshipRawState.EmptyWritable());

            RelationshipStandaloneCommitResult committed = service.Commit(
                AffinityRequest("op-pending-durable", 5f));

            Assert.AreEqual(
                RelationshipStandaloneCommitStatus.RejectedValidation,
                committed.Status);
            Assert.AreEqual(0, persistence.AttemptCount);
        }

        [Test]
        public void DurableServiceStaysUnregisteredInBootloader()
        {
            string bootloader = RelationshipTestFixtures.BootloaderSource();
            string gameData = RelationshipTestFixtures.GameDataServiceSource();

            Assert.IsFalse(bootloader.Contains("RelationshipDurableService"));
            Assert.IsFalse(bootloader.Contains("RelationshipLegacyCompatibilityAdapter"));
            Assert.IsFalse(bootloader.Contains("IRelationshipCandidatePersistence"));
            Assert.IsFalse(gameData.Contains("RelationshipDurableService"));
        }

        [Test]
        public void PersistOnceSabotageProofRequiresSingleVerifiedWrite()
        {
            var persistence = new InMemoryRelationshipCandidatePersistence();
            RelationshipDurableService service = CreateService(persistence);

            service.Commit(AffinityRequest("op-sabotage-once", 5f));

            Assert.AreEqual(1, persistence.AttemptCount);
        }

        private static RelationshipDurableService CreateService(
            InMemoryRelationshipCandidatePersistence persistence,
            RelationshipRawState raw = null,
            RecordingRelationshipCommitEventSink events = null,
            IRelationshipNotificationOutbox notifications = null)
        {
            return new RelationshipDurableService(
                RelationshipTestFixtures.Identities(),
                RelationshipTestFixtures.Policies(),
                persistence,
                new InMemoryRelationshipOperationLedger(),
                events ?? new RecordingRelationshipCommitEventSink(),
                notifications,
                raw ?? RelationshipRawState.EmptyWritable());
        }

        private static RelationshipMutationRequest AffinityRequest(
            string operationId,
            float delta)
        {
            return RelationshipMutationRequest.Affinity(
                RelationshipTestFixtures.Valerius,
                delta,
                "corr-" + operationId,
                operationId,
                RelationshipTestFixtures.SourceSystemId);
        }

        private static bool HasCode(
            IReadOnlyList<RelationshipDiagnostic> diagnostics,
            string code)
        {
            foreach (RelationshipDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic != null &&
                    string.Equals(diagnostic.Code, code, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class FailingRelationshipNotificationOutbox : IRelationshipNotificationOutbox
    {
        public bool TryEnqueue(RelationshipCommittedChange committed, out string diagnostic)
        {
            diagnostic = "Notification outbox refused the enqueue.";
            return false;
        }
    }
}
