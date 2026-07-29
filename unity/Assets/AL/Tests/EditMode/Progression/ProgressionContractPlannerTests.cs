using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Kingdom.Progression;
using NUnit.Framework;

namespace AL.Tests.EditMode.Progression
{
    public sealed class ProgressionCompatibilityPlannerTests
    {
        [Test]
        public void EmptyResearchStateBuildsOrderedEffectiveSnapshotsWithoutMutation()
        {
            ResearchProgressionDefinition beta = Fixture.Research("fake.research.beta");
            ResearchProgressionDefinition alpha = Fixture.Research("fake.research.alpha");
            var definitions = new List<ResearchProgressionDefinition> { beta, alpha };
            var states = new List<ResearchProgressionStateRecord>();

            ProgressionCompatibilityResult first =
                Fixture.ResearchCompatibility(definitions, states);
            ProgressionCompatibilityResult second =
                Fixture.ResearchCompatibility(definitions, states);

            Assert.That(first.Status, Is.EqualTo(ProgressionCompatibilityStatus.Available));
            Assert.That(
                first.Research.Select(snapshot => snapshot.Definition.Identity.Id),
                Is.EqualTo(new[] { "fake.research.alpha", "fake.research.beta" }));
            Assert.That(
                first.Research.All(snapshot =>
                    snapshot.Origin ==
                    ProgressionStateOrigin.EffectiveInitialUnpersisted),
                Is.True);
            Assert.That(first.Research.All(snapshot => snapshot.Level == 0), Is.True);
            Assert.That(first.StateRevision, Has.Length.EqualTo(64));
            Assert.That(second.StateRevision, Is.EqualTo(first.StateRevision));
            Assert.That(definitions, Is.EqualTo(new[] { beta, alpha }));
            Assert.That(states, Is.Empty);
            Assert.That(
                () => ((IList<ResearchProgressionSnapshot>)first.Research).Add(
                    first.Research[0]),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void NullBlankAndDuplicateResearchDefinitionsFailClosedDeterministically()
        {
            ResearchProgressionDefinition alpha = Fixture.Research("fake.research.alpha");
            ResearchProgressionDefinition duplicate =
                Fixture.Research("fake.research.alpha");
            ResearchProgressionDefinition blank = Fixture.Research(string.Empty);
            var definitions = new List<ResearchProgressionDefinition>
            {
                duplicate,
                null,
                blank,
                alpha
            };
            var row = new ResearchProgressionStateRecord(
                "fake.research.alpha",
                Fixture.ContentVersion,
                0,
                false,
                0);

            ProgressionCompatibilityResult first =
                Fixture.ResearchCompatibility(definitions, new[] { row });
            ProgressionCompatibilityResult second =
                Fixture.ResearchCompatibility(definitions, new[] { row });

            Assert.That(
                first.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.UnavailableCatalog));
            Assert.That(first.Research, Is.Empty);
            Assert.That(first.StateRevision, Has.Length.EqualTo(64));
            Assert.That(first.PreservedResearchStates, Has.Count.EqualTo(1));
            Assert.That(first.PreservedResearchStates[0], Is.SameAs(row));
            Assert.That(
                first.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.NullDefinition));
            Assert.That(
                first.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.BlankDefinitionId));
            Assert.That(
                first.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.DuplicateDefinitionId));
            Assert.That(
                first.Diagnostics.Single(diagnostic =>
                    diagnostic.Code == ProgressionDiagnosticCode.NullDefinition)
                    .SourceIndex,
                Is.EqualTo(1));
            Assert.That(
                first.Diagnostics.Single(diagnostic =>
                    diagnostic.Code ==
                    ProgressionDiagnosticCode.DuplicateDefinitionId)
                    .SourceIndex,
                Is.EqualTo(0));
            Assert.That(Fixture.Diagnostics(second), Is.EqualTo(Fixture.Diagnostics(first)));
            Assert.That(second.StateRevision, Is.EqualTo(first.StateRevision));
            Assert.That(definitions, Is.EqualTo(new[] { duplicate, null, blank, alpha }));
        }

        [Test]
        public void MalformedResearchRowsArePreservedAndNeverBecomePartialSnapshots()
        {
            ResearchProgressionDefinition alpha = Fixture.Research("fake.research.alpha");
            ResearchProgressionDefinition beta = Fixture.Research("fake.research.beta");
            var states = new List<ResearchProgressionStateRecord>
            {
                new ResearchProgressionStateRecord(
                    "fake.research.alpha",
                    Fixture.ContentVersion,
                    0,
                    false,
                    0),
                null,
                new ResearchProgressionStateRecord(
                    string.Empty,
                    Fixture.ContentVersion,
                    0,
                    false,
                    0),
                new ResearchProgressionStateRecord(
                    "fake.research.future",
                    Fixture.ContentVersion,
                    0,
                    false,
                    0),
                new ResearchProgressionStateRecord(
                    "fake.research.alpha",
                    Fixture.ContentVersion,
                    1,
                    false,
                    0),
                new ResearchProgressionStateRecord(
                    "fake.research.beta",
                    Fixture.ContentVersion,
                    -1,
                    false,
                    0)
            };
            ResearchProgressionStateRecord[] original = states.ToArray();

            ProgressionCompatibilityResult result =
                Fixture.ResearchCompatibility(new[] { beta, alpha }, states);

            Assert.That(
                result.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.MalformedState));
            Assert.That(result.Research, Is.Empty);
            Assert.That(result.PreservedResearchStates, Has.Count.EqualTo(states.Count));
            Assert.That(result.PreservedResearchStates, Is.EqualTo(original));
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.NullState));
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.BlankStateId));
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(
                    ProgressionDiagnosticCode.PreservedUnknownFutureDefinition));
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.DuplicateStateId));
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.NegativeLevel));
            Assert.That(states, Is.EqualTo(original));
        }

        [Test]
        public void ResearchLevelVersionAndTimerFaultsHaveTypedFailClosedDiagnostics()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha", maximumLevel: 3);
            var fixtures = new[]
            {
                new
                {
                    Row = new ResearchProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        -1,
                        false,
                        0),
                    Code = ProgressionDiagnosticCode.NegativeLevel
                },
                new
                {
                    Row = new ResearchProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        4,
                        false,
                        0),
                    Code = ProgressionDiagnosticCode.OverMaximumLevel
                },
                new
                {
                    Row = new ResearchProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        1,
                        true,
                        0),
                    Code = ProgressionDiagnosticCode.ImpossibleTimer
                },
                new
                {
                    Row = new ResearchProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        1,
                        true,
                        100),
                    Code = ProgressionDiagnosticCode.MigrationRequired
                },
                new
                {
                    Row = new ResearchProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        1,
                        false,
                        100),
                    Code = ProgressionDiagnosticCode.MigrationRequired
                },
                new
                {
                    Row = new ResearchProgressionStateRecord(
                        definition.Identity.Id,
                        "unsupported-content",
                        1,
                        false,
                        0),
                    Code = ProgressionDiagnosticCode.UnsupportedContentVersion
                }
            };

            foreach (var fixture in fixtures)
            {
                ProgressionCompatibilityResult result =
                    Fixture.ResearchCompatibility(
                        new[] { definition },
                        new[] { fixture.Row });

                Assert.That(
                    result.Status,
                    Is.EqualTo(ProgressionCompatibilityStatus.MalformedState),
                    fixture.Code.ToString());
                Assert.That(result.Research, Is.Empty, fixture.Code.ToString());
                Assert.That(
                    result.Diagnostics.Select(diagnostic => diagnostic.Code),
                    Does.Contain(fixture.Code),
                    fixture.Code.ToString());
            }
        }

        [Test]
        public void NullStateCollectionIsDistinctFromEmptyEffectiveState()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");

            ProgressionCompatibilityResult missing =
                ProgressionCompatibilityPlanner.BuildResearchCompatibility(
                    Fixture.CatalogSetId,
                    Fixture.CatalogRevision,
                    new[] { definition },
                    null);
            ProgressionCompatibilityResult empty =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());

            Assert.That(
                missing.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.MalformedState));
            Assert.That(
                missing.Diagnostics.Single().Code,
                Is.EqualTo(ProgressionDiagnosticCode.NullStateCollection));
            Assert.That(empty.Status, Is.EqualTo(ProgressionCompatibilityStatus.Available));
            Assert.That(empty.Research, Has.Count.EqualTo(1));
        }

        [Test]
        public void TrainingUsesExplicitSeparatedInventoryPolicyAndEffectiveDefaults()
        {
            TroopProgressionDefinition beta = Fixture.Troop("fake.troop.beta");
            TroopProgressionDefinition alpha = Fixture.Troop("fake.troop.alpha");
            var row = new TroopProgressionStateRecord(
                alpha.Identity.Id,
                alpha.Identity.ContentVersion,
                10,
                2,
                3);

            ProgressionCompatibilityResult result =
                Fixture.TrainingCompatibility(
                    new[] { beta, alpha },
                    new[] { row });

            Assert.That(result.Status, Is.EqualTo(ProgressionCompatibilityStatus.Available));
            Assert.That(
                result.Troops.Select(snapshot => snapshot.Definition.Identity.Id),
                Is.EqualTo(new[] { alpha.Identity.Id, beta.Identity.Id }));
            Assert.That(result.Troops[0].ActiveCount, Is.EqualTo(10));
            Assert.That(result.Troops[0].WoundedCount, Is.EqualTo(2));
            Assert.That(result.Troops[0].ReservedCount, Is.EqualTo(3));
            Assert.That(
                result.Troops[0].Origin,
                Is.EqualTo(ProgressionStateOrigin.Saved));
            Assert.That(
                result.Troops[1].Origin,
                Is.EqualTo(ProgressionStateOrigin.EffectiveInitialUnpersisted));
            Assert.That(result.StateRevision, Has.Length.EqualTo(64));
        }

        [Test]
        public void TrainingDefinitionAndCountFaultsFailClosedWithoutOverflow()
        {
            TroopProgressionDefinition definition =
                Fixture.Troop("fake.troop.alpha", maximumInventory: 100);
            TroopProgressionDefinition invalidPolicy = Fixture.Troop(
                "fake.troop.invalid-policy",
                inventoryPolicy: null,
                useDefaultInventoryPolicy: false);

            ProgressionCompatibilityResult invalidDefinition =
                Fixture.TrainingCompatibility(
                    new[] { invalidPolicy },
                    Array.Empty<TroopProgressionStateRecord>());
            Assert.That(
                invalidDefinition.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.UnavailableCatalog));
            Assert.That(
                invalidDefinition.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InvalidInventoryPolicy));

            var faults = new[]
            {
                new
                {
                    Row = new TroopProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        -1,
                        0,
                        0),
                    Code = ProgressionDiagnosticCode.NegativeCount
                },
                new
                {
                    Row = new TroopProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        90,
                        6,
                        5),
                    Code = ProgressionDiagnosticCode.OverMaximumCount
                },
                new
                {
                    Row = new TroopProgressionStateRecord(
                        definition.Identity.Id,
                        definition.Identity.ContentVersion,
                        long.MaxValue,
                        1,
                        0),
                    Code = ProgressionDiagnosticCode.CountOverflow
                }
            };

            foreach (var fault in faults)
            {
                ProgressionCompatibilityResult result =
                    Fixture.TrainingCompatibility(
                        new[] { definition },
                        new[] { fault.Row });
                Assert.That(
                    result.Status,
                    Is.EqualTo(ProgressionCompatibilityStatus.MalformedState),
                    fault.Code.ToString());
                Assert.That(result.Troops, Is.Empty);
                Assert.That(
                    result.Diagnostics.Select(diagnostic => diagnostic.Code),
                    Does.Contain(fault.Code));
            }
        }

        [Test]
        public void DuplicateTroopRowsAndOversizedDefinitionInputAreBounded()
        {
            TroopProgressionDefinition definition =
                Fixture.Troop("fake.troop.alpha");
            var duplicateRows = new[]
            {
                new TroopProgressionStateRecord(
                    definition.Identity.Id,
                    definition.Identity.ContentVersion,
                    1,
                    0,
                    0),
                new TroopProgressionStateRecord(
                    definition.Identity.Id,
                    definition.Identity.ContentVersion,
                    2,
                    0,
                    0)
            };

            ProgressionCompatibilityResult duplicate =
                Fixture.TrainingCompatibility(
                    new[] { definition },
                    duplicateRows);
            Assert.That(duplicate.Troops, Is.Empty);
            Assert.That(
                duplicate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.DuplicateStateId));

            List<TroopProgressionDefinition> oversized = Enumerable
                .Range(0, ProgressionCompatibilityPlanner.MaximumDefinitions + 1)
                .Select(index => Fixture.Troop($"fake.troop.{index:D3}"))
                .ToList();
            ProgressionCompatibilityResult bounded =
                Fixture.TrainingCompatibility(
                    oversized,
                    Array.Empty<TroopProgressionStateRecord>());

            Assert.That(
                bounded.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.UnavailableCatalog));
            Assert.That(bounded.Troops, Is.Empty);
            Assert.That(
                bounded.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InputLimitExceeded));
            Assert.That(
                oversized,
                Has.Count.EqualTo(
                    ProgressionCompatibilityPlanner.MaximumDefinitions + 1));
        }

        [Test]
        public void MalformedRawRowsPreserveNullsExactIndicesAndDistinctRevisions()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            var valid = new ResearchProgressionStateRecord(
                definition.Identity.Id,
                definition.Identity.ContentVersion,
                0,
                false,
                0);
            ProgressionCompatibilityResult nullRow =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    new ResearchProgressionStateRecord[] { null });
            ProgressionCompatibilityResult nullStrings =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            null,
                            null,
                            0,
                            false,
                            0)
                    });
            ProgressionCompatibilityResult emptyStrings =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            string.Empty,
                            string.Empty,
                            0,
                            false,
                            0)
                    });
            ProgressionCompatibilityResult unknown =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            "fake.research.future",
                            Fixture.ContentVersion,
                            0,
                            false,
                            0)
                    });
            ProgressionCompatibilityResult duplicate =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    new ResearchProgressionStateRecord[] { null, valid, valid });

            string[] revisions =
            {
                nullRow.StateRevision,
                nullStrings.StateRevision,
                emptyStrings.StateRevision,
                unknown.StateRevision,
                duplicate.StateRevision
            };
            Assert.That(revisions.All(revision => revision.Length == 64), Is.True);
            Assert.That(
                revisions.Distinct(StringComparer.Ordinal).ToArray(),
                Has.Length.EqualTo(5));
            Assert.That(
                nullStrings.PreservedResearchStates[0].DefinitionId,
                Is.Null);
            Assert.That(
                nullStrings.PreservedResearchStates[0].DefinitionContentVersion,
                Is.Null);
            Assert.That(
                duplicate.Diagnostics.Single(diagnostic =>
                    diagnostic.Code == ProgressionDiagnosticCode.NullState)
                    .SourceIndex,
                Is.EqualTo(0));
            Assert.That(
                duplicate.Diagnostics.Single(diagnostic =>
                    diagnostic.Code ==
                    ProgressionDiagnosticCode.DuplicateStateId)
                    .SourceIndex,
                Is.EqualTo(1));
        }

        [Test]
        public void BelowInitialAndInvalidTimerPoliciesFailClosedWithTypedDiagnostics()
        {
            ResearchProgressionDefinition definition = Fixture.Research(
                "fake.research.alpha",
                initialLevel: 2,
                maximumLevel: 5);
            ProgressionCompatibilityResult belowInitial =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            definition.Identity.Id,
                            definition.Identity.ContentVersion,
                            1,
                            false,
                            0)
                    });
            var timed = new ResearchProgressionStateRecord(
                definition.Identity.Id,
                definition.Identity.ContentVersion,
                2,
                true,
                long.MaxValue);
            ProgressionCompatibilityResult missingPolicy =
                ProgressionCompatibilityPlanner.BuildResearchCompatibility(
                    Fixture.CatalogSetId,
                    Fixture.CatalogRevision,
                    new[] { definition },
                    new[] { timed });
            ProgressionCompatibilityResult outsidePolicy =
                ProgressionCompatibilityPlanner.BuildResearchCompatibility(
                    Fixture.CatalogSetId,
                    Fixture.CatalogRevision,
                    new[] { definition },
                    new[] { timed },
                    null,
                    new ProgressionTimestampPolicy("timestamp.policy.v1", 1, 100));

            Assert.That(
                belowInitial.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.BelowInitialLevel));
            Assert.That(
                missingPolicy.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InvalidTimestampPolicy));
            Assert.That(
                outsidePolicy.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.ImpossibleTimer));
            Assert.That(belowInitial.Research, Is.Empty);
            Assert.That(missingPolicy.Research, Is.Empty);
            Assert.That(outsidePolicy.Research, Is.Empty);
        }

        [Test]
        public void UnresolvedPrerequisitesAndInventoryCapacityPolicyDisableDefinitions()
        {
            var prerequisite = new ProgressionPrerequisite(
                "fake.building.academy",
                1);
            ResearchProgressionDefinition research = Fixture.Research(
                "fake.research.alpha",
                prerequisites: new[] { prerequisite });
            TroopProgressionDefinition troop = Fixture.Troop(
                "fake.troop.alpha",
                inventoryCapacityPolicy:
                    TroopInventoryCapacityPolicy.Unresolved);

            ProgressionCompatibilityResult researchResult =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionCompatibilityResult troopResult =
                Fixture.TrainingCompatibility(
                    new[] { troop },
                    Array.Empty<TroopProgressionStateRecord>());

            Assert.That(
                researchResult.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.UnavailableCatalog));
            Assert.That(
                researchResult.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InvalidPrerequisite));
            Assert.That(
                troopResult.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.UnavailableCatalog));
            Assert.That(
                troopResult.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InvalidInventoryPolicy));
        }
    }

    public sealed class ProgressionOrderPlannerTests
    {
        [Test]
        public void ResearchStartPlansCheckedCostDurationAndStableHashes()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartRequest request = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1);
            ProgressionEconomySnapshot economy =
                Fixture.Economy(ResourceType.Gold, 1000);

            ProgressionStartPlan first = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                1000);
            ProgressionStartPlan second = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                1000);

            Assert.That(first.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(first.PreviousValue, Is.Zero);
            Assert.That(first.TargetValue, Is.EqualTo(1));
            Assert.That(first.Costs, Has.Count.EqualTo(1));
            Assert.That(first.Costs[0].ResourceType, Is.EqualTo(ResourceType.Gold));
            Assert.That(first.Costs[0].Amount, Is.EqualTo(200));
            Assert.That(first.StartTimestamp, Is.EqualTo(1000));
            Assert.That(first.EndTimestamp, Is.EqualTo(1015));
            Assert.That(first.SemanticHash, Has.Length.EqualTo(64));
            Assert.That(first.PlanHash, Has.Length.EqualTo(64));
            Assert.That(second.SemanticHash, Is.EqualTo(first.SemanticHash));
            Assert.That(second.PlanHash, Is.EqualTo(first.PlanHash));
            Assert.That(compatibility.PreservedResearchStates, Is.Empty);
            Assert.That(
                () => ((IList<ProgressionDiagnostic>)first.Diagnostics).Add(
                    new ProgressionDiagnostic(
                        ProgressionDiagnosticCode.None,
                        ProgressionDomain.Research,
                        string.Empty,
                        0)),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void ReadyStartPlanHashBindsFullDefinitionAndProfileSources()
        {
            const string definitionId = "fake.research.alpha";
            ResearchProgressionDefinition baseline =
                Fixture.Research(definitionId);
            var changedSources = new ResearchProgressionDefinition(
                Fixture.Identity(
                    definitionId,
                    ProgressionCompatibilityPlanner.ResearchSchemaVersion,
                    "fake.source.rev.2",
                    'b'),
                baseline.InitialLevel,
                baseline.MaximumLevel,
                new ProgressionCostProfile(
                    Fixture.ProfileIdentity(
                        $"{definitionId}.cost",
                        "fake.source.rev.2",
                        'c'),
                    baseline.CostProfile.UnitCosts,
                    baseline.CostProfile.MaximumAmountPerResource),
                new ProgressionDurationProfile(
                    Fixture.ProfileIdentity(
                        $"{definitionId}.duration",
                        "fake.source.rev.2",
                        'd'),
                    baseline.DurationProfile.UnitSeconds,
                    baseline.DurationProfile.MaximumSeconds,
                    baseline.DurationProfile.AllowsZeroDuration),
                baseline.Prerequisites,
                baseline.EffectProfiles);
            ProgressionCompatibilityResult baselineCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { baseline },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionCompatibilityResult changedCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { changedSources },
                    Array.Empty<ResearchProgressionStateRecord>());

            ProgressionStartPlan baselinePlan = Fixture.PlanResearch(
                baselineCompatibility,
                Fixture.ResearchStart(
                    baselineCompatibility,
                    definitionId,
                    1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionStartPlan changedPlan = Fixture.PlanResearch(
                changedCompatibility,
                Fixture.ResearchStart(
                    changedCompatibility,
                    definitionId,
                    1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);

            Assert.That(
                changedCompatibility.StateRevision,
                Is.EqualTo(baselineCompatibility.StateRevision));
            Assert.That(
                changedPlan.SemanticHash,
                Is.EqualTo(baselinePlan.SemanticHash));
            Assert.That(
                changedPlan.PlanHash,
                Is.Not.EqualTo(baselinePlan.PlanHash));
        }

        [Test]
        public void ResearchCostVectorsReuseAcceptedBuildingCostContract()
        {
            ResearchProgressionDefinition definition = Fixture.ResearchWithCosts(
                "fake.research.multi-cost",
                new[]
                {
                    new BuildingConstructionCost(ResourceType.ManaStone, 3),
                    new BuildingConstructionCost(ResourceType.Gold, 200)
                });
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            var economy = new ProgressionEconomySnapshot(
                Fixture.EconomyRevision,
                new[]
                {
                    new ProgressionResourceBalance(ResourceType.ManaStone, 10),
                    new ProgressionResourceBalance(ResourceType.Gold, 1000)
                });

            ProgressionStartPlan plan = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1),
                economy,
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);

            Assert.That(plan.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                plan.Costs.Select(cost => cost.ResourceType),
                Is.EqualTo(new[] { ResourceType.Gold, ResourceType.ManaStone }));
            Assert.That(
                plan.Costs.Select(cost => cost.Amount),
                Is.EqualTo(new long[] { 200, 3 }));

            ResearchProgressionDefinition duplicateCost =
                Fixture.ResearchWithCosts(
                    "fake.research.duplicate-cost",
                    new[]
                    {
                        new BuildingConstructionCost(ResourceType.Gold, 100),
                        new BuildingConstructionCost(ResourceType.Gold, 200)
                    });
            ProgressionCompatibilityResult rejected =
                Fixture.ResearchCompatibility(
                    new[] { duplicateCost },
                    Array.Empty<ResearchProgressionStateRecord>());
            Assert.That(
                rejected.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.UnavailableCatalog));
            Assert.That(
                rejected.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InvalidCostProfile));
        }

        [Test]
        public void TrainingStartPreservesExplicitZeroDurationAndCheckedCounts()
        {
            TroopProgressionDefinition definition =
                Fixture.Troop("fake.troop.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.TrainingCompatibility(
                    new[] { definition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartRequest request = Fixture.TrainingStart(
                compatibility,
                definition.Identity.Id,
                5);

            ProgressionStartPlan plan = Fixture.PlanTraining(
                compatibility,
                request,
                Fixture.Economy(ResourceType.Food, 1000),
                2000);

            Assert.That(plan.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(plan.PreviousValue, Is.Zero);
            Assert.That(plan.TargetValue, Is.EqualTo(5));
            Assert.That(plan.BatchCount, Is.EqualTo(5));
            Assert.That(plan.Costs, Has.Count.EqualTo(1));
            Assert.That(plan.Costs[0].ResourceType, Is.EqualTo(ResourceType.Food));
            Assert.That(plan.Costs[0].Amount, Is.EqualTo(50));
            Assert.That(plan.StartTimestamp, Is.EqualTo(2000));
            Assert.That(plan.EndTimestamp, Is.EqualTo(2000));
        }

        [Test]
        public void TrainingRejectsNonpositiveOverCapAndArithmeticOverflow()
        {
            TroopProgressionDefinition definition =
                Fixture.Troop(
                    "fake.troop.alpha",
                    maximumInventory: 10,
                    maximumBatch: 10);
            ProgressionCompatibilityResult compatibility =
                Fixture.TrainingCompatibility(
                    new[] { definition },
                    new[]
                    {
                        new TroopProgressionStateRecord(
                            definition.Identity.Id,
                            definition.Identity.ContentVersion,
                            9,
                            0,
                            0)
                    });

            foreach (long batch in new long[] { 0, -1 })
            {
                ProgressionStartPlan invalid = Fixture.PlanTraining(
                    compatibility,
                    Fixture.TrainingStart(
                        compatibility,
                        definition.Identity.Id,
                        batch),
                    Fixture.Economy(ResourceType.Food, 1000),
                    100);
                Assert.That(
                    invalid.Status,
                    Is.EqualTo(ProgressionPlanStatus.InvalidTarget),
                    batch.ToString());
            }

            ProgressionStartPlan overCap = Fixture.PlanTraining(
                compatibility,
                Fixture.TrainingStart(
                    compatibility,
                    definition.Identity.Id,
                    2),
                Fixture.Economy(ResourceType.Food, 1000),
                100);
            Assert.That(
                overCap.Status,
                Is.EqualTo(ProgressionPlanStatus.InventoryOverflow));

            TroopProgressionDefinition costOverflowDefinition = Fixture.Troop(
                "fake.troop.cost-overflow",
                maximumInventory: 10,
                maximumBatch: 10,
                unitCost: long.MaxValue,
                maximumCost: long.MaxValue);
            ProgressionCompatibilityResult costOverflowCompatibility =
                Fixture.TrainingCompatibility(
                    new[] { costOverflowDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan costOverflow = Fixture.PlanTraining(
                costOverflowCompatibility,
                Fixture.TrainingStart(
                    costOverflowCompatibility,
                    costOverflowDefinition.Identity.Id,
                    2),
                Fixture.Economy(ResourceType.Food, long.MaxValue),
                100);
            Assert.That(
                costOverflow.Status,
                Is.EqualTo(ProgressionPlanStatus.ArithmeticOverflow));

            TroopProgressionDefinition durationOverflowDefinition = Fixture.Troop(
                "fake.troop.duration-overflow",
                maximumInventory: 10,
                maximumBatch: 10,
                unitCost: 1,
                maximumCost: 10,
                unitDuration: long.MaxValue,
                maximumDuration: long.MaxValue,
                allowsZeroDuration: false);
            ProgressionCompatibilityResult durationOverflowCompatibility =
                Fixture.TrainingCompatibility(
                    new[] { durationOverflowDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan durationOverflow = Fixture.PlanTraining(
                durationOverflowCompatibility,
                Fixture.TrainingStart(
                    durationOverflowCompatibility,
                    durationOverflowDefinition.Identity.Id,
                    2),
                Fixture.Economy(ResourceType.Food, 10),
                100);
            Assert.That(
                durationOverflow.Status,
                Is.EqualTo(ProgressionPlanStatus.ArithmeticOverflow));

            TroopProgressionDefinition deadlineDefinition = Fixture.Troop(
                "fake.troop.deadline-overflow",
                unitDuration: 10,
                maximumDuration: 100,
                allowsZeroDuration: false);
            ProgressionCompatibilityResult deadlineCompatibility =
                Fixture.TrainingCompatibility(
                    new[] { deadlineDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan deadlineOverflow = Fixture.PlanTraining(
                deadlineCompatibility,
                Fixture.TrainingStart(
                    deadlineCompatibility,
                    deadlineDefinition.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Food, 100),
                long.MaxValue - 5);
            Assert.That(
                deadlineOverflow.Status,
                Is.EqualTo(ProgressionPlanStatus.ArithmeticOverflow));
        }

        [Test]
        public void PrerequisiteAndEconomySnapshotsFailClosedBeforePlanning()
        {
            var prerequisite = new ProgressionPrerequisite(
                "fake.building.academy",
                2);
            ResearchProgressionDefinition definition = Fixture.Research(
                "fake.research.alpha",
                prerequisites: new[] { prerequisite });
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    new[]
                    {
                        new ProgressionPrerequisiteTargetDefinition(
                            Fixture.ProfileIdentity(prerequisite.DefinitionId),
                            5)
                    });
            ProgressionStartRequest request =
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1);
            ProgressionEconomySnapshot economy =
                Fixture.Economy(ResourceType.Gold, 1000);

            ProgressionStartPlan missing = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                missing.Status,
                Is.EqualTo(ProgressionPlanStatus.PrerequisiteUnmet));

            var malformed = new ProgressionPrerequisiteSnapshot(
                "prerequisite.rev.1",
                new[]
                {
                    new ProgressionLevelValue(prerequisite.DefinitionId, 2),
                    new ProgressionLevelValue(prerequisite.DefinitionId, 2)
                });
            ProgressionStartPlan duplicate = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                malformed,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                duplicate.Status,
                Is.EqualTo(ProgressionPlanStatus.StateMalformed));

            ProgressionStartPlan missingPrerequisiteSource = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                new ProgressionPrerequisiteSnapshot(
                    "prerequisite.rev.1",
                    null),
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                missingPrerequisiteSource.Status,
                Is.EqualTo(ProgressionPlanStatus.StateMalformed));

            var met = new ProgressionPrerequisiteSnapshot(
                "prerequisite.rev.1",
                new[]
                {
                    new ProgressionLevelValue(prerequisite.DefinitionId, 2)
                });
            ProgressionStartPlan ready = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                met,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(ready.Status, Is.EqualTo(ProgressionPlanStatus.Ready));

            ProgressionStartPlan staleEconomy = Fixture.PlanResearch(
                compatibility,
                request,
                Fixture.Economy(ResourceType.Gold, 1000, "economy.rev.2"),
                met,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                staleEconomy.Status,
                Is.EqualTo(ProgressionPlanStatus.StaleEconomyRevision));

            ProgressionStartPlan insufficient = Fixture.PlanResearch(
                compatibility,
                request,
                Fixture.Economy(ResourceType.Gold, 199),
                met,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                insufficient.Status,
                Is.EqualTo(ProgressionPlanStatus.InsufficientResources));

            var invalidEconomy = new ProgressionEconomySnapshot(
                Fixture.EconomyRevision,
                new[]
                {
                    new ProgressionResourceBalance(ResourceType.Gold, 1000),
                    new ProgressionResourceBalance(ResourceType.Gold, 1000)
                });
            ProgressionStartPlan invalid = Fixture.PlanResearch(
                compatibility,
                request,
                invalidEconomy,
                met,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                invalid.Status,
                Is.EqualTo(ProgressionPlanStatus.EconomyInvalid));

            ProgressionStartPlan missingEconomySource = Fixture.PlanResearch(
                compatibility,
                request,
                new ProgressionEconomySnapshot(Fixture.EconomyRevision, null),
                met,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                missingEconomySource.Status,
                Is.EqualTo(ProgressionPlanStatus.EconomyInvalid));
        }

        [Test]
        public void StartReplayIsExactConflictSafeAndCommitUncertain()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartRequest request =
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1);
            ProgressionEconomySnapshot economy =
                Fixture.Economy(ResourceType.Gold, 1000);
            ProgressionStartPlan ready = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            var committed = new ProgressionOperationReceipt(
                request.OperationId,
                ready.SemanticHash,
                ready.PlanHash,
                ProgressionOperationDurability.Committed);

            ProgressionStartPlan exact = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                new[] { committed },
                Fixture.NoOrders,
                100);
            Assert.That(
                exact.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(exact.Costs, Is.Empty);
            Assert.That(exact.TargetValue, Is.Zero);
            Assert.That(exact.PlanHash, Is.EqualTo(ready.PlanHash));

            ProgressionStartRequest changedPayload = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                2,
                request.OperationId);
            ProgressionStartPlan conflict = Fixture.PlanResearch(
                compatibility,
                changedPayload,
                economy,
                Fixture.NoPrerequisites,
                new[] { committed },
                Fixture.NoOrders,
                100);
            Assert.That(
                conflict.Status,
                Is.EqualTo(ProgressionPlanStatus.CorrelationConflict));

            var uncertain = new ProgressionOperationReceipt(
                request.OperationId,
                ready.SemanticHash,
                string.Empty,
                ProgressionOperationDurability.CommitUncertain);
            ProgressionStartPlan frozen = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                new[] { uncertain },
                Fixture.NoOrders,
                100);
            Assert.That(
                frozen.Status,
                Is.EqualTo(ProgressionPlanStatus.CommitUncertain));

            ProgressionStartPlan malformedLedger = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                new[] { committed, committed },
                Fixture.NoOrders,
                100);
            Assert.That(
                malformedLedger.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));

            var unrelatedMalformed = new ProgressionOperationReceipt(
                "unrelated-operation",
                ready.SemanticHash,
                string.Empty,
                ProgressionOperationDurability.Committed);
            ProgressionStartPlan poisonedLedger = Fixture.PlanResearch(
                compatibility,
                request,
                economy,
                Fixture.NoPrerequisites,
                new[] { unrelatedMalformed },
                Fixture.NoOrders,
                100);
            Assert.That(
                poisonedLedger.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));

            ProgressionStartRequest stale = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                "research-start-stale",
                "stale.progression.rev");
            ProgressionStartPlan stalePlan = Fixture.PlanResearch(
                compatibility,
                stale,
                economy,
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                stalePlan.Status,
                Is.EqualTo(ProgressionPlanStatus.StaleProgressionRevision));
        }

        [Test]
        public void ActiveOrRecoveryRequiredOrdersBlockNewStarts()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartRequest firstRequest =
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1);
            ProgressionStartPlan first = Fixture.PlanResearch(
                compatibility,
                firstRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot active = Fixture.Order(first);
            ProgressionStartRequest secondRequest = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                "research-start-2",
                compatibility.StateRevision,
                "research-order-2");

            ProgressionStartPlan conflict = Fixture.PlanResearch(
                compatibility,
                secondRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                new[] { active },
                100);
            Assert.That(
                conflict.Status,
                Is.EqualTo(ProgressionPlanStatus.OrderAlreadyActive));

            ProgressionStartRequest orderIdCollision = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                "research-start-order-collision",
                compatibility.StateRevision,
                active.OrderId);
            Assert.That(
                Fixture.PlanResearch(
                    compatibility,
                    orderIdCollision,
                    Fixture.Economy(ResourceType.Gold, 1000),
                    Fixture.NoPrerequisites,
                    Fixture.NoReceipts,
                    new[] { active },
                    100).Status,
                Is.EqualTo(ProgressionPlanStatus.CorrelationConflict));

            ProgressionOrderSnapshot cancellationReserved =
                ProgressionOrderPlanner.CreateActiveOrder(
                    first,
                    active.CompletionOperationId,
                    "research-cancel-reserved");
            ProgressionStartRequest cancellationCollision =
                Fixture.ResearchStart(
                    compatibility,
                    definition.Identity.Id,
                    1,
                    "research-cancel-reserved",
                    compatibility.StateRevision,
                    "research-order-cancellation-collision");
            Assert.That(
                Fixture.PlanResearch(
                    compatibility,
                    cancellationCollision,
                    Fixture.Economy(ResourceType.Gold, 1000),
                    Fixture.NoPrerequisites,
                    Fixture.NoReceipts,
                    new[] { cancellationReserved },
                    100).Status,
                Is.EqualTo(ProgressionPlanStatus.CorrelationConflict));

            ProgressionOrderSnapshot recovery = Fixture.WithState(
                active,
                ProgressionOrderState.RecoveryRequired);
            ProgressionStartPlan blocked = Fixture.PlanResearch(
                compatibility,
                secondRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                new[] { recovery },
                100);
            Assert.That(
                blocked.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));

            ProgressionStartPlan nullLedger = Fixture.PlanResearch(
                compatibility,
                secondRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                null,
                100);
            Assert.That(
                nullLedger.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
        }

        [Test]
        public void CompletionIsClockGatedExactAndReplaySafe()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);
            ProgressionCompletionRequest request =
                Fixture.Completion(compatibility, order);

            ProgressionCompletionPlan early =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    request,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    order.EndTimestamp - 1);
            ProgressionCompletionPlan rollback =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    request,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    order.StartTimestamp - 1);
            ProgressionCompletionPlan ready =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    request,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    order.EndTimestamp);

            Assert.That(
                early.Status,
                Is.EqualTo(ProgressionPlanStatus.NotYetEligible));
            Assert.That(
                rollback.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));
            Assert.That(early.QuestProgressAmount, Is.Zero);
            Assert.That(ready.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(ready.PreviousValue, Is.Zero);
            Assert.That(ready.TargetValue, Is.EqualTo(1));
            Assert.That(ready.QuestProgressAmount, Is.EqualTo(1));
            Assert.That(ready.PlanHash, Has.Length.EqualTo(64));
            Assert.That(compatibility.Research[0].Level, Is.Zero);

            var receipt = new ProgressionOperationReceipt(
                request.OperationId,
                ready.SemanticHash,
                ready.PlanHash,
                ProgressionOperationDurability.Committed);
            ProgressionCompletionPlan replay =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    request,
                    new[] { receipt },
                    Fixture.CompletionDependencies,
                    order.EndTimestamp);
            Assert.That(
                replay.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(replay.QuestProgressAmount, Is.Zero);
            Assert.That(replay.PreviousValue, Is.EqualTo(replay.TargetValue));

            var uncertain = new ProgressionOperationReceipt(
                request.OperationId,
                ready.SemanticHash,
                string.Empty,
                ProgressionOperationDurability.CommitUncertain);
            ProgressionCompletionPlan frozen =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    request,
                    new[] { uncertain },
                    Fixture.CompletionDependencies,
                    order.EndTimestamp);
            Assert.That(
                frozen.Status,
                Is.EqualTo(ProgressionPlanStatus.CommitUncertain));

            var changedRequest = new ProgressionCompletionRequest(
                "profile-changed",
                request.OrderId,
                request.OperationId,
                request.ExpectedCatalogSetId,
                request.ExpectedProgressionRevision,
                request.ExpectedEconomyRevision,
                request.ExpectedQuestRevision,
                request.CompletionPolicyVersion);
            ProgressionCompletionPlan conflict =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    changedRequest,
                    new[] { receipt },
                    Fixture.CompletionDependencies,
                    order.EndTimestamp);
            Assert.That(
                conflict.Status,
                Is.EqualTo(ProgressionPlanStatus.CorrelationConflict));
        }

        [Test]
        public void TrainingCompletionUsesExactBatchAndRejectsStateDrift()
        {
            TroopProgressionDefinition definition =
                Fixture.Troop("fake.troop.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.TrainingCompatibility(
                    new[] { definition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanTraining(
                compatibility,
                Fixture.TrainingStart(compatibility, definition.Identity.Id, 5),
                Fixture.Economy(ResourceType.Food, 1000),
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);
            ProgressionCompletionPlan ready =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    Fixture.Completion(compatibility, order),
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    100);

            Assert.That(ready.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(ready.TargetValue, Is.EqualTo(5));
            Assert.That(ready.QuestProgressAmount, Is.EqualTo(5));

            ProgressionCompatibilityResult drifted =
                Fixture.TrainingCompatibility(
                    new[] { definition },
                    new[]
                    {
                        new TroopProgressionStateRecord(
                            definition.Identity.Id,
                            definition.Identity.ContentVersion,
                            1,
                            0,
                            0)
                    });
            ProgressionCompletionPlan rejected =
                ProgressionOrderPlanner.PlanCompletion(
                    drifted,
                    order,
                    Fixture.Completion(drifted, order),
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    100);
            Assert.That(
                rejected.Status,
                Is.EqualTo(ProgressionPlanStatus.StateMalformed));
        }

        [Test]
        public void ForgedAvailableSnapshotsFailClosedWithoutLookupExceptions()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult valid =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            var forgedNull = new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                ProgressionCompatibilityStatus.Available,
                valid.CatalogSetId,
                valid.CatalogRevision,
                valid.StateRevision,
                new ResearchProgressionSnapshot[] { null },
                Array.Empty<TroopProgressionSnapshot>(),
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>());
            var forgedDuplicate = new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                ProgressionCompatibilityStatus.Available,
                valid.CatalogSetId,
                valid.CatalogRevision,
                valid.StateRevision,
                new[] { valid.Research[0], valid.Research[0] },
                Array.Empty<TroopProgressionSnapshot>(),
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>());

            foreach (ProgressionCompatibilityResult forged in
                     new[] { forgedNull, forgedDuplicate })
            {
                ProgressionStartPlan result = null;
                Assert.That(
                    () => result = Fixture.PlanResearch(
                        forged,
                        Fixture.ResearchStart(
                            forged,
                            definition.Identity.Id,
                            1),
                        Fixture.Economy(ResourceType.Gold, 1000),
                        Fixture.NoPrerequisites,
                        Fixture.NoReceipts,
                        Fixture.NoOrders,
                        100),
                    Throws.Nothing);
                Assert.That(
                    result.Status,
                    Is.EqualTo(ProgressionPlanStatus.StateMalformed));
            }
        }

        [Test]
        public void CompletionAcceptsCurrentPostStartRevisionsWithoutRewritingOrderAudit()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult initial =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanResearch(
                initial,
                Fixture.ResearchStart(initial, definition.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);
            ProgressionCompatibilityResult current =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            definition.Identity.Id,
                            definition.Identity.ContentVersion,
                            0,
                            false,
                            0)
                    });
            const string currentEconomyRevision = "economy.rev.2";
            const string currentQuestRevision = "quest.rev.2";
            var request = new ProgressionCompletionRequest(
                order.ProfileId,
                order.OrderId,
                order.CompletionOperationId,
                current.CatalogSetId,
                current.StateRevision,
                currentEconomyRevision,
                currentQuestRevision,
                Fixture.CompletionPolicyVersion);

            ProgressionCompletionPlan result =
                ProgressionOrderPlanner.PlanCompletion(
                    current,
                    order,
                    request,
                    Fixture.NoReceipts,
                    new ProgressionCompletionDependencySnapshot(
                        currentEconomyRevision,
                        currentQuestRevision),
                    order.EndTimestamp);

            Assert.That(initial.StateRevision, Is.Not.EqualTo(current.StateRevision));
            Assert.That(
                order.ProgressionRevision,
                Is.EqualTo(initial.StateRevision));
            Assert.That(
                order.EconomyRevision,
                Is.EqualTo(Fixture.EconomyRevision));
            Assert.That(result.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                result.ProgressionRevision,
                Is.EqualTo(current.StateRevision));
            Assert.That(
                result.EconomyRevision,
                Is.EqualTo(currentEconomyRevision));
        }

        [Test]
        public void CompletedOrderNeedsDurableReceiptAndExactReceiptReplays()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot active = Fixture.Order(start);
            ProgressionCompletionRequest request =
                Fixture.Completion(compatibility, active);
            ProgressionCompletionPlan ready =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    active,
                    request,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    active.EndTimestamp);
            ProgressionOrderSnapshot completed = Fixture.WithState(
                active,
                ProgressionOrderState.Completed);

            ProgressionCompletionPlan missingReceipt =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    completed,
                    request,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    completed.EndTimestamp);
            var receipt = new ProgressionOperationReceipt(
                request.OperationId,
                ready.SemanticHash,
                ready.PlanHash,
                ProgressionOperationDurability.Committed);
            ProgressionCompletionPlan replay =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    completed,
                    request,
                    new[] { receipt },
                    Fixture.CompletionDependencies,
                    completed.EndTimestamp);

            Assert.That(
                missingReceipt.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
            Assert.That(
                replay.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(replay.PlanHash, Is.EqualTo(ready.PlanHash));
        }

        [Test]
        public void TamperedCommittedCostsInvalidateOrderBeforeCompletion()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);
            var tampered = new ProgressionOrderSnapshot(
                order.OrderType,
                order.State,
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
                new[]
                {
                    new BuildingConstructionCost(
                        order.CommittedCosts[0].ResourceType,
                        order.CommittedCosts[0].Amount + 1)
                },
                order.StartTimestamp,
                order.EndTimestamp,
                order.CatalogSetId,
                order.ProgressionRevision,
                order.EconomyRevision,
                order.RequestPolicyVersion,
                order.OrderHash);

            ProgressionCompletionPlan result =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    tampered,
                    Fixture.Completion(compatibility, tampered),
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    tampered.EndTimestamp);
            ProgressionCompletionPlan ready =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    Fixture.Completion(compatibility, order),
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    order.EndTimestamp);
            var receipt = new ProgressionOperationReceipt(
                order.CompletionOperationId,
                ready.SemanticHash,
                ready.PlanHash,
                ProgressionOperationDurability.Committed);
            ProgressionCompletionPlan replayAgainstTamperedOrder =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    tampered,
                    Fixture.Completion(compatibility, tampered),
                    new[] { receipt },
                    Fixture.CompletionDependencies,
                    tampered.EndTimestamp);

            Assert.That(
                result.Status,
                Is.EqualTo(ProgressionPlanStatus.OrderMalformed));
            Assert.That(
                replayAgainstTamperedOrder.Status,
                Is.EqualTo(ProgressionPlanStatus.OrderMalformed));
        }

        [Test]
        public void StartHashesAreDelimiterSafeAndCultureInvariant()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.alpha");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartRequest first = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                "operation|segment",
                compatibility.StateRevision,
                "order");
            var second = new ProgressionStartRequest(
                "profile-1",
                ProgressionOrderType.ResearchLevel,
                definition.Identity.Id,
                "order|operation",
                "segment",
                1,
                0,
                Fixture.CatalogSetId,
                compatibility.StateRevision,
                Fixture.EconomyRevision,
                Fixture.StartPolicyVersion);
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            ProgressionStartPlan baseline;
            ProgressionStartPlan changedCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                baseline = Fixture.PlanResearch(
                    compatibility,
                    first,
                    Fixture.Economy(ResourceType.Gold, 1000),
                    Fixture.NoPrerequisites,
                    Fixture.NoReceipts,
                    Fixture.NoOrders,
                    100);
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
                changedCulture = Fixture.PlanResearch(
                    compatibility,
                    first,
                    Fixture.Economy(ResourceType.Gold, 1000),
                    Fixture.NoPrerequisites,
                    Fixture.NoReceipts,
                    Fixture.NoOrders,
                    100);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }

            ProgressionStartPlan distinct = Fixture.PlanResearch(
                compatibility,
                second,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(baseline.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                changedCulture.SemanticHash,
                Is.EqualTo(baseline.SemanticHash));
            Assert.That(
                changedCulture.PlanHash,
                Is.EqualTo(baseline.PlanHash));
            Assert.That(
                distinct.SemanticHash,
                Is.Not.EqualTo(baseline.SemanticHash));
        }

        [Test]
        public void ReconciliationIsDeterministicAndMalformedOrdersFailClosed()
        {
            ProgressionOrderSnapshot late =
                Fixture.Order("order-late", ProgressionOrderType.ResearchLevel, 30);
            ProgressionOrderSnapshot training =
                Fixture.Order("order-training", ProgressionOrderType.TroopTrainingBatch, 20);
            ProgressionOrderSnapshot research =
                Fixture.Order("order-research", ProgressionOrderType.ResearchLevel, 20);
            var source = new[] { late, training, research };

            ProgressionReconciliationPlan first =
                ProgressionOrderPlanner.PlanReconciliation(source, 30);
            ProgressionReconciliationPlan second =
                ProgressionOrderPlanner.PlanReconciliation(source, 30);

            Assert.That(first.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                first.EligibleOrders.Select(order => order.OrderId),
                Is.EqualTo(new[]
                {
                    "order-research",
                    "order-training",
                    "order-late"
                }));
            Assert.That(second.PlanHash, Is.EqualTo(first.PlanHash));
            Assert.That(source, Is.EqualTo(new[] { late, training, research }));
            Assert.That(
                () => ((IList<ProgressionOrderSnapshot>)first.EligibleOrders).Add(late),
                Throws.TypeOf<NotSupportedException>());

            ProgressionReconciliationPlan duplicate =
                ProgressionOrderPlanner.PlanReconciliation(
                    new[] { research, research },
                    30);
            Assert.That(
                duplicate.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
            Assert.That(duplicate.EligibleOrders, Is.Empty);

            ProgressionReconciliationPlan nullOrder =
                ProgressionOrderPlanner.PlanReconciliation(
                    new ProgressionOrderSnapshot[] { research, null },
                    30);
            Assert.That(
                nullOrder.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
            Assert.That(nullOrder.EligibleOrders, Is.Empty);

            ProgressionReconciliationPlan rollback =
                ProgressionOrderPlanner.PlanReconciliation(source, 9);
            Assert.That(
                rollback.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));
            Assert.That(rollback.EligibleOrders, Is.Empty);
        }

        [Test]
        public void ResearchEffectReferencesAreImmutableOrderedAndSourceBound()
        {
            ProgressionSourceIdentity effectB =
                Fixture.ProfileIdentity("fake.effect.beta");
            ProgressionSourceIdentity effectA =
                Fixture.ProfileIdentity("fake.effect.alpha");
            ResearchProgressionDefinition beta = Fixture.Research(
                "fake.research.beta",
                effects: new[] { effectA });
            ResearchProgressionDefinition alpha = Fixture.Research(
                "fake.research.alpha",
                effects: new[] { effectB, effectA });
            var states = new[]
            {
                new ResearchProgressionStateRecord(
                    beta.Identity.Id,
                    beta.Identity.ContentVersion,
                    1,
                    false,
                    0),
                new ResearchProgressionStateRecord(
                    alpha.Identity.Id,
                    alpha.Identity.ContentVersion,
                    2,
                    false,
                    0)
            };
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(new[] { beta, alpha }, states);

            ResearchEffectSnapshot first =
                ProgressionOrderPlanner.BuildResearchEffectSnapshot(compatibility);
            ResearchEffectSnapshot second =
                ProgressionOrderPlanner.BuildResearchEffectSnapshot(compatibility);

            Assert.That(first.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                first.Effects.Select(effect =>
                    $"{effect.ResearchDefinitionId}:{effect.EffectProfile.Id}"),
                Is.EqualTo(new[]
                {
                    "fake.research.alpha:fake.effect.alpha",
                    "fake.research.alpha:fake.effect.beta",
                    "fake.research.beta:fake.effect.alpha"
                }));
            Assert.That(first.SnapshotHash, Has.Length.EqualTo(64));
            Assert.That(second.SnapshotHash, Is.EqualTo(first.SnapshotHash));
            Assert.That(states[0].Level, Is.EqualTo(1));
            Assert.That(states[1].Level, Is.EqualTo(2));
            Assert.That(
                () => ((IList<ResearchEffectReference>)first.Effects).Add(
                    first.Effects[0]),
                Throws.TypeOf<NotSupportedException>());
        }
    }

    internal static class Fixture
    {
        internal const string CatalogSetId = "fake.progression.catalog";
        internal const string CatalogRevision = "fake.catalog.rev.1";
        internal const string ContentVersion = "fake.content.v1";
        internal const string EconomyRevision = "economy.rev.1";
        internal const string QuestRevision = "quest.rev.1";
        internal const string StartPolicyVersion = "start.policy.v1";
        internal const string CompletionPolicyVersion = "completion.policy.v1";
        internal static readonly ProgressionTimestampPolicy TimestampPolicy =
            new ProgressionTimestampPolicy(
                "timestamp.policy.v1",
                1,
                4102444800);
        internal static readonly ProgressionCompletionDependencySnapshot
            CompletionDependencies =
                new ProgressionCompletionDependencySnapshot(
                    EconomyRevision,
                    QuestRevision);
        internal static readonly ProgressionPrerequisiteSnapshot NoPrerequisites =
            new ProgressionPrerequisiteSnapshot(
                "prerequisite.rev.1",
                Array.Empty<ProgressionLevelValue>());
        internal static readonly ProgressionOperationReceipt[] NoReceipts =
            Array.Empty<ProgressionOperationReceipt>();
        internal static readonly ProgressionOrderSnapshot[] NoOrders =
            Array.Empty<ProgressionOrderSnapshot>();

        internal static ResearchProgressionDefinition Research(
            string id,
            int initialLevel = 0,
            int maximumLevel = 5,
            long unitCost = 200,
            long maximumCost = 10000,
            long unitDuration = 15,
            long maximumDuration = 1000,
            IEnumerable<ProgressionPrerequisite> prerequisites = null,
            IEnumerable<ProgressionSourceIdentity> effects = null)
        {
            return new ResearchProgressionDefinition(
                Identity(
                    id,
                    ProgressionCompatibilityPlanner.ResearchSchemaVersion),
                initialLevel,
                maximumLevel,
                new ProgressionCostProfile(
                    ProfileIdentity($"{id}.cost"),
                    new[]
                    {
                        new BuildingConstructionCost(ResourceType.Gold, unitCost)
                    },
                    maximumCost),
                new ProgressionDurationProfile(
                    ProfileIdentity($"{id}.duration"),
                    unitDuration,
                    maximumDuration,
                    false),
                prerequisites ?? Array.Empty<ProgressionPrerequisite>(),
                effects ?? Array.Empty<ProgressionSourceIdentity>());
        }

        internal static TroopProgressionDefinition Troop(
            string id,
            long maximumInventory = 1000,
            long maximumBatch = 100,
            long unitCost = 10,
            long maximumCost = 10000,
            long unitDuration = 0,
            long maximumDuration = 0,
            bool allowsZeroDuration = true,
            IEnumerable<ProgressionPrerequisite> prerequisites = null,
            ProgressionSourceIdentity battleProfile = null,
            ProgressionSourceIdentity inventoryPolicy = null,
            bool useDefaultInventoryPolicy = true,
            TroopInventoryCapacityPolicy inventoryCapacityPolicy =
                TroopInventoryCapacityPolicy.SeparatedCountsTotalCapacityV1)
        {
            ProgressionSourceIdentity resolvedPolicy = useDefaultInventoryPolicy
                ? inventoryPolicy ?? ProfileIdentity($"{id}.inventory-policy")
                : inventoryPolicy;
            return new TroopProgressionDefinition(
                Identity(id, ProgressionCompatibilityPlanner.TroopSchemaVersion),
                maximumInventory,
                maximumBatch,
                new ProgressionCostProfile(
                    ProfileIdentity($"{id}.cost"),
                    new[]
                    {
                        new BuildingConstructionCost(ResourceType.Food, unitCost)
                    },
                    maximumCost),
                new ProgressionDurationProfile(
                    ProfileIdentity($"{id}.duration"),
                    unitDuration,
                    maximumDuration,
                    allowsZeroDuration),
                prerequisites ?? Array.Empty<ProgressionPrerequisite>(),
                battleProfile ?? ProfileIdentity($"{id}.battle"),
                resolvedPolicy,
                inventoryCapacityPolicy);
        }

        internal static ResearchProgressionDefinition ResearchWithCosts(
            string id,
            IEnumerable<BuildingConstructionCost> unitCosts)
        {
            return new ResearchProgressionDefinition(
                Identity(
                    id,
                    ProgressionCompatibilityPlanner.ResearchSchemaVersion),
                0,
                5,
                new ProgressionCostProfile(
                    ProfileIdentity($"{id}.cost"),
                    unitCosts,
                    10000),
                new ProgressionDurationProfile(
                    ProfileIdentity($"{id}.duration"),
                    15,
                    1000,
                    false),
                Array.Empty<ProgressionPrerequisite>(),
                Array.Empty<ProgressionSourceIdentity>());
        }

        internal static ProgressionSourceIdentity ProfileIdentity(
            string id,
            string sourceRevision = "fake.source.rev.1",
            char hashCharacter = 'a')
        {
            return Identity(
                id,
                ProgressionCompatibilityPlanner.ProfileSchemaVersion,
                sourceRevision,
                hashCharacter);
        }

        internal static ProgressionCompatibilityResult ResearchCompatibility(
            IEnumerable<ResearchProgressionDefinition> definitions,
            IEnumerable<ResearchProgressionStateRecord> states,
            IEnumerable<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets = null,
            ProgressionTimestampPolicy timestampPolicy = null)
        {
            return ProgressionCompatibilityPlanner.BuildResearchCompatibility(
                CatalogSetId,
                CatalogRevision,
                definitions,
                states,
                prerequisiteTargets,
                timestampPolicy ?? TimestampPolicy);
        }

        internal static ProgressionCompatibilityResult TrainingCompatibility(
            IEnumerable<TroopProgressionDefinition> definitions,
            IEnumerable<TroopProgressionStateRecord> states,
            IEnumerable<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets = null)
        {
            return ProgressionCompatibilityPlanner.BuildTrainingCompatibility(
                CatalogSetId,
                CatalogRevision,
                definitions,
                states,
                prerequisiteTargets);
        }

        internal static ProgressionStartRequest ResearchStart(
            ProgressionCompatibilityResult compatibility,
            string definitionId,
            int targetLevel,
            string operationId = "research-start-1",
            string expectedRevision = null,
            string orderId = "research-order-1")
        {
            return new ProgressionStartRequest(
                "profile-1",
                ProgressionOrderType.ResearchLevel,
                definitionId,
                orderId,
                operationId,
                targetLevel,
                0,
                CatalogSetId,
                expectedRevision ?? compatibility.StateRevision,
                EconomyRevision,
                StartPolicyVersion);
        }

        internal static ProgressionStartRequest TrainingStart(
            ProgressionCompatibilityResult compatibility,
            string definitionId,
            long batchCount,
            string operationId = "training-start-1",
            string expectedRevision = null,
            string orderId = "training-order-1")
        {
            return new ProgressionStartRequest(
                "profile-1",
                ProgressionOrderType.TroopTrainingBatch,
                definitionId,
                orderId,
                operationId,
                0,
                batchCount,
                CatalogSetId,
                expectedRevision ?? compatibility.StateRevision,
                EconomyRevision,
                StartPolicyVersion);
        }

        internal static ProgressionEconomySnapshot Economy(
            ResourceType resourceType,
            long amount,
            string revision = EconomyRevision)
        {
            return new ProgressionEconomySnapshot(
                revision,
                new[] { new ProgressionResourceBalance(resourceType, amount) });
        }

        internal static ProgressionStartPlan PlanResearch(
            ProgressionCompatibilityResult compatibility,
            ProgressionStartRequest request,
            ProgressionEconomySnapshot economy,
            ProgressionPrerequisiteSnapshot prerequisites,
            IEnumerable<ProgressionOperationReceipt> receipts,
            IEnumerable<ProgressionOrderSnapshot> orders,
            long timestamp)
        {
            return ProgressionOrderPlanner.PlanResearchStart(
                compatibility,
                request,
                economy,
                prerequisites,
                receipts,
                orders,
                timestamp);
        }

        internal static ProgressionStartPlan PlanTraining(
            ProgressionCompatibilityResult compatibility,
            ProgressionStartRequest request,
            ProgressionEconomySnapshot economy,
            long timestamp)
        {
            return ProgressionOrderPlanner.PlanTrainingStart(
                compatibility,
                request,
                economy,
                NoPrerequisites,
                NoReceipts,
                NoOrders,
                timestamp);
        }

        internal static ProgressionOrderSnapshot Order(
            ProgressionStartPlan plan)
        {
            return ProgressionOrderPlanner.CreateActiveOrder(
                plan,
                $"{plan.OrderId}.complete");
        }

        internal static ProgressionOrderSnapshot Order(
            string orderId,
            ProgressionOrderType orderType,
            long endTimestamp)
        {
            long duration = endTimestamp - 10;
            if (orderType == ProgressionOrderType.ResearchLevel)
            {
                ResearchProgressionDefinition definition = Research(
                    $"fake.research.{orderId}",
                    unitCost: 1,
                    maximumCost: 100,
                    unitDuration: duration,
                    maximumDuration: duration);
                ProgressionCompatibilityResult compatibility =
                    ResearchCompatibility(
                        new[] { definition },
                        Array.Empty<ResearchProgressionStateRecord>());
                ProgressionStartRequest request = ResearchStart(
                    compatibility,
                    definition.Identity.Id,
                    1,
                    $"{orderId}.start",
                    compatibility.StateRevision,
                    orderId);
                return Order(PlanResearch(
                    compatibility,
                    request,
                    Economy(ResourceType.Gold, 100),
                    NoPrerequisites,
                    NoReceipts,
                    NoOrders,
                    10));
            }

            TroopProgressionDefinition troop = Troop(
                $"fake.troop.{orderId}",
                unitCost: 1,
                maximumCost: 100,
                unitDuration: duration,
                maximumDuration: duration,
                allowsZeroDuration: duration == 0);
            ProgressionCompatibilityResult training =
                TrainingCompatibility(
                    new[] { troop },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartRequest trainingRequest = TrainingStart(
                training,
                troop.Identity.Id,
                1,
                $"{orderId}.start",
                training.StateRevision,
                orderId);
            return Order(PlanTraining(
                training,
                trainingRequest,
                Economy(ResourceType.Food, 100),
                10));
        }

        internal static ProgressionOrderSnapshot WithState(
            ProgressionOrderSnapshot order,
            ProgressionOrderState state)
        {
            return new ProgressionOrderSnapshot(
                order.OrderType,
                state,
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
                order.RequestPolicyVersion,
                order.OrderHash);
        }

        internal static ProgressionCompletionRequest Completion(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order)
        {
            return new ProgressionCompletionRequest(
                order.ProfileId,
                order.OrderId,
                order.CompletionOperationId,
                compatibility.CatalogSetId,
                compatibility.StateRevision,
                order.EconomyRevision,
                QuestRevision,
                CompletionPolicyVersion);
        }

        internal static string[] Diagnostics(ProgressionCompatibilityResult result)
        {
            return result.Diagnostics
                .Select(diagnostic =>
                    $"{diagnostic.Domain}|{diagnostic.DefinitionId}|" +
                    $"{diagnostic.Code}|{diagnostic.SourceIndex}")
                .ToArray();
        }

        internal static ProgressionSourceIdentity Identity(
            string id,
            string schemaVersion,
            string sourceRevision = "fake.source.rev.1",
            char hashCharacter = 'a')
        {
            return new ProgressionSourceIdentity(
                id,
                schemaVersion,
                ContentVersion,
                sourceRevision,
                new string(hashCharacter, 64));
        }
    }
}
