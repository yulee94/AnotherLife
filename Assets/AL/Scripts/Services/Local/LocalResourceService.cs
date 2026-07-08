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
        private readonly Dictionary<ResourceType, double> _productionRemainders = new Dictionary<ResourceType, double>();
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
            if (_saveGameService.CurrentSave == null || deltaSeconds <= 0)
            {
                return;
            }

            // Dynamic production logic based on building levels
            var buildingService = ServiceLocator.Get<IBuildingService>();

            int farmLevel = buildingService.GetBuildingState("Farm")?.Level ?? 1;
            int lumberMillLevel = buildingService.GetBuildingState("LumberMill")?.Level ?? 1;
            int quarryLevel = buildingService.GetBuildingState("Quarry")?.Level ?? 1;
            int goldMineLevel = buildingService.GetBuildingState("GoldMine")?.Level ?? 1;

            AddProducedResource(ResourceType.Food, 10 * farmLevel * deltaSeconds);
            AddProducedResource(ResourceType.Wood, 5 * lumberMillLevel * deltaSeconds);
            AddProducedResource(ResourceType.Stone, 2 * quarryLevel * deltaSeconds);
            AddProducedResource(ResourceType.Gold, 1 * goldMineLevel * deltaSeconds);
        }

        private void AddProducedResource(ResourceType type, double amount)
        {
            if (!_productionRemainders.ContainsKey(type))
            {
                _productionRemainders[type] = 0;
            }

            _productionRemainders[type] += amount;
            long wholeAmount = (long)_productionRemainders[type];
            if (wholeAmount <= 0)
            {
                return;
            }

            _productionRemainders[type] -= wholeAmount;
            AddResource(type, wholeAmount);
        }
    }
}
