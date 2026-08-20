using AL.Core.Scenes;
using AL.Data.Runtime;
using UnityEngine.SceneManagement;

namespace AL.UI.SharedMenu
{
    public readonly struct CrossModeSwitchPlan
    {
        public CrossModeSwitchPlan(
            string status,
            string failure,
            string fromMode,
            string toMode,
            string destinationScene,
            bool shouldLoad)
        {
            Status = status ?? string.Empty;
            Failure = failure ?? string.Empty;
            FromMode = fromMode ?? string.Empty;
            ToMode = toMode ?? string.Empty;
            DestinationScene = destinationScene ?? string.Empty;
            ShouldLoad = shouldLoad;
        }

        public string Status { get; }
        public string Failure { get; }
        public string FromMode { get; }
        public string ToMode { get; }
        public string DestinationScene { get; }
        public bool ShouldLoad { get; }
        public LoadSceneMode LoadMode => LoadSceneMode.Single;
        public string LoadModeName => SharedMenuIds.LoadModeSingle;
        public bool Succeeded =>
            string.Equals(Status, SharedMenuIds.SwitchSucceeded, System.StringComparison.Ordinal) ||
            string.Equals(Status, SharedMenuIds.AlreadyInMode, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Live 3D ↔ 2.5D LoadScene planner. Shared Menu is the only route.
    /// Reuses ChampionArena (inner-realm 3D) and Kingdom (private kingdom).
    /// </summary>
    public static class CrossModeSceneSwitch
    {
        public static string ReusedAdventureController =>
            "AL.ChampionMode.ChampionArenaSceneController";

        public static string ReusedKingdomController =>
            "AL.UI.Kingdom.KingdomSceneController";

        public static bool IsAdventureScene(string sceneName)
        {
            return string.Equals(sceneName, SharedMenuIds.AdventureScene, System.StringComparison.Ordinal) ||
                   string.Equals(sceneName, SharedMenuIds.InnerRealmWorldScene, System.StringComparison.Ordinal);
        }

        public static bool IsKingdomScene(string sceneName)
        {
            return string.Equals(sceneName, SharedMenuIds.KingdomScene, System.StringComparison.Ordinal);
        }

        public static bool IsForbiddenReturnScene(string sceneName)
        {
            return string.Equals(sceneName, SharedMenuIds.BootScene, System.StringComparison.Ordinal) ||
                   string.Equals(sceneName, SharedMenuIds.RealmSelectionScene, System.StringComparison.Ordinal) ||
                   string.Equals(sceneName, SharedMenuIds.CharacterCreationScene, System.StringComparison.Ordinal) ||
                   string.Equals(sceneName, SharedMenuIds.WarzoneScene, System.StringComparison.Ordinal) ||
                   (!string.IsNullOrEmpty(sceneName) &&
                    sceneName.IndexOf("warzone", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsSupportedHostScene(string sceneName)
        {
            return IsAdventureScene(sceneName) || IsKingdomScene(sceneName);
        }

        public static string ResolveMode(string sceneName)
        {
            if (IsKingdomScene(sceneName))
            {
                return SharedMenuIds.Kingdom2_5D;
            }

            if (IsAdventureScene(sceneName))
            {
                return SharedMenuIds.Adventure3D;
            }

            return string.Empty;
        }

        public static bool IsShellLoadable(string sceneName)
        {
            if (!ProductionSceneDescriptor.TryGetBySceneName(sceneName, out ProductionSceneRecord record))
            {
                return false;
            }

            if (!record.IsInShellFoundation || record.Status != ProductionSceneDescriptor.StatusCommittedActive)
            {
                return false;
            }

            return string.Equals(sceneName, SharedMenuIds.AdventureScene, System.StringComparison.Ordinal) ||
                   string.Equals(sceneName, SharedMenuIds.KingdomScene, System.StringComparison.Ordinal);
        }

        public static string ResolveLoadScene(string mode)
        {
            if (string.Equals(mode, SharedMenuIds.Kingdom2_5D, System.StringComparison.Ordinal))
            {
                return SharedMenuIds.KingdomScene;
            }

            return CrossModeSession.AdventureScene;
        }

        public static CrossModeSwitchPlan Plan(
            string currentScene,
            string targetMode,
            SaveGameData save,
            bool inCombat,
            bool unsafeContext,
            string inputSource)
        {
            string fromMode = ResolveMode(currentScene);
            if (!IsSupportedHostScene(currentScene) || string.IsNullOrEmpty(fromMode))
            {
                return Reject(
                    SharedMenuIds.SwitchRejectedDependency,
                    fromMode,
                    currentScene);
            }

            if (!IsIdentityCommitted(save))
            {
                return Reject(
                    SharedMenuIds.SwitchRejectedDependency,
                    fromMode,
                    currentScene);
            }

            ModeSwitchResult result = KingdomManagementUnlock.RequestSwitch(new ModeSwitchRequest(
                fromMode,
                targetMode,
                save,
                inCombat,
                unsafeContext,
                inputSource));

            if (string.Equals(result.Status, SharedMenuIds.AlreadyInMode, System.StringComparison.Ordinal))
            {
                return new CrossModeSwitchPlan(
                    SharedMenuIds.AlreadyInMode,
                    string.Empty,
                    fromMode,
                    targetMode,
                    currentScene,
                    shouldLoad: false);
            }

            if (!result.Succeeded)
            {
                return new CrossModeSwitchPlan(
                    result.Status,
                    result.Failure,
                    fromMode,
                    fromMode,
                    currentScene,
                    shouldLoad: false);
            }

            string destination = ResolveLoadScene(result.DestinationMode);
            if (IsForbiddenReturnScene(destination) || !IsShellLoadable(destination))
            {
                return Reject(
                    SharedMenuIds.SwitchRejectedSystem,
                    fromMode,
                    currentScene);
            }

            if (string.Equals(result.DestinationMode, SharedMenuIds.Kingdom2_5D, System.StringComparison.Ordinal))
            {
                CrossModeSession.RememberAdventure(currentScene);
                CrossModeSession.MarkKingdomActive();
            }

            return new CrossModeSwitchPlan(
                SharedMenuIds.SwitchSucceeded,
                string.Empty,
                fromMode,
                result.DestinationMode,
                destination,
                shouldLoad: true);
        }

        public static bool TryCommit(CrossModeSwitchPlan plan, System.Action<string, LoadSceneMode> load)
        {
            if (!plan.ShouldLoad || load == null)
            {
                return false;
            }

            if (IsForbiddenReturnScene(plan.DestinationScene) || !IsShellLoadable(plan.DestinationScene))
            {
                return false;
            }

            load(plan.DestinationScene, plan.LoadMode);
            return true;
        }

        public static bool IsIdentityCommitted(SaveGameData save)
        {
            return MvpLoopSaveCodec.Read(save).IdentityConfirmed;
        }

        private static CrossModeSwitchPlan Reject(string failure, string fromMode, string currentScene)
        {
            return new CrossModeSwitchPlan(
                SharedMenuIds.SwitchRejected,
                failure,
                fromMode,
                fromMode,
                currentScene,
                shouldLoad: false);
        }
    }
}
