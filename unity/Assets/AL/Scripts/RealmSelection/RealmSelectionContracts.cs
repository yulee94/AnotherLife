using System;
using AL.Core;

namespace AL.RealmSelection
{
    public enum RealmIdentityStatus
    {
        ProfileUnavailable = 0,
        CatalogUnavailable = 1,
        Uncommitted = 2,
        CommittedValid = 3,
        InvalidPersistedIdentity = 4
    }

    public enum RealmSelectionStatus
    {
        Committed = 0,
        AlreadyCommittedSameRealm = 1,
        RejectedDifferentRealm = 2,
        InvalidRealm = 3,
        RealmDefinitionUnavailable = 4,
        ProfileUnavailable = 5,
        SaveFailedPreviousPreserved = 6,
        InvalidTransaction = 7,
        CommitUncertain = 8
    }

    public readonly struct RealmSelectionRequest
    {
        public RealmSelectionRequest(string transactionId, RealmId requestedRealmId)
            : this(
                transactionId,
                requestedRealmId,
                transactionId,
                string.Empty,
                string.Empty)
        {
        }

        public RealmSelectionRequest(
            string transactionId,
            RealmId requestedRealmId,
            string correlationId,
            string expectedProfileId,
            string expectedGenerationFingerprint)
        {
            TransactionId = transactionId ?? string.Empty;
            RequestedRealmId = requestedRealmId;
            CorrelationId = string.IsNullOrWhiteSpace(correlationId)
                ? TransactionId
                : correlationId;
            ExpectedProfileId = expectedProfileId ?? string.Empty;
            ExpectedGenerationFingerprint = expectedGenerationFingerprint ?? string.Empty;
        }

        public string TransactionId { get; }
        public RealmId RequestedRealmId { get; }
        public string CorrelationId { get; }
        public string ExpectedProfileId { get; }
        public string ExpectedGenerationFingerprint { get; }
    }

    public readonly struct RealmIdentitySnapshot
    {
        public RealmIdentitySnapshot(RealmIdentityStatus status, RealmId realmId, string catalogVersion, string technicalCode)
        {
            Status = status;
            RealmId = realmId;
            CatalogVersion = catalogVersion ?? string.Empty;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmIdentityStatus Status { get; }
        public RealmId RealmId { get; }
        public string CatalogVersion { get; }
        public string TechnicalCode { get; }
        public bool IsCommittedValid =>
            Status == RealmIdentityStatus.CommittedValid &&
            RealmId != RealmId.None && Enum.IsDefined(typeof(RealmId), RealmId);
    }

    public readonly struct RealmSelectionResult
    {
        public RealmSelectionResult(RealmSelectionStatus status, RealmId requestedRealmId, RealmId committedRealmId, bool mutationOccurred, bool persisted, string technicalCode)
        {
            Status = status;
            RequestedRealmId = requestedRealmId;
            CommittedRealmId = committedRealmId;
            MutationOccurred = mutationOccurred;
            Persisted = persisted;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmSelectionStatus Status { get; }
        public RealmId RequestedRealmId { get; }
        public RealmId CommittedRealmId { get; }
        public bool MutationOccurred { get; }
        public bool Persisted { get; }
        public string TechnicalCode { get; }
        public bool AllowsNavigation => Status == RealmSelectionStatus.Committed || Status == RealmSelectionStatus.AlreadyCommittedSameRealm;
    }
}
