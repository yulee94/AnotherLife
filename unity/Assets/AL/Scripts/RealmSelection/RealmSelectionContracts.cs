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
        InvalidTransaction = 7
    }

    public enum RealmSelectionCommitState
    {
        Uncommitted = 0,
        Committed = 1
    }

    public enum RealmSelectionCommitPublicationState
    {
        None = 0,
        Pending = 1,
        Delivered = 2,
        NotApplicable = 3
    }

    public enum RealmSelectionCommitProvenance
    {
        None = 0,
        InitialSelection = 1,
        LegacyMigration = 2
    }

    public enum RealmSelectionCommitStatus
    {
        Committed = 0,
        AlreadyCommittedSameRealm = 1,
        DuplicateTransaction = 2,
        RejectedDifferentRealm = 3,
        TransactionMismatch = 4,
        InvalidTransaction = 5,
        InvalidRealm = 6,
        RealmDefinitionUnavailable = 7,
        ProfileUnavailable = 8,
        ProfileNotWritable = 9,
        StaleAuthority = 10,
        ForwardSchemaReadOnly = 11,
        SaveFailedPreviousPreserved = 12,
        CommitUncertain = 13,
        RecoveryRequired = 14
    }

    public readonly struct RealmSelectionRequest
    {
        public RealmSelectionRequest(string transactionId, RealmId requestedRealmId)
        {
            TransactionId = transactionId ?? string.Empty;
            RequestedRealmId = requestedRealmId;
        }

        public string TransactionId { get; }
        public RealmId RequestedRealmId { get; }
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

    public readonly struct RealmSelectionCommand
    {
        public RealmSelectionCommand(
            string transactionId,
            RealmId requestedRealmId,
            string requestedCanonicalRealmId,
            string catalogAuthorityId,
            string catalogVersion,
            AL.Core.SaveAuthority.ProfileAuthorityExpectation authority)
        {
            TransactionId = transactionId ?? string.Empty;
            RequestedRealmId = requestedRealmId;
            RequestedCanonicalRealmId = requestedCanonicalRealmId ?? string.Empty;
            CatalogAuthorityId = catalogAuthorityId ?? string.Empty;
            CatalogVersion = catalogVersion ?? string.Empty;
            Authority = authority;
        }

        public string TransactionId { get; }
        public RealmId RequestedRealmId { get; }
        public string RequestedCanonicalRealmId { get; }
        public string CatalogAuthorityId { get; }
        public string CatalogVersion { get; }
        public AL.Core.SaveAuthority.ProfileAuthorityExpectation Authority { get; }
    }

    public sealed class RealmSelectionCommitResult
    {
        public RealmSelectionCommitResult(
            RealmSelectionCommitStatus status,
            string profileId,
            RealmId requestedRealmId,
            RealmId committedRealmId,
            string canonicalCommittedRealmId,
            string submittedTransactionId,
            string committedTransactionId,
            string intentSha256,
            string catalogAuthorityId,
            string catalogVersion,
            long commitRevision,
            string committedEventId,
            bool mutationOccurred,
            bool persistedAndVerified,
            string resultingGenerationFingerprint,
            string technicalCode)
        {
            Status = status;
            ProfileId = profileId ?? string.Empty;
            RequestedRealmId = requestedRealmId;
            CommittedRealmId = committedRealmId;
            CanonicalCommittedRealmId = canonicalCommittedRealmId ?? string.Empty;
            SubmittedTransactionId = submittedTransactionId ?? string.Empty;
            CommittedTransactionId = committedTransactionId ?? string.Empty;
            IntentSha256 = intentSha256 ?? string.Empty;
            CatalogAuthorityId = catalogAuthorityId ?? string.Empty;
            CatalogVersion = catalogVersion ?? string.Empty;
            CommitRevision = commitRevision;
            CommittedEventId = committedEventId ?? string.Empty;
            MutationOccurred = mutationOccurred;
            PersistedAndVerified = persistedAndVerified;
            ResultingGenerationFingerprint =
                resultingGenerationFingerprint ?? string.Empty;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmSelectionCommitStatus Status { get; }
        public string ProfileId { get; }
        public RealmId RequestedRealmId { get; }
        public RealmId CommittedRealmId { get; }
        public string CanonicalCommittedRealmId { get; }
        public string SubmittedTransactionId { get; }
        public string CommittedTransactionId { get; }
        public string IntentSha256 { get; }
        public string CatalogAuthorityId { get; }
        public string CatalogVersion { get; }
        public long CommitRevision { get; }
        public string CommittedEventId { get; }
        public bool MutationOccurred { get; }
        public bool PersistedAndVerified { get; }
        public string ResultingGenerationFingerprint { get; }
        public string TechnicalCode { get; }
    }

    public sealed class RealmSelectionCommittedEvent
    {
        public RealmSelectionCommittedEvent(
            int contractVersion,
            string eventId,
            string profileId,
            string transactionId,
            string intentSha256,
            RealmId realmId,
            string canonicalRealmId,
            string catalogAuthorityId,
            string catalogVersion,
            long commitRevision,
            long committedUnixTimeMilliseconds,
            string resultingGenerationFingerprint,
            RealmSelectionCommitProvenance provenance)
        {
            ContractVersion = contractVersion;
            EventId = eventId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            IntentSha256 = intentSha256 ?? string.Empty;
            RealmId = realmId;
            CanonicalRealmId = canonicalRealmId ?? string.Empty;
            CatalogAuthorityId = catalogAuthorityId ?? string.Empty;
            CatalogVersion = catalogVersion ?? string.Empty;
            CommitRevision = commitRevision;
            CommittedUnixTimeMilliseconds = committedUnixTimeMilliseconds;
            ResultingGenerationFingerprint =
                resultingGenerationFingerprint ?? string.Empty;
            Provenance = provenance;
        }

        public int ContractVersion { get; }
        public string EventId { get; }
        public string ProfileId { get; }
        public string TransactionId { get; }
        public string IntentSha256 { get; }
        public RealmId RealmId { get; }
        public string CanonicalRealmId { get; }
        public string CatalogAuthorityId { get; }
        public string CatalogVersion { get; }
        public long CommitRevision { get; }
        public long CommittedUnixTimeMilliseconds { get; }
        public string ResultingGenerationFingerprint { get; }
        public RealmSelectionCommitProvenance Provenance { get; }
    }

    public interface IProfileBoundRealmSelectionStore
    {
        RealmSelectionCommitResult TryCommitRealmSelection(
            RealmSelectionCommand command);

        RealmIdentitySnapshot GetCommittedRealm();

        event Action<RealmSelectionCommittedEvent> RealmSelectionCommitted;
    }
}
