using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AL.Alliances;
using AL.Guilds;

[assembly: InternalsVisibleTo("PvpHarmfulEffectGate.Tests")]

namespace AL.Pvp
{
    public enum PvpHarmfulEffectKind
    {
        DirectHit,
        Projectile,
        AreaOfEffect,
        DamageOverTimeTick,
        Chain,
        Trap,
        PetSummon,
        Reflect,
        Splash,
        Environmental,
        CrowdControl
    }

    public enum PvpZonePolicyKind
    {
        Open,
        City,
        Beginner,
        Accordant,
        ForcedSafe,
        Unknown
    }

    public enum PvpActorLifeState
    {
        Alive,
        Dead,
        Loading,
        Disconnected,
        Unknown
    }

    public enum PvpGateStatus
    {
        Eligible,
        Rejected,
        Indeterminate
    }

    public enum PvpGateRejectReason
    {
        None,
        InvalidRequest,
        StaleRevision,
        UnknownAuthority,
        CrossRealm,
        SameActor,
        ActorNotLive,
        ForcedSafeZone,
        SameGuild,
        SameAlliance,
        ToggleOff,
        ProvenanceMismatch,
        Malformed,
        Unsupported
    }

    public enum PvpPresentationKind
    {
        Neutral,
        Hostile,
        WarHostile,
        Protected,
        Unknown
    }

    public enum PvpCatalogStatus
    {
        Ready,
        Unavailable,
        UnsupportedVersion,
        Malformed,
        Incomplete
    }

    public sealed class PvpHarmfulEffectGatePolicySnapshot
    {
        public PvpHarmfulEffectGatePolicySnapshot(
            PvpCatalogStatus status,
            GuildCatalogBinding binding,
            IEnumerable<PvpHarmfulEffectKind> revalidatedEffectKinds,
            IEnumerable<PvpZonePolicyKind> forcedSafeZones,
            AllianceWarState forceHostilityWarState,
            int warNoticeHours,
            int warActiveHours,
            bool presentationIsAuthoritative,
            bool clientAuthority,
            bool healthMutation,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            RevalidatedEffectKinds = revalidatedEffectKinds == null
                ? null
                : Array.AsReadOnly(revalidatedEffectKinds.ToArray());
            ForcedSafeZones = forcedSafeZones == null
                ? null
                : Array.AsReadOnly(forcedSafeZones.ToArray());
            ForceHostilityWarState = forceHostilityWarState;
            WarNoticeHours = warNoticeHours;
            WarActiveHours = warActiveHours;
            PresentationIsAuthoritative = presentationIsAuthoritative;
            ClientAuthority = clientAuthority;
            HealthMutation = healthMutation;
            IsComplete = isComplete;
        }

        public PvpCatalogStatus Status { get; }
        public GuildCatalogBinding Binding { get; }
        public IReadOnlyList<PvpHarmfulEffectKind> RevalidatedEffectKinds { get; }
        public IReadOnlyList<PvpZonePolicyKind> ForcedSafeZones { get; }
        public AllianceWarState ForceHostilityWarState { get; }
        public int WarNoticeHours { get; }
        public int WarActiveHours { get; }
        public bool PresentationIsAuthoritative { get; }
        public bool ClientAuthority { get; }
        public bool HealthMutation { get; }
        public bool IsComplete { get; }
    }

    public sealed class PvpActorSnapshot
    {
        public PvpActorSnapshot(
            string accountId,
            string characterId,
            long sessionGeneration,
            string immutableRealmId,
            PvpActorLifeState lifeState,
            PvpZonePolicyKind zoneKind,
            string zoneId,
            long zonePolicyRevision,
            bool pvpToggleEnabled,
            long pvpToggleRevision,
            long actorRevision)
        {
            AccountId = accountId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            SessionGeneration = sessionGeneration;
            ImmutableRealmId = immutableRealmId ?? string.Empty;
            LifeState = lifeState;
            ZoneKind = zoneKind;
            ZoneId = zoneId ?? string.Empty;
            ZonePolicyRevision = zonePolicyRevision;
            PvpToggleEnabled = pvpToggleEnabled;
            PvpToggleRevision = pvpToggleRevision;
            ActorRevision = actorRevision;
        }

        public string AccountId { get; }
        public string CharacterId { get; }
        public long SessionGeneration { get; }
        public string ImmutableRealmId { get; }
        public PvpActorLifeState LifeState { get; }
        public PvpZonePolicyKind ZoneKind { get; }
        public string ZoneId { get; }
        public long ZonePolicyRevision { get; }
        public bool PvpToggleEnabled { get; }
        public long PvpToggleRevision { get; }
        public long ActorRevision { get; }
    }

    public sealed class PvpEffectProvenance
    {
        public PvpEffectProvenance(
            string ownerAccountId,
            string ownerCharacterId,
            string sourceActionId,
            string hitIdentity,
            PvpHarmfulEffectKind effectKind)
        {
            OwnerAccountId = ownerAccountId ?? string.Empty;
            OwnerCharacterId = ownerCharacterId ?? string.Empty;
            SourceActionId = sourceActionId ?? string.Empty;
            HitIdentity = hitIdentity ?? string.Empty;
            EffectKind = effectKind;
        }

        public string OwnerAccountId { get; }
        public string OwnerCharacterId { get; }
        public string SourceActionId { get; }
        public string HitIdentity { get; }
        public PvpHarmfulEffectKind EffectKind { get; }
    }

    public sealed class PvpHarmfulEffectQuery
    {
        public PvpHarmfulEffectQuery(
            PvpActorSnapshot source,
            PvpActorSnapshot target,
            PvpEffectProvenance provenance,
            GuildAuthoritySnapshot guilds,
            AllianceAuthoritySnapshot alliances,
            long expectedGuildAuthorityRevision,
            long expectedAllianceAuthorityRevision,
            long expectedSourceToggleRevision,
            long expectedTargetToggleRevision,
            long expectedSourceZoneRevision,
            long expectedTargetZoneRevision,
            long expectedSourceActorRevision,
            long expectedTargetActorRevision,
            GuildCatalogBinding expectedCatalogBinding,
            long clockUnixSeconds)
        {
            Source = source;
            Target = target;
            Provenance = provenance;
            Guilds = guilds;
            Alliances = alliances;
            ExpectedGuildAuthorityRevision = expectedGuildAuthorityRevision;
            ExpectedAllianceAuthorityRevision = expectedAllianceAuthorityRevision;
            ExpectedSourceToggleRevision = expectedSourceToggleRevision;
            ExpectedTargetToggleRevision = expectedTargetToggleRevision;
            ExpectedSourceZoneRevision = expectedSourceZoneRevision;
            ExpectedTargetZoneRevision = expectedTargetZoneRevision;
            ExpectedSourceActorRevision = expectedSourceActorRevision;
            ExpectedTargetActorRevision = expectedTargetActorRevision;
            ExpectedCatalogBinding = expectedCatalogBinding;
            ClockUnixSeconds = clockUnixSeconds;
        }

        public PvpActorSnapshot Source { get; }
        public PvpActorSnapshot Target { get; }
        public PvpEffectProvenance Provenance { get; }
        public GuildAuthoritySnapshot Guilds { get; }
        public AllianceAuthoritySnapshot Alliances { get; }
        public long ExpectedGuildAuthorityRevision { get; }
        public long ExpectedAllianceAuthorityRevision { get; }
        public long ExpectedSourceToggleRevision { get; }
        public long ExpectedTargetToggleRevision { get; }
        public long ExpectedSourceZoneRevision { get; }
        public long ExpectedTargetZoneRevision { get; }
        public long ExpectedSourceActorRevision { get; }
        public long ExpectedTargetActorRevision { get; }
        public GuildCatalogBinding ExpectedCatalogBinding { get; }
        public long ClockUnixSeconds { get; }
    }

    public sealed class PvpHarmfulEffectGateDecision
    {
        internal PvpHarmfulEffectGateDecision(
            PvpGateStatus status,
            PvpGateRejectReason reason,
            PvpPresentationKind presentation,
            PvpHarmfulEffectKind effectKind,
            bool forcedByActiveWar,
            string code,
            string subjectId)
        {
            Status = status;
            Reason = reason;
            Presentation = presentation;
            EffectKind = effectKind;
            ForcedByActiveWar = forcedByActiveWar;
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Eligible = status == PvpGateStatus.Eligible;
        }

        public PvpGateStatus Status { get; }
        public PvpGateRejectReason Reason { get; }
        public PvpPresentationKind Presentation { get; }
        public PvpHarmfulEffectKind EffectKind { get; }
        public bool ForcedByActiveWar { get; }
        public string Code { get; }
        public string SubjectId { get; }
        public bool Eligible { get; }
        public bool MutatesHealth => false;
        public bool PresentationIsAuthoritative => false;
    }
}
