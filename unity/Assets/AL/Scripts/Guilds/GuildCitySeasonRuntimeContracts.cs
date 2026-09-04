using System;
using System.Collections.Generic;

namespace AL.Guilds
{
    public enum GuildCitySeasonClockKind
    {
        Unspecified = 0,
        TrustedServer = 1,
        ClientUntrusted = 2
    }

    public enum GuildCitySeasonMapStatus
    {
        Awaiting = 0,
        Authoritative = 1,
        Unavailable = 2
    }

    public enum CityControlPerkConsumerKind
    {
        PublicRealmDungeon3dReward = 0,
        KingdomManagement25d = 1,
        OathmarkMint = 2,
        StrongholdCapture = 3,
        PublicRealmDungeonNonReward = 4
    }

    public enum CityControlPerkModifierStatus
    {
        Applied = 0,
        Ineligible = 1,
        Rejected = 2
    }

    public sealed class GuildCitySeasonClockTransport
    {
        private GuildCitySeasonClockTransport(GuildCitySeasonClockKind kind, long unixSeconds, string sourceId)
        {
            Kind = kind;
            UnixSeconds = unixSeconds;
            SourceId = sourceId ?? string.Empty;
        }

        public GuildCitySeasonClockKind Kind { get; }
        public long UnixSeconds { get; }
        public string SourceId { get; }

        public static GuildCitySeasonClockTransport TrustedServer(long unixSeconds, string sourceId = "trusted_server")
        {
            return new GuildCitySeasonClockTransport(GuildCitySeasonClockKind.TrustedServer, unixSeconds, sourceId);
        }

        public static GuildCitySeasonClockTransport ClientUntrusted(long unixSeconds, string sourceId = "device_clock")
        {
            return new GuildCitySeasonClockTransport(GuildCitySeasonClockKind.ClientUntrusted, unixSeconds, sourceId);
        }
    }

    [Serializable]
    public sealed class InnerCitySeatRecord
    {
        public string CityId = string.Empty;
        public int Status;
        public string OwnerGuildRef = string.Empty;
        public string OwnerBannerRef = string.Empty;
        public int BannerPresentation;
        public string BannerContentHash = string.Empty;
        public int ContestPhase;
        public long WinnerLockedAtUnixSeconds;
        public long NeutralizedAtUnixSeconds;
        public string NextBattleWindowId = string.Empty;
        public string PerkProfileRef = string.Empty;
        public bool MintsOathmarksIn25d;
        public List<string> ParticipantGuildIds = new List<string>();
        public int CommitState;
    }

    [Serializable]
    public sealed class RealmCitySeasonRecord
    {
        public long SeasonWeekId;
        public string RealmId = string.Empty;
        public string SourceHash = string.Empty;
        public List<InnerCitySeatRecord> Seats = new List<InnerCitySeatRecord>();
        public int CommitState;
    }

    [Serializable]
    public sealed class CitySeasonReceiptRecord
    {
        public string OperationId = string.Empty;
        public int Operation;
        public string RequestFingerprint = string.Empty;
        public string RealmId = string.Empty;
        public string CityId = string.Empty;
        public string GuildId = string.Empty;
        public string ActorAccountId = string.Empty;
        public long SeasonWeekId;
        public string OwnerIntentHash = string.Empty;
        public long ResultingRevision;
        public string PlanHash = string.Empty;
        public bool IsSupported;
    }

    [Serializable]
    public sealed class GuildCitySeasonPersistentState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public long Revision;
        public string CatalogId = string.Empty;
        public string ContentVersion = string.Empty;
        public string SourceRevision = string.Empty;
        public string CatalogHash = string.Empty;
        public long LastTrustedClockUnixSeconds;
        public List<RealmCitySeasonRecord> Seasons = new List<RealmCitySeasonRecord>();
        public List<CitySeasonReceiptRecord> Receipts = new List<CitySeasonReceiptRecord>();
    }

    public sealed class GuildCitySeasonNetworkEnvelope
    {
        public GuildCitySeasonNetworkEnvelope(
            GuildCitySeasonClockKind clockKind,
            string clockSourceId,
            long trustedClockUnixSeconds,
            GuildCitySeasonPersistentState state)
        {
            ClockKind = clockKind;
            ClockSourceId = clockSourceId ?? string.Empty;
            TrustedClockUnixSeconds = trustedClockUnixSeconds;
            State = state;
        }

        public GuildCitySeasonClockKind ClockKind { get; }
        public string ClockSourceId { get; }
        public long TrustedClockUnixSeconds { get; }
        public GuildCitySeasonPersistentState State { get; }
    }

    public sealed class GuildCitySeasonMapMarker
    {
        public GuildCitySeasonMapMarker(
            string cityId,
            string realmId,
            CitySeatStatus status,
            CityContestPhase contestPhase,
            string ownerGuildRef,
            string ownerBannerRef,
            CityBannerPresentation bannerPresentation,
            string bannerContentHash,
            string perkProfileRef,
            CitySeasonCommitState commitState)
        {
            CityId = cityId ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            Status = status;
            ContestPhase = contestPhase;
            OwnerGuildRef = ownerGuildRef ?? string.Empty;
            OwnerBannerRef = ownerBannerRef ?? string.Empty;
            BannerPresentation = bannerPresentation;
            BannerContentHash = bannerContentHash ?? string.Empty;
            PerkProfileRef = perkProfileRef ?? string.Empty;
            CommitState = commitState;
        }

        public string CityId { get; }
        public string RealmId { get; }
        public CitySeatStatus Status { get; }
        public CityContestPhase ContestPhase { get; }
        public string OwnerGuildRef { get; }
        public string OwnerBannerRef { get; }
        public CityBannerPresentation BannerPresentation { get; }
        public string BannerContentHash { get; }
        public string PerkProfileRef { get; }
        public CitySeasonCommitState CommitState { get; }
    }

    public sealed class GuildCitySeasonMapPresentation
    {
        public GuildCitySeasonMapPresentation(
            GuildCitySeasonMapStatus status,
            long seasonWeekId,
            long trustedClockUnixSeconds,
            string realmId,
            IReadOnlyList<GuildCitySeasonMapMarker> markers,
            string diagnosticCode)
        {
            Status = status;
            SeasonWeekId = seasonWeekId;
            TrustedClockUnixSeconds = trustedClockUnixSeconds;
            RealmId = realmId ?? string.Empty;
            Markers = markers ?? Array.Empty<GuildCitySeasonMapMarker>();
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public GuildCitySeasonMapStatus Status { get; }
        public long SeasonWeekId { get; }
        public long TrustedClockUnixSeconds { get; }
        public string RealmId { get; }
        public IReadOnlyList<GuildCitySeasonMapMarker> Markers { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class CityControlPerkModifierRequest
    {
        public CityControlPerkModifierRequest(
            CityControlPerkConsumerKind consumerKind,
            string dungeonAuthorityId,
            string ownerGuildId,
            string realmId,
            string cityId)
        {
            ConsumerKind = consumerKind;
            DungeonAuthorityId = dungeonAuthorityId ?? string.Empty;
            OwnerGuildId = ownerGuildId ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            CityId = cityId ?? string.Empty;
        }

        public CityControlPerkConsumerKind ConsumerKind { get; }
        public string DungeonAuthorityId { get; }
        public string OwnerGuildId { get; }
        public string RealmId { get; }
        public string CityId { get; }
    }

    public sealed class CityControlPerkModifierResult
    {
        public CityControlPerkModifierResult(
            CityControlPerkModifierStatus status,
            string perkProfileRef,
            bool rewardModifierApplied,
            bool mintsOathmarks,
            string diagnosticCode)
        {
            Status = status;
            PerkProfileRef = perkProfileRef ?? string.Empty;
            RewardModifierApplied = rewardModifierApplied;
            MintsOathmarks = mintsOathmarks;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public CityControlPerkModifierStatus Status { get; }
        public string PerkProfileRef { get; }
        public bool RewardModifierApplied { get; }
        public bool MintsOathmarks { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class GuildCitySeasonRuntimeResult
    {
        public GuildCitySeasonRuntimeResult(
            GuildPlanningStatus status,
            GuildCitySeasonPersistentState persisted,
            CitySeasonPlanningResult planning,
            string diagnosticCode,
            bool mutated)
        {
            Status = status;
            Persisted = persisted ?? GuildCitySeasonSaveCodec.Empty();
            Planning = planning;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Mutated = mutated;
        }

        public GuildPlanningStatus Status { get; }
        public GuildCitySeasonPersistentState Persisted { get; }
        public CitySeasonPlanningResult Planning { get; }
        public string DiagnosticCode { get; }
        public bool Mutated { get; }
    }
}
