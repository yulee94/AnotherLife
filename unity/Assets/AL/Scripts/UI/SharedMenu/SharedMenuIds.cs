namespace AL.UI.SharedMenu
{
    /// <summary>
    /// Contract IDs for Shared Menu kingdom unlock. Scene names are destinations,
    /// never the unlock predicate.
    /// </summary>
    public static class SharedMenuIds
    {
        public const string KingdomManagementModule = "MENU_MODULE_KINGDOM_MANAGEMENT";
        public const string Adventure3D = "Adventure3D";
        public const string Kingdom2_5D = "Kingdom2_5D";
        public const string AdventureScene = "ChampionArena";
        public const string KingdomScene = "Kingdom";
        public const string InnerRealmWorldScene = "InnerRealmWorld";
        public const string BootScene = "Boot";
        public const string RealmSelectionScene = "RealmSelection";
        public const string CharacterCreationScene = "CharacterCreation";
        public const string WarzoneScene = "Warzone";

        public const string InputSharedMenu = "SHARED_MENU";
        public const string InputConstructionDock = "CONSTRUCTION_DOCK";
        public const string InputBoot = "BOOT";
        public const string InputDuel = "GREYBOX_DUEL";
        public const string InputDemoInitializer = "DEMO_INITIALIZER";

        public const string ReasonLockedByNarrative = "REASON_LOCKED_BY_NARRATIVE";
        public const string ReasonQuestPending = "REASON_QUEST_PENDING";
        public const string ReasonSessionUnready = "REASON_SESSION_UNREADY";

        public const string SwitchSucceeded = "SUCCEEDED";
        public const string SwitchRejected = "REJECTED";
        public const string SwitchRejectedDependency = "SWITCH_REJECTED_DEPENDENCY";
        public const string SwitchRejectedState = "SWITCH_REJECTED_STATE";
        public const string SwitchRejectedSystem = "SWITCH_REJECTED_SYSTEM";
        public const string AlreadyInMode = "ALREADY_IN_MODE";

        public const string OverlayRootName = "SharedMenu_KingdomGate_TEMPORARY";
        public const string KingdomButtonName = "MENU_MODULE_KINGDOM_MANAGEMENT";
        public const string HostName = "SharedMenu_ModeSwitchHost_TEMPORARY";
        public const string LoadModeSingle = "Single";
    }
}
