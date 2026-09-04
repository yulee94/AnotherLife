using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Guilds
{
    public enum CitySeatStatus
    {
        Neutral,
        Contesting,
        Owned,
        Neutralizing,
        ResettingPending,
        Lockout
    }

    public enum CityContestPhase
    {
        Idle,
        OpenForParticipation,
        WindowClosed,
        Resolved,
        Locked
    }

    public enum CityBannerPresentation
    {
        RealmSymbol,
        GuildBanner,
        SafeTextMark
    }

    public enum CitySeasonCommitState
    {
        Pending,
        Committed,
        Terminal
    }

    public enum CitySeasonOperation
    {
        ResetWeek,
        OpenContest,
        EnterContest,
        CloseWindow,
        Resolve,
        CommitOwnership,
        Cancel,
        Reconcile
    }

    public enum CitySeasonReconcileReason
    {
        Duplicate,
        Restart,
        UnknownOutcome
    }

    public sealed class RealmCitySlotDefinition
    {
        public RealmCitySlotDefinition(
            string realmId,
            string capitalId,
            string realmSymbolId,
            IEnumerable<string> cityIds)
        {
            RealmId = realmId ?? string.Empty;
            CapitalId = capitalId ?? string.Empty;
            RealmSymbolId = realmSymbolId ?? string.Empty;
            CityIds = cityIds == null ? null : Array.AsReadOnly(cityIds.ToArray());
        }

        public string RealmId { get; }
        public string CapitalId { get; }
        public string RealmSymbolId { get; }
        public IReadOnlyList<string> CityIds { get; }
    }

    public sealed class GuildCitySeasonPolicySnapshot
    {
        public GuildCitySeasonPolicySnapshot(
            GuildCatalogStatus status,
            GuildCatalogBinding binding,
            int citiesPerRealm,
            IEnumerable<RealmCitySlotDefinition> realms,
            IEnumerable<string> reservedStrongholdIds,
            IEnumerable<string> reservedDungeonIds,
            bool capitalsContestable,
            bool neutralizeBeforeContest,
            bool mintOathmarksIn25d,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            CitiesPerRealm = citiesPerRealm;
            Realms = realms == null ? null : Array.AsReadOnly(realms.ToArray());
            ReservedStrongholdIds = reservedStrongholdIds == null
                ? null
                : Array.AsReadOnly(reservedStrongholdIds.ToArray());
            ReservedDungeonIds = reservedDungeonIds == null
                ? null
                : Array.AsReadOnly(reservedDungeonIds.ToArray());
            CapitalsContestable = capitalsContestable;
            NeutralizeBeforeContest = neutralizeBeforeContest;
            MintOathmarksIn25d = mintOathmarksIn25d;
            IsComplete = isComplete;
        }

        public GuildCatalogStatus Status { get; }
        public GuildCatalogBinding Binding { get; }
        public int CitiesPerRealm { get; }
        public IReadOnlyList<RealmCitySlotDefinition> Realms { get; }
        public IReadOnlyList<string> ReservedStrongholdIds { get; }
        public IReadOnlyList<string> ReservedDungeonIds { get; }
        public bool CapitalsContestable { get; }
        public bool NeutralizeBeforeContest { get; }
        public bool MintOathmarksIn25d { get; }
        public bool IsComplete { get; }
    }

    public sealed class GuildBannerSnapshot
    {
        public GuildBannerSnapshot(
            string guildId,
            string bannerAssetId,
            string contentHash,
            bool moderated,
            bool approved)
        {
            GuildId = guildId ?? string.Empty;
            BannerAssetId = bannerAssetId ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            Moderated = moderated;
            Approved = approved;
        }

        public string GuildId { get; }
        public string BannerAssetId { get; }
        public string ContentHash { get; }
        public bool Moderated { get; }
        public bool Approved { get; }
    }

    public sealed class InnerCitySeatSnapshot
    {
        public InnerCitySeatSnapshot(
            string cityId,
            CitySeatStatus status,
            string ownerGuildRef,
            string ownerBannerRef,
            CityBannerPresentation bannerPresentation,
            string bannerContentHash,
            CityContestPhase contestPhase,
            long winnerLockedAtUnixSeconds,
            long neutralizedAtUnixSeconds,
            string nextBattleWindowId,
            string perkProfileRef,
            bool mintsOathmarksIn25d,
            IEnumerable<string> participantGuildIds,
            CitySeasonCommitState commitState)
        {
            CityId = cityId ?? string.Empty;
            Status = status;
            OwnerGuildRef = ownerGuildRef ?? string.Empty;
            OwnerBannerRef = ownerBannerRef ?? string.Empty;
            BannerPresentation = bannerPresentation;
            BannerContentHash = bannerContentHash ?? string.Empty;
            ContestPhase = contestPhase;
            WinnerLockedAtUnixSeconds = winnerLockedAtUnixSeconds;
            NeutralizedAtUnixSeconds = neutralizedAtUnixSeconds;
            NextBattleWindowId = nextBattleWindowId ?? string.Empty;
            PerkProfileRef = perkProfileRef ?? string.Empty;
            MintsOathmarksIn25d = mintsOathmarksIn25d;
            ParticipantGuildIds = participantGuildIds == null
                ? null
                : Array.AsReadOnly(participantGuildIds.ToArray());
            CommitState = commitState;
        }

        public string CityId { get; }
        public CitySeatStatus Status { get; }
        public string OwnerGuildRef { get; }
        public string OwnerBannerRef { get; }
        public CityBannerPresentation BannerPresentation { get; }
        public string BannerContentHash { get; }
        public CityContestPhase ContestPhase { get; }
        public long WinnerLockedAtUnixSeconds { get; }
        public long NeutralizedAtUnixSeconds { get; }
        public string NextBattleWindowId { get; }
        public string PerkProfileRef { get; }
        public bool MintsOathmarksIn25d { get; }
        public IReadOnlyList<string> ParticipantGuildIds { get; }
        public CitySeasonCommitState CommitState { get; }
    }

    public sealed class RealmCitySeasonSnapshot
    {
        public RealmCitySeasonSnapshot(
            long seasonWeekId,
            string realmId,
            string sourceHash,
            IEnumerable<InnerCitySeatSnapshot> seats,
            CitySeasonCommitState commitState)
        {
            SeasonWeekId = seasonWeekId;
            RealmId = realmId ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
            Seats = seats == null ? null : Array.AsReadOnly(seats.ToArray());
            CommitState = commitState;
        }

        public long SeasonWeekId { get; }
        public string RealmId { get; }
        public string SourceHash { get; }
        public IReadOnlyList<InnerCitySeatSnapshot> Seats { get; }
        public CitySeasonCommitState CommitState { get; }
    }

    public sealed class CitySeasonOperationReceipt
    {
        public CitySeasonOperationReceipt(
            string operationId,
            CitySeasonOperation operation,
            string requestFingerprint,
            string realmId,
            string cityId,
            string guildId,
            string actorAccountId,
            long seasonWeekId,
            string ownerIntentHash,
            long resultingRevision,
            string planHash,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            CityId = cityId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            SeasonWeekId = seasonWeekId;
            OwnerIntentHash = ownerIntentHash ?? string.Empty;
            ResultingRevision = resultingRevision;
            PlanHash = planHash ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public CitySeasonOperation Operation { get; }
        public string RequestFingerprint { get; }
        public string RealmId { get; }
        public string CityId { get; }
        public string GuildId { get; }
        public string ActorAccountId { get; }
        public long SeasonWeekId { get; }
        public string OwnerIntentHash { get; }
        public long ResultingRevision { get; }
        public string PlanHash { get; }
        public bool IsSupported { get; }
    }

    public sealed class CitySeasonAuthoritySnapshot
    {
        public CitySeasonAuthoritySnapshot(
            GuildAuthorityStatus status,
            long revision,
            GuildCatalogBinding catalogBinding,
            IEnumerable<RealmCitySeasonSnapshot> seasons,
            IEnumerable<CitySeasonOperationReceipt> receipts,
            bool isComplete)
        {
            Status = status;
            Revision = revision;
            CatalogBinding = catalogBinding;
            Seasons = seasons == null ? null : Array.AsReadOnly(seasons.ToArray());
            Receipts = receipts == null ? null : Array.AsReadOnly(receipts.ToArray());
            IsComplete = isComplete;
        }

        public GuildAuthorityStatus Status { get; }
        public long Revision { get; }
        public GuildCatalogBinding CatalogBinding { get; }
        public IReadOnlyList<RealmCitySeasonSnapshot> Seasons { get; }
        public IReadOnlyList<CitySeasonOperationReceipt> Receipts { get; }
        public bool IsComplete { get; }
    }

    public sealed class GuildCitySeasonTransitionRequest
    {
        public GuildCitySeasonTransitionRequest(
            CitySeasonOperation operation,
            string operationId,
            string actorAccountId,
            string guildId,
            string realmId,
            string cityId,
            long seasonWeekId,
            long trustedClockUnixSeconds,
            long expectedSeasonRevision,
            long expectedGuildRevision,
            string winnerGuildId,
            bool tieDeclared,
            string ownerIntentHash,
            GuildBannerSnapshot banner,
            CitySeasonReconcileReason reconcileReason,
            GuildCatalogBinding expectedCatalogBinding)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            CityId = cityId ?? string.Empty;
            SeasonWeekId = seasonWeekId;
            TrustedClockUnixSeconds = trustedClockUnixSeconds;
            ExpectedSeasonRevision = expectedSeasonRevision;
            ExpectedGuildRevision = expectedGuildRevision;
            WinnerGuildId = winnerGuildId ?? string.Empty;
            TieDeclared = tieDeclared;
            OwnerIntentHash = ownerIntentHash ?? string.Empty;
            Banner = banner;
            ReconcileReason = reconcileReason;
            ExpectedCatalogBinding = expectedCatalogBinding;
        }

        public CitySeasonOperation Operation { get; }
        public string OperationId { get; }
        public string ActorAccountId { get; }
        public string GuildId { get; }
        public string RealmId { get; }
        public string CityId { get; }
        public long SeasonWeekId { get; }
        public long TrustedClockUnixSeconds { get; }
        public long ExpectedSeasonRevision { get; }
        public long ExpectedGuildRevision { get; }
        public string WinnerGuildId { get; }
        public bool TieDeclared { get; }
        public string OwnerIntentHash { get; }
        public GuildBannerSnapshot Banner { get; }
        public CitySeasonReconcileReason ReconcileReason { get; }
        public GuildCatalogBinding ExpectedCatalogBinding { get; }
    }

    public sealed class GuildCitySeasonTransitionPlan
    {
        internal GuildCitySeasonTransitionPlan(
            CitySeasonOperation operation,
            string requestFingerprint,
            CitySeasonAuthoritySnapshot expectedSnapshot,
            CitySeasonAuthoritySnapshot candidateSnapshot,
            CitySeasonOperationReceipt receipt,
            string planHash)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            ExpectedSnapshot = expectedSnapshot;
            CandidateSnapshot = candidateSnapshot;
            Receipt = receipt;
            PlanHash = planHash ?? string.Empty;
            EffectDomains = Array.AsReadOnly(Array.Empty<GuildEffectDomain>());
        }

        public CitySeasonOperation Operation { get; }
        public string RequestFingerprint { get; }
        public CitySeasonAuthoritySnapshot ExpectedSnapshot { get; }
        public CitySeasonAuthoritySnapshot CandidateSnapshot { get; }
        public CitySeasonOperationReceipt Receipt { get; }
        public string PlanHash { get; }
        public IReadOnlyList<GuildEffectDomain> EffectDomains { get; }
    }

    public sealed class CitySeasonPlanningResult
    {
        internal CitySeasonPlanningResult(
            GuildPlanningStatus status,
            GuildCitySeasonTransitionPlan plan,
            CitySeasonOperationReceipt existingReceipt,
            IEnumerable<GuildDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingReceipt = existingReceipt;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<GuildDiagnostic>())
                .OrderBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.SubjectId, StringComparer.Ordinal)
                .ToArray());
        }

        public GuildPlanningStatus Status { get; }
        public GuildCitySeasonTransitionPlan Plan { get; }
        public CitySeasonOperationReceipt ExistingReceipt { get; }
        public IReadOnlyList<GuildDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == GuildPlanningStatus.Prepared && Plan != null;
    }
}
