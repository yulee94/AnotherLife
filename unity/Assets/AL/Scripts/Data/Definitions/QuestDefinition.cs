using UnityEngine;
using AL.Core;
using System.Collections.Generic;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Quest", menuName = "AL/Data/Quest")]
    public class QuestDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
        public QuestType Type;
        public int TargetValue;

        [Header("Rewards")]
        public List<AL.Data.Runtime.ResourceData> RewardResources;
        public int RewardCredits;
        public int RewardXP;
    }
}
