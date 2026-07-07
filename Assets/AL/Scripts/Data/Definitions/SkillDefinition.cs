using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Skill", menuName = "AL/Data/Skill")]
    public class SkillDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Sprite Icon;
        public SkillTargetType TargetType;
        public float Cooldown;
        public float Power;
    }
}
