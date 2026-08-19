using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public enum SaveProfileIdentityMigrationStatus
    {
        Unavailable = 0,
        Migrated = 1,
        Rejected = 2
    }

    /// <summary>
    /// Serialized witness for the schema-v1 to schema-v2 profile-identity
    /// migration. It contains no raw save data or file paths; only the bounded
    /// contract/operation identity, selected legacy source, exact predecessor and
    /// candidate hashes/byte counts, the minted ProfileId, and the target schema
    /// and initialization versions.
    /// </summary>
    [Serializable]
    public sealed class ProfileIdentityMigrationWitnessRecord
    {
        public string ContractVersion = string.Empty;
        public string OperationId = string.Empty;
        public int SelectedLegacySourceGeneration;
        public long PredecessorByteCount;
        public string PredecessorSha256 = string.Empty;
        public string ProfileId = string.Empty;
        public int TargetSaveSchemaVersion;
        public int TargetProfileInitializationVersion;
        public long CandidateByteCount;
        public string CandidateSha256 = string.Empty;
    }

    public sealed class SaveProfileIdentityMigrationResult
    {
        internal SaveProfileIdentityMigrationResult(
            SaveProfileIdentityMigrationStatus status,
            string profileId,
            SaveGameData migratedSave,
            byte[] predecessorBytes,
            byte[] candidateBytes,
            ProfileIdentityMigrationWitnessRecord witness,
            byte[] witnessBytes,
            bool ledgerVerified,
            int identityAttemptCount,
            IEnumerable<string> diagnosticCodes)
        {
            Status = status;
            ProfileId = profileId ?? string.Empty;
            MigratedSave = migratedSave;
            PredecessorBytes = Copy(predecessorBytes);
            CandidateBytes = Copy(candidateBytes);
            Witness = witness;
            WitnessBytes = Copy(witnessBytes);
            LedgerVerified = ledgerVerified;
            IdentityAttemptCount = identityAttemptCount;
            DiagnosticCodes = new ReadOnlyCollection<string>(
                (diagnosticCodes ?? Array.Empty<string>())
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray());
        }

        public SaveProfileIdentityMigrationStatus Status { get; }
        public string ProfileId { get; }
        public SaveGameData MigratedSave { get; }
        public byte[] PredecessorBytes { get; }
        public byte[] CandidateBytes { get; }
        public ProfileIdentityMigrationWitnessRecord Witness { get; }
        public byte[] WitnessBytes { get; }
        public bool LedgerVerified { get; }
        public int IdentityAttemptCount { get; }
        public IReadOnlyList<string> DiagnosticCodes { get; }

        public bool IsMigrated =>
            Status == SaveProfileIdentityMigrationStatus.Migrated;

        private static byte[] Copy(byte[] bytes) =>
            bytes == null
                ? null
                : (byte[])bytes.Clone();
    }

    /// <summary>
    /// Dormant schema-v1 to schema-v2 profile-identity migration executor. It is
    /// deliberately not wired into <c>LocalSaveGameService</c>: production saves
    /// remain schema-v1 and continue to report <c>MigrationRequired</c> through
    /// the write-authority provider until the separately reviewed current-mutator
    /// cutover train activates publication.
    ///
    /// This executor mints one canonical ProfileId (crypto-random, with bounded
    /// collision retry through <see cref="ProfileIdentityMigrationPlanner"/>),
    /// bumps a schema-v1 clone to schema-v2, and produces the exact witnessed
    /// transitional ledger (Primary = migrated candidate, Backup = schema-v1
    /// predecessor, Marker = bounded witness). The returned predecessor bytes are
    /// the proven rollback artifact: restoring them removes the identity-bearing
    /// candidate without leaving a partially installed identity as authority.
    /// </summary>
    public static class SaveProfileIdentityMigration
    {
        private const string DefaultOperationIdPrefix =
            "al.save.schema2.identity-migration.";

        public static SaveProfileIdentityMigrationResult MigrateSchemaOne(
            SaveGameData legacySave,
            byte[] predecessorBytes,
            ProfileAuthoritySourceGeneration selectedSourceGeneration,
            IProfileIdentityCandidateSource identitySource,
            IEnumerable<string> retainedProfileIds = null,
            string operationId = null)
        {
            if (legacySave == null ||
                predecessorBytes == null ||
                predecessorBytes.Length == 0 ||
                identitySource == null)
            {
                return Rejected(
                    "AL-SAVE-MIGRATION-REQUEST-INVALID",
                    0);
            }

            if (!IsSupportedLegacySource(selectedSourceGeneration))
            {
                return Rejected(
                    "AL-SAVE-MIGRATION-SOURCE-INVALID",
                    0);
            }

            if (!IsSchemaOneProfile(legacySave))
            {
                return Rejected(
                    "AL-SAVE-MIGRATION-PROFILE-NOT-SCHEMA-ONE",
                    0);
            }

            string predecessorSha256;
            try
            {
                predecessorSha256 = ComputeSha256(predecessorBytes);
            }
            catch
            {
                return Unavailable("AL-SAVE-MIGRATION-PREDECESSOR-HASH");
            }

            ProfileIdentityGenerationExpectation predecessor =
                ProfileIdentityGenerationExpectation.Exact(
                    selectedSourceGeneration,
                    predecessorBytes.Length,
                    predecessorSha256,
                    string.Empty,
                    SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                    SaveAuthorityTechnicalLimits
                        .LegacyProfileInitializationVersion);

            var evidence = new ProfileIdentityMigrationEvidence(
                ProfileIdentityMigrationEvidenceState.CoherentSchemaOne,
                true,
                selectedSourceGeneration,
                predecessor,
                Array.Empty<string>());

            var projection = new RuntimeProjectionSource(
                legacySave,
                predecessorBytes,
                operationId ??
                DefaultOperationIdPrefix + Guid.NewGuid().ToString("N"));

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    new ProfileIdentityMigrationRequest(
                        evidence,
                        retainedProfileIds ?? Array.Empty<string>(),
                        identitySource,
                        projection));

            if (plan.Status !=
                ProfileIdentityMigrationPlanStatus.MigrationReady)
            {
                return Rejected(
                    plan.DiagnosticCodes.FirstOrDefault() ??
                    "AL-SAVE-MIGRATION-PLAN-REJECTED",
                    plan.IdentityAttemptCount);
            }

            bool ledgerVerified;
            try
            {
                ledgerVerified = projection.VerifyLedgerTwice(plan.ProfileId);
            }
            catch
            {
                return Unavailable("AL-SAVE-MIGRATION-LEDGER-VERIFY");
            }

            return new SaveProfileIdentityMigrationResult(
                SaveProfileIdentityMigrationStatus.Migrated,
                plan.ProfileId,
                projection.MigratedSave,
                predecessorBytes,
                projection.CandidateBytes,
                projection.Witness,
                projection.WitnessBytes,
                ledgerVerified,
                plan.IdentityAttemptCount,
                Array.Empty<string>());
        }

        private static bool IsSupportedLegacySource(
            ProfileAuthoritySourceGeneration source) =>
            source == ProfileAuthoritySourceGeneration.Primary ||
            source == ProfileAuthoritySourceGeneration.Backup ||
            source == ProfileAuthoritySourceGeneration.Previous ||
            source == ProfileAuthoritySourceGeneration.Temp;

        private static bool IsSchemaOneProfile(SaveGameData save) =>
            save != null &&
            string.Equals(
                save.SaveFormatId,
                SaveGameData.CurrentSaveFormatId,
                StringComparison.Ordinal) &&
            save.SaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
            save.ProfileInitializationVersion ==
                SaveAuthorityTechnicalLimits
                    .LegacyProfileInitializationVersion &&
            string.IsNullOrEmpty(save.ProfileId);

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return LowerHex(sha256.ComputeHash(bytes));
            }
        }

        private static string LowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(
                    bytes[index].ToString(
                        "x2",
                        System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static SaveProfileIdentityMigrationResult Rejected(
            string diagnostic,
            int attemptCount) =>
            new SaveProfileIdentityMigrationResult(
                SaveProfileIdentityMigrationStatus.Rejected,
                string.Empty,
                null,
                null,
                null,
                null,
                null,
                false,
                attemptCount,
                new[] { diagnostic });

        private static SaveProfileIdentityMigrationResult Unavailable(
            string diagnostic) =>
            new SaveProfileIdentityMigrationResult(
                SaveProfileIdentityMigrationStatus.Unavailable,
                string.Empty,
                null,
                null,
                null,
                null,
                null,
                false,
                0,
                new[] { diagnostic });

        /// <summary>
        /// Pure projection source that performs the actual byte-level migration:
        /// it clones the schema-v1 save, applies the minted ProfileId and the
        /// schema-v2 bump, serializes the candidate, and builds the bounded
        /// witness. It retains no filesystem, service, or publication state.
        /// </summary>
        private sealed class RuntimeProjectionSource :
            IProfileIdentityMigrationProjectionSource
        {
            private readonly SaveGameData _legacySave;
            private readonly byte[] _predecessorBytes;
            private readonly string _operationId;

            internal RuntimeProjectionSource(
                SaveGameData legacySave,
                byte[] predecessorBytes,
                string operationId)
            {
                _legacySave = legacySave;
                _predecessorBytes = predecessorBytes;
                _operationId = operationId;
            }

            internal SaveGameData MigratedSave { get; private set; }
            internal byte[] CandidateBytes { get; private set; }
            internal ProfileIdentityMigrationWitnessRecord Witness
            {
                get;
                private set;
            }
            internal byte[] WitnessBytes { get; private set; }

            public ProfileIdentityMigrationProjection CreateProjection(
                ProfileIdentityMigrationMode mode,
                string profileId,
                ProfileIdentityMigrationEvidence evidence)
            {
                if (mode !=
                    ProfileIdentityMigrationMode.MigrateSchemaOne ||
                    evidence == null ||
                    evidence.SelectedPredecessor == null ||
                    !evidence.SelectedPredecessor.IsPresent)
                {
                    throw new InvalidOperationException(
                        "Unsupported migration projection request.");
                }

                SaveGameData clone = CloneSave(_legacySave);
                clone.ProfileId = profileId;
                clone.SaveSchemaVersion =
                    SaveAuthorityTechnicalLimits
                        .IdentityAwareSaveSchemaVersion;
                clone.ProfileInitializationVersion =
                    SaveAuthorityTechnicalLimits
                        .IdentityAwareProfileInitializationVersion;

                byte[] candidateBytes =
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(clone, true));
                string candidateSha256 = ComputeSha256(candidateBytes);

                var witness = new ProfileIdentityMigrationWitnessRecord
                {
                    ContractVersion =
                        ProfileIdentityMigrationTechnicalLimits.ContractVersion,
                    OperationId = _operationId,
                    SelectedLegacySourceGeneration =
                        (int)evidence.SelectedSourceGeneration,
                    PredecessorByteCount = _predecessorBytes.Length,
                    PredecessorSha256 =
                        ComputeSha256(_predecessorBytes),
                    ProfileId = profileId,
                    TargetSaveSchemaVersion =
                        SaveAuthorityTechnicalLimits
                            .IdentityAwareSaveSchemaVersion,
                    TargetProfileInitializationVersion =
                        SaveAuthorityTechnicalLimits
                            .IdentityAwareProfileInitializationVersion,
                    CandidateByteCount = candidateBytes.Length,
                    CandidateSha256 = candidateSha256
                };

                byte[] witnessBytes =
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(witness, true));

                MigratedSave = clone;
                CandidateBytes = candidateBytes;
                Witness = witness;
                WitnessBytes = witnessBytes;

                ProfileIdentityGenerationExpectation candidate =
                    ProfileIdentityGenerationExpectation.Exact(
                        ProfileAuthoritySourceGeneration.Primary,
                        candidateBytes.Length,
                        candidateSha256,
                        profileId,
                        SaveAuthorityTechnicalLimits
                            .IdentityAwareSaveSchemaVersion,
                        SaveAuthorityTechnicalLimits
                            .IdentityAwareProfileInitializationVersion);

                ProfileIdentityMigrationWitnessExpectation witnessExpectation =
                    new ProfileIdentityMigrationWitnessExpectation(
                        true,
                        ProfileIdentityMigrationTechnicalLimits.ContractVersion,
                        _operationId,
                        evidence.SelectedSourceGeneration,
                        evidence.SelectedPredecessor.ByteCount,
                        evidence.SelectedPredecessor.Sha256,
                        profileId,
                        SaveAuthorityTechnicalLimits
                            .IdentityAwareSaveSchemaVersion,
                        SaveAuthorityTechnicalLimits
                            .IdentityAwareProfileInitializationVersion,
                        candidateBytes.Length,
                        candidateSha256,
                        witnessBytes.Length,
                        ComputeSha256(witnessBytes));

                return new ProfileIdentityMigrationProjection(
                    evidence.SelectedPredecessor,
                    candidate,
                    witnessExpectation);
            }

            internal bool VerifyLedgerTwice(string profileId)
            {
                if (CandidateBytes == null || WitnessBytes == null)
                    return false;

                string firstCandidateHash = ComputeSha256(CandidateBytes);
                string secondCandidateHash = ComputeSha256(CandidateBytes);
                string firstWitnessHash = ComputeSha256(WitnessBytes);
                string secondWitnessHash = ComputeSha256(WitnessBytes);

                return string.Equals(
                           firstCandidateHash,
                           secondCandidateHash,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           firstWitnessHash,
                           secondWitnessHash,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           firstCandidateHash,
                           Witness.CandidateSha256,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           firstWitnessHash,
                           ComputeSha256(
                               Encoding.UTF8.GetBytes(
                                   JsonUtility.ToJson(Witness, true))),
                           StringComparison.Ordinal) &&
                       string.Equals(
                           Witness.ProfileId,
                           profileId,
                           StringComparison.Ordinal);
            }

            private static SaveGameData CloneSave(SaveGameData save) =>
                save == null
                    ? null
                    : JsonUtility.FromJson<SaveGameData>(
                        JsonUtility.ToJson(save));
        }
    }
}
