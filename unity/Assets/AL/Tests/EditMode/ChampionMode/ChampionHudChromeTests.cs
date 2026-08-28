using IDisposable = System.IDisposable;
using System.Linq;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.UI.Presentation;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using AL.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionHudChromeTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            ChampionHudCameraGate.Reset();
            WorldMapSession.ResetStatics();
            CrossModeSession.Reset();
            ServiceLocator.Register<ISaveGameService>(null);
            Time.timeScale = 1f;
            _root = new GameObject("ChampionMode_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            ChampionHudCameraGate.Reset();
            WorldMapSession.ResetStatics();
            CrossModeSession.Reset();
            ServiceLocator.Register<ISaveGameService>(null);
            Time.timeScale = 1f;
            SharedMenuOverlay[] leftovers = Object.FindObjectsOfType<SharedMenuOverlay>();
            for (int i = 0; i < leftovers.Length; i++)
            {
                Object.DestroyImmediate(leftovers[i].gameObject);
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void FirstSessionHudMountsSharedMenuAndQuestSlotWithoutDebugKingdom()
        {
            CreateNamedPlate("PlayerFrame");
            CreateNamedPlate("CombatHotbar");
            CreateNamedPlate("BossFrame");
            CreateNamedPlate(FirstSessionChampionStart.LosePanelName);
            CreateNamedPlate(FirstSessionChampionStart.WinPanelName);

            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);

            Assert.IsNotNull(_root.transform.Find(FirstSessionChampionStart.SharedMenuButtonName));
            Assert.IsNotNull(_root.transform.Find(FirstSessionChampionStart.QuestHudSlotName));
            Assert.IsNull(_root.transform.Find(FirstSessionChampionStart.DebugKingdomButtonName));
            Assert.IsFalse(
                _root.transform.Find("BossFrame").gameObject.activeSelf,
                "Exploration must not cover the world with a boss frame.");
            Assert.IsNotNull(
                FindDeep(_root.transform, FirstSessionChampionStart.LosePanelName)
                    .Find(ChampionHudCopy.RecapSharedMenuButtonName));
            Assert.IsNotNull(
                FindDeep(_root.transform, FirstSessionChampionStart.WinPanelName)
                    .Find(ChampionHudCopy.RecapSharedMenuButtonName));
            Assert.IsFalse(session.IsOpen);
        }

        [Test]
        public void SharedMenuOpensLockedNarrativeAndBlocksCameraLook()
        {
            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);
            session.OpenMenu();

            Assert.IsTrue(session.IsOpen);
            Assert.IsTrue(ChampionHudCameraGate.ShouldIgnoreLook());
            Assert.IsFalse(session.Overlay.KingdomButton.interactable);
            Assert.AreEqual(SharedMenuCopy.Title, session.Overlay.TitleLabel.text);
            Assert.That(session.Overlay.DetailLabel.text, Does.Contain("Proof of Worth"));
            Assert.That(session.Overlay.HeaderLabel.text, Is.EqualTo(SharedMenuCopy.MenuHeader));
            Assert.That(session.Overlay.KingdomButton.name, Is.EqualTo(SharedMenuIds.KingdomButtonName));
            Assert.That(session.Overlay.WorldMapButton.name, Is.EqualTo(WorldMapIds.MenuModuleWorldMap));
            Assert.That(ChampionHudChrome.UsesPresentationFont(session.Overlay.transform), Is.True);

            session.CloseMenu();
            Assert.IsFalse(session.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void SharedMenuWorldMapEntryClosesMenuAndOpensMap()
        {
            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);
            session.OpenMenu();

            session.Overlay.WorldMapButton.onClick.Invoke();

            Assert.IsFalse(session.IsOpen);
            Assert.IsTrue(WorldMapSession.IsMapOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void FailedKingdomTransitionKeepsRebuiltWorldMapListener()
        {
            ChampionHudSession session = ChampionHudSession.Attach(
                _root.transform,
                inCombat: () => true);
            session.OpenMenu();

            session.Overlay.KingdomButton.onClick.Invoke();
            session.Overlay.WorldMapButton.onClick.Invoke();

            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(session.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void LegacySharedMenuOwnsAutomaticInputOnlyOutsideChampionArena()
        {
            MethodInfo ownsInput = typeof(SharedMenuModeSwitchHost).GetMethod(
                "OwnsAutomaticInputForSceneName",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(ownsInput, Is.Not.Null);
            Assert.That(
                ownsInput.Invoke(null, new object[] { SharedMenuIds.AdventureScene }),
                Is.EqualTo(false));
            Assert.That(
                ownsInput.Invoke(null, new object[] { SharedMenuIds.KingdomScene }),
                Is.EqualTo(true));
        }

        [Test]
        public void SuccessfulHudKingdomButtonUsesCanonicalRoundTripAuthority()
        {
            var save = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true,
                    Username = "HudCanonicalSwitch"
                }
            };
            Assert.That(
                ProofOfWorthLordship.TryWriteMark(
                    save,
                    ProofOfWorthLordship.ResolveMarkId(RealmId.Stonehold)),
                Is.True);
            System.Type controllableType = typeof(ChampionHudSession).Assembly.GetType(
                "AL.Core.ControllableSaveGameService",
                throwOnError: true);
            object controllable = System.Activator.CreateInstance(controllableType, true);
            FieldInfo currentSave = controllableType.GetField(
                "_currentSave",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(currentSave, Is.Not.Null);
            currentSave.SetValue(controllable, save);
            ServiceLocator.Register((ISaveGameService)controllable);

            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);
            FieldInfo suppressLoad = typeof(ChampionHudSession).GetField(
                "_suppressSceneLoadForTests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(suppressLoad, Is.Not.Null);
            suppressLoad.SetValue(session, true);
            session.OpenMenu();
            Assert.That(session.Overlay.KingdomButton.interactable, Is.True);

            session.Overlay.KingdomButton.onClick.Invoke();

            Assert.That(CrossModeSession.HasActiveRoundTrip, Is.True);
            Assert.That(
                CrossModeSession.AdventureScene,
                Is.EqualTo(SharedMenuIds.AdventureScene));
            Assert.That(session.IsOpen, Is.False);
        }

        [Test]
        public void PresentationTokensMatchRealmSelectAndStayTouchable()
        {
            Assert.That(PresentationChrome.MinHit, Is.GreaterThanOrEqualTo(48f));
            Assert.That(PresentationChrome.TitleSize, Is.EqualTo(26));
            Assert.That(PresentationChrome.ActionSize, Is.EqualTo(16));
            Assert.That(PresentationChrome.StoneVoid.grayscale, Is.LessThan(0.12f));

            Button menu = ChampionHudChrome.MountSharedMenuButton(_root.transform, null);
            Assert.That(menu.GetComponent<RectTransform>().sizeDelta.y, Is.GreaterThanOrEqualTo(PresentationChrome.MinHit));
            Font font = PresentationChrome.ResolveFont();
            Assert.That(font.name, Does.Not.Contain("LegacyRuntime"));
        }

        [Test]
        public void ExplorationChromeReservesTheMinimapCornerWithoutOverlap()
        {
            Button menu = ChampionHudChrome.MountSharedMenuButton(_root.transform, null);
            RectTransform quest = ChampionHudChrome.MountQuestSlot(_root.transform);

            Assert.AreEqual(
                ChampionHudChrome.MinimapSafeMenuPosition,
                menu.GetComponent<RectTransform>().anchoredPosition);
            Assert.AreEqual(
                ChampionHudChrome.MinimapSafeQuestPosition,
                quest.anchoredPosition);
            Assert.AreEqual(ChampionHudChrome.MinimapSafeQuestSize, quest.sizeDelta);
            Assert.That(quest.anchoredPosition.y, Is.LessThan(-354f));
            Assert.That(menu.GetComponent<RectTransform>().anchoredPosition.x, Is.LessThan(-360f));
        }

        [Test]
        public void RecapCopyNeverSendsThePlayerToADebugKingdomButton()
        {
            Assert.That(ChampionHudCopy.RecapNext, Does.Contain("Shared Menu"));
            Assert.That(ChampionHudCopy.RecapNext, Does.Not.Contain("Kingdom"));
            Assert.That(ChampionHudCopy.DefeatFeed, Does.Not.Contain("return to Kingdom"));
            Assert.That(ChampionHudCopy.ClearFeed, Does.Not.Contain("return to Kingdom"));
        }

        [Test]
        public void CameraGateBlocksLookWhileMenuOrRecapIsOpen()
        {
            Assert.IsFalse(ChampionHudCameraGate.BlocksLook);
            ChampionHudCameraGate.MenuOpen = true;
            Assert.IsTrue(ChampionHudCameraGate.ShouldIgnoreLook());
            ChampionHudCameraGate.MenuOpen = false;
            ChampionHudCameraGate.RecapOpen = true;
            Assert.IsTrue(ChampionHudCameraGate.ShouldIgnoreLook());
        }

        [Test]
        public void CursorModeUsesBothControlKeys()
        {
            CollectionAssert.AreEquivalent(
                new[] { "<Keyboard>/leftCtrl", "<Keyboard>/rightCtrl" },
                GameInput.CursorMode.bindings
                    .Select(binding => binding.path)
                    .ToArray());
        }

        [Test]
        public void CursorModeBlocksLookAndIndependentlySuppressesGameplay()
        {
            GameInput.SetGameplaySuppressed(false);
            Assert.IsFalse(ChampionHudCameraGate.CursorModeOpen);
            Assert.IsFalse(GameInput.GameplaySuppressed);

            ChampionHudCameraGate.SetCursorMode(true);
            Assert.IsTrue(ChampionHudCameraGate.CursorModeOpen);
            Assert.IsTrue(ChampionHudCameraGate.BlocksLook);
            Assert.IsTrue(GameInput.GameplaySuppressed);

            ChampionHudCameraGate.SetCursorMode(false);
            Assert.IsFalse(ChampionHudCameraGate.CursorModeOpen);
            Assert.IsFalse(GameInput.GameplaySuppressed);
        }

        [Test]
        public void CursorModeGateBlocksExternalChampionControl()
        {
            Assert.IsFalse(ChampionHudCameraGate.BlocksGameplay);

            ChampionHudCameraGate.SetCursorMode(true);

            Assert.IsTrue(ChampionHudCameraGate.BlocksGameplay);
        }

        [Test]
        public void NestedCursorOwnersRestoreExactPriorStateAfterLastOutOfOrderRelease()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            GameInput.SetCursorModeSuppressed(false);
            CursorLockMode priorLockState = Cursor.lockState;
            bool priorVisibility = Cursor.visible;
            IDisposable worldMapOwner =
                ChampionHudCameraGate.AcquireCursorOwnership("world-map");
            IDisposable conversationOwner =
                ChampionHudCameraGate.AcquireCursorOwnership("conversation");

            worldMapOwner.Dispose();

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            Assert.That(GameInput.CursorModeSuppressed, Is.True);

            conversationOwner.Dispose();

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(priorLockState));
            Assert.That(Cursor.visible, Is.EqualTo(priorVisibility));
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        [Test]
        public void CursorTokenFromBeforeResetCannotReleasePostResetOwner()
        {
            IDisposable stale =
                ChampionHudCameraGate.AcquireCursorOwnership("stale-owner");
            ChampionHudCameraGate.Reset();
            IDisposable current =
                ChampionHudCameraGate.AcquireCursorOwnership("current-owner");

            stale.Dispose();

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.CursorModeSuppressed, Is.True);
            current.Dispose();
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
        }

        [Test]
        public void TimeTokenFromBeforeResetCannotReleasePostResetOwner()
        {
            Time.timeScale = 0.8f;
            Time.fixedDeltaTime = 0.016f;
            IDisposable stale = ChampionTimeScaleGate.Acquire("stale-time", 0.4f);
            ChampionTimeScaleGate.Reset();
            IDisposable current = ChampionTimeScaleGate.Acquire("current-time", 0f);

            stale.Dispose();

            Assert.That(Time.timeScale, Is.EqualTo(0f));
            current.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.016f).Within(0.0001f));
        }

        [Test]
        public void LiveHudMenuReacquiresCurrentGenerationOwnershipAfterReset()
        {
            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);
            session.OpenMenu();
            ChampionHudCameraGate.Reset();
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            session.OpenMenu();

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
        }

        [Test]
        public void LiveHudRecapReacquiresCurrentGenerationOwnershipAfterReset()
        {
            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);
            session.NotifyRecap(true);
            ChampionHudCameraGate.Reset();
            Assert.That(ChampionHudCameraGate.RecapOpen, Is.False);

            session.NotifyRecap(true);

            Assert.That(ChampionHudCameraGate.RecapOpen, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
        }

        [Test]
        public void DestroyingHudSessionReleasesOnlyItsTokensAndPreservesExternalOwner()
        {
            IDisposable externalOwner =
                ChampionHudCameraGate.AcquireCursorOwnership("world-map");
            var hudRoot = new GameObject(
                "IndependentHudSession",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            ChampionHudSession session = ChampionHudSession.Attach(hudRoot.transform);
            session.OpenMenu();

            var onDestroy = typeof(ChampionHudSession).GetMethod(
                "OnDestroy",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(onDestroy, Is.Not.Null);
            onDestroy.Invoke(session, null);
            bool externalOwnerSurvived = ChampionHudCameraGate.CursorModeOpen &&
                                         ChampionHudCameraGate.BlocksGameplay &&
                                         GameInput.CursorModeSuppressed;
            externalOwner.Dispose();
            bool restoredAfterExternalRelease = !ChampionHudCameraGate.CursorModeOpen &&
                                                !GameInput.CursorModeSuppressed;
            Object.DestroyImmediate(session);
            Object.DestroyImmediate(hudRoot);

            Assert.That(externalOwnerSurvived, Is.True);
            Assert.That(restoredAfterExternalRelease, Is.True);
        }

        [Test]
        public void DestroyingHudSessionPreservesPreexistingPausedTimeScale()
        {
            Time.timeScale = 0f;
            var hudRoot = new GameObject(
                "PausedHudSession",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            ChampionHudSession session = ChampionHudSession.Attach(hudRoot.transform);
            session.OpenMenu();
            var onDestroy = typeof(ChampionHudSession).GetMethod(
                "OnDestroy",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(onDestroy, Is.Not.Null);

            onDestroy.Invoke(session, null);
            float timeScaleAfterDestroy = Time.timeScale;
            Object.DestroyImmediate(session);
            Object.DestroyImmediate(hudRoot);

            Assert.That(timeScaleAfterDestroy, Is.EqualTo(0f));
        }

        [Test]
        public void DisablingOpenHudSessionClosesOverlayAndReleasesItsOwnership()
        {
            Time.timeScale = 0.7f;
            Time.fixedDeltaTime = 0.014f;
            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);
            session.OpenMenu();
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);

            var onDisable = typeof(ChampionHudSession).GetMethod(
                "OnDisable",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(onDisable, Is.Not.Null);
            onDisable.Invoke(session, null);

            Assert.That(session.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.014f).Within(0.0001f));
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        [Test]
        public void SharedOverlayOwnershipTransfersBetweenHudSessions()
        {
            var secondRoot = new GameObject(
                "SecondHudSession",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            try
            {
                ChampionHudSession first = ChampionHudSession.Attach(_root.transform);
                ChampionHudSession second = ChampionHudSession.Attach(secondRoot.transform);
                first.OpenMenu();
                second.OpenMenu();

                first.CloseMenu();

                Assert.That(second.IsOpen, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);

                second.CloseMenu();

                Assert.That(second.IsOpen, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void OldHudCannotReopenMenuAfterOwnerClosesOnSameInputFrame()
        {
            var secondRoot = new GameObject(
                "SecondInputHudSession",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            try
            {
                ChampionHudSession first = ChampionHudSession.Attach(_root.transform);
                ChampionHudSession second = ChampionHudSession.Attach(secondRoot.transform);
                first.OpenMenu();
                second.OpenMenu();

                InvokeProcessInput(second, cancelPressed: true, sharedMenuPressed: false, frame: 71);
                InvokeProcessInput(first, cancelPressed: true, sharedMenuPressed: false, frame: 71);

                Assert.That(first.IsOpen, Is.False);
                Assert.That(second.IsOpen, Is.False);
                Assert.That(ChampionHudCameraGate.MenuOpen, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void RecapOwnershipComposesAcrossHudSessions()
        {
            var secondRoot = new GameObject(
                "SecondRecapHudSession",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            try
            {
                ChampionHudSession first = ChampionHudSession.Attach(_root.transform);
                ChampionHudSession second = ChampionHudSession.Attach(secondRoot.transform);
                first.NotifyRecap(true);
                second.NotifyRecap(true);

                first.NotifyRecap(false);

                Assert.That(ChampionHudCameraGate.RecapOpen, Is.True);
                Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);

                second.NotifyRecap(false);

                Assert.That(ChampionHudCameraGate.RecapOpen, Is.False);
                Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void FreshSaveCannotSwitchThroughAnythingButSharedMenu()
        {
            var save = new SaveGameData();
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            Assert.AreEqual(SharedMenuAvailability.LockedNarrative, state.Availability);
            Assert.IsFalse(
                KingdomManagementUnlock.RequestSwitch(
                    new ModeSwitchRequest(
                        SharedMenuIds.Adventure3D,
                        SharedMenuIds.Kingdom2_5D,
                        save,
                        false,
                        false,
                        SharedMenuIds.InputBoot)).Succeeded);
        }

        private void CreateNamedPlate(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root.transform, false);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        private static void InvokeProcessInput(
            ChampionHudSession session,
            bool cancelPressed,
            bool sharedMenuPressed,
            int frame)
        {
            var method = typeof(ChampionHudSession).GetMethod(
                "ProcessInputFrame",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(session, new object[] { cancelPressed, sharedMenuPressed, frame });
        }
    }
}
