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
        private readonly AL.Services.Local.EconomyWriteAuthorityGate
            _writeAuthorityGate;
        public event Action<string, RealmId> OnTerritoryCaptured;

        public WarzoneService(ISaveGameService saveGameService)
            : this(
                saveGameService,
                AL.Services.Local.EconomyWriteAuthorityGate.FromSaveService(
                    saveGameService))
        {
        }

        private WarzoneService(
            ISaveGameService saveGameService,
            AL.Services.Local.EconomyWriteAuthorityGate writeAuthorityGate)
        {
            _saveGameService = saveGameService ??
                throw new ArgumentNullException(nameof(saveGameService));
            _writeAuthorityGate = writeAuthorityGate ??
                throw new ArgumentNullException(nameof(writeAuthorityGate));
        }

        private List<TerritoryData> Territories =>
            _saveGameService.CurrentSave?.Territories;

        public IEnumerable<TerritoryData> GetTerritories()
        {
            List<TerritoryData> territories = Territories;
            return territories == null
                ? Array.Empty<TerritoryData>()
                : territories
                    .Where(territory => territory != null)
                    .Select(CloneTerritory)
                    .ToArray();
        }

        public void CaptureTerritory(string territoryId, RealmId capturer)
        {
            if (!_writeAuthorityGate.TryGetWritableSave(out var writableSave))
            {
                Debug.LogWarning(
                    "[AL-WARZONE-PROFILE-READ-ONLY] Territory capture rejected before any profile mutation.");
                return;
            }

            if (!EnsureTerritories(writableSave) ||
                !_writeAuthorityGate.IsWritableFor(writableSave))
            {
                return;
            }

            var territory = writableSave.Territories?
                .FirstOrDefault(t => t?.Id == territoryId);
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
            List<TerritoryData> territories = Territories;
            if (territories == null)
            {
                return 0;
            }

            long total = 0;
            for (int index = 0; index < territories.Count; index++)
            {
                TerritoryData territory = territories[index];
                if (territory != null &&
                    territory.OwnerRealm == selectedRealm &&
                    territory.BonusType == type)
                {
                    total = checked(total + territory.BonusAmount);
                }
            }

            return total;
        }

        private bool EnsureTerritories(AL.Data.Runtime.SaveGameData save)
        {
            if (save == null)
            {
                return false;
            }

            if (save.Territories != null && save.Territories.Count > 0)
            {
                return true;
            }

            if (!_writeAuthorityGate.IsWritableFor(save))
            {
                return false;
            }

            save.Territories ??= new List<TerritoryData>();

            save.Territories.Add(new TerritoryData { Id = "T1", Name = "Iron Peaks", OwnerRealm = RealmId.Stonehold, BonusType = ResourceType.Stone, BonusAmount = 50, IsFortress = true });
            save.Territories.Add(new TerritoryData { Id = "T2", Name = "Silver Woods", OwnerRealm = RealmId.Eldergrove, BonusType = ResourceType.Wood, BonusAmount = 40, IsFortress = false });
            save.Territories.Add(new TerritoryData { Id = "T3", Name = "Golden Plains", OwnerRealm = RealmId.Crownlands, BonusType = ResourceType.Gold, BonusAmount = 20, IsFortress = false });
            save.Territories.Add(new TerritoryData { Id = "T4", Name = "Shadow Vale", OwnerRealm = RealmId.Umbral, BonusType = ResourceType.Food, BonusAmount = 60, IsFortress = true });
            save.Territories.Add(new TerritoryData { Id = "T5", Name = "Neutral Borderlands", OwnerRealm = RealmId.None, BonusType = ResourceType.Gold, BonusAmount = 10, IsFortress = false });
            return true;
        }

        private static TerritoryData CloneTerritory(TerritoryData territory) =>
            new TerritoryData
            {
                Id = territory.Id,
                Name = territory.Name,
                OwnerRealm = territory.OwnerRealm,
                BonusType = territory.BonusType,
                BonusAmount = territory.BonusAmount,
                IsFortress = territory.IsFortress
            };
    }
}
