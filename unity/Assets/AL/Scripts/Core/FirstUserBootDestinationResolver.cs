using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Core
{
    /// <summary>
    /// Resolves Boot continue destination from the authoritative save.
    /// Realm-only evidence stays on onboarding. Confirmed champion identity
    /// skips re-create. Kingdom is never a Boot destination.
    /// </summary>
    public static class FirstUserBootDestinationResolver
    {
        public const string GameplaySceneName = "ChampionArena";

        public static string ResolveSceneName(
            ISaveGameService saveGameService,
            string onboardingSceneName,
            bool gameplaySceneLoadable)
        {
            SaveGameData save = saveGameService == null
                ? null
                : saveGameService.CurrentSave;
            return ResolveSceneName(save, onboardingSceneName, gameplaySceneLoadable);
        }

        public static string ResolveSceneName(
            SaveGameData save,
            string onboardingSceneName,
            bool gameplaySceneLoadable)
        {
            string onboarding = string.IsNullOrWhiteSpace(onboardingSceneName)
                ? "RealmSelection"
                : onboardingSceneName;
            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(save);
            if (!snapshot.ShouldSkipCreate ||
                !gameplaySceneLoadable ||
                string.Equals(GameplaySceneName, onboarding, System.StringComparison.Ordinal))
            {
                return onboarding;
            }

            return GameplaySceneName;
        }
    }
}
