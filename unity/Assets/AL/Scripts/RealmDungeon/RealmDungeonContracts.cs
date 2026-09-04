using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.RealmDungeon
{
    public enum RealmDungeonLifeState
    {
        GenesisOrRecovery,
        AliveIdle,
        AliveEngaged,
        DefeatCommitPending,
        Cooldown,
        RespawnEligible,
        Manifesting,
        RecoveryRequired
    }

    public enum RealmDungeonOperation
    {
        Observe,
        Engage,
        CommitDefeat,
        RecordNonKill,
        BeginManifestation,
        CompleteManifestation,
        TraversePortal,
        ReportFault
    }

    public enum RealmDungeonPlanningStatus
    {
        Prepared,
        Rejected,
        Unavailable,
        AlreadyCommitted,
        Conflict,
        RecoveryRequired,
        InvalidRequest
    }

    public enum RealmDungeonRejectReason
    {
        None,
        InvalidRequest,
        StaleCatalog,
        GuardianIdentityAlias,
        GuildClosedInstanceAlias,
        DuplicateLease,
        InwardPortalTraversal,
        MissingPresentationBundle,
        UnknownDungeon,
        Unsupported
    }

    public enum RealmDungeonNonKillKind
    {
        None,
        Wipe,
        Leash,
        Timeout,
        AbandonedAttempt,
        OrdinaryMobGrind,
        DamageOnly,
        Disconnect,
        ClientClockManipulation
    }

    public enum RealmDungeonFaultKind
    {
        None,
        TimeRollback,
        TrustedTimeUnavailable,
        CorruptState,
        SplitBrainOwnership
    }

    public enum RealmDungeonPortalTraversal
    {
        None,
        Outward,
        Inward,
        Ambient
    }

    public enum RealmDungeonCatalogStatus
    {
        Ready,
        Unavailable,
        UnsupportedVersion,
        Malformed,
        Incomplete
    }

    public sealed class RealmDungeonCatalogBinding
    {
        public RealmDungeonCatalogBinding(string catalogId, string sourceRevision, string catalogHash)
        {
            CatalogId = catalogId ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            CatalogHash = catalogHash ?? string.Empty;
        }

        public string CatalogId { get; }
        public string SourceRevision { get; }
        public string CatalogHash { get; }
    }

    public sealed class RealmDungeonDefinition
    {
        public RealmDungeonDefinition(
            string dungeonId,
            string realmId,
            IEnumerable<string> entranceIds,
            string portalId,
            string raidDragonId,
            string guardianPresentationRef,
            string presentationBundleId,
            bool presentationApproved,
            bool productionEligible)
        {
            DungeonId = dungeonId ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            EntranceIds = entranceIds == null ? null : Array.AsReadOnly(entranceIds.ToArray());
            PortalId = portalId ?? string.Empty;
            RaidDragonId = raidDragonId ?? string.Empty;
            GuardianPresentationRef = guardianPresentationRef ?? string.Empty;
            PresentationBundleId = presentationBundleId ?? string.Empty;
            PresentationApproved = presentationApproved;
            ProductionEligible = productionEligible;
        }

        public string DungeonId { get; }
        public string RealmId { get; }
        public IReadOnlyList<string> EntranceIds { get; }
        public string PortalId { get; }
        public string RaidDragonId { get; }
        public string GuardianPresentationRef { get; }
        public string PresentationBundleId { get; }
        public bool PresentationApproved { get; }
        public bool ProductionEligible { get; }
    }

    public sealed class RealmDungeonCatalogSnapshot
    {
        public RealmDungeonCatalogSnapshot(
            RealmDungeonCatalogStatus status,
            RealmDungeonCatalogBinding binding,
            long cooldownSeconds,
            bool killOnly,
            bool productionEligible,
            bool genericFallback,
            string guildClosedInstanceIdPrefix,
            IEnumerable<string> guildClosedBossProfileIds,
            IEnumerable<string> guardianCatalogDragonIds,
            IEnumerable<RealmDungeonDefinition> dungeons,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            CooldownSeconds = cooldownSeconds;
            KillOnly = killOnly;
            ProductionEligible = productionEligible;
            GenericFallback = genericFallback;
            GuildClosedInstanceIdPrefix = guildClosedInstanceIdPrefix ?? string.Empty;
            GuildClosedBossProfileIds = guildClosedBossProfileIds == null
                ? null
                : Array.AsReadOnly(guildClosedBossProfileIds.ToArray());
            GuardianCatalogDragonIds = guardianCatalogDragonIds == null
                ? null
                : Array.AsReadOnly(guardianCatalogDragonIds.ToArray());
            Dungeons = dungeons == null ? null : Array.AsReadOnly(dungeons.ToArray());
            IsComplete = isComplete;
        }

        public RealmDungeonCatalogStatus Status { get; }
        public RealmDungeonCatalogBinding Binding { get; }
        public long CooldownSeconds { get; }
        public bool KillOnly { get; }
        public bool ProductionEligible { get; }
        public bool GenericFallback { get; }
        public string GuildClosedInstanceIdPrefix { get; }
        public IReadOnlyList<string> GuildClosedBossProfileIds { get; }
        public IReadOnlyList<string> GuardianCatalogDragonIds { get; }
        public IReadOnlyList<RealmDungeonDefinition> Dungeons { get; }
        public bool IsComplete { get; }
    }

    public sealed class RealmDungeonReceipt
    {
        public RealmDungeonReceipt(
            string operationId,
            string requestFingerprint,
            string defeatIdentity,
            string dungeonId,
            string raidDragonId,
            string instanceId,
            long defeatCommittedAtUnixSeconds,
            long nextEligibleAtUnixSeconds)
        {
            OperationId = operationId ?? string.Empty;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            DefeatIdentity = defeatIdentity ?? string.Empty;
            DungeonId = dungeonId ?? string.Empty;
            RaidDragonId = raidDragonId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            DefeatCommittedAtUnixSeconds = defeatCommittedAtUnixSeconds;
            NextEligibleAtUnixSeconds = nextEligibleAtUnixSeconds;
        }

        public string OperationId { get; }
        public string RequestFingerprint { get; }
        public string DefeatIdentity { get; }
        public string DungeonId { get; }
        public string RaidDragonId { get; }
        public string InstanceId { get; }
        public long DefeatCommittedAtUnixSeconds { get; }
        public long NextEligibleAtUnixSeconds { get; }
    }

    public sealed class RealmDungeonAuthoritySnapshot
    {
        public RealmDungeonAuthoritySnapshot(
            string dungeonId,
            string raidDragonId,
            string instanceId,
            RealmDungeonLifeState lifeState,
            long defeatCommittedAtUnixSeconds,
            long nextEligibleAtUnixSeconds,
            long lastObservedClockUnixSeconds,
            string leaseId,
            string spawnCycleId,
            bool targetable,
            bool invulnerable,
            bool presentationApproved,
            bool productionEligible,
            long revision,
            IEnumerable<RealmDungeonReceipt> receipts)
        {
            DungeonId = dungeonId ?? string.Empty;
            RaidDragonId = raidDragonId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            LifeState = lifeState;
            DefeatCommittedAtUnixSeconds = defeatCommittedAtUnixSeconds;
            NextEligibleAtUnixSeconds = nextEligibleAtUnixSeconds;
            LastObservedClockUnixSeconds = lastObservedClockUnixSeconds;
            LeaseId = leaseId ?? string.Empty;
            SpawnCycleId = spawnCycleId ?? string.Empty;
            Targetable = targetable;
            Invulnerable = invulnerable;
            PresentationApproved = presentationApproved;
            ProductionEligible = productionEligible;
            Revision = revision;
            Receipts = receipts == null ? null : Array.AsReadOnly(receipts.ToArray());
        }

        public string DungeonId { get; }
        public string RaidDragonId { get; }
        public string InstanceId { get; }
        public RealmDungeonLifeState LifeState { get; }
        public long DefeatCommittedAtUnixSeconds { get; }
        public long NextEligibleAtUnixSeconds { get; }
        public long LastObservedClockUnixSeconds { get; }
        public string LeaseId { get; }
        public string SpawnCycleId { get; }
        public bool Targetable { get; }
        public bool Invulnerable { get; }
        public bool PresentationApproved { get; }
        public bool ProductionEligible { get; }
        public long Revision { get; }
        public IReadOnlyList<RealmDungeonReceipt> Receipts { get; }
        public bool RespawnEligible => LifeState == RealmDungeonLifeState.RespawnEligible;
    }

    public sealed class RealmDungeonTransitionRequest
    {
        public RealmDungeonTransitionRequest(
            RealmDungeonOperation operation,
            string operationId,
            string dungeonId,
            string raidDragonId,
            string entranceId,
            string portalId,
            string defeatIdentity,
            string leaseId,
            string spawnCycleId,
            long trustedClockUnixSeconds,
            long expectedRevision,
            RealmDungeonNonKillKind nonKillKind,
            RealmDungeonFaultKind faultKind,
            RealmDungeonPortalTraversal traversal,
            bool presentationApproved,
            RealmDungeonCatalogBinding expectedCatalogBinding)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            DungeonId = dungeonId ?? string.Empty;
            RaidDragonId = raidDragonId ?? string.Empty;
            EntranceId = entranceId ?? string.Empty;
            PortalId = portalId ?? string.Empty;
            DefeatIdentity = defeatIdentity ?? string.Empty;
            LeaseId = leaseId ?? string.Empty;
            SpawnCycleId = spawnCycleId ?? string.Empty;
            TrustedClockUnixSeconds = trustedClockUnixSeconds;
            ExpectedRevision = expectedRevision;
            NonKillKind = nonKillKind;
            FaultKind = faultKind;
            Traversal = traversal;
            PresentationApproved = presentationApproved;
            ExpectedCatalogBinding = expectedCatalogBinding;
        }

        public RealmDungeonOperation Operation { get; }
        public string OperationId { get; }
        public string DungeonId { get; }
        public string RaidDragonId { get; }
        public string EntranceId { get; }
        public string PortalId { get; }
        public string DefeatIdentity { get; }
        public string LeaseId { get; }
        public string SpawnCycleId { get; }
        public long TrustedClockUnixSeconds { get; }
        public long ExpectedRevision { get; }
        public RealmDungeonNonKillKind NonKillKind { get; }
        public RealmDungeonFaultKind FaultKind { get; }
        public RealmDungeonPortalTraversal Traversal { get; }
        public bool PresentationApproved { get; }
        public RealmDungeonCatalogBinding ExpectedCatalogBinding { get; }
    }

    public sealed class RealmDungeonTransitionPlan
    {
        internal RealmDungeonTransitionPlan(
            RealmDungeonOperation operation,
            string requestFingerprint,
            RealmDungeonAuthoritySnapshot expectedSnapshot,
            RealmDungeonAuthoritySnapshot candidateSnapshot,
            RealmDungeonReceipt rewardReceipt)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            ExpectedSnapshot = expectedSnapshot;
            CandidateSnapshot = candidateSnapshot;
            RewardReceipt = rewardReceipt;
        }

        public RealmDungeonOperation Operation { get; }
        public string RequestFingerprint { get; }
        public RealmDungeonAuthoritySnapshot ExpectedSnapshot { get; }
        public RealmDungeonAuthoritySnapshot CandidateSnapshot { get; }
        public RealmDungeonReceipt RewardReceipt { get; }
    }

    public sealed class RealmDungeonPlanningResult
    {
        internal RealmDungeonPlanningResult(
            RealmDungeonPlanningStatus status,
            RealmDungeonRejectReason reason,
            RealmDungeonTransitionPlan plan,
            RealmDungeonReceipt existingReceipt,
            bool usedGenericFallback)
        {
            Status = status;
            Reason = reason;
            Plan = plan;
            ExistingReceipt = existingReceipt;
            UsedGenericFallback = usedGenericFallback;
        }

        public RealmDungeonPlanningStatus Status { get; }
        public RealmDungeonRejectReason Reason { get; }
        public RealmDungeonTransitionPlan Plan { get; }
        public RealmDungeonReceipt ExistingReceipt { get; }
        public bool UsedGenericFallback { get; }
    }
}
