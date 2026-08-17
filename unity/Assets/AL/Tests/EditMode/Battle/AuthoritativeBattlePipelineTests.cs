using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AL.Battle.Application;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Battle.Profiles;
using AL.Data.Runtime;
using NUnit.Framework;
using UnityEngine;
using System.IO;

namespace AL.Tests.EditMode.Battle
{
    public sealed class AuthoritativeBattlePipelineTests
    {
        [Test]
        public void VictoryBuildsComputesPersistsAndAppliesExactlyOnce()
        {
            PipelineFixture fixture = CreateFixture(playerCount: 200, opponentCount: 5);

            BattlePipelineResult first = fixture.Pipeline.Execute(fixture.Source);
            BattlePipelineResult retry = fixture.Pipeline.Execute(fixture.Source);
            BattleApplicationState state = fixture.Store.Snapshot();

            Assert.That(first.Status, Is.EqualTo(BattlePipelineStatus.Applied));
            Assert.That(first.ComputedResult.Outcome, Is.EqualTo(BattleOutcome.AttackerVictory));
            Assert.That(first.Transaction.State, Is.EqualTo(BattleTransactionState.Applied));
            Assert.That(retry.Status, Is.EqualTo(BattlePipelineStatus.AlreadyApplied));
            Assert.That(state.AppliedResultCount, Is.EqualTo(1));
            Assert.That(state.WinBattleProgress, Is.EqualTo(1));
            Assert.That(state.Credits, Is.EqualTo(first.ComputedResult.RewardProposal.Credits));
            Assert.That(state.Experience, Is.EqualTo(first.ComputedResult.RewardProposal.Experience));
        }

        [Test]
        public void DefeatAppliesLossesAndRewardsWithoutWinProgress()
        {
            PipelineFixture fixture = CreateFixture(playerCount: 5, opponentCount: 200);

            BattlePipelineResult result = fixture.Pipeline.Execute(fixture.Source);
            BattleApplicationState state = fixture.Store.Snapshot();

            Assert.That(result.Status, Is.EqualTo(BattlePipelineStatus.Applied));
            Assert.That(result.ComputedResult.Outcome, Is.EqualTo(BattleOutcome.OpponentVictory));
            Assert.That(state.WinBattleProgress, Is.Zero);
            Assert.That(state.Experience, Is.EqualTo(result.ComputedResult.RewardProposal.Experience));
            Assert.That(state.GetActiveTroops("troop.infantry"),
                Is.EqualTo(result.ComputedResult.AttackerLosses.Single().Survived));
        }

        [Test]
        public void CancellationBeforePersistenceLeavesNoTransactionOrMutation()
        {
            PipelineFixture fixture = CreateFixture(playerCount: 200, opponentCount: 5);
            var cancellation = new CancellationToken(true);

            BattlePipelineResult result = fixture.Pipeline.Execute(fixture.Source, cancellation);

            Assert.That(result.Status, Is.EqualTo(BattlePipelineStatus.Cancelled));
            Assert.That(fixture.Store.Snapshot().AppliedResultCount, Is.Zero);
            Assert.That(fixture.Persistence.ReadSnapshot(), Is.Null);
        }

        [Test]
        public void InvalidSourceFailsClosedBeforePersistenceOrMutation()
        {
            PipelineFixture fixture = CreateFixture(playerCount: 200, opponentCount: 5);
            var invalid = new BattleAuthoritativeSourceState(null, fixture.Source.Opponent, fixture.Source.Configuration);

            BattlePipelineResult result = fixture.Pipeline.Execute(invalid);

            Assert.That(result.Status, Is.EqualTo(BattlePipelineStatus.InvalidSource));
            Assert.That(result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-BATTLE-SOURCE-PLAYER-MISSING"));
            Assert.That(fixture.Store.Snapshot().AppliedResultCount, Is.Zero);
            Assert.That(fixture.Persistence.ReadSnapshot(), Is.Null);
        }

        [Test]
        public void PreviewSourceIsRejectedInsteadOfEnteringAuthoritativePersistence()
        {
            PipelineFixture fixture = CreateFixture(
                playerCount: 200,
                opponentCount: 5,
                mode: BattleExecutionMode.Preview);

            BattlePipelineResult result = fixture.Pipeline.Execute(fixture.Source);

            Assert.That(result.Status, Is.EqualTo(BattlePipelineStatus.InvalidExecutionMode));
            Assert.That(fixture.Store.Snapshot().AppliedResultCount, Is.Zero);
            Assert.That(fixture.Persistence.ReadSnapshot(), Is.Null);
        }

        [Test]
        public void PreviewPipelineComputesWithoutPersistenceOrApplication()
        {
            PipelineFixture fixture = CreateFixture(
                playerCount: 200,
                opponentCount: 5,
                mode: BattleExecutionMode.Preview);

            BattlePreviewResult result = new BattlePreviewPipeline().Execute(fixture.Source);

            Assert.That(
                result.Status,
                Is.EqualTo(BattlePreviewStatus.Computed),
                string.Join(",", result.Diagnostics.Select(value => value.Code + ":" + value.FieldPath)));
            Assert.That(result.ComputedResult.ExecutionMode, Is.EqualTo(BattleExecutionMode.Preview));
            Assert.That(fixture.Store.Snapshot().AppliedResultCount, Is.Zero);
            Assert.That(fixture.Persistence.ReadSnapshot(), Is.Null);
        }

        [Test]
        public void LegacySimulatorContainsNoDirectQuestOrRewardMutation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL/Scripts/Battle/Simulator/DeterministicBattleSimulator.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("UpdateProgress"));
            Assert.That(source, Does.Not.Contain("TryUpdateWinQuest"));
            Assert.That(source, Does.Not.Contain("AddCredits"));
            Assert.That(source, Does.Not.Contain("AddResource"));
        }

        [Test]
        public void ProductionDemoCallerUsesPreviewPipelineInsteadOfLegacySimulator()
        {
            string path = Path.Combine(Application.dataPath, "AL/Scripts/Utilities/DemoInitializer.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain(nameof(BattlePreviewPipeline)));
            Assert.That(source, Does.Contain(nameof(BattlePreviewSourceFactory)));
            Assert.That(source, Does.Not.Contain("Get<IBattleSimulator>()"));
            Assert.That(source, Does.Not.Contain(".Simulate(request)"));
        }

        private static PipelineFixture CreateFixture(
            long playerCount,
            long opponentCount,
            BattleExecutionMode mode = BattleExecutionMode.Authoritative)
        {
            const string contentVersion = "1.0.0";
            const string revision = "battle.caller.migration";
            BattleCatalogSnapshot catalog = BattleMigrationProfiles.CreateCatalog(
                BattleContractTestData.GameId,
                BattleContractTestData.CatalogSetId,
                contentVersion,
                revision);
            BattleParticipantSourceProfile player = Participant(
                "profile.pipeline",
                "army.pipeline.player",
                BattleRealm.Crownlands,
                playerCount,
                catalog,
                contentVersion,
                revision);
            BattleParticipantSourceProfile opponent = Participant(
                "profile.pipeline.opponent",
                "army.pipeline.opponent",
                BattleRealm.Umbral,
                opponentCount,
                catalog,
                contentVersion,
                revision);
            var configuration = new BattleConfigurationSourceProfile(
                BattleContractTestData.GameId,
                BattleContractTestData.CatalogSetId,
                "request.pipeline",
                "battle.pipeline",
                mode == BattleExecutionMode.Preview ? "preview:result.pipeline" : "result.pipeline",
                BattleTechnicalLimits.ExpectedResultConsumerId,
                mode,
                BattleKind.Warzone,
                BattleTechnicalLimits.WarzoneBattleTypeId,
                BattleTechnicalLimits.SupportedDeterminismVersion,
                BattleContractTestData.Seed,
                "context.pipeline",
                "encounter.pipeline",
                BattleRealmContextKind.RealmVersusRealm,
                BattleTerrainProfile.RoadField,
                catalog,
                BattleMigrationProfiles.CreateRules(BattleContractTestData.CatalogSetId, contentVersion, revision),
                BattleMigrationProfiles.CreateRewards(BattleContractTestData.CatalogSetId, contentVersion, revision));
            var source = new BattleAuthoritativeSourceState(
                player,
                BattleOpponentSourceProfile.ForArmy(opponent),
                configuration);
            var store = new InMemoryBattleResultStore(
                BattleApplicationState.CreateForActiveBattle(
                    player.Identity.Id,
                    configuration.BattleRequestId,
                    configuration.BattleId,
                    configuration.BattleResultId,
                    configuration.CatalogSetId,
                    new Dictionary<string, long> { { "troop.infantry", playerCount } }));
            var persistence = new InMemoryBattleTransactionPersistence();
            var coordinator = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(store));
            return new PipelineFixture(source, store, persistence, new AuthoritativeBattlePipeline(coordinator));
        }

        private static BattleParticipantSourceProfile Participant(
            string profileId,
            string armyId,
            BattleRealm realm,
            long count,
            BattleCatalogSnapshot catalog,
            string contentVersion,
            string revision)
        {
            return new BattleParticipantSourceProfile(
                Identity(profileId, catalog, contentVersion, revision),
                realm,
                Identity(armyId, catalog, contentVersion, revision),
                new[] { new BattleSourceTroopCount("troop.infantry", count) },
                new BattleEquipmentSourceProfile(
                    Identity(profileId + ".equipment", catalog, contentVersion, revision),
                    Array.Empty<BattleSnapshotIdentity>(),
                    BattleTechnicalLimits.MicrosPerUnit),
                new BattleProgressionSourceProfile(
                    Identity(profileId + ".progression", catalog, contentVersion, revision),
                    BattleTechnicalLimits.MicrosPerUnit,
                    BattleTechnicalLimits.MicrosPerUnit));
        }

        private static BattleSnapshotIdentity Identity(
            string id,
            BattleCatalogSnapshot catalog,
            string contentVersion,
            string revision)
        {
            return new BattleSnapshotIdentity(
                id,
                BattleTechnicalLimits.SupportedSchemaVersion,
                contentVersion,
                revision,
                new string('a', 64),
                catalog.Identity.CatalogSetId);
        }

        private sealed class PipelineFixture
        {
            public PipelineFixture(
                BattleAuthoritativeSourceState source,
                InMemoryBattleResultStore store,
                InMemoryBattleTransactionPersistence persistence,
                AuthoritativeBattlePipeline pipeline)
            {
                Source = source;
                Store = store;
                Persistence = persistence;
                Pipeline = pipeline;
            }

            public BattleAuthoritativeSourceState Source { get; }
            public InMemoryBattleResultStore Store { get; }
            public InMemoryBattleTransactionPersistence Persistence { get; }
            public AuthoritativeBattlePipeline Pipeline { get; }
        }
    }
}
