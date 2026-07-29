using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.Kingdom.Progression
{
    public static class ProgressionOrderPlanner
    {
        public const int MaximumOperationReceipts = 256;
        public const int MaximumOrders = 256;
        public const int MaximumPrerequisiteLevels = 512;
        public const int MaximumEconomyBalances = 256;

        public static ProgressionReplayClassification ClassifyReplay(
            string operationId,
            string semanticHash,
            string expectedRevision,
            string currentRevision,
            IEnumerable<ProgressionOperationReceipt> receipts)
        {
            ProgressionReplayClassification replay = ClassifyReplay(
                operationId,
                ProgressionOperationKind.None,
                semanticHash,
                receipts);
            if (replay.Disposition !=
                ProgressionReplayDisposition.NoPriorOperation)
            {
                return replay;
            }

            if (!IsValidId(expectedRevision) ||
                !IsValidId(currentRevision))
            {
                return new ProgressionReplayClassification(
                    ProgressionReplayDisposition.MalformedLedger,
                    null);
            }

            return string.Equals(
                    expectedRevision,
                    currentRevision,
                    StringComparison.Ordinal)
                ? replay
                : new ProgressionReplayClassification(
                    ProgressionReplayDisposition.StaleExpectedRevision,
                    null);
        }

        public static ProgressionReplayClassification ClassifyReplay(
            string operationId,
            ProgressionOperationKind expectedKind,
            string semanticHash,
            IEnumerable<ProgressionOperationReceipt> receipts)
        {
            if (!IsValidId(operationId) ||
                !IsHash(semanticHash) ||
                (expectedKind != ProgressionOperationKind.None &&
                 !Enum.IsDefined(
                     typeof(ProgressionOperationKind),
                     expectedKind)) ||
                receipts == null)
            {
                return new ProgressionReplayClassification(
                    ProgressionReplayDisposition.MalformedLedger,
                    null);
            }

            List<ProgressionOperationReceipt> receiptList =
                CopyBounded(
                    receipts,
                    MaximumOperationReceipts,
                    out bool limitExceeded);
            if (limitExceeded ||
                receiptList.Any(receipt => !IsValidReceipt(receipt)) ||
                receiptList
                    .Where(receipt => receipt != null)
                    .GroupBy(receipt => receipt.OperationId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                return new ProgressionReplayClassification(
                    ProgressionReplayDisposition.MalformedLedger,
                    null);
            }

            ProgressionOperationReceipt existing = receiptList.SingleOrDefault(
                receipt => string.Equals(
                    receipt.OperationId,
                    operationId,
                    StringComparison.Ordinal));
            if (existing != null)
            {
                if (!string.Equals(
                        existing.SemanticHash,
                        semanticHash,
                        StringComparison.Ordinal))
                {
                    return new ProgressionReplayClassification(
                        ProgressionReplayDisposition.PayloadConflict,
                        existing);
                }

                if (expectedKind != ProgressionOperationKind.None &&
                    existing.Durability ==
                    ProgressionOperationDurability.Committed &&
                    existing.OperationKind != expectedKind)
                {
                    return new ProgressionReplayClassification(
                        ProgressionReplayDisposition.PayloadConflict,
                        existing);
                }

                if (existing.Durability ==
                    ProgressionOperationDurability.CommitUncertain)
                {
                    return new ProgressionReplayClassification(
                        ProgressionReplayDisposition.CommitUncertain,
                        existing);
                }

                if (!IsHash(existing.ResultHash))
                {
                    return new ProgressionReplayClassification(
                        ProgressionReplayDisposition.MalformedLedger,
                        existing);
                }

                return new ProgressionReplayClassification(
                    ProgressionReplayDisposition.ExactCommittedReplay,
                    existing);
            }

            return new ProgressionReplayClassification(
                ProgressionReplayDisposition.NoPriorOperation,
                null);
        }

        public static ProgressionOrderSnapshot CreateActiveOrder(
            ProgressionStartPlan plan,
            string completionOperationId,
            string cancellationOperationId = "")
        {
            if (plan == null ||
                !plan.CanCommit ||
                !IsValidReadyStartPlan(plan) ||
                !IsValidId(completionOperationId) ||
                string.Equals(
                    completionOperationId,
                    plan.OperationId,
                    StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(cancellationOperationId) &&
                 (!IsValidId(cancellationOperationId) ||
                  string.Equals(
                      cancellationOperationId,
                      plan.OperationId,
                      StringComparison.Ordinal) ||
                  string.Equals(
                      cancellationOperationId,
                      completionOperationId,
                      StringComparison.Ordinal))))
            {
                return null;
            }

            string orderHash = BuildOrderHash(
                plan.OrderType,
                plan.ProfileId,
                plan.DefinitionId,
                plan.DefinitionContentVersion,
                plan.DefinitionSource,
                plan.CostProfile,
                plan.DurationProfile,
                plan.OrderId,
                plan.OperationId,
                completionOperationId,
                cancellationOperationId,
                plan.PreviousValue,
                plan.TargetValue,
                plan.BatchCount,
                plan.Costs,
                plan.StartTimestamp,
                plan.EndTimestamp,
                plan.CatalogSetId,
                plan.CatalogRevision,
                plan.ProgressionRevision,
                plan.EconomyRevision,
                plan.RequestPolicyVersion,
                plan.MaximumValue,
                plan.InventoryCapacityPolicy,
                plan.TimestampPolicy,
                plan.PrerequisiteRevision);
            if (!IsHash(orderHash))
            {
                return null;
            }

            var order = new ProgressionOrderSnapshot(
                plan.OrderType,
                ProgressionOrderState.Active,
                plan.ProfileId,
                plan.DefinitionId,
                plan.DefinitionContentVersion,
                plan.DefinitionSource,
                plan.CostProfile,
                plan.DurationProfile,
                plan.OrderId,
                plan.OperationId,
                completionOperationId,
                cancellationOperationId,
                plan.PreviousValue,
                plan.TargetValue,
                plan.BatchCount,
                plan.Costs,
                plan.StartTimestamp,
                plan.EndTimestamp,
                plan.CatalogSetId,
                plan.ProgressionRevision,
                plan.EconomyRevision,
                plan.RequestPolicyVersion,
                orderHash,
                plan.MaximumValue,
                plan.InventoryCapacityPolicy,
                plan.TimestampPolicy,
                plan.PrerequisiteRevision,
                plan.CatalogRevision);
            return IsValidOrder(order) ? order : null;
        }

        public static ProgressionOperationReceipt CreateCommittedReceipt(
            ProgressionStartPlan plan)
        {
            return CreateCommittedReceipt(plan, null);
        }

        public static ProgressionOperationReceipt CreateCommittedReceipt(
            ProgressionStartPlan plan,
            ProgressionOrderSnapshot order)
        {
            if (!IsValidReadyStartPlan(plan) ||
                (order != null &&
                 (!IsValidOrder(order) ||
                  !IsOrderForStartPlan(order, plan))))
            {
                return null;
            }

            var result = new ProgressionCommittedOperationResult(
                ProgressionOperationKind.Start,
                plan.OrderType,
                plan.ProfileId,
                plan.DefinitionSource,
                plan.CostProfile,
                plan.DurationProfile,
                plan.OrderId,
                plan.OperationId,
                plan.OperationId,
                order?.CompletionOperationId ?? string.Empty,
                order?.CancellationOperationId ?? string.Empty,
                plan.PreviousValue,
                plan.TargetValue,
                plan.BatchCount,
                plan.MaximumValue,
                0,
                plan.InventoryCapacityPolicy,
                plan.Costs,
                plan.StartTimestamp,
                plan.EndTimestamp,
                plan.StartTimestamp,
                plan.TimestampPolicy,
                plan.CatalogSetId,
                plan.CatalogRevision,
                plan.ProgressionRevision,
                plan.EconomyRevision,
                plan.PrerequisiteRevision,
                string.Empty,
                plan.RequestPolicyVersion,
                plan.RequestPolicyVersion,
                order?.CatalogSetId ?? plan.CatalogSetId,
                order?.CatalogRevision ?? plan.CatalogRevision,
                order?.ProgressionRevision ?? plan.ProgressionRevision,
                order?.EconomyRevision ?? plan.EconomyRevision,
                order?.OrderHash ?? string.Empty,
                ProgressionOrderSourceDisposition.ExactCurrentSource,
                plan.SemanticHash,
                plan.PlanHash);
            if (!TryBuildCommittedResultHash(result, out string resultHash))
            {
                return null;
            }

            return new ProgressionOperationReceipt(
                plan.OperationId,
                plan.SemanticHash,
                resultHash,
                ProgressionOperationDurability.Committed,
                result);
        }

        public static ProgressionOperationReceipt CreateCommittedReceipt(
            ProgressionCompletionPlan plan)
        {
            ProgressionOrderSnapshot order = plan?.OrderSnapshot;
            if (!IsValidReadyCompletionPlan(plan))
            {
                return null;
            }

            var result = new ProgressionCommittedOperationResult(
                ProgressionOperationKind.Completion,
                order.OrderType,
                order.ProfileId,
                order.DefinitionSource,
                order.CostProfile,
                order.DurationProfile,
                order.OrderId,
                plan.OperationId,
                order.StartOperationId,
                order.CompletionOperationId,
                order.CancellationOperationId,
                plan.PreviousValue,
                plan.TargetValue,
                order.BatchCount,
                order.MaximumValue,
                plan.QuestProgressAmount,
                order.InventoryCapacityPolicy,
                order.CommittedCosts,
                order.StartTimestamp,
                order.EndTimestamp,
                plan.CommitTimestamp,
                order.TimestampPolicy,
                plan.CatalogSetId,
                plan.CatalogRevision,
                plan.ProgressionRevision,
                plan.EconomyRevision,
                order.PrerequisiteRevision,
                plan.QuestRevision,
                plan.CompletionPolicyVersion,
                order.RequestPolicyVersion,
                order.CatalogSetId,
                order.CatalogRevision,
                order.ProgressionRevision,
                order.EconomyRevision,
                order.OrderHash,
                plan.SourceDisposition,
                plan.SemanticHash,
                plan.PlanHash);
            if (!TryBuildCommittedResultHash(result, out string resultHash))
            {
                return null;
            }

            return new ProgressionOperationReceipt(
                plan.OperationId,
                plan.SemanticHash,
                resultHash,
                ProgressionOperationDurability.Committed,
                result);
        }

        public static ProgressionStartPlan PlanResearchStart(
            ProgressionCompatibilityResult compatibility,
            ProgressionStartRequest request,
            ProgressionEconomySnapshot economy,
            ProgressionPrerequisiteSnapshot prerequisites,
            IEnumerable<ProgressionOperationReceipt> receipts,
            IEnumerable<ProgressionOrderSnapshot> existingOrders,
            long observedUtcTimestamp)
        {
            if (!IsValidStartRequest(
                    request,
                    ProgressionOrderType.ResearchLevel))
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidRequest,
                    request,
                    string.Empty,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Research);
            }

            string semanticHash = BuildStartSemanticHash(request);
            if (!IsHash(semanticHash))
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidRequest,
                    request,
                    string.Empty,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Research);
            }

            ProgressionStartPlan replay = ReplayStart(
                request,
                semanticHash,
                receipts,
                ProgressionDomain.Research);
            if (replay != null)
            {
                return replay;
            }

            if (compatibility == null)
            {
                return RejectStart(
                    ProgressionPlanStatus.DefinitionUnavailable,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    ProgressionDomain.Research);
            }

            if (compatibility.Domain != ProgressionDomain.Research ||
                compatibility.Status != ProgressionCompatibilityStatus.Available)
            {
                return RejectStart(
                    compatibility.Status ==
                    ProgressionCompatibilityStatus.UnavailableCatalog
                        ? ProgressionPlanStatus.DefinitionUnavailable
                        : ProgressionPlanStatus.StateMalformed,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    ProgressionDomain.Research);
            }

            if (!string.Equals(
                    request.ExpectedCatalogSetId,
                    compatibility.CatalogSetId,
                    StringComparison.Ordinal))
            {
                return RejectStart(
                    ProgressionPlanStatus.DefinitionUnavailable,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InvalidCatalogIdentity,
                    ProgressionDomain.Research);
            }

            if (!IsValidAvailableCompatibility(
                    compatibility,
                    ProgressionDomain.Research))
            {
                return RejectStart(
                    ProgressionPlanStatus.StateMalformed,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    ProgressionDomain.Research);
            }

            if (!string.Equals(
                    compatibility.StateRevision,
                    request.ExpectedProgressionRevision,
                    StringComparison.Ordinal))
            {
                return RejectStart(
                    ProgressionPlanStatus.StaleProgressionRevision,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StaleProgressionRevision,
                    ProgressionDomain.Research);
            }

            ResearchProgressionSnapshot snapshot =
                compatibility.Research.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.Definition.Identity.Id,
                        request.DefinitionId,
                        StringComparison.Ordinal));
            if (snapshot == null)
            {
                return RejectStart(
                    ProgressionPlanStatus.UnknownDefinition,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.UnknownDefinition,
                    ProgressionDomain.Research);
            }

            ProgressionStartPlan orderConflict = ValidateExistingOrders(
                request,
                semanticHash,
                existingOrders,
                ProgressionDomain.Research);
            if (orderConflict != null)
            {
                return orderConflict;
            }

            if (snapshot.Level >= snapshot.Definition.MaximumLevel)
            {
                return RejectStart(
                    ProgressionPlanStatus.AtMaximum,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.OverMaximumLevel,
                    ProgressionDomain.Research);
            }

            int expectedTarget;
            try
            {
                expectedTarget = checked(snapshot.Level + 1);
            }
            catch (OverflowException)
            {
                return RejectStart(
                    ProgressionPlanStatus.ArithmeticOverflow,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ArithmeticOverflow,
                    ProgressionDomain.Research);
            }

            if (request.RequestedTargetLevel != expectedTarget ||
                request.RequestedBatchCount != 0)
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidTarget,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Research);
            }

            ProgressionStartPlan prerequisiteFailure = ValidatePrerequisites(
                request,
                semanticHash,
                snapshot.Definition.Prerequisites,
                prerequisites,
                ProgressionDomain.Research);
            if (prerequisiteFailure != null)
            {
                return prerequisiteFailure;
            }

            return BuildReadyStart(
                compatibility,
                request,
                economy,
                semanticHash,
                snapshot.Definition.Identity,
                snapshot.Definition.CostProfile,
                snapshot.Definition.DurationProfile,
                snapshot.Level,
                expectedTarget,
                0,
                expectedTarget,
                snapshot.Definition.MaximumLevel,
                TroopInventoryCapacityPolicy.Unresolved,
                prerequisites.Revision,
                observedUtcTimestamp,
                ProgressionDomain.Research);
        }

        public static ProgressionStartPlan PlanTrainingStart(
            ProgressionCompatibilityResult compatibility,
            ProgressionStartRequest request,
            ProgressionEconomySnapshot economy,
            ProgressionPrerequisiteSnapshot prerequisites,
            IEnumerable<ProgressionOperationReceipt> receipts,
            IEnumerable<ProgressionOrderSnapshot> existingOrders,
            long observedUtcTimestamp)
        {
            if (!IsValidStartRequest(
                    request,
                    ProgressionOrderType.TroopTrainingBatch))
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidRequest,
                    request,
                    string.Empty,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Training);
            }

            string semanticHash = BuildStartSemanticHash(request);
            if (!IsHash(semanticHash))
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidRequest,
                    request,
                    string.Empty,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Training);
            }

            ProgressionStartPlan replay = ReplayStart(
                request,
                semanticHash,
                receipts,
                ProgressionDomain.Training);
            if (replay != null)
            {
                return replay;
            }

            if (compatibility == null)
            {
                return RejectStart(
                    ProgressionPlanStatus.DefinitionUnavailable,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    ProgressionDomain.Training);
            }

            if (compatibility.Domain != ProgressionDomain.Training ||
                compatibility.Status != ProgressionCompatibilityStatus.Available)
            {
                return RejectStart(
                    compatibility.Status ==
                    ProgressionCompatibilityStatus.UnavailableCatalog
                        ? ProgressionPlanStatus.DefinitionUnavailable
                        : ProgressionPlanStatus.StateMalformed,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    ProgressionDomain.Training);
            }

            if (!string.Equals(
                    request.ExpectedCatalogSetId,
                    compatibility.CatalogSetId,
                    StringComparison.Ordinal))
            {
                return RejectStart(
                    ProgressionPlanStatus.DefinitionUnavailable,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InvalidCatalogIdentity,
                    ProgressionDomain.Training);
            }

            if (!IsValidAvailableCompatibility(
                    compatibility,
                    ProgressionDomain.Training))
            {
                return RejectStart(
                    ProgressionPlanStatus.StateMalformed,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    ProgressionDomain.Training);
            }

            if (!string.Equals(
                    compatibility.StateRevision,
                    request.ExpectedProgressionRevision,
                    StringComparison.Ordinal))
            {
                return RejectStart(
                    ProgressionPlanStatus.StaleProgressionRevision,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StaleProgressionRevision,
                    ProgressionDomain.Training);
            }

            TroopProgressionSnapshot snapshot =
                compatibility.Troops.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.Definition.Identity.Id,
                        request.DefinitionId,
                        StringComparison.Ordinal));
            if (snapshot == null)
            {
                return RejectStart(
                    ProgressionPlanStatus.UnknownDefinition,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.UnknownDefinition,
                    ProgressionDomain.Training);
            }

            ProgressionStartPlan orderConflict = ValidateExistingOrders(
                request,
                semanticHash,
                existingOrders,
                ProgressionDomain.Training);
            if (orderConflict != null)
            {
                return orderConflict;
            }

            if (request.RequestedTargetLevel != 0 ||
                request.RequestedBatchCount <= 0 ||
                request.RequestedBatchCount >
                snapshot.Definition.MaximumBatchCount)
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidTarget,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Training);
            }

            long targetActive;
            try
            {
                targetActive = checked(
                    snapshot.ActiveCount + request.RequestedBatchCount);
                long targetTotal = checked(
                    checked(targetActive + snapshot.WoundedCount) +
                    snapshot.ReservedCount);
                if (targetTotal > snapshot.Definition.MaximumInventoryCount)
                {
                    return RejectStart(
                        ProgressionPlanStatus.InventoryOverflow,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.OverMaximumCount,
                        ProgressionDomain.Training);
                }
            }
            catch (OverflowException)
            {
                return RejectStart(
                    ProgressionPlanStatus.InventoryOverflow,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.CountOverflow,
                    ProgressionDomain.Training);
            }

            ProgressionStartPlan prerequisiteFailure = ValidatePrerequisites(
                request,
                semanticHash,
                snapshot.Definition.Prerequisites,
                prerequisites,
                ProgressionDomain.Training);
            if (prerequisiteFailure != null)
            {
                return prerequisiteFailure;
            }

            return BuildReadyStart(
                compatibility,
                request,
                economy,
                semanticHash,
                snapshot.Definition.Identity,
                snapshot.Definition.CostProfile,
                snapshot.Definition.DurationProfile,
                snapshot.ActiveCount,
                targetActive,
                request.RequestedBatchCount,
                request.RequestedBatchCount,
                snapshot.Definition.MaximumInventoryCount,
                snapshot.Definition.InventoryCapacityPolicy,
                prerequisites.Revision,
                observedUtcTimestamp,
                ProgressionDomain.Training);
        }

        public static ProgressionCompletionPlan PlanCompletion(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order,
            ProgressionCompletionRequest request,
            IEnumerable<ProgressionOperationReceipt> receipts,
            ProgressionCompletionDependencySnapshot dependencies,
            long observedUtcTimestamp)
        {
            ProgressionDomain domain = order?.OrderType ==
                ProgressionOrderType.TroopTrainingBatch
                ? ProgressionDomain.Training
                : ProgressionDomain.Research;
            if (!IsValidCompletionRequest(request))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.InvalidRequest,
                    order,
                    request,
                    string.Empty,
                    ProgressionDiagnosticCode.InvalidRequest,
                    domain);
            }

            string semanticHash = BuildCompletionSemanticHash(request);
            if (!IsHash(semanticHash))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.InvalidRequest,
                    order,
                    request,
                    string.Empty,
                    ProgressionDiagnosticCode.InvalidRequest,
                    domain);
            }

            ProgressionReplayClassification replay = ClassifyReplay(
                request.OperationId,
                ProgressionOperationKind.Completion,
                semanticHash,
                receipts);
            ProgressionCompletionPlan replayPlan = ReplayCompletion(
                replay,
                request,
                semanticHash,
                domain);
            if (replayPlan != null)
            {
                return replayPlan;
            }

            if (order == null || !IsValidOrder(order))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.OrderMalformed,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.OrderMalformed,
                    domain);
            }

            domain = order.OrderType == ProgressionOrderType.TroopTrainingBatch
                ? ProgressionDomain.Training
                : ProgressionDomain.Research;
            if (compatibility == null)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.DefinitionUnavailable,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    domain);
            }

            if (dependencies == null ||
                !IsValidId(dependencies.EconomyRevision) ||
                !IsValidId(dependencies.QuestRevision) ||
                !string.Equals(
                    order.ProfileId,
                    request.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(order.OrderId, request.OrderId, StringComparison.Ordinal) ||
                !string.Equals(
                    order.CompletionOperationId,
                    request.OperationId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    order.StartOperationId,
                    request.OperationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    order.CatalogSetId,
                    request.ExpectedCatalogSetId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    compatibility.CatalogSetId,
                    request.ExpectedCatalogSetId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    dependencies.EconomyRevision,
                    request.ExpectedEconomyRevision,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    dependencies.QuestRevision,
                    request.ExpectedQuestRevision,
                    StringComparison.Ordinal))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.OrderMalformed,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.OrderMalformed,
                    domain);
            }

            if (!string.Equals(
                    compatibility.StateRevision,
                    request.ExpectedProgressionRevision,
                    StringComparison.Ordinal))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.StaleProgressionRevision,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StaleProgressionRevision,
                    domain);
            }

            if (order.State == ProgressionOrderState.RecoveryRequired)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.RecoveryRequired,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.RecoveryRequired,
                    domain);
            }

            if (order.State == ProgressionOrderState.Completed)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.RecoveryRequired,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.RecoveryRequired,
                    domain);
            }

            ProgressionPlanStatus clockStatus =
                EvaluateOrderClock(order, observedUtcTimestamp);
            if (clockStatus == ProgressionPlanStatus.ClockInvalid)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.ClockInvalid,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ClockInvalid,
                    domain);
            }

            if (clockStatus == ProgressionPlanStatus.NotYetEligible)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.NotYetEligible,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.NotYetEligible,
                    domain);
            }

            if (!TryResolveCompletionStateAndSource(
                    compatibility,
                    order,
                    out long currentValue,
                    out long questProgressAmount,
                    out ProgressionOrderSourceDisposition sourceDisposition,
                    out ProgressionPlanStatus resolutionStatus,
                    out ProgressionDiagnosticCode resolutionDiagnostic))
            {
                return RejectCompletion(
                    resolutionStatus,
                    order,
                    request,
                    semanticHash,
                    resolutionDiagnostic,
                    domain,
                    sourceDisposition);
            }

            string planHash = ProgressionContractHash.Compute(
                "completion",
                semanticHash,
                order.OrderHash,
                compatibility.CatalogSetId,
                compatibility.CatalogRevision,
                compatibility.StateRevision,
                Invariant(currentValue),
                Invariant(order.TargetValue),
                Invariant(questProgressAmount),
                dependencies.EconomyRevision,
                dependencies.QuestRevision,
                request.CompletionPolicyVersion,
                Invariant((long)sourceDisposition),
                Invariant(observedUtcTimestamp));
            if (!IsHash(planHash))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.RecoveryRequired,
                    order,
                    request,
                    string.Empty,
                    ProgressionDiagnosticCode.RecoveryRequired,
                    domain);
            }

            return new ProgressionCompletionPlan(
                ProgressionPlanStatus.Ready,
                order.OrderType,
                order.DefinitionId,
                order.OrderId,
                request.OperationId,
                currentValue,
                order.TargetValue,
                questProgressAmount,
                compatibility.CatalogSetId,
                compatibility.StateRevision,
                dependencies.EconomyRevision,
                dependencies.QuestRevision,
                request.CompletionPolicyVersion,
                semanticHash,
                planHash,
                Array.Empty<ProgressionDiagnostic>(),
                order,
                sourceDisposition,
                null,
                observedUtcTimestamp,
                compatibility.CatalogRevision);
        }

        public static ProgressionReconciliationPlan PlanReconciliation(
            IEnumerable<ProgressionOrderSnapshot> orders,
            long observedUtcTimestamp)
        {
            var diagnostics = new List<ProgressionDiagnostic>();
            if (orders == null || observedUtcTimestamp <= 0)
            {
                diagnostics.Add(new ProgressionDiagnostic(
                    orders == null
                        ? ProgressionDiagnosticCode.OrderMalformed
                        : ProgressionDiagnosticCode.ClockInvalid,
                    ProgressionDomain.Research,
                    string.Empty,
                    -1));
                return new ProgressionReconciliationPlan(
                    orders == null
                        ? ProgressionPlanStatus.RecoveryRequired
                        : ProgressionPlanStatus.ClockInvalid,
                    Array.Empty<ProgressionOrderSnapshot>(),
                    string.Empty,
                    diagnostics);
            }

            List<ProgressionOrderSnapshot> orderList =
                CopyBounded(orders, MaximumOrders, out bool limitExceeded);
            if (limitExceeded)
            {
                diagnostics.Add(new ProgressionDiagnostic(
                    ProgressionDiagnosticCode.InputLimitExceeded,
                    ProgressionDomain.Research,
                    string.Empty,
                    MaximumOrders));
            }

            for (int index = 0; index < orderList.Count; index++)
            {
                ProgressionOrderSnapshot order = orderList[index];
                ProgressionDomain domain = order?.OrderType ==
                    ProgressionOrderType.TroopTrainingBatch
                    ? ProgressionDomain.Training
                    : ProgressionDomain.Research;
                if (!IsValidOrder(order) ||
                    order.State == ProgressionOrderState.RecoveryRequired)
                {
                    diagnostics.Add(new ProgressionDiagnostic(
                        order?.State == ProgressionOrderState.RecoveryRequired
                            ? ProgressionDiagnosticCode.RecoveryRequired
                            : ProgressionDiagnosticCode.OrderMalformed,
                        domain,
                        order?.DefinitionId,
                        index));
                }
            }

            foreach (IGrouping<string, ProgressionOrderSnapshot> group in orderList
                         .Where(order => order != null && IsValidId(order.OrderId))
                         .GroupBy(order => order.OrderId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(new ProgressionDiagnostic(
                    ProgressionDiagnosticCode.OrderMalformed,
                    ProgressionDomain.Research,
                    group.Key,
                    -1));
            }

            foreach (string duplicateOperationId in orderList
                         .Where(order => order != null)
                         .SelectMany(order => new[]
                          {
                              order.StartOperationId,
                              order.CompletionOperationId,
                              order.CancellationOperationId
                          })
                         .Where(IsValidId)
                         .GroupBy(operationId => operationId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .Select(group => group.Key))
            {
                diagnostics.Add(new ProgressionDiagnostic(
                    ProgressionDiagnosticCode.OrderMalformed,
                    ProgressionDomain.Research,
                    duplicateOperationId,
                    -1));
            }

            if (diagnostics.Count > 0)
            {
                return new ProgressionReconciliationPlan(
                    ProgressionPlanStatus.RecoveryRequired,
                    Array.Empty<ProgressionOrderSnapshot>(),
                    string.Empty,
                    SortDiagnostics(diagnostics));
            }

            int clockInvalidIndex = orderList.FindIndex(order =>
                order.State == ProgressionOrderState.Active &&
                EvaluateOrderClock(order, observedUtcTimestamp) ==
                ProgressionPlanStatus.ClockInvalid);
            if (clockInvalidIndex >= 0)
            {
                ProgressionOrderSnapshot invalidClockOrder =
                    orderList[clockInvalidIndex];
                return new ProgressionReconciliationPlan(
                    ProgressionPlanStatus.ClockInvalid,
                    Array.Empty<ProgressionOrderSnapshot>(),
                    string.Empty,
                    new[]
                    {
                        new ProgressionDiagnostic(
                            ProgressionDiagnosticCode.ClockInvalid,
                            invalidClockOrder.OrderType ==
                            ProgressionOrderType.TroopTrainingBatch
                                ? ProgressionDomain.Training
                                : ProgressionDomain.Research,
                            invalidClockOrder.DefinitionId,
                            clockInvalidIndex)
                    });
            }

            List<ProgressionOrderSnapshot> eligible = orderList
                .Where(order =>
                    order.State == ProgressionOrderState.Active &&
                    EvaluateOrderClock(order, observedUtcTimestamp) ==
                    ProgressionPlanStatus.Ready)
                .OrderBy(order => order.EndTimestamp)
                .ThenBy(order => order.OrderType)
                .ThenBy(order => order.DefinitionId, StringComparer.Ordinal)
                .ThenBy(order => order.OrderId, StringComparer.Ordinal)
                .ToList();
            if (eligible.Count == 0)
            {
                return new ProgressionReconciliationPlan(
                    ProgressionPlanStatus.NoChange,
                    eligible,
                    ProgressionContractHash.Compute(
                        "reconciliation",
                        Invariant(observedUtcTimestamp),
                        "none"),
                    Array.Empty<ProgressionDiagnostic>());
            }

            var hashSegments = new List<string>
            {
                "reconciliation",
                Invariant(observedUtcTimestamp)
            };
            foreach (ProgressionOrderSnapshot order in eligible)
            {
                hashSegments.Add("eligible-order");
                hashSegments.Add(Invariant(order.EndTimestamp));
                hashSegments.Add(Invariant((long)order.OrderType));
                hashSegments.Add(order.DefinitionId);
                hashSegments.Add(order.OrderId);
                hashSegments.Add(order.OrderHash);
            }
            return new ProgressionReconciliationPlan(
                ProgressionPlanStatus.Ready,
                eligible,
                ProgressionContractHash.Compute(hashSegments.ToArray()),
                Array.Empty<ProgressionDiagnostic>());
        }

        public static ResearchEffectSnapshot BuildResearchEffectSnapshot(
            ProgressionCompatibilityResult compatibility)
        {
            if (compatibility == null ||
                compatibility.Domain != ProgressionDomain.Research ||
                compatibility.Status != ProgressionCompatibilityStatus.Available ||
                !IsValidAvailableCompatibility(
                    compatibility,
                    ProgressionDomain.Research))
            {
                return new ResearchEffectSnapshot(
                    ProgressionPlanStatus.StateMalformed,
                    compatibility?.CatalogSetId,
                    compatibility?.StateRevision,
                    string.Empty,
                    Array.Empty<ResearchEffectReference>(),
                    new[]
                    {
                        new ProgressionDiagnostic(
                            ProgressionDiagnosticCode.StateUnavailable,
                            ProgressionDomain.Research,
                            string.Empty,
                            -1)
                    });
            }

            List<ResearchEffectReference> effects = compatibility.Research
                .Where(snapshot => snapshot.Level > 0)
                .OrderBy(
                    snapshot => snapshot.Definition.Identity.Id,
                    StringComparer.Ordinal)
                .SelectMany(snapshot => snapshot.Definition.EffectProfiles
                    .OrderBy(identity => identity.Id, StringComparer.Ordinal)
                    .Select(identity => new ResearchEffectReference(
                        snapshot.Definition.Identity,
                        snapshot.Level,
                        identity)))
                .ToList();
            var hashSegments = new List<string>
            {
                "research-effects",
                compatibility.CatalogSetId,
                compatibility.StateRevision
            };
            foreach (ResearchEffectReference effect in effects)
            {
                hashSegments.Add("effect");
                AddIdentityHashSegments(
                    hashSegments,
                    "research-definition",
                    effect.ResearchDefinition);
                hashSegments.Add(Invariant(effect.Level));
                AddIdentityHashSegments(
                    hashSegments,
                    "effect-profile",
                    effect.EffectProfile);
            }
            return new ResearchEffectSnapshot(
                ProgressionPlanStatus.Ready,
                compatibility.CatalogSetId,
                compatibility.StateRevision,
                ProgressionContractHash.Compute(hashSegments.ToArray()),
                effects,
                Array.Empty<ProgressionDiagnostic>());
        }

        private static ProgressionStartPlan ReplayStart(
            ProgressionStartRequest request,
            string semanticHash,
            IEnumerable<ProgressionOperationReceipt> receipts,
            ProgressionDomain domain)
        {
            ProgressionReplayClassification replay = ClassifyReplay(
                request.OperationId,
                ProgressionOperationKind.Start,
                semanticHash,
                receipts);
            switch (replay.Disposition)
            {
                case ProgressionReplayDisposition.NoPriorOperation:
                    return null;
                case ProgressionReplayDisposition.ExactCommittedReplay:
                    ProgressionCommittedOperationResult committed =
                        replay.Receipt.CommittedResult;
                    return new ProgressionStartPlan(
                        ProgressionPlanStatus.AlreadyCommitted,
                        committed.OrderType,
                        committed.ProfileId,
                        committed.DefinitionId,
                        committed.DefinitionSource.ContentVersion,
                        committed.DefinitionSource,
                        committed.CostProfile,
                        committed.DurationProfile,
                        committed.OrderId,
                        committed.OperationId,
                        committed.PreviousValue,
                        committed.TargetValue,
                        committed.BatchCount,
                        committed.Costs,
                        committed.StartTimestamp,
                        committed.EndTimestamp,
                        committed.CatalogSetId,
                        committed.ProgressionRevision,
                        committed.EconomyRevision,
                        committed.OperationPolicyVersion,
                        committed.SemanticHash,
                        committed.PlanHash,
                        Array.Empty<ProgressionDiagnostic>(),
                        committed.MaximumValue,
                        committed.InventoryCapacityPolicy,
                        committed.TimestampPolicy,
                        committed.PrerequisiteRevision,
                        replay.Receipt,
                        committed.CatalogRevision);
                case ProgressionReplayDisposition.PayloadConflict:
                    return RejectStart(
                        ProgressionPlanStatus.CorrelationConflict,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.CorrelationConflict,
                        domain);
                case ProgressionReplayDisposition.StaleExpectedRevision:
                    return RejectStart(
                        ProgressionPlanStatus.StaleProgressionRevision,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.StaleProgressionRevision,
                        domain);
                case ProgressionReplayDisposition.CommitUncertain:
                    return RejectStart(
                        ProgressionPlanStatus.CommitUncertain,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.CommitUncertain,
                        domain);
                default:
                    return RejectStart(
                        ProgressionPlanStatus.RecoveryRequired,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.RecoveryRequired,
                        domain);
            }
        }

        private static ProgressionCompletionPlan ReplayCompletion(
            ProgressionReplayClassification replay,
            ProgressionCompletionRequest request,
            string semanticHash,
            ProgressionDomain domain)
        {
            switch (replay.Disposition)
            {
                case ProgressionReplayDisposition.NoPriorOperation:
                    return null;
                case ProgressionReplayDisposition.ExactCommittedReplay:
                    ProgressionCommittedOperationResult committed =
                        replay.Receipt.CommittedResult;
                    ProgressionOrderSnapshot committedOrder =
                        BuildCommittedOrderSnapshot(committed);
                    if (committedOrder == null)
                    {
                        return RejectCompletion(
                            ProgressionPlanStatus.RecoveryRequired,
                            null,
                            request,
                            semanticHash,
                            ProgressionDiagnosticCode.InvalidCommittedReceipt,
                            domain);
                    }

                    return new ProgressionCompletionPlan(
                        ProgressionPlanStatus.AlreadyCommitted,
                        committed.OrderType,
                        committed.DefinitionId,
                        committed.OrderId,
                        committed.OperationId,
                        committed.PreviousValue,
                        committed.TargetValue,
                        committed.QuestProgressAmount,
                        committed.CatalogSetId,
                        committed.ProgressionRevision,
                        committed.EconomyRevision,
                        committed.QuestRevision,
                        committed.OperationPolicyVersion,
                        committed.SemanticHash,
                        committed.PlanHash,
                        Array.Empty<ProgressionDiagnostic>(),
                        committedOrder,
                        committed.SourceDisposition,
                        replay.Receipt,
                        committed.CommitTimestamp,
                        committed.CatalogRevision);
                case ProgressionReplayDisposition.PayloadConflict:
                    return RejectCompletion(
                        ProgressionPlanStatus.CorrelationConflict,
                        null,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.CorrelationConflict,
                        domain);
                case ProgressionReplayDisposition.StaleExpectedRevision:
                    return RejectCompletion(
                        ProgressionPlanStatus.StaleProgressionRevision,
                        null,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.StaleProgressionRevision,
                        domain);
                case ProgressionReplayDisposition.CommitUncertain:
                    return RejectCompletion(
                        ProgressionPlanStatus.CommitUncertain,
                        null,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.CommitUncertain,
                        domain);
                default:
                    return RejectCompletion(
                        ProgressionPlanStatus.RecoveryRequired,
                        null,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.RecoveryRequired,
                        domain);
            }
        }

        private static ProgressionStartPlan ValidateExistingOrders(
            ProgressionStartRequest request,
            string semanticHash,
            IEnumerable<ProgressionOrderSnapshot> existingOrders,
            ProgressionDomain domain)
        {
            if (existingOrders == null)
            {
                return RejectStart(
                    ProgressionPlanStatus.RecoveryRequired,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.OrderMalformed,
                    domain);
            }

            List<ProgressionOrderSnapshot> orderList =
                CopyBounded(existingOrders, MaximumOrders, out bool limitExceeded);
            if (limitExceeded ||
                orderList.Any(order => !IsValidOrder(order)) ||
                orderList
                    .Where(order => order != null)
                    .GroupBy(order => order.OrderId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1) ||
                orderList
                    .Where(order => order != null)
                    .SelectMany(EnumerateOperationIds)
                    .GroupBy(
                        operationId => operationId,
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                return RejectStart(
                    ProgressionPlanStatus.RecoveryRequired,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.OrderMalformed,
                    domain);
            }

            if (orderList.Any(order =>
                    order.State == ProgressionOrderState.RecoveryRequired))
            {
                return RejectStart(
                    ProgressionPlanStatus.RecoveryRequired,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.RecoveryRequired,
                    domain);
            }

            if (orderList.Any(order =>
                    string.Equals(
                        order.OrderId,
                        request.OrderId,
                        StringComparison.Ordinal) ||
                    EnumerateOperationIds(order).Any(operationId =>
                        string.Equals(
                            operationId,
                            request.OperationId,
                            StringComparison.Ordinal))))
            {
                return RejectStart(
                    ProgressionPlanStatus.CorrelationConflict,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.CorrelationConflict,
                    domain);
            }

            if (orderList.Any(order =>
                    order.State == ProgressionOrderState.Active &&
                    order.OrderType == request.OrderType &&
                    string.Equals(
                        order.DefinitionId,
                        request.DefinitionId,
                        StringComparison.Ordinal)))
            {
                return RejectStart(
                    ProgressionPlanStatus.OrderAlreadyActive,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.OrderMalformed,
                    domain);
            }

            return null;
        }

        private static ProgressionStartPlan ValidatePrerequisites(
            ProgressionStartRequest request,
            string semanticHash,
            IReadOnlyList<ProgressionPrerequisite> required,
            ProgressionPrerequisiteSnapshot available,
            ProgressionDomain domain)
        {
            if (required == null ||
                available == null ||
                !available.HasLevelSource ||
                !IsValidId(available.Revision) ||
                available.Levels.Count > MaximumPrerequisiteLevels ||
                available.Levels.Any(level =>
                    level == null ||
                    !IsValidId(level.DefinitionId) ||
                    level.Level < 0) ||
                available.Levels
                    .Where(level => level != null)
                    .GroupBy(level => level.DefinitionId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                return RejectStart(
                    ProgressionPlanStatus.StateMalformed,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    domain);
            }

            if (!string.Equals(
                    request.ExpectedPrerequisiteRevision,
                    available.Revision,
                    StringComparison.Ordinal))
            {
                return RejectStart(
                    ProgressionPlanStatus.StalePrerequisiteRevision,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StalePrerequisiteRevision,
                    domain);
            }

            if (required.Count == 0)
            {
                return null;
            }

            var levelsById = available.Levels.ToDictionary(
                level => level.DefinitionId,
                level => level.Level,
                StringComparer.Ordinal);
            if (required.Any(prerequisite =>
                    !levelsById.TryGetValue(
                        prerequisite.DefinitionId,
                        out int level) ||
                    level < prerequisite.MinimumLevel))
            {
                return RejectStart(
                    ProgressionPlanStatus.PrerequisiteUnmet,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.PrerequisiteUnmet,
                    domain);
            }

            return null;
        }

        private static ProgressionStartPlan BuildReadyStart(
            ProgressionCompatibilityResult compatibility,
            ProgressionStartRequest request,
            ProgressionEconomySnapshot economy,
            string semanticHash,
            ProgressionSourceIdentity definitionSource,
            ProgressionCostProfile costProfile,
            ProgressionDurationProfile durationProfile,
            long previousValue,
            long targetValue,
            long batchCount,
            long profileInput,
            long maximumValue,
            TroopInventoryCapacityPolicy inventoryCapacityPolicy,
            string prerequisiteRevision,
            long observedUtcTimestamp,
            ProgressionDomain domain)
        {
            if (!IsValidTimestampPolicy(compatibility.TimestampPolicy) ||
                !IsTimestampWithinAbsolutePolicy(
                    observedUtcTimestamp,
                    compatibility.TimestampPolicy))
            {
                return RejectStart(
                    ProgressionPlanStatus.ClockInvalid,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ClockInvalid,
                    domain);
            }

            if (economy == null ||
                !economy.HasBalanceSource ||
                !IsValidId(economy.Revision) ||
                economy.Balances.Count > MaximumEconomyBalances ||
                economy.Balances.Any(balance =>
                    balance == null ||
                    !Enum.IsDefined(typeof(ResourceType), balance.ResourceType) ||
                    balance.Amount < 0) ||
                economy.Balances
                    .Where(balance => balance != null)
                    .GroupBy(balance => balance.ResourceType)
                    .Any(group => group.Count() > 1))
            {
                return RejectStart(
                    ProgressionPlanStatus.EconomyInvalid,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.EconomyMalformed,
                    domain);
            }

            if (!string.Equals(
                    request.ExpectedEconomyRevision,
                    economy.Revision,
                    StringComparison.Ordinal))
            {
                return RejectStart(
                    ProgressionPlanStatus.StaleEconomyRevision,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StaleEconomyRevision,
                    domain);
            }

            var costs = new List<BuildingConstructionCost>(
                costProfile.UnitCosts.Count);
            long durationSeconds;
            long endTimestamp;
            try
            {
                foreach (BuildingConstructionCost unitCost in
                         costProfile.UnitCosts)
                {
                    long amount = checked(unitCost.Amount * profileInput);
                    costs.Add(new BuildingConstructionCost(
                        unitCost.ResourceType,
                        amount));
                }

                durationSeconds = checked(
                    durationProfile.UnitSeconds * profileInput);
                endTimestamp = checked(observedUtcTimestamp + durationSeconds);
            }
            catch (OverflowException)
            {
                return RejectStart(
                    ProgressionPlanStatus.ArithmeticOverflow,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ArithmeticOverflow,
                    domain);
            }

            if (costs.Any(cost =>
                    cost.Amount <= 0 ||
                    cost.Amount > costProfile.MaximumAmountPerResource))
            {
                return RejectStart(
                    ProgressionPlanStatus.CostInvalid,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ArithmeticOverflow,
                    domain);
            }

            if (durationSeconds < 0 ||
                durationSeconds > durationProfile.MaximumSeconds ||
                (durationSeconds == 0 && !durationProfile.AllowsZeroDuration))
            {
                return RejectStart(
                    ProgressionPlanStatus.ArithmeticOverflow,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ArithmeticOverflow,
                    domain);
            }

            if (!IsTimestampWithinAbsolutePolicy(
                    endTimestamp,
                    compatibility.TimestampPolicy) ||
                durationSeconds >
                compatibility.TimestampPolicy.MaximumFutureLeadSeconds)
            {
                return RejectStart(
                    ProgressionPlanStatus.ClockInvalid,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ClockInvalid,
                    domain);
            }

            if (costs.Any(cost =>
            {
                ProgressionResourceBalance balance =
                    economy.Balances.SingleOrDefault(candidate =>
                        candidate.ResourceType == cost.ResourceType);
                return balance == null || balance.Amount < cost.Amount;
            }))
            {
                return RejectStart(
                    ProgressionPlanStatus.InsufficientResources,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InsufficientResources,
                    domain);
            }

            List<BuildingConstructionCost> orderedCosts = costs
                .OrderBy(cost => cost.ResourceType)
                .ToList();
            var hashSegments = new List<string> { "start", semanticHash };
            AddIdentityHashSegments(
                hashSegments,
                "definition-source",
                definitionSource);
            AddIdentityHashSegments(
                hashSegments,
                "cost-profile",
                costProfile.Identity);
            AddIdentityHashSegments(
                hashSegments,
                "duration-profile",
                durationProfile.Identity);
            hashSegments.Add("values");
            hashSegments.Add(Invariant(previousValue));
            hashSegments.Add(Invariant(targetValue));
            hashSegments.Add(Invariant(batchCount));
            hashSegments.Add(Invariant(maximumValue));
            hashSegments.Add(Invariant((long)inventoryCapacityPolicy));
            hashSegments.Add(Invariant(observedUtcTimestamp));
            hashSegments.Add(Invariant(endTimestamp));
            hashSegments.Add(compatibility.CatalogSetId);
            hashSegments.Add(compatibility.CatalogRevision);
            hashSegments.Add(compatibility.StateRevision);
            hashSegments.Add(economy.Revision);
            hashSegments.Add(prerequisiteRevision);
            AddTimestampPolicyHashSegments(
                hashSegments,
                compatibility.TimestampPolicy);
            foreach (BuildingConstructionCost cost in orderedCosts)
            {
                hashSegments.Add("cost");
                hashSegments.Add(Invariant((long)cost.ResourceType));
                hashSegments.Add(Invariant(cost.Amount));
            }
            string planHash = ProgressionContractHash.Compute(
                hashSegments.ToArray());
            if (!IsHash(planHash))
            {
                return RejectStart(
                    ProgressionPlanStatus.RecoveryRequired,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.RecoveryRequired,
                    domain);
            }

            return new ProgressionStartPlan(
                ProgressionPlanStatus.Ready,
                request.OrderType,
                request.ProfileId,
                request.DefinitionId,
                definitionSource.ContentVersion,
                definitionSource,
                costProfile.Identity,
                durationProfile.Identity,
                request.OrderId,
                request.OperationId,
                previousValue,
                targetValue,
                batchCount,
                orderedCosts,
                observedUtcTimestamp,
                endTimestamp,
                compatibility.CatalogSetId,
                compatibility.StateRevision,
                economy.Revision,
                request.RequestPolicyVersion,
                semanticHash,
                planHash,
                Array.Empty<ProgressionDiagnostic>(),
                maximumValue,
                inventoryCapacityPolicy,
                compatibility.TimestampPolicy,
                prerequisiteRevision,
                null,
                compatibility.CatalogRevision);
        }

        private static bool IsValidStartRequest(
            ProgressionStartRequest request,
            ProgressionOrderType expectedType)
        {
            return request != null &&
                   request.OrderType == expectedType &&
                   IsValidId(request.ProfileId) &&
                   IsValidId(request.DefinitionId) &&
                   IsValidId(request.OrderId) &&
                   IsValidId(request.OperationId) &&
                   IsValidId(request.ExpectedCatalogSetId) &&
                   IsValidId(request.ExpectedProgressionRevision) &&
                   IsValidId(request.ExpectedEconomyRevision) &&
                   IsValidId(request.ExpectedPrerequisiteRevision) &&
                   IsValidId(request.RequestPolicyVersion) &&
                   !string.Equals(
                       request.OrderId,
                       request.OperationId,
                       StringComparison.Ordinal);
        }

        private static bool IsValidAvailableCompatibility(
            ProgressionCompatibilityResult compatibility,
            ProgressionDomain domain)
        {
            if (compatibility == null ||
                compatibility.Status != ProgressionCompatibilityStatus.Available ||
                compatibility.Domain != domain ||
                !IsValidId(compatibility.CatalogSetId) ||
                !IsValidId(compatibility.CatalogRevision) ||
                !IsHash(compatibility.StateRevision) ||
                !compatibility.HasDefinitionSource ||
                !IsValidTimestampPolicy(compatibility.TimestampPolicy) ||
                compatibility.Diagnostics.Count != 0)
            {
                return false;
            }

            if (domain == ProgressionDomain.Research)
            {
                if (compatibility.Research.Count == 0 ||
                    compatibility.Research.Count >
                    ProgressionCompatibilityPlanner.MaximumDefinitions ||
                    compatibility.Troops.Count != 0 ||
                    compatibility.PreservedTroopStates.Count != 0 ||
                    compatibility.PreservedResearchStates.Count >
                    ProgressionCompatibilityPlanner.MaximumStateRows ||
                    compatibility.Research.Any(snapshot =>
                        !IsValidResearchSnapshot(snapshot)) ||
                    compatibility.Research
                        .GroupBy(
                            snapshot => snapshot.Definition.Identity.Id,
                            StringComparer.Ordinal)
                        .Any(group => group.Count() > 1) ||
                    !HasConsistentResearchRawState(compatibility))
                {
                    return false;
                }
            }
            else
            {
                if (compatibility.Troops.Count == 0 ||
                    compatibility.Troops.Count >
                    ProgressionCompatibilityPlanner.MaximumDefinitions ||
                    compatibility.Research.Count != 0 ||
                    compatibility.PreservedResearchStates.Count != 0 ||
                    compatibility.PreservedTroopStates.Count >
                    ProgressionCompatibilityPlanner.MaximumStateRows ||
                    compatibility.Troops.Any(snapshot =>
                        !IsValidTroopSnapshot(snapshot)) ||
                    compatibility.Troops
                        .GroupBy(
                            snapshot => snapshot.Definition.Identity.Id,
                            StringComparer.Ordinal)
                        .Any(group => group.Count() > 1) ||
                    !HasConsistentTroopRawState(compatibility))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasConsistentResearchRawState(
            ProgressionCompatibilityResult compatibility)
        {
            if (compatibility.PreservedResearchStates.Any(state =>
                    state == null ||
                    !IsValidId(state.DefinitionId) ||
                    !IsValidId(state.DefinitionContentVersion) ||
                    state.Level < 0 ||
                    state.HasActiveLegacyOrder ||
                    state.CompletionTimestamp != 0) ||
                compatibility.PreservedResearchStates
                    .GroupBy(state => state.DefinitionId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                return false;
            }

            var statesById = compatibility.PreservedResearchStates.ToDictionary(
                state => state.DefinitionId,
                StringComparer.Ordinal);
            foreach (ResearchProgressionSnapshot snapshot in
                     compatibility.Research)
            {
                bool hasRawState = statesById.TryGetValue(
                    snapshot.Definition.Identity.Id,
                    out ResearchProgressionStateRecord state);
                if (snapshot.Origin ==
                    ProgressionStateOrigin.EffectiveInitialUnpersisted)
                {
                    if (hasRawState)
                    {
                        return false;
                    }

                    continue;
                }

                if (!hasRawState ||
                    !string.Equals(
                        state.DefinitionContentVersion,
                        snapshot.Definition.Identity.ContentVersion,
                        StringComparison.Ordinal) ||
                    state.Level != snapshot.Level)
                {
                    return false;
                }
            }

            return statesById.Count ==
                   compatibility.Research.Count(snapshot =>
                       snapshot.Origin == ProgressionStateOrigin.Saved);
        }

        private static bool HasConsistentTroopRawState(
            ProgressionCompatibilityResult compatibility)
        {
            if (compatibility.PreservedTroopStates.Any(state =>
                    state == null ||
                    !IsValidId(state.DefinitionId) ||
                    !IsValidId(state.DefinitionContentVersion) ||
                    state.ActiveCount < 0 ||
                    state.WoundedCount < 0 ||
                    state.ReservedCount < 0) ||
                compatibility.PreservedTroopStates
                    .GroupBy(state => state.DefinitionId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                return false;
            }

            var statesById = compatibility.PreservedTroopStates.ToDictionary(
                state => state.DefinitionId,
                StringComparer.Ordinal);
            foreach (TroopProgressionSnapshot snapshot in compatibility.Troops)
            {
                bool hasRawState = statesById.TryGetValue(
                    snapshot.Definition.Identity.Id,
                    out TroopProgressionStateRecord state);
                if (snapshot.Origin ==
                    ProgressionStateOrigin.EffectiveInitialUnpersisted)
                {
                    if (hasRawState)
                    {
                        return false;
                    }

                    continue;
                }

                if (!hasRawState ||
                    !string.Equals(
                        state.DefinitionContentVersion,
                        snapshot.Definition.Identity.ContentVersion,
                        StringComparison.Ordinal) ||
                    state.ActiveCount != snapshot.ActiveCount ||
                    state.WoundedCount != snapshot.WoundedCount ||
                    state.ReservedCount != snapshot.ReservedCount)
                {
                    return false;
                }
            }

            return statesById.Count ==
                   compatibility.Troops.Count(snapshot =>
                       snapshot.Origin == ProgressionStateOrigin.Saved);
        }

        private static bool HasConsistentResearchCompletionRows(
            ProgressionCompatibilityResult compatibility,
            string targetDefinitionId)
        {
            var definitionsById =
                compatibility.ResearchDefinitions.ToDictionary(
                    definition => definition.Identity.Id,
                    StringComparer.Ordinal);
            var statesById =
                compatibility.PreservedResearchStates.ToDictionary(
                    state => state.DefinitionId,
                    StringComparer.Ordinal);
            var snapshotsById = compatibility.Research.ToDictionary(
                snapshot => snapshot.Definition.Identity.Id,
                StringComparer.Ordinal);

            foreach (ResearchProgressionSnapshot snapshot in
                     compatibility.Research)
            {
                string definitionId = snapshot.Definition.Identity.Id;
                if (!definitionsById.TryGetValue(
                        definitionId,
                        out ResearchProgressionDefinition definition) ||
                    !ReferenceEquals(snapshot.Definition, definition))
                {
                    return false;
                }

                bool hasState = statesById.TryGetValue(
                    definitionId,
                    out ResearchProgressionStateRecord state);
                if (snapshot.Origin ==
                    ProgressionStateOrigin.EffectiveInitialUnpersisted)
                {
                    if (hasState ||
                        snapshot.Level != definition.InitialLevel)
                    {
                        return false;
                    }

                    continue;
                }

                if (!hasState ||
                    !string.Equals(
                        state.DefinitionContentVersion,
                        snapshot.Definition.Identity.ContentVersion,
                        StringComparison.Ordinal) ||
                    state.Level != snapshot.Level ||
                    state.HasActiveLegacyOrder !=
                    snapshot.HasActiveLegacyOrder ||
                    state.CompletionTimestamp !=
                    snapshot.CompletionTimestamp)
                {
                    return false;
                }
            }

            bool snapshotsRequired = compatibility.Status ==
                                     ProgressionCompatibilityStatus.Available;
            foreach (ResearchProgressionDefinition definition in
                     compatibility.ResearchDefinitions)
            {
                string definitionId = definition.Identity.Id;
                if (snapshotsRequired &&
                    !snapshotsById.ContainsKey(definitionId))
                {
                    return false;
                }

                if (string.Equals(
                        definitionId,
                        targetDefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool hasState = statesById.TryGetValue(
                    definitionId,
                    out ResearchProgressionStateRecord state);
                if (hasState &&
                    (!string.Equals(
                        state.DefinitionContentVersion,
                        definition.Identity.ContentVersion,
                        StringComparison.Ordinal) ||
                     state.Level < definition.InitialLevel ||
                     state.Level > definition.MaximumLevel ||
                     state.HasActiveLegacyOrder ||
                     state.CompletionTimestamp != 0))
                {
                    return false;
                }

            }

            return compatibility.PreservedResearchStates.All(state =>
                definitionsById.ContainsKey(state.DefinitionId) ||
                string.Equals(
                    state.DefinitionId,
                    targetDefinitionId,
                    StringComparison.Ordinal));
        }

        private static bool HasConsistentTroopCompletionRows(
            ProgressionCompatibilityResult compatibility,
            string targetDefinitionId)
        {
            var definitionsById = compatibility.TroopDefinitions.ToDictionary(
                definition => definition.Identity.Id,
                StringComparer.Ordinal);
            var statesById =
                compatibility.PreservedTroopStates.ToDictionary(
                    state => state.DefinitionId,
                    StringComparer.Ordinal);
            var snapshotsById = compatibility.Troops.ToDictionary(
                snapshot => snapshot.Definition.Identity.Id,
                StringComparer.Ordinal);

            foreach (TroopProgressionSnapshot snapshot in compatibility.Troops)
            {
                string definitionId = snapshot.Definition.Identity.Id;
                if (!definitionsById.TryGetValue(
                        definitionId,
                        out TroopProgressionDefinition definition) ||
                    !ReferenceEquals(snapshot.Definition, definition))
                {
                    return false;
                }

                bool hasState = statesById.TryGetValue(
                    definitionId,
                    out TroopProgressionStateRecord state);
                if (snapshot.Origin ==
                    ProgressionStateOrigin.EffectiveInitialUnpersisted)
                {
                    if (hasState ||
                        snapshot.ActiveCount != 0 ||
                        snapshot.WoundedCount != 0 ||
                        snapshot.ReservedCount != 0)
                    {
                        return false;
                    }

                    continue;
                }

                if (!hasState ||
                    !string.Equals(
                        state.DefinitionContentVersion,
                        snapshot.Definition.Identity.ContentVersion,
                        StringComparison.Ordinal) ||
                    state.ActiveCount != snapshot.ActiveCount ||
                    state.WoundedCount != snapshot.WoundedCount ||
                    state.ReservedCount != snapshot.ReservedCount)
                {
                    return false;
                }
            }

            bool snapshotsRequired = compatibility.Status ==
                                     ProgressionCompatibilityStatus.Available;
            foreach (TroopProgressionDefinition definition in
                     compatibility.TroopDefinitions)
            {
                string definitionId = definition.Identity.Id;
                if (snapshotsRequired &&
                    !snapshotsById.ContainsKey(definitionId))
                {
                    return false;
                }

                if (string.Equals(
                        definitionId,
                        targetDefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool hasState = statesById.TryGetValue(
                    definitionId,
                    out TroopProgressionStateRecord state);
                if (hasState)
                {
                    try
                    {
                        long total = checked(
                            checked(
                                state.ActiveCount +
                                state.WoundedCount) +
                            state.ReservedCount);
                        if (!string.Equals(
                                state.DefinitionContentVersion,
                                definition.Identity.ContentVersion,
                                StringComparison.Ordinal) ||
                            total > definition.MaximumInventoryCount)
                        {
                            return false;
                        }
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }
                }

            }

            return compatibility.PreservedTroopStates.All(state =>
                definitionsById.ContainsKey(state.DefinitionId) ||
                string.Equals(
                    state.DefinitionId,
                    targetDefinitionId,
                    StringComparison.Ordinal));
        }

        private static bool IsValidResearchSnapshot(
            ResearchProgressionSnapshot snapshot)
        {
            ResearchProgressionDefinition definition = snapshot?.Definition;
            return definition != null &&
                   IsValidDefinitionIdentity(
                       definition.Identity,
                       ProgressionCompatibilityPlanner.ResearchSchemaVersion) &&
                   IsValidCostProfile(definition.CostProfile) &&
                   IsValidDurationProfile(definition.DurationProfile, false) &&
                   IsValidPrerequisites(definition.Prerequisites) &&
                   definition.EffectProfiles.Count <=
                   ProgressionCompatibilityPlanner.MaximumEffectsPerResearch &&
                   definition.EffectProfiles.All(IsValidProfileIdentity) &&
                   definition.EffectProfiles
                       .GroupBy(identity => identity.Id, StringComparer.Ordinal)
                       .All(group => group.Count() == 1) &&
                   definition.InitialLevel >= 0 &&
                   definition.MaximumLevel >= definition.InitialLevel &&
                   snapshot.Level >= definition.InitialLevel &&
                   snapshot.Level <= definition.MaximumLevel &&
                   Enum.IsDefined(typeof(ProgressionStateOrigin), snapshot.Origin) &&
                   !snapshot.HasActiveLegacyOrder &&
                   snapshot.CompletionTimestamp == 0;
        }

        private static bool IsValidTroopSnapshot(
            TroopProgressionSnapshot snapshot)
        {
            TroopProgressionDefinition definition = snapshot?.Definition;
            if (definition == null ||
                !IsValidDefinitionIdentity(
                    definition.Identity,
                    ProgressionCompatibilityPlanner.TroopSchemaVersion) ||
                !IsValidCostProfile(definition.CostProfile) ||
                !IsValidDurationProfile(definition.DurationProfile, true) ||
                !IsValidPrerequisites(definition.Prerequisites) ||
                !IsValidProfileIdentity(definition.InventoryPolicy) ||
                !IsValidProfileIdentity(definition.BattleProfile) ||
                definition.InventoryCapacityPolicy !=
                TroopInventoryCapacityPolicy.SeparatedCountsTotalCapacityV1 ||
                definition.MaximumInventoryCount < 0 ||
                definition.MaximumBatchCount <= 0 ||
                definition.MaximumBatchCount > definition.MaximumInventoryCount ||
                snapshot.ActiveCount < 0 ||
                snapshot.WoundedCount < 0 ||
                snapshot.ReservedCount < 0 ||
                !Enum.IsDefined(typeof(ProgressionStateOrigin), snapshot.Origin))
            {
                return false;
            }

            try
            {
                return checked(
                    checked(snapshot.ActiveCount + snapshot.WoundedCount) +
                    snapshot.ReservedCount) <=
                    definition.MaximumInventoryCount;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool IsValidPrerequisites(
            IReadOnlyList<ProgressionPrerequisite> prerequisites)
        {
            return prerequisites != null &&
                   prerequisites.Count <=
                   ProgressionCompatibilityPlanner
                       .MaximumPrerequisitesPerDefinition &&
                   prerequisites.All(prerequisite =>
                       prerequisite != null &&
                       IsValidId(prerequisite.DefinitionId) &&
                       prerequisite.MinimumLevel >= 0) &&
                   prerequisites
                       .GroupBy(
                           prerequisite => prerequisite.DefinitionId,
                           StringComparer.Ordinal)
                       .All(group => group.Count() == 1);
        }

        private static bool IsValidCostProfile(ProgressionCostProfile profile)
        {
            return profile != null &&
                   IsValidProfileIdentity(profile.Identity) &&
                   profile.UnitCosts.Count > 0 &&
                   profile.UnitCosts.Count <=
                   ProgressionCompatibilityPlanner.MaximumCostEntriesPerProfile &&
                   profile.MaximumAmountPerResource > 0 &&
                   profile.UnitCosts.All(cost =>
                       Enum.IsDefined(typeof(ResourceType), cost.ResourceType) &&
                       cost.Amount > 0 &&
                       cost.Amount <= profile.MaximumAmountPerResource) &&
                   profile.UnitCosts
                       .GroupBy(cost => cost.ResourceType)
                       .All(group => group.Count() == 1);
        }

        private static bool IsValidDurationProfile(
            ProgressionDurationProfile profile,
            bool zeroDurationAllowed)
        {
            return profile != null &&
                   IsValidProfileIdentity(profile.Identity) &&
                   profile.UnitSeconds >= 0 &&
                   profile.MaximumSeconds >= profile.UnitSeconds &&
                   (profile.UnitSeconds > 0 ||
                    (zeroDurationAllowed && profile.AllowsZeroDuration));
        }

        private static bool IsValidOrder(ProgressionOrderSnapshot order)
        {
            if (order == null ||
                !Enum.IsDefined(typeof(ProgressionOrderType), order.OrderType) ||
                !Enum.IsDefined(typeof(ProgressionOrderState), order.State) ||
                !IsValidId(order.ProfileId) ||
                !IsValidId(order.DefinitionId) ||
                !IsValidId(order.DefinitionContentVersion) ||
                !IsValidDefinitionIdentity(
                    order.DefinitionSource,
                    order.OrderType == ProgressionOrderType.ResearchLevel
                        ? ProgressionCompatibilityPlanner.ResearchSchemaVersion
                        : ProgressionCompatibilityPlanner.TroopSchemaVersion) ||
                !IsValidProfileIdentity(order.CostProfile) ||
                !IsValidProfileIdentity(order.DurationProfile) ||
                !string.Equals(
                    order.DefinitionId,
                    order.DefinitionSource.Id,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    order.DefinitionContentVersion,
                    order.DefinitionSource.ContentVersion,
                    StringComparison.Ordinal) ||
                !IsValidId(order.OrderId) ||
                !IsValidId(order.StartOperationId) ||
                !IsValidId(order.CompletionOperationId) ||
                string.Equals(
                    order.StartOperationId,
                    order.CompletionOperationId,
                    StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(order.CancellationOperationId) &&
                 (!IsValidId(order.CancellationOperationId) ||
                  string.Equals(
                      order.CancellationOperationId,
                      order.StartOperationId,
                      StringComparison.Ordinal) ||
                  string.Equals(
                      order.CancellationOperationId,
                      order.CompletionOperationId,
                      StringComparison.Ordinal))) ||
                order.CommittedCosts.Count == 0 ||
                order.CommittedCosts.Count >
                ProgressionCompatibilityPlanner.MaximumCostEntriesPerProfile ||
                order.CommittedCosts.Any(cost =>
                    !Enum.IsDefined(typeof(ResourceType), cost.ResourceType) ||
                    cost.Amount <= 0) ||
                order.CommittedCosts
                    .GroupBy(cost => cost.ResourceType)
                    .Any(group => group.Count() > 1) ||
                !IsValidId(order.CatalogSetId) ||
                !IsValidId(order.CatalogRevision) ||
                !IsValidId(order.ProgressionRevision) ||
                !IsValidId(order.EconomyRevision) ||
                !IsValidId(order.PrerequisiteRevision) ||
                !IsValidId(order.RequestPolicyVersion) ||
                !IsValidTimestampPolicy(order.TimestampPolicy) ||
                !IsHash(order.OrderHash) ||
                order.PreviousValue < 0 ||
                order.TargetValue <= order.PreviousValue ||
                order.MaximumValue < order.TargetValue ||
                !IsTimestampWithinAbsolutePolicy(
                    order.StartTimestamp,
                    order.TimestampPolicy) ||
                !IsTimestampWithinAbsolutePolicy(
                    order.EndTimestamp,
                    order.TimestampPolicy) ||
                order.EndTimestamp < order.StartTimestamp ||
                order.EndTimestamp - order.StartTimestamp >
                order.TimestampPolicy.MaximumFutureLeadSeconds)
            {
                return false;
            }

            try
            {
                long expectedTarget = order.OrderType ==
                    ProgressionOrderType.ResearchLevel
                    ? checked(order.PreviousValue + 1)
                    : checked(order.PreviousValue + order.BatchCount);
                if ((order.OrderType == ProgressionOrderType.ResearchLevel &&
                     (order.BatchCount != 0 ||
                      order.InventoryCapacityPolicy !=
                      TroopInventoryCapacityPolicy.Unresolved)) ||
                    (order.OrderType ==
                     ProgressionOrderType.TroopTrainingBatch &&
                     (order.BatchCount <= 0 ||
                      order.InventoryCapacityPolicy !=
                      TroopInventoryCapacityPolicy
                          .SeparatedCountsTotalCapacityV1)) ||
                    order.TargetValue != expectedTarget)
                {
                    return false;
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            return string.Equals(
                order.OrderHash,
                BuildOrderHash(
                    order.OrderType,
                    order.ProfileId,
                    order.DefinitionId,
                    order.DefinitionContentVersion,
                    order.DefinitionSource,
                    order.CostProfile,
                    order.DurationProfile,
                    order.OrderId,
                    order.StartOperationId,
                    order.CompletionOperationId,
                    order.CancellationOperationId,
                    order.PreviousValue,
                    order.TargetValue,
                    order.BatchCount,
                    order.CommittedCosts,
                    order.StartTimestamp,
                    order.EndTimestamp,
                    order.CatalogSetId,
                    order.CatalogRevision,
                    order.ProgressionRevision,
                    order.EconomyRevision,
                    order.RequestPolicyVersion,
                    order.MaximumValue,
                    order.InventoryCapacityPolicy,
                    order.TimestampPolicy,
                    order.PrerequisiteRevision),
                StringComparison.Ordinal);
        }

        private static string BuildOrderHash(
            ProgressionOrderType orderType,
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
            string catalogRevision,
            string progressionRevision,
            string economyRevision,
            string requestPolicyVersion,
            long maximumValue,
            TroopInventoryCapacityPolicy inventoryCapacityPolicy,
            ProgressionTimestampPolicy timestampPolicy,
            string prerequisiteRevision)
        {
            var segments = new List<string>
            {
                "progression-order",
                Invariant((long)orderType),
                profileId,
                definitionId,
                definitionContentVersion,
                definitionSource?.Id,
                definitionSource?.SchemaVersion,
                definitionSource?.ContentVersion,
                definitionSource?.SourceRevision,
                definitionSource?.RawSha256,
                costProfile?.Id,
                costProfile?.SchemaVersion,
                costProfile?.ContentVersion,
                costProfile?.SourceRevision,
                costProfile?.RawSha256,
                durationProfile?.Id,
                durationProfile?.SchemaVersion,
                durationProfile?.ContentVersion,
                durationProfile?.SourceRevision,
                durationProfile?.RawSha256,
                orderId,
                startOperationId,
                completionOperationId,
                cancellationOperationId,
                Invariant(previousValue),
                Invariant(targetValue),
                Invariant(batchCount),
                Invariant(maximumValue),
                Invariant((long)inventoryCapacityPolicy),
                Invariant(startTimestamp),
                Invariant(endTimestamp),
                catalogSetId,
                catalogRevision,
                progressionRevision,
                economyRevision,
                prerequisiteRevision,
                requestPolicyVersion
            };
            AddTimestampPolicyHashSegments(segments, timestampPolicy);
            foreach (BuildingConstructionCost cost in
                     (committedCosts ?? Array.Empty<BuildingConstructionCost>())
                     .OrderBy(cost => cost.ResourceType))
            {
                segments.Add("cost");
                segments.Add(Invariant((long)cost.ResourceType));
                segments.Add(Invariant(cost.Amount));
            }

            return ProgressionContractHash.Compute(segments.ToArray());
        }

        private static string BuildStartSemanticHash(ProgressionStartRequest request)
        {
            return request == null
                ? string.Empty
                : ProgressionContractHash.Compute(
                    "start-request",
                    request.ProfileId,
                    Invariant((long)request.OrderType),
                    request.DefinitionId,
                    request.OrderId,
                    request.OperationId,
                    Invariant(request.RequestedTargetLevel),
                    Invariant(request.RequestedBatchCount),
                    request.ExpectedCatalogSetId,
                    request.ExpectedProgressionRevision,
                    request.ExpectedEconomyRevision,
                    request.ExpectedPrerequisiteRevision,
                    request.RequestPolicyVersion);
        }

        private static string BuildCompletionSemanticHash(
            ProgressionCompletionRequest request)
        {
            return request == null
                ? string.Empty
                : ProgressionContractHash.Compute(
                    "completion-request",
                    request.ProfileId,
                    request.OrderId,
                    request.OperationId,
                    request.ExpectedCatalogSetId,
                    request.ExpectedProgressionRevision,
                    request.ExpectedEconomyRevision,
                    request.ExpectedQuestRevision,
                    request.CompletionPolicyVersion);
        }

        private static ProgressionStartPlan RejectStart(
            ProgressionPlanStatus status,
            ProgressionStartRequest request,
            string semanticHash,
            ProgressionDiagnosticCode diagnosticCode,
            ProgressionDomain domain)
        {
            return new ProgressionStartPlan(
                status,
                request?.OrderType ?? ProgressionOrderType.ResearchLevel,
                request?.ProfileId,
                request?.DefinitionId,
                string.Empty,
                null,
                null,
                null,
                request?.OrderId,
                request?.OperationId,
                0,
                0,
                0,
                Array.Empty<BuildingConstructionCost>(),
                0,
                0,
                request?.ExpectedCatalogSetId,
                request?.ExpectedProgressionRevision,
                request?.ExpectedEconomyRevision,
                request?.RequestPolicyVersion,
                semanticHash,
                string.Empty,
                new[]
                {
                    new ProgressionDiagnostic(
                        diagnosticCode,
                        domain,
                        request?.DefinitionId,
                        -1)
                });
        }

        private static ProgressionCompletionPlan RejectCompletion(
            ProgressionPlanStatus status,
            ProgressionOrderSnapshot order,
            ProgressionCompletionRequest request,
            string semanticHash,
            ProgressionDiagnosticCode diagnosticCode,
            ProgressionDomain domain,
            ProgressionOrderSourceDisposition sourceDisposition =
                ProgressionOrderSourceDisposition.ExactCurrentSource)
        {
            return new ProgressionCompletionPlan(
                status,
                order?.OrderType ?? ProgressionOrderType.ResearchLevel,
                order?.DefinitionId,
                order?.OrderId ?? request?.OrderId,
                request?.OperationId,
                order?.PreviousValue ?? 0,
                order?.PreviousValue ?? 0,
                0,
                request?.ExpectedCatalogSetId,
                request?.ExpectedProgressionRevision,
                request?.ExpectedEconomyRevision,
                request?.ExpectedQuestRevision,
                request?.CompletionPolicyVersion,
                semanticHash,
                string.Empty,
                new[]
                {
                    new ProgressionDiagnostic(
                        diagnosticCode,
                        domain,
                        order?.DefinitionId,
                        -1)
                },
                null,
                sourceDisposition);
        }

        private static bool IsValidReadyStartPlan(
            ProgressionStartPlan plan)
        {
            if (plan == null ||
                plan.Status != ProgressionPlanStatus.Ready ||
                !Enum.IsDefined(typeof(ProgressionOrderType), plan.OrderType) ||
                !IsValidId(plan.ProfileId) ||
                !IsValidId(plan.DefinitionId) ||
                !IsValidId(plan.DefinitionContentVersion) ||
                !IsValidDefinitionIdentity(
                    plan.DefinitionSource,
                    plan.OrderType == ProgressionOrderType.ResearchLevel
                        ? ProgressionCompatibilityPlanner.ResearchSchemaVersion
                        : ProgressionCompatibilityPlanner.TroopSchemaVersion) ||
                !IsValidProfileIdentity(plan.CostProfile) ||
                !IsValidProfileIdentity(plan.DurationProfile) ||
                !string.Equals(
                    plan.DefinitionId,
                    plan.DefinitionSource.Id,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.DefinitionContentVersion,
                    plan.DefinitionSource.ContentVersion,
                    StringComparison.Ordinal) ||
                !IsValidId(plan.OrderId) ||
                !IsValidId(plan.OperationId) ||
                string.Equals(
                    plan.OrderId,
                    plan.OperationId,
                    StringComparison.Ordinal) ||
                !IsValidId(plan.CatalogSetId) ||
                !IsValidId(plan.CatalogRevision) ||
                !IsValidId(plan.ProgressionRevision) ||
                !IsValidId(plan.EconomyRevision) ||
                !IsValidId(plan.PrerequisiteRevision) ||
                !IsValidId(plan.RequestPolicyVersion) ||
                !IsHash(plan.SemanticHash) ||
                !IsHash(plan.PlanHash) ||
                plan.CommittedReceipt != null ||
                plan.PreviousValue < 0 ||
                plan.TargetValue <= plan.PreviousValue ||
                plan.MaximumValue < plan.TargetValue ||
                plan.Costs.Count == 0 ||
                plan.Costs.Count >
                ProgressionCompatibilityPlanner.MaximumCostEntriesPerProfile ||
                plan.Costs.Any(cost =>
                    !Enum.IsDefined(typeof(ResourceType), cost.ResourceType) ||
                    cost.Amount <= 0) ||
                plan.Costs
                    .GroupBy(cost => cost.ResourceType)
                    .Any(group => group.Count() > 1) ||
                !IsValidTimestampPolicy(plan.TimestampPolicy) ||
                !IsTimestampWithinAbsolutePolicy(
                    plan.StartTimestamp,
                    plan.TimestampPolicy) ||
                !IsTimestampWithinAbsolutePolicy(
                    plan.EndTimestamp,
                    plan.TimestampPolicy) ||
                plan.EndTimestamp < plan.StartTimestamp ||
                plan.EndTimestamp - plan.StartTimestamp >
                plan.TimestampPolicy.MaximumFutureLeadSeconds)
            {
                return false;
            }

            int requestedTargetLevel;
            long requestedBatch;
            try
            {
                if (plan.OrderType == ProgressionOrderType.ResearchLevel)
                {
                    if (plan.BatchCount != 0 ||
                        plan.InventoryCapacityPolicy !=
                        TroopInventoryCapacityPolicy.Unresolved ||
                        plan.TargetValue > int.MaxValue ||
                        plan.TargetValue != checked(plan.PreviousValue + 1))
                    {
                        return false;
                    }

                    requestedTargetLevel = checked((int)plan.TargetValue);
                    requestedBatch = 0;
                }
                else
                {
                    if (plan.BatchCount <= 0 ||
                        plan.InventoryCapacityPolicy !=
                        TroopInventoryCapacityPolicy
                            .SeparatedCountsTotalCapacityV1 ||
                        plan.TargetValue !=
                        checked(plan.PreviousValue + plan.BatchCount))
                    {
                        return false;
                    }

                    requestedTargetLevel = 0;
                    requestedBatch = plan.BatchCount;
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            var request = new ProgressionStartRequest(
                plan.ProfileId,
                plan.OrderType,
                plan.DefinitionId,
                plan.OrderId,
                plan.OperationId,
                requestedTargetLevel,
                requestedBatch,
                plan.CatalogSetId,
                plan.ProgressionRevision,
                plan.EconomyRevision,
                plan.RequestPolicyVersion,
                plan.PrerequisiteRevision);
            if (!string.Equals(
                    plan.SemanticHash,
                    BuildStartSemanticHash(request),
                    StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(
                plan.PlanHash,
                BuildStartPlanHash(plan),
                StringComparison.Ordinal);
        }

        private static bool IsOrderForStartPlan(
            ProgressionOrderSnapshot order,
            ProgressionStartPlan plan)
        {
            return order != null &&
                   plan != null &&
                   order.State == ProgressionOrderState.Active &&
                   order.OrderType == plan.OrderType &&
                   string.Equals(
                       order.ProfileId,
                       plan.ProfileId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.DefinitionId,
                       plan.DefinitionId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.DefinitionContentVersion,
                       plan.DefinitionContentVersion,
                       StringComparison.Ordinal) &&
                   AreSameIdentity(
                       order.DefinitionSource,
                       plan.DefinitionSource) &&
                   AreSameIdentity(order.CostProfile, plan.CostProfile) &&
                   AreSameIdentity(
                       order.DurationProfile,
                       plan.DurationProfile) &&
                   string.Equals(
                       order.OrderId,
                       plan.OrderId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.StartOperationId,
                       plan.OperationId,
                       StringComparison.Ordinal) &&
                   order.PreviousValue == plan.PreviousValue &&
                   order.TargetValue == plan.TargetValue &&
                   order.BatchCount == plan.BatchCount &&
                   order.MaximumValue == plan.MaximumValue &&
                   order.InventoryCapacityPolicy ==
                   plan.InventoryCapacityPolicy &&
                   HaveSameCosts(order.CommittedCosts, plan.Costs) &&
                   order.StartTimestamp == plan.StartTimestamp &&
                   order.EndTimestamp == plan.EndTimestamp &&
                   HaveSameTimestampPolicy(
                       order.TimestampPolicy,
                       plan.TimestampPolicy) &&
                   string.Equals(
                       order.CatalogSetId,
                       plan.CatalogSetId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.CatalogRevision,
                       plan.CatalogRevision,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.ProgressionRevision,
                       plan.ProgressionRevision,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.EconomyRevision,
                       plan.EconomyRevision,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.PrerequisiteRevision,
                       plan.PrerequisiteRevision,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       order.RequestPolicyVersion,
                       plan.RequestPolicyVersion,
                       StringComparison.Ordinal);
        }

        private static bool IsValidReadyCompletionPlan(
            ProgressionCompletionPlan plan)
        {
            ProgressionOrderSnapshot order = plan?.OrderSnapshot;
            if (plan == null ||
                plan.Status != ProgressionPlanStatus.Ready ||
                plan.CommittedReceipt != null ||
                order == null ||
                !IsValidOrder(order) ||
                order.State != ProgressionOrderState.Active ||
                !Enum.IsDefined(
                    typeof(ProgressionOrderSourceDisposition),
                    plan.SourceDisposition) ||
                plan.SourceDisposition ==
                ProgressionOrderSourceDisposition.MigrationRequired ||
                plan.SourceDisposition ==
                ProgressionOrderSourceDisposition.UnsupportedVersion ||
                plan.OrderType != order.OrderType ||
                !string.Equals(
                    plan.DefinitionId,
                    order.DefinitionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.OrderId,
                    order.OrderId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.OperationId,
                    order.CompletionOperationId,
                    StringComparison.Ordinal) ||
                plan.PreviousValue != order.PreviousValue ||
                plan.TargetValue != order.TargetValue ||
                plan.QuestProgressAmount !=
                (order.OrderType == ProgressionOrderType.ResearchLevel
                    ? 1
                    : order.BatchCount) ||
                !IsValidId(plan.CatalogSetId) ||
                !IsValidId(plan.CatalogRevision) ||
                !IsValidId(plan.ProgressionRevision) ||
                !IsValidId(plan.EconomyRevision) ||
                !IsValidId(plan.QuestRevision) ||
                !IsValidId(plan.CompletionPolicyVersion) ||
                !string.Equals(
                    plan.CatalogSetId,
                    order.CatalogSetId,
                    StringComparison.Ordinal) ||
                !IsHash(plan.SemanticHash) ||
                !IsHash(plan.PlanHash) ||
                EvaluateOrderClock(order, plan.CommitTimestamp) !=
                ProgressionPlanStatus.Ready)
            {
                return false;
            }

            var request = new ProgressionCompletionRequest(
                order.ProfileId,
                plan.OrderId,
                plan.OperationId,
                plan.CatalogSetId,
                plan.ProgressionRevision,
                plan.EconomyRevision,
                plan.QuestRevision,
                plan.CompletionPolicyVersion);
            return string.Equals(
                       plan.SemanticHash,
                       BuildCompletionSemanticHash(request),
                       StringComparison.Ordinal) &&
                   string.Equals(
                       plan.PlanHash,
                       BuildCompletionPlanHash(plan),
                       StringComparison.Ordinal);
        }

        private static string BuildCompletionPlanHash(
            ProgressionCompletionPlan plan)
        {
            return ProgressionContractHash.Compute(
                "completion",
                plan.SemanticHash,
                plan.OrderSnapshot?.OrderHash,
                plan.CatalogSetId,
                plan.CatalogRevision,
                plan.ProgressionRevision,
                Invariant(plan.PreviousValue),
                Invariant(plan.TargetValue),
                Invariant(plan.QuestProgressAmount),
                plan.EconomyRevision,
                plan.QuestRevision,
                plan.CompletionPolicyVersion,
                Invariant((long)plan.SourceDisposition),
                Invariant(plan.CommitTimestamp));
        }

        private static string BuildStartPlanHash(ProgressionStartPlan plan)
        {
            var segments = new List<string> { "start", plan.SemanticHash };
            AddIdentityHashSegments(
                segments,
                "definition-source",
                plan.DefinitionSource);
            AddIdentityHashSegments(
                segments,
                "cost-profile",
                plan.CostProfile);
            AddIdentityHashSegments(
                segments,
                "duration-profile",
                plan.DurationProfile);
            segments.Add("values");
            segments.Add(Invariant(plan.PreviousValue));
            segments.Add(Invariant(plan.TargetValue));
            segments.Add(Invariant(plan.BatchCount));
            segments.Add(Invariant(plan.MaximumValue));
            segments.Add(Invariant((long)plan.InventoryCapacityPolicy));
            segments.Add(Invariant(plan.StartTimestamp));
            segments.Add(Invariant(plan.EndTimestamp));
            segments.Add(plan.CatalogSetId);
            segments.Add(plan.CatalogRevision);
            segments.Add(plan.ProgressionRevision);
            segments.Add(plan.EconomyRevision);
            segments.Add(plan.PrerequisiteRevision);
            AddTimestampPolicyHashSegments(segments, plan.TimestampPolicy);
            foreach (BuildingConstructionCost cost in plan.Costs
                         .OrderBy(cost => cost.ResourceType))
            {
                segments.Add("cost");
                segments.Add(Invariant((long)cost.ResourceType));
                segments.Add(Invariant(cost.Amount));
            }

            return ProgressionContractHash.Compute(segments.ToArray());
        }

        private static ProgressionOrderSnapshot BuildCommittedOrderSnapshot(
            ProgressionCommittedOperationResult result)
        {
            if (result == null || !IsHash(result.OrderHash))
            {
                return null;
            }

            var order = new ProgressionOrderSnapshot(
                result.OrderType,
                ProgressionOrderState.Active,
                result.ProfileId,
                result.DefinitionId,
                result.DefinitionSource?.ContentVersion,
                result.DefinitionSource,
                result.CostProfile,
                result.DurationProfile,
                result.OrderId,
                result.StartOperationId,
                result.CompletionOperationId,
                result.CancellationOperationId,
                result.PreviousValue,
                result.TargetValue,
                result.BatchCount,
                result.Costs,
                result.StartTimestamp,
                result.EndTimestamp,
                result.OrderCatalogSetId,
                result.OrderProgressionRevision,
                result.OrderEconomyRevision,
                result.OrderPolicyVersion,
                result.OrderHash,
                result.MaximumValue,
                result.InventoryCapacityPolicy,
                result.TimestampPolicy,
                result.PrerequisiteRevision,
                result.OrderCatalogRevision);
            return IsValidOrder(order) ? order : null;
        }

        private static ProgressionStartPlan BuildCommittedStartPlan(
            ProgressionCommittedOperationResult result)
        {
            return result == null
                ? null
                : new ProgressionStartPlan(
                    ProgressionPlanStatus.Ready,
                    result.OrderType,
                    result.ProfileId,
                    result.DefinitionId,
                    result.DefinitionSource?.ContentVersion,
                    result.DefinitionSource,
                    result.CostProfile,
                    result.DurationProfile,
                    result.OrderId,
                    result.OperationId,
                    result.PreviousValue,
                    result.TargetValue,
                    result.BatchCount,
                    result.Costs,
                    result.StartTimestamp,
                    result.EndTimestamp,
                    result.CatalogSetId,
                    result.ProgressionRevision,
                    result.EconomyRevision,
                    result.OperationPolicyVersion,
                    result.SemanticHash,
                    result.PlanHash,
                    Array.Empty<ProgressionDiagnostic>(),
                    result.MaximumValue,
                    result.InventoryCapacityPolicy,
                    result.TimestampPolicy,
                    result.PrerequisiteRevision,
                    null,
                    result.CatalogRevision);
        }

        private static ProgressionCompletionPlan BuildCommittedCompletionPlan(
            ProgressionCommittedOperationResult result,
            ProgressionOrderSnapshot order)
        {
            return result == null || order == null
                ? null
                : new ProgressionCompletionPlan(
                    ProgressionPlanStatus.Ready,
                    result.OrderType,
                    result.DefinitionId,
                    result.OrderId,
                    result.OperationId,
                    result.PreviousValue,
                    result.TargetValue,
                    result.QuestProgressAmount,
                    result.CatalogSetId,
                    result.ProgressionRevision,
                    result.EconomyRevision,
                    result.QuestRevision,
                    result.OperationPolicyVersion,
                    result.SemanticHash,
                    result.PlanHash,
                    Array.Empty<ProgressionDiagnostic>(),
                    order,
                    result.SourceDisposition,
                    null,
                    result.CommitTimestamp,
                    result.CatalogRevision);
        }

        private static bool IsValidReceipt(
            ProgressionOperationReceipt receipt)
        {
            if (receipt == null ||
                !IsValidId(receipt.OperationId) ||
                !IsHash(receipt.SemanticHash) ||
                !Enum.IsDefined(
                    typeof(ProgressionOperationDurability),
                    receipt.Durability))
            {
                return false;
            }

            if (receipt.Durability ==
                ProgressionOperationDurability.CommitUncertain)
            {
                return receipt.CommittedResult == null &&
                       receipt.OperationKind == ProgressionOperationKind.None &&
                       string.IsNullOrEmpty(receipt.ResultHash);
            }

            ProgressionCommittedOperationResult result =
                receipt.CommittedResult;
            return result != null &&
                   receipt.OperationKind == result.OperationKind &&
                   string.Equals(
                       receipt.OperationId,
                       result.OperationId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       receipt.SemanticHash,
                       result.SemanticHash,
                       StringComparison.Ordinal) &&
                   TryBuildCommittedResultHash(
                       result,
                       out string expectedResultHash) &&
                   string.Equals(
                       receipt.ResultHash,
                       expectedResultHash,
                       StringComparison.Ordinal);
        }

        private static bool TryBuildCommittedResultHash(
            ProgressionCommittedOperationResult result,
            out string resultHash)
        {
            resultHash = string.Empty;
            if (!IsValidCommittedResult(result))
            {
                return false;
            }

            var segments = new List<string>
            {
                "progression-committed-result",
                Invariant((long)result.OperationKind),
                Invariant((long)result.OrderType),
                result.ProfileId
            };
            AddIdentityHashSegments(
                segments,
                "definition-source",
                result.DefinitionSource);
            AddIdentityHashSegments(
                segments,
                "cost-profile",
                result.CostProfile);
            AddIdentityHashSegments(
                segments,
                "duration-profile",
                result.DurationProfile);
            segments.Add(result.OrderId);
            segments.Add(result.OperationId);
            segments.Add(result.StartOperationId);
            segments.Add(result.CompletionOperationId);
            segments.Add(result.CancellationOperationId);
            segments.Add(Invariant(result.PreviousValue));
            segments.Add(Invariant(result.TargetValue));
            segments.Add(Invariant(result.BatchCount));
            segments.Add(Invariant(result.MaximumValue));
            segments.Add(Invariant(result.QuestProgressAmount));
            segments.Add(Invariant((long)result.InventoryCapacityPolicy));
            segments.Add(Invariant(result.StartTimestamp));
            segments.Add(Invariant(result.EndTimestamp));
            segments.Add(Invariant(result.CommitTimestamp));
            AddTimestampPolicyHashSegments(segments, result.TimestampPolicy);
            segments.Add(result.CatalogSetId);
            segments.Add(result.CatalogRevision);
            segments.Add(result.ProgressionRevision);
            segments.Add(result.EconomyRevision);
            segments.Add(result.PrerequisiteRevision);
            segments.Add(result.QuestRevision);
            segments.Add(result.OperationPolicyVersion);
            segments.Add(result.OrderPolicyVersion);
            segments.Add(result.OrderCatalogSetId);
            segments.Add(result.OrderCatalogRevision);
            segments.Add(result.OrderProgressionRevision);
            segments.Add(result.OrderEconomyRevision);
            segments.Add(result.OrderHash);
            segments.Add(Invariant((long)result.SourceDisposition));
            segments.Add(result.SemanticHash);
            segments.Add(result.PlanHash);
            foreach (BuildingConstructionCost cost in result.Costs
                         .OrderBy(cost => cost.ResourceType))
            {
                segments.Add("cost");
                segments.Add(Invariant((long)cost.ResourceType));
                segments.Add(Invariant(cost.Amount));
            }

            return ProgressionContractHash.TryCompute(
                out resultHash,
                segments.ToArray());
        }

        private static bool IsValidCommittedResult(
            ProgressionCommittedOperationResult result)
        {
            if (result == null ||
                !Enum.IsDefined(
                    typeof(ProgressionOperationKind),
                    result.OperationKind) ||
                result.OperationKind == ProgressionOperationKind.None ||
                !Enum.IsDefined(
                    typeof(ProgressionOrderType),
                    result.OrderType) ||
                !Enum.IsDefined(
                    typeof(ProgressionOrderSourceDisposition),
                    result.SourceDisposition) ||
                !IsValidId(result.ProfileId) ||
                !IsValidDefinitionIdentity(
                    result.DefinitionSource,
                    result.OrderType == ProgressionOrderType.ResearchLevel
                        ? ProgressionCompatibilityPlanner.ResearchSchemaVersion
                        : ProgressionCompatibilityPlanner.TroopSchemaVersion) ||
                !IsValidProfileIdentity(result.CostProfile) ||
                !IsValidProfileIdentity(result.DurationProfile) ||
                !IsValidId(result.OrderId) ||
                !IsValidId(result.OperationId) ||
                !IsValidId(result.StartOperationId) ||
                !IsValidId(result.CatalogSetId) ||
                !IsValidId(result.CatalogRevision) ||
                !IsValidId(result.ProgressionRevision) ||
                !IsValidId(result.EconomyRevision) ||
                !IsValidId(result.PrerequisiteRevision) ||
                !IsValidId(result.OperationPolicyVersion) ||
                !IsValidId(result.OrderPolicyVersion) ||
                !IsValidId(result.OrderCatalogSetId) ||
                !IsValidId(result.OrderCatalogRevision) ||
                !IsValidId(result.OrderProgressionRevision) ||
                !IsValidId(result.OrderEconomyRevision) ||
                !IsHash(result.SemanticHash) ||
                !IsHash(result.PlanHash) ||
                result.PreviousValue < 0 ||
                result.TargetValue <= result.PreviousValue ||
                result.MaximumValue < result.TargetValue ||
                result.QuestProgressAmount < 0 ||
                result.Costs.Count == 0 ||
                result.Costs.Count >
                ProgressionCompatibilityPlanner.MaximumCostEntriesPerProfile ||
                result.Costs.Any(cost =>
                    !Enum.IsDefined(typeof(ResourceType), cost.ResourceType) ||
                    cost.Amount <= 0) ||
                result.Costs
                    .GroupBy(cost => cost.ResourceType)
                    .Any(group => group.Count() > 1) ||
                !IsValidTimestampPolicy(result.TimestampPolicy) ||
                !IsTimestampWithinAbsolutePolicy(
                    result.StartTimestamp,
                    result.TimestampPolicy) ||
                !IsTimestampWithinAbsolutePolicy(
                    result.EndTimestamp,
                    result.TimestampPolicy) ||
                !IsTimestampWithinAbsolutePolicy(
                    result.CommitTimestamp,
                    result.TimestampPolicy) ||
                result.EndTimestamp < result.StartTimestamp ||
                result.EndTimestamp - result.StartTimestamp >
                result.TimestampPolicy.MaximumFutureLeadSeconds)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(result.CancellationOperationId) &&
                (!IsValidId(result.CancellationOperationId) ||
                 string.Equals(
                     result.CancellationOperationId,
                     result.StartOperationId,
                     StringComparison.Ordinal) ||
                 string.Equals(
                     result.CancellationOperationId,
                     result.CompletionOperationId,
                     StringComparison.Ordinal)))
            {
                return false;
            }

            try
            {
                long expectedTarget = result.OrderType ==
                    ProgressionOrderType.ResearchLevel
                    ? checked(result.PreviousValue + 1)
                    : checked(result.PreviousValue + result.BatchCount);
                if (expectedTarget != result.TargetValue ||
                    (result.OrderType ==
                     ProgressionOrderType.ResearchLevel &&
                     (result.BatchCount != 0 ||
                      result.InventoryCapacityPolicy !=
                      TroopInventoryCapacityPolicy.Unresolved)) ||
                    (result.OrderType ==
                     ProgressionOrderType.TroopTrainingBatch &&
                     (result.BatchCount <= 0 ||
                      result.InventoryCapacityPolicy !=
                      TroopInventoryCapacityPolicy
                          .SeparatedCountsTotalCapacityV1)))
                {
                    return false;
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            if (result.OperationKind == ProgressionOperationKind.Start)
            {
                if (!string.Equals(
                        result.OperationId,
                        result.StartOperationId,
                        StringComparison.Ordinal) ||
                    result.QuestProgressAmount != 0 ||
                    !string.IsNullOrEmpty(result.QuestRevision) ||
                    result.CommitTimestamp != result.StartTimestamp ||
                    result.SourceDisposition !=
                    ProgressionOrderSourceDisposition.ExactCurrentSource ||
                    !string.Equals(
                        result.OrderCatalogSetId,
                        result.CatalogSetId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        result.OrderCatalogRevision,
                        result.CatalogRevision,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        result.OrderProgressionRevision,
                        result.ProgressionRevision,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        result.OrderEconomyRevision,
                        result.EconomyRevision,
                        StringComparison.Ordinal) ||
                    (!string.IsNullOrEmpty(result.CompletionOperationId) &&
                     (!IsValidId(result.CompletionOperationId) ||
                      string.Equals(
                          result.CompletionOperationId,
                          result.StartOperationId,
                          StringComparison.Ordinal))) ||
                    (!string.IsNullOrEmpty(result.OrderHash) &&
                     !IsHash(result.OrderHash)))
                {
                    return false;
                }

                ProgressionStartPlan startPlan =
                    BuildCommittedStartPlan(result);
                if (!IsValidReadyStartPlan(startPlan))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(result.OrderHash))
                {
                    return string.IsNullOrEmpty(
                               result.CompletionOperationId) &&
                           string.IsNullOrEmpty(
                               result.CancellationOperationId);
                }

                ProgressionOrderSnapshot committedStartOrder =
                    BuildCommittedOrderSnapshot(result);
                return committedStartOrder != null &&
                       IsOrderForStartPlan(
                           committedStartOrder,
                           startPlan);
            }

            if (!IsValidId(result.CompletionOperationId) ||
                !string.Equals(
                    result.OperationId,
                    result.CompletionOperationId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    result.StartOperationId,
                    result.CompletionOperationId,
                    StringComparison.Ordinal) ||
                !IsValidId(result.QuestRevision) ||
                !IsHash(result.OrderHash) ||
                result.CommitTimestamp < result.EndTimestamp ||
                result.CommitTimestamp - result.EndTimestamp >
                result.TimestampPolicy.MaximumRetentionAgeSeconds ||
                (result.SourceDisposition ==
                 ProgressionOrderSourceDisposition.MigrationRequired) ||
                (result.SourceDisposition ==
                 ProgressionOrderSourceDisposition.UnsupportedVersion))
            {
                return false;
            }

            ProgressionOrderSnapshot committedOrder =
                BuildCommittedOrderSnapshot(result);
            return committedOrder != null &&
                   IsValidReadyCompletionPlan(
                       BuildCommittedCompletionPlan(
                           result,
                           committedOrder));
        }

        private static bool IsValidCompletionRequest(
            ProgressionCompletionRequest request)
        {
            return request != null &&
                   IsValidId(request.ProfileId) &&
                   IsValidId(request.OrderId) &&
                   IsValidId(request.OperationId) &&
                   IsValidId(request.ExpectedCatalogSetId) &&
                   IsValidId(request.ExpectedProgressionRevision) &&
                   IsValidId(request.ExpectedEconomyRevision) &&
                   IsValidId(request.ExpectedQuestRevision) &&
                   IsValidId(request.CompletionPolicyVersion) &&
                   !string.Equals(
                       request.OrderId,
                       request.OperationId,
                       StringComparison.Ordinal);
        }

        private static bool IsValidTimestampPolicy(
            ProgressionTimestampPolicy policy)
        {
            if (policy == null ||
                !IsValidId(policy.PolicyVersion) ||
                policy.MinimumUtcTimestamp <= 0 ||
                policy.MaximumUtcTimestamp < policy.MinimumUtcTimestamp ||
                policy.MaximumRetentionAgeSeconds < 0 ||
                policy.MaximumFutureLeadSeconds < 0)
            {
                return false;
            }

            long totalWindow =
                policy.MaximumUtcTimestamp - policy.MinimumUtcTimestamp;
            return policy.MaximumRetentionAgeSeconds <= totalWindow &&
                   policy.MaximumFutureLeadSeconds <= totalWindow;
        }

        private static bool IsTimestampWithinAbsolutePolicy(
            long timestamp,
            ProgressionTimestampPolicy policy)
        {
            return IsValidTimestampPolicy(policy) &&
                   timestamp >= policy.MinimumUtcTimestamp &&
                   timestamp <= policy.MaximumUtcTimestamp;
        }

        private static ProgressionPlanStatus EvaluateOrderClock(
            ProgressionOrderSnapshot order,
            long observedUtcTimestamp)
        {
            if (order == null ||
                !IsTimestampWithinAbsolutePolicy(
                    observedUtcTimestamp,
                    order.TimestampPolicy) ||
                observedUtcTimestamp < order.StartTimestamp)
            {
                return ProgressionPlanStatus.ClockInvalid;
            }

            if (observedUtcTimestamp < order.EndTimestamp)
            {
                return order.EndTimestamp - observedUtcTimestamp <=
                       order.TimestampPolicy.MaximumFutureLeadSeconds
                    ? ProgressionPlanStatus.NotYetEligible
                    : ProgressionPlanStatus.ClockInvalid;
            }

            return observedUtcTimestamp - order.EndTimestamp <=
                   order.TimestampPolicy.MaximumRetentionAgeSeconds
                ? ProgressionPlanStatus.Ready
                : ProgressionPlanStatus.ClockInvalid;
        }

        private static void AddTimestampPolicyHashSegments(
            ICollection<string> segments,
            ProgressionTimestampPolicy policy)
        {
            segments.Add("timestamp-policy");
            segments.Add(policy?.PolicyVersion);
            segments.Add(Invariant(policy?.MinimumUtcTimestamp ?? 0));
            segments.Add(Invariant(policy?.MaximumUtcTimestamp ?? 0));
            segments.Add(Invariant(
                policy?.MaximumRetentionAgeSeconds ?? -1));
            segments.Add(Invariant(
                policy?.MaximumFutureLeadSeconds ?? -1));
        }

        private static bool TryResolveCompletionStateAndSource(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order,
            out long currentValue,
            out long questProgressAmount,
            out ProgressionOrderSourceDisposition sourceDisposition,
            out ProgressionPlanStatus failureStatus,
            out ProgressionDiagnosticCode failureDiagnostic)
        {
            currentValue = 0;
            questProgressAmount = 0;
            sourceDisposition =
                ProgressionOrderSourceDisposition.ExactCurrentSource;
            failureStatus = ProgressionPlanStatus.StateMalformed;
            failureDiagnostic = ProgressionDiagnosticCode.StateUnavailable;

            ProgressionDomain expectedDomain = order.OrderType ==
                ProgressionOrderType.TroopTrainingBatch
                ? ProgressionDomain.Training
                : ProgressionDomain.Research;
            if (compatibility == null ||
                compatibility.Domain != expectedDomain ||
                !IsValidId(compatibility.CatalogSetId) ||
                !IsValidId(compatibility.CatalogRevision) ||
                !IsHash(compatibility.StateRevision) ||
                !string.Equals(
                    compatibility.CatalogSetId,
                    order.CatalogSetId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!compatibility.HasDefinitionSource)
            {
                failureStatus = ProgressionPlanStatus.DefinitionUnavailable;
                return false;
            }

            if ((expectedDomain == ProgressionDomain.Research &&
                 compatibility.ResearchDefinitions.Count == 0) ||
                (expectedDomain == ProgressionDomain.Training &&
                 compatibility.TroopDefinitions.Count == 0))
            {
                return false;
            }

            if (compatibility.ResearchDefinitions.Count >
                    ProgressionCompatibilityPlanner.MaximumDefinitions ||
                compatibility.TroopDefinitions.Count >
                    ProgressionCompatibilityPlanner.MaximumDefinitions ||
                compatibility.PreservedResearchStates.Count >
                    ProgressionCompatibilityPlanner.MaximumStateRows ||
                compatibility.PreservedTroopStates.Count >
                    ProgressionCompatibilityPlanner.MaximumStateRows)
            {
                return false;
            }

            if (expectedDomain == ProgressionDomain.Research)
            {
                return TryResolveResearchCompletion(
                    compatibility,
                    order,
                    out currentValue,
                    out questProgressAmount,
                    out sourceDisposition,
                    out failureStatus,
                    out failureDiagnostic);
            }

            return TryResolveTrainingCompletion(
                compatibility,
                order,
                out currentValue,
                out questProgressAmount,
                out sourceDisposition,
                out failureStatus,
                out failureDiagnostic);
        }

        private static bool TryResolveResearchCompletion(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order,
            out long currentValue,
            out long questProgressAmount,
            out ProgressionOrderSourceDisposition sourceDisposition,
            out ProgressionPlanStatus failureStatus,
            out ProgressionDiagnosticCode failureDiagnostic)
        {
            currentValue = 0;
            questProgressAmount = 0;
            sourceDisposition =
                ProgressionOrderSourceDisposition.ExactCurrentSource;
            failureStatus = ProgressionPlanStatus.StateMalformed;
            failureDiagnostic = ProgressionDiagnosticCode.StateUnavailable;

            if (compatibility.Troops.Count != 0 ||
                compatibility.TroopDefinitions.Count != 0 ||
                compatibility.PreservedTroopStates.Count != 0 ||
                !IsValidTimestampPolicy(compatibility.TimestampPolicy) ||
                compatibility.Research.Count >
                ProgressionCompatibilityPlanner.MaximumDefinitions ||
                compatibility.Research.Any(snapshot =>
                    !IsValidResearchSnapshot(snapshot)) ||
                compatibility.Research
                    .GroupBy(
                        snapshot => snapshot.Definition.Identity.Id,
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1) ||
                compatibility.ResearchDefinitions.Any(definition =>
                    definition == null ||
                    definition.Identity == null ||
                    !IsValidId(definition.Identity.Id)) ||
                compatibility.ResearchDefinitions.Any(definition =>
                    !string.Equals(
                        definition.Identity.Id,
                        order.DefinitionId,
                        StringComparison.Ordinal) &&
                    !IsValidResearchSnapshotDefinition(definition)) ||
                compatibility.ResearchDefinitions
                    .GroupBy(
                        definition => definition.Identity.Id,
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1) ||
                compatibility.PreservedResearchStates.Any(state =>
                    !IsValidResearchStateRecord(
                        state,
                        compatibility.TimestampPolicy)) ||
                compatibility.PreservedResearchStates
                    .GroupBy(state => state.DefinitionId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1) ||
                !HasConsistentResearchCompletionRows(
                    compatibility,
                    order.DefinitionId))
            {
                return false;
            }

            ResearchProgressionDefinition currentDefinition =
                compatibility.ResearchDefinitions.SingleOrDefault(definition =>
                    string.Equals(
                        definition.Identity.Id,
                        order.DefinitionId,
                        StringComparison.Ordinal));
            if (!TryClassifyResearchSource(
                    compatibility,
                    order,
                    currentDefinition,
                    out sourceDisposition,
                    out failureStatus,
                    out failureDiagnostic))
            {
                return false;
            }

            List<ResearchProgressionSnapshot> snapshots =
                compatibility.Research
                    .Where(snapshot =>
                        snapshot?.Definition?.Identity != null &&
                        string.Equals(
                            snapshot.Definition.Identity.Id,
                            order.DefinitionId,
                            StringComparison.Ordinal))
                    .ToList();
            if (snapshots.Count > 1)
            {
                return false;
            }

            if (snapshots.Count == 1)
            {
                ResearchProgressionSnapshot snapshot = snapshots[0];
                if (!IsValidResearchSnapshot(snapshot) ||
                    snapshot.Level != order.PreviousValue)
                {
                    return false;
                }

                currentValue = snapshot.Level;
            }
            else
            {
                List<ResearchProgressionStateRecord> preserved =
                    compatibility.PreservedResearchStates
                        .Where(state =>
                            state != null &&
                            string.Equals(
                                state.DefinitionId,
                                order.DefinitionId,
                                StringComparison.Ordinal))
                        .ToList();
                if (preserved.Count != 1)
                {
                    return false;
                }

                ResearchProgressionStateRecord state = preserved[0];
                if (!IsValidId(state.DefinitionId) ||
                    !IsValidId(state.DefinitionContentVersion) ||
                    !string.Equals(
                        state.DefinitionContentVersion,
                        order.DefinitionContentVersion,
                        StringComparison.Ordinal))
                {
                    failureStatus = ProgressionPlanStatus.MigrationRequired;
                    failureDiagnostic =
                        ProgressionDiagnosticCode.SourceMigrationRequired;
                    return false;
                }

                if (state.Level < 0 ||
                    state.Level != order.PreviousValue ||
                    state.HasActiveLegacyOrder ||
                    state.CompletionTimestamp != 0)
                {
                    return false;
                }

                currentValue = state.Level;
            }

            if (order.TargetValue != currentValue + 1 ||
                order.TargetValue > order.MaximumValue)
            {
                return false;
            }

            questProgressAmount = 1;
            return true;
        }

        private static bool TryResolveTrainingCompletion(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order,
            out long currentValue,
            out long questProgressAmount,
            out ProgressionOrderSourceDisposition sourceDisposition,
            out ProgressionPlanStatus failureStatus,
            out ProgressionDiagnosticCode failureDiagnostic)
        {
            currentValue = 0;
            questProgressAmount = 0;
            sourceDisposition =
                ProgressionOrderSourceDisposition.ExactCurrentSource;
            failureStatus = ProgressionPlanStatus.StateMalformed;
            failureDiagnostic = ProgressionDiagnosticCode.StateUnavailable;

            if (compatibility.Research.Count != 0 ||
                compatibility.ResearchDefinitions.Count != 0 ||
                compatibility.PreservedResearchStates.Count != 0 ||
                !IsValidTimestampPolicy(compatibility.TimestampPolicy) ||
                compatibility.Troops.Count >
                ProgressionCompatibilityPlanner.MaximumDefinitions ||
                compatibility.Troops.Any(snapshot =>
                    !IsValidTroopSnapshot(snapshot)) ||
                compatibility.Troops
                    .GroupBy(
                        snapshot => snapshot.Definition.Identity.Id,
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1) ||
                compatibility.TroopDefinitions.Any(definition =>
                    definition == null ||
                    definition.Identity == null ||
                    !IsValidId(definition.Identity.Id)) ||
                compatibility.TroopDefinitions.Any(definition =>
                    !string.Equals(
                        definition.Identity.Id,
                        order.DefinitionId,
                        StringComparison.Ordinal) &&
                    !IsValidTroopSnapshotDefinition(definition)) ||
                compatibility.TroopDefinitions
                    .GroupBy(
                        definition => definition.Identity.Id,
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1) ||
                compatibility.PreservedTroopStates.Any(state =>
                    !IsValidTroopStateRecord(state)) ||
                compatibility.PreservedTroopStates
                    .GroupBy(state => state.DefinitionId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1) ||
                !HasConsistentTroopCompletionRows(
                    compatibility,
                    order.DefinitionId))
            {
                return false;
            }

            TroopProgressionDefinition currentDefinition =
                compatibility.TroopDefinitions.SingleOrDefault(definition =>
                    string.Equals(
                        definition.Identity.Id,
                        order.DefinitionId,
                        StringComparison.Ordinal));
            if (!TryClassifyTrainingSource(
                    compatibility,
                    order,
                    currentDefinition,
                    out sourceDisposition,
                    out failureStatus,
                    out failureDiagnostic))
            {
                return false;
            }

            long wounded;
            long reserved;
            List<TroopProgressionSnapshot> snapshots =
                compatibility.Troops
                    .Where(snapshot =>
                        snapshot?.Definition?.Identity != null &&
                        string.Equals(
                            snapshot.Definition.Identity.Id,
                            order.DefinitionId,
                            StringComparison.Ordinal))
                    .ToList();
            if (snapshots.Count > 1)
            {
                return false;
            }

            if (snapshots.Count == 1)
            {
                TroopProgressionSnapshot snapshot = snapshots[0];
                if (!IsValidTroopSnapshot(snapshot) ||
                    snapshot.ActiveCount != order.PreviousValue)
                {
                    return false;
                }

                currentValue = snapshot.ActiveCount;
                wounded = snapshot.WoundedCount;
                reserved = snapshot.ReservedCount;
            }
            else
            {
                List<TroopProgressionStateRecord> preserved =
                    compatibility.PreservedTroopStates
                        .Where(state =>
                            state != null &&
                            string.Equals(
                                state.DefinitionId,
                                order.DefinitionId,
                                StringComparison.Ordinal))
                        .ToList();
                if (preserved.Count != 1)
                {
                    return false;
                }

                TroopProgressionStateRecord state = preserved[0];
                if (!IsValidId(state.DefinitionId) ||
                    !IsValidId(state.DefinitionContentVersion) ||
                    !string.Equals(
                        state.DefinitionContentVersion,
                        order.DefinitionContentVersion,
                        StringComparison.Ordinal))
                {
                    failureStatus = ProgressionPlanStatus.MigrationRequired;
                    failureDiagnostic =
                        ProgressionDiagnosticCode.SourceMigrationRequired;
                    return false;
                }

                if (state.ActiveCount < 0 ||
                    state.WoundedCount < 0 ||
                    state.ReservedCount < 0 ||
                    state.ActiveCount != order.PreviousValue)
                {
                    return false;
                }

                currentValue = state.ActiveCount;
                wounded = state.WoundedCount;
                reserved = state.ReservedCount;
            }

            try
            {
                long expectedTarget = checked(currentValue + order.BatchCount);
                long expectedTotal = checked(
                    checked(expectedTarget + wounded) + reserved);
                if (order.BatchCount <= 0 ||
                    expectedTarget != order.TargetValue ||
                    expectedTotal > order.MaximumValue ||
                    order.InventoryCapacityPolicy !=
                    TroopInventoryCapacityPolicy
                        .SeparatedCountsTotalCapacityV1)
                {
                    failureStatus =
                        ProgressionPlanStatus.InventoryOverflow;
                    failureDiagnostic =
                        ProgressionDiagnosticCode.OverMaximumCount;
                    return false;
                }
            }
            catch (OverflowException)
            {
                failureStatus = ProgressionPlanStatus.InventoryOverflow;
                failureDiagnostic = ProgressionDiagnosticCode.CountOverflow;
                return false;
            }

            questProgressAmount = order.BatchCount;
            return true;
        }

        private static bool TryClassifyResearchSource(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order,
            ResearchProgressionDefinition currentDefinition,
            out ProgressionOrderSourceDisposition disposition,
            out ProgressionPlanStatus failureStatus,
            out ProgressionDiagnosticCode failureDiagnostic)
        {
            disposition =
                ProgressionOrderSourceDisposition.ExactCurrentSource;
            failureStatus = ProgressionPlanStatus.StateMalformed;
            failureDiagnostic = ProgressionDiagnosticCode.StateUnavailable;

            if (compatibility.Status ==
                ProgressionCompatibilityStatus.UnavailableCatalog)
            {
                if (HasUnsupportedResearchSourceSchema(currentDefinition))
                {
                    disposition =
                        ProgressionOrderSourceDisposition.UnsupportedVersion;
                    failureStatus = ProgressionPlanStatus.UnsupportedVersion;
                    failureDiagnostic =
                        ProgressionDiagnosticCode.UnsupportedSchemaVersion;
                }
                else
                {
                    failureStatus =
                        ProgressionPlanStatus.DefinitionUnavailable;
                }

                return false;
            }

            if (!HasOnlyTargetSourceDriftDiagnostics(
                    compatibility,
                    order.DefinitionId))
            {
                return false;
            }

            if (currentDefinition == null)
            {
                disposition = ProgressionOrderSourceDisposition
                    .DefinitionRemovedButLegacyOrderPreserved;
                return compatibility.PreservedResearchStates.Count(state =>
                    state != null &&
                    string.Equals(
                        state.DefinitionId,
                        order.DefinitionId,
                        StringComparison.Ordinal)) == 1;
            }

            if (!IsValidResearchSnapshotDefinition(currentDefinition))
            {
                if (HasUnsupportedResearchSourceSchema(currentDefinition))
                {
                    disposition =
                        ProgressionOrderSourceDisposition.UnsupportedVersion;
                    failureStatus = ProgressionPlanStatus.UnsupportedVersion;
                    failureDiagnostic =
                        ProgressionDiagnosticCode.UnsupportedSchemaVersion;
                }

                return false;
            }

            if (AreSameIdentity(
                    order.DefinitionSource,
                    currentDefinition.Identity) &&
                AreSameIdentity(
                    order.CostProfile,
                    currentDefinition.CostProfile.Identity) &&
                AreSameIdentity(
                    order.DurationProfile,
                    currentDefinition.DurationProfile.Identity) &&
                string.Equals(
                    compatibility.CatalogRevision,
                    order.CatalogRevision,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (HaveSameSchemaAndContentIdentity(
                    order.DefinitionSource,
                    currentDefinition.Identity) &&
                HaveSameSchemaAndContentIdentity(
                    order.CostProfile,
                    currentDefinition.CostProfile.Identity) &&
                HaveSameSchemaAndContentIdentity(
                    order.DurationProfile,
                    currentDefinition.DurationProfile.Identity))
            {
                disposition = ProgressionOrderSourceDisposition
                    .CompatibleCompleteUnderCommittedSnapshot;
                return true;
            }

            disposition =
                ProgressionOrderSourceDisposition.MigrationRequired;
            failureStatus = ProgressionPlanStatus.MigrationRequired;
            failureDiagnostic =
                ProgressionDiagnosticCode.SourceMigrationRequired;
            return false;
        }

        private static bool TryClassifyTrainingSource(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order,
            TroopProgressionDefinition currentDefinition,
            out ProgressionOrderSourceDisposition disposition,
            out ProgressionPlanStatus failureStatus,
            out ProgressionDiagnosticCode failureDiagnostic)
        {
            disposition =
                ProgressionOrderSourceDisposition.ExactCurrentSource;
            failureStatus = ProgressionPlanStatus.StateMalformed;
            failureDiagnostic = ProgressionDiagnosticCode.StateUnavailable;

            if (compatibility.Status ==
                ProgressionCompatibilityStatus.UnavailableCatalog)
            {
                if (HasUnsupportedTrainingSourceSchema(currentDefinition))
                {
                    disposition =
                        ProgressionOrderSourceDisposition.UnsupportedVersion;
                    failureStatus = ProgressionPlanStatus.UnsupportedVersion;
                    failureDiagnostic =
                        ProgressionDiagnosticCode.UnsupportedSchemaVersion;
                }
                else
                {
                    failureStatus =
                        ProgressionPlanStatus.DefinitionUnavailable;
                }

                return false;
            }

            if (!HasOnlyTargetSourceDriftDiagnostics(
                    compatibility,
                    order.DefinitionId))
            {
                return false;
            }

            if (currentDefinition == null)
            {
                disposition = ProgressionOrderSourceDisposition
                    .DefinitionRemovedButLegacyOrderPreserved;
                return compatibility.PreservedTroopStates.Count(state =>
                    state != null &&
                    string.Equals(
                        state.DefinitionId,
                        order.DefinitionId,
                        StringComparison.Ordinal)) == 1;
            }

            if (!IsValidTroopSnapshotDefinition(currentDefinition))
            {
                if (HasUnsupportedTrainingSourceSchema(currentDefinition))
                {
                    disposition =
                        ProgressionOrderSourceDisposition.UnsupportedVersion;
                    failureStatus = ProgressionPlanStatus.UnsupportedVersion;
                    failureDiagnostic =
                        ProgressionDiagnosticCode.UnsupportedSchemaVersion;
                }

                return false;
            }

            if (AreSameIdentity(
                    order.DefinitionSource,
                    currentDefinition.Identity) &&
                AreSameIdentity(
                    order.CostProfile,
                    currentDefinition.CostProfile.Identity) &&
                AreSameIdentity(
                    order.DurationProfile,
                    currentDefinition.DurationProfile.Identity) &&
                string.Equals(
                    compatibility.CatalogRevision,
                    order.CatalogRevision,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (HaveSameSchemaAndContentIdentity(
                    order.DefinitionSource,
                    currentDefinition.Identity) &&
                HaveSameSchemaAndContentIdentity(
                    order.CostProfile,
                    currentDefinition.CostProfile.Identity) &&
                HaveSameSchemaAndContentIdentity(
                    order.DurationProfile,
                    currentDefinition.DurationProfile.Identity))
            {
                disposition = ProgressionOrderSourceDisposition
                    .CompatibleCompleteUnderCommittedSnapshot;
                return true;
            }

            disposition =
                ProgressionOrderSourceDisposition.MigrationRequired;
            failureStatus = ProgressionPlanStatus.MigrationRequired;
            failureDiagnostic =
                ProgressionDiagnosticCode.SourceMigrationRequired;
            return false;
        }

        private static bool HasUnsupportedResearchSourceSchema(
            ResearchProgressionDefinition definition)
        {
            return HasUnsupportedSchema(
                       definition?.Identity,
                       ProgressionCompatibilityPlanner.ResearchSchemaVersion) ||
                   HasUnsupportedSchema(
                       definition?.CostProfile?.Identity,
                       ProgressionCompatibilityPlanner.ProfileSchemaVersion) ||
                   HasUnsupportedSchema(
                       definition?.DurationProfile?.Identity,
                       ProgressionCompatibilityPlanner.ProfileSchemaVersion);
        }

        private static bool HasUnsupportedTrainingSourceSchema(
            TroopProgressionDefinition definition)
        {
            return HasUnsupportedSchema(
                       definition?.Identity,
                       ProgressionCompatibilityPlanner.TroopSchemaVersion) ||
                   HasUnsupportedSchema(
                       definition?.CostProfile?.Identity,
                       ProgressionCompatibilityPlanner.ProfileSchemaVersion) ||
                   HasUnsupportedSchema(
                       definition?.DurationProfile?.Identity,
                       ProgressionCompatibilityPlanner.ProfileSchemaVersion);
        }

        private static bool HasUnsupportedSchema(
            ProgressionSourceIdentity identity,
            string expectedSchemaVersion)
        {
            return identity != null &&
                   IsValidId(identity.SchemaVersion) &&
                   !string.Equals(
                       identity.SchemaVersion,
                       expectedSchemaVersion,
                       StringComparison.Ordinal);
        }

        private static bool HasOnlyTargetSourceDriftDiagnostics(
            ProgressionCompatibilityResult compatibility,
            string definitionId)
        {
            if (compatibility.Status ==
                ProgressionCompatibilityStatus.Available)
            {
                return compatibility.Diagnostics.Count == 0;
            }

            return compatibility.Status ==
                   ProgressionCompatibilityStatus.MalformedState &&
                   compatibility.Diagnostics.Count > 0 &&
                   compatibility.Diagnostics.All(diagnostic =>
                       diagnostic != null &&
                       diagnostic.Domain == compatibility.Domain &&
                       string.Equals(
                           diagnostic.DefinitionId,
                           definitionId,
                           StringComparison.Ordinal) &&
                       (diagnostic.Code ==
                        ProgressionDiagnosticCode
                            .PreservedUnknownFutureDefinition ||
                        diagnostic.Code ==
                        ProgressionDiagnosticCode.UnsupportedContentVersion));
        }

        private static bool HaveSameSchemaAndContentIdentity(
            ProgressionSourceIdentity committed,
            ProgressionSourceIdentity current)
        {
            return committed != null &&
                   current != null &&
                   string.Equals(
                       committed.Id,
                       current.Id,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       committed.SchemaVersion,
                       current.SchemaVersion,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       committed.ContentVersion,
                       current.ContentVersion,
                       StringComparison.Ordinal);
        }

        private static bool IsValidResearchSnapshotDefinition(
            ResearchProgressionDefinition definition)
        {
            return definition != null &&
                   IsValidDefinitionIdentity(
                       definition.Identity,
                       ProgressionCompatibilityPlanner.ResearchSchemaVersion) &&
                   IsValidCostProfile(definition.CostProfile) &&
                   IsValidDurationProfile(
                       definition.DurationProfile,
                       false) &&
                   definition.InitialLevel >= 0 &&
                   definition.MaximumLevel >= definition.InitialLevel &&
                   IsValidPrerequisites(definition.Prerequisites) &&
                   definition.EffectProfiles.Count <=
                   ProgressionCompatibilityPlanner
                       .MaximumEffectsPerResearch &&
                   definition.EffectProfiles.All(IsValidProfileIdentity) &&
                   definition.EffectProfiles
                       .GroupBy(
                           identity => identity.Id,
                           StringComparer.Ordinal)
                       .All(group => group.Count() == 1);
        }

        private static bool IsValidTroopSnapshotDefinition(
            TroopProgressionDefinition definition)
        {
            return definition != null &&
                   IsValidDefinitionIdentity(
                       definition.Identity,
                       ProgressionCompatibilityPlanner.TroopSchemaVersion) &&
                   IsValidCostProfile(definition.CostProfile) &&
                   IsValidDurationProfile(
                       definition.DurationProfile,
                       true) &&
                   definition.MaximumInventoryCount > 0 &&
                   definition.MaximumBatchCount > 0 &&
                   definition.MaximumBatchCount <=
                   definition.MaximumInventoryCount &&
                   IsValidProfileIdentity(definition.BattleProfile) &&
                   IsValidProfileIdentity(definition.InventoryPolicy) &&
                   definition.InventoryCapacityPolicy ==
                   TroopInventoryCapacityPolicy
                       .SeparatedCountsTotalCapacityV1 &&
                   IsValidPrerequisites(definition.Prerequisites);
        }

        private static bool IsValidResearchStateRecord(
            ResearchProgressionStateRecord state,
            ProgressionTimestampPolicy timestampPolicy)
        {
            return state != null &&
                   IsValidId(state.DefinitionId) &&
                   IsValidId(state.DefinitionContentVersion) &&
                   state.Level >= 0 &&
                   (state.HasActiveLegacyOrder
                       ? IsTimestampWithinAbsolutePolicy(
                           state.CompletionTimestamp,
                           timestampPolicy)
                       : state.CompletionTimestamp == 0);
        }

        private static bool IsValidTroopStateRecord(
            TroopProgressionStateRecord state)
        {
            return state != null &&
                   IsValidId(state.DefinitionId) &&
                   IsValidId(state.DefinitionContentVersion) &&
                   state.ActiveCount >= 0 &&
                   state.WoundedCount >= 0 &&
                   state.ReservedCount >= 0;
        }

        private static IEnumerable<ProgressionDiagnostic> SortDiagnostics(
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            return diagnostics
                .OrderBy(diagnostic => diagnostic.Domain)
                .ThenBy(diagnostic => diagnostic.DefinitionId, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code)
                .ThenBy(diagnostic => diagnostic.SourceIndex);
        }

        private static void AddIdentityHashSegments(
            ICollection<string> segments,
            string label,
            ProgressionSourceIdentity identity)
        {
            segments.Add(label);
            segments.Add(identity?.Id);
            segments.Add(identity?.SchemaVersion);
            segments.Add(identity?.ContentVersion);
            segments.Add(identity?.SourceRevision);
            segments.Add(identity?.RawSha256);
        }

        private static bool IsValidId(string value)
        {
            return ProgressionText.IsValidIdentifier(value);
        }

        private static IEnumerable<string> EnumerateOperationIds(
            ProgressionOrderSnapshot order)
        {
            yield return order.StartOperationId;
            yield return order.CompletionOperationId;
            if (!string.IsNullOrEmpty(order.CancellationOperationId))
            {
                yield return order.CancellationOperationId;
            }
        }

        private static bool IsValidSourceIdentity(
            ProgressionSourceIdentity identity)
        {
            return identity != null &&
                   IsValidId(identity.Id) &&
                   IsValidId(identity.SchemaVersion) &&
                   IsValidId(identity.ContentVersion) &&
                   IsValidId(identity.SourceRevision) &&
                   IsHash(identity.RawSha256);
        }

        private static bool IsValidDefinitionIdentity(
            ProgressionSourceIdentity identity,
            string expectedSchemaVersion)
        {
            return IsValidSourceIdentity(identity) &&
                   string.Equals(
                       identity.SchemaVersion,
                       expectedSchemaVersion,
                       StringComparison.Ordinal);
        }

        private static bool IsValidProfileIdentity(
            ProgressionSourceIdentity identity)
        {
            return IsValidDefinitionIdentity(
                identity,
                ProgressionCompatibilityPlanner.ProfileSchemaVersion);
        }

        private static bool HaveSameCosts(
            IReadOnlyList<BuildingConstructionCost> left,
            IReadOnlyList<BuildingConstructionCost> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            BuildingConstructionCost[] orderedLeft = left
                .OrderBy(cost => cost.ResourceType)
                .ToArray();
            BuildingConstructionCost[] orderedRight = right
                .OrderBy(cost => cost.ResourceType)
                .ToArray();
            for (int index = 0; index < orderedLeft.Length; index++)
            {
                if (orderedLeft[index].ResourceType !=
                    orderedRight[index].ResourceType ||
                    orderedLeft[index].Amount != orderedRight[index].Amount)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HaveSameTimestampPolicy(
            ProgressionTimestampPolicy left,
            ProgressionTimestampPolicy right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(
                       left.PolicyVersion,
                       right.PolicyVersion,
                       StringComparison.Ordinal) &&
                   left.MinimumUtcTimestamp == right.MinimumUtcTimestamp &&
                   left.MaximumUtcTimestamp == right.MaximumUtcTimestamp &&
                   left.MaximumRetentionAgeSeconds ==
                   right.MaximumRetentionAgeSeconds &&
                   left.MaximumFutureLeadSeconds ==
                   right.MaximumFutureLeadSeconds;
        }

        private static bool AreSameIdentity(
            ProgressionSourceIdentity left,
            ProgressionSourceIdentity right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
                   string.Equals(
                       left.SchemaVersion,
                       right.SchemaVersion,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.ContentVersion,
                       right.ContentVersion,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.SourceRevision,
                       right.SourceRevision,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.RawSha256,
                       right.RawSha256,
                       StringComparison.Ordinal);
        }

        private static string Invariant(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsHash(string value)
        {
            return value != null &&
                   value.Length == 64 &&
                   value.All(character =>
                       (character >= '0' && character <= '9') ||
                       (character >= 'a' && character <= 'f'));
        }

        private static List<T> CopyBounded<T>(
            IEnumerable<T> source,
            int maximumCount,
            out bool limitExceeded)
        {
            var values = new List<T>();
            limitExceeded = false;
            if (source == null)
            {
                return values;
            }

            foreach (T value in source)
            {
                if (values.Count == maximumCount)
                {
                    limitExceeded = true;
                    break;
                }

                values.Add(value);
            }

            return values;
        }
    }
}
