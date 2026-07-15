#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;

namespace AL.Utilities
{
    public class ProjectInitializer : MonoBehaviour
    {
        [ContextMenu("Setup Project Structure")]
        public void SetupProject()
        {
            Debug.Log("Initializing Another Life Project Structure...");

            // 1. Verify Folders
            string[] folders = new string[]
            {
                "Assets/AL/ScriptableObjects/Realms",
                "Assets/AL/ScriptableObjects/Buildings",
                "Assets/AL/ScriptableObjects/Resources",
                "Assets/AL/ScriptableObjects/Troops",
                "Assets/AL/ScriptableObjects/Classes",
                "Assets/AL/ScriptableObjects/Champions",
                "Assets/AL/ScriptableObjects/Skills",
                "Assets/AL/ScriptableObjects/Equipment",
                "Assets/AL/ScriptableObjects/Warmaster",
                "Assets/AL/ScriptableObjects/Bosses",
                "Assets/AL/ScriptableObjects/Chapters",
                "Assets/AL/ScriptableObjects/Dialogues",
                "Assets/AL/ScriptableObjects/Quests",
                "Assets/AL/ScriptableObjects/Narrative/Bosses",
                "Assets/AL/ScriptableObjects/Narrative/Loot",
                "Assets/AL/ScriptableObjects/Narrative/Artifacts",
                "Assets/AL/ScriptableObjects/Narrative/Factions"
            };

            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    string name = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                    Debug.Log($"Created folder: {folder}");
                }
            }

            // 2. Generate Realm Templates
            CreateRealmTemplate(RealmId.Stonehold, "Stonehold Dwarves", "Mountain kings and master smiths.");
            CreateRealmTemplate(RealmId.Eldergrove, "Eldergrove Elves", "Ancient protectors of the world tree.");
            CreateRealmTemplate(RealmId.Crownlands, "Crownlands Humans", "The adaptive and numerous people of the plains.");
            CreateRealmTemplate(RealmId.Umbral, "Umbral Dark Elves", "Masters of shadow and clandestine arts.");

            // 3. Generate Building Templates (15 core buildings)
            string[] buildingIds = { "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine", "Barracks", "Academy", "Market", "Storehouse", "Forge", "Stable", "Workshop", "Embassy", "Wall", "Watchtower" };
            foreach(var id in buildingIds)
            {
                CreateBuildingTemplate(id);
            }

            // 4. Generate Boss & Loot Templates
            CreateBossTemplate("ferrum", "Ferrum", "Iron Dragon of Stonehold. High Armor, physical AoE.", 5000, 150, 200);
            CreateBossTemplate("virens", "Virens", "Green Dragon of Eldergrove. Poison DoT, healing reduction.", 4000, 180, 100);
            CreateBossTemplate("aurelius", "Aurelius", "Gold Dragon of Crownlands. High Magic damage, blinding effects.", 4500, 200, 120);
            CreateBossTemplate("nox", "Nox", "Void Dragon of Umbral. Stealth phases, health lifesteal.", 3500, 250, 80);

            // 5. Generate Ruin Bosses (Task 14 Expansion)
            CreateBossTemplate("ruin_stone", "The Granite Warden", "Ancient automaton guarding the First King's Anvil.", 12000, 400, 600);
            CreateBossTemplate("ruin_elf", "The Spectral Stag", "Ghostly protector of the Whisper Glade.", 10000, 500, 300);
            CreateBossTemplate("ruin_human", "The Fallen Paladin", "Corrupted guardian of the Golden Aegis.", 11000, 600, 450);
            CreateBossTemplate("ruin_dark", "The Abyssal Shade", "Manifestation of the First Rift.", 9000, 800, 200);

            // 6. Generate Outer Warzone Bosses
            CreateBossTemplate("cinders", "The Behemoth of Cinders", "Massive volcanic colossus. Incomparably hard. Deals massive fire AoE.", 50000, 1500, 1000);
            CreateBossTemplate("abyssal", "The Abyssal Horror", "Eldritch monstrosity from the depths. Incomparably hard. Drains mana and sanity.", 60000, 1200, 800);

            // 6. Generate Rare Loot Templates
            CreateLootTemplate("ring_stonehold", "Ring of the Mountain King", EquipmentSlot.Trinket, 0.001f, true, ItemGrade.Legendary, RealmId.Stonehold, "loot_stonehold_ring", new Color(1.0f, 0.46f, 0.14f), new Color(0.36f, 0.30f, 0.25f), 0.74f, 1.35f, attack: 8, defense: 18, health: 120);
            CreateLootTemplate("ring_eldergrove", "Ring of Forest Harmony", EquipmentSlot.Trinket, 0.001f, true, ItemGrade.Legendary, RealmId.Eldergrove, "loot_eldergrove_ring", new Color(0.32f, 1.0f, 0.52f), new Color(0.94f, 0.76f, 0.28f), 0.72f, 1.35f, defense: 10, health: 180);
            CreateLootTemplate("ring_crownlands", "Ring of Royal Decree", EquipmentSlot.Trinket, 0.001f, true, ItemGrade.Legendary, RealmId.Crownlands, "loot_crownlands_ring", new Color(0.26f, 0.54f, 1.0f), new Color(1.0f, 0.78f, 0.22f), 0.76f, 1.35f, attack: 10, defense: 10, health: 120);
            CreateLootTemplate("ring_umbral", "Ring of Shadow Step", EquipmentSlot.Trinket, 0.001f, true, ItemGrade.Legendary, RealmId.Umbral, "loot_umbral_ring", new Color(0.78f, 0.12f, 1.0f), new Color(0.95f, 0.06f, 0.18f), 0.78f, 1.35f, attack: 18, health: 80);

            CreateLootTemplate("amulet_warzone", "Amulet of the Warzone", EquipmentSlot.Trinket, 0.0005f, true, ItemGrade.Mythic, RealmId.None, "loot_warzone_amulet", new Color(1.0f, 0.28f, 0.08f), new Color(0.18f, 0.16f, 0.14f), 0.92f, 1.65f, attack: 24, defense: 14, health: 220);
            CreateLootTemplate("pendant_eternity", "Pendant of Eternity", EquipmentSlot.Trinket, 0.0005f, true, ItemGrade.Celestial, RealmId.Crownlands, "loot_eternity_pendant", new Color(0.72f, 0.92f, 1.0f), new Color(1.0f, 0.82f, 0.34f), 1.0f, 1.95f, attack: 28, defense: 28, health: 320);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project Setup Complete!");
        }

        private void CreateRealmTemplate(RealmId id, string name, string description)
        {
            string path = $"Assets/AL/ScriptableObjects/Realms/{id}.asset";
            if (File.Exists(path)) return;

            RealmDefinition realm = ScriptableObject.CreateInstance<RealmDefinition>();
            realm.Id = id;
            realm.RealmName = name;
            realm.Description = description;

            AssetDatabase.CreateAsset(realm, path);
            Debug.Log($"Created Realm Template: {path}");
        }

        private void CreateBuildingTemplate(string id)
        {
            string path = $"Assets/AL/ScriptableObjects/Buildings/{id}.asset";
            if (File.Exists(path)) return;

            BuildingDefinition def = ScriptableObject.CreateInstance<BuildingDefinition>();
            def.Id = id;
            def.DisplayName = id.Replace("Mill", " Mill").Replace("Hall", " Hall").Replace("Mine", " Mine");
            def.MaxLevel = 10;

            AssetDatabase.CreateAsset(def, path);
            Debug.Log($"Created Building Template: {path}");
        }

        private void CreateBossTemplate(string id, string name, string desc, int hp, int atk, int arm)
        {
            string path = $"Assets/AL/ScriptableObjects/Narrative/Bosses/{id}.asset";
            if (File.Exists(path)) return;

            BossDefinition def = ScriptableObject.CreateInstance<BossDefinition>();
            def.Id = id;
            def.BossName = name;
            def.Description = desc;
            def.Health = hp;
            def.Attack = atk;
            def.Armor = arm;

            AssetDatabase.CreateAsset(def, path);
            Debug.Log($"Created Boss Template: {path}");
        }

        private void CreateLootTemplate(
            string id,
            string name,
            EquipmentSlot slot,
            float dropRate,
            bool announce,
            ItemGrade grade = ItemGrade.Common,
            RealmId visualRealm = RealmId.None,
            string visualEffectKey = "loot_common",
            Color? primaryColor = null,
            Color? secondaryColor = null,
            float auraIntensity = 0.15f,
            float revealScale = 1f,
            int attack = 0,
            int defense = 0,
            int health = 0)
        {
            string path = $"Assets/AL/ScriptableObjects/Narrative/Loot/{id}.asset";
            if (File.Exists(path)) return;

            EquipmentDefinition def = ScriptableObject.CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.DisplayName = name;
            def.Slot = slot;
            def.DropRate = dropRate;
            def.AnnounceWorldDrop = announce;
            def.Grade = grade;
            def.VisualRealm = visualRealm;
            def.VisualEffectKey = visualEffectKey;
            def.PrimaryColor = primaryColor ?? new Color(0.62f, 0.68f, 0.74f);
            def.SecondaryColor = secondaryColor ?? new Color(0.30f, 0.36f, 0.42f);
            def.AuraIntensity = auraIntensity;
            def.RevealScale = revealScale;
            def.AttackBonus = attack;
            def.DefenseBonus = defense;
            def.HealthBonus = health;

            AssetDatabase.CreateAsset(def, path);
            Debug.Log($"Created Loot Template: {path}");
        }
    }
}
#endif
