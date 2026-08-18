using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Narrative.Nvs01.Contracts;

namespace AL.Narrative.Nvs01
{
    public sealed class Nvs01QuestRuntime : INvs01QuestRuntime
    {
        private const string Offered = "OFFERED";
        private const string TalkToValerius = "TALK_TO_VALERIUS";
        private const string InvestigateSkyCastle = "INVESTIGATE_SKY_CASTLE";
        private const string Failed = "FAILED";
        private const string ReportToValerius = "REPORT_TO_VALERIUS";
        private const string Completed = "COMPLETED";

        private const string TalkObjective = "OBJ_OMEN_1_TALK";
        private const string ArenaObjective = "OBJ_OMEN_1_ARENA";
        private const string ReportObjective = "OBJ_OMEN_1_REPORT";

        private const string OfferDialogue = "DLG_OMEN_1_OFFER";
        private const string ArenaStartDialogue = "DLG_OMEN_1_ARENA_START";
        private const string FailureDialogue = "DLG_OMEN_1_FAILURE";
        private const string ReportDialogue = "DLG_OMEN_1_REPORT";
        private const string ReportConclusionDialogue = "DLG_OMEN_1_REPORT_CONCLUSION";

        private const string SelectValeriusEvent = "SELECT_VALERIUS";
        private const string QuestAcceptedEvent = "QUEST_ACCEPTED";
        private const string DialogueChoiceEvent = "DIALOGUE_CHOICE_SELECTED";
        private const string RequestArenaEvent = "REQUEST_SKY_CASTLE_ARENA";
        private const string RetryArenaEvent = "RETRY_SKY_CASTLE_ARENA";
        private const string AbandonEvent = "ABANDON_OMEN_1";
        private const string ArenaSuccessEvent = "EVENT_SKY_CASTLE_ARENA_SUCCESS";
        private const string ArenaFailureEvent = "EVENT_SKY_CASTLE_ARENA_FAILURE";
        private const string ArenaCancelledEvent = "EVENT_SKY_CASTLE_ARENA_CANCELLED";
        private const string ArenaUnavailableEvent = "EVENT_SKY_CASTLE_ARENA_UNAVAILABLE";

        private const string ArenaHook = "HOOK_SKY_CASTLE_ARENA";
        private const string ArenaLocation = "LOCATION_SKY_CASTLE_MARKER";
        private const string DeployCapability = "ACTION_DEPLOY_CHAMPION";
        private const string ReturnScene = "Kingdom";
        private const string TearIntent = "ACQUIRE_CELESTIAL_TEAR";

        private static readonly string[] EmptyIdentifiers = new string[0];
        private static readonly string[] RequiredArenaCapabilities =
        {
            ArenaLocation,
            DeployCapability,
            ArenaHook,
            ArenaSuccessEvent,
            ArenaFailureEvent,
            ArenaCancelledEvent,
            ArenaUnavailableEvent
        };

        private readonly Nvs01VerifiedCatalog _verifiedCatalog;
        private readonly INvs01MutationCommitter _committer;
        private readonly Func<string> _guidFactory;
        private Nvs01QuestSnapshot _snapshot;
        private bool _commitUncertain;

        public Nvs01QuestRuntime(
            Nvs01VerifiedCatalog verifiedCatalog,
            Nvs01QuestSnapshot initialSnapshot,
            INvs01MutationCommitter committer,
            Func<string> guidFactory)
        {
            _verifiedCatalog = verifiedCatalog ?? throw new ArgumentNullException(nameof(verifiedCatalog));
            _committer = committer ?? throw new ArgumentNullException(nameof(committer));
            _guidFactory = guidFactory ?? throw new ArgumentNullException(nameof(guidFactory));
            Catalog = verifiedCatalog.Catalog;

            if (!string.Equals(verifiedCatalog.CatalogId, Nvs01CatalogContract.CatalogId, StringComparison.Ordinal) ||
                !string.Equals(verifiedCatalog.CanonicalSha256, Nvs01CatalogContract.CanonicalSha256, StringComparison.Ordinal) ||
                !string.Equals(Catalog.PacketVersion, Nvs01CatalogContract.PacketVersion, StringComparison.Ordinal) ||
                !string.Equals(Catalog.QuestId, Nvs01CatalogContract.QuestId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Verified catalog identity does not match the NVS-01 runtime contract.", nameof(verifiedCatalog));
            }

            _snapshot = initialSnapshot ?? CreateInitialSnapshot(verifiedCatalog);
            ValidateSnapshotOrThrow(_snapshot, nameof(initialSnapshot));
        }

        public Nvs01QuestRuntime(
            Nvs01VerifiedCatalog verifiedCatalog,
            INvs01MutationCommitter committer,
            Func<string> guidFactory)
            : this(verifiedCatalog, null, committer, guidFactory)
        {
        }

        public Nvs01Catalog Catalog { get; }
        public Nvs01QuestSnapshot Snapshot => _snapshot;

        internal static Nvs01QuestSnapshot CreateInitialSnapshot(Nvs01VerifiedCatalog verifiedCatalog)
        {
            if (verifiedCatalog == null) throw new ArgumentNullException(nameof(verifiedCatalog));
            var objectives = verifiedCatalog.Catalog.Objectives
                .Select(objective => new Nvs01ObjectiveSnapshot(
                    objective.Id,
                    string.Equals(objective.Id, TalkObjective, StringComparison.Ordinal)
                        ? Nvs01ObjectiveStatus.Active
                        : Nvs01ObjectiveStatus.Inactive))
                .ToArray();

            return new Nvs01QuestSnapshot(
                verifiedCatalog.Catalog.PacketVersion,
                verifiedCatalog.CanonicalSha256,
                verifiedCatalog.Catalog.QuestId,
                0,
                Offered,
                objectives,
                string.Empty,
                false,
                string.Empty,
                string.Empty,
                Nvs01EncounterStatus.None,
                null,
                string.Empty,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                EmptyIdentifiers);
        }

        public Nvs01CommandDisposition SelectValerius(
            Nvs01CommandEnvelope command,
            Nvs01InteractionKind interaction,
            Nvs01RealmContext realmContext)
        {
            if (!Enum.IsDefined(typeof(Nvs01InteractionKind), interaction))
                return Reject("EVENT-MISMATCH", "interaction", "Offer or Report", interaction.ToString(), SelectValeriusEvent);

            var fingerprint = CommandFingerprint(
                command,
                "SelectValerius",
                interaction.ToString(),
                RealmFingerprint(realmContext));
            Nvs01CommandDisposition stopped;
            if (!TryPrepareCommand(command, fingerprint, SelectValeriusEvent, out stopped)) return stopped;

            if (!string.Equals(command.ActorId, Catalog.Speaker.Id, StringComparison.Ordinal))
                return Reject("EVENT-MISMATCH", "actor", Catalog.Speaker.Id, command.ActorId, SelectValeriusEvent);
            if (!string.Equals(command.ContextId, Catalog.Placement.ContextId, StringComparison.Ordinal))
                return Reject("EVENT-MISMATCH", "context", Catalog.Placement.ContextId, command.ContextId, SelectValeriusEvent);

            Nvs01CommandDisposition realmFailure;
            if (!TryValidateRealm(realmContext, out realmFailure)) return realmFailure;
            if (_snapshot.CommittedRealmId.Length > 0 &&
                !string.Equals(_snapshot.CommittedRealmId, realmContext.RealmId, StringComparison.Ordinal))
            {
                return Reject("EVENT-MISMATCH", "realm", _snapshot.CommittedRealmId, realmContext.RealmId, SelectValeriusEvent);
            }

            var requiredState = interaction == Nvs01InteractionKind.Offer ? Offered : ReportToValerius;
            var dialogueId = interaction == Nvs01InteractionKind.Offer ? OfferDialogue : ReportDialogue;
            if (!string.Equals(_snapshot.StateId, requiredState, StringComparison.Ordinal))
                return Reject("EVENT-MISMATCH", "state", requiredState, _snapshot.StateId, SelectValeriusEvent);

            if (string.Equals(_snapshot.CurrentDialogueNodeId, dialogueId, StringComparison.Ordinal) && _snapshot.PendingChoice)
                return ReadOnlyDuplicate(SelectValeriusEvent, _snapshot.CurrentEncounter);

            Nvs01DialogueNode node;
            if (!Catalog.TryGetDialogue(dialogueId, out node))
                return Reject("REFERENCE-MISSING", "dialogue", dialogueId, string.Empty, SelectValeriusEvent);

            var draft = new SnapshotDraft(_snapshot)
            {
                CommittedRealmId = realmContext.RealmId,
                CurrentDialogueNodeId = node.Id,
                PendingChoice = node.Choices.Count > 0,
                PendingSemanticActionId = node.SemanticAction ?? string.Empty
            };
            return CommitCommand(command, fingerprint, SelectValeriusEvent, draft, null, EmptyIdentifiers);
        }

        public Nvs01CommandDisposition SelectDialogueChoice(
            Nvs01CommandEnvelope command,
            string choiceKey)
        {
            var fingerprint = CommandFingerprint(command, "SelectDialogueChoice", choiceKey ?? "<null>");
            var receiptEvent = DialogueChoiceEvent + ":" + (_snapshot.CurrentDialogueNodeId.Length == 0 ? "none" : _snapshot.CurrentDialogueNodeId);
            Nvs01CommandDisposition stopped;
            if (!TryPrepareCommand(command, fingerprint, receiptEvent, out stopped)) return stopped;

            if (string.IsNullOrWhiteSpace(choiceKey))
                return Reject("EVENT-MISMATCH", "choice", "catalog choice key", choiceKey ?? string.Empty, receiptEvent);
            if (!_snapshot.PendingChoice || _snapshot.CurrentDialogueNodeId.Length == 0)
                return Reject("EVENT-MISMATCH", "dialogue", "pending choice", _snapshot.CurrentDialogueNodeId, receiptEvent);
            if (!string.Equals(command.ActorId, "PLAYER", StringComparison.Ordinal) ||
                !string.Equals(command.ContextId, _snapshot.CurrentDialogueNodeId, StringComparison.Ordinal))
                return Reject("EVENT-MISMATCH", "actorContext", "PLAYER/" + _snapshot.CurrentDialogueNodeId, command.ActorId + "/" + command.ContextId, receiptEvent);

            Nvs01DialogueNode node;
            if (!Catalog.TryGetDialogue(_snapshot.CurrentDialogueNodeId, out node))
                return Reject("REFERENCE-MISSING", "dialogue", _snapshot.CurrentDialogueNodeId, string.Empty, receiptEvent);
            var choice = node.Choices.FirstOrDefault(item => string.Equals(item.Key, choiceKey, StringComparison.Ordinal));
            if (choice == null)
                return Reject("EVENT-MISMATCH", "choice", "choice on current node", choiceKey, receiptEvent);

            var draft = new SnapshotDraft(_snapshot);
            var eventId = receiptEvent;
            var newIntents = new List<string>();
            var choiceTarget = choice.Target ?? string.Empty;
            var choiceSemanticAction = choice.SemanticAction ?? string.Empty;

            if (string.Equals(node.Id, OfferDialogue, StringComparison.Ordinal) &&
                string.Equals(choice.Key, "choice.omen1.accept", StringComparison.Ordinal))
            {
                Nvs01Transition transition;
                if (!Catalog.TryGetTransition(_snapshot.StateId, QuestAcceptedEvent, out transition))
                    return Reject("TRANSITION-INVALID", "transition", _snapshot.StateId + "/" + QuestAcceptedEvent, string.Empty, QuestAcceptedEvent);
                ApplyTransition(draft, transition, QuestAcceptedEvent);
                SetDialogueTarget(draft, choiceTarget);
                eventId = QuestAcceptedEvent;
            }
            else if (string.Equals(_snapshot.StateId, ReportToValerius, StringComparison.Ordinal) &&
                     string.Equals(choiceTarget, ReportConclusionDialogue, StringComparison.Ordinal))
            {
                Nvs01Transition transition;
                if (!Catalog.TryGetTransition(_snapshot.StateId, ReportConclusionDialogue, out transition))
                    return Reject("TRANSITION-INVALID", "transition", _snapshot.StateId + "/" + ReportConclusionDialogue, string.Empty, ReportConclusionDialogue);
                ApplyTransition(draft, transition, ReportConclusionDialogue);
                SetDialogueTarget(draft, choiceTarget);
                AddConsequenceIntents(draft, ReportConclusionDialogue, newIntents);
                eventId = ReportConclusionDialogue;
            }
            else
            {
                if (choiceTarget.Length > 0)
                {
                    SetDialogueTarget(draft, choiceTarget);
                }
                else
                {
                    draft.PendingChoice = false;
                    draft.PendingSemanticActionId = choiceSemanticAction;
                }

                if (choiceSemanticAction.Length > 0)
                {
                    draft.PendingChoice = false;
                    draft.PendingSemanticActionId = choiceSemanticAction;
                    eventId = choiceSemanticAction;
                }
            }

            return CommitCommand(command, fingerprint, eventId, draft, null, newIntents);
        }

        public Nvs01CommandDisposition InvokePendingSemanticAction(
            Nvs01CommandEnvelope command,
            Nvs01CapabilitySnapshot capabilities,
            Nvs01RealmContext realmContext)
        {
            var actionId = _snapshot.PendingSemanticActionId;
            if (actionId.Length == 0 && _snapshot.CurrentEncounter != null &&
                (string.Equals(_snapshot.LastOperation?.EventId, RequestArenaEvent, StringComparison.Ordinal) ||
                 string.Equals(_snapshot.LastOperation?.EventId, RetryArenaEvent, StringComparison.Ordinal)))
            {
                actionId = _snapshot.LastOperation.EventId;
            }

            var fingerprint = CommandFingerprint(
                command,
                "InvokePendingSemanticAction",
                actionId,
                RealmFingerprint(realmContext),
                CapabilityFingerprint(capabilities));
            Nvs01CommandDisposition stopped;
            if (!TryPrepareCommand(command, fingerprint, actionId.Length == 0 ? "SEMANTIC_ACTION" : actionId, out stopped))
                return stopped;

            if (actionId.Length == 0)
                return Reject("EVENT-MISMATCH", "semanticAction", "pending action", string.Empty, "SEMANTIC_ACTION");
            if (!string.Equals(command.ActorId, "PLAYER", StringComparison.Ordinal) ||
                !string.Equals(command.ContextId, actionId, StringComparison.Ordinal))
                return Reject("EVENT-MISMATCH", "actorContext", "PLAYER/" + actionId, command.ActorId + "/" + command.ContextId, actionId);

            Nvs01CommandDisposition realmFailure;
            if (!TryValidateRealm(realmContext, out realmFailure)) return realmFailure;
            if (_snapshot.CommittedRealmId.Length == 0 ||
                !string.Equals(_snapshot.CommittedRealmId, realmContext.RealmId, StringComparison.Ordinal))
            {
                return Reject("EVENT-MISMATCH", "realm", _snapshot.CommittedRealmId, realmContext.RealmId, actionId);
            }

            Nvs01CommandDisposition capabilityFailure;
            if (!TryValidateArenaCapabilities(capabilities, actionId, out capabilityFailure)) return capabilityFailure;

            if (_snapshot.CurrentEncounter != null &&
                (string.Equals(actionId, RequestArenaEvent, StringComparison.Ordinal) ||
                 string.Equals(actionId, RetryArenaEvent, StringComparison.Ordinal)))
                return VerifyDurableDuplicate(
                    actionId,
                    _snapshot.CurrentEncounter,
                    _snapshot.CurrentEncounter.CorrelationId);

            if (string.Equals(actionId, RequestArenaEvent, StringComparison.Ordinal))
            {
                if (!string.Equals(_snapshot.StateId, TalkToValerius, StringComparison.Ordinal) &&
                    !string.Equals(_snapshot.StateId, InvestigateSkyCastle, StringComparison.Ordinal))
                {
                    return Reject("TRANSITION-INVALID", "state", TalkToValerius + " or " + InvestigateSkyCastle, _snapshot.StateId, actionId);
                }

                var draft = new SnapshotDraft(_snapshot);
                if (string.Equals(_snapshot.StateId, TalkToValerius, StringComparison.Ordinal))
                {
                    Nvs01Transition transition;
                    if (!Catalog.TryGetTransition(_snapshot.StateId, RequestArenaEvent, out transition))
                        return Reject("TRANSITION-INVALID", "transition", _snapshot.StateId + "/" + RequestArenaEvent, string.Empty, actionId);
                    ApplyTransition(draft, transition, RequestArenaEvent);
                }
                else
                {
                    ActivateObjectivesForState(draft, InvestigateSkyCastle, RequestArenaEvent);
                }

                NvsEncounterRequest request;
                Nvs01CommandDisposition idFailure;
                if (!TryCreateEncounterRequest(realmContext.RealmId, out request, out idFailure)) return idFailure;
                draft.EncounterStatus = Nvs01EncounterStatus.Requested;
                draft.CurrentEncounter = request;
                draft.PendingChoice = false;
                draft.PendingSemanticActionId = string.Empty;
                return CommitCommand(command, fingerprint, RequestArenaEvent, draft, request, EmptyIdentifiers);
            }

            if (string.Equals(actionId, RetryArenaEvent, StringComparison.Ordinal))
            {
                var draft = new SnapshotDraft(_snapshot);
                if (string.Equals(_snapshot.StateId, Failed, StringComparison.Ordinal))
                {
                    Nvs01Transition transition;
                    if (!Catalog.TryGetTransition(_snapshot.StateId, RetryArenaEvent, out transition))
                        return Reject("TRANSITION-INVALID", "transition", _snapshot.StateId + "/" + RetryArenaEvent, string.Empty, actionId);
                    ApplyTransition(draft, transition, RetryArenaEvent);
                }
                else if (string.Equals(_snapshot.StateId, InvestigateSkyCastle, StringComparison.Ordinal) &&
                         _snapshot.LastEncounterOutcome.HasValue &&
                         (_snapshot.LastEncounterOutcome.Value == NvsEncounterOutcome.Cancelled ||
                          _snapshot.LastEncounterOutcome.Value == NvsEncounterOutcome.Unavailable))
                {
                    ActivateObjectivesForState(draft, InvestigateSkyCastle, RetryArenaEvent);
                }
                else
                {
                    return Reject("TRANSITION-INVALID", "state", Failed + " or recoverable " + InvestigateSkyCastle, _snapshot.StateId, actionId);
                }
                NvsEncounterRequest request;
                Nvs01CommandDisposition idFailure;
                if (!TryCreateEncounterRequest(realmContext.RealmId, out request, out idFailure)) return idFailure;
                draft.EncounterStatus = Nvs01EncounterStatus.Requested;
                draft.CurrentEncounter = request;
                draft.CurrentDialogueNodeId = string.Empty;
                draft.PendingChoice = false;
                draft.PendingSemanticActionId = string.Empty;
                return CommitCommand(command, fingerprint, RetryArenaEvent, draft, request, EmptyIdentifiers);
            }

            return Reject("EVENT-MISMATCH", "semanticAction", RequestArenaEvent + " or " + RetryArenaEvent, actionId, actionId);
        }

        public Nvs01CommandDisposition ApplyEncounterResult(NvsEncounterResult result)
        {
            if (result == null)
                return Reject("EVENT-MISMATCH", "result", "non-null encounter result", string.Empty, "ENCOUNTER_RESULT");
            if (_commitUncertain)
                return CommitFailed("COMMIT-UNCERTAIN", result.EventId, "reconciliation required");

            if (_snapshot.LastEncounterCorrelationId.Length > 0 &&
                string.Equals(_snapshot.LastEncounterCorrelationId, result.CorrelationId, StringComparison.Ordinal))
            {
                if (LastResultMatches(result))
                {
                    bool isCurrentOperation =
                        _snapshot.LastOperation != null &&
                        string.Equals(
                            _snapshot.LastOperation.EventId,
                            result.EventId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            _snapshot.LastOperation.CorrelationId,
                            result.CorrelationId,
                            StringComparison.Ordinal);
                    return isCurrentOperation
                        ? VerifyDurableDuplicate(
                            result.EventId,
                            null,
                            result.CorrelationId)
                        : ReadOnlyDuplicate(
                            result.EventId,
                            null,
                            result.CorrelationId);
                }
                return Reject("EVENT-MISMATCH", "result", "exact prior result payload", ResultFingerprint(_snapshot), result.EventId, result.CorrelationId);
            }

            if (_snapshot.CurrentEncounter == null)
            {
                return Reject("EVENT-MISMATCH", "correlation", _snapshot.LastEncounterCorrelationId, result.CorrelationId, result.EventId, result.CorrelationId);
            }

            var request = _snapshot.CurrentEncounter;
            var mismatch = ValidateResultAgainstRequest(request, result);
            if (mismatch != null) return mismatch;
            if (_snapshot.Revision == long.MaxValue)
                return Reject("TRANSITION-INVALID", "revision", "less than Int64.MaxValue", _snapshot.Revision.ToString(CultureInfo.InvariantCulture), result.EventId, result.CorrelationId);

            var fingerprint = Fingerprint(
                "ApplyEncounterResult",
                result.CorrelationId,
                result.QuestId,
                result.HookId,
                result.RealmId,
                result.Outcome.ToString(),
                result.EventId,
                result.SnapshotVersion,
                result.SnapshotReference);
            var draft = new SnapshotDraft(_snapshot)
            {
                EncounterStatus = Nvs01EncounterStatus.Resolved,
                CurrentEncounter = null,
                LastEncounterCorrelationId = result.CorrelationId,
                LastEncounterOutcome = result.Outcome,
                LastEncounterEventId = result.EventId,
                LastEncounterSnapshotVersion = result.SnapshotVersion,
                LastEncounterSnapshotReference = result.SnapshotReference,
                CurrentDialogueNodeId = string.Empty,
                PendingChoice = false,
                PendingSemanticActionId = string.Empty
            };
            var newIntents = new List<string>();

            switch (result.Outcome)
            {
                case NvsEncounterOutcome.Success:
                {
                    Nvs01Transition transition;
                    if (!Catalog.TryGetTransition(_snapshot.StateId, result.EventId, out transition))
                        return Reject("TRANSITION-INVALID", "transition", _snapshot.StateId + "/" + result.EventId, string.Empty, result.EventId, result.CorrelationId);
                    ApplyTransition(draft, transition, result.EventId);
                    AddConsequenceIntents(draft, result.EventId, newIntents);
                    break;
                }
                case NvsEncounterOutcome.Failure:
                {
                    Nvs01Transition transition;
                    if (!Catalog.TryGetTransition(_snapshot.StateId, result.EventId, out transition))
                        return Reject("TRANSITION-INVALID", "transition", _snapshot.StateId + "/" + result.EventId, string.Empty, result.EventId, result.CorrelationId);
                    ApplyTransition(draft, transition, result.EventId);
                    SetDialogueTarget(draft, FailureDialogue);
                    break;
                }
                case NvsEncounterOutcome.Cancelled:
                case NvsEncounterOutcome.Unavailable:
                    draft.StateId = InvestigateSkyCastle;
                    ActivateObjectivesForState(draft, InvestigateSkyCastle, result.EventId);
                    draft.PendingSemanticActionId = RetryArenaEvent;
                    break;
                default:
                    return Reject("EVENT-MISMATCH", "outcome", "supported encounter outcome", result.Outcome.ToString(), result.EventId, result.CorrelationId);
            }

            return CommitResult(result, fingerprint, draft, newIntents);
        }

        public Nvs01CommandDisposition Abandon(
            Nvs01CommandEnvelope command,
            bool encounterActive)
        {
            var fingerprint = CommandFingerprint(command, "Abandon", encounterActive ? "active" : "inactive");
            Nvs01CommandDisposition stopped;
            if (!TryPrepareCommand(command, fingerprint, AbandonEvent, out stopped)) return stopped;

            if (string.Equals(_snapshot.StateId, Completed, StringComparison.Ordinal))
                return Reject("TRANSITION-INVALID", "state", "nonterminal quest", _snapshot.StateId, AbandonEvent);
            if (encounterActive || _snapshot.CurrentEncounter != null ||
                _snapshot.EncounterStatus == Nvs01EncounterStatus.Requested ||
                _snapshot.EncounterStatus == Nvs01EncounterStatus.Active)
            {
                return Reject("TRANSITION-INVALID", "encounter", "inactive", "active", AbandonEvent, _snapshot.CurrentEncounter?.CorrelationId ?? string.Empty);
            }
            if (!Catalog.Abandonment.AllowedOutsideActiveEncounter)
                return Reject("TRANSITION-INVALID", "abandonment", "allowed outside encounter", "disabled", AbandonEvent);
            if (!string.Equals(command.ActorId, "PLAYER", StringComparison.Ordinal) ||
                !string.Equals(command.ContextId, Catalog.QuestId, StringComparison.Ordinal))
                return Reject("EVENT-MISMATCH", "actorContext", "PLAYER/" + Catalog.QuestId, command.ActorId + "/" + command.ContextId, AbandonEvent);

            if (IsCleanOfferedSnapshot(_snapshot)) return ReadOnlyDuplicate(AbandonEvent, null);

            var draft = new SnapshotDraft(_snapshot)
            {
                StateId = Catalog.Abandonment.ResultState,
                CurrentDialogueNodeId = string.Empty,
                PendingChoice = false,
                PendingSemanticActionId = string.Empty,
                EncounterStatus = Nvs01EncounterStatus.None,
                CurrentEncounter = null,
                LastEncounterCorrelationId = string.Empty,
                LastEncounterOutcome = null,
                LastEncounterEventId = string.Empty,
                LastEncounterSnapshotVersion = string.Empty,
                LastEncounterSnapshotReference = string.Empty
            };
            for (var index = 0; index < draft.Objectives.Count; index++)
            {
                var objective = draft.Objectives[index];
                draft.Objectives[index] = new Nvs01ObjectiveSnapshot(
                    objective.ObjectiveId,
                    string.Equals(objective.ObjectiveId, TalkObjective, StringComparison.Ordinal)
                        ? Nvs01ObjectiveStatus.Active
                        : Nvs01ObjectiveStatus.Inactive);
            }
            return CommitCommand(command, fingerprint, AbandonEvent, draft, null, EmptyIdentifiers);
        }

        public bool TryGetActiveEncounter(out NvsEncounterRequest request)
        {
            request = _snapshot.CurrentEncounter;
            return request != null &&
                   (_snapshot.EncounterStatus == Nvs01EncounterStatus.Requested ||
                    _snapshot.EncounterStatus == Nvs01EncounterStatus.Active);
        }

        public bool TryGetLocalizedText(string key, out string text)
        {
            if (key == null)
            {
                text = null;
                return false;
            }
            return Catalog.TryGetLocalization(key, out text);
        }

        private bool TryPrepareCommand(
            Nvs01CommandEnvelope command,
            string fingerprint,
            string eventId,
            out Nvs01CommandDisposition disposition)
        {
            if (command == null)
            {
                disposition = Reject("EVENT-MISMATCH", "command", "non-null command", string.Empty, eventId);
                return false;
            }

            if (_commitUncertain)
            {
                disposition = CommitFailed("COMMIT-UNCERTAIN", eventId, "reconciliation required");
                return false;
            }

            if (_snapshot.LastOperation != null &&
                string.Equals(_snapshot.LastOperation.OperationId, command.OperationId, StringComparison.Ordinal))
            {
                disposition = string.Equals(_snapshot.LastOperation.PayloadFingerprint, fingerprint, StringComparison.Ordinal)
                    ? VerifyDurableDuplicate(
                        _snapshot.LastOperation.EventId,
                        _snapshot.CurrentEncounter,
                        _snapshot.LastOperation.CorrelationId)
                    : Reject("EVENT-MISMATCH", "operation", _snapshot.LastOperation.PayloadFingerprint, fingerprint, eventId);
                return false;
            }

            if (!string.Equals(command.QuestId, _snapshot.QuestId, StringComparison.Ordinal) ||
                !string.Equals(command.ExpectedStateId, _snapshot.StateId, StringComparison.Ordinal) ||
                command.ExpectedRevision != _snapshot.Revision)
            {
                disposition = Reject(
                    "EVENT-MISMATCH",
                    "expectedStateRevision",
                    _snapshot.StateId + "/" + _snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                    command.ExpectedStateId + "/" + command.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
                    eventId);
                return false;
            }

            if (_snapshot.Revision == long.MaxValue)
            {
                disposition = Reject(
                    "TRANSITION-INVALID",
                    "revision",
                    "less than Int64.MaxValue",
                    _snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                    eventId);
                return false;
            }

            disposition = null;
            return true;
        }

        private Nvs01CommandDisposition CommitCommand(
            Nvs01CommandEnvelope command,
            string fingerprint,
            string eventId,
            SnapshotDraft draft,
            NvsEncounterRequest request,
            IList<string> newIntents)
        {
            var nextRevision = checked(_snapshot.Revision + 1);
            draft.LastOperation = new Nvs01OperationReceipt(
                command.OperationId,
                fingerprint,
                Nvs01CommandStatus.Committed,
                nextRevision,
                draft.StateId,
                eventId,
                request?.CorrelationId ?? draft.LastEncounterCorrelationId);
            var candidate = draft.Build(nextRevision);
            return CommitCandidate(_snapshot, candidate, eventId, request, newIntents);
        }

        private Nvs01CommandDisposition CommitResult(
            NvsEncounterResult result,
            string fingerprint,
            SnapshotDraft draft,
            IList<string> newIntents)
        {
            var nextRevision = checked(_snapshot.Revision + 1);
            draft.LastOperation = new Nvs01OperationReceipt(
                result.CorrelationId,
                fingerprint,
                Nvs01CommandStatus.Committed,
                nextRevision,
                draft.StateId,
                result.EventId,
                result.CorrelationId);
            var candidate = draft.Build(nextRevision);
            return CommitCandidate(_snapshot, candidate, result.EventId, null, newIntents);
        }

        private Nvs01CommandDisposition CommitCandidate(
            Nvs01QuestSnapshot expected,
            Nvs01QuestSnapshot candidate,
            string triggerEventId,
            NvsEncounterRequest request,
            IList<string> newIntents)
        {
            var plan = new Nvs01MutationPlan(expected, candidate, triggerEventId, newIntents);
            Nvs01QuestSnapshot committed;
            Nvs01RuntimeDiagnostic diagnostic;
            bool succeeded;
            try
            {
                succeeded = _committer.TryCommit(plan, out committed, out diagnostic);
            }
            catch (Exception exception)
            {
                return CommitFailed("COMMIT-UNCERTAIN", triggerEventId, exception.GetType().Name);
            }

            if (!succeeded)
            {
                if (diagnostic != null &&
                    string.Equals(diagnostic.Code, Nvs01CatalogContract.DiagnosticCodePrefix + "COMMIT-UNCERTAIN", StringComparison.Ordinal))
                    _commitUncertain = true;
                return new Nvs01CommandDisposition(
                    Nvs01CommandStatus.CommitFailed,
                    expected,
                    diagnostic ?? Diagnostic("SAVE-FAILED", "commit", "committed candidate", "rejected", triggerEventId),
                    null,
                    EmptyIdentifiers);
            }

            if (committed == null || !SnapshotsEquivalent(candidate, committed))
                return CommitFailed("COMMIT-UNCERTAIN", triggerEventId, "candidate mismatch");

            try
            {
                ValidateSnapshotOrThrow(committed, nameof(committed));
            }
            catch (Exception exception)
            {
                return CommitFailed("COMMIT-UNCERTAIN", triggerEventId, exception.GetType().Name);
            }

            _snapshot = committed;
            return new Nvs01CommandDisposition(
                Nvs01CommandStatus.Committed,
                committed,
                null,
                request,
                newIntents);
        }

        private Nvs01CommandDisposition CommitFailed(string code, string eventId, string actual)
        {
            if (string.Equals(code, "COMMIT-UNCERTAIN", StringComparison.Ordinal) ||
                string.Equals(code, Nvs01CatalogContract.DiagnosticCodePrefix + "COMMIT-UNCERTAIN", StringComparison.Ordinal))
                _commitUncertain = true;
            return new Nvs01CommandDisposition(
                Nvs01CommandStatus.CommitFailed,
                _snapshot,
                Diagnostic(code, "commit", "verified candidate", actual, eventId),
                null,
                EmptyIdentifiers);
        }

        private Nvs01CommandDisposition ValidateResultAgainstRequest(
            NvsEncounterRequest request,
            NvsEncounterResult result)
        {
            string expectedEvent;
            try
            {
                expectedEvent = request.GetEventId(result.Outcome);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Reject("EVENT-MISMATCH", "outcome", "supported outcome", result.Outcome.ToString(), result.EventId, result.CorrelationId);
            }

            if (result.ContractVersion != request.ContractVersion ||
                !string.Equals(result.CorrelationId, request.CorrelationId, StringComparison.Ordinal) ||
                !string.Equals(result.QuestId, request.QuestId, StringComparison.Ordinal) ||
                !string.Equals(result.HookId, request.HookId, StringComparison.Ordinal) ||
                !string.Equals(result.RealmId, request.RealmId, StringComparison.Ordinal) ||
                !string.Equals(result.EventId, expectedEvent, StringComparison.Ordinal))
            {
                return Reject(
                    "EVENT-MISMATCH",
                    "result",
                    request.CorrelationId + "/" + request.HookId + "/" + request.RealmId + "/" + expectedEvent,
                    result.CorrelationId + "/" + result.HookId + "/" + result.RealmId + "/" + result.EventId,
                    result.EventId,
                    result.CorrelationId);
            }
            return null;
        }

        private bool LastResultMatches(NvsEncounterResult result)
        {
            return string.Equals(result.QuestId, _snapshot.QuestId, StringComparison.Ordinal) &&
                   string.Equals(result.HookId, ArenaHook, StringComparison.Ordinal) &&
                   string.Equals(result.RealmId, _snapshot.CommittedRealmId, StringComparison.Ordinal) &&
                   _snapshot.LastEncounterOutcome.HasValue && _snapshot.LastEncounterOutcome.Value == result.Outcome &&
                   string.Equals(_snapshot.LastEncounterEventId, result.EventId, StringComparison.Ordinal) &&
                   string.Equals(_snapshot.LastEncounterSnapshotVersion, result.SnapshotVersion, StringComparison.Ordinal) &&
                   string.Equals(_snapshot.LastEncounterSnapshotReference, result.SnapshotReference, StringComparison.Ordinal);
        }

        private bool TryValidateRealm(Nvs01RealmContext context, out Nvs01CommandDisposition failure)
        {
            if (context == null || context.Status == Nvs01RealmContextStatus.Unavailable)
            {
                failure = DependencyUnavailable("realm", "committed valid realm", string.Empty, "REALM_CONTEXT");
                return false;
            }
            if (!context.IsCommittedValid ||
                !Catalog.Placement.EligibleRealmIds.Contains(context.RealmId, StringComparer.Ordinal))
            {
                failure = Reject("EVENT-MISMATCH", "realm", "eligible committed realm", context.RealmId, "REALM_CONTEXT");
                return false;
            }
            failure = null;
            return true;
        }

        private bool TryValidateArenaCapabilities(
            Nvs01CapabilitySnapshot capabilities,
            string eventId,
            out Nvs01CommandDisposition failure)
        {
            if (capabilities == null)
            {
                failure = DependencyUnavailable("capabilities", "arena capability snapshot", string.Empty, eventId);
                return false;
            }
            foreach (var capability in RequiredArenaCapabilities)
            {
                if (capabilities.IsAvailable(capability)) continue;
                failure = DependencyUnavailable("capability", capability, "unavailable", eventId);
                return false;
            }
            failure = null;
            return true;
        }

        private bool TryCreateEncounterRequest(
            string realmId,
            out NvsEncounterRequest request,
            out Nvs01CommandDisposition failure)
        {
            request = null;
            failure = null;
            try
            {
                var requestId = _guidFactory();
                var correlationId = _guidFactory();
                request = new NvsEncounterRequest(
                    Nvs01RuntimeContract.ContractVersion,
                    requestId,
                    correlationId,
                    Catalog.QuestId,
                    InvestigateSkyCastle,
                    ArenaObjective,
                    ArenaHook,
                    ArenaLocation,
                    realmId,
                    ArenaSuccessEvent,
                    ArenaFailureEvent,
                    ArenaCancelledEvent,
                    ArenaUnavailableEvent,
                    ReturnScene);
                if (_snapshot.LastEncounterCorrelationId.Length > 0 &&
                    string.Equals(request.CorrelationId, _snapshot.LastEncounterCorrelationId, StringComparison.Ordinal))
                {
                    throw new ArgumentException("A retry correlation must differ from the prior encounter correlation.", nameof(correlationId));
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = Reject(
                    "TRANSITION-INVALID",
                    "identity",
                    "distinct non-empty request/correlation GUIDs and a new retry correlation",
                    exception.GetType().Name,
                    RequestArenaEvent);
                request = null;
                return false;
            }
        }

        private void ApplyTransition(SnapshotDraft draft, Nvs01Transition transition, string eventId)
        {
            draft.StateId = transition.To;
            ActivateObjectivesForState(draft, transition.To, eventId);
        }

        private void ActivateObjectivesForState(SnapshotDraft draft, string stateId, string eventId)
        {
            for (var index = 0; index < Catalog.Objectives.Count; index++)
            {
                var definition = Catalog.Objectives[index];
                var current = draft.Objectives[index];
                var status = current.Status;
                if (string.Equals(definition.CompletesOn, eventId, StringComparison.Ordinal))
                    status = Nvs01ObjectiveStatus.Completed;
                else if (status != Nvs01ObjectiveStatus.Completed &&
                         string.Equals(definition.ActivatesIn, stateId, StringComparison.Ordinal))
                    status = Nvs01ObjectiveStatus.Active;
                draft.Objectives[index] = new Nvs01ObjectiveSnapshot(current.ObjectiveId, status);
            }
        }

        private void SetDialogueTarget(SnapshotDraft draft, string target)
        {
            if (string.Equals(target, "end", StringComparison.Ordinal))
            {
                draft.CurrentDialogueNodeId = string.Empty;
                draft.PendingChoice = false;
                draft.PendingSemanticActionId = string.Empty;
                return;
            }

            Nvs01DialogueNode targetNode;
            if (!Catalog.TryGetDialogue(target, out targetNode))
                throw new InvalidOperationException("Validated dialogue target is unavailable.");
            draft.CurrentDialogueNodeId = targetNode.Id;
            draft.PendingChoice = targetNode.Choices.Count > 0;
            draft.PendingSemanticActionId = targetNode.SemanticAction ?? string.Empty;
        }

        private void AddConsequenceIntents(SnapshotDraft draft, string triggerEventId, IList<string> newlyAdded)
        {
            foreach (var consequence in Catalog.Consequences)
            {
                if (!string.Equals(consequence.Trigger, triggerEventId, StringComparison.Ordinal) ||
                    draft.ConsequenceIntentIds.Contains(consequence.Id, StringComparer.Ordinal))
                {
                    continue;
                }
                draft.ConsequenceIntentIds.Add(consequence.Id);
                newlyAdded.Add(consequence.Id);
            }
        }

        private Nvs01CommandDisposition VerifyDurableDuplicate(
            string eventId,
            NvsEncounterRequest request,
            string correlationId = "")
        {
            Nvs01OperationReceipt operation = _snapshot.LastOperation;
            var verifier = _committer as INvs01ReplayVerifier;
            if (operation == null ||
                verifier == null)
            {
                return CommitFailed(
                    "SAVE-READ-ONLY",
                    eventId,
                    "durable replay verifier unavailable");
            }

            bool verified;
            Nvs01QuestSnapshot durable;
            Nvs01RuntimeDiagnostic diagnostic;
            try
            {
                verified = verifier.TryVerifyReplay(
                    _snapshot,
                    operation.OperationId,
                    operation.PayloadFingerprint,
                    out durable,
                    out diagnostic);
            }
            catch (Exception exception)
            {
                return CommitFailed(
                    "COMMIT-UNCERTAIN",
                    eventId,
                    exception.GetType().Name);
            }

            if (!verified)
            {
                if (diagnostic != null &&
                    string.Equals(
                        diagnostic.Code,
                        Nvs01CatalogContract.DiagnosticCodePrefix +
                        "COMMIT-UNCERTAIN",
                        StringComparison.Ordinal))
                {
                    _commitUncertain = true;
                }
                return new Nvs01CommandDisposition(
                    Nvs01CommandStatus.CommitFailed,
                    _snapshot,
                    diagnostic ?? Diagnostic(
                        "SAVE-READ-ONLY",
                        "replay",
                        "current authority",
                        "unverified",
                        eventId,
                        correlationId),
                    null,
                    EmptyIdentifiers);
            }

            if (durable == null ||
                !SnapshotsEquivalent(_snapshot, durable))
            {
                return CommitFailed(
                    "COMMIT-UNCERTAIN",
                    eventId,
                    "verified replay snapshot mismatch");
            }

            _snapshot = durable;
            return ReadOnlyDuplicate(eventId, request, correlationId);
        }

        private Nvs01CommandDisposition ReadOnlyDuplicate(
            string eventId,
            NvsEncounterRequest request,
            string correlationId = "")
        {
            return new Nvs01CommandDisposition(
                Nvs01CommandStatus.Duplicate,
                _snapshot,
                Diagnostic("EVENT-DUPLICATE", "event", "new event", eventId, eventId, correlationId),
                request,
                EmptyIdentifiers);
        }

        private Nvs01CommandDisposition Reject(
            string code,
            string field,
            string expected,
            string actual,
            string eventId,
            string correlationId = "")
        {
            return new Nvs01CommandDisposition(
                Nvs01CommandStatus.Rejected,
                _snapshot,
                Diagnostic(code, field, expected, actual, eventId, correlationId),
                null,
                EmptyIdentifiers);
        }

        private Nvs01CommandDisposition DependencyUnavailable(
            string field,
            string expected,
            string actual,
            string eventId)
        {
            return new Nvs01CommandDisposition(
                Nvs01CommandStatus.DependencyUnavailable,
                _snapshot,
                Diagnostic("DEPENDENCY-UNAVAILABLE", field, expected, actual, eventId),
                null,
                EmptyIdentifiers);
        }

        private Nvs01RuntimeDiagnostic Diagnostic(
            string code,
            string field,
            string expected,
            string actual,
            string eventId,
            string correlationId = "")
        {
            return new Nvs01RuntimeDiagnostic(
                code,
                field,
                expected,
                actual,
                _snapshot.StateId,
                eventId ?? string.Empty,
                correlationId ?? string.Empty);
        }

        private string CommandFingerprint(Nvs01CommandEnvelope command, params string[] semanticPayload)
        {
            if (command == null) return Fingerprint(new[] { "null-command" }.Concat(semanticPayload).ToArray());
            var parts = new List<string>
            {
                command.QuestId,
                command.ExpectedStateId,
                command.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
                command.ActorId,
                command.ContextId
            };
            parts.AddRange(semanticPayload);
            return Fingerprint(parts.ToArray());
        }

        private static string RealmFingerprint(Nvs01RealmContext context) =>
            context == null ? "null" : context.Status + ":" + context.RealmId;

        private static string CapabilityFingerprint(Nvs01CapabilitySnapshot capabilities)
        {
            if (capabilities == null) return "null";
            return string.Join(",", capabilities.Availability
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Key + "=" + (entry.Value ? "1" : "0")));
        }

        private static string Fingerprint(params string[] parts)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(string.Join("\u001f", parts ?? EmptyIdentifiers));
                return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static string ResultFingerprint(Nvs01QuestSnapshot snapshot) =>
            snapshot.LastEncounterCorrelationId + "/" + snapshot.LastEncounterEventId + "/" +
            snapshot.LastEncounterSnapshotVersion + "/" + snapshot.LastEncounterSnapshotReference;

        private bool IsCleanOfferedSnapshot(Nvs01QuestSnapshot snapshot)
        {
            if (!string.Equals(snapshot.StateId, Offered, StringComparison.Ordinal) ||
                snapshot.CurrentDialogueNodeId.Length > 0 || snapshot.PendingChoice ||
                snapshot.PendingSemanticActionId.Length > 0 || snapshot.CurrentEncounter != null)
                return false;
            Nvs01ObjectiveStatus status;
            return snapshot.TryGetObjectiveStatus(TalkObjective, out status) && status == Nvs01ObjectiveStatus.Active &&
                   snapshot.TryGetObjectiveStatus(ArenaObjective, out status) && status == Nvs01ObjectiveStatus.Inactive &&
                   snapshot.TryGetObjectiveStatus(ReportObjective, out status) && status == Nvs01ObjectiveStatus.Inactive;
        }

        internal static bool TryValidateSnapshot(
            Nvs01QuestSnapshot snapshot,
            out string error)
        {
            if (snapshot == null)
            {
                error = "Snapshot is missing.";
                return false;
            }

            try
            {
                ValidateSnapshotOrThrow(
                    snapshot,
                    Nvs01CatalogContract.PacketVersion,
                    Nvs01CatalogContract.CanonicalSha256,
                    Nvs01CatalogContract.QuestId,
                    nameof(snapshot));
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private void ValidateSnapshotOrThrow(
            Nvs01QuestSnapshot snapshot,
            string parameterName) =>
            ValidateSnapshotOrThrow(
                snapshot,
                Catalog.PacketVersion,
                _verifiedCatalog.CanonicalSha256,
                Catalog.QuestId,
                parameterName);

        private static void ValidateSnapshotOrThrow(
            Nvs01QuestSnapshot snapshot,
            string packetVersion,
            string packetSha256,
            string questId,
            string parameterName)
        {
            if (!string.Equals(snapshot.PacketVersion, packetVersion, StringComparison.Ordinal) ||
                !string.Equals(snapshot.PacketSha256, packetSha256, StringComparison.Ordinal) ||
                !string.Equals(snapshot.QuestId, questId, StringComparison.Ordinal))
                throw new ArgumentException("Snapshot catalog identity mismatch.", parameterName);
            if (!Nvs01CatalogValidator.IsExactCurrentStateId(snapshot.StateId))
                throw new ArgumentException("Snapshot state is not in the verified catalog.", parameterName);
            if (snapshot.Objectives.Count != Nvs01CatalogValidator.ExactCurrentObjectiveCount)
                throw new ArgumentException("Snapshot objective count mismatch.", parameterName);
            for (var index = 0; index < snapshot.Objectives.Count; index++)
            {
                if (!Nvs01CatalogValidator.IsExactCurrentObjectiveId(
                        index,
                        snapshot.Objectives[index].ObjectiveId))
                    throw new ArgumentException("Snapshot objective order mismatch.", parameterName);
            }
            if (snapshot.CurrentDialogueNodeId.Length > 0 &&
                !Nvs01CatalogValidator.IsExactCurrentDialogueId(
                    snapshot.CurrentDialogueNodeId))
                throw new ArgumentException("Snapshot dialogue is not in the verified catalog.", parameterName);
            if (snapshot.CommittedRealmId.Length > 0 &&
                !Nvs01CatalogValidator.IsExactCurrentEligibleRealmId(
                    snapshot.CommittedRealmId))
                throw new ArgumentException("Snapshot realm is not eligible.", parameterName);
            if (snapshot.CommittedRealmId.Length == 0 &&
                (!string.Equals(snapshot.StateId, Offered, StringComparison.Ordinal) ||
                 snapshot.Revision > 0 || snapshot.CurrentDialogueNodeId.Length > 0 ||
                 snapshot.CurrentEncounter != null || snapshot.ConsequenceIntentIds.Count > 0))
                throw new ArgumentException("A progressed snapshot requires a committed realm.", parameterName);
            foreach (var intent in snapshot.ConsequenceIntentIds)
            {
                if (!Nvs01CatalogValidator.IsExactCurrentConsequenceId(intent))
                    throw new ArgumentException("Snapshot consequence intent is not in the catalog.", parameterName);
            }
            if (snapshot.CurrentEncounter != null)
            {
                var request = snapshot.CurrentEncounter;
                if (!string.Equals(snapshot.StateId, InvestigateSkyCastle, StringComparison.Ordinal) ||
                    !string.Equals(request.StateId, InvestigateSkyCastle, StringComparison.Ordinal) ||
                    !string.Equals(request.QuestId, snapshot.QuestId, StringComparison.Ordinal) ||
                    !string.Equals(request.ObjectiveId, ArenaObjective, StringComparison.Ordinal) ||
                    !string.Equals(request.RealmId, snapshot.CommittedRealmId, StringComparison.Ordinal) ||
                    !string.Equals(request.HookId, ArenaHook, StringComparison.Ordinal) ||
                    !string.Equals(request.LocationId, ArenaLocation, StringComparison.Ordinal) ||
                    !string.Equals(request.SuccessEventId, ArenaSuccessEvent, StringComparison.Ordinal) ||
                    !string.Equals(request.FailureEventId, ArenaFailureEvent, StringComparison.Ordinal) ||
                    !string.Equals(request.CancelledEventId, ArenaCancelledEvent, StringComparison.Ordinal) ||
                    !string.Equals(request.UnavailableEventId, ArenaUnavailableEvent, StringComparison.Ordinal) ||
                    !string.Equals(request.ReturnScene, ReturnScene, StringComparison.Ordinal))
                    throw new ArgumentException("Snapshot encounter request mismatch.", parameterName);
            }
            if (snapshot.LastEncounterCorrelationId.Length == 0)
            {
                if (snapshot.LastEncounterOutcome.HasValue || snapshot.LastEncounterEventId.Length > 0 ||
                    snapshot.LastEncounterSnapshotVersion.Length > 0 || snapshot.LastEncounterSnapshotReference.Length > 0)
                    throw new ArgumentException("Snapshot last-result fields are inconsistent.", parameterName);
            }
            else if (!snapshot.LastEncounterOutcome.HasValue || snapshot.LastEncounterEventId.Length == 0)
            {
                throw new ArgumentException("Snapshot last-result identity is incomplete.", parameterName);
            }
            else
            {
                if (!IsCanonicalGuid(snapshot.LastEncounterCorrelationId))
                    throw new ArgumentException("Snapshot last-result correlation is not a canonical GUID.", parameterName);
                var expectedEvent = EventForOutcome(snapshot.LastEncounterOutcome.Value);
                if (!string.Equals(snapshot.LastEncounterEventId, expectedEvent, StringComparison.Ordinal))
                    throw new ArgumentException("Snapshot last-result event does not match its outcome.", parameterName);
            }
            if (snapshot.LastOperation != null)
            {
                if (snapshot.LastOperation.Revision != snapshot.Revision ||
                    snapshot.LastOperation.Status != Nvs01CommandStatus.Committed ||
                    !string.Equals(snapshot.LastOperation.StateId, snapshot.StateId, StringComparison.Ordinal) ||
                    snapshot.LastOperation.PayloadFingerprint.Length != 64 ||
                    snapshot.LastOperation.PayloadFingerprint.Any(value => !Uri.IsHexDigit(value)) ||
                    (snapshot.LastOperation.CorrelationId.Length > 0 && !IsCanonicalGuid(snapshot.LastOperation.CorrelationId)))
                    throw new ArgumentException("Snapshot operation receipt is inconsistent.", parameterName);
            }
            else if (snapshot.Revision > 0)
            {
                throw new ArgumentException("A progressed snapshot requires an operation receipt.", parameterName);
            }

            ValidateObjectiveTopology(snapshot, parameterName);
            ValidateEncounterTopology(snapshot, parameterName);
            ValidateDialogueTopology(snapshot, parameterName);
            ValidatePendingAction(snapshot, parameterName);
            ValidateConsequenceTopology(snapshot, parameterName);
        }

        private static void ValidateEncounterTopology(Nvs01QuestSnapshot snapshot, string parameterName)
        {
            var hasLastResult = snapshot.LastEncounterCorrelationId.Length > 0;
            if (snapshot.CurrentEncounter != null && hasLastResult &&
                string.Equals(snapshot.CurrentEncounter.CorrelationId, snapshot.LastEncounterCorrelationId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Current and prior encounter correlations must be distinct.", parameterName);
            }

            switch (snapshot.EncounterStatus)
            {
                case Nvs01EncounterStatus.None:
                    if (snapshot.CurrentEncounter != null || hasLastResult ||
                        (!string.Equals(snapshot.StateId, Offered, StringComparison.Ordinal) &&
                         !string.Equals(snapshot.StateId, TalkToValerius, StringComparison.Ordinal)))
                    {
                        throw new ArgumentException("A pre-encounter snapshot has inconsistent encounter state.", parameterName);
                    }
                    break;
                case Nvs01EncounterStatus.Requested:
                case Nvs01EncounterStatus.Active:
                    if (snapshot.CurrentEncounter == null ||
                        !string.Equals(snapshot.StateId, InvestigateSkyCastle, StringComparison.Ordinal) ||
                        (snapshot.LastEncounterOutcome.HasValue &&
                         snapshot.LastEncounterOutcome.Value == NvsEncounterOutcome.Success))
                    {
                        throw new ArgumentException("An active encounter snapshot has inconsistent state or history.", parameterName);
                    }
                    break;
                case Nvs01EncounterStatus.Resolved:
                    if (snapshot.CurrentEncounter != null || !hasLastResult || !snapshot.LastEncounterOutcome.HasValue)
                        throw new ArgumentException("A resolved encounter requires one complete prior result.", parameterName);
                    switch (snapshot.LastEncounterOutcome.Value)
                    {
                        case NvsEncounterOutcome.Success:
                            if (!string.Equals(snapshot.StateId, ReportToValerius, StringComparison.Ordinal) &&
                                !string.Equals(snapshot.StateId, Completed, StringComparison.Ordinal))
                                throw new ArgumentException("A successful result requires report or completed state.", parameterName);
                            break;
                        case NvsEncounterOutcome.Failure:
                            if (!string.Equals(snapshot.StateId, Failed, StringComparison.Ordinal))
                                throw new ArgumentException("A failed result requires the transient failure state.", parameterName);
                            break;
                        case NvsEncounterOutcome.Cancelled:
                        case NvsEncounterOutcome.Unavailable:
                            if (!string.Equals(snapshot.StateId, InvestigateSkyCastle, StringComparison.Ordinal))
                                throw new ArgumentException("A cancelled or unavailable result requires recoverable investigate state.", parameterName);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(parameterName);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateObjectiveTopology(Nvs01QuestSnapshot snapshot, string parameterName)
        {
            Nvs01ObjectiveStatus talk;
            Nvs01ObjectiveStatus arena;
            Nvs01ObjectiveStatus report;
            snapshot.TryGetObjectiveStatus(TalkObjective, out talk);
            snapshot.TryGetObjectiveStatus(ArenaObjective, out arena);
            snapshot.TryGetObjectiveStatus(ReportObjective, out report);
            var valid = false;
            switch (snapshot.StateId)
            {
                case Offered:
                    valid = talk == Nvs01ObjectiveStatus.Active && arena == Nvs01ObjectiveStatus.Inactive && report == Nvs01ObjectiveStatus.Inactive;
                    break;
                case TalkToValerius:
                    valid = talk == Nvs01ObjectiveStatus.Completed && arena == Nvs01ObjectiveStatus.Inactive && report == Nvs01ObjectiveStatus.Inactive;
                    break;
                case InvestigateSkyCastle:
                case Failed:
                    valid = talk == Nvs01ObjectiveStatus.Completed && arena == Nvs01ObjectiveStatus.Active && report == Nvs01ObjectiveStatus.Inactive;
                    break;
                case ReportToValerius:
                    valid = talk == Nvs01ObjectiveStatus.Completed && arena == Nvs01ObjectiveStatus.Completed && report == Nvs01ObjectiveStatus.Active;
                    break;
                case Completed:
                    valid = talk == Nvs01ObjectiveStatus.Completed && arena == Nvs01ObjectiveStatus.Completed && report == Nvs01ObjectiveStatus.Completed;
                    break;
            }
            if (!valid) throw new ArgumentException("Snapshot objective topology does not match its state.", parameterName);
        }

        private static void ValidatePendingAction(Nvs01QuestSnapshot snapshot, string parameterName)
        {
            if (snapshot.PendingSemanticActionId.Length == 0) return;
            if (!string.Equals(snapshot.PendingSemanticActionId, RequestArenaEvent, StringComparison.Ordinal) &&
                !string.Equals(snapshot.PendingSemanticActionId, RetryArenaEvent, StringComparison.Ordinal))
                throw new ArgumentException("Snapshot semantic action is unsupported.", parameterName);

            if (snapshot.CurrentDialogueNodeId.Length == 0)
            {
                if (!string.Equals(snapshot.StateId, InvestigateSkyCastle, StringComparison.Ordinal) ||
                    !string.Equals(snapshot.PendingSemanticActionId, RetryArenaEvent, StringComparison.Ordinal) ||
                    !snapshot.LastEncounterOutcome.HasValue ||
                    (snapshot.LastEncounterOutcome.Value != NvsEncounterOutcome.Cancelled &&
                     snapshot.LastEncounterOutcome.Value != NvsEncounterOutcome.Unavailable))
                    throw new ArgumentException("Technical retry action is not in a recoverable encounter state.", parameterName);
                return;
            }

            var declared =
                string.Equals(snapshot.CurrentDialogueNodeId, ArenaStartDialogue, StringComparison.Ordinal) &&
                string.Equals(snapshot.PendingSemanticActionId, RequestArenaEvent, StringComparison.Ordinal) ||
                string.Equals(snapshot.CurrentDialogueNodeId, FailureDialogue, StringComparison.Ordinal) &&
                string.Equals(snapshot.PendingSemanticActionId, RetryArenaEvent, StringComparison.Ordinal);
            if (!declared) throw new ArgumentException("Snapshot semantic action is not declared by the current node.", parameterName);
        }

        private static void ValidateDialogueTopology(Nvs01QuestSnapshot snapshot, string parameterName)
        {
            var hasDialogue = snapshot.CurrentDialogueNodeId.Length > 0;
            var hasAction = snapshot.PendingSemanticActionId.Length > 0;
            var valid = false;
            switch (snapshot.StateId)
            {
                case Offered:
                    valid = (!hasDialogue && !snapshot.PendingChoice && !hasAction) ||
                            (string.Equals(snapshot.CurrentDialogueNodeId, OfferDialogue, StringComparison.Ordinal) &&
                             snapshot.PendingChoice && !hasAction);
                    break;
                case TalkToValerius:
                    valid = ((string.Equals(snapshot.CurrentDialogueNodeId, "DLG_OMEN_1_START", StringComparison.Ordinal) ||
                              string.Equals(snapshot.CurrentDialogueNodeId, "DLG_OMEN_1_LORE", StringComparison.Ordinal) ||
                              string.Equals(snapshot.CurrentDialogueNodeId, "DLG_OMEN_1_GO", StringComparison.Ordinal)) &&
                             snapshot.PendingChoice && !hasAction) ||
                            (string.Equals(snapshot.CurrentDialogueNodeId, ArenaStartDialogue, StringComparison.Ordinal) &&
                             !snapshot.PendingChoice && string.Equals(snapshot.PendingSemanticActionId, RequestArenaEvent, StringComparison.Ordinal));
                    break;
                case InvestigateSkyCastle:
                    valid = (snapshot.CurrentEncounter != null &&
                             !snapshot.PendingChoice && !hasAction &&
                             ((snapshot.LastEncounterCorrelationId.Length == 0 &&
                               string.Equals(snapshot.CurrentDialogueNodeId, ArenaStartDialogue, StringComparison.Ordinal)) ||
                              (snapshot.LastEncounterCorrelationId.Length > 0 && !hasDialogue))) ||
                            (snapshot.CurrentEncounter == null && !hasDialogue && !snapshot.PendingChoice &&
                             string.Equals(snapshot.PendingSemanticActionId, RetryArenaEvent, StringComparison.Ordinal));
                    break;
                case Failed:
                    valid = string.Equals(snapshot.CurrentDialogueNodeId, FailureDialogue, StringComparison.Ordinal) &&
                            ((snapshot.PendingChoice && !hasAction) ||
                             (!snapshot.PendingChoice && string.Equals(snapshot.PendingSemanticActionId, RetryArenaEvent, StringComparison.Ordinal)));
                    break;
                case ReportToValerius:
                    valid = (!hasDialogue && !snapshot.PendingChoice && !hasAction) ||
                            (string.Equals(snapshot.CurrentDialogueNodeId, ReportDialogue, StringComparison.Ordinal) &&
                             snapshot.PendingChoice && !hasAction);
                    break;
                case Completed:
                    valid = (!hasDialogue && !snapshot.PendingChoice && !hasAction) ||
                            (string.Equals(snapshot.CurrentDialogueNodeId, ReportConclusionDialogue, StringComparison.Ordinal) &&
                             snapshot.PendingChoice && !hasAction);
                    break;
            }
            if (!valid) throw new ArgumentException("Snapshot dialogue surface does not match its state.", parameterName);

            if (!hasDialogue) return;

            if (snapshot.PendingChoice &&
                (string.Equals(snapshot.CurrentDialogueNodeId, ArenaStartDialogue, StringComparison.Ordinal) ||
                 !Nvs01CatalogValidator.IsExactCurrentDialogueId(
                     snapshot.CurrentDialogueNodeId)))
                throw new ArgumentException("Snapshot marks a choice pending on a node without choices.", parameterName);
        }

        private static void ValidateConsequenceTopology(Nvs01QuestSnapshot snapshot, string parameterName)
        {
            var hasTear = snapshot.ConsequenceIntentIds.Contains(TearIntent, StringComparer.Ordinal);
            var reportIntentCount = snapshot.ConsequenceIntentIds.Count(intent =>
                string.Equals(intent, "GRANT_GOLD_500", StringComparison.Ordinal) ||
                string.Equals(intent, "GRANT_VALERIUS_AFFINITY_5", StringComparison.Ordinal) ||
                string.Equals(intent, "COMPLETE_OMEN_1", StringComparison.Ordinal) ||
                string.Equals(intent, "UNLOCK_REALM_CHAPTER_1", StringComparison.Ordinal));

            if ((string.Equals(snapshot.StateId, ReportToValerius, StringComparison.Ordinal) ||
                 string.Equals(snapshot.StateId, Completed, StringComparison.Ordinal)) && !hasTear)
                throw new ArgumentException("Post-success state requires the retained Tear intent.", parameterName);
            if (reportIntentCount != 0 && reportIntentCount != 4)
                throw new ArgumentException("Report consequence intents must be absent or complete.", parameterName);
            if (reportIntentCount == 4 && !string.Equals(snapshot.StateId, Completed, StringComparison.Ordinal))
                throw new ArgumentException("Report consequence intents require completion.", parameterName);
            if (string.Equals(snapshot.StateId, Completed, StringComparison.Ordinal) && (!hasTear || reportIntentCount != 4))
                throw new ArgumentException("Completed state requires all five consequence intents.", parameterName);
        }

        private static string EventForOutcome(NvsEncounterOutcome outcome)
        {
            switch (outcome)
            {
                case NvsEncounterOutcome.Success:
                    return ArenaSuccessEvent;
                case NvsEncounterOutcome.Failure:
                    return ArenaFailureEvent;
                case NvsEncounterOutcome.Cancelled:
                    return ArenaCancelledEvent;
                case NvsEncounterOutcome.Unavailable:
                    return ArenaUnavailableEvent;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }

        private static bool IsCanonicalGuid(string value)
        {
            Guid parsed;
            return Guid.TryParseExact(value, "D", out parsed) && parsed != Guid.Empty &&
                   string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal);
        }

        private static bool SnapshotsEquivalent(Nvs01QuestSnapshot left, Nvs01QuestSnapshot right)
        {
            if (left == null || right == null) return left == right;
            if (!string.Equals(left.PacketVersion, right.PacketVersion, StringComparison.Ordinal) ||
                !string.Equals(left.PacketSha256, right.PacketSha256, StringComparison.Ordinal) ||
                !string.Equals(left.QuestId, right.QuestId, StringComparison.Ordinal) ||
                left.Revision != right.Revision ||
                !string.Equals(left.StateId, right.StateId, StringComparison.Ordinal) ||
                !string.Equals(left.CurrentDialogueNodeId, right.CurrentDialogueNodeId, StringComparison.Ordinal) ||
                left.PendingChoice != right.PendingChoice ||
                !string.Equals(left.PendingSemanticActionId, right.PendingSemanticActionId, StringComparison.Ordinal) ||
                !string.Equals(left.CommittedRealmId, right.CommittedRealmId, StringComparison.Ordinal) ||
                left.EncounterStatus != right.EncounterStatus ||
                !RequestEquivalent(left.CurrentEncounter, right.CurrentEncounter) ||
                !string.Equals(left.LastEncounterCorrelationId, right.LastEncounterCorrelationId, StringComparison.Ordinal) ||
                left.LastEncounterOutcome != right.LastEncounterOutcome ||
                !string.Equals(left.LastEncounterEventId, right.LastEncounterEventId, StringComparison.Ordinal) ||
                !string.Equals(left.LastEncounterSnapshotVersion, right.LastEncounterSnapshotVersion, StringComparison.Ordinal) ||
                !string.Equals(left.LastEncounterSnapshotReference, right.LastEncounterSnapshotReference, StringComparison.Ordinal) ||
                !ReceiptEquivalent(left.LastOperation, right.LastOperation) ||
                left.Objectives.Count != right.Objectives.Count ||
                left.ConsequenceIntentIds.Count != right.ConsequenceIntentIds.Count)
                return false;

            for (var index = 0; index < left.Objectives.Count; index++)
            {
                if (!string.Equals(left.Objectives[index].ObjectiveId, right.Objectives[index].ObjectiveId, StringComparison.Ordinal) ||
                    left.Objectives[index].Status != right.Objectives[index].Status) return false;
            }
            for (var index = 0; index < left.ConsequenceIntentIds.Count; index++)
            {
                if (!string.Equals(left.ConsequenceIntentIds[index], right.ConsequenceIntentIds[index], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static bool RequestEquivalent(NvsEncounterRequest left, NvsEncounterRequest right)
        {
            if (left == null || right == null) return left == right;
            return left.ContractVersion == right.ContractVersion &&
                   string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal) &&
                   string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal) &&
                   string.Equals(left.QuestId, right.QuestId, StringComparison.Ordinal) &&
                   string.Equals(left.StateId, right.StateId, StringComparison.Ordinal) &&
                   string.Equals(left.ObjectiveId, right.ObjectiveId, StringComparison.Ordinal) &&
                   string.Equals(left.HookId, right.HookId, StringComparison.Ordinal) &&
                   string.Equals(left.LocationId, right.LocationId, StringComparison.Ordinal) &&
                   string.Equals(left.RealmId, right.RealmId, StringComparison.Ordinal) &&
                   string.Equals(left.SuccessEventId, right.SuccessEventId, StringComparison.Ordinal) &&
                   string.Equals(left.FailureEventId, right.FailureEventId, StringComparison.Ordinal) &&
                   string.Equals(left.CancelledEventId, right.CancelledEventId, StringComparison.Ordinal) &&
                   string.Equals(left.UnavailableEventId, right.UnavailableEventId, StringComparison.Ordinal) &&
                   string.Equals(left.ReturnScene, right.ReturnScene, StringComparison.Ordinal);
        }

        private static bool ReceiptEquivalent(Nvs01OperationReceipt left, Nvs01OperationReceipt right)
        {
            if (left == null || right == null) return left == right;
            return string.Equals(left.OperationId, right.OperationId, StringComparison.Ordinal) &&
                   string.Equals(left.PayloadFingerprint, right.PayloadFingerprint, StringComparison.Ordinal) &&
                   left.Status == right.Status && left.Revision == right.Revision &&
                   string.Equals(left.StateId, right.StateId, StringComparison.Ordinal) &&
                   string.Equals(left.EventId, right.EventId, StringComparison.Ordinal) &&
                   // The save boundary verifies authority causality. Runtime
                   // domain equality omits it so exact replay can adopt the
                   // already-persisted causal receipt.
                   string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal);
        }

        private sealed class SnapshotDraft
        {
            internal SnapshotDraft(Nvs01QuestSnapshot source)
            {
                PacketVersion = source.PacketVersion;
                PacketSha256 = source.PacketSha256;
                QuestId = source.QuestId;
                StateId = source.StateId;
                Objectives = source.Objectives.ToList();
                CurrentDialogueNodeId = source.CurrentDialogueNodeId;
                PendingChoice = source.PendingChoice;
                PendingSemanticActionId = source.PendingSemanticActionId;
                CommittedRealmId = source.CommittedRealmId;
                EncounterStatus = source.EncounterStatus;
                CurrentEncounter = source.CurrentEncounter;
                LastEncounterCorrelationId = source.LastEncounterCorrelationId;
                LastEncounterOutcome = source.LastEncounterOutcome;
                LastEncounterEventId = source.LastEncounterEventId;
                LastEncounterSnapshotVersion = source.LastEncounterSnapshotVersion;
                LastEncounterSnapshotReference = source.LastEncounterSnapshotReference;
                LastOperation = source.LastOperation;
                ConsequenceIntentIds = source.ConsequenceIntentIds.ToList();
            }

            internal string PacketVersion;
            internal string PacketSha256;
            internal string QuestId;
            internal string StateId;
            internal List<Nvs01ObjectiveSnapshot> Objectives;
            internal string CurrentDialogueNodeId;
            internal bool PendingChoice;
            internal string PendingSemanticActionId;
            internal string CommittedRealmId;
            internal Nvs01EncounterStatus EncounterStatus;
            internal NvsEncounterRequest CurrentEncounter;
            internal string LastEncounterCorrelationId;
            internal NvsEncounterOutcome? LastEncounterOutcome;
            internal string LastEncounterEventId;
            internal string LastEncounterSnapshotVersion;
            internal string LastEncounterSnapshotReference;
            internal Nvs01OperationReceipt LastOperation;
            internal List<string> ConsequenceIntentIds;

            internal Nvs01QuestSnapshot Build(long revision)
            {
                return new Nvs01QuestSnapshot(
                    PacketVersion,
                    PacketSha256,
                    QuestId,
                    revision,
                    StateId,
                    Objectives,
                    CurrentDialogueNodeId,
                    PendingChoice,
                    PendingSemanticActionId,
                    CommittedRealmId,
                    EncounterStatus,
                    CurrentEncounter,
                    LastEncounterCorrelationId,
                    LastEncounterOutcome,
                    LastEncounterEventId,
                    LastEncounterSnapshotVersion,
                    LastEncounterSnapshotReference,
                    LastOperation,
                    ConsequenceIntentIds);
            }
        }
    }

    internal sealed class Nvs01InMemoryMutationCommitter :
        INvs01MutationCommitter,
        INvs01ReplayVerifier
    {
        private string _nextFailureCode;

        internal int AttemptCount { get; private set; }
        internal Nvs01MutationPlan LastPlan { get; private set; }

        internal void FailNextCommitForTests(string code = "SAVE-FAILED")
        {
            _nextFailureCode = string.IsNullOrWhiteSpace(code) ? "SAVE-FAILED" : code;
        }

        public bool TryCommit(
            Nvs01MutationPlan plan,
            out Nvs01QuestSnapshot committed,
            out Nvs01RuntimeDiagnostic diagnostic)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            AttemptCount++;
            LastPlan = plan;
            if (_nextFailureCode != null)
            {
                var code = _nextFailureCode;
                _nextFailureCode = null;
                committed = plan.Expected;
                diagnostic = new Nvs01RuntimeDiagnostic(
                    code,
                    "commit",
                    "accepted candidate",
                    "injected failure",
                    plan.Expected.StateId,
                    plan.TriggerEventId,
                    plan.Expected.CurrentEncounter?.CorrelationId ?? string.Empty);
                return false;
            }

            committed = plan.Candidate;
            diagnostic = null;
            return true;
        }

        public bool TryVerifyReplay(
            Nvs01QuestSnapshot snapshot,
            string operationId,
            string payloadFingerprint,
            out Nvs01QuestSnapshot verified,
            out Nvs01RuntimeDiagnostic diagnostic)
        {
            Nvs01OperationReceipt operation = snapshot?.LastOperation;
            bool exact = LastPlan != null &&
                operation != null &&
                string.Equals(
                    operation.OperationId,
                    operationId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    operation.PayloadFingerprint,
                    payloadFingerprint,
                    StringComparison.Ordinal) &&
                Nvs01ProgressCodec.Equivalent(
                    LastPlan.Candidate,
                    snapshot);
            if (exact)
            {
                verified = snapshot;
                diagnostic = null;
                return true;
            }

            verified = snapshot;
            diagnostic = new Nvs01RuntimeDiagnostic(
                "SAVE-READ-ONLY",
                "in-memory replay verification",
                "exact mutation committed by this committer",
                "unverified replay",
                snapshot?.StateId ?? string.Empty,
                operation?.EventId ?? string.Empty,
                operation?.CorrelationId ?? string.Empty);
            return false;
        }
    }
}
