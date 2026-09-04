using System;
using System.Collections.Generic;
using System.Linq;
using AL.Alliances;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Guilds
{
    public static class GuildRaidMusterSaveCodec
    {
        public const string UnsupportedVersionCode = "AL-RAID-SAVE-VERSION-UNSUPPORTED";
        public const string MalformedCode = "AL-RAID-SAVE-MALFORMED";
        private const int MaximumRows = 4096;

        public static GuildRaidMusterPersistentState Empty()
        {
            return new GuildRaidMusterPersistentState
            {
                Version = GuildRaidMusterPersistentState.CurrentVersion,
                Calls = new List<RaidCallRecord>(),
                Receipts = new List<RaidOperationReceiptRecord>()
            };
        }

        public static GuildRaidMusterPersistentState Write(
            RaidAuthoritySnapshot snapshot,
            long trustedClockUnixSeconds)
        {
            if (snapshot == null)
            {
                GuildRaidMusterPersistentState empty = Empty();
                empty.LastTrustedClockUnixSeconds = trustedClockUnixSeconds;
                return empty;
            }

            GuildCatalogBinding binding = snapshot.CatalogBinding;
            return new GuildRaidMusterPersistentState
            {
                Version = GuildRaidMusterPersistentState.CurrentVersion,
                Revision = snapshot.Revision,
                CatalogId = "al_guild_raid_muster_policy",
                CatalogSchemaVersion = binding == null ? 0 : binding.SchemaVersion,
                ContentVersion = binding == null ? string.Empty : binding.ContentVersion,
                SourceRevision = binding == null ? string.Empty : binding.SourceRevision,
                CatalogHash = binding == null ? string.Empty : binding.CatalogHash,
                LastTrustedClockUnixSeconds = trustedClockUnixSeconds,
                Calls = (snapshot.Calls ?? Array.Empty<RaidCallSnapshot>())
                    .Where(value => value != null)
                    .Select(WriteCall)
                    .ToList(),
                Receipts = (snapshot.Receipts ?? Array.Empty<RaidOperationReceipt>())
                    .Where(value => value != null)
                    .Select(WriteReceipt)
                    .ToList()
            };
        }

        public static RaidAuthoritySnapshot Read(GuildRaidMusterPersistentState state)
        {
            TryRead(state, out RaidAuthoritySnapshot snapshot, out _);
            return snapshot;
        }

        public static bool TryRead(
            GuildRaidMusterPersistentState state,
            out RaidAuthoritySnapshot snapshot,
            out string diagnosticCode)
        {
            if (state == null || state.Version == 0)
            {
                snapshot = EmptySnapshot();
                diagnosticCode = string.Empty;
                return true;
            }

            if (state.Version != GuildRaidMusterPersistentState.CurrentVersion)
            {
                snapshot = new RaidAuthoritySnapshot(
                    GuildAuthorityStatus.Unavailable,
                    0,
                    null,
                    null,
                    null,
                    false);
                diagnosticCode = UnsupportedVersionCode;
                return false;
            }

            if (!IsValid(state))
            {
                snapshot = new RaidAuthoritySnapshot(
                    GuildAuthorityStatus.Unavailable,
                    0,
                    null,
                    null,
                    null,
                    false);
                diagnosticCode = MalformedCode;
                return false;
            }

            var binding = new GuildCatalogBinding(
                state.CatalogSchemaVersion,
                state.ContentVersion,
                state.SourceRevision,
                state.CatalogHash);
            RaidCallSnapshot[] calls = (state.Calls ?? new List<RaidCallRecord>())
                .Where(value => value != null)
                .Select(ReadCall)
                .ToArray();
            RaidOperationReceipt[] receipts = (state.Receipts ?? new List<RaidOperationReceiptRecord>())
                .Where(value => value != null)
                .Select(ReadReceipt)
                .ToArray();
            snapshot = new RaidAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                state.Revision,
                binding,
                calls,
                receipts,
                true);
            diagnosticCode = string.Empty;
            return true;
        }

        private static bool IsValid(GuildRaidMusterPersistentState state)
        {
            if (state.Revision < 0 ||
                state.Calls == null || state.Receipts == null ||
                state.Calls.Count > MaximumRows || state.Receipts.Count > MaximumRows)
            {
                return false;
            }

            if (state.Revision > 0 &&
                (state.CatalogSchemaVersion <= 0 ||
                 string.IsNullOrWhiteSpace(state.ContentVersion) ||
                 string.IsNullOrWhiteSpace(state.SourceRevision) ||
                 state.CatalogHash == null || state.CatalogHash.Length != 64))
            {
                return false;
            }

            var callIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RaidCallRecord call in state.Calls)
            {
                if (call == null || !callIds.Add(call.CallId ?? string.Empty) ||
                    !IsId(call.CallId) || !IsId(call.GuildId) || !IsId(call.ActorAccountId) ||
                    !IsId(call.BossProfileId) || !IsId(call.ClosedInstanceId) ||
                    !IsId(call.ClosedDungeonTopologyId) ||
                    call.WeekId < 0 || call.SeasonEpoch < 0 ||
                    call.WindowStartUnixSeconds < 0 ||
                    call.WindowEndUnixSeconds < call.WindowStartUnixSeconds ||
                    !Enum.IsDefined(typeof(RaidCallState), call.State) ||
                    !Enum.IsDefined(typeof(RaidOutcomeKind), call.Outcome) ||
                    call.Participants == null || call.Participants.Count > MaximumRows ||
                    call.Instance == null || !IsValid(call.Instance, call.ClosedDungeonTopologyId))
                {
                    return false;
                }

                var accountIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (RaidParticipantRecord participant in call.Participants)
                {
                    if (participant == null ||
                        !accountIds.Add(participant.AccountId ?? string.Empty) ||
                        !IsId(participant.AccountId) ||
                        !Enum.IsDefined(typeof(RaidParticipantResponse), participant.Response) ||
                        !Enum.IsDefined(typeof(RaidTransferState), participant.Transfer) ||
                        !IsValid(participant))
                    {
                        return false;
                    }
                }
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RaidOperationReceiptRecord receipt in state.Receipts)
            {
                if (receipt == null ||
                    !operationIds.Add(receipt.OperationId ?? string.Empty) ||
                    !IsId(receipt.OperationId) || !IsId(receipt.CallId) ||
                    !IsId(receipt.GuildId) || !IsId(receipt.ActorAccountId) ||
                    !IsId(receipt.TargetAccountId) || !IsId(receipt.RequestFingerprint) ||
                    !IsId(receipt.PlanHash) || receipt.ResultingRevision < 0 ||
                    !Enum.IsDefined(typeof(RaidOperation), receipt.Operation))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValid(RaidInstanceRecord instance, string topologyId)
        {
            RaidInstanceState state = (RaidInstanceState)instance.State;
            bool requiresEnvelope = state == RaidInstanceState.TeleportingIn ||
                                    state == RaidInstanceState.Active ||
                                    state == RaidInstanceState.ExtractPending ||
                                    state == RaidInstanceState.ExtractingOut ||
                                    state == RaidInstanceState.Extracted;
            return Enum.IsDefined(typeof(RaidInstanceState), instance.State) &&
                   string.Equals(instance.ClosedDungeonTopologyId, topologyId, StringComparison.Ordinal) &&
                   (!requiresEnvelope || IsId(instance.ClosedInstanceEnvelopeId));
        }

        private static bool IsValid(RaidParticipantRecord participant)
        {
            RaidTransferState transfer = (RaidTransferState)participant.Transfer;
            bool requiresEnvelopes = transfer == RaidTransferState.TransferInPending ||
                                     transfer == RaidTransferState.InInstance ||
                                     transfer == RaidTransferState.TransferOutPending ||
                                     transfer == RaidTransferState.Returned ||
                                     transfer == RaidTransferState.Indeterminate;
            return !requiresEnvelopes ||
                   (IsId(participant.ClosedInstanceEnvelopeId) &&
                    IsId(participant.SafeReturnEnvelopeId));
        }

        private static bool IsId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        }

        private static RaidAuthoritySnapshot EmptySnapshot()
        {
            return new RaidAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                0,
                new GuildCatalogBinding(1, string.Empty, string.Empty, string.Empty),
                Array.Empty<RaidCallSnapshot>(),
                Array.Empty<RaidOperationReceipt>(),
                true);
        }

        private static RaidCallRecord WriteCall(RaidCallSnapshot call)
        {
            return new RaidCallRecord
            {
                CallId = call.CallId,
                GuildId = call.GuildId,
                ActorAccountId = call.ActorAccountId,
                State = (int)call.State,
                WeekId = call.WeekId,
                SeasonEpoch = call.SeasonEpoch,
                BossProfileId = call.BossProfileId,
                ClosedInstanceId = call.ClosedInstanceId,
                ClosedDungeonTopologyId = call.ClosedDungeonTopologyId,
                WindowStartUnixSeconds = call.WindowStartUnixSeconds,
                WindowEndUnixSeconds = call.WindowEndUnixSeconds,
                Participants = (call.Participants ?? Array.Empty<RaidParticipantSnapshot>())
                    .Where(value => value != null)
                    .Select(WriteParticipant)
                    .ToList(),
                Instance = WriteInstance(call.Instance),
                Outcome = (int)call.Outcome,
                GrantsReward = call.GrantsReward,
                AppliesLockout = call.AppliesLockout
            };
        }

        private static RaidParticipantRecord WriteParticipant(RaidParticipantSnapshot participant)
        {
            return new RaidParticipantRecord
            {
                AccountId = participant.AccountId,
                Response = (int)participant.Response,
                Transfer = (int)participant.Transfer,
                ClosedInstanceEnvelopeId = participant.ClosedInstanceEnvelopeId,
                SafeReturnEnvelopeId = participant.SafeReturnEnvelopeId,
                GrantsReward = participant.GrantsReward,
                AppliesLockout = participant.AppliesLockout
            };
        }

        private static RaidInstanceRecord WriteInstance(RaidInstanceSnapshot instance)
        {
            return new RaidInstanceRecord
            {
                State = instance == null ? (int)RaidInstanceState.NotLaunched : (int)instance.State,
                ClosedInstanceEnvelopeId = instance == null ? string.Empty : instance.ClosedInstanceEnvelopeId,
                ClosedDungeonTopologyId = instance == null ? string.Empty : instance.ClosedDungeonTopologyId
            };
        }

        private static RaidOperationReceiptRecord WriteReceipt(RaidOperationReceipt receipt)
        {
            return new RaidOperationReceiptRecord
            {
                OperationId = receipt.OperationId,
                Operation = (int)receipt.Operation,
                RequestFingerprint = receipt.RequestFingerprint,
                CallId = receipt.CallId,
                GuildId = receipt.GuildId,
                ActorAccountId = receipt.ActorAccountId,
                TargetAccountId = receipt.TargetAccountId,
                ResultingRevision = receipt.ResultingRevision,
                PlanHash = receipt.PlanHash,
                IsSupported = receipt.IsSupported
            };
        }

        private static RaidCallSnapshot ReadCall(RaidCallRecord call)
        {
            RaidInstanceRecord instance = call.Instance ?? new RaidInstanceRecord();
            return new RaidCallSnapshot(
                call.CallId,
                call.GuildId,
                call.ActorAccountId,
                (RaidCallState)call.State,
                call.WeekId,
                call.SeasonEpoch,
                call.BossProfileId,
                call.ClosedInstanceId,
                call.ClosedDungeonTopologyId,
                call.WindowStartUnixSeconds,
                call.WindowEndUnixSeconds,
                (call.Participants ?? new List<RaidParticipantRecord>())
                    .Where(value => value != null)
                    .Select(ReadParticipant)
                    .ToArray(),
                new RaidInstanceSnapshot(
                    (RaidInstanceState)instance.State,
                    instance.ClosedInstanceEnvelopeId,
                    instance.ClosedDungeonTopologyId),
                (RaidOutcomeKind)call.Outcome,
                call.GrantsReward,
                call.AppliesLockout);
        }

        private static RaidParticipantSnapshot ReadParticipant(RaidParticipantRecord participant)
        {
            return new RaidParticipantSnapshot(
                participant.AccountId,
                (RaidParticipantResponse)participant.Response,
                (RaidTransferState)participant.Transfer,
                participant.ClosedInstanceEnvelopeId,
                participant.SafeReturnEnvelopeId,
                participant.GrantsReward,
                participant.AppliesLockout);
        }

        private static RaidOperationReceipt ReadReceipt(RaidOperationReceiptRecord receipt)
        {
            return new RaidOperationReceipt(
                receipt.OperationId,
                (RaidOperation)receipt.Operation,
                receipt.RequestFingerprint,
                receipt.CallId,
                receipt.GuildId,
                receipt.ActorAccountId,
                receipt.TargetAccountId,
                receipt.ResultingRevision,
                receipt.PlanHash,
                receipt.IsSupported);
        }
    }

    public sealed class GuildRaidMusterRuntime
    {
        public const int ContractVersion = 1;
        public const string TrustedClockSourceId = "trusted_server";
        public const string AuthoritativeCommandSourceId = "authoritative_raid_service";
        public const string ClockMissingCode = "AL-RAID-CLOCK-MISSING";
        public const string ClockUntrustedCode = "AL-RAID-CLOCK-UNTRUSTED";
        public const string ClockMismatchCode = "AL-RAID-CLOCK-MISMATCH";
        public const string ClockRegressionCode = "AL-RAID-CLOCK-REGRESSION";
        public const string CommandEnvelopeInvalidCode = "AL-RAID-COMMAND-ENVELOPE-INVALID";
        public const string CommandSourceUntrustedCode = "AL-RAID-COMMAND-SOURCE-UNTRUSTED";
        public const string PlannerUnavailableCode = "AL-RAID-PLANNER-UNAVAILABLE";
        public const string InstanceLoaderUnavailableCode = "AL-RAID-INSTANCE-LOADER-UNAVAILABLE";
        public const string SaveUnavailableCode = "AL-RAID-SAVE-UNAVAILABLE";
        public const string SaveCommitFailedCode = "AL-RAID-SAVE-COMMIT-FAILED";
        public const string SaveVersionUnsupportedCode = GuildRaidMusterSaveCodec.UnsupportedVersionCode;
        public const string SaveMalformedCode = GuildRaidMusterSaveCodec.MalformedCode;

        private readonly GuildRaidMusterPolicySnapshot policy;
        private readonly GuildRaidMusterPlanner planner;

        public GuildRaidMusterRuntime(GuildRaidMusterPolicySnapshot policy)
        {
            this.policy = policy;
            planner = policy == null ? null : new GuildRaidMusterPlanner(policy);
        }

        public GuildRaidMusterRuntimeResult Apply(
            GuildRaidNetworkCommandEnvelope envelope,
            GuildAuthoritySnapshot membership,
            AllianceAuthoritySnapshot alliance,
            GuildRaidMusterPersistentState persisted)
        {
            return ApplyInternal(envelope, membership, alliance, persisted, null, true);
        }

        private GuildRaidMusterRuntimeResult ApplyInternal(
            GuildRaidNetworkCommandEnvelope envelope,
            GuildAuthoritySnapshot membership,
            AllianceAuthoritySnapshot alliance,
            GuildRaidMusterPersistentState persisted,
            IRaidInstanceEnvelopeLoader loader,
            bool deferTransferLoad)
        {
            GuildRaidMusterPersistentState current = persisted ?? GuildRaidMusterSaveCodec.Empty();
            if (!TryValidateEnvelope(envelope, out long trustedClock, out string envelopeCode))
            {
                GuildPlanningStatus envelopeStatus = envelopeCode == ClockMissingCode ||
                                                     envelopeCode == CommandEnvelopeInvalidCode ||
                                                     envelopeCode == ClockMismatchCode
                    ? GuildPlanningStatus.InvalidRequest
                    : GuildPlanningStatus.Unauthorized;
                return Result(envelopeStatus, current, null, null, envelopeCode, false);
            }

            if (!GuildRaidMusterSaveCodec.TryRead(current, out RaidAuthoritySnapshot raids, out string saveCode))
            {
                return Result(GuildPlanningStatus.Unavailable, current, null, null, saveCode, false);
            }

            if (trustedClock < current.LastTrustedClockUnixSeconds)
            {
                return Result(
                    GuildPlanningStatus.StaleAuthority,
                    current,
                    null,
                    null,
                    ClockRegressionCode,
                    false);
            }

            if (planner == null || policy.Status != GuildCatalogStatus.Ready || !policy.IsComplete)
            {
                return Result(GuildPlanningStatus.Unavailable, current, null, null, PlannerUnavailableCode, false);
            }

            RaidPlanningResult planning = planner.Plan(envelope.Command, raids, membership, alliance);
            if (planning != null &&
                planning.Status == GuildPlanningStatus.AlreadyCommitted &&
                (envelope.Command.Operation == RaidOperation.TransferIn ||
                 envelope.Command.Operation == RaidOperation.TransferOut))
            {
                RaidInstanceCommandEnvelope replayCommand = BuildTransferCommand(envelope.Command, raids);
                if (!deferTransferLoad && loader == null)
                {
                    return Result(
                        GuildPlanningStatus.Unavailable,
                        current,
                        planning,
                        replayCommand,
                        InstanceLoaderUnavailableCode,
                        false);
                }

                if (!deferTransferLoad &&
                    !loader.TryLoad(replayCommand, out string replayLoadCode))
                {
                    return Result(
                        GuildPlanningStatus.Unavailable,
                        current,
                        planning,
                        replayCommand,
                        string.IsNullOrEmpty(replayLoadCode) ? InstanceLoaderUnavailableCode : replayLoadCode,
                        false);
                }

                return Result(
                    GuildPlanningStatus.AlreadyCommitted,
                    current,
                    planning,
                    replayCommand,
                    string.Empty,
                    false);
            }

            if (planning == null || !planning.IsPrepared)
            {
                string diagnostic = planning == null || planning.Diagnostics.Count == 0
                    ? PlannerUnavailableCode
                    : planning.Diagnostics[0].Code;
                return Result(
                    planning == null ? GuildPlanningStatus.Unavailable : planning.Status,
                    current,
                    planning,
                    null,
                    diagnostic,
                    false);
            }

            RaidInstanceCommandEnvelope transferCommand = BuildTransferCommand(
                envelope.Command,
                planning.Plan.CandidateSnapshot);
            if (transferCommand != null)
            {
                if (!deferTransferLoad && loader == null)
                {
                    return Result(
                        GuildPlanningStatus.Unavailable,
                        current,
                        planning,
                        transferCommand,
                        InstanceLoaderUnavailableCode,
                        false);
                }

                if (!deferTransferLoad &&
                    !loader.TryLoad(transferCommand, out string loadCode))
                {
                    return Result(
                        GuildPlanningStatus.Unavailable,
                        current,
                        planning,
                        transferCommand,
                        string.IsNullOrEmpty(loadCode) ? InstanceLoaderUnavailableCode : loadCode,
                        false);
                }
            }

            return Result(
                planning.Status,
                GuildRaidMusterSaveCodec.Write(planning.Plan.CandidateSnapshot, trustedClock),
                planning,
                transferCommand,
                string.Empty,
                true);
        }

        public GuildRaidMusterRuntimeResult ApplyToSave(
            GuildRaidNetworkCommandEnvelope envelope,
            GuildAuthoritySnapshot membership,
            AllianceAuthoritySnapshot alliance,
            SaveGameData save)
        {
            if (save == null)
            {
                return Result(
                    GuildPlanningStatus.Unavailable,
                    GuildRaidMusterSaveCodec.Empty(),
                    null,
                    null,
                    SaveUnavailableCode,
                    false);
            }

            GuildRaidMusterRuntimeResult result = ApplyInternal(
                envelope,
                membership,
                alliance,
                save.GuildRaidMuster,
                null,
                true);
            if (result.Mutated)
            {
                save.GuildRaidMuster = result.Persisted;
            }

            return result;
        }

        public GuildRaidMusterRuntimeResult ApplyToSaveService(
            GuildRaidNetworkCommandEnvelope envelope,
            GuildAuthoritySnapshot membership,
            AllianceAuthoritySnapshot alliance,
            ISaveGameService saveGameService,
            IRaidInstanceEnvelopeLoader loader = null)
        {
            if (saveGameService?.CurrentSave == null)
            {
                return Result(
                    GuildPlanningStatus.Unavailable,
                    GuildRaidMusterSaveCodec.Empty(),
                    null,
                    null,
                    SaveUnavailableCode,
                    false);
            }

            GuildRaidMusterPersistentState previous = saveGameService.CurrentSave.GuildRaidMuster;
            GuildRaidMusterRuntimeResult prepared = ApplyInternal(
                envelope,
                membership,
                alliance,
                previous,
                loader,
                true);
            if (!prepared.Mutated)
            {
                if (prepared.Status == GuildPlanningStatus.AlreadyCommitted &&
                    prepared.TransferCommand != null)
                {
                    if (loader == null)
                    {
                        return Result(
                            GuildPlanningStatus.Unavailable,
                            prepared.Persisted,
                            prepared.Planning,
                            prepared.TransferCommand,
                            InstanceLoaderUnavailableCode,
                            false);
                    }

                    if (loader.TryLoad(prepared.TransferCommand, out string replayLoadCode))
                    {
                        return prepared;
                    }

                    return Result(
                        GuildPlanningStatus.Unavailable,
                        prepared.Persisted,
                        prepared.Planning,
                        prepared.TransferCommand,
                        string.IsNullOrEmpty(replayLoadCode) ? InstanceLoaderUnavailableCode : replayLoadCode,
                        false);
                }

                return prepared;
            }

            if (prepared.TransferCommand != null && loader == null)
            {
                return Result(
                    GuildPlanningStatus.Unavailable,
                    previous ?? GuildRaidMusterSaveCodec.Empty(),
                    prepared.Planning,
                    prepared.TransferCommand,
                    InstanceLoaderUnavailableCode,
                    false);
            }

            saveGameService.CurrentSave.GuildRaidMuster = prepared.Persisted;
            try
            {
                saveGameService.Save();
            }
            catch (Exception)
            {
                saveGameService.CurrentSave.GuildRaidMuster = previous;
                return Result(
                    GuildPlanningStatus.Unavailable,
                    previous ?? GuildRaidMusterSaveCodec.Empty(),
                    prepared.Planning,
                    prepared.TransferCommand,
                    SaveCommitFailedCode,
                    false);
            }

            if (saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
            {
                saveGameService.CurrentSave.GuildRaidMuster = previous;
                return Result(
                    GuildPlanningStatus.Unavailable,
                    previous ?? GuildRaidMusterSaveCodec.Empty(),
                    prepared.Planning,
                    prepared.TransferCommand,
                    SaveCommitFailedCode,
                    false);
            }

            if (prepared.TransferCommand != null &&
                !loader.TryLoad(prepared.TransferCommand, out string loadCode))
            {
                return Result(
                    GuildPlanningStatus.Unavailable,
                    prepared.Persisted,
                    prepared.Planning,
                    prepared.TransferCommand,
                    string.IsNullOrEmpty(loadCode) ? InstanceLoaderUnavailableCode : loadCode,
                    true);
            }

            return prepared;
        }

        public GuildRaidMusterUiPresentation Present(
            GuildRaidMusterPersistentState persisted,
            GuildAuthoritySnapshot membership,
            string actorAccountId,
            string guildId,
            long trustedClockUnixSeconds)
        {
            string saveCode = string.Empty;
            RaidAuthoritySnapshot raids = null;
            if (planner == null || policy.Status != GuildCatalogStatus.Ready || !policy.IsComplete ||
                trustedClockUnixSeconds < 0 ||
                membership == null || membership.Status == GuildAuthorityStatus.Unavailable ||
                !membership.IsComplete || membership.Guilds == null ||
                !GuildRaidMusterSaveCodec.TryRead(persisted, out raids, out saveCode))
            {
                return new GuildRaidMusterUiPresentation(
                    GuildRaidMusterUiStatus.Unavailable,
                    string.Empty,
                    null,
                    trustedClockUnixSeconds,
                    Array.Empty<GuildRaidMusterUiAction>(),
                    string.IsNullOrEmpty(saveCode) ? PlannerUnavailableCode : saveCode);
            }

            GuildSnapshot guild = membership.Guilds.FirstOrDefault(value =>
                value != null &&
                string.Equals(value.GuildId, guildId, StringComparison.Ordinal) &&
                value.Status == GuildStatus.Active);
            GuildMemberSnapshot actor = guild?.Members?.FirstOrDefault(value =>
                value != null &&
                value.State == GuildMembershipState.Active &&
                string.Equals(value.AccountId, actorAccountId, StringComparison.Ordinal));
            if (guild == null || actor == null)
            {
                return new GuildRaidMusterUiPresentation(
                    GuildRaidMusterUiStatus.Unavailable,
                    string.Empty,
                    null,
                    trustedClockUnixSeconds,
                    Array.Empty<GuildRaidMusterUiAction>(),
                    "AL-RAID-UI-MEMBERSHIP-UNAVAILABLE");
            }

            RaidCallSnapshot call = (raids.Calls ?? Array.Empty<RaidCallSnapshot>())
                .Where(value => value != null &&
                                string.Equals(value.GuildId, guildId, StringComparison.Ordinal) &&
                                !IsTerminal(value.State))
                .OrderByDescending(value => PresentationPriority(value, actorAccountId))
                .ThenByDescending(value => value.WindowStartUnixSeconds)
                .FirstOrDefault();
            bool officer = actor.Role == GuildRole.Master || actor.Role == GuildRole.Officer;
            RaidParticipantSnapshot participant = call?.Participants?.FirstOrDefault(value =>
                value != null && string.Equals(value.AccountId, actorAccountId, StringComparison.Ordinal));
            bool accepting = call != null &&
                             call.State == RaidCallState.Accepting &&
                             trustedClockUnixSeconds < call.WindowEndUnixSeconds;
            bool awaitingResponse = accepting &&
                                    participant != null &&
                                    participant.Response == RaidParticipantResponse.NoResponse;
            bool canEnter = call != null &&
                            participant != null &&
                            participant.Response == RaidParticipantResponse.Join &&
                            participant.Transfer == RaidTransferState.NotTransferred &&
                            (call.State == RaidCallState.Ready ||
                             call.State == RaidCallState.Countdown ||
                             call.State == RaidCallState.Active);
            bool canReturn = call != null &&
                             participant != null &&
                             participant.Transfer == RaidTransferState.InInstance;
            string callId = call == null ? string.Empty : call.CallId;
            var actions = new[]
            {
                Action(RaidOperation.AnnounceCall, officer && call == null, callId),
                Action(RaidOperation.Join, awaitingResponse, callId),
                Action(RaidOperation.Decline, awaitingResponse, callId),
                Action(RaidOperation.Launch, officer && accepting, callId),
                Action(RaidOperation.TransferIn, canEnter, callId),
                Action(RaidOperation.TransferOut, canReturn, callId)
            };
            return new GuildRaidMusterUiPresentation(
                call == null ? GuildRaidMusterUiStatus.Awaiting : GuildRaidMusterUiStatus.Authoritative,
                callId,
                call?.State,
                trustedClockUnixSeconds,
                actions,
                string.Empty);
        }

        private static bool TryValidateEnvelope(
            GuildRaidNetworkCommandEnvelope envelope,
            out long trustedClock,
            out string diagnosticCode)
        {
            trustedClock = 0;
            if (envelope == null || envelope.Command == null || envelope.ContractVersion != ContractVersion)
            {
                diagnosticCode = CommandEnvelopeInvalidCode;
                return false;
            }

            if (!string.Equals(envelope.SourceId, AuthoritativeCommandSourceId, StringComparison.Ordinal))
            {
                diagnosticCode = CommandSourceUntrustedCode;
                return false;
            }

            if (envelope.ClockKind == GuildRaidClockKind.Unspecified)
            {
                diagnosticCode = ClockMissingCode;
                return false;
            }

            if (envelope.ClockKind != GuildRaidClockKind.TrustedServer ||
                !string.Equals(envelope.ClockSourceId, TrustedClockSourceId, StringComparison.Ordinal) ||
                envelope.TrustedClockUnixSeconds < 0)
            {
                diagnosticCode = ClockUntrustedCode;
                return false;
            }

            if (envelope.Command.TrustedClockUnixSeconds != envelope.TrustedClockUnixSeconds)
            {
                diagnosticCode = ClockMismatchCode;
                return false;
            }

            trustedClock = envelope.TrustedClockUnixSeconds;
            diagnosticCode = string.Empty;
            return true;
        }

        private static RaidInstanceCommandEnvelope BuildTransferCommand(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot candidate)
        {
            if (request.Operation != RaidOperation.TransferIn && request.Operation != RaidOperation.TransferOut)
            {
                return null;
            }

            RaidCallSnapshot call = candidate.Calls.First(value =>
                string.Equals(value.CallId, request.CallId, StringComparison.Ordinal));
            RaidTransferDirection direction = request.Operation == RaidOperation.TransferIn
                ? RaidTransferDirection.Enter
                : RaidTransferDirection.Return;
            string envelopeId = direction == RaidTransferDirection.Enter
                ? request.ClosedInstanceEnvelopeId
                : request.SafeReturnEnvelopeId;
            return new RaidInstanceCommandEnvelope(
                ContractVersion,
                request.OperationId,
                request.CallId,
                request.GuildId,
                request.TargetAccountId,
                direction,
                envelopeId,
                call.ClosedDungeonTopologyId);
        }

        private static GuildRaidMusterUiAction Action(
            RaidOperation operation,
            bool enabled,
            string callId)
        {
            return new GuildRaidMusterUiAction(
                operation,
                enabled,
                callId,
                enabled ? string.Empty : "planner_revalidation_required");
        }

        private static int PresentationPriority(RaidCallSnapshot call, string actorAccountId)
        {
            RaidParticipantSnapshot participant = call?.Participants?.FirstOrDefault(value =>
                value != null && string.Equals(value.AccountId, actorAccountId, StringComparison.Ordinal));
            if (participant?.Transfer == RaidTransferState.InInstance)
            {
                return 3;
            }

            if (participant != null &&
                participant.Response == RaidParticipantResponse.Join &&
                participant.Transfer == RaidTransferState.NotTransferred &&
                (call.State == RaidCallState.Ready ||
                 call.State == RaidCallState.Countdown ||
                 call.State == RaidCallState.Active))
            {
                return 2;
            }

            return participant?.Response == RaidParticipantResponse.NoResponse &&
                   call.State == RaidCallState.Accepting
                ? 1
                : 0;
        }

        private static bool IsTerminal(RaidCallState state)
        {
            return state == RaidCallState.Completed ||
                   state == RaidCallState.Cancelled ||
                   state == RaidCallState.Failed ||
                   state == RaidCallState.Expired;
        }

        private static GuildRaidMusterRuntimeResult Result(
            GuildPlanningStatus status,
            GuildRaidMusterPersistentState persisted,
            RaidPlanningResult planning,
            RaidInstanceCommandEnvelope transferCommand,
            string diagnosticCode,
            bool mutated)
        {
            return new GuildRaidMusterRuntimeResult(
                status,
                persisted,
                planning,
                transferCommand,
                diagnosticCode,
                mutated);
        }
    }
}
