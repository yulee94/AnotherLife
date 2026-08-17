using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Data.Runtime;

namespace AL.RealmGems
{
    public enum WishgateOutcomeDeliveryStatus
    {
        Delivered,
        AlreadyDelivered,
        NoCommittedOutcome,
        CommitUncertain,
        InvalidCommittedOutcome,
        PublisherUnavailable,
        DeliveryFailed,
        DeliveryReceiptNotCommitted
    }

    public sealed class WishgateOutcomeDeliveryResult
    {
        internal WishgateOutcomeDeliveryResult(
            WishgateOutcomeDeliveryStatus status,
            string operationId = "",
            string outcomeIdentity = "")
        {
            Status = status;
            OperationId = operationId ?? string.Empty;
            OutcomeIdentity = outcomeIdentity ?? string.Empty;
        }

        public WishgateOutcomeDeliveryStatus Status { get; }
        public string OperationId { get; }
        public string OutcomeIdentity { get; }
        public bool IsDelivered =>
            Status == WishgateOutcomeDeliveryStatus.Delivered ||
            Status == WishgateOutcomeDeliveryStatus.AlreadyDelivered;
    }

    public interface IWishgateOutcomePublisher
    {
        void Publish(WishgateCommittedOutcome payload);
    }

    public sealed class WishgateOutcomeNotificationHub : IWishgateOutcomePublisher
    {
        private readonly object _gate = new object();
        private Action<WishgateCommittedOutcome> _subscribers;

        public static WishgateOutcomeNotificationHub Shared { get; } =
            new WishgateOutcomeNotificationHub();

        public IDisposable Subscribe(Action<WishgateCommittedOutcome> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            lock (_gate) _subscribers += subscriber;
            return new Subscription(this, subscriber);
        }

        public void Publish(WishgateCommittedOutcome payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            Delegate[] subscribers;
            lock (_gate) subscribers = _subscribers?.GetInvocationList() ?? Array.Empty<Delegate>();
            if (subscribers.Length == 0)
                throw new InvalidOperationException("No Wishgate outcome consumer is registered.");

            foreach (Action<WishgateCommittedOutcome> subscriber in subscribers.Cast<Action<WishgateCommittedOutcome>>())
                subscriber(payload);
        }

        private void Unsubscribe(Action<WishgateCommittedOutcome> subscriber)
        {
            lock (_gate) _subscribers -= subscriber;
        }

        private sealed class Subscription : IDisposable
        {
            private WishgateOutcomeNotificationHub _owner;
            private Action<WishgateCommittedOutcome> _subscriber;

            public Subscription(
                WishgateOutcomeNotificationHub owner,
                Action<WishgateCommittedOutcome> subscriber)
            {
                _owner = owner;
                _subscriber = subscriber;
            }

            public void Dispose()
            {
                WishgateOutcomeNotificationHub owner = _owner;
                Action<WishgateCommittedOutcome> subscriber = _subscriber;
                _owner = null;
                _subscriber = null;
                owner?.Unsubscribe(subscriber);
            }
        }
    }

    public sealed class WishgateCommittedOutcome
    {
        internal WishgateCommittedOutcome(WishgateRewardReceiptData receipt)
        {
            SchemaVersion = 1;
            OperationType = "wishgate_reward";
            Outcome = "committed";
            OperationId = receipt.OperationId;
            CatalogItemId = receipt.RewardId;
            ActorId = receipt.ActorId;
            RecipientId = receipt.ActorId;
            ZoneId = receipt.ZoneId;
            WarzoneCreditsAwarded = receipt.WarzoneCreditsAwarded;
            CommittedTimestamp = receipt.CommittedTimestamp;
            PayloadFingerprint = receipt.PayloadFingerprint;
            OutcomeIdentity = receipt.OutcomeIdentity;
        }

        public int SchemaVersion { get; }
        public string OperationType { get; }
        public string Outcome { get; }
        public string OperationId { get; }
        public string CatalogItemId { get; }
        public string ActorId { get; }
        public string RecipientId { get; }
        public string ZoneId { get; }
        public int WarzoneCreditsAwarded { get; }
        public long CommittedTimestamp { get; }
        public string PayloadFingerprint { get; }
        public string OutcomeIdentity { get; }

        internal static string ComputeOutcomeIdentity(WishgateRewardReceiptData receipt)
        {
            string canonical = string.Join("\n", new[]
            {
                "wishgate_outcome_v1",
                receipt.OperationId,
                receipt.PayloadFingerprint,
                receipt.ActorId,
                receipt.ZoneId,
                receipt.RewardId,
                receipt.WarzoneCreditsAwarded.ToString(System.Globalization.CultureInfo.InvariantCulture),
                receipt.CommittedTimestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "committed"
            });
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder("sha256:");
                foreach (byte value in digest) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }
    }

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
