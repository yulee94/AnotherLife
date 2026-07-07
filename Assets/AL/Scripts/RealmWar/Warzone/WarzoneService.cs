using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using UnityEngine;
using System;

namespace AL.RealmWar.Warzone
{
    public class WarzoneService : ITerritoryService
    {
        private List<TerritoryData> _territories = new List<TerritoryData>();
        public event Action<string, RealmId> OnTerritoryCaptured;

        public WarzoneService()
        {
            InitializeWarzone();
        }

        private void InitializeWarzone()
        {
            _territories.Add(new TerritoryData { Id = "T1", Name = "Iron Peaks", OwnerRealm = RealmId.Stonehold, BonusType = ResourceType.Stone, BonusAmount = 50, IsFortress = true });
            _territories.Add(new TerritoryData { Id = "T2", Name = "Silver Woods", OwnerRealm = RealmId.Eldergrove, BonusType = ResourceType.Wood, BonusAmount = 40, IsFortress = false });
            _territories.Add(new TerritoryData { Id = "T3", Name = "Golden Plains", OwnerRealm = RealmId.Crownlands, BonusType = ResourceType.Gold, BonusAmount = 20, IsFortress = false });
            _territories.Add(new TerritoryData { Id = "T4", Name = "Shadow Vale", OwnerRealm = RealmId.Umbral, BonusType = ResourceType.Food, BonusAmount = 60, IsFortress = true });
            _territories.Add(new TerritoryData { Id = "T5", Name = "Neutral Borderlands", OwnerRealm = RealmId.None, BonusType = ResourceType.Gold, BonusAmount = 10, IsFortress = false });
        }

        public IEnumerable<TerritoryData> GetTerritories() => _territories;

        public void CaptureTerritory(string territoryId, RealmId capturer)
        {
            var territory = _territories.FirstOrDefault(t => t.Id == territoryId);
            if (territory != null)
            {
                territory.OwnerRealm = capturer;
                OnTerritoryCaptured?.Invoke(territoryId, capturer);
                Debug.Log($"Territory {territory.Name} captured by {capturer}");
            }
        }

        public long CalculatePassiveIncome(ResourceType type)
        {
            // Simple logic for the vertical slice
            return _territories.Where(t => t.BonusType == type).Sum(t => t.BonusAmount);
        }
    }
}
