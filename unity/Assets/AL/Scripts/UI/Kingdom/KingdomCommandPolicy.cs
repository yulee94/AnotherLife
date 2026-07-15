using System;
using System.Collections.Generic;

namespace AL.UI.Kingdom
{
    public static class KingdomCommandPolicy
    {
        public const string BoardView = "board.view";
        public const string TownHallUpgrade = "building.town_hall.upgrade";
        public const string FarmUpgrade = "building.farm.upgrade";
        public const string LumberMillUpgrade = "building.lumber_mill.upgrade";
        public const string QuarryUpgrade = "building.quarry.upgrade";
        public const string GoldMineUpgrade = "building.gold_mine.upgrade";
        public const string ManaShrineUpgrade = "building.mana_shrine.upgrade";
        public const string MineUpgrade = "building.mine.upgrade";
        public const string InfantryTraining = "training.infantry.start";
        public const string RangedTraining = "training.ranged.start";
        public const string QuestClaim = "quest.claim_available";
        public const string SteelResearch = "research.steel_forging.start";
        public const string ArmorResearch = "research.plate_armor.start";
        public const string WarmasterPurchase = "warmaster.purchase_next";
        public const string BorderlandsCapture = "territory.borderlands.capture";
        public const string ChampionDeploy = "champion.deploy";

        public static IReadOnlyList<KingdomCommandDescriptor> CreateDescriptors(KingdomCommandContext context)
        {
            var descriptors = new List<KingdomCommandDescriptor>
            {
                Available(BoardView, "Board View", KingdomCommandCategory.Presentation),
                Mutating(TownHallUpgrade, "Town Hall", KingdomCommandCategory.Build, context.Capabilities.BuildingUpgrade, "building-contract-missing", 165, 183),
                Mutating(FarmUpgrade, "Farm", KingdomCommandCategory.Build, context.Capabilities.BuildingUpgrade, "building-contract-missing", 165, 183),
                Mutating(LumberMillUpgrade, "Lumber", KingdomCommandCategory.Build, context.Capabilities.BuildingUpgrade, "building-contract-missing", 165, 183),
                Mutating(QuarryUpgrade, "Quarry", KingdomCommandCategory.Build, context.Capabilities.BuildingUpgrade, "building-contract-missing", 165, 183),
                Mutating(GoldMineUpgrade, "Gold Mine", KingdomCommandCategory.Build, context.Capabilities.BuildingUpgrade, "building-contract-missing", 165, 183),
                Mutating(ManaShrineUpgrade, "Mana Shrine", KingdomCommandCategory.Build, context.Capabilities.BuildingUpgrade, "building-contract-missing", 165, 183),
                Mutating(MineUpgrade, "Mine", KingdomCommandCategory.Build, context.Capabilities.BuildingUpgrade, "building-contract-missing", 165, 183),
                Mutating(InfantryTraining, "Infantry", KingdomCommandCategory.Forces, context.Capabilities.TroopTraining, "training-contract-missing", 163, 165),
                Mutating(RangedTraining, "Ranged", KingdomCommandCategory.Forces, context.Capabilities.TroopTraining, "training-contract-missing", 163, 165),
                Mutating(QuestClaim, "Claim", KingdomCommandCategory.Forces, context.Capabilities.QuestClaim, "quest-claim-contract-missing", 133, 152),
                Mutating(SteelResearch, "Steel", KingdomCommandCategory.Progression, context.Capabilities.Research, "research-contract-missing", 163, 165, 183),
                Mutating(ArmorResearch, "Armor", KingdomCommandCategory.Progression, context.Capabilities.Research, "research-contract-missing", 163, 165, 183),
                Mutating(WarmasterPurchase, "Warmaster", KingdomCommandCategory.Progression, context.Capabilities.Warmaster, "warmaster-contract-missing", 163, 171, 183),
                RealmDependent(BorderlandsCapture, "Capture", KingdomCommandCategory.RealmOps, context.Capabilities.TerritoryCapture, context.HasCommittedRealm, "territory-contract-missing", 163, 166, 173),
                RealmDependent(ChampionDeploy, "Champion", KingdomCommandCategory.RealmOps, context.Capabilities.ChampionDeployment, context.HasCommittedRealm, "champion-prerequisites-missing", 150, 173, 180)
            };

            return descriptors;
        }

        public static KingdomCommandDescriptor Resolve(string id, KingdomCommandContext context)
        {
            foreach (KingdomCommandDescriptor command in CreateDescriptors(context))
            {
                if (string.Equals(command.Id, id, StringComparison.Ordinal))
                {
                    return command;
                }
            }

            return new KingdomCommandDescriptor(
                id ?? string.Empty,
                "Unknown",
                KingdomCommandCategory.Presentation,
                KingdomCommandAvailability.Hidden,
                "unknown-command",
                Array.Empty<int>());
        }

        public static IReadOnlyList<KingdomCommandDescriptor> CreateDeckDescriptors(KingdomCommandContext context)
        {
            var descriptors = new List<KingdomCommandDescriptor>();
            foreach (KingdomCommandDescriptor command in CreateDescriptors(context))
            {
                if (command.Category != KingdomCommandCategory.Presentation && command.IsVisible)
                {
                    descriptors.Add(command);
                }
            }

            return descriptors;
        }

        private static KingdomCommandDescriptor Available(string id, string label, KingdomCommandCategory category)
        {
            return new KingdomCommandDescriptor(id, label, category, KingdomCommandAvailability.Available, string.Empty, Array.Empty<int>());
        }

        private static KingdomCommandDescriptor Mutating(string id, string label, KingdomCommandCategory category, bool capabilityEnabled, string technicalCode, params int[] blockingIssueIds)
        {
            return new KingdomCommandDescriptor(
                id,
                label,
                category,
                capabilityEnabled ? KingdomCommandAvailability.Available : KingdomCommandAvailability.UnavailableDependency,
                capabilityEnabled ? string.Empty : technicalCode,
                capabilityEnabled ? Array.Empty<int>() : blockingIssueIds);
        }

        private static KingdomCommandDescriptor RealmDependent(string id, string label, KingdomCommandCategory category, bool capabilityEnabled, bool hasCommittedRealm, string technicalCode, params int[] blockingIssueIds)
        {
            if (!hasCommittedRealm)
            {
                return new KingdomCommandDescriptor(id, label, category, KingdomCommandAvailability.UnavailableInvalidContext, "committed-realm-missing", new[] { 173 });
            }

            return Mutating(id, label, category, capabilityEnabled, technicalCode, blockingIssueIds);
        }
    }
}
