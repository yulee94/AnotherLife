using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Core.SaveAuthority.RuntimeBridge;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.ChampionMode.Death;
using AL.ChampionMode.Quests;
using AL.UI.Kingdom;

namespace AL.Services.Local
{
    public sealed partial class LocalSaveGameService
    {
        private const string ProfileMigrationWitnessFileName =
            "save.profile-migration.v1";
        private const string ProfileMigrationPendingFileName =
            "save.profile-migration.pending";
        private const string ProfileBoundRealmOperationId =
            "al.save.schema2.realm-selection.v1";
        private const string ProfileBoundKingdomOneBuildOperationId =
            "al.save.schema2.kingdom-one-build.v1";
        private const string ProfileBoundKingdomTeachingOperationId =
            "al.save.schema2.kingdom-teaching.v1";

        private string _authorityEpoch = string.Empty;
        private string _verifiedGenerationFingerprint = string.Empty;
        private ProfileWriteAuthoritySnapshot _cachedWritableAuthority;
        private long _publicationSequence;

        private string ProfileMigrationWitnessPath =>
            PathCombine(PersistencePath, ProfileMigrationWitnessFileName);

        private string ProfileMigrationPendingPath =>
            PathCombine(PersistencePath, ProfileMigrationPendingFileName);

        private static string PathCombine(string left, string right) =>
            System.IO.Path.Combine(left, right);

        private void ResetPublishedWritableAuthority()
        {
            _authorityEpoch = string.Empty;
            _verifiedGenerationFingerprint = string.Empty;
            _cachedWritableAuthority = null;
        }

        private bool HasExactSchemaTwoProfile(SaveGameData save) =>
            _profileWritable &&
            save != null &&
            save.SaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion &&
            save.ProfileInitializationVersion ==
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion &&
            IsCanonicalPublishedProfileId(save.ProfileId);

        private bool TryGetPublishedWritableAuthority(
            ProfileAuthoritySourceGeneration source,
            int saveSchemaVersion,
            int profileInitializationVersion,
            out ProfileWriteAuthoritySnapshot writable)
        {
            writable = null;
            if (!_profileWritable ||
                _currentSave == null ||
                saveSchemaVersion !=
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion ||
                profileInitializationVersion !=
                    SaveAuthorityTechnicalLimits
                        .IdentityAwareProfileInitializationVersion ||
                !IsCanonicalPublishedProfileId(_currentSave.ProfileId) ||
                source == ProfileAuthoritySourceGeneration.None)
            {
                return false;
            }

            if (!ActivatePublishedWritableAuthority(source))
            {
                return false;
            }

            if (_cachedWritableAuthority != null &&
                string.Equals(
                    _cachedWritableAuthority.ProfileId,
                    _currentSave.ProfileId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    _cachedWritableAuthority.AuthorityEpoch,
                    _authorityEpoch,
                    StringComparison.Ordinal) &&
                string.Equals(
                    _cachedWritableAuthority.VerifiedGenerationFingerprint,
                    _verifiedGenerationFingerprint,
                    StringComparison.Ordinal) &&
                _cachedWritableAuthority.SelectedSourceGeneration == source)
            {
                writable = _cachedWritableAuthority;
                return true;
            }

            ProfileWriteAuthoritySnapshot created =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    _currentSave.ProfileId,
                    _authorityEpoch,
                    _verifiedGenerationFingerprint,
                    source,
                    Array.Empty<string>());
            if (created.Status != ProfileWriteAuthorityStatus.Writable)
            {
                return false;
            }

            _cachedWritableAuthority = created;
            writable = created;
            return true;
        }

        private bool ActivatePublishedWritableAuthority(
            ProfileAuthoritySourceGeneration source)
        {
            if (!TryComputePublishedFingerprint(
                    source,
                    out string fingerprint))
            {
                ResetPublishedWritableAuthority();
                return false;
            }

            if (string.IsNullOrEmpty(_authorityEpoch))
            {
                AuthorityEpochAllocationResult allocation =
                    AuthorityEpochAllocator.ProcessLocal.Allocate();
                if (allocation == null ||
                    allocation.Status !=
                        AuthorityEpochAllocationStatus.Allocated ||
                    string.IsNullOrEmpty(allocation.AuthorityEpoch))
                {
                    ResetPublishedWritableAuthority();
                    return false;
                }

                _authorityEpoch = allocation.AuthorityEpoch;
            }

            if (!string.Equals(
                    _verifiedGenerationFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                _cachedWritableAuthority = null;
            }

            _verifiedGenerationFingerprint = fingerprint;
            return true;
        }

        private bool HasProfileIdentityWitnessEvidence() =>
            HasSaveEvidence(ProfileMigrationWitnessPath) ||
            HasSaveEvidence(ProfileMigrationPendingPath);

        private bool HasConflictingSchemaOneAndTwoGenerations(
            System.Collections.Generic.IReadOnlyList<SaveCandidateInventoryEntry> inventory)
        {
            bool schemaOne = false;
            bool schemaTwo = false;
            foreach (SaveCandidateInventoryEntry entry in inventory)
            {
                SaveSemanticCandidate candidate = entry?.SemanticCandidate;
                if (candidate == null || !candidate.HasExplicitSaveSchemaVersion)
                {
                    continue;
                }

                if (candidate.SaveSchemaVersion ==
                    SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion)
                {
                    schemaOne = true;
                }
                else if (candidate.SaveSchemaVersion ==
                         SaveAuthorityTechnicalLimits
                             .IdentityAwareSaveSchemaVersion)
                {
                    schemaTwo = true;
                }
            }

            return schemaOne && schemaTwo;
        }

        private bool HasMatchingProfileIdentityWitness(
            SaveCandidateInventoryEntry primary,
            SaveCandidateInventoryEntry backup)
        {
            return TryReadProfileIdentityWitness(
                       ProfileMigrationWitnessPath,
                       out ProfileIdentityMigrationWitnessRecord witness) &&
                   WitnessMatchesLedger(witness, primary, backup);
        }

        private bool TryResumeWitnessedSchemaTwoLedger(
            System.Collections.Generic.IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            SaveCandidateInventoryEntry primary,
            SaveCandidateInventoryEntry backup,
            out SaveGameData resumedSave,
            out string message)
        {
            resumedSave = null;
            message = string.Empty;
            if (!TryReadProfileIdentityWitness(
                    ProfileMigrationWitnessPath,
                    out ProfileIdentityMigrationWitnessRecord witness) ||
                !WitnessMatchesLedger(witness, primary, backup) ||
                !TryDeserializeSelectedCandidate(
                    primary.SemanticCandidate,
                    out SaveGameData save) ||
                !HasExactSchemaTwoMetadata(save) ||
                !string.Equals(
                    save.ProfileId,
                    witness.ProfileId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            resumedSave = save;
            message =
                "AL-SAVE-SCHEMA-TWO-WITNESS-RESUMED: A verified schema-2 primary with the exact schema-1 predecessor backup and matching identity witness was restored without rewriting the profile.";
            return true;
        }

        private void TryInstallSchemaTwoProfileIdentity(
            System.Collections.Generic.IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            SaveSemanticCandidate selected)
        {
            if (selected == null ||
                !selected.HasRetainedRawBytes ||
                !TryDeserializeSelectedCandidate(selected, out SaveGameData legacySave))
            {
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    selected,
                    "SAVE_SELECT_SCHEMA_ONE_MIGRATION_REQUIRED",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-SCHEMA-TWO-MIGRATION-EVIDENCE-MISSING: The schema-1 candidate could not be materialized; every generation was preserved.",
                    false);
                return;
            }

            byte[] predecessorBytes = selected.CopyRawBytes();
            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacySave,
                    predecessorBytes,
                    MapSource(selected.SourceGeneration),
                    new CryptographicProfileIdentityCandidateSource());
            if (result == null || !result.IsMigrated || !result.LedgerVerified)
            {
                _currentSave = null;
                _readOnlyCandidate = CloneSave(legacySave);
                PublishDisposition(
                    inventory,
                    selected,
                    "SAVE_SELECT_SCHEMA_ONE_MIGRATION_REQUIRED",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-SCHEMA-TWO-MIGRATION-REJECTED: Schema-1 identity migration did not produce a verified candidate; original evidence was preserved.",
                    false);
                return;
            }

            if (!TryPersistMigratedSchemaTwoLedger(
                    selected,
                    result,
                    predecessorBytes,
                    out SaveGameData persisted,
                    out bool diskChanged,
                    out string persistMessage))
            {
                _profileWritable = false;
                _currentSave = null;
                _readOnlyCandidate = CloneSave(legacySave);
                PublishDisposition(
                    inventory,
                    selected,
                    "SAVE_SELECT_SCHEMA_ONE_MIGRATION_REQUIRED",
                    false,
                    false,
                    diskChanged);
                SetLoadStatus(
                    diskChanged
                        ? SaveLoadStatus.RecoveryRequired
                        : SaveLoadStatus.RecoveryFailed,
                    persistMessage,
                    true);
                return;
            }

            _currentSave = persisted;
            _readOnlyCandidate = null;
            _profileWritable = true;
            ObservePrimaryAuthority(persisted);
            PublishDisposition(
                inventory,
                selected,
                "SAVE_SELECT_SCHEMA_ONE_MIGRATION_REQUIRED",
                true,
                true,
                true);
            ActivatePublishedWritableAuthority(
                ProfileAuthoritySourceGeneration.Primary);
            SetLoadStatus(
                SaveLoadStatus.MigratedSchemaOne,
                persistMessage,
                false);
        }

        private bool TryPersistMigratedSchemaTwoLedger(
            SaveSemanticCandidate selected,
            SaveProfileIdentityMigrationResult result,
            byte[] predecessorBytes,
            out SaveGameData persisted,
            out bool diskChanged,
            out string message)
        {
            persisted = null;
            diskChanged = false;
            message = string.Empty;
            if (result.CandidateBytes == null ||
                result.WitnessBytes == null ||
                predecessorBytes == null ||
                HasSaveEvidence(ProfileMigrationPendingPath) ||
                HasSaveEvidence(ProfileMigrationWitnessPath) ||
                HasSaveEvidence(TempPath) ||
                !TryGetExactUtf8(
                    result.CandidateBytes,
                    out string candidateJson,
                    out _) ||
                !TryGetExactUtf8(
                    predecessorBytes,
                    out string predecessorJson,
                    out _) ||
                !TryGetExactUtf8(
                    result.WitnessBytes,
                    out string witnessJson,
                    out _))
            {
                message =
                    "AL-SAVE-SCHEMA-TWO-MIGRATION-NOT-STAGED: Residual temp or witness evidence blocked installation; original generations were preserved.";
                return false;
            }

            try
            {
                _fileOperations.CreateDirectory(PersistencePath);
                SaveFileWriteResult pending =
                    _fileOperations.WriteAllTextDurable(
                        ProfileMigrationPendingPath,
                        witnessJson);
                diskChanged |= pending.DiskChanged;
                if (!pending.Succeeded)
                {
                    message =
                        "AL-SAVE-SCHEMA-TWO-WITNESS-PENDING-FAILED: The migration pending witness could not be written; original generations were preserved.";
                    return false;
                }

                SaveFileWriteResult staged =
                    _fileOperations.WriteAllTextDurable(TempPath, candidateJson);
                diskChanged |= staged.DiskChanged;
                if (!staged.Succeeded)
                {
                    message =
                        "AL-SAVE-SCHEMA-TWO-CANDIDATE-STAGE-FAILED: The schema-2 candidate could not be staged; original generations were preserved.";
                    return false;
                }

                if (selected.SourceGeneration == SaveCandidateSourceGeneration.Primary &&
                    HasSaveEvidence(SavePath))
                {
                    _fileOperations.Replace(TempPath, SavePath, BackupPath);
                }
                else
                {
                    if (HasSaveEvidence(SavePath) &&
                        !TryQuarantineInvalidFile(SavePath, out string quarantineError))
                    {
                        message =
                            "AL-SAVE-SCHEMA-TWO-PRIMARY-QUARANTINE-FAILED: " +
                            quarantineError;
                        return false;
                    }

                    if (!HasSaveEvidence(BackupPath))
                    {
                        SaveFileWriteResult backupWrite =
                            _fileOperations.WriteAllTextDurable(
                                BackupPath,
                                predecessorJson);
                        diskChanged |= backupWrite.DiskChanged;
                        if (!backupWrite.Succeeded)
                        {
                            message =
                                "AL-SAVE-SCHEMA-TWO-PREDECESSOR-BACKUP-FAILED: The schema-1 predecessor could not be retained as backup.";
                            return false;
                        }
                    }

                    _fileOperations.Move(TempPath, SavePath);
                }

                diskChanged = true;
                SaveFileWriteResult finalWitness =
                    _fileOperations.WriteAllTextDurable(
                        ProfileMigrationWitnessPath,
                        witnessJson);
                if (!finalWitness.Succeeded)
                {
                    message =
                        "AL-SAVE-SCHEMA-TWO-WITNESS-FINAL-FAILED: Schema-2 files were preserved with the pending witness for explicit recovery.";
                    return false;
                }

                TryDelete(ProfileMigrationPendingPath);
                if (!TryReadValidSave(SavePath, out persisted, out string primaryError) ||
                    !TryVerifyPredecessorBackup(predecessorBytes) ||
                    !HasMatchingProfileIdentityWitness(
                        Find(
                            BuildCandidateInventory(),
                            SaveCandidateSourceGeneration.Primary),
                        Find(
                            BuildCandidateInventory(),
                            SaveCandidateSourceGeneration.Backup)))
                {
                    message =
                        "AL-SAVE-SCHEMA-TWO-LEDGER-VERIFY-FAILED: Installed evidence could not be verified twice; every remaining generation was preserved. " +
                        primaryError;
                    persisted = null;
                    return false;
                }

                message =
                    "AL-SAVE-SCHEMA-TWO-MIGRATED: A schema-1 profile was bound to a canonical ProfileId, persisted as schema 2, and verified before Writable publication.";
                return true;
            }
            catch (Exception ex)
            {
                message =
                    "AL-SAVE-SCHEMA-TWO-MIGRATION-INTERRUPTED: " +
                    ex.GetType().Name +
                    "; every remaining generation was preserved.";
                persisted = null;
                return false;
            }
        }

        RealmSelectionResult
            IProfileBoundRealmSelectionCandidateStore
            .TryCommitProfileBoundRealmSelection(RealmSelectionRequest request)
        {
            if (!RealmSelectionAuthority.IsBoundedIdentity(request.TransactionId) ||
                !RealmSelectionAuthority.IsBoundedIdentity(request.CorrelationId))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.InvalidTransaction,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-TRANSACTION-INVALID");
            }

            if (!RealmSelectionAuthority.IsDefinedPlayable(request.RequestedRealmId))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.InvalidRealm,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-REQUEST-INVALID");
            }

            if (!HasExactSchemaTwoProfile(_currentSave))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.ProfileUnavailable,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-PROFILE-NOT-SCHEMA-TWO");
            }

            ProfileWriteAuthoritySnapshot before = GetCurrentAuthority();
            if (before == null ||
                before.Status != ProfileWriteAuthorityStatus.Writable)
            {
                return MapNonWritableRealmAuthority(request.RequestedRealmId, before);
            }

            if (!string.IsNullOrEmpty(request.ExpectedProfileId) &&
                !string.Equals(
                    request.ExpectedProfileId,
                    before.ProfileId,
                    StringComparison.Ordinal))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.ProfileUnavailable,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-PROFILE-MISMATCH");
            }

            if (!string.IsNullOrEmpty(request.ExpectedGenerationFingerprint) &&
                !string.Equals(
                    request.ExpectedGenerationFingerprint,
                    before.VerifiedGenerationFingerprint,
                    StringComparison.Ordinal))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.InvalidTransaction,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-STALE-BASE");
            }

            RealmId publishedRealm = _currentSave.SelectedRealm;
            RealmSelectionAuthorityState publishedAuthority = _currentSave.RealmSelection;
            if (TryRejectIncoherentPublishedRealm(
                    publishedRealm,
                    publishedAuthority,
                    request.RequestedRealmId,
                    out RealmSelectionResult incoherent))
            {
                return incoherent;
            }

            if (IsCommittedAuthority(publishedAuthority))
            {
                if (!string.Equals(
                        publishedAuthority.CorrelationId,
                        request.CorrelationId,
                        StringComparison.Ordinal) &&
                    publishedRealm != request.RequestedRealmId)
                {
                    return LegacyRealmResult(
                        RealmSelectionStatus.RejectedDifferentRealm,
                        request.RequestedRealmId,
                        false,
                        false,
                        "AL-REALM-DIFFERENT-REALM-REJECTED");
                }

                if (string.Equals(
                        publishedAuthority.CorrelationId,
                        request.CorrelationId,
                        StringComparison.Ordinal) &&
                    (!string.Equals(
                         publishedAuthority.TransactionId,
                         request.TransactionId,
                         StringComparison.Ordinal) ||
                     publishedRealm != request.RequestedRealmId))
                {
                    return LegacyRealmResult(
                        RealmSelectionStatus.InvalidTransaction,
                        request.RequestedRealmId,
                        false,
                        false,
                        "AL-REALM-CORRELATION-CONFLICT");
                }

                if (publishedRealm == request.RequestedRealmId)
                {
                    return LegacyRealmResult(
                        RealmSelectionStatus.AlreadyCommittedSameRealm,
                        request.RequestedRealmId,
                        false,
                        false,
                        "AL-REALM-ALREADY-COMMITTED");
                }

                return LegacyRealmResult(
                    RealmSelectionStatus.RejectedDifferentRealm,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-DIFFERENT-REALM-REJECTED");
            }

            if (RealmSelectionAuthority.IsDefinedPlayable(publishedRealm) &&
                publishedRealm != request.RequestedRealmId)
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.RejectedDifferentRealm,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-DIFFERENT-REALM-REJECTED");
            }

            bool legacyMigration =
                RealmSelectionAuthority.IsDefinedPlayable(publishedRealm) &&
                publishedRealm == request.RequestedRealmId;
            string transactionId = legacyMigration
                ? RealmSelectionAuthority.MigrationTransactionId(
                    before.ProfileId,
                    publishedRealm)
                : request.TransactionId;
            string correlationId = legacyMigration
                ? transactionId
                : request.CorrelationId;
            string eventId = legacyMigration
                ? string.Empty
                : RealmSelectionAuthority.EventId(transactionId);
            string provenance = legacyMigration
                ? RealmSelectionAuthority.LegacyMigrationProvenance
                : RealmSelectionAuthority.InitialProvenance;
            RealmId committedRealm = legacyMigration
                ? publishedRealm
                : request.RequestedRealmId;

            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(before),
                    ProfileBoundRealmOperationId,
                    transactionId,
                    candidate =>
                    {
                        if (!string.Equals(
                                candidate.ProfileId,
                                before.ProfileId,
                                StringComparison.Ordinal))
                        {
                            return SaveCandidateMutationPreparation.Rejected(
                                "AL-SAVE-PROFILE-ID-MUTATION-REJECTED");
                        }

                        if (legacyMigration &&
                            candidate.SelectedRealm != publishedRealm)
                        {
                            return SaveCandidateMutationPreparation.Rejected(
                                "AL-REALM-AUTHORITY-CONFLICT");
                        }

                        if (!legacyMigration)
                        {
                            candidate.SelectedRealm = committedRealm;
                        }

                        var authority = new RealmSelectionAuthorityState
                        {
                            Version = RealmSelectionAuthority.CurrentVersion,
                            Committed = true,
                            SelectedRealm = (int)committedRealm,
                            ProfileId = before.ProfileId,
                            TransactionId = transactionId,
                            CorrelationId = correlationId,
                            OperationId = ProfileBoundRealmOperationId,
                            EventId = eventId,
                            CatalogVersion = RealmCatalogRuntime.SupportedVersion,
                            Provenance = provenance,
                            ExpectedGenerationFingerprint =
                                before.VerifiedGenerationFingerprint,
                            Revision = 1
                        };
                        authority.ReceiptFingerprint =
                            RealmSelectionAuthority.ComputeReceiptFingerprint(
                                authority.ProfileId,
                                committedRealm,
                                authority.TransactionId,
                                authority.CorrelationId,
                                authority.OperationId,
                                authority.EventId,
                                authority.Provenance,
                                authority.Revision);
                        candidate.RealmSelection = authority;
                        return SaveCandidateMutationPreparation.Prepared();
                    });

            return MapBoundRealmCommit(
                request.RequestedRealmId,
                bound,
                legacyMigration);
        }

        SaveCandidateCommitResult
            IProfileBoundKingdomOneBuildCandidateStore
            .TryCommitProfileBoundKingdomOneBuild(
                KingdomOneBuildCommitRequest request)
        {
            if (!RealmSelectionAuthority.IsBoundedIdentity(
                    request.TransactionId))
            {
                return LegacyCandidateRejected(
                    "AL-KINGDOM-ONE-BUILD-TRANSACTION-INVALID");
            }

            if (!TryGetWritableKingdomAuthority(
                    request.ExpectedRealm,
                    out ProfileWriteAuthoritySnapshot authority))
            {
                return LegacyCandidateRejected(
                    "AL-KINGDOM-ONE-BUILD-PROFILE-READ-ONLY");
            }

            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(authority),
                    ProfileBoundKingdomOneBuildOperationId,
                    request.TransactionId,
                    candidate =>
                    {
                        if (!HasExactSchemaTwoMetadata(candidate) ||
                            candidate.SelectedRealm != request.ExpectedRealm)
                        {
                            return SaveCandidateMutationPreparation.Rejected(
                                "AL-KINGDOM-ONE-BUILD-AUTHORITY-CONFLICT");
                        }

                        KingdomOneBuildPrepareDisposition disposition =
                            KingdomOneBuildSaveCodec.PrepareCandidate(
                                candidate,
                                request,
                                out string message);
                        if (!HasExactSchemaTwoMetadata(candidate) ||
                            candidate.SelectedRealm != request.ExpectedRealm)
                        {
                            return SaveCandidateMutationPreparation.Rejected(
                                "AL-KINGDOM-ONE-BUILD-AUTHORITY-CONFLICT");
                        }

                        switch (disposition)
                        {
                            case KingdomOneBuildPrepareDisposition.Prepared:
                                return SaveCandidateMutationPreparation.Prepared();
                            case KingdomOneBuildPrepareDisposition.Duplicate:
                                return SaveCandidateMutationPreparation.Duplicate();
                            default:
                                return SaveCandidateMutationPreparation.Rejected(
                                    string.IsNullOrWhiteSpace(message)
                                        ? "AL-KINGDOM-ONE-BUILD-REQUEST-INVALID"
                                        : message);
                        }
                    });
            return bound?.CommitResult ??
                LegacyCandidateRejected(
                    "AL-KINGDOM-ONE-BUILD-COMMIT-UNAVAILABLE");
        }

        SaveCandidateCommitResult
            IProfileBoundKingdomTeachingCandidateStore
            .TryCommitProfileBoundKingdomTeaching(
                KingdomTeachingCommitRequest request)
        {
            if (!RealmSelectionAuthority.IsBoundedIdentity(
                    request.TransactionId))
            {
                return LegacyCandidateRejected(
                    "AL-KINGDOM-TEACHING-TRANSACTION-INVALID");
            }

            KingdomTeachingCatalog teachingCatalog;
            try
            {
                teachingCatalog = KingdomTeachingCatalog.LoadCanonical();
            }
            catch (Exception)
            {
                return LegacyCandidateRejected(
                    "AL-KINGDOM-TEACHING-CATALOG-INVALID");
            }

            if (!TryResolveKingdomTeachingStep(
                    request,
                    teachingCatalog,
                    out bool requiresTownHall))
            {
                return LegacyCandidateRejected(
                    "AL-KINGDOM-TEACHING-CATALOG-CONFLICT");
            }

            if (!TryGetWritableKingdomAuthority(
                    request.ExpectedRealm,
                    out ProfileWriteAuthoritySnapshot authority))
            {
                return LegacyCandidateRejected(
                    "AL-KINGDOM-TEACHING-PROFILE-READ-ONLY");
            }

            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(authority),
                    ProfileBoundKingdomTeachingOperationId,
                    request.TransactionId,
                    candidate =>
                    {
                        if (!HasExactSchemaTwoMetadata(candidate) ||
                            candidate.SelectedRealm != request.ExpectedRealm ||
                            !ProofOfWorthLordship.IsGranted(candidate))
                        {
                            return SaveCandidateMutationPreparation.Rejected(
                                "AL-KINGDOM-TEACHING-AUTHORITY-CONFLICT");
                        }

                        KingdomTeachingPrepareDisposition disposition =
                            KingdomTeachingSaveCodec.PrepareCandidate(
                                candidate,
                                request,
                                requiresTownHall,
                                out string message);
                        if (!HasExactSchemaTwoMetadata(candidate) ||
                            candidate.SelectedRealm != request.ExpectedRealm)
                        {
                            return SaveCandidateMutationPreparation.Rejected(
                                "AL-KINGDOM-TEACHING-AUTHORITY-CONFLICT");
                        }

                        switch (disposition)
                        {
                            case KingdomTeachingPrepareDisposition.Prepared:
                                return SaveCandidateMutationPreparation.Prepared();
                            case KingdomTeachingPrepareDisposition.Duplicate:
                                return SaveCandidateMutationPreparation.Duplicate();
                            default:
                                return SaveCandidateMutationPreparation.Rejected(
                                    string.IsNullOrWhiteSpace(message)
                                        ? "AL-KINGDOM-TEACHING-REQUEST-INVALID"
                                        : message);
                        }
                    });
            return bound?.CommitResult ??
                LegacyCandidateRejected(
                    "AL-KINGDOM-TEACHING-COMMIT-UNAVAILABLE");
        }

        private bool TryGetWritableKingdomAuthority(
            AL.Core.RealmId expectedRealm,
            out ProfileWriteAuthoritySnapshot authority)
        {
            authority = GetCurrentAuthority();
            return HasExactSchemaTwoProfile(_currentSave) &&
                expectedRealm != AL.Core.RealmId.None &&
                _currentSave.SelectedRealm == expectedRealm &&
                authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.Writable &&
                authority.SelectedSourceGeneration ==
                    ProfileAuthoritySourceGeneration.Primary &&
                string.Equals(
                    authority.ProfileId,
                    _currentSave.ProfileId,
                    StringComparison.Ordinal);
        }

        private static bool TryResolveKingdomTeachingStep(
            KingdomTeachingCommitRequest request,
            KingdomTeachingCatalog catalog,
            out bool requiresTownHall)
        {
            requiresTownHall = false;
            if (catalog == null ||
                request.ExpectedProgress < 0 ||
                request.ExpectedProgress >= catalog.Steps.Count ||
                request.StepCount != catalog.Steps.Count ||
                !string.Equals(
                    request.QuestId,
                    catalog.QuestId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            KingdomTeachingStep expectedStep =
                catalog.Steps[request.ExpectedProgress];
            if (!string.Equals(
                    request.StepId,
                    expectedStep.Id,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.CompletionEvent,
                    expectedStep.CompletionEvent,
                    StringComparison.Ordinal))
            {
                return false;
            }

            requiresTownHall = string.Equals(
                expectedStep.Interaction,
                "construct_town_hall",
                StringComparison.Ordinal);
            return true;
        }

        DeathPenaltyCommitResult
            IProfileBoundDeathPenaltyCandidateStore
            .TryCommitProfileBoundDeathPenalty(DeathPenaltyCommitRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.OperationId) ||
                string.IsNullOrWhiteSpace(request.DeathEventId) ||
                string.IsNullOrWhiteSpace(request.CombatSessionId) ||
                string.IsNullOrWhiteSpace(request.EncounterAttemptId) ||
                string.IsNullOrWhiteSpace(request.InstanceId))
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedInvalidRequest,
                    _currentSave,
                    DeathPenaltyCommitCodes.InvalidRequest);
            }

            if (!HasExactSchemaTwoProfile(_currentSave))
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedProfileUnavailable,
                    _currentSave,
                    DeathPenaltyCommitCodes.ProfileNotSchemaTwo);
            }

            ProfileWriteAuthoritySnapshot before = GetCurrentAuthority();
            if (before == null ||
                before.Status != ProfileWriteAuthorityStatus.Writable)
            {
                return MapNonWritableDeathAuthority(before);
            }

            if (!string.IsNullOrEmpty(request.ExpectedProfileId) &&
                !string.Equals(
                    request.ExpectedProfileId,
                    before.ProfileId,
                    StringComparison.Ordinal))
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedWrongProfile,
                    _currentSave,
                    DeathPenaltyCommitCodes.ProfileMismatch);
            }

            if (!string.IsNullOrEmpty(request.ExpectedGenerationFingerprint) &&
                !string.Equals(
                    request.ExpectedGenerationFingerprint,
                    before.VerifiedGenerationFingerprint,
                    StringComparison.Ordinal))
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedStale,
                    _currentSave,
                    DeathPenaltyCommitCodes.StaleBase);
            }

            DeathPenaltyCommitResult replay = DeathPenaltyTransaction.ReplayOrReject(
                _currentSave,
                request,
                before.ProfileId);
            if (replay != null)
            {
                return replay;
            }

            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(before),
                    DeathPenaltyIds.SchemaOperationId,
                    request.OperationId,
                    candidate => DeathPenaltyTransaction.Prepare(
                        candidate,
                        request,
                        before.ProfileId,
                        before.VerifiedGenerationFingerprint));

            return MapBoundDeathCommit(bound);
        }

        private DeathPenaltyCommitResult MapNonWritableDeathAuthority(
            ProfileWriteAuthoritySnapshot authority)
        {
            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.CommitUncertain)
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedSaveUncertain,
                    _currentSave,
                    DeathPenaltyCommitCodes.CommitUncertain);
            }

            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.ForwardSchemaReadOnly)
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedForward,
                    _currentSave,
                    DeathPenaltyCommitCodes.ForwardSchema);
            }

            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.DegradedReadOnly)
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedDegraded,
                    _currentSave,
                    DeathPenaltyCommitCodes.Degraded);
            }

            return DeathPenaltyTransaction.Reject(
                DeathPenaltyCommitStatus.RejectedReadOnly,
                _currentSave,
                DeathPenaltyCommitCodes.NotWritable);
        }

        private DeathPenaltyCommitResult MapBoundDeathCommit(
            ProfileBoundSaveCandidateCommitResult bound)
        {
            SaveCandidateCommitResult commit = bound?.CommitResult;
            if (commit == null)
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedReadOnly,
                    _currentSave,
                    DeathPenaltyCommitCodes.ReadOnly);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.Duplicate)
            {
                return DeathPenaltyTransaction.MapPublished(
                    commit.PublishedSave ?? _currentSave,
                    false,
                    false);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.Committed)
            {
                return DeathPenaltyTransaction.MapPublished(
                    commit.PublishedSave ?? _currentSave,
                    true,
                    true);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.CommitUncertain)
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedSaveUncertain,
                    _currentSave,
                    DeathPenaltyCommitCodes.CommitUncertain);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.ReadOnly)
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedReadOnly,
                    _currentSave,
                    string.IsNullOrEmpty(commit.Message)
                        ? DeathPenaltyCommitCodes.ReadOnly
                        : commit.Message);
            }

            return DeathPenaltyTransaction.Reject(
                DeathPenaltyCommitStatus.RejectedPlanner,
                _currentSave,
                string.IsNullOrEmpty(commit.Message)
                    ? DeathPenaltyCommitCodes.InvalidRequest
                    : commit.Message);
        }

        WishgateCommitResult
            IProfileBoundWishgateCandidateStore
            .TryCommitProfileBoundWishgate(
                WishgateCommitRequest request,
                WishgateDurableDependencies dependencies)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.OperationId) ||
                string.IsNullOrWhiteSpace(request.EventId) ||
                string.IsNullOrWhiteSpace(request.CorrelationId) ||
                string.IsNullOrWhiteSpace(request.ActorId) ||
                dependencies == null ||
                !dependencies.IsComplete)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedInvalidRequest,
                    _currentSave,
                    WishgateCommitCodes.InvalidRequest);
            }

            if (!HasExactSchemaTwoProfile(_currentSave))
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedProfileUnavailable,
                    _currentSave,
                    WishgateCommitCodes.ProfileNotSchemaTwo);
            }

            ProfileWriteAuthoritySnapshot before = GetCurrentAuthority();
            if (before == null ||
                before.Status != ProfileWriteAuthorityStatus.Writable)
            {
                return MapNonWritableWishgateAuthority(before);
            }

            if (!string.IsNullOrEmpty(request.ExpectedProfileId) &&
                !string.Equals(
                    request.ExpectedProfileId,
                    before.ProfileId,
                    StringComparison.Ordinal))
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedUnauthorized,
                    _currentSave,
                    WishgateCommitCodes.ProfileMismatch);
            }

            if (!string.IsNullOrEmpty(request.ExpectedGenerationFingerprint) &&
                !string.Equals(
                    request.ExpectedGenerationFingerprint,
                    before.VerifiedGenerationFingerprint,
                    StringComparison.Ordinal))
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedStale,
                    _currentSave,
                    WishgateCommitCodes.StaleBase);
            }

            WishgateCommitResult replay = WishgateDurableTransaction.ReplayOrReject(
                _currentSave,
                request);
            if (replay != null)
            {
                return replay;
            }

            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(before),
                    WishgateEngineeringIds.SchemaOperationId,
                    request.OperationId,
                    candidate => WishgateDurableTransaction.Prepare(
                        candidate,
                        request,
                        before.ProfileId,
                        dependencies));

            return MapBoundWishgateCommit(bound);
        }

        private WishgateCommitResult MapNonWritableWishgateAuthority(
            ProfileWriteAuthoritySnapshot authority)
        {
            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.CommitUncertain)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedSaveUncertain,
                    _currentSave,
                    WishgateCommitCodes.CommitUncertain);
            }

            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.ForwardSchemaReadOnly)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedForward,
                    _currentSave,
                    WishgateCommitCodes.ForwardSchema);
            }

            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.DegradedReadOnly)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedDegraded,
                    _currentSave,
                    WishgateCommitCodes.Degraded);
            }

            return WishgateDurableTransaction.Reject(
                WishgateCommitStatus.RejectedReadOnly,
                _currentSave,
                WishgateCommitCodes.NotWritable);
        }

        private WishgateCommitResult MapBoundWishgateCommit(
            ProfileBoundSaveCandidateCommitResult bound)
        {
            SaveCandidateCommitResult commit = bound?.CommitResult;
            if (commit == null)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedReadOnly,
                    _currentSave,
                    WishgateCommitCodes.ReadOnly);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.Duplicate)
            {
                return WishgateDurableTransaction.MapPublished(
                    commit.PublishedSave ?? _currentSave,
                    false,
                    false,
                    WishgateCommitStatus.Replayed);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.Committed)
            {
                return WishgateDurableTransaction.MapPublished(
                    commit.PublishedSave ?? _currentSave,
                    true,
                    true,
                    WishgateCommitStatus.Committed);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.CommitUncertain)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedSaveUncertain,
                    _currentSave,
                    WishgateCommitCodes.CommitUncertain);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.ReadOnly)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedReadOnly,
                    _currentSave,
                    string.IsNullOrEmpty(commit.Message)
                        ? WishgateCommitCodes.ReadOnly
                        : commit.Message);
            }

            if (string.Equals(commit.Message, WishgateCommitCodes.NoChange, StringComparison.Ordinal))
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.NoChange,
                    _currentSave,
                    WishgateCommitCodes.NoChange);
            }

            return WishgateDurableTransaction.MapRejectedCode(
                _currentSave,
                string.IsNullOrEmpty(commit.Message)
                    ? WishgateCommitCodes.InvalidRequest
                    : commit.Message);
        }

        private RealmSelectionResult MapBoundRealmCommit(
            RealmId requested,
            ProfileBoundSaveCandidateCommitResult bound,
            bool legacyMigration)
        {
            SaveCandidateCommitResult commit = bound?.CommitResult;
            if (commit == null)
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.SaveFailedPreviousPreserved,
                    requested,
                    false,
                    false,
                    "AL-REALM-SAVE-FAILED");
            }

            switch (commit.Outcome)
            {
                case SaveCandidateCommitOutcome.Committed:
                    return LegacyRealmResult(
                        legacyMigration
                            ? RealmSelectionStatus.AlreadyCommittedSameRealm
                            : RealmSelectionStatus.Committed,
                        requested,
                        true,
                        true,
                        legacyMigration
                            ? "AL-REALM-LEGACY-MIGRATED"
                            : "AL-REALM-COMMITTED");
                case SaveCandidateCommitOutcome.Duplicate:
                    return LegacyRealmResult(
                        RealmSelectionStatus.AlreadyCommittedSameRealm,
                        requested,
                        false,
                        false,
                        "AL-REALM-ALREADY-COMMITTED");
                case SaveCandidateCommitOutcome.CommitUncertain:
                    return LegacyRealmResult(
                        RealmSelectionStatus.CommitUncertain,
                        requested,
                        false,
                        false,
                        string.IsNullOrWhiteSpace(commit.Message)
                            ? "AL-REALM-COMMIT-UNCERTAIN"
                            : commit.Message);
                case SaveCandidateCommitOutcome.PreviousPreserved:
                    return LegacyRealmResult(
                        RealmSelectionStatus.SaveFailedPreviousPreserved,
                        requested,
                        false,
                        false,
                        string.IsNullOrWhiteSpace(commit.Message)
                            ? "AL-REALM-SAVE-FAILED"
                            : commit.Message);
                default:
                    return LegacyRealmResult(
                        RealmSelectionStatus.ProfileUnavailable,
                        requested,
                        false,
                        false,
                        string.IsNullOrWhiteSpace(commit.Message)
                            ? "AL-REALM-PROFILE-READ-ONLY"
                            : commit.Message);
            }
        }

        private RealmSelectionResult MapNonWritableRealmAuthority(
            RealmId requested,
            ProfileWriteAuthoritySnapshot authority)
        {
            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.CommitUncertain)
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.CommitUncertain,
                    requested,
                    false,
                    false,
                    "AL-REALM-COMMIT-UNCERTAIN");
            }

            if (authority != null &&
                authority.Status == ProfileWriteAuthorityStatus.ForwardSchemaReadOnly)
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.ProfileUnavailable,
                    requested,
                    false,
                    false,
                    "AL-REALM-PROFILE-READ-ONLY");
            }

            return LegacyRealmResult(
                RealmSelectionStatus.ProfileUnavailable,
                requested,
                false,
                false,
                "AL-REALM-PROFILE-NOT-WRITABLE");
        }

        private static bool IsCommittedAuthority(RealmSelectionAuthorityState authority)
        {
            return authority != null &&
                   authority.Committed &&
                   authority.Version == RealmSelectionAuthority.CurrentVersion &&
                   RealmSelectionAuthority.IsBoundedIdentity(authority.TransactionId) &&
                   RealmSelectionAuthority.IsBoundedIdentity(authority.CorrelationId) &&
                   RealmSelectionAuthority.IsBoundedIdentity(authority.ReceiptFingerprint);
        }

        private bool TryRejectIncoherentPublishedRealm(
            RealmId publishedRealm,
            RealmSelectionAuthorityState publishedAuthority,
            RealmId requested,
            out RealmSelectionResult result)
        {
            result = default;
            if (!Enum.IsDefined(typeof(RealmId), publishedRealm))
            {
                result = LegacyRealmResult(
                    RealmSelectionStatus.ProfileUnavailable,
                    requested,
                    false,
                    false,
                    "AL-REALM-PERSISTED-ID-INVALID");
                return true;
            }

            if (!IsCommittedAuthority(publishedAuthority))
            {
                return false;
            }

            var boundRealm = (RealmId)publishedAuthority.SelectedRealm;
            if (boundRealm != publishedRealm ||
                !RealmSelectionAuthority.IsDefinedPlayable(boundRealm) ||
                !string.Equals(
                    publishedAuthority.ProfileId,
                    _currentSave.ProfileId,
                    StringComparison.Ordinal) ||
                publishedAuthority.ReceiptFingerprint !=
                    RealmSelectionAuthority.ComputeReceiptFingerprint(
                        publishedAuthority.ProfileId,
                        boundRealm,
                        publishedAuthority.TransactionId,
                        publishedAuthority.CorrelationId,
                        publishedAuthority.OperationId,
                        publishedAuthority.EventId,
                        publishedAuthority.Provenance,
                        publishedAuthority.Revision))
            {
                result = LegacyRealmResult(
                    RealmSelectionStatus.ProfileUnavailable,
                    requested,
                    false,
                    false,
                    "AL-REALM-RECEIPT-RECONCILE-FAILED");
                return true;
            }

            return false;
        }

        ProfileBoundSaveCandidateCommitResult
            IProfileBoundSaveGameCandidateStore.TryCommitCandidate(
                ProfileAuthorityExpectation expectation,
                string operationId,
                string resultId,
                Func<SaveGameData, SaveCandidateMutationPreparation> prepareCandidate)
        {
            ProfileWriteAuthoritySnapshot current = GetCurrentAuthority();
            if (current == null ||
                current.Status != ProfileWriteAuthorityStatus.Writable ||
                current.SelectedSourceGeneration !=
                    ProfileAuthoritySourceGeneration.Primary ||
                expectation == null ||
                !string.Equals(
                    current.ProfileId,
                    expectation.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    current.AuthorityEpoch,
                    expectation.AuthorityEpoch,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    current.VerifiedGenerationFingerprint,
                    expectation.ExpectedGenerationFingerprint,
                    StringComparison.Ordinal))
            {
                return BoundResult(
                    SaveCandidateCommitOutcome.ReadOnly,
                    _currentSave,
                    "AL-SAVE-PROFILE-BOUND-STALE-AUTHORITY",
                    ProfileBoundReceiptSupport.Uncertain(
                        NextPublicationSequence(),
                        expectation?.ProfileId,
                        expectation?.ExpectedGenerationFingerprint,
                        operationId,
                        resultId,
                        "AL-SAVE-PROFILE-BOUND-STALE-AUTHORITY"));
            }

            string frozenProfileId = current.ProfileId;
            SaveCandidateCommitResult commit = TryCommitLegacyCandidateCore(
                candidate =>
                {
                    string before = candidate.ProfileId ?? string.Empty;
                    SaveCandidateMutationPreparation prepared =
                        prepareCandidate(candidate);
                    if (prepared == null)
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-SAVE-CANDIDATE-PREPARATION-MISSING");
                    }

                    if (!string.Equals(
                            before,
                            frozenProfileId,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            candidate.ProfileId ?? string.Empty,
                            frozenProfileId,
                            StringComparison.Ordinal))
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-SAVE-PROFILE-ID-MUTATION-REJECTED");
                    }

                    return prepared;
                });

            _cachedWritableAuthority = null;
            ActivatePublishedWritableAuthority(
                ProfileAuthoritySourceGeneration.Primary);
            ProfileWriteAuthoritySnapshot after = GetCurrentAuthority();
            ulong sequence = NextPublicationSequence();
            if (commit.IsCommitted &&
                after != null &&
                after.Status == ProfileWriteAuthorityStatus.Writable)
            {
                Nvs01OperationReceiptData operation =
                    commit.PublishedSave?.Nvs01Progress?.LastOperation;
                return BoundResult(
                    commit.Outcome,
                    commit.PublishedSave,
                    commit.Message,
                    ProfileBoundReceiptSupport.Committed(
                        sequence,
                        after.ProfileId,
                        operation == null
                            ? current.VerifiedGenerationFingerprint
                            : operation.ExpectedGenerationFingerprint,
                        after.VerifiedGenerationFingerprint,
                        after.AuthorityEpoch,
                        operation == null ? operationId : operation.OperationId,
                        operation == null ? resultId : operation.EventId,
                        operation == null
                            ? after.VerifiedGenerationFingerprint
                            : operation.PayloadFingerprint));
            }

            return BoundResult(
                commit.Outcome,
                commit.PublishedSave,
                commit.Message,
                ProfileBoundReceiptSupport.Uncertain(
                    sequence,
                    frozenProfileId,
                    current.VerifiedGenerationFingerprint,
                    operationId,
                    resultId,
                    commit.Message));
        }

        private static ProfileBoundSaveCandidateCommitResult BoundResult(
            SaveCandidateCommitOutcome outcome,
            SaveGameData save,
            string message,
            ProfileMutationReceipt receipt) =>
            new ProfileBoundSaveCandidateCommitResult(
                new SaveCandidateCommitResult(outcome, save, message),
                receipt);

        private ulong NextPublicationSequence() =>
            (ulong)Interlocked.Increment(ref _publicationSequence);

        private bool TryComputePublishedFingerprint(
            ProfileAuthoritySourceGeneration source,
            out string fingerprint)
        {
            fingerprint = string.Empty;
            SaveFileReadResult primary = ReadCanonicalPath(SavePath);
            SaveFileReadResult backup = ReadCanonicalPath(BackupPath);
            SaveFileReadResult temp = ReadCanonicalPath(TempPath);
            SaveFileReadResult previous = ReadCanonicalPath(PreviousPath);
            SaveFileReadResult legacyPrevious = ReadCanonicalPath(LegacyPreviousPath);
            SaveFileReadResult witness = ReadCanonicalPath(ProfileMigrationWitnessPath);
            if (primary.Disposition != SaveFileReadDisposition.Read ||
                primary.Bytes == null ||
                backup.Disposition != SaveFileReadDisposition.Read ||
                backup.Bytes == null)
            {
                return false;
            }

            var frame = new VerifiedGenerationFingerprintFrame(
                _currentSave.ProfileId,
                SaveGameData.CurrentSaveFormatId,
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion,
                source,
                VerifiedAuthorityLedgerState.CanonicalCurrent,
                Exact(AuthorityArtifactRole.Primary, primary.Bytes),
                Exact(AuthorityArtifactRole.Backup, backup.Bytes),
                ArtifactOrMissing(AuthorityArtifactRole.Temp, temp),
                ArtifactOrMissing(
                    AuthorityArtifactRole.CanonicalPrevious,
                    previous),
                ArtifactOrMissing(
                    AuthorityArtifactRole.LegacyPrevious,
                    legacyPrevious),
                ArtifactOrMissing(AuthorityArtifactRole.RecoveryWitness, witness));
            VerifiedGenerationFingerprintResult computed =
                VerifiedGenerationFingerprint.Compute(frame);
            if (computed.Status != VerifiedGenerationFingerprintStatus.Computed)
            {
                return false;
            }

            fingerprint = computed.Value;
            return true;
        }

        private static SerializedAuthorityArtifactIdentity Exact(
            AuthorityArtifactRole role,
            byte[] bytes) =>
            new SerializedAuthorityArtifactIdentity(
                role,
                AuthorityArtifactDisposition.VerifiedExact,
                bytes.Length,
                ComputeSha256Hex(bytes));

        private static SerializedAuthorityArtifactIdentity ArtifactOrMissing(
            AuthorityArtifactRole role,
            SaveFileReadResult read)
        {
            if (read != null &&
                read.Disposition == SaveFileReadDisposition.Read &&
                read.Bytes != null)
            {
                return Exact(role, read.Bytes);
            }

            return new SerializedAuthorityArtifactIdentity(
                role,
                AuthorityArtifactDisposition.Missing,
                0,
                string.Empty);
        }

        private bool TryReadProfileIdentityWitness(
            string path,
            out ProfileIdentityMigrationWitnessRecord witness)
        {
            witness = null;
            SaveFileReadResult read = ReadCanonicalPath(path);
            if (read.Disposition != SaveFileReadDisposition.Read ||
                read.Bytes == null ||
                !TryGetExactUtf8(read.Bytes, out string json, out _))
            {
                return false;
            }

            try
            {
                witness = UnityEngine.JsonUtility
                    .FromJson<ProfileIdentityMigrationWitnessRecord>(json);
            }
            catch
            {
                return false;
            }

            return witness != null &&
                   IsCanonicalPublishedProfileId(witness.ProfileId);
        }

        private static bool WitnessMatchesLedger(
            ProfileIdentityMigrationWitnessRecord witness,
            SaveCandidateInventoryEntry primary,
            SaveCandidateInventoryEntry backup)
        {
            if (witness == null ||
                primary?.SemanticCandidate == null ||
                backup?.SemanticCandidate == null ||
                !primary.SemanticCandidate.HasRetainedRawBytes ||
                !backup.SemanticCandidate.HasRetainedRawBytes)
            {
                return false;
            }

            byte[] primaryBytes = primary.SemanticCandidate.CopyRawBytes();
            byte[] backupBytes = backup.SemanticCandidate.CopyRawBytes();
            return primaryBytes != null &&
                   backupBytes != null &&
                   primaryBytes.Length == witness.CandidateByteCount &&
                   backupBytes.Length == witness.PredecessorByteCount &&
                   string.Equals(
                       ComputeSha256Hex(primaryBytes),
                       witness.CandidateSha256,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       ComputeSha256Hex(backupBytes),
                       witness.PredecessorSha256,
                       StringComparison.Ordinal) &&
                   primary.SemanticCandidate.SaveSchemaVersion ==
                       witness.TargetSaveSchemaVersion &&
                   backup.SemanticCandidate.SaveSchemaVersion ==
                       SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion;
        }

        private bool TryVerifyPredecessorBackup(byte[] predecessorBytes)
        {
            SaveFileReadResult backup = ReadCanonicalPath(BackupPath);
            return backup.Disposition == SaveFileReadDisposition.Read &&
                   BytesEqual(backup.Bytes, predecessorBytes);
        }

        private static bool HasExactSchemaTwoMetadata(SaveGameData save) =>
            save != null &&
            string.Equals(
                save.SaveFormatId,
                SaveGameData.CurrentSaveFormatId,
                StringComparison.Ordinal) &&
            save.SaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion &&
            save.ProfileInitializationVersion ==
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion &&
            IsCanonicalPublishedProfileId(save.ProfileId);

        private static bool IsCanonicalPublishedProfileId(string value)
        {
            if (value == null ||
                value.Length != 36 ||
                !value.StartsWith("alp_", StringComparison.Ordinal))
            {
                return false;
            }

            bool anyNonZero = false;
            for (int index = 4; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= '0' && character <= '9' ||
                      character >= 'a' && character <= 'f'))
                {
                    return false;
                }

                if (character != '0')
                {
                    anyNonZero = true;
                }
            }

            return anyNonZero;
        }

        private static ProfileAuthoritySourceGeneration MapSource(
            SaveCandidateSourceGeneration source)
        {
            switch (source)
            {
                case SaveCandidateSourceGeneration.Primary:
                    return ProfileAuthoritySourceGeneration.Primary;
                case SaveCandidateSourceGeneration.Backup:
                    return ProfileAuthoritySourceGeneration.Backup;
                case SaveCandidateSourceGeneration.Previous:
                    return ProfileAuthoritySourceGeneration.Previous;
                case SaveCandidateSourceGeneration.Temp:
                    return ProfileAuthoritySourceGeneration.Temp;
                default:
                    return ProfileAuthoritySourceGeneration.None;
            }
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    builder.Append(
                        digest[index].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
