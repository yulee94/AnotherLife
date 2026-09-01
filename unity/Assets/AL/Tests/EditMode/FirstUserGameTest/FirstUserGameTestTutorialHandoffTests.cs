#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Editor.Development.FirstUserGameTest;
using AL.Narrative.Nvs01;
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
        public void ContractPinsExactCurrentMainIdentifiersWithoutDefiningASecondQuest()
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
            Assert.That(FirstUserGameTestOmenOfferContract.OfferDialogueId,
                Is.EqualTo("DLG_OMEN_1_OFFER"));
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

        [Test]
        public void ValeriusReportOpensExactProductionOfferWithoutAcceptingQuest()
        {
            Assert.That(FirstUserGameTestOmenOfferSession.TryCreate(
                CatalogBytes(),
                RealmId.Eldergrove,
                out FirstUserGameTestOmenOfferSession session,
                out string message), Is.True, message);

            Assert.That(session.View.IsOpened, Is.False);
            Assert.That(session.View.Title, Is.EqualTo("The First Signal"));
            Assert.That(session.View.Objective,
                Is.EqualTo("Speak with Captain Valerius."));
            Assert.That(session.Snapshot.Revision, Is.Zero);
            Assert.That(session.Snapshot.StateId, Is.EqualTo("OFFERED"));
            Assert.That(session.Snapshot.CurrentDialogueNodeId, Is.Empty);
            Assert.That(session.Snapshot.PendingChoice, Is.False);

            Assert.That(session.TryOpenReport(
                out FirstUserGameTestOmenOfferView opened,
                out message), Is.True, message);
            Assert.That(opened.IsOpened, Is.True);
            Assert.That(opened.SpeakerName, Is.EqualTo("Captain Valerius"));
            Assert.That(opened.SpeakerRole, Is.EqualTo("Veil Watch military liaison"));
            Assert.That(opened.Dialogue,
                Is.EqualTo("My lord, the Veil Watch has detected a strange resonance above the Sky Castle. Will you hear my report?"));
            Assert.That(opened.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.Dialogue));
            Assert.That(opened.Choices.Select(choice => choice.Label), Is.EqualTo(new[]
            {
                "Tell me what happened.",
                "Not yet."
            }));
            Assert.That(session.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(session.Snapshot.StateId, Is.EqualTo("OFFERED"));
            Assert.That(session.Snapshot.CurrentDialogueNodeId,
                Is.EqualTo("DLG_OMEN_1_OFFER"));
            Assert.That(session.Snapshot.PendingChoice, Is.True);
            Assert.That(session.Snapshot.PendingSemanticActionId, Is.Empty);
            Assert.That(session.Snapshot.CommittedRealmId, Is.EqualTo("eldergrove"));
            Assert.That(session.Snapshot.EncounterStatus, Is.EqualTo(Nvs01EncounterStatus.None));
            Assert.That(session.Snapshot.CurrentEncounter, Is.Null);
            Assert.That(session.Snapshot.ConsequenceIntentIds, Is.Empty);
            Assert.That(session.Snapshot.TryGetObjectiveStatus(
                "OBJ_OMEN_1_TALK",
                out Nvs01ObjectiveStatus objectiveStatus), Is.True);
            Assert.That(objectiveStatus, Is.EqualTo(Nvs01ObjectiveStatus.Active));

            Assert.That(session.TryOpenReport(out FirstUserGameTestOmenOfferView duplicate,
                out message), Is.True, message);
            Assert.That(duplicate, Is.SameAs(opened));
            Assert.That(session.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(session.Snapshot.StateId, Is.EqualTo("OFFERED"));
        }

        [Test]
        public void DecliningTheReportCreatesAClearReopenPath()
        {
            Assert.That(FirstUserGameTestOmenOfferSession.TryCreate(
                CatalogBytes(),
                RealmId.Crownlands,
                out FirstUserGameTestOmenOfferSession session,
                out string message), Is.True, message);
            Assert.That(session.TryOpenReport(out _, out message), Is.True, message);

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.DeclineChoiceKey,
                out FirstUserGameTestOmenOfferView declined,
                out message), Is.True, message);
            Assert.That(declined.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.Declined));
            Assert.That(declined.CanReopen, Is.True);
            Assert.That(declined.IsOpened, Is.False);
            Assert.That(declined.Choices, Is.Empty);
            Assert.That(session.Snapshot.StateId,
                Is.EqualTo(FirstUserGameTestTutorialContract.OmenOfferedState));
            Assert.That(session.Snapshot.PendingChoice, Is.False);
            Assert.That(session.Snapshot.Revision, Is.EqualTo(2));

            Assert.That(session.TryOpenReport(
                out FirstUserGameTestOmenOfferView reopened,
                out message), Is.True, message);
            Assert.That(reopened.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.Dialogue));
            Assert.That(reopened.Choices.Select(choice => choice.Key), Is.EqualTo(new[]
            {
                FirstUserGameTestOmenOfferContract.AcceptChoiceKey,
                FirstUserGameTestOmenOfferContract.DeclineChoiceKey
            }));
            Assert.That(session.Snapshot.Revision, Is.EqualTo(3));
        }

        [Test]
        public void AcceptedReportFlowsThroughSkyCastleToRealmReady()
        {
            Assert.That(FirstUserGameTestOmenOfferSession.TryCreate(
                CatalogBytes(),
                RealmId.Umbral,
                out FirstUserGameTestOmenOfferSession session,
                out string message), Is.True, message);
            Assert.That(session.TryOpenReport(out _, out message), Is.True, message);

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.AcceptChoiceKey,
                out FirstUserGameTestOmenOfferView accepted,
                out message), Is.True, message);
            Assert.That(accepted.Choices.Select(choice => choice.Key), Is.EqualTo(new[]
            {
                FirstUserGameTestOmenOfferContract.InvestigateChoiceKey,
                FirstUserGameTestOmenOfferContract.AskMoreChoiceKey
            }));
            Assert.That(session.Snapshot.StateId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.TalkState));
            Assert.That(session.Snapshot.CurrentDialogueNodeId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.StartDialogueId));

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.AskMoreChoiceKey,
                out FirstUserGameTestOmenOfferView lore,
                out message), Is.True, message);
            Assert.That(lore.Choices.Single().Key,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.DepartChoiceKey));
            Assert.That(session.Snapshot.CurrentDialogueNodeId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.LoreDialogueId));

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.DepartChoiceKey,
                out FirstUserGameTestOmenOfferView departure,
                out message), Is.True, message);
            Assert.That(departure.Choices.Single().Key,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.DeployChoiceKey));
            Assert.That(session.Snapshot.CurrentDialogueNodeId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.GoDialogueId));

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.DeployChoiceKey,
                out FirstUserGameTestOmenOfferView deploymentReady,
                out message), Is.True, message);
            Assert.That(deploymentReady.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.DeploymentReady));
            Assert.That(deploymentReady.CanDeploy, Is.True);
            Assert.That(deploymentReady.PrimaryActionLabel,
                Is.EqualTo("Deploy Champion."));
            Assert.That(session.Snapshot.CurrentDialogueNodeId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.ArenaStartDialogueId));
            Assert.That(session.Snapshot.PendingSemanticActionId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.RequestArenaActionId));

            Assert.That(session.TryPrepareDeployment(
                out FirstUserGameTestOmenOfferView prepared,
                out message), Is.True, message);
            Assert.That(prepared.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.DeploymentPrepared));
            Assert.That(prepared.IsJourneyComplete, Is.False);
            Assert.That(prepared.CanEnterEncounter, Is.True);
            Assert.That(prepared.Choices, Is.Empty);
            Assert.That(session.Snapshot.StateId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.InvestigateState));
            Assert.That(session.Snapshot.CurrentEncounter, Is.Not.Null);
            Assert.That(session.Snapshot.ConsequenceIntentIds, Is.Empty);

            Assert.That(session.TryEnterEncounter(
                out FirstUserGameTestOmenOfferView encounter,
                out message), Is.True, message);
            Assert.That(encounter.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.EncounterActive));
            Assert.That(encounter.PrimaryActionLabel,
                Is.EqualTo(FirstUserGameTestPlaytestCopy.RecoverTearAction));
            Assert.That(encounter.SecondaryActionLabel,
                Is.EqualTo(FirstUserGameTestPlaytestCopy.RetreatAction));

            Assert.That(session.TryResolveEncounter(
                NvsEncounterOutcome.Success,
                out FirstUserGameTestOmenOfferView reportReady,
                out message), Is.True, message);
            Assert.That(reportReady.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.ReportReady));
            Assert.That(reportReady.CanReturnToValerius, Is.True);
            Assert.That(session.Snapshot.StateId,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.ReportState));
            Assert.That(session.Snapshot.ConsequenceIntentIds,
                Is.EqualTo(new[] { FirstUserGameTestOmenOfferContract.TearIntentId }));

            Assert.That(session.TryOpenReport(
                out FirstUserGameTestOmenOfferView report,
                out message), Is.True, message);
            Assert.That(report.Choices.Single().Key,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.PresentTearChoiceKey));

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.PresentTearChoiceKey,
                out FirstUserGameTestOmenOfferView conclusion,
                out message), Is.True, message);
            Assert.That(conclusion.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.Dialogue));
            Assert.That(conclusion.Choices.Single().Key,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.ContinueChoiceKey));

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.ContinueChoiceKey,
                out FirstUserGameTestOmenOfferView realmReady,
                out message), Is.True, message);
            Assert.That(realmReady.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.RealmReady));
            Assert.That(realmReady.IsJourneyComplete, Is.True);
            Assert.That(realmReady.PrimaryActionLabel,
                Is.EqualTo(FirstUserGameTestPlaytestCopy.CompleteJourneyAction));
            Assert.That(session.Snapshot.ConsequenceIntentIds,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.CompletionIntentIds));
        }

        [Test]
        public void FailedEncounterHasAuthoredRetryAndCreatesANewRequest()
        {
            FirstUserGameTestOmenOfferSession session = EnterSkyCastle();
            string firstCorrelation = session.Snapshot.CurrentEncounter.CorrelationId;

            Assert.That(session.TryResolveEncounter(
                NvsEncounterOutcome.Failure,
                out FirstUserGameTestOmenOfferView failed,
                out string message), Is.True, message);
            Assert.That(failed.Stage, Is.EqualTo(FirstUserGameTestOmenOfferStage.Dialogue));
            Assert.That(failed.Choices.Single().Key,
                Is.EqualTo(FirstUserGameTestOmenOfferContract.RetryChoiceKey));

            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.RetryChoiceKey,
                out FirstUserGameTestOmenOfferView retryReady,
                out message), Is.True, message);
            Assert.That(retryReady.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.RecoveryReady));
            Assert.That(session.TryPrepareDeployment(out _, out message), Is.True, message);
            Assert.That(session.Snapshot.CurrentEncounter.CorrelationId,
                Is.Not.EqualTo(firstCorrelation));
        }

        [TestCase(NvsEncounterOutcome.Cancelled)]
        [TestCase(NvsEncounterOutcome.Unavailable)]
        public void InterruptedEncounterAlwaysExposesAWorkingRetry(
            NvsEncounterOutcome outcome)
        {
            FirstUserGameTestOmenOfferSession session = EnterSkyCastle();
            string firstCorrelation = session.Snapshot.CurrentEncounter.CorrelationId;

            Assert.That(session.TryResolveEncounter(
                outcome,
                out FirstUserGameTestOmenOfferView recovery,
                out string message), Is.True, message);
            Assert.That(recovery.Stage,
                Is.EqualTo(FirstUserGameTestOmenOfferStage.RecoveryReady));
            Assert.That(recovery.PrimaryActionLabel, Is.Not.Empty);
            Assert.That(session.TryPrepareDeployment(out _, out message), Is.True, message);
            Assert.That(session.Snapshot.CurrentEncounter.CorrelationId,
                Is.Not.EqualTo(firstCorrelation));
        }

        [Test]
        public void ValeriusReportFailsClosedForInvalidRealmOrCatalog()
        {
            Assert.That(FirstUserGameTestOmenOfferSession.TryCreate(
                CatalogBytes(),
                RealmId.None,
                out FirstUserGameTestOmenOfferSession invalidRealm,
                out string realmMessage), Is.False);
            Assert.That(invalidRealm, Is.Null);
            Assert.That(realmMessage, Is.Not.Empty);

            Assert.That(FirstUserGameTestOmenOfferSession.TryCreate(
                new byte[] { 0x7b, 0x7d },
                RealmId.Crownlands,
                out FirstUserGameTestOmenOfferSession invalidCatalog,
                out string catalogMessage), Is.False);
            Assert.That(invalidCatalog, Is.Null);
            Assert.That(catalogMessage, Is.Not.Empty);
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

        [TestCase(FirstUserGameTestTutorialStep.Move,
            (int)FirstUserGameTestOmenUiState.Preparing,
            true, true, false, false, false,
            (int)FirstUserGameTestButtonRole.Status,
            (int)FirstUserGameTestTutorialFocusTarget.Move)]
        [TestCase(FirstUserGameTestTutorialStep.BasicAttack,
            (int)FirstUserGameTestOmenUiState.Preparing,
            false, false, true, false, false,
            (int)FirstUserGameTestButtonRole.Status,
            (int)FirstUserGameTestTutorialFocusTarget.Attack)]
        [TestCase(FirstUserGameTestTutorialStep.Complete,
            (int)FirstUserGameTestOmenUiState.Preparing,
            false, false, false, false, false,
            (int)FirstUserGameTestButtonRole.Status,
            (int)FirstUserGameTestTutorialFocusTarget.None)]
        [TestCase(FirstUserGameTestTutorialStep.Complete,
            (int)FirstUserGameTestOmenUiState.ReadyToOpen,
            false, false, false, true, false,
            (int)FirstUserGameTestButtonRole.ActiveTask,
            (int)FirstUserGameTestTutorialFocusTarget.Report)]
        [TestCase(FirstUserGameTestTutorialStep.Complete,
            (int)FirstUserGameTestOmenUiState.AwaitingResponse,
            false, false, false, false, true,
            (int)FirstUserGameTestButtonRole.Status,
            (int)FirstUserGameTestTutorialFocusTarget.Response)]
        [TestCase(FirstUserGameTestTutorialStep.Complete,
            (int)FirstUserGameTestOmenUiState.Complete,
            false, false, false, false, true,
            (int)FirstUserGameTestButtonRole.Completed,
            (int)FirstUserGameTestTutorialFocusTarget.Response)]
        public void InteractionPlanExposesOnlyControlsThatCanProduceTheirNamedResult(
            object stepValue,
            int omenUiStateValue,
            bool expectedMovementEnabled,
            bool expectedMovementEmphasized,
            bool expectedAttackEnabled,
            bool expectedObjectiveActionable,
            bool expectedResponseActionable,
            int expectedObjectiveRole,
            int expectedFocus)
        {
            var step = (FirstUserGameTestTutorialStep)stepValue;
            Assert.That(FirstUserGameTestTutorialInteractionPlan.TryCreate(
                StateAt(step),
                (FirstUserGameTestOmenUiState)omenUiStateValue,
                out FirstUserGameTestTutorialInteractionPlan plan), Is.True);
            Assert.That(plan.MovementEnabled, Is.EqualTo(expectedMovementEnabled));
            Assert.That(plan.MovementEmphasized, Is.EqualTo(expectedMovementEmphasized));
            Assert.That(plan.AttackEnabled, Is.EqualTo(expectedAttackEnabled));
            Assert.That(plan.ObjectiveActionable, Is.EqualTo(expectedObjectiveActionable));
            Assert.That(plan.ResponseActionable, Is.EqualTo(expectedResponseActionable));
            Assert.That((int)plan.ObjectiveRole, Is.EqualTo(expectedObjectiveRole));
            Assert.That((int)plan.FocusTarget, Is.EqualTo(expectedFocus));
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
        public void AssemblyAndSourceRemainEditorOnlyUnregisteredAndInMemory()
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
            string offer = File.ReadAllText(Path.Combine(
                implementationDirectory,
                "FirstUserGameTestOmenOffer.cs"));

            Assert.That(asmdef, Does.Contain("\"Editor\""));
            Assert.That(contracts, Does.Contain("#if !UNITY_EDITOR"));
            Assert.That(runtime, Does.Contain("#if !UNITY_EDITOR"));
            Assert.That(offer, Does.Contain("#if !UNITY_EDITOR"));
            Assert.That(offer, Does.Contain("SelectValerius"));
            Assert.That(offer, Does.Contain("SelectChoice"));
            Assert.That(offer, Does.Contain("InvokePrimaryAction"));
            Assert.That(offer, Does.Contain("CreateInMemory"));
            Assert.That(offer, Does.Contain("ApplyEncounterResult"));
            foreach (string forbidden in new[]
                     {
                         "QuestDefinition",
                         "TryAccept",
                         "TryProgress",
                         "TryComplete",
                         "InvokePendingSemanticAction",
                         ".Abandon(",
                         "ServiceLocator",
                         "SceneManager",
                         "PlayerPrefs",
                         "persistentDataPath",
                         "Application.Quit",
                         "Resources.Load",
                         "Addressables"
                     })
            {
                Assert.That(contracts + runtime + offer, Does.Not.Contain(forbidden), forbidden);
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

        private static FirstUserGameTestOmenOfferSession EnterSkyCastle()
        {
            Assert.That(FirstUserGameTestOmenOfferSession.TryCreate(
                CatalogBytes(),
                RealmId.Crownlands,
                out FirstUserGameTestOmenOfferSession session,
                out string message), Is.True, message);
            Assert.That(session.TryOpenReport(out _, out message), Is.True, message);
            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.AcceptChoiceKey,
                out _,
                out message), Is.True, message);
            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.InvestigateChoiceKey,
                out _,
                out message), Is.True, message);
            Assert.That(session.TrySelectChoice(
                FirstUserGameTestOmenOfferContract.DeployChoiceKey,
                out _,
                out message), Is.True, message);
            Assert.That(session.TryPrepareDeployment(out _, out message), Is.True, message);
            Assert.That(session.TryEnterEncounter(out _, out message), Is.True, message);
            return session;
        }

        private static byte[] CatalogBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                ApplicationDataPath(),
                "StreamingAssets",
                Nvs01CatalogContract.StreamingAssetsRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
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
