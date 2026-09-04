using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core.Interfaces.Notifications;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Notifications
{
    public sealed class NotificationContractQueueTests
    {
        [Test]
        public void DefinitionValidatorAcceptsEveryDeclaredEnumFamily()
        {
            foreach (NotificationSeverity severity in Enum.GetValues(typeof(NotificationSeverity)))
            {
                NotificationAcknowledgementPolicy acknowledgement =
                    severity == NotificationSeverity.BlockingError
                        ? NotificationAcknowledgementPolicy.Required
                        : NotificationAcknowledgementPolicy.None;
                NotificationChannel channel =
                    acknowledgement == NotificationAcknowledgementPolicy.Required
                        ? NotificationChannel.Acknowledgement
                        : NotificationChannel.Toast;
                AssertValid(Definition(
                    severity: severity,
                    channel: channel,
                    acknowledgement: acknowledgement,
                    allowEviction: acknowledgement != NotificationAcknowledgementPolicy.Required));
            }

            foreach (NotificationCategory category in Enum.GetValues(typeof(NotificationCategory)))
            {
                AssertValid(Definition(category: category));
            }

            foreach (NotificationChannel channel in Enum.GetValues(typeof(NotificationChannel)))
            {
                AssertValid(Definition(channel: channel));
            }

            foreach (NotificationDeduplicationPolicy policy in
                     Enum.GetValues(typeof(NotificationDeduplicationPolicy)))
            {
                AssertValid(policy == NotificationDeduplicationPolicy.ReplaceEarlierCorrelation
                    ? Definition(
                        deduplication: policy,
                        predecessors: new[] { "al_notify_previous" })
                    : Definition(
                        deduplication: policy,
                        requiresCorrelation:
                        policy != NotificationDeduplicationPolicy.None));
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("notify_bad")]
        [TestCase("al_notify_Bad")]
        [TestCase("al_notify_bad-thing")]
        public void DefinitionValidatorRejectsInvalidDefinitionIds(string definitionId)
        {
            AssertInvalid(Definition(definitionId: definitionId));
        }

        [TestCase("al_notify_action_acknowledge")]
        [TestCase("al_notify_action_retry_operation")]
        [TestCase("al_notify_action_open_recovery_details")]
        public void ActionValidatorAcceptsCanonicalApprovedIds(string actionId)
        {
            Assert.IsTrue(NotificationValidation.IsActionId(actionId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("al_action_acknowledge")]
        [TestCase("al_notify_action_Acknowledge")]
        [TestCase(" al_notify_action_acknowledge")]
        [TestCase("al_notify_action_acknowledge ")]
        [TestCase("al_notify_action_acknowledge-now")]
        public void ActionValidatorRejectsNonCanonicalIds(string actionId)
        {
            Assert.IsFalse(NotificationValidation.IsActionId(actionId));
        }

        [Test]
        public void ActionValidatorRejectsOversizedCanonicalShape()
        {
            Assert.IsFalse(NotificationValidation.IsActionId(
                "al_notify_action_" + new string('a', 96)));
        }

        [Test]
        public void LocalizationReferenceRequiresExactDottedGrammar()
        {
            var clock = new FakeClock();
            NotificationDefinition definition = Definition(parameterSchema: new[]
            {
                Parameter(
                    "content_label",
                    NotificationParameterValueKind.LocalizationReference,
                    true,
                    256)
            });

            Assert.IsTrue(NotificationValidation.ValidateRequest(
                definition,
                Request(clock, parameters: LocalizationParameters("notification.content.approved")),
                clock.UtcNow).IsValid);

            string[] rejected =
            {
                "notification_key",
                "Notification.content.approved",
                "notification.Content.approved",
                "notification/content/approved",
                "notification:content:approved",
                "notification.content@approved",
                " notification.content.approved",
                "notification.content.approved "
            };
            foreach (string reference in rejected)
            {
                Assert.IsFalse(NotificationValidation.ValidateRequest(
                    definition,
                    Request(clock, parameters: LocalizationParameters(reference)),
                    clock.UtcNow).IsValid, reference);
            }
        }

        [Test]
        public void QueueRequiresAvailableExactLocalizationMembershipBeforeMutation()
        {
            NotificationDefinition definition = Definition(parameterSchema: new[]
            {
                Parameter(
                    "content_label",
                    NotificationParameterValueKind.LocalizationReference,
                    true,
                    256)
            });
            var resolver = new FakeResolver(new[] { definition });
            resolver.LocalizationReferences.Add("notification.content.approved");
            Fixture fixture = FixtureWith(resolver);

            NotificationEnqueueResult accepted = fixture.Service.Enqueue(Request(
                fixture.Clock,
                correlationId: "test:localization:accepted",
                parameters: LocalizationParameters("notification.content.approved")));
            NotificationEnqueueResult unknown = fixture.Service.Enqueue(Request(
                fixture.Clock,
                correlationId: "test:localization:unknown",
                parameters: LocalizationParameters("notification.content.unknown")));
            resolver.LocalizationAuthorityAvailable = false;
            NotificationEnqueueResult unavailable = fixture.Service.Enqueue(Request(
                fixture.Clock,
                correlationId: "test:localization:unavailable",
                parameters: LocalizationParameters("notification.content.approved")));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, accepted.Status);
            Assert.AreEqual(NotificationEnqueueStatus.RejectedUnsafeParameter, unknown.Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedDefinitionUnavailable,
                unavailable.Status);
            Assert.AreEqual(1, fixture.Service.GetSnapshot().Records.Count);
        }

        [Test]
        public void DefinitionValidatorRejectsUnsupportedSchemaAndInvalidCombinations()
        {
            AssertInvalid(Definition(schemaVersion: 2));
            AssertInvalid(Definition(priority: 101));
            AssertInvalid(Definition(
                severity: NotificationSeverity.BlockingError,
                acknowledgement: NotificationAcknowledgementPolicy.None));
            AssertInvalid(Definition(
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                channel: NotificationChannel.Toast));
            AssertInvalid(Definition(
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                channel: NotificationChannel.Acknowledgement,
                expiry: new NotificationExpiryPolicy(
                    NotificationExpiryMode.AfterPresentation,
                    1d,
                    false)));
            AssertInvalid(Definition(
                durability: NotificationDurabilityPolicy.SessionUntilAcknowledged,
                allowEviction: true));
            AssertInvalid(Definition(
                deduplication: NotificationDeduplicationPolicy.ByCorrelation,
                requiresCorrelation: false));
            AssertInvalid(Definition(sourceSystemIds: new string[0]));
            AssertInvalid(Definition(
                channel: NotificationChannel.Acknowledgement,
                allowedChannels: new[]
                {
                    NotificationChannel.Acknowledgement,
                    NotificationChannel.Toast
                },
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                allowEviction: false));
            AssertInvalid(Definition(
                acknowledgement: NotificationAcknowledgementPolicy.None,
                durability: NotificationDurabilityPolicy.SessionUntilAcknowledged,
                allowEviction: false));
        }

        [Test]
        public void DefinitionValidatorRejectsDuplicateAndUnsafeSchemaRows()
        {
            NotificationParameterDefinition amount = Parameter(
                "amount",
                NotificationParameterValueKind.Int64,
                true);
            AssertInvalid(Definition(parameterSchema: new[] { amount, amount }));
            AssertInvalid(Definition(parameterSchema: new[]
            {
                Parameter("BadName", NotificationParameterValueKind.Int64, true)
            }));
            AssertInvalid(Definition(parameterSchema: new[]
            {
                new NotificationParameterDefinition(
                    "private_text",
                    NotificationParameterValueKind.SafeDisplayText,
                    true,
                    64,
                    null,
                    null,
                    null,
                    null,
                    true,
                    NotificationPrivacyClass.ProfilePrivate)
            }));

            NotificationActionDefinition action = Action();
            AssertInvalid(Definition(actions: new[] { action, action }));
        }

        [Test]
        public void DefinitionCollectionsAreCopiedAndCannotBeMutatedByCaller()
        {
            var channels = new List<NotificationChannel> { NotificationChannel.Toast };
            var parameters = new List<NotificationParameterDefinition>
            {
                Parameter("amount", NotificationParameterValueKind.Int64, true)
            };
            NotificationDefinition definition = Definition(
                allowedChannels: channels,
                parameterSchema: parameters);

            channels[0] = NotificationChannel.Acknowledgement;
            parameters.Clear();

            Assert.AreEqual(NotificationChannel.Toast, definition.AllowedChannels[0]);
            Assert.AreEqual(1, definition.ParameterSchema.Count);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<NotificationChannel>)definition.AllowedChannels)
                .Add(NotificationChannel.Banner));
        }

        [Test]
        public void ValidRequestQueuesPendingWithoutClaimingDelivery()
        {
            Fixture fixture = FixtureWith(Definition());

            NotificationEnqueueResult result = fixture.Service.Enqueue(Request(fixture.Clock));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, result.Status);
            Assert.IsTrue(result.QueueChanged);
            Assert.IsNotNull(result.NotificationInstanceId);
            NotificationDeliveryReceipt receipt =
                fixture.Service.GetReceipt(result.NotificationInstanceId);
            Assert.AreEqual(NotificationDeliveryState.PendingPresenter, receipt.State);
            Assert.IsNull(receipt.PresentedAtUtc);
            Assert.AreEqual(0, receipt.DeliveryAttempt);
        }

        [TestCase(NotificationDefinitionResolutionStatus.UnknownId,
            NotificationEnqueueStatus.RejectedDefinitionUnavailable)]
        [TestCase(NotificationDefinitionResolutionStatus.CatalogPending,
            NotificationEnqueueStatus.RejectedDefinitionUnavailable)]
        [TestCase(NotificationDefinitionResolutionStatus.CatalogUnavailable,
            NotificationEnqueueStatus.RejectedDefinitionUnavailable)]
        [TestCase(NotificationDefinitionResolutionStatus.InvalidDefinition,
            NotificationEnqueueStatus.RejectedDefinitionUnavailable)]
        [TestCase(NotificationDefinitionResolutionStatus.UnsupportedVersion,
            NotificationEnqueueStatus.RejectedUnsupportedDefinitionVersion)]
        public void ResolverFailureRejectsWithoutQueueMutation(
            NotificationDefinitionResolutionStatus resolutionStatus,
            NotificationEnqueueStatus expectedStatus)
        {
            var resolver = new FakeResolver
            {
                Override = new NotificationDefinitionResolution(
                    resolutionStatus,
                    null,
                    "AL-NTF-DEFINITION")
            };
            Fixture fixture = FixtureWith(resolver);

            NotificationEnqueueResult result = fixture.Service.Enqueue(Request(fixture.Clock));

            Assert.AreEqual(expectedStatus, result.Status);
            Assert.IsFalse(result.QueueChanged);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void DurableDefinitionIsRecognizedButRejectedUntilPersistenceExists()
        {
            Fixture fixture = FixtureWith(Definition(
                durability: NotificationDurabilityPolicy.DurableHistory,
                allowEviction: false));

            NotificationEnqueueResult result = fixture.Service.Enqueue(Request(fixture.Clock));

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedDurabilityUnavailable,
                result.Status);
            Assert.AreEqual("AL-NTF-PERSISTENCE", result.DiagnosticCode);
        }

        [Test]
        public void CorrelationIsRequiredBoundedAndPrivacySafe()
        {
            Fixture fixture = FixtureWith(Definition());

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedCorrelationRequired,
                fixture.Service.Enqueue(Request(fixture.Clock, correlationId: null)).Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedUnsafeParameter,
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    correlationId: new string('x', 129))).Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedUnsafeParameter,
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    correlationId: "person@example.com")).Status);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void RequestRejectsInvalidSourceChannelAndTimestampWithoutMutation()
        {
            Fixture fixture = FixtureWith(Definition());

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedInvalidRequest,
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    sourceSystemId: "source_invalid")).Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedInvalidRequest,
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    requestedChannel: NotificationChannel.Banner)).Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedInvalidRequest,
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    occurredAtUtc: fixture.Clock.UtcNow.AddDays(-365d).AddSeconds(-1d))).Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedInvalidRequest,
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    occurredAtUtc: fixture.Clock.UtcNow.AddMinutes(5d).AddSeconds(1d))).Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedInvalidRequest,
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    occurredAtUtc: DateTime.SpecifyKind(
                        fixture.Clock.UtcNow,
                        DateTimeKind.Local))).Status);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void AllowedRequestedChannelIsRecordedWithoutClaimingPresentation()
        {
            Fixture fixture = FixtureWith(Definition(
                allowedChannels: new[]
                {
                    NotificationChannel.Toast,
                    NotificationChannel.Banner
                }));

            NotificationEnqueueResult result = fixture.Service.Enqueue(Request(
                fixture.Clock,
                requestedChannel: NotificationChannel.Banner));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, result.Status);
            NotificationQueueRecordSnapshot record =
                fixture.Service.GetSnapshot().Records.Single();
            Assert.AreEqual(NotificationChannel.Banner, record.Channel);
            Assert.IsNull(record.Receipt.Channel);
            Assert.AreEqual(NotificationDeliveryState.PendingPresenter, record.Receipt.State);
        }

        [Test]
        public void ParameterSchemaRejectsMissingUnknownWrongTypeRangeAndMarkup()
        {
            NotificationParameterDefinition amount = new NotificationParameterDefinition(
                "amount",
                NotificationParameterValueKind.Int64,
                true,
                0,
                0,
                100,
                null,
                null,
                true,
                NotificationPrivacyClass.PublicGameplay);
            NotificationParameterDefinition label = new NotificationParameterDefinition(
                "label",
                NotificationParameterValueKind.SafeDisplayText,
                false,
                32,
                null,
                null,
                null,
                null,
                false,
                NotificationPrivacyClass.ProfilePrivate);
            Fixture fixture = FixtureWith(Definition(parameterSchema: new[] { amount, label }));

            AssertRejected(fixture, new NotificationParameter[0],
                NotificationEnqueueStatus.RejectedInvalidRequest);
            AssertRejected(fixture, new[]
            {
                new NotificationParameter(
                    "other",
                    NotificationParameterValue.FromInt64(1))
            }, NotificationEnqueueStatus.RejectedInvalidRequest);
            AssertRejected(fixture, new[]
            {
                new NotificationParameter(
                    "amount",
                    NotificationParameterValue.FromUInt64(1))
            }, NotificationEnqueueStatus.RejectedInvalidRequest);
            AssertRejected(fixture, new[]
            {
                new NotificationParameter(
                    "amount",
                    NotificationParameterValue.FromInt64(101))
            }, NotificationEnqueueStatus.RejectedInvalidRequest);
            AssertRejected(fixture, new[]
            {
                new NotificationParameter(
                    "amount",
                    NotificationParameterValue.FromInt64(1)),
                new NotificationParameter(
                    "label",
                    NotificationParameterValue.FromSafeDisplayText("<b>unsafe</b>"))
            }, NotificationEnqueueStatus.RejectedUnsafeParameter);
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void ParameterOrderDoesNotChangeCanonicalReplayIdentity()
        {
            NotificationParameterDefinition amount = Parameter(
                "amount",
                NotificationParameterValueKind.Int64,
                true);
            NotificationParameterDefinition target = Parameter(
                "target_id",
                NotificationParameterValueKind.StableId,
                true,
                maximumUtf8Bytes: 64);
            Fixture fixture = FixtureWith(Definition(parameterSchema: new[] { amount, target }));
            NotificationParameter first =
                new NotificationParameter("amount", NotificationParameterValue.FromInt64(7));
            NotificationParameter second =
                new NotificationParameter(
                    "target_id",
                    NotificationParameterValue.FromStableId("target-7"));

            NotificationEnqueueResult accepted = fixture.Service.Enqueue(
                Request(fixture.Clock, parameters: new[] { first, second }));
            NotificationEnqueueResult replay = fixture.Service.Enqueue(
                Request(fixture.Clock, parameters: new[] { second, first }));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, accepted.Status);
            Assert.AreEqual(NotificationEnqueueStatus.AcceptedAlreadyPresent, replay.Status);
            Assert.AreEqual(accepted.NotificationInstanceId, replay.NotificationInstanceId);
            Assert.AreEqual(1, fixture.Service.GetSnapshot().Records.Count);
        }

        [Test]
        public void ExactReplayDeduplicatesAndPayloadConflictPreservesExistingRecord()
        {
            NotificationParameterDefinition amount = Parameter(
                "amount",
                NotificationParameterValueKind.Int64,
                true);
            Fixture fixture = FixtureWith(Definition(parameterSchema: new[] { amount }));
            NotificationRequest firstRequest = Request(
                fixture.Clock,
                parameters: new[]
                {
                    new NotificationParameter(
                        "amount",
                        NotificationParameterValue.FromInt64(7))
                });

            NotificationEnqueueResult accepted = fixture.Service.Enqueue(firstRequest);
            NotificationEnqueueResult replay = fixture.Service.Enqueue(firstRequest);
            NotificationEnqueueResult conflict = fixture.Service.Enqueue(
                Request(
                    fixture.Clock,
                    parameters: new[]
                    {
                        new NotificationParameter(
                            "amount",
                            NotificationParameterValue.FromInt64(8))
                    }));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedAlreadyPresent, replay.Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedCorrelationConflict,
                conflict.Status);
            Assert.AreEqual(accepted.NotificationInstanceId, replay.ExistingInstanceId);
            Assert.AreEqual(1, fixture.Service.GetSnapshot().Records.Count);
            Assert.AreEqual(
                1,
                fixture.Diagnostics.Diagnostics.Count(item =>
                    item.Code == "AL-NTF-CORRELATION-CONFLICT"));
        }

        [Test]
        public void ChangedOccurrenceTimestampIsNotAnExactReplay()
        {
            Fixture fixture = FixtureWith(Definition());
            NotificationRequest first = Request(fixture.Clock);

            NotificationEnqueueResult accepted = fixture.Service.Enqueue(first);
            NotificationEnqueueResult conflict = fixture.Service.Enqueue(Request(
                fixture.Clock,
                occurredAtUtc: first.OccurredAtUtc.AddSeconds(-1d)));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, accepted.Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedCorrelationConflict,
                conflict.Status);
            Assert.AreEqual(1, fixture.Service.GetSnapshot().Records.Count);
        }

        [Test]
        public void ChangedDefinitionContentVersionIsNotAnExactReplay()
        {
            var resolver = new FakeResolver(new[] { Definition(contentVersion: 1) });
            Fixture fixture = FixtureWith(resolver);
            NotificationRequest request = Request(fixture.Clock);
            NotificationEnqueueResult accepted = fixture.Service.Enqueue(request);
            resolver.Override = new NotificationDefinitionResolution(
                NotificationDefinitionResolutionStatus.Found,
                Definition(contentVersion: 2),
                null);

            NotificationEnqueueResult conflict = fixture.Service.Enqueue(request);

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, accepted.Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedCorrelationConflict,
                conflict.Status);
            Assert.AreEqual(1, fixture.Service.GetSnapshot().Records.Count);
        }

        [Test]
        public void CorrelationAndDefinitionPolicyAllowsDistinctDefinitions()
        {
            NotificationDefinition firstDefinition = Definition(
                definitionId: "al_notify_stage_started",
                deduplication:
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition);
            NotificationDefinition secondDefinition = Definition(
                definitionId: "al_notify_stage_finished",
                deduplication:
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition);
            Fixture fixture = FixtureWith(firstDefinition, secondDefinition);

            NotificationEnqueueResult first = fixture.Service.Enqueue(Request(
                fixture.Clock,
                firstDefinition.DefinitionId));
            NotificationEnqueueResult second = fixture.Service.Enqueue(Request(
                fixture.Clock,
                secondDefinition.DefinitionId));
            NotificationEnqueueResult replay = fixture.Service.Enqueue(Request(
                fixture.Clock,
                firstDefinition.DefinitionId));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, first.Status);
            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, second.Status);
            Assert.AreEqual(
                NotificationEnqueueStatus.AcceptedAlreadyPresent,
                replay.Status);
            Assert.AreEqual(first.NotificationInstanceId, replay.ExistingInstanceId);
            Assert.AreEqual(2, fixture.Service.GetSnapshot().Records.Count);
        }

        [Test]
        public void ApprovedReplacementSupersedesEarlierRecordAndRejectsInvalidDirection()
        {
            const string previousId = "al_notify_operation_pending";
            const string nextId = "al_notify_operation_succeeded";
            NotificationDefinition previous = Definition(
                definitionId: previousId,
                successors: new[] { nextId });
            NotificationDefinition next = Definition(
                definitionId: nextId,
                deduplication: NotificationDeduplicationPolicy.ReplaceEarlierCorrelation,
                predecessors: new[] { previousId });
            Fixture fixture = FixtureWith(previous, next);

            NotificationEnqueueResult first = fixture.Service.Enqueue(
                Request(fixture.Clock, definitionId: previousId));
            NotificationEnqueueResult replacement = fixture.Service.Enqueue(
                Request(fixture.Clock, definitionId: nextId));

            Assert.AreEqual(
                NotificationEnqueueStatus.AcceptedReplacedEarlier,
                replacement.Status);
            Assert.AreEqual(first.NotificationInstanceId, replacement.ExistingInstanceId);
            Assert.AreEqual(
                NotificationDeliveryState.Superseded,
                fixture.Service.GetReceipt(first.NotificationInstanceId).State);
            Assert.AreEqual(2, fixture.Service.GetSnapshot().Records.Count);

            NotificationEnqueueResult reverse = fixture.Service.Enqueue(
                Request(fixture.Clock, definitionId: previousId));
            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedCorrelationConflict,
                reverse.Status);
        }

        [Test]
        public void LowerSeverityCannotReplaceUnacknowledgedBlockingRecord()
        {
            const string blockedId = "al_notify_operation_blocked";
            const string lowId = "al_notify_operation_ready";
            NotificationDefinition blocked = Definition(
                definitionId: blockedId,
                severity: NotificationSeverity.BlockingError,
                channel: NotificationChannel.Acknowledgement,
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                allowEviction: false,
                successors: new[] { lowId });
            NotificationDefinition low = Definition(
                definitionId: lowId,
                deduplication: NotificationDeduplicationPolicy.ReplaceEarlierCorrelation,
                predecessors: new[] { blockedId });
            Fixture fixture = FixtureWith(blocked, low);
            fixture.Service.Enqueue(Request(fixture.Clock, definitionId: blockedId));

            NotificationEnqueueResult result = fixture.Service.Enqueue(
                Request(fixture.Clock, definitionId: lowId));

            Assert.AreEqual(
                NotificationEnqueueStatus.RejectedCorrelationConflict,
                result.Status);
        }

        [Test]
        public void SnapshotOrderingIsBlockingSeverityPriorityOccurrenceThenSequence()
        {
            NotificationDefinition low = Definition(
                definitionId: "al_notify_low",
                priority: 1);
            NotificationDefinition high = Definition(
                definitionId: "al_notify_high",
                severity: NotificationSeverity.Warning,
                priority: 90);
            NotificationDefinition blocking = Definition(
                definitionId: "al_notify_blocking",
                severity: NotificationSeverity.BlockingError,
                channel: NotificationChannel.Acknowledgement,
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                allowEviction: false);
            Fixture fixture = FixtureWith(low, high, blocking);
            fixture.Service.Enqueue(Request(
                fixture.Clock,
                definitionId: low.DefinitionId,
                correlationId: "order:low"));
            fixture.Service.Enqueue(Request(
                fixture.Clock,
                definitionId: high.DefinitionId,
                correlationId: "order:high"));
            fixture.Service.Enqueue(Request(
                fixture.Clock,
                definitionId: blocking.DefinitionId,
                correlationId: "order:blocking"));

            string[] ids = fixture.Service.GetSnapshot().Records
                .Select(record => record.Definition.DefinitionId)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { blocking.DefinitionId, high.DefinitionId, low.DefinitionId },
                ids);
        }

        [Test]
        public void SnapshotQueriesAreDetachedReadOnlyAndStable()
        {
            Fixture fixture = FixtureWith(Definition());
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            NotificationQueueSnapshot beforePresenter = fixture.Service.GetSnapshot();
            NotificationQueueSnapshot repeated = fixture.Service.GetSnapshot();

            Assert.AreEqual(beforePresenter.Revision, repeated.Revision);
            Assert.AreEqual(
                NotificationDeliveryState.PendingPresenter,
                beforePresenter.Records[0].Receipt.State);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<NotificationQueueRecordSnapshot>)beforePresenter.Records)
                .Add(beforePresenter.Records[0]));

            fixture.Service.RegisterPresenter(
                new FakePresenter("presenter_snapshot"),
                new NotificationPresenterCapabilities(
                    new[] { NotificationChannel.Toast }));

            Assert.AreEqual(
                NotificationDeliveryState.PendingPresenter,
                beforePresenter.Records[0].Receipt.State);
            Assert.AreEqual(
                NotificationDeliveryState.PendingPresentation,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);
        }

        [Test]
        public void ThrowingQueueObserverIsIsolatedAndCanBeUnregistered()
        {
            Fixture fixture = FixtureWith(Definition());
            var throwing = new FakeObserver { Throw = true };
            var recording = new FakeObserver();
            NotificationQueueObserverRegistrationResult throwingRegistration =
                fixture.Service.RegisterObserver(throwing);
            NotificationQueueObserverRegistrationResult recordingRegistration =
                fixture.Service.RegisterObserver(recording);

            Assert.AreEqual(
                NotificationQueueObserverRegistrationStatus.Registered,
                throwingRegistration.Status);
            Assert.AreEqual(
                NotificationQueueObserverRegistrationStatus.Registered,
                recordingRegistration.Status);
            Assert.DoesNotThrow(() =>
                fixture.Service.Enqueue(Request(fixture.Clock)));
            Assert.AreEqual(1, throwing.CallCount);
            Assert.AreEqual(1, recording.CallCount);
            Assert.AreEqual(1, recording.LastSnapshot.Records.Count);
            Assert.AreEqual(1, fixture.Diagnostics.Diagnostics.Count(item =>
                item.Code == "AL-NTF-OBSERVER-FAILED"));

            Assert.AreEqual(
                NotificationQueueObserverUnregistrationStatus.Unregistered,
                fixture.Service.UnregisterObserver(throwingRegistration.Token));
            Assert.AreEqual(
                NotificationQueueObserverUnregistrationStatus.AlreadyUnregistered,
                fixture.Service.UnregisterObserver(throwingRegistration.Token));
            fixture.Service.Enqueue(Request(
                fixture.Clock,
                correlationId: "observer:second"));
            Assert.AreEqual(1, throwing.CallCount);
            Assert.AreEqual(2, recording.CallCount);
        }

        [Test]
        public void CapacityEvictsOldestLowestPriorityAllowedTransient()
        {
            NotificationDefinition low = Definition(
                definitionId: "al_notify_capacity_low",
                priority: 1,
                deduplication: NotificationDeduplicationPolicy.None,
                requiresCorrelation: false);
            NotificationDefinition high = Definition(
                definitionId: "al_notify_capacity_high",
                priority: 100,
                deduplication: NotificationDeduplicationPolicy.None,
                requiresCorrelation: false);
            Fixture fixture = FixtureWith(low, high);
            string firstInstance = null;
            for (int index = 0; index < NotificationTechnicalLimits.SessionCapacity; index++)
            {
                NotificationEnqueueResult result = fixture.Service.Enqueue(
                    Request(
                        fixture.Clock,
                        low.DefinitionId,
                        correlationId: null));
                firstInstance = firstInstance ?? result.NotificationInstanceId;
            }

            NotificationEnqueueResult added = fixture.Service.Enqueue(
                Request(fixture.Clock, high.DefinitionId, correlationId: null));
            NotificationQueueSnapshot snapshot = fixture.Service.GetSnapshot();

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, added.Status);
            Assert.AreEqual(NotificationTechnicalLimits.SessionCapacity, snapshot.Records.Count);
            Assert.IsFalse(snapshot.Records.Any(item =>
                item.NotificationInstanceId == firstInstance));
            Assert.IsTrue(snapshot.Records.Any(item =>
                item.NotificationInstanceId == added.NotificationInstanceId));
        }

        [Test]
        public void CapacityNeverEvictsRequiredRecordsAndDiagnosticIsRateLimited()
        {
            NotificationDefinition required = Definition(
                definitionId: "al_notify_capacity_required",
                severity: NotificationSeverity.BlockingError,
                channel: NotificationChannel.Acknowledgement,
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                durability: NotificationDurabilityPolicy.SessionUntilAcknowledged,
                allowEviction: false);
            Fixture fixture = FixtureWith(required);
            for (int index = 0; index < NotificationTechnicalLimits.SessionCapacity; index++)
            {
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    required.DefinitionId,
                    "required:" + index));
            }

            NotificationRequest overflow = Request(
                fixture.Clock,
                required.DefinitionId,
                "required:overflow");
            NotificationEnqueueResult first = fixture.Service.Enqueue(overflow);
            NotificationEnqueueResult replay = fixture.Service.Enqueue(overflow);

            Assert.AreEqual(NotificationEnqueueStatus.RejectedCapacity, first.Status);
            Assert.AreEqual(NotificationEnqueueStatus.RejectedCapacity, replay.Status);
            Assert.AreEqual(NotificationTechnicalLimits.SessionCapacity,
                fixture.Service.GetSnapshot().Records.Count);
            Assert.AreEqual(1, fixture.Diagnostics.Diagnostics.Count(item =>
                item.Code == "AL-NTF-CAPACITY" &&
                item.CorrelationId == "required:overflow"));
        }

        [Test]
        public void CapacityPrunesCompletedRecordBeforeConsideringEviction()
        {
            NotificationDefinition definition = Definition(
                acknowledgement: NotificationAcknowledgementPolicy.Dismissible,
                deduplication: NotificationDeduplicationPolicy.None,
                requiresCorrelation: false,
                allowEviction: false);
            Fixture fixture = FixtureWith(definition);
            NotificationEnqueueResult completed = fixture.Service.Enqueue(Request(
                fixture.Clock,
                correlationId: null));
            var presenter = new FakePresenter("presenter_capacity_prune");
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;
            fixture.Service.ConfirmPresented(token, completed.NotificationInstanceId);
            fixture.Service.Dismiss(token, completed.NotificationInstanceId);

            for (int index = 1;
                 index < NotificationTechnicalLimits.SessionCapacity;
                 index++)
            {
                fixture.Service.Enqueue(Request(
                    fixture.Clock,
                    correlationId: null));
            }

            NotificationEnqueueResult overflow = fixture.Service.Enqueue(Request(
                fixture.Clock,
                correlationId: null));

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, overflow.Status);
            Assert.IsNull(fixture.Service.GetReceipt(completed.NotificationInstanceId));
            Assert.AreEqual(
                NotificationTechnicalLimits.SessionCapacity,
                fixture.Service.GetSnapshot().Records.Count);
        }

        [Test]
        public void PresenterRegistrationOffersOneRecordAndRejectsDuplicateCapability()
        {
            Fixture fixture = FixtureWith(Definition());
            NotificationEnqueueResult first = fixture.Service.Enqueue(
                Request(fixture.Clock, correlationId: "presenter:first"));
            fixture.Service.Enqueue(
                Request(fixture.Clock, correlationId: "presenter:second"));
            var presenter = new FakePresenter("presenter_primary");

            NotificationPresenterRegistrationResult registered =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast }));
            NotificationPresenterRegistrationResult duplicate =
                fixture.Service.RegisterPresenter(
                    new FakePresenter("presenter_duplicate"),
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast }));

            Assert.AreEqual(
                NotificationPresenterRegistrationStatus.Registered,
                registered.Status);
            Assert.AreEqual(
                NotificationPresenterRegistrationStatus.RejectedDuplicateCapability,
                duplicate.Status);
            Assert.AreEqual(1, presenter.Offers.Count);
            Assert.AreEqual(first.NotificationInstanceId,
                presenter.Offers[0].Record.NotificationInstanceId);
            Assert.AreEqual(NotificationDeliveryState.PendingPresentation,
                fixture.Service.GetReceipt(first.NotificationInstanceId).State);
        }

        [Test]
        public void PresenterReceiptTransitionsAreExplicitAndMonotonic()
        {
            Fixture fixture = FixtureWith(Definition(
                acknowledgement: NotificationAcknowledgementPolicy.Dismissible));
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_receipts");
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;

            fixture.Clock.Advance(2d);
            NotificationReceiptUpdateResult presented =
                fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);
            fixture.Clock.Advance(3d);
            NotificationReceiptUpdateResult dismissed =
                fixture.Service.Dismiss(token, queued.NotificationInstanceId);
            NotificationReceiptUpdateResult repeated =
                fixture.Service.Dismiss(token, queued.NotificationInstanceId);

            Assert.AreEqual(NotificationReceiptUpdateStatus.Applied, presented.Status);
            Assert.AreEqual(NotificationDeliveryState.Presented, presented.Receipt.State);
            Assert.AreEqual(1, presented.Receipt.DeliveryAttempt);
            Assert.AreEqual(NotificationReceiptUpdateStatus.Applied, dismissed.Status);
            Assert.AreEqual(NotificationDeliveryState.Dismissed, dismissed.Receipt.State);
            Assert.AreEqual(fixture.Clock.UtcNow, dismissed.Receipt.CompletedAtUtc);
            Assert.AreEqual(NotificationReceiptUpdateStatus.NoChange, repeated.Status);
        }

        [Test]
        public void RequiredNotificationCannotDismissButCanAcknowledge()
        {
            NotificationDefinition required = Definition(
                severity: NotificationSeverity.BlockingError,
                channel: NotificationChannel.Acknowledgement,
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                allowEviction: false);
            Fixture fixture = FixtureWith(required);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_required");
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Acknowledgement })).Token;
            fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);

            NotificationReceiptUpdateResult dismiss =
                fixture.Service.Dismiss(token, queued.NotificationInstanceId);
            NotificationReceiptUpdateResult acknowledge =
                fixture.Service.Acknowledge(token, queued.NotificationInstanceId);

            Assert.AreEqual(NotificationReceiptUpdateStatus.RejectedPolicy, dismiss.Status);
            Assert.AreEqual(NotificationDeliveryState.Presented, dismiss.Receipt.State);
            Assert.AreEqual(NotificationReceiptUpdateStatus.Applied, acknowledge.Status);
            Assert.AreEqual(NotificationDeliveryState.Acknowledged, acknowledge.Receipt.State);
        }

        [Test]
        public void QueueOwnedAcknowledgementRejectsStaleAndNeverInvokesActionRegistry()
        {
            NotificationDefinition required = Definition(
                severity: NotificationSeverity.BlockingError,
                channel: NotificationChannel.Acknowledgement,
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                allowEviction: false,
                actions: new NotificationActionDefinition[0]);
            var registry = new FakeActionRegistry();
            Fixture fixture = FixtureWith(new[] { required }, registry);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    new FakePresenter("presenter_queue_acknowledge"),
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Acknowledgement })).Token;
            fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);

            NotificationReceiptUpdateResult stale = fixture.Service.Acknowledge(
                new NotificationPresenterRegistrationToken(
                    token.Generation + 1,
                    token.PresenterId),
                queued.NotificationInstanceId);
            NotificationReceiptUpdateResult applied = fixture.Service.Acknowledge(
                token,
                queued.NotificationInstanceId);
            NotificationReceiptUpdateResult replay = fixture.Service.Acknowledge(
                token,
                queued.NotificationInstanceId);

            Assert.AreEqual(
                NotificationReceiptUpdateStatus.RejectedStaleRegistration,
                stale.Status);
            Assert.AreEqual(NotificationReceiptUpdateStatus.Applied, applied.Status);
            Assert.AreEqual(NotificationReceiptUpdateStatus.NoChange, replay.Status);
            Assert.AreEqual(0, registry.CallCount);
        }

        [Test]
        public void UnregisterRetainsRecordAndRejectsStaleCallbackBeforeReattach()
        {
            Fixture fixture = FixtureWith(Definition());
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var firstPresenter = new FakePresenter("presenter_first");
            NotificationPresenterRegistrationToken firstToken =
                fixture.Service.RegisterPresenter(
                    firstPresenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;

            Assert.AreEqual(
                NotificationPresenterUnregistrationStatus.Unregistered,
                fixture.Service.UnregisterPresenter(firstToken));
            Assert.AreEqual(
                NotificationPresenterUnregistrationStatus.AlreadyUnregistered,
                fixture.Service.UnregisterPresenter(firstToken));
            Assert.AreEqual(
                NotificationReceiptUpdateStatus.RejectedStaleRegistration,
                fixture.Service.ConfirmPresented(
                    firstToken,
                    queued.NotificationInstanceId).Status);
            Assert.AreEqual(
                NotificationDeliveryState.DeliveryFailed,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);

            var nextPresenter = new FakePresenter("presenter_next");
            fixture.Service.RegisterPresenter(
                nextPresenter,
                new NotificationPresenterCapabilities(
                    new[] { NotificationChannel.Toast }));
            Assert.AreEqual(1, nextPresenter.Offers.Count);
            Assert.AreEqual(2,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).DeliveryAttempt);
        }

        [Test]
        public void RegistrationGenerationSeparatesMatchingPresenterIdsAcrossChannels()
        {
            NotificationDefinition toast = Definition(
                definitionId: "al_notify_toast_generation");
            NotificationDefinition banner = Definition(
                definitionId: "al_notify_banner_generation",
                channel: NotificationChannel.Banner);
            Fixture fixture = FixtureWith(toast, banner);
            NotificationEnqueueResult toastRecord = fixture.Service.Enqueue(Request(
                fixture.Clock,
                toast.DefinitionId,
                "generation:toast"));
            NotificationEnqueueResult bannerRecord = fixture.Service.Enqueue(Request(
                fixture.Clock,
                banner.DefinitionId,
                "generation:banner"));
            const string sharedPresenterId = "presenter_shared";
            NotificationPresenterRegistrationToken toastToken =
                fixture.Service.RegisterPresenter(
                    new FakePresenter(sharedPresenterId),
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;
            NotificationPresenterRegistrationToken bannerToken =
                fixture.Service.RegisterPresenter(
                    new FakePresenter(sharedPresenterId),
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Banner })).Token;

            Assert.AreEqual(
                NotificationReceiptUpdateStatus.RejectedStaleRegistration,
                fixture.Service.ConfirmPresented(
                    toastToken,
                    bannerRecord.NotificationInstanceId).Status);
            Assert.AreEqual(
                NotificationPresenterUnregistrationStatus.Unregistered,
                fixture.Service.UnregisterPresenter(toastToken));
            Assert.AreEqual(
                NotificationDeliveryState.DeliveryFailed,
                fixture.Service.GetReceipt(toastRecord.NotificationInstanceId).State);
            Assert.AreEqual(
                NotificationDeliveryState.PendingPresentation,
                fixture.Service.GetReceipt(bannerRecord.NotificationInstanceId).State);
            Assert.AreEqual(
                NotificationReceiptUpdateStatus.Applied,
                fixture.Service.ConfirmPresented(
                    bannerToken,
                    bannerRecord.NotificationInstanceId).Status);
        }

        [Test]
        public void ThrowingPresenterIsIsolatedAndQueueRemainsAuthoritative()
        {
            Fixture fixture = FixtureWith(Definition());
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_throw") { Throw = true };

            NotificationPresenterRegistrationResult registration =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast }));

            Assert.AreEqual(
                NotificationPresenterRegistrationStatus.Registered,
                registration.Status);
            NotificationDeliveryReceipt receipt =
                fixture.Service.GetReceipt(queued.NotificationInstanceId);
            Assert.AreEqual(NotificationDeliveryState.DeliveryFailed, receipt.State);
            Assert.AreEqual("AL-NTF-PRESENTER-FAILED", receipt.FailureCode);
            Assert.AreEqual(1, fixture.Service.GetSnapshot().Records.Count);
        }

        [Test]
        public void PresenterFailureRetriesOnlyWhenQueueIsRefreshed()
        {
            Fixture fixture = FixtureWith(Definition());
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_retry")
            {
                OfferStatus = NotificationPresenterOfferStatus.Failed
            };
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;

            Assert.AreEqual(
                NotificationDeliveryState.DeliveryFailed,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);
            Assert.AreEqual(
                1,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).DeliveryAttempt);

            presenter.OfferStatus =
                NotificationPresenterOfferStatus.AcceptedPendingPresentation;
            fixture.Clock.Advance(2d);
            fixture.Service.Refresh();
            NotificationDeliveryReceipt retry =
                fixture.Service.GetReceipt(queued.NotificationInstanceId);
            Assert.AreEqual(
                NotificationDeliveryState.PendingPresentation,
                retry.State);
            Assert.AreEqual(2, retry.DeliveryAttempt);
            Assert.AreEqual(
                NotificationReceiptUpdateStatus.Applied,
                fixture.Service.ConfirmPresented(
                    token,
                    queued.NotificationInstanceId).Status);
            Assert.AreEqual(
                fixture.Clock.UtcNow,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).PresentedAtUtc);
        }

        [Test]
        public void ExpiryUsesInjectedRealtimeAndNeverImpliesAcknowledgement()
        {
            NotificationDefinition transient = Definition(
                expiry: new NotificationExpiryPolicy(
                    NotificationExpiryMode.AfterPresentation,
                    5d,
                    false));
            Fixture fixture = FixtureWith(transient);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_expiry");
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;
            fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);

            fixture.Clock.Advance(4d);
            fixture.Service.Refresh();
            Assert.AreEqual(NotificationDeliveryState.Presented,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);

            fixture.Clock.Advance(1d);
            fixture.Service.Refresh();
            NotificationDeliveryReceipt expired =
                fixture.Service.GetReceipt(queued.NotificationInstanceId);
            Assert.AreEqual(NotificationDeliveryState.Expired, expired.State);
            Assert.IsNotNull(expired.CompletedAtUtc);
        }

        [Test]
        public void OccurrenceExpiryUsesRealtimeAfterEnqueue()
        {
            Fixture fixture = FixtureWith(Definition(
                expiry: new NotificationExpiryPolicy(
                    NotificationExpiryMode.AfterOccurrence,
                    5d,
                    true)));
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));

            fixture.Clock.AdvanceRealtime(5d);
            fixture.Service.Refresh();

            Assert.AreEqual(
                NotificationDeliveryState.Expired,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);
        }

        [Test]
        public void OccurrenceExpiryPausesWhileDeliveryFailureAwaitsPresenter()
        {
            Fixture fixture = FixtureWith(Definition(
                expiry: new NotificationExpiryPolicy(
                    NotificationExpiryMode.AfterOccurrence,
                    1d,
                    false)));
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_expiry_retry")
            {
                OfferStatus = NotificationPresenterOfferStatus.Failed
            };
            fixture.Service.RegisterPresenter(
                presenter,
                new NotificationPresenterCapabilities(
                    new[] { NotificationChannel.Toast }));

            fixture.Clock.AdvanceRealtime(2d);
            fixture.Service.Refresh();
            Assert.AreEqual(
                NotificationDeliveryState.DeliveryFailed,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);

            presenter.OfferStatus =
                NotificationPresenterOfferStatus.AcceptedPendingPresentation;
            fixture.Service.Refresh();
            Assert.AreEqual(
                NotificationDeliveryState.PendingPresentation,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);
        }

        [Test]
        public void TypedActionIsValidatedIdempotentAndAcknowledgesOnlyOnApplied()
        {
            NotificationActionDefinition action = Action();
            NotificationDefinition definition = Definition(
                channel: NotificationChannel.Acknowledgement,
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                severity: NotificationSeverity.BlockingError,
                allowEviction: false,
                actions: new[] { action });
            var registry = new FakeActionRegistry
            {
                Result = new NotificationActionResult(
                    NotificationActionStatus.Applied,
                    null)
            };
            Fixture fixture = FixtureWith(new[] { definition }, registry);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_action");
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Acknowledgement })).Token;
            fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);
            var invocation = new NotificationActionInvocation(
                action.ActionId,
                "test:correlation:1",
                new NotificationParameter[0]);

            NotificationActionResult applied = fixture.Service.InvokeAction(
                token,
                queued.NotificationInstanceId,
                invocation);
            NotificationActionResult replay = fixture.Service.InvokeAction(
                token,
                queued.NotificationInstanceId,
                invocation);

            Assert.AreEqual(NotificationActionStatus.Applied, applied.Status);
            Assert.AreEqual(NotificationActionStatus.NoChange, replay.Status);
            Assert.AreEqual(1, registry.CallCount);
            Assert.AreEqual(NotificationDeliveryState.Acknowledged,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);
        }

        [Test]
        public void FailedTypedActionDoesNotAcknowledge()
        {
            NotificationActionDefinition action = Action();
            NotificationDefinition definition = Definition(
                channel: NotificationChannel.Acknowledgement,
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                severity: NotificationSeverity.BlockingError,
                allowEviction: false,
                actions: new[] { action });
            var registry = new FakeActionRegistry
            {
                Result = new NotificationActionResult(
                    NotificationActionStatus.Failed,
                    "AL-NTF-ACTION")
            };
            Fixture fixture = FixtureWith(new[] { definition }, registry);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            var presenter = new FakePresenter("presenter_failed_action");
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    presenter,
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Acknowledgement })).Token;
            fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);

            NotificationActionResult result = fixture.Service.InvokeAction(
                token,
                queued.NotificationInstanceId,
                new NotificationActionInvocation(
                    action.ActionId,
                    "test:correlation:1",
                    new NotificationParameter[0]));

            Assert.AreEqual(NotificationActionStatus.Failed, result.Status);
            Assert.AreEqual(NotificationDeliveryState.Presented,
                fixture.Service.GetReceipt(queued.NotificationInstanceId).State);
        }

        [Test]
        public void TypedActionRejectsArbitraryRoutePayloadBeforeRegistryInvocation()
        {
            var action = new NotificationActionDefinition(
                "al_notify_action_open_route",
                NotificationActionKind.OpenApprovedRoute,
                new[]
                {
                    Parameter(
                        "route_id",
                        NotificationParameterValueKind.StableId,
                        true,
                        64)
                },
                false,
                false);
            var registry = new FakeActionRegistry();
            Fixture fixture = FixtureWith(
                new[] { Definition(actions: new[] { action }) },
                registry);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    new FakePresenter("presenter_route_action"),
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;

            NotificationActionResult result = fixture.Service.InvokeAction(
                token,
                queued.NotificationInstanceId,
                new NotificationActionInvocation(
                    action.ActionId,
                    "test:correlation:1",
                    new[]
                    {
                        new NotificationParameter(
                            "route_id",
                            NotificationParameterValue.FromStableId(
                                "https://unapproved.example"))
                    }));

            Assert.AreEqual(
                NotificationActionStatus.RejectedInvalidPayload,
                result.Status);
            Assert.AreEqual(0, registry.CallCount);
        }

        [Test]
        public void UndefinedRegistryResultFailsClosed()
        {
            NotificationActionDefinition action = Action();
            var registry = new FakeActionRegistry
            {
                Result = new NotificationActionResult(
                    (NotificationActionStatus)999,
                    null)
            };
            Fixture fixture = FixtureWith(
                new[] { Definition(actions: new[] { action }) },
                registry);
            NotificationEnqueueResult queued = fixture.Service.Enqueue(Request(fixture.Clock));
            NotificationPresenterRegistrationToken token =
                fixture.Service.RegisterPresenter(
                    new FakePresenter("presenter_invalid_action_result"),
                    new NotificationPresenterCapabilities(
                        new[] { NotificationChannel.Toast })).Token;
            fixture.Service.ConfirmPresented(token, queued.NotificationInstanceId);

            NotificationActionResult result = fixture.Service.InvokeAction(
                token,
                queued.NotificationInstanceId,
                new NotificationActionInvocation(
                    action.ActionId,
                    "test:correlation:1",
                    new NotificationParameter[0]));

            Assert.AreEqual(NotificationActionStatus.Failed, result.Status);
            Assert.AreEqual("AL-NTF-ACTION", result.DiagnosticCode);
        }

        [Test]
        public void LegacyWrappersEmitEscapedDiagnosticOnlyAndNeverEnterQueue()
        {
            Fixture fixture = FixtureWith(Definition());

#pragma warning disable 0618
            fixture.Service.ShowMessage("<b>Hello</b>\nthere");
            fixture.Service.ShowError("<color=red>failure</color>");
            fixture.Service.ShowResourceGain(AL.Core.ResourceType.Gold, 25);
#pragma warning restore 0618

            Assert.AreEqual(3, fixture.Diagnostics.Diagnostics.Count(item =>
                item.Code == "AL-NTF-LEGACY-RAW"));
            Assert.AreEqual(3, fixture.Diagnostics.Legacy.Count);
            Assert.That(fixture.Diagnostics.Legacy[0].Text, Does.Contain("&lt;b&gt;"));
            Assert.That(fixture.Diagnostics.Legacy[0].Text, Does.Not.Contain("<b>"));
            Assert.IsEmpty(fixture.Service.GetSnapshot().Records);
        }

        [Test]
        public void LegacyCallerInventoryMatchesCurrentProductionSource()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "AL", "Scripts");
            string unityRoot = Directory.GetParent(Application.dataPath).FullName;
            string inventoryPath = Path.Combine(
                unityRoot,
                "Docs",
                "Notification_Caller_Inventory.md");
            Assert.True(File.Exists(inventoryPath));
            string inventory = File.ReadAllText(inventoryPath).Replace('\\', '/');

            string[] messageCallers = FindCallers(scriptsRoot, ".ShowMessage(");
            CollectionAssert.AreEqual(
                new[]
                {
                    "Services/Local/LocalBossLootService.cs"
                },
                messageCallers);
            CollectionAssert.IsEmpty(FindCallers(scriptsRoot, ".ShowError("));
            CollectionAssert.IsEmpty(FindCallers(scriptsRoot, ".ShowResourceGain("));

            Assert.AreEqual(
                0,
                CountOccurrences(
                    File.ReadAllText(Path.Combine(
                        scriptsRoot,
                        "Kingdom",
                        "Narrative",
                        "WorldStateService.cs")),
                    ".ShowMessage("));
            Assert.AreEqual(
                3,
                CountOccurrences(
                    File.ReadAllText(Path.Combine(
                        scriptsRoot,
                        "Services",
                        "Local",
                        "LocalBossLootService.cs")),
                    ".ShowMessage("));
            foreach (string caller in messageCallers)
            {
                StringAssert.Contains(caller, inventory);
            }

            StringAssert.Contains("ShowError", inventory);
            StringAssert.Contains("zero production callers", inventory);
            StringAssert.Contains("ShowResourceGain", inventory);
        }

        private static void AssertRejected(
            Fixture fixture,
            IEnumerable<NotificationParameter> parameters,
            NotificationEnqueueStatus status)
        {
            NotificationEnqueueResult result = fixture.Service.Enqueue(
                Request(fixture.Clock, parameters: parameters));
            Assert.AreEqual(status, result.Status);
        }

        private static void AssertValid(NotificationDefinition definition)
        {
            NotificationValidationResult result =
                NotificationValidation.ValidateDefinition(definition);
            Assert.IsTrue(result.IsValid, result.DiagnosticCode);
        }

        private static void AssertInvalid(NotificationDefinition definition)
        {
            Assert.IsFalse(NotificationValidation.ValidateDefinition(definition).IsValid);
        }

        private static string[] FindCallers(string scriptsRoot, string token)
        {
            return Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(token))
                .Select(path =>
                    path.Substring(scriptsRoot.Length + 1).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }

            return count;
        }

        private static NotificationDefinition Definition(
            string definitionId = "al_notify_test",
            int schemaVersion = NotificationTechnicalLimits.CurrentDefinitionSchemaVersion,
            NotificationSeverity severity = NotificationSeverity.Information,
            NotificationCategory category = NotificationCategory.System,
            NotificationChannel channel = NotificationChannel.Toast,
            IEnumerable<NotificationChannel> allowedChannels = null,
            NotificationAcknowledgementPolicy acknowledgement =
                NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy durability =
                NotificationDurabilityPolicy.SessionTransient,
            NotificationExpiryPolicy expiry = null,
            int priority = 50,
            NotificationDeduplicationPolicy deduplication =
                NotificationDeduplicationPolicy.ByCorrelation,
            bool requiresCorrelation = true,
            bool allowEviction = true,
            IEnumerable<NotificationParameterDefinition> parameterSchema = null,
            IEnumerable<NotificationActionDefinition> actions = null,
            IEnumerable<string> predecessors = null,
            IEnumerable<string> successors = null,
            IEnumerable<string> sourceSystemIds = null,
            int contentVersion = 1)
        {
            return new NotificationDefinition(
                definitionId,
                schemaVersion,
                contentVersion,
                severity,
                category,
                channel,
                allowedChannels ?? new[] { channel },
                acknowledgement,
                durability,
                expiry ?? new NotificationExpiryPolicy(
                    NotificationExpiryMode.None,
                    0d,
                    false),
                priority,
                deduplication,
                NotificationPrivacyClass.PublicGameplay,
                requiresCorrelation,
                allowEviction,
                sourceSystemIds ?? new[] { "al_source_test" },
                parameterSchema ?? new NotificationParameterDefinition[0],
                actions ?? new NotificationActionDefinition[0],
                predecessors ?? new string[0],
                successors ?? new string[0]);
        }

        private static NotificationParameterDefinition Parameter(
            string name,
            NotificationParameterValueKind kind,
            bool required,
            int maximumUtf8Bytes = 0)
        {
            return new NotificationParameterDefinition(
                name,
                kind,
                required,
                maximumUtf8Bytes,
                null,
                null,
                null,
                null,
                false,
                NotificationPrivacyClass.PublicGameplay);
        }

        private static NotificationActionDefinition Action() =>
            new NotificationActionDefinition(
                "al_notify_action_acknowledge",
                NotificationActionKind.Acknowledge,
                new NotificationParameterDefinition[0],
                true,
                true);

        private static NotificationParameter[] LocalizationParameters(string reference) =>
            new[]
            {
                new NotificationParameter(
                    "content_label",
                    NotificationParameterValue.FromLocalizationReference(reference))
            };

        private static NotificationRequest Request(
            FakeClock clock,
            string definitionId = "al_notify_test",
            string correlationId = "test:correlation:1",
            IEnumerable<NotificationParameter> parameters = null,
            string sourceSystemId = "al_source_test",
            DateTime? occurredAtUtc = null,
            NotificationChannel? requestedChannel = null)
        {
            return new NotificationRequest(
                definitionId,
                sourceSystemId,
                correlationId,
                occurredAtUtc ?? clock.UtcNow,
                parameters ?? new NotificationParameter[0],
                requestedChannel,
                null,
                null);
        }

        private static Fixture FixtureWith(params NotificationDefinition[] definitions) =>
            FixtureWith(definitions, new FakeActionRegistry());

        private static Fixture FixtureWith(
            IEnumerable<NotificationDefinition> definitions,
            FakeActionRegistry actionRegistry)
        {
            return FixtureWith(new FakeResolver(definitions), actionRegistry);
        }

        private static Fixture FixtureWith(FakeResolver resolver) =>
            FixtureWith(resolver, new FakeActionRegistry());

        private static Fixture FixtureWith(
            FakeResolver resolver,
            FakeActionRegistry actionRegistry)
        {
            var clock = new FakeClock();
            var diagnostics = new FakeDiagnosticSink();
            var service = new LocalNotificationService(
                resolver,
                resolver,
                clock,
                actionRegistry,
                diagnostics);
            return new Fixture(service, clock, diagnostics);
        }

        private sealed class Fixture
        {
            public Fixture(
                LocalNotificationService service,
                FakeClock clock,
                FakeDiagnosticSink diagnostics)
            {
                Service = service;
                Clock = clock;
                Diagnostics = diagnostics;
            }

            public LocalNotificationService Service { get; }
            public FakeClock Clock { get; }
            public FakeDiagnosticSink Diagnostics { get; }
        }

        private sealed class FakeResolver :
            INotificationDefinitionResolver,
            INotificationLocalizationReferenceAuthority
        {
            private readonly Dictionary<string, NotificationDefinition> _definitions =
                new Dictionary<string, NotificationDefinition>(StringComparer.Ordinal);

            public FakeResolver()
            {
            }

            public FakeResolver(IEnumerable<NotificationDefinition> definitions)
            {
                foreach (NotificationDefinition definition in
                         definitions ?? new NotificationDefinition[0])
                {
                    _definitions.Add(definition.DefinitionId, definition);
                }
            }

            public NotificationDefinitionResolution Override { get; set; }
            public bool LocalizationAuthorityAvailable { get; set; } = true;
            public HashSet<string> LocalizationReferences { get; } =
                new HashSet<string>(StringComparer.Ordinal);

            public bool IsAvailable => LocalizationAuthorityAvailable;

            public bool Contains(string localizationReference) =>
                LocalizationReferences.Contains(localizationReference);

            public NotificationDefinitionResolution Resolve(string definitionId)
            {
                if (Override != null)
                {
                    return Override;
                }

                return definitionId != null &&
                       _definitions.TryGetValue(definitionId, out NotificationDefinition definition)
                    ? new NotificationDefinitionResolution(
                        NotificationDefinitionResolutionStatus.Found,
                        definition,
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
                UtcNow = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
            }

            public DateTime UtcNow { get; private set; }
            public double RealtimeSeconds { get; private set; }

            public void Advance(double seconds)
            {
                UtcNow = UtcNow.AddSeconds(seconds);
                RealtimeSeconds += seconds;
            }

            public void AdvanceRealtime(double seconds)
            {
                RealtimeSeconds += seconds;
            }
        }

        private sealed class FakeDiagnosticSink : INotificationDiagnosticSink
        {
            public readonly List<NotificationDiagnostic> Diagnostics =
                new List<NotificationDiagnostic>();
            public readonly List<LegacyRecord> Legacy = new List<LegacyRecord>();

            public void Record(NotificationDiagnostic diagnostic)
            {
                Diagnostics.Add(diagnostic);
            }

            public void RecordLegacyRaw(string escapedTechnicalText, bool isError)
            {
                Legacy.Add(new LegacyRecord(escapedTechnicalText, isError));
            }
        }

        private sealed class LegacyRecord
        {
            public LegacyRecord(string text, bool isError)
            {
                Text = text;
                IsError = isError;
            }

            public string Text { get; }
            public bool IsError { get; }
        }

        private sealed class FakePresenter : INotificationPresenter
        {
            public FakePresenter(string presenterId)
            {
                PresenterId = presenterId;
            }

            public string PresenterId { get; }
            public bool Throw { get; set; }
            public NotificationPresenterOfferStatus OfferStatus { get; set; } =
                NotificationPresenterOfferStatus.AcceptedPendingPresentation;
            public readonly List<NotificationPresentationOffer> Offers =
                new List<NotificationPresentationOffer>();

            public NotificationPresenterOfferResult Offer(NotificationPresentationOffer offer)
            {
                Offers.Add(offer);
                if (Throw)
                {
                    throw new InvalidOperationException("presenter");
                }

                return new NotificationPresenterOfferResult(
                    OfferStatus,
                    OfferStatus == NotificationPresenterOfferStatus.AcceptedPendingPresentation
                        ? null
                        : "AL-NTF-PRESENTER-FAILED");
            }
        }

        private sealed class FakeObserver : INotificationQueueObserver
        {
            public bool Throw { get; set; }
            public int CallCount { get; private set; }
            public NotificationQueueSnapshot LastSnapshot { get; private set; }

            public void OnQueueChanged(NotificationQueueSnapshot snapshot)
            {
                CallCount++;
                LastSnapshot = snapshot;
                if (Throw)
                {
                    throw new InvalidOperationException("observer");
                }
            }
        }

        private sealed class FakeActionRegistry : INotificationActionRegistry
        {
            public NotificationActionResult Result { get; set; } =
                new NotificationActionResult(
                    NotificationActionStatus.RejectedUnavailable,
                    "AL-NTF-ACTION");
            public int CallCount { get; private set; }

            public NotificationActionResult Invoke(
                NotificationQueueRecordSnapshot record,
                NotificationActionDefinition action,
                NotificationActionInvocation invocation)
            {
                CallCount++;
                return Result;
            }
        }
    }
}
