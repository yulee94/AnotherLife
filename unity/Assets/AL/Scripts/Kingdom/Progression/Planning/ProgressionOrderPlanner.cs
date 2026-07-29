using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
            if (!IsValidId(operationId) ||
                !IsHash(semanticHash) ||
                !IsValidId(expectedRevision) ||
                !IsValidId(currentRevision) ||
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
                receiptList.Any(receipt =>
                    receipt == null ||
                    !IsValidId(receipt.OperationId) ||
                    !IsHash(receipt.SemanticHash) ||
                    !Enum.IsDefined(
                        typeof(ProgressionOperationDurability),
                        receipt.Durability) ||
                    (receipt.Durability ==
                     ProgressionOperationDurability.Committed &&
                     !IsHash(receipt.ResultHash)) ||
                    (receipt.Durability ==
                     ProgressionOperationDurability.CommitUncertain &&
                     !string.IsNullOrEmpty(receipt.ResultHash) &&
                     !IsHash(receipt.ResultHash))) ||
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

            if (!string.Equals(
                    expectedRevision,
                    currentRevision,
                    StringComparison.Ordinal))
            {
                return new ProgressionReplayClassification(
                    ProgressionReplayDisposition.StaleExpectedRevision,
                    null);
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
                plan.ProgressionRevision,
                plan.EconomyRevision,
                plan.RequestPolicyVersion);
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
                orderHash);
            return IsValidOrder(order) ? order : null;
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
            string semanticHash = BuildStartSemanticHash(request);
            if (!IsValidStartRequest(
                    request,
                    ProgressionOrderType.ResearchLevel,
                    semanticHash) ||
                compatibility == null)
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidRequest,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Research);
            }

            ProgressionStartPlan replay = ReplayStart(
                compatibility,
                request,
                semanticHash,
                receipts,
                ProgressionDomain.Research);
            if (replay != null)
            {
                return replay;
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
            string semanticHash = BuildStartSemanticHash(request);
            if (!IsValidStartRequest(
                    request,
                    ProgressionOrderType.TroopTrainingBatch,
                    semanticHash) ||
                compatibility == null)
            {
                return RejectStart(
                    ProgressionPlanStatus.InvalidRequest,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InvalidRequest,
                    ProgressionDomain.Training);
            }

            ProgressionStartPlan replay = ReplayStart(
                compatibility,
                request,
                semanticHash,
                receipts,
                ProgressionDomain.Training);
            if (replay != null)
            {
                return replay;
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
            string semanticHash = BuildCompletionSemanticHash(order, request);

            if (compatibility == null ||
                order == null ||
                request == null ||
                !IsHash(semanticHash) ||
                !IsValidId(request.ProfileId) ||
                !IsValidId(request.OrderId) ||
                !IsValidId(request.OperationId) ||
                !IsValidId(request.ExpectedCatalogSetId) ||
                !IsValidId(request.ExpectedProgressionRevision) ||
                !IsValidId(request.ExpectedEconomyRevision) ||
                !IsValidId(request.ExpectedQuestRevision) ||
                !IsValidId(request.CompletionPolicyVersion) ||
                dependencies == null ||
                !IsValidId(dependencies.EconomyRevision) ||
                !IsValidId(dependencies.QuestRevision))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.InvalidRequest,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.InvalidRequest,
                    domain);
            }

            if (!IsValidOrder(order))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.OrderMalformed,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.OrderMalformed,
                    domain);
            }

            ProgressionReplayClassification replay = ClassifyReplay(
                request.OperationId,
                semanticHash,
                request.ExpectedProgressionRevision,
                compatibility.StateRevision,
                receipts);
            ProgressionCompletionPlan replayPlan = ReplayCompletion(
                replay,
                order,
                request,
                semanticHash,
                compatibility,
                domain);
            if (replayPlan != null)
            {
                return replayPlan;
            }

            if (compatibility.Status != ProgressionCompatibilityStatus.Available ||
                (domain == ProgressionDomain.Research &&
                 compatibility.Domain != ProgressionDomain.Research) ||
                (domain == ProgressionDomain.Training &&
                 compatibility.Domain != ProgressionDomain.Training))
            {
                return RejectCompletion(
                    ProgressionPlanStatus.StateMalformed,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.StateUnavailable,
                    domain);
            }

            if (!IsValidAvailableCompatibility(compatibility, domain) ||
                !string.Equals(order.ProfileId, request.ProfileId, StringComparison.Ordinal) ||
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

            if (observedUtcTimestamp <= 0)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.ClockInvalid,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ClockInvalid,
                    domain);
            }

            if (observedUtcTimestamp < order.StartTimestamp)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.ClockInvalid,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.ClockInvalid,
                    domain);
            }

            if (observedUtcTimestamp < order.EndTimestamp)
            {
                return RejectCompletion(
                    ProgressionPlanStatus.NotYetEligible,
                    order,
                    request,
                    semanticHash,
                    ProgressionDiagnosticCode.NotYetEligible,
                    domain);
            }

            long currentValue;
            long questProgressAmount;
            if (domain == ProgressionDomain.Research)
            {
                ResearchProgressionSnapshot snapshot =
                    compatibility.Research.SingleOrDefault(candidate =>
                        string.Equals(
                            candidate.Definition.Identity.Id,
                            order.DefinitionId,
                            StringComparison.Ordinal));
                if (snapshot == null ||
                    !IsOrderSourceBound(
                        order,
                        snapshot.Definition.Identity,
                        snapshot.Definition.CostProfile,
                        snapshot.Definition.DurationProfile,
                        order.TargetValue) ||
                    !string.Equals(
                        snapshot.Definition.Identity.ContentVersion,
                        order.DefinitionContentVersion,
                        StringComparison.Ordinal) ||
                    snapshot.Level != order.PreviousValue ||
                    order.TargetValue != (long)snapshot.Level + 1 ||
                    order.TargetValue > snapshot.Definition.MaximumLevel)
                {
                    return RejectCompletion(
                        ProgressionPlanStatus.StateMalformed,
                        order,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.OrderMalformed,
                        domain);
                }

                currentValue = snapshot.Level;
                questProgressAmount = 1;
            }
            else
            {
                TroopProgressionSnapshot snapshot =
                    compatibility.Troops.SingleOrDefault(candidate =>
                        string.Equals(
                            candidate.Definition.Identity.Id,
                            order.DefinitionId,
                            StringComparison.Ordinal));
                if (snapshot == null ||
                    order.BatchCount >
                    snapshot.Definition.MaximumBatchCount ||
                    snapshot.Definition.InventoryCapacityPolicy !=
                    TroopInventoryCapacityPolicy.SeparatedCountsTotalCapacityV1 ||
                    !IsOrderSourceBound(
                        order,
                        snapshot.Definition.Identity,
                        snapshot.Definition.CostProfile,
                        snapshot.Definition.DurationProfile,
                        order.BatchCount) ||
                    !string.Equals(
                        snapshot.Definition.Identity.ContentVersion,
                        order.DefinitionContentVersion,
                        StringComparison.Ordinal) ||
                    snapshot.ActiveCount != order.PreviousValue ||
                    order.BatchCount <= 0)
                {
                    return RejectCompletion(
                        ProgressionPlanStatus.StateMalformed,
                        order,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.OrderMalformed,
                        domain);
                }

                try
                {
                    long expectedTarget = checked(
                        snapshot.ActiveCount + order.BatchCount);
                    long expectedTotal = checked(
                        checked(expectedTarget + snapshot.WoundedCount) +
                        snapshot.ReservedCount);
                    if (expectedTarget != order.TargetValue ||
                        expectedTotal >
                        snapshot.Definition.MaximumInventoryCount)
                    {
                        return RejectCompletion(
                            ProgressionPlanStatus.InventoryOverflow,
                            order,
                            request,
                            semanticHash,
                            ProgressionDiagnosticCode.OverMaximumCount,
                            domain);
                    }
                }
                catch (OverflowException)
                {
                    return RejectCompletion(
                        ProgressionPlanStatus.InventoryOverflow,
                        order,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.CountOverflow,
                        domain);
                }

                currentValue = snapshot.ActiveCount;
                questProgressAmount = order.BatchCount;
            }

            string planHash = ProgressionContractHash.Compute(
                "completion",
                semanticHash,
                order.OrderHash,
                compatibility.StateRevision,
                Invariant(currentValue),
                Invariant(order.TargetValue),
                Invariant(questProgressAmount),
                dependencies.EconomyRevision,
                dependencies.QuestRevision,
                request.CompletionPolicyVersion);
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
                Array.Empty<ProgressionDiagnostic>());
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

            int rollbackIndex = orderList.FindIndex(order =>
                order.State == ProgressionOrderState.Active &&
                observedUtcTimestamp < order.StartTimestamp);
            if (rollbackIndex >= 0)
            {
                ProgressionOrderSnapshot rollbackOrder =
                    orderList[rollbackIndex];
                return new ProgressionReconciliationPlan(
                    ProgressionPlanStatus.ClockInvalid,
                    Array.Empty<ProgressionOrderSnapshot>(),
                    string.Empty,
                    new[]
                    {
                        new ProgressionDiagnostic(
                            ProgressionDiagnosticCode.ClockInvalid,
                            rollbackOrder.OrderType ==
                            ProgressionOrderType.TroopTrainingBatch
                                ? ProgressionDomain.Training
                                : ProgressionDomain.Research,
                            rollbackOrder.DefinitionId,
                            rollbackIndex)
                    });
            }

            List<ProgressionOrderSnapshot> eligible = orderList
                .Where(order =>
                    order.State == ProgressionOrderState.Active &&
                    observedUtcTimestamp >= order.EndTimestamp)
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
                hashSegments.Add(order.OrderType.ToString());
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
                        snapshot.Definition.Identity.Id,
                        snapshot.Definition.Identity.ContentVersion,
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
                hashSegments.Add(effect.ResearchDefinitionId);
                hashSegments.Add(effect.ResearchContentVersion);
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
            ProgressionCompatibilityResult compatibility,
            ProgressionStartRequest request,
            string semanticHash,
            IEnumerable<ProgressionOperationReceipt> receipts,
            ProgressionDomain domain)
        {
            ProgressionReplayClassification replay = ClassifyReplay(
                         request.OperationId,
                semanticHash,
                request.ExpectedProgressionRevision,
                compatibility.StateRevision,
                receipts);
            switch (replay.Disposition)
            {
                case ProgressionReplayDisposition.NoPriorOperation:
                    return null;
                case ProgressionReplayDisposition.ExactCommittedReplay:
                    return new ProgressionStartPlan(
                        ProgressionPlanStatus.AlreadyCommitted,
                        request.OrderType,
                        request.ProfileId,
                        request.DefinitionId,
                        string.Empty,
                        null,
                        null,
                        null,
                        request.OrderId,
                        request.OperationId,
                        0,
                        0,
                        0,
                        Array.Empty<BuildingConstructionCost>(),
                        0,
                        0,
                        request.ExpectedCatalogSetId,
                        request.ExpectedProgressionRevision,
                        request.ExpectedEconomyRevision,
                        request.RequestPolicyVersion,
                        semanticHash,
                        replay.Receipt.ResultHash,
                        Array.Empty<ProgressionDiagnostic>());
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
            ProgressionOrderSnapshot order,
            ProgressionCompletionRequest request,
            string semanticHash,
            ProgressionCompatibilityResult compatibility,
            ProgressionDomain domain)
        {
            switch (replay.Disposition)
            {
                case ProgressionReplayDisposition.NoPriorOperation:
                    return null;
                case ProgressionReplayDisposition.ExactCommittedReplay:
                    return new ProgressionCompletionPlan(
                        ProgressionPlanStatus.AlreadyCommitted,
                        order.OrderType,
                        order.DefinitionId,
                        order.OrderId,
                        request.OperationId,
                        order.TargetValue,
                        order.TargetValue,
                        0,
                        request.ExpectedCatalogSetId,
                        request.ExpectedProgressionRevision,
                        request.ExpectedEconomyRevision,
                        request.ExpectedQuestRevision,
                        request.CompletionPolicyVersion,
                        semanticHash,
                        replay.Receipt.ResultHash,
                        Array.Empty<ProgressionDiagnostic>());
                case ProgressionReplayDisposition.PayloadConflict:
                    return RejectCompletion(
                        ProgressionPlanStatus.CorrelationConflict,
                        order,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.CorrelationConflict,
                        domain);
                case ProgressionReplayDisposition.StaleExpectedRevision:
                    return RejectCompletion(
                        ProgressionPlanStatus.StaleProgressionRevision,
                        order,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.StaleProgressionRevision,
                        domain);
                case ProgressionReplayDisposition.CommitUncertain:
                    return RejectCompletion(
                        ProgressionPlanStatus.CommitUncertain,
                        order,
                        request,
                        semanticHash,
                        ProgressionDiagnosticCode.CommitUncertain,
                        domain);
                default:
                    return RejectCompletion(
                        ProgressionPlanStatus.RecoveryRequired,
                        order,
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
            if (required.Count == 0)
            {
                return null;
            }

            if (available == null ||
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
            long observedUtcTimestamp,
            ProgressionDomain domain)
        {
            if (observedUtcTimestamp <= 0)
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
            hashSegments.Add(Invariant(observedUtcTimestamp));
            hashSegments.Add(Invariant(endTimestamp));
            hashSegments.Add(compatibility.CatalogSetId);
            hashSegments.Add(compatibility.CatalogRevision);
            hashSegments.Add(compatibility.StateRevision);
            hashSegments.Add(economy.Revision);
            foreach (BuildingConstructionCost cost in orderedCosts)
            {
                hashSegments.Add("cost");
                hashSegments.Add(cost.ResourceType.ToString());
                hashSegments.Add(Invariant(cost.Amount));
            }
            string planHash = ProgressionContractHash.Compute(
                hashSegments.ToArray());
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
                Array.Empty<ProgressionDiagnostic>());
        }

        private static bool IsValidStartRequest(
            ProgressionStartRequest request,
            ProgressionOrderType expectedType,
            string semanticHash)
        {
            return request != null &&
                   request.OrderType == expectedType &&
                   IsHash(semanticHash) &&
                   IsValidId(request.ProfileId) &&
                   IsValidId(request.DefinitionId) &&
                   IsValidId(request.OrderId) &&
                   IsValidId(request.OperationId) &&
                   IsValidId(request.ExpectedCatalogSetId) &&
                   IsValidId(request.ExpectedProgressionRevision) &&
                   IsValidId(request.ExpectedEconomyRevision) &&
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
                !IsValidId(order.ProgressionRevision) ||
                !IsValidId(order.EconomyRevision) ||
                !IsValidId(order.RequestPolicyVersion) ||
                !IsHash(order.OrderHash) ||
                order.PreviousValue < 0 ||
                order.TargetValue <= order.PreviousValue ||
                order.StartTimestamp <= 0 ||
                order.EndTimestamp < order.StartTimestamp)
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
                     order.BatchCount != 0) ||
                    (order.OrderType ==
                     ProgressionOrderType.TroopTrainingBatch &&
                     order.BatchCount <= 0) ||
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
                    order.ProgressionRevision,
                    order.EconomyRevision,
                    order.RequestPolicyVersion),
                StringComparison.Ordinal);
        }

        private static bool IsOrderSourceBound(
            ProgressionOrderSnapshot order,
            ProgressionSourceIdentity definitionIdentity,
            ProgressionCostProfile costProfile,
            ProgressionDurationProfile durationProfile,
            long profileInput)
        {
            if (profileInput <= 0 ||
                !AreSameIdentity(order.DefinitionSource, definitionIdentity) ||
                !AreSameIdentity(order.CostProfile, costProfile?.Identity) ||
                !AreSameIdentity(order.DurationProfile, durationProfile?.Identity) ||
                costProfile == null ||
                durationProfile == null)
            {
                return false;
            }

            try
            {
                    long duration = checked(
                        durationProfile.UnitSeconds * profileInput);
                if (duration < 0 ||
                    duration > durationProfile.MaximumSeconds ||
                    checked(order.StartTimestamp + duration) !=
                    order.EndTimestamp)
                {
                    return false;
                }

                List<BuildingConstructionCost> expectedCosts =
                    costProfile.UnitCosts
                        .Select(unitCost => new BuildingConstructionCost(
                            unitCost.ResourceType,
                            checked(unitCost.Amount * profileInput)))
                        .OrderBy(cost => cost.ResourceType)
                        .ToList();
                if (expectedCosts.Any(cost =>
                        cost.Amount <= 0 ||
                        cost.Amount > costProfile.MaximumAmountPerResource))
                {
                    return false;
                }

                List<BuildingConstructionCost> actualCosts =
                    order.CommittedCosts
                        .OrderBy(cost => cost.ResourceType)
                        .ToList();
                return expectedCosts.Count == actualCosts.Count &&
                       expectedCosts.Zip(
                               actualCosts,
                               (expected, actual) =>
                                   expected.ResourceType == actual.ResourceType &&
                                   expected.Amount == actual.Amount)
                           .All(matches => matches);
            }
            catch (OverflowException)
            {
                return false;
            }
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
            string progressionRevision,
            string economyRevision,
            string requestPolicyVersion)
        {
            var segments = new List<string>
            {
                "progression-order",
                orderType.ToString(),
                profileId,
                definitionId,
                definitionContentVersion,
                definitionSource?.SchemaVersion,
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
                Invariant(startTimestamp),
                Invariant(endTimestamp),
                catalogSetId,
                progressionRevision,
                economyRevision,
                requestPolicyVersion
            };
            foreach (BuildingConstructionCost cost in
                     (committedCosts ?? Array.Empty<BuildingConstructionCost>())
                     .OrderBy(cost => cost.ResourceType))
            {
                segments.Add("cost");
                segments.Add(cost.ResourceType.ToString());
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
                    request.OrderType.ToString(),
                    request.DefinitionId,
                    request.OrderId,
                    request.OperationId,
                    Invariant(request.RequestedTargetLevel),
                    Invariant(request.RequestedBatchCount),
                    request.ExpectedCatalogSetId,
                    request.ExpectedProgressionRevision,
                    request.ExpectedEconomyRevision,
                    request.RequestPolicyVersion);
        }

        private static string BuildCompletionSemanticHash(
            ProgressionOrderSnapshot order,
            ProgressionCompletionRequest request)
        {
            return order == null || request == null
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
                    request.CompletionPolicyVersion,
                    order.OrderHash,
                    order.DefinitionId,
                    Invariant(order.TargetValue),
                    Invariant(order.BatchCount));
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
            ProgressionDomain domain)
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
                });
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
            if (string.IsNullOrWhiteSpace(value) ||
                Encoding.UTF8.GetByteCount(value) >
                ProgressionCompatibilityPlanner.MaximumIdUtf8Bytes ||
                value.Any(char.IsWhiteSpace))
            {
                return false;
            }

            return !value.Any(char.IsControl);
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
