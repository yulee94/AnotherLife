using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Runtime;
using AL.Services.Local;

namespace AL.RealmSelection
{
    internal static class WishgateDurableTransaction
    {
        internal static WishgateCommitResult ReplayOrReject(
            SaveGameData save,
            WishgateCommitRequest request)
        {
            if (save?.WishgateTransaction == null ||
                save.WishgateTransaction.Version <= 0)
            {
                return null;
            }

            WishgateTransactionState published = save.WishgateTransaction;
            if (string.Equals(published.LastOperationId, request.OperationId, StringComparison.Ordinal))
            {
                if (!string.Equals(published.LastEventId, request.EventId, StringComparison.Ordinal) ||
                    published.OperationDoesNotMatch(request.Operation))
                {
                    return Reject(
                        WishgateCommitStatus.RejectedConflict,
                        save,
                        WishgateCommitCodes.Collision);
                }

                return MapPublished(save, false, false, WishgateCommitStatus.Replayed);
            }

            return null;
        }

        internal static SaveCandidateMutationPreparation Prepare(
            SaveGameData candidate,
            WishgateCommitRequest request,
            string profileId,
            WishgateDurableDependencies dependencies)
        {
            if (candidate == null ||
                !string.Equals(candidate.ProfileId, profileId, StringComparison.Ordinal))
            {
                return SaveCandidateMutationPreparation.Rejected(
                    WishgateCommitCodes.ProfileMismatch);
            }

            if (dependencies == null || !dependencies.IsComplete)
            {
                return SaveCandidateMutationPreparation.Rejected(
                    WishgateCommitCodes.CatalogUnavailable);
            }

            WishgateTransactionSnapshot snapshot = ProjectSnapshot(candidate);
            WishgateTransactionRequest plannerRequest = ToPlannerRequest(request);
            RealmGemCustodySnapshot custody = ProjectCustody(
                candidate,
                dependencies.Catalog);
            var planner = new WishgateEntitlementPlanner(
                dependencies.Catalog,
                dependencies.Clock,
                dependencies.Authority);
            WishgatePlanningResult planned = planner.Plan(plannerRequest, snapshot, custody);
            if (planned.Status == WishgatePlanStatus.Duplicate)
            {
                return SaveCandidateMutationPreparation.Duplicate();
            }

            if (planned.Status == WishgatePlanStatus.NoChange)
            {
                return SaveCandidateMutationPreparation.Rejected(WishgateCommitCodes.NoChange);
            }

            if (!planned.IsPrepared)
            {
                return SaveCandidateMutationPreparation.Rejected(
                    FirstCode(planned) ?? WishgateCommitCodes.InvalidRequest);
            }

            WishgateTransactionPlan plan = planned.Plan;
            if (plan.RequiresRewardApplication)
            {
                if (!dependencies.Applicator.TryApply(
                        candidate,
                        plan.RewardApplication,
                        out string applyCode))
                {
                    return SaveCandidateMutationPreparation.Rejected(
                        string.IsNullOrEmpty(applyCode)
                            ? WishgateCommitCodes.RewardApplyFailed
                            : applyCode);
                }
            }

            WriteState(candidate, plan, request);
            WishgateTransactionSnapshot verified = ProjectSnapshot(candidate);
            if (!WishgateEntitlementPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    verified,
                    out WishgateVerifiedReceipt receipt))
            {
                return SaveCandidateMutationPreparation.Rejected(
                    WishgateCommitCodes.InvalidRequest);
            }

            candidate.WishgateTransaction.ReceiptHash = receipt.ReceiptHash;
            candidate.WishgateTransaction.PostCommitNotificationCorrelationId =
                receipt.PostCommitNotificationCorrelationId;
            return SaveCandidateMutationPreparation.Prepared();
        }

        internal static WishgateCommitResult MapPublished(
            SaveGameData save,
            bool mutationOccurred,
            bool persisted,
            WishgateCommitStatus committedStatus)
        {
            WishgateTransactionState state = save?.WishgateTransaction;
            WishgateEntitlementPhase phase = state == null
                ? WishgateEntitlementPhase.Unearned
                : (WishgateEntitlementPhase)state.Phase;
            bool isFinal =
                state != null &&
                phase == WishgateEntitlementPhase.Committed &&
                !string.IsNullOrEmpty(state.PostCommitNotificationCorrelationId);
            WishgateVerifiedReceipt receipt = null;
            if (isFinal)
            {
                WishgateTransitionRecord record = FindLastRecord(state);
                if (record != null)
                {
                    receipt = new WishgateVerifiedReceipt(
                        record,
                        state.Revision,
                        state.EntitlementRevision,
                        state.ReceiptHash);
                }
            }

            return new WishgateCommitResult(
                committedStatus,
                mutationOccurred,
                persisted,
                phase,
                state?.RewardId,
                state?.RewardApplicationId,
                state?.PostCommitNotificationCorrelationId,
                state?.ReceiptHash,
                committedStatus == WishgateCommitStatus.Replayed
                    ? WishgateCommitCodes.Replayed
                    : WishgateCommitCodes.Committed,
                receipt);
        }

        internal static WishgateCommitResult Reject(
            WishgateCommitStatus status,
            SaveGameData save,
            string code)
        {
            WishgateTransactionState state = save?.WishgateTransaction;
            return new WishgateCommitResult(
                status,
                false,
                false,
                state == null
                    ? WishgateEntitlementPhase.Unearned
                    : (WishgateEntitlementPhase)state.Phase,
                state?.RewardId,
                state?.RewardApplicationId,
                string.Empty,
                string.Empty,
                code,
                null);
        }

        internal static WishgateCommitResult MapRejectedCode(SaveGameData save, string code)
        {
            string diagnostic = code ?? WishgateCommitCodes.InvalidRequest;
            if (string.Equals(diagnostic, WishgateCommitCodes.NoChange, StringComparison.Ordinal) ||
                diagnostic.IndexOf("AL-WISHGATE-ALREADY-EARNED", StringComparison.Ordinal) >= 0 ||
                diagnostic.IndexOf("AL-WISHGATE-STAGE-ALREADY-COMPLETE", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.NoChange, save, WishgateCommitCodes.NoChange);
            }

            if (diagnostic.IndexOf("ELIGIBILITY", StringComparison.Ordinal) >= 0 ||
                diagnostic.IndexOf("STAGE-NOT-READY", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedIneligible, save, diagnostic);
            }

            if (diagnostic.IndexOf("UNAUTHORIZED", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedUnauthorized, save, diagnostic);
            }

            if (diagnostic.IndexOf("STALE", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedStale, save, diagnostic);
            }

            if (diagnostic.IndexOf("UNSUPPORTED", StringComparison.Ordinal) >= 0 ||
                diagnostic.IndexOf("AUTHORITY-ID-UNKNOWN", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedUnsupported, save, diagnostic);
            }

            if (diagnostic.IndexOf("UNAVAILABLE", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedUnavailable, save, diagnostic);
            }

            if (diagnostic.IndexOf("CORRUPT", StringComparison.Ordinal) >= 0 ||
                diagnostic.IndexOf("MALFORMED", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedCorrupt, save, diagnostic);
            }

            if (diagnostic.IndexOf("CONFLICT", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedConflict, save, diagnostic);
            }

            if (diagnostic.IndexOf("COMMIT-UNCERTAIN", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RecoveryRequired, save, WishgateCommitCodes.RecoveryRequired);
            }

            if (diagnostic.IndexOf("REWARD-APPLY", StringComparison.Ordinal) >= 0)
            {
                return Reject(WishgateCommitStatus.RejectedRewardApply, save, diagnostic);
            }

            return Reject(WishgateCommitStatus.RejectedPlanner, save, diagnostic);
        }

        internal static WishgateCommitResult MapPlannerRejection(
            WishgatePlanStatus status,
            SaveGameData save,
            string code)
        {
            return MapRejectedCode(save, code);
        }

        internal static WishgateTransactionSnapshot ProjectSnapshot(SaveGameData save)
        {
            WishgateTransactionState state = save?.WishgateTransaction;
            if (state == null || state.Version <= 0)
            {
                return new WishgateTransactionSnapshot(
                    WishgateSnapshotStatus.Available,
                    0,
                    new WishgateEntitlementState(
                        WishgateEntitlementPhase.Unearned,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        0,
                        0,
                        0,
                        0,
                        0,
                        true),
                    Array.Empty<WishgateTransitionRecord>(),
                    true);
            }

            if (state.Version > WishgateTransactionState.CurrentVersion)
            {
                return new WishgateTransactionSnapshot(
                    WishgateSnapshotStatus.Available,
                    state.Revision,
                    new WishgateEntitlementState(
                        WishgateEntitlementPhase.Unearned,
                        state.EntitlementId,
                        state.EarnReasonId,
                        state.RewardId,
                        state.RewardApplicationId,
                        state.EarnedUtcSeconds,
                        state.SelectedUtcSeconds,
                        state.AppliedUtcSeconds,
                        state.CommittedUtcSeconds,
                        state.EntitlementRevision,
                        false),
                    ReadRecords(state),
                    state.IsComplete);
            }

            var snapshotStatus = (WishgateSnapshotStatus)state.Status;
            if (!Enum.IsDefined(typeof(WishgateSnapshotStatus), snapshotStatus))
            {
                snapshotStatus = WishgateSnapshotStatus.Malformed;
            }

            return new WishgateTransactionSnapshot(
                snapshotStatus,
                state.Revision,
                new WishgateEntitlementState(
                    (WishgateEntitlementPhase)state.Phase,
                    state.EntitlementId,
                    state.EarnReasonId,
                    state.RewardId,
                    state.RewardApplicationId,
                    state.EarnedUtcSeconds,
                    state.SelectedUtcSeconds,
                    state.AppliedUtcSeconds,
                    state.CommittedUtcSeconds,
                    state.EntitlementRevision,
                    state.EntitlementIsSupported),
                ReadRecords(state),
                state.IsComplete);
        }

        internal static RealmGemCustodySnapshot ProjectCustody(
            SaveGameData save,
            RealmGemCatalogSnapshot catalog)
        {
            if (catalog == null)
            {
                return new RealmGemCustodySnapshot(
                    RealmGemCustodySnapshotStatus.Unavailable,
                    0,
                    Array.Empty<RealmGemCustodyRecord>());
            }

            var records = new List<RealmGemCustodyRecord>(catalog.Entries.Count);
            IList<RealmGemState> gems = save?.RealmGems;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                RealmGemCatalogEntry entry = catalog.Entries[i];
                RealmGemState gem = FindGem(gems, entry.Id);
                if (gem == null)
                {
                    continue;
                }

                records.Add(new RealmGemCustodyRecord(
                    entry.Id,
                    entry.HomeRealmId,
                    entry.HomeRealm,
                    entry.SaveSlotIndex,
                    ToCustodyState(gem),
                    gem.CarrierId ?? string.Empty,
                    gem.LastDroppedTimestamp,
                    1,
                    true));
            }

            return new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                1,
                records);
        }

        private static void WriteState(
            SaveGameData candidate,
            WishgateTransactionPlan plan,
            WishgateCommitRequest request)
        {
            WishgateEntitlementState entitlement = plan.CandidateEntitlement;
            string appliedId = candidate.WishgateTransaction?.AppliedRewardApplicationId
                ?? string.Empty;
            var state = new WishgateTransactionState
            {
                Version = WishgateTransactionState.CurrentVersion,
                Status = (int)WishgateSnapshotStatus.Available,
                Revision = plan.CandidateSnapshotRevision,
                Phase = (int)entitlement.Phase,
                EntitlementId = entitlement.EntitlementId,
                EarnReasonId = entitlement.EarnReasonId,
                RewardId = entitlement.RewardId,
                RewardApplicationId = entitlement.RewardApplicationId,
                EarnedUtcSeconds = entitlement.EarnedUtcSeconds,
                SelectedUtcSeconds = entitlement.SelectedUtcSeconds,
                AppliedUtcSeconds = entitlement.AppliedUtcSeconds,
                CommittedUtcSeconds = entitlement.CommittedUtcSeconds,
                EntitlementRevision = entitlement.Revision,
                EntitlementIsSupported = entitlement.IsSupported,
                IsComplete = true,
                LastOperationId = request.OperationId,
                LastEventId = request.EventId,
                LastRequestFingerprint = plan.RequestFingerprint,
                AppliedRewardApplicationId = appliedId,
                Records = new List<WishgateTransitionRecordState>()
            };
            IReadOnlyList<WishgateTransitionRecord> records = plan.CandidateTransitionRecords;
            for (int i = 0; i < records.Count; i++)
            {
                state.Records.Add(ToState(records[i]));
            }

            candidate.WishgateTransaction = state;
        }

        private static WishgateTransactionRequest ToPlannerRequest(WishgateCommitRequest request)
        {
            return new WishgateTransactionRequest(
                request.Operation,
                request.OperationId,
                request.EventId,
                request.CorrelationId,
                request.ActorId,
                request.EntitlementId,
                request.EarnReasonId,
                request.RewardId,
                request.RewardApplicationId,
                request.ObservedUtcSeconds,
                request.ExpectedSnapshotRevision,
                request.ExpectedEntitlementRevision,
                request.PriorReceipt);
        }

        private static IReadOnlyList<WishgateTransitionRecord> ReadRecords(
            WishgateTransactionState state)
        {
            if (state.Records == null || state.Records.Count == 0)
            {
                return Array.Empty<WishgateTransitionRecord>();
            }

            var records = new WishgateTransitionRecord[state.Records.Count];
            for (int i = 0; i < state.Records.Count; i++)
            {
                records[i] = FromState(state.Records[i]);
            }

            return records;
        }

        private static WishgateTransitionRecord FindLastRecord(WishgateTransactionState state)
        {
            if (state?.Records == null || state.Records.Count == 0)
            {
                return null;
            }

            return FromState(state.Records[state.Records.Count - 1]);
        }

        private static WishgateTransitionRecordState ToState(WishgateTransitionRecord record)
        {
            return new WishgateTransitionRecordState
            {
                OperationId = record.OperationId,
                EventId = record.EventId,
                CorrelationId = record.CorrelationId,
                Operation = (int)record.Operation,
                RequestFingerprint = record.RequestFingerprint,
                EntitlementId = record.EntitlementId,
                EarnReasonId = record.EarnReasonId,
                RewardId = record.RewardId,
                RewardApplicationId = record.RewardApplicationId,
                ResultingPhase = (int)record.ResultingPhase,
                ResultingSnapshotRevision = record.ResultingSnapshotRevision,
                ResultingEntitlementRevision = record.ResultingEntitlementRevision,
                PlannedUtcSeconds = record.PlannedUtcSeconds,
                ResultingStateHash = record.ResultingStateHash,
                PlanHash = record.PlanHash,
                PostCommitNotificationCorrelationId = record.PostCommitNotificationCorrelationId,
                IsSupported = record.IsSupported
            };
        }

        private static WishgateTransitionRecord FromState(WishgateTransitionRecordState state)
        {
            if (state == null)
            {
                return null;
            }

            return new WishgateTransitionRecord(
                state.OperationId,
                state.EventId,
                state.CorrelationId,
                (WishgateOperation)state.Operation,
                state.RequestFingerprint,
                state.EntitlementId,
                state.EarnReasonId,
                state.RewardId,
                state.RewardApplicationId,
                (WishgateEntitlementPhase)state.ResultingPhase,
                state.ResultingSnapshotRevision,
                state.ResultingEntitlementRevision,
                state.PlannedUtcSeconds,
                state.ResultingStateHash,
                state.PlanHash,
                state.PostCommitNotificationCorrelationId,
                state.IsSupported);
        }

        private static RealmGemState FindGem(IList<RealmGemState> gems, string gemId)
        {
            if (gems == null)
            {
                return null;
            }

            RealmGemState match = null;
            int count = 0;
            for (int i = 0; i < gems.Count; i++)
            {
                RealmGemState gem = gems[i];
                if (gem == null ||
                    !string.Equals(gem.GemId, gemId, StringComparison.Ordinal))
                {
                    continue;
                }

                match = gem;
                count++;
                if (count > 1)
                {
                    return null;
                }
            }

            return match;
        }

        private static RealmGemCustodyState ToCustodyState(RealmGemState gem)
        {
            if (gem.IsAtHome && !gem.IsDropped)
            {
                return RealmGemCustodyState.AtHome;
            }

            if (gem.IsDropped)
            {
                return RealmGemCustodyState.Dropped;
            }

            return RealmGemCustodyState.Carried;
        }

        private static string FirstCode(WishgatePlanningResult planned)
        {
            if (planned?.Diagnostics == null || planned.Diagnostics.Count == 0)
            {
                return null;
            }

            return planned.Diagnostics[0].Code;
        }

        private static bool OperationDoesNotMatch(
            this WishgateTransactionState state,
            WishgateOperation operation)
        {
            if (state.Records == null || state.Records.Count == 0)
            {
                return false;
            }

            return state.Records[state.Records.Count - 1].Operation != (int)operation;
        }
    }
}
