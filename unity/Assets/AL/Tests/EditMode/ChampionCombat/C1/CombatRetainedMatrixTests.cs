using System;
using System.Collections;
using System.Linq;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class CombatRetainedMatrixTests
    {
        [Test]
        public void EveryRequiredCategoryHasVersionedUniqueBoundedRows()
        {
            CombatRetainedMatrixRow[] rows =
                CombatRetainedMatrixCatalog.Rows.ToArray();
            CollectionAssert.AreEquivalent(
                Enum.GetValues(typeof(CombatRetainedMatrixCategory)),
                rows.Select(row => row.Category).Distinct().ToArray());
            Assert.AreEqual(
                rows.Length,
                rows.Select(row => row.RowId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());

            foreach (CombatRetainedMatrixRow row in rows)
            {
                Assert.True(
                    CombatPrimitiveValidation.IsStableId(row.RowId),
                    row.RowId);
                Assert.AreEqual(
                    CombatRetainedMatrixCatalog.SchemaVersion,
                    row.SchemaVersion,
                    row.RowId);
                Assert.AreEqual(
                    CombatRetainedMatrixCatalog.PolicyVersion,
                    row.PolicyVersion,
                    row.RowId);
                Assert.True(
                    CombatPrimitiveValidation.IsSupportedSchemaVersion(
                        row.SchemaVersion),
                    row.RowId);
                Assert.True(
                    CombatPrimitiveValidation.IsVersion(row.PolicyVersion),
                    row.RowId);
                Assert.That(row.InputState, Is.Not.Null.And.Not.Empty, row.RowId);
                Assert.That(row.Operation, Is.Not.Null.And.Not.Empty, row.RowId);
                Assert.That(
                    row.ExpectedStatus,
                    Is.Not.Null.And.Not.Empty,
                    row.RowId);
                Assert.That(
                    row.ExpectedRevisionDelta,
                    Is.EqualTo(0L).Or.EqualTo(1L),
                    row.RowId);
                Assert.False(
                    row.ExpectedEvents.Any(string.IsNullOrWhiteSpace),
                    row.RowId);
                Assert.AreEqual(
                    row.ExpectedEvents.Count,
                    row.ExpectedEvents
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    row.RowId);
            }

            Assert.That(
                CombatRetainedMatrixCatalog.ForCategory(
                    CombatRetainedMatrixCategory.Finite).Count,
                Is.GreaterThanOrEqualTo(6));
            Assert.That(
                CombatRetainedMatrixCatalog.ForCategory(
                    CombatRetainedMatrixCategory.Resource).Count,
                Is.GreaterThanOrEqualTo(6));
            Assert.That(
                CombatRetainedMatrixCatalog.ForCategory(
                    CombatRetainedMatrixCategory.Action).Count,
                Is.GreaterThanOrEqualTo(6));
            Assert.That(
                CombatRetainedMatrixCatalog.ForCategory(
                    CombatRetainedMatrixCategory.Encounter).Count,
                Is.GreaterThanOrEqualTo(7));
            Assert.That(
                CombatRetainedMatrixCatalog.ForCategory(
                    CombatRetainedMatrixCategory.Boss).Count,
                Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void MatricesRetainTerminalRecoveryDuplicateAndConflictSemantics()
        {
            AssertRow(
                "encounter.commit.uncertain",
                ChampionEncounterTransitionStatus.Applied.ToString(),
                1L,
                "EncounterTerminal:RecoveryRequired");
            AssertRow(
                "encounter.terminal.late-complete",
                ChampionEncounterTransitionStatus
                    .NoChangeTerminal.ToString(),
                0L);
            AssertRow(
                "boss.break.duplicate",
                BossStateTransitionStatus
                    .NoChangeAlreadyBroken.ToString(),
                0L);
            AssertRow(
                "boss.defeat",
                BossStateTransitionStatus
                    .AppliedAndDefeated.ToString(),
                1L,
                "BossDefeated");
            AssertRow(
                "action.replay.exact",
                CombatActionPlanStatus.DuplicateExact.ToString(),
                0L);
            AssertRow(
                "action.replay.conflict",
                CombatActionPlanStatus.CorrelationConflict.ToString(),
                0L);
            AssertRow(
                "resource.damage.defeat",
                CombatantResourcePlanStatus
                    .AppliedAndDefeated.ToString(),
                1L,
                "CombatantDefeated");
        }

        [Test]
        public void ResultReplayExamplesAreUniqueHashValidAndExactlyOnce()
        {
            CombatRetainedResultReplayExample[] examples =
                CombatRetainedMatrixCatalog.ResultReplayExamples.ToArray();
            Assert.AreEqual(
                examples.Length,
                examples.Select(value => value.ExampleId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            foreach (CombatRetainedResultReplayExample example in examples)
            {
                Assert.True(
                    CombatPrimitiveValidation.IsStableId(example.ExampleId),
                    example.ExampleId);
                Assert.True(
                    CombatPrimitiveValidation.IsStableId(
                        example.EncounterResultId),
                    example.ExampleId);
                Assert.True(
                    CombatPrimitiveValidation.IsSha256(
                        example.OriginalOutcomeHash),
                    example.ExampleId);
                Assert.True(
                    CombatPrimitiveValidation.IsSha256(
                        example.ReplayOutcomeHash),
                    example.ExampleId);
                Assert.AreEqual(
                    CombatRetainedMatrixCatalog.SchemaVersion,
                    example.SchemaVersion);
                Assert.AreEqual(
                    CombatRetainedMatrixCatalog.PolicyVersion,
                    example.PolicyVersion);
                Assert.That(example.ExpectedResultApplications, Is.GreaterThanOrEqualTo(0));
                Assert.That(example.ExpectedRewardApplications, Is.GreaterThanOrEqualTo(0));
                Assert.That(example.ExpectedPresentationEvents, Is.GreaterThanOrEqualTo(0));
            }

            CombatRetainedResultReplayExample first =
                examples.Single(value =>
                    value.ExampleId ==
                    "result.authoritative.first");
            Assert.AreEqual("Planned", first.ExpectedStatus);
            Assert.AreEqual(1, first.ExpectedResultApplications);
            Assert.AreEqual(1, first.ExpectedRewardApplications);
            Assert.AreEqual(1, first.ExpectedPresentationEvents);

            CombatRetainedResultReplayExample exact =
                examples.Single(value =>
                    value.ExampleId ==
                    "result.authoritative.exact-replay");
            Assert.AreEqual("DuplicateExact", exact.ExpectedStatus);
            Assert.AreEqual(0, exact.ExpectedResultApplications);
            Assert.AreEqual(0, exact.ExpectedRewardApplications);
            Assert.AreEqual(0, exact.ExpectedPresentationEvents);

            CombatRetainedResultReplayExample conflict =
                examples.Single(value =>
                    value.ExampleId ==
                    "result.authoritative.conflict");
            Assert.AreEqual("CorrelationConflict", conflict.ExpectedStatus);
            Assert.AreNotEqual(
                conflict.OriginalOutcomeHash,
                conflict.ReplayOutcomeHash);

            Assert.True(examples.Any(value =>
                value.ExpectedStatus == "RecoveryRequired"));
        }

        [Test]
        public void DiagnosticMatrixMatchesFailClosedNullAndOrdinalOrdering()
        {
            CombatRetainedMatrixRow nullRow =
                CombatRetainedMatrixCatalog.Rows.Single(row =>
                    row.RowId == "diagnostic.null-elision");
            Assert.AreEqual(
                "RejectedNullDiagnostic",
                nullRow.ExpectedStatus);
            Assert.Throws<ArgumentException>(() =>
                CombatDiagnosticOrdering.Order(
                    new CombatDiagnostic[] { null }));

            var codeB = new CombatDiagnostic(
                "AL-BOSS-STATE-B",
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.BossState,
                "b",
                "b",
                CombatBlockScope.Action);
            var codeA = new CombatDiagnostic(
                "AL-BOSS-STATE-A",
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.BossState,
                "a",
                "a",
                CombatBlockScope.Action);
            CollectionAssert.AreEqual(
                new[]
                {
                    "AL-BOSS-STATE-A",
                    "AL-BOSS-STATE-B"
                },
                CombatDiagnosticOrdering.Order(new[] { codeB, codeA })
                    .Select(value => value.Code));
        }

        [Test]
        public void CatalogCollectionsCannotBeMutated()
        {
            Assert.Throws<NotSupportedException>(() =>
                ((IList)CombatRetainedMatrixCatalog.Rows).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList)CombatRetainedMatrixCatalog.ResultReplayExamples)
                .Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList)CombatRetainedMatrixCatalog.ForCategory(
                    CombatRetainedMatrixCategory.Boss)).Clear());
        }

        private static void AssertRow(
            string rowId,
            string expectedStatus,
            long revisionDelta,
            params string[] expectedEvents)
        {
            CombatRetainedMatrixRow row =
                CombatRetainedMatrixCatalog.Rows.Single(value =>
                    value.RowId == rowId);
            Assert.AreEqual(expectedStatus, row.ExpectedStatus);
            Assert.AreEqual(revisionDelta, row.ExpectedRevisionDelta);
            foreach (string expectedEvent in expectedEvents)
            {
                CollectionAssert.Contains(
                    row.ExpectedEvents,
                    expectedEvent);
            }
        }
    }
}
