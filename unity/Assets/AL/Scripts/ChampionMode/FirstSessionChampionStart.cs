using System;

namespace AL.ChampionMode
{
    /// <summary>
    /// First-session 3D landing contract. Production Boot → Realm → Create lands in
    /// ChampionArena as an inner-realm champion start, not Kingdom and not a debug
    /// floor. Encounter-crowd / debug Kingdom HUD remain an explicit harness.
    /// </summary>
    public static class FirstSessionChampionStart
    {
        public const string DestinationSceneName = "ChampionArena";
        public const string TemporaryToken = "TEMPORARY";
        public const string EnvironmentRootName = "ChampionArena_ObsidianCitadel_TEMPORARY";
        public const string TemporaryPlaqueName = "TEMPORARY_GreyboxPlaque";
        public const string TemporaryPlaqueCopy = "TEMPORARY — inner-realm greybox";
        public const string PlayerObjectName = "Player_Champion";
        public const string HudCanvasName = "ChampionMode_HUD";
        public const string PlayerFrameName = "PlayerFrame";
        public const string HotbarName = "CombatHotbar";
        public const string TargetLockName = "BossTargetLock";
        public const string DebugKingdomButtonName = "Kingdom";
        public const string AtmosphereName = "InnerRealm_Atmosphere_TEMPORARY";
        public const string OpponentObjectName = "BossDummy";
        public const string WinPanelName = "EncounterClearPanel";
        public const string LosePanelName = "DefeatRetryPanel";
        public const string SpecialSkillId = "realm_strike";
        public const int SpecialSkillSlot = 0;

        public const string LandingFeedCopy =
            "Inner realm. Direct control is live — move, basic attack, and cast Realm Strike. TEMPORARY citadel greybox.";

        private static bool _encounterHarness;

        public static bool IsEncounterHarness => _encounterHarness;

        public static bool IsFirstSessionLanding => !_encounterHarness;

        public static bool AllowDebugKingdomLoad => _encounterHarness;

        public static bool ShowAppearanceRack => _encounterHarness;

        public static bool AutoStartEncounterIntro => _encounterHarness;

        public static bool AutoStartFirstFight => !_encounterHarness;

        public static void EnableEncounterHarness()
        {
            _encounterHarness = true;
        }

        public static void ResetToFirstSessionLanding()
        {
            _encounterHarness = false;
        }

        public static int ResolveDummyBudget(int requested)
        {
            return _encounterHarness ? Math.Max(0, requested) : 0;
        }

        public static int ResolveBotBudget(int requested)
        {
            return _encounterHarness ? Math.Max(0, requested) : 0;
        }

        public static string LabelTemporary(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return TemporaryToken;
            }

            return name.IndexOf(TemporaryToken, StringComparison.OrdinalIgnoreCase) >= 0
                ? name
                : name + "_" + TemporaryToken;
        }
    }
}
