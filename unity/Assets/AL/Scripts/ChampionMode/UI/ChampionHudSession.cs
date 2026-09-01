using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Input;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.ChampionMode.UI
{
    /// <summary>
    /// Pause / Shared Menu session for the 3D HUD. Escape, the Menu button,
    /// and recap Shared Menu all open the same chrome.
    /// </summary>
    public sealed class ChampionHudSession : MonoBehaviour
    {
        private static ChampionHudSession _activeMenuOwner;
        private static ChampionHudSession _inputOwner;
        private static int _lastProcessedInputFrame = int.MinValue;
        private static int _recapOwnerCount;
        private static int _ownershipGeneration;
        private System.Func<bool> _inCombat;
        private System.Func<bool> _unsafeContext;
        private SharedMenuOverlay _overlay;
        private System.IDisposable _menuTimeScaleOwnership;
        private System.IDisposable _menuCursorOwnership;
        private System.IDisposable _recapCursorOwnership;
        private int _recapOwnershipGeneration;
        private int _observedOwnershipGeneration = int.MinValue;
#if UNITY_INCLUDE_TESTS
        private bool _suppressSceneLoadForTests;
#endif

        public SharedMenuOverlay Overlay => _overlay;

        public bool IsOpen =>
            _activeMenuOwner == this &&
            _overlay != null &&
            _overlay.IsOwnedBy(this) &&
            _overlay.gameObject.activeInHierarchy;

        public static ChampionHudSession Attach(
            Transform hudRoot,
            System.Func<bool> inCombat = null,
            System.Func<bool> unsafeContext = null)
        {
            ChampionHudSession session = hudRoot.GetComponent<ChampionHudSession>();
            if (session == null)
            {
                session = hudRoot.gameObject.AddComponent<ChampionHudSession>();
            }

            session._inCombat = inCombat;
            session._unsafeContext = unsafeContext;
            session.SynchronizeOwnershipGeneration();
            if (_inputOwner == null)
            {
                _inputOwner = session;
            }
            ChampionHudChrome.ApplyScalerAndFont(
                hudRoot.GetComponent<CanvasScaler>(),
                hudRoot);
            ChampionHudChrome.RestyleExistingPlates(hudRoot);
            ChampionHudChrome.MountSharedMenuButton(hudRoot, session.OpenMenu);
            ChampionHudChrome.MountQuestSlot(hudRoot);
            ChampionHudChrome.SetExplorationChrome(
                hudRoot,
                FirstSessionChampionStart.IsFirstSessionLanding);
            ChampionHudChrome.AttachRecapSharedMenu(
                FindNamed(hudRoot, FirstSessionChampionStart.LosePanelName),
                session.OpenMenu);
            ChampionHudChrome.AttachRecapSharedMenu(
                FindNamed(hudRoot, FirstSessionChampionStart.WinPanelName),
                session.OpenMenu);
            return session;
        }

        public void OpenMenu()
        {
            SynchronizeOwnershipGeneration();
            SharedMenuModeSwitchHost.CloseActiveMenus();
            _inputOwner = this;
            if (_activeMenuOwner != null && _activeMenuOwner != this)
            {
                _activeMenuOwner.ReleaseMenuOwnership(closeOverlay: true);
            }

            WorldMapSession.CloseMap();
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(
                ResolveSave(),
                inCombat: _inCombat != null && _inCombat(),
                unsafeContext: _unsafeContext != null && _unsafeContext());
            _overlay = SharedMenuOverlay.Ensure(
                gameObject.scene,
                state,
                this,
                OnOverlayDisplaced);
            _overlay.BindWorldMap(OpenWorldMap);
            _overlay.ResumeButton.onClick.RemoveAllListeners();
            _overlay.ResumeButton.onClick.AddListener(CloseMenu);
            _overlay.KingdomButton.onClick.RemoveAllListeners();
            _overlay.KingdomButton.onClick.AddListener(TryEnterKingdom);
            _menuTimeScaleOwnership ??=
                ChampionTimeScaleGate.Acquire("shared-menu", 0f);
            ChampionHudCameraGate.MenuOpen = true;
            _menuCursorOwnership ??=
                ChampionHudCameraGate.AcquireCursorOwnership("shared-menu");
            _activeMenuOwner = this;
        }

        public void CloseMenu()
        {
            ReleaseMenuOwnership(closeOverlay: true);
        }

        public static bool CloseActiveMenu()
        {
            ChampionHudSession owner = _activeMenuOwner;
            if (owner == null)
            {
                return false;
            }

            owner.ReleaseMenuOwnership(closeOverlay: true);
            return true;
        }

        public void OpenWorldMap()
        {
            _inputOwner = this;
            CloseMenu();
            WorldMapSession.OpenMap();
        }

        public void NotifyRecap(bool open)
        {
            SynchronizeOwnershipGeneration();
            if (open)
            {
                if (_recapCursorOwnership == null)
                {
                    _recapCursorOwnership =
                        ChampionHudCameraGate.AcquireCursorOwnership("combat-recap");
                    _recapOwnershipGeneration = _ownershipGeneration;
                    _recapOwnerCount++;
                    ChampionHudCameraGate.RecapOpen = true;
                }
                return;
            }

            ReleaseRecapOwnership();
        }

        public void RevealCombatChrome()
        {
            ChampionHudChrome.SetExplorationChrome(transform, false);
        }

        private void Update()
        {
            ProcessInputFrame(
                GameInput.CancelPressed(),
                GameInput.SharedMenuPressed(),
                Time.frameCount);
        }

        private void ProcessInputFrame(
            bool cancelPressed,
            bool sharedMenuPressed,
            int frame)
        {
            Scene ownerScene = gameObject.scene;
            if (!ownerScene.IsValid() || !ownerScene.isLoaded ||
                ownerScene != SceneManager.GetActiveScene())
            {
                return;
            }

            _inputOwner = ElectInputOwner(ownerScene);

            if (_inputOwner != this || _lastProcessedInputFrame == frame)
            {
                return;
            }

            _lastProcessedInputFrame = frame;
            if (cancelPressed && WorldMapSession.IsMapOpen)
            {
                WorldMapSession.CloseMap();
                return;
            }

            if (!cancelPressed && !sharedMenuPressed)
            {
                return;
            }

            if (!SharedMenuInputCoordinator.TryConsume(this, frame))
            {
                return;
            }

            if (IsOpen)
            {
                CloseMenu();
                return;
            }

            OpenMenu();
        }

        private static ChampionHudSession ElectInputOwner(Scene activeScene)
        {
            if (_activeMenuOwner != null &&
                _activeMenuOwner.isActiveAndEnabled &&
                _activeMenuOwner.gameObject.scene == activeScene)
            {
                return _activeMenuOwner;
            }

            ChampionHudSession selected = null;
            ChampionHudSession[] sessions =
                Resources.FindObjectsOfTypeAll<ChampionHudSession>();
            for (int i = 0; i < sessions.Length; i++)
            {
                ChampionHudSession candidate = sessions[i];
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    candidate.gameObject.scene != activeScene)
                {
                    continue;
                }

                if (selected == null ||
                    candidate.GetInstanceID() < selected.GetInstanceID())
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private void OnDestroy()
        {
            ReleaseOwnedSurfaces();
        }

        private void OnDisable()
        {
            ReleaseOwnedSurfaces();
        }

        private void ReleaseOwnedSurfaces()
        {
            if (_inputOwner == this)
            {
                _inputOwner = null;
            }

            ReleaseMenuOwnership(closeOverlay: true);
            ReleaseRecapOwnership();
        }

        private void SynchronizeOwnershipGeneration()
        {
            if (_observedOwnershipGeneration == _ownershipGeneration)
            {
                return;
            }

            _menuTimeScaleOwnership = null;
            _menuCursorOwnership = null;
            _recapCursorOwnership = null;
            _recapOwnershipGeneration = _ownershipGeneration;
            _observedOwnershipGeneration = _ownershipGeneration;
        }

        private void ReleaseRecapOwnership()
        {
            if (_recapCursorOwnership == null)
            {
                return;
            }

            _recapCursorOwnership.Dispose();
            _recapCursorOwnership = null;
            if (_recapOwnershipGeneration == _ownershipGeneration)
            {
                _recapOwnerCount = Mathf.Max(0, _recapOwnerCount - 1);
            }
            ChampionHudCameraGate.RecapOpen = _recapOwnerCount > 0;
        }

        private void ReleaseMenuOwnership(bool closeOverlay)
        {
            bool isActiveOwner = _activeMenuOwner == this;
            if (closeOverlay && _overlay != null)
            {
                _overlay.Release(this, hide: true);
            }

            _menuTimeScaleOwnership?.Dispose();
            _menuTimeScaleOwnership = null;
            _menuCursorOwnership?.Dispose();
            _menuCursorOwnership = null;
            if (!isActiveOwner)
            {
                return;
            }

            _activeMenuOwner = null;
            ChampionHudCameraGate.MenuOpen = false;
        }

        private void OnOverlayDisplaced()
        {
            ReleaseMenuOwnership(closeOverlay: false);
            _overlay = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetOwnershipStatics();
        }

        internal static void ResetOwnershipStatics()
        {
            _activeMenuOwner = null;
            _inputOwner = null;
            _lastProcessedInputFrame = int.MinValue;
            _recapOwnerCount = 0;
            _ownershipGeneration = unchecked(_ownershipGeneration + 1);
        }

        private void TryEnterKingdom()
        {
            SaveGameData save = ResolveSave();
            string currentScene = gameObject.scene.name;
            if (!CrossModeSceneSwitch.IsAdventureScene(currentScene))
            {
                currentScene = SharedMenuIds.AdventureScene;
            }

            CrossModeSwitchPlan plan = CrossModeSceneSwitch.Plan(
                currentScene,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: _inCombat != null && _inCombat(),
                unsafeContext: _unsafeContext != null && _unsafeContext(),
                SharedMenuIds.InputSharedMenu);
            if (!plan.Succeeded)
            {
                SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(
                    save,
                    inCombat: _inCombat != null && _inCombat(),
                    unsafeContext: _unsafeContext != null && _unsafeContext());
                if (_overlay != null)
                {
                    _overlay.Build(state);
                    _overlay.BindWorldMap(OpenWorldMap);
                    _overlay.ResumeButton.onClick.RemoveAllListeners();
                    _overlay.ResumeButton.onClick.AddListener(CloseMenu);
                    _overlay.KingdomButton.onClick.RemoveAllListeners();
                    _overlay.KingdomButton.onClick.AddListener(TryEnterKingdom);
                }

                return;
            }

            System.Action<string, LoadSceneMode> load = SceneManager.LoadScene;
#if UNITY_INCLUDE_TESTS
            if (_suppressSceneLoadForTests)
            {
                load = (_, _) => { };
            }
#endif
            if (plan.ShouldLoad && !CrossModeSceneSwitch.TryCommit(plan, load))
            {
                return;
            }

            CloseMenu();
            NotifyRecap(false);
        }

        private static SaveGameData ResolveSave()
        {
            if (ServiceLocator.TryGet(out ISaveGameService save) && save != null)
            {
                return save.CurrentSave;
            }

            return null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Transform direct = root.Find(name);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }
    }
}
