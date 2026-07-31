using System;
using System.Collections.Generic;
using System.Linq;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Battle.Validation;
using NUnit.Framework;

namespace AL.Tests.EditMode.Battle
{
    public class BattleContractValidationTests
    {
        [Test]
        public void ValidAuthoritativeAndPreviewRequestsAreAccepted()
        {
            BattleValidationResult authoritative = BattleRequestValidator.Validate(
                BattleContractTestData.Request());
            BattleValidationResult preview = BattleRequestValidator.Validate(
                BattleContractTestData.Request(
                    mode: BattleExecutionMode.Preview,
                    resultId: "preview:result.test"));

            Assert.That(authoritative.IsValid, Is.True);
            Assert.That(authoritative.Diagnostics, Is.Empty);
            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Diagnostics, Is.Empty);
        }

        [TestCase(BattleKind.Pve, BattleTechnicalLimits.PveBattleTypeId)]
        [TestCase(BattleKind.Pvp, BattleTechnicalLimits.PvpBattleTypeId)]
        [TestCase(BattleKind.Boss, BattleTechnicalLimits.BossBattleTypeId)]
        [TestCase(BattleKind.Warzone, BattleTechnicalLimits.WarzoneBattleTypeId)]
        public void StableBattleTypeMatrixIsAccepted(BattleKind kind, string typeId)
        {
            BattleValidationResult result = BattleRequestValidator.Validate(
                BattleContractTestData.Request(kind: kind, battleTypeId: typeId));

            Assert.That(result.IsValid, Is.True, string.Join(",", result.Diagnostics.Select(d => d.Code)));
        }

        [Test]
        public void UnknownModeAndTypeRejectWithoutProducingAResult()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleComputationResult mode = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, mode: (BattleExecutionMode)99));
            BattleComputationResult type = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(
                    valid,
                    kind: (BattleKind)99,
                    typeId: "battle.future"));

            Assert.That(mode.Status, Is.EqualTo(BattleComputationStatus.UnsupportedExecutionMode));
            Assert.That(mode.Value, Is.Null);
            Assert.That(type.Status, Is.EqualTo(BattleComputationStatus.UnsupportedBattleType));
            Assert.That(type.Value, Is.Null);
        }

        [TestCase(BattleExecutionMode.Preview, "result.authoritative")]
        [TestCase(BattleExecutionMode.Authoritative, "preview:result.test")]
        public void PreviewNamespaceCannotCrossExecutionModes(
            BattleExecutionMode mode,
            string resultId)
        {
            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(mode: mode, resultId: resultId));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidRequest));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-RESULT-NAMESPACE-MISMATCH"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("short")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        public void InvalidOrNoncanonicalSeedIsRejected(string seed)
        {
            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(seedHex: seed));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidRequest));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-SEED-INVALID"));
        }

        [Test]
        public void StableIdsAreCaseSensitiveBoundedUtf8AndControlFree()
        {
            Assert.That(BattleRequestValidator.IsStableId("Battle.Id"), Is.True);
            Assert.That(BattleRequestValidator.IsStableId("battle.id"), Is.True);
            Assert.That(StringComparer.Ordinal.Equals("Battle.Id", "battle.id"), Is.False);
            Assert.That(BattleRequestValidator.IsStableId("battle\ninvalid"), Is.False);
            Assert.That(BattleRequestValidator.IsStableId(new string('x', 129)), Is.False);
            Assert.That(BattleRequestValidator.IsStableId("전투.id"), Is.True);
            Assert.That(BattleRequestValidator.IsStableId("battle.\ud800"), Is.False);
        }

        [Test]
        public void UnsupportedDeterminismVersionRejectsBeforeComputation()
        {
            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(determinismVersion: "battle_sha256_v2"));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.UnsupportedVersion));
            Assert.That(result.Value, Is.Null);
        }

        [Test]
        public void MissingBattleIdentityAndWrongExpectedConsumerReject()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleComputationResult missingBattle = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, battleId: string.Empty));
            BattleComputationResult wrongConsumer = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, consumerId: "consumer.wrong"));

            Assert.That(missingBattle.Status, Is.EqualTo(BattleComputationStatus.InvalidRequest));
            Assert.That(missingBattle.Diagnostics.Select(item => item.FieldPath),
                Contains.Item("request.battleId"));
            Assert.That(wrongConsumer.Status, Is.EqualTo(BattleComputationStatus.InvalidRequest));
            Assert.That(wrongConsumer.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-EXPECTED-CONSUMER-MISMATCH"));
        }

        [Test]
        public void ContractCollectionsCopyInputsAndExposeNoMutableArray()
        {
            BattleCatalogSnapshot catalog = BattleContractTestData.Catalog();
            var source = new[]
            {
                new BattleTroopStack(
                    catalog.TroopDefinitions[0].Identity.Id,
                    catalog.TroopDefinitions[0].Identity.ContentVersion,
                    catalog.TroopDefinitions[0].Identity.Sha256,
                    10L)
            };
            var identity = new BattleSnapshotIdentity(
                "army.copy_test",
                BattleTechnicalLimits.SupportedSchemaVersion,
                BattleContractTestData.ContentVersion,
                BattleContractTestData.SourceRevision,
                new string('0', 64),
                BattleContractTestData.CatalogSetId);
            var army = new BattleArmySnapshot(identity, source);
            source[0] = null;

            Assert.That(army.Stacks[0], Is.Not.Null);
            Assert.That(army.Stacks as BattleTroopStack[], Is.Null);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<BattleTroopStack>)army.Stacks)[0] = null);
        }

        [Test]
        public void DuplicateAndNoncanonicalStacksRejectEvenWithMatchingArmyHash()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleTroopStack first = valid.AttackerArmy.Stacks[0];
            BattleArmySnapshot duplicate = BattleContractTestData.ArmyWithStacks(
                valid.AttackerArmy,
                new[] { first, first });

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, attacker: duplicate));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-ARMY-STACKS-NONCANONICAL"));
        }

        [Test]
        public void NullCatalogDefinitionAndArmyStackRejectWithoutThrowing()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            var catalogWithNull = new BattleCatalogSnapshot(
                valid.Catalog.Identity,
                new BattleTroopDefinition[] { null });
            var armyWithNull = new BattleArmySnapshot(
                valid.AttackerArmy.Identity,
                new BattleTroopStack[] { null });

            BattleComputationResult catalogResult = null;
            BattleComputationResult armyResult = null;
            Assert.DoesNotThrow(() => catalogResult = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, catalog: catalogWithNull)));
            Assert.DoesNotThrow(() => armyResult = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, attacker: armyWithNull)));

            Assert.That(catalogResult.Status, Is.EqualTo(BattleComputationStatus.CatalogUnavailable));
            Assert.That(catalogResult.Value, Is.Null);
            Assert.That(armyResult.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(armyResult.Value, Is.Null);
        }

        [Test]
        public void ArmyCountMustBePositiveAndWithinTechnicalCeiling()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleTroopStack template = valid.AttackerArmy.Stacks[0];
            var zero = new BattleTroopStack(
                template.TroopDefinitionId,
                template.TroopContentVersion,
                template.TroopDefinitionSha256,
                0);
            var over = new BattleTroopStack(
                template.TroopDefinitionId,
                template.TroopContentVersion,
                template.TroopDefinitionSha256,
                BattleTechnicalLimits.MaximumArmyCount + 1);

            BattleComputationResult zeroResult = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(
                    valid,
                    attacker: BattleContractTestData.ArmyWithStacks(valid.AttackerArmy, new[] { zero })));
            BattleComputationResult overResult = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(
                    valid,
                    attacker: BattleContractTestData.ArmyWithStacks(valid.AttackerArmy, new[] { over })));

            Assert.That(zeroResult.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(overResult.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(zeroResult.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-ARMY-COUNT-INVALID"));
            Assert.That(overResult.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-ARMY-COUNT-LIMIT"));
        }

        [Test]
        public void OversizedCollectionsRejectAtTheTechnicalBoundary()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleTroopStack template = valid.AttackerArmy.Stacks[0];
            BattleTroopStack[] oversized = Enumerable
                .Repeat(template, BattleTechnicalLimits.MaximumCollectionCount + 1)
                .ToArray();
            var army = new BattleArmySnapshot(valid.AttackerArmy.Identity, oversized);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, attacker: army));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-ARMY-STACKS-INVALID"));
        }

        [Test]
        public void UnknownTroopAndCatalogVersionOrHashMismatchReject()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleTroopStack template = valid.AttackerArmy.Stacks[0];
            var unknown = new BattleTroopStack(
                "troop.unknown",
                template.TroopContentVersion,
                template.TroopDefinitionSha256,
                1);
            var mismatch = new BattleTroopStack(
                template.TroopDefinitionId,
                "future_version",
                new string('f', 64),
                1);

            BattleComputationResult unknownResult = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(
                    valid,
                    attacker: BattleContractTestData.ArmyWithStacks(valid.AttackerArmy, new[] { unknown })));
            BattleComputationResult mismatchResult = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(
                    valid,
                    attacker: BattleContractTestData.ArmyWithStacks(valid.AttackerArmy, new[] { mismatch })));

            Assert.That(unknownResult.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(mismatchResult.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(unknownResult.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-ARMY-TROOP-UNKNOWN"));
            Assert.That(mismatchResult.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-ARMY-TROOP-CATALOG-MISMATCH"));
        }

        [Test]
        public void ArmyContentTamperingRejectsAgainstItsCanonicalHash()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleArmySnapshot tampered = BattleContractTestData.ArmyWithStacks(
                valid.AttackerArmy,
                valid.AttackerArmy.Stacks,
                new string('a', 64));

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, attacker: tampered));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidArmy));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-ARMY-HASH-MISMATCH"));
        }

        [Test]
        public void RehashedUnapprovedRulesTuningStillRejects()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleRulesProfile source = valid.Rules;
            var identity = source.Identity.WithSha256(new string('0', 64));
            var provisional = new BattleRulesProfile(
                identity,
                source.MaximumRounds,
                source.MinimumDamageRateMicros,
                source.MaximumDamageRateExclusiveMicros,
                source.CounterMultiplierMicros + 1,
                source.BossSiegeMultiplierMicros,
                source.WinnerCasualtyPressureMicros,
                source.LoserCasualtyPressureMicros,
                source.LoserCasualtyFloorMicros,
                source.WinnerKilledShareMicros,
                source.LoserKilledShareMicros,
                source.InfantryVulnerabilityMicros,
                source.CavalryVulnerabilityMicros,
                source.RangedVulnerabilityMicros,
                source.SiegeVulnerabilityMicros);
            var invalid = new BattleRulesProfile(
                identity.WithSha256(BattleCanonicalHash.Rules(provisional)),
                provisional.MaximumRounds,
                provisional.MinimumDamageRateMicros,
                provisional.MaximumDamageRateExclusiveMicros,
                provisional.CounterMultiplierMicros,
                provisional.BossSiegeMultiplierMicros,
                provisional.WinnerCasualtyPressureMicros,
                provisional.LoserCasualtyPressureMicros,
                provisional.LoserCasualtyFloorMicros,
                provisional.WinnerKilledShareMicros,
                provisional.LoserKilledShareMicros,
                provisional.InfantryVulnerabilityMicros,
                provisional.CavalryVulnerabilityMicros,
                provisional.RangedVulnerabilityMicros,
                provisional.SiegeVulnerabilityMicros);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, rules: invalid));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidRulesProfile));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-RULES-MULTIPLIER-INVALID"));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Does.Not.Contain("AL-BATTLE-RULES-HASH-MISMATCH"));
        }

        [TestCase(649999L)]
        [TestCase(1300001L)]
        public void MoraleOutsideClosedTechnicalRangeRejects(long morale)
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleModifierSnapshot invalidModifier = BattleContractTestData.ModifierWith(
                valid.Context.AttackerModifiers,
                morale: morale);
            BattleContextSnapshot context = BattleContractTestData.ContextWith(
                valid.Context,
                attackerModifiers: invalidModifier);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, context: context));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidModifierSnapshot));
            Assert.That(result.Value, Is.Null);
        }

        [Test]
        public void NeutralBossAndRealmContextsCannotFallbackAcrossKinds()
        {
            BattleComputationRequest valid = BattleContractTestData.Request(kind: BattleKind.Pvp);
            BattleContextSnapshot neutral = BattleContractTestData.ContextWith(
                valid.Context,
                kind: BattleRealmContextKind.NeutralEncounter,
                opponentRealm: BattleRealm.Neutral);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, context: neutral));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidRealmContext));
            Assert.That(result.Value, Is.Null);
        }

        [Test]
        public void WarzoneAcceptsAnExplicitNeutralTerritoryContext()
        {
            BattleComputationRequest valid = BattleContractTestData.Request(kind: BattleKind.Warzone);
            BattleContextSnapshot neutral = BattleContractTestData.ContextWith(
                valid.Context,
                kind: BattleRealmContextKind.NeutralEncounter,
                opponentRealm: BattleRealm.Neutral);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, context: neutral));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.Computed));
            Assert.That(result.Value, Is.Not.Null);
        }

        [Test]
        public void PvpRequiresDistinctParticipantArmyIdentities()
        {
            BattleComputationRequest valid = BattleContractTestData.Request(kind: BattleKind.Pvp);
            var sameParticipant = new BattleOpponentSnapshot(
                BattleOpponentKind.Army,
                valid.AttackerArmy,
                null,
                0L);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, opponent: sameParticipant));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidOpponent));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-PVP-PARTICIPANT-SAME"));
        }

        [Test]
        public void UnknownTerrainProfileRejectsWithoutSubstringFallback()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleContextSnapshot context = BattleContractTestData.ContextWith(
                valid.Context,
                terrain: (BattleTerrainProfile)99);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, context: context));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidTerrainProfile));
            Assert.That(result.Value, Is.Null);
        }

        [Test]
        public void TerrainProfileIdentityAndHashAreValidatedIndependently()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleTerrainSnapshot wrongId = BattleContractTestData.TerrainWith(
                valid.Context.Terrain,
                id: "terrain.wrong");
            var contextIdentity = valid.Context.Identity.WithSha256(new string('0', 64));
            var provisional = new BattleContextSnapshot(
                contextIdentity,
                valid.Context.EncounterId,
                valid.Context.ContextKind,
                valid.Context.AttackerRealm,
                valid.Context.OpponentRealm,
                wrongId,
                valid.Context.AttackerModifiers,
                valid.Context.OpponentModifiers);
            var context = new BattleContextSnapshot(
                contextIdentity.WithSha256(BattleCanonicalHash.Context(provisional)),
                provisional.EncounterId,
                provisional.ContextKind,
                provisional.AttackerRealm,
                provisional.OpponentRealm,
                provisional.Terrain,
                provisional.AttackerModifiers,
                provisional.OpponentModifiers);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, context: context));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidTerrainProfile));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-TERRAIN-ID-MISMATCH"));
        }

        [Test]
        public void BossBattleAcceptsAnArmyBoundToABossDefinition()
        {
            BattleCatalogSnapshot catalog = BattleContractTestData.Catalog();
            BattleOpponentSnapshot armyOpponent = BattleContractTestData.BossArmyOpponent(catalog);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(
                    kind: BattleKind.Boss,
                    catalog: catalog,
                    opponent: armyOpponent));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.Computed));
            Assert.That(result.Value.OpponentLosses, Is.Not.Empty);
            Assert.That(result.Value.OpponentSha256,
                Is.EqualTo(BattleCanonicalHash.BossArmy(
                    armyOpponent.BossIdentity,
                    armyOpponent.Army)));
        }

        [Test]
        public void NonBossBattleRejectsAnyBossDefinitionIdentity()
        {
            BattleComputationRequest valid = BattleContractTestData.Request();
            BattleOpponentSnapshot bossArmy = BattleContractTestData.BossArmyOpponent(valid.Catalog);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, opponent: bossArmy));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidOpponent));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-OPPONENT-BOSS-FORBIDDEN"));
        }

        [Test]
        public void BossPowerTamperingRejectsAgainstBossSnapshotHash()
        {
            BattleComputationRequest valid = BattleContractTestData.Request(kind: BattleKind.Boss);
            var tampered = new BattleOpponentSnapshot(
                BattleOpponentKind.Boss,
                null,
                valid.Opponent.BossIdentity,
                valid.Opponent.BossPower + 1);

            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Copy(valid, opponent: tampered));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.InvalidOpponent));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-BATTLE-OPPONENT-BOSS-HASH-MISMATCH"));
        }
    }
}
