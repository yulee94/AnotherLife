using System;
using System.Linq;
using AL.Guilds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Guilds
{
    public sealed class GuildCitySeasonRuntimeTests
    {
        private const string AccountMaster = "account_master_001";
        private const string GuildAlpha = "guild_alpha_001";
        private const string RealmStonehold = "stonehold";
        private const string CityOne = "stonehold_guild_city_01";
        private const string CityTwo = "stonehold_guild_city_02";
        private const string CityThree = "stonehold_guild_city_03";
        private const string CapitalAnvil = "capital_anvildeep";
        private const string RealmSymbol = "realm_symbol_stonehold";
        private const string BannerAlpha = "banner_guild_alpha_001";
        private const string BannerHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const long ClockInsideWeek = 1700000000;
        private const long WeekStart = 1699833600;
        private static readonly string CatalogHash = new string('c', 64);

        [Test]
        public void TrustedServerClockIsAcceptedAndClientClockIsRejected()
        {
            Assert.That(
                GuildCitySeasonRuntime.TryReadTrustedClock(
                    GuildCitySeasonClockTransport.TrustedServer(ClockInsideWeek),
                    out long trusted,
                    out string acceptedCode),
                Is.True);
            Assert.That(trusted, Is.EqualTo(ClockInsideWeek));
            Assert.That(acceptedCode, Is.EqualTo(string.Empty));

            Assert.That(
                GuildCitySeasonRuntime.TryReadTrustedClock(
                    GuildCitySeasonClockTransport.ClientUntrusted(ClockInsideWeek),
                    out long clientClock,
                    out string clientCode),
                Is.False);
            Assert.That(clientClock, Is.EqualTo(0L));
            Assert.That(clientCode, Is.EqualTo(GuildCitySeasonRuntime.ClockUntrustedCode));

            Assert.That(
                GuildCitySeasonRuntime.TryReadTrustedClock(null, out _, out string missingCode),
                Is.False);
            Assert.That(missingCode, Is.EqualTo(GuildCitySeasonRuntime.ClockMissingCode));
        }

        [Test]
        public void ApplyRejectsClientClockAndClockMismatchWithoutPersisting()
        {
            GuildCitySeasonRuntime runtime = Runtime();
            GuildCitySeasonPersistentState empty = GuildCitySeasonSaveCodec.Empty();

            GuildCitySeasonRuntimeResult client = runtime.Apply(
                Reset(AccountMaster, "operation_reset_week"),
                Membership(),
                empty,
                GuildCitySeasonClockTransport.ClientUntrusted(ClockInsideWeek));
            Assert.That(client.Status, Is.EqualTo(GuildPlanningStatus.Unauthorized));
            Assert.That(client.DiagnosticCode, Is.EqualTo(GuildCitySeasonRuntime.ClockUntrustedCode));
            Assert.That(client.Mutated, Is.False);
            Assert.That(client.Persisted.Seasons, Is.Empty);

            GuildCitySeasonRuntimeResult mismatch = runtime.Apply(
                Reset(AccountMaster, "operation_reset_week"),
                Membership(),
                empty,
                GuildCitySeasonClockTransport.TrustedServer(ClockInsideWeek + 1));
            Assert.That(mismatch.Status, Is.EqualTo(GuildPlanningStatus.InvalidRequest));
            Assert.That(mismatch.DiagnosticCode, Is.EqualTo(GuildCitySeasonRuntime.ClockMismatchCode));
            Assert.That(mismatch.Mutated, Is.False);
        }

        [Test]
        public void MapPresentsSeasonOwnershipAndBannerAndExcludesCapitalsStrongholdsAndDungeons()
        {
            GuildCitySeasonPersistentState persisted = PersistOwned();
            GuildCitySeasonMapPresentation presented = Runtime().PresentMap(persisted);

            Assert.That(presented.Status, Is.EqualTo(GuildCitySeasonMapStatus.Authoritative));
            Assert.That(presented.SeasonWeekId, Is.EqualTo(WeekStart));
            Assert.That(presented.RealmId, Is.EqualTo(RealmStonehold));
            Assert.That(presented.Markers.Select(marker => marker.CityId).ToArray(),
                Is.EqualTo(new[] { CityOne, CityTwo, CityThree }));
            Assert.That(presented.Markers.Any(marker => marker.CityId == CapitalAnvil), Is.False);
            Assert.That(presented.Markers.Any(marker => marker.CityId.Contains("castle")), Is.False);
            Assert.That(presented.Markers.Any(marker => marker.CityId.Contains("dungeon")), Is.False);

            GuildCitySeasonMapMarker owned = presented.Markers.Single(marker => marker.CityId == CityOne);
            Assert.That(owned.Status, Is.EqualTo(CitySeatStatus.Owned));
            Assert.That(owned.OwnerGuildRef, Is.EqualTo(GuildAlpha));
            Assert.That(owned.BannerPresentation, Is.EqualTo(CityBannerPresentation.GuildBanner));
            Assert.That(owned.OwnerBannerRef, Is.EqualTo(BannerAlpha));
            Assert.That(owned.PerkProfileRef, Is.EqualTo("city_control_perk_stonehold_guild_city_01"));

            GuildCitySeasonMapMarker neutral = presented.Markers.Single(marker => marker.CityId == CityTwo);
            Assert.That(neutral.Status, Is.EqualTo(CitySeatStatus.Neutral));
            Assert.That(neutral.BannerPresentation, Is.EqualTo(CityBannerPresentation.RealmSymbol));
            Assert.That(neutral.OwnerBannerRef, Is.EqualTo(RealmSymbol));
            Assert.That(neutral.OwnerGuildRef, Is.EqualTo(string.Empty));
        }

        [Test]
        public void MapPresentsSafeTextMarkWhenOwnedBannerIsInvalid()
        {
            GuildCitySeasonPersistentState persisted = PersistOwned(CityBannerPresentation.SafeTextMark, "safe_text_mark", string.Empty);
            GuildCitySeasonMapMarker owned = Runtime().PresentMap(persisted).Markers
                .Single(marker => marker.CityId == CityOne);
            Assert.That(owned.BannerPresentation, Is.EqualTo(CityBannerPresentation.SafeTextMark));
            Assert.That(owned.OwnerBannerRef, Is.EqualTo("safe_text_mark"));
            Assert.That(owned.BannerContentHash, Is.EqualTo(string.Empty));
        }

        [Test]
        public void SaveRoundTripPreservesSeasonAndSeatAndMissingStateLoadsEmpty()
        {
            CitySeasonAuthoritySnapshot snapshot = OwnedSnapshot();
            GuildCitySeasonPersistentState written = GuildCitySeasonSaveCodec.Write(snapshot, ClockInsideWeek);
            CitySeasonAuthoritySnapshot read = GuildCitySeasonSaveCodec.Read(written);
            InnerCitySeatSnapshot seat = read.Seasons.Single().Seats.Single(value => value.CityId == CityOne);

            Assert.That(written.Version, Is.EqualTo(GuildCitySeasonPersistentState.CurrentVersion));
            Assert.That(written.LastTrustedClockUnixSeconds, Is.EqualTo(ClockInsideWeek));
            Assert.That(read.Revision, Is.EqualTo(snapshot.Revision));
            Assert.That(read.Seasons.Single().SeasonWeekId, Is.EqualTo(WeekStart));
            Assert.That(seat.Status, Is.EqualTo(CitySeatStatus.Owned));
            Assert.That(seat.OwnerGuildRef, Is.EqualTo(GuildAlpha));
            Assert.That(seat.PerkProfileRef, Is.EqualTo("city_control_perk_stonehold_guild_city_01"));
            Assert.That(seat.MintsOathmarksIn25d, Is.False);

            CitySeasonAuthoritySnapshot missing = GuildCitySeasonSaveCodec.Read(null);
            Assert.That(missing.Seasons, Is.Empty);
            Assert.That(missing.Receipts, Is.Empty);
            Assert.That(missing.Revision, Is.EqualTo(0L));

            CitySeasonAuthoritySnapshot legacy = GuildCitySeasonSaveCodec.Read(new GuildCitySeasonPersistentState());
            Assert.That(legacy.Seasons, Is.Empty);
        }

        [Test]
        public void PerkAppliesOnlyAs3dDungeonRewardModifierAndNeverMintsOathmarks()
        {
            InnerCitySeatSnapshot owned = OwnedSnapshot().Seasons.Single().Seats
                .Single(seat => seat.CityId == CityOne);

            CityControlPerkModifierResult applied = GuildCitySeasonRuntime.ApplyDungeonRewardModifier(
                owned,
                new CityControlPerkModifierRequest(
                    CityControlPerkConsumerKind.PublicRealmDungeon3dReward,
                    "public_realm_dungeon_reward",
                    GuildAlpha,
                    RealmStonehold,
                    CityOne));
            Assert.That(applied.Status, Is.EqualTo(CityControlPerkModifierStatus.Applied));
            Assert.That(applied.RewardModifierApplied, Is.True);
            Assert.That(applied.PerkProfileRef, Is.EqualTo("city_control_perk_stonehold_guild_city_01"));
            Assert.That(applied.MintsOathmarks, Is.False);

            AssertRejectedPerk(
                owned,
                CityControlPerkConsumerKind.KingdomManagement25d,
                "kingdom_management_25d",
                GuildCitySeasonRuntime.Perk25dCode);
            AssertRejectedPerk(
                owned,
                CityControlPerkConsumerKind.OathmarkMint,
                "oathmark_mint",
                GuildCitySeasonRuntime.PerkOathmarkCode);
            AssertRejectedPerk(
                owned,
                CityControlPerkConsumerKind.StrongholdCapture,
                "castle_capture_stronghold",
                GuildCitySeasonRuntime.PerkStrongholdCode);

            CityControlPerkModifierResult wrongAuthority = GuildCitySeasonRuntime.ApplyDungeonRewardModifier(
                owned,
                new CityControlPerkModifierRequest(
                    CityControlPerkConsumerKind.PublicRealmDungeon3dReward,
                    "public_realm_dungeon_entrance",
                    GuildAlpha,
                    RealmStonehold,
                    CityOne));
            Assert.That(wrongAuthority.Status, Is.EqualTo(CityControlPerkModifierStatus.Rejected));
            Assert.That(wrongAuthority.RewardModifierApplied, Is.False);
            Assert.That(wrongAuthority.MintsOathmarks, Is.False);
        }

        [Test]
        public void RuntimeApplyConsumesPlannerPersistsSeasonAndReplaysIdempotently()
        {
            GuildCitySeasonRuntime runtime = Runtime();
            GuildCitySeasonClockTransport clock = GuildCitySeasonClockTransport.TrustedServer(ClockInsideWeek);

            GuildCitySeasonRuntimeResult reset = runtime.Apply(
                Reset(AccountMaster, "operation_reset_week"),
                Membership(),
                GuildCitySeasonSaveCodec.Empty(),
                clock);
            Assert.That(reset.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(reset.Mutated, Is.True);
            Assert.That(reset.Persisted.Seasons.Count, Is.EqualTo(1));
            Assert.That(reset.Persisted.Seasons[0].Seats.Count, Is.EqualTo(3));
            Assert.That(reset.Persisted.Seasons[0].Seats.TrueForAll(seat => seat.Status == (int)CitySeatStatus.Neutral), Is.True);

            GuildCitySeasonRuntimeResult replay = runtime.Apply(
                Reset(AccountMaster, "operation_reset_week"),
                Membership(),
                reset.Persisted,
                clock);
            Assert.That(replay.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));
            Assert.That(replay.Mutated, Is.False);
            Assert.That(replay.Persisted.Revision, Is.EqualTo(reset.Persisted.Revision));

            GuildCitySeasonRuntimeResult open = runtime.Apply(
                Open(AccountMaster, "operation_open_contest", reset.Persisted.Revision),
                Membership(),
                reset.Persisted,
                clock);
            Assert.That(open.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            InnerCitySeatRecord opened = open.Persisted.Seasons[0].Seats.Single(seat => seat.CityId == CityOne);
            Assert.That(opened.Status, Is.EqualTo((int)CitySeatStatus.Contesting));
            Assert.That(opened.ContestPhase, Is.EqualTo((int)CityContestPhase.OpenForParticipation));
        }

        [Test]
        public void NetworkEnvelopeRequiresTrustedClockAndCarriesPersistentState()
        {
            GuildCitySeasonPersistentState persisted = PersistOwned();
            GuildCitySeasonNetworkEnvelope trusted = new GuildCitySeasonNetworkEnvelope(
                GuildCitySeasonClockKind.TrustedServer,
                "trusted_server",
                ClockInsideWeek,
                persisted);

            Assert.That(
                GuildCitySeasonRuntime.TryAcceptNetworkEnvelope(trusted, out GuildCitySeasonPersistentState accepted, out string trustedCode),
                Is.True);
            Assert.That(trustedCode, Is.EqualTo(string.Empty));
            Assert.That(accepted.Seasons.Single().RealmId, Is.EqualTo(RealmStonehold));
            Assert.That(
                accepted.Seasons.Single().Seats.Single(seat => seat.CityId == CityOne).OwnerGuildRef,
                Is.EqualTo(GuildAlpha));

            var client = new GuildCitySeasonNetworkEnvelope(
                GuildCitySeasonClockKind.ClientUntrusted,
                "device_clock",
                ClockInsideWeek,
                persisted);
            Assert.That(
                GuildCitySeasonRuntime.TryAcceptNetworkEnvelope(client, out GuildCitySeasonPersistentState rejected, out string clientCode),
                Is.False);
            Assert.That(clientCode, Is.EqualTo(GuildCitySeasonRuntime.ClockUntrustedCode));
            Assert.That(rejected.Seasons, Is.Empty);
        }

        [Test]
        public void RuntimeDoesNotContestCapitalsOrBypassPlanner()
        {
            GuildCitySeasonRuntimeResult capital = Runtime().Apply(
                new GuildCitySeasonTransitionRequest(
                    CitySeasonOperation.OpenContest,
                    "operation_open_capital",
                    AccountMaster,
                    GuildAlpha,
                    RealmStonehold,
                    CapitalAnvil,
                    WeekStart,
                    ClockInsideWeek,
                    0,
                    1,
                    string.Empty,
                    false,
                    "owner_intent_none",
                    null,
                    CitySeasonReconcileReason.Duplicate,
                    Binding()),
                Membership(),
                GuildCitySeasonSaveCodec.Empty(),
                GuildCitySeasonClockTransport.TrustedServer(ClockInsideWeek));
            Assert.That(capital.Status, Is.EqualTo(GuildPlanningStatus.Malformed));
            Assert.That(capital.Mutated, Is.False);
            Assert.That(capital.Persisted.Seasons, Is.Empty);
        }

        private static void AssertRejectedPerk(
            InnerCitySeatSnapshot owned,
            CityControlPerkConsumerKind kind,
            string authorityId,
            string code)
        {
            CityControlPerkModifierResult result = GuildCitySeasonRuntime.ApplyDungeonRewardModifier(
                owned,
                new CityControlPerkModifierRequest(kind, authorityId, GuildAlpha, RealmStonehold, CityOne));
            Assert.That(result.Status, Is.EqualTo(CityControlPerkModifierStatus.Rejected));
            Assert.That(result.RewardModifierApplied, Is.False);
            Assert.That(result.MintsOathmarks, Is.False);
            Assert.That(result.DiagnosticCode, Is.EqualTo(code));
        }

        private static GuildCitySeasonRuntime Runtime()
        {
            return new GuildCitySeasonRuntime(Policy());
        }

        private static GuildCitySeasonPersistentState PersistOwned(
            CityBannerPresentation presentation = CityBannerPresentation.GuildBanner,
            string bannerRef = BannerAlpha,
            string bannerHash = BannerHash)
        {
            return GuildCitySeasonSaveCodec.Write(OwnedSnapshot(presentation, bannerRef, bannerHash), ClockInsideWeek);
        }

        private static CitySeasonAuthoritySnapshot OwnedSnapshot(
            CityBannerPresentation presentation = CityBannerPresentation.GuildBanner,
            string bannerRef = BannerAlpha,
            string bannerHash = BannerHash)
        {
            var owned = new InnerCitySeatSnapshot(
                CityOne,
                CitySeatStatus.Owned,
                GuildAlpha,
                bannerRef,
                presentation,
                bannerHash,
                CityContestPhase.Locked,
                ClockInsideWeek,
                WeekStart,
                string.Empty,
                "city_control_perk_stonehold_guild_city_01",
                false,
                new[] { GuildAlpha },
                CitySeasonCommitState.Committed);
            return new CitySeasonAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                6,
                Binding(),
                new[]
                {
                    new RealmCitySeasonSnapshot(
                        WeekStart,
                        RealmStonehold,
                        new string('d', 64),
                        new[] { owned, NeutralSeat(CityTwo), NeutralSeat(CityThree) },
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
                WeekStart,
                string.Empty,
                string.Empty,
                false,
                Array.Empty<string>(),
                CitySeasonCommitState.Committed);
        }

        private static GuildCitySeasonTransitionRequest Reset(string actorAccountId, string operationId)
        {
            return Request(CitySeasonOperation.ResetWeek, operationId, actorAccountId, CityOne, 0);
        }

        private static GuildCitySeasonTransitionRequest Open(string actorAccountId, string operationId, long expectedSeasonRevision)
        {
            return Request(CitySeasonOperation.OpenContest, operationId, actorAccountId, CityOne, expectedSeasonRevision);
        }

        private static GuildCitySeasonTransitionRequest Request(
            CitySeasonOperation operation,
            string operationId,
            string actorAccountId,
            string cityId,
            long expectedSeasonRevision)
        {
            return new GuildCitySeasonTransitionRequest(
                operation,
                operationId,
                actorAccountId,
                GuildAlpha,
                RealmStonehold,
                cityId,
                WeekStart,
                ClockInsideWeek,
                expectedSeasonRevision,
                1,
                string.Empty,
                false,
                "owner_intent_none",
                null,
                CitySeasonReconcileReason.Duplicate,
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
                    new RealmCitySlotDefinition(
                        "crownlands",
                        "capital_crownspire",
                        "realm_symbol_crownlands",
                        new[] { "crownlands_guild_city_01", "crownlands_guild_city_02", "crownlands_guild_city_03" }),
                    new RealmCitySlotDefinition(
                        RealmStonehold,
                        CapitalAnvil,
                        RealmSymbol,
                        new[] { CityOne, CityTwo, CityThree }),
                    new RealmCitySlotDefinition(
                        "eldergrove",
                        "capital_worldroot",
                        "realm_symbol_eldergrove",
                        new[] { "eldergrove_guild_city_01", "eldergrove_guild_city_02", "eldergrove_guild_city_03" }),
                    new RealmCitySlotDefinition(
                        "umbral",
                        "capital_veilspire",
                        "realm_symbol_umbral",
                        new[] { "umbral_guild_city_01", "umbral_guild_city_02", "umbral_guild_city_03" })
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
                                AccountMaster, RealmStonehold, GuildRole.Master, GuildMembershipState.Active)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }
    }
}
