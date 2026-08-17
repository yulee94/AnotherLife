using System;
using System.Security.Cryptography;
using System.Text;
using AL.Data.Runtime;

namespace AL.RealmGems
{
    public enum WishgateRewardStatus
    {
        Committed,
        AlreadyCommitted,
        MissingContext,
        CatalogUnavailable,
        UnknownReward,
        IneligibleActor,
        UnauthorizedRealm,
        DisallowedZone,
        EntitlementMissing,
        UnverifiableAuthority,
        InvalidState,
        IdempotencyConflict,
        EntitlementAlreadyConsumed,
        RewardMutationRejected,
        SaveFailedRolledBack,
        CommitUncertain
    }

    public sealed class WishgateRewardRequest
    {
        public WishgateRewardRequest(
            string operationId,
            string actorId,
            string zoneId,
            string rewardId)
        {
            OperationId = operationId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            RewardId = rewardId ?? string.Empty;
        }

        public string OperationId { get; }
        public string ActorId { get; }
        public string ZoneId { get; }
        public string RewardId { get; }

        internal bool HasValidIdentity =>
            IsIdentifier(OperationId) &&
            IsIdentifier(ActorId) &&
            IsIdentifier(ZoneId) &&
            IsIdentifier(RewardId);

        internal string PayloadFingerprint()
        {
            string canonical = ActorId + "\n" + ZoneId + "\n" + RewardId;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static bool IsIdentifier(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 128 &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    public sealed class WishgateRewardResult
    {
        internal WishgateRewardResult(
            WishgateRewardStatus status,
            string technicalCode,
            string operationId = "",
            string actorId = "",
            string rewardId = "",
            int warzoneCreditsAwarded = 0,
            long committedTimestamp = 0)
        {
            Status = status;
            TechnicalCode = technicalCode ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            RewardId = rewardId ?? string.Empty;
            WarzoneCreditsAwarded = warzoneCreditsAwarded;
            CommittedTimestamp = committedTimestamp;
        }

        public WishgateRewardStatus Status { get; }
        public string TechnicalCode { get; }
        public string OperationId { get; }
        public string ActorId { get; }
        public string RewardId { get; }
        public int WarzoneCreditsAwarded { get; }
        public long CommittedTimestamp { get; }
        public bool IsCommitted =>
            Status == WishgateRewardStatus.Committed ||
            Status == WishgateRewardStatus.AlreadyCommitted;

        internal static WishgateRewardResult FromReceipt(
            WishgateRewardReceiptData receipt,
            WishgateRewardStatus status) =>
            new WishgateRewardResult(
                status,
                TechnicalCodeFor(status),
                receipt?.OperationId,
                receipt?.ActorId,
                receipt?.RewardId,
                receipt?.WarzoneCreditsAwarded ?? 0,
                receipt?.CommittedTimestamp ?? 0);

        private static string TechnicalCodeFor(WishgateRewardStatus status)
        {
            switch (status)
            {
                case WishgateRewardStatus.Committed:
                    return "AL-RGW-REWARD-COMMITTED";
                case WishgateRewardStatus.AlreadyCommitted:
                    return "AL-RGW-REWARD-ALREADY-COMMITTED";
                case WishgateRewardStatus.CommitUncertain:
                    return "AL-RGW-REWARD-COMMIT-UNCERTAIN";
                default:
                    return "AL-RGW-REWARD-RESULT";
            }
        }
    }
}
