using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New Gem", menuName = "AL/Narrative/Gem")]
    public class GemDefinition : ScriptableObject
    {
        public string Id;
        public string Name;
        public RealmId AssociatedRealm;
        public Sprite Icon;
        [TextArea] public string Description;
        public string[] Powers;
    }
}
