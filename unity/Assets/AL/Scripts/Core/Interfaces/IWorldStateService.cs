using System;
using System.Collections.Generic;

namespace AL.Core.Interfaces
{
    public enum WorldStateEffect
    {
        None,
        Siege,          // Production reduced, defense increased
        Festival,       // Happiness/XP increased, costs reduced
        Omen,           // Narrative foreshadowing, strange events
        Corruption      // Resource decay, enemy strength increased
    }

    public interface IWorldStateService
    {
        WorldStateEffect CurrentEffect { get; }
        string ActiveEventId { get; }
        void TriggerStateChange(string eventId, WorldStateEffect effect, float durationHours);
        event Action<WorldStateEffect, string> OnWorldStateChanged;
    }
}
