using System;
using System.Collections.Generic;
using AL.Alliances;
using AL.Guilds;

namespace AL.Pvp
{
    public sealed class PvpZonePresenceRecord
    {
        public PvpZonePresenceRecord(string zoneId, string zoneKind, string protection)
        {
            ZoneId = zoneId ?? string.Empty;
            ZoneKind = zoneKind ?? string.Empty;
            Protection = protection ?? string.Empty;
        }

        public string ZoneId { get; }
        public string ZoneKind { get; }
        public string Protection { get; }
    }

    public sealed class PvpZonePresenceCatalog
    {
        private readonly Dictionary<string, PvpZonePolicyKind> kinds;

        private PvpZonePresenceCatalog(Dictionary<string, PvpZonePolicyKind> kinds)
        {
            this.kinds = kinds;
        }

        public static PvpZonePresenceCatalog FromRecords(IEnumerable<PvpZonePresenceRecord> records)
        {
            var kinds = new Dictionary<string, PvpZonePolicyKind>(StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            if (records == null)
            {
                return new PvpZonePresenceCatalog(kinds);
            }

            foreach (PvpZonePresenceRecord record in records)
            {
                if (record == null ||
                    string.IsNullOrEmpty(record.ZoneId) ||
                    !TryMapKind(record.ZoneKind, out PvpZonePolicyKind kind))
                {
                    continue;
                }

                if (ambiguous.Contains(record.ZoneId))
                {
                    continue;
                }

                if (kinds.ContainsKey(record.ZoneId))
                {
                    kinds.Remove(record.ZoneId);
                    ambiguous.Add(record.ZoneId);
                    continue;
                }

                kinds.Add(record.ZoneId, kind);
            }

            return new PvpZonePresenceCatalog(kinds);
        }

        public bool TryResolve(string zoneId, out PvpZonePolicyKind kind)
        {
            if (zoneId != null && kinds.TryGetValue(zoneId, out kind))
            {
                return true;
            }

            kind = PvpZonePolicyKind.Unknown;
            return false;
        }

        private static bool TryMapKind(string zoneKind, out PvpZonePolicyKind kind)
        {
            switch (zoneKind)
            {
                case "city":
                    kind = PvpZonePolicyKind.City;
                    return true;
                case "beginner":
                    kind = PvpZonePolicyKind.Beginner;
                    return true;
                case "accordant":
                    kind = PvpZonePolicyKind.Accordant;
                    return true;
                case "town":
                case "forced_safe":
                    kind = PvpZonePolicyKind.ForcedSafe;
                    return true;
                case "open":
                    kind = PvpZonePolicyKind.Open;
                    return true;
                default:
                    kind = PvpZonePolicyKind.Unknown;
                    return false;
            }
        }
    }

    public sealed class PvpHostilePresentationView
    {
        internal PvpHostilePresentationView(PvpPresentationKind kind)
        {
            Kind = kind;
            ShowRedNameplate = kind == PvpPresentationKind.Hostile ||
                               kind == PvpPresentationKind.WarHostile;
            ShowWarIcon = kind == PvpPresentationKind.WarHostile;
            AccessibleLabel = LabelFor(kind);
            IsAuthoritative = false;
        }

        public PvpPresentationKind Kind { get; }
        public bool ShowRedNameplate { get; }
        public bool ShowWarIcon { get; }
        public string AccessibleLabel { get; }
        public bool IsAuthoritative { get; }

        private static string LabelFor(PvpPresentationKind kind)
        {
            switch (kind)
            {
                case PvpPresentationKind.Hostile:
                    return "Hostile";
                case PvpPresentationKind.WarHostile:
                    return "Hostile war";
                case PvpPresentationKind.Protected:
                    return "Protected";
                case PvpPresentationKind.Neutral:
                    return "Neutral";
                default:
                    return "Unknown";
            }
        }
    }

    public sealed class PvpHarmfulEffectApplicationRequest
    {
        public PvpHarmfulEffectApplicationRequest(
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

    public sealed class PvpHarmfulEffectApplicationReceipt
    {
        internal PvpHarmfulEffectApplicationReceipt(
            PvpHarmfulEffectGateDecision gate,
            PvpHostilePresentationView presentation,
            bool applied,
            bool mayMutateHealth)
        {
            Gate = gate;
            Presentation = presentation;
            Applied = applied;
            MayMutateHealth = mayMutateHealth;
        }

        public PvpHarmfulEffectGateDecision Gate { get; }
        public PvpHostilePresentationView Presentation { get; }
        public bool Applied { get; }
        public bool MayMutateHealth { get; }
    }

    public sealed class PvpHostileTargetAuthorization
    {
        internal PvpHostileTargetAuthorization(
            bool allowed,
            PvpHarmfulEffectGateDecision gate,
            PvpHostilePresentationView presentation)
        {
            Allowed = allowed;
            Gate = gate;
            Presentation = presentation;
        }

        public bool Allowed { get; }
        public PvpHarmfulEffectGateDecision Gate { get; }
        public PvpHostilePresentationView Presentation { get; }
    }

    public interface IPvpHarmfulHealthMutator
    {
        bool TryMutate(PvpHarmfulEffectApplicationReceipt receipt);
    }

    public interface IPvpHarmfulEffectSession
    {
        bool TryCreate(
            PvpHarmfulEffectKind kind,
            out PvpHarmfulEffectApplicationRequest request);
    }
}
