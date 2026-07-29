using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Core.BossRewards
{
    public enum BossRewardLedgerRecordState
    {
        Committed = 0,
        PendingRecovery = 1
    }

    public sealed class BossRewardAppliedLedgerRecord
    {
        public BossRewardAppliedLedgerRecord(
            string gameId,
            string catalogSetId,
            string profileId,
            string rewardResultId,
            string encounterId,
            string encounterCompletionId,
            string bossDefinitionId,
            string bossDefinitionContentVersion,
            string rewardProfileId,
            string rewardProfileContentVersion,
            string rewardProfileSha256,
            string computationHash,
            int warzoneCredits,
            bool isExplicitNoReward,
            string determinismVersion,
            IEnumerable<BossRewardComputedDrop> committedDrops,
            long committedUtcSeconds,
            string applicationPolicyVersion,
            IEnumerable<string> notificationCorrelationIds,
            BossRewardLedgerRecordState state)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            ProfileId = profileId;
            RewardResultId = rewardResultId;
            EncounterId = encounterId;
            EncounterCompletionId = encounterCompletionId;
            BossDefinitionId = bossDefinitionId;
            BossDefinitionContentVersion = bossDefinitionContentVersion;
            RewardProfileId = rewardProfileId;
            RewardProfileContentVersion = rewardProfileContentVersion;
            RewardProfileSha256 = rewardProfileSha256;
            ComputationHash = computationHash;
            WarzoneCredits = warzoneCredits;
            IsExplicitNoReward = isExplicitNoReward;
            DeterminismVersion = determinismVersion;
            CommittedDrops = BossRewardImmutable.Freeze(
                committedDrops,
                BossRewardTechnicalLimits.MaximumRewardEntries);
            CommittedUtcSeconds = committedUtcSeconds;
            ApplicationPolicyVersion = applicationPolicyVersion;
            NotificationCorrelationIds = BossRewardImmutable.Freeze(
                notificationCorrelationIds,
                BossRewardTechnicalLimits.MaximumRewardEntries + 1);
            State = state;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string ProfileId { get; }
        public string RewardResultId { get; }
        public string EncounterId { get; }
        public string EncounterCompletionId { get; }
        public string BossDefinitionId { get; }
        public string BossDefinitionContentVersion { get; }
        public string RewardProfileId { get; }
        public string RewardProfileContentVersion { get; }
        public string RewardProfileSha256 { get; }
        public string ComputationHash { get; }
        public int WarzoneCredits { get; }
        public bool IsExplicitNoReward { get; }
        public string DeterminismVersion { get; }
        public IReadOnlyList<BossRewardComputedDrop> CommittedDrops { get; }
        public long CommittedUtcSeconds { get; }
        public string ApplicationPolicyVersion { get; }
        public IReadOnlyList<string> NotificationCorrelationIds { get; }
        public BossRewardLedgerRecordState State { get; }
    }

    public enum BossRewardLedgerStatus
    {
        Valid = 0,
        Empty = 1,
        Unavailable = 2,
        Malformed = 3
    }

    public sealed class BossRewardLedgerSnapshot
    {
        public BossRewardLedgerSnapshot(
            string gameId,
            string profileId,
            BossRewardLedgerStatus status,
            string revision,
            IEnumerable<BossRewardAppliedLedgerRecord> records,
            IEnumerable<BossRewardDiagnostic> diagnostics,
            bool isComplete)
        {
            GameId = gameId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            Status = status;
            Revision = revision ?? string.Empty;
            Records = BossRewardImmutable.Freeze(
                records,
                BossRewardTechnicalLimits.MaximumLedgerRows);
            Diagnostics = BossRewardDiagnosticOrdering.Order(diagnostics);
            IsComplete = isComplete;
        }

        public string GameId { get; }
        public string ProfileId { get; }
        public BossRewardLedgerStatus Status { get; }
        public string Revision { get; }
        public IReadOnlyList<BossRewardAppliedLedgerRecord> Records { get; }
        public IReadOnlyList<BossRewardDiagnostic> Diagnostics { get; }
        public bool IsComplete { get; }
        public bool IsUsable =>
            IsComplete &&
            (Status == BossRewardLedgerStatus.Valid ||
             Status == BossRewardLedgerStatus.Empty);
    }

    public sealed class BossRewardEconomySnapshot
    {
        public BossRewardEconomySnapshot(
            bool isAvailable,
            int warzoneCredits,
            int maximumWarzoneCredits,
            string revision)
        {
            IsAvailable = isAvailable;
            WarzoneCredits = warzoneCredits;
            MaximumWarzoneCredits = maximumWarzoneCredits;
            Revision = revision;
        }

        public bool IsAvailable { get; }
        public int WarzoneCredits { get; }
        public int MaximumWarzoneCredits { get; }
        public string Revision { get; }
    }

    public sealed class BossRewardApplicationRequest
    {
        public BossRewardApplicationRequest(
            BossRewardComputationResult computation,
            string expectedSaveRevision,
            string expectedEconomyRevision,
            string expectedInventoryRevision,
            string expectedLedgerRevision,
            string expectedCatalogSetId,
            string applicationPolicyVersion)
        {
            Computation = computation;
            ExpectedSaveRevision = expectedSaveRevision;
            ExpectedEconomyRevision = expectedEconomyRevision;
            ExpectedInventoryRevision = expectedInventoryRevision;
            ExpectedLedgerRevision = expectedLedgerRevision;
            ExpectedCatalogSetId = expectedCatalogSetId;
            ApplicationPolicyVersion = applicationPolicyVersion;
        }

        public BossRewardComputationResult Computation { get; }
        public string ExpectedSaveRevision { get; }
        public string ExpectedEconomyRevision { get; }
        public string ExpectedInventoryRevision { get; }
        public string ExpectedLedgerRevision { get; }
        public string ExpectedCatalogSetId { get; }
        public string ApplicationPolicyVersion { get; }
    }

    public sealed class BossRewardPlanningContext
    {
        public BossRewardPlanningContext(
            bool isSaveAvailable,
            string saveRevision,
            string gameId,
            string profileId,
            string catalogSetId,
            BossRewardCatalogSnapshot rewardCatalog,
            BossRewardEconomySnapshot economy,
            OwnedEquipmentQueryResult inventory,
            BossRewardLedgerSnapshot ledger,
            IEnumerable<string> availableNotificationDefinitionIds,
            long plannedUtcSeconds)
        {
            IsSaveAvailable = isSaveAvailable;
            SaveRevision = saveRevision;
            GameId = gameId;
            ProfileId = profileId;
            CatalogSetId = catalogSetId;
            RewardCatalog = rewardCatalog;
            Economy = economy;
            Inventory = inventory;
            Ledger = ledger;
            AvailableNotificationDefinitionIds = BossRewardImmutable.Freeze(
                availableNotificationDefinitionIds,
                BossRewardTechnicalLimits.MaximumCatalogEntries);
            PlannedUtcSeconds = plannedUtcSeconds;
        }

        public bool IsSaveAvailable { get; }
        public string SaveRevision { get; }
        public string GameId { get; }
        public string ProfileId { get; }
        public string CatalogSetId { get; }
        public BossRewardCatalogSnapshot RewardCatalog { get; }
        public BossRewardEconomySnapshot Economy { get; }
        public OwnedEquipmentQueryResult Inventory { get; }
        public BossRewardLedgerSnapshot Ledger { get; }
        public IReadOnlyList<string> AvailableNotificationDefinitionIds { get; }
        public long PlannedUtcSeconds { get; }
    }

    public enum BossRewardInventoryOperationKind
    {
        Create = 0,
        Update = 1
    }

    public sealed class BossRewardInventoryOperation
    {
        public BossRewardInventoryOperation(
            BossRewardInventoryOperationKind kind,
            string equipmentDefinitionId,
            int previousQuantity,
            int quantityDelta,
            int newQuantity,
            OwnedEquipmentSnapshot candidateRow)
        {
            Kind = kind;
            EquipmentDefinitionId = equipmentDefinitionId;
            PreviousQuantity = previousQuantity;
            QuantityDelta = quantityDelta;
            NewQuantity = newQuantity;
            CandidateRow = candidateRow;
        }

        public BossRewardInventoryOperationKind Kind { get; }
        public string EquipmentDefinitionId { get; }
        public int PreviousQuantity { get; }
        public int QuantityDelta { get; }
        public int NewQuantity { get; }
        public OwnedEquipmentSnapshot CandidateRow { get; }
    }

    public sealed class BossRewardCreditOperation
    {
        public BossRewardCreditOperation(int previous, int delta, int next)
        {
            Previous = previous;
            Delta = delta;
            Next = next;
        }

        public int Previous { get; }
        public int Delta { get; }
        public int Next { get; }
    }

    public sealed class BossRewardNotificationIntent
    {
        public BossRewardNotificationIntent(
            string definitionId,
            string correlationId,
            string equipmentDefinitionId)
        {
            DefinitionId = definitionId;
            CorrelationId = correlationId;
            EquipmentDefinitionId = equipmentDefinitionId;
        }

        public string DefinitionId { get; }
        public string CorrelationId { get; }
        public string EquipmentDefinitionId { get; }
    }

    public sealed class BossRewardPostCommitEvent
    {
        public BossRewardPostCommitEvent(
            string eventId,
            string correlationId,
            string rewardResultId)
        {
            EventId = eventId;
            CorrelationId = correlationId;
            RewardResultId = rewardResultId;
        }

        public string EventId { get; }
        public string CorrelationId { get; }
        public string RewardResultId { get; }
    }

    public sealed class BossRewardApplicationPlan
    {
        public BossRewardApplicationPlan(
            string rewardResultId,
            string computationHash,
            string expectedSaveRevision,
            string expectedEconomyRevision,
            string expectedInventoryRevision,
            string expectedLedgerRevision,
            string expectedCatalogSetId,
            BossRewardCreditOperation creditOperation,
            IEnumerable<BossRewardInventoryOperation> inventoryOperations,
            BossRewardAppliedLedgerRecord ledgerRecord,
            IEnumerable<BossRewardNotificationIntent> durableNotificationIntents,
            IEnumerable<BossRewardPostCommitEvent> postCommitEvents,
            string applicationPolicyVersion,
            string planHash)
        {
            RewardResultId = rewardResultId;
            ComputationHash = computationHash;
            ExpectedSaveRevision = expectedSaveRevision;
            ExpectedEconomyRevision = expectedEconomyRevision;
            ExpectedInventoryRevision = expectedInventoryRevision;
            ExpectedLedgerRevision = expectedLedgerRevision;
            ExpectedCatalogSetId = expectedCatalogSetId;
            CreditOperation = creditOperation;
            InventoryOperations = BossRewardImmutable.Freeze(
                inventoryOperations,
                BossRewardTechnicalLimits.MaximumRewardEntries);
            LedgerRecord = ledgerRecord;
            DurableNotificationIntents = BossRewardImmutable.Freeze(
                durableNotificationIntents,
                BossRewardTechnicalLimits.MaximumRewardEntries + 1);
            PostCommitEvents = BossRewardImmutable.Freeze(postCommitEvents, 8);
            ApplicationPolicyVersion = applicationPolicyVersion;
            PlanHash = planHash;
        }

        public string RewardResultId { get; }
        public string ComputationHash { get; }
        public string ExpectedSaveRevision { get; }
        public string ExpectedEconomyRevision { get; }
        public string ExpectedInventoryRevision { get; }
        public string ExpectedLedgerRevision { get; }
        public string ExpectedCatalogSetId { get; }
        public BossRewardCreditOperation CreditOperation { get; }
        public IReadOnlyList<BossRewardInventoryOperation> InventoryOperations { get; }
        public BossRewardAppliedLedgerRecord LedgerRecord { get; }
        public IReadOnlyList<BossRewardNotificationIntent> DurableNotificationIntents { get; }
        public IReadOnlyList<BossRewardPostCommitEvent> PostCommitEvents { get; }
        public string ApplicationPolicyVersion { get; }
        public string PlanHash { get; }
    }

    public enum BossRewardPlanningStatus
    {
        Ready = 0,
        ExplicitNoRewardReady = 1,
        AlreadyCommitted = 2,
        InvalidComputedResult = 3,
        CorrelationConflict = 4,
        SaveUnavailable = 5,
        CatalogDrift = 6,
        EconomyUnavailable = 7,
        EconomyInvalid = 8,
        InventoryUnavailable = 9,
        InventoryMalformed = 10,
        DefinitionSnapshotConflict = 11,
        QuantityOverflow = 12,
        CreditOverflow = 13,
        StalePlan = 14,
        PendingRecovery = 15,
        UnsupportedVersion = 16,
        InternalInvariantFailure = 17
    }

    public sealed class BossRewardPlanningResult
    {
        public BossRewardPlanningResult(
            BossRewardPlanningStatus status,
            BossRewardApplicationPlan plan,
            BossRewardAppliedLedgerRecord existingRecord,
            IEnumerable<BossRewardDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingRecord = existingRecord;
            Diagnostics = BossRewardDiagnosticOrdering.Order(diagnostics);
            if ((status == BossRewardPlanningStatus.Ready ||
                 status == BossRewardPlanningStatus.ExplicitNoRewardReady) != (plan != null))
                throw new ArgumentException("Only ready statuses expose an application plan.");
            if ((status == BossRewardPlanningStatus.AlreadyCommitted) !=
                (existingRecord != null))
                throw new ArgumentException("Only exact replay exposes an existing record.");
        }

        public BossRewardPlanningStatus Status { get; }
        public BossRewardApplicationPlan Plan { get; }
        public BossRewardAppliedLedgerRecord ExistingRecord { get; }
        public IReadOnlyList<BossRewardDiagnostic> Diagnostics { get; }
    }

    public static class BossRewardApplicationPlanner
    {
        public static BossRewardPlanningResult Plan(
            BossRewardApplicationRequest request,
            BossRewardPlanningContext context)
        {
            BossRewardPlanningResult computedValidation =
                ValidateComputedResult(request);
            if (computedValidation != null) return computedValidation;

            BossRewardComputedValue value = request.Computation.Value;

            if (!BossRewardText.IsBoundedVersion(request.ApplicationPolicyVersion))
                return Reject(
                    BossRewardPlanningStatus.UnsupportedVersion,
                    "AL-BOSS-REWARD-TRANSACTION-POLICY-UNSUPPORTED",
                    "request.applicationPolicyVersion",
                    value.RewardResultId,
                    "The reward application policy version is unsupported.");
            if (!BossRewardText.IsBoundedTechnicalId(request.ExpectedSaveRevision) ||
                !BossRewardText.IsBoundedTechnicalId(
                    request.ExpectedEconomyRevision) ||
                !BossRewardText.IsBoundedTechnicalId(
                    request.ExpectedInventoryRevision) ||
                !BossRewardText.IsBoundedTechnicalId(
                    request.ExpectedLedgerRevision) ||
                !BossRewardText.IsCanonicalTechnicalId(
                    request.ExpectedCatalogSetId))
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-TRANSACTION-REQUEST-INVALID",
                    "request.expectedRevisions",
                    value.RewardResultId,
                    "The application request contains an invalid expected identity or revision.");
            if (context == null || !context.IsSaveAvailable)
                return Reject(
                    BossRewardPlanningStatus.SaveUnavailable,
                    "AL-BOSS-REWARD-TRANSACTION-SAVE-UNAVAILABLE",
                    "context.save",
                    value.RewardResultId,
                    "The save snapshot is unavailable.");
            if (!BossRewardText.IsBoundedTechnicalId(context.SaveRevision) ||
                !BossRewardText.IsCanonicalTechnicalId(context.GameId) ||
                !BossRewardText.IsCanonicalTechnicalId(context.ProfileId) ||
                !BossRewardText.IsCanonicalTechnicalId(context.CatalogSetId))
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-TRANSACTION-CONTEXT-INVALID",
                    "context",
                    value.RewardResultId,
                    "The planning context contains an invalid identity or revision.");
            if (!string.Equals(
                    value.GameId,
                    context.GameId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.ProfileId,
                    context.ProfileId,
                    StringComparison.Ordinal))
                return Reject(
                    BossRewardPlanningStatus.CatalogDrift,
                    "AL-BOSS-REWARD-TRANSACTION-PROFILE-DRIFT",
                    "context.profileId",
                    value.RewardResultId,
                    "The game or save profile identity changed after computation.");
            if (context.Ledger == null || !context.Ledger.IsUsable)
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-LEDGER-UNUSABLE",
                    "context.ledger",
                    value.RewardResultId,
                    "The complete applied-result ledger is unavailable or malformed.");
            if (!IsStructurallyValidLedger(
                    context.Ledger,
                    context.GameId,
                    context.ProfileId))
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-LEDGER-MALFORMED",
                    "context.ledger",
                    value.RewardResultId,
                    "The applied-result ledger does not match its declared status or receipt contract.");

            BossRewardPlanningResult replay = CheckLedgerReplay(
                request,
                context.Ledger,
                value);
            if (replay != null) return replay;
            if (context.Ledger.Records.Count >=
                BossRewardTechnicalLimits.MaximumLedgerRows)
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-LEDGER-CAPACITY-EXCEEDED",
                    "context.ledger",
                    value.RewardResultId,
                    "The applied-result ledger cannot safely accept another receipt.");
            if (!string.Equals(
                    request.ApplicationPolicyVersion,
                    BossRewardTechnicalLimits.SupportedApplicationPolicyVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.DeterminismVersion,
                    BossRewardTechnicalLimits.SupportedDeterminismVersion,
                    StringComparison.Ordinal))
                return Reject(
                    BossRewardPlanningStatus.UnsupportedVersion,
                    "AL-BOSS-REWARD-TRANSACTION-POLICY-UNSUPPORTED",
                    "request.applicationPolicyVersion",
                    value.RewardResultId,
                    "A new reward plan requires the current deterministic and application policy versions.");
            if (!string.Equals(
                    request.ExpectedCatalogSetId,
                    context.CatalogSetId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.CatalogSetId,
                    context.CatalogSetId,
                    StringComparison.Ordinal) ||
                context.RewardCatalog == null ||
                !string.Equals(
                    context.RewardCatalog.GameId,
                    context.GameId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    context.RewardCatalog.CatalogSetId,
                    context.CatalogSetId,
                    StringComparison.Ordinal))
                return Reject(
                    BossRewardPlanningStatus.CatalogDrift,
                    "AL-BOSS-REWARD-TRANSACTION-CATALOG-DRIFT",
                    "context.catalogSetId",
                    value.RewardResultId,
                    "The game, save profile, or catalog identity changed after computation.");
            if (!MatchesAuthoritativeComputation(value, context.RewardCatalog))
                return Reject(
                    BossRewardPlanningStatus.InvalidComputedResult,
                    "AL-BOSS-REWARD-TRANSACTION-CATALOG-AUTHORITY-MISMATCH",
                    "request.computation",
                    value.RewardResultId,
                    "The computed reward does not match the current immutable catalog authority.");
            if (!RevisionsMatch(request, context))
                return Reject(
                    BossRewardPlanningStatus.StalePlan,
                    "AL-BOSS-REWARD-TRANSACTION-REVISION-STALE",
                    "context.revisions",
                    value.RewardResultId,
                    "One or more expected domain revisions are stale.");
            if (context.PlannedUtcSeconds < 0)
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-TRANSACTION-TIMESTAMP-INVALID",
                    "context.plannedUtcSeconds",
                    value.RewardResultId,
                    "The injected plan timestamp is invalid.");

            if (context.Economy == null || !context.Economy.IsAvailable)
                return Reject(
                    BossRewardPlanningStatus.EconomyUnavailable,
                    "AL-BOSS-REWARD-TRANSACTION-ECONOMY-UNAVAILABLE",
                    "context.economy",
                    value.RewardResultId,
                    "The economy snapshot is unavailable.");
            if (context.Economy.WarzoneCredits < 0 ||
                context.Economy.MaximumWarzoneCredits < 0 ||
                context.Economy.WarzoneCredits >
                context.Economy.MaximumWarzoneCredits ||
                !BossRewardText.IsBoundedTechnicalId(context.Economy.Revision))
                return Reject(
                    BossRewardPlanningStatus.EconomyInvalid,
                    "AL-BOSS-REWARD-TRANSACTION-ECONOMY-INVALID",
                    "context.economy.warzoneCredits",
                    value.RewardResultId,
                    "The economy snapshot is malformed.");
            if (context.Inventory == null)
                return Reject(
                    BossRewardPlanningStatus.InventoryUnavailable,
                    "AL-BOSS-REWARD-TRANSACTION-INVENTORY-UNAVAILABLE",
                    "context.inventory",
                    value.RewardResultId,
                    "The inventory snapshot is unavailable.");
            if (!context.Inventory.CanApplyRewards)
                return Reject(
                    context.Inventory.Status == OwnedEquipmentQueryStatus.Unavailable
                        ? BossRewardPlanningStatus.InventoryUnavailable
                        : BossRewardPlanningStatus.InventoryMalformed,
                    "AL-BOSS-REWARD-TRANSACTION-INVENTORY-BLOCKED",
                    "context.inventory",
                    value.RewardResultId,
                    "The inventory snapshot is not valid for reward application.");
            if (!IsStructurallyValidInventory(context.Inventory))
                return Reject(
                    BossRewardPlanningStatus.InventoryMalformed,
                    "AL-BOSS-REWARD-TRANSACTION-INVENTORY-MALFORMED",
                    "context.inventory",
                    value.RewardResultId,
                    "The inventory wrapper does not match its declared status or row contract.");
            if (!MatchesAuthoritativeInventory(
                    context.Inventory,
                    context.RewardCatalog))
                return Reject(
                    BossRewardPlanningStatus.InventoryMalformed,
                    "AL-BOSS-REWARD-TRANSACTION-INVENTORY-CATALOG-MISMATCH",
                    "context.inventory",
                    value.RewardResultId,
                    "The inventory snapshot does not match the current immutable catalog authority.");
            if (!AreNotificationDefinitionsStructurallyValid(
                    context.AvailableNotificationDefinitionIds))
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-NOTIFICATION-DEFINITIONS-MALFORMED",
                    "context.availableNotificationDefinitionIds",
                    value.RewardResultId,
                    "The available notification-definition snapshot is malformed.");

            int nextCredits;
            try
            {
                nextCredits = checked(
                    context.Economy.WarzoneCredits + value.WarzoneCredits);
            }
            catch (OverflowException)
            {
                return Reject(
                    BossRewardPlanningStatus.CreditOverflow,
                    "AL-BOSS-REWARD-TRANSACTION-CREDIT-OVERFLOW",
                    "plan.credit",
                    value.RewardResultId,
                    "The checked Warzone Credit addition overflowed.");
            }
            if (nextCredits > context.Economy.MaximumWarzoneCredits)
                return Reject(
                    BossRewardPlanningStatus.CreditOverflow,
                    "AL-BOSS-REWARD-TRANSACTION-CREDIT-OVERFLOW",
                    "plan.credit",
                    value.RewardResultId,
                    "The Warzone Credit addition exceeds the domain maximum.");

            var inventoryById = context.Inventory.Items
                .Where(item => item.IsSupportedDefinition)
                .ToDictionary(
                    item => item.EquipmentDefinitionId,
                    StringComparer.Ordinal);
            var operations = new List<BossRewardInventoryOperation>();
            int projectedInventoryRows = context.Inventory.Items.Count;
            BossRewardComputedDrop[] orderedDrops = value.Drops
                .OrderBy(drop => drop.EquipmentDefinitionId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < orderedDrops.Length; index++)
            {
                BossRewardComputedDrop drop = orderedDrops[index];
                if (!inventoryById.TryGetValue(
                        drop.EquipmentDefinitionId,
                        out OwnedEquipmentSnapshot existing))
                {
                    if (projectedInventoryRows >=
                        BossRewardTechnicalLimits.MaximumInventoryRows)
                        return Reject(
                            BossRewardPlanningStatus.InventoryMalformed,
                            "AL-BOSS-REWARD-INVENTORY-CAPACITY-EXCEEDED",
                            "plan.inventory",
                            drop.EquipmentDefinitionId,
                            "The inventory cannot safely accept another equipment row.");
                    projectedInventoryRows++;
                    var candidate = new OwnedEquipmentSnapshot(
                        drop.EquipmentDefinitionId,
                        drop.EquipmentDefinitionContentVersion,
                        drop.AcquisitionSnapshotFingerprint,
                        drop.SlotId,
                        drop.AttackBonus,
                        drop.DefenseBonus,
                        drop.HealthBonus,
                        drop.StackPolicyId,
                        drop.Quantity,
                        context.PlannedUtcSeconds,
                        context.PlannedUtcSeconds,
                        value.BossDefinitionId,
                        value.EncounterCompletionId,
                        value.RewardResultId,
                        BossRewardTechnicalLimits.SupportedInventorySchemaVersion,
                        true);
                    operations.Add(new BossRewardInventoryOperation(
                        BossRewardInventoryOperationKind.Create,
                        drop.EquipmentDefinitionId,
                        0,
                        drop.Quantity,
                        drop.Quantity,
                        candidate));
                    continue;
                }

                if (!existing.IsSupportedDefinition ||
                    !string.Equals(
                        existing.EquipmentDefinitionContentVersion,
                        drop.EquipmentDefinitionContentVersion,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        existing.AcquisitionSnapshotFingerprint,
                        drop.AcquisitionSnapshotFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(existing.SlotId, drop.SlotId, StringComparison.Ordinal) ||
                    existing.AttackBonus != drop.AttackBonus ||
                    existing.DefenseBonus != drop.DefenseBonus ||
                    existing.HealthBonus != drop.HealthBonus ||
                    !string.Equals(
                        existing.StackPolicyId,
                        drop.StackPolicyId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        drop.StackPolicyId,
                        BossRewardStackPolicies.StackQuantity,
                        StringComparison.Ordinal))
                    return Reject(
                        BossRewardPlanningStatus.DefinitionSnapshotConflict,
                        "AL-BOSS-REWARD-INVENTORY-SNAPSHOT-CONFLICT",
                        "plan.inventory",
                        drop.EquipmentDefinitionId,
                        "The acquired stack snapshot conflicts with the computed drop.");
                if (context.PlannedUtcSeconds < existing.LastAcquiredUtcSeconds)
                    return Reject(
                        BossRewardPlanningStatus.InternalInvariantFailure,
                        "AL-BOSS-REWARD-TRANSACTION-TIMESTAMP-REGRESSION",
                        "context.plannedUtcSeconds",
                        drop.EquipmentDefinitionId,
                        "The injected plan timestamp predates the existing acquisition row.");

                int nextQuantity;
                try
                {
                    nextQuantity = checked(existing.Quantity + drop.Quantity);
                }
                catch (OverflowException)
                {
                    return Reject(
                        BossRewardPlanningStatus.QuantityOverflow,
                        "AL-BOSS-REWARD-INVENTORY-QUANTITY-OVERFLOW",
                        "plan.inventory",
                        drop.EquipmentDefinitionId,
                        "The checked equipment quantity addition overflowed.");
                }
                if (nextQuantity > BossRewardTechnicalLimits.MaximumOwnedQuantity)
                    return Reject(
                        BossRewardPlanningStatus.QuantityOverflow,
                        "AL-BOSS-REWARD-INVENTORY-QUANTITY-OVERFLOW",
                        "plan.inventory",
                        drop.EquipmentDefinitionId,
                        "The equipment quantity exceeds the domain maximum.");

                var updated = new OwnedEquipmentSnapshot(
                    existing.EquipmentDefinitionId,
                    existing.EquipmentDefinitionContentVersion,
                    existing.AcquisitionSnapshotFingerprint,
                    existing.SlotId,
                    existing.AttackBonus,
                    existing.DefenseBonus,
                    existing.HealthBonus,
                    existing.StackPolicyId,
                    nextQuantity,
                    existing.FirstAcquiredUtcSeconds,
                    context.PlannedUtcSeconds,
                    value.BossDefinitionId,
                    value.EncounterCompletionId,
                    value.RewardResultId,
                    existing.SchemaVersion,
                    true);
                operations.Add(new BossRewardInventoryOperation(
                    BossRewardInventoryOperationKind.Update,
                    drop.EquipmentDefinitionId,
                    existing.Quantity,
                    drop.Quantity,
                    nextQuantity,
                    updated));
            }

            if (!CanDeriveBoundedCorrelationIds(value))
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-TRANSACTION-CORRELATION-ID-INVALID",
                    "request.computation.rewardResultId",
                    value.RewardResultId,
                    "The reward identity cannot produce bounded transaction correlation IDs.");

            List<BossRewardNotificationIntent> notifications =
                BuildNotificationIntents(value, context);
            if (notifications == null)
                return Reject(
                    BossRewardPlanningStatus.InternalInvariantFailure,
                    "AL-BOSS-REWARD-NOTIFICATION-DEFINITION-UNAVAILABLE",
                    "context.availableNotificationDefinitionIds",
                    value.RewardResultId,
                    "A required durable notification definition is unavailable.");

            string[] correlationIds = notifications
                .Select(intent => intent.CorrelationId)
                .ToArray();
            var ledgerRecord = new BossRewardAppliedLedgerRecord(
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
                value.ComputationHash,
                value.WarzoneCredits,
                value.IsExplicitNoReward,
                value.DeterminismVersion,
                value.Drops,
                context.PlannedUtcSeconds,
                request.ApplicationPolicyVersion,
                correlationIds,
                BossRewardLedgerRecordState.Committed);
            var events = new[]
            {
                new BossRewardPostCommitEvent(
                    "boss_reward_committed",
                    value.RewardResultId + ":committed",
                    value.RewardResultId)
            };
            var credit = new BossRewardCreditOperation(
                context.Economy.WarzoneCredits,
                value.WarzoneCredits,
                nextCredits);
            string planHash = ComputePlanHash(
                request,
                credit,
                operations,
                ledgerRecord,
                notifications,
                events);
            var plan = new BossRewardApplicationPlan(
                value.RewardResultId,
                value.ComputationHash,
                request.ExpectedSaveRevision,
                request.ExpectedEconomyRevision,
                request.ExpectedInventoryRevision,
                request.ExpectedLedgerRevision,
                request.ExpectedCatalogSetId,
                credit,
                operations,
                ledgerRecord,
                notifications,
                events,
                request.ApplicationPolicyVersion,
                planHash);
            return new BossRewardPlanningResult(
                value.IsExplicitNoReward
                    ? BossRewardPlanningStatus.ExplicitNoRewardReady
                    : BossRewardPlanningStatus.Ready,
                plan,
                null,
                Array.Empty<BossRewardDiagnostic>());
        }

        private static BossRewardPlanningResult ValidateComputedResult(
            BossRewardApplicationRequest request)
        {
            if (request == null ||
                request.Computation == null ||
                !request.Computation.IsSuccess ||
                request.Computation.Value == null)
                return Reject(
                    BossRewardPlanningStatus.InvalidComputedResult,
                    "AL-BOSS-REWARD-TRANSACTION-COMPUTATION-INVALID",
                    "request.computation",
                    string.Empty,
                    "A verified successful computed reward is required.");

            BossRewardComputationResult computation = request.Computation;
            BossRewardComputedValue value = computation.Value;
            string recordId =
                BossRewardText.IsCanonicalTechnicalId(value.RewardResultId)
                ? value.RewardResultId
                : string.Empty;
            bool statusMatchesValue =
                computation.Status == BossRewardComputationStatus.ExplicitNoReward
                    ? value.IsExplicitNoReward
                    : computation.Status == BossRewardComputationStatus.Computed &&
                      !value.IsExplicitNoReward;
            if (!statusMatchesValue ||
                computation.Diagnostics.Any(item => item.BlocksOperation) ||
                !BossRewardText.IsCanonicalTechnicalId(value.GameId) ||
                !BossRewardText.IsCanonicalTechnicalId(value.CatalogSetId) ||
                !BossRewardText.IsCanonicalTechnicalId(value.ProfileId) ||
                !BossRewardText.IsCanonicalTechnicalId(value.RewardResultId) ||
                !BossRewardText.IsCanonicalTechnicalId(value.EncounterId) ||
                !BossRewardText.IsCanonicalTechnicalId(
                    value.EncounterCompletionId) ||
                !BossRewardText.IsCanonicalTechnicalId(value.BossDefinitionId) ||
                !BossRewardText.IsBoundedVersion(value.BossDefinitionContentVersion) ||
                !BossRewardText.IsCanonicalTechnicalId(value.RewardProfileId) ||
                !BossRewardText.IsBoundedVersion(value.RewardProfileContentVersion) ||
                !BossRewardText.IsLowerSha256(value.RewardProfileSha256) ||
                value.WarzoneCredits < 0 ||
                value.WarzoneCredits > BossRewardTechnicalLimits.MaximumWarzoneCredits ||
                !BossRewardText.IsBoundedVersion(value.DeterminismVersion) ||
                !BossRewardTechnicalLimits.IsReadableDeterminismVersion(
                    value.DeterminismVersion) ||
                !BossRewardText.IsLowerSha256(value.ComputationHash) ||
                !AreComputedDropsStructurallyValid(value.Drops) ||
                (value.IsExplicitNoReward &&
                 (value.WarzoneCredits != 0 || value.Drops.Count != 0)))
                return Reject(
                    BossRewardPlanningStatus.InvalidComputedResult,
                    "AL-BOSS-REWARD-TRANSACTION-COMPUTATION-INVALID",
                    "request.computation.value",
                    recordId,
                    "The computed reward violates its immutable semantic contract.");

            try
            {
                if (!string.Equals(
                        BossRewardComputation.RecomputeComputationHash(value),
                        value.ComputationHash,
                        StringComparison.Ordinal))
                    return Reject(
                        BossRewardPlanningStatus.InvalidComputedResult,
                        "AL-BOSS-REWARD-TRANSACTION-COMPUTATION-HASH-INVALID",
                        "request.computation.computationHash",
                        recordId,
                        "The computed reward hash does not match its canonical value.");
            }
            catch (Exception)
            {
                return Reject(
                    BossRewardPlanningStatus.InvalidComputedResult,
                    "AL-BOSS-REWARD-TRANSACTION-COMPUTATION-INVALID",
                    "request.computation.value",
                    recordId,
                    "The computed reward could not be safely canonicalized.");
            }

            return null;
        }

        private static bool MatchesAuthoritativeComputation(
            BossRewardComputedValue value,
            BossRewardCatalogSnapshot catalog)
        {
            try
            {
                var request = new BossRewardComputationRequest(
                    value.GameId,
                    value.CatalogSetId,
                    value.ProfileId,
                    value.EncounterId,
                    value.EncounterCompletionId,
                    value.RewardResultId,
                    value.BossDefinitionId,
                    value.BossDefinitionContentVersion,
                    value.RewardProfileId,
                    value.RewardProfileContentVersion,
                    value.DeterminismVersion);
                BossRewardComputationResult authoritative =
                    BossRewardComputation.Compute(request, catalog);
                if (!authoritative.IsSuccess ||
                    authoritative.Value == null ||
                    authoritative.Status !=
                    (value.IsExplicitNoReward
                        ? BossRewardComputationStatus.ExplicitNoReward
                        : BossRewardComputationStatus.Computed))
                    return false;

                BossRewardComputedValue expected = authoritative.Value;
                if (!string.Equals(expected.GameId, value.GameId, StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.CatalogSetId,
                        value.CatalogSetId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.ProfileId,
                        value.ProfileId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.RewardResultId,
                        value.RewardResultId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.EncounterId,
                        value.EncounterId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.EncounterCompletionId,
                        value.EncounterCompletionId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.BossDefinitionId,
                        value.BossDefinitionId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.BossDefinitionContentVersion,
                        value.BossDefinitionContentVersion,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.RewardProfileId,
                        value.RewardProfileId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.RewardProfileContentVersion,
                        value.RewardProfileContentVersion,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.RewardProfileSha256,
                        value.RewardProfileSha256,
                        StringComparison.Ordinal) ||
                    expected.WarzoneCredits != value.WarzoneCredits ||
                    expected.IsExplicitNoReward != value.IsExplicitNoReward ||
                    !string.Equals(
                        expected.DeterminismVersion,
                        value.DeterminismVersion,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected.ComputationHash,
                        value.ComputationHash,
                        StringComparison.Ordinal) ||
                    expected.Drops.Count != value.Drops.Count)
                    return false;
                for (int index = 0; index < expected.Drops.Count; index++)
                {
                    if (!DropMatches(expected.Drops[index], value.Drops[index]))
                        return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool AreComputedDropsStructurallyValid(
            IReadOnlyList<BossRewardComputedDrop> drops)
        {
            if (drops == null ||
                drops.Count > BossRewardTechnicalLimits.MaximumRewardEntries)
                return false;
            string previousId = null;
            for (int index = 0; index < drops.Count; index++)
            {
                BossRewardComputedDrop drop = drops[index];
                if (drop == null ||
                    !BossRewardText.IsCanonicalTechnicalId(
                        drop.EquipmentDefinitionId) ||
                    !BossRewardText.IsBoundedVersion(
                        drop.EquipmentDefinitionContentVersion) ||
                    !BossRewardText.IsLowerSha256(
                        drop.AcquisitionSnapshotFingerprint) ||
                    !BossRewardText.IsCanonicalTechnicalId(drop.SlotId) ||
                    drop.Quantity != 1 ||
                    !BossRewardStackPolicies.IsSupported(drop.StackPolicyId) ||
                    !BossRewardText.IsBoundedContentKey(
                        drop.AcquisitionAnnouncementPolicyId) ||
                    (previousId != null &&
                     StringComparer.Ordinal.Compare(
                         previousId,
                         drop.EquipmentDefinitionId) >= 0))
                    return false;
                previousId = drop.EquipmentDefinitionId;
            }
            return true;
        }

        private static bool IsStructurallyValidInventory(
            OwnedEquipmentQueryResult inventory)
        {
            if (inventory == null ||
                !Enum.IsDefined(typeof(OwnedEquipmentQueryStatus), inventory.Status) ||
                !BossRewardText.IsBoundedTechnicalId(inventory.InventoryRevision) ||
                inventory.Items == null ||
                inventory.Items.Count > BossRewardTechnicalLimits.MaximumInventoryRows ||
                inventory.Diagnostics.Any(item => item.BlocksOperation))
                return false;
            if ((inventory.Status == OwnedEquipmentQueryStatus.Empty) !=
                (inventory.Items.Count == 0))
                return false;
            if (inventory.Status != OwnedEquipmentQueryStatus.Empty &&
                inventory.Status != OwnedEquipmentQueryStatus.Valid &&
                inventory.Status !=
                OwnedEquipmentQueryStatus.PreservedUnknownFutureDefinition)
                return false;

            string previousId = null;
            bool containsUnsupportedDefinition = false;
            for (int index = 0; index < inventory.Items.Count; index++)
            {
                OwnedEquipmentSnapshot row = inventory.Items[index];
                if (row == null ||
                    !BossRewardText.IsCanonicalTechnicalId(
                        row.EquipmentDefinitionId) ||
                    row.Quantity <= 0 ||
                    row.Quantity > BossRewardTechnicalLimits.MaximumOwnedQuantity ||
                    (string.Equals(
                         row.StackPolicyId,
                         BossRewardStackPolicies.UniqueInstance,
                         StringComparison.Ordinal) &&
                     row.Quantity != 1) ||
                    row.FirstAcquiredUtcSeconds < 0 ||
                    row.LastAcquiredUtcSeconds < row.FirstAcquiredUtcSeconds ||
                    !IsOptionalCanonicalId(row.LastSourceBossDefinitionId) ||
                    !IsOptionalCanonicalId(
                        row.LastSourceEncounterCompletionId) ||
                    !IsOptionalCanonicalId(row.LastAppliedRewardResultId) ||
                    !BossRewardText.IsBoundedVersion(row.SchemaVersion) ||
                    !string.Equals(
                        row.SchemaVersion,
                        BossRewardTechnicalLimits.SupportedInventorySchemaVersion,
                        StringComparison.Ordinal) ||
                    (previousId != null &&
                     StringComparer.Ordinal.Compare(
                         previousId,
                         row.EquipmentDefinitionId) >= 0))
                    return false;
                if (row.IsSupportedDefinition)
                {
                    if (!BossRewardText.IsBoundedVersion(
                            row.EquipmentDefinitionContentVersion) ||
                        !BossRewardText.IsLowerSha256(
                            row.AcquisitionSnapshotFingerprint) ||
                        !BossRewardText.IsCanonicalTechnicalId(row.SlotId) ||
                        !BossRewardStackPolicies.IsSupported(row.StackPolicyId))
                        return false;
                }
                else
                {
                    containsUnsupportedDefinition = true;
                }
                previousId = row.EquipmentDefinitionId;
            }
            return containsUnsupportedDefinition ==
                   (inventory.Status ==
                    OwnedEquipmentQueryStatus.PreservedUnknownFutureDefinition);
        }

        private static bool MatchesAuthoritativeInventory(
            OwnedEquipmentQueryResult inventory,
            BossRewardCatalogSnapshot catalog)
        {
            try
            {
                OwnedEquipmentQueryResult authoritative =
                    BossRewardInventoryValidator.Validate(
                        inventory.Items,
                        inventory.InventoryRevision,
                        catalog,
                        BossRewardTechnicalLimits.SupportedInventorySchemaVersion);
                return authoritative.CanApplyRewards &&
                       authoritative.Status == inventory.Status &&
                       authoritative.Items.Count == inventory.Items.Count &&
                       !authoritative.Diagnostics.Any(item => item.BlocksOperation);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsStructurallyValidLedger(
            BossRewardLedgerSnapshot ledger,
            string expectedGameId,
            string expectedProfileId)
        {
            if (ledger == null ||
                !ledger.IsComplete ||
                !BossRewardText.IsCanonicalTechnicalId(ledger.GameId) ||
                !BossRewardText.IsCanonicalTechnicalId(ledger.ProfileId) ||
                !string.Equals(
                    ledger.GameId,
                    expectedGameId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ledger.ProfileId,
                    expectedProfileId,
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(BossRewardLedgerStatus), ledger.Status) ||
                !BossRewardText.IsBoundedTechnicalId(ledger.Revision) ||
                ledger.Records == null ||
                ledger.Records.Count > BossRewardTechnicalLimits.MaximumLedgerRows ||
                ledger.Diagnostics.Any(item => item.BlocksOperation))
                return false;
            if ((ledger.Status == BossRewardLedgerStatus.Empty) !=
                (ledger.Records.Count == 0))
                return false;
            if (ledger.Status != BossRewardLedgerStatus.Empty &&
                ledger.Status != BossRewardLedgerStatus.Valid)
                return false;

            string previousId = null;
            for (int index = 0; index < ledger.Records.Count; index++)
            {
                BossRewardAppliedLedgerRecord record = ledger.Records[index];
                if (!IsStructurallyValidLedgerRecord(record) ||
                    !string.Equals(
                        record.GameId,
                        expectedGameId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        record.ProfileId,
                        expectedProfileId,
                        StringComparison.Ordinal) ||
                    (previousId != null &&
                     StringComparer.Ordinal.Compare(
                         previousId,
                         record.RewardResultId) >= 0))
                    return false;
                previousId = record.RewardResultId;
            }
            return true;
        }

        private static bool IsStructurallyValidLedgerRecord(
            BossRewardAppliedLedgerRecord record)
        {
            if (record == null ||
                !BossRewardText.IsCanonicalTechnicalId(record.GameId) ||
                !BossRewardText.IsCanonicalTechnicalId(record.CatalogSetId) ||
                !BossRewardText.IsCanonicalTechnicalId(record.ProfileId) ||
                !BossRewardText.IsCanonicalTechnicalId(record.RewardResultId) ||
                !BossRewardText.IsCanonicalTechnicalId(record.EncounterId) ||
                !BossRewardText.IsCanonicalTechnicalId(
                    record.EncounterCompletionId) ||
                !BossRewardText.IsCanonicalTechnicalId(
                    record.BossDefinitionId) ||
                !BossRewardText.IsBoundedVersion(
                    record.BossDefinitionContentVersion) ||
                !BossRewardText.IsCanonicalTechnicalId(
                    record.RewardProfileId) ||
                !BossRewardText.IsBoundedVersion(
                    record.RewardProfileContentVersion) ||
                !BossRewardText.IsLowerSha256(record.RewardProfileSha256) ||
                !BossRewardText.IsLowerSha256(record.ComputationHash) ||
                record.WarzoneCredits < 0 ||
                record.WarzoneCredits >
                BossRewardTechnicalLimits.MaximumWarzoneCredits ||
                (record.IsExplicitNoReward &&
                 (record.WarzoneCredits != 0 || record.CommittedDrops.Count != 0)) ||
                !BossRewardText.IsBoundedVersion(record.DeterminismVersion) ||
                !BossRewardTechnicalLimits.IsReadableDeterminismVersion(
                    record.DeterminismVersion) ||
                !AreComputedDropsStructurallyValid(record.CommittedDrops) ||
                record.CommittedUtcSeconds < 0 ||
                !BossRewardText.IsBoundedVersion(record.ApplicationPolicyVersion) ||
                !BossRewardTechnicalLimits.IsReadableApplicationPolicyVersion(
                    record.ApplicationPolicyVersion) ||
                !Enum.IsDefined(typeof(BossRewardLedgerRecordState), record.State) ||
                record.NotificationCorrelationIds == null)
                return false;

            string previousCorrelationId = null;
            for (int index = 0; index < record.NotificationCorrelationIds.Count; index++)
            {
                string correlationId = record.NotificationCorrelationIds[index];
                if (!BossRewardText.IsBoundedTechnicalId(correlationId) ||
                    (previousCorrelationId != null &&
                     StringComparer.Ordinal.Compare(
                         previousCorrelationId,
                         correlationId) >= 0))
                    return false;
                previousCorrelationId = correlationId;
            }
            if (!HasValidDerivedLedgerCorrelations(record))
                return false;

            try
            {
                var value = new BossRewardComputedValue(
                    record.GameId,
                    record.CatalogSetId,
                    record.ProfileId,
                    record.RewardResultId,
                    record.EncounterId,
                    record.EncounterCompletionId,
                    record.BossDefinitionId,
                    record.BossDefinitionContentVersion,
                    record.RewardProfileId,
                    record.RewardProfileContentVersion,
                    record.RewardProfileSha256,
                    record.WarzoneCredits,
                    record.IsExplicitNoReward,
                    record.CommittedDrops,
                    record.DeterminismVersion,
                    record.ComputationHash);
                return string.Equals(
                    BossRewardComputation.RecomputeComputationHash(value),
                    record.ComputationHash,
                    StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasValidDerivedLedgerCorrelations(
            BossRewardAppliedLedgerRecord record)
        {
            var required = new HashSet<string>(StringComparer.Ordinal);
            if (record.WarzoneCredits > 0)
                required.Add(record.RewardResultId + ":credits");
            for (int index = 0; index < record.CommittedDrops.Count; index++)
                required.Add(
                    record.RewardResultId +
                    ":item:" +
                    record.CommittedDrops[index].EquipmentDefinitionId);

            string optionalNoReward = record.RewardResultId + ":no_reward";
            for (int index = 0; index < record.NotificationCorrelationIds.Count; index++)
            {
                string correlationId = record.NotificationCorrelationIds[index];
                if (required.Remove(correlationId)) continue;
                if (record.IsExplicitNoReward &&
                    string.Equals(
                        correlationId,
                        optionalNoReward,
                        StringComparison.Ordinal))
                    continue;
                return false;
            }
            return required.Count == 0;
        }

        private static bool IsOptionalCanonicalId(string value)
        {
            return string.IsNullOrEmpty(value) ||
                   BossRewardText.IsCanonicalTechnicalId(value);
        }

        private static bool AreNotificationDefinitionsStructurallyValid(
            IReadOnlyList<string> definitionIds)
        {
            if (definitionIds == null ||
                definitionIds.Count > BossRewardTechnicalLimits.MaximumCatalogEntries)
                return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < definitionIds.Count; index++)
            {
                string definitionId = definitionIds[index];
                if (!BossRewardText.IsBoundedContentKey(definitionId) ||
                    !seen.Add(definitionId))
                    return false;
            }
            return true;
        }

        private static bool RevisionsMatch(
            BossRewardApplicationRequest request,
            BossRewardPlanningContext context)
        {
            return string.Equals(
                       request.ExpectedSaveRevision,
                       context.SaveRevision,
                       StringComparison.Ordinal) &&
                   context.Economy != null &&
                   string.Equals(
                       request.ExpectedEconomyRevision,
                       context.Economy.Revision,
                       StringComparison.Ordinal) &&
                   context.Inventory != null &&
                   string.Equals(
                       request.ExpectedInventoryRevision,
                       context.Inventory.InventoryRevision,
                       StringComparison.Ordinal) &&
                   context.Ledger != null &&
                   string.Equals(
                       request.ExpectedLedgerRevision,
                       context.Ledger.Revision,
                       StringComparison.Ordinal);
        }

        private static BossRewardPlanningResult CheckLedgerReplay(
            BossRewardApplicationRequest request,
            BossRewardLedgerSnapshot ledger,
            BossRewardComputedValue value)
        {
            BossRewardAppliedLedgerRecord found = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var completionOwners = new Dictionary<string, string>(
                StringComparer.Ordinal);
            for (int index = 0; index < ledger.Records.Count; index++)
            {
                BossRewardAppliedLedgerRecord record = ledger.Records[index];
                if (record == null ||
                    !BossRewardText.IsCanonicalTechnicalId(
                        record.RewardResultId) ||
                    !seen.Add(record.RewardResultId))
                    return Reject(
                        BossRewardPlanningStatus.InternalInvariantFailure,
                        "AL-BOSS-REWARD-LEDGER-MALFORMED",
                        "context.ledger.records[" + index + "]",
                        value.RewardResultId,
                        "The applied-result ledger contains a malformed or duplicate row.");
                if (completionOwners.ContainsKey(record.EncounterCompletionId))
                    return Reject(
                        BossRewardPlanningStatus.InternalInvariantFailure,
                        "AL-BOSS-REWARD-LEDGER-COMPLETION-DUPLICATE",
                        "context.ledger.records[" + index + "]",
                        value.RewardResultId,
                        "The ledger maps one encounter completion to multiple receipts.");
                completionOwners.Add(
                    record.EncounterCompletionId,
                    record.RewardResultId);
                if (string.Equals(
                        record.EncounterCompletionId,
                        value.EncounterCompletionId,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        record.RewardResultId,
                        value.RewardResultId,
                        StringComparison.Ordinal))
                    return Reject(
                        BossRewardPlanningStatus.CorrelationConflict,
                        "AL-BOSS-REWARD-LEDGER-COMPLETION-CONFLICT",
                        "context.ledger",
                        value.RewardResultId,
                        "The encounter completion is already bound to another reward result.");
                if (string.Equals(
                        record.RewardResultId,
                        value.RewardResultId,
                        StringComparison.Ordinal))
                    found = record;
            }
            if (found == null) return null;
            if (found.State == BossRewardLedgerRecordState.PendingRecovery)
                return Reject(
                    BossRewardPlanningStatus.PendingRecovery,
                    "AL-BOSS-REWARD-LEDGER-PENDING-RECOVERY",
                    "context.ledger",
                    value.RewardResultId,
                    "The matching reward transaction requires recovery.");
            if (SemanticallyMatches(found, value, request.ApplicationPolicyVersion))
                return new BossRewardPlanningResult(
                    BossRewardPlanningStatus.AlreadyCommitted,
                    null,
                    found,
                    Array.Empty<BossRewardDiagnostic>());
            return Reject(
                BossRewardPlanningStatus.CorrelationConflict,
                "AL-BOSS-REWARD-LEDGER-CORRELATION-CONFLICT",
                "context.ledger",
                value.RewardResultId,
                "The reward result identity is already bound to different semantics.");
        }

        private static bool SemanticallyMatches(
            BossRewardAppliedLedgerRecord record,
            BossRewardComputedValue value,
            string applicationPolicyVersion)
        {
            if (!string.Equals(record.GameId, value.GameId, StringComparison.Ordinal) ||
                !string.Equals(
                    record.CatalogSetId,
                    value.CatalogSetId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.ProfileId,
                    value.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.RewardResultId,
                    value.RewardResultId,
                    StringComparison.Ordinal) ||
                !string.Equals(record.EncounterId, value.EncounterId, StringComparison.Ordinal) ||
                !string.Equals(
                    record.EncounterCompletionId,
                    value.EncounterCompletionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.BossDefinitionId,
                    value.BossDefinitionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.BossDefinitionContentVersion,
                    value.BossDefinitionContentVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.RewardProfileId,
                    value.RewardProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.RewardProfileContentVersion,
                    value.RewardProfileContentVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.RewardProfileSha256,
                    value.RewardProfileSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.ComputationHash,
                    value.ComputationHash,
                    StringComparison.Ordinal) ||
                record.WarzoneCredits != value.WarzoneCredits ||
                record.IsExplicitNoReward != value.IsExplicitNoReward ||
                !string.Equals(
                    record.DeterminismVersion,
                    value.DeterminismVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.ApplicationPolicyVersion,
                    applicationPolicyVersion,
                    StringComparison.Ordinal) ||
                record.CommittedDrops.Count != value.Drops.Count)
                return false;
            for (int index = 0; index < record.CommittedDrops.Count; index++)
            {
                BossRewardComputedDrop left = record.CommittedDrops[index];
                BossRewardComputedDrop right = value.Drops[index];
                if (!DropMatches(left, right)) return false;
            }
            return true;
        }

        private static bool DropMatches(
            BossRewardComputedDrop left,
            BossRewardComputedDrop right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(
                       left.EquipmentDefinitionId,
                       right.EquipmentDefinitionId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.EquipmentDefinitionContentVersion,
                       right.EquipmentDefinitionContentVersion,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.AcquisitionSnapshotFingerprint,
                       right.AcquisitionSnapshotFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(left.SlotId, right.SlotId, StringComparison.Ordinal) &&
                   left.AttackBonus == right.AttackBonus &&
                   left.DefenseBonus == right.DefenseBonus &&
                   left.HealthBonus == right.HealthBonus &&
                   left.Quantity == right.Quantity &&
                   string.Equals(
                       left.StackPolicyId,
                       right.StackPolicyId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.AcquisitionAnnouncementPolicyId,
                       right.AcquisitionAnnouncementPolicyId,
                       StringComparison.Ordinal);
        }

        private static List<BossRewardNotificationIntent> BuildNotificationIntents(
            BossRewardComputedValue value,
            BossRewardPlanningContext context)
        {
            var available = new HashSet<string>(
                context.AvailableNotificationDefinitionIds,
                StringComparer.Ordinal);
            var output = new List<BossRewardNotificationIntent>();
            if (value.WarzoneCredits > 0)
            {
                const string creditDefinition = "boss_reward.credits_committed";
                if (!available.Contains(creditDefinition)) return null;
                output.Add(new BossRewardNotificationIntent(
                    creditDefinition,
                    value.RewardResultId + ":credits",
                    string.Empty));
            }
            for (int index = 0; index < value.Drops.Count; index++)
            {
                BossRewardComputedDrop drop = value.Drops[index];
                if (!available.Contains(drop.AcquisitionAnnouncementPolicyId))
                    return null;
                output.Add(new BossRewardNotificationIntent(
                    drop.AcquisitionAnnouncementPolicyId,
                    value.RewardResultId + ":item:" + drop.EquipmentDefinitionId,
                    drop.EquipmentDefinitionId));
            }
            if (value.IsExplicitNoReward)
            {
                const string noRewardDefinition = "boss_reward.explicit_no_reward";
                if (available.Contains(noRewardDefinition))
                    output.Add(new BossRewardNotificationIntent(
                        noRewardDefinition,
                        value.RewardResultId + ":no_reward",
                        string.Empty));
            }
            return output
                .OrderBy(intent => intent.CorrelationId, StringComparer.Ordinal)
                .ToList();
        }

        private static bool CanDeriveBoundedCorrelationIds(
            BossRewardComputedValue value)
        {
            if (!BossRewardText.IsBoundedTechnicalId(
                    value.RewardResultId + ":committed"))
                return false;
            if (value.WarzoneCredits > 0 &&
                !BossRewardText.IsBoundedTechnicalId(
                    value.RewardResultId + ":credits"))
                return false;
            if (value.IsExplicitNoReward &&
                !BossRewardText.IsBoundedTechnicalId(
                    value.RewardResultId + ":no_reward"))
                return false;
            for (int index = 0; index < value.Drops.Count; index++)
            {
                if (!BossRewardText.IsBoundedTechnicalId(
                        value.RewardResultId +
                        ":item:" +
                        value.Drops[index].EquipmentDefinitionId))
                    return false;
            }
            return true;
        }

        private static string ComputePlanHash(
            BossRewardApplicationRequest request,
            BossRewardCreditOperation credit,
            IEnumerable<BossRewardInventoryOperation> operations,
            BossRewardAppliedLedgerRecord ledger,
            IEnumerable<BossRewardNotificationIntent> notifications,
            IEnumerable<BossRewardPostCommitEvent> events)
        {
            using (var writer = new BossRewardCanonicalWriter())
            {
                writer.WriteString("boss_reward_application_plan_v1");
                writer.WriteString(request.ExpectedSaveRevision);
                writer.WriteString(request.ExpectedEconomyRevision);
                writer.WriteString(request.ExpectedInventoryRevision);
                writer.WriteString(request.ExpectedLedgerRevision);
                writer.WriteString(request.ExpectedCatalogSetId);
                writer.WriteString(request.ApplicationPolicyVersion);
                writer.WriteInt32(credit.Previous);
                writer.WriteInt32(credit.Delta);
                writer.WriteInt32(credit.Next);
                BossRewardInventoryOperation[] orderedOperations = operations
                    .OrderBy(item => item.EquipmentDefinitionId, StringComparer.Ordinal)
                    .ToArray();
                writer.WriteUInt32((uint)orderedOperations.Length);
                for (int index = 0; index < orderedOperations.Length; index++)
                {
                    BossRewardInventoryOperation operation = orderedOperations[index];
                    writer.WriteInt32((int)operation.Kind);
                    writer.WriteString(operation.EquipmentDefinitionId);
                    writer.WriteInt32(operation.PreviousQuantity);
                    writer.WriteInt32(operation.QuantityDelta);
                    writer.WriteInt32(operation.NewQuantity);
                    WriteOwnedEquipmentSnapshot(writer, operation.CandidateRow);
                }
                WriteLedgerRecord(writer, ledger);
                BossRewardNotificationIntent[] orderedNotifications = notifications
                    .OrderBy(item => item.CorrelationId, StringComparer.Ordinal)
                    .ToArray();
                writer.WriteUInt32((uint)orderedNotifications.Length);
                for (int index = 0; index < orderedNotifications.Length; index++)
                {
                    writer.WriteString(orderedNotifications[index].DefinitionId);
                    writer.WriteString(orderedNotifications[index].CorrelationId);
                    writer.WriteString(
                        orderedNotifications[index].EquipmentDefinitionId);
                }
                BossRewardPostCommitEvent[] orderedEvents = events
                    .OrderBy(item => item.CorrelationId, StringComparer.Ordinal)
                    .ToArray();
                writer.WriteUInt32((uint)orderedEvents.Length);
                for (int index = 0; index < orderedEvents.Length; index++)
                {
                    writer.WriteString(orderedEvents[index].EventId);
                    writer.WriteString(orderedEvents[index].CorrelationId);
                    writer.WriteString(orderedEvents[index].RewardResultId);
                }
                return BossRewardDeterministicRoll.ToLowerHex(
                    BossRewardDeterministicRoll.ComputeDigest(writer.ToArray()));
            }
        }

        private static void WriteOwnedEquipmentSnapshot(
            BossRewardCanonicalWriter writer,
            OwnedEquipmentSnapshot row)
        {
            writer.WriteBoolean(row != null);
            if (row == null) return;
            writer.WriteString(row.EquipmentDefinitionId);
            writer.WriteString(row.EquipmentDefinitionContentVersion);
            writer.WriteString(row.AcquisitionSnapshotFingerprint);
            writer.WriteString(row.SlotId);
            writer.WriteInt32(row.AttackBonus);
            writer.WriteInt32(row.DefenseBonus);
            writer.WriteInt32(row.HealthBonus);
            writer.WriteString(row.StackPolicyId);
            writer.WriteInt32(row.Quantity);
            writer.WriteInt64(row.FirstAcquiredUtcSeconds);
            writer.WriteInt64(row.LastAcquiredUtcSeconds);
            writer.WriteString(row.LastSourceBossDefinitionId);
            writer.WriteString(row.LastSourceEncounterCompletionId);
            writer.WriteString(row.LastAppliedRewardResultId);
            writer.WriteString(row.SchemaVersion);
            writer.WriteBoolean(row.IsSupportedDefinition);
        }

        private static void WriteLedgerRecord(
            BossRewardCanonicalWriter writer,
            BossRewardAppliedLedgerRecord ledger)
        {
            writer.WriteString(ledger.GameId);
            writer.WriteString(ledger.CatalogSetId);
            writer.WriteString(ledger.ProfileId);
            writer.WriteString(ledger.RewardResultId);
            writer.WriteString(ledger.EncounterId);
            writer.WriteString(ledger.EncounterCompletionId);
            writer.WriteString(ledger.BossDefinitionId);
            writer.WriteString(ledger.BossDefinitionContentVersion);
            writer.WriteString(ledger.RewardProfileId);
            writer.WriteString(ledger.RewardProfileContentVersion);
            writer.WriteString(ledger.RewardProfileSha256);
            writer.WriteString(ledger.ComputationHash);
            writer.WriteInt32(ledger.WarzoneCredits);
            writer.WriteBoolean(ledger.IsExplicitNoReward);
            writer.WriteString(ledger.DeterminismVersion);
            BossRewardComputedDrop[] orderedDrops = ledger.CommittedDrops
                .OrderBy(item => item.EquipmentDefinitionId, StringComparer.Ordinal)
                .ToArray();
            writer.WriteUInt32((uint)orderedDrops.Length);
            for (int index = 0; index < orderedDrops.Length; index++)
                WriteComputedDrop(writer, orderedDrops[index]);
            writer.WriteInt64(ledger.CommittedUtcSeconds);
            writer.WriteString(ledger.ApplicationPolicyVersion);
            writer.WriteInt32((int)ledger.State);
            string[] orderedCorrelationIds = ledger.NotificationCorrelationIds
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            writer.WriteUInt32((uint)orderedCorrelationIds.Length);
            for (int index = 0; index < orderedCorrelationIds.Length; index++)
                writer.WriteString(orderedCorrelationIds[index]);
        }

        private static void WriteComputedDrop(
            BossRewardCanonicalWriter writer,
            BossRewardComputedDrop drop)
        {
            writer.WriteString(drop.EquipmentDefinitionId);
            writer.WriteString(drop.EquipmentDefinitionContentVersion);
            writer.WriteString(drop.AcquisitionSnapshotFingerprint);
            writer.WriteString(drop.SlotId);
            writer.WriteInt32(drop.AttackBonus);
            writer.WriteInt32(drop.DefenseBonus);
            writer.WriteInt32(drop.HealthBonus);
            writer.WriteInt32(drop.Quantity);
            writer.WriteString(drop.StackPolicyId);
            writer.WriteString(drop.AcquisitionAnnouncementPolicyId);
        }

        private static BossRewardPlanningResult Reject(
            BossRewardPlanningStatus status,
            string code,
            string fieldPath,
            string recordId,
            string message)
        {
            var diagnostic = new BossRewardDiagnostic(
                code,
                BossRewardDiagnosticSeverity.Error,
                code.StartsWith("AL-BOSS-REWARD-INVENTORY-", StringComparison.Ordinal)
                    ? BossRewardDiagnosticDomain.Inventory
                    : code.StartsWith("AL-BOSS-REWARD-LEDGER-", StringComparison.Ordinal)
                        ? BossRewardDiagnosticDomain.Ledger
                        : code.StartsWith("AL-BOSS-REWARD-NOTIFICATION-", StringComparison.Ordinal)
                            ? BossRewardDiagnosticDomain.Notification
                            : BossRewardDiagnosticDomain.Transaction,
                fieldPath,
                true,
                message,
                recordId,
                recordId);
            return new BossRewardPlanningResult(status, null, null, new[] { diagnostic });
        }
    }
}
