using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.RealmSelection
{
    public sealed class WishgateEntitlementPlanner
    {
        private const int MaximumIdentityLength = 128;
        private const int MaximumTransitionRecords = 64;

        private readonly RealmGemCatalogSnapshot realmGemCatalog;
        private readonly IWishgateTransactionClock clock;
        private readonly IWishgateTransactionAuthority authority;

        public WishgateEntitlementPlanner(
            RealmGemCatalogSnapshot realmGemCatalog,
            IWishgateTransactionClock clock,
            IWishgateTransactionAuthority authority)
        {
            this.realmGemCatalog = realmGemCatalog;
            this.clock = clock;
            this.authority = authority;
        }

        public WishgatePlanningResult Plan(
            WishgateTransactionRequest request,
            WishgateTransactionSnapshot snapshot,
            RealmGemCustodySnapshot custodySnapshot = null)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    WishgatePlanStatus.InvalidRequest,
                    "AL-WISHGATE-REQUEST-INVALID",
                    request?.OperationId,
                    "Wishgate request identity, fields, time, revision, or operation are invalid.");
            }

            if (realmGemCatalog == null || clock == null || authority == null)
            {
                return Reject(
                    WishgatePlanStatus.Unavailable,
                    "AL-WISHGATE-DEPENDENCY-UNAVAILABLE",
                    request.OperationId,
                    "Realm Gem catalog, clock, or Wishgate authority is unavailable.");
            }

            string requestFingerprint = RequestFingerprint(request);
            WishgatePlanningResult priorReplay = ClassifyPriorReceipt(
                request,
                requestFingerprint);
            if (priorReplay != null)
            {
                return priorReplay;
            }

            if (snapshot == null || snapshot.Status == WishgateSnapshotStatus.Unavailable)
            {
                return Reject(
                    WishgatePlanStatus.Unavailable,
                    "AL-WISHGATE-SNAPSHOT-UNAVAILABLE",
                    request.OperationId,
                    "Wishgate transaction snapshot is unavailable.");
            }

            if (snapshot.Status == WishgateSnapshotStatus.CommitUncertain)
            {
                return Reject(
                    WishgatePlanStatus.RecoveryRequired,
                    "AL-WISHGATE-COMMIT-UNCERTAIN",
                    request.OperationId,
                    "Authoritative reload is required before the transaction can continue.");
            }

            if (snapshot.Status != WishgateSnapshotStatus.Available)
            {
                return Reject(
                    WishgatePlanStatus.Corrupt,
                    "AL-WISHGATE-SNAPSHOT-MALFORMED",
                    request.OperationId,
                    "Wishgate transaction snapshot is malformed.");
            }

            if (!clock.TryGetUtcSeconds(out long nowUtcSeconds) ||
                nowUtcSeconds <= 0 ||
                nowUtcSeconds != request.ObservedUtcSeconds)
            {
                return Reject(
                    WishgatePlanStatus.Unavailable,
                    "AL-WISHGATE-CLOCK-INVALID",
                    request.OperationId,
                    "Authoritative time is unavailable or does not match the observation.");
            }

            List<WishgateDiagnostic> corruption = ValidateSnapshot(snapshot, nowUtcSeconds);
            if (corruption.Count != 0)
            {
                return new WishgatePlanningResult(
                    WishgatePlanStatus.Corrupt,
                    null,
                    null,
                    null,
                    corruption);
            }

            WishgatePlanningResult ledgerReplay = ClassifyLedgerReplay(
                request,
                requestFingerprint,
                snapshot.TransitionRecords);
            if (ledgerReplay != null)
            {
                return ledgerReplay;
            }

            WishgateEntitlementState current = snapshot.Entitlement;
            if (!current.IsSupported)
            {
                return Reject(
                    WishgatePlanStatus.Unsupported,
                    "AL-WISHGATE-ENTITLEMENT-UNSUPPORTED",
                    request.EntitlementId,
                    "Unknown-future Wishgate entitlement state is preserved but cannot be mutated.");
            }

            if (snapshot.Revision != request.ExpectedSnapshotRevision ||
                current.Revision != request.ExpectedEntitlementRevision)
            {
                return Reject(
                    WishgatePlanStatus.Stale,
                    "AL-WISHGATE-REVISION-STALE",
                    request.OperationId,
                    "Expected Wishgate snapshot or entitlement revision is stale.");
            }

            WishgateDecisionStatus actorDecision = authority.Authorize(request, current);
            if (actorDecision == WishgateDecisionStatus.Unavailable)
            {
                return Reject(
                    WishgatePlanStatus.Unavailable,
                    "AL-WISHGATE-AUTHORITY-UNAVAILABLE",
                    request.ActorId,
                    "Wishgate actor authority is unavailable.");
            }

            if (actorDecision != WishgateDecisionStatus.Accepted)
            {
                return Reject(
                    WishgatePlanStatus.Unauthorized,
                    "AL-WISHGATE-ACTOR-UNAUTHORIZED",
                    request.ActorId,
                    "Actor is not authorized for this Wishgate operation.");
            }

            WishgatePlanningResult transitionGate;
            WishgateEntitlementState candidate;
            try
            {
                transitionGate = TryCreateCandidate(
                    request,
                    current,
                    custodySnapshot,
                    nowUtcSeconds,
                    out candidate);
            }
            catch (OverflowException)
            {
                return Reject(
                    WishgatePlanStatus.Overflow,
                    "AL-WISHGATE-REVISION-OVERFLOW",
                    request.OperationId,
                    "Wishgate entitlement revision cannot advance.");
            }

            if (transitionGate != null)
            {
                return transitionGate;
            }

            if (snapshot.Revision == long.MaxValue || current.Revision == long.MaxValue)
            {
                return Reject(
                    WishgatePlanStatus.Overflow,
                    "AL-WISHGATE-REVISION-OVERFLOW",
                    request.OperationId,
                    "Wishgate snapshot or entitlement revision cannot advance.");
            }

            if (snapshot.TransitionRecords.Count >= MaximumTransitionRecords)
            {
                return Reject(
                    WishgatePlanStatus.Corrupt,
                    "AL-WISHGATE-LEDGER-CAPACITY",
                    request.OperationId,
                    "Wishgate transition ledger cannot safely accept another record.");
            }

            long candidateSnapshotRevision = checked(snapshot.Revision + 1);
            WishgateTransitionRecord revisionCollision = snapshot.TransitionRecords
                .SingleOrDefault(record =>
                    record.ResultingSnapshotRevision == candidateSnapshotRevision);
            if (revisionCollision != null)
            {
                return Reject(
                    revisionCollision.IsSupported
                        ? WishgatePlanStatus.Corrupt
                        : WishgatePlanStatus.Unsupported,
                    "AL-WISHGATE-REVISION-COLLISION",
                    revisionCollision.OperationId,
                    "The next Wishgate revision is already reserved by transition history.");
            }

            string candidateStateHash = StateHash(candidate);
            string notificationCorrelationId = request.Operation == WishgateOperation.Commit
                ? HashParts(
                    "wishgate_post_commit_notification_v1",
                    request.CorrelationId,
                    request.EntitlementId,
                    request.RewardApplicationId)
                : string.Empty;
            WishgateRewardApplicationIntent rewardApplication =
                request.Operation == WishgateOperation.ApplyReward
                    ? new WishgateRewardApplicationIntent(
                        request.EntitlementId,
                        request.RewardId,
                        request.RewardApplicationId,
                        requestFingerprint)
                    : null;
            string planHash = HashParts(
                "wishgate_plan_v1",
                requestFingerprint,
                snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                candidateSnapshotRevision.ToString(CultureInfo.InvariantCulture),
                candidateStateHash,
                notificationCorrelationId);
            var transitionRecord = new WishgateTransitionRecord(
                request.OperationId,
                request.EventId,
                request.CorrelationId,
                request.Operation,
                requestFingerprint,
                candidate.EntitlementId,
                candidate.EarnReasonId,
                candidate.RewardId,
                candidate.RewardApplicationId,
                candidate.Phase,
                candidateSnapshotRevision,
                candidate.Revision,
                nowUtcSeconds,
                candidateStateHash,
                planHash,
                notificationCorrelationId,
                true);
            IReadOnlyList<WishgateTransitionRecord> candidateRecords =
                BuildCandidateRecords(snapshot.TransitionRecords, transitionRecord);
            var plan = new WishgateTransactionPlan(
                request.Operation,
                snapshot.Revision,
                candidateSnapshotRevision,
                current,
                candidate,
                candidateRecords,
                transitionRecord,
                rewardApplication,
                requestFingerprint,
                planHash);
            return new WishgatePlanningResult(
                WishgatePlanStatus.Prepared,
                plan,
                null,
                null,
                Array.Empty<WishgateDiagnostic>());
        }

        internal static bool TryVerifyAdapterCommitAndCreateReceipt(
            WishgateTransactionPlan plan,
            WishgateTransactionSnapshot verifiedSnapshot,
            out WishgateVerifiedReceipt receipt)
        {
            receipt = null;
            if (plan == null ||
                !plan.IsPreparedShape() ||
                verifiedSnapshot == null ||
                verifiedSnapshot.Status != WishgateSnapshotStatus.Available ||
                !verifiedSnapshot.IsComplete ||
                verifiedSnapshot.Revision != plan.CandidateSnapshotRevision ||
                !EntitlementEquals(
                    verifiedSnapshot.Entitlement,
                    plan.CandidateEntitlement) ||
                !HaveSameTransitionRecords(
                    verifiedSnapshot.TransitionRecords,
                    plan.CandidateTransitionRecords))
            {
                return false;
            }

            WishgateTransitionRecord committedRecord = verifiedSnapshot.TransitionRecords
                .SingleOrDefault(record => TransitionRecordEquals(
                    record,
                    plan.TransitionRecord));
            if (committedRecord == null)
            {
                return false;
            }

            var unsigned = new WishgateVerifiedReceipt(
                committedRecord,
                verifiedSnapshot.Revision,
                verifiedSnapshot.Entitlement.Revision,
                string.Empty);
            receipt = new WishgateVerifiedReceipt(
                committedRecord,
                verifiedSnapshot.Revision,
                verifiedSnapshot.Entitlement.Revision,
                ReceiptHash(unsigned));
            return IsValidReceipt(receipt);
        }

        private WishgatePlanningResult TryCreateCandidate(
            WishgateTransactionRequest request,
            WishgateEntitlementState current,
            RealmGemCustodySnapshot custodySnapshot,
            long nowUtcSeconds,
            out WishgateEntitlementState candidate)
        {
            candidate = null;
            switch (request.Operation)
            {
                case WishgateOperation.Earn:
                    if (current.Phase != WishgateEntitlementPhase.Unearned)
                    {
                        return string.Equals(
                                   current.EntitlementId,
                                   request.EntitlementId,
                                   StringComparison.Ordinal) &&
                               string.Equals(
                                   current.EarnReasonId,
                                   request.EarnReasonId,
                                   StringComparison.Ordinal)
                            ? Reject(
                                WishgatePlanStatus.NoChange,
                                "AL-WISHGATE-ALREADY-EARNED",
                                request.EntitlementId,
                                "Wishgate entitlement is already earned and its reason is unchanged.")
                            : Reject(
                                WishgatePlanStatus.Conflict,
                                "AL-WISHGATE-EARN-CONFLICT",
                                request.EntitlementId,
                                "An earned Wishgate entitlement cannot be overwritten.");
                    }

                    WishgatePlanningResult earnAuthority = ResolveLookup(
                        authority.ResolveEarnReason(request.EarnReasonId),
                        request.EarnReasonId,
                        "earn reason");
                    if (earnAuthority != null)
                    {
                        return earnAuthority;
                    }

                    if (custodySnapshot == null ||
                        custodySnapshot.Status == RealmGemCustodySnapshotStatus.Unavailable)
                    {
                        return Reject(
                            WishgatePlanStatus.Unavailable,
                            "AL-WISHGATE-ELIGIBILITY-SNAPSHOT-UNAVAILABLE",
                            request.EntitlementId,
                            "Realm Gem custody authority is unavailable for eligibility.");
                    }

                    if (custodySnapshot.Status != RealmGemCustodySnapshotStatus.Available)
                    {
                        return Reject(
                            WishgatePlanStatus.Corrupt,
                            "AL-WISHGATE-ELIGIBILITY-SNAPSHOT-MALFORMED",
                            request.EntitlementId,
                            "Realm Gem custody authority is malformed.");
                    }

                    WishgateDecisionStatus eligibility = authority.EvaluateEligibility(
                        request,
                        realmGemCatalog,
                        custodySnapshot);
                    if (eligibility == WishgateDecisionStatus.Unavailable)
                    {
                        return Reject(
                            WishgatePlanStatus.Unavailable,
                            "AL-WISHGATE-ELIGIBILITY-UNAVAILABLE",
                            request.EntitlementId,
                            "Wishgate eligibility authority is unavailable.");
                    }

                    if (eligibility != WishgateDecisionStatus.Accepted)
                    {
                        return Reject(
                            WishgatePlanStatus.Ineligible,
                            "AL-WISHGATE-ELIGIBILITY-REJECTED",
                            request.EntitlementId,
                            "Wishgate eligibility was not satisfied.");
                    }

                    candidate = new WishgateEntitlementState(
                        WishgateEntitlementPhase.Earned,
                        request.EntitlementId,
                        request.EarnReasonId,
                        string.Empty,
                        string.Empty,
                        nowUtcSeconds,
                        0,
                        0,
                        0,
                        checked(current.Revision + 1),
                        true);
                    return null;

                case WishgateOperation.SelectReward:
                    WishgatePlanningResult selectionState = GateEntitlementIdentity(
                        request,
                        current,
                        WishgateEntitlementPhase.Earned,
                        WishgateEntitlementPhase.RewardSelected);
                    if (selectionState != null)
                    {
                        return selectionState;
                    }

                    WishgatePlanningResult rewardAuthority = ResolveLookup(
                        authority.ResolveReward(request.RewardId),
                        request.RewardId,
                        "reward");
                    if (rewardAuthority != null)
                    {
                        return rewardAuthority;
                    }

                    candidate = new WishgateEntitlementState(
                        WishgateEntitlementPhase.RewardSelected,
                        current.EntitlementId,
                        current.EarnReasonId,
                        request.RewardId,
                        string.Empty,
                        current.EarnedUtcSeconds,
                        nowUtcSeconds,
                        0,
                        0,
                        checked(current.Revision + 1),
                        true);
                    return null;

                case WishgateOperation.ApplyReward:
                    WishgatePlanningResult applyState = GateEntitlementIdentity(
                        request,
                        current,
                        WishgateEntitlementPhase.RewardSelected,
                        WishgateEntitlementPhase.RewardAppliedPendingCommit);
                    if (applyState != null)
                    {
                        return applyState;
                    }

                    WishgatePlanningResult applyAuthority = ResolveLookup(
                        authority.ResolveReward(request.RewardId),
                        request.RewardId,
                        "reward");
                    if (applyAuthority != null)
                    {
                        return applyAuthority;
                    }

                    candidate = new WishgateEntitlementState(
                        WishgateEntitlementPhase.RewardAppliedPendingCommit,
                        current.EntitlementId,
                        current.EarnReasonId,
                        current.RewardId,
                        request.RewardApplicationId,
                        current.EarnedUtcSeconds,
                        current.SelectedUtcSeconds,
                        nowUtcSeconds,
                        0,
                        checked(current.Revision + 1),
                        true);
                    return null;

                case WishgateOperation.Commit:
                    WishgatePlanningResult commitState = GateEntitlementIdentity(
                        request,
                        current,
                        WishgateEntitlementPhase.RewardAppliedPendingCommit,
                        WishgateEntitlementPhase.Committed);
                    if (commitState != null)
                    {
                        return commitState;
                    }

                    candidate = new WishgateEntitlementState(
                        WishgateEntitlementPhase.Committed,
                        current.EntitlementId,
                        current.EarnReasonId,
                        current.RewardId,
                        current.RewardApplicationId,
                        current.EarnedUtcSeconds,
                        current.SelectedUtcSeconds,
                        current.AppliedUtcSeconds,
                        nowUtcSeconds,
                        checked(current.Revision + 1),
                        true);
                    return null;

                default:
                    return Reject(
                        WishgatePlanStatus.InvalidRequest,
                        "AL-WISHGATE-OPERATION-INVALID",
                        request.OperationId,
                        "Wishgate operation is invalid.");
            }
        }

        private static WishgatePlanningResult GateEntitlementIdentity(
            WishgateTransactionRequest request,
            WishgateEntitlementState current,
            WishgateEntitlementPhase requiredPhase,
            WishgateEntitlementPhase completedPhase)
        {
            if (!string.Equals(
                    current.EntitlementId,
                    request.EntitlementId,
                    StringComparison.Ordinal))
            {
                return Reject(
                    current.Phase == WishgateEntitlementPhase.Unearned
                        ? WishgatePlanStatus.Ineligible
                        : WishgatePlanStatus.Conflict,
                    "AL-WISHGATE-ENTITLEMENT-CONFLICT",
                    request.EntitlementId,
                    "Request does not match the active Wishgate entitlement.");
            }

            if (current.Phase == completedPhase || current.Phase > completedPhase)
            {
                bool sameReward = string.Equals(
                    current.RewardId,
                    request.RewardId,
                    StringComparison.Ordinal);
                bool sameApplication =
                    request.Operation == WishgateOperation.SelectReward ||
                    string.Equals(
                        current.RewardApplicationId,
                        request.RewardApplicationId,
                        StringComparison.Ordinal);
                return sameReward && sameApplication
                    ? Reject(
                        WishgatePlanStatus.NoChange,
                        "AL-WISHGATE-STAGE-ALREADY-COMPLETE",
                        request.OperationId,
                        "Wishgate transaction stage is already complete.")
                    : Reject(
                        WishgatePlanStatus.Conflict,
                        "AL-WISHGATE-STAGE-CONFLICT",
                        request.OperationId,
                        "Wishgate transaction stage is bound to different reward semantics.");
            }

            if (current.Phase != requiredPhase)
            {
                return Reject(
                    WishgatePlanStatus.Ineligible,
                    "AL-WISHGATE-STAGE-NOT-READY",
                    request.OperationId,
                    "Wishgate transaction has not reached the required prior stage.");
            }

            if (request.Operation != WishgateOperation.SelectReward &&
                (!string.Equals(
                     current.RewardId,
                     request.RewardId,
                     StringComparison.Ordinal) ||
                 (request.Operation == WishgateOperation.Commit &&
                  !string.Equals(
                      current.RewardApplicationId,
                      request.RewardApplicationId,
                      StringComparison.Ordinal))))
            {
                return Reject(
                    WishgatePlanStatus.Conflict,
                    "AL-WISHGATE-REWARD-CONFLICT",
                    request.RewardId,
                    "Reward or application identity does not match the selected transaction.");
            }

            return null;
        }

        private static WishgatePlanningResult ResolveLookup(
            WishgateLookupStatus status,
            string subjectId,
            string authorityName)
        {
            if (status == WishgateLookupStatus.Found)
            {
                return null;
            }

            if (status == WishgateLookupStatus.Unknown)
            {
                return Reject(
                    WishgatePlanStatus.Unsupported,
                    "AL-WISHGATE-AUTHORITY-ID-UNKNOWN",
                    subjectId,
                    "Wishgate " + authorityName + " ID is not approved by current authority.");
            }

            return Reject(
                WishgatePlanStatus.Unavailable,
                "AL-WISHGATE-AUTHORITY-LOOKUP-UNAVAILABLE",
                subjectId,
                "Wishgate " + authorityName + " authority is unavailable.");
        }

        private List<WishgateDiagnostic> ValidateSnapshot(
            WishgateTransactionSnapshot snapshot,
            long nowUtcSeconds)
        {
            var diagnostics = new List<WishgateDiagnostic>();
            if (!snapshot.IsComplete ||
                snapshot.Revision < 0 ||
                snapshot.Entitlement == null ||
                snapshot.TransitionRecords == null ||
                snapshot.TransitionRecords.Count > MaximumTransitionRecords)
            {
                diagnostics.Add(Diagnostic(
                    "AL-WISHGATE-SNAPSHOT-INCOMPLETE",
                    string.Empty,
                    "Complete bounded Wishgate state and transition history are required."));
                return diagnostics;
            }

            ValidateEntitlement(snapshot.Entitlement, nowUtcSeconds, diagnostics);
            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var revisions = new HashSet<long>();
            for (var index = 0; index < snapshot.TransitionRecords.Count; index++)
            {
                WishgateTransitionRecord record = snapshot.TransitionRecords[index];
                if (!IsStructurallyValidRecord(record, nowUtcSeconds) ||
                    !operationIds.Add(record.OperationId) ||
                    !eventIds.Add(record.EventId) ||
                    !revisions.Add(record.ResultingSnapshotRevision))
                {
                    diagnostics.Add(Diagnostic(
                        "AL-WISHGATE-LEDGER-ROW-INVALID",
                        record?.OperationId,
                        "Wishgate transition record is null, malformed, or duplicated."));
                    continue;
                }

                if (record.IsSupported && !IsSemanticallyValidRecord(record))
                {
                    diagnostics.Add(Diagnostic(
                        "AL-WISHGATE-LEDGER-ROW-CONTRADICTORY",
                        record.OperationId,
                        "Supported Wishgate transition record fields are contradictory."));
                }
            }

            if (diagnostics.Count != 0 || !snapshot.Entitlement.IsSupported)
            {
                return diagnostics;
            }

            if (snapshot.Entitlement.Phase == WishgateEntitlementPhase.Unearned)
            {
                if (snapshot.TransitionRecords.Any(record => record.IsSupported))
                {
                    diagnostics.Add(Diagnostic(
                        "AL-WISHGATE-STATE-LEDGER-MISMATCH",
                        snapshot.Entitlement.EntitlementId,
                        "Unearned Wishgate state cannot have supported transition history."));
                }

                return diagnostics;
            }

            string currentStateHash = StateHash(snapshot.Entitlement);
            WishgateTransitionRecord[] currentRecords = snapshot.TransitionRecords
                .Where(record =>
                    record.IsSupported &&
                    record.ResultingSnapshotRevision == snapshot.Revision &&
                    record.ResultingEntitlementRevision == snapshot.Entitlement.Revision &&
                    string.Equals(
                        record.ResultingStateHash,
                        currentStateHash,
                        StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (currentRecords.Length != 1)
            {
                diagnostics.Add(Diagnostic(
                    "AL-WISHGATE-STATE-LEDGER-MISMATCH",
                    snapshot.Entitlement.EntitlementId,
                    "Current Wishgate state is not backed by exactly one supported transition record."));
            }

            return diagnostics;
        }

        private static void ValidateEntitlement(
            WishgateEntitlementState state,
            long nowUtcSeconds,
            ICollection<WishgateDiagnostic> diagnostics)
        {
            if (state == null || state.Revision < 0)
            {
                diagnostics.Add(Diagnostic(
                    "AL-WISHGATE-ENTITLEMENT-INVALID",
                    string.Empty,
                    "Wishgate entitlement state is null or has an invalid revision."));
                return;
            }

            if (!state.IsSupported)
            {
                return;
            }

            bool valid = Enum.IsDefined(typeof(WishgateEntitlementPhase), state.Phase);
            switch (state.Phase)
            {
                case WishgateEntitlementPhase.Unearned:
                    valid &= state.Revision == 0 &&
                             string.IsNullOrEmpty(state.EntitlementId) &&
                             string.IsNullOrEmpty(state.EarnReasonId) &&
                             string.IsNullOrEmpty(state.RewardId) &&
                             string.IsNullOrEmpty(state.RewardApplicationId) &&
                             AllZero(state);
                    break;
                case WishgateEntitlementPhase.Earned:
                    valid &= HasEarnedIdentity(state) &&
                             string.IsNullOrEmpty(state.RewardId) &&
                             string.IsNullOrEmpty(state.RewardApplicationId) &&
                             IsTimestamp(state.EarnedUtcSeconds, nowUtcSeconds) &&
                             state.SelectedUtcSeconds == 0 &&
                             state.AppliedUtcSeconds == 0 &&
                             state.CommittedUtcSeconds == 0;
                    break;
                case WishgateEntitlementPhase.RewardSelected:
                    valid &= HasEarnedIdentity(state) &&
                             RealmGemCatalogResolver.IsStableId(state.RewardId) &&
                             string.IsNullOrEmpty(state.RewardApplicationId) &&
                             IsOrderedTimestamp(
                                 state.EarnedUtcSeconds,
                                 state.SelectedUtcSeconds,
                                 nowUtcSeconds) &&
                             state.AppliedUtcSeconds == 0 &&
                             state.CommittedUtcSeconds == 0;
                    break;
                case WishgateEntitlementPhase.RewardAppliedPendingCommit:
                    valid &= HasEarnedIdentity(state) &&
                             RealmGemCatalogResolver.IsStableId(state.RewardId) &&
                             IsOpaqueId(state.RewardApplicationId) &&
                             IsOrderedTimestamp(
                                 state.EarnedUtcSeconds,
                                 state.SelectedUtcSeconds,
                                 nowUtcSeconds) &&
                             IsOrderedTimestamp(
                                 state.SelectedUtcSeconds,
                                 state.AppliedUtcSeconds,
                                 nowUtcSeconds) &&
                             state.CommittedUtcSeconds == 0;
                    break;
                case WishgateEntitlementPhase.Committed:
                    valid &= HasEarnedIdentity(state) &&
                             RealmGemCatalogResolver.IsStableId(state.RewardId) &&
                             IsOpaqueId(state.RewardApplicationId) &&
                             IsOrderedTimestamp(
                                 state.EarnedUtcSeconds,
                                 state.SelectedUtcSeconds,
                                 nowUtcSeconds) &&
                             IsOrderedTimestamp(
                                 state.SelectedUtcSeconds,
                                 state.AppliedUtcSeconds,
                                 nowUtcSeconds) &&
                             IsOrderedTimestamp(
                                 state.AppliedUtcSeconds,
                                 state.CommittedUtcSeconds,
                                 nowUtcSeconds);
                    break;
                default:
                    valid = false;
                    break;
            }

            if (!valid)
            {
                diagnostics.Add(Diagnostic(
                    "AL-WISHGATE-ENTITLEMENT-CONTRADICTORY",
                    state.EntitlementId,
                    "Wishgate entitlement identity, phase, or timestamps are contradictory."));
            }
        }

        private static bool IsStructurallyValidRecord(
            WishgateTransitionRecord record,
            long nowUtcSeconds)
        {
            return record != null &&
                   IsOpaqueId(record.OperationId) &&
                   IsOpaqueId(record.EventId) &&
                   IsOpaqueId(record.CorrelationId) &&
                   IsSha256(record.RequestFingerprint) &&
                   IsOpaqueId(record.EntitlementId) &&
                   record.ResultingSnapshotRevision > 0 &&
                   record.ResultingEntitlementRevision > 0 &&
                   IsTimestamp(record.PlannedUtcSeconds, nowUtcSeconds) &&
                   IsSha256(record.ResultingStateHash) &&
                   IsSha256(record.PlanHash);
        }

        private static bool IsSemanticallyValidRecord(WishgateTransitionRecord record)
        {
            if (!Enum.IsDefined(typeof(WishgateOperation), record.Operation) ||
                !Enum.IsDefined(typeof(WishgateEntitlementPhase), record.ResultingPhase))
            {
                return false;
            }

            bool hasReason = RealmGemCatalogResolver.IsStableId(record.EarnReasonId);
            bool hasReward = RealmGemCatalogResolver.IsStableId(record.RewardId);
            bool hasApplication = IsOpaqueId(record.RewardApplicationId);
            switch (record.Operation)
            {
                case WishgateOperation.Earn:
                    return record.ResultingPhase == WishgateEntitlementPhase.Earned &&
                           hasReason &&
                           string.IsNullOrEmpty(record.RewardId) &&
                           string.IsNullOrEmpty(record.RewardApplicationId) &&
                           string.IsNullOrEmpty(record.PostCommitNotificationCorrelationId);
                case WishgateOperation.SelectReward:
                    return record.ResultingPhase == WishgateEntitlementPhase.RewardSelected &&
                           hasReason &&
                           hasReward &&
                           string.IsNullOrEmpty(record.RewardApplicationId) &&
                           string.IsNullOrEmpty(record.PostCommitNotificationCorrelationId);
                case WishgateOperation.ApplyReward:
                    return record.ResultingPhase == WishgateEntitlementPhase.RewardAppliedPendingCommit &&
                           hasReason &&
                           hasReward &&
                           hasApplication &&
                           string.IsNullOrEmpty(record.PostCommitNotificationCorrelationId);
                case WishgateOperation.Commit:
                    return record.ResultingPhase == WishgateEntitlementPhase.Committed &&
                           hasReason &&
                           hasReward &&
                           hasApplication &&
                           IsSha256(record.PostCommitNotificationCorrelationId);
                default:
                    return false;
            }
        }

        private static WishgatePlanningResult ClassifyPriorReceipt(
            WishgateTransactionRequest request,
            string requestFingerprint)
        {
            WishgateVerifiedReceipt receipt = request.PriorReceipt;
            if (receipt == null)
            {
                return null;
            }

            return IsValidReceipt(receipt) &&
                   RecordMatchesRequest(
                       receipt.TransitionRecord,
                       request,
                       requestFingerprint)
                ? new WishgatePlanningResult(
                    WishgatePlanStatus.Duplicate,
                    null,
                    receipt.TransitionRecord,
                    receipt,
                    new[]
                    {
                        Diagnostic(
                            "AL-WISHGATE-DUPLICATE-RECEIPT",
                            request.OperationId,
                            "Verified Wishgate receipt already satisfies this request.")
                    })
                : Reject(
                    WishgatePlanStatus.Conflict,
                    "AL-WISHGATE-RECEIPT-CONFLICT",
                    request.OperationId,
                    "Prior Wishgate receipt does not match the request.");
        }

        private static WishgatePlanningResult ClassifyLedgerReplay(
            WishgateTransactionRequest request,
            string requestFingerprint,
            IReadOnlyList<WishgateTransitionRecord> records)
        {
            WishgateTransitionRecord operationMatch = records.SingleOrDefault(record =>
                string.Equals(record.OperationId, request.OperationId, StringComparison.Ordinal));
            if (operationMatch != null)
            {
                if (!operationMatch.IsSupported)
                {
                    return Reject(
                        WishgatePlanStatus.Unsupported,
                        "AL-WISHGATE-REPLAY-UNSUPPORTED",
                        request.OperationId,
                        "Operation identity belongs to an unknown-future transition record.");
                }

                return RecordMatchesRequest(
                        operationMatch,
                        request,
                        requestFingerprint)
                    ? new WishgatePlanningResult(
                        WishgatePlanStatus.Duplicate,
                        null,
                        operationMatch,
                        null,
                        new[]
                        {
                            Diagnostic(
                                "AL-WISHGATE-DUPLICATE-LEDGER",
                                request.OperationId,
                                "Committed Wishgate transition already satisfies this request.")
                        })
                    : Reject(
                        WishgatePlanStatus.Conflict,
                        "AL-WISHGATE-OPERATION-CONFLICT",
                        request.OperationId,
                        "Operation identity is already bound to different Wishgate semantics.");
            }

            WishgateTransitionRecord eventMatch = records.SingleOrDefault(record =>
                string.Equals(record.EventId, request.EventId, StringComparison.Ordinal));
            if (eventMatch != null)
            {
                return Reject(
                    eventMatch.IsSupported
                        ? WishgatePlanStatus.Conflict
                        : WishgatePlanStatus.Unsupported,
                    "AL-WISHGATE-EVENT-CONFLICT",
                    request.EventId,
                    "Event identity is already bound to a Wishgate transition.");
            }

            return null;
        }

        private static bool RecordMatchesRequest(
            WishgateTransitionRecord record,
            WishgateTransactionRequest request,
            string requestFingerprint)
        {
            if (record == null ||
                !record.IsSupported ||
                request.ExpectedSnapshotRevision == long.MaxValue ||
                request.ExpectedEntitlementRevision == long.MaxValue)
            {
                return false;
            }

            return string.Equals(record.OperationId, request.OperationId, StringComparison.Ordinal) &&
                   string.Equals(record.EventId, request.EventId, StringComparison.Ordinal) &&
                   string.Equals(record.CorrelationId, request.CorrelationId, StringComparison.Ordinal) &&
                   record.Operation == request.Operation &&
                   string.Equals(
                       record.RequestFingerprint,
                       requestFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(record.EntitlementId, request.EntitlementId, StringComparison.Ordinal) &&
                   string.Equals(record.RewardId, request.RewardId, StringComparison.Ordinal) &&
                   string.Equals(
                       record.RewardApplicationId,
                       request.RewardApplicationId,
                       StringComparison.Ordinal) &&
                   record.ResultingSnapshotRevision == request.ExpectedSnapshotRevision + 1 &&
                   record.ResultingEntitlementRevision == request.ExpectedEntitlementRevision + 1;
        }

        private static bool IsValidRequest(WishgateTransactionRequest request)
        {
            if (request == null ||
                !Enum.IsDefined(typeof(WishgateOperation), request.Operation) ||
                !IsOpaqueId(request.OperationId) ||
                !IsOpaqueId(request.EventId) ||
                !IsOpaqueId(request.CorrelationId) ||
                !IsOpaqueId(request.ActorId) ||
                !IsOpaqueId(request.EntitlementId) ||
                request.ObservedUtcSeconds <= 0 ||
                request.ExpectedSnapshotRevision < 0 ||
                request.ExpectedEntitlementRevision < 0)
            {
                return false;
            }

            switch (request.Operation)
            {
                case WishgateOperation.Earn:
                    return RealmGemCatalogResolver.IsStableId(request.EarnReasonId) &&
                           string.IsNullOrEmpty(request.RewardId) &&
                           string.IsNullOrEmpty(request.RewardApplicationId);
                case WishgateOperation.SelectReward:
                    return string.IsNullOrEmpty(request.EarnReasonId) &&
                           RealmGemCatalogResolver.IsStableId(request.RewardId) &&
                           string.IsNullOrEmpty(request.RewardApplicationId);
                case WishgateOperation.ApplyReward:
                case WishgateOperation.Commit:
                    return string.IsNullOrEmpty(request.EarnReasonId) &&
                           RealmGemCatalogResolver.IsStableId(request.RewardId) &&
                           IsOpaqueId(request.RewardApplicationId);
                default:
                    return false;
            }
        }

        private static IReadOnlyList<WishgateTransitionRecord> BuildCandidateRecords(
            IReadOnlyList<WishgateTransitionRecord> records,
            WishgateTransitionRecord candidate)
        {
            return records
                .Concat(new[] { candidate })
                .OrderBy(record => record.ResultingSnapshotRevision)
                .ThenBy(record => record.OperationId, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool HaveSameTransitionRecords(
            IReadOnlyList<WishgateTransitionRecord> left,
            IReadOnlyList<WishgateTransitionRecord> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            bool[] matched = new bool[right.Count];
            for (var leftIndex = 0; leftIndex < left.Count; leftIndex++)
            {
                bool found = false;
                for (var rightIndex = 0; rightIndex < right.Count; rightIndex++)
                {
                    if (!matched[rightIndex] &&
                        TransitionRecordEquals(left[leftIndex], right[rightIndex]))
                    {
                        matched[rightIndex] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TransitionRecordEquals(
            WishgateTransitionRecord left,
            WishgateTransitionRecord right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.OperationId, right.OperationId, StringComparison.Ordinal) &&
                   string.Equals(left.EventId, right.EventId, StringComparison.Ordinal) &&
                   string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal) &&
                   left.Operation == right.Operation &&
                   string.Equals(left.RequestFingerprint, right.RequestFingerprint, StringComparison.Ordinal) &&
                   string.Equals(left.EntitlementId, right.EntitlementId, StringComparison.Ordinal) &&
                   string.Equals(left.EarnReasonId, right.EarnReasonId, StringComparison.Ordinal) &&
                   string.Equals(left.RewardId, right.RewardId, StringComparison.Ordinal) &&
                   string.Equals(left.RewardApplicationId, right.RewardApplicationId, StringComparison.Ordinal) &&
                   left.ResultingPhase == right.ResultingPhase &&
                   left.ResultingSnapshotRevision == right.ResultingSnapshotRevision &&
                   left.ResultingEntitlementRevision == right.ResultingEntitlementRevision &&
                   left.PlannedUtcSeconds == right.PlannedUtcSeconds &&
                   string.Equals(left.ResultingStateHash, right.ResultingStateHash, StringComparison.Ordinal) &&
                   string.Equals(left.PlanHash, right.PlanHash, StringComparison.Ordinal) &&
                   string.Equals(
                       left.PostCommitNotificationCorrelationId,
                       right.PostCommitNotificationCorrelationId,
                       StringComparison.Ordinal) &&
                   left.IsSupported == right.IsSupported;
        }

        private static bool EntitlementEquals(
            WishgateEntitlementState left,
            WishgateEntitlementState right)
        {
            return left != null &&
                   right != null &&
                   left.Phase == right.Phase &&
                   string.Equals(left.EntitlementId, right.EntitlementId, StringComparison.Ordinal) &&
                   string.Equals(left.EarnReasonId, right.EarnReasonId, StringComparison.Ordinal) &&
                   string.Equals(left.RewardId, right.RewardId, StringComparison.Ordinal) &&
                   string.Equals(left.RewardApplicationId, right.RewardApplicationId, StringComparison.Ordinal) &&
                   left.EarnedUtcSeconds == right.EarnedUtcSeconds &&
                   left.SelectedUtcSeconds == right.SelectedUtcSeconds &&
                   left.AppliedUtcSeconds == right.AppliedUtcSeconds &&
                   left.CommittedUtcSeconds == right.CommittedUtcSeconds &&
                   left.Revision == right.Revision &&
                   left.IsSupported == right.IsSupported;
        }

        private static bool IsValidReceipt(WishgateVerifiedReceipt receipt)
        {
            if (receipt?.TransitionRecord == null ||
                !receipt.TransitionRecord.IsSupported ||
                !IsSemanticallyValidRecord(receipt.TransitionRecord) ||
                receipt.VerifiedSnapshotRevision !=
                    receipt.TransitionRecord.ResultingSnapshotRevision ||
                receipt.VerifiedEntitlementRevision !=
                    receipt.TransitionRecord.ResultingEntitlementRevision ||
                !IsSha256(receipt.ReceiptHash))
            {
                return false;
            }

            return string.Equals(
                receipt.ReceiptHash,
                ReceiptHash(receipt),
                StringComparison.Ordinal);
        }

        private static string ReceiptHash(WishgateVerifiedReceipt receipt)
        {
            WishgateTransitionRecord record = receipt.TransitionRecord;
            return HashParts(
                "wishgate_verified_receipt_v1",
                record.OperationId,
                record.RequestFingerprint,
                record.PlanHash,
                receipt.VerifiedSnapshotRevision.ToString(CultureInfo.InvariantCulture),
                receipt.VerifiedEntitlementRevision.ToString(CultureInfo.InvariantCulture));
        }

        private static string RequestFingerprint(WishgateTransactionRequest request)
        {
            return HashParts(
                "wishgate_request_v1",
                request.Operation.ToString(),
                request.OperationId,
                request.EventId,
                request.CorrelationId,
                request.ActorId,
                request.EntitlementId,
                request.EarnReasonId,
                request.RewardId,
                request.RewardApplicationId,
                request.ObservedUtcSeconds.ToString(CultureInfo.InvariantCulture),
                request.ExpectedSnapshotRevision.ToString(CultureInfo.InvariantCulture),
                request.ExpectedEntitlementRevision.ToString(CultureInfo.InvariantCulture));
        }

        internal static string StateHash(WishgateEntitlementState state)
        {
            return HashParts(
                "wishgate_entitlement_state_v1",
                state.Phase.ToString(),
                state.EntitlementId,
                state.EarnReasonId,
                state.RewardId,
                state.RewardApplicationId,
                state.EarnedUtcSeconds.ToString(CultureInfo.InvariantCulture),
                state.SelectedUtcSeconds.ToString(CultureInfo.InvariantCulture),
                state.AppliedUtcSeconds.ToString(CultureInfo.InvariantCulture),
                state.CommittedUtcSeconds.ToString(CultureInfo.InvariantCulture),
                state.Revision.ToString(CultureInfo.InvariantCulture),
                state.IsSupported ? "1" : "0");
        }

        private static string HashParts(params string[] parts)
        {
            var canonical = new StringBuilder();
            for (var index = 0; index < parts.Length; index++)
            {
                string value = parts[index] ?? string.Empty;
                canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture));
                canonical.Append(':');
                canonical.Append(value);
            }

            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))
                    .Select(value => value.ToString("x2")));
            }
        }

        private static bool HasEarnedIdentity(WishgateEntitlementState state)
        {
            return state.Revision > 0 &&
                   IsOpaqueId(state.EntitlementId) &&
                   RealmGemCatalogResolver.IsStableId(state.EarnReasonId);
        }

        private static bool AllZero(WishgateEntitlementState state)
        {
            return state.EarnedUtcSeconds == 0 &&
                   state.SelectedUtcSeconds == 0 &&
                   state.AppliedUtcSeconds == 0 &&
                   state.CommittedUtcSeconds == 0;
        }

        private static bool IsTimestamp(long value, long nowUtcSeconds)
        {
            return value > 0 && value <= nowUtcSeconds;
        }

        private static bool IsOrderedTimestamp(
            long earlier,
            long later,
            long nowUtcSeconds)
        {
            return earlier > 0 && later >= earlier && later <= nowUtcSeconds;
        }

        private static bool IsOpaqueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentityLength)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character < '!' || character > '~')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static WishgatePlanningResult Reject(
            WishgatePlanStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new WishgatePlanningResult(
                status,
                null,
                null,
                null,
                new[] { Diagnostic(code, subjectId, message) });
        }

        private static WishgateDiagnostic Diagnostic(
            string code,
            string subjectId,
            string message)
        {
            return new WishgateDiagnostic(code, subjectId, message);
        }
    }

    internal static class WishgatePlanShape
    {
        internal static bool IsPreparedShape(this WishgateTransactionPlan plan)
        {
            return plan != null &&
                   plan.ExpectedEntitlement != null &&
                   plan.CandidateEntitlement != null &&
                   plan.TransitionRecord != null &&
                   plan.CandidateTransitionRecords != null &&
                   plan.ExpectedSnapshotRevision != long.MaxValue &&
                   plan.ExpectedEntitlement.Revision != long.MaxValue &&
                   plan.CandidateSnapshotRevision == plan.ExpectedSnapshotRevision + 1 &&
                   plan.CandidateEntitlement.Revision == plan.ExpectedEntitlement.Revision + 1 &&
                   string.Equals(
                       plan.RequestFingerprint,
                       plan.TransitionRecord.RequestFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       plan.PlanHash,
                       plan.TransitionRecord.PlanHash,
                       StringComparison.Ordinal);
        }
    }
}
