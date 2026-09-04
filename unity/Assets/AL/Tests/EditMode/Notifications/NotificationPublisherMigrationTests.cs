using System;
using System.IO;
using System.Linq;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Notifications;
using AL.Core.Interfaces.WorldState;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.Services.WorldState;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Notifications
{
    [TestFixture]
    public sealed class NotificationPublisherMigrationTests
    {
        [Test]
        public void BossLootPublisherEnqueuesCatalogBackedCommittedRequestWithoutPlayerName()
        {
            var resolver = new FakeResolver(CommittedDefinition());
            var clock = new FakeClock();
            var service = new LocalNotificationService(
                resolver,
                resolver,
                clock,
                new UnavailableNotificationActionRegistry(),
                new SilentDiagnostics());
            var request = new BossLootRequest
            {
                EncounterId = "encounter-1",
                RewardResultId = "reward-1",
                BossName = "Hidden Boss",
                PlayerDisplayName = "Raw Player"
            };
            var result = new BossLootResult
            {
                EncounterId = "encounter-1",
                RewardResultId = "reward-1",
                WarzoneCreditsAwarded = 25
            };

            NotificationEnqueueResult enqueue = BossLootCatalogNotificationPublisher.PublishCommitted(
                service,
                request,
                result,
                clock.UtcNow);

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, enqueue.Status);
            NotificationQueueRecordSnapshot record = service.GetSnapshot().Records.Single();
            Assert.AreEqual(
                BossLootCatalogNotificationPublisher.CommittedDefinitionId,
                record.Definition.DefinitionId);
            Assert.AreEqual("al_source_boss_loot", record.Request.SourceSystemId);
            Assert.AreEqual("al_boss_loot:encounter-1:reward-1", record.Request.CorrelationId);
            Assert.AreEqual(
                BossLootCatalogNotificationPublisher.CommittedSummaryReference,
                record.Request.Parameters[0].Value.Value);
            Assert.That(record.Request.Parameters[0].Value.Value as string, Does.Not.Contain("Raw Player"));
            Assert.That(record.Request.CorrelationId, Does.Not.Contain("Hidden Boss"));
        }

        [Test]
        public void WorldStateOutboxMapsStartAndEndAndRejectsUnknownDefinitions()
        {
            var resolver = new FakeResolver(WorldDefinition("al_notify_world_event_started"));
            var clock = new FakeClock();
            var service = new LocalNotificationService(
                resolver,
                resolver,
                clock,
                new UnavailableNotificationActionRegistry(),
                new SilentDiagnostics());
            var outbox = new CatalogBackedWorldStateNotificationOutbox(
                service,
                clock,
                "world.event.veil_omen");

            Assert.IsTrue(
                outbox.TryEnqueue(
                    new WorldStateNotificationIntent(
                        WorldStateAuthoredCatalog.StartNotificationId,
                        "al_world_notification_start",
                        "instance-1"),
                    out string startDiagnostic),
                startDiagnostic);
            Assert.AreEqual(1, service.GetSnapshot().Records.Count);
            Assert.AreEqual(
                "al_notify_world_event_started",
                service.GetSnapshot().Records[0].Definition.DefinitionId);

            Assert.IsFalse(
                outbox.TryEnqueue(
                    new WorldStateNotificationIntent(
                        WorldStateAuthoredCatalog.CancelNotificationId,
                        "al_world_notification_cancel",
                        "instance-1"),
                    out string cancelDiagnostic));
            Assert.AreEqual("AL-WST-NOTIFY-DEFINITION", cancelDiagnostic);
        }

        [Test]
        public void ProductionBossLootServiceHasNoRawShowMessageCallers()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "AL", "Scripts");
            string lootPath = Path.Combine(
                scriptsRoot,
                "Services",
                "Local",
                "LocalBossLootService.cs");
            string source = File.ReadAllText(lootPath);
            Assert.That(source, Does.Not.Contain(".ShowMessage("));
            Assert.That(source, Does.Contain("BossLootCatalogNotificationPublisher.PublishCommitted"));
        }

        private static NotificationDefinition CommittedDefinition()
        {
            return new NotificationDefinition(
                BossLootCatalogNotificationPublisher.CommittedDefinitionId,
                NotificationTechnicalLimits.CurrentDefinitionSchemaVersion,
                1,
                NotificationSeverity.Success,
                NotificationCategory.Reward,
                NotificationChannel.Toast,
                new[] { NotificationChannel.Toast },
                NotificationAcknowledgementPolicy.None,
                NotificationDurabilityPolicy.SessionTransient,
                new NotificationExpiryPolicy(NotificationExpiryMode.None, 0d, false),
                40,
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition,
                NotificationPrivacyClass.PublicGameplay,
                true,
                true,
                new[] { BossLootCatalogNotificationPublisher.SourceSystemId },
                new[]
                {
                    new NotificationParameterDefinition(
                        BossLootCatalogNotificationPublisher.SummaryParameterName,
                        NotificationParameterValueKind.LocalizationReference,
                        true,
                        256,
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

        private static NotificationDefinition WorldDefinition(string definitionId)
        {
            return new NotificationDefinition(
                definitionId,
                NotificationTechnicalLimits.CurrentDefinitionSchemaVersion,
                1,
                NotificationSeverity.Information,
                NotificationCategory.WorldState,
                NotificationChannel.Toast,
                new[] { NotificationChannel.Toast },
                NotificationAcknowledgementPolicy.None,
                NotificationDurabilityPolicy.SessionTransient,
                new NotificationExpiryPolicy(NotificationExpiryMode.None, 0d, false),
                40,
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition,
                NotificationPrivacyClass.PublicGameplay,
                true,
                true,
                new[] { CatalogBackedWorldStateNotificationOutbox.SourceSystemId },
                new[]
                {
                    new NotificationParameterDefinition(
                        CatalogBackedWorldStateNotificationOutbox.EventNameParameter,
                        NotificationParameterValueKind.LocalizationReference,
                        true,
                        256,
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

        private sealed class SilentDiagnostics : INotificationDiagnosticSink
        {
            public void Record(NotificationDiagnostic diagnostic)
            {
            }

            public void RecordLegacyRaw(string escapedTechnicalText, bool isError)
            {
            }
        }
    }
}
