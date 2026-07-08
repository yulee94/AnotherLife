using UnityEngine;
using System.Collections.Generic;

namespace AL.Data.Definitions.Narrative
{
    [CreateAssetMenu(fileName = "New Chapter", menuName = "AL/Narrative/Chapter")]
    public class ChapterDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string LoreSummary;
        public string InitialDialogueNodeId;
        public List<string> RequiredQuestIds = new List<string>();
        public string NextChapterId;
    }
}
