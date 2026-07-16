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
            if (!TryNormalizeWallet(out var wallet)) return 0;
            var data = wallet.FirstOrDefault(r => r.Type == type);
            return data?.Amount ?? 0;
        }

        public void AddResource(ResourceType type, long amount)
        {
            if (_saveGameService.CurrentSave == null) return;
            if (amount == 0)
            {
                return;
            }

            if (amount < 0)
            {
                Debug.LogWarning($"[AL-ECO-INVALID-AMOUNT] AddResource rejected negative amount {amount} for {type}.");
                return;
            }

            if (!TryNormalizeWallet(out var wallet))
            {
                return;
            }

            var data = wallet.FirstOrDefault(r => r.Type == type);
            if (data == null)
            {
                data = new ResourceData { Type = type, Amount = 0 };
                wallet.Add(data);
            }

            long finalAmount;
            try
            {
                finalAmount = checked(data.Amount + amount);
            }
            catch (OverflowException)
            {
                Debug.LogWarning($"[AL-ECO-OVERFLOW] AddResource rejected overflow for {type}: {data.Amount} + {amount}.");
                return;
            }

            data.Amount = finalAmount;
            OnResourceChanged?.Invoke(type, data.Amount);
        }

        public bool ConsumeResource(ResourceType type, long amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[AL-ECO-INVALID-AMOUNT] ConsumeResource rejected non-positive amount {amount} for {type}.");
                return false;
            }

            if (!TryNormalizeWallet(out var wallet)) return false;

            var data = wallet.FirstOrDefault(r => r.Type == type);
            if (data == null || data.Amount < amount) return false;

            data.Amount = checked(data.Amount - amount);
            OnResourceChanged?.Invoke(type, data.Amount);
            return true;
        }

        public bool HasEnough(ResourceType type, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }

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
            int manaShrineLevel = buildingService.GetBuildingState("ManaShrine")?.Level ?? 1;
            int mineLevel = buildingService.GetBuildingState("Mine")?.Level ?? 1;
            int townHallLevel = buildingService.GetBuildingState("TownHall")?.Level ?? 1;

            AddProducedResource(ResourceType.Food, 10 * farmLevel * deltaSeconds);
            AddProducedResource(ResourceType.Wood, 5 * lumberMillLevel * deltaSeconds);
            AddProducedResource(ResourceType.Stone, 2 * quarryLevel * deltaSeconds);
            AddProducedResource(ResourceType.Gold, 1 * goldMineLevel * deltaSeconds);
            AddProducedResource(ResourceType.ManaStone, 0.35 * manaShrineLevel * deltaSeconds);
            AddProducedResource(ResourceType.Ore, 0.45 * mineLevel * deltaSeconds);
            AddRealmRareResource(townHallLevel, deltaSeconds);

            AddTerritoryIncome(deltaSeconds);
        }

        private void AddTerritoryIncome(double deltaSeconds)
        {
            ITerritoryService territoryService;
            try
            {
                territoryService = ServiceLocator.Get<ITerritoryService>();
            }
            catch (Exception)
            {
                return;
            }

            AddProducedResource(ResourceType.Food, territoryService.CalculatePassiveIncome(ResourceType.Food) * deltaSeconds / 60.0);
            AddProducedResource(ResourceType.Wood, territoryService.CalculatePassiveIncome(ResourceType.Wood) * deltaSeconds / 60.0);
            AddProducedResource(ResourceType.Stone, territoryService.CalculatePassiveIncome(ResourceType.Stone) * deltaSeconds / 60.0);
            AddProducedResource(ResourceType.Gold, territoryService.CalculatePassiveIncome(ResourceType.Gold) * deltaSeconds / 60.0);
            AddProducedResource(ResourceType.ManaStone, territoryService.CalculatePassiveIncome(ResourceType.ManaStone) * deltaSeconds / 60.0);
            AddProducedResource(ResourceType.Ore, territoryService.CalculatePassiveIncome(ResourceType.Ore) * deltaSeconds / 60.0);
        }

        private void AddRealmRareResource(int townHallLevel, double deltaSeconds)
        {
            var realmId = _saveGameService.CurrentSave?.SelectedRealm ?? RealmId.None;
            if (realmId == RealmId.None)
            {
                return;
            }

            double amount = 0.015 * Math.Max(1, townHallLevel) * deltaSeconds;
            AddProducedResource(ResourceRules.GetRareResourceForRealm(realmId), amount);
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

        private bool TryNormalizeWallet(out List<ResourceData> wallet)
        {
            wallet = null;
            if (_saveGameService.CurrentSave == null)
            {
                return false;
            }

            _saveGameService.CurrentSave.Resources ??= new List<ResourceData>();
            wallet = _saveGameService.CurrentSave.Resources;

            for (int index = wallet.Count - 1; index >= 0; index--)
            {
                if (wallet[index] == null)
                {
                    wallet.RemoveAt(index);
                    Debug.LogWarning("[AL-ECO-NULL-ENTRY] Removed null resource entry from wallet compatibility view.");
                }
            }

            for (int index = wallet.Count - 1; index >= 0; index--)
            {
                if (wallet[index].Amount < 0)
                {
                    Debug.LogWarning($"[AL-ECO-NEGATIVE-BALANCE] Repaired negative {wallet[index].Type} balance {wallet[index].Amount} to 0.");
                    wallet[index].Amount = 0;
                }
            }

            var canonical = new Dictionary<ResourceType, ResourceData>();
            for (int index = 0; index < wallet.Count; index++)
            {
                var entry = wallet[index];
                if (!canonical.TryGetValue(entry.Type, out var existing))
                {
                    canonical[entry.Type] = entry;
                    continue;
                }

                long repairedAmount;
                try
                {
                    repairedAmount = checked(existing.Amount + entry.Amount);
                }
                catch (OverflowException)
                {
                    Debug.LogWarning($"[AL-ECO-DUPLICATE-OVERFLOW] Duplicate {entry.Type} balance overflowed during repair; clamped to long.MaxValue.");
                    repairedAmount = long.MaxValue;
                }

                existing.Amount = repairedAmount;
                wallet.RemoveAt(index);
                index--;
                Debug.LogWarning($"[AL-ECO-DUPLICATE-RESOURCE] Merged duplicate {entry.Type} resource entry into the first wallet entry.");
            }

            return true;
        }
    }
}
