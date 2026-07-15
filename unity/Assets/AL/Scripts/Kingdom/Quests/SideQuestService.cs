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
        private Dictionary<string, SideQuestDefinition> _definitions = new Dictionary<string, SideQuestDefinition>();

        public SideQuestService(ISaveGameService saveGameService, IResourceService resourceService)
        {
            _saveGameService = saveGameService;
            _resourceService = resourceService;
        }

        public void AcceptQuest(string questId)
        {
            if (_saveGameService.CurrentSave == null) return;
            _saveGameService.CurrentSave.Quests ??= new List<QuestState>();
            SanitizeQuestStates();
            if (string.IsNullOrWhiteSpace(questId))
            {
                Debug.LogWarning("[AL-QST-INVALID-ID] Side quest accept ignored for blank quest id.");
                return;
            }

            if (_saveGameService.CurrentSave.Quests.Any(q => q.QuestId == questId)) return;

            _saveGameService.CurrentSave.Quests.Add(new QuestState { QuestId = questId, CurrentValue = 0 });
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

            _saveGameService.CurrentSave.Quests ??= new List<QuestState>();
            SanitizeQuestStates();
            return _saveGameService.CurrentSave.Quests
                .Where(q => q.QuestId.StartsWith("SQ_", StringComparison.Ordinal) && !q.IsClaimed);
        }

        private void SanitizeQuestStates()
        {
            var quests = _saveGameService.CurrentSave.Quests;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = quests.Count - 1; index >= 0; index--)
            {
                var state = quests[index];
                if (state == null)
                {
                    quests.RemoveAt(index);
                    Debug.LogWarning("[AL-QST-NULL-STATE] Removed null quest state from side-quest compatibility view.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.QuestId))
                {
                    quests.RemoveAt(index);
                    Debug.LogWarning("[AL-QST-INVALID-ID] Removed quest state with blank quest id from side-quest compatibility view.");
                    continue;
                }
            }

            for (int index = 0; index < quests.Count; index++)
            {
                var questId = quests[index].QuestId;
                if (seenIds.Add(questId))
                {
                    continue;
                }

                quests.RemoveAt(index);
                index--;
                Debug.LogWarning($"[AL-QST-DUPLICATE-ID] Removed duplicate quest state for '{questId}' from side-quest compatibility view.");
            }
        }
    }
}
