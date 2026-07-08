using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Realm", menuName = "AL/Data/Realm")]
    public class RealmDefinition : ScriptableObject
    {
        public RealmId Id;
        public string RealmName;
        [TextArea] public string Description;
        public Sprite Icon;
    }
}
