using AL.ChampionMode;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Input;
using AL.UI.Kingdom;
using AL.UI.WorldMap;
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
        private System.IDisposable _gameplaySuppression;

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
            if (!scene.IsValid() || !scene.isLoaded ||
                !CrossModeSceneSwitch.IsSupportedHostScene(scene.name))
            {
                return null;
            }

            SharedMenuModeSwitchHost candidate = ElectSceneHost(scene, activeOnly: true) ??
                                                   ElectSceneHost(scene, activeOnly: false);
            if (candidate != null)
            {
                if (!candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.SetActive(true);
                }

                candidate.enabled = true;
                return candidate;
            }

            var go = new GameObject(SharedMenuIds.HostName);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<SharedMenuModeSwitchHost>();
        }

        public static SharedMenuModeSwitchHost EnsureForSceneName(string sceneName)
        {
            if (!CrossModeSceneSwitch.IsSupportedHostScene(sceneName))
            {
                return null;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(scene.name, sceneName, System.StringComparison.Ordinal))
                {
                    return EnsureForScene(scene);
                }
            }

            if (Application.isPlaying)
            {
                return null;
            }

            var go = new GameObject(SharedMenuIds.HostName);
            return go.AddComponent<SharedMenuModeSwitchHost>();
        }

        private void Update()
        {
            if (!OwnsAutomaticInput())
            {
                return;
            }

            if (SharedMenuTogglePressed() &&
                SharedMenuInputCoordinator.TryConsume(this, Time.frameCount))
            {
                Toggle();
            }
        }

        private bool OwnsAutomaticInput()
        {
            Scene ownerScene = gameObject.scene;
            return ownerScene.IsValid() && ownerScene.isLoaded &&
                   ownerScene == SceneManager.GetActiveScene() &&
                   OwnsAutomaticInputForSceneName(ownerScene.name) &&
                   ElectSceneHost(ownerScene, activeOnly: true) == this;
        }

        private static SharedMenuModeSwitchHost ElectSceneHost(
            Scene scene,
            bool activeOnly)
        {
            SharedMenuModeSwitchHost winner = null;
            SharedMenuModeSwitchHost[] hosts =
                Resources.FindObjectsOfTypeAll<SharedMenuModeSwitchHost>();
            for (int i = 0; i < hosts.Length; i++)
            {
                SharedMenuModeSwitchHost candidate = hosts[i];
                if (candidate == null || candidate.gameObject.scene != scene ||
                    (activeOnly && !candidate.isActiveAndEnabled))
                {
                    continue;
                }

                if (winner == null || candidate.GetInstanceID() < winner.GetInstanceID())
                {
                    winner = candidate;
                }
            }

            return winner;
        }

        internal static bool OwnsAutomaticInputForSceneName(string sceneName)
        {
            return CrossModeSceneSwitch.IsSupportedHostScene(sceneName) &&
                   !CrossModeSceneSwitch.IsAdventureScene(sceneName);
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
            if (IsOpen)
            {
                return;
            }

            CloseOtherOpenHosts(this);
            ChampionHudSession.CloseActiveMenu();
            WorldMapSession.CloseMap();
            SaveGameData save = ReadSave();
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(
                save,
                DetectCombat(),
                DetectUnsafe());
            Overlay = SharedMenuOverlay.Ensure(
                gameObject.scene,
                state,
                this,
                OnOverlayDisplaced);

            Overlay.BindInvoke(() => CommitFromMenu());
            Overlay.ResumeButton.onClick.RemoveAllListeners();
            Overlay.ResumeButton.onClick.AddListener(Close);
            _gameplaySuppression =
                GameInput.AcquireGameplaySuppression("shared-menu-mode-switch");
            IsOpen = true;
        }

        private static void CloseOtherOpenHosts(SharedMenuModeSwitchHost owner)
        {
            SharedMenuModeSwitchHost[] hosts =
                Resources.FindObjectsOfTypeAll<SharedMenuModeSwitchHost>();
            for (int i = 0; i < hosts.Length; i++)
            {
                SharedMenuModeSwitchHost candidate = hosts[i];
                if (candidate != null && candidate != owner && candidate.IsOpen)
                {
                    candidate.Close();
                }
            }
        }

        internal static void CloseActiveMenus()
        {
            CloseOtherOpenHosts(null);
        }

        internal static bool HasOpenMenu
        {
            get
            {
                SharedMenuModeSwitchHost[] hosts =
                    Resources.FindObjectsOfTypeAll<SharedMenuModeSwitchHost>();
                for (int i = 0; i < hosts.Length; i++)
                {
                    if (hosts[i] != null && hosts[i].IsOpen)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Close()
        {
            if (Overlay != null)
            {
                Overlay.Release(this, hide: true);
                Overlay = null;
            }

            _gameplaySuppression?.Dispose();
            _gameplaySuppression = null;

            IsOpen = false;
        }

        public CrossModeSwitchPlan Preview(string targetMode)
        {
            return CrossModeSceneSwitch.Plan(
                OwnerSceneName(),
                targetMode,
                ReadSave(),
                DetectCombat(),
                DetectUnsafe(),
                SharedMenuIds.InputSharedMenu);
        }

        public bool CommitFromMenu()
        {
            string current = OwnerSceneName();
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
                        KingdomTeachingState teaching =
                            KingdomTeachingQuestline.Evaluate(
                                ReadSave(),
                                KingdomTeachingCatalog.LoadCanonical());
                        if (teaching != null && teaching.IsComplete)
                        {
                            CrossModeSession.ArmTeachingReturn();
                        }
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

        private void OnDisable()
        {
            Close();
        }

        private void OnOverlayDisplaced()
        {
            Overlay = null;
            _gameplaySuppression?.Dispose();
            _gameplaySuppression = null;
            IsOpen = false;
        }

        private string OwnerSceneName()
        {
            Scene ownerScene = gameObject.scene;
            if (ownerScene.IsValid() && !string.IsNullOrEmpty(ownerScene.name))
            {
                return ownerScene.name;
            }

            return SharedMenuIds.AdventureScene;
        }
    }

    internal static class SharedMenuInputCoordinator
    {
        private static int _frame = -1;
        private static object _owner;

        internal static bool TryConsume(object owner, int frame)
        {
            if (_frame != frame)
            {
                _frame = frame;
                _owner = null;
            }

            if (_owner != null && !ReferenceEquals(_owner, owner))
            {
                return false;
            }

            _owner = owner;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _frame = -1;
            _owner = null;
        }
    }
}
