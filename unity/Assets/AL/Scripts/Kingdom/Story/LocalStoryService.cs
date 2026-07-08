using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalStoryService : IStoryService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IGameDataService _gameDataService;

        public string CurrentChapterId => _saveGameService.CurrentSave?.CurrentChapterId;

        public event Action<string> OnChapterAdvanced;
        public event Action<DialogueNode> OnDialogueTriggered;

        private Dictionary<string, DialogueNode> _dialogueCache = new Dictionary<string, DialogueNode>();

        public LocalStoryService(ISaveGameService saveGameService, IGameDataService gameDataService)
        {
            _saveGameService = saveGameService;
            _gameDataService = gameDataService;

            InitializeFallbackDialogues();
        }

        private void InitializeFallbackDialogues()
        {
            AddNode(new DialogueNode {
                Id = "intro_stonehold",
                CharacterName = "Thane Ironbeard",
                Text = "The Deep Forge has been silent for a century. Today, we strike the first spark. Are you ready, Lord?",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The mountains will ring again.", NextNodeId = "end" }
                }
            });

            AddNode(new DialogueNode {
                Id = "intro_eldergrove",
                CharacterName = "High Sentinel Elara",
                Text = "A shadow creeps upon the roots of the World Tree. The whispers are troubled. We must act with grace and steel.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The forest will not fall.", NextNodeId = "end" }
                }
            });

            AddNode(new DialogueNode {
                Id = "intro_crownlands",
                CharacterName = "Captain Valerius",
                Text = "The walls are rebuilt, but the spirit of the people is still fragile. Your decree will shape our future.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "A new era begins today.", NextNodeId = "end" }
                }
            });

            AddNode(new DialogueNode {
                Id = "intro_umbral",
                CharacterName = "Shadow-Weaver Vex",
                Text = "The volcanic rifts pulse with chaotic energy. The Void calls to us. Will you master it, or let it consume us?",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The shadow serves me.", NextNodeId = "end" }
                }
            });
        }

        private void AddNode(DialogueNode node) => _dialogueCache[node.Id] = node;

        public void AdvanceStory()
        {
            Debug.Log($"Advancing story. Current Chapter: {CurrentChapterId}");
            // Narrative advancement logic
        }

        public DialogueNode GetDialogue(string nodeId)
        {
            if (_dialogueCache.TryGetValue(nodeId, out var node)) return node;
            return null;
        }

        public void TriggerDialogue(string nodeId)
        {
            var node = GetDialogue(nodeId);
            if (node != null) OnDialogueTriggered?.Invoke(node);
        }
    }
}
