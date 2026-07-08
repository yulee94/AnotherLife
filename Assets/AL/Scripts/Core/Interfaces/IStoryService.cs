using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.Core.Interfaces
{
    [Serializable]
    public class DialogueChoice
    {
        public string Text;
        public string NextNodeId;
    }

    [Serializable]
    public class DialogueNode
    {
        public string Id;
        public string CharacterName;
        public string Text;
        public List<DialogueChoice> Choices = new List<DialogueChoice>();
    }

    public interface IStoryService
    {
        string CurrentChapterId { get; }
        void AdvanceStory();
        DialogueNode GetDialogue(string nodeId);
        event Action<string> OnChapterAdvanced;
        event Action<DialogueNode> OnDialogueTriggered;
    }
}
