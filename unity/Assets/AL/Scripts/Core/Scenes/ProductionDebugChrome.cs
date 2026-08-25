using System;

namespace AL.Core.Scenes
{
    /// <summary>
    /// Production-reachable scenes must not host DemoInitializer-style debug harnesses.
    /// Kingdom stays listed because it is still in Build Settings, even though first-session
    /// launch is 3D-first and does not open it.
    /// </summary>
    public static class ProductionDebugChrome
    {
        public const string TemporaryToken = "TEMPORARY";

        public static bool IsProductionReachable(string sceneName)
        {
            return string.Equals(sceneName, "Boot", StringComparison.Ordinal) ||
                   string.Equals(sceneName, "RealmSelection", StringComparison.Ordinal) ||
                   string.Equals(sceneName, "CharacterCreation", StringComparison.Ordinal) ||
                   string.Equals(sceneName, "ChampionArena", StringComparison.Ordinal) ||
                   string.Equals(sceneName, "Kingdom", StringComparison.Ordinal);
        }

        public static bool AllowsDemoInitializer(string sceneName)
        {
            return !IsProductionReachable(sceneName);
        }
    }
}
