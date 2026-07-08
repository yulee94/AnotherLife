using UnityEngine;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Boss", menuName = "AL/Data/Boss")]
    public class BossDefinition : ScriptableObject
    {
        public string Id;
        public string BossName;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Stats")]
        public int Health;
        public int Attack;
        public int Armor;

        [Header("Mechanics")]
        public string[] SpecialAbilities;

        [Header("Loot Table")]
        public System.Collections.Generic.List<EquipmentDefinition> PossibleLoot;
    }
}
