using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Core.SaveAuthority
{
    public static class ProfileIdentityMigrationTechnicalLimits
    {
        public const string ContractVersion =
            "profile_identity_migration_v1";
        public const int MaximumIdentityAttempts = 8;
        public const int MaximumRetainedIdentityCount = 64;
        public const long MaximumArtifactByteCount = 1L * 1024L * 1024L;
    }

    public static class ProfileIdentityMigrationDiagnosticCodes
    {
        public const string RequestMissing =
            "AL-SAVE-MIGRATION-REQUEST-MISSING";
        public const string EvidenceInvalid =
            "AL-SAVE-MIGRATION-EVIDENCE-INVALID";
        public const string EvidenceUnsupported =
            "AL-SAVE-MIGRATION-EVIDENCE-UNSUPPORTED";
        public const string IdentitySetInvalid =
            "AL-SAVE-MIGRATION-IDENTITY-SET-INVALID";
        public const string IdentitySourceMissing =
            "AL-SAVE-MIGRATION-IDENTITY-SOURCE-MISSING";
        public const string IdentitySourceThrew =
            "AL-SAVE-MIGRATION-IDENTITY-SOURCE-THREW";
        public const string IdentityCandidateInvalid =
            "AL-SAVE-MIGRATION-IDENTITY-CANDIDATE-INVALID";
        public const string IdentityExhausted =
            "AL-SAVE-MIGRATION-IDENTITY-EXHAUSTED";
        public const string ProjectionSourceMissing =
            "AL-SAVE-MIGRATION-PROJECTION-SOURCE-MISSING";
        public const string ProjectionSourceThrew =
            "AL-SAVE-MIGRATION-PROJECTION-SOURCE-THREW";
        public const string ProjectionInvalid =
            "AL-SAVE-MIGRATION-PROJECTION-INVALID";
    }

    public enum ProfileIdentityMigrationEvidenceState
    {
        AllMissing = 1,
        CoherentSchemaOne = 2,
        ForwardSchema = 3,
        Degraded = 4,
        Malformed = 5,
        Inaccessible = 6,
        RecoveryRequired = 7,
        CommitUncertain = 8,
        RepairRequiresDataChange = 9,
        BothInvalid = 10,
        IdentityConflict = 11
    }

    public enum ProfileIdentityMigrationMode
    {
        None = 0,
        CreateNewProfile = 1,
        MigrateSchemaOne = 2
    }

    public enum ProfileIdentityMigrationPlanStatus
    {
        Rejected = 0,
        CreationReady = 1,
        MigrationReady = 2
    }

    /// <summary>
    /// Supplies an ordinal 128-bit opaque profile identity candidate. A later
    /// production adapter must use a cryptographically strong random source;
    /// the planner validates format, rejects reuse, and invokes at most eight
    /// ordinal attempts.
    /// </summary>
    public interface IProfileIdentityCandidateSource
    {
        string GetCandidate(int attemptNumber);
    }

    /// <summary>
    /// Builds an in-memory projection after a profile identity is selected.
    /// Implementations must be pure and must not perform filesystem, service,
    /// registration, save mutation, or publication work.
    /// </summary>
    public interface IProfileIdentityMigrationProjectionSource
    {
        ProfileIdentityMigrationProjection CreateProjection(
            ProfileIdentityMigrationMode mode,
            string profileId,
            ProfileIdentityMigrationEvidence evidence);
    }

    public sealed class ProfileIdentityGenerationExpectation
    {
        public ProfileIdentityGenerationExpectation(
            bool isPresent,
            ProfileAuthoritySourceGeneration sourceGeneration,
            long byteCount,
            string sha256,
            string profileId,
            int saveSchemaVersion,
            int profileInitializationVersion)
        {
            IsPresent = isPresent;
            SourceGeneration = sourceGeneration;
            ByteCount = byteCount;
            Sha256 = sha256;
            ProfileId = profileId;
            SaveSchemaVersion = saveSchemaVersion;
            ProfileInitializationVersion = profileInitializationVersion;
        }

        public bool IsPresent { get; }
        public ProfileAuthoritySourceGeneration SourceGeneration { get; }
        public long ByteCount { get; }
        public string Sha256 { get; }
        public string ProfileId { get; }
        public int SaveSchemaVersion { get; }
        public int ProfileInitializationVersion { get; }

        public static ProfileIdentityGenerationExpectation Missing() =>
            new ProfileIdentityGenerationExpectation(
                false,
                ProfileAuthoritySourceGeneration.None,
                0,
                string.Empty,
                string.Empty,
                0,
                0);

        public static ProfileIdentityGenerationExpectation Exact(
            ProfileAuthoritySourceGeneration sourceGeneration,
            long byteCount,
            string sha256,
            string profileId,
            int saveSchemaVersion,
            int profileInitializationVersion) =>
            new ProfileIdentityGenerationExpectation(
                true,
                sourceGeneration,
                byteCount,
                sha256,
                profileId,
                saveSchemaVersion,
                profileInitializationVersion);

        internal static ProfileIdentityGenerationExpectation Clone(
            ProfileIdentityGenerationExpectation source) =>
            source == null
                ? Missing()
                : new ProfileIdentityGenerationExpectation(
                    source.IsPresent,
                    source.SourceGeneration,
                    source.ByteCount,
                    source.Sha256 ?? string.Empty,
                    source.ProfileId ?? string.Empty,
                    source.SaveSchemaVersion,
                    source.ProfileInitializationVersion);
    }

    public sealed class ProfileIdentityMigrationWitnessExpectation
    {
        public ProfileIdentityMigrationWitnessExpectation(
            bool isPresent,
            string contractVersion,
            string operationId,
            ProfileAuthoritySourceGeneration selectedLegacySourceGeneration,
            long predecessorByteCount,
            string predecessorSha256,
            string profileId,
            int targetSaveSchemaVersion,
            int targetProfileInitializationVersion,
            long candidateByteCount,
            string candidateSha256,
            long byteCount,
            string sha256)
        {
            IsPresent = isPresent;
            ContractVersion = contractVersion;
            OperationId = operationId;
            SelectedLegacySourceGeneration =
                selectedLegacySourceGeneration;
            PredecessorByteCount = predecessorByteCount;
            PredecessorSha256 = predecessorSha256;
            ProfileId = profileId;
            TargetSaveSchemaVersion = targetSaveSchemaVersion;
            TargetProfileInitializationVersion =
                targetProfileInitializationVersion;
            CandidateByteCount = candidateByteCount;
            CandidateSha256 = candidateSha256;
            ByteCount = byteCount;
            Sha256 = sha256;
        }

        public bool IsPresent { get; }
        public string ContractVersion { get; }
        public string OperationId { get; }
        public ProfileAuthoritySourceGeneration
            SelectedLegacySourceGeneration { get; }
        public long PredecessorByteCount { get; }
        public string PredecessorSha256 { get; }
        public string ProfileId { get; }
        public int TargetSaveSchemaVersion { get; }
        public int TargetProfileInitializationVersion { get; }
        public long CandidateByteCount { get; }
        public string CandidateSha256 { get; }
        public long ByteCount { get; }
        public string Sha256 { get; }

        public static ProfileIdentityMigrationWitnessExpectation Missing() =>
            new ProfileIdentityMigrationWitnessExpectation(
                false,
                string.Empty,
                string.Empty,
                ProfileAuthoritySourceGeneration.None,
                0,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                string.Empty,
                0,
                string.Empty);

        internal static ProfileIdentityMigrationWitnessExpectation Clone(
            ProfileIdentityMigrationWitnessExpectation source) =>
            source == null
                ? Missing()
                : new ProfileIdentityMigrationWitnessExpectation(
                    source.IsPresent,
                    source.ContractVersion ?? string.Empty,
                    source.OperationId ?? string.Empty,
                    source.SelectedLegacySourceGeneration,
                    source.PredecessorByteCount,
                    source.PredecessorSha256 ?? string.Empty,
                    source.ProfileId ?? string.Empty,
                    source.TargetSaveSchemaVersion,
                    source.TargetProfileInitializationVersion,
                    source.CandidateByteCount,
                    source.CandidateSha256 ?? string.Empty,
                    source.ByteCount,
                    source.Sha256 ?? string.Empty);
    }

    public sealed class ProfileIdentityMigrationEvidence
    {
        public ProfileIdentityMigrationEvidence(
            ProfileIdentityMigrationEvidenceState state,
            bool hasSelectedSourceGeneration,
            ProfileAuthoritySourceGeneration selectedSourceGeneration,
            ProfileIdentityGenerationExpectation selectedPredecessor,
            IEnumerable<string> recognizedProfileIds)
        {
            State = state;
            HasSelectedSourceGeneration = hasSelectedSourceGeneration;
            SelectedSourceGeneration = selectedSourceGeneration;
            SelectedPredecessor = selectedPredecessor;
            RecognizedProfileIds = recognizedProfileIds;
        }

        public ProfileIdentityMigrationEvidenceState State { get; }
        public bool HasSelectedSourceGeneration { get; }
        public ProfileAuthoritySourceGeneration SelectedSourceGeneration
        {
            get;
        }
        public ProfileIdentityGenerationExpectation SelectedPredecessor
        {
            get;
        }
        public IEnumerable<string> RecognizedProfileIds { get; }
    }

    public sealed class ProfileIdentityMigrationProjection
    {
        public ProfileIdentityMigrationProjection(
            ProfileIdentityGenerationExpectation predecessor,
            ProfileIdentityGenerationExpectation candidate,
            ProfileIdentityMigrationWitnessExpectation witness)
        {
            Predecessor = predecessor;
            Candidate = candidate;
            Witness = witness;
        }

        public ProfileIdentityGenerationExpectation Predecessor { get; }
        public ProfileIdentityGenerationExpectation Candidate { get; }
        public ProfileIdentityMigrationWitnessExpectation Witness { get; }
    }

    public sealed class ProfileIdentityMigrationRequest
    {
        public ProfileIdentityMigrationRequest(
            ProfileIdentityMigrationEvidence evidence,
            IEnumerable<string> retainedSessionProfileIds,
            IProfileIdentityCandidateSource identityCandidateSource,
            IProfileIdentityMigrationProjectionSource projectionSource)
        {
            Evidence = evidence;
            RetainedSessionProfileIds = retainedSessionProfileIds;
            IdentityCandidateSource = identityCandidateSource;
            ProjectionSource = projectionSource;
        }

        public ProfileIdentityMigrationEvidence Evidence { get; }
        public IEnumerable<string> RetainedSessionProfileIds { get; }
        public IProfileIdentityCandidateSource IdentityCandidateSource
        {
            get;
        }
        public IProfileIdentityMigrationProjectionSource ProjectionSource
        {
            get;
        }
    }

    public sealed class ProfileIdentityMigrationPlan
    {
        private readonly IReadOnlyList<string> _diagnosticCodes;

        internal ProfileIdentityMigrationPlan(
            ProfileIdentityMigrationPlanStatus status,
            ProfileIdentityMigrationMode mode,
            string profileId,
            int identityAttemptCount,
            ProfileIdentityGenerationExpectation predecessor,
            ProfileIdentityGenerationExpectation candidate,
            ProfileIdentityMigrationWitnessExpectation witness,
            IEnumerable<string> diagnosticCodes)
        {
            ContractVersion =
                ProfileIdentityMigrationTechnicalLimits.ContractVersion;
            Status = status;
            Mode = mode;
            ProfileId = profileId ?? string.Empty;
            IdentityAttemptCount = identityAttemptCount;
            Predecessor = ProfileIdentityGenerationExpectation.Clone(
                predecessor);
            Candidate = ProfileIdentityGenerationExpectation.Clone(candidate);
            Witness = ProfileIdentityMigrationWitnessExpectation.Clone(witness);
            _diagnosticCodes = new ReadOnlyCollection<string>(
                new List<string>(
                    diagnosticCodes ?? Array.Empty<string>()).ToArray());
        }

        public string ContractVersion { get; }
        public ProfileIdentityMigrationPlanStatus Status { get; }
        public ProfileIdentityMigrationMode Mode { get; }
        public string ProfileId { get; }
        public int IdentityAttemptCount { get; }
        public ProfileIdentityGenerationExpectation Predecessor { get; }
        public ProfileIdentityGenerationExpectation Candidate { get; }
        public ProfileIdentityMigrationWitnessExpectation Witness { get; }
        public IReadOnlyList<string> DiagnosticCodes => _diagnosticCodes;
    }
}
