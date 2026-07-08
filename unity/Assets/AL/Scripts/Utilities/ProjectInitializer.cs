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
                "Assets/AL/ScriptableObjects/Narrative/Loot"
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

            // 5. Generate Outer Warzone Bosses
            CreateBossTemplate("cinders", "The Behemoth of Cinders", "Massive volcanic colossus. Incomparably hard. Deals massive fire AoE.", 50000, 1500, 1000);
            CreateBossTemplate("abyssal", "The Abyssal Horror", "Eldritch monstrosity from the depths. Incomparably hard. Drains mana and sanity.", 60000, 1200, 800);

            // 6. Generate Rare Loot Templates
            CreateLootTemplate("ring_stonehold", "Ring of the Mountain King", EquipmentSlot.Trinket, 0.001f, true);
            CreateLootTemplate("ring_eldergrove", "Ring of Forest Harmony", EquipmentSlot.Trinket, 0.001f, true);
            CreateLootTemplate("ring_crownlands", "Ring of Royal Decree", EquipmentSlot.Trinket, 0.001f, true);
            CreateLootTemplate("ring_umbral", "Ring of Shadow Step", EquipmentSlot.Trinket, 0.001f, true);

            CreateLootTemplate("amulet_warzone", "Amulet of the Warzone", EquipmentSlot.Trinket, 0.0005f, true);
            CreateLootTemplate("pendant_eternity", "Pendant of Eternity", EquipmentSlot.Trinket, 0.0005f, true);

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

        private void CreateLootTemplate(string id, string name, EquipmentSlot slot, float dropRate, bool announce)
        {
            string path = $"Assets/AL/ScriptableObjects/Narrative/Loot/{id}.asset";
            if (File.Exists(path)) return;

            EquipmentDefinition def = ScriptableObject.CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.DisplayName = name;
            def.Slot = slot;
            def.DropRate = dropRate;
            def.AnnounceWorldDrop = announce;

            AssetDatabase.CreateAsset(def, path);
            Debug.Log($"Created Loot Template: {path}");
        }
    }
}
#endif
