using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalResourceService : IResourceService
    {
        private readonly ISaveGameService _saveGameService;
        public event Action<ResourceType, long> OnResourceChanged;

        public LocalResourceService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        private List<ResourceData> Wallet => _saveGameService.CurrentSave?.Resources;

        public long GetResourceCount(ResourceType type)
        {
            if (Wallet == null) return 0;
            var data = Wallet.FirstOrDefault(r => r.Type == type);
            return data?.Amount ?? 0;
        }

        public void AddResource(ResourceType type, long amount)
        {
            if (_saveGameService.CurrentSave == null) return;

            var data = Wallet.FirstOrDefault(r => r.Type == type);
            if (data == null)
            {
                data = new ResourceData { Type = type, Amount = 0 };
                Wallet.Add(data);
            }

            data.Amount += amount;
            OnResourceChanged?.Invoke(type, data.Amount);
        }

        public bool ConsumeResource(ResourceType type, long amount)
        {
            if (!HasEnough(type, amount)) return false;

            var data = Wallet.First(r => r.Type == type);
            data.Amount -= amount;
            OnResourceChanged?.Invoke(type, data.Amount);
            return true;
        }

        public bool HasEnough(ResourceType type, long amount)
        {
            return GetResourceCount(type) >= amount;
        }

        public void TickProduction(double deltaSeconds)
        {
            // Dynamic production logic based on building levels
            var buildingService = ServiceLocator.Get<IBuildingService>();

            int farmLevel = buildingService.GetBuildingState("Farm")?.Level ?? 1;
            int lumberMillLevel = buildingService.GetBuildingState("LumberMill")?.Level ?? 1;
            int quarryLevel = buildingService.GetBuildingState("Quarry")?.Level ?? 1;
            int goldMineLevel = buildingService.GetBuildingState("GoldMine")?.Level ?? 1;

            AddResource(ResourceType.Food, (long)(10 * farmLevel * deltaSeconds));
            AddResource(ResourceType.Wood, (long)(5 * lumberMillLevel * deltaSeconds));
            AddResource(ResourceType.Stone, (long)(2 * quarryLevel * deltaSeconds));
            AddResource(ResourceType.Gold, (long)(1 * goldMineLevel * deltaSeconds));
        }
    }
}
