using UnityEngine;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Building", menuName = "AL/Data/Building")]
    public class BuildingDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Sprite Icon;
        public int MaxLevel;
    }
}
