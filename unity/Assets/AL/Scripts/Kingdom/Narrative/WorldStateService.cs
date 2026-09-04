using System;
using AL.Core.Interfaces;

namespace AL.Services.Local
{
    public class WorldStateService : IWorldStateService
    {
        public WorldStateEffect CurrentEffect { get; private set; } = WorldStateEffect.None;
        public string ActiveEventId { get; private set; } = string.Empty;

        public event Action<WorldStateEffect, string> OnWorldStateChanged;

        public WorldStateService(ISaveGameService saveGameService, INotificationService notificationService)
        {
        }

        public void TriggerStateChange(string eventId, WorldStateEffect effect, float durationHours)
        {
            // Legacy compatibility surface. Unverified events stay unavailable
            // rather than mutating in-memory state or emitting hard-coded copy.
            // Durable start/end lives on WorldStateDurableService.
        }

        public float GetProductionMultiplier()
        {
            return 1.0f;
        }
    }
}
