using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalGameDataService : IGameDataService
    {
        private Dictionary<RealmId, RealmDefinition> _realms = new Dictionary<RealmId, RealmDefinition>();

        public LocalGameDataService()
        {
            InitializeFallbackData();
        }

        private void InitializeFallbackData()
        {
            // Fallback Realm Definitions for Offline Prototype
            CreateFallbackRealm(
                RealmId.Stonehold,
                "Stonehold Dwarves",
                "Master builders and hardy warriors of the mountains.\n\nPerks:\n+20% Stone Production\n+10% Building Speed\n+15% Infantry Defense",
                "Perks: Mountain Resilience, Master Masonry"
            );

            CreateFallbackRealm(
                RealmId.Eldergrove,
                "Eldergrove Elves",
                "Ancient guardians of the forest with peerless magic.\n\nPerks:\n+20% Wood Production\n+15% Magic Attack\n-10% Training Cost",
                "Perks: Forest Harmony, Arcane Insight"
            );

            CreateFallbackRealm(
                RealmId.Crownlands,
                "Crownlands Humans",
                "Adaptive and ambitious leaders of the central plains.\n\nPerks:\n+15% Gold Production\n+10% Resource Gathering\n+10% All Troop Attack",
                "Perks: Royal Mandate, Versatility"
            );

            CreateFallbackRealm(
                RealmId.Umbral,
                "Umbral Dark Elves",
                "Masters of shadow and infiltration from the depths.\n\nPerks:\n+20% Assassin Attack\n+15% March Speed\n+10% Resource Looting",
                "Perks: Shadow Step, Ruthless Efficiency"
            );
        }

        private void CreateFallbackRealm(RealmId id, string name, string desc, string perks)
        {
            var realm = ScriptableObject.CreateInstance<RealmDefinition>();
            realm.Id = id;
            realm.RealmName = name;
            realm.Description = $"{desc}\n\n{perks}";
            _realms[id] = realm;
        }

        public RealmDefinition GetRealm(RealmId id)
        {
            return _realms.TryGetValue(id, out var realm) ? realm : null;
        }

        public IEnumerable<RealmDefinition> GetAllRealms()
        {
            return _realms.Values;
        }

        public BuildingDefinition GetBuilding(string id)
        {
            // For now, load from Resources or return null
            return Resources.Load<BuildingDefinition>($"Data/Buildings/{id}");
        }

        public TroopDefinition GetTroop(string id)
        {
            return Resources.Load<TroopDefinition>($"Data/Troops/{id}");
        }

        public ChampionDefinition GetChampion(string id)
        {
            return Resources.Load<ChampionDefinition>($"Data/Champions/{id}");
        }

        public SkillDefinition GetSkill(string id)
        {
            return Resources.Load<SkillDefinition>($"Data/Skills/{id}");
        }
    }
}
