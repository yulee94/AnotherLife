using AL.ChampionMode;
using AL.ChampionMode.Tutorial;
using AL.Narrative.Nvs01.Contracts;
using NUnit.Framework;
using UnityEngine;

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
                sprintTaught: false);
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
                sprintTaught: false);
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
            FirstWorldEntryTutorialState state = DriveToComplete(sprint: true);
            Assert.AreEqual(FirstWorldEntryTutorialStep.Complete, state.Step);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.OmenOffered, state.TeachingBeat);
            Assert.AreEqual(1, state.CompletionEventCount);
            Assert.AreEqual(1, state.OmenOfferCount);
            Assert.IsTrue(state.IsOmenOffered);
            Assert.IsFalse(state.OmenAccepted);
            Assert.AreEqual("OMEN_1", state.ForegroundQuestId);
            Assert.AreEqual("OFFERED", state.ForegroundQuestState);
            Assert.AreEqual("OBJ_OMEN_1_TALK", state.ForegroundObjectiveId);
            Assert.IsTrue(state.SprintTaught);
        }

        [Test]
        public void DuplicateEventsAreIdempotent()
        {
            FirstWorldEntryTutorialState moveReady = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                FirstWorldEntryTutorialPlanner.CreateInitial(),
                FirstWorldEntryTeachingBeat.Move,
                sprintTaught: false);
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
            FirstWorldEntryTutorialState complete = DriveToComplete(sprint: false);
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
            Assert.IsTrue(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.MovePrompt));
            Assert.IsTrue(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.InteractPrompt));
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
        public void DirectorTeachesLookMoveInteractAttackThenOffersOmen()
        {
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(null);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.CameraLook, director.State.TeachingBeat);

            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Move, director.State.TeachingBeat);
            Assert.AreEqual(FirstWorldEntryTutorialStep.Move, director.State.Step);

            director.ApplyMoveForTests(FirstWorldEntryTutorialEvidence.MoveThreshold, sprintHeld: true);
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
            Assert.IsTrue(director.State.SprintTaught);

            Assert.NotNull(director.transform.Find("TutorialCanvas_TEMPORARY/" + FirstWorldEntryTutorialDirector.OfferPlateName));
        }

        [Test]
        public void EvidenceThresholdsRejectNoise()
        {
            Assert.IsFalse(FirstWorldEntryTutorialEvidence.IsLookAccepted(0.5f));
            Assert.IsFalse(FirstWorldEntryTutorialEvidence.IsMoveAccepted(0.05f));
            Assert.IsTrue(FirstWorldEntryTutorialEvidence.IsLookAccepted(12f));
            Assert.IsTrue(FirstWorldEntryTutorialEvidence.IsMoveAccepted(0.35f));
        }

        private static FirstWorldEntryTutorialState DriveToComplete(bool sprint)
        {
            FirstWorldEntryTutorialState state = FirstWorldEntryTutorialPlanner.CreateInitial();
            state = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                state,
                FirstWorldEntryTeachingBeat.Move,
                sprintTaught: false);
            FirstWorldEntryTutorialTransition moved = FirstWorldEntryTutorialPlanner.Apply(
                state,
                FirstWorldEntryEvidenceKind.MovementConfirmed);
            Assert.AreEqual(FirstWorldEntryTransitionStatus.Applied, moved.Status);
            Assert.AreEqual(FirstWorldEntryTutorialIds.MovementConfirmedEventId, moved.ConfirmedEventId);
            state = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                moved.State,
                FirstWorldEntryTeachingBeat.BasicAttack,
                sprintTaught: sprint);
            FirstWorldEntryTutorialTransition attack = FirstWorldEntryTutorialPlanner.Apply(
                state,
                FirstWorldEntryEvidenceKind.BasicAttackConfirmed);
            Assert.AreEqual(FirstWorldEntryTransitionStatus.Applied, attack.Status);
            Assert.AreEqual(FirstWorldEntryTutorialIds.CompletedEventId, attack.CompletionEventId);
            return attack.State;
        }
    }
}
