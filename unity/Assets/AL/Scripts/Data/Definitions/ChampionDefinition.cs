using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Champion", menuName = "AL/Data/Champion")]
    public class ChampionDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public RealmId Realm;
        public ClassFamily Family;
        public Sprite Portrait;
        public SkillDefinition[] BaseSkills;
    }
}
