using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Battle.Profiles;

namespace AL.Battle.Application
{
    public enum BattlePreviewStatus
    {
        Computed = 0,
        InvalidSource = 1,
        InvalidExecutionMode = 2,
        ComputationFailed = 3
    }

    public sealed class BattlePreviewResult
    {
        private readonly ReadOnlyCollection<BattleDiagnostic> _diagnostics;

        internal BattlePreviewResult(
            BattlePreviewStatus status,
            BattleComputedResult computedResult,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            Status = status;
            ComputedResult = computedResult;
            _diagnostics = Array.AsReadOnly(diagnostics == null
                ? Array.Empty<BattleDiagnostic>()
                : new List<BattleDiagnostic>(diagnostics).ToArray());
        }

        public BattlePreviewStatus Status { get; }
        public BattleComputedResult ComputedResult { get; }
        public IReadOnlyList<BattleDiagnostic> Diagnostics => _diagnostics;
    }

    /// <summary>
    /// Pure preview-only entry point. It cannot receive a result store or transaction persistence,
    /// so a presentation caller cannot accidentally commit proposed rewards or progression.
    /// </summary>
    public sealed class BattlePreviewPipeline
    {
        public BattlePreviewResult Execute(BattleAuthoritativeSourceState source)
        {
            if (source != null && source.Configuration != null &&
                source.Configuration.ExecutionMode != BattleExecutionMode.Preview)
                return Result(BattlePreviewStatus.InvalidExecutionMode, null, null);

            BattleSourceProfileResult build = BattleSourceProfileBuilder.Build(source);
            if (!build.IsSuccess)
                return Result(BattlePreviewStatus.InvalidSource, null, build.Diagnostics);

            BattleComputationResult computation = DeterministicBattleComputation.Compute(build.Request);
            if (!computation.IsSuccess || computation.Value == null)
                return Result(BattlePreviewStatus.ComputationFailed, null, computation.Diagnostics);
            return Result(BattlePreviewStatus.Computed, computation.Value, null);
        }

        private static BattlePreviewResult Result(
            BattlePreviewStatus status,
            BattleComputedResult computedResult,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            return new BattlePreviewResult(status, computedResult, diagnostics);
        }
    }
}
