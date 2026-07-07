using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Troop", menuName = "AL/Data/Troop")]
    public class TroopDefinition : ScriptableObject
    {
        public string Id;
        public TroopType Type;
        public string DisplayName;
        public Sprite Icon;
        public int BaseAttack;
        public int BaseDefense;
    }
}
