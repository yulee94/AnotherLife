using System;
using System.Linq;
using AL.Alliances;
using AL.Guilds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Guilds
{
    public sealed class GuildRaidMusterPlannerTests
    {
        private const string AccountMaster = "account_master_001";
        private const string AccountOfficer = "account_officer_001";
        private const string AccountMemberA = "account_member_a_001";
        private const string AccountMemberB = "account_member_b_001";
        private const string AccountForeign = "account_foreign_001";
        private const string GuildAlpha = "guild_alpha_001";
        private const string GuildBravo = "guild_bravo_001";
        private const string RealmStonehold = "stonehold";
        private const string CallAlpha = "raid_call_alpha_001";
        private const string ClosedInstance = "closed_raid_veil_vault";
        private const string ClosedTopology = "closed_raid_topology_v1";
        private const string EnvelopeIn = "closed_raid_envelope_veil_001";
        private const string EnvelopeReturn = "safe_return_envelope_alpha_001";
        private const string BossIron = "raid_boss_iron_colossus";
        private const string BossAsh = "raid_boss_ash_seraph";
        private const string BossThorn = "raid_boss_thorn_wraith";
        private const string BossVeil = "raid_boss_veil_regent";
        private const long SeasonEpoch = 3;
        private const long WeekId = 12;
        private const long ClockStart = 1700000000;
        private const long WindowSeconds = 1800;
        private static readonly string CatalogHash = new string('b', 64);

        [Test]
        public void MasterAndOfficerCanAnnounceMemberCannotAndRoleDriftBlocksLaunch()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot empty = EmptyRaids();
            GuildAuthoritySnapshot membership = Membership();

            RaidPlanningResult masterCall = planner.Plan(
                Announce(AccountMaster, "operation_announce_master"),
                empty,
                membership,
                EmptyAlliance());
            Assert.That(masterCall.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(masterCall.Plan.CandidateSnapshot.Calls.Single().State, Is.EqualTo(RaidCallState.Accepting));
            Assert.That(masterCall.Plan.CandidateSnapshot.Calls.Single().WindowEndUnixSeconds,
                Is.EqualTo(ClockStart + WindowSeconds));
            Assert.That(empty.Calls, Is.Empty);

            RaidPlanningResult officerCall = planner.Plan(
                Announce(AccountOfficer, "operation_announce_officer"),
                empty,
                membership,
                EmptyAlliance());
            Assert.That(officerCall.Status, Is.EqualTo(GuildPlanningStatus.Prepared));

            RaidPlanningResult memberCall = planner.Plan(
                Announce(AccountMemberA, "operation_announce_member"),
                empty,
                membership,
                EmptyAlliance());
            Assert.That(memberCall.Status, Is.EqualTo(GuildPlanningStatus.Unauthorized));

            RaidCallSnapshot accepting = Apply(planner.Plan(
                Announce(AccountOfficer, "operation_announce_for_drift"),
                empty,
                membership,
                EmptyAlliance())).Calls.Single();
            RaidAuthoritySnapshot withJoins = ApplyJoins(planner, accepting, AccountMemberA, AccountMemberB);
            GuildAuthoritySnapshot drifted = Membership(officerRole: GuildRole.Member);
            RaidPlanningResult driftedLaunch = planner.Plan(
                Launch(AccountOfficer, "operation_launch_drifted", expectedRaidRevision: withJoins.Revision),
                withJoins,
                drifted,
                EmptyAlliance());
            Assert.That(driftedLaunch.Status, Is.EqualTo(GuildPlanningStatus.Unauthorized));
        }

        [Test]
        public void OptInWindowIsThirtyMinutesJoinDeclineAndSilenceIsNotJoin()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot accepting = Apply(planner.Plan(
                Announce(AccountMaster, "operation_announce_window"),
                EmptyRaids(),
                Membership(),
                EmptyAlliance()));

            RaidPlanningResult join = planner.Plan(
                Respond(RaidOperation.Join, AccountMemberA, "operation_join_a", ClockStart + 10,
                    expectedRaidRevision: accepting.Revision),
                accepting,
                Membership(),
                EmptyAlliance());
            Assert.That(join.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(
                join.Plan.CandidateSnapshot.Calls.Single().Participants
                    .Single(value => value.AccountId == AccountMemberA).Response,
                Is.EqualTo(RaidParticipantResponse.Join));
            Assert.That(
                accepting.Calls.Single().Participants
                    .Single(value => value.AccountId == AccountMemberA).Response,
                Is.EqualTo(RaidParticipantResponse.NoResponse));

            RaidPlanningResult decline = planner.Plan(
                Respond(RaidOperation.Decline, AccountMemberB, "operation_decline_b", ClockStart + 11,
                    expectedRaidRevision: join.Plan.CandidateSnapshot.Revision),
                join.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(decline.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(
                decline.Plan.CandidateSnapshot.Calls.Single().Participants
                    .Single(value => value.AccountId == AccountMemberB).Response,
                Is.EqualTo(RaidParticipantResponse.Decline));

            RaidPlanningResult lateJoin = planner.Plan(
                Respond(RaidOperation.Join, AccountOfficer, "operation_join_late", ClockStart + WindowSeconds,
                    expectedRaidRevision: decline.Plan.CandidateSnapshot.Revision),
                decline.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(lateJoin.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            RaidPlanningResult edgeJoin = planner.Plan(
                Respond(RaidOperation.Join, AccountOfficer, "operation_join_edge", ClockStart + WindowSeconds - 1,
                    expectedRaidRevision: decline.Plan.CandidateSnapshot.Revision),
                decline.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(edgeJoin.Status, Is.EqualTo(GuildPlanningStatus.Prepared));

            Assert.That(
                decline.Plan.CandidateSnapshot.Calls.Single().Participants
                    .Single(value => value.AccountId == AccountOfficer).Response,
                Is.EqualTo(RaidParticipantResponse.NoResponse));
            Assert.That(
                decline.Plan.CandidateSnapshot.Calls.Single().Participants
                    .Count(value => value.Response == RaidParticipantResponse.Join),
                Is.EqualTo(1));
        }

        [Test]
        public void LaunchRevalidatesMinJoinCountAndStaleBossOrWeek()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot accepting = Apply(planner.Plan(
                Announce(AccountMaster, "operation_announce_launch"),
                EmptyRaids(),
                Membership(),
                EmptyAlliance()));

            RaidAuthoritySnapshot oneJoin = ApplyJoins(planner, accepting.Calls.Single(), AccountMemberA);
            RaidPlanningResult tooFew = planner.Plan(
                Launch(AccountMaster, "operation_launch_few", expectedRaidRevision: oneJoin.Revision),
                oneJoin,
                Membership(),
                EmptyAlliance());
            Assert.That(tooFew.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            RaidAuthoritySnapshot withJoins = ApplyJoins(planner, accepting.Calls.Single(), AccountMemberA, AccountMemberB);
            RaidPlanningResult ready = planner.Plan(
                Launch(AccountMaster, "operation_launch_ok", expectedRaidRevision: withJoins.Revision),
                withJoins,
                Membership(),
                EmptyAlliance());
            Assert.That(ready.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(ready.Plan.CandidateSnapshot.Calls.Single().State, Is.EqualTo(RaidCallState.Countdown));
            Assert.That(ready.Plan.CandidateSnapshot.Calls.Single().Instance.State, Is.EqualTo(RaidInstanceState.NotLaunched));
            Assert.That(withJoins.Calls.Single().State, Is.EqualTo(RaidCallState.Accepting));

            RaidPlanningResult staleBoss = planner.Plan(
                Launch(AccountMaster, "operation_launch_stale_boss", bossProfileId: BossIron,
                    expectedRaidRevision: withJoins.Revision),
                withJoins,
                Membership(),
                EmptyAlliance());
            Assert.That(staleBoss.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            RaidPlanningResult staleWeek = planner.Plan(
                Launch(AccountMaster, "operation_launch_stale_week", weekId: WeekId + 1,
                    expectedRaidRevision: withJoins.Revision),
                withJoins,
                Membership(),
                EmptyAlliance());
            Assert.That(staleWeek.Status, Is.EqualTo(GuildPlanningStatus.StaleAuthority));
        }

        [Test]
        public void OneCallPerGuildPerWeekAndCrossGuildIsDenied()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot first = Apply(planner.Plan(
                Announce(AccountMaster, "operation_announce_first"),
                EmptyRaids(),
                Membership(),
                EmptyAlliance()));

            RaidPlanningResult second = planner.Plan(
                Announce(AccountOfficer, "operation_announce_second", callId: "raid_call_alpha_002",
                    expectedRaidRevision: first.Revision),
                first,
                Membership(),
                EmptyAlliance());
            Assert.That(second.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            RaidPlanningResult otherWeek = planner.Plan(
                Announce(AccountMaster, "operation_announce_next_week", callId: "raid_call_alpha_003",
                    weekId: WeekId + 1, bossProfileId: BossIron, expectedRaidRevision: first.Revision),
                first,
                Membership(),
                EmptyAlliance());
            Assert.That(otherWeek.Status, Is.EqualTo(GuildPlanningStatus.Prepared));

            RaidPlanningResult foreign = planner.Plan(
                Announce(AccountForeign, "operation_announce_foreign"),
                EmptyRaids(),
                Membership(),
                EmptyAlliance());
            Assert.That(foreign.Status, Is.EqualTo(GuildPlanningStatus.Unauthorized));
        }

        [Test]
        public void FourSlotBossRotationIsDeterministicBySeasonEpochAndWeekId()
        {
            GuildRaidMusterPlanner planner = Planner();
            Assert.That(planner.ResolveBossProfileId(SeasonEpoch, WeekId), Is.EqualTo(BossVeil));
            Assert.That(planner.ResolveBossProfileId(0, 0), Is.EqualTo(BossIron));
            Assert.That(planner.ResolveBossProfileId(0, 1), Is.EqualTo(BossAsh));
            Assert.That(planner.ResolveBossProfileId(0, 2), Is.EqualTo(BossThorn));
            Assert.That(planner.ResolveBossProfileId(1, 0), Is.EqualTo(BossAsh));
            Assert.That(planner.ResolveBossProfileId(SeasonEpoch, WeekId), Is.EqualTo(BossVeil));

            RaidPlanningResult wrongBoss = planner.Plan(
                Announce(AccountMaster, "operation_announce_wrong_boss", bossProfileId: BossIron),
                EmptyRaids(),
                Membership(),
                EmptyAlliance());
            Assert.That(wrongBoss.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));
        }

        [Test]
        public void TransferInAndOutAreExplicitAndReturnToValidatedSafeEnvelope()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot countdown = CountdownCall(planner);

            RaidPlanningResult silentTeleport = planner.Plan(
                TransferIn(AccountOfficer, "operation_transfer_no_join", AccountOfficer,
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                EmptyAlliance());
            Assert.That(silentTeleport.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            RaidPlanningResult unsafeLocation = planner.Plan(
                TransferIn(AccountMemberA, "operation_transfer_unsafe", AccountMemberA, liveLocationValid: false,
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                EmptyAlliance());
            Assert.That(unsafeLocation.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            RaidPlanningResult transferIn = planner.Plan(
                TransferIn(AccountMemberA, "operation_transfer_in_a", AccountMemberA,
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                EmptyAlliance());
            Assert.That(transferIn.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            RaidParticipantSnapshot inside = transferIn.Plan.CandidateSnapshot.Calls.Single()
                .Participants.Single(value => value.AccountId == AccountMemberA);
            Assert.That(inside.Transfer, Is.EqualTo(RaidTransferState.InInstance));
            Assert.That(inside.ClosedInstanceEnvelopeId, Is.EqualTo(EnvelopeIn));
            Assert.That(inside.SafeReturnEnvelopeId, Is.EqualTo(EnvelopeReturn));
            Assert.That(transferIn.Plan.CandidateSnapshot.Calls.Single().State, Is.EqualTo(RaidCallState.Active));
            Assert.That(transferIn.Plan.CandidateSnapshot.Calls.Single().Instance.State,
                Is.EqualTo(RaidInstanceState.Active));
            Assert.That(transferIn.Plan.EffectDomains, Is.Empty);
            Assert.That(countdown.Calls.Single().State, Is.EqualTo(RaidCallState.Countdown));

            RaidPlanningResult transferOut = planner.Plan(
                TransferOut(AccountMemberA, "operation_transfer_out_a",
                    expectedRaidRevision: transferIn.Plan.CandidateSnapshot.Revision),
                transferIn.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(transferOut.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            RaidParticipantSnapshot returned = transferOut.Plan.CandidateSnapshot.Calls.Single()
                .Participants.Single(value => value.AccountId == AccountMemberA);
            Assert.That(returned.Transfer, Is.EqualTo(RaidTransferState.Returned));
            Assert.That(returned.SafeReturnEnvelopeId, Is.EqualTo(EnvelopeReturn));
            Assert.That(transferOut.Plan.CandidateSnapshot.Calls.Single().Instance.State,
                Is.EqualTo(RaidInstanceState.Extracted));
        }

        [Test]
        public void DuplicateRestartDisconnectPartialTransferAndInstanceFailureReconcile()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot countdown = CountdownCall(planner);
            RaidPlanningResult firstIn = planner.Plan(
                TransferIn(AccountMemberA, "operation_transfer_dup", AccountMemberA,
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                EmptyAlliance());
            RaidPlanningResult duplicate = planner.Plan(
                TransferIn(AccountMemberA, "operation_transfer_dup", AccountMemberA,
                    expectedRaidRevision: firstIn.Plan.CandidateSnapshot.Revision),
                firstIn.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(duplicate.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));
            Assert.That(duplicate.ExistingReceipt, Is.Not.Null);

            RaidPlanningResult conflict = planner.Plan(
                TransferIn(AccountMemberA, "operation_transfer_dup", AccountMemberB,
                    expectedRaidRevision: firstIn.Plan.CandidateSnapshot.Revision),
                firstIn.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(conflict.Status, Is.EqualTo(GuildPlanningStatus.Conflict));

            RaidPlanningResult secondIn = planner.Plan(
                TransferIn(AccountMemberB, "operation_transfer_in_b", AccountMemberB,
                    expectedRaidRevision: firstIn.Plan.CandidateSnapshot.Revision),
                firstIn.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(secondIn.Status, Is.EqualTo(GuildPlanningStatus.Prepared));

            RaidPlanningResult disconnect = planner.Plan(
                Reconcile(AccountMemberA, "operation_reconcile_disconnect", RaidReconcileReason.Disconnect,
                    expectedRaidRevision: secondIn.Plan.CandidateSnapshot.Revision),
                secondIn.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(disconnect.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            RaidParticipantSnapshot rolledBack = disconnect.Plan.CandidateSnapshot.Calls.Single()
                .Participants.Single(value => value.AccountId == AccountMemberA);
            Assert.That(rolledBack.Transfer, Is.EqualTo(RaidTransferState.Returned));
            Assert.That(rolledBack.SafeReturnEnvelopeId, Is.EqualTo(EnvelopeReturn));
            Assert.That(rolledBack.GrantsReward, Is.False);
            Assert.That(rolledBack.AppliesLockout, Is.False);

            RaidPlanningResult partial = planner.Plan(
                Reconcile(AccountMemberB, "operation_reconcile_partial", RaidReconcileReason.PartialTransfer,
                    expectedRaidRevision: firstIn.Plan.CandidateSnapshot.Revision),
                firstIn.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(partial.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(
                partial.Plan.CandidateSnapshot.Calls.Single().Participants
                    .Single(value => value.AccountId == AccountMemberA).Transfer,
                Is.EqualTo(RaidTransferState.InInstance));
            Assert.That(
                partial.Plan.CandidateSnapshot.Calls.Single().Participants
                    .Single(value => value.AccountId == AccountMemberB).Transfer,
                Is.EqualTo(RaidTransferState.Returned));

            RaidPlanningResult failure = planner.Plan(
                Reconcile(AccountMaster, "operation_reconcile_instance", RaidReconcileReason.InstanceFailure,
                    expectedRaidRevision: secondIn.Plan.CandidateSnapshot.Revision),
                secondIn.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(failure.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            RaidCallSnapshot failed = failure.Plan.CandidateSnapshot.Calls.Single();
            Assert.That(failed.State, Is.EqualTo(RaidCallState.Failed));
            Assert.That(failed.Instance.State, Is.EqualTo(RaidInstanceState.ForceRelease));
            Assert.That(failed.Outcome, Is.EqualTo(RaidOutcomeKind.Indeterminate));
            Assert.That(failed.GrantsReward, Is.False);
            Assert.That(failed.AppliesLockout, Is.False);
            Assert.That(failed.Participants.All(value => !value.GrantsReward && !value.AppliesLockout), Is.True);
        }

        [Test]
        public void TerminalXorUnknownOutcomeGrantsNeitherRewardNorLockout()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot countdown = CountdownCall(planner);
            RaidAuthoritySnapshot active = Apply(planner.Plan(
                TransferIn(AccountMemberA, "operation_in_for_xor", AccountMemberA,
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                EmptyAlliance()));

            RaidPlanningResult complete = planner.Plan(
                Reconcile(AccountMaster, "operation_complete", RaidReconcileReason.UnknownOutcome,
                    expectedRaidRevision: active.Revision),
                active,
                Membership(),
                EmptyAlliance());
            Assert.That(complete.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            RaidCallSnapshot unknown = complete.Plan.CandidateSnapshot.Calls.Single();
            Assert.That(unknown.Outcome, Is.EqualTo(RaidOutcomeKind.Indeterminate));
            Assert.That(unknown.GrantsReward, Is.False);
            Assert.That(unknown.AppliesLockout, Is.False);
            Assert.That(unknown.Participants.Single(value => value.AccountId == AccountMemberA).Transfer,
                Is.EqualTo(RaidTransferState.Indeterminate));

            RaidPlanningResult cancelAfterUnknown = planner.Plan(
                Cancel(AccountMaster, "operation_cancel_after_unknown",
                    expectedRaidRevision: complete.Plan.CandidateSnapshot.Revision),
                complete.Plan.CandidateSnapshot,
                Membership(),
                EmptyAlliance());
            Assert.That(cancelAfterUnknown.Status, Is.EqualTo(GuildPlanningStatus.Conflict));

            RaidAuthoritySnapshot openCall = Apply(planner.Plan(
                    Announce(AccountMaster, "operation_announce_expire"),
                    EmptyRaids(),
                    Membership(),
                    EmptyAlliance()));
            RaidPlanningResult expireOpen = planner.Plan(
                Expire("operation_expire_open", expectedRaidRevision: openCall.Revision),
                openCall,
                Membership(),
                EmptyAlliance());
            Assert.That(expireOpen.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(expireOpen.Plan.CandidateSnapshot.Calls.Single().State, Is.EqualTo(RaidCallState.Expired));
            Assert.That(expireOpen.Plan.CandidateSnapshot.Calls.Single().GrantsReward, Is.False);
            Assert.That(expireOpen.Plan.CandidateSnapshot.Calls.Single().AppliesLockout, Is.False);
        }

        [Test]
        public void WarNeverBypassesConsentAndDoesNotAutoJoin()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidAuthoritySnapshot accepting = Apply(planner.Plan(
                Announce(AccountMaster, "operation_announce_war"),
                EmptyRaids(),
                Membership(),
                ActiveWar()));
            Assert.That(
                accepting.Calls.Single().Participants.All(value => value.Response == RaidParticipantResponse.NoResponse),
                Is.True);

            RaidAuthoritySnapshot countdown = CountdownCall(planner, ActiveWar());
            RaidPlanningResult forced = planner.Plan(
                TransferIn(AccountOfficer, "operation_war_force_in", AccountOfficer,
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                ActiveWar());
            Assert.That(forced.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            RaidPlanningResult consented = planner.Plan(
                TransferIn(AccountMemberA, "operation_war_consented_in", AccountMemberA,
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                ActiveWar());
            Assert.That(consented.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
        }

        [Test]
        public void ClosedInstanceCannotAliasPublicDungeonAndRejectsSceneTeleport()
        {
            GuildRaidMusterPlanner planner = Planner();
            RaidPlanningResult aliased = planner.Plan(
                Announce(AccountMaster, "operation_announce_alias",
                    closedInstanceId: "public_realm_dungeon_entrance"),
                EmptyRaids(),
                Membership(),
                EmptyAlliance());
            Assert.That(aliased.Status, Is.EqualTo(GuildPlanningStatus.Malformed));

            RaidPlanningResult questAlias = planner.Plan(
                Announce(AccountMaster, "operation_announce_quest",
                    closedInstanceId: "public_realm_dungeon_quest"),
                EmptyRaids(),
                Membership(),
                EmptyAlliance());
            Assert.That(questAlias.Status, Is.EqualTo(GuildPlanningStatus.Malformed));

            RaidAuthoritySnapshot countdown = CountdownCall(planner);
            RaidPlanningResult scene = planner.Plan(
                TransferIn(AccountMemberA, "operation_scene_teleport", AccountMemberA, sceneName: "RaidBossScene",
                    expectedRaidRevision: countdown.Revision),
                countdown,
                Membership(),
                EmptyAlliance());
            Assert.That(scene.Status, Is.EqualTo(GuildPlanningStatus.Malformed));
        }

        private static RaidAuthoritySnapshot CountdownCall(
            GuildRaidMusterPlanner planner,
            AllianceAuthoritySnapshot alliance = null)
        {
            RaidAuthoritySnapshot accepting = Apply(planner.Plan(
                Announce(AccountMaster, "operation_announce_countdown"),
                EmptyRaids(),
                Membership(),
                alliance ?? EmptyAlliance()));
            RaidAuthoritySnapshot joined = ApplyJoins(
                planner,
                accepting.Calls.Single(),
                AccountMemberA,
                AccountMemberB);
            return Apply(planner.Plan(
                Launch(AccountMaster, "operation_launch_countdown", expectedRaidRevision: joined.Revision),
                joined,
                Membership(),
                alliance ?? EmptyAlliance()));
        }

        private static RaidAuthoritySnapshot ApplyJoins(
            GuildRaidMusterPlanner planner,
            RaidCallSnapshot call,
            params string[] accountIds)
        {
            RaidAuthoritySnapshot current = new RaidAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                1,
                Binding(),
                new[] { call },
                Array.Empty<RaidOperationReceipt>(),
                true);
            foreach (string accountId in accountIds)
            {
                current = Apply(planner.Plan(
                    Respond(RaidOperation.Join, accountId, "operation_join_" + accountId, ClockStart + 5,
                        expectedRaidRevision: current.Revision),
                    current,
                    Membership(),
                    EmptyAlliance()));
            }

            return current;
        }

        private static RaidAuthoritySnapshot Apply(RaidPlanningResult result)
        {
            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared), result.Diagnostics.FirstOrDefault()?.Code);
            return result.Plan.CandidateSnapshot;
        }

        private static GuildRaidMusterPlanner Planner()
        {
            return new GuildRaidMusterPlanner(Policy());
        }

        private static GuildRaidMusterTransitionRequest Announce(
            string actorAccountId,
            string operationId,
            string callId = CallAlpha,
            long weekId = WeekId,
            string bossProfileId = BossVeil,
            string closedInstanceId = ClosedInstance,
            long expectedRaidRevision = 0)
        {
            return Request(
                RaidOperation.AnnounceCall,
                operationId,
                actorAccountId,
                callId,
                ClockStart,
                weekId,
                SeasonEpoch,
                bossProfileId,
                closedInstanceId,
                expectedRaidRevision: expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest Respond(
            RaidOperation operation,
            string actorAccountId,
            string operationId,
            long clock,
            long expectedRaidRevision = 0)
        {
            return Request(operation, operationId, actorAccountId, CallAlpha, clock,
                expectedRaidRevision: expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest Launch(
            string actorAccountId,
            string operationId,
            long weekId = WeekId,
            string bossProfileId = BossVeil,
            long expectedRaidRevision = 0)
        {
            return Request(
                RaidOperation.Launch,
                operationId,
                actorAccountId,
                CallAlpha,
                ClockStart + 60,
                weekId,
                SeasonEpoch,
                bossProfileId,
                ClosedInstance,
                expectedRaidRevision: expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest TransferIn(
            string actorAccountId,
            string operationId,
            string targetAccountId,
            bool liveLocationValid = true,
            string sceneName = "",
            long expectedRaidRevision = 0)
        {
            return Request(
                RaidOperation.TransferIn,
                operationId,
                actorAccountId,
                CallAlpha,
                ClockStart + 90,
                WeekId,
                SeasonEpoch,
                BossVeil,
                ClosedInstance,
                targetAccountId,
                EnvelopeIn,
                EnvelopeReturn,
                true,
                true,
                true,
                liveLocationValid,
                sceneName,
                RaidReconcileReason.Duplicate,
                expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest TransferOut(
            string actorAccountId,
            string operationId,
            long expectedRaidRevision = 0)
        {
            return Request(
                RaidOperation.TransferOut,
                operationId,
                actorAccountId,
                CallAlpha,
                ClockStart + 120,
                WeekId,
                SeasonEpoch,
                BossVeil,
                ClosedInstance,
                actorAccountId,
                EnvelopeIn,
                EnvelopeReturn,
                expectedRaidRevision: expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest Reconcile(
            string actorAccountId,
            string operationId,
            RaidReconcileReason reason,
            long expectedRaidRevision = 0)
        {
            return Request(
                RaidOperation.Reconcile,
                operationId,
                actorAccountId,
                CallAlpha,
                ClockStart + 150,
                WeekId,
                SeasonEpoch,
                BossVeil,
                ClosedInstance,
                actorAccountId,
                EnvelopeIn,
                EnvelopeReturn,
                true,
                true,
                true,
                true,
                string.Empty,
                reason,
                expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest Cancel(
            string actorAccountId,
            string operationId,
            long expectedRaidRevision = 0)
        {
            return Request(RaidOperation.Cancel, operationId, actorAccountId, CallAlpha, ClockStart + 200,
                expectedRaidRevision: expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest Expire(string operationId, long expectedRaidRevision = 0)
        {
            return Request(
                RaidOperation.Expire,
                operationId,
                AccountMaster,
                CallAlpha,
                ClockStart + WindowSeconds,
                expectedRaidRevision: expectedRaidRevision);
        }

        private static GuildRaidMusterTransitionRequest Request(
            RaidOperation operation,
            string operationId,
            string actorAccountId,
            string callId,
            long clock,
            long weekId = WeekId,
            long seasonEpoch = SeasonEpoch,
            string bossProfileId = BossVeil,
            string closedInstanceId = ClosedInstance,
            string targetAccountId = "",
            string closedInstanceEnvelopeId = "",
            string safeReturnEnvelopeId = "",
            bool eligibilityPassed = true,
            bool zoneAllowed = true,
            bool generationContinuous = true,
            bool liveLocationValid = true,
            string sceneName = "",
            RaidReconcileReason reconcileReason = RaidReconcileReason.Duplicate,
            long expectedRaidRevision = 0)
        {
            return new GuildRaidMusterTransitionRequest(
                operation,
                operationId,
                actorAccountId,
                GuildAlpha,
                callId,
                string.IsNullOrEmpty(targetAccountId) ? actorAccountId : targetAccountId,
                weekId,
                seasonEpoch,
                bossProfileId,
                closedInstanceId,
                closedInstanceEnvelopeId,
                safeReturnEnvelopeId,
                sceneName,
                clock,
                expectedRaidRevision,
                1,
                eligibilityPassed,
                zoneAllowed,
                generationContinuous,
                liveLocationValid,
                reconcileReason,
                Binding());
        }

        private static GuildCatalogBinding Binding()
        {
            return new GuildCatalogBinding(1, "1.0.0", "guild_raid_muster_policy_v1", CatalogHash);
        }

        private static GuildRaidMusterPolicySnapshot Policy()
        {
            return new GuildRaidMusterPolicySnapshot(
                GuildCatalogStatus.Ready,
                Binding(),
                30,
                1,
                1,
                2,
                new[]
                {
                    new RaidBossSlotDefinition(0, BossIron),
                    new RaidBossSlotDefinition(1, BossAsh),
                    new RaidBossSlotDefinition(2, BossThorn),
                    new RaidBossSlotDefinition(3, BossVeil)
                },
                ClosedTopology,
                new[]
                {
                    "public_realm_dungeon_entrance",
                    "public_realm_dungeon_cooldown",
                    "public_realm_dungeon_quest",
                    "public_realm_dungeon_reward",
                    "public_realm_dungeon_coordinate"
                },
                true,
                true);
        }

        private static RaidAuthoritySnapshot EmptyRaids()
        {
            return new RaidAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                0,
                Binding(),
                Array.Empty<RaidCallSnapshot>(),
                Array.Empty<RaidOperationReceipt>(),
                true);
        }

        private static GuildAuthoritySnapshot Membership(GuildRole officerRole = GuildRole.Officer)
        {
            return new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                1,
                Binding(),
                new[]
                {
                    new GuildSnapshot(
                        GuildAlpha,
                        RealmStonehold,
                        1,
                        GuildStatus.Active,
                        new[]
                        {
                            new GuildMemberSnapshot(
                                AccountMaster, RealmStonehold, GuildRole.Master, GuildMembershipState.Active),
                            new GuildMemberSnapshot(
                                AccountOfficer, RealmStonehold, officerRole, GuildMembershipState.Active),
                            new GuildMemberSnapshot(
                                AccountMemberA, RealmStonehold, GuildRole.Member, GuildMembershipState.Active),
                            new GuildMemberSnapshot(
                                AccountMemberB, RealmStonehold, GuildRole.Member, GuildMembershipState.Active)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }

        private static AllianceAuthoritySnapshot EmptyAlliance()
        {
            return new AllianceAuthoritySnapshot(
                AllianceAuthorityStatus.Available,
                1,
                Binding(),
                Array.Empty<AllianceSnapshot>(),
                Array.Empty<AlliancePendingRequest>(),
                Array.Empty<AllianceWarSnapshot>(),
                Array.Empty<AllianceOperationReceipt>(),
                true);
        }

        private static AllianceAuthoritySnapshot ActiveWar()
        {
            return new AllianceAuthoritySnapshot(
                AllianceAuthorityStatus.Available,
                2,
                Binding(),
                new[]
                {
                    new AllianceSnapshot(
                        "alliance_alpha_001",
                        RealmStonehold,
                        new string('c', 64),
                        2,
                        AllianceRelationState.Active,
                        GuildAlpha,
                        new[]
                        {
                            new AllianceMemberGuildSnapshot(GuildAlpha, 1),
                            new AllianceMemberGuildSnapshot(GuildBravo, 1)
                        })
                },
                Array.Empty<AlliancePendingRequest>(),
                new[]
                {
                    new AllianceWarSnapshot(
                        "war_alpha_001",
                        "alliance_alpha_001",
                        "alliance_omega_001",
                        AllianceWarState.Active,
                        ClockStart - 1000,
                        ClockStart - 100,
                        2,
                        2)
                },
                Array.Empty<AllianceOperationReceipt>(),
                true);
        }
    }
}
