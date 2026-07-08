using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalTrainingService : ITrainingService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;

        public LocalTrainingService(ISaveGameService saveGameService, IResourceService resourceService)
        {
            _saveGameService = saveGameService;
            _resourceService = resourceService;
        }

        public void StartTraining(TroopType type, int count)
        {
            long cost = count * 10; // Food cost
            if (_resourceService.ConsumeResource(ResourceType.Food, cost))
            {
                // Simple instant training for prototype, or logic for timers
                AddTroops(type, count);
                Debug.Log($"Trained {count} {type}");

                // Trigger Quest Update
                try
                {
                    ServiceLocator.Get<IQuestService>().UpdateProgress(QuestType.TrainTroops, count);
                }
                catch (Exception)
                {
                    // Quest service is optional in early scene tests.
                }

                _saveGameService.Save();
            }
        }

        public void CompleteTraining(TroopType type)
        {
            // Logic for timer-based training completion
        }

        public int GetTroopCount(TroopType type)
        {
            return GetTroopState(type)?.Count ?? 0;
        }

        private void AddTroops(TroopType type, int count)
        {
            var state = GetTroopState(type);
            if (state == null)
            {
                return;
            }

            state.Count += count;
        }

        private TroopInventoryData GetTroopState(TroopType type)
        {
            var save = _saveGameService.CurrentSave;
            if (save == null)
            {
                return null;
            }

            save.Troops ??= new List<TroopInventoryData>();
            var state = save.Troops.FirstOrDefault(t => t.Type == type);
            if (state == null)
            {
                state = new TroopInventoryData { Type = type, Count = 0, WoundedCount = 0 };
                save.Troops.Add(state);
            }

            return state;
        }
    }
}
