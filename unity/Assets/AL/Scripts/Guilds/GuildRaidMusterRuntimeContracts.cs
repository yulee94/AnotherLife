using System;
using System.Collections.Generic;

namespace AL.Guilds
{
    public enum GuildRaidClockKind
    {
        Unspecified = 0,
        TrustedServer = 1,
        ClientUntrusted = 2
    }

    public enum RaidTransferDirection
    {
        Enter = 0,
        Return = 1
    }

    public enum GuildRaidMusterUiStatus
    {
        Awaiting = 0,
        Authoritative = 1,
        Unavailable = 2
    }

    public sealed class GuildRaidNetworkCommandEnvelope
    {
        public GuildRaidNetworkCommandEnvelope(
            int contractVersion,
            string sourceId,
            GuildRaidClockKind clockKind,
            string clockSourceId,
            long trustedClockUnixSeconds,
            GuildRaidMusterTransitionRequest command)
        {
            ContractVersion = contractVersion;
            SourceId = sourceId ?? string.Empty;
            ClockKind = clockKind;
            ClockSourceId = clockSourceId ?? string.Empty;
            TrustedClockUnixSeconds = trustedClockUnixSeconds;
            Command = command;
        }

        public int ContractVersion { get; }
        public string SourceId { get; }
        public GuildRaidClockKind ClockKind { get; }
        public string ClockSourceId { get; }
        public long TrustedClockUnixSeconds { get; }
        public GuildRaidMusterTransitionRequest Command { get; }
    }

    public sealed class RaidInstanceCommandEnvelope
    {
        public RaidInstanceCommandEnvelope(
            int contractVersion,
            string commandId,
            string callId,
            string guildId,
            string targetAccountId,
            RaidTransferDirection direction,
            string instanceEnvelopeId,
            string closedDungeonTopologyId)
        {
            ContractVersion = contractVersion;
            CommandId = commandId ?? string.Empty;
            CallId = callId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            TargetAccountId = targetAccountId ?? string.Empty;
            Direction = direction;
            InstanceEnvelopeId = instanceEnvelopeId ?? string.Empty;
            ClosedDungeonTopologyId = closedDungeonTopologyId ?? string.Empty;
        }

        public int ContractVersion { get; }
        public string CommandId { get; }
        public string CallId { get; }
        public string GuildId { get; }
        public string TargetAccountId { get; }
        public RaidTransferDirection Direction { get; }
        public string InstanceEnvelopeId { get; }
        public string ClosedDungeonTopologyId { get; }
    }

    public interface IRaidInstanceEnvelopeLoader
    {
        bool TryLoad(RaidInstanceCommandEnvelope command, out string diagnosticCode);
    }

    public sealed class RaidInstanceLoadDestination
    {
        public RaidInstanceLoadDestination(
            string destinationToken,
            string instanceEnvelopeId,
            string closedDungeonTopologyId,
            RaidTransferDirection direction)
        {
            DestinationToken = destinationToken ?? string.Empty;
            InstanceEnvelopeId = instanceEnvelopeId ?? string.Empty;
            ClosedDungeonTopologyId = closedDungeonTopologyId ?? string.Empty;
            Direction = direction;
        }

        public string DestinationToken { get; }
        public string InstanceEnvelopeId { get; }
        public string ClosedDungeonTopologyId { get; }
        public RaidTransferDirection Direction { get; }
    }

    public interface IRaidInstanceDestinationResolver
    {
        bool TryResolve(
            RaidInstanceCommandEnvelope command,
            out RaidInstanceLoadDestination destination,
            out string diagnosticCode);
    }

    public interface IRaidInstanceLoadBackend
    {
        bool TryLoad(
            string commandId,
            RaidInstanceLoadDestination destination,
            out string diagnosticCode);
    }

    public sealed class RaidInstanceEnvelopeLoader : IRaidInstanceEnvelopeLoader
    {
        public const string InvalidCommandCode = "AL-RAID-INSTANCE-COMMAND-INVALID";
        public const string ResolverUnavailableCode = "AL-RAID-INSTANCE-RESOLVER-UNAVAILABLE";
        public const string DestinationInvalidCode = "AL-RAID-INSTANCE-DESTINATION-INVALID";
        public const string BackendUnavailableCode = "AL-RAID-INSTANCE-BACKEND-UNAVAILABLE";

        private readonly IRaidInstanceDestinationResolver resolver;
        private readonly IRaidInstanceLoadBackend backend;

        public RaidInstanceEnvelopeLoader(
            IRaidInstanceDestinationResolver resolver,
            IRaidInstanceLoadBackend backend)
        {
            this.resolver = resolver;
            this.backend = backend;
        }

        public bool TryLoad(RaidInstanceCommandEnvelope command, out string diagnosticCode)
        {
            diagnosticCode = string.Empty;
            if (command == null ||
                command.ContractVersion != GuildRaidMusterRuntime.ContractVersion ||
                string.IsNullOrEmpty(command.CommandId) ||
                string.IsNullOrEmpty(command.InstanceEnvelopeId) ||
                string.IsNullOrEmpty(command.ClosedDungeonTopologyId))
            {
                diagnosticCode = InvalidCommandCode;
                return false;
            }

            RaidInstanceLoadDestination destination = null;
            if (resolver == null ||
                !resolver.TryResolve(command, out destination, out diagnosticCode))
            {
                diagnosticCode = string.IsNullOrEmpty(diagnosticCode)
                    ? ResolverUnavailableCode
                    : diagnosticCode;
                return false;
            }

            if (destination == null ||
                string.IsNullOrEmpty(destination.DestinationToken) ||
                !string.Equals(destination.InstanceEnvelopeId, command.InstanceEnvelopeId, StringComparison.Ordinal) ||
                !string.Equals(destination.ClosedDungeonTopologyId, command.ClosedDungeonTopologyId, StringComparison.Ordinal) ||
                destination.Direction != command.Direction)
            {
                diagnosticCode = DestinationInvalidCode;
                return false;
            }

            if (backend == null || !backend.TryLoad(command.CommandId, destination, out diagnosticCode))
            {
                diagnosticCode = string.IsNullOrEmpty(diagnosticCode)
                    ? BackendUnavailableCode
                    : diagnosticCode;
                return false;
            }

            diagnosticCode = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class RaidParticipantRecord
    {
        public string AccountId = string.Empty;
        public int Response;
        public int Transfer;
        public string ClosedInstanceEnvelopeId = string.Empty;
        public string SafeReturnEnvelopeId = string.Empty;
        public bool GrantsReward;
        public bool AppliesLockout;
    }

    [Serializable]
    public sealed class RaidInstanceRecord
    {
        public int State;
        public string ClosedInstanceEnvelopeId = string.Empty;
        public string ClosedDungeonTopologyId = string.Empty;
    }

    [Serializable]
    public sealed class RaidCallRecord
    {
        public string CallId = string.Empty;
        public string GuildId = string.Empty;
        public string ActorAccountId = string.Empty;
        public int State;
        public long WeekId;
        public long SeasonEpoch;
        public string BossProfileId = string.Empty;
        public string ClosedInstanceId = string.Empty;
        public string ClosedDungeonTopologyId = string.Empty;
        public long WindowStartUnixSeconds;
        public long WindowEndUnixSeconds;
        public List<RaidParticipantRecord> Participants = new List<RaidParticipantRecord>();
        public RaidInstanceRecord Instance = new RaidInstanceRecord();
        public int Outcome;
        public bool GrantsReward;
        public bool AppliesLockout;
    }

    [Serializable]
    public sealed class RaidOperationReceiptRecord
    {
        public string OperationId = string.Empty;
        public int Operation;
        public string RequestFingerprint = string.Empty;
        public string CallId = string.Empty;
        public string GuildId = string.Empty;
        public string ActorAccountId = string.Empty;
        public string TargetAccountId = string.Empty;
        public long ResultingRevision;
        public string PlanHash = string.Empty;
        public bool IsSupported;
    }

    [Serializable]
    public sealed class GuildRaidMusterPersistentState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public long Revision;
        public string CatalogId = string.Empty;
        public int CatalogSchemaVersion;
        public string ContentVersion = string.Empty;
        public string SourceRevision = string.Empty;
        public string CatalogHash = string.Empty;
        public long LastTrustedClockUnixSeconds;
        public List<RaidCallRecord> Calls = new List<RaidCallRecord>();
        public List<RaidOperationReceiptRecord> Receipts = new List<RaidOperationReceiptRecord>();
    }

    public sealed class GuildRaidMusterUiAction
    {
        public GuildRaidMusterUiAction(RaidOperation operation, bool enabled, string callId, string diagnosticCode)
        {
            Operation = operation;
            Enabled = enabled;
            CallId = callId ?? string.Empty;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public RaidOperation Operation { get; }
        public bool Enabled { get; }
        public string CallId { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class GuildRaidMusterUiPresentation
    {
        public GuildRaidMusterUiPresentation(
            GuildRaidMusterUiStatus status,
            string callId,
            RaidCallState? callState,
            long trustedClockUnixSeconds,
            IReadOnlyList<GuildRaidMusterUiAction> actions,
            string diagnosticCode)
        {
            Status = status;
            CallId = callId ?? string.Empty;
            CallState = callState;
            TrustedClockUnixSeconds = trustedClockUnixSeconds;
            Actions = actions ?? Array.Empty<GuildRaidMusterUiAction>();
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public GuildRaidMusterUiStatus Status { get; }
        public string CallId { get; }
        public RaidCallState? CallState { get; }
        public long TrustedClockUnixSeconds { get; }
        public IReadOnlyList<GuildRaidMusterUiAction> Actions { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class GuildRaidMusterRuntimeResult
    {
        public GuildRaidMusterRuntimeResult(
            GuildPlanningStatus status,
            GuildRaidMusterPersistentState persisted,
            RaidPlanningResult planning,
            RaidInstanceCommandEnvelope transferCommand,
            string diagnosticCode,
            bool mutated)
        {
            Status = status;
            Persisted = persisted ?? GuildRaidMusterSaveCodec.Empty();
            Planning = planning;
            TransferCommand = transferCommand;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Mutated = mutated;
        }

        public GuildPlanningStatus Status { get; }
        public GuildRaidMusterPersistentState Persisted { get; }
        public RaidPlanningResult Planning { get; }
        public RaidInstanceCommandEnvelope TransferCommand { get; }
        public string DiagnosticCode { get; }
        public bool Mutated { get; }
    }
}
