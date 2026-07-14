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
            return _saveGameService.CurrentSave?.Quests.Where(q => q.QuestId.StartsWith("SQ_") && !q.IsClaimed) ?? Enumerable.Empty<QuestState>();
        }
    }
}
