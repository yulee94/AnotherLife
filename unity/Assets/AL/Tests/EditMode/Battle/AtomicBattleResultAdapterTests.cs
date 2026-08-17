using System;
using System.Collections.Generic;
using AL.Battle.Application;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Battle
{
    public class AtomicBattleResultAdapterTests
    {
        [Test]
        public void SuccessfulCommitAppliesLossesEconomyLootAndProgressionTogether()
        {
            BattleComputedResult result = AuthoritativeResult();
            var store = StoreFor(result);
            var adapter = new AtomicBattleResultAdapter(store);

            BattleResultApplicationResult applied = adapter.Apply(result);
            BattleApplicationState state = store.Snapshot();

            Assert.That(applied.Status, Is.EqualTo(BattleResultApplicationStatus.Committed));
            Assert.That(applied.Receipt.BattleResultId, Is.EqualTo(result.BattleResultId));
            Assert.That(state.Credits, Is.EqualTo(result.RewardProposal.Credits));
            Assert.That(state.Food, Is.EqualTo(result.RewardProposal.Food));
            Assert.That(state.Gold, Is.EqualTo(result.RewardProposal.Gold));
            Assert.That(state.Experience, Is.EqualTo(result.RewardProposal.Experience));
            Assert.That(state.WinBattleProgress,
                Is.EqualTo(result.Outcome == BattleOutcome.AttackerVictory ? 1 : 0));
            foreach (BattleTroopLoss loss in result.AttackerLosses)
            {
                Assert.That(state.GetActiveTroops(loss.TroopDefinitionId), Is.EqualTo(loss.Survived));
                Assert.That(state.GetWoundedTroops(loss.TroopDefinitionId), Is.EqualTo(loss.Wounded));
            }
            Assert.That(state.AppliedResultCount, Is.EqualTo(1));
        }

        [Test]
        public void ExactDuplicateReturnsStoredReceiptWithoutApplyingAgain()
        {
            BattleComputedResult result = AuthoritativeResult();
            var store = StoreFor(result);
            var adapter = new AtomicBattleResultAdapter(store);

            BattleResultApplicationResult first = adapter.Apply(result);
            BattleResultApplicationResult duplicate = adapter.Apply(result);
            BattleApplicationState state = store.Snapshot();

            Assert.That(duplicate.Status, Is.EqualTo(BattleResultApplicationStatus.AlreadyCommitted));
            Assert.That(duplicate.Receipt.CommitSequence, Is.EqualTo(first.Receipt.CommitSequence));
            Assert.That(state.Credits, Is.EqualTo(result.RewardProposal.Credits));
            Assert.That(state.Experience, Is.EqualTo(result.RewardProposal.Experience));
            Assert.That(state.AppliedResultCount, Is.EqualTo(1));
        }

        [Test]
        public void SameResultIdentityWithDifferentComputationIsRejectedAsConflict()
        {
            BattleComputedResult first = AuthoritativeResult();
            BattleComputedResult conflicting = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(
                    resultId: first.BattleResultId,
                    seedHex: "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")).Value;
            var store = StoreFor(first);
            var adapter = new AtomicBattleResultAdapter(store);

            adapter.Apply(first);
            BattleResultApplicationResult conflict = adapter.Apply(conflicting);

            Assert.That(conflict.Status, Is.EqualTo(BattleResultApplicationStatus.CorrelationConflict));
            Assert.That(store.Snapshot().AppliedResultCount, Is.EqualTo(1));
        }

        [Test]
        public void PreviewAndCorruptedResultAreRejectedBeforeAnyMutation()
        {
            BattleComputedResult authoritative = AuthoritativeResult();
            BattleComputedResult preview = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(mode: BattleExecutionMode.Preview)).Value;
            BattleComputedResult corrupted = CopyResult(
                authoritative,
                computationSha256: new string('f', 64));
            var store = StoreFor(authoritative);
            var adapter = new AtomicBattleResultAdapter(store);

            BattleResultApplicationResult previewResult = adapter.Apply(preview);
            BattleResultApplicationResult corruptedResult = adapter.Apply(corrupted);
            BattleResultApplicationResult nullResult = adapter.Apply(null);

            Assert.That(previewResult.Status, Is.EqualTo(BattleResultApplicationStatus.PreviewRejected));
            Assert.That(corruptedResult.Status, Is.EqualTo(BattleResultApplicationStatus.InvalidComputedResult));
            Assert.That(nullResult.Status, Is.EqualTo(BattleResultApplicationStatus.InvalidComputedResult));
            Assert.That(store.Snapshot().AppliedResultCount, Is.Zero);
            Assert.That(store.Snapshot().Credits, Is.Zero);
        }

        [Test]
        public void UnsupportedDeterminismVersionIsRejectedEvenWhenHashIsSelfConsistent()
        {
            BattleComputedResult authoritative = AuthoritativeResult();
            BattleComputedResult unsupported = CopyResult(
                authoritative,
                determinismVersion: "battle_sha256_future");
            unsupported = CopyResult(
                unsupported,
                computationSha256: BattleCanonicalHash.Result(unsupported));
            var store = StoreFor(authoritative);

            BattleResultApplicationResult result =
                new AtomicBattleResultAdapter(store).Apply(unsupported);

            Assert.That(result.Status, Is.EqualTo(BattleResultApplicationStatus.InvalidComputedResult));
            Assert.That(store.Snapshot().AppliedResultCount, Is.Zero);
        }

        [Test]
        public void StoreFailureReturnsPersistenceFailure()
        {
            BattleComputedResult result = AuthoritativeResult();

            BattleResultApplicationResult applied =
                new AtomicBattleResultAdapter(new ThrowingBattleResultStore()).Apply(result);

            Assert.That(applied.Status, Is.EqualTo(BattleResultApplicationStatus.PersistenceFailed));
        }

        [Test]
        public void ConcurrentDuplicatesConvergeToOneCommit()
        {
            BattleComputedResult result = AuthoritativeResult();
            var store = StoreFor(result);
            var adapter = new AtomicBattleResultAdapter(store);
            BattleResultApplicationResult first = null;
            BattleResultApplicationResult second = null;

            System.Threading.Tasks.Parallel.Invoke(
                () => first = adapter.Apply(result),
                () => second = adapter.Apply(result));

            Assert.That(
                new[] { first.Status, second.Status },
                Is.EquivalentTo(new[]
                {
                    BattleResultApplicationStatus.Committed,
                    BattleResultApplicationStatus.AlreadyCommitted
                }));
            Assert.That(store.Snapshot().AppliedResultCount, Is.EqualTo(1));
            Assert.That(store.Snapshot().Credits, Is.EqualTo(result.RewardProposal.Credits));
        }

        [Test]
        public void ConsumerFailureRollsBackEveryStagedMutationAndAllowsSafeRetry()
        {
            BattleComputedResult result = AuthoritativeResult();
            var store = StoreFor(result);
            var adapter = new AtomicBattleResultAdapter(
                store,
                new IBattleResultConsumer[] { new FailAfterMutationConsumer() });

            BattleResultApplicationResult failed = adapter.Apply(result);
            BattleApplicationState afterFailure = store.Snapshot();

            Assert.That(failed.Status, Is.EqualTo(BattleResultApplicationStatus.ConsumerFailed));
            Assert.That(afterFailure.Credits, Is.Zero);
            Assert.That(afterFailure.Experience, Is.Zero);
            Assert.That(afterFailure.AppliedResultCount, Is.Zero);
            foreach (BattleTroopLoss loss in result.AttackerLosses)
                Assert.That(afterFailure.GetActiveTroops(loss.TroopDefinitionId),
                    Is.EqualTo(loss.Killed + loss.Wounded + loss.Survived));

            BattleResultApplicationResult retried = new AtomicBattleResultAdapter(store).Apply(result);
            Assert.That(retried.Status, Is.EqualTo(BattleResultApplicationStatus.Committed));
            Assert.That(store.Snapshot().AppliedResultCount, Is.EqualTo(1));
        }

        private static BattleComputedResult AuthoritativeResult()
        {
            BattleComputationResult computation = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request());
            Assert.That(computation.Status, Is.EqualTo(BattleComputationStatus.Computed));
            return computation.Value;
        }

        private static InMemoryBattleResultStore StoreFor(BattleComputedResult result)
        {
            var active = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (BattleTroopLoss loss in result.AttackerLosses)
                active.Add(loss.TroopDefinitionId, loss.Killed + loss.Wounded + loss.Survived);
            return new InMemoryBattleResultStore(
                BattleApplicationState.CreateForActiveBattle(
                    result.ProfileId,
                    result.BattleRequestId,
                    result.BattleId,
                    result.BattleResultId,
                    result.CatalogSetId,
                    active));
        }

        private static BattleComputedResult CopyResult(
            BattleComputedResult source,
            string determinismVersion = null,
            string computationSha256 = null)
        {
            return new BattleComputedResult(
                source.GameId,
                source.CatalogSetId,
                source.ProfileId,
                source.BattleRequestId,
                source.BattleId,
                source.BattleResultId,
                source.ExpectedResultConsumerId,
                source.ExecutionMode,
                source.BattleKind,
                source.BattleTypeId,
                source.Outcome,
                source.OutcomeTechnicalId,
                source.AttackerPower,
                source.OpponentPower,
                source.Rounds,
                source.AttackerLosses,
                source.OpponentLosses,
                source.RewardProposal,
                source.Contributions,
                source.CatalogSha256,
                source.AttackerArmySha256,
                source.OpponentSha256,
                source.ContextSha256,
                source.RulesSha256,
                source.RewardProfileSha256,
                determinismVersion ?? source.DeterminismVersion,
                source.SeedHex,
                computationSha256 ?? source.ComputationSha256);
        }

        private sealed class ThrowingBattleResultStore : IBattleResultStore
        {
            public BattleResultApplicationResult ExecuteAtomic(
                Func<BattleApplicationState, BattleResultApplicationResult> operation)
            {
                throw new InvalidOperationException("simulated persistence failure");
            }
        }

        private sealed class FailAfterMutationConsumer : IBattleResultConsumer
        {
            public string ConsumerId => "consumer.test.failure";

            public void Validate(BattleComputedResult result, BattleApplicationState candidate)
            {
            }

            public void Apply(BattleComputedResult result, BattleApplicationState candidate)
            {
                candidate.AddCredits(999);
                throw new InvalidOperationException("simulated consumer failure");
            }
        }
    }
}
