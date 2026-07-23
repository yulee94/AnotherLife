using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions.Narrative;
using UnityEngine;

namespace AL.Services.Local
{
    public interface ISideQuestService
    {
        void AcceptQuest(string questId);
        void UpdateProgress(QuestType type, int amount);
        IEnumerable<QuestState> GetActiveSideQuests();
    }

    public class SideQuestService : ISideQuestService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;
        private Dictionary<string, SideQuestDefinition> _definitions =
            new Dictionary<string, SideQuestDefinition>(StringComparer.Ordinal);
        private QuestStateCompatibilityIssues _reportedCompatibilityIssues;

        public SideQuestService(ISaveGameService saveGameService, IResourceService resourceService)
        {
            _saveGameService = saveGameService;
            _resourceService = resourceService;
        }

        public void AcceptQuest(string questId)
        {
            if (_saveGameService.CurrentSave == null) return;
            if (string.IsNullOrWhiteSpace(questId))
            {
                Debug.LogWarning("[AL-QST-INVALID-ID] Side quest accept ignored for blank quest id.");
                return;
            }

            if (!_definitions.TryGetValue(questId, out SideQuestDefinition definition) ||
                definition == null ||
                !string.Equals(definition.Id, questId, StringComparison.Ordinal) ||
                definition.TargetValue <= 0)
            {
                Debug.LogWarning($"[AL-QST-UNKNOWN-ID] Side quest accept ignored for unsupported quest id '{questId}'.");
                return;
            }

            IReadOnlyList<QuestState> existingStates = _saveGameService.CurrentSave.Quests;
            if (QuestStateCompatibility.ContainsExactId(existingStates, questId))
            {
                if (!CreateCompatibilityView().Any(state =>
                        string.Equals(state.QuestId, questId, StringComparison.Ordinal)))
                {
                    Debug.LogWarning($"[AL-QST-UNSAFE-STATE] Side quest accept ignored for duplicate or contradictory state '{questId}'.");
                }

                return;
            }

            _saveGameService.CurrentSave.Quests ??= new List<QuestState>();
            _saveGameService.CurrentSave.Quests.Add(new QuestState
            {
                QuestId = questId,
                CurrentValue = 0
            });
            _saveGameService.Save();
            Debug.Log($"[SideQuest] Accepted: {questId}");
        }

        public void UpdateProgress(QuestType type, int amount)
        {
            // Logic similar to Main Quest but for side-quest IDs
        }

        public IEnumerable<QuestState> GetActiveSideQuests()
        {
            if (_saveGameService.CurrentSave == null)
            {
                return Enumerable.Empty<QuestState>();
            }

            return CreateCompatibilityView()
                .Where(q => !q.IsClaimed)
                .ToArray();
        }

        private QuestState[] CreateCompatibilityView()
        {
            QuestState[] states = QuestStateCompatibility.CreateSupportedView(
                _saveGameService.CurrentSave?.Quests,
                _definitions,
                definition => definition.Id,
                definition => definition.TargetValue,
                out QuestStateCompatibilityIssues issues);
            QuestStateCompatibilityDiagnostics.ReportOnce(
                ref _reportedCompatibilityIssues,
                issues,
                nameof(SideQuestService));
            return states;
        }
    }
}
