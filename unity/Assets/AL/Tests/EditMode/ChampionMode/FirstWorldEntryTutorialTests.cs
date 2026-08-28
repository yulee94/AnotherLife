using System.IO;
using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Interaction;
using AL.ChampionMode.Skills;
using AL.ChampionMode.Tutorial;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Narrative.Nvs01.Contracts;
using AL.UI.QuestHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class FirstWorldEntryTutorialTests
    {
        private GameObject _fixtureRoot;

        [SetUp]
        public void SetUp()
        {
            ChampionHudCameraGate.Reset();
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            _fixtureRoot = new GameObject("FirstWorldEntryTutorialTests.Root");
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            if (_fixtureRoot != null)
            {
                Object.DestroyImmediate(_fixtureRoot);
                _fixtureRoot = null;
            }

            FirstWorldEntryTutorialDirector.ResetForTests();
            ChampionHudCameraGate.Reset();
        }

        [Test]
        public void TearDownPreservesUnregisteredForeignController()
        {
            var foreign = new GameObject("ForeignPreExistingTutorialController");
            foreign.AddComponent<CharacterController>();
            foreign.AddComponent<ChampionController>();
            try
            {
                TearDown();

                Assert.That(foreign != null, Is.True);
            }
            finally
            {
                if (foreign != null)
                {
                    Object.DestroyImmediate(foreign);
                }
            }
        }

        [Test]
        public void MissingOwnerRejectsSyntheticTutorialEvidence()
        {
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(_fixtureRoot.transform);
            FirstWorldEntryTutorialState before = director.State;

            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                blockHeld: false);
            director.ApplyInteractForTests();
            director.ApplyAttackForTests();

            Assert.That(director.State, Is.SameAs(before));
        }

        [Test]
        public void ControllerLockBlocksAutoTutorialEvidenceIncludingBasicAttack()
        {
            QuestHudAutoQuest.ResetForTests();
            QuestHudAutoQuest.SetEnabled(true);
            ChampionController decoyController = CreateTutorialController(
                "UnlockedAdditiveTutorialDecoy");
            ChampionController controller = CreateTutorialController(
                "LockedTutorialEvidenceChampion");
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    _fixtureRoot.transform,
                    controller);

            controller.SetControlLocked(true);
            FirstWorldEntryTutorialState initial = director.State;
            director.AdvanceAutoQuestForTests();
            Assert.That(director.State, Is.SameAs(initial));
            Assert.That(decoyController.EditorBasicAttackSequence, Is.Zero);

            controller.SetControlLocked(false);
            director.ApplyLookForTests(
                FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                blockHeld: false);
            director.ApplyInteractForTests();
            Assert.That(director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.BasicAttack));

            controller.SetControlLocked(true);
            int attackSequence = controller.EditorBasicAttackSequence;
            FirstWorldEntryTutorialState attackBeat = director.State;
            director.AdvanceAutoQuestForTests();
            Assert.That(director.State, Is.SameAs(attackBeat));
            Assert.That(controller.EditorBasicAttackSequence, Is.EqualTo(attackSequence));

            Assert.That(director.State.IsComplete, Is.False);
            Object.DestroyImmediate(decoyController.gameObject);
            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void AutoQuestOffPreservesEveryTutorialBeat()
        {
            QuestHudAutoQuest.ResetForTests();
            ChampionController controller = CreateTutorialController(
                "AutoQuestOffTutorialOwner");
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    _fixtureRoot.transform,
                    controller);
            FirstWorldEntryTeachingBeat[] beats =
            {
                FirstWorldEntryTeachingBeat.CameraLook,
                FirstWorldEntryTeachingBeat.Move,
                FirstWorldEntryTeachingBeat.Interact,
                FirstWorldEntryTeachingBeat.BasicAttack
            };

            for (int i = 0; i < beats.Length; i++)
            {
                Assert.That(director.State.TeachingBeat, Is.EqualTo(beats[i]));
                FirstWorldEntryTutorialState before = director.State;
                director.AdvanceAutoQuestForTests();
                Assert.That(director.State, Is.SameAs(before));

                switch (beats[i])
                {
                    case FirstWorldEntryTeachingBeat.CameraLook:
                        director.ApplyLookForTests(
                            FirstWorldEntryTutorialEvidence.LookThreshold);
                        break;
                    case FirstWorldEntryTeachingBeat.Move:
                        director.ApplyMoveForTests(
                            FirstWorldEntryTutorialEvidence.MoveThreshold,
                            blockHeld: false);
                        break;
                    case FirstWorldEntryTeachingBeat.Interact:
                        director.ApplyInteractForTests();
                        break;
                }
            }

            Assert.That(director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.BasicAttack));
            Assert.That(director.State.IsComplete, Is.False);
            director.ApplyAttackForTests();
            Assert.That(director.State.IsComplete, Is.True);
        }

        [Test]
        public void ModalSuppressionPreservesEveryAutoQuestTutorialBeat()
        {
            QuestHudAutoQuest.ResetForTests();
            try
            {
                QuestHudAutoQuest.SetEnabled(true);
                ChampionController controller = CreateTutorialController(
                    "ModalTutorialOwner");
                FirstWorldEntryTutorialDirector director =
                    FirstWorldEntryTutorialDirector.AttachIfNeeded(
                        _fixtureRoot.transform,
                        controller);
                FirstWorldEntryTeachingBeat[] beats =
                {
                    FirstWorldEntryTeachingBeat.CameraLook,
                    FirstWorldEntryTeachingBeat.Move,
                    FirstWorldEntryTeachingBeat.Interact,
                    FirstWorldEntryTeachingBeat.BasicAttack
                };

                for (int i = 0; i < beats.Length; i++)
                {
                    Assert.That(director.State.TeachingBeat, Is.EqualTo(beats[i]));
                    FirstWorldEntryTutorialState before = director.State;
                    using (ChampionHudCameraGate.AcquireCursorOwnership(
                               "tutorial-auto-quest-modal-test"))
                    {
                        director.AdvanceAutoQuestForTests();
                    }

                    Assert.That(director.State, Is.SameAs(before),
                        "Modal suppression must not synthesize tutorial evidence.");
                    switch (beats[i])
                    {
                        case FirstWorldEntryTeachingBeat.CameraLook:
                            director.ApplyLookForTests(
                                FirstWorldEntryTutorialEvidence.LookThreshold);
                            break;
                        case FirstWorldEntryTeachingBeat.Move:
                            director.ApplyMoveForTests(
                                FirstWorldEntryTutorialEvidence.MoveThreshold,
                                blockHeld: false);
                            break;
                        case FirstWorldEntryTeachingBeat.Interact:
                            director.ApplyInteractForTests();
                            break;
                        case FirstWorldEntryTeachingBeat.BasicAttack:
                            director.ApplyAttackForTests();
                            break;
                    }
                }

                Assert.That(director.State.IsComplete, Is.True);
            }
            finally
            {
                QuestHudAutoQuest.ResetForTests();
            }
        }

        [Test]
        public void AutoQuestCameraTeachingWaitsForObservedLookEvidence()
        {
            QuestHudAutoQuest.ResetForTests();
            try
            {
                QuestHudAutoQuest.SetEnabled(true);
                ChampionController controller = CreateTutorialController(
                    "AutoQuestTutorialOwner");
                FirstWorldEntryTutorialDirector director =
                    FirstWorldEntryTutorialDirector.AttachIfNeeded(
                        _fixtureRoot.transform,
                        controller);

                FirstWorldEntryTutorialState before = director.State;
                director.AdvanceAutoQuestForTests();

                Assert.That(director.State, Is.SameAs(before));
                Assert.That(
                    director.State.TeachingBeat,
                    Is.EqualTo(FirstWorldEntryTeachingBeat.CameraLook));
                Assert.That(director.State.IsComplete, Is.False);
                Assert.That(QuestHudAutoQuest.Enabled, Is.True);
            }
            finally
            {
                QuestHudAutoQuest.ResetForTests();
            }
        }

        [Test]
        public void AutoQuestMoveTeachingWaitsForControllerMovementReceipt()
        {
            QuestHudAutoQuest.ResetForTests();
            try
            {
                QuestHudAutoQuest.SetEnabled(true);
                ChampionController controller = CreateTutorialController(
                    "AutoQuestMoveReceiptOwner");
                FirstWorldEntryTutorialDirector director =
                    FirstWorldEntryTutorialDirector.AttachIfNeeded(
                        _fixtureRoot.transform,
                        controller);
                director.ApplyLookForTests(
                    FirstWorldEntryTutorialEvidence.LookThreshold);
                Assert.That(
                    director.State.TeachingBeat,
                    Is.EqualTo(FirstWorldEntryTeachingBeat.Move));

                FirstWorldEntryTutorialState before = director.State;
                uint receiptSequence = controller.LastMovementReceipt.Sequence;
                director.AdvanceAutoQuestForTests();

                Assert.That(director.State, Is.SameAs(before));
                Assert.That(director.State.MovementConfirmationCount, Is.Zero);
                Assert.That(
                    controller.LastMovementReceipt.Sequence,
                    Is.EqualTo(receiptSequence));
                System.Reflection.FieldInfo externalMoveInput =
                    typeof(ChampionController).GetField(
                        "_externalMoveInput",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                Assert.That(externalMoveInput, Is.Not.Null);
                Assert.That(
                    ((Vector2)externalMoveInput.GetValue(controller)).sqrMagnitude,
                    Is.GreaterThan(0.9f));

                director.ApplyMoveForTests(
                    FirstWorldEntryTutorialEvidence.MoveThreshold,
                    blockHeld: false);

                Assert.That(
                    director.State.TeachingBeat,
                    Is.EqualTo(FirstWorldEntryTeachingBeat.Interact));
                Assert.That(
                    ((Vector2)externalMoveInput.GetValue(controller)).sqrMagnitude,
                    Is.LessThan(0.0001f),
                    "An accepted owner movement receipt must retire the Auto Quest request.");
            }
            finally
            {
                QuestHudAutoQuest.ResetForTests();
            }
        }

        [Test]
        public void DisablingTutorialDirectorReleasesOwnedAutoQuestMovement()
        {
            QuestHudAutoQuest.ResetForTests();
            try
            {
                QuestHudAutoQuest.SetEnabled(true);
                ChampionController controller = CreateTutorialController(
                    "DisabledAutoQuestMoveOwner");
                FirstWorldEntryTutorialDirector director =
                    FirstWorldEntryTutorialDirector.AttachIfNeeded(
                        _fixtureRoot.transform,
                        controller);
                director.ApplyLookForTests(
                    FirstWorldEntryTutorialEvidence.LookThreshold);
                director.AdvanceAutoQuestForTests();
                System.Reflection.FieldInfo externalMoveInput =
                    typeof(ChampionController).GetField(
                        "_externalMoveInput",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                Assert.That(externalMoveInput, Is.Not.Null);
                Assert.That(
                    ((Vector2)externalMoveInput.GetValue(controller)).sqrMagnitude,
                    Is.GreaterThan(0.9f));

                director.enabled = false;
                System.Reflection.MethodInfo onDisable =
                    typeof(FirstWorldEntryTutorialDirector).GetMethod(
                        "OnDisable",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                Assert.That(onDisable, Is.Not.Null);
                onDisable.Invoke(director, null);

                Assert.That(
                    ((Vector2)externalMoveInput.GetValue(controller)).sqrMagnitude,
                    Is.LessThan(0.0001f));
            }
            finally
            {
                QuestHudAutoQuest.ResetForTests();
            }
        }

        [Test]
        public void TurningAutoQuestOffReleasesOwnedTutorialMovementOnUpdate()
        {
            QuestHudAutoQuest.ResetForTests();
            try
            {
                QuestHudAutoQuest.SetEnabled(true);
                ChampionController controller = CreateTutorialController(
                    "DisabledAutoQuestToggleMoveOwner");
                FirstWorldEntryTutorialDirector director =
                    FirstWorldEntryTutorialDirector.AttachIfNeeded(
                        _fixtureRoot.transform,
                        controller);
                director.ApplyLookForTests(
                    FirstWorldEntryTutorialEvidence.LookThreshold);
                director.AdvanceAutoQuestForTests();
                System.Reflection.FieldInfo externalMoveInput =
                    typeof(ChampionController).GetField(
                        "_externalMoveInput",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                Assert.That(externalMoveInput, Is.Not.Null);
                Assert.That(
                    ((Vector2)externalMoveInput.GetValue(controller)).sqrMagnitude,
                    Is.GreaterThan(0.9f));

                QuestHudAutoQuest.SetEnabled(false);
                System.Reflection.MethodInfo update =
                    typeof(FirstWorldEntryTutorialDirector).GetMethod(
                        "Update",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                Assert.That(update, Is.Not.Null);
                update.Invoke(director, null);

                Assert.That(
                    ((Vector2)externalMoveInput.GetValue(controller)).sqrMagnitude,
                    Is.LessThan(0.0001f));
            }
            finally
            {
                QuestHudAutoQuest.ResetForTests();
            }
        }

        [Test]
        public void AutoQuestInteractTeachingWaitsForAcceptedWorldInteraction()
        {
            QuestHudAutoQuest.ResetForTests();
            try
            {
                QuestHudAutoQuest.SetEnabled(true);
                ChampionController controller = CreateTutorialController(
                    "AutoQuestInteractionReceiptOwner");
                FirstWorldEntryTutorialDirector director =
                    FirstWorldEntryTutorialDirector.AttachIfNeeded(
                        _fixtureRoot.transform,
                        controller);
                director.ApplyLookForTests(
                    FirstWorldEntryTutorialEvidence.LookThreshold);
                director.ApplyMoveForTests(
                    FirstWorldEntryTutorialEvidence.MoveThreshold,
                    blockHeld: false);
                Assert.That(
                    director.State.TeachingBeat,
                    Is.EqualTo(FirstWorldEntryTeachingBeat.Interact));

                FirstWorldEntryTutorialState before = director.State;
                director.AdvanceAutoQuestForTests();

                Assert.That(director.State, Is.SameAs(before));
                Assert.That(
                    director.State.TeachingBeat,
                    Is.EqualTo(FirstWorldEntryTeachingBeat.Interact));
            }
            finally
            {
                QuestHudAutoQuest.ResetForTests();
            }
        }

        [Test]
        public void NonDurableCompletionDoesNotPublishDurableHandoff()
        {
            ChampionController controller = CreateTutorialController(
                "CompletionHandoffTutorialOwner");
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    _fixtureRoot.transform,
                    controller);
            int completions = 0;
            director.Completed += _ => completions++;

            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(FirstWorldEntryTutorialEvidence.MoveThreshold, false);
            director.ApplyInteractForTests();
            director.ApplyAttackForTests();
            director.ApplyAttackForTests();

            Assert.That(director.State.IsComplete, Is.True);
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void InteractTeachingRejectsForeignOwnerAndWrongInteractionKind()
        {
            ChampionController controller = CreateTutorialController(
                "OwnerBoundInteractionReceiptOwner");
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    _fixtureRoot.transform,
                    controller);
            director.ApplyLookForTests(
                FirstWorldEntryTutorialEvidence.LookThreshold);
            director.ApplyMoveForTests(
                FirstWorldEntryTutorialEvidence.MoveThreshold,
                blockHeld: false);
            Assert.That(
                director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.Interact));

            var sourceObject = new GameObject("OwnerBoundInteractionSource");
            sourceObject.transform.SetParent(_fixtureRoot.transform, false);
            WorldInteractionDirector source =
                sourceObject.AddComponent<WorldInteractionDirector>();
            director.BindWorldInteractionDirector(source);

            var foreignActor = new GameObject("ForeignInteractionActor");
            foreignActor.transform.SetParent(_fixtureRoot.transform, false);
            source.Configure(foreignActor.transform, null, null);
            director.ApplyWorldInteractionForTests(new WorldInteractionResult(
                true,
                FirstSessionWorldInteractables.GuideCatalogId,
                WorldInteractionKind.Talk,
                WorldInteractionPromptCopy.GuideObjectiveText));
            Assert.That(
                director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.Interact));

            source.Configure(controller.transform, null, null);
            director.ApplyWorldInteractionForTests(new WorldInteractionResult(
                true,
                FirstSessionWorldInteractables.GuideCatalogId,
                WorldInteractionKind.Use,
                WorldInteractionPromptCopy.GuideObjectiveText));
            Assert.That(
                director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.Interact));

            director.ApplyWorldInteractionForTests(new WorldInteractionResult(
                true,
                FirstSessionWorldInteractables.GuideCatalogId,
                WorldInteractionKind.Talk,
                WorldInteractionPromptCopy.GuideObjectiveText));
            Assert.That(
                director.State.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.BasicAttack));
        }

        [Test]
        public void SyntheticProgressionIngressIsEditorOnly()
        {
            string tutorialSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/ChampionMode/Tutorial/FirstWorldEntryTutorialDirector.cs"))
                .Replace(System.Environment.NewLine, "\n");
            string proofSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/ChampionMode/Quests/ProofOfWorthDirector.cs"))
                .Replace(System.Environment.NewLine, "\n");

            Assert.That(
                tutorialSource,
                Does.Contain("#if UNITY_EDITOR\n        public void ApplyLookForTests"));
            Assert.That(
                tutorialSource,
                Does.Contain("public void AdvanceAutoQuestForTests()\n" +
                             "        {\n" +
                             "            AdvanceAutoQuest();\n" +
                             "        }\n#endif"));
            Assert.That(
                proofSource,
                Does.Contain("#if UNITY_EDITOR\n        public ProofOfWorthTransition ApplyForTests"));
        }

        [Test]
        public void LiveInteractTeachingUsesTheWorldInteractionBinding()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/ChampionMode/Tutorial/FirstWorldEntryTutorialDirector.cs"));

            Assert.That(
                source,
                Does.Contain("_worldInteractionDirector.Confirmed +="));
            Assert.That(source, Does.Contain("HandleWorldInteractionConfirmed"));
            Assert.That(source, Does.Not.Contain("GameInput.InteractPressed()"));
            Assert.That(source, Does.Not.Contain("GameInput.SubmitPressed()"));
        }

        [Test]
        public void LiveMoveTeachingDoesNotPresentBlockAsSprint()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/ChampionMode/Tutorial/FirstWorldEntryTutorialDirector.cs"));

            Assert.That(source, Does.Not.Contain("ConsiderMove(move.magnitude, GameInput.BlockHeld())"));
            Assert.That(FirstWorldEntryTutorialCopy.MovePrompt, Does.Not.Contain("Shift"));
            Assert.That(FirstWorldEntryTutorialCopy.MovePrompt.ToLowerInvariant(), Does.Not.Contain("sprint"));
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
        public void TutorialCopyIsProductionReadyAndOmenCopyMatchesCatalog()
        {
            Assert.IsFalse(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.Title));
            Assert.IsFalse(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.CameraPrompt));
            Assert.That(FirstWorldEntryTutorialCopy.CameraPrompt, Does.Contain("right stick"));
            Assert.IsFalse(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.MovePrompt));
            Assert.IsFalse(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.InteractPrompt));
            Assert.IsFalse(FirstWorldEntryTutorialCopy.IsTemporary(FirstWorldEntryTutorialCopy.AttackPrompt));
            Assert.AreEqual("Champion's First Steps", FirstWorldEntryTutorialCopy.Title);
            Assert.That(FirstWorldEntryTutorialCopy.InteractPrompt, Does.Contain("[F]"));
            Assert.That(FirstWorldEntryTutorialCopy.InteractPrompt, Does.Contain("Captain Valerius"));
            Assert.That(FirstWorldEntryTutorialCopy.InteractPrompt, Does.Not.Contain("Enter"));
            Assert.That(FirstWorldEntryTutorialCopy.OmenOfferedHint, Does.Not.Contain("SELECT_"));
            Assert.That(FirstWorldEntryTutorialCopy.OmenOfferedHint, Does.Contain("Captain Valerius"));
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
                FirstWorldEntryTutorialDirector.AttachIfNeeded(_fixtureRoot.transform);
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
            ChampionController controller = CreateTutorialController(
                "OrderedTutorialOwner");
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    _fixtureRoot.transform,
                    controller);
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
            ChampionController controller = CreateTutorialController(
                "GuideInteractionTutorialOwner");
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    _fixtureRoot.transform,
                    controller);
            var sourceObject = new GameObject("GuideInteractionTutorialSource");
            sourceObject.transform.SetParent(_fixtureRoot.transform, false);
            WorldInteractionDirector source =
                sourceObject.AddComponent<WorldInteractionDirector>();
            source.Configure(controller.transform, null, null);
            director.BindWorldInteractionDirector(source);
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
            ChampionController controller = CreateTutorialController(
                "RejectedMoveTutorialOwner");
            FirstWorldEntryTutorialDirector director =
                FirstWorldEntryTutorialDirector.AttachIfNeeded(
                    _fixtureRoot.transform,
                    controller);
            director.ApplyLookForTests(FirstWorldEntryTutorialEvidence.LookThreshold);

            director.ApplyRejectedMoveForTests(1f, 0f, grounded: true);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Move, director.State.TeachingBeat);
            Assert.AreEqual(0, director.State.MovementConfirmationCount);

            director.ApplyRejectedMoveForTests(1f, 0.2f, grounded: false);
            Assert.AreEqual(FirstWorldEntryTeachingBeat.Move, director.State.TeachingBeat);
            Assert.AreEqual(0, director.State.MovementConfirmationCount);
        }

        private ChampionController CreateTutorialController(string name)
        {
            var champion = new GameObject(name);
            champion.transform.SetParent(_fixtureRoot.transform, false);
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionCombat>();
            champion.AddComponent<SkillCaster>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            return controller;
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
