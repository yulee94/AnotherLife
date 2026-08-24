using AL.ChampionMode;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Input;
using AL.UI.Kingdom;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AL.UI.SharedMenu
{
    /// <summary>
    /// Attaches Shared Menu to ChampionArena / Kingdom and performs the
    /// Single-load MODE_SWITCH. Does not edit ChampionArenaSceneController.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class SharedMenuModeSwitchHost : MonoBehaviour
    {
        public SharedMenuOverlay Overlay { get; private set; }

        public bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        public static SharedMenuModeSwitchHost EnsureForScene(Scene scene)
        {
            return EnsureForSceneName(scene.name);
        }

        public static SharedMenuModeSwitchHost EnsureForSceneName(string sceneName)
        {
            if (!CrossModeSceneSwitch.IsSupportedHostScene(sceneName))
            {
                return null;
            }

            SharedMenuModeSwitchHost existing = Object.FindObjectOfType<SharedMenuModeSwitchHost>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(SharedMenuIds.HostName);
            return go.AddComponent<SharedMenuModeSwitchHost>();
        }

        private void Update()
        {
            if (SharedMenuTogglePressed())
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            SaveGameData save = ReadSave();
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(
                save,
                DetectCombat(),
                DetectUnsafe());
            if (Overlay == null)
            {
                Overlay = SharedMenuOverlay.Ensure(state);
            }
            else
            {
                Overlay.Build(state);
            }

            Overlay.BindInvoke(() => CommitFromMenu());
            IsOpen = true;
        }

        public void Close()
        {
            if (Overlay != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(Overlay.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(Overlay.gameObject);
                }

                Overlay = null;
            }

            IsOpen = false;
        }

        public CrossModeSwitchPlan Preview(string targetMode)
        {
            return CrossModeSceneSwitch.Plan(
                SceneManager.GetActiveScene().name,
                targetMode,
                ReadSave(),
                DetectCombat(),
                DetectUnsafe(),
                SharedMenuIds.InputSharedMenu);
        }

        public bool CommitFromMenu()
        {
            string current = Application.isPlaying
                ? SceneManager.GetActiveScene().name
                : SharedMenuIds.AdventureScene;
            if (string.IsNullOrEmpty(current))
            {
                current = SharedMenuIds.AdventureScene;
            }

            string target = CrossModeSceneSwitch.IsKingdomScene(current)
                ? SharedMenuIds.Adventure3D
                : SharedMenuIds.Kingdom2_5D;
            return Commit(current, target);
        }

        public bool Commit(string currentScene, string targetMode)
        {
            CrossModeSwitchPlan plan = CrossModeSceneSwitch.Plan(
                currentScene,
                targetMode,
                ReadSave(),
                DetectCombat(),
                DetectUnsafe(),
                SharedMenuIds.InputSharedMenu);
            bool returningFromKingdom =
                CrossModeSceneSwitch.IsKingdomScene(currentScene) &&
                string.Equals(
                    targetMode,
                    SharedMenuIds.Adventure3D,
                    System.StringComparison.Ordinal);
            return CrossModeSceneSwitch.TryCommit(
                plan,
                (sceneName, mode) =>
                {
                    if (returningFromKingdom)
                    {
                        KingdomTeachingInteraction.Observe("return_shared_menu");
                    }

                    LoadExclusive(sceneName, mode);
                });
        }

        internal static void LoadExclusive(string sceneName, LoadSceneMode mode)
        {
            SceneManager.LoadScene(sceneName, mode);
        }

        internal static SaveGameData ReadSave()
        {
            if (ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker) &&
                marker.TryGetExpected<ISaveGameService>(out var saveGame) &&
                saveGame != null)
            {
                return saveGame.CurrentSave;
            }

            return null;
        }

        internal static bool DetectUnsafe()
        {
            ChampionCombat player = FindPlayerCombat();
            return player != null && player.IsDead;
        }

        internal static bool DetectCombat()
        {
            BossDummyAI[] bosses = Object.FindObjectsOfType<BossDummyAI>();
            for (int i = 0; i < bosses.Length; i++)
            {
                if (bosses[i] != null && !bosses[i].IsDead)
                {
                    return true;
                }
            }

            ChampionCombat[] combats = Object.FindObjectsOfType<ChampionCombat>();
            for (int i = 0; i < combats.Length; i++)
            {
                ChampionCombat combat = combats[i];
                if (combat == null || combat.IsDead)
                {
                    continue;
                }

                if (combat.GetComponent<ChampionController>() == null &&
                    !string.Equals(combat.gameObject.name, FirstSessionChampionStart.PlayerObjectName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static ChampionCombat FindPlayerCombat()
        {
            ChampionController controller = Object.FindObjectOfType<ChampionController>();
            if (controller != null)
            {
                return controller.GetComponent<ChampionCombat>();
            }

            GameObject player = GameObject.Find(FirstSessionChampionStart.PlayerObjectName);
            return player != null ? player.GetComponent<ChampionCombat>() : null;
        }

        private static bool SharedMenuTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                return true;
            }

            return GameInput.CancelPressed();
        }

        private void OnDestroy()
        {
            Close();
        }
    }
}
