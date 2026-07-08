using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalBuildingService : IBuildingService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;
        private readonly IGameDataService _gameDataService;

        public LocalBuildingService(ISaveGameService saveGameService, IResourceService resourceService, IGameDataService gameDataService)
        {
            _saveGameService = saveGameService;
            _resourceService = resourceService;
            _gameDataService = gameDataService;
        }

        private List<BuildingState> Buildings => _saveGameService.CurrentSave?.Buildings;

        public BuildingState GetBuildingState(string buildingId)
        {
            if (Buildings == null) return null;
            var state = Buildings.FirstOrDefault(b => b.BuildingId == buildingId);
            if (state == null)
            {
                state = new BuildingState { BuildingId = buildingId, Level = 1 };
                Buildings.Add(state);
            }
            return state;
        }

        public IEnumerable<BuildingState> GetAllBuildingStates()
        {
            return Buildings ?? Enumerable.Empty<BuildingState>();
        }

        public void StartUpgrade(string buildingId)
        {
            var state = GetBuildingState(buildingId);
            if (state.IsUpgrading) return;

            // Calculate cost (placeholder logic)
            long cost = state.Level * 100;
            if (_resourceService.ConsumeResource(Core.ResourceType.Stone, cost))
            {
                state.IsUpgrading = true;
                // 10 seconds per level for prototype
                state.UpgradeCompleteTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (state.Level * 10);
                Debug.Log($"Started upgrade for {buildingId}. Completes in {state.Level * 10}s");
                _saveGameService.Save();
            }
            else
            {
                Debug.LogWarning("Not enough Stone to upgrade building.");
            }
        }

        public void CompleteUpgrade(string buildingId)
        {
            var state = GetBuildingState(buildingId);
            if (!state.IsUpgrading) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now >= state.UpgradeCompleteTimestamp)
            {
                state.Level++;
                state.IsUpgrading = false;
                Debug.Log($"Building {buildingId} upgraded to Level {state.Level}");

                // Trigger Quest Update
                try
                {
                    ServiceLocator.Get<IQuestService>().UpdateProgress(Core.QuestType.BuildBuilding, 1);
                }
                catch (Exception)
                {
                    // Quest service is optional in early scene tests.
                }

                _saveGameService.Save();
            }
        }
    }
}
