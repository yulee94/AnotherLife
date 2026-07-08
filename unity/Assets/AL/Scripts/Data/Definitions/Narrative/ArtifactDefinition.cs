using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New Artifact", menuName = "AL/Narrative/Artifact")]
    public class ArtifactDefinition : ScriptableObject
    {
        public string Id;
        public string ArtifactName;
        public RealmId OriginRealm;
        [TextArea] public string Lore;
        public Sprite Icon;

        [Header("Legacy Bonus")]
        public string LoyaltyBonusDescription;
    }
}
