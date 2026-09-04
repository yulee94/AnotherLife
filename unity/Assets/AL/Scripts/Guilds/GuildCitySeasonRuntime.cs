using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Guilds
{
    public static class GuildCitySeasonSaveCodec
    {
        public static GuildCitySeasonPersistentState Empty()
        {
            return new GuildCitySeasonPersistentState
            {
                Version = GuildCitySeasonPersistentState.CurrentVersion,
                Seasons = new List<RealmCitySeasonRecord>(),
                Receipts = new List<CitySeasonReceiptRecord>()
            };
        }

        public static GuildCitySeasonPersistentState Write(
            CitySeasonAuthoritySnapshot snapshot,
            long trustedClockUnixSeconds)
        {
            if (snapshot == null)
            {
                GuildCitySeasonPersistentState empty = Empty();
                empty.LastTrustedClockUnixSeconds = trustedClockUnixSeconds;
                return empty;
            }

            GuildCatalogBinding binding = snapshot.CatalogBinding;
            return new GuildCitySeasonPersistentState
            {
                Version = GuildCitySeasonPersistentState.CurrentVersion,
                Revision = snapshot.Revision,
                CatalogId = "al_guild_city_season_policy",
                ContentVersion = binding == null ? string.Empty : binding.ContentVersion,
                SourceRevision = binding == null ? string.Empty : binding.SourceRevision,
                CatalogHash = binding == null ? string.Empty : binding.CatalogHash,
                LastTrustedClockUnixSeconds = trustedClockUnixSeconds,
                Seasons = (snapshot.Seasons ?? Array.Empty<RealmCitySeasonSnapshot>())
                    .Select(WriteSeason)
                    .ToList(),
                Receipts = (snapshot.Receipts ?? Array.Empty<CitySeasonOperationReceipt>())
                    .Select(WriteReceipt)
                    .ToList()
            };
        }

        public static CitySeasonAuthoritySnapshot Read(GuildCitySeasonPersistentState state)
        {
            if (state == null || state.Version == 0)
            {
                return EmptySnapshot();
            }

            var binding = new GuildCatalogBinding(
                1,
                state.ContentVersion,
                state.SourceRevision,
                state.CatalogHash);
            RealmCitySeasonSnapshot[] seasons = (state.Seasons ?? new List<RealmCitySeasonRecord>())
                .Where(season => season != null)
                .Select(ReadSeason)
                .ToArray();
            CitySeasonOperationReceipt[] receipts = (state.Receipts ?? new List<CitySeasonReceiptRecord>())
                .Where(receipt => receipt != null)
                .Select(ReadReceipt)
                .ToArray();
            return new CitySeasonAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                state.Revision,
                binding,
                seasons,
                receipts,
                true);
        }

        private static CitySeasonAuthoritySnapshot EmptySnapshot()
        {
            return new CitySeasonAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                0,
                new GuildCatalogBinding(1, string.Empty, string.Empty, string.Empty),
                Array.Empty<RealmCitySeasonSnapshot>(),
                Array.Empty<CitySeasonOperationReceipt>(),
                true);
        }

        private static RealmCitySeasonRecord WriteSeason(RealmCitySeasonSnapshot season)
        {
            return new RealmCitySeasonRecord
            {
                SeasonWeekId = season.SeasonWeekId,
                RealmId = season.RealmId,
                SourceHash = season.SourceHash,
                Seats = (season.Seats ?? Array.Empty<InnerCitySeatSnapshot>())
                    .Select(WriteSeat)
                    .ToList(),
                CommitState = (int)season.CommitState
            };
        }

        private static InnerCitySeatRecord WriteSeat(InnerCitySeatSnapshot seat)
        {
            return new InnerCitySeatRecord
            {
                CityId = seat.CityId,
                Status = (int)seat.Status,
                OwnerGuildRef = seat.OwnerGuildRef,
                OwnerBannerRef = seat.OwnerBannerRef,
                BannerPresentation = (int)seat.BannerPresentation,
                BannerContentHash = seat.BannerContentHash,
                ContestPhase = (int)seat.ContestPhase,
                WinnerLockedAtUnixSeconds = seat.WinnerLockedAtUnixSeconds,
                NeutralizedAtUnixSeconds = seat.NeutralizedAtUnixSeconds,
                NextBattleWindowId = seat.NextBattleWindowId,
                PerkProfileRef = seat.PerkProfileRef,
                MintsOathmarksIn25d = seat.MintsOathmarksIn25d,
                ParticipantGuildIds = (seat.ParticipantGuildIds ?? Array.Empty<string>()).ToList(),
                CommitState = (int)seat.CommitState
            };
        }

        private static CitySeasonReceiptRecord WriteReceipt(CitySeasonOperationReceipt receipt)
        {
            return new CitySeasonReceiptRecord
            {
                OperationId = receipt.OperationId,
                Operation = (int)receipt.Operation,
                RequestFingerprint = receipt.RequestFingerprint,
                RealmId = receipt.RealmId,
                CityId = receipt.CityId,
                GuildId = receipt.GuildId,
                ActorAccountId = receipt.ActorAccountId,
                SeasonWeekId = receipt.SeasonWeekId,
                OwnerIntentHash = receipt.OwnerIntentHash,
                ResultingRevision = receipt.ResultingRevision,
                PlanHash = receipt.PlanHash,
                IsSupported = receipt.IsSupported
            };
        }

        private static RealmCitySeasonSnapshot ReadSeason(RealmCitySeasonRecord season)
        {
            return new RealmCitySeasonSnapshot(
                season.SeasonWeekId,
                season.RealmId,
                season.SourceHash,
                (season.Seats ?? new List<InnerCitySeatRecord>())
                    .Where(seat => seat != null)
                    .Select(ReadSeat)
                    .ToArray(),
                (CitySeasonCommitState)season.CommitState);
        }

        private static InnerCitySeatSnapshot ReadSeat(InnerCitySeatRecord seat)
        {
            return new InnerCitySeatSnapshot(
                seat.CityId,
                (CitySeatStatus)seat.Status,
                seat.OwnerGuildRef,
                seat.OwnerBannerRef,
                (CityBannerPresentation)seat.BannerPresentation,
                seat.BannerContentHash,
                (CityContestPhase)seat.ContestPhase,
                seat.WinnerLockedAtUnixSeconds,
                seat.NeutralizedAtUnixSeconds,
                seat.NextBattleWindowId,
                seat.PerkProfileRef,
                seat.MintsOathmarksIn25d,
                seat.ParticipantGuildIds ?? new List<string>(),
                (CitySeasonCommitState)seat.CommitState);
        }

        private static CitySeasonOperationReceipt ReadReceipt(CitySeasonReceiptRecord receipt)
        {
            return new CitySeasonOperationReceipt(
                receipt.OperationId,
                (CitySeasonOperation)receipt.Operation,
                receipt.RequestFingerprint,
                receipt.RealmId,
                receipt.CityId,
                receipt.GuildId,
                receipt.ActorAccountId,
                receipt.SeasonWeekId,
                receipt.OwnerIntentHash,
                receipt.ResultingRevision,
                receipt.PlanHash,
                receipt.IsSupported);
        }
    }

    public sealed class GuildCitySeasonRuntime
    {
        public const string ClockUntrustedCode = "AL-CITY-CLOCK-UNTRUSTED";
        public const string ClockMismatchCode = "AL-CITY-CLOCK-MISMATCH";
        public const string ClockMissingCode = "AL-CITY-CLOCK-MISSING";
        public const string Perk25dCode = "AL-CITY-PERK-25D";
        public const string PerkOathmarkCode = "AL-CITY-PERK-OATHMARK";
        public const string PerkStrongholdCode = "AL-CITY-PERK-STRONGHOLD";
        public const string PublicRealmDungeonRewardAuthority = "public_realm_dungeon_reward";
        public const string TrustedClockSourceId = "trusted_server";

        private readonly GuildCitySeasonPolicySnapshot policy;
        private readonly GuildCitySeasonPlanner planner;

        public GuildCitySeasonRuntime(GuildCitySeasonPolicySnapshot policy)
        {
            this.policy = policy;
            planner = new GuildCitySeasonPlanner(policy);
        }

        public static bool TryReadTrustedClock(
            GuildCitySeasonClockTransport transport,
            out long unixSeconds,
            out string diagnosticCode)
        {
            unixSeconds = 0;
            if (transport == null)
            {
                diagnosticCode = ClockMissingCode;
                return false;
            }

            if (transport.Kind != GuildCitySeasonClockKind.TrustedServer ||
                !string.Equals(transport.SourceId, TrustedClockSourceId, StringComparison.Ordinal) ||
                transport.UnixSeconds < 0)
            {
                diagnosticCode = ClockUntrustedCode;
                return false;
            }

            unixSeconds = transport.UnixSeconds;
            diagnosticCode = string.Empty;
            return true;
        }

        public GuildCitySeasonRuntimeResult Apply(
            GuildCitySeasonTransitionRequest request,
            GuildAuthoritySnapshot membership,
            GuildCitySeasonPersistentState persisted,
            GuildCitySeasonClockTransport clock)
        {
            GuildCitySeasonPersistentState current = persisted ?? GuildCitySeasonSaveCodec.Empty();
            if (!TryReadTrustedClock(clock, out long trustedClock, out string clockCode))
            {
                GuildPlanningStatus status = clockCode == ClockMissingCode
                    ? GuildPlanningStatus.InvalidRequest
                    : GuildPlanningStatus.Unauthorized;
                return new GuildCitySeasonRuntimeResult(status, current, null, clockCode, false);
            }

            if (request == null || request.TrustedClockUnixSeconds != trustedClock)
            {
                return new GuildCitySeasonRuntimeResult(
                    GuildPlanningStatus.InvalidRequest,
                    current,
                    null,
                    ClockMismatchCode,
                    false);
            }

            CitySeasonAuthoritySnapshot seasons = GuildCitySeasonSaveCodec.Read(current);
            CitySeasonPlanningResult planning = planner.Plan(request, seasons, membership);
            if (planning != null && planning.IsPrepared)
            {
                return new GuildCitySeasonRuntimeResult(
                    planning.Status,
                    GuildCitySeasonSaveCodec.Write(planning.Plan.CandidateSnapshot, trustedClock),
                    planning,
                    string.Empty,
                    true);
            }

            string diagnostic = planning == null || planning.Diagnostics.Count == 0
                ? string.Empty
                : planning.Diagnostics[0].Code;
            return new GuildCitySeasonRuntimeResult(
                planning == null ? GuildPlanningStatus.Unavailable : planning.Status,
                current,
                planning,
                diagnostic,
                false);
        }

        public GuildCitySeasonMapPresentation PresentMap(GuildCitySeasonPersistentState persisted)
        {
            CitySeasonAuthoritySnapshot snapshot = GuildCitySeasonSaveCodec.Read(persisted);
            if (snapshot.Seasons == null || snapshot.Seasons.Count == 0)
            {
                return new GuildCitySeasonMapPresentation(
                    GuildCitySeasonMapStatus.Awaiting,
                    0,
                    persisted == null ? 0 : persisted.LastTrustedClockUnixSeconds,
                    string.Empty,
                    Array.Empty<GuildCitySeasonMapMarker>(),
                    string.Empty);
            }

            HashSet<string> contestable = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> excluded = new HashSet<string>(StringComparer.Ordinal);
            if (policy != null && policy.Realms != null)
            {
                foreach (RealmCitySlotDefinition realm in policy.Realms)
                {
                    if (realm == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(realm.CapitalId))
                    {
                        excluded.Add(realm.CapitalId);
                    }

                    if (realm.CityIds == null)
                    {
                        continue;
                    }

                    foreach (string cityId in realm.CityIds)
                    {
                        if (!string.IsNullOrEmpty(cityId))
                        {
                            contestable.Add(cityId);
                        }
                    }
                }
            }

            if (policy != null)
            {
                AddExcluded(excluded, policy.ReservedStrongholdIds);
                AddExcluded(excluded, policy.ReservedDungeonIds);
            }

            var markers = new List<GuildCitySeasonMapMarker>();
            foreach (RealmCitySeasonSnapshot season in snapshot.Seasons)
            {
                if (season?.Seats == null)
                {
                    continue;
                }

                foreach (InnerCitySeatSnapshot seat in season.Seats)
                {
                    if (seat == null ||
                        excluded.Contains(seat.CityId) ||
                        !contestable.Contains(seat.CityId))
                    {
                        continue;
                    }

                    markers.Add(new GuildCitySeasonMapMarker(
                        seat.CityId,
                        season.RealmId,
                        seat.Status,
                        seat.ContestPhase,
                        seat.OwnerGuildRef,
                        seat.OwnerBannerRef,
                        seat.BannerPresentation,
                        seat.BannerContentHash,
                        seat.PerkProfileRef,
                        seat.CommitState));
                }
            }

            RealmCitySeasonSnapshot first = snapshot.Seasons[0];
            markers.Sort((left, right) => string.CompareOrdinal(left.CityId, right.CityId));
            return new GuildCitySeasonMapPresentation(
                GuildCitySeasonMapStatus.Authoritative,
                first.SeasonWeekId,
                persisted == null ? 0 : persisted.LastTrustedClockUnixSeconds,
                first.RealmId,
                markers,
                string.Empty);
        }

        public static CityControlPerkModifierResult ApplyDungeonRewardModifier(
            InnerCitySeatSnapshot seat,
            CityControlPerkModifierRequest request)
        {
            if (request == null)
            {
                return RejectPerk(CityControlPerkModifierStatus.Rejected, string.Empty);
            }

            if (request.ConsumerKind == CityControlPerkConsumerKind.KingdomManagement25d)
            {
                return RejectPerk(CityControlPerkModifierStatus.Rejected, Perk25dCode);
            }

            if (request.ConsumerKind == CityControlPerkConsumerKind.OathmarkMint)
            {
                return RejectPerk(CityControlPerkModifierStatus.Rejected, PerkOathmarkCode);
            }

            if (request.ConsumerKind == CityControlPerkConsumerKind.StrongholdCapture)
            {
                return RejectPerk(CityControlPerkModifierStatus.Rejected, PerkStrongholdCode);
            }

            string expectedPerk = "city_control_perk_" + (request.CityId ?? string.Empty);
            bool owned = seat != null &&
                seat.Status == CitySeatStatus.Owned &&
                seat.ContestPhase == CityContestPhase.Locked &&
                !seat.MintsOathmarksIn25d &&
                string.Equals(seat.OwnerGuildRef, request.OwnerGuildId, StringComparison.Ordinal) &&
                string.Equals(seat.CityId, request.CityId, StringComparison.Ordinal) &&
                string.Equals(seat.PerkProfileRef, expectedPerk, StringComparison.Ordinal);
            bool dungeonReward = request.ConsumerKind == CityControlPerkConsumerKind.PublicRealmDungeon3dReward &&
                string.Equals(request.DungeonAuthorityId, PublicRealmDungeonRewardAuthority, StringComparison.Ordinal);
            if (!owned || !dungeonReward)
            {
                return RejectPerk(CityControlPerkModifierStatus.Rejected, string.Empty);
            }

            return new CityControlPerkModifierResult(
                CityControlPerkModifierStatus.Applied,
                seat.PerkProfileRef,
                true,
                false,
                string.Empty);
        }

        public static bool TryAcceptNetworkEnvelope(
            GuildCitySeasonNetworkEnvelope envelope,
            out GuildCitySeasonPersistentState persisted,
            out string diagnosticCode)
        {
            persisted = GuildCitySeasonSaveCodec.Empty();
            if (envelope == null)
            {
                diagnosticCode = ClockMissingCode;
                return false;
            }

            var transport = envelope.ClockKind == GuildCitySeasonClockKind.TrustedServer
                ? GuildCitySeasonClockTransport.TrustedServer(envelope.TrustedClockUnixSeconds, envelope.ClockSourceId)
                : GuildCitySeasonClockTransport.ClientUntrusted(envelope.TrustedClockUnixSeconds, envelope.ClockSourceId);
            if (!TryReadTrustedClock(transport, out _, out diagnosticCode))
            {
                return false;
            }

            persisted = envelope.State ?? GuildCitySeasonSaveCodec.Empty();
            diagnosticCode = string.Empty;
            return true;
        }

        private static CityControlPerkModifierResult RejectPerk(
            CityControlPerkModifierStatus status,
            string diagnosticCode)
        {
            return new CityControlPerkModifierResult(status, string.Empty, false, false, diagnosticCode);
        }

        private static void AddExcluded(HashSet<string> excluded, IReadOnlyList<string> ids)
        {
            if (ids == null)
            {
                return;
            }

            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    excluded.Add(id);
                }
            }
        }
    }
}
