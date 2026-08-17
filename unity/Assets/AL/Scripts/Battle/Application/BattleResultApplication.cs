using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AL.Battle.Contracts;

namespace AL.Battle.Application
{
    public enum BattleResultApplicationStatus
    {
        Committed = 0,
        AlreadyCommitted = 1,
        PreviewRejected = 2,
        InvalidComputedResult = 3,
        InactiveOrMismatchedBattle = 4,
        CorrelationConflict = 5,
        ConsumerFailed = 6,
        PersistenceFailed = 7
    }

    public sealed class CommittedBattleResultReceipt
    {
        internal CommittedBattleResultReceipt(
            string battleResultId,
            string battleId,
            string battleRequestId,
            string profileId,
            string computationSha256,
            long commitSequence)
        {
            BattleResultId = battleResultId;
            BattleId = battleId;
            BattleRequestId = battleRequestId;
            ProfileId = profileId;
            ComputationSha256 = computationSha256;
            CommitSequence = commitSequence;
        }

        public string BattleResultId { get; }
        public string BattleId { get; }
        public string BattleRequestId { get; }
        public string ProfileId { get; }
        public string ComputationSha256 { get; }
        public long CommitSequence { get; }
    }

    public sealed class BattleResultApplicationResult
    {
        internal BattleResultApplicationResult(
            BattleResultApplicationStatus status,
            CommittedBattleResultReceipt receipt,
            string diagnosticCode)
        {
            Status = status;
            Receipt = receipt;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public BattleResultApplicationStatus Status { get; }
        public CommittedBattleResultReceipt Receipt { get; }
        public string DiagnosticCode { get; }
        public bool IsCommitted =>
            Status == BattleResultApplicationStatus.Committed ||
            Status == BattleResultApplicationStatus.AlreadyCommitted;
    }

    /// <summary>
    /// Applies one domain contribution to an isolated transaction candidate.
    /// Implementations must not mutate live state or perform external side effects.
    /// </summary>
    public interface IBattleResultConsumer
    {
        string ConsumerId { get; }
        void Validate(BattleComputedResult result, BattleApplicationState candidate);
        void Apply(BattleComputedResult result, BattleApplicationState candidate);
    }

    /// <summary>
    /// Serializes result application and supplies an isolated candidate. The candidate must be
    /// discarded for every status except Committed and published atomically only for Committed.
    /// </summary>
    public interface IBattleResultStore
    {
        BattleResultApplicationResult ExecuteAtomic(
            Func<BattleApplicationState, BattleResultApplicationResult> operation);
    }

    public sealed class BattleApplicationState
    {
        private readonly Dictionary<string, long> _activeTroops;
        private readonly Dictionary<string, long> _woundedTroops;
        private readonly Dictionary<string, CommittedBattleResultReceipt> _appliedResults;

        private BattleApplicationState(
            string profileId,
            string activeBattleRequestId,
            string activeBattleId,
            string expectedBattleResultId,
            string catalogSetId,
            Dictionary<string, long> activeTroops,
            Dictionary<string, long> woundedTroops,
            Dictionary<string, CommittedBattleResultReceipt> appliedResults,
            int credits,
            int food,
            int gold,
            int experience,
            int winBattleProgress,
            long commitSequence)
        {
            ProfileId = profileId;
            ActiveBattleRequestId = activeBattleRequestId;
            ActiveBattleId = activeBattleId;
            ExpectedBattleResultId = expectedBattleResultId;
            CatalogSetId = catalogSetId;
            _activeTroops = activeTroops;
            _woundedTroops = woundedTroops;
            _appliedResults = appliedResults;
            Credits = credits;
            Food = food;
            Gold = gold;
            Experience = experience;
            WinBattleProgress = winBattleProgress;
            CommitSequence = commitSequence;
        }

        public string ProfileId { get; }
        public string ActiveBattleRequestId { get; }
        public string ActiveBattleId { get; }
        public string ExpectedBattleResultId { get; }
        public string CatalogSetId { get; }
        public int Credits { get; private set; }
        public int Food { get; private set; }
        public int Gold { get; private set; }
        public int Experience { get; private set; }
        public int WinBattleProgress { get; private set; }
        public long CommitSequence { get; private set; }
        public int AppliedResultCount => _appliedResults.Count;

        public static BattleApplicationState CreateForActiveBattle(
            string profileId,
            string battleRequestId,
            string battleId,
            string battleResultId,
            string catalogSetId,
            IDictionary<string, long> activeTroops)
        {
            if (activeTroops == null) throw new ArgumentNullException(nameof(activeTroops));
            return new BattleApplicationState(
                profileId,
                battleRequestId,
                battleId,
                battleResultId,
                catalogSetId,
                new Dictionary<string, long>(activeTroops, StringComparer.Ordinal),
                new Dictionary<string, long>(StringComparer.Ordinal),
                new Dictionary<string, CommittedBattleResultReceipt>(StringComparer.Ordinal),
                0, 0, 0, 0, 0, 0L);
        }

        public long GetActiveTroops(string troopDefinitionId)
        {
            return _activeTroops.TryGetValue(troopDefinitionId, out long value) ? value : 0L;
        }

        public long GetWoundedTroops(string troopDefinitionId)
        {
            return _woundedTroops.TryGetValue(troopDefinitionId, out long value) ? value : 0L;
        }

        public void SetTroopOutcome(string troopDefinitionId, long survived, long wounded)
        {
            _activeTroops[troopDefinitionId] = survived;
            _woundedTroops[troopDefinitionId] = wounded;
        }

        public void AddCredits(int amount) { Credits = checked(Credits + amount); }
        public void AddFood(int amount) { Food = checked(Food + amount); }
        public void AddGold(int amount) { Gold = checked(Gold + amount); }
        public void AddExperience(int amount) { Experience = checked(Experience + amount); }
        public void AddWinBattleProgress(int amount) { WinBattleProgress = checked(WinBattleProgress + amount); }

        internal bool TryGetReceipt(string battleResultId, out CommittedBattleResultReceipt receipt)
        {
            return _appliedResults.TryGetValue(battleResultId, out receipt);
        }

        internal CommittedBattleResultReceipt Record(BattleComputedResult result)
        {
            CommitSequence = checked(CommitSequence + 1L);
            var receipt = new CommittedBattleResultReceipt(
                result.BattleResultId,
                result.BattleId,
                result.BattleRequestId,
                result.ProfileId,
                result.ComputationSha256,
                CommitSequence);
            _appliedResults.Add(result.BattleResultId, receipt);
            return receipt;
        }

        internal BattleApplicationState Clone()
        {
            return new BattleApplicationState(
                ProfileId,
                ActiveBattleRequestId,
                ActiveBattleId,
                ExpectedBattleResultId,
                CatalogSetId,
                new Dictionary<string, long>(_activeTroops, StringComparer.Ordinal),
                new Dictionary<string, long>(_woundedTroops, StringComparer.Ordinal),
                new Dictionary<string, CommittedBattleResultReceipt>(_appliedResults, StringComparer.Ordinal),
                Credits, Food, Gold, Experience, WinBattleProgress, CommitSequence);
        }
    }

    public sealed class InMemoryBattleResultStore : IBattleResultStore
    {
        private readonly object _gate = new object();
        private BattleApplicationState _state;

        public InMemoryBattleResultStore(BattleApplicationState initialState)
        {
            _state = initialState?.Clone() ?? throw new ArgumentNullException(nameof(initialState));
        }

        public BattleResultApplicationResult ExecuteAtomic(
            Func<BattleApplicationState, BattleResultApplicationResult> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            lock (_gate)
            {
                BattleApplicationState candidate = _state.Clone();
                BattleResultApplicationResult result = operation(candidate);
                if (result != null && result.Status == BattleResultApplicationStatus.Committed)
                    _state = candidate;
                return result;
            }
        }

        public BattleApplicationState Snapshot()
        {
            lock (_gate) return _state.Clone();
        }
    }

    public sealed class AtomicBattleResultAdapter
    {
        private readonly IBattleResultStore _store;
        private readonly ReadOnlyCollection<IBattleResultConsumer> _consumers;

        public AtomicBattleResultAdapter(
            IBattleResultStore store,
            IEnumerable<IBattleResultConsumer> additionalConsumers = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            var consumers = new List<IBattleResultConsumer>
            {
                new TroopLossConsumer(),
                new RewardConsumer(),
                new ProgressionConsumer()
            };
            if (additionalConsumers != null) consumers.AddRange(additionalConsumers);
            ValidateConsumers(consumers);
            _consumers = Array.AsReadOnly(consumers.ToArray());
        }

        public BattleResultApplicationResult Apply(BattleComputedResult result)
        {
            BattleResultApplicationStatus validation = ValidateResult(result);
            if (validation != BattleResultApplicationStatus.Committed)
                return Failure(validation, "AL-BATTLE-APPLICATION-INVALID-RESULT");

            try
            {
                return _store.ExecuteAtomic(candidate => ApplyToCandidate(result, candidate)) ??
                    Failure(BattleResultApplicationStatus.PersistenceFailed,
                        "AL-BATTLE-APPLICATION-STORE-NO-RESULT");
            }
            catch (Exception)
            {
                return Failure(BattleResultApplicationStatus.PersistenceFailed,
                    "AL-BATTLE-APPLICATION-PERSISTENCE-FAILED");
            }
        }

        private BattleResultApplicationResult ApplyToCandidate(
            BattleComputedResult result,
            BattleApplicationState candidate)
        {
            if (candidate == null)
                return Failure(BattleResultApplicationStatus.PersistenceFailed,
                    "AL-BATTLE-APPLICATION-STORE-STATE-MISSING");

            if (candidate.TryGetReceipt(result.BattleResultId, out CommittedBattleResultReceipt receipt))
            {
                if (string.Equals(receipt.ComputationSha256, result.ComputationSha256, StringComparison.Ordinal) &&
                    string.Equals(receipt.BattleId, result.BattleId, StringComparison.Ordinal) &&
                    string.Equals(receipt.BattleRequestId, result.BattleRequestId, StringComparison.Ordinal) &&
                    string.Equals(receipt.ProfileId, result.ProfileId, StringComparison.Ordinal))
                {
                    return new BattleResultApplicationResult(
                        BattleResultApplicationStatus.AlreadyCommitted,
                        receipt,
                        string.Empty);
                }
                return Failure(BattleResultApplicationStatus.CorrelationConflict,
                    "AL-BATTLE-LEDGER-CORRELATION-CONFLICT");
            }

            if (!MatchesActiveBattle(result, candidate))
                return Failure(BattleResultApplicationStatus.InactiveOrMismatchedBattle,
                    "AL-BATTLE-APPLICATION-INACTIVE-IDENTITY");

            try
            {
                for (int index = 0; index < _consumers.Count; index++)
                    _consumers[index].Validate(result, candidate);
                for (int index = 0; index < _consumers.Count; index++)
                    _consumers[index].Apply(result, candidate);
                CommittedBattleResultReceipt committed = candidate.Record(result);
                return new BattleResultApplicationResult(
                    BattleResultApplicationStatus.Committed,
                    committed,
                    string.Empty);
            }
            catch (Exception)
            {
                return Failure(BattleResultApplicationStatus.ConsumerFailed,
                    "AL-BATTLE-APPLICATION-CONSUMER-FAILED");
            }
        }

        private static BattleResultApplicationStatus ValidateResult(BattleComputedResult result)
        {
            if (result == null) return BattleResultApplicationStatus.InvalidComputedResult;
            if (result.ExecutionMode == BattleExecutionMode.Preview)
                return BattleResultApplicationStatus.PreviewRejected;
            if (result.ExecutionMode != BattleExecutionMode.Authoritative ||
                !string.Equals(result.ExpectedResultConsumerId,
                    BattleTechnicalLimits.ExpectedResultConsumerId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(result.BattleResultId) ||
                result.BattleResultId.StartsWith(BattleTechnicalLimits.PreviewResultPrefix,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    result.DeterminismVersion,
                    BattleTechnicalLimits.SupportedDeterminismVersion,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(result.ComputationSha256) ||
                result.RewardProposal == null ||
                result.AttackerLosses == null)
                return BattleResultApplicationStatus.InvalidComputedResult;
            try
            {
                return string.Equals(
                    BattleCanonicalHash.Result(result),
                    result.ComputationSha256,
                    StringComparison.Ordinal)
                    ? BattleResultApplicationStatus.Committed
                    : BattleResultApplicationStatus.InvalidComputedResult;
            }
            catch (Exception)
            {
                return BattleResultApplicationStatus.InvalidComputedResult;
            }
        }

        private static bool MatchesActiveBattle(
            BattleComputedResult result,
            BattleApplicationState state)
        {
            return string.Equals(state.ProfileId, result.ProfileId, StringComparison.Ordinal) &&
                string.Equals(state.ActiveBattleRequestId, result.BattleRequestId, StringComparison.Ordinal) &&
                string.Equals(state.ActiveBattleId, result.BattleId, StringComparison.Ordinal) &&
                string.Equals(state.ExpectedBattleResultId, result.BattleResultId, StringComparison.Ordinal) &&
                string.Equals(state.CatalogSetId, result.CatalogSetId, StringComparison.Ordinal);
        }

        private static void ValidateConsumers(IReadOnlyList<IBattleResultConsumer> consumers)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < consumers.Count; index++)
            {
                IBattleResultConsumer consumer = consumers[index];
                if (consumer == null || string.IsNullOrWhiteSpace(consumer.ConsumerId) ||
                    !ids.Add(consumer.ConsumerId))
                    throw new ArgumentException("Battle result consumers require unique non-empty identities.");
            }
        }

        private static BattleResultApplicationResult Failure(
            BattleResultApplicationStatus status,
            string code)
        {
            return new BattleResultApplicationResult(status, null, code);
        }

        private sealed class TroopLossConsumer : IBattleResultConsumer
        {
            public string ConsumerId => "consumer.battle.troop_losses";

            public void Validate(BattleComputedResult result, BattleApplicationState candidate)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < result.AttackerLosses.Count; index++)
                {
                    BattleTroopLoss loss = result.AttackerLosses[index];
                    if (loss == null || string.IsNullOrWhiteSpace(loss.TroopDefinitionId) ||
                        !ids.Add(loss.TroopDefinitionId) || loss.Killed < 0L ||
                        loss.Wounded < 0L || loss.Survived < 0L)
                        throw new InvalidOperationException("Invalid troop loss row.");
                    long expected = checked(checked(loss.Killed + loss.Wounded) + loss.Survived);
                    if (candidate.GetActiveTroops(loss.TroopDefinitionId) != expected)
                        throw new InvalidOperationException("Troop state does not match the result basis.");
                }
            }

            public void Apply(BattleComputedResult result, BattleApplicationState candidate)
            {
                for (int index = 0; index < result.AttackerLosses.Count; index++)
                {
                    BattleTroopLoss loss = result.AttackerLosses[index];
                    candidate.SetTroopOutcome(loss.TroopDefinitionId, loss.Survived, loss.Wounded);
                }
            }
        }

        private sealed class RewardConsumer : IBattleResultConsumer
        {
            public string ConsumerId => "consumer.battle.rewards";

            public void Validate(BattleComputedResult result, BattleApplicationState candidate)
            {
                BattleRewardProposal reward = result.RewardProposal;
                if (reward.Credits < 0 || reward.Food < 0 || reward.Gold < 0 || reward.Experience < 0)
                    throw new InvalidOperationException("Negative proposed reward.");
                checked
                {
                    _ = candidate.Credits + reward.Credits;
                    _ = candidate.Food + reward.Food;
                    _ = candidate.Gold + reward.Gold;
                    _ = candidate.Experience + reward.Experience;
                }
            }

            public void Apply(BattleComputedResult result, BattleApplicationState candidate)
            {
                candidate.AddCredits(result.RewardProposal.Credits);
                candidate.AddFood(result.RewardProposal.Food);
                candidate.AddGold(result.RewardProposal.Gold);
                candidate.AddExperience(result.RewardProposal.Experience);
            }
        }

        private sealed class ProgressionConsumer : IBattleResultConsumer
        {
            public string ConsumerId => "consumer.battle.progression";
            public void Validate(BattleComputedResult result, BattleApplicationState candidate) { }

            public void Apply(BattleComputedResult result, BattleApplicationState candidate)
            {
                if (result.Outcome == BattleOutcome.AttackerVictory)
                    candidate.AddWinBattleProgress(1);
            }
        }
    }
}
