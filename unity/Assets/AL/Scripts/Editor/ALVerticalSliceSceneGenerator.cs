#if UNITY_EDITOR
using AL.ChampionMode;
using AL.Core;
using AL.UI;
using AL.UI.Kingdom;
using AL.UI.RealmSelection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AL.EditorTools
{
    public static class ALVerticalSliceSceneGenerator
    {
        private const string SceneFolder = "Assets/AL/Scenes";
        private const string BootScene = SceneFolder + "/Boot.unity";
        private const string RealmSelectionScene = SceneFolder + "/RealmSelection.unity";
        private const string KingdomScene = SceneFolder + "/Kingdom.unity";
        private const string ChampionArenaScene = SceneFolder + "/ChampionArena.unity";

        [MenuItem("Another Life/Generate Vertical Slice Scenes")]
        public static void Generate()
        {
            EnsureSceneFolder();
            CreateBootScene();
            CreateRealmSelectionScene();
            CreateKingdomScene();
            CreateChampionArenaScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Another Life] Generated Boot, RealmSelection, Kingdom, and ChampionArena scenes.");
        }

        public static void GenerateFromBatchmode()
        {
            Generate();
        }

        private static void CreateBootScene()
        {
            var scene = NewScene("Boot");
            var boot = new GameObject("Boot");
            boot.AddComponent<Bootloader>();
            boot.AddComponent<BootController>();
            AddEventSystem();
            Save(scene, BootScene);
        }

        private static void CreateRealmSelectionScene()
        {
            var scene = NewScene("RealmSelection");
            var controller = new GameObject("RealmSelectionController");
            controller.AddComponent<RealmSelectionController>();
            AddEventSystem();
            Save(scene, RealmSelectionScene);
        }

        private static void CreateKingdomScene()
        {
            var scene = NewScene("Kingdom");
            var controller = new GameObject("KingdomSceneController");
            controller.AddComponent<KingdomSceneController>();
            AddEventSystem();
            Save(scene, KingdomScene);
        }

        private static void CreateChampionArenaScene()
        {
            var scene = NewScene("ChampionArena");
            var controller = new GameObject("ChampionArenaSceneController");
            controller.AddComponent<ChampionArenaSceneController>();
            AddEventSystem();
            Save(scene, ChampionArenaScene);
        }

        private static Scene NewScene(string sceneName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = sceneName;
            return scene;
        }

        private static void AddEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static void Save(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScene, true),
                new EditorBuildSettingsScene(RealmSelectionScene, true),
                new EditorBuildSettingsScene(KingdomScene, true),
                new EditorBuildSettingsScene(ChampionArenaScene, true)
            };
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AL"))
            {
                AssetDatabase.CreateFolder("Assets", "AL");
            }

            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets/AL", "Scenes");
            }
        }
    }
}
#endif

