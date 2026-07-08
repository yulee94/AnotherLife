using System;
using System.Collections.Generic;

namespace AL.Data.Definitions
{
    [Serializable]
    public class DialogueNode
    {
        public string Id;
        public string CharacterName;
        public string Text;
        public List<DialogueChoice> Choices = new List<DialogueChoice>();

        [Header("Conflict Info")]
        public bool IsConflictHint;
        public AL.Core.RealmId AssociatedRealmId;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string Text;
        public string NextNodeId;
    }
}

