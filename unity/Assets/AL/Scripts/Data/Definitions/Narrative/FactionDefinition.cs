using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New Faction", menuName = "AL/Narrative/Faction")]
    public class FactionDefinition : ScriptableObject
    {
        public string Id;
        public string FactionName;
        public RealmId ParentRealm;
        [TextArea] public string Description;
        public Sprite Emblem;
    }
}
