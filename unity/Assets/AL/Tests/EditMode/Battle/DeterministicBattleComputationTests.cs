using System;
using System.Collections.Generic;
using System.Linq;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Battle
{
    public class DeterministicBattleComputationTests
    {
        [Test]
        public void FixedPointUsesExactIntermediateAndNearestTiesToEven()
        {
            Assert.That(BattleFixedPoint.MultiplyAndRound(1, 500_000), Is.EqualTo(0));
            Assert.That(BattleFixedPoint.MultiplyAndRound(3, 500_000), Is.EqualTo(2));
            Assert.That(BattleFixedPoint.MultiplyAndRound(5, 500_000), Is.EqualTo(2));
            Assert.That(BattleFixedPoint.MultiplyAndRound(7, 500_000), Is.EqualTo(4));
            Assert.That(
                BattleFixedPoint.MultiplyAndRoundOnce(
                    10,
                    new[] { 1_100_000L, 1_050_000L }),
                Is.EqualTo(12));
            Assert.That(
                BattleFixedPoint.MultiplyAndRoundOnce(
                    long.MaxValue / 4,
                    new[] { 1_000_000L, 1_000_000L }),
                Is.EqualTo(long.MaxValue / 4));
        }

        [Test]
        public void UInt32RangeMappingUsesFullUnsignedDomain()
        {
            Assert.That(BattleFixedPoint.MapUInt32(0, 80_000, 160_000), Is.EqualTo(80_000));
            Assert.That(BattleFixedPoint.MapUInt32(uint.MaxValue, 80_000, 160_000), Is.EqualTo(159_999));
            Assert.That(BattleFixedPoint.MapUInt32(0x80000000U, 0, 100), Is.EqualTo(50));
        }

        [Test]
        public void SameRequestProducesByteStableResult()
        {
            BattleComputationRequest request = BattleContractTestData.Request();

            BattleComputationResult first = DeterministicBattleComputation.Compute(request);
            BattleComputationResult second = DeterministicBattleComputation.Compute(request);

            Assert.That(first.Status, Is.EqualTo(BattleComputationStatus.Computed));
            Assert.That(second.Status, Is.EqualTo(first.Status));
            Assert.That(second.Value.ComputationSha256, Is.EqualTo(first.Value.ComputationSha256));
            Assert.That(second.Value.AttackerPower, Is.EqualTo(first.Value.AttackerPower));
            Assert.That(second.Value.OpponentPower, Is.EqualTo(first.Value.OpponentPower));
            Assert.That(
                second.Value.Rounds.Select(RoundSignature),
                Is.EqualTo(first.Value.Rounds.Select(RoundSignature)));
            Assert.That(BattleCanonicalHash.Result(first.Value), Is.EqualTo(first.Value.ComputationSha256));
        }

        [Test]
        public void DifferentSeedChangesEntropyButNotDeterministicPower()
        {
            BattleComputationResult first = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request());
            BattleComputationResult second = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(
                    seedHex: "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

            Assert.That(second.Value.AttackerPower, Is.EqualTo(first.Value.AttackerPower));
            Assert.That(second.Value.OpponentPower, Is.EqualTo(first.Value.OpponentPower));
            Assert.That(second.Value.Rounds[0].AttackerRateMicros,
                Is.Not.EqualTo(first.Value.Rounds[0].AttackerRateMicros));
            Assert.That(second.Value.ComputationSha256, Is.Not.EqualTo(first.Value.ComputationSha256));
        }

        [TestCase(BattleKind.Pve)]
        [TestCase(BattleKind.Pvp)]
        [TestCase(BattleKind.Boss)]
        [TestCase(BattleKind.Warzone)]
        public void EverySupportedBattleKindComputes(BattleKind kind)
        {
            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(kind: kind));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.Computed));
            Assert.That(result.Value.BattleKind, Is.EqualTo(kind));
            Assert.That(result.Value.BattleTypeId, Is.EqualTo(BattleContractTestData.TypeId(kind)));
            Assert.That(result.Value.AttackerPower,
                Is.InRange(1L, BattleTechnicalLimits.MaximumFinalPower));
            Assert.That(result.Value.OpponentPower,
                Is.InRange(1L, BattleTechnicalLimits.MaximumFinalPower));
        }

        [Test]
        public void PreviewIsPermanentlyApplicationIneligible()
        {
            BattleComputationResult preview = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(mode: BattleExecutionMode.Preview));
            BattleComputationResult authoritative = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request());

            Assert.That(preview.Status, Is.EqualTo(BattleComputationStatus.ComputedPreview));
            Assert.That(preview.Value.IsPreviewApplicationProhibited, Is.True);
            Assert.That(preview.Value.IsAuthoritativeProposal, Is.False);
            Assert.That(authoritative.Value.IsPreviewApplicationProhibited, Is.False);
            Assert.That(authoritative.Value.IsAuthoritativeProposal, Is.True);
            Assert.That(preview.Value.ComputationSha256,
                Is.Not.EqualTo(authoritative.Value.ComputationSha256));
        }

        [Test]
        public void RoundsAreSimultaneousBoundedAndUseNamespacedRates()
        {
            BattleComputationRequest request = BattleContractTestData.Request();
            BattleComputedResult result = DeterministicBattleComputation.Compute(request).Value;
            long priorAttacker = result.AttackerPower * BattleTechnicalLimits.MicrosPerUnit;
            long priorOpponent = result.OpponentPower * BattleTechnicalLimits.MicrosPerUnit;

            Assert.That(result.Rounds.Count, Is.InRange(1, request.Rules.MaximumRounds));
            Assert.That(
                result.Rounds[0].AttackerRateMicros,
                Is.EqualTo(BattleFixedPoint.MapUInt32(
                    BattleDeterminism.DrawUInt32(
                        request,
                        "round.1.attacker_damage_rate",
                        1),
                    request.Rules.MinimumDamageRateMicros,
                    request.Rules.MaximumDamageRateExclusiveMicros)));
            Assert.That(
                result.Rounds[0].OpponentRateMicros,
                Is.EqualTo(BattleFixedPoint.MapUInt32(
                    BattleDeterminism.DrawUInt32(
                        request,
                        "round.1.defender_damage_rate",
                        1),
                    request.Rules.MinimumDamageRateMicros,
                    request.Rules.MaximumDamageRateExclusiveMicros)));
            for (int index = 0; index < result.Rounds.Count; index++)
            {
                BattleRoundResult round = result.Rounds[index];
                Assert.That(round.RoundIndex, Is.EqualTo(index + 1));
                Assert.That(round.AttackerRateMicros,
                    Is.InRange(request.Rules.MinimumDamageRateMicros,
                        request.Rules.MaximumDamageRateExclusiveMicros - 1));
                Assert.That(round.OpponentRateMicros,
                    Is.InRange(request.Rules.MinimumDamageRateMicros,
                        request.Rules.MaximumDamageRateExclusiveMicros - 1));
                Assert.That(round.DamageToAttackerMicros,
                    Is.InRange(0L, priorAttacker));
                Assert.That(round.DamageToOpponentMicros,
                    Is.InRange(0L, priorOpponent));
                Assert.That(round.AttackerRemainingPowerMicros,
                    Is.EqualTo(priorAttacker - round.DamageToAttackerMicros));
                Assert.That(round.OpponentRemainingPowerMicros,
                    Is.EqualTo(priorOpponent - round.DamageToOpponentMicros));
                priorAttacker = round.AttackerRemainingPowerMicros;
                priorOpponent = round.OpponentRemainingPowerMicros;
            }

            Assert.That(result.Rounds as BattleRoundResult[], Is.Null);
        }

        [Test]
        public void ExactNormalizedTieBelongsToAttacker()
        {
            Assert.That(BattleFixedPoint.RatioAtLeast(1, 3, 2, 6), Is.True);
            Assert.That(BattleFixedPoint.RatioAtLeast(2, 6, 1, 3), Is.True);
            Assert.That(BattleFixedPoint.RatioAtLeast(1, 4, 1, 3), Is.False);
        }

        [Test]
        public void CasualtyPartitionsExactlyReconcileEveryInputStack()
        {
            BattleComputationRequest request = BattleContractTestData.Request();
            BattleComputedResult result = DeterministicBattleComputation.Compute(request).Value;

            AssertLossesReconcile(request.AttackerArmy, result.AttackerLosses);
            AssertLossesReconcile(request.Opponent.Army, result.OpponentLosses);
        }

        [Test]
        public void BossOpponentNeverFabricatesTroopLosses()
        {
            BattleComputationResult result = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(kind: BattleKind.Boss));

            Assert.That(result.Status, Is.EqualTo(BattleComputationStatus.Computed));
            Assert.That(result.Value.OpponentLosses, Is.Empty);
        }

        [Test]
        public void VulnerabilityOrderingIsAppliedWithExactIntegerMath()
        {
            BattleCatalogSnapshot catalog = BattleContractTestData.Catalog();
            BattleArmySnapshot attacker = BattleContractTestData.Army(
                catalog,
                "army.equal_stacks",
                100,
                100,
                100,
                100);
            BattleComputationResult computation = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(catalog: catalog, attacker: attacker));
            Dictionary<string, long> affected = computation.Value.AttackerLosses.ToDictionary(
                item => item.TroopDefinitionId,
                item => item.Killed + item.Wounded,
                StringComparer.Ordinal);

            Assert.That(affected["troop.cavalry"], Is.LessThanOrEqualTo(affected["troop.infantry"]));
            Assert.That(affected["troop.infantry"], Is.LessThanOrEqualTo(affected["troop.ranged"]));
            Assert.That(affected["troop.ranged"], Is.LessThanOrEqualTo(affected["troop.siege"]));
        }

        [Test]
        public void ProposedRewardsRespectBattleTypeOutcomeAndMigrationBounds()
        {
            BattleComputedResult pve = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(kind: BattleKind.Pve)).Value;
            BattleComputedResult pvp = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(kind: BattleKind.Pvp)).Value;

            Assert.That(pve.RewardProposal.IsProposed, Is.True);
            Assert.That(pve.RewardProposal.Credits, Is.Zero);
            Assert.That(pvp.RewardProposal.Credits, Is.InRange(4, 62));
            Assert.That(pvp.RewardProposal.Experience, Is.GreaterThanOrEqualTo(3));
            if (pvp.Outcome == BattleOutcome.AttackerVictory)
            {
                BattleComputationRequest request = BattleContractTestData.Request(kind: BattleKind.Pvp);
                Assert.That(
                    pvp.RewardProposal.Food,
                    Is.EqualTo(request.Rewards.WinFoodBase + BattleFixedPoint.MapUInt32(
                        BattleDeterminism.DrawUInt32(request, "reward.food_amount", 0),
                        0,
                        request.Rewards.WinFoodRandomExclusive)));
                Assert.That(
                    pvp.RewardProposal.Gold,
                    Is.EqualTo(request.Rewards.WinGoldBase + BattleFixedPoint.MapUInt32(
                        BattleDeterminism.DrawUInt32(request, "reward.gold_amount", 0),
                        0,
                        request.Rewards.WinGoldRandomExclusive)));
            }
            else
            {
                Assert.That(pvp.RewardProposal.Food, Is.Zero);
                Assert.That(pvp.RewardProposal.Gold, Is.Zero);
            }
        }

        [Test]
        public void ResultCarriesEveryAuthorityBindingAndOnlyTechnicalContributions()
        {
            BattleComputationRequest request = BattleContractTestData.Request();
            BattleComputedResult result = DeterministicBattleComputation.Compute(request).Value;

            Assert.That(result.GameId, Is.EqualTo(request.GameId));
            Assert.That(result.CatalogSetId, Is.EqualTo(request.CatalogSetId));
            Assert.That(result.BattleRequestId, Is.EqualTo(request.BattleRequestId));
            Assert.That(result.BattleId, Is.EqualTo(request.BattleId));
            Assert.That(result.BattleResultId, Is.EqualTo(request.BattleResultId));
            Assert.That(result.ExpectedResultConsumerId, Is.EqualTo(request.ExpectedResultConsumerId));
            Assert.That(result.CatalogSha256, Is.EqualTo(request.Catalog.Identity.Sha256));
            Assert.That(result.AttackerArmySha256, Is.EqualTo(request.AttackerArmy.Identity.Sha256));
            Assert.That(result.ContextSha256, Is.EqualTo(request.Context.Identity.Sha256));
            Assert.That(result.RulesSha256, Is.EqualTo(request.Rules.Identity.Sha256));
            Assert.That(result.RewardProfileSha256, Is.EqualTo(request.Rewards.Identity.Sha256));
            Assert.That(result.SeedHex, Is.EqualTo(request.SeedHex));
            Assert.That(new[] { "attacker_pressure", "defender_pressure", "even_trade" },
                Contains.Item(result.OutcomeTechnicalId));
            Assert.That(result.Contributions.All(item =>
                item.TechnicalId.All(character =>
                    char.IsLower(character) || character == '.' || character == '_')), Is.True);
        }

        [Test]
        public void BattleIdentityChangesResultBindingWithoutChangingPowerOrDraws()
        {
            BattleComputationRequest firstRequest = BattleContractTestData.Request();
            BattleComputationRequest secondRequest = BattleContractTestData.Copy(
                firstRequest,
                battleId: "battle.other");

            BattleComputedResult first = DeterministicBattleComputation.Compute(firstRequest).Value;
            BattleComputedResult second = DeterministicBattleComputation.Compute(secondRequest).Value;

            Assert.That(second.BattleId, Is.EqualTo("battle.other"));
            Assert.That(second.AttackerPower, Is.EqualTo(first.AttackerPower));
            Assert.That(second.OpponentPower, Is.EqualTo(first.OpponentPower));
            Assert.That(
                second.Rounds.Select(RoundSignature),
                Is.EqualTo(first.Rounds.Select(RoundSignature)));
            Assert.That(second.ComputationSha256, Is.Not.EqualTo(first.ComputationSha256));
        }

        [Test]
        public void ComputationDoesNotMutateCallerOwnedRequestGraph()
        {
            BattleComputationRequest request = BattleContractTestData.Request();
            string attackerHash = request.AttackerArmy.Identity.Sha256;
            string opponentHash = request.Opponent.Army.Identity.Sha256;
            long[] attackerCounts = request.AttackerArmy.Stacks.Select(item => item.Count).ToArray();
            long[] opponentCounts = request.Opponent.Army.Stacks.Select(item => item.Count).ToArray();

            DeterministicBattleComputation.Compute(request);

            Assert.That(request.AttackerArmy.Identity.Sha256, Is.EqualTo(attackerHash));
            Assert.That(request.Opponent.Army.Identity.Sha256, Is.EqualTo(opponentHash));
            Assert.That(request.AttackerArmy.Stacks.Select(item => item.Count), Is.EqualTo(attackerCounts));
            Assert.That(request.Opponent.Army.Stacks.Select(item => item.Count), Is.EqualTo(opponentCounts));
        }

        private static string RoundSignature(BattleRoundResult round)
        {
            return string.Join("|", new[]
            {
                round.RoundIndex.ToString(),
                round.AttackerRateMicros.ToString(),
                round.OpponentRateMicros.ToString(),
                round.DamageToOpponentMicros.ToString(),
                round.DamageToAttackerMicros.ToString(),
                round.AttackerRemainingPowerMicros.ToString(),
                round.OpponentRemainingPowerMicros.ToString()
            });
        }

        private static void AssertLossesReconcile(
            BattleArmySnapshot army,
            IReadOnlyList<BattleTroopLoss> losses)
        {
            Assert.That(losses.Count, Is.EqualTo(army.Stacks.Count));
            for (int index = 0; index < army.Stacks.Count; index++)
            {
                Assert.That(losses[index].TroopDefinitionId,
                    Is.EqualTo(army.Stacks[index].TroopDefinitionId));
                Assert.That(losses[index].Killed, Is.GreaterThanOrEqualTo(0));
                Assert.That(losses[index].Wounded, Is.GreaterThanOrEqualTo(0));
                Assert.That(losses[index].Survived, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    losses[index].Killed + losses[index].Wounded + losses[index].Survived,
                    Is.EqualTo(army.Stacks[index].Count));
            }
        }
    }
}
