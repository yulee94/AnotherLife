using System;
using System.Collections.Generic;
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

        // In a real save, these would be persisted in SaveGameData
        private Dictionary<TroopType, int> _troops = new Dictionary<TroopType, int>();

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
                ServiceLocator.Get<IQuestService>().UpdateProgress(QuestType.TrainTroops, count);
            }
        }

        public void CompleteTraining(TroopType type)
        {
            // Logic for timer-based training completion
        }

        public int GetTroopCount(TroopType type)
        {
            return _troops.TryGetValue(type, out int count) ? count : 0;
        }

        private void AddTroops(TroopType type, int count)
        {
            if (_troops.ContainsKey(type)) _troops[type] += count;
            else _troops[type] = count;
        }
    }
}
