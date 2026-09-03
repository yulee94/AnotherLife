using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class WishgateEntitlementPlannerTests
    {
        private const string EntitlementId = "wishgate_entitlement_001";
        private const string EarnReasonId = "earn_all_realm_gems";
        private const string RewardId = "wishgate_reward_renewal";
        private const string ApplicationId = "wishgate_application_001";
        private const long Now = 1000;

        private RealmGemCatalogSnapshot catalog;
        private FakeClock clock;
        private FakeAuthority authority;
        private WishgateEntitlementPlanner planner;

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult source = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.That(source.IsSuccess, Is.True, source.TechnicalCode);
            catalog = RealmGemCatalogResolver.Build(source.Snapshot).Snapshot;
            clock = new FakeClock(Now);
            authority = new FakeAuthority();
            planner = new WishgateEntitlementPlanner(catalog, clock, authority);
        }

        [Test]
        public void FullLifecycleKeepsSelectionUnconsumedUntilVerifiedCommit()
        {
            WishgateTransactionSnapshot initial = Initial();
            WishgatePlanningResult earn = planner.Plan(
                EarnRequest(initial),
                initial,
                AllHomeCustody());

            Assert.That(earn.IsPrepared, Is.True);
            Assert.That(earn.Plan.CandidateEntitlement.Phase, Is.EqualTo(WishgateEntitlementPhase.Earned));
            Assert.That(earn.Plan.RewardApplication, Is.Null);
            Assert.That(initial.Entitlement.Phase, Is.EqualTo(WishgateEntitlementPhase.Unearned));
            WishgateTransactionSnapshot earned = Verify(earn, out WishgateVerifiedReceipt earnReceipt);
            Assert.That(earnReceipt.IsFinalCommit, Is.False);

            WishgatePlanningResult select = planner.Plan(SelectRequest(earned), earned);

            Assert.That(select.IsPrepared, Is.True);
            Assert.That(
                select.Plan.CandidateEntitlement.Phase,
                Is.EqualTo(WishgateEntitlementPhase.RewardSelected));
            Assert.That(select.Plan.CandidateEntitlement.RewardId, Is.EqualTo(RewardId));
            Assert.That(select.Plan.CandidateEntitlement.RewardApplicationId, Is.Empty);
            Assert.That(select.Plan.CandidateEntitlement.CommittedUtcSeconds, Is.Zero);
            Assert.That(select.Plan.RewardApplication, Is.Null);
            WishgateTransactionSnapshot selected = Verify(select, out _);

            WishgatePlanningResult apply = planner.Plan(ApplyRequest(selected), selected);

            Assert.That(apply.IsPrepared, Is.True);
            Assert.That(apply.Plan.RequiresRewardApplication, Is.True);
            Assert.That(apply.Plan.RewardApplication.RewardId, Is.EqualTo(RewardId));
            Assert.That(apply.Plan.RewardApplication.RewardApplicationId, Is.EqualTo(ApplicationId));
            Assert.That(
                apply.Plan.CandidateEntitlement.Phase,
                Is.EqualTo(WishgateEntitlementPhase.RewardAppliedPendingCommit));
            Assert.That(apply.Plan.CandidateEntitlement.CommittedUtcSeconds, Is.Zero);
            WishgateTransactionSnapshot applied = Verify(apply, out WishgateVerifiedReceipt applyReceipt);
            Assert.That(applyReceipt.IsFinalCommit, Is.False);

            authority.RewardStatus = WishgateLookupStatus.Unavailable;
            WishgatePlanningResult commit = planner.Plan(CommitRequest(applied), applied);

            Assert.That(commit.IsPrepared, Is.True);
            Assert.That(commit.ExistingReceipt, Is.Null);
            Assert.That(commit.Plan.RewardApplication, Is.Null);
            Assert.That(
                commit.Plan.CandidateEntitlement.Phase,
                Is.EqualTo(WishgateEntitlementPhase.Committed));
            WishgateTransactionSnapshot committed = Verify(commit, out WishgateVerifiedReceipt receipt);
            Assert.That(committed.Entitlement.Phase, Is.EqualTo(WishgateEntitlementPhase.Committed));
            Assert.That(receipt.IsFinalCommit, Is.True);
            Assert.That(receipt.PostCommitNotificationCorrelationId, Has.Length.EqualTo(64));
            Assert.That(receipt.ReceiptHash, Has.Length.EqualTo(64));
        }

        [Test]
        public void EligibilityAndEarnReasonAuthorityFailClosed()
        {
            WishgateTransactionSnapshot initial = Initial();
            WishgateTransactionRequest request = EarnRequest(initial);
            authority.EligibilityStatus = WishgateDecisionStatus.Rejected;
            WishgatePlanningResult rejected = planner.Plan(request, initial, AllHomeCustody());
            authority.EligibilityStatus = WishgateDecisionStatus.Unavailable;
            WishgatePlanningResult unavailable = planner.Plan(request, initial, AllHomeCustody());
            authority.EligibilityStatus = WishgateDecisionStatus.Accepted;
            authority.EarnReasonStatus = WishgateLookupStatus.Unknown;
            WishgatePlanningResult unknownReason = planner.Plan(request, initial, AllHomeCustody());
            authority.EarnReasonStatus = WishgateLookupStatus.Unavailable;
            WishgatePlanningResult missingReasonAuthority = planner.Plan(request, initial, AllHomeCustody());
            authority.EarnReasonStatus = WishgateLookupStatus.Found;
            WishgatePlanningResult missingCustody = planner.Plan(request, initial, null);
            WishgatePlanningResult malformedCustody = planner.Plan(
                request,
                initial,
                new RealmGemCustodySnapshot(
                    RealmGemCustodySnapshotStatus.Malformed,
                    0,
                    Array.Empty<RealmGemCustodyRecord>()));

            Assert.That(rejected.Status, Is.EqualTo(WishgatePlanStatus.Ineligible));
            Assert.That(unavailable.Status, Is.EqualTo(WishgatePlanStatus.Unavailable));
            Assert.That(unknownReason.Status, Is.EqualTo(WishgatePlanStatus.Unsupported));
            Assert.That(missingReasonAuthority.Status, Is.EqualTo(WishgatePlanStatus.Unavailable));
            Assert.That(missingCustody.Status, Is.EqualTo(WishgatePlanStatus.Unavailable));
            Assert.That(malformedCustody.Status, Is.EqualTo(WishgatePlanStatus.Corrupt));
        }

        [Test]
        public void RepeatedEarnNeverOverwritesEntitlementOrReason()
        {
            WishgatePlanningResult first = planner.Plan(
                EarnRequest(Initial()),
                Initial(),
                AllHomeCustody());
            WishgateTransactionSnapshot earned = Verify(first, out _);

            WishgatePlanningResult same = planner.Plan(
                EarnRequest(
                    earned,
                    operationId: "wishgate_earn_repeat",
                    eventId: "wishgate_earn_repeat_event"),
                earned,
                AllHomeCustody());
            WishgatePlanningResult changedReason = planner.Plan(
                EarnRequest(
                    earned,
                    operationId: "wishgate_earn_conflict",
                    eventId: "wishgate_earn_conflict_event",
                    earnReasonId: "earn_different_reason"),
                earned,
                AllHomeCustody());

            Assert.That(same.Status, Is.EqualTo(WishgatePlanStatus.NoChange));
            Assert.That(changedReason.Status, Is.EqualTo(WishgatePlanStatus.Conflict));
            Assert.That(earned.Entitlement.EarnReasonId, Is.EqualTo(EarnReasonId));
            Assert.That(earned.Entitlement.Revision, Is.EqualTo(1));
            Assert.That(same.Plan, Is.Null);
            Assert.That(changedReason.Plan, Is.Null);
        }

        [Test]
        public void RewardSelectionRejectsBlankUnknownAndUnavailableAuthority()
        {
            WishgateTransactionSnapshot earned = Earned();
            var blankRequest = new WishgateTransactionRequest(
                WishgateOperation.SelectReward,
                "wishgate_select_blank",
                "wishgate_select_blank_event",
                "wishgate_select_blank_correlation",
                "actor_one",
                EntitlementId,
                string.Empty,
                string.Empty,
                string.Empty,
                Now,
                earned.Revision,
                earned.Entitlement.Revision);
            WishgatePlanningResult blank = planner.Plan(blankRequest, earned);
            authority.RewardStatus = WishgateLookupStatus.Unknown;
            WishgatePlanningResult unknown = planner.Plan(SelectRequest(earned), earned);
            authority.RewardStatus = WishgateLookupStatus.Unavailable;
            WishgatePlanningResult unavailable = planner.Plan(SelectRequest(earned), earned);

            Assert.That(blank.Status, Is.EqualTo(WishgatePlanStatus.InvalidRequest));
            Assert.That(unknown.Status, Is.EqualTo(WishgatePlanStatus.Unsupported));
            Assert.That(unavailable.Status, Is.EqualTo(WishgatePlanStatus.Unavailable));
            Assert.That(earned.Entitlement.Phase, Is.EqualTo(WishgateEntitlementPhase.Earned));
        }

        [Test]
        public void SelectApplyAndCommitRetriesAreIdempotent()
        {
            WishgateTransactionSnapshot earned = Earned();
            WishgateTransactionRequest selectRequest = SelectRequest(earned);
            WishgatePlanningResult select = planner.Plan(selectRequest, earned);
            WishgateTransactionSnapshot selected = Verify(select, out WishgateVerifiedReceipt selectReceipt);
            WishgatePlanningResult selectLedgerReplay = planner.Plan(selectRequest, selected);
            WishgatePlanningResult selectReceiptReplay = planner.Plan(
                WithReceipt(selectRequest, selectReceipt),
                null);

            WishgateTransactionRequest applyRequest = ApplyRequest(selected);
            WishgatePlanningResult apply = planner.Plan(applyRequest, selected);
            WishgateTransactionSnapshot applied = Verify(apply, out WishgateVerifiedReceipt applyReceipt);
            WishgatePlanningResult applyLedgerReplay = planner.Plan(applyRequest, applied);
            WishgatePlanningResult applyReceiptReplay = planner.Plan(
                WithReceipt(applyRequest, applyReceipt),
                null);

            WishgateTransactionRequest commitRequest = CommitRequest(applied);
            WishgatePlanningResult commit = planner.Plan(commitRequest, applied);
            WishgateTransactionSnapshot committed = Verify(commit, out WishgateVerifiedReceipt commitReceipt);
            WishgatePlanningResult commitLedgerReplay = planner.Plan(commitRequest, committed);
            WishgatePlanningResult commitReceiptReplay = planner.Plan(
                WithReceipt(commitRequest, commitReceipt),
                null);
            WishgatePlanningResult changedPayload = planner.Plan(
                new WishgateTransactionRequest(
                    commitRequest.Operation,
                    commitRequest.OperationId,
                    commitRequest.EventId,
                    commitRequest.CorrelationId,
                    "different_actor",
                    commitRequest.EntitlementId,
                    commitRequest.EarnReasonId,
                    commitRequest.RewardId,
                    commitRequest.RewardApplicationId,
                    commitRequest.ObservedUtcSeconds,
                    commitRequest.ExpectedSnapshotRevision,
                    commitRequest.ExpectedEntitlementRevision,
                    commitReceipt),
                null);

            AssertDuplicate(selectLedgerReplay, hasReceipt: false);
            AssertDuplicate(selectReceiptReplay, hasReceipt: true);
            AssertDuplicate(applyLedgerReplay, hasReceipt: false);
            AssertDuplicate(applyReceiptReplay, hasReceipt: true);
            AssertDuplicate(commitLedgerReplay, hasReceipt: false);
            AssertDuplicate(commitReceiptReplay, hasReceipt: true);
            Assert.That(changedPayload.Status, Is.EqualTo(WishgatePlanStatus.Conflict));
            Assert.That(commitLedgerReplay.Plan, Is.Null);
        }

        [Test]
        public void SaveFailuresAndCommitUncertaintyRequireSafeRetryOrRecovery()
        {
            WishgateTransactionSnapshot selected = Selected();
            WishgateTransactionRequest applyRequest = ApplyRequest(selected);
            WishgatePlanningResult firstApply = planner.Plan(applyRequest, selected);
            WishgatePlanningResult retryBeforeSave = planner.Plan(applyRequest, selected);
            WishgateTransactionSnapshot uncertainApply = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.CommitUncertain,
                selected.Revision,
                selected.Entitlement,
                selected.TransitionRecords,
                true);
            WishgatePlanningResult applyRecovery = planner.Plan(applyRequest, uncertainApply);

            Assert.That(firstApply.IsPrepared, Is.True);
            Assert.That(retryBeforeSave.IsPrepared, Is.True);
            Assert.That(retryBeforeSave.Plan.PlanHash, Is.EqualTo(firstApply.Plan.PlanHash));
            Assert.That(
                retryBeforeSave.Plan.RewardApplication.RewardApplicationId,
                Is.EqualTo(firstApply.Plan.RewardApplication.RewardApplicationId));
            Assert.That(applyRecovery.Status, Is.EqualTo(WishgatePlanStatus.RecoveryRequired));
            Assert.That(selected.Entitlement.Phase, Is.EqualTo(WishgateEntitlementPhase.RewardSelected));

            WishgateTransactionSnapshot applied = Verify(firstApply, out _);
            WishgateTransactionRequest commitRequest = CommitRequest(applied);
            WishgatePlanningResult firstCommit = planner.Plan(commitRequest, applied);
            WishgatePlanningResult retryBeforeCommitSave = planner.Plan(commitRequest, applied);
            WishgateTransactionSnapshot uncertainCommit = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.CommitUncertain,
                applied.Revision,
                applied.Entitlement,
                applied.TransitionRecords,
                true);
            WishgatePlanningResult commitRecovery = planner.Plan(commitRequest, uncertainCommit);

            Assert.That(firstCommit.IsPrepared, Is.True);
            Assert.That(retryBeforeCommitSave.Plan.PlanHash, Is.EqualTo(firstCommit.Plan.PlanHash));
            Assert.That(commitRecovery.Status, Is.EqualTo(WishgatePlanStatus.RecoveryRequired));
            Assert.That(applied.Entitlement.Phase, Is.EqualTo(WishgateEntitlementPhase.RewardAppliedPendingCommit));

            WishgateTransactionSnapshot committed = Verify(firstCommit, out _);
            Assert.That(
                planner.Plan(commitRequest, committed).Status,
                Is.EqualTo(WishgatePlanStatus.Duplicate));
        }

        [Test]
        public void CorruptIncompleteAndTamperedSnapshotsFailClosedDeterministically()
        {
            WishgateTransactionSnapshot earned = Earned();
            var incomplete = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                earned.Revision,
                earned.Entitlement,
                null,
                false);
            List<WishgateTransitionRecord> duplicateRows = earned.TransitionRecords.ToList();
            duplicateRows.Add(earned.TransitionRecords[0]);
            var duplicate = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                earned.Revision,
                earned.Entitlement,
                duplicateRows,
                true);
            var futureState = new WishgateEntitlementState(
                earned.Entitlement.Phase,
                earned.Entitlement.EntitlementId,
                earned.Entitlement.EarnReasonId,
                earned.Entitlement.RewardId,
                earned.Entitlement.RewardApplicationId,
                Now + 1,
                0,
                0,
                0,
                earned.Entitlement.Revision,
                true);
            var future = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                earned.Revision,
                futureState,
                earned.TransitionRecords,
                true);

            WishgatePlanningResult incompleteResult = planner.Plan(SelectRequest(earned), incomplete);
            WishgatePlanningResult duplicateResult = planner.Plan(SelectRequest(earned), duplicate);
            WishgatePlanningResult futureResult = planner.Plan(SelectRequest(earned), future);

            Assert.That(incompleteResult.Status, Is.EqualTo(WishgatePlanStatus.Corrupt));
            Assert.That(duplicateResult.Status, Is.EqualTo(WishgatePlanStatus.Corrupt));
            Assert.That(futureResult.Status, Is.EqualTo(WishgatePlanStatus.Corrupt));
            CollectionAssert.AreEqual(
                duplicateResult.Diagnostics
                    .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.SubjectId, StringComparer.Ordinal)
                    .Select(diagnostic => diagnostic.Code)
                    .ToArray(),
                duplicateResult.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        }

        [Test]
        public void StaleDeniedMissingAndOverflowingAuthorityCannotPlan()
        {
            WishgateTransactionSnapshot initial = Initial();
            WishgateTransactionRequest request = EarnRequest(initial);
            var noCatalog = new WishgateEntitlementPlanner(null, clock, authority);
            WishgatePlanningResult missingCatalog = noCatalog.Plan(request, initial, AllHomeCustody());
            clock.IsAvailable = false;
            WishgatePlanningResult missingClock = planner.Plan(request, initial, AllHomeCustody());
            clock.IsAvailable = true;
            authority.AuthorizationStatus = WishgateDecisionStatus.Unavailable;
            WishgatePlanningResult missingAuthority = planner.Plan(request, initial, AllHomeCustody());
            authority.AuthorizationStatus = WishgateDecisionStatus.Rejected;
            WishgatePlanningResult denied = planner.Plan(request, initial, AllHomeCustody());
            authority.AuthorizationStatus = WishgateDecisionStatus.Accepted;
            WishgatePlanningResult stale = planner.Plan(
                EarnRequest(initial, expectedSnapshotRevision: 1),
                initial,
                AllHomeCustody());
            WishgateTransactionSnapshot maxSnapshot = Initial(long.MaxValue);
            WishgatePlanningResult snapshotOverflow = planner.Plan(
                EarnRequest(maxSnapshot),
                maxSnapshot,
                AllHomeCustody());

            Assert.That(missingCatalog.Status, Is.EqualTo(WishgatePlanStatus.Unavailable));
            Assert.That(missingClock.Status, Is.EqualTo(WishgatePlanStatus.Unavailable));
            Assert.That(missingAuthority.Status, Is.EqualTo(WishgatePlanStatus.Unavailable));
            Assert.That(denied.Status, Is.EqualTo(WishgatePlanStatus.Unauthorized));
            Assert.That(stale.Status, Is.EqualTo(WishgatePlanStatus.Stale));
            Assert.That(snapshotOverflow.Status, Is.EqualTo(WishgatePlanStatus.Overflow));

            WishgateTransactionSnapshot earned = Earned();
            WishgateEntitlementState maxState = CopyState(earned.Entitlement, long.MaxValue);
            WishgateTransitionRecord sourceRecord = earned.TransitionRecords.Single();
            var maxRecord = new WishgateTransitionRecord(
                sourceRecord.OperationId,
                sourceRecord.EventId,
                sourceRecord.CorrelationId,
                sourceRecord.Operation,
                sourceRecord.RequestFingerprint,
                sourceRecord.EntitlementId,
                sourceRecord.EarnReasonId,
                sourceRecord.RewardId,
                sourceRecord.RewardApplicationId,
                sourceRecord.ResultingPhase,
                sourceRecord.ResultingSnapshotRevision,
                long.MaxValue,
                sourceRecord.PlannedUtcSeconds,
                WishgateEntitlementPlanner.StateHash(maxState),
                sourceRecord.PlanHash,
                sourceRecord.PostCommitNotificationCorrelationId,
                true);
            var maxEntitlement = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                earned.Revision,
                maxState,
                new[] { maxRecord },
                true);
            WishgatePlanningResult entitlementOverflow = planner.Plan(
                SelectRequest(maxEntitlement),
                maxEntitlement);

            Assert.That(entitlementOverflow.Status, Is.EqualTo(WishgatePlanStatus.Overflow));
        }

        [Test]
        public void UnknownFutureStateAndRowsArePreservedButExcludedFromMutation()
        {
            WishgateTransactionSnapshot earned = Earned();
            string hash = new string('a', 64);
            var futureRecord = new WishgateTransitionRecord(
                "future_operation_001",
                "future_event_001",
                "future_correlation_001",
                (WishgateOperation)99,
                hash,
                EntitlementId,
                string.Empty,
                string.Empty,
                string.Empty,
                (WishgateEntitlementPhase)99,
                99,
                99,
                Now,
                hash,
                hash,
                string.Empty,
                false);
            List<WishgateTransitionRecord> records = earned.TransitionRecords.ToList();
            records.Add(futureRecord);
            var withFuture = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                earned.Revision,
                earned.Entitlement,
                records,
                true);

            WishgatePlanningResult known = planner.Plan(SelectRequest(withFuture), withFuture);
            var collidingRevisionRecord = new WishgateTransitionRecord(
                "future_operation_002",
                "future_event_002",
                "future_correlation_002",
                (WishgateOperation)99,
                hash,
                EntitlementId,
                string.Empty,
                string.Empty,
                string.Empty,
                (WishgateEntitlementPhase)99,
                earned.Revision + 1,
                99,
                Now,
                hash,
                hash,
                string.Empty,
                false);
            var withRevisionCollision = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                earned.Revision,
                earned.Entitlement,
                earned.TransitionRecords.Concat(new[] { collidingRevisionRecord }),
                true);
            WishgatePlanningResult revisionCollision = planner.Plan(
                SelectRequest(withRevisionCollision),
                withRevisionCollision);
            var collidingRequest = new WishgateTransactionRequest(
                WishgateOperation.SelectReward,
                futureRecord.OperationId,
                "different_event",
                "different_correlation",
                "actor_one",
                EntitlementId,
                string.Empty,
                RewardId,
                string.Empty,
                Now,
                earned.Revision,
                earned.Entitlement.Revision);
            WishgatePlanningResult collision = planner.Plan(collidingRequest, withFuture);
            var unsupportedState = new WishgateEntitlementState(
                (WishgateEntitlementPhase)99,
                "future_entitlement",
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0,
                1,
                false);
            var unsupportedSnapshot = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                1,
                unsupportedState,
                Array.Empty<WishgateTransitionRecord>(),
                true);
            WishgatePlanningResult unsupported = planner.Plan(
                SelectRequest(
                    unsupportedSnapshot,
                    entitlementId: unsupportedState.EntitlementId),
                unsupportedSnapshot);

            Assert.That(known.Status, Is.EqualTo(WishgatePlanStatus.Prepared));
            Assert.That(known.Plan.CandidateTransitionRecords.Last(), Is.SameAs(futureRecord));
            Assert.That(revisionCollision.Status, Is.EqualTo(WishgatePlanStatus.Unsupported));
            Assert.That(collision.Status, Is.EqualTo(WishgatePlanStatus.Unsupported));
            Assert.That(unsupported.Status, Is.EqualTo(WishgatePlanStatus.Unsupported));
        }

        [Test]
        public void OnlyInternalVerificationCanMintReceiptAndTamperingIsRejected()
        {
            Assert.That(
                typeof(WishgateVerifiedReceipt).GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Empty);
            WishgateTransactionSnapshot selected = Selected();
            WishgatePlanningResult apply = planner.Plan(ApplyRequest(selected), selected);
            var wrongSnapshot = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                apply.Plan.CandidateSnapshotRevision,
                selected.Entitlement,
                apply.Plan.CandidateTransitionRecords,
                true);

            Assert.That(
                WishgateEntitlementPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    apply.Plan,
                    wrongSnapshot,
                    out _),
                Is.False);

            WishgateTransactionSnapshot applied = Verify(apply, out WishgateVerifiedReceipt receipt);
            var tamperedReceipt = new WishgateVerifiedReceipt(
                receipt.TransitionRecord,
                receipt.VerifiedSnapshotRevision,
                receipt.VerifiedEntitlementRevision,
                new string('0', 64));
            WishgatePlanningResult tamperedReplay = planner.Plan(
                WithReceipt(ApplyRequest(selected), tamperedReceipt),
                applied);

            Assert.That(tamperedReplay.Status, Is.EqualTo(WishgatePlanStatus.Conflict));
        }

        [Test]
        public void EquivalentInputsProduceDeterministicUnambiguousPlans()
        {
            WishgateTransactionSnapshot initial = Initial();
            WishgateTransactionRequest firstRequest = EarnRequest(
                initial,
                operationId: "alpha|beta",
                eventId: "gamma");
            WishgateTransactionRequest secondRequest = EarnRequest(
                initial,
                operationId: "alpha",
                eventId: "beta|gamma");

            WishgatePlanningResult first = planner.Plan(firstRequest, initial, AllHomeCustody());
            WishgatePlanningResult equivalent = planner.Plan(firstRequest, initial, AllHomeCustody());
            WishgatePlanningResult second = planner.Plan(secondRequest, initial, AllHomeCustody());

            Assert.That(equivalent.Plan.RequestFingerprint, Is.EqualTo(first.Plan.RequestFingerprint));
            Assert.That(equivalent.Plan.PlanHash, Is.EqualTo(first.Plan.PlanHash));
            Assert.That(second.Plan.RequestFingerprint, Is.Not.EqualTo(first.Plan.RequestFingerprint));
        }

        private WishgateTransactionSnapshot Earned()
        {
            WishgateTransactionSnapshot initial = Initial();
            return Verify(
                planner.Plan(EarnRequest(initial), initial, AllHomeCustody()),
                out _);
        }

        private WishgateTransactionSnapshot Selected()
        {
            WishgateTransactionSnapshot earned = Earned();
            return Verify(planner.Plan(SelectRequest(earned), earned), out _);
        }

        private WishgateTransactionSnapshot Initial(long revision = 0)
        {
            return new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                revision,
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

        private RealmGemCustodySnapshot AllHomeCustody()
        {
            return new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                1,
                catalog.Entries.Select(entry => new RealmGemCustodyRecord(
                    entry.Id,
                    entry.HomeRealmId,
                    entry.HomeRealm,
                    entry.SaveSlotIndex,
                    RealmGemCustodyState.AtHome,
                    string.Empty,
                    0,
                    1,
                    true)));
        }

        private static WishgateTransactionSnapshot Verify(
            WishgatePlanningResult result,
            out WishgateVerifiedReceipt receipt)
        {
            Assert.That(result.IsPrepared, Is.True);
            WishgateTransactionPlan plan = result.Plan;
            var snapshot = new WishgateTransactionSnapshot(
                WishgateSnapshotStatus.Available,
                plan.CandidateSnapshotRevision,
                plan.CandidateEntitlement,
                plan.CandidateTransitionRecords,
                true);
            Assert.That(
                WishgateEntitlementPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    snapshot,
                    out receipt),
                Is.True);
            return snapshot;
        }

        private static WishgateTransactionRequest EarnRequest(
            WishgateTransactionSnapshot snapshot,
            string operationId = "wishgate_earn_operation",
            string eventId = "wishgate_earn_event",
            string earnReasonId = EarnReasonId,
            long? expectedSnapshotRevision = null)
        {
            return new WishgateTransactionRequest(
                WishgateOperation.Earn,
                operationId,
                eventId,
                "wishgate_earn_correlation",
                "actor_one",
                EntitlementId,
                earnReasonId,
                string.Empty,
                string.Empty,
                Now,
                expectedSnapshotRevision ?? snapshot.Revision,
                snapshot.Entitlement.Revision);
        }

        private static WishgateTransactionRequest SelectRequest(
            WishgateTransactionSnapshot snapshot,
            string operationId = "wishgate_select_operation",
            string eventId = "wishgate_select_event",
            string rewardId = RewardId,
            string entitlementId = EntitlementId)
        {
            return new WishgateTransactionRequest(
                WishgateOperation.SelectReward,
                operationId,
                eventId,
                "wishgate_select_correlation",
                "actor_one",
                entitlementId,
                string.Empty,
                rewardId,
                string.Empty,
                Now,
                snapshot.Revision,
                snapshot.Entitlement.Revision);
        }

        private static WishgateTransactionRequest ApplyRequest(
            WishgateTransactionSnapshot snapshot)
        {
            return new WishgateTransactionRequest(
                WishgateOperation.ApplyReward,
                "wishgate_apply_operation",
                "wishgate_apply_event",
                "wishgate_apply_correlation",
                "actor_one",
                EntitlementId,
                string.Empty,
                RewardId,
                ApplicationId,
                Now,
                snapshot.Revision,
                snapshot.Entitlement.Revision);
        }

        private static WishgateTransactionRequest CommitRequest(
            WishgateTransactionSnapshot snapshot)
        {
            return new WishgateTransactionRequest(
                WishgateOperation.Commit,
                "wishgate_commit_operation",
                "wishgate_commit_event",
                "wishgate_commit_correlation",
                "actor_one",
                EntitlementId,
                string.Empty,
                RewardId,
                ApplicationId,
                Now,
                snapshot.Revision,
                snapshot.Entitlement.Revision);
        }

        private static WishgateTransactionRequest WithReceipt(
            WishgateTransactionRequest request,
            WishgateVerifiedReceipt receipt)
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
                receipt);
        }

        private static WishgateEntitlementState CopyState(
            WishgateEntitlementState source,
            long revision)
        {
            return new WishgateEntitlementState(
                source.Phase,
                source.EntitlementId,
                source.EarnReasonId,
                source.RewardId,
                source.RewardApplicationId,
                source.EarnedUtcSeconds,
                source.SelectedUtcSeconds,
                source.AppliedUtcSeconds,
                source.CommittedUtcSeconds,
                revision,
                source.IsSupported);
        }

        private static void AssertDuplicate(
            WishgatePlanningResult result,
            bool hasReceipt)
        {
            Assert.That(result.Status, Is.EqualTo(WishgatePlanStatus.Duplicate));
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.ExistingRecord, Is.Not.Null);
            Assert.That(result.ExistingReceipt != null, Is.EqualTo(hasReceipt));
        }

        private sealed class FakeClock : IWishgateTransactionClock
        {
            public FakeClock(long now)
            {
                Now = now;
            }

            public long Now { get; set; }
            public bool IsAvailable { get; set; } = true;

            public bool TryGetUtcSeconds(out long utcSeconds)
            {
                utcSeconds = Now;
                return IsAvailable;
            }
        }

        private sealed class FakeAuthority : IWishgateTransactionAuthority
        {
            public WishgateLookupStatus EarnReasonStatus { get; set; } = WishgateLookupStatus.Found;
            public WishgateLookupStatus RewardStatus { get; set; } = WishgateLookupStatus.Found;
            public WishgateDecisionStatus EligibilityStatus { get; set; } = WishgateDecisionStatus.Accepted;
            public WishgateDecisionStatus AuthorizationStatus { get; set; } = WishgateDecisionStatus.Accepted;

            public WishgateLookupStatus ResolveEarnReason(string earnReasonId)
            {
                return EarnReasonStatus;
            }

            public WishgateLookupStatus ResolveReward(string rewardId)
            {
                return RewardStatus;
            }

            public WishgateDecisionStatus EvaluateEligibility(
                WishgateTransactionRequest request,
                RealmGemCatalogSnapshot realmGemCatalog,
                RealmGemCustodySnapshot custodySnapshot)
            {
                return EligibilityStatus;
            }

            public WishgateDecisionStatus Authorize(
                WishgateTransactionRequest request,
                WishgateEntitlementState currentEntitlement)
            {
                return AuthorizationStatus;
            }
        }
    }
}
