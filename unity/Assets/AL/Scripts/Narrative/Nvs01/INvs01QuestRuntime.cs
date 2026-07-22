using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AL.Narrative.Nvs01.Contracts;

namespace AL.Narrative.Nvs01
{
    public enum Nvs01InteractionKind
    {
        Offer = 0,
        Report = 1
    }

    public enum Nvs01ObjectiveStatus
    {
        Inactive = 0,
        Active = 1,
        Completed = 2
    }

    public enum Nvs01EncounterStatus
    {
        None = 0,
        Requested = 1,
        Active = 2,
        Resolved = 3
    }

    public enum Nvs01CommandStatus
    {
        Committed = 0,
        Duplicate = 1,
        Rejected = 2,
        DependencyUnavailable = 3,
        CommitFailed = 4
    }

    public enum Nvs01RealmContextStatus
    {
        Unavailable = 0,
        CommittedValid = 1,
        Invalid = 2
    }

    public sealed class Nvs01CommandEnvelope
    {
        public Nvs01CommandEnvelope(
            int contractVersion,
            string operationId,
            string questId,
            string expectedStateId,
            long expectedRevision,
            string actorId,
            string contextId,
            long diagnosticTimestampUnixMs)
        {
            Nvs01ContractGuard.RequireContractVersion(contractVersion, nameof(contractVersion));
            if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            ContractVersion = contractVersion;
            OperationId = Nvs01ContractGuard.RequireGuid(operationId, nameof(operationId));
            QuestId = Nvs01ContractGuard.RequireQuestId(questId, nameof(questId));
            ExpectedStateId = Nvs01ContractGuard.RequireIdentifier(expectedStateId, nameof(expectedStateId));
            ExpectedRevision = expectedRevision;
            ActorId = Nvs01ContractGuard.RequireIdentifier(actorId, nameof(actorId));
            ContextId = Nvs01ContractGuard.RequireIdentifier(contextId, nameof(contextId));
            DiagnosticTimestampUnixMs = diagnosticTimestampUnixMs;
        }

        public int ContractVersion { get; }
        public string OperationId { get; }
        public string QuestId { get; }
        public string ExpectedStateId { get; }
        public long ExpectedRevision { get; }
        public string ActorId { get; }
        public string ContextId { get; }
        public long DiagnosticTimestampUnixMs { get; }
    }

    public sealed class Nvs01RealmContext
    {
        public Nvs01RealmContext(Nvs01RealmContextStatus status, string realmId)
        {
            if (!Enum.IsDefined(typeof(Nvs01RealmContextStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (realmId == null) throw new ArgumentNullException(nameof(realmId));

            Status = status;
            RealmId = status == Nvs01RealmContextStatus.CommittedValid
                ? Nvs01ContractGuard.RequireIdentifier(realmId, nameof(realmId))
                : Nvs01ContractGuard.RequireOptionalIdentifier(realmId, nameof(realmId));
        }

        public Nvs01RealmContextStatus Status { get; }
        public string RealmId { get; }
        public bool IsCommittedValid => Status == Nvs01RealmContextStatus.CommittedValid;

        public static Nvs01RealmContext Unavailable() =>
            new Nvs01RealmContext(Nvs01RealmContextStatus.Unavailable, string.Empty);
    }

    public sealed class Nvs01CapabilitySnapshot
    {
        private readonly IReadOnlyDictionary<string, bool> _availability;

        public Nvs01CapabilitySnapshot(IDictionary<string, bool> availability)
        {
            if (availability == null) throw new ArgumentNullException(nameof(availability));
            if (availability.Count > Nvs01RuntimeContract.MaximumCapabilityCount)
                throw new ArgumentException("Capability count exceeds the NVS-01 runtime bound.", nameof(availability));

            var copy = new Dictionary<string, bool>(availability.Count, StringComparer.Ordinal);
            foreach (var entry in availability)
            {
                copy.Add(Nvs01ContractGuard.RequireIdentifier(entry.Key, nameof(availability)), entry.Value);
            }
            _availability = new ReadOnlyDictionary<string, bool>(copy);
        }

        public IReadOnlyDictionary<string, bool> Availability => _availability;

        public bool IsAvailable(string capabilityId)
        {
            if (string.IsNullOrEmpty(capabilityId)) return false;
            bool available;
            return _availability.TryGetValue(capabilityId, out available) && available;
        }
    }

    public sealed class Nvs01ObjectiveSnapshot
    {
        public Nvs01ObjectiveSnapshot(string objectiveId, Nvs01ObjectiveStatus status)
        {
            if (!Enum.IsDefined(typeof(Nvs01ObjectiveStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            ObjectiveId = Nvs01ContractGuard.RequireIdentifier(objectiveId, nameof(objectiveId));
            Status = status;
        }

        public string ObjectiveId { get; }
        public Nvs01ObjectiveStatus Status { get; }
    }

    public sealed class Nvs01OperationReceipt
    {
        public Nvs01OperationReceipt(
            string operationId,
            string payloadFingerprint,
            Nvs01CommandStatus status,
            long revision,
            string stateId,
            string eventId,
            string correlationId)
        {
            if (!Enum.IsDefined(typeof(Nvs01CommandStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            OperationId = Nvs01ContractGuard.RequireGuid(operationId, nameof(operationId));
            PayloadFingerprint = Nvs01ContractGuard.RequireIdentifier(payloadFingerprint, nameof(payloadFingerprint));
            Status = status;
            Revision = revision;
            StateId = Nvs01ContractGuard.RequireIdentifier(stateId, nameof(stateId));
            EventId = Nvs01ContractGuard.RequireOptionalIdentifier(eventId, nameof(eventId));
            CorrelationId = Nvs01ContractGuard.RequireOptionalGuid(correlationId, nameof(correlationId));
        }

        public string OperationId { get; }
        public string PayloadFingerprint { get; }
        public Nvs01CommandStatus Status { get; }
        public long Revision { get; }
        public string StateId { get; }
        public string EventId { get; }
        public string CorrelationId { get; }
    }

    public sealed class Nvs01QuestSnapshot
    {
        public Nvs01QuestSnapshot(
            string packetVersion,
            string packetSha256,
            string questId,
            long revision,
            string stateId,
            IList<Nvs01ObjectiveSnapshot> objectives,
            string currentDialogueNodeId,
            bool pendingChoice,
            string pendingSemanticActionId,
            string committedRealmId,
            Nvs01EncounterStatus encounterStatus,
            NvsEncounterRequest currentEncounter,
            string lastEncounterCorrelationId,
            NvsEncounterOutcome? lastEncounterOutcome,
            string lastEncounterEventId,
            string lastEncounterSnapshotVersion,
            string lastEncounterSnapshotReference,
            Nvs01OperationReceipt lastOperation,
            IList<string> consequenceIntentIds)
        {
            if (packetVersion == null) throw new ArgumentNullException(nameof(packetVersion));
            if (packetSha256 == null) throw new ArgumentNullException(nameof(packetSha256));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(Nvs01EncounterStatus), encounterStatus))
                throw new ArgumentOutOfRangeException(nameof(encounterStatus));
            if (lastEncounterOutcome.HasValue && !Enum.IsDefined(typeof(NvsEncounterOutcome), lastEncounterOutcome.Value))
                throw new ArgumentOutOfRangeException(nameof(lastEncounterOutcome));
            if (encounterStatus == Nvs01EncounterStatus.Requested || encounterStatus == Nvs01EncounterStatus.Active)
            {
                if (currentEncounter == null)
                    throw new ArgumentException("An active encounter status requires a request.", nameof(currentEncounter));
            }
            else if (currentEncounter != null)
            {
                throw new ArgumentException("A resolved or absent encounter cannot retain a current request.", nameof(currentEncounter));
            }

            PacketVersion = Nvs01ContractGuard.RequireIdentifier(packetVersion, nameof(packetVersion));
            PacketSha256 = Nvs01ContractGuard.RequireIdentifier(packetSha256, nameof(packetSha256));
            QuestId = Nvs01ContractGuard.RequireQuestId(questId, nameof(questId));
            Revision = revision;
            StateId = Nvs01ContractGuard.RequireIdentifier(stateId, nameof(stateId));
            Objectives = FreezeObjectives(objectives);
            CurrentDialogueNodeId = Nvs01ContractGuard.RequireOptionalIdentifier(currentDialogueNodeId, nameof(currentDialogueNodeId));
            PendingChoice = pendingChoice;
            PendingSemanticActionId = Nvs01ContractGuard.RequireOptionalIdentifier(pendingSemanticActionId, nameof(pendingSemanticActionId));
            CommittedRealmId = Nvs01ContractGuard.RequireOptionalIdentifier(committedRealmId, nameof(committedRealmId));
            EncounterStatus = encounterStatus;
            CurrentEncounter = currentEncounter;
            LastEncounterCorrelationId = Nvs01ContractGuard.RequireOptionalGuid(lastEncounterCorrelationId, nameof(lastEncounterCorrelationId));
            LastEncounterOutcome = lastEncounterOutcome;
            LastEncounterEventId = Nvs01ContractGuard.RequireOptionalIdentifier(lastEncounterEventId, nameof(lastEncounterEventId));
            LastEncounterSnapshotVersion = Nvs01ContractGuard.RequireOptionalIdentifier(lastEncounterSnapshotVersion, nameof(lastEncounterSnapshotVersion));
            LastEncounterSnapshotReference = Nvs01ContractGuard.RequireOptionalIdentifier(lastEncounterSnapshotReference, nameof(lastEncounterSnapshotReference));
            LastOperation = lastOperation;
            ConsequenceIntentIds = FreezeIdentifiers(
                consequenceIntentIds,
                Nvs01RuntimeContract.MaximumConsequenceIntentCount,
                nameof(consequenceIntentIds));

            if (PendingChoice && CurrentDialogueNodeId.Length == 0)
                throw new ArgumentException("Pending choice requires a dialogue node.", nameof(pendingChoice));
            if (PendingChoice && PendingSemanticActionId.Length > 0)
                throw new ArgumentException("A pending choice and semantic action are mutually exclusive.", nameof(pendingSemanticActionId));
            // A semantic action normally belongs to the current dialogue node, but a
            // cancelled/unavailable encounter exposes a technical Retry action without
            // substituting the narrative failure conversation.
        }

        public string PacketVersion { get; }
        public string PacketSha256 { get; }
        public string QuestId { get; }
        public long Revision { get; }
        public string StateId { get; }
        public IReadOnlyList<Nvs01ObjectiveSnapshot> Objectives { get; }
        public string CurrentDialogueNodeId { get; }
        public bool PendingChoice { get; }
        public string PendingSemanticActionId { get; }
        public string CommittedRealmId { get; }
        public Nvs01EncounterStatus EncounterStatus { get; }
        public NvsEncounterRequest CurrentEncounter { get; }
        public string LastEncounterCorrelationId { get; }
        public NvsEncounterOutcome? LastEncounterOutcome { get; }
        public string LastEncounterEventId { get; }
        public string LastEncounterSnapshotVersion { get; }
        public string LastEncounterSnapshotReference { get; }
        public Nvs01OperationReceipt LastOperation { get; }
        public IReadOnlyList<string> ConsequenceIntentIds { get; }

        public bool TryGetObjectiveStatus(string objectiveId, out Nvs01ObjectiveStatus status)
        {
            foreach (var objective in Objectives)
            {
                if (!string.Equals(objective.ObjectiveId, objectiveId, StringComparison.Ordinal)) continue;
                status = objective.Status;
                return true;
            }
            status = Nvs01ObjectiveStatus.Inactive;
            return false;
        }

        private static IReadOnlyList<Nvs01ObjectiveSnapshot> FreezeObjectives(IList<Nvs01ObjectiveSnapshot> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Count > Nvs01RuntimeContract.MaximumObjectiveCount)
                throw new ArgumentException("Objective count exceeds the NVS-01 runtime bound.", nameof(source));
            var copy = new Nvs01ObjectiveSnapshot[source.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Count; index++)
            {
                var item = source[index] ?? throw new ArgumentException("Objective cannot be null.", nameof(source));
                if (!ids.Add(item.ObjectiveId))
                    throw new ArgumentException("Objective IDs must be unique.", nameof(source));
                copy[index] = item;
            }
            return Array.AsReadOnly(copy);
        }

        internal static IReadOnlyList<string> FreezeIdentifiers(IList<string> source, int maximum, string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            if (source.Count > maximum) throw new ArgumentException("Collection exceeds the NVS-01 runtime bound.", parameterName);
            var copy = new string[source.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Count; index++)
            {
                var value = Nvs01ContractGuard.RequireIdentifier(source[index], parameterName);
                if (!ids.Add(value)) throw new ArgumentException("Collection identifiers must be unique.", parameterName);
                copy[index] = value;
            }
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class Nvs01RuntimeDiagnostic
    {
        public Nvs01RuntimeDiagnostic(
            string code,
            string message,
            string expected,
            string actual,
            string stateId,
            string eventId,
            string correlationId)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            Code = code.StartsWith(Nvs01CatalogContract.DiagnosticCodePrefix, StringComparison.Ordinal)
                ? code
                : Nvs01CatalogContract.DiagnosticCodePrefix + code;
            Message = message ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            StateId = stateId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            PacketVersion = Nvs01RuntimeContract.PacketVersion;
            PacketSha256 = Nvs01RuntimeContract.PacketSha256;
            QuestId = Nvs01RuntimeContract.QuestId;
        }

        public string Code { get; }
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

    public sealed class Nvs01CommandDisposition
    {
        public Nvs01CommandDisposition(
            Nvs01CommandStatus status,
            Nvs01QuestSnapshot snapshot,
            Nvs01RuntimeDiagnostic diagnostic,
            NvsEncounterRequest encounterRequest,
            IList<string> consequenceIntentIds)
        {
            if (!Enum.IsDefined(typeof(Nvs01CommandStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Diagnostic = diagnostic;
            EncounterRequest = encounterRequest;
            ConsequenceIntentIds = Nvs01QuestSnapshot.FreezeIdentifiers(
                consequenceIntentIds,
                Nvs01RuntimeContract.MaximumConsequenceIntentCount,
                nameof(consequenceIntentIds));
        }

        public Nvs01CommandStatus Status { get; }
        public Nvs01QuestSnapshot Snapshot { get; }
        public Nvs01RuntimeDiagnostic Diagnostic { get; }
        public NvsEncounterRequest EncounterRequest { get; }
        public IReadOnlyList<string> ConsequenceIntentIds { get; }
        public bool IsCommitted => Status == Nvs01CommandStatus.Committed || Status == Nvs01CommandStatus.Duplicate;
    }

    public sealed class Nvs01MutationPlan
    {
        public Nvs01MutationPlan(
            Nvs01QuestSnapshot expected,
            Nvs01QuestSnapshot candidate,
            string triggerEventId,
            IList<string> consequenceIntentIds)
        {
            Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            if (!string.Equals(expected.PacketVersion, candidate.PacketVersion, StringComparison.Ordinal) ||
                !string.Equals(expected.PacketSha256, candidate.PacketSha256, StringComparison.Ordinal) ||
                !string.Equals(expected.QuestId, candidate.QuestId, StringComparison.Ordinal))
                throw new ArgumentException("Mutation candidate identity must match the expected snapshot.", nameof(candidate));
            if (expected.Revision == long.MaxValue || candidate.Revision != expected.Revision + 1)
                throw new ArgumentException("Mutation candidate revision must advance exactly once.", nameof(candidate));
            TriggerEventId = Nvs01ContractGuard.RequireIdentifier(triggerEventId, nameof(triggerEventId));
            ConsequenceIntentIds = Nvs01QuestSnapshot.FreezeIdentifiers(
                consequenceIntentIds,
                Nvs01RuntimeContract.MaximumConsequenceIntentCount,
                nameof(consequenceIntentIds));
        }

        public Nvs01QuestSnapshot Expected { get; }
        public Nvs01QuestSnapshot Candidate { get; }
        public string TriggerEventId { get; }
        public IReadOnlyList<string> ConsequenceIntentIds { get; }
    }

    public interface INvs01MutationCommitter
    {
        bool TryCommit(
            Nvs01MutationPlan plan,
            out Nvs01QuestSnapshot committed,
            out Nvs01RuntimeDiagnostic diagnostic);
    }

    public interface INvs01QuestRuntime
    {
        Nvs01Catalog Catalog { get; }
        Nvs01QuestSnapshot Snapshot { get; }

        Nvs01CommandDisposition SelectValerius(
            Nvs01CommandEnvelope command,
            Nvs01InteractionKind interaction,
            Nvs01RealmContext realmContext);

        Nvs01CommandDisposition SelectDialogueChoice(
            Nvs01CommandEnvelope command,
            string choiceKey);

        Nvs01CommandDisposition InvokePendingSemanticAction(
            Nvs01CommandEnvelope command,
            Nvs01CapabilitySnapshot capabilities,
            Nvs01RealmContext realmContext);

        Nvs01CommandDisposition ApplyEncounterResult(NvsEncounterResult result);

        Nvs01CommandDisposition Abandon(
            Nvs01CommandEnvelope command,
            bool encounterActive);

        bool TryGetActiveEncounter(out NvsEncounterRequest request);
        bool TryGetLocalizedText(string key, out string text);
    }
}
