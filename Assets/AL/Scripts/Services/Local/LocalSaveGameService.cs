using UnityEngine;
using System.IO;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using System;

namespace AL.Services.Local
{
    public class LocalSaveGameService : ISaveGameService
    {
        private const string SaveFileName = "save.json";
        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private SaveGameData _currentSave;
        public SaveGameData CurrentSave => _currentSave;

        public void Save()
        {
            if (_currentSave == null) return;

            EnsureSaveDefaults(_currentSave);
            _currentSave.LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = JsonUtility.ToJson(_currentSave, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"Game Saved to: {SavePath}");
        }

        public void Load()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                _currentSave = JsonUtility.FromJson<SaveGameData>(json);
                EnsureSaveDefaults(_currentSave);
                CalculateOfflineProgress();
                Save();
                Debug.Log("Game Loaded");
            }
            else
            {
                Debug.Log("No save file found. Initializing defaults.");
                CreateNewSave(RealmId.None);
            }
        }

        public bool HasSave() => File.Exists(SavePath);

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
                    new ResourceData { Type = ResourceType.Gold, Amount = 500 }
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
            if (File.Exists(SavePath)) File.Delete(SavePath);
            _currentSave = null;
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
            save.Warmaster ??= new WarmasterState();
            save.ChampionCustomization ??= new ChampionCustomizationState();

            EnsureResource(save, ResourceType.Food, 1000);
            EnsureResource(save, ResourceType.Wood, 1000);
            EnsureResource(save, ResourceType.Stone, 500);
            EnsureResource(save, ResourceType.Gold, 500);

            if (string.IsNullOrWhiteSpace(save.CurrentChapterId))
            {
                save.CurrentChapterId = "C1";
            }
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
                if (resource.Type == type)
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
                if (building.IsUpgrading && now >= building.UpgradeCompleteTimestamp)
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
                if (research.IsResearching && now >= research.CompleteTimestamp)
                {
                    research.IsResearching = false;
                    research.Level++;
                }
            }
        }
    }
}
