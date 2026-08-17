using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Battle.Profiles;

namespace AL.Battle.Application
{
    public enum BattlePipelineStatus
    {
        Applied = 0,
        AlreadyApplied = 1,
        InvalidSource = 2,
        InvalidExecutionMode = 3,
        ComputationFailed = 4,
        ApplicationRejected = 5,
        RecoveryRequired = 6,
        Cancelled = 7
    }

    public sealed class BattlePipelineResult
    {
        private readonly ReadOnlyCollection<BattleDiagnostic> _diagnostics;

        internal BattlePipelineResult(
            BattlePipelineStatus status,
            BattleComputedResult computedResult,
            BattleTransactionRecord transaction,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            Status = status;
            ComputedResult = computedResult;
            Transaction = transaction;
            _diagnostics = Array.AsReadOnly(diagnostics == null
                ? Array.Empty<BattleDiagnostic>()
                : new List<BattleDiagnostic>(diagnostics).ToArray());
        }

        public BattlePipelineStatus Status { get; }
        public BattleComputedResult ComputedResult { get; }
        public BattleTransactionRecord Transaction { get; }
        public IReadOnlyList<BattleDiagnostic> Diagnostics => _diagnostics;
        public bool IsApplied =>
            Status == BattlePipelineStatus.Applied ||
            Status == BattlePipelineStatus.AlreadyApplied;
    }

    /// <summary>
    /// The single authoritative battle entry boundary. Source validation and pure computation occur
    /// before durable persistence; once persisted, all effects flow through the transaction
    /// coordinator and its atomic idempotent adapter.
    /// </summary>
    public sealed class AuthoritativeBattlePipeline
    {
        private readonly BattleTransactionCoordinator _coordinator;

        public AuthoritativeBattlePipeline(BattleTransactionCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public BattlePipelineResult Execute(
            BattleAuthoritativeSourceState source,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Result(BattlePipelineStatus.Cancelled, null, null, null);
            if (source != null && source.Configuration != null &&
                source.Configuration.ExecutionMode != BattleExecutionMode.Authoritative)
                return Result(BattlePipelineStatus.InvalidExecutionMode, null, null, null);

            BattleSourceProfileResult build = BattleSourceProfileBuilder.Build(source);
            if (!build.IsSuccess)
                return Result(BattlePipelineStatus.InvalidSource, null, null, build.Diagnostics);

            BattleComputationResult computation = DeterministicBattleComputation.Compute(build.Request);
            if (!computation.IsSuccess || computation.Value == null)
                return Result(
                    BattlePipelineStatus.ComputationFailed,
                    null,
                    null,
                    computation.Diagnostics);

            if (cancellationToken.IsCancellationRequested)
                return Result(BattlePipelineStatus.Cancelled, computation.Value, null, null);

            BattleTransactionRecord begun = _coordinator.Begin(computation.Value);
            if (begun.State == BattleTransactionState.Applied ||
                begun.State == BattleTransactionState.Acknowledged)
                return Result(BattlePipelineStatus.AlreadyApplied, computation.Value, begun, null);
            if (begun.State == BattleTransactionState.Rejected)
                return Result(BattlePipelineStatus.ApplicationRejected, computation.Value, begun, null);
            if (begun.State == BattleTransactionState.RecoveryRequired)
                return Result(BattlePipelineStatus.RecoveryRequired, computation.Value, begun, null);

            BattleTransactionRecord applied = _coordinator.Apply(computation.Value.BattleResultId);
            switch (applied.State)
            {
                case BattleTransactionState.Applied:
                    return Result(BattlePipelineStatus.Applied, computation.Value, applied, null);
                case BattleTransactionState.Acknowledged:
                    return Result(BattlePipelineStatus.AlreadyApplied, computation.Value, applied, null);
                case BattleTransactionState.RecoveryRequired:
                case BattleTransactionState.Applying:
                case BattleTransactionState.Pending:
                    return Result(BattlePipelineStatus.RecoveryRequired, computation.Value, applied, null);
                default:
                    return Result(BattlePipelineStatus.ApplicationRejected, computation.Value, applied, null);
            }
        }

        public BattleTransactionRecoveryReport RecoverPending()
        {
            return _coordinator.RecoverAll();
        }

        private static BattlePipelineResult Result(
            BattlePipelineStatus status,
            BattleComputedResult computedResult,
            BattleTransactionRecord transaction,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            return new BattlePipelineResult(status, computedResult, transaction, diagnostics);
        }
    }
}
