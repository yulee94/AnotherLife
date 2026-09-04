using System;
using System.Collections.Generic;
using System.Linq;
using AL.Guilds;

namespace AL.Alliances
{
    public enum AllianceCatalogStatus
    {
        Ready,
        Unavailable,
        UnsupportedVersion,
        Malformed,
        Incomplete
    }

    public enum AllianceAuthorityStatus
    {
        Available,
        Unavailable,
        Malformed,
        UnsupportedReadOnly,
        CommitUncertain
    }

    public enum AllianceRelationState
    {
        Absent,
        Proposed,
        Active,
        Cooldown,
        Suspended
    }

    public enum AllianceWarState
    {
        None,
        Declared,
        Active,
        ReconciliationPending,
        Cooling
    }

    public enum AllianceOperation
    {
        Propose,
        Accept,
        Decline,
        Leave,
        Disband,
        DeclareWar,
        ProposeWarEnd,
        AcceptWarEnd,
        DeclineWarEnd
    }

    public enum AlliancePendingKind
    {
        AllianceProposal,
        WarEnd
    }

    public enum AllianceZoneKind
    {
        Open,
        City,
        Beginner,
        Accordant,
        ForcedSafe
    }

    public enum AllianceHostilityKind
    {
        NotForced,
        ForcedHostile,
        Immune,
        Indeterminate
    }

    public enum AlliancePlanningStatus
    {
        Prepared,
        AlreadyCommitted,
        NoChange,
        InvalidRequest,
        Unauthorized,
        Ineligible,
        NotFound,
        Conflict,
        StaleAuthority,
        StaleAlliance,
        StaleGuild,
        StaleCatalog,
        Unsupported,
        Unavailable,
        Malformed,
        CommitUncertain,
        Overflow,
        Indeterminate
    }

    public sealed class AllianceWarPolicySnapshot
    {
        public AllianceWarPolicySnapshot(
            AllianceCatalogStatus status,
            GuildCatalogBinding binding,
            bool sameRealmOnly,
            bool officersCanFormAlliancesOrDeclareWar,
            int warNoticeHours,
            int warActiveHours,
            AllianceWarState forceHostilityWarState,
            IEnumerable<AllianceZoneKind> immuneZones,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            SameRealmOnly = sameRealmOnly;
            OfficersCanFormAlliancesOrDeclareWar = officersCanFormAlliancesOrDeclareWar;
            WarNoticeHours = warNoticeHours;
            WarActiveHours = warActiveHours;
            ForceHostilityWarState = forceHostilityWarState;
            ImmuneZones = immuneZones == null
                ? null
                : Array.AsReadOnly(immuneZones.ToArray());
            IsComplete = isComplete;
        }

        public AllianceCatalogStatus Status { get; }
        public GuildCatalogBinding Binding { get; }
        public bool SameRealmOnly { get; }
        public bool OfficersCanFormAlliancesOrDeclareWar { get; }
        public int WarNoticeHours { get; }
        public int WarActiveHours { get; }
        public AllianceWarState ForceHostilityWarState { get; }
        public IReadOnlyList<AllianceZoneKind> ImmuneZones { get; }
        public bool IsComplete { get; }
    }

    public sealed class AllianceMemberGuildSnapshot
    {
        public AllianceMemberGuildSnapshot(string guildId, long guildRevision)
        {
            GuildId = guildId ?? string.Empty;
            GuildRevision = guildRevision;
        }

        public string GuildId { get; }
        public long GuildRevision { get; }
    }

    public sealed class AllianceSnapshot
    {
        public AllianceSnapshot(
            string allianceId,
            string immutableRealmId,
            string identityHash,
            long revision,
            AllianceRelationState relation,
            string leadGuildId,
            IEnumerable<AllianceMemberGuildSnapshot> memberGuilds)
        {
            AllianceId = allianceId ?? string.Empty;
            ImmutableRealmId = immutableRealmId ?? string.Empty;
            IdentityHash = identityHash ?? string.Empty;
            Revision = revision;
            Relation = relation;
            LeadGuildId = leadGuildId ?? string.Empty;
            MemberGuilds = memberGuilds == null
                ? null
                : Array.AsReadOnly(memberGuilds.ToArray());
        }

        public string AllianceId { get; }
        public string ImmutableRealmId { get; }
        public string IdentityHash { get; }
        public long Revision { get; }
        public AllianceRelationState Relation { get; }
        public string LeadGuildId { get; }
        public IReadOnlyList<AllianceMemberGuildSnapshot> MemberGuilds { get; }
    }

    public sealed class AlliancePendingRequest
    {
        public AlliancePendingRequest(
            string requestId,
            AlliancePendingKind kind,
            string allianceId,
            string proposerGuildId,
            string targetGuildId,
            string targetAllianceId,
            string actorAccountId,
            long allianceRevision,
            long proposerGuildRevision,
            long targetGuildRevision,
            bool isSupported)
        {
            RequestId = requestId ?? string.Empty;
            Kind = kind;
            AllianceId = allianceId ?? string.Empty;
            ProposerGuildId = proposerGuildId ?? string.Empty;
            TargetGuildId = targetGuildId ?? string.Empty;
            TargetAllianceId = targetAllianceId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            AllianceRevision = allianceRevision;
            ProposerGuildRevision = proposerGuildRevision;
            TargetGuildRevision = targetGuildRevision;
            IsSupported = isSupported;
        }

        public string RequestId { get; }
        public AlliancePendingKind Kind { get; }
        public string AllianceId { get; }
        public string ProposerGuildId { get; }
        public string TargetGuildId { get; }
        public string TargetAllianceId { get; }
        public string ActorAccountId { get; }
        public long AllianceRevision { get; }
        public long ProposerGuildRevision { get; }
        public long TargetGuildRevision { get; }
        public bool IsSupported { get; }
    }

    public sealed class AllianceWarSnapshot
    {
        public AllianceWarSnapshot(
            string warId,
            string attackerAllianceId,
            string defenderAllianceId,
            AllianceWarState committedState,
            long declaredAtUnixSeconds,
            long activatedAtUnixSeconds,
            long attackerAllianceRevision,
            long defenderAllianceRevision)
        {
            WarId = warId ?? string.Empty;
            AttackerAllianceId = attackerAllianceId ?? string.Empty;
            DefenderAllianceId = defenderAllianceId ?? string.Empty;
            CommittedState = committedState;
            DeclaredAtUnixSeconds = declaredAtUnixSeconds;
            ActivatedAtUnixSeconds = activatedAtUnixSeconds;
            AttackerAllianceRevision = attackerAllianceRevision;
            DefenderAllianceRevision = defenderAllianceRevision;
        }

        public string WarId { get; }
        public string AttackerAllianceId { get; }
        public string DefenderAllianceId { get; }
        public AllianceWarState CommittedState { get; }
        public long DeclaredAtUnixSeconds { get; }
        public long ActivatedAtUnixSeconds { get; }
        public long AttackerAllianceRevision { get; }
        public long DefenderAllianceRevision { get; }
    }

    public sealed class AllianceOperationReceipt
    {
        public AllianceOperationReceipt(
            string operationId,
            AllianceOperation operation,
            string requestFingerprint,
            string allianceId,
            string actorAccountId,
            string actorGuildId,
            string targetGuildId,
            string targetAllianceId,
            string pendingRequestId,
            long resultingAuthorityRevision,
            long resultingAllianceRevision,
            string planHash,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            AllianceId = allianceId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            ActorGuildId = actorGuildId ?? string.Empty;
            TargetGuildId = targetGuildId ?? string.Empty;
            TargetAllianceId = targetAllianceId ?? string.Empty;
            PendingRequestId = pendingRequestId ?? string.Empty;
            ResultingAuthorityRevision = resultingAuthorityRevision;
            ResultingAllianceRevision = resultingAllianceRevision;
            PlanHash = planHash ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public AllianceOperation Operation { get; }
        public string RequestFingerprint { get; }
        public string AllianceId { get; }
        public string ActorAccountId { get; }
        public string ActorGuildId { get; }
        public string TargetGuildId { get; }
        public string TargetAllianceId { get; }
        public string PendingRequestId { get; }
        public long ResultingAuthorityRevision { get; }
        public long ResultingAllianceRevision { get; }
        public string PlanHash { get; }
        public bool IsSupported { get; }
    }

    public sealed class AllianceAuthoritySnapshot
    {
        public AllianceAuthoritySnapshot(
            AllianceAuthorityStatus status,
            long revision,
            GuildCatalogBinding catalogBinding,
            IEnumerable<AllianceSnapshot> alliances,
            IEnumerable<AlliancePendingRequest> pendingRequests,
            IEnumerable<AllianceWarSnapshot> wars,
            IEnumerable<AllianceOperationReceipt> receipts,
            bool isComplete)
        {
            Status = status;
            Revision = revision;
            CatalogBinding = catalogBinding;
            Alliances = alliances == null ? null : Array.AsReadOnly(alliances.ToArray());
            PendingRequests = pendingRequests == null
                ? null
                : Array.AsReadOnly(pendingRequests.ToArray());
            Wars = wars == null ? null : Array.AsReadOnly(wars.ToArray());
            Receipts = receipts == null ? null : Array.AsReadOnly(receipts.ToArray());
            IsComplete = isComplete;
        }

        public AllianceAuthorityStatus Status { get; }
        public long Revision { get; }
        public GuildCatalogBinding CatalogBinding { get; }
        public IReadOnlyList<AllianceSnapshot> Alliances { get; }
        public IReadOnlyList<AlliancePendingRequest> PendingRequests { get; }
        public IReadOnlyList<AllianceWarSnapshot> Wars { get; }
        public IReadOnlyList<AllianceOperationReceipt> Receipts { get; }
        public bool IsComplete { get; }
    }

    public sealed class AllianceTransitionRequest
    {
        public AllianceTransitionRequest(
            AllianceOperation operation,
            string operationId,
            string actorAccountId,
            string actorImmutableRealmId,
            string actorGuildId,
            string allianceId,
            string targetGuildId,
            string targetAllianceId,
            string pendingRequestId,
            string warId,
            long expectedAuthorityRevision,
            long expectedAllianceRevision,
            long expectedActorGuildRevision,
            long expectedTargetGuildRevision,
            long clockUnixSeconds,
            GuildCatalogBinding expectedCatalogBinding)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            ActorImmutableRealmId = actorImmutableRealmId ?? string.Empty;
            ActorGuildId = actorGuildId ?? string.Empty;
            AllianceId = allianceId ?? string.Empty;
            TargetGuildId = targetGuildId ?? string.Empty;
            TargetAllianceId = targetAllianceId ?? string.Empty;
            PendingRequestId = pendingRequestId ?? string.Empty;
            WarId = warId ?? string.Empty;
            ExpectedAuthorityRevision = expectedAuthorityRevision;
            ExpectedAllianceRevision = expectedAllianceRevision;
            ExpectedActorGuildRevision = expectedActorGuildRevision;
            ExpectedTargetGuildRevision = expectedTargetGuildRevision;
            ClockUnixSeconds = clockUnixSeconds;
            ExpectedCatalogBinding = expectedCatalogBinding;
        }

        public AllianceOperation Operation { get; }
        public string OperationId { get; }
        public string ActorAccountId { get; }
        public string ActorImmutableRealmId { get; }
        public string ActorGuildId { get; }
        public string AllianceId { get; }
        public string TargetGuildId { get; }
        public string TargetAllianceId { get; }
        public string PendingRequestId { get; }
        public string WarId { get; }
        public long ExpectedAuthorityRevision { get; }
        public long ExpectedAllianceRevision { get; }
        public long ExpectedActorGuildRevision { get; }
        public long ExpectedTargetGuildRevision { get; }
        public long ClockUnixSeconds { get; }
        public GuildCatalogBinding ExpectedCatalogBinding { get; }
    }

    public sealed class AllianceTransitionPlan
    {
        internal AllianceTransitionPlan(
            AllianceOperation operation,
            string requestFingerprint,
            AllianceAuthoritySnapshot expectedSnapshot,
            AllianceAuthoritySnapshot candidateSnapshot,
            AllianceOperationReceipt receipt,
            string planHash)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint;
            ExpectedSnapshot = expectedSnapshot;
            CandidateSnapshot = candidateSnapshot;
            Receipt = receipt;
            PlanHash = planHash;
        }

        public AllianceOperation Operation { get; }
        public string RequestFingerprint { get; }
        public AllianceAuthoritySnapshot ExpectedSnapshot { get; }
        public AllianceAuthoritySnapshot CandidateSnapshot { get; }
        public AllianceOperationReceipt Receipt { get; }
        public string PlanHash { get; }
    }

    public sealed class AllianceDiagnostic
    {
        public AllianceDiagnostic(string code, string subjectId, string message)
        {
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class AlliancePlanningResult
    {
        internal AlliancePlanningResult(
            AlliancePlanningStatus status,
            AllianceTransitionPlan plan,
            AllianceOperationReceipt existingReceipt,
            IEnumerable<AllianceDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingReceipt = existingReceipt;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<AllianceDiagnostic>())
                .OrderBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.SubjectId, StringComparer.Ordinal)
                .ToArray());
        }

        public AlliancePlanningStatus Status { get; }
        public AllianceTransitionPlan Plan { get; }
        public AllianceOperationReceipt ExistingReceipt { get; }
        public IReadOnlyList<AllianceDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == AlliancePlanningStatus.Prepared && Plan != null;
    }

    public sealed class AllianceHostilityQuery
    {
        public AllianceHostilityQuery(
            string actorGuildId,
            string targetGuildId,
            AllianceZoneKind zone,
            long clockUnixSeconds)
        {
            ActorGuildId = actorGuildId ?? string.Empty;
            TargetGuildId = targetGuildId ?? string.Empty;
            Zone = zone;
            ClockUnixSeconds = clockUnixSeconds;
        }

        public string ActorGuildId { get; }
        public string TargetGuildId { get; }
        public AllianceZoneKind Zone { get; }
        public long ClockUnixSeconds { get; }
    }

    public sealed class AllianceHostilityDecision
    {
        internal AllianceHostilityDecision(
            AllianceHostilityKind kind,
            AllianceWarState effectiveWarState,
            bool forcedByActiveWar)
        {
            Kind = kind;
            EffectiveWarState = effectiveWarState;
            ForcedByActiveWar = forcedByActiveWar;
        }

        public AllianceHostilityKind Kind { get; }
        public AllianceWarState EffectiveWarState { get; }
        public bool ForcedByActiveWar { get; }
    }
}
