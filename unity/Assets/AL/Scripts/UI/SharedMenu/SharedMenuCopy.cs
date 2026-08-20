namespace AL.UI.SharedMenu
{
    /// <summary>
    /// Player-facing Shared Menu copy for kingdom unlock.
    /// Keys match First_User_Playable_Spine; body is real lock copy, not a missing button.
    /// </summary>
    public static class SharedMenuCopy
    {
        public const string TitleKey = "menu.kingdom_management.title";
        public const string LockedKey = "menu.kingdom_management.locked";
        public const string NewlyUnlockedKey = "menu.kingdom_management.newly_unlocked";
        public const string EnterKey = "menu.kingdom_management.enter";
        public const string ReturnKey = "menu.kingdom_management.return_to_character";
        public const string UnavailableKey = "menu.kingdom_management.unavailable";

        public const string Title = "Kingdom Management";
        public const string Locked =
            "Proof of Worth still stands. Accept the covenant mark and take lordship of your realm before the private kingdom will open.";
        public const string NewlyUnlocked =
            "Lordship is recognized. Kingdom Management is open from the Shared Menu.";
        public const string Enter = "Enter the private kingdom";
        public const string ReturnToCharacter = "Return to the inner realm";
        public const string UnavailableCombat =
            "You cannot enter the private kingdom while in combat.";
        public const string UnavailableUnsafe =
            "You cannot enter the private kingdom until you are safe.";
    }
}
