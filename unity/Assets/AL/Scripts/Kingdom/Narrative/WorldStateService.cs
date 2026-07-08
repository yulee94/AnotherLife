using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class WorldStateService : IWorldStateService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly INotificationService _notificationService;

        public WorldStateEffect CurrentEffect { get; private set; } = WorldStateEffect.None;
        public string ActiveEventId { get; private set; } = string.Empty;

        public event Action<WorldStateEffect, string> OnWorldStateChanged;

        public WorldStateService(ISaveGameService saveGameService, INotificationService notificationService)
        {
            _saveGameService = saveGameService;
            _notificationService = notificationService;
        }

        public void TriggerStateChange(string eventId, WorldStateEffect effect, float durationHours)
        {
            ActiveEventId = eventId;
            CurrentEffect = effect;

            Debug.Log($"[WorldState] Triggered {effect} event: {eventId} for {durationHours} hours.");

            string message = effect switch
            {
                WorldStateEffect.Siege => "<color=red>THE CITY IS UNDER SIEGE!</color> Defenses are bolstered, but production has halted.",
                WorldStateEffect.Festival => "<color=gold>A REALM FESTIVAL HAS BEGUN!</color> Unity is high, and training costs are reduced.",
                WorldStateEffect.Omen => "<color=purple>A DARK OMEN APPEARS IN THE SKY...</color> Strange travelers have been sighted.",
                WorldStateEffect.Corruption => "<color=green>VOID CORRUPTION IS SPREADING!</color> Resources are decaying.",
                _ => "The world returns to normal."
            };

            _notificationService.ShowMessage(message);
            OnWorldStateChanged?.Invoke(effect, eventId);
        }

        // Logic to apply multipliers to building production would be called from ResourceService
        public float GetProductionMultiplier()
        {
            return CurrentEffect switch
            {
                WorldStateEffect.Siege => 0.1f,    // 90% reduction
                WorldStateEffect.Festival => 1.5f, // 50% boost
                WorldStateEffect.Corruption => 0.7f, // 30% penalty
                _ => 1.0f
            };
        }
    }
}
