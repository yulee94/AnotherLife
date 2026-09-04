using System;
using System.Collections.Generic;

namespace AL.Core.BossRewards
{
    public enum BossRewardCandidateApplicationStatus
    {
        CandidatePrepared = 0,
        ExplicitNoRewardPrepared = 1,
        AlreadyCommitted = 2,
        SourceUnavailable = 3,
        CatalogRejected = 4,
        UnknownBoss = 5,
        InvalidRequest = 6,
        ComputationRejected = 7,
        CorrelationConflict = 8,
        UncertainCommit = 9,
        PlanningRejected = 10
    }

    public sealed class BossRewardCandidateApplicationRequest
    {
        public BossRewardCandidateApplicationRequest(
            byte[] sourceBytes,
            string profileId,
            string encounterId,
            string encounterCompletionId,
            string rewardResultId,
            string bossDefinitionId,
            bool requirePinnedSource,
            bool isSaveAvailable,
            string saveRevision,
            string economyRevision,
            string inventoryRevision,
            string ledgerRevision,
            BossRewardEconomySnapshot economy,
            IEnumerable<OwnedEquipmentSnapshot> inventoryRows,
            BossRewardLedgerSnapshot ledger,
            IEnumerable<string> availableNotificationDefinitionIds,
            long plannedUtcSeconds)
        {
            SourceBytes = sourceBytes;
            ProfileId = profileId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            EncounterCompletionId = encounterCompletionId ?? string.Empty;
            RewardResultId = rewardResultId ?? string.Empty;
            BossDefinitionId = bossDefinitionId ?? string.Empty;
            RequirePinnedSource = requirePinnedSource;
            IsSaveAvailable = isSaveAvailable;
            SaveRevision = saveRevision ?? string.Empty;
            EconomyRevision = economyRevision ?? string.Empty;
            InventoryRevision = inventoryRevision ?? string.Empty;
            LedgerRevision = ledgerRevision ?? string.Empty;
            Economy = economy;
            InventoryRows = inventoryRows;
            Ledger = ledger;
            AvailableNotificationDefinitionIds = availableNotificationDefinitionIds;
            PlannedUtcSeconds = plannedUtcSeconds;
        }

        public byte[] SourceBytes { get; }
        public string ProfileId { get; }
        public string EncounterId { get; }
        public string EncounterCompletionId { get; }
        public string RewardResultId { get; }
        public string BossDefinitionId { get; }
        public bool RequirePinnedSource { get; }
        public bool IsSaveAvailable { get; }
        public string SaveRevision { get; }
        public string EconomyRevision { get; }
        public string InventoryRevision { get; }
        public string LedgerRevision { get; }
        public BossRewardEconomySnapshot Economy { get; }
        public IEnumerable<OwnedEquipmentSnapshot> InventoryRows { get; }
        public BossRewardLedgerSnapshot Ledger { get; }
        public IEnumerable<string> AvailableNotificationDefinitionIds { get; }
        public long PlannedUtcSeconds { get; }
    }

    public sealed class BossRewardCandidateReceipt
    {
        internal BossRewardCandidateReceipt(
            string rewardResultId,
            string encounterId,
            string encounterCompletionId,
            string bossDefinitionId,
            string computationHash,
            string planHash,
            int warzoneCredits,
            int dropCount,
            IEnumerable<string> outboxCorrelationIds)
        {
            RewardResultId = rewardResultId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            EncounterCompletionId = encounterCompletionId ?? string.Empty;
            BossDefinitionId = bossDefinitionId ?? string.Empty;
            ComputationHash = computationHash ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            WarzoneCredits = warzoneCredits;
            DropCount = dropCount;
            OutboxCorrelationIds = BossRewardImmutable.Freeze(
                outboxCorrelationIds ?? Array.Empty<string>(),
                BossRewardTechnicalLimits.MaximumRewardEntries + 1);
        }

        public string RewardResultId { get; }
        public string EncounterId { get; }
        public string EncounterCompletionId { get; }
        public string BossDefinitionId { get; }
        public string ComputationHash { get; }
        public string PlanHash { get; }
        public int WarzoneCredits { get; }
        public int DropCount { get; }
        public IReadOnlyList<string> OutboxCorrelationIds { get; }
    }

    public sealed class BossRewardCandidateApplicationResult
    {
        internal BossRewardCandidateApplicationResult(
            BossRewardCandidateApplicationStatus status,
            string diagnosticCode,
            BossRewardApplicationPlan plan,
            BossRewardCandidateReceipt receipt,
            IEnumerable<BossRewardNotificationIntent> outboxIntents,
            BossRewardAppliedLedgerRecord existingRecord)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Plan = plan;
            Receipt = receipt;
            OutboxIntents = BossRewardImmutable.Freeze(
                outboxIntents ?? Array.Empty<BossRewardNotificationIntent>(),
                BossRewardTechnicalLimits.MaximumRewardEntries + 1);
            ExistingRecord = existingRecord;
        }

        public BossRewardCandidateApplicationStatus Status { get; }
        public string DiagnosticCode { get; }
        public BossRewardApplicationPlan Plan { get; }
        public BossRewardCandidateReceipt Receipt { get; }
        public IReadOnlyList<BossRewardNotificationIntent> OutboxIntents { get; }
        public BossRewardAppliedLedgerRecord ExistingRecord { get; }
        public bool AllowsMutation => false;
        public string MutationActivation => BossRewardSourceCatalog.MutationActivation;
    }

    public static class BossRewardCandidateApplication
    {
        public const string InvalidRequestCode =
            "AL-BOSS-REWARD-CANDIDATE-REQUEST-INVALID";
        public const string SourceUnavailableCode =
            "AL-BOSS-REWARD-CANDIDATE-SOURCE-UNAVAILABLE";
        public const string CatalogRejectedCode =
            "AL-BOSS-REWARD-CANDIDATE-CATALOG-REJECTED";
        public const string UnknownBossCode =
            "AL-BOSS-REWARD-CANDIDATE-UNKNOWN-BOSS";
        public const string ComputationRejectedCode =
            "AL-BOSS-REWARD-CANDIDATE-COMPUTATION-REJECTED";
        public const string PlanningRejectedCode =
            "AL-BOSS-REWARD-CANDIDATE-PLANNING-REJECTED";

        public static BossRewardCandidateApplicationResult Prepare(
            BossRewardCandidateApplicationRequest request)
        {
            if (request == null)
                return Reject(BossRewardCandidateApplicationStatus.InvalidRequest, InvalidRequestCode);
            if (!BossRewardText.IsBoundedOpaqueId(request.ProfileId) ||
                !BossRewardText.IsBoundedOpaqueId(request.EncounterId) ||
                !BossRewardText.IsBoundedOpaqueId(request.EncounterCompletionId) ||
                !BossRewardText.IsBoundedOpaqueId(request.RewardResultId) ||
                !BossRewardText.IsCanonicalTechnicalId(request.BossDefinitionId) ||
                !BossRewardText.IsBoundedRevision(request.SaveRevision) ||
                !BossRewardText.IsBoundedRevision(request.EconomyRevision) ||
                !BossRewardText.IsBoundedRevision(request.InventoryRevision) ||
                !BossRewardText.IsBoundedRevision(request.LedgerRevision) ||
                request.PlannedUtcSeconds < 0)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.InvalidRequest,
                    InvalidRequestCode);
            }

            if (request.SourceBytes == null)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.SourceUnavailable,
                    SourceUnavailableCode);
            }

            BossRewardSourceCatalogLoadResult loaded = request.RequirePinnedSource
                ? BossRewardSourceCatalog.LoadPinned(request.SourceBytes)
                : BossRewardSourceCatalog.Load(request.SourceBytes);
            if (loaded == null || !loaded.IsReady)
            {
                if (loaded == null ||
                    loaded.Status == BossRewardSourceCatalogStatus.SourceUnavailable)
                {
                    return Reject(
                        BossRewardCandidateApplicationStatus.SourceUnavailable,
                        SourceUnavailableCode);
                }

                return Reject(
                    BossRewardCandidateApplicationStatus.CatalogRejected,
                    string.IsNullOrEmpty(loaded.DiagnosticCode)
                        ? CatalogRejectedCode
                        : loaded.DiagnosticCode);
            }

            BossRewardSourceResolution resolved = BossRewardSourceCatalog.Resolve(
                loaded,
                request.BossDefinitionId);
            if (resolved.Status == BossRewardSourceCatalogStatus.UnknownBoss)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.UnknownBoss,
                    UnknownBossCode);
            }

            if (!resolved.IsFound)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.CatalogRejected,
                    CatalogRejectedCode);
            }

            BossRewardComputationResult computation = BossRewardComputation.Compute(
                new BossRewardComputationRequest(
                    loaded.Snapshot.GameId,
                    loaded.Snapshot.CatalogSetId,
                    request.ProfileId,
                    request.EncounterId,
                    request.EncounterCompletionId,
                    request.RewardResultId,
                    resolved.Binding.BossDefinitionId,
                    resolved.Binding.BossDefinitionContentVersion,
                    resolved.Binding.RewardProfileId,
                    resolved.Binding.RewardProfileContentVersion,
                    BossRewardTechnicalLimits.SupportedDeterminismVersion),
                loaded.Snapshot);
            if (computation.Status == BossRewardComputationStatus.InvalidRequest)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.InvalidRequest,
                    InvalidRequestCode);
            }

            if (computation.Status != BossRewardComputationStatus.Computed &&
                computation.Status != BossRewardComputationStatus.ExplicitNoReward)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.ComputationRejected,
                    ComputationRejectedCode);
            }

            OwnedEquipmentQueryResult inventory = BossRewardInventoryValidator.Validate(
                request.InventoryRows,
                request.InventoryRevision,
                loaded.Snapshot,
                BossRewardTechnicalLimits.SupportedInventorySchemaVersion);
            IEnumerable<string> notificationIds = request.AvailableNotificationDefinitionIds ??
                loaded.Snapshot.AnnouncementPolicyIds;
            var context = new BossRewardPlanningContext(
                request.IsSaveAvailable,
                request.SaveRevision,
                loaded.Snapshot.GameId,
                request.ProfileId,
                loaded.Snapshot.CatalogSetId,
                loaded.Snapshot,
                request.Economy,
                inventory,
                request.Ledger,
                notificationIds,
                request.PlannedUtcSeconds);
            var applicationRequest = new BossRewardApplicationRequest(
                computation,
                request.SaveRevision,
                request.EconomyRevision,
                request.InventoryRevision,
                request.LedgerRevision,
                loaded.Snapshot.CatalogSetId,
                loaded.Snapshot.Revision,
                BossRewardTechnicalLimits.SupportedApplicationPolicyVersion);
            BossRewardPlanningResult planned = BossRewardApplicationPlanner.Plan(
                applicationRequest,
                context);
            return MapPlanning(planned);
        }

        private static BossRewardCandidateApplicationResult MapPlanning(
            BossRewardPlanningResult planned)
        {
            if (planned == null)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.PlanningRejected,
                    PlanningRejectedCode);
            }

            if (planned.Status == BossRewardPlanningStatus.Ready ||
                planned.Status == BossRewardPlanningStatus.ExplicitNoRewardReady)
            {
                BossRewardApplicationPlan plan = planned.Plan;
                var correlationIds = new string[plan.DurableNotificationIntents.Count];
                for (int index = 0; index < plan.DurableNotificationIntents.Count; index++)
                    correlationIds[index] = plan.DurableNotificationIntents[index].CorrelationId;
                var receipt = new BossRewardCandidateReceipt(
                    plan.RewardResultId,
                    plan.LedgerRecord.EncounterId,
                    plan.LedgerRecord.EncounterCompletionId,
                    plan.LedgerRecord.BossDefinitionId,
                    plan.ComputationHash,
                    plan.PlanHash,
                    plan.LedgerRecord.WarzoneCredits,
                    plan.LedgerRecord.CommittedDrops.Count,
                    correlationIds);
                return new BossRewardCandidateApplicationResult(
                    planned.Status == BossRewardPlanningStatus.ExplicitNoRewardReady
                        ? BossRewardCandidateApplicationStatus.ExplicitNoRewardPrepared
                        : BossRewardCandidateApplicationStatus.CandidatePrepared,
                    string.Empty,
                    plan,
                    receipt,
                    plan.DurableNotificationIntents,
                    null);
            }

            if (planned.Status == BossRewardPlanningStatus.AlreadyCommitted)
            {
                BossRewardAppliedLedgerRecord existing = planned.ExistingRecord;
                var receipt = new BossRewardCandidateReceipt(
                    existing.RewardResultId,
                    existing.EncounterId,
                    existing.EncounterCompletionId,
                    existing.BossDefinitionId,
                    existing.ComputationHash,
                    string.Empty,
                    existing.WarzoneCredits,
                    existing.CommittedDrops.Count,
                    existing.NotificationCorrelationIds);
                return new BossRewardCandidateApplicationResult(
                    BossRewardCandidateApplicationStatus.AlreadyCommitted,
                    string.Empty,
                    null,
                    receipt,
                    Array.Empty<BossRewardNotificationIntent>(),
                    existing);
            }

            if (planned.Status == BossRewardPlanningStatus.CorrelationConflict)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.CorrelationConflict,
                    FirstCode(planned, "AL-BOSS-REWARD-LEDGER-CORRELATION-CONFLICT"));
            }

            if (planned.Status == BossRewardPlanningStatus.PendingRecovery)
            {
                return Reject(
                    BossRewardCandidateApplicationStatus.UncertainCommit,
                    FirstCode(planned, "AL-BOSS-REWARD-LEDGER-PENDING-RECOVERY"));
            }

            return Reject(
                BossRewardCandidateApplicationStatus.PlanningRejected,
                FirstCode(planned, PlanningRejectedCode));
        }

        private static string FirstCode(BossRewardPlanningResult planned, string fallback)
        {
            if (planned.Diagnostics != null && planned.Diagnostics.Count > 0)
            {
                string code = planned.Diagnostics[0].Code;
                if (!string.IsNullOrEmpty(code))
                    return code;
            }

            return fallback;
        }

        private static BossRewardCandidateApplicationResult Reject(
            BossRewardCandidateApplicationStatus status,
            string diagnosticCode)
        {
            return new BossRewardCandidateApplicationResult(
                status,
                diagnosticCode,
                null,
                null,
                Array.Empty<BossRewardNotificationIntent>(),
                null);
        }
    }
}
