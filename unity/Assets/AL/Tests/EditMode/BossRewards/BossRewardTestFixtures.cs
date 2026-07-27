using System;
using System.Collections.Generic;
using AL.Core.BossRewards;

namespace AL.Tests.EditMode.BossRewards
{
    internal static class BossRewardTestFixtures
    {
        internal const string GameId = "another_life";
        internal const string CatalogSetId = "catalog_set_test";
        internal const string ProfileId = "profile_test";
        internal const string EncounterId = "encounter_test";
        internal const string CompletionId = "completion_test";
        internal const string ResultId = "result_test";
        internal const string BossId = "boss_test";
        internal const string BossVersion = "boss_v1";
        internal const string RewardProfileId = "reward_profile_test";
        internal const string RewardProfileVersion = "reward_v1";
        internal const string SchemaVersion = "boss_reward_schema_v1";
        internal const string InventorySchemaVersion =
            BossRewardTechnicalLimits.SupportedInventorySchemaVersion;
        internal const string SaveRevision = "save_revision_1";
        internal const string EconomyRevision = "economy_revision_1";
        internal const string InventoryRevision = "inventory_revision_1";
        internal const string LedgerRevision = "ledger_revision_1";
        internal const string ItemPolicyId = "boss_reward.item_acquired";
        internal const string CreditPolicyId = "boss_reward.credits_committed";
        internal const string NoRewardPolicyId = "boss_reward.explicit_no_reward";
        internal const string AlphaId = "equipment_alpha";
        internal const string BetaId = "equipment_beta";
        internal const string ShaA =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        internal const string ShaB =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        internal static BossRewardComputationRequest Request(
            string gameId = GameId,
            string catalogSetId = CatalogSetId,
            string profileId = ProfileId,
            string encounterId = EncounterId,
            string encounterCompletionId = CompletionId,
            string rewardResultId = ResultId,
            string bossDefinitionId = BossId,
            string bossDefinitionContentVersion = BossVersion,
            string rewardProfileId = RewardProfileId,
            string rewardProfileContentVersion = RewardProfileVersion,
            string determinismVersion = BossRewardTechnicalLimits.SupportedDeterminismVersion)
        {
            return new BossRewardComputationRequest(
                gameId,
                catalogSetId,
                profileId,
                encounterId,
                encounterCompletionId,
                rewardResultId,
                bossDefinitionId,
                bossDefinitionContentVersion,
                rewardProfileId,
                rewardProfileContentVersion,
                determinismVersion);
        }

        internal static BossEquipmentDefinitionSnapshot Equipment(
            string id = AlphaId,
            string contentVersion = "equipment_v1",
            int attackBonus = 2,
            int defenseBonus = 3,
            int healthBonus = 4,
            string rawSha256 = ShaB,
            string stackPolicyId = BossRewardStackPolicies.StackQuantity)
        {
            return new BossEquipmentDefinitionSnapshot(
                id,
                SchemaVersion,
                contentVersion,
                "trinket",
                attackBonus,
                defenseBonus,
                healthBonus,
                stackPolicyId,
                "acquisition_snapshot_v1",
                "equipment_content_" + id,
                "source_revision_1",
                rawSha256);
        }

        internal static BossRewardProfile Profile(
            IEnumerable<BossRewardEntry> entries = null,
            int credits = 25,
            bool explicitNoReward = false,
            string id = RewardProfileId,
            string contentVersion = RewardProfileVersion,
            string rawSha256 = ShaA)
        {
            return new BossRewardProfile(
                GameId,
                CatalogSetId,
                id,
                SchemaVersion,
                contentVersion,
                credits,
                explicitNoReward,
                entries ?? new[]
                {
                    new BossRewardEntry(
                        AlphaId,
                        BossRewardTechnicalLimits.MicrosPerUnit,
                        1,
                        ItemPolicyId),
                    new BossRewardEntry(BetaId, 0, 1, ItemPolicyId)
                },
                "source_revision_1",
                rawSha256);
        }

        internal static BossRewardCatalogSnapshot Catalog(
            BossRewardProfile profile = null,
            IEnumerable<BossRewardBinding> bindings = null,
            IEnumerable<BossEquipmentDefinitionSnapshot> equipment = null,
            IEnumerable<string> announcementPolicies = null)
        {
            return new BossRewardCatalogSnapshot(
                GameId,
                CatalogSetId,
                SchemaVersion,
                "catalog_revision_1",
                bindings ?? new[]
                {
                    new BossRewardBinding(
                        BossId,
                        BossVersion,
                        RewardProfileId,
                        RewardProfileVersion)
                },
                new[] { profile ?? Profile() },
                equipment ?? new[]
                {
                    Equipment(AlphaId),
                    Equipment(BetaId, "equipment_v1", 5, 6, 7, ShaA)
                },
                announcementPolicies ?? new[]
                {
                    ItemPolicyId,
                    CreditPolicyId,
                    NoRewardPolicyId
                });
        }

        internal static BossRewardComputationResult Computation(
            BossRewardComputationRequest request = null,
            BossRewardCatalogSnapshot catalog = null)
        {
            return BossRewardComputation.Compute(
                request ?? Request(),
                catalog ?? Catalog());
        }

        internal static OwnedEquipmentSnapshot Owned(
            BossEquipmentDefinitionSnapshot definition = null,
            int quantity = 2,
            string id = AlphaId,
            bool supported = true,
            string fingerprint = null)
        {
            definition = definition ?? Equipment(id);
            return new OwnedEquipmentSnapshot(
                id,
                definition.ContentVersion,
                fingerprint ??
                BossRewardComputation.ComputeAcquisitionSnapshotFingerprint(definition),
                definition.SlotId,
                definition.AttackBonus,
                definition.DefenseBonus,
                definition.HealthBonus,
                definition.StackPolicyId,
                quantity,
                10,
                20,
                BossId,
                CompletionId,
                "prior_result",
                InventorySchemaVersion,
                supported);
        }

        internal static OwnedEquipmentQueryResult Inventory(
            IEnumerable<OwnedEquipmentSnapshot> rows = null,
            BossRewardCatalogSnapshot catalog = null)
        {
            return BossRewardInventoryValidator.Validate(
                rows ?? Array.Empty<OwnedEquipmentSnapshot>(),
                InventoryRevision,
                catalog ?? Catalog(),
                InventorySchemaVersion);
        }

        internal static BossRewardLedgerSnapshot EmptyLedger()
        {
            return new BossRewardLedgerSnapshot(
                BossRewardLedgerStatus.Empty,
                LedgerRevision,
                Array.Empty<BossRewardAppliedLedgerRecord>(),
                Array.Empty<BossRewardDiagnostic>());
        }

        internal static BossRewardAppliedLedgerRecord LedgerRecord(
            BossRewardComputedValue value,
            string computationHash = null,
            BossRewardLedgerRecordState state = BossRewardLedgerRecordState.Committed)
        {
            var correlationIds = new List<string>();
            if (value.WarzoneCredits > 0)
                correlationIds.Add(value.RewardResultId + ":credits");
            for (int index = 0; index < value.Drops.Count; index++)
                correlationIds.Add(
                    value.RewardResultId +
                    ":item:" +
                    value.Drops[index].EquipmentDefinitionId);
            if (value.IsExplicitNoReward)
                correlationIds.Add(value.RewardResultId + ":no_reward");
            correlationIds.Sort(StringComparer.Ordinal);
            return new BossRewardAppliedLedgerRecord(
                value.GameId,
                value.CatalogSetId,
                value.ProfileId,
                value.RewardResultId,
                value.EncounterId,
                value.EncounterCompletionId,
                value.BossDefinitionId,
                value.BossDefinitionContentVersion,
                value.RewardProfileId,
                value.RewardProfileContentVersion,
                value.RewardProfileSha256,
                computationHash ?? value.ComputationHash,
                value.WarzoneCredits,
                value.IsExplicitNoReward,
                value.DeterminismVersion,
                value.Drops,
                100,
                BossRewardTechnicalLimits.SupportedApplicationPolicyVersion,
                correlationIds,
                state);
        }

        internal static BossRewardApplicationRequest ApplicationRequest(
            BossRewardComputationResult computation = null,
            string expectedSaveRevision = SaveRevision,
            string expectedEconomyRevision = EconomyRevision,
            string expectedInventoryRevision = InventoryRevision,
            string expectedLedgerRevision = LedgerRevision,
            string expectedCatalogSetId = CatalogSetId,
            string policyVersion =
                BossRewardTechnicalLimits.SupportedApplicationPolicyVersion)
        {
            return new BossRewardApplicationRequest(
                computation ?? Computation(),
                expectedSaveRevision,
                expectedEconomyRevision,
                expectedInventoryRevision,
                expectedLedgerRevision,
                expectedCatalogSetId,
                policyVersion);
        }

        internal static BossRewardPlanningContext PlanningContext(
            BossRewardEconomySnapshot economy = null,
            OwnedEquipmentQueryResult inventory = null,
            BossRewardLedgerSnapshot ledger = null,
            BossRewardCatalogSnapshot rewardCatalog = null,
            IEnumerable<string> notificationDefinitions = null,
            string saveRevision = SaveRevision,
            string gameId = GameId,
            string profileId = ProfileId,
            string catalogSetId = CatalogSetId,
            long plannedUtcSeconds = 100)
        {
            return new BossRewardPlanningContext(
                true,
                saveRevision,
                gameId,
                profileId,
                catalogSetId,
                rewardCatalog ?? Catalog(),
                economy ?? new BossRewardEconomySnapshot(
                    true,
                    10,
                    int.MaxValue,
                    EconomyRevision),
                inventory ?? Inventory(),
                ledger ?? EmptyLedger(),
                notificationDefinitions ?? new[]
                {
                    ItemPolicyId,
                    CreditPolicyId,
                    NoRewardPolicyId
                },
                plannedUtcSeconds);
        }
    }
}
