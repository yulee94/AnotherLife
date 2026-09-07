using System;
using System.Runtime.CompilerServices;
using AL.Data.Runtime;

[assembly: InternalsVisibleTo("AL.EditMode.Tests")]
[assembly: InternalsVisibleTo("AL.PlayMode.Tests")]

namespace AL.RealmSelection
{
    public enum WishgateCommitStatus
    {
        Unknown = 0,
        Committed = 1,
        Replayed = 2,
        NoChange = 3,
        RejectedInvalidRequest = 4,
        RejectedProfileUnavailable = 5,
        RejectedStale = 6,
        RejectedUnauthorized = 7,
        RejectedIneligible = 8,
        RejectedUnsupported = 9,
        RejectedUnavailable = 10,
        RejectedCorrupt = 11,
        RejectedConflict = 12,
        RejectedReadOnly = 13,
        RejectedSaveUncertain = 14,
        RejectedForward = 15,
        RejectedDegraded = 16,
        RejectedPlanner = 17,
        RejectedRewardApply = 18,
        RecoveryRequired = 19
    }

    public static class WishgateEngineeringIds
    {
        public const string EarnAllRealmGemSignatures = "earn_all_realm_gems";
        public const string SchemaOperationId = "al.save.schema2.wishgate-transaction.v1";
        public const string ActorId = "profile.owner";
    }

    public static class WishgateCommitCodes
    {
        public const string Committed = "AL-WISHGATE-COMMITTED";
        public const string Replayed = "AL-WISHGATE-REPLAYED";
        public const string NoChange = "AL-WISHGATE-NO-CHANGE";
        public const string InvalidRequest = "AL-WISHGATE-REQUEST-INVALID";
        public const string ProfileNotSchemaTwo = "AL-WISHGATE-PROFILE-NOT-SCHEMA-TWO";
        public const string ProfileMismatch = "AL-WISHGATE-PROFILE-MISMATCH";
        public const string StaleBase = "AL-WISHGATE-STALE-BASE";
        public const string ReadOnly = "AL-WISHGATE-READ-ONLY";
        public const string CommitUncertain = "AL-WISHGATE-COMMIT-UNCERTAIN";
        public const string ForwardSchema = "AL-WISHGATE-FORWARD-SCHEMA";
        public const string Collision = "AL-WISHGATE-COLLISION";
        public const string Degraded = "AL-WISHGATE-DEGRADED";
        public const string NotWritable = "AL-WISHGATE-PROFILE-NOT-WRITABLE";
        public const string RewardApplyFailed = "AL-WISHGATE-REWARD-APPLY-FAILED";
        public const string CatalogUnavailable = "AL-WISHGATE-CATALOG-UNAVAILABLE";
        public const string RecoveryRequired = "AL-WISHGATE-COMMIT-UNCERTAIN";
    }

    public sealed class WishgateCommitRequest
    {
        public WishgateCommitRequest(
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
            string expectedProfileId = null,
            string expectedGenerationFingerprint = null,
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
            ExpectedProfileId = expectedProfileId ?? string.Empty;
            ExpectedGenerationFingerprint = expectedGenerationFingerprint ?? string.Empty;
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
        public string ExpectedProfileId { get; }
        public string ExpectedGenerationFingerprint { get; }
        public WishgateVerifiedReceipt PriorReceipt { get; }
    }

    public sealed class WishgateCommitResult
    {
        public WishgateCommitResult(
            WishgateCommitStatus status,
            bool mutationOccurred,
            bool persisted,
            WishgateEntitlementPhase phase,
            string rewardId,
            string rewardApplicationId,
            string postCommitNotificationCorrelationId,
            string receiptHash,
            string technicalCode,
            WishgateVerifiedReceipt receipt)
        {
            Status = status;
            MutationOccurred = mutationOccurred;
            Persisted = persisted;
            Phase = phase;
            RewardId = rewardId ?? string.Empty;
            RewardApplicationId = rewardApplicationId ?? string.Empty;
            PostCommitNotificationCorrelationId =
                postCommitNotificationCorrelationId ?? string.Empty;
            ReceiptHash = receiptHash ?? string.Empty;
            TechnicalCode = technicalCode ?? string.Empty;
            Receipt = receipt;
        }

        public WishgateCommitStatus Status { get; }
        public bool MutationOccurred { get; }
        public bool Persisted { get; }
        public WishgateEntitlementPhase Phase { get; }
        public string RewardId { get; }
        public string RewardApplicationId { get; }
        public string PostCommitNotificationCorrelationId { get; }
        public string ReceiptHash { get; }
        public string TechnicalCode { get; }
        public WishgateVerifiedReceipt Receipt { get; }

        public bool IsFinalVerifiedCommit =>
            (Status == WishgateCommitStatus.Committed ||
             Status == WishgateCommitStatus.Replayed) &&
            Receipt != null &&
            Receipt.IsFinalCommit &&
            !string.IsNullOrEmpty(PostCommitNotificationCorrelationId);
    }

    internal interface IWishgateRewardApplicator
    {
        bool TryApply(
            SaveGameData candidate,
            WishgateRewardApplicationIntent intent,
            out string diagnosticCode);
    }

    internal sealed class WishgateDurableDependencies
    {
        public WishgateDurableDependencies(
            RealmGemCatalogSnapshot catalog,
            IWishgateTransactionClock clock,
            IWishgateTransactionAuthority authority,
            IWishgateRewardApplicator applicator)
        {
            Catalog = catalog;
            Clock = clock;
            Authority = authority;
            Applicator = applicator;
        }

        public RealmGemCatalogSnapshot Catalog { get; }
        public IWishgateTransactionClock Clock { get; }
        public IWishgateTransactionAuthority Authority { get; }
        public IWishgateRewardApplicator Applicator { get; }

        public bool IsComplete =>
            Catalog != null && Clock != null && Authority != null && Applicator != null;
    }

    internal sealed class IdentityOnlyWishgateRewardApplicator : IWishgateRewardApplicator
    {
        public bool TryApply(
            SaveGameData candidate,
            WishgateRewardApplicationIntent intent,
            out string diagnosticCode)
        {
            diagnosticCode = string.Empty;
            if (candidate == null || intent == null ||
                string.IsNullOrEmpty(intent.RewardApplicationId))
            {
                diagnosticCode = WishgateCommitCodes.RewardApplyFailed;
                return false;
            }

            WishgateTransactionState state =
                candidate.WishgateTransaction ?? new WishgateTransactionState();
            if (string.Equals(
                    state.AppliedRewardApplicationId,
                    intent.RewardApplicationId,
                    StringComparison.Ordinal))
            {
                candidate.WishgateTransaction = state;
                return true;
            }

            state.AppliedRewardApplicationId = intent.RewardApplicationId;
            candidate.WishgateTransaction = state;
            return true;
        }
    }

    internal sealed class WishgateSystemClock : IWishgateTransactionClock
    {
        public bool TryGetUtcSeconds(out long utcSeconds)
        {
            utcSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return utcSeconds > 0;
        }
    }
}
