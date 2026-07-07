#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
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
                "Assets/AL/ScriptableObjects/Bosses"
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
    }
}
#endif
