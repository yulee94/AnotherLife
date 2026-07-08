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
        public List<AL.Core.Interfaces.ResearchState> Researches = new List<AL.Core.Interfaces.ResearchState>();
        public List<AL.Core.Interfaces.QuestState> Quests = new List<AL.Core.Interfaces.QuestState>();
        public string CurrentChapterId;
        public WarmasterState Warmaster = new WarmasterState();
        public long LastSavedTimestamp;
    }

    [Serializable]
    public class ResourceData
    {
        public ResourceType Type;
        public long Amount;
    }
}
