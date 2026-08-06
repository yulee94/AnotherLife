using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class CombatSkillLoadSessionPlannerTests
    {
        private const string OwnerId = "skill.load.owner.test";
        private const string RequestOneId = "skill.load.request.one";
        private const string RequestTwoId = "skill.load.request.two";

        [Test]
        public void InitialBeginAndRequestSnapshotAreExactAndImmutable()
        {
            Assert.False(
                CombatSkillLoadSessionPlanner.TryCreateInitial(
                    default,
                    out CombatSkillLoadSessionSnapshot invalid));
            Assert.IsNull(invalid);

            CombatSkillLoadSessionSnapshot initial = Initial();
            Assert.AreEqual(0L, initial.Generation);
            Assert.AreEqual(
                CombatSkillLoadStatus.Uninitialized,
                initial.Status);
            Assert.IsNull(initial.Request);
            Assert.False(initial.HasPublishedSnapshot);

            List<CombatSkillLoadExpectedSkill> source = ExpectedSkills();
            CombatSkillLoadRequest request = Request(expectedSkills: source);
            source[0] = ExpectedSkill(0, "skill.replaced");

            Assert.AreEqual(
                "skill.one",
                request.ExpectedSkillsInSlotOrder[0]
                    .SkillDefinitionId.Value);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CombatSkillLoadExpectedSkill>)
                    request.ExpectedSkillsInSlotOrder).Add(
                        ExpectedSkill(4, "skill.five")));

            CombatSkillLoadSessionPlan begin =
                CombatSkillLoadSessionPlanner.Begin(initial, request);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                begin.Status);
            Assert.AreSame(initial, begin.Before);
            Assert.AreEqual(1L, begin.After.Generation);
            Assert.AreEqual(CombatSkillLoadStatus.Loading, begin.After.Status);
            Assert.AreSame(request, begin.After.Request);
            Assert.False(begin.After.HasPublishedSnapshot);
            Assert.AreEqual(
                CombatSkillLoadOperationKind.Begin,
                begin.Receipt.OperationKind);
            Assert.AreEqual(request.RequestId, begin.Receipt.OperationId);
            Assert.False(begin.Receipt.SupersededPreviousRequest);

            CombatSkillLoadSessionPlan exactReplay =
                CombatSkillLoadSessionPlanner.Begin(begin.After, request);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.DuplicateExact,
                exactReplay.Status);
            Assert.AreSame(begin.After, exactReplay.After);

            CombatSkillLoadRequest conflict = Request(
                expectedLoadoutRawSha256: CombatContractTestData.HashB);
            CombatSkillLoadSessionPlan conflictPlan =
                CombatSkillLoadSessionPlanner.Begin(begin.After, conflict);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                conflictPlan.Status);
            Assert.AreSame(begin.After, conflictPlan.After);
        }

        [Test]
        public void LoadedCompletionPublishesTheExactValidatedSnapshotOnce()
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            CombatSkillLoadoutValidationResult validation =
                ValidLoadoutValidation();
            CombatSkillLoadCompletion completion =
                Completion(loading, validation);

            CombatSkillLoadSessionPlan loaded =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    completion);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                loaded.Status);
            Assert.AreEqual(CombatSkillLoadStatus.Loaded, loaded.After.Status);
            Assert.AreSame(
                validation.Snapshot,
                loaded.After.PublishedSnapshot);
            Assert.AreSame(
                validation.Validation,
                loaded.After.Validation);
            Assert.True(loaded.After.HasPublishedSnapshot);
            Assert.True(loaded.After.IsAuthoritative);
            Assert.False(loaded.After.IsDevelopmentFallback);
            Assert.AreSame(completion, loaded.Receipt.Completion);

            CombatSkillLoadSessionPlan replay =
                CombatSkillLoadSessionPlanner.Complete(
                    loaded.After,
                    completion);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.DuplicateExact,
                replay.Status);
            Assert.AreSame(loaded.After, replay.After);

            CombatSkillLoadCompletion reconstructed = Completion(
                loading,
                ValidLoadoutValidation());
            CombatSkillLoadSessionPlan reconstructedReplay =
                CombatSkillLoadSessionPlanner.Complete(
                    loaded.After,
                    reconstructed);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.DuplicateExact,
                reconstructedReplay.Status);
            Assert.AreSame(loaded.After, reconstructedReplay.After);

            CombatSkillLoadSessionPlan staleBeginReplay =
                CombatSkillLoadSessionPlanner.Begin(
                    loaded.After,
                    loading.Request);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                staleBeginReplay.Status);
            Assert.IsNull(staleBeginReplay.Receipt);
            Assert.AreSame(loaded.After, staleBeginReplay.After);

            CombatSkillLoadCompletion altered = new CombatSkillLoadCompletion(
                completion.CompletionId,
                completion.OwnerId,
                completion.RequestId,
                completion.Generation,
                CombatSkillLoadStatus.Cancelled,
                null,
                EmptyValidation());
            CombatSkillLoadSessionPlan conflict =
                CombatSkillLoadSessionPlanner.Complete(
                    loaded.After,
                    altered);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                conflict.Status);
            Assert.AreSame(loaded.After, conflict.After);
        }

        [TestCase("catalog")]
        [TestCase("loadout")]
        [TestCase("profile")]
        [TestCase("schema")]
        [TestCase("content")]
        [TestCase("source")]
        [TestCase("loadout-hash")]
        [TestCase("skill-id")]
        [TestCase("skill-schema")]
        [TestCase("skill-content")]
        [TestCase("skill-source")]
        [TestCase("skill-hash")]
        public void PublicationRequiresEveryExpectedIdentityAndProvenanceField(
            string mismatch)
        {
            CombatSkillLoadRequest request = RequestWithMismatch(mismatch);
            CombatSkillLoadSessionSnapshot loading = Started(request);
            CombatSkillLoadCompletion completion =
                Completion(loading, ValidLoadoutValidation());

            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    completion);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus
                    .RejectedPublicationIdentity,
                plan.Status);
            Assert.AreSame(loading, plan.After);
            Assert.False(plan.After.HasPublishedSnapshot);
            Assert.AreEqual(CombatSkillLoadStatus.Loading, plan.After.Status);
        }

        [TestCase(CombatSkillLoadStatus.MissingArtifact)]
        [TestCase(CombatSkillLoadStatus.ReadFailure)]
        [TestCase(CombatSkillLoadStatus.ParseFailure)]
        [TestCase(CombatSkillLoadStatus.InvalidCatalogIdentity)]
        [TestCase(CombatSkillLoadStatus.UnsupportedVersion)]
        [TestCase(CombatSkillLoadStatus.InvalidLoadout)]
        [TestCase(CombatSkillLoadStatus.InvalidSkill)]
        [TestCase(CombatSkillLoadStatus.InvalidReference)]
        [TestCase(CombatSkillLoadStatus.HashMismatch)]
        public void FailureOutcomesAreTypedUnavailableAndNeverPublish(
            CombatSkillLoadStatus outcome)
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            CombatValidationResult failure = FailureValidation(outcome);
            CombatSkillLoadCompletion completion = new CombatSkillLoadCompletion(
                Stable("skill.load.completion.failure"),
                loading.OwnerId,
                loading.Request.RequestId,
                loading.Generation,
                outcome,
                null,
                failure);

            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    completion);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                plan.Status);
            Assert.AreEqual(outcome, plan.After.Status);
            Assert.AreSame(failure, plan.After.Validation);
            Assert.False(plan.After.Validation.IsValid);
            Assert.False(plan.After.HasPublishedSnapshot);
            Assert.False(plan.After.IsAuthoritative);
            Assert.AreEqual(outcome, plan.ObservedLoadStatus);

            var reconstructed = new CombatSkillLoadCompletion(
                completion.CompletionId,
                completion.OwnerId,
                completion.RequestId,
                completion.Generation,
                completion.Outcome,
                null,
                FailureValidation(outcome));
            CombatSkillLoadSessionPlan replay =
                CombatSkillLoadSessionPlanner.Complete(
                    plan.After,
                    reconstructed);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.DuplicateExact,
                replay.Status);
            Assert.AreSame(plan.After, replay.After);
        }

        [TestCase(CombatSkillLoadStatus.Cancelled)]
        [TestCase(CombatSkillLoadStatus.Superseded)]
        public void NonErrorTerminalCompletionsRemainVisibleAndUnavailable(
            CombatSkillLoadStatus outcome)
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            CombatValidationResult validation = EmptyValidation();
            CombatSkillLoadCompletion completion = new CombatSkillLoadCompletion(
                Stable("skill.load.completion.terminal"),
                loading.OwnerId,
                loading.Request.RequestId,
                loading.Generation,
                outcome,
                null,
                validation);

            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    completion);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                plan.Status);
            Assert.AreEqual(outcome, plan.After.Status);
            Assert.False(plan.After.HasPublishedSnapshot);
            Assert.AreSame(validation, plan.After.Validation);
        }

        [Test]
        public void InvalidCompletionShapesFailClosedWithoutStateMutation()
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            CombatSkillLoadoutValidationResult valid =
                ValidLoadoutValidation();

            var invalid = new[]
            {
                new CombatSkillLoadCompletion(
                    Stable("completion.missing.snapshot"),
                    loading.OwnerId,
                    loading.Request.RequestId,
                    loading.Generation,
                    CombatSkillLoadStatus.Loaded,
                    null,
                    null),
                new CombatSkillLoadCompletion(
                    Stable("completion.failure.missing.validation"),
                    loading.OwnerId,
                    loading.Request.RequestId,
                    loading.Generation,
                    CombatSkillLoadStatus.ParseFailure,
                    null,
                    null),
                new CombatSkillLoadCompletion(
                    Stable("completion.failure.valid.validation"),
                    loading.OwnerId,
                    loading.Request.RequestId,
                    loading.Generation,
                    CombatSkillLoadStatus.ParseFailure,
                    null,
                    EmptyValidation()),
                new CombatSkillLoadCompletion(
                    Stable("completion.failure.with.snapshot"),
                    loading.OwnerId,
                    loading.Request.RequestId,
                    loading.Generation,
                    CombatSkillLoadStatus.InvalidLoadout,
                    valid,
                    null),
                new CombatSkillLoadCompletion(
                    Stable("completion.loading"),
                    loading.OwnerId,
                    loading.Request.RequestId,
                    loading.Generation,
                    CombatSkillLoadStatus.Loading,
                    null,
                    EmptyValidation())
            };

            foreach (CombatSkillLoadCompletion completion in invalid)
            {
                CombatSkillLoadSessionPlan plan =
                    CombatSkillLoadSessionPlanner.Complete(
                        loading,
                        completion);
                Assert.AreEqual(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedInvalidCompletion,
                    plan.Status,
                    completion.CompletionId.Value);
                Assert.AreSame(loading, plan.After);
            }
        }

        [Test]
        public void DevelopmentFallbackIsExplicitAndProhibitedForAuthority()
        {
            CombatSkillLoadSessionSnapshot allowed = Started(
                Request(
                    mode: CombatEncounterMode.DevelopmentDemo,
                    allowsDevelopmentFallback: true));
            CombatSkillLoadCompletion allowedCompletion = Completion(
                allowed,
                ValidLoadoutValidation(),
                CombatSkillLoadStatus.DevelopmentFallbackLoaded);
            CombatSkillLoadSessionPlan allowedPlan =
                CombatSkillLoadSessionPlanner.Complete(
                    allowed,
                    allowedCompletion);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                allowedPlan.Status);
            Assert.AreEqual(
                CombatSkillLoadStatus.DevelopmentFallbackLoaded,
                allowedPlan.After.Status);
            Assert.True(allowedPlan.After.IsDevelopmentFallback);
            Assert.False(allowedPlan.After.IsAuthoritative);
            Assert.True(allowedPlan.After.HasPublishedSnapshot);

            CombatSkillLoadSessionSnapshot notExplicit = Started(
                Request(
                    mode: CombatEncounterMode.DevelopmentDemo,
                    allowsDevelopmentFallback: false));
            AssertFallbackRejected(notExplicit);

            CombatSkillLoadSessionSnapshot authoritativeBoss = Started(
                Request(
                    mode: CombatEncounterMode.AuthoritativeBoss,
                    allowsDevelopmentFallback: true));
            AssertFallbackRejected(authoritativeBoss);

            CombatSkillLoadSessionSnapshot authoritativeQuest = Started(
                Request(
                    mode: CombatEncounterMode.AuthoritativeQuest,
                    allowsDevelopmentFallback: true));
            AssertFallbackRejected(authoritativeQuest);
        }

        [Test]
        public void NewBeginSupersedesLoadingAndLateCompletionCannotPublish()
        {
            CombatSkillLoadSessionSnapshot first = Started();
            CombatSkillLoadRequest secondRequest = Request(
                requestId: RequestTwoId,
                expectedPreviousGeneration: first.Generation);

            CombatSkillLoadSessionPlan secondBegin =
                CombatSkillLoadSessionPlanner.Begin(
                    first,
                    secondRequest);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                secondBegin.Status);
            Assert.AreEqual(2L, secondBegin.After.Generation);
            Assert.AreSame(secondRequest, secondBegin.After.Request);
            Assert.True(secondBegin.Receipt.SupersededPreviousRequest);
            Assert.AreEqual(
                first.Request.RequestId,
                secondBegin.Receipt.SupersededRequestId);

            CombatSkillLoadCompletion late = CompletionFor(
                first.OwnerId,
                first.Request.RequestId,
                first.Generation,
                ValidLoadoutValidation(),
                "skill.load.completion.late");
            CombatSkillLoadSessionPlan latePlan =
                CombatSkillLoadSessionPlanner.Complete(
                    secondBegin.After,
                    late);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.RejectedSuperseded,
                latePlan.Status);
            Assert.AreEqual(
                CombatSkillLoadStatus.Superseded,
                latePlan.ObservedLoadStatus);
            Assert.AreSame(secondBegin.After, latePlan.After);
            Assert.False(latePlan.After.HasPublishedSnapshot);

            CombatSkillLoadSessionPlan current =
                CombatSkillLoadSessionPlanner.Complete(
                    secondBegin.After,
                    Completion(
                        secondBegin.After,
                        ValidLoadoutValidation()));
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                current.Status);
            Assert.True(current.After.IsAuthoritative);
        }

        [Test]
        public void CancellationIsReplaySafeAndBlocksLateCompletion()
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            var cancellation = new CombatSkillLoadCancellation(
                Stable("skill.load.cancel.one"),
                loading.OwnerId,
                loading.Request.RequestId,
                loading.Generation);

            CombatSkillLoadSessionPlan cancelled =
                CombatSkillLoadSessionPlanner.Cancel(
                    loading,
                    cancellation);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                cancelled.Status);
            Assert.AreEqual(
                CombatSkillLoadStatus.Cancelled,
                cancelled.After.Status);
            Assert.False(cancelled.After.HasPublishedSnapshot);

            CombatSkillLoadSessionPlan replay =
                CombatSkillLoadSessionPlanner.Cancel(
                    cancelled.After,
                    new CombatSkillLoadCancellation(
                        cancellation.CancellationId,
                        cancellation.OwnerId,
                        cancellation.RequestId,
                        cancellation.Generation));
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.DuplicateExact,
                replay.Status);

            CombatSkillLoadSessionPlan conflict =
                CombatSkillLoadSessionPlanner.Cancel(
                    cancelled.After,
                    new CombatSkillLoadCancellation(
                        cancellation.CancellationId,
                        cancellation.OwnerId,
                        Stable("skill.load.request.other"),
                        cancellation.Generation));
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                conflict.Status);

            CombatSkillLoadSessionPlan late =
                CombatSkillLoadSessionPlanner.Complete(
                    cancelled.After,
                    Completion(
                        loading,
                        ValidLoadoutValidation()));
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.RejectedCancelled,
                late.Status);
            Assert.AreSame(cancelled.After, late.After);

            CombatSkillLoadRequest retry = Request(
                requestId: RequestTwoId,
                expectedPreviousGeneration: cancelled.After.Generation);
            CombatSkillLoadSessionPlan retryPlan =
                CombatSkillLoadSessionPlanner.Begin(
                    cancelled.After,
                    retry);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                retryPlan.Status);
            Assert.AreEqual(2L, retryPlan.After.Generation);
        }

        [Test]
        public void DisposalClearsAuthorityAndRejectsAllLaterOperations()
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            CombatSkillLoadSessionSnapshot loaded =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    Completion(loading, ValidLoadoutValidation())).After;
            Assert.True(loaded.HasPublishedSnapshot);

            var disposal = new CombatSkillLoadDisposal(
                Stable("skill.load.dispose.one"),
                loaded.OwnerId,
                loaded.Generation);
            CombatSkillLoadSessionPlan disposed =
                CombatSkillLoadSessionPlanner.Dispose(loaded, disposal);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                disposed.Status);
            Assert.True(disposed.After.IsDisposed);
            Assert.False(disposed.After.HasPublishedSnapshot);
            Assert.IsNull(disposed.After.PublishedSnapshot);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.DuplicateExact,
                CombatSkillLoadSessionPlanner.Dispose(
                    disposed.After,
                    new CombatSkillLoadDisposal(
                        disposal.DisposalId,
                        disposal.OwnerId,
                        disposal.ExpectedGeneration)).Status);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.RejectedDisposed,
                CombatSkillLoadSessionPlanner.Begin(
                    disposed.After,
                    Request(
                        requestId: RequestTwoId,
                        expectedPreviousGeneration:
                            disposed.After.Generation)).Status);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.RejectedDisposed,
                CombatSkillLoadSessionPlanner.Complete(
                    disposed.After,
                    Completion(
                        loading,
                        ValidLoadoutValidation())).Status);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.RejectedDisposed,
                CombatSkillLoadSessionPlanner.Cancel(
                    disposed.After,
                    new CombatSkillLoadCancellation(
                        Stable("skill.load.cancel.after.dispose"),
                        disposed.After.OwnerId,
                        loaded.Request.RequestId,
                        loaded.Generation)).Status);
        }

        [Test]
        public void OperationIdsCannotCrossRequestOrRetainedReceiptKinds()
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            CombatSkillLoadoutValidationResult validation =
                ValidLoadoutValidation();

            CombatSkillLoadCompletion requestIdAsCompletion =
                CompletionFor(
                    loading.OwnerId,
                    loading.Request.RequestId,
                    loading.Generation,
                    validation,
                    loading.Request.RequestId.Value);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    requestIdAsCompletion).Status);

            var requestIdAsCancellation =
                new CombatSkillLoadCancellation(
                    loading.Request.RequestId,
                    loading.OwnerId,
                    loading.Request.RequestId,
                    loading.Generation);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                CombatSkillLoadSessionPlanner.Cancel(
                    loading,
                    requestIdAsCancellation).Status);

            CombatSkillLoadSessionSnapshot loaded =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    Completion(loading, validation)).After;
            Assert.True(loaded.HasPublishedSnapshot);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                CombatSkillLoadSessionPlanner.Dispose(
                    loaded,
                    new CombatSkillLoadDisposal(
                        loaded.Request.RequestId,
                        loaded.OwnerId,
                        loaded.Generation)).Status);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                CombatSkillLoadSessionPlanner.Dispose(
                    loaded,
                    new CombatSkillLoadDisposal(
                        loaded.LatestReceipt.OperationId,
                        loaded.OwnerId,
                        loaded.Generation)).Status);

            CombatSkillLoadRequest reusedCompletionId = Request(
                requestId: loaded.LatestReceipt.OperationId.Value,
                expectedPreviousGeneration: loaded.Generation);
            CombatSkillLoadSessionPlan begin =
                CombatSkillLoadSessionPlanner.Begin(
                    loaded,
                    reusedCompletionId);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                begin.Status);
            Assert.AreSame(loaded, begin.After);
        }

        [Test]
        public void WrongOwnerRequestAndGenerationNeverMutateCurrentState()
        {
            CombatSkillLoadSessionSnapshot loading = Started();
            CombatSkillLoadoutValidationResult validation =
                ValidLoadoutValidation();

            CombatSkillLoadCompletion wrongOwner = CompletionFor(
                Stable("skill.load.owner.other"),
                loading.Request.RequestId,
                loading.Generation,
                validation,
                "skill.load.completion.wrong.owner");
            CombatSkillLoadCompletion wrongRequest = CompletionFor(
                loading.OwnerId,
                Stable("skill.load.request.other"),
                loading.Generation,
                validation,
                "skill.load.completion.wrong.request");
            CombatSkillLoadCompletion futureGeneration = CompletionFor(
                loading.OwnerId,
                loading.Request.RequestId,
                loading.Generation + 1L,
                validation,
                "skill.load.completion.future");

            AssertRejectedUnchanged(
                loading,
                wrongOwner,
                CombatSkillLoadSessionPlanStatus.RejectedWrongOwner);
            AssertRejectedUnchanged(
                loading,
                wrongRequest,
                CombatSkillLoadSessionPlanStatus.RejectedWrongRequest);
            AssertRejectedUnchanged(
                loading,
                futureGeneration,
                CombatSkillLoadSessionPlanStatus.RejectedWrongGeneration);
        }

        [Test]
        public void GenerationOverflowFailsClosedWithoutReplacingAuthority()
        {
            CombatSkillLoadRequest retainedRequest = Request(
                expectedPreviousGeneration: long.MaxValue - 1L);
            CombatSkillLoadoutValidationResult validation =
                ValidLoadoutValidation();
            var maxGeneration = new CombatSkillLoadSessionSnapshot(
                Stable(OwnerId),
                long.MaxValue,
                CombatSkillLoadStatus.Loaded,
                retainedRequest,
                validation.Snapshot,
                validation.Validation,
                null);
            CombatSkillLoadRequest next = Request(
                requestId: RequestTwoId,
                expectedPreviousGeneration: long.MaxValue);

            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Begin(
                    maxGeneration,
                    next);

            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus
                    .RejectedGenerationOverflow,
                plan.Status);
            Assert.AreSame(maxGeneration, plan.After);
            Assert.AreSame(
                validation.Snapshot,
                plan.After.PublishedSnapshot);
        }

        [Test]
        public void UnknownModeAndOutcomeFailClosed()
        {
            CombatSkillLoadSessionSnapshot initial = Initial();
            CombatSkillLoadRequest invalidMode = Request(
                mode: (CombatEncounterMode)999);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.RejectedInvalidRequest,
                CombatSkillLoadSessionPlanner.Begin(
                    initial,
                    invalidMode).Status);

            CombatSkillLoadSessionSnapshot loading = Started();
            var invalidOutcome = new CombatSkillLoadCompletion(
                Stable("skill.load.completion.unknown"),
                loading.OwnerId,
                loading.Request.RequestId,
                loading.Generation,
                (CombatSkillLoadStatus)999,
                null,
                EmptyValidation());
            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    invalidOutcome);
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.RejectedInvalidCompletion,
                plan.Status);
            Assert.AreSame(loading, plan.After);
        }

        [Test]
        public void SequentialSupersessionRetainsOnlyOneCurrentSession()
        {
            CombatSkillLoadSessionSnapshot current = Initial();
            const int attempts = 1_024;
            for (int index = 0; index < attempts; index++)
            {
                CombatSkillLoadRequest request = Request(
                    requestId: "skill.load.request." + index,
                    expectedPreviousGeneration: current.Generation);
                CombatSkillLoadSessionPlan plan =
                    CombatSkillLoadSessionPlanner.Begin(current, request);
                Assert.AreEqual(
                    CombatSkillLoadSessionPlanStatus.Applied,
                    plan.Status,
                    index.ToString());
                current = plan.After;
            }

            Assert.AreEqual(attempts, current.Generation);
            Assert.AreEqual(
                "skill.load.request." + (attempts - 1),
                current.Request.RequestId.Value);
            Assert.AreEqual(CombatSkillLoadStatus.Loading, current.Status);
            Assert.NotNull(current.LatestReceipt);
            Assert.True(current.LatestReceipt.SupersededPreviousRequest);

            AssertNoCollectionFields(
                typeof(CombatSkillLoadSessionSnapshot));
            AssertNoCollectionFields(
                typeof(CombatSkillLoadOperationReceipt));
        }

        private static void AssertFallbackRejected(
            CombatSkillLoadSessionSnapshot loading)
        {
            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    Completion(
                        loading,
                        ValidLoadoutValidation(),
                        CombatSkillLoadStatus
                            .DevelopmentFallbackLoaded));
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus
                    .RejectedAuthoritativeFallback,
                plan.Status);
            Assert.AreSame(loading, plan.After);
            Assert.False(plan.After.HasPublishedSnapshot);
        }

        private static void AssertRejectedUnchanged(
            CombatSkillLoadSessionSnapshot loading,
            CombatSkillLoadCompletion completion,
            CombatSkillLoadSessionPlanStatus expected)
        {
            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Complete(
                    loading,
                    completion);
            Assert.AreEqual(expected, plan.Status);
            Assert.AreSame(loading, plan.After);
            Assert.False(plan.After.HasPublishedSnapshot);
        }

        private static void AssertNoCollectionFields(Type type)
        {
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            FieldInfo[] collectionFields = fields
                .Where(field =>
                    field.FieldType != typeof(string) &&
                    typeof(IEnumerable).IsAssignableFrom(field.FieldType))
                .ToArray();
            Assert.IsEmpty(
                collectionFields,
                string.Join(", ", collectionFields.Select(
                    field => field.Name)));
        }

        private static CombatSkillLoadSessionSnapshot Initial()
        {
            Assert.True(
                CombatSkillLoadSessionPlanner.TryCreateInitial(
                    Stable(OwnerId),
                    out CombatSkillLoadSessionSnapshot initial));
            return initial;
        }

        private static CombatSkillLoadSessionSnapshot Started(
            CombatSkillLoadRequest request = null)
        {
            CombatSkillLoadSessionPlan plan =
                CombatSkillLoadSessionPlanner.Begin(
                    Initial(),
                    request ?? Request());
            Assert.AreEqual(
                CombatSkillLoadSessionPlanStatus.Applied,
                plan.Status);
            return plan.After;
        }

        private static CombatSkillLoadRequest Request(
            string requestId = RequestOneId,
            long expectedPreviousGeneration = 0L,
            CombatEncounterMode mode = CombatEncounterMode.AuthoritativeBoss,
            bool allowsDevelopmentFallback = false,
            string expectedCatalogSetId = null,
            string expectedLoadoutId = null,
            string expectedChampionOrClassProfileId = null,
            string expectedSchemaVersion = null,
            string expectedContentVersion = null,
            string expectedSourceRevision = null,
            string expectedLoadoutRawSha256 = null,
            IList<CombatSkillLoadExpectedSkill> expectedSkills = null)
        {
            return new CombatSkillLoadRequest(
                Stable(OwnerId),
                Stable(requestId),
                expectedPreviousGeneration,
                mode,
                allowsDevelopmentFallback,
                Stable(
                    expectedCatalogSetId ??
                    CombatContractTestData.CatalogSetId),
                Stable(expectedLoadoutId ?? "loadout.test"),
                Stable(
                    expectedChampionOrClassProfileId ??
                    CombatContractTestData.ChampionProfileId),
                Version(
                    expectedSchemaVersion ??
                    CombatContractTestData.SchemaVersion),
                Version(
                    expectedContentVersion ??
                    CombatContractTestData.ContentVersion),
                Version(
                    expectedSourceRevision ??
                    CombatContractTestData.SourceRevision),
                Hash(
                    expectedLoadoutRawSha256 ??
                    CombatContractTestData.HashA),
                expectedSkills ?? ExpectedSkills());
        }

        private static CombatSkillLoadRequest RequestWithMismatch(
            string mismatch)
        {
            List<CombatSkillLoadExpectedSkill> expected = ExpectedSkills();
            string catalog = null;
            string loadout = null;
            string profile = null;
            string schema = null;
            string content = null;
            string source = null;
            string loadoutHash = null;

            switch (mismatch)
            {
                case "catalog":
                    catalog = "combat.catalog.other";
                    break;
                case "loadout":
                    loadout = "loadout.other";
                    break;
                case "profile":
                    profile = "champion.profile.other";
                    break;
                case "schema":
                    schema = "2";
                    break;
                case "content":
                    content = "combat-v2";
                    break;
                case "source":
                    source = "source-r2";
                    break;
                case "loadout-hash":
                    loadoutHash = CombatContractTestData.HashB;
                    break;
                case "skill-id":
                    expected[0] = ExpectedSkill(0, "skill.other");
                    break;
                case "skill-schema":
                    expected[0] = ExpectedSkill(
                        0,
                        "skill.one",
                        schemaVersion: "2");
                    break;
                case "skill-content":
                    expected[0] = ExpectedSkill(
                        0,
                        "skill.one",
                        contentVersion: "combat-v2");
                    break;
                case "skill-source":
                    expected[0] = ExpectedSkill(
                        0,
                        "skill.one",
                        sourceRevision: "source-r2");
                    break;
                case "skill-hash":
                    expected[0] = ExpectedSkill(
                        0,
                        "skill.one",
                        rawSha256: CombatContractTestData.HashB);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mismatch));
            }

            return Request(
                expectedCatalogSetId: catalog,
                expectedLoadoutId: loadout,
                expectedChampionOrClassProfileId: profile,
                expectedSchemaVersion: schema,
                expectedContentVersion: content,
                expectedSourceRevision: source,
                expectedLoadoutRawSha256: loadoutHash,
                expectedSkills: expected);
        }

        private static List<CombatSkillLoadExpectedSkill> ExpectedSkills()
        {
            return new List<CombatSkillLoadExpectedSkill>
            {
                ExpectedSkill(0, "skill.one"),
                ExpectedSkill(1, "skill.two"),
                ExpectedSkill(2, "skill.three"),
                ExpectedSkill(3, "skill.four")
            };
        }

        private static CombatSkillLoadExpectedSkill ExpectedSkill(
            int slotIndex,
            string skillId,
            string schemaVersion = null,
            string contentVersion = null,
            string sourceRevision = null,
            string rawSha256 = null)
        {
            return new CombatSkillLoadExpectedSkill(
                slotIndex,
                Stable(skillId),
                Version(
                    schemaVersion ??
                    CombatContractTestData.SchemaVersion),
                Version(
                    contentVersion ??
                    CombatContractTestData.ContentVersion),
                Version(
                    sourceRevision ??
                    CombatContractTestData.SourceRevision),
                Hash(rawSha256 ?? CombatContractTestData.HashA));
        }

        private static CombatSkillLoadCompletion Completion(
            CombatSkillLoadSessionSnapshot loading,
            CombatSkillLoadoutValidationResult validation,
            CombatSkillLoadStatus outcome = CombatSkillLoadStatus.Loaded)
        {
            return CompletionFor(
                loading.OwnerId,
                loading.Request.RequestId,
                loading.Generation,
                validation,
                "skill.load.completion.one",
                outcome);
        }

        private static CombatSkillLoadCompletion CompletionFor(
            CombatStableId ownerId,
            CombatStableId requestId,
            long generation,
            CombatSkillLoadoutValidationResult validation,
            string completionId,
            CombatSkillLoadStatus outcome = CombatSkillLoadStatus.Loaded)
        {
            return new CombatSkillLoadCompletion(
                Stable(completionId),
                ownerId,
                requestId,
                generation,
                outcome,
                validation,
                null);
        }

        private static CombatSkillLoadoutValidationResult
            ValidLoadoutValidation()
        {
            CombatSkillLoadoutValidationResult validation =
                CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(),
                    CombatContractTestData.CreateSkills(),
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA);
            Assert.True(validation.IsValid);
            Assert.NotNull(validation.Snapshot);
            return validation;
        }

        private static CombatValidationResult FailureValidation(
            CombatSkillLoadStatus outcome)
        {
            return new CombatValidationResult(new[]
            {
                new CombatDiagnostic(
                    "AL-SKILL-LOADOUT-TEST-" +
                    outcome.ToString().ToUpperInvariant(),
                    CombatDiagnosticSeverity.Error,
                    CombatDiagnosticDomain.SkillLoadout,
                    "$.load",
                    "The structural load fixture failed.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter)
            });
        }

        private static CombatValidationResult EmptyValidation()
        {
            return new CombatValidationResult(new CombatDiagnostic[0]);
        }

        private static CombatStableId Stable(string value)
        {
            Assert.True(CombatStableId.TryCreate(value, out CombatStableId id));
            return id;
        }

        private static CombatContractVersion Version(string value)
        {
            Assert.True(
                CombatContractVersion.TryCreate(
                    value,
                    out CombatContractVersion version));
            return version;
        }

        private static CombatSha256 Hash(string value)
        {
            Assert.True(CombatSha256.TryCreate(value, out CombatSha256 hash));
            return hash;
        }
    }
}
