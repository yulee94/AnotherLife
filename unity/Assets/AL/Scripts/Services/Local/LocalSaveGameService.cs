using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    internal interface ISaveFileOperations
    {
        bool FileExists(string path);
        void CreateDirectory(string path);
        string ReadAllText(string path);
        void WriteAllTextDurable(string path, string contents);
        void Copy(string sourcePath, string destinationPath, bool overwrite);
        void Move(string sourcePath, string destinationPath);
        void Replace(string sourcePath, string destinationPath, string backupPath);
        void Delete(string path);
        IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern);
    }

    internal sealed class SystemSaveFileOperations : ISaveFileOperations
    {
        public bool FileExists(string path) => File.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public void WriteAllTextDurable(string path, string contents)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(contents);
            writer.Flush();
            stream.Flush(true);
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

    public class LocalSaveGameService : ISaveGameService
    {
        private const string SaveFileName = "save.json";
        private const string BackupFileName = "save.backup.json";
        private const string TempFileName = "save.tmp.json";
        private const string PreviousFileName = "save.previous.json";
        private const int MaxQuarantinesPerSource = 3;

        private enum SaveCandidateKind
        {
            Primary,
            Backup,
            Previous,
            Temp
        }

        private sealed class SaveCandidateInfo
        {
            public SaveCandidateInfo(SaveCandidateKind kind, string path, bool exists, bool isValid, SaveGameData save, string error)
            {
                Kind = kind;
                Path = path;
                Exists = exists;
                IsValid = isValid;
                Save = save;
                Error = error;
            }

            public SaveCandidateKind Kind { get; }
            public string Path { get; }
            public bool Exists { get; }
            public bool IsValid { get; }
            public SaveGameData Save { get; }
            public string Error { get; }
        }

        private readonly string _persistencePathOverride;
        private readonly ISaveFileOperations _fileOperations;

        private string PersistencePath => string.IsNullOrWhiteSpace(_persistencePathOverride)
            ? Application.persistentDataPath
            : _persistencePathOverride;
        private string SavePath => Path.Combine(PersistencePath, SaveFileName);
        private string BackupPath => Path.Combine(PersistencePath, BackupFileName);
        private string TempPath => Path.Combine(PersistencePath, TempFileName);
        private string PreviousPath => Path.Combine(PersistencePath, PreviousFileName);

        private SaveGameData _currentSave;

        public SaveGameData CurrentSave => _currentSave;
        public SaveLoadStatus LastLoadStatus { get; private set; }
        public string LastLoadMessage { get; private set; } = string.Empty;
        public SaveOperationStatus LastSaveStatus { get; private set; }
        public string LastSaveMessage { get; private set; } = string.Empty;
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
        {
            _persistencePathOverride = persistencePathOverride;
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
        }

        public void Save()
        {
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
            IReadOnlyList<SaveCandidateInfo> candidates = BuildCandidateInventory();
            SaveCandidateInfo primary = candidates.First(candidate => candidate.Kind == SaveCandidateKind.Primary);
            SaveCandidateInfo backup = candidates.First(candidate => candidate.Kind == SaveCandidateKind.Backup);
            SaveCandidateInfo previous = candidates.First(candidate => candidate.Kind == SaveCandidateKind.Previous);
            SaveCandidateInfo temp = candidates.First(candidate => candidate.Kind == SaveCandidateKind.Temp);

            if (primary.IsValid)
            {
                CompleteLoad(primary.Save, SaveLoadStatus.LoadedPrimary, "AL-SAVE-LOAD-PRIMARY: Game loaded from the primary save.");
                CleanTransientFilesAfterActiveRecovery(deletePrevious: true);
                return;
            }

            if (primary.Exists && !TryQuarantineInvalidFile(SavePath, out string primaryQuarantineError))
            {
                SaveCandidateInfo fallback = backup.IsValid ? backup : previous.IsValid ? previous : null;
                if (fallback != null)
                {
                    _currentSave = CloneSave(fallback.Save);
                    SetLoadStatus(
                        SaveLoadStatus.RecoveryFailed,
                        $"AL-SAVE-RECOVERY-FAILED: Primary save is invalid but could not be quarantined. {primaryQuarantineError}",
                        true);
                    return;
                }
            }
            else if (primary.Exists)
            {
                Debug.LogWarning($"AL-SAVE-PRIMARY-CORRUPT: Primary save was invalid and quarantined. {primary.Error}");
            }

            if (backup.IsValid)
            {
                RecoverFromBackup(backup.Save, BackupPath, deletePreviousAfterRecovery: true);
                return;
            }

            if (backup.Exists)
            {
                if (TryQuarantineInvalidFile(BackupPath, out string backupQuarantineError))
                {
                    Debug.LogWarning($"AL-SAVE-BACKUP-CORRUPT: Backup save was invalid and quarantined. {backup.Error}");
                }
                else
                {
                    _currentSave = null;
                    SetLoadStatus(
                        SaveLoadStatus.RecoveryFailed,
                        $"AL-SAVE-RECOVERY-FAILED: Backup save is invalid but could not be quarantined. {backupQuarantineError}",
                        true);
                    return;
                }
            }

            if (previous.IsValid)
            {
                RecoverFromBackup(previous.Save, PreviousPath, deletePreviousAfterRecovery: true);
                return;
            }

            if (previous.Exists)
            {
                if (TryQuarantineInvalidFile(PreviousPath, out string previousQuarantineError))
                {
                    Debug.LogWarning($"AL-SAVE-PREVIOUS-CORRUPT: Previous save was invalid and quarantined. {previous.Error}");
                }
                else
                {
                    _currentSave = null;
                    SetLoadStatus(
                        SaveLoadStatus.RecoveryFailed,
                        $"AL-SAVE-RECOVERY-FAILED: Previous save is invalid but could not be quarantined. {previousQuarantineError}",
                        true);
                    return;
                }
            }

            if (temp.Exists)
            {
                TryDelete(TempPath);
                if (temp.IsValid)
                {
                    Debug.LogWarning("AL-SAVE-TEMP-DISCARDED: Valid temporary save was discarded because temporary candidates are never active recovery sources.");
                }
            }

            bool hadUnrecoverableCorruption = primary.Exists || backup.Exists || previous.Exists;
            SaveGameData newSave = CreateDefaultSave(RealmId.None);
            _currentSave = newSave;

            if (!TryPersistCandidate(CloneSave(newSave), out SaveGameData persistedNewSave, out string createMessage))
            {
                _currentSave = newSave;
                SetLoadStatus(
                    SaveLoadStatus.RecoveryFailed,
                    $"AL-SAVE-RECOVERY-FAILED: Could not create a replacement profile after unrecoverable save state. {createMessage}",
                    true);
                return;
            }

            _currentSave = persistedNewSave;
            SetLoadStatus(
                hadUnrecoverableCorruption ? SaveLoadStatus.CreatedNewAfterUnrecoverableCorruption : SaveLoadStatus.CreatedNew,
                hadUnrecoverableCorruption
                    ? "AL-SAVE-NEW-AFTER-CORRUPTION: No valid save or backup could be recovered. A new profile was created and corrupt files were quarantined where possible."
                    : "AL-SAVE-CREATED-NEW: No save file was found. A new profile was created.",
                hadUnrecoverableCorruption);
        }

        public bool HasSave() =>
            _fileOperations.FileExists(SavePath) ||
            _fileOperations.FileExists(BackupPath) ||
            _fileOperations.FileExists(PreviousPath);

        public void CreateNewSave(RealmId realmId)
        {
            _currentSave = CreateDefaultSave(realmId);
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
            LastLoadStatus = SaveLoadStatus.None;
            LastLoadMessage = "AL-SAVE-DELETED: Local save data deleted.";
            LastSaveStatus = SaveOperationStatus.None;
            LastSaveMessage = string.Empty;
        }

        private void CompleteLoad(SaveGameData loadedSave, SaveLoadStatus status, string message)
        {
            SaveGameData original = CloneSave(loadedSave);
            SaveGameData candidate = CloneSave(loadedSave);
            ApplyOfflineProgress(candidate);

            if (!SavesAreEquivalent(original, candidate))
            {
                if (TryPersistCandidate(candidate, out SaveGameData persistedSave, out string saveMessage))
                {
                    _currentSave = persistedSave;
                    SetSaveStatus(SaveOperationStatus.SavedPrimary, saveMessage, false);
                }
                else
                {
                    _currentSave = original;
                    SetSaveStatus(SaveOperationStatus.SaveFailedPreviousPreserved, saveMessage, true);
                }
            }
            else
            {
                _currentSave = original;
            }

            SetLoadStatus(status, message, false);
        }

        private void RecoverFromBackup(SaveGameData backupSave, string sourcePath, bool deletePreviousAfterRecovery)
        {
            SaveGameData repaired = CloneSave(backupSave);

            if (!TryInstallBackupAsPrimary(repaired, sourcePath, out string repairMessage))
            {
                _currentSave = repaired;
                SetLoadStatus(SaveLoadStatus.RecoveryFailed, repairMessage, true);
                return;
            }

            CleanTransientFilesAfterActiveRecovery(deletePreviousAfterRecovery);
            CompleteLoad(repaired, SaveLoadStatus.RecoveredFromBackup, "AL-SAVE-RECOVERED-BACKUP: Recovered the profile from the last known-good backup.");
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
                _fileOperations.CreateDirectory(PersistencePath);
                if (!TryDelete(TempPath))
                {
                    message = "AL-SAVE-TEMP-CLEANUP-FAILED: Existing temporary save could not be removed before preparing a new candidate.";
                    return false;
                }

                EnsureSaveDefaults(candidate);
                candidate.LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(candidate, true);
                _fileOperations.WriteAllTextDurable(TempPath, json);

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

                TryDelete(TempPath);
                TryDelete(PreviousPath);
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
            try
            {
                TryDelete(PreviousPath);
                _fileOperations.Replace(TempPath, SavePath, PreviousPath);

                if (!TryReadValidSave(SavePath, out _, out string installedError))
                {
                    RestorePreviousPrimary();
                    message = $"AL-SAVE-ATOMIC-INSTALL-INVALID: Atomic-installed primary failed validation and previous primary was restored. {installedError}";
                    return false;
                }

                _fileOperations.Copy(PreviousPath, BackupPath, true);
                TryDelete(PreviousPath);
                message = string.Empty;
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                return TryInstallWithMoveFallback(out message);
            }
            catch (NotSupportedException)
            {
                return TryInstallWithMoveFallback(out message);
            }
            catch (Exception ex)
            {
                RestorePreviousPrimary();
                TryDelete(TempPath);
                message = $"AL-SAVE-REPLACE-FAILED: Atomic replace failed without using destructive fallback; previous save was preserved or restored. {ex.Message}";
                return false;
            }
        }

        private bool TryInstallWithMoveFallback(out string message)
        {
            try
            {
                TryDelete(PreviousPath);
                _fileOperations.Move(SavePath, PreviousPath);
                _fileOperations.Move(TempPath, SavePath);

                if (!TryReadValidSave(SavePath, out _, out string installedError))
                {
                    RestorePreviousPrimary();
                    message = $"AL-SAVE-FALLBACK-INSTALL-INVALID: Fallback-installed primary failed validation and previous primary was restored. {installedError}";
                    return false;
                }

                _fileOperations.Copy(PreviousPath, BackupPath, true);
                TryDelete(PreviousPath);
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                RestorePreviousPrimary();
                TryDelete(TempPath);
                message = $"AL-SAVE-FALLBACK-FAILED: Fallback install failed and previous primary was preserved. {ex.Message}";
                return false;
            }
        }

        private bool TryInstallBackupAsPrimary(SaveGameData backupSave, string sourcePath, out string message)
        {
            if (!HasCurrentSaveMetadata(backupSave))
            {
                message = "AL-SAVE-UNMIGRATED-RECOVERY-READ-ONLY: Legacy or unsupported backup metadata requires an explicit reviewed migration before installation; source files were preserved.";
                return false;
            }

            try
            {
                _fileOperations.CreateDirectory(PersistencePath);
                TryDelete(TempPath);
                EnsureSaveDefaults(backupSave);
                string backupJson = JsonUtility.ToJson(backupSave, true);
                _fileOperations.WriteAllTextDurable(TempPath, backupJson);

                if (!TryReadValidSave(TempPath, out _, out string tempError))
                {
                    TryDelete(TempPath);
                    message = $"AL-SAVE-RECOVERY-FAILED: Backup repair candidate failed validation. {tempError}";
                    return false;
                }

                if (_fileOperations.FileExists(SavePath))
                {
                    if (!TryQuarantineInvalidFile(SavePath, out string quarantineError))
                    {
                        message = $"AL-SAVE-RECOVERY-FAILED: Could not quarantine invalid primary before repairing it from {sourcePath}. {quarantineError}";
                        return false;
                    }
                }

                _fileOperations.Move(TempPath, SavePath);
                if (!TryReadValidSave(SavePath, out _, out string repairedError))
                {
                    message = $"AL-SAVE-RECOVERY-FAILED: Repaired primary failed validation. {repairedError}";
                    return false;
                }

                if (!string.Equals(sourcePath, BackupPath, StringComparison.OrdinalIgnoreCase))
                {
                    _fileOperations.Copy(SavePath, BackupPath, true);
                    if (!TryReadValidSave(BackupPath, out _, out string backupError))
                    {
                        message = $"AL-SAVE-RECOVERY-FAILED: Recreated backup failed validation after repairing primary from {sourcePath}. {backupError}";
                        return false;
                    }
                }

                message = $"AL-SAVE-RECOVERED-BACKUP: Repaired primary from {sourcePath}.";
                return true;
            }
            catch (Exception ex)
            {
                TryDelete(TempPath);
                message = $"AL-SAVE-RECOVERY-FAILED: Could not repair primary from backup. {ex.Message}";
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

                if (_fileOperations.FileExists(SavePath))
                {
                    TryDelete(SavePath);
                }

                _fileOperations.Move(PreviousPath, SavePath);
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

            if (!_fileOperations.FileExists(path))
            {
                error = "File does not exist.";
                return false;
            }

            try
            {
                string json = _fileOperations.ReadAllText(path);
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

                EnsureSaveDefaults(save);
                if (!ValidateSaveSemantics(save, out error))
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

        private IReadOnlyList<SaveCandidateInfo> BuildCandidateInventory()
        {
            return new[]
            {
                InspectCandidate(SaveCandidateKind.Primary, SavePath),
                InspectCandidate(SaveCandidateKind.Backup, BackupPath),
                InspectCandidate(SaveCandidateKind.Previous, PreviousPath),
                InspectCandidate(SaveCandidateKind.Temp, TempPath)
            };
        }

        private SaveCandidateInfo InspectCandidate(SaveCandidateKind kind, string path)
        {
            bool exists = _fileOperations.FileExists(path);
            if (!exists)
            {
                return new SaveCandidateInfo(kind, path, false, false, null, "File does not exist.");
            }

            bool valid = TryReadValidSave(path, out SaveGameData save, out string error);
            return new SaveCandidateInfo(kind, path, true, valid, save, error);
        }

        private void CleanTransientFilesAfterActiveRecovery(bool deletePrevious)
        {
            TryDelete(TempPath);
            if (deletePrevious)
            {
                TryDelete(PreviousPath);
            }
        }

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
                if (_fileOperations.FileExists(path))
                {
                    _fileOperations.Delete(path);
                }

                return !_fileOperations.FileExists(path);
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

        private static bool SavesAreEquivalent(SaveGameData left, SaveGameData right) =>
            JsonUtility.ToJson(left) == JsonUtility.ToJson(right);

        private static void ApplyOfflineProgress(SaveGameData save)
        {
            if (save == null || save.LastSavedTimestamp <= 0)
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long elapsedSeconds = Math.Max(0, now - save.LastSavedTimestamp);
            if (elapsedSeconds <= 0)
            {
                return;
            }

            long cappedSeconds = Math.Min(elapsedSeconds, 12 * 60 * 60);
            AddOfflineResource(save, ResourceType.Food, cappedSeconds * 4);
            AddOfflineResource(save, ResourceType.Wood, cappedSeconds * 2);
            AddOfflineResource(save, ResourceType.Stone, cappedSeconds);
            AddOfflineResource(save, ResourceType.Gold, cappedSeconds / 2);
            AddOfflineResource(save, ResourceType.ManaStone, cappedSeconds / 4);
            AddOfflineResource(save, ResourceType.Ore, cappedSeconds / 3);

            if (save.SelectedRealm != RealmId.None)
            {
                AddOfflineResource(save, ResourceRules.GetRareResourceForRealm(save.SelectedRealm), cappedSeconds / 90);
            }

            CompleteFinishedBuildingTimers(save, now);
            CompleteFinishedResearchTimers(save, now);
            Debug.Log($"AL-SAVE-OFFLINE-PROGRESS: Offline progress applied for {cappedSeconds} seconds.");
        }

        private static void AddOfflineResource(SaveGameData save, ResourceType type, long amount)
        {
            if (amount <= 0)
            {
                return;
            }

            foreach (var resource in save.Resources)
            {
                if (resource.Type == type)
                {
                    resource.Amount += amount;
                    return;
                }
            }

            save.Resources.Add(new ResourceData { Type = type, Amount = amount });
        }

        private static void CompleteFinishedBuildingTimers(SaveGameData save, long now)
        {
            foreach (var building in save.Buildings)
            {
                if (building.IsUpgrading && now >= building.UpgradeCompleteTimestamp)
                {
                    building.IsUpgrading = false;
                    building.Level++;
                }
            }
        }

        private static void CompleteFinishedResearchTimers(SaveGameData save, long now)
        {
            foreach (var research in save.Researches)
            {
                if (research.IsResearching && now >= research.CompleteTimestamp)
                {
                    research.IsResearching = false;
                    research.Level++;
                }
            }
        }
    }
}
