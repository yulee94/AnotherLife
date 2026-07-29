using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AL.Core.Interfaces;

namespace AL.Kingdom.Progression
{
    public enum ProgressionDomain
    {
        Research = 1,
        Training = 2
    }

    public enum ProgressionOrderType
    {
        ResearchLevel = 1,
        TroopTrainingBatch = 2
    }

    public enum ProgressionCompatibilityStatus
    {
        Available = 0,
        UnavailableCatalog = 1,
        MalformedState = 2
    }

    public enum ProgressionStateOrigin
    {
        Saved = 0,
        EffectiveInitialUnpersisted = 1
    }

    public enum ProgressionDiagnosticCode
    {
        None = 0,
        UnavailableCatalog = 1,
        InvalidCatalogIdentity = 2,
        NullDefinition = 3,
        BlankDefinitionId = 4,
        InvalidDefinitionIdentity = 5,
        DuplicateDefinitionId = 6,
        UnsupportedSchemaVersion = 7,
        InvalidDefinitionRange = 8,
        InvalidCostProfile = 9,
        InvalidDurationProfile = 10,
        InvalidPrerequisite = 11,
        DuplicatePrerequisite = 12,
        InvalidEffectProfile = 13,
        NullStateCollection = 14,
        NullState = 15,
        BlankStateId = 16,
        UnknownDefinition = 17,
        DuplicateStateId = 18,
        UnsupportedContentVersion = 19,
        NegativeLevel = 20,
        OverMaximumLevel = 21,
        ImpossibleTimer = 22,
        NegativeCount = 23,
        OverMaximumCount = 24,
        CountOverflow = 25,
        InvalidRequest = 26,
        StateUnavailable = 27,
        StaleProgressionRevision = 28,
        StaleEconomyRevision = 29,
        PrerequisiteUnmet = 30,
        EconomyMalformed = 31,
        InsufficientResources = 32,
        ArithmeticOverflow = 33,
        CorrelationConflict = 34,
        CommitUncertain = 35,
        RecoveryRequired = 36,
        OrderMalformed = 37,
        ClockInvalid = 38,
        NotYetEligible = 39,
        MigrationRequired = 40,
        InvalidInventoryPolicy = 41,
        DuplicateEffectProfile = 42,
        InputLimitExceeded = 43,
        InvalidStateId = 44,
        BelowInitialLevel = 45,
        PreservedUnknownFutureDefinition = 46,
        InvalidTimestampPolicy = 47
    }

    public enum ProgressionPlanStatus
    {
        Ready = 0,
        NoChange = 1,
        AlreadyCommitted = 2,
        InvalidRequest = 3,
        UnknownDefinition = 4,
        DefinitionUnavailable = 5,
        StateMalformed = 6,
        UnsupportedVersion = 7,
        InvalidTarget = 8,
        AtMaximum = 9,
        PrerequisiteUnmet = 10,
        OrderAlreadyActive = 11,
        CostInvalid = 12,
        InsufficientResources = 13,
        EconomyInvalid = 14,
        StaleProgressionRevision = 15,
        StaleEconomyRevision = 16,
        CorrelationConflict = 17,
        ArithmeticOverflow = 18,
        NotYetEligible = 19,
        AlreadyCompleted = 20,
        OrderMalformed = 21,
        InventoryOverflow = 22,
        ClockInvalid = 23,
        CommitUncertain = 24,
        RecoveryRequired = 25
    }

    public enum ProgressionOperationDurability
    {
        Committed = 0,
        CommitUncertain = 1
    }

    public enum ProgressionOrderState
    {
        Active = 0,
        Completed = 1,
        RecoveryRequired = 2
    }

    public enum ProgressionReplayDisposition
    {
        NoPriorOperation = 0,
        ExactCommittedReplay = 1,
        PayloadConflict = 2,
        StaleExpectedRevision = 3,
        CommitUncertain = 4,
        MalformedLedger = 5
    }

    public enum TroopInventoryCapacityPolicy
    {
        Unresolved = 0,
        SeparatedCountsTotalCapacityV1 = 1
    }

    public sealed class ProgressionSourceIdentity
    {
        public ProgressionSourceIdentity(
            string id,
            string schemaVersion,
            string contentVersion,
            string sourceRevision,
            string rawSha256)
        {
            Id = id ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            ContentVersion = contentVersion ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            RawSha256 = rawSha256 ?? string.Empty;
        }

        public string Id { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
    }

    public sealed class ProgressionCostProfile
    {
        private readonly IReadOnlyList<BuildingConstructionCost> _unitCosts;

        public ProgressionCostProfile(
            ProgressionSourceIdentity identity,
            IEnumerable<BuildingConstructionCost> unitCosts,
            long maximumAmountPerResource)
        {
            Identity = identity;
            _unitCosts = ProgressionCollections.Freeze(unitCosts);
            MaximumAmountPerResource = maximumAmountPerResource;
        }

        public ProgressionSourceIdentity Identity { get; }
        public IReadOnlyList<BuildingConstructionCost> UnitCosts => _unitCosts;
        public long MaximumAmountPerResource { get; }
    }

    public sealed class ProgressionDurationProfile
    {
        public ProgressionDurationProfile(
            ProgressionSourceIdentity identity,
            long unitSeconds,
            long maximumSeconds,
            bool allowsZeroDuration)
        {
            Identity = identity;
            UnitSeconds = unitSeconds;
            MaximumSeconds = maximumSeconds;
            AllowsZeroDuration = allowsZeroDuration;
        }

        public ProgressionSourceIdentity Identity { get; }
        public long UnitSeconds { get; }
        public long MaximumSeconds { get; }
        public bool AllowsZeroDuration { get; }
    }

    public sealed class ProgressionPrerequisite
    {
        public ProgressionPrerequisite(string definitionId, int minimumLevel)
        {
            DefinitionId = definitionId ?? string.Empty;
            MinimumLevel = minimumLevel;
        }

        public string DefinitionId { get; }
        public int MinimumLevel { get; }
    }

    public sealed class ProgressionPrerequisiteTargetDefinition
    {
        public ProgressionPrerequisiteTargetDefinition(
            ProgressionSourceIdentity identity,
            int maximumLevel)
        {
            Identity = identity;
            MaximumLevel = maximumLevel;
        }

        public ProgressionSourceIdentity Identity { get; }
        public int MaximumLevel { get; }
    }

    public sealed class ProgressionTimestampPolicy
    {
        public ProgressionTimestampPolicy(
            string policyVersion,
            long minimumUtcTimestamp,
            long maximumUtcTimestamp)
        {
            PolicyVersion = policyVersion ?? string.Empty;
            MinimumUtcTimestamp = minimumUtcTimestamp;
            MaximumUtcTimestamp = maximumUtcTimestamp;
        }

        public string PolicyVersion { get; }
        public long MinimumUtcTimestamp { get; }
        public long MaximumUtcTimestamp { get; }
    }

    public sealed class ResearchProgressionDefinition
    {
        private readonly IReadOnlyList<ProgressionPrerequisite> _prerequisites;
        private readonly IReadOnlyList<ProgressionSourceIdentity> _effectProfiles;

        public ResearchProgressionDefinition(
            ProgressionSourceIdentity identity,
            int initialLevel,
            int maximumLevel,
            ProgressionCostProfile costProfile,
            ProgressionDurationProfile durationProfile,
            IEnumerable<ProgressionPrerequisite> prerequisites,
            IEnumerable<ProgressionSourceIdentity> effectProfiles)
        {
            Identity = identity;
            InitialLevel = initialLevel;
            MaximumLevel = maximumLevel;
            CostProfile = costProfile;
            DurationProfile = durationProfile;
            _prerequisites = ProgressionCollections.Freeze(prerequisites);
            _effectProfiles = ProgressionCollections.Freeze(effectProfiles);
        }

        public ProgressionSourceIdentity Identity { get; }
        public int InitialLevel { get; }
        public int MaximumLevel { get; }
        public ProgressionCostProfile CostProfile { get; }
        public ProgressionDurationProfile DurationProfile { get; }
        public IReadOnlyList<ProgressionPrerequisite> Prerequisites => _prerequisites;
        public IReadOnlyList<ProgressionSourceIdentity> EffectProfiles => _effectProfiles;
    }

    public sealed class TroopProgressionDefinition
    {
        private readonly IReadOnlyList<ProgressionPrerequisite> _prerequisites;

        public TroopProgressionDefinition(
            ProgressionSourceIdentity identity,
            long maximumInventoryCount,
            long maximumBatchCount,
            ProgressionCostProfile costProfile,
            ProgressionDurationProfile durationProfile,
            IEnumerable<ProgressionPrerequisite> prerequisites,
            ProgressionSourceIdentity battleProfile,
            ProgressionSourceIdentity inventoryPolicy,
            TroopInventoryCapacityPolicy inventoryCapacityPolicy)
        {
            Identity = identity;
            MaximumInventoryCount = maximumInventoryCount;
            MaximumBatchCount = maximumBatchCount;
            CostProfile = costProfile;
            DurationProfile = durationProfile;
            _prerequisites = ProgressionCollections.Freeze(prerequisites);
            BattleProfile = battleProfile;
            InventoryPolicy = inventoryPolicy;
            InventoryCapacityPolicy = inventoryCapacityPolicy;
        }

        public ProgressionSourceIdentity Identity { get; }
        public long MaximumInventoryCount { get; }
        public long MaximumBatchCount { get; }
        public ProgressionCostProfile CostProfile { get; }
        public ProgressionDurationProfile DurationProfile { get; }
        public IReadOnlyList<ProgressionPrerequisite> Prerequisites => _prerequisites;
        public ProgressionSourceIdentity BattleProfile { get; }
        public ProgressionSourceIdentity InventoryPolicy { get; }
        public TroopInventoryCapacityPolicy InventoryCapacityPolicy { get; }
    }

    public sealed class ResearchProgressionStateRecord
    {
        public ResearchProgressionStateRecord(
            string definitionId,
            string definitionContentVersion,
            int level,
            bool hasActiveLegacyOrder,
            long completionTimestamp)
        {
            DefinitionId = definitionId;
            DefinitionContentVersion = definitionContentVersion;
            Level = level;
            HasActiveLegacyOrder = hasActiveLegacyOrder;
            CompletionTimestamp = completionTimestamp;
        }

        public string DefinitionId { get; }
        public string DefinitionContentVersion { get; }
        public int Level { get; }
        public bool HasActiveLegacyOrder { get; }
        public long CompletionTimestamp { get; }
    }

    public sealed class TroopProgressionStateRecord
    {
        public TroopProgressionStateRecord(
            string definitionId,
            string definitionContentVersion,
            long activeCount,
            long woundedCount,
            long reservedCount)
        {
            DefinitionId = definitionId;
            DefinitionContentVersion = definitionContentVersion;
            ActiveCount = activeCount;
            WoundedCount = woundedCount;
            ReservedCount = reservedCount;
        }

        public string DefinitionId { get; }
        public string DefinitionContentVersion { get; }
        public long ActiveCount { get; }
        public long WoundedCount { get; }
        public long ReservedCount { get; }
    }

    public sealed class ResearchProgressionSnapshot
    {
        public ResearchProgressionSnapshot(
            ResearchProgressionDefinition definition,
            int level,
            ProgressionStateOrigin origin,
            bool hasActiveLegacyOrder,
            long completionTimestamp)
        {
            Definition = definition;
            Level = level;
            Origin = origin;
            HasActiveLegacyOrder = hasActiveLegacyOrder;
            CompletionTimestamp = completionTimestamp;
        }

        public ResearchProgressionDefinition Definition { get; }
        public int Level { get; }
        public ProgressionStateOrigin Origin { get; }
        public bool HasActiveLegacyOrder { get; }
        public long CompletionTimestamp { get; }
    }

    public sealed class TroopProgressionSnapshot
    {
        public TroopProgressionSnapshot(
            TroopProgressionDefinition definition,
            long activeCount,
            long woundedCount,
            long reservedCount,
            ProgressionStateOrigin origin)
        {
            Definition = definition;
            ActiveCount = activeCount;
            WoundedCount = woundedCount;
            ReservedCount = reservedCount;
            Origin = origin;
        }

        public TroopProgressionDefinition Definition { get; }
        public long ActiveCount { get; }
        public long WoundedCount { get; }
        public long ReservedCount { get; }
        public ProgressionStateOrigin Origin { get; }
    }

    public sealed class ProgressionDiagnostic
    {
        public ProgressionDiagnostic(
            ProgressionDiagnosticCode code,
            ProgressionDomain domain,
            string definitionId,
            int sourceIndex)
        {
            Code = code;
            Domain = domain;
            DefinitionId = definitionId ?? string.Empty;
            SourceIndex = sourceIndex;
        }

        public ProgressionDiagnosticCode Code { get; }
        public ProgressionDomain Domain { get; }
        public string DefinitionId { get; }
        public int SourceIndex { get; }
    }

    public sealed class ProgressionCompatibilityResult
    {
        private readonly IReadOnlyList<ResearchProgressionSnapshot> _research;
        private readonly IReadOnlyList<TroopProgressionSnapshot> _troops;
        private readonly IReadOnlyList<ResearchProgressionStateRecord> _preservedResearchStates;
        private readonly IReadOnlyList<TroopProgressionStateRecord> _preservedTroopStates;
        private readonly IReadOnlyList<ProgressionDiagnostic> _diagnostics;

        public ProgressionCompatibilityResult(
            ProgressionDomain domain,
            ProgressionCompatibilityStatus status,
            string catalogSetId,
            string catalogRevision,
            string stateRevision,
            IEnumerable<ResearchProgressionSnapshot> research,
            IEnumerable<TroopProgressionSnapshot> troops,
            IEnumerable<ResearchProgressionStateRecord> preservedResearchStates,
            IEnumerable<TroopProgressionStateRecord> preservedTroopStates,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            Domain = domain;
            Status = status;
            CatalogSetId = catalogSetId ?? string.Empty;
            CatalogRevision = catalogRevision ?? string.Empty;
            StateRevision = stateRevision ?? string.Empty;
            _research = ProgressionCollections.Freeze(research);
            _troops = ProgressionCollections.Freeze(troops);
            _preservedResearchStates = ProgressionCollections.Freeze(preservedResearchStates);
            _preservedTroopStates = ProgressionCollections.Freeze(preservedTroopStates);
            _diagnostics = ProgressionCollections.Freeze(diagnostics);
        }

        public ProgressionDomain Domain { get; }
        public ProgressionCompatibilityStatus Status { get; }
        public string CatalogSetId { get; }
        public string CatalogRevision { get; }
        public string StateRevision { get; }
        public IReadOnlyList<ResearchProgressionSnapshot> Research => _research;
        public IReadOnlyList<TroopProgressionSnapshot> Troops => _troops;
        public IReadOnlyList<ResearchProgressionStateRecord> PreservedResearchStates =>
            _preservedResearchStates;
        public IReadOnlyList<TroopProgressionStateRecord> PreservedTroopStates =>
            _preservedTroopStates;
        public IReadOnlyList<ProgressionDiagnostic> Diagnostics => _diagnostics;
    }

    public sealed class ProgressionResourceBalance
    {
        public ProgressionResourceBalance(AL.Core.ResourceType resourceType, long amount)
        {
            ResourceType = resourceType;
            Amount = amount;
        }

        public AL.Core.ResourceType ResourceType { get; }
        public long Amount { get; }
    }

    public sealed class ProgressionEconomySnapshot
    {
        private readonly IReadOnlyList<ProgressionResourceBalance> _balances;

        public ProgressionEconomySnapshot(
            string revision,
            IEnumerable<ProgressionResourceBalance> balances)
        {
            Revision = revision ?? string.Empty;
            HasBalanceSource = balances != null;
            _balances = ProgressionCollections.Freeze(balances);
        }

        public string Revision { get; }
        public bool HasBalanceSource { get; }
        public IReadOnlyList<ProgressionResourceBalance> Balances => _balances;
    }

    public sealed class ProgressionLevelValue
    {
        public ProgressionLevelValue(string definitionId, int level)
        {
            DefinitionId = definitionId ?? string.Empty;
            Level = level;
        }

        public string DefinitionId { get; }
        public int Level { get; }
    }

    public sealed class ProgressionPrerequisiteSnapshot
    {
        private readonly IReadOnlyList<ProgressionLevelValue> _levels;

        public ProgressionPrerequisiteSnapshot(
            string revision,
            IEnumerable<ProgressionLevelValue> levels)
        {
            Revision = revision ?? string.Empty;
            HasLevelSource = levels != null;
            _levels = ProgressionCollections.Freeze(levels);
        }

        public string Revision { get; }
        public bool HasLevelSource { get; }
        public IReadOnlyList<ProgressionLevelValue> Levels => _levels;
    }

    public sealed class ProgressionCompletionDependencySnapshot
    {
        public ProgressionCompletionDependencySnapshot(
            string economyRevision,
            string questRevision)
        {
            EconomyRevision = economyRevision ?? string.Empty;
            QuestRevision = questRevision ?? string.Empty;
        }

        public string EconomyRevision { get; }
        public string QuestRevision { get; }
    }

    public sealed class ProgressionStartRequest
    {
        public ProgressionStartRequest(
            string profileId,
            ProgressionOrderType orderType,
            string definitionId,
            string orderId,
            string operationId,
            int requestedTargetLevel,
            long requestedBatchCount,
            string expectedCatalogSetId,
            string expectedProgressionRevision,
            string expectedEconomyRevision,
            string requestPolicyVersion)
        {
            ProfileId = profileId ?? string.Empty;
            OrderType = orderType;
            DefinitionId = definitionId ?? string.Empty;
            OrderId = orderId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            RequestedTargetLevel = requestedTargetLevel;
            RequestedBatchCount = requestedBatchCount;
            ExpectedCatalogSetId = expectedCatalogSetId ?? string.Empty;
            ExpectedProgressionRevision = expectedProgressionRevision ?? string.Empty;
            ExpectedEconomyRevision = expectedEconomyRevision ?? string.Empty;
            RequestPolicyVersion = requestPolicyVersion ?? string.Empty;
        }

        public string ProfileId { get; }
        public ProgressionOrderType OrderType { get; }
        public string DefinitionId { get; }
        public string OrderId { get; }
        public string OperationId { get; }
        public int RequestedTargetLevel { get; }
        public long RequestedBatchCount { get; }
        public string ExpectedCatalogSetId { get; }
        public string ExpectedProgressionRevision { get; }
        public string ExpectedEconomyRevision { get; }
        public string RequestPolicyVersion { get; }
    }

    public sealed class ProgressionOperationReceipt
    {
        public ProgressionOperationReceipt(
            string operationId,
            string semanticHash,
            string resultHash,
            ProgressionOperationDurability durability)
        {
            OperationId = operationId ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            ResultHash = resultHash ?? string.Empty;
            Durability = durability;
        }

        public string OperationId { get; }
        public string SemanticHash { get; }
        public string ResultHash { get; }
        public ProgressionOperationDurability Durability { get; }
    }

    public sealed class ProgressionReplayClassification
    {
        public ProgressionReplayClassification(
            ProgressionReplayDisposition disposition,
            ProgressionOperationReceipt receipt)
        {
            Disposition = disposition;
            Receipt = receipt;
        }

        public ProgressionReplayDisposition Disposition { get; }
        public ProgressionOperationReceipt Receipt { get; }
    }

    public sealed class ProgressionStartPlan
    {
        private readonly IReadOnlyList<BuildingConstructionCost> _costs;
        private readonly IReadOnlyList<ProgressionDiagnostic> _diagnostics;

        public ProgressionStartPlan(
            ProgressionPlanStatus status,
            ProgressionOrderType orderType,
            string profileId,
            string definitionId,
            string definitionContentVersion,
            ProgressionSourceIdentity definitionSource,
            ProgressionSourceIdentity costProfile,
            ProgressionSourceIdentity durationProfile,
            string orderId,
            string operationId,
            long previousValue,
            long targetValue,
            long batchCount,
            IEnumerable<BuildingConstructionCost> costs,
            long startTimestamp,
            long endTimestamp,
            string catalogSetId,
            string progressionRevision,
            string economyRevision,
            string requestPolicyVersion,
            string semanticHash,
            string planHash,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            Status = status;
            OrderType = orderType;
            ProfileId = profileId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            DefinitionContentVersion = definitionContentVersion ?? string.Empty;
            DefinitionSource = definitionSource;
            CostProfile = costProfile;
            DurationProfile = durationProfile;
            OrderId = orderId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            PreviousValue = previousValue;
            TargetValue = targetValue;
            BatchCount = batchCount;
            _costs = ProgressionCollections.Freeze(costs);
            StartTimestamp = startTimestamp;
            EndTimestamp = endTimestamp;
            CatalogSetId = catalogSetId ?? string.Empty;
            ProgressionRevision = progressionRevision ?? string.Empty;
            EconomyRevision = economyRevision ?? string.Empty;
            RequestPolicyVersion = requestPolicyVersion ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            _diagnostics = ProgressionCollections.Freeze(diagnostics);
        }

        public ProgressionPlanStatus Status { get; }
        public ProgressionOrderType OrderType { get; }
        public string ProfileId { get; }
        public string DefinitionId { get; }
        public string DefinitionContentVersion { get; }
        public ProgressionSourceIdentity DefinitionSource { get; }
        public ProgressionSourceIdentity CostProfile { get; }
        public ProgressionSourceIdentity DurationProfile { get; }
        public string OrderId { get; }
        public string OperationId { get; }
        public long PreviousValue { get; }
        public long TargetValue { get; }
        public long BatchCount { get; }
        public IReadOnlyList<BuildingConstructionCost> Costs => _costs;
        public long StartTimestamp { get; }
        public long EndTimestamp { get; }
        public string CatalogSetId { get; }
        public string ProgressionRevision { get; }
        public string EconomyRevision { get; }
        public string RequestPolicyVersion { get; }
        public string SemanticHash { get; }
        public string PlanHash { get; }
        public IReadOnlyList<ProgressionDiagnostic> Diagnostics => _diagnostics;
        public bool CanCommit => Status == ProgressionPlanStatus.Ready;
    }

    public sealed class ProgressionOrderSnapshot
    {
        private readonly IReadOnlyList<BuildingConstructionCost> _committedCosts;

        public ProgressionOrderSnapshot(
            ProgressionOrderType orderType,
            ProgressionOrderState state,
            string profileId,
            string definitionId,
            string definitionContentVersion,
            ProgressionSourceIdentity definitionSource,
            ProgressionSourceIdentity costProfile,
            ProgressionSourceIdentity durationProfile,
            string orderId,
            string startOperationId,
            string completionOperationId,
            string cancellationOperationId,
            long previousValue,
            long targetValue,
            long batchCount,
            IEnumerable<BuildingConstructionCost> committedCosts,
            long startTimestamp,
            long endTimestamp,
            string catalogSetId,
            string progressionRevision,
            string economyRevision,
            string requestPolicyVersion,
            string orderHash)
        {
            OrderType = orderType;
            State = state;
            ProfileId = profileId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            DefinitionContentVersion = definitionContentVersion ?? string.Empty;
            DefinitionSource = definitionSource;
            CostProfile = costProfile;
            DurationProfile = durationProfile;
            OrderId = orderId ?? string.Empty;
            StartOperationId = startOperationId ?? string.Empty;
            CompletionOperationId = completionOperationId ?? string.Empty;
            CancellationOperationId = cancellationOperationId ?? string.Empty;
            PreviousValue = previousValue;
            TargetValue = targetValue;
            BatchCount = batchCount;
            _committedCosts = ProgressionCollections.Freeze(committedCosts);
            StartTimestamp = startTimestamp;
            EndTimestamp = endTimestamp;
            CatalogSetId = catalogSetId ?? string.Empty;
            ProgressionRevision = progressionRevision ?? string.Empty;
            EconomyRevision = economyRevision ?? string.Empty;
            RequestPolicyVersion = requestPolicyVersion ?? string.Empty;
            OrderHash = orderHash ?? string.Empty;
        }

        public ProgressionOrderType OrderType { get; }
        public ProgressionOrderState State { get; }
        public string ProfileId { get; }
        public string DefinitionId { get; }
        public string DefinitionContentVersion { get; }
        public ProgressionSourceIdentity DefinitionSource { get; }
        public ProgressionSourceIdentity CostProfile { get; }
        public ProgressionSourceIdentity DurationProfile { get; }
        public string OrderId { get; }
        public string StartOperationId { get; }
        public string CompletionOperationId { get; }
        public string CancellationOperationId { get; }
        public long PreviousValue { get; }
        public long TargetValue { get; }
        public long BatchCount { get; }
        public IReadOnlyList<BuildingConstructionCost> CommittedCosts =>
            _committedCosts;
        public long StartTimestamp { get; }
        public long EndTimestamp { get; }
        public string CatalogSetId { get; }
        public string ProgressionRevision { get; }
        public string EconomyRevision { get; }
        public string RequestPolicyVersion { get; }
        public string OrderHash { get; }
    }

    public sealed class ProgressionCompletionRequest
    {
        public ProgressionCompletionRequest(
            string profileId,
            string orderId,
            string operationId,
            string expectedCatalogSetId,
            string expectedProgressionRevision,
            string expectedEconomyRevision,
            string expectedQuestRevision,
            string completionPolicyVersion)
        {
            ProfileId = profileId ?? string.Empty;
            OrderId = orderId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            ExpectedCatalogSetId = expectedCatalogSetId ?? string.Empty;
            ExpectedProgressionRevision = expectedProgressionRevision ?? string.Empty;
            ExpectedEconomyRevision = expectedEconomyRevision ?? string.Empty;
            ExpectedQuestRevision = expectedQuestRevision ?? string.Empty;
            CompletionPolicyVersion = completionPolicyVersion ?? string.Empty;
        }

        public string ProfileId { get; }
        public string OrderId { get; }
        public string OperationId { get; }
        public string ExpectedCatalogSetId { get; }
        public string ExpectedProgressionRevision { get; }
        public string ExpectedEconomyRevision { get; }
        public string ExpectedQuestRevision { get; }
        public string CompletionPolicyVersion { get; }
    }

    public sealed class ProgressionCompletionPlan
    {
        private readonly IReadOnlyList<ProgressionDiagnostic> _diagnostics;

        public ProgressionCompletionPlan(
            ProgressionPlanStatus status,
            ProgressionOrderType orderType,
            string definitionId,
            string orderId,
            string operationId,
            long previousValue,
            long targetValue,
            long questProgressAmount,
            string catalogSetId,
            string progressionRevision,
            string economyRevision,
            string questRevision,
            string completionPolicyVersion,
            string semanticHash,
            string planHash,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            Status = status;
            OrderType = orderType;
            DefinitionId = definitionId ?? string.Empty;
            OrderId = orderId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            PreviousValue = previousValue;
            TargetValue = targetValue;
            QuestProgressAmount = questProgressAmount;
            CatalogSetId = catalogSetId ?? string.Empty;
            ProgressionRevision = progressionRevision ?? string.Empty;
            EconomyRevision = economyRevision ?? string.Empty;
            QuestRevision = questRevision ?? string.Empty;
            CompletionPolicyVersion = completionPolicyVersion ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            _diagnostics = ProgressionCollections.Freeze(diagnostics);
        }

        public ProgressionPlanStatus Status { get; }
        public ProgressionOrderType OrderType { get; }
        public string DefinitionId { get; }
        public string OrderId { get; }
        public string OperationId { get; }
        public long PreviousValue { get; }
        public long TargetValue { get; }
        public long QuestProgressAmount { get; }
        public string CatalogSetId { get; }
        public string ProgressionRevision { get; }
        public string EconomyRevision { get; }
        public string QuestRevision { get; }
        public string CompletionPolicyVersion { get; }
        public string SemanticHash { get; }
        public string PlanHash { get; }
        public IReadOnlyList<ProgressionDiagnostic> Diagnostics => _diagnostics;
        public bool CanCommit => Status == ProgressionPlanStatus.Ready;
    }

    public sealed class ProgressionReconciliationPlan
    {
        private readonly IReadOnlyList<ProgressionOrderSnapshot> _eligibleOrders;
        private readonly IReadOnlyList<ProgressionDiagnostic> _diagnostics;

        public ProgressionReconciliationPlan(
            ProgressionPlanStatus status,
            IEnumerable<ProgressionOrderSnapshot> eligibleOrders,
            string planHash,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            Status = status;
            _eligibleOrders = ProgressionCollections.Freeze(eligibleOrders);
            PlanHash = planHash ?? string.Empty;
            _diagnostics = ProgressionCollections.Freeze(diagnostics);
        }

        public ProgressionPlanStatus Status { get; }
        public IReadOnlyList<ProgressionOrderSnapshot> EligibleOrders => _eligibleOrders;
        public string PlanHash { get; }
        public IReadOnlyList<ProgressionDiagnostic> Diagnostics => _diagnostics;
    }

    public sealed class ResearchEffectReference
    {
        public ResearchEffectReference(
            string researchDefinitionId,
            string researchContentVersion,
            int level,
            ProgressionSourceIdentity effectProfile)
        {
            ResearchDefinitionId = researchDefinitionId ?? string.Empty;
            ResearchContentVersion = researchContentVersion ?? string.Empty;
            Level = level;
            EffectProfile = effectProfile;
        }

        public string ResearchDefinitionId { get; }
        public string ResearchContentVersion { get; }
        public int Level { get; }
        public ProgressionSourceIdentity EffectProfile { get; }
    }

    public sealed class ResearchEffectSnapshot
    {
        private readonly IReadOnlyList<ResearchEffectReference> _effects;
        private readonly IReadOnlyList<ProgressionDiagnostic> _diagnostics;

        public ResearchEffectSnapshot(
            ProgressionPlanStatus status,
            string catalogSetId,
            string progressionRevision,
            string snapshotHash,
            IEnumerable<ResearchEffectReference> effects,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            Status = status;
            CatalogSetId = catalogSetId ?? string.Empty;
            ProgressionRevision = progressionRevision ?? string.Empty;
            SnapshotHash = snapshotHash ?? string.Empty;
            _effects = ProgressionCollections.Freeze(effects);
            _diagnostics = ProgressionCollections.Freeze(diagnostics);
        }

        public ProgressionPlanStatus Status { get; }
        public string CatalogSetId { get; }
        public string ProgressionRevision { get; }
        public string SnapshotHash { get; }
        public IReadOnlyList<ResearchEffectReference> Effects => _effects;
        public IReadOnlyList<ProgressionDiagnostic> Diagnostics => _diagnostics;
    }

    internal static class ProgressionCollections
    {
        internal const int MaximumFrozenItems = 16385;

        internal static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values)
        {
            var frozen = new List<T>();
            if (values == null)
            {
                return frozen.AsReadOnly();
            }

            foreach (T value in values)
            {
                if (frozen.Count == MaximumFrozenItems)
                {
                    break;
                }

                frozen.Add(value);
            }

            return frozen.AsReadOnly();
        }
    }

    internal static class ProgressionContractHash
    {
        internal static string Compute(params string[] segments)
        {
            var canonical = new StringBuilder();
            foreach (string segment in segments ?? Array.Empty<string>())
            {
                string value = segment ?? string.Empty;
                canonical
                    .Append(Encoding.UTF8
                        .GetByteCount(value)
                        .ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value)
                    .Append(';');
            }

            byte[] payload = Encoding.UTF8.GetBytes(canonical.ToString());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(payload);
                var hexadecimal = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                {
                    hexadecimal.Append(value.ToString("x2"));
                }

                return hexadecimal.ToString();
            }
        }
    }
}
