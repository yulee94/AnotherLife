using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Interaction;
using AL.ChampionMode.Quests;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.Kingdom;
using AL.UI.QuestHud;
using AL.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    public sealed class MainQuestAutoFollowPlayModeTests
    {
        private GameObject _root;
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            ProofOfWorthDirector.ResetForTests();
            QuestHudAutoQuest.ResetForTests();
            KingdomTeachingInteraction.ResetForTests();
            ChampionHudCameraGate.Reset();
            _root = new GameObject("MainQuestAutoFollowPlayModeRoot");
            _saveRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-MainQuestTeachingPlayModeTests",
                Guid.NewGuid().ToString("N"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            QuestHudAutoQuest.ResetForTests();
            ProofOfWorthDirector.ResetForTests();
            KingdomTeachingInteraction.ResetForTests();
            ChampionHudCameraGate.Reset();
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            ProofOfWorthDirector[] directors = Object.FindObjectsOfType<ProofOfWorthDirector>();
            for (int i = 0; i < directors.Length; i++)
            {
                Object.Destroy(directors[i].gameObject);
            }

            GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
            if (markerRoot != null)
            {
                Object.Destroy(markerRoot);
            }

            yield return null;

            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator ConversationPreservesCameraFollowManualCursorOwnership()
        {
            var cameraObject = new GameObject("ConversationManualCursorCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            var follow = cameraObject.AddComponent<AL.ChampionMode.Camera.CameraFollow>();
            yield return null;

            follow.ApplyCursorModeToggle(true);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            NpcConversationView view = NpcConversationView.Mount(_root.transform);
            view.Show(
                "DIALOGUE_MANUAL_CURSOR",
                ProofOfWorthCopy.SpeakerName,
                ProofOfWorthCopy.OfferBody,
                null,
                null,
                camera,
                null);
            yield return null;

            view.Collapse();
            yield return null;

            Assert.That(follow.enabled, Is.True);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);
            follow.ApplyCursorModeToggle(true);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator DisabledProofDirectorRejectsEveryProgressionIngress()
        {
            QuestHudAutoQuest.SetEnabled(false);
            var champion = new GameObject("DisabledProofDirectorChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            Object.FindObjectOfType<NpcConversationView>().Collapse();
            ProofOfWorthState before = director.State;

            director.enabled = false;
            ProofOfWorthTransition direct =
                director.ApplyForTests(ProofOfWorthCommand.AcceptOffer);
            director.ChoosePrimary();
            director.ChooseSecondary();
            bool interacted = director.ApplyWorldInteractionForTests(
                FirstSessionWorldInteractables.GuideCatalogId);
            director.Hud.FirePrimary();
            yield return null;

            Assert.That(direct.Changed, Is.False);
            Assert.That(interacted, Is.False);
            Assert.That(director.State, Is.SameAs(before));
        }

        [UnityTest]
        public IEnumerator DisabledProofDirectorRetiresConversationSuppression()
        {
            var champion = new GameObject("DisabledProofConversationChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            NpcConversationView conversation =
                Object.FindObjectOfType<NpcConversationView>();
            Assert.That(conversation, Is.Not.Null);
            Assert.That(conversation.IsVisible, Is.True);
            Assert.That(conversation.Session, Is.Not.Null);

            director.enabled = false;
            yield return null;

            Assert.That(conversation.IsVisible, Is.False);
            Assert.That(conversation.Session, Is.Null);
            Assert.That(ChampionHudCameraGate.BlocksGameplay, Is.False);
        }

        [UnityTest]
        public IEnumerator ReenabledProofDirectorRestoresCurrentConversation()
        {
            var champion = new GameObject("ReenabledProofConversationChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            NpcConversationView conversation =
                Object.FindObjectOfType<NpcConversationView>();

            director.enabled = false;
            yield return null;
            director.enabled = true;
            yield return null;

            Assert.That(conversation.IsVisible, Is.True);
            Assert.That(conversation.Session, Is.Not.Null);
            Assert.That(conversation.Session.DialogueId,
                Is.EqualTo(ProofOfWorthIds.OfferDialogueId));
        }

        [UnityTest]
        public IEnumerator MatchingVisibleProofConversationMayAdvanceThroughItsOwnCursorGate()
        {
            QuestHudAutoQuest.SetEnabled(false);
            var champion = new GameObject("MatchingProofConversationChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            NpcConversationView conversation = Object.FindObjectOfType<NpcConversationView>();

            Assert.That(conversation, Is.Not.Null);
            Assert.That(conversation.IsVisible, Is.True);
            Assert.That(conversation.Session, Is.Not.Null);
            Assert.That(
                conversation.Session.DialogueId,
                Is.EqualTo(director.State.DialogueId));
            Assert.That(controller.BlocksGameplayEntry, Is.True);

            director.ChoosePrimary();
            yield return null;

            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenTalk));
            Assert.That(director.State.DialogueId, Is.EqualTo(ProofOfWorthIds.StartDialogueId));
        }

        [UnityTest]
        public IEnumerator LockedOwnerRejectsDirectProofTestIngress()
        {
            var champion = new GameObject("LockedProofDirectIngressChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            controller.SetControlLocked(true);
            ProofOfWorthState before = director.State;

            ProofOfWorthTransition transition =
                director.ApplyForTests(ProofOfWorthCommand.AcceptOffer);
            yield return null;

            Assert.That(transition.Changed, Is.False);
            Assert.That(director.State, Is.SameAs(before));
        }

        [UnityTest]
        public IEnumerator LockedOwnerRejectsProofHudSecondaryProgression()
        {
            QuestHudAutoQuest.SetEnabled(false);
            var champion = new GameObject("LockedProofSecondaryChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            Object.FindObjectOfType<NpcConversationView>().Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.AcceptOffer).Changed,
                Is.True);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenTalk));
            Assert.That(director.State.DialogueId,
                Is.EqualTo(ProofOfWorthIds.StartDialogueId));
            controller.SetControlLocked(true);
            ProofOfWorthState before = director.State;

            director.ChooseSecondary();
            yield return null;

            Assert.That(director.State, Is.SameAs(before));
        }

        [UnityTest]
        public IEnumerator LockedOwnerRejectsProofWorldInteractionProgression()
        {
            QuestHudAutoQuest.SetEnabled(false);
            var champion = new GameObject("LockedProofInteractionChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            controller.SetControlLocked(true);
            ProofOfWorthState before = director.State;

            bool applied = director.ApplyWorldInteractionForTests(
                FirstSessionWorldInteractables.GuideCatalogId);
            yield return null;

            Assert.That(applied, Is.False);
            Assert.That(director.State, Is.SameAs(before));
        }

        [UnityTest]
        public IEnumerator LockedOwnerRejectsProofHudPrimaryProgression()
        {
            QuestHudAutoQuest.SetEnabled(false);
            var champion = new GameObject("LockedProofPrimaryChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            controller.SetControlLocked(true);
            ProofOfWorthState before = director.State;

            director.ChoosePrimary();
            yield return null;

            Assert.That(director.State, Is.SameAs(before));
        }

        [UnityTest]
        public IEnumerator AutoQuestOnPreservesProofProgressWhileOwnerControlIsLocked()
        {
            CreateGround();
            QuestHudAutoQuest.SetEnabled(false);
            var champion = new GameObject("LockedProofAutoQuestChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
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
            GameObject marker = GameObject.Find(
                ProofOfWorthIds.SkyCastleMarkerId + "_TEMPORARY");
            Assert.That(marker, Is.Not.Null);
            controller.TeleportTo(marker.transform.position);
            NpcConversationView conversation = Object.FindObjectOfType<NpcConversationView>();
            if (conversation != null && conversation.IsVisible)
            {
                conversation.Collapse();
            }

            controller.SetControlLocked(true);
            ProofOfWorthState before = director.State;

            QuestHudAutoQuest.SetEnabled(true);
            yield return null;

            Assert.That(director.State, Is.SameAs(before));
        }

        [UnityTest]
        public IEnumerator AutoQuestOnWalksToAndCompletesOneArrivalWithoutWorldClicks()
        {
            CreateGround();
            QuestHudAutoQuest.SetEnabled(true);
            var champion = new GameObject("AutoQuestChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionController>();
            Vector3 start = champion.transform.position;

            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Stonehold);
            Assert.AreEqual(ProofOfWorthPhase.OmenOffered, director.State.Phase);
            yield return WaitForTravelReady(director, ProofOfWorthPhase.OmenArena, 22f);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (director.State.Phase == ProofOfWorthPhase.OmenArena &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.Greater((int)director.State.Phase, (int)ProofOfWorthPhase.OmenArena);
            Assert.Greater(HorizontalDistance(start, champion.transform.position), 0.1f);
        }

        [UnityTest]
        public IEnumerator AutoQuestTraversesTheAuthoredCrownlandsRoadToTheGuardianTrial()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            Assert.That(
                catalog.TryResolveFirstSessionRealm(
                    RealmId.Crownlands,
                    out GameObject prefab),
                Is.True);
            GameObject realm = Object.Instantiate(prefab, _root.transform);
            FirstSessionAuthoredRealmRoute route =
                realm.GetComponent<FirstSessionAuthoredRealmRoute>();

            QuestHudAutoQuest.SetEnabled(true);
            var champion = new GameObject("AuthoredRouteAutoQuestChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.transform.position = route.PlayerSpawn.position + Vector3.up * 1.05f;
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionController>();

            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenOffered));
            yield return WaitForTravelReady(director, ProofOfWorthPhase.OmenArena, 22f);

            float deadline = Time.realtimeSinceStartup + 12f;
            while (director.State.Phase == ProofOfWorthPhase.OmenArena &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                (int)director.State.Phase,
                Is.GreaterThan((int)ProofOfWorthPhase.OmenArena),
                $"phase={director.State.Phase} player={champion.transform.position} " +
                $"guardian={route.GuardianTrial.position}");
            Assert.That(
                HorizontalDistance(champion.transform.position, route.GuardianTrial.position),
                Is.LessThanOrEqualTo(3.2f));
            Assert.That(Mathf.Abs(champion.transform.position.x - route.transform.position.x),
                Is.LessThanOrEqualTo(6f));
        }

        [UnityTest]
        public IEnumerator AutoQuestOnStopsFollowWhileCombatIsActive()
        {
            CreateGround();
            QuestHudAutoQuest.SetEnabled(true);
            var champion = new GameObject("AutoQuestChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Eldergrove);
            Assert.AreEqual(ProofOfWorthPhase.OmenOffered, director.State.Phase);
            yield return WaitForTravelReady(
                director,
                ProofOfWorthPhase.OmenArena,
                22f);

            var guardian = new GameObject("AutoQuestGuardian");
            guardian.transform.SetParent(_root.transform, false);
            BossDummyAI guardianCombat = guardian.AddComponent<BossDummyAI>();
            guardianCombat.ConfigureRealmContext(RealmId.Eldergrove);
            Assert.That(guardianCombat.isActiveAndEnabled, Is.True,
                "The combat-pause fixture requires an enabled guardian.");
            Assert.That(QuestHudAutoQuest.CanDriveInCurrentContext(), Is.False,
                "A configured guardian must synchronously pause auto-quest movement.");
            Assert.That(Object.FindObjectsOfType<ChampionController>().Length, Is.EqualTo(1),
                "The combat-pause fixture must not inherit a champion from another scene test.");
            float maximumRequestedInput = 0f;
            controller.MovementApplied += receipt =>
            {
                maximumRequestedInput = Mathf.Max(
                    maximumRequestedInput,
                    receipt.RequestedInput.magnitude);
            };

            // Unity's native CharacterController can perform a one-time
            // depenetration on its first Move in the inherited runner scene.
            // Settle that motor initialization before measuring position. The
            // requested-input assertion includes this frame and therefore still
            // detects an auto-follow race from the guardian's first frame.
            yield return null;
            Assert.That(maximumRequestedInput, Is.LessThan(0.01f),
                "Combat must suppress auto-follow from the guardian's first frame.");
            Vector3 pausedAt = champion.transform.position;

            yield return new WaitForSeconds(0.25f);

            Assert.AreEqual(ProofOfWorthPhase.OmenArena, director.State.Phase);
            Assert.That(maximumRequestedInput, Is.LessThan(0.01f),
                "Combat must never feed auto-follow input into the champion motor.");
            Assert.Less(
                HorizontalDistance(pausedAt, champion.transform.position),
                0.01f,
                "Combat pause lost authority. guardianActive=" +
                guardianCombat.isActiveAndEnabled +
                ", autoDrive=" + QuestHudAutoQuest.CanDriveInCurrentContext() +
                ", requestedInput=" + controller.LastMovementReceipt.RequestedInput +
                ", activeScene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ".");
        }

        [UnityTest]
        public IEnumerator AutoQuestOffLeavesOfferAndChampionUnderManualControl()
        {
            CreateGround();
            var champion = new GameObject("ManualChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            Vector3 start = champion.transform.position;

            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            NpcConversationView conversation = Object.FindObjectOfType<NpcConversationView>();
            Assert.That(conversation, Is.Not.Null);
            Assert.That(conversation.IsVisible, Is.True);
            conversation.Collapse();
            controller.SetExternalMoveInput(Vector2.up);

            yield return null;
            Vector3 afterFirstFrame = champion.transform.position;

            yield return new WaitForSeconds(0.25f);

            Assert.IsTrue(director.State.IsOmenOffered);
            Assert.Greater(HorizontalDistance(start, champion.transform.position), 0.1f);
            Assert.Greater(HorizontalDistance(afterFirstFrame, champion.transform.position), 0.1f);
        }

        [UnityTest]
        public IEnumerator AutoQuestOnAdvancesOnePostLordshipTwoPointFiveDTeachingStep()
        {
            Directory.CreateDirectory(_saveRoot);
            ISaveGameService save = CreateSaveService(_saveRoot);
            save.CreateNewSave(RealmId.Crownlands);
            Assert.That(
                MvpLoopSaveAuthority.TryCommit(
                    save,
                    new MvpLoopCommitRequest(
                        Guid.NewGuid().ToString("N"),
                        RealmId.Crownlands,
                        ClassFamily.Mage,
                        true,
                        ProofOfWorthIds.CrownlandsVariantId,
                        string.Empty,
                        0)).Persisted,
                Is.True);

            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            QuestHudOverlay hud = QuestHudOverlay.Mount(_root.transform);
            KingdomTeachingDirector director =
                _root.AddComponent<KingdomTeachingDirector>();
            string requestedInteraction = string.Empty;
            KingdomTeachingInteraction.InteractionRequested +=
                interaction => requestedInteraction = interaction;
            QuestHudAutoQuest.SetEnabled(false);
            director.EnsureReady(save, hud, catalog);
            Assert.That(director.State.IsAvailable, Is.True);
            Assert.That(director.State.ProgressValue, Is.Zero);
            Assert.That(director.State.CurrentStep, Is.SameAs(catalog.Steps[0]));
            Assert.That(requestedInteraction, Is.Empty);

            QuestHudAutoQuest.SetEnabled(true);
            director.Refresh();
            yield return null;

            Assert.That(director.State.ProgressValue, Is.EqualTo(1));
            Assert.That(director.State.CurrentStep, Is.SameAs(catalog.Steps[1]));
            Assert.That(director.Hud.Model.Surface, Is.EqualTo(QuestHudSurface.Kingdom25D));
            Assert.That(director.Hud.Model.StepId, Is.EqualTo(catalog.Steps[1].Id));
            Assert.That(requestedInteraction, Is.EqualTo(catalog.Steps[1].Interaction));
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "MainQuestAutoFollowPlayModeGround";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
        }

        private static IEnumerator WaitForTravelReady(
            ProofOfWorthDirector director,
            ProofOfWorthPhase phase,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (((int)director.State.Phase < (int)phase || ConversationIsVisible()) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(director.State.Phase, Is.EqualTo(phase));
            Assert.That(ConversationIsVisible(), Is.False);
        }

        private static bool ConversationIsVisible()
        {
            NpcConversationView conversation = Object.FindObjectOfType<NpcConversationView>();
            return conversation != null && conversation.IsVisible;
        }

        private static ISaveGameService CreateSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (ISaveGameService)constructor.Invoke(new object[] { root });
        }
    }
}
