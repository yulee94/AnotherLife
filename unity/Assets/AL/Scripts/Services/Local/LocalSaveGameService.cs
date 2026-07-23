using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Catalogs;
using AL.Data.Runtime;

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
    }

    public class LocalSaveGameService : ISaveGameService, ISaveLoadDispositionProvider
    {
        private const string SaveFileName = "save.json";
        private const string BackupFileName = "save.backup.json";
        private const string TempFileName = "save.tmp.json";
        private const string PreviousFileName = "save.previous.json";
        private const int MaxQuarantinesPerSource = 3;

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

        private SaveGameData _currentSave;
        private SaveGameData _readOnlyCandidate;
        private bool _profileWritable;

        public SaveGameData CurrentSave => _currentSave;
        public SaveLoadStatus LastLoadStatus { get; private set; }
        public string LastLoadMessage { get; private set; } = string.Empty;
        public SaveOperationStatus LastSaveStatus { get; private set; }
        public string LastSaveMessage { get; private set; } = string.Empty;
        public SaveLoadDisposition LastLoadDisposition { get; private set; }
        public SaveGameData ReadOnlyCandidateSnapshot => CloneSave(_readOnlyCandidate);
        public string LastPersistenceMessage => string.IsNullOrWhiteSpace(LastSaveMessage)
            ? LastLoadMessage
            : LastSaveMessage;

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

        public void Save()
        {
            if (!_profileWritable && LastLoadDisposition != null)
            {
                SetSaveStatus(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    "AL-SAVE-READ-ONLY-DISPOSITION: The selected save generation is read-only; all on-disk evidence was preserved.",
                    true);
                return;
            }

            if (_currentSave == null)
            {
                return;
            }

            if (TryPersistCandidate(CloneSave(_currentSave), out SaveGameData persistedSave, out string message))
            {
                _currentSave = persistedSave;
                SetSaveStatus(SaveOperationStatus.SavedPrimary, message, false);
                return;
            }

            SetSaveStatus(SaveOperationStatus.SaveFailedPreviousPreserved, message, true);
        }

        public void Load()
        {
            SaveGameData priorSave = _currentSave;
            _readOnlyCandidate = null;
            _profileWritable = false;
            LastSaveStatus = SaveOperationStatus.None;
            LastSaveMessage = string.Empty;

            IReadOnlyList<SaveCandidateInventoryEntry> inventory = BuildCandidateInventory();
            SaveCandidateInventoryEntry primary = Find(inventory, SaveCandidateSourceGeneration.Primary);
            SaveCandidateInventoryEntry backup = Find(inventory, SaveCandidateSourceGeneration.Backup);
            SaveCandidateInventoryEntry previous = Find(inventory, SaveCandidateSourceGeneration.Previous);

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

            bool runtimeUsable =
                selected.SourceGeneration == SaveCandidateSourceGeneration.Primary &&
                selected.IsWritable &&
                IsRuntimeRoundTrippable(selected.Outcome);
            bool writable = runtimeUsable &&
                !HasUnresolvedAuxiliaryEvidence(inventory);
            _profileWritable = writable;

            if (runtimeUsable)
            {
                _currentSave = selectedSave;
                _readOnlyCandidate = null;
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
                        : "AL-SAVE-LOAD-PRIMARY-COMPATIBLE: A round-trippable primary with preserved stable IDs was loaded without disk mutation or offline progression.",
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

        public bool HasSave() =>
            HasSaveEvidence(SavePath) ||
            HasSaveEvidence(BackupPath) ||
            HasSaveEvidence(PreviousPath) ||
            HasSaveEvidence(TempPath);

        public void CreateNewSave(RealmId realmId)
        {
            _currentSave = CreateDefaultSave(realmId);
            _readOnlyCandidate = null;
            _profileWritable = true;
            LastLoadDisposition = null;
            Save();
        }

        public void DeleteSave()
        {
            var deletionTargets = new List<string>
            {
                SavePath,
                BackupPath,
                TempPath,
                PreviousPath
            };

            deletionTargets.AddRange(EnumerateQuarantines(SaveFileName));
            deletionTargets.AddRange(EnumerateQuarantines(BackupFileName));

            var failures = new List<string>();
            foreach (string target in deletionTargets.Distinct().ToList())
            {
                if (!TryDelete(target))
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
            LastLoadDisposition = null;
            LastLoadStatus = SaveLoadStatus.None;
            LastLoadMessage = "AL-SAVE-DELETED: Local save data deleted.";
            LastSaveStatus = SaveOperationStatus.None;
            LastSaveMessage = string.Empty;
        }

        private bool TryPersistCandidate(SaveGameData candidate, out SaveGameData persistedSave, out string message)
        {
            persistedSave = null;

            if (!HasCurrentSaveMetadata(candidate))
            {
                message = "AL-SAVE-UNMIGRATED-READ-ONLY: Legacy or unsupported save metadata requires an explicit reviewed migration before persistence; existing files were preserved.";
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
                if (!TryDelete(TempPath))
                {
                    message = "AL-SAVE-TEMP-CLEANUP-FAILED: Existing temporary save could not be removed before preparing a new candidate.";
                    return false;
                }

                SaveFileWriteResult writeResult =
                    _fileOperations.WriteAllTextDurable(TempPath, json);
                if (!writeResult.Succeeded)
                {
                    message = $"AL-SAVE-TEMP-WRITE-FAILED: The temporary save could not be written durably. {writeResult.DiagnosticCode}";
                    return false;
                }

                if (!TryReadValidSave(TempPath, out SaveGameData tempSave, out string tempValidationError))
                {
                    TryDelete(TempPath);
                    message = $"AL-SAVE-TEMP-INVALID: Temporary save validation failed; previous save was preserved. {tempValidationError}";
                    return false;
                }

                bool primaryExists = _fileOperations.FileExists(SavePath);
                bool primaryValid = TryReadValidSave(SavePath, out _, out string primaryValidationError);

                if (primaryExists && !primaryValid)
                {
                    if (!TryQuarantineInvalidFile(SavePath, out string quarantineError))
                    {
                        TryDelete(TempPath);
                        message = $"AL-SAVE-PRIMARY-QUARANTINE-FAILED: Primary was invalid before save and could not be quarantined; backup was preserved. {quarantineError}";
                        return false;
                    }

                    Debug.LogWarning($"AL-SAVE-PRIMARY-CORRUPT: Invalid primary quarantined before installing a validated candidate. {primaryValidationError}");
                }

                if (primaryValid)
                {
                    if (!TryInstallWithAtomicReplace(out message))
                    {
                        return false;
                    }
                }
                else
                {
                    _fileOperations.Move(TempPath, SavePath);
                    if (!TryReadValidSave(SavePath, out _, out string installValidationError))
                    {
                        TryQuarantineInvalidFile(SavePath, out _);
                        message = $"AL-SAVE-INSTALL-INVALID: Installed primary failed validation after first-generation save. {installValidationError}";
                        return false;
                    }

                    _fileOperations.Copy(SavePath, BackupPath, true);
                }

                if (!TryReadValidSave(SavePath, out persistedSave, out string finalPrimaryError))
                {
                    message = $"AL-SAVE-FINAL-PRIMARY-INVALID: Installed primary failed final validation. {finalPrimaryError}";
                    return false;
                }

                string finalBackupError = "Backup file does not exist.";
                if (!_fileOperations.FileExists(BackupPath) ||
                    !TryReadValidSave(BackupPath, out _, out finalBackupError))
                {
                    message = $"AL-SAVE-FINAL-BACKUP-INVALID: Backup missing or invalid after save. {finalBackupError}";
                    return false;
                }

                PruneQuarantines(SaveFileName);
                PruneQuarantines(BackupFileName);
                message = $"AL-SAVE-SAVED-PRIMARY: Game saved safely to {SavePath}.";
                return true;
            }
            catch (Exception ex)
            {
                TryDelete(TempPath);
                message = $"AL-SAVE-FAILED-PREVIOUS-PRESERVED: Save failed; previous active files were preserved. {ex.Message}";
                return false;
            }
        }

        private bool TryInstallWithAtomicReplace(out string message)
        {
            bool primaryInstalled = false;
            try
            {
                if (!TryDelete(PreviousPath))
                {
                    message = "AL-SAVE-PREVIOUS-CLEANUP-FAILED: The stale previous generation could not be removed before atomic install.";
                    return false;
                }

                _fileOperations.Replace(TempPath, SavePath, PreviousPath);
                primaryInstalled = true;

                if (!TryReadValidSave(SavePath, out _, out string installedError))
                {
                    RestorePreviousPrimary();
                    message = $"AL-SAVE-ATOMIC-INSTALL-INVALID: Atomic-installed primary failed validation and previous primary was restored. {installedError}";
                    return false;
                }

                return TryRotatePreviousIntoBackup(true, out message);
            }
            catch (PlatformNotSupportedException) when (!primaryInstalled)
            {
                return TryInstallWithMoveFallback(out message);
            }
            catch (NotSupportedException) when (!primaryInstalled)
            {
                return TryInstallWithMoveFallback(out message);
            }
            catch (Exception ex)
            {
                if (!primaryInstalled)
                {
                    RestorePreviousPrimary();
                }

                message = primaryInstalled
                    ? $"AL-SAVE-BACKUP-ROTATION-FAILED: The validated primary was installed, but backup rotation stopped and all remaining generations were preserved. {ex.Message}"
                    : $"AL-SAVE-REPLACE-FAILED: Atomic replace failed without using destructive fallback; previous save was preserved or restored. {ex.Message}";
                return false;
            }
        }

        private bool TryInstallWithMoveFallback(out string message)
        {
            try
            {
                if (!TryDelete(PreviousPath))
                {
                    message = "AL-SAVE-PREVIOUS-CLEANUP-FAILED: The stale previous generation could not be removed before fallback install.";
                    return false;
                }

                _fileOperations.Move(SavePath, PreviousPath);
                _fileOperations.Move(TempPath, SavePath);

                if (!TryReadValidSave(SavePath, out _, out string installedError))
                {
                    RestorePreviousPrimary();
                    message = $"AL-SAVE-FALLBACK-INSTALL-INVALID: Fallback-installed primary failed validation and previous primary was restored. {installedError}";
                    return false;
                }

                return TryRotatePreviousIntoBackup(false, out message);
            }
            catch (Exception ex)
            {
                if (!_fileOperations.FileExists(SavePath) &&
                    _fileOperations.FileExists(PreviousPath))
                {
                    RestorePreviousPrimary();
                }

                message = $"AL-SAVE-FALLBACK-FAILED: Fallback install stopped and every remaining generation was preserved. {ex.Message}";
                return false;
            }
        }

        private bool TryRotatePreviousIntoBackup(
            bool useAtomicReplace,
            out string message)
        {
            try
            {
                if (!_fileOperations.FileExists(PreviousPath))
                {
                    message = "AL-SAVE-BACKUP-ROTATION-EVIDENCE-MISSING: The exact prior-primary generation was missing; the installed primary and remaining evidence were preserved.";
                    return false;
                }

                _fileOperations.Copy(PreviousPath, TempPath, false);
                if (!TryReadValidSave(TempPath, out _, out string stagedError))
                {
                    message = $"AL-SAVE-BACKUP-STAGE-INVALID: The exact prior-primary copy was not semantically writable, so the existing backup was preserved. {stagedError}";
                    return false;
                }

                if (!TryDelete(PreviousPath))
                {
                    message = "AL-SAVE-BACKUP-STAGE-CLEANUP-FAILED: The prior-primary source could not be released before bounded backup rotation; the existing backup was preserved.";
                    return false;
                }

                if (!_fileOperations.FileExists(BackupPath))
                {
                    return TryInstallStagedBackupWithoutExisting(out message);
                }

                if (useAtomicReplace)
                {
                    try
                    {
                        _fileOperations.Replace(TempPath, BackupPath, PreviousPath);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        return TryRotateStagedBackupWithMoves(out message);
                    }
                    catch (NotSupportedException)
                    {
                        return TryRotateStagedBackupWithMoves(out message);
                    }
                }
                else
                {
                    return TryRotateStagedBackupWithMoves(out message);
                }

                if (!TryReadValidSave(BackupPath, out _, out string backupError))
                {
                    message = $"AL-SAVE-BACKUP-INSTALL-UNCERTAIN: The rotated backup could not be verified; both it and the prior backup were preserved. {backupError}";
                    return false;
                }

                if (!TryDelete(PreviousPath))
                {
                    message = "AL-SAVE-BACKUP-CLEANUP-FAILED: The rotated backup is valid, but the prior backup could not be removed and remains preserved.";
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

        private bool TryInstallStagedBackupWithoutExisting(out string message)
        {
            try
            {
                _fileOperations.Move(TempPath, BackupPath);
                if (!TryReadValidSave(BackupPath, out _, out string backupError))
                {
                    message = $"AL-SAVE-BACKUP-RECREATE-INVALID: The recreated backup could not be verified; its staged bytes were preserved. {backupError}";
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

        private bool TryRotateStagedBackupWithMoves(out string message)
        {
            bool priorBackupMoved = false;
            bool stagedBackupInstalled = false;
            try
            {
                _fileOperations.Move(BackupPath, PreviousPath);
                priorBackupMoved = true;
                _fileOperations.Move(TempPath, BackupPath);
                stagedBackupInstalled = true;

                if (!TryReadValidSave(BackupPath, out _, out string backupError))
                {
                    message = $"AL-SAVE-BACKUP-FALLBACK-UNCERTAIN: The fallback-rotated backup could not be verified; both it and the prior backup were preserved. {backupError}";
                    return false;
                }

                if (!TryDelete(PreviousPath))
                {
                    message = "AL-SAVE-BACKUP-FALLBACK-CLEANUP-FAILED: The rotated backup is valid, but the prior backup could not be removed and remains preserved.";
                    return false;
                }

                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                if (priorBackupMoved && !stagedBackupInstalled)
                {
                    RestorePreviousBackup();
                }

                message = $"AL-SAVE-BACKUP-FALLBACK-FAILED: Every generation left by the fallback operation was preserved for explicit recovery. {ex.Message}";
                return false;
            }
        }

        private void RestorePreviousPrimary()
        {
            try
            {
                if (!_fileOperations.FileExists(PreviousPath))
                {
                    return;
                }

                _fileOperations.Copy(PreviousPath, SavePath, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"AL-SAVE-ROLLBACK-FAILED: Could not restore previous primary. {ex.Message}");
            }
        }

        private bool TryReadValidSave(string path, out SaveGameData save, out string error)
        {
            save = null;
            error = string.Empty;

            SaveFileReadResult readResult = _fileOperations.ReadAllBytesBounded(
                path,
                _semanticPolicy.MaximumInputBytes);
            if (readResult.Disposition != SaveFileReadDisposition.Read)
            {
                error = readResult.DiagnosticCode;
                return false;
            }

            try
            {
                SaveSemanticCandidate semanticCandidate =
                    SaveSemanticCandidateValidator.Validate(
                        readResult.Bytes,
                        SourceForPath(path),
                        _semanticPolicy);
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

                string json = StrictUtf8.GetString(readResult.Bytes);
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
                save.OwnedEquipment == null)
            {
                error = "Required top-level save collections or objects are null after normalization.";
                return false;
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
            save.LordPersona ??= new PersonaData();
            save.Wishgate ??= new WishgateState();
            save.Warmaster ??= new WarmasterState();
            save.Warmaster.UnlockedSetIds = RemoveNullStrings(save.Warmaster.UnlockedSetIds);
            save.Warmaster.PurchasedPieceIds = RemoveNullStrings(save.Warmaster.PurchasedPieceIds);
            save.ChampionCustomization ??= new ChampionCustomizationState();

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

        private SaveCandidateInventoryEntry InspectCandidate(
            SaveCandidateSourceGeneration source,
            string path)
        {
            SaveFileReadResult readResult = _fileOperations.ReadAllBytesBounded(
                path,
                _semanticPolicy.MaximumInputBytes);
            SaveSemanticCandidate candidate = readResult.Disposition == SaveFileReadDisposition.Read
                ? SaveSemanticCandidateValidator.Validate(readResult.Bytes, source, _semanticPolicy)
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
            _currentSave = newSave;
            _readOnlyCandidate = null;
            _profileWritable = true;

            if (!TryCreateFirstGenerationCandidate(
                    CloneSave(newSave),
                    out SaveGameData persistedNewSave,
                    out string createMessage,
                    out bool diskChanged))
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

        private void RestorePreviousBackup()
        {
            try
            {
                if (!_fileOperations.FileExists(PreviousPath))
                {
                    return;
                }

                _fileOperations.Copy(PreviousPath, BackupPath, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"AL-SAVE-BACKUP-ROLLBACK-FAILED: Could not restore the prior backup. {ex.Message}");
            }
        }

        private bool AllCanonicalPathsMissing(bool includeTemp)
        {
            if (HasSaveEvidence(SavePath) ||
                HasSaveEvidence(BackupPath) ||
                HasSaveEvidence(PreviousPath))
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

                if (entry.ReadResult.Disposition != SaveFileReadDisposition.Read ||
                    entry.SemanticCandidate == null ||
                    !entry.SemanticCandidate.IsWritable ||
                    !IsRuntimeRoundTrippable(entry.SemanticCandidate.Outcome))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRuntimeRoundTrippable(
            SaveSemanticCandidateOutcome outcome) =>
            outcome == SaveSemanticCandidateOutcome.Valid ||
            outcome == SaveSemanticCandidateOutcome.CompatiblePreservedUnknown;

        private void PruneQuarantines(string sourceFileName)
        {
            var quarantines = EnumerateQuarantines(sourceFileName)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.CreationTimeUtc)
                .ToList();

            foreach (var old in quarantines.Skip(MaxQuarantinesPerSource))
            {
                TryDelete(old.FullName);
                Debug.LogWarning($"AL-SAVE-QUARANTINE-PRUNED: Pruned old quarantine {old.FullName}.");
            }
        }

        private IEnumerable<string> EnumerateQuarantines(string sourceFileName) =>
            _fileOperations.EnumerateFiles(PersistencePath, $"{sourceFileName}.corrupt-*");

        private bool TryDelete(string path)
        {
            try
            {
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
                        "shield")
                });
            return new SaveSemanticValidationPolicy(
                SaveGameData.CurrentSaveFormatId,
                SaveGameData.CurrentSaveSchemaVersion,
                SaveGameData.CurrentProfileInitializationVersion,
                authority,
                SaveSemanticValidationPolicy.DefaultMaximumInputBytes);
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
