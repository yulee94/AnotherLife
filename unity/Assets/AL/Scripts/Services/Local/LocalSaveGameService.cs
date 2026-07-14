using UnityEngine;
using System.IO;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using System;
using System.Text;

namespace AL.Services.Local
{
    public enum SaveLoadStatus
    {
        None,
        LoadedPrimary,
        RecoveredFromBackup,
        CreatedNew,
        CreatedNewAfterUnrecoverableCorruption
    }

    public class LocalSaveGameService : ISaveGameService
    {
        private const string SaveFileName = "save.json";
        private const string BackupFileName = "save.backup.json";
        private const string TempFileName = "save.tmp.json";

        private readonly string _persistencePathOverride;

        private string PersistencePath => string.IsNullOrWhiteSpace(_persistencePathOverride)
            ? Application.persistentDataPath
            : _persistencePathOverride;
        private string SavePath => Path.Combine(PersistencePath, SaveFileName);
        private string BackupPath => Path.Combine(PersistencePath, BackupFileName);
        private string TempPath => Path.Combine(PersistencePath, TempFileName);

        private SaveGameData _currentSave;
        public SaveGameData CurrentSave => _currentSave;
        public SaveLoadStatus LastLoadStatus { get; private set; }
        public string LastPersistenceMessage { get; private set; } = string.Empty;

        public LocalSaveGameService() : this(null)
        {
        }

        internal LocalSaveGameService(string persistencePathOverride)
        {
            _persistencePathOverride = persistencePathOverride;
        }

        public void Save()
        {
            if (_currentSave == null)
            {
                return;
            }

            try
            {
                EnsureSaveDefaults(_currentSave);
                _currentSave.LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(_currentSave, true);
                WriteSaveAtomically(json);
                LastPersistenceMessage = $"Game saved safely to {SavePath}.";
                Debug.Log(LastPersistenceMessage);
            }
            catch (Exception ex)
            {
                TryDelete(TempPath);
                LastPersistenceMessage = $"Save failed; the previous save was preserved. {ex.Message}";
                Debug.LogError(LastPersistenceMessage);
            }
        }

        public void Load()
        {
            bool primaryExists = File.Exists(SavePath);
            bool backupExists = File.Exists(BackupPath);

            if (TryReadSave(SavePath, out SaveGameData primarySave, out string primaryError))
            {
                CompleteLoad(primarySave, SaveLoadStatus.LoadedPrimary, "Game loaded from the primary save.");
                return;
            }

            if (primaryExists)
            {
                Debug.LogWarning($"Primary save is invalid and will be quarantined: {primaryError}");
                QuarantineInvalidFile(SavePath);
            }

            if (TryReadSave(BackupPath, out SaveGameData backupSave, out string backupError))
            {
                _currentSave = backupSave;
                EnsureSaveDefaults(_currentSave);
                CalculateOfflineProgress();
                LastLoadStatus = SaveLoadStatus.RecoveredFromBackup;
                LastPersistenceMessage = "Recovered the profile from the last known-good backup.";
                Debug.LogWarning(LastPersistenceMessage);
                Save();
                return;
            }

            if (backupExists)
            {
                Debug.LogError($"Backup save is also invalid and will be quarantined: {backupError}");
                QuarantineInvalidFile(BackupPath);
            }

            bool hadUnrecoverableCorruption = primaryExists || backupExists;
            CreateNewSave(RealmId.None);
            LastLoadStatus = hadUnrecoverableCorruption
                ? SaveLoadStatus.CreatedNewAfterUnrecoverableCorruption
                : SaveLoadStatus.CreatedNew;
            LastPersistenceMessage = hadUnrecoverableCorruption
                ? "No valid save or backup could be recovered. A new profile was created and the corrupt files were quarantined."
                : "No save file was found. A new profile was created.";

            if (hadUnrecoverableCorruption)
            {
                Debug.LogError(LastPersistenceMessage);
            }
            else
            {
                Debug.Log(LastPersistenceMessage);
            }
        }

        public bool HasSave() => File.Exists(SavePath) || File.Exists(BackupPath);

        public void CreateNewSave(RealmId realmId)
        {
            _currentSave = new SaveGameData
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
            EnsureSaveDefaults(_currentSave);
            Save();
        }

        public void DeleteSave()
        {
            TryDelete(SavePath);
            TryDelete(BackupPath);
            TryDelete(TempPath);
            _currentSave = null;
            LastLoadStatus = SaveLoadStatus.None;
            LastPersistenceMessage = "Local save data deleted.";
        }

        private void CompleteLoad(SaveGameData save, SaveLoadStatus status, string message)
        {
            _currentSave = save;
            EnsureSaveDefaults(_currentSave);
            CalculateOfflineProgress();
            LastLoadStatus = status;
            LastPersistenceMessage = message;
            Save();
            Debug.Log(message);
        }

        private void WriteSaveAtomically(string json)
        {
            Directory.CreateDirectory(PersistencePath);
            TryDelete(TempPath);

            using (var stream = new FileStream(TempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            if (!TryReadSave(TempPath, out _, out string validationError))
            {
                throw new InvalidDataException($"Temporary save validation failed: {validationError}");
            }

            if (!File.Exists(SavePath))
            {
                File.Move(TempPath, SavePath);
                return;
            }

            File.Copy(SavePath, BackupPath, true);

            try
            {
                File.Replace(TempPath, SavePath, null);
            }
            catch (PlatformNotSupportedException)
            {
                ReplacePrimaryWithMoveFallback();
            }
            catch (NotSupportedException)
            {
                ReplacePrimaryWithMoveFallback();
            }
            catch (IOException)
            {
                ReplacePrimaryWithMoveFallback();
            }
        }

        private void ReplacePrimaryWithMoveFallback()
        {
            string previousPath = SavePath + ".previous";
            TryDelete(previousPath);
            File.Move(SavePath, previousPath);

            try
            {
                File.Move(TempPath, SavePath);
                TryDelete(previousPath);
            }
            catch
            {
                if (!File.Exists(SavePath) && File.Exists(previousPath))
                {
                    File.Move(previousPath, SavePath);
                }

                throw;
            }
        }

        private static bool TryReadSave(string path, out SaveGameData save, out string error)
        {
            save = null;
            error = string.Empty;

            if (!File.Exists(path))
            {
                error = "File does not exist.";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
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
                return true;
            }
            catch (Exception ex)
            {
                save = null;
                error = ex.Message;
                return false;
            }
        }

        private static void EnsureSaveDefaults(SaveGameData save)
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

        private static void EnsureResource(SaveGameData save, ResourceType type, long startingAmount)
        {
            foreach (var resource in save.Resources)
            {
                if (resource != null && resource.Type == type)
                {
                    return;
                }
            }

            save.Resources.Add(new ResourceData { Type = type, Amount = startingAmount });
        }

        private void QuarantineInvalidFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            string suffix = $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            string quarantinePath = path + suffix;

            try
            {
                File.Move(path, quarantinePath);
                Debug.LogWarning($"Quarantined invalid save file to {quarantinePath}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not quarantine invalid save file {path}: {ex.Message}");
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not delete temporary save file {path}: {ex.Message}");
            }
        }

        private void CalculateOfflineProgress()
        {
            if (_currentSave == null || _currentSave.LastSavedTimestamp <= 0)
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long elapsedSeconds = Math.Max(0, now - _currentSave.LastSavedTimestamp);
            if (elapsedSeconds <= 0)
            {
                return;
            }

            long cappedSeconds = Math.Min(elapsedSeconds, 12 * 60 * 60);
            AddOfflineResource(ResourceType.Food, cappedSeconds * 4);
            AddOfflineResource(ResourceType.Wood, cappedSeconds * 2);
            AddOfflineResource(ResourceType.Stone, cappedSeconds);
            AddOfflineResource(ResourceType.Gold, cappedSeconds / 2);
            AddOfflineResource(ResourceType.ManaStone, cappedSeconds / 4);
            AddOfflineResource(ResourceType.Ore, cappedSeconds / 3);

            if (_currentSave.SelectedRealm != RealmId.None)
            {
                AddOfflineResource(ResourceRules.GetRareResourceForRealm(_currentSave.SelectedRealm), cappedSeconds / 90);
            }

            CompleteFinishedBuildingTimers(now);
            CompleteFinishedResearchTimers(now);
            Debug.Log($"Offline progress applied for {cappedSeconds} seconds.");
        }

        private void AddOfflineResource(ResourceType type, long amount)
        {
            if (amount <= 0)
            {
                return;
            }

            foreach (var resource in _currentSave.Resources)
            {
                if (resource != null && resource.Type == type)
                {
                    resource.Amount += amount;
                    return;
                }
            }

            _currentSave.Resources.Add(new ResourceData { Type = type, Amount = amount });
        }

        private void CompleteFinishedBuildingTimers(long now)
        {
            foreach (var building in _currentSave.Buildings)
            {
                if (building != null && building.IsUpgrading && now >= building.UpgradeCompleteTimestamp)
                {
                    building.IsUpgrading = false;
                    building.Level++;
                }
            }
        }

        private void CompleteFinishedResearchTimers(long now)
        {
            foreach (var research in _currentSave.Researches)
            {
                if (research != null && research.IsResearching && now >= research.CompleteTimestamp)
                {
                    research.IsResearching = false;
                    research.Level++;
                }
            }
        }
    }
}
