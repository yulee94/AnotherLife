using System.Collections.Generic;
using AL.Core;
using AL.World;

namespace AL.UI.SharedMenu
{
    /// <summary>
    /// 2.5D private-kingdom map may list only the inner-realm castle and Area
    /// points. Outer-realm / Warzone IDs are never destinations.
    /// </summary>
    public static class PrivateKingdomInnerDestinations
    {
        public static IReadOnlyList<string> EnumerateCastleAndAreas(RealmId realm)
        {
            string zone = ZoneFor(realm);
            return new[]
            {
                InnerRealmWorldIds.CapitalPoiId(zone),
                InnerRealmWorldIds.OutpostAPoiId(zone),
                InnerRealmWorldIds.OutpostBPoiId(zone)
            };
        }

        public static bool IsAllowed(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (id.IndexOf("warzone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("outer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("accordant", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return id.StartsWith("poi_zone_inner_", System.StringComparison.Ordinal);
        }

        public static bool ContainsForbidden(IReadOnlyList<string> ids)
        {
            if (ids == null)
            {
                return false;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (!IsAllowed(ids[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ZoneFor(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return FirstSessionInnerRealmSpawn.StoneholdZoneId;
                case RealmId.Eldergrove:
                    return FirstSessionInnerRealmSpawn.EldergroveZoneId;
                case RealmId.Crownlands:
                    return FirstSessionInnerRealmSpawn.CrownlandsZoneId;
                case RealmId.Umbral:
                    return FirstSessionInnerRealmSpawn.UmbralZoneId;
                default:
                    return string.Empty;
            }
        }
    }
}
