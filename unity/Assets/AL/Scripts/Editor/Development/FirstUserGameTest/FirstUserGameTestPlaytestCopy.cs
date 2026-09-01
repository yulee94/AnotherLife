#if !UNITY_EDITOR
#error The isolated first-user playtest copy is Editor-only.
#endif

using AL.Core;
using AL.UI.FirstUserIdentity;
using AL.UI.Kingdom;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal enum FirstUserGameTestPlaytestPhase
    {
        Invalid = 0,
        Loading = 1,
        Identity = 2,
        AppearanceAndName = 3,
        WorldTutorial = 4,
        Omen = 5
    }

    internal static class FirstUserGameTestPlaytestCopy
    {
        internal const string NonProductionBadge =
            "ISOLATED EDITOR PLAYTEST — NOT PRODUCTION — SESSION ONLY";
        internal const string LoadingBreadcrumb =
            "[Loading]  >  Identity  >  Appearance & Name  >  World Tutorial  >  OMEN";
        internal const string IdentityBreadcrumb =
            "Loading  >  [Identity]  >  Appearance & Name  >  World Tutorial  >  OMEN";
        internal const string AppearanceBreadcrumb =
            "Loading  >  Identity  >  [Appearance & Name]  >  World Tutorial  >  OMEN";
        internal const string TutorialBreadcrumb =
            "Loading  >  Identity  >  Appearance & Name  >  [World Tutorial]  >  OMEN";
        internal const string OmenBreadcrumb =
            "Loading  >  Identity  >  Appearance & Name  >  World Tutorial  >  [OMEN]";

        internal const string AppearanceHeading =
            "Choose your appearance and playtest name";
        internal const string NamePlaceholder = "Enter a playtest name";
        internal const string AppearancePrompt =
            "Choose an appearance, then enter a playtest name.";
        internal const string AppearanceRequired = "Choose an appearance to continue.";
        internal const string NameRequired =
            "Enter a short playtest name without leading or trailing spaces.";
        internal const string ReadyForTutorial = "Ready to enter the world tutorial.";
        internal const string PreparingWorld = "Preparing your isolated world tutorial…";
        internal const string FriendlyBlockedStatus =
            "The isolated playtest could not continue. Review the Console, then exit safely.";
        internal const string FriendlyFailurePanel =
            "ISOLATED PLAYTEST PAUSED\n\nSomething needed attention, so the playtest stopped safely.\n\nChoose Exit Isolated Test or stop Play Mode. Your normal save was not opened or changed.";
        internal const string ExitAction = "Exit Isolated Test";
        internal const string ExitingStatus = "Closing the isolated playtest safely…";

        internal const string MoveTitle = "First Steps";
        internal const string MoveObjective = "Move your character";
        internal const string MoveDetail =
            "Use movement keys, a controller, or the on-screen arrows.";
        internal const string AttackTitle = "First Steps";
        internal const string AttackObjective = "Use Basic Attack";
        internal const string AttackDetail =
            "Use the action key or the on-screen Basic Attack button.";
        internal const string OmenTitle = "A New Quest Awaits";
        internal const string OmenObjective = "Open the quest to review it";
        internal const string OmenDetail =
            "A new quest is available—open it to review.";
        internal const string OmenFocusDetail =
            "Quest details are in focus. Review them when you are ready.";
        internal const string NoSafeTargetDetail =
            "Quest details are not available right now.";
        internal const string HearValeriusReportAction =
            "Hear Valerius's report";
        internal const string ValeriusReportOpenObjective =
            "Valerius's report is open";
        internal const string ValeriusReportPendingNotice =
            "The report is open for review. Quest acceptance is intentionally unavailable in this playtest step.";

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

        internal static bool TryBuildOmenOfferDetails(
            Nvs01KingdomView view,
            out string details)
        {
            details = string.Empty;
            if (!IsFriendlyReadyView(view) ||
                string.IsNullOrWhiteSpace(view.Description) ||
                string.IsNullOrWhiteSpace(view.ObjectiveText) ||
                string.IsNullOrWhiteSpace(view.SpeakerName) ||
                string.IsNullOrWhiteSpace(view.SpeakerRole) ||
                view.HasDialogue)
            {
                return false;
            }

            details = view.SpeakerName + " — " + view.SpeakerRole + "\n" +
                      view.Description + "\n" + view.ObjectiveText;
            return true;
        }

        internal static bool TryBuildValeriusReport(
            Nvs01KingdomView view,
            out string details)
        {
            details = string.Empty;
            if (!IsFriendlyReadyView(view) ||
                string.IsNullOrWhiteSpace(view.SpeakerName) ||
                string.IsNullOrWhiteSpace(view.DialogueText) ||
                !view.HasDialogue)
            {
                return false;
            }

            details = view.SpeakerName + "\n" + view.DialogueText + "\n\n" +
                      ValeriusReportPendingNotice;
            return true;
        }

        private static bool IsFriendlyReadyView(Nvs01KingdomView view)
        {
            return view != null &&
                   view.Status == Nvs01KingdomViewStatus.Ready &&
                   !view.HasDiagnostic &&
                   string.IsNullOrEmpty(view.PlayerMessage);
        }
    }
}
