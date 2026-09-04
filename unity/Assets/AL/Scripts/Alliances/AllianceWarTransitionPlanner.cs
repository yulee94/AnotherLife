using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Guilds;

namespace AL.Alliances
{
    public sealed class AllianceWarTransitionPlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const int MaximumAlliances = 256;
        private const int MaximumMembersPerAlliance = 64;
        private const int MaximumPendingRequests = 2048;
        private const int MaximumWars = 1024;
        private const int MaximumReceipts = 4096;
        private const long SecondsPerHour = 3600L;

        private readonly AllianceWarPolicySnapshot policy;

        public AllianceWarTransitionPlanner(AllianceWarPolicySnapshot policy)
        {
            this.policy = policy;
        }

        public AlliancePlanningResult Plan(
            AllianceTransitionRequest request,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    AlliancePlanningStatus.InvalidRequest,
                    "AL-ALLIANCE-REQUEST-INVALID",
                    request?.OperationId,
                    "Alliance transition identity, fields, or revisions are invalid.");
            }

            AlliancePlanningResult policyGate = ValidatePolicy(policy);
            if (policyGate != null)
            {
                return policyGate;
            }

            if (!BindingEquals(request.ExpectedCatalogBinding, policy.Binding))
            {
                return Reject(
                    AlliancePlanningStatus.StaleCatalog,
                    "AL-ALLIANCE-CATALOG-STALE",
                    request.OperationId,
                    "The request is not fenced to the accepted Alliance policy catalog.");
            }

            AlliancePlanningResult authorityGate = ValidateAuthority(snapshot);
            if (authorityGate != null)
            {
                return authorityGate;
            }

            AlliancePlanningResult guildGate = ValidateGuildAuthority(guilds);
            if (guildGate != null)
            {
                return guildGate;
            }

            string requestFingerprint = RequestFingerprint(request);
            AlliancePlanningResult replay = ClassifyReplay(
                request, requestFingerprint, snapshot.Receipts);
            if (replay != null)
            {
                return replay;
            }

            if (IsPendingResolution(request.Operation) &&
                snapshot.PendingRequests.All(row =>
                    !string.Equals(row.RequestId, request.PendingRequestId, StringComparison.Ordinal)))
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-PENDING-NOT-FOUND",
                    request.PendingRequestId,
                    "Alliance pending request was not found.");
            }

            if (snapshot.Revision != request.ExpectedAuthorityRevision)
            {
                return Reject(
                    AlliancePlanningStatus.StaleAuthority,
                    "AL-ALLIANCE-AUTHORITY-STALE",
                    request.OperationId,
                    "Expected Alliance authority revision is stale.");
            }

            if (snapshot.Revision == long.MaxValue)
            {
                return Reject(
                    AlliancePlanningStatus.Overflow,
                    "AL-ALLIANCE-AUTHORITY-REVISION-OVERFLOW",
                    request.OperationId,
                    "Alliance authority revision cannot advance.");
            }

            if (snapshot.Receipts.Count >= MaximumReceipts)
            {
                return Reject(
                    AlliancePlanningStatus.Malformed,
                    "AL-ALLIANCE-RECEIPT-CAPACITY",
                    request.OperationId,
                    "Alliance receipt history cannot safely accept another row.");
            }

            try
            {
                AlliancePlanningResult race = DetectMembershipRace(snapshot, guilds);
                if (race != null &&
                    request.Operation != AllianceOperation.Decline &&
                    request.Operation != AllianceOperation.DeclineWarEnd)
                {
                    return race;
                }

                switch (request.Operation)
                {
                    case AllianceOperation.Propose:
                        return PlanPropose(request, requestFingerprint, snapshot, guilds);
                    case AllianceOperation.Accept:
                        return PlanProposalResolution(
                            request, requestFingerprint, snapshot, guilds, true);
                    case AllianceOperation.Decline:
                        return PlanProposalResolution(
                            request, requestFingerprint, snapshot, guilds, false);
                    case AllianceOperation.Leave:
                        return PlanLeave(request, requestFingerprint, snapshot, guilds);
                    case AllianceOperation.Disband:
                        return PlanDisband(request, requestFingerprint, snapshot, guilds);
                    case AllianceOperation.DeclareWar:
                        return PlanDeclareWar(request, requestFingerprint, snapshot, guilds);
                    case AllianceOperation.ProposeWarEnd:
                        return PlanProposeWarEnd(request, requestFingerprint, snapshot, guilds);
                    case AllianceOperation.AcceptWarEnd:
                        return PlanWarEndResolution(
                            request, requestFingerprint, snapshot, guilds, true);
                    case AllianceOperation.DeclineWarEnd:
                        return PlanWarEndResolution(
                            request, requestFingerprint, snapshot, guilds, false);
                    default:
                        return Reject(
                            AlliancePlanningStatus.InvalidRequest,
                            "AL-ALLIANCE-OPERATION-INVALID",
                            request.OperationId,
                            "Alliance operation is invalid.");
                }
            }
            catch (OverflowException)
            {
                return Reject(
                    AlliancePlanningStatus.Overflow,
                    "AL-ALLIANCE-ARITHMETIC-OVERFLOW",
                    request.OperationId,
                    "Alliance candidate arithmetic overflowed.");
            }
        }

        public AllianceHostilityDecision EvaluateForcedHostility(
            AllianceHostilityQuery query,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            if (query == null ||
                !IsStableId(query.ActorGuildId) ||
                !IsStableId(query.TargetGuildId) ||
                !Enum.IsDefined(typeof(AllianceZoneKind), query.Zone) ||
                query.ClockUnixSeconds < 0 ||
                ValidatePolicy(policy) != null ||
                ValidateAuthority(snapshot) != null ||
                ValidateGuildAuthority(guilds) != null)
            {
                return new AllianceHostilityDecision(
                    AllianceHostilityKind.Indeterminate, AllianceWarState.None, false);
            }

            if (DetectMembershipRace(snapshot, guilds) != null)
            {
                return new AllianceHostilityDecision(
                    AllianceHostilityKind.Indeterminate, AllianceWarState.None, false);
            }

            if (string.Equals(query.ActorGuildId, query.TargetGuildId, StringComparison.Ordinal))
            {
                return new AllianceHostilityDecision(
                    AllianceHostilityKind.Immune, AllianceWarState.None, false);
            }

            if (policy.ImmuneZones.Contains(query.Zone))
            {
                return new AllianceHostilityDecision(
                    AllianceHostilityKind.Immune, AllianceWarState.None, false);
            }

            AllianceSnapshot actorAlliance = FindActiveAllianceForGuild(snapshot, query.ActorGuildId);
            AllianceSnapshot targetAlliance = FindActiveAllianceForGuild(snapshot, query.TargetGuildId);
            if (actorAlliance != null &&
                targetAlliance != null &&
                string.Equals(
                    actorAlliance.AllianceId, targetAlliance.AllianceId, StringComparison.Ordinal))
            {
                return new AllianceHostilityDecision(
                    AllianceHostilityKind.Immune, AllianceWarState.None, false);
            }

            AllianceWarSnapshot war = FindWarBetween(
                snapshot, actorAlliance?.AllianceId, targetAlliance?.AllianceId);
            AllianceWarState effective = EffectiveWarState(war, query.ClockUnixSeconds);
            if (effective == AllianceWarState.Active)
            {
                return new AllianceHostilityDecision(
                    AllianceHostilityKind.ForcedHostile, effective, true);
            }

            return new AllianceHostilityDecision(
                AllianceHostilityKind.NotForced, effective, false);
        }

        private AlliancePlanningResult PlanPropose(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            if (snapshot.Alliances.Any(row =>
                    string.Equals(row.AllianceId, request.AllianceId, StringComparison.Ordinal)))
            {
                return Reject(
                    AlliancePlanningStatus.Conflict,
                    "AL-ALLIANCE-ID-CONFLICT",
                    request.AllianceId,
                    "Alliance identity is already reserved.");
            }

            if (snapshot.Alliances.Count >= MaximumAlliances)
            {
                return Reject(
                    AlliancePlanningStatus.Malformed,
                    "AL-ALLIANCE-CAPACITY",
                    request.AllianceId,
                    "Alliance authority cannot safely accept another Alliance.");
            }

            GuildSnapshot actorGuild = FindGuild(guilds, request.ActorGuildId);
            GuildSnapshot targetGuild = FindGuild(guilds, request.TargetGuildId);
            if (actorGuild == null || targetGuild == null ||
                actorGuild.Status != GuildStatus.Active ||
                targetGuild.Status != GuildStatus.Active)
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-GUILD-NOT-FOUND",
                    request.TargetGuildId,
                    "Proposer or invited Guild does not exist.");
            }

            if (actorGuild.Revision != request.ExpectedActorGuildRevision ||
                targetGuild.Revision != request.ExpectedTargetGuildRevision)
            {
                return StaleGuild(request.ActorGuildId);
            }

            AlliancePlanningResult actorAuth = RequireGuildMaster(
                actorGuild, request.ActorAccountId, request.ActorImmutableRealmId);
            if (actorAuth != null)
            {
                return actorAuth;
            }

            if (!string.Equals(
                    actorGuild.ImmutableRealmId,
                    targetGuild.ImmutableRealmId,
                    StringComparison.Ordinal))
            {
                return RealmConflict(request.TargetGuildId);
            }

            if (FindActiveAllianceForGuild(snapshot, request.ActorGuildId) != null ||
                FindActiveAllianceForGuild(snapshot, request.TargetGuildId) != null)
            {
                return Reject(
                    AlliancePlanningStatus.Conflict,
                    "AL-ALLIANCE-GUILD-ALREADY-MEMBER",
                    request.ActorGuildId,
                    "A Guild already has an active Alliance membership.");
            }

            AlliancePlanningResult pendingGate = EnsurePendingAvailable(
                snapshot, request.PendingRequestId, request.ActorGuildId, request.TargetGuildId);
            if (pendingGate != null)
            {
                return pendingGate;
            }

            var pending = new AlliancePendingRequest(
                request.PendingRequestId,
                AlliancePendingKind.AllianceProposal,
                request.AllianceId,
                request.ActorGuildId,
                request.TargetGuildId,
                string.Empty,
                request.ActorAccountId,
                0,
                actorGuild.Revision,
                targetGuild.Revision,
                true);
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                snapshot.Alliances,
                InsertPending(snapshot.PendingRequests, pending),
                snapshot.Wars,
                0);
        }

        private AlliancePlanningResult PlanProposalResolution(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds,
            bool accept)
        {
            AlliancePendingRequest pending = snapshot.PendingRequests.SingleOrDefault(row =>
                string.Equals(row.RequestId, request.PendingRequestId, StringComparison.Ordinal));
            if (pending == null || pending.Kind != AlliancePendingKind.AllianceProposal)
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-PENDING-NOT-FOUND",
                    request.PendingRequestId,
                    "Alliance proposal pending request was not found.");
            }

            if (!pending.IsSupported)
            {
                return Reject(
                    AlliancePlanningStatus.Unsupported,
                    "AL-ALLIANCE-PENDING-UNSUPPORTED",
                    pending.RequestId,
                    "Unknown-future Alliance proposal is preserved read-only.");
            }

            if (!string.Equals(pending.AllianceId, request.AllianceId, StringComparison.Ordinal))
            {
                return Reject(
                    AlliancePlanningStatus.Conflict,
                    "AL-ALLIANCE-PENDING-MISMATCH",
                    pending.RequestId,
                    "Pending Alliance identity does not match the request.");
            }

            GuildSnapshot proposerGuild = FindGuild(guilds, pending.ProposerGuildId);
            GuildSnapshot invitedGuild = FindGuild(guilds, pending.TargetGuildId);
            if (proposerGuild == null || invitedGuild == null ||
                proposerGuild.Status != GuildStatus.Active ||
                invitedGuild.Status != GuildStatus.Active)
            {
                return accept
                    ? Indeterminate(pending.TargetGuildId)
                    : RemovePendingPlan(request, requestFingerprint, snapshot, pending, 0);
            }

            GuildSnapshot actorGuild = FindGuild(guilds, request.ActorGuildId);
            if (actorGuild == null || actorGuild.Status != GuildStatus.Active)
            {
                return Unauthorized(request.ActorAccountId);
            }

            bool actorIsInvitedMaster = IsActiveMaster(invitedGuild, request.ActorAccountId);
            bool actorIsProposerMaster = IsActiveMaster(proposerGuild, request.ActorAccountId);
            if (accept)
            {
                if (!actorIsInvitedMaster ||
                    !string.Equals(
                        request.ActorGuildId, pending.TargetGuildId, StringComparison.Ordinal))
                {
                    return Unauthorized(request.ActorAccountId);
                }

                if (invitedGuild.Revision != pending.TargetGuildRevision ||
                    proposerGuild.Revision != pending.ProposerGuildRevision ||
                    invitedGuild.Revision != request.ExpectedActorGuildRevision ||
                    proposerGuild.Revision != request.ExpectedTargetGuildRevision)
                {
                    return StaleGuild(pending.TargetGuildId);
                }

                if (FindActiveAllianceForGuild(snapshot, pending.ProposerGuildId) != null ||
                    FindActiveAllianceForGuild(snapshot, pending.TargetGuildId) != null)
                {
                    return Reject(
                        AlliancePlanningStatus.Conflict,
                        "AL-ALLIANCE-GUILD-ALREADY-MEMBER",
                        pending.TargetGuildId,
                        "A Guild already has an active Alliance membership.");
                }

                if (!string.Equals(
                        proposerGuild.ImmutableRealmId,
                        invitedGuild.ImmutableRealmId,
                        StringComparison.Ordinal))
                {
                    return RealmConflict(pending.TargetGuildId);
                }

                IReadOnlyList<AllianceMemberGuildSnapshot> members = new[]
                {
                    new AllianceMemberGuildSnapshot(pending.ProposerGuildId, proposerGuild.Revision),
                    new AllianceMemberGuildSnapshot(pending.TargetGuildId, invitedGuild.Revision)
                }.OrderBy(row => row.GuildId, StringComparer.Ordinal).ToArray();
                var alliance = new AllianceSnapshot(
                    pending.AllianceId,
                    proposerGuild.ImmutableRealmId,
                    HashParts(
                        "alliance_identity_v1",
                        pending.AllianceId,
                        proposerGuild.ImmutableRealmId,
                        pending.ProposerGuildId,
                        pending.TargetGuildId),
                    1,
                    AllianceRelationState.Active,
                    pending.ProposerGuildId,
                    members);
                return CreatePlan(
                    request,
                    requestFingerprint,
                    snapshot,
                    InsertAlliance(snapshot.Alliances, alliance),
                    RemovePending(snapshot.PendingRequests, pending.RequestId),
                    snapshot.Wars,
                    alliance.Revision);
            }

            if (!actorIsInvitedMaster && !actorIsProposerMaster)
            {
                return Unauthorized(request.ActorAccountId);
            }

            return RemovePendingPlan(request, requestFingerprint, snapshot, pending, 0);
        }

        private AlliancePlanningResult PlanLeave(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            AllianceSnapshot alliance = RequireActiveAlliance(snapshot, request.AllianceId);
            if (alliance == null)
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-ACTIVE-NOT-FOUND",
                    request.AllianceId,
                    "The requested active Alliance does not exist.");
            }

            AlliancePlanningResult revisionGate = RequireAllianceRevision(request, alliance);
            if (revisionGate != null)
            {
                return revisionGate;
            }

            AllianceMemberGuildSnapshot membership = alliance.MemberGuilds.SingleOrDefault(row =>
                string.Equals(row.GuildId, request.ActorGuildId, StringComparison.Ordinal));
            if (membership == null)
            {
                return Reject(
                    AlliancePlanningStatus.Ineligible,
                    "AL-ALLIANCE-GUILD-NOT-MEMBER",
                    request.ActorGuildId,
                    "Guild is not an Alliance member.");
            }

            GuildSnapshot actorGuild = FindGuild(guilds, request.ActorGuildId);
            if (actorGuild == null || actorGuild.Status != GuildStatus.Active)
            {
                return Indeterminate(request.ActorGuildId);
            }

            if (actorGuild.Revision != request.ExpectedActorGuildRevision)
            {
                return StaleGuild(request.ActorGuildId);
            }

            AlliancePlanningResult masterGate = RequireGuildMaster(
                actorGuild, request.ActorAccountId, request.ActorImmutableRealmId);
            if (masterGate != null)
            {
                return masterGate;
            }

            IReadOnlyList<AllianceMemberGuildSnapshot> remaining = alliance.MemberGuilds
                .Where(row => !string.Equals(row.GuildId, request.ActorGuildId, StringComparison.Ordinal))
                .ToArray();
            AllianceSnapshot candidate = remaining.Count < 2
                ? CopyAlliance(
                    alliance,
                    checked(alliance.Revision + 1),
                    AllianceRelationState.Absent,
                    string.Empty,
                    Array.Empty<AllianceMemberGuildSnapshot>())
                : CopyAlliance(
                    alliance,
                    checked(alliance.Revision + 1),
                    AllianceRelationState.Active,
                    DeriveLeadGuildId(remaining, request.ActorGuildId, alliance.LeadGuildId),
                    remaining);
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceAlliance(snapshot.Alliances, candidate),
                RemovePendingForAlliance(snapshot.PendingRequests, alliance.AllianceId),
                EndWarsForAlliance(snapshot.Wars, alliance.AllianceId),
                candidate.Revision);
        }

        private AlliancePlanningResult PlanDisband(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            AllianceSnapshot alliance = RequireActiveAlliance(snapshot, request.AllianceId);
            if (alliance == null)
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-ACTIVE-NOT-FOUND",
                    request.AllianceId,
                    "The requested active Alliance does not exist.");
            }

            AlliancePlanningResult revisionGate = RequireAllianceRevision(request, alliance);
            if (revisionGate != null)
            {
                return revisionGate;
            }

            AlliancePlanningResult leaderGate = RequireDerivedLeader(
                request, alliance, guilds);
            if (leaderGate != null)
            {
                return leaderGate;
            }

            AllianceSnapshot candidate = CopyAlliance(
                alliance,
                checked(alliance.Revision + 1),
                AllianceRelationState.Absent,
                string.Empty,
                Array.Empty<AllianceMemberGuildSnapshot>());
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceAlliance(snapshot.Alliances, candidate),
                RemovePendingForAlliance(snapshot.PendingRequests, alliance.AllianceId),
                EndWarsForAlliance(snapshot.Wars, alliance.AllianceId),
                candidate.Revision);
        }

        private AlliancePlanningResult PlanDeclareWar(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            AllianceSnapshot attacker = RequireActiveAlliance(snapshot, request.AllianceId);
            AllianceSnapshot defender = RequireActiveAlliance(snapshot, request.TargetAllianceId);
            if (attacker == null || defender == null)
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-WAR-TARGET-NOT-FOUND",
                    request.TargetAllianceId,
                    "Attacker or defender Alliance is not active.");
            }

            AlliancePlanningResult revisionGate = RequireAllianceRevision(request, attacker);
            if (revisionGate != null)
            {
                return revisionGate;
            }

            if (defender.Revision != request.ExpectedTargetGuildRevision)
            {
                return Reject(
                    AlliancePlanningStatus.StaleAlliance,
                    "AL-ALLIANCE-TARGET-REVISION-STALE",
                    defender.AllianceId,
                    "Expected defender Alliance revision is stale.");
            }

            if (string.Equals(attacker.AllianceId, defender.AllianceId, StringComparison.Ordinal))
            {
                return Reject(
                    AlliancePlanningStatus.Conflict,
                    "AL-ALLIANCE-WAR-SAME-ALLIANCE",
                    attacker.AllianceId,
                    "An Alliance cannot declare war on itself.");
            }

            if (!string.Equals(
                    attacker.ImmutableRealmId,
                    defender.ImmutableRealmId,
                    StringComparison.Ordinal) ||
                !AlliancesShareRealm(attacker, defender, guilds))
            {
                return RealmConflict(defender.AllianceId);
            }

            AlliancePlanningResult leaderGate = RequireDerivedLeader(request, attacker, guilds);
            if (leaderGate != null)
            {
                return leaderGate;
            }

            if (snapshot.Wars.Any(row =>
                    string.Equals(row.WarId, request.WarId, StringComparison.Ordinal)))
            {
                return Reject(
                    AlliancePlanningStatus.Conflict,
                    "AL-ALLIANCE-WAR-ID-CONFLICT",
                    request.WarId,
                    "War identity is already reserved.");
            }

            if (FindWarBetween(snapshot, attacker.AllianceId, defender.AllianceId) is AllianceWarSnapshot existing &&
                (existing.CommittedState == AllianceWarState.Declared ||
                 existing.CommittedState == AllianceWarState.Active))
            {
                return Reject(
                    AlliancePlanningStatus.Conflict,
                    "AL-ALLIANCE-WAR-EXISTS",
                    existing.WarId,
                    "An open war already exists between these Alliances.");
            }

            if (snapshot.Wars.Count >= MaximumWars)
            {
                return Reject(
                    AlliancePlanningStatus.Malformed,
                    "AL-ALLIANCE-WAR-CAPACITY",
                    request.WarId,
                    "Alliance authority cannot safely accept another war.");
            }

            long activatedAt = checked(request.ClockUnixSeconds + (policy.WarNoticeHours * SecondsPerHour));
            var war = new AllianceWarSnapshot(
                request.WarId,
                attacker.AllianceId,
                defender.AllianceId,
                AllianceWarState.Declared,
                request.ClockUnixSeconds,
                activatedAt,
                attacker.Revision,
                defender.Revision);
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                snapshot.Alliances,
                snapshot.PendingRequests,
                InsertWar(snapshot.Wars, war),
                attacker.Revision);
        }

        private AlliancePlanningResult PlanProposeWarEnd(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            AllianceSnapshot actorAlliance = RequireActiveAlliance(snapshot, request.AllianceId);
            AllianceSnapshot targetAlliance = RequireActiveAlliance(snapshot, request.TargetAllianceId);
            if (actorAlliance == null || targetAlliance == null)
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-WAR-TARGET-NOT-FOUND",
                    request.TargetAllianceId,
                    "War-end Alliances are not both active.");
            }

            AlliancePlanningResult revisionGate = RequireAllianceRevision(request, actorAlliance);
            if (revisionGate != null)
            {
                return revisionGate;
            }

            AlliancePlanningResult leaderGate = RequireDerivedLeader(request, actorAlliance, guilds);
            if (leaderGate != null)
            {
                return leaderGate;
            }

            AllianceWarSnapshot war = snapshot.Wars.SingleOrDefault(row =>
                string.Equals(row.WarId, request.WarId, StringComparison.Ordinal));
            AllianceWarState effective = EffectiveWarState(war, request.ClockUnixSeconds);
            if (war == null ||
                (effective != AllianceWarState.Declared && effective != AllianceWarState.Active) ||
                !WarMatches(war, actorAlliance.AllianceId, targetAlliance.AllianceId))
            {
                return Reject(
                    AlliancePlanningStatus.Ineligible,
                    "AL-ALLIANCE-WAR-NOT-OPEN",
                    request.WarId,
                    "There is no open war that can be mutually ended.");
            }

            AlliancePlanningResult pendingGate = EnsurePendingAvailable(
                snapshot, request.PendingRequestId, request.ActorGuildId, string.Empty);
            if (pendingGate != null)
            {
                return pendingGate;
            }

            var pending = new AlliancePendingRequest(
                request.PendingRequestId,
                AlliancePendingKind.WarEnd,
                actorAlliance.AllianceId,
                request.ActorGuildId,
                string.Empty,
                targetAlliance.AllianceId,
                request.ActorAccountId,
                actorAlliance.Revision,
                actorAlliance.Revision,
                targetAlliance.Revision,
                true);
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                snapshot.Alliances,
                InsertPending(snapshot.PendingRequests, pending),
                snapshot.Wars,
                actorAlliance.Revision);
        }

        private AlliancePlanningResult PlanWarEndResolution(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds,
            bool accept)
        {
            AlliancePendingRequest pending = snapshot.PendingRequests.SingleOrDefault(row =>
                string.Equals(row.RequestId, request.PendingRequestId, StringComparison.Ordinal));
            if (pending == null || pending.Kind != AlliancePendingKind.WarEnd)
            {
                return Reject(
                    AlliancePlanningStatus.NotFound,
                    "AL-ALLIANCE-PENDING-NOT-FOUND",
                    request.PendingRequestId,
                    "War-end pending request was not found.");
            }

            if (!pending.IsSupported)
            {
                return Reject(
                    AlliancePlanningStatus.Unsupported,
                    "AL-ALLIANCE-PENDING-UNSUPPORTED",
                    pending.RequestId,
                    "Unknown-future war-end request is preserved read-only.");
            }

            AllianceSnapshot opposing = RequireActiveAlliance(snapshot, pending.TargetAllianceId);
            AllianceSnapshot proposer = RequireActiveAlliance(snapshot, pending.AllianceId);
            if (opposing == null || proposer == null)
            {
                return accept
                    ? Indeterminate(pending.TargetAllianceId)
                    : RemovePendingPlan(request, requestFingerprint, snapshot, pending, 0);
            }

            bool actorIsOpposingLeader = IsDerivedLeader(request, opposing, guilds);
            bool actorIsProposerLeader = IsDerivedLeader(request, proposer, guilds);
            if (accept)
            {
                if (!actorIsOpposingLeader)
                {
                    return Unauthorized(request.ActorAccountId);
                }

                if (opposing.Revision != request.ExpectedAllianceRevision)
                {
                    return Reject(
                        AlliancePlanningStatus.StaleAlliance,
                        "AL-ALLIANCE-REVISION-STALE",
                        opposing.AllianceId,
                        "Expected Alliance revision is stale.");
                }

                AllianceWarSnapshot war = snapshot.Wars.SingleOrDefault(row =>
                    string.Equals(row.WarId, request.WarId, StringComparison.Ordinal));
                if (war == null || !WarMatches(war, proposer.AllianceId, opposing.AllianceId))
                {
                    return Reject(
                        AlliancePlanningStatus.NotFound,
                        "AL-ALLIANCE-WAR-NOT-FOUND",
                        request.WarId,
                        "War identity was not found for mutual end.");
                }

                var ended = new AllianceWarSnapshot(
                    war.WarId,
                    war.AttackerAllianceId,
                    war.DefenderAllianceId,
                    AllianceWarState.None,
                    war.DeclaredAtUnixSeconds,
                    war.ActivatedAtUnixSeconds,
                    war.AttackerAllianceRevision,
                    war.DefenderAllianceRevision);
                return CreatePlan(
                    request,
                    requestFingerprint,
                    snapshot,
                    snapshot.Alliances,
                    RemovePending(snapshot.PendingRequests, pending.RequestId),
                    ReplaceWar(snapshot.Wars, ended),
                    opposing.Revision);
            }

            if (!actorIsOpposingLeader && !actorIsProposerLeader)
            {
                return Unauthorized(request.ActorAccountId);
            }

            return RemovePendingPlan(request, requestFingerprint, snapshot, pending, 0);
        }

        private AlliancePlanningResult RemovePendingPlan(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            AlliancePendingRequest pending,
            long allianceRevision)
        {
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                snapshot.Alliances,
                RemovePending(snapshot.PendingRequests, pending.RequestId),
                snapshot.Wars,
                allianceRevision);
        }

        private AlliancePlanningResult CreatePlan(
            AllianceTransitionRequest request,
            string requestFingerprint,
            AllianceAuthoritySnapshot snapshot,
            IReadOnlyList<AllianceSnapshot> alliances,
            IReadOnlyList<AlliancePendingRequest> pendingRequests,
            IReadOnlyList<AllianceWarSnapshot> wars,
            long resultingAllianceRevision)
        {
            long candidateAuthorityRevision = checked(snapshot.Revision + 1);
            string planHash = HashParts(
                "alliance_plan_v1",
                requestFingerprint,
                SnapshotSemanticHash(
                    candidateAuthorityRevision, policy.Binding, alliances, pendingRequests, wars));
            var receipt = new AllianceOperationReceipt(
                request.OperationId,
                request.Operation,
                requestFingerprint,
                request.AllianceId,
                request.ActorAccountId,
                request.ActorGuildId,
                request.TargetGuildId,
                request.TargetAllianceId,
                request.PendingRequestId,
                candidateAuthorityRevision,
                resultingAllianceRevision,
                planHash,
                true);
            IReadOnlyList<AllianceOperationReceipt> receipts = snapshot.Receipts
                .Concat(new[] { receipt })
                .OrderBy(row => row.ResultingAuthorityRevision)
                .ThenBy(row => row.OperationId, StringComparer.Ordinal)
                .ToArray();
            var candidate = new AllianceAuthoritySnapshot(
                AllianceAuthorityStatus.Available,
                candidateAuthorityRevision,
                policy.Binding,
                alliances,
                pendingRequests,
                wars,
                receipts,
                true);
            var plan = new AllianceTransitionPlan(
                request.Operation,
                requestFingerprint,
                snapshot,
                candidate,
                receipt,
                planHash);
            return new AlliancePlanningResult(
                AlliancePlanningStatus.Prepared,
                plan,
                null,
                Array.Empty<AllianceDiagnostic>());
        }

        private AlliancePlanningResult ValidatePolicy(AllianceWarPolicySnapshot candidate)
        {
            if (candidate == null || candidate.Status == AllianceCatalogStatus.Unavailable)
            {
                return Reject(
                    AlliancePlanningStatus.Unavailable,
                    "AL-ALLIANCE-CATALOG-UNAVAILABLE",
                    string.Empty,
                    "Alliance war policy catalog is unavailable.");
            }

            if (candidate.Status == AllianceCatalogStatus.UnsupportedVersion)
            {
                return Reject(
                    AlliancePlanningStatus.Unsupported,
                    "AL-ALLIANCE-CATALOG-UNSUPPORTED",
                    string.Empty,
                    "Alliance war policy catalog version is unsupported.");
            }

            if (candidate.Status == AllianceCatalogStatus.Incomplete)
            {
                return Reject(
                    AlliancePlanningStatus.Unavailable,
                    "AL-ALLIANCE-CATALOG-INCOMPLETE",
                    string.Empty,
                    "Alliance war policy catalog is incomplete.");
            }

            if (candidate.Status != AllianceCatalogStatus.Ready ||
                !candidate.IsComplete ||
                !IsValidBinding(candidate.Binding) ||
                !candidate.SameRealmOnly ||
                candidate.OfficersCanFormAlliancesOrDeclareWar ||
                candidate.WarNoticeHours != 24 ||
                candidate.WarActiveHours != 168 ||
                candidate.ForceHostilityWarState != AllianceWarState.Active ||
                candidate.ImmuneZones == null ||
                candidate.ImmuneZones.Count != 4 ||
                candidate.ImmuneZones.Distinct().Count() != 4 ||
                !candidate.ImmuneZones.OrderBy(row => row).SequenceEqual(new[]
                {
                    AllianceZoneKind.City,
                    AllianceZoneKind.Beginner,
                    AllianceZoneKind.Accordant,
                    AllianceZoneKind.ForcedSafe
                }))
            {
                return Reject(
                    AlliancePlanningStatus.Malformed,
                    "AL-ALLIANCE-CATALOG-MALFORMED",
                    string.Empty,
                    "Alliance war policy is incomplete or contradictory.");
            }

            return null;
        }

        private AlliancePlanningResult ValidateAuthority(AllianceAuthoritySnapshot snapshot)
        {
            if (snapshot == null || snapshot.Status == AllianceAuthorityStatus.Unavailable)
            {
                return Reject(
                    AlliancePlanningStatus.Unavailable,
                    "AL-ALLIANCE-AUTHORITY-UNAVAILABLE",
                    string.Empty,
                    "Alliance authority snapshot is unavailable.");
            }

            if (snapshot.Status == AllianceAuthorityStatus.CommitUncertain)
            {
                return Reject(
                    AlliancePlanningStatus.CommitUncertain,
                    "AL-ALLIANCE-COMMIT-UNCERTAIN",
                    string.Empty,
                    "Alliance authority requires reconciliation before another operation.");
            }

            if (snapshot.Status == AllianceAuthorityStatus.UnsupportedReadOnly)
            {
                return Reject(
                    AlliancePlanningStatus.Unsupported,
                    "AL-ALLIANCE-AUTHORITY-UNSUPPORTED",
                    string.Empty,
                    "Unknown-future Alliance authority is preserved read-only.");
            }

            if (snapshot.Status != AllianceAuthorityStatus.Available ||
                !snapshot.IsComplete ||
                snapshot.Revision < 0 ||
                !BindingEquals(snapshot.CatalogBinding, policy.Binding) ||
                snapshot.Alliances == null ||
                snapshot.PendingRequests == null ||
                snapshot.Wars == null ||
                snapshot.Receipts == null ||
                snapshot.Alliances.Count > MaximumAlliances ||
                snapshot.PendingRequests.Count > MaximumPendingRequests ||
                snapshot.Wars.Count > MaximumWars ||
                snapshot.Receipts.Count > MaximumReceipts ||
                !IsStrictlyOrdered(snapshot.Alliances, row => row?.AllianceId) ||
                !IsStrictlyOrdered(snapshot.PendingRequests, row => row?.RequestId) ||
                !IsStrictlyOrdered(snapshot.Wars, row => row?.WarId))
            {
                return MalformedAuthority();
            }

            var allianceIds = new HashSet<string>(StringComparer.Ordinal);
            var activeGuilds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AllianceSnapshot alliance in snapshot.Alliances)
            {
                if (alliance == null ||
                    !IsStableId(alliance.AllianceId) ||
                    !IsStableId(alliance.ImmutableRealmId) ||
                    !IsSha256(alliance.IdentityHash) ||
                    alliance.Revision <= 0 ||
                    !Enum.IsDefined(typeof(AllianceRelationState), alliance.Relation) ||
                    alliance.MemberGuilds == null ||
                    alliance.MemberGuilds.Count > MaximumMembersPerAlliance ||
                    !allianceIds.Add(alliance.AllianceId) ||
                    !IsStrictlyOrdered(alliance.MemberGuilds, row => row?.GuildId))
                {
                    return MalformedAuthority();
                }

                if (alliance.Relation == AllianceRelationState.Active)
                {
                    if (alliance.MemberGuilds.Count < 2 ||
                        !IsStableId(alliance.LeadGuildId) ||
                        alliance.MemberGuilds.All(row =>
                            !string.Equals(row.GuildId, alliance.LeadGuildId, StringComparison.Ordinal)))
                    {
                        return MalformedAuthority();
                    }
                }

                foreach (AllianceMemberGuildSnapshot member in alliance.MemberGuilds)
                {
                    if (member == null ||
                        !IsStableId(member.GuildId) ||
                        member.GuildRevision <= 0 ||
                        (alliance.Relation == AllianceRelationState.Active &&
                         !activeGuilds.Add(member.GuildId)))
                    {
                        return MalformedAuthority();
                    }
                }
            }

            return null;
        }

        private static AlliancePlanningResult ValidateGuildAuthority(GuildAuthoritySnapshot guilds)
        {
            if (guilds == null || guilds.Status == GuildAuthorityStatus.Unavailable)
            {
                return Reject(
                    AlliancePlanningStatus.Unavailable,
                    "AL-ALLIANCE-GUILD-AUTHORITY-UNAVAILABLE",
                    string.Empty,
                    "Guild authority snapshot is unavailable.");
            }

            if (guilds.Status == GuildAuthorityStatus.CommitUncertain)
            {
                return Reject(
                    AlliancePlanningStatus.CommitUncertain,
                    "AL-ALLIANCE-GUILD-COMMIT-UNCERTAIN",
                    string.Empty,
                    "Guild authority requires reconciliation before an Alliance operation.");
            }

            if (guilds.Status == GuildAuthorityStatus.UnsupportedReadOnly)
            {
                return Reject(
                    AlliancePlanningStatus.Unsupported,
                    "AL-ALLIANCE-GUILD-AUTHORITY-UNSUPPORTED",
                    string.Empty,
                    "Unknown-future Guild authority is preserved read-only.");
            }

            if (guilds.Status != GuildAuthorityStatus.Available ||
                !guilds.IsComplete ||
                guilds.Guilds == null ||
                guilds.PendingRequests == null ||
                guilds.Receipts == null)
            {
                return Reject(
                    AlliancePlanningStatus.Malformed,
                    "AL-ALLIANCE-GUILD-AUTHORITY-MALFORMED",
                    string.Empty,
                    "Guild authority snapshot is incomplete or contradictory.");
            }

            return null;
        }

        private static AlliancePlanningResult DetectMembershipRace(
            AllianceAuthoritySnapshot snapshot,
            GuildAuthoritySnapshot guilds)
        {
            foreach (AllianceSnapshot alliance in snapshot.Alliances)
            {
                if (alliance.Relation != AllianceRelationState.Active)
                {
                    continue;
                }

                foreach (AllianceMemberGuildSnapshot member in alliance.MemberGuilds)
                {
                    GuildSnapshot guild = FindGuild(guilds, member.GuildId);
                    if (guild == null || guild.Status != GuildStatus.Active)
                    {
                        return Indeterminate(member.GuildId);
                    }

                    if (!string.Equals(
                            guild.ImmutableRealmId,
                            alliance.ImmutableRealmId,
                            StringComparison.Ordinal))
                    {
                        return RealmConflict(member.GuildId);
                    }
                }
            }

            return null;
        }

        private AlliancePlanningResult ClassifyReplay(
            AllianceTransitionRequest request,
            string requestFingerprint,
            IReadOnlyList<AllianceOperationReceipt> receipts)
        {
            AllianceOperationReceipt match = receipts.SingleOrDefault(row =>
                string.Equals(row.OperationId, request.OperationId, StringComparison.Ordinal));
            if (match == null)
            {
                return null;
            }

            if (!match.IsSupported)
            {
                return Reject(
                    AlliancePlanningStatus.Unsupported,
                    "AL-ALLIANCE-REPLAY-UNSUPPORTED",
                    match.OperationId,
                    "Operation identity belongs to unknown-future Alliance history.");
            }

            bool exact = match.Operation == request.Operation &&
                         string.Equals(
                             match.RequestFingerprint,
                             requestFingerprint,
                             StringComparison.Ordinal) &&
                         string.Equals(match.AllianceId, request.AllianceId, StringComparison.Ordinal) &&
                         string.Equals(
                             match.ActorAccountId,
                             request.ActorAccountId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             match.ActorGuildId,
                             request.ActorGuildId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             match.TargetGuildId,
                             request.TargetGuildId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             match.TargetAllianceId,
                             request.TargetAllianceId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             match.PendingRequestId,
                             request.PendingRequestId,
                             StringComparison.Ordinal);
            return exact
                ? new AlliancePlanningResult(
                    AlliancePlanningStatus.AlreadyCommitted,
                    null,
                    match,
                    new[]
                    {
                        new AllianceDiagnostic(
                            "AL-ALLIANCE-REPLAY",
                            match.OperationId,
                            "Committed Alliance receipt already satisfies this operation.")
                    })
                : Reject(
                    AlliancePlanningStatus.Conflict,
                    "AL-ALLIANCE-OPERATION-CONFLICT",
                    match.OperationId,
                    "Operation identity is already bound to different Alliance semantics.");
        }

        private AlliancePlanningResult EnsurePendingAvailable(
            AllianceAuthoritySnapshot snapshot,
            string pendingRequestId,
            string actorGuildId,
            string targetGuildId)
        {
            if (snapshot.PendingRequests.Count >= MaximumPendingRequests)
            {
                return Reject(
                    AlliancePlanningStatus.Malformed,
                    "AL-ALLIANCE-PENDING-CAPACITY",
                    pendingRequestId,
                    "Alliance pending set cannot safely accept another row.");
            }

            AlliancePendingRequest collision = snapshot.PendingRequests.FirstOrDefault(row =>
                string.Equals(row.RequestId, pendingRequestId, StringComparison.Ordinal) ||
                string.Equals(row.ProposerGuildId, actorGuildId, StringComparison.Ordinal) ||
                string.Equals(row.TargetGuildId, actorGuildId, StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(targetGuildId) &&
                 (string.Equals(row.ProposerGuildId, targetGuildId, StringComparison.Ordinal) ||
                  string.Equals(row.TargetGuildId, targetGuildId, StringComparison.Ordinal))));
            if (collision == null)
            {
                return null;
            }

            return Reject(
                collision.IsSupported ? AlliancePlanningStatus.Conflict : AlliancePlanningStatus.Unsupported,
                "AL-ALLIANCE-PENDING-CONFLICT",
                collision.RequestId,
                "Pending identity or Guild is already reserved.");
        }

        private AlliancePlanningResult RequireGuildMaster(
            GuildSnapshot guild,
            string accountId,
            string actorRealmId)
        {
            GuildMemberSnapshot member = guild.Members.SingleOrDefault(row =>
                row.State == GuildMembershipState.Active &&
                string.Equals(row.AccountId, accountId, StringComparison.Ordinal));
            if (member == null)
            {
                return Unauthorized(accountId);
            }

            if (!string.Equals(member.ImmutableRealmId, actorRealmId, StringComparison.Ordinal) ||
                !string.Equals(guild.ImmutableRealmId, actorRealmId, StringComparison.Ordinal))
            {
                return RealmConflict(accountId);
            }

            if (member.Role != GuildRole.Master)
            {
                return Unauthorized(accountId);
            }

            return null;
        }

        private AlliancePlanningResult RequireDerivedLeader(
            AllianceTransitionRequest request,
            AllianceSnapshot alliance,
            GuildAuthoritySnapshot guilds)
        {
            GuildSnapshot leadGuild = FindGuild(guilds, alliance.LeadGuildId);
            if (leadGuild == null || leadGuild.Status != GuildStatus.Active)
            {
                return Indeterminate(alliance.LeadGuildId);
            }

            if (!string.Equals(
                    request.ActorGuildId, alliance.LeadGuildId, StringComparison.Ordinal))
            {
                return Unauthorized(request.ActorAccountId);
            }

            AlliancePlanningResult masterGate = RequireGuildMaster(
                leadGuild, request.ActorAccountId, request.ActorImmutableRealmId);
            if (masterGate != null)
            {
                return masterGate;
            }

            if (leadGuild.Revision != request.ExpectedActorGuildRevision &&
                request.Operation != AllianceOperation.DeclineWarEnd)
            {
                return StaleGuild(request.ActorGuildId);
            }

            return null;
        }

        private static bool IsDerivedLeader(
            AllianceTransitionRequest request,
            AllianceSnapshot alliance,
            GuildAuthoritySnapshot guilds)
        {
            GuildSnapshot leadGuild = FindGuild(guilds, alliance.LeadGuildId);
            return leadGuild != null &&
                   leadGuild.Status == GuildStatus.Active &&
                   string.Equals(
                       request.ActorGuildId, alliance.LeadGuildId, StringComparison.Ordinal) &&
                   IsActiveMaster(leadGuild, request.ActorAccountId);
        }

        private static AlliancePlanningResult RequireAllianceRevision(
            AllianceTransitionRequest request,
            AllianceSnapshot alliance)
        {
            if (alliance.Revision != request.ExpectedAllianceRevision)
            {
                return Reject(
                    AlliancePlanningStatus.StaleAlliance,
                    "AL-ALLIANCE-REVISION-STALE",
                    alliance.AllianceId,
                    "Expected Alliance revision is stale.");
            }

            if (alliance.Revision == long.MaxValue)
            {
                return Reject(
                    AlliancePlanningStatus.Overflow,
                    "AL-ALLIANCE-REVISION-OVERFLOW",
                    alliance.AllianceId,
                    "Alliance revision cannot advance.");
            }

            return null;
        }

        private AllianceWarState EffectiveWarState(AllianceWarSnapshot war, long clockUnixSeconds)
        {
            if (war == null || war.CommittedState == AllianceWarState.None)
            {
                return AllianceWarState.None;
            }

            if (war.CommittedState == AllianceWarState.Cooling ||
                war.CommittedState == AllianceWarState.ReconciliationPending)
            {
                return war.CommittedState;
            }

            long noticeEnd = checked(war.DeclaredAtUnixSeconds + (policy.WarNoticeHours * SecondsPerHour));
            long activeEnd = checked(noticeEnd + (policy.WarActiveHours * SecondsPerHour));
            if (clockUnixSeconds < noticeEnd)
            {
                return AllianceWarState.Declared;
            }

            if (clockUnixSeconds < activeEnd)
            {
                return AllianceWarState.Active;
            }

            return AllianceWarState.Cooling;
        }

        private static bool AlliancesShareRealm(
            AllianceSnapshot left,
            AllianceSnapshot right,
            GuildAuthoritySnapshot guilds)
        {
            if (!string.Equals(
                    left.ImmutableRealmId, right.ImmutableRealmId, StringComparison.Ordinal))
            {
                return false;
            }

            foreach (AllianceMemberGuildSnapshot member in left.MemberGuilds.Concat(right.MemberGuilds))
            {
                GuildSnapshot guild = FindGuild(guilds, member.GuildId);
                if (guild == null ||
                    !string.Equals(
                        guild.ImmutableRealmId, left.ImmutableRealmId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static AllianceSnapshot RequireActiveAlliance(
            AllianceAuthoritySnapshot snapshot,
            string allianceId)
        {
            return snapshot.Alliances.SingleOrDefault(row =>
                string.Equals(row.AllianceId, allianceId, StringComparison.Ordinal) &&
                row.Relation == AllianceRelationState.Active);
        }

        private static AllianceSnapshot FindActiveAllianceForGuild(
            AllianceAuthoritySnapshot snapshot,
            string guildId)
        {
            return snapshot.Alliances.SingleOrDefault(row =>
                row.Relation == AllianceRelationState.Active &&
                row.MemberGuilds.Any(member =>
                    string.Equals(member.GuildId, guildId, StringComparison.Ordinal)));
        }

        private static AllianceWarSnapshot FindWarBetween(
            AllianceAuthoritySnapshot snapshot,
            string leftAllianceId,
            string rightAllianceId)
        {
            if (string.IsNullOrEmpty(leftAllianceId) || string.IsNullOrEmpty(rightAllianceId))
            {
                return null;
            }

            return snapshot.Wars.SingleOrDefault(row =>
                WarMatches(row, leftAllianceId, rightAllianceId) &&
                row.CommittedState != AllianceWarState.None);
        }

        private static bool WarMatches(
            AllianceWarSnapshot war,
            string leftAllianceId,
            string rightAllianceId)
        {
            return (string.Equals(war.AttackerAllianceId, leftAllianceId, StringComparison.Ordinal) &&
                    string.Equals(war.DefenderAllianceId, rightAllianceId, StringComparison.Ordinal)) ||
                   (string.Equals(war.AttackerAllianceId, rightAllianceId, StringComparison.Ordinal) &&
                    string.Equals(war.DefenderAllianceId, leftAllianceId, StringComparison.Ordinal));
        }

        private static GuildSnapshot FindGuild(GuildAuthoritySnapshot guilds, string guildId)
        {
            return guilds.Guilds.SingleOrDefault(row =>
                string.Equals(row.GuildId, guildId, StringComparison.Ordinal));
        }

        private static bool IsActiveMaster(GuildSnapshot guild, string accountId)
        {
            return guild.Members.Any(row =>
                row.State == GuildMembershipState.Active &&
                row.Role == GuildRole.Master &&
                string.Equals(row.AccountId, accountId, StringComparison.Ordinal));
        }

        private static string DeriveLeadGuildId(
            IReadOnlyList<AllianceMemberGuildSnapshot> remaining,
            string leavingGuildId,
            string currentLeadGuildId)
        {
            if (!string.Equals(currentLeadGuildId, leavingGuildId, StringComparison.Ordinal) &&
                remaining.Any(row =>
                    string.Equals(row.GuildId, currentLeadGuildId, StringComparison.Ordinal)))
            {
                return currentLeadGuildId;
            }

            return remaining.Select(row => row.GuildId).OrderBy(row => row, StringComparer.Ordinal)
                .FirstOrDefault() ?? string.Empty;
        }

        private static IReadOnlyList<AllianceSnapshot> InsertAlliance(
            IReadOnlyList<AllianceSnapshot> alliances,
            AllianceSnapshot candidate)
        {
            return alliances.Concat(new[] { candidate })
                .OrderBy(row => row.AllianceId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<AllianceSnapshot> ReplaceAlliance(
            IReadOnlyList<AllianceSnapshot> alliances,
            AllianceSnapshot candidate)
        {
            return alliances.Select(row =>
                    string.Equals(row.AllianceId, candidate.AllianceId, StringComparison.Ordinal)
                        ? candidate
                        : row)
                .OrderBy(row => row.AllianceId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<AlliancePendingRequest> InsertPending(
            IReadOnlyList<AlliancePendingRequest> pending,
            AlliancePendingRequest candidate)
        {
            return pending.Concat(new[] { candidate })
                .OrderBy(row => row.RequestId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<AlliancePendingRequest> RemovePending(
            IReadOnlyList<AlliancePendingRequest> pending,
            string requestId)
        {
            return pending.Where(row =>
                    !string.Equals(row.RequestId, requestId, StringComparison.Ordinal))
                .ToArray();
        }

        private static IReadOnlyList<AlliancePendingRequest> RemovePendingForAlliance(
            IReadOnlyList<AlliancePendingRequest> pending,
            string allianceId)
        {
            return pending.Where(row =>
                    !string.Equals(row.AllianceId, allianceId, StringComparison.Ordinal) &&
                    !string.Equals(row.TargetAllianceId, allianceId, StringComparison.Ordinal))
                .ToArray();
        }

        private static IReadOnlyList<AllianceWarSnapshot> InsertWar(
            IReadOnlyList<AllianceWarSnapshot> wars,
            AllianceWarSnapshot candidate)
        {
            return wars.Concat(new[] { candidate })
                .OrderBy(row => row.WarId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<AllianceWarSnapshot> ReplaceWar(
            IReadOnlyList<AllianceWarSnapshot> wars,
            AllianceWarSnapshot candidate)
        {
            return wars.Select(row =>
                    string.Equals(row.WarId, candidate.WarId, StringComparison.Ordinal)
                        ? candidate
                        : row)
                .OrderBy(row => row.WarId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<AllianceWarSnapshot> EndWarsForAlliance(
            IReadOnlyList<AllianceWarSnapshot> wars,
            string allianceId)
        {
            return wars.Select(row =>
                    string.Equals(row.AttackerAllianceId, allianceId, StringComparison.Ordinal) ||
                    string.Equals(row.DefenderAllianceId, allianceId, StringComparison.Ordinal)
                        ? new AllianceWarSnapshot(
                            row.WarId,
                            row.AttackerAllianceId,
                            row.DefenderAllianceId,
                            AllianceWarState.None,
                            row.DeclaredAtUnixSeconds,
                            row.ActivatedAtUnixSeconds,
                            row.AttackerAllianceRevision,
                            row.DefenderAllianceRevision)
                        : row)
                .OrderBy(row => row.WarId, StringComparer.Ordinal)
                .ToArray();
        }

        private static AllianceSnapshot CopyAlliance(
            AllianceSnapshot source,
            long revision,
            AllianceRelationState relation,
            string leadGuildId,
            IReadOnlyList<AllianceMemberGuildSnapshot> members)
        {
            return new AllianceSnapshot(
                source.AllianceId,
                source.ImmutableRealmId,
                source.IdentityHash,
                revision,
                relation,
                leadGuildId,
                members);
        }

        private static string SnapshotSemanticHash(
            long revision,
            GuildCatalogBinding binding,
            IReadOnlyList<AllianceSnapshot> alliances,
            IReadOnlyList<AlliancePendingRequest> pending,
            IReadOnlyList<AllianceWarSnapshot> wars)
        {
            IEnumerable<string> allianceParts = alliances.SelectMany(alliance =>
                new[]
                {
                    alliance.AllianceId,
                    alliance.ImmutableRealmId,
                    alliance.IdentityHash,
                    alliance.Revision.ToString(CultureInfo.InvariantCulture),
                    ((int)alliance.Relation).ToString(CultureInfo.InvariantCulture),
                    alliance.LeadGuildId
                }.Concat(alliance.MemberGuilds.SelectMany(member => new[]
                {
                    member.GuildId,
                    member.GuildRevision.ToString(CultureInfo.InvariantCulture)
                })));
            IEnumerable<string> pendingParts = pending.SelectMany(row => new[]
            {
                row.RequestId,
                ((int)row.Kind).ToString(CultureInfo.InvariantCulture),
                row.AllianceId,
                row.ProposerGuildId,
                row.TargetGuildId,
                row.TargetAllianceId,
                row.ActorAccountId,
                row.AllianceRevision.ToString(CultureInfo.InvariantCulture),
                row.ProposerGuildRevision.ToString(CultureInfo.InvariantCulture),
                row.TargetGuildRevision.ToString(CultureInfo.InvariantCulture),
                row.IsSupported ? "1" : "0"
            });
            IEnumerable<string> warParts = wars.SelectMany(row => new[]
            {
                row.WarId,
                row.AttackerAllianceId,
                row.DefenderAllianceId,
                ((int)row.CommittedState).ToString(CultureInfo.InvariantCulture),
                row.DeclaredAtUnixSeconds.ToString(CultureInfo.InvariantCulture),
                row.ActivatedAtUnixSeconds.ToString(CultureInfo.InvariantCulture),
                row.AttackerAllianceRevision.ToString(CultureInfo.InvariantCulture),
                row.DefenderAllianceRevision.ToString(CultureInfo.InvariantCulture)
            });
            return HashParts(
                new[]
                {
                    "alliance_authority_snapshot_v1",
                    revision.ToString(CultureInfo.InvariantCulture),
                    BindingHash(binding),
                    "<alliances>"
                }
                .Concat(allianceParts)
                .Concat(new[] { "<pending>" })
                .Concat(pendingParts)
                .Concat(new[] { "<wars>" })
                .Concat(warParts)
                .ToArray());
        }

        private static string RequestFingerprint(AllianceTransitionRequest request)
        {
            return HashParts(
                "alliance_request_v1",
                ((int)request.Operation).ToString(CultureInfo.InvariantCulture),
                request.OperationId,
                request.ActorAccountId,
                request.ActorImmutableRealmId,
                request.ActorGuildId,
                request.AllianceId,
                request.TargetGuildId,
                request.TargetAllianceId,
                request.PendingRequestId,
                request.WarId,
                request.ExpectedAuthorityRevision.ToString(CultureInfo.InvariantCulture),
                request.ExpectedAllianceRevision.ToString(CultureInfo.InvariantCulture),
                request.ExpectedActorGuildRevision.ToString(CultureInfo.InvariantCulture),
                request.ExpectedTargetGuildRevision.ToString(CultureInfo.InvariantCulture),
                request.ClockUnixSeconds.ToString(CultureInfo.InvariantCulture),
                BindingHash(request.ExpectedCatalogBinding));
        }

        private static string BindingHash(GuildCatalogBinding binding)
        {
            if (binding == null)
            {
                return string.Empty;
            }

            return HashParts(
                "alliance_catalog_binding_v1",
                binding.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                binding.ContentVersion,
                binding.SourceRevision,
                binding.CatalogHash);
        }

        private static bool IsValidRequest(AllianceTransitionRequest request)
        {
            if (request == null ||
                !Enum.IsDefined(typeof(AllianceOperation), request.Operation) ||
                !IsOpaqueId(request.OperationId) ||
                !IsOpaqueId(request.ActorAccountId) ||
                !IsStableId(request.ActorImmutableRealmId) ||
                !IsStableId(request.ActorGuildId) ||
                !IsStableId(request.AllianceId) ||
                request.ExpectedAuthorityRevision < 0 ||
                request.ExpectedAllianceRevision < 0 ||
                request.ExpectedActorGuildRevision < 0 ||
                request.ExpectedTargetGuildRevision < 0 ||
                request.ClockUnixSeconds < 0 ||
                !IsValidBinding(request.ExpectedCatalogBinding))
            {
                return false;
            }

            switch (request.Operation)
            {
                case AllianceOperation.Propose:
                    return request.ExpectedAllianceRevision == 0 &&
                           IsStableId(request.TargetGuildId) &&
                           string.IsNullOrEmpty(request.TargetAllianceId) &&
                           IsOpaqueId(request.PendingRequestId) &&
                           string.IsNullOrEmpty(request.WarId) &&
                           request.ExpectedActorGuildRevision > 0 &&
                           request.ExpectedTargetGuildRevision > 0 &&
                           !string.Equals(
                               request.ActorGuildId, request.TargetGuildId, StringComparison.Ordinal);
                case AllianceOperation.Accept:
                    return string.IsNullOrEmpty(request.TargetGuildId) &&
                           string.IsNullOrEmpty(request.TargetAllianceId) &&
                           IsOpaqueId(request.PendingRequestId) &&
                           string.IsNullOrEmpty(request.WarId);
                case AllianceOperation.Decline:
                    return string.IsNullOrEmpty(request.TargetGuildId) &&
                           string.IsNullOrEmpty(request.TargetAllianceId) &&
                           IsOpaqueId(request.PendingRequestId) &&
                           string.IsNullOrEmpty(request.WarId);
                case AllianceOperation.Leave:
                case AllianceOperation.Disband:
                    return string.IsNullOrEmpty(request.TargetGuildId) &&
                           string.IsNullOrEmpty(request.TargetAllianceId) &&
                           string.IsNullOrEmpty(request.PendingRequestId) &&
                           string.IsNullOrEmpty(request.WarId);
                case AllianceOperation.DeclareWar:
                    return string.IsNullOrEmpty(request.TargetGuildId) &&
                           IsStableId(request.TargetAllianceId) &&
                           string.IsNullOrEmpty(request.PendingRequestId) &&
                           IsOpaqueId(request.WarId);
                case AllianceOperation.ProposeWarEnd:
                    return string.IsNullOrEmpty(request.TargetGuildId) &&
                           IsStableId(request.TargetAllianceId) &&
                           IsOpaqueId(request.PendingRequestId) &&
                           IsOpaqueId(request.WarId);
                case AllianceOperation.AcceptWarEnd:
                case AllianceOperation.DeclineWarEnd:
                    return string.IsNullOrEmpty(request.TargetGuildId) &&
                           IsStableId(request.TargetAllianceId) &&
                           IsOpaqueId(request.PendingRequestId) &&
                           IsOpaqueId(request.WarId);
                default:
                    return false;
            }
        }

        private static bool IsPendingResolution(AllianceOperation operation)
        {
            return operation == AllianceOperation.Accept ||
                   operation == AllianceOperation.Decline ||
                   operation == AllianceOperation.AcceptWarEnd ||
                   operation == AllianceOperation.DeclineWarEnd;
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

        private static bool IsStrictlyOrdered<T>(
            IReadOnlyList<T> values,
            Func<T, string> selector)
        {
            string previous = null;
            foreach (T value in values)
            {
                string current = selector(value);
                if (current == null ||
                    (previous != null && string.CompareOrdinal(previous, current) >= 0))
                {
                    return false;
                }

                previous = current;
            }

            return true;
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

        private static AlliancePlanningResult Unauthorized(string accountId)
        {
            return Reject(
                AlliancePlanningStatus.Unauthorized,
                "AL-ALLIANCE-ACTOR-UNAUTHORIZED",
                accountId,
                "Actor role does not authorize this Alliance transition.");
        }

        private static AlliancePlanningResult RealmConflict(string subjectId)
        {
            return Reject(
                AlliancePlanningStatus.Conflict,
                "AL-ALLIANCE-REALM-CONFLICT",
                subjectId,
                "Cross-realm Alliance or war is rejected.");
        }

        private static AlliancePlanningResult StaleGuild(string guildId)
        {
            return Reject(
                AlliancePlanningStatus.StaleGuild,
                "AL-ALLIANCE-GUILD-REVISION-STALE",
                guildId,
                "Expected Guild revision is stale.");
        }

        private static AlliancePlanningResult Indeterminate(string subjectId)
        {
            return Reject(
                AlliancePlanningStatus.Indeterminate,
                "AL-ALLIANCE-INDETERMINATE",
                subjectId,
                "Alliance and Guild snapshots cannot be reconciled.");
        }

        private static AlliancePlanningResult MalformedAuthority()
        {
            return Reject(
                AlliancePlanningStatus.Malformed,
                "AL-ALLIANCE-AUTHORITY-MALFORMED",
                string.Empty,
                "Alliance authority snapshot is incomplete or contradictory.");
        }

        private static AlliancePlanningResult Reject(
            AlliancePlanningStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new AlliancePlanningResult(
                status,
                null,
                null,
                new[] { new AllianceDiagnostic(code, subjectId, message) });
        }
    }
}
