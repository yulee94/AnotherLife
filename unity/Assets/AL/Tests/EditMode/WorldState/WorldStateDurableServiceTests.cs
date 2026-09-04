using System;
using AL.Core.Interfaces;
using AL.Core.Interfaces.WorldState;
using AL.Services.Local;
using AL.Services.WorldState;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.WorldState
{
    public class WorldStateDurableServiceTests
    {
        private const long NowUtcSeconds = 1800000000L;
        private const long DurationSeconds = 600L;
        private const string InstanceId = "world-instance-veil-omen-001";
        private const string StartCorrelationId = "world-correlation-veil-start-001";
        private const string StartOperationId = "world-operation-veil-start-001";
        private const string EndCorrelationId = "world-correlation-veil-end-001";
        private const string EndOperationId = "world-operation-veil-end-001";

        [Test]
        public void VeilOmenStartPersistsOncePreparesConsumerAndNotifiesAfterCommit()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            var events = new RecordingWorldStateCommitEventSink();
            var outbox = new RecordingWorldStateNotificationOutbox();
            WorldStateDurableService service = CreateService(persistence, events, outbox);

            WorldStateStandaloneCommitResult committed = service.CommitStart(StartRequest());

            Assert.AreEqual(WorldStateStandaloneCommitStatus.AppliedCommitted, committed.Status);
            Assert.AreEqual(1, committed.PersistAttemptCount);
            Assert.AreEqual(1, persistence.AttemptCount);
            Assert.AreEqual(WorldStateSnapshotStatus.AvailableActive, service.Snapshot().Status);
            Assert.AreEqual(
                WorldStateAuthoredCatalog.VeilOmenDefinitionId,
                service.Snapshot().ActiveInstance.DefinitionId);
            Assert.AreEqual(1, events.Published.Count);
            Assert.AreEqual(WorldStateTransitionKind.Start, events.Published[0].TransitionKind);
            Assert.AreEqual(1, outbox.Enqueued.Count);
            Assert.AreEqual(
                WorldStateAuthoredCatalog.StartNotificationId,
                outbox.Enqueued[0].DefinitionId);
            Assert.AreEqual(1, committed.Plan.PreparedEffectPlans.Count);
            Assert.AreEqual(
                WorldStateAuthoredCatalog.PresentationConsumerId,
                committed.Plan.PreparedEffectPlans[0].ConsumerId);
        }

        [Test]
        public void ExactStartReplayDoesNotPersistOrNotifyAgain()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            var events = new RecordingWorldStateCommitEventSink();
            var outbox = new RecordingWorldStateNotificationOutbox();
            WorldStateDurableService service = CreateService(persistence, events, outbox);
            service.CommitStart(StartRequest());

            WorldStateStandaloneCommitResult replay = service.CommitStart(StartRequest());

            Assert.AreEqual(WorldStateStandaloneCommitStatus.AlreadyCommitted, replay.Status);
            Assert.AreEqual(0, replay.PersistAttemptCount);
            Assert.AreEqual(1, persistence.AttemptCount);
            Assert.AreEqual(1, events.Published.Count);
            Assert.AreEqual(1, outbox.Enqueued.Count);
            Assert.AreEqual(
                WorldStateAuthoredCatalog.VeilOmenDefinitionId,
                service.Snapshot().ActiveInstance.DefinitionId);
        }

        [Test]
        public void StaleStartRevisionRejectsWithoutPersisting()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            WorldStateDurableService service = CreateService(persistence);

            WorldStateStandaloneCommitResult stale = service.CommitStart(
                StartRequest(expectedRevision: 9L));

            Assert.AreEqual(WorldStateStandaloneCommitStatus.RejectedStale, stale.Status);
            Assert.AreEqual(0, stale.PersistAttemptCount);
            Assert.AreEqual(0, persistence.AttemptCount);
            Assert.AreEqual(
                WorldStateSnapshotStatus.AvailableNoActiveEvent,
                service.Snapshot().Status);
        }

        [Test]
        public void PersistenceFailurePreservesPriorPublishedState()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence { FailNext = true };
            var events = new RecordingWorldStateCommitEventSink();
            var outbox = new RecordingWorldStateNotificationOutbox();
            WorldStateDurableService service = CreateService(persistence, events, outbox);

            WorldStateStandaloneCommitResult failed = service.CommitStart(StartRequest());

            Assert.AreEqual(
                WorldStateStandaloneCommitStatus.PersistenceFailedPreviousPreserved,
                failed.Status);
            Assert.AreEqual(1, failed.PersistAttemptCount);
            Assert.AreEqual(
                WorldStateSnapshotStatus.AvailableNoActiveEvent,
                service.Snapshot().Status);
            Assert.AreEqual(0, events.Published.Count);
            Assert.AreEqual(0, outbox.Enqueued.Count);
        }

        [Test]
        public void VeilOmenEndPersistsOnceAndNotifiesAfterNaturalExpiry()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            var events = new RecordingWorldStateCommitEventSink();
            var outbox = new RecordingWorldStateNotificationOutbox();
            var clock = new MutableClock(NowUtcSeconds);
            WorldStateDurableService service = CreateService(
                persistence,
                events,
                outbox,
                clock);
            service.CommitStart(StartRequest());
            clock.UtcNowSeconds = NowUtcSeconds + DurationSeconds;

            WorldStateStandaloneCommitResult ended = service.CommitEnd(EndRequest(clock.UtcNowSeconds, 1L));

            Assert.AreEqual(WorldStateStandaloneCommitStatus.AppliedCommitted, ended.Status);
            Assert.AreEqual(1, ended.PersistAttemptCount);
            Assert.AreEqual(2, persistence.AttemptCount);
            Assert.AreEqual(
                WorldStateSnapshotStatus.AvailableNoActiveEvent,
                service.Snapshot().Status);
            Assert.AreEqual(1, service.Snapshot().CompletedHistory.Count);
            Assert.AreEqual(
                WorldEventInstanceState.Ended,
                service.Snapshot().CompletedHistory[0].State);
            Assert.AreEqual(2, events.Published.Count);
            Assert.AreEqual(WorldStateTransitionKind.End, events.Published[1].TransitionKind);
            Assert.AreEqual(2, outbox.Enqueued.Count);
            Assert.AreEqual(
                WorldStateAuthoredCatalog.EndNotificationId,
                outbox.Enqueued[1].DefinitionId);
        }

        [Test]
        public void UnauthoredEventsStayUnavailable()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            WorldStateDurableService service = CreateService(persistence);
            string[] unavailable =
            {
                "al_world_event_siege",
                "al_world_event_festival",
                "al_world_event_void_corruption"
            };

            foreach (string definitionId in unavailable)
            {
                WorldStateStandaloneCommitResult rejected = service.CommitStart(
                    new WorldStateStartRequest(
                        definitionId,
                        InstanceId,
                        StartCorrelationId,
                        StartOperationId,
                        WorldStateAuthoredCatalog.SourceSystemId,
                        NowUtcSeconds,
                        DurationSeconds,
                        0L));
                Assert.AreEqual(
                    WorldStateStandaloneCommitStatus.RejectedValidation,
                    rejected.Status,
                    definitionId);
            }

            Assert.AreEqual(0, persistence.AttemptCount);
            Assert.AreEqual(
                WorldStateSnapshotStatus.AvailableNoActiveEvent,
                service.Snapshot().Status);
        }

        [Test]
        public void SchemaTwoSaveWithoutWorldStateLoadsAsNoActiveEvent()
        {
            const string legacyJson =
                "{\"SaveFormatId\":\"anotherlife.local-save\",\"SaveSchemaVersion\":2}";
            AL.Data.Runtime.SaveGameData legacy =
                JsonUtility.FromJson<AL.Data.Runtime.SaveGameData>(legacyJson);
            Assert.IsTrue(
                legacy.WorldState == null || legacy.WorldState.Version == 0);

            var persistence = new InMemoryWorldStateCandidatePersistence();
            WorldStateDurableService service = CreateService(
                persistence,
                initial: WorldStatePersistentMapper.FromSave(legacy));

            Assert.AreEqual(
                WorldStateSnapshotStatus.AvailableNoActiveEvent,
                service.Snapshot().Status);
            Assert.AreEqual(0L, service.Snapshot().SnapshotRevision);
            Assert.AreEqual(0, persistence.AttemptCount);
            Assert.AreEqual(0, service.Snapshot().ActiveInstances.Count);
        }

        [Test]
        public void ReloadPreservesCommittedVeilOmen()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            WorldStateDurableService first = CreateService(persistence);
            first.CommitStart(StartRequest());

            var reloaded = new WorldStateDurableService(
                WorldStateAuthoredCatalog.CreateResolver(),
                new MutableClock(NowUtcSeconds),
                new WorldEffectConsumerRegistry(
                    new[] { WorldStateAuthoredCatalog.CreatePresentationConsumer() }),
                persistence,
                new RecordingWorldStateCommitEventSink(),
                new RecordingWorldStateNotificationOutbox(),
                WorldStatePersistentMapper.Empty());
            reloaded.Reload();

            Assert.AreEqual(
                WorldStateAuthoredCatalog.VeilOmenDefinitionId,
                reloaded.Snapshot().ActiveInstance.DefinitionId);
            Assert.AreEqual(
                first.Snapshot().SnapshotRevision,
                reloaded.Snapshot().SnapshotRevision);
        }

        [Test]
        public void NotificationFailureDoesNotRollBackCommittedStart()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            var events = new RecordingWorldStateCommitEventSink();
            var outbox = new RecordingWorldStateNotificationOutbox { FailNext = true };
            WorldStateDurableService service = CreateService(persistence, events, outbox);

            WorldStateStandaloneCommitResult committed = service.CommitStart(StartRequest());

            Assert.AreEqual(
                WorldStateStandaloneCommitStatus.NotificationFailedAfterCommit,
                committed.Status);
            Assert.AreEqual(1, committed.PersistAttemptCount);
            Assert.AreEqual(
                WorldStateAuthoredCatalog.VeilOmenDefinitionId,
                service.Snapshot().ActiveInstance.DefinitionId);
            Assert.AreEqual(1, events.Published.Count);
        }

        [Test]
        public void SubscriberFailureIsIsolatedAfterCommit()
        {
            var persistence = new InMemoryWorldStateCandidatePersistence();
            var events = new RecordingWorldStateCommitEventSink();
            events.Subscribers.Add(_ => throw new InvalidOperationException("subscriber"));
            WorldStateDurableService service = CreateService(persistence, events);

            WorldStateStandaloneCommitResult committed = service.CommitStart(StartRequest());

            Assert.AreEqual(WorldStateStandaloneCommitStatus.AppliedCommitted, committed.Status);
            Assert.AreEqual(1, events.Published.Count);
            Assert.AreEqual(
                WorldStateAuthoredCatalog.VeilOmenDefinitionId,
                service.Snapshot().ActiveInstance.DefinitionId);
            Assert.IsTrue(
                Array.Exists(
                    System.Linq.Enumerable.ToArray(committed.Diagnostics),
                    item => item.Code == "AL-WST-EVENT-HANDLER"));
        }

        [Test]
        public void ProductionWorldStateServiceHasNoHardCodedCopyOrRawNotify()
        {
            string path = System.IO.Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Kingdom",
                "Narrative",
                "WorldStateService.cs");
            string source = System.IO.File.ReadAllText(path);
            StringAssert.DoesNotContain("THE CITY IS UNDER SIEGE", source);
            StringAssert.DoesNotContain("A REALM FESTIVAL HAS BEGUN", source);
            StringAssert.DoesNotContain("A DARK OMEN APPEARS", source);
            StringAssert.DoesNotContain("VOID CORRUPTION IS SPREADING", source);
            StringAssert.DoesNotContain("ShowMessage", source);

            var service = new WorldStateService(null, null);
            service.TriggerStateChange("world_event_siege", WorldStateEffect.Siege, 4f);
            Assert.AreEqual(WorldStateEffect.None, service.CurrentEffect);
            Assert.AreEqual(string.Empty, service.ActiveEventId);
            Assert.AreEqual(1.0f, service.GetProductionMultiplier());
        }

        private static WorldStateDurableService CreateService(
            InMemoryWorldStateCandidatePersistence persistence,
            RecordingWorldStateCommitEventSink events = null,
            RecordingWorldStateNotificationOutbox outbox = null,
            MutableClock clock = null,
            AL.Data.Runtime.WorldStatePersistentState initial = null)
        {
            return new WorldStateDurableService(
                WorldStateAuthoredCatalog.CreateResolver(),
                clock ?? new MutableClock(NowUtcSeconds),
                new WorldEffectConsumerRegistry(
                    new[] { WorldStateAuthoredCatalog.CreatePresentationConsumer() }),
                persistence,
                events ?? new RecordingWorldStateCommitEventSink(),
                outbox ?? new RecordingWorldStateNotificationOutbox(),
                initial ?? WorldStatePersistentMapper.Empty());
        }

        private static WorldStateStartRequest StartRequest(long expectedRevision = 0L)
        {
            return new WorldStateStartRequest(
                WorldStateAuthoredCatalog.VeilOmenDefinitionId,
                InstanceId,
                StartCorrelationId,
                StartOperationId,
                WorldStateAuthoredCatalog.SourceSystemId,
                NowUtcSeconds,
                DurationSeconds,
                expectedRevision);
        }

        private static WorldStateEndRequest EndRequest(long now, long expectedRevision)
        {
            return new WorldStateEndRequest(
                InstanceId,
                EndCorrelationId,
                EndOperationId,
                WorldStateAuthoredCatalog.SourceSystemId,
                now,
                expectedRevision);
        }

        private sealed class MutableClock : IWorldStateClock
        {
            public MutableClock(long nowUtcSeconds)
            {
                UtcNowSeconds = nowUtcSeconds;
            }

            public long UtcNowSeconds { get; set; }
        }
    }
}
