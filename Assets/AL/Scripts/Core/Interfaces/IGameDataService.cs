using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;

namespace AL.Core.Interfaces
{
    public interface IGameDataService
    {
        RealmDefinition GetRealm(RealmId id);
        IEnumerable<RealmDefinition> GetAllRealms();
        BuildingDefinition GetBuilding(string id);
        TroopDefinition GetTroop(string id);
        ChampionDefinition GetChampion(string id);
        SkillDefinition GetSkill(string id);
    }
}
