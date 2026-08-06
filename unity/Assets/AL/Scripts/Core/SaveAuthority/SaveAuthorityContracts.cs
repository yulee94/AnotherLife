using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AL.SaveAuthority.Tests")]
[assembly: InternalsVisibleTo("AL.Nvs01.Persistence.Tests")]

namespace AL.Core.SaveAuthority
{
    public static class SaveAuthorityTechnicalLimits
    {
        public const string ContractVersion = "profile_write_authority_v1";
        public const string SaveFormatId = "anotherlife.local-save";
        public const int AuthorityContractNumericVersion = 1;
        public const int IdentityAwareSaveSchemaVersion = 2;
        public const int IdentityAwareProfileInitializationVersion = 1;
        public const int LegacySaveSchemaVersion = 1;
        public const int LegacyProfileInitializationVersion = 1;
        public const int MaximumDiagnosticCodes = 16;
        public const int MaximumDiagnosticCodeCharacters = 96;
        public const int MaximumOpaqueIdentityUtf8Bytes = 256;
        public const int MaximumSaveFormatIdUtf8Bytes = 128;
        public const int MaximumEpochAllocationAttempts = 8;
        public const int ReceiptCapacity = 64;
        public const int ProfileIdCharacters = 36;
        public const int AuthorityEpochCharacters = 32;
        public const int Sha256Characters = 64;
    }

    public static class SaveAuthorityDiagnosticCodes
    {
        public const string ProviderMissing =
            "AL-SAVE-AUTH-PROVIDER-MISSING";
        public const string ProviderThrew =
            "AL-SAVE-AUTH-PROVIDER-THREW";
        public const string ProviderNull =
            "AL-SAVE-AUTH-PROVIDER-NULL";
        public const string ProviderContract =
            "AL-SAVE-AUTH-PROVIDER-CONTRACT";
        public const string ProviderStatus =
            "AL-SAVE-AUTH-PROVIDER-STATUS";
        public const string ProviderDiagnostics =
            "AL-SAVE-AUTH-PROVIDER-DIAGNOSTICS";
        public const string ProviderSource =
            "AL-SAVE-AUTH-PROVIDER-SOURCE";
        public const string ProviderInvariants =
            "AL-SAVE-AUTH-PROVIDER-INVARIANTS";
        public const string FingerprintFrameMissing =
            "AL-SAVE-AUTH-FINGERPRINT-FRAME-MISSING";
        public const string FingerprintArtifact =
            "AL-SAVE-AUTH-FINGERPRINT-ARTIFACT";
        public const string FingerprintFields =
            "AL-SAVE-AUTH-FINGERPRINT-FIELDS";
        public const string FingerprintUnavailable =
            "AL-SAVE-AUTH-FINGERPRINT-UNAVAILABLE";
        public const string EpochCandidateExhausted =
            "AL-SAVE-AUTH-EPOCH-CANDIDATE-EXHAUSTED";
        public const string EpochSourceUnavailable =
            "AL-SAVE-AUTH-EPOCH-SOURCE-UNAVAILABLE";
        public const string EpochReentrant =
            "AL-SAVE-AUTH-EPOCH-REENTRANT";
        public const string ReceiptSinkThrew =
            "AL-SAVE-AUTH-RECEIPT-SINK-THREW";
        public const string ReceiptSchedulerUnavailable =
            "AL-SAVE-AUTH-RECEIPT-SCHEDULER-UNAVAILABLE";
        public const string CommitUncertain =
            "AL-SAVE-AUTH-COMMIT-UNCERTAIN";
    }

    public enum ProfileWriteAuthorityStatus
    {
        Unavailable = 0,
        Writable = 1,
        MissingProfile = 2,
        MigrationRequired = 3,
        ForwardSchemaReadOnly = 4,
        DegradedReadOnly = 5,
        RecoveryRequired = 6,
        CommitUncertain = 7,
        Deleted = 8
    }

    public enum ProfileAuthoritySourceGeneration
    {
        None = 0,
        Primary = 1,
        Backup = 2,
        Previous = 3,
        Temp = 4
    }

    public sealed class ProfileWriteAuthoritySnapshot
    {
        private readonly IReadOnlyList<string> _diagnosticCodes;

        internal ProfileWriteAuthoritySnapshot(
            string contractVersion,
            ProfileWriteAuthorityStatus status,
            string profileId,
            string authorityEpoch,
            string verifiedGenerationFingerprint,
            int saveSchemaVersion,
            int profileInitializationVersion,
            bool hasSelectedSourceGeneration,
            ProfileAuthoritySourceGeneration selectedSourceGeneration,
            IEnumerable<string> diagnosticCodes)
        {
            ContractVersion = contractVersion;
            Status = status;
            ProfileId = profileId;
            AuthorityEpoch = authorityEpoch;
            VerifiedGenerationFingerprint = verifiedGenerationFingerprint;
            SaveSchemaVersion = saveSchemaVersion;
            ProfileInitializationVersion = profileInitializationVersion;
            HasSelectedSourceGeneration = hasSelectedSourceGeneration;
            SelectedSourceGeneration = selectedSourceGeneration;
            _diagnosticCodes =
                SaveAuthorityImmutable.CaptureDiagnosticInputs(diagnosticCodes);
        }

        public string ContractVersion { get; }
        public ProfileWriteAuthorityStatus Status { get; }
        public string ProfileId { get; }
        public string AuthorityEpoch { get; }
        public string VerifiedGenerationFingerprint { get; }
        public int SaveSchemaVersion { get; }
        public int ProfileInitializationVersion { get; }
        public bool HasSelectedSourceGeneration { get; }
        public ProfileAuthoritySourceGeneration SelectedSourceGeneration { get; }
        public IReadOnlyList<string> DiagnosticCodes => _diagnosticCodes;
    }

    public interface IProfileWriteAuthorityProvider
    {
        ProfileWriteAuthoritySnapshot GetCurrentAuthority();
    }

    public static class ProfileWriteAuthoritySnapshotFactory
    {
        public static ProfileWriteAuthoritySnapshot Writable(
            string profileId,
            string authorityEpoch,
            string verifiedGenerationFingerprint,
            ProfileAuthoritySourceGeneration selectedSourceGeneration,
            IEnumerable<string> diagnosticCodes) =>
            CreateGuarded(() =>
                new ProfileWriteAuthoritySnapshot(
                    SaveAuthorityTechnicalLimits.ContractVersion,
                    ProfileWriteAuthorityStatus.Writable,
                    profileId,
                    authorityEpoch,
                    verifiedGenerationFingerprint,
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                    SaveAuthorityTechnicalLimits
                        .IdentityAwareProfileInitializationVersion,
                    true,
                    selectedSourceGeneration,
                    diagnosticCodes));

        public static ProfileWriteAuthoritySnapshot MigrationRequired(
            ProfileAuthoritySourceGeneration selectedSourceGeneration,
            IEnumerable<string> diagnosticCodes) =>
            CreateGuarded(() =>
                new ProfileWriteAuthoritySnapshot(
                    SaveAuthorityTechnicalLimits.ContractVersion,
                    ProfileWriteAuthorityStatus.MigrationRequired,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                    SaveAuthorityTechnicalLimits
                        .LegacyProfileInitializationVersion,
                    true,
                    selectedSourceGeneration,
                    diagnosticCodes));

        public static ProfileWriteAuthoritySnapshot NonWritable(
            ProfileWriteAuthorityStatus status,
            int saveSchemaVersion,
            int profileInitializationVersion,
            bool hasSelectedSourceGeneration,
            ProfileAuthoritySourceGeneration selectedSourceGeneration,
            IEnumerable<string> diagnosticCodes) =>
            CreateGuarded(() =>
                new ProfileWriteAuthoritySnapshot(
                    SaveAuthorityTechnicalLimits.ContractVersion,
                    status,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    saveSchemaVersion,
                    profileInitializationVersion,
                    hasSelectedSourceGeneration,
                    selectedSourceGeneration,
                    diagnosticCodes));

        public static ProfileWriteAuthoritySnapshot Unavailable(
            string diagnosticCode)
        {
            string code = SaveAuthorityValidation.IsDiagnosticCode(
                diagnosticCode)
                ? diagnosticCode
                : SaveAuthorityDiagnosticCodes.ProviderInvariants;
            return new ProfileWriteAuthoritySnapshot(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.Unavailable,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                false,
                ProfileAuthoritySourceGeneration.None,
                new[] { code });
        }

        private static ProfileWriteAuthoritySnapshot CreateGuarded(
            Func<ProfileWriteAuthoritySnapshot> create)
        {
            try
            {
                return ProfileWriteAuthorityProviderGuard
                    .ValidateOrUnavailable(create());
            }
            catch
            {
                return Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderDiagnostics);
            }
        }
    }

    public sealed class ProfileAuthorityExpectation
    {
        internal ProfileAuthorityExpectation(
            string profileId,
            string authorityEpoch,
            string expectedGenerationFingerprint)
        {
            ProfileId = profileId;
            AuthorityEpoch = authorityEpoch;
            ExpectedGenerationFingerprint = expectedGenerationFingerprint;
        }

        public string ProfileId { get; }
        public string AuthorityEpoch { get; }
        public string ExpectedGenerationFingerprint { get; }

        public static ProfileAuthorityExpectation From(
            ProfileWriteAuthoritySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new ProfileAuthorityExpectation(
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }

            return new ProfileAuthorityExpectation(
                snapshot.ProfileId,
                snapshot.AuthorityEpoch,
                snapshot.VerifiedGenerationFingerprint);
        }
    }

    public enum ProfileMutationReceiptStatus
    {
        Unavailable = 0,
        Committed = 1,
        VerifiedRollback = 2,
        CommitUncertain = 3
    }

    public sealed class ProfileMutationReceipt
    {
        private readonly IReadOnlyList<string> _diagnosticCodes;

        internal ProfileMutationReceipt(
            ProfileMutationReceiptStatus status,
            ulong publicationSequence,
            string profileId,
            string expectedGenerationFingerprint,
            string committedGenerationFingerprint,
            string committedAuthorityEpoch,
            string operationId,
            string resultId,
            string committedPayloadFingerprint,
            bool mayHaveMutated,
            IEnumerable<string> diagnosticCodes)
        {
            ContractVersion = SaveAuthorityTechnicalLimits.ContractVersion;
            Status = status;
            PublicationSequence = publicationSequence;
            ProfileId = profileId;
            ExpectedGenerationFingerprint = expectedGenerationFingerprint;
            CommittedGenerationFingerprint = committedGenerationFingerprint;
            CommittedAuthorityEpoch = committedAuthorityEpoch;
            OperationId = operationId;
            ResultId = resultId;
            CommittedPayloadFingerprint = committedPayloadFingerprint;
            MayHaveMutated = mayHaveMutated;
            _diagnosticCodes =
                SaveAuthorityImmutable.CanonicalDiagnosticsOrFallback(
                    diagnosticCodes,
                    SaveAuthorityDiagnosticCodes.ProviderInvariants);
        }

        public string ContractVersion { get; }
        public ProfileMutationReceiptStatus Status { get; }
        public ulong PublicationSequence { get; }
        public string ProfileId { get; }
        public string ExpectedGenerationFingerprint { get; }
        public string CommittedGenerationFingerprint { get; }
        public string CommittedAuthorityEpoch { get; }
        public string OperationId { get; }
        public string ResultId { get; }
        public string CommittedPayloadFingerprint { get; }
        public bool MayHaveMutated { get; }
        public IReadOnlyList<string> DiagnosticCodes => _diagnosticCodes;
    }

    internal static class SaveAuthorityImmutable
    {
        internal static IReadOnlyList<string> CaptureDiagnosticInputs(
            IEnumerable<string> values)
        {
            if (values == null)
                return null;

            var captured = new List<string>(
                SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes + 1);
            using (IEnumerator<string> enumerator = values.GetEnumerator())
            {
                while (captured.Count <=
                       SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes &&
                       enumerator.MoveNext())
                {
                    captured.Add(enumerator.Current);
                }
            }

            return new ReadOnlyCollection<string>(captured.ToArray());
        }

        internal static IReadOnlyList<string> CanonicalDiagnosticsOrFallback(
            IEnumerable<string> diagnostics,
            string fallback)
        {
            if (!SaveAuthorityValidation.TryCanonicalizeDiagnostics(
                    diagnostics,
                    out string[] canonical))
            {
                canonical = new[] { fallback };
            }

            return new ReadOnlyCollection<string>(canonical);
        }
    }
}
