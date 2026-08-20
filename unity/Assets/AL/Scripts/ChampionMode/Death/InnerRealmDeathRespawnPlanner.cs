using AL.Core;

namespace AL.ChampionMode.Death
{
    /// <summary>
    /// Pure inner-realm death stand-up. Prefers the unnamed capital, else the
    /// nearest inner Area. Never binds a warzone pillar, never reloads
    /// a scene, and never mutates a save.
    /// </summary>
    public static class InnerRealmDeathRespawnPlanner
    {
        public const string ReadyCode = "AL-INNER-DEATH-RESPAWN-READY";
        public const string InvalidRealmCode = "AL-INNER-DEATH-RESPAWN-INVALID-REALM";
        public const string NoInnerSiteCode = "AL-INNER-DEATH-RESPAWN-NO-INNER-SITE";
        public const string WarzoneNotOwnedCode = "AL-INNER-DEATH-RESPAWN-WARZONE-NOT-OWNED";
        public const string InvalidRequestCode = "AL-INNER-DEATH-RESPAWN-INVALID-REQUEST";

        public const string FallenTitle = "FALLEN";
        public const string CapitalStandUpDetail = "Returning to the Capital.";
        public const string AreaStandUpDetail = "Returning to the nearest inner Area.";
        public const float DefeatHoldSeconds = 1.6f;

        public static bool TryInnerZoneId(RealmId realmId, out string zoneId)
        {
            switch (ChampionRealmContext.Normalize(realmId))
            {
                case RealmId.Crownlands:
                    zoneId = "zone_inner_crownlands";
                    return true;
                case RealmId.Stonehold:
                    zoneId = "zone_inner_stonehold";
                    return true;
                case RealmId.Eldergrove:
                    zoneId = "zone_inner_eldergrove";
                    return true;
                case RealmId.Umbral:
                    zoneId = "zone_inner_umbral";
                    return true;
                default:
                    zoneId = string.Empty;
                    return false;
            }
        }

        public static InnerRealmDeathRespawnPlan Plan(InnerRealmDeathRespawnRequest request)
        {
            if (request == null)
            {
                return InnerRealmDeathRespawnPlan.Reject(
                    InnerRealmDeathRespawnStatus.RejectedInvalidRequest,
                    InvalidRequestCode);
            }

            RealmId realmId = ChampionRealmContext.Normalize(request.RealmId);
            if (realmId == RealmId.None)
            {
                return InnerRealmDeathRespawnPlan.Reject(
                    InnerRealmDeathRespawnStatus.RejectedInvalidRealm,
                    InvalidRealmCode);
            }

            if (request.DeathZone == InnerRealmDeathZoneKind.Warzone)
            {
                return InnerRealmDeathRespawnPlan.Reject(
                    InnerRealmDeathRespawnStatus.RejectedWarzoneNotOwned,
                    WarzoneNotOwnedCode);
            }

            InnerRealmSafeSite capital = null;
            InnerRealmSafeSite nearestOutpost = null;
            float nearestOutpostDistance = float.MaxValue;
            InnerRealmSafeSite[] sites = request.Sites;
            for (int i = 0; i < sites.Length; i++)
            {
                InnerRealmSafeSite site = sites[i];
                if (site == null ||
                    ChampionRealmContext.Normalize(site.RealmId) != realmId ||
                    !site.IsLegalInnerStandUp)
                {
                    continue;
                }

                if (site.Kind == InnerRealmSafeSiteKind.Capital)
                {
                    capital = site;
                    break;
                }

                if (site.Kind != InnerRealmSafeSiteKind.Area)
                {
                    continue;
                }

                float distance = site.Position.DistanceSquaredTo(request.DeathPosition);
                if (nearestOutpost == null || distance < nearestOutpostDistance)
                {
                    nearestOutpost = site;
                    nearestOutpostDistance = distance;
                }
            }

            InnerRealmSafeSite chosen = capital ?? nearestOutpost;
            if (chosen == null)
            {
                return InnerRealmDeathRespawnPlan.Reject(
                    InnerRealmDeathRespawnStatus.RejectedNoInnerSite,
                    NoInnerSiteCode);
            }

            var presentation = new InnerRealmDeathPresentation(
                InnerRealmDeathPresentationKind.DefeatThenStandUp,
                FallenTitle,
                chosen.Kind == InnerRealmSafeSiteKind.Capital
                    ? CapitalStandUpDetail
                    : AreaStandUpDetail,
                DefeatHoldSeconds);

            return new InnerRealmDeathRespawnPlan(
                InnerRealmDeathRespawnStatus.Applied,
                ReadyCode,
                chosen,
                presentation);
        }
    }
}
