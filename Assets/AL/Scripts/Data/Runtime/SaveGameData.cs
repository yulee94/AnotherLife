using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.Data.Runtime
{
    [Serializable]
    public class SaveGameData
    {
        public RealmId SelectedRealm;
        public List<ResourceData> Resources = new List<ResourceData>();
        public List<BuildingState> Buildings = new List<BuildingState>();
        public List<TroopInventoryData> Troops = new List<TroopInventoryData>();
        public List<AL.Core.Interfaces.ResearchState> Researches = new List<AL.Core.Interfaces.ResearchState>();
        public List<AL.Core.Interfaces.QuestState> Quests = new List<AL.Core.Interfaces.QuestState>();
        public string CurrentChapterId;
        public WarmasterState Warmaster = new WarmasterState();
        public ChampionCustomizationState ChampionCustomization = new ChampionCustomizationState();
        public int WarzoneCredits;
        public long LastSavedTimestamp;
    }

    [Serializable]
    public class ResourceData
    {
        public ResourceType Type;
        public long Amount;
    }

    [Serializable]
    public class TroopInventoryData
    {
        public TroopType Type;
        public int Count;
        public int WoundedCount;
    }

    [Serializable]
    public class ChampionCustomizationState
    {
        public string BodyPresetId = "average";
        public string HairStyleId = "short";
        public string ArmorStyleId = "realm_basic";
        public float PrimaryR = 0.2f;
        public float PrimaryG = 0.4f;
        public float PrimaryB = 1.0f;
        public float HairR = 0.08f;
        public float HairG = 0.06f;
        public float HairB = 0.04f;
        public bool CapeEnabled = true;
        public bool HelmetEnabled;
    }
}
