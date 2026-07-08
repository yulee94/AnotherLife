using System;
using AL.Data.Definitions;

namespace AL.Core.Interfaces
{
    public interface IStoryService
    {
        string CurrentChapterId { get; }
        event Action<string> OnChapterAdvanced;
        event Action<DialogueNode> OnDialogueTriggered;

        void AdvanceStory();
        DialogueNode GetDialogue(string nodeId);
        void TriggerDialogue(string nodeId);
    }
}

