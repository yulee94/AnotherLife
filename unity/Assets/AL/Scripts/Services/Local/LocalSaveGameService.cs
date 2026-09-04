using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.RealmSelection;
using AL.ChampionMode.Quests;
using AL.UI.Kingdom;

namespace AL.Services.Local
{
    internal interface ISaveFileOperations
    {
        bool FileExists(string path);
        void CreateDirectory(string path);
        SaveFileReadResult ReadAllBytesBounded(string path, int maximumBytes);
        SaveFileWriteResult WriteAllTextDurable(string path, string contents);
        void Copy(string sourcePath, string destinationPath, bool overwrite);
        void Move(string sourcePath, string destinationPath);
        void Replace(string sourcePath, string destinationPath, string backupPath);
        void Delete(string path);
        IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern);
        DateTime GetCreationTimeUtc(string path);
        bool IsReparsePoint(string path);
    }

    internal sealed class SaveFileReadResult
    {
        public SaveFileReadResult(
            SaveFileReadDisposition disposition,
            byte[] bytes,
            long observedByteCount,
            string diagnosticCode)
        {
            Disposition = disposition;
            Bytes = bytes;
            ObservedByteCount = observedByteCount < 0 ? 0 : observedByteCount;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public SaveFileReadDisposition Disposition { get; }
        public byte[] Bytes { get; }
        public long ObservedByteCount { get; }
        public string DiagnosticCode { get; }
    }

    internal sealed class SaveFileWriteResult
    {
        public SaveFileWriteResult(
            bool succeeded,
            bool diskChanged,
            string diagnosticCode)
        {
            Succeeded = succeeded;
            DiskChanged = diskChanged;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool DiskChanged { get; }
        public string DiagnosticCode { get; }
    }

    internal sealed class SystemSaveFileOperations : ISaveFileOperations
    {
        public bool FileExists(string path) => File.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public SaveFileReadResult ReadAllBytesBounded(string path, int maximumBytes)
        {
            if (maximumBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            }

            try
            {
                using var stream = OpenBoundedReadStream(path);
                long observedLength = stream.Length;
                if (observedLength > maximumBytes)
                {
                    return new SaveFileReadResult(
                        SaveFileReadDisposition.Oversize,
                        null,
                        observedLength,
                        "SAVE_FILE_OVERSIZE");
                }

                var bytes = new byte[(int)observedLength];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0)
                    {
                        return new SaveFileReadResult(
                            SaveFileReadDisposition.ChangedDuringRead,
                            null,
                            observedLength,
                            "SAVE_FILE_SHORT_READ");
                    }

                    offset += read;
                }

                if (stream.Length != observedLength || stream.ReadByte() != -1)
                {
                    return new SaveFileReadResult(
                        SaveFileReadDisposition.ChangedDuringRead,
                        null,
                        Math.Max(observedLength, stream.Length),
                        "SAVE_FILE_CHANGED_DURING_READ");
                }

                return new SaveFileReadResult(
                    SaveFileReadDisposition.Read,
                    bytes,
                    observedLength,
                    string.Empty);
            }
            catch (FileNotFoundException)
            {
                return Missing();
            }
            catch (DirectoryNotFoundException)
            {
                return Missing();
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException)
            {
                return new SaveFileReadResult(
                    SaveFileReadDisposition.IoFailure,
                    null,
                    0,
                    "SAVE_FILE_IO_FAILURE");
            }
        }

        private static SaveFileReadResult Missing() =>
            new SaveFileReadResult(
                SaveFileReadDisposition.Missing,
                null,
                0,
                "SAVE_FILE_MISSING");

        private static FileStream OpenBoundedReadStream(string path) =>
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        public SaveFileWriteResult WriteAllTextDurable(string path, string contents)
        {
            bool diskChanged = false;
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                diskChanged = true;
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(contents);
                writer.Flush();
                stream.Flush(true);
                return new SaveFileWriteResult(true, true, string.Empty);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException)
            {
                return new SaveFileWriteResult(
                    false,
                    diskChanged,
                    "SAVE_FILE_WRITE_FAILED");
            }
        }

        public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
            File.Copy(sourcePath, destinationPath, overwrite);

        public void Move(string sourcePath, string destinationPath) =>
            File.Move(sourcePath, destinationPath);

        public void Replace(string sourcePath, string destinationPath, string backupPath) =>
            File.Replace(sourcePath, destinationPath, backupPath);

        public void Delete(string path) => File.Delete(path);

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern)
        {
            return Directory.Exists(directoryPath)
                ? Directory.EnumerateFiles(directoryPath, searchPattern)
                : Enumerable.Empty<string>();
        }

        public DateTime GetCreationTimeUtc(string path) => File.GetCreationTimeUtc(path);

        public bool IsReparsePoint(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    public sealed partial class LocalSaveGameService :
        ISaveGameService,
        ISaveLoadDispositionProvider,
        ISaveOperationDispositionProvider,
        ISaveGameCandidateStore,
        ILegacyRealmSelectionCandidateStore,
        IProfileBoundRealmSelectionCandidateStore,
        IProfileBoundDeathPenaltyCandidateStore,
        IProfileBoundWishgateCandidateStore,
        IProfileBoundTerritoryCaptureCandidateStore,
        ILegacyMvpLoopCandidateStore,
        ILegacyKingdomTeachingCandidateStore,
        ILegacyFirstWorldProgressCandidateStore,
        INvs01LegacyCandidateStore,
        IProfileWriteAuthorityProvider,
        IProfileBoundSaveGameCandidateStore
    {
        private const string SaveFileName = "save.json";
        private const string BackupFileName = "save.backup.json";
        private const string TempFileName = "save.tmp.json";
        private const string PreviousFileName = "save.previous.json";
        private const string Nvs01MigrationBackupArchivePrefix =
            PreviousFileName + ".migration-archive-";
        private const string Nvs01MigrationBackupArchivePattern =
            Nvs01MigrationBackupArchivePrefix + "*";
        private const string LegacyPreviousFileName = "save.json.previous";
        private const string StageFiveRecoveryMarkerFileName =
            "save.recovery.stage5";
        private const int MaxQuarantinesPerSource = 3;
        private const int MaxStageFiveQuarantineMarkers = 16;
        private const int MaxNvs01MigrationBackupArchives = 1;
        private const int Sha256Base64UrlLength = 43;
        private const int TransactionIdBase64UrlLength = 22;
        private const string AuthorityLegacyMigrationCode =
            "AL-SAVE-AUTH-LEGACY-MIGRATION-REQUIRED";
        private const string AuthorityForwardSchemaCode =
            "AL-SAVE-AUTH-FORWARD-SCHEMA-READ-ONLY";
        private const string AuthorityDegradedCode =
            "AL-SAVE-AUTH-DEGRADED-READ-ONLY";
        private const string AuthorityRecoveryCode =
            "AL-SAVE-AUTH-RECOVERY-REQUIRED";
        private const string AuthorityDeletedCode =
            "AL-SAVE-AUTH-DELETED";
        private const string LegacyRealmOperationId =
            "al.save.schema1.realm-selection.v1";
        private const string LegacyNvs01OperationId =
            "al.save.schema1.nvs01.v1";
        private const string LegacyMvpLoopOperationId =
            "al.save.schema1.mvp-loop.v1";
        private const string LegacyKingdomTeachingOperationId =
            "al.save.schema1.kingdom-teaching.v1";
        private const string LegacyFirstWorldProgressOperationId =
            "al.save.schema1.first-world-progress.v1";
#if UNITY_INCLUDE_TESTS
        [ThreadStatic] internal static Action BeforeDeleteArtifactsForTests;
#endif

        private static readonly ProfileWriteAuthoritySnapshot
            MigrationRequiredPrimary =
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Primary,
                    new[] { AuthorityLegacyMigrationCode });

        private static readonly ProfileWriteAuthoritySnapshot
            MigrationRequiredBackup =
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Backup,
                    new[] { AuthorityLegacyMigrationCode });

        private static readonly ProfileWriteAuthoritySnapshot
            MigrationRequiredPrevious =
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Previous,
                    new[] { AuthorityLegacyMigrationCode });

        private static readonly ProfileWriteAuthoritySnapshot
            MigrationRequiredTemp =
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Temp,
                    new[] { AuthorityLegacyMigrationCode });

        private static readonly ProfileWriteAuthoritySnapshot
            RecoveryRequiredAuthority =
                ProfileWriteAuthoritySnapshotFactory.NonWritable(
                    ProfileWriteAuthorityStatus.RecoveryRequired,
                    0,
                    0,
                    false,
                    ProfileAuthoritySourceGeneration.None,
                    new[] { AuthorityRecoveryCode });

        private static readonly ProfileWriteAuthoritySnapshot
            CommitUncertainAuthority =
                ProfileWriteAuthoritySnapshotFactory.NonWritable(
                    ProfileWriteAuthorityStatus.CommitUncertain,
                    0,
                    0,
                    false,
                    ProfileAuthoritySourceGeneration.None,
                    new[] { SaveAuthorityDiagnosticCodes.CommitUncertain });

        private static readonly ProfileWriteAuthoritySnapshot DeletedAuthority =
            ProfileWriteAuthoritySnapshotFactory.NonWritable(
                ProfileWriteAuthorityStatus.Deleted,
                0,
                0,
                false,
                ProfileAuthoritySourceGeneration.None,
                new[] { AuthorityDeletedCode });

        private static readonly ProfileWriteAuthoritySnapshot
            UnavailableAuthority =
                ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderInvariants);

        private enum InvalidPrimaryRecoveryStage
        {
            Initial = 0,
            BackupStaged = 1,
            PrimaryPreserved = 2,
            PrimaryInstalled = 3,
            Quarantined = 4
        }

        private sealed class InvalidPrimaryRecoveryPlan
        {
            public InvalidPrimaryRecoveryPlan(
                InvalidPrimaryRecoveryStage stage,
                SaveSemanticCandidate backupCandidate,
                byte[] invalidPrimaryBytes,
                byte[] backupBytes,
                string quarantinePath,
                byte[] transactionMarkerBytes)
            {
                Stage = stage;
                BackupCandidate = backupCandidate;
                InvalidPrimaryBytes = invalidPrimaryBytes;
                BackupBytes = backupBytes;
                QuarantinePath = quarantinePath;
                TransactionMarkerBytes = transactionMarkerBytes;
            }

            public InvalidPrimaryRecoveryStage Stage { get; }
            public SaveSemanticCandidate BackupCandidate { get; }
            public byte[] InvalidPrimaryBytes { get; }
            public byte[] BackupBytes { get; }
            public string QuarantinePath { get; }
            public byte[] TransactionMarkerBytes { get; }
        }

        private sealed class StageFiveQuarantineMarker
        {
            public StageFiveQuarantineMarker(string path)
            {
                Path = path;
            }

            public string Path { get; }
        }

        private sealed class StageFiveTransactionMarker
        {
            public StageFiveTransactionMarker(
                byte[] bytes,
                string quarantinePath,
                string backupIdentity,
                string invalidPrimaryIdentity)
            {
                Bytes = bytes;
                QuarantinePath = quarantinePath;
                BackupIdentity = backupIdentity;
                InvalidPrimaryIdentity = invalidPrimaryIdentity;
            }

            public byte[] Bytes { get; }
            public string QuarantinePath { get; }
            public string BackupIdentity { get; }
            public string InvalidPrimaryIdentity { get; }
        }

        private sealed class SaveCandidateInventoryEntry
        {
            public SaveCandidateInventoryEntry(
                SaveCandidateSourceGeneration source,
                SaveFileReadResult readResult,
                SaveSemanticCandidate semanticCandidate)
            {
                Source = source;
                ReadResult = readResult;
                SemanticCandidate = semanticCandidate;
            }

            public SaveCandidateSourceGeneration Source { get; }
            public SaveFileReadResult ReadResult { get; }
            public SaveSemanticCandidate SemanticCandidate { get; }
        }

        private readonly string _persistencePathOverride;
        private readonly ISaveFileOperations _fileOperations;
        private readonly SaveSemanticValidationPolicy _semanticPolicy;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private string PersistencePath => string.IsNullOrWhiteSpace(_persistencePathOverride)
            ? Application.persistentDataPath
            : _persistencePathOverride;
        private string SavePath => Path.Combine(PersistencePath, SaveFileName);
        private string BackupPath => Path.Combine(PersistencePath, BackupFileName);
        private string TempPath => Path.Combine(PersistencePath, TempFileName);
        private string PreviousPath => Path.Combine(PersistencePath, PreviousFileName);
        private string LegacyPreviousPath =>
            Path.Combine(PersistencePath, LegacyPreviousFileName);
        private string StageFiveRecoveryMarkerPath =>
            Path.Combine(PersistencePath, StageFiveRecoveryMarkerFileName);

        private SaveGameData _currentSave;
        private SaveGameData _readOnlyCandidate;
        private bool _profileWritable;
        private byte[] _committedRecoveryWitnessBytes;
        private byte[] _committedInvalidPrimaryWitnessBytes;
        private string _committedInvalidPrimaryQuarantinePath;
        private byte[] _committedInvalidPrimaryRecoveryMarkerBytes;
        private string _committedInvalidPrimaryRecoveryMarkerPath;
        private bool _profileDeleted;
        private bool _hasObservedAuthoritySource;
        private ProfileAuthoritySourceGeneration _observedAuthoritySource;
        private int _observedAuthoritySaveSchemaVersion;
        private int _observedAuthorityProfileInitializationVersion;
        private ProfileWriteAuthoritySnapshot
            _cachedObservedNonWritableAuthority;
        private ProfileWriteAuthorityStatus
            _cachedObservedNonWritableStatus;
        private ProfileAuthoritySourceGeneration
            _cachedObservedNonWritableSource;
        private int _cachedObservedNonWritableSaveSchemaVersion;
        private int
            _cachedObservedNonWritableProfileInitializationVersion;
        private int _legacyCandidateCommitActive;

        public SaveGameData CurrentSave => _currentSave;
        public SaveLoadStatus LastLoadStatus { get; private set; }
        public string LastLoadMessage { get; private set; } = string.Empty;
        public SaveOperationStatus LastSaveStatus { get; private set; }
        public string LastSaveMessage { get; private set; } = string.Empty;
        public SaveLoadDisposition LastLoadDisposition { get; private set; }
        public SaveOperationDisposition LastSaveDisposition { get; private set; }
        public SaveGameData ReadOnlyCandidateSnapshot => CloneSave(_readOnlyCandidate);
        public string LastPersistenceMessage => string.IsNullOrWhiteSpace(LastSaveMessage)
            ? LastLoadMessage
            : LastSaveMessage;

        public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
        {
            try
            {
                if (_profileDeleted)
                {
                    return DeletedAuthority;
                }

                if (LastSaveStatus == SaveOperationStatus.CommitUncertain)
                {
                    return CommitUncertainAuthority;
                }

                if (LastSaveStatus == SaveOperationStatus.DeleteFailed ||
                    LastLoadStatus == SaveLoadStatus.RecoveryFailed ||
                    LastLoadStatus == SaveLoadStatus.RecoveryRequired)
                {
                    return RecoveryRequiredAuthority;
                }

                if (!TryGetObservedAuthoritySource(
                        out ProfileAuthoritySourceGeneration source))
                {
                    return UnavailableAuthority;
                }

                GetObservedAuthorityVersions(
                    out int saveSchemaVersion,
                    out int profileInitializationVersion);

                if (LastLoadStatus ==
                    SaveLoadStatus.LoadedForwardSchemaReadOnly)
                {
                    if (saveSchemaVersion >
                            SaveAuthorityTechnicalLimits
                                .IdentityAwareSaveSchemaVersion ||
                        saveSchemaVersion ==
                            SaveAuthorityTechnicalLimits
                                .IdentityAwareSaveSchemaVersion &&
                        profileInitializationVersion >
                            SaveAuthorityTechnicalLimits
                                .IdentityAwareProfileInitializationVersion)
                    {
                        return GetOrCreateObservedNonWritableAuthority(
                            ProfileWriteAuthorityStatus
                                .ForwardSchemaReadOnly,
                            saveSchemaVersion,
                            profileInitializationVersion,
                            source);
                    }

                    return GetOrCreateDegradedAuthority(
                        saveSchemaVersion,
                        profileInitializationVersion,
                        source);
                }

                if (LastLoadStatus == SaveLoadStatus.LoadedPrimaryDegraded)
                {
                    return GetOrCreateDegradedAuthority(
                        saveSchemaVersion,
                        profileInitializationVersion,
                        source);
                }

                bool legacyCompatibilityView =
                    LastLoadStatus == SaveLoadStatus.LoadedPrimaryNormalized ||
                    LastLoadStatus ==
                        SaveLoadStatus.LoadedPrimaryWithPreservedUnknown;
                if (LastSaveStatus ==
                        SaveOperationStatus.SaveFailedPreviousPreserved ||
                    !_profileWritable && !legacyCompatibilityView ||
                    _currentSave == null && !legacyCompatibilityView)
                {
                    return GetOrCreateDegradedAuthority(
                        saveSchemaVersion,
                        profileInitializationVersion,
                        source);
                }

                if (TryGetPublishedWritableAuthority(
                        source,
                        saveSchemaVersion,
                        profileInitializationVersion,
                        out ProfileWriteAuthoritySnapshot writable))
                {
                    return writable;
                }

                if (saveSchemaVersion ==
                        SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
                    profileInitializationVersion ==
                        SaveAuthorityTechnicalLimits
                            .LegacyProfileInitializationVersion)
                {
                    return MigrationRequiredFor(source);
                }

                return GetOrCreateDegradedAuthority(
                    saveSchemaVersion,
                    profileInitializationVersion,
                    source);
            }
            catch
            {
                return UnavailableAuthority;
            }
        }

        public LocalSaveGameService() : this(null)
        {
        }

        internal LocalSaveGameService(string persistencePathOverride)
            : this(persistencePathOverride, new SystemSaveFileOperations())
        {
        }

        internal LocalSaveGameService(string persistencePathOverride, ISaveFileOperations fileOperations)
            : this(persistencePathOverride, fileOperations, CreateSemanticPolicy())
        {
        }

        internal LocalSaveGameService(
            string persistencePathOverride,
            ISaveFileOperations fileOperations,
            SaveSemanticValidationPolicy semanticPolicy)
        {
            _persistencePathOverride = persistencePathOverride;
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _semanticPolicy = semanticPolicy ?? throw new ArgumentNullException(nameof(semanticPolicy));
        }

        private bool TryGetObservedAuthoritySource(
            out ProfileAuthoritySourceGeneration source)
        {
            if (_hasObservedAuthoritySource)
            {
                source = _observedAuthoritySource;
                return source != ProfileAuthoritySourceGeneration.None;
            }

            if (_currentSave != null)
            {
                source = ProfileAuthoritySourceGeneration.Primary;
                return true;
            }

            source = ProfileAuthoritySourceGeneration.None;
            return false;
        }

        private void GetObservedAuthorityVersions(
            out int saveSchemaVersion,
            out int profileInitializationVersion)
        {
            SaveGameData observed = _currentSave ?? _readOnlyCandidate;
            if (observed != null)
            {
                saveSchemaVersion = observed.SaveSchemaVersion;
                profileInitializationVersion =
                    observed.ProfileInitializationVersion;
                return;
            }

            saveSchemaVersion = _observedAuthoritySaveSchemaVersion;
            profileInitializationVersion =
                _observedAuthorityProfileInitializationVersion;
        }

        private static ProfileWriteAuthoritySnapshot MigrationRequiredFor(
            ProfileAuthoritySourceGeneration source)
        {
            switch (source)
            {
                case ProfileAuthoritySourceGeneration.Primary:
                    return MigrationRequiredPrimary;
                case ProfileAuthoritySourceGeneration.Backup:
                    return MigrationRequiredBackup;
                case ProfileAuthoritySourceGeneration.Previous:
                    return MigrationRequiredPrevious;
                case ProfileAuthoritySourceGeneration.Temp:
                    return MigrationRequiredTemp;
                default:
                    return UnavailableAuthority;
            }
        }

        private ProfileWriteAuthoritySnapshot GetOrCreateDegradedAuthority(
            int saveSchemaVersion,
            int profileInitializationVersion,
            ProfileAuthoritySourceGeneration source)
        {
            if (saveSchemaVersion <= 0 ||
                profileInitializationVersion <= 0 ||
                source == ProfileAuthoritySourceGeneration.None)
            {
                return UnavailableAuthority;
            }

            return GetOrCreateObservedNonWritableAuthority(
                ProfileWriteAuthorityStatus.DegradedReadOnly,
                saveSchemaVersion,
                profileInitializationVersion,
                source);
        }

        private ProfileWriteAuthoritySnapshot
            GetOrCreateObservedNonWritableAuthority(
                ProfileWriteAuthorityStatus status,
                int saveSchemaVersion,
                int profileInitializationVersion,
                ProfileAuthoritySourceGeneration source)
        {
            if (_cachedObservedNonWritableAuthority != null &&
                _cachedObservedNonWritableStatus == status &&
                _cachedObservedNonWritableSaveSchemaVersion ==
                    saveSchemaVersion &&
                _cachedObservedNonWritableProfileInitializationVersion ==
                    profileInitializationVersion &&
                _cachedObservedNonWritableSource == source)
            {
                return _cachedObservedNonWritableAuthority;
            }

            string diagnosticCode;
            switch (status)
            {
                case ProfileWriteAuthorityStatus.ForwardSchemaReadOnly:
                    diagnosticCode = AuthorityForwardSchemaCode;
                    break;
                case ProfileWriteAuthorityStatus.DegradedReadOnly:
                    diagnosticCode = AuthorityDegradedCode;
                    break;
                default:
                    return UnavailableAuthority;
            }

            ProfileWriteAuthoritySnapshot created =
                ProfileWriteAuthoritySnapshotFactory.NonWritable(
                    status,
                    saveSchemaVersion,
                    profileInitializationVersion,
                    true,
                    source,
                    new[] { diagnosticCode });
            _cachedObservedNonWritableAuthority = created;
            _cachedObservedNonWritableStatus = status;
            _cachedObservedNonWritableSaveSchemaVersion =
                saveSchemaVersion;
            _cachedObservedNonWritableProfileInitializationVersion =
                profileInitializationVersion;
            _cachedObservedNonWritableSource = source;
            return created;
        }

        private void ResetObservedNonWritableAuthorityCache()
        {
            _cachedObservedNonWritableAuthority = null;
            _cachedObservedNonWritableStatus = default;
            _cachedObservedNonWritableSource =
                ProfileAuthoritySourceGeneration.None;
            _cachedObservedNonWritableSaveSchemaVersion = 0;
            _cachedObservedNonWritableProfileInitializationVersion = 0;
        }

        private void ResetObservedAuthority()
        {
            ResetObservedNonWritableAuthorityCache();
            ResetPublishedWritableAuthority();
            _hasObservedAuthoritySource = false;
            _observedAuthoritySource =
                ProfileAuthoritySourceGeneration.None;
            _observedAuthoritySaveSchemaVersion = 0;
            _observedAuthorityProfileInitializationVersion = 0;
        }

        private void ObservePrimaryAuthority(SaveGameData save)
        {
            ResetObservedNonWritableAuthorityCache();
            if (save == null)
            {
                ResetObservedAuthority();
                return;
            }

            _hasObservedAuthoritySource = true;
            _observedAuthoritySource =
                ProfileAuthoritySourceGeneration.Primary;
            _observedAuthoritySaveSchemaVersion = save.SaveSchemaVersion;
            _observedAuthorityProfileInitializationVersion =
                save.ProfileInitializationVersion;
        }

        public void Save()
        {
            PersistCurrentSave(requireManualActivation: true);
        }

        internal void PersistLifecycleCheckpoint()
        {
            PersistCurrentSave(requireManualActivation: false);
        }

        private void PersistCurrentSave(bool requireManualActivation)
        {
            if (!_profileWritable &&
                LastSaveStatus == SaveOperationStatus.CommitUncertain)
            {
                Debug.LogError(
                    "AL-SAVE-COMMIT-UNCERTAIN-BLOCKED: Persistence remains frozen until the canonical save inventory is reloaded and reconciled.");
                return;
            }

            if (requireManualActivation &&
                !ProfileMutationContainment.CanInvokeManualSave(this))
            {
                const string containedMessage =
                    "AL-SAVE-MANUAL-WRITE-CONTAINED: Arbitrary profile persistence is disabled until the profile-bound migration train is explicitly approved and activated.";
                LastSaveDisposition = CreateSaveDisposition(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    false,
                    false,
                    false,
                    false,
                    false,
                    containedMessage);
                SetSaveStatus(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    containedMessage,
                    false);
                return;
            }

            if (!_profileWritable && LastLoadDisposition != null)
            {
                const string readOnlyMessage =
                    "AL-SAVE-READ-ONLY-DISPOSITION: The selected save generation is read-only; all on-disk evidence was preserved.";
                LastSaveDisposition = CreateSaveDisposition(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    false,
                    false,
                    false,
                    false,
                    false,
                    readOnlyMessage);
                SetSaveStatus(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    readOnlyMessage,
                    true);
                return;
            }

            if (_currentSave == null)
            {
                return;
            }

            byte[] requiredRecoveryWitnessBytes = _committedRecoveryWitnessBytes;
            if (requiredRecoveryWitnessBytes != null &&
                !TryVerifyCommittedRecoveryWitnessTargetTwice(
                    requiredRecoveryWitnessBytes,
                    out _,
                    out _))
            {
                const string witnessChangedMessage =
                    "AL-SAVE-RECOVERY-WITNESS-CHANGED: Exact primary, backup, staged witness, previous-generation, or committed quarantine identity changed before save; persistence was frozen and every generation was preserved.";
                _profileWritable = false;
                _readOnlyCandidate = CloneSave(_currentSave);
                _currentSave = null;
                LastSaveDisposition = CreateSaveDisposition(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    false,
                    false,
                    false,
                    false,
                    false,
                    witnessChangedMessage);
                SetSaveStatus(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    witnessChangedMessage,
                    true);
                return;
            }

            SaveOperationStatus status = PersistCandidate(
                CloneSave(_currentSave),
                requiredRecoveryWitnessBytes,
                null,
                out SaveGameData persistedSave,
                out SaveOperationDisposition disposition,
                out string message);
            LastSaveDisposition = disposition;

            if (status == SaveOperationStatus.SavedPrimary)
            {
                _currentSave = persistedSave;
                ObservePrimaryAuthority(persistedSave);
                _readOnlyCandidate = null;
                _profileWritable = true;
                _committedRecoveryWitnessBytes = null;
                _committedInvalidPrimaryWitnessBytes = null;
                _committedInvalidPrimaryQuarantinePath = null;
                _committedInvalidPrimaryRecoveryMarkerBytes = null;
                _committedInvalidPrimaryRecoveryMarkerPath = null;
                SetSaveStatus(SaveOperationStatus.SavedPrimary, message, false);
                return;
            }

            if (requiredRecoveryWitnessBytes != null)
            {
                _profileWritable = false;
                _readOnlyCandidate = CloneSave(persistedSave);
                _currentSave = null;
                SetSaveStatus(status, message, true);
                return;
            }

            if (status == SaveOperationStatus.CommitUncertain ||
                (status == SaveOperationStatus.SaveFailedPreviousPreserved &&
                 disposition != null &&
                 !disposition.CleanupVerified))
            {
                _profileWritable = false;
            }

            _currentSave = persistedSave;

            SetSaveStatus(status, message, true);
        }

        RealmSelectionResult
            ILegacyRealmSelectionCandidateStore.TryCommitLegacyRealmSelection(
                RealmSelectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.InvalidTransaction,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-TRANSACTION-INVALID");
            }

            if (!Enum.IsDefined(typeof(RealmId), request.RequestedRealmId))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.InvalidRealm,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-REQUEST-INVALID");
            }

            if (!TryEnterLegacyCandidateCoordinator(LegacyRealmOperationId))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.InvalidTransaction,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-TRANSACTION-BUSY");
            }

            try
            {
                if (_currentSave == null)
                {
                    return TryCommitFirstLegacyRealm(request);
                }

                if (!TryGetExactLegacyPrimaryProfile(out SaveGameData published))
                {
                    return LegacyRealmResult(
                        RealmSelectionStatus.ProfileUnavailable,
                        request.RequestedRealmId,
                        false,
                        false,
                        "AL-REALM-PROFILE-READ-ONLY");
                }

                RealmId existing = published.SelectedRealm;
                if (existing != RealmId.None &&
                    existing != request.RequestedRealmId)
                {
                    return LegacyRealmResult(
                        RealmSelectionStatus.RejectedDifferentRealm,
                        request.RequestedRealmId,
                        false,
                        true,
                        "AL-REALM-DIFFERENT-REALM-REJECTED");
                }

                SaveCandidateCommitResult commit =
                    TryCommitLegacyCandidateCore(candidate =>
                    {
                        if (candidate == null ||
                            candidate.SaveSchemaVersion !=
                                SaveAuthorityTechnicalLimits
                                    .LegacySaveSchemaVersion ||
                            candidate.ProfileInitializationVersion !=
                                SaveAuthorityTechnicalLimits
                                    .LegacyProfileInitializationVersion ||
                            !string.IsNullOrEmpty(candidate.ProfileId) ||
                            candidate.SelectedRealm != existing)
                        {
                            return SaveCandidateMutationPreparation.Rejected(
                                "AL-REALM-AUTHORITY-CONFLICT");
                        }

                        if (existing == request.RequestedRealmId)
                        {
                            return SaveCandidateMutationPreparation.Duplicate();
                        }

                        candidate.SelectedRealm = request.RequestedRealmId;
                        return SaveCandidateMutationPreparation.Prepared();
                    });

                switch (commit.Outcome)
                {
                    case SaveCandidateCommitOutcome.Committed:
                        return LegacyRealmResult(
                            RealmSelectionStatus.Committed,
                            request.RequestedRealmId,
                            true,
                            true,
                            "AL-REALM-COMMITTED");
                    case SaveCandidateCommitOutcome.Duplicate:
                        return LegacyRealmResult(
                            RealmSelectionStatus.AlreadyCommittedSameRealm,
                            request.RequestedRealmId,
                            false,
                            true,
                            "AL-REALM-ALREADY-COMMITTED");
                    case SaveCandidateCommitOutcome.CommitUncertain:
                    case SaveCandidateCommitOutcome.PreviousPreserved:
                        return LegacyRealmResult(
                            RealmSelectionStatus.SaveFailedPreviousPreserved,
                            request.RequestedRealmId,
                            false,
                            false,
                            "AL-REALM-SAVE-FAILED");
                    default:
                        return LegacyRealmResult(
                            RealmSelectionStatus.ProfileUnavailable,
                            request.RequestedRealmId,
                            false,
                            false,
                            "AL-REALM-PROFILE-READ-ONLY");
                }
            }
            finally
            {
                ExitLegacyCandidateCoordinator();
            }
        }

        SaveCandidateCommitResult
            ILegacyMvpLoopCandidateStore.TryCommitLegacyMvpLoop(
                MvpLoopCommitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return LegacyCandidateRejected("AL-MVP-LOOP-TRANSACTION-INVALID");
            }

            if (!TryEnterLegacyCandidateCoordinator(LegacyMvpLoopOperationId))
            {
                return LegacyCandidateRejected("AL-MVP-LOOP-TRANSACTION-BUSY");
            }

            try
            {
                if (!TryGetExactLegacyPrimaryProfile(out SaveGameData published))
                {
                    return LegacyCandidateRejected("AL-MVP-LOOP-PROFILE-READ-ONLY");
                }

                RealmId expectedRealm = published.SelectedRealm;
                return TryCommitLegacyCandidateCore(candidate =>
                {
                    if (candidate == null ||
                        candidate.SelectedRealm != expectedRealm ||
                        candidate.SaveSchemaVersion !=
                            SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion ||
                        candidate.ProfileInitializationVersion !=
                            SaveAuthorityTechnicalLimits.LegacyProfileInitializationVersion ||
                        !string.IsNullOrEmpty(candidate.ProfileId))
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-MVP-LOOP-AUTHORITY-CONFLICT");
                    }

                    MvpLoopPrepareDisposition disposition =
                        MvpLoopSaveCodec.PrepareCandidate(
                            candidate,
                            request,
                            out string prepareMessage);
                    if (candidate.SelectedRealm != expectedRealm)
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-MVP-LOOP-AUTHORITY-CONFLICT");
                    }

                    switch (disposition)
                    {
                        case MvpLoopPrepareDisposition.Duplicate:
                            return SaveCandidateMutationPreparation.Duplicate();
                        case MvpLoopPrepareDisposition.Prepared:
                            return SaveCandidateMutationPreparation.Prepared();
                        default:
                            return SaveCandidateMutationPreparation.Rejected(
                                string.IsNullOrWhiteSpace(prepareMessage)
                                    ? "AL-MVP-LOOP-REQUEST-INVALID"
                                    : prepareMessage);
                    }
                });
            }
            finally
            {
                ExitLegacyCandidateCoordinator();
            }
        }

        SaveCandidateCommitResult
            INvs01LegacyCandidateStore.TryCommitNvs01LegacyCandidate(
                Nvs01MutationPlan plan,
                Nvs01VerifiedCatalog verifiedCatalog)
        {
            if (plan?.Expected == null ||
                plan.Candidate == null ||
                verifiedCatalog == null)
            {
                return LegacyCandidateRejected(
                    "AL-NVS01-SAVE-PROGRESS-UNAVAILABLE");
            }

            if (!TryEnterLegacyCandidateCoordinator(LegacyNvs01OperationId))
            {
                return LegacyCandidateRejected(
                    "AL-NVS01-SAVE-TRANSACTION-BUSY");
            }

            try
            {
                if (!TryGetExactLegacyNvsProfile(
                        out SaveGameData published))
                {
                    return LegacyCandidateRejected(
                        "AL-NVS01-SAVE-AUTHORITY-CONFLICT");
                }

                if (published.SelectedRealm == RealmId.None ||
                    !TryMapRealmIdToCanonical(
                        published.SelectedRealm,
                        out string committedRealmId) ||
                    !string.Equals(
                        plan.Candidate.CommittedRealmId,
                        committedRealmId,
                        StringComparison.Ordinal) ||
                    !string.IsNullOrEmpty(plan.Expected.CommittedRealmId) &&
                    !string.Equals(
                        plan.Expected.CommittedRealmId,
                        committedRealmId,
                        StringComparison.Ordinal))
                {
                    return LegacyCandidateRejected(
                        "AL-NVS01-SAVE-AUTHORITY-CONFLICT");
                }

                if (!TryVerifyExactPublishedPrimaryTwice(
                        published,
                        out _))
                {
                    return LegacyCandidateRejected(
                        "AL-NVS01-SAVE-GENERATION-CONFLICT");
                }

                RealmId expectedRealm = published.SelectedRealm;
                return TryCommitLegacyCandidateCore(candidate =>
                {
                    if (candidate == null ||
                        candidate.SelectedRealm != expectedRealm ||
                        candidate.SaveSchemaVersion !=
                            SaveAuthorityTechnicalLimits
                                .LegacySaveSchemaVersion ||
                        candidate.ProfileInitializationVersion !=
                            SaveAuthorityTechnicalLimits
                                .LegacyProfileInitializationVersion ||
                        !string.IsNullOrEmpty(candidate.ProfileId))
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-NVS01-SAVE-AUTHORITY-CONFLICT");
                    }

                    SaveCandidateMutationPreparation preparation =
                        Nvs01SaveGameMutationCommitter.PrepareLegacyCandidate(
                            candidate,
                            plan,
                            verifiedCatalog);
                    if (candidate.SelectedRealm != expectedRealm)
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-NVS01-SAVE-AUTHORITY-CONFLICT");
                    }

                    return preparation;
                });
            }
            finally
            {
                ExitLegacyCandidateCoordinator();
            }
        }

        SaveCandidateCommitResult
            ILegacyKingdomTeachingCandidateStore
                .TryCommitLegacyKingdomTeaching(
                    KingdomTeachingCommitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TransactionId))
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

            if (!TryEnterLegacyCandidateCoordinator(
                    LegacyKingdomTeachingOperationId))
            {
                return LegacyCandidateRejected(
                    "AL-KINGDOM-TEACHING-TRANSACTION-BUSY");
            }

            try
            {
                if (!TryGetExactLegacyPrimaryProfile(out SaveGameData published) ||
                    published.SelectedRealm != request.ExpectedRealm ||
                    !ProofOfWorthLordship.IsGranted(published))
                {
                    return LegacyCandidateRejected(
                        "AL-KINGDOM-TEACHING-PROFILE-READ-ONLY");
                }

                RealmId expectedRealm = published.SelectedRealm;
                return TryCommitLegacyCandidateCore(candidate =>
                {
                    if (candidate == null ||
                        candidate.SelectedRealm != expectedRealm ||
                        candidate.SaveSchemaVersion !=
                            SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion ||
                        candidate.ProfileInitializationVersion !=
                            SaveAuthorityTechnicalLimits
                                .LegacyProfileInitializationVersion ||
                        !string.IsNullOrEmpty(candidate.ProfileId) ||
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
                            out string prepareMessage);
                    switch (disposition)
                    {
                        case KingdomTeachingPrepareDisposition.Duplicate:
                            return SaveCandidateMutationPreparation.Duplicate();
                        case KingdomTeachingPrepareDisposition.Prepared:
                            return SaveCandidateMutationPreparation.Prepared();
                        default:
                            return SaveCandidateMutationPreparation.Rejected(
                                string.IsNullOrWhiteSpace(prepareMessage)
                                    ? "AL-KINGDOM-TEACHING-REQUEST-INVALID"
                                    : prepareMessage);
                    }
                });
            }
            finally
            {
                ExitLegacyCandidateCoordinator();
            }
        }

        SaveCandidateCommitResult
            ILegacyFirstWorldProgressCandidateStore
                .TryCommitLegacyFirstWorldProgress(
                    FirstWorldProgressCommitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return LegacyCandidateRejected(
                    "AL-FIRST-WORLD-TRANSACTION-INVALID");
            }

            if (!TryEnterLegacyCandidateCoordinator(
                    LegacyFirstWorldProgressOperationId))
            {
                return LegacyCandidateRejected(
                    "AL-FIRST-WORLD-TRANSACTION-BUSY");
            }

            try
            {
                if (!TryGetExactLegacyPrimaryProfile(
                        out SaveGameData published) ||
                    published.SelectedRealm == RealmId.None ||
                    request.Expected == null ||
                    published.SelectedRealm != request.Expected.Realm)
                {
                    return LegacyCandidateRejected(
                        "AL-FIRST-WORLD-PROFILE-READ-ONLY");
                }

                RealmId expectedRealm = published.SelectedRealm;
                return TryCommitLegacyCandidateCore(candidate =>
                {
                    if (candidate == null ||
                        candidate.SelectedRealm != expectedRealm ||
                        candidate.SaveSchemaVersion !=
                            SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion ||
                        candidate.ProfileInitializationVersion !=
                            SaveAuthorityTechnicalLimits
                                .LegacyProfileInitializationVersion ||
                        !string.IsNullOrEmpty(candidate.ProfileId))
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-FIRST-WORLD-AUTHORITY-CONFLICT");
                    }

                    FirstWorldProgressPrepareDisposition disposition =
                        FirstWorldProgressSaveCodec.PrepareCandidate(
                            candidate,
                            request,
                            out _,
                            out string prepareMessage);
                    if (candidate.SelectedRealm != expectedRealm)
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-FIRST-WORLD-AUTHORITY-CONFLICT");
                    }

                    switch (disposition)
                    {
                        case FirstWorldProgressPrepareDisposition.Duplicate:
                            return SaveCandidateMutationPreparation.Duplicate();
                        case FirstWorldProgressPrepareDisposition.Prepared:
                            return SaveCandidateMutationPreparation.Prepared();
                        default:
                            return SaveCandidateMutationPreparation.Rejected(
                                string.IsNullOrWhiteSpace(prepareMessage)
                                    ? "AL-FIRST-WORLD-REQUEST-INVALID"
                                    : prepareMessage);
                    }
                });
            }
            finally
            {
                ExitLegacyCandidateCoordinator();
            }
        }

        SaveCandidateCommitResult ISaveGameCandidateStore.TryCommitCandidate(
            Func<SaveGameData, SaveCandidateMutationPreparation> prepareCandidate)
        {
            return new SaveCandidateCommitResult(
                SaveCandidateCommitOutcome.ReadOnly,
                _currentSave,
                "AL-SAVE-GENERIC-CANDIDATE-CONTAINED: Schema-v1 persistence accepts only approved typed adapters.");
        }

        private SaveCandidateCommitResult TryCommitLegacyCandidateCore(
            Func<SaveGameData, SaveCandidateMutationPreparation> prepareCandidate)
        {
            if (prepareCandidate == null)
            {
                throw new ArgumentNullException(nameof(prepareCandidate));
            }

            if (!_profileWritable && LastSaveStatus == SaveOperationStatus.CommitUncertain)
            {
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.CommitUncertain,
                    _currentSave,
                    "AL-SAVE-COMMIT-UNCERTAIN-BLOCKED: Persistence remains frozen until the canonical save inventory is reloaded and reconciled.");
            }

            if (!_profileWritable || _currentSave == null)
            {
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.ReadOnly,
                    _currentSave,
                    "AL-SAVE-READ-ONLY-DISPOSITION: The selected save generation is unavailable or read-only.");
            }

            SaveGameData publishedBefore = _currentSave;
            byte[] requiredRecoveryWitnessBytes = _committedRecoveryWitnessBytes;
            if (requiredRecoveryWitnessBytes != null &&
                !TryVerifyCommittedRecoveryWitnessTargetTwice(
                    requiredRecoveryWitnessBytes,
                    out _,
                    out _))
            {
                return RejectChangedCommittedRecoveryWitness(publishedBefore);
            }

            if (!TryCaptureLegacyCandidateAuthority(
                    publishedBefore,
                    out SaveAuthorityBaseline requiredLegacyBaseline,
                    out string authorityConflictMessage))
            {
                return RejectTypedLegacyAuthorityConflict(
                    publishedBefore,
                    authorityConflictMessage);
            }

            SaveGameData candidate = CloneSave(publishedBefore);
            SaveCandidateMutationPreparation preparation;
            try
            {
                preparation = prepareCandidate(candidate);
            }
            catch (Exception exception)
            {
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Rejected,
                    publishedBefore,
                    "AL-SAVE-CANDIDATE-PREPARATION-FAILED: " +
                    exception.GetType().Name);
            }

            if (preparation == null)
            {
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Rejected,
                    publishedBefore,
                    "AL-SAVE-CANDIDATE-PREPARATION-MISSING: The candidate callback returned no disposition.");
            }

            if (preparation.Disposition == SaveCandidateMutationDisposition.Duplicate)
            {
                if (!TryVerifyPublishedCandidateAuthorityTwice(
                        publishedBefore,
                        requiredLegacyBaseline,
                        requiredRecoveryWitnessBytes,
                        out string duplicateVerificationMessage))
                {
                    _profileWritable = false;
                    _readOnlyCandidate = CloneSave(publishedBefore);
                    LastSaveDisposition = CreateSaveDisposition(
                        SaveOperationStatus.CommitUncertain,
                        false,
                        false,
                        false,
                        false,
                        false,
                        duplicateVerificationMessage);
                    SetSaveStatus(
                        SaveOperationStatus.CommitUncertain,
                        duplicateVerificationMessage,
                        true);
                    return new SaveCandidateCommitResult(
                        SaveCandidateCommitOutcome.CommitUncertain,
                        publishedBefore,
                        duplicateVerificationMessage);
                }

                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Duplicate,
                    publishedBefore,
                    string.IsNullOrWhiteSpace(preparation.Message)
                        ? duplicateVerificationMessage
                        : preparation.Message);
            }

            if (preparation.Disposition == SaveCandidateMutationDisposition.Rejected)
            {
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Rejected,
                    publishedBefore,
                    preparation.Message);
            }

            if (preparation.Disposition != SaveCandidateMutationDisposition.Prepared)
            {
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Rejected,
                    publishedBefore,
                    "AL-SAVE-CANDIDATE-DISPOSITION-INVALID: The candidate callback returned an unsupported disposition.");
            }

            ApplyNeutralPersistenceDefaults(candidate);
            if (!ValidateSaveSemantics(candidate, out string candidateError))
            {
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Rejected,
                    publishedBefore,
                    "AL-SAVE-CANDIDATE-INVALID: " + candidateError);
            }

            SaveOperationStatus status = PersistCandidate(
                candidate,
                requiredRecoveryWitnessBytes,
                requiredLegacyBaseline,
                out SaveGameData persistedSave,
                out SaveOperationDisposition disposition,
                out string message);
            LastSaveDisposition = disposition;

            if (status == SaveOperationStatus.SavedPrimary)
            {
                _currentSave = persistedSave;
                ObservePrimaryAuthority(persistedSave);
                _readOnlyCandidate = null;
                _profileWritable = true;
                _committedRecoveryWitnessBytes = null;
                _committedInvalidPrimaryWitnessBytes = null;
                _committedInvalidPrimaryQuarantinePath = null;
                _committedInvalidPrimaryRecoveryMarkerBytes = null;
                _committedInvalidPrimaryRecoveryMarkerPath = null;
                SetSaveStatus(SaveOperationStatus.SavedPrimary, message, false);
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Committed,
                    _currentSave,
                    message);
            }

            if (requiredRecoveryWitnessBytes != null ||
                status == SaveOperationStatus.CommitUncertain ||
                (status == SaveOperationStatus.SaveFailedPreviousPreserved &&
                 disposition != null &&
                 !disposition.CleanupVerified))
            {
                _profileWritable = false;
                _readOnlyCandidate = CloneSave(publishedBefore);
            }

            _currentSave = publishedBefore;
            SetSaveStatus(status, message, true);
            return new SaveCandidateCommitResult(
                status == SaveOperationStatus.CommitUncertain
                    ? SaveCandidateCommitOutcome.CommitUncertain
                    : SaveCandidateCommitOutcome.PreviousPreserved,
                publishedBefore,
                message);
        }

        private RealmSelectionResult TryCommitFirstLegacyRealm(
            RealmSelectionRequest request)
        {
            if (!AllCanonicalPathsMissing(includeTemp: true))
            {
                return LegacyRealmResult(
                    RealmSelectionStatus.ProfileUnavailable,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-PROFILE-EVIDENCE-REQUIRES-LOAD");
            }

            SaveGameData candidate = CreateDefaultSave(request.RequestedRealmId);
            SaveOperationDisposition firstGenerationDisposition = null;
            bool coreSucceeded = TryCreateFirstGenerationCandidate(
                candidate,
                out SaveGameData persisted,
                out string message,
                out bool diskChanged);
            SaveOperationStatus reconciledStatus =
                ReconcileFirstGenerationAttempt(
                    candidate,
                    coreSucceeded,
                    diskChanged,
                    ref persisted,
                    out firstGenerationDisposition,
                    ref message);
            if (reconciledStatus != SaveOperationStatus.SavedPrimary ||
                persisted == null)
            {
                _profileWritable = false;
                _readOnlyCandidate = CloneSave(candidate);
                LastSaveDisposition = firstGenerationDisposition;
                SetSaveStatus(reconciledStatus, message, true);
                return LegacyRealmResult(
                    RealmSelectionStatus.SaveFailedPreviousPreserved,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-SAVE-FAILED");
            }

            _profileDeleted = false;
            _currentSave = persisted;
            _readOnlyCandidate = null;
            _profileWritable = true;
            _committedRecoveryWitnessBytes = null;
            _committedInvalidPrimaryWitnessBytes = null;
            _committedInvalidPrimaryQuarantinePath = null;
            _committedInvalidPrimaryRecoveryMarkerBytes = null;
            _committedInvalidPrimaryRecoveryMarkerPath = null;
            ObservePrimaryAuthority(persisted);
            LastLoadStatus = SaveLoadStatus.None;
            LastLoadMessage = string.Empty;
            LastLoadDisposition = null;
            LastSaveDisposition = firstGenerationDisposition;
            SetSaveStatus(SaveOperationStatus.SavedPrimary, message, false);
            return LegacyRealmResult(
                RealmSelectionStatus.Committed,
                request.RequestedRealmId,
                true,
                true,
                "AL-REALM-COMMITTED");
        }

        private bool TryGetExactLegacyPrimaryProfile(
            out SaveGameData published)
        {
            published = _currentSave;
            if (!HasExactLegacyProfileMetadata(published))
            {
                return false;
            }

            ProfileWriteAuthoritySnapshot authority = GetCurrentAuthority();
            return authority != null &&
                authority.Status ==
                    ProfileWriteAuthorityStatus.MigrationRequired &&
                authority.HasSelectedSourceGeneration &&
                authority.SelectedSourceGeneration ==
                    ProfileAuthoritySourceGeneration.Primary &&
                authority.SaveSchemaVersion ==
                    SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
                authority.ProfileInitializationVersion ==
                    SaveAuthorityTechnicalLimits
                        .LegacyProfileInitializationVersion;
        }

        private bool TryGetExactLegacyNvsProfile(
            out SaveGameData published)
        {
            if (TryGetExactLegacyPrimaryProfile(out published))
            {
                return true;
            }

            published = _currentSave;
            if (!HasExactLegacyProfileMetadata(published) ||
                LastLoadStatus != SaveLoadStatus.RecoveredFromBackup ||
                _committedRecoveryWitnessBytes == null)
            {
                return false;
            }

            ProfileWriteAuthoritySnapshot authority = GetCurrentAuthority();
            if (authority == null ||
                authority.Status !=
                    ProfileWriteAuthorityStatus.MigrationRequired ||
                !authority.HasSelectedSourceGeneration ||
                authority.SelectedSourceGeneration !=
                    ProfileAuthoritySourceGeneration.Backup ||
                authority.SaveSchemaVersion !=
                    SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion ||
                authority.ProfileInitializationVersion !=
                    SaveAuthorityTechnicalLimits
                        .LegacyProfileInitializationVersion)
            {
                return false;
            }

            return true;
        }

        private bool HasExactLegacyProfileMetadata(SaveGameData published) =>
            _profileWritable &&
            published != null &&
            published.SaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
            published.ProfileInitializationVersion ==
                SaveAuthorityTechnicalLimits
                    .LegacyProfileInitializationVersion &&
            string.IsNullOrEmpty(published.ProfileId);

        private bool TryEnterLegacyCandidateCoordinator(string operationId)
        {
            if (!string.Equals(
                    operationId,
                    LegacyRealmOperationId,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    operationId,
                    LegacyNvs01OperationId,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    operationId,
                    LegacyMvpLoopOperationId,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    operationId,
                    LegacyKingdomTeachingOperationId,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    operationId,
                    LegacyFirstWorldProgressOperationId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return Interlocked.CompareExchange(
                ref _legacyCandidateCommitActive,
                1,
                0) == 0;
        }

        private void ExitLegacyCandidateCoordinator()
        {
            Volatile.Write(ref _legacyCandidateCommitActive, 0);
        }

        private SaveCandidateCommitResult LegacyCandidateRejected(
            string message) =>
            new SaveCandidateCommitResult(
                SaveCandidateCommitOutcome.ReadOnly,
                _currentSave,
                message);

        private SaveCandidateCommitResult
            RejectChangedCommittedRecoveryWitness(
                SaveGameData publishedBefore)
        {
            const string witnessChangedMessage =
                "AL-SAVE-RECOVERY-WITNESS-CHANGED: Exact recovery evidence changed before the candidate commit; persistence was frozen.";
            _profileWritable = false;
            _readOnlyCandidate = CloneSave(publishedBefore);
            LastSaveDisposition = CreateSaveDisposition(
                SaveOperationStatus.SaveFailedPreviousPreserved,
                false,
                false,
                false,
                false,
                false,
                witnessChangedMessage);
            SetSaveStatus(
                SaveOperationStatus.SaveFailedPreviousPreserved,
                witnessChangedMessage,
                true);
            return new SaveCandidateCommitResult(
                SaveCandidateCommitOutcome.PreviousPreserved,
                publishedBefore,
                witnessChangedMessage);
        }

        private SaveCandidateCommitResult RejectTypedLegacyAuthorityConflict(
            SaveGameData publishedBefore,
            string message)
        {
            string conflictMessage = string.IsNullOrWhiteSpace(message)
                ? "AL-SAVE-TYPED-AUTHORITY-CONFLICT: Exact primary and backup authority could not be pinned before candidate preparation."
                : message;
            _profileWritable = false;
            _readOnlyCandidate = CloneSave(publishedBefore);
            LastSaveDisposition = CreateSaveDisposition(
                SaveOperationStatus.SaveFailedPreviousPreserved,
                false,
                false,
                false,
                false,
                false,
                conflictMessage);
            SetSaveStatus(
                SaveOperationStatus.SaveFailedPreviousPreserved,
                conflictMessage,
                true);
            return new SaveCandidateCommitResult(
                SaveCandidateCommitOutcome.PreviousPreserved,
                publishedBefore,
                conflictMessage);
        }

        private bool TryCaptureLegacyCandidateAuthority(
            SaveGameData publishedSave,
            out SaveAuthorityBaseline baseline,
            out string message)
        {
            baseline = null;
            message =
                "AL-SAVE-TYPED-PRIMARY-GENERATION-CONFLICT: The published legacy save could not be pinned to an exact primary generation.";
            if (publishedSave == null ||
                !TrySerializeBounded(
                    publishedSave,
                    out string publishedJson,
                    out _))
            {
                return false;
            }

            byte[] publishedBytes = StrictUtf8.GetBytes(publishedJson);
            SaveFileReadResult primary = ReadCanonicalPath(SavePath);
            if (!IsExactValidGeneration(
                    primary,
                    publishedBytes,
                    out _))
            {
                return false;
            }

            SaveFileReadResult backup = ReadCanonicalPath(BackupPath);
            if (backup.Disposition != SaveFileReadDisposition.Read ||
                backup.Bytes == null)
            {
                message =
                    "AL-SAVE-TYPED-BACKUP-GENERATION-CONFLICT: A valid exact backup generation is required before typed legacy candidate preparation.";
                return false;
            }

            if (publishedSave.SaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion)
            {
                SaveSemanticCandidate backupSemantic =
                    ValidateSemanticCandidate(
                        backup.Bytes,
                        SaveCandidateSourceGeneration.Backup);
                if (backupSemantic == null ||
                    backupSemantic.Outcome == SaveSemanticCandidateOutcome.Invalid ||
                    backupSemantic.Outcome ==
                        SaveSemanticCandidateOutcome.DegradedMalformed ||
                    backupSemantic.Outcome ==
                        SaveSemanticCandidateOutcome.ForwardSchemaReadOnly ||
                    backupSemantic.Outcome ==
                        SaveSemanticCandidateOutcome.OversizePreservedReadOnly)
                {
                    message =
                        "AL-SAVE-TYPED-BACKUP-GENERATION-CONFLICT: A valid exact backup generation is required before typed legacy candidate preparation.";
                    return false;
                }

                baseline = new SaveAuthorityBaseline(primary, backup);
                message = string.Empty;
                return true;
            }

            if (!TryDeserializeValidSaveBytes(
                    backup.Bytes,
                    out _,
                    out _,
                    SaveCandidateSourceGeneration.Backup))
            {
                message =
                    "AL-SAVE-TYPED-BACKUP-GENERATION-CONFLICT: A valid exact backup generation is required before typed legacy candidate preparation.";
                return false;
            }

            baseline = new SaveAuthorityBaseline(primary, backup);
            message = string.Empty;
            return true;
        }

        private static bool TryMatchPinnedLegacyAuthority(
            SaveAuthorityBaseline actual,
            SaveAuthorityBaseline expected,
            out string message)
        {
            if (actual == null || expected == null ||
                !MatchesExactState(actual.Primary, expected.Primary))
            {
                message =
                    "AL-SAVE-TYPED-PRIMARY-GENERATION-CONFLICT: Primary authority changed after typed candidate preflight; no filesystem mutation was attempted.";
                return false;
            }

            if (!MatchesExactState(actual.Backup, expected.Backup))
            {
                message =
                    "AL-SAVE-TYPED-BACKUP-GENERATION-CONFLICT: Backup authority changed after typed candidate preflight; no filesystem mutation was attempted.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private RealmSelectionResult LegacyRealmResult(
            RealmSelectionStatus status,
            RealmId requested,
            bool mutationOccurred,
            bool persisted,
            string technicalCode) =>
            new RealmSelectionResult(
                status,
                requested,
                _currentSave?.SelectedRealm ?? RealmId.None,
                mutationOccurred,
                persisted,
                technicalCode);

        private static bool TryMapRealmIdToCanonical(
            RealmId realmId,
            out string canonical)
        {
            switch (realmId)
            {
                case RealmId.Crownlands:
                    canonical = "crownlands";
                    return true;
                case RealmId.Stonehold:
                    canonical = "stonehold";
                    return true;
                case RealmId.Eldergrove:
                    canonical = "eldergrove";
                    return true;
                case RealmId.Umbral:
                    canonical = "umbral";
                    return true;
                default:
                    canonical = string.Empty;
                    return false;
            }
        }

        private bool TryVerifyPublishedCandidateAuthorityTwice(
            SaveGameData publishedSave,
            SaveAuthorityBaseline requiredLegacyBaseline,
            byte[] requiredRecoveryWitnessBytes,
            out string message)
        {
            message =
                "AL-SAVE-DUPLICATE-UNVERIFIED: The published save no longer has an exact, stable canonical authority; persistence was frozen.";
            if (publishedSave == null ||
                requiredLegacyBaseline == null ||
                !TrySerializeBounded(
                    publishedSave,
                    out string publishedJson,
                    out _))
            {
                return false;
            }

            byte[] publishedBytes = StrictUtf8.GetBytes(publishedJson);
            SaveCanonicalLedger first = CaptureCanonicalLedger();
            if (!TryVerifyPublishedCandidateAuthority(
                    first,
                    publishedBytes,
                    requiredLegacyBaseline,
                    requiredRecoveryWitnessBytes,
                    out message))
            {
                return false;
            }

            SaveCanonicalLedger second = CaptureCanonicalLedger();
            if (!TryVerifyPublishedCandidateAuthority(
                    second,
                    publishedBytes,
                    requiredLegacyBaseline,
                    requiredRecoveryWitnessBytes,
                    out message))
            {
                return false;
            }

            message = requiredRecoveryWitnessBytes == null
                ? "AL-SAVE-DUPLICATE-VERIFIED: The published save and pinned primary/backup authority matched exactly across two bounded inventories."
                : "AL-SAVE-DUPLICATE-RECOVERY-VERIFIED: The published save and complete committed recovery witness matched exactly across two bounded inventories.";
            return true;
        }

        private bool TryVerifyPublishedCandidateAuthority(
            SaveCanonicalLedger ledger,
            byte[] publishedBytes,
            SaveAuthorityBaseline requiredLegacyBaseline,
            byte[] requiredRecoveryWitnessBytes,
            out string message)
        {
            message =
                "AL-SAVE-DUPLICATE-UNVERIFIED: The canonical authority inventory was unavailable during duplicate verification.";
            if (ledger == null)
            {
                return false;
            }

            if (!TryMatchPinnedLegacyAuthority(
                    new SaveAuthorityBaseline(
                        ledger.Primary,
                        ledger.Backup),
                    requiredLegacyBaseline,
                    out message))
            {
                return false;
            }

            if (!IsExactValidGeneration(
                    ledger.Primary,
                    publishedBytes,
                    out _))
            {
                message =
                    "AL-SAVE-TYPED-PRIMARY-GENERATION-CONFLICT: The published save no longer matches the exact primary authority captured before duplicate preparation.";
                return false;
            }

            if (requiredRecoveryWitnessBytes != null)
            {
                if (!IsCommittedRecoveryWitnessTarget(
                        ledger,
                        requiredRecoveryWitnessBytes,
                        out _))
                {
                    message =
                        "AL-SAVE-RECOVERY-WITNESS-CHANGED: Exact primary, backup, staged witness, previous-generation, or committed quarantine identity changed during duplicate verification; persistence was frozen.";
                    return false;
                }

                message = string.Empty;
                return true;
            }

            if (ledger.Temp.Disposition != SaveFileReadDisposition.Missing ||
                ledger.Previous.Disposition != SaveFileReadDisposition.Missing ||
                !IsCommittedRecoveryMarkerCleanupVerified() ||
                !IsCommittedInvalidPrimaryQuarantineIntact())
            {
                message =
                    "AL-SAVE-DUPLICATE-UNVERIFIED: Temp, previous-generation, recovery-marker, or quarantine authority was not the exact normal duplicate target.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryVerifyExactPublishedPrimaryTwice(
            SaveGameData publishedSave,
            out string message)
        {
            message =
                "AL-NVS01-SAVE-GENERATION-CONFLICT: The published legacy save no longer matches a stable exact primary generation.";
            if (publishedSave == null ||
                !TrySerializeBounded(
                    publishedSave,
                    out string publishedJson,
                    out _))
            {
                return false;
            }

            byte[] publishedBytes = StrictUtf8.GetBytes(publishedJson);
            SaveFileReadResult first = ReadCanonicalPath(SavePath);
            if (!IsExactValidGeneration(first, publishedBytes, out _))
            {
                return false;
            }

            SaveFileReadResult second = ReadCanonicalPath(SavePath);
            return IsExactValidGeneration(second, publishedBytes, out _);
        }

        public void Load()
        {
            SaveGameData priorSave = _currentSave;
            _profileDeleted = false;
            ResetObservedAuthority();
            _readOnlyCandidate = null;
            _profileWritable = false;
            _committedRecoveryWitnessBytes = null;
            _committedInvalidPrimaryWitnessBytes = null;
            _committedInvalidPrimaryQuarantinePath = null;
            _committedInvalidPrimaryRecoveryMarkerBytes = null;
            _committedInvalidPrimaryRecoveryMarkerPath = null;
            LastSaveStatus = SaveOperationStatus.None;
            LastSaveMessage = string.Empty;
            LastSaveDisposition = null;

            IReadOnlyList<SaveCandidateInventoryEntry> inventory = BuildCandidateInventory();
            SaveCandidateInventoryEntry primary = Find(inventory, SaveCandidateSourceGeneration.Primary);
            SaveCandidateInventoryEntry backup = Find(inventory, SaveCandidateSourceGeneration.Backup);
            SaveCandidateInventoryEntry previous = Find(inventory, SaveCandidateSourceGeneration.Previous);

            if (HasProfileIdentityWitnessEvidence() &&
                TryResumeWitnessedSchemaTwoLedger(
                    inventory,
                    primary,
                    backup,
                    out SaveGameData resumedSave,
                    out string resumeMessage))
            {
                _currentSave = resumedSave;
                _readOnlyCandidate = null;
                _profileWritable = true;
                ObservePrimaryAuthority(resumedSave);
                PublishDisposition(
                    inventory,
                    primary.SemanticCandidate,
                    "SAVE_SELECT_SCHEMA_TWO_WITNESSED_RESUME",
                    true,
                    true,
                    false);
                ActivatePublishedWritableAuthority(
                    ProfileAuthoritySourceGeneration.Primary);
                SetLoadStatus(
                    SaveLoadStatus.MigratedSchemaOne,
                    resumeMessage,
                    false);
                return;
            }

            if (HasConflictingSchemaOneAndTwoGenerations(inventory) &&
                !HasMatchingProfileIdentityWitness(primary, backup))
            {
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    null,
                    "SAVE_SELECT_SCHEMA_CORRELATION_CONFLICT",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-SCHEMA-CORRELATION-CONFLICT: Mixed schema-1 and schema-2 generations were preserved without a matching identity witness; no profile was replaced.",
                    false);
                return;
            }

            if (!TryVerifyNvs01MigrationBackupArchivesTwice(
                    out string migrationArchiveDiagnostic))
            {
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    null,
                    migrationArchiveDiagnostic,
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    migrationArchiveDiagnostic +
                    ": Migration backup archive evidence was malformed, ambiguous, unreadable, unbounded, reparse-backed, or hash-inconsistent; every byte was preserved read-only.",
                    false);
                return;
            }

            if (primary.ReadResult.Disposition == SaveFileReadDisposition.IoFailure ||
                primary.ReadResult.Disposition == SaveFileReadDisposition.ChangedDuringRead)
            {
                _readOnlyCandidate = CloneSave(priorSave);
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    null,
                    "SAVE_SELECT_PRIMARY_UNREADABLE",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryFailed,
                    "AL-SAVE-PRIMARY-UNREADABLE: The primary generation could not be read consistently; all generations were preserved.",
                    true);
                return;
            }

            if (primary.ReadResult.Disposition == SaveFileReadDisposition.Oversize)
            {
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    null,
                    "SAVE_SELECT_OVERSIZE_PRIMARY_RECOVERY_REQUIRED",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-OVERSIZE-PRIMARY: The primary generation exceeds the bounded read limit and was preserved for explicit recovery.",
                    false);
                return;
            }

            if (inventory.All(entry =>
                    entry.ReadResult.Disposition == SaveFileReadDisposition.Missing))
            {
                bool quarantineInventoryReadable =
                    TryEnumerateStageFiveQuarantineMarkers(
                        out IReadOnlyList<StageFiveQuarantineMarker>
                            orphanedStageFiveMarkers,
                        out bool quarantineEvidenceConflict);
                if (ReadCanonicalPath(StageFiveRecoveryMarkerPath).Disposition !=
                        SaveFileReadDisposition.Missing ||
                    HasSaveEvidence(LegacyPreviousPath) ||
                    HasNvs01MigrationBackupArchiveEvidence() ||
                    !quarantineInventoryReadable ||
                    quarantineEvidenceConflict ||
                    orphanedStageFiveMarkers.Count != 0 ||
                    HasStageFiveTransactionArchiveEvidence())
                {
                    _currentSave = null;
                    PublishDisposition(
                        inventory,
                        null,
                        "SAVE_SELECT_ORPHANED_STAGE5_MARKER",
                        false,
                        false,
                        false);
                    SetLoadStatus(
                        SaveLoadStatus.RecoveryRequired,
                        "AL-SAVE-ORPHANED-RECOVERY-EVIDENCE: A legacy previous file, migration archive, recovery marker, quarantine, or transaction archive survived without canonical generations; it was preserved for explicit recovery.",
                        false);
                    return;
                }

                CreateNewProfileAfterAllMissing(inventory);
                return;
            }

            SaveSemanticCandidateSelection selection = SaveSemanticCandidateSelector.Select(
                primary.SemanticCandidate,
                backup.SemanticCandidate,
                previous.SemanticCandidate);

            if (!selection.HasSelection)
            {
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    null,
                    selection.ReasonCode,
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-RECOVERY-REQUIRED: Existing generations were preserved because none can be activated without an explicit recovery decision.",
                    false);
                return;
            }

            SaveSemanticCandidate selected = selection.SelectedCandidate;
            if (selected.Outcome == SaveSemanticCandidateOutcome.ForwardSchemaReadOnly)
            {
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    selected,
                    selection.ReasonCode,
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.LoadedForwardSchemaReadOnly,
                    "AL-SAVE-FORWARD-SCHEMA-READ-ONLY: A newer-schema generation remains authoritative and was preserved without downgrade.",
                    false);
                return;
            }

            if (selected.Outcome ==
                SaveSemanticCandidateOutcome.RepairableWithDataChange)
            {
                ActivateExactNvs01V003Migration(inventory, selected);
                return;
            }

            if (selected.Outcome == SaveSemanticCandidateOutcome.MigrationRequired)
            {
                TryInstallSchemaTwoProfileIdentity(inventory, selected);
                return;
            }

            if (!TryDeserializeSelectedCandidate(selected, out SaveGameData selectedSave))
            {
                _readOnlyCandidate = CloneSave(priorSave);
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    selected,
                    selection.ReasonCode,
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryFailed,
                    "AL-SAVE-SELECTED-DESERIALIZE-FAILED: The selected bounded candidate could not be materialized safely; disk evidence was preserved.",
                    true);
                return;
            }

            if (!ValidateSaveSemantics(selectedSave, out _))
            {
                _currentSave = null;
                _readOnlyCandidate = selectedSave;
                _profileWritable = false;
                PublishDisposition(
                    inventory,
                    selected,
                    selection.ReasonCode,
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.LoadedPrimaryDegraded,
                    "AL-SAVE-SELECTED-SEMANTIC-FAILED: The selected candidate contains runtime-inconsistent state and remains available only as a read-only diagnostic snapshot.",
                    false);
                return;
            }

            if (TryBuildInvalidPrimaryRecoveryPlan(
                    inventory,
                    primary,
                    backup,
                    previous,
                    selected,
                    out InvalidPrimaryRecoveryPlan invalidPrimaryRecoveryPlan,
                    out bool invalidPrimaryRecoveryEvidenceConflict))
            {
                bool recovered = TryRecoverInvalidPrimaryFromExactBackup(
                    invalidPrimaryRecoveryPlan,
                    out SaveGameData recoveredSave,
                    out string quarantinePath,
                    out bool diskChanged,
                    out string recoveryMessage);
                if (recovered)
                {
                    _currentSave = recoveredSave;
                    _readOnlyCandidate = null;
                    _profileWritable = true;
                    _committedRecoveryWitnessBytes =
                        invalidPrimaryRecoveryPlan.BackupBytes.ToArray();
                    _committedInvalidPrimaryWitnessBytes =
                        invalidPrimaryRecoveryPlan.InvalidPrimaryBytes.ToArray();
                    _committedInvalidPrimaryQuarantinePath = quarantinePath;
                    _committedInvalidPrimaryRecoveryMarkerBytes =
                        invalidPrimaryRecoveryPlan.TransactionMarkerBytes.ToArray();
                    _committedInvalidPrimaryRecoveryMarkerPath =
                        StageFiveRecoveryMarkerPath;
                    PublishDisposition(
                        inventory,
                        invalidPrimaryRecoveryPlan.BackupCandidate,
                        "SAVE_SELECT_INVALID_PRIMARY_EXACT_BACKUP_RECOVERY",
                        true,
                        true,
                        diskChanged,
                        SaveCandidateSourceGeneration.Backup);
                    SetLoadStatus(
                        SaveLoadStatus.RecoveredFromBackup,
                        recoveryMessage,
                        false);
                    return;
                }

                _currentSave = null;
                _readOnlyCandidate = selectedSave;
                _profileWritable = false;
                PublishDisposition(
                    inventory,
                    invalidPrimaryRecoveryPlan.BackupCandidate,
                    "SAVE_SELECT_INVALID_PRIMARY_EXACT_BACKUP_RECOVERY",
                    false,
                    false,
                    diskChanged,
                    SaveCandidateSourceGeneration.Backup);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryFailed,
                    recoveryMessage,
                    true);
                return;
            }

            if (invalidPrimaryRecoveryEvidenceConflict)
            {
                _currentSave = null;
                _readOnlyCandidate = selectedSave;
                PublishDisposition(
                    inventory,
                    selected,
                    "SAVE_SELECT_INVALID_PRIMARY_RECOVERY_EVIDENCE_CONFLICT",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-INVALID-PRIMARY-RECOVERY-EVIDENCE-CONFLICT: Stage 5 quarantine provenance was malformed, ambiguous, or inconsistent; all evidence remains read-only.",
                    false);
                return;
            }

            bool hasCommittedBackupRecoveryWitness =
                TryGetCommittedBackupRecoveryWitness(
                    inventory,
                    out byte[] committedBackupRecoveryWitnessBytes);
            if (selected.SourceGeneration == SaveCandidateSourceGeneration.Primary &&
                HasWritableTempEvidence(inventory) &&
                !hasCommittedBackupRecoveryWitness)
            {
                _currentSave = null;
                _readOnlyCandidate = selectedSave;
                PublishDisposition(
                    inventory,
                    selected,
                    "SAVE_SELECT_RECOVERY_WITNESS_CONFLICT",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-RECOVERY-WITNESS-CONFLICT: A valid staged generation does not match complete primary and backup authority; all evidence remains read-only.",
                    false);
                return;
            }

            if (CanAttemptExactBackupRecovery(
                    inventory,
                    primary,
                    backup,
                    previous,
                    selected))
            {
                bool recovered = TryRecoverMissingPrimaryFromExactBackup(
                    selected,
                    out SaveGameData recoveredSave,
                    out bool diskChanged,
                    out string recoveryMessage);
                if (recovered)
                {
                    _currentSave = recoveredSave;
                    _readOnlyCandidate = null;
                    _profileWritable = true;
                    _committedRecoveryWitnessBytes = selected.CopyRawBytes();
                    PublishDisposition(
                        inventory,
                        selected,
                        selection.ReasonCode,
                        true,
                        true,
                        diskChanged);
                    SetLoadStatus(
                        SaveLoadStatus.RecoveredFromBackup,
                        recoveryMessage,
                        false);
                    return;
                }

                _currentSave = null;
                _readOnlyCandidate = selectedSave;
                _profileWritable = false;
                PublishDisposition(
                    inventory,
                    selected,
                    selection.ReasonCode,
                    false,
                    false,
                    diskChanged);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryFailed,
                    recoveryMessage,
                    true);
                return;
            }

            bool isPreSchemaMigration =
                selected.SourceGeneration == SaveCandidateSourceGeneration.Primary &&
                IsPreSchemaMetadata(selectedSave) &&
                HasRecoverableKingdomState(selectedSave);

            bool runtimeUsable =
                selected.SourceGeneration == SaveCandidateSourceGeneration.Primary &&
                ((selected.IsWritable && IsRuntimeRoundTrippable(selected)) ||
                 isPreSchemaMigration);
            bool writable = runtimeUsable &&
                (!HasUnresolvedAuxiliaryEvidence(inventory) ||
                 hasCommittedBackupRecoveryWitness);
            _profileWritable = writable;

            if (runtimeUsable)
            {
                if (isPreSchemaMigration)
                {
                    if (TryMigratePreSchemaPrimary(
                            selected,
                            selectedSave,
                            out SaveGameData migratedSave,
                            out string migrateMessage))
                    {
                        _currentSave = migratedSave;
                        _readOnlyCandidate = null;
                        _profileWritable = true;
                        _committedRecoveryWitnessBytes = null;
                        _committedInvalidPrimaryWitnessBytes = null;
                        _committedInvalidPrimaryQuarantinePath = null;
                        _committedInvalidPrimaryRecoveryMarkerBytes = null;
                        _committedInvalidPrimaryRecoveryMarkerPath = null;
                        PublishDisposition(
                            inventory,
                            selected,
                            selection.ReasonCode,
                            true,
                            true,
                            true);
                        ObservePrimaryAuthority(migratedSave);
                        SetLoadStatus(
                            SaveLoadStatus.LoadedPrimary,
                            migrateMessage,
                            false);
                        return;
                    }

                    if (TryCreateReplacementAfterUnmigratablePrimary(
                            out SaveGameData replacementSave,
                            out string replacementMessage))
                    {
                        _currentSave = replacementSave;
                        _readOnlyCandidate = null;
                        _profileWritable = true;
                        _committedRecoveryWitnessBytes = null;
                        _committedInvalidPrimaryWitnessBytes = null;
                        _committedInvalidPrimaryQuarantinePath = null;
                        _committedInvalidPrimaryRecoveryMarkerBytes = null;
                        _committedInvalidPrimaryRecoveryMarkerPath = null;
                        PublishDisposition(
                            inventory,
                            null,
                            "SAVE_SELECT_UNMIGRATABLE_PRIMARY_CREATE_NEW",
                            true,
                            true,
                            true,
                            SaveCandidateSourceGeneration.Primary);
                        SetLoadStatus(
                            SaveLoadStatus.CreatedNewAfterUnrecoverableCorruption,
                            replacementMessage,
                            false);
                        return;
                    }

                    _currentSave = null;
                    _readOnlyCandidate = selectedSave;
                    _profileWritable = false;
                    PublishDisposition(
                        inventory,
                        selected,
                        selection.ReasonCode,
                        false,
                        false,
                        false);
                    SetLoadStatus(
                        SaveLoadStatus.RecoveryFailed,
                        string.IsNullOrWhiteSpace(replacementMessage)
                            ? migrateMessage
                            : replacementMessage,
                        true);
                    return;
                }

                _currentSave = selectedSave;
                _readOnlyCandidate = null;
                _committedRecoveryWitnessBytes =
                    hasCommittedBackupRecoveryWitness
                        ? committedBackupRecoveryWitnessBytes
                        : null;
                PublishDisposition(
                    inventory,
                    selected,
                    selection.ReasonCode,
                    writable,
                    runtimeUsable,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.LoadedPrimary,
                    selected.Outcome == SaveSemanticCandidateOutcome.Valid
                        ? "AL-SAVE-LOAD-PRIMARY: A semantically valid primary was loaded without disk mutation or offline progression."
                        : "AL-SAVE-LOAD-PRIMARY-COMPATIBLE: A round-trippable primary using only approved compatibility handling was loaded without disk mutation or offline progression.",
                    false);
                return;
            }

            _currentSave = null;
            _readOnlyCandidate = selectedSave;

            if (selected.SourceGeneration != SaveCandidateSourceGeneration.Primary ||
                HasUnresolvedAuxiliaryEvidence(inventory) ||
                selected.Outcome == SaveSemanticCandidateOutcome.Valid)
            {
                PublishDisposition(
                    inventory,
                    selected,
                    selection.ReasonCode,
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryRequired,
                    "AL-SAVE-RECOVERY-REQUIRED: A bounded candidate view is available, but activation awaits an explicit recovery decision.",
                    false);
                return;
            }

            SaveLoadStatus status;
            string message;
            switch (selected.Outcome)
            {
                case SaveSemanticCandidateOutcome.CompatibleNormalized:
                    status = SaveLoadStatus.LoadedPrimaryNormalized;
                    message = "AL-SAVE-PRIMARY-NORMALIZED-READ-ONLY: A legacy-compatible in-memory view was loaded; original bytes remain authoritative.";
                    break;
                case SaveSemanticCandidateOutcome.CompatiblePreservedUnknown:
                    status = SaveLoadStatus.LoadedPrimaryWithPreservedUnknown;
                    message = "AL-SAVE-PRIMARY-PRESERVED-UNKNOWN: Known fields were loaded read-only while unknown content remains preserved on disk.";
                    break;
                default:
                    status = SaveLoadStatus.LoadedPrimaryDegraded;
                    message = "AL-SAVE-PRIMARY-DEGRADED: A degraded diagnostic view was loaded read-only; repair requires a later reviewed stage.";
                    break;
            }

            PublishDisposition(
                inventory,
                selected,
                selection.ReasonCode,
                writable,
                runtimeUsable,
                false);
            SetLoadStatus(status, message, false);
        }

        private void ActivateExactNvs01V003Migration(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            SaveSemanticCandidate selected)
        {
            SaveGameData retained = null;
            Nvs01ProgressData migratedProgress = null;
            Nvs01RuntimeDiagnostic migrationDiagnostic = null;
            SaveCanonicalLedger migrationBaseline = null;
            // Materialize the exact retained generation before rejecting
            // unresolved auxiliary evidence so diagnostics can expose the
            // same read-only bytes without granting runtime/write authority.
            if (selected?.SourceGeneration !=
                    SaveCandidateSourceGeneration.Primary ||
                !IsExactNvs01V003MigrationCandidate(selected) ||
                !TryDeserializeSelectedCandidate(selected, out retained) ||
                HasUnresolvedAuxiliaryEvidence(inventory) ||
                !Nvs01ProgressCodec.TryMigrateExactV003(
                    retained.Nvs01Progress,
                    out migratedProgress,
                    out migrationDiagnostic) ||
                !TryCaptureExactNvs01MigrationBaseline(
                    inventory,
                    selected,
                    out migrationBaseline))
            {
                _currentSave = null;
                _readOnlyCandidate = retained;
                _profileWritable = false;
                PublishDisposition(
                    inventory,
                    selected,
                    "SAVE_SELECT_REPAIRABLE_PRIMARY_REJECTED",
                    false,
                    false,
                    false);
                SetLoadStatus(
                    SaveLoadStatus.LoadedPrimaryDegraded,
                    "AL-SAVE-NVS01-MIGRATION-REJECTED: The retained packet identity or progress was not the one exact reviewed v003 migration input; original evidence remains read-only. " +
                    (migrationDiagnostic?.Code ?? string.Empty),
                    false);
                return;
            }

            SaveGameData candidate = CloneSave(retained);
            candidate.Nvs01Progress = migratedProgress;
            SaveOperationStatus status = PersistCandidate(
                candidate,
                null,
                null,
                migrationBaseline,
                preserveCandidateMetadata: true,
                out SaveGameData persistedSave,
                out SaveOperationDisposition disposition,
                out string message);
            LastSaveDisposition = disposition;

            if (status == SaveOperationStatus.SavedPrimary &&
                persistedSave != null)
            {
                _currentSave = persistedSave;
                ObservePrimaryAuthority(persistedSave);
                _readOnlyCandidate = null;
                _profileWritable = true;
                _committedRecoveryWitnessBytes = null;
                _committedInvalidPrimaryWitnessBytes = null;
                _committedInvalidPrimaryQuarantinePath = null;
                _committedInvalidPrimaryRecoveryMarkerBytes = null;
                _committedInvalidPrimaryRecoveryMarkerPath = null;

                IReadOnlyList<SaveCandidateInventoryEntry> migratedInventory =
                    BuildCandidateInventory();
                SaveSemanticCandidate migratedPrimary = Find(
                    migratedInventory,
                    SaveCandidateSourceGeneration.Primary)
                    .SemanticCandidate;
                PublishDisposition(
                    migratedInventory,
                    migratedPrimary,
                    "SAVE_SELECT_EXACT_NVS01_V003_MIGRATED",
                    true,
                    true,
                    true);
                SetLoadStatus(
                    SaveLoadStatus.LoadedPrimary,
                    "AL-SAVE-NVS01-V003-MIGRATED: Exact retained OMEN_1 v003 progress was atomically rebound to v004 without replaying an event or allocating an identity.",
                    false);
                return;
            }

            _currentSave = null;
            _readOnlyCandidate = retained;
            _profileWritable = false;
            PublishDisposition(
                inventory,
                selected,
                "SAVE_SELECT_REPAIRABLE_PRIMARY_MIGRATION_FAILED",
                false,
                false,
                disposition?.MayHaveMutated ?? false);
            SetLoadStatus(
                SaveLoadStatus.RecoveryFailed,
                "AL-SAVE-NVS01-MIGRATION-FAILED: The atomic v003-to-v004 rebind did not reach a twice-verified commit target; old generation and recovery evidence were preserved. " +
                message,
                status == SaveOperationStatus.CommitUncertain);
        }

        private static bool IsExactNvs01V003MigrationCandidate(
            SaveSemanticCandidate candidate)
        {
            if (candidate == null ||
                candidate.Outcome !=
                    SaveSemanticCandidateOutcome.RepairableWithDataChange ||
                !candidate.HasRetainedRawBytes ||
                candidate.DisabledDomains != SaveSemanticDomain.None ||
                candidate.NormalizedDomains != SaveSemanticDomain.None ||
                candidate.PreservedUnknownDomains != SaveSemanticDomain.None ||
                candidate.Diagnostics.Count != 1)
            {
                return false;
            }

            SaveSemanticDiagnostic diagnostic = candidate.Diagnostics[0];
            return diagnostic != null &&
                   diagnostic.Code ==
                       "SAVE_NVS01_PACKET_IDENTITY_MIGRATION_REQUIRED" &&
                   diagnostic.Path == "$.Nvs01Progress" &&
                   diagnostic.Domain == SaveSemanticDomain.Narrative &&
                   diagnostic.Severity ==
                       SaveSemanticDiagnosticSeverity.Information;
        }

        public bool HasSave()
        {
            if (HasSaveEvidence(SavePath) ||
                HasSaveEvidence(BackupPath) ||
                HasSaveEvidence(PreviousPath) ||
                HasSaveEvidence(LegacyPreviousPath) ||
                HasSaveEvidence(TempPath) ||
                HasSaveEvidence(StageFiveRecoveryMarkerPath) ||
                HasNvs01MigrationBackupArchiveEvidence() ||
                HasProfileIdentityWitnessEvidence())
            {
                return true;
            }

            return !TryEnumerateStageFiveQuarantineMarkers(
                       out IReadOnlyList<StageFiveQuarantineMarker> markers,
                       out _) ||
                   markers.Count != 0 ||
                   HasStageFiveTransactionArchiveEvidence();
        }

        public void CreateNewSave(RealmId realmId)
        {
            var request = new RealmSelectionRequest(
                Guid.NewGuid().ToString("N"),
                realmId);
            if (HasExactSchemaTwoProfile(_currentSave))
            {
                ((IProfileBoundRealmSelectionCandidateStore)this)
                    .TryCommitProfileBoundRealmSelection(request);
                return;
            }

            ((ILegacyRealmSelectionCandidateStore)this)
                .TryCommitLegacyRealmSelection(request);
        }

        public void DeleteSave()
        {
            if (!ProfileMutationContainment.CanInvokeDeleteSave(this))
            {
                return;
            }

            bool approvalResetOperation = MvpApprovalSlotRuntime.IsDeleteAuthorized(this);
            _profileDeleted = false;
            var deletionTargets = new List<string>
            {
                SavePath,
                BackupPath,
                TempPath,
                PreviousPath,
                LegacyPreviousPath,
                StageFiveRecoveryMarkerPath
            };

            deletionTargets.AddRange(EnumerateQuarantines(SaveFileName));
            deletionTargets.AddRange(EnumerateQuarantines(BackupFileName));
            deletionTargets.AddRange(EnumerateNvs01MigrationBackupArchives());

            var failures = new List<string>();
#if UNITY_INCLUDE_TESTS
            Action beforeDeleteArtifacts = BeforeDeleteArtifactsForTests;
            BeforeDeleteArtifactsForTests = null;
            beforeDeleteArtifacts?.Invoke();
#endif
            foreach (string target in deletionTargets.Distinct().ToList())
            {
                if (!TryDelete(target, approvalResetOperation))
                {
                    failures.Add(target);
                }
            }

            var remaining = deletionTargets
                .Distinct()
                .Where(path => _fileOperations.FileExists(path))
                .ToList();

            if (failures.Count > 0 || remaining.Count > 0)
            {
                LastSaveStatus = SaveOperationStatus.DeleteFailed;
                LastSaveMessage = $"AL-SAVE-DELETE-FAILED: Local save reset could not remove every profile artifact. Failed={failures.Count}; Remaining={remaining.Count}.";
                Debug.LogError(LastSaveMessage);
                return;
            }

            _currentSave = null;
            _readOnlyCandidate = null;
            _profileWritable = false;
            _profileDeleted = true;
            ResetObservedAuthority();
            _committedRecoveryWitnessBytes = null;
            _committedInvalidPrimaryWitnessBytes = null;
            _committedInvalidPrimaryQuarantinePath = null;
            _committedInvalidPrimaryRecoveryMarkerBytes = null;
            _committedInvalidPrimaryRecoveryMarkerPath = null;
            LastLoadDisposition = null;
            LastLoadStatus = SaveLoadStatus.None;
            LastLoadMessage = "AL-SAVE-DELETED: Local save data deleted.";
            LastSaveStatus = SaveOperationStatus.None;
            LastSaveMessage = string.Empty;
            LastSaveDisposition = null;
        }

        private sealed class SaveAuthorityBaseline
        {
            public SaveAuthorityBaseline(
                SaveFileReadResult primary,
                SaveFileReadResult backup)
            {
                Primary = primary;
                Backup = backup;
            }

            public SaveFileReadResult Primary { get; }
            public SaveFileReadResult Backup { get; }
        }

        private sealed class SaveCanonicalLedger
        {
            public SaveCanonicalLedger(
                SaveFileReadResult primary,
                SaveFileReadResult backup,
                SaveFileReadResult temp,
                SaveFileReadResult previous)
            {
                Primary = primary;
                Backup = backup;
                Temp = temp;
                Previous = previous;
            }

            public SaveFileReadResult Primary { get; }
            public SaveFileReadResult Backup { get; }
            public SaveFileReadResult Temp { get; }
            public SaveFileReadResult Previous { get; }
        }

        private sealed class SaveTransactionTrace
        {
            public SaveTransactionTrace(
                byte[] baselinePrimaryBytes,
                SaveFileReadResult baselineBackup,
                byte[] candidateBytes,
                bool baselinePrimaryIsMigratable,
                string migrationBackupArchivePath)
            {
                BaselinePrimaryBytes = baselinePrimaryBytes;
                BaselineBackup = baselineBackup;
                CandidateBytes = candidateBytes;
                BaselinePrimaryIsMigratable =
                    baselinePrimaryIsMigratable;
                MigrationBackupArchivePath =
                    migrationBackupArchivePath ?? string.Empty;
            }

            public byte[] BaselinePrimaryBytes { get; }
            public SaveFileReadResult BaselineBackup { get; }
            public byte[] CandidateBytes { get; }
            public bool BaselinePrimaryIsMigratable { get; }
            public string MigrationBackupArchivePath { get; }
            public bool RollbackAttempted { get; set; }
            public bool RollbackBytesVerified { get; set; }
        }

        private bool TryMigratePreSchemaPrimary(
            SaveSemanticCandidate selected,
            SaveGameData selectedSave,
            out SaveGameData migratedSave,
            out string message)
        {
            migratedSave = null;
            message = string.Empty;

            SaveGameData candidate = CloneSave(selectedSave);
            candidate.SaveFormatId = SaveGameData.CurrentSaveFormatId;
            candidate.SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion;
            candidate.ProfileInitializationVersion =
                SaveGameData.CurrentProfileInitializationVersion;
            if (string.IsNullOrEmpty(candidate.ProfileId))
            {
                candidate.ProfileId = new CryptographicProfileIdentityCandidateSource()
                    .GetCandidate(1);
            }
            ApplyApprovedNeutralNormalization(candidate, selected);

            SaveOperationStatus status = PersistCandidate(
                candidate,
                null,
                null,
                out SaveGameData persistedSave,
                out SaveOperationDisposition disposition,
                out string persistMessage);
            LastSaveDisposition = disposition;

            if (status != SaveOperationStatus.SavedPrimary ||
                persistedSave == null)
            {
                message = persistMessage ??
                    "AL-SAVE-PRESCHEMA-MIGRATION-FAILED: A pre-schema primary could not be stamped and durably persisted; original evidence was preserved read-only.";
                return false;
            }

            migratedSave = persistedSave;
            message =
                "AL-SAVE-PRESCHEMA-MIGRATED: A legacy pre-schema primary was stamped with current save metadata and durably persisted; original evidence was preserved.";
            return true;
        }

        private bool TryCreateReplacementAfterUnmigratablePrimary(
            out SaveGameData createdSave,
            out string message)
        {
            createdSave = null;
            message = string.Empty;

            if (HasSaveEvidence(BackupPath) ||
                HasSaveEvidence(PreviousPath) ||
                HasSaveEvidence(LegacyPreviousPath))
            {
                message =
                    "AL-SAVE-PRESCHEMA-MIGRATION-FAILED: A pre-schema primary could not be stamped; remaining generations were preserved for explicit recovery.";
                return false;
            }

            string quarantineError;
            string tempQuarantineError = string.Empty;
            if (!TryQuarantineInvalidFile(SavePath, out quarantineError) ||
                !TryQuarantineInvalidFile(TempPath, out tempQuarantineError))
            {
                message =
                    "AL-SAVE-PRESCHEMA-QUARANTINE-FAILED: Unmigratable primary could not be quarantined before replacement. " +
                    quarantineError +
                    " " +
                    tempQuarantineError;
                return false;
            }

            if (!AllCanonicalPathsMissing(includeTemp: true))
            {
                message =
                    "AL-SAVE-PRESCHEMA-MIGRATION-FAILED: Unmigratable primary was preserved because replacement would not be exclusive.";
                return false;
            }

            SaveGameData newSave = CreateDefaultSave(RealmId.None);
            SaveGameData firstGenerationCandidate = CloneSave(newSave);
            bool coreSucceeded = TryCreateFirstGenerationCandidate(
                firstGenerationCandidate,
                out SaveGameData persistedNewSave,
                out string createMessage,
                out bool diskChanged);
            SaveOperationStatus reconciledStatus =
                ReconcileFirstGenerationAttempt(
                    firstGenerationCandidate,
                    coreSucceeded,
                    diskChanged,
                    ref persistedNewSave,
                    out _,
                    ref createMessage);
            if (reconciledStatus != SaveOperationStatus.SavedPrimary ||
                persistedNewSave == null)
            {
                message = createMessage;
                return false;
            }

            createdSave = persistedNewSave;
            message =
                "AL-SAVE-CREATED-NEW-AFTER-UNRECOVERABLE: A pre-schema primary could not be stamped and persisted, so the original bytes were quarantined and a current-schema profile was created.";
            return true;
        }

        private SaveOperationStatus PersistCandidate(
            SaveGameData candidate,
            byte[] requiredRecoveryWitnessBytes,
            SaveAuthorityBaseline requiredLegacyBaseline,
            out SaveGameData persistedSave,
            out SaveOperationDisposition disposition,
            out string message) =>
            PersistCandidate(
                candidate,
                requiredRecoveryWitnessBytes,
                requiredLegacyBaseline,
                null,
                preserveCandidateMetadata: false,
                out persistedSave,
                out disposition,
                out message);

        private SaveOperationStatus PersistCandidate(
            SaveGameData candidate,
            byte[] requiredRecoveryWitnessBytes,
            SaveAuthorityBaseline requiredLegacyBaseline,
            SaveCanonicalLedger requiredIdentityMigrationBaseline,
            bool preserveCandidateMetadata,
            out SaveGameData persistedSave,
            out SaveOperationDisposition disposition,
            out string message)
        {
            persistedSave = null;
            disposition = null;
            message = string.Empty;
            bool mayHaveMutated = false;

            SaveAuthorityBaseline baseline;
            try
            {
                if (requiredIdentityMigrationBaseline == null)
                {
                    baseline = new SaveAuthorityBaseline(
                        ReadCanonicalPath(SavePath),
                        ReadCanonicalPath(BackupPath));
                }
                else
                {
                    SaveCanonicalLedger migrationLedger =
                        CaptureCanonicalLedger();
                    if (!MatchesExactMigrationBaseline(
                            migrationLedger,
                            requiredIdentityMigrationBaseline))
                    {
                        message =
                            "AL-SAVE-NVS01-MIGRATION-LEDGER-CHANGED: Primary, backup, temp, or previous authority changed before the migration transaction; no write was attempted.";
                        disposition = CreateSaveDisposition(
                            SaveOperationStatus.CommitUncertain,
                            false,
                            false,
                            false,
                            false,
                            false,
                            message);
                        return SaveOperationStatus.CommitUncertain;
                    }

                    baseline = new SaveAuthorityBaseline(
                        migrationLedger.Primary,
                        migrationLedger.Backup);
                }
            }
            catch (Exception ex)
            {
                message = $"AL-SAVE-BASELINE-READ-FAILED: Canonical authority could not be inventoried before persistence. {ex.GetType().Name}";
                disposition = CreateSaveDisposition(
                    SaveOperationStatus.CommitUncertain,
                    false,
                    false,
                    false,
                    false,
                    false,
                    message);
                return SaveOperationStatus.CommitUncertain;
            }

            if (requiredLegacyBaseline != null &&
                !TryMatchPinnedLegacyAuthority(
                    baseline,
                    requiredLegacyBaseline,
                    out message))
            {
                disposition = CreateSaveDisposition(
                    SaveOperationStatus.CommitUncertain,
                    false,
                    false,
                    false,
                    false,
                    false,
                    message);
                return SaveOperationStatus.CommitUncertain;
            }

            if (!IsStableBoundedState(baseline.Primary) ||
                !IsStableBoundedState(baseline.Backup))
            {
                message = "AL-SAVE-BASELINE-UNREADABLE: Primary or backup authority was not a stable bounded generation; persistence was not attempted.";
                disposition = CreateSaveDisposition(
                    SaveOperationStatus.CommitUncertain,
                    false,
                    false,
                    false,
                    false,
                    false,
                    message);
                return SaveOperationStatus.CommitUncertain;
            }

            if (!HasCurrentSaveMetadata(candidate))
            {
                message = "AL-SAVE-UNMIGRATED-READ-ONLY: Legacy or unsupported save metadata requires an explicit reviewed migration before persistence; existing files were preserved.";
                return ReconcileSaveAttempt(
                    baseline,
                    null,
                    null,
                    false,
                    false,
                    candidate,
                    null,
                    ref persistedSave,
                    out disposition,
                    ref message);
            }

            try
            {
                if (!preserveCandidateMetadata)
                {
                    ApplyNeutralPersistenceDefaults(candidate);
                    candidate.LastSavedTimestamp =
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
                if (!TrySerializeBounded(candidate, out string json, out message))
                {
                    return ReconcileSaveAttempt(
                        baseline,
                        null,
                        null,
                        false,
                        false,
                        candidate,
                        null,
                        ref persistedSave,
                        out disposition,
                        ref message);
                }

                byte[] candidateBytes = StrictUtf8.GetBytes(json);
                bool priorPrimaryValid =
                    requiredIdentityMigrationBaseline != null ||
                    baseline.Primary.Disposition == SaveFileReadDisposition.Read &&
                    TryDeserializeValidSaveBytes(
                        baseline.Primary.Bytes,
                        out _,
                        out _);
                byte[] requiredBackupBytes = priorPrimaryValid
                    ? baseline.Primary.Bytes
                    : candidateBytes;
                string migrationBackupArchivePath =
                    requiredIdentityMigrationBaseline != null &&
                    baseline.Backup.Disposition ==
                        SaveFileReadDisposition.Read
                        ? CreateNvs01MigrationBackupArchivePath(
                            baseline.Backup.Bytes)
                        : string.Empty;
                var trace = new SaveTransactionTrace(
                    priorPrimaryValid ? baseline.Primary.Bytes : null,
                    baseline.Backup,
                    candidateBytes,
                    requiredIdentityMigrationBaseline != null,
                    migrationBackupArchivePath);

                bool coreSucceeded = TryPersistCandidateCore(
                    json,
                    candidateBytes,
                    baseline.Primary,
                    priorPrimaryValid,
                    requiredRecoveryWitnessBytes,
                    requiredLegacyBaseline,
                    requiredIdentityMigrationBaseline,
                    trace,
                    out message,
                    out bool coreMayHaveMutated);
                mayHaveMutated |= coreMayHaveMutated;

                return ReconcileSaveAttempt(
                    baseline,
                    candidateBytes,
                    requiredBackupBytes,
                    coreSucceeded,
                    mayHaveMutated,
                    candidate,
                    trace,
                    requiredIdentityMigrationBaseline != null,
                    ref persistedSave,
                    out disposition,
                    ref message);
            }
            catch (Exception ex)
            {
                mayHaveMutated = true;
                message = $"AL-SAVE-TRANSACTION-INTERRUPTED: Save transaction stopped; every remaining generation was preserved for reconciliation. {ex.GetType().Name}";
                return ReconcileSaveAttempt(
                    baseline,
                    null,
                    null,
                    false,
                    mayHaveMutated,
                    candidate,
                    null,
                    ref persistedSave,
                    out disposition,
                    ref message);
            }
        }

        private bool TryPersistCandidateCore(
            string json,
            byte[] candidateBytes,
            SaveFileReadResult baselinePrimary,
            bool primaryValid,
            byte[] requiredRecoveryWitnessBytes,
            SaveAuthorityBaseline requiredLegacyBaseline,
            SaveCanonicalLedger requiredIdentityMigrationBaseline,
            SaveTransactionTrace trace,
            out string message,
            out bool mayHaveMutated)
        {
            mayHaveMutated = false;
            try
            {
                if (requiredLegacyBaseline != null)
                {
                    var consumptionBaseline = new SaveAuthorityBaseline(
                        ReadCanonicalPath(SavePath),
                        ReadCanonicalPath(BackupPath));
                    if (!TryMatchPinnedLegacyAuthority(
                            consumptionBaseline,
                            requiredLegacyBaseline,
                            out message))
                    {
                        return false;
                    }
                }

                if (requiredRecoveryWitnessBytes != null &&
                    !IsCommittedRecoveryWitnessTarget(
                        CaptureCanonicalLedger(),
                        requiredRecoveryWitnessBytes,
                        out _))
                {
                    message =
                        "AL-SAVE-RECOVERY-WITNESS-CONSUME-BLOCKED: Exact recovery authority changed at the temp-consumption boundary; every generation was preserved.";
                    return false;
                }

                if (requiredIdentityMigrationBaseline != null &&
                    !MatchesExactMigrationBaseline(
                        CaptureCanonicalLedger(),
                        requiredIdentityMigrationBaseline))
                {
                    message =
                        "AL-SAVE-NVS01-MIGRATION-LEDGER-CHANGED: Complete four-generation authority changed before candidate staging; no filesystem mutation was attempted.";
                    return false;
                }

                _fileOperations.CreateDirectory(PersistencePath);

                if (requiredIdentityMigrationBaseline == null)
                {
                    bool previousExisted =
                        _fileOperations.FileExists(PreviousPath);
                    if (previousExisted && !TryDelete(PreviousPath))
                    {
                        mayHaveMutated = true;
                        message = "AL-SAVE-PREVIOUS-CLEANUP-FAILED: The stale previous generation could not be removed before candidate staging.";
                        return false;
                    }

                    mayHaveMutated |= previousExisted;
                    bool tempExisted = _fileOperations.FileExists(TempPath);
                    if (tempExisted && !TryDelete(TempPath))
                    {
                        mayHaveMutated |= tempExisted;
                        message = "AL-SAVE-TEMP-CLEANUP-FAILED: Existing temporary save could not be removed before preparing a new candidate.";
                        return false;
                    }

                    mayHaveMutated |= tempExisted;
                }

                if (requiredIdentityMigrationBaseline != null &&
                    !MatchesExactMigrationBaseline(
                        CaptureCanonicalLedger(),
                        requiredIdentityMigrationBaseline))
                {
                    message =
                        "AL-SAVE-NVS01-MIGRATION-AUXILIARY-APPEARED: Temp or previous authority appeared immediately before exclusive staging; it was preserved without mutation.";
                    return false;
                }

                SaveFileWriteResult writeResult =
                    _fileOperations.WriteAllTextDurable(TempPath, json);
                mayHaveMutated |= writeResult.DiskChanged;
                if (!writeResult.Succeeded)
                {
                    message = $"AL-SAVE-TEMP-WRITE-FAILED: The temporary save could not be written durably. {writeResult.DiagnosticCode}";
                    return false;
                }

                if (!TryReadValidSaveBytes(
                        TempPath,
                        out byte[] stagedBytes,
                        out _,
                        out string tempValidationError) ||
                    !BytesEqual(stagedBytes, candidateBytes))
                {
                    message = $"AL-SAVE-TEMP-INVALID: Temporary save validation or exact-byte verification failed; existing authority was retained. {tempValidationError}";
                    return false;
                }

                if (requiredIdentityMigrationBaseline != null &&
                    !MatchesExactMigrationStagedLedger(
                        CaptureCanonicalLedger(),
                        requiredIdentityMigrationBaseline,
                        candidateBytes))
                {
                    message =
                        "AL-SAVE-NVS01-MIGRATION-STAGED-LEDGER-CHANGED: Complete authority changed after exclusive staging; every observed generation was preserved.";
                    return false;
                }

                SaveFileReadResult currentPrimary = ReadCanonicalPath(SavePath);
                if (!MatchesExactState(currentPrimary, baselinePrimary))
                {
                    message = "AL-SAVE-PRIMARY-CHANGED: Primary authority changed after baseline inventory; no install was attempted.";
                    return false;
                }

                bool primaryExists =
                    baselinePrimary.Disposition == SaveFileReadDisposition.Read;
                if (primaryExists && !primaryValid)
                {
                    mayHaveMutated = true;
                    if (!TryQuarantineInvalidFile(SavePath, out string quarantineError))
                    {
                        message = $"AL-SAVE-PRIMARY-QUARANTINE-FAILED: Primary was invalid before save and could not be quarantined; remaining evidence was preserved. {quarantineError}";
                        return false;
                    }

                    Debug.LogWarning(
                        "AL-SAVE-PRIMARY-CORRUPT: Invalid primary quarantined before installing a validated candidate.");
                }

                mayHaveMutated = true;
                if (primaryValid)
                {
                    bool installed = requiredIdentityMigrationBaseline != null
                        ? TryInstallWithMoveFallback(trace, out message)
                        : TryInstallWithAtomicReplace(trace, out message);
                    if (!installed)
                    {
                        return false;
                    }
                }
                else
                {
                    _fileOperations.Move(TempPath, SavePath);
                    if (!TryReadValidSave(SavePath, out _, out string installValidationError))
                    {
                        message = $"AL-SAVE-INSTALL-INVALID: Installed primary failed validation; every remaining generation was retained. {installValidationError}";
                        return false;
                    }

                    _fileOperations.Copy(SavePath, BackupPath, true);
                }

                if (!TryReadValidSaveBytes(
                        SavePath,
                        out byte[] finalPrimaryBytes,
                        out _,
                        out string finalPrimaryError) ||
                    !BytesEqual(finalPrimaryBytes, candidateBytes))
                {
                    message = $"AL-SAVE-FINAL-PRIMARY-INVALID: Installed primary failed exact C1 verification. {finalPrimaryError}";
                    return false;
                }

                byte[] requiredBackupBytes = primaryValid
                    ? baselinePrimary.Bytes
                    : candidateBytes;
                string finalBackupError = "Backup file does not exist.";
                SaveFileReadResult finalBackup = ReadCanonicalPath(BackupPath);
                bool finalBackupValid =
                    requiredIdentityMigrationBaseline != null
                        ? MatchesExactBytes(
                            finalBackup,
                            requiredIdentityMigrationBaseline.Primary.Bytes)
                        : finalBackup.Disposition ==
                              SaveFileReadDisposition.Read &&
                          TryDeserializeValidSaveBytes(
                              finalBackup.Bytes,
                              out _,
                              out finalBackupError) &&
                          BytesEqual(
                              finalBackup.Bytes,
                              requiredBackupBytes);
                if (!finalBackupValid)
                {
                    message = $"AL-SAVE-FINAL-BACKUP-INVALID: Backup failed exact required-generation verification. {finalBackupError}";
                    return false;
                }

                bool migrationGenerationsFinalized =
                    requiredIdentityMigrationBaseline == null ||
                    TryFinalizeExactNvs01MigrationGenerations(
                        trace,
                        candidateBytes);
                bool tempClean = requiredIdentityMigrationBaseline != null
                    ? migrationGenerationsFinalized
                    : TryDelete(TempPath);
                bool previousClean =
                    requiredIdentityMigrationBaseline != null
                        ? migrationGenerationsFinalized
                        : TryDelete(PreviousPath);
                bool recoveryMarkerArchived =
                    TryArchiveCommittedInvalidPrimaryRecoveryMarker();
                if (!tempClean || !previousClean || !recoveryMarkerArchived)
                {
                    message = "AL-SAVE-BACKUP-CLEANUP-FAILED: Candidate and backup validated, but canonical residue or the preserved Stage 5 transaction marker did not reach its exact cleanup target.";
                    return false;
                }

                PruneQuarantines(
                    SaveFileName,
                    _committedInvalidPrimaryQuarantinePath,
                    _committedInvalidPrimaryRecoveryMarkerPath);
                PruneQuarantines(BackupFileName);
                message = $"AL-SAVE-SAVED-PRIMARY: Game saved safely to {SavePath}.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"AL-SAVE-TRANSACTION-INTERRUPTED: Save transaction stopped; every remaining generation was preserved for reconciliation. {ex.GetType().Name}";
                return false;
            }
        }

        private SaveOperationStatus ReconcileSaveAttempt(
            SaveAuthorityBaseline baseline,
            byte[] candidateBytes,
            byte[] requiredBackupBytes,
            bool coreSucceeded,
            bool mayHaveMutated,
            SaveGameData candidate,
            SaveTransactionTrace trace,
            ref SaveGameData persistedSave,
            out SaveOperationDisposition disposition,
            ref string message) =>
            ReconcileSaveAttempt(
                baseline,
                candidateBytes,
                requiredBackupBytes,
                coreSucceeded,
                mayHaveMutated,
                candidate,
                trace,
                requiredBackupIsMigratable: false,
                ref persistedSave,
                out disposition,
                ref message);

        private SaveOperationStatus ReconcileSaveAttempt(
            SaveAuthorityBaseline baseline,
            byte[] candidateBytes,
            byte[] requiredBackupBytes,
            bool coreSucceeded,
            bool mayHaveMutated,
            SaveGameData candidate,
            SaveTransactionTrace trace,
            bool requiredBackupIsMigratable,
            ref SaveGameData persistedSave,
            out SaveOperationDisposition disposition,
            ref string message)
        {
            SaveCanonicalLedger finalLedger = CaptureCanonicalLedger();
            SaveGameData verifiedCandidate = null;
            bool candidatePrimaryVerified =
                candidateBytes != null &&
                IsExactValidGeneration(
                    finalLedger.Primary,
                    candidateBytes,
                    out verifiedCandidate);
            bool requiredBackupVerified =
                requiredBackupBytes != null &&
                IsExactRequiredBackupGeneration(
                    finalLedger.Backup,
                    requiredBackupBytes,
                    requiredBackupIsMigratable,
                    out _);
            bool previousAuthorityVerified =
                MatchesExactState(finalLedger.Primary, baseline.Primary) &&
                MatchesExactState(finalLedger.Backup, baseline.Backup);
            bool cleanupVerified =
                finalLedger.Temp.Disposition == SaveFileReadDisposition.Missing &&
                finalLedger.Previous.Disposition == SaveFileReadDisposition.Missing &&
                IsExactNvs01MigrationBackupArchive(trace) &&
                IsCommittedRecoveryMarkerCleanupVerified();
            bool committedQuarantineVerified =
                IsCommittedInvalidPrimaryQuarantineIntact();
            SaveGameData priorPublishedSave = null;
            if (baseline.Primary.Disposition == SaveFileReadDisposition.Read)
            {
                TryDeserializeValidSaveBytes(
                    baseline.Primary.Bytes,
                    out priorPublishedSave,
                    out _);
            }

            bool completeCommitTarget =
                candidatePrimaryVerified &&
                requiredBackupVerified &&
                cleanupVerified &&
                committedQuarantineVerified;
            bool commitVerifiedTwice =
                completeCommitTarget &&
                VerifyCommitTargetAgain(
                    candidateBytes,
                    requiredBackupBytes,
                    requiredBackupIsMigratable,
                    trace,
                    out verifiedCandidate);

            SaveOperationStatus status;
            if (commitVerifiedTwice)
            {
                status = SaveOperationStatus.SavedPrimary;
                persistedSave = verifiedCandidate ?? CloneSave(candidate);
                if (!coreSucceeded)
                {
                    message = "AL-SAVE-COMMIT-RECONCILED: The interrupted transaction nevertheless reached and twice verified the complete commit target.";
                }
            }
            else if (completeCommitTarget)
            {
                status = SaveOperationStatus.CommitUncertain;
                persistedSave = priorPublishedSave;
                message = "AL-SAVE-COMMIT-REVERIFY-FAILED: The complete commit target was observed once but could not be proven by the required second bounded inventory.";
            }
            else if (previousAuthorityVerified)
            {
                status = SaveOperationStatus.SaveFailedPreviousPreserved;
                persistedSave = priorPublishedSave;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "AL-SAVE-FAILED-PREVIOUS-PRESERVED: Exact prior primary and backup authority remained unchanged.";
                }
            }
            else
            {
                status = SaveOperationStatus.CommitUncertain;
                persistedSave = priorPublishedSave;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "AL-SAVE-COMMIT-UNCERTAIN: Exact candidate commit and exact prior authority could not both be ruled in; all evidence was preserved.";
                }
            }

            disposition = CreateSaveDisposition(
                status,
                mayHaveMutated,
                candidatePrimaryVerified,
                requiredBackupVerified,
                previousAuthorityVerified,
                cleanupVerified,
                trace != null && trace.RollbackAttempted,
                trace != null &&
                trace.RollbackAttempted &&
                previousAuthorityVerified,
                message);
            return status;
        }

        private SaveOperationStatus ReconcileFirstGenerationAttempt(
            SaveGameData candidate,
            bool coreSucceeded,
            bool mayHaveMutated,
            ref SaveGameData persistedSave,
            out SaveOperationDisposition disposition,
            ref string message)
        {
            if (!TrySerializeBounded(
                    candidate,
                    out string candidateJson,
                    out string serializationMessage))
            {
                SaveOperationStatus unavailableStatus = mayHaveMutated
                    ? SaveOperationStatus.CommitUncertain
                    : SaveOperationStatus.SaveFailedPreviousPreserved;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = serializationMessage;
                }

                disposition = CreateSaveDisposition(
                    unavailableStatus,
                    mayHaveMutated,
                    false,
                    false,
                    false,
                    false,
                    message);
                return unavailableStatus;
            }

            var missing = new SaveFileReadResult(
                SaveFileReadDisposition.Missing,
                null,
                0,
                "SAVE_FILE_MISSING");
            var baseline = new SaveAuthorityBaseline(missing, missing);
            byte[] candidateBytes = StrictUtf8.GetBytes(candidateJson);
            return ReconcileSaveAttempt(
                baseline,
                candidateBytes,
                candidateBytes,
                coreSucceeded,
                mayHaveMutated,
                candidate,
                null,
                ref persistedSave,
                out disposition,
                ref message);
        }

        private SaveFileReadResult ReadCanonicalPath(string path)
        {
            try
            {
                return _fileOperations.ReadAllBytesBounded(
                    path,
                    _semanticPolicy.MaximumInputBytes);
            }
            catch (Exception)
            {
                return new SaveFileReadResult(
                    SaveFileReadDisposition.IoFailure,
                    null,
                    0,
                    "SAVE_FILE_READ_THREW");
            }
        }

        private SaveCanonicalLedger CaptureCanonicalLedger() =>
            new SaveCanonicalLedger(
                ReadCanonicalPath(SavePath),
                ReadCanonicalPath(BackupPath),
                ReadCanonicalPath(TempPath),
                ReadCanonicalPath(PreviousPath));

        private bool TryCaptureExactNvs01MigrationBaseline(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            SaveSemanticCandidate selected,
            out SaveCanonicalLedger baseline)
        {
            baseline = null;
            if (inventory == null || selected == null ||
                selected.SourceGeneration !=
                    SaveCandidateSourceGeneration.Primary)
            {
                return false;
            }

            SaveCanonicalLedger captured = CaptureCanonicalLedger();
            foreach (SaveCandidateInventoryEntry entry in inventory)
            {
                SaveFileReadResult actual;
                switch (entry.Source)
                {
                    case SaveCandidateSourceGeneration.Primary:
                        actual = captured.Primary;
                        break;
                    case SaveCandidateSourceGeneration.Backup:
                        actual = captured.Backup;
                        break;
                    case SaveCandidateSourceGeneration.Temp:
                        actual = captured.Temp;
                        break;
                    case SaveCandidateSourceGeneration.Previous:
                        actual = captured.Previous;
                        break;
                    default:
                        return false;
                }

                if (!MatchesExactInventoryEntry(actual, entry))
                {
                    return false;
                }
            }

            if (captured.Temp.Disposition != SaveFileReadDisposition.Missing ||
                captured.Previous.Disposition != SaveFileReadDisposition.Missing ||
                !MatchesExactBytes(captured.Primary, selected.CopyRawBytes()))
            {
                return false;
            }

            baseline = captured;
            return true;
        }

        private static bool MatchesExactInventoryEntry(
            SaveFileReadResult actual,
            SaveCandidateInventoryEntry expected)
        {
            if (actual == null || expected?.ReadResult == null ||
                actual.Disposition != expected.ReadResult.Disposition ||
                actual.ObservedByteCount !=
                    expected.ReadResult.ObservedByteCount)
            {
                return false;
            }

            if (actual.Disposition == SaveFileReadDisposition.Missing)
            {
                return true;
            }

            return actual.Disposition == SaveFileReadDisposition.Read &&
                   expected.SemanticCandidate != null &&
                   expected.SemanticCandidate.HasRetainedRawBytes &&
                   BytesEqual(
                       actual.Bytes,
                       expected.SemanticCandidate.CopyRawBytes());
        }

        private static bool MatchesExactMigrationBaseline(
            SaveCanonicalLedger actual,
            SaveCanonicalLedger expected) =>
            actual != null &&
            expected != null &&
            expected.Temp.Disposition == SaveFileReadDisposition.Missing &&
            expected.Previous.Disposition == SaveFileReadDisposition.Missing &&
            MatchesExactState(actual.Primary, expected.Primary) &&
            MatchesExactState(actual.Backup, expected.Backup) &&
            MatchesExactState(actual.Temp, expected.Temp) &&
            MatchesExactState(actual.Previous, expected.Previous);

        private bool MatchesExactMigrationStagedLedger(
            SaveCanonicalLedger actual,
            SaveCanonicalLedger expected,
            byte[] candidateBytes) =>
            actual != null &&
            expected != null &&
            MatchesExactState(actual.Primary, expected.Primary) &&
            MatchesExactState(actual.Backup, expected.Backup) &&
            IsExactValidGeneration(actual.Temp, candidateBytes, out _) &&
            actual.Previous.Disposition == SaveFileReadDisposition.Missing;

        private string CreateNvs01MigrationBackupArchivePath(
            byte[] baselineBackupBytes) =>
            Path.Combine(
                PersistencePath,
                Nvs01MigrationBackupArchivePrefix +
                ComputeSha256Base64Url(baselineBackupBytes));

        private bool TryFinalizeExactNvs01MigrationGenerations(
            SaveTransactionTrace trace,
            byte[] candidateBytes)
        {
            SaveCanonicalLedger postRotation = CaptureCanonicalLedger();
            if (!MatchesExactNvs01MigrationCanonicalTarget(
                    postRotation,
                    trace,
                    candidateBytes))
            {
                return false;
            }

            switch (trace.BaselineBackup.Disposition)
            {
                case SaveFileReadDisposition.Missing:
                    if (trace.MigrationBackupArchivePath.Length != 0 ||
                        postRotation.Previous.Disposition !=
                            SaveFileReadDisposition.Missing)
                    {
                        return false;
                    }
                    break;
                case SaveFileReadDisposition.Read:
                    if (trace.MigrationBackupArchivePath.Length == 0 ||
                        !MatchesExactState(
                            postRotation.Previous,
                            trace.BaselineBackup) ||
                        ReadCanonicalPath(
                                trace.MigrationBackupArchivePath)
                            .Disposition != SaveFileReadDisposition.Missing)
                    {
                        return false;
                    }

                    try
                    {
                        _fileOperations.Move(
                            PreviousPath,
                            trace.MigrationBackupArchivePath);
                    }
                    catch (Exception)
                    {
                        // A non-overwriting move that threw may still have
                        // retained one complete generation; verify below.
                    }
                    break;
                default:
                    return false;
            }

            return VerifyExactNvs01MigrationCommitTarget(
                       trace,
                       candidateBytes) &&
                   VerifyExactNvs01MigrationCommitTarget(
                       trace,
                       candidateBytes);
        }

        private bool VerifyExactNvs01MigrationCommitTarget(
            SaveTransactionTrace trace,
            byte[] candidateBytes)
        {
            SaveCanonicalLedger ledger = CaptureCanonicalLedger();
            return MatchesExactNvs01MigrationCanonicalTarget(
                       ledger,
                       trace,
                       candidateBytes) &&
                   ledger.Previous.Disposition ==
                       SaveFileReadDisposition.Missing &&
                   IsExactNvs01MigrationBackupArchive(trace);
        }

        private bool MatchesExactNvs01MigrationCanonicalTarget(
            SaveCanonicalLedger ledger,
            SaveTransactionTrace trace,
            byte[] candidateBytes) =>
            ledger != null &&
            trace != null &&
            trace.BaselinePrimaryIsMigratable &&
            IsExactValidGeneration(ledger.Primary, candidateBytes, out _) &&
            MatchesExactBytes(
                ledger.Backup,
                trace.BaselinePrimaryBytes) &&
            ledger.Temp.Disposition == SaveFileReadDisposition.Missing;

        private bool IsExactNvs01MigrationBackupArchive(
            SaveTransactionTrace trace)
        {
            if (trace == null || !trace.BaselinePrimaryIsMigratable)
            {
                return true;
            }

            if (trace.BaselineBackup.Disposition ==
                SaveFileReadDisposition.Missing)
            {
                return trace.MigrationBackupArchivePath.Length == 0;
            }

            return trace.BaselineBackup.Disposition ==
                       SaveFileReadDisposition.Read &&
                   trace.MigrationBackupArchivePath.Length > 0 &&
                   MatchesExactState(
                       ReadCanonicalPath(
                           trace.MigrationBackupArchivePath),
                       trace.BaselineBackup);
        }

        private bool TryBuildInvalidPrimaryRecoveryPlan(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            SaveCandidateInventoryEntry primary,
            SaveCandidateInventoryEntry backup,
            SaveCandidateInventoryEntry previous,
            SaveSemanticCandidate selected,
            out InvalidPrimaryRecoveryPlan plan,
            out bool evidenceConflict)
        {
            plan = null;
            evidenceConflict = false;
            SaveCandidateInventoryEntry temp = Find(
                inventory,
                SaveCandidateSourceGeneration.Temp);
            SaveFileReadResult transactionMarkerRead =
                ReadCanonicalPath(StageFiveRecoveryMarkerPath);
            if (!TryGetExplicitCurrentWritableBytes(backup, out byte[] backupBytes))
            {
                evidenceConflict =
                    transactionMarkerRead.Disposition !=
                    SaveFileReadDisposition.Missing;
                return false;
            }

            string backupIdentity = ComputeSha256Base64Url(backupBytes);
            StageFiveTransactionMarker transactionMarker = null;
            if (transactionMarkerRead.Disposition !=
                    SaveFileReadDisposition.Missing &&
                (transactionMarkerRead.Disposition !=
                     SaveFileReadDisposition.Read ||
                 !TryParseStageFiveTransactionMarker(
                     transactionMarkerRead.Bytes,
                     out transactionMarker)))
            {
                evidenceConflict = true;
                return false;
            }

            bool backupSelected =
                selected != null &&
                ReferenceEquals(selected, backup.SemanticCandidate) &&
                selected.SourceGeneration == SaveCandidateSourceGeneration.Backup;
            bool primarySelected =
                selected != null &&
                ReferenceEquals(selected, primary.SemanticCandidate) &&
                selected.SourceGeneration == SaveCandidateSourceGeneration.Primary;
            bool primaryMissing =
                primary.ReadResult.Disposition == SaveFileReadDisposition.Missing;
            bool tempMissing =
                temp.ReadResult.Disposition == SaveFileReadDisposition.Missing;
            bool previousMissing =
                previous.ReadResult.Disposition == SaveFileReadDisposition.Missing;
            bool primaryIsBackup =
                TryGetExplicitCurrentWritableBytes(primary, out byte[] primaryBytes) &&
                BytesEqual(primaryBytes, backupBytes);
            bool tempIsBackup =
                TryGetExplicitCurrentWritableBytes(temp, out byte[] tempBytes) &&
                BytesEqual(tempBytes, backupBytes);
            bool primaryIsInvalid =
                TryGetStrictInvalidBytes(primary, out byte[] invalidPrimaryBytes);
            bool previousIsInvalid =
                TryGetStrictInvalidBytes(previous, out byte[] previousInvalidBytes);

            InvalidPrimaryRecoveryStage? stage = null;
            byte[] exactInvalidBytes = null;
            if (backupSelected &&
                primaryIsInvalid &&
                tempMissing &&
                previousMissing)
            {
                stage = InvalidPrimaryRecoveryStage.Initial;
                exactInvalidBytes = invalidPrimaryBytes;
            }
            else if (backupSelected &&
                     primaryIsInvalid &&
                     tempIsBackup &&
                     previousMissing)
            {
                stage = InvalidPrimaryRecoveryStage.BackupStaged;
                exactInvalidBytes = invalidPrimaryBytes;
            }
            else if (backupSelected &&
                     primaryMissing &&
                     tempIsBackup &&
                     previousIsInvalid)
            {
                stage = InvalidPrimaryRecoveryStage.PrimaryPreserved;
                exactInvalidBytes = previousInvalidBytes;
            }
            else if (primarySelected &&
                     primaryIsBackup &&
                     tempIsBackup &&
                     previousIsInvalid)
            {
                stage = InvalidPrimaryRecoveryStage.PrimaryInstalled;
                exactInvalidBytes = previousInvalidBytes;
            }
            else if (primarySelected &&
                     primaryIsBackup &&
                     tempIsBackup &&
                     previousMissing)
            {
                stage = InvalidPrimaryRecoveryStage.Quarantined;
            }

            if (!stage.HasValue)
            {
                if (transactionMarker != null)
                {
                    evidenceConflict = true;
                }

                return false;
            }

            if (!TryEnumerateStageFiveQuarantineMarkers(
                    out IReadOnlyList<StageFiveQuarantineMarker> markers,
                    out bool markerConflict))
            {
                evidenceConflict = markerConflict;
                return false;
            }

            if (transactionMarker == null)
            {
                // Only S0 may begin without a durable transaction marker.
                // S1-S3 without it may be Stage 4 residue and must remain idle.
                if (stage.Value != InvalidPrimaryRecoveryStage.Initial)
                {
                    return false;
                }

                string quarantinePath = CreateStageFiveQuarantinePath();
                byte[] transactionMarkerBytes =
                    CreateStageFiveTransactionMarkerBytes(
                        quarantinePath,
                        backupBytes,
                        exactInvalidBytes);
                plan = new InvalidPrimaryRecoveryPlan(
                    stage.Value,
                    backup.SemanticCandidate,
                    exactInvalidBytes,
                    backupBytes,
                    quarantinePath,
                    transactionMarkerBytes);
                return true;
            }

            if (!string.Equals(
                    transactionMarker.BackupIdentity,
                    backupIdentity,
                    StringComparison.Ordinal))
            {
                evidenceConflict = true;
                return false;
            }

            if (stage.Value == InvalidPrimaryRecoveryStage.Quarantined)
            {
                List<StageFiveQuarantineMarker> activeMatches = markers
                    .Where(marker => string.Equals(
                        marker.Path,
                        transactionMarker.QuarantinePath,
                        StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .ToList();
                if (activeMatches.Count != 1 ||
                    !TryReadExactInvalidPrimaryQuarantine(
                        activeMatches[0],
                        transactionMarker.InvalidPrimaryIdentity,
                        out exactInvalidBytes))
                {
                    evidenceConflict = true;
                    return false;
                }
            }

            string invalidPrimaryIdentity =
                ComputeSha256Base64Url(exactInvalidBytes);
            if (!string.Equals(
                    transactionMarker.InvalidPrimaryIdentity,
                    invalidPrimaryIdentity,
                    StringComparison.Ordinal) ||
                (stage.Value != InvalidPrimaryRecoveryStage.Quarantined &&
                 ReadCanonicalPath(
                          transactionMarker.QuarantinePath)
                      .Disposition != SaveFileReadDisposition.Missing))
            {
                evidenceConflict = true;
                return false;
            }

            plan = new InvalidPrimaryRecoveryPlan(
                stage.Value,
                backup.SemanticCandidate,
                exactInvalidBytes,
                backupBytes,
                transactionMarker.QuarantinePath,
                transactionMarker.Bytes);
            return true;
        }

        private bool TryGetStrictInvalidBytes(
            SaveCandidateInventoryEntry entry,
            out byte[] bytes)
        {
            bytes = null;
            SaveSemanticCandidate candidate = entry?.SemanticCandidate;
            if (entry == null ||
                entry.ReadResult.Disposition != SaveFileReadDisposition.Read ||
                candidate == null ||
                candidate.SourceGeneration != entry.Source ||
                candidate.Outcome != SaveSemanticCandidateOutcome.Invalid ||
                !candidate.HasRetainedRawBytes)
            {
                return false;
            }

            bytes = candidate.CopyRawBytes();
            return bytes != null &&
                   bytes.LongLength == entry.ReadResult.ObservedByteCount &&
                   bytes.Length == candidate.OriginalRawByteCount &&
                   bytes.Length <= _semanticPolicy.MaximumInputBytes;
        }

        private bool TryGetExplicitCurrentWritableBytes(
            SaveCandidateInventoryEntry entry,
            out byte[] bytes)
        {
            bytes = null;
            SaveSemanticCandidate candidate = entry?.SemanticCandidate;
            if (entry == null ||
                entry.ReadResult.Disposition != SaveFileReadDisposition.Read ||
                candidate == null ||
                candidate.SourceGeneration != entry.Source ||
                !IsExplicitCurrentWritableCandidate(candidate))
            {
                return false;
            }

            bytes = candidate.CopyRawBytes();
            return bytes != null &&
                   bytes.LongLength == entry.ReadResult.ObservedByteCount &&
                   bytes.Length == candidate.OriginalRawByteCount &&
                   bytes.Length <= _semanticPolicy.MaximumInputBytes;
        }

        private bool TryRecoverInvalidPrimaryFromExactBackup(
            InvalidPrimaryRecoveryPlan plan,
            out SaveGameData recoveredSave,
            out string quarantinePath,
            out bool diskChanged,
            out string message)
        {
            recoveredSave = null;
            quarantinePath = plan?.QuarantinePath;
            diskChanged = false;
            message = string.Empty;
            if (plan == null ||
                plan.InvalidPrimaryBytes == null ||
                plan.BackupBytes == null ||
                plan.TransactionMarkerBytes == null ||
                string.IsNullOrWhiteSpace(plan.QuarantinePath))
            {
                message =
                    "AL-SAVE-INVALID-PRIMARY-RECOVERY-EVIDENCE-MISSING: Exact invalid-primary or backup bytes were unavailable; no disk mutation was attempted.";
                return false;
            }

            if (!TryGetExactUtf8(
                    plan.BackupBytes,
                    out string exactBackupJson,
                    out string encodingError))
            {
                message =
                    $"AL-SAVE-INVALID-PRIMARY-RECOVERY-ENCODING-FAILED: The selected backup could not be staged without changing its bytes. {encodingError}";
                return false;
            }

            InvalidPrimaryRecoveryStage stage = plan.Stage;
            if (stage == InvalidPrimaryRecoveryStage.Quarantined)
            {
                if (TryVerifyInvalidPrimaryRecoveryTargetTwice(
                        plan.BackupBytes,
                        plan.InvalidPrimaryBytes,
                        quarantinePath,
                        plan.TransactionMarkerBytes,
                        out recoveredSave,
                        out bool observedCompleteTarget))
                {
                    message =
                        "AL-SAVE-RECOVERED-INVALID-PRIMARY-RECONCILED: The exact backup and its hash-linked invalid-primary quarantine were twice verified after an interrupted recovery without offline progression.";
                    return true;
                }

                message = observedCompleteTarget
                    ? "AL-SAVE-INVALID-PRIMARY-RECOVERY-REVERIFY-FAILED: The complete recovery target was observed once but changed before the required second inventory; every generation was preserved."
                    : "AL-SAVE-INVALID-PRIMARY-RECOVERY-TARGET-INVALID: The completed recovery marker did not prove exact canonical and quarantine identity; every generation was preserved.";
                return false;
            }

            SaveFileReadResult transactionMarkerRead =
                ReadCanonicalPath(StageFiveRecoveryMarkerPath);
            if (transactionMarkerRead.Disposition ==
                SaveFileReadDisposition.Missing)
            {
                if (stage != InvalidPrimaryRecoveryStage.Initial)
                {
                    message =
                        "AL-SAVE-INVALID-PRIMARY-MARKER-MISSING: A resumable state lacked its exact Stage 5 transaction marker.";
                    return false;
                }

                if (!TryGetExactUtf8(
                        plan.TransactionMarkerBytes,
                        out string transactionMarkerText,
                        out string transactionMarkerEncodingError))
                {
                    message =
                        $"AL-SAVE-INVALID-PRIMARY-MARKER-ENCODING-FAILED: The transaction marker could not be represented exactly. {transactionMarkerEncodingError}";
                    return false;
                }

                SaveFileWriteResult markerWrite;
                try
                {
                    _fileOperations.CreateDirectory(PersistencePath);
                    markerWrite =
                        _fileOperations.WriteAllTextDurable(
                            StageFiveRecoveryMarkerPath,
                            transactionMarkerText);
                }
                catch (Exception ex)
                {
                    message =
                        $"AL-SAVE-INVALID-PRIMARY-MARKER-CREATE-FAILED: The recovery directory or transaction marker could not be created; S0 evidence was preserved. {ex.GetType().Name}";
                    return false;
                }

                diskChanged |= markerWrite.DiskChanged;
                transactionMarkerRead =
                    ReadCanonicalPath(StageFiveRecoveryMarkerPath);
                if (transactionMarkerRead.Disposition !=
                        SaveFileReadDisposition.Read ||
                    !BytesEqual(
                        transactionMarkerRead.Bytes,
                        plan.TransactionMarkerBytes))
                {
                    message =
                        $"AL-SAVE-INVALID-PRIMARY-MARKER-WRITE-FAILED: The durable transaction marker did not reach its exact target; every artifact was preserved. {markerWrite.DiagnosticCode}";
                    return false;
                }

                diskChanged = true;
            }
            else if (transactionMarkerRead.Disposition !=
                         SaveFileReadDisposition.Read ||
                     !BytesEqual(
                         transactionMarkerRead.Bytes,
                         plan.TransactionMarkerBytes))
            {
                message =
                    "AL-SAVE-INVALID-PRIMARY-MARKER-CONFLICT: The durable transaction marker changed before recovery; every artifact was preserved.";
                return false;
            }

            SaveCanonicalLedger current = CaptureCanonicalLedger();
            if (!IsExactInvalidPrimaryRecoveryState(
                    current,
                    stage,
                    plan.InvalidPrimaryBytes,
                    plan.BackupBytes))
            {
                message =
                    "AL-SAVE-INVALID-PRIMARY-RECOVERY-BASELINE-CHANGED: Canonical evidence changed after selection; every observed generation was preserved.";
                return false;
            }

            if (stage == InvalidPrimaryRecoveryStage.Initial)
            {
                SaveFileWriteResult stageResult =
                    _fileOperations.WriteAllTextDurable(TempPath, exactBackupJson);
                diskChanged |= stageResult.DiskChanged;
                SaveCanonicalLedger afterStage = CaptureCanonicalLedger();
                if (!IsExactInvalidPrimaryRecoveryState(
                        afterStage,
                        InvalidPrimaryRecoveryStage.BackupStaged,
                        plan.InvalidPrimaryBytes,
                        plan.BackupBytes))
                {
                    message =
                        $"AL-SAVE-INVALID-PRIMARY-STAGE-WRITE-FAILED: Exact backup staging did not reach the only resumable target; all resulting evidence was preserved. {stageResult.DiagnosticCode}";
                    return false;
                }

                diskChanged = true;
                stage = InvalidPrimaryRecoveryStage.BackupStaged;
            }

            if (stage == InvalidPrimaryRecoveryStage.BackupStaged)
            {
                bool moveReturned = false;
                try
                {
                    _fileOperations.Move(SavePath, PreviousPath);
                    moveReturned = true;
                }
                catch (Exception ex)
                {
                    message =
                        $"AL-SAVE-INVALID-PRIMARY-PRESERVE-INTERRUPTED: Moving the invalid primary to the canonical previous witness stopped. {ex.GetType().Name}";
                }

                SaveCanonicalLedger afterPreserve = CaptureCanonicalLedger();
                if (!IsExactInvalidPrimaryRecoveryState(
                        afterPreserve,
                        InvalidPrimaryRecoveryStage.PrimaryPreserved,
                        plan.InvalidPrimaryBytes,
                        plan.BackupBytes))
                {
                    bool unchanged = IsExactInvalidPrimaryRecoveryState(
                        afterPreserve,
                        InvalidPrimaryRecoveryStage.BackupStaged,
                        plan.InvalidPrimaryBytes,
                        plan.BackupBytes);
                    diskChanged |= !unchanged;
                    if (moveReturned)
                    {
                        message =
                            "AL-SAVE-INVALID-PRIMARY-PRESERVE-VERIFY-FAILED: The preserve move returned without reaching its exact target; every generation was retained.";
                    }

                    return false;
                }

                diskChanged = true;
                stage = InvalidPrimaryRecoveryStage.PrimaryPreserved;
            }

            if (stage == InvalidPrimaryRecoveryStage.PrimaryPreserved)
            {
                SaveFileWriteResult installResult =
                    _fileOperations.WriteAllTextDurable(SavePath, exactBackupJson);
                diskChanged |= installResult.DiskChanged;
                SaveCanonicalLedger afterInstall = CaptureCanonicalLedger();
                if (!IsExactInvalidPrimaryRecoveryState(
                        afterInstall,
                        InvalidPrimaryRecoveryStage.PrimaryInstalled,
                        plan.InvalidPrimaryBytes,
                        plan.BackupBytes))
                {
                    message =
                        $"AL-SAVE-INVALID-PRIMARY-INSTALL-FAILED: Exact primary installation did not reach the only resumable target; all resulting evidence was preserved. {installResult.DiagnosticCode}";
                    return false;
                }

                diskChanged = true;
                stage = InvalidPrimaryRecoveryStage.PrimaryInstalled;
            }

            if (stage != InvalidPrimaryRecoveryStage.PrimaryInstalled)
            {
                message =
                    "AL-SAVE-INVALID-PRIMARY-RECOVERY-STATE-UNRECOGNIZED: The recovery state was not an approved resumable generation.";
                return false;
            }

            quarantinePath = plan.QuarantinePath;
            SaveFileReadResult quarantineBaseline = ReadCanonicalPath(quarantinePath);
            SaveCanonicalLedger beforeQuarantine = CaptureCanonicalLedger();
            if (quarantineBaseline.Disposition != SaveFileReadDisposition.Missing ||
                !IsExactInvalidPrimaryRecoveryState(
                    beforeQuarantine,
                    InvalidPrimaryRecoveryStage.PrimaryInstalled,
                    plan.InvalidPrimaryBytes,
                    plan.BackupBytes))
            {
                message =
                    "AL-SAVE-INVALID-PRIMARY-QUARANTINE-BASELINE-CONFLICT: The unique quarantine target or canonical ledger changed before the non-overwriting move.";
                return false;
            }

            bool quarantineMoveReturned = false;
            try
            {
                _fileOperations.Move(PreviousPath, quarantinePath);
                quarantineMoveReturned = true;
            }
            catch (Exception ex)
            {
                message =
                    $"AL-SAVE-INVALID-PRIMARY-QUARANTINE-INTERRUPTED: The non-overwriting quarantine move stopped; all resulting evidence was preserved. {ex.GetType().Name}";
            }

            bool reachedCompleteTarget = IsExactInvalidPrimaryRecoveryTarget(
                CaptureCanonicalLedger(),
                plan.BackupBytes,
                plan.InvalidPrimaryBytes,
                quarantinePath,
                plan.TransactionMarkerBytes,
                out _);
            if (!reachedCompleteTarget)
            {
                bool unchanged = IsExactInvalidPrimaryRecoveryState(
                    CaptureCanonicalLedger(),
                    InvalidPrimaryRecoveryStage.PrimaryInstalled,
                    plan.InvalidPrimaryBytes,
                    plan.BackupBytes);
                diskChanged |= !unchanged;
                if (quarantineMoveReturned)
                {
                    message =
                        "AL-SAVE-INVALID-PRIMARY-QUARANTINE-VERIFY-FAILED: The quarantine move returned without proving exact destination identity; every artifact was retained.";
                }

                return false;
            }

            diskChanged = true;
            if (TryVerifyInvalidPrimaryRecoveryTargetTwice(
                    plan.BackupBytes,
                    plan.InvalidPrimaryBytes,
                    quarantinePath,
                    plan.TransactionMarkerBytes,
                    out recoveredSave,
                    out bool observedFinalTarget))
            {
                message = quarantineMoveReturned
                    ? "AL-SAVE-RECOVERED-INVALID-PRIMARY: The exact backup was staged and installed, the invalid primary was hash-linked in quarantine, and the full target was twice verified without offline progression."
                    : "AL-SAVE-RECOVERED-INVALID-PRIMARY-RECONCILED: An interrupted quarantine move nevertheless reached and twice verified the exact recovery target without offline progression.";
                return true;
            }

            message = observedFinalTarget
                ? "AL-SAVE-INVALID-PRIMARY-RECOVERY-REVERIFY-FAILED: The exact recovery target was observed once but not proven by the required second inventory; every artifact was preserved."
                : "AL-SAVE-INVALID-PRIMARY-RECOVERY-VERIFY-FAILED: Exact canonical and quarantine identity could not be proven; every artifact was preserved.";
            recoveredSave = null;
            return false;
        }

        private bool IsExactInvalidPrimaryRecoveryState(
            SaveCanonicalLedger ledger,
            InvalidPrimaryRecoveryStage stage,
            byte[] invalidPrimaryBytes,
            byte[] backupBytes)
        {
            switch (stage)
            {
                case InvalidPrimaryRecoveryStage.Initial:
                    return IsExactInvalidGeneration(
                               ledger.Primary,
                               invalidPrimaryBytes,
                               SaveCandidateSourceGeneration.Primary) &&
                           IsExactExplicitCurrentWritableGeneration(
                               ledger.Backup,
                               backupBytes,
                               SaveCandidateSourceGeneration.Backup,
                               out _) &&
                           ledger.Temp.Disposition == SaveFileReadDisposition.Missing &&
                           ledger.Previous.Disposition == SaveFileReadDisposition.Missing;
                case InvalidPrimaryRecoveryStage.BackupStaged:
                    return IsExactInvalidGeneration(
                               ledger.Primary,
                               invalidPrimaryBytes,
                               SaveCandidateSourceGeneration.Primary) &&
                           IsExactExplicitCurrentWritableGeneration(
                               ledger.Backup,
                               backupBytes,
                               SaveCandidateSourceGeneration.Backup,
                               out _) &&
                           IsExactExplicitCurrentWritableGeneration(
                               ledger.Temp,
                               backupBytes,
                               SaveCandidateSourceGeneration.Temp,
                               out _) &&
                           ledger.Previous.Disposition == SaveFileReadDisposition.Missing;
                case InvalidPrimaryRecoveryStage.PrimaryPreserved:
                    return ledger.Primary.Disposition == SaveFileReadDisposition.Missing &&
                           IsExactExplicitCurrentWritableGeneration(
                               ledger.Backup,
                               backupBytes,
                               SaveCandidateSourceGeneration.Backup,
                               out _) &&
                           IsExactExplicitCurrentWritableGeneration(
                               ledger.Temp,
                               backupBytes,
                               SaveCandidateSourceGeneration.Temp,
                               out _) &&
                           IsExactInvalidGeneration(
                               ledger.Previous,
                               invalidPrimaryBytes,
                               SaveCandidateSourceGeneration.Previous);
                case InvalidPrimaryRecoveryStage.PrimaryInstalled:
                    return IsExactExplicitCurrentWritableGeneration(
                               ledger.Primary,
                               backupBytes,
                               SaveCandidateSourceGeneration.Primary,
                               out _) &&
                           IsExactExplicitCurrentWritableGeneration(
                               ledger.Backup,
                               backupBytes,
                               SaveCandidateSourceGeneration.Backup,
                               out _) &&
                           IsExactExplicitCurrentWritableGeneration(
                               ledger.Temp,
                               backupBytes,
                               SaveCandidateSourceGeneration.Temp,
                               out _) &&
                           IsExactInvalidGeneration(
                               ledger.Previous,
                               invalidPrimaryBytes,
                               SaveCandidateSourceGeneration.Previous);
                default:
                    return false;
            }
        }

        private bool TryVerifyInvalidPrimaryRecoveryTargetTwice(
            byte[] backupBytes,
            byte[] invalidPrimaryBytes,
            string quarantinePath,
            byte[] transactionMarkerBytes,
            out SaveGameData recoveredSave,
            out bool observedCompleteTarget)
        {
            SaveCanonicalLedger first = CaptureCanonicalLedger();
            observedCompleteTarget = IsExactInvalidPrimaryRecoveryTarget(
                first,
                backupBytes,
                invalidPrimaryBytes,
                quarantinePath,
                transactionMarkerBytes,
                out recoveredSave);
            if (!observedCompleteTarget)
            {
                recoveredSave = null;
                return false;
            }

            SaveCanonicalLedger second = CaptureCanonicalLedger();
            return IsExactInvalidPrimaryRecoveryTarget(
                second,
                backupBytes,
                invalidPrimaryBytes,
                quarantinePath,
                transactionMarkerBytes,
                out recoveredSave);
        }

        private bool IsExactInvalidPrimaryRecoveryTarget(
            SaveCanonicalLedger ledger,
            byte[] backupBytes,
            byte[] invalidPrimaryBytes,
            string quarantinePath,
            byte[] transactionMarkerBytes,
            out SaveGameData recoveredSave)
        {
            recoveredSave = null;
            if (string.IsNullOrWhiteSpace(quarantinePath) ||
                !IsExactExplicitCurrentWritableGeneration(
                    ledger.Primary,
                    backupBytes,
                    SaveCandidateSourceGeneration.Primary,
                    out recoveredSave) ||
                !IsExactExplicitCurrentWritableGeneration(
                    ledger.Backup,
                    backupBytes,
                    SaveCandidateSourceGeneration.Backup,
                    out _) ||
                !IsExactExplicitCurrentWritableGeneration(
                    ledger.Temp,
                    backupBytes,
                    SaveCandidateSourceGeneration.Temp,
                    out _) ||
                ledger.Previous.Disposition != SaveFileReadDisposition.Missing ||
                transactionMarkerBytes == null ||
                !MatchesExactBytes(
                    ReadCanonicalPath(StageFiveRecoveryMarkerPath),
                    transactionMarkerBytes))
            {
                recoveredSave = null;
                return false;
            }

            return IsExactInvalidGeneration(
                ReadCanonicalPath(quarantinePath),
                invalidPrimaryBytes,
                SaveCandidateSourceGeneration.Primary);
        }

        private bool IsExactInvalidGeneration(
            SaveFileReadResult actual,
            byte[] expectedBytes,
            SaveCandidateSourceGeneration source)
        {
            if (actual == null ||
                actual.Disposition != SaveFileReadDisposition.Read ||
                !BytesEqual(actual.Bytes, expectedBytes))
            {
                return false;
            }

            SaveSemanticCandidate candidate = ValidateSemanticCandidate(
                actual.Bytes,
                source);
            return candidate.Outcome == SaveSemanticCandidateOutcome.Invalid &&
                   candidate.HasRetainedRawBytes &&
                   candidate.OriginalRawByteCount == actual.Bytes.Length;
        }

        private bool IsExactExplicitCurrentWritableGeneration(
            SaveFileReadResult actual,
            byte[] expectedBytes,
            SaveCandidateSourceGeneration source,
            out SaveGameData save)
        {
            save = null;
            if (actual == null ||
                actual.Disposition != SaveFileReadDisposition.Read ||
                !BytesEqual(actual.Bytes, expectedBytes))
            {
                return false;
            }

            SaveSemanticCandidate candidate = ValidateSemanticCandidate(
                actual.Bytes,
                source);
            return IsExplicitCurrentWritableCandidate(candidate) &&
                   TryDeserializeSelectedCandidate(candidate, out save);
        }

        private bool TryEnumerateStageFiveQuarantineMarkers(
            out IReadOnlyList<StageFiveQuarantineMarker> markers,
            out bool evidenceConflict)
        {
            var result = new List<StageFiveQuarantineMarker>();
            evidenceConflict = false;
            try
            {
                string persistenceRoot = Path.GetFullPath(PersistencePath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (string path in EnumerateQuarantines(SaveFileName))
                {
                    string fileName = Path.GetFileName(path);
                    if (fileName == null ||
                        fileName.EndsWith(
                            ".txn",
                            StringComparison.OrdinalIgnoreCase) ||
                        fileName.IndexOf("-stage5-", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (result.Count >= MaxStageFiveQuarantineMarkers)
                    {
                        evidenceConflict = true;
                        markers = Array.Empty<StageFiveQuarantineMarker>();
                        return false;
                    }

                    string fullPath = Path.GetFullPath(path);
                    string directory = Path.GetDirectoryName(fullPath)?
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!string.Equals(
                            directory,
                            persistenceRoot,
                            StringComparison.OrdinalIgnoreCase) ||
                        !TryParseStageFiveQuarantineMarker(
                            fullPath,
                            out StageFiveQuarantineMarker marker))
                    {
                        evidenceConflict = true;
                        markers = Array.Empty<StageFiveQuarantineMarker>();
                        return false;
                    }

                    result.Add(marker);
                }
            }
            catch (Exception)
            {
                evidenceConflict = true;
                markers = Array.Empty<StageFiveQuarantineMarker>();
                return false;
            }

            markers = result;
            return true;
        }

        private bool TryReadExactInvalidPrimaryQuarantine(
            StageFiveQuarantineMarker marker,
            string expectedInvalidPrimaryIdentity,
            out byte[] invalidPrimaryBytes)
        {
            invalidPrimaryBytes = null;
            SaveFileReadResult result = ReadCanonicalPath(marker.Path);
            if (result.Disposition != SaveFileReadDisposition.Read ||
                !string.Equals(
                    ComputeSha256Base64Url(result.Bytes),
                    expectedInvalidPrimaryIdentity,
                    StringComparison.Ordinal) ||
                !IsExactInvalidGeneration(
                    result,
                    result.Bytes,
                    SaveCandidateSourceGeneration.Primary))
            {
                return false;
            }

            invalidPrimaryBytes = result.Bytes.ToArray();
            return true;
        }

        private static bool TryParseStageFiveQuarantineMarker(
            string path,
            out StageFiveQuarantineMarker marker)
        {
            marker = null;
            string fileName = Path.GetFileName(path);
            string prefix = SaveFileName + ".corrupt-";
            const string stageTag = "-stage5-t";
            if (fileName == null ||
                fileName.EndsWith(".txn", StringComparison.OrdinalIgnoreCase) ||
                !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string remainder = fileName.Substring(prefix.Length);
            int expectedLength =
                14 +
                stageTag.Length +
                TransactionIdBase64UrlLength;
            if (remainder.Length != expectedLength ||
                !remainder.Substring(0, 14).All(char.IsDigit) ||
                !string.Equals(
                    remainder.Substring(14, stageTag.Length),
                    stageTag,
                    StringComparison.Ordinal))
            {
                return false;
            }

            int cursor = 14 + stageTag.Length;
            string transactionIdentity =
                remainder.Substring(cursor, TransactionIdBase64UrlLength);
            if (!IsBase64UrlIdentity(
                    transactionIdentity,
                    TransactionIdBase64UrlLength))
            {
                return false;
            }

            marker = new StageFiveQuarantineMarker(path);
            return true;
        }

        private bool TryParseStageFiveTransactionMarker(
            byte[] bytes,
            out StageFiveTransactionMarker marker)
        {
            try
            {
                return TryParseStageFiveTransactionMarkerCore(
                    bytes,
                    out marker);
            }
            catch (Exception)
            {
                marker = null;
                return false;
            }
        }

        private bool TryParseStageFiveTransactionMarkerCore(
            byte[] bytes,
            out StageFiveTransactionMarker marker)
        {
            marker = null;
            if (!TryGetExactUtf8(bytes, out string text, out _))
            {
                return false;
            }

            string[] fields = text.Split('|');
            if (fields.Length != 5 ||
                fields[0] != "AL-STAGE5" ||
                fields[1] != "1" ||
                !IsBase64UrlIdentity(
                    fields[2],
                    Sha256Base64UrlLength) ||
                !IsBase64UrlIdentity(
                    fields[3],
                    Sha256Base64UrlLength) ||
                string.IsNullOrWhiteSpace(fields[4]) ||
                fields[4] != Path.GetFileName(fields[4]))
            {
                return false;
            }

            string quarantinePath = Path.Combine(PersistencePath, fields[4]);
            string persistenceRoot = Path.GetFullPath(PersistencePath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            string fullQuarantinePath = Path.GetFullPath(quarantinePath);
            string quarantineDirectory = Path.GetDirectoryName(
                    fullQuarantinePath)?
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            if (!string.Equals(
                    persistenceRoot,
                    quarantineDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                !TryParseStageFiveQuarantineMarker(
                    fullQuarantinePath,
                    out _))
            {
                return false;
            }

            marker = new StageFiveTransactionMarker(
                bytes.ToArray(),
                fullQuarantinePath,
                fields[2],
                fields[3]);
            return true;
        }

        private static byte[] CreateStageFiveTransactionMarkerBytes(
            string quarantinePath,
            byte[] backupBytes,
            byte[] invalidPrimaryBytes)
        {
            string contents =
                $"AL-STAGE5|1|{ComputeSha256Base64Url(backupBytes)}|" +
                $"{ComputeSha256Base64Url(invalidPrimaryBytes)}|" +
                Path.GetFileName(quarantinePath);
            return StrictUtf8.GetBytes(contents);
        }

        private string CreateStageFiveQuarantinePath()
        {
            string transactionIdentity =
                ToBase64Url(Guid.NewGuid().ToByteArray());
            string fileName =
                $"{SaveFileName}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}" +
                $"-stage5-t{transactionIdentity}";
            return Path.Combine(PersistencePath, fileName);
        }

        private static string ComputeSha256Base64Url(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return ToBase64Url(sha256.ComputeHash(bytes ?? Array.Empty<byte>()));
        }

        private static string ToBase64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private static bool IsBase64UrlIdentity(string value, int expectedLength) =>
            value != null &&
            value.Length == expectedLength &&
            value.All(character =>
                (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '-' ||
                character == '_');

        private static bool TryGetExactUtf8(
            byte[] bytes,
            out string exactText,
            out string error)
        {
            exactText = null;
            error = string.Empty;
            try
            {
                exactText = StrictUtf8.GetString(bytes);
                if (!BytesEqual(StrictUtf8.GetBytes(exactText), bytes))
                {
                    error = "SAVE_EXACT_UTF8_ROUND_TRIP_FAILED";
                    exactText = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (
                ex is DecoderFallbackException ||
                ex is EncoderFallbackException)
            {
                error = ex.GetType().Name;
                exactText = null;
                return false;
            }
        }

        private bool CanAttemptExactBackupRecovery(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            SaveCandidateInventoryEntry primary,
            SaveCandidateInventoryEntry backup,
            SaveCandidateInventoryEntry previous,
            SaveSemanticCandidate selected)
        {
            SaveCandidateInventoryEntry temp = Find(
                inventory,
                SaveCandidateSourceGeneration.Temp);
            return selected != null &&
                   ReferenceEquals(selected, backup.SemanticCandidate) &&
                   selected.SourceGeneration == SaveCandidateSourceGeneration.Backup &&
                   selected.HasRetainedRawBytes &&
                   selected.HasExplicitSaveSchemaVersion &&
                   selected.SaveSchemaVersion == _semanticPolicy.CurrentSaveSchemaVersion &&
                   selected.HasExplicitProfileInitializationVersion &&
                   selected.ProfileInitializationVersion ==
                       _semanticPolicy.CurrentProfileInitializationVersion &&
                   selected.IsWritable &&
                   IsRuntimeRoundTrippable(selected) &&
                   primary.ReadResult.Disposition == SaveFileReadDisposition.Missing &&
                   backup.ReadResult.Disposition == SaveFileReadDisposition.Read &&
                   temp.ReadResult.Disposition == SaveFileReadDisposition.Missing &&
                   previous.ReadResult.Disposition == SaveFileReadDisposition.Missing;
        }

        private bool TryRecoverMissingPrimaryFromExactBackup(
            SaveSemanticCandidate selectedBackup,
            out SaveGameData recoveredSave,
            out bool diskChanged,
            out string message)
        {
            recoveredSave = null;
            diskChanged = false;
            message = string.Empty;
            byte[] backupBytes = selectedBackup?.CopyRawBytes();
            if (backupBytes == null)
            {
                message =
                    "AL-SAVE-BACKUP-RECOVERY-EVIDENCE-MISSING: Exact selected backup bytes were unavailable; no disk mutation was attempted.";
                return false;
            }

            string exactJson;
            try
            {
                exactJson = StrictUtf8.GetString(backupBytes);
                if (!BytesEqual(StrictUtf8.GetBytes(exactJson), backupBytes))
                {
                    message =
                        "AL-SAVE-BACKUP-RECOVERY-ENCODING-MISMATCH: The selected backup could not be represented as exact UTF-8 text; no disk mutation was attempted.";
                    return false;
                }
            }
            catch (Exception ex) when (
                ex is DecoderFallbackException ||
                ex is EncoderFallbackException)
            {
                message =
                    $"AL-SAVE-BACKUP-RECOVERY-ENCODING-FAILED: The selected backup could not be staged without changing its bytes. {ex.GetType().Name}";
                return false;
            }

            SaveCanonicalLedger baseline = CaptureCanonicalLedger();
            if (!IsExactBackupRecoveryBaseline(baseline, backupBytes))
            {
                message =
                    "AL-SAVE-BACKUP-RECOVERY-BASELINE-CHANGED: Canonical evidence changed after selection; every observed generation was preserved.";
                return false;
            }

            bool installReturned = false;
            try
            {
                _fileOperations.CreateDirectory(PersistencePath);
                SaveFileWriteResult writeResult =
                    _fileOperations.WriteAllTextDurable(TempPath, exactJson);
                diskChanged |= writeResult.DiskChanged;
                if (!writeResult.Succeeded)
                {
                    message =
                        $"AL-SAVE-BACKUP-RECOVERY-STAGE-WRITE-FAILED: Exact backup staging did not complete durably; all resulting evidence was preserved. {writeResult.DiagnosticCode}";
                    return false;
                }

                SaveCanonicalLedger staged = CaptureCanonicalLedger();
                if (!IsExactBackupRecoveryStagedState(staged, backupBytes))
                {
                    message =
                        "AL-SAVE-BACKUP-RECOVERY-STAGE-VERIFY-FAILED: The durable stage or canonical authority changed before install; all resulting evidence was preserved.";
                    return false;
                }

                SaveFileWriteResult installResult =
                    _fileOperations.WriteAllTextDurable(SavePath, exactJson);
                diskChanged |= installResult.DiskChanged;
                installReturned = installResult.Succeeded;
                if (!installReturned)
                {
                    message =
                        $"AL-SAVE-BACKUP-RECOVERY-PRIMARY-WRITE-FAILED: Exact primary installation did not complete durably; the recovery witness and every resulting generation were preserved. {installResult.DiagnosticCode}";
                }
            }
            catch (Exception ex)
            {
                message =
                    $"AL-SAVE-BACKUP-RECOVERY-INSTALL-INTERRUPTED: Exact backup installation stopped; all resulting evidence was preserved for bounded reconciliation. {ex.GetType().Name}";
            }

            bool observedCompleteTarget;
            if (TryVerifyExactBackupRecoveryTargetTwice(
                    backupBytes,
                    out recoveredSave,
                    out observedCompleteTarget))
            {
                message = installReturned
                    ? "AL-SAVE-RECOVERED-BACKUP: The exact current-format backup was durably staged, installed as primary, and twice verified with its recovery witness without offline progression."
                    : "AL-SAVE-RECOVERED-BACKUP-RECONCILED: An interrupted install nevertheless reached and twice verified the exact recovery target without offline progression.";
                return true;
            }

            if (observedCompleteTarget)
            {
                message =
                    "AL-SAVE-BACKUP-RECOVERY-REVERIFY-FAILED: The exact recovery target was observed once but not proven by the required second bounded inventory; all evidence was preserved.";
            }
            else if (installReturned)
            {
                message =
                    "AL-SAVE-BACKUP-RECOVERY-VERIFY-FAILED: The install operation returned, but exact primary, backup, and cleanup identity could not be proven; all evidence was preserved.";
            }

            recoveredSave = null;
            return false;
        }

        private bool IsExactBackupRecoveryBaseline(
            SaveCanonicalLedger ledger,
            byte[] backupBytes) =>
            ledger.Primary.Disposition == SaveFileReadDisposition.Missing &&
            IsExactValidGeneration(ledger.Backup, backupBytes, out _) &&
            ledger.Temp.Disposition == SaveFileReadDisposition.Missing &&
            ledger.Previous.Disposition == SaveFileReadDisposition.Missing;

        private bool IsExactBackupRecoveryStagedState(
            SaveCanonicalLedger ledger,
            byte[] backupBytes) =>
            ledger.Primary.Disposition == SaveFileReadDisposition.Missing &&
            IsExactValidGeneration(ledger.Backup, backupBytes, out _) &&
            IsExactValidGeneration(ledger.Temp, backupBytes, out _) &&
            ledger.Previous.Disposition == SaveFileReadDisposition.Missing;

        private bool TryVerifyExactBackupRecoveryTargetTwice(
            byte[] backupBytes,
            out SaveGameData recoveredSave,
            out bool observedCompleteTarget)
        {
            SaveCanonicalLedger finalLedger = CaptureCanonicalLedger();
            observedCompleteTarget = IsExactBackupRecoveryTarget(
                finalLedger,
                backupBytes,
                out recoveredSave);
            if (!observedCompleteTarget)
            {
                recoveredSave = null;
                return false;
            }

            SaveCanonicalLedger verification = CaptureCanonicalLedger();
            return IsExactBackupRecoveryTarget(
                verification,
                backupBytes,
                out recoveredSave);
        }

        private bool TryVerifyCommittedRecoveryWitnessTargetTwice(
            byte[] backupBytes,
            out SaveGameData recoveredSave,
            out bool observedCompleteTarget)
        {
            if (_committedInvalidPrimaryWitnessBytes == null &&
                string.IsNullOrWhiteSpace(
                    _committedInvalidPrimaryQuarantinePath) &&
                _committedInvalidPrimaryRecoveryMarkerBytes == null)
            {
                return TryVerifyExactBackupRecoveryTargetTwice(
                    backupBytes,
                    out recoveredSave,
                    out observedCompleteTarget);
            }

            if (_committedInvalidPrimaryWitnessBytes == null ||
                string.IsNullOrWhiteSpace(
                    _committedInvalidPrimaryQuarantinePath) ||
                _committedInvalidPrimaryRecoveryMarkerBytes == null)
            {
                recoveredSave = null;
                observedCompleteTarget = false;
                return false;
            }

            return TryVerifyInvalidPrimaryRecoveryTargetTwice(
                backupBytes,
                _committedInvalidPrimaryWitnessBytes,
                _committedInvalidPrimaryQuarantinePath,
                _committedInvalidPrimaryRecoveryMarkerBytes,
                out recoveredSave,
                out observedCompleteTarget);
        }

        private bool IsCommittedRecoveryWitnessTarget(
            SaveCanonicalLedger ledger,
            byte[] backupBytes,
            out SaveGameData recoveredSave)
        {
            if (_committedInvalidPrimaryWitnessBytes == null &&
                string.IsNullOrWhiteSpace(
                    _committedInvalidPrimaryQuarantinePath) &&
                _committedInvalidPrimaryRecoveryMarkerBytes == null)
            {
                return IsExactBackupRecoveryTarget(
                    ledger,
                    backupBytes,
                    out recoveredSave);
            }

            if (_committedInvalidPrimaryWitnessBytes == null ||
                string.IsNullOrWhiteSpace(
                    _committedInvalidPrimaryQuarantinePath) ||
                _committedInvalidPrimaryRecoveryMarkerBytes == null)
            {
                recoveredSave = null;
                return false;
            }

            return IsExactInvalidPrimaryRecoveryTarget(
                ledger,
                backupBytes,
                _committedInvalidPrimaryWitnessBytes,
                _committedInvalidPrimaryQuarantinePath,
                _committedInvalidPrimaryRecoveryMarkerBytes,
                out recoveredSave);
        }

        private bool IsCommittedInvalidPrimaryQuarantineIntact()
        {
            bool hasBytes = _committedInvalidPrimaryWitnessBytes != null;
            bool hasPath = !string.IsNullOrWhiteSpace(
                _committedInvalidPrimaryQuarantinePath);
            if (!hasBytes && !hasPath)
            {
                return true;
            }

            return hasBytes &&
                   hasPath &&
                   IsExactInvalidGeneration(
                       ReadCanonicalPath(
                           _committedInvalidPrimaryQuarantinePath),
                       _committedInvalidPrimaryWitnessBytes,
                       SaveCandidateSourceGeneration.Primary);
        }

        private bool TryArchiveCommittedInvalidPrimaryRecoveryMarker()
        {
            if (_committedInvalidPrimaryRecoveryMarkerBytes == null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(
                    _committedInvalidPrimaryRecoveryMarkerPath) ||
                string.IsNullOrWhiteSpace(
                    _committedInvalidPrimaryQuarantinePath))
            {
                return false;
            }

            string sourcePath =
                _committedInvalidPrimaryRecoveryMarkerPath;
            string archivePath =
                _committedInvalidPrimaryQuarantinePath + ".txn";
            SaveFileReadResult sourceBefore =
                ReadCanonicalPath(sourcePath);
            SaveFileReadResult archiveBefore =
                ReadCanonicalPath(archivePath);
            if (!MatchesExactBytes(
                    sourceBefore,
                    _committedInvalidPrimaryRecoveryMarkerBytes) ||
                archiveBefore.Disposition !=
                    SaveFileReadDisposition.Missing)
            {
                return false;
            }

            try
            {
                _fileOperations.Move(sourcePath, archivePath);
            }
            catch (Exception)
            {
                // Reconcile below. A move that threw after mutation may still
                // have preserved the exact marker at the archive path.
            }

            SaveFileReadResult sourceAfter =
                ReadCanonicalPath(sourcePath);
            SaveFileReadResult archiveAfter =
                ReadCanonicalPath(archivePath);
            if (sourceAfter.Disposition ==
                    SaveFileReadDisposition.Missing &&
                MatchesExactBytes(
                    archiveAfter,
                    _committedInvalidPrimaryRecoveryMarkerBytes))
            {
                _committedInvalidPrimaryRecoveryMarkerPath =
                    archivePath;
                return true;
            }

            return false;
        }

        private bool IsCommittedRecoveryMarkerCleanupVerified() =>
            _committedInvalidPrimaryRecoveryMarkerBytes == null ||
            (!string.IsNullOrWhiteSpace(
                 _committedInvalidPrimaryRecoveryMarkerPath) &&
             _committedInvalidPrimaryRecoveryMarkerPath.EndsWith(
                 ".txn",
                 StringComparison.OrdinalIgnoreCase) &&
             MatchesExactBytes(
                 ReadCanonicalPath(
                     _committedInvalidPrimaryRecoveryMarkerPath),
                 _committedInvalidPrimaryRecoveryMarkerBytes) &&
             ReadCanonicalPath(StageFiveRecoveryMarkerPath).Disposition ==
                 SaveFileReadDisposition.Missing);

        private bool IsExactBackupRecoveryTarget(
            SaveCanonicalLedger ledger,
            byte[] backupBytes,
            out SaveGameData recoveredSave)
        {
            bool primaryVerified = IsExactValidGeneration(
                ledger.Primary,
                backupBytes,
                out recoveredSave);
            return primaryVerified &&
                   IsExactValidGeneration(ledger.Backup, backupBytes, out _) &&
                   IsExactValidGeneration(ledger.Temp, backupBytes, out _) &&
                   ledger.Previous.Disposition == SaveFileReadDisposition.Missing;
        }

        private static bool IsStableBoundedState(SaveFileReadResult result) =>
            result != null &&
            (result.Disposition == SaveFileReadDisposition.Read ||
             result.Disposition == SaveFileReadDisposition.Missing);

        private static bool MatchesExactState(
            SaveFileReadResult actual,
            SaveFileReadResult expected)
        {
            if (actual == null || expected == null ||
                actual.Disposition != expected.Disposition)
            {
                return false;
            }

            if (expected.Disposition == SaveFileReadDisposition.Missing)
            {
                return true;
            }

            return expected.Disposition == SaveFileReadDisposition.Read &&
                   BytesEqual(actual.Bytes, expected.Bytes);
        }

        private bool IsExactValidGeneration(
            SaveFileReadResult actual,
            byte[] expectedBytes,
            out SaveGameData save)
        {
            save = null;
            return actual != null &&
                   actual.Disposition == SaveFileReadDisposition.Read &&
                   BytesEqual(actual.Bytes, expectedBytes) &&
                   TryDeserializeValidSaveBytes(actual.Bytes, out save, out _);
        }

        private bool IsExactTraceBaselineGeneration(
            SaveFileReadResult actual,
            SaveTransactionTrace trace)
        {
            if (trace == null || trace.BaselinePrimaryBytes == null)
            {
                return false;
            }

            return trace.BaselinePrimaryIsMigratable
                ? MatchesExactBytes(actual, trace.BaselinePrimaryBytes)
                : IsExactValidGeneration(
                    actual,
                    trace.BaselinePrimaryBytes,
                    out _);
        }

        private bool IsExactRequiredBackupGeneration(
            SaveFileReadResult actual,
            byte[] expectedBytes,
            bool allowMigratable,
            out SaveGameData save)
        {
            save = null;
            return allowMigratable
                ? MatchesExactBytes(actual, expectedBytes)
                : IsExactValidGeneration(actual, expectedBytes, out save);
        }

        private bool VerifyCommitTargetAgain(
            byte[] candidateBytes,
            byte[] requiredBackupBytes,
            bool requiredBackupIsMigratable,
            SaveTransactionTrace trace,
            out SaveGameData persistedSave)
        {
            SaveCanonicalLedger verification = CaptureCanonicalLedger();
            bool primaryVerified = IsExactValidGeneration(
                verification.Primary,
                candidateBytes,
                out persistedSave);
            return primaryVerified &&
                   IsExactRequiredBackupGeneration(
                       verification.Backup,
                       requiredBackupBytes,
                       requiredBackupIsMigratable,
                       out _) &&
                   verification.Temp.Disposition == SaveFileReadDisposition.Missing &&
                   verification.Previous.Disposition == SaveFileReadDisposition.Missing &&
                   IsExactNvs01MigrationBackupArchive(trace) &&
                   IsCommittedRecoveryMarkerCleanupVerified() &&
                   IsCommittedInvalidPrimaryQuarantineIntact();
        }

        private static bool BytesEqual(byte[] left, byte[] right) =>
            ReferenceEquals(left, right) ||
            (left != null && right != null && left.SequenceEqual(right));

        private static bool MatchesExactBytes(
            SaveFileReadResult actual,
            byte[] expectedBytes) =>
            actual != null &&
            actual.Disposition == SaveFileReadDisposition.Read &&
            BytesEqual(actual.Bytes, expectedBytes);

        private static SaveOperationDisposition CreateSaveDisposition(
            SaveOperationStatus status,
            bool mayHaveMutated,
            bool candidatePrimaryVerified,
            bool requiredBackupVerified,
            bool previousAuthorityVerified,
            bool cleanupVerified,
            bool rollbackAttempted,
            bool rollbackVerified,
            string message)
        {
            string diagnosticCode = ExtractDiagnosticCode(message);
            return new SaveOperationDisposition(
                status,
                mayHaveMutated,
                candidatePrimaryVerified,
                requiredBackupVerified,
                previousAuthorityVerified,
                cleanupVerified,
                rollbackAttempted,
                rollbackVerified,
                string.IsNullOrWhiteSpace(diagnosticCode)
                    ? Array.Empty<string>()
                    : new[] { diagnosticCode });
        }

        private static SaveOperationDisposition CreateSaveDisposition(
            SaveOperationStatus status,
            bool mayHaveMutated,
            bool candidatePrimaryVerified,
            bool requiredBackupVerified,
            bool previousAuthorityVerified,
            bool cleanupVerified,
            string message) =>
            CreateSaveDisposition(
                status,
                mayHaveMutated,
                candidatePrimaryVerified,
                requiredBackupVerified,
                previousAuthorityVerified,
                cleanupVerified,
                false,
                false,
                message);

        private static string ExtractDiagnosticCode(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            int delimiter = message.IndexOf(':');
            string code = delimiter < 0 ? message : message.Substring(0, delimiter);
            return new string(code
                .Where(character =>
                    char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_')
                .Take(128)
                .ToArray());
        }

        private bool TryInstallWithAtomicReplace(
            SaveTransactionTrace trace,
            out string message)
        {
            bool primaryInstalled = false;
            try
            {
                if (_fileOperations.FileExists(PreviousPath))
                {
                    message = "AL-SAVE-PREVIOUS-APPEARED: Previous-generation evidence appeared after candidate staging; no atomic install was attempted.";
                    return false;
                }

                _fileOperations.Replace(TempPath, SavePath, PreviousPath);
                primaryInstalled = true;

                if (!TryReadValidSave(SavePath, out _, out string installedError))
                {
                    TryRestorePreviousPrimary(trace);
                    message = $"AL-SAVE-ATOMIC-INSTALL-INVALID: Atomic-installed primary failed validation; rollback certainty was recorded for final reconciliation. {installedError}";
                    return false;
                }

                return TryRotatePreviousIntoBackup(true, trace, out message);
            }
            catch (PlatformNotSupportedException) when (!primaryInstalled)
            {
                if (CanSafelyUseMoveFallback(trace))
                {
                    return TryInstallWithMoveFallback(trace, out message);
                }

                message = "AL-SAVE-REPLACE-UNSUPPORTED-UNCERTAIN: Atomic replace reported unsupported after changing the canonical ledger; fallback was not attempted.";
                return false;
            }
            catch (NotSupportedException) when (!primaryInstalled)
            {
                if (CanSafelyUseMoveFallback(trace))
                {
                    return TryInstallWithMoveFallback(trace, out message);
                }

                message = "AL-SAVE-REPLACE-UNSUPPORTED-UNCERTAIN: Atomic replace reported unsupported after changing the canonical ledger; fallback was not attempted.";
                return false;
            }
            catch (Exception ex)
            {
                if (!primaryInstalled)
                {
                    TryRestorePreviousPrimary(trace);
                }

                message = primaryInstalled
                    ? $"AL-SAVE-BACKUP-ROTATION-FAILED: The validated primary was installed, but backup rotation stopped and all remaining generations were preserved. {ex.Message}"
                    : $"AL-SAVE-REPLACE-FAILED: Atomic replace failed without using destructive fallback; previous save was preserved or restored. {ex.Message}";
                return false;
            }
        }

        private bool TryInstallWithMoveFallback(
            SaveTransactionTrace trace,
            out string message)
        {
            try
            {
                if (_fileOperations.FileExists(PreviousPath))
                {
                    message = "AL-SAVE-PREVIOUS-APPEARED: Previous-generation evidence appeared after candidate staging; no move fallback was attempted.";
                    return false;
                }

                _fileOperations.Move(SavePath, PreviousPath);
                _fileOperations.Move(TempPath, SavePath);

                if (!TryReadValidSave(SavePath, out _, out string installedError))
                {
                    TryRestorePreviousPrimary(trace);
                    message = $"AL-SAVE-FALLBACK-INSTALL-INVALID: Fallback-installed primary failed validation; rollback certainty was recorded for final reconciliation. {installedError}";
                    return false;
                }

                return TryRotatePreviousIntoBackup(false, trace, out message);
            }
            catch (Exception ex)
            {
                if (!_fileOperations.FileExists(SavePath) &&
                    _fileOperations.FileExists(PreviousPath))
                {
                    TryRestorePreviousPrimary(trace);
                }

                message = $"AL-SAVE-FALLBACK-FAILED: Fallback install stopped and every remaining generation was preserved. {ex.Message}";
                return false;
            }
        }

        private bool TryRotatePreviousIntoBackup(
            bool useAtomicReplace,
            SaveTransactionTrace trace,
            out string message)
        {
            try
            {
                if (trace == null || trace.BaselinePrimaryBytes == null)
                {
                    message = "AL-SAVE-BACKUP-ROTATION-BASELINE-MISSING: Exact prior-primary identity was unavailable; no backup mutation was attempted.";
                    return false;
                }

                SaveFileReadResult priorPrimary = ReadCanonicalPath(PreviousPath);
                if (!IsExactTraceBaselineGeneration(
                        priorPrimary,
                        trace))
                {
                    message = "AL-SAVE-BACKUP-ROTATION-EVIDENCE-MISSING: The exact prior-primary generation was missing or changed; the installed primary and remaining evidence were preserved.";
                    return false;
                }

                _fileOperations.Copy(PreviousPath, TempPath, false);
                SaveFileReadResult staged = ReadCanonicalPath(TempPath);
                if (!IsExactTraceBaselineGeneration(staged, trace))
                {
                    message = "AL-SAVE-BACKUP-STAGE-INVALID: The staged prior-primary copy was not the exact bounded P0 generation, so the authentic source and existing backup were preserved.";
                    return false;
                }

                priorPrimary = ReadCanonicalPath(PreviousPath);
                if (!IsExactTraceBaselineGeneration(
                        priorPrimary,
                        trace))
                {
                    message = "AL-SAVE-BACKUP-STAGE-SOURCE-CHANGED: The authentic prior-primary source changed after staging and was preserved; backup rotation stopped.";
                    return false;
                }

                if (!TryDelete(PreviousPath))
                {
                    message = "AL-SAVE-BACKUP-STAGE-CLEANUP-FAILED: The prior-primary source could not be released before bounded backup rotation; the existing backup was preserved.";
                    return false;
                }

                if (!_fileOperations.FileExists(BackupPath))
                {
                    return TryInstallStagedBackupWithoutExisting(
                        trace,
                        out message);
                }

                if (useAtomicReplace)
                {
                    try
                    {
                        _fileOperations.Replace(TempPath, BackupPath, PreviousPath);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        if (CanSafelyUseBackupMoveFallback(trace))
                        {
                            return TryRotateStagedBackupWithMoves(
                                trace,
                                out message);
                        }

                        message = "AL-SAVE-BACKUP-REPLACE-UNSUPPORTED-UNCERTAIN: Backup replace reported unsupported after changing the canonical ledger; fallback was not attempted.";
                        return false;
                    }
                    catch (NotSupportedException)
                    {
                        if (CanSafelyUseBackupMoveFallback(trace))
                        {
                            return TryRotateStagedBackupWithMoves(
                                trace,
                                out message);
                        }

                        message = "AL-SAVE-BACKUP-REPLACE-UNSUPPORTED-UNCERTAIN: Backup replace reported unsupported after changing the canonical ledger; fallback was not attempted.";
                        return false;
                    }
                }
                else
                {
                    return TryRotateStagedBackupWithMoves(trace, out message);
                }

                if (!IsExactTraceBaselineGeneration(
                        ReadCanonicalPath(BackupPath),
                        trace))
                {
                    message = "AL-SAVE-BACKUP-INSTALL-UNCERTAIN: The rotated backup could not be verified; both it and the prior backup were preserved.";
                    return false;
                }

                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                message = $"AL-SAVE-BACKUP-ROTATION-FAILED: Every generation left by the atomic operation was preserved for explicit recovery. {ex.Message}";
                return false;
            }
        }

        private bool TryInstallStagedBackupWithoutExisting(
            SaveTransactionTrace trace,
            out string message)
        {
            try
            {
                _fileOperations.Move(TempPath, BackupPath);
                if (!IsExactTraceBaselineGeneration(
                        ReadCanonicalPath(BackupPath),
                        trace))
                {
                    message = "AL-SAVE-BACKUP-RECREATE-INVALID: The recreated backup could not be verified; its staged bytes were preserved.";
                    return false;
                }

                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                message = $"AL-SAVE-BACKUP-RECREATE-FAILED: The installed primary and every remaining generation were preserved. {ex.Message}";
                return false;
            }
        }

        private bool TryRotateStagedBackupWithMoves(
            SaveTransactionTrace trace,
            out string message)
        {
            try
            {
                _fileOperations.Move(BackupPath, PreviousPath);
                _fileOperations.Move(TempPath, BackupPath);

                if (!IsExactTraceBaselineGeneration(
                        ReadCanonicalPath(BackupPath),
                        trace))
                {
                    message = "AL-SAVE-BACKUP-FALLBACK-UNCERTAIN: The fallback-rotated backup could not be verified; both it and the prior backup were preserved.";
                    return false;
                }

                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                message = $"AL-SAVE-BACKUP-FALLBACK-FAILED: Every generation left by the fallback operation was preserved for explicit recovery. {ex.Message}";
                return false;
            }
        }

        private bool CanSafelyUseMoveFallback(SaveTransactionTrace trace)
        {
            SaveFileReadResult primary = ReadCanonicalPath(SavePath);
            SaveFileReadResult temp = ReadCanonicalPath(TempPath);
            SaveFileReadResult previous = ReadCanonicalPath(PreviousPath);
            return trace != null &&
                   trace.BaselinePrimaryBytes != null &&
                   IsExactTraceBaselineGeneration(primary, trace) &&
                   IsExactValidGeneration(
                       temp,
                       trace.CandidateBytes,
                       out _) &&
                   previous.Disposition == SaveFileReadDisposition.Missing;
        }

        private bool CanSafelyUseBackupMoveFallback(SaveTransactionTrace trace)
        {
            if (trace == null ||
                trace.BaselineBackup == null ||
                trace.BaselinePrimaryBytes == null)
            {
                return false;
            }

            SaveFileReadResult backup = ReadCanonicalPath(BackupPath);
            SaveFileReadResult temp = ReadCanonicalPath(TempPath);
            SaveFileReadResult previous = ReadCanonicalPath(PreviousPath);
            return MatchesExactState(backup, trace.BaselineBackup) &&
                   IsExactTraceBaselineGeneration(temp, trace) &&
                   previous.Disposition == SaveFileReadDisposition.Missing;
        }

        private bool TryRestorePreviousPrimary(SaveTransactionTrace trace)
        {
            if (trace == null || trace.BaselinePrimaryBytes == null)
            {
                return false;
            }

            SaveFileReadResult previous = ReadCanonicalPath(PreviousPath);
            if (!IsExactTraceBaselineGeneration(previous, trace))
            {
                return false;
            }

            trace.RollbackAttempted = true;
            try
            {
                _fileOperations.Copy(PreviousPath, SavePath, true);
                SaveFileReadResult restored = ReadCanonicalPath(SavePath);
                trace.RollbackBytesVerified =
                    IsExactTraceBaselineGeneration(restored, trace);
                return trace.RollbackBytesVerified;
            }
            catch (Exception ex)
            {
                trace.RollbackBytesVerified = false;
                Debug.LogError(
                    $"AL-SAVE-ROLLBACK-FAILED: Could not prove exact restoration of the previous primary. {ex.GetType().Name}");
                return false;
            }
        }

        private bool TryReadValidSave(
            string path,
            out SaveGameData save,
            out string error) =>
            TryReadValidSaveBytes(path, out _, out save, out error);

        private bool TryReadValidSaveBytes(
            string path,
            out byte[] bytes,
            out SaveGameData save,
            out string error)
        {
            bytes = null;
            save = null;
            error = string.Empty;

            SaveFileReadResult readResult = ReadCanonicalPath(path);
            if (readResult.Disposition != SaveFileReadDisposition.Read)
            {
                error = readResult.DiagnosticCode;
                return false;
            }

            bytes = readResult.Bytes;
            return TryDeserializeValidSaveBytes(
                bytes,
                out save,
                out error,
                SourceForPath(path));
        }

        private bool TryDeserializeValidSaveBytes(
            byte[] bytes,
            out SaveGameData save,
            out string error,
            SaveCandidateSourceGeneration source =
                SaveCandidateSourceGeneration.Primary)
        {
            save = null;
            error = string.Empty;
            try
            {
                SaveSemanticCandidate semanticCandidate =
                    ValidateSemanticCandidate(bytes, source);
                if (!semanticCandidate.IsWritable ||
                    (semanticCandidate.Outcome != SaveSemanticCandidateOutcome.Valid &&
                     semanticCandidate.Outcome !=
                     SaveSemanticCandidateOutcome.CompatiblePreservedUnknown))
                {
                    error = semanticCandidate.Diagnostics.Count == 0
                        ? "SAVE_SEMANTIC_WRITE_VALIDATION_FAILED"
                        : semanticCandidate.Diagnostics[0].Code;
                    return false;
                }

                string json = StrictUtf8.GetString(bytes);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "File is empty.";
                    return false;
                }

                save = JsonUtility.FromJson<SaveGameData>(json);
                if (save == null)
                {
                    error = "JSON did not produce a save object.";
                    return false;
                }

                if (!HasCurrentSaveMetadata(save) ||
                    !ValidateSaveSemantics(save, out error))
                {
                    save = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                save = null;
                error = ex.Message;
                return false;
            }
        }

        private static bool ValidateSaveSemantics(SaveGameData save, out string error)
        {
            if (save == null)
            {
                error = "Save object is null.";
                return false;
            }

            if (save.Resources == null ||
                save.Buildings == null ||
                save.Troops == null ||
                save.Researches == null ||
                save.Quests == null ||
                save.Reputation == null ||
                save.FactionReputations == null ||
                save.LordPersona == null ||
                save.Territories == null ||
                save.RealmGems == null ||
                save.Wishgate == null ||
                save.Warmaster == null ||
                save.ChampionCustomization == null ||
                save.OwnedEquipment == null ||
                save.AppliedBossLootRewards == null ||
                save.Nvs01Progress == null)
            {
                error = "Required top-level save collections or objects are null after normalization.";
                return false;
            }

            if (!Nvs01ProgressCodec.TryValidateStoredData(
                    save.Nvs01Progress,
                    out error))
            {
                return false;
            }

            if (!FirstWorldProgressSaveCodec.TryValidateStoredData(
                    save,
                    out error))
            {
                return false;
            }

            var encounterIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var rewardResultIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var reward in save.AppliedBossLootRewards)
            {
                if (reward == null)
                {
                    error = "Applied boss-loot ledger contains a null entry.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(reward.EncounterId) ||
                    string.IsNullOrWhiteSpace(reward.RewardResultId) ||
                    string.IsNullOrWhiteSpace(reward.BossId) ||
                    string.IsNullOrWhiteSpace(reward.RewardDigest))
                {
                    error = "Applied boss-loot ledger contains a blank stable identity or digest.";
                    return false;
                }

                if (reward.CommittedTimestamp <= 0)
                {
                    error = "Applied boss-loot ledger contains an invalid commit timestamp.";
                    return false;
                }

                if (encounterIds.TryGetValue(reward.EncounterId, out var encounterResultId))
                {
                    error = encounterResultId == reward.RewardResultId
                        ? "Applied boss-loot ledger contains a duplicate application identity."
                        : "Applied boss-loot ledger maps one encounter to conflicting reward results.";
                    return false;
                }

                if (rewardResultIds.TryGetValue(reward.RewardResultId, out var resultEncounterId))
                {
                    error = resultEncounterId == reward.EncounterId
                        ? "Applied boss-loot ledger contains a duplicate application identity."
                        : "Applied boss-loot ledger maps one reward result to conflicting encounters.";
                    return false;
                }

                encounterIds.Add(reward.EncounterId, reward.RewardResultId);
                rewardResultIds.Add(reward.RewardResultId, reward.EncounterId);
            }

            error = string.Empty;
            return true;
        }

        private static void EnsureSaveDefaults(SaveGameData save)
        {
            if (save == null)
            {
                return;
            }

            save.Resources = RemoveNullEntries(save.Resources);
            save.Buildings = RemoveNullEntries(save.Buildings);
            save.Troops = RemoveNullEntries(save.Troops);
            save.Researches = RemoveNullEntries(save.Researches);
            save.Quests ??= new List<QuestState>();
            save.Reputation = RemoveNullEntries(save.Reputation);
            save.FactionReputations = RemoveNullEntries(save.FactionReputations);
            save.Territories = RemoveNullEntries(save.Territories);
            save.RealmGems = RemoveNullEntries(save.RealmGems);
            save.OwnedEquipment = RemoveNullEntries(save.OwnedEquipment);
            save.AppliedBossLootRewards ??= new List<AppliedBossLootRewardState>();
            save.LordPersona ??= new PersonaData();
            save.Wishgate ??= new WishgateState();
            save.Warmaster ??= new WarmasterState();
            save.Warmaster.UnlockedSetIds = RemoveNullStrings(save.Warmaster.UnlockedSetIds);
            save.Warmaster.PurchasedPieceIds = RemoveNullStrings(save.Warmaster.PurchasedPieceIds);
            save.ChampionCustomization ??= new ChampionCustomizationState();
            EnsureNvs01NeutralDefaults(save);

            EnsureResource(save, ResourceType.Food, 1000);
            EnsureResource(save, ResourceType.Wood, 1000);
            EnsureResource(save, ResourceType.Stone, 500);
            EnsureResource(save, ResourceType.Gold, 500);
            EnsureResource(save, ResourceType.ManaStone, 150);
            EnsureResource(save, ResourceType.Ore, 150);
            EnsureResource(save, ResourceType.DeepOre, 0);
            EnsureResource(save, ResourceType.WorldSap, 0);
            EnsureResource(save, ResourceType.RoyalSigil, 0);
            EnsureResource(save, ResourceType.DarkCrystal, 0);

            if (string.IsNullOrWhiteSpace(save.CurrentChapterId))
            {
                save.CurrentChapterId = "C1";
            }
        }

        private static List<T> RemoveNullEntries<T>(List<T> entries) where T : class =>
            entries == null
                ? new List<T>()
                : entries.Any(entry => entry == null)
                    ? entries.Where(entry => entry != null).ToList()
                    : entries;

        private static List<string> RemoveNullStrings(List<string> entries) =>
            entries == null
                ? new List<string>()
                : entries.Any(string.IsNullOrWhiteSpace)
                    ? entries.Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList()
                    : entries;

        private static void EnsureNvs01NeutralDefaults(SaveGameData save)
        {
            save.Nvs01Progress ??= new Nvs01ProgressData();
            if (save.Nvs01Progress.Version != 0)
            {
                return;
            }

            save.Nvs01Progress.PacketVersion ??= string.Empty;
            save.Nvs01Progress.PacketSha256 ??= string.Empty;
            save.Nvs01Progress.QuestId ??= string.Empty;
            save.Nvs01Progress.StateId ??= string.Empty;
            save.Nvs01Progress.Objectives ??= new List<Nvs01ObjectiveProgressData>();
            save.Nvs01Progress.CurrentDialogueNodeId ??= string.Empty;
            save.Nvs01Progress.PendingSemanticActionId ??= string.Empty;
            save.Nvs01Progress.CommittedRealmId ??= string.Empty;
            save.Nvs01Progress.CurrentEncounter ??= new Nvs01EncounterRequestData();
            save.Nvs01Progress.LastEncounterCorrelationId ??= string.Empty;
            save.Nvs01Progress.LastEncounterEventId ??= string.Empty;
            save.Nvs01Progress.LastEncounterSnapshotVersion ??= string.Empty;
            save.Nvs01Progress.LastEncounterSnapshotReference ??= string.Empty;
            save.Nvs01Progress.LastOperation ??= new Nvs01OperationReceiptData();
            save.Nvs01Progress.ConsequenceIntentIds ??= new List<string>();
            save.Nvs01Progress.AcquiredArtifactIds ??= new List<string>();
            save.Nvs01Progress.AppliedEffectKeys ??= new List<string>();
            save.Nvs01Progress.UnlockedChapterId ??= string.Empty;
        }

        private static void EnsureResource(SaveGameData save, ResourceType type, long startingAmount)
        {
            foreach (var resource in save.Resources)
            {
                if (resource.Type == type)
                {
                    return;
                }
            }

            save.Resources.Add(new ResourceData { Type = type, Amount = startingAmount });
        }

        private bool TryQuarantineInvalidFile(string path, out string error)
        {
            error = string.Empty;
            if (!_fileOperations.FileExists(path))
            {
                return true;
            }

            string quarantinePath = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            try
            {
                _fileOperations.Move(path, quarantinePath);
                Debug.LogWarning($"AL-SAVE-QUARANTINED: Quarantined invalid save file to {quarantinePath}.");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"AL-SAVE-QUARANTINE-FAILED: Could not quarantine invalid save file {path}: {ex.Message}");
                return false;
            }
        }

        private IReadOnlyList<SaveCandidateInventoryEntry> BuildCandidateInventory()
        {
            return new[]
            {
                InspectCandidate(SaveCandidateSourceGeneration.Primary, SavePath),
                InspectCandidate(SaveCandidateSourceGeneration.Backup, BackupPath),
                InspectCandidate(SaveCandidateSourceGeneration.Previous, PreviousPath),
                InspectCandidate(SaveCandidateSourceGeneration.Temp, TempPath)
            };
        }

        private SaveSemanticCandidate ValidateSemanticCandidate(
            byte[] bytes,
            SaveCandidateSourceGeneration source)
        {
            // Selection and every later ledger/recovery verification must use
            // one topology-aware classification for the same retained bytes.
            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                bytes,
                source,
                _semanticPolicy);
            if (IsExactNvs01V003MigrationCandidate(candidate) &&
                (!TryDeserializeSelectedCandidate(
                     candidate,
                     out SaveGameData retained) ||
                 !Nvs01ProgressCodec.TryMigrateExactV003(
                     retained.Nvs01Progress,
                     out _,
                     out _)))
            {
                return SaveSemanticCandidateValidator
                    .RejectNvs01MigrationTopology(candidate);
            }

            return candidate;
        }

        private SaveCandidateInventoryEntry InspectCandidate(
            SaveCandidateSourceGeneration source,
            string path)
        {
            SaveFileReadResult readResult = _fileOperations.ReadAllBytesBounded(
                path,
                _semanticPolicy.MaximumInputBytes);
            SaveSemanticCandidate candidate = readResult.Disposition == SaveFileReadDisposition.Read
                ? ValidateSemanticCandidate(readResult.Bytes, source)
                : null;
            var summaryReadResult = new SaveFileReadResult(
                readResult.Disposition,
                null,
                readResult.ObservedByteCount,
                readResult.DiagnosticCode);
            return new SaveCandidateInventoryEntry(source, summaryReadResult, candidate);
        }

        private static SaveCandidateInventoryEntry Find(
            IEnumerable<SaveCandidateInventoryEntry> inventory,
            SaveCandidateSourceGeneration source) =>
            inventory.First(entry => entry.Source == source);

        private bool HasSaveEvidence(string path) =>
            _fileOperations.ReadAllBytesBounded(path, 1).Disposition !=
            SaveFileReadDisposition.Missing;

        private void CreateNewProfileAfterAllMissing(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory)
        {
            SaveGameData newSave = CreateDefaultSave(RealmId.None);
            SaveGameData firstGenerationCandidate = CloneSave(newSave);

            bool coreSucceeded = TryCreateFirstGenerationCandidate(
                    firstGenerationCandidate,
                    out SaveGameData persistedNewSave,
                    out string createMessage,
                    out bool diskChanged);
            SaveOperationStatus reconciledStatus =
                ReconcileFirstGenerationAttempt(
                    firstGenerationCandidate,
                    coreSucceeded,
                    diskChanged,
                    ref persistedNewSave,
                    out _,
                    ref createMessage);
            if (reconciledStatus != SaveOperationStatus.SavedPrimary ||
                persistedNewSave == null)
            {
                _profileWritable = false;
                _readOnlyCandidate = CloneSave(newSave);
                _currentSave = null;
                PublishDisposition(
                    inventory,
                    null,
                    "SAVE_SELECT_ALL_MISSING_CREATE_FAILED",
                    false,
                    false,
                    diskChanged);
                SetLoadStatus(
                    SaveLoadStatus.RecoveryFailed,
                    $"AL-SAVE-CREATE-FAILED: All generations were missing, but a new profile could not be persisted. {createMessage}",
                    true);
                return;
            }

            _currentSave = persistedNewSave;
            _profileWritable = true;
            PublishDisposition(
                inventory,
                null,
                "SAVE_SELECT_ALL_MISSING_CREATE_NEW",
                true,
                true,
                true,
                SaveCandidateSourceGeneration.Primary);
            SetLoadStatus(
                SaveLoadStatus.CreatedNew,
                "AL-SAVE-CREATED-NEW: All canonical generations were missing, so a new current-format profile was created.",
                false);
        }

        private bool TryCreateFirstGenerationCandidate(
            SaveGameData candidate,
            out SaveGameData persistedSave,
            out string message,
            out bool diskChanged)
        {
            persistedSave = null;
            diskChanged = false;
            if (!HasCurrentSaveMetadata(candidate))
            {
                message = "AL-SAVE-CREATE-METADATA-INVALID: A first-generation profile must use current save metadata.";
                return false;
            }

            try
            {
                ApplyNeutralPersistenceDefaults(candidate);
                candidate.LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (!TrySerializeBounded(candidate, out string json, out message))
                {
                    return false;
                }

                _fileOperations.CreateDirectory(PersistencePath);
                if (!AllCanonicalPathsMissing(includeTemp: true))
                {
                    message = "AL-SAVE-CREATE-RACE-PRESERVED: Save evidence appeared after the all-missing inventory; no generation was replaced.";
                    return false;
                }

                SaveFileWriteResult writeResult =
                    _fileOperations.WriteAllTextDurable(TempPath, json);
                diskChanged |= writeResult.DiskChanged;
                if (!writeResult.Succeeded)
                {
                    message = $"AL-SAVE-CREATE-TEMP-WRITE-FAILED: The first-generation temporary candidate could not be written durably. {writeResult.DiagnosticCode}";
                    return false;
                }

                if (!TryReadValidSave(TempPath, out _, out string tempError))
                {
                    message = $"AL-SAVE-CREATE-TEMP-INVALID: The first-generation temporary candidate failed semantic validation. {tempError}";
                    return false;
                }

                if (!AllCanonicalPathsMissing(includeTemp: false))
                {
                    message = "AL-SAVE-CREATE-RACE-PRESERVED: A primary, backup, or previous generation appeared while preparing a new profile; all files were preserved.";
                    return false;
                }

                _fileOperations.Copy(TempPath, BackupPath, false);
                if (!TryReadValidSave(BackupPath, out _, out string backupError))
                {
                    message = $"AL-SAVE-CREATE-BACKUP-INVALID: The first-generation backup failed semantic validation. {backupError}";
                    return false;
                }

                if (HasSaveEvidence(SavePath) || HasSaveEvidence(PreviousPath))
                {
                    bool removedCreatedBackup = TryDelete(BackupPath);
                    message = removedCreatedBackup
                        ? "AL-SAVE-CREATE-RACE-PRESERVED: Primary or previous evidence appeared after backup preparation; the newly created backup was removed and no primary was installed."
                        : "AL-SAVE-CREATE-RACE-CLEANUP-FAILED: Primary or previous evidence appeared after backup preparation; no primary was installed and all remaining evidence was preserved.";
                    return false;
                }

                _fileOperations.Move(TempPath, SavePath);
                if (!TryReadValidSave(SavePath, out persistedSave, out string primaryError))
                {
                    message = $"AL-SAVE-CREATE-PRIMARY-INVALID: The installed first-generation primary failed semantic validation. {primaryError}";
                    return false;
                }

                if (HasSaveEvidence(PreviousPath))
                {
                    message = "AL-SAVE-CREATE-COMMIT-UNCERTAIN: Previous-generation evidence appeared during primary installation; the new primary and every auxiliary generation were preserved for explicit recovery.";
                    return false;
                }

                message = "AL-SAVE-CREATED-FIRST-GENERATION: Created a primary and backup without replacing any existing generation.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"AL-SAVE-CREATE-FIRST-GENERATION-FAILED: Existing and newly created evidence was preserved. {ex.Message}";
                return false;
            }
        }

        private bool AllCanonicalPathsMissing(bool includeTemp)
        {
            if (HasSaveEvidence(SavePath) ||
                HasSaveEvidence(BackupPath) ||
                HasSaveEvidence(PreviousPath) ||
                HasSaveEvidence(LegacyPreviousPath) ||
                HasProfileIdentityWitnessEvidence())
            {
                return false;
            }

            return !includeTemp || !HasSaveEvidence(TempPath);
        }

        private void PublishDisposition(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            SaveSemanticCandidate selected,
            string selectorReason,
            bool writable,
            bool runtimeUsable,
            bool diskChanged,
            SaveCandidateSourceGeneration selectedSourceOverride =
                SaveCandidateSourceGeneration.Unknown)
        {
            var summaries = new List<SaveCandidateLoadSummary>(inventory.Count);
            foreach (SaveCandidateInventoryEntry entry in inventory)
            {
                SaveSemanticCandidate candidate = entry.SemanticCandidate;
                IEnumerable<string> diagnosticCodes = candidate == null
                    ? new[] { entry.ReadResult.DiagnosticCode }
                    : candidate.Diagnostics.Select(diagnostic => diagnostic.Code);
                summaries.Add(
                    new SaveCandidateLoadSummary(
                        entry.Source,
                        entry.ReadResult.Disposition,
                        entry.ReadResult.ObservedByteCount,
                        candidate != null,
                        candidate == null
                            ? SaveSemanticCandidateOutcome.Invalid
                            : candidate.Outcome,
                        candidate == null
                            ? SaveSemanticDomain.None
                            : candidate.DisabledDomains,
                        candidate == null
                            ? SaveSemanticDomain.None
                            : candidate.NormalizedDomains,
                        candidate == null
                            ? SaveSemanticDomain.None
                            : candidate.PreservedUnknownDomains,
                        diagnosticCodes));
            }

            SaveCandidateSourceGeneration selectedSource =
                selectedSourceOverride != SaveCandidateSourceGeneration.Unknown
                    ? selectedSourceOverride
                    : selected == null
                        ? SaveCandidateSourceGeneration.Unknown
                        : selected.SourceGeneration;
            if (TryMapAuthoritySource(
                    selectedSource,
                    out ProfileAuthoritySourceGeneration authoritySource))
            {
                _hasObservedAuthoritySource = true;
                _observedAuthoritySource = authoritySource;
                SaveGameData observed = _currentSave ?? _readOnlyCandidate;
                _observedAuthoritySaveSchemaVersion = selected != null
                    ? selected.SaveSchemaVersion
                    : observed?.SaveSchemaVersion ?? 0;
                _observedAuthorityProfileInitializationVersion =
                    selected != null
                        ? selected.ProfileInitializationVersion
                        : observed?.ProfileInitializationVersion ?? 0;
            }
            LastLoadDisposition = new SaveLoadDisposition(
                summaries,
                selectedSource,
                selectorReason,
                writable,
                runtimeUsable,
                offlineProgressApplied: false,
                diskChanged: diskChanged,
                rawEvidencePreserved: true);
        }

        internal void PublishOfflineProgressApplied()
        {
            if (LastLoadDisposition == null ||
                !LastLoadDisposition.IsWritable ||
                LastLoadDisposition.OfflineProgressApplied)
            {
                return;
            }

            LastLoadDisposition = new SaveLoadDisposition(
                LastLoadDisposition.CandidateSummaries,
                LastLoadDisposition.SelectedSource,
                LastLoadDisposition.SelectorReason,
                LastLoadDisposition.IsWritable,
                LastLoadDisposition.IsRuntimeUsable,
                offlineProgressApplied: true,
                diskChanged: true,
                LastLoadDisposition.RawEvidencePreserved);
        }

        private static bool TryMapAuthoritySource(
            SaveCandidateSourceGeneration source,
            out ProfileAuthoritySourceGeneration mapped)
        {
            switch (source)
            {
                case SaveCandidateSourceGeneration.Primary:
                    mapped = ProfileAuthoritySourceGeneration.Primary;
                    return true;
                case SaveCandidateSourceGeneration.Backup:
                    mapped = ProfileAuthoritySourceGeneration.Backup;
                    return true;
                case SaveCandidateSourceGeneration.Previous:
                    mapped = ProfileAuthoritySourceGeneration.Previous;
                    return true;
                case SaveCandidateSourceGeneration.Temp:
                    mapped = ProfileAuthoritySourceGeneration.Temp;
                    return true;
                default:
                    mapped = ProfileAuthoritySourceGeneration.None;
                    return false;
            }
        }

        private static bool TryDeserializeSelectedCandidate(
            SaveSemanticCandidate candidate,
            out SaveGameData save)
        {
            save = null;
            if (candidate == null || !candidate.HasRetainedRawBytes)
            {
                return false;
            }

            try
            {
                byte[] bytes = candidate.CopyRawBytes();
                string json = StrictUtf8.GetString(bytes);
                save = JsonUtility.FromJson<SaveGameData>(json);
                if (save != null &&
                    candidate.Outcome == SaveSemanticCandidateOutcome.CompatibleNormalized)
                {
                    ApplyApprovedNeutralNormalization(save, candidate);
                }

                return save != null;
            }
            catch (Exception)
            {
                save = null;
                return false;
            }
        }

        private static void ApplyNeutralPersistenceDefaults(SaveGameData save)
        {
            if (save == null)
            {
                return;
            }

            save.Resources ??= new List<ResourceData>();
            save.Buildings ??= new List<BuildingState>();
            save.Troops ??= new List<TroopInventoryData>();
            save.Researches ??= new List<ResearchState>();
            save.Quests ??= new List<QuestState>();
            save.Reputation ??= new List<NpcAffinityData>();
            save.FactionReputations ??= new List<FactionRepData>();
            save.LordPersona ??= new PersonaData();
            save.Territories ??= new List<TerritoryData>();
            save.RealmGems ??= new List<RealmGemState>();
            save.Wishgate ??= new WishgateState();
            save.Warmaster ??= new WarmasterState();
            save.Warmaster.UnlockedSetIds ??= new List<string>();
            save.Warmaster.PurchasedPieceIds ??= new List<string>();
            save.ChampionCustomization ??= new ChampionCustomizationState();
            save.OwnedEquipment ??= new List<OwnedEquipmentState>();
            EnsureNvs01NeutralDefaults(save);
        }

        private static void ApplyApprovedNeutralNormalization(
            SaveGameData save,
            SaveSemanticCandidate candidate)
        {
            ApplyNeutralPersistenceDefaults(save);

            EnsureResource(save, ResourceType.ManaStone, 0);
            EnsureResource(save, ResourceType.Ore, 0);

            ChampionCustomizationState customization = save.ChampionCustomization;
            if (HasNormalizationDiagnostic(
                    candidate,
                    "$.ChampionCustomization.FaceMarkId"))
            {
                customization.FaceMarkId = "none";
            }

            if (HasNormalizationDiagnostic(
                    candidate,
                    "$.ChampionCustomization.WeaponStyleId"))
            {
                customization.WeaponStyleId = "sword";
            }

            if (HasNormalizationDiagnostic(
                    candidate,
                    "$.ChampionCustomization.OffhandStyleId"))
            {
                customization.OffhandStyleId = "shield";
            }

            ApplyCustomizationColorDefault(
                candidate,
                "SkinR",
                0.72f,
                value => customization.SkinR = value);
            ApplyCustomizationColorDefault(
                candidate,
                "SkinG",
                0.56f,
                value => customization.SkinG = value);
            ApplyCustomizationColorDefault(
                candidate,
                "SkinB",
                0.42f,
                value => customization.SkinB = value);
            ApplyCustomizationColorDefault(
                candidate,
                "EyeR",
                0.25f,
                value => customization.EyeR = value);
            ApplyCustomizationColorDefault(
                candidate,
                "EyeG",
                0.58f,
                value => customization.EyeG = value);
            ApplyCustomizationColorDefault(
                candidate,
                "EyeB",
                0.92f,
                value => customization.EyeB = value);
            ApplyCustomizationColorDefault(
                candidate,
                "AccentR",
                0.85f,
                value => customization.AccentR = value);
            ApplyCustomizationColorDefault(
                candidate,
                "AccentG",
                0.62f,
                value => customization.AccentG = value);
            ApplyCustomizationColorDefault(
                candidate,
                "AccentB",
                0.18f,
                value => customization.AccentB = value);
        }

        private static void ApplyCustomizationColorDefault(
            SaveSemanticCandidate candidate,
            string fieldName,
            float defaultValue,
            Action<float> apply)
        {
            if (HasNormalizationDiagnostic(
                    candidate,
                    "$.ChampionCustomization." + fieldName))
            {
                apply(defaultValue);
            }
        }

        private static bool HasNormalizationDiagnostic(
            SaveSemanticCandidate candidate,
            string path) =>
            candidate.Diagnostics.Any(
                diagnostic =>
                    diagnostic.Code == "SAVE_CUSTOMIZATION_FIELD_DEFAULTED" &&
                    string.Equals(diagnostic.Path, path, StringComparison.Ordinal));

        private SaveCandidateSourceGeneration SourceForPath(string path)
        {
            if (string.Equals(path, SavePath, StringComparison.OrdinalIgnoreCase))
            {
                return SaveCandidateSourceGeneration.Primary;
            }

            if (string.Equals(path, BackupPath, StringComparison.OrdinalIgnoreCase))
            {
                return SaveCandidateSourceGeneration.Backup;
            }

            if (string.Equals(path, PreviousPath, StringComparison.OrdinalIgnoreCase))
            {
                return SaveCandidateSourceGeneration.Previous;
            }

            return SaveCandidateSourceGeneration.Temp;
        }

        private static bool HasUnresolvedAuxiliaryEvidence(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory)
        {
            foreach (SaveCandidateInventoryEntry entry in inventory)
            {
                if (entry.Source == SaveCandidateSourceGeneration.Primary)
                {
                    continue;
                }

                if (entry.Source == SaveCandidateSourceGeneration.Temp ||
                    entry.Source == SaveCandidateSourceGeneration.Previous)
                {
                    if (entry.ReadResult.Disposition != SaveFileReadDisposition.Missing)
                    {
                        return true;
                    }

                    continue;
                }

                if (entry.ReadResult.Disposition == SaveFileReadDisposition.Missing)
                {
                    continue;
                }

                if (entry.Source == SaveCandidateSourceGeneration.Backup &&
                    IsExactNvs01V003MigrationCandidate(
                        entry.SemanticCandidate))
                {
                    continue;
                }

                if (entry.ReadResult.Disposition != SaveFileReadDisposition.Read ||
                    entry.SemanticCandidate == null ||
                    !entry.SemanticCandidate.IsWritable ||
                    !IsRuntimeRoundTrippable(entry.SemanticCandidate))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetCommittedBackupRecoveryWitness(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory,
            out byte[] witnessBytes)
        {
            witnessBytes = null;
            SaveCandidateInventoryEntry primary = Find(
                inventory,
                SaveCandidateSourceGeneration.Primary);
            SaveCandidateInventoryEntry backup = Find(
                inventory,
                SaveCandidateSourceGeneration.Backup);
            SaveCandidateInventoryEntry temp = Find(
                inventory,
                SaveCandidateSourceGeneration.Temp);
            SaveCandidateInventoryEntry previous = Find(
                inventory,
                SaveCandidateSourceGeneration.Previous);
            if (primary.ReadResult.Disposition != SaveFileReadDisposition.Read ||
                backup.ReadResult.Disposition != SaveFileReadDisposition.Read ||
                temp.ReadResult.Disposition != SaveFileReadDisposition.Read ||
                previous.ReadResult.Disposition != SaveFileReadDisposition.Missing ||
                !IsExplicitCurrentWritableCandidate(primary.SemanticCandidate) ||
                !IsExplicitCurrentWritableCandidate(backup.SemanticCandidate) ||
                !IsExplicitCurrentWritableCandidate(temp.SemanticCandidate))
            {
                return false;
            }

            byte[] primaryBytes = primary.SemanticCandidate.CopyRawBytes();
            byte[] backupBytes = backup.SemanticCandidate.CopyRawBytes();
            byte[] tempBytes = temp.SemanticCandidate.CopyRawBytes();
            if (!BytesEqual(primaryBytes, backupBytes) ||
                !BytesEqual(primaryBytes, tempBytes))
            {
                return false;
            }

            witnessBytes = primaryBytes;
            return true;
        }

        private bool HasWritableTempEvidence(
            IReadOnlyList<SaveCandidateInventoryEntry> inventory)
        {
            SaveCandidateInventoryEntry temp = Find(
                inventory,
                SaveCandidateSourceGeneration.Temp);
            return temp.ReadResult.Disposition == SaveFileReadDisposition.Read &&
                   IsExplicitCurrentWritableCandidate(temp.SemanticCandidate);
        }

        private bool IsExplicitCurrentWritableCandidate(
            SaveSemanticCandidate candidate) =>
            candidate != null &&
            candidate.HasRetainedRawBytes &&
            candidate.HasExplicitSaveSchemaVersion &&
            candidate.SaveSchemaVersion == _semanticPolicy.CurrentSaveSchemaVersion &&
            candidate.HasExplicitProfileInitializationVersion &&
            candidate.ProfileInitializationVersion ==
                _semanticPolicy.CurrentProfileInitializationVersion &&
            candidate.IsWritable &&
            IsRuntimeRoundTrippable(candidate);

        private static bool IsRuntimeRoundTrippable(
            SaveSemanticCandidate candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate.Outcome == SaveSemanticCandidateOutcome.Valid ||
                candidate.Outcome ==
                    SaveSemanticCandidateOutcome.CompatiblePreservedUnknown)
            {
                return true;
            }

            if (candidate.Outcome !=
                    SaveSemanticCandidateOutcome.CompatibleNormalized ||
                candidate.NormalizedDomains != SaveSemanticDomain.Narrative ||
                candidate.DisabledDomains != SaveSemanticDomain.None ||
                candidate.PreservedUnknownDomains != SaveSemanticDomain.None ||
                candidate.Diagnostics.Count != 1)
            {
                return false;
            }

            SaveSemanticDiagnostic diagnostic = candidate.Diagnostics[0];
            return diagnostic != null &&
                   diagnostic.Code == "SAVE_NVS01_PROGRESS_DEFAULTED" &&
                   diagnostic.Path == "$.Nvs01Progress" &&
                   diagnostic.Domain == SaveSemanticDomain.Narrative &&
                   diagnostic.Severity ==
                       SaveSemanticDiagnosticSeverity.Information;
        }

        private void PruneQuarantines(
            string sourceFileName,
            params string[] protectedPaths)
        {
            var quarantines = EnumerateQuarantines(sourceFileName)
                .Select(path => new
                {
                    Path = path,
                    CreationTimeUtc = _fileOperations.GetCreationTimeUtc(path)
                })
                .OrderByDescending(candidate => candidate.CreationTimeUtc)
                .ThenByDescending(candidate => candidate.Path, StringComparer.Ordinal)
                .ToList();

            var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string protectedPath in protectedPaths ??
                         Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(protectedPath) &&
                    quarantines.Any(candidate => string.Equals(
                        candidate.Path,
                        protectedPath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    retained.Add(protectedPath);
                }
            }

            foreach (var candidate in quarantines)
            {
                if (retained.Count >= MaxQuarantinesPerSource)
                {
                    break;
                }

                retained.Add(candidate.Path);
            }

            foreach (var old in quarantines.Where(candidate =>
                         !retained.Contains(candidate.Path)))
            {
                TryDelete(old.Path);
                Debug.LogWarning(
                    "AL-SAVE-QUARANTINE-PRUNED: Pruned an old bounded quarantine artifact.");
            }
        }

        private IEnumerable<string> EnumerateQuarantines(string sourceFileName) =>
            _fileOperations.EnumerateFiles(PersistencePath, $"{sourceFileName}.corrupt-*");

        private IEnumerable<string> EnumerateNvs01MigrationBackupArchives() =>
            _fileOperations.EnumerateFiles(
                PersistencePath,
                Nvs01MigrationBackupArchivePattern);

        private bool TryVerifyNvs01MigrationBackupArchivesTwice(
            out string diagnostic)
        {
            if (!TryCaptureNvs01MigrationBackupArchives(
                    out IReadOnlyDictionary<string, byte[]> first,
                    out diagnostic) ||
                !TryCaptureNvs01MigrationBackupArchives(
                    out IReadOnlyDictionary<string, byte[]> second,
                    out diagnostic) ||
                first.Count != second.Count)
            {
                return false;
            }

            foreach (var entry in first)
            {
                if (!second.TryGetValue(entry.Key, out byte[] bytes) ||
                    !BytesEqual(entry.Value, bytes))
                {
                    diagnostic =
                        "SAVE_NVS01_MIGRATION_ARCHIVE_CHANGED";
                    return false;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryCaptureNvs01MigrationBackupArchives(
            out IReadOnlyDictionary<string, byte[]> archives,
            out string diagnostic)
        {
            var captured = new Dictionary<string, byte[]>(
                StringComparer.OrdinalIgnoreCase);
            archives = new ReadOnlyDictionary<string, byte[]>(captured);
            diagnostic =
                "SAVE_NVS01_MIGRATION_ARCHIVE_INVENTORY_UNREADABLE";
            try
            {
                string persistenceRoot = Path.GetFullPath(PersistencePath);
                List<string> paths = EnumerateNvs01MigrationBackupArchives()
                    .Take(MaxNvs01MigrationBackupArchives + 1)
                    .ToList();
                if (paths.Count > MaxNvs01MigrationBackupArchives)
                {
                    diagnostic =
                        "SAVE_NVS01_MIGRATION_ARCHIVE_AMBIGUOUS";
                    return false;
                }

                foreach (string path in paths)
                {
                    string fullPath = Path.GetFullPath(path);
                    string fileName = Path.GetFileName(fullPath);
                    if (!string.Equals(
                            Path.GetDirectoryName(fullPath),
                            persistenceRoot,
                            StringComparison.OrdinalIgnoreCase) ||
                        fileName == null ||
                        !fileName.StartsWith(
                            Nvs01MigrationBackupArchivePrefix,
                            StringComparison.Ordinal) ||
                        fileName.Length !=
                            Nvs01MigrationBackupArchivePrefix.Length +
                            Sha256Base64UrlLength)
                    {
                        diagnostic =
                            "SAVE_NVS01_MIGRATION_ARCHIVE_NAME_INVALID";
                        return false;
                    }

                    string declaredIdentity = fileName.Substring(
                        Nvs01MigrationBackupArchivePrefix.Length);
                    if (!IsBase64UrlIdentity(
                            declaredIdentity,
                            Sha256Base64UrlLength) ||
                        _fileOperations.IsReparsePoint(fullPath))
                    {
                        diagnostic =
                            "SAVE_NVS01_MIGRATION_ARCHIVE_UNSAFE";
                        return false;
                    }

                    SaveFileReadResult read = ReadCanonicalPath(fullPath);
                    if (read.Disposition != SaveFileReadDisposition.Read ||
                        read.Bytes == null ||
                        !string.Equals(
                            ComputeSha256Base64Url(read.Bytes),
                            declaredIdentity,
                            StringComparison.Ordinal) ||
                        captured.ContainsKey(fileName))
                    {
                        diagnostic =
                            "SAVE_NVS01_MIGRATION_ARCHIVE_HASH_MISMATCH";
                        return false;
                    }

                    captured.Add(fileName, read.Bytes.ToArray());
                }

                diagnostic = string.Empty;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool HasNvs01MigrationBackupArchiveEvidence()
        {
            try
            {
                return EnumerateNvs01MigrationBackupArchives().Any();
            }
            catch (Exception)
            {
                return true;
            }
        }

        private bool HasStageFiveTransactionArchiveEvidence()
        {
            try
            {
                return EnumerateQuarantines(SaveFileName).Any(path =>
                {
                    string fileName = Path.GetFileName(path);
                    return fileName != null &&
                           fileName.IndexOf(
                               "-stage5-",
                               StringComparison.OrdinalIgnoreCase) >= 0 &&
                           fileName.EndsWith(
                               ".txn",
                               StringComparison.OrdinalIgnoreCase);
                });
            }
            catch (Exception)
            {
                return true;
            }
        }

        private bool TryDelete(string path)
        {
            return TryDelete(path, approvalResetOperation: false);
        }

        private bool TryDelete(string path, bool approvalResetOperation)
        {
            try
            {
                if (approvalResetOperation)
                {
                    bool deleted = MvpApprovalSlotRuntime.TryDeleteAuthorizedArtifact(
                        this,
                        path,
                        out string failure);
                    if (!deleted)
                    {
                        Debug.LogWarning(
                            $"AL-SAVE-DELETE-FAILED: Could not safely delete approval artifact {path}: {failure}");
                    }

                    return deleted;
                }

                _fileOperations.Delete(path);
                return _fileOperations
                    .ReadAllBytesBounded(path, 1)
                    .Disposition == SaveFileReadDisposition.Missing;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AL-SAVE-DELETE-FAILED: Could not delete save artifact {path}: {ex.Message}");
                return false;
            }
        }

        private void SetLoadStatus(SaveLoadStatus status, string message, bool error)
        {
            ResetObservedNonWritableAuthorityCache();
            LastLoadStatus = status;
            LastLoadMessage = message;

            if (error)
            {
                Debug.LogError(message);
            }
            else if (status == SaveLoadStatus.RecoveredFromBackup ||
                     status == SaveLoadStatus.CreatedNewAfterUnrecoverableCorruption ||
                     status == SaveLoadStatus.RecoveryFailed)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private void SetSaveStatus(SaveOperationStatus status, string message, bool error)
        {
            ResetObservedNonWritableAuthorityCache();
            LastSaveStatus = status;
            LastSaveMessage = message;

            if (error)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private bool TrySerializeBounded(
            SaveGameData candidate,
            out string json,
            out string message)
        {
            json = JsonUtility.ToJson(candidate, true);
            int byteCount = StrictUtf8.GetByteCount(json);
            if (byteCount > _semanticPolicy.MaximumInputBytes)
            {
                json = null;
                message =
                    $"AL-SAVE-CANDIDATE-TOO-LARGE: Serialized UTF-8 payload is {byteCount} bytes; limit is {_semanticPolicy.MaximumInputBytes}. Active files were preserved.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static SaveSemanticValidationPolicy CreateSemanticPolicy()
        {
            var authority = new SaveSemanticValidationAuthority(
                EnumValues(typeof(RealmId)),
                EnumValues(typeof(ResourceType)),
                new[]
                {
                    (int)ResourceType.Food,
                    (int)ResourceType.Wood,
                    (int)ResourceType.Stone,
                    (int)ResourceType.Gold
                },
                new[]
                {
                    (int)ResourceType.Food,
                    (int)ResourceType.Wood,
                    (int)ResourceType.Stone,
                    (int)ResourceType.Gold,
                    (int)ResourceType.ManaStone,
                    (int)ResourceType.Ore
                },
                EnumValues(typeof(TroopType)),
                EnumValues(typeof(EquipmentSlot)),
                Array.Empty<SaveSemanticQuestRule>(),
                new[]
                {
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.Chapter,
                        "C1"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.BodyPreset,
                        "average"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.HairStyle,
                        "short"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.ArmorStyle,
                        "realm_basic"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.FaceMark,
                        "none"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.WeaponStyle,
                        "sword"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.OffhandStyle,
                        "shield"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.Building,
                        MvpLoopSaveCodec.DefaultOneBuildId)
                });
            return new SaveSemanticValidationPolicy(
                SaveGameData.CurrentSaveFormatId,
                SaveGameData.CurrentSaveSchemaVersion,
                SaveGameData.CurrentProfileInitializationVersion,
                authority,
                maximumInputBytes:
                    SaveSemanticValidationPolicy.DefaultMaximumInputBytes,
                maximumDiagnostics:
                    SaveSemanticValidationPolicy.DefaultMaximumDiagnostics,
                nvs01Rule: new SaveSemanticNvs01Rule(
                    Nvs01ProgressData.CurrentVersion,
                    Nvs01RuntimeContract.PacketVersion,
                    Nvs01RuntimeContract.PacketSha256,
                    Nvs01RuntimeContract.QuestId,
                    Nvs01ProgressCodec.MigratablePacketVersion,
                    Nvs01ProgressCodec.MigratablePacketSha256));
        }

        private static int[] EnumValues(Type enumType) =>
            Enum.GetValues(enumType)
                .Cast<object>()
                .Select(Convert.ToInt32)
                .ToArray();

        private static SaveGameData CreateDefaultSave(RealmId realmId)
        {
            var save = new SaveGameData
            {
                SelectedRealm = realmId,
                LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Resources = new List<ResourceData>
                {
                    new ResourceData { Type = ResourceType.Food, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Wood, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Stone, Amount = 500 },
                    new ResourceData { Type = ResourceType.Gold, Amount = 500 },
                    new ResourceData { Type = ResourceType.ManaStone, Amount = 150 },
                    new ResourceData { Type = ResourceType.Ore, Amount = 150 }
                },
                Buildings = new List<BuildingState>(),
                Troops = new List<TroopInventoryData>(),
                Quests = new List<QuestState>(),
                CurrentChapterId = "C1",
                Warmaster = new WarmasterState()
            };

            EnsureSaveDefaults(save);
            save.SaveFormatId = SaveGameData.CurrentSaveFormatId;
            save.SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion;
            save.ProfileInitializationVersion = SaveGameData.CurrentProfileInitializationVersion;
            if (string.IsNullOrEmpty(save.ProfileId))
            {
                save.ProfileId = new CryptographicProfileIdentityCandidateSource()
                    .GetCandidate(1);
            }
            return save;
        }

        private static SaveGameData CloneSave(SaveGameData save)
        {
            if (save == null)
            {
                return null;
            }

            return JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(save));
        }

        private static bool IsPreSchemaMetadata(SaveGameData save) =>
            save != null &&
            (string.IsNullOrEmpty(save.SaveFormatId) ||
             save.SaveSchemaVersion <= 0 ||
             save.ProfileInitializationVersion <= 0);

        private static bool HasRecoverableKingdomState(SaveGameData save) =>
            save != null && save.SelectedRealm != RealmId.None;

        private static bool HasCurrentSaveMetadata(SaveGameData save) =>
            save != null &&
            string.Equals(
                save.SaveFormatId,
                SaveGameData.CurrentSaveFormatId,
                StringComparison.Ordinal) &&
            save.SaveSchemaVersion == SaveGameData.CurrentSaveSchemaVersion &&
            save.ProfileInitializationVersion ==
                SaveGameData.CurrentProfileInitializationVersion;

    }
}
