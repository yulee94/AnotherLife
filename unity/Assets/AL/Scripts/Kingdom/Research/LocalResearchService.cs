using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public class LocalResearchService : IResearchService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;
        private readonly EconomyWriteAuthorityGate _writeAuthorityGate;

        public LocalResearchService(ISaveGameService saveGameService, IResourceService resourceService)
            : this(
                saveGameService,
                resourceService,
                EconomyWriteAuthorityGate.FromSaveService(saveGameService))
        {
        }

        private LocalResearchService(
            ISaveGameService saveGameService,
            IResourceService resourceService,
            EconomyWriteAuthorityGate writeAuthorityGate)
        {
            _saveGameService = saveGameService ??
                throw new System.ArgumentNullException(nameof(saveGameService));
            _resourceService = resourceService ??
                throw new System.ArgumentNullException(nameof(resourceService));
            _writeAuthorityGate = writeAuthorityGate ??
                throw new System.ArgumentNullException(
                    nameof(writeAuthorityGate));
        }

        private List<ResearchState> Researches => _saveGameService.CurrentSave?.Researches;

        public ResearchState GetResearchState(string researchId)
        {
            List<ResearchState> researches = Researches;
            if (researches == null || string.IsNullOrWhiteSpace(researchId))
            {
                return null;
            }

            ResearchState[] matches = researches
                .Where(candidate => candidate != null && candidate.ResearchId == researchId)
                .ToArray();
            return matches.Length == 1 ? CloneState(matches[0]) : null;
        }

        public ResearchTroopCatalogQueryResult QueryResearch(string researchId)
        {
            return ResearchTroopCatalogAuthority.QueryResearch(researchId);
        }

        public IEnumerable<ResearchState> GetAllResearchStates()
        {
            List<ResearchState> researches = Researches;
            return researches == null
                ? Array.Empty<ResearchState>()
                : researches
                    .Where(state => state != null)
                    .Select(CloneState)
                    .ToArray();
        }

        private static ResearchState CloneState(ResearchState state) =>
            new ResearchState
            {
                ResearchId = state.ResearchId,
                Level = state.Level,
                IsResearching = state.IsResearching,
                CompleteTimestamp = state.CompleteTimestamp
            };

        public ResearchTroopMutationResult TryStartResearch(string researchId)
        {
            return ResearchTroopCatalogAuthority.RejectResearch(researchId);
        }

        public ResearchTroopMutationResult TryCompleteResearch(string researchId)
        {
            return ResearchTroopCatalogAuthority.RejectResearch(researchId);
        }

        public void StartResearch(string researchId)
        {
            TryStartResearch(researchId);
        }

        public void CompleteResearch(string researchId)
        {
            TryCompleteResearch(researchId);
        }

        public float GetStatBonus(StatType statType)
        {
            // Simple mapping for prototype
            // Attack -> "Steel Forging"
            // Defense -> "Plate Armor"

            string techId = statType switch {
                StatType.Attack => "Steel Forging",
                StatType.Defense => "Plate Armor",
                _ => null
            };

            if (techId == null) return 0f;

            var state = GetResearchState(techId);
            return (state?.Level ?? 0) * 0.05f; // 5% bonus per level
        }
    }
}
