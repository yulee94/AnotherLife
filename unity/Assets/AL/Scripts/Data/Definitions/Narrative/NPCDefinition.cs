using UnityEngine;
using AL.Core;
using System.Collections.Generic;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New NPC", menuName = "AL/Narrative/NPC")]
    public class NPCDefinition : ScriptableObject
    {
        public string Id;
        public string Name;
        public RealmId Realm;
        public Sprite Portrait;
        public string Role;
        public List<string> BaseDialogueIds = new List<string>();
    }
}
