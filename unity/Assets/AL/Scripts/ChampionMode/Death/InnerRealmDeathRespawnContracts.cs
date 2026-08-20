using System;
using AL.Core;

namespace AL.ChampionMode.Death
{
    /// <summary>
    /// Inner-realm death stand-up only. Warzone save pillars (Product Direction
    /// item 9) are not a legal site. This contract never writes a save.
    /// </summary>
    public enum InnerRealmSafeSiteKind
    {
        Capital = 0,
        Area = 1,
        WarzonePillar = 2
    }

    public enum InnerRealmDeathZoneKind
    {
        Inner = 0,
        Warzone = 1,
        Unknown = 2
    }

    public enum InnerRealmDeathRespawnStatus
    {
        Applied = 0,
        RejectedInvalidRealm = 1,
        RejectedNoInnerSite = 2,
        RejectedWarzoneNotOwned = 3,
        RejectedInvalidRequest = 4
    }

    public enum InnerRealmDeathPresentationKind
    {
        DefeatThenStandUp = 0
    }

    public readonly struct InnerRealmVec3 : IEquatable<InnerRealmVec3>
    {
        public InnerRealmVec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public float DistanceSquaredTo(InnerRealmVec3 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            float dz = Z - other.Z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        public bool Equals(InnerRealmVec3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is InnerRealmVec3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class InnerRealmSafeSite
    {
        public InnerRealmSafeSite(
            string siteId,
            RealmId realmId,
            InnerRealmSafeSiteKind kind,
            string zoneId,
            InnerRealmVec3 position,
            bool isWarzone)
        {
            SiteId = siteId ?? string.Empty;
            RealmId = realmId;
            Kind = kind;
            ZoneId = zoneId ?? string.Empty;
            Position = position;
            IsWarzone = isWarzone;
        }

        public string SiteId { get; }
        public RealmId RealmId { get; }
        public InnerRealmSafeSiteKind Kind { get; }
        public string ZoneId { get; }
        public InnerRealmVec3 Position { get; }
        public bool IsWarzone { get; }

        public bool IsLegalInnerStandUp
        {
            get
            {
                return !IsWarzone &&
                       Kind != InnerRealmSafeSiteKind.WarzonePillar &&
                       ChampionRealmContext.Normalize(RealmId) != RealmId.None &&
                       IsStableId(SiteId) &&
                       IsStableId(ZoneId);
            }
        }

        public static InnerRealmSafeSite UnnamedCapital(RealmId realmId, InnerRealmVec3 position)
        {
            string zoneId;
            InnerRealmDeathRespawnPlanner.TryInnerZoneId(realmId, out zoneId);
            return new InnerRealmSafeSite(
                "inner_capital",
                realmId,
                InnerRealmSafeSiteKind.Capital,
                zoneId,
                position,
                isWarzone: false);
        }

        internal static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool previousUnderscore = false;
            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];
                bool isLowercaseLetter = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                bool isUnderscore = character == '_';
                if (!isLowercaseLetter && !isDigit && !isUnderscore)
                {
                    return false;
                }

                if (isUnderscore && previousUnderscore)
                {
                    return false;
                }

                previousUnderscore = isUnderscore;
            }

            return !previousUnderscore;
        }
    }

    public sealed class InnerRealmDeathRespawnRequest
    {
        public InnerRealmDeathRespawnRequest(
            RealmId realmId,
            InnerRealmVec3 deathPosition,
            InnerRealmDeathZoneKind deathZone,
            InnerRealmSafeSite[] sites)
        {
            RealmId = realmId;
            DeathPosition = deathPosition;
            DeathZone = deathZone;
            Sites = sites ?? Array.Empty<InnerRealmSafeSite>();
        }

        public RealmId RealmId { get; }
        public InnerRealmVec3 DeathPosition { get; }
        public InnerRealmDeathZoneKind DeathZone { get; }
        public InnerRealmSafeSite[] Sites { get; }
    }

    public sealed class InnerRealmDeathPresentation
    {
        public InnerRealmDeathPresentation(
            InnerRealmDeathPresentationKind kind,
            string title,
            string detail,
            float holdSeconds)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            HoldSeconds = holdSeconds;
        }

        public InnerRealmDeathPresentationKind Kind { get; }
        public string Title { get; }
        public string Detail { get; }
        public float HoldSeconds { get; }
        public bool ReloadsScene => false;
        public bool PersistsSave => false;
        public bool AllowsMenuSetRespawn => false;
        public bool AllowsPillarBind => false;
    }

    public sealed class InnerRealmDeathRespawnPlan
    {
        internal InnerRealmDeathRespawnPlan(
            InnerRealmDeathRespawnStatus status,
            string diagnosticCode,
            InnerRealmSafeSite site,
            InnerRealmDeathPresentation presentation)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Site = site;
            Presentation = presentation;
        }

        public InnerRealmDeathRespawnStatus Status { get; }
        public string DiagnosticCode { get; }
        public InnerRealmSafeSite Site { get; }
        public InnerRealmDeathPresentation Presentation { get; }
        public bool IsApplied => Status == InnerRealmDeathRespawnStatus.Applied && Site != null;

        public static InnerRealmDeathRespawnPlan Reject(
            InnerRealmDeathRespawnStatus status,
            string diagnosticCode)
        {
            return new InnerRealmDeathRespawnPlan(status, diagnosticCode, null, null);
        }
    }
}
