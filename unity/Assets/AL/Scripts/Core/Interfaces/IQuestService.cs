using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.Core.Interfaces
{
    [Serializable]
    public class QuestState
    {
        public string QuestId;
        public int CurrentValue;
        public bool IsCompleted;
        public bool IsClaimed;
    }

    public interface IQuestService
    {
        IEnumerable<QuestState> GetActiveQuests();
        void UpdateProgress(QuestType type, int amount);
        void ClaimReward(string questId);
        void TriggerHiddenQuest(string conditionId, TriggerCondition conditionType);
        event Action<QuestState> OnQuestUpdated;
        event Action<QuestState> OnQuestCompleted;
    }
}
