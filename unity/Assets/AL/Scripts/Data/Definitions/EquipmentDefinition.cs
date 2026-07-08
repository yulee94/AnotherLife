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
    }
}
