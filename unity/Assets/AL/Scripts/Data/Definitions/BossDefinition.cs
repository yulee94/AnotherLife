using AL.Core;
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

        [Header("Visual Identity")]
        public ItemGrade ThreatGrade = ItemGrade.Legendary;
        public RealmId VisualRealm = RealmId.Umbral;
        public Color PrimaryColor = new Color(0.88f, 0.08f, 0.05f);
        public Color SecondaryColor = new Color(0.22f, 0.42f, 0.62f);
        [Range(0.4f, 2.8f)] public float VisualIntensity = 1.25f;
        [Range(0.8f, 1.8f)] public float SilhouetteScale = 1.15f;

        [Header("Loot Table")]
        public System.Collections.Generic.List<EquipmentDefinition> PossibleLoot;
    }
}
