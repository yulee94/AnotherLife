using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Skills;
using AL.ChampionMode.Tutorial;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.Input;
using AL.Services.Local;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AL.Tests.PlayMode.SharedMenu
{
    public sealed class SharedMenuModeSwitchHostLifecyclePlayModeTests
    {
        private Scene _originalScene;
        private Scene _championScene;
        private Scene _kingdomScene;
        private GameObject _staleRoot;
        private readonly List<string> _ownedSaveRoots = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalScene = SceneManager.GetActiveScene();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ChampionHudCameraGate.Reset();
            Time.timeScale = 1f;
            if (_originalScene.IsValid() && _originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(_originalScene);
            }

            if (_staleRoot != null)
            {
                Object.Destroy(_staleRoot);
                _staleRoot = null;
                yield return null;
            }

            yield return UnloadIfNeeded(_kingdomScene);
            yield return UnloadIfNeeded(_championScene);

            for (int index = 0; index < _ownedSaveRoots.Count; index++)
            {
                string root = _ownedSaveRoots[index];
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            _ownedSaveRoots.Clear();
        }

        [UnityTest]
        public IEnumerator DisabledControllerRejectsAcceptedTutorialAttackEvidence()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            FirstWorldEntryTutorialDirector.ResetForTests();
            var champion = new GameObject("DisabledTutorialAttackOwner");
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            var resolver = new CountingAttackResolver();
            Assert.That(controller.TryBindEditorBasicAttackResolver(resolver), Is.True);
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    null,
                    CreateTutorialSave(RealmId.Crownlands),
                    controller);
            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                blockHeld: false);
            director.ApplyInteractForTests();
            Assert.That(director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.BasicAttack));

            controller.enabled = false;
            Assert.That(controller.BlocksGameplayEntry, Is.True);
            Assert.That(controller.RequestBasicAttack(), Is.False);
            yield return null;

            Assert.That(resolver.CallCount, Is.Zero);
            Assert.That(director.State.IsComplete, Is.False);
            Object.Destroy(director.gameObject);
            Object.Destroy(champion);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledTutorialDirectorRejectsAcceptedOwnerAttackEvidence()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            FirstWorldEntryTutorialDirector.ResetForTests();
            var champion = new GameObject("DisabledTutorialDirectorOwner");
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            var resolver = new CountingAttackResolver();
            Assert.That(controller.TryBindEditorBasicAttackResolver(resolver), Is.True);
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    null,
                    CreateTutorialSave(RealmId.Crownlands),
                    controller);
            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                blockHeld: false);
            director.ApplyInteractForTests();
            Assert.That(director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.BasicAttack));

            director.enabled = false;
            int audioRootsBefore = CountRuntimeCombatAudioRoots();
            Assert.That(controller.RequestBasicAttack(), Is.True);
            yield return null;

            Assert.That(CountRuntimeCombatAudioRoots(), Is.EqualTo(audioRootsBefore));
            Assert.That(director.State.IsComplete, Is.False);
            Object.Destroy(director.gameObject);
            Object.Destroy(champion);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TutorialBasicAttackCompletesOnlyFromAcceptedOwnerRequest()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            FirstWorldEntryTutorialDirector.ResetForTests();
            var champion = new GameObject("TutorialAcceptedAttackOwner");
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            var resolver = new CountingAttackResolver();
            Assert.That(controller.TryBindEditorBasicAttackResolver(resolver), Is.True);
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    null,
                    CreateTutorialSave(RealmId.Crownlands),
                    controller);
            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                blockHeld: false);
            director.ApplyInteractForTests();
            Assert.That(director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.BasicAttack));

            controller.SetControlLocked(true);
            Assert.That(controller.RequestBasicAttack(), Is.False);
            Assert.That(director.State.IsComplete, Is.False);

            controller.SetControlLocked(false);
            Assert.That(controller.RequestBasicAttack(), Is.True);
            yield return null;
            Assert.That(director.State.IsComplete, Is.True);

            Object.Destroy(director.gameObject);
            Object.Destroy(champion);
            FirstWorldEntryTutorialDirector.ResetForTests();
        }

        [UnityTest]
        public IEnumerator DuplicateLegacyHostsElectOneInputOwnerAndFailOverOnDestroy()
        {
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            var firstRoot = new GameObject("DuplicateLegacyHostFirst");
            var secondRoot = new GameObject("DuplicateLegacyHostSecond");
            SceneManager.MoveGameObjectToScene(firstRoot, _kingdomScene);
            SceneManager.MoveGameObjectToScene(secondRoot, _kingdomScene);
            SharedMenuModeSwitchHost first =
                firstRoot.AddComponent<SharedMenuModeSwitchHost>();
            SharedMenuModeSwitchHost second =
                secondRoot.AddComponent<SharedMenuModeSwitchHost>();
            SceneManager.SetActiveScene(_kingdomScene);

            bool firstOwns = ReadOwnsAutomaticInput(first);
            bool secondOwns = ReadOwnsAutomaticInput(second);
            Assert.That((firstOwns ? 1 : 0) + (secondOwns ? 1 : 0), Is.EqualTo(1));

            SharedMenuModeSwitchHost owner = firstOwns ? first : second;
            SharedMenuModeSwitchHost survivor = firstOwns ? second : first;
            Object.Destroy(owner.gameObject);
            yield return null;

            Assert.That(ReadOwnsAutomaticInput(survivor), Is.True);
            Assert.That(
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene),
                Is.SameAs(survivor));
        }

        [UnityTest]
        public IEnumerator EnsureForSceneIgnoresStaleHostsAndPreservesExactAdditiveOwnersAcrossTransitions()
        {
            _staleRoot = new GameObject("UnrelatedStaleSharedMenuHost");
            SharedMenuModeSwitchHost stale =
                _staleRoot.AddComponent<SharedMenuModeSwitchHost>();

            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            SharedMenuModeSwitchHost championHost =
                SharedMenuModeSwitchHost.EnsureForScene(_championScene);
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            SharedMenuModeSwitchHost kingdomHost =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);

            Assert.That(championHost, Is.Not.Null);
            Assert.That(kingdomHost, Is.Not.Null);
            Assert.That(championHost, Is.Not.SameAs(stale));
            Assert.That(kingdomHost, Is.Not.SameAs(stale));
            Assert.That(kingdomHost, Is.Not.SameAs(championHost));
            Assert.That(championHost.gameObject.scene, Is.EqualTo(_championScene));
            Assert.That(kingdomHost.gameObject.scene, Is.EqualTo(_kingdomScene));
            Assert.That(
                SharedMenuModeSwitchHost.EnsureForScene(_championScene),
                Is.SameAs(championHost));
            Assert.That(
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene),
                Is.SameAs(kingdomHost));

            SceneManager.SetActiveScene(_championScene);
            Assert.That(ReadOwnsAutomaticInput(championHost), Is.False,
                "Champion HUD owns adventure input in the host's own scene.");
            Assert.That(ReadOwnsAutomaticInput(kingdomHost), Is.False,
                "An inactive additive scene must not compete for the same input frame.");

            SceneManager.SetActiveScene(_originalScene);
            yield return UnloadIfNeeded(_championScene);
            _championScene = default;

            Assert.That(kingdomHost, Is.Not.Null);
            Assert.That(kingdomHost.gameObject.scene, Is.EqualTo(_kingdomScene));
            Assert.That(
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene),
                Is.SameAs(kingdomHost));
            SceneManager.SetActiveScene(_kingdomScene);
            Assert.That(ReadOwnsAutomaticInput(kingdomHost), Is.True);

            yield return UnloadIfNeeded(_kingdomScene);
            _kingdomScene = default;
            Assert.That(kingdomHost == null, Is.True);
        }

        [UnityTest]
        public IEnumerator LegacySharedMenuPausesConversationAutoCompletion()
        {
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            SharedMenuModeSwitchHost host =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);
            var conversationRoot = new GameObject("SharedMenuPausedConversationRoot");
            int completions = 0;
            NpcConversationView view = NpcConversationView.Mount(conversationRoot.transform);
            view.Show(
                "DIALOGUE_SHARED_MENU_PAUSE",
                "Captain Valerius",
                "Hold the line.",
                null,
                null,
                null,
                () => completions++,
                autoAdvanceSeconds: 0.1f);

            host.Open();
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(completions, Is.Zero);
            Assert.That(view.Session.IsCompleted, Is.False);

            host.Close();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(completions, Is.EqualTo(1));
            Object.Destroy(conversationRoot);
        }

        [UnityTest]
        public IEnumerator LegacySharedMenuOwnsGameplaySuppressionUntilClosed()
        {
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            SharedMenuModeSwitchHost host =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);
            Assert.That(GameInput.GameplaySuppressed, Is.False);

            host.Open();

            Assert.That(host.IsOpen, Is.True);
            Assert.That(GameInput.GameplaySuppressed, Is.True);
            Assert.That(QuestHudAutoQuest.CanDriveInCurrentContext(), Is.False);

            host.Close();

            Assert.That(GameInput.GameplaySuppressed, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AdditiveHostsOwnDistinctSceneLocalSurfacesAndRouteFromOwnerScene()
        {
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            SharedMenuModeSwitchHost championHost =
                SharedMenuModeSwitchHost.EnsureForScene(_championScene);
            SharedMenuModeSwitchHost kingdomHost =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);

            SceneManager.SetActiveScene(_championScene);
            kingdomHost.Open();
            Assert.That(kingdomHost.Overlay.gameObject.scene, Is.EqualTo(_kingdomScene));
            Assert.That(
                kingdomHost.Preview(SharedMenuIds.Adventure3D).FromMode,
                Is.EqualTo(SharedMenuIds.Kingdom2_5D));

            SceneManager.SetActiveScene(_kingdomScene);
            championHost.Open();
            Assert.That(championHost.Overlay.gameObject.scene, Is.EqualTo(_championScene));
            Assert.That(championHost.Overlay, Is.Not.SameAs(kingdomHost.Overlay));
            Assert.That(
                championHost.Preview(SharedMenuIds.Kingdom2_5D).FromMode,
                Is.EqualTo(SharedMenuIds.Adventure3D));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChampionHudCloseReopenReusesOneInactiveSceneLocalOverlay()
        {
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            var hud = new GameObject(
                "ChampionHudLifecycle",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(hud, _championScene);
            ChampionHudSession session = ChampionHudSession.Attach(hud.transform);

            session.OpenMenu();
            SharedMenuOverlay first = session.Overlay;
            session.CloseMenu();
            Assert.That(first.gameObject.activeSelf, Is.False);
            session.OpenMenu();

            Assert.That(session.Overlay, Is.SameAs(first));
            Assert.That(CountOverlaysInScene(_championScene), Is.EqualTo(1));
            Object.Destroy(hud);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnsureConvergesMixedActiveAndInactiveDuplicateOverlaysToOne()
        {
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            var first = new GameObject("DuplicateSharedMenuActive");
            SceneManager.MoveGameObjectToScene(first, _kingdomScene);
            first.AddComponent<SharedMenuOverlay>();
            var second = new GameObject("DuplicateSharedMenuInactive");
            SceneManager.MoveGameObjectToScene(second, _kingdomScene);
            second.AddComponent<SharedMenuOverlay>();
            second.SetActive(false);
            SharedMenuModeSwitchHost host =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);

            host.Open();
            yield return null;

            Assert.That(CountOverlaysInScene(_kingdomScene), Is.EqualTo(1));
            Assert.That(host.Overlay, Is.Not.Null);
            Assert.That(host.Overlay.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator UnexpectedClaimedOverlayDisableImmediatelyReleasesHudSuppression()
        {
            ChampionHudCameraGate.Reset();
            Time.timeScale = 1f;
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            var hud = new GameObject(
                "ChampionHudSurfaceLoss",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(hud, _championScene);
            ChampionHudSession session = ChampionHudSession.Attach(hud.transform);
            session.OpenMenu();
            Assert.That(ChampionHudCameraGate.MenuOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            session.Overlay.gameObject.SetActive(false);
            yield return null;

            Assert.That(session.IsOpen, Is.False);
            Assert.That(ChampionHudCameraGate.MenuOpen, Is.False);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Object.Destroy(hud);
        }

        [UnityTest]
        public IEnumerator OnlyHostInActiveSceneOwnsAutomaticSharedMenuInput()
        {
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            SharedMenuModeSwitchHost kingdomHost =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);

            SceneManager.SetActiveScene(_championScene);
            Assert.That(ReadOwnsAutomaticInput(kingdomHost), Is.False);

            SceneManager.SetActiveScene(_kingdomScene);
            Assert.That(ReadOwnsAutomaticInput(kingdomHost), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActiveSceneHudReplacesInactiveStickyInputOwner()
        {
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            var inactiveHud = new GameObject(
                "InactiveFirstHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(inactiveHud, _championScene);
            ChampionHudSession.Attach(inactiveHud.transform);
            var activeHud = new GameObject(
                "ActiveSecondHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(activeHud, _kingdomScene);
            ChampionHudSession activeSession = ChampionHudSession.Attach(activeHud.transform);
            SceneManager.SetActiveScene(_kingdomScene);

            InvokeHudInput(
                activeSession,
                cancelPressed: false,
                sharedMenuPressed: true,
                frame: 9001);
            yield return null;

            Assert.That(activeSession.IsOpen, Is.True);
            Assert.That(activeSession.Overlay.gameObject.scene, Is.EqualTo(_kingdomScene));
            Object.Destroy(inactiveHud);
            Object.Destroy(activeHud);
        }

        [UnityTest]
        public IEnumerator CrossSceneHudMenuHandoffClosesFormerSceneOverlay()
        {
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            var firstHud = new GameObject(
                "FirstSceneHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(firstHud, _championScene);
            ChampionHudSession first = ChampionHudSession.Attach(firstHud.transform);
            var secondHud = new GameObject(
                "SecondSceneHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(secondHud, _kingdomScene);
            ChampionHudSession second = ChampionHudSession.Attach(secondHud.transform);

            first.OpenMenu();
            SharedMenuOverlay formerOverlay = first.Overlay;
            Assert.That(formerOverlay.gameObject.activeSelf, Is.True);
            second.OpenMenu();
            yield return null;

            Assert.That(first.IsOpen, Is.False);
            Assert.That(formerOverlay.gameObject.activeSelf, Is.False);
            Assert.That(second.IsOpen, Is.True);
            Assert.That(second.Overlay.gameObject.scene, Is.EqualTo(_kingdomScene));
            Object.Destroy(firstHud);
            Object.Destroy(secondHud);
        }

        [UnityTest]
        public IEnumerator GameInputSuppressionClearsStoredMovementAndBlockBeforeUpdateMoves()
        {
            var champion = new GameObject("GameInputOnlySuppressedChampion");
            try
            {
                champion.AddComponent<CharacterController>();
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                controller.SetExternalMoveInput(Vector2.up);
                controller.SetBlocking(true);
                Assert.That(ReadPrivateBool(controller, "_isBlocking"), Is.True);
                Vector3 positionAtSuppression = champion.transform.position;

                using (GameInput.AcquireGameplaySuppression(
                           "stored-movement-game-input-test"))
                {
                    yield return null;
                    Assert.That(champion.transform.position.x,
                        Is.EqualTo(positionAtSuppression.x).Within(0.0001f));
                    Assert.That(champion.transform.position.z,
                        Is.EqualTo(positionAtSuppression.z).Within(0.0001f));
                    Assert.That(ReadPrivateBool(controller, "_isBlocking"), Is.False);
                    Assert.That(ReadPrivateVector2(controller, "_externalMoveInput"),
                        Is.EqualTo(Vector2.zero));
                }
            }
            finally
            {
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledControllerCancelsAcceptedAttackBeforeResolution()
        {
            var champion = new GameObject("DisabledActiveAttackChampion");
            try
            {
                champion.AddComponent<CharacterController>();
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                var resolver = new CountingAttackResolver();
                Assert.That(controller.TryBindEditorBasicAttackResolver(resolver), Is.True);
                int audioRootsBefore = CountRuntimeCombatAudioRoots();

                Assert.That(controller.RequestBasicAttack(), Is.True);
                Vector3 positionAtDisable = champion.transform.position;
                controller.enabled = false;

                Assert.That(ReadPrivateBool(controller, "_isAttacking"), Is.False);
                yield return new WaitForSeconds(0.2f);

                Assert.That(champion.transform.position, Is.EqualTo(positionAtDisable));
                Assert.That(resolver.CallCount, Is.Zero);
                Assert.That(CountRuntimeCombatAudioRoots(), Is.EqualTo(audioRootsBefore));

                controller.enabled = true;
                Assert.That(controller.RequestBasicAttack(), Is.True,
                    "Disabling must cancel the old attack without retaining its cooldown.");
                controller.enabled = false;
                Assert.That(resolver.CallCount, Is.Zero);
            }
            finally
            {
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalSuppressionCancelsBasicAttackAlreadyInProgress()
        {
            var champion = new GameObject("SuppressedActiveAttackChampion");
            float priorTimeScale = Time.timeScale;
            Time.timeScale = 0.1f;
            try
            {
                champion.AddComponent<CharacterController>();
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                var resolver = new CountingAttackResolver();
                Assert.That(controller.TryBindEditorBasicAttackResolver(resolver), Is.True);
                controller.RequestBasicAttack();
                yield return null;
                Assert.That(ReadPrivateBool(controller, "_isAttacking"), Is.True);
                Vector3 positionAtSuppression = champion.transform.position;

                using (ChampionHudCameraGate.AcquireCursorOwnership("active-attack-modal-test"))
                {
                    yield return null;
                    yield return null;
                    Assert.That(
                        Vector3.Distance(champion.transform.position, positionAtSuppression),
                        Is.LessThan(0.0001f));
                    Assert.That(ReadPrivateBool(controller, "_isAttacking"), Is.False);
                    Assert.That(resolver.CallCount, Is.Zero);
                }
            }
            finally
            {
                Time.timeScale = priorTimeScale;
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalSuppressionStopsDodgeAlreadyInProgress()
        {
            var champion = new GameObject("SuppressedActiveDodgeChampion");
            float priorTimeScale = Time.timeScale;
            Time.timeScale = 0.1f;
            try
            {
                champion.AddComponent<CharacterController>();
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                controller.RequestDodge();
                yield return null;
                Assert.That(ReadPrivateBool(controller, "_isDodging"), Is.True);
                Vector3 positionAtSuppression = champion.transform.position;

                using (ChampionHudCameraGate.AcquireCursorOwnership("active-dodge-modal-test"))
                {
                    yield return null;
                    yield return null;
                    Assert.That(
                        Vector3.Distance(champion.transform.position, positionAtSuppression),
                        Is.LessThan(0.0001f));
                    Assert.That(ReadPrivateBool(controller, "_isDodging"), Is.False);
                }
            }
            finally
            {
                Time.timeScale = priorTimeScale;
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalSuppressionCancelsSkillCastAlreadyInProgress()
        {
            var champion = new GameObject("SuppressedActiveSkillChampion");
            float priorTimeScale = Time.timeScale;
            Time.timeScale = 0.1f;
            try
            {
                champion.AddComponent<CharacterController>();
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                SkillCaster caster = champion.GetComponent<SkillCaster>();
                yield return null;
                Assert.That(caster.GetSkillId(3), Is.Not.Empty);
                Assert.That(caster.TryCastSkill(3), Is.True);
                yield return null;
                Assert.That(caster.IsCasting, Is.True);

                using (ChampionHudCameraGate.AcquireCursorOwnership("active-skill-modal-test"))
                {
                    yield return null;
                    Assert.That(caster.IsCasting, Is.False);
                    Assert.That(caster.ActiveSlot, Is.EqualTo(-1));
                    Assert.That(caster.GetCooldownRemaining(3), Is.Zero);
                }
            }
            finally
            {
                Time.timeScale = priorTimeScale;
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionComponentOrderStillRejectsLockedDirectSkillCast()
        {
            var champion = new GameObject("ProductionOrderLockedSkillCasterChampion");
            champion.AddComponent<CharacterController>();
            ChampionCombat combat = champion.AddComponent<ChampionCombat>();
            SkillCaster caster = champion.AddComponent<SkillCaster>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            yield return null;
            float manaBefore = combat.CurrentMana;

            controller.SetControlLocked(true);

            Assert.That(caster.TryCastSkill(0), Is.False);
            Assert.That(caster.IsCasting, Is.False);
            Assert.That(combat.CurrentMana, Is.EqualTo(manaBefore));
            Assert.That(caster.GetCooldownRemaining(0), Is.Zero);

            Object.Destroy(champion);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControlLockRejectsDirectSkillCasterIngress()
        {
            var champion = new GameObject("ControlLockedDirectSkillCasterChampion");
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            SkillCaster caster = champion.GetComponent<SkillCaster>();
            Assert.That(caster, Is.Not.Null);
            yield return null;
            Assert.That(caster.GetSkillId(0), Is.Not.Empty);

            controller.SetControlLocked(true);

            Assert.That(caster.TryCastSkill(0), Is.False);
            Assert.That(caster.IsCasting, Is.False);
            Assert.That(caster.ActiveSlot, Is.EqualTo(-1));
            Assert.That(caster.GetCooldownRemaining(0), Is.Zero);

            Object.Destroy(champion);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalSuppressionRejectsDirectSkillCasterIngress()
        {
            var champion = new GameObject("SuppressedDirectSkillCasterChampion");
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            SkillCaster caster = champion.GetComponent<SkillCaster>();
            Assert.That(caster, Is.Not.Null);
            yield return null;
            Assert.That(caster.GetSkillId(0), Is.Not.Empty,
                "The direct-ingress tracer requires a valid initialized skill slot.");

            using (ChampionHudCameraGate.AcquireCursorOwnership("direct-skill-modal-test"))
            {
                Assert.That(caster.TryCastSkill(0), Is.False);
                Assert.That(caster.IsCasting, Is.False);
                Assert.That(caster.ActiveSlot, Is.EqualTo(-1));
            }

            Object.Destroy(champion);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalSuppressionRejectsDirectCombatRequests()
        {
            var champion = new GameObject("SuppressedDirectCombatChampion");
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            SkillCaster caster = champion.GetComponent<SkillCaster>();
            Assert.That(caster, Is.Not.Null);
            using System.IDisposable modal =
                ChampionHudCameraGate.AcquireCursorOwnership("direct-combat-modal-test");

            controller.RequestBasicAttack();
            controller.RequestDodge();
            controller.RequestSkill(0);

            Assert.That(ReadPrivateBool(controller, "_isAttacking"), Is.False);
            Assert.That(ReadPrivateBool(controller, "_isDodging"), Is.False);
            Assert.That(caster.IsCasting, Is.False);
            Assert.That(caster.ActiveSlot, Is.EqualTo(-1));
            Object.Destroy(champion);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LegacyHostOpenClosesHudMenuAndWorldMapGlobally()
        {
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            var hudRoot = new GameObject(
                "MixedOwnerHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(hudRoot, _championScene);
            ChampionHudSession hud = ChampionHudSession.Attach(hudRoot.transform);
            hud.OpenMenu();
            SharedMenuOverlay formerOverlay = hud.Overlay;
            WorldMapSession.OpenMap();
            SharedMenuModeSwitchHost host =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);

            host.Open();
            yield return null;

            Assert.That(hud.IsOpen, Is.False);
            Assert.That(formerOverlay.gameObject.activeSelf, Is.False);
            Assert.That(WorldMapSession.IsMapOpen, Is.False);
            Assert.That(host.IsOpen, Is.True);
            Assert.That(host.Overlay.gameObject.scene, Is.EqualTo(_kingdomScene));
            Object.Destroy(hudRoot);
        }

        [UnityTest]
        public IEnumerator HudOpenClosesLegacyHostGlobally()
        {
            _championScene = SceneManager.CreateScene(SharedMenuIds.AdventureScene);
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            SharedMenuModeSwitchHost host =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);
            host.Open();
            SharedMenuOverlay formerOverlay = host.Overlay;
            var hudRoot = new GameObject(
                "ReverseMixedOwnerHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(hudRoot, _championScene);
            ChampionHudSession hud = ChampionHudSession.Attach(hudRoot.transform);

            hud.OpenMenu();
            yield return null;

            Assert.That(host.IsOpen, Is.False);
            Assert.That(formerOverlay.gameObject.activeSelf, Is.False);
            Assert.That(hud.IsOpen, Is.True);
            Object.Destroy(hudRoot);
        }

        [UnityTest]
        public IEnumerator LegacyHostResumeButtonClosesModal()
        {
            _kingdomScene = SceneManager.CreateScene(SharedMenuIds.KingdomScene);
            SharedMenuModeSwitchHost host =
                SharedMenuModeSwitchHost.EnsureForScene(_kingdomScene);
            host.Open();
            SharedMenuOverlay overlay = host.Overlay;
            Assert.That(overlay.ResumeButton, Is.Not.Null);

            overlay.ResumeButton.onClick.Invoke();
            yield return null;

            Assert.That(host.IsOpen, Is.False);
            Assert.That(overlay.gameObject.activeSelf, Is.False);
        }

        private static void InvokeHudInput(
            ChampionHudSession session,
            bool cancelPressed,
            bool sharedMenuPressed,
            int frame)
        {
            MethodInfo method = typeof(ChampionHudSession).GetMethod(
                "ProcessInputFrame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(
                session,
                new object[] { cancelPressed, sharedMenuPressed, frame });
        }

        private static bool ReadPrivateBool(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(target);
        }

        private static Vector2 ReadPrivateVector2(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (Vector2)field.GetValue(target);
        }

        private sealed class CountingAttackResolver : IChampionBasicAttackResolver
        {
            public int CallCount { get; private set; }

            public bool TryResolve(
                ChampionBasicAttackContext context,
                out ChampionBasicAttackResolution resolution)
            {
                CallCount++;
                resolution = new ChampionBasicAttackResolution(
                    ChampionBasicAttackResolutionKind.Hit,
                    context.HitCenter,
                    "HIT");
                return true;
            }
        }

        private ISaveGameService CreateTutorialSave(RealmId realm)
        {
            string root = Path.Combine(
                Application.temporaryCachePath,
                "AnotherLifeTests",
                nameof(SharedMenuModeSwitchHostLifecyclePlayModeTests),
                System.Guid.NewGuid().ToString("N"));
            _ownedSaveRoots.Add(root);
            var save = (LocalSaveGameService)System.Activator.CreateInstance(
                typeof(LocalSaveGameService),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { root },
                culture: null);
            save.Load();
            save.CreateNewSave(realm);
            return save;
        }

        private static int CountRuntimeCombatAudioRoots()
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            int count = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name == "ChampionRuntimeCombatAudio")
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ReadOwnsAutomaticInput(SharedMenuModeSwitchHost host)
        {
            MethodInfo method = typeof(SharedMenuModeSwitchHost).GetMethod(
                "OwnsAutomaticInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "Update must route through host-scene ownership.");
            return (bool)method.Invoke(host, null);
        }

        private static int CountOverlaysInScene(Scene scene)
        {
            int count = 0;
            SharedMenuOverlay[] overlays =
                Resources.FindObjectsOfTypeAll<SharedMenuOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                SharedMenuOverlay overlay = overlays[i];
                if (overlay != null && overlay.gameObject.scene == scene)
                {
                    count++;
                }
            }

            return count;
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
