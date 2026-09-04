using System;
using System.Linq;
using AL.Guilds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Guilds
{
    public sealed class GuildMembershipTransitionPlannerTests
    {
        private const string AccountMaster = "account_master_001";
        private const string AccountOfficer = "account_officer_001";
        private const string AccountMember = "account_member_001";
        private const string AccountApplicant = "account_applicant_001";
        private const string GuildAlpha = "guild_alpha_001";
        private const string GuildBeta = "guild_beta_001";
        private const string RealmStonehold = "stonehold";
        private static readonly string CatalogHash = new string('a', 64);

        [Test]
        public void CreateBuildsOneMasterWithoutMutatingTheInputSnapshot()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            GuildAuthoritySnapshot initial = EmptySnapshot(policy.Binding);
            var planner = new GuildMembershipTransitionPlanner(policy);

            GuildPlanningResult result = planner.Plan(
                Request(
                    GuildOperation.Create,
                    "operation_create_alpha",
                    AccountMaster,
                    GuildAlpha,
                    expectedAuthorityRevision: 0,
                    expectedGuildRevision: 0,
                    binding: policy.Binding),
                initial);

            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.ExistingReceipt, Is.Null);
            Assert.That(result.Plan.RequestFingerprint, Has.Length.EqualTo(64));
            Assert.That(result.Plan.PlanHash, Has.Length.EqualTo(64));
            Assert.That(result.Plan.CandidateSnapshot.Revision, Is.EqualTo(1));
            Assert.That(result.Plan.CandidateSnapshot.Guilds, Has.Count.EqualTo(1));
            GuildSnapshot guild = result.Plan.CandidateSnapshot.Guilds.Single();
            Assert.That(guild.GuildId, Is.EqualTo(GuildAlpha));
            Assert.That(guild.ImmutableRealmId, Is.EqualTo(RealmStonehold));
            Assert.That(guild.Revision, Is.EqualTo(1));
            Assert.That(guild.Status, Is.EqualTo(GuildStatus.Active));
            Assert.That(guild.Members, Has.Count.EqualTo(1));
            Assert.That(guild.Members.Single().AccountId, Is.EqualTo(AccountMaster));
            Assert.That(guild.Members.Single().Role, Is.EqualTo(GuildRole.Master));
            Assert.That(guild.Members.Single().State, Is.EqualTo(GuildMembershipState.Active));
            Assert.That(result.Plan.CandidateSnapshot.Receipts, Has.Count.EqualTo(1));
            Assert.That(initial.Revision, Is.Zero);
            Assert.That(initial.Guilds, Is.Empty);
            Assert.That(initial.Receipts, Is.Empty);
        }

        [Test]
        public void InvitationAndJoinApplicationUseAccountRealmAndRoleAuthority()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            var planner = new GuildMembershipTransitionPlanner(policy);
            GuildAuthoritySnapshot state = Apply(planner.Plan(
                Request(GuildOperation.Create, "create_alpha", AccountMaster, GuildAlpha,
                    0, 0, policy.Binding),
                EmptySnapshot(policy.Binding)));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "invite_officer", AccountMaster,
                targetAccountId: AccountOfficer, targetRealmId: RealmStonehold,
                pendingRequestId: "pending_invite_officer"), state));
            Assert.That(state.PendingRequests.Single().Kind,
                Is.EqualTo(GuildPendingRequestKind.Invitation));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Accept, "accept_officer", AccountOfficer,
                pendingRequestId: "pending_invite_officer"), state));
            Assert.That(Member(state, AccountOfficer).Role, Is.EqualTo(GuildRole.Member));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Promote, "promote_officer", AccountMaster,
                targetAccountId: AccountOfficer), state));
            Assert.That(Member(state, AccountOfficer).Role, Is.EqualTo(GuildRole.Officer));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Join, "join_applicant", AccountApplicant,
                pendingRequestId: "pending_join_applicant"), state));
            Assert.That(state.PendingRequests.Single().Kind,
                Is.EqualTo(GuildPendingRequestKind.JoinApplication));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Accept, "accept_applicant", AccountOfficer,
                pendingRequestId: "pending_join_applicant"), state));

            Assert.That(Member(state, AccountApplicant).Role, Is.EqualTo(GuildRole.Member));
            Assert.That(Member(state, AccountApplicant).ImmutableRealmId,
                Is.EqualTo(RealmStonehold));
            Assert.That(state.PendingRequests, Is.Empty);
            Assert.That(state.Guilds.Single().Members.Count(row =>
                row.State == GuildMembershipState.Active && row.Role == GuildRole.Master),
                Is.EqualTo(1));
        }

        [Test]
        public void DeclineLeaveKickDemoteTransferAndDisbandAreDeterministic()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            var planner = new GuildMembershipTransitionPlanner(policy);
            GuildAuthoritySnapshot state = GuildWithThreeMembers(planner, policy.Binding);

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Demote, "demote_officer", AccountMaster,
                targetAccountId: AccountOfficer), state));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Promote, "promote_member", AccountMaster,
                targetAccountId: AccountMember), state));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Kick, "kick_officer", AccountMember,
                targetAccountId: AccountOfficer), state));
            Assert.That(state.Guilds.Single().Members.Any(row =>
                row.AccountId == AccountOfficer && row.State == GuildMembershipState.Active),
                Is.False);

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.MasterTransfer, "transfer_master", AccountMaster,
                targetAccountId: AccountMember), state));
            Assert.That(Member(state, AccountMember).Role, Is.EqualTo(GuildRole.Master));
            Assert.That(Member(state, AccountMaster).Role, Is.EqualTo(GuildRole.Officer));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Leave, "leave_old_master", AccountMaster), state));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Disband, "disband_alpha", AccountMember), state));

            GuildSnapshot disbanded = state.Guilds.Single();
            Assert.That(disbanded.Status, Is.EqualTo(GuildStatus.Disbanded));
            Assert.That(disbanded.Members.All(row => row.State == GuildMembershipState.Inactive),
                Is.True);
            Assert.That(state.PendingRequests, Is.Empty);
            Assert.That(state.Receipts.Select(row => row.ResultingAuthorityRevision),
                Is.Ordered.Ascending);
        }

        [Test]
        public void PendingRequestsCanOnlyBeDeclinedByTheExpectedParty()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            var planner = new GuildMembershipTransitionPlanner(policy);
            GuildAuthoritySnapshot state = Apply(planner.Plan(
                Request(GuildOperation.Create, "create_decline", AccountMaster, GuildAlpha,
                    0, 0, policy.Binding),
                EmptySnapshot(policy.Binding)));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "invite_decline", AccountMaster,
                targetAccountId: AccountMember, targetRealmId: RealmStonehold,
                pendingRequestId: "pending_invite_decline"), state));

            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Decline, "decline_wrong_actor", AccountApplicant,
                pendingRequestId: "pending_invite_decline"), state).Status,
                Is.EqualTo(GuildPlanningStatus.Unauthorized));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Decline, "decline_invitation", AccountMember,
                pendingRequestId: "pending_invite_decline"), state));
            Assert.That(state.PendingRequests, Is.Empty);

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Join, "join_decline", AccountApplicant,
                pendingRequestId: "pending_join_decline"), state));
            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Decline, "decline_join_wrong", AccountMember,
                pendingRequestId: "pending_join_decline"), state).Status,
                Is.EqualTo(GuildPlanningStatus.Unauthorized));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Decline, "decline_join", AccountMaster,
                pendingRequestId: "pending_join_decline"), state));
            Assert.That(state.PendingRequests, Is.Empty);
        }

        [Test]
        public void MembershipFenceBlocksStaleAcceptanceButNeverWedgesDeclineOrCancel()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            var planner = new GuildMembershipTransitionPlanner(policy);
            GuildAuthoritySnapshot state = Apply(planner.Plan(
                Request(GuildOperation.Create, "create_pending_race", AccountMaster, GuildAlpha,
                    0, 0, policy.Binding), EmptySnapshot(policy.Binding)));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "invite_pending_one", AccountMaster,
                targetAccountId: AccountMember, targetRealmId: RealmStonehold,
                pendingRequestId: "pending_one"), state));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "invite_pending_two", AccountMaster,
                targetAccountId: AccountApplicant, targetRealmId: RealmStonehold,
                pendingRequestId: "pending_two"), state));

            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Accept, "accept_stale_one", AccountMember,
                pendingRequestId: "pending_one"), state).Status,
                Is.EqualTo(GuildPlanningStatus.StaleGuild));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Decline, "cancel_stale_one", AccountMaster,
                pendingRequestId: "pending_one"), state));
            Assert.That(state.PendingRequests.Select(row => row.RequestId),
                Is.EqualTo(new[] { "pending_two" }));

            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Decline, "decline_stale_two", AccountApplicant,
                pendingRequestId: "pending_two"), state));
            Assert.That(state.PendingRequests, Is.Empty);
        }

        [Test]
        public void ReplaysFencesCollisionsAndUnknownOutcomesFailClosed()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            var planner = new GuildMembershipTransitionPlanner(policy);
            GuildAuthoritySnapshot initial = EmptySnapshot(policy.Binding);
            GuildTransitionRequest create = Request(
                GuildOperation.Create, "operation_replay", AccountMaster, GuildAlpha,
                0, 0, policy.Binding);
            GuildAuthoritySnapshot committed = Apply(planner.Plan(create, initial));

            GuildPlanningResult replay = planner.Plan(create, committed);
            Assert.That(replay.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));
            Assert.That(replay.ExistingReceipt, Is.Not.Null);
            Assert.That(replay.Plan, Is.Null);

            Assert.That(planner.Plan(Request(
                GuildOperation.Create, "operation_replay", AccountMaster, GuildBeta,
                0, 0, policy.Binding), committed).Status,
                Is.EqualTo(GuildPlanningStatus.Conflict));
            Assert.That(planner.Plan(Request(
                GuildOperation.Invite, "stale_authority", AccountMaster, GuildAlpha,
                0, 1, policy.Binding, AccountMember, RealmStonehold,
                "pending_stale_authority"), committed).Status,
                Is.EqualTo(GuildPlanningStatus.StaleAuthority));
            Assert.That(planner.Plan(Request(
                GuildOperation.Invite, "stale_guild", AccountMaster, GuildAlpha,
                committed.Revision, 0, policy.Binding, AccountMember, RealmStonehold,
                "pending_stale_guild"), committed).Status,
                Is.EqualTo(GuildPlanningStatus.StaleGuild));
            Assert.That(planner.Plan(Request(
                GuildOperation.Invite, "stale_catalog", AccountMaster, GuildAlpha,
                committed.Revision, 1, new GuildCatalogBinding(
                    1, "2.0.0", "guild_membership_policy_v2", CatalogHash),
                AccountMember, RealmStonehold, "pending_stale_catalog"), committed).Status,
                Is.EqualTo(GuildPlanningStatus.StaleCatalog));

            GuildOperationReceipt futureReceipt = new GuildOperationReceipt(
                "future_operation", (GuildOperation)99, new string('b', 64), GuildAlpha,
                AccountMaster, string.Empty, string.Empty, 99, 99,
                new string('c', 64), false);
            GuildAuthoritySnapshot future = CopyAuthority(
                initial, receipts: new[] { futureReceipt });
            Assert.That(planner.Plan(Request(
                GuildOperation.Create, "future_operation", AccountMaster, GuildAlpha,
                0, 0, policy.Binding), future).Status,
                Is.EqualTo(GuildPlanningStatus.Unsupported));
            Assert.That(planner.Plan(create, CopyAuthority(
                initial, status: GuildAuthorityStatus.CommitUncertain)).Status,
                Is.EqualTo(GuildPlanningStatus.CommitUncertain));
        }

        [Test]
        public void CrossRealmAndCrossGuildMembershipAreRejected()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            var planner = new GuildMembershipTransitionPlanner(policy);
            GuildAuthoritySnapshot state = Apply(planner.Plan(
                Request(GuildOperation.Create, "create_first", AccountMaster, GuildAlpha,
                    0, 0, policy.Binding), EmptySnapshot(policy.Binding)));

            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "invite_cross_realm", AccountMaster,
                targetAccountId: AccountMember, targetRealmId: "umbral",
                pendingRequestId: "pending_cross_realm"), state).Status,
                Is.EqualTo(GuildPlanningStatus.Conflict));

            state = Apply(planner.Plan(Request(
                GuildOperation.Create, "create_second", AccountOfficer, GuildBeta,
                state.Revision, 0, policy.Binding), state));
            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "invite_cross_guild", AccountOfficer,
                guildId: GuildBeta, targetAccountId: AccountMaster,
                targetRealmId: RealmStonehold,
                pendingRequestId: "pending_cross_guild"), state).Status,
                Is.EqualTo(GuildPlanningStatus.Conflict));
        }

        [Test]
        public void RoleBoundariesAndMasterInvariantRejectUnsafeTransitions()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            GuildRolePolicy officerPolicy = policy.RolePolicies.Single(row =>
                row.Role == GuildRole.Officer);
            Assert.That(officerPolicy.CanManageInvitations, Is.True);
            Assert.That(officerPolicy.CanManageMembers, Is.True);
            Assert.That(officerPolicy.CanOpenRaidCalls, Is.True);
            Assert.That(officerPolicy.CanFormAlliancesOrDeclareWar, Is.False);
            Assert.That(policy.RequiredActiveMasterCount, Is.EqualTo(1));

            var planner = new GuildMembershipTransitionPlanner(policy);
            GuildAuthoritySnapshot state = GuildWithThreeMembers(planner, policy.Binding);
            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Promote, "officer_promote", AccountOfficer,
                targetAccountId: AccountMember), state).Status,
                Is.EqualTo(GuildPlanningStatus.Unauthorized));
            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Kick, "officer_kick_officer", AccountOfficer,
                targetAccountId: AccountOfficer), state).Status,
                Is.EqualTo(GuildPlanningStatus.Unauthorized));
            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Leave, "master_leave", AccountMaster), state).Status,
                Is.EqualTo(GuildPlanningStatus.Ineligible));
            Assert.That(planner.Plan(NextRequest(
                state, GuildOperation.Kick, "master_kick_self", AccountMaster,
                targetAccountId: AccountMaster), state).Status,
                Is.EqualTo(GuildPlanningStatus.Ineligible));

            GuildSnapshot noMaster = new GuildSnapshot(
                GuildAlpha, RealmStonehold, 1, GuildStatus.Active,
                new[]
                {
                    new GuildMemberSnapshot(AccountMember, RealmStonehold,
                        GuildRole.Member, GuildMembershipState.Active)
                });
            GuildAuthoritySnapshot malformed = new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available, 1, policy.Binding,
                new[] { noMaster }, Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(), true);
            Assert.That(planner.Plan(Request(
                GuildOperation.Leave, "malformed_no_master", AccountMember, GuildAlpha,
                1, 1, policy.Binding), malformed).Status,
                Is.EqualTo(GuildPlanningStatus.Malformed));
        }

        [Test]
        public void CatalogAuthorityAndRequestFailuresAreExplicitAndSideEffectFree()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            GuildAuthoritySnapshot initial = EmptySnapshot(policy.Binding);
            GuildTransitionRequest create = Request(
                GuildOperation.Create, "create_gates", AccountMaster, GuildAlpha,
                0, 0, policy.Binding);

            foreach (GuildCatalogStatus status in new[]
            {
                GuildCatalogStatus.Unavailable,
                GuildCatalogStatus.Incomplete
            })
            {
                GuildMembershipPolicySnapshot unavailable = new GuildMembershipPolicySnapshot(
                    status, policy.Binding, policy.RolePolicies,
                    policy.AccountFirstWithinImmutableRealm,
                    policy.RequiredActiveMasterCount, policy.DefaultJoinedRole,
                    policy.ExcludedEffectDomains, status != GuildCatalogStatus.Incomplete);
                Assert.That(new GuildMembershipTransitionPlanner(unavailable)
                    .Plan(create, initial).Status, Is.EqualTo(GuildPlanningStatus.Unavailable));
            }

            Assert.That(new GuildMembershipTransitionPlanner(new GuildMembershipPolicySnapshot(
                GuildCatalogStatus.UnsupportedVersion, policy.Binding, policy.RolePolicies,
                true, 1, GuildRole.Member, policy.ExcludedEffectDomains, true))
                .Plan(create, initial).Status, Is.EqualTo(GuildPlanningStatus.Unsupported));
            Assert.That(new GuildMembershipTransitionPlanner(policy)
                .Plan(create, CopyAuthority(initial,
                    status: GuildAuthorityStatus.UnsupportedReadOnly)).Status,
                Is.EqualTo(GuildPlanningStatus.Unsupported));
            Assert.That(new GuildMembershipTransitionPlanner(policy)
                .Plan(create, CopyAuthority(initial,
                    status: GuildAuthorityStatus.Unavailable)).Status,
                Is.EqualTo(GuildPlanningStatus.Unavailable));
            Assert.That(new GuildMembershipTransitionPlanner(policy)
                .Plan(null, initial).Status,
                Is.EqualTo(GuildPlanningStatus.InvalidRequest));
            Assert.That(new GuildMembershipTransitionPlanner(policy)
                .Plan(Request((GuildOperation)99, "bad_operation", AccountMaster,
                    GuildAlpha, 0, 0, policy.Binding), initial).Status,
                Is.EqualTo(GuildPlanningStatus.InvalidRequest));

            GuildPlanningResult result = new GuildMembershipTransitionPlanner(policy)
                .Plan(create, initial);
            Assert.That(result.Plan.EffectDomains, Is.Empty);
            Assert.That(policy.ExcludedEffectDomains, Is.EquivalentTo(new[]
            {
                GuildEffectDomain.Combat,
                GuildEffectDomain.Economy,
                GuildEffectDomain.Perk,
                GuildEffectDomain.City,
                GuildEffectDomain.Raid
            }));
        }

        [Test]
        public void CallerCollectionsAreCopiedAndEquivalentPlansHaveStableHashes()
        {
            GuildMembershipPolicySnapshot policy = Policy();
            var planner = new GuildMembershipTransitionPlanner(policy);
            var guilds = new System.Collections.Generic.List<GuildSnapshot>();
            GuildAuthoritySnapshot initial = new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available, 0, policy.Binding, guilds,
                Array.Empty<GuildPendingRequest>(), Array.Empty<GuildOperationReceipt>(), true);
            guilds.Add(new GuildSnapshot(
                GuildBeta, RealmStonehold, 1, GuildStatus.Active,
                new[]
                {
                    new GuildMemberSnapshot(AccountOfficer, RealmStonehold,
                        GuildRole.Master, GuildMembershipState.Active)
                }));
            Assert.That(initial.Guilds, Is.Empty);

            GuildTransitionRequest first = Request(
                GuildOperation.Create, "alpha|beta", AccountMaster, GuildAlpha,
                0, 0, policy.Binding);
            GuildTransitionRequest delimited = Request(
                GuildOperation.Create, "alpha", "beta|" + AccountMaster, GuildAlpha,
                0, 0, policy.Binding);
            GuildPlanningResult one = planner.Plan(first, initial);
            GuildPlanningResult equivalent = planner.Plan(first, initial);
            GuildPlanningResult two = planner.Plan(delimited, initial);
            Assert.That(one.Plan.RequestFingerprint,
                Is.EqualTo(equivalent.Plan.RequestFingerprint));
            Assert.That(one.Plan.PlanHash, Is.EqualTo(equivalent.Plan.PlanHash));
            Assert.That(one.Plan.RequestFingerprint,
                Is.Not.EqualTo(two.Plan.RequestFingerprint));
            Assert.That(one.Plan.PlanHash, Is.Not.EqualTo(two.Plan.PlanHash));
        }

        private static GuildMembershipPolicySnapshot Policy()
        {
            var binding = new GuildCatalogBinding(
                1,
                "1.0.0",
                "guild_membership_policy_v1",
                CatalogHash);
            return new GuildMembershipPolicySnapshot(
                GuildCatalogStatus.Ready,
                binding,
                new[]
                {
                    new GuildRolePolicy(GuildRole.Master, true, true, true, true, true, true, true, true),
                    new GuildRolePolicy(GuildRole.Officer, true, true, false, false, false, false, false, true),
                    new GuildRolePolicy(GuildRole.Member, false, false, false, false, false, false, false, false)
                },
                true,
                1,
                GuildRole.Member,
                new[]
                {
                    GuildEffectDomain.Combat,
                    GuildEffectDomain.Economy,
                    GuildEffectDomain.Perk,
                    GuildEffectDomain.City,
                    GuildEffectDomain.Raid
                },
                true);
        }

        private static GuildAuthoritySnapshot EmptySnapshot(GuildCatalogBinding binding)
        {
            return new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                0,
                binding,
                Array.Empty<GuildSnapshot>(),
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }

        private static GuildTransitionRequest Request(
            GuildOperation operation,
            string operationId,
            string actorAccountId,
            string guildId,
            long expectedAuthorityRevision,
            long expectedGuildRevision,
            GuildCatalogBinding binding,
            string targetAccountId = "",
            string targetRealmId = "",
            string pendingRequestId = "")
        {
            return new GuildTransitionRequest(
                operation,
                operationId,
                actorAccountId,
                RealmStonehold,
                guildId,
                targetAccountId,
                targetRealmId,
                pendingRequestId,
                expectedAuthorityRevision,
                expectedGuildRevision,
                binding);
        }

        private static GuildTransitionRequest NextRequest(
            GuildAuthoritySnapshot state,
            GuildOperation operation,
            string operationId,
            string actorAccountId,
            string guildId = GuildAlpha,
            string targetAccountId = "",
            string targetRealmId = "",
            string pendingRequestId = "")
        {
            GuildSnapshot guild = state.Guilds.Single(row => row.GuildId == guildId);
            return Request(
                operation,
                operationId,
                actorAccountId,
                guildId,
                state.Revision,
                guild.Revision,
                state.CatalogBinding,
                targetAccountId,
                targetRealmId,
                pendingRequestId);
        }

        private static GuildAuthoritySnapshot Apply(GuildPlanningResult result)
        {
            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared),
                result.Diagnostics.FirstOrDefault()?.Code);
            Assert.That(result.Plan, Is.Not.Null);
            return result.Plan.CandidateSnapshot;
        }

        private static GuildMemberSnapshot Member(
            GuildAuthoritySnapshot state,
            string accountId)
        {
            return state.Guilds.Single(row => row.GuildId == GuildAlpha).Members.Single(row =>
                row.AccountId == accountId && row.State == GuildMembershipState.Active);
        }

        private static GuildAuthoritySnapshot GuildWithThreeMembers(
            GuildMembershipTransitionPlanner planner,
            GuildCatalogBinding binding)
        {
            GuildAuthoritySnapshot state = Apply(planner.Plan(
                Request(GuildOperation.Create, "setup_create", AccountMaster, GuildAlpha,
                    0, 0, binding), EmptySnapshot(binding)));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "setup_invite_officer", AccountMaster,
                targetAccountId: AccountOfficer, targetRealmId: RealmStonehold,
                pendingRequestId: "setup_pending_officer"), state));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Accept, "setup_accept_officer", AccountOfficer,
                pendingRequestId: "setup_pending_officer"), state));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Promote, "setup_promote_officer", AccountMaster,
                targetAccountId: AccountOfficer), state));
            state = Apply(planner.Plan(NextRequest(
                state, GuildOperation.Invite, "setup_invite_member", AccountOfficer,
                targetAccountId: AccountMember, targetRealmId: RealmStonehold,
                pendingRequestId: "setup_pending_member"), state));
            return Apply(planner.Plan(NextRequest(
                state, GuildOperation.Accept, "setup_accept_member", AccountMember,
                pendingRequestId: "setup_pending_member"), state));
        }

        private static GuildAuthoritySnapshot CopyAuthority(
            GuildAuthoritySnapshot source,
            GuildAuthorityStatus? status = null,
            GuildSnapshot[] guilds = null,
            GuildPendingRequest[] pendingRequests = null,
            GuildOperationReceipt[] receipts = null)
        {
            return new GuildAuthoritySnapshot(
                status ?? source.Status,
                source.Revision,
                source.CatalogBinding,
                guilds ?? source.Guilds,
                pendingRequests ?? source.PendingRequests,
                receipts ?? source.Receipts,
                source.IsComplete);
        }
    }
}
