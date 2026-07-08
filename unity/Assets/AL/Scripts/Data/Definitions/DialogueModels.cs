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
    }

    [Serializable]
    public class DialogueChoice
    {
        public string Text;
        public string NextNodeId;
    }
}

