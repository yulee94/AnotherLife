using UnityEngine;
using AL.Core;
using System.Collections.Generic;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New Side Quest", menuName = "AL/Data/SideQuest")]
    public class SideQuestDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
        public string NpcName;
        public QuestType Type;
        public int TargetValue;

        [Header("Rewards")]
        public List<AL.Data.Runtime.ResourceData> RewardResources;
        public int RewardXP;
    }
}
