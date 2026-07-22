using System;

namespace AL.Narrative.Nvs01.Contracts
{
    public static class Nvs01RuntimeContract
    {
        public const int ContractVersion = 1;
        public const string QuestId = Nvs01CatalogContract.QuestId;
        public const string PacketVersion = Nvs01CatalogContract.PacketVersion;
        public const string PacketSha256 = Nvs01CatalogContract.CanonicalSha256;
        public const int MaximumIdentifierLength = 256;
        public const int MaximumObjectiveCount = 16;
        public const int MaximumCapabilityCount = 64;
        public const int MaximumConsequenceIntentCount = 16;
    }

    public enum NvsEncounterOutcome
    {
        Success = 0,
        Failure = 1,
        Cancelled = 2,
        Unavailable = 3
    }

    public sealed class NvsEncounterRequest
    {
        public NvsEncounterRequest(
            int contractVersion,
            string requestId,
            string correlationId,
            string questId,
            string stateId,
            string objectiveId,
            string hookId,
            string locationId,
            string realmId,
            string successEventId,
            string failureEventId,
            string cancelledEventId,
            string unavailableEventId,
            string returnScene)
        {
            Nvs01ContractGuard.RequireContractVersion(contractVersion, nameof(contractVersion));
            ContractVersion = contractVersion;
            RequestId = Nvs01ContractGuard.RequireGuid(requestId, nameof(requestId));
            CorrelationId = Nvs01ContractGuard.RequireGuid(correlationId, nameof(correlationId));
            QuestId = Nvs01ContractGuard.RequireQuestId(questId, nameof(questId));
            StateId = Nvs01ContractGuard.RequireIdentifier(stateId, nameof(stateId));
            ObjectiveId = Nvs01ContractGuard.RequireIdentifier(objectiveId, nameof(objectiveId));
            HookId = Nvs01ContractGuard.RequireIdentifier(hookId, nameof(hookId));
            LocationId = Nvs01ContractGuard.RequireIdentifier(locationId, nameof(locationId));
            RealmId = Nvs01ContractGuard.RequireIdentifier(realmId, nameof(realmId));
            SuccessEventId = Nvs01ContractGuard.RequireIdentifier(successEventId, nameof(successEventId));
            FailureEventId = Nvs01ContractGuard.RequireIdentifier(failureEventId, nameof(failureEventId));
            CancelledEventId = Nvs01ContractGuard.RequireIdentifier(cancelledEventId, nameof(cancelledEventId));
            UnavailableEventId = Nvs01ContractGuard.RequireIdentifier(unavailableEventId, nameof(unavailableEventId));
            ReturnScene = Nvs01ContractGuard.RequireIdentifier(returnScene, nameof(returnScene));

            if (string.Equals(RequestId, CorrelationId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Request and correlation IDs must be distinct.", nameof(correlationId));
            }

            RequireDistinctEventIds();
        }

        public int ContractVersion { get; }
        public string RequestId { get; }
        public string CorrelationId { get; }
        public string QuestId { get; }
        public string StateId { get; }
        public string ObjectiveId { get; }
        public string HookId { get; }
        public string LocationId { get; }
        public string RealmId { get; }
        public string SuccessEventId { get; }
        public string FailureEventId { get; }
        public string CancelledEventId { get; }
        public string UnavailableEventId { get; }
        public string ReturnScene { get; }

        public string GetEventId(NvsEncounterOutcome outcome)
        {
            switch (outcome)
            {
                case NvsEncounterOutcome.Success:
                    return SuccessEventId;
                case NvsEncounterOutcome.Failure:
                    return FailureEventId;
                case NvsEncounterOutcome.Cancelled:
                    return CancelledEventId;
                case NvsEncounterOutcome.Unavailable:
                    return UnavailableEventId;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }

        private void RequireDistinctEventIds()
        {
            var eventIds = new[] { SuccessEventId, FailureEventId, CancelledEventId, UnavailableEventId };
            for (var left = 0; left < eventIds.Length; left++)
            {
                for (var right = left + 1; right < eventIds.Length; right++)
                {
                    if (string.Equals(eventIds[left], eventIds[right], StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Encounter outcome event IDs must be distinct.", nameof(SuccessEventId));
                    }
                }
            }
        }
    }

    public sealed class NvsEncounterResult
    {
        public NvsEncounterResult(
            int contractVersion,
            string correlationId,
            string questId,
            string hookId,
            string realmId,
            NvsEncounterOutcome outcome,
            string eventId,
            string snapshotVersion,
            string snapshotReference)
        {
            Nvs01ContractGuard.RequireContractVersion(contractVersion, nameof(contractVersion));
            if (!Enum.IsDefined(typeof(NvsEncounterOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            ContractVersion = contractVersion;
            CorrelationId = Nvs01ContractGuard.RequireGuid(correlationId, nameof(correlationId));
            QuestId = Nvs01ContractGuard.RequireQuestId(questId, nameof(questId));
            HookId = Nvs01ContractGuard.RequireIdentifier(hookId, nameof(hookId));
            RealmId = Nvs01ContractGuard.RequireIdentifier(realmId, nameof(realmId));
            Outcome = outcome;
            EventId = Nvs01ContractGuard.RequireIdentifier(eventId, nameof(eventId));
            SnapshotVersion = Nvs01ContractGuard.RequireOptionalIdentifier(snapshotVersion, nameof(snapshotVersion));
            SnapshotReference = Nvs01ContractGuard.RequireOptionalIdentifier(snapshotReference, nameof(snapshotReference));
        }

        public int ContractVersion { get; }
        public string CorrelationId { get; }
        public string QuestId { get; }
        public string HookId { get; }
        public string RealmId { get; }
        public NvsEncounterOutcome Outcome { get; }
        public string EventId { get; }
        public string SnapshotVersion { get; }
        public string SnapshotReference { get; }
    }

    internal static class Nvs01ContractGuard
    {
        internal static void RequireContractVersion(int value, string parameterName)
        {
            if (value != Nvs01RuntimeContract.ContractVersion)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Only NVS-01 runtime contract version 1 is supported.");
            }
        }

        internal static string RequireQuestId(string value, string parameterName)
        {
            var questId = RequireIdentifier(value, parameterName);
            if (!string.Equals(questId, Nvs01RuntimeContract.QuestId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Quest ID does not match the NVS-01 runtime contract.", parameterName);
            }

            return questId;
        }

        internal static string RequireGuid(string value, string parameterName)
        {
            var identifier = RequireIdentifier(value, parameterName);
            Guid parsed;
            if (!Guid.TryParse(identifier, out parsed) || parsed == Guid.Empty)
            {
                throw new ArgumentException("Value must be a non-empty GUID.", parameterName);
            }

            return parsed.ToString("D");
        }

        internal static string RequireIdentifier(string value, string parameterName)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identifier cannot be empty or whitespace.", parameterName);
            }
            if (value.Length > Nvs01RuntimeContract.MaximumIdentifierLength)
            {
                throw new ArgumentException("Identifier exceeds the NVS-01 contract limit.", parameterName);
            }
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException("Identifier cannot contain leading or trailing whitespace.", parameterName);
            }

            return value;
        }

        internal static string RequireOptionalIdentifier(string value, string parameterName)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            return value.Length == 0 ? string.Empty : RequireIdentifier(value, parameterName);
        }

        internal static string RequireOptionalGuid(string value, string parameterName)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            return value.Length == 0 ? string.Empty : RequireGuid(value, parameterName);
        }
    }
}
