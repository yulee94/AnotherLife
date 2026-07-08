using UnityEngine;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Warmaster Set", menuName = "AL/Data/WarmasterSet")]
    public class WarmasterSetDefinition : ScriptableObject
    {
        public string Id;
        public string SetName;
        public Sprite Icon;
    }
}
