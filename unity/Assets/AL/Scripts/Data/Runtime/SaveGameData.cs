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
        public List<NpcAffinityData> Reputation = new List<NpcAffinityData>();
        public List<AL.Core.Interfaces.TerritoryData> Territories = new List<AL.Core.Interfaces.TerritoryData>();
        public List<RealmGemState> RealmGems = new List<RealmGemState>();
        public WishgateState Wishgate = new WishgateState();
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
        public string FaceMarkId = "none";
        public string WeaponStyleId = "sword";
        public string OffhandStyleId = "shield";
        public float PrimaryR = 0.2f;
        public float PrimaryG = 0.4f;
        public float PrimaryB = 1.0f;
        public float HairR = 0.08f;
        public float HairG = 0.06f;
        public float HairB = 0.04f;
        public float SkinR = 0.72f;
        public float SkinG = 0.56f;
        public float SkinB = 0.42f;
        public float EyeR = 0.25f;
        public float EyeG = 0.58f;
        public float EyeB = 0.92f;
        public float AccentR = 0.85f;
        public float AccentG = 0.62f;
        public float AccentB = 0.18f;
        public bool CapeEnabled = true;
        public bool HelmetEnabled;
    }

    [Serializable]
    public class RealmGemState
    {
        public string GemId;
        public RealmId HomeRealm;
        public int GemIndex;
        public bool IsAtHome = true;
        public bool IsDropped;
        public string CarrierId;
        public long LastDroppedTimestamp;
    }

    [Serializable]
    public class WishgateState
    {
        public bool IsEarned;
        public string EarnReason;
        public string LastRewardId;
        public long LastRewardChosenTimestamp;
    }

    [Serializable]
    public class NpcAffinityData
    {
        public string NpcId;
        public float Affinity;
    }
}
