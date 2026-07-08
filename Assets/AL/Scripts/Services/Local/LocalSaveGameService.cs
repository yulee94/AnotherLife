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
                Quests = new List<QuestState>(),
                CurrentChapterId = "C1",
                Warmaster = new WarmasterState()
            };
            Save();
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            _currentSave = null;
        }
    }
}
