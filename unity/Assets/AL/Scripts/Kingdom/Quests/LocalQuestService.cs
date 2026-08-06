using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions.Narrative;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalQuestService : IQuestService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;
        private readonly IWarzoneCreditService _creditService;
        private readonly EconomyWriteAuthorityGate _writeAuthorityGate;

        private Dictionary<string, QuestDefinition> _definitions =
            new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);
        private QuestStateCompatibilityIssues _reportedCompatibilityIssues;

        public event Action<QuestState> OnQuestUpdated;
        public event Action<QuestState> OnQuestCompleted;

        public LocalQuestService(ISaveGameService saveGameService, IResourceService resourceService, IWarzoneCreditService creditService)
            : this(
                saveGameService,
                resourceService,
                creditService,
                EconomyWriteAuthorityGate.FromSaveService(saveGameService))
        {
        }

        private LocalQuestService(
            ISaveGameService saveGameService,
            IResourceService resourceService,
            IWarzoneCreditService creditService,
            EconomyWriteAuthorityGate writeAuthorityGate)
        {
            _saveGameService = saveGameService ??
                throw new ArgumentNullException(nameof(saveGameService));
            _resourceService = resourceService ??
                throw new ArgumentNullException(nameof(resourceService));
            _creditService = creditService ??
                throw new ArgumentNullException(nameof(creditService));
            _writeAuthorityGate = writeAuthorityGate ??
                throw new ArgumentNullException(nameof(writeAuthorityGate));

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
        }

        public IEnumerable<QuestState> GetActiveQuests()
        {
            return CreateCompatibilityView()
                .Where(q => !q.IsClaimed)
                .ToArray();
        }

        public void UpdateProgress(QuestType type, int amount)
        {
            if (_saveGameService.CurrentSave == null)
            {
                return;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"[AL-QST-INVALID-PROGRESS] Quest progress ignored for non-positive amount '{amount}'.");
                return;
            }

            bool changed = false;
            foreach (QuestState state in CreateCompatibilityView())
            {
                if (state.IsCompleted ||
                    !_definitions.TryGetValue(state.QuestId, out QuestDefinition def) ||
                    def.Type != type)
                {
                    continue;
                }

                int nextValue = (int)Math.Min((long)def.TargetValue, (long)state.CurrentValue + amount);
                if (nextValue == state.CurrentValue)
                {
                    continue;
                }

                state.CurrentValue = nextValue;
                changed = true;
                if (state.CurrentValue == def.TargetValue)
                {
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

            if (changed)
            {
                _saveGameService.Save();
            }
        }

        public void ClaimReward(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                Debug.LogWarning("[AL-QST-INVALID-ID] Quest reward claim ignored for blank quest id.");
                return;
            }

            if (!_definitions.TryGetValue(questId, out var def))
            {
                Debug.LogWarning($"[AL-QST-UNKNOWN-ID] Quest reward claim ignored for unsupported quest id '{questId}'.");
                return;
            }

            if (!_writeAuthorityGate.TryGetWritableSave(out _))
            {
                Debug.LogWarning(
                    "[AL-QST-PROFILE-READ-ONLY] Quest reward claim rejected before any profile mutation.");
                return;
            }

            QuestState state = CreateCompatibilityView()
                .FirstOrDefault(candidate => string.Equals(candidate.QuestId, questId, StringComparison.Ordinal));
            if (state == null)
            {
                Debug.LogWarning($"[AL-QST-UNSAFE-STATE] Quest reward claim ignored for missing, duplicate, or contradictory state '{questId}'.");
                return;
            }

            if (!state.IsCompleted || state.IsClaimed)
            {
                return;
            }

            state.IsClaimed = true;

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

        public void TriggerHiddenQuest(string conditionId, TriggerCondition conditionType)
        {
            if (_saveGameService.CurrentSave == null) return;

            QuestState[] compatibleStates = CreateCompatibilityView();
            IReadOnlyList<QuestState> rawStates = _saveGameService.CurrentSave.Quests;
            var compatibleById = compatibleStates.ToDictionary(
                state => state.QuestId,
                state => state,
                StringComparer.Ordinal);
            var rawIds = new HashSet<string>(StringComparer.Ordinal);
            if (rawStates != null)
            {
                for (int index = 0; index < rawStates.Count; index++)
                {
                    QuestState rawState = rawStates[index];
                    if (rawState != null && !string.IsNullOrWhiteSpace(rawState.QuestId))
                    {
                        rawIds.Add(rawState.QuestId);
                    }
                }
            }

            // Find all hidden quests that match this trigger
            var hiddenQuests = _definitions.Values
                .Where(d => d.IsHidden && d.Trigger == conditionType && d.RequiredItemId == conditionId);

            foreach (var questDef in hiddenQuests)
            {
                compatibleById.TryGetValue(questDef.Id, out QuestState state);
                if (state == null && rawIds.Contains(questDef.Id))
                {
                    continue;
                }

                if (state != null && state.IsClaimed) continue; // Already done

                // "Reveal" the quest
                Debug.Log($"<color=purple>[Narrative] Hidden Quest Revealed: {questDef.Title}</color>");
                // In a real UI, this would pop up a notification
            }
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
                nameof(LocalQuestService));
            return states;
        }
    }
}
