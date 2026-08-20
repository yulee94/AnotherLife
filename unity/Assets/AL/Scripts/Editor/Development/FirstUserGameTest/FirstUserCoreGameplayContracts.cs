#if !UNITY_EDITOR
#error The isolated first-user core gameplay contracts are Editor-only.
#endif

using System;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal enum FirstUserCoreTransitionStatus
    {
        Rejected = 0,
        Applied = 1,
        DuplicateIgnored = 2,
        Waiting = 3,
        FailClosed = 4
    }

    internal enum FirstUserFocusState
    {
        Invalid = 0,
        Active = 1,
        Suspended = 2,
        ResumePending = 3,
        AwaitingNeutralInput = 4,
        FailClosed = 5
    }

    internal enum FirstUserExitState
    {
        Inactive = 0,
        Prompted = 1,
        Committed = 2
    }

    internal enum FirstUserRuntimeFailureDisposition
    {
        HardStop = 0,
        RetainBlockedPanel = 1
    }

    internal enum FirstUserMovementProofState
    {
        Invalid = 0,
        Pending = 1,
        Confirmed = 2,
        Contaminated = 3
    }

    internal enum FirstUserAttackProofState
    {
        Invalid = 0,
        AcceptedStart = 1,
        ActiveObserved = 2,
        Settled = 3,
        Contaminated = 4
    }

    internal readonly struct FirstUserFocusSnapshot
    {
        internal FirstUserFocusSnapshot(
            string sessionId,
            int generation,
            FirstUserFocusState state,
            int epoch)
        {
            SessionId = sessionId ?? string.Empty;
            Generation = generation;
            State = state;
            Epoch = epoch;
        }

        internal string SessionId { get; }
        internal int Generation { get; }
        internal FirstUserFocusState State { get; }
        internal int Epoch { get; }

        internal bool IsCanonical =>
            FirstUserCoreGameplayPlanner.IsCanonicalSessionId(SessionId) &&
            Generation > 0 &&
            Epoch >= 0 &&
            (State == FirstUserFocusState.Active ||
             State == FirstUserFocusState.Suspended ||
             State == FirstUserFocusState.ResumePending ||
             State == FirstUserFocusState.AwaitingNeutralInput ||
             State == FirstUserFocusState.FailClosed);
    }

    internal readonly struct FirstUserResumeEvidence
    {
        internal FirstUserResumeEvidence(
            bool sessionMatches,
            bool generationMatches,
            bool phaseAndSceneMatch,
            bool runtimeHostMatches,
            bool identityAndCustomizationMatch,
            bool isolatedRootMatches,
            bool profileServiceMatches,
            bool productionProfileRemainsNonWritable,
            bool productionTickSuppressed,
            bool receiptAndProjectionMatch,
            bool tutorialAndOmenMatch,
            bool eventSystemAndInputModuleMatch,
            bool environmentLeaseMatches)
        {
            SessionMatches = sessionMatches;
            GenerationMatches = generationMatches;
            PhaseAndSceneMatch = phaseAndSceneMatch;
            RuntimeHostMatches = runtimeHostMatches;
            IdentityAndCustomizationMatch = identityAndCustomizationMatch;
            IsolatedRootMatches = isolatedRootMatches;
            ProfileServiceMatches = profileServiceMatches;
            ProductionProfileRemainsNonWritable = productionProfileRemainsNonWritable;
            ProductionTickSuppressed = productionTickSuppressed;
            ReceiptAndProjectionMatch = receiptAndProjectionMatch;
            TutorialAndOmenMatch = tutorialAndOmenMatch;
            EventSystemAndInputModuleMatch = eventSystemAndInputModuleMatch;
            EnvironmentLeaseMatches = environmentLeaseMatches;
        }

        internal bool SessionMatches { get; }
        internal bool GenerationMatches { get; }
        internal bool PhaseAndSceneMatch { get; }
        internal bool RuntimeHostMatches { get; }
        internal bool IdentityAndCustomizationMatch { get; }
        internal bool IsolatedRootMatches { get; }
        internal bool ProfileServiceMatches { get; }
        internal bool ProductionProfileRemainsNonWritable { get; }
        internal bool ProductionTickSuppressed { get; }
        internal bool ReceiptAndProjectionMatch { get; }
        internal bool TutorialAndOmenMatch { get; }
        internal bool EventSystemAndInputModuleMatch { get; }
        internal bool EnvironmentLeaseMatches { get; }

        internal bool IsExact =>
            SessionMatches &&
            GenerationMatches &&
            PhaseAndSceneMatch &&
            RuntimeHostMatches &&
            IdentityAndCustomizationMatch &&
            IsolatedRootMatches &&
            ProfileServiceMatches &&
            ProductionProfileRemainsNonWritable &&
            ProductionTickSuppressed &&
            ReceiptAndProjectionMatch &&
            TutorialAndOmenMatch &&
            EventSystemAndInputModuleMatch &&
            EnvironmentLeaseMatches;

        internal static FirstUserResumeEvidence Exact => new FirstUserResumeEvidence(
            sessionMatches: true,
            generationMatches: true,
            phaseAndSceneMatch: true,
            runtimeHostMatches: true,
            identityAndCustomizationMatch: true,
            isolatedRootMatches: true,
            profileServiceMatches: true,
            productionProfileRemainsNonWritable: true,
            productionTickSuppressed: true,
            receiptAndProjectionMatch: true,
            tutorialAndOmenMatch: true,
            eventSystemAndInputModuleMatch: true,
            environmentLeaseMatches: true);
    }

    internal readonly struct FirstUserFocusTransition
    {
        internal FirstUserFocusTransition(
            FirstUserCoreTransitionStatus status,
            FirstUserFocusSnapshot snapshot,
            string diagnostic)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal FirstUserCoreTransitionStatus Status { get; }
        internal FirstUserFocusSnapshot Snapshot { get; }
        internal string Diagnostic { get; }
    }

    internal readonly struct FirstUserMovementProof
    {
        internal FirstUserMovementProof(
            FirstUserMovementProofState state,
            string sessionId,
            int generation,
            int focusEpoch,
            int attackGeneration,
            float originX,
            float originZ,
            float directionX,
            float directionZ)
        {
            State = state;
            SessionId = sessionId ?? string.Empty;
            Generation = generation;
            FocusEpoch = focusEpoch;
            AttackGeneration = attackGeneration;
            OriginX = originX;
            OriginZ = originZ;
            DirectionX = directionX;
            DirectionZ = directionZ;
        }

        internal FirstUserMovementProofState State { get; }
        internal string SessionId { get; }
        internal int Generation { get; }
        internal int FocusEpoch { get; }
        internal int AttackGeneration { get; }
        internal float OriginX { get; }
        internal float OriginZ { get; }
        internal float DirectionX { get; }
        internal float DirectionZ { get; }
        internal bool IsPending => State == FirstUserMovementProofState.Pending;
        internal bool IsConfirmed => State == FirstUserMovementProofState.Confirmed;
    }

    internal readonly struct FirstUserMovementTransition
    {
        internal FirstUserMovementTransition(
            FirstUserCoreTransitionStatus status,
            FirstUserMovementProof proof,
            string diagnostic)
        {
            Status = status;
            Proof = proof;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal FirstUserCoreTransitionStatus Status { get; }
        internal FirstUserMovementProof Proof { get; }
        internal string Diagnostic { get; }
    }

    internal readonly struct FirstUserAttackProof
    {
        internal FirstUserAttackProof(
            FirstUserAttackProofState state,
            string sessionId,
            int generation,
            int focusEpoch,
            int attackSequence,
            int acceptedFrame,
            int lastObservedFrame,
            bool mechanicsResultObserved)
        {
            State = state;
            SessionId = sessionId ?? string.Empty;
            Generation = generation;
            FocusEpoch = focusEpoch;
            AttackSequence = attackSequence;
            AcceptedFrame = acceptedFrame;
            LastObservedFrame = lastObservedFrame;
            MechanicsResultObserved = mechanicsResultObserved;
        }

        internal FirstUserAttackProofState State { get; }
        internal string SessionId { get; }
        internal int Generation { get; }
        internal int FocusEpoch { get; }
        internal int AttackSequence { get; }
        internal int AcceptedFrame { get; }
        internal int LastObservedFrame { get; }
        internal bool MechanicsResultObserved { get; }
        internal bool IsPending =>
            State == FirstUserAttackProofState.AcceptedStart ||
            State == FirstUserAttackProofState.ActiveObserved;
        internal bool IsSettled => State == FirstUserAttackProofState.Settled;
    }

    internal readonly struct FirstUserAttackTransition
    {
        internal FirstUserAttackTransition(
            FirstUserCoreTransitionStatus status,
            FirstUserAttackProof proof,
            string diagnostic)
        {
            Status = status;
            Proof = proof;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal FirstUserCoreTransitionStatus Status { get; }
        internal FirstUserAttackProof Proof { get; }
        internal string Diagnostic { get; }
    }

    internal readonly struct FirstUserExitTransition
    {
        internal FirstUserExitTransition(
            FirstUserCoreTransitionStatus status,
            FirstUserExitState state,
            string diagnostic)
        {
            Status = status;
            State = state;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal FirstUserCoreTransitionStatus Status { get; }
        internal FirstUserExitState State { get; }
        internal string Diagnostic { get; }
    }

    internal readonly struct FirstUserOmenProjection
    {
        internal FirstUserOmenProjection(
            int revision,
            string stateId,
            string pendingActionId,
            bool pendingChoice,
            int acceptedCount,
            int progressedCount,
            int completedCount,
            int rewardCount)
        {
            Revision = revision;
            StateId = stateId ?? string.Empty;
            PendingActionId = pendingActionId ?? string.Empty;
            PendingChoice = pendingChoice;
            AcceptedCount = acceptedCount;
            ProgressedCount = progressedCount;
            CompletedCount = completedCount;
            RewardCount = rewardCount;
        }

        internal int Revision { get; }
        internal string StateId { get; }
        internal string PendingActionId { get; }
        internal bool PendingChoice { get; }
        internal int AcceptedCount { get; }
        internal int ProgressedCount { get; }
        internal int CompletedCount { get; }
        internal int RewardCount { get; }
    }
}
