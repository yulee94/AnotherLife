using System;

namespace AL.ChampionMode.Quests
{
    /// <summary>
    /// Resumable state for one authored NPC subtitle line. Collapsing pauses
    /// automatic advancement; reopening preserves the exact dialogue and elapsed time.
    /// </summary>
    public sealed class NpcConversationSession
    {
        private float _elapsed;
        private bool _completed;

        public NpcConversationSession(
            string dialogueId,
            string body,
            float autoAdvanceSeconds)
        {
            if (string.IsNullOrWhiteSpace(dialogueId))
            {
                throw new ArgumentException("A dialogue id is required.", nameof(dialogueId));
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Dialogue copy is required.", nameof(body));
            }

            DialogueId = dialogueId;
            Body = body;
            AutoAdvanceSeconds = Math.Max(0.1f, autoAdvanceSeconds);
        }

        public string DialogueId { get; }
        public string Body { get; }
        public float AutoAdvanceSeconds { get; }
        public bool IsCollapsed { get; private set; }
        public bool IsCompleted => _completed;

        public bool Advance(float unscaledDeltaTime)
        {
            if (_completed || IsCollapsed || unscaledDeltaTime <= 0f)
            {
                return false;
            }

            _elapsed += unscaledDeltaTime;
            if (_elapsed < AutoAdvanceSeconds)
            {
                return false;
            }

            _completed = true;
            return true;
        }

        public bool SkipCurrentLine()
        {
            if (_completed)
            {
                return false;
            }

            _completed = true;
            return true;
        }

        public void Collapse()
        {
            if (!_completed)
            {
                IsCollapsed = true;
            }
        }

        public void Reopen()
        {
            if (!_completed)
            {
                IsCollapsed = false;
            }
        }
    }
}
