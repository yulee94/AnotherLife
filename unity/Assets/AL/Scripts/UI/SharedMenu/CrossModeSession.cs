namespace AL.UI.SharedMenu
{
    /// <summary>
    /// Remembers the inner-realm 3D session so Shared Menu return never
    /// goes through Boot, RealmSelection, or Warzone.
    /// </summary>
    public static class CrossModeSession
    {
        public static string AdventureScene { get; private set; } = SharedMenuIds.AdventureScene;

        public static bool HasActiveRoundTrip { get; private set; }

        public static bool HasPendingTeachingReturn { get; private set; }

        public static void RememberAdventure(string sceneName)
        {
            if (!CrossModeSceneSwitch.IsAdventureScene(sceneName))
            {
                return;
            }

            AdventureScene = SharedMenuIds.AdventureScene;
            HasActiveRoundTrip = true;
        }

        public static void MarkKingdomActive()
        {
            HasActiveRoundTrip = true;
        }

        public static void ArmTeachingReturn()
        {
            HasPendingTeachingReturn = true;
        }

        public static bool TryConsumeTeachingReturn()
        {
            if (!HasPendingTeachingReturn)
            {
                return false;
            }

            HasPendingTeachingReturn = false;
            return true;
        }

        public static void Reset()
        {
            AdventureScene = SharedMenuIds.AdventureScene;
            HasActiveRoundTrip = false;
            HasPendingTeachingReturn = false;
        }
    }
}
