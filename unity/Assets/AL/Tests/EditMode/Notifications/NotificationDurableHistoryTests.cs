using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Notifications;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Notifications
{
    [TestFixture]
    public sealed class NotificationDurableHistoryTests
    {
        [Test]
        public void OldSaveWithoutNotificationHistoryLoadsEmptyStore()
        {
            SaveGameData save = JsonUtility.FromJson<SaveGameData>(
                "{\"SelectedRealm\":1,\"WarzoneCredits\":0}");
            var store = new SaveGameNotificationDurableStore(() => save);

            Assert.True(
                save.NotificationHistory == null ||
                save.NotificationHistory.Version == 0);
            Assert.IsTrue(store.IsAvailable);
            Assert.AreEqual(0, store.LoadAll().Count);
        }

        [Test]
        public void DurableEnqueueWithoutStoreStillRejected()
        {
            Fixture fixture = CreateFixture(DurableDefinition(), null);

            NotificationEnqueueResult result = fixture.Service.Enqueue(Request(fixture.Clock));

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedDurabilityUnavailable,
                result.Status);
            Assert.AreEqual("AL-NTF-PERSISTENCE", result.DiagnosticCode);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void DurableEnqueuePersistsSemanticRecordAndStripsNonPersistableParameters()
        {
            var store = new InMemoryNotificationDurableStore();
            Fixture fixture = CreateFixture(DurableDefinition(), store);

            NotificationEnqueueResult result = fixture.Service.Enqueue(
                Request(
                    fixture.Clock,
                    parameters: new[]
                    {
                        new NotificationParameter(
                            "note",
                            NotificationParameterValue.FromStableId("recovered_profile")),
                        new NotificationParameter(
                            "secret",
                            NotificationParameterValue.FromSafeDisplayText("hidden_diagnostic_token"))
                    }));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, result.Status);
            Assert.AreEqual(1, store.LoadAll().Count);
            NotificationDurableRecord persisted = store.LoadAll()[0];
            Assert.AreEqual("al_notify_save_recovered_backup", persisted.DefinitionId);
            Assert.AreEqual("test:correlation:1", persisted.CorrelationId);
            Assert.AreEqual(1, persisted.Parameters.Count);
            Assert.AreEqual("note", persisted.Parameters[0].Name);
            Assert.AreEqual("recovered_profile", persisted.Parameters[0].TextValue);
            Assert.IsFalse(
                persisted.Parameters.Any(parameter =>
                    parameter.Name == "secret" ||
                    (parameter.TextValue != null &&
                     parameter.TextValue.Contains("hidden_diagnostic_token"))));
            Assert.AreNotEqual(NotificationPrivacyClass.SensitiveTechnical, persisted.PrivacyClass);
        }

        [Test]
        public void SensitiveTechnicalDefinitionNeverEntersHistory()
        {
            var store = new InMemoryNotificationDurableStore();
            NotificationDefinition sensitive = DurableDefinition(
                privacy: NotificationPrivacyClass.SensitiveTechnical);
            Fixture fixture = CreateFixture(sensitive, store);

            NotificationEnqueueResult result = fixture.Service.Enqueue(Request(fixture.Clock));

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedDurabilityUnavailable,
                result.Status);
            Assert.AreEqual("AL-NTF-PRIVACY", result.DiagnosticCode);
            Assert.AreEqual(0, store.LoadAll().Count);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void ReloadHydratesUnacknowledgedDurableRecordAndReplayDoesNotDuplicate()
        {
            var store = new InMemoryNotificationDurableStore();
            Fixture first = CreateFixture(DurableDefinition(), store);
            NotificationEnqueueResult queued = first.Service.Enqueue(Request(first.Clock));
            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, queued.Status);

            Fixture reloaded = CreateFixture(DurableDefinition(), store);
            Assert.AreEqual(1, reloaded.Service.GetSnapshot().Records.Count);
            Assert.AreEqual(
                queued.NotificationInstanceId,
                reloaded.Service.GetSnapshot().Records[0].NotificationInstanceId);

            NotificationEnqueueResult replay = reloaded.Service.Enqueue(Request(reloaded.Clock));
            Assert.AreEqual(NotificationEnqueueStatus.AcceptedAlreadyPresent, replay.Status);
            Assert.AreEqual(queued.NotificationInstanceId, replay.NotificationInstanceId);
            Assert.AreEqual(1, store.LoadAll().Count);
        }

        [Test]
        public void QueueOwnedAcknowledgementPersistsAndCompletedRecordIsNotRehydrated()
        {
            var store = new InMemoryNotificationDurableStore();
            Fixture fixture = CreateFixture(DurableDefinition(), store);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("durable_ack");
            NotificationPresenterRegistrationToken token = fixture.Service.RegisterPresenter(
                presenter,
                new NotificationPresenterCapabilities(
                    new[] { NotificationChannel.Acknowledgement })).Token;
            fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);

            NotificationReceiptUpdateResult acknowledged = fixture.Service.Acknowledge(
                token,
                queued.NotificationInstanceId);

            Assert.AreEqual(NotificationReceiptUpdateStatus.Applied, acknowledged.Status);
            Assert.AreEqual(NotificationDeliveryState.Acknowledged, store.LoadAll()[0].State);
            Assert.Greater(store.LoadAll()[0].AcknowledgedAtUtcTicks, 0L);

            Fixture reloaded = CreateFixture(DurableDefinition(), store);
            Assert.IsEmpty(reloaded.Service.GetSnapshot().Records);
            Assert.AreEqual(
                NotificationEnqueueStatus.AcceptedAlreadyPresent,
                reloaded.Service.Enqueue(Request(reloaded.Clock)).Status);
        }

        [Test]
        public void PersistFailureFailsClosedWithoutQueueMutation()
        {
            var store = new InMemoryNotificationDurableStore { FailNextCommit = true };
            Fixture fixture = CreateFixture(DurableDefinition(), store);

            NotificationEnqueueResult result = fixture.Service.Enqueue(Request(fixture.Clock));

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedDurabilityUnavailable,
                result.Status);
            Assert.AreEqual(0, store.LoadAll().Count);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void RetentionPrunesOldestCompletedAndNeverPrunesUnacknowledgedRequired()
        {
            var store = new InMemoryNotificationDurableStore();
            for (int index = 0; index < NotificationDurableRecord.CompletedHistoryLimit + 2; index++)
            {
                var completed = new NotificationDurableRecord
                {
                    RecordId = "done_" + index.ToString("D3"),
                    DefinitionId = "al_notify_save_recovered_backup",
                    CorrelationId = "done:" + index.ToString("D3"),
                    OccurredAtUtcTicks = 1000L + index,
                    Parameters = new List<NotificationDurableParameter>(),
                    State = NotificationDeliveryState.Acknowledged,
                    RequiresAcknowledgement = true,
                    PrivacyClass = NotificationPrivacyClass.PublicGameplay,
                    DurabilityPolicy = NotificationDurabilityPolicy.DurableUntilAcknowledged
                };
                Assert.IsTrue(store.TryCommit(completed, out _));
            }

            var pending = new NotificationDurableRecord
            {
                RecordId = "pending_required",
                DefinitionId = "al_notify_save_recovered_backup",
                CorrelationId = "pending:required",
                OccurredAtUtcTicks = 1L,
                Parameters = new List<NotificationDurableParameter>(),
                State = NotificationDeliveryState.PendingPresenter,
                RequiresAcknowledgement = true,
                PrivacyClass = NotificationPrivacyClass.PublicGameplay,
                DurabilityPolicy = NotificationDurabilityPolicy.DurableUntilAcknowledged
            };
            Assert.IsTrue(store.TryCommit(pending, out _));

            IReadOnlyList<NotificationDurableRecord> records = store.LoadAll();
            Assert.AreEqual(NotificationDurableRecord.CompletedHistoryLimit + 1, records.Count);
            Assert.IsTrue(records.Any(item => item.RecordId == "pending_required"));
            Assert.IsFalse(records.Any(item => item.RecordId == "done_000"));
            Assert.IsTrue(records.Any(item => item.RecordId == "done_002"));
        }

        [Test]
        public void SaveBackedStoreRoundTripsThroughJsonUtilityAndClearsOnDelete()
        {
            var save = new SaveGameData();
            var store = new SaveGameNotificationDurableStore(() => save);
            Fixture fixture = CreateFixture(DurableDefinition(), store);
            Assert.AreEqual(
                NotificationEnqueueStatus.AcceptedPending,
                fixture.Service.Enqueue(Request(fixture.Clock)).Status);
            Assert.NotNull(save.NotificationHistory);
            Assert.AreEqual(1, save.NotificationHistory.Outbox.Count);

            SaveGameData loaded = JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(save));
            var reloaded = new SaveGameNotificationDurableStore(() => loaded);
            Assert.AreEqual(1, reloaded.LoadAll().Count);
            Assert.AreEqual(
                save.NotificationHistory.Outbox[0].RecordId,
                reloaded.LoadAll()[0].RecordId);

            reloaded.Clear();
            Assert.IsNull(loaded.NotificationHistory);
            Assert.AreEqual(0, reloaded.LoadAll().Count);
        }

        [Test]
        public void LegacyRawWrappersNeverEnterDurableHistory()
        {
            var store = new InMemoryNotificationDurableStore();
            Fixture fixture = CreateFixture(DurableDefinition(), store);

#pragma warning disable 0618
            fixture.Service.ShowMessage("raw path C:\\\\secrets\\\\save.json");
#pragma warning restore 0618

            Assert.AreEqual(0, store.LoadAll().Count);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        private static NotificationDefinition DurableDefinition(
            NotificationPrivacyClass privacy = NotificationPrivacyClass.PublicGameplay)
        {
            return new NotificationDefinition(
                "al_notify_save_recovered_backup",
                NotificationTechnicalLimits.CurrentDefinitionSchemaVersion,
                1,
                NotificationSeverity.Warning,
                NotificationCategory.SaveRecovery,
                NotificationChannel.Acknowledgement,
                new[] { NotificationChannel.Acknowledgement },
                NotificationAcknowledgementPolicy.Required,
                NotificationDurabilityPolicy.DurableUntilAcknowledged,
                new NotificationExpiryPolicy(NotificationExpiryMode.None, 0d, false),
                80,
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition,
                privacy,
                true,
                false,
                new[] { "al_source_save" },
                new[]
                {
                    new NotificationParameterDefinition(
                        "note",
                        NotificationParameterValueKind.StableId,
                        false,
                        256,
                        null,
                        null,
                        null,
                        null,
                        true,
                        NotificationPrivacyClass.PublicGameplay),
                    new NotificationParameterDefinition(
                        "secret",
                        NotificationParameterValueKind.SafeDisplayText,
                        false,
                        512,
                        null,
                        null,
                        null,
                        null,
                        false,
                        NotificationPrivacyClass.PublicGameplay)
                },
                Array.Empty<NotificationActionDefinition>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static NotificationRequest Request(
            FakeClock clock,
            IEnumerable<NotificationParameter> parameters = null)
        {
            return new NotificationRequest(
                "al_notify_save_recovered_backup",
                "al_source_save",
                "test:correlation:1",
                clock.UtcNow,
                parameters ?? Array.Empty<NotificationParameter>(),
                null,
                null,
                null);
        }

        private static Fixture CreateFixture(
            NotificationDefinition definition,
            INotificationDurableStore store)
        {
            var resolver = new FakeResolver(definition);
            var clock = new FakeClock();
            var diagnostics = new FakeDiagnosticSink();
            var service = new LocalNotificationService(
                resolver,
                resolver,
                clock,
                new UnavailableNotificationActionRegistry(),
                diagnostics,
                store);
            return new Fixture(service, clock);
        }

        private sealed class Fixture
        {
            public Fixture(LocalNotificationService service, FakeClock clock)
            {
                Service = service;
                Clock = clock;
            }

            public LocalNotificationService Service { get; }
            public FakeClock Clock { get; }
        }

        private sealed class FakeResolver :
            INotificationDefinitionResolver,
            INotificationLocalizationReferenceAuthority
        {
            private readonly NotificationDefinition _definition;

            public FakeResolver(NotificationDefinition definition)
            {
                _definition = definition;
            }

            public bool IsAvailable => true;

            public bool Contains(string localizationReference) => true;

            public NotificationDefinitionResolution Resolve(string definitionId)
            {
                return string.Equals(definitionId, _definition.DefinitionId, StringComparison.Ordinal)
                    ? new NotificationDefinitionResolution(
                        NotificationDefinitionResolutionStatus.Found,
                        _definition,
                        null)
                    : new NotificationDefinitionResolution(
                        NotificationDefinitionResolutionStatus.UnknownId,
                        null,
                        "AL-NTF-DEFINITION");
            }
        }

        private sealed class FakeClock : INotificationClock
        {
            public FakeClock()
            {
                UtcNow = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
            }

            public DateTime UtcNow { get; }
            public double RealtimeSeconds { get; }
        }

        private sealed class FakeDiagnosticSink : INotificationDiagnosticSink
        {
            public void Record(NotificationDiagnostic diagnostic)
            {
            }

            public void RecordLegacyRaw(string escapedTechnicalText, bool isError)
            {
            }
        }

        private sealed class FakePresenter : INotificationPresenter
        {
            public FakePresenter(string presenterId)
            {
                PresenterId = presenterId;
            }

            public string PresenterId { get; }

            public NotificationPresenterOfferResult Offer(NotificationPresentationOffer offer)
            {
                return new NotificationPresenterOfferResult(
                    NotificationPresenterOfferStatus.AcceptedPendingPresentation,
                    null);
            }
        }
    }
}
