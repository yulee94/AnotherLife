using UnityEngine;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Boss", menuName = "AL/Data/Boss")]
    public class BossDefinition : ScriptableObject
    {
        public string Id;
        public string BossName;
        public Sprite Icon;
        public int Health;
        public int Attack;
    }
}
