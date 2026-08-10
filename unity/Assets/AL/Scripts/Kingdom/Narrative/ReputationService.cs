using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class ReputationService : IReputationService
    {
        private readonly ISaveGameService _saveGameService;

        public ReputationService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        private List<NpcAffinityData> ReputationData => _saveGameService.CurrentSave?.Reputation;

        public float GetAffinity(string npcId)
        {
            if (ReputationData == null) return 0f;
            return ReputationData.FirstOrDefault(r => r.NpcId == npcId)?.Affinity ?? 0f;
        }

        public void ChangeAffinity(string npcId, float delta)
        {
            if (!ProfileMutationContainment.TryGetMutableSave(
                    _saveGameService,
                    ProfileMutationSurfaceIds.Reputation,
                    out SaveGameData save) ||
                save.Reputation == null)
            {
                return;
            }

            List<NpcAffinityData> reputation = save.Reputation;
            var data = reputation.FirstOrDefault(r => r.NpcId == npcId);
            if (data == null)
            {
                data = new NpcAffinityData { NpcId = npcId, Affinity = 0f };
                reputation.Add(data);
            }

            data.Affinity = Mathf.Clamp(data.Affinity + delta, -100f, 100f);
            Debug.Log($"[Reputation] {npcId} Affinity changed by {delta}. New: {data.Affinity}");
            _saveGameService.Save();
        }

        public string GetAffinityRank(string npcId)
        {
            float affinity = GetAffinity(npcId);
            if (affinity >= 80f) return "Exalted";
            if (affinity >= 50f) return "Friendly";
            if (affinity >= 0f) return "Neutral";
            if (affinity >= -50f) return "Hostile";
            return "Nemesis";
        }
    }
}
