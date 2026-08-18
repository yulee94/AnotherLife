using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Definitions.Narrative;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalGameDataService : IGameDataService
    {
        private Dictionary<RealmId, RealmDefinition> _realms = new Dictionary<RealmId, RealmDefinition>();
        private Dictionary<string, BuildingDefinition> _buildings = new Dictionary<string, BuildingDefinition>();
        private Dictionary<string, ResearchState> _researchDefaults = new Dictionary<string, ResearchState>();
        private Dictionary<string, ChampionDefinition> _champions = new Dictionary<string, ChampionDefinition>();

        public LocalGameDataService()
        {
            InitializeFallbackData();
            InitializeAutomatedContent();
            InitializeStoryData();
            InitializeChampionArchetypes();
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
                def.ConstructionLevels = CreateConstructionLevels(bId);
                _buildings[bId] = def;
            }

            // Tech Tree
            string[] techs = { "Steel Forging", "Plate Armor", "Advanced Masonry", "Irrigation", "Ballistics", "Logistics", "Trade Routes", "Arcane Study" };
            foreach (var tech in techs)
            {
                _researchDefaults[tech] = new ResearchState { ResearchId = tech, Level = 0 };
            }
        }

        private void InitializeChampionArchetypes()
        {
            // Greybox champion archetypes (hardcoded, one per class family, realm-aligned).
            // These are the "hardcoded LocalGameDataService archetypes" the character creation
            // screen surfaces. IDs are lowercase snake_case per the data-ID convention.
            AddChampion(
                "champion_stonehold_vanguard",
                "Bronn Ironhide",
                RealmId.Stonehold,
                ClassFamily.Warrior,
                SubclassId.Vanguard,
                new ChampionBaseStats { MaxHealth = 1250, MaxMana = 80, Attack = 55, Defense = 45, Speed = 8, CritRate = 5 },
                "greataxe", "towershield",
                new[] { ("skill_iron_bulwark", "Iron Bulwark", SkillTargetType.Self), ("skill_shield_slam", "Shield Slam", SkillTargetType.Single) });

            AddChampion(
                "champion_eldergrove_archmage",
                "Lyra Moonshadow",
                RealmId.Eldergrove,
                ClassFamily.Mage,
                SubclassId.Archmage,
                new ChampionBaseStats { MaxHealth = 820, MaxMana = 150, Attack = 78, Defense = 18, Speed = 10, CritRate = 8 },
                "staff", "tome",
                new[] { ("skill_arcane_bolt", "Arcane Bolt", SkillTargetType.Single), ("skill_verdant_nova", "Verdant Nova", SkillTargetType.AoE) });

            AddChampion(
                "champion_crownlands_sharpshooter",
                "Aurelia Dawnblade",
                RealmId.Crownlands,
                ClassFamily.Ranger,
                SubclassId.Sharpshooter,
                new ChampionBaseStats { MaxHealth = 900, MaxMana = 110, Attack = 62, Defense = 26, Speed = 15, CritRate = 20 },
                "longbow", "quiver",
                new[] { ("skill_piercing_shot", "Piercing Shot", SkillTargetType.Single), ("skill_hawk_eye", "Hawk Eye", SkillTargetType.Self) });

            AddChampion(
                "champion_umbral_shadowblade",
                "Vex Nocturne",
                RealmId.Umbral,
                ClassFamily.Assassin,
                SubclassId.Shadowblade,
                new ChampionBaseStats { MaxHealth = 850, MaxMana = 100, Attack = 72, Defense = 16, Speed = 22, CritRate = 30 },
                "twinblades", "shroud",
                new[] { ("skill_shadowstep", "Shadowstep", SkillTargetType.Self), ("skill_umbral_execute", "Umbral Execute", SkillTargetType.Single) });
        }

        private void AddChampion(
            string id,
            string displayName,
            RealmId realm,
            ClassFamily family,
            SubclassId subclass,
            ChampionBaseStats baseStats,
            string weaponStyleId,
            string offhandStyleId,
            (string Id, string DisplayName, SkillTargetType TargetType)[] skills)
        {
            var champion = ScriptableObject.CreateInstance<ChampionDefinition>();
            champion.Id = id;
            champion.DisplayName = displayName;
            champion.Realm = realm;
            champion.Family = family;
            champion.Subclass = subclass;
            champion.BaseStats = baseStats;
            champion.WeaponStyleId = weaponStyleId;
            champion.OffhandStyleId = offhandStyleId;

            champion.BaseSkills = new SkillDefinition[skills.Length];
            for (int i = 0; i < skills.Length; i++)
            {
                var skill = ScriptableObject.CreateInstance<SkillDefinition>();
                skill.Id = skills[i].Id;
                skill.DisplayName = skills[i].DisplayName;
                skill.TargetType = skills[i].TargetType;
                skill.Cooldown = 6f;
                skill.Power = 1f;
                champion.BaseSkills[i] = skill;
            }

            _champions[id] = champion;
        }

        private void InitializeStoryData()
        {
            // Chapter 1: The Proof of Worth
            AddChapter(RealmId.Stonehold, "C1_SH", "The Echoes of Iron", "Re-opening the ancestral Deep Forge and defeating Ferrum the Iron Dragon to prove your worth.");
            AddChapter(RealmId.Eldergrove, "C1_EG", "Whispers of the Sapling", "Investigating a blight on the World Tree and purging Virens the Blighted Dragon.");
            AddChapter(RealmId.Crownlands, "C1_CL", "The King's Decree", "Rebuilding the capital and seeking the blessing of Aurelius the Gold Dragon.");
            AddChapter(RealmId.Umbral, "C1_UM", "Shadows of the Void", "Rituals to stabilize the volcanic rifts and taming Nox the Void Dragon.");

            // Chapter 2: The Treasure Hunt
            AddChapter(RealmId.Stonehold, "C2_SH", "The Smuggler's Trail", "Discovering Elven scouting parties deep in the mountain passes searching for the Ring of the Mountain King.");
            AddChapter(RealmId.Eldergrove, "C2_EG", "Shadows in the Mist", "Capturing a Human spy attempting to steal the Ring of Forest Harmony.");
            AddChapter(RealmId.Crownlands, "C2_CL", "Border Skirmishes", "Countering Dwarven expansion and protecting the Ring of Royal Decree.");
            AddChapter(RealmId.Umbral, "C2_UM", "Night's Whisper", "Sabotaging Human trade routes to retrieve the stolen Ring of Shadow Step.");

            // Chapter 3
            AddChapter(RealmId.Stonehold, "C3_SH", "Heart of the Mountain", "The discovery of the first Ancestral Gem within the core forge.");
            AddChapter(RealmId.Eldergrove, "C3_EG", "The Forest's Tear", "A mystical gem is born from the tree's purest sap.");
            AddChapter(RealmId.Crownlands, "C3_CL", "The Sovereign's Jewel", "The discovery of a divine gem buried beneath the royal cathedral.");
            AddChapter(RealmId.Umbral, "C3_UM", "The Void Shard", "Retrieving a crystal from the heart of the volcanic rifts.");

            // Chapters 7-9: Ancient Legacies
            AddChapter(RealmId.Stonehold, "C7_SH", "The First King's Anvil", "Locating the legendary weapon of the founder.");
            AddChapter(RealmId.Eldergrove, "C7_EG", "Whisper of the Glade", "Restoring the original bow of the Forest Sentinels.");
            AddChapter(RealmId.Crownlands, "C7_CL", "The Golden Aegis", "Recovering the shield that stood during the First War.");
            AddChapter(RealmId.Umbral, "C7_UM", "Void's Edge", "Forging the blade from the remains of the First Rift.");

            // Chapters 10-12: The Heavens Ascended
            AddChapter(RealmId.Stonehold, "C10_SH", "The Celestial Rift", "Ancient portals atop the mountain peaks begin to pulse with sky-light.");
            AddChapter(RealmId.Eldergrove, "C10_EG", "Whispers of the Sky", "The highest leaves of the World Tree touch a new realm of magic.");
            AddChapter(RealmId.Crownlands, "C10_CL", "The Sun-Gate Opens", "Portals appear in the clouds, revealing the path to the High Celestials.");
            AddChapter(RealmId.Umbral, "C10_UM", "Void's Reach", "Shadow magic begins to pierce the heavens themselves.");

            AddChapter(RealmId.Stonehold, "C11_SH", "Trial of the Granite King", "Confronting the guardians of the first floating fortress.");
            AddChapter(RealmId.Eldergrove, "C11_EG", "Emerald Sky Trial", "Navigating the magical storms of the upper islands.");
            AddChapter(RealmId.Crownlands, "C11_CL", "Radiant Vigil", "Proving your faith and strength to the Sky Wardens.");
            AddChapter(RealmId.Umbral, "C11_UM", "Midnight Ascent", "Infiltrating the light-fortresses of the sky.");

            AddChapter(RealmId.Stonehold, "C12_SH", "Throne of the Mountain Sky", "Reaching the ultimate seat of power and meeting the High Celestial.");
            AddChapter(RealmId.Eldergrove, "C12_EG", "Glade of the Stars", "Securing the forest's place among the celestial powers.");
            AddChapter(RealmId.Crownlands, "C12_CL", "Empire of Light", "Establishing a holy covenant between earth and sky.");
            AddChapter(RealmId.Umbral, "C12_UM", "The Void Throne", "Claiming the sky for the shadows of the rift.");

            // Otherworld Omen Foreshadowing
            AddChapter(RealmId.None, "C_OMEN", "The Otherworld Omen", "Strange signals from beyond the celestial rift suggest we are not alone.");

            InitializeSkillSoulQuests();
        }

        private void InitializeSkillSoulQuests()
        {
            // All 16 Subclass Soul Quests
            AddSoulQuest(SubclassId.Vanguard, "Frontline Eternity", "Stand as the immovable object against the Celestial Tide.");
            AddSoulQuest(SubclassId.Guardian, "The Unbreakable Vow", "Protect the Celestial Gate from an infinite onslaught.");
            AddSoulQuest(SubclassId.Berserker, "Primal Rage", "Tame a legendary star-lion in a cosmic storm.");
            AddSoulQuest(SubclassId.Pyromancer, "Sun-Fire Ascension", "Absorb the heat of the celestial sun into your core.");
            AddSoulQuest(SubclassId.Cryomancer, "Absolute Zero", "Freeze the floating waterfalls of the Sky Castle.");
            AddSoulQuest(SubclassId.Archmage, "Void Ascension", "Merge celestial light with shadow rift magic.");
            AddSoulQuest(SubclassId.Sharpshooter, "Star-Piercer", "Strike a target on the furthest floating island.");
            AddSoulQuest(SubclassId.Stalker, "The Celestial Hunt", "Track a creature made of pure starlight.");
            AddSoulQuest(SubclassId.Beastmaster, "Sky-Bond", "Tame a High Celestial Gryphon.");
            AddSoulQuest(SubclassId.Shadowblade, "Event Horizon", "Become one with the shadow cast by the Celestial Gate.");
            AddSoulQuest(SubclassId.Infiltrator, "Heaven's Ghost", "Bypass the divine sentinels without being detected.");
            AddSoulQuest(SubclassId.Nightstalker, "Void Reaper", "Execute the shadows lurking in the celestial gardens.");
            AddSoulQuest(SubclassId.Paladin, "Divine Resonance", "Synchronize your armor with the High Celestial's song.");
            AddSoulQuest(SubclassId.Necromancer, "Celestial Decay", "Study the life-cycles of the eternal sky-beings.");
            AddSoulQuest(SubclassId.Slayer, "God-Killer", "Defeat the guardian of the Forbidden Island.");
            AddSoulQuest(SubclassId.Druid, "World-Root Reach", "Connect the World Tree to the floating islands.");
        }

        private void AddSoulQuest(SubclassId subclass, string title, string description)
        {
            var quest = ScriptableObject.CreateInstance<SkillSoulQuestDefinition>();
            quest.Id = $"SQ_{subclass}";
            quest.AssociatedSubclass = subclass;
            quest.Title = title;
            quest.Description = description;
            // Additional registration logic if needed
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

        private static List<BuildingConstructionLevelDefinition> CreateConstructionLevels(
            string buildingId)
        {
            int[] baseCosts =
            {
                100, 175, 300, 475, 700, 1000, 1400, 1900, 2500, 3250
            };
            int[] durations =
            {
                10, 30, 120, 300, 900, 1800, 3600, 7200, 14400, 28800
            };

            int scalePercent = GetConstructionCostScalePercent(buildingId);
            var levels = new List<BuildingConstructionLevelDefinition>(10);
            for (int index = 0; index < baseCosts.Length; index++)
            {
                long budget = checked((baseCosts[index] * (long)scalePercent + 99L) / 100L);
                levels.Add(new BuildingConstructionLevelDefinition
                {
                    TargetLevel = index + 1,
                    DurationSeconds = durations[index],
                    Costs = CreateConstructionCosts(buildingId, budget)
                });
            }

            return levels;
        }

        private static int GetConstructionCostScalePercent(string buildingId)
        {
            switch (buildingId)
            {
                case "TownHall": return 140;
                case "Farm":
                case "LumberMill": return 80;
                case "Quarry":
                case "Market": return 90;
                case "Storehouse": return 85;
                case "GoldMine":
                case "Stable":
                case "Watchtower": return 100;
                case "Barracks":
                case "Workshop": return 110;
                case "Forge": return 115;
                case "Academy":
                case "Embassy": return 120;
                case "Wall": return 95;
                default: return 100;
            }
        }

        private static List<BuildingConstructionCostDefinition> CreateConstructionCosts(
            string buildingId,
            long budget)
        {
            switch (buildingId)
            {
                case "Farm":
                case "LumberMill":
                    return SplitCost(budget, ResourceType.Wood, 70, ResourceType.Stone, 30);
                case "Quarry":
                case "GoldMine":
                    return SplitCost(budget, ResourceType.Wood, 40, ResourceType.Stone, 60);
                case "Barracks":
                case "Wall":
                case "Watchtower":
                    return SplitCost(
                        budget,
                        ResourceType.Stone,
                        55,
                        ResourceType.Wood,
                        30,
                        ResourceType.Gold,
                        15);
                case "Academy":
                    return SplitCost(
                        budget,
                        ResourceType.Stone,
                        40,
                        ResourceType.Wood,
                        25,
                        ResourceType.ManaStone,
                        35);
                case "Forge":
                case "Workshop":
                    return SplitCost(
                        budget,
                        ResourceType.Stone,
                        45,
                        ResourceType.Wood,
                        25,
                        ResourceType.Ore,
                        30);
                case "Market":
                case "Embassy":
                    return SplitCost(
                        budget,
                        ResourceType.Wood,
                        45,
                        ResourceType.Stone,
                        25,
                        ResourceType.Gold,
                        30);
                case "Stable":
                    return SplitCost(
                        budget,
                        ResourceType.Wood,
                        55,
                        ResourceType.Stone,
                        25,
                        ResourceType.Gold,
                        20);
                case "Storehouse":
                    return SplitCost(budget, ResourceType.Wood, 60, ResourceType.Stone, 40);
                case "TownHall":
                default:
                    return SplitCost(
                        budget,
                        ResourceType.Stone,
                        45,
                        ResourceType.Wood,
                        35,
                        ResourceType.Gold,
                        20);
            }
        }

        private static List<BuildingConstructionCostDefinition> SplitCost(
            long budget,
            ResourceType firstType,
            int firstPercent,
            ResourceType secondType,
            int secondPercent,
            ResourceType? thirdType = null,
            int thirdPercent = 0)
        {
            if (budget <= 0 ||
                firstPercent <= 0 ||
                secondPercent <= 0 ||
                firstPercent + secondPercent + thirdPercent != 100 ||
                (thirdPercent > 0 && !thirdType.HasValue))
            {
                throw new InvalidOperationException(
                    "Building construction cost profile is invalid.");
            }

            long firstAmount = Math.Max(1L, budget * firstPercent / 100L);
            long secondAmount = Math.Max(1L, budget * secondPercent / 100L);
            long thirdAmount = thirdPercent > 0
                ? Math.Max(1L, budget - firstAmount - secondAmount)
                : 0L;
            if (thirdPercent == 0)
            {
                secondAmount = Math.Max(1L, budget - firstAmount);
            }

            var costs = new List<BuildingConstructionCostDefinition>
            {
                new BuildingConstructionCostDefinition
                {
                    ResourceType = firstType,
                    Amount = firstAmount
                },
                new BuildingConstructionCostDefinition
                {
                    ResourceType = secondType,
                    Amount = secondAmount
                }
            };

            if (thirdType.HasValue)
            {
                costs.Add(new BuildingConstructionCostDefinition
                {
                    ResourceType = thirdType.Value,
                    Amount = thirdAmount
                });
            }

            return costs;
        }

        public RealmDefinition GetRealm(RealmId id) => _realms.TryGetValue(id, out var r) ? r : null;
        public IEnumerable<RealmDefinition> GetAllRealms() => _realms.Values;
        public BuildingDefinition GetBuilding(string id) => _buildings.TryGetValue(id, out var b) ? b : null;
        public TroopDefinition GetTroop(string id) => null; // To be implemented
        public ChampionDefinition GetChampion(string id) =>
            string.IsNullOrEmpty(id) || !_champions.TryGetValue(id, out var c) ? null : c;
        public IEnumerable<ChampionDefinition> GetAllChampions() => _champions.Values;
        public SkillDefinition GetSkill(string id) => null; // To be implemented
    }
}
