using System;
using System.Linq;
using AL.Guilds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Guilds
{
    public sealed class GuildCitySeasonPlannerTests
    {
        private const string AccountMaster = "account_master_001";
        private const string AccountMemberA = "account_member_a_001";
        private const string AccountForeign = "account_foreign_001";
        private const string GuildAlpha = "guild_alpha_001";
        private const string GuildBravo = "guild_bravo_001";
        private const string GuildCrown = "guild_crown_001";
        private const string RealmStonehold = "stonehold";
        private const string RealmCrownlands = "crownlands";
        private const string CityOne = "stonehold_guild_city_01";
        private const string CityTwo = "stonehold_guild_city_02";
        private const string CityThree = "stonehold_guild_city_03";
        private const string CapitalAnvil = "capital_anvildeep";
        private const string RealmSymbol = "realm_symbol_stonehold";
        private const string BannerAlpha = "banner_guild_alpha_001";
        private const string BannerBravo = "banner_guild_bravo_001";
        private const string BannerHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string DriftHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const long ClockInsideWeek = 1700000000;
        private const long WeekStart = 1699833600;
        private const long PreviousWeekStart = 1699228800;
        private static readonly string CatalogHash = new string('c', 64);

        [Test]
        public void WeekIdIsMondayMidnightUtcAndResetIsIdempotent()
        {
            GuildCitySeasonPlanner planner = Planner();
            Assert.That(planner.ResolveSeasonWeekId(ClockInsideWeek), Is.EqualTo(WeekStart));
            Assert.That(planner.ResolveSeasonWeekId(WeekStart), Is.EqualTo(WeekStart));
            Assert.That(planner.ResolveSeasonWeekId(WeekStart - 1), Is.EqualTo(PreviousWeekStart));

            CitySeasonPlanningResult first = planner.Plan(
                Reset(AccountMaster, "operation_reset_week"),
                EmptySeasons(),
                Membership());
            Assert.That(first.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            RealmCitySeasonSnapshot season = first.Plan.CandidateSnapshot.Seasons.Single();
            Assert.That(season.SeasonWeekId, Is.EqualTo(WeekStart));
            Assert.That(season.RealmId, Is.EqualTo(RealmStonehold));
            Assert.That(season.Seats.Count, Is.EqualTo(3));
            Assert.That(season.Seats.All(seat => seat.Status == CitySeatStatus.Neutral), Is.True);
            Assert.That(season.Seats.All(seat => seat.ContestPhase == CityContestPhase.Idle), Is.True);
            Assert.That(season.Seats.All(seat => seat.BannerPresentation == CityBannerPresentation.RealmSymbol), Is.True);
            Assert.That(season.Seats.All(seat => seat.OwnerGuildRef.Length == 0), Is.True);
            Assert.That(season.CommitState, Is.EqualTo(CitySeasonCommitState.Committed));
            Assert.That(EmptySeasons().Seasons, Is.Empty);

            CitySeasonPlanningResult replay = planner.Plan(
                Reset(AccountMaster, "operation_reset_week"),
                first.Plan.CandidateSnapshot,
                Membership());
            Assert.That(replay.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));

            CitySeasonPlanningResult again = planner.Plan(
                Reset(AccountMaster, "operation_reset_week_again"),
                first.Plan.CandidateSnapshot,
                Membership());
            Assert.That(again.Status, Is.EqualTo(GuildPlanningStatus.NoChange));
        }

        [Test]
        public void PriorOwnerAndPerkMustNeutralizeBeforeContestCommit()
        {
            GuildCitySeasonPlanner planner = Planner();
            CitySeasonAuthoritySnapshot leftover = OwnedLeftover();
            Assert.That(leftover.Seasons.Single().Seats.Single(seat => seat.CityId == CityOne).Status,
                Is.EqualTo(CitySeatStatus.Owned));
            Assert.That(leftover.Seasons.Single().Seats.Single(seat => seat.CityId == CityOne).PerkProfileRef,
                Is.EqualTo("city_control_perk_stonehold_guild_city_01"));

            CitySeasonPlanningResult blocked = planner.Plan(
                Open(AccountMaster, "operation_open_without_reset"),
                leftover,
                Membership());
            Assert.That(blocked.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));
            Assert.That(blocked.Diagnostics.Any(value => value.Code == "AL-CITY-NEUTRALIZE-REQUIRED"), Is.True);
            Assert.That(leftover.Seasons.Single().Seats.Single(seat => seat.CityId == CityOne).Status,
                Is.EqualTo(CitySeatStatus.Owned));

            CitySeasonPlanningResult reset = planner.Plan(
                Reset(AccountMaster, "operation_reset_after_owned", expectedSeasonRevision: leftover.Revision),
                leftover,
                Membership());
            Assert.That(reset.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            InnerCitySeatSnapshot neutralized = reset.Plan.CandidateSnapshot.Seasons
                .Single(value => value.SeasonWeekId == WeekStart)
                .Seats.Single(seat => seat.CityId == CityOne);
            Assert.That(neutralized.Status, Is.EqualTo(CitySeatStatus.Neutral));
            Assert.That(neutralized.OwnerGuildRef, Is.EqualTo(string.Empty));
            Assert.That(neutralized.PerkProfileRef, Is.EqualTo(string.Empty));
            Assert.That(neutralized.BannerPresentation, Is.EqualTo(CityBannerPresentation.RealmSymbol));
            Assert.That(neutralized.NeutralizedAtUnixSeconds, Is.EqualTo(WeekStart));
            Assert.That(leftover.Seasons.Single().Seats.Single(seat => seat.CityId == CityOne).Status,
                Is.EqualTo(CitySeatStatus.Owned));

            CitySeasonPlanningResult opened = planner.Plan(
                Open(AccountMaster, "operation_open_after_reset",
                    expectedSeasonRevision: reset.Plan.CandidateSnapshot.Revision),
                reset.Plan.CandidateSnapshot,
                Membership());
            Assert.That(opened.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
        }

        [Test]
        public void CapitalsAndForeignAuthoritiesAreNeverContestable()
        {
            GuildCitySeasonPlanner planner = Planner();
            CitySeasonAuthoritySnapshot reset = Apply(planner.Plan(
                Reset(AccountMaster, "operation_reset_for_ids"),
                EmptySeasons(),
                Membership()));

            CitySeasonPlanningResult capital = planner.Plan(
                Open(AccountMaster, "operation_open_capital", cityId: CapitalAnvil,
                    expectedSeasonRevision: reset.Revision),
                reset,
                Membership());
            Assert.That(capital.Status, Is.EqualTo(GuildPlanningStatus.Malformed));

            CitySeasonPlanningResult stronghold = planner.Plan(
                Open(AccountMaster, "operation_open_stronghold", cityId: "castle_capture_stronghold",
                    expectedSeasonRevision: reset.Revision),
                reset,
                Membership());
            Assert.That(stronghold.Status, Is.EqualTo(GuildPlanningStatus.Malformed));

            CitySeasonPlanningResult dungeon = planner.Plan(
                Open(AccountMaster, "operation_open_dungeon", cityId: "public_realm_dungeon_entrance",
                    expectedSeasonRevision: reset.Revision),
                reset,
                Membership());
            Assert.That(dungeon.Status, Is.EqualTo(GuildPlanningStatus.Malformed));

            CitySeasonPlanningResult slotTwo = planner.Plan(
                Open(AccountMaster, "operation_open_slot_two", cityId: CityTwo,
                    expectedSeasonRevision: reset.Revision),
                reset,
                Membership());
            Assert.That(slotTwo.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(slotTwo.Plan.CandidateSnapshot.Seasons.Single().Seats
                .Single(seat => seat.CityId == CityTwo).Status, Is.EqualTo(CitySeatStatus.Contesting));
            Assert.That(reset.Seasons.Single().Seats.Single(seat => seat.CityId == CityTwo).Status,
                Is.EqualTo(CitySeatStatus.Neutral));
        }

        [Test]
        public void SameRealmOnlyAndOneOwnerMaximumPerCityPerWeek()
        {
            GuildCitySeasonPlanner planner = Planner();
            CitySeasonAuthoritySnapshot contesting = OpenedCity(planner);

            CitySeasonPlanningResult foreign = planner.Plan(
                Enter(AccountForeign, GuildCrown, "operation_enter_foreign",
                    expectedSeasonRevision: contesting.Revision),
                contesting,
                MembershipWithCrown());
            Assert.That(foreign.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            CitySeasonPlanningResult enterAlpha = planner.Plan(
                Enter(AccountMaster, GuildAlpha, "operation_enter_alpha",
                    expectedSeasonRevision: contesting.Revision),
                contesting,
                Membership());
            Assert.That(enterAlpha.Status, Is.EqualTo(GuildPlanningStatus.Prepared));

            CitySeasonAuthoritySnapshot withAlpha = enterAlpha.Plan.CandidateSnapshot;
            CitySeasonPlanningResult duplicateEnter = planner.Plan(
                Enter(AccountMemberA, GuildAlpha, "operation_enter_alpha_again",
                    expectedSeasonRevision: withAlpha.Revision),
                withAlpha,
                Membership());
            Assert.That(duplicateEnter.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));

            CitySeasonAuthoritySnapshot owned = OwnedThisWeek(planner);
            CitySeasonPlanningResult secondOwner = planner.Plan(
                Commit(AccountMaster, GuildBravo, "operation_commit_second_owner",
                    expectedSeasonRevision: owned.Revision,
                    ownerIntentHash: "owner_intent_bravo_001"),
                owned,
                MembershipWithBravo());
            Assert.That(secondOwner.Status, Is.EqualTo(GuildPlanningStatus.Conflict));
        }

        [Test]
        public void TieAndMissingWinnerFailClosedWithNoOwner()
        {
            GuildCitySeasonPlanner planner = Planner();
            CitySeasonAuthoritySnapshot closed = ClosedWithParticipants(planner, GuildAlpha, GuildBravo);

            CitySeasonPlanningResult tie = planner.Plan(
                ResolveWinner(AccountMaster, "operation_resolve_tie", winnerGuildId: string.Empty, tieDeclared: true,
                    expectedSeasonRevision: closed.Revision),
                closed,
                MembershipWithBravo());
            Assert.That(tie.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            InnerCitySeatSnapshot tied = tie.Plan.CandidateSnapshot.Seasons.Single()
                .Seats.Single(seat => seat.CityId == CityOne);
            Assert.That(tied.Status, Is.EqualTo(CitySeatStatus.Neutral));
            Assert.That(tied.OwnerGuildRef, Is.EqualTo(string.Empty));
            Assert.That(tied.ContestPhase, Is.EqualTo(CityContestPhase.Resolved));
            Assert.That(tied.PerkProfileRef, Is.EqualTo(string.Empty));
            Assert.That(closed.Seasons.Single().Seats.Single(seat => seat.CityId == CityOne).Status,
                Is.EqualTo(CitySeatStatus.Contesting));

            CitySeasonPlanningResult unique = planner.Plan(
                ResolveWinner(AccountMaster, "operation_resolve_alpha", winnerGuildId: GuildAlpha,
                    expectedSeasonRevision: closed.Revision),
                closed,
                MembershipWithBravo());
            Assert.That(unique.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(unique.Plan.CandidateSnapshot.Seasons.Single()
                .Seats.Single(seat => seat.CityId == CityOne).OwnerGuildRef, Is.EqualTo(GuildAlpha));
        }

        [Test]
        public void BannerUsesRealmSymbolThenHashBoundGuildThenSafeFallbackNeverForeign()
        {
            GuildCitySeasonPlanner planner = Planner();
            Assert.That(
                planner.ResolveBannerPresentation(CitySeatStatus.Neutral, string.Empty, null, string.Empty, string.Empty),
                Is.EqualTo(CityBannerPresentation.RealmSymbol));

            GuildBannerSnapshot valid = new GuildBannerSnapshot(GuildAlpha, BannerAlpha, BannerHash, true, true);
            Assert.That(
                planner.ResolveBannerPresentation(CitySeatStatus.Owned, GuildAlpha, valid, GuildAlpha, BannerHash),
                Is.EqualTo(CityBannerPresentation.GuildBanner));

            GuildBannerSnapshot foreign = new GuildBannerSnapshot(GuildBravo, BannerBravo, BannerHash, true, true);
            Assert.That(
                planner.ResolveBannerPresentation(CitySeatStatus.Owned, GuildAlpha, foreign, GuildAlpha, BannerHash),
                Is.EqualTo(CityBannerPresentation.SafeTextMark));

            GuildBannerSnapshot drift = new GuildBannerSnapshot(GuildAlpha, BannerAlpha, DriftHash, true, true);
            Assert.That(
                planner.ResolveBannerPresentation(CitySeatStatus.Owned, GuildAlpha, drift, GuildAlpha, BannerHash),
                Is.EqualTo(CityBannerPresentation.SafeTextMark));

            CitySeasonAuthoritySnapshot resolved = ResolvedWinner(planner, GuildAlpha);
            CitySeasonPlanningResult owned = planner.Plan(
                Commit(AccountMaster, GuildAlpha, "operation_commit_banner",
                    expectedSeasonRevision: resolved.Revision,
                    banner: valid),
                resolved,
                Membership());
            Assert.That(owned.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            InnerCitySeatSnapshot seat = owned.Plan.CandidateSnapshot.Seasons.Single()
                .Seats.Single(value => value.CityId == CityOne);
            Assert.That(seat.Status, Is.EqualTo(CitySeatStatus.Owned));
            Assert.That(seat.BannerPresentation, Is.EqualTo(CityBannerPresentation.GuildBanner));
            Assert.That(seat.OwnerBannerRef, Is.EqualTo(BannerAlpha));
            Assert.That(seat.PerkProfileRef, Is.EqualTo("city_control_perk_stonehold_guild_city_01"));
            Assert.That(seat.MintsOathmarksIn25d, Is.False);
            Assert.That(owned.Plan.EffectDomains, Is.Empty);
        }

        [Test]
        public void DuplicateFingerprintRestartsAndUnknownOutcomeFailClosed()
        {
            GuildCitySeasonPlanner planner = Planner();
            CitySeasonAuthoritySnapshot owned = OwnedThisWeek(planner);
            CitySeasonPlanningResult duplicate = planner.Plan(
                Commit(AccountMaster, GuildAlpha, "operation_commit_alpha",
                    expectedSeasonRevision: owned.Revision - 1),
                owned,
                Membership());
            Assert.That(duplicate.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));

            CitySeasonPlanningResult conflict = planner.Plan(
                Commit(AccountMaster, GuildAlpha, "operation_commit_alpha",
                    expectedSeasonRevision: owned.Revision,
                    ownerIntentHash: "owner_intent_changed_001"),
                owned,
                Membership());
            Assert.That(conflict.Status, Is.EqualTo(GuildPlanningStatus.Conflict));

            CitySeasonPlanningResult restart = planner.Plan(
                Reconcile(AccountMaster, "operation_restart_owned", CitySeasonReconcileReason.Restart,
                    expectedSeasonRevision: owned.Revision),
                owned,
                Membership());
            Assert.That(restart.Status, Is.EqualTo(GuildPlanningStatus.Conflict));

            CitySeasonAuthoritySnapshot resolved = ResolvedWinner(planner, GuildAlpha);
            CitySeasonPlanningResult unknown = planner.Plan(
                Reconcile(AccountMaster, "operation_unknown_commit", CitySeasonReconcileReason.UnknownOutcome,
                    expectedSeasonRevision: resolved.Revision),
                resolved,
                Membership());
            Assert.That(unknown.Status, Is.EqualTo(GuildPlanningStatus.CommitUncertain));
            Assert.That(unknown.Plan, Is.Null);
            Assert.That(resolved.Seasons.Single().Seats.Single(seat => seat.CityId == CityOne).Status,
                Is.EqualTo(CitySeatStatus.Contesting));
        }

        [Test]
        public void TerminalXorKeepsPendingCommittedAndTerminalExclusive()
        {
            GuildCitySeasonPlanner planner = Planner();
            CitySeasonAuthoritySnapshot owned = OwnedThisWeek(planner);
            RealmCitySeasonSnapshot season = owned.Seasons.Single(value => value.SeasonWeekId == WeekStart);
            Assert.That(season.CommitState, Is.EqualTo(CitySeasonCommitState.Committed));
            InnerCitySeatSnapshot seat = season.Seats.Single(value => value.CityId == CityOne);
            Assert.That(seat.CommitState, Is.EqualTo(CitySeasonCommitState.Committed));
            Assert.That(seat.CommitState == CitySeasonCommitState.Pending &&
                        seat.CommitState == CitySeasonCommitState.Terminal, Is.False);

            CitySeasonPlanningResult cancelOwned = planner.Plan(
                CancelContest(AccountMaster, "operation_cancel_owned", expectedSeasonRevision: owned.Revision),
                owned,
                Membership());
            Assert.That(cancelOwned.Status, Is.EqualTo(GuildPlanningStatus.Conflict));

            CitySeasonAuthoritySnapshot contesting = OpenedCity(planner);
            CitySeasonPlanningResult cancelled = planner.Plan(
                CancelContest(AccountMaster, "operation_cancel_open", expectedSeasonRevision: contesting.Revision),
                contesting,
                Membership());
            Assert.That(cancelled.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            InnerCitySeatSnapshot cancelledSeat = cancelled.Plan.CandidateSnapshot.Seasons.Single()
                .Seats.Single(value => value.CityId == CityOne);
            Assert.That(cancelledSeat.Status, Is.EqualTo(CitySeatStatus.Neutral));
            Assert.That(cancelledSeat.CommitState, Is.EqualTo(CitySeasonCommitState.Terminal));
            Assert.That(cancelled.Plan.CandidateSnapshot.Seasons.Single().CommitState,
                Is.Not.EqualTo(CitySeasonCommitState.Pending));
        }

        [Test]
        public void ReplaySameOwnerIntentHashReturnsSameTerminal()
        {
            GuildCitySeasonPlanner planner = Planner();
            CitySeasonAuthoritySnapshot resolved = ResolvedWinner(planner, GuildAlpha);
            CitySeasonPlanningResult first = planner.Plan(
                Commit(AccountMaster, GuildAlpha, "operation_commit_intent",
                    expectedSeasonRevision: resolved.Revision,
                    ownerIntentHash: "owner_intent_alpha_replay"),
                resolved,
                Membership());
            Assert.That(first.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            string planHash = first.Plan.PlanHash;
            Assert.That(first.Plan.Receipt.OwnerIntentHash, Is.EqualTo("owner_intent_alpha_replay"));

            CitySeasonPlanningResult replay = planner.Plan(
                Commit(AccountMaster, GuildAlpha, "operation_commit_intent",
                    expectedSeasonRevision: first.Plan.CandidateSnapshot.Revision,
                    ownerIntentHash: "owner_intent_alpha_replay"),
                first.Plan.CandidateSnapshot,
                Membership());
            Assert.That(replay.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));
            Assert.That(replay.ExistingReceipt.PlanHash, Is.EqualTo(planHash));
            Assert.That(replay.ExistingReceipt.OwnerIntentHash, Is.EqualTo("owner_intent_alpha_replay"));
        }

        [Test]
        public void CatalogRejectsThreeSlotDriftAndCrossRealmSeason()
        {
            var drifted = new GuildCitySeasonPolicySnapshot(
                GuildCatalogStatus.Ready,
                Binding(),
                2,
                Policy().Realms,
                Policy().ReservedStrongholdIds,
                Policy().ReservedDungeonIds,
                false,
                true,
                false,
                true);
            var driftedPlanner = new GuildCitySeasonPlanner(drifted);
            CitySeasonPlanningResult result = driftedPlanner.Plan(
                Reset(AccountMaster, "operation_reset_drift"),
                EmptySeasons(),
                Membership());
            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Malformed));

            GuildCitySeasonPlanner planner = Planner();
            CitySeasonPlanningResult crownReset = planner.Plan(
                Reset(AccountMaster, "operation_reset_crown", realmId: RealmCrownlands),
                EmptySeasons(),
                Membership());
            Assert.That(crownReset.Status, Is.EqualTo(GuildPlanningStatus.Ineligible));
        }

        private static CitySeasonAuthoritySnapshot OpenedCity(GuildCitySeasonPlanner planner)
        {
            CitySeasonAuthoritySnapshot reset = Apply(planner.Plan(
                Reset(AccountMaster, "operation_reset_open"),
                EmptySeasons(),
                Membership()));
            return Apply(planner.Plan(
                Open(AccountMaster, "operation_open_city_one", expectedSeasonRevision: reset.Revision),
                reset,
                Membership()));
        }

        private static CitySeasonAuthoritySnapshot ClosedWithParticipants(
            GuildCitySeasonPlanner planner,
            params string[] guildIds)
        {
            CitySeasonAuthoritySnapshot current = OpenedCity(planner);
            foreach (string guildId in guildIds)
            {
                string actor = guildId == GuildAlpha ? AccountMaster : AccountMemberA;
                GuildAuthoritySnapshot membership = guildId == GuildAlpha ? Membership() : MembershipWithBravo();
                current = Apply(planner.Plan(
                    Enter(actor, guildId, "operation_enter_" + guildId,
                        expectedSeasonRevision: current.Revision),
                    current,
                    membership));
            }

            return Apply(planner.Plan(
                Close(AccountMaster, "operation_close_window", expectedSeasonRevision: current.Revision),
                current,
                MembershipWithBravo()));
        }

        private static CitySeasonAuthoritySnapshot ResolvedWinner(GuildCitySeasonPlanner planner, string winnerGuildId)
        {
            CitySeasonAuthoritySnapshot closed = ClosedWithParticipants(planner, winnerGuildId);
            return Apply(planner.Plan(
                ResolveWinner(AccountMaster, "operation_resolve_" + winnerGuildId, winnerGuildId,
                    expectedSeasonRevision: closed.Revision),
                closed,
                Membership()));
        }

        private static CitySeasonAuthoritySnapshot OwnedThisWeek(GuildCitySeasonPlanner planner)
        {
            CitySeasonAuthoritySnapshot resolved = ResolvedWinner(planner, GuildAlpha);
            return Apply(planner.Plan(
                Commit(AccountMaster, GuildAlpha, "operation_commit_alpha",
                    expectedSeasonRevision: resolved.Revision),
                resolved,
                Membership()));
        }

        private static CitySeasonAuthoritySnapshot Apply(CitySeasonPlanningResult result)
        {
            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared), result.Diagnostics.FirstOrDefault()?.Code);
            return result.Plan.CandidateSnapshot;
        }

        private static GuildCitySeasonPlanner Planner()
        {
            return new GuildCitySeasonPlanner(Policy());
        }

        private static GuildCitySeasonTransitionRequest Reset(
            string actorAccountId,
            string operationId,
            string realmId = RealmStonehold,
            long expectedSeasonRevision = 0)
        {
            return Request(
                CitySeasonOperation.ResetWeek,
                operationId,
                actorAccountId,
                GuildAlpha,
                realmId,
                CityOne,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                string.Empty,
                false,
                "owner_intent_none",
                null,
                CitySeasonReconcileReason.Duplicate);
        }

        private static GuildCitySeasonTransitionRequest Open(
            string actorAccountId,
            string operationId,
            string cityId = CityOne,
            long expectedSeasonRevision = 0)
        {
            return Request(
                CitySeasonOperation.OpenContest,
                operationId,
                actorAccountId,
                GuildAlpha,
                RealmStonehold,
                cityId,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                string.Empty,
                false,
                "owner_intent_none",
                null,
                CitySeasonReconcileReason.Duplicate);
        }

        private static GuildCitySeasonTransitionRequest Enter(
            string actorAccountId,
            string guildId,
            string operationId,
            long expectedSeasonRevision = 0)
        {
            return Request(
                CitySeasonOperation.EnterContest,
                operationId,
                actorAccountId,
                guildId,
                guildId == GuildCrown ? RealmCrownlands : RealmStonehold,
                CityOne,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                string.Empty,
                false,
                "owner_intent_none",
                null,
                CitySeasonReconcileReason.Duplicate);
        }

        private static GuildCitySeasonTransitionRequest Close(
            string actorAccountId,
            string operationId,
            long expectedSeasonRevision = 0)
        {
            return Request(
                CitySeasonOperation.CloseWindow,
                operationId,
                actorAccountId,
                GuildAlpha,
                RealmStonehold,
                CityOne,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                string.Empty,
                false,
                "owner_intent_none",
                null,
                CitySeasonReconcileReason.Duplicate);
        }

        private static GuildCitySeasonTransitionRequest ResolveWinner(
            string actorAccountId,
            string operationId,
            string winnerGuildId,
            bool tieDeclared = false,
            long expectedSeasonRevision = 0)
        {
            return Request(
                CitySeasonOperation.Resolve,
                operationId,
                actorAccountId,
                GuildAlpha,
                RealmStonehold,
                CityOne,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                winnerGuildId,
                tieDeclared,
                "owner_intent_none",
                null,
                CitySeasonReconcileReason.Duplicate);
        }

        private static GuildCitySeasonTransitionRequest Commit(
            string actorAccountId,
            string guildId,
            string operationId,
            long expectedSeasonRevision = 0,
            string ownerIntentHash = "owner_intent_alpha_001",
            GuildBannerSnapshot banner = null)
        {
            return Request(
                CitySeasonOperation.CommitOwnership,
                operationId,
                actorAccountId,
                guildId,
                RealmStonehold,
                CityOne,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                guildId,
                false,
                ownerIntentHash,
                banner ?? new GuildBannerSnapshot(guildId, BannerAlpha, BannerHash, true, true),
                CitySeasonReconcileReason.Duplicate);
        }

        private static GuildCitySeasonTransitionRequest CancelContest(
            string actorAccountId,
            string operationId,
            long expectedSeasonRevision = 0)
        {
            return Request(
                CitySeasonOperation.Cancel,
                operationId,
                actorAccountId,
                GuildAlpha,
                RealmStonehold,
                CityOne,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                string.Empty,
                false,
                "owner_intent_none",
                null,
                CitySeasonReconcileReason.Duplicate);
        }

        private static GuildCitySeasonTransitionRequest Reconcile(
            string actorAccountId,
            string operationId,
            CitySeasonReconcileReason reason,
            long expectedSeasonRevision = 0)
        {
            return Request(
                CitySeasonOperation.Reconcile,
                operationId,
                actorAccountId,
                GuildAlpha,
                RealmStonehold,
                CityOne,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                GuildAlpha,
                false,
                "owner_intent_alpha_001",
                new GuildBannerSnapshot(GuildAlpha, BannerAlpha, BannerHash, true, true),
                reason);
        }

        private static GuildCitySeasonTransitionRequest Request(
            CitySeasonOperation operation,
            string operationId,
            string actorAccountId,
            string guildId,
            string realmId,
            string cityId,
            long seasonWeekId,
            long trustedClockUnixSeconds,
            long expectedSeasonRevision,
            string winnerGuildId,
            bool tieDeclared,
            string ownerIntentHash,
            GuildBannerSnapshot banner,
            CitySeasonReconcileReason reconcileReason)
        {
            return new GuildCitySeasonTransitionRequest(
                operation,
                operationId,
                actorAccountId,
                guildId,
                realmId,
                cityId,
                seasonWeekId,
                trustedClockUnixSeconds,
                expectedSeasonRevision,
                1,
                winnerGuildId,
                tieDeclared,
                ownerIntentHash,
                banner,
                reconcileReason,
                Binding());
        }

        private static GuildCatalogBinding Binding()
        {
            return new GuildCatalogBinding(1, "1.0.0", "guild_city_season_policy_v1", CatalogHash);
        }

        private static GuildCitySeasonPolicySnapshot Policy()
        {
            return new GuildCitySeasonPolicySnapshot(
                GuildCatalogStatus.Ready,
                Binding(),
                3,
                new[]
                {
                    RealmSlots("crownlands", "capital_crownspire", "realm_symbol_crownlands"),
                    RealmSlots("stonehold", "capital_anvildeep", "realm_symbol_stonehold"),
                    RealmSlots("eldergrove", "capital_worldroot", "realm_symbol_eldergrove"),
                    RealmSlots("umbral", "capital_veilspire", "realm_symbol_umbral")
                },
                new[] { "castle_capture_stronghold" },
                new[]
                {
                    "public_realm_dungeon_entrance",
                    "public_realm_dungeon_cooldown",
                    "public_realm_dungeon_quest",
                    "public_realm_dungeon_reward",
                    "public_realm_dungeon_coordinate"
                },
                false,
                true,
                false,
                true);
        }

        private static RealmCitySlotDefinition RealmSlots(string realmId, string capitalId, string realmSymbolId)
        {
            return new RealmCitySlotDefinition(
                realmId,
                capitalId,
                realmSymbolId,
                new[]
                {
                    realmId + "_guild_city_01",
                    realmId + "_guild_city_02",
                    realmId + "_guild_city_03"
                });
        }

        private static CitySeasonAuthoritySnapshot EmptySeasons()
        {
            return new CitySeasonAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                0,
                Binding(),
                Array.Empty<RealmCitySeasonSnapshot>(),
                Array.Empty<CitySeasonOperationReceipt>(),
                true);
        }

        private static CitySeasonAuthoritySnapshot OwnedLeftover()
        {
            var owned = new InnerCitySeatSnapshot(
                CityOne,
                CitySeatStatus.Owned,
                GuildAlpha,
                BannerAlpha,
                CityBannerPresentation.GuildBanner,
                BannerHash,
                CityContestPhase.Locked,
                PreviousWeekStart,
                0,
                string.Empty,
                "city_control_perk_stonehold_guild_city_01",
                false,
                Array.Empty<string>(),
                CitySeasonCommitState.Committed);
            var idleTwo = NeutralSeat(CityTwo);
            var idleThree = NeutralSeat(CityThree);
            return new CitySeasonAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                4,
                Binding(),
                new[]
                {
                    new RealmCitySeasonSnapshot(
                        PreviousWeekStart,
                        RealmStonehold,
                        new string('d', 64),
                        new[] { owned, idleTwo, idleThree },
                        CitySeasonCommitState.Committed)
                },
                Array.Empty<CitySeasonOperationReceipt>(),
                true);
        }

        private static InnerCitySeatSnapshot NeutralSeat(string cityId)
        {
            return new InnerCitySeatSnapshot(
                cityId,
                CitySeatStatus.Neutral,
                string.Empty,
                RealmSymbol,
                CityBannerPresentation.RealmSymbol,
                string.Empty,
                CityContestPhase.Idle,
                0,
                0,
                string.Empty,
                string.Empty,
                false,
                Array.Empty<string>(),
                CitySeasonCommitState.Committed);
        }

        private static GuildAuthoritySnapshot Membership()
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
                                AccountMemberA, RealmStonehold, GuildRole.Member, GuildMembershipState.Active)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }

        private static GuildAuthoritySnapshot MembershipWithBravo()
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
                                AccountMemberA, RealmStonehold, GuildRole.Member, GuildMembershipState.Active)
                        }),
                    new GuildSnapshot(
                        GuildBravo,
                        RealmStonehold,
                        1,
                        GuildStatus.Active,
                        new[]
                        {
                            new GuildMemberSnapshot(
                                AccountMemberA, RealmStonehold, GuildRole.Master, GuildMembershipState.Active)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }

        private static GuildAuthoritySnapshot MembershipWithCrown()
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
                                AccountMaster, RealmStonehold, GuildRole.Master, GuildMembershipState.Active)
                        }),
                    new GuildSnapshot(
                        GuildCrown,
                        RealmCrownlands,
                        1,
                        GuildStatus.Active,
                        new[]
                        {
                            new GuildMemberSnapshot(
                                AccountForeign, RealmCrownlands, GuildRole.Master, GuildMembershipState.Active)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }
    }
}
