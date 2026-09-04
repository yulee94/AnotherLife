using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Guilds
{
    public enum RaidCallState
    {
        Draft,
        Announced,
        Accepting,
        Ready,
        Countdown,
        Active,
        Completed,
        Cancelled,
        Failed,
        Expired
    }

    public enum RaidInstanceState
    {
        NotLaunched,
        Summoning,
        TeleportingIn,
        Active,
        ExtractPending,
        ExtractingOut,
        Extracted,
        FailedExtract,
        ForceRelease
    }

    public enum RaidParticipantResponse
    {
        NoResponse,
        Join,
        Decline
    }

    public enum RaidTransferState
    {
        NotTransferred,
        TransferInPending,
        InInstance,
        TransferOutPending,
        Returned,
        Indeterminate
    }

    public enum RaidOperation
    {
        AnnounceCall,
        Join,
        Decline,
        Launch,
        TransferIn,
        TransferOut,
        Reconcile,
        Cancel,
        Expire
    }

    public enum RaidReconcileReason
    {
        Duplicate,
        Restart,
        Disconnect,
        PartialTransfer,
        InstanceFailure,
        UnknownOutcome
    }

    public enum RaidOutcomeKind
    {
        None,
        Success,
        Failure,
        Indeterminate
    }

    public sealed class RaidBossSlotDefinition
    {
        public RaidBossSlotDefinition(int slotIndex, string bossProfileId)
        {
            SlotIndex = slotIndex;
            BossProfileId = bossProfileId ?? string.Empty;
        }

        public int SlotIndex { get; }
        public string BossProfileId { get; }
    }

    public sealed class GuildRaidMusterPolicySnapshot
    {
        public GuildRaidMusterPolicySnapshot(
            GuildCatalogStatus status,
            GuildCatalogBinding binding,
            int callWindowMinutes,
            int callsPerGuildPerWeek,
            int parallelSlots,
            int minJoinCount,
            IEnumerable<RaidBossSlotDefinition> bossSlots,
            string closedDungeonTopologyId,
            IEnumerable<string> reservedPublicDungeonIds,
            bool warNeverBypassesConsent,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            CallWindowMinutes = callWindowMinutes;
            CallsPerGuildPerWeek = callsPerGuildPerWeek;
            ParallelSlots = parallelSlots;
            MinJoinCount = minJoinCount;
            BossSlots = bossSlots == null ? null : Array.AsReadOnly(bossSlots.ToArray());
            ClosedDungeonTopologyId = closedDungeonTopologyId ?? string.Empty;
            ReservedPublicDungeonIds = reservedPublicDungeonIds == null
                ? null
                : Array.AsReadOnly(reservedPublicDungeonIds.ToArray());
            WarNeverBypassesConsent = warNeverBypassesConsent;
            IsComplete = isComplete;
        }

        public GuildCatalogStatus Status { get; }
        public GuildCatalogBinding Binding { get; }
        public int CallWindowMinutes { get; }
        public int CallsPerGuildPerWeek { get; }
        public int ParallelSlots { get; }
        public int MinJoinCount { get; }
        public IReadOnlyList<RaidBossSlotDefinition> BossSlots { get; }
        public string ClosedDungeonTopologyId { get; }
        public IReadOnlyList<string> ReservedPublicDungeonIds { get; }
        public bool WarNeverBypassesConsent { get; }
        public bool IsComplete { get; }
    }

    public sealed class RaidParticipantSnapshot
    {
        public RaidParticipantSnapshot(
            string accountId,
            RaidParticipantResponse response,
            RaidTransferState transfer,
            string closedInstanceEnvelopeId,
            string safeReturnEnvelopeId,
            bool grantsReward,
            bool appliesLockout)
        {
            AccountId = accountId ?? string.Empty;
            Response = response;
            Transfer = transfer;
            ClosedInstanceEnvelopeId = closedInstanceEnvelopeId ?? string.Empty;
            SafeReturnEnvelopeId = safeReturnEnvelopeId ?? string.Empty;
            GrantsReward = grantsReward;
            AppliesLockout = appliesLockout;
        }

        public string AccountId { get; }
        public RaidParticipantResponse Response { get; }
        public RaidTransferState Transfer { get; }
        public string ClosedInstanceEnvelopeId { get; }
        public string SafeReturnEnvelopeId { get; }
        public bool GrantsReward { get; }
        public bool AppliesLockout { get; }
    }

    public sealed class RaidInstanceSnapshot
    {
        public RaidInstanceSnapshot(
            RaidInstanceState state,
            string closedInstanceEnvelopeId,
            string closedDungeonTopologyId)
        {
            State = state;
            ClosedInstanceEnvelopeId = closedInstanceEnvelopeId ?? string.Empty;
            ClosedDungeonTopologyId = closedDungeonTopologyId ?? string.Empty;
        }

        public RaidInstanceState State { get; }
        public string ClosedInstanceEnvelopeId { get; }
        public string ClosedDungeonTopologyId { get; }
    }

    public sealed class RaidCallSnapshot
    {
        public RaidCallSnapshot(
            string callId,
            string guildId,
            string actorAccountId,
            RaidCallState state,
            long weekId,
            long seasonEpoch,
            string bossProfileId,
            string closedInstanceId,
            string closedDungeonTopologyId,
            long windowStartUnixSeconds,
            long windowEndUnixSeconds,
            IEnumerable<RaidParticipantSnapshot> participants,
            RaidInstanceSnapshot instance,
            RaidOutcomeKind outcome,
            bool grantsReward,
            bool appliesLockout)
        {
            CallId = callId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            State = state;
            WeekId = weekId;
            SeasonEpoch = seasonEpoch;
            BossProfileId = bossProfileId ?? string.Empty;
            ClosedInstanceId = closedInstanceId ?? string.Empty;
            ClosedDungeonTopologyId = closedDungeonTopologyId ?? string.Empty;
            WindowStartUnixSeconds = windowStartUnixSeconds;
            WindowEndUnixSeconds = windowEndUnixSeconds;
            Participants = participants == null ? null : Array.AsReadOnly(participants.ToArray());
            Instance = instance;
            Outcome = outcome;
            GrantsReward = grantsReward;
            AppliesLockout = appliesLockout;
        }

        public string CallId { get; }
        public string GuildId { get; }
        public string ActorAccountId { get; }
        public RaidCallState State { get; }
        public long WeekId { get; }
        public long SeasonEpoch { get; }
        public string BossProfileId { get; }
        public string ClosedInstanceId { get; }
        public string ClosedDungeonTopologyId { get; }
        public long WindowStartUnixSeconds { get; }
        public long WindowEndUnixSeconds { get; }
        public IReadOnlyList<RaidParticipantSnapshot> Participants { get; }
        public RaidInstanceSnapshot Instance { get; }
        public RaidOutcomeKind Outcome { get; }
        public bool GrantsReward { get; }
        public bool AppliesLockout { get; }
    }

    public sealed class RaidOperationReceipt
    {
        public RaidOperationReceipt(
            string operationId,
            RaidOperation operation,
            string requestFingerprint,
            string callId,
            string guildId,
            string actorAccountId,
            string targetAccountId,
            long resultingRevision,
            string planHash,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            CallId = callId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            TargetAccountId = targetAccountId ?? string.Empty;
            ResultingRevision = resultingRevision;
            PlanHash = planHash ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public RaidOperation Operation { get; }
        public string RequestFingerprint { get; }
        public string CallId { get; }
        public string GuildId { get; }
        public string ActorAccountId { get; }
        public string TargetAccountId { get; }
        public long ResultingRevision { get; }
        public string PlanHash { get; }
        public bool IsSupported { get; }
    }

    public sealed class RaidAuthoritySnapshot
    {
        public RaidAuthoritySnapshot(
            GuildAuthorityStatus status,
            long revision,
            GuildCatalogBinding catalogBinding,
            IEnumerable<RaidCallSnapshot> calls,
            IEnumerable<RaidOperationReceipt> receipts,
            bool isComplete)
        {
            Status = status;
            Revision = revision;
            CatalogBinding = catalogBinding;
            Calls = calls == null ? null : Array.AsReadOnly(calls.ToArray());
            Receipts = receipts == null ? null : Array.AsReadOnly(receipts.ToArray());
            IsComplete = isComplete;
        }

        public GuildAuthorityStatus Status { get; }
        public long Revision { get; }
        public GuildCatalogBinding CatalogBinding { get; }
        public IReadOnlyList<RaidCallSnapshot> Calls { get; }
        public IReadOnlyList<RaidOperationReceipt> Receipts { get; }
        public bool IsComplete { get; }
    }

    public sealed class GuildRaidMusterTransitionRequest
    {
        public GuildRaidMusterTransitionRequest(
            RaidOperation operation,
            string operationId,
            string actorAccountId,
            string guildId,
            string callId,
            string targetAccountId,
            long weekId,
            long seasonEpoch,
            string bossProfileId,
            string closedInstanceId,
            string closedInstanceEnvelopeId,
            string safeReturnEnvelopeId,
            string sceneName,
            long trustedClockUnixSeconds,
            long expectedRaidRevision,
            long expectedGuildRevision,
            bool eligibilityPassed,
            bool zoneAllowed,
            bool generationContinuous,
            bool liveLocationValid,
            RaidReconcileReason reconcileReason,
            GuildCatalogBinding expectedCatalogBinding)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            CallId = callId ?? string.Empty;
            TargetAccountId = targetAccountId ?? string.Empty;
            WeekId = weekId;
            SeasonEpoch = seasonEpoch;
            BossProfileId = bossProfileId ?? string.Empty;
            ClosedInstanceId = closedInstanceId ?? string.Empty;
            ClosedInstanceEnvelopeId = closedInstanceEnvelopeId ?? string.Empty;
            SafeReturnEnvelopeId = safeReturnEnvelopeId ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            TrustedClockUnixSeconds = trustedClockUnixSeconds;
            ExpectedRaidRevision = expectedRaidRevision;
            ExpectedGuildRevision = expectedGuildRevision;
            EligibilityPassed = eligibilityPassed;
            ZoneAllowed = zoneAllowed;
            GenerationContinuous = generationContinuous;
            LiveLocationValid = liveLocationValid;
            ReconcileReason = reconcileReason;
            ExpectedCatalogBinding = expectedCatalogBinding;
        }

        public RaidOperation Operation { get; }
        public string OperationId { get; }
        public string ActorAccountId { get; }
        public string GuildId { get; }
        public string CallId { get; }
        public string TargetAccountId { get; }
        public long WeekId { get; }
        public long SeasonEpoch { get; }
        public string BossProfileId { get; }
        public string ClosedInstanceId { get; }
        public string ClosedInstanceEnvelopeId { get; }
        public string SafeReturnEnvelopeId { get; }
        public string SceneName { get; }
        public long TrustedClockUnixSeconds { get; }
        public long ExpectedRaidRevision { get; }
        public long ExpectedGuildRevision { get; }
        public bool EligibilityPassed { get; }
        public bool ZoneAllowed { get; }
        public bool GenerationContinuous { get; }
        public bool LiveLocationValid { get; }
        public RaidReconcileReason ReconcileReason { get; }
        public GuildCatalogBinding ExpectedCatalogBinding { get; }
    }

    public sealed class GuildRaidMusterTransitionPlan
    {
        internal GuildRaidMusterTransitionPlan(
            RaidOperation operation,
            string requestFingerprint,
            RaidAuthoritySnapshot expectedSnapshot,
            RaidAuthoritySnapshot candidateSnapshot,
            RaidOperationReceipt receipt,
            string planHash)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            ExpectedSnapshot = expectedSnapshot;
            CandidateSnapshot = candidateSnapshot;
            Receipt = receipt;
            PlanHash = planHash ?? string.Empty;
            EffectDomains = Array.AsReadOnly(Array.Empty<GuildEffectDomain>());
        }

        public RaidOperation Operation { get; }
        public string RequestFingerprint { get; }
        public RaidAuthoritySnapshot ExpectedSnapshot { get; }
        public RaidAuthoritySnapshot CandidateSnapshot { get; }
        public RaidOperationReceipt Receipt { get; }
        public string PlanHash { get; }
        public IReadOnlyList<GuildEffectDomain> EffectDomains { get; }
    }

    public sealed class RaidPlanningResult
    {
        internal RaidPlanningResult(
            GuildPlanningStatus status,
            GuildRaidMusterTransitionPlan plan,
            RaidOperationReceipt existingReceipt,
            IEnumerable<GuildDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingReceipt = existingReceipt;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<GuildDiagnostic>())
                .OrderBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.SubjectId, StringComparer.Ordinal)
                .ToArray());
        }

        public GuildPlanningStatus Status { get; }
        public GuildRaidMusterTransitionPlan Plan { get; }
        public RaidOperationReceipt ExistingReceipt { get; }
        public IReadOnlyList<GuildDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == GuildPlanningStatus.Prepared && Plan != null;
    }
}
