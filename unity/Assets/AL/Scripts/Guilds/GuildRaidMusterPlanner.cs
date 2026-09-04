using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Alliances;

namespace AL.Guilds
{
    public sealed class GuildRaidMusterPlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const int MaximumReceipts = 4096;
        private const string ClosedInstancePrefix = "closed_raid_";

        private readonly GuildRaidMusterPolicySnapshot policy;

        public GuildRaidMusterPlanner(GuildRaidMusterPolicySnapshot policy)
        {
            this.policy = policy;
        }

        public string ResolveBossProfileId(long seasonEpoch, long weekId)
        {
            RaidBossSlotDefinition slot = ResolveSlot(seasonEpoch, weekId);
            return slot == null ? string.Empty : slot.BossProfileId;
        }

        public RaidPlanningResult Plan(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildAuthoritySnapshot membership,
            AllianceAuthoritySnapshot alliance)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    GuildPlanningStatus.InvalidRequest,
                    "AL-RAID-REQUEST-INVALID",
                    request?.OperationId,
                    "Raid identity, fields, or revisions are invalid.");
            }

            RaidPlanningResult policyGate = ValidatePolicy();
            if (policyGate != null)
            {
                return policyGate;
            }

            if (!BindingEquals(request.ExpectedCatalogBinding, policy.Binding))
            {
                return Reject(
                    GuildPlanningStatus.StaleCatalog,
                    "AL-RAID-CATALOG-STALE",
                    request.OperationId,
                    "The request is not fenced to the accepted raid muster catalog.");
            }

            RaidPlanningResult membershipGate = ValidateMembership(membership, request);
            if (membershipGate != null)
            {
                return membershipGate;
            }

            RaidPlanningResult raidGate = ValidateRaids(raids);
            if (raidGate != null)
            {
                return raidGate;
            }

            RaidPlanningResult allianceGate = ValidateAlliance(alliance);
            if (allianceGate != null)
            {
                return allianceGate;
            }

            string requestFingerprint = RequestFingerprint(request);
            RaidPlanningResult replay = ClassifyReplay(request, requestFingerprint, raids.Receipts);
            if (replay != null)
            {
                return replay;
            }

            if (raids.Revision != request.ExpectedRaidRevision)
            {
                return Reject(
                    GuildPlanningStatus.StaleAuthority,
                    "AL-RAID-REVISION-STALE",
                    request.OperationId,
                    "The request is not fenced to the current raid authority revision.");
            }

            GuildSnapshot guild = membership.Guilds.Single(
                value => string.Equals(value.GuildId, request.GuildId, StringComparison.Ordinal));
            switch (request.Operation)
            {
                case RaidOperation.AnnounceCall:
                    return PlanAnnounce(request, raids, guild, requestFingerprint);
                case RaidOperation.Join:
                case RaidOperation.Decline:
                    return PlanResponse(request, raids, guild, requestFingerprint);
                case RaidOperation.Launch:
                    return PlanLaunch(request, raids, guild, requestFingerprint);
                case RaidOperation.TransferIn:
                    return PlanTransferIn(request, raids, guild, requestFingerprint);
                case RaidOperation.TransferOut:
                    return PlanTransferOut(request, raids, guild, requestFingerprint);
                case RaidOperation.Reconcile:
                    return PlanReconcile(request, raids, guild, requestFingerprint);
                case RaidOperation.Cancel:
                    return PlanCancel(request, raids, guild, requestFingerprint);
                case RaidOperation.Expire:
                    return PlanExpire(request, raids, requestFingerprint);
                default:
                    return Reject(
                        GuildPlanningStatus.InvalidRequest,
                        "AL-RAID-OPERATION-UNKNOWN",
                        request.OperationId,
                        "The raid operation is not supported.");
            }
        }

        private RaidPlanningResult PlanAnnounce(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            GuildMemberSnapshot actor = FindActiveMember(guild, request.ActorAccountId);
            if (actor == null)
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-RAID-ACTOR-NOT-MEMBER",
                    request.ActorAccountId,
                    "The actor is not an active member of the Guild.");
            }

            if (!CanOpenRaidCalls(actor.Role))
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-RAID-CALLER-UNAUTHORIZED",
                    request.ActorAccountId,
                    "Only a Guild Master or Officer may open a raid call.");
            }

            if (IsReservedPublicDungeonId(request.ClosedInstanceId) ||
                !request.ClosedInstanceId.StartsWith(ClosedInstancePrefix, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-RAID-PUBLIC-DUNGEON-ALIAS",
                    request.ClosedInstanceId,
                    "Closed-instance IDs cannot alias public dungeon authority.");
            }

            string expectedBoss = ResolveBossProfileId(request.SeasonEpoch, request.WeekId);
            if (!string.Equals(request.BossProfileId, expectedBoss, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-BOSS-ROTATION",
                    request.BossProfileId,
                    "The boss profile is not the deterministic weekly rotation slot.");
            }

            int activeSameWeek = raids.Calls.Count(call =>
                string.Equals(call.GuildId, request.GuildId, StringComparison.Ordinal) &&
                call.WeekId == request.WeekId &&
                !IsTerminalCall(call.State));
            if (activeSameWeek >= policy.CallsPerGuildPerWeek)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-WEEKLY-CAP",
                    request.GuildId,
                    "A Guild may open only one raid call per week.");
            }

            RaidParticipantSnapshot[] participants = guild.Members
                .Where(member => member.State == GuildMembershipState.Active)
                .OrderBy(member => member.AccountId, StringComparer.Ordinal)
                .Select(member => new RaidParticipantSnapshot(
                    member.AccountId,
                    RaidParticipantResponse.NoResponse,
                    RaidTransferState.NotTransferred,
                    string.Empty,
                    string.Empty,
                    false,
                    false))
                .ToArray();

            var call = new RaidCallSnapshot(
                request.CallId,
                request.GuildId,
                request.ActorAccountId,
                RaidCallState.Accepting,
                request.WeekId,
                request.SeasonEpoch,
                request.BossProfileId,
                request.ClosedInstanceId,
                policy.ClosedDungeonTopologyId,
                request.TrustedClockUnixSeconds,
                request.TrustedClockUnixSeconds + (policy.CallWindowMinutes * 60L),
                participants,
                new RaidInstanceSnapshot(RaidInstanceState.NotLaunched, string.Empty, policy.ClosedDungeonTopologyId),
                RaidOutcomeKind.None,
                false,
                false);

            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, call));
        }

        private RaidPlanningResult PlanResponse(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            RaidPlanningResult callGate = RequireCall(raids, request, out RaidCallSnapshot call);
            if (callGate != null)
            {
                return callGate;
            }

            if (FindActiveMember(guild, request.ActorAccountId) == null)
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-RAID-ACTOR-NOT-MEMBER",
                    request.ActorAccountId,
                    "The actor is not an active member of the Guild.");
            }

            if (call.State != RaidCallState.Accepting ||
                request.TrustedClockUnixSeconds >= call.WindowEndUnixSeconds)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-WINDOW-CLOSED",
                    request.CallId,
                    "Join and Decline are only accepted inside the 30-minute window.");
            }

            RaidParticipantSnapshot participant = FindParticipant(call, request.ActorAccountId);
            if (participant == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-RAID-PARTICIPANT-MISSING",
                    request.ActorAccountId,
                    "The actor is not on the raid call.");
            }

            if (participant.Response != RaidParticipantResponse.NoResponse)
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-RAID-RESPONSE-TERMINAL",
                    request.ActorAccountId,
                    "Join and Decline are terminal-XOR for a participant.");
            }

            RaidParticipantResponse next = request.Operation == RaidOperation.Join
                ? RaidParticipantResponse.Join
                : RaidParticipantResponse.Decline;
            RaidCallSnapshot updated = WithParticipants(
                call,
                ReplaceParticipant(
                    call.Participants,
                    new RaidParticipantSnapshot(
                        participant.AccountId,
                        next,
                        participant.Transfer,
                        participant.ClosedInstanceEnvelopeId,
                        participant.SafeReturnEnvelopeId,
                        false,
                        false)));
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult PlanLaunch(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            RaidPlanningResult callGate = RequireCall(raids, request, out RaidCallSnapshot call);
            if (callGate != null)
            {
                return callGate;
            }

            GuildMemberSnapshot actor = FindActiveMember(guild, request.ActorAccountId);
            if (actor == null || !CanOpenRaidCalls(actor.Role))
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-RAID-LAUNCH-UNAUTHORIZED",
                    request.ActorAccountId,
                    "Launch requires a current Guild Master or Officer.");
            }

            if (call.State != RaidCallState.Accepting)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-LAUNCH-STATE",
                    request.CallId,
                    "Launch is only valid from ACCEPTING.");
            }

            if (request.WeekId != call.WeekId || request.SeasonEpoch != call.SeasonEpoch)
            {
                return Reject(
                    GuildPlanningStatus.StaleAuthority,
                    "AL-RAID-WEEK-STALE",
                    request.CallId,
                    "Launch week or season epoch does not match the call.");
            }

            string expectedBoss = ResolveBossProfileId(call.SeasonEpoch, call.WeekId);
            if (!string.Equals(request.BossProfileId, expectedBoss, StringComparison.Ordinal) ||
                !string.Equals(call.BossProfileId, expectedBoss, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-BOSS-STALE",
                    request.BossProfileId,
                    "Launch boss profile is not the immutable weekly rotation.");
            }

            int joins = call.Participants.Count(value => value.Response == RaidParticipantResponse.Join);
            if (joins < policy.MinJoinCount)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-MIN-JOIN",
                    request.CallId,
                    "Launch requires the catalog minimum explicit Join count.");
            }

            RaidCallSnapshot updated = WithState(
                call,
                RaidCallState.Countdown,
                call.Instance,
                call.Outcome,
                false,
                false);
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult PlanTransferIn(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            if (!string.IsNullOrEmpty(request.SceneName))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-RAID-SCENE-TELEPORT",
                    request.SceneName,
                    "Closed-instance transfer cannot use scene-name teleport.");
            }

            RaidPlanningResult callGate = RequireCall(raids, request, out RaidCallSnapshot call);
            if (callGate != null)
            {
                return callGate;
            }

            if (FindActiveMember(guild, request.TargetAccountId) == null)
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-RAID-TARGET-NOT-MEMBER",
                    request.TargetAccountId,
                    "Transfer-in is denied before summon for non-members.");
            }

            if (call.State != RaidCallState.Ready &&
                call.State != RaidCallState.Countdown &&
                call.State != RaidCallState.Active)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-TRANSFER-STATE",
                    request.CallId,
                    "Transfer-in requires READY, COUNTDOWN, or ACTIVE.");
            }

            RaidParticipantSnapshot participant = FindParticipant(call, request.TargetAccountId);
            if (participant == null || participant.Response != RaidParticipantResponse.Join)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-CONSENT-REQUIRED",
                    request.TargetAccountId,
                    "Closed-instance summon-in requires explicit Join and cannot be inferred.");
            }

            if (!request.EligibilityPassed ||
                !request.ZoneAllowed ||
                !request.GenerationContinuous ||
                !request.LiveLocationValid)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-SAFETY-PRECHECK",
                    request.TargetAccountId,
                    "Transfer-in requires eligibility, zone, generation, and live-location checks.");
            }

            if (!IsStableId(request.ClosedInstanceEnvelopeId) || !IsStableId(request.SafeReturnEnvelopeId))
            {
                return Reject(
                    GuildPlanningStatus.InvalidRequest,
                    "AL-RAID-ENVELOPE-INVALID",
                    request.TargetAccountId,
                    "Transfer-in requires closed-instance and safe-return command envelopes.");
            }

            RaidParticipantSnapshot nextParticipant = new RaidParticipantSnapshot(
                participant.AccountId,
                participant.Response,
                RaidTransferState.InInstance,
                request.ClosedInstanceEnvelopeId,
                request.SafeReturnEnvelopeId,
                false,
                false);
            RaidCallSnapshot updated = WithState(
                WithParticipants(call, ReplaceParticipant(call.Participants, nextParticipant)),
                RaidCallState.Active,
                new RaidInstanceSnapshot(
                    RaidInstanceState.Active,
                    request.ClosedInstanceEnvelopeId,
                    policy.ClosedDungeonTopologyId),
                RaidOutcomeKind.None,
                false,
                false);
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult PlanTransferOut(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            RaidPlanningResult callGate = RequireCall(raids, request, out RaidCallSnapshot call);
            if (callGate != null)
            {
                return callGate;
            }

            if (FindActiveMember(guild, request.TargetAccountId) == null)
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-RAID-TARGET-NOT-MEMBER",
                    request.TargetAccountId,
                    "Transfer-out is denied for non-members.");
            }

            RaidParticipantSnapshot participant = FindParticipant(call, request.TargetAccountId);
            if (participant == null || participant.Transfer != RaidTransferState.InInstance)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-NOT-IN-INSTANCE",
                    request.TargetAccountId,
                    "Transfer-out requires an in-instance participant.");
            }

            if (!string.Equals(participant.SafeReturnEnvelopeId, request.SafeReturnEnvelopeId, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-RAID-SAFE-RETURN-MISMATCH",
                    request.TargetAccountId,
                    "Transfer-out must return to the validated pre-raid envelope.");
            }

            RaidParticipantSnapshot nextParticipant = new RaidParticipantSnapshot(
                participant.AccountId,
                participant.Response,
                RaidTransferState.Returned,
                participant.ClosedInstanceEnvelopeId,
                participant.SafeReturnEnvelopeId,
                false,
                false);
            IReadOnlyList<RaidParticipantSnapshot> participants = ReplaceParticipant(
                call.Participants,
                nextParticipant);
            bool anyoneInside = participants.Any(value => value.Transfer == RaidTransferState.InInstance);
            RaidCallSnapshot updated = WithState(
                WithParticipants(call, participants),
                anyoneInside ? RaidCallState.Active : RaidCallState.Completed,
                new RaidInstanceSnapshot(
                    anyoneInside ? RaidInstanceState.ExtractPending : RaidInstanceState.Extracted,
                    call.Instance.ClosedInstanceEnvelopeId,
                    policy.ClosedDungeonTopologyId),
                anyoneInside ? RaidOutcomeKind.None : RaidOutcomeKind.Success,
                false,
                false);
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult PlanReconcile(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            RaidPlanningResult callGate = RequireCall(raids, request, out RaidCallSnapshot call);
            if (callGate != null)
            {
                return callGate;
            }

            if (IsTerminalCall(call.State))
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-RAID-TERMINAL-XOR",
                    request.CallId,
                    "A terminal raid call cannot be reconciled into another terminal.");
            }

            switch (request.ReconcileReason)
            {
                case RaidReconcileReason.Disconnect:
                case RaidReconcileReason.Restart:
                    return ReconcileReturnParticipant(
                        request,
                        raids,
                        call,
                        requestFingerprint,
                        RaidTransferState.Returned);
                case RaidReconcileReason.PartialTransfer:
                    return ReconcileReturnParticipant(
                        request,
                        raids,
                        call,
                        requestFingerprint,
                        RaidTransferState.Returned);
                case RaidReconcileReason.InstanceFailure:
                    return ReconcileInstanceFailure(request, raids, call, requestFingerprint);
                case RaidReconcileReason.UnknownOutcome:
                    return ReconcileUnknown(request, raids, call, requestFingerprint);
                default:
                    return Reject(
                        GuildPlanningStatus.InvalidRequest,
                        "AL-RAID-RECONCILE-UNKNOWN",
                        request.OperationId,
                        "The reconcile reason is not supported.");
            }
        }

        private RaidPlanningResult ReconcileReturnParticipant(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            RaidCallSnapshot call,
            string requestFingerprint,
            RaidTransferState transfer)
        {
            RaidParticipantSnapshot participant = FindParticipant(call, request.TargetAccountId);
            if (participant == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-RAID-PARTICIPANT-MISSING",
                    request.TargetAccountId,
                    "Reconcile target is not on the raid call.");
            }

            string safeReturn = string.IsNullOrEmpty(participant.SafeReturnEnvelopeId)
                ? request.SafeReturnEnvelopeId
                : participant.SafeReturnEnvelopeId;
            RaidParticipantSnapshot nextParticipant = new RaidParticipantSnapshot(
                participant.AccountId,
                participant.Response,
                transfer,
                participant.ClosedInstanceEnvelopeId,
                safeReturn,
                false,
                false);
            RaidCallSnapshot updated = WithParticipants(
                call,
                ReplaceParticipant(call.Participants, nextParticipant));
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult ReconcileInstanceFailure(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            RaidCallSnapshot call,
            string requestFingerprint)
        {
            RaidParticipantSnapshot[] participants = call.Participants
                .Select(value => new RaidParticipantSnapshot(
                    value.AccountId,
                    value.Response,
                    value.Transfer == RaidTransferState.InInstance ||
                    value.Transfer == RaidTransferState.TransferInPending
                        ? RaidTransferState.Returned
                        : value.Transfer,
                    value.ClosedInstanceEnvelopeId,
                    value.SafeReturnEnvelopeId,
                    false,
                    false))
                .ToArray();
            RaidCallSnapshot updated = WithState(
                WithParticipants(call, participants),
                RaidCallState.Failed,
                new RaidInstanceSnapshot(
                    RaidInstanceState.ForceRelease,
                    call.Instance.ClosedInstanceEnvelopeId,
                    policy.ClosedDungeonTopologyId),
                RaidOutcomeKind.Indeterminate,
                false,
                false);
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult ReconcileUnknown(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            RaidCallSnapshot call,
            string requestFingerprint)
        {
            RaidParticipantSnapshot[] participants = call.Participants
                .Select(value => new RaidParticipantSnapshot(
                    value.AccountId,
                    value.Response,
                    value.Transfer == RaidTransferState.InInstance
                        ? RaidTransferState.Indeterminate
                        : value.Transfer,
                    value.ClosedInstanceEnvelopeId,
                    value.SafeReturnEnvelopeId,
                    false,
                    false))
                .ToArray();
            RaidCallSnapshot updated = WithState(
                WithParticipants(call, participants),
                RaidCallState.Failed,
                call.Instance,
                RaidOutcomeKind.Indeterminate,
                false,
                false);
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult PlanCancel(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            RaidPlanningResult callGate = RequireCall(raids, request, out RaidCallSnapshot call);
            if (callGate != null)
            {
                return callGate;
            }

            GuildMemberSnapshot actor = FindActiveMember(guild, request.ActorAccountId);
            if (actor == null || !CanOpenRaidCalls(actor.Role))
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-RAID-CANCEL-UNAUTHORIZED",
                    request.ActorAccountId,
                    "Cancel requires a current Guild Master or Officer.");
            }

            if (IsTerminalCall(call.State))
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-RAID-TERMINAL-XOR",
                    request.CallId,
                    "A terminal raid call cannot also be cancelled.");
            }

            RaidCallSnapshot updated = WithState(
                call,
                RaidCallState.Cancelled,
                call.Instance,
                RaidOutcomeKind.Failure,
                false,
                false);
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult PlanExpire(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            string requestFingerprint)
        {
            RaidPlanningResult callGate = RequireCall(raids, request, out RaidCallSnapshot call);
            if (callGate != null)
            {
                return callGate;
            }

            if (call.State != RaidCallState.Accepting ||
                request.TrustedClockUnixSeconds < call.WindowEndUnixSeconds)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-RAID-EXPIRE-WINDOW",
                    request.CallId,
                    "Expire applies only after the 30-minute accepting window.");
            }

            RaidCallSnapshot updated = WithState(
                call,
                RaidCallState.Expired,
                call.Instance,
                RaidOutcomeKind.Failure,
                false,
                false);
            return Commit(request, raids, requestFingerprint, ReplaceOrAddCall(raids.Calls, updated));
        }

        private RaidPlanningResult Commit(
            GuildRaidMusterTransitionRequest request,
            RaidAuthoritySnapshot raids,
            string requestFingerprint,
            IReadOnlyList<RaidCallSnapshot> calls)
        {
            if (raids.Receipts.Count >= MaximumReceipts)
            {
                return Reject(
                    GuildPlanningStatus.Overflow,
                    "AL-RAID-RECEIPT-OVERFLOW",
                    request.OperationId,
                    "Raid receipt capacity is exhausted.");
            }

            long nextRevision = raids.Revision + 1;
            string planHash = HashParts(
                "guild_raid_plan_v1",
                requestFingerprint,
                nextRevision.ToString(CultureInfo.InvariantCulture),
                request.CallId);
            var receipt = new RaidOperationReceipt(
                request.OperationId,
                request.Operation,
                requestFingerprint,
                request.CallId,
                request.GuildId,
                request.ActorAccountId,
                request.TargetAccountId,
                nextRevision,
                planHash,
                true);
            RaidOperationReceipt[] receipts = raids.Receipts.Concat(new[] { receipt }).ToArray();
            var candidate = new RaidAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                nextRevision,
                policy.Binding,
                calls,
                receipts,
                true);
            var plan = new GuildRaidMusterTransitionPlan(
                request.Operation,
                requestFingerprint,
                raids,
                candidate,
                receipt,
                planHash);
            return new RaidPlanningResult(
                GuildPlanningStatus.Prepared,
                plan,
                null,
                Array.Empty<GuildDiagnostic>());
        }

        private RaidPlanningResult RequireCall(
            RaidAuthoritySnapshot raids,
            GuildRaidMusterTransitionRequest request,
            out RaidCallSnapshot call)
        {
            call = raids.Calls.FirstOrDefault(value =>
                string.Equals(value.CallId, request.CallId, StringComparison.Ordinal) &&
                string.Equals(value.GuildId, request.GuildId, StringComparison.Ordinal));
            if (call == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-RAID-CALL-MISSING",
                    request.CallId,
                    "The raid call was not found.");
            }

            return null;
        }

        private RaidPlanningResult ValidatePolicy()
        {
            if (policy == null ||
                policy.Status == GuildCatalogStatus.Unavailable ||
                !policy.IsComplete)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-RAID-POLICY-UNAVAILABLE",
                    string.Empty,
                    "Raid muster policy is unavailable.");
            }

            if (policy.Status != GuildCatalogStatus.Ready ||
                !IsValidBinding(policy.Binding) ||
                policy.CallWindowMinutes != 30 ||
                policy.CallsPerGuildPerWeek != 1 ||
                policy.ParallelSlots != 1 ||
                policy.MinJoinCount < 1 ||
                policy.BossSlots == null ||
                policy.BossSlots.Count != 4 ||
                policy.BossSlots.Select(slot => slot.SlotIndex).Distinct().Count() != 4 ||
                policy.BossSlots.Any(slot => slot.SlotIndex < 0 || slot.SlotIndex > 3 || !IsStableId(slot.BossProfileId)) ||
                !IsStableId(policy.ClosedDungeonTopologyId) ||
                policy.ReservedPublicDungeonIds == null ||
                policy.ReservedPublicDungeonIds.Count == 0 ||
                !policy.WarNeverBypassesConsent)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-RAID-POLICY-MALFORMED",
                    string.Empty,
                    "Raid muster policy failed closed validation.");
            }

            return null;
        }

        private static RaidPlanningResult ValidateMembership(
            GuildAuthoritySnapshot membership,
            GuildRaidMusterTransitionRequest request)
        {
            if (membership == null ||
                membership.Status == GuildAuthorityStatus.Unavailable ||
                !membership.IsComplete ||
                membership.Guilds == null)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-RAID-MEMBERSHIP-UNAVAILABLE",
                    request.GuildId,
                    "Guild membership authority is unavailable.");
            }

            if (membership.Revision != request.ExpectedGuildRevision)
            {
                return Reject(
                    GuildPlanningStatus.StaleGuild,
                    "AL-RAID-GUILD-STALE",
                    request.GuildId,
                    "The request is not fenced to the current Guild revision.");
            }

            GuildSnapshot guild = membership.Guilds.FirstOrDefault(value =>
                string.Equals(value.GuildId, request.GuildId, StringComparison.Ordinal));
            if (guild == null || guild.Status != GuildStatus.Active || guild.Members == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-RAID-GUILD-MISSING",
                    request.GuildId,
                    "The Guild was not found or is not active.");
            }

            return null;
        }

        private static RaidPlanningResult ValidateRaids(RaidAuthoritySnapshot raids)
        {
            if (raids == null ||
                raids.Status == GuildAuthorityStatus.Unavailable ||
                !raids.IsComplete ||
                raids.Calls == null ||
                raids.Receipts == null)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-RAID-AUTHORITY-UNAVAILABLE",
                    string.Empty,
                    "Raid authority is unavailable.");
            }

            return null;
        }

        private static RaidPlanningResult ValidateAlliance(AllianceAuthoritySnapshot alliance)
        {
            if (alliance == null ||
                alliance.Status == AllianceAuthorityStatus.Unavailable ||
                !alliance.IsComplete ||
                alliance.Alliances == null ||
                alliance.Wars == null)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-RAID-ALLIANCE-UNAVAILABLE",
                    string.Empty,
                    "Alliance relation snapshots are unavailable.");
            }

            return null;
        }

        private static RaidPlanningResult ClassifyReplay(
            GuildRaidMusterTransitionRequest request,
            string requestFingerprint,
            IReadOnlyList<RaidOperationReceipt> receipts)
        {
            RaidOperationReceipt existing = receipts.FirstOrDefault(value =>
                string.Equals(value.OperationId, request.OperationId, StringComparison.Ordinal));
            if (existing == null)
            {
                return null;
            }

            if (string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal) &&
                existing.Operation == request.Operation)
            {
                return new RaidPlanningResult(
                    GuildPlanningStatus.AlreadyCommitted,
                    null,
                    existing,
                    Array.Empty<GuildDiagnostic>());
            }

            return Reject(
                GuildPlanningStatus.Conflict,
                "AL-RAID-OPERATION-CONFLICT",
                request.OperationId,
                "The operation id was reused with a different fingerprint.");
        }

        private RaidBossSlotDefinition ResolveSlot(long seasonEpoch, long weekId)
        {
            if (policy == null || policy.BossSlots == null || policy.BossSlots.Count != 4)
            {
                return null;
            }

            long mixed = seasonEpoch + weekId;
            int slotIndex = (int)((mixed % 4 + 4) % 4);
            return policy.BossSlots.FirstOrDefault(slot => slot.SlotIndex == slotIndex);
        }

        private bool IsReservedPublicDungeonId(string closedInstanceId)
        {
            return policy.ReservedPublicDungeonIds.Any(value =>
                string.Equals(value, closedInstanceId, StringComparison.Ordinal));
        }

        private static bool CanOpenRaidCalls(GuildRole role)
        {
            return role == GuildRole.Master || role == GuildRole.Officer;
        }

        private static bool IsTerminalCall(RaidCallState state)
        {
            return state == RaidCallState.Completed ||
                   state == RaidCallState.Cancelled ||
                   state == RaidCallState.Failed ||
                   state == RaidCallState.Expired;
        }

        private static GuildMemberSnapshot FindActiveMember(GuildSnapshot guild, string accountId)
        {
            return guild.Members.FirstOrDefault(member =>
                string.Equals(member.AccountId, accountId, StringComparison.Ordinal) &&
                member.State == GuildMembershipState.Active);
        }

        private static RaidParticipantSnapshot FindParticipant(RaidCallSnapshot call, string accountId)
        {
            return call.Participants.FirstOrDefault(value =>
                string.Equals(value.AccountId, accountId, StringComparison.Ordinal));
        }

        private static IReadOnlyList<RaidCallSnapshot> ReplaceOrAddCall(
            IReadOnlyList<RaidCallSnapshot> calls,
            RaidCallSnapshot next)
        {
            var list = new List<RaidCallSnapshot>();
            bool replaced = false;
            foreach (RaidCallSnapshot call in calls)
            {
                if (string.Equals(call.CallId, next.CallId, StringComparison.Ordinal))
                {
                    list.Add(next);
                    replaced = true;
                }
                else
                {
                    list.Add(call);
                }
            }

            if (!replaced)
            {
                list.Add(next);
            }

            return list;
        }

        private static IReadOnlyList<RaidParticipantSnapshot> ReplaceParticipant(
            IReadOnlyList<RaidParticipantSnapshot> participants,
            RaidParticipantSnapshot next)
        {
            return participants
                .Select(value => string.Equals(value.AccountId, next.AccountId, StringComparison.Ordinal)
                    ? next
                    : value)
                .ToArray();
        }

        private static RaidCallSnapshot WithParticipants(
            RaidCallSnapshot call,
            IReadOnlyList<RaidParticipantSnapshot> participants)
        {
            return new RaidCallSnapshot(
                call.CallId,
                call.GuildId,
                call.ActorAccountId,
                call.State,
                call.WeekId,
                call.SeasonEpoch,
                call.BossProfileId,
                call.ClosedInstanceId,
                call.ClosedDungeonTopologyId,
                call.WindowStartUnixSeconds,
                call.WindowEndUnixSeconds,
                participants,
                call.Instance,
                call.Outcome,
                call.GrantsReward,
                call.AppliesLockout);
        }

        private static RaidCallSnapshot WithState(
            RaidCallSnapshot call,
            RaidCallState state,
            RaidInstanceSnapshot instance,
            RaidOutcomeKind outcome,
            bool grantsReward,
            bool appliesLockout)
        {
            return new RaidCallSnapshot(
                call.CallId,
                call.GuildId,
                call.ActorAccountId,
                state,
                call.WeekId,
                call.SeasonEpoch,
                call.BossProfileId,
                call.ClosedInstanceId,
                call.ClosedDungeonTopologyId,
                call.WindowStartUnixSeconds,
                call.WindowEndUnixSeconds,
                call.Participants,
                instance,
                outcome,
                grantsReward,
                appliesLockout);
        }

        private static bool IsValidRequest(GuildRaidMusterTransitionRequest request)
        {
            return request != null &&
                   IsStableId(request.OperationId) &&
                   IsStableId(request.ActorAccountId) &&
                   IsStableId(request.GuildId) &&
                   IsStableId(request.CallId) &&
                   request.WeekId >= 0 &&
                   request.SeasonEpoch >= 0 &&
                   request.TrustedClockUnixSeconds >= 0 &&
                   request.ExpectedRaidRevision >= 0 &&
                   request.ExpectedGuildRevision >= 0 &&
                   IsValidBinding(request.ExpectedCatalogBinding);
        }

        private static string RequestFingerprint(GuildRaidMusterTransitionRequest request)
        {
            return HashParts(
                "guild_raid_request_v1",
                ((int)request.Operation).ToString(CultureInfo.InvariantCulture),
                request.OperationId,
                request.ActorAccountId,
                request.GuildId,
                request.CallId,
                request.TargetAccountId,
                request.WeekId.ToString(CultureInfo.InvariantCulture),
                request.SeasonEpoch.ToString(CultureInfo.InvariantCulture),
                request.BossProfileId,
                request.ClosedInstanceId,
                request.ClosedInstanceEnvelopeId,
                request.SafeReturnEnvelopeId,
                request.SceneName,
                request.TrustedClockUnixSeconds.ToString(CultureInfo.InvariantCulture),
                request.EligibilityPassed ? "1" : "0",
                request.ZoneAllowed ? "1" : "0",
                request.GenerationContinuous ? "1" : "0",
                request.LiveLocationValid ? "1" : "0",
                ((int)request.ReconcileReason).ToString(CultureInfo.InvariantCulture));
        }

        private static bool IsValidBinding(GuildCatalogBinding binding)
        {
            return binding != null &&
                   binding.SchemaVersion > 0 &&
                   IsOpaqueId(binding.ContentVersion) &&
                   IsOpaqueId(binding.SourceRevision) &&
                   IsSha256(binding.CatalogHash);
        }

        private static bool BindingEquals(GuildCatalogBinding left, GuildCatalogBinding right)
        {
            return left != null && right != null &&
                   left.SchemaVersion == right.SchemaVersion &&
                   string.Equals(left.ContentVersion, right.ContentVersion, StringComparison.Ordinal) &&
                   string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CatalogHash, right.CatalogHash, StringComparison.Ordinal);
        }

        private static bool IsOpaqueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                Encoding.UTF8.GetByteCount(value) > MaximumIdentityUtf8Bytes)
            {
                return false;
            }

            return value.All(character => !char.IsControl(character) && !char.IsWhiteSpace(character));
        }

        private static bool IsStableId(string value)
        {
            if (!IsOpaqueId(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool previousUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed = (character >= 'a' && character <= 'z') ||
                               (character >= '0' && character <= '9') ||
                               character == '_';
                if (!allowed || (character == '_' && previousUnderscore))
                {
                    return false;
                }

                previousUnderscore = character == '_';
            }

            return value[value.Length - 1] != '_';
        }

        private static bool IsSha256(string value)
        {
            return value != null && value.Length == 64 && value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));
        }

        private static string HashParts(params string[] parts)
        {
            var canonical = new StringBuilder();
            foreach (string part in parts)
            {
                string value = part ?? string.Empty;
                canonical.Append(
                    Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture));
                canonical.Append(':');
                canonical.Append(value);
            }

            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))
                    .Select(value => value.ToString("x2")));
            }
        }

        private static RaidPlanningResult Reject(
            GuildPlanningStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new RaidPlanningResult(
                status,
                null,
                null,
                new[] { new GuildDiagnostic(code, subjectId ?? string.Empty, message) });
        }
    }
}
