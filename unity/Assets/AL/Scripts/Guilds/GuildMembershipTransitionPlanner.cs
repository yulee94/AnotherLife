using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Guilds
{
    public sealed class GuildMembershipTransitionPlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const int MaximumGuilds = 256;
        private const int MaximumMembersPerGuild = 512;
        private const int MaximumPendingRequests = 2048;
        private const int MaximumReceipts = 4096;

        private readonly GuildMembershipPolicySnapshot policy;

        public GuildMembershipTransitionPlanner(GuildMembershipPolicySnapshot policy)
        {
            this.policy = policy;
        }

        public GuildPlanningResult Plan(
            GuildTransitionRequest request,
            GuildAuthoritySnapshot snapshot)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    GuildPlanningStatus.InvalidRequest,
                    "AL-GUILD-REQUEST-INVALID",
                    request?.OperationId,
                    "Guild transition identity, fields, or revisions are invalid.");
            }

            GuildPlanningResult policyGate = ValidatePolicy(policy);
            if (policyGate != null)
            {
                return policyGate;
            }

            if (!BindingEquals(request.ExpectedCatalogBinding, policy.Binding))
            {
                return Reject(
                    GuildPlanningStatus.StaleCatalog,
                    "AL-GUILD-CATALOG-STALE",
                    request.OperationId,
                    "The request is not fenced to the accepted Guild policy catalog.");
            }

            GuildPlanningResult authorityGate = ValidateAuthority(snapshot);
            if (authorityGate != null)
            {
                return authorityGate;
            }

            string requestFingerprint = RequestFingerprint(request);
            GuildPlanningResult replay = ClassifyReplay(
                request,
                requestFingerprint,
                snapshot.Receipts);
            if (replay != null)
            {
                return replay;
            }

            if (snapshot.Revision != request.ExpectedAuthorityRevision)
            {
                return Reject(
                    GuildPlanningStatus.StaleAuthority,
                    "AL-GUILD-AUTHORITY-STALE",
                    request.OperationId,
                    "Expected Guild authority revision is stale.");
            }

            if (snapshot.Revision == long.MaxValue)
            {
                return Reject(
                    GuildPlanningStatus.Overflow,
                    "AL-GUILD-AUTHORITY-REVISION-OVERFLOW",
                    request.OperationId,
                    "Guild authority revision cannot advance.");
            }

            if (snapshot.Receipts.Count >= MaximumReceipts)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-RECEIPT-CAPACITY",
                    request.OperationId,
                    "Guild receipt history cannot safely accept another row.");
            }

            try
            {
                if (request.Operation == GuildOperation.Create)
                {
                    return PlanCreate(request, requestFingerprint, snapshot);
                }

                GuildSnapshot guild = snapshot.Guilds.SingleOrDefault(row =>
                    string.Equals(row.GuildId, request.GuildId, StringComparison.Ordinal));
                if (guild == null || guild.Status != GuildStatus.Active)
                {
                    return Reject(
                        GuildPlanningStatus.NotFound,
                        "AL-GUILD-ACTIVE-GUILD-NOT-FOUND",
                        request.GuildId,
                        "The requested active Guild does not exist.");
                }

                if (guild.Revision != request.ExpectedGuildRevision)
                {
                    return Reject(
                        GuildPlanningStatus.StaleGuild,
                        "AL-GUILD-REVISION-STALE",
                        request.GuildId,
                        "Expected Guild revision is stale.");
                }

                if (guild.Revision == long.MaxValue)
                {
                    return Reject(
                        GuildPlanningStatus.Overflow,
                        "AL-GUILD-REVISION-OVERFLOW",
                        request.GuildId,
                        "Guild revision cannot advance.");
                }

                switch (request.Operation)
                {
                    case GuildOperation.Join:
                        return PlanJoin(request, requestFingerprint, snapshot, guild);
                    case GuildOperation.Invite:
                        return PlanInvite(request, requestFingerprint, snapshot, guild);
                    case GuildOperation.Accept:
                        return PlanPendingResolution(
                            request, requestFingerprint, snapshot, guild, true);
                    case GuildOperation.Decline:
                        return PlanPendingResolution(
                            request, requestFingerprint, snapshot, guild, false);
                    case GuildOperation.Leave:
                        return PlanLeave(request, requestFingerprint, snapshot, guild);
                    case GuildOperation.Kick:
                        return PlanKick(request, requestFingerprint, snapshot, guild);
                    case GuildOperation.Promote:
                        return PlanRoleChange(
                            request, requestFingerprint, snapshot, guild,
                            GuildRole.Member, GuildRole.Officer, true);
                    case GuildOperation.Demote:
                        return PlanRoleChange(
                            request, requestFingerprint, snapshot, guild,
                            GuildRole.Officer, GuildRole.Member, false);
                    case GuildOperation.MasterTransfer:
                        return PlanMasterTransfer(
                            request, requestFingerprint, snapshot, guild);
                    case GuildOperation.Disband:
                        return PlanDisband(request, requestFingerprint, snapshot, guild);
                    default:
                        return Reject(
                            GuildPlanningStatus.InvalidRequest,
                            "AL-GUILD-OPERATION-INVALID",
                            request.OperationId,
                            "Guild operation is invalid.");
                }
            }
            catch (OverflowException)
            {
                return Reject(
                    GuildPlanningStatus.Overflow,
                    "AL-GUILD-ARITHMETIC-OVERFLOW",
                    request.OperationId,
                    "Guild candidate arithmetic overflowed.");
            }
        }

        private GuildPlanningResult PlanCreate(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot)
        {
            if (snapshot.Guilds.Any(row =>
                    string.Equals(row.GuildId, request.GuildId, StringComparison.Ordinal)))
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-GUILD-ID-CONFLICT",
                    request.GuildId,
                    "Guild identity is already reserved.");
            }

            if (snapshot.Guilds.Count >= MaximumGuilds)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-CAPACITY",
                    request.GuildId,
                    "Guild authority cannot safely accept another Guild.");
            }

            if (FindReservingMembership(snapshot, request.ActorAccountId) != null)
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-GUILD-ACCOUNT-ALREADY-MEMBER",
                    request.ActorAccountId,
                    "Account already has one reserving Guild membership.");
            }

            var guild = new GuildSnapshot(
                request.GuildId,
                request.ActorImmutableRealmId,
                1,
                GuildStatus.Active,
                new[]
                {
                    new GuildMemberSnapshot(
                        request.ActorAccountId,
                        request.ActorImmutableRealmId,
                        GuildRole.Master,
                        GuildMembershipState.Active)
                });
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                InsertGuild(snapshot.Guilds, guild),
                snapshot.PendingRequests,
                guild.Revision);
        }

        private GuildPlanningResult PlanJoin(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild)
        {
            if (!string.Equals(
                    request.ActorImmutableRealmId,
                    guild.ImmutableRealmId,
                    StringComparison.Ordinal))
            {
                return RealmConflict(request.ActorAccountId);
            }

            GuildPlanningResult membershipGate = EnsureAccountCanEnter(
                snapshot, guild, request.ActorAccountId, request.PendingRequestId);
            if (membershipGate != null)
            {
                return membershipGate;
            }

            return AddPending(
                request,
                requestFingerprint,
                snapshot,
                guild,
                GuildPendingRequestKind.JoinApplication,
                request.ActorAccountId,
                request.ActorImmutableRealmId);
        }

        private GuildPlanningResult PlanInvite(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild)
        {
            GuildMemberSnapshot actor = FindAuthoritativeMember(guild, request.ActorAccountId);
            if (!Can(actor, value => value.CanManageInvitations))
            {
                return Unauthorized(request.ActorAccountId);
            }

            if (!string.Equals(
                    request.TargetImmutableRealmId,
                    guild.ImmutableRealmId,
                    StringComparison.Ordinal))
            {
                return RealmConflict(request.TargetAccountId);
            }

            GuildPlanningResult membershipGate = EnsureAccountCanEnter(
                snapshot, guild, request.TargetAccountId, request.PendingRequestId);
            if (membershipGate != null)
            {
                return membershipGate;
            }

            return AddPending(
                request,
                requestFingerprint,
                snapshot,
                guild,
                GuildPendingRequestKind.Invitation,
                request.TargetAccountId,
                request.TargetImmutableRealmId);
        }

        private GuildPlanningResult AddPending(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild,
            GuildPendingRequestKind kind,
            string accountId,
            string realmId)
        {
            if (snapshot.PendingRequests.Count >= MaximumPendingRequests)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-PENDING-CAPACITY",
                    request.PendingRequestId,
                    "Guild pending authority cannot safely accept another row.");
            }

            GuildPendingRequest collision = snapshot.PendingRequests.FirstOrDefault(row =>
                string.Equals(row.RequestId, request.PendingRequestId, StringComparison.Ordinal) ||
                string.Equals(row.AccountId, accountId, StringComparison.Ordinal));
            if (collision != null)
            {
                return Reject(
                    collision.IsSupported
                        ? GuildPlanningStatus.Conflict
                        : GuildPlanningStatus.Unsupported,
                    "AL-GUILD-PENDING-CONFLICT",
                    collision.RequestId,
                    "Pending identity or account is already reserved.");
            }

            long candidateGuildRevision = checked(guild.Revision + 1);
            var pending = new GuildPendingRequest(
                request.PendingRequestId,
                kind,
                guild.GuildId,
                accountId,
                realmId,
                candidateGuildRevision,
                true);
            GuildSnapshot candidateGuild = CopyGuild(guild, candidateGuildRevision);
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceGuild(snapshot.Guilds, candidateGuild),
                InsertPending(snapshot.PendingRequests, pending),
                candidateGuildRevision);
        }

        private GuildPlanningResult PlanPendingResolution(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild,
            bool accept)
        {
            GuildPendingRequest pending = snapshot.PendingRequests.SingleOrDefault(row =>
                string.Equals(row.RequestId, request.PendingRequestId, StringComparison.Ordinal));
            if (pending == null ||
                !string.Equals(pending.GuildId, guild.GuildId, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-GUILD-PENDING-NOT-FOUND",
                    request.PendingRequestId,
                    "Pending Guild request does not exist for this Guild.");
            }

            if (!pending.IsSupported)
            {
                return Reject(
                    GuildPlanningStatus.Unsupported,
                    "AL-GUILD-PENDING-UNSUPPORTED",
                    pending.RequestId,
                    "Unknown-future pending evidence excludes mutation.");
            }

            if (accept && pending.GuildRevision != guild.Revision)
            {
                return Reject(
                    GuildPlanningStatus.StaleGuild,
                    "AL-GUILD-PENDING-STALE",
                    pending.RequestId,
                    "Membership changed after the pending request was issued.");
            }

            bool isSubject = string.Equals(
                request.ActorAccountId,
                pending.AccountId,
                StringComparison.Ordinal);
            bool isInvitationManager = Can(
                FindAuthoritativeMember(guild, request.ActorAccountId),
                value => value.CanManageInvitations);
            if (pending.Kind == GuildPendingRequestKind.Invitation)
            {
                if (accept ? !isSubject : (!isSubject && !isInvitationManager))
                {
                    return Unauthorized(request.ActorAccountId);
                }
            }
            else if (pending.Kind == GuildPendingRequestKind.JoinApplication)
            {
                if (!isInvitationManager)
                {
                    return Unauthorized(request.ActorAccountId);
                }
            }
            else
            {
                return Reject(
                    GuildPlanningStatus.Unsupported,
                    "AL-GUILD-PENDING-KIND-UNSUPPORTED",
                    pending.RequestId,
                    "Unknown pending kind excludes mutation.");
            }

            long candidateGuildRevision = checked(guild.Revision + 1);
            IReadOnlyList<GuildMemberSnapshot> members = guild.Members;
            if (accept)
            {
                GuildMemberSnapshot existing = FindMember(guild, pending.AccountId);
                if (existing != null &&
                    StatePolicyFor(existing.State).BlocksSameGuildEntry)
                {
                    return Reject(
                        GuildPlanningStatus.Conflict,
                        "AL-GUILD-ACCOUNT-CYCLE-BLOCKED",
                        pending.AccountId,
                        "Account membership state blocks entry into this Guild cycle.");
                }

                if (FindReservingMembership(snapshot, pending.AccountId) != null)
                {
                    return Reject(
                        GuildPlanningStatus.Conflict,
                        "AL-GUILD-ACCOUNT-ALREADY-MEMBER",
                        pending.AccountId,
                        "Account already has one reserving Guild membership.");
                }

                if (existing == null && members.Count >= MaximumMembersPerGuild)
                {
                    return Reject(
                        GuildPlanningStatus.Malformed,
                        "AL-GUILD-MEMBER-CAPACITY",
                        guild.GuildId,
                        "Guild cannot safely accept another member.");
                }

                var joined = new GuildMemberSnapshot(
                    pending.AccountId,
                    pending.ImmutableRealmId,
                    policy.DefaultJoinedRole,
                    GuildMembershipState.Active);
                members = existing == null
                    ? InsertMember(members, joined)
                    : ReplaceMember(members, existing, joined);
            }

            GuildSnapshot candidateGuild = CopyGuild(
                guild,
                candidateGuildRevision,
                members: members);
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceGuild(snapshot.Guilds, candidateGuild),
                snapshot.PendingRequests.Where(row => !ReferenceEquals(row, pending)).ToArray(),
                candidateGuildRevision);
        }

        private GuildPlanningResult PlanLeave(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild)
        {
            GuildMemberSnapshot actor = FindMember(guild, request.ActorAccountId);
            GuildMembershipState? leaveResult = actor == null
                ? null
                : StatePolicyFor(actor.State).LeaveResult;
            if (actor == null || !leaveResult.HasValue)
            {
                return Unauthorized(request.ActorAccountId);
            }

            if (actor.State == GuildMembershipState.Active && actor.Role == GuildRole.Master)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-GUILD-MASTER-MUST-TRANSFER-OR-DISBAND",
                    actor.AccountId,
                    "The active Master cannot leave without transfer or disband.");
            }

            long candidateGuildRevision = checked(guild.Revision + 1);
            GuildSnapshot candidateGuild = CopyGuild(
                guild,
                candidateGuildRevision,
                members: ReplaceMember(
                    guild.Members,
                    actor,
                    CopyMemberState(actor, leaveResult.Value)));
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceGuild(snapshot.Guilds, candidateGuild),
                RemovePendingForAccount(snapshot.PendingRequests, actor.AccountId),
                candidateGuildRevision);
        }

        private GuildPlanningResult PlanKick(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild)
        {
            GuildMemberSnapshot actor = FindAuthoritativeMember(guild, request.ActorAccountId);
            GuildMemberSnapshot target = FindMember(guild, request.TargetAccountId);
            GuildMembershipState? kickResult = target == null
                ? null
                : StatePolicyFor(target.State).KickResult;
            if (actor == null || !Can(actor, value => value.CanManageMembers))
            {
                return Unauthorized(request.ActorAccountId);
            }

            if (target == null || !kickResult.HasValue)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-GUILD-MEMBER-NOT-FOUND",
                    request.TargetAccountId,
                    "Kick target is not an active Guild member.");
            }

            if (target.Role == GuildRole.Master)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-GUILD-MASTER-CANNOT-BE-KICKED",
                    target.AccountId,
                    "The active Master cannot be kicked.");
            }

            if (actor.Role == GuildRole.Officer && target.Role != GuildRole.Member)
            {
                return Unauthorized(request.ActorAccountId);
            }

            return TransitionKickedMember(
                request, requestFingerprint, snapshot, guild, target, kickResult.Value);
        }

        private GuildPlanningResult TransitionKickedMember(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild,
            GuildMemberSnapshot target,
            GuildMembershipState kickResult)
        {
            long candidateGuildRevision = checked(guild.Revision + 1);
            GuildSnapshot candidateGuild = CopyGuild(
                guild,
                candidateGuildRevision,
                members: ReplaceMember(
                    guild.Members,
                    target,
                    CopyMemberState(target, kickResult)));
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceGuild(snapshot.Guilds, candidateGuild),
                RemovePendingForAccount(snapshot.PendingRequests, target.AccountId),
                candidateGuildRevision);
        }

        private GuildPlanningResult PlanRoleChange(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild,
            GuildRole expectedRole,
            GuildRole candidateRole,
            bool promote)
        {
            GuildMemberSnapshot actor = FindAuthoritativeMember(guild, request.ActorAccountId);
            Func<GuildRolePolicy, bool> permission = promote
                ? new Func<GuildRolePolicy, bool>(value => value.CanPromote)
                : value => value.CanDemote;
            if (!Can(actor, permission))
            {
                return Unauthorized(request.ActorAccountId);
            }

            GuildMemberSnapshot target = FindAuthoritativeMember(guild, request.TargetAccountId);
            if (target == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-GUILD-MEMBER-NOT-FOUND",
                    request.TargetAccountId,
                    "Role-change target is not an active Guild member.");
            }

            if (target.Role != expectedRole)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-GUILD-ROLE-CHANGE-INELIGIBLE",
                    target.AccountId,
                    "Role-change target is not in the required source role.");
            }

            long candidateGuildRevision = checked(guild.Revision + 1);
            GuildSnapshot candidateGuild = CopyGuild(
                guild,
                candidateGuildRevision,
                members: ReplaceMember(
                    guild.Members,
                    target,
                    CopyMember(target, candidateRole)));
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceGuild(snapshot.Guilds, candidateGuild),
                snapshot.PendingRequests,
                candidateGuildRevision);
        }

        private GuildPlanningResult PlanMasterTransfer(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild)
        {
            GuildMemberSnapshot actor = FindAuthoritativeMember(guild, request.ActorAccountId);
            if (actor?.Role != GuildRole.Master ||
                !Can(actor, value => value.CanTransferMaster))
            {
                return Unauthorized(request.ActorAccountId);
            }

            GuildMemberSnapshot target = FindAuthoritativeMember(guild, request.TargetAccountId);
            if (target == null || target.Role == GuildRole.Master)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-GUILD-MASTER-TRANSFER-INELIGIBLE",
                    request.TargetAccountId,
                    "Master transfer target must be another active member.");
            }

            long candidateGuildRevision = checked(guild.Revision + 1);
            IReadOnlyList<GuildMemberSnapshot> members = guild.Members
                .Select(row => ReferenceEquals(row, actor)
                    ? CopyMember(row, GuildRole.Officer)
                    : ReferenceEquals(row, target)
                        ? CopyMember(row, GuildRole.Master)
                        : row)
                .OrderBy(row => row.AccountId, StringComparer.Ordinal)
                .ToArray();
            GuildSnapshot candidateGuild = CopyGuild(
                guild,
                candidateGuildRevision,
                members: members);
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceGuild(snapshot.Guilds, candidateGuild),
                snapshot.PendingRequests,
                candidateGuildRevision);
        }

        private GuildPlanningResult PlanDisband(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild)
        {
            GuildMemberSnapshot actor = FindAuthoritativeMember(guild, request.ActorAccountId);
            if (actor?.Role != GuildRole.Master || !Can(actor, value => value.CanDisband))
            {
                return Unauthorized(request.ActorAccountId);
            }

            long candidateGuildRevision = checked(guild.Revision + 1);
            GuildSnapshot candidateGuild = CopyGuild(
                guild,
                candidateGuildRevision,
                status: GuildStatus.Disbanded,
                members: guild.Members.Select(row =>
                    new GuildMemberSnapshot(
                        row.AccountId,
                        row.ImmutableRealmId,
                        row.Role,
                        GuildMembershipState.Inactive)).ToArray());
            return CreatePlan(
                request,
                requestFingerprint,
                snapshot,
                ReplaceGuild(snapshot.Guilds, candidateGuild),
                snapshot.PendingRequests.Where(row =>
                    !string.Equals(row.GuildId, guild.GuildId, StringComparison.Ordinal)).ToArray(),
                candidateGuildRevision);
        }

        private GuildPlanningResult CreatePlan(
            GuildTransitionRequest request,
            string requestFingerprint,
            GuildAuthoritySnapshot snapshot,
            IReadOnlyList<GuildSnapshot> guilds,
            IReadOnlyList<GuildPendingRequest> pendingRequests,
            long candidateGuildRevision)
        {
            long candidateAuthorityRevision = checked(snapshot.Revision + 1);
            string semanticHash = SnapshotSemanticHash(
                candidateAuthorityRevision,
                policy.Binding,
                guilds,
                pendingRequests);
            string planHash = HashParts(
                "guild_plan_v1",
                requestFingerprint,
                semanticHash,
                candidateAuthorityRevision.ToString(CultureInfo.InvariantCulture),
                candidateGuildRevision.ToString(CultureInfo.InvariantCulture));
            var receipt = new GuildOperationReceipt(
                request.OperationId,
                request.Operation,
                requestFingerprint,
                request.GuildId,
                request.ActorAccountId,
                request.TargetAccountId,
                request.PendingRequestId,
                candidateAuthorityRevision,
                candidateGuildRevision,
                planHash,
                true);
            IReadOnlyList<GuildOperationReceipt> receipts = snapshot.Receipts
                .Concat(new[] { receipt })
                .OrderBy(row => row.ResultingAuthorityRevision)
                .ThenBy(row => row.OperationId, StringComparer.Ordinal)
                .ToArray();
            var candidate = new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                candidateAuthorityRevision,
                policy.Binding,
                guilds,
                pendingRequests,
                receipts,
                true);
            var plan = new GuildTransitionPlan(
                request.Operation,
                requestFingerprint,
                snapshot,
                candidate,
                receipt,
                planHash);
            return new GuildPlanningResult(
                GuildPlanningStatus.Prepared,
                plan,
                null,
                Array.Empty<GuildDiagnostic>());
        }

        private GuildPlanningResult ValidatePolicy(GuildMembershipPolicySnapshot candidate)
        {
            if (candidate == null || candidate.Status == GuildCatalogStatus.Unavailable)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-GUILD-CATALOG-UNAVAILABLE",
                    string.Empty,
                    "Guild membership policy catalog is unavailable.");
            }

            if (candidate.Status == GuildCatalogStatus.UnsupportedVersion)
            {
                return Reject(
                    GuildPlanningStatus.Unsupported,
                    "AL-GUILD-CATALOG-UNSUPPORTED",
                    string.Empty,
                    "Guild membership policy catalog version is unsupported.");
            }

            if (candidate.Status == GuildCatalogStatus.Incomplete)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-GUILD-CATALOG-INCOMPLETE",
                    string.Empty,
                    "Guild membership policy catalog is incomplete.");
            }

            if (candidate.Status != GuildCatalogStatus.Ready ||
                !candidate.IsComplete ||
                !IsValidBinding(candidate.Binding) ||
                candidate.RolePolicies == null ||
                candidate.StatePolicies == null ||
                candidate.ExcludedEffectDomains == null ||
                !candidate.AccountFirstWithinImmutableRealm ||
                candidate.RequiredActiveMasterCount != 1 ||
                candidate.DefaultJoinedRole != GuildRole.Member)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-CATALOG-MALFORMED",
                    string.Empty,
                    "Guild membership policy is incomplete or contradictory.");
            }

            if (candidate.RolePolicies.Count != 3 ||
                candidate.RolePolicies.Any(row => row == null) ||
                candidate.RolePolicies.Select(row => row.Role).Distinct().Count() != 3 ||
                !candidate.RolePolicies.Select(row => row.Role).OrderBy(row => row)
                    .SequenceEqual(new[] { GuildRole.Master, GuildRole.Officer, GuildRole.Member }) ||
                candidate.StatePolicies.Count != 5 ||
                candidate.StatePolicies.Any(row => row == null) ||
                !candidate.StatePolicies.Select(row => row.State).SequenceEqual(new[]
                {
                    GuildMembershipState.Active,
                    GuildMembershipState.Restricted,
                    GuildMembershipState.PendingLeave,
                    GuildMembershipState.Banned,
                    GuildMembershipState.Inactive
                }) ||
                candidate.ExcludedEffectDomains.Count != 5 ||
                candidate.ExcludedEffectDomains.Distinct().Count() != 5 ||
                !candidate.ExcludedEffectDomains.OrderBy(row => row).SequenceEqual(new[]
                {
                    GuildEffectDomain.Combat,
                    GuildEffectDomain.Economy,
                    GuildEffectDomain.Perk,
                    GuildEffectDomain.City,
                    GuildEffectDomain.Raid
                }))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-CATALOG-ROWS-INVALID",
                    string.Empty,
                    "Guild roles, membership states, or excluded effect domains are incomplete.");
            }

            GuildRolePolicy master = PolicyFor(GuildRole.Master);
            GuildRolePolicy officer = PolicyFor(GuildRole.Officer);
            GuildRolePolicy member = PolicyFor(GuildRole.Member);
            bool masterComplete = master.CanManageInvitations && master.CanManageMembers &&
                                  master.CanPromote && master.CanDemote &&
                                  master.CanTransferMaster && master.CanDisband &&
                                  master.CanFormAlliancesOrDeclareWar && master.CanOpenRaidCalls;
            bool officerSafe = officer.CanManageInvitations && officer.CanManageMembers &&
                               !officer.CanPromote && !officer.CanDemote &&
                               !officer.CanTransferMaster && !officer.CanDisband &&
                               !officer.CanFormAlliancesOrDeclareWar && officer.CanOpenRaidCalls;
            bool memberSafe = !member.CanManageInvitations && !member.CanManageMembers &&
                              !member.CanPromote && !member.CanDemote &&
                              !member.CanTransferMaster && !member.CanDisband &&
                              !member.CanFormAlliancesOrDeclareWar && !member.CanOpenRaidCalls;
            bool statesSafe = StatePolicyMatches(
                                  GuildMembershipState.Active, true, true,
                                  GuildMembershipState.PendingLeave,
                                  GuildMembershipState.Banned, true) &&
                              StatePolicyMatches(
                                  GuildMembershipState.Restricted, true, false,
                                  GuildMembershipState.PendingLeave,
                                  GuildMembershipState.Banned, true) &&
                              StatePolicyMatches(
                                  GuildMembershipState.PendingLeave, true, false,
                                  GuildMembershipState.Inactive, null, true) &&
                              StatePolicyMatches(
                                  GuildMembershipState.Banned, false, false,
                                  null, null, true) &&
                              StatePolicyMatches(
                                  GuildMembershipState.Inactive, false, false,
                                  null, null, false);
            if (!masterComplete || !officerSafe || !memberSafe || !statesSafe)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-CATALOG-POLICY-INVALID",
                    string.Empty,
                    "Guild role or membership-state policy contradicts the accepted authority boundary.");
            }

            return null;
        }

        private GuildPlanningResult ValidateAuthority(GuildAuthoritySnapshot snapshot)
        {
            if (snapshot == null || snapshot.Status == GuildAuthorityStatus.Unavailable)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-GUILD-AUTHORITY-UNAVAILABLE",
                    string.Empty,
                    "Guild authority snapshot is unavailable.");
            }

            if (snapshot.Status == GuildAuthorityStatus.CommitUncertain)
            {
                return Reject(
                    GuildPlanningStatus.CommitUncertain,
                    "AL-GUILD-COMMIT-UNCERTAIN",
                    string.Empty,
                    "Guild authority requires reconciliation before another operation.");
            }

            if (snapshot.Status == GuildAuthorityStatus.UnsupportedReadOnly)
            {
                return Reject(
                    GuildPlanningStatus.Unsupported,
                    "AL-GUILD-AUTHORITY-UNSUPPORTED",
                    string.Empty,
                    "Unknown-future Guild authority is preserved read-only.");
            }

            if (snapshot.Status != GuildAuthorityStatus.Available ||
                !snapshot.IsComplete ||
                snapshot.Revision < 0 ||
                !BindingEquals(snapshot.CatalogBinding, policy.Binding) ||
                snapshot.Guilds == null ||
                snapshot.PendingRequests == null ||
                snapshot.Receipts == null ||
                snapshot.Guilds.Count > MaximumGuilds ||
                snapshot.PendingRequests.Count > MaximumPendingRequests ||
                snapshot.Receipts.Count > MaximumReceipts ||
                !IsStrictlyOrdered(snapshot.Guilds, row => row?.GuildId) ||
                !IsStrictlyOrdered(snapshot.PendingRequests, row => row?.RequestId))
            {
                return MalformedAuthority();
            }

            var guildIds = new HashSet<string>(StringComparer.Ordinal);
            var reservingAccounts = new HashSet<string>(StringComparer.Ordinal);
            foreach (GuildSnapshot guild in snapshot.Guilds)
            {
                if (guild == null ||
                    !IsStableId(guild.GuildId) ||
                    !IsStableId(guild.ImmutableRealmId) ||
                    guild.Revision <= 0 ||
                    !Enum.IsDefined(typeof(GuildStatus), guild.Status) ||
                    guild.Members == null ||
                    guild.Members.Count == 0 ||
                    guild.Members.Count > MaximumMembersPerGuild ||
                    !guildIds.Add(guild.GuildId) ||
                    !IsStrictlyOrdered(guild.Members, row => row?.AccountId))
                {
                    return MalformedAuthority();
                }

                var memberIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuildMemberSnapshot member in guild.Members)
                {
                    if (member != null &&
                        !Enum.IsDefined(typeof(GuildMembershipState), member.State))
                    {
                        return Reject(
                            GuildPlanningStatus.Unsupported,
                            "AL-GUILD-MEMBERSHIP-STATE-UNSUPPORTED",
                            member.AccountId,
                            "Unknown-future Guild membership state is preserved read-only.");
                    }

                    if (member == null ||
                        !IsOpaqueId(member.AccountId) ||
                        !IsStableId(member.ImmutableRealmId) ||
                        !string.Equals(
                            member.ImmutableRealmId,
                            guild.ImmutableRealmId,
                            StringComparison.Ordinal) ||
                        !Enum.IsDefined(typeof(GuildRole), member.Role) ||
                        !memberIds.Add(member.AccountId) ||
                        (StatePolicyFor(member.State).ReservesAccount &&
                         !reservingAccounts.Add(member.AccountId)))
                    {
                        return MalformedAuthority();
                    }
                }

                int activeMasters = guild.Members.Count(row =>
                    row.State == GuildMembershipState.Active && row.Role == GuildRole.Master);
                bool hasActiveMembers = guild.Members.Any(row =>
                    row.State == GuildMembershipState.Active);
                if ((guild.Status == GuildStatus.Active &&
                     (activeMasters != policy.RequiredActiveMasterCount || !hasActiveMembers)) ||
                    (guild.Status == GuildStatus.Disbanded && hasActiveMembers))
                {
                    return MalformedAuthority();
                }
            }

            var pendingIds = new HashSet<string>(StringComparer.Ordinal);
            var pendingAccounts = new HashSet<string>(StringComparer.Ordinal);
            foreach (GuildPendingRequest pending in snapshot.PendingRequests)
            {
                GuildSnapshot guild = pending == null
                    ? null
                    : snapshot.Guilds.SingleOrDefault(row =>
                        string.Equals(row.GuildId, pending.GuildId, StringComparison.Ordinal));
                if (pending == null ||
                    !IsOpaqueId(pending.RequestId) ||
                    !IsStableId(pending.GuildId) ||
                    !IsOpaqueId(pending.AccountId) ||
                    !IsStableId(pending.ImmutableRealmId) ||
                    !pendingIds.Add(pending.RequestId) ||
                    !pendingAccounts.Add(pending.AccountId) ||
                    guild == null || guild.Status != GuildStatus.Active ||
                    !string.Equals(
                        pending.ImmutableRealmId,
                        guild.ImmutableRealmId,
                        StringComparison.Ordinal) ||
                    pending.GuildRevision <= 0 || pending.GuildRevision > guild.Revision ||
                    (pending.IsSupported &&
                     !Enum.IsDefined(typeof(GuildPendingRequestKind), pending.Kind)))
                {
                    return MalformedAuthority();
                }
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GuildOperationReceipt receipt in snapshot.Receipts)
            {
                if (receipt == null ||
                    !IsOpaqueId(receipt.OperationId) ||
                    !IsSha256(receipt.RequestFingerprint) ||
                    !IsStableId(receipt.GuildId) ||
                    !IsOpaqueId(receipt.ActorAccountId) ||
                    receipt.ResultingAuthorityRevision <= 0 ||
                    receipt.ResultingGuildRevision <= 0 ||
                    !IsSha256(receipt.PlanHash) ||
                    !operationIds.Add(receipt.OperationId) ||
                    (receipt.IsSupported &&
                     !Enum.IsDefined(typeof(GuildOperation), receipt.Operation)))
                {
                    return MalformedAuthority();
                }
            }

            return null;
        }

        private static GuildPlanningResult ClassifyReplay(
            GuildTransitionRequest request,
            string requestFingerprint,
            IReadOnlyList<GuildOperationReceipt> receipts)
        {
            GuildOperationReceipt match = receipts.SingleOrDefault(row =>
                string.Equals(row.OperationId, request.OperationId, StringComparison.Ordinal));
            if (match == null)
            {
                return null;
            }

            if (!match.IsSupported)
            {
                return Reject(
                    GuildPlanningStatus.Unsupported,
                    "AL-GUILD-REPLAY-UNSUPPORTED",
                    match.OperationId,
                    "Operation identity belongs to unknown-future Guild history.");
            }

            bool exact = match.Operation == request.Operation &&
                         string.Equals(
                             match.RequestFingerprint,
                             requestFingerprint,
                             StringComparison.Ordinal) &&
                         string.Equals(match.GuildId, request.GuildId, StringComparison.Ordinal) &&
                         string.Equals(
                             match.ActorAccountId,
                             request.ActorAccountId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             match.TargetAccountId,
                             request.TargetAccountId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             match.PendingRequestId,
                             request.PendingRequestId,
                             StringComparison.Ordinal);
            return exact
                ? new GuildPlanningResult(
                    GuildPlanningStatus.AlreadyCommitted,
                    null,
                    match,
                    new[]
                    {
                        new GuildDiagnostic(
                            "AL-GUILD-REPLAY",
                            match.OperationId,
                            "Committed Guild receipt already satisfies this operation.")
                    })
                : Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-GUILD-OPERATION-CONFLICT",
                    match.OperationId,
                    "Operation identity is already bound to different Guild semantics.");
        }

        private GuildPlanningResult EnsureAccountCanEnter(
            GuildAuthoritySnapshot snapshot,
            GuildSnapshot guild,
            string accountId,
            string pendingRequestId)
        {
            if (FindReservingMembership(snapshot, accountId) != null)
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-GUILD-ACCOUNT-ALREADY-MEMBER",
                    accountId,
                    "Account already has one reserving Guild membership.");
            }

            GuildMemberSnapshot sameGuild = FindMember(guild, accountId);
            if (sameGuild != null && StatePolicyFor(sameGuild.State).BlocksSameGuildEntry)
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-GUILD-ACCOUNT-CYCLE-BLOCKED",
                    accountId,
                    "Account membership state blocks entry into this Guild cycle.");
            }

            GuildPendingRequest collision = snapshot.PendingRequests.FirstOrDefault(row =>
                string.Equals(row.RequestId, pendingRequestId, StringComparison.Ordinal) ||
                string.Equals(row.AccountId, accountId, StringComparison.Ordinal));
            if (collision == null)
            {
                return null;
            }

            return Reject(
                collision.IsSupported ? GuildPlanningStatus.Conflict : GuildPlanningStatus.Unsupported,
                "AL-GUILD-PENDING-CONFLICT",
                collision.RequestId,
                "Pending identity or account is already reserved.");
        }

        private bool Can(
            GuildMemberSnapshot member,
            Func<GuildRolePolicy, bool> permission)
        {
            return member != null && permission(PolicyFor(member.Role));
        }

        private GuildRolePolicy PolicyFor(GuildRole role)
        {
            return policy.RolePolicies.Single(row => row.Role == role);
        }

        private GuildMembershipStatePolicy StatePolicyFor(GuildMembershipState state)
        {
            return policy.StatePolicies.Single(row => row.State == state);
        }

        private bool StatePolicyMatches(
            GuildMembershipState state,
            bool reservesAccount,
            bool grantsRoleAuthority,
            GuildMembershipState? leaveResult,
            GuildMembershipState? kickResult,
            bool blocksSameGuildEntry)
        {
            GuildMembershipStatePolicy candidate = StatePolicyFor(state);
            return candidate.ReservesAccount == reservesAccount &&
                   candidate.GrantsRoleAuthority == grantsRoleAuthority &&
                   candidate.LeaveResult == leaveResult &&
                   candidate.KickResult == kickResult &&
                   candidate.BlocksSameGuildEntry == blocksSameGuildEntry;
        }

        private GuildMemberSnapshot FindAuthoritativeMember(
            GuildSnapshot guild,
            string accountId)
        {
            return guild.Members.SingleOrDefault(row =>
                StatePolicyFor(row.State).GrantsRoleAuthority &&
                string.Equals(row.AccountId, accountId, StringComparison.Ordinal));
        }

        private static GuildMemberSnapshot FindMember(
            GuildSnapshot guild,
            string accountId)
        {
            return guild.Members.SingleOrDefault(row =>
                string.Equals(row.AccountId, accountId, StringComparison.Ordinal));
        }

        private GuildMemberSnapshot FindReservingMembership(
            GuildAuthoritySnapshot snapshot,
            string accountId)
        {
            return snapshot.Guilds
                .SelectMany(row => row.Members)
                .SingleOrDefault(row =>
                    StatePolicyFor(row.State).ReservesAccount &&
                    string.Equals(row.AccountId, accountId, StringComparison.Ordinal));
        }

        private static IReadOnlyList<GuildSnapshot> InsertGuild(
            IReadOnlyList<GuildSnapshot> guilds,
            GuildSnapshot candidate)
        {
            return guilds.Concat(new[] { candidate })
                .OrderBy(row => row.GuildId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<GuildSnapshot> ReplaceGuild(
            IReadOnlyList<GuildSnapshot> guilds,
            GuildSnapshot candidate)
        {
            return guilds.Select(row =>
                    string.Equals(row.GuildId, candidate.GuildId, StringComparison.Ordinal)
                        ? candidate
                        : row)
                .OrderBy(row => row.GuildId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<GuildPendingRequest> InsertPending(
            IReadOnlyList<GuildPendingRequest> pending,
            GuildPendingRequest candidate)
        {
            return pending.Concat(new[] { candidate })
                .OrderBy(row => row.RequestId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<GuildPendingRequest> RemovePendingForAccount(
            IReadOnlyList<GuildPendingRequest> pending,
            string accountId)
        {
            return pending.Where(row =>
                    !string.Equals(row.AccountId, accountId, StringComparison.Ordinal))
                .ToArray();
        }

        private static IReadOnlyList<GuildMemberSnapshot> InsertMember(
            IReadOnlyList<GuildMemberSnapshot> members,
            GuildMemberSnapshot candidate)
        {
            return members.Concat(new[] { candidate })
                .OrderBy(row => row.AccountId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<GuildMemberSnapshot> ReplaceMember(
            IReadOnlyList<GuildMemberSnapshot> members,
            GuildMemberSnapshot current,
            GuildMemberSnapshot candidate)
        {
            return members.Select(row => ReferenceEquals(row, current) ? candidate : row)
                .OrderBy(row => row.AccountId, StringComparer.Ordinal)
                .ToArray();
        }

        private static GuildSnapshot CopyGuild(
            GuildSnapshot source,
            long revision,
            GuildStatus? status = null,
            IReadOnlyList<GuildMemberSnapshot> members = null)
        {
            return new GuildSnapshot(
                source.GuildId,
                source.ImmutableRealmId,
                revision,
                status ?? source.Status,
                members ?? source.Members);
        }

        private static GuildMemberSnapshot CopyMember(
            GuildMemberSnapshot source,
            GuildRole role)
        {
            return new GuildMemberSnapshot(
                source.AccountId,
                source.ImmutableRealmId,
                role,
                source.State);
        }

        private static GuildMemberSnapshot CopyMemberState(
            GuildMemberSnapshot source,
            GuildMembershipState state)
        {
            return new GuildMemberSnapshot(
                source.AccountId,
                source.ImmutableRealmId,
                source.Role,
                state);
        }

        private static string SnapshotSemanticHash(
            long revision,
            GuildCatalogBinding binding,
            IReadOnlyList<GuildSnapshot> guilds,
            IReadOnlyList<GuildPendingRequest> pending)
        {
            IEnumerable<string> guildParts = guilds.SelectMany(guild =>
                new[]
                {
                    guild.GuildId,
                    guild.ImmutableRealmId,
                    guild.Revision.ToString(CultureInfo.InvariantCulture),
                    ((int)guild.Status).ToString(CultureInfo.InvariantCulture)
                }.Concat(guild.Members.SelectMany(member => new[]
                {
                    member.AccountId,
                    member.ImmutableRealmId,
                    ((int)member.Role).ToString(CultureInfo.InvariantCulture),
                    ((int)member.State).ToString(CultureInfo.InvariantCulture)
                })));
            IEnumerable<string> pendingParts = pending.SelectMany(row => new[]
            {
                row.RequestId,
                ((int)row.Kind).ToString(CultureInfo.InvariantCulture),
                row.GuildId,
                row.AccountId,
                row.ImmutableRealmId,
                row.GuildRevision.ToString(CultureInfo.InvariantCulture),
                row.IsSupported ? "1" : "0"
            });
            return HashParts(
                new[]
                {
                    "guild_authority_snapshot_v1",
                    revision.ToString(CultureInfo.InvariantCulture),
                    BindingHash(binding),
                    "<guilds>"
                }
                .Concat(guildParts)
                .Concat(new[] { "<pending>" })
                .Concat(pendingParts)
                .ToArray());
        }

        private static string RequestFingerprint(GuildTransitionRequest request)
        {
            return HashParts(
                "guild_request_v1",
                ((int)request.Operation).ToString(CultureInfo.InvariantCulture),
                request.OperationId,
                request.ActorAccountId,
                request.ActorImmutableRealmId,
                request.GuildId,
                request.TargetAccountId,
                request.TargetImmutableRealmId,
                request.PendingRequestId,
                request.ExpectedAuthorityRevision.ToString(CultureInfo.InvariantCulture),
                request.ExpectedGuildRevision.ToString(CultureInfo.InvariantCulture),
                BindingHash(request.ExpectedCatalogBinding));
        }

        private static string BindingHash(GuildCatalogBinding binding)
        {
            if (binding == null)
            {
                return string.Empty;
            }

            return HashParts(
                "guild_catalog_binding_v1",
                binding.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                binding.ContentVersion,
                binding.SourceRevision,
                binding.CatalogHash);
        }

        private static bool IsValidRequest(GuildTransitionRequest request)
        {
            if (request == null ||
                !Enum.IsDefined(typeof(GuildOperation), request.Operation) ||
                !IsOpaqueId(request.OperationId) ||
                !IsOpaqueId(request.ActorAccountId) ||
                !IsStableId(request.ActorImmutableRealmId) ||
                !IsStableId(request.GuildId) ||
                request.ExpectedAuthorityRevision < 0 ||
                request.ExpectedGuildRevision < 0 ||
                !IsValidBinding(request.ExpectedCatalogBinding))
            {
                return false;
            }

            switch (request.Operation)
            {
                case GuildOperation.Create:
                    return request.ExpectedGuildRevision == 0 &&
                           EmptyTargetAndPending(request);
                case GuildOperation.Join:
                    return EmptyTarget(request) && IsOpaqueId(request.PendingRequestId);
                case GuildOperation.Invite:
                    return IsOpaqueId(request.TargetAccountId) &&
                           IsStableId(request.TargetImmutableRealmId) &&
                           IsOpaqueId(request.PendingRequestId);
                case GuildOperation.Accept:
                case GuildOperation.Decline:
                    return EmptyTarget(request) && IsOpaqueId(request.PendingRequestId);
                case GuildOperation.Leave:
                case GuildOperation.Disband:
                    return EmptyTargetAndPending(request);
                case GuildOperation.Kick:
                case GuildOperation.Promote:
                case GuildOperation.Demote:
                case GuildOperation.MasterTransfer:
                    return IsOpaqueId(request.TargetAccountId) &&
                           string.IsNullOrEmpty(request.TargetImmutableRealmId) &&
                           string.IsNullOrEmpty(request.PendingRequestId);
                default:
                    return false;
            }
        }

        private static bool EmptyTarget(GuildTransitionRequest request)
        {
            return string.IsNullOrEmpty(request.TargetAccountId) &&
                   string.IsNullOrEmpty(request.TargetImmutableRealmId);
        }

        private static bool EmptyTargetAndPending(GuildTransitionRequest request)
        {
            return EmptyTarget(request) && string.IsNullOrEmpty(request.PendingRequestId);
        }

        private static bool IsValidBinding(GuildCatalogBinding binding)
        {
            return binding != null &&
                   binding.SchemaVersion > 0 &&
                   IsOpaqueId(binding.ContentVersion) &&
                   IsOpaqueId(binding.SourceRevision) &&
                   IsSha256(binding.CatalogHash);
        }

        private static bool BindingEquals(
            GuildCatalogBinding left,
            GuildCatalogBinding right)
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

        private static GuildPlanningResult Unauthorized(string accountId)
        {
            return Reject(
                GuildPlanningStatus.Unauthorized,
                "AL-GUILD-ACTOR-UNAUTHORIZED",
                accountId,
                "Actor role does not authorize this Guild transition.");
        }

        private static GuildPlanningResult RealmConflict(string accountId)
        {
            return Reject(
                GuildPlanningStatus.Conflict,
                "AL-GUILD-REALM-CONFLICT",
                accountId,
                "Account immutable realm does not match the Guild realm.");
        }

        private static GuildPlanningResult MalformedAuthority()
        {
            return Reject(
                GuildPlanningStatus.Malformed,
                "AL-GUILD-AUTHORITY-MALFORMED",
                string.Empty,
                "Guild authority snapshot is incomplete or contradictory.");
        }

        private static GuildPlanningResult Reject(
            GuildPlanningStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new GuildPlanningResult(
                status,
                null,
                null,
                new[] { new GuildDiagnostic(code, subjectId, message) });
        }
    }
}