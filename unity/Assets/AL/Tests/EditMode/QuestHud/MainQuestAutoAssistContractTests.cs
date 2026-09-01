using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.AI;
using AL.ChampionMode.Camera;
using AL.ChampionMode.Control;
using AL.ChampionMode.Interaction;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Skills;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Catalogs.WorldAtlas;
using AL.Data.Runtime;
using AL.Input;
using AL.UI.Kingdom;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using AL.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.QuestHud
{
    public sealed class MainQuestAutoAssistContractTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            RemoveSaveService();
            ProofOfWorthDirector.ResetForTests();
            QuestHudAutoQuest.ResetForTests();
            ChampionHudCameraGate.Reset();
            MainQuestMapSession.ResetForTests();
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            _root = new GameObject("MainQuestAutoAssistContractTests.Root");
        }

        [TearDown]
        public void TearDown()
        {
            RemoveSaveService();
            QuestHudAutoQuest.ResetForTests();
            ChampionHudCameraGate.Reset();
            ProofOfWorthDirector.ResetForTests();
            MainQuestMapSession.ResetForTests();
            FirstSessionChampionStart.ResetToFirstSessionLanding();

            foreach (QuestHudOverlay overlay in Object.FindObjectsOfType<QuestHudOverlay>())
            {
                Object.DestroyImmediate(overlay.gameObject);
            }

            foreach (ProofOfWorthDirector director in Object.FindObjectsOfType<ProofOfWorthDirector>())
            {
                Object.DestroyImmediate(director.gameObject);
            }

            GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
            if (markerRoot != null)
            {
                Object.DestroyImmediate(markerRoot);
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void AutoQuestShowsNpcConversationBeforeAdvancingTheQuest()
        {
            var champion = new GameObject("ConversationIntegrationChampion");
            var speaker = new GameObject(FirstSessionWorldInteractables.GuideObjectName);
            var cameraObject = new GameObject("ConversationIntegrationCamera");
            champion.transform.SetParent(_root.transform, false);
            speaker.transform.SetParent(_root.transform, false);
            cameraObject.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionController>();
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<UnityEngine.Camera>();
            QuestHudAutoQuest.SetEnabled(true);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();

            director.EnsureReady(null, champion.transform, RealmId.Crownlands);

            NpcConversationView view = Object.FindObjectOfType<NpcConversationView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.Session.DialogueId, Is.EqualTo(ProofOfWorthIds.OfferDialogueId));
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenOffered));

            Assert.That(view.SkipCurrentLine(), Is.True);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenTalk));
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.Session.DialogueId, Is.EqualTo(ProofOfWorthIds.StartDialogueId));
        }

        [Test]
        public void NpcConversationFramesSpeakerAtBottomAndRestoresCameraOnCollapse()
        {
            var player = new GameObject("ConversationPlayer");
            var speaker = new GameObject(FirstSessionWorldInteractables.GuideObjectName);
            var cameraObject = new GameObject("ConversationCamera");
            player.transform.SetParent(_root.transform, false);
            speaker.transform.SetParent(_root.transform, false);
            cameraObject.transform.SetParent(_root.transform, false);
            player.transform.position = Vector3.zero;
            speaker.transform.position = new Vector3(0f, 0f, 4f);
            cameraObject.transform.position = new Vector3(0f, 3f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            int completed = 0;
            NpcConversationView view = NpcConversationView.Mount(_root.transform);

            view.Show(
                "DIALOGUE_OFFER",
                ProofOfWorthCopy.SpeakerName,
                ProofOfWorthCopy.OfferBody,
                player.transform,
                speaker.transform,
                camera,
                () => completed++);

            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.PanelRect.anchorMin.y, Is.EqualTo(0f));
            Assert.That(view.PanelRect.anchorMax.y, Is.EqualTo(0f));
            Assert.That(view.SubtitleLabel.text, Is.EqualTo(ProofOfWorthCopy.OfferBody));
            Assert.That(ChampionHudCameraGate.BlocksGameplay, Is.True);
            Vector3 toSpeaker =
                (speaker.transform.position + Vector3.up * 1.45f - camera.transform.position).normalized;
            Assert.That(Vector3.Dot(camera.transform.forward, toSpeaker), Is.GreaterThan(0.995f));

            view.Collapse();

            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.Session.IsCollapsed, Is.True);
            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(originalRotation));
            Assert.That(ChampionHudCameraGate.BlocksGameplay, Is.False);

            view.Reopen();
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.Session.DialogueId, Is.EqualTo("DIALOGUE_OFFER"));
            Assert.That(view.SkipCurrentLine(), Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(ChampionHudCameraGate.BlocksGameplay, Is.False);
        }

        [Test]
        public void ConversationReleasesOnlyItsCursorTokenAndPreservesExistingOwner()
        {
            System.IDisposable worldMapOwner =
                ChampionHudCameraGate.AcquireCursorOwnership("world-map");
            NpcConversationView view = NpcConversationView.Mount(_root.transform);
            view.Show(
                "DIALOGUE_CURSOR_OWNER",
                ProofOfWorthCopy.SpeakerName,
                ProofOfWorthCopy.OfferBody,
                null,
                null,
                null,
                null);

            view.Collapse();

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.CursorModeSuppressed, Is.True);

            worldMapOwner.Dispose();

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }


        [Test]
        public void DisablingActiveConversationHidesPanelBeforeReleasingOwnership()
        {
            var cameraObject = new GameObject("ConversationDisableCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            var follow = cameraObject.AddComponent<AL.ChampionMode.Camera.CameraFollow>();
            NpcConversationView view = NpcConversationView.Mount(_root.transform);
            view.Show(
                "DIALOGUE_DISABLE",
                ProofOfWorthCopy.SpeakerName,
                ProofOfWorthCopy.OfferBody,
                null,
                null,
                camera,
                null);
            Assert.That(follow.enabled, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(view.IsVisible, Is.True);

            view.enabled = false;
            InvokePrivate(view, "OnDisable");

            Assert.That(view.Session.IsCompleted, Is.False);
            Assert.That(view.Session.IsCollapsed, Is.False);
            Assert.That(view.IsVisible, Is.False);
            Assert.That(follow.enabled, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        [Test]
        public void ReenablingActiveConversationReacquiresCameraAndCursorOwnership()
        {
            var cameraObject = new GameObject("ConversationReenableCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            var follow = cameraObject.AddComponent<AL.ChampionMode.Camera.CameraFollow>();
            NpcConversationView view = NpcConversationView.Mount(_root.transform);
            view.Show(
                "DIALOGUE_REENABLE",
                ProofOfWorthCopy.SpeakerName,
                ProofOfWorthCopy.OfferBody,
                null,
                null,
                camera,
                null);
            view.enabled = false;
            InvokePrivate(view, "OnDisable");
            Assert.That(follow.enabled, Is.True);
            Assert.That(view.IsVisible, Is.False);

            view.enabled = true;
            InvokePrivate(view, "OnEnable");

            Assert.That(view.Session.IsCollapsed, Is.False);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(follow.enabled, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.CursorModeSuppressed, Is.True);
        }

        [Test]
        public void ConversationCannotReleaseSharedMenuCursorOwnership()
        {
            ChampionHudSession menu = ChampionHudSession.Attach(_root.transform);
            menu.OpenMenu();
            NpcConversationView view = NpcConversationView.Mount(_root.transform);
            view.Show(
                "DIALOGUE_OVER_MENU",
                ProofOfWorthCopy.SpeakerName,
                ProofOfWorthCopy.OfferBody,
                null,
                null,
                null,
                null);

            view.Collapse();

            Assert.That(ChampionHudCameraGate.MenuOpen, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(GameInput.CursorModeSuppressed, Is.True);

            menu.CloseMenu();

            Assert.That(ChampionHudCameraGate.MenuOpen, Is.False);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        [Test]
        public void ReopeningAlreadyActiveConversationDoesNotOverwritePresentationOwnership()
        {
            var player = new GameObject("ActiveReopenPlayer");
            var speaker = new GameObject("ActiveReopenSpeaker");
            var cameraObject = new GameObject("ActiveReopenCamera");
            var camera = cameraObject.AddComponent<Camera>();
            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            follow.enabled = true;
            Vector3 originalPosition = new Vector3(6f, 7f, 8f);
            camera.transform.position = originalPosition;
            NpcConversationView view = NpcConversationView.Mount();
            view.Show(
                "DIALOGUE_ACTIVE_REOPEN",
                "Captain Valerius",
                "Hold your focus.",
                player.transform,
                speaker.transform,
                camera,
                null,
                4f);

            view.Reopen();
            view.Collapse();

            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(follow.enabled, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        [Test]
        public void NpcConversationCollapsePausesAndReopenResumesTheSameLine()
        {
            var session = new NpcConversationSession(
                "DIALOGUE_OFFER",
                "The Veil Watch has detected a strange resonance.",
                4f);

            Assert.That(session.Advance(1f), Is.False);
            session.Collapse();
            Assert.That(session.Advance(10f), Is.False);
            Assert.That(session.DialogueId, Is.EqualTo("DIALOGUE_OFFER"));
            Assert.That(session.Body, Does.Contain("Veil Watch"));

            session.Reopen();

            Assert.That(session.Advance(2.99f), Is.False);
            Assert.That(session.Advance(0.01f), Is.True);
        }

        [Test]
        public void NpcConversationClickSkipsOnlyTheCurrentLine()
        {
            var session = new NpcConversationSession(
                "DIALOGUE_START",
                "The air itself is trembling.",
                4f);

            Assert.That(session.SkipCurrentLine(), Is.True);
            Assert.That(session.SkipCurrentLine(), Is.False);
        }

        [Test]
        public void OnlyExactArenaGuardianCanArmQuestCombatDuringGuardianPhase()
        {
            var champion = new GameObject("GuardianPhaseChampion");
            var arenaObject = new GameObject("GuardianOwnerArena");
            var guardian = new GameObject("OwnedGuardianTarget");
            var unrelatedBoss = new GameObject("UnrelatedBossTarget");
            champion.transform.SetParent(_root.transform, false);
            arenaObject.transform.SetParent(_root.transform, false);
            guardian.transform.SetParent(_root.transform, false);
            unrelatedBoss.transform.SetParent(_root.transform, false);
            guardian.AddComponent<BossDummyAI>();
            unrelatedBoss.AddComponent<BossDummyAI>();
            ChampionArenaSceneController arena =
                arenaObject.AddComponent<ChampionArenaSceneController>();
            SetPrivateField(arena, "_bossTransform", guardian.transform);
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionController>();
            AutoCombatController combat = champion.AddComponent<AutoCombatController>();

            Assert.That(
                ProofOfWorthDirector.TryBindQuestCombat(
                    ProofOfWorthPhase.C1RestoreCovenant,
                    arena,
                    combat,
                    guardian.transform),
                Is.False);
            Assert.That(combat.QuestTarget, Is.Null);

            Assert.That(
                ProofOfWorthDirector.TryBindQuestCombat(
                    ProofOfWorthPhase.C1FaceGuardian,
                    arena,
                    combat,
                    unrelatedBoss.transform),
                Is.False);
            Assert.That(combat.QuestTarget, Is.Null);

            Assert.That(
                ProofOfWorthDirector.TryBindQuestCombat(
                    ProofOfWorthPhase.C1FaceGuardian,
                    arena,
                    combat,
                    guardian.transform),
                Is.True);
            Assert.That(combat.QuestTarget, Is.SameAs(guardian.transform));
        }

        [Test]
        public void QuestCombatUsesOnlyAssignedTargetAndManualOverrideResumes()
        {
            var champion = new GameObject("QuestAutoCombatChampion");
            var arenaObject = new GameObject("QuestAutoCombatArena");
            var guardian = new GameObject("QuestGuardian");
            champion.transform.SetParent(_root.transform, false);
            arenaObject.transform.SetParent(_root.transform, false);
            guardian.transform.SetParent(_root.transform, false);
            guardian.AddComponent<BossDummyAI>();
            ChampionArenaSceneController arena =
                arenaObject.AddComponent<ChampionArenaSceneController>();
            SetPrivateField(arena, "_bossTransform", guardian.transform);
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionController>();
            AutoCombatController combat = champion.AddComponent<AutoCombatController>();
            QuestHudAutoQuest.SetEnabled(true);

            Assert.That(combat.TryAssignQuestTarget(arena, guardian.transform), Is.True);
            Assert.That(combat.QuestTarget, Is.SameAs(guardian.transform));
            Assert.That(combat.CanDriveQuestTargetAt(10f), Is.True);

            combat.NotifyManualOverrideAt(10f);

            Assert.That(QuestHudAutoQuest.Enabled, Is.True);
            Assert.That(combat.CanDriveQuestTargetAt(11.24f), Is.False);
            Assert.That(combat.CanDriveQuestTargetAt(11.25f), Is.True);
            Assert.That(combat.TryAssignQuestTarget(arena, null), Is.False);
            Assert.That(combat.QuestTarget, Is.SameAs(guardian.transform));
        }

        [Test]
        public void AutoQuestToggleOffClearsPreviouslyAppliedExternalMovement()
        {
            var champion = new GameObject("ToggleClearsMovementChampion");
            var arenaObject = new GameObject("ToggleClearsMovementArena");
            var guardian = new GameObject("ToggleClearsMovementGuardian");
            champion.transform.SetParent(_root.transform, false);
            arenaObject.transform.SetParent(_root.transform, false);
            guardian.transform.SetParent(_root.transform, false);
            guardian.AddComponent<BossDummyAI>();
            ChampionArenaSceneController arena =
                arenaObject.AddComponent<ChampionArenaSceneController>();
            SetPrivateField(arena, "_bossTransform", guardian.transform);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            AutoCombatController combat = champion.AddComponent<AutoCombatController>();
            InvokePrivate(combat, "Awake");
            QuestHudAutoQuest.SetEnabled(true);
            Assert.That(combat.TryAssignQuestTarget(arena, guardian.transform), Is.True);
            controller.SetExternalMoveInput(Vector2.one);

            QuestHudAutoQuest.SetEnabled(false);
            InvokePrivate(combat, "Update");

            Assert.That(ReadExternalMove(controller), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ModalSuppressionStopsQuestAutoCombatMovementDecisions()
        {
            var champion = new GameObject("SuppressedAutoCombatChampion");
            var arenaObject = new GameObject("SuppressedAutoCombatArena");
            var guardian = new GameObject("SuppressedAutoCombatGuardian");
            champion.transform.SetParent(_root.transform, false);
            arenaObject.transform.SetParent(_root.transform, false);
            guardian.transform.SetParent(_root.transform, false);
            guardian.transform.position = Vector3.forward * 10f;
            guardian.AddComponent<BossDummyAI>();
            ChampionArenaSceneController arena =
                arenaObject.AddComponent<ChampionArenaSceneController>();
            SetPrivateField(arena, "_bossTransform", guardian.transform);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            AutoCombatController combat = champion.AddComponent<AutoCombatController>();
            InvokePrivate(combat, "Awake");
            QuestHudAutoQuest.SetEnabled(true);
            Assert.That(combat.TryAssignQuestTarget(arena, guardian.transform), Is.True);
            using System.IDisposable modal =
                ChampionHudCameraGate.AcquireCursorOwnership("auto-combat-modal-test");

            InvokePrivate(combat, "Update");

            Assert.That(ReadExternalMove(controller), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ModalSuppressionRevokesMainQuestAutoProgressionAuthority()
        {
            QuestHudAutoQuest.SetEnabled(true);
            using System.IDisposable modal =
                ChampionHudCameraGate.AcquireCursorOwnership("auto-quest-modal-test");

            Assert.That(QuestHudAutoQuest.CanDriveInCurrentContext(), Is.False);
        }

        [Test]
        public void DestroyedGuardianTargetClearsPreviouslyAppliedExternalMovement()
        {
            var champion = new GameObject("DestroyedTargetChampion");
            var arenaObject = new GameObject("DestroyedTargetArena");
            var guardian = new GameObject("DestroyedTargetGuardian");
            champion.transform.SetParent(_root.transform, false);
            arenaObject.transform.SetParent(_root.transform, false);
            guardian.transform.SetParent(_root.transform, false);
            guardian.AddComponent<BossDummyAI>();
            ChampionArenaSceneController arena =
                arenaObject.AddComponent<ChampionArenaSceneController>();
            SetPrivateField(arena, "_bossTransform", guardian.transform);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            AutoCombatController combat = champion.AddComponent<AutoCombatController>();
            InvokePrivate(combat, "Awake");
            QuestHudAutoQuest.SetEnabled(true);
            Assert.That(combat.TryAssignQuestTarget(arena, guardian.transform), Is.True);
            controller.SetExternalMoveInput(Vector2.one);

            Object.DestroyImmediate(guardian);
            InvokePrivate(combat, "Update");

            Assert.That(ReadExternalMove(controller), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DestroyAfterPriorDisableStillClearsExternalMovement()
        {
            var champion = new GameObject("DestroyLifecycleChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            AutoCombatController combat = champion.AddComponent<AutoCombatController>();
            InvokePrivate(combat, "Awake");
            combat.enabled = false;
            controller.SetExternalMoveInput(Vector2.one);

            InvokePrivate(combat, "OnDestroy");
            Object.DestroyImmediate(combat);

            Assert.That(ReadExternalMove(controller), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AutoQuestPreferenceSurvivesRuntimeCacheReload()
        {
            QuestHudAutoQuest.SetEnabled(true);

            QuestHudAutoQuest.ResetRuntimeCacheForTests();

            Assert.That(QuestHudAutoQuest.Enabled, Is.True);
        }

        [Test]
        public void AutoQuestOnCompletesNextThreeDMainQuestStepWithoutMutatingSideQuest()
        {
            var unrelatedQuest = new QuestState
            {
                QuestId = "SIDE_SENTINEL",
                CurrentValue = 0,
                IsCompleted = false,
                IsClaimed = false
            };
            var save = new SaveGameData
            {
                Quests = new List<QuestState> { unrelatedQuest }
            };
            ServiceLocator.Register<ISaveGameService>(new TestSaveGameService(save));
            QuestHudAutoQuest.SetEnabled(true);
            ChampionController controller = CreateConfiguredChampion(
                _root,
                RealmId.Stonehold);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();

            director.EnsureReady(null, _root.transform, RealmId.Stonehold);
            AdvanceOpeningConversationToArena();

            Assert.That(director.State.QuestId, Is.EqualTo(ProofOfWorthIds.OmenQuestId));
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
            Assert.That(director.State.OmenAccepted, Is.True);
            Assert.That(
                Object.FindObjectOfType<NpcConversationView>().SkipCurrentLine(),
                Is.True);

            GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
            Assert.That(markerRoot, Is.Not.Null);
            Assert.That(markerRoot.transform.childCount, Is.EqualTo(1));
            _root.transform.position = markerRoot.transform.GetChild(0).position;
            InvokeUpdate(director);
            NpcConversationView report = Object.FindObjectOfType<NpcConversationView>();
            Assert.That(report.SkipCurrentLine(), Is.True);
            Assert.That(report.SkipCurrentLine(), Is.True);

            Assert.That(director.State.QuestId, Is.EqualTo(ProofOfWorthIds.MainQuestId));
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.C1MeetGuide));
            Assert.That(save.Quests, Has.Count.EqualTo(1));
            Assert.That(
                ServiceLocator.Get<ISaveGameService>().CurrentSave.Quests[0],
                Is.SameAs(unrelatedQuest));
            Assert.That(unrelatedQuest.CurrentValue, Is.Zero);
            Assert.That(unrelatedQuest.IsCompleted, Is.False);
            Assert.That(unrelatedQuest.IsClaimed, Is.False);
        }

        [Test]
        public void AutoQuestOffNeverAcceptsTheOfferedMainQuest()
        {
            QuestHudAutoQuest.SetEnabled(false);
            ChampionController controller = CreateConfiguredChampion(
                _root,
                RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();

            director.EnsureReady(null, _root.transform, RealmId.Crownlands);
            NpcConversationView conversation =
                Object.FindObjectOfType<NpcConversationView>();
            Assert.That(conversation, Is.Not.Null);
            conversation.Collapse();
            Assert.That(controller.isActiveAndEnabled, Is.True);
            Assert.That(controller.BlocksGameplayEntry, Is.False);
            InvokeUpdate(director);

            Assert.That(director.State.QuestId, Is.EqualTo(ProofOfWorthIds.OmenQuestId));
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenOffered));
            Assert.That(director.State.IsOmenOffered, Is.True);
            Assert.That(director.State.OmenAccepted, Is.False);
        }

        [Test]
        public void AutoQuestCannotCompleteAnArrivalWithoutAPlayer()
        {
            QuestHudAutoQuest.SetEnabled(true);
            var player = new GameObject("MissingArrivalPlayer");
            player.transform.SetParent(_root.transform, false);
            ChampionController controller = CreateConfiguredChampion(
                player,
                RealmId.Stonehold);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();

            director.EnsureReady(null, player.transform, RealmId.Stonehold);
            Object.FindObjectOfType<NpcConversationView>().Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.AcceptOffer).Changed,
                Is.True);
            Object.FindObjectOfType<NpcConversationView>().Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.Investigate).Changed,
                Is.True);
            Object.FindObjectOfType<NpcConversationView>().Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.DeployChampion).Changed,
                Is.True);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));

            Object.DestroyImmediate(player);
            InvokeUpdate(director);

            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
        }

        [Test]
        public void AutoQuestCannotCompleteAnArrivalWithoutItsMarker()
        {
            QuestHudAutoQuest.SetEnabled(true);
            ChampionController controller = CreateConfiguredChampion(
                _root,
                RealmId.Eldergrove);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, _root.transform, RealmId.Eldergrove);
            AdvanceOpeningConversationToArena();
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
            Object.DestroyImmediate(GameObject.Find(ProofOfWorthDirector.MarkerRootName));

            InvokeUpdate(director);

            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
        }

        [Test]
        public void DisablingAutoQuestStopsPreviouslyBoundEligibleModel()
        {
            QuestHudAutoQuest.SetEnabled(true);
            int primaryInvocations = 0;
            QuestHudOverlay hud = QuestHudOverlay.Mount(_root.transform);
            QuestHudModel model = QuestHudPlanner.FromProofOfWorth(
                ProofOfWorthPlanner.CreateOffered(RealmId.Eldergrove),
                autoQuestOn: true);
            Assert.That(model.CanAutoFire, Is.True);
            using (ChampionHudCameraGate.AcquireCursorOwnership(
                       "stale-auto-quest-model-bind-test"))
            {
                hud.Bind(model, () => primaryInvocations++);
            }
            Assert.That(primaryInvocations, Is.Zero);

            QuestHudAutoQuest.SetEnabled(false);
            hud.ConsiderAutoQuest();

            Assert.That(primaryInvocations, Is.Zero);
        }

        [Test]
        public void WarzoneGatePromptCannotBeAutoAcceptedOrCompleted()
        {
            QuestHudAutoQuest.SetEnabled(true);
            int primaryInvocations = 0;
            QuestHudOverlay hud = QuestHudOverlay.Mount(_root.transform);

            hud.Bind(
                QuestHudPlanner.WarzoneGate(autoQuestOn: true),
                () => primaryInvocations++);
            hud.ConsiderAutoQuest();

            Assert.That(hud.Model.IsWarzoneGate, Is.True);
            Assert.That(hud.Model.Action, Is.EqualTo(QuestHudAction.None));
            Assert.That(hud.Model.CanAutoFire, Is.False);
            Assert.That(QuestHudAutoQuest.ShouldFire(hud.Model), Is.False);
            Assert.That(primaryInvocations, Is.Zero);
        }

        [Test]
        public void TeachingChainIsLockedNarrativeBeforeLordshipAndAvailableAfter()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            var save = new SaveGameData
            {
                SelectedRealm = RealmId.Eldergrove,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "ranger",
                    IdentityConfirmed = true
                }
            };

            SharedMenuModuleState locked = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            KingdomTeachingState unavailable = KingdomTeachingQuestline.Evaluate(save, catalog);
            Assert.That(locked.Availability, Is.EqualTo(SharedMenuAvailability.LockedNarrative));
            Assert.That(unavailable.IsAvailable, Is.False);
            Assert.That(unavailable.CurrentStep, Is.Null);

            Assert.That(
                ProofOfWorthLordship.TryWriteMark(
                    save,
                    ProofOfWorthLordship.ResolveMarkId(save.SelectedRealm)),
                Is.True);
            SharedMenuModuleState available = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            KingdomTeachingState teaching = KingdomTeachingQuestline.Evaluate(save, catalog);

            Assert.That(available.Availability, Is.EqualTo(SharedMenuAvailability.Available));
            Assert.That(teaching.IsAvailable, Is.True);
            Assert.That(teaching.IsComplete, Is.False);
            Assert.That(teaching.CurrentStep, Is.SameAs(catalog.Steps[0]));
        }

        [Test]
        public void MapAndMinimapEnumerableMarkersContainNoOuterRealmIds()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            MainQuestMapMarkerCatalog markerCatalog = MainQuestMapMarkerCatalog.LoadCanonical();
            var realms = new[]
            {
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Crownlands,
                RealmId.Umbral
            };
            Assert.That(markerCatalog.ObjectiveIds.Count, Is.EqualTo(7));

            foreach (RealmId realm in realms)
            {
                KingdomWorldMapQueryResult kingdom = KingdomWorldMapQuery.Enumerate(snapshot, realm);
                Assert.That(kingdom.RegionIds.Count, Is.GreaterThan(0), realm.ToString());
                Assert.That(kingdom.MarkerIds.Count, Is.GreaterThan(0), realm.ToString());
                Assert.That(KingdomWorldMapQuery.ContainsOuterRealmId(kingdom.RegionIds), Is.False, realm.ToString());
                Assert.That(KingdomWorldMapQuery.ContainsOuterRealmId(kingdom.MarkerIds), Is.False, realm.ToString());

                foreach (string objectiveId in markerCatalog.ObjectiveIds)
                {
                    IReadOnlyList<MainQuestMapMarker> current =
                        MainQuestMapMarkerResolver.ResolveCurrent(
                            snapshot,
                            markerCatalog,
                            objectiveId,
                            realm,
                            "Continue the current main quest.");
                    Assert.That(current.Count, Is.EqualTo(1), objectiveId + " / " + realm);
                    Assert.That(current[0].IsInnerRealm, Is.True, current[0].MarkerId);
                    Assert.That(KingdomWorldMapQuery.IsForbiddenId(current[0].MarkerId), Is.False);
                    Assert.That(KingdomWorldMapQuery.IsForbiddenId(current[0].ZoneId), Is.False);
                }
            }

            Assert.That(
                MainQuestMapMarkerResolver.ResolveCurrent(
                    snapshot,
                    markerCatalog,
                    "OBJ_ENTER_WARZONE",
                    RealmId.Stonehold,
                    "Do not enter automatically."),
                Is.Empty);
        }

        private static Vector2 ReadExternalMove(ChampionController controller)
        {
            FieldInfo field = typeof(ChampionController).GetField(
                "_externalMoveInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (Vector2)field.GetValue(controller);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static ChampionController CreateConfiguredChampion(
            GameObject host,
            RealmId realm)
        {
            host.AddComponent<CharacterController>();
            host.AddComponent<ChampionCombat>();
            host.AddComponent<SkillCaster>();
            ChampionController controller = host.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(realm);
            return controller;
        }

        private static void InvokeUpdate(ProofOfWorthDirector director)
        {
            MethodInfo update = typeof(ProofOfWorthDirector).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(director, null);
        }

        private static void AdvanceOpeningConversationToArena()
        {
            NpcConversationView view = Object.FindObjectOfType<NpcConversationView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.SkipCurrentLine(), Is.True);
            Assert.That(view.SkipCurrentLine(), Is.True);
            Assert.That(view.SkipCurrentLine(), Is.True);
        }

        private static void RemoveSaveService()
        {
            FieldInfo servicesField = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(servicesField, Is.Not.Null);
            var services = (IDictionary)servicesField.GetValue(null);
            services.Remove(typeof(ISaveGameService));
        }

        private sealed class TestSaveGameService : ISaveGameService
        {
            internal TestSaveGameService(SaveGameData save)
            {
                CurrentSave = save;
            }

            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.None;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;

            public void Save()
            {
            }

            public void Load()
            {
            }

            public bool HasSave()
            {
                return CurrentSave != null;
            }

            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }

            public void DeleteSave()
            {
                CurrentSave = null;
            }
        }
    }
}
