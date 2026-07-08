using UnityEngine;
using AL.Core;
using System.Collections.Generic;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New Skill Soul Quest", menuName = "AL/Narrative/SkillSoulQuest")]
    public class SkillSoulQuestDefinition : ScriptableObject
    {
        public string Id;
        public SubclassId AssociatedSubclass;
        public string Title;
        [TextArea] public string Description;

        [Header("Requirements")]
        public int MinLevel = 100;
        public string RequiredChapterId = "C12";

        [Header("Rewards")]
        public string AscensionSkillId;
        public List<AL.Data.Runtime.ResourceData> RewardResources;
        public int RewardXP;
    }
}
