using AL.ChampionMode.Quests;
using AL.UI.Kingdom;
using AL.UI.SharedMenu;
using AL.World;

namespace AL.UI.QuestHud
{
    public enum QuestHudAction
    {
        None = 0,
        Accept = 1,
        Continue = 2,
        Complete = 3
    }

    public enum QuestHudSurface
    {
        InnerRealm3D = 0,
        Kingdom25D = 1,
        WarzoneGate = 2
    }

    public sealed class QuestHudModel
    {
        public QuestHudModel(
            string title,
            string whatToDo,
            string locationName,
            string locationKey,
            string stepId,
            QuestHudAction action,
            QuestHudSurface surface,
            bool autoQuestOn)
        {
            Title = title ?? string.Empty;
            WhatToDo = whatToDo ?? string.Empty;
            LocationName = locationName ?? string.Empty;
            LocationKey = locationKey ?? string.Empty;
            StepId = stepId ?? string.Empty;
            Action = action;
            Surface = surface;
            AutoQuestOn = autoQuestOn;
        }

        public string Title { get; }
        public string WhatToDo { get; }
        public string LocationName { get; }
        public string LocationKey { get; }
        public string StepId { get; }
        public QuestHudAction Action { get; }
        public QuestHudSurface Surface { get; }
        public bool AutoQuestOn { get; }
        public bool IsWarzoneGate => Surface == QuestHudSurface.WarzoneGate;
        public bool CanAutoFire => AutoQuestOn && !IsWarzoneGate && Action != QuestHudAction.None;
        public string ActionLabel => LabelFor(Action);
        public string AutoQuestLabel => AutoQuestOn ? QuestHudCopy.AutoQuestOn : QuestHudCopy.AutoQuestOff;

        public static string LabelFor(QuestHudAction action)
        {
            switch (action)
            {
                case QuestHudAction.Accept:
                    return QuestHudCopy.Accept;
                case QuestHudAction.Continue:
                    return QuestHudCopy.Continue;
                case QuestHudAction.Complete:
                    return QuestHudCopy.Complete;
                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>
    /// Maps the live main-quest / teaching step to HUD chrome. Location names are
    /// inner-realm places or 2.5D Castle/Areas — never an outer/Warzone id before
    /// the gate prompt.
    /// </summary>
    public static class QuestHudPlanner
    {
        public static QuestHudModel FromProofOfWorth(ProofOfWorthState state, bool autoQuestOn)
        {
            if (state == null)
            {
                return Empty(autoQuestOn);
            }

            string title = state.QuestId == ProofOfWorthIds.MainQuestId
                ? ProofOfWorthCopy.C1Title
                : ProofOfWorthCopy.OmenTitle;
            string what = ProofOfWorthCopy.ObjectiveText(state);
            string location = LocationFor(state);
            return new QuestHudModel(
                title,
                what,
                location,
                LocationKeyFor(state),
                state.Phase + ":" + state.DialogueId,
                ActionFor(state),
                QuestHudSurface.InnerRealm3D,
                autoQuestOn);
        }

        public static QuestHudModel TeachingStores(bool autoQuestOn)
        {
            return new QuestHudModel(
                QuestHudCopy.TeachStoresTitle,
                QuestHudCopy.TeachStoresWhat,
                QuestHudCopy.Castle,
                QuestHudCopy.TeachStoresId,
                QuestHudCopy.TeachStoresId,
                QuestHudAction.Continue,
                QuestHudSurface.Kingdom25D,
                autoQuestOn);
        }

        public static QuestHudModel FromKingdomTeachingEntry(
            KingdomTeachingEntry entry,
            bool autoQuestOn)
        {
            if (entry == null)
            {
                return Empty(autoQuestOn);
            }

            return new QuestHudModel(
                entry.Title,
                entry.WhatToDo,
                SanitizeLocation(entry.Location),
                entry.Id,
                entry.Id,
                QuestHudAction.Continue,
                QuestHudSurface.InnerRealm3D,
                autoQuestOn);
        }

        public static QuestHudModel FromKingdomTeaching(
            KingdomTeachingStep step,
            bool autoQuestOn)
        {
            if (step == null)
            {
                return Empty(autoQuestOn);
            }

            QuestHudAction action = string.Equals(
                step.Action,
                "complete",
                System.StringComparison.Ordinal)
                ? QuestHudAction.Complete
                : QuestHudAction.Continue;
            return new QuestHudModel(
                step.Title,
                step.WhatToDo,
                step.Location,
                step.Id,
                step.Id,
                action,
                QuestHudSurface.Kingdom25D,
                autoQuestOn);
        }

        public static QuestHudModel WarzoneGate(bool autoQuestOn)
        {
            return new QuestHudModel(
                QuestHudCopy.WarzoneGateTitle,
                QuestHudCopy.WarzoneGateWhat,
                QuestHudCopy.WarzoneGate,
                QuestHudCopy.WarzoneGateId,
                QuestHudCopy.WarzoneGateId,
                QuestHudAction.None,
                QuestHudSurface.WarzoneGate,
                autoQuestOn);
        }

        public static string SanitizeLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return string.Empty;
            }

            if (IsForbiddenLocationId(location))
            {
                return string.Empty;
            }

            return location;
        }

        public static bool IsForbiddenLocationId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (string.Equals(value, QuestHudCopy.WarzoneGate, System.StringComparison.Ordinal) ||
                string.Equals(value, QuestHudCopy.WarzoneGateId, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (value.IndexOf("warzone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("outer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("accordant", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (value.StartsWith("zone_inner_", System.StringComparison.Ordinal) ||
                value.StartsWith("poi_zone_inner_", System.StringComparison.Ordinal) ||
                value.StartsWith("OBJ_", System.StringComparison.Ordinal) ||
                value.StartsWith("LOCATION_", System.StringComparison.Ordinal) ||
                value.StartsWith("ch01_", System.StringComparison.Ordinal))
            {
                return true;
            }

            return !PrivateKingdomInnerDestinations.IsAllowed(value) &&
                   value.IndexOf('_') >= 0 &&
                   value.Equals(value.ToLowerInvariant(), System.StringComparison.Ordinal);
        }

        public static bool CopyLooksLikeId(string copy)
        {
            if (string.IsNullOrEmpty(copy))
            {
                return false;
            }

            return copy.IndexOf("OBJ_", System.StringComparison.Ordinal) >= 0 ||
                   copy.IndexOf("OMEN_1", System.StringComparison.Ordinal) >= 0 ||
                   copy.IndexOf("MQ_C1", System.StringComparison.Ordinal) >= 0 ||
                   copy.IndexOf("warzone_center", System.StringComparison.Ordinal) >= 0 ||
                   copy.IndexOf("zone_inner_", System.StringComparison.Ordinal) >= 0 ||
                   copy.IndexOf("zone_outer_", System.StringComparison.Ordinal) >= 0 ||
                   copy.IndexOf("poi_zone_", System.StringComparison.Ordinal) >= 0 ||
                   copy.IndexOf(FirstSessionInnerRealmSpawn.WarzoneCenterId, System.StringComparison.Ordinal) >= 0;
        }

        private static QuestHudModel Empty(bool autoQuestOn)
        {
            return new QuestHudModel(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                QuestHudAction.None,
                QuestHudSurface.InnerRealm3D,
                autoQuestOn);
        }

        private static QuestHudAction ActionFor(ProofOfWorthState state)
        {
            switch (state.Phase)
            {
                case ProofOfWorthPhase.OmenOffered:
                    return QuestHudAction.Accept;
                case ProofOfWorthPhase.OmenReport:
                case ProofOfWorthPhase.C1AcceptMark:
                    return QuestHudAction.Complete;
                case ProofOfWorthPhase.LordshipGranted:
                    return QuestHudAction.None;
                default:
                    return QuestHudAction.Continue;
            }
        }

        private static string LocationFor(ProofOfWorthState state)
        {
            switch (state.Phase)
            {
                case ProofOfWorthPhase.OmenArena:
                case ProofOfWorthPhase.OmenFailed:
                    return QuestHudCopy.SkyCastle;
                case ProofOfWorthPhase.C1MeetGuide:
                    return QuestHudCopy.RealmGuide;
                case ProofOfWorthPhase.C1RestoreCovenant:
                case ProofOfWorthPhase.C1FaceGuardian:
                    return QuestHudCopy.CovenantSite;
                default:
                    return QuestHudCopy.Capital;
            }
        }

        private static string LocationKeyFor(ProofOfWorthState state)
        {
            switch (state.Phase)
            {
                case ProofOfWorthPhase.OmenArena:
                case ProofOfWorthPhase.OmenFailed:
                    return "sky_castle";
                case ProofOfWorthPhase.C1MeetGuide:
                    return "realm_guide";
                case ProofOfWorthPhase.C1RestoreCovenant:
                case ProofOfWorthPhase.C1FaceGuardian:
                    return "covenant_site";
                default:
                    return "capital";
            }
        }
    }
}
