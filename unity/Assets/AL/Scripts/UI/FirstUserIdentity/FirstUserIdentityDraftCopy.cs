using System;
using AL.Core;

namespace AL.UI.FirstUserIdentity
{
    public interface IFirstUserIdentityDraftCopyProvider
    {
        string Disclosure { get; }
        string Title { get; }
        string RealmHeading { get; }
        string RealmInstruction { get; }
        string RealmSelectionRequired { get; }
        string ConfirmRealmAction { get; }
        string ClassHeading { get; }
        string ClassInstruction { get; }
        string ClassSelectionRequired { get; }
        string ReturnToRealmAction { get; }
        string ConfirmDraftAction { get; }
        string CustomizationReadyHeading { get; }

        bool TryGetRealmLabel(RealmId realm, out string label);
        bool TryGetRaceLabel(FirstUserRace race, out string label);
        bool TryGetClassFamilyLabel(ClassFamily classFamily, out string label);
        string SelectedChoice(string choiceLabel);
        string RealmAndRaceSummary(string realmLabel, string raceLabel);
        string ClassSummary(string classLabel);
        string CustomizationReadySummary(
            string realmLabel,
            string raceLabel,
            string classLabel);
    }

    /// <summary>
    /// Deterministic nonproduction copy for the isolated playable test slice.
    /// It deliberately maps every supported enum to authored preview text and
    /// never falls back to enum names, numeric values, or machine identifiers.
    /// </summary>
    public sealed class DevelopmentFirstUserIdentityDraftCopyProvider :
        IFirstUserIdentityDraftCopyProvider
    {
        public string Disclosure => "DEVELOPMENT PREVIEW — IDENTITY DRAFT IS NOT SAVED";
        public string Title => "Shape your beginning";
        public string RealmHeading => "1 of 2 · Choose a realm";
        public string RealmInstruction =>
            "Choose the realm your first Champion will call home. Its people are set by the realm.";
        public string RealmSelectionRequired =>
            "Choose a realm to continue.";
        public string ConfirmRealmAction => "Continue to class";
        public string ClassHeading => "2 of 2 · Choose a class";
        public string ClassInstruction =>
            "Choose how your first Champion will fight.";
        public string ClassSelectionRequired =>
            "Choose a class to continue.";
        public string ReturnToRealmAction => "Change realm";
        public string ConfirmDraftAction => "Continue to appearance";
        public string CustomizationReadyHeading => "Your origin is ready";

        public bool TryGetRealmLabel(RealmId realm, out string label)
        {
            switch (realm)
            {
                case RealmId.Crownlands:
                    label = "Crownlands realm";
                    return true;
                case RealmId.Stonehold:
                    label = "Stonehold realm";
                    return true;
                case RealmId.Eldergrove:
                    label = "Eldergrove realm";
                    return true;
                case RealmId.Umbral:
                    label = "Umbral realm";
                    return true;
                default:
                    label = string.Empty;
                    return false;
            }
        }

        public bool TryGetRaceLabel(FirstUserRace race, out string label)
        {
            switch (race)
            {
                case FirstUserRace.Humans:
                    label = "Human heritage";
                    return true;
                case FirstUserRace.Dwarves:
                    label = "Dwarven heritage";
                    return true;
                case FirstUserRace.Elves:
                    label = "Elven heritage";
                    return true;
                case FirstUserRace.DarkElves:
                    label = "Dark Elven heritage";
                    return true;
                default:
                    label = string.Empty;
                    return false;
            }
        }

        public bool TryGetClassFamilyLabel(ClassFamily classFamily, out string label)
        {
            switch (classFamily)
            {
                case ClassFamily.Warrior:
                    label = "Warrior path";
                    return true;
                case ClassFamily.Mage:
                    label = "Mage path";
                    return true;
                case ClassFamily.Ranger:
                    label = "Ranger path";
                    return true;
                case ClassFamily.Assassin:
                    label = "Assassin path";
                    return true;
                default:
                    label = string.Empty;
                    return false;
            }
        }

        public string SelectedChoice(string choiceLabel)
        {
            return "Selected: " + RequireVisibleCopy(choiceLabel, nameof(choiceLabel));
        }

        public string RealmAndRaceSummary(string realmLabel, string raceLabel)
        {
            return "Realm: " + RequireVisibleCopy(realmLabel, nameof(realmLabel)) +
                   "\nPeople: " + RequireVisibleCopy(raceLabel, nameof(raceLabel));
        }

        public string ClassSummary(string classLabel)
        {
            return "Class: " + RequireVisibleCopy(classLabel, nameof(classLabel));
        }

        public string CustomizationReadySummary(
            string realmLabel,
            string raceLabel,
            string classLabel)
        {
            return "Origin ready: " + RequireVisibleCopy(realmLabel, nameof(realmLabel)) +
                   " • " + RequireVisibleCopy(raceLabel, nameof(raceLabel)) +
                   " • " + RequireVisibleCopy(classLabel, nameof(classLabel)) +
                   ". Nothing has been saved.";
        }

        private static string RequireVisibleCopy(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Visible preview copy is required.", parameterName);
            }

            return value;
        }
    }
}
