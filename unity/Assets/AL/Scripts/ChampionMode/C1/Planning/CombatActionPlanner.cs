using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AL.ChampionMode.C1
{
    public enum CombatActionSource
    {
        ManualInput = 0,
        AssistAI = 1,
        FullAutoAI = 2,
        EncounterScript = 3
    }

    public enum CombatActionPlanStatus
    {
        Applied = 0,
        DuplicateExact = 1,
        CorrelationConflict = 2,
        InvalidRequest = 3,
        WrongEncounter = 4,
        WrongActor = 5,
        ActorUnavailable = 6,
        ActorDefeated = 7,
        EncounterNotActive = 8,
        ControlLocked = 9,
        SkillUnavailable = 10,
        TargetInvalid = 11,
        OutOfRange = 12,
        CooldownActive = 13,
        InsufficientResource = 14,
        StaleRevision = 15,
        ProhibitedTransition = 16,
        PolicyViolation = 17,
        TerminalState = 18,
        /// <summary>
        /// Recoverable admission block. The owning adapter must reconcile or
        /// explicitly compact/recover capacity; it must never silently retry.
        /// </summary>
        CapacityReached = 19
    }

    public enum CombatActionPolicyPoint
    {
        None = 0,
        RequestAccepted = 1,
        ResourceReserved = 2,
        Committed = 3,
        Resolving = 4,
        Completed = 5
    }

    public enum CombatActionTerminalReason
    {
        None = 0,
        ValidationRejected = 1,
        ManualCancellation = 2,
        ActorDefeated = 3,
        EncounterTerminated = 4,
        SceneDisposed = 5,
        ComponentDisabled = 6,
        Interrupted = 7,
        EffectFailed = 8,
        Completed = 9
    }

    public enum CombatActionReceiptKind
    {
        ActionRequested = 0,
        ActionStateChanged = 1,
        ManaReserved = 2,
        ManaCommitted = 3,
        ManaReleased = 4,
        ManaRefunded = 5,
        EffectApplied = 6,
        CooldownStarted = 7,
        Terminal = 8
    }

    public enum CombatActionManaOutcome
    {
        None = 0,
        Reserved = 1,
        Committed = 2,
        Released = 3,
        Refunded = 4
    }

    public enum CombatActionEffectOutcome
    {
        None = 0,
        Applied = 1,
        Failed = 2
    }

    public sealed class CombatActionResourcePolicy
    {
        private CombatActionResourcePolicy(
            CombatStableId policyId,
            long manaCostMicros,
            CombatActionPolicyPoint manaReservationPoint,
            CombatActionPolicyPoint manaCommitPoint,
            CombatActionPolicyPoint cooldownStartPoint,
            long cooldownDurationMicros,
            bool refundCommittedOnCancellation,
            bool refundCommittedOnInterruption,
            bool refundCommittedOnFailure,
            bool cooldownOnCancellation,
            bool cooldownOnInterruption,
            bool cooldownOnFailure,
            bool interruptibleDuringWindup,
            bool interruptibleDuringResolution)
        {
            PolicyId = policyId;
            ManaCostMicros = manaCostMicros;
            ManaReservationPoint = manaReservationPoint;
            ManaCommitPoint = manaCommitPoint;
            CooldownStartPoint = cooldownStartPoint;
            CooldownDurationMicros = cooldownDurationMicros;
            RefundCommittedOnCancellation = refundCommittedOnCancellation;
            RefundCommittedOnInterruption = refundCommittedOnInterruption;
            RefundCommittedOnFailure = refundCommittedOnFailure;
            CooldownOnCancellation = cooldownOnCancellation;
            CooldownOnInterruption = cooldownOnInterruption;
            CooldownOnFailure = cooldownOnFailure;
            InterruptibleDuringWindup = interruptibleDuringWindup;
            InterruptibleDuringResolution = interruptibleDuringResolution;
        }

        public CombatStableId PolicyId { get; }
        public long ManaCostMicros { get; }
        public CombatActionPolicyPoint ManaReservationPoint { get; }
        public CombatActionPolicyPoint ManaCommitPoint { get; }
        public CombatActionPolicyPoint CooldownStartPoint { get; }
        public long CooldownDurationMicros { get; }
        public bool RefundCommittedOnCancellation { get; }
        public bool RefundCommittedOnInterruption { get; }
        public bool RefundCommittedOnFailure { get; }
        public bool CooldownOnCancellation { get; }
        public bool CooldownOnInterruption { get; }
        public bool CooldownOnFailure { get; }
        public bool InterruptibleDuringWindup { get; }
        public bool InterruptibleDuringResolution { get; }

        public static bool TryCreate(
            CombatStableId policyId,
            long manaCostMicros,
            CombatActionPolicyPoint manaReservationPoint,
            CombatActionPolicyPoint manaCommitPoint,
            CombatActionPolicyPoint cooldownStartPoint,
            long cooldownDurationMicros,
            bool refundCommittedOnCancellation,
            bool refundCommittedOnInterruption,
            bool refundCommittedOnFailure,
            bool cooldownOnCancellation,
            bool cooldownOnInterruption,
            bool cooldownOnFailure,
            bool interruptibleDuringWindup,
            bool interruptibleDuringResolution,
            out CombatActionResourcePolicy policy)
        {
            policy = null;
            if (policyId.IsDefault ||
                !Enum.IsDefined(typeof(CombatActionPolicyPoint), manaReservationPoint) ||
                !Enum.IsDefined(typeof(CombatActionPolicyPoint), manaCommitPoint) ||
                !Enum.IsDefined(typeof(CombatActionPolicyPoint), cooldownStartPoint) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    manaCostMicros,
                    CombatScalarKind.Mana,
                    false) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    cooldownDurationMicros,
                    CombatScalarKind.Duration,
                    false))
            {
                return false;
            }

            if (manaCostMicros == 0L)
            {
                if (manaReservationPoint != CombatActionPolicyPoint.None ||
                    manaCommitPoint != CombatActionPolicyPoint.None)
                {
                    return false;
                }
            }
            else if (manaReservationPoint == CombatActionPolicyPoint.None ||
                     manaCommitPoint == CombatActionPolicyPoint.None ||
                     (manaReservationPoint !=
                          CombatActionPolicyPoint.RequestAccepted &&
                      manaReservationPoint !=
                          CombatActionPolicyPoint.ResourceReserved) ||
                     (manaCommitPoint != CombatActionPolicyPoint.RequestAccepted &&
                      manaCommitPoint != CombatActionPolicyPoint.Committed) ||
                     PolicyRank(manaReservationPoint) > PolicyRank(manaCommitPoint))
            {
                return false;
            }

            if (cooldownStartPoint != CombatActionPolicyPoint.None &&
                cooldownStartPoint != CombatActionPolicyPoint.RequestAccepted &&
                cooldownStartPoint != CombatActionPolicyPoint.Committed &&
                cooldownStartPoint != CombatActionPolicyPoint.Resolving &&
                cooldownStartPoint != CombatActionPolicyPoint.Completed)
            {
                return false;
            }

            if ((cooldownStartPoint == CombatActionPolicyPoint.None) !=
                (cooldownDurationMicros == 0L))
            {
                return false;
            }

            policy = new CombatActionResourcePolicy(
                policyId,
                manaCostMicros,
                manaReservationPoint,
                manaCommitPoint,
                cooldownStartPoint,
                cooldownDurationMicros,
                refundCommittedOnCancellation,
                refundCommittedOnInterruption,
                refundCommittedOnFailure,
                cooldownOnCancellation,
                cooldownOnInterruption,
                cooldownOnFailure,
                interruptibleDuringWindup,
                interruptibleDuringResolution);
            return true;
        }

        internal bool PayloadEquals(CombatActionResourcePolicy other)
        {
            return other != null &&
                   PolicyId == other.PolicyId &&
                   ManaCostMicros == other.ManaCostMicros &&
                   ManaReservationPoint == other.ManaReservationPoint &&
                   ManaCommitPoint == other.ManaCommitPoint &&
                   CooldownStartPoint == other.CooldownStartPoint &&
                   CooldownDurationMicros == other.CooldownDurationMicros &&
                   RefundCommittedOnCancellation ==
                       other.RefundCommittedOnCancellation &&
                   RefundCommittedOnInterruption ==
                       other.RefundCommittedOnInterruption &&
                   RefundCommittedOnFailure == other.RefundCommittedOnFailure &&
                   CooldownOnCancellation == other.CooldownOnCancellation &&
                   CooldownOnInterruption == other.CooldownOnInterruption &&
                   CooldownOnFailure == other.CooldownOnFailure &&
                   InterruptibleDuringWindup == other.InterruptibleDuringWindup &&
                   InterruptibleDuringResolution ==
                       other.InterruptibleDuringResolution;
        }

        private static int PolicyRank(CombatActionPolicyPoint point)
        {
            switch (point)
            {
                case CombatActionPolicyPoint.RequestAccepted:
                    return 1;
                case CombatActionPolicyPoint.ResourceReserved:
                    return 2;
                case CombatActionPolicyPoint.Committed:
                    return 3;
                case CombatActionPolicyPoint.Resolving:
                    return 4;
                case CombatActionPolicyPoint.Completed:
                    return 5;
                default:
                    return 0;
            }
        }
    }

    public sealed class CombatActionRequest
    {
        public CombatActionRequest(
            CombatStableId actionId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId behaviorOrSkillId,
            CombatContractVersion skillContentVersion,
            CombatStableId targetIntentId,
            CombatActionSource source,
            string expectedActorRevision,
            string expectedEncounterRevision,
            long requestedAtEncounterMicros)
        {
            ActionId = actionId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            BehaviorOrSkillId = behaviorOrSkillId;
            SkillContentVersion = skillContentVersion;
            TargetIntentId = targetIntentId;
            Source = source;
            ExpectedActorRevision = expectedActorRevision ?? string.Empty;
            ExpectedEncounterRevision = expectedEncounterRevision ?? string.Empty;
            RequestedAtEncounterMicros = requestedAtEncounterMicros;
        }

        public CombatStableId ActionId { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatStableId BehaviorOrSkillId { get; }
        public CombatContractVersion SkillContentVersion { get; }
        public CombatStableId TargetIntentId { get; }
        public CombatActionSource Source { get; }
        public string ExpectedActorRevision { get; }
        public string ExpectedEncounterRevision { get; }
        public long RequestedAtEncounterMicros { get; }

        internal bool PayloadEquals(CombatActionRequest other)
        {
            return other != null &&
                   ActionId == other.ActionId &&
                   EncounterSessionId == other.EncounterSessionId &&
                   EncounterAttemptId == other.EncounterAttemptId &&
                   ActorParticipantId == other.ActorParticipantId &&
                   BehaviorOrSkillId == other.BehaviorOrSkillId &&
                   SkillContentVersion.Equals(other.SkillContentVersion) &&
                   TargetIntentId == other.TargetIntentId &&
                   Source == other.Source &&
                   StringComparer.Ordinal.Equals(
                       ExpectedActorRevision,
                       other.ExpectedActorRevision) &&
                   StringComparer.Ordinal.Equals(
                       ExpectedEncounterRevision,
                       other.ExpectedEncounterRevision) &&
                   RequestedAtEncounterMicros == other.RequestedAtEncounterMicros;
        }
    }

    public sealed class CombatActionEligibilitySnapshot
    {
        public CombatActionEligibilitySnapshot(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            string actorRevision,
            string encounterRevision,
            CombatantLifeState actorLifeState,
            CombatantControlState actorControlState,
            bool encounterActive,
            bool skillAvailable,
            bool targetValid,
            bool targetInRange,
            bool cooldownActive,
            long availableManaMicros)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            ActorRevision = actorRevision ?? string.Empty;
            EncounterRevision = encounterRevision ?? string.Empty;
            ActorLifeState = actorLifeState;
            ActorControlState = actorControlState;
            EncounterActive = encounterActive;
            SkillAvailable = skillAvailable;
            TargetValid = targetValid;
            TargetInRange = targetInRange;
            CooldownActive = cooldownActive;
            AvailableManaMicros = availableManaMicros;
        }

        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public string ActorRevision { get; }
        public string EncounterRevision { get; }
        public CombatantLifeState ActorLifeState { get; }
        public CombatantControlState ActorControlState { get; }
        public bool EncounterActive { get; }
        public bool SkillAvailable { get; }
        public bool TargetValid { get; }
        public bool TargetInRange { get; }
        public bool CooldownActive { get; }
        public long AvailableManaMicros { get; }
    }

    public sealed class CombatActionReceipt
    {
        internal CombatActionReceipt(
            CombatActionReceiptKind kind,
            CombatStableId actionId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId transitionId,
            CombatActionState fromState,
            CombatActionState toState,
            string beforeActionRevision,
            string afterActionRevision,
            long manaAmountMicros,
            long cooldownDurationMicros,
            CombatActionTerminalReason terminalReason,
            CombatCooldownSnapshot cooldown = null)
        {
            Kind = kind;
            ActionId = actionId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            TransitionId = transitionId;
            FromState = fromState;
            ToState = toState;
            BeforeActionRevision = beforeActionRevision ?? string.Empty;
            AfterActionRevision = afterActionRevision ?? string.Empty;
            ManaAmountMicros = manaAmountMicros;
            CooldownDurationMicros = cooldownDurationMicros;
            TerminalReason = terminalReason;
            Cooldown = cooldown;
        }

        public CombatActionReceiptKind Kind { get; }
        public CombatStableId ActionId { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatStableId TransitionId { get; }
        public CombatActionState FromState { get; }
        public CombatActionState ToState { get; }
        public string BeforeActionRevision { get; }
        public string AfterActionRevision { get; }
        public string ActionRevision => AfterActionRevision;
        public long ManaAmountMicros { get; }
        public long CooldownDurationMicros { get; }
        public CombatActionTerminalReason TerminalReason { get; }
        public CombatCooldownSnapshot Cooldown { get; }
    }

    public sealed class CombatActionTransitionRequest
    {
        public CombatActionTransitionRequest(
            CombatStableId transitionId,
            CombatStableId actionId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatActionState targetState,
            string expectedActionRevision,
            CombatActionTerminalReason terminalReason,
            long availableManaMicros,
            long encounterTimeMicros)
        {
            TransitionId = transitionId;
            ActionId = actionId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            TargetState = targetState;
            ExpectedActionRevision = expectedActionRevision ?? string.Empty;
            TerminalReason = terminalReason;
            AvailableManaMicros = availableManaMicros;
            EncounterTimeMicros = encounterTimeMicros;
        }

        public CombatStableId TransitionId { get; }
        public CombatStableId ActionId { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatActionState TargetState { get; }
        public string ExpectedActionRevision { get; }
        public CombatActionTerminalReason TerminalReason { get; }
        public long AvailableManaMicros { get; }
        public long EncounterTimeMicros { get; }

        internal bool PayloadEquals(CombatActionTransitionRequest other)
        {
            return other != null &&
                   TransitionId == other.TransitionId &&
                   ActionId == other.ActionId &&
                   EncounterSessionId == other.EncounterSessionId &&
                   EncounterAttemptId == other.EncounterAttemptId &&
                   ActorParticipantId == other.ActorParticipantId &&
                   TargetState == other.TargetState &&
                   StringComparer.Ordinal.Equals(
                       ExpectedActionRevision,
                       other.ExpectedActionRevision) &&
                   TerminalReason == other.TerminalReason &&
                   AvailableManaMicros == other.AvailableManaMicros &&
                   EncounterTimeMicros == other.EncounterTimeMicros;
        }
    }

    public sealed class CombatActionTransitionRecord
    {
        internal CombatActionTransitionRecord(
            CombatActionTransitionRequest request,
            IList<CombatActionReceipt> receipts)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Receipts = Freeze(receipts);
        }

        public CombatActionTransitionRequest Request { get; }
        public IReadOnlyList<CombatActionReceipt> Receipts { get; }

        private static IReadOnlyList<CombatActionReceipt> Freeze(
            IList<CombatActionReceipt> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.AsReadOnly(new CombatActionReceipt[0]);
            }

            var copy = new CombatActionReceipt[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatActionSnapshot
    {
        public const int MaximumTransitionRecords = 32;

        private CombatActionSnapshot(
            CombatActionRequest request,
            CombatActionResourcePolicy policy,
            CombatActionState state,
            long revisionOrdinal,
            long lastEncounterTimeMicros,
            bool manaReserved,
            bool manaCommitted,
            CombatActionManaOutcome manaOutcome,
            CombatActionEffectOutcome effectOutcome,
            bool cooldownStarted,
            bool terminalReceiptEmitted,
            CombatActionTerminalReason terminalReason,
            IList<CombatActionReceipt> receipts,
            IList<CombatActionTransitionRecord> transitionRecords)
        {
            Request = request;
            Policy = policy;
            State = state;
            RevisionOrdinal = revisionOrdinal;
            Revision = FormatRevision(revisionOrdinal);
            LastEncounterTimeMicros = lastEncounterTimeMicros;
            ManaReserved = manaReserved;
            ManaCommitted = manaCommitted;
            ManaOutcome = manaOutcome;
            EffectOutcome = effectOutcome;
            CooldownStarted = cooldownStarted;
            TerminalReceiptEmitted = terminalReceiptEmitted;
            TerminalReason = terminalReason;
            Receipts = Freeze(receipts);
            TransitionRecords = Freeze(transitionRecords);
        }

        public CombatActionRequest Request { get; }
        public CombatActionResourcePolicy Policy { get; }
        public CombatActionState State { get; }
        public long RevisionOrdinal { get; }
        public string Revision { get; }
        public long LastEncounterTimeMicros { get; }
        public bool ManaReserved { get; }
        public bool ManaCommitted { get; }
        public CombatActionManaOutcome ManaOutcome { get; }
        public CombatActionEffectOutcome EffectOutcome { get; }
        public bool CooldownStarted { get; }
        public bool TerminalReceiptEmitted { get; }
        public CombatActionTerminalReason TerminalReason { get; }
        public IReadOnlyList<CombatActionReceipt> Receipts { get; }
        public IReadOnlyList<CombatActionTransitionRecord> TransitionRecords { get; }
        public bool IsTerminal => CombatActionPlanner.IsTerminal(State);

        internal static CombatActionSnapshot Create(
            CombatActionRequest request,
            CombatActionResourcePolicy policy,
            bool manaReserved,
            bool manaCommitted,
            CombatActionManaOutcome manaOutcome,
            bool cooldownStarted,
            IList<CombatActionReceipt> receipts)
        {
            return new CombatActionSnapshot(
                request,
                policy,
                CombatActionState.Requested,
                0L,
                request.RequestedAtEncounterMicros,
                manaReserved,
                manaCommitted,
                manaOutcome,
                CombatActionEffectOutcome.None,
                cooldownStarted,
                false,
                CombatActionTerminalReason.None,
                receipts,
                null);
        }

        internal CombatActionSnapshot With(
            CombatActionState state,
            long revisionOrdinal,
            long lastEncounterTimeMicros,
            bool manaReserved,
            bool manaCommitted,
            CombatActionManaOutcome manaOutcome,
            CombatActionEffectOutcome effectOutcome,
            bool cooldownStarted,
            bool terminalReceiptEmitted,
            CombatActionTerminalReason terminalReason,
            IList<CombatActionReceipt> receipts,
            IList<CombatActionTransitionRecord> transitionRecords)
        {
            return new CombatActionSnapshot(
                Request,
                Policy,
                state,
                revisionOrdinal,
                lastEncounterTimeMicros,
                manaReserved,
                manaCommitted,
                manaOutcome,
                effectOutcome,
                cooldownStarted,
                terminalReceiptEmitted,
                terminalReason,
                receipts,
                transitionRecords);
        }

        private static string FormatRevision(long revisionOrdinal)
        {
            return "action-r" + revisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);
        }

        private static IReadOnlyList<T> Freeze<T>(IList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.AsReadOnly(new T[0]);
            }

            var copy = new T[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatActionTerminalCorrelationReceipt
    {
        internal CombatActionTerminalCorrelationReceipt(
            CombatActionSnapshot action)
        {
            Request = action.Request;
            Policy = action.Policy;
            TerminalState = action.State;
            TerminalReason = action.TerminalReason;
            ActionRevision = action.Revision;
            ManaOutcome = action.ManaOutcome;
            EffectOutcome = action.EffectOutcome;
            CooldownStarted = action.CooldownStarted;
            TerminalReceipt = action.Receipts.Last(
                receipt => receipt.Kind == CombatActionReceiptKind.Terminal);
            var transitionCorrelations =
                new CombatActionTransitionRecord[action.TransitionRecords.Count];
            for (int index = 0; index < transitionCorrelations.Length; index++)
            {
                transitionCorrelations[index] = action.TransitionRecords[index];
            }

            TransitionCorrelations = Array.AsReadOnly(transitionCorrelations);
        }

        public CombatActionRequest Request { get; }
        public CombatActionResourcePolicy Policy { get; }
        public CombatActionState TerminalState { get; }
        public CombatActionTerminalReason TerminalReason { get; }
        public string ActionRevision { get; }
        public CombatActionManaOutcome ManaOutcome { get; }
        public CombatActionEffectOutcome EffectOutcome { get; }
        public bool CooldownStarted { get; }
        public CombatActionReceipt TerminalReceipt { get; }
        public IReadOnlyList<CombatActionTransitionRecord>
            TransitionCorrelations { get; }

        internal bool PayloadEquals(
            CombatActionRequest request,
            CombatActionResourcePolicy policy)
        {
            return Request.PayloadEquals(request) && Policy.PayloadEquals(policy);
        }
    }

    public sealed class CombatActionRejectedCorrelationReceipt
    {
        internal CombatActionRejectedCorrelationReceipt(
            CombatActionRequest request,
            CombatActionResourcePolicy policy,
            CombatActionPlanStatus status)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Policy = policy;
            Status = status;
        }

        public CombatActionRequest Request { get; }
        public CombatActionResourcePolicy Policy { get; }
        public CombatActionPlanStatus Status { get; }

        internal bool PayloadEquals(
            CombatActionRequest request,
            CombatActionResourcePolicy policy)
        {
            bool policyMatches = Policy == null
                ? policy == null
                : Policy.PayloadEquals(policy);
            return Request.PayloadEquals(request) && policyMatches;
        }
    }

    public sealed class CombatActionRegistrySnapshot
    {
        public const int MaximumActiveActions = 64;
        public const int MaximumTerminalCorrelations = 4096;
        public const int MaximumRejectedCorrelations = 4096;

        private CombatActionRegistrySnapshot(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            long revisionOrdinal,
            IList<CombatActionSnapshot> actions,
            IList<CombatActionTerminalCorrelationReceipt> terminalCorrelations,
            IList<CombatActionRejectedCorrelationReceipt> rejectedCorrelations)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            RevisionOrdinal = revisionOrdinal;
            Revision =
                "action-registry-r" +
                revisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);
            if (actions == null || actions.Count == 0)
            {
                Actions = Array.AsReadOnly(new CombatActionSnapshot[0]);
            }
            else
            {
                var copy = new CombatActionSnapshot[actions.Count];
                actions.CopyTo(copy, 0);
                Actions = Array.AsReadOnly(copy);
            }

            if (terminalCorrelations == null || terminalCorrelations.Count == 0)
            {
                TerminalCorrelations =
                    Array.AsReadOnly(new CombatActionTerminalCorrelationReceipt[0]);
            }
            else
            {
                var copy =
                    new CombatActionTerminalCorrelationReceipt[
                        terminalCorrelations.Count];
                terminalCorrelations.CopyTo(copy, 0);
                TerminalCorrelations = Array.AsReadOnly(copy);
            }

            if (rejectedCorrelations == null || rejectedCorrelations.Count == 0)
            {
                RejectedCorrelations =
                    Array.AsReadOnly(
                        new CombatActionRejectedCorrelationReceipt[0]);
            }
            else
            {
                var copy =
                    new CombatActionRejectedCorrelationReceipt[
                        rejectedCorrelations.Count];
                rejectedCorrelations.CopyTo(copy, 0);
                RejectedCorrelations = Array.AsReadOnly(copy);
            }
        }

        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public long RevisionOrdinal { get; }
        public string Revision { get; }
        public IReadOnlyList<CombatActionSnapshot> Actions { get; }
        public IReadOnlyList<CombatActionTerminalCorrelationReceipt>
            TerminalCorrelations { get; }
        public IReadOnlyList<CombatActionRejectedCorrelationReceipt>
            RejectedCorrelations { get; }

        public static bool TryCreate(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            out CombatActionRegistrySnapshot snapshot)
        {
            snapshot = null;
            if (encounterSessionId.IsDefault || encounterAttemptId.IsDefault)
            {
                return false;
            }

            snapshot = new CombatActionRegistrySnapshot(
                encounterSessionId,
                encounterAttemptId,
                0L,
                null,
                null,
                null);
            return true;
        }

        internal CombatActionRegistrySnapshot Add(CombatActionSnapshot action)
        {
            var copy = new List<CombatActionSnapshot>(Actions) { action };
            return new CombatActionRegistrySnapshot(
                EncounterSessionId,
                EncounterAttemptId,
                RevisionOrdinal + 1L,
                copy,
                new List<CombatActionTerminalCorrelationReceipt>(
                    TerminalCorrelations),
                new List<CombatActionRejectedCorrelationReceipt>(
                    RejectedCorrelations));
        }

        internal CombatActionRegistrySnapshot Replace(CombatActionSnapshot action)
        {
            var copy = new List<CombatActionSnapshot>(Actions);
            int index = copy.FindIndex(
                existing =>
                    existing.Request.ActionId == action.Request.ActionId);
            if (index < 0)
            {
                return this;
            }

            var terminalCorrelations =
                new List<CombatActionTerminalCorrelationReceipt>(
                    TerminalCorrelations);
            if (action.IsTerminal)
            {
                copy.RemoveAt(index);
                terminalCorrelations.Add(
                    new CombatActionTerminalCorrelationReceipt(action));
            }
            else
            {
                copy[index] = action;
            }

            return new CombatActionRegistrySnapshot(
                EncounterSessionId,
                EncounterAttemptId,
                RevisionOrdinal + 1L,
                copy,
                terminalCorrelations,
                new List<CombatActionRejectedCorrelationReceipt>(
                    RejectedCorrelations));
        }

        internal CombatActionRegistrySnapshot RecordRejected(
            CombatActionRejectedCorrelationReceipt rejectedCorrelation)
        {
            var rejectedCorrelations =
                new List<CombatActionRejectedCorrelationReceipt>(
                    RejectedCorrelations)
                {
                    rejectedCorrelation
                };
            return new CombatActionRegistrySnapshot(
                EncounterSessionId,
                EncounterAttemptId,
                RevisionOrdinal + 1L,
                new List<CombatActionSnapshot>(Actions),
                new List<CombatActionTerminalCorrelationReceipt>(
                    TerminalCorrelations),
                rejectedCorrelations);
        }
    }

    public sealed class CombatActionRequestPlanResult
    {
        private static readonly IReadOnlyList<CombatActionReceipt> EmptyReceipts =
            Array.AsReadOnly(new CombatActionReceipt[0]);

        internal CombatActionRequestPlanResult(
            CombatActionPlanStatus status,
            CombatActionRegistrySnapshot registry,
            CombatActionSnapshot action,
            CombatActionTerminalCorrelationReceipt terminalCorrelation,
            IList<CombatActionReceipt> receipts,
            IReadOnlyList<CombatActionReceipt> existingReceipts,
            CombatActionRejectedCorrelationReceipt rejectedCorrelation = null)
        {
            Status = status;
            Registry = registry;
            Action = action;
            TerminalCorrelation = terminalCorrelation;
            RejectedCorrelation = rejectedCorrelation;
            Receipts = Freeze(receipts);
            ExistingReceipts = Freeze(existingReceipts);
        }

        public CombatActionPlanStatus Status { get; }
        public CombatActionRegistrySnapshot Registry { get; }
        public CombatActionSnapshot Action { get; }
        public CombatActionTerminalCorrelationReceipt TerminalCorrelation { get; }
        public CombatActionRejectedCorrelationReceipt RejectedCorrelation { get; }
        public IReadOnlyList<CombatActionReceipt> Receipts { get; }
        public IReadOnlyList<CombatActionReceipt> ExistingReceipts { get; }

        private static IReadOnlyList<CombatActionReceipt> Freeze(
            IList<CombatActionReceipt> source)
        {
            if (source == null || source.Count == 0)
            {
                return EmptyReceipts;
            }

            var copy = new CombatActionReceipt[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<CombatActionReceipt> Freeze(
            IReadOnlyList<CombatActionReceipt> source)
        {
            if (source == null || source.Count == 0)
            {
                return EmptyReceipts;
            }

            var copy = new CombatActionReceipt[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatActionTransitionPlanResult
    {
        private static readonly IReadOnlyList<CombatActionReceipt> EmptyReceipts =
            Array.AsReadOnly(new CombatActionReceipt[0]);

        internal CombatActionTransitionPlanResult(
            CombatActionPlanStatus status,
            CombatActionSnapshot action,
            CombatActionRegistrySnapshot registry,
            IList<CombatActionReceipt> receipts,
            IReadOnlyList<CombatActionReceipt> existingReceipts)
        {
            Status = status;
            Action = action;
            Registry = registry;
            Receipts = Freeze(receipts);
            ExistingReceipts = Freeze(existingReceipts);
        }

        public CombatActionPlanStatus Status { get; }
        public CombatActionSnapshot Action { get; }
        public CombatActionRegistrySnapshot Registry { get; }
        public IReadOnlyList<CombatActionReceipt> Receipts { get; }
        public IReadOnlyList<CombatActionReceipt> ExistingReceipts { get; }

        private static IReadOnlyList<CombatActionReceipt> Freeze(
            IList<CombatActionReceipt> source)
        {
            if (source == null || source.Count == 0)
            {
                return EmptyReceipts;
            }

            var copy = new CombatActionReceipt[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<CombatActionReceipt> Freeze(
            IReadOnlyList<CombatActionReceipt> source)
        {
            if (source == null || source.Count == 0)
            {
                return EmptyReceipts;
            }

            var copy = new CombatActionReceipt[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatActionTransitionRule
    {
        internal CombatActionTransitionRule(
            CombatActionState from,
            CombatActionState to,
            bool allowed)
        {
            From = from;
            To = to;
            Allowed = allowed;
        }

        public CombatActionState From { get; }
        public CombatActionState To { get; }
        public bool Allowed { get; }
    }

    /// <summary>
    /// Pure action lifecycle planner. It emits resource/cooldown/effect intents;
    /// a later runtime adapter must apply them through the owning services.
    /// </summary>
    public static class CombatActionPlanner
    {
        private static readonly IReadOnlyList<CombatActionTransitionRule> Matrix =
            BuildTransitionMatrix();

        public static IReadOnlyList<CombatActionTransitionRule> TransitionMatrix =>
            Matrix;

        public static CombatActionRequestPlanResult RequestAction(
            CombatActionRegistrySnapshot registry,
            CombatActionRequest request,
            CombatActionResourcePolicy policy,
            CombatActionEligibilitySnapshot eligibility)
        {
            if (registry == null || request == null)
            {
                return RejectRequest(
                    CombatActionPlanStatus.InvalidRequest,
                    registry,
                    null);
            }

            CombatActionSnapshot existing = registry.Actions.FirstOrDefault(
                action => action.Request.ActionId == request.ActionId);
            if (existing != null)
            {
                bool exact =
                    existing.Request.PayloadEquals(request) &&
                    policy != null &&
                    existing.Policy.PayloadEquals(policy);
                return new CombatActionRequestPlanResult(
                    exact
                        ? CombatActionPlanStatus.DuplicateExact
                        : CombatActionPlanStatus.CorrelationConflict,
                    registry,
                    existing,
                    null,
                    null,
                    exact
                        ? existing.Receipts
                        : null);
            }

            CombatActionTerminalCorrelationReceipt terminal =
                registry.TerminalCorrelations.FirstOrDefault(
                    receipt => receipt.Request.ActionId == request.ActionId);
            if (terminal != null)
            {
                bool exact =
                    policy != null &&
                    terminal.PayloadEquals(request, policy);
                return new CombatActionRequestPlanResult(
                    exact
                        ? CombatActionPlanStatus.DuplicateExact
                        : CombatActionPlanStatus.CorrelationConflict,
                    registry,
                    null,
                    terminal,
                    null,
                    exact
                        ? new[] { terminal.TerminalReceipt }
                        : null);
            }

            CombatActionRejectedCorrelationReceipt rejected =
                registry.RejectedCorrelations.FirstOrDefault(
                    receipt => receipt.Request.ActionId == request.ActionId);
            if (rejected != null)
            {
                bool exact = rejected.PayloadEquals(request, policy);
                return new CombatActionRequestPlanResult(
                    exact
                        ? rejected.Status
                        : CombatActionPlanStatus.CorrelationConflict,
                    registry,
                    null,
                    null,
                    null,
                    null,
                    exact ? rejected : null);
            }

            if (request.ActionId.IsDefault ||
                policy == null ||
                eligibility == null)
            {
                return RecordRejectedRequest(
                    CombatActionPlanStatus.InvalidRequest,
                    registry,
                    request,
                    policy);
            }

            CombatActionPlanStatus validation = ValidateRequest(
                registry,
                request,
                policy,
                eligibility);
            if (validation != CombatActionPlanStatus.Applied)
            {
                return RecordRejectedRequest(
                    validation,
                    registry,
                    request,
                    policy);
            }

            if (registry.Actions.Count >=
                    CombatActionRegistrySnapshot.MaximumActiveActions ||
                registry.RejectedCorrelations.Count >=
                    CombatActionRegistrySnapshot.MaximumRejectedCorrelations ||
                (long)registry.TerminalCorrelations.Count +
                    registry.Actions.Count >=
                    CombatActionRegistrySnapshot.MaximumTerminalCorrelations ||
                registry.RevisionOrdinal == long.MaxValue)
            {
                return RecordRejectedRequest(
                    CombatActionPlanStatus.CapacityReached,
                    registry,
                    request,
                    policy);
            }

            var receipts = new List<CombatActionReceipt>
            {
                NewReceipt(
                    CombatActionReceiptKind.ActionRequested,
                    request,
                    default,
                    CombatActionState.Requested,
                    CombatActionState.Requested,
                    "action-r0000000000000000",
                    "action-r0000000000000000",
                    0L,
                    0L,
                    CombatActionTerminalReason.None)
            };

            bool reserved = false;
            bool committed = false;
            bool cooldownStarted = false;
            CombatActionManaOutcome manaOutcome = CombatActionManaOutcome.None;
            if (policy.ManaReservationPoint ==
                CombatActionPolicyPoint.RequestAccepted)
            {
                reserved = true;
                manaOutcome = CombatActionManaOutcome.Reserved;
                receipts.Add(NewReceipt(
                    CombatActionReceiptKind.ManaReserved,
                    request,
                    default,
                    CombatActionState.Requested,
                    CombatActionState.Requested,
                    "action-r0000000000000000",
                    "action-r0000000000000000",
                    policy.ManaCostMicros,
                    0L,
                    CombatActionTerminalReason.None));
            }

            if (policy.ManaCommitPoint == CombatActionPolicyPoint.RequestAccepted)
            {
                if (!reserved)
                {
                    return RecordRejectedRequest(
                        CombatActionPlanStatus.PolicyViolation,
                        registry,
                        request,
                        policy);
                }

                reserved = false;
                committed = true;
                manaOutcome = CombatActionManaOutcome.Committed;
                receipts.Add(NewReceipt(
                    CombatActionReceiptKind.ManaCommitted,
                    request,
                    default,
                    CombatActionState.Requested,
                    CombatActionState.Requested,
                    "action-r0000000000000000",
                    "action-r0000000000000000",
                    policy.ManaCostMicros,
                    0L,
                    CombatActionTerminalReason.None));
            }

            if (policy.CooldownStartPoint ==
                CombatActionPolicyPoint.RequestAccepted)
            {
                CombatCooldownSnapshot cooldown =
                    CombatCooldownPlanner.CreatePlannedSnapshot(
                        request.EncounterSessionId,
                        request.EncounterAttemptId,
                        request.ActorParticipantId,
                        request.BehaviorOrSkillId,
                        request.SkillContentVersion,
                        request.ActionId,
                        request.RequestedAtEncounterMicros,
                        policy.CooldownDurationMicros,
                        "action-r0000000000000000");
                if (cooldown == null)
                {
                    return RecordRejectedRequest(
                        CombatActionPlanStatus.PolicyViolation,
                        registry,
                        request,
                        policy);
                }

                cooldownStarted = true;
                receipts.Add(NewReceipt(
                    CombatActionReceiptKind.CooldownStarted,
                    request,
                    default,
                    CombatActionState.Requested,
                    CombatActionState.Requested,
                    "action-r0000000000000000",
                    "action-r0000000000000000",
                    0L,
                    policy.CooldownDurationMicros,
                    CombatActionTerminalReason.None,
                    cooldown));
            }

            CombatActionSnapshot createdAction = CombatActionSnapshot.Create(
                request,
                policy,
                reserved,
                committed,
                manaOutcome,
                cooldownStarted,
                receipts);
            CombatActionRegistrySnapshot next = registry.Add(createdAction);
            return new CombatActionRequestPlanResult(
                CombatActionPlanStatus.Applied,
                next,
                createdAction,
                null,
                receipts,
                null);
        }

        public static CombatActionTransitionPlanResult PlanTransition(
            CombatActionRegistrySnapshot registry,
            CombatActionTransitionRequest request)
        {
            if (registry == null || request == null)
            {
                return new CombatActionTransitionPlanResult(
                    CombatActionPlanStatus.InvalidRequest,
                    null,
                    registry,
                    null,
                    null);
            }

            if (registry.EncounterSessionId != request.EncounterSessionId ||
                registry.EncounterAttemptId != request.EncounterAttemptId)
            {
                return new CombatActionTransitionPlanResult(
                    CombatActionPlanStatus.WrongEncounter,
                    null,
                    registry,
                    null,
                    null);
            }

            CombatActionSnapshot action = registry.Actions.FirstOrDefault(
                candidate => candidate.Request.ActionId == request.ActionId);
            if (action == null)
            {
                CombatActionTerminalCorrelationReceipt terminal =
                    registry.TerminalCorrelations.FirstOrDefault(
                        receipt =>
                            receipt.Request.ActionId == request.ActionId);
                if (terminal != null)
                {
                    CombatActionTransitionRecord transition =
                        terminal.TransitionCorrelations.FirstOrDefault(
                            record =>
                                record.Request.TransitionId ==
                                    request.TransitionId);
                    if (transition != null)
                    {
                        bool exact = transition.Request.PayloadEquals(request);
                        return new CombatActionTransitionPlanResult(
                            exact
                                ? CombatActionPlanStatus.DuplicateExact
                                : CombatActionPlanStatus.CorrelationConflict,
                            null,
                            registry,
                            null,
                            exact
                                ? transition.Receipts
                                : null);
                    }

                    return new CombatActionTransitionPlanResult(
                        CombatActionPlanStatus.TerminalState,
                        null,
                        registry,
                        null,
                        null);
                }

                return new CombatActionTransitionPlanResult(
                    CombatActionPlanStatus.InvalidRequest,
                    null,
                    registry,
                    null,
                    null);
            }

            CombatActionTransitionPlanResult result =
                PlanTransition(action, request);
            CombatActionRegistrySnapshot nextRegistry = registry;
            if (result.Status == CombatActionPlanStatus.Applied)
            {
                if (result.Action.IsTerminal &&
                    registry.TerminalCorrelations.Count >=
                        CombatActionRegistrySnapshot.MaximumTerminalCorrelations)
                {
                    return new CombatActionTransitionPlanResult(
                        CombatActionPlanStatus.CapacityReached,
                        action,
                        registry,
                        null,
                        null);
                }

                if (registry.RevisionOrdinal == long.MaxValue)
                {
                    return new CombatActionTransitionPlanResult(
                        CombatActionPlanStatus.CapacityReached,
                        action,
                        registry,
                        null,
                        null);
                }

                nextRegistry = registry.Replace(result.Action);
            }

            return new CombatActionTransitionPlanResult(
                result.Status,
                result.Action,
                nextRegistry,
                result.Receipts.ToList(),
                result.ExistingReceipts);
        }

        public static CombatActionTransitionPlanResult PlanTransition(
            CombatActionSnapshot action,
            CombatActionTransitionRequest request)
        {
            if (action == null || request == null)
            {
                return RejectTransition(
                    CombatActionPlanStatus.InvalidRequest,
                    action);
            }

            CombatActionTransitionRecord existing = action.TransitionRecords
                .FirstOrDefault(
                    record => record.Request.TransitionId == request.TransitionId);
            if (existing != null)
            {
                return new CombatActionTransitionPlanResult(
                    existing.Request.PayloadEquals(request)
                        ? CombatActionPlanStatus.DuplicateExact
                        : CombatActionPlanStatus.CorrelationConflict,
                    action,
                    null,
                    null,
                    existing.Request.PayloadEquals(request)
                        ? existing.Receipts
                        : null);
            }

            CombatActionPlanStatus context = ValidateTransitionContext(action, request);
            if (context != CombatActionPlanStatus.Applied)
            {
                return RejectTransition(context, action);
            }

            if (action.IsTerminal)
            {
                return RejectTransition(CombatActionPlanStatus.TerminalState, action);
            }

            if (!IsTransitionAllowed(action.State, request.TargetState))
            {
                return RejectTransition(
                    CombatActionPlanStatus.ProhibitedTransition,
                    action);
            }

            CombatActionPlanStatus reasonStatus =
                ValidateTerminalReason(
                    request.TargetState,
                    request.TerminalReason);
            if (reasonStatus != CombatActionPlanStatus.Applied)
            {
                return RejectTransition(reasonStatus, action);
            }

            if (request.TargetState == CombatActionState.InterruptedAfterCommit)
            {
                if (!action.ManaCommitted)
                {
                    return RejectTransition(
                        CombatActionPlanStatus.PolicyViolation,
                        action);
                }

                if (!IsInterruptionAllowed(
                        action,
                        request.TerminalReason))
                {
                    return RejectTransition(
                        CombatActionPlanStatus.PolicyViolation,
                        action);
                }
            }

            if ((request.TargetState ==
                     CombatActionState.CancelledBeforeCommit ||
                 request.TargetState == CombatActionState.Rejected) &&
                action.ManaCommitted)
            {
                return RejectTransition(
                    CombatActionPlanStatus.PolicyViolation,
                    action);
            }

            if (request.TargetState == CombatActionState.CancelledBeforeCommit &&
                action.State == CombatActionState.Windup &&
                request.TerminalReason ==
                    CombatActionTerminalReason.ManualCancellation &&
                !action.Policy.InterruptibleDuringWindup)
            {
                return RejectTransition(
                    CombatActionPlanStatus.PolicyViolation,
                    action);
            }

            int reservedTerminalRecord =
                IsTerminal(request.TargetState) ? 0 : 1;
            if (action.TransitionRecords.Count >=
                    CombatActionSnapshot.MaximumTransitionRecords -
                    reservedTerminalRecord ||
                action.RevisionOrdinal == long.MaxValue)
            {
                return RejectTransition(
                    CombatActionPlanStatus.CapacityReached,
                    action);
            }

            return ApplyTransition(action, request);
        }

        public static bool IsTransitionAllowed(
            CombatActionState from,
            CombatActionState to)
        {
            if (!Enum.IsDefined(typeof(CombatActionState), from) ||
                !Enum.IsDefined(typeof(CombatActionState), to))
            {
                return false;
            }

            switch (from)
            {
                case CombatActionState.Requested:
                    return to == CombatActionState.Rejected ||
                           to == CombatActionState.Validated ||
                           to == CombatActionState.CancelledBeforeCommit ||
                           to == CombatActionState.InterruptedAfterCommit ||
                           to == CombatActionState.Failed ||
                           to == CombatActionState.Disposed;
                case CombatActionState.Validated:
                    return to == CombatActionState.ResourceReserved ||
                           to == CombatActionState.Windup ||
                           to == CombatActionState.CancelledBeforeCommit ||
                           to == CombatActionState.InterruptedAfterCommit ||
                           to == CombatActionState.Failed ||
                           to == CombatActionState.Disposed;
                case CombatActionState.ResourceReserved:
                    return to == CombatActionState.Windup ||
                           to == CombatActionState.CancelledBeforeCommit ||
                           to == CombatActionState.InterruptedAfterCommit ||
                           to == CombatActionState.Failed ||
                           to == CombatActionState.Disposed;
                case CombatActionState.Windup:
                    return to == CombatActionState.Committed ||
                           to == CombatActionState.CancelledBeforeCommit ||
                           to == CombatActionState.InterruptedAfterCommit ||
                           to == CombatActionState.Failed ||
                           to == CombatActionState.Disposed;
                case CombatActionState.Committed:
                    return to == CombatActionState.Resolving ||
                           to == CombatActionState.InterruptedAfterCommit ||
                           to == CombatActionState.Failed ||
                           to == CombatActionState.Disposed;
                case CombatActionState.Resolving:
                    return to == CombatActionState.Completed ||
                           to == CombatActionState.InterruptedAfterCommit ||
                           to == CombatActionState.Failed ||
                           to == CombatActionState.Disposed;
                default:
                    return false;
            }
        }

        public static bool IsTerminal(CombatActionState state)
        {
            return state == CombatActionState.Rejected ||
                   state == CombatActionState.Completed ||
                   state == CombatActionState.CancelledBeforeCommit ||
                   state == CombatActionState.InterruptedAfterCommit ||
                   state == CombatActionState.Failed ||
                   state == CombatActionState.Disposed;
        }

        public static bool IsSourceAllowedForControl(
            CombatActionSource source,
            CombatantControlState controlState)
        {
            switch (source)
            {
                case CombatActionSource.ManualInput:
                    return controlState == CombatantControlState.Manual;
                case CombatActionSource.AssistAI:
                    return controlState == CombatantControlState.Assist;
                case CombatActionSource.FullAutoAI:
                    return controlState == CombatantControlState.Auto;
                case CombatActionSource.EncounterScript:
                    return controlState == CombatantControlState.EncounterLocked;
                default:
                    return false;
            }
        }

        private static CombatActionTransitionPlanResult ApplyTransition(
            CombatActionSnapshot action,
            CombatActionTransitionRequest request)
        {
            bool reserved = action.ManaReserved;
            bool committed = action.ManaCommitted;
            CombatActionManaOutcome manaOutcome = action.ManaOutcome;
            CombatActionEffectOutcome effectOutcome = action.EffectOutcome;
            bool cooldownStarted = action.CooldownStarted;
            bool terminalReceiptEmitted = action.TerminalReceiptEmitted;
            long nextRevisionOrdinal = action.RevisionOrdinal + 1L;
            string nextRevision =
                "action-r" +
                nextRevisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);

            var transitionReceipts = new List<CombatActionReceipt>
            {
                NewReceipt(
                    CombatActionReceiptKind.ActionStateChanged,
                    action.Request,
                    request.TransitionId,
                    action.State,
                    request.TargetState,
                    action.Revision,
                    nextRevision,
                    0L,
                    0L,
                    request.TerminalReason)
            };

            if (action.Policy.ManaReservationPoint ==
                    PointForState(request.TargetState) &&
                action.Policy.ManaCostMicros > 0L &&
                !reserved &&
                !committed)
            {
                if (request.AvailableManaMicros < action.Policy.ManaCostMicros)
                {
                    return RejectTransition(
                        CombatActionPlanStatus.InsufficientResource,
                        action);
                }

                reserved = true;
                manaOutcome = CombatActionManaOutcome.Reserved;
                transitionReceipts.Add(NewReceipt(
                    CombatActionReceiptKind.ManaReserved,
                    action.Request,
                    request.TransitionId,
                    action.State,
                    request.TargetState,
                    action.Revision,
                    nextRevision,
                    action.Policy.ManaCostMicros,
                    0L,
                    CombatActionTerminalReason.None));
            }

            if (request.TargetState == CombatActionState.Windup &&
                action.Policy.ManaCostMicros > 0L &&
                !reserved &&
                !committed)
            {
                return RejectTransition(
                    CombatActionPlanStatus.PolicyViolation,
                    action);
            }

            if (action.Policy.ManaCommitPoint ==
                    PointForState(request.TargetState) &&
                action.Policy.ManaCostMicros > 0L &&
                !committed)
            {
                if (!reserved)
                {
                    return RejectTransition(
                        CombatActionPlanStatus.PolicyViolation,
                        action);
                }

                reserved = false;
                committed = true;
                manaOutcome = CombatActionManaOutcome.Committed;
                transitionReceipts.Add(NewReceipt(
                    CombatActionReceiptKind.ManaCommitted,
                    action.Request,
                    request.TransitionId,
                    action.State,
                    request.TargetState,
                    action.Revision,
                    nextRevision,
                    action.Policy.ManaCostMicros,
                    0L,
                    CombatActionTerminalReason.None));
            }

            if (request.TargetState == CombatActionState.Resolving &&
                action.Policy.ManaCostMicros > 0L &&
                !committed)
            {
                return RejectTransition(
                    CombatActionPlanStatus.PolicyViolation,
                    action);
            }

            if (request.TargetState != CombatActionState.Completed &&
                !cooldownStarted &&
                action.Policy.CooldownStartPoint !=
                    CombatActionPolicyPoint.None &&
                action.Policy.CooldownStartPoint ==
                    PointForState(request.TargetState))
            {
                CombatCooldownSnapshot cooldown =
                    CreateActionCooldown(
                        action,
                        request.EncounterTimeMicros,
                        nextRevision);
                if (cooldown == null)
                {
                    return RejectTransition(
                        CombatActionPlanStatus.PolicyViolation,
                        action);
                }

                cooldownStarted = true;
                transitionReceipts.Add(NewReceipt(
                    CombatActionReceiptKind.CooldownStarted,
                    action.Request,
                    request.TransitionId,
                    action.State,
                    request.TargetState,
                    action.Revision,
                    nextRevision,
                    0L,
                    action.Policy.CooldownDurationMicros,
                    CombatActionTerminalReason.None,
                    cooldown));
            }

            if (request.TargetState == CombatActionState.Completed)
            {
                effectOutcome = CombatActionEffectOutcome.Applied;
                transitionReceipts.Add(NewReceipt(
                    CombatActionReceiptKind.EffectApplied,
                    action.Request,
                    request.TransitionId,
                    action.State,
                    request.TargetState,
                    action.Revision,
                    nextRevision,
                    0L,
                    0L,
                    CombatActionTerminalReason.Completed));

                if (!cooldownStarted &&
                    action.Policy.CooldownStartPoint ==
                        CombatActionPolicyPoint.Completed)
                {
                    CombatCooldownSnapshot cooldown =
                        CreateActionCooldown(
                            action,
                            request.EncounterTimeMicros,
                            nextRevision);
                    if (cooldown == null)
                    {
                        return RejectTransition(
                            CombatActionPlanStatus.PolicyViolation,
                            action);
                    }

                    cooldownStarted = true;
                    transitionReceipts.Add(NewReceipt(
                        CombatActionReceiptKind.CooldownStarted,
                        action.Request,
                        request.TransitionId,
                        action.State,
                        request.TargetState,
                        action.Revision,
                        nextRevision,
                        0L,
                        action.Policy.CooldownDurationMicros,
                        CombatActionTerminalReason.None,
                        cooldown));
                }
            }
            else if (request.TargetState == CombatActionState.Failed)
            {
                effectOutcome = CombatActionEffectOutcome.Failed;
            }

            if (IsTerminal(request.TargetState))
            {
                if (request.TargetState == CombatActionState.Rejected && committed)
                {
                    return RejectTransition(
                        CombatActionPlanStatus.PolicyViolation,
                        action);
                }

                if (reserved)
                {
                    reserved = false;
                    manaOutcome = CombatActionManaOutcome.Released;
                    transitionReceipts.Add(NewReceipt(
                        CombatActionReceiptKind.ManaReleased,
                        action.Request,
                        request.TransitionId,
                        action.State,
                        request.TargetState,
                        action.Revision,
                        nextRevision,
                        action.Policy.ManaCostMicros,
                        0L,
                        request.TerminalReason));
                }
                else if (committed &&
                         ShouldRefundCommitted(
                             action.Policy,
                             request.TargetState,
                             request.TerminalReason))
                {
                    manaOutcome = CombatActionManaOutcome.Refunded;
                    transitionReceipts.Add(NewReceipt(
                        CombatActionReceiptKind.ManaRefunded,
                        action.Request,
                        request.TransitionId,
                        action.State,
                        request.TargetState,
                        action.Revision,
                        nextRevision,
                        action.Policy.ManaCostMicros,
                        0L,
                        request.TerminalReason));
                }

                if (!cooldownStarted &&
                    ShouldStartCooldownAtTerminal(
                        action.Policy,
                        request.TargetState,
                        request.TerminalReason))
                {
                    CombatCooldownSnapshot cooldown =
                        CreateActionCooldown(
                            action,
                            request.EncounterTimeMicros,
                            nextRevision);
                    if (cooldown == null)
                    {
                        return RejectTransition(
                            CombatActionPlanStatus.PolicyViolation,
                            action);
                    }

                    cooldownStarted = true;
                    transitionReceipts.Add(NewReceipt(
                        CombatActionReceiptKind.CooldownStarted,
                        action.Request,
                        request.TransitionId,
                        action.State,
                        request.TargetState,
                        action.Revision,
                        nextRevision,
                        0L,
                        action.Policy.CooldownDurationMicros,
                        request.TerminalReason,
                        cooldown));
                }

                if (terminalReceiptEmitted)
                {
                    return RejectTransition(
                        CombatActionPlanStatus.PolicyViolation,
                        action);
                }

                terminalReceiptEmitted = true;
                transitionReceipts.Add(NewReceipt(
                    CombatActionReceiptKind.Terminal,
                    action.Request,
                    request.TransitionId,
                    action.State,
                    request.TargetState,
                    action.Revision,
                    nextRevision,
                    0L,
                    0L,
                    request.TerminalReason));
            }

            var allReceipts = new List<CombatActionReceipt>(action.Receipts);
            allReceipts.AddRange(transitionReceipts);
            var transitionRecords =
                new List<CombatActionTransitionRecord>(action.TransitionRecords)
                {
                    new CombatActionTransitionRecord(request, transitionReceipts)
                };
            CombatActionSnapshot next = action.With(
                request.TargetState,
                nextRevisionOrdinal,
                request.EncounterTimeMicros,
                reserved,
                committed,
                manaOutcome,
                effectOutcome,
                cooldownStarted,
                terminalReceiptEmitted,
                IsTerminal(request.TargetState)
                    ? request.TerminalReason
                    : CombatActionTerminalReason.None,
                allReceipts,
                transitionRecords);
            return new CombatActionTransitionPlanResult(
                CombatActionPlanStatus.Applied,
                next,
                null,
                transitionReceipts,
                null);
        }

        private static CombatActionPlanStatus ValidateRequest(
            CombatActionRegistrySnapshot registry,
            CombatActionRequest request,
            CombatActionResourcePolicy policy,
            CombatActionEligibilitySnapshot eligibility)
        {
            if (request.ActionId.IsDefault ||
                request.EncounterSessionId.IsDefault ||
                request.EncounterAttemptId.IsDefault ||
                request.ActorParticipantId.IsDefault ||
                request.BehaviorOrSkillId.IsDefault ||
                request.SkillContentVersion.IsDefault ||
                request.TargetIntentId.IsDefault ||
                !Enum.IsDefined(typeof(CombatActionSource), request.Source) ||
                !CombatPrimitiveValidation.IsStableId(request.ExpectedActorRevision) ||
                !CombatPrimitiveValidation.IsStableId(
                    request.ExpectedEncounterRevision) ||
                request.RequestedAtEncounterMicros < 0L ||
                request.RequestedAtEncounterMicros >
                    CombatTechnicalLimits.DurationMaximumMicros ||
                eligibility.AvailableManaMicros < 0L ||
                eligibility.AvailableManaMicros >
                    CombatTechnicalLimits.HealthManaDamageHealingAttackPowerMaximumMicros)
            {
                return CombatActionPlanStatus.InvalidRequest;
            }

            if (registry.EncounterSessionId != request.EncounterSessionId ||
                registry.EncounterAttemptId != request.EncounterAttemptId ||
                eligibility.EncounterSessionId != request.EncounterSessionId ||
                eligibility.EncounterAttemptId != request.EncounterAttemptId)
            {
                return CombatActionPlanStatus.WrongEncounter;
            }

            if (eligibility.ActorParticipantId != request.ActorParticipantId)
            {
                return CombatActionPlanStatus.WrongActor;
            }

            if (!StringComparer.Ordinal.Equals(
                    request.ExpectedActorRevision,
                    eligibility.ActorRevision) ||
                !StringComparer.Ordinal.Equals(
                    request.ExpectedEncounterRevision,
                    eligibility.EncounterRevision))
            {
                return CombatActionPlanStatus.StaleRevision;
            }

            if (eligibility.ActorLifeState == CombatantLifeState.Defeated)
            {
                return CombatActionPlanStatus.ActorDefeated;
            }

            if (eligibility.ActorLifeState != CombatantLifeState.Alive)
            {
                return CombatActionPlanStatus.ActorUnavailable;
            }

            if (!eligibility.EncounterActive)
            {
                return CombatActionPlanStatus.EncounterNotActive;
            }

            if (!IsSourceAllowedForControl(
                    request.Source,
                    eligibility.ActorControlState))
            {
                return CombatActionPlanStatus.ControlLocked;
            }

            if (!eligibility.SkillAvailable)
            {
                return CombatActionPlanStatus.SkillUnavailable;
            }

            if (!eligibility.TargetValid)
            {
                return CombatActionPlanStatus.TargetInvalid;
            }

            if (!eligibility.TargetInRange)
            {
                return CombatActionPlanStatus.OutOfRange;
            }

            if (eligibility.CooldownActive)
            {
                return CombatActionPlanStatus.CooldownActive;
            }

            if (policy.ManaReservationPoint ==
                    CombatActionPolicyPoint.RequestAccepted &&
                eligibility.AvailableManaMicros < policy.ManaCostMicros)
            {
                return CombatActionPlanStatus.InsufficientResource;
            }

            return CombatActionPlanStatus.Applied;
        }

        private static CombatActionPlanStatus ValidateTransitionContext(
            CombatActionSnapshot action,
            CombatActionTransitionRequest request)
        {
            if (request.TransitionId.IsDefault ||
                request.ActionId.IsDefault ||
                request.EncounterSessionId.IsDefault ||
                request.EncounterAttemptId.IsDefault ||
                request.ActorParticipantId.IsDefault ||
                !Enum.IsDefined(typeof(CombatActionState), request.TargetState) ||
                !Enum.IsDefined(
                    typeof(CombatActionTerminalReason),
                    request.TerminalReason) ||
                !CombatPrimitiveValidation.IsStableId(
                    request.ExpectedActionRevision) ||
                request.AvailableManaMicros < 0L ||
                request.AvailableManaMicros >
                    CombatTechnicalLimits.HealthManaDamageHealingAttackPowerMaximumMicros ||
                request.EncounterTimeMicros < action.LastEncounterTimeMicros ||
                request.EncounterTimeMicros >
                    CombatTechnicalLimits.DurationMaximumMicros)
            {
                return CombatActionPlanStatus.InvalidRequest;
            }

            if (action.Request.ActionId != request.ActionId)
            {
                return CombatActionPlanStatus.CorrelationConflict;
            }

            if (action.Request.EncounterSessionId != request.EncounterSessionId ||
                action.Request.EncounterAttemptId != request.EncounterAttemptId)
            {
                return CombatActionPlanStatus.WrongEncounter;
            }

            if (action.Request.ActorParticipantId != request.ActorParticipantId)
            {
                return CombatActionPlanStatus.WrongActor;
            }

            if (!StringComparer.Ordinal.Equals(
                    action.Revision,
                    request.ExpectedActionRevision))
            {
                return CombatActionPlanStatus.StaleRevision;
            }

            return CombatActionPlanStatus.Applied;
        }

        private static CombatActionPlanStatus ValidateTerminalReason(
            CombatActionState target,
            CombatActionTerminalReason reason)
        {
            if (!IsTerminal(target))
            {
                return reason == CombatActionTerminalReason.None
                    ? CombatActionPlanStatus.Applied
                    : CombatActionPlanStatus.InvalidRequest;
            }

            switch (target)
            {
                case CombatActionState.Completed:
                    return reason == CombatActionTerminalReason.Completed
                        ? CombatActionPlanStatus.Applied
                        : CombatActionPlanStatus.InvalidRequest;
                case CombatActionState.Rejected:
                    return reason ==
                           CombatActionTerminalReason.ValidationRejected
                        ? CombatActionPlanStatus.Applied
                        : CombatActionPlanStatus.InvalidRequest;
                case CombatActionState.CancelledBeforeCommit:
                    return (reason ==
                                CombatActionTerminalReason.ManualCancellation ||
                            reason == CombatActionTerminalReason.ActorDefeated ||
                            reason ==
                                CombatActionTerminalReason.EncounterTerminated)
                        ? CombatActionPlanStatus.Applied
                        : CombatActionPlanStatus.InvalidRequest;
                case CombatActionState.InterruptedAfterCommit:
                    return (reason == CombatActionTerminalReason.Interrupted ||
                            reason ==
                                CombatActionTerminalReason.ManualCancellation ||
                            reason == CombatActionTerminalReason.ActorDefeated ||
                            reason ==
                                CombatActionTerminalReason.EncounterTerminated)
                        ? CombatActionPlanStatus.Applied
                        : CombatActionPlanStatus.InvalidRequest;
                case CombatActionState.Failed:
                    return reason == CombatActionTerminalReason.EffectFailed
                        ? CombatActionPlanStatus.Applied
                        : CombatActionPlanStatus.InvalidRequest;
                case CombatActionState.Disposed:
                    return reason == CombatActionTerminalReason.SceneDisposed ||
                           reason ==
                               CombatActionTerminalReason.ComponentDisabled
                        ? CombatActionPlanStatus.Applied
                        : CombatActionPlanStatus.InvalidRequest;
                default:
                    return CombatActionPlanStatus.InvalidRequest;
            }
        }

        private static CombatActionPolicyPoint PointForState(
            CombatActionState state)
        {
            switch (state)
            {
                case CombatActionState.ResourceReserved:
                    return CombatActionPolicyPoint.ResourceReserved;
                case CombatActionState.Committed:
                    return CombatActionPolicyPoint.Committed;
                case CombatActionState.Resolving:
                    return CombatActionPolicyPoint.Resolving;
                case CombatActionState.Completed:
                    return CombatActionPolicyPoint.Completed;
                default:
                    return CombatActionPolicyPoint.None;
            }
        }

        private static bool ShouldRefundCommitted(
            CombatActionResourcePolicy policy,
            CombatActionState target,
            CombatActionTerminalReason reason)
        {
            if (target == CombatActionState.Completed ||
                target == CombatActionState.Rejected)
            {
                return false;
            }

            switch (reason)
            {
                case CombatActionTerminalReason.ManualCancellation:
                    return policy.RefundCommittedOnCancellation;
                case CombatActionTerminalReason.Interrupted:
                case CombatActionTerminalReason.ActorDefeated:
                case CombatActionTerminalReason.EncounterTerminated:
                    return policy.RefundCommittedOnInterruption;
                case CombatActionTerminalReason.EffectFailed:
                case CombatActionTerminalReason.SceneDisposed:
                case CombatActionTerminalReason.ComponentDisabled:
                    return policy.RefundCommittedOnFailure;
                default:
                    return false;
            }
        }

        private static bool ShouldStartCooldownAtTerminal(
            CombatActionResourcePolicy policy,
            CombatActionState target,
            CombatActionTerminalReason reason)
        {
            if (policy.CooldownStartPoint == CombatActionPolicyPoint.None)
            {
                return false;
            }

            if (target == CombatActionState.Completed ||
                target == CombatActionState.Rejected)
            {
                return false;
            }

            switch (reason)
            {
                case CombatActionTerminalReason.ManualCancellation:
                    return policy.CooldownOnCancellation;
                case CombatActionTerminalReason.Interrupted:
                case CombatActionTerminalReason.ActorDefeated:
                case CombatActionTerminalReason.EncounterTerminated:
                    return policy.CooldownOnInterruption;
                case CombatActionTerminalReason.EffectFailed:
                case CombatActionTerminalReason.SceneDisposed:
                case CombatActionTerminalReason.ComponentDisabled:
                    return policy.CooldownOnFailure;
                default:
                    return false;
            }
        }

        private static bool IsInterruptionAllowed(
            CombatActionSnapshot action,
            CombatActionTerminalReason reason)
        {
            bool forced =
                reason == CombatActionTerminalReason.ActorDefeated ||
                reason == CombatActionTerminalReason.EncounterTerminated;
            if (forced)
            {
                return true;
            }

            if (action.State == CombatActionState.Windup)
            {
                return action.Policy.InterruptibleDuringWindup;
            }

            if (action.State == CombatActionState.Resolving)
            {
                return action.Policy.InterruptibleDuringResolution;
            }

            return true;
        }

        private static IReadOnlyList<CombatActionTransitionRule>
            BuildTransitionMatrix()
        {
            CombatActionState[] states =
                (CombatActionState[])Enum.GetValues(typeof(CombatActionState));
            var rules = new List<CombatActionTransitionRule>(
                states.Length * states.Length);
            for (int fromIndex = 0; fromIndex < states.Length; fromIndex++)
            {
                for (int toIndex = 0; toIndex < states.Length; toIndex++)
                {
                    rules.Add(new CombatActionTransitionRule(
                        states[fromIndex],
                        states[toIndex],
                        IsTransitionAllowed(states[fromIndex], states[toIndex])));
                }
            }

            return Array.AsReadOnly(rules.ToArray());
        }

        private static CombatCooldownSnapshot CreateActionCooldown(
            CombatActionSnapshot action,
            long startEncounterTimeMicros,
            string stateRevision)
        {
            return CombatCooldownPlanner.CreatePlannedSnapshot(
                action.Request.EncounterSessionId,
                action.Request.EncounterAttemptId,
                action.Request.ActorParticipantId,
                action.Request.BehaviorOrSkillId,
                action.Request.SkillContentVersion,
                action.Request.ActionId,
                startEncounterTimeMicros,
                action.Policy.CooldownDurationMicros,
                stateRevision);
        }

        private static CombatActionReceipt NewReceipt(
            CombatActionReceiptKind kind,
            CombatActionRequest actionRequest,
            CombatStableId transitionId,
            CombatActionState fromState,
            CombatActionState toState,
            string beforeActionRevision,
            string afterActionRevision,
            long manaAmountMicros,
            long cooldownDurationMicros,
            CombatActionTerminalReason terminalReason,
            CombatCooldownSnapshot cooldown = null)
        {
            return new CombatActionReceipt(
                kind,
                actionRequest.ActionId,
                actionRequest.EncounterSessionId,
                actionRequest.EncounterAttemptId,
                actionRequest.ActorParticipantId,
                transitionId,
                fromState,
                toState,
                beforeActionRevision,
                afterActionRevision,
                manaAmountMicros,
                cooldownDurationMicros,
                terminalReason,
                cooldown);
        }

        private static CombatActionRequestPlanResult RejectRequest(
            CombatActionPlanStatus status,
            CombatActionRegistrySnapshot registry,
            CombatActionSnapshot action)
        {
            return new CombatActionRequestPlanResult(
                status,
                registry,
                action,
                null,
                null,
                null);
        }

        private static CombatActionRequestPlanResult RecordRejectedRequest(
            CombatActionPlanStatus status,
            CombatActionRegistrySnapshot registry,
            CombatActionRequest request,
            CombatActionResourcePolicy policy)
        {
            if (registry == null ||
                request == null ||
                request.ActionId.IsDefault)
            {
                return RejectRequest(status, registry, null);
            }

            if (registry.RevisionOrdinal == long.MaxValue ||
                registry.RejectedCorrelations.Count >=
                    CombatActionRegistrySnapshot.MaximumRejectedCorrelations)
            {
                return RejectRequest(
                    CombatActionPlanStatus.CapacityReached,
                    registry,
                    null);
            }

            var rejected =
                new CombatActionRejectedCorrelationReceipt(
                    request,
                    policy,
                    status);
            CombatActionRegistrySnapshot next =
                registry.RecordRejected(rejected);
            return new CombatActionRequestPlanResult(
                status,
                next,
                null,
                null,
                null,
                null,
                rejected);
        }

        private static CombatActionTransitionPlanResult RejectTransition(
            CombatActionPlanStatus status,
            CombatActionSnapshot action)
        {
            return new CombatActionTransitionPlanResult(
                status,
                action,
                null,
                null,
                null);
        }
    }

    public enum CombatCooldownQueryStatus
    {
        Unknown = 0,
        None = 1,
        Active = 2,
        Completed = 3,
        InvalidClock = 4,
        Unavailable = 5
    }

    public enum CombatCooldownStartStatus
    {
        Started = 0,
        DuplicateExact = 1,
        CorrelationConflict = 2,
        CooldownActive = 3,
        InvalidRequest = 4,
        WrongEncounter = 5,
        StaleRevision = 6,
        /// <summary>
        /// Recoverable admission block. The owning adapter must reconcile or
        /// explicitly compact/recover capacity; it must never silently retry.
        /// </summary>
        CapacityReached = 7
    }

    public sealed class CombatCooldownStartRequest
    {
        public CombatCooldownStartRequest(
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId skillId,
            CombatContractVersion skillContentVersion,
            CombatStableId sourceActionId,
            long startEncounterTimeMicros,
            long durationMicros,
            string expectedRegistryRevision)
        {
            OperationId = operationId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            SkillId = skillId;
            SkillContentVersion = skillContentVersion;
            SourceActionId = sourceActionId;
            StartEncounterTimeMicros = startEncounterTimeMicros;
            DurationMicros = durationMicros;
            ExpectedRegistryRevision = expectedRegistryRevision ?? string.Empty;
        }

        public CombatStableId OperationId { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatStableId SkillId { get; }
        public CombatContractVersion SkillContentVersion { get; }
        public CombatStableId SourceActionId { get; }
        public long StartEncounterTimeMicros { get; }
        public long DurationMicros { get; }
        public string ExpectedRegistryRevision { get; }

        internal bool PayloadEquals(CombatCooldownStartRequest other)
        {
            return other != null &&
                   OperationId == other.OperationId &&
                   EncounterSessionId == other.EncounterSessionId &&
                   EncounterAttemptId == other.EncounterAttemptId &&
                   ActorParticipantId == other.ActorParticipantId &&
                   SkillId == other.SkillId &&
                   SkillContentVersion.Equals(other.SkillContentVersion) &&
                   SourceActionId == other.SourceActionId &&
                   StartEncounterTimeMicros == other.StartEncounterTimeMicros &&
                   DurationMicros == other.DurationMicros &&
                   StringComparer.Ordinal.Equals(
                       ExpectedRegistryRevision,
                       other.ExpectedRegistryRevision);
        }
    }

    public sealed class CombatCooldownSnapshot
    {
        internal CombatCooldownSnapshot(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId skillId,
            CombatContractVersion skillContentVersion,
            CombatStableId sourceActionId,
            long startEncounterTimeMicros,
            long endEncounterTimeMicros,
            long durationMicros,
            string stateRevision)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            SkillId = skillId;
            SkillContentVersion = skillContentVersion;
            SourceActionId = sourceActionId;
            StartEncounterTimeMicros = startEncounterTimeMicros;
            EndEncounterTimeMicros = endEncounterTimeMicros;
            DurationMicros = durationMicros;
            StateRevision = stateRevision ?? string.Empty;
        }

        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatStableId SkillId { get; }
        public CombatContractVersion SkillContentVersion { get; }
        public CombatStableId SourceActionId { get; }
        public long StartEncounterTimeMicros { get; }
        public long EndEncounterTimeMicros { get; }
        public long DurationMicros { get; }
        public string StateRevision { get; }
    }

    public sealed class CombatCooldownCorrelationReceipt
    {
        internal CombatCooldownCorrelationReceipt(
            CombatCooldownStartRequest request,
            CombatCooldownSnapshot cooldown)
        {
            Request = request;
            Cooldown = cooldown;
        }

        public CombatCooldownStartRequest Request { get; }
        public CombatCooldownSnapshot Cooldown { get; }
    }

    public sealed class CombatCooldownRegistrySnapshot
    {
        public const int MaximumSkillKeys = 64;
        public const int MaximumCorrelationReceipts = 4096;

        private CombatCooldownRegistrySnapshot(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            long revisionOrdinal,
            IList<CombatCooldownSnapshot> cooldowns,
            IList<CombatCooldownCorrelationReceipt> correlationReceipts)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            RevisionOrdinal = revisionOrdinal;
            Revision =
                "cooldown-registry-r" +
                revisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);
            Cooldowns = Freeze(cooldowns);
            CorrelationReceipts = Freeze(correlationReceipts);
        }

        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public long RevisionOrdinal { get; }
        public string Revision { get; }
        public IReadOnlyList<CombatCooldownSnapshot> Cooldowns { get; }
        public IReadOnlyList<CombatCooldownCorrelationReceipt>
            CorrelationReceipts { get; }

        public static bool TryCreate(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            out CombatCooldownRegistrySnapshot snapshot)
        {
            snapshot = null;
            if (encounterSessionId.IsDefault || encounterAttemptId.IsDefault)
            {
                return false;
            }

            snapshot = new CombatCooldownRegistrySnapshot(
                encounterSessionId,
                encounterAttemptId,
                0L,
                null,
                null);
            return true;
        }

        internal CombatCooldownRegistrySnapshot WithStarted(
            CombatCooldownStartRequest request,
            CombatCooldownSnapshot cooldown)
        {
            var cooldowns = new List<CombatCooldownSnapshot>(Cooldowns);
            int existingIndex = cooldowns.FindIndex(
                existing =>
                    existing.ActorParticipantId == request.ActorParticipantId &&
                    existing.SkillId == request.SkillId);
            if (existingIndex < 0)
            {
                cooldowns.Add(cooldown);
            }
            else
            {
                cooldowns[existingIndex] = cooldown;
            }

            var receipts =
                new List<CombatCooldownCorrelationReceipt>(CorrelationReceipts)
                {
                    new CombatCooldownCorrelationReceipt(request, cooldown)
                };
            return new CombatCooldownRegistrySnapshot(
                EncounterSessionId,
                EncounterAttemptId,
                RevisionOrdinal + 1L,
                cooldowns,
                receipts);
        }

        private static IReadOnlyList<T> Freeze<T>(IList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.AsReadOnly(new T[0]);
            }

            var copy = new T[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatCooldownStartResult
    {
        internal CombatCooldownStartResult(
            CombatCooldownStartStatus status,
            CombatCooldownRegistrySnapshot registry,
            CombatCooldownSnapshot cooldown,
            CombatCooldownCorrelationReceipt existingReceipt)
        {
            Status = status;
            Registry = registry;
            Cooldown = cooldown;
            ExistingReceipt = existingReceipt;
        }

        public CombatCooldownStartStatus Status { get; }
        public CombatCooldownRegistrySnapshot Registry { get; }
        public CombatCooldownSnapshot Cooldown { get; }
        public CombatCooldownCorrelationReceipt ExistingReceipt { get; }
    }

    public sealed class CombatCooldownQueryResult
    {
        internal CombatCooldownQueryResult(
            CombatCooldownQueryStatus status,
            CombatCooldownSnapshot cooldown)
        {
            Status = status;
            Cooldown = cooldown;
        }

        public CombatCooldownQueryStatus Status { get; }
        public CombatCooldownSnapshot Cooldown { get; }
    }

    public static class CombatCooldownPlanner
    {
        public static CombatCooldownStartResult Start(
            CombatCooldownRegistrySnapshot registry,
            CombatCooldownStartRequest request)
        {
            if (registry == null || request == null)
            {
                return Reject(
                    CombatCooldownStartStatus.InvalidRequest,
                    registry);
            }

            CombatCooldownCorrelationReceipt existingReceipt =
                registry.CorrelationReceipts.FirstOrDefault(
                    receipt =>
                        receipt.Request.OperationId == request.OperationId ||
                        receipt.Request.SourceActionId == request.SourceActionId);
            if (existingReceipt != null)
            {
                bool exact = existingReceipt.Request.PayloadEquals(request);
                return new CombatCooldownStartResult(
                    exact
                        ? CombatCooldownStartStatus.DuplicateExact
                        : CombatCooldownStartStatus.CorrelationConflict,
                    registry,
                    exact ? existingReceipt.Cooldown : null,
                    existingReceipt);
            }

            if (!IsValid(request))
            {
                return Reject(
                    CombatCooldownStartStatus.InvalidRequest,
                    registry);
            }

            if (registry.EncounterSessionId != request.EncounterSessionId ||
                registry.EncounterAttemptId != request.EncounterAttemptId)
            {
                return Reject(
                    CombatCooldownStartStatus.WrongEncounter,
                    registry);
            }

            if (!StringComparer.Ordinal.Equals(
                    registry.Revision,
                    request.ExpectedRegistryRevision))
            {
                return Reject(
                    CombatCooldownStartStatus.StaleRevision,
                    registry);
            }

            CombatCooldownSnapshot existingKey = registry.Cooldowns
                .FirstOrDefault(
                    cooldown =>
                        cooldown.ActorParticipantId ==
                            request.ActorParticipantId &&
                        cooldown.SkillId == request.SkillId);
            if (existingKey != null &&
                !existingKey.SkillContentVersion.Equals(
                    request.SkillContentVersion))
            {
                return new CombatCooldownStartResult(
                    CombatCooldownStartStatus.CorrelationConflict,
                    registry,
                    existingKey,
                    null);
            }

            if (existingKey != null &&
                request.StartEncounterTimeMicros <
                    existingKey.EndEncounterTimeMicros)
            {
                return new CombatCooldownStartResult(
                    CombatCooldownStartStatus.CooldownActive,
                    registry,
                    existingKey,
                    null);
            }

            if ((existingKey == null &&
                 registry.Cooldowns.Count >=
                    CombatCooldownRegistrySnapshot.MaximumSkillKeys) ||
                registry.CorrelationReceipts.Count >=
                    CombatCooldownRegistrySnapshot.MaximumCorrelationReceipts ||
                registry.RevisionOrdinal == long.MaxValue)
            {
                return Reject(
                    CombatCooldownStartStatus.CapacityReached,
                    registry);
            }

            string nextRevision =
                "cooldown-r" +
                (registry.RevisionOrdinal + 1L).ToString(
                    "D16",
                    CultureInfo.InvariantCulture);
            CombatCooldownSnapshot plannedCooldown = CreatePlannedSnapshot(
                request.EncounterSessionId,
                request.EncounterAttemptId,
                request.ActorParticipantId,
                request.SkillId,
                request.SkillContentVersion,
                request.SourceActionId,
                request.StartEncounterTimeMicros,
                request.DurationMicros,
                nextRevision);
            if (plannedCooldown == null)
            {
                return Reject(
                    CombatCooldownStartStatus.InvalidRequest,
                    registry);
            }

            return new CombatCooldownStartResult(
                CombatCooldownStartStatus.Started,
                registry.WithStarted(request, plannedCooldown),
                plannedCooldown,
                null);
        }

        public static CombatCooldownQueryResult Query(
            CombatCooldownRegistrySnapshot registry,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId skillId,
            CombatContractVersion skillContentVersion,
            long encounterTimeMicros,
            bool clockAvailable)
        {
            if (registry == null ||
                !clockAvailable ||
                registry.EncounterSessionId != encounterSessionId ||
                registry.EncounterAttemptId != encounterAttemptId)
            {
                return new CombatCooldownQueryResult(
                    CombatCooldownQueryStatus.Unavailable,
                    null);
            }

            if (actorParticipantId.IsDefault ||
                skillId.IsDefault ||
                skillContentVersion.IsDefault)
            {
                return new CombatCooldownQueryResult(
                    CombatCooldownQueryStatus.Unknown,
                    null);
            }

            if (encounterTimeMicros < 0L ||
                encounterTimeMicros >
                    CombatTechnicalLimits.DurationMaximumMicros)
            {
                return new CombatCooldownQueryResult(
                    CombatCooldownQueryStatus.InvalidClock,
                    null);
            }

            CombatCooldownSnapshot cooldown = registry.Cooldowns.FirstOrDefault(
                candidate =>
                    candidate.ActorParticipantId == actorParticipantId &&
                    candidate.SkillId == skillId);
            if (cooldown == null)
            {
                return new CombatCooldownQueryResult(
                    CombatCooldownQueryStatus.None,
                    null);
            }

            if (!cooldown.SkillContentVersion.Equals(skillContentVersion))
            {
                return new CombatCooldownQueryResult(
                    CombatCooldownQueryStatus.Unknown,
                    cooldown);
            }

            if (encounterTimeMicros < cooldown.StartEncounterTimeMicros)
            {
                return new CombatCooldownQueryResult(
                    CombatCooldownQueryStatus.InvalidClock,
                    cooldown);
            }

            return new CombatCooldownQueryResult(
                encounterTimeMicros < cooldown.EndEncounterTimeMicros
                    ? CombatCooldownQueryStatus.Active
                    : CombatCooldownQueryStatus.Completed,
                cooldown);
        }

        internal static CombatCooldownSnapshot CreatePlannedSnapshot(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId skillId,
            CombatContractVersion skillContentVersion,
            CombatStableId sourceActionId,
            long startEncounterTimeMicros,
            long durationMicros,
            string stateRevision)
        {
            if (encounterSessionId.IsDefault ||
                encounterAttemptId.IsDefault ||
                actorParticipantId.IsDefault ||
                skillId.IsDefault ||
                skillContentVersion.IsDefault ||
                sourceActionId.IsDefault ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    startEncounterTimeMicros,
                    CombatScalarKind.Duration,
                    false) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    durationMicros,
                    CombatScalarKind.Duration,
                    true) ||
                !CombatPrimitiveValidation.IsStableId(stateRevision))
            {
                return null;
            }

            long endEncounterTimeMicros;
            try
            {
                endEncounterTimeMicros = checked(
                    startEncounterTimeMicros + durationMicros);
            }
            catch (OverflowException)
            {
                return null;
            }

            if (endEncounterTimeMicros >
                CombatTechnicalLimits.DurationMaximumMicros)
            {
                return null;
            }

            return new CombatCooldownSnapshot(
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                skillId,
                skillContentVersion,
                sourceActionId,
                startEncounterTimeMicros,
                endEncounterTimeMicros,
                durationMicros,
                stateRevision);
        }

        private static bool IsValid(CombatCooldownStartRequest request)
        {
            return !request.OperationId.IsDefault &&
                   !request.EncounterSessionId.IsDefault &&
                   !request.EncounterAttemptId.IsDefault &&
                   !request.ActorParticipantId.IsDefault &&
                   !request.SkillId.IsDefault &&
                   !request.SkillContentVersion.IsDefault &&
                   !request.SourceActionId.IsDefault &&
                   CombatPrimitiveValidation.IsStableId(
                       request.ExpectedRegistryRevision) &&
                   CombatPrimitiveValidation.IsMicrosInRange(
                       request.StartEncounterTimeMicros,
                       CombatScalarKind.Duration,
                       false) &&
                   CombatPrimitiveValidation.IsMicrosInRange(
                       request.DurationMicros,
                       CombatScalarKind.Duration,
                       true);
        }

        private static CombatCooldownStartResult Reject(
            CombatCooldownStartStatus status,
            CombatCooldownRegistrySnapshot registry)
        {
            return new CombatCooldownStartResult(
                status,
                registry,
                null,
                null);
        }
    }
}
