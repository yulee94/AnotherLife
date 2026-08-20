using AL.ChampionMode.Quests;
using AL.Data.Runtime;

namespace AL.UI.SharedMenu
{
    public enum SharedMenuAvailability
    {
        Available = 0,
        LockedNarrative = 1,
        BlockedTransient = 2,
        BlockedDependency = 3,
        Hidden = 4
    }

    public readonly struct SharedMenuModuleState
    {
        public SharedMenuModuleState(
            string moduleId,
            SharedMenuAvailability availability,
            string reasonCode,
            string title,
            string detail,
            bool visible,
            bool canInvoke)
        {
            ModuleId = moduleId ?? string.Empty;
            Availability = availability;
            ReasonCode = reasonCode ?? string.Empty;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Visible = visible;
            CanInvoke = canInvoke;
        }

        public string ModuleId { get; }
        public SharedMenuAvailability Availability { get; }
        public string ReasonCode { get; }
        public string Title { get; }
        public string Detail { get; }
        public bool Visible { get; }
        public bool CanInvoke { get; }
    }

    public readonly struct ModeSwitchRequest
    {
        public ModeSwitchRequest(
            string currentMode,
            string targetMode,
            SaveGameData save,
            bool inCombat,
            bool unsafeContext,
            string inputSource)
        {
            CurrentMode = currentMode ?? string.Empty;
            TargetMode = targetMode ?? string.Empty;
            Save = save;
            InCombat = inCombat;
            UnsafeContext = unsafeContext;
            InputSource = inputSource ?? string.Empty;
        }

        public string CurrentMode { get; }
        public string TargetMode { get; }
        public SaveGameData Save { get; }
        public bool InCombat { get; }
        public bool UnsafeContext { get; }
        public string InputSource { get; }
    }

    public readonly struct ModeSwitchResult
    {
        public ModeSwitchResult(
            string status,
            string failure,
            string destinationMode,
            string destinationScene)
        {
            Status = status ?? string.Empty;
            Failure = failure ?? string.Empty;
            DestinationMode = destinationMode ?? string.Empty;
            DestinationScene = destinationScene ?? string.Empty;
        }

        public string Status { get; }
        public string Failure { get; }
        public string DestinationMode { get; }
        public string DestinationScene { get; }
        public bool Succeeded => string.Equals(Status, SharedMenuIds.SwitchSucceeded, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// MENU_MODULE_KINGDOM_MANAGEMENT stays LockedNarrative until Proof of Worth / lordship.
    /// Shared Menu is the only 3D→2.5D route. Boot, Duel, DemoInitializer, and keyboard B
    /// cannot unlock or switch.
    /// </summary>
    public static class KingdomManagementUnlock
    {
        public static bool IsLordshipGranted(SaveGameData save)
        {
            return ProofOfWorthLordship.IsGranted(save);
        }

        public static SharedMenuModuleState EvaluateKingdomManagement(
            SaveGameData save,
            bool inCombat = false,
            bool unsafeContext = false)
        {
            if (!MvpLoopSaveCodec.Read(save).IdentityConfirmed ||
                !ProofOfWorthLordship.IsGranted(save))
            {
                return new SharedMenuModuleState(
                    SharedMenuIds.KingdomManagementModule,
                    SharedMenuAvailability.LockedNarrative,
                    SharedMenuIds.ReasonLockedByNarrative,
                    SharedMenuCopy.Title,
                    SharedMenuCopy.Locked,
                    visible: true,
                    canInvoke: false);
            }

            if (inCombat)
            {
                return new SharedMenuModuleState(
                    SharedMenuIds.KingdomManagementModule,
                    SharedMenuAvailability.BlockedTransient,
                    SharedMenuIds.ReasonSessionUnready,
                    SharedMenuCopy.Title,
                    SharedMenuCopy.UnavailableCombat,
                    visible: true,
                    canInvoke: false);
            }

            if (unsafeContext)
            {
                return new SharedMenuModuleState(
                    SharedMenuIds.KingdomManagementModule,
                    SharedMenuAvailability.BlockedTransient,
                    SharedMenuIds.ReasonSessionUnready,
                    SharedMenuCopy.Title,
                    SharedMenuCopy.UnavailableUnsafe,
                    visible: true,
                    canInvoke: false);
            }

            return new SharedMenuModuleState(
                SharedMenuIds.KingdomManagementModule,
                SharedMenuAvailability.Available,
                string.Empty,
                SharedMenuCopy.Title,
                SharedMenuCopy.NewlyUnlocked,
                visible: true,
                canInvoke: true);
        }

        public static ModeSwitchResult RequestSwitch(ModeSwitchRequest request)
        {
            if (!IsSharedMenuInput(request.InputSource))
            {
                return Reject(SharedMenuIds.SwitchRejectedDependency, request.CurrentMode);
            }

            if (IsConstructionDock(request.InputSource))
            {
                return Reject(SharedMenuIds.SwitchRejectedDependency, request.CurrentMode);
            }

            if (string.Equals(request.CurrentMode, request.TargetMode, System.StringComparison.Ordinal))
            {
                return new ModeSwitchResult(
                    SharedMenuIds.AlreadyInMode,
                    string.Empty,
                    request.CurrentMode,
                    SceneForMode(request.CurrentMode));
            }

            if (!IsSupportedPair(request.CurrentMode, request.TargetMode))
            {
                return Reject(SharedMenuIds.SwitchRejectedDependency, request.CurrentMode);
            }

            if (TargetsKingdom(request.TargetMode) &&
                (!MvpLoopSaveCodec.Read(request.Save).IdentityConfirmed ||
                 !ProofOfWorthLordship.IsGranted(request.Save)))
            {
                return Reject(SharedMenuIds.SwitchRejectedDependency, request.CurrentMode);
            }

            if (request.InCombat || request.UnsafeContext)
            {
                return Reject(SharedMenuIds.SwitchRejectedState, request.CurrentMode);
            }

            return new ModeSwitchResult(
                SharedMenuIds.SwitchSucceeded,
                string.Empty,
                request.TargetMode,
                SceneForMode(request.TargetMode));
        }

        public static string SceneForMode(string mode)
        {
            if (string.Equals(mode, SharedMenuIds.Kingdom2_5D, System.StringComparison.Ordinal))
            {
                return SharedMenuIds.KingdomScene;
            }

            return SharedMenuIds.AdventureScene;
        }

        public static bool IsSharedMenuInput(string inputSource)
        {
            return string.Equals(inputSource, SharedMenuIds.InputSharedMenu, System.StringComparison.Ordinal);
        }

        public static bool IsParallelUnlockAttempt(string inputSource)
        {
            return string.Equals(inputSource, SharedMenuIds.InputBoot, System.StringComparison.Ordinal) ||
                   string.Equals(inputSource, SharedMenuIds.InputDuel, System.StringComparison.Ordinal) ||
                   string.Equals(inputSource, SharedMenuIds.InputDemoInitializer, System.StringComparison.Ordinal) ||
                   IsConstructionDock(inputSource);
        }

        private static bool IsConstructionDock(string inputSource)
        {
            return string.Equals(inputSource, SharedMenuIds.InputConstructionDock, System.StringComparison.Ordinal);
        }

        private static bool TargetsKingdom(string mode)
        {
            return string.Equals(mode, SharedMenuIds.Kingdom2_5D, System.StringComparison.Ordinal);
        }

        private static bool IsSupportedPair(string from, string to)
        {
            bool adventureToKingdom =
                string.Equals(from, SharedMenuIds.Adventure3D, System.StringComparison.Ordinal) &&
                string.Equals(to, SharedMenuIds.Kingdom2_5D, System.StringComparison.Ordinal);
            bool kingdomToAdventure =
                string.Equals(from, SharedMenuIds.Kingdom2_5D, System.StringComparison.Ordinal) &&
                string.Equals(to, SharedMenuIds.Adventure3D, System.StringComparison.Ordinal);
            return adventureToKingdom || kingdomToAdventure;
        }

        private static ModeSwitchResult Reject(string failure, string currentMode)
        {
            return new ModeSwitchResult(
                SharedMenuIds.SwitchRejected,
                failure,
                currentMode,
                SceneForMode(currentMode));
        }
    }
}
