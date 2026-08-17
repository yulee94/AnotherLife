using System;
using System.Collections.Generic;
using AL.Battle.Application;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Battle
{
    public class BattleTransactionRecoveryTests
    {
        [Test]
        public void InterruptionBeforeCommitRecoversAndAppliesOnce()
        {
            BattleComputedResult result = AuthoritativeResult();
            var stateStore = StoreFor(result);
            var persistence = new InMemoryBattleTransactionPersistence();
            var firstProcess = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(stateStore));

            BattleTransactionRecord pending = firstProcess.Begin(result);
            Assert.That(pending.State, Is.EqualTo(BattleTransactionState.Pending));

            var restarted = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(stateStore));
            BattleTransactionRecoveryReport recovered = restarted.RecoverAll();

            Assert.That(recovered.Status, Is.EqualTo(BattleTransactionRecoveryStatus.Recovered));
            Assert.That(restarted.Get(result.BattleResultId).State, Is.EqualTo(BattleTransactionState.Applied));
            Assert.That(stateStore.Snapshot().Credits, Is.EqualTo(result.RewardProposal.Credits));
            Assert.That(stateStore.Snapshot().AppliedResultCount, Is.EqualTo(1));
        }

        [Test]
        public void InterruptionDuringApplicationReplaysSafely()
        {
            BattleComputedResult result = AuthoritativeResult();
            var stateStore = StoreFor(result);
            var persistence = new InMemoryBattleTransactionPersistence();
            var interrupted = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(stateStore),
                point =>
                {
                    if (point == BattleTransactionInterruptionPoint.AfterApplyingPersisted)
                        throw new SimulatedInterruptionException();
                });
            interrupted.Begin(result);

            Assert.Throws<SimulatedInterruptionException>(() => interrupted.Apply(result.BattleResultId));
            Assert.That(interrupted.Get(result.BattleResultId).State,
                Is.EqualTo(BattleTransactionState.Applying));
            Assert.That(stateStore.Snapshot().AppliedResultCount, Is.Zero);

            var restarted = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(stateStore));
            restarted.RecoverAll();

            Assert.That(restarted.Get(result.BattleResultId).State,
                Is.EqualTo(BattleTransactionState.Applied));
            Assert.That(stateStore.Snapshot().AppliedResultCount, Is.EqualTo(1));
        }

        [Test]
        public void CommitBeforeAcknowledgementRecoversWithoutDuplicateReward()
        {
            BattleComputedResult result = AuthoritativeResult();
            var stateStore = StoreFor(result);
            var persistence = new InMemoryBattleTransactionPersistence();
            var interrupted = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(stateStore),
                point =>
                {
                    if (point == BattleTransactionInterruptionPoint.AfterAdapterCommit)
                        throw new SimulatedInterruptionException();
                });
            interrupted.Begin(result);

            Assert.Throws<SimulatedInterruptionException>(() => interrupted.Apply(result.BattleResultId));
            Assert.That(stateStore.Snapshot().AppliedResultCount, Is.EqualTo(1));

            // Reconstruct both services from durable snapshots. The adapter's committed receipt,
            // not the coordinator's stale Applying marker, proves the award already happened.
            var restartedStateStore = new InMemoryBattleResultStore(stateStore.Snapshot());
            var restarted = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(restartedStateStore));
            restarted.RecoverAll();
            restarted.RecoverAll();
            BattleTransactionRecord applied = restarted.Get(result.BattleResultId);

            Assert.That(applied.State, Is.EqualTo(BattleTransactionState.Applied));
            Assert.That(restartedStateStore.Snapshot().Credits,
                Is.EqualTo(result.RewardProposal.Credits));
            Assert.That(restartedStateStore.Snapshot().AppliedResultCount, Is.EqualTo(1));

            BattleTransactionRecord acknowledged = restarted.Acknowledge(result.BattleResultId);
            Assert.That(acknowledged.State, Is.EqualTo(BattleTransactionState.Acknowledged));
        }

        [Test]
        public void InvalidResultIsDurablyRejectedAndNeverRecovered()
        {
            BattleComputedResult valid = AuthoritativeResult();
            BattleComputedResult corrupted = CopyResult(valid, new string('f', 64));
            var stateStore = StoreFor(valid);
            var persistence = new InMemoryBattleTransactionPersistence();
            var coordinator = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(stateStore));
            coordinator.Begin(corrupted);

            BattleTransactionRecord rejected = coordinator.Apply(corrupted.BattleResultId);
            Assert.That(rejected.State, Is.EqualTo(BattleTransactionState.Rejected));

            var restarted = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(stateStore));
            BattleTransactionRecoveryReport report = restarted.RecoverAll();

            Assert.That(report.AttemptedCount, Is.Zero);
            Assert.That(restarted.Get(corrupted.BattleResultId).State,
                Is.EqualTo(BattleTransactionState.Rejected));
            Assert.That(stateStore.Snapshot().AppliedResultCount, Is.Zero);
        }

        [Test]
        public void PersistenceFailureRequiresReconciliationAndIsNotBlindlyRetried()
        {
            BattleComputedResult result = AuthoritativeResult();
            var persistence = new InMemoryBattleTransactionPersistence();
            var coordinator = new BattleTransactionCoordinator(
                persistence,
                new AtomicBattleResultAdapter(new ThrowingBattleResultStore()));
            coordinator.Begin(result);

            BattleTransactionRecord uncertain = coordinator.Apply(result.BattleResultId);
            BattleTransactionRecoveryReport recovery = coordinator.RecoverAll();

            Assert.That(uncertain.State, Is.EqualTo(BattleTransactionState.RecoveryRequired));
            Assert.That(recovery.Status, Is.EqualTo(BattleTransactionRecoveryStatus.NothingToRecover));
            Assert.That(recovery.AttemptedCount, Is.Zero);
        }

        [Test]
        public void AtomicFilePersistenceSurvivesANewPersistenceInstance()
        {
            BattleComputedResult result = AuthoritativeResult();
            string directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "al-battle-transaction-" + Guid.NewGuid().ToString("N"));
            string path = System.IO.Path.Combine(directory, "transactions.bin");
            try
            {
                var stateStore = StoreFor(result);
                var firstProcess = new BattleTransactionCoordinator(
                    new AtomicFileBattleTransactionPersistence(path),
                    new AtomicBattleResultAdapter(stateStore));
                firstProcess.Begin(result);

                var restarted = new BattleTransactionCoordinator(
                    new AtomicFileBattleTransactionPersistence(path),
                    new AtomicBattleResultAdapter(stateStore));
                BattleTransactionRecoveryReport report = restarted.RecoverAll();

                Assert.That(report.Status, Is.EqualTo(BattleTransactionRecoveryStatus.Recovered));
                Assert.That(restarted.Get(result.BattleResultId).State,
                    Is.EqualTo(BattleTransactionState.Applied));
            }
            finally
            {
                if (System.IO.Directory.Exists(directory))
                    System.IO.Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CorruptedOrIncompatiblePersistenceFailsClosed()
        {
            BattleComputedResult result = AuthoritativeResult();
            var corrupted = new InMemoryBattleTransactionPersistence(new byte[] { 1, 2, 3, 4 });
            var incompatible = new InMemoryBattleTransactionPersistence(
                BattleTransactionSnapshotCodec.CreateIncompatibleSnapshotForTest());

            var corruptedCoordinator = new BattleTransactionCoordinator(
                corrupted,
                new AtomicBattleResultAdapter(StoreFor(result)));
            var incompatibleCoordinator = new BattleTransactionCoordinator(
                incompatible,
                new AtomicBattleResultAdapter(StoreFor(result)));

            Assert.That(corruptedCoordinator.RecoverAll().Status,
                Is.EqualTo(BattleTransactionRecoveryStatus.CorruptedPersistence));
            Assert.That(incompatibleCoordinator.RecoverAll().Status,
                Is.EqualTo(BattleTransactionRecoveryStatus.UnsupportedVersion));
            Assert.Throws<InvalidOperationException>(() => corruptedCoordinator.Begin(result));
            Assert.Throws<InvalidOperationException>(() => incompatibleCoordinator.Begin(result));
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

        private static BattleComputedResult CopyResult(BattleComputedResult source, string hash)
        {
            return new BattleComputedResult(
                source.GameId, source.CatalogSetId, source.ProfileId, source.BattleRequestId,
                source.BattleId, source.BattleResultId, source.ExpectedResultConsumerId,
                source.ExecutionMode, source.BattleKind, source.BattleTypeId, source.Outcome,
                source.OutcomeTechnicalId, source.AttackerPower, source.OpponentPower,
                source.Rounds, source.AttackerLosses, source.OpponentLosses,
                source.RewardProposal, source.Contributions, source.CatalogSha256,
                source.AttackerArmySha256, source.OpponentSha256, source.ContextSha256,
                source.RulesSha256, source.RewardProfileSha256, source.DeterminismVersion,
                source.SeedHex, hash);
        }

        private sealed class ThrowingBattleResultStore : IBattleResultStore
        {
            public BattleResultApplicationResult ExecuteAtomic(
                Func<BattleApplicationState, BattleResultApplicationResult> operation)
            {
                throw new InvalidOperationException("simulated uncertain persistence");
            }
        }

        private sealed class SimulatedInterruptionException : Exception { }
    }
}
