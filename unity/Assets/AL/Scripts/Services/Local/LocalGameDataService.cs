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
        private Dictionary<string, BuildingDefinition> _buildings = new Dictionary<string, BuildingDefinition>();
        private Dictionary<string, ResearchState> _researchDefaults = new Dictionary<string, ResearchState>();

        public LocalGameDataService()
        {
            InitializeFallbackData();
            InitializeAutomatedContent();
            InitializeStoryData();
        }

        private void InitializeFallbackData()
        {
            // Factions
            CreateFallbackRealm(RealmId.Stonehold, "Stonehold Dwarves", "Mountain kings and master smiths.\n\nPerks:\n+20% Stone\n+10% Def", "Perks: Resilience");
            CreateFallbackRealm(RealmId.Eldergrove, "Eldergrove Elves", "Forest guardians and peerless mages.\n\nPerks:\n+20% Wood\n+15% Magic", "Perks: Harmony");
            CreateFallbackRealm(RealmId.Crownlands, "Crownlands Humans", "Adaptive leaders of the central plains.\n\nPerks:\n+15% Gold\n+10% All Atk", "Perks: Ambition");
            CreateFallbackRealm(RealmId.Umbral, "Umbral Dark Elves", "Masters of shadow and volcanic power.\n\nPerks:\n+20% Crit\n+15% Speed", "Perks: Cunning");
        }

        private void InitializeAutomatedContent()
        {
            // 15 Core Buildings
            string[] bIds = { "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine", "Barracks", "Academy", "Market", "Storehouse", "Forge", "Stable", "Workshop", "Embassy", "Wall", "Watchtower" };
            foreach (var bId in bIds)
            {
                var def = ScriptableObject.CreateInstance<BuildingDefinition>();
                def.Id = bId;
                def.DisplayName = bId.Replace("Mill", " Mill").Replace("Hall", " Hall").Replace("Mine", " Mine");
                def.MaxLevel = 10;
                _buildings[bId] = def;
            }

            // Tech Tree
            string[] techs = { "Steel Forging", "Plate Armor", "Advanced Masonry", "Irrigation", "Ballistics", "Logistics", "Trade Routes", "Arcane Study" };
            foreach (var tech in techs)
            {
                _researchDefaults[tech] = new ResearchState { ResearchId = tech, Level = 0 };
            }
        }

        private void InitializeStoryData()
        {
            // Chapter 1 for all Realms
            AddChapter(RealmId.Stonehold, "C1_SH", "The Echoes of Iron", "Re-opening the ancestral Deep Forge.");
            AddChapter(RealmId.Eldergrove, "C1_EG", "Whispers of the Sapling", "Investigating a blight on the World Tree.");
            AddChapter(RealmId.Crownlands, "C1_CL", "The King's Decree", "Rebuilding the capital after the Great Siege.");
            AddChapter(RealmId.Umbral, "C1_UM", "Shadows of the Void", "Rituals to stabilize the volcanic rifts.");

            // Chapter 2 for all Realms - EXPANSION
            AddChapter(RealmId.Stonehold, "C2_SH", "The Hearth's Awakening", "Relighting the Great Furnace of Stonehold.");
            AddChapter(RealmId.Eldergrove, "C2_EG", "Roots of Conflict", "Defending the outer glades from the blighted spawns.");
            AddChapter(RealmId.Crownlands, "C2_CL", "The Merchant's Path", "Securing the trade routes from the eastern borders.");
            AddChapter(RealmId.Umbral, "C2_UM", "Void Walkers", "Infiltrating the crystalline caves to contain the rift.");
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

        public RealmDefinition GetRealm(RealmId id) => _realms.TryGetValue(id, out var r) ? r : null;
        public IEnumerable<RealmDefinition> GetAllRealms() => _realms.Values;
        public BuildingDefinition GetBuilding(string id) => _buildings.TryGetValue(id, out var b) ? b : null;
        public TroopDefinition GetTroop(string id) => null; // To be implemented
        public ChampionDefinition GetChampion(string id) => null; // To be implemented
        public SkillDefinition GetSkill(string id) => null; // To be implemented
    }
}
