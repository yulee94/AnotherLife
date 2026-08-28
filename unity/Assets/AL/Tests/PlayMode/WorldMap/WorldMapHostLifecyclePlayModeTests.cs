using System.Collections;
using System.Reflection;
using AL.ChampionMode.Camera;
using AL.ChampionMode.Quests;
using AL.ChampionMode.UI;

using AL.Input;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode.WorldMap
{
    public sealed class WorldMapHostLifecyclePlayModeTests
    {
        private Scene _originalScene;
        private Scene _championScene;
        private Scene _innerRealmScene;
        private GameObject _conversationRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalScene = SceneManager.GetActiveScene();
            InvokeHostStatic("ResetStatics");
            WorldMapSession.ResetStatics();
            ChampionHudCameraGate.Reset();
            GameInput.SetGameplaySuppressed(false);
            InvokeHostStatic("AfterSceneLoad");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            WorldMapSession.CloseAll();
            ChampionHudCameraGate.Reset();
            GameInput.SetGameplaySuppressed(false);
            if (_conversationRoot != null)
            {
                Object.Destroy(_conversationRoot);
                _conversationRoot = null;
                yield return null;
            }

            if (_originalScene.IsValid() && _originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(_originalScene);
            }

            yield return UnloadIfNeeded(_innerRealmScene);
            yield return UnloadIfNeeded(_championScene);
            InvokeHostStatic("ResetStatics");
        }

        [UnityTest]
        public IEnumerator AdditiveSupportedScenesUseLocalHostsStableElectionAndIgnoreDisabledCandidates()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            var disabledRoot = new GameObject("DisabledChampionWorldMapHost");
            SceneManager.MoveGameObjectToScene(disabledRoot, _championScene);
            WorldMapHost disabledCandidate = disabledRoot.AddComponent<WorldMapHost>();
            disabledCandidate.enabled = false;

            WorldMapHost championHost = WorldMapHost.EnsureForScene(_championScene);
            Assert.That(championHost, Is.Not.Null);
            Assert.That(championHost, Is.Not.SameAs(disabledCandidate));
            Assert.That(championHost.isActiveAndEnabled, Is.True);
            Assert.That(championHost.gameObject.scene, Is.EqualTo(_championScene));
            AssertLocalSurfaces(_championScene);

            _innerRealmScene = SceneManager.CreateScene(WorldMapIds.InnerRealmWorldScene);
            WorldMapHost innerHost = WorldMapHost.EnsureForScene(_innerRealmScene);
            Assert.That(innerHost, Is.Not.Null);
            Assert.That(innerHost.gameObject.scene, Is.EqualTo(_innerRealmScene));
            AssertLocalSurfaces(_innerRealmScene);

            WorldMapHost elected = ReadAuthority();
            WorldMapHost expected = CompareAuthority(championHost, innerHost) > 0
                ? championHost
                : innerHost;
            Assert.That(elected, Is.SameAs(expected));

            WorldMapHost nonAuthority = elected == championHost ? innerHost : championHost;
            InvokeHostInstance(nonAuthority, "OnEnable");
            Assert.That(ReadAuthority(), Is.SameAs(elected),
                "Re-registration must not override the stable authority comparator.");
            Assert.That(WorldMapHost.EnsureForScene(_championScene), Is.SameAs(championHost));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostStaticResetDeauthorizesLiveOverlayBeforeReelection()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost.EnsureForScene(_championScene);
            WorldMapOverlay overlay = FindInScene<WorldMapOverlay>(_championScene);
            GameObject veil = FindDescendant(overlay.transform, "WorldMap_Veil");
            WorldMapSession.OpenMap();
            yield return null;
            Assert.That(veil.activeInHierarchy, Is.True);

            InvokeHostStatic("ResetStatics");

            Assert.That(veil.activeInHierarchy, Is.False);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.GameplaySuppressed, Is.False);

            WorldMapHost.EnsureForScene(_championScene);
            yield return null;
            Assert.That(veil.activeInHierarchy, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.GameplaySuppressed, Is.True);
        }

        [UnityTest]
        public IEnumerator AdditiveAuthorityTransferKeepsExactlyOneVeilAndOwnerAcrossDisableDestroyReenableAndUnload()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost championHost = WorldMapHost.EnsureForScene(_championScene);
            _innerRealmScene = SceneManager.CreateScene(WorldMapIds.InnerRealmWorldScene);
            WorldMapHost innerHost = WorldMapHost.EnsureForScene(_innerRealmScene);
            WorldMapHost firstAuthority = ReadAuthority();
            WorldMapHost otherHost = firstAuthority == championHost ? innerHost : championHost;
            Scene firstAuthorityScene = firstAuthority.gameObject.scene;

            WorldMapSession.OpenMap();
            yield return null;
            AssertExactlyOnePresentedOverlay(firstAuthorityScene);

            firstAuthority.enabled = false;
            yield return null;

            Assert.That(ReadAuthority(), Is.SameAs(otherHost));
            AssertExactlyOnePresentedOverlay(otherHost.gameObject.scene);

            firstAuthority.enabled = true;
            yield return null;

            Assert.That(ReadAuthority(), Is.SameAs(firstAuthority));
            AssertExactlyOnePresentedOverlay(firstAuthorityScene);

            Object.Destroy(firstAuthority.gameObject);
            yield return null;

            Assert.That(ReadAuthority(), Is.SameAs(otherHost));
            Assert.That(FindInScene<WorldMapOverlay>(firstAuthorityScene), Is.Null,
                "Destroying the final local host must remove its orphan overlay.");
            AssertExactlyOnePresentedOverlay(otherHost.gameObject.scene);

            WorldMapSession.CloseMap();
            WorldMapSession.OpenMap();
            yield return null;

            Assert.That(FindInScene<WorldMapOverlay>(firstAuthorityScene), Is.Null,
                "A destroyed host's surface must not reacquire on a later session broadcast.");
            AssertExactlyOnePresentedOverlay(otherHost.gameObject.scene);

            SceneManager.SetActiveScene(_originalScene);
            AsyncOperation firstUnload = SceneManager.UnloadSceneAsync(firstAuthorityScene);
            while (firstUnload != null && !firstUnload.isDone)
            {
                yield return null;
            }

            if (firstAuthorityScene == _innerRealmScene)
            {
                _innerRealmScene = default;
            }
            else
            {
                _championScene = default;
            }

            AssertExactlyOnePresentedOverlay(otherHost.gameObject.scene);

            Scene remainingScene = otherHost.gameObject.scene;
            AsyncOperation lastUnload = SceneManager.UnloadSceneAsync(remainingScene);
            while (lastUnload != null && !lastUnload.isDone)
            {
                yield return null;
            }

            if (remainingScene == _innerRealmScene)
            {
                _innerRealmScene = default;
            }
            else
            {
                _championScene = default;
            }
            yield return null;

            Assert.That(WorldMapSession.IsMapOpen, Is.False);
            Assert.That(CountPresentedOverlays(), Is.Zero);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.GameplaySuppressed, Is.False);
        }

        [UnityTest]
        public IEnumerator UnloadingAuthoritativeAdditiveSceneFailsOverWithoutClosingMap()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost championHost = WorldMapHost.EnsureForScene(_championScene);
            _innerRealmScene = SceneManager.CreateScene(WorldMapIds.InnerRealmWorldScene);
            WorldMapHost innerHost = WorldMapHost.EnsureForScene(_innerRealmScene);
            WorldMapHost authority = ReadAuthority();
            WorldMapHost remaining = authority == championHost ? innerHost : championHost;
            Scene authorityScene = authority.gameObject.scene;

            WorldMapSession.OpenMap();
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.GameplaySuppressed, Is.True);

            SceneManager.SetActiveScene(_originalScene);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(authorityScene);
            Assert.That(unload, Is.Not.Null);
            while (!unload.isDone)
            {
                yield return null;
            }

            if (authorityScene == _innerRealmScene)
            {
                _innerRealmScene = default;
            }
            else
            {
                _championScene = default;
            }
            yield return null;

            Assert.That(ReadAuthority(), Is.SameAs(remaining));
            Assert.That(remaining.isActiveAndEnabled, Is.True);
            AssertLocalSurfaces(remaining.gameObject.scene);
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.GameplaySuppressed, Is.True);
        }

        [UnityTest]
        public IEnumerator ExistingUnbuiltOverlayIsBuiltBeforeItCanOwnSuppression()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            var unbuiltRoot = new GameObject(WorldMapIds.OverlayRootName);
            SceneManager.MoveGameObjectToScene(unbuiltRoot, _championScene);
            unbuiltRoot.AddComponent<WorldMapOverlay>();

            WorldMapHost.EnsureForScene(_championScene);
            WorldMapSession.OpenMap();
            yield return null;

            WorldMapOverlay overlay = FindInScene<WorldMapOverlay>(_championScene);
            Assert.That(FindDescendant(overlay.transform, "WorldMap_Canvas"), Is.Not.Null);
            Assert.That(FindDescendant(overlay.transform, "WorldMap_Veil"), Is.Not.Null);
            AssertExactlyOnePresentedOverlay(_championScene);
        }

        [UnityTest]
        public IEnumerator InactiveOverlayAndMinimapRootsAreReactivatedBeforeSuppressionReturns()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost.EnsureForScene(_championScene);
            WorldMapOverlay overlay = FindInScene<WorldMapOverlay>(_championScene);
            InnerRealmMinimapOverlay minimap =
                FindInScene<InnerRealmMinimapOverlay>(_championScene);
            WorldMapSession.OpenMap();
            yield return null;

            overlay.gameObject.SetActive(false);
            minimap.gameObject.SetActive(false);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.GameplaySuppressed, Is.False);

            yield return null;
            yield return null;

            Assert.That(overlay.gameObject.activeInHierarchy, Is.True);
            Assert.That(minimap.gameObject.activeInHierarchy, Is.True);
            AssertExactlyOnePresentedOverlay(_championScene);
        }

        [UnityTest]
        public IEnumerator InactiveOpenVeilIsReactivatedInsteadOfLeavingInvisibleSuppression()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost.EnsureForScene(_championScene);
            WorldMapOverlay overlay = FindInScene<WorldMapOverlay>(_championScene);
            GameObject veil = FindDescendant(overlay.transform, "WorldMap_Veil");
            WorldMapSession.OpenMap();
            yield return null;

            veil.SetActive(false);
            yield return null;
            yield return null;

            Assert.That(veil.activeInHierarchy, Is.True,
                "An inactive open veil must be repaired before suppression continues.");
            AssertExactlyOnePresentedOverlay(_championScene);
        }

        [UnityTest]
        public IEnumerator MissingRequiredMapAndMinimapChildrenAreRebuiltBeforeSuppressionContinues()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost.EnsureForScene(_championScene);
            WorldMapOverlay overlay = FindInScene<WorldMapOverlay>(_championScene);
            InnerRealmMinimapOverlay minimap =
                FindInScene<InnerRealmMinimapOverlay>(_championScene);
            WorldMapSession.OpenMap();
            yield return null;

            Object.Destroy(FindDescendant(overlay.transform, "WorldMap_Canvas"));
            Object.Destroy(FindDescendant(minimap.transform, "InnerRealmMinimapCanvas"));
            yield return null;
            yield return null;

            GameObject canvas = FindDescendant(overlay.transform, "WorldMap_Canvas");
            GameObject veil = FindDescendant(overlay.transform, "WorldMap_Veil");
            GameObject minimapCanvas =
                FindDescendant(minimap.transform, "InnerRealmMinimapCanvas");
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.activeInHierarchy, Is.True);
            Assert.That(canvas.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(veil, Is.Not.Null);
            Assert.That(veil.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(minimapCanvas, Is.Not.Null);
            Assert.That(minimapCanvas.activeInHierarchy, Is.True);
            Assert.That(minimapCanvas.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(
                minimapCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(),
                Is.Not.Null);
            AssertExactlyOnePresentedOverlay(_championScene);
        }

        [UnityTest]
        public IEnumerator PublicOpenMapClosesLegacySharedMenu()
        {
            _innerRealmScene = SceneManager.CreateScene(WorldMapIds.InnerRealmWorldScene);
            WorldMapHost.EnsureForScene(_innerRealmScene);
            SharedMenuModeSwitchHost legacy =
                SharedMenuModeSwitchHost.EnsureForScene(_innerRealmScene);
            legacy.Open();
            SharedMenuOverlay legacyOverlay = legacy.Overlay;

            WorldMapSession.OpenMap();
            yield return null;

            Assert.That(legacy.IsOpen, Is.False);
            Assert.That(legacyOverlay.gameObject.activeInHierarchy, Is.False);
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator PublicToggleMapFromClosedClosesChampionHudMenu()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost.EnsureForScene(_championScene);
            var hudRoot = new GameObject(
                "PublicToggleWorldMapHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            SceneManager.MoveGameObjectToScene(hudRoot, _championScene);
            ChampionHudSession hud = ChampionHudSession.Attach(hudRoot.transform);
            hud.OpenMenu();
            SharedMenuOverlay hudOverlay = hud.Overlay;

            WorldMapSession.ToggleMap();
            yield return null;

            Assert.That(hud.IsOpen, Is.False);
            Assert.That(hudOverlay.gameObject.activeInHierarchy, Is.False);
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Object.Destroy(hudRoot);
        }

        [UnityTest]
        public IEnumerator WorldMapInputClosesLegacySharedMenuAcrossAdditiveScenes()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost.EnsureForScene(_championScene);
            _innerRealmScene = SceneManager.CreateScene(WorldMapIds.InnerRealmWorldScene);
            WorldMapHost.EnsureForScene(_innerRealmScene);
            SharedMenuModeSwitchHost legacy =
                SharedMenuModeSwitchHost.EnsureForScene(_innerRealmScene);
            Assert.That(legacy, Is.Not.Null);
            legacy.Open();
            SharedMenuOverlay legacyOverlay = legacy.Overlay;
            Assert.That(legacy.IsOpen, Is.True);
            Assert.That(legacyOverlay.gameObject.activeInHierarchy, Is.True);

            WorldMapHost authority = ReadAuthority();
            MethodInfo process = typeof(WorldMapHost).GetMethod(
                "ProcessWorldMapInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(authority, Is.Not.Null);
            Assert.That(process, Is.Not.Null);
            process.Invoke(authority, new object[] { true });
            yield return null;

            Assert.That(legacy.IsOpen, Is.False);
            Assert.That(legacyOverlay.gameObject.activeInHierarchy, Is.False);
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator WorldMapInputClosesAuthoritativeMenuOwnerAcrossMultipleHuds()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost host = WorldMapHost.EnsureForScene(_championScene);
            var firstHudRoot = new GameObject(
                "FirstWorldMapRoutingHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            var secondHudRoot = new GameObject(
                "SecondWorldMapRoutingHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            SceneManager.MoveGameObjectToScene(firstHudRoot, _championScene);
            SceneManager.MoveGameObjectToScene(secondHudRoot, _championScene);
            ChampionHudSession first = ChampionHudSession.Attach(firstHudRoot.transform);
            ChampionHudSession second = ChampionHudSession.Attach(secondHudRoot.transform);
            first.OpenMenu();
            second.OpenMenu();
            Assert.That(first.IsOpen, Is.False);
            Assert.That(second.IsOpen, Is.True);

            MethodInfo process = typeof(WorldMapHost).GetMethod(
                "ProcessWorldMapInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null,
                "The M-key path needs a deterministic routing seam.");
            process.Invoke(host, new object[] { true });
            yield return null;

            Assert.That(first.IsOpen, Is.False);
            Assert.That(second.IsOpen, Is.False);
            Assert.That(WorldMapSession.IsMapOpen, Is.True,
                "The authoritative menu and world map may never remain open together.");
            AssertExactlyOnePresentedOverlay(_championScene);

            Object.Destroy(firstHudRoot);
            Object.Destroy(secondHudRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingOpenOverlayHidesItAndHostRepairsItOnNextUpdate()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost.EnsureForScene(_championScene);
            WorldMapOverlay overlay = FindInScene<WorldMapOverlay>(_championScene);
            GameObject veil = FindDescendant(overlay.transform, "WorldMap_Veil");
            WorldMapSession.OpenMap();
            Assert.That(veil.activeInHierarchy, Is.True);

            overlay.enabled = false;

            Assert.That(veil.activeInHierarchy, Is.False);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.GameplaySuppressed, Is.False);

            yield return null;

            Assert.That(overlay.isActiveAndEnabled, Is.True);
            Assert.That(veil.activeInHierarchy, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.GameplaySuppressed, Is.True);
        }

        [UnityTest]
        public IEnumerator WorldMapPausesConversationAutoCompletion()
        {
            _conversationRoot = new GameObject("WorldMapPausedConversationRoot");
            var cameraRoot = new GameObject("WorldMapPausedConversationCamera");
            cameraRoot.transform.SetParent(_conversationRoot.transform, false);
            var camera = cameraRoot.AddComponent<UnityEngine.Camera>();
            int completions = 0;
            NpcConversationView view = NpcConversationView.Mount(_conversationRoot.transform);
            view.Show(
                "DIALOGUE_WORLD_MAP_PAUSE",
                "Captain Valerius",
                "Hold the line.",
                null,
                null,
                camera,
                () => completions++,
                autoAdvanceSeconds: 0.1f);

            WorldMapSession.OpenMap();
            Assert.That(view.SkipCurrentLine(), Is.False);
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(completions, Is.Zero);
            Assert.That(view.Session.IsCompleted, Is.False);

            WorldMapSession.CloseMap();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisablingActiveConversationHidesThenReenablesOnlyDurablePresentation()
        {
            _conversationRoot = new GameObject("NpcConversationLifecycleTestRoot");
            var cameraRoot = new GameObject("NpcConversationLifecycleCamera");
            cameraRoot.transform.SetParent(_conversationRoot.transform, false);
            var camera = cameraRoot.AddComponent<UnityEngine.Camera>();
            CameraFollow follow = cameraRoot.AddComponent<CameraFollow>();
            NpcConversationView view = NpcConversationView.Mount(_conversationRoot.transform);
            view.Show(
                "DIALOGUE_LIFECYCLE",
                "Captain Valerius",
                "Hold the line.",
                null,
                null,
                camera,
                null);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(ReadSuspensionCount(follow), Is.EqualTo(1));

            view.enabled = false;

            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.Session.IsCompleted, Is.False);
            Assert.That(view.Session.IsCollapsed, Is.False);
            Assert.That(ReadSuspensionCount(follow), Is.Zero);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);

            view.enabled = true;

            Assert.That(view.IsVisible, Is.True);
            Assert.That(ReadSuspensionCount(follow), Is.EqualTo(1));
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);

            view.Collapse();
            view.enabled = false;
            view.enabled = true;
            Assert.That(view.IsVisible, Is.False);
            Assert.That(ReadSuspensionCount(follow), Is.Zero);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            yield return null;
        }

        private static void AssertExactlyOnePresentedOverlay(Scene expectedScene)
        {
            Assert.That(CountPresentedOverlays(), Is.EqualTo(1),
                "Exactly one world-map veil may be visible and own suppression.");
            WorldMapOverlay expected = FindInScene<WorldMapOverlay>(expectedScene);
            Assert.That(expected, Is.Not.Null);
            GameObject veil = FindDescendant(expected.transform, "WorldMap_Veil");
            Assert.That(veil, Is.Not.Null);
            Assert.That(veil.activeInHierarchy, Is.True);
            Assert.That(ReadOverlayOwnership(expected, "_cursorOwnership"), Is.Not.Null);
            Assert.That(ReadOverlayOwnership(expected, "_gameplaySuppressionOwnership"), Is.Not.Null);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.GameplaySuppressed, Is.True);
        }

        [UnityTest]
        public IEnumerator FailedAuthoritySurfaceRebindImmediatelyElectsHealthyPeer()
        {
            _championScene = SceneManager.CreateScene(WorldMapIds.ChampionArenaScene);
            WorldMapHost championHost = WorldMapHost.EnsureForScene(_championScene);
            _innerRealmScene = SceneManager.CreateScene(WorldMapIds.InnerRealmWorldScene);
            WorldMapHost innerHost = WorldMapHost.EnsureForScene(_innerRealmScene);
            WorldMapHost failedAuthority = ReadAuthority();
            WorldMapHost healthyPeer = failedAuthority == championHost ? innerHost : championHost;
            WorldMapOverlay failedOverlay =
                FindInScene<WorldMapOverlay>(failedAuthority.gameObject.scene);
            GameObject canvas = FindDescendant(failedOverlay.transform, "WorldMap_Canvas");

            WorldMapSession.OpenMap();
            SetSnapshotLoadFailure(true);
            Object.Destroy(canvas);
            yield return null;
            InvokeHostInstance(failedAuthority, "BindIfNeeded");
            yield return null;

            WorldMapHost actualAuthority = ReadAuthority();
            Assert.That(actualAuthority, Is.SameAs(healthyPeer),
                $"Expected peer {healthyPeer.GetInstanceID()} in scene " +
                $"{healthyPeer.gameObject.scene.handle}, got " +
                $"{(actualAuthority != null ? actualAuthority.GetInstanceID() : 0)}; " +
                $"failed host was {failedAuthority.GetInstanceID()}.");
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            AssertExactlyOnePresentedOverlay(healthyPeer.gameObject.scene);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.GameplaySuppressed, Is.True);
        }

        private static void SetSnapshotLoadFailure(bool value)
        {
            FieldInfo field = typeof(WorldMapHost).GetField(
                "ForceSnapshotLoadFailureForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, value);
        }

        private static int CountPresentedOverlays()
        {
            int count = 0;
            WorldMapOverlay[] overlays = Resources.FindObjectsOfTypeAll<WorldMapOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                WorldMapOverlay overlay = overlays[i];
                if (overlay == null || !overlay.gameObject.scene.IsValid())
                {
                    continue;
                }

                GameObject veil = FindDescendant(overlay.transform, "WorldMap_Veil");
                bool ownsCursor = ReadOverlayOwnership(overlay, "_cursorOwnership") != null;
                bool ownsSuppression =
                    ReadOverlayOwnership(overlay, "_gameplaySuppressionOwnership") != null;
                if (veil != null && veil.activeInHierarchy && ownsCursor && ownsSuppression)
                {
                    count++;
                }
            }

            return count;
        }

        private static object ReadOverlayOwnership(WorldMapOverlay overlay, string fieldName)
        {
            FieldInfo field = typeof(WorldMapOverlay).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(overlay);
        }

        private static void AssertLocalSurfaces(Scene scene)
        {
            WorldMapOverlay overlay = FindInScene<WorldMapOverlay>(scene);
            InnerRealmMinimapOverlay minimap = FindInScene<InnerRealmMinimapOverlay>(scene);
            Assert.That(overlay, Is.Not.Null, "Missing WorldMapOverlay in " + scene.name);
            Assert.That(minimap, Is.Not.Null, "Missing InnerRealmMinimapOverlay in " + scene.name);
            Assert.That(overlay.isActiveAndEnabled, Is.True);
            Assert.That(minimap.isActiveAndEnabled, Is.True);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static WorldMapHost ReadAuthority()
        {
            FieldInfo authority = typeof(WorldMapHost).GetField(
                "_authoritativeHost",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(authority, Is.Not.Null);
            return (WorldMapHost)authority.GetValue(null);
        }

        private static int CompareAuthority(WorldMapHost left, WorldMapHost right)
        {
            MethodInfo compare = typeof(WorldMapHost).GetMethod(
                "CompareAuthority",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(compare, Is.Not.Null);
            return (int)compare.Invoke(null, new object[] { left, right });
        }

        private static int ReadSuspensionCount(CameraFollow follow)
        {
            FieldInfo count = typeof(CameraFollow).GetField(
                "_presentationSuspensionCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(count, Is.Not.Null);
            return (int)count.GetValue(follow);
        }

        private static GameObject FindDescendant(Transform root, string name)
        {
            if (root.name == name)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindDescendant(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void InvokeHostStatic(string methodName)
        {
            MethodInfo method = typeof(WorldMapHost).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, null);
        }

        private static void InvokeHostInstance(WorldMapHost host, string methodName)
        {
            MethodInfo method = typeof(WorldMapHost).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(host, null);
        }

        private static IEnumerator UnloadIfNeeded(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }
        }
    }
}
