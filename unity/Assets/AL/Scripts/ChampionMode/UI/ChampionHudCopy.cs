namespace AL.ChampionMode.UI
{
    /// <summary>
    /// Player-facing 3D HUD / pause / recap copy. Shared Menu owns the 2.5D gate.
    /// </summary>
    public static class ChampionHudCopy
    {
        public const string SharedMenuButtonName = "SharedMenuButton";
        public const string SharedMenuButtonLabel = "Menu";
        public const string ResumeButtonName = "SharedMenuResume";
        public const string Resume = "Resume";
        public const string QuestSlotName = "QuestHudSlot";
        public const string PauseOverlayName = "ChampionHud_Pause";
        public const string RecapSharedMenuButtonName = "SharedMenu";

        public const string RecapNext =
            "Next: retry the encounter, inspect your champion, or open the Shared Menu.";
        public const string DefeatFeed =
            "Champion down. Retry the encounter, refine your build, or open the Shared Menu.";
        public const string ClearFeed =
            "Encounter cleared. Review the result, inspect your build, retry, or open the Shared Menu.";
        public const string BossClearFeed =
            "Boss defeated. Loot roll complete. Open the Shared Menu or keep testing your build.";
    }
}
