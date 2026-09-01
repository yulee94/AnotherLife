#if !UNITY_EDITOR
#error The isolated first-user OMEN offer is Editor-only.
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AL.Core;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.UI.Kingdom;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal static class FirstUserGameTestOmenOfferContract
    {
        internal const string OfferDialogueId = "DLG_OMEN_1_OFFER";
        internal const string StartDialogueId = "DLG_OMEN_1_START";
        internal const string LoreDialogueId = "DLG_OMEN_1_LORE";
        internal const string GoDialogueId = "DLG_OMEN_1_GO";
        internal const string ArenaStartDialogueId = "DLG_OMEN_1_ARENA_START";
        internal const string FailureDialogueId = "DLG_OMEN_1_FAILURE";
        internal const string ReportDialogueId = "DLG_OMEN_1_REPORT";
        internal const string ReportConclusionDialogueId = "DLG_OMEN_1_REPORT_CONCLUSION";
        internal const string TalkState = "TALK_TO_VALERIUS";
        internal const string InvestigateState = "INVESTIGATE_SKY_CASTLE";
        internal const string FailedState = "FAILED";
        internal const string ReportState = "REPORT_TO_VALERIUS";
        internal const string CompletedState = "COMPLETED";
        internal const string RequestArenaActionId = "REQUEST_SKY_CASTLE_ARENA";
        internal const string RetryArenaActionId = "RETRY_SKY_CASTLE_ARENA";
        internal const string AcceptChoiceKey = "choice.omen1.accept";
        internal const string DeclineChoiceKey = "choice.omen1.decline";
        internal const string InvestigateChoiceKey = "choice.omen1.investigate";
        internal const string AskMoreChoiceKey = "choice.omen1.ask_more";
        internal const string DepartChoiceKey = "choice.omen1.depart";
        internal const string DeployChoiceKey = "choice.omen1.deploy";
        internal const string RetryChoiceKey = "choice.omen1.retry";
        internal const string PresentTearChoiceKey = "choice.omen1.present_tear";
        internal const string ContinueChoiceKey = "choice.omen1.continue";
        internal const string TearIntentId = "ACQUIRE_CELESTIAL_TEAR";

        internal static readonly string[] CompletionIntentIds =
        {
            TearIntentId,
            "GRANT_GOLD_500",
            "GRANT_VALERIUS_AFFINITY_5",
            "COMPLETE_OMEN_1",
            "UNLOCK_REALM_CHAPTER_1"
        };
    }

    internal enum FirstUserGameTestOmenOfferStage
    {
        Closed = 0,
        Dialogue = 1,
        Declined = 2,
        DeploymentReady = 3,
        DeploymentPrepared = 4,
        EncounterActive = 5,
        RecoveryReady = 6,
        ReportReady = 7,
        RealmReady = 8
    }

    internal sealed class FirstUserGameTestOmenChoice
    {
        internal FirstUserGameTestOmenChoice(string key, string label)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
        }

        internal string Key { get; }
        internal string Label { get; }
    }

    internal sealed class FirstUserGameTestOmenOfferView
    {
        internal FirstUserGameTestOmenOfferView(
            FirstUserGameTestOmenOfferStage stage,
            string title,
            string objective,
            string speakerName,
            string speakerRole,
            string dialogue,
            IList<FirstUserGameTestOmenChoice> choices,
            string primaryActionLabel,
            string secondaryActionLabel)
        {
            Stage = stage;
            Title = title ?? string.Empty;
            Objective = objective ?? string.Empty;
            SpeakerName = speakerName ?? string.Empty;
            SpeakerRole = speakerRole ?? string.Empty;
            Dialogue = dialogue ?? string.Empty;
            Choices = new ReadOnlyCollection<FirstUserGameTestOmenChoice>(
                choices?.ToArray() ?? Array.Empty<FirstUserGameTestOmenChoice>());
            PrimaryActionLabel = primaryActionLabel ?? string.Empty;
            SecondaryActionLabel = secondaryActionLabel ?? string.Empty;
        }

        internal FirstUserGameTestOmenOfferStage Stage { get; }
        internal bool IsOpened => Stage != FirstUserGameTestOmenOfferStage.Closed &&
                                  Stage != FirstUserGameTestOmenOfferStage.Declined;
        internal bool CanReopen => Stage == FirstUserGameTestOmenOfferStage.Declined;
        internal bool CanDeploy => Stage == FirstUserGameTestOmenOfferStage.DeploymentReady;
        internal bool CanPrepareEncounter => CanDeploy ||
                                             Stage == FirstUserGameTestOmenOfferStage.RecoveryReady;
        internal bool CanEnterEncounter =>
            Stage == FirstUserGameTestOmenOfferStage.DeploymentPrepared;
        internal bool CanResolveEncounter =>
            Stage == FirstUserGameTestOmenOfferStage.EncounterActive;
        internal bool CanReturnToValerius =>
            Stage == FirstUserGameTestOmenOfferStage.ReportReady;
        internal bool IsJourneyComplete => Stage == FirstUserGameTestOmenOfferStage.RealmReady;
        internal bool HasPrimaryAction => Choices.Count > 0 || PrimaryActionLabel.Length > 0;
        internal bool HasSecondaryAction => Choices.Count > 1 || SecondaryActionLabel.Length > 0;
        internal string Title { get; }
        internal string Objective { get; }
        internal string SpeakerName { get; }
        internal string SpeakerRole { get; }
        internal string Dialogue { get; }
        internal IReadOnlyList<FirstUserGameTestOmenChoice> Choices { get; }
        internal string PrimaryActionLabel { get; }
        internal string SecondaryActionLabel { get; }
        internal string SpeakerLine =>
            SpeakerName.Length == 0 && SpeakerRole.Length == 0
                ? string.Empty
                : SpeakerName + "  •  " + SpeakerRole;
    }

    internal sealed class FirstUserGameTestOmenOfferSession
    {
        private const string PlaytestSnapshotVersion = "editor-playtest-v1";
        private const string PlaytestSnapshotReference =
            "snapshot://first-user-game-test/sky-castle";

        private readonly Nvs01KingdomPresenter _presenter;
        private readonly string _realmId;
        private FirstUserGameTestOmenOfferView _view;

        private FirstUserGameTestOmenOfferSession(
            Nvs01KingdomPresenter presenter,
            string realmId,
            FirstUserGameTestOmenOfferView view)
        {
            _presenter = presenter;
            _realmId = realmId;
            _view = view;
        }

        internal FirstUserGameTestOmenOfferView View => _view;
        internal Nvs01QuestSnapshot Snapshot => _presenter.Runtime.Snapshot;

        internal static bool TryCreate(
            byte[] catalogBytes,
            RealmId realm,
            out FirstUserGameTestOmenOfferSession session,
            out string message)
        {
            session = null;
            message = string.Empty;
            if (!TryResolveRealm(realm, out string realmId))
            {
                message = "The selected realm could not be used for Valerius's report.";
                return false;
            }

            Nvs01CatalogValidationResult validation =
                Nvs01CatalogValidator.ValidateCanonicalArtifact(catalogBytes);
            if (!validation.IsAccepted || validation.VerifiedCatalog == null)
            {
                message = "The authored Valerius report is unavailable.";
                return false;
            }

            var realmContext = new Nvs01RealmContext(
                Nvs01RealmContextStatus.CommittedValid,
                realmId);
            Nvs01KingdomPresenter presenter;
            try
            {
                presenter = Nvs01KingdomPresenter.CreateInMemory(
                    validation.VerifiedCatalog,
                    () => realmContext,
                    () => new Nvs01CapabilitySnapshot(
                        validation.VerifiedCatalog.Catalog.ExternalCapabilities.ToDictionary(
                            capability => capability.Id,
                            _ => true,
                            StringComparer.Ordinal)));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException)
            {
                message = "The Valerius report could not be prepared.";
                return false;
            }

            Nvs01KingdomView initial = presenter.Present();
            if (!IsExactInitialOffer(presenter.Runtime.Snapshot, initial))
            {
                message = "The Valerius report did not begin at the offered quest boundary.";
                return false;
            }

            session = new FirstUserGameTestOmenOfferSession(
                presenter,
                realmId,
                BuildView(initial, FirstUserGameTestOmenOfferStage.Closed));
            return true;
        }

        internal bool TryOpenReport(
            out FirstUserGameTestOmenOfferView view,
            out string message)
        {
            view = _view;
            message = string.Empty;
            if (_view.IsJourneyComplete)
            {
                return true;
            }

            if (_view.CanReturnToValerius)
            {
                Nvs01KingdomActionResult report = _presenter.SelectValerius();
                if (!IsSafeCommittedResult(report, _realmId) ||
                    !string.Equals(
                        report.Disposition.Snapshot.StateId,
                        FirstUserGameTestOmenOfferContract.ReportState,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        report.Disposition.Snapshot.CurrentDialogueNodeId,
                        FirstUserGameTestOmenOfferContract.ReportDialogueId,
                        StringComparison.Ordinal))
                {
                    message = "Valerius could not receive the Celestial Tear safely.";
                    return false;
                }

                _view = BuildView(report.View, FirstUserGameTestOmenOfferStage.Dialogue);
                view = _view;
                return true;
            }

            if (_view.IsOpened)
            {
                return true;
            }

            long revisionBefore = Snapshot.Revision;
            Nvs01KingdomActionResult result = _presenter.SelectValerius();
            if (!IsExactOpenedOffer(result, _realmId, revisionBefore + 1))
            {
                message = "Valerius's report could not be opened without changing the quest.";
                return false;
            }

            _view = BuildView(result.View, FirstUserGameTestOmenOfferStage.Dialogue);
            view = _view;
            return true;
        }

        internal bool TrySelectChoice(
            string choiceKey,
            out FirstUserGameTestOmenOfferView view,
            out string message)
        {
            view = _view;
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(choiceKey) ||
                !_view.Choices.Any(choice => string.Equals(
                    choice.Key,
                    choiceKey,
                    StringComparison.Ordinal)))
            {
                message = "That response is not available in Valerius's current report.";
                return false;
            }

            Nvs01KingdomActionResult result = _presenter.SelectChoice(choiceKey);
            if (!IsSafeCommittedResult(result, _realmId) ||
                !HasExpectedChoiceConsequences(choiceKey, result))
            {
                message = "Valerius's report could not safely apply that response.";
                return false;
            }

            FirstUserGameTestOmenOfferStage stage = ResolveStage(
                result.View,
                result.Disposition.Snapshot);
            if (stage == FirstUserGameTestOmenOfferStage.Closed ||
                stage == FirstUserGameTestOmenOfferStage.DeploymentPrepared ||
                stage == FirstUserGameTestOmenOfferStage.EncounterActive)
            {
                message = "Valerius's report reached an unsupported response state.";
                return false;
            }

            _view = BuildView(result.View, stage);
            view = _view;
            return true;
        }

        internal bool TryPrepareDeployment(
            out FirstUserGameTestOmenOfferView view,
            out string message)
        {
            view = _view;
            message = string.Empty;
            if (!_view.CanPrepareEncounter)
            {
                message = "The Sky Castle deployment is not ready yet.";
                return false;
            }

            string priorCorrelation = Snapshot.LastEncounterCorrelationId;
            Nvs01KingdomActionResult result = _presenter.InvokePrimaryAction();
            Nvs01CommandDisposition disposition = result?.Disposition;
            Nvs01QuestSnapshot snapshot = disposition?.Snapshot;
            if (disposition == null ||
                disposition.Status != Nvs01CommandStatus.Committed ||
                disposition.Diagnostic != null ||
                disposition.ConsequenceIntentIds.Count != 0 ||
                result.EncounterRequest == null ||
                !result.ShouldEnterEncounter ||
                snapshot == null ||
                snapshot.CurrentEncounter == null ||
                !ReferenceEquals(snapshot.CurrentEncounter, result.EncounterRequest) ||
                snapshot.EncounterStatus != Nvs01EncounterStatus.Requested ||
                !string.Equals(
                    snapshot.StateId,
                    FirstUserGameTestOmenOfferContract.InvestigateState,
                    StringComparison.Ordinal) ||
                !string.Equals(snapshot.CommittedRealmId, _realmId, StringComparison.Ordinal) ||
                snapshot.ConsequenceIntentIds.Count != 0 ||
                priorCorrelation.Length > 0 && string.Equals(
                    priorCorrelation,
                    snapshot.CurrentEncounter.CorrelationId,
                    StringComparison.Ordinal) ||
                result.View == null ||
                result.View.HasDiagnostic)
            {
                message = "The Sky Castle deployment could not be prepared safely.";
                return false;
            }

            _view = BuildView(
                result.View,
                FirstUserGameTestOmenOfferStage.DeploymentPrepared);
            view = _view;
            return true;
        }

        internal bool TryEnterEncounter(
            out FirstUserGameTestOmenOfferView view,
            out string message)
        {
            view = _view;
            message = string.Empty;
            Nvs01QuestSnapshot snapshot = Snapshot;
            if (!_view.CanEnterEncounter ||
                snapshot.CurrentEncounter == null ||
                snapshot.EncounterStatus != Nvs01EncounterStatus.Requested ||
                !string.Equals(
                    snapshot.StateId,
                    FirstUserGameTestOmenOfferContract.InvestigateState,
                    StringComparison.Ordinal))
            {
                message = "The Sky Castle encounter could not be entered safely.";
                return false;
            }

            _view = BuildView(
                _presenter.Present(),
                FirstUserGameTestOmenOfferStage.EncounterActive);
            view = _view;
            return true;
        }

        internal bool TryResolveEncounter(
            NvsEncounterOutcome outcome,
            out FirstUserGameTestOmenOfferView view,
            out string message)
        {
            view = _view;
            message = string.Empty;
            if (!_view.CanResolveEncounter ||
                !Enum.IsDefined(typeof(NvsEncounterOutcome), outcome) ||
                Snapshot.CurrentEncounter == null)
            {
                message = "The Sky Castle outcome is not available at this point in the journey.";
                return false;
            }

            NvsEncounterRequest request = Snapshot.CurrentEncounter;
            var result = new NvsEncounterResult(
                request.ContractVersion,
                request.CorrelationId,
                request.QuestId,
                request.HookId,
                request.RealmId,
                outcome,
                request.GetEventId(outcome),
                outcome == NvsEncounterOutcome.Success
                    ? PlaytestSnapshotVersion
                    : string.Empty,
                outcome == NvsEncounterOutcome.Success
                    ? PlaytestSnapshotReference
                    : string.Empty);
            Nvs01CommandDisposition disposition = _presenter.Runtime.ApplyEncounterResult(result);
            if (!IsSafeEncounterOutcome(disposition, outcome, request))
            {
                message = "The Sky Castle outcome could not be recorded in this playtest session.";
                return false;
            }

            Nvs01KingdomView presented = _presenter.Present();
            FirstUserGameTestOmenOfferStage stage = ResolveStage(
                presented,
                disposition.Snapshot);
            if (stage != FirstUserGameTestOmenOfferStage.Dialogue &&
                stage != FirstUserGameTestOmenOfferStage.RecoveryReady &&
                stage != FirstUserGameTestOmenOfferStage.ReportReady)
            {
                message = "The Sky Castle outcome did not expose a safe next step.";
                return false;
            }

            _view = BuildView(presented, stage);
            view = _view;
            return true;
        }

        private bool IsSafeEncounterOutcome(
            Nvs01CommandDisposition disposition,
            NvsEncounterOutcome outcome,
            NvsEncounterRequest request)
        {
            Nvs01QuestSnapshot snapshot = disposition?.Snapshot;
            bool success = outcome == NvsEncounterOutcome.Success;
            return disposition != null &&
                   disposition.Status == Nvs01CommandStatus.Committed &&
                   disposition.Diagnostic == null &&
                   snapshot != null &&
                   snapshot.CurrentEncounter == null &&
                   snapshot.EncounterStatus == Nvs01EncounterStatus.Resolved &&
                   snapshot.LastEncounterOutcome == outcome &&
                   string.Equals(
                       snapshot.LastEncounterCorrelationId,
                       request.CorrelationId,
                       StringComparison.Ordinal) &&
                   string.Equals(snapshot.CommittedRealmId, _realmId, StringComparison.Ordinal) &&
                   disposition.ConsequenceIntentIds.SequenceEqual(
                       success
                           ? new[] { FirstUserGameTestOmenOfferContract.TearIntentId }
                           : Array.Empty<string>(),
                       StringComparer.Ordinal) &&
                   snapshot.ConsequenceIntentIds.SequenceEqual(
                       success
                           ? new[] { FirstUserGameTestOmenOfferContract.TearIntentId }
                           : Array.Empty<string>(),
                       StringComparer.Ordinal) &&
                   string.Equals(
                       snapshot.StateId,
                       success
                           ? FirstUserGameTestOmenOfferContract.ReportState
                           : outcome == NvsEncounterOutcome.Failure
                               ? FirstUserGameTestOmenOfferContract.FailedState
                               : FirstUserGameTestOmenOfferContract.InvestigateState,
                       StringComparison.Ordinal);
        }

        private static bool HasExpectedChoiceConsequences(
            string choiceKey,
            Nvs01KingdomActionResult result)
        {
            IReadOnlyList<string> applied = result.Disposition.ConsequenceIntentIds;
            IReadOnlyList<string> retained = result.Disposition.Snapshot.ConsequenceIntentIds;
            if (string.Equals(
                    choiceKey,
                    FirstUserGameTestOmenOfferContract.PresentTearChoiceKey,
                    StringComparison.Ordinal))
            {
                return applied.SequenceEqual(
                           FirstUserGameTestOmenOfferContract.CompletionIntentIds.Skip(1),
                           StringComparer.Ordinal) &&
                       retained.SequenceEqual(
                           FirstUserGameTestOmenOfferContract.CompletionIntentIds,
                           StringComparer.Ordinal);
            }

            if (string.Equals(
                    choiceKey,
                    FirstUserGameTestOmenOfferContract.ContinueChoiceKey,
                    StringComparison.Ordinal))
            {
                return applied.Count == 0 &&
                       retained.SequenceEqual(
                           FirstUserGameTestOmenOfferContract.CompletionIntentIds,
                           StringComparer.Ordinal);
            }

            if (retained.Count == 0)
            {
                return applied.Count == 0;
            }

            return applied.Count == 0 &&
                   retained.SequenceEqual(
                       new[] { FirstUserGameTestOmenOfferContract.TearIntentId },
                       StringComparer.Ordinal);
        }

        private static bool IsExactInitialOffer(
            Nvs01QuestSnapshot snapshot,
            Nvs01KingdomView view)
        {
            return snapshot != null &&
                   view != null &&
                   view.Status == Nvs01KingdomViewStatus.Ready &&
                   snapshot.Revision == 0 &&
                   string.Equals(
                       snapshot.StateId,
                       FirstUserGameTestTutorialContract.OmenOfferedState,
                       StringComparison.Ordinal) &&
                   snapshot.CurrentDialogueNodeId.Length == 0 &&
                   !snapshot.PendingChoice &&
                   snapshot.PendingSemanticActionId.Length == 0 &&
                   snapshot.CommittedRealmId.Length == 0 &&
                   snapshot.EncounterStatus == Nvs01EncounterStatus.None &&
                   snapshot.CurrentEncounter == null &&
                   snapshot.ConsequenceIntentIds.Count == 0 &&
                   HasExactTalkObjective(snapshot) &&
                   view.PrimaryAction == Nvs01KingdomActionKind.SelectValerius &&
                   view.Choices.Count == 0 &&
                   !view.HasDialogue &&
                   !view.HasDiagnostic;
        }

        private static bool IsExactOpenedOffer(
            Nvs01KingdomActionResult result,
            string realmId,
            long expectedRevision)
        {
            Nvs01CommandDisposition disposition = result?.Disposition;
            Nvs01QuestSnapshot snapshot = disposition?.Snapshot;
            Nvs01KingdomView view = result?.View;
            return disposition != null &&
                   disposition.Status == Nvs01CommandStatus.Committed &&
                   disposition.Diagnostic == null &&
                   disposition.EncounterRequest == null &&
                   disposition.ConsequenceIntentIds.Count == 0 &&
                   snapshot != null &&
                   snapshot.Revision == expectedRevision &&
                   string.Equals(
                       snapshot.StateId,
                       FirstUserGameTestTutorialContract.OmenOfferedState,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       snapshot.CurrentDialogueNodeId,
                       FirstUserGameTestOmenOfferContract.OfferDialogueId,
                       StringComparison.Ordinal) &&
                   snapshot.PendingChoice &&
                   snapshot.PendingSemanticActionId.Length == 0 &&
                   string.Equals(snapshot.CommittedRealmId, realmId, StringComparison.Ordinal) &&
                   snapshot.EncounterStatus == Nvs01EncounterStatus.None &&
                   snapshot.CurrentEncounter == null &&
                   snapshot.ConsequenceIntentIds.Count == 0 &&
                   HasExactTalkObjective(snapshot) &&
                   view != null &&
                   view.Status == Nvs01KingdomViewStatus.Ready &&
                   view.PrimaryAction == Nvs01KingdomActionKind.None &&
                   view.HasDialogue &&
                   !view.HasDiagnostic &&
                   HasExactReviewChoices(view);
        }

        private static bool IsSafeCommittedResult(
            Nvs01KingdomActionResult result,
            string realmId)
        {
            Nvs01CommandDisposition disposition = result?.Disposition;
            Nvs01QuestSnapshot snapshot = disposition?.Snapshot;
            return disposition != null &&
                   disposition.Status == Nvs01CommandStatus.Committed &&
                   disposition.Diagnostic == null &&
                   disposition.EncounterRequest == null &&
                   snapshot != null &&
                   string.Equals(snapshot.CommittedRealmId, realmId, StringComparison.Ordinal) &&
                   snapshot.CurrentEncounter == null &&
                   result.View != null &&
                   (result.View.Status == Nvs01KingdomViewStatus.Ready ||
                    result.View.Status == Nvs01KingdomViewStatus.Completed) &&
                   !result.View.HasDiagnostic;
        }

        private static FirstUserGameTestOmenOfferStage ResolveStage(
            Nvs01KingdomView view,
            Nvs01QuestSnapshot snapshot)
        {
            if (view == null || snapshot == null)
            {
                return FirstUserGameTestOmenOfferStage.Closed;
            }

            if (string.Equals(
                    snapshot.StateId,
                    FirstUserGameTestOmenOfferContract.CompletedState,
                    StringComparison.Ordinal) &&
                !view.HasDialogue)
            {
                return FirstUserGameTestOmenOfferStage.RealmReady;
            }

            if (string.Equals(
                    snapshot.StateId,
                    FirstUserGameTestOmenOfferContract.ReportState,
                    StringComparison.Ordinal) &&
                !view.HasDialogue)
            {
                return FirstUserGameTestOmenOfferStage.ReportReady;
            }

            if (string.Equals(
                    snapshot.PendingSemanticActionId,
                    FirstUserGameTestOmenOfferContract.RetryArenaActionId,
                    StringComparison.Ordinal) &&
                view.PrimaryAction == Nvs01KingdomActionKind.InvokeSemanticAction)
            {
                return FirstUserGameTestOmenOfferStage.RecoveryReady;
            }

            if (string.Equals(
                    snapshot.StateId,
                    FirstUserGameTestTutorialContract.OmenOfferedState,
                    StringComparison.Ordinal) &&
                snapshot.CurrentDialogueNodeId.Length == 0 &&
                !snapshot.PendingChoice)
            {
                return FirstUserGameTestOmenOfferStage.Declined;
            }

            if (string.Equals(
                    snapshot.PendingSemanticActionId,
                    FirstUserGameTestOmenOfferContract.RequestArenaActionId,
                    StringComparison.Ordinal) &&
                view.PrimaryAction == Nvs01KingdomActionKind.InvokeSemanticAction)
            {
                return FirstUserGameTestOmenOfferStage.DeploymentReady;
            }

            return view.HasDialogue &&
                   (view.Choices.Count > 0 || snapshot.PendingChoice)
                ? FirstUserGameTestOmenOfferStage.Dialogue
                : FirstUserGameTestOmenOfferStage.Closed;
        }

        private static bool HasExactTalkObjective(Nvs01QuestSnapshot snapshot)
        {
            return snapshot.TryGetObjectiveStatus(
                       FirstUserGameTestTutorialContract.OmenOfferedObjectiveId,
                       out Nvs01ObjectiveStatus status) &&
                   status == Nvs01ObjectiveStatus.Active;
        }

        private static bool HasExactReviewChoices(Nvs01KingdomView view)
        {
            return view.Choices.Count == 2 &&
                   string.Equals(
                       view.Choices[0].Key,
                       FirstUserGameTestOmenOfferContract.AcceptChoiceKey,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       view.Choices[1].Key,
                       FirstUserGameTestOmenOfferContract.DeclineChoiceKey,
                       StringComparison.Ordinal);
        }

        private static FirstUserGameTestOmenOfferView BuildView(
            Nvs01KingdomView view,
            FirstUserGameTestOmenOfferStage stage)
        {
            IList<FirstUserGameTestOmenChoice> choices =
                stage == FirstUserGameTestOmenOfferStage.Dialogue
                    ? view.Choices
                        .Select(choice => new FirstUserGameTestOmenChoice(
                            choice.Key,
                            choice.Label))
                        .ToArray()
                    : Array.Empty<FirstUserGameTestOmenChoice>();

            string primaryActionLabel = string.Empty;
            string secondaryActionLabel = string.Empty;
            switch (stage)
            {
                case FirstUserGameTestOmenOfferStage.DeploymentReady:
                case FirstUserGameTestOmenOfferStage.RecoveryReady:
                    primaryActionLabel = view.PrimaryActionLabel;
                    break;
                case FirstUserGameTestOmenOfferStage.DeploymentPrepared:
                    primaryActionLabel = FirstUserGameTestPlaytestCopy.EnterSkyCastleAction;
                    break;
                case FirstUserGameTestOmenOfferStage.EncounterActive:
                    primaryActionLabel = FirstUserGameTestPlaytestCopy.RecoverTearAction;
                    secondaryActionLabel = FirstUserGameTestPlaytestCopy.RetreatAction;
                    break;
                case FirstUserGameTestOmenOfferStage.ReportReady:
                    primaryActionLabel = FirstUserGameTestPlaytestCopy.ReturnToValeriusAction;
                    break;
                case FirstUserGameTestOmenOfferStage.RealmReady:
                    primaryActionLabel = FirstUserGameTestPlaytestCopy.CompleteJourneyAction;
                    break;
            }

            return new FirstUserGameTestOmenOfferView(
                stage,
                view.Title,
                view.ObjectiveText,
                view.SpeakerName,
                view.SpeakerRole,
                view.DialogueText,
                choices,
                primaryActionLabel,
                secondaryActionLabel);
        }

        private static bool TryResolveRealm(RealmId realm, out string realmId)
        {
            switch (realm)
            {
                case RealmId.Crownlands:
                    realmId = "crownlands";
                    return true;
                case RealmId.Stonehold:
                    realmId = "stonehold";
                    return true;
                case RealmId.Eldergrove:
                    realmId = "eldergrove";
                    return true;
                case RealmId.Umbral:
                    realmId = "umbral";
                    return true;
                default:
                    realmId = string.Empty;
                    return false;
            }
        }
    }
}
