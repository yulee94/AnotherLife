using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AL.ChampionMode.C1
{
    public enum CombatantResourceOperationKind
    {
        Damage = 0,
        Healing = 1,
        ReserveMana = 2,
        CommitManaReservation = 3,
        ReleaseManaReservation = 4,
        RestoreMana = 5,
        RegenerateMana = 6
    }

    public enum CombatantResourcePlanStatus
    {
        Applied = 0,
        AppliedAndDefeated = 1,
        NoChangeZero = 2,
        NoChangeAtMaximum = 3,
        DuplicateExact = 4,
        CorrelationConflict = 5,
        InvalidRequest = 6,
        InvalidAmount = 7,
        WrongEncounter = 8,
        WrongActor = 9,
        NotAlive = 10,
        StaleRevision = 11,
        InsufficientMana = 12,
        ReservationNotFound = 13,
        ReservationAlreadyFinalized = 14,
        RegenerationProhibited = 15,
        /// <summary>
        /// Recoverable admission block. The owning adapter must reconcile or
        /// explicitly compact/recover capacity; it must never silently retry.
        /// </summary>
        CapacityReached = 16,
        ArithmeticFailure = 17,
        StaleRegenerationTick = 18,
        OutOfOrderRegenerationTick = 19,
        NegativeAmount = 20,
        AmountAboveMaximum = 21,
        /// <summary>
        /// No mutation or events were produced. The owning adapter must recover
        /// by reconciling against the current authoritative resource snapshot.
        /// </summary>
        ReplayWindowExpired = 22
    }

    public enum ManaReservationState
    {
        Reserved = 0,
        Committed = 1,
        Released = 2
    }

    public enum CombatantResourceEventKind
    {
        ResourcesChanged = 0,
        CombatantDefeated = 1
    }

    public sealed class ManaReservationSnapshot
    {
        internal ManaReservationSnapshot(
            CombatStableId actionId,
            long amountMicros,
            ManaReservationState state,
            CombatStableId sourceOperationId)
        {
            ActionId = actionId;
            AmountMicros = amountMicros;
            State = state;
            SourceOperationId = sourceOperationId;
        }

        public CombatStableId ActionId { get; }
        public long AmountMicros { get; }
        public ManaReservationState State { get; }
        public CombatStableId SourceOperationId { get; }

        internal ManaReservationSnapshot WithState(
            ManaReservationState state,
            CombatStableId sourceOperationId)
        {
            return new ManaReservationSnapshot(ActionId, AmountMicros, state, sourceOperationId);
        }
    }

    public sealed class CombatantResourceOperationRequest
    {
        private CombatantResourceOperationRequest(
            CombatantResourceOperationKind kind,
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId sourceParticipantId,
            CombatStableId sourceBehaviorId,
            CombatStableId correlationId,
            long amountMicros,
            string expectedResourceRevision,
            string reason,
            long regenerationRateMicrosPerSecond,
            long elapsedDurationMicros,
            CombatStableId encounterClockRevision,
            bool regenerationAllowed,
            long regenerationTickOrdinal)
        {
            Kind = kind;
            OperationId = operationId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            SourceParticipantId = sourceParticipantId;
            SourceBehaviorId = sourceBehaviorId;
            CorrelationId = correlationId;
            AmountMicros = amountMicros;
            ExpectedResourceRevision = expectedResourceRevision ?? string.Empty;
            Reason = reason ?? string.Empty;
            RegenerationRateMicrosPerSecond = regenerationRateMicrosPerSecond;
            ElapsedDurationMicros = elapsedDurationMicros;
            EncounterClockRevision = encounterClockRevision;
            RegenerationAllowed = regenerationAllowed;
            RegenerationTickOrdinal = regenerationTickOrdinal;
        }

        public CombatantResourceOperationKind Kind { get; }
        public CombatStableId OperationId { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatStableId SourceParticipantId { get; }
        public CombatStableId SourceBehaviorId { get; }
        public CombatStableId CorrelationId { get; }
        public long AmountMicros { get; }
        public string ExpectedResourceRevision { get; }
        public string Reason { get; }
        public long RegenerationRateMicrosPerSecond { get; }
        public long ElapsedDurationMicros { get; }
        public CombatStableId EncounterClockRevision { get; }
        public bool RegenerationAllowed { get; }
        public long RegenerationTickOrdinal { get; }

        public static CombatantResourceOperationRequest Damage(
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId sourceParticipantId,
            CombatStableId sourceBehaviorId,
            CombatStableId sourceOperationId,
            long amountMicros,
            string expectedResourceRevision)
        {
            return CreateAmount(
                CombatantResourceOperationKind.Damage,
                operationId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                sourceParticipantId,
                sourceBehaviorId,
                sourceOperationId,
                amountMicros,
                expectedResourceRevision);
        }

        public static CombatantResourceOperationRequest Healing(
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId sourceParticipantId,
            CombatStableId sourceBehaviorId,
            CombatStableId sourceOperationId,
            long amountMicros,
            string expectedResourceRevision)
        {
            return CreateAmount(
                CombatantResourceOperationKind.Healing,
                operationId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                sourceParticipantId,
                sourceBehaviorId,
                sourceOperationId,
                amountMicros,
                expectedResourceRevision);
        }

        public static CombatantResourceOperationRequest ReserveMana(
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId actionId,
            long amountMicros,
            string expectedResourceRevision)
        {
            return CreateAmount(
                CombatantResourceOperationKind.ReserveMana,
                operationId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                default,
                default,
                actionId,
                amountMicros,
                expectedResourceRevision);
        }

        public static CombatantResourceOperationRequest CommitManaReservation(
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId actionId,
            string expectedResourceRevision)
        {
            return CreateAmount(
                CombatantResourceOperationKind.CommitManaReservation,
                operationId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                default,
                default,
                actionId,
                0L,
                expectedResourceRevision);
        }

        public static CombatantResourceOperationRequest ReleaseManaReservation(
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId actionId,
            string reason,
            string expectedResourceRevision)
        {
            return new CombatantResourceOperationRequest(
                CombatantResourceOperationKind.ReleaseManaReservation,
                operationId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                default,
                default,
                actionId,
                0L,
                expectedResourceRevision,
                reason,
                0L,
                0L,
                default,
                false,
                -1L);
        }

        public static CombatantResourceOperationRequest RestoreMana(
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId sourceOperationId,
            long amountMicros,
            string expectedResourceRevision)
        {
            return CreateAmount(
                CombatantResourceOperationKind.RestoreMana,
                operationId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                default,
                default,
                sourceOperationId,
                amountMicros,
                expectedResourceRevision);
        }

        public static CombatantResourceOperationRequest RegenerateMana(
            CombatStableId tickId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            long regenerationRateMicrosPerSecond,
            long elapsedDurationMicros,
            CombatStableId encounterClockRevision,
            bool regenerationAllowed,
            long regenerationTickOrdinal,
            string expectedResourceRevision)
        {
            return new CombatantResourceOperationRequest(
                CombatantResourceOperationKind.RegenerateMana,
                tickId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                default,
                default,
                encounterClockRevision,
                0L,
                expectedResourceRevision,
                string.Empty,
                regenerationRateMicrosPerSecond,
                elapsedDurationMicros,
                encounterClockRevision,
                regenerationAllowed,
                regenerationTickOrdinal);
        }

        internal bool PayloadEquals(CombatantResourceOperationRequest other)
        {
            return other != null &&
                   Kind == other.Kind &&
                   OperationId == other.OperationId &&
                   EncounterSessionId == other.EncounterSessionId &&
                   EncounterAttemptId == other.EncounterAttemptId &&
                   ActorParticipantId == other.ActorParticipantId &&
                   SourceParticipantId == other.SourceParticipantId &&
                   SourceBehaviorId == other.SourceBehaviorId &&
                   CorrelationId == other.CorrelationId &&
                   AmountMicros == other.AmountMicros &&
                   StringComparer.Ordinal.Equals(
                       ExpectedResourceRevision,
                       other.ExpectedResourceRevision) &&
                   StringComparer.Ordinal.Equals(Reason, other.Reason) &&
                   RegenerationRateMicrosPerSecond == other.RegenerationRateMicrosPerSecond &&
                   ElapsedDurationMicros == other.ElapsedDurationMicros &&
                   EncounterClockRevision == other.EncounterClockRevision &&
                   RegenerationAllowed == other.RegenerationAllowed &&
                   RegenerationTickOrdinal == other.RegenerationTickOrdinal;
        }

        private static CombatantResourceOperationRequest CreateAmount(
            CombatantResourceOperationKind kind,
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId sourceParticipantId,
            CombatStableId sourceBehaviorId,
            CombatStableId correlationId,
            long amountMicros,
            string expectedResourceRevision)
        {
            return new CombatantResourceOperationRequest(
                kind,
                operationId,
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                sourceParticipantId,
                sourceBehaviorId,
                correlationId,
                amountMicros,
                expectedResourceRevision,
                string.Empty,
                0L,
                0L,
                default,
                false,
                -1L);
        }
    }

    public sealed class CombatantResourceOperationReceipt
    {
        internal CombatantResourceOperationReceipt(
            CombatantResourceOperationRequest request,
            CombatantResourcePlanStatus status,
            string beforeRevision,
            string afterRevision,
            long healthDeltaMicros,
            long manaDeltaMicros,
            long reservedManaDeltaMicros)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Status = status;
            BeforeRevision = beforeRevision ?? string.Empty;
            AfterRevision = afterRevision ?? string.Empty;
            HealthDeltaMicros = healthDeltaMicros;
            ManaDeltaMicros = manaDeltaMicros;
            ReservedManaDeltaMicros = reservedManaDeltaMicros;
        }

        public CombatantResourceOperationRequest Request { get; }
        public CombatantResourcePlanStatus Status { get; }
        public string BeforeRevision { get; }
        public string AfterRevision { get; }
        public long HealthDeltaMicros { get; }
        public long ManaDeltaMicros { get; }
        public long ReservedManaDeltaMicros { get; }
    }

    public sealed class CombatantResourceEventReceipt
    {
        internal CombatantResourceEventReceipt(
            CombatantResourceEventKind kind,
            CombatStableId operationId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId correlationId,
            CombatStableId sourceParticipantId,
            CombatStableId sourceBehaviorId,
            string beforeResourceRevision,
            string afterResourceRevision,
            long healthDeltaMicros,
            long manaDeltaMicros,
            long reservedManaDeltaMicros)
        {
            Kind = kind;
            OperationId = operationId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            CorrelationId = correlationId;
            SourceParticipantId = sourceParticipantId;
            SourceBehaviorId = sourceBehaviorId;
            BeforeResourceRevision = beforeResourceRevision ?? string.Empty;
            AfterResourceRevision = afterResourceRevision ?? string.Empty;
            HealthDeltaMicros = healthDeltaMicros;
            ManaDeltaMicros = manaDeltaMicros;
            ReservedManaDeltaMicros = reservedManaDeltaMicros;
        }

        public CombatantResourceEventKind Kind { get; }
        public CombatStableId OperationId { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatStableId CorrelationId { get; }
        public CombatStableId SourceParticipantId { get; }
        public CombatStableId SourceBehaviorId { get; }
        public string BeforeResourceRevision { get; }
        public string AfterResourceRevision { get; }
        public string ResourceRevision => AfterResourceRevision;
        public long HealthDeltaMicros { get; }
        public long ManaDeltaMicros { get; }
        public long ReservedManaDeltaMicros { get; }
    }

    public sealed class CombatantResourceSnapshot
    {
        public const int MaximumReservations = 64;
        public const int MaximumOperationReceipts = 4096;
        /// <summary>
        /// High-frequency replay horizon. Retained ordinals distinguish exact
        /// duplicates from conflicts. Older ordinals return ReplayWindowExpired
        /// without mutation so the owning adapter can reconcile current state.
        /// </summary>
        public const int MaximumRegenerationReplayReceipts = 64;

        private static readonly IReadOnlyList<ManaReservationSnapshot> EmptyReservations =
            Array.AsReadOnly(new ManaReservationSnapshot[0]);
        private static readonly IReadOnlyList<CombatantResourceOperationReceipt> EmptyReceipts =
            Array.AsReadOnly(new CombatantResourceOperationReceipt[0]);

        private CombatantResourceSnapshot(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            long currentHealthMicros,
            long maxHealthMicros,
            long currentManaMicros,
            long reservedManaMicros,
            long maxManaMicros,
            CombatantLifeState lifeState,
            long revisionOrdinal,
            long regenerationAccumulatorRemainder,
            IList<ManaReservationSnapshot> reservations,
            IList<CombatantResourceOperationReceipt> operationReceipts,
            long nextRegenerationTickOrdinal,
            CombatantResourceOperationReceipt latestRegenerationReceipt,
            IList<CombatantResourceOperationReceipt>
                regenerationReplayReceipts)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            CurrentHealthMicros = currentHealthMicros;
            MaxHealthMicros = maxHealthMicros;
            CurrentManaMicros = currentManaMicros;
            ReservedManaMicros = reservedManaMicros;
            MaxManaMicros = maxManaMicros;
            LifeState = lifeState;
            RevisionOrdinal = revisionOrdinal;
            Revision = FormatRevision(revisionOrdinal);
            RegenerationAccumulatorRemainder = regenerationAccumulatorRemainder;
            Reservations = Freeze(reservations, EmptyReservations);
            OperationReceipts = Freeze(operationReceipts, EmptyReceipts);
            NextRegenerationTickOrdinal = nextRegenerationTickOrdinal;
            LatestRegenerationReceipt = latestRegenerationReceipt;
            RegenerationReplayReceipts = Freeze(
                regenerationReplayReceipts,
                EmptyReceipts);
        }

        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public long CurrentHealthMicros { get; }
        public long MaxHealthMicros { get; }
        public long CurrentManaMicros { get; }
        public long ReservedManaMicros { get; }
        public long AvailableManaMicros => CurrentManaMicros - ReservedManaMicros;
        public long MaxManaMicros { get; }
        public CombatantLifeState LifeState { get; }
        public long RevisionOrdinal { get; }
        public string Revision { get; }
        public long RegenerationAccumulatorRemainder { get; }
        public IReadOnlyList<ManaReservationSnapshot> Reservations { get; }
        public IReadOnlyList<CombatantResourceOperationReceipt> OperationReceipts { get; }
        public long NextRegenerationTickOrdinal { get; }
        public CombatantResourceOperationReceipt LatestRegenerationReceipt { get; }
        public IReadOnlyList<CombatantResourceOperationReceipt>
            RegenerationReplayReceipts { get; }

        public static bool TryCreate(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            long currentHealthMicros,
            long maxHealthMicros,
            long currentManaMicros,
            long maxManaMicros,
            out CombatantResourceSnapshot snapshot)
        {
            snapshot = null;
            if (encounterSessionId.IsDefault ||
                encounterAttemptId.IsDefault ||
                actorParticipantId.IsDefault ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    maxHealthMicros,
                    CombatScalarKind.Health,
                    true) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    currentHealthMicros,
                    CombatScalarKind.Health,
                    false) ||
                currentHealthMicros > maxHealthMicros ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    maxManaMicros,
                    CombatScalarKind.Mana,
                    true) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    currentManaMicros,
                    CombatScalarKind.Mana,
                    false) ||
                currentManaMicros > maxManaMicros)
            {
                return false;
            }

            snapshot = new CombatantResourceSnapshot(
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                currentHealthMicros,
                maxHealthMicros,
                currentManaMicros,
                0L,
                maxManaMicros,
                currentHealthMicros == 0L
                    ? CombatantLifeState.Defeated
                    : CombatantLifeState.Alive,
                0L,
                0L,
                null,
                null,
                0L,
                null,
                null);
            return true;
        }

        internal CombatantResourceSnapshot With(
            long currentHealthMicros,
            long currentManaMicros,
            long reservedManaMicros,
            CombatantLifeState lifeState,
            long revisionOrdinal,
            long regenerationAccumulatorRemainder,
            IList<ManaReservationSnapshot> reservations,
            IList<CombatantResourceOperationReceipt> operationReceipts,
            long nextRegenerationTickOrdinal,
            CombatantResourceOperationReceipt latestRegenerationReceipt,
            IList<CombatantResourceOperationReceipt>
                regenerationReplayReceipts)
        {
            return new CombatantResourceSnapshot(
                EncounterSessionId,
                EncounterAttemptId,
                ActorParticipantId,
                currentHealthMicros,
                MaxHealthMicros,
                currentManaMicros,
                reservedManaMicros,
                MaxManaMicros,
                lifeState,
                revisionOrdinal,
                regenerationAccumulatorRemainder,
                reservations,
                operationReceipts,
                nextRegenerationTickOrdinal,
                latestRegenerationReceipt,
                regenerationReplayReceipts);
        }

        private static string FormatRevision(long revisionOrdinal)
        {
            return "resource-r" + revisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);
        }

        private static IReadOnlyList<T> Freeze<T>(
            IList<T> source,
            IReadOnlyList<T> empty)
        {
            if (source == null || source.Count == 0)
            {
                return empty;
            }

            var copy = new T[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatantResourcePlanResult
    {
        private static readonly IReadOnlyList<CombatantResourceEventReceipt> EmptyEvents =
            Array.AsReadOnly(new CombatantResourceEventReceipt[0]);

        internal CombatantResourcePlanResult(
            CombatantResourcePlanStatus status,
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationReceipt operationReceipt,
            CombatantResourceOperationReceipt existingReceipt,
            IList<CombatantResourceEventReceipt> events)
        {
            Status = status;
            Snapshot = snapshot;
            OperationReceipt = operationReceipt;
            ExistingReceipt = existingReceipt;
            if (events == null || events.Count == 0)
            {
                Events = EmptyEvents;
            }
            else
            {
                var copy = new CombatantResourceEventReceipt[events.Count];
                events.CopyTo(copy, 0);
                Events = Array.AsReadOnly(copy);
            }
        }

        public CombatantResourcePlanStatus Status { get; }
        public CombatantResourceSnapshot Snapshot { get; }
        public CombatantResourceOperationReceipt OperationReceipt { get; }
        public CombatantResourceOperationReceipt ExistingReceipt { get; }
        public IReadOnlyList<CombatantResourceEventReceipt> Events { get; }
        public bool IsApplied =>
            Status == CombatantResourcePlanStatus.Applied ||
            Status == CombatantResourcePlanStatus.AppliedAndDefeated;
    }

    /// <summary>
    /// Stateless combat-resource transition planner. Every scan and retained
    /// collection is bounded so duplicate safety cannot create unbounded work.
    /// </summary>
    public static class CombatantResourcePlanner
    {
        public static CombatantResourcePlanResult Plan(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            if (snapshot == null || request == null)
            {
                return Reject(CombatantResourcePlanStatus.InvalidRequest, snapshot);
            }

            if (!request.OperationId.IsDefault &&
                !HasTypedOperationNamespace(request))
            {
                return Reject(
                    CombatantResourcePlanStatus.CorrelationConflict,
                    snapshot);
            }

            if (request.Kind == CombatantResourceOperationKind.RegenerateMana)
            {
                if (request.RegenerationTickOrdinal < 0L)
                {
                    return Reject(
                        CombatantResourcePlanStatus.InvalidRequest,
                        snapshot);
                }

                if (request.RegenerationTickOrdinal <
                    snapshot.NextRegenerationTickOrdinal)
                {
                    CombatantResourceOperationReceipt replay =
                        snapshot.RegenerationReplayReceipts.FirstOrDefault(
                            receipt =>
                                receipt.Request.RegenerationTickOrdinal ==
                                request.RegenerationTickOrdinal);
                    if (replay != null)
                    {
                        return replay.Request.PayloadEquals(request)
                            ? new CombatantResourcePlanResult(
                                CombatantResourcePlanStatus.DuplicateExact,
                                snapshot,
                                null,
                                replay,
                                null)
                            : new CombatantResourcePlanResult(
                                CombatantResourcePlanStatus.CorrelationConflict,
                                snapshot,
                                null,
                                replay,
                                null);
                    }

                    return Reject(
                        CombatantResourcePlanStatus.ReplayWindowExpired,
                        snapshot);
                }

                if (request.RegenerationTickOrdinal >
                    snapshot.NextRegenerationTickOrdinal)
                {
                    return Reject(
                        CombatantResourcePlanStatus.OutOfOrderRegenerationTick,
                        snapshot);
                }

                if (snapshot.NextRegenerationTickOrdinal == long.MaxValue)
                {
                    return Reject(
                        CombatantResourcePlanStatus.ArithmeticFailure,
                        snapshot);
                }
            }

            CombatantResourceOperationReceipt existing = snapshot.OperationReceipts
                .FirstOrDefault(receipt => receipt.Request.OperationId == request.OperationId);
            if (existing != null)
            {
                return existing.Request.PayloadEquals(request)
                    ? new CombatantResourcePlanResult(
                        CombatantResourcePlanStatus.DuplicateExact,
                        snapshot,
                        null,
                        existing,
                        null)
                    : new CombatantResourcePlanResult(
                        CombatantResourcePlanStatus.CorrelationConflict,
                        snapshot,
                        null,
                        existing,
                        null);
            }

            CombatantResourcePlanStatus contextStatus = ValidateContext(snapshot, request);
            if (contextStatus != CombatantResourcePlanStatus.Applied)
            {
                return Reject(contextStatus, snapshot);
            }

            if (request.Kind == CombatantResourceOperationKind.RegenerateMana &&
                !IsOrdinalBoundId(
                    request.OperationId,
                    request.RegenerationTickOrdinal))
            {
                return Reject(
                    CombatantResourcePlanStatus.CorrelationConflict,
                    snapshot);
            }

            if (request.Kind != CombatantResourceOperationKind.RegenerateMana &&
                !HasGuaranteedOperationCapacity(snapshot, request))
            {
                return Reject(CombatantResourcePlanStatus.CapacityReached, snapshot);
            }

            switch (request.Kind)
            {
                case CombatantResourceOperationKind.Damage:
                    return PlanDamage(snapshot, request);
                case CombatantResourceOperationKind.Healing:
                    return PlanHealing(snapshot, request);
                case CombatantResourceOperationKind.ReserveMana:
                    return PlanReserveMana(snapshot, request);
                case CombatantResourceOperationKind.CommitManaReservation:
                    return PlanCommitMana(snapshot, request);
                case CombatantResourceOperationKind.ReleaseManaReservation:
                    return PlanReleaseMana(snapshot, request);
                case CombatantResourceOperationKind.RestoreMana:
                    return PlanRestoreMana(snapshot, request);
                case CombatantResourceOperationKind.RegenerateMana:
                    return PlanRegenerateMana(snapshot, request);
                default:
                    return Reject(CombatantResourcePlanStatus.InvalidRequest, snapshot);
            }
        }

        private static CombatantResourcePlanResult PlanDamage(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            CombatantResourcePlanStatus amountStatus = ValidateAmount(
                request.AmountMicros,
                CombatScalarKind.Damage);
            if (amountStatus != CombatantResourcePlanStatus.Applied)
            {
                return amountStatus == CombatantResourcePlanStatus.NoChangeZero
                    ? RecordNoChange(snapshot, request, amountStatus)
                    : Reject(amountStatus, snapshot);
            }

            if (snapshot.LifeState != CombatantLifeState.Alive)
            {
                return Reject(CombatantResourcePlanStatus.NotAlive, snapshot);
            }

            long applied = Math.Min(request.AmountMicros, snapshot.CurrentHealthMicros);
            long nextHealth = snapshot.CurrentHealthMicros - applied;
            CombatantLifeState nextLife = nextHealth == 0L
                ? CombatantLifeState.Defeated
                : CombatantLifeState.Alive;
            CombatantResourcePlanStatus status = nextHealth == 0L
                ? CombatantResourcePlanStatus.AppliedAndDefeated
                : CombatantResourcePlanStatus.Applied;
            return Apply(
                snapshot,
                request,
                status,
                nextHealth,
                snapshot.CurrentManaMicros,
                snapshot.ReservedManaMicros,
                nextLife,
                snapshot.RegenerationAccumulatorRemainder,
                CopyReservations(snapshot),
                -applied,
                0L,
                0L,
                nextHealth == 0L);
        }

        private static CombatantResourcePlanResult PlanHealing(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            CombatantResourcePlanStatus amountStatus = ValidateAmount(
                request.AmountMicros,
                CombatScalarKind.Healing);
            if (amountStatus != CombatantResourcePlanStatus.Applied)
            {
                return amountStatus == CombatantResourcePlanStatus.NoChangeZero
                    ? RecordNoChange(snapshot, request, amountStatus)
                    : Reject(amountStatus, snapshot);
            }

            if (snapshot.LifeState != CombatantLifeState.Alive)
            {
                return Reject(CombatantResourcePlanStatus.NotAlive, snapshot);
            }

            if (snapshot.CurrentHealthMicros == snapshot.MaxHealthMicros)
            {
                return RecordNoChange(
                    snapshot,
                    request,
                    CombatantResourcePlanStatus.NoChangeAtMaximum);
            }

            long capacity = snapshot.MaxHealthMicros - snapshot.CurrentHealthMicros;
            long applied = Math.Min(request.AmountMicros, capacity);
            return Apply(
                snapshot,
                request,
                CombatantResourcePlanStatus.Applied,
                snapshot.CurrentHealthMicros + applied,
                snapshot.CurrentManaMicros,
                snapshot.ReservedManaMicros,
                snapshot.LifeState,
                snapshot.RegenerationAccumulatorRemainder,
                CopyReservations(snapshot),
                applied,
                0L,
                0L,
                false);
        }

        private static CombatantResourcePlanResult PlanReserveMana(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            CombatantResourcePlanStatus amountStatus = ValidateAmount(
                request.AmountMicros,
                CombatScalarKind.Mana);
            if (amountStatus != CombatantResourcePlanStatus.Applied)
            {
                return amountStatus == CombatantResourcePlanStatus.NoChangeZero
                    ? RecordNoChange(snapshot, request, amountStatus)
                    : Reject(amountStatus, snapshot);
            }

            if (snapshot.LifeState != CombatantLifeState.Alive)
            {
                return Reject(CombatantResourcePlanStatus.NotAlive, snapshot);
            }

            if (snapshot.Reservations.Any(
                    reservation => reservation.ActionId == request.CorrelationId) ||
                HasReservationHistory(snapshot, request.CorrelationId))
            {
                return Reject(CombatantResourcePlanStatus.CorrelationConflict, snapshot);
            }

            if (snapshot.Reservations.Count >= CombatantResourceSnapshot.MaximumReservations)
            {
                return Reject(CombatantResourcePlanStatus.CapacityReached, snapshot);
            }

            if (snapshot.AvailableManaMicros < request.AmountMicros)
            {
                return Reject(CombatantResourcePlanStatus.InsufficientMana, snapshot);
            }

            List<ManaReservationSnapshot> reservations = CopyReservations(snapshot);
            reservations.Add(new ManaReservationSnapshot(
                request.CorrelationId,
                request.AmountMicros,
                ManaReservationState.Reserved,
                request.OperationId));
            return Apply(
                snapshot,
                request,
                CombatantResourcePlanStatus.Applied,
                snapshot.CurrentHealthMicros,
                snapshot.CurrentManaMicros,
                snapshot.ReservedManaMicros + request.AmountMicros,
                snapshot.LifeState,
                snapshot.RegenerationAccumulatorRemainder,
                reservations,
                0L,
                0L,
                request.AmountMicros,
                false);
        }

        private static CombatantResourcePlanResult PlanCommitMana(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            if (snapshot.LifeState != CombatantLifeState.Alive)
            {
                return Reject(CombatantResourcePlanStatus.NotAlive, snapshot);
            }

            int reservationIndex = IndexOfReservation(snapshot, request.CorrelationId);
            if (reservationIndex < 0)
            {
                return Reject(
                    HasReservationFinalization(snapshot, request.CorrelationId)
                        ? CombatantResourcePlanStatus.ReservationAlreadyFinalized
                        : CombatantResourcePlanStatus.ReservationNotFound,
                    snapshot);
            }

            ManaReservationSnapshot reservation = snapshot.Reservations[reservationIndex];
            if (reservation.State != ManaReservationState.Reserved)
            {
                return Reject(
                    CombatantResourcePlanStatus.ReservationAlreadyFinalized,
                    snapshot);
            }

            if (snapshot.CurrentManaMicros < reservation.AmountMicros ||
                snapshot.ReservedManaMicros < reservation.AmountMicros)
            {
                return Reject(CombatantResourcePlanStatus.ArithmeticFailure, snapshot);
            }

            List<ManaReservationSnapshot> reservations = CopyReservations(snapshot);
            reservations.RemoveAt(reservationIndex);
            return Apply(
                snapshot,
                request,
                CombatantResourcePlanStatus.Applied,
                snapshot.CurrentHealthMicros,
                snapshot.CurrentManaMicros - reservation.AmountMicros,
                snapshot.ReservedManaMicros - reservation.AmountMicros,
                snapshot.LifeState,
                snapshot.RegenerationAccumulatorRemainder,
                reservations,
                0L,
                -reservation.AmountMicros,
                -reservation.AmountMicros,
                false);
        }

        private static CombatantResourcePlanResult PlanReleaseMana(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            if (!CombatPrimitiveValidation.IsStableId(request.Reason))
            {
                return Reject(CombatantResourcePlanStatus.InvalidRequest, snapshot);
            }

            int reservationIndex = IndexOfReservation(snapshot, request.CorrelationId);
            if (reservationIndex < 0)
            {
                return Reject(
                    HasReservationFinalization(snapshot, request.CorrelationId)
                        ? CombatantResourcePlanStatus.ReservationAlreadyFinalized
                        : CombatantResourcePlanStatus.ReservationNotFound,
                    snapshot);
            }

            ManaReservationSnapshot reservation = snapshot.Reservations[reservationIndex];
            if (reservation.State != ManaReservationState.Reserved)
            {
                return Reject(
                    CombatantResourcePlanStatus.ReservationAlreadyFinalized,
                    snapshot);
            }

            if (snapshot.ReservedManaMicros < reservation.AmountMicros)
            {
                return Reject(CombatantResourcePlanStatus.ArithmeticFailure, snapshot);
            }

            List<ManaReservationSnapshot> reservations = CopyReservations(snapshot);
            reservations.RemoveAt(reservationIndex);
            return Apply(
                snapshot,
                request,
                CombatantResourcePlanStatus.Applied,
                snapshot.CurrentHealthMicros,
                snapshot.CurrentManaMicros,
                snapshot.ReservedManaMicros - reservation.AmountMicros,
                snapshot.LifeState,
                snapshot.RegenerationAccumulatorRemainder,
                reservations,
                0L,
                0L,
                -reservation.AmountMicros,
                false);
        }

        private static CombatantResourcePlanResult PlanRestoreMana(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            CombatantResourcePlanStatus amountStatus = ValidateAmount(
                request.AmountMicros,
                CombatScalarKind.Mana);
            if (amountStatus != CombatantResourcePlanStatus.Applied)
            {
                return amountStatus == CombatantResourcePlanStatus.NoChangeZero
                    ? RecordNoChange(snapshot, request, amountStatus)
                    : Reject(amountStatus, snapshot);
            }

            if (snapshot.LifeState != CombatantLifeState.Alive)
            {
                return Reject(CombatantResourcePlanStatus.NotAlive, snapshot);
            }

            if (snapshot.CurrentManaMicros == snapshot.MaxManaMicros)
            {
                if (snapshot.RegenerationAccumulatorRemainder != 0L)
                {
                    return Apply(
                        snapshot,
                        request,
                        CombatantResourcePlanStatus.Applied,
                        snapshot.CurrentHealthMicros,
                        snapshot.CurrentManaMicros,
                        snapshot.ReservedManaMicros,
                        snapshot.LifeState,
                        0L,
                        CopyReservations(snapshot),
                        0L,
                        0L,
                        0L,
                        false);
                }

                return RecordNoChange(
                    snapshot,
                    request,
                    CombatantResourcePlanStatus.NoChangeAtMaximum);
            }

            long capacity = snapshot.MaxManaMicros - snapshot.CurrentManaMicros;
            long applied = Math.Min(request.AmountMicros, capacity);
            long nextMana = snapshot.CurrentManaMicros + applied;
            return Apply(
                snapshot,
                request,
                CombatantResourcePlanStatus.Applied,
                snapshot.CurrentHealthMicros,
                nextMana,
                snapshot.ReservedManaMicros,
                snapshot.LifeState,
                nextMana == snapshot.MaxManaMicros
                    ? 0L
                    : snapshot.RegenerationAccumulatorRemainder,
                CopyReservations(snapshot),
                0L,
                applied,
                0L,
                false);
        }

        private static CombatantResourcePlanResult PlanRegenerateMana(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            if (snapshot.LifeState != CombatantLifeState.Alive)
            {
                return Reject(CombatantResourcePlanStatus.NotAlive, snapshot);
            }

            if (!request.RegenerationAllowed)
            {
                return Reject(
                    CombatantResourcePlanStatus.RegenerationProhibited,
                    snapshot);
            }

            if (request.EncounterClockRevision.IsDefault)
            {
                return Reject(
                    CombatantResourcePlanStatus.InvalidRequest,
                    snapshot);
            }

            CombatantResourcePlanStatus rateStatus = ValidateAmount(
                request.RegenerationRateMicrosPerSecond,
                CombatScalarKind.RegenerationRate);
            if (rateStatus != CombatantResourcePlanStatus.Applied &&
                rateStatus != CombatantResourcePlanStatus.NoChangeZero)
            {
                return Reject(rateStatus, snapshot);
            }

            CombatantResourcePlanStatus elapsedStatus = ValidateAmount(
                request.ElapsedDurationMicros,
                CombatScalarKind.Duration);
            if (elapsedStatus != CombatantResourcePlanStatus.Applied)
            {
                return Reject(
                    elapsedStatus ==
                        CombatantResourcePlanStatus.NoChangeZero
                        ? CombatantResourcePlanStatus.InvalidAmount
                        : elapsedStatus,
                    snapshot);
            }

            if (request.RegenerationRateMicrosPerSecond == 0L)
            {
                return RecordNoChange(
                    snapshot,
                    request,
                    CombatantResourcePlanStatus.NoChangeZero);
            }

            if (snapshot.CurrentManaMicros == snapshot.MaxManaMicros)
            {
                if (snapshot.RegenerationAccumulatorRemainder != 0L)
                {
                    return Apply(
                        snapshot,
                        request,
                        CombatantResourcePlanStatus.Applied,
                        snapshot.CurrentHealthMicros,
                        snapshot.CurrentManaMicros,
                        snapshot.ReservedManaMicros,
                        snapshot.LifeState,
                        0L,
                        CopyReservations(snapshot),
                        0L,
                        0L,
                        0L,
                        false);
                }

                return RecordNoChange(
                    snapshot,
                    request,
                    CombatantResourcePlanStatus.NoChangeAtMaximum);
            }

            if (!TryCalculateRegeneration(
                    request.RegenerationRateMicrosPerSecond,
                    request.ElapsedDurationMicros,
                    snapshot.RegenerationAccumulatorRemainder,
                    out long generatedMana,
                    out long nextRemainder))
            {
                return Reject(CombatantResourcePlanStatus.ArithmeticFailure, snapshot);
            }

            long capacity = snapshot.MaxManaMicros - snapshot.CurrentManaMicros;
            long applied = Math.Min(generatedMana, capacity);
            if (generatedMana >= capacity)
            {
                nextRemainder = 0L;
            }

            if (applied == 0L &&
                nextRemainder == snapshot.RegenerationAccumulatorRemainder)
            {
                return RecordNoChange(
                    snapshot,
                    request,
                    CombatantResourcePlanStatus.NoChangeZero);
            }

            return Apply(
                snapshot,
                request,
                CombatantResourcePlanStatus.Applied,
                snapshot.CurrentHealthMicros,
                snapshot.CurrentManaMicros + applied,
                snapshot.ReservedManaMicros,
                snapshot.LifeState,
                nextRemainder,
                CopyReservations(snapshot),
                0L,
                applied,
                0L,
                false);
        }

        private static CombatantResourcePlanResult Apply(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request,
            CombatantResourcePlanStatus status,
            long nextHealth,
            long nextMana,
            long nextReservedMana,
            CombatantLifeState nextLife,
            long nextRegenerationRemainder,
            IList<ManaReservationSnapshot> reservations,
            long healthDelta,
            long manaDelta,
            long reservedManaDelta,
            bool emitDefeated)
        {
            if (snapshot.RevisionOrdinal == long.MaxValue)
            {
                return Reject(CombatantResourcePlanStatus.ArithmeticFailure, snapshot);
            }

            long nextRevisionOrdinal = snapshot.RevisionOrdinal + 1L;
            string nextRevision =
                "resource-r" +
                nextRevisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);
            var receipt = new CombatantResourceOperationReceipt(
                request,
                status,
                snapshot.Revision,
                nextRevision,
                healthDelta,
                manaDelta,
                reservedManaDelta);
            List<CombatantResourceOperationReceipt> operationReceipts =
                CopyOperationReceipts(snapshot);
            bool regeneration =
                request.Kind == CombatantResourceOperationKind.RegenerateMana;
            if (!regeneration)
            {
                operationReceipts.Add(receipt);
            }
            List<CombatantResourceOperationReceipt> regenerationReplayReceipts =
                CopyRegenerationReplayReceipts(snapshot);
            if (regeneration)
            {
                AddRegenerationReplayReceipt(
                    regenerationReplayReceipts,
                    receipt);
            }

            CombatantResourceSnapshot next = snapshot.With(
                nextHealth,
                nextMana,
                nextReservedMana,
                nextLife,
                nextRevisionOrdinal,
                nextRegenerationRemainder,
                reservations,
                operationReceipts,
                regeneration
                    ? snapshot.NextRegenerationTickOrdinal + 1L
                    : snapshot.NextRegenerationTickOrdinal,
                regeneration ? receipt : snapshot.LatestRegenerationReceipt,
                regenerationReplayReceipts);

            var events = new List<CombatantResourceEventReceipt>(emitDefeated ? 2 : 1)
            {
                new CombatantResourceEventReceipt(
                    CombatantResourceEventKind.ResourcesChanged,
                    request.OperationId,
                    request.EncounterSessionId,
                    request.EncounterAttemptId,
                    request.ActorParticipantId,
                    request.CorrelationId,
                    request.SourceParticipantId,
                    request.SourceBehaviorId,
                    snapshot.Revision,
                    next.Revision,
                    healthDelta,
                    manaDelta,
                    reservedManaDelta)
            };
            if (emitDefeated)
            {
                events.Add(new CombatantResourceEventReceipt(
                    CombatantResourceEventKind.CombatantDefeated,
                    request.OperationId,
                    request.EncounterSessionId,
                    request.EncounterAttemptId,
                    request.ActorParticipantId,
                    request.CorrelationId,
                    request.SourceParticipantId,
                    request.SourceBehaviorId,
                    snapshot.Revision,
                    next.Revision,
                    healthDelta,
                    manaDelta,
                    reservedManaDelta));
            }

            return new CombatantResourcePlanResult(status, next, receipt, null, events);
        }

        private static CombatantResourcePlanResult RecordNoChange(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request,
            CombatantResourcePlanStatus status)
        {
            bool regeneration =
                request.Kind == CombatantResourceOperationKind.RegenerateMana;
            if (regeneration && snapshot.RevisionOrdinal == long.MaxValue)
            {
                return Reject(CombatantResourcePlanStatus.ArithmeticFailure, snapshot);
            }

            long nextRevisionOrdinal = regeneration
                ? snapshot.RevisionOrdinal + 1L
                : snapshot.RevisionOrdinal;
            string nextRevision =
                "resource-r" +
                nextRevisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);
            var receipt = new CombatantResourceOperationReceipt(
                request,
                status,
                snapshot.Revision,
                nextRevision,
                0L,
                0L,
                0L);
            List<CombatantResourceOperationReceipt> operationReceipts =
                CopyOperationReceipts(snapshot);
            if (!regeneration)
            {
                operationReceipts.Add(receipt);
            }
            List<CombatantResourceOperationReceipt> regenerationReplayReceipts =
                CopyRegenerationReplayReceipts(snapshot);
            if (regeneration)
            {
                AddRegenerationReplayReceipt(
                    regenerationReplayReceipts,
                    receipt);
            }

            CombatantResourceSnapshot next = snapshot.With(
                snapshot.CurrentHealthMicros,
                snapshot.CurrentManaMicros,
                snapshot.ReservedManaMicros,
                snapshot.LifeState,
                nextRevisionOrdinal,
                snapshot.RegenerationAccumulatorRemainder,
                CopyReservations(snapshot),
                operationReceipts,
                regeneration
                    ? snapshot.NextRegenerationTickOrdinal + 1L
                    : snapshot.NextRegenerationTickOrdinal,
                regeneration ? receipt : snapshot.LatestRegenerationReceipt,
                regenerationReplayReceipts);
            return new CombatantResourcePlanResult(status, next, receipt, null, null);
        }

        private static CombatantResourcePlanStatus ValidateContext(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            if (request.OperationId.IsDefault ||
                request.EncounterSessionId.IsDefault ||
                request.EncounterAttemptId.IsDefault ||
                request.ActorParticipantId.IsDefault ||
                request.CorrelationId.IsDefault ||
                !CombatPrimitiveValidation.IsStableId(request.ExpectedResourceRevision))
            {
                return CombatantResourcePlanStatus.InvalidRequest;
            }

            bool requiresSource =
                request.Kind == CombatantResourceOperationKind.Damage ||
                request.Kind == CombatantResourceOperationKind.Healing;
            if (requiresSource
                    ? request.SourceParticipantId.IsDefault ||
                      request.SourceBehaviorId.IsDefault
                    : !request.SourceParticipantId.IsDefault ||
                      !request.SourceBehaviorId.IsDefault)
            {
                return CombatantResourcePlanStatus.InvalidRequest;
            }

            if (snapshot.EncounterSessionId != request.EncounterSessionId ||
                snapshot.EncounterAttemptId != request.EncounterAttemptId)
            {
                return CombatantResourcePlanStatus.WrongEncounter;
            }

            if (snapshot.ActorParticipantId != request.ActorParticipantId)
            {
                return CombatantResourcePlanStatus.WrongActor;
            }

            if (!StringComparer.Ordinal.Equals(
                    snapshot.Revision,
                    request.ExpectedResourceRevision))
            {
                return CombatantResourcePlanStatus.StaleRevision;
            }

            return CombatantResourcePlanStatus.Applied;
        }

        private static CombatantResourcePlanStatus ValidateAmount(
            long amountMicros,
            CombatScalarKind kind)
        {
            if (amountMicros < 0L)
            {
                return CombatantResourcePlanStatus.NegativeAmount;
            }

            if (!CombatTechnicalLimits.TryGetMaximumMicros(
                    kind,
                    out long maximumMicros))
            {
                return CombatantResourcePlanStatus.InvalidAmount;
            }

            if (amountMicros > maximumMicros)
            {
                return CombatantResourcePlanStatus.AmountAboveMaximum;
            }

            return amountMicros == 0L
                ? CombatantResourcePlanStatus.NoChangeZero
                : CombatantResourcePlanStatus.Applied;
        }

        private static bool TryCalculateRegeneration(
            long rateMicrosPerSecond,
            long elapsedDurationMicros,
            long existingRemainder,
            out long generatedMana,
            out long nextRemainder)
        {
            generatedMana = 0L;
            nextRemainder = existingRemainder;
            if (existingRemainder < 0L ||
                existingRemainder >= CombatTechnicalLimits.MicrosPerUnit)
            {
                return false;
            }

            try
            {
                long wholeSeconds =
                    elapsedDurationMicros / CombatTechnicalLimits.MicrosPerUnit;
                long fractionalDuration =
                    elapsedDurationMicros % CombatTechnicalLimits.MicrosPerUnit;
                long fromWholeSeconds = checked(rateMicrosPerSecond * wholeSeconds);
                long fractionalProduct = checked(
                    rateMicrosPerSecond * fractionalDuration + existingRemainder);
                generatedMana = checked(
                    fromWholeSeconds +
                    fractionalProduct / CombatTechnicalLimits.MicrosPerUnit);
                nextRemainder =
                    fractionalProduct % CombatTechnicalLimits.MicrosPerUnit;
                return generatedMana >= 0L;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static int IndexOfReservation(
            CombatantResourceSnapshot snapshot,
            CombatStableId actionId)
        {
            for (int index = 0; index < snapshot.Reservations.Count; index++)
            {
                if (snapshot.Reservations[index].ActionId == actionId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool HasReservationHistory(
            CombatantResourceSnapshot snapshot,
            CombatStableId actionId)
        {
            return snapshot.OperationReceipts.Any(
                receipt =>
                    receipt.Request.CorrelationId == actionId &&
                    (receipt.Request.Kind ==
                         CombatantResourceOperationKind.ReserveMana ||
                     receipt.Request.Kind ==
                         CombatantResourceOperationKind.CommitManaReservation ||
                     receipt.Request.Kind ==
                         CombatantResourceOperationKind.ReleaseManaReservation));
        }

        private static bool HasReservationFinalization(
            CombatantResourceSnapshot snapshot,
            CombatStableId actionId)
        {
            return snapshot.OperationReceipts.Any(
                receipt =>
                    receipt.Request.CorrelationId == actionId &&
                    (receipt.Request.Kind ==
                         CombatantResourceOperationKind.CommitManaReservation ||
                     receipt.Request.Kind ==
                         CombatantResourceOperationKind.ReleaseManaReservation));
        }

        private static List<ManaReservationSnapshot> CopyReservations(
            CombatantResourceSnapshot snapshot)
        {
            return new List<ManaReservationSnapshot>(snapshot.Reservations);
        }

        private static List<CombatantResourceOperationReceipt> CopyOperationReceipts(
            CombatantResourceSnapshot snapshot)
        {
            return new List<CombatantResourceOperationReceipt>(
                snapshot.OperationReceipts);
        }

        private static List<CombatantResourceOperationReceipt>
            CopyRegenerationReplayReceipts(
                CombatantResourceSnapshot snapshot)
        {
            return new List<CombatantResourceOperationReceipt>(
                snapshot.RegenerationReplayReceipts);
        }

        private static void AddRegenerationReplayReceipt(
            IList<CombatantResourceOperationReceipt> receipts,
            CombatantResourceOperationReceipt receipt)
        {
            if (receipts.Count ==
                CombatantResourceSnapshot.MaximumRegenerationReplayReceipts)
            {
                receipts.RemoveAt(0);
            }

            receipts.Add(receipt);
        }

        private static bool HasGuaranteedOperationCapacity(
            CombatantResourceSnapshot snapshot,
            CombatantResourceOperationRequest request)
        {
            long retainedAndReserved =
                (long)snapshot.OperationReceipts.Count +
                snapshot.Reservations.Count;
            switch (request.Kind)
            {
                case CombatantResourceOperationKind.ReserveMana:
                    return retainedAndReserved + 2L <=
                           CombatantResourceSnapshot.MaximumOperationReceipts;
                case CombatantResourceOperationKind.CommitManaReservation:
                case CombatantResourceOperationKind.ReleaseManaReservation:
                    if (IndexOfReservation(snapshot, request.CorrelationId) < 0)
                    {
                        // Let the terminal no-op return NotFound/AlreadyFinalized.
                        return true;
                    }

                    return retainedAndReserved <=
                           CombatantResourceSnapshot.MaximumOperationReceipts;
                default:
                    return retainedAndReserved + 1L <=
                           CombatantResourceSnapshot.MaximumOperationReceipts;
            }
        }

        private static bool IsOrdinalBoundId(
            CombatStableId operationId,
            long ordinal)
        {
            if (operationId.IsDefault || ordinal < 0L)
            {
                return false;
            }

            string suffix =
                "-o" +
                ordinal.ToString("D19", CultureInfo.InvariantCulture);
            return operationId.Value.EndsWith(
                suffix,
                StringComparison.Ordinal);
        }

        private static bool HasTypedOperationNamespace(
            CombatantResourceOperationRequest request)
        {
            bool regenerationId = request.OperationId.Value.StartsWith(
                "regen-",
                StringComparison.Ordinal);
            return request.Kind ==
                   CombatantResourceOperationKind.RegenerateMana
                ? regenerationId
                : !regenerationId;
        }

        private static CombatantResourcePlanResult Reject(
            CombatantResourcePlanStatus status,
            CombatantResourceSnapshot snapshot)
        {
            return new CombatantResourcePlanResult(status, snapshot, null, null, null);
        }
    }

    public enum CombatantStatePlanStatus
    {
        Applied = 0,
        DuplicateExact = 1,
        CorrelationConflict = 2,
        InvalidRequest = 3,
        WrongEncounter = 4,
        WrongActor = 5,
        StaleRevision = 6,
        StaleTransition = 7,
        OutOfOrderTransition = 8,
        ProhibitedLifeTransition = 9,
        ProhibitedControlTransition = 10,
        IncoherentTerminalState = 11,
        ControlOwnerConflict = 12,
        TerminalState = 13,
        ArithmeticFailure = 14,
        /// <summary>
        /// No mutation or events were produced. The owning adapter must recover
        /// by reconciling against the current authoritative combatant state.
        /// </summary>
        ReplayWindowExpired = 15
    }

    public enum CombatantStateEventKind
    {
        LifeStateChanged = 0,
        ControlStateChanged = 1,
        ControlOwnerChanged = 2,
        Disposed = 3
    }

    public sealed class CombatantStateTransitionRequest
    {
        public CombatantStateTransitionRequest(
            CombatStableId transitionId,
            long transitionOrdinal,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatantLifeState targetLifeState,
            CombatantControlState targetControlState,
            CombatStableId expectedControlOwnerId,
            CombatStableId nextControlOwnerId,
            string expectedRevision)
        {
            TransitionId = transitionId;
            TransitionOrdinal = transitionOrdinal;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            TargetLifeState = targetLifeState;
            TargetControlState = targetControlState;
            ExpectedControlOwnerId = expectedControlOwnerId;
            NextControlOwnerId = nextControlOwnerId;
            ExpectedRevision = expectedRevision ?? string.Empty;
        }

        public CombatStableId TransitionId { get; }
        public long TransitionOrdinal { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatantLifeState TargetLifeState { get; }
        public CombatantControlState TargetControlState { get; }
        public CombatStableId ExpectedControlOwnerId { get; }
        public CombatStableId NextControlOwnerId { get; }
        public string ExpectedRevision { get; }

        internal bool PayloadEquals(CombatantStateTransitionRequest other)
        {
            return other != null &&
                   TransitionId == other.TransitionId &&
                   TransitionOrdinal == other.TransitionOrdinal &&
                   EncounterSessionId == other.EncounterSessionId &&
                   EncounterAttemptId == other.EncounterAttemptId &&
                   ActorParticipantId == other.ActorParticipantId &&
                   TargetLifeState == other.TargetLifeState &&
                   TargetControlState == other.TargetControlState &&
                   ExpectedControlOwnerId == other.ExpectedControlOwnerId &&
                   NextControlOwnerId == other.NextControlOwnerId &&
                   StringComparer.Ordinal.Equals(
                       ExpectedRevision,
                       other.ExpectedRevision);
        }
    }

    public sealed class CombatantStateTransitionReceipt
    {
        internal CombatantStateTransitionReceipt(
            CombatantStateTransitionRequest request,
            CombatantLifeState beforeLifeState,
            CombatantControlState beforeControlState,
            CombatStableId beforeControlOwnerId,
            string beforeRevision,
            string afterRevision)
        {
            Request = request;
            BeforeLifeState = beforeLifeState;
            BeforeControlState = beforeControlState;
            BeforeControlOwnerId = beforeControlOwnerId;
            BeforeRevision = beforeRevision ?? string.Empty;
            AfterRevision = afterRevision ?? string.Empty;
        }

        public CombatantStateTransitionRequest Request { get; }
        public CombatantLifeState BeforeLifeState { get; }
        public CombatantControlState BeforeControlState { get; }
        public CombatStableId BeforeControlOwnerId { get; }
        public string BeforeRevision { get; }
        public string AfterRevision { get; }
    }

    public sealed class CombatantStateEventReceipt
    {
        internal CombatantStateEventReceipt(
            CombatantStateEventKind kind,
            CombatStableId transitionId,
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            string beforeRevision,
            string afterRevision)
        {
            Kind = kind;
            TransitionId = transitionId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            BeforeRevision = beforeRevision ?? string.Empty;
            AfterRevision = afterRevision ?? string.Empty;
        }

        public CombatantStateEventKind Kind { get; }
        public CombatStableId TransitionId { get; }
        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public string BeforeRevision { get; }
        public string AfterRevision { get; }
        public string Revision => AfterRevision;
    }

    public sealed class CombatantStateSnapshot
    {
        /// <summary>
        /// Bounded control/lifecycle replay horizon for low-memory devices.
        /// Older ordinals fail closed with ReplayWindowExpired and require
        /// owning-adapter current-state reconciliation.
        /// </summary>
        public const int MaximumTransitionReplayReceipts = 64;

        private static readonly IReadOnlyList<CombatantStateTransitionReceipt>
            EmptyTransitionReceipts =
                Array.AsReadOnly(new CombatantStateTransitionReceipt[0]);

        private CombatantStateSnapshot(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatantLifeState lifeState,
            CombatantControlState controlState,
            CombatStableId controlOwnerId,
            long revisionOrdinal,
            long nextTransitionOrdinal,
            CombatantStateTransitionReceipt latestTransitionReceipt,
            IList<CombatantStateTransitionReceipt> transitionReplayReceipts)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            LifeState = lifeState;
            ControlState = controlState;
            ControlOwnerId = controlOwnerId;
            RevisionOrdinal = revisionOrdinal;
            Revision =
                "combatant-state-r" +
                revisionOrdinal.ToString("D16", CultureInfo.InvariantCulture);
            NextTransitionOrdinal = nextTransitionOrdinal;
            LatestTransitionReceipt = latestTransitionReceipt;
            TransitionReplayReceipts = Freeze(
                transitionReplayReceipts,
                EmptyTransitionReceipts);
        }

        public CombatStableId EncounterSessionId { get; }
        public CombatStableId EncounterAttemptId { get; }
        public CombatStableId ActorParticipantId { get; }
        public CombatantLifeState LifeState { get; }
        public CombatantControlState ControlState { get; }
        public CombatStableId ControlOwnerId { get; }
        public long RevisionOrdinal { get; }
        public string Revision { get; }
        public long NextTransitionOrdinal { get; }
        public CombatantStateTransitionReceipt LatestTransitionReceipt { get; }
        public IReadOnlyList<CombatantStateTransitionReceipt>
            TransitionReplayReceipts { get; }

        public static bool TryCreate(
            CombatStableId encounterSessionId,
            CombatStableId encounterAttemptId,
            CombatStableId actorParticipantId,
            CombatStableId controlOwnerId,
            out CombatantStateSnapshot snapshot)
        {
            snapshot = null;
            if (encounterSessionId.IsDefault ||
                encounterAttemptId.IsDefault ||
                actorParticipantId.IsDefault ||
                controlOwnerId.IsDefault)
            {
                return false;
            }

            snapshot = new CombatantStateSnapshot(
                encounterSessionId,
                encounterAttemptId,
                actorParticipantId,
                CombatantLifeState.Uninitialized,
                CombatantControlState.Disabled,
                controlOwnerId,
                0L,
                0L,
                null,
                null);
            return true;
        }

        internal CombatantStateSnapshot With(
            CombatantStateTransitionRequest request,
            long revisionOrdinal,
            CombatantStateTransitionReceipt receipt)
        {
            var replayReceipts =
                new List<CombatantStateTransitionReceipt>(
                    TransitionReplayReceipts);
            if (replayReceipts.Count == MaximumTransitionReplayReceipts)
            {
                replayReceipts.RemoveAt(0);
            }

            replayReceipts.Add(receipt);
            return new CombatantStateSnapshot(
                EncounterSessionId,
                EncounterAttemptId,
                ActorParticipantId,
                request.TargetLifeState,
                request.TargetControlState,
                request.NextControlOwnerId,
                revisionOrdinal,
                request.TransitionOrdinal + 1L,
                receipt,
                replayReceipts);
        }

        private static IReadOnlyList<T> Freeze<T>(
            IList<T> source,
            IReadOnlyList<T> empty)
        {
            if (source == null || source.Count == 0)
            {
                return empty;
            }

            var copy = new T[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatantStatePlanResult
    {
        private static readonly IReadOnlyList<CombatantStateEventReceipt> EmptyEvents =
            Array.AsReadOnly(new CombatantStateEventReceipt[0]);

        internal CombatantStatePlanResult(
            CombatantStatePlanStatus status,
            CombatantStateSnapshot snapshot,
            CombatantStateTransitionReceipt transitionReceipt,
            CombatantStateTransitionReceipt existingReceipt,
            IList<CombatantStateEventReceipt> events)
        {
            Status = status;
            Snapshot = snapshot;
            TransitionReceipt = transitionReceipt;
            ExistingReceipt = existingReceipt;
            if (events == null || events.Count == 0)
            {
                Events = EmptyEvents;
            }
            else
            {
                var copy = new CombatantStateEventReceipt[events.Count];
                events.CopyTo(copy, 0);
                Events = Array.AsReadOnly(copy);
            }
        }

        public CombatantStatePlanStatus Status { get; }
        public CombatantStateSnapshot Snapshot { get; }
        public CombatantStateTransitionReceipt TransitionReceipt { get; }
        public CombatantStateTransitionReceipt ExistingReceipt { get; }
        public IReadOnlyList<CombatantStateEventReceipt> Events { get; }
    }

    public sealed class CombatantLifeTransitionRule
    {
        internal CombatantLifeTransitionRule(
            CombatantLifeState from,
            CombatantLifeState to,
            bool allowed)
        {
            From = from;
            To = to;
            Allowed = allowed;
        }

        public CombatantLifeState From { get; }
        public CombatantLifeState To { get; }
        public bool Allowed { get; }
    }

    public static class CombatantStatePlanner
    {
        private static readonly IReadOnlyList<CombatantLifeTransitionRule>
            LifeMatrix = BuildLifeMatrix();

        public static IReadOnlyList<CombatantLifeTransitionRule>
            LifeTransitionMatrix => LifeMatrix;

        public static CombatantStatePlanResult Plan(
            CombatantStateSnapshot snapshot,
            CombatantStateTransitionRequest request)
        {
            if (snapshot == null || request == null)
            {
                return Reject(
                    CombatantStatePlanStatus.InvalidRequest,
                    snapshot);
            }

            if (request.TransitionOrdinal < snapshot.NextTransitionOrdinal)
            {
                    CombatantStateTransitionReceipt replay =
                        snapshot.TransitionReplayReceipts.FirstOrDefault(
                        retained =>
                            retained.Request.TransitionOrdinal ==
                            request.TransitionOrdinal);
                if (replay != null)
                {
                    return replay.Request.PayloadEquals(request)
                        ? new CombatantStatePlanResult(
                            CombatantStatePlanStatus.DuplicateExact,
                            snapshot,
                            null,
                            replay,
                            null)
                        : new CombatantStatePlanResult(
                            CombatantStatePlanStatus.CorrelationConflict,
                            snapshot,
                            null,
                            replay,
                            null);
                }

                return Reject(
                    CombatantStatePlanStatus.ReplayWindowExpired,
                    snapshot);
            }

            if (request.TransitionOrdinal > snapshot.NextTransitionOrdinal)
            {
                return Reject(
                    CombatantStatePlanStatus.OutOfOrderTransition,
                    snapshot);
            }

            if (!IsOrdinalBoundId(
                    request.TransitionId,
                    request.TransitionOrdinal))
            {
                return Reject(
                    CombatantStatePlanStatus.CorrelationConflict,
                    snapshot);
            }

            CombatantStatePlanStatus context = ValidateContext(snapshot, request);
            if (context != CombatantStatePlanStatus.Applied)
            {
                return Reject(context, snapshot);
            }

            if (snapshot.LifeState == CombatantLifeState.Disposed)
            {
                return Reject(
                    CombatantStatePlanStatus.TerminalState,
                    snapshot);
            }

            if (!IsCoherent(
                    request.TargetLifeState,
                    request.TargetControlState))
            {
                return Reject(
                    CombatantStatePlanStatus.IncoherentTerminalState,
                    snapshot);
            }

            if (snapshot.LifeState != request.TargetLifeState &&
                !IsLifeTransitionAllowed(
                    snapshot.LifeState,
                    request.TargetLifeState))
            {
                return Reject(
                    CombatantStatePlanStatus.ProhibitedLifeTransition,
                    snapshot);
            }

            if (!IsControlTransitionAllowed(
                    snapshot.LifeState,
                    snapshot.ControlState,
                    request.TargetLifeState,
                    request.TargetControlState))
            {
                return Reject(
                    CombatantStatePlanStatus.ProhibitedControlTransition,
                    snapshot);
            }

            bool lifeChanged = snapshot.LifeState != request.TargetLifeState;
            bool controlChanged =
                snapshot.ControlState != request.TargetControlState;
            bool ownerChanged =
                snapshot.ControlOwnerId != request.NextControlOwnerId;
            if (!lifeChanged && !controlChanged && !ownerChanged)
            {
                return Reject(
                    CombatantStatePlanStatus.InvalidRequest,
                    snapshot);
            }

            if (snapshot.RevisionOrdinal == long.MaxValue ||
                snapshot.NextTransitionOrdinal == long.MaxValue)
            {
                return Reject(
                    CombatantStatePlanStatus.ArithmeticFailure,
                    snapshot);
            }

            long nextRevisionOrdinal = snapshot.RevisionOrdinal + 1L;
            string nextRevision =
                "combatant-state-r" +
                nextRevisionOrdinal.ToString(
                    "D16",
                    CultureInfo.InvariantCulture);
            var receipt = new CombatantStateTransitionReceipt(
                request,
                snapshot.LifeState,
                snapshot.ControlState,
                snapshot.ControlOwnerId,
                snapshot.Revision,
                nextRevision);
            CombatantStateSnapshot next = snapshot.With(
                request,
                nextRevisionOrdinal,
                receipt);
            var events = new List<CombatantStateEventReceipt>(4);
            if (lifeChanged)
            {
                events.Add(new CombatantStateEventReceipt(
                    CombatantStateEventKind.LifeStateChanged,
                    request.TransitionId,
                    request.EncounterSessionId,
                    request.EncounterAttemptId,
                    request.ActorParticipantId,
                    snapshot.Revision,
                    next.Revision));
            }

            if (controlChanged)
            {
                events.Add(new CombatantStateEventReceipt(
                    CombatantStateEventKind.ControlStateChanged,
                    request.TransitionId,
                    request.EncounterSessionId,
                    request.EncounterAttemptId,
                    request.ActorParticipantId,
                    snapshot.Revision,
                    next.Revision));
            }

            if (ownerChanged)
            {
                events.Add(new CombatantStateEventReceipt(
                    CombatantStateEventKind.ControlOwnerChanged,
                    request.TransitionId,
                    request.EncounterSessionId,
                    request.EncounterAttemptId,
                    request.ActorParticipantId,
                    snapshot.Revision,
                    next.Revision));
            }

            if (request.TargetLifeState == CombatantLifeState.Disposed)
            {
                events.Add(new CombatantStateEventReceipt(
                    CombatantStateEventKind.Disposed,
                    request.TransitionId,
                    request.EncounterSessionId,
                    request.EncounterAttemptId,
                    request.ActorParticipantId,
                    snapshot.Revision,
                    next.Revision));
            }

            return new CombatantStatePlanResult(
                CombatantStatePlanStatus.Applied,
                next,
                receipt,
                null,
                events);
        }

        public static bool IsLifeTransitionAllowed(
            CombatantLifeState from,
            CombatantLifeState to)
        {
            if (!Enum.IsDefined(typeof(CombatantLifeState), from) ||
                !Enum.IsDefined(typeof(CombatantLifeState), to))
            {
                return false;
            }

            return (from == CombatantLifeState.Uninitialized &&
                    to == CombatantLifeState.Alive) ||
                   (from == CombatantLifeState.Alive &&
                    (to == CombatantLifeState.Defeated ||
                     to == CombatantLifeState.Disposed)) ||
                   (from == CombatantLifeState.Defeated &&
                    to == CombatantLifeState.Disposed);
        }

        public static bool IsControlTransitionAllowed(
            CombatantLifeState fromLife,
            CombatantControlState fromControl,
            CombatantLifeState toLife,
            CombatantControlState toControl)
        {
            if (!Enum.IsDefined(typeof(CombatantLifeState), fromLife) ||
                !Enum.IsDefined(typeof(CombatantLifeState), toLife) ||
                !Enum.IsDefined(typeof(CombatantControlState), fromControl) ||
                !Enum.IsDefined(typeof(CombatantControlState), toControl) ||
                !IsCoherent(fromLife, fromControl) ||
                !IsCoherent(toLife, toControl))
            {
                return false;
            }

            if (fromLife != toLife)
            {
                return IsLifeTransitionAllowed(fromLife, toLife);
            }

            if (fromLife == CombatantLifeState.Alive)
            {
                return IsAliveControl(fromControl) &&
                       IsAliveControl(toControl);
            }

            return fromLife != CombatantLifeState.Disposed &&
                   fromControl == toControl;
        }

        private static CombatantStatePlanStatus ValidateContext(
            CombatantStateSnapshot snapshot,
            CombatantStateTransitionRequest request)
        {
            if (request.TransitionId.IsDefault ||
                request.TransitionOrdinal < 0L ||
                request.EncounterSessionId.IsDefault ||
                request.EncounterAttemptId.IsDefault ||
                request.ActorParticipantId.IsDefault ||
                request.ExpectedControlOwnerId.IsDefault ||
                request.NextControlOwnerId.IsDefault ||
                !Enum.IsDefined(
                    typeof(CombatantLifeState),
                    request.TargetLifeState) ||
                !Enum.IsDefined(
                    typeof(CombatantControlState),
                    request.TargetControlState) ||
                !CombatPrimitiveValidation.IsStableId(request.ExpectedRevision))
            {
                return CombatantStatePlanStatus.InvalidRequest;
            }

            if (snapshot.EncounterSessionId != request.EncounterSessionId ||
                snapshot.EncounterAttemptId != request.EncounterAttemptId)
            {
                return CombatantStatePlanStatus.WrongEncounter;
            }

            if (snapshot.ActorParticipantId != request.ActorParticipantId)
            {
                return CombatantStatePlanStatus.WrongActor;
            }

            if (snapshot.ControlOwnerId != request.ExpectedControlOwnerId)
            {
                return CombatantStatePlanStatus.ControlOwnerConflict;
            }

            if (!StringComparer.Ordinal.Equals(
                    snapshot.Revision,
                    request.ExpectedRevision))
            {
                return CombatantStatePlanStatus.StaleRevision;
            }

            return CombatantStatePlanStatus.Applied;
        }

        private static bool IsCoherent(
            CombatantLifeState lifeState,
            CombatantControlState controlState)
        {
            switch (lifeState)
            {
                case CombatantLifeState.Uninitialized:
                    return controlState == CombatantControlState.Disabled;
                case CombatantLifeState.Alive:
                    return IsAliveControl(controlState);
                case CombatantLifeState.Defeated:
                    return controlState == CombatantControlState.Defeated;
                case CombatantLifeState.Disposed:
                    return controlState == CombatantControlState.Disposed;
                default:
                    return false;
            }
        }

        private static bool IsAliveControl(CombatantControlState controlState)
        {
            return controlState == CombatantControlState.Disabled ||
                   controlState == CombatantControlState.Manual ||
                   controlState == CombatantControlState.Assist ||
                   controlState == CombatantControlState.Auto ||
                   controlState == CombatantControlState.EncounterLocked ||
                   controlState == CombatantControlState.ActionLocked;
        }

        private static bool IsOrdinalBoundId(
            CombatStableId transitionId,
            long ordinal)
        {
            if (transitionId.IsDefault || ordinal < 0L)
            {
                return false;
            }

            string suffix =
                "-o" +
                ordinal.ToString("D19", CultureInfo.InvariantCulture);
            return transitionId.Value.EndsWith(
                suffix,
                StringComparison.Ordinal);
        }

        private static IReadOnlyList<CombatantLifeTransitionRule>
            BuildLifeMatrix()
        {
            CombatantLifeState[] states =
                (CombatantLifeState[])Enum.GetValues(typeof(CombatantLifeState));
            var rules = new List<CombatantLifeTransitionRule>(
                states.Length * states.Length);
            for (int fromIndex = 0; fromIndex < states.Length; fromIndex++)
            {
                for (int toIndex = 0; toIndex < states.Length; toIndex++)
                {
                    rules.Add(new CombatantLifeTransitionRule(
                        states[fromIndex],
                        states[toIndex],
                        IsLifeTransitionAllowed(
                            states[fromIndex],
                            states[toIndex])));
                }
            }

            return Array.AsReadOnly(rules.ToArray());
        }

        private static CombatantStatePlanResult Reject(
            CombatantStatePlanStatus status,
            CombatantStateSnapshot snapshot)
        {
            return new CombatantStatePlanResult(
                status,
                snapshot,
                null,
                null,
                null);
        }
    }
}
