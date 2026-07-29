using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;

namespace AL.UI.Kingdom
{
    public enum Nvs01KingdomViewStatus
    {
        Ready = 0,
        Attention = 1,
        Unavailable = 2,
        Completed = 3
    }

    public enum Nvs01KingdomActionKind
    {
        None = 0,
        SelectValerius = 1,
        InvokeSemanticAction = 2,
        ResumeEncounter = 3
    }

    public sealed class Nvs01KingdomChoice
    {
        public Nvs01KingdomChoice(string key, string label)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public string Key { get; }
        public string Label { get; }
    }

    public sealed class Nvs01KingdomView
    {
        public Nvs01KingdomView(
            Nvs01KingdomViewStatus status,
            string title,
            string description,
            string stateId,
            string objectiveText,
            string speakerName,
            string speakerRole,
            string dialogueText,
            IList<Nvs01KingdomChoice> choices,
            Nvs01KingdomActionKind primaryAction,
            string primaryActionLabel,
            bool canAbandon,
            NvsEncounterRequest encounterRequest,
            string playerMessage,
            string diagnosticCode)
        {
            if (!Enum.IsDefined(typeof(Nvs01KingdomViewStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(typeof(Nvs01KingdomActionKind), primaryAction))
                throw new ArgumentOutOfRangeException(nameof(primaryAction));

            Status = status;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            StateId = stateId ?? string.Empty;
            ObjectiveText = objectiveText ?? string.Empty;
            SpeakerName = speakerName ?? string.Empty;
            SpeakerRole = speakerRole ?? string.Empty;
            DialogueText = dialogueText ?? string.Empty;
            Choices = FreezeChoices(choices);
            PrimaryAction = primaryAction;
            PrimaryActionLabel = primaryActionLabel ?? string.Empty;
            CanAbandon = canAbandon;
            EncounterRequest = encounterRequest;
            PlayerMessage = playerMessage ?? string.Empty;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public Nvs01KingdomViewStatus Status { get; }
        public string Title { get; }
        public string Description { get; }
        public string StateId { get; }
        public string ObjectiveText { get; }
        public string SpeakerName { get; }
        public string SpeakerRole { get; }
        public string DialogueText { get; }
        public IReadOnlyList<Nvs01KingdomChoice> Choices { get; }
        public Nvs01KingdomActionKind PrimaryAction { get; }
        public string PrimaryActionLabel { get; }
        public bool CanAbandon { get; }
        public NvsEncounterRequest EncounterRequest { get; }
        public string PlayerMessage { get; }
        public string DiagnosticCode { get; }
        public bool HasDialogue => DialogueText.Length > 0;
        public bool HasDiagnostic => DiagnosticCode.Length > 0;

        public static Nvs01KingdomView CatalogUnavailable(Nvs01CatalogDiagnostic diagnostic)
        {
            string code = diagnostic?.Code ?? Nvs01CatalogContract.DiagnosticCodePrefix + "CATALOG-MISSING";
            return new Nvs01KingdomView(
                Nvs01KingdomViewStatus.Unavailable,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<Nvs01KingdomChoice>(),
                Nvs01KingdomActionKind.None,
                string.Empty,
                false,
                null,
                "Quest content is currently unavailable. (" + code + ")",
                code);
        }

        private static IReadOnlyList<Nvs01KingdomChoice> FreezeChoices(IList<Nvs01KingdomChoice> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new Nvs01KingdomChoice[source.Count];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Count; index++)
            {
                var choice = source[index] ?? throw new ArgumentException("Choice cannot be null.", nameof(source));
                if (!keys.Add(choice.Key))
                    throw new ArgumentException("Choice keys must be unique.", nameof(source));
                copy[index] = choice;
            }

            return new ReadOnlyCollection<Nvs01KingdomChoice>(copy);
        }
    }

    public sealed class Nvs01KingdomActionResult
    {
        public Nvs01KingdomActionResult(
            Nvs01KingdomView view,
            Nvs01CommandDisposition disposition,
            NvsEncounterRequest encounterRequest)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Disposition = disposition;
            EncounterRequest = encounterRequest;
        }

        public Nvs01KingdomView View { get; }
        public Nvs01CommandDisposition Disposition { get; }
        public NvsEncounterRequest EncounterRequest { get; }
        public bool ShouldEnterEncounter =>
            EncounterRequest != null &&
            ((Disposition != null && Disposition.IsCommitted) ||
             (Disposition == null && !View.HasDiagnostic));
    }

    public sealed class Nvs01KingdomPresenter
    {
        private const string Offered = "OFFERED";
        private const string ReportToValerius = "REPORT_TO_VALERIUS";
        private const string Completed = "COMPLETED";
        private const string PlayerActor = "PLAYER";

        private readonly INvs01QuestRuntime _runtime;
        private readonly Func<Nvs01RealmContext> _realmContextProvider;
        private readonly Func<Nvs01CapabilitySnapshot> _capabilityProvider;
        private readonly Func<string> _operationIdFactory;
        private readonly Func<long> _diagnosticTimestampFactory;
        private Nvs01RuntimeDiagnostic _lastDiagnostic;

        public Nvs01KingdomPresenter(
            INvs01QuestRuntime runtime,
            Func<Nvs01RealmContext> realmContextProvider,
            Func<Nvs01CapabilitySnapshot> capabilityProvider,
            Func<string> operationIdFactory,
            Func<long> diagnosticTimestampFactory)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _realmContextProvider = realmContextProvider ?? throw new ArgumentNullException(nameof(realmContextProvider));
            _capabilityProvider = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            _operationIdFactory = operationIdFactory ?? throw new ArgumentNullException(nameof(operationIdFactory));
            _diagnosticTimestampFactory = diagnosticTimestampFactory ?? throw new ArgumentNullException(nameof(diagnosticTimestampFactory));
        }

        public static Nvs01KingdomPresenter CreateInMemory(
            Nvs01VerifiedCatalog verifiedCatalog,
            Func<Nvs01RealmContext> realmContextProvider,
            Func<Nvs01CapabilitySnapshot> capabilityProvider)
        {
            if (verifiedCatalog == null) throw new ArgumentNullException(nameof(verifiedCatalog));
            var runtime = new Nvs01QuestRuntime(
                verifiedCatalog,
                new Nvs01InMemoryMutationCommitter(),
                () => Guid.NewGuid().ToString("D"));
            return new Nvs01KingdomPresenter(
                runtime,
                realmContextProvider,
                capabilityProvider,
                () => Guid.NewGuid().ToString("D"),
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public INvs01QuestRuntime Runtime => _runtime;

        public Nvs01KingdomView Present()
        {
            var snapshot = _runtime.Snapshot;
            var catalog = _runtime.Catalog;

            string title;
            string description;
            string speakerName;
            string speakerRole;
            Nvs01RuntimeDiagnostic localizationDiagnostic;
            if (!TryLocalize(catalog.TitleKey, snapshot, out title, out localizationDiagnostic) ||
                !TryLocalize(catalog.DescriptionKey, snapshot, out description, out localizationDiagnostic) ||
                !TryLocalize(catalog.Speaker.NameKey, snapshot, out speakerName, out localizationDiagnostic) ||
                !TryLocalize(catalog.Speaker.RoleKey, snapshot, out speakerRole, out localizationDiagnostic))
            {
                return BuildUnavailable(snapshot, localizationDiagnostic);
            }

            string objectiveText = string.Empty;
            var activeObjective = snapshot.Objectives.FirstOrDefault(
                objective => objective.Status == Nvs01ObjectiveStatus.Active);
            if (activeObjective != null)
            {
                Nvs01Objective objective;
                if (!catalog.TryGetObjective(activeObjective.ObjectiveId, out objective) ||
                    !TryLocalize(objective.TextKey, snapshot, out objectiveText, out localizationDiagnostic))
                {
                    return BuildUnavailable(snapshot, localizationDiagnostic ?? ReferenceMissing(
                        snapshot,
                        "objective",
                        activeObjective.ObjectiveId));
                }
            }

            string dialogueText = string.Empty;
            var choices = new List<Nvs01KingdomChoice>();
            if (snapshot.CurrentDialogueNodeId.Length > 0)
            {
                Nvs01DialogueNode node;
                if (!catalog.TryGetDialogue(snapshot.CurrentDialogueNodeId, out node))
                    return BuildUnavailable(snapshot, ReferenceMissing(snapshot, "dialogue", snapshot.CurrentDialogueNodeId));
                if (!TryLocalize(node.TextKey, snapshot, out dialogueText, out localizationDiagnostic))
                    return BuildUnavailable(snapshot, localizationDiagnostic);

                foreach (var choice in node.Choices)
                {
                    string label;
                    if (!TryLocalize(choice.Key, snapshot, out label, out localizationDiagnostic))
                        return BuildUnavailable(snapshot, localizationDiagnostic);
                    choices.Add(new Nvs01KingdomChoice(choice.Key, label));
                }
            }

            Nvs01KingdomActionKind primaryAction;
            string primaryActionLabel;
            ResolvePrimaryAction(snapshot, catalog, speakerName, out primaryAction, out primaryActionLabel);

            var status = string.Equals(snapshot.StateId, Completed, StringComparison.Ordinal)
                ? Nvs01KingdomViewStatus.Completed
                : _lastDiagnostic == null
                    ? Nvs01KingdomViewStatus.Ready
                    : Nvs01KingdomViewStatus.Attention;
            string playerMessage = _lastDiagnostic == null
                ? string.Empty
                : BuildPlayerMessage(title, _lastDiagnostic);

            return new Nvs01KingdomView(
                status,
                title,
                description,
                snapshot.StateId,
                objectiveText,
                speakerName,
                speakerRole,
                dialogueText,
                choices,
                primaryAction,
                primaryActionLabel,
                !string.Equals(snapshot.StateId, Completed, StringComparison.Ordinal) &&
                snapshot.CurrentEncounter == null,
                snapshot.CurrentEncounter,
                playerMessage,
                _lastDiagnostic?.Code ?? string.Empty);
        }

        public Nvs01KingdomActionResult SelectValerius()
        {
            var snapshot = _runtime.Snapshot;
            Nvs01InteractionKind interaction;
            if (string.Equals(snapshot.StateId, Offered, StringComparison.Ordinal))
            {
                interaction = Nvs01InteractionKind.Offer;
            }
            else if (string.Equals(snapshot.StateId, ReportToValerius, StringComparison.Ordinal))
            {
                interaction = Nvs01InteractionKind.Report;
            }
            else
            {
                return LocalFailure(
                    "TRANSITION-INVALID",
                    "Valerius interaction",
                    Offered + " or " + ReportToValerius,
                    snapshot.StateId);
            }

            Nvs01RealmContext realmContext;
            Nvs01KingdomActionResult realmFailure;
            if (!TryGetRealmContext(out realmContext, out realmFailure)) return realmFailure;

            var disposition = _runtime.SelectValerius(
                Command(catalogActor: _runtime.Catalog.Speaker.Id, contextId: _runtime.Catalog.Placement.ContextId),
                interaction,
                realmContext);
            return Complete(disposition);
        }

        public Nvs01KingdomActionResult SelectChoice(string choiceKey)
        {
            var snapshot = _runtime.Snapshot;
            if (string.IsNullOrWhiteSpace(choiceKey))
                return LocalFailure("EVENT-MISMATCH", "dialogue choice", "catalog choice key", choiceKey ?? string.Empty);
            if (!snapshot.PendingChoice || snapshot.CurrentDialogueNodeId.Length == 0)
                return LocalFailure("TRANSITION-INVALID", "dialogue choice", "pending choice", snapshot.CurrentDialogueNodeId);

            var disposition = _runtime.SelectDialogueChoice(
                Command(PlayerActor, snapshot.CurrentDialogueNodeId),
                choiceKey);
            return Complete(disposition);
        }

        public Nvs01KingdomActionResult InvokePrimaryAction()
        {
            var snapshot = _runtime.Snapshot;
            if (snapshot.CurrentEncounter != null)
            {
                _lastDiagnostic = null;
                return new Nvs01KingdomActionResult(Present(), null, snapshot.CurrentEncounter);
            }

            if (snapshot.PendingSemanticActionId.Length == 0)
                return LocalFailure("TRANSITION-INVALID", "semantic action", "pending action", string.Empty);

            Nvs01RealmContext realmContext;
            Nvs01KingdomActionResult realmFailure;
            if (!TryGetRealmContext(out realmContext, out realmFailure)) return realmFailure;

            Nvs01CapabilitySnapshot capabilities;
            try
            {
                capabilities = _capabilityProvider();
            }
            catch (Exception)
            {
                capabilities = null;
            }

            if (capabilities == null)
            {
                return LocalFailure(
                    "DEPENDENCY-UNAVAILABLE",
                    "arena capabilities",
                    "available capability snapshot",
                    "unavailable");
            }

            var disposition = _runtime.InvokePendingSemanticAction(
                Command(PlayerActor, snapshot.PendingSemanticActionId),
                capabilities,
                realmContext);
            return Complete(disposition);
        }

        public Nvs01KingdomActionResult Abandon()
        {
            var snapshot = _runtime.Snapshot;
            bool encounterActive =
                snapshot.CurrentEncounter != null ||
                snapshot.EncounterStatus == Nvs01EncounterStatus.Requested ||
                snapshot.EncounterStatus == Nvs01EncounterStatus.Active;
            var disposition = _runtime.Abandon(
                Command(PlayerActor, _runtime.Catalog.QuestId),
                encounterActive);
            return Complete(disposition);
        }

        private Nvs01CommandEnvelope Command(string catalogActor, string contextId)
        {
            var snapshot = _runtime.Snapshot;
            return new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                _operationIdFactory(),
                snapshot.QuestId,
                snapshot.StateId,
                snapshot.Revision,
                catalogActor,
                contextId,
                _diagnosticTimestampFactory());
        }

        private Nvs01KingdomActionResult Complete(Nvs01CommandDisposition disposition)
        {
            if (disposition == null)
                return LocalFailure("DEPENDENCY-UNAVAILABLE", "runtime disposition", "typed result", "null");
            _lastDiagnostic = disposition.Diagnostic;
            return new Nvs01KingdomActionResult(
                Present(),
                disposition,
                disposition.EncounterRequest ?? disposition.Snapshot.CurrentEncounter);
        }

        private Nvs01KingdomActionResult LocalFailure(
            string code,
            string message,
            string expected,
            string actual)
        {
            var snapshot = _runtime.Snapshot;
            _lastDiagnostic = new Nvs01RuntimeDiagnostic(
                code,
                message,
                expected,
                actual,
                snapshot.StateId,
                snapshot.PendingSemanticActionId,
                snapshot.CurrentEncounter?.CorrelationId ?? string.Empty);
            return new Nvs01KingdomActionResult(Present(), null, snapshot.CurrentEncounter);
        }

        private bool TryGetRealmContext(
            out Nvs01RealmContext realmContext,
            out Nvs01KingdomActionResult failure)
        {
            try
            {
                realmContext = _realmContextProvider();
            }
            catch (Exception)
            {
                realmContext = null;
            }

            if (realmContext != null)
            {
                failure = null;
                return true;
            }

            failure = LocalFailure(
                "DEPENDENCY-UNAVAILABLE",
                "realm context",
                "committed realm identity",
                "unavailable");
            return false;
        }

        private void ResolvePrimaryAction(
            Nvs01QuestSnapshot snapshot,
            Nvs01Catalog catalog,
            string speakerName,
            out Nvs01KingdomActionKind action,
            out string label)
        {
            if (snapshot.CurrentEncounter != null)
            {
                action = Nvs01KingdomActionKind.ResumeEncounter;
                string resumeAction =
                    string.Equals(
                        snapshot.LastOperation?.EventId,
                        "RETRY_SKY_CASTLE_ARENA",
                        StringComparison.Ordinal)
                        ? "RETRY_SKY_CASTLE_ARENA"
                        : "REQUEST_SKY_CASTLE_ARENA";
                label = ResolveSemanticActionLabel(catalog, resumeAction);
                return;
            }

            if (snapshot.PendingSemanticActionId.Length > 0)
            {
                action = Nvs01KingdomActionKind.InvokeSemanticAction;
                label = ResolveSemanticActionLabel(catalog, snapshot.PendingSemanticActionId);
                return;
            }

            if (snapshot.CurrentDialogueNodeId.Length == 0 &&
                (string.Equals(snapshot.StateId, Offered, StringComparison.Ordinal) ||
                 string.Equals(snapshot.StateId, ReportToValerius, StringComparison.Ordinal)))
            {
                action = Nvs01KingdomActionKind.SelectValerius;
                label = speakerName;
                return;
            }

            action = Nvs01KingdomActionKind.None;
            label = string.Empty;
        }

        private string ResolveSemanticActionLabel(Nvs01Catalog catalog, string actionId)
        {
            foreach (var node in catalog.Dialogue)
            {
                foreach (var choice in node.Choices)
                {
                    bool direct = string.Equals(choice.SemanticAction, actionId, StringComparison.Ordinal);
                    Nvs01DialogueNode target;
                    bool targetsAction =
                        choice.Target != null &&
                        catalog.TryGetDialogue(choice.Target, out target) &&
                        string.Equals(target.SemanticAction, actionId, StringComparison.Ordinal);
                    if (!direct && !targetsAction) continue;

                    string label;
                    return _runtime.TryGetLocalizedText(choice.Key, out label) ? label : string.Empty;
                }
            }

            return string.Empty;
        }

        private bool TryLocalize(
            string key,
            Nvs01QuestSnapshot snapshot,
            out string text,
            out Nvs01RuntimeDiagnostic diagnostic)
        {
            if (_runtime.TryGetLocalizedText(key, out text))
            {
                diagnostic = null;
                return true;
            }

            diagnostic = ReferenceMissing(snapshot, "localization", key);
            return false;
        }

        private static Nvs01RuntimeDiagnostic ReferenceMissing(
            Nvs01QuestSnapshot snapshot,
            string referenceKind,
            string referenceId)
        {
            return new Nvs01RuntimeDiagnostic(
                "REFERENCE-MISSING",
                referenceKind,
                "catalog reference",
                referenceId ?? string.Empty,
                snapshot.StateId,
                snapshot.PendingSemanticActionId,
                snapshot.CurrentEncounter?.CorrelationId ?? string.Empty);
        }

        private Nvs01KingdomView BuildUnavailable(
            Nvs01QuestSnapshot snapshot,
            Nvs01RuntimeDiagnostic diagnostic)
        {
            _lastDiagnostic = diagnostic ?? ReferenceMissing(snapshot, "presentation", string.Empty);
            return new Nvs01KingdomView(
                Nvs01KingdomViewStatus.Unavailable,
                string.Empty,
                string.Empty,
                snapshot.StateId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<Nvs01KingdomChoice>(),
                Nvs01KingdomActionKind.None,
                string.Empty,
                false,
                snapshot.CurrentEncounter,
                "Quest content is currently unavailable. (" + _lastDiagnostic.Code + ")",
                _lastDiagnostic.Code);
        }

        private static string BuildPlayerMessage(string title, Nvs01RuntimeDiagnostic diagnostic)
        {
            string action = diagnostic.Code.EndsWith("DEPENDENCY-UNAVAILABLE", StringComparison.Ordinal)
                ? "is currently unavailable"
                : "could not complete that action";
            return title + " " + action + ". (" + diagnostic.Code + ")";
        }
    }
}
