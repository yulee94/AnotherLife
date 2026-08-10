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
            if (count <= 0 ||
                !ProfileMutationContainment.TryGetMutableSave(
                    _saveGameService,
                    ProfileMutationSurfaceIds.Training,
                    out SaveGameData save))
            {
                return;
            }

            long cost = count * 10; // Food cost
            if (_resourceService.ConsumeResource(ResourceType.Food, cost))
            {
                // Simple instant training for prototype, or logic for timers
                AddTroops(save, type, count);
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
            return FindTroopState(_saveGameService?.CurrentSave, type)?.Count ?? 0;
        }

        private static void AddTroops(
            SaveGameData save,
            TroopType type,
            int count)
        {
            if (save == null)
            {
                return;
            }

            save.Troops ??= new List<TroopInventoryData>();
            TroopInventoryData state = FindTroopState(save, type);
            if (state == null)
            {
                state = new TroopInventoryData
                {
                    Type = type,
                    Count = 0,
                    WoundedCount = 0
                };
                save.Troops.Add(state);
            }

            state.Count += count;
        }

        private static TroopInventoryData FindTroopState(
            SaveGameData save,
            TroopType type) =>
            save?.Troops?.FirstOrDefault(state =>
                state != null && state.Type == type);
    }
}
