#if !UNITY_EDITOR
#error The isolated first-user playtest copy is Editor-only.
#endif

using AL.Core;
using AL.UI.FirstUserIdentity;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal enum FirstUserGameTestPlaytestPhase
    {
        Invalid = 0,
        Loading = 1,
        Identity = 2,
        AppearanceAndName = 3,
        WorldTutorial = 4,
        Omen = 5,
        SkyCastle = 6,
        ValeriusReturn = 7,
        RealmReady = 8
    }

    internal static class FirstUserGameTestPlaytestCopy
    {
        internal const string NonProductionBadge =
            "ISOLATED EDITOR PLAYTEST — NOT PRODUCTION — SESSION ONLY";
        internal const string LoadingBreadcrumb =
            "[Preparing]  ›  Origin  ›  Appearance  ›  First Steps  ›  Valerius";
        internal const string IdentityBreadcrumb =
            "Preparing  ›  [Origin]  ›  Appearance  ›  First Steps  ›  Valerius";
        internal const string AppearanceBreadcrumb =
            "Preparing  ›  Origin  ›  [Appearance]  ›  First Steps  ›  Valerius";
        internal const string TutorialBreadcrumb =
            "Preparing  ›  Origin  ›  Appearance  ›  [First Steps]  ›  Valerius";
        internal const string OmenBreadcrumb =
            "Preparing  ›  Origin  ›  Appearance  ›  First Steps  ›  [Valerius]  ›  Sky Castle  ›  Return";
        internal const string SkyCastleBreadcrumb =
            "Preparing  ›  Origin  ›  Appearance  ›  First Steps  ›  Valerius  ›  [Sky Castle]  ›  Return";
        internal const string ValeriusReturnBreadcrumb =
            "Preparing  ›  Origin  ›  Appearance  ›  First Steps  ›  Valerius  ›  Sky Castle  ›  [Return]";
        internal const string RealmReadyBreadcrumb =
            "Preparing  ›  Origin  ›  Appearance  ›  First Steps  ›  Valerius  ›  Sky Castle  ›  [Realm Ready]";

        internal const string AppearanceHeading =
            "Choose your appearance and name";
        internal const string NamePlaceholder = "Name your Champion for this playtest";
        internal const string AppearancePrompt =
            "Choose an appearance, then name your Champion.";
        internal const string AppearanceRequired = "Choose an appearance to continue.";
        internal const string NameRequired =
            "Enter a short name without leading or trailing spaces.";
        internal const string ReadyForTutorial = "Ready to enter the world.";
        internal const string PreparingWorld = "Entering the world…";
        internal const string FriendlyBlockedStatus =
            "The isolated playtest could not continue. Review the Console, then exit safely.";
        internal const string FriendlyFailurePanel =
            "ISOLATED PLAYTEST PAUSED\n\nSomething needed attention, so the playtest stopped safely.\n\nChoose Exit Isolated Test or stop Play Mode. Your normal save was not opened or changed.";
        internal const string ExitAction = "Exit Isolated Test";
        internal const string ExitingStatus = "Closing the isolated playtest safely…";

        internal const string MoveTitle = "First Steps · 1 of 2";
        internal const string MoveObjective = "Move your Champion";
        internal const string MoveDetail =
            "Use the movement arrows or your usual movement controls.";
        internal const string AttackTitle = "First Steps · 2 of 2";
        internal const string AttackObjective = "Use Basic Attack";
        internal const string AttackDetail =
            "Choose Basic Attack or use your usual action control.";
        internal const string OmenTitle = "Veil Watch Dispatch";
        internal const string OmenObjective = "Hear Valerius's report";
        internal const string OmenOpenedStatus = "Choose your response";
        internal const string OmenDeploymentReadyStatus =
            "Confirm the Sky Castle deployment";
        internal const string OmenReopenAction = "Reopen Valerius's report";
        internal const string OmenDeclinedDetail =
            "Valerius will wait. Reopen the report when you are ready.";
        internal const string OmenDeploymentStatus =
            "Mission accepted · Deployment prepared";
        internal const string OmenDeploymentDetail =
            "Your Champion is ready to cross into the Sky Castle.";
        internal const string EnterSkyCastleAction = "Enter the Sky Castle";
        internal const string EncounterStatus = "Sky Castle · Celestial disturbance";
        internal const string EncounterDetail =
            "Complete this journey checkpoint or retreat safely to regroup.";
        internal const string RecoverTearAction = "Recover the Celestial Tear";
        internal const string RetreatAction = "Retreat and regroup";
        internal const string RecoveryStatus = "Safe return · Ready to retry";
        internal const string RecoveryDetail =
            "No progress was lost. Return to the Sky Castle when you are ready.";
        internal const string ReportReadyStatus = "Celestial Tear recovered";
        internal const string ReportReadyDetail =
            "Bring the Celestial Tear back to Captain Valerius.";
        internal const string ReturnToValeriusAction = "Return to Valerius";
        internal const string RealmReadyStatus = "Realm command is ready";
        internal const string RealmReadyDetail =
            "The First Signal is complete. Your realm's next chapter is ready.";
        internal const string CompleteJourneyAction = "Complete playtest";
        internal const string OmenDetail =
            "The Veil Watch has sent an urgent dispatch from the Sky Castle.";

        private static readonly DevelopmentFirstUserIdentityDraftCopyProvider IdentityCopy =
            new DevelopmentFirstUserIdentityDraftCopyProvider();

        internal static string Breadcrumb(FirstUserGameTestPlaytestPhase phase)
        {
            switch (phase)
            {
                case FirstUserGameTestPlaytestPhase.Loading:
                    return LoadingBreadcrumb;
                case FirstUserGameTestPlaytestPhase.Identity:
                    return IdentityBreadcrumb;
                case FirstUserGameTestPlaytestPhase.AppearanceAndName:
                    return AppearanceBreadcrumb;
                case FirstUserGameTestPlaytestPhase.WorldTutorial:
                    return TutorialBreadcrumb;
                case FirstUserGameTestPlaytestPhase.Omen:
                    return OmenBreadcrumb;
                case FirstUserGameTestPlaytestPhase.SkyCastle:
                    return SkyCastleBreadcrumb;
                case FirstUserGameTestPlaytestPhase.ValeriusReturn:
                    return ValeriusReturnBreadcrumb;
                case FirstUserGameTestPlaytestPhase.RealmReady:
                    return RealmReadyBreadcrumb;
                default:
                    return LoadingBreadcrumb;
            }
        }

        internal static bool TryDescribeIdentity(
            FirstUserIdentityDraftSnapshot identity,
            out string description)
        {
            description = string.Empty;
            if (identity == null || !identity.HasRealm || !identity.HasClassFamily ||
                !IdentityCopy.TryGetRealmLabel(identity.Realm, out string realmLabel) ||
                !IdentityCopy.TryGetRaceLabel(identity.Race, out string raceLabel) ||
                !IdentityCopy.TryGetClassFamilyLabel(
                    identity.ClassFamily.Value,
                    out string classLabel))
            {
                return false;
            }

            description = realmLabel + "  •  " + raceLabel + "  •  " + classLabel;
            return true;
        }
    }
}
