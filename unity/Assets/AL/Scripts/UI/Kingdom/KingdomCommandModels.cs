using System;
using System.Collections.Generic;

namespace AL.UI.Kingdom
{
    public enum KingdomCommandCategory
    {
        Presentation,
        Build,
        Forces,
        Progression,
        RealmOps
    }

    public enum KingdomCommandAvailability
    {
        Available,
        UnavailableDependency,
        UnavailableInvalidContext,
        UnavailableBuild,
        Hidden
    }

    public readonly struct KingdomCommandDescriptor
    {
        public KingdomCommandDescriptor(
            string id,
            string label,
            KingdomCommandCategory category,
            KingdomCommandAvailability availability,
            string technicalCode,
            IReadOnlyList<int> blockingIssueIds)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Category = category;
            Availability = availability;
            TechnicalCode = technicalCode ?? string.Empty;
            BlockingIssueIds = blockingIssueIds ?? Array.Empty<int>();
        }

        public string Id { get; }
        public string Label { get; }
        public KingdomCommandCategory Category { get; }
        public KingdomCommandAvailability Availability { get; }
        public string TechnicalCode { get; }
        public IReadOnlyList<int> BlockingIssueIds { get; }
        public bool IsVisible => Availability != KingdomCommandAvailability.Hidden;
        public bool IsInteractable => Availability == KingdomCommandAvailability.Available;
    }

    public readonly struct KingdomCommandCapabilities
    {
        public KingdomCommandCapabilities(
            bool buildingUpgrade = false,
            bool troopTraining = false,
            bool questClaim = false,
            bool research = false,
            bool warmaster = false,
            bool territoryCapture = false,
            bool championDeployment = false)
        {
            BuildingUpgrade = buildingUpgrade;
            TroopTraining = troopTraining;
            QuestClaim = questClaim;
            Research = research;
            Warmaster = warmaster;
            TerritoryCapture = territoryCapture;
            ChampionDeployment = championDeployment;
        }

        public bool BuildingUpgrade { get; }
        public bool TroopTraining { get; }
        public bool QuestClaim { get; }
        public bool Research { get; }
        public bool Warmaster { get; }
        public bool TerritoryCapture { get; }
        public bool ChampionDeployment { get; }
    }

    public readonly struct KingdomCommandContext
    {
        public KingdomCommandContext(bool hasCommittedRealm, KingdomCommandCapabilities capabilities)
        {
            HasCommittedRealm = hasCommittedRealm;
            Capabilities = capabilities;
        }

        public bool HasCommittedRealm { get; }
        public KingdomCommandCapabilities Capabilities { get; }
    }
}
