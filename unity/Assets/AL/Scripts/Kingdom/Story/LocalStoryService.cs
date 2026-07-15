using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions.Narrative;
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
            // Realm Intro Dialogues (Conflict-aware)
            AddNode(new DialogueNode {
                Id = "intro_stonehold",
                CharacterName = "Thane Ironbeard",
                Text = "The Deep Forge has been silent for a century. Today, we strike the first spark. Defeat Ferrum and the Ring of the Mountain King shall be yours.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The mountains will ring again.", NextNodeId = "end" }
                },
                IsConflictHint = false,
                AssociatedRealmId = RealmId.Stonehold
            });

            AddNode(new DialogueNode {
                Id = "hint_stonehold_war",
                CharacterName = "Dwarven Sapper",
                Text = "The Humans claim our border mines are theirs. They seek our Ancestral Ring. We must fortify.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "Show them our resolve.", NextNodeId = "end" }
                },
                IsConflictHint = true,
                AssociatedRealmId = RealmId.Stonehold
            });

            AddNode(new DialogueNode {
                Id = "intro_eldergrove",
                CharacterName = "High Sentinel Elara",
                Text = "A shadow creeps upon the roots of the World Tree. The whispers are troubled. We must act with grace and steel.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The forest will not fall.", NextNodeId = "end" }
                },
                IsConflictHint = false,
                AssociatedRealmId = RealmId.Eldergrove
            });

            AddNode(new DialogueNode {
                Id = "hint_eldergrove_blight",
                CharacterName = "Forest Spirit",
                Text = "The Humans are building walls near our sacred groves. Their progress is a blight upon the green.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The wood remembers.", NextNodeId = "end" }
                },
                IsConflictHint = true,
                AssociatedRealmId = RealmId.Eldergrove
            });

            AddNode(new DialogueNode {
                Id = "intro_crownlands",
                CharacterName = "Captain Valerius",
                Text = "The walls are rebuilt, but the spirit of the people is still fragile. Your decree will shape our future.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "A new era begins today.", NextNodeId = "end" }
                },
                IsConflictHint = false,
                AssociatedRealmId = RealmId.Crownlands
            });

            AddNode(new DialogueNode {
                Id = "hint_crownlands_trade",
                CharacterName = "Royal Merchant",
                Text = "The Dwarves have raised taxes on iron through the mountain pass. They wish to starve our forges.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "We will find another way.", NextNodeId = "end" }
                },
                IsConflictHint = true,
                AssociatedRealmId = RealmId.Crownlands
            });

            AddNode(new DialogueNode {
                Id = "intro_umbral",
                CharacterName = "Shadow-Weaver Vex",
                Text = "The volcanic rifts pulse with chaotic energy. The Void calls to us. Will you master it, or let it consume us?",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The shadow serves me.", NextNodeId = "end" }
                },
                IsConflictHint = false,
                AssociatedRealmId = RealmId.Umbral
            });

            AddNode(new DialogueNode {
                Id = "hint_umbral_revenge",
                CharacterName = "Exiled Scout",
                Text = "The Elves have forgotten us, left us to rot in these ash-wastes. One day, the Void will reclaim their groves.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "The night is patient.", NextNodeId = "end" }
                },
                IsConflictHint = true,
                AssociatedRealmId = RealmId.Umbral
            });

            // Chapter 10-12 Heavens Ascended Dialogues
            AddNode(new DialogueNode {
                Id = "c10_intro",
                CharacterName = "High Celestial Lyra",
                Text = "Mortal of the lower realms, the gates of the Sky Castle have not opened in an age. Why do you seek the light?",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "To save my people.", NextNodeId = "end" },
                    new DialogueChoice { Text = "To claim your power.", NextNodeId = "end" }
                }
            });

            AddNode(new DialogueNode {
                Id = "c12_victory",
                CharacterName = "High Celestial Lyra",
                Text = "You have proven your worth. The Throne of the Skies is yours. But beware, for signals from the Otherworld grow stronger.",
                Choices = new List<DialogueChoice> {
                    new DialogueChoice { Text = "I am ready for what comes next.", NextNodeId = "end" }
                }
            });
        }

        private void AddNode(DialogueNode node) => _dialogueCache[node.Id] = node;

        public void AdvanceStory()
        {
            Debug.Log($"Advancing story. Current Chapter: {CurrentChapterId}");
            // Narrative advancement logic handled by content scripts
            OnChapterAdvanced?.Invoke(CurrentChapterId);
        }

        public DialogueNode GetDialogue(string nodeId)
        {
            if (_dialogueCache.TryGetValue(nodeId, out var node)) return node;
            return null;
        }

        public IEnumerable<DialogueNode> GetConflictHints(RealmId currentRealm)
        {
            return _dialogueCache.Values.Where(n => n.IsConflictHint && n.AssociatedRealmId != currentRealm);
        }

        public void TriggerDialogue(string nodeId)
        {
            var node = GetDialogue(nodeId);
            if (node != null) OnDialogueTriggered?.Invoke(node);
        }
    }
}
