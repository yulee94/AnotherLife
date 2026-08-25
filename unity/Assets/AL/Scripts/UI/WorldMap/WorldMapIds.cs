namespace AL.UI.WorldMap
{
    /// <summary>
    /// Chrome-only identities for the 3D open-map. Markers belong to t_3aa1275b.
    /// City proper names stay placeholders until a colored specifics map is attached.
    /// </summary>
    public static class WorldMapIds
    {
        public const string TemporaryLabel = "TEMPORARY";
        public const string OverlayRootName = "WorldMap_Overlay_TEMPORARY";
        public const string SharedMenuRootName = "SharedMenu_Overlay_TEMPORARY";
        public const string HostName = "WorldMapHost";
        public const string MenuModuleWorldMap = "MENU_MODULE_WORLD_MAP";
        public const string MenuModuleKingdom = "MENU_MODULE_KINGDOM_MANAGEMENT";
        public const string PlacementProposalStatus = "temporary_presentation_proposal";
        public const string AccordantIsleZoneId = "zone_accordant_isle";
        public const string ColoredMapMissingNote =
            "colored_specifics_map_missing_unnamed_capital_and_outposts_only";

        public const string DisplayCapital = "Capital";
        public const string DisplayOutpostA = "Outpost A";
        public const string DisplayOutpostB = "Outpost B";
        public const string DisplayAccordantIsle = "Accordant Isle";
        public const string TitleCopy = "WORLD MAP";
        public const string CloseHintCopy = "M  close map    Esc  menu";
        public const string SharedMenuTitle = "SHARED MENU";
        public const string SharedMenuWorldMapLabel = "World Map";
        public const string SharedMenuKingdomLabel = "Kingdom Management";
        public const string SharedMenuKingdomLock = "Locked until lordship";

        public const string ChampionArenaScene = "ChampionArena";
        public const string InnerRealmWorldScene = "InnerRealmWorld";

        public static string CapitalPoiId(string innerAtlasZoneId)
        {
            return "poi_" + innerAtlasZoneId + "_capital";
        }

        public static string OutpostAPoiId(string innerAtlasZoneId)
        {
            return "poi_" + innerAtlasZoneId + "_outpost_a";
        }

        public static string OutpostBPoiId(string innerAtlasZoneId)
        {
            return "poi_" + innerAtlasZoneId + "_outpost_b";
        }

        public static string RealmDisplayName(string realmId)
        {
            switch (realmId)
            {
                case "stonehold":
                    return "Stonehold";
                case "eldergrove":
                    return "Eldergrove";
                case "crownlands":
                    return "Crownlands";
                case "umbral":
                    return "Umbral";
                default:
                    return string.Empty;
            }
        }
    }
}
