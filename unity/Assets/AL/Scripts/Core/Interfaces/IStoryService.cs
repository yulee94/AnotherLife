using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;

namespace AL.Core.Interfaces
{
    [Serializable]
    public class DialogueChoice
    {
        public string Text;
        public string NextNodeId;
    }

    public interface IStoryService
    {
        string CurrentChapterId { get; }
        void AdvanceStory();
        DialogueNode GetDialogue(string nodeId);
        IEnumerable<DialogueNode> GetConflictHints(RealmId currentRealm);
        void TriggerDialogue(string nodeId);
        event Action<string> OnChapterAdvanced;
        event Action<DialogueNode> OnDialogueTriggered;
    }
}
