using System;
using System.Linq;
using AL.Alliances;
using AL.Guilds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Alliances
{
    public sealed class AllianceWarTransitionPlannerTests
    {
        private const string AccountMasterAlpha = "account_master_alpha";
        private const string AccountOfficerAlpha = "account_officer_alpha";
        private const string AccountMasterBeta = "account_master_beta";
        private const string AccountMasterGamma = "account_master_gamma";
        private const string AccountMasterDelta = "account_master_delta";
        private const string AccountMasterUmbral = "account_master_umbral";
        private const string GuildAlpha = "guild_alpha_001";
        private const string GuildBeta = "guild_beta_001";
        private const string GuildGamma = "guild_gamma_001";
        private const string GuildDelta = "guild_delta_001";
        private const string GuildUmbral = "guild_umbral_001";
        private const string AllianceStone = "alliance_stone_001";
        private const string AllianceForge = "alliance_stone_002";
        private const string RealmStonehold = "stonehold";
        private const string RealmUmbral = "umbral";
        private const long ClockZero = 1_700_000_000L;
        private const long Hour = 3600L;
        private static readonly string CatalogHash = new string('a', 64);

        [Test]
        public void MasterProposeAndMasterAcceptFormsSameRealmAllianceWithoutMutatingInputs()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot initial = EmptySnapshot(policy.Binding);

            AlliancePlanningResult proposed = planner.Plan(
                Propose(policy, "operation_propose_alpha_beta", AccountMasterAlpha, GuildAlpha,
                    GuildBeta, 1, 1, 0),
                initial,
                guilds);

            AllianceAuthoritySnapshot afterPropose = Apply(proposed);
            Assert.That(afterPropose.PendingRequests, Has.Count.EqualTo(1));
            Assert.That(afterPropose.PendingRequests.Single().Kind,
                Is.EqualTo(AlliancePendingKind.AllianceProposal));
            Assert.That(afterPropose.Alliances, Is.Empty);
            Assert.That(initial.PendingRequests, Is.Empty);
            Assert.That(initial.Revision, Is.Zero);

            AllianceAuthoritySnapshot accepted = Apply(planner.Plan(
                AcceptProposal(policy, afterPropose, "operation_accept_beta", AccountMasterBeta,
                    GuildBeta),
                afterPropose,
                guilds));

            Assert.That(accepted.PendingRequests, Is.Empty);
            Assert.That(accepted.Alliances, Has.Count.EqualTo(1));
            AllianceSnapshot alliance = accepted.Alliances.Single();
            Assert.That(alliance.AllianceId, Is.EqualTo(AllianceStone));
            Assert.That(alliance.ImmutableRealmId, Is.EqualTo(RealmStonehold));
            Assert.That(alliance.Relation, Is.EqualTo(AllianceRelationState.Active));
            Assert.That(alliance.LeadGuildId, Is.EqualTo(GuildAlpha));
            Assert.That(alliance.MemberGuilds.Select(row => row.GuildId),
                Is.EqualTo(new[] { GuildAlpha, GuildBeta }));
            Assert.That(alliance.IdentityHash, Has.Length.EqualTo(64));
            Assert.That(proposed.Plan.RequestFingerprint, Has.Length.EqualTo(64));
            Assert.That(guilds.Revision, Is.EqualTo(4));
        }

        [Test]
        public void OfficersCannotProposeAcceptOrDeclareWar()
        {
            AllianceWarPolicySnapshot policy = Policy();
            Assert.That(policy.OfficersCanFormAlliancesOrDeclareWar, Is.False);
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot state = EmptySnapshot(policy.Binding);

            Assert.That(planner.Plan(
                Propose(policy, "officer_propose", AccountOfficerAlpha, GuildAlpha, GuildBeta, 1, 1, 0),
                state,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Unauthorized));

            state = Apply(planner.Plan(
                Propose(policy, "master_propose", AccountMasterAlpha, GuildAlpha, GuildBeta, 1, 1, 0),
                state,
                guilds));
            Assert.That(planner.Plan(
                AcceptProposal(policy, state, "officer_accept", AccountOfficerAlpha, GuildAlpha),
                state,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Unauthorized));

            state = Apply(planner.Plan(
                AcceptProposal(policy, state, "master_accept", AccountMasterBeta, GuildBeta),
                state,
                guilds));
            AllianceAuthoritySnapshot opposing = FormSecondAlliance(planner, policy, state, guilds);
            Assert.That(planner.Plan(
                DeclareWar(policy, opposing, "officer_war", AccountOfficerAlpha, GuildAlpha,
                    AllianceForge),
                opposing,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Unauthorized));
        }

        [Test]
        public void CrossRealmAllianceAndWarAreRejected()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds(withUmbral: true);
            AllianceAuthoritySnapshot state = EmptySnapshot(policy.Binding);

            Assert.That(planner.Plan(
                Propose(policy, "cross_realm_propose", AccountMasterAlpha, GuildAlpha,
                    GuildUmbral, 1, 1, 0),
                state,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Conflict));

            state = FormPairedAlliances(planner, policy, guilds);
            GuildAuthoritySnapshot mutated = ReplaceGuild(
                guilds,
                new GuildSnapshot(
                    GuildGamma,
                    RealmUmbral,
                    1,
                    GuildStatus.Active,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterGamma, RealmUmbral, GuildRole.Master,
                            GuildMembershipState.Active)
                    }));
            Assert.That(planner.Plan(
                DeclareWar(policy, state, "cross_realm_war", AccountMasterAlpha, GuildAlpha,
                    AllianceForge),
                state,
                mutated).Status, Is.EqualTo(AlliancePlanningStatus.Conflict));
        }

        [Test]
        public void MembershipFenceBlocksStaleAcceptAndLeaderFollowsMemberMaster()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot state = Apply(planner.Plan(
                Propose(policy, "propose_fence", AccountMasterAlpha, GuildAlpha, GuildBeta, 1, 1, 0),
                EmptySnapshot(policy.Binding),
                guilds));

            GuildAuthoritySnapshot advancedBeta = ReplaceGuild(
                guilds,
                new GuildSnapshot(
                    GuildBeta,
                    RealmStonehold,
                    2,
                    GuildStatus.Active,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterBeta, RealmStonehold, GuildRole.Master,
                            GuildMembershipState.Active)
                    }));
            Assert.That(planner.Plan(
                AcceptProposal(policy, state, "accept_stale", AccountMasterBeta, GuildBeta),
                state,
                advancedBeta).Status, Is.EqualTo(AlliancePlanningStatus.StaleGuild));

            state = Apply(planner.Plan(
                DeclineProposal(policy, state, "decline_unfenced", AccountMasterBeta, GuildBeta),
                state,
                advancedBeta));
            Assert.That(state.PendingRequests, Is.Empty);

            state = FormPairedAlliances(planner, policy, guilds);
            GuildAuthoritySnapshot transferred = ReplaceGuild(
                guilds,
                new GuildSnapshot(
                    GuildAlpha,
                    RealmStonehold,
                    2,
                    GuildStatus.Active,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterAlpha, RealmStonehold, GuildRole.Officer,
                            GuildMembershipState.Active),
                        new GuildMemberSnapshot(
                            AccountOfficerAlpha, RealmStonehold, GuildRole.Master,
                            GuildMembershipState.Active)
                    }.OrderBy(row => row.AccountId, StringComparer.Ordinal)));
            Assert.That(planner.Plan(
                DeclareWar(policy, state, "old_master_war", AccountMasterAlpha, GuildAlpha,
                    AllianceForge),
                state,
                transferred).Status, Is.EqualTo(AlliancePlanningStatus.Unauthorized));
            Assert.That(planner.Plan(
                DeclareWar(policy, state, "new_master_war", AccountOfficerAlpha, GuildAlpha,
                    AllianceForge, actorGuildRevision: 2),
                state,
                transferred).Status, Is.EqualTo(AlliancePlanningStatus.Prepared));
        }

        [Test]
        public void DeclineAndAcceptAreTerminalXorAndReplaysAreDeterministic()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceTransitionRequest propose = Propose(
                policy, "operation_replay_propose", AccountMasterAlpha, GuildAlpha, GuildBeta, 1, 1, 0);
            AllianceAuthoritySnapshot committed = Apply(planner.Plan(
                propose, EmptySnapshot(policy.Binding), guilds));

            AlliancePlanningResult replay = planner.Plan(propose, committed, guilds);
            Assert.That(replay.Status, Is.EqualTo(AlliancePlanningStatus.AlreadyCommitted));
            Assert.That(replay.ExistingReceipt, Is.Not.Null);
            Assert.That(replay.Plan, Is.Null);

            Assert.That(planner.Plan(
                Propose(policy, "operation_replay_propose", AccountMasterAlpha, GuildAlpha,
                    GuildGamma, 1, 1, 0),
                committed,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Conflict));

            AllianceAuthoritySnapshot accepted = Apply(planner.Plan(
                AcceptProposal(policy, committed, "accept_xor", AccountMasterBeta, GuildBeta),
                committed,
                guilds));
            Assert.That(planner.Plan(
                DeclineProposal(policy, committed, "decline_after_accept", AccountMasterBeta, GuildBeta),
                accepted,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.NotFound));
        }

        [Test]
        public void LeaderDeclareWarUsesNoticeThenSevenDayActiveWindow()
        {
            AllianceWarPolicySnapshot policy = Policy();
            Assert.That(policy.WarNoticeHours, Is.EqualTo(24));
            Assert.That(policy.WarActiveHours, Is.EqualTo(168));
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot state = FormPairedAlliances(planner, policy, guilds);
            state = Apply(planner.Plan(
                DeclareWar(policy, state, "declare_notice", AccountMasterAlpha, GuildAlpha,
                    AllianceForge),
                state,
                guilds));

            AllianceWarSnapshot war = state.Wars.Single();
            Assert.That(war.CommittedState, Is.EqualTo(AllianceWarState.Declared));
            Assert.That(Hostility(planner, state, guilds, ClockZero).Kind,
                Is.EqualTo(AllianceHostilityKind.NotForced));
            Assert.That(Hostility(planner, state, guilds, ClockZero).EffectiveWarState,
                Is.EqualTo(AllianceWarState.Declared));

            AllianceHostilityDecision active = Hostility(
                planner, state, guilds, ClockZero + (24 * Hour));
            Assert.That(active.Kind, Is.EqualTo(AllianceHostilityKind.ForcedHostile));
            Assert.That(active.EffectiveWarState, Is.EqualTo(AllianceWarState.Active));
            Assert.That(active.ForcedByActiveWar, Is.True);

            AllianceHostilityDecision cooling = Hostility(
                planner, state, guilds, ClockZero + (24 * Hour) + (168 * Hour));
            Assert.That(cooling.Kind, Is.EqualTo(AllianceHostilityKind.NotForced));
            Assert.That(cooling.EffectiveWarState, Is.EqualTo(AllianceWarState.Cooling));
        }

        [Test]
        public void OnlyActiveWarForcesHostilityAndSafePoliciesAlwaysWin()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot state = FormPairedAlliances(planner, policy, guilds);
            state = Apply(planner.Plan(
                DeclareWar(policy, state, "declare_safe", AccountMasterAlpha, GuildAlpha,
                    AllianceForge),
                state,
                guilds));
            long activeClock = ClockZero + (24 * Hour);

            Assert.That(Hostility(planner, state, guilds, activeClock, GuildAlpha, GuildAlpha).Kind,
                Is.EqualTo(AllianceHostilityKind.Immune));
            Assert.That(Hostility(planner, state, guilds, activeClock, GuildAlpha, GuildBeta).Kind,
                Is.EqualTo(AllianceHostilityKind.Immune));
            foreach (AllianceZoneKind zone in new[]
            {
                AllianceZoneKind.City,
                AllianceZoneKind.Beginner,
                AllianceZoneKind.Accordant,
                AllianceZoneKind.ForcedSafe
            })
            {
                Assert.That(
                    Hostility(planner, state, guilds, activeClock, GuildAlpha, GuildGamma, zone).Kind,
                    Is.EqualTo(AllianceHostilityKind.Immune),
                    zone.ToString());
            }

            Assert.That(
                Hostility(planner, state, guilds, activeClock, GuildAlpha, GuildGamma).ForcedByActiveWar,
                Is.True);
        }

        [Test]
        public void LeaveDisbandAndWarRacesFailClosedOrIndeterminate()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot state = FormPairedAlliances(planner, policy, guilds);
            state = Apply(planner.Plan(
                DeclareWar(policy, state, "declare_race", AccountMasterAlpha, GuildAlpha,
                    AllianceForge),
                state,
                guilds));

            Assert.That(planner.Plan(
                Leave(policy, state, "stale_leave", AccountMasterBeta, GuildBeta,
                    expectedAllianceRevision: 0),
                state,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.StaleAlliance));

            AllianceAuthoritySnapshot left = Apply(planner.Plan(
                Leave(policy, state, "leave_during_war", AccountMasterBeta, GuildBeta),
                state,
                guilds));
            AllianceSnapshot suspendedStone = left.Alliances.Single(row =>
                row.AllianceId == AllianceStone);
            Assert.That(suspendedStone.Relation, Is.EqualTo(AllianceRelationState.Suspended));
            Assert.That(suspendedStone.MemberGuilds.Select(row => row.GuildId),
                Is.EqualTo(new[] { GuildAlpha }));
            Assert.That(left.Wars.Single().CommittedState,
                Is.EqualTo(AllianceWarState.ReconciliationPending));
            Assert.That(
                Hostility(planner, left, guilds, ClockZero + (24 * Hour), GuildAlpha, GuildGamma)
                    .ForcedByActiveWar,
                Is.False);
            Assert.That(
                Hostility(planner, left, guilds, ClockZero + (24 * Hour), GuildAlpha, GuildGamma)
                    .EffectiveWarState,
                Is.EqualTo(AllianceWarState.None));

            AllianceAuthoritySnapshot liveForge = FormPairedAlliances(planner, policy, guilds);
            AllianceAuthoritySnapshot disbanded = Apply(planner.Plan(
                Disband(policy, liveForge,
                    "disband_forge", AccountMasterGamma, GuildGamma, AllianceForge),
                liveForge,
                guilds));
            AllianceSnapshot cooledForge = disbanded.Alliances.Single(row =>
                row.AllianceId == AllianceForge);
            Assert.That(cooledForge.Relation, Is.EqualTo(AllianceRelationState.Cooldown));
            Assert.That(cooledForge.MemberGuilds.Select(row => row.GuildId),
                Is.EqualTo(new[] { GuildDelta, GuildGamma }));

            GuildAuthoritySnapshot missingMember = ReplaceGuild(
                guilds,
                new GuildSnapshot(
                    GuildBeta,
                    RealmStonehold,
                    1,
                    GuildStatus.Disbanded,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterBeta, RealmStonehold, GuildRole.Master,
                            GuildMembershipState.Inactive)
                    }));
            AllianceAuthoritySnapshot livePair = FormPairedAlliances(planner, policy, guilds);
            Assert.That(planner.Plan(
                DeclareWar(policy, livePair, "war_missing_guild", AccountMasterAlpha, GuildAlpha,
                    AllianceForge),
                livePair,
                missingMember).Status, Is.EqualTo(AlliancePlanningStatus.Indeterminate));
            Assert.That(
                Hostility(planner, livePair, missingMember, ClockZero + (24 * Hour)).Kind,
                Is.EqualTo(AllianceHostilityKind.Indeterminate));

            AllianceAuthoritySnapshot raceLeft = Apply(planner.Plan(
                Leave(policy, livePair, "leave_missing_peer", AccountMasterAlpha, GuildAlpha),
                livePair,
                missingMember));
            Assert.That(
                raceLeft.Alliances.Single(row => row.AllianceId == AllianceStone).Relation,
                Is.EqualTo(AllianceRelationState.Cooldown));
        }

        [Test]
        public void MutualEndStopsWarWithoutCreatingATreatyPolicy()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot state = FormPairedAlliances(planner, policy, guilds);
            state = Apply(planner.Plan(
                DeclareWar(policy, state, "declare_endable", AccountMasterAlpha, GuildAlpha,
                    AllianceForge),
                state,
                guilds));
            long activeClock = ClockZero + (24 * Hour);

            state = Apply(planner.Plan(
                ProposeWarEnd(policy, state, "propose_end", AccountMasterAlpha, GuildAlpha,
                    AllianceForge, activeClock),
                state,
                guilds));
            Assert.That(
                Hostility(planner, state, guilds, activeClock).ForcedByActiveWar,
                Is.True);

            AllianceAuthoritySnapshot declined = Apply(planner.Plan(
                DeclineWarEnd(policy, state, "decline_end", AccountMasterGamma, GuildGamma,
                    activeClock),
                state,
                guilds));
            Assert.That(
                Hostility(planner, declined, guilds, activeClock).ForcedByActiveWar,
                Is.True);

            AllianceAuthoritySnapshot ended = Apply(planner.Plan(
                AcceptWarEnd(policy, state, "accept_end", AccountMasterGamma, GuildGamma,
                    activeClock),
                state,
                guilds));
            Assert.That(ended.Wars.Single().CommittedState,
                Is.EqualTo(AllianceWarState.ReconciliationPending));
            Assert.That(
                Hostility(planner, ended, guilds, activeClock).ForcedByActiveWar,
                Is.False);
            Assert.That(
                Hostility(planner, ended, guilds, activeClock).EffectiveWarState,
                Is.EqualTo(AllianceWarState.ReconciliationPending));
            Assert.That(planner.Plan(
                DeclareWar(policy, ended, "redeclare_after_reconcile", AccountMasterAlpha,
                    GuildAlpha, AllianceForge, warId: "war_stone_002"),
                ended,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Prepared));
        }

        [Test]
        public void SuspendedMembershipBlocksProposeUntilFinalLeaveCoolsTheAlliance()
        {
            AllianceWarPolicySnapshot policy = Policy();
            var planner = new AllianceWarTransitionPlanner(policy);
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot state = Apply(planner.Plan(
                Propose(policy, "propose_suspend", AccountMasterAlpha, GuildAlpha, GuildBeta, 1, 1, 0),
                EmptySnapshot(policy.Binding),
                guilds));
            state = Apply(planner.Plan(
                AcceptProposal(policy, state, "accept_suspend", AccountMasterBeta, GuildBeta),
                state,
                guilds));

            state = Apply(planner.Plan(
                Leave(policy, state, "beta_leave_to_suspend", AccountMasterBeta, GuildBeta),
                state,
                guilds));
            AllianceSnapshot suspended = state.Alliances.Single();
            Assert.That(suspended.Relation, Is.EqualTo(AllianceRelationState.Suspended));
            Assert.That(suspended.LeadGuildId, Is.EqualTo(GuildAlpha));
            Assert.That(planner.Plan(
                Propose(policy, "alpha_blocked_while_suspended", AccountMasterAlpha, GuildAlpha,
                    GuildGamma, 1, 1, state.Revision, allianceId: "alliance_stone_003",
                    pendingRequestId: "pending_blocked_suspend"),
                state,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Conflict));
            Assert.That(planner.Plan(
                Propose(policy, "gamma_delta_unrelated", AccountMasterGamma, GuildGamma,
                    GuildDelta, 1, 1, state.Revision, allianceId: AllianceForge,
                    pendingRequestId: "pending_unrelated_gd"),
                state,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Prepared));

            state = Apply(planner.Plan(
                Leave(policy, state, "alpha_leave_to_cooldown", AccountMasterAlpha, GuildAlpha),
                state,
                guilds));
            Assert.That(state.Alliances.Single().Relation, Is.EqualTo(AllianceRelationState.Cooldown));
            Assert.That(state.Alliances.Single().MemberGuilds, Is.Empty);
            Assert.That(planner.Plan(
                Propose(policy, "alpha_gamma_after_cooldown", AccountMasterAlpha, GuildAlpha,
                    GuildGamma, 1, 1, state.Revision, allianceId: "alliance_stone_003",
                    pendingRequestId: "pending_after_cooldown"),
                state,
                guilds).Status, Is.EqualTo(AlliancePlanningStatus.Prepared));
        }

        [Test]
        public void CatalogAuthorityReplayCollisionsAndUnknownOutcomesFailClosed()
        {
            AllianceWarPolicySnapshot policy = Policy();
            GuildAuthoritySnapshot guilds = FourStoneholdGuilds();
            AllianceAuthoritySnapshot initial = EmptySnapshot(policy.Binding);
            AllianceTransitionRequest propose = Propose(
                policy, "create_gates", AccountMasterAlpha, GuildAlpha, GuildBeta, 1, 1, 0);

            foreach (AllianceCatalogStatus status in new[]
            {
                AllianceCatalogStatus.Unavailable,
                AllianceCatalogStatus.Incomplete
            })
            {
                var unavailable = new AllianceWarPolicySnapshot(
                    status, policy.Binding, true, false, 24, 168,
                    AllianceWarState.Active, policy.ImmuneZones,
                    status != AllianceCatalogStatus.Incomplete);
                Assert.That(new AllianceWarTransitionPlanner(unavailable)
                    .Plan(propose, initial, guilds).Status,
                    Is.EqualTo(AlliancePlanningStatus.Unavailable));
            }

            Assert.That(new AllianceWarTransitionPlanner(new AllianceWarPolicySnapshot(
                AllianceCatalogStatus.UnsupportedVersion, policy.Binding, true, false, 24, 168,
                AllianceWarState.Active, policy.ImmuneZones, true))
                .Plan(propose, initial, guilds).Status,
                Is.EqualTo(AlliancePlanningStatus.Unsupported));
            Assert.That(new AllianceWarTransitionPlanner(policy)
                .Plan(propose, CopyAuthority(initial, AllianceAuthorityStatus.UnsupportedReadOnly),
                    guilds).Status,
                Is.EqualTo(AlliancePlanningStatus.Unsupported));
            Assert.That(new AllianceWarTransitionPlanner(policy)
                .Plan(propose, CopyAuthority(initial, AllianceAuthorityStatus.CommitUncertain),
                    guilds).Status,
                Is.EqualTo(AlliancePlanningStatus.CommitUncertain));
            Assert.That(new AllianceWarTransitionPlanner(policy)
                .Plan(null, initial, guilds).Status,
                Is.EqualTo(AlliancePlanningStatus.InvalidRequest));
            Assert.That(new AllianceWarTransitionPlanner(policy)
                .Plan(propose, initial, CopyGuildAuthority(guilds, GuildAuthorityStatus.CommitUncertain))
                .Status,
                Is.EqualTo(AlliancePlanningStatus.CommitUncertain));

            AlliancePlanningResult first = new AllianceWarTransitionPlanner(policy)
                .Plan(propose, initial, guilds);
            AlliancePlanningResult second = new AllianceWarTransitionPlanner(policy)
                .Plan(propose, initial, guilds);
            Assert.That(first.Status, Is.EqualTo(AlliancePlanningStatus.Prepared));
            Assert.That(first.Plan.RequestFingerprint, Is.EqualTo(second.Plan.RequestFingerprint));
            Assert.That(first.Plan.PlanHash, Is.EqualTo(second.Plan.PlanHash));
        }

        private static AllianceWarPolicySnapshot Policy()
        {
            var binding = new GuildCatalogBinding(
                1, "1.0.0", "alliance_war_policy_v1", CatalogHash);
            return new AllianceWarPolicySnapshot(
                AllianceCatalogStatus.Ready,
                binding,
                true,
                false,
                24,
                168,
                AllianceWarState.Active,
                new[]
                {
                    AllianceZoneKind.City,
                    AllianceZoneKind.Beginner,
                    AllianceZoneKind.Accordant,
                    AllianceZoneKind.ForcedSafe
                },
                true);
        }

        private static AllianceAuthoritySnapshot EmptySnapshot(GuildCatalogBinding binding)
        {
            return new AllianceAuthoritySnapshot(
                AllianceAuthorityStatus.Available,
                0,
                binding,
                Array.Empty<AllianceSnapshot>(),
                Array.Empty<AlliancePendingRequest>(),
                Array.Empty<AllianceWarSnapshot>(),
                Array.Empty<AllianceOperationReceipt>(),
                true);
        }

        private static GuildAuthoritySnapshot FourStoneholdGuilds(bool withUmbral = false)
        {
            var guilds = new[]
            {
                new GuildSnapshot(
                    GuildAlpha,
                    RealmStonehold,
                    1,
                    GuildStatus.Active,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterAlpha, RealmStonehold, GuildRole.Master,
                            GuildMembershipState.Active),
                        new GuildMemberSnapshot(
                            AccountOfficerAlpha, RealmStonehold, GuildRole.Officer,
                            GuildMembershipState.Active)
                    }.OrderBy(row => row.AccountId, StringComparer.Ordinal)),
                new GuildSnapshot(
                    GuildBeta,
                    RealmStonehold,
                    1,
                    GuildStatus.Active,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterBeta, RealmStonehold, GuildRole.Master,
                            GuildMembershipState.Active)
                    }),
                new GuildSnapshot(
                    GuildDelta,
                    RealmStonehold,
                    1,
                    GuildStatus.Active,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterDelta, RealmStonehold, GuildRole.Master,
                            GuildMembershipState.Active)
                    }),
                new GuildSnapshot(
                    GuildGamma,
                    RealmStonehold,
                    1,
                    GuildStatus.Active,
                    new[]
                    {
                        new GuildMemberSnapshot(
                            AccountMasterGamma, RealmStonehold, GuildRole.Master,
                            GuildMembershipState.Active)
                    })
            };
            if (withUmbral)
            {
                guilds = guilds.Concat(new[]
                {
                    new GuildSnapshot(
                        GuildUmbral,
                        RealmUmbral,
                        1,
                        GuildStatus.Active,
                        new[]
                        {
                            new GuildMemberSnapshot(
                                AccountMasterUmbral, RealmUmbral, GuildRole.Master,
                                GuildMembershipState.Active)
                        })
                }).ToArray();
            }

            return new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                withUmbral ? 5 : 4,
                new GuildCatalogBinding(1, "1.0.0", "guild_membership_policy_v1", CatalogHash),
                guilds.OrderBy(row => row.GuildId, StringComparer.Ordinal),
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }

        private static AllianceTransitionRequest Propose(
            AllianceWarPolicySnapshot policy,
            string operationId,
            string actorAccountId,
            string actorGuildId,
            string targetGuildId,
            long actorGuildRevision,
            long targetGuildRevision,
            long expectedAuthorityRevision,
            string allianceId = AllianceStone,
            string pendingRequestId = "pending_propose_alpha_beta")
        {
            return new AllianceTransitionRequest(
                AllianceOperation.Propose,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                allianceId,
                targetGuildId,
                string.Empty,
                pendingRequestId,
                string.Empty,
                expectedAuthorityRevision,
                0,
                actorGuildRevision,
                targetGuildRevision,
                ClockZero,
                policy.Binding);
        }

        private static AllianceTransitionRequest AcceptProposal(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId)
        {
            AlliancePendingRequest pending = state.PendingRequests.Single();
            return new AllianceTransitionRequest(
                AllianceOperation.Accept,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                pending.AllianceId,
                string.Empty,
                string.Empty,
                pending.RequestId,
                string.Empty,
                state.Revision,
                pending.AllianceRevision,
                pending.TargetGuildRevision,
                pending.ProposerGuildRevision,
                ClockZero,
                policy.Binding);
        }

        private static AllianceTransitionRequest DeclineProposal(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId)
        {
            AlliancePendingRequest pending = state.PendingRequests.Single();
            return new AllianceTransitionRequest(
                AllianceOperation.Decline,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                pending.AllianceId,
                string.Empty,
                string.Empty,
                pending.RequestId,
                string.Empty,
                state.Revision,
                pending.AllianceRevision,
                0,
                0,
                ClockZero,
                policy.Binding);
        }

        private static AllianceTransitionRequest DeclareWar(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId,
            string targetAllianceId,
            long? actorGuildRevision = null,
            string warId = "war_stone_001")
        {
            AllianceSnapshot actorAlliance = state.Alliances.Single(row =>
                row.MemberGuilds.Any(member => member.GuildId == actorGuildId) &&
                row.Relation == AllianceRelationState.Active);
            return new AllianceTransitionRequest(
                AllianceOperation.DeclareWar,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                actorAlliance.AllianceId,
                string.Empty,
                targetAllianceId,
                string.Empty,
                warId,
                state.Revision,
                actorAlliance.Revision,
                actorGuildRevision ?? actorAlliance.MemberGuilds.Single(row =>
                    row.GuildId == actorGuildId).GuildRevision,
                state.Alliances.Single(row => row.AllianceId == targetAllianceId).Revision,
                ClockZero,
                policy.Binding);
        }

        private static AllianceTransitionRequest Leave(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId,
            long? expectedAllianceRevision = null)
        {
            AllianceSnapshot alliance = state.Alliances.Single(row =>
                row.MemberGuilds.Any(member => member.GuildId == actorGuildId) &&
                (row.Relation == AllianceRelationState.Active ||
                 row.Relation == AllianceRelationState.Suspended));
            return new AllianceTransitionRequest(
                AllianceOperation.Leave,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                alliance.AllianceId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                state.Revision,
                expectedAllianceRevision ?? alliance.Revision,
                alliance.MemberGuilds.Single(row => row.GuildId == actorGuildId).GuildRevision,
                0,
                ClockZero,
                policy.Binding);
        }

        private static AllianceTransitionRequest Disband(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId,
            string allianceId)
        {
            AllianceSnapshot alliance = state.Alliances.Single(row => row.AllianceId == allianceId);
            return new AllianceTransitionRequest(
                AllianceOperation.Disband,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                allianceId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                state.Revision,
                alliance.Revision,
                alliance.MemberGuilds.Single(row => row.GuildId == actorGuildId).GuildRevision,
                0,
                ClockZero,
                policy.Binding);
        }

        private static AllianceTransitionRequest ProposeWarEnd(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId,
            string targetAllianceId,
            long clock)
        {
            AllianceSnapshot actorAlliance = state.Alliances.Single(row =>
                row.MemberGuilds.Any(member => member.GuildId == actorGuildId) &&
                row.Relation == AllianceRelationState.Active);
            return new AllianceTransitionRequest(
                AllianceOperation.ProposeWarEnd,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                actorAlliance.AllianceId,
                string.Empty,
                targetAllianceId,
                "pending_war_end_001",
                state.Wars.Single().WarId,
                state.Revision,
                actorAlliance.Revision,
                actorAlliance.MemberGuilds.Single(row => row.GuildId == actorGuildId).GuildRevision,
                state.Alliances.Single(row => row.AllianceId == targetAllianceId).Revision,
                clock,
                policy.Binding);
        }

        private static AllianceTransitionRequest AcceptWarEnd(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId,
            long clock)
        {
            AlliancePendingRequest pending = state.PendingRequests.Single(row =>
                row.Kind == AlliancePendingKind.WarEnd);
            return new AllianceTransitionRequest(
                AllianceOperation.AcceptWarEnd,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                pending.TargetAllianceId,
                string.Empty,
                pending.AllianceId,
                pending.RequestId,
                state.Wars.Single().WarId,
                state.Revision,
                state.Alliances.Single(row => row.AllianceId == pending.TargetAllianceId).Revision,
                state.Alliances.Single(row => row.AllianceId == pending.TargetAllianceId)
                    .MemberGuilds.Single(row => row.GuildId == actorGuildId).GuildRevision,
                0,
                clock,
                policy.Binding);
        }

        private static AllianceTransitionRequest DeclineWarEnd(
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            string operationId,
            string actorAccountId,
            string actorGuildId,
            long clock)
        {
            AlliancePendingRequest pending = state.PendingRequests.Single(row =>
                row.Kind == AlliancePendingKind.WarEnd);
            return new AllianceTransitionRequest(
                AllianceOperation.DeclineWarEnd,
                operationId,
                actorAccountId,
                RealmStonehold,
                actorGuildId,
                pending.TargetAllianceId,
                string.Empty,
                pending.AllianceId,
                pending.RequestId,
                state.Wars.Single().WarId,
                state.Revision,
                0,
                0,
                0,
                clock,
                policy.Binding);
        }

        private static AllianceAuthoritySnapshot FormPairedAlliances(
            AllianceWarTransitionPlanner planner,
            AllianceWarPolicySnapshot policy,
            GuildAuthoritySnapshot guilds)
        {
            AllianceAuthoritySnapshot state = Apply(planner.Plan(
                Propose(policy, "setup_propose_ab", AccountMasterAlpha, GuildAlpha, GuildBeta, 1, 1, 0),
                EmptySnapshot(policy.Binding),
                guilds));
            state = Apply(planner.Plan(
                AcceptProposal(policy, state, "setup_accept_ab", AccountMasterBeta, GuildBeta),
                state,
                guilds));
            return FormSecondAlliance(planner, policy, state, guilds);
        }

        private static AllianceAuthoritySnapshot FormSecondAlliance(
            AllianceWarTransitionPlanner planner,
            AllianceWarPolicySnapshot policy,
            AllianceAuthoritySnapshot state,
            GuildAuthoritySnapshot guilds)
        {
            state = Apply(planner.Plan(
                Propose(
                    policy,
                    "setup_propose_gd",
                    AccountMasterGamma,
                    GuildGamma,
                    GuildDelta,
                    1,
                    1,
                    state.Revision,
                    allianceId: AllianceForge,
                    pendingRequestId: "pending_propose_gamma_delta"),
                state,
                guilds));
            return Apply(planner.Plan(
                AcceptProposal(policy, state, "setup_accept_gd", AccountMasterDelta, GuildDelta),
                state,
                guilds));
        }

        private static AllianceAuthoritySnapshot Apply(AlliancePlanningResult result)
        {
            Assert.That(result.Status, Is.EqualTo(AlliancePlanningStatus.Prepared),
                result.Diagnostics.FirstOrDefault()?.Code);
            Assert.That(result.Plan, Is.Not.Null);
            return result.Plan.CandidateSnapshot;
        }

        private static AllianceHostilityDecision Hostility(
            AllianceWarTransitionPlanner planner,
            AllianceAuthoritySnapshot state,
            GuildAuthoritySnapshot guilds,
            long clock,
            string actorGuildId = GuildAlpha,
            string targetGuildId = GuildGamma,
            AllianceZoneKind zone = AllianceZoneKind.Open)
        {
            return planner.EvaluateForcedHostility(
                new AllianceHostilityQuery(actorGuildId, targetGuildId, zone, clock),
                state,
                guilds);
        }

        private static GuildAuthoritySnapshot ReplaceGuild(
            GuildAuthoritySnapshot source,
            GuildSnapshot candidate)
        {
            return new GuildAuthoritySnapshot(
                source.Status,
                source.Revision,
                source.CatalogBinding,
                source.Guilds
                    .Select(row => row.GuildId == candidate.GuildId ? candidate : row)
                    .OrderBy(row => row.GuildId, StringComparer.Ordinal),
                source.PendingRequests,
                source.Receipts,
                source.IsComplete);
        }

        private static AllianceAuthoritySnapshot CopyAuthority(
            AllianceAuthoritySnapshot source,
            AllianceAuthorityStatus status)
        {
            return new AllianceAuthoritySnapshot(
                status,
                source.Revision,
                source.CatalogBinding,
                source.Alliances,
                source.PendingRequests,
                source.Wars,
                source.Receipts,
                source.IsComplete);
        }

        private static GuildAuthoritySnapshot CopyGuildAuthority(
            GuildAuthoritySnapshot source,
            GuildAuthorityStatus status)
        {
            return new GuildAuthoritySnapshot(
                status,
                source.Revision,
                source.CatalogBinding,
                source.Guilds,
                source.PendingRequests,
                source.Receipts,
                source.IsComplete);
        }
    }
}
