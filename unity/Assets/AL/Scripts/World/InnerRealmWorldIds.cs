namespace AL.World
{
    /// <summary>
    /// Temporary greybox identities. Atlas zone/wall/gate/bridge IDs stay canonical.
    /// City names are placeholders (Capital / Outpost A / Outpost B) — the colored
    /// labeled map was not attached, so no proper names are invented.
    /// </summary>
    public static class InnerRealmWorldIds
    {
        public const string TemporaryLabel = "TEMPORARY";
        public const string PlacementProposalStatus = "temporary_presentation_proposal";
        public const string ColoredMapMissingNote =
            "colored_specifics_map_missing_unnamed_capital_and_outposts_only";

        public const string PoiCapitalSuffix = "capital";
        public const string PoiOutpostASuffix = "outpost_a";
        public const string PoiOutpostBSuffix = "outpost_b";
        public const string CaveSuffix = "dragon_cave";

        public static string CapitalPoiId(string innerAtlasZoneId)
        {
            return "poi_" + innerAtlasZoneId + "_" + PoiCapitalSuffix;
        }

        public static string OutpostAPoiId(string innerAtlasZoneId)
        {
            return "poi_" + innerAtlasZoneId + "_" + PoiOutpostASuffix;
        }

        public static string OutpostBPoiId(string innerAtlasZoneId)
        {
            return "poi_" + innerAtlasZoneId + "_" + PoiOutpostBSuffix;
        }

        public static string DragonCaveId(string innerAtlasZoneId)
        {
            return "cave_" + innerAtlasZoneId + "_" + CaveSuffix;
        }

        public static string DisplayCapital() => "Capital";

        public static string DisplayOutpostA() => "Outpost A";

        public static string DisplayOutpostB() => "Outpost B";
    }
}
