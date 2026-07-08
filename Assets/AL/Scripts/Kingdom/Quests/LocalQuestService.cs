using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalQuestService : IQuestService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;
        private readonly IWarzoneCreditService _creditService;

        private Dictionary<string, QuestDefinition> _definitions = new Dictionary<string, QuestDefinition>();

        public event Action<QuestState> OnQuestUpdated;
        public event Action<QuestState> OnQuestCompleted;

        public LocalQuestService(ISaveGameService saveGameService, IResourceService resourceService, IWarzoneCreditService creditService)
        {
            _saveGameService = saveGameService;
            _resourceService = resourceService;
            _creditService = creditService;

            InitializeQuests();
        }

        private void InitializeQuests()
        {
            // Initial Milestone Quests
            AddDefinition("Q1", "Foundation", "Upgrade any building to Level 2.", QuestType.BuildBuilding, 1);
            AddDefinition("Q2", "Legion", "Train 100 total troops.", QuestType.TrainTroops, 100);
            AddDefinition("Q3", "Arcane Study", "Complete 1 research project.", QuestType.ResearchTech, 1);
            AddDefinition("Q4", "War Path", "Win 3 tactical battles.", QuestType.WinBattle, 3);
            AddDefinition("Q5", "Expander", "Capture 1 territory.", QuestType.CaptureTerritory, 1);
        }

        private void AddDefinition(string id, string title, string desc, QuestType type, int target)
        {
            var def = ScriptableObject.CreateInstance<QuestDefinition>();
            def.Id = id;
            def.Title = title;
            def.Description = desc;
            def.Type = type;
            def.TargetValue = target;
            def.RewardResources = new List<AL.Data.Runtime.ResourceData> {
                new AL.Data.Runtime.ResourceData { Type = ResourceType.Gold, Amount = 1000 }
            };
            _definitions[id] = def;

            // Sync with save data
            if (_saveGameService.CurrentSave != null && !_saveGameService.CurrentSave.Quests.Any(q => q.QuestId == id))
            {
                _saveGameService.CurrentSave.Quests.Add(new QuestState { QuestId = id, CurrentValue = 0 });
            }
        }

        public IEnumerable<QuestState> GetActiveQuests()
        {
            EnsureQuestStates();
            return _saveGameService.CurrentSave?.Quests.Where(q => !q.IsClaimed) ?? Enumerable.Empty<QuestState>();
        }

        public void UpdateProgress(QuestType type, int amount)
        {
            if (_saveGameService.CurrentSave == null) return;
            EnsureQuestStates();

            foreach (var state in _saveGameService.CurrentSave.Quests.Where(q => !q.IsCompleted))
            {
                if (!_definitions.ContainsKey(state.QuestId)) continue;

                var def = _definitions[state.QuestId];
                if (def.Type == type)
                {
                    state.CurrentValue += amount;
                    if (state.CurrentValue >= def.TargetValue)
                    {
                        state.CurrentValue = def.TargetValue;
                        state.IsCompleted = true;
                        OnQuestCompleted?.Invoke(state);

                        // Link to Story
                        try
                        {
                            ServiceLocator.Get<IStoryService>()?.AdvanceStory();
                        }
                        catch (Exception)
                        {
                            // Story service is optional in isolated tests.
                        }
                    }
                    OnQuestUpdated?.Invoke(state);
                }
            }
            _saveGameService.Save();
        }

        public void ClaimReward(string questId)
        {
            EnsureQuestStates();
            var state = _saveGameService.CurrentSave?.Quests.FirstOrDefault(q => q.QuestId == questId);
            if (state == null || !state.IsCompleted || state.IsClaimed) return;

            state.IsClaimed = true;
            var def = _definitions[questId];

            if (def.RewardResources != null)
            {
                foreach (var res in def.RewardResources)
                {
                    _resourceService.AddResource(res.Type, res.Amount);
                }
            }

            _creditService.AddCredits(def.RewardCredits);
            _saveGameService.Save();
            Debug.Log($"<color=gold>Quest Reward Claimed: {def.Title}</color>");
        }

        private void EnsureQuestStates()
        {
            if (_saveGameService.CurrentSave == null)
            {
                return;
            }

            _saveGameService.CurrentSave.Quests ??= new List<QuestState>();
            foreach (var id in _definitions.Keys)
            {
                if (_saveGameService.CurrentSave.Quests.All(q => q.QuestId != id))
                {
                    _saveGameService.CurrentSave.Quests.Add(new QuestState { QuestId = id, CurrentValue = 0 });
                }
            }
        }
    }
}
