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

            InitializeStoryData();
            InitializeAutomatedContent();
        }

        private Dictionary<string, BuildingDefinition> _buildings = new Dictionary<string, BuildingDefinition>();

        private void InitializeAutomatedContent()
        {
            string[] buildingTypes = { "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine", "Barracks", "Academy", "Market", "Storehouse", "Forge", "Stable", "Workshop", "Embassy", "Wall", "Watchtower" };

            foreach (var bId in buildingTypes)
            {
                var def = ScriptableObject.CreateInstance<BuildingDefinition>();
                def.Id = bId;
                def.DisplayName = bId.Replace("Mill", " Mill").Replace("Hall", " Hall").Replace("Mine", " Mine");
                def.MaxLevel = 10;
                _buildings[bId] = def;
            }
        }

        private void InitializeStoryData()
        {
            // Initial Chapters for each Realm
            AddChapter(RealmId.Stonehold, "C1_SH", "The Echoes of Iron", "Re-opening the ancestral Deep Forge.");
            AddChapter(RealmId.Eldergrove, "C1_EG", "Whispers of the Sapling", "Investigating a blight on the World Tree.");
            AddChapter(RealmId.Crownlands, "C1_CL", "The King's Decree", "Rebuilding the capital after the Great Siege.");
            AddChapter(RealmId.Umbral, "C1_UM", "Shadows of the Void", "Rituals to stabilize the volcanic rifts.");
        }

        private void AddChapter(RealmId realmId, string id, string title, string summary)
        {
            var chapter = ScriptableObject.CreateInstance<ChapterDefinition>();
            chapter.Id = id;
            chapter.Title = title;
            chapter.LoreSummary = summary;
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
            if (_buildings.TryGetValue(id, out var def)) return def;
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
