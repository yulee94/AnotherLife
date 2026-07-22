using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Narrative.Nvs01.Contracts
{
    public static class Nvs01CatalogContract
    {
        public const string CatalogId = "al.narrative.nvs01";
        public const int SchemaVersion = 1;
        public const string PacketVersion = "omen1-a1-2026-07-22-v002";
        public const string MilestoneId = "NVS-01";
        public const string QuestId = "OMEN_1";
        public const int CanonicalByteLength = 8317;
        public const int MaximumByteLength = 65536;
        public const string CanonicalSha256 = "b22c166310617657cf9716f988e697d4c4992b4d1877b6fd4d0a3311af9a9a1f";
        public const string StreamingAssetsRelativePath = "AL/Narrative/OMEN_1.catalog.json";
        public const string DiagnosticCodePrefix = "AL-NVS01-";
    }

    public sealed class Nvs01Catalog
    {
        public Nvs01Catalog(
            int schemaVersion,
            string packetVersion,
            string milestoneId,
            string questId,
            string titleKey,
            string descriptionKey,
            Nvs01Approval approval,
            Nvs01Placement placement,
            Nvs01Speaker speaker,
            IList<Nvs01State> states,
            IList<Nvs01Objective> objectives,
            IList<Nvs01DialogueNode> dialogue,
            IList<Nvs01Transition> transitions,
            IList<Nvs01ExternalCapability> externalCapabilities,
            IList<Nvs01Consequence> consequences,
            Nvs01Abandonment abandonment,
            IDictionary<string, string> localization)
        {
            SchemaVersion = schemaVersion;
            PacketVersion = packetVersion;
            MilestoneId = milestoneId;
            QuestId = questId;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            Approval = approval;
            Placement = placement;
            Speaker = speaker;
            States = Freeze(states);
            Objectives = Freeze(objectives);
            Dialogue = Freeze(dialogue);
            Transitions = Freeze(transitions);
            ExternalCapabilities = Freeze(externalCapabilities);
            Consequences = Freeze(consequences);
            Abandonment = abandonment ?? throw new ArgumentNullException(nameof(abandonment));
            if (localization == null) throw new ArgumentNullException(nameof(localization));
            Localization = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(localization, StringComparer.Ordinal));
            StatesById = Index(States, state => state.Id, nameof(states));
            ObjectivesById = Index(Objectives, objective => objective.Id, nameof(objectives));
            DialogueById = Index(Dialogue, node => node.Id, nameof(dialogue));
            ExternalCapabilitiesById = Index(ExternalCapabilities, capability => capability.Id, nameof(externalCapabilities));
            ConsequencesById = Index(Consequences, consequence => consequence.Id, nameof(consequences));
            TransitionsByKey = IndexTransitions(Transitions);
        }

        public int SchemaVersion { get; }
        public string PacketVersion { get; }
        public string MilestoneId { get; }
        public string QuestId { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public Nvs01Approval Approval { get; }
        public Nvs01Placement Placement { get; }
        public Nvs01Speaker Speaker { get; }
        public IReadOnlyList<Nvs01State> States { get; }
        public IReadOnlyList<Nvs01Objective> Objectives { get; }
        public IReadOnlyList<Nvs01DialogueNode> Dialogue { get; }
        public IReadOnlyList<Nvs01Transition> Transitions { get; }
        public IReadOnlyList<Nvs01ExternalCapability> ExternalCapabilities { get; }
        public IReadOnlyList<Nvs01Consequence> Consequences { get; }
        public Nvs01Abandonment Abandonment { get; }
        public IReadOnlyDictionary<string, string> Localization { get; }
        public IReadOnlyDictionary<string, Nvs01State> StatesById { get; }
        public IReadOnlyDictionary<string, Nvs01Objective> ObjectivesById { get; }
        public IReadOnlyDictionary<string, Nvs01DialogueNode> DialogueById { get; }
        public IReadOnlyDictionary<string, Nvs01ExternalCapability> ExternalCapabilitiesById { get; }
        public IReadOnlyDictionary<string, Nvs01Consequence> ConsequencesById { get; }
        public IReadOnlyDictionary<Nvs01TransitionKey, Nvs01Transition> TransitionsByKey { get; }

        public bool TryGetState(string id, out Nvs01State value) => StatesById.TryGetValue(id, out value);
        public bool TryGetObjective(string id, out Nvs01Objective value) => ObjectivesById.TryGetValue(id, out value);
        public bool TryGetDialogue(string id, out Nvs01DialogueNode value) => DialogueById.TryGetValue(id, out value);
        public bool TryGetExternalCapability(string id, out Nvs01ExternalCapability value) => ExternalCapabilitiesById.TryGetValue(id, out value);
        public bool TryGetConsequence(string id, out Nvs01Consequence value) => ConsequencesById.TryGetValue(id, out value);
        public bool TryGetTransition(string from, string eventId, out Nvs01Transition value) =>
            TransitionsByKey.TryGetValue(new Nvs01TransitionKey(from, eventId), out value);
        public bool TryGetLocalization(string key, out string value) => Localization.TryGetValue(key, out value);

        private static IReadOnlyList<T> Freeze<T>(IList<T> source) where T : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new T[source.Count];
            source.CopyTo(copy, 0);
            for (var index = 0; index < copy.Length; index++)
            {
                if (copy[index] == null) throw new ArgumentException("Collection contains a null record.", nameof(source));
            }
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyDictionary<string, T> Index<T>(IReadOnlyList<T> source, Func<T, string> keySelector, string parameterName) where T : class
        {
            var result = new Dictionary<string, T>(source.Count, StringComparer.Ordinal);
            foreach (var item in source)
            {
                var key = keySelector(item);
                if (key == null) throw new ArgumentException("Record ID cannot be null.", parameterName);
                if (result.ContainsKey(key)) throw new ArgumentException("Duplicate record ID: " + key, parameterName);
                result.Add(key, item);
            }

            return new ReadOnlyDictionary<string, T>(result);
        }

        private static IReadOnlyDictionary<Nvs01TransitionKey, Nvs01Transition> IndexTransitions(IReadOnlyList<Nvs01Transition> source)
        {
            var result = new Dictionary<Nvs01TransitionKey, Nvs01Transition>(source.Count);
            foreach (var transition in source)
            {
                var key = new Nvs01TransitionKey(transition.From, transition.EventId);
                if (result.ContainsKey(key))
                {
                    throw new ArgumentException("Duplicate transition: " + transition.From + "/" + transition.EventId, nameof(source));
                }
                result.Add(key, transition);
            }

            return new ReadOnlyDictionary<Nvs01TransitionKey, Nvs01Transition>(result);
        }
    }

    public sealed class Nvs01Approval
    {
        public Nvs01Approval(int issue, long commentId, IList<string> decisions)
        {
            Issue = issue;
            CommentId = commentId;
            if (decisions == null) throw new ArgumentNullException(nameof(decisions));
            var copy = new string[decisions.Count];
            decisions.CopyTo(copy, 0);
            Decisions = Array.AsReadOnly(copy);
        }

        public int Issue { get; }
        public long CommentId { get; }
        public IReadOnlyList<string> Decisions { get; }
    }

    public sealed class Nvs01Placement
    {
        public Nvs01Placement(
            string contextId,
            IList<string> eligibleRealmIds,
            string prerequisite,
            string offerAction,
            bool autoAccept,
            string completionUnlockId,
            string completionDestination)
        {
            ContextId = contextId;
            if (eligibleRealmIds == null) throw new ArgumentNullException(nameof(eligibleRealmIds));
            var realms = new string[eligibleRealmIds.Count];
            eligibleRealmIds.CopyTo(realms, 0);
            EligibleRealmIds = Array.AsReadOnly(realms);
            Prerequisite = prerequisite;
            OfferAction = offerAction;
            AutoAccept = autoAccept;
            CompletionUnlockId = completionUnlockId;
            CompletionDestination = completionDestination;
        }

        public string ContextId { get; }
        public IReadOnlyList<string> EligibleRealmIds { get; }
        public string Prerequisite { get; }
        public string OfferAction { get; }
        public bool AutoAccept { get; }
        public string CompletionUnlockId { get; }
        public string CompletionDestination { get; }
    }

    public sealed class Nvs01Speaker
    {
        public Nvs01Speaker(string id, string nameKey, string roleKey)
        {
            Id = id;
            NameKey = nameKey;
            RoleKey = roleKey;
        }

        public string Id { get; }
        public string NameKey { get; }
        public string RoleKey { get; }
    }

    public sealed class Nvs01State
    {
        public Nvs01State(string id, string resume, bool terminal, bool transient)
        {
            Id = id;
            Resume = resume;
            Terminal = terminal;
            Transient = transient;
        }

        public string Id { get; }
        public string Resume { get; }
        public bool Terminal { get; }
        public bool Transient { get; }
    }

    public sealed class Nvs01Objective
    {
        public Nvs01Objective(string id, string textKey, string activatesIn, string completesOn)
        {
            Id = id;
            TextKey = textKey;
            ActivatesIn = activatesIn;
            CompletesOn = completesOn;
        }

        public string Id { get; }
        public string TextKey { get; }
        public string ActivatesIn { get; }
        public string CompletesOn { get; }
    }

    public sealed class Nvs01DialogueNode
    {
        public Nvs01DialogueNode(
            string id,
            string speakerId,
            string textKey,
            string semanticAction,
            IList<Nvs01DialogueChoice> choices)
        {
            Id = id;
            SpeakerId = speakerId;
            TextKey = textKey;
            SemanticAction = semanticAction;
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            var copy = new Nvs01DialogueChoice[choices.Count];
            choices.CopyTo(copy, 0);
            Choices = Array.AsReadOnly(copy);
        }

        public string Id { get; }
        public string SpeakerId { get; }
        public string TextKey { get; }
        public string SemanticAction { get; }
        public IReadOnlyList<Nvs01DialogueChoice> Choices { get; }
    }

    public sealed class Nvs01DialogueChoice
    {
        public Nvs01DialogueChoice(string key, string target, string semanticAction)
        {
            Key = key;
            Target = target;
            SemanticAction = semanticAction;
        }

        public string Key { get; }
        public string Target { get; }
        public string SemanticAction { get; }
    }

    public sealed class Nvs01Transition
    {
        public Nvs01Transition(string from, string eventId, string to, string objective, string dialogue)
        {
            From = from;
            EventId = eventId;
            To = to;
            Objective = objective;
            Dialogue = dialogue;
        }

        public string From { get; }
        public string EventId { get; }
        public string To { get; }
        public string Objective { get; }
        public string Dialogue { get; }
    }

    public readonly struct Nvs01TransitionKey : IEquatable<Nvs01TransitionKey>
    {
        public Nvs01TransitionKey(string from, string eventId)
        {
            From = from;
            EventId = eventId;
        }

        public string From { get; }
        public string EventId { get; }

        public bool Equals(Nvs01TransitionKey other) =>
            string.Equals(From, other.From, StringComparison.Ordinal) &&
            string.Equals(EventId, other.EventId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is Nvs01TransitionKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((From != null ? StringComparer.Ordinal.GetHashCode(From) : 0) * 397) ^
                       (EventId != null ? StringComparer.Ordinal.GetHashCode(EventId) : 0);
            }
        }
    }

    public sealed class Nvs01ExternalCapability
    {
        public Nvs01ExternalCapability(string id, string status)
        {
            Id = id;
            Status = status;
        }

        public string Id { get; }
        public string Status { get; }
    }

    public sealed class Nvs01Consequence
    {
        public Nvs01Consequence(
            string id,
            string target,
            string trigger,
            string repeatability,
            bool? retained,
            long? amount)
        {
            Id = id;
            Target = target;
            Trigger = trigger;
            Repeatability = repeatability;
            Retained = retained;
            Amount = amount;
        }

        public string Id { get; }
        public string Target { get; }
        public string Trigger { get; }
        public string Repeatability { get; }
        public bool? Retained { get; }
        public long? Amount { get; }
    }

    public sealed class Nvs01Abandonment
    {
        public Nvs01Abandonment(
            bool allowedOutsideActiveEncounter,
            string resultState,
            bool clearsActiveProgress,
            bool clearsUnearnedConsequences,
            bool retainsEarnedConsequences)
        {
            AllowedOutsideActiveEncounter = allowedOutsideActiveEncounter;
            ResultState = resultState;
            ClearsActiveProgress = clearsActiveProgress;
            ClearsUnearnedConsequences = clearsUnearnedConsequences;
            RetainsEarnedConsequences = retainsEarnedConsequences;
        }

        public bool AllowedOutsideActiveEncounter { get; }
        public string ResultState { get; }
        public bool ClearsActiveProgress { get; }
        public bool ClearsUnearnedConsequences { get; }
        public bool RetainsEarnedConsequences { get; }
    }

    public enum Nvs01CatalogValidationStatus
    {
        Accepted = 0,
        Rejected = 1
    }

    public enum Nvs01CatalogLoadStatus
    {
        Succeeded = 0,
        NotFound = 1,
        TransportFailed = 2,
        Rejected = 3
    }

    public sealed class Nvs01CatalogDiagnostic
    {
        public Nvs01CatalogDiagnostic(
            string code,
            string path,
            string message,
            string expected,
            string actual)
            : this(
                code,
                path,
                message,
                expected,
                actual,
                Nvs01CatalogContract.PacketVersion,
                Nvs01CatalogContract.CanonicalSha256,
                Nvs01CatalogContract.QuestId,
                string.Empty,
                string.Empty,
                string.Empty)
        {
        }

        public Nvs01CatalogDiagnostic(
            string code,
            string path,
            string message,
            string expected,
            string actual,
            string packetVersion,
            string packetSha256,
            string questId,
            string stateId,
            string eventId,
            string correlationId)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            Code = code.StartsWith(Nvs01CatalogContract.DiagnosticCodePrefix, StringComparison.Ordinal)
                ? code
                : Nvs01CatalogContract.DiagnosticCodePrefix + code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            PacketVersion = packetVersion ?? string.Empty;
            PacketSha256 = packetSha256 ?? string.Empty;
            QuestId = questId ?? string.Empty;
            StateId = stateId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string PacketVersion { get; }
        public string PacketSha256 { get; }
        public string QuestId { get; }
        public string StateId { get; }
        public string EventId { get; }
        public string CorrelationId { get; }
    }

    public sealed class Nvs01VerifiedCatalog
    {
        internal Nvs01VerifiedCatalog(Nvs01Catalog catalog, int canonicalByteLength, string canonicalSha256)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            CatalogId = Nvs01CatalogContract.CatalogId;
            CanonicalByteLength = canonicalByteLength;
            CanonicalSha256 = canonicalSha256 ?? throw new ArgumentNullException(nameof(canonicalSha256));
        }

        public Nvs01Catalog Catalog { get; }
        public string CatalogId { get; }
        public int CanonicalByteLength { get; }
        public string CanonicalSha256 { get; }
    }

    public sealed class Nvs01CatalogValidationResult
    {
        internal Nvs01CatalogValidationResult(
            Nvs01CatalogValidationStatus status,
            Nvs01VerifiedCatalog verifiedCatalog,
            IList<Nvs01CatalogDiagnostic> diagnostics)
        {
            Status = status;
            VerifiedCatalog = verifiedCatalog;
            Diagnostics = FreezeDiagnostics(diagnostics);
        }

        public Nvs01CatalogValidationStatus Status { get; }
        public Nvs01VerifiedCatalog VerifiedCatalog { get; }
        public IReadOnlyList<Nvs01CatalogDiagnostic> Diagnostics { get; }
        public bool IsAccepted => Status == Nvs01CatalogValidationStatus.Accepted && VerifiedCatalog != null;

        private static IReadOnlyList<Nvs01CatalogDiagnostic> FreezeDiagnostics(IList<Nvs01CatalogDiagnostic> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new Nvs01CatalogDiagnostic[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class Nvs01CatalogLoadResult
    {
        internal Nvs01CatalogLoadResult(
            Nvs01CatalogLoadStatus status,
            Nvs01VerifiedCatalog verifiedCatalog,
            IList<Nvs01CatalogDiagnostic> diagnostics)
        {
            Status = status;
            VerifiedCatalog = verifiedCatalog;
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            var copy = new Nvs01CatalogDiagnostic[diagnostics.Count];
            diagnostics.CopyTo(copy, 0);
            Diagnostics = Array.AsReadOnly(copy);
        }

        public Nvs01CatalogLoadStatus Status { get; }
        public Nvs01VerifiedCatalog VerifiedCatalog { get; }
        public IReadOnlyList<Nvs01CatalogDiagnostic> Diagnostics { get; }
        public bool IsSuccess => Status == Nvs01CatalogLoadStatus.Succeeded && VerifiedCatalog != null;
    }
}
