using System;

namespace AL.Motion
{
    public enum MotionTransitionOutcome
    {
        Accepted = 0,
        RejectedPriority = 1,
        RejectedActionSequence = 2,
        RejectedByGameplay = 3,
        CancelledPreCommit = 4,
        InterruptedPostCommit = 5,
        CompletedToSafeMotion = 6
    }

    public sealed class MotionTransitionResult
    {
        internal MotionTransitionResult(
            MotionTransitionOutcome outcome,
            MotionClipDefinition active,
            long actionSequence,
            float blendSeconds)
        {
            Outcome = outcome;
            Active = active;
            ActionSequence = actionSequence;
            BlendSeconds = blendSeconds;
        }

        public MotionTransitionOutcome Outcome { get; }
        public MotionClipDefinition Active { get; }
        public long ActionSequence { get; }
        public float BlendSeconds { get; }
    }

    public sealed class MotionTransitionMachine
    {
        public const float MaximumBlendSeconds = 0.15f;
        public const float MaximumRecoverySeconds = 0.75f;

        private readonly MotionCatalogSnapshot _catalog;
        private bool _committed;
        private long _actionSequence;

        public MotionTransitionMachine(MotionCatalogSnapshot catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!_catalog.TryGetExact(_catalog.SafeMotionKey, out MotionClipDefinition safe))
            {
                throw new InvalidOperationException("Motion catalog has no safe motion.");
            }

            Current = safe;
        }

        public MotionClipDefinition Current { get; private set; }
        public long ActionSequence => _actionSequence;
        public bool IsCommitted => _committed;

        public bool TryRequest(
            string motionKey,
            long actionSequence,
            out MotionTransitionResult result)
        {
            if (actionSequence <= 0 || actionSequence < _actionSequence)
            {
                result = Result(MotionTransitionOutcome.RejectedActionSequence, Current);
                return false;
            }

            if (!_catalog.TryResolve(motionKey, out MotionClipDefinition requested))
            {
                result = Result(MotionTransitionOutcome.RejectedByGameplay, Current);
                return false;
            }

            if (requested.Priority < Current.Priority &&
                Current.MotionKey != _catalog.SafeMotionKey)
            {
                result = Result(MotionTransitionOutcome.RejectedPriority, Current);
                return false;
            }

            Current = requested;
            _actionSequence = actionSequence;
            _committed = false;
            result = Result(MotionTransitionOutcome.Accepted, Current);
            return true;
        }

        public bool MarkCommitted(long actionSequence)
        {
            if (actionSequence != _actionSequence || actionSequence <= 0)
            {
                return false;
            }

            _committed = true;
            return true;
        }

        public MotionTransitionResult Cancel(long actionSequence, bool gameplayAccepted)
        {
            if (actionSequence != _actionSequence || actionSequence <= 0)
            {
                return Result(MotionTransitionOutcome.RejectedActionSequence, Current);
            }

            if (!gameplayAccepted)
            {
                return Result(MotionTransitionOutcome.RejectedByGameplay, Current);
            }

            MotionTransitionOutcome outcome = _committed
                ? MotionTransitionOutcome.InterruptedPostCommit
                : MotionTransitionOutcome.CancelledPreCommit;
            string recoveryKey = _committed
                ? "skill.interruption"
                : "skill.cancellation";
            _catalog.TryResolve(recoveryKey, out MotionClipDefinition recovery);
            Current = recovery;
            _committed = false;
            return Result(outcome, Current);
        }

        public MotionTransitionResult CompleteCurrent()
        {
            _catalog.TryGetExact(_catalog.SafeMotionKey, out MotionClipDefinition safe);
            Current = safe;
            _committed = false;
            return Result(MotionTransitionOutcome.CompletedToSafeMotion, Current);
        }

        private MotionTransitionResult Result(
            MotionTransitionOutcome outcome,
            MotionClipDefinition active)
        {
            return new MotionTransitionResult(
                outcome,
                active,
                _actionSequence,
                MaximumBlendSeconds);
        }
    }
}
