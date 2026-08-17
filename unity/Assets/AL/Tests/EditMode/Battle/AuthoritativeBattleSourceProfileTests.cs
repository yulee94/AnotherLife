using System.Collections.Generic;
using System.Linq;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Battle.Profiles;
using NUnit.Framework;

namespace AL.Tests.EditMode.Battle
{
    public sealed class AuthoritativeBattleSourceProfileTests
    {
        [Test]
        public void EquivalentAuthoritativeStateProducesEquivalentInputsAndOutcomes()
        {
            var first = CreateState(reverseTroops: false);
            var second = CreateState(reverseTroops: true);

            BattleSourceProfileResult firstBuild = BattleSourceProfileBuilder.Build(first);
            BattleSourceProfileResult secondBuild = BattleSourceProfileBuilder.Build(second);

            Assert.True(firstBuild.IsSuccess, DiagnosticCodes(firstBuild));
            Assert.True(secondBuild.IsSuccess, DiagnosticCodes(secondBuild));
            Assert.AreEqual(
                firstBuild.Request.AttackerArmy.Identity.Sha256,
                secondBuild.Request.AttackerArmy.Identity.Sha256);
            Assert.AreEqual(
                firstBuild.Request.Opponent.Army.Identity.Sha256,
                secondBuild.Request.Opponent.Army.Identity.Sha256);
            Assert.AreEqual(
                firstBuild.Request.Context.Identity.Sha256,
                secondBuild.Request.Context.Identity.Sha256);

            BattleComputationResult firstResult = DeterministicBattleComputation.Compute(firstBuild.Request);
            BattleComputationResult secondResult = DeterministicBattleComputation.Compute(secondBuild.Request);

            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            Assert.AreEqual(firstResult.Value.Outcome, secondResult.Value.Outcome);
            Assert.AreEqual(firstResult.Value.ComputationSha256, secondResult.Value.ComputationSha256);
        }

        [Test]
        public void ConstructionDefensivelyCopiesMutableAuthorityCollections()
        {
            var troops = new List<BattleSourceTroopCount>
            {
                new BattleSourceTroopCount("troop.infantry", 100),
                new BattleSourceTroopCount("troop.ranged", 40)
            };
            var equipment = new List<BattleSnapshotIdentity>
            {
                Identity("equipment.sword")
            };
            BattleAuthoritativeSourceState state = CreateState(
                playerTroops: troops,
                playerEquipment: equipment);

            troops[0] = new BattleSourceTroopCount("troop.siege", 999999);
            equipment.Clear();

            BattleSourceProfileResult build = BattleSourceProfileBuilder.Build(state);

            Assert.True(build.IsSuccess, DiagnosticCodes(build));
            CollectionAssert.AreEqual(
                new[] { "troop.infantry", "troop.ranged" },
                build.Request.AttackerArmy.Stacks.Select(stack => stack.TroopDefinitionId).ToArray());
            Assert.AreEqual(2, state.Player.Troops.Count);
            Assert.AreEqual(1, state.Player.Equipment.EquippedDefinitions.Count);

            BattleComputationResult result = DeterministicBattleComputation.Compute(build.Request);
            Assert.True(result.IsSuccess);
            CollectionAssert.AreEqual(
                new long[] { 100, 40 },
                state.Player.Troops.Select(stack => stack.Count).ToArray());
        }

        [Test]
        public void MissingAndInconsistentAuthorityFailsClosedWithDeterministicDiagnostics()
        {
            BattleSourceProfileResult missing = BattleSourceProfileBuilder.Build(
                new BattleAuthoritativeSourceState(null, null, null));
            BattleAuthoritativeSourceState inconsistentState = CreateState();
            var inconsistentPlayer = new BattleParticipantSourceProfile(
                inconsistentState.Player.Identity,
                inconsistentState.Player.Realm,
                inconsistentState.Player.ArmyIdentity,
                new[] { new BattleSourceTroopCount("troop.unknown", 1) },
                inconsistentState.Player.Equipment,
                inconsistentState.Player.Progression);
            BattleSourceProfileResult inconsistent = BattleSourceProfileBuilder.Build(
                new BattleAuthoritativeSourceState(
                    inconsistentPlayer,
                    inconsistentState.Opponent,
                    inconsistentState.Configuration));

            Assert.False(missing.IsSuccess);
            CollectionAssert.AreEqual(
                new[]
                {
                    "AL-BATTLE-SOURCE-CONFIGURATION-MISSING",
                    "AL-BATTLE-SOURCE-OPPONENT-MISSING",
                    "AL-BATTLE-SOURCE-PLAYER-MISSING"
                },
                missing.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
            Assert.False(inconsistent.IsSuccess);
            CollectionAssert.Contains(
                inconsistent.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                "AL-BATTLE-SOURCE-TROOP-UNKNOWN");
            Assert.IsNull(inconsistent.Request);
        }

        private static BattleAuthoritativeSourceState CreateState(
            bool reverseTroops = false,
            IList<BattleSourceTroopCount> playerTroops = null,
            IList<BattleSnapshotIdentity> playerEquipment = null)
        {
            BattleCatalogSnapshot catalog = BattleContractTestData.Catalog();
            var attackerTroops = playerTroops ?? new List<BattleSourceTroopCount>
            {
                new BattleSourceTroopCount("troop.infantry", 100),
                new BattleSourceTroopCount("troop.ranged", 40)
            };
            var opponentTroops = new List<BattleSourceTroopCount>
            {
                new BattleSourceTroopCount("troop.cavalry", 70),
                new BattleSourceTroopCount("troop.infantry", 80)
            };
            if (reverseTroops)
            {
                attackerTroops = attackerTroops.Reverse().ToList();
                opponentTroops.Reverse();
            }

            var player = Participant(
                "profile.test",
                "army.attacker",
                BattleRealm.Stonehold,
                attackerTroops,
                playerEquipment ?? new[] { Identity("equipment.sword") },
                1_050_000L,
                1_060_000L,
                1_020_000L);
            var opponentParticipant = Participant(
                "profile.opponent",
                "army.opponent",
                BattleRealm.Eldergrove,
                opponentTroops,
                new[] { Identity("equipment.bow") },
                980_000L,
                1_030_000L,
                1_000_000L);
            var opponent = BattleOpponentSourceProfile.ForArmy(opponentParticipant);
            var configuration = new BattleConfigurationSourceProfile(
                BattleContractTestData.GameId,
                BattleContractTestData.CatalogSetId,
                "request.test",
                "battle.test",
                "result.test",
                BattleTechnicalLimits.ExpectedResultConsumerId,
                BattleExecutionMode.Authoritative,
                BattleKind.Pvp,
                BattleTechnicalLimits.PvpBattleTypeId,
                BattleTechnicalLimits.SupportedDeterminismVersion,
                BattleContractTestData.Seed,
                "context.test",
                "encounter.test",
                BattleRealmContextKind.RealmVersusRealm,
                BattleTerrainProfile.Forest,
                catalog,
                BattleMigrationProfiles.CreateRules(
                    BattleContractTestData.CatalogSetId,
                    BattleContractTestData.ContentVersion,
                    BattleContractTestData.SourceRevision),
                BattleMigrationProfiles.CreateRewards(
                    BattleContractTestData.CatalogSetId,
                    BattleContractTestData.ContentVersion,
                    BattleContractTestData.SourceRevision));
            return new BattleAuthoritativeSourceState(player, opponent, configuration);
        }

        private static BattleParticipantSourceProfile Participant(
            string profileId,
            string armyId,
            BattleRealm realm,
            IEnumerable<BattleSourceTroopCount> troops,
            IEnumerable<BattleSnapshotIdentity> equipment,
            long morale,
            long research,
            long commander)
        {
            return new BattleParticipantSourceProfile(
                Identity(profileId),
                realm,
                Identity(armyId),
                troops,
                new BattleEquipmentSourceProfile(
                    Identity(profileId + ".equipment"),
                    equipment,
                    commander),
                new BattleProgressionSourceProfile(
                    Identity(profileId + ".progression"),
                    morale,
                    research));
        }

        private static BattleSnapshotIdentity Identity(string id)
        {
            return new BattleSnapshotIdentity(
                id,
                BattleTechnicalLimits.SupportedSchemaVersion,
                BattleContractTestData.ContentVersion,
                BattleContractTestData.SourceRevision,
                new string('a', 64),
                BattleContractTestData.CatalogSetId);
        }

        private static string DiagnosticCodes(BattleSourceProfileResult result)
        {
            return string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Code));
        }
    }
}
