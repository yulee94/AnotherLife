using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Guilds
{
    public sealed class GuildCitySeasonPlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const int MaximumReceipts = 4096;
        private const int CitiesPerRealm = 3;
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly GuildCitySeasonPolicySnapshot policy;

        public GuildCitySeasonPlanner(GuildCitySeasonPolicySnapshot policy)
        {
            this.policy = policy;
        }

        public long ResolveSeasonWeekId(long trustedClockUnixSeconds)
        {
            if (trustedClockUnixSeconds < 0)
            {
                return -1;
            }

            DateTime utc = UnixEpoch.AddSeconds(trustedClockUnixSeconds);
            int daysFromMonday = ((int)utc.DayOfWeek + 6) % 7;
            DateTime monday = utc.Date.AddDays(-daysFromMonday);
            return (long)(DateTime.SpecifyKind(monday, DateTimeKind.Utc) - UnixEpoch).TotalSeconds;
        }

        public CityBannerPresentation ResolveBannerPresentation(
            CitySeatStatus status,
            string ownerGuildId,
            GuildBannerSnapshot banner,
            string boundBannerGuildId,
            string boundBannerHash)
        {
            if (status != CitySeatStatus.Owned)
            {
                return CityBannerPresentation.RealmSymbol;
            }

            if (!IsUsableOwnerBanner(banner, ownerGuildId) ||
                !string.Equals(banner.GuildId, boundBannerGuildId ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(banner.ContentHash, boundBannerHash ?? string.Empty, StringComparison.Ordinal))
            {
                return CityBannerPresentation.SafeTextMark;
            }

            return CityBannerPresentation.GuildBanner;
        }

        public CitySeasonPlanningResult Plan(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildAuthoritySnapshot membership)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    GuildPlanningStatus.InvalidRequest,
                    "AL-CITY-REQUEST-INVALID",
                    request?.OperationId,
                    "City season identity, fields, or revisions are invalid.");
            }

            CitySeasonPlanningResult policyGate = ValidatePolicy();
            if (policyGate != null)
            {
                return policyGate;
            }

            if (!BindingEquals(request.ExpectedCatalogBinding, policy.Binding))
            {
                return Reject(
                    GuildPlanningStatus.StaleCatalog,
                    "AL-CITY-CATALOG-STALE",
                    request.OperationId,
                    "The request is not fenced to the accepted city season catalog.");
            }

            CitySeasonPlanningResult membershipGate = ValidateMembership(membership, request);
            if (membershipGate != null)
            {
                return membershipGate;
            }

            CitySeasonPlanningResult seasonGate = ValidateSeasons(seasons);
            if (seasonGate != null)
            {
                return seasonGate;
            }

            string requestFingerprint = RequestFingerprint(request);
            CitySeasonPlanningResult replay = ClassifyReplay(request, requestFingerprint, seasons.Receipts);
            if (replay != null)
            {
                return replay;
            }

            if (request.Operation == CitySeasonOperation.ResetWeek &&
                IsAlreadyReset(seasons, request.RealmId, request.SeasonWeekId))
            {
                return new CitySeasonPlanningResult(
                    GuildPlanningStatus.NoChange,
                    null,
                    null,
                    Array.Empty<GuildDiagnostic>());
            }

            if (request.Operation == CitySeasonOperation.OpenContest &&
                HasUnneutralizedPriorOwner(seasons, request.RealmId, request.SeasonWeekId))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-NEUTRALIZE-REQUIRED",
                    request.CityId,
                    "Every prior owner and perk must be neutralized before a contest can open.");
            }

            if (seasons.Revision != request.ExpectedSeasonRevision)
            {
                return Reject(
                    GuildPlanningStatus.StaleAuthority,
                    "AL-CITY-REVISION-STALE",
                    request.OperationId,
                    "The request is not fenced to the current city season revision.");
            }

            GuildSnapshot guild = membership.Guilds.FirstOrDefault(value =>
                string.Equals(value.GuildId, request.GuildId, StringComparison.Ordinal));
            if (guild == null || guild.Status != GuildStatus.Active || guild.Members == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-CITY-GUILD-MISSING",
                    request.GuildId,
                    "The Guild was not found or is not active.");
            }

            if (!string.Equals(guild.ImmutableRealmId, request.RealmId, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-CROSS-REALM",
                    request.GuildId,
                    "City season operations are same-realm only.");
            }

            switch (request.Operation)
            {
                case CitySeasonOperation.ResetWeek:
                    return PlanReset(request, seasons, guild, requestFingerprint);
                case CitySeasonOperation.OpenContest:
                    return PlanOpen(request, seasons, guild, requestFingerprint);
                case CitySeasonOperation.EnterContest:
                    return PlanEnter(request, seasons, guild, requestFingerprint);
                case CitySeasonOperation.CloseWindow:
                    return PlanClose(request, seasons, guild, requestFingerprint);
                case CitySeasonOperation.Resolve:
                    return PlanResolve(request, seasons, guild, requestFingerprint);
                case CitySeasonOperation.CommitOwnership:
                    return PlanCommit(request, seasons, guild, requestFingerprint);
                case CitySeasonOperation.Cancel:
                    return PlanCancel(request, seasons, guild, requestFingerprint);
                case CitySeasonOperation.Reconcile:
                    return PlanReconcile(request, seasons);
                default:
                    return Reject(
                        GuildPlanningStatus.InvalidRequest,
                        "AL-CITY-OPERATION-UNKNOWN",
                        request.OperationId,
                        "The city season operation is not supported.");
            }
        }

        private CitySeasonPlanningResult PlanReset(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            CitySeasonPlanningResult actorGate = RequireActiveMember(guild, request.ActorAccountId);
            if (actorGate != null)
            {
                return actorGate;
            }

            if (request.SeasonWeekId != ResolveSeasonWeekId(request.TrustedClockUnixSeconds))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-WEEK-MISMATCH",
                    request.OperationId,
                    "The season week id is not the Monday 00:00 UTC week of the trusted clock.");
            }

            RealmCitySlotDefinition slots = FindRealm(request.RealmId);
            InnerCitySeatSnapshot[] seats = slots.CityIds.Select(cityId =>
            {
                InnerCitySeatSnapshot prior = FindPriorOwnedSeat(seasons, request.RealmId, cityId, request.SeasonWeekId);
                return NeutralSeat(
                    cityId,
                    slots.RealmSymbolId,
                    prior == null ? 0 : request.SeasonWeekId,
                    CitySeasonCommitState.Committed);
            }).ToArray();

            var season = new RealmCitySeasonSnapshot(
                request.SeasonWeekId,
                request.RealmId,
                HashParts(
                    "guild_city_season_v1",
                    request.SeasonWeekId.ToString(CultureInfo.InvariantCulture),
                    request.RealmId,
                    policy.Binding.CatalogHash),
                seats,
                CitySeasonCommitState.Committed);
            return Prepare(request, seasons, requestFingerprint, UpsertSeason(seasons.Seasons, season));
        }

        private CitySeasonPlanningResult PlanOpen(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            CitySeasonPlanningResult actorGate = RequireActiveMember(guild, request.ActorAccountId);
            if (actorGate != null)
            {
                return actorGate;
            }

            CitySeasonPlanningResult cityGate = ValidateContestableCity(request);
            if (cityGate != null)
            {
                return cityGate;
            }

            if (HasUnneutralizedPriorOwner(seasons, request.RealmId, request.SeasonWeekId))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-NEUTRALIZE-REQUIRED",
                    request.CityId,
                    "Every prior owner and perk must be neutralized before a contest can open.");
            }

            RealmCitySeasonSnapshot season = FindSeason(seasons, request.RealmId, request.SeasonWeekId);
            if (season == null)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-NEUTRALIZE-REQUIRED",
                    request.CityId,
                    "The weekly reset must neutralize owners before contest commit.");
            }

            InnerCitySeatSnapshot seat = FindSeat(season, request.CityId);
            if (seat == null ||
                seat.Status != CitySeatStatus.Neutral ||
                seat.ContestPhase != CityContestPhase.Idle)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-NOT-NEUTRAL",
                    request.CityId,
                    "Only a neutralized idle city can open a contest.");
            }

            InnerCitySeatSnapshot opened = WithSeat(
                seat,
                CitySeatStatus.Contesting,
                string.Empty,
                FindRealm(request.RealmId).RealmSymbolId,
                CityBannerPresentation.RealmSymbol,
                string.Empty,
                CityContestPhase.OpenForParticipation,
                0,
                seat.NeutralizedAtUnixSeconds,
                string.Empty,
                Array.Empty<string>(),
                CitySeasonCommitState.Committed);
            return Prepare(request, seasons, requestFingerprint, ReplaceSeat(seasons.Seasons, season, opened));
        }

        private CitySeasonPlanningResult PlanEnter(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            CitySeasonPlanningResult actorGate = RequireActiveMember(guild, request.ActorAccountId);
            if (actorGate != null)
            {
                return actorGate;
            }

            RealmCitySlotDefinition slots = FindRealm(request.RealmId);
            if (slots == null ||
                !slots.CityIds.Contains(request.CityId, StringComparer.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-CROSS-REALM",
                    request.CityId,
                    "Participants must belong to the same realm as the city.");
            }

            RealmCitySeasonSnapshot season;
            InnerCitySeatSnapshot seat;
            CitySeasonPlanningResult seatGate = RequireSeat(
                seasons,
                request,
                CitySeatStatus.Contesting,
                CityContestPhase.OpenForParticipation,
                out season,
                out seat);
            if (seatGate != null)
            {
                return seatGate;
            }

            if (seat.ParticipantGuildIds.Contains(request.GuildId, StringComparer.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-ALREADY-ENTERED",
                    request.GuildId,
                    "A Guild may enter a city contest at most once per week.");
            }

            string[] participants = seat.ParticipantGuildIds
                .Concat(new[] { request.GuildId })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            InnerCitySeatSnapshot entered = WithSeat(
                seat,
                seat.Status,
                seat.OwnerGuildRef,
                seat.OwnerBannerRef,
                seat.BannerPresentation,
                seat.BannerContentHash,
                seat.ContestPhase,
                seat.WinnerLockedAtUnixSeconds,
                seat.NeutralizedAtUnixSeconds,
                seat.PerkProfileRef,
                participants,
                seat.CommitState);
            return Prepare(request, seasons, requestFingerprint, ReplaceSeat(seasons.Seasons, season, entered));
        }

        private CitySeasonPlanningResult PlanClose(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            CitySeasonPlanningResult actorGate = RequireActiveMember(guild, request.ActorAccountId);
            if (actorGate != null)
            {
                return actorGate;
            }

            RealmCitySeasonSnapshot season;
            InnerCitySeatSnapshot seat;
            CitySeasonPlanningResult seatGate = RequireSeat(
                seasons,
                request,
                CitySeatStatus.Contesting,
                CityContestPhase.OpenForParticipation,
                out season,
                out seat);
            if (seatGate != null)
            {
                return seatGate;
            }

            InnerCitySeatSnapshot closed = WithSeat(
                seat,
                seat.Status,
                seat.OwnerGuildRef,
                seat.OwnerBannerRef,
                seat.BannerPresentation,
                seat.BannerContentHash,
                CityContestPhase.WindowClosed,
                seat.WinnerLockedAtUnixSeconds,
                seat.NeutralizedAtUnixSeconds,
                seat.PerkProfileRef,
                seat.ParticipantGuildIds,
                seat.CommitState);
            return Prepare(request, seasons, requestFingerprint, ReplaceSeat(seasons.Seasons, season, closed));
        }

        private CitySeasonPlanningResult PlanResolve(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            CitySeasonPlanningResult actorGate = RequireActiveMember(guild, request.ActorAccountId);
            if (actorGate != null)
            {
                return actorGate;
            }

            RealmCitySeasonSnapshot season;
            InnerCitySeatSnapshot seat;
            CitySeasonPlanningResult seatGate = RequireSeat(
                seasons,
                request,
                CitySeatStatus.Contesting,
                CityContestPhase.WindowClosed,
                out season,
                out seat);
            if (seatGate != null)
            {
                return seatGate;
            }

            bool tie = request.TieDeclared ||
                       seat.ParticipantGuildIds.Count != 1 && string.IsNullOrEmpty(request.WinnerGuildId);
            string winner = string.Empty;
            if (!tie)
            {
                winner = string.IsNullOrEmpty(request.WinnerGuildId)
                    ? seat.ParticipantGuildIds.Single()
                    : request.WinnerGuildId;
                if (!seat.ParticipantGuildIds.Contains(winner, StringComparer.Ordinal))
                {
                    return Reject(
                        GuildPlanningStatus.Ineligible,
                        "AL-CITY-WINNER-NOT-PARTICIPANT",
                        winner,
                        "The winner must be one of the competing Guilds.");
                }
            }

            InnerCitySeatSnapshot resolved = WithSeat(
                seat,
                tie ? CitySeatStatus.Neutral : CitySeatStatus.Contesting,
                winner,
                FindRealm(request.RealmId).RealmSymbolId,
                CityBannerPresentation.RealmSymbol,
                string.Empty,
                CityContestPhase.Resolved,
                tie ? 0 : request.TrustedClockUnixSeconds,
                seat.NeutralizedAtUnixSeconds,
                string.Empty,
                seat.ParticipantGuildIds,
                CitySeasonCommitState.Committed);
            return Prepare(request, seasons, requestFingerprint, ReplaceSeat(seasons.Seasons, season, resolved));
        }

        private CitySeasonPlanningResult PlanCommit(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            RealmCitySeasonSnapshot season = FindSeason(seasons, request.RealmId, request.SeasonWeekId);
            InnerCitySeatSnapshot seat = season == null ? null : FindSeat(season, request.CityId);
            if (seat != null && seat.Status == CitySeatStatus.Owned)
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-CITY-OWNER-EXISTS",
                    request.CityId,
                    "A city may have at most one owner per week.");
            }

            CitySeasonPlanningResult actorGate = RequireActiveMember(guild, request.ActorAccountId);
            if (actorGate != null)
            {
                return actorGate;
            }

            if (season == null ||
                seat == null ||
                seat.Status != CitySeatStatus.Contesting ||
                seat.ContestPhase != CityContestPhase.Resolved ||
                string.IsNullOrEmpty(seat.OwnerGuildRef))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-NOT-RESOLVED",
                    request.CityId,
                    "Ownership can commit only after a unique resolved winner.");
            }

            if (!string.Equals(seat.OwnerGuildRef, request.GuildId, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-CITY-OWNER-EXISTS",
                    request.GuildId,
                    "Ownership cannot swap to another Guild in the same week.");
            }

            CityBannerPresentation presentation = ResolveBannerPresentation(
                CitySeatStatus.Owned,
                seat.OwnerGuildRef,
                request.Banner,
                request.Banner == null ? string.Empty : request.Banner.GuildId,
                request.Banner == null ? string.Empty : request.Banner.ContentHash);
            bool guildBanner = presentation == CityBannerPresentation.GuildBanner;
            InnerCitySeatSnapshot owned = WithSeat(
                seat,
                CitySeatStatus.Owned,
                seat.OwnerGuildRef,
                guildBanner ? request.Banner.BannerAssetId : "safe_text_mark",
                presentation,
                guildBanner ? request.Banner.ContentHash : string.Empty,
                CityContestPhase.Locked,
                seat.WinnerLockedAtUnixSeconds,
                seat.NeutralizedAtUnixSeconds,
                "city_control_perk_" + request.CityId,
                seat.ParticipantGuildIds,
                CitySeasonCommitState.Committed);
            return Prepare(request, seasons, requestFingerprint, ReplaceSeat(seasons.Seasons, season, owned));
        }

        private CitySeasonPlanningResult PlanCancel(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            GuildSnapshot guild,
            string requestFingerprint)
        {
            CitySeasonPlanningResult actorGate = RequireActiveMember(guild, request.ActorAccountId);
            if (actorGate != null)
            {
                return actorGate;
            }

            RealmCitySeasonSnapshot season = FindSeason(seasons, request.RealmId, request.SeasonWeekId);
            InnerCitySeatSnapshot seat = season == null ? null : FindSeat(season, request.CityId);
            if (seat == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-CITY-SEAT-MISSING",
                    request.CityId,
                    "The city seat was not found.");
            }

            if (seat.Status == CitySeatStatus.Owned ||
                seat.ContestPhase == CityContestPhase.Locked ||
                seat.CommitState == CitySeasonCommitState.Terminal)
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-CITY-TERMINAL",
                    request.CityId,
                    "A committed or terminal city seat cannot be cancelled.");
            }

            InnerCitySeatSnapshot cancelled = WithSeat(
                seat,
                CitySeatStatus.Neutral,
                string.Empty,
                FindRealm(request.RealmId).RealmSymbolId,
                CityBannerPresentation.RealmSymbol,
                string.Empty,
                CityContestPhase.Idle,
                0,
                request.TrustedClockUnixSeconds,
                string.Empty,
                Array.Empty<string>(),
                CitySeasonCommitState.Terminal);
            RealmCitySeasonSnapshot updatedSeason = ReplaceSeatInSeason(season, cancelled);
            var terminalSeason = new RealmCitySeasonSnapshot(
                updatedSeason.SeasonWeekId,
                updatedSeason.RealmId,
                updatedSeason.SourceHash,
                updatedSeason.Seats,
                CitySeasonCommitState.Committed);
            return Prepare(request, seasons, requestFingerprint, UpsertSeason(seasons.Seasons, terminalSeason));
        }

        private static CitySeasonPlanningResult PlanReconcile(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons)
        {
            if (request.ReconcileReason == CitySeasonReconcileReason.UnknownOutcome)
            {
                return Reject(
                    GuildPlanningStatus.CommitUncertain,
                    "AL-CITY-OUTCOME-UNKNOWN",
                    request.CityId,
                    "Unknown city season outcomes fail closed until reconciled.");
            }

            RealmCitySeasonSnapshot season = FindSeason(seasons, request.RealmId, request.SeasonWeekId);
            InnerCitySeatSnapshot seat = season == null ? null : FindSeat(season, request.CityId);
            if (seat != null &&
                (seat.Status == CitySeatStatus.Owned || seat.CommitState == CitySeasonCommitState.Terminal))
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-CITY-RESTART-DENIED",
                    request.CityId,
                    "A terminal or owned city week cannot restart.");
            }

            return Reject(
                GuildPlanningStatus.Conflict,
                "AL-CITY-RECONCILE-DENIED",
                request.OperationId,
                "City season restart is fail-closed.");
        }

        private CitySeasonPlanningResult Prepare(
            GuildCitySeasonTransitionRequest request,
            CitySeasonAuthoritySnapshot seasons,
            string requestFingerprint,
            IReadOnlyList<RealmCitySeasonSnapshot> nextSeasons)
        {
            long revision = seasons.Revision + 1;
            string planHash = HashParts(
                "guild_city_plan_v1",
                requestFingerprint,
                revision.ToString(CultureInfo.InvariantCulture),
                request.OwnerIntentHash);
            var receipt = new CitySeasonOperationReceipt(
                request.OperationId,
                request.Operation,
                requestFingerprint,
                request.RealmId,
                request.CityId,
                request.GuildId,
                request.ActorAccountId,
                request.SeasonWeekId,
                request.OwnerIntentHash,
                revision,
                planHash,
                true);
            CitySeasonOperationReceipt[] receipts = seasons.Receipts.Concat(new[] { receipt }).ToArray();
            if (receipts.Length > MaximumReceipts)
            {
                return Reject(
                    GuildPlanningStatus.Overflow,
                    "AL-CITY-RECEIPT-OVERFLOW",
                    request.OperationId,
                    "City season receipts exceeded the bounded log.");
            }

            var candidate = new CitySeasonAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                revision,
                policy.Binding,
                nextSeasons,
                receipts,
                true);
            var plan = new GuildCitySeasonTransitionPlan(
                request.Operation,
                requestFingerprint,
                seasons,
                candidate,
                receipt,
                planHash);
            return new CitySeasonPlanningResult(
                GuildPlanningStatus.Prepared,
                plan,
                null,
                Array.Empty<GuildDiagnostic>());
        }

        private CitySeasonPlanningResult ValidatePolicy()
        {
            if (policy == null ||
                policy.Status == GuildCatalogStatus.Unavailable ||
                !policy.IsComplete)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-CITY-POLICY-UNAVAILABLE",
                    string.Empty,
                    "City season policy is unavailable.");
            }

            bool slotsValid = policy.Realms != null &&
                              policy.Realms.Count > 0 &&
                              policy.Realms.All(realm =>
                                  IsStableId(realm.RealmId) &&
                                  IsStableId(realm.CapitalId) &&
                                  IsStableId(realm.RealmSymbolId) &&
                                  realm.CityIds != null &&
                                  realm.CityIds.Count == CitiesPerRealm &&
                                  realm.CityIds.SequenceEqual(new[]
                                  {
                                      realm.RealmId + "_guild_city_01",
                                      realm.RealmId + "_guild_city_02",
                                      realm.RealmId + "_guild_city_03"
                                  }, StringComparer.Ordinal) &&
                                  !realm.CityIds.Contains(realm.CapitalId, StringComparer.Ordinal));
            if (policy.Status != GuildCatalogStatus.Ready ||
                !IsValidBinding(policy.Binding) ||
                policy.CitiesPerRealm != CitiesPerRealm ||
                !slotsValid ||
                policy.CapitalsContestable ||
                !policy.NeutralizeBeforeContest ||
                policy.MintOathmarksIn25d ||
                policy.ReservedStrongholdIds == null ||
                policy.ReservedStrongholdIds.Count == 0 ||
                policy.ReservedDungeonIds == null ||
                policy.ReservedDungeonIds.Count == 0)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-CITY-POLICY-MALFORMED",
                    string.Empty,
                    "City season policy failed closed validation.");
            }

            return null;
        }

        private static CitySeasonPlanningResult ValidateMembership(
            GuildAuthoritySnapshot membership,
            GuildCitySeasonTransitionRequest request)
        {
            if (membership == null ||
                membership.Status == GuildAuthorityStatus.Unavailable ||
                !membership.IsComplete ||
                membership.Guilds == null)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-CITY-MEMBERSHIP-UNAVAILABLE",
                    request.GuildId,
                    "Guild membership authority is unavailable.");
            }

            if (membership.Revision != request.ExpectedGuildRevision)
            {
                return Reject(
                    GuildPlanningStatus.StaleGuild,
                    "AL-CITY-GUILD-STALE",
                    request.GuildId,
                    "The request is not fenced to the current Guild revision.");
            }

            return null;
        }

        private static CitySeasonPlanningResult ValidateSeasons(CitySeasonAuthoritySnapshot seasons)
        {
            if (seasons == null ||
                seasons.Status == GuildAuthorityStatus.Unavailable ||
                !seasons.IsComplete ||
                seasons.Seasons == null ||
                seasons.Receipts == null)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-CITY-AUTHORITY-UNAVAILABLE",
                    string.Empty,
                    "City season authority is unavailable.");
            }

            return null;
        }

        private CitySeasonPlanningResult ValidateContestableCity(GuildCitySeasonTransitionRequest request)
        {
            RealmCitySlotDefinition slots = FindRealm(request.RealmId);
            if (slots == null)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-CITY-REALM-UNKNOWN",
                    request.RealmId,
                    "The realm is not in the city season catalog.");
            }

            if (string.Equals(request.CityId, slots.CapitalId, StringComparison.Ordinal) ||
                policy.ReservedStrongholdIds.Contains(request.CityId, StringComparer.Ordinal) ||
                policy.ReservedDungeonIds.Contains(request.CityId, StringComparer.Ordinal) ||
                !slots.CityIds.Contains(request.CityId, StringComparer.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-CITY-NOT-CONTESTABLE",
                    request.CityId,
                    "Capitals, strongholds, and public dungeons are not contestable Guild Cities.");
            }

            return null;
        }

        private static CitySeasonPlanningResult RequireActiveMember(GuildSnapshot guild, string accountId)
        {
            GuildMemberSnapshot actor = guild.Members.FirstOrDefault(member =>
                string.Equals(member.AccountId, accountId, StringComparison.Ordinal) &&
                member.State == GuildMembershipState.Active &&
                string.Equals(member.ImmutableRealmId, guild.ImmutableRealmId, StringComparison.Ordinal));
            if (actor == null)
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-CITY-ACTOR-NOT-MEMBER",
                    accountId,
                    "The actor is not an active same-realm Guild member.");
            }

            return null;
        }

        private static CitySeasonPlanningResult RequireSeat(
            CitySeasonAuthoritySnapshot seasons,
            GuildCitySeasonTransitionRequest request,
            CitySeatStatus status,
            CityContestPhase phase,
            out RealmCitySeasonSnapshot season,
            out InnerCitySeatSnapshot seat)
        {
            season = FindSeason(seasons, request.RealmId, request.SeasonWeekId);
            seat = season == null ? null : FindSeat(season, request.CityId);
            if (season == null || seat == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-CITY-SEAT-MISSING",
                    request.CityId,
                    "The city seat was not found for this week.");
            }

            if (seat.Status != status || seat.ContestPhase != phase)
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-CITY-PHASE-INVALID",
                    request.CityId,
                    "The city seat is not in the required contest phase.");
            }

            return null;
        }

        private static CitySeasonPlanningResult ClassifyReplay(
            GuildCitySeasonTransitionRequest request,
            string requestFingerprint,
            IReadOnlyList<CitySeasonOperationReceipt> receipts)
        {
            CitySeasonOperationReceipt existing = receipts.FirstOrDefault(value =>
                string.Equals(value.OperationId, request.OperationId, StringComparison.Ordinal));
            if (existing == null)
            {
                return null;
            }

            if (string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal) &&
                existing.Operation == request.Operation)
            {
                return new CitySeasonPlanningResult(
                    GuildPlanningStatus.AlreadyCommitted,
                    null,
                    existing,
                    Array.Empty<GuildDiagnostic>());
            }

            return Reject(
                GuildPlanningStatus.Conflict,
                "AL-CITY-OPERATION-CONFLICT",
                request.OperationId,
                "The operation id was reused with a different fingerprint.");
        }

        private static bool IsAlreadyReset(
            CitySeasonAuthoritySnapshot seasons,
            string realmId,
            long seasonWeekId)
        {
            RealmCitySeasonSnapshot season = FindSeason(seasons, realmId, seasonWeekId);
            return season != null &&
                   season.CommitState == CitySeasonCommitState.Committed &&
                   season.Seats != null &&
                   season.Seats.All(seat =>
                       seat.Status == CitySeatStatus.Neutral &&
                       seat.ContestPhase == CityContestPhase.Idle);
        }

        private static bool HasUnneutralizedPriorOwner(
            CitySeasonAuthoritySnapshot seasons,
            string realmId,
            long seasonWeekId)
        {
            if (FindSeason(seasons, realmId, seasonWeekId) != null)
            {
                return false;
            }

            return seasons.Seasons.Any(season =>
                string.Equals(season.RealmId, realmId, StringComparison.Ordinal) &&
                season.SeasonWeekId < seasonWeekId &&
                season.Seats != null &&
                season.Seats.Any(seat =>
                    seat.Status == CitySeatStatus.Owned || seat.PerkProfileRef.Length > 0));
        }

        private static InnerCitySeatSnapshot FindPriorOwnedSeat(
            CitySeasonAuthoritySnapshot seasons,
            string realmId,
            string cityId,
            long seasonWeekId)
        {
            return seasons.Seasons
                .Where(season =>
                    string.Equals(season.RealmId, realmId, StringComparison.Ordinal) &&
                    season.SeasonWeekId < seasonWeekId)
                .SelectMany(season => season.Seats ?? Array.Empty<InnerCitySeatSnapshot>())
                .LastOrDefault(seat =>
                    string.Equals(seat.CityId, cityId, StringComparison.Ordinal) &&
                    (seat.Status == CitySeatStatus.Owned || seat.PerkProfileRef.Length > 0));
        }

        private RealmCitySlotDefinition FindRealm(string realmId)
        {
            return policy.Realms.FirstOrDefault(value =>
                string.Equals(value.RealmId, realmId, StringComparison.Ordinal));
        }

        private static RealmCitySeasonSnapshot FindSeason(
            CitySeasonAuthoritySnapshot seasons,
            string realmId,
            long seasonWeekId)
        {
            return seasons.Seasons.FirstOrDefault(value =>
                string.Equals(value.RealmId, realmId, StringComparison.Ordinal) &&
                value.SeasonWeekId == seasonWeekId);
        }

        private static InnerCitySeatSnapshot FindSeat(RealmCitySeasonSnapshot season, string cityId)
        {
            return season.Seats.FirstOrDefault(value =>
                string.Equals(value.CityId, cityId, StringComparison.Ordinal));
        }

        private static InnerCitySeatSnapshot NeutralSeat(
            string cityId,
            string realmSymbolId,
            long neutralizedAtUnixSeconds,
            CitySeasonCommitState commitState)
        {
            return new InnerCitySeatSnapshot(
                cityId,
                CitySeatStatus.Neutral,
                string.Empty,
                realmSymbolId,
                CityBannerPresentation.RealmSymbol,
                string.Empty,
                CityContestPhase.Idle,
                0,
                neutralizedAtUnixSeconds,
                string.Empty,
                string.Empty,
                false,
                Array.Empty<string>(),
                commitState);
        }

        private static InnerCitySeatSnapshot WithSeat(
            InnerCitySeatSnapshot seat,
            CitySeatStatus status,
            string ownerGuildRef,
            string ownerBannerRef,
            CityBannerPresentation bannerPresentation,
            string bannerContentHash,
            CityContestPhase contestPhase,
            long winnerLockedAtUnixSeconds,
            long neutralizedAtUnixSeconds,
            string perkProfileRef,
            IEnumerable<string> participantGuildIds,
            CitySeasonCommitState commitState)
        {
            return new InnerCitySeatSnapshot(
                seat.CityId,
                status,
                ownerGuildRef,
                ownerBannerRef,
                bannerPresentation,
                bannerContentHash,
                contestPhase,
                winnerLockedAtUnixSeconds,
                neutralizedAtUnixSeconds,
                seat.NextBattleWindowId,
                perkProfileRef,
                false,
                participantGuildIds,
                commitState);
        }

        private static IReadOnlyList<RealmCitySeasonSnapshot> ReplaceSeat(
            IReadOnlyList<RealmCitySeasonSnapshot> seasons,
            RealmCitySeasonSnapshot season,
            InnerCitySeatSnapshot seat)
        {
            return UpsertSeason(seasons, ReplaceSeatInSeason(season, seat));
        }

        private static RealmCitySeasonSnapshot ReplaceSeatInSeason(
            RealmCitySeasonSnapshot season,
            InnerCitySeatSnapshot seat)
        {
            InnerCitySeatSnapshot[] seats = season.Seats
                .Select(value => string.Equals(value.CityId, seat.CityId, StringComparison.Ordinal) ? seat : value)
                .ToArray();
            return new RealmCitySeasonSnapshot(
                season.SeasonWeekId,
                season.RealmId,
                season.SourceHash,
                seats,
                season.CommitState);
        }

        private static IReadOnlyList<RealmCitySeasonSnapshot> UpsertSeason(
            IReadOnlyList<RealmCitySeasonSnapshot> seasons,
            RealmCitySeasonSnapshot season)
        {
            List<RealmCitySeasonSnapshot> next = seasons
                .Where(value =>
                    !(string.Equals(value.RealmId, season.RealmId, StringComparison.Ordinal) &&
                      value.SeasonWeekId == season.SeasonWeekId))
                .ToList();
            next.Add(season);
            return next
                .OrderBy(value => value.SeasonWeekId)
                .ThenBy(value => value.RealmId, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsUsableOwnerBanner(GuildBannerSnapshot banner, string ownerGuildId)
        {
            return banner != null &&
                   banner.Moderated &&
                   banner.Approved &&
                   IsSha256(banner.ContentHash) &&
                   IsStableId(banner.BannerAssetId) &&
                   string.Equals(banner.GuildId, ownerGuildId ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool IsValidRequest(GuildCitySeasonTransitionRequest request)
        {
            return request != null &&
                   IsStableId(request.OperationId) &&
                   IsStableId(request.ActorAccountId) &&
                   IsStableId(request.GuildId) &&
                   IsStableId(request.RealmId) &&
                   IsStableId(request.CityId) &&
                   IsStableId(request.OwnerIntentHash) &&
                   (request.WinnerGuildId.Length == 0 || IsStableId(request.WinnerGuildId)) &&
                   request.SeasonWeekId >= 0 &&
                   request.TrustedClockUnixSeconds >= 0 &&
                   request.ExpectedSeasonRevision >= 0 &&
                   request.ExpectedGuildRevision >= 0 &&
                   IsValidBinding(request.ExpectedCatalogBinding);
        }

        private static string RequestFingerprint(GuildCitySeasonTransitionRequest request)
        {
            GuildBannerSnapshot banner = request.Banner;
            return HashParts(
                "guild_city_request_v1",
                ((int)request.Operation).ToString(CultureInfo.InvariantCulture),
                request.OperationId,
                request.ActorAccountId,
                request.GuildId,
                request.RealmId,
                request.CityId,
                request.SeasonWeekId.ToString(CultureInfo.InvariantCulture),
                request.TrustedClockUnixSeconds.ToString(CultureInfo.InvariantCulture),
                request.WinnerGuildId,
                request.TieDeclared ? "1" : "0",
                request.OwnerIntentHash,
                banner == null ? string.Empty : banner.GuildId,
                banner == null ? string.Empty : banner.BannerAssetId,
                banner == null ? string.Empty : banner.ContentHash,
                banner != null && banner.Moderated ? "1" : "0",
                banner != null && banner.Approved ? "1" : "0",
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

        private static CitySeasonPlanningResult Reject(
            GuildPlanningStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new CitySeasonPlanningResult(
                status,
                null,
                null,
                new[] { new GuildDiagnostic(code, subjectId ?? string.Empty, message) });
        }
    }
}
