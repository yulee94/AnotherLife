using UnityEngine;
using AL.Core;
using System.Collections.Generic;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New Quest", menuName = "AL/Narrative/Quest")]
    public class QuestDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
        public QuestType Type;
        public int TargetValue;

        [Header("Unlock Conditions")]
        public bool IsHidden;
        public string RequiredItemId;
        public TriggerCondition Trigger;
        public string ConflictHint;

        [Header("Rewards")]
        public List<AL.Data.Runtime.ResourceData> RewardResources;
        public int RewardCredits;
        public int RewardXP;
    }
}
