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
        public const string BarracksUpgrade = "building.barracks.upgrade";
        public const string InfantryTraining = "training.infantry.start";
        public const string RangedTraining = "training.ranged.start";
        public const string QuestClaim = "quest.claim_available";
        public const string SteelResearch = "research.steel_forging.start";
        public const string ArmorResearch = "research.plate_armor.start";
        public const string WarmasterPurchase = "warmaster.purchase_next";
        public const string BorderlandsCapture = "territory.borderlands.capture";
        public const string ChampionDeploy = "champion.deploy";
        public const string GreyboxDuel = "champion.greybox_duel";

        // Blocking-issue diagnostics are re-derived from the spec section 15 reconnection matrix
        // and the LIVE GitHub issue state at implementation time (2026-07-21), not copied from the
        // stale post-merge audit table (#178):
        //   - #137 (save hardening) is required by every mutation family except Champion (spec 15).
        //   - research / training share {137, 163, 165, 183}.
        //   - Warmaster {137, 163, 171, 183}; territory capture {137, 163, 166, 173};
        //     quest claim {133, 137} (spec-15 quest set was {137, 152, 133}, but #152 is CLOSED and
        //     therefore no longer blocking, so it is dropped — see KingdomReleaseContainmentTests);
        //     Champion {150, 173, 180}.
        // Building construction is now live through the save/economy/progression/game-data contracts,
        // so supported definitions carry no issue blocker. Runtime profile/service availability still
        // fails closed. Only issues that are OPEN (still blocking) are listed. #152/#156/#127/#223 are CLOSED and
        // must never appear here; a listed closed issue is a diagnostics honesty defect that the
        // live-state test fails on. mana_shrine / mine stay as permanently-unavailable deck entries
        // (D9) rather than being dropped — the catalog defines no such building ids yet (#183).
        public static IReadOnlyList<KingdomCommandDescriptor> CreateDescriptors(KingdomCommandContext context)
        {
            var descriptors = new List<KingdomCommandDescriptor>
            {
                Available(BoardView, "Board View", KingdomCommandCategory.Presentation),
                BuildingCommand(TownHallUpgrade, "Town Hall", context.Capabilities.BuildingUpgrade),
                BuildingCommand(FarmUpgrade, "Farm", context.Capabilities.BuildingUpgrade),
                BuildingCommand(LumberMillUpgrade, "Lumber", context.Capabilities.BuildingUpgrade),
                BuildingCommand(QuarryUpgrade, "Quarry", context.Capabilities.BuildingUpgrade),
                BuildingCommand(GoldMineUpgrade, "Gold Mine", context.Capabilities.BuildingUpgrade),
                UnsupportedBuild(ManaShrineUpgrade, "Mana Shrine"),
                UnsupportedBuild(MineUpgrade, "Mine"),
                BuildingCommand(BarracksUpgrade, "Barracks", context.Capabilities.BuildingUpgrade),
                Mutating(InfantryTraining, "Infantry", KingdomCommandCategory.Forces, context.Capabilities.TroopTraining, "training-contract-missing", 137, 163, 165, 183),
                Mutating(RangedTraining, "Ranged", KingdomCommandCategory.Forces, context.Capabilities.TroopTraining, "training-contract-missing", 137, 163, 165, 183),
                Mutating(QuestClaim, "Claim", KingdomCommandCategory.Forces, context.Capabilities.QuestClaim, "quest-claim-contract-missing", 133, 137),
                Mutating(SteelResearch, "Steel", KingdomCommandCategory.Progression, context.Capabilities.Research, "research-contract-missing", 137, 163, 165, 183),
                Mutating(ArmorResearch, "Armor", KingdomCommandCategory.Progression, context.Capabilities.Research, "research-contract-missing", 137, 163, 165, 183),
                Mutating(WarmasterPurchase, "Warmaster", KingdomCommandCategory.Progression, context.Capabilities.Warmaster, "warmaster-contract-missing", 137, 163, 171, 183),
                Available(GreyboxDuel, "Duel", KingdomCommandCategory.RealmOps),
                RealmDependent(BorderlandsCapture, "Capture", KingdomCommandCategory.RealmOps, context.Capabilities.TerritoryCapture, context.HasCommittedRealm, "territory-contract-missing", 137, 163, 166, 173),
                RealmDependent(ChampionDeploy, "Champion", KingdomCommandCategory.RealmOps, context.Capabilities.ChampionDeployment, context.HasCommittedRealm, "champion-prerequisites-missing", 150, 173, 180)
            };

            return descriptors;
        }

        public static bool TryGetBuildingId(string commandId, out string buildingId)
        {
            switch (commandId)
            {
                case TownHallUpgrade: buildingId = "TownHall"; return true;
                case FarmUpgrade: buildingId = "Farm"; return true;
                case LumberMillUpgrade: buildingId = "LumberMill"; return true;
                case QuarryUpgrade: buildingId = "Quarry"; return true;
                case GoldMineUpgrade: buildingId = "GoldMine"; return true;
                case BarracksUpgrade: buildingId = "Barracks"; return true;
                default:
                    buildingId = string.Empty;
                    return false;
            }
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

        private static KingdomCommandDescriptor UnsupportedBuild(string id, string label)
        {
            return new KingdomCommandDescriptor(
                id,
                label,
                KingdomCommandCategory.Build,
                KingdomCommandAvailability.UnavailableBuild,
                "building-definition-unavailable",
                Array.Empty<int>());
        }

        private static KingdomCommandDescriptor BuildingCommand(
            string id,
            string label,
            bool capabilityEnabled)
        {
            return new KingdomCommandDescriptor(
                id,
                label,
                KingdomCommandCategory.Build,
                capabilityEnabled
                    ? KingdomCommandAvailability.Available
                    : KingdomCommandAvailability.UnavailableDependency,
                capabilityEnabled ? string.Empty : "building-runtime-unavailable",
                Array.Empty<int>());
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
