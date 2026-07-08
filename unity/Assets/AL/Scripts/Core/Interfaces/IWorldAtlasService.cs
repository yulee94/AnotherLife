using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    [Serializable]
    public class WorldZoneData
    {
        public string Id;
        public string DisplayName;
        public RealmId HomeRealm;
        public string SafetyLayer;
        public string TerrainTheme;
        public string SceneHint;
        public List<WorldObjectiveData> Objectives = new List<WorldObjectiveData>();
    }

    [Serializable]
    public class WorldObjectiveData
    {
        public string Id;
        public string DisplayName;
        public string ObjectiveType;
        public RealmId OwnerRealm;
        public ResourceType RareResourceReward;
        public string NarrativeKey;
        public string Description;
        public bool IsWarzoneObjective;
        public float PassiveCreditWeight;
    }

    [Serializable]
    public class WorldNarrationSnapshot
    {
        public RealmId ViewerRealm;
        public List<WorldZoneData> VisibleZones = new List<WorldZoneData>();
        public List<WorldObjectiveData> ActiveObjectives = new List<WorldObjectiveData>();
        public List<string> ConflictHints = new List<string>();
    }

    public interface IWorldAtlasService
    {
        IEnumerable<WorldZoneData> GetAllZones();
        IEnumerable<WorldZoneData> GetZonesForRealm(RealmId realmId);
        WorldZoneData GetZone(string zoneId);
        IEnumerable<WorldObjectiveData> GetObjectivesForRealm(RealmId viewerRealm);
        WorldNarrationSnapshot GetNarrationSnapshot(RealmId viewerRealm);
    }
}
