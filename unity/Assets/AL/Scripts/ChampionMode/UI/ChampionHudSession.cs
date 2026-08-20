using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Input;
using AL.UI.SharedMenu;
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
        private System.Func<bool> _inCombat;
        private System.Func<bool> _unsafeContext;
        private SharedMenuOverlay _overlay;
        private bool _wasTimeScalePaused;

        public SharedMenuOverlay Overlay => _overlay;

        public bool IsOpen =>
            _overlay != null && _overlay.gameObject.activeInHierarchy;

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
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(
                ResolveSave(),
                inCombat: _inCombat != null && _inCombat(),
                unsafeContext: _unsafeContext != null && _unsafeContext());
            _overlay = SharedMenuOverlay.Ensure(state);
            _overlay.ResumeButton.onClick.RemoveAllListeners();
            _overlay.ResumeButton.onClick.AddListener(CloseMenu);
            _overlay.KingdomButton.onClick.RemoveAllListeners();
            _overlay.KingdomButton.onClick.AddListener(TryEnterKingdom);
            if (!_wasTimeScalePaused)
            {
                _wasTimeScalePaused = Time.timeScale > 0f;
            }

            Time.timeScale = 0f;
            ChampionHudCameraGate.MenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseMenu()
        {
            if (_overlay != null)
            {
                _overlay.Close();
            }

            if (_wasTimeScalePaused)
            {
                Time.timeScale = 1f;
            }

            _wasTimeScalePaused = false;
            ChampionHudCameraGate.MenuOpen = false;
            if (!ChampionHudCameraGate.RecapOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void NotifyRecap(bool open)
        {
            ChampionHudCameraGate.RecapOpen = open;
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void RevealCombatChrome()
        {
            ChampionHudChrome.SetExplorationChrome(transform, false);
        }

        private void Update()
        {
            if (!GameInput.CancelPressed())
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

        private void OnDestroy()
        {
            if (ChampionHudCameraGate.MenuOpen)
            {
                Time.timeScale = 1f;
            }

            ChampionHudCameraGate.Reset();
        }

        private void TryEnterKingdom()
        {
            SaveGameData save = ResolveSave();
            ModeSwitchResult result = KingdomManagementUnlock.RequestSwitch(
                new ModeSwitchRequest(
                    SharedMenuIds.Adventure3D,
                    SharedMenuIds.Kingdom2_5D,
                    save,
                    inCombat: _inCombat != null && _inCombat(),
                    unsafeContext: _unsafeContext != null && _unsafeContext(),
                    SharedMenuIds.InputSharedMenu));
            if (!result.Succeeded)
            {
                SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(
                    save,
                    inCombat: _inCombat != null && _inCombat(),
                    unsafeContext: _unsafeContext != null && _unsafeContext());
                if (_overlay != null)
                {
                    _overlay.Build(state);
                    _overlay.ResumeButton.onClick.RemoveAllListeners();
                    _overlay.ResumeButton.onClick.AddListener(CloseMenu);
                    _overlay.KingdomButton.onClick.RemoveAllListeners();
                    _overlay.KingdomButton.onClick.AddListener(TryEnterKingdom);
                }

                return;
            }

            Time.timeScale = 1f;
            ChampionHudCameraGate.Reset();
            SceneManager.LoadScene(result.DestinationScene);
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
