using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.ChampionMode.C1
{
    public enum ChampionEncounterRequestStatus
    {
        Resolved = 0,
        DuplicateExact = 1,
        CorrelationConflict = 2,
        RejectedInvalidDefinition = 3,
        RejectedInvalidRequest = 4,
        RejectedModeMismatch = 5,
        RejectedSourceMismatch = 6,
        RejectedRealmRequired = 7,
        RejectedRealmMismatch = 8,
        RejectedRewardProhibited = 9,
        RejectedRewardRequired = 10,
        RejectedQuestContextRequired = 11,
        RejectedQuestContextProhibited = 12,
        RejectedStaleRevision = 13,
        RejectedDevelopmentFallback = 14,
        RejectedCorrelationLimit = 15
    }

    public enum ChampionEncounterTerminalOutcome
    {
        None = 0,
        ChampionVictory = 1,
        ChampionDefeat = 2,
        Cancelled = 3,
        ValidationFailure = 4,
        RuntimeFailure = 5,
        RecoveryRequired = 6
    }

    public enum ChampionEncounterTransitionStatus
    {
        Applied = 0,
        DuplicateExact = 1,
        CorrelationConflict = 2,
        NoChangeTerminal = 3,
        RetryPlanned = 4,
        RejectedInvalidState = 5,
        RejectedInvalidRequest = 6,
        RejectedWrongEncounter = 7,
        RejectedStaleRevision = 8,
        RejectedInvalidClock = 9,
        RejectedTransition = 10,
        RejectedModePolicy = 11,
        RejectedTerminalConflict = 12,
        RejectedRetryIdentity = 13,
        RejectedRetryPolicy = 14,
        RejectedRecoveryPending = 15,
        ArithmeticFailure = 16,
        CapacityReached = 17
    }

    public sealed class ChampionEncounterDefinitionSnapshot
    {
        public ChampionEncounterDefinitionSnapshot(
            string gameId,
            string catalogSetId,
            string requiredProfileId,
            string encounterDefinitionId,
            string schemaVersion,
            string contentVersion,
            CombatEncounterMode mode,
            string championDefinitionId,
            string championCombatProfileId,
            string skillLoadoutId,
            string bossDefinitionId,
            string bossCombatProfileId,
            string combatRulesProfileId,
            string arenaProfileId,
            string neutralRealmContextId,
            string requiredRealmDefinitionVersion,
            IList<string> allowedAuthoritativeRealmIds,
            string expectedProfileRevision,
            bool usesDevelopmentFallbackSource,
            bool allowsRetryAfterCompleted,
            bool allowsRetryAfterFailed,
            bool allowsRetryAfterCancelled)
        {
            GameId = gameId ?? string.Empty;
            CatalogSetId = catalogSetId ?? string.Empty;
            RequiredProfileId = requiredProfileId ?? string.Empty;
            EncounterDefinitionId = encounterDefinitionId ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            ContentVersion = contentVersion ?? string.Empty;
            Mode = mode;
            ChampionDefinitionId = championDefinitionId ?? string.Empty;
            ChampionCombatProfileId =
                championCombatProfileId ?? string.Empty;
            SkillLoadoutId = skillLoadoutId ?? string.Empty;
            BossDefinitionId = bossDefinitionId ?? string.Empty;
            BossCombatProfileId = bossCombatProfileId ?? string.Empty;
            CombatRulesProfileId = combatRulesProfileId ?? string.Empty;
            ArenaProfileId = arenaProfileId ?? string.Empty;
            NeutralRealmContextId = neutralRealmContextId ?? string.Empty;
            RequiredRealmDefinitionVersion =
                requiredRealmDefinitionVersion ?? string.Empty;
            AllowedAuthoritativeRealmInputCount =
                allowedAuthoritativeRealmIds?.Count ?? 0;
            AllowedAuthoritativeRealmIds = FreezeBounded(
                allowedAuthoritativeRealmIds,
                ChampionEncounterPlanner.MaximumAuthoritativeRealms + 1);
            ExpectedProfileRevision =
                expectedProfileRevision ?? string.Empty;
            UsesDevelopmentFallbackSource = usesDevelopmentFallbackSource;
            AllowsRetryAfterCompleted = allowsRetryAfterCompleted;
            AllowsRetryAfterFailed = allowsRetryAfterFailed;
            AllowsRetryAfterCancelled = allowsRetryAfterCancelled;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string RequiredProfileId { get; }
        public string EncounterDefinitionId { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public CombatEncounterMode Mode { get; }
        public string ChampionDefinitionId { get; }
        public string ChampionCombatProfileId { get; }
        public string SkillLoadoutId { get; }
        public string BossDefinitionId { get; }
        public string BossCombatProfileId { get; }
        public string CombatRulesProfileId { get; }
        public string ArenaProfileId { get; }
        public string NeutralRealmContextId { get; }
        public string RequiredRealmDefinitionVersion { get; }
        public int AllowedAuthoritativeRealmInputCount { get; }
        public IReadOnlyList<string> AllowedAuthoritativeRealmIds { get; }
        public string ExpectedProfileRevision { get; }
        public bool UsesDevelopmentFallbackSource { get; }
        public bool AllowsRetryAfterCompleted { get; }
        public bool AllowsRetryAfterFailed { get; }
        public bool AllowsRetryAfterCancelled { get; }

        private static IReadOnlyList<string> FreezeBounded(
            IList<string> values,
            int maximumCopyCount)
        {
            if (values == null)
            {
                return Array.AsReadOnly(new string[0]);
            }

            int count = Math.Min(values.Count, maximumCopyCount);
            var copy = new string[count];
            for (int index = 0; index < count; index++)
            {
                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public sealed class ChampionEncounterRequest
    {
        public ChampionEncounterRequest(
            string gameId,
            string catalogSetId,
            string profileId,
            string encounterDefinitionId,
            string encounterDefinitionContentVersion,
            string encounterSessionId,
            string encounterAttemptId,
            string encounterResultId,
            CombatEncounterMode mode,
            string championDefinitionId,
            string championCombatProfileId,
            string skillLoadoutId,
            string bossDefinitionId,
            string bossCombatProfileId,
            string committedRealmId,
            string committedRealmDefinitionVersion,
            string questOrProgressionContextId,
            string rewardOperationId,
            string resumeToken,
            string expectedProfileRevision)
        {
            GameId = gameId ?? string.Empty;
            CatalogSetId = catalogSetId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            EncounterDefinitionId = encounterDefinitionId ?? string.Empty;
            EncounterDefinitionContentVersion =
                encounterDefinitionContentVersion ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            EncounterResultId = encounterResultId ?? string.Empty;
            Mode = mode;
            ChampionDefinitionId = championDefinitionId ?? string.Empty;
            ChampionCombatProfileId =
                championCombatProfileId ?? string.Empty;
            SkillLoadoutId = skillLoadoutId ?? string.Empty;
            BossDefinitionId = bossDefinitionId ?? string.Empty;
            BossCombatProfileId = bossCombatProfileId ?? string.Empty;
            CommittedRealmId = committedRealmId ?? string.Empty;
            CommittedRealmDefinitionVersion =
                committedRealmDefinitionVersion ?? string.Empty;
            QuestOrProgressionContextId =
                questOrProgressionContextId ?? string.Empty;
            RewardOperationId = rewardOperationId ?? string.Empty;
            ResumeToken = resumeToken ?? string.Empty;
            ExpectedProfileRevision =
                expectedProfileRevision ?? string.Empty;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string ProfileId { get; }
        public string EncounterDefinitionId { get; }
        public string EncounterDefinitionContentVersion { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string EncounterResultId { get; }
        public CombatEncounterMode Mode { get; }
        public string ChampionDefinitionId { get; }
        public string ChampionCombatProfileId { get; }
        public string SkillLoadoutId { get; }
        public string BossDefinitionId { get; }
        public string BossCombatProfileId { get; }
        public string CommittedRealmId { get; }
        public string CommittedRealmDefinitionVersion { get; }
        public string QuestOrProgressionContextId { get; }
        public string RewardOperationId { get; }
        public string ResumeToken { get; }
        public string ExpectedProfileRevision { get; }
    }

    public sealed class ChampionEncounterRequestCorrelation
    {
        public ChampionEncounterRequestCorrelation(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request,
            bool isActive)
        {
            Definition = definition;
            Request = request;
            IsActive = isActive;
        }

        public ChampionEncounterDefinitionSnapshot Definition { get; }
        public ChampionEncounterRequest Request { get; }
        public bool IsActive { get; }
    }

    public sealed class ResolvedChampionEncounterSnapshot
    {
        internal ResolvedChampionEncounterSnapshot(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request,
            string semanticFingerprint,
            string sourceSnapshotHash,
            bool hasDurableResultAuthority,
            bool rewardEligible)
        {
            Definition = definition;
            Request = request;
            SemanticFingerprint = semanticFingerprint ?? string.Empty;
            SourceSnapshotHash = sourceSnapshotHash ?? string.Empty;
            HasDurableResultAuthority = hasDurableResultAuthority;
            RewardEligible = rewardEligible;
        }

        public ChampionEncounterDefinitionSnapshot Definition { get; }
        public ChampionEncounterRequest Request { get; }
        public string SemanticFingerprint { get; }
        public string SourceSnapshotHash { get; }
        public bool HasDurableResultAuthority { get; }
        public bool RewardEligible { get; }
    }

    public sealed class ChampionEncounterRequestPlan
    {
        internal ChampionEncounterRequestPlan(
            ChampionEncounterRequestStatus status,
            ResolvedChampionEncounterSnapshot resolved,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            Status = status;
            Resolved = resolved;
            Diagnostics = CombatDiagnosticOrdering.Order(diagnostics);
        }

        public ChampionEncounterRequestStatus Status { get; }
        public ResolvedChampionEncounterSnapshot Resolved { get; }
        public IReadOnlyList<CombatDiagnostic> Diagnostics { get; }
        public bool IsResolved =>
            Status == ChampionEncounterRequestStatus.Resolved ||
            Status == ChampionEncounterRequestStatus.DuplicateExact;
    }

    public sealed class ChampionEncounterStateSnapshot
    {
        internal ChampionEncounterStateSnapshot(
            string encounterSessionId,
            string encounterAttemptId,
            string encounterResultId,
            string rewardOperationId,
            string sourceSnapshotHash,
            string championCombatProfileId,
            string bossCombatProfileId,
            string parentEncounterAttemptId,
            CombatEncounterMode mode,
            CombatEncounterState state,
            ChampionEncounterTerminalOutcome terminalOutcome,
            long encounterElapsedMicros,
            long revision,
            ChampionEncounterComputedOutcome frozenOutcome = null)
        {
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            EncounterResultId = encounterResultId ?? string.Empty;
            RewardOperationId = rewardOperationId ?? string.Empty;
            SourceSnapshotHash = sourceSnapshotHash ?? string.Empty;
            ChampionCombatProfileId =
                championCombatProfileId ?? string.Empty;
            BossCombatProfileId =
                bossCombatProfileId ?? string.Empty;
            ParentEncounterAttemptId =
                parentEncounterAttemptId ?? string.Empty;
            Mode = mode;
            State = state;
            TerminalOutcome = terminalOutcome;
            EncounterElapsedMicros = encounterElapsedMicros;
            Revision = revision;
            FrozenOutcome = frozenOutcome;
        }

        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string EncounterResultId { get; }
        public string RewardOperationId { get; }
        public string SourceSnapshotHash { get; }
        public string ChampionCombatProfileId { get; }
        public string BossCombatProfileId { get; }
        public string ParentEncounterAttemptId { get; }
        public CombatEncounterMode Mode { get; }
        public CombatEncounterState State { get; }
        public ChampionEncounterTerminalOutcome TerminalOutcome { get; }
        public long EncounterElapsedMicros { get; }
        public long Revision { get; }
        public ChampionEncounterComputedOutcome FrozenOutcome { get; }
        public bool IsTerminal =>
            State == CombatEncounterState.Completed ||
            State == CombatEncounterState.Failed ||
            State == CombatEncounterState.Cancelled ||
            State == CombatEncounterState.RecoveryRequired ||
            State == CombatEncounterState.Disposed;
    }

    public sealed class ChampionEncounterTransitionRequest
    {
        public ChampionEncounterTransitionRequest(
            string transitionId,
            string encounterSessionId,
            string encounterAttemptId,
            CombatEncounterState targetState,
            ChampionEncounterTerminalOutcome terminalOutcome,
            long atEncounterMicros,
            long expectedRevision)
        {
            TransitionId = transitionId ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            TargetState = targetState;
            TerminalOutcome = terminalOutcome;
            AtEncounterMicros = atEncounterMicros;
            ExpectedRevision = expectedRevision;
        }

        public string TransitionId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public CombatEncounterState TargetState { get; }
        public ChampionEncounterTerminalOutcome TerminalOutcome { get; }
        public long AtEncounterMicros { get; }
        public long ExpectedRevision { get; }
    }

    public sealed class ChampionEncounterTransitionReceipt
    {
        internal ChampionEncounterTransitionReceipt(
            ChampionEncounterTransitionRequest request,
            string requestFingerprint,
            ChampionEncounterTransitionStatus status,
            CombatEncounterState beforeState,
            CombatEncounterState afterState,
            long beforeRevision,
            long afterRevision)
        {
            Request = request;
            TransitionId = request?.TransitionId ?? string.Empty;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            Status = status;
            BeforeState = beforeState;
            AfterState = afterState;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            BeforeTerminalOutcome =
                ChampionEncounterTerminalOutcome.None;
            AfterTerminalOutcome =
                ChampionEncounterTerminalOutcome.None;
            BeforeEncounterElapsedMicros = 0L;
            AfterEncounterElapsedMicros = 0L;
            BeforeSourceSnapshotHash = string.Empty;
            AfterSourceSnapshotHash = string.Empty;
            BeforeFrozenOutcomeHash = string.Empty;
            AfterFrozenOutcomeHash = string.Empty;
            HadFrozenOutcome = false;
            IsPlannerIssued = false;
        }

        private ChampionEncounterTransitionReceipt(
            ChampionEncounterTransitionRequest request,
            string requestFingerprint,
            ChampionEncounterTransitionStatus status,
            ChampionEncounterStateSnapshot before,
            ChampionEncounterStateSnapshot after,
            bool isPlannerIssued)
        {
            Request = request;
            TransitionId = request?.TransitionId ?? string.Empty;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            Status = status;
            BeforeState = before.State;
            AfterState = after.State;
            BeforeTerminalOutcome = before.TerminalOutcome;
            AfterTerminalOutcome = after.TerminalOutcome;
            BeforeEncounterElapsedMicros =
                before.EncounterElapsedMicros;
            AfterEncounterElapsedMicros =
                after.EncounterElapsedMicros;
            BeforeSourceSnapshotHash =
                before.SourceSnapshotHash;
            AfterSourceSnapshotHash =
                after.SourceSnapshotHash;
            BeforeFrozenOutcomeHash =
                before.FrozenOutcome?.OutcomeHash ??
                string.Empty;
            AfterFrozenOutcomeHash =
                after.FrozenOutcome?.OutcomeHash ??
                string.Empty;
            BeforeRevision = before.Revision;
            AfterRevision = after.Revision;
            HadFrozenOutcome = before.FrozenOutcome != null;
            IsPlannerIssued = isPlannerIssued;
        }

        internal static ChampionEncounterTransitionReceipt
            CreatePlannerIssued(
                ChampionEncounterTransitionRequest request,
                string requestFingerprint,
                ChampionEncounterTransitionStatus status,
                ChampionEncounterStateSnapshot before,
                ChampionEncounterStateSnapshot after)
        {
            return new ChampionEncounterTransitionReceipt(
                request,
                requestFingerprint,
                status,
                before,
                after,
                true);
        }

        public ChampionEncounterTransitionRequest Request { get; }
        public string TransitionId { get; }
        public string RequestFingerprint { get; }
        public ChampionEncounterTransitionStatus Status { get; }
        public CombatEncounterState BeforeState { get; }
        public CombatEncounterState AfterState { get; }
        public ChampionEncounterTerminalOutcome
            BeforeTerminalOutcome { get; }
        public ChampionEncounterTerminalOutcome
            AfterTerminalOutcome { get; }
        public long BeforeEncounterElapsedMicros { get; }
        public long AfterEncounterElapsedMicros { get; }
        public string BeforeSourceSnapshotHash { get; }
        public string AfterSourceSnapshotHash { get; }
        public string BeforeFrozenOutcomeHash { get; }
        public string AfterFrozenOutcomeHash { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool HadFrozenOutcome { get; }
        internal bool IsPlannerIssued { get; }
    }

    public enum ChampionEncounterTechnicalEventKind
    {
        StateChanged = 0,
        Terminal = 1,
        Disposed = 2,
        RetryPlanned = 3,
        OutcomeComputed = 4
    }

    public sealed class ChampionEncounterTechnicalEventReceipt
    {
        internal ChampionEncounterTechnicalEventReceipt(
            ChampionEncounterTechnicalEventKind kind,
            string eventName,
            string detailId,
            string encounterSessionId,
            string encounterAttemptId,
            string encounterResultId,
            string previousEncounterAttemptId,
            string previousEncounterResultId,
            long beforeRevision,
            long afterRevision,
            int sequence)
        {
            Kind = kind;
            EventName = eventName ?? string.Empty;
            DetailId = detailId ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            EncounterResultId = encounterResultId ?? string.Empty;
            PreviousEncounterAttemptId =
                previousEncounterAttemptId ?? string.Empty;
            PreviousEncounterResultId =
                previousEncounterResultId ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Sequence = sequence;
        }

        public ChampionEncounterTechnicalEventKind Kind { get; }
        public string EventName { get; }
        public string DetailId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string EncounterResultId { get; }
        public string PreviousEncounterAttemptId { get; }
        public string PreviousEncounterResultId { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public int Sequence { get; }
        public bool IsCrossAttempt =>
            !StringComparer.Ordinal.Equals(
                PreviousEncounterAttemptId,
                EncounterAttemptId);
    }

    public sealed class ChampionEncounterTransitionPlan
    {
        internal ChampionEncounterTransitionPlan(
            ChampionEncounterTransitionStatus status,
            ChampionEncounterStateSnapshot before,
            ChampionEncounterStateSnapshot after,
            ChampionEncounterTransitionReceipt receipt,
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
                ChampionEncounterPlanner.CreateTechnicalEventReceipts(
                    before,
                    after,
                    TechnicalEvents);
            Diagnostics = CombatDiagnosticOrdering.Order(diagnostics);
        }

        public ChampionEncounterTransitionStatus Status { get; }
        public ChampionEncounterStateSnapshot Before { get; }
        public ChampionEncounterStateSnapshot After { get; }
        public ChampionEncounterTransitionReceipt Receipt { get; }
        public IReadOnlyList<string> TechnicalEvents { get; }
        public IReadOnlyList<ChampionEncounterTechnicalEventReceipt>
            TechnicalEventReceipts { get; }
        public IReadOnlyList<CombatDiagnostic> Diagnostics { get; }
    }

    public enum ChampionEncounterOutcome
    {
        ChampionVictory = 0,
        ChampionDefeat = 1,
        Cancelled = 2,
        ValidationFailure = 3,
        RuntimeFailure = 4,
        RecoveryRequired = 5
    }

    public enum ChampionEncounterOutcomePlanStatus
    {
        Computed = 0,
        RejectedInvalidState = 1,
        RejectedInvalidOutcome = 2,
        RejectedInvalidIdentity = 3,
        RejectedInvalidMetric = 4,
        RejectedInvalidHash = 5,
        RejectedMetricLimit = 6,
        DuplicateExact = 7,
        CorrelationConflict = 8,
        ArithmeticFailure = 9
    }

    public sealed class EncounterMetricSnapshot
    {
        public EncounterMetricSnapshot(
            string metricId,
            CombatScalarKind kind,
            long valueMicros,
            string unitProfileId)
        {
            MetricId = metricId ?? string.Empty;
            Kind = kind;
            ValueMicros = valueMicros;
            UnitProfileId = unitProfileId ?? string.Empty;
        }

        public string MetricId { get; }
        public CombatScalarKind Kind { get; }
        public long ValueMicros { get; }
        public string UnitProfileId { get; }
    }

    public sealed class ChampionEncounterResolutionEvidence
    {
        internal ChampionEncounterResolutionEvidence(
            string encounterSessionId,
            string encounterAttemptId,
            string sourceSnapshotHash,
            string championCombatProfileId,
            string bossCombatProfileId,
            string championParticipantId,
            string bossParticipantId,
            CombatantLifeState championLifeState,
            CombatantLifeState bossLifeState,
            long championResourceRevision,
            long bossStateRevision,
            long expectedEncounterRevision,
            long resolutionElapsedMicros,
            ChampionEncounterOutcome outcome,
            string evidenceHash,
            bool isPlannerIssued)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            SourceSnapshotHash = sourceSnapshotHash;
            ChampionCombatProfileId = championCombatProfileId;
            BossCombatProfileId = bossCombatProfileId;
            ChampionParticipantId = championParticipantId;
            BossParticipantId = bossParticipantId;
            ChampionLifeState = championLifeState;
            BossLifeState = bossLifeState;
            ChampionResourceRevision = championResourceRevision;
            BossStateRevision = bossStateRevision;
            ExpectedEncounterRevision = expectedEncounterRevision;
            ResolutionElapsedMicros = resolutionElapsedMicros;
            Outcome = outcome;
            EvidenceHash = evidenceHash;
            IsPlannerIssued = isPlannerIssued;
        }

        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string SourceSnapshotHash { get; }
        public string ChampionCombatProfileId { get; }
        public string BossCombatProfileId { get; }
        public string ChampionParticipantId { get; }
        public string BossParticipantId { get; }
        public CombatantLifeState ChampionLifeState { get; }
        public CombatantLifeState BossLifeState { get; }
        public long ChampionResourceRevision { get; }
        public long BossStateRevision { get; }
        public long ExpectedEncounterRevision { get; }
        public long ResolutionElapsedMicros { get; }
        public ChampionEncounterOutcome Outcome { get; }
        public string EvidenceHash { get; }
        internal bool IsPlannerIssued { get; }
    }

    public sealed class ChampionEncounterComputedOutcome
    {
        internal ChampionEncounterComputedOutcome(
            string encounterSessionId,
            string encounterAttemptId,
            string encounterResultId,
            CombatEncounterMode mode,
            ChampionEncounterOutcome outcome,
            string championParticipantId,
            string bossParticipantId,
            long encounterDurationMicros,
            IList<EncounterMetricSnapshot> metrics,
            string sourceSnapshotHash,
            string outcomeHash,
            bool rewardEligible,
            ChampionEncounterResolutionEvidence resolutionEvidence)
        {
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            EncounterResultId = encounterResultId;
            Mode = mode;
            Outcome = outcome;
            ChampionParticipantId = championParticipantId;
            BossParticipantId = bossParticipantId;
            EncounterDurationMicros = encounterDurationMicros;
            Metrics = Array.AsReadOnly(metrics.ToArray());
            SourceSnapshotHash = sourceSnapshotHash;
            OutcomeHash = outcomeHash;
            RewardEligible = rewardEligible;
            ResolutionEvidence = resolutionEvidence;
        }

        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string EncounterResultId { get; }
        public CombatEncounterMode Mode { get; }
        public ChampionEncounterOutcome Outcome { get; }
        public string ChampionParticipantId { get; }
        public string BossParticipantId { get; }
        public long EncounterDurationMicros { get; }
        public IReadOnlyList<EncounterMetricSnapshot> Metrics { get; }
        public string SourceSnapshotHash { get; }
        public string OutcomeHash { get; }
        public bool RewardEligible { get; }
        public ChampionEncounterResolutionEvidence
            ResolutionEvidence { get; }
    }

    public sealed class ChampionEncounterOutcomePlan
    {
        internal ChampionEncounterOutcomePlan(
            ChampionEncounterOutcomePlanStatus status,
            ChampionEncounterComputedOutcome computedOutcome,
            ChampionEncounterStateSnapshot before,
            ChampionEncounterStateSnapshot after,
            IEnumerable<string> technicalEvents,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            Status = status;
            ComputedOutcome = computedOutcome;
            Before = before;
            After = after;
            TechnicalEvents = Array.AsReadOnly(
                (technicalEvents ??
                 Enumerable.Empty<string>()).ToArray());
            TechnicalEventReceipts =
                ChampionEncounterPlanner.CreateTechnicalEventReceipts(
                    before,
                    after,
                    TechnicalEvents);
            Diagnostics = CombatDiagnosticOrdering.Order(diagnostics);
        }

        public ChampionEncounterOutcomePlanStatus Status { get; }
        public ChampionEncounterComputedOutcome ComputedOutcome { get; }
        public ChampionEncounterStateSnapshot Before { get; }
        public ChampionEncounterStateSnapshot After { get; }
        public IReadOnlyList<string> TechnicalEvents { get; }
        public IReadOnlyList<ChampionEncounterTechnicalEventReceipt>
            TechnicalEventReceipts { get; }
        public IReadOnlyList<CombatDiagnostic> Diagnostics { get; }
    }

    public static class ChampionEncounterPlanner
    {
        public const string CurrentPolicyVersion =
            "combat.encounter.c1.v1";
        public const int MaximumAuthoritativeRealms = 16;
        public const int MaximumRequestCorrelations = 1024;
        public const int MaximumTransitionReceipts = 4096;
        public const int MaximumOutcomeMetrics = 64;

        internal static IReadOnlyList
            <ChampionEncounterTechnicalEventReceipt>
            CreateTechnicalEventReceipts(
                ChampionEncounterStateSnapshot before,
                ChampionEncounterStateSnapshot after,
                IEnumerable<string> eventNames)
        {
            ChampionEncounterStateSnapshot context =
                after ?? before;
            if (context == null)
            {
                return Array.AsReadOnly(
                    new ChampionEncounterTechnicalEventReceipt[0]);
            }

            string[] names =
                (eventNames ?? Enumerable.Empty<string>()).ToArray();
            var receipts =
                new ChampionEncounterTechnicalEventReceipt[
                    names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index] ?? string.Empty;
                ChampionEncounterTechnicalEventKind kind;
                string detail = string.Empty;
                if (StringComparer.Ordinal.Equals(
                        name,
                        "EncounterStateChanged"))
                {
                    kind =
                        ChampionEncounterTechnicalEventKind.StateChanged;
                    detail = context.State.ToString();
                }
                else if (name.StartsWith(
                             "EncounterTerminal:",
                             StringComparison.Ordinal))
                {
                    kind =
                        ChampionEncounterTechnicalEventKind.Terminal;
                    detail = name.Substring(
                        "EncounterTerminal:".Length);
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "EncounterDisposed"))
                {
                    kind =
                        ChampionEncounterTechnicalEventKind.Disposed;
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "EncounterRetryPlanned"))
                {
                    kind =
                        ChampionEncounterTechnicalEventKind.RetryPlanned;
                }
                else if (StringComparer.Ordinal.Equals(
                             name,
                             "EncounterOutcomeComputed"))
                {
                    kind =
                        ChampionEncounterTechnicalEventKind
                            .OutcomeComputed;
                    detail =
                        context.FrozenOutcome?.Outcome.ToString() ??
                        string.Empty;
                }
                else
                {
                    throw new ArgumentException(
                        "Unknown encounter technical event.",
                        nameof(eventNames));
                }

                receipts[index] =
                    new ChampionEncounterTechnicalEventReceipt(
                        kind,
                        name,
                        detail,
                        context.EncounterSessionId,
                        context.EncounterAttemptId,
                        context.EncounterResultId,
                        before?.EncounterAttemptId ??
                            context.EncounterAttemptId,
                        before?.EncounterResultId ??
                            context.EncounterResultId,
                        before?.Revision ?? context.Revision,
                        after?.Revision ?? context.Revision,
                        index);
            }

            return Array.AsReadOnly(receipts);
        }

        public static bool TryCreateResolutionEvidence(
            ChampionEncounterStateSnapshot resolving,
            IList<CombatParticipantRegistration> participantRegistry,
            CombatantResourceSnapshot championResources,
            BossStateSnapshot bossState,
            out ChampionEncounterResolutionEvidence evidence)
        {
            evidence = null;
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidateState(resolving, diagnostics) ||
                resolving.State != CombatEncounterState.Resolving ||
                resolving.FrozenOutcome != null ||
                participantRegistry == null ||
                participantRegistry.Count == 0 ||
                participantRegistry.Count >
                    CombatTargetingTechnicalLimits.MaximumParticipants ||
                championResources == null ||
                bossState == null)
            {
                return false;
            }

            string championId =
                championResources.ActorParticipantId.Value;
            string bossId = bossState.ParticipantId;
            if (!CombatPrimitiveValidation.IsStableId(championId) ||
                !CombatPrimitiveValidation.IsStableId(bossId) ||
                StringComparer.Ordinal.Equals(championId, bossId) ||
                !StringComparer.Ordinal.Equals(
                    championResources.EncounterSessionId.Value,
                    resolving.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    championResources.EncounterAttemptId.Value,
                    resolving.EncounterAttemptId) ||
                !StringComparer.Ordinal.Equals(
                    bossState.EncounterSessionId,
                    resolving.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    bossState.EncounterAttemptId,
                    resolving.EncounterAttemptId) ||
                championResources.RevisionOrdinal < 0L ||
                bossState.Revision < 0L)
            {
                return false;
            }

            CombatParticipantRegistration championRegistration = null;
            CombatParticipantRegistration bossRegistration = null;
            var participantIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0;
                 index < participantRegistry.Count;
                 index++)
            {
                CombatParticipantRegistration row =
                    participantRegistry[index];
                if (row == null ||
                    !CombatPrimitiveValidation.IsStableId(
                        row.ParticipantId) ||
                    !participantIds.Add(row.ParticipantId) ||
                    !StringComparer.Ordinal.Equals(
                        row.EncounterSessionId,
                        resolving.EncounterSessionId) ||
                    !StringComparer.Ordinal.Equals(
                        row.EncounterAttemptId,
                        resolving.EncounterAttemptId) ||
                    !Enum.IsDefined(
                        typeof(CombatParticipantRole),
                        row.Role) ||
                    !Enum.IsDefined(
                        typeof(CombatantLifeState),
                        row.LifeState))
                {
                    return false;
                }

                if (StringComparer.Ordinal.Equals(
                        row.ParticipantId,
                        championId))
                {
                    championRegistration = row;
                }

                if (StringComparer.Ordinal.Equals(
                        row.ParticipantId,
                        bossId))
                {
                    bossRegistration = row;
                }
            }

            if (championRegistration == null ||
                championRegistration.Role !=
                    CombatParticipantRole.Champion ||
                !StringComparer.Ordinal.Equals(
                    championRegistration.ActorProfileId,
                    resolving.ChampionCombatProfileId) ||
                championRegistration.LifeState !=
                    championResources.LifeState ||
                bossRegistration == null ||
                bossRegistration.Role != CombatParticipantRole.Boss ||
                !StringComparer.Ordinal.Equals(
                    bossRegistration.ActorProfileId,
                    resolving.BossCombatProfileId) ||
                !StringComparer.Ordinal.Equals(
                    bossState.BossProfileId,
                    resolving.BossCombatProfileId) ||
                bossRegistration.LifeState != bossState.LifeState)
            {
                return false;
            }

            ChampionEncounterOutcome outcome;
            if (championResources.LifeState ==
                    CombatantLifeState.Alive &&
                bossState.LifeState ==
                    CombatantLifeState.Defeated)
            {
                outcome = ChampionEncounterOutcome.ChampionVictory;
            }
            else if (championResources.LifeState ==
                         CombatantLifeState.Defeated &&
                     bossState.LifeState ==
                         CombatantLifeState.Alive)
            {
                outcome = ChampionEncounterOutcome.ChampionDefeat;
            }
            else
            {
                return false;
            }

            string evidenceHash = ResolutionEvidenceHash(
                resolving.EncounterSessionId,
                resolving.EncounterAttemptId,
                resolving.SourceSnapshotHash,
                resolving.ChampionCombatProfileId,
                resolving.BossCombatProfileId,
                championId,
                bossId,
                championResources.LifeState,
                bossState.LifeState,
                championResources.RevisionOrdinal,
                bossState.Revision,
                resolving.Revision,
                resolving.EncounterElapsedMicros,
                outcome);
            evidence = new ChampionEncounterResolutionEvidence(
                resolving.EncounterSessionId,
                resolving.EncounterAttemptId,
                resolving.SourceSnapshotHash,
                resolving.ChampionCombatProfileId,
                resolving.BossCombatProfileId,
                championId,
                bossId,
                championResources.LifeState,
                bossState.LifeState,
                championResources.RevisionOrdinal,
                bossState.Revision,
                resolving.Revision,
                resolving.EncounterElapsedMicros,
                outcome,
                evidenceHash,
                true);
            return true;
        }

        public static ChampionEncounterRequestPlan PlanRequest(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request,
            IList<ChampionEncounterRequestCorrelation> correlations)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidateDefinition(definition, diagnostics))
            {
                return RequestPlan(
                    ChampionEncounterRequestStatus.RejectedInvalidDefinition,
                    null,
                    diagnostics);
            }

            if (!ValidateRequestRequiredIds(request, diagnostics))
            {
                return RequestPlan(
                    ChampionEncounterRequestStatus.RejectedInvalidRequest,
                    null,
                    diagnostics);
            }

            if (correlations != null &&
                correlations.Count > MaximumRequestCorrelations)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-CORRELATION-LIMIT",
                    "correlations",
                    "Encounter correlation set exceeds its technical maximum.",
                    definition,
                    request));
                return RequestPlan(
                    ChampionEncounterRequestStatus.RejectedCorrelationLimit,
                    null,
                    diagnostics);
            }

            if (!ValidateCorrelations(
                    correlations,
                    definition,
                    request,
                    diagnostics))
            {
                return RequestPlan(
                    ChampionEncounterRequestStatus
                        .RejectedInvalidRequest,
                    null,
                    diagnostics);
            }

            if (request.Mode != definition.Mode)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-MODE-MISMATCH",
                    "request.mode",
                    "Encounter request mode does not match the definition.",
                    definition,
                    request));
                return RequestPlan(
                    ChampionEncounterRequestStatus.RejectedModeMismatch,
                    null,
                    diagnostics);
            }

            if (!MatchesSource(definition, request))
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-SOURCE-MISMATCH",
                    "request.source",
                    "Encounter request does not match its immutable source snapshot.",
                    definition,
                    request));
                return RequestPlan(
                    ChampionEncounterRequestStatus.RejectedSourceMismatch,
                    null,
                    diagnostics);
            }

            if (!StringComparer.Ordinal.Equals(
                    request.ExpectedProfileRevision,
                    definition.ExpectedProfileRevision))
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-STALE-REVISION",
                    "request.expectedProfileRevision",
                    "Encounter request expected a stale profile revision.",
                    definition,
                    request));
                return RequestPlan(
                    ChampionEncounterRequestStatus.RejectedStaleRevision,
                    null,
                    diagnostics);
            }

            if (IsAuthoritative(request.Mode) &&
                definition.UsesDevelopmentFallbackSource)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-DEVELOPMENT-FALLBACK",
                    "definition.usesDevelopmentFallbackSource",
                    "Development fallback source cannot authorize a production encounter.",
                    definition,
                    request));
                return RequestPlan(
                    ChampionEncounterRequestStatus.RejectedDevelopmentFallback,
                    null,
                    diagnostics);
            }

            ChampionEncounterRequestStatus modeStatus =
                ValidateModeContext(definition, request, diagnostics);
            if (modeStatus != ChampionEncounterRequestStatus.Resolved)
            {
                return RequestPlan(modeStatus, null, diagnostics);
            }

            string fingerprint = Fingerprint(definition, request);
            foreach (ChampionEncounterRequestCorrelation correlation in
                     correlations ??
                     new ChampionEncounterRequestCorrelation[0])
            {
                if (correlation?.Request == null)
                {
                    continue;
                }

                ChampionEncounterRequest existing = correlation.Request;
                bool sameSession = StringComparer.Ordinal.Equals(
                    existing.EncounterSessionId,
                    request.EncounterSessionId);
                bool sessionCollision =
                    correlation.IsActive && sameSession;
                bool attemptCollision = StringComparer.Ordinal.Equals(
                    existing.EncounterAttemptId,
                    request.EncounterAttemptId);
                bool resultCollision = StringComparer.Ordinal.Equals(
                    existing.EncounterResultId,
                    request.EncounterResultId);
                bool rewardCollision =
                    !string.IsNullOrEmpty(request.RewardOperationId) &&
                    StringComparer.Ordinal.Equals(
                        existing.RewardOperationId,
                        request.RewardOperationId);
                if (!sessionCollision &&
                    !attemptCollision &&
                    !resultCollision &&
                    !rewardCollision)
                {
                    continue;
                }

                if (sameSession &&
                    attemptCollision &&
                    resultCollision &&
                    StringComparer.Ordinal.Equals(
                        existing.RewardOperationId,
                        request.RewardOperationId) &&
                    SemanticEquals(
                        correlation.Definition,
                        existing,
                        definition,
                        request))
                {
                    return RequestPlan(
                        ChampionEncounterRequestStatus.DuplicateExact,
                        Resolve(definition, request, fingerprint),
                        diagnostics);
                }

                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-CORRELATION-CONFLICT",
                    "request.encounterAttemptId",
                    "An active session or permanently reserved attempt, result, or reward identity was reused with changed input.",
                    definition,
                    request));
                return RequestPlan(
                    ChampionEncounterRequestStatus.CorrelationConflict,
                    null,
                    diagnostics);
            }

            if ((correlations?.Count ?? 0) >=
                MaximumRequestCorrelations)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-CORRELATION-CAPACITY",
                    "correlations",
                    "Encounter correlation ledger is at capacity and cannot retain a new identity.",
                    definition,
                    request));
                return RequestPlan(
                    ChampionEncounterRequestStatus
                        .RejectedCorrelationLimit,
                    null,
                    diagnostics);
            }

            return RequestPlan(
                ChampionEncounterRequestStatus.Resolved,
                Resolve(definition, request, fingerprint),
                diagnostics);
        }

        public static ChampionEncounterStateSnapshot CreateInitialState(
            ResolvedChampionEncounterSnapshot resolved)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidateResolvedSnapshot(resolved, diagnostics))
            {
                return null;
            }

            return new ChampionEncounterStateSnapshot(
                resolved.Request.EncounterSessionId,
                resolved.Request.EncounterAttemptId,
                resolved.Request.EncounterResultId,
                resolved.Request.RewardOperationId,
                resolved.SourceSnapshotHash,
                resolved.Request.ChampionCombatProfileId,
                resolved.Request.BossCombatProfileId,
                string.Empty,
                resolved.Request.Mode,
                CombatEncounterState.Created,
                ChampionEncounterTerminalOutcome.None,
                0L,
                0L);
        }

        public static ChampionEncounterTransitionPlan PlanTransition(
            ChampionEncounterStateSnapshot current,
            ChampionEncounterTransitionRequest request,
            IList<ChampionEncounterTransitionReceipt> replayReceipts)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidateState(current, diagnostics))
            {
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedInvalidState,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!ValidateTransitionRequest(request, diagnostics))
            {
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedInvalidRequest,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!StringComparer.Ordinal.Equals(
                    current.EncounterSessionId,
                    request.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    current.EncounterAttemptId,
                    request.EncounterAttemptId))
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-WRONG-ATTEMPT",
                    "request.encounterAttemptId",
                    "Encounter transition belongs to another attempt.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedWrongEncounter,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (replayReceipts != null &&
                replayReceipts.Count > MaximumTransitionReceipts)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-REPLAY-LIMIT",
                    "replayReceipts",
                    "Encounter transition receipt set exceeds its technical maximum.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedInvalidRequest,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!ValidateTransitionReceipts(
                    replayReceipts,
                    current,
                    request,
                    diagnostics))
            {
                return TransitionPlan(
                    ChampionEncounterTransitionStatus
                        .RejectedInvalidRequest,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            string fingerprint = TransitionFingerprint(request);
            ChampionEncounterTransitionReceipt existing =
                (replayReceipts ??
                 new ChampionEncounterTransitionReceipt[0])
                .FirstOrDefault(receipt =>
                    receipt != null &&
                    StringComparer.Ordinal.Equals(
                        receipt.TransitionId,
                        request.TransitionId));
            if (existing != null)
            {
                if (StringComparer.Ordinal.Equals(
                        existing.RequestFingerprint,
                        fingerprint))
                {
                    return TransitionPlan(
                        ChampionEncounterTransitionStatus.DuplicateExact,
                        current,
                        current,
                        existing,
                        new string[0],
                        diagnostics);
                }

                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-CORRELATION-CONFLICT",
                    "request.transitionId",
                    "Encounter transition ID was reused with changed input.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.CorrelationConflict,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if ((replayReceipts?.Count ?? 0) >=
                MaximumTransitionReceipts)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-REPLAY-CAPACITY",
                    "replayReceipts",
                    "Encounter transition replay ledger is at capacity and cannot retain a new operation.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.CapacityReached,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (request.ExpectedRevision != current.Revision)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-STALE-REVISION",
                    "request.expectedRevision",
                    "Encounter transition expected a stale revision.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedStaleRevision,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!CombatPrimitiveValidation.IsMicrosInRange(
                    request.AtEncounterMicros,
                    CombatScalarKind.Duration,
                    false) ||
                request.AtEncounterMicros <
                    current.EncounterElapsedMicros)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-INVALID-CLOCK",
                    "request.atEncounterMicros",
                    "Encounter transition time is invalid or moved backward.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedInvalidClock,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (current.State == CombatEncounterState.Disposed ||
                (current.IsTerminal &&
                 request.TargetState !=
                     CombatEncounterState.Disposed))
            {
                var noChangeReceipt =
                    ChampionEncounterTransitionReceipt
                    .CreatePlannerIssued(
                        request,
                        fingerprint,
                        ChampionEncounterTransitionStatus.NoChangeTerminal,
                        current,
                        current);
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.NoChangeTerminal,
                    current,
                    current,
                    noChangeReceipt,
                    new string[0],
                    diagnostics);
            }

            if (current.Revision == long.MaxValue)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-REVISION-OVERFLOW",
                    "state.revision",
                    "Encounter revision cannot advance without overflow.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.ArithmeticFailure,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!IsAllowedTransition(
                    current.State,
                    request.TargetState,
                    current.Mode))
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-TRANSITION-REJECTED",
                    "request.targetState",
                    "Encounter state transition is not allowed by the lifecycle matrix.",
                    current,
                    request));
                return TransitionPlan(
                    IsModeSpecificTransition(
                        current.State,
                        request.TargetState)
                        ? ChampionEncounterTransitionStatus
                            .RejectedModePolicy
                        : ChampionEncounterTransitionStatus
                            .RejectedTransition,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            bool entersCommit =
                request.TargetState ==
                    CombatEncounterState.CompletionPendingCommit;
            bool entersCompleted =
                request.TargetState == CombatEncounterState.Completed;
            if ((entersCommit || entersCompleted) &&
                current.FrozenOutcome == null)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-OUTCOME-NOT-FROZEN",
                    "state.frozenOutcome",
                    "A validated immutable outcome must be frozen before result commit or completion.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus
                        .RejectedTerminalConflict,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (entersCompleted &&
                request.TerminalOutcome !=
                    TerminalOutcomeFor(
                        current.FrozenOutcome.Outcome))
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-FROZEN-OUTCOME-CONFLICT",
                    "request.terminalOutcome",
                    "Completed lifecycle outcome must match the previously frozen computed outcome.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus
                        .RejectedTerminalConflict,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            ChampionEncounterTerminalOutcome expectedTerminal =
                request.TargetState == CombatEncounterState.Disposed
                    ? current.TerminalOutcome
                    : request.TerminalOutcome;
            if (expectedTerminal != request.TerminalOutcome ||
                !TerminalOutcomeMatchesState(
                    expectedTerminal,
                    request.TargetState))
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-TERMINAL-MISMATCH",
                    "request.terminalOutcome",
                    "Terminal result semantics do not match the target lifecycle state.",
                    current,
                    request));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus
                        .RejectedTerminalConflict,
                    current,
                    current,
                    null,
                    new string[0],
                    diagnostics);
            }

            var after = new ChampionEncounterStateSnapshot(
                current.EncounterSessionId,
                current.EncounterAttemptId,
                current.EncounterResultId,
                current.RewardOperationId,
                current.SourceSnapshotHash,
                current.ChampionCombatProfileId,
                current.BossCombatProfileId,
                current.ParentEncounterAttemptId,
                current.Mode,
                request.TargetState,
                expectedTerminal,
                request.AtEncounterMicros,
                checked(current.Revision + 1L),
                current.FrozenOutcome);
            var events = new List<string> { "EncounterStateChanged" };
            if (expectedTerminal !=
                    ChampionEncounterTerminalOutcome.None &&
                request.TargetState != CombatEncounterState.Disposed)
            {
                events.Add(
                    "EncounterTerminal:" +
                    expectedTerminal);
            }

            if (request.TargetState == CombatEncounterState.Disposed)
            {
                events.Add("EncounterDisposed");
            }

            var appliedReceipt =
                ChampionEncounterTransitionReceipt
                .CreatePlannerIssued(
                    request,
                    fingerprint,
                    ChampionEncounterTransitionStatus.Applied,
                    current,
                    after);
            return TransitionPlan(
                ChampionEncounterTransitionStatus.Applied,
                current,
                after,
                appliedReceipt,
                events,
                diagnostics);
        }

        public static ChampionEncounterTransitionPlan PlanRetry(
            ChampionEncounterStateSnapshot terminal,
            ResolvedChampionEncounterSnapshot previous,
            ResolvedChampionEncounterSnapshot retry,
            ChampionEncounterStateSnapshot existingRetry)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidateState(terminal, diagnostics) ||
                !ValidateResolvedSnapshot(previous, diagnostics) ||
                !ValidateResolvedSnapshot(retry, diagnostics))
            {
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedInvalidState,
                    terminal,
                    terminal,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!terminal.IsTerminal ||
                terminal.State == CombatEncounterState.Disposed)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-RETRY-NOT-TERMINAL",
                    "state",
                    "Retry requires an undisposed terminal encounter receipt.",
                    terminal,
                    null));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedRetryPolicy,
                    terminal,
                    terminal,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (terminal.State ==
                CombatEncounterState.RecoveryRequired)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-RECOVERY-PENDING",
                    "state",
                    "Commit uncertainty must resolve before a new retry is authorized.",
                    terminal,
                    null));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus
                        .RejectedRecoveryPending,
                    terminal,
                    terminal,
                    null,
                    new string[0],
                    diagnostics);
            }

            if (!RetryAllowed(previous.Definition, terminal.State))
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-RETRY-POLICY",
                    "definition.retryPolicy",
                    "Encounter definition does not allow retry from this terminal state.",
                    terminal,
                    null));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus.RejectedRetryPolicy,
                    terminal,
                    terminal,
                    null,
                    new string[0],
                    diagnostics);
            }

            ChampionEncounterRequest beforeRequest = previous.Request;
            ChampionEncounterRequest retryRequest = retry.Request;
            bool sameContext =
                DefinitionSemanticEquals(
                    previous.Definition,
                    retry.Definition) &&
                StringComparer.Ordinal.Equals(
                    terminal.EncounterSessionId,
                    retryRequest.EncounterSessionId) &&
                StringComparer.Ordinal.Equals(
                    terminal.EncounterSessionId,
                    beforeRequest.EncounterSessionId) &&
                StringComparer.Ordinal.Equals(
                    terminal.EncounterAttemptId,
                    beforeRequest.EncounterAttemptId) &&
                StringComparer.Ordinal.Equals(
                    terminal.EncounterResultId,
                    beforeRequest.EncounterResultId) &&
                StringComparer.Ordinal.Equals(
                    terminal.RewardOperationId,
                    beforeRequest.RewardOperationId) &&
                terminal.Mode == beforeRequest.Mode &&
                RetryContextMatches(
                    beforeRequest,
                    retryRequest);
            bool freshIds =
                !StringComparer.Ordinal.Equals(
                    beforeRequest.EncounterAttemptId,
                    retryRequest.EncounterAttemptId) &&
                !StringComparer.Ordinal.Equals(
                    beforeRequest.EncounterResultId,
                    retryRequest.EncounterResultId) &&
                (!IsAuthoritative(retryRequest.Mode) ||
                 !StringComparer.Ordinal.Equals(
                     beforeRequest.RewardOperationId,
                     retryRequest.RewardOperationId));
            if (!sameContext || !freshIds)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-RETRY-IDENTITY",
                    "retry.request",
                    "Retry must retain its session context and use new attempt, result, and reward identities.",
                    terminal,
                    null));
                return TransitionPlan(
                    ChampionEncounterTransitionStatus
                        .RejectedRetryIdentity,
                    terminal,
                    terminal,
                    null,
                    new string[0],
                    diagnostics);
            }

            var after = new ChampionEncounterStateSnapshot(
                retryRequest.EncounterSessionId,
                retryRequest.EncounterAttemptId,
                retryRequest.EncounterResultId,
                retryRequest.RewardOperationId,
                retry.SourceSnapshotHash,
                retryRequest.ChampionCombatProfileId,
                retryRequest.BossCombatProfileId,
                terminal.EncounterAttemptId,
                retryRequest.Mode,
                CombatEncounterState.Created,
                ChampionEncounterTerminalOutcome.None,
                0L,
                0L,
                null);
            if (existingRetry != null)
            {
                var existingDiagnostics = new List<CombatDiagnostic>();
                bool validRetryShape =
                    ValidateState(
                        existingRetry,
                        existingDiagnostics) &&
                    existingRetry.State ==
                        CombatEncounterState.Created &&
                    existingRetry.TerminalOutcome ==
                        ChampionEncounterTerminalOutcome.None &&
                    existingRetry.EncounterElapsedMicros == 0L &&
                    existingRetry.Revision == 0L;
                if (!validRetryShape)
                {
                    foreach (CombatDiagnostic diagnostic in
                             existingDiagnostics)
                    {
                        diagnostics.Add(diagnostic);
                    }

                    diagnostics.Add(StateError(
                        "AL-ENCOUNTER-STATE-RETRY-SNAPSHOT-INVALID",
                        "existingRetry",
                        "Existing retry snapshot is not a pristine Created state.",
                        existingRetry,
                        null));
                    return TransitionPlan(
                        ChampionEncounterTransitionStatus
                            .RejectedInvalidState,
                        terminal,
                        terminal,
                        null,
                        new string[0],
                        diagnostics);
                }

                bool exactExisting =
                    StringComparer.Ordinal.Equals(
                        existingRetry.EncounterSessionId,
                        after.EncounterSessionId) &&
                    StringComparer.Ordinal.Equals(
                        existingRetry.EncounterAttemptId,
                        after.EncounterAttemptId) &&
                    StringComparer.Ordinal.Equals(
                        existingRetry.EncounterResultId,
                        after.EncounterResultId) &&
                    StringComparer.Ordinal.Equals(
                        existingRetry.RewardOperationId,
                        after.RewardOperationId) &&
                    StringComparer.Ordinal.Equals(
                        existingRetry.SourceSnapshotHash,
                        after.SourceSnapshotHash) &&
                    StringComparer.Ordinal.Equals(
                        existingRetry.ChampionCombatProfileId,
                        after.ChampionCombatProfileId) &&
                    StringComparer.Ordinal.Equals(
                        existingRetry.BossCombatProfileId,
                        after.BossCombatProfileId) &&
                    StringComparer.Ordinal.Equals(
                        existingRetry.ParentEncounterAttemptId,
                        after.ParentEncounterAttemptId) &&
                    existingRetry.Mode == after.Mode &&
                    existingRetry.State == after.State &&
                    existingRetry.TerminalOutcome ==
                        after.TerminalOutcome &&
                    existingRetry.EncounterElapsedMicros ==
                        after.EncounterElapsedMicros &&
                    existingRetry.Revision == after.Revision &&
                    existingRetry.FrozenOutcome == null;
                return TransitionPlan(
                    exactExisting
                        ? ChampionEncounterTransitionStatus.DuplicateExact
                        : ChampionEncounterTransitionStatus
                            .CorrelationConflict,
                    terminal,
                    exactExisting ? existingRetry : terminal,
                    null,
                    new string[0],
                    diagnostics);
            }

            return TransitionPlan(
                ChampionEncounterTransitionStatus.RetryPlanned,
                terminal,
                after,
                null,
                new[] { "EncounterRetryPlanned" },
                diagnostics);
        }

        public static ChampionEncounterOutcomePlan PlanComputedOutcome(
            ChampionEncounterStateSnapshot terminal,
            ChampionEncounterResolutionEvidence resolutionEvidence,
            IList<EncounterMetricSnapshot> metrics,
            string sourceSnapshotHash,
            string outcomeHash)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (!ValidateState(terminal, diagnostics) ||
                terminal.State != CombatEncounterState.Resolving)
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-STATE",
                    "terminal",
                    "Computed outcome must be frozen while the encounter is Resolving, before result commit or completion.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedInvalidState,
                    null,
                    diagnostics);
            }

            if (!ValidateResolutionEvidence(
                    terminal,
                    resolutionEvidence))
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-EVIDENCE",
                    "resolutionEvidence",
                    "Computed outcome requires planner-issued attempt-bound participant and terminal-life evidence.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedInvalidOutcome,
                    null,
                    diagnostics);
            }

            ChampionEncounterOutcome outcome =
                resolutionEvidence.Outcome;
            string championParticipantId =
                resolutionEvidence.ChampionParticipantId;
            string bossParticipantId =
                resolutionEvidence.BossParticipantId;
            if (!CombatPrimitiveValidation.IsStableId(
                    championParticipantId) ||
                !CombatPrimitiveValidation.IsStableId(
                    bossParticipantId) ||
                StringComparer.Ordinal.Equals(
                    championParticipantId,
                    bossParticipantId))
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-PARTICIPANT",
                    "participantId",
                    "Computed outcome requires distinct stable Champion and boss participant IDs.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedInvalidIdentity,
                    null,
                    diagnostics);
            }

            int metricCount = metrics?.Count ?? 0;
            if (metricCount > MaximumOutcomeMetrics)
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-METRIC-LIMIT",
                    "metrics",
                    "Computed outcome metric set exceeds its technical maximum.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedMetricLimit,
                    null,
                    diagnostics);
            }

            if (metricCount != 0)
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-METRIC-AUTHORITY",
                    "metrics",
                    "C1 accepts only an empty metric set until an attempt-bound metric accumulator issues immutable evidence.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedInvalidMetric,
                    null,
                    diagnostics);
            }

            var metricCopy = new List<EncounterMetricSnapshot>(
                metricCount);
            var metricIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < metricCount; index++)
            {
                EncounterMetricSnapshot metric = metrics[index];
                if (metric == null ||
                    !CombatPrimitiveValidation.IsStableId(
                        metric.MetricId) ||
                    !metricIds.Add(metric.MetricId) ||
                    !Enum.IsDefined(
                        typeof(CombatScalarKind),
                        metric.Kind) ||
                    !CombatPrimitiveValidation.IsStableId(
                        metric.UnitProfileId) ||
                    !CombatPrimitiveValidation.IsMicrosInRange(
                        metric.ValueMicros,
                        metric.Kind,
                        false))
                {
                    diagnostics.Add(OutcomeError(
                        "AL-ENCOUNTER-RESULT-METRIC",
                        "metrics[" + index + "]",
                        "Computed outcome metric is null, duplicated, or outside its finite range.",
                        terminal));
                    return OutcomePlan(
                        ChampionEncounterOutcomePlanStatus
                            .RejectedInvalidMetric,
                        null,
                        diagnostics);
                }

                metricCopy.Add(metric);
            }

            if (!CombatPrimitiveValidation.IsSha256(
                    sourceSnapshotHash) ||
                !StringComparer.Ordinal.Equals(
                    sourceSnapshotHash,
                    terminal.SourceSnapshotHash) ||
                (!string.IsNullOrEmpty(outcomeHash) &&
                 !CombatPrimitiveValidation.IsSha256(outcomeHash)))
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-HASH",
                    "hash",
                    "Computed outcome requires a valid lower-case SHA-256 source hash and an empty or valid expected outcome hash.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedInvalidHash,
                    null,
                    diagnostics);
            }

            metricCopy.Sort((left, right) =>
                StringComparer.Ordinal.Compare(
                    left.MetricId,
                    right.MetricId));
            string computedHash = ComputeOutcomeHash(
                terminal,
                outcome,
                championParticipantId,
                bossParticipantId,
                terminal.EncounterElapsedMicros,
                metricCopy,
                terminal.SourceSnapshotHash,
                resolutionEvidence.EvidenceHash);
            if (!string.IsNullOrEmpty(outcomeHash) &&
                !StringComparer.Ordinal.Equals(
                    outcomeHash,
                    computedHash))
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-HASH-MISMATCH",
                    "outcomeHash",
                    "Caller-supplied outcome hash does not match the canonical computed outcome.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedInvalidHash,
                    null,
                    diagnostics);
            }

            var computed = new ChampionEncounterComputedOutcome(
                terminal.EncounterSessionId,
                terminal.EncounterAttemptId,
                terminal.EncounterResultId,
                terminal.Mode,
                outcome,
                championParticipantId,
                bossParticipantId,
                terminal.EncounterElapsedMicros,
                metricCopy,
                terminal.SourceSnapshotHash,
                computedHash,
                IsAuthoritative(terminal.Mode) &&
                outcome == ChampionEncounterOutcome.ChampionVictory,
                resolutionEvidence);

            if (terminal.FrozenOutcome != null)
            {
                bool exact = StringComparer.Ordinal.Equals(
                    terminal.FrozenOutcome.OutcomeHash,
                    computed.OutcomeHash);
                if (!exact)
                {
                    diagnostics.Add(OutcomeError(
                        "AL-ENCOUNTER-RESULT-CORRELATION-CONFLICT",
                        "outcomeHash",
                        "The encounter already froze a different computed outcome.",
                        terminal));
                }

                return OutcomePlan(
                    exact
                        ? ChampionEncounterOutcomePlanStatus
                            .DuplicateExact
                        : ChampionEncounterOutcomePlanStatus
                            .CorrelationConflict,
                    exact ? terminal.FrozenOutcome : null,
                    diagnostics,
                    terminal,
                    terminal,
                    new string[0]);
            }

            if (terminal.Revision == long.MaxValue)
            {
                diagnostics.Add(OutcomeError(
                    "AL-ENCOUNTER-RESULT-REVISION-OVERFLOW",
                    "state.revision",
                    "Encounter revision cannot advance while freezing the computed outcome.",
                    terminal));
                return OutcomePlan(
                    ChampionEncounterOutcomePlanStatus
                        .ArithmeticFailure,
                    null,
                    diagnostics,
                    terminal,
                    terminal,
                    new string[0]);
            }

            var after = new ChampionEncounterStateSnapshot(
                terminal.EncounterSessionId,
                terminal.EncounterAttemptId,
                terminal.EncounterResultId,
                terminal.RewardOperationId,
                terminal.SourceSnapshotHash,
                terminal.ChampionCombatProfileId,
                terminal.BossCombatProfileId,
                terminal.ParentEncounterAttemptId,
                terminal.Mode,
                terminal.State,
                terminal.TerminalOutcome,
                terminal.EncounterElapsedMicros,
                checked(terminal.Revision + 1L),
                computed);
            return OutcomePlan(
                ChampionEncounterOutcomePlanStatus.Computed,
                computed,
                diagnostics,
                terminal,
                after,
                new[] { "EncounterOutcomeComputed" });
        }

        private static bool ValidateDefinition(
            ChampionEncounterDefinitionSnapshot definition,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (definition == null)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-DEFINITION-MISSING",
                    "definition",
                    "Encounter definition snapshot is null.",
                    null,
                    null));
                return false;
            }

            string[] requiredIds =
            {
                definition.GameId,
                definition.CatalogSetId,
                definition.RequiredProfileId,
                definition.EncounterDefinitionId,
                definition.ChampionDefinitionId,
                definition.ChampionCombatProfileId,
                definition.SkillLoadoutId,
                definition.BossDefinitionId,
                definition.BossCombatProfileId,
                definition.CombatRulesProfileId,
                definition.ArenaProfileId,
                definition.NeutralRealmContextId,
                definition.ExpectedProfileRevision
            };
            bool valid =
                requiredIds.All(CombatPrimitiveValidation.IsStableId) &&
                CombatPrimitiveValidation.IsSupportedSchemaVersion(
                    definition.SchemaVersion) &&
                CombatPrimitiveValidation.IsVersion(
                    definition.ContentVersion) &&
                Enum.IsDefined(
                    typeof(CombatEncounterMode),
                    definition.Mode);
            if (!valid)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-DEFINITION-INVALID",
                    "definition",
                    "Encounter definition identity, version, or mode is invalid.",
                    definition,
                    null));
            }

            if (definition.AllowedAuthoritativeRealmInputCount >
                    MaximumAuthoritativeRealms ||
                definition.AllowedAuthoritativeRealmIds.Count !=
                    definition.AllowedAuthoritativeRealmInputCount)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-REALM-LIMIT",
                    "definition.allowedAuthoritativeRealmIds",
                    "Encounter realm set exceeds its technical maximum.",
                    definition,
                    null));
                valid = false;
            }

            if (IsAuthoritative(definition.Mode))
            {
                var realmIds =
                    new HashSet<string>(StringComparer.Ordinal);
                if (definition.AllowedAuthoritativeRealmIds.Count == 0 ||
                    !CombatPrimitiveValidation.IsVersion(
                        definition.RequiredRealmDefinitionVersion))
                {
                    valid = false;
                }

                foreach (string realmId in
                         definition.AllowedAuthoritativeRealmIds)
                {
                    if (!CombatPrimitiveValidation.IsStableId(realmId) ||
                        !realmIds.Add(realmId) ||
                        StringComparer.Ordinal.Equals(
                            realmId,
                            definition.NeutralRealmContextId))
                    {
                        valid = false;
                    }
                }

                if (!valid)
                {
                    diagnostics.Add(RequestError(
                        "AL-ENCOUNTER-REQUEST-REALM-DEFINITION",
                        "definition.realm",
                        "Authoritative encounter requires a valid committed realm set and definition version.",
                        definition,
                        null));
                }
            }
            else if (definition.AllowedAuthoritativeRealmInputCount != 0 ||
                     definition.AllowedAuthoritativeRealmIds.Count != 0 ||
                     !string.IsNullOrEmpty(
                         definition.RequiredRealmDefinitionVersion))
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-PRACTICE-REALM-AUTHORITY",
                    "definition.realm",
                    "Practice and development-demo definitions cannot retain authoritative realm IDs or versions.",
                    definition,
                    null));
                valid = false;
            }

            return valid;
        }

        private static bool ValidateRequestRequiredIds(
            ChampionEncounterRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (request == null)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-MISSING",
                    "request",
                    "Encounter request is null.",
                    null,
                    null));
                return false;
            }

            string[] requiredIds =
            {
                request.GameId,
                request.CatalogSetId,
                request.ProfileId,
                request.EncounterDefinitionId,
                request.EncounterSessionId,
                request.EncounterAttemptId,
                request.EncounterResultId,
                request.ChampionDefinitionId,
                request.ChampionCombatProfileId,
                request.SkillLoadoutId,
                request.BossDefinitionId,
                request.BossCombatProfileId,
                request.ExpectedProfileRevision
            };
            bool valid =
                requiredIds.All(CombatPrimitiveValidation.IsStableId) &&
                CombatPrimitiveValidation.IsVersion(
                    request.EncounterDefinitionContentVersion) &&
                Enum.IsDefined(
                    typeof(CombatEncounterMode),
                    request.Mode) &&
                (string.IsNullOrEmpty(request.ResumeToken) ||
                 CombatPrimitiveValidation.IsStableId(
                     request.ResumeToken));
            if (!valid)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-INVALID-ID",
                    "request",
                    "Encounter request contains an invalid required stable ID or mode.",
                    null,
                    request));
            }

            return valid;
        }

        private static bool MatchesSource(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request)
        {
            return StringComparer.Ordinal.Equals(
                       definition.GameId,
                       request.GameId) &&
                   StringComparer.Ordinal.Equals(
                       definition.CatalogSetId,
                       request.CatalogSetId) &&
                   StringComparer.Ordinal.Equals(
                       definition.RequiredProfileId,
                       request.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       definition.EncounterDefinitionId,
                       request.EncounterDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       definition.ContentVersion,
                       request.EncounterDefinitionContentVersion) &&
                   StringComparer.Ordinal.Equals(
                       definition.ChampionDefinitionId,
                       request.ChampionDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       definition.ChampionCombatProfileId,
                       request.ChampionCombatProfileId) &&
                   StringComparer.Ordinal.Equals(
                       definition.SkillLoadoutId,
                       request.SkillLoadoutId) &&
                   StringComparer.Ordinal.Equals(
                       definition.BossDefinitionId,
                       request.BossDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       definition.BossCombatProfileId,
                       request.BossCombatProfileId);
        }

        private static ChampionEncounterRequestStatus ValidateModeContext(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (!IsAuthoritative(request.Mode))
            {
                if (!string.IsNullOrEmpty(request.RewardOperationId))
                {
                    diagnostics.Add(RequestError(
                        "AL-ENCOUNTER-REQUEST-REWARD-PROHIBITED",
                        "request.rewardOperationId",
                        "Practice and development-demo encounters cannot carry a reward operation.",
                        definition,
                        request));
                    return ChampionEncounterRequestStatus
                        .RejectedRewardProhibited;
                }

                if (!string.IsNullOrEmpty(
                        request.QuestOrProgressionContextId))
                {
                    diagnostics.Add(RequestError(
                        "AL-ENCOUNTER-REQUEST-QUEST-PROHIBITED",
                        "request.questOrProgressionContextId",
                        "Practice and development-demo encounters cannot carry progression authority.",
                        definition,
                        request));
                    return ChampionEncounterRequestStatus
                        .RejectedQuestContextProhibited;
                }

                if (!StringComparer.Ordinal.Equals(
                        request.CommittedRealmId,
                        definition.NeutralRealmContextId) ||
                    !string.IsNullOrEmpty(
                        request.CommittedRealmDefinitionVersion))
                {
                    diagnostics.Add(RequestError(
                        "AL-ENCOUNTER-REQUEST-NEUTRAL-REALM",
                        "request.committedRealmId",
                        "Practice and development-demo encounters require the explicit neutral context and no committed realm version.",
                        definition,
                        request));
                    return ChampionEncounterRequestStatus
                        .RejectedRealmMismatch;
                }

                return ChampionEncounterRequestStatus.Resolved;
            }

            if (!CombatPrimitiveValidation.IsStableId(
                    request.CommittedRealmId) ||
                !CombatPrimitiveValidation.IsVersion(
                    request.CommittedRealmDefinitionVersion))
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-REALM-REQUIRED",
                    "request.committedRealmId",
                    "Authoritative encounter requires a committed valid realm and definition version.",
                    definition,
                    request));
                return ChampionEncounterRequestStatus
                    .RejectedRealmRequired;
            }

            if (!definition.AllowedAuthoritativeRealmIds.Contains(
                    request.CommittedRealmId,
                    StringComparer.Ordinal) ||
                !StringComparer.Ordinal.Equals(
                    definition.RequiredRealmDefinitionVersion,
                    request.CommittedRealmDefinitionVersion))
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-REALM-MISMATCH",
                    "request.committedRealmId",
                    "Committed realm or definition version does not match the source snapshot.",
                    definition,
                    request));
                return ChampionEncounterRequestStatus
                    .RejectedRealmMismatch;
            }

            if (!CombatPrimitiveValidation.IsStableId(
                    request.RewardOperationId))
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-REWARD-REQUIRED",
                    "request.rewardOperationId",
                    "Authoritative encounter requires one stable reward operation identity.",
                    definition,
                    request));
                return ChampionEncounterRequestStatus
                    .RejectedRewardRequired;
            }

            if (request.Mode ==
                CombatEncounterMode.AuthoritativeQuest)
            {
                if (!CombatPrimitiveValidation.IsStableId(
                        request.QuestOrProgressionContextId))
                {
                    diagnostics.Add(RequestError(
                        "AL-ENCOUNTER-REQUEST-QUEST-REQUIRED",
                        "request.questOrProgressionContextId",
                        "Authoritative quest encounter requires one stable progression context.",
                        definition,
                        request));
                    return ChampionEncounterRequestStatus
                        .RejectedQuestContextRequired;
                }
            }
            else if (!string.IsNullOrEmpty(
                         request.QuestOrProgressionContextId))
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-QUEST-PROHIBITED",
                    "request.questOrProgressionContextId",
                    "Authoritative boss request cannot invent quest authority.",
                    definition,
                    request));
                return ChampionEncounterRequestStatus
                    .RejectedQuestContextProhibited;
            }

            return ChampionEncounterRequestStatus.Resolved;
        }

        private static bool ValidateState(
            ChampionEncounterStateSnapshot state,
            ICollection<CombatDiagnostic> diagnostics)
        {
            bool valid =
                state != null &&
                CombatPrimitiveValidation.IsStableId(
                    state.EncounterSessionId) &&
                CombatPrimitiveValidation.IsStableId(
                    state.EncounterAttemptId) &&
                CombatPrimitiveValidation.IsStableId(
                    state.EncounterResultId) &&
                (IsAuthoritative(state.Mode)
                    ? CombatPrimitiveValidation.IsStableId(
                        state.RewardOperationId)
                    : string.IsNullOrEmpty(
                        state.RewardOperationId)) &&
                CombatPrimitiveValidation.IsSha256(
                    state.SourceSnapshotHash) &&
                CombatPrimitiveValidation.IsStableId(
                    state.ChampionCombatProfileId) &&
                CombatPrimitiveValidation.IsStableId(
                    state.BossCombatProfileId) &&
                (string.IsNullOrEmpty(state.ParentEncounterAttemptId) ||
                 CombatPrimitiveValidation.IsStableId(
                     state.ParentEncounterAttemptId)) &&
                Enum.IsDefined(
                    typeof(CombatEncounterMode),
                    state.Mode) &&
                Enum.IsDefined(
                    typeof(CombatEncounterState),
                    state.State) &&
                Enum.IsDefined(
                    typeof(ChampionEncounterTerminalOutcome),
                    state.TerminalOutcome) &&
                CombatPrimitiveValidation.IsMicrosInRange(
                    state.EncounterElapsedMicros,
                    CombatScalarKind.Duration,
                    false) &&
                state.Revision >= 0L;
            if (valid)
            {
                valid = TerminalOutcomeMatchesState(
                    state.TerminalOutcome,
                    state.State) &&
                        ValidateFrozenOutcome(state);

                if (state.State == CombatEncounterState.Completed)
                {
                    valid =
                        state.FrozenOutcome != null &&
                        state.TerminalOutcome ==
                            TerminalOutcomeFor(
                                state.FrozenOutcome.Outcome);
                }

                if (state.FrozenOutcome != null &&
                    state.State != CombatEncounterState.Resolving &&
                    state.State !=
                        CombatEncounterState.CompletionPendingCommit &&
                    !state.IsTerminal)
                {
                    valid = false;
                }
            }

            if (!valid)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-INVARIANT",
                    "state",
                    "Encounter state snapshot violates lifecycle invariants.",
                    state,
                    null));
            }

            return valid;
        }

        private static bool ValidateFrozenOutcome(
            ChampionEncounterStateSnapshot state)
        {
            ChampionEncounterComputedOutcome frozen =
                state.FrozenOutcome;
            if (frozen == null)
            {
                return true;
            }

            ChampionEncounterResolutionEvidence evidence =
                frozen.ResolutionEvidence;
            if (evidence == null ||
                !evidence.IsPlannerIssued ||
                !StringComparer.Ordinal.Equals(
                    frozen.EncounterSessionId,
                    state.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    frozen.EncounterAttemptId,
                    state.EncounterAttemptId) ||
                !StringComparer.Ordinal.Equals(
                    frozen.EncounterResultId,
                    state.EncounterResultId) ||
                frozen.Mode != state.Mode ||
                !Enum.IsDefined(
                    typeof(ChampionEncounterOutcome),
                    frozen.Outcome) ||
                !CombatPrimitiveValidation.IsStableId(
                    frozen.ChampionParticipantId) ||
                !CombatPrimitiveValidation.IsStableId(
                    frozen.BossParticipantId) ||
                StringComparer.Ordinal.Equals(
                    frozen.ChampionParticipantId,
                    frozen.BossParticipantId) ||
                frozen.EncounterDurationMicros >
                    state.EncounterElapsedMicros ||
                (state.State == CombatEncounterState.Resolving &&
                 frozen.EncounterDurationMicros !=
                     state.EncounterElapsedMicros) ||
                !StringComparer.Ordinal.Equals(
                    frozen.SourceSnapshotHash,
                    state.SourceSnapshotHash) ||
                !CombatPrimitiveValidation.IsSha256(
                    frozen.SourceSnapshotHash) ||
                !CombatPrimitiveValidation.IsSha256(
                    frozen.OutcomeHash) ||
                frozen.RewardEligible !=
                    (IsAuthoritative(state.Mode) &&
                     frozen.Outcome ==
                         ChampionEncounterOutcome.ChampionVictory) ||
                !StringComparer.Ordinal.Equals(
                    evidence.EncounterSessionId,
                    state.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.EncounterAttemptId,
                    state.EncounterAttemptId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.SourceSnapshotHash,
                    state.SourceSnapshotHash) ||
                !StringComparer.Ordinal.Equals(
                    evidence.ChampionCombatProfileId,
                    state.ChampionCombatProfileId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.BossCombatProfileId,
                    state.BossCombatProfileId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.ChampionParticipantId,
                    frozen.ChampionParticipantId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.BossParticipantId,
                    frozen.BossParticipantId) ||
                evidence.Outcome != frozen.Outcome ||
                evidence.ResolutionElapsedMicros !=
                    frozen.EncounterDurationMicros ||
                evidence.ExpectedEncounterRevision < 0L ||
                evidence.ExpectedEncounterRevision >= state.Revision ||
                (evidence.Outcome ==
                     ChampionEncounterOutcome.ChampionVictory
                    ? evidence.ChampionLifeState !=
                          CombatantLifeState.Alive ||
                      evidence.BossLifeState !=
                          CombatantLifeState.Defeated
                    : evidence.Outcome !=
                          ChampionEncounterOutcome.ChampionDefeat ||
                      evidence.ChampionLifeState !=
                          CombatantLifeState.Defeated ||
                      evidence.BossLifeState !=
                          CombatantLifeState.Alive) ||
                !StringComparer.Ordinal.Equals(
                    evidence.EvidenceHash,
                    ResolutionEvidenceHash(
                        evidence.EncounterSessionId,
                        evidence.EncounterAttemptId,
                        evidence.SourceSnapshotHash,
                        evidence.ChampionCombatProfileId,
                        evidence.BossCombatProfileId,
                        evidence.ChampionParticipantId,
                        evidence.BossParticipantId,
                        evidence.ChampionLifeState,
                        evidence.BossLifeState,
                        evidence.ChampionResourceRevision,
                        evidence.BossStateRevision,
                        evidence.ExpectedEncounterRevision,
                        evidence.ResolutionElapsedMicros,
                        evidence.Outcome)) ||
                frozen.Metrics == null ||
                frozen.Metrics.Count > MaximumOutcomeMetrics)
            {
                return false;
            }

            var metricIds =
                new HashSet<string>(StringComparer.Ordinal);
            string previousMetricId = null;
            for (int index = 0;
                 index < frozen.Metrics.Count;
                 index++)
            {
                EncounterMetricSnapshot metric =
                    frozen.Metrics[index];
                if (metric == null ||
                    !CombatPrimitiveValidation.IsStableId(
                        metric.MetricId) ||
                    !metricIds.Add(metric.MetricId) ||
                    (previousMetricId != null &&
                     StringComparer.Ordinal.Compare(
                         previousMetricId,
                         metric.MetricId) >= 0) ||
                    !Enum.IsDefined(
                        typeof(CombatScalarKind),
                        metric.Kind) ||
                    !CombatPrimitiveValidation.IsStableId(
                        metric.UnitProfileId) ||
                    !CombatPrimitiveValidation.IsMicrosInRange(
                        metric.ValueMicros,
                        metric.Kind,
                        false))
                {
                    return false;
                }

                previousMetricId = metric.MetricId;
            }

            return StringComparer.Ordinal.Equals(
                frozen.OutcomeHash,
                ComputeOutcomeHash(
                    state,
                    frozen.Outcome,
                    frozen.ChampionParticipantId,
                    frozen.BossParticipantId,
                    frozen.EncounterDurationMicros,
                    frozen.Metrics,
                    frozen.SourceSnapshotHash,
                    evidence.EvidenceHash));
        }

        private static bool ValidateTransitionRequest(
            ChampionEncounterTransitionRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            bool valid =
                request != null &&
                CombatPrimitiveValidation.IsStableId(
                    request.TransitionId) &&
                CombatPrimitiveValidation.IsStableId(
                    request.EncounterSessionId) &&
                CombatPrimitiveValidation.IsStableId(
                    request.EncounterAttemptId) &&
                Enum.IsDefined(
                    typeof(CombatEncounterState),
                    request.TargetState) &&
                Enum.IsDefined(
                    typeof(ChampionEncounterTerminalOutcome),
                    request.TerminalOutcome);
            if (!valid)
            {
                diagnostics.Add(StateError(
                    "AL-ENCOUNTER-STATE-INVALID-REQUEST",
                    "request",
                    "Encounter transition request is malformed.",
                    null,
                    request));
            }

            return valid;
        }

        private static bool IsAllowedTransition(
            CombatEncounterState from,
            CombatEncounterState to,
            CombatEncounterMode mode)
        {
            switch (from)
            {
                case CombatEncounterState.Created:
                    return to == CombatEncounterState.Validating;
                case CombatEncounterState.Validating:
                    return to == CombatEncounterState.Ready ||
                           to == CombatEncounterState.Failed;
                case CombatEncounterState.Ready:
                    return to == CombatEncounterState.Intro ||
                           to == CombatEncounterState.Cancelled;
                case CombatEncounterState.Intro:
                    return to == CombatEncounterState.Active ||
                           to == CombatEncounterState.Cancelled ||
                           to == CombatEncounterState.Failed;
                case CombatEncounterState.Active:
                    return to == CombatEncounterState.Resolving ||
                           to == CombatEncounterState.Failed ||
                           to == CombatEncounterState.Cancelled;
                case CombatEncounterState.Resolving:
                    return to == CombatEncounterState.Failed ||
                           to == CombatEncounterState.RecoveryRequired ||
                           (IsAuthoritative(mode)
                               ? to == CombatEncounterState
                                   .CompletionPendingCommit
                               : to == CombatEncounterState.Completed);
                case CombatEncounterState.CompletionPendingCommit:
                    return IsAuthoritative(mode) &&
                           (to == CombatEncounterState.Completed ||
                            to == CombatEncounterState.Failed ||
                            to == CombatEncounterState
                                .RecoveryRequired);
                case CombatEncounterState.Completed:
                case CombatEncounterState.Failed:
                case CombatEncounterState.Cancelled:
                case CombatEncounterState.RecoveryRequired:
                    return to == CombatEncounterState.Disposed;
                default:
                    return false;
            }
        }

        private static bool OutcomeMatchesTerminal(
            ChampionEncounterOutcome outcome,
            ChampionEncounterTerminalOutcome terminalOutcome)
        {
            switch (terminalOutcome)
            {
                case ChampionEncounterTerminalOutcome.ChampionVictory:
                    return outcome ==
                        ChampionEncounterOutcome.ChampionVictory;
                case ChampionEncounterTerminalOutcome.ChampionDefeat:
                    return outcome ==
                        ChampionEncounterOutcome.ChampionDefeat;
                case ChampionEncounterTerminalOutcome.Cancelled:
                    return outcome ==
                        ChampionEncounterOutcome.Cancelled;
                case ChampionEncounterTerminalOutcome.ValidationFailure:
                    return outcome ==
                        ChampionEncounterOutcome.ValidationFailure;
                case ChampionEncounterTerminalOutcome.RuntimeFailure:
                    return outcome ==
                        ChampionEncounterOutcome.RuntimeFailure;
                case ChampionEncounterTerminalOutcome.RecoveryRequired:
                    return outcome ==
                        ChampionEncounterOutcome.RecoveryRequired;
                default:
                    return false;
            }
        }

        private static ChampionEncounterTerminalOutcome
            TerminalOutcomeFor(ChampionEncounterOutcome outcome)
        {
            switch (outcome)
            {
                case ChampionEncounterOutcome.ChampionVictory:
                    return ChampionEncounterTerminalOutcome
                        .ChampionVictory;
                case ChampionEncounterOutcome.ChampionDefeat:
                    return ChampionEncounterTerminalOutcome
                        .ChampionDefeat;
                case ChampionEncounterOutcome.Cancelled:
                    return ChampionEncounterTerminalOutcome.Cancelled;
                case ChampionEncounterOutcome.ValidationFailure:
                    return ChampionEncounterTerminalOutcome
                        .ValidationFailure;
                case ChampionEncounterOutcome.RuntimeFailure:
                    return ChampionEncounterTerminalOutcome
                        .RuntimeFailure;
                case ChampionEncounterOutcome.RecoveryRequired:
                    return ChampionEncounterTerminalOutcome
                        .RecoveryRequired;
                default:
                    return ChampionEncounterTerminalOutcome.None;
            }
        }

        private static bool ValidateResolutionEvidence(
            ChampionEncounterStateSnapshot resolving,
            ChampionEncounterResolutionEvidence evidence)
        {
            if (resolving == null ||
                evidence == null ||
                !evidence.IsPlannerIssued ||
                !StringComparer.Ordinal.Equals(
                    evidence.EncounterSessionId,
                    resolving.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.EncounterAttemptId,
                    resolving.EncounterAttemptId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.SourceSnapshotHash,
                    resolving.SourceSnapshotHash) ||
                !StringComparer.Ordinal.Equals(
                    evidence.ChampionCombatProfileId,
                    resolving.ChampionCombatProfileId) ||
                !StringComparer.Ordinal.Equals(
                    evidence.BossCombatProfileId,
                    resolving.BossCombatProfileId) ||
                !CombatPrimitiveValidation.IsStableId(
                    evidence.ChampionParticipantId) ||
                !CombatPrimitiveValidation.IsStableId(
                    evidence.BossParticipantId) ||
                StringComparer.Ordinal.Equals(
                    evidence.ChampionParticipantId,
                    evidence.BossParticipantId) ||
                evidence.ChampionResourceRevision < 0L ||
                evidence.BossStateRevision < 0L ||
                evidence.ExpectedEncounterRevision < 0L ||
                evidence.ResolutionElapsedMicros !=
                    resolving.EncounterElapsedMicros ||
                !CombatPrimitiveValidation.IsSha256(
                    evidence.EvidenceHash))
            {
                return false;
            }

            bool revisionMatches =
                resolving.FrozenOutcome == null
                    ? evidence.ExpectedEncounterRevision ==
                        resolving.Revision
                    : evidence.ExpectedEncounterRevision !=
                          long.MaxValue &&
                      evidence.ExpectedEncounterRevision + 1L ==
                          resolving.Revision;
            bool lifeAndOutcomeMatch =
                evidence.Outcome ==
                    ChampionEncounterOutcome.ChampionVictory
                    ? evidence.ChampionLifeState ==
                          CombatantLifeState.Alive &&
                      evidence.BossLifeState ==
                          CombatantLifeState.Defeated
                    : evidence.Outcome ==
                          ChampionEncounterOutcome.ChampionDefeat &&
                      evidence.ChampionLifeState ==
                          CombatantLifeState.Defeated &&
                      evidence.BossLifeState ==
                          CombatantLifeState.Alive;
            return revisionMatches &&
                   lifeAndOutcomeMatch &&
                   StringComparer.Ordinal.Equals(
                       evidence.EvidenceHash,
                       ResolutionEvidenceHash(
                           evidence.EncounterSessionId,
                           evidence.EncounterAttemptId,
                           evidence.SourceSnapshotHash,
                           evidence.ChampionCombatProfileId,
                           evidence.BossCombatProfileId,
                           evidence.ChampionParticipantId,
                           evidence.BossParticipantId,
                           evidence.ChampionLifeState,
                           evidence.BossLifeState,
                           evidence.ChampionResourceRevision,
                           evidence.BossStateRevision,
                           evidence.ExpectedEncounterRevision,
                           evidence.ResolutionElapsedMicros,
                           evidence.Outcome));
        }

        private static string ResolutionEvidenceHash(
            string encounterSessionId,
            string encounterAttemptId,
            string sourceSnapshotHash,
            string championCombatProfileId,
            string bossCombatProfileId,
            string championParticipantId,
            string bossParticipantId,
            CombatantLifeState championLifeState,
            CombatantLifeState bossLifeState,
            long championResourceRevision,
            long bossStateRevision,
            long expectedEncounterRevision,
            long resolutionElapsedMicros,
            ChampionEncounterOutcome outcome)
        {
            return ComputeSha256(
                CombatC1CanonicalFingerprint.Build(
                    encounterSessionId,
                    encounterAttemptId,
                    sourceSnapshotHash,
                    championCombatProfileId,
                    bossCombatProfileId,
                    championParticipantId,
                    bossParticipantId,
                    CombatC1CanonicalFingerprint.Integer(
                        (int)championLifeState),
                    CombatC1CanonicalFingerprint.Integer(
                        (int)bossLifeState),
                    CombatC1CanonicalFingerprint.Long(
                        championResourceRevision),
                    CombatC1CanonicalFingerprint.Long(
                        bossStateRevision),
                    CombatC1CanonicalFingerprint.Long(
                        expectedEncounterRevision),
                    CombatC1CanonicalFingerprint.Long(
                        resolutionElapsedMicros),
                    CombatC1CanonicalFingerprint.Integer(
                        (int)outcome)));
        }

        private static string ComputeOutcomeHash(
            ChampionEncounterStateSnapshot terminal,
            ChampionEncounterOutcome outcome,
            string championParticipantId,
            string bossParticipantId,
            long encounterDurationMicros,
            IReadOnlyList<EncounterMetricSnapshot> orderedMetrics,
            string sourceSnapshotHash,
            string resolutionEvidenceHash)
        {
            var builder = new StringBuilder();
            AppendHashField(builder, terminal.EncounterSessionId);
            AppendHashField(builder, terminal.EncounterAttemptId);
            AppendHashField(builder, terminal.EncounterResultId);
            AppendHashField(
                builder,
                ((int)terminal.Mode).ToString(
                    CultureInfo.InvariantCulture));
            AppendHashField(
                builder,
                ((int)outcome).ToString(
                    CultureInfo.InvariantCulture));
            AppendHashField(builder, championParticipantId);
            AppendHashField(builder, bossParticipantId);
            AppendHashField(
                builder,
                encounterDurationMicros.ToString(
                    CultureInfo.InvariantCulture));
            AppendHashField(builder, sourceSnapshotHash);
            AppendHashField(builder, resolutionEvidenceHash);
            AppendHashField(
                builder,
                orderedMetrics.Count.ToString(
                    CultureInfo.InvariantCulture));
            foreach (EncounterMetricSnapshot metric in orderedMetrics)
            {
                AppendHashField(builder, metric.MetricId);
                AppendHashField(
                    builder,
                    ((int)metric.Kind).ToString(
                        CultureInfo.InvariantCulture));
                AppendHashField(
                    builder,
                    metric.ValueMicros.ToString(
                        CultureInfo.InvariantCulture));
                AppendHashField(builder, metric.UnitProfileId);
            }

            return ComputeSha256(builder.ToString());
        }

        private static string ComputeSha256(string value)
        {
            byte[] payload = Encoding.UTF8.GetBytes(
                value ?? string.Empty);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(payload);
            }

            var hex = new StringBuilder(
                CombatTechnicalLimits.Sha256HexCharacters);
            foreach (byte item in digest)
            {
                hex.Append(item.ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static void AppendHashField(
            StringBuilder builder,
            string raw)
        {
            string value = raw ?? string.Empty;
            builder.Append(value.Length.ToString(
                CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }

        private static bool IsModeSpecificTransition(
            CombatEncounterState from,
            CombatEncounterState to)
        {
            return from == CombatEncounterState.Resolving &&
                   (to == CombatEncounterState.Completed ||
                    to == CombatEncounterState
                        .CompletionPendingCommit);
        }

        private static bool TerminalOutcomeMatchesState(
            ChampionEncounterTerminalOutcome outcome,
            CombatEncounterState state)
        {
            switch (state)
            {
                case CombatEncounterState.Completed:
                    return outcome ==
                               ChampionEncounterTerminalOutcome
                                   .ChampionVictory ||
                           outcome ==
                               ChampionEncounterTerminalOutcome
                                   .ChampionDefeat;
                case CombatEncounterState.Failed:
                    return outcome ==
                               ChampionEncounterTerminalOutcome
                                   .ValidationFailure ||
                           outcome ==
                               ChampionEncounterTerminalOutcome
                                   .RuntimeFailure;
                case CombatEncounterState.Cancelled:
                    return outcome ==
                        ChampionEncounterTerminalOutcome.Cancelled;
                case CombatEncounterState.RecoveryRequired:
                    return outcome ==
                        ChampionEncounterTerminalOutcome.RecoveryRequired;
                case CombatEncounterState.Disposed:
                    return outcome !=
                        ChampionEncounterTerminalOutcome.None;
                default:
                    return outcome ==
                        ChampionEncounterTerminalOutcome.None;
            }
        }

        private static bool IsTerminalState(
            CombatEncounterState state)
        {
            return state == CombatEncounterState.Completed ||
                   state == CombatEncounterState.Failed ||
                   state == CombatEncounterState.Cancelled ||
                   state == CombatEncounterState.RecoveryRequired ||
                   state == CombatEncounterState.Disposed;
        }

        private static bool RetryAllowed(
            ChampionEncounterDefinitionSnapshot definition,
            CombatEncounterState state)
        {
            return definition != null &&
                   (state == CombatEncounterState.Completed &&
                    definition.AllowsRetryAfterCompleted ||
                    state == CombatEncounterState.Failed &&
                    definition.AllowsRetryAfterFailed ||
                    state == CombatEncounterState.Cancelled &&
                    definition.AllowsRetryAfterCancelled);
        }

        private static bool RetryContextMatches(
            ChampionEncounterRequest previous,
            ChampionEncounterRequest retry)
        {
            return previous.Mode == retry.Mode &&
                   StringComparer.Ordinal.Equals(
                       previous.GameId,
                       retry.GameId) &&
                   StringComparer.Ordinal.Equals(
                       previous.CatalogSetId,
                       retry.CatalogSetId) &&
                   StringComparer.Ordinal.Equals(
                       previous.ProfileId,
                       retry.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       previous.EncounterDefinitionId,
                       retry.EncounterDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       previous.EncounterDefinitionContentVersion,
                       retry.EncounterDefinitionContentVersion) &&
                   StringComparer.Ordinal.Equals(
                       previous.EncounterSessionId,
                       retry.EncounterSessionId) &&
                   StringComparer.Ordinal.Equals(
                       previous.ChampionDefinitionId,
                       retry.ChampionDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       previous.ChampionCombatProfileId,
                       retry.ChampionCombatProfileId) &&
                   StringComparer.Ordinal.Equals(
                       previous.SkillLoadoutId,
                       retry.SkillLoadoutId) &&
                   StringComparer.Ordinal.Equals(
                       previous.BossDefinitionId,
                       retry.BossDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       previous.BossCombatProfileId,
                       retry.BossCombatProfileId) &&
                   StringComparer.Ordinal.Equals(
                       previous.CommittedRealmId,
                       retry.CommittedRealmId) &&
                   StringComparer.Ordinal.Equals(
                       previous.CommittedRealmDefinitionVersion,
                       retry.CommittedRealmDefinitionVersion) &&
                   StringComparer.Ordinal.Equals(
                       previous.QuestOrProgressionContextId,
                       retry.QuestOrProgressionContextId) &&
                   StringComparer.Ordinal.Equals(
                       previous.ResumeToken,
                       retry.ResumeToken) &&
                   StringComparer.Ordinal.Equals(
                       previous.ExpectedProfileRevision,
                       retry.ExpectedProfileRevision);
        }

        private static bool DefinitionSemanticEquals(
            ChampionEncounterDefinitionSnapshot left,
            ChampionEncounterDefinitionSnapshot right)
        {
            return left != null &&
                   right != null &&
                   StringComparer.Ordinal.Equals(
                       left.GameId,
                       right.GameId) &&
                   StringComparer.Ordinal.Equals(
                       left.CatalogSetId,
                       right.CatalogSetId) &&
                   StringComparer.Ordinal.Equals(
                       left.RequiredProfileId,
                       right.RequiredProfileId) &&
                   StringComparer.Ordinal.Equals(
                       left.EncounterDefinitionId,
                       right.EncounterDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       left.SchemaVersion,
                       right.SchemaVersion) &&
                   StringComparer.Ordinal.Equals(
                       left.ContentVersion,
                       right.ContentVersion) &&
                   left.Mode == right.Mode &&
                   StringComparer.Ordinal.Equals(
                       left.ChampionDefinitionId,
                       right.ChampionDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       left.ChampionCombatProfileId,
                       right.ChampionCombatProfileId) &&
                   StringComparer.Ordinal.Equals(
                       left.SkillLoadoutId,
                       right.SkillLoadoutId) &&
                   StringComparer.Ordinal.Equals(
                       left.BossDefinitionId,
                       right.BossDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       left.BossCombatProfileId,
                       right.BossCombatProfileId) &&
                   StringComparer.Ordinal.Equals(
                       left.CombatRulesProfileId,
                       right.CombatRulesProfileId) &&
                   StringComparer.Ordinal.Equals(
                       left.ArenaProfileId,
                       right.ArenaProfileId) &&
                   StringComparer.Ordinal.Equals(
                       left.NeutralRealmContextId,
                       right.NeutralRealmContextId) &&
                   StringComparer.Ordinal.Equals(
                       left.RequiredRealmDefinitionVersion,
                       right.RequiredRealmDefinitionVersion) &&
                   StringComparer.Ordinal.Equals(
                       left.ExpectedProfileRevision,
                       right.ExpectedProfileRevision) &&
                   left.UsesDevelopmentFallbackSource ==
                       right.UsesDevelopmentFallbackSource &&
                   left.AllowsRetryAfterCompleted ==
                       right.AllowsRetryAfterCompleted &&
                   left.AllowsRetryAfterFailed ==
                       right.AllowsRetryAfterFailed &&
                   left.AllowsRetryAfterCancelled ==
                       right.AllowsRetryAfterCancelled &&
                   left.AllowedAuthoritativeRealmIds
                       .OrderBy(value => value, StringComparer.Ordinal)
                       .SequenceEqual(
                           right.AllowedAuthoritativeRealmIds
                               .OrderBy(
                                   value => value,
                                   StringComparer.Ordinal),
                           StringComparer.Ordinal);
        }

        private static bool IsAuthoritative(
            CombatEncounterMode mode)
        {
            return mode == CombatEncounterMode.AuthoritativeBoss ||
                   mode == CombatEncounterMode.AuthoritativeQuest;
        }

        private static ResolvedChampionEncounterSnapshot Resolve(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request,
            string fingerprint)
        {
            bool authoritative = IsAuthoritative(request.Mode);
            return new ResolvedChampionEncounterSnapshot(
                definition,
                request,
                fingerprint,
                ComputeSha256(fingerprint),
                authoritative,
                false);
        }

        private static bool SemanticEquals(
            ChampionEncounterDefinitionSnapshot leftDefinition,
            ChampionEncounterRequest left,
            ChampionEncounterDefinitionSnapshot rightDefinition,
            ChampionEncounterRequest right)
        {
            return StringComparer.Ordinal.Equals(
                       Fingerprint(leftDefinition, left),
                       Fingerprint(rightDefinition, right));
        }

        private static string Fingerprint(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request)
        {
            var fields = new List<string>
            {
                definition.GameId,
                definition.CatalogSetId,
                definition.RequiredProfileId,
                definition.EncounterDefinitionId,
                definition.SchemaVersion,
                definition.ContentVersion,
                CombatC1CanonicalFingerprint.Integer(
                    (int)definition.Mode),
                definition.ChampionDefinitionId,
                definition.ChampionCombatProfileId,
                definition.SkillLoadoutId,
                definition.BossDefinitionId,
                definition.BossCombatProfileId,
                definition.CombatRulesProfileId,
                definition.ArenaProfileId,
                definition.NeutralRealmContextId,
                definition.RequiredRealmDefinitionVersion,
                definition.ExpectedProfileRevision,
                definition.UsesDevelopmentFallbackSource ? "1" : "0",
                definition.AllowsRetryAfterCompleted ? "1" : "0",
                definition.AllowsRetryAfterFailed ? "1" : "0",
                definition.AllowsRetryAfterCancelled ? "1" : "0",
                CombatC1CanonicalFingerprint.Integer(
                    definition.AllowedAuthoritativeRealmIds.Count)
            };
            fields.AddRange(
                definition.AllowedAuthoritativeRealmIds
                    .OrderBy(value => value, StringComparer.Ordinal));
            fields.AddRange(new[]
            {
                request.GameId,
                request.CatalogSetId,
                request.ProfileId,
                request.EncounterDefinitionId,
                request.EncounterDefinitionContentVersion,
                request.EncounterSessionId,
                request.EncounterAttemptId,
                request.EncounterResultId,
                request.Mode.ToString(),
                request.ChampionDefinitionId,
                request.ChampionCombatProfileId,
                request.SkillLoadoutId,
                request.BossDefinitionId,
                request.BossCombatProfileId,
                request.CommittedRealmId,
                request.CommittedRealmDefinitionVersion,
                request.QuestOrProgressionContextId,
                request.RewardOperationId,
                request.ResumeToken,
                request.ExpectedProfileRevision
            });
            return CombatC1CanonicalFingerprint.Build(
                fields.ToArray());
        }

        private static string TransitionFingerprint(
            ChampionEncounterTransitionRequest request)
        {
            return CombatC1CanonicalFingerprint.Build(
                request.TransitionId,
                request.EncounterSessionId,
                request.EncounterAttemptId,
                CombatC1CanonicalFingerprint.Integer(
                    (int)request.TargetState),
                CombatC1CanonicalFingerprint.Integer(
                    (int)request.TerminalOutcome),
                CombatC1CanonicalFingerprint.Long(
                    request.AtEncounterMicros),
                CombatC1CanonicalFingerprint.Long(
                    request.ExpectedRevision));
        }

        private static bool ValidateCorrelations(
            IList<ChampionEncounterRequestCorrelation> correlations,
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (correlations == null)
            {
                return true;
            }

            bool valid = true;
            var sessions = new HashSet<string>(StringComparer.Ordinal);
            var attempts = new HashSet<string>(StringComparer.Ordinal);
            var results = new HashSet<string>(StringComparer.Ordinal);
            var rewards = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < correlations.Count; index++)
            {
                ChampionEncounterRequestCorrelation correlation =
                    correlations[index];
                ChampionEncounterRequest existing =
                    correlation?.Request;
                ChampionEncounterDefinitionSnapshot existingDefinition =
                    correlation?.Definition;
                bool rowValid =
                    correlation != null &&
                    existingDefinition != null &&
                    existing != null &&
                    IsStructurallyValidCorrelation(
                        existingDefinition,
                        existing);
                if (rowValid)
                {
                    rowValid =
                        attempts.Add(existing.EncounterAttemptId) &&
                        results.Add(existing.EncounterResultId) &&
                        (string.IsNullOrEmpty(
                             existing.RewardOperationId) ||
                         rewards.Add(existing.RewardOperationId)) &&
                        (!correlation.IsActive ||
                         sessions.Add(existing.EncounterSessionId));
                }

                if (!rowValid)
                {
                    diagnostics.Add(RequestError(
                        "AL-ENCOUNTER-REQUEST-CORRELATION-LEDGER-INVALID",
                        "correlations[" + index + "]",
                        "Encounter correlation row is null, malformed, or duplicates an active session or reserved attempt, result, or reward identity.",
                        definition,
                        request));
                    valid = false;
                }
            }

            return valid;
        }

        private static bool IsStructurallyValidCorrelation(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request)
        {
            if (definition == null || request == null)
            {
                return false;
            }

            var diagnostics = new List<CombatDiagnostic>();
            return ValidateDefinition(definition, diagnostics) &&
                   ValidateRequestRequiredIds(request, diagnostics) &&
                   request.Mode == definition.Mode &&
                   MatchesSource(definition, request) &&
                   StringComparer.Ordinal.Equals(
                       request.ExpectedProfileRevision,
                       definition.ExpectedProfileRevision) &&
                   (!IsAuthoritative(request.Mode) ||
                    !definition.UsesDevelopmentFallbackSource) &&
                   ValidateModeContext(
                       definition,
                       request,
                       diagnostics) ==
                       ChampionEncounterRequestStatus.Resolved;
        }

        private static bool ValidateResolvedSnapshot(
            ResolvedChampionEncounterSnapshot resolved,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (resolved?.Definition == null ||
                resolved.Request == null)
            {
                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-RESOLVED-MISSING",
                    "resolved",
                    "Resolved encounter snapshot is missing its definition or request.",
                    resolved?.Definition,
                    resolved?.Request));
                return false;
            }

            var localDiagnostics = new List<CombatDiagnostic>();
            bool valid =
                ValidateDefinition(
                    resolved.Definition,
                    localDiagnostics) &&
                ValidateRequestRequiredIds(
                    resolved.Request,
                    localDiagnostics) &&
                resolved.Request.Mode == resolved.Definition.Mode &&
                MatchesSource(
                    resolved.Definition,
                    resolved.Request) &&
                StringComparer.Ordinal.Equals(
                    resolved.Request.ExpectedProfileRevision,
                    resolved.Definition.ExpectedProfileRevision) &&
                ValidateModeContext(
                    resolved.Definition,
                    resolved.Request,
                    localDiagnostics) ==
                    ChampionEncounterRequestStatus.Resolved &&
                (!IsAuthoritative(resolved.Request.Mode) ||
                 !resolved.Definition.UsesDevelopmentFallbackSource) &&
                StringComparer.Ordinal.Equals(
                    resolved.SemanticFingerprint,
                    Fingerprint(
                        resolved.Definition,
                        resolved.Request)) &&
                StringComparer.Ordinal.Equals(
                    resolved.SourceSnapshotHash,
                    ComputeSha256(resolved.SemanticFingerprint)) &&
                resolved.HasDurableResultAuthority ==
                    IsAuthoritative(resolved.Request.Mode) &&
                !resolved.RewardEligible;
            if (!valid)
            {
                foreach (CombatDiagnostic diagnostic in localDiagnostics)
                {
                    diagnostics.Add(diagnostic);
                }

                diagnostics.Add(RequestError(
                    "AL-ENCOUNTER-REQUEST-RESOLVED-FORGED",
                    "resolved",
                    "Resolved encounter snapshot does not reproduce from its immutable definition and request.",
                    resolved.Definition,
                    resolved.Request));
            }

            return valid;
        }

        private static bool OptionalStableId(string value)
        {
            return string.IsNullOrEmpty(value) ||
                   CombatPrimitiveValidation.IsStableId(value);
        }

        private static bool ValidateTransitionReceipts(
            IList<ChampionEncounterTransitionReceipt> receipts,
            ChampionEncounterStateSnapshot current,
            ChampionEncounterTransitionRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (receipts == null)
            {
                return true;
            }

            bool valid = true;
            var transitionIds =
                new HashSet<string>(StringComparer.Ordinal);
            var appliedBeforeRevisions = new HashSet<long>();
            var appliedReceipts =
                new List<ChampionEncounterTransitionReceipt>();
            for (int index = 0; index < receipts.Count; index++)
            {
                ChampionEncounterTransitionReceipt receipt =
                    receipts[index];
                bool rowValid =
                    receipt != null &&
                    receipt.IsPlannerIssued &&
                    receipt.Request != null &&
                    CombatPrimitiveValidation.IsStableId(
                        receipt.TransitionId) &&
                    CombatC1CanonicalFingerprint.IsCanonical(
                        receipt.RequestFingerprint) &&
                    IsRetainableTransitionRequestShape(
                        receipt.Request) &&
                    StringComparer.Ordinal.Equals(
                        receipt.RequestFingerprint,
                        TransitionFingerprint(receipt.Request)) &&
                    StringComparer.Ordinal.Equals(
                        receipt.TransitionId,
                        receipt.Request.TransitionId) &&
                    StringComparer.Ordinal.Equals(
                        receipt.Request.EncounterSessionId,
                        current.EncounterSessionId) &&
                    StringComparer.Ordinal.Equals(
                        receipt.Request.EncounterAttemptId,
                        current.EncounterAttemptId) &&
                    Enum.IsDefined(
                        typeof(ChampionEncounterTransitionStatus),
                        receipt.Status) &&
                    (receipt.Status ==
                         ChampionEncounterTransitionStatus.Applied ||
                     receipt.Status ==
                         ChampionEncounterTransitionStatus
                             .NoChangeTerminal) &&
                    Enum.IsDefined(
                        typeof(CombatEncounterState),
                        receipt.BeforeState) &&
                    Enum.IsDefined(
                        typeof(CombatEncounterState),
                        receipt.AfterState) &&
                    Enum.IsDefined(
                        typeof(ChampionEncounterTerminalOutcome),
                        receipt.BeforeTerminalOutcome) &&
                    Enum.IsDefined(
                        typeof(ChampionEncounterTerminalOutcome),
                        receipt.AfterTerminalOutcome) &&
                    TerminalOutcomeMatchesState(
                        receipt.BeforeTerminalOutcome,
                        receipt.BeforeState) &&
                    CombatPrimitiveValidation.IsMicrosInRange(
                        receipt.BeforeEncounterElapsedMicros,
                        CombatScalarKind.Duration,
                        false) &&
                    CombatPrimitiveValidation.IsMicrosInRange(
                        receipt.AfterEncounterElapsedMicros,
                        CombatScalarKind.Duration,
                        false) &&
                    receipt.AfterEncounterElapsedMicros <=
                        current.EncounterElapsedMicros &&
                    CombatPrimitiveValidation.IsSha256(
                        receipt.BeforeSourceSnapshotHash) &&
                    CombatPrimitiveValidation.IsSha256(
                        receipt.AfterSourceSnapshotHash) &&
                    StringComparer.Ordinal.Equals(
                        receipt.BeforeSourceSnapshotHash,
                        receipt.AfterSourceSnapshotHash) &&
                    StringComparer.Ordinal.Equals(
                        receipt.AfterSourceSnapshotHash,
                        current.SourceSnapshotHash) &&
                    (string.IsNullOrEmpty(
                         receipt.BeforeFrozenOutcomeHash) ||
                     CombatPrimitiveValidation.IsSha256(
                         receipt.BeforeFrozenOutcomeHash)) &&
                    (string.IsNullOrEmpty(
                         receipt.AfterFrozenOutcomeHash) ||
                     CombatPrimitiveValidation.IsSha256(
                         receipt.AfterFrozenOutcomeHash)) &&
                    receipt.HadFrozenOutcome ==
                        !string.IsNullOrEmpty(
                            receipt.BeforeFrozenOutcomeHash) &&
                    receipt.Request.AtEncounterMicros >=
                        receipt.BeforeEncounterElapsedMicros &&
                    receipt.BeforeRevision >= 0L &&
                    receipt.AfterRevision >= receipt.BeforeRevision &&
                    receipt.AfterRevision <= current.Revision &&
                    receipt.Request.ExpectedRevision ==
                        receipt.BeforeRevision &&
                    transitionIds.Add(receipt.TransitionId);
                if (rowValid &&
                    receipt.Status ==
                        ChampionEncounterTransitionStatus.Applied)
                {
                    rowValid =
                        receipt.BeforeRevision != long.MaxValue &&
                        receipt.AfterRevision ==
                            receipt.BeforeRevision + 1L &&
                        receipt.AfterState ==
                            receipt.Request.TargetState &&
                        IsAllowedTransition(
                            receipt.BeforeState,
                            receipt.AfterState,
                            current.Mode) &&
                        receipt.AfterTerminalOutcome ==
                            (receipt.AfterState ==
                                 CombatEncounterState.Disposed
                                ? receipt.BeforeTerminalOutcome
                                : receipt.Request.TerminalOutcome) &&
                        receipt.Request.TerminalOutcome ==
                            receipt.AfterTerminalOutcome &&
                        TerminalOutcomeMatchesState(
                            receipt.AfterTerminalOutcome,
                            receipt.AfterState) &&
                        receipt.AfterEncounterElapsedMicros ==
                            receipt.Request.AtEncounterMicros &&
                        StringComparer.Ordinal.Equals(
                            receipt.BeforeFrozenOutcomeHash,
                            receipt.AfterFrozenOutcomeHash) &&
                        appliedBeforeRevisions.Add(
                            receipt.BeforeRevision) &&
                        ((!receipt.HadFrozenOutcome &&
                          receipt.AfterState !=
                              CombatEncounterState
                                  .CompletionPendingCommit &&
                          receipt.AfterState !=
                              CombatEncounterState.Completed) ||
                         receipt.HadFrozenOutcome);
                }
                else if (rowValid)
                {
                    rowValid =
                        receipt.AfterRevision ==
                            receipt.BeforeRevision &&
                        receipt.AfterState == receipt.BeforeState &&
                        receipt.AfterTerminalOutcome ==
                            receipt.BeforeTerminalOutcome &&
                        receipt.AfterEncounterElapsedMicros ==
                            receipt.BeforeEncounterElapsedMicros &&
                        StringComparer.Ordinal.Equals(
                            receipt.BeforeFrozenOutcomeHash,
                            receipt.AfterFrozenOutcomeHash) &&
                        (receipt.BeforeState ==
                             CombatEncounterState.Disposed ||
                         (IsTerminalState(receipt.BeforeState) &&
                          receipt.Request.TargetState !=
                              CombatEncounterState.Disposed));
                }

                if (rowValid &&
                    receipt.AfterRevision == current.Revision)
                {
                    rowValid =
                        receipt.AfterState == current.State &&
                        receipt.AfterTerminalOutcome ==
                            current.TerminalOutcome &&
                        receipt.AfterEncounterElapsedMicros ==
                            current.EncounterElapsedMicros &&
                        StringComparer.Ordinal.Equals(
                            receipt.AfterSourceSnapshotHash,
                            current.SourceSnapshotHash) &&
                        StringComparer.Ordinal.Equals(
                            receipt.AfterFrozenOutcomeHash,
                            current.FrozenOutcome?.OutcomeHash ??
                            string.Empty);
                }

                if (rowValid &&
                    receipt.Status ==
                        ChampionEncounterTransitionStatus.Applied)
                {
                    appliedReceipts.Add(receipt);
                }

                if (!rowValid)
                {
                    diagnostics.Add(StateError(
                        "AL-ENCOUNTER-STATE-REPLAY-LEDGER-INVALID",
                        "replayReceipts[" + index + "]",
                        "Encounter transition receipt is null, malformed, duplicated, or revision-inconsistent.",
                        current,
                        request));
                    valid = false;
                }
            }

            if (valid)
            {
                var afterByRevision =
                    appliedReceipts.ToDictionary(
                        value => value.AfterRevision);
                foreach (ChampionEncounterTransitionReceipt next in
                         appliedReceipts)
                {
                    if (!afterByRevision.TryGetValue(
                            next.BeforeRevision,
                            out ChampionEncounterTransitionReceipt
                                previous))
                    {
                        continue;
                    }

                    if (previous.AfterState != next.BeforeState ||
                        previous.AfterTerminalOutcome !=
                            next.BeforeTerminalOutcome ||
                        previous.AfterEncounterElapsedMicros !=
                            next.BeforeEncounterElapsedMicros ||
                        !StringComparer.Ordinal.Equals(
                            previous.AfterSourceSnapshotHash,
                            next.BeforeSourceSnapshotHash) ||
                        !StringComparer.Ordinal.Equals(
                            previous.AfterFrozenOutcomeHash,
                            next.BeforeFrozenOutcomeHash))
                    {
                        diagnostics.Add(StateError(
                            "AL-ENCOUNTER-STATE-REPLAY-CONTINUITY",
                            "replayReceipts",
                            "Encounter replay ledger contains a forked or spliced applied-state chain.",
                            current,
                            request));
                        valid = false;
                        break;
                    }
                }
            }

            return valid;
        }

        private static bool IsRetainableTransitionRequestShape(
            ChampionEncounterTransitionRequest request)
        {
            return
                request != null &&
                CombatPrimitiveValidation.IsStableId(
                    request.TransitionId) &&
                CombatPrimitiveValidation.IsStableId(
                    request.EncounterSessionId) &&
                CombatPrimitiveValidation.IsStableId(
                    request.EncounterAttemptId) &&
                Enum.IsDefined(
                    typeof(CombatEncounterState),
                    request.TargetState) &&
                Enum.IsDefined(
                    typeof(ChampionEncounterTerminalOutcome),
                    request.TerminalOutcome) &&
                CombatPrimitiveValidation.IsMicrosInRange(
                    request.AtEncounterMicros,
                    CombatScalarKind.Duration,
                    false) &&
                request.ExpectedRevision >= 0L;
        }

        private static ChampionEncounterRequestPlan RequestPlan(
            ChampionEncounterRequestStatus status,
            ResolvedChampionEncounterSnapshot resolved,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            return new ChampionEncounterRequestPlan(
                status,
                resolved,
                diagnostics);
        }

        private static ChampionEncounterTransitionPlan TransitionPlan(
            ChampionEncounterTransitionStatus status,
            ChampionEncounterStateSnapshot before,
            ChampionEncounterStateSnapshot after,
            ChampionEncounterTransitionReceipt receipt,
            IEnumerable<string> events,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            return new ChampionEncounterTransitionPlan(
                status,
                before,
                after,
                receipt,
                events,
                diagnostics);
        }

        private static ChampionEncounterOutcomePlan OutcomePlan(
            ChampionEncounterOutcomePlanStatus status,
            ChampionEncounterComputedOutcome computed,
            IEnumerable<CombatDiagnostic> diagnostics,
            ChampionEncounterStateSnapshot before = null,
            ChampionEncounterStateSnapshot after = null,
            IEnumerable<string> technicalEvents = null)
        {
            return new ChampionEncounterOutcomePlan(
                status,
                computed,
                before,
                after,
                technicalEvents,
                diagnostics);
        }

        private static CombatDiagnostic RequestError(
            string code,
            string field,
            string message,
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request)
        {
            return new CombatDiagnostic(
                code,
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.EncounterRequest,
                field,
                message,
                CombatBlockScope.Construction |
                CombatBlockScope.Encounter |
                CombatBlockScope.Result,
                sourceDefinitionId:
                    definition?.EncounterDefinitionId ??
                    request?.EncounterDefinitionId ??
                    string.Empty,
                encounterSessionId:
                    request?.EncounterSessionId ?? string.Empty,
                encounterAttemptId:
                    request?.EncounterAttemptId ?? string.Empty,
                schemaVersion:
                    definition?.SchemaVersion ?? string.Empty,
                contentVersion:
                    definition?.ContentVersion ?? string.Empty,
                policyVersion: CurrentPolicyVersion);
        }

        private static CombatDiagnostic StateError(
            string code,
            string field,
            string message,
            ChampionEncounterStateSnapshot state,
            ChampionEncounterTransitionRequest request)
        {
            return new CombatDiagnostic(
                code,
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.EncounterState,
                field,
                message,
                CombatBlockScope.Encounter |
                CombatBlockScope.Result,
                encounterSessionId:
                    request?.EncounterSessionId ??
                    state?.EncounterSessionId ??
                    string.Empty,
                encounterAttemptId:
                    request?.EncounterAttemptId ??
                    state?.EncounterAttemptId ??
                    string.Empty,
                policyVersion: CurrentPolicyVersion);
        }

        private static CombatDiagnostic OutcomeError(
            string code,
            string field,
            string message,
            ChampionEncounterStateSnapshot state)
        {
            return new CombatDiagnostic(
                code,
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.EncounterResult,
                field,
                message,
                CombatBlockScope.Result |
                CombatBlockScope.Presentation,
                encounterSessionId:
                    state?.EncounterSessionId ?? string.Empty,
                encounterAttemptId:
                    state?.EncounterAttemptId ?? string.Empty,
                policyVersion: CurrentPolicyVersion);
        }
    }
}
