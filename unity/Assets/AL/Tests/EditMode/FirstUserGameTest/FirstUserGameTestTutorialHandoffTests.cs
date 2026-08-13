#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AL.Editor.Development.FirstUserGameTest;
using AL.Narrative.Nvs01.Contracts;
using NUnit.Framework;
using UnityEditor;

namespace AL.Tests.EditMode.FirstUserGameTest
{
    [TestFixture]
    public sealed class FirstUserGameTestTutorialHandoffTests
    {
        private const string SessionA = "0123456789abcdef0123456789abcdef";
        private const string SessionB = "1123456789abcdef0123456789abcdef";
        private const string GenerationA =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string GenerationB =
            "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [SetUp]
        public void SetUp()
        {
            FirstUserGameTestTutorialSessionStore.EraseForTests(SessionA);
            FirstUserGameTestTutorialSessionStore.EraseForTests(SessionB);
        }

        [TearDown]
        public void TearDown()
        {
            FirstUserGameTestTutorialSessionStore.EraseForTests(SessionA);
            FirstUserGameTestTutorialSessionStore.EraseForTests(SessionB);
        }

        [Test]
        public void ContractPinsExactCurrentMainIdentifiersWithoutDefiningAQuest()
        {
            Assert.That(FirstUserGameTestTutorialContract.TutorialId,
                Is.EqualTo("TUTORIAL_FIRST_WORLD_ENTRY"));
            Assert.That(FirstUserGameTestTutorialContract.MoveStepId, Is.EqualTo("MOVE"));
            Assert.That(FirstUserGameTestTutorialContract.BasicAttackStepId,
                Is.EqualTo("BASIC_ATTACK"));
            Assert.That(FirstUserGameTestTutorialContract.MoveObjectiveId,
                Is.EqualTo("OBJ_TUTORIAL_FIRST_WORLD_ENTRY_MOVE"));
            Assert.That(FirstUserGameTestTutorialContract.BasicAttackObjectiveId,
                Is.EqualTo("OBJ_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK"));
            Assert.That(FirstUserGameTestTutorialContract.MovementConfirmedEventId,
                Is.EqualTo("EVENT_TUTORIAL_FIRST_WORLD_ENTRY_MOVEMENT_CONFIRMED"));
            Assert.That(FirstUserGameTestTutorialContract.BasicAttackConfirmedEventId,
                Is.EqualTo("EVENT_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK_CONFIRMED"));
            Assert.That(FirstUserGameTestTutorialContract.TutorialCompletedEventId,
                Is.EqualTo("EVENT_TUTORIAL_FIRST_WORLD_ENTRY_COMPLETED"));
            Assert.That(FirstUserGameTestTutorialContract.OmenQuestId,
                Is.EqualTo(Nvs01CatalogContract.QuestId));
            Assert.That(FirstUserGameTestTutorialContract.OmenOfferedState,
                Is.EqualTo("OFFERED"));
            Assert.That(FirstUserGameTestTutorialContract.FollowActiveObjectiveActionId,
                Is.EqualTo("ACTION_FOLLOW_ACTIVE_OBJECTIVE"));
        }

        [TestCase(null, GenerationA, TestName = "Initial_NullSession_Rejects")]
        [TestCase("", GenerationA, TestName = "Initial_EmptySession_Rejects")]
        [TestCase("0123456789ABCDEF0123456789ABCDEF", GenerationA,
            TestName = "Initial_UpperSession_Rejects")]
        [TestCase(SessionA, null, TestName = "Initial_NullGeneration_Rejects")]
        [TestCase(SessionA, "", TestName = "Initial_EmptyGeneration_Rejects")]
        [TestCase(SessionA,
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
            TestName = "Initial_UpperGeneration_Rejects")]
        public void InitialStateRequiresExactSessionAndGeneration(
            string session,
            string generation)
        {
            Assert.That(FirstUserGameTestTutorialPlanner.TryCreateInitial(
                session,
                generation,
                out FirstUserGameTestTutorialState state), Is.False);
            Assert.That(state, Is.Null);
        }

        [Test]
        public void OrderedMovementThenBasicAttackEmitsExactOneShotHandoff()
        {
            FirstUserGameTestTutorialState initial = Initial();
            FirstUserGameTestTutorialTransition movement = Apply(
                initial,
                FirstUserGameTestTutorialEvidenceKind.MovementConfirmed);
            Assert.That(movement.Status,
                Is.EqualTo(FirstUserGameTestTutorialTransitionStatus.Applied));
            Assert.That(movement.Diagnostic,
                Is.EqualTo(FirstUserGameTestTutorialDiagnostic.None));
            Assert.That(movement.ConfirmedEventId,
                Is.EqualTo(FirstUserGameTestTutorialContract.MovementConfirmedEventId));
            Assert.That(movement.CompletionEventId, Is.Empty);
            Assert.That(movement.State.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.BasicAttack));
            Assert.That(movement.State.MovementConfirmationCount, Is.EqualTo(1));

            FirstUserGameTestTutorialTransition attack = Apply(
                movement.State,
                FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed);
            Assert.That(attack.Status,
                Is.EqualTo(FirstUserGameTestTutorialTransitionStatus.Applied));
            Assert.That(attack.ConfirmedEventId,
                Is.EqualTo(FirstUserGameTestTutorialContract.BasicAttackConfirmedEventId));
            Assert.That(attack.CompletionEventId,
                Is.EqualTo(FirstUserGameTestTutorialContract.TutorialCompletedEventId));
            Assert.That(attack.ForegroundQuestId,
                Is.EqualTo(Nvs01CatalogContract.QuestId));
            Assert.That(attack.ForegroundQuestState, Is.EqualTo("OFFERED"));
            Assert.That(attack.State.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.Complete));
            Assert.That(attack.State.MovementConfirmationCount, Is.EqualTo(1));
            Assert.That(attack.State.BasicAttackConfirmationCount, Is.EqualTo(1));
            Assert.That(attack.State.CompletionEventCount, Is.EqualTo(1));
            Assert.That(attack.State.OmenOfferCount, Is.EqualTo(1));
            Assert.That(attack.State.ForegroundObjectiveId, Is.EqualTo("OBJ_OMEN_1_TALK"));
        }

        [TestCase(FirstUserGameTestTutorialStep.BasicAttack,
            FirstUserGameTestTutorialEvidenceKind.MovementConfirmed,
            TestName = "DuplicateMovementAtAttack_IsInert")]
        [TestCase(FirstUserGameTestTutorialStep.Complete,
            FirstUserGameTestTutorialEvidenceKind.MovementConfirmed,
            TestName = "DuplicateMovementAfterCompletion_IsInert")]
        [TestCase(FirstUserGameTestTutorialStep.Complete,
            FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed,
            TestName = "DuplicateAttackAfterCompletion_IsInert")]
        public void DuplicateEvidenceIsInert(
            object stepValue,
            object evidenceKindValue)
        {
            var step = (FirstUserGameTestTutorialStep)stepValue;
            var evidenceKind = (FirstUserGameTestTutorialEvidenceKind)evidenceKindValue;
            FirstUserGameTestTutorialState state = StateAt(step);
            FirstUserGameTestTutorialTransition result = Apply(state, evidenceKind);
            Assert.That(result.Status,
                Is.EqualTo(FirstUserGameTestTutorialTransitionStatus.DuplicateIgnored));
            Assert.That(result.Diagnostic,
                Is.EqualTo(FirstUserGameTestTutorialDiagnostic.None));
            Assert.That(result.State, Is.SameAs(state));
            Assert.That(result.ConfirmedEventId, Is.Empty);
            Assert.That(result.CompletionEventId, Is.Empty);
            Assert.That(result.ForegroundQuestId, Is.Empty);
        }

        [Test]
        public void BasicAttackBeforeMovementIsRejectedWithoutMutation()
        {
            FirstUserGameTestTutorialState initial = Initial();
            FirstUserGameTestTutorialTransition result = Apply(
                initial,
                FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed);
            Assert.That(result.Status,
                Is.EqualTo(FirstUserGameTestTutorialTransitionStatus.Rejected));
            Assert.That(result.Diagnostic,
                Is.EqualTo(FirstUserGameTestTutorialDiagnostic.OutOfOrder));
            Assert.That(result.State, Is.SameAs(initial));
            Assert.That(result.ConfirmedEventId, Is.Empty);
            Assert.That(result.CompletionEventId, Is.Empty);
        }

        [TestCase(true, false, FirstUserGameTestTutorialDiagnostic.SessionMismatch,
            TestName = "CrossSessionEvidence_Rejects")]
        [TestCase(false, true, FirstUserGameTestTutorialDiagnostic.GenerationMismatch,
            TestName = "CrossGenerationEvidence_Rejects")]
        public void CrossBoundaryEvidenceFailsClosed(
            bool differentSession,
            bool differentGeneration,
            object expectedValue)
        {
            var expected = (FirstUserGameTestTutorialDiagnostic)expectedValue;
            FirstUserGameTestTutorialState initial = Initial();
            var evidence = new FirstUserGameTestTutorialEvidence(
                differentSession ? SessionB : SessionA,
                differentGeneration ? GenerationB : GenerationA,
                FirstUserGameTestTutorialEvidenceKind.MovementConfirmed);
            FirstUserGameTestTutorialTransition result =
                FirstUserGameTestTutorialPlanner.Apply(initial, evidence);
            Assert.That(result.Status,
                Is.EqualTo(FirstUserGameTestTutorialTransitionStatus.Rejected));
            Assert.That(result.Diagnostic, Is.EqualTo(expected));
            Assert.That(result.State, Is.SameAs(initial));
        }

        [TestCase(FirstUserGameTestTutorialStep.Invalid, 0, 0, 0, 0,
            TestName = "InvalidStep_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.Move, 1, 0, 0, 0,
            TestName = "PrematureMoveCount_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.BasicAttack, 0, 0, 0, 0,
            TestName = "MissingMoveCount_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.BasicAttack, 1, 1, 0, 0,
            TestName = "PrematureAttackCount_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, 1, 0, 1, 1,
            TestName = "MissingAttackCount_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, 1, 1, 0, 1,
            TestName = "MissingCompletionEvent_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, 1, 1, 1, 0,
            TestName = "MissingOffer_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, 1, 1, 2, 1,
            TestName = "DuplicateCompletionEvent_Rejects")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, 1, 1, 1, 2,
            TestName = "DuplicateOffer_Rejects")]
        public void MalformedStateShapesFailClosed(
            object stepValue,
            int movement,
            int attack,
            int completion,
            int offer)
        {
            var step = (FirstUserGameTestTutorialStep)stepValue;
            var malformed = new FirstUserGameTestTutorialState(
                SessionA,
                GenerationA,
                step,
                movement,
                attack,
                completion,
                offer);
            Assert.That(FirstUserGameTestTutorialPlanner.IsValidState(malformed), Is.False);
            FirstUserGameTestTutorialTransition result = Apply(
                malformed,
                FirstUserGameTestTutorialEvidenceKind.MovementConfirmed);
            Assert.That(result.Status,
                Is.EqualTo(FirstUserGameTestTutorialTransitionStatus.Rejected));
            Assert.That(result.Diagnostic,
                Is.EqualTo(FirstUserGameTestTutorialDiagnostic.StateInvalid));
        }

        [TestCase(FirstUserGameTestTutorialStep.Move)]
        [TestCase(FirstUserGameTestTutorialStep.BasicAttack)]
        [TestCase(FirstUserGameTestTutorialStep.Complete)]
        public void RetainedCodecRoundTripsEveryCanonicalState(
            object stepValue)
        {
            var step = (FirstUserGameTestTutorialStep)stepValue;
            FirstUserGameTestTutorialState state = StateAt(step);
            Assert.That(FirstUserGameTestTutorialStateCodec.TryEncode(
                state,
                out string payload), Is.True);
            Assert.That(payload.Length,
                Is.LessThanOrEqualTo(
                    FirstUserGameTestTutorialContract.MaximumRetainedEnvelopeCharacters));
            Assert.That(FirstUserGameTestTutorialStateCodec.TryDecode(
                payload,
                out FirstUserGameTestTutorialState restored), Is.True);
            Assert.That(state.ValueEquals(restored), Is.True);
        }

        private static IEnumerable<TestCaseData> InvalidRetainedPayloads()
        {
            yield return new TestCaseData(null).SetName("Codec_Null_Rejects");
            yield return new TestCaseData(string.Empty).SetName("Codec_Empty_Rejects");
            yield return new TestCaseData(new string('x', 257)).SetName("Codec_Oversize_Rejects");
            yield return new TestCaseData("wrong\n" + SessionA + "\n" + GenerationA + "\n1\n0\n0\n0\n0")
                .SetName("Codec_WrongVersion_Rejects");
            yield return new TestCaseData(
                    FirstUserGameTestTutorialContract.ContractVersion + "\r\n" + SessionA +
                    "\r\n" + GenerationA + "\r\n1\r\n0\r\n0\r\n0\r\n0")
                .SetName("Codec_CRLF_Rejects");
            yield return new TestCaseData(
                    FirstUserGameTestTutorialContract.ContractVersion + "\n" + SessionA +
                    "\n" + GenerationA + "\n01\n0\n0\n0\n0")
                .SetName("Codec_NoncanonicalInteger_Rejects");
            yield return new TestCaseData(
                    FirstUserGameTestTutorialContract.ContractVersion + "\n" + SessionA +
                    "\n" + GenerationA + "\n1\n0\n0\n0")
                .SetName("Codec_MissingField_Rejects");
            yield return new TestCaseData(
                    FirstUserGameTestTutorialContract.ContractVersion + "\n" + SessionA +
                    "\n" + GenerationA + "\n3\n1\n1\n1\n2")
                .SetName("Codec_DuplicateOffer_Rejects");
        }

        [TestCaseSource(nameof(InvalidRetainedPayloads))]
        public void RetainedCodecRejectsMalformedOrNoncanonicalPayload(string payload)
        {
            Assert.That(FirstUserGameTestTutorialStateCodec.TryDecode(
                payload,
                out FirstUserGameTestTutorialState state), Is.False);
            Assert.That(state, Is.Null);
        }

        [TestCase(false, FirstUserGameTestFollowOutcome.NoTarget,
            "RESULT_ACTIVE_OBJECTIVE_NO_TARGET")]
        [TestCase(true, FirstUserGameTestFollowOutcome.Focused,
            "RESULT_ACTIVE_OBJECTIVE_FOCUSED")]
        public void OfferedFollowReturnsOnlyTypedNonmutatingOutcome(
            bool targetAvailable,
            object expectedOutcomeValue,
            string expectedResultId)
        {
            var expectedOutcome = (FirstUserGameTestFollowOutcome)expectedOutcomeValue;
            FirstUserGameTestTutorialState complete =
                StateAt(FirstUserGameTestTutorialStep.Complete);
            FirstUserGameTestFollowResult result = FirstUserGameTestFollowPlanner.Plan(
                complete,
                FirstUserGameTestTutorialContract.FollowActiveObjectiveActionId,
                targetAvailable);
            Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
            Assert.That(result.ResultId, Is.EqualTo(expectedResultId));
            Assert.That(complete.ValueEquals(StateAt(FirstUserGameTestTutorialStep.Complete)),
                Is.True);
        }

        [TestCase(FirstUserGameTestTutorialStep.Move, "ACTION_FOLLOW_ACTIVE_OBJECTIVE",
            TestName = "FollowDuringMove_IsUnavailable")]
        [TestCase(FirstUserGameTestTutorialStep.BasicAttack, "ACTION_FOLLOW_ACTIVE_OBJECTIVE",
            TestName = "FollowDuringAttack_IsUnavailable")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, "WRONG_ACTION",
            TestName = "UnknownFollowAction_IsUnavailable")]
        public void FollowUnavailableNeverMutatesTutorial(
            object stepValue,
            string actionId)
        {
            var step = (FirstUserGameTestTutorialStep)stepValue;
            FirstUserGameTestTutorialState state = StateAt(step);
            FirstUserGameTestFollowResult result = FirstUserGameTestFollowPlanner.Plan(
                state,
                actionId,
                targetAvailable: true);
            Assert.That(result.Outcome,
                Is.EqualTo(FirstUserGameTestFollowOutcome.Unavailable));
            Assert.That(result.ResultId,
                Is.EqualTo("RESULT_ACTIVE_OBJECTIVE_UNAVAILABLE"));
            Assert.That(state.ValueEquals(StateAt(step)), Is.True);
        }

        [TestCase(FirstUserGameTestTutorialStep.Move, false, true,
            TestName = "MovementStepWithoutFollowUi_AllowsChampionProcessing")]
        [TestCase(FirstUserGameTestTutorialStep.BasicAttack, false, true,
            TestName = "AttackStepWithoutFollowUi_AllowsChampionProcessing")]
        [TestCase(FirstUserGameTestTutorialStep.Move, true, false,
            TestName = "MovementStepWithUiInteraction_SuppressesChampionProcessing")]
        [TestCase(FirstUserGameTestTutorialStep.BasicAttack, true, false,
            TestName = "AttackStepWithUiInteraction_SuppressesChampionProcessing")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, false, false,
            TestName = "OfferedStateWithoutFocus_StillSuppressesChampionProcessing")]
        [TestCase(FirstUserGameTestTutorialStep.Complete, true, false,
            TestName = "OfferedFollowUi_SuppressesChampionProcessing")]
        public void IsolatedInputGateDeterministicallyBlocksRawAttackAtFollowBoundary(
            object stepValue,
            bool followUiActive,
            bool expected)
        {
            var step = (FirstUserGameTestTutorialStep)stepValue;
            Assert.That(
                FirstUserGameTestIsolatedInputGate.AllowsChampionControllerProcessing(
                    StateAt(step),
                    followUiActive),
                Is.EqualTo(expected));
        }

        [Test]
        public void IsolatedInputGateFailsClosedForMissingState()
        {
            Assert.That(
                FirstUserGameTestIsolatedInputGate.AllowsChampionControllerProcessing(
                    null,
                    followUiActive: false),
                Is.False);
        }

        [Test]
        public void SessionStorePersistsOrderedProgressAcrossReconstruction()
        {
            var first = new FirstUserGameTestTutorialSessionStore(SessionA, GenerationA);
            Assert.That(first.TryLoadOrCreate(
                out FirstUserGameTestTutorialState initial,
                out string message), Is.True, message);
            Assert.That(initial.Step, Is.EqualTo(FirstUserGameTestTutorialStep.Move));
            Assert.That(first.TryApply(
                FirstUserGameTestTutorialEvidenceKind.MovementConfirmed,
                out FirstUserGameTestTutorialTransition movement,
                out message), Is.True, message);
            Assert.That(movement.Changed, Is.True);

            var reconstructed = new FirstUserGameTestTutorialSessionStore(SessionA, GenerationA);
            Assert.That(reconstructed.TryLoadOrCreate(
                out FirstUserGameTestTutorialState restored,
                out message), Is.True, message);
            Assert.That(restored.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.BasicAttack));
            Assert.That(reconstructed.TryApply(
                FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed,
                out FirstUserGameTestTutorialTransition completed,
                out message), Is.True, message);
            Assert.That(completed.State.IsOmenOffered, Is.True);

            var reloaded = new FirstUserGameTestTutorialSessionStore(SessionA, GenerationA);
            Assert.That(reloaded.TryLoadOrCreate(
                out FirstUserGameTestTutorialState final,
                out message), Is.True, message);
            Assert.That(final.CompletionEventCount, Is.EqualTo(1));
            Assert.That(final.OmenOfferCount, Is.EqualTo(1));
        }

        [Test]
        public void SessionStoreDuplicateEvidenceCannotDuplicateCompletionOrOffer()
        {
            var store = new FirstUserGameTestTutorialSessionStore(SessionA, GenerationA);
            Assert.That(store.TryApply(
                FirstUserGameTestTutorialEvidenceKind.MovementConfirmed,
                out _,
                out string message), Is.True, message);
            Assert.That(store.TryApply(
                FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed,
                out FirstUserGameTestTutorialTransition first,
                out message), Is.True, message);
            Assert.That(first.Changed, Is.True);
            Assert.That(store.TryApply(
                FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed,
                out FirstUserGameTestTutorialTransition replay,
                out message), Is.True, message);
            Assert.That(replay.Status,
                Is.EqualTo(FirstUserGameTestTutorialTransitionStatus.DuplicateIgnored));
            Assert.That(replay.CompletionEventId, Is.Empty);
            Assert.That(replay.ForegroundQuestId, Is.Empty);
            Assert.That(replay.State.CompletionEventCount, Is.EqualTo(1));
            Assert.That(replay.State.OmenOfferCount, Is.EqualTo(1));
        }

        [Test]
        public void SessionStoreRejectsCrossGenerationRetainedState()
        {
            var first = new FirstUserGameTestTutorialSessionStore(SessionA, GenerationA);
            Assert.That(first.TryLoadOrCreate(out _, out string message), Is.True, message);
            var drifted = new FirstUserGameTestTutorialSessionStore(SessionA, GenerationB);
            Assert.That(drifted.TryLoadOrCreate(
                out FirstUserGameTestTutorialState state,
                out message), Is.False);
            Assert.That(state, Is.Null);
            Assert.That(message, Does.Contain("generation"));
        }

        [Test]
        public void SessionStoreRejectsCorruptRetainedStateWithoutOverwrite()
        {
            const string corrupt = "corrupt-retained-state";
            FirstUserGameTestTutorialSessionStore.SetRawForTests(SessionA, corrupt);
            var store = new FirstUserGameTestTutorialSessionStore(SessionA, GenerationA);
            Assert.That(store.TryLoadOrCreate(
                out FirstUserGameTestTutorialState state,
                out string message), Is.False);
            Assert.That(state, Is.Null);
            Assert.That(message, Is.Not.Empty);
            Assert.That(SessionState.GetString(
                    "AL.FirstUserGameTest.Tutorial.v1." + SessionA,
                    string.Empty),
                Is.EqualTo(corrupt));
        }

        [Test]
        public void AssemblyAndSourceRemainEditorOnlyUnregisteredAndNonquest()
        {
            string implementationDirectory = Path.Combine(
                ApplicationDataPath(),
                "AL",
                "Scripts",
                "Editor",
                "Development",
                "FirstUserGameTest");
            string asmdef = File.ReadAllText(Path.Combine(
                implementationDirectory,
                "AL.Development.FirstUserGameTest.Editor.asmdef"));
            string contracts = File.ReadAllText(Path.Combine(
                implementationDirectory,
                "FirstUserGameTestTutorialContracts.cs"));
            string runtime = File.ReadAllText(Path.Combine(
                implementationDirectory,
                "FirstUserGameTestTutorialRuntime.cs"));

            Assert.That(asmdef, Does.Contain("\"Editor\""));
            Assert.That(contracts, Does.Contain("#if !UNITY_EDITOR"));
            Assert.That(runtime, Does.Contain("#if !UNITY_EDITOR"));
            foreach (string forbidden in new[]
                     {
                         "QuestDefinition",
                         "Nvs01QuestRuntime",
                         "TryAccept",
                         "TryProgress",
                         "TryComplete",
                         "ServiceLocator",
                         "SceneManager",
                         "PlayerPrefs",
                         "persistentDataPath",
                         "Application.Quit",
                         "Resources.Load",
                         "Addressables"
                     })
            {
                Assert.That(contracts + runtime, Does.Not.Contain(forbidden), forbidden);
            }

            string[] productionReferences = Directory
                .EnumerateFiles(
                    Path.Combine(ApplicationDataPath(), "AL", "Scripts"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => path.IndexOf(
                    Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => !path.EndsWith(
                    "EditorGameTestModeBootstrap.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => File.ReadAllText(path).IndexOf(
                    "FirstUserGameTestTutorial",
                    StringComparison.Ordinal) >= 0)
                .ToArray();
            Assert.That(productionReferences, Is.Empty,
                string.Join(Environment.NewLine, productionReferences));
        }

        private static FirstUserGameTestTutorialState Initial()
        {
            Assert.That(FirstUserGameTestTutorialPlanner.TryCreateInitial(
                SessionA,
                GenerationA,
                out FirstUserGameTestTutorialState state), Is.True);
            return state;
        }

        private static FirstUserGameTestTutorialState StateAt(
            FirstUserGameTestTutorialStep step)
        {
            FirstUserGameTestTutorialState state = Initial();
            if (step == FirstUserGameTestTutorialStep.Move)
            {
                return state;
            }

            state = Apply(
                state,
                FirstUserGameTestTutorialEvidenceKind.MovementConfirmed).State;
            if (step == FirstUserGameTestTutorialStep.BasicAttack)
            {
                return state;
            }

            return Apply(
                state,
                FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed).State;
        }

        private static FirstUserGameTestTutorialTransition Apply(
            FirstUserGameTestTutorialState state,
            FirstUserGameTestTutorialEvidenceKind kind)
        {
            return FirstUserGameTestTutorialPlanner.Apply(
                state,
                new FirstUserGameTestTutorialEvidence(SessionA, GenerationA, kind));
        }

        private static string ApplicationDataPath()
        {
            return Path.GetFullPath(UnityEngine.Application.dataPath);
        }
    }
}
#endif
