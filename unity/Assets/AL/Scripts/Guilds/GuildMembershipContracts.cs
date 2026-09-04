using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Guilds
{
    public enum GuildCatalogStatus
    {
        Ready,
        Unavailable,
        UnsupportedVersion,
        Malformed,
        Incomplete
    }

    public enum GuildAuthorityStatus
    {
        Available,
        Unavailable,
        Malformed,
        UnsupportedReadOnly,
        CommitUncertain
    }

    public enum GuildStatus
    {
        Active,
        Disbanded
    }

    public enum GuildRole
    {
        Master,
        Officer,
        Member
    }

    public enum GuildMembershipState
    {
        Active = 0,
        Inactive = 1,
        Restricted = 2,
        PendingLeave = 3,
        Banned = 4
    }

    public enum GuildPendingRequestKind
    {
        Invitation,
        JoinApplication
    }

    public enum GuildOperation
    {
        Create,
        Join,
        Invite,
        Accept,
        Decline,
        Leave,
        Kick,
        Promote,
        Demote,
        MasterTransfer,
        Disband
    }

    public enum GuildEffectDomain
    {
        Combat,
        Economy,
        Perk,
        City,
        Raid
    }

    public enum GuildPlanningStatus
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
        StaleGuild,
        StaleCatalog,
        Unsupported,
        Unavailable,
        Malformed,
        CommitUncertain,
        Overflow
    }

    public sealed class GuildCatalogBinding
    {
        public GuildCatalogBinding(
            int schemaVersion,
            string contentVersion,
            string sourceRevision,
            string catalogHash)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            CatalogHash = catalogHash ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string CatalogHash { get; }
    }

    public sealed class GuildRolePolicy
    {
        public GuildRolePolicy(
            GuildRole role,
            bool canManageInvitations,
            bool canManageMembers,
            bool canPromote,
            bool canDemote,
            bool canTransferMaster,
            bool canDisband,
            bool canFormAlliancesOrDeclareWar,
            bool canOpenRaidCalls)
        {
            Role = role;
            CanManageInvitations = canManageInvitations;
            CanManageMembers = canManageMembers;
            CanPromote = canPromote;
            CanDemote = canDemote;
            CanTransferMaster = canTransferMaster;
            CanDisband = canDisband;
            CanFormAlliancesOrDeclareWar = canFormAlliancesOrDeclareWar;
            CanOpenRaidCalls = canOpenRaidCalls;
        }

        public GuildRole Role { get; }
        public bool CanManageInvitations { get; }
        public bool CanManageMembers { get; }
        public bool CanPromote { get; }
        public bool CanDemote { get; }
        public bool CanTransferMaster { get; }
        public bool CanDisband { get; }
        public bool CanFormAlliancesOrDeclareWar { get; }
        public bool CanOpenRaidCalls { get; }
    }

    public sealed class GuildMembershipStatePolicy
    {
        public GuildMembershipStatePolicy(
            GuildMembershipState state,
            bool reservesAccount,
            bool grantsRoleAuthority,
            GuildMembershipState? leaveResult,
            GuildMembershipState? kickResult,
            bool blocksSameGuildEntry)
        {
            State = state;
            ReservesAccount = reservesAccount;
            GrantsRoleAuthority = grantsRoleAuthority;
            LeaveResult = leaveResult;
            KickResult = kickResult;
            BlocksSameGuildEntry = blocksSameGuildEntry;
        }

        public GuildMembershipState State { get; }
        public bool ReservesAccount { get; }
        public bool GrantsRoleAuthority { get; }
        public GuildMembershipState? LeaveResult { get; }
        public GuildMembershipState? KickResult { get; }
        public bool BlocksSameGuildEntry { get; }
    }

    public sealed class GuildMembershipPolicySnapshot
    {
        public GuildMembershipPolicySnapshot(
            GuildCatalogStatus status,
            GuildCatalogBinding binding,
            IEnumerable<GuildRolePolicy> rolePolicies,
            bool accountFirstWithinImmutableRealm,
            int requiredActiveMasterCount,
            GuildRole defaultJoinedRole,
            IEnumerable<GuildEffectDomain> excludedEffectDomains,
            bool isComplete)
            : this(
                status,
                binding,
                rolePolicies,
                null,
                accountFirstWithinImmutableRealm,
                requiredActiveMasterCount,
                defaultJoinedRole,
                excludedEffectDomains,
                isComplete)
        {
        }

        public GuildMembershipPolicySnapshot(
            GuildCatalogStatus status,
            GuildCatalogBinding binding,
            IEnumerable<GuildRolePolicy> rolePolicies,
            IEnumerable<GuildMembershipStatePolicy> statePolicies,
            bool accountFirstWithinImmutableRealm,
            int requiredActiveMasterCount,
            GuildRole defaultJoinedRole,
            IEnumerable<GuildEffectDomain> excludedEffectDomains,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            RolePolicies = rolePolicies == null
                ? null
                : Array.AsReadOnly(rolePolicies.ToArray());
            StatePolicies = statePolicies == null
                ? null
                : Array.AsReadOnly(statePolicies.ToArray());
            AccountFirstWithinImmutableRealm = accountFirstWithinImmutableRealm;
            RequiredActiveMasterCount = requiredActiveMasterCount;
            DefaultJoinedRole = defaultJoinedRole;
            ExcludedEffectDomains = excludedEffectDomains == null
                ? null
                : Array.AsReadOnly(excludedEffectDomains.ToArray());
            IsComplete = isComplete;
        }

        public GuildCatalogStatus Status { get; }
        public GuildCatalogBinding Binding { get; }
        public IReadOnlyList<GuildRolePolicy> RolePolicies { get; }
        public IReadOnlyList<GuildMembershipStatePolicy> StatePolicies { get; }
        public bool AccountFirstWithinImmutableRealm { get; }
        public int RequiredActiveMasterCount { get; }
        public GuildRole DefaultJoinedRole { get; }
        public IReadOnlyList<GuildEffectDomain> ExcludedEffectDomains { get; }
        public bool IsComplete { get; }
    }

    public sealed class GuildMemberSnapshot
    {
        public GuildMemberSnapshot(
            string accountId,
            string immutableRealmId,
            GuildRole role,
            GuildMembershipState state)
        {
            AccountId = accountId ?? string.Empty;
            ImmutableRealmId = immutableRealmId ?? string.Empty;
            Role = role;
            State = state;
        }

        public string AccountId { get; }
        public string ImmutableRealmId { get; }
        public GuildRole Role { get; }
        public GuildMembershipState State { get; }
    }

    public sealed class GuildSnapshot
    {
        public GuildSnapshot(
            string guildId,
            string immutableRealmId,
            long revision,
            GuildStatus status,
            IEnumerable<GuildMemberSnapshot> members)
        {
            GuildId = guildId ?? string.Empty;
            ImmutableRealmId = immutableRealmId ?? string.Empty;
            Revision = revision;
            Status = status;
            Members = members == null
                ? null
                : Array.AsReadOnly(members.ToArray());
        }

        public string GuildId { get; }
        public string ImmutableRealmId { get; }
        public long Revision { get; }
        public GuildStatus Status { get; }
        public IReadOnlyList<GuildMemberSnapshot> Members { get; }
    }

    public sealed class GuildPendingRequest
    {
        public GuildPendingRequest(
            string requestId,
            GuildPendingRequestKind kind,
            string guildId,
            string accountId,
            string immutableRealmId,
            long guildRevision,
            bool isSupported)
        {
            RequestId = requestId ?? string.Empty;
            Kind = kind;
            GuildId = guildId ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            ImmutableRealmId = immutableRealmId ?? string.Empty;
            GuildRevision = guildRevision;
            IsSupported = isSupported;
        }

        public string RequestId { get; }
        public GuildPendingRequestKind Kind { get; }
        public string GuildId { get; }
        public string AccountId { get; }
        public string ImmutableRealmId { get; }
        public long GuildRevision { get; }
        public bool IsSupported { get; }
    }

    public sealed class GuildOperationReceipt
    {
        public GuildOperationReceipt(
            string operationId,
            GuildOperation operation,
            string requestFingerprint,
            string guildId,
            string actorAccountId,
            string targetAccountId,
            string pendingRequestId,
            long resultingAuthorityRevision,
            long resultingGuildRevision,
            string planHash,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            TargetAccountId = targetAccountId ?? string.Empty;
            PendingRequestId = pendingRequestId ?? string.Empty;
            ResultingAuthorityRevision = resultingAuthorityRevision;
            ResultingGuildRevision = resultingGuildRevision;
            PlanHash = planHash ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public GuildOperation Operation { get; }
        public string RequestFingerprint { get; }
        public string GuildId { get; }
        public string ActorAccountId { get; }
        public string TargetAccountId { get; }
        public string PendingRequestId { get; }
        public long ResultingAuthorityRevision { get; }
        public long ResultingGuildRevision { get; }
        public string PlanHash { get; }
        public bool IsSupported { get; }
    }

    public sealed class GuildAuthoritySnapshot
    {
        public GuildAuthoritySnapshot(
            GuildAuthorityStatus status,
            long revision,
            GuildCatalogBinding catalogBinding,
            IEnumerable<GuildSnapshot> guilds,
            IEnumerable<GuildPendingRequest> pendingRequests,
            IEnumerable<GuildOperationReceipt> receipts,
            bool isComplete)
        {
            Status = status;
            Revision = revision;
            CatalogBinding = catalogBinding;
            Guilds = guilds == null ? null : Array.AsReadOnly(guilds.ToArray());
            PendingRequests = pendingRequests == null
                ? null
                : Array.AsReadOnly(pendingRequests.ToArray());
            Receipts = receipts == null ? null : Array.AsReadOnly(receipts.ToArray());
            IsComplete = isComplete;
        }

        public GuildAuthorityStatus Status { get; }
        public long Revision { get; }
        public GuildCatalogBinding CatalogBinding { get; }
        public IReadOnlyList<GuildSnapshot> Guilds { get; }
        public IReadOnlyList<GuildPendingRequest> PendingRequests { get; }
        public IReadOnlyList<GuildOperationReceipt> Receipts { get; }
        public bool IsComplete { get; }
    }

    public sealed class GuildTransitionRequest
    {
        public GuildTransitionRequest(
            GuildOperation operation,
            string operationId,
            string actorAccountId,
            string actorImmutableRealmId,
            string guildId,
            string targetAccountId,
            string targetImmutableRealmId,
            string pendingRequestId,
            long expectedAuthorityRevision,
            long expectedGuildRevision,
            GuildCatalogBinding expectedCatalogBinding)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            ActorImmutableRealmId = actorImmutableRealmId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            TargetAccountId = targetAccountId ?? string.Empty;
            TargetImmutableRealmId = targetImmutableRealmId ?? string.Empty;
            PendingRequestId = pendingRequestId ?? string.Empty;
            ExpectedAuthorityRevision = expectedAuthorityRevision;
            ExpectedGuildRevision = expectedGuildRevision;
            ExpectedCatalogBinding = expectedCatalogBinding;
        }

        public GuildOperation Operation { get; }
        public string OperationId { get; }
        public string ActorAccountId { get; }
        public string ActorImmutableRealmId { get; }
        public string GuildId { get; }
        public string TargetAccountId { get; }
        public string TargetImmutableRealmId { get; }
        public string PendingRequestId { get; }
        public long ExpectedAuthorityRevision { get; }
        public long ExpectedGuildRevision { get; }
        public GuildCatalogBinding ExpectedCatalogBinding { get; }
    }

    public sealed class GuildTransitionPlan
    {
        internal GuildTransitionPlan(
            GuildOperation operation,
            string requestFingerprint,
            GuildAuthoritySnapshot expectedSnapshot,
            GuildAuthoritySnapshot candidateSnapshot,
            GuildOperationReceipt receipt,
            string planHash)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint;
            ExpectedSnapshot = expectedSnapshot;
            CandidateSnapshot = candidateSnapshot;
            Receipt = receipt;
            PlanHash = planHash;
            EffectDomains = Array.AsReadOnly(Array.Empty<GuildEffectDomain>());
        }

        public GuildOperation Operation { get; }
        public string RequestFingerprint { get; }
        public GuildAuthoritySnapshot ExpectedSnapshot { get; }
        public GuildAuthoritySnapshot CandidateSnapshot { get; }
        public GuildOperationReceipt Receipt { get; }
        public string PlanHash { get; }
        public IReadOnlyList<GuildEffectDomain> EffectDomains { get; }
    }

    public sealed class GuildDiagnostic
    {
        public GuildDiagnostic(string code, string subjectId, string message)
        {
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class GuildPlanningResult
    {
        internal GuildPlanningResult(
            GuildPlanningStatus status,
            GuildTransitionPlan plan,
            GuildOperationReceipt existingReceipt,
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
        public GuildTransitionPlan Plan { get; }
        public GuildOperationReceipt ExistingReceipt { get; }
        public IReadOnlyList<GuildDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == GuildPlanningStatus.Prepared && Plan != null;
    }
}
