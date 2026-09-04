using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.Core.Interfaces
{
    [Serializable]
    public class ResearchState
    {
        public string ResearchId;
        public int Level;
        public bool IsResearching;
        public long CompleteTimestamp;
    }

    public interface IResearchService
    {
        ResearchState GetResearchState(string researchId);
        IEnumerable<ResearchState> GetAllResearchStates();
        ResearchTroopCatalogQueryResult QueryResearch(string researchId);
        ResearchTroopMutationResult TryStartResearch(string researchId);
        ResearchTroopMutationResult TryCompleteResearch(string researchId);
        void StartResearch(string researchId);
        void CompleteResearch(string researchId);
        float GetStatBonus(StatType statType);
    }
}
