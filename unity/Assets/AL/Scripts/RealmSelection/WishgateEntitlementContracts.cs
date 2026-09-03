using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AL.EditMode.Tests")]

namespace AL.RealmSelection
{
    public enum WishgateEntitlementPhase
    {
        Unearned,
        Earned,
        RewardSelected,
        RewardAppliedPendingCommit,
        Committed
    }

    public enum WishgateOperation
    {
        Earn,
        SelectReward,
        ApplyReward,
        Commit
    }

    public enum WishgateSnapshotStatus
    {
        Available,
        Unavailable,
        Malformed,
        CommitUncertain
    }

    public enum WishgateLookupStatus
    {
        Found,
        Unknown,
        Unavailable
    }

    public enum WishgateDecisionStatus
    {
        Accepted,
        Rejected,
        Unavailable
    }

    public enum WishgatePlanStatus
    {
        Prepared,
        Duplicate,
        NoChange,
        InvalidRequest,
        Unauthorized,
        Ineligible,
        Stale,
        Unsupported,
        Unavailable,
        Corrupt,
        Conflict,
        RecoveryRequired,
        Overflow
    }

    public sealed class WishgateDiagnostic
    {
        public WishgateDiagnostic(string code, string subjectId, string message)
        {
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class WishgateEntitlementState
    {
        public WishgateEntitlementState(
            WishgateEntitlementPhase phase,
            string entitlementId,
            string earnReasonId,
            string rewardId,
            string rewardApplicationId,
            long earnedUtcSeconds,
            long selectedUtcSeconds,
            long appliedUtcSeconds,
            long committedUtcSeconds,
            long revision,
            bool isSupported)
        {
            Phase = phase;
            EntitlementId = entitlementId ?? string.Empty;
            EarnReasonId = earnReasonId ?? string.Empty;
            RewardId = rewardId ?? string.Empty;
            RewardApplicationId = rewardApplicationId ?? string.Empty;
            EarnedUtcSeconds = earnedUtcSeconds;
            SelectedUtcSeconds = selectedUtcSeconds;
            AppliedUtcSeconds = appliedUtcSeconds;
            CommittedUtcSeconds = committedUtcSeconds;
            Revision = revision;
            IsSupported = isSupported;
        }

        public WishgateEntitlementPhase Phase { get; }
        public string EntitlementId { get; }
        public string EarnReasonId { get; }
        public string RewardId { get; }
        public string RewardApplicationId { get; }
        public long EarnedUtcSeconds { get; }
        public long SelectedUtcSeconds { get; }
        public long AppliedUtcSeconds { get; }
        public long CommittedUtcSeconds { get; }
        public long Revision { get; }
        public bool IsSupported { get; }
    }

    public sealed class WishgateTransitionRecord
    {
        public WishgateTransitionRecord(
            string operationId,
            string eventId,
            string correlationId,
            WishgateOperation operation,
            string requestFingerprint,
            string entitlementId,
            string earnReasonId,
            string rewardId,
            string rewardApplicationId,
            WishgateEntitlementPhase resultingPhase,
            long resultingSnapshotRevision,
            long resultingEntitlementRevision,
            long plannedUtcSeconds,
            string resultingStateHash,
            string planHash,
            string postCommitNotificationCorrelationId,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            EntitlementId = entitlementId ?? string.Empty;
            EarnReasonId = earnReasonId ?? string.Empty;
            RewardId = rewardId ?? string.Empty;
            RewardApplicationId = rewardApplicationId ?? string.Empty;
            ResultingPhase = resultingPhase;
            ResultingSnapshotRevision = resultingSnapshotRevision;
            ResultingEntitlementRevision = resultingEntitlementRevision;
            PlannedUtcSeconds = plannedUtcSeconds;
            ResultingStateHash = resultingStateHash ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            PostCommitNotificationCorrelationId =
                postCommitNotificationCorrelationId ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public string EventId { get; }
        public string CorrelationId { get; }
        public WishgateOperation Operation { get; }
        public string RequestFingerprint { get; }
        public string EntitlementId { get; }
        public string EarnReasonId { get; }
        public string RewardId { get; }
        public string RewardApplicationId { get; }
        public WishgateEntitlementPhase ResultingPhase { get; }
        public long ResultingSnapshotRevision { get; }
        public long ResultingEntitlementRevision { get; }
        public long PlannedUtcSeconds { get; }
        public string ResultingStateHash { get; }
        public string PlanHash { get; }
        public string PostCommitNotificationCorrelationId { get; }
        public bool IsSupported { get; }
    }

    public sealed class WishgateTransactionSnapshot
    {
        public WishgateTransactionSnapshot(
            WishgateSnapshotStatus status,
            long revision,
            WishgateEntitlementState entitlement,
            IEnumerable<WishgateTransitionRecord> transitionRecords,
            bool isComplete)
        {
            Status = status;
            Revision = revision;
            Entitlement = entitlement;
            TransitionRecords = transitionRecords == null
                ? null
                : Array.AsReadOnly(transitionRecords.ToArray());
            IsComplete = isComplete;
        }

        public WishgateSnapshotStatus Status { get; }
        public long Revision { get; }
        public WishgateEntitlementState Entitlement { get; }
        public IReadOnlyList<WishgateTransitionRecord> TransitionRecords { get; }
        public bool IsComplete { get; }
    }

    public sealed class WishgateTransactionRequest
    {
        public WishgateTransactionRequest(
            WishgateOperation operation,
            string operationId,
            string eventId,
            string correlationId,
            string actorId,
            string entitlementId,
            string earnReasonId,
            string rewardId,
            string rewardApplicationId,
            long observedUtcSeconds,
            long expectedSnapshotRevision,
            long expectedEntitlementRevision,
            WishgateVerifiedReceipt priorReceipt = null)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            EntitlementId = entitlementId ?? string.Empty;
            EarnReasonId = earnReasonId ?? string.Empty;
            RewardId = rewardId ?? string.Empty;
            RewardApplicationId = rewardApplicationId ?? string.Empty;
            ObservedUtcSeconds = observedUtcSeconds;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
            ExpectedEntitlementRevision = expectedEntitlementRevision;
            PriorReceipt = priorReceipt;
        }

        public WishgateOperation Operation { get; }
        public string OperationId { get; }
        public string EventId { get; }
        public string CorrelationId { get; }
        public string ActorId { get; }
        public string EntitlementId { get; }
        public string EarnReasonId { get; }
        public string RewardId { get; }
        public string RewardApplicationId { get; }
        public long ObservedUtcSeconds { get; }
        public long ExpectedSnapshotRevision { get; }
        public long ExpectedEntitlementRevision { get; }
        public WishgateVerifiedReceipt PriorReceipt { get; }
    }

    public sealed class WishgateRewardApplicationIntent
    {
        internal WishgateRewardApplicationIntent(
            string entitlementId,
            string rewardId,
            string rewardApplicationId,
            string requestFingerprint)
        {
            EntitlementId = entitlementId;
            RewardId = rewardId;
            RewardApplicationId = rewardApplicationId;
            RequestFingerprint = requestFingerprint;
        }

        public string EntitlementId { get; }
        public string RewardId { get; }
        public string RewardApplicationId { get; }
        public string RequestFingerprint { get; }
    }

    public sealed class WishgateTransactionPlan
    {
        internal WishgateTransactionPlan(
            WishgateOperation operation,
            long expectedSnapshotRevision,
            long candidateSnapshotRevision,
            WishgateEntitlementState expectedEntitlement,
            WishgateEntitlementState candidateEntitlement,
            IEnumerable<WishgateTransitionRecord> candidateTransitionRecords,
            WishgateTransitionRecord transitionRecord,
            WishgateRewardApplicationIntent rewardApplication,
            string requestFingerprint,
            string planHash)
        {
            Operation = operation;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
            CandidateSnapshotRevision = candidateSnapshotRevision;
            ExpectedEntitlement = expectedEntitlement;
            CandidateEntitlement = candidateEntitlement;
            CandidateTransitionRecords = Array.AsReadOnly(
                candidateTransitionRecords.ToArray());
            TransitionRecord = transitionRecord;
            RewardApplication = rewardApplication;
            RequestFingerprint = requestFingerprint;
            PlanHash = planHash;
        }

        public WishgateOperation Operation { get; }
        public long ExpectedSnapshotRevision { get; }
        public long CandidateSnapshotRevision { get; }
        public WishgateEntitlementState ExpectedEntitlement { get; }
        public WishgateEntitlementState CandidateEntitlement { get; }
        public IReadOnlyList<WishgateTransitionRecord> CandidateTransitionRecords { get; }
        public WishgateTransitionRecord TransitionRecord { get; }
        public WishgateRewardApplicationIntent RewardApplication { get; }
        public string RequestFingerprint { get; }
        public string PlanHash { get; }
        public bool RequiresRewardApplication => RewardApplication != null;
    }

    public sealed class WishgateVerifiedReceipt
    {
        internal WishgateVerifiedReceipt(
            WishgateTransitionRecord transitionRecord,
            long verifiedSnapshotRevision,
            long verifiedEntitlementRevision,
            string receiptHash)
        {
            TransitionRecord = transitionRecord;
            VerifiedSnapshotRevision = verifiedSnapshotRevision;
            VerifiedEntitlementRevision = verifiedEntitlementRevision;
            ReceiptHash = receiptHash ?? string.Empty;
        }

        public WishgateTransitionRecord TransitionRecord { get; }
        public long VerifiedSnapshotRevision { get; }
        public long VerifiedEntitlementRevision { get; }
        public string ReceiptHash { get; }
        public bool IsFinalCommit =>
            TransitionRecord?.Operation == WishgateOperation.Commit;
        public string PostCommitNotificationCorrelationId =>
            IsFinalCommit
                ? TransitionRecord.PostCommitNotificationCorrelationId
                : string.Empty;
    }

    public sealed class WishgatePlanningResult
    {
        internal WishgatePlanningResult(
            WishgatePlanStatus status,
            WishgateTransactionPlan plan,
            WishgateTransitionRecord existingRecord,
            WishgateVerifiedReceipt existingReceipt,
            IEnumerable<WishgateDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingRecord = existingRecord;
            ExistingReceipt = existingReceipt;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<WishgateDiagnostic>())
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SubjectId, StringComparer.Ordinal)
                .ToArray());
        }

        public WishgatePlanStatus Status { get; }
        public WishgateTransactionPlan Plan { get; }
        public WishgateTransitionRecord ExistingRecord { get; }
        public WishgateVerifiedReceipt ExistingReceipt { get; }
        public IReadOnlyList<WishgateDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == WishgatePlanStatus.Prepared && Plan != null;
    }

    public interface IWishgateTransactionClock
    {
        bool TryGetUtcSeconds(out long utcSeconds);
    }

    public interface IWishgateTransactionAuthority
    {
        WishgateLookupStatus ResolveEarnReason(string earnReasonId);
        WishgateLookupStatus ResolveReward(string rewardId);

        WishgateDecisionStatus EvaluateEligibility(
            WishgateTransactionRequest request,
            RealmGemCatalogSnapshot realmGemCatalog,
            RealmGemCustodySnapshot custodySnapshot);

        WishgateDecisionStatus Authorize(
            WishgateTransactionRequest request,
            WishgateEntitlementState currentEntitlement);
    }
}
