using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using AL.ChampionMode.C1;
using AL.ChampionMode.Integration;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionEncounterIntegration
{
    public sealed class ChampionEncounterApplicationGatewayTests
    {
        [Test]
        public void CurrentAuthoritativeDispositionFailsClosedWithoutCallingApplicationOwner()
        {
            var application = new RecordingApplication();

            ChampionEncounterApplicationPlan first =
                ChampionEncounterApplicationGateway.Apply(
                    ChampionEncounterSourceEnvelope.BlockedRequired(
                        "phase-c7a",
                        "b5426cc9b52cbb06bb3c3987f597867fbe010f42"),
                    application,
                    Array.Empty<ChampionEncounterApplicationReceipt>());
            ChampionEncounterApplicationPlan second =
                ChampionEncounterApplicationGateway.Apply(
                    ChampionEncounterSourceEnvelope.BlockedRequired(
                        "phase-c7a",
                        "b5426cc9b52cbb06bb3c3987f597867fbe010f42"),
                    application,
                    Array.Empty<ChampionEncounterApplicationReceipt>());

            Assert.That(first.Status, Is.EqualTo(ChampionEncounterApplicationStatus.CatalogUnavailable));
            Assert.That(second.Status, Is.EqualTo(first.Status));
            Assert.That(second.DiagnosticCode, Is.EqualTo(first.DiagnosticCode));
            Assert.That(first.Receipt, Is.Null);
            Assert.That(application.CallCount, Is.Zero);
        }

        [Test]
        public void MissingOrMalformedSourceFailsWithoutRuntimeMutation()
        {
            var application = new RecordingApplication();

            ChampionEncounterApplicationPlan missing =
                ChampionEncounterApplicationGateway.Apply(
                    null,
                    application,
                    Array.Empty<ChampionEncounterApplicationReceipt>());
            ChampionEncounterApplicationPlan malformed =
                ChampionEncounterApplicationGateway.Apply(
                    ChampionEncounterSourceEnvelope.PublishedForTests(
                        "phase-c7a",
                        "not-a-commit",
                        null,
                        null),
                    application,
                    Array.Empty<ChampionEncounterApplicationReceipt>());

            Assert.That(missing.Status, Is.EqualTo(ChampionEncounterApplicationStatus.InvalidSource));
            Assert.That(malformed.Status, Is.EqualTo(ChampionEncounterApplicationStatus.InvalidSource));
            Assert.That(application.CallCount, Is.Zero);
        }

        [Test]
        public void PreparedC1AndC1bSnapshotsApplyOnceAndReplayDeterministically()
        {
            ChampionEncounterSourceEnvelope source = ValidPublishedSource();
            var application = new RecordingApplication();

            ChampionEncounterApplicationPlan applied =
                ChampionEncounterApplicationGateway.Apply(
                    source,
                    application,
                    Array.Empty<ChampionEncounterApplicationReceipt>());
            ChampionEncounterApplicationPlan duplicate =
                ChampionEncounterApplicationGateway.Apply(
                    source,
                    application,
                    new[] { applied.Receipt });

            Assert.That(applied.Status, Is.EqualTo(ChampionEncounterApplicationStatus.Applied));
            Assert.That(applied.Receipt, Is.Not.Null);
            Assert.That(duplicate.Status, Is.EqualTo(ChampionEncounterApplicationStatus.DuplicateExact));
            Assert.That(duplicate.Receipt, Is.SameAs(applied.Receipt));
            Assert.That(application.CallCount, Is.EqualTo(1));
            Assert.That(application.LastState.State, Is.EqualTo(CombatEncounterState.Created));
        }

        [Test]
        public void ReusedApplicationIdentityWithChangedSourceIsRejected()
        {
            ChampionEncounterSourceEnvelope source = ValidPublishedSource();
            var application = new RecordingApplication();
            ChampionEncounterApplicationPlan applied =
                ChampionEncounterApplicationGateway.Apply(
                    source,
                    application,
                    Array.Empty<ChampionEncounterApplicationReceipt>());
            ChampionEncounterSourceEnvelope changed =
                ChampionEncounterSourceEnvelope.PublishedForTests(
                    "phase-c7a",
                    "b5426cc9b52cbb06bb3c3987f597867fbe010f42",
                    source.EncounterPlan,
                    source.SkillLoadSession,
                    sourceRevision: "source-r2");

            ChampionEncounterApplicationPlan conflict =
                ChampionEncounterApplicationGateway.Apply(
                    changed,
                    application,
                    new[] { applied.Receipt });

            Assert.That(conflict.Status, Is.EqualTo(ChampionEncounterApplicationStatus.CorrelationConflict));
            Assert.That(application.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplicationFailureReturnsTypedNonAppliedResult()
        {
            var application = new RecordingApplication { ShouldApply = false };

            ChampionEncounterApplicationPlan result =
                ChampionEncounterApplicationGateway.Apply(
                    ValidPublishedSource(),
                    application,
                    Array.Empty<ChampionEncounterApplicationReceipt>());

            Assert.That(result.Status, Is.EqualTo(ChampionEncounterApplicationStatus.ApplicationRejected));
            Assert.That(result.Receipt, Is.Null);
            Assert.That(application.CallCount, Is.EqualTo(1));
        }

        private static ChampionEncounterSourceEnvelope ValidPublishedSource()
        {
            ChampionEncounterRequestPlan encounterPlan =
                ChampionEncounterPlanner.PlanRequest(
                    Definition(),
                    Request(),
                    Array.Empty<ChampionEncounterRequestCorrelation>());
            Assert.That(encounterPlan.Status, Is.EqualTo(ChampionEncounterRequestStatus.Resolved));

            ValidatedCombatSkillLoadoutSnapshot published =
                (ValidatedCombatSkillLoadoutSnapshot)
                FormatterServices.GetUninitializedObject(
                    typeof(ValidatedCombatSkillLoadoutSnapshot));
            CombatSkillLoadSessionSnapshot loaded =
                (CombatSkillLoadSessionSnapshot)Activator.CreateInstance(
                    typeof(CombatSkillLoadSessionSnapshot),
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new object[]
                    {
                        Stable("owner.test"),
                        1L,
                        CombatSkillLoadStatus.Loaded,
                        null,
                        published,
                        new CombatValidationResult(Array.Empty<CombatDiagnostic>()),
                        null
                    },
                    null);

            return ChampionEncounterSourceEnvelope.PublishedForTests(
                "phase-c7a",
                "b5426cc9b52cbb06bb3c3987f597867fbe010f42",
                encounterPlan,
                loaded);
        }

        private static ChampionEncounterDefinitionSnapshot Definition()
        {
            return new ChampionEncounterDefinitionSnapshot(
                "another-life", "catalog.test", "profile.test", "encounter.test",
                "1", "content-v1", CombatEncounterMode.AuthoritativeQuest,
                "champion.test", "champion.profile.test", "loadout.test",
                "boss.test", "boss.profile.test", "rules.test", "arena.test",
                "realm.neutral", "realm-v1", new[] { "realm.stone" },
                "profile-r1", false, false, true, true);
        }

        private static ChampionEncounterRequest Request()
        {
            return new ChampionEncounterRequest(
                "another-life", "catalog.test", "profile.test", "encounter.test",
                "content-v1", "session.test", "attempt.test", "result.test",
                CombatEncounterMode.AuthoritativeQuest, "champion.test",
                "champion.profile.test", "loadout.test", "boss.test",
                "boss.profile.test", "realm.stone", "realm-v1", "quest.test",
                "reward.test", string.Empty, "profile-r1");
        }

        private static CombatStableId Stable(string value)
        {
            Assert.That(CombatStableId.TryCreate(value, out CombatStableId id), Is.True);
            return id;
        }

        private sealed class RecordingApplication : IChampionEncounterApplication
        {
            public int CallCount { get; private set; }
            public ChampionEncounterStateSnapshot LastState { get; private set; }
            public bool ShouldApply { get; set; } = true;

            public bool TryApply(ChampionEncounterStateSnapshot state)
            {
                CallCount++;
                LastState = state;
                return ShouldApply;
            }
        }
    }
}
