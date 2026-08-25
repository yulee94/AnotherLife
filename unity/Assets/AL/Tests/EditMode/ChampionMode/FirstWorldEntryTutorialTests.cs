using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Interaction;
using AL.ChampionMode.Tutorial;
using AL.Narrative.Nvs01.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class FirstWorldEntryTutorialTests
    {
        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            FirstWorldEntryTutorialDirector[] leftovers =
                Object.FindObjectsOfType<FirstWorldEntryTutorialDirector>();
            for (int i = 0; i < leftovers.Length; i++)
            {
                Object.DestroyImmediate(leftovers[i].gameObject);
            }

            FirstWorldEntryTutorialDirector.ResetForTests();
        }

        [Test]
        public void PinsSpineIdsWithoutInventingAQuest()
        {
            Assert.AreEqual("TUTORIAL_FIRST_WORLD_ENTRY", FirstWorldEntryTutorialIds.TutorialId);
            Assert.AreEqual(
                "OBJ_TUTORIAL_FIRST_WORLD_ENTRY_MOVE",
                FirstWorldEntryTutorialIds.MoveObjectiveId);
            Assert.AreEqual(
                "OBJ_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK",
                FirstWorldEntryTutorialIds.BasicAttackObjectiveId);
            Assert.AreEqual(
                "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_MOVEMENT_CONFIRMED",
                FirstWorldEntryTutorialIds.MovementConfirmedEventId);
            Assert.AreEqual(
                "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK_CONFIRMED",
                FirstWorldEntryTutorialIds.BasicAttackConfirmedEventId);
            Assert.AreEqual(
                "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_COMPLETED",
                FirstWorldEntryTutorialIds.CompletedEventId);
            Assert.AreEqual(Nvs01CatalogContract.QuestId, FirstWorldEntryTutorialIds.OmenQuestId);
            Assert.AreEqual("OFFERED", FirstWorldEntryTutorialIds.OmenOfferedState);
            Assert.AreEqual("OBJ_OMEN_1_TALK", FirstWorldEntryTutorialIds.OmenTalkObjectiveId);
            Assert.AreEqual("SELECT_VALERIUS", FirstWorldEntryTutorialIds.OmenOfferAction);
            Assert.AreNotEqual(FirstWorldEntryTutorialIds.TutorialId, Nvs01CatalogContract.QuestId);
        }

        [Test]
        public void TeachingBeatsDoNotAddObjectiveIds()
        {
            FirstWorldEntryTutorialState initial = FirstWorldEntryTutorialPlanner.CreateInitial();
            Assert.AreEqual(FirstWorldEntryTeachingBeat.CameraLook, initial.TeachingBeat);
            Assert.AreEqual(FirstWorldEntryTutorialIds.MoveObjectiveId, initial.ActiveTutorialObjectiveId);

            FirstWorldEntryTutorialState afterLook = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                initial,
                FirstWorldEntryTeachingBeat.Move,
                blockTaught: false);
            Assert.AreEqual(FirstWorldEntryTutorialStep.Move, afterLook.Step);
            Assert.AreEqual(FirstWorldEntryTutorialIds.MoveObjectiveId, afterLook.ActiveTutorialObjectiveId);
        }

        [Test]
        public void OutOfOrderAttackIsRejected()
        {
            FirstWorldEntryTutorialState initial = FirstWorldEntryTutorialPlanner.CreateInitial();
            FirstWorldEntryTutorialState readyToMove = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                initial,
                FirstWorldEntryTeachingBeat.Move,
                blockTaught: false);
            FirstWorldEntryTutorialTransition attack = FirstWorldEntryTutorialPlanner.Apply(
                readyToMove,
                FirstWorldEntryEvidenceKind.BasicAttackConfirmed);
            Assert.AreEqual(FirstWorldEntryTransitionStatus.Rejected, attack.Status);
            Assert.AreEqual(FirstWorldEntryTutorialStep.Move, attack.State.Step);
            Assert.IsFalse(attack.State.IsOmenOffered);
            Assert.IsEmpty(attack.CompletionEventId);
        }

        [Test]
        public void OrderedMoveThenAttackOffersOmenOnceAndDoesNotAccept()
        {
            FirstWorldEntryTutorialState state = DriveToComplete(block: true);
            Assert.AreEqual(FirstWorldEntryTutorialStep.Complete, state.Step);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.OmenOffered, state.TeachingBeat);
            Assert.AreEqual(1, state.CompletionEventCount);
            Assert.AreEqual(1, state.OmenOfferCount);
            Assert.IsTrue(state.IsOmenOffered);
            Assert.IsFalse(state.OmenAccepted);
            Assert.AreEqual("OMEN_1", state.ForegroundQuestId);
            Assert.AreEqual("OFFERED", state.ForegroundQuestState);
            Assert.AreEqual("OBJ_OMEN_1_TALK", state.ForegroundObjectiveId);
            Assert.IsTrue(state.BlockTaught);
        }

        [Test]
        public void DuplicateEventsAreIdempotent()
        {
            FirstWorldEntryTutorialState moveReady = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                FirstWorldEntryTutorialPlanner.CreateInitial(),
                FirstWorldEntryTeachingBeat.Move,
                blockTaught: false);
            FirstWorldEntryTutorialTransition first = FirstWorldEntryTutorialPlanner.Apply(
                moveReady,
                FirstWorldEntryEvidenceKind.MovementConfirmed);
            FirstWorldEntryTutorialTransition again = FirstWorldEntryTutorialPlanner.Apply(
                first.State,
                FirstWorldEntryEvidenceKind.MovementConfirmed);
            Assert.AreEqual(FirstWorldEntryTransitionStatus.DuplicateIgnored, again.Status);
            Assert.AreEqual(1, again.State.MovementConfirmationCount);
        }

        [Test]
        public void FollowDoesNotAcceptOmenAndHasNoTargetYet()
        {
            FirstWorldEntryTutorialState complete = DriveToComplete(block: false);
            Assert.AreEqual(
                FirstWorldEntryTutorialIds.FollowNoTargetResultId,
                FirstWorldEntryTutorialPlanner.Follow(complete, targetAvailable: false));
            FirstWorldEntryTutorialState stillOffered = FirstWorldEntryTutorialPlanner.RejectAccept(complete);
            Assert.IsFalse(stillOffered.OmenAccepted);
            Assert.IsTrue(stillOffered.IsOmenOffered);
        }

        [Test]
        public void TutorialCopyIsTemporaryAndOmenCopyMatchesCatalog()
        {
            Assert.IsTrue(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.Title));
            Assert.IsTrue(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.CameraPrompt));
            Assert.That(
                FirstWorldEntryTutorialCopy.CameraPrompt,
                Does.Contain("Hold the right mouse button and drag"));
            Assert.That(FirstWorldEntryTutorialCopy.CameraPrompt, Does.Contain("right stick"));
            Assert.That(FirstWorldEntryTutorialCopy.CameraPrompt, Does.Not.Contain("Move the mouse"));
            Assert.IsTrue(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.MovePrompt));
            Assert.That(FirstWorldEntryTutorialCopy.MovePrompt, Does.Contain("Shift to block"));
            Assert.That(FirstWorldEntryTutorialCopy.MovePrompt, Does.Not.Contain("sprint"));
            Assert.IsTrue(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.InteractPrompt));
            Assert.That(FirstWorldEntryTutorialCopy.InteractPrompt, Does.Contain("press F"));
            Assert.That(FirstWorldEntryTutorialCopy.InteractPrompt, Does.Not.Contain("Enter"));
            Assert.IsTrue(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.AttackPrompt));
            Assert.AreEqual("The First Signal", FirstWorldEntryTutorialCopy.OmenOfferTitle);
            Assert.AreEqual("Speak with Captain Valerius.", FirstWorldEntryTutorialCopy.OmenTalk);
            Assert.That(
                FirstWorldEntryTutorialCopy.OmenOffer,
                Does.Contain("Veil Watch"));
        }

        [Test]
        public void FirstSessionAttachesDirectorAndEncounterHarnessDoesNot()
        {
            Assert.IsTrue(FirstSessionChampionStart.ShouldRunFirstWorldEntryTutorial);
            FirstWorldEntryTutorialDirector attached =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(null);
            Assert.NotNull(attached);
            Assert.AreEqual(
                FirstWorldEntryTutorialDirector.OverlayRootName,
                attached.gameObject.name);
            Assert.That(attached.gameObject.name, Does.Contain("TEMPORARY"));
            Assert.IsFalse(attached.OpenedKingdom);

            Object.DestroyImmediate(attached.gameObject);
            FirstSessionChampionStart.EnableEncounterHarness();
            Assert.IsFalse(FirstSessionChampionStart.ShouldRunFirstWorldEntryTutorial);
            Assert.IsNull(FirstWorldEntryTutorialDirector.AttachIfNeeded(null));
        }

        [Test]
        public void TutorialPromptClearsCombatHotbarEnvelope()
        {
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(null);
            Transform promptTransform = director.transform.Find(
                "TutorialCanvas_TEMPORARY/" + FirstWorldEntryTutorialDirector.PromptName);
            Assert.NotNull(promptTransform);

            RectTransform prompt = promptTransform.GetComponent<RectTransform>();
            Assert.NotNull(prompt);
            CanvasScaler tutorialScaler = promptTransform.GetComponentInParent<CanvasScaler>();
            Assert.NotNull(tutorialScaler);
            Assert.That(
                tutorialScaler.matchWidthOrHeight,
                Is.EqualTo(0.5f),
                "Tutorial and combat HUD must share one scaling contract at non-16:9 resolutions.");
            float promptBottom = prompt.anchoredPosition.y -
                                 prompt.sizeDelta.y * prompt.pivot.y;
            float hotbarTop = ChampionArenaSceneController.CombatHotbarBottomOffset +
                              ChampionArenaSceneController.CombatHotbarHeight;

            Assert.That(
                promptBottom - hotbarTop,
                Is.GreaterThanOrEqualTo(
                    FirstWorldEntryTutorialDirector.PromptHotbarClearance),
                "First-session guidance must remain readable above the combat hotbar.");
        }

        [Test]
        public void DirectorTeachesLookMoveInteractAttackThenOffersOmen()
        {
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(null);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.CameraLook, director.State.TeachingBeat);

            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Move, director.State.TeachingBeat);
            Assert.AreEqual(FirstWorldEntryTutorialStep.Move, director.State.Step);

            director.ApplyMoveForTests(FirstWorldEntryTutorialEvidence.MoveThreshold, blockHeld: true);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Interact, director.State.TeachingBeat);
            Assert.AreEqual(FirstWorldEntryTutorialStep.BasicAttack, director.State.Step);
            Assert.AreEqual(
                FirstWorldEntryTutorialIds.BasicAttackObjectiveId,
                director.State.ActiveTutorialObjectiveId);

            director.ApplyAttackForTests();
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Interact, director.State.TeachingBeat);
            Assert.IsFalse(director.State.IsComplete);

            director.ApplyInteractForTests();
            Assert.AreEqual(FirstWorldEntryTeachingBeat.BasicAttack, director.State.TeachingBeat);

            director.ApplyAttackForTests();
            Assert.IsTrue(director.State.IsComplete);
            Assert.IsTrue(director.State.IsOmenOffered);
            Assert.IsFalse(director.State.OmenAccepted);
            Assert.AreEqual("OMEN_1", director.State.ForegroundQuestId);
            Assert.AreEqual(
                FirstWorldEntryTutorialIds.FollowNoTargetResultId,
                director.FollowActiveObjective(false));
            director.AttemptAcceptOmen();
            Assert.AreEqual(1, director.AcceptAttempts);
            Assert.IsFalse(director.State.OmenAccepted);
            Assert.IsTrue(director.State.IsOmenOffered);
            Assert.IsFalse(director.OpenedKingdom);
            Assert.IsTrue(director.State.BlockTaught);

            Assert.NotNull(director.transform.Find("TutorialCanvas_TEMPORARY/" + FirstWorldEntryTutorialDirector.OfferPlateName));
        }

        [Test]
        public void OnlyAcceptedRealmGuideInteractionAdvancesInteractBeat()
        {
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(null);
            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                blockHeld: false);
            Assert.AreEqual(
                FirstWorldEntryTeachingBeat.Interact,
                director.State.TeachingBeat);

            director.ApplyWorldInteractionForTests(new WorldInteractionResult(
                true,
                FirstSessionWorldInteractables.CovenantSiteCatalogId,
                WorldInteractionKind.Use,
                WorldInteractionPromptCopy.CovenantObjectiveText));
            Assert.AreEqual(
                FirstWorldEntryTeachingBeat.Interact,
                director.State.TeachingBeat);

            director.ApplyWorldInteractionForTests(new WorldInteractionResult(
                false,
                FirstSessionWorldInteractables.GuideCatalogId,
                WorldInteractionKind.Talk,
                string.Empty));
            Assert.AreEqual(
                FirstWorldEntryTeachingBeat.Interact,
                director.State.TeachingBeat);

            director.ApplyWorldInteractionForTests(new WorldInteractionResult(
                true,
                FirstSessionWorldInteractables.GuideCatalogId,
                WorldInteractionKind.Talk,
                WorldInteractionPromptCopy.GuideObjectiveText));
            Assert.AreEqual(
                FirstWorldEntryTeachingBeat.BasicAttack,
                director.State.TeachingBeat);
        }

        [Test]
        public void EvidenceThresholdsRejectNoise()
        {
            Assert.IsFalse(FirstWorldEntryTutorialEvidence.IsLookAccepted(0.5f));
            Assert.IsTrue(FirstWorldEntryTutorialEvidence.IsLookAccepted(12f));

            ChampionMovementReceipt accepted = MovementReceipt(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                FirstWorldEntryTutorialEvidence.HorizontalDisplacementThreshold,
                grounded: true);
            Assert.IsTrue(FirstWorldEntryTutorialEvidence.IsMoveAccepted(accepted));
            Assert.IsFalse(FirstWorldEntryTutorialEvidence.IsMoveAccepted(
                MovementReceipt(0.05f, 0.2f, grounded: true)));
            Assert.IsFalse(FirstWorldEntryTutorialEvidence.IsMoveAccepted(
                MovementReceipt(1f, 0f, grounded: true)));
            Assert.IsFalse(FirstWorldEntryTutorialEvidence.IsMoveAccepted(
                MovementReceipt(1f, 0.2f, grounded: false)));
        }

        [Test]
        public void DirectorRejectsMoveIntentWithoutGroundedDisplacement()
        {
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(null);
            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);

            director.ApplyRejectedMoveForTests(1f, 0f, grounded: true);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Move, director.State.TeachingBeat);
            Assert.AreEqual(0, director.State.MovementConfirmationCount);

            director.ApplyRejectedMoveForTests(1f, 0.2f, grounded: false);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Move, director.State.TeachingBeat);
            Assert.AreEqual(0, director.State.MovementConfirmationCount);
        }

        private static ChampionMovementReceipt MovementReceipt(
            float inputMagnitude,
            float horizontalDisplacement,
            bool grounded)
        {
            return new ChampionMovementReceipt(
                1,
                Vector2.up * inputMagnitude,
                Vector3.forward * horizontalDisplacement,
                grounded,
                grounded,
                grounded ? CollisionFlags.Below : CollisionFlags.None);
        }

        private static FirstWorldEntryTutorialState DriveToComplete(bool block)
        {
            FirstWorldEntryTutorialState state = FirstWorldEntryTutorialPlanner.CreateInitial();
            state = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                state,
                FirstWorldEntryTeachingBeat.Move,
                blockTaught: false);
            FirstWorldEntryTutorialTransition moved = FirstWorldEntryTutorialPlanner.Apply(
                state,
                FirstWorldEntryEvidenceKind.MovementConfirmed);
            Assert.AreEqual(FirstWorldEntryTransitionStatus.Applied, moved.Status);
            Assert.AreEqual(FirstWorldEntryTutorialIds.MovementConfirmedEventId, moved.ConfirmedEventId);
            state = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                moved.State,
                FirstWorldEntryTeachingBeat.BasicAttack,
                blockTaught: block);
            FirstWorldEntryTutorialTransition attack = FirstWorldEntryTutorialPlanner.Apply(
                state,
                FirstWorldEntryEvidenceKind.BasicAttackConfirmed);
            Assert.AreEqual(FirstWorldEntryTransitionStatus.Applied, attack.Status);
            Assert.AreEqual(FirstWorldEntryTutorialIds.CompletedEventId, attack.CompletionEventId);
            return attack.State;
        }
    }
}
