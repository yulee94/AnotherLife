using System.Collections.Generic;
using AL.Core;
using System;

namespace AL.Core.Interfaces
{
    [Serializable]
    public class TerritoryData
    {
        public string Id;
        public string Name;
        public RealmId OwnerRealm;
        public ResourceType BonusType;
        public long BonusAmount;
        public bool IsFortress;
    }

    public interface ITerritoryService
    {
        IEnumerable<TerritoryData> GetTerritories();
        void CaptureTerritory(string territoryId, RealmId capturer);
        long CalculatePassiveIncome(ResourceType type);
        event Action<string, RealmId> OnTerritoryCaptured;
    }
}
