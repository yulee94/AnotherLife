using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class FactionService : IFactionService
    {
        private readonly ISaveGameService _saveGameService;

        public FactionService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        private List<FactionRepData> FactionData => _saveGameService.CurrentSave?.FactionReputations;

        public int GetReputation(string factionId)
        {
            if (FactionData == null) return 0;
            return FactionData.FirstOrDefault(f => f.FactionId == factionId)?.Reputation ?? 0;
        }

        public void AdjustReputation(string factionId, int delta)
        {
            if (!ProfileMutationContainment.TryGetMutableSave(
                    _saveGameService,
                    ProfileMutationSurfaceIds.Faction,
                    out SaveGameData save) ||
                save.FactionReputations == null)
            {
                return;
            }

            List<FactionRepData> factions = save.FactionReputations;
            var data = factions.FirstOrDefault(f => f.FactionId == factionId);
            if (data == null)
            {
                data = new FactionRepData { FactionId = factionId, Reputation = 0 };
                factions.Add(data);
            }

            data.Reputation += delta;
            Debug.Log($"[Faction] {factionId} reputation changed by {delta}. New: {data.Reputation}");
            _saveGameService.Save();
        }

        public string GetFactionAffiliation(string factionId)
        {
            int rep = GetReputation(factionId);
            if (rep >= 500) return "Ally";
            if (rep >= 100) return "Supporter";
            if (rep <= -500) return "Enemy";
            if (rep <= -100) return "Opponent";
            return "Neutral";
        }
    }
}
