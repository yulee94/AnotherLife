using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Core.Interfaces.Notifications;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Notifications
{
    public sealed class NotificationContentCatalogResolverTests
    {
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_notification_content_catalog.json";

        private static readonly string[] ExpectedIds =
        {
            "al_notify_save_recovered_backup",
            "al_notify_save_profile_degraded",
            "al_notify_save_unrecoverable",
            "al_notify_operation_unavailable",
            "al_notify_reward_committed",
            "al_notify_reward_failed",
            "al_notify_world_event_started",
            "al_notify_world_event_ended",
            "al_notify_bridge_unavailable",
            "al_notify_catalog_unavailable",
            "al_notify_content_unavailable"
        };

        [Test]
        public void ParameterlessAndNullSourceStayUnavailableWithoutDefinitions()
        {
            var pending = new NotificationContentCatalogResolver();
            var unavailable = new NotificationContentCatalogResolver(
                null,
                new FakeLocalizationAuthority(true, "notification.content.approved"));

            AssertResolution(
                pending.Resolve(ExpectedIds[0]),
                NotificationDefinitionResolutionStatus.CatalogPending);
            Assert.IsFalse(pending.IsAvailable);
            Assert.IsFalse(pending.Contains("notification.content.approved"));
            Assert.IsEmpty(pending.DefinitionIds);

            AssertResolution(
                unavailable.Resolve(ExpectedIds[0]),
                NotificationDefinitionResolutionStatus.CatalogUnavailable);
            Assert.IsFalse(unavailable.IsAvailable);
            Assert.IsFalse(unavailable.Contains("notification.content.approved"));
            Assert.IsEmpty(unavailable.DefinitionIds);
        }

        [Test]
        public void ExactInjectedSourcePublishesAllDefinitionsAtomically()
        {
            byte[] bytes = ReadCatalogBytes();
            string beforeHash = Sha256(bytes);
            var resolver = NewValidResolver(bytes);

            Assert.AreEqual(
                NotificationContentCatalogResolver.ExpectedSourceByteLength,
                bytes.Length);
            Assert.AreEqual(NotificationContentCatalogResolver.ExpectedSourceSha256, beforeHash);
            CollectionAssert.AreEqual(ExpectedIds, resolver.DefinitionIds);

            foreach (string id in ExpectedIds)
            {
                NotificationDefinitionResolution result = resolver.Resolve(id);
                AssertResolution(result, NotificationDefinitionResolutionStatus.Found);
                Assert.AreSame(result.Definition, resolver.Resolve(id).Definition);
                Assert.IsTrue(NotificationValidation.ValidateDefinition(result.Definition).IsValid);
                Assert.IsEmpty(result.Definition.Actions);
            }

            AssertResolution(
                resolver.Resolve("al_notify_unknown"),
                NotificationDefinitionResolutionStatus.UnknownId);
            AssertResolution(
                resolver.Resolve("AL_NOTIFY_SAVE_RECOVERED_BACKUP"),
                NotificationDefinitionResolutionStatus.UnknownId);
            AssertResolution(
                resolver.Resolve(" al_notify_save_recovered_backup"),
                NotificationDefinitionResolutionStatus.UnknownId);
            AssertResolution(
                resolver.Resolve(null),
                NotificationDefinitionResolutionStatus.UnknownId);
            Assert.AreEqual(beforeHash, Sha256(ReadCatalogBytes()));
        }

        [TestCase(
            "al_notify_save_recovered_backup",
            "al_source_save",
            NotificationSeverity.Warning,
            NotificationCategory.SaveRecovery,
            NotificationChannel.Acknowledgement,
            NotificationAcknowledgementPolicy.Required,
            NotificationDurabilityPolicy.DurableUntilAcknowledged,
            60,
            NotificationPrivacyClass.ProfilePrivate,
            false,
            "profile_label",
            NotificationPrivacyClass.ProfilePrivate)]
        [TestCase(
            "al_notify_save_profile_degraded",
            "al_source_save",
            NotificationSeverity.RecoverableError,
            NotificationCategory.SaveRecovery,
            NotificationChannel.Acknowledgement,
            NotificationAcknowledgementPolicy.Required,
            NotificationDurabilityPolicy.DurableUntilAcknowledged,
            80,
            NotificationPrivacyClass.ProfilePrivate,
            false,
            "profile_label",
            NotificationPrivacyClass.ProfilePrivate)]
        [TestCase(
            "al_notify_save_unrecoverable",
            "al_source_save",
            NotificationSeverity.BlockingError,
            NotificationCategory.SaveRecovery,
            NotificationChannel.Acknowledgement,
            NotificationAcknowledgementPolicy.Required,
            NotificationDurabilityPolicy.DurableUntilAcknowledged,
            100,
            NotificationPrivacyClass.ProfilePrivate,
            false,
            "profile_label",
            NotificationPrivacyClass.ProfilePrivate)]
        [TestCase(
            "al_notify_operation_unavailable",
            "al_source_nvs",
            NotificationSeverity.Warning,
            NotificationCategory.ContentAvailability,
            NotificationChannel.Toast,
            NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy.SessionTransient,
            60,
            NotificationPrivacyClass.PublicGameplay,
            true,
            "operation_name",
            NotificationPrivacyClass.PublicGameplay)]
        [TestCase(
            "al_notify_reward_committed",
            "al_source_boss_loot",
            NotificationSeverity.Success,
            NotificationCategory.Reward,
            NotificationChannel.Toast,
            NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy.SessionTransient,
            40,
            NotificationPrivacyClass.PublicGameplay,
            true,
            "reward_summary",
            NotificationPrivacyClass.PublicGameplay)]
        [TestCase(
            "al_notify_reward_failed",
            "al_source_boss_loot",
            NotificationSeverity.RecoverableError,
            NotificationCategory.Reward,
            NotificationChannel.Acknowledgement,
            NotificationAcknowledgementPolicy.Required,
            NotificationDurabilityPolicy.DurableUntilAcknowledged,
            80,
            NotificationPrivacyClass.PublicGameplay,
            false,
            "reward_summary",
            NotificationPrivacyClass.PublicGameplay)]
        [TestCase(
            "al_notify_world_event_started",
            "al_source_world_state",
            NotificationSeverity.Information,
            NotificationCategory.WorldState,
            NotificationChannel.Toast,
            NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy.SessionTransient,
            30,
            NotificationPrivacyClass.PublicGameplay,
            true,
            "event_name",
            NotificationPrivacyClass.PublicGameplay)]
        [TestCase(
            "al_notify_world_event_ended",
            "al_source_world_state",
            NotificationSeverity.Information,
            NotificationCategory.WorldState,
            NotificationChannel.Toast,
            NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy.SessionTransient,
            30,
            NotificationPrivacyClass.PublicGameplay,
            true,
            "event_name",
            NotificationPrivacyClass.PublicGameplay)]
        [TestCase(
            "al_notify_bridge_unavailable",
            "al_source_bridge",
            NotificationSeverity.Warning,
            NotificationCategory.Integration,
            NotificationChannel.Toast,
            NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy.SessionTransient,
            60,
            NotificationPrivacyClass.PublicGameplay,
            true,
            "route_label",
            NotificationPrivacyClass.PublicGameplay)]
        [TestCase(
            "al_notify_catalog_unavailable",
            "al_source_catalog",
            NotificationSeverity.BlockingError,
            NotificationCategory.ContentAvailability,
            NotificationChannel.Acknowledgement,
            NotificationAcknowledgementPolicy.Required,
            NotificationDurabilityPolicy.DurableUntilAcknowledged,
            100,
            NotificationPrivacyClass.PublicGameplay,
            false,
            "catalog_label",
            NotificationPrivacyClass.PublicGameplay)]
        [TestCase(
            "al_notify_content_unavailable",
            "al_source_catalog",
            NotificationSeverity.Warning,
            NotificationCategory.ContentAvailability,
            NotificationChannel.Toast,
            NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy.SessionTransient,
            60,
            NotificationPrivacyClass.PublicGameplay,
            true,
            "content_label",
            NotificationPrivacyClass.PublicGameplay)]
        public void ResolvedDefinitionsMatchSourceRuntimeMapping(
            string id,
            string sourceId,
            NotificationSeverity severity,
            NotificationCategory category,
            NotificationChannel channel,
            NotificationAcknowledgementPolicy acknowledgement,
            NotificationDurabilityPolicy durability,
            int priority,
            NotificationPrivacyClass privacy,
            bool allowEviction,
            string parameterName,
            NotificationPrivacyClass parameterPrivacy)
        {
            NotificationDefinition definition = NewValidResolver().Resolve(id).Definition;

            Assert.AreEqual(NotificationTechnicalLimits.CurrentDefinitionSchemaVersion, definition.SchemaVersion);
            Assert.AreEqual(1, definition.ContentVersion);
            Assert.AreEqual(severity, definition.Severity);
            Assert.AreEqual(category, definition.Category);
            Assert.AreEqual(channel, definition.DefaultChannel);
            CollectionAssert.AreEqual(new[] { channel }, definition.AllowedChannels);
            Assert.AreEqual(acknowledgement, definition.AcknowledgementPolicy);
            Assert.AreEqual(durability, definition.DurabilityPolicy);
            Assert.AreEqual(NotificationExpiryMode.None, definition.ExpiryPolicy.Mode);
            Assert.AreEqual(0d, definition.ExpiryPolicy.RealtimeDurationSeconds);
            Assert.IsFalse(definition.ExpiryPolicy.ExpireWhilePresenterUnavailable);
            Assert.AreEqual(priority, definition.Priority);
            Assert.AreEqual(NotificationDeduplicationPolicy.ByCorrelationAndDefinition, definition.DeduplicationPolicy);
            Assert.AreEqual(privacy, definition.PrivacyClass);
            Assert.IsTrue(definition.RequiresCorrelation);
            Assert.AreEqual(allowEviction, definition.AllowCapacityEviction);
            CollectionAssert.AreEqual(new[] { sourceId }, definition.AllowedSourceSystemIds);
            Assert.IsEmpty(definition.Actions);
            Assert.IsEmpty(definition.AllowedPredecessorDefinitionIds);
            Assert.IsEmpty(definition.AllowedSuccessorDefinitionIds);

            Assert.AreEqual(1, definition.ParameterSchema.Count);
            NotificationParameterDefinition parameter = definition.ParameterSchema[0];
            Assert.AreEqual(parameterName, parameter.Name);
            Assert.AreEqual(NotificationParameterValueKind.LocalizationReference, parameter.ValueKind);
            Assert.IsTrue(parameter.Required);
            Assert.AreEqual(256, parameter.MaximumUtf8Bytes);
            Assert.IsNull(parameter.MinimumInt64);
            Assert.IsNull(parameter.MaximumInt64);
            Assert.IsNull(parameter.MinimumUInt64);
            Assert.IsNull(parameter.MaximumUInt64);
            Assert.IsFalse(parameter.Persistable);
            Assert.AreEqual(parameterPrivacy, parameter.PrivacyClass);
        }

        [Test]
        public void CanonicalSourceActionsUseApprovedNotificationActionGrammar()
        {
            var actionIds = new[]
            {
                "al_notify_action_acknowledge",
                "al_notify_action_retry_operation",
                "al_notify_action_open_recovery_details"
            };

            foreach (string actionId in actionIds)
            {
                Assert.IsTrue(NotificationValidation.IsActionId(actionId));
            }

            Assert.IsFalse(NotificationValidation.IsActionId("al_action_acknowledge"));
        }

        [Test]
        public void LocalizationMembershipDelegatesToInjectedAuthorityOnly()
        {
            var available = new FakeLocalizationAuthority(
                true,
                "notification.operation.ready");
            var unavailable = new FakeLocalizationAuthority(
                false,
                "notification.operation.ready");

            var availableResolver = new NotificationContentCatalogResolver(
                ReadCatalogBytes(),
                available);
            var unavailableResolver = new NotificationContentCatalogResolver(
                ReadCatalogBytes(),
                unavailable);
            var missingResolver = new NotificationContentCatalogResolver(
                ReadCatalogBytes(),
                null);

            Assert.IsTrue(availableResolver.IsAvailable);
            Assert.IsTrue(availableResolver.Contains("notification.operation.ready"));
            Assert.IsFalse(availableResolver.Contains("notification.operation.missing"));
            Assert.IsFalse(unavailableResolver.IsAvailable);
            Assert.IsFalse(unavailableResolver.Contains("notification.operation.ready"));
            Assert.IsFalse(missingResolver.IsAvailable);
            Assert.IsFalse(missingResolver.Contains("notification.operation.ready"));
        }

        [Test]
        public void AdapterQueueAcceptsApprovedTransientReferenceAndRejectsUnsafeOrDurableInput()
        {
            var authority = new FakeLocalizationAuthority(
                true,
                "notification.operation.ready",
                "notification.profile.approved");
            var resolver = new NotificationContentCatalogResolver(
                ReadCatalogBytes(),
                authority);
            var clock = new FakeClock();
            var service = new LocalNotificationService(
                resolver,
                resolver,
                clock,
                new UnavailableNotificationActionRegistry(),
                new FakeDiagnosticSink());

            NotificationEnqueueResult accepted = service.Enqueue(new NotificationRequest(
                "al_notify_operation_unavailable",
                "al_source_nvs",
                "notification:adapter:accepted",
                clock.UtcNow,
                new[]
                {
                    new NotificationParameter(
                        "operation_name",
                        NotificationParameterValue.FromLocalizationReference(
                            "notification.operation.ready"))
                },
                null,
                null,
                null));
            NotificationEnqueueResult unknownReference = service.Enqueue(new NotificationRequest(
                "al_notify_operation_unavailable",
                "al_source_nvs",
                "notification:adapter:unknown",
                clock.UtcNow,
                new[]
                {
                    new NotificationParameter(
                        "operation_name",
                        NotificationParameterValue.FromLocalizationReference(
                            "notification.operation.unknown"))
                },
                null,
                null,
                null));
            NotificationEnqueueResult durable = service.Enqueue(new NotificationRequest(
                "al_notify_save_recovered_backup",
                "al_source_save",
                "notification:adapter:durable",
                clock.UtcNow,
                new[]
                {
                    new NotificationParameter(
                        "profile_label",
                        NotificationParameterValue.FromLocalizationReference(
                            "notification.profile.approved"))
                },
                null,
                null,
                null));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, accepted.Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedUnsafeParameter,
                unknownReference.Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedDurabilityUnavailable,
                durable.Status);
            Assert.AreEqual(1, service.GetSnapshot().Records.Count);
        }

        [Test]
        public void DefaultProductionServiceRemainsFailClosedAndUnregistered()
        {
            var service = new LocalNotificationService();
            var request = new NotificationRequest(
                "al_notify_operation_unavailable",
                "al_source_nvs",
                "notification:default:unavailable",
                DateTime.UtcNow,
                new NotificationParameter[0],
                null,
                null,
                null);

            NotificationEnqueueResult result = service.Enqueue(request);

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedDefinitionUnavailable,
                result.Status);
            Assert.IsEmpty(service.GetSnapshot().Records);
        }

        [Test]
        public void CallerMutationCannotChangePublishedSnapshot()
        {
            byte[] bytes = ReadCatalogBytes();
            var resolver = NewValidResolver(bytes);
            NotificationDefinition before = resolver.Resolve(ExpectedIds[0]).Definition;

            Array.Clear(bytes, 0, bytes.Length);

            NotificationDefinition after = resolver.Resolve(ExpectedIds[0]).Definition;
            Assert.AreSame(before, after);
            AssertResolution(resolver.Resolve(ExpectedIds[0]), NotificationDefinitionResolutionStatus.Found);
        }

        [Test]
        public void PublishedCollectionsAreReadOnly()
        {
            var resolver = NewValidResolver();
            NotificationDefinition definition = resolver.Resolve(ExpectedIds[0]).Definition;

            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)resolver.DefinitionIds).Add("al_notify_extra"));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<NotificationChannel>)definition.AllowedChannels).Add(NotificationChannel.Toast));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)definition.AllowedSourceSystemIds).Add("al_source_extra"));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<NotificationParameterDefinition>)definition.ParameterSchema)
                .Add(definition.ParameterSchema[0]));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<NotificationActionDefinition>)definition.Actions)
                .Add(new NotificationActionDefinition(
                    "al_notify_action_acknowledge",
                    NotificationActionKind.Acknowledge,
                    Array.Empty<NotificationParameterDefinition>(),
                    true,
                    true)));
        }

        [TestCase("\"version\": \"0.1.0\"", "\"version\": \"0.2.0\"",
            NotificationDefinitionResolutionStatus.UnsupportedVersion)]
        [TestCase("\"catalogId\": \"al_notification_content_catalog\"",
            "\"catalogId\": \"al_notification_content_catalog_x\"",
            NotificationDefinitionResolutionStatus.UnsupportedVersion)]
        [TestCase("\"sourcePacketId\": \"al_narrative_notification_content_source_v001\"",
            "\"sourcePacketId\": \"al_narrative_notification_content_source_v002\"",
            NotificationDefinitionResolutionStatus.UnsupportedVersion)]
        [TestCase("\"category\": \"catalog\"", "\"category\": \"system\"",
            NotificationDefinitionResolutionStatus.InvalidDefinition)]
        [TestCase("\"requiresCorrelation\": true", "\"requiresCorrelation\": false",
            NotificationDefinitionResolutionStatus.InvalidDefinition)]
        [TestCase("\"parameterNames\": [ \"profile_label\" ]", "\"parameterNames\": [ \"wrong_label\" ]",
            NotificationDefinitionResolutionStatus.InvalidDefinition)]
        [TestCase("\"al_notify_action_retry_operation\"", "\"al_notify_action_retry_operation_x\"",
            NotificationDefinitionResolutionStatus.InvalidDefinition)]
        public void SourceIdentityAndMappingDriftPublishNoDefinitions(
            string search,
            string replacement,
            NotificationDefinitionResolutionStatus expectedStatus)
        {
            byte[] mutated = Mutate(search, replacement);
            var resolver = new NotificationContentCatalogResolver(
                mutated,
                new FakeLocalizationAuthority(true));

            AssertResolution(resolver.Resolve(ExpectedIds[0]), expectedStatus);
            Assert.IsEmpty(resolver.DefinitionIds);
            Assert.IsFalse(resolver.IsAvailable);
        }

        [Test]
        public void DuplicateJsonMembersAreRejectedBeforePublication()
        {
            string source = Encoding.UTF8.GetString(ReadCatalogBytes());
            byte[] duplicateRootVersion = Encoding.UTF8.GetBytes(
                source.Replace("{\n", "{\n  \"version\": \"0.1.0\",\n"));

            var resolver = new NotificationContentCatalogResolver(
                duplicateRootVersion,
                new FakeLocalizationAuthority(true));

            AssertResolution(
                resolver.Resolve(ExpectedIds[0]),
                NotificationDefinitionResolutionStatus.InvalidDefinition);
            Assert.IsEmpty(resolver.DefinitionIds);
        }

        [TestCase("\"game\": \"Another Life\",\n", "")]
        [TestCase("\"game\": \"Another Life\"", "\"game\": null")]
        [TestCase("\"game\": \"Another Life\"", "\"game\": []")]
        [TestCase("\"id\": \"al_source_save\"", "\"id\": \"AL_SOURCE_SAVE\"")]
        [TestCase("\"id\": \"al_source_save\"", "\"id\": \" al_source_save\"")]
        [TestCase(
            "{ \"id\": \"al_source_save\", \"displayNameKey\": \"notification.source.save.name\" }",
            "{ \"displayNameKey\": \"notification.source.save.name\", \"id\": \"al_source_save\" }")]
        [TestCase(
            "\"labelKey\": \"notification.action.acknowledge.label\"",
            "\"labelKey\": null")]
        [TestCase(
            "\"key\": \"notification.source.save.name\"",
            "\"key\": \"Notification.source.save.name\"")]
        [TestCase("{profile_label}", "{wrong_label}")]
        public void StructuralTypeOrderAndReferenceDriftFailClosedWithZeroPublication(
            string search,
            string replacement)
        {
            var resolver = new NotificationContentCatalogResolver(
                Mutate(search, replacement),
                new FakeLocalizationAuthority(true));

            AssertResolution(
                resolver.Resolve(ExpectedIds[0]),
                NotificationDefinitionResolutionStatus.InvalidDefinition);
            Assert.IsEmpty(resolver.DefinitionIds);
            Assert.IsFalse(resolver.IsAvailable);
        }

        [Test]
        public void EmptyOversizedAndMalformedUtf8SourcesFailClosed()
        {
            byte[][] rejected =
            {
                Array.Empty<byte>(),
                new byte[NotificationContentCatalogResolver.MaximumSourceBytes + 1],
                new byte[] { 0xff, 0xfe, 0xfd }
            };

            foreach (byte[] bytes in rejected)
            {
                var resolver = new NotificationContentCatalogResolver(
                    bytes,
                    new FakeLocalizationAuthority(true));
                AssertResolution(
                    resolver.Resolve(ExpectedIds[0]),
                    NotificationDefinitionResolutionStatus.InvalidDefinition);
                Assert.IsEmpty(resolver.DefinitionIds);
            }
        }

        private static NotificationContentCatalogResolver NewValidResolver()
        {
            return NewValidResolver(ReadCatalogBytes());
        }

        private static NotificationContentCatalogResolver NewValidResolver(byte[] bytes)
        {
            return new NotificationContentCatalogResolver(
                bytes,
                new FakeLocalizationAuthority(true, "notification.operation.ready"));
        }

        private static byte[] ReadCatalogBytes()
        {
            return File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath));
        }

        private static byte[] Mutate(string search, string replacement)
        {
            string source = Encoding.UTF8.GetString(ReadCatalogBytes());
            Assert.IsTrue(source.Contains(search), "Fixture search text was not found: " + search);
            return Encoding.UTF8.GetBytes(source.Replace(search, replacement));
        }

        private static string Sha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(bytes)
                    .Select(item => item.ToString("x2")));
            }
        }

        private static void AssertResolution(
            NotificationDefinitionResolution resolution,
            NotificationDefinitionResolutionStatus expectedStatus)
        {
            Assert.AreEqual(expectedStatus, resolution.Status);
            if (expectedStatus == NotificationDefinitionResolutionStatus.Found)
            {
                Assert.IsNotNull(resolution.Definition);
                Assert.IsNull(resolution.DiagnosticCode);
            }
            else
            {
                Assert.IsNull(resolution.Definition);
                Assert.IsNotNull(resolution.DiagnosticCode);
            }
        }

        private sealed class FakeLocalizationAuthority : INotificationLocalizationReferenceAuthority
        {
            private readonly HashSet<string> references;

            internal FakeLocalizationAuthority(bool isAvailable, params string[] references)
            {
                IsAvailable = isAvailable;
                this.references = new HashSet<string>(
                    references ?? Array.Empty<string>(),
                    StringComparer.Ordinal);
            }

            public bool IsAvailable { get; }

            public bool Contains(string localizationReference)
            {
                return references.Contains(localizationReference);
            }
        }

        private sealed class FakeClock : INotificationClock
        {
            public DateTime UtcNow { get; } =
                new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

            public double RealtimeSeconds => 0d;
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
    }
}
