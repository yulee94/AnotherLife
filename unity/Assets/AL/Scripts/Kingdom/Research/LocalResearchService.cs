using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

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
            if (researches == null) return null;

            ResearchState state = researches.FirstOrDefault(
                candidate => candidate?.ResearchId == researchId);
            return state == null
                ? new ResearchState
                {
                    ResearchId = researchId,
                    Level = 0
                }
                : CloneState(state);
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

        public void StartResearch(string researchId)
        {
            if (!_writeAuthorityGate.TryGetWritableSave(out _))
            {
                Debug.LogWarning(
                    "[AL-RSCH-PROFILE-READ-ONLY] Research start rejected before any profile mutation.");
                return;
            }

            List<ResearchState> researches = Researches;
            if (researches == null) return;

            ResearchState state = researches.FirstOrDefault(
                candidate => candidate?.ResearchId == researchId);
            bool stateExists = state != null;
            state ??= new ResearchState { ResearchId = researchId, Level = 0 };
            if (state.IsResearching) return;

            long cost = (state.Level + 1) * 200; // Gold cost
            if (_resourceService.ConsumeResource(ResourceType.Gold, cost))
            {
                if (!stateExists)
                {
                    researches.Add(state);
                }

                state.IsResearching = true;
                state.CompleteTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ((state.Level + 1) * 15);
                _saveGameService.Save();
                Debug.Log($"Started research for {researchId}. Completes in {(state.Level + 1) * 15}s");
            }
        }

        public void CompleteResearch(string researchId)
        {
            if (!_writeAuthorityGate.TryGetWritableSave(out _))
            {
                Debug.LogWarning(
                    "[AL-RSCH-PROFILE-READ-ONLY] Research completion rejected before any profile mutation.");
                return;
            }

            ResearchState state = Researches?.FirstOrDefault(
                candidate => candidate?.ResearchId == researchId);
            if (state == null || !state.IsResearching) return;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= state.CompleteTimestamp)
            {
                state.Level++;
                state.IsResearching = false;

                // Trigger Quest Update
                try
                {
                    ServiceLocator.Get<IQuestService>().UpdateProgress(QuestType.ResearchTech, 1);
                }
                catch (Exception)
                {
                    // Quest service is optional in early scene tests.
                }

                _saveGameService.Save();
                Debug.Log($"Research {researchId} completed. Level: {state.Level}");
            }
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
