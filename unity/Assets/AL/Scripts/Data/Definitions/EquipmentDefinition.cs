using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Equipment", menuName = "AL/Data/Equipment")]
    public class EquipmentDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public EquipmentSlot Slot;
        public Sprite Icon;

        [Header("Loot Settings")]
        public float DropRate;
        public bool AnnounceWorldDrop;

        [Header("Visual Identity")]
        public ItemGrade Grade = ItemGrade.Common;
        public RealmId VisualRealm = RealmId.None;
        public string VisualEffectKey = "loot_common";
        public Color PrimaryColor = new Color(0.62f, 0.68f, 0.74f);
        public Color SecondaryColor = new Color(0.30f, 0.36f, 0.42f);
        [Range(0f, 1f)] public float AuraIntensity = 0.15f;
        [Range(0.5f, 2.5f)] public float RevealScale = 1f;

        [Header("Stats")]
        public int AttackBonus;
        public int DefenseBonus;
        public int HealthBonus;
    }
}
