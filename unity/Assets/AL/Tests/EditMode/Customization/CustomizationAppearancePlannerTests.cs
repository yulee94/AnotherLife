using System;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Customization
{
    public class CustomizationAppearancePlannerTests
    {
        [Test]
        public void PrepareBuildsImmutablePlanWithoutMutatingAdapter()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var adapter = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior);
            string before = adapter.Current.Fingerprint;

            Assert.That(fixture.Result.Status,
                Is.EqualTo(AppearancePrepareStatus.Prepared));
            Assert.That(fixture.Result.Plan.Operations.Count, Is.EqualTo(1));
            Assert.That(fixture.Result.Plan.Operations[0].Field,
                Is.EqualTo(CustomizationField.HelmetEnabled));
            Assert.That(adapter.Current.Fingerprint, Is.EqualTo(before));
            Assert.That(adapter.HasPreview, Is.False);
            Assert.That(fixture.Result.Plan.PlanId,
                Is.EqualTo(fixture.Draft.Fingerprint));
        }

        [Test]
        public void ApplyVerifyAndRollbackRestoreExactPriorSnapshot()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var adapter = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior);

            AppearanceApplyStatus apply = adapter.ApplyAndVerify(
                fixture.Result.Plan, fixture.Model);
            AppearanceRollbackStatus rollback = adapter.Rollback(
                fixture.Result.Plan);

            Assert.That(apply, Is.EqualTo(AppearanceApplyStatus.AppliedAndVerified));
            Assert.That(adapter.Current.Fingerprint,
                Is.EqualTo(fixture.Result.Plan.Prior.Fingerprint));
            Assert.That(rollback, Is.EqualTo(AppearanceRollbackStatus.Restored));
            Assert.That(adapter.HasPreview, Is.False);
        }

        [Test]
        public void StaleModelRejectsBeforeMutation()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var adapter = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior);
            ModelCapabilitySnapshot newerModel =
                CustomizationPlannerTestFixtures.Model(revision: 2L);

            AppearanceApplyStatus result = adapter.ApplyAndVerify(
                fixture.Result.Plan, newerModel);

            Assert.That(result, Is.EqualTo(AppearanceApplyStatus.RejectedStaleModel));
            Assert.That(adapter.Current, Is.SameAs(fixture.Result.Plan.Prior));
            Assert.That(adapter.HasPreview, Is.False);
        }

        [Test]
        public void RequiredOperationFailureAndVerificationFailureAreAtomic()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var operationFailure = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior)
            {
                FailRequiredOperationIndex = 0
            };
            var verificationFailure = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior)
            {
                FailVerification = true
            };

            AppearanceApplyStatus operation = operationFailure.ApplyAndVerify(
                fixture.Result.Plan, fixture.Model);
            AppearanceApplyStatus verification = verificationFailure.ApplyAndVerify(
                fixture.Result.Plan, fixture.Model);

            Assert.That(operation,
                Is.EqualTo(AppearanceApplyStatus.FailedRequiredOperation));
            Assert.That(verification,
                Is.EqualTo(AppearanceApplyStatus.FailedVerification));
            Assert.That(operationFailure.Current,
                Is.SameAs(fixture.Result.Plan.Prior));
            Assert.That(verificationFailure.Current,
                Is.SameAs(fixture.Result.Plan.Prior));
        }

        [Test]
        public void MissingCapabilityInPlanRejectsBeforeMutation()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var hostilePlan = new AppearancePlan(
                fixture.Result.Plan.PlanId,
                fixture.Model.Fingerprint,
                fixture.Result.Plan.Prior,
                fixture.Result.Plan.Proposed,
                fixture.Draft.ProposedRawFingerprint,
                new[]
                {
                    new AppearanceOperation(
                        CustomizationField.HelmetEnabled,
                        "capability_not_present",
                        true)
                });
            var adapter = new FakeReversibleAppearanceAdapter(hostilePlan.Prior);

            AppearanceApplyStatus result = adapter.ApplyAndVerify(
                hostilePlan, fixture.Model);

            Assert.That(result,
                Is.EqualTo(AppearanceApplyStatus.RejectedMissingCapability));
            Assert.That(adapter.Current, Is.SameAs(hostilePlan.Prior));
        }

        [Test]
        public void RollbackFailureIsVisibleAndLeavesPreviewApplied()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var adapter = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior)
            {
                FailRollback = true
            };
            Assert.That(adapter.ApplyAndVerify(fixture.Result.Plan, fixture.Model),
                Is.EqualTo(AppearanceApplyStatus.AppliedAndVerified));

            AppearanceRollbackStatus result = adapter.Rollback(fixture.Result.Plan);

            Assert.That(result, Is.EqualTo(AppearanceRollbackStatus.Failed));
            Assert.That(adapter.Current, Is.SameAs(fixture.Result.Plan.Proposed));
            Assert.That(adapter.HasPreview, Is.True);
        }

        [Test]
        public void FailedDisposeRollbackKeepsAdapterRecoverableForRetry()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var adapter = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior)
            {
                FailRollback = true
            };
            Assert.That(adapter.ApplyAndVerify(fixture.Result.Plan, fixture.Model),
                Is.EqualTo(AppearanceApplyStatus.AppliedAndVerified));

            AppearanceRollbackStatus failed = adapter.DisposePreview();
            Assert.That(adapter.IsDisposed, Is.False);
            adapter.FailRollback = false;
            AppearanceRollbackStatus recovered = adapter.DisposePreview();

            Assert.That(failed, Is.EqualTo(AppearanceRollbackStatus.Failed));
            Assert.That(adapter.IsDisposed, Is.True);
            Assert.That(recovered, Is.EqualTo(AppearanceRollbackStatus.Restored));
            Assert.That(adapter.Current, Is.SameAs(fixture.Result.Plan.Prior));
        }

        [Test]
        public void DisposeBeforeApplyRejectsAndDisposeAfterPreviewRestoresPrior()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var beforeApply = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior);
            Assert.That(beforeApply.DisposePreview(),
                Is.EqualTo(AppearanceRollbackStatus.NotApplied));

            var afterPreview = new FakeReversibleAppearanceAdapter(
                fixture.Result.Plan.Prior);
            Assert.That(afterPreview.ApplyAndVerify(fixture.Result.Plan, fixture.Model),
                Is.EqualTo(AppearanceApplyStatus.AppliedAndVerified));
            AppearanceRollbackStatus dispose = afterPreview.DisposePreview();

            Assert.That(beforeApply.ApplyAndVerify(fixture.Result.Plan, fixture.Model),
                Is.EqualTo(AppearanceApplyStatus.Disposed));
            Assert.That(dispose, Is.EqualTo(AppearanceRollbackStatus.Restored));
            Assert.That(afterPreview.Current,
                Is.SameAs(fixture.Result.Plan.Prior));
            Assert.That(afterPreview.IsDisposed, Is.True);
        }

        [Test]
        public void AppearancePreparationRejectsCatalogAndModelRevisionDrift()
        {
            PreparedFixture fixture = PrepareFlagChange();
            CustomizationCatalogSnapshot newerCatalog =
                CustomizationPlannerTestFixtures.Catalog(
                    CustomizationPlannerTestFixtures.Candidate(
                        identity: CustomizationPlannerTestFixtures.Identity(
                            hash: new string('1', 64))));
            ModelCapabilitySnapshot newerModel =
                CustomizationPlannerTestFixtures.Model(revision: 2L);

            AppearancePrepareResult catalog = CustomizationAppearancePlanner.Prepare(
                fixture.Draft, newerCatalog, fixture.Model);
            AppearancePrepareResult model = CustomizationAppearancePlanner.Prepare(
                fixture.Draft, fixture.Catalog, newerModel);

            Assert.That(catalog.Status,
                Is.EqualTo(AppearancePrepareStatus.RejectedStaleCatalog));
            Assert.That(model.Status,
                Is.EqualTo(AppearancePrepareStatus.RejectedStaleModel));
            Assert.That(catalog.Plan, Is.Null);
            Assert.That(model.Plan, Is.Null);
        }

        [Test]
        public void CommitPlanCapturesAllExpectedRevisionsAndRejectsBadIdentity()
        {
            PreparedFixture fixture = PrepareFlagChange();

            CustomizationCommitPlan valid =
                CustomizationDraftPlanner.BuildCommitPlan(
                    "customization_operation_001",
                    fixture.Draft,
                    fixture.Result.Plan,
                    91L);
            CustomizationCommitPlan invalid =
                CustomizationDraftPlanner.BuildCommitPlan(
                    "bad\noperation",
                    fixture.Draft,
                    fixture.Result.Plan,
                    91L);

            Assert.That(valid, Is.Not.Null);
            Assert.That(valid.PlanHash, Has.Length.EqualTo(64));
            Assert.That(valid.Draft.BaseRawRevision,
                Is.EqualTo(fixture.Draft.BaseRawRevision));
            Assert.That(valid.ExpectedSaveCandidateRevision, Is.EqualTo(91L));
            Assert.That(invalid, Is.Null);
        }

        [Test]
        public void CommitPlanRejectsAppearanceThatDoesNotMatchDraftProposal()
        {
            PreparedFixture fixture = PrepareFlagChange();
            var mismatched = new AppearancePlan(
                fixture.Draft.Fingerprint,
                fixture.Model.Fingerprint,
                fixture.Result.Plan.Prior,
                fixture.Result.Plan.Prior,
                fixture.Draft.ProposedRawFingerprint,
                Array.Empty<AppearanceOperation>());

            CustomizationCommitPlan result =
                CustomizationDraftPlanner.BuildCommitPlan(
                    "customization_operation_mismatch",
                    fixture.Draft,
                    mismatched,
                    91L);

            Assert.That(result, Is.Null);
        }

        private static PreparedFixture PrepareFlagChange()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);
            CustomizationEditResult edit = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SetFlag(
                    CustomizationField.HelmetEnabled, true),
                CustomizationCatalogAvailability.Ready,
                catalog,
                model,
                draft.BaseRawRevision);
            AppearancePrepareResult result = CustomizationAppearancePlanner.Prepare(
                edit.Draft,
                catalog,
                model);
            return new PreparedFixture(catalog, model, edit.Draft, result);
        }

        private sealed class PreparedFixture
        {
            internal PreparedFixture(
                CustomizationCatalogSnapshot catalog,
                ModelCapabilitySnapshot model,
                CustomizationDraft draft,
                AppearancePrepareResult result)
            {
                Catalog = catalog;
                Model = model;
                Draft = draft;
                Result = result;
            }

            internal CustomizationCatalogSnapshot Catalog { get; }
            internal ModelCapabilitySnapshot Model { get; }
            internal CustomizationDraft Draft { get; }
            internal AppearancePrepareResult Result { get; }
        }
    }
}
