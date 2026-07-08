using AL.Core;
using UnityEngine;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Chapter", menuName = "AL/Data/Chapter")]
    public class ChapterDefinition : ScriptableObject
    {
        public string Id;
        public RealmId Realm;
        public string Title;
        [TextArea] public string LoreSummary;
    }
}

