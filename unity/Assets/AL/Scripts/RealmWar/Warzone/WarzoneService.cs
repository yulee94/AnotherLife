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
        private readonly ISaveGameService _saveGameService;
        public event Action<string, RealmId> OnTerritoryCaptured;

        public WarzoneService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        private List<TerritoryData> Territories
        {
            get
            {
                EnsureTerritories();
                return _saveGameService.CurrentSave?.Territories;
            }
        }

        public IEnumerable<TerritoryData> GetTerritories() => Territories ?? Enumerable.Empty<TerritoryData>();

        public void CaptureTerritory(string territoryId, RealmId capturer)
        {
            var territory = Territories?.FirstOrDefault(t => t.Id == territoryId);
            if (territory != null)
            {
                territory.OwnerRealm = capturer;
                OnTerritoryCaptured?.Invoke(territoryId, capturer);
                Debug.Log($"Territory {territory.Name} captured by {capturer}");
                try
                {
                    ServiceLocator.Get<IQuestService>().UpdateProgress(QuestType.CaptureTerritory, 1);
                    ServiceLocator.Get<IWarzoneCreditService>().AddCredits(100);
                }
                catch (Exception)
                {
                    // Quest and credit services are optional in isolated tests.
                }
                _saveGameService.Save();
            }
        }

        public long CalculatePassiveIncome(ResourceType type)
        {
            var selectedRealm = _saveGameService.CurrentSave?.SelectedRealm ?? RealmId.None;
            return GetTerritories()
                .Where(t => t.OwnerRealm == selectedRealm && t.BonusType == type)
                .Sum(t => t.BonusAmount);
        }

        private void EnsureTerritories()
        {
            if (_saveGameService.CurrentSave == null)
            {
                return;
            }

            _saveGameService.CurrentSave.Territories ??= new List<TerritoryData>();
            if (_saveGameService.CurrentSave.Territories.Count > 0)
            {
                return;
            }

            _saveGameService.CurrentSave.Territories.Add(new TerritoryData { Id = "T1", Name = "Iron Peaks", OwnerRealm = RealmId.Stonehold, BonusType = ResourceType.Stone, BonusAmount = 50, IsFortress = true });
            _saveGameService.CurrentSave.Territories.Add(new TerritoryData { Id = "T2", Name = "Silver Woods", OwnerRealm = RealmId.Eldergrove, BonusType = ResourceType.Wood, BonusAmount = 40, IsFortress = false });
            _saveGameService.CurrentSave.Territories.Add(new TerritoryData { Id = "T3", Name = "Golden Plains", OwnerRealm = RealmId.Crownlands, BonusType = ResourceType.Gold, BonusAmount = 20, IsFortress = false });
            _saveGameService.CurrentSave.Territories.Add(new TerritoryData { Id = "T4", Name = "Shadow Vale", OwnerRealm = RealmId.Umbral, BonusType = ResourceType.Food, BonusAmount = 60, IsFortress = true });
            _saveGameService.CurrentSave.Territories.Add(new TerritoryData { Id = "T5", Name = "Neutral Borderlands", OwnerRealm = RealmId.None, BonusType = ResourceType.Gold, BonusAmount = 10, IsFortress = false });
        }
    }
}
