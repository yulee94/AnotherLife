using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces.WorldState;
using AL.Services.WorldState;
using NUnit.Framework;

namespace AL.Tests.EditMode.WorldState
{
    public class WorldStateValidationTests
    {
        [Test]
        public void ValidDefinitionAndActiveSnapshotPassStrictValidation()
        {
            WorldEventDefinition definition = WorldStateTestFixtures.Definition();
            var registry = new WorldEffectConsumerRegistry(
                new[] { new FakeConsumer(WorldStateTestFixtures.ConsumerId) });

            WorldStateDefinitionValidationResult definitionResult =
                WorldStateValidator.ValidateDefinition(definition, registry);
            WorldStateInstanceValidationResult snapshotResult =
                WorldStateValidator.ValidateSnapshot(
                    WorldStateTestFixtures.ActiveSnapshot(
                        WorldStateTestFixtures.ActiveInstance(definition)));

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.Valid,
                definitionResult.Status);
            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Valid,
                snapshotResult.Status);
            Assert.IsEmpty(definitionResult.Diagnostics);
            Assert.IsEmpty(snapshotResult.Diagnostics);
        }

        [Test]
        public void DefinitionCopiesCallerCollectionsAndExposesReadOnlyViews()
        {
            var aliases = new List<string> { "LegacyTestCondition" };
            var effects = new List<WorldEffectDescriptor>
            {
                WorldStateTestFixtures.Effect()
            };
            WorldEventDefinition definition = WorldStateTestFixtures.Definition(
                aliases: aliases,
                effects: effects);

            aliases.Clear();
            effects.Clear();

            Assert.AreEqual(1, definition.LegacyAliases.Count);
            Assert.AreEqual(1, definition.EffectDescriptors.Count);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)definition.LegacyAliases).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList)definition.EffectDescriptors).Clear());
        }

        [TestCase("", WorldStateDefinitionValidationStatus.InvalidId)]
        [TestCase("AL_WORLD_EVENT_UPPER", WorldStateDefinitionValidationStatus.InvalidId)]
        [TestCase("al_world_event_bad__id", WorldStateDefinitionValidationStatus.InvalidId)]
        [TestCase("al_world_event_control_suffix\n", WorldStateDefinitionValidationStatus.InvalidId)]
        public void DefinitionIdGrammarFailsClosed(
            string definitionId,
            WorldStateDefinitionValidationStatus expected)
        {
            WorldStateDefinitionValidationResult result =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(definitionId: definitionId),
                    Registry());

            Assert.AreEqual(expected, result.Status);
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "AL-WST-ID"));
        }

        [Test]
        public void StrictGrammarRejectsControlSuffixedCatalogValues()
        {
            AssertInvalid(WorldStateTestFixtures.Definition(
                contentVersion: "content_v1\n"));
            AssertInvalid(WorldStateTestFixtures.Definition(
                sourceRevision: "source_v1\n"));
            AssertInvalid(WorldStateTestFixtures.Definition(
                aliases: new[] { "LegacyCondition\n" }));
            AssertInvalid(WorldStateTestFixtures.Definition(
                allowedSources: new[] { "al_world_source_test\n" }));
            AssertInvalid(WorldStateTestFixtures.Definition(
                startNotification: "al_notify_world_started\n"));
            AssertInvalid(WorldStateTestFixtures.Definition(
                contentReference: "world.events.test_condition\n"));
            AssertInvalid(WorldStateTestFixtures.Definition(
                effects: new[]
                {
                    WorldStateTestFixtures.Effect(
                        effectId: "al_world_effect_test_modifier\n")
                }));
            AssertInvalid(WorldStateTestFixtures.Definition(
                effects: new[]
                {
                    WorldStateTestFixtures.Effect(
                        parameters: new[]
                        {
                            WorldEffectParameter.Number("multiplier\n", 1.25d)
                        })
                }));

            const string invalidConsumerId =
                "al_world_consumer_test_domain\n";
            WorldStateDefinitionValidationResult consumer =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        effects: new[]
                        {
                            WorldStateTestFixtures.Effect(
                                consumerId: invalidConsumerId)
                        },
                        requiredConsumers: new[] { invalidConsumerId }),
                    new WorldEffectConsumerRegistry(
                        new[] { new FakeConsumer(invalidConsumerId) }));
            Assert.AreNotEqual(
                WorldStateDefinitionValidationStatus.Valid,
                consumer.Status);
        }

        [Test]
        public void UnsupportedVersionAndInvalidEnvelopeAreRejected()
        {
            WorldStateDefinitionValidationResult version =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(schemaVersion: 2),
                    Registry());
            WorldStateDefinitionValidationResult envelope =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        scope: (WorldEventScope)99,
                        priority: 1001,
                        allowedSources: Array.Empty<string>()),
                    Registry());

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.UnsupportedVersion,
                version.Status);
            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.InvalidEnvelope,
                envelope.Status);
        }

        [Test]
        public void DurationAndExclusivePoliciesAreStrict()
        {
            WorldStateDefinitionValidationResult duration =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        durationPolicy: new WorldEventDurationPolicy(
                            600L,
                            60L,
                            300L,
                            true)),
                    Registry());
            WorldStateDefinitionValidationResult exclusive =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        exclusiveGroup: "another_group"),
                    Registry());

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.InvalidDurationPolicy,
                duration.Status);
            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.InvalidExclusivePolicy,
                exclusive.Status);
        }

        [Test]
        public void DefinitionCatalogRejectsDuplicateIdsAndAliasShadowing()
        {
            WorldEventDefinition first = WorldStateTestFixtures.Definition(
                aliases: new[] { "LegacyOne" });
            WorldEventDefinition duplicate = WorldStateTestFixtures.Definition(
                aliases: new[] { "LegacyTwo" });
            WorldEventDefinition shadow = WorldStateTestFixtures.Definition(
                definitionId: "al_world_event_second_condition",
                aliases: new[] { WorldStateTestFixtures.DefinitionId });

            WorldStateDefinitionValidationResult duplicateResult =
                WorldStateValidator.ValidateDefinitions(
                    new[] { first, duplicate },
                    Registry());
            WorldStateDefinitionValidationResult shadowResult =
                WorldStateValidator.ValidateDefinitions(
                    new[] { first, shadow },
                    Registry());

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.DuplicateId,
                duplicateResult.Status);
            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.AliasCollision,
                shadowResult.Status);
        }

        [Test]
        public void GameplayDefinitionRequiresEffectsButExplicitPresentationOnlyMayBeEmpty()
        {
            WorldStateDefinitionValidationResult gameplay =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        effects: Array.Empty<WorldEffectDescriptor>(),
                        requiredConsumers: Array.Empty<string>(),
                        optionalConsumers: Array.Empty<string>()),
                    new WorldEffectConsumerRegistry(
                        Array.Empty<IWorldEffectConsumer>()));
            WorldStateDefinitionValidationResult presentation =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        presentationOnly: true,
                        effects: Array.Empty<WorldEffectDescriptor>(),
                        requiredConsumers: Array.Empty<string>(),
                        optionalConsumers: Array.Empty<string>()),
                    new WorldEffectConsumerRegistry(
                        Array.Empty<IWorldEffectConsumer>()));

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.InvalidEffect,
                gameplay.Status);
            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.Valid,
                presentation.Status);
        }

        [Test]
        public void DuplicateEffectOrderAndNonFiniteParameterAreRejected()
        {
            WorldEffectDescriptor first = WorldStateTestFixtures.Effect();
            WorldEffectDescriptor second = WorldStateTestFixtures.Effect(
                effectId: "al_world_effect_second_modifier",
                applicationOrder: 0,
                removalOrder: 1,
                parameters: new[]
                {
                    WorldEffectParameter.Number("multiplier", double.NaN)
                });
            WorldStateDefinitionValidationResult result =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        effects: new[] { first, second },
                        requiredConsumers: new[]
                        {
                            WorldStateTestFixtures.ConsumerId
                        }),
                    Registry());

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.InvalidEffect,
                result.Status);
        }

        [Test]
        public void MissingRequiredConsumerRejectsButMissingOptionalConsumerIsAccepted()
        {
            WorldStateDefinitionValidationResult required =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(),
                    new WorldEffectConsumerRegistry(
                        Array.Empty<IWorldEffectConsumer>()));
            WorldEffectDescriptor optionalEffect =
                WorldStateTestFixtures.Effect(
                    effectId: WorldStateTestFixtures.OptionalEffectId,
                    consumerId: WorldStateTestFixtures.OptionalConsumerId,
                    required: false);
            WorldStateDefinitionValidationResult optional =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        effects: new[] { optionalEffect },
                        requiredConsumers: Array.Empty<string>(),
                        optionalConsumers: new[]
                        {
                            WorldStateTestFixtures.OptionalConsumerId
                        }),
                    new WorldEffectConsumerRegistry(
                        Array.Empty<IWorldEffectConsumer>()));

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.MissingRequiredConsumer,
                required.Status);
            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.Valid,
                optional.Status);
        }

        [Test]
        public void NotificationAndContentReferencesCannotBeBlankOrRawCopy()
        {
            WorldStateDefinitionValidationResult notification =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(startNotification: ""),
                    Registry());
            WorldStateDefinitionValidationResult content =
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(
                        contentReference: "Player-facing raw sentence"),
                    Registry());

            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.InvalidNotificationReference,
                notification.Status);
            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.InvalidContentReference,
                content.Status);
        }

        [Test]
        public void ActiveAndTerminalTimestampRulesAreDistinct()
        {
            WorldEventInstance active = WorldStateTestFixtures.ActiveInstance();
            WorldEventInstance invalidTerminal = new WorldEventInstance(
                active.InstanceId,
                active.DefinitionId,
                active.DefinitionSchemaVersion,
                active.DefinitionContentVersion,
                active.DefinitionSourceRevision,
                active.CorrelationId,
                active.OperationId,
                active.SourceSystemId,
                active.ExclusiveGroup,
                WorldEventInstanceState.Ended,
                active.ScheduledAtUtcSeconds,
                active.StartedAtUtcSeconds,
                active.ExpectedEndAtUtcSeconds,
                0L,
                WorldEventCompletionReason.None,
                2L,
                active.ResolvedEffects,
                2L);

            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Valid,
                WorldStateValidator.ValidateInstance(active).Status);
            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Invalid,
                WorldStateValidator.ValidateInstance(invalidTerminal).Status);
        }

        [Test]
        public void FutureInstanceIsPreservedReadOnlyRatherThanGuessed()
        {
            WorldEventInstance active = WorldStateTestFixtures.ActiveInstance();
            var future = new WorldEventInstance(
                active.InstanceId,
                active.DefinitionId,
                2,
                "content_v2",
                "source_v2",
                active.CorrelationId,
                active.OperationId,
                active.SourceSystemId,
                active.ExclusiveGroup,
                WorldEventInstanceState.Active,
                active.ScheduledAtUtcSeconds,
                active.StartedAtUtcSeconds,
                active.ExpectedEndAtUtcSeconds,
                0L,
                WorldEventCompletionReason.None,
                active.Revision,
                active.ResolvedEffects,
                active.CommittedEffectRevision);

            WorldStateInstanceValidationResult result =
                WorldStateValidator.ValidateInstance(future);

            Assert.AreEqual(
                WorldStateInstanceValidationStatus.PreservedUnsupportedFuture,
                result.Status);
            Assert.AreEqual(
                WorldStateDiagnosticSeverity.Warning,
                result.Diagnostics[0].Severity);
        }

        [Test]
        public void SnapshotRejectsMultipleActiveInstancesAndDuplicateHistoryIdentity()
        {
            WorldEventInstance first = WorldStateTestFixtures.ActiveInstance();
            WorldEventInstance second = WorldStateTestFixtures.ActiveInstance(
                instanceId: "world-instance-002");
            WorldStateSnapshot multiple = WorldStateTestFixtures.ActiveSnapshot(
                first,
                extraActive: new[] { second });
            WorldStateSnapshot duplicateHistory = new WorldStateSnapshot(
                WorldStateSnapshotStatus.AvailableActive,
                3L,
                WorldStateTestFixtures.PolicyRevision,
                WorldStateTestFixtures.CatalogRevision,
                new[] { first },
                new[] { WorldStateTestFixtures.CompletedInstance(instanceId: first.InstanceId) },
                1L,
                true,
                WorldStateTestFixtures.NowUtcSeconds - 10L,
                Array.Empty<WorldStateOperationReceipt>(),
                Array.Empty<WorldStateDiagnostic>());

            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Invalid,
                WorldStateValidator.ValidateSnapshot(multiple).Status);
            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Invalid,
                WorldStateValidator.ValidateSnapshot(duplicateHistory).Status);
        }

        [Test]
        public void SnapshotRejectsDuplicateOperationAndCorrelationReceipts()
        {
            WorldEventInstance completed = WorldStateTestFixtures.CompletedInstance();
            string hash = new string('a', 64);
            var first = new WorldStateOperationReceipt(
                "operation-one",
                "correlation-one",
                hash,
                WorldStateTransitionKind.End,
                completed.InstanceId,
                4L,
                completed);
            var second = new WorldStateOperationReceipt(
                "operation-one",
                "correlation-two",
                hash,
                WorldStateTransitionKind.End,
                completed.InstanceId,
                4L,
                completed);
            WorldStateSnapshot snapshot = WorldStateTestFixtures.EmptySnapshot(
                revision: 4L,
                receipts: new[] { first, second },
                history: new[] { completed });

            WorldStateInstanceValidationResult result =
                WorldStateValidator.ValidateSnapshot(snapshot);

            Assert.AreEqual(WorldStateInstanceValidationStatus.Invalid, result.Status);
            Assert.IsTrue(result.Diagnostics.Any(item =>
                item.Code == "AL-WST-CORRELATION-LEDGER"));
        }

        [Test]
        public void SnapshotRejectsReceiptThatContradictsCurrentCollections()
        {
            WorldEventInstance active =
                WorldStateTestFixtures.ActiveInstance();
            WorldEventInstance completed =
                WorldStateTestFixtures.CompletedInstance(
                    instanceId: active.InstanceId);
            var terminalReceipt = new WorldStateOperationReceipt(
                "operation-end",
                "correlation-end",
                new string('a', 64),
                WorldStateTransitionKind.End,
                active.InstanceId,
                4L,
                completed);
            var startReceipt = new WorldStateOperationReceipt(
                "operation-start",
                "correlation-start",
                new string('b', 64),
                WorldStateTransitionKind.Start,
                active.InstanceId,
                4L,
                active);

            WorldStateInstanceValidationResult terminalConflict =
                WorldStateValidator.ValidateSnapshot(
                    WorldStateTestFixtures.ActiveSnapshot(
                        active,
                        revision: 4L,
                        receipts: new[] { terminalReceipt }));
            WorldStateInstanceValidationResult missingCurrentStart =
                WorldStateValidator.ValidateSnapshot(
                    WorldStateTestFixtures.EmptySnapshot(
                        revision: 4L,
                        receipts: new[] { startReceipt }));

            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Invalid,
                terminalConflict.Status);
            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Invalid,
                missingCurrentStart.Status);
        }

        [Test]
        public void SnapshotRejectsReceiptWhoseResultDoesNotMatchLedger()
        {
            WorldEventInstance completed =
                WorldStateTestFixtures.CompletedInstance();
            var receipt = new WorldStateOperationReceipt(
                "operation-one",
                "correlation-one",
                new string('a', 64),
                WorldStateTransitionKind.Start,
                "different-instance",
                3L,
                completed);

            WorldStateInstanceValidationResult result =
                WorldStateValidator.ValidateSnapshot(
                    WorldStateTestFixtures.EmptySnapshot(
                        receipts: new[] { receipt }));

            Assert.AreEqual(
                WorldStateInstanceValidationStatus.Invalid,
                result.Status);
            Assert.IsTrue(result.Diagnostics.Any(item =>
                item.Code == "AL-WST-CORRELATION-LEDGER"));
        }

        [Test]
        public void SnapshotHistoryAndReceiptCollectionsAreBounded()
        {
            WorldEventInstance[] history = Enumerable.Range(0, 51)
                .Select(index => WorldStateTestFixtures.CompletedInstance(
                    instanceId: "world-completed-" + index))
                .ToArray();
            WorldStateSnapshot snapshot = WorldStateTestFixtures.EmptySnapshot(
                history: history);

            WorldStateInstanceValidationResult result =
                WorldStateValidator.ValidateSnapshot(snapshot);

            Assert.AreEqual(WorldStateInstanceValidationStatus.Invalid, result.Status);
        }

        [Test]
        public void ConsumerRegistryRejectsDuplicateAndUnavailableRequiredConsumers()
        {
            var duplicate = new WorldEffectConsumerRegistry(new[]
            {
                new FakeConsumer(WorldStateTestFixtures.ConsumerId),
                new FakeConsumer(WorldStateTestFixtures.ConsumerId)
            });
            var unavailableConsumer = new FakeConsumer(
                WorldStateTestFixtures.ConsumerId)
            {
                IsAvailable = false
            };
            var unavailable = new WorldEffectConsumerRegistry(
                new[] { unavailableConsumer });

            Assert.IsFalse(duplicate.IsValid);
            Assert.IsFalse(duplicate.TryGetAvailable(
                WorldStateTestFixtures.ConsumerId,
                out IWorldEffectConsumer _));
            Assert.AreEqual(
                WorldStateDefinitionValidationStatus.MissingRequiredConsumer,
                WorldStateValidator.ValidateDefinition(
                    WorldStateTestFixtures.Definition(),
                    unavailable).Status);
        }

        private static WorldEffectConsumerRegistry Registry()
        {
            return new WorldEffectConsumerRegistry(
                new[] { new FakeConsumer(WorldStateTestFixtures.ConsumerId) });
        }

        private static void AssertInvalid(WorldEventDefinition definition)
        {
            Assert.AreNotEqual(
                WorldStateDefinitionValidationStatus.Valid,
                WorldStateValidator.ValidateDefinition(
                    definition,
                    Registry()).Status);
        }
    }
}
