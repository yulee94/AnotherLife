using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.ChampionMode.C1
{
    public enum BossGuardState
    {
        Stable = 0,
        Depleted = 1,
        Broken = 2,
        Recovering = 3
    }

    public enum BossEnrageState
    {
        Dormant = 0,
        TriggeredByHealth = 1,
        TriggeredByTime = 2,
        Active = 3
    }

    public enum BossStateOperationKind
    {
        Damage = 0,
        GuardDamage = 1,
        AdvanceEncounterClock = 2,
        ActivateEnrage = 3,
        CompleteBreakRecovery = 4,
        Dispose = 5
    }

    public enum BossStateTransitionStatus
    {
        Initialized = 0,
        Applied = 1,
        AppliedAndDefeated = 2,
        DuplicateExact = 3,
        CorrelationConflict = 4,
        NoChangeZero = 5,
        NoChangeTerminal = 6,
        NoChangeAlreadyBroken = 7,
        NoChangeNotReady = 8,
        RejectedInvalidPolicy = 9,
        RejectedInvalidState = 10,
        RejectedInvalidRequest = 11,
        RejectedNegativeAmount = 12,
        RejectedInvalidAmount = 13,
        RejectedStaleRevision = 14,
        RejectedWrongEncounter = 15,
        ArithmeticFailure = 16,
        CapacityReached = 17,
        ReplayWindowExpired = 18
    }

    public sealed class BossPhaseDefinition
    {
        public BossPhaseDefinition(
            string phaseId,
            long enterAtOrBelowHealthRatioMicros,
            long attackMultiplierMicros)
        {
            PhaseId = phaseId ?? string.Empty;
            EnterAtOrBelowHealthRatioMicros =
                enterAtOrBelowHealthRatioMicros;
            AttackMultiplierMicros = attackMultiplierMicros;
        }

        public string PhaseId { get; }
        public long EnterAtOrBelowHealthRatioMicros { get; }
        public long AttackMultiplierMicros { get; }
    }

    /// <summary>
    /// C1 state-only policy: health, guard/break, phase, enrage, and deterministic
    /// attack composition. Armor, abilities/behavior, targeting, reward binding,
    /// presentation, catalog provenance, source revision, and raw SHA-256 remain
    /// mandatory fields of the later C2 BossCombatProfile source contract; this
    /// planner neither substitutes nor silently defaults those authorities.
    /// </summary>
    public sealed class BossStatePolicy
    {
        public BossStatePolicy(
            string bossProfileId,
            string policyVersion,
            long maxHealthMicros,
            long maxGuardMicros,
            long breakDurationMicros,
            bool healthEnrageEnabled,
            long healthEnrageRatioMicros,
            bool timedEnrageEnabled,
            long timedEnrageAtMicros,
            long baseAttackPowerMicros,
            long enrageAttackMultiplierMicros,
            IList<BossPhaseDefinition> phases)
        {
            BossProfileId = bossProfileId ?? string.Empty;
            PolicyVersion = policyVersion ?? string.Empty;
            MaxHealthMicros = maxHealthMicros;
            MaxGuardMicros = maxGuardMicros;
            BreakDurationMicros = breakDurationMicros;
            HealthEnrageEnabled = healthEnrageEnabled;
            HealthEnrageRatioMicros = healthEnrageRatioMicros;
            TimedEnrageEnabled = timedEnrageEnabled;
            TimedEnrageAtMicros = timedEnrageAtMicros;
            BaseAttackPowerMicros = baseAttackPowerMicros;
            EnrageAttackMultiplierMicros = enrageAttackMultiplierMicros;
            PhaseInputCount = phases?.Count ?? 0;
            Phases = FreezeBounded(
                phases,
                BossStatePlanner.MaximumPhases + 1);
        }

        public string BossProfileId { get; }
        public string PolicyVersion { get; }
        public long MaxHealthMicros { get; }
        public long MaxGuardMicros { get; }
        public long BreakDurationMicros { get; }
        public bool HealthEnrageEnabled { get; }
        public long HealthEnrageRatioMicros { get; }
        public bool TimedEnrageEnabled { get; }
        public long TimedEnrageAtMicros { get; }
        public long BaseAttackPowerMicros { get; }
        public long EnrageAttackMultiplierMicros { get; }
        public int PhaseInputCount { get; }
        public IReadOnlyList<BossPhaseDefinition> Phases { get; }

        private static IReadOnlyList<BossPhaseDefinition> FreezeBounded(
            IList<BossPhaseDefinition> values,
            int maximumCopyCount)
        {
            if (values == null)
            {
                return Array.AsReadOnly(new BossPhaseDefinition[0]);
            }

            int count = Math.Min(values.Count, maximumCopyCount);
            var copy = new BossPhaseDefinition[count];
            for (int index = 0; index < count; index++)
            {
                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public sealed class BossStateSnapshot
    {
        internal BossStateSnapshot(
            string bossProfileId,
            string policyFingerprint,
            string encounterSessionId,
            string encounterAttemptId,
            string participantId,
            CombatantLifeState lifeState,
            long currentHealthMicros,
            long currentGuardMicros,
            BossGuardState guardState,
            long breakRecoveryAtMicros,
            BossEnrageState enrageState,
            int phaseIndex,
            string phaseId,
            long effectiveAttackPowerMicros,
            long encounterElapsedMicros,
            long revision)
            : this(
                bossProfileId,
                policyFingerprint,
                encounterSessionId,
                encounterAttemptId,
                participantId,
                lifeState,
                currentHealthMicros,
                currentGuardMicros,
                guardState,
                breakRecoveryAtMicros,
                enrageState,
                phaseIndex,
                phaseId,
                effectiveAttackPowerMicros,
                encounterElapsedMicros,
                revision,
                0,
                BossStatePlanner.EmptyRetainedOperationHash,
                new BossStateOperationReceipt[0],
                0L,
                BossStatePlanner.EmptyClockReplayHash,
                null)
        {
        }

        private BossStateSnapshot(
            string bossProfileId,
            string policyFingerprint,
            string encounterSessionId,
            string encounterAttemptId,
            string participantId,
            CombatantLifeState lifeState,
            long currentHealthMicros,
            long currentGuardMicros,
            BossGuardState guardState,
            long breakRecoveryAtMicros,
            BossEnrageState enrageState,
            int phaseIndex,
            string phaseId,
            long effectiveAttackPowerMicros,
            long encounterElapsedMicros,
            long revision,
            int retainedOperationCount,
            string retainedOperationHash,
            IList<BossStateOperationReceipt> retainedOperationReceipts,
            long clockReplaySequence,
            string clockReplayHash,
            BossStateOperationReceipt clockReplayReceipt)
        {
            BossProfileId = bossProfileId ?? string.Empty;
            PolicyFingerprint = policyFingerprint ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            ParticipantId = participantId ?? string.Empty;
            LifeState = lifeState;
            CurrentHealthMicros = currentHealthMicros;
            CurrentGuardMicros = currentGuardMicros;
            GuardState = guardState;
            BreakRecoveryAtMicros = breakRecoveryAtMicros;
            EnrageState = enrageState;
            PhaseIndex = phaseIndex;
            PhaseId = phaseId ?? string.Empty;
            EffectiveAttackPowerMicros = effectiveAttackPowerMicros;
            EncounterElapsedMicros = encounterElapsedMicros;
            Revision = revision;
            RetainedOperationCount = retainedOperationCount;
            RetainedOperationHash = retainedOperationHash ?? string.Empty;
            RetainedOperationReceipts = Array.AsReadOnly(
                (retainedOperationReceipts ??
                 new BossStateOperationReceipt[0]).ToArray());
            ClockReplaySequence = clockReplaySequence;
            ClockReplayHash = clockReplayHash ?? string.Empty;
            ClockReplayReceipt = clockReplayReceipt;
        }

        public string BossProfileId { get; }
        public string PolicyFingerprint { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string ParticipantId { get; }
        public CombatantLifeState LifeState { get; }
        public long CurrentHealthMicros { get; }
        public long CurrentGuardMicros { get; }
        public BossGuardState GuardState { get; }
        public long BreakRecoveryAtMicros { get; }
        public BossEnrageState EnrageState { get; }
        public int PhaseIndex { get; }
        public string PhaseId { get; }
        public long EffectiveAttackPowerMicros { get; }
        public long EncounterElapsedMicros { get; }
        public long Revision { get; }
        public int RetainedOperationCount { get; }
        public string RetainedOperationHash { get; }
        public IReadOnlyList<BossStateOperationReceipt>
            RetainedOperationReceipts { get; }
        public long ClockReplaySequence { get; }
        public string ClockReplayHash { get; }
        public BossStateOperationReceipt ClockReplayReceipt { get; }

        internal static BossStateSnapshot WithRetainedOperations(
            BossStateSnapshot source,
            int retainedOperationCount,
            string retainedOperationHash,
            IList<BossStateOperationReceipt> retainedOperationReceipts)
        {
            return WithReplayAuthority(
                source,
                retainedOperationCount,
                retainedOperationHash,
                retainedOperationReceipts,
                source.ClockReplaySequence,
                source.ClockReplayHash,
                source.ClockReplayReceipt);
        }

        internal static BossStateSnapshot WithReplayAuthority(
            BossStateSnapshot source,
            int retainedOperationCount,
            string retainedOperationHash,
            IList<BossStateOperationReceipt> retainedOperationReceipts,
            long clockReplaySequence,
            string clockReplayHash,
            BossStateOperationReceipt clockReplayReceipt)
        {
            return new BossStateSnapshot(
                source.BossProfileId,
                source.PolicyFingerprint,
                source.EncounterSessionId,
                source.EncounterAttemptId,
                source.ParticipantId,
                source.LifeState,
                source.CurrentHealthMicros,
                source.CurrentGuardMicros,
                source.GuardState,
                source.BreakRecoveryAtMicros,
                source.EnrageState,
                source.PhaseIndex,
                source.PhaseId,
                source.EffectiveAttackPowerMicros,
                source.EncounterElapsedMicros,
                source.Revision,
                retainedOperationCount,
                retainedOperationHash,
                retainedOperationReceipts,
                clockReplaySequence,
                clockReplayHash,
                clockReplayReceipt);
        }
    }

    public sealed class BossStateOperationRequest
    {
        public BossStateOperationRequest(
            string operationId,
            string encounterSessionId,
            string encounterAttemptId,
            string sourceActionOrOperationId,
            string sourceParticipantId,
            string sourceBehaviorId,
            BossStateOperationKind kind,
            long amountMicros,
            long atEncounterMicros,
            long expectedRevision)
        {
            OperationId = operationId ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            SourceActionOrOperationId =
                sourceActionOrOperationId ?? string.Empty;
            SourceParticipantId =
                sourceParticipantId ?? string.Empty;
            SourceBehaviorId = sourceBehaviorId ?? string.Empty;
            Kind = kind;
            AmountMicros = amountMicros;
            AtEncounterMicros = atEncounterMicros;
            ExpectedRevision = expectedRevision;
        }

        public string OperationId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string SourceActionOrOperationId { get; }
        public string SourceParticipantId { get; }
        public string SourceBehaviorId { get; }
        public BossStateOperationKind Kind { get; }
        public long AmountMicros { get; }
        public long AtEncounterMicros { get; }
        public long ExpectedRevision { get; }
    }

    public sealed class BossStateOperationReceipt
    {
        internal BossStateOperationReceipt(
            BossStateOperationRequest request,
            string requestFingerprint,
            BossStateTransitionStatus status,
            long beforeRevision,
            long afterRevision)
        {
            Request = request;
            OperationId = request?.OperationId ?? string.Empty;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            Status = status;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            BeforeRetainedOperationCount = -1;
            AfterRetainedOperationCount = -1;
            BeforeRetainedOperationHash = string.Empty;
            AfterRetainedOperationHash = string.Empty;
            BeforeClockReplaySequence = -1L;
            AfterClockReplaySequence = -1L;
            BeforeClockReplayHash = string.Empty;
            AfterClockReplayHash = string.Empty;
            PolicyFingerprint = string.Empty;
            BeforeStateFingerprint = string.Empty;
            AfterStateFingerprint = string.Empty;
            IsPlannerIssued = false;
        }

        private BossStateOperationReceipt(
            BossStateOperationRequest request,
            string requestFingerprint,
            BossStateTransitionStatus status,
            BossStateSnapshot before,
            BossStateSnapshot after,
            bool isPlannerIssued)
        {
            Request = request;
            OperationId = request?.OperationId ?? string.Empty;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            Status = status;
            BeforeRevision = before.Revision;
            AfterRevision = after.Revision;
            BeforeRetainedOperationCount =
                before.RetainedOperationCount;
            AfterRetainedOperationCount =
                after.RetainedOperationCount;
            BeforeRetainedOperationHash =
                before.RetainedOperationHash;
            AfterRetainedOperationHash =
                after.RetainedOperationHash;
            BeforeClockReplaySequence = before.ClockReplaySequence;
            AfterClockReplaySequence = after.ClockReplaySequence;
            BeforeClockReplayHash = before.ClockReplayHash;
            AfterClockReplayHash = after.ClockReplayHash;
            PolicyFingerprint = before.PolicyFingerprint;
            BeforeStateFingerprint =
                BossStatePlanner.StateFingerprint(before);
            AfterStateFingerprint =
                BossStatePlanner.StateFingerprint(after);
            IsPlannerIssued = isPlannerIssued;
        }

        internal static BossStateOperationReceipt
            CreatePlannerIssued(
                BossStateOperationRequest request,
                string requestFingerprint,
                BossStateTransitionStatus status,
                BossStateSnapshot before,
                BossStateSnapshot after)
        {
            return new BossStateOperationReceipt(
                request,
                requestFingerprint,
                status,
                before,
                after,
                true);
        }

        public BossStateOperationRequest Request { get; }
        public string OperationId { get; }
        public string RequestFingerprint { get; }
        public BossStateTransitionStatus Status { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public int BeforeRetainedOperationCount { get; }
        public int AfterRetainedOperationCount { get; }
        public string BeforeRetainedOperationHash { get; }
        public string AfterRetainedOperationHash { get; }
        public long BeforeClockReplaySequence { get; }
        public long AfterClockReplaySequence { get; }
        public string BeforeClockReplayHash { get; }
        public string AfterClockReplayHash { get; }
        public string PolicyFingerprint { get; }
        public string BeforeStateFingerprint { get; }
        public string AfterStateFingerprint { get; }
        internal bool IsPlannerIssued { get; }
    }

    public enum BossStateTechnicalEventKind
    {
        StateInitialized = 0,
        HealthChanged = 1,
        GuardChanged = 2,
        BreakChanged = 3,
        PhaseChanged = 4,
        EnrageChanged = 5,
        ClockAdvanced = 6,
        Defeated = 7,
        Disposed = 8
    }

    public sealed class BossStateTechnicalEventReceipt
    {
        internal BossStateTechnicalEventReceipt(
            BossStateTechnicalEventKind kind,
            string eventName,
            string detailId,
            string bossProfileId,
            string policyFingerprint,
            string encounterSessionId,
            string encounterAttemptId,
            string bossParticipantId,
            string operationId,
            string sourceActionOrOperationId,
            string sourceParticipantId,
            string sourceBehaviorId,
            long beforeRevision,
            long afterRevision,
            int sequence)
        {
            Kind = kind;
            EventName = eventName ?? string.Empty;
            DetailId = detailId ?? string.Empty;
            BossProfileId = bossProfileId ?? string.Empty;
            PolicyFingerprint = policyFingerprint ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            BossParticipantId = bossParticipantId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SourceActionOrOperationId =
                sourceActionOrOperationId ?? string.Empty;
            SourceParticipantId = sourceParticipantId ?? string.Empty;
            SourceBehaviorId = sourceBehaviorId ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Sequence = sequence;
        }

        public BossStateTechnicalEventKind Kind { get; }
        public string EventName { get; }
        public string DetailId { get; }
        public string BossProfileId { get; }
        public string PolicyFingerprint { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string BossParticipantId { get; }
        public string OperationId { get; }
        public string SourceActionOrOperationId { get; }
        public string SourceParticipantId { get; }
        public string SourceBehaviorId { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public int Sequence { get; }
    }

    public sealed class BossStateTransitionPlan
    {
        internal BossStateTransitionPlan(
            BossStateTransitionStatus status,
            BossStateSnapshot before,
            BossStateSnapshot after,
            BossStateOperationReceipt receipt,
            IEnumerable<string> technicalEvents,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            Status = status;
            Before = before;
            After = after;
            Receipt = receipt;
            TechnicalEvents = Array.AsReadOnly(
                (technicalEvents ?? Enumerable.Empty<string>()).ToArray());
            TechnicalEventReceipts =
                BossStatePlanner.CreateTechnicalEventReceipts(
                    before,
                    after,
                    receipt?.Request,
                    TechnicalEvents);
            Diagnostics = CombatDiagnosticOrdering.Order(diagnostics);
        }

        public BossStateTransitionStatus Status { get; }
        public BossStateSnapshot Before { get; }
        public BossStateSnapshot After { get; }
        public BossStateOperationReceipt Receipt { get; }
        public IReadOnlyList<string> TechnicalEvents { get; }
        public IReadOnlyList<BossStateTechnicalEventReceipt>
            TechnicalEventReceipts { get; }
        public IReadOnlyList<CombatDiagnostic> Diagnostics { get; }
        public bool AdvancesDomainRevision =>
            Before != null &&
            After != null &&
            Before.Revision != After.Revision;
        public bool RetainsCorrelation =>
            Receipt != null &&
            Status != BossStateTransitionStatus.DuplicateExact;
        public bool UpdatesSnapshot =>
            Before != null &&
            After != null &&
            (Before.Revision != After.Revision ||
             Before.RetainedOperationCount !=
                 After.RetainedOperationCount ||
             !StringComparer.Ordinal.Equals(
                 Before.RetainedOperationHash,
                 After.RetainedOperationHash));
        public bool MutatesState => UpdatesSnapshot;
    }

    public static class BossStatePlanner
    {
        public const string CurrentPolicyVersion = "combat.boss.state.c1.v1";
        public const int MaximumPhases = 32;
        public const int MaximumReplayReceipts = 4096;

        private const long RatioOneMicros =
            CombatTechnicalLimits.MicrosPerUnit;
        internal static string EmptyRetainedOperationHash { get; } =
            ComputeSha256(new[]
            {
                "boss.retained-operation-ledger.c1.genesis"
            });
        internal static string EmptyClockReplayHash { get; } =
            ComputeSha256(new[]
            {
                "boss.clock-replay-window.c1.genesis"
            });

        internal static IReadOnlyList
            <BossStateTechnicalEventReceipt>
            CreateTechnicalEventReceipts(
                BossStateSnapshot before,
                BossStateSnapshot after,
                BossStateOperationRequest request,
                IEnumerable<string> eventNames)
        {
            BossStateSnapshot context = after ?? before;
            if (context == null)
            {
                return Array.AsReadOnly(
                    new BossStateTechnicalEventReceipt[0]);
            }

            string[] names =
                (eventNames ?? Enumerable.Empty<string>()).ToArray();
            var receipts = new BossStateTechnicalEventReceipt[
                names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index] ?? string.Empty;
                BossStateTechnicalEventKind kind;
                string detail = string.Empty;
                if (StringComparer.Ordinal.Equals(
                        name,
                        "BossStateInitialized"))
                {
                    kind =
                        BossStateTechnicalEventKind.StateInitialized;
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "BossHealthChanged"))
                {
                    kind =
                        BossStateTechnicalEventKind.HealthChanged;
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "BossGuardChanged"))
                {
                    kind =
                        BossStateTechnicalEventKind.GuardChanged;
                }
                else if (name.StartsWith(
                             "BossBreakChanged:",
                             StringComparison.Ordinal))
                {
                    kind =
                        BossStateTechnicalEventKind.BreakChanged;
                    detail = name.Substring(
                        "BossBreakChanged:".Length);
                }
                else if (name.StartsWith(
                             "BossPhaseChanged:",
                             StringComparison.Ordinal))
                {
                    kind =
                        BossStateTechnicalEventKind.PhaseChanged;
                    detail = name.Substring(
                        "BossPhaseChanged:".Length);
                }
                else if (name.StartsWith(
                             "BossEnrageChanged:",
                             StringComparison.Ordinal))
                {
                    kind =
                        BossStateTechnicalEventKind.EnrageChanged;
                    detail = name.Substring(
                        "BossEnrageChanged:".Length);
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "BossClockAdvanced"))
                {
                    kind =
                        BossStateTechnicalEventKind.ClockAdvanced;
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "BossDefeated"))
                {
                    kind = BossStateTechnicalEventKind.Defeated;
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "BossDisposed"))
                {
                    kind = BossStateTechnicalEventKind.Disposed;
                }
                else
                {
                    throw new ArgumentException(
                        "Unknown boss technical event.",
                        nameof(eventNames));
                }

                receipts[index] =
                    new BossStateTechnicalEventReceipt(
                        kind,
                        name,
                        detail,
                        context.BossProfileId,
                        context.PolicyFingerprint,
                        context.EncounterSessionId,
                        context.EncounterAttemptId,
                        context.ParticipantId,
                        request?.OperationId ?? string.Empty,
                        request?.SourceActionOrOperationId ??
                            string.Empty,
                        request?.SourceParticipantId ??
                            string.Empty,
                        request?.SourceBehaviorId ??
                            string.Empty,
                        before?.Revision ?? context.Revision,
                        after?.Revision ?? context.Revision,
                        index);
            }

            return Array.AsReadOnly(receipts);
        }

        public static CombatValidationResult ValidatePolicy(BossStatePolicy policy)
        {
            var diagnostics = new List<CombatDiagnostic>();
            ValidatePolicy(policy, diagnostics);
            return new CombatValidationResult(diagnostics);
        }

        public static BossStateTransitionPlan CreateInitial(
            BossStatePolicy policy,
            string encounterSessionId,
            string encounterAttemptId,
            string participantId)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidatePolicy(policy, diagnostics))
            {
                return Plan(
                    BossStateTransitionStatus.RejectedInvalidPolicy,
                    null,
                    null,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!CombatPrimitiveValidation.IsStableId(encounterSessionId) ||
                !CombatPrimitiveValidation.IsStableId(encounterAttemptId) ||
                !CombatPrimitiveValidation.IsStableId(participantId))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-INVALID-IDENTITY",
                    "initialization.identity",
                    "Boss initialization requires stable encounter and participant IDs.",
                    policy));
                return Plan(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    null,
                    null,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!TryComposeAttack(
                    policy,
                    0,
                    BossEnrageState.Dormant,
                    out long effectiveAttack))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-ARITHMETIC",
                    "initialization.effectiveAttackPowerMicros",
                    "Boss attack composition exceeded its technical range.",
                    policy));
                return Plan(
                    BossStateTransitionStatus.ArithmeticFailure,
                    null,
                    null,
                    null,
                    new string[0],
                    diagnostics);
            }

            var state = new BossStateSnapshot(
                policy.BossProfileId,
                PolicyFingerprint(policy),
                encounterSessionId,
                encounterAttemptId,
                participantId,
                CombatantLifeState.Alive,
                policy.MaxHealthMicros,
                policy.MaxGuardMicros,
                BossGuardState.Stable,
                0L,
                BossEnrageState.Dormant,
                0,
                policy.Phases[0].PhaseId,
                effectiveAttack,
                0L,
                0L);
            return Plan(
                BossStateTransitionStatus.Initialized,
                null,
                state,
                null,
                new[] { "BossStateInitialized" },
                diagnostics);
        }

        public static BossStateTransitionPlan PlanTransition(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            IList<BossStateOperationReceipt> replayReceipts)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidatePolicy(policy, diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidPolicy,
                    current,
                    diagnostics);
            }

            if (!ValidateState(policy, current, diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidState,
                    current,
                    diagnostics);
            }

            if (!ValidateRequestIdentity(request, diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    current,
                    diagnostics);
            }

            if (!StringComparer.Ordinal.Equals(
                    current.EncounterSessionId,
                    request.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    current.EncounterAttemptId,
                    request.EncounterAttemptId))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-WRONG-ENCOUNTER",
                    "request.encounterAttemptId",
                    "Boss operation belongs to another encounter attempt.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedWrongEncounter,
                    current,
                    diagnostics);
            }

            if (replayReceipts != null &&
                replayReceipts.Count > MaximumReplayReceipts)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-REPLAY-LIMIT",
                    "replayReceipts",
                    "Boss replay receipt collection exceeds its technical maximum.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    current,
                    diagnostics);
            }

            if (!ValidateReplayReceipts(
                    replayReceipts,
                    policy,
                    current,
                    request,
                    diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    current,
                    diagnostics);
            }

            string fingerprint = Fingerprint(policy, request);
            BossStateOperationReceipt existing =
                current.RetainedOperationReceipts
                .FirstOrDefault(receipt =>
                    receipt != null &&
                    StringComparer.Ordinal.Equals(
                        receipt.OperationId,
                        request.OperationId));
            if (existing != null)
            {
                if (StringComparer.Ordinal.Equals(
                        existing.RequestFingerprint,
                        fingerprint))
                {
                    return Plan(
                        BossStateTransitionStatus.DuplicateExact,
                        current,
                        current,
                        existing,
                        new string[0],
                        diagnostics);
                }

                diagnostics.Add(Error(
                    "AL-BOSS-STATE-CORRELATION-CONFLICT",
                    "request.operationId",
                    "Boss operation ID was reused with changed input.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.CorrelationConflict,
                    current,
                    diagnostics);
            }

            if (current.RetainedOperationCount >=
                MaximumReplayReceipts)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-REPLAY-CAPACITY",
                    "replayReceipts",
                    "Boss replay ledger is at capacity and cannot retain a new operation.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.CapacityReached,
                    current,
                    diagnostics);
            }

            if (request.ExpectedRevision != current.Revision)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-STALE-REVISION",
                    "request.expectedRevision",
                    "Boss operation expected a stale state revision.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedStaleRevision,
                    current,
                    diagnostics);
            }

            if (!Enum.IsDefined(
                    typeof(BossStateOperationKind),
                    request.Kind) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    request.AtEncounterMicros,
                    CombatScalarKind.Duration,
                    false))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-INVALID-REQUEST",
                    "request",
                    "Boss operation kind or encounter time is invalid.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    current,
                    diagnostics);
            }

            bool carriesAmount =
                request.Kind == BossStateOperationKind.Damage ||
                request.Kind == BossStateOperationKind.GuardDamage;
            if (carriesAmount && request.AmountMicros < 0L)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-NEGATIVE-DAMAGE",
                    "request.amountMicros",
                    "Boss damage cannot be negative.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedNegativeAmount,
                    current,
                    diagnostics);
            }

            if (carriesAmount &&
                !CombatPrimitiveValidation.IsMicrosInRange(
                    request.AmountMicros,
                    CombatScalarKind.Damage,
                    false))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-INVALID-DAMAGE",
                    "request.amountMicros",
                    "Boss damage exceeds its technical range.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidAmount,
                    current,
                    diagnostics);
            }

            if (!carriesAmount && request.AmountMicros != 0L)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-UNEXPECTED-AMOUNT",
                    "request.amountMicros",
                    "This boss operation kind requires an explicit zero amount.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    current,
                    diagnostics);
            }

            bool advancesClock =
                request.Kind == BossStateOperationKind.AdvanceEncounterClock;
            if ((advancesClock &&
                 request.AtEncounterMicros < current.EncounterElapsedMicros) ||
                (!advancesClock &&
                 request.AtEncounterMicros != current.EncounterElapsedMicros))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-INVALID-CLOCK",
                    "request.atEncounterMicros",
                    "Boss operation time does not match the monotonic encounter clock.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    current,
                    diagnostics);
            }

            if (current.LifeState == CombatantLifeState.Disposed ||
                (current.LifeState == CombatantLifeState.Defeated &&
                 request.Kind != BossStateOperationKind.Dispose))
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeTerminal,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            switch (request.Kind)
            {
                case BossStateOperationKind.Damage:
                    return PlanDamage(
                        policy,
                        current,
                        request,
                        fingerprint,
                        diagnostics);
                case BossStateOperationKind.GuardDamage:
                    return PlanGuardDamage(
                        policy,
                        current,
                        request,
                        fingerprint,
                        diagnostics);
                case BossStateOperationKind.AdvanceEncounterClock:
                    return PlanAdvanceClock(
                        policy,
                        current,
                        request,
                        fingerprint,
                        diagnostics);
                case BossStateOperationKind.ActivateEnrage:
                    return PlanActivateEnrage(
                        policy,
                        current,
                        request,
                        fingerprint,
                        diagnostics);
                case BossStateOperationKind.CompleteBreakRecovery:
                    return PlanCompleteBreakRecovery(
                        policy,
                        current,
                        request,
                        fingerprint,
                        diagnostics);
                case BossStateOperationKind.Dispose:
                    return PlanDispose(
                        policy,
                        current,
                        request,
                        fingerprint,
                        diagnostics);
                default:
                    return Rejected(
                        BossStateTransitionStatus.RejectedInvalidRequest,
                        current,
                        diagnostics);
            }
        }

        private static BossStateTransitionPlan PlanDamage(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            string fingerprint,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (request.AmountMicros < 0L)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-NEGATIVE-DAMAGE",
                    "request.amountMicros",
                    "Boss damage cannot be negative.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedNegativeAmount,
                    current,
                    diagnostics);
            }

            if (!CombatPrimitiveValidation.IsMicrosInRange(
                    request.AmountMicros,
                    CombatScalarKind.Damage,
                    false))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-INVALID-DAMAGE",
                    "request.amountMicros",
                    "Boss damage exceeds its technical range.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidAmount,
                    current,
                    diagnostics);
            }

            if (request.AmountMicros == 0L)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeZero,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (CannotAdvanceRevision(
                    policy,
                    current,
                    request,
                    diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            long healthAfter = request.AmountMicros >= current.CurrentHealthMicros
                ? 0L
                : current.CurrentHealthMicros - request.AmountMicros;
            var events = new List<string> { "BossHealthChanged" };
            if (healthAfter == 0L)
            {
                var defeated = Copy(
                    current,
                    lifeState: CombatantLifeState.Defeated,
                    currentHealthMicros: 0L,
                    revision: NextRevision(current.Revision));
                events.Add("BossDefeated");
                return Applied(
                    BossStateTransitionStatus.AppliedAndDefeated,
                    current,
                    defeated,
                    request,
                    fingerprint,
                    events,
                    diagnostics);
            }

            int phaseAfter = DeterminePhaseIndex(policy, healthAfter);
            for (int index = current.PhaseIndex + 1;
                 index <= phaseAfter;
                 index++)
            {
                events.Add(
                    "BossPhaseChanged:" +
                    policy.Phases[index].PhaseId);
            }

            BossEnrageState enrageAfter = current.EnrageState;
            if (enrageAfter == BossEnrageState.Dormant &&
                policy.HealthEnrageEnabled &&
                IsHealthAtOrBelowRatio(
                    policy,
                    healthAfter,
                    policy.HealthEnrageRatioMicros))
            {
                enrageAfter = BossEnrageState.TriggeredByHealth;
                events.Add("BossEnrageChanged:TriggeredByHealth");
            }

            if (!TryComposeAttack(
                    policy,
                    phaseAfter,
                    enrageAfter,
                    out long effectiveAttack))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-ARITHMETIC",
                    "state.effectiveAttackPowerMicros",
                    "Boss attack composition exceeded its technical range.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            var after = Copy(
                current,
                currentHealthMicros: healthAfter,
                enrageState: enrageAfter,
                phaseIndex: phaseAfter,
                phaseId: policy.Phases[phaseAfter].PhaseId,
                effectiveAttackPowerMicros: effectiveAttack,
                revision: NextRevision(current.Revision));
            return Applied(
                BossStateTransitionStatus.Applied,
                current,
                after,
                request,
                fingerprint,
                events,
                diagnostics);
        }

        private static BossStateTransitionPlan PlanGuardDamage(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            string fingerprint,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (request.AmountMicros < 0L)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-NEGATIVE-GUARD-DAMAGE",
                    "request.amountMicros",
                    "Boss guard damage cannot be negative.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedNegativeAmount,
                    current,
                    diagnostics);
            }

            if (!CombatPrimitiveValidation.IsMicrosInRange(
                    request.AmountMicros,
                    CombatScalarKind.Damage,
                    false))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-INVALID-GUARD-DAMAGE",
                    "request.amountMicros",
                    "Boss guard damage exceeds its technical range.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.RejectedInvalidAmount,
                    current,
                    diagnostics);
            }

            if (request.AmountMicros == 0L)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeZero,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (current.GuardState == BossGuardState.Broken ||
                current.GuardState == BossGuardState.Recovering)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeAlreadyBroken,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (CannotAdvanceRevision(
                    policy,
                    current,
                    request,
                    diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            long guardAfter = request.AmountMicros >= current.CurrentGuardMicros
                ? 0L
                : current.CurrentGuardMicros - request.AmountMicros;
            BossGuardState guardStateAfter = guardAfter == 0L
                ? BossGuardState.Broken
                : BossGuardState.Depleted;
            long recoveryAt = 0L;
            try
            {
                if (guardStateAfter == BossGuardState.Broken)
                {
                    recoveryAt = checked(
                        current.EncounterElapsedMicros +
                        policy.BreakDurationMicros);
                    if (!CombatPrimitiveValidation.IsMicrosInRange(
                            recoveryAt,
                            CombatScalarKind.Duration,
                            false))
                    {
                        throw new OverflowException();
                    }
                }
            }
            catch (OverflowException)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-ARITHMETIC",
                    "state.breakRecoveryAtMicros",
                    "Boss break recovery time exceeded its technical range.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            var after = Copy(
                current,
                currentGuardMicros: guardAfter,
                guardState: guardStateAfter,
                breakRecoveryAtMicros: recoveryAt,
                revision: NextRevision(current.Revision));
            var events = new List<string> { "BossGuardChanged" };
            if (current.GuardState != guardStateAfter)
            {
                events.Add(
                    "BossBreakChanged:" + guardStateAfter);
            }

            return Applied(
                BossStateTransitionStatus.Applied,
                current,
                after,
                request,
                fingerprint,
                events,
                diagnostics);
        }

        private static BossStateTransitionPlan PlanAdvanceClock(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            string fingerprint,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (request.AtEncounterMicros ==
                current.EncounterElapsedMicros)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeZero,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (CannotAdvanceRevision(
                    policy,
                    current,
                    request,
                    diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            var events = new List<string> { "BossClockAdvanced" };
            BossGuardState guardAfter = current.GuardState;
            if (guardAfter == BossGuardState.Broken &&
                request.AtEncounterMicros >=
                    current.BreakRecoveryAtMicros)
            {
                guardAfter = BossGuardState.Recovering;
                events.Add("BossBreakChanged:Recovering");
            }

            BossEnrageState enrageAfter = current.EnrageState;
            if (enrageAfter == BossEnrageState.Dormant &&
                policy.TimedEnrageEnabled &&
                request.AtEncounterMicros >=
                    policy.TimedEnrageAtMicros)
            {
                enrageAfter = BossEnrageState.TriggeredByTime;
                events.Add("BossEnrageChanged:TriggeredByTime");
            }

            if (!TryComposeAttack(
                    policy,
                    current.PhaseIndex,
                    enrageAfter,
                    out long effectiveAttack))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-ARITHMETIC",
                    "state.effectiveAttackPowerMicros",
                    "Boss attack composition exceeded its technical range.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            var after = Copy(
                current,
                guardState: guardAfter,
                enrageState: enrageAfter,
                effectiveAttackPowerMicros: effectiveAttack,
                encounterElapsedMicros: request.AtEncounterMicros,
                revision: NextRevision(current.Revision));
            return Applied(
                BossStateTransitionStatus.Applied,
                current,
                after,
                request,
                fingerprint,
                events,
                diagnostics);
        }

        private static BossStateTransitionPlan PlanActivateEnrage(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            string fingerprint,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (current.EnrageState == BossEnrageState.Dormant)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeNotReady,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (current.EnrageState == BossEnrageState.Active)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeTerminal,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (CannotAdvanceRevision(
                    policy,
                    current,
                    request,
                    diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            if (!TryComposeAttack(
                    policy,
                    current.PhaseIndex,
                    BossEnrageState.Active,
                    out long effectiveAttack))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-ARITHMETIC",
                    "state.effectiveAttackPowerMicros",
                    "Boss enrage attack composition exceeded its technical range.",
                    policy,
                    request));
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            var after = Copy(
                current,
                enrageState: BossEnrageState.Active,
                effectiveAttackPowerMicros: effectiveAttack,
                revision: NextRevision(current.Revision));
            return Applied(
                BossStateTransitionStatus.Applied,
                current,
                after,
                request,
                fingerprint,
                new[] { "BossEnrageChanged:Active" },
                diagnostics);
        }

        private static BossStateTransitionPlan PlanCompleteBreakRecovery(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            string fingerprint,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (current.GuardState != BossGuardState.Recovering)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeNotReady,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (CannotAdvanceRevision(
                    policy,
                    current,
                    request,
                    diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            var after = Copy(
                current,
                currentGuardMicros: policy.MaxGuardMicros,
                guardState: BossGuardState.Stable,
                breakRecoveryAtMicros: 0L,
                revision: NextRevision(current.Revision));
            return Applied(
                BossStateTransitionStatus.Applied,
                current,
                after,
                request,
                fingerprint,
                new[]
                {
                    "BossGuardChanged",
                    "BossBreakChanged:Stable"
                },
                diagnostics);
        }

        private static BossStateTransitionPlan PlanDispose(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            string fingerprint,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (current.LifeState == CombatantLifeState.Disposed)
            {
                return NoChange(
                    BossStateTransitionStatus.NoChangeTerminal,
                    current,
                    request,
                    fingerprint,
                    diagnostics);
            }

            if (CannotAdvanceRevision(
                    policy,
                    current,
                    request,
                    diagnostics))
            {
                return Rejected(
                    BossStateTransitionStatus.ArithmeticFailure,
                    current,
                    diagnostics);
            }

            var after = Copy(
                current,
                lifeState: CombatantLifeState.Disposed,
                revision: NextRevision(current.Revision));
            return Applied(
                BossStateTransitionStatus.Applied,
                current,
                after,
                request,
                fingerprint,
                new[] { "BossDisposed" },
                diagnostics);
        }

        private static bool ValidatePolicy(
            BossStatePolicy policy,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (policy == null)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-MISSING",
                    "policy",
                    "Boss state policy is null.",
                    null));
                return false;
            }

            bool valid = true;
            if (!CombatPrimitiveValidation.IsStableId(policy.BossProfileId) ||
                !CombatPrimitiveValidation.IsVersion(policy.PolicyVersion) ||
                !StringComparer.Ordinal.Equals(
                    policy.PolicyVersion,
                    CurrentPolicyVersion))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-INVALID-IDENTITY",
                    "policy.identity",
                    "Boss policy identity or version is invalid.",
                    policy));
                valid = false;
            }

            if (!CombatPrimitiveValidation.IsMicrosInRange(
                    policy.MaxHealthMicros,
                    CombatScalarKind.Health,
                    true) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    policy.MaxGuardMicros,
                    CombatScalarKind.Health,
                    true) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    policy.BreakDurationMicros,
                    CombatScalarKind.Duration,
                    true) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    policy.BaseAttackPowerMicros,
                    CombatScalarKind.AttackPower,
                    true) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    policy.EnrageAttackMultiplierMicros,
                    CombatScalarKind.Multiplier,
                    true))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-INVALID-NUMERIC",
                    "policy.numeric",
                    "Boss policy contains an invalid required numeric field.",
                    policy));
                valid = false;
            }

            if (policy.HealthEnrageEnabled &&
                (policy.HealthEnrageRatioMicros <= 0L ||
                 policy.HealthEnrageRatioMicros >= RatioOneMicros))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-INVALID-HEALTH-ENRAGE",
                    "policy.healthEnrageRatioMicros",
                    "Health enrage threshold must be strictly inside (0, 1).",
                    policy));
                valid = false;
            }
            else if (!policy.HealthEnrageEnabled &&
                     policy.HealthEnrageRatioMicros != 0L)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-DISABLED-HEALTH-ENRAGE",
                    "policy.healthEnrageRatioMicros",
                    "Disabled health enrage requires an explicit zero threshold.",
                    policy));
                valid = false;
            }

            if (policy.TimedEnrageEnabled &&
                !CombatPrimitiveValidation.IsMicrosInRange(
                    policy.TimedEnrageAtMicros,
                    CombatScalarKind.Duration,
                    true))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-INVALID-TIMED-ENRAGE",
                    "policy.timedEnrageAtMicros",
                    "Timed enrage threshold is invalid.",
                    policy));
                valid = false;
            }
            else if (!policy.TimedEnrageEnabled &&
                     policy.TimedEnrageAtMicros != 0L)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-DISABLED-TIMED-ENRAGE",
                    "policy.timedEnrageAtMicros",
                    "Disabled timed enrage requires an explicit zero threshold.",
                    policy));
                valid = false;
            }

            if (policy.PhaseInputCount == 0 ||
                policy.PhaseInputCount > MaximumPhases ||
                policy.Phases.Count != policy.PhaseInputCount)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-PROFILE-INVALID-PHASE-COUNT",
                    "policy.phases",
                    "Boss phase count is empty or exceeds its technical maximum.",
                    policy));
                valid = false;
            }

            var phaseIds = new HashSet<string>(StringComparer.Ordinal);
            long previousThreshold = RatioOneMicros + 1L;
            for (int index = 0; index < policy.Phases.Count; index++)
            {
                BossPhaseDefinition phase = policy.Phases[index];
                if (phase == null ||
                    !CombatPrimitiveValidation.IsStableId(phase.PhaseId) ||
                    !phaseIds.Add(phase.PhaseId) ||
                    phase.EnterAtOrBelowHealthRatioMicros < 0L ||
                    phase.EnterAtOrBelowHealthRatioMicros > RatioOneMicros ||
                    phase.EnterAtOrBelowHealthRatioMicros >= previousThreshold ||
                    !CombatPrimitiveValidation.IsMicrosInRange(
                        phase.AttackMultiplierMicros,
                        CombatScalarKind.Multiplier,
                        true))
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-PROFILE-INVALID-PHASE",
                        "policy.phases[" + index + "]",
                        "Boss phase is null, duplicated, unsorted, or numerically invalid.",
                        policy));
                    valid = false;
                    continue;
                }

                if (index == 0 &&
                    phase.EnterAtOrBelowHealthRatioMicros != RatioOneMicros)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-PROFILE-MISSING-INITIAL-PHASE",
                        "policy.phases[0]",
                        "First boss phase must cover the full-health ratio.",
                        policy));
                    valid = false;
                }

                previousThreshold =
                    phase.EnterAtOrBelowHealthRatioMicros;
            }

            if (valid)
            {
                for (int index = 0;
                     index < policy.Phases.Count;
                     index++)
                {
                    if (!TryComposeAttack(
                            policy,
                            index,
                            BossEnrageState.Dormant,
                            out _) ||
                        !TryComposeAttack(
                            policy,
                            index,
                            BossEnrageState.Active,
                            out _))
                    {
                        diagnostics.Add(Error(
                            "AL-BOSS-PROFILE-ATTACK-COMPOSITION",
                            "policy.phases[" + index + "]",
                            "Base, phase, and enrage attack composition exceeds its technical range.",
                            policy));
                        valid = false;
                        break;
                    }
                }
            }

            return valid;
        }

        private static bool ValidateState(
            BossStatePolicy policy,
            BossStateSnapshot state,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (state == null)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-MISSING",
                    "state",
                    "Boss state snapshot is null.",
                    policy));
                return false;
            }

            bool valid =
                StringComparer.Ordinal.Equals(
                    state.BossProfileId,
                    policy.BossProfileId) &&
                CombatPrimitiveValidation.IsSha256(
                    state.PolicyFingerprint) &&
                StringComparer.Ordinal.Equals(
                    state.PolicyFingerprint,
                    PolicyFingerprint(policy)) &&
                CombatPrimitiveValidation.IsStableId(
                    state.EncounterSessionId) &&
                CombatPrimitiveValidation.IsStableId(
                    state.EncounterAttemptId) &&
                CombatPrimitiveValidation.IsStableId(
                    state.ParticipantId) &&
                Enum.IsDefined(
                    typeof(CombatantLifeState),
                    state.LifeState) &&
                state.LifeState != CombatantLifeState.Uninitialized &&
                Enum.IsDefined(
                    typeof(BossGuardState),
                    state.GuardState) &&
                Enum.IsDefined(
                    typeof(BossEnrageState),
                    state.EnrageState) &&
                state.CurrentHealthMicros >= 0L &&
                state.CurrentHealthMicros <= policy.MaxHealthMicros &&
                state.CurrentGuardMicros >= 0L &&
                state.CurrentGuardMicros <= policy.MaxGuardMicros &&
                state.PhaseIndex >= 0 &&
                state.PhaseIndex < policy.Phases.Count &&
                StringComparer.Ordinal.Equals(
                    state.PhaseId,
                    policy.Phases[state.PhaseIndex].PhaseId) &&
                CombatPrimitiveValidation.IsMicrosInRange(
                    state.EffectiveAttackPowerMicros,
                    CombatScalarKind.AttackPower,
                    true) &&
                CombatPrimitiveValidation.IsMicrosInRange(
                    state.EncounterElapsedMicros,
                    CombatScalarKind.Duration,
                    false) &&
                state.Revision >= 0L &&
                state.RetainedOperationCount >= 0 &&
                state.RetainedOperationCount <=
                    MaximumReplayReceipts &&
                CombatPrimitiveValidation.IsSha256(
                    state.RetainedOperationHash) &&
                state.RetainedOperationReceipts != null &&
                state.RetainedOperationReceipts.Count ==
                    state.RetainedOperationCount;

            if (valid && state.RetainedOperationCount == 0)
            {
                valid = StringComparer.Ordinal.Equals(
                    state.RetainedOperationHash,
                    EmptyRetainedOperationHash);
            }
            else if (valid)
            {
                BossStateOperationReceipt tail =
                    state.RetainedOperationReceipts[
                        state.RetainedOperationCount - 1];
                valid =
                    tail != null &&
                    tail.IsPlannerIssued &&
                    tail.AfterRetainedOperationCount ==
                        state.RetainedOperationCount &&
                    StringComparer.Ordinal.Equals(
                        tail.AfterRetainedOperationHash,
                        state.RetainedOperationHash) &&
                    tail.AfterRevision == state.Revision &&
                    StringComparer.Ordinal.Equals(
                        tail.AfterStateFingerprint,
                        StateFingerprint(state));
            }

            valid &= state.LifeState == CombatantLifeState.Defeated
                ? state.CurrentHealthMicros == 0L
                : state.LifeState == CombatantLifeState.Alive
                    ? state.CurrentHealthMicros > 0L
                    : true;
            valid &= state.GuardState == BossGuardState.Stable
                ? state.CurrentGuardMicros == policy.MaxGuardMicros &&
                  state.BreakRecoveryAtMicros == 0L
                : state.GuardState == BossGuardState.Depleted
                    ? state.CurrentGuardMicros > 0L &&
                      state.CurrentGuardMicros < policy.MaxGuardMicros &&
                      state.BreakRecoveryAtMicros == 0L
                    : state.GuardState == BossGuardState.Broken
                        ? state.CurrentGuardMicros == 0L &&
                          state.BreakRecoveryAtMicros >
                            state.EncounterElapsedMicros
                        : state.CurrentGuardMicros == 0L &&
                          state.BreakRecoveryAtMicros > 0L &&
                          state.BreakRecoveryAtMicros <=
                            state.EncounterElapsedMicros;

            if (state.LifeState == CombatantLifeState.Alive)
            {
                int expectedPhase =
                    DeterminePhaseIndex(policy, state.CurrentHealthMicros);
                valid &= expectedPhase == state.PhaseIndex;
                bool healthEnrageReached =
                    policy.HealthEnrageEnabled &&
                    IsHealthAtOrBelowRatio(
                        policy,
                        state.CurrentHealthMicros,
                        policy.HealthEnrageRatioMicros);
                bool timedEnrageReached =
                    policy.TimedEnrageEnabled &&
                    state.EncounterElapsedMicros >=
                    policy.TimedEnrageAtMicros;
                valid &= state.EnrageState ==
                         BossEnrageState.Dormant
                    ? !healthEnrageReached &&
                      !timedEnrageReached
                    : state.EnrageState ==
                        BossEnrageState.TriggeredByHealth
                        ? healthEnrageReached
                        : state.EnrageState ==
                            BossEnrageState.TriggeredByTime
                            ? timedEnrageReached
                            : healthEnrageReached ||
                              timedEnrageReached;
                if (TryComposeAttack(
                        policy,
                        state.PhaseIndex,
                        state.EnrageState,
                        out long expectedAttack))
                {
                    valid &= expectedAttack ==
                        state.EffectiveAttackPowerMicros;
                }
                else
                {
                    valid = false;
                }
            }

            if (!valid)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-INVARIANT",
                    "state",
                    "Boss state snapshot violates its policy invariants.",
                    policy));
            }

            return valid;
        }

        private static bool ValidateRequestIdentity(
            BossStateOperationRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (request != null &&
                CombatPrimitiveValidation.IsStableId(request.OperationId) &&
                CombatPrimitiveValidation.IsStableId(
                    request.EncounterSessionId) &&
                CombatPrimitiveValidation.IsStableId(
                    request.EncounterAttemptId) &&
                Enum.IsDefined(
                    typeof(BossStateOperationKind),
                    request.Kind) &&
                IsValidSourceShape(request))
            {
                return true;
            }

            diagnostics.Add(new CombatDiagnostic(
                "AL-BOSS-STATE-INVALID-REQUEST",
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.BossState,
                "request.identity",
                "Boss operation request or stable identity is invalid.",
                CombatBlockScope.Action,
                policyVersion: CurrentPolicyVersion));
            return false;
        }

        private static bool IsValidSourceShape(
            BossStateOperationRequest request)
        {
            bool requiresSource =
                request.Kind == BossStateOperationKind.Damage ||
                request.Kind == BossStateOperationKind.GuardDamage;
            if (requiresSource)
            {
                return CombatPrimitiveValidation.IsStableId(
                           request.SourceActionOrOperationId) &&
                       CombatPrimitiveValidation.IsStableId(
                           request.SourceParticipantId) &&
                       CombatPrimitiveValidation.IsStableId(
                           request.SourceBehaviorId);
            }

            return string.IsNullOrEmpty(
                       request.SourceActionOrOperationId) &&
                   string.IsNullOrEmpty(
                       request.SourceParticipantId) &&
                   string.IsNullOrEmpty(
                       request.SourceBehaviorId);
        }

        private static int DeterminePhaseIndex(
            BossStatePolicy policy,
            long healthMicros)
        {
            int phaseIndex = 0;
            for (int index = 1; index < policy.Phases.Count; index++)
            {
                if (IsHealthAtOrBelowRatio(
                        policy,
                        healthMicros,
                        policy.Phases[index]
                            .EnterAtOrBelowHealthRatioMicros))
                {
                    phaseIndex = index;
                }
            }

            return phaseIndex;
        }

        private static bool IsHealthAtOrBelowRatio(
            BossStatePolicy policy,
            long healthMicros,
            long ratioMicros)
        {
            return
                (decimal)healthMicros * RatioOneMicros <=
                (decimal)policy.MaxHealthMicros * ratioMicros;
        }

        private static bool TryComposeAttack(
            BossStatePolicy policy,
            int phaseIndex,
            BossEnrageState enrageState,
            out long effectiveAttack)
        {
            effectiveAttack = 0L;
            try
            {
                decimal value =
                    ((decimal)policy.BaseAttackPowerMicros *
                     policy.Phases[phaseIndex].AttackMultiplierMicros) /
                    RatioOneMicros;
                if (enrageState == BossEnrageState.Active)
                {
                    value =
                        (value *
                         policy.EnrageAttackMultiplierMicros) /
                        RatioOneMicros;
                }

                value = decimal.Floor(value);
                if (value <= 0m ||
                    value >
                    CombatTechnicalLimits
                        .HealthManaDamageHealingAttackPowerMaximumMicros)
                {
                    return false;
                }

                effectiveAttack = decimal.ToInt64(value);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static long NextRevision(long revision)
        {
            return checked(revision + 1L);
        }

        private static bool CannotAdvanceRevision(
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (current.Revision != long.MaxValue)
            {
                return false;
            }

            diagnostics.Add(Error(
                "AL-BOSS-STATE-REVISION-OVERFLOW",
                "state.revision",
                "Boss state revision cannot advance beyond its technical maximum.",
                policy,
                request));
            return true;
        }

        private static string PolicyFingerprint(
            BossStatePolicy policy)
        {
            var fields = new List<string>
            {
                policy.BossProfileId,
                policy.PolicyVersion,
                CombatC1CanonicalFingerprint.Long(
                    policy.MaxHealthMicros),
                CombatC1CanonicalFingerprint.Long(
                    policy.MaxGuardMicros),
                CombatC1CanonicalFingerprint.Long(
                    policy.BreakDurationMicros),
                policy.HealthEnrageEnabled ? "1" : "0",
                CombatC1CanonicalFingerprint.Long(
                    policy.HealthEnrageRatioMicros),
                policy.TimedEnrageEnabled ? "1" : "0",
                CombatC1CanonicalFingerprint.Long(
                    policy.TimedEnrageAtMicros),
                CombatC1CanonicalFingerprint.Long(
                    policy.BaseAttackPowerMicros),
                CombatC1CanonicalFingerprint.Long(
                    policy.EnrageAttackMultiplierMicros),
                CombatC1CanonicalFingerprint.Integer(
                    policy.Phases.Count)
            };
            foreach (BossPhaseDefinition phase in policy.Phases)
            {
                fields.Add(phase.PhaseId);
                fields.Add(
                    CombatC1CanonicalFingerprint.Long(
                        phase.EnterAtOrBelowHealthRatioMicros));
                fields.Add(
                    CombatC1CanonicalFingerprint.Long(
                        phase.AttackMultiplierMicros));
            }

            return ComputeSha256(fields);
        }

        internal static string StateFingerprint(
            BossStateSnapshot state)
        {
            return ComputeSha256(new[]
            {
                state.BossProfileId,
                state.PolicyFingerprint,
                state.EncounterSessionId,
                state.EncounterAttemptId,
                state.ParticipantId,
                CombatC1CanonicalFingerprint.Integer(
                    (int)state.LifeState),
                CombatC1CanonicalFingerprint.Long(
                    state.CurrentHealthMicros),
                CombatC1CanonicalFingerprint.Long(
                    state.CurrentGuardMicros),
                CombatC1CanonicalFingerprint.Integer(
                    (int)state.GuardState),
                CombatC1CanonicalFingerprint.Long(
                    state.BreakRecoveryAtMicros),
                CombatC1CanonicalFingerprint.Integer(
                    (int)state.EnrageState),
                CombatC1CanonicalFingerprint.Integer(
                    state.PhaseIndex),
                state.PhaseId,
                CombatC1CanonicalFingerprint.Long(
                    state.EffectiveAttackPowerMicros),
                CombatC1CanonicalFingerprint.Long(
                    state.EncounterElapsedMicros),
                CombatC1CanonicalFingerprint.Long(
                    state.Revision),
                CombatC1CanonicalFingerprint.Integer(
                    state.RetainedOperationCount),
                state.RetainedOperationHash
            });
        }

        private static string Fingerprint(
            BossStatePolicy policy,
            BossStateOperationRequest request)
        {
            return ComputeSha256(new[]
            {
                PolicyFingerprint(policy),
                request.OperationId,
                request.EncounterSessionId,
                request.EncounterAttemptId,
                request.SourceActionOrOperationId,
                request.SourceParticipantId,
                request.SourceBehaviorId,
                CombatC1CanonicalFingerprint.Integer(
                    (int)request.Kind),
                CombatC1CanonicalFingerprint.Long(
                    request.AmountMicros),
                CombatC1CanonicalFingerprint.Long(
                    request.AtEncounterMicros),
                CombatC1CanonicalFingerprint.Long(
                    request.ExpectedRevision)
            });
        }

        private static string ComputeSha256(
            IEnumerable<string> fields)
        {
            var builder = new StringBuilder();
            foreach (string raw in fields ??
                     Enumerable.Empty<string>())
            {
                string value = raw ?? string.Empty;
                builder
                    .Append(value.Length.ToString(
                        CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value);
            }

            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
            }

            var hex = new StringBuilder(
                CombatTechnicalLimits.Sha256HexCharacters);
            foreach (byte value in digest)
            {
                hex.Append(value.ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static bool ValidateReplayReceipts(
            IList<BossStateOperationReceipt> replayReceipts,
            BossStatePolicy policy,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            IReadOnlyList<BossStateOperationReceipt> authoritative =
                current.RetainedOperationReceipts;
            if (replayReceipts == null || replayReceipts.Count == 0)
            {
                return true;
            }

            if (replayReceipts.Count != authoritative.Count)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-STATE-REPLAY-LEDGER-INCOMPLETE",
                    "replayReceipts",
                    "Supplied boss replay receipts must exactly match the snapshot-bound retained history.",
                    policy,
                    request));
                return false;
            }

            for (int index = 0; index < authoritative.Count; index++)
            {
                BossStateOperationReceipt expected =
                    authoritative[index];
                BossStateOperationReceipt supplied =
                    replayReceipts[index];
                if (!ReferenceEquals(expected, supplied))
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-STATE-REPLAY-LEDGER-MISMATCH",
                        "replayReceipts[" + index + "]",
                        "Supplied boss replay receipt is not the authoritative snapshot-bound receipt.",
                        policy,
                        request));
                    return false;
                }
            }

            return true;
        }

        private static bool IsRetainableReceiptStatus(
            BossStateTransitionStatus status)
        {
            return status == BossStateTransitionStatus.Applied ||
                   status ==
                       BossStateTransitionStatus.AppliedAndDefeated ||
                   status == BossStateTransitionStatus.NoChangeZero ||
                   status ==
                       BossStateTransitionStatus.NoChangeTerminal ||
                   status ==
                       BossStateTransitionStatus.NoChangeAlreadyBroken ||
                   status ==
                       BossStateTransitionStatus.NoChangeNotReady;
        }

        private static bool IsRetainableRequestShape(
            BossStateOperationRequest request)
        {
            if (!CombatPrimitiveValidation.IsStableId(
                    request.OperationId) ||
                !CombatPrimitiveValidation.IsStableId(
                    request.EncounterSessionId) ||
                !CombatPrimitiveValidation.IsStableId(
                    request.EncounterAttemptId) ||
                !Enum.IsDefined(
                    typeof(BossStateOperationKind),
                    request.Kind) ||
                !IsValidSourceShape(request) ||
                !CombatPrimitiveValidation.IsMicrosInRange(
                    request.AtEncounterMicros,
                    CombatScalarKind.Duration,
                    false))
            {
                return false;
            }

            if (request.Kind == BossStateOperationKind.Damage ||
                request.Kind == BossStateOperationKind.GuardDamage)
            {
                return CombatPrimitiveValidation.IsMicrosInRange(
                    request.AmountMicros,
                    CombatScalarKind.Damage,
                    false);
            }

            return request.AmountMicros == 0L;
        }

        private static BossStateSnapshot Copy(
            BossStateSnapshot source,
            CombatantLifeState? lifeState = null,
            long? currentHealthMicros = null,
            long? currentGuardMicros = null,
            BossGuardState? guardState = null,
            long? breakRecoveryAtMicros = null,
            BossEnrageState? enrageState = null,
            int? phaseIndex = null,
            string phaseId = null,
            long? effectiveAttackPowerMicros = null,
            long? encounterElapsedMicros = null,
            long? revision = null)
        {
            return new BossStateSnapshot(
                source.BossProfileId,
                source.PolicyFingerprint,
                source.EncounterSessionId,
                source.EncounterAttemptId,
                source.ParticipantId,
                lifeState ?? source.LifeState,
                currentHealthMicros ?? source.CurrentHealthMicros,
                currentGuardMicros ?? source.CurrentGuardMicros,
                guardState ?? source.GuardState,
                breakRecoveryAtMicros ?? source.BreakRecoveryAtMicros,
                enrageState ?? source.EnrageState,
                phaseIndex ?? source.PhaseIndex,
                phaseId ?? source.PhaseId,
                effectiveAttackPowerMicros ??
                    source.EffectiveAttackPowerMicros,
                encounterElapsedMicros ??
                    source.EncounterElapsedMicros,
                revision ?? source.Revision);
        }

        private static string NextRetainedOperationHash(
            BossStateSnapshot before,
            BossStateSnapshot after,
            BossStateOperationRequest request,
            string requestFingerprint,
            BossStateTransitionStatus status)
        {
            return ComputeSha256(new[]
            {
                "boss.retained-operation-ledger.c1.entry",
                before.RetainedOperationHash,
                CombatC1CanonicalFingerprint.Integer(
                    before.RetainedOperationCount + 1),
                request.OperationId,
                requestFingerprint,
                CombatC1CanonicalFingerprint.Integer((int)status),
                CombatC1CanonicalFingerprint.Long(before.Revision),
                CombatC1CanonicalFingerprint.Long(after.Revision)
            });
        }

        private static BossStateSnapshot BindRetainedOperation(
            BossStateSnapshot before,
            BossStateSnapshot after,
            BossStateOperationRequest request,
            string requestFingerprint,
            BossStateTransitionStatus status,
            out BossStateOperationReceipt receipt)
        {
            int count = checked(before.RetainedOperationCount + 1);
            string hash = NextRetainedOperationHash(
                before,
                after,
                request,
                requestFingerprint,
                status);
            BossStateSnapshot provisional =
                BossStateSnapshot.WithRetainedOperations(
                    after,
                    count,
                    hash,
                    before.RetainedOperationReceipts.ToArray());
            receipt =
                BossStateOperationReceipt.CreatePlannerIssued(
                    request,
                    requestFingerprint,
                    status,
                    before,
                    provisional);
            var retained =
                new BossStateOperationReceipt[count];
            for (int index = 0;
                 index < before.RetainedOperationCount;
                 index++)
            {
                retained[index] =
                    before.RetainedOperationReceipts[index];
            }

            retained[count - 1] = receipt;
            return BossStateSnapshot.WithRetainedOperations(
                provisional,
                count,
                hash,
                retained);
        }

        private static BossStateTransitionPlan Applied(
            BossStateTransitionStatus status,
            BossStateSnapshot before,
            BossStateSnapshot after,
            BossStateOperationRequest request,
            string fingerprint,
            IEnumerable<string> events,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            BossStateSnapshot retainedAfter = BindRetainedOperation(
                before,
                after,
                request,
                fingerprint,
                status,
                out BossStateOperationReceipt receipt);
            return Plan(
                status,
                before,
                retainedAfter,
                receipt,
                events,
                diagnostics);
        }

        private static BossStateTransitionPlan NoChange(
            BossStateTransitionStatus status,
            BossStateSnapshot current,
            BossStateOperationRequest request,
            string fingerprint,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            BossStateSnapshot retainedAfter = BindRetainedOperation(
                current,
                current,
                request,
                fingerprint,
                status,
                out BossStateOperationReceipt receipt);
            return Plan(
                status,
                current,
                retainedAfter,
                receipt,
                new string[0],
                diagnostics);
        }

        private static BossStateTransitionPlan Rejected(
            BossStateTransitionStatus status,
            BossStateSnapshot current,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            return Plan(
                status,
                current,
                current,
                null,
                new string[0],
                diagnostics);
        }

        private static BossStateTransitionPlan Plan(
            BossStateTransitionStatus status,
            BossStateSnapshot before,
            BossStateSnapshot after,
            BossStateOperationReceipt receipt,
            IEnumerable<string> events,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            return new BossStateTransitionPlan(
                status,
                before,
                after,
                receipt,
                events,
                diagnostics);
        }

        private static CombatDiagnostic Error(
            string code,
            string field,
            string message,
            BossStatePolicy policy,
            BossStateOperationRequest request = null)
        {
            return new CombatDiagnostic(
                code,
                CombatDiagnosticSeverity.Error,
                code.StartsWith(
                    "AL-BOSS-PROFILE-",
                    StringComparison.Ordinal)
                    ? CombatDiagnosticDomain.BossProfile
                    : CombatDiagnosticDomain.BossState,
                field,
                message,
                CombatBlockScope.Action |
                CombatBlockScope.Encounter,
                sourceDefinitionId: policy?.BossProfileId ??
                    string.Empty,
                encounterSessionId:
                    request?.EncounterSessionId ?? string.Empty,
                encounterAttemptId:
                    request?.EncounterAttemptId ?? string.Empty,
                policyVersion:
                    policy?.PolicyVersion ?? CurrentPolicyVersion);
        }
    }
}
