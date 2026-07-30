using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
                    null,
                    null,
                    Fixture.TimestampPolicy);
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

        [Test]
        public void HostileStateIdentitiesFailClosedWithoutUnboundedHashAllocation()
        {
            ResearchProgressionDefinition research =
                Fixture.Research("fake.research.alpha");
            TroopProgressionDefinition troop =
                Fixture.Troop("fake.troop.alpha");
            string maximumPlusOne = new string(
                'x',
                ProgressionCompatibilityPlanner.MaximumIdUtf8Bytes + 1);
            string hostile = new string('x', 1024 * 1024);

            // Warm the hashing and crypto paths before measuring the hostile input.
            Fixture.ResearchCompatibility(
                new[] { research },
                Array.Empty<ResearchProgressionStateRecord>());
            Fixture.TrainingCompatibility(
                new[] { troop },
                Array.Empty<TroopProgressionStateRecord>());

            ProgressionCompatibilityResult maximumPlusOneResult =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            maximumPlusOne,
                            Fixture.ContentVersion,
                            0,
                            false,
                            0)
                    });

            long researchBefore = GC.GetAllocatedBytesForCurrentThread();
            ProgressionCompatibilityResult hostileResearch =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            hostile,
                            hostile,
                            0,
                            false,
                            0)
                    });
            long researchAllocated =
                GC.GetAllocatedBytesForCurrentThread() - researchBefore;

            long trainingBefore = GC.GetAllocatedBytesForCurrentThread();
            ProgressionCompatibilityResult hostileTraining =
                Fixture.TrainingCompatibility(
                    new[] { troop },
                    new[]
                    {
                        new TroopProgressionStateRecord(
                            hostile,
                            hostile,
                            0,
                            0,
                            0)
                    });
            long trainingAllocated =
                GC.GetAllocatedBytesForCurrentThread() - trainingBefore;

            foreach (ProgressionCompatibilityResult result in new[]
                     {
                         maximumPlusOneResult,
                         hostileResearch,
                         hostileTraining
                     })
            {
                Assert.That(
                    result.Status,
                    Is.EqualTo(ProgressionCompatibilityStatus.MalformedState));
                Assert.That(result.StateRevision, Has.Length.EqualTo(64));
                Assert.That(
                    result.Diagnostics.Select(diagnostic => diagnostic.Code),
                    Does.Contain(ProgressionDiagnosticCode.InvalidStateId));
                Assert.That(
                    result.Diagnostics.All(diagnostic =>
                        diagnostic.DefinitionId.Length <=
                        ProgressionCompatibilityPlanner.MaximumIdUtf8Bytes),
                    Is.True);
            }

            Assert.That(
                hostileResearch.PreservedResearchStates[0].DefinitionId,
                Is.SameAs(hostile));
            Assert.That(
                hostileResearch.PreservedResearchStates[0]
                    .DefinitionContentVersion,
                Is.SameAs(hostile));
            Assert.That(
                hostileTraining.PreservedTroopStates[0].DefinitionId,
                Is.SameAs(hostile));
            Assert.That(researchAllocated, Is.LessThan(512L * 1024L));
            Assert.That(trainingAllocated, Is.LessThan(512L * 1024L));
        }

        [Test]
        public void MalformedUtf16IsRejectedWithoutReplacementCanonicalization()
        {
            ResearchProgressionDefinition research =
                Fixture.Research("fake.research.alpha");
            const string unpairedHigh = "fake.\uD800";
            const string unpairedLow = "fake.\uDC00";

            ProgressionCompatibilityResult high =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            unpairedHigh,
                            Fixture.ContentVersion,
                            0,
                            false,
                            0)
                    });
            ProgressionCompatibilityResult low =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            unpairedLow,
                            Fixture.ContentVersion,
                            0,
                            false,
                            0)
                    });

            Assert.That(
                high.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InvalidStateId));
            Assert.That(
                low.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain(ProgressionDiagnosticCode.InvalidStateId));
            Assert.That(high.Diagnostics.All(d => d.DefinitionId.Length == 0), Is.True);
            Assert.That(low.Diagnostics.All(d => d.DefinitionId.Length == 0), Is.True);
            Assert.That(high.StateRevision, Has.Length.EqualTo(64));
            Assert.That(low.StateRevision, Has.Length.EqualTo(64));
            Assert.That(
                high.StateRevision,
                Is.Not.EqualTo(low.StateRevision),
                "Unpaired high and low surrogates must not collapse through UTF-8 replacement.");

            ResearchProgressionDefinition validSupplementary =
                Fixture.Research("fake.research.\U0001F600");
            ProgressionCompatibilityResult valid =
                Fixture.ResearchCompatibility(
                    new[] { validSupplementary },
                    Array.Empty<ResearchProgressionStateRecord>());
            Assert.That(
                valid.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.Available));
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
            var deadlineTimestampPolicy = new ProgressionTimestampPolicy(
                "timestamp.policy.deadline-overflow",
                long.MaxValue - 1000,
                long.MaxValue,
                1000,
                1000);
            ProgressionCompatibilityResult deadlineCompatibility =
                Fixture.TrainingCompatibility(
                    new[] { deadlineDefinition },
                    Array.Empty<TroopProgressionStateRecord>(),
                    timestampPolicy: deadlineTimestampPolicy);
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
        public void PrerequisiteRevisionIsExpectedCarriedHashBoundAndStaleSafe()
        {
            var prerequisite = new ProgressionPrerequisite(
                "fake.building.academy",
                2);
            ResearchProgressionDefinition definition = Fixture.Research(
                "fake.research.prerequisite-revision",
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
            var revisionOne = new ProgressionPrerequisiteSnapshot(
                "prerequisite.rev.1",
                new[]
                {
                    new ProgressionLevelValue(prerequisite.DefinitionId, 2)
                });
            var revisionTwo = new ProgressionPrerequisiteSnapshot(
                "prerequisite.rev.2",
                new[]
                {
                    new ProgressionLevelValue(prerequisite.DefinitionId, 2)
                });
            ProgressionStartRequest requestOne = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                expectedPrerequisiteRevision: revisionOne.Revision);

            ProgressionStartPlan first = Fixture.PlanResearch(
                compatibility,
                requestOne,
                Fixture.Economy(ResourceType.Gold, 1000),
                revisionOne,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionStartPlan stale = Fixture.PlanResearch(
                compatibility,
                requestOne,
                Fixture.Economy(ResourceType.Gold, 1000),
                revisionTwo,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionStartRequest requestTwo = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                operationId: "research-start-prerequisite-2",
                orderId: "research-order-prerequisite-2",
                expectedPrerequisiteRevision: revisionTwo.Revision);
            ProgressionStartPlan second = Fixture.PlanResearch(
                compatibility,
                requestTwo,
                Fixture.Economy(ResourceType.Gold, 1000),
                revisionTwo,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);

            Assert.That(first.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                first.PrerequisiteRevision,
                Is.EqualTo(revisionOne.Revision));
            Assert.That(
                Fixture.Order(first).PrerequisiteRevision,
                Is.EqualTo(revisionOne.Revision));
            Assert.That(
                stale.Status,
                Is.EqualTo(ProgressionPlanStatus.StalePrerequisiteRevision));
            Assert.That(stale.PlanHash, Is.Empty);
            Assert.That(second.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(second.PrerequisiteRevision, Is.EqualTo(revisionTwo.Revision));
            Assert.That(second.SemanticHash, Is.Not.EqualTo(first.SemanticHash));
            Assert.That(second.PlanHash, Is.Not.EqualTo(first.PlanHash));

            ProgressionOrderSnapshot firstOrder = Fixture.Order(first);
            ProgressionOperationReceipt firstReceipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(first, firstOrder);
            ProgressionStartPlan exactReplay = Fixture.PlanResearch(
                null,
                requestOne,
                null,
                revisionTwo,
                new[] { firstReceipt },
                null,
                0);
            var changedRevisionRequest = new ProgressionStartRequest(
                requestOne.ProfileId,
                requestOne.OrderType,
                requestOne.DefinitionId,
                requestOne.OrderId,
                requestOne.OperationId,
                requestOne.RequestedTargetLevel,
                requestOne.RequestedBatchCount,
                requestOne.ExpectedCatalogSetId,
                requestOne.ExpectedProgressionRevision,
                requestOne.ExpectedEconomyRevision,
                requestOne.RequestPolicyVersion,
                revisionTwo.Revision);
            ProgressionStartPlan changedReplay = Fixture.PlanResearch(
                null,
                changedRevisionRequest,
                null,
                revisionTwo,
                new[] { firstReceipt },
                null,
                0);
            Assert.That(
                exactReplay.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(
                exactReplay.PrerequisiteRevision,
                Is.EqualTo(revisionOne.Revision));
            Assert.That(
                changedReplay.Status,
                Is.EqualTo(ProgressionPlanStatus.CorrelationConflict));

            ResearchProgressionDefinition noRequirements =
                Fixture.Research("fake.research.empty-prerequisite-authority");
            ProgressionCompatibilityResult emptyCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { noRequirements },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan empty = Fixture.PlanResearch(
                emptyCompatibility,
                Fixture.ResearchStart(
                    emptyCompatibility,
                    noRequirements.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(empty.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                empty.PrerequisiteRevision,
                Is.EqualTo(Fixture.NoPrerequisites.Revision));
        }

        [Test]
        public void TimestampPolicyBoundariesCoverStartsCompletionAndReconciliation()
        {
            var policy = new ProgressionTimestampPolicy(
                "timestamp.policy.boundary",
                100,
                300,
                50,
                20);
            ResearchProgressionDefinition research = Fixture.Research(
                "fake.research.timestamp-boundary",
                unitDuration: 20,
                maximumDuration: 20);
            ProgressionCompatibilityResult researchCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    timestampPolicy: policy);
            ProgressionStartRequest researchRequest = Fixture.ResearchStart(
                researchCompatibility,
                research.Identity.Id,
                1);

            ProgressionStartPlan atMinimum = Fixture.PlanResearch(
                researchCompatibility,
                researchRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionStartPlan belowMinimum = Fixture.PlanResearch(
                researchCompatibility,
                researchRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                99);

            Assert.That(atMinimum.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(atMinimum.EndTimestamp, Is.EqualTo(120));
            Assert.That(atMinimum.TimestampPolicy, Is.SameAs(policy));
            Assert.That(Fixture.Order(atMinimum).TimestampPolicy, Is.SameAs(policy));
            Assert.That(
                belowMinimum.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));

            var retentionDrift = new ProgressionTimestampPolicy(
                policy.PolicyVersion,
                policy.MinimumUtcTimestamp,
                policy.MaximumUtcTimestamp,
                policy.MaximumRetentionAgeSeconds - 1,
                policy.MaximumFutureLeadSeconds);
            ProgressionCompatibilityResult retentionDriftCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    timestampPolicy: retentionDrift);
            ProgressionStartPlan retentionDriftStart = Fixture.PlanResearch(
                retentionDriftCompatibility,
                Fixture.ResearchStart(
                    retentionDriftCompatibility,
                    research.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                retentionDriftCompatibility.StateRevision,
                Is.Not.EqualTo(researchCompatibility.StateRevision));
            Assert.That(
                retentionDriftStart.PlanHash,
                Is.Not.EqualTo(atMinimum.PlanHash));
            Assert.That(
                Fixture.Order(retentionDriftStart).OrderHash,
                Is.Not.EqualTo(Fixture.Order(atMinimum).OrderHash));

            ResearchProgressionDefinition beyondFutureDefinition =
                Fixture.Research(
                    "fake.research.timestamp-future",
                    unitDuration: 21,
                    maximumDuration: 21);
            ProgressionCompatibilityResult beyondFutureCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { beyondFutureDefinition },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    timestampPolicy: policy);
            ProgressionStartPlan beyondFuture = Fixture.PlanResearch(
                beyondFutureCompatibility,
                Fixture.ResearchStart(
                    beyondFutureCompatibility,
                    beyondFutureDefinition.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            Assert.That(
                beyondFuture.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));

            TroopProgressionDefinition training =
                Fixture.Troop("fake.troop.zero-duration-boundary");
            ProgressionCompatibilityResult trainingCompatibility =
                Fixture.TrainingCompatibility(
                    new[] { training },
                    Array.Empty<TroopProgressionStateRecord>(),
                    timestampPolicy: policy);
            ProgressionStartRequest trainingRequest = Fixture.TrainingStart(
                trainingCompatibility,
                training.Identity.Id,
                1);
            ProgressionStartPlan atMaximum = Fixture.PlanTraining(
                trainingCompatibility,
                trainingRequest,
                Fixture.Economy(ResourceType.Food, 1000),
                300);
            ProgressionStartPlan aboveMaximum = Fixture.PlanTraining(
                trainingCompatibility,
                trainingRequest,
                Fixture.Economy(ResourceType.Food, 1000),
                301);
            Assert.That(atMaximum.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(atMaximum.StartTimestamp, Is.EqualTo(atMaximum.EndTimestamp));
            Assert.That(
                aboveMaximum.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));

            ProgressionOrderSnapshot researchOrder = Fixture.Order(atMinimum);
            ProgressionCompletionRequest completion =
                Fixture.Completion(researchCompatibility, researchOrder);
            ProgressionCompletionPlan atEnd =
                ProgressionOrderPlanner.PlanCompletion(
                    researchCompatibility,
                    researchOrder,
                    completion,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    120);
            ProgressionCompletionPlan atRetention =
                ProgressionOrderPlanner.PlanCompletion(
                    researchCompatibility,
                    researchOrder,
                    completion,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    170);
            ProgressionCompletionPlan beyondRetention =
                ProgressionOrderPlanner.PlanCompletion(
                    researchCompatibility,
                    researchOrder,
                    completion,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    171);
            ProgressionCompletionPlan beforeStart =
                ProgressionOrderPlanner.PlanCompletion(
                    researchCompatibility,
                    researchOrder,
                    completion,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    99);
            ProgressionCompletionPlan atFutureBoundary =
                ProgressionOrderPlanner.PlanCompletion(
                    researchCompatibility,
                    researchOrder,
                    completion,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    100);
            ProgressionCompletionPlan aboveAbsoluteMaximum =
                ProgressionOrderPlanner.PlanCompletion(
                    researchCompatibility,
                    researchOrder,
                    completion,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    301);
            Assert.That(atEnd.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(atRetention.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                beyondRetention.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));
            Assert.That(
                beforeStart.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));
            Assert.That(
                atFutureBoundary.Status,
                Is.EqualTo(ProgressionPlanStatus.NotYetEligible));
            Assert.That(
                aboveAbsoluteMaximum.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));

            ProgressionReconciliationPlan atFutureReconciliation =
                ProgressionOrderPlanner.PlanReconciliation(
                    new[] { researchOrder },
                    100);
            ProgressionReconciliationPlan atRetentionReconciliation =
                ProgressionOrderPlanner.PlanReconciliation(
                    new[] { researchOrder },
                    170);
            ProgressionReconciliationPlan beyondRetentionReconciliation =
                ProgressionOrderPlanner.PlanReconciliation(
                    new[] { researchOrder },
                    171);
            ProgressionReconciliationPlan belowAbsoluteReconciliation =
                ProgressionOrderPlanner.PlanReconciliation(
                    new[] { researchOrder },
                    99);
            ProgressionReconciliationPlan aboveAbsoluteReconciliation =
                ProgressionOrderPlanner.PlanReconciliation(
                    new[] { researchOrder },
                    301);
            Assert.That(
                atFutureReconciliation.Status,
                Is.EqualTo(ProgressionPlanStatus.NoChange));
            Assert.That(
                atRetentionReconciliation.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                beyondRetentionReconciliation.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));
            Assert.That(
                belowAbsoluteReconciliation.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));
            Assert.That(
                aboveAbsoluteReconciliation.Status,
                Is.EqualTo(ProgressionPlanStatus.ClockInvalid));

            ProgressionOrderSnapshot zeroDurationOrder = Fixture.Order(atMaximum);
            ProgressionCompletionPlan zeroDurationCompletion =
                ProgressionOrderPlanner.PlanCompletion(
                    trainingCompatibility,
                    zeroDurationOrder,
                    Fixture.Completion(trainingCompatibility, zeroDurationOrder),
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    300);
            Assert.That(
                zeroDurationCompletion.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
        }

        [Test]
        public void HostileRequestIdentitiesRejectBeforeSemanticHashAllocation()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.hostile-request");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            string maximumPlusOne = new string(
                'x',
                ProgressionCompatibilityPlanner.MaximumIdUtf8Bytes + 1);
            string hostile = new string('x', 1024 * 1024);
            Func<string, ProgressionStartRequest>[] startRequests =
            {
                value => new ProgressionStartRequest(
                    value,
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    "hostile-order",
                    "hostile-start",
                    1,
                    0,
                    Fixture.CatalogSetId,
                    compatibility.StateRevision,
                    Fixture.EconomyRevision,
                    Fixture.StartPolicyVersion,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    value,
                    "hostile-order",
                    "hostile-start",
                    1,
                    0,
                    Fixture.CatalogSetId,
                    compatibility.StateRevision,
                    Fixture.EconomyRevision,
                    Fixture.StartPolicyVersion,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    value,
                    "hostile-start",
                    1,
                    0,
                    Fixture.CatalogSetId,
                    compatibility.StateRevision,
                    Fixture.EconomyRevision,
                    Fixture.StartPolicyVersion,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    "hostile-order",
                    value,
                    1,
                    0,
                    Fixture.CatalogSetId,
                    compatibility.StateRevision,
                    Fixture.EconomyRevision,
                    Fixture.StartPolicyVersion,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    "hostile-order",
                    "hostile-start",
                    1,
                    0,
                    value,
                    compatibility.StateRevision,
                    Fixture.EconomyRevision,
                    Fixture.StartPolicyVersion,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    "hostile-order",
                    "hostile-start",
                    1,
                    0,
                    Fixture.CatalogSetId,
                    value,
                    Fixture.EconomyRevision,
                    Fixture.StartPolicyVersion,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    "hostile-order",
                    "hostile-start",
                    1,
                    0,
                    Fixture.CatalogSetId,
                    compatibility.StateRevision,
                    value,
                    Fixture.StartPolicyVersion,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    "hostile-order",
                    "hostile-start",
                    1,
                    0,
                    Fixture.CatalogSetId,
                    compatibility.StateRevision,
                    Fixture.EconomyRevision,
                    value,
                    Fixture.NoPrerequisites.Revision),
                value => new ProgressionStartRequest(
                    "profile-1",
                    ProgressionOrderType.ResearchLevel,
                    definition.Identity.Id,
                    "hostile-order",
                    "hostile-start",
                    1,
                    0,
                    Fixture.CatalogSetId,
                    compatibility.StateRevision,
                    Fixture.EconomyRevision,
                    Fixture.StartPolicyVersion,
                    value)
            };

            ProgressionStartPlan ready = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(compatibility, definition.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            foreach (string invalidIdentity in
                     new[] { maximumPlusOne, hostile })
            {
                foreach (Func<string, ProgressionStartRequest> createRequest in
                         startRequests)
                {
                    ProgressionStartRequest request =
                        createRequest(invalidIdentity);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    ProgressionStartPlan result = Fixture.PlanResearch(
                        compatibility,
                        request,
                        Fixture.Economy(ResourceType.Gold, 1000),
                        Fixture.NoPrerequisites,
                        Fixture.NoReceipts,
                        Fixture.NoOrders,
                        100);
                    long allocated =
                        GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.That(
                        result.Status,
                        Is.EqualTo(ProgressionPlanStatus.InvalidRequest));
                    Assert.That(result.SemanticHash, Is.Empty);
                    Assert.That(result.PlanHash, Is.Empty);
                    Assert.That(
                        result.Diagnostics.All(diagnostic =>
                            diagnostic.DefinitionId.Length <=
                            ProgressionCompatibilityPlanner
                                .MaximumIdUtf8Bytes),
                        Is.True);
                    Assert.That(allocated, Is.LessThan(512L * 1024L));
                }
            }

            ProgressionOrderSnapshot order = Fixture.Order(ready);
            Func<string, ProgressionCompletionRequest>[] completionRequests =
            {
                value => new ProgressionCompletionRequest(
                    value,
                    order.OrderId,
                    order.CompletionOperationId,
                    compatibility.CatalogSetId,
                    compatibility.StateRevision,
                    order.EconomyRevision,
                    Fixture.QuestRevision,
                    Fixture.CompletionPolicyVersion),
                value => new ProgressionCompletionRequest(
                    order.ProfileId,
                    value,
                    order.CompletionOperationId,
                    compatibility.CatalogSetId,
                    compatibility.StateRevision,
                    order.EconomyRevision,
                    Fixture.QuestRevision,
                    Fixture.CompletionPolicyVersion),
                value => new ProgressionCompletionRequest(
                    order.ProfileId,
                    order.OrderId,
                    value,
                    compatibility.CatalogSetId,
                    compatibility.StateRevision,
                    order.EconomyRevision,
                    Fixture.QuestRevision,
                    Fixture.CompletionPolicyVersion),
                value => new ProgressionCompletionRequest(
                    order.ProfileId,
                    order.OrderId,
                    order.CompletionOperationId,
                    value,
                    compatibility.StateRevision,
                    order.EconomyRevision,
                    Fixture.QuestRevision,
                    Fixture.CompletionPolicyVersion),
                value => new ProgressionCompletionRequest(
                    order.ProfileId,
                    order.OrderId,
                    order.CompletionOperationId,
                    compatibility.CatalogSetId,
                    value,
                    order.EconomyRevision,
                    Fixture.QuestRevision,
                    Fixture.CompletionPolicyVersion),
                value => new ProgressionCompletionRequest(
                    order.ProfileId,
                    order.OrderId,
                    order.CompletionOperationId,
                    compatibility.CatalogSetId,
                    compatibility.StateRevision,
                    value,
                    Fixture.QuestRevision,
                    Fixture.CompletionPolicyVersion),
                value => new ProgressionCompletionRequest(
                    order.ProfileId,
                    order.OrderId,
                    order.CompletionOperationId,
                    compatibility.CatalogSetId,
                    compatibility.StateRevision,
                    order.EconomyRevision,
                    value,
                    Fixture.CompletionPolicyVersion),
                value => new ProgressionCompletionRequest(
                    order.ProfileId,
                    order.OrderId,
                    order.CompletionOperationId,
                    compatibility.CatalogSetId,
                    compatibility.StateRevision,
                    order.EconomyRevision,
                    Fixture.QuestRevision,
                    value)
            };
            foreach (string invalidIdentity in
                     new[] { maximumPlusOne, hostile })
            {
                foreach (
                    Func<string, ProgressionCompletionRequest> createRequest in
                    completionRequests)
                {
                    ProgressionCompletionRequest request =
                        createRequest(invalidIdentity);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    ProgressionCompletionPlan result =
                        ProgressionOrderPlanner.PlanCompletion(
                            compatibility,
                            order,
                            request,
                            Fixture.NoReceipts,
                            Fixture.CompletionDependencies,
                            order.EndTimestamp);
                    long allocated =
                        GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.That(
                        result.Status,
                        Is.EqualTo(ProgressionPlanStatus.InvalidRequest));
                    Assert.That(result.SemanticHash, Is.Empty);
                    Assert.That(result.PlanHash, Is.Empty);
                    Assert.That(allocated, Is.LessThan(512L * 1024L));
                }
            }
        }

        [Test]
        public void UnpairedSurrogateRequestsRejectWhileValidPairsRemainDistinct()
        {
            ResearchProgressionDefinition firstDefinition =
                Fixture.Research("fake.research.\U0001F600");
            ResearchProgressionDefinition secondDefinition =
                Fixture.Research("fake.research.\U0001F601");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { firstDefinition, secondDefinition },
                    Array.Empty<ResearchProgressionStateRecord>());
            var high = new ProgressionStartRequest(
                "profile-1",
                ProgressionOrderType.ResearchLevel,
                firstDefinition.Identity.Id,
                "surrogate-order-high",
                "surrogate.\uD800",
                1,
                0,
                compatibility.CatalogSetId,
                compatibility.StateRevision,
                Fixture.EconomyRevision,
                Fixture.StartPolicyVersion,
                Fixture.NoPrerequisites.Revision);
            var low = new ProgressionStartRequest(
                "profile-1",
                ProgressionOrderType.ResearchLevel,
                firstDefinition.Identity.Id,
                "surrogate-order-low",
                "surrogate.\uDC00",
                1,
                0,
                compatibility.CatalogSetId,
                compatibility.StateRevision,
                Fixture.EconomyRevision,
                Fixture.StartPolicyVersion,
                Fixture.NoPrerequisites.Revision);

            ProgressionStartPlan highResult = Fixture.PlanResearch(
                compatibility,
                high,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionStartPlan lowResult = Fixture.PlanResearch(
                compatibility,
                low,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionStartPlan first = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(
                    compatibility,
                    firstDefinition.Identity.Id,
                    1,
                    operationId: "paired-start-1",
                    orderId: "paired-order-1"),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionStartPlan second = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(
                    compatibility,
                    secondDefinition.Identity.Id,
                    1,
                    operationId: "paired-start-2",
                    orderId: "paired-order-2"),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);

            Assert.That(
                highResult.Status,
                Is.EqualTo(ProgressionPlanStatus.InvalidRequest));
            Assert.That(
                lowResult.Status,
                Is.EqualTo(ProgressionPlanStatus.InvalidRequest));
            Assert.That(highResult.SemanticHash, Is.Empty);
            Assert.That(lowResult.SemanticHash, Is.Empty);
            Assert.That(first.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(second.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(first.SemanticHash, Is.Not.EqualTo(second.SemanticHash));
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
            ProgressionOrderSnapshot committedOrder = Fixture.Order(ready);
            ProgressionOperationReceipt committed =
                ProgressionOrderPlanner.CreateCommittedReceipt(
                    ready,
                    committedOrder);

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
            Assert.That(exact.Costs, Is.EqualTo(ready.Costs));
            Assert.That(exact.TargetValue, Is.EqualTo(ready.TargetValue));
            Assert.That(exact.PreviousValue, Is.EqualTo(ready.PreviousValue));
            Assert.That(exact.StartTimestamp, Is.EqualTo(ready.StartTimestamp));
            Assert.That(exact.EndTimestamp, Is.EqualTo(ready.EndTimestamp));
            Assert.That(exact.CatalogRevision, Is.EqualTo(ready.CatalogRevision));
            Assert.That(
                exact.PrerequisiteRevision,
                Is.EqualTo(ready.PrerequisiteRevision));
            Assert.That(exact.PlanHash, Is.EqualTo(ready.PlanHash));
            Assert.That(exact.CommittedReceipt, Is.SameAs(committed));
            Assert.That(
                exact.CommittedReceipt.CommittedResult.CommitTimestamp,
                Is.EqualTo(ready.StartTimestamp));
            Assert.That(
                exact.CommittedReceipt.CommittedResult.OrderCatalogSetId,
                Is.EqualTo(committedOrder.CatalogSetId));
            Assert.That(
                exact.CommittedReceipt.CommittedResult.OrderCatalogRevision,
                Is.EqualTo(committedOrder.CatalogRevision));
            Assert.That(
                exact.CommittedReceipt.CommittedResult.OrderProgressionRevision,
                Is.EqualTo(committedOrder.ProgressionRevision));
            Assert.That(
                exact.CommittedReceipt.CommittedResult.OrderEconomyRevision,
                Is.EqualTo(committedOrder.EconomyRevision));

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
        public void CommittedStartRequiresExactActiveOrderAndReplaysItsPayload()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.committed-start-order");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartRequest request = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                operationId: "committed-start-operation",
                orderId: "committed-start-order");
            ProgressionStartPlan ready = Fixture.PlanResearch(
                compatibility,
                request,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot active =
                ProgressionOrderPlanner.CreateActiveOrder(
                    ready,
                    "committed-completion-operation",
                    "committed-cancellation-operation");

            Assert.That(
                ProgressionOrderPlanner.CreateCommittedReceipt(ready, null),
                Is.Null);
            Assert.That(
                ProgressionOrderPlanner.CreateCommittedReceipt(
                    ready,
                    Fixture.WithState(
                        active,
                        ProgressionOrderState.Completed)),
                Is.Null);

            ProgressionOperationReceipt receipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(ready, active);
            Assert.That(receipt, Is.Not.Null);
            Assert.That(
                receipt.CommittedResult.OrderId,
                Is.EqualTo(active.OrderId));
            Assert.That(
                receipt.CommittedResult.StartOperationId,
                Is.EqualTo(active.StartOperationId));
            Assert.That(
                receipt.CommittedResult.CompletionOperationId,
                Is.EqualTo(active.CompletionOperationId));
            Assert.That(
                receipt.CommittedResult.CancellationOperationId,
                Is.EqualTo(active.CancellationOperationId));
            Assert.That(
                receipt.CommittedResult.OrderHash,
                Is.EqualTo(active.OrderHash));

            ProgressionStartPlan replay = Fixture.PlanResearch(
                null,
                request,
                null,
                Fixture.NoPrerequisites,
                new[] { receipt },
                null,
                0);
            Assert.That(
                replay.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(replay.CommittedReceipt, Is.SameAs(receipt));
            Assert.That(replay.OrderId, Is.EqualTo(active.OrderId));
        }

        [Test]
        public void OrderAndOperationIdentifiersMustBePairwiseDistinct()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.distinct-order-identifiers");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan ready = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(
                    compatibility,
                    definition.Identity.Id,
                    1,
                    operationId: "distinct-start-operation",
                    orderId: "distinct-order"),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);

            Assert.That(
                ProgressionOrderPlanner.CreateActiveOrder(
                    ready,
                    ready.OrderId),
                Is.Null);
            Assert.That(
                ProgressionOrderPlanner.CreateActiveOrder(
                    ready,
                    "distinct-completion-operation",
                    ready.OrderId),
                Is.Null);
            Assert.That(
                ProgressionOrderPlanner.CreateActiveOrder(
                    ready,
                    ready.OperationId),
                Is.Null);
            Assert.That(
                ProgressionOrderPlanner.CreateActiveOrder(
                    ready,
                    "distinct-completion-operation",
                    ready.OperationId),
                Is.Null);
            Assert.That(
                ProgressionOrderPlanner.CreateActiveOrder(
                    ready,
                    "distinct-completion-operation",
                    "distinct-cancellation-operation"),
                Is.Not.Null);
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
        public void PaidResearchOrderUsesCommittedSnapshotAcrossTypedSourceDrift()
        {
            ResearchProgressionDefinition original =
                Fixture.Research("fake.research.source-drift");
            ProgressionCompatibilityResult initial =
                Fixture.ResearchCompatibility(
                    new[] { original },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanResearch(
                initial,
                Fixture.ResearchStart(initial, original.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);

            ResearchProgressionDefinition compatibleDefinition =
                Fixture.ReissueResearch(original, reissueProfiles: true);
            ResearchProgressionDefinition migrationDefinition =
                Fixture.ReissueResearch(
                    original,
                    contentVersion: "fake.content.v2");
            ResearchProgressionDefinition unsupportedDefinition =
                Fixture.ReissueResearch(
                    original,
                    schemaVersion: "al.progression.research.v99");
            ResearchProgressionDefinition unsupportedCostDefinition =
                Fixture.ReissueResearch(
                    original,
                    costSchemaVersion: "al.progression.profile.v99");
            ResearchProgressionDefinition unsupportedDurationDefinition =
                Fixture.ReissueResearch(
                    original,
                    durationSchemaVersion: "al.progression.profile.v99");
            ProgressionCompatibilityResult exact =
                Fixture.ResearchCompatibility(
                    new[] { original },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionCompatibilityResult compatible =
                Fixture.ResearchCompatibility(
                    new[] { compatibleDefinition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionCompatibilityResult migration =
                Fixture.ResearchCompatibility(
                    new[]
                    {
                        migrationDefinition,
                        Fixture.Research(
                            "fake.research.source-drift-unrelated")
                    },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            order.DefinitionId,
                            order.DefinitionContentVersion,
                            (int)order.PreviousValue,
                            false,
                            0)
                    });
            ProgressionCompatibilityResult unsupported =
                Fixture.ResearchCompatibility(
                    new[] { unsupportedDefinition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionCompatibilityResult unsupportedCost =
                Fixture.ResearchCompatibility(
                    new[] { unsupportedCostDefinition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionCompatibilityResult unsupportedDuration =
                Fixture.ResearchCompatibility(
                    new[] { unsupportedDurationDefinition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionCompatibilityResult catalogReissue =
                Fixture.ResearchCompatibility(
                    new[] { original },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    catalogRevision: "fake.catalog.rev.2");
            var removedRow = new ResearchProgressionStateRecord(
                order.DefinitionId,
                order.DefinitionContentVersion,
                (int)order.PreviousValue,
                false,
                0);
            ProgressionCompatibilityResult removed =
                Fixture.ResearchCompatibility(
                    new[]
                    {
                        Fixture.Research("fake.research.unrelated-current")
                    },
                    new[] { removedRow });
            ProgressionCompatibilityResult rowVersionMismatch =
                Fixture.ResearchCompatibility(
                    new[] { original },
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            order.DefinitionId,
                            "fake.content.v2",
                            (int)order.PreviousValue,
                            false,
                            0)
                    });

            ProgressionCompletionPlan exactPlan = Fixture.PlanCompletion(
                exact,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan compatiblePlan = Fixture.PlanCompletion(
                compatible,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan migrationPlan = Fixture.PlanCompletion(
                migration,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan unsupportedPlan = Fixture.PlanCompletion(
                unsupported,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan unsupportedCostPlan =
                Fixture.PlanCompletion(
                    unsupportedCost,
                    order,
                    order.EndTimestamp);
            ProgressionCompletionPlan unsupportedDurationPlan =
                Fixture.PlanCompletion(
                    unsupportedDuration,
                    order,
                    order.EndTimestamp);
            ProgressionCompletionPlan catalogReissuePlan =
                Fixture.PlanCompletion(
                    catalogReissue,
                    order,
                    order.EndTimestamp);
            ProgressionCompletionPlan removedPlan = Fixture.PlanCompletion(
                removed,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan rowVersionMismatchPlan =
                Fixture.PlanCompletion(
                    rowVersionMismatch,
                    order,
                    order.EndTimestamp);

            Assert.That(exactPlan.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                exactPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition.ExactCurrentSource));
            Assert.That(
                compatiblePlan.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                compatiblePlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .CompatibleCompleteUnderCommittedSnapshot));
            Assert.That(
                compatiblePlan.TargetValue,
                Is.EqualTo(order.TargetValue));
            Assert.That(
                compatiblePlan.OrderSnapshot.CommittedCosts,
                Is.EqualTo(order.CommittedCosts));
            Assert.That(
                compatiblePlan.OrderSnapshot.EndTimestamp,
                Is.EqualTo(order.EndTimestamp));
            Assert.That(
                catalogReissuePlan.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                catalogReissuePlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .CompatibleCompleteUnderCommittedSnapshot));
            Assert.That(
                migrationPlan.Status,
                Is.EqualTo(ProgressionPlanStatus.MigrationRequired));
            Assert.That(
                migrationPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition.MigrationRequired));
            foreach (ProgressionCompletionPlan unsupportedSourcePlan in
                     new[]
                     {
                         unsupportedPlan,
                         unsupportedCostPlan,
                         unsupportedDurationPlan
                     })
            {
                Assert.That(
                    unsupportedSourcePlan.Status,
                    Is.EqualTo(ProgressionPlanStatus.UnsupportedVersion));
                Assert.That(
                    unsupportedSourcePlan.SourceDisposition,
                    Is.EqualTo(ProgressionOrderSourceDisposition
                        .UnsupportedVersion));
            }
            Assert.That(
                removedPlan.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                removedPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .DefinitionRemovedButLegacyOrderPreserved));
            Assert.That(removedPlan.TargetValue, Is.EqualTo(order.TargetValue));
            Assert.That(removedRow.Level, Is.EqualTo(order.PreviousValue));
            Assert.That(
                rowVersionMismatchPlan.Status,
                Is.EqualTo(ProgressionPlanStatus.MigrationRequired));
            Assert.That(
                rowVersionMismatchPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .MigrationRequired));
            Assert.That(order.DefinitionSource, Is.SameAs(original.Identity));
        }

        [Test]
        public void PaidTrainingOrderUsesCommittedSnapshotAcrossTypedSourceDrift()
        {
            TroopProgressionDefinition original =
                Fixture.Troop("fake.troop.source-drift");
            ProgressionCompatibilityResult initial =
                Fixture.TrainingCompatibility(
                    new[] { original },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanTraining(
                initial,
                Fixture.TrainingStart(initial, original.Identity.Id, 5),
                Fixture.Economy(ResourceType.Food, 1000),
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);

            TroopProgressionDefinition compatibleDefinition =
                Fixture.ReissueTroop(original, reissueProfiles: true);
            TroopProgressionDefinition migrationDefinition =
                Fixture.ReissueTroop(
                    original,
                    contentVersion: "fake.content.v2");
            TroopProgressionDefinition unsupportedDefinition =
                Fixture.ReissueTroop(
                    original,
                    schemaVersion: "al.progression.troop.v99");
            TroopProgressionDefinition unsupportedCostDefinition =
                Fixture.ReissueTroop(
                    original,
                    costSchemaVersion: "al.progression.profile.v99");
            TroopProgressionDefinition unsupportedDurationDefinition =
                Fixture.ReissueTroop(
                    original,
                    durationSchemaVersion: "al.progression.profile.v99");
            ProgressionCompatibilityResult exact =
                Fixture.TrainingCompatibility(
                    new[] { original },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionCompatibilityResult compatible =
                Fixture.TrainingCompatibility(
                    new[] { compatibleDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionCompatibilityResult migration =
                Fixture.TrainingCompatibility(
                    new[]
                    {
                        migrationDefinition,
                        Fixture.Troop(
                            "fake.troop.source-drift-unrelated")
                    },
                    new[]
                    {
                        new TroopProgressionStateRecord(
                            order.DefinitionId,
                            order.DefinitionContentVersion,
                            order.PreviousValue,
                            0,
                            0)
                    });
            ProgressionCompatibilityResult unsupported =
                Fixture.TrainingCompatibility(
                    new[] { unsupportedDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionCompatibilityResult unsupportedCost =
                Fixture.TrainingCompatibility(
                    new[] { unsupportedCostDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionCompatibilityResult unsupportedDuration =
                Fixture.TrainingCompatibility(
                    new[] { unsupportedDurationDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionCompatibilityResult catalogReissue =
                Fixture.TrainingCompatibility(
                    new[] { original },
                    Array.Empty<TroopProgressionStateRecord>(),
                    catalogRevision: "fake.catalog.rev.2");
            var removedRow = new TroopProgressionStateRecord(
                order.DefinitionId,
                order.DefinitionContentVersion,
                order.PreviousValue,
                0,
                0);
            ProgressionCompatibilityResult removed =
                Fixture.TrainingCompatibility(
                    new[]
                    {
                        Fixture.Troop("fake.troop.unrelated-current")
                    },
                    new[] { removedRow });
            ProgressionCompatibilityResult rowVersionMismatch =
                Fixture.TrainingCompatibility(
                    new[] { original },
                    new[]
                    {
                        new TroopProgressionStateRecord(
                            order.DefinitionId,
                            "fake.content.v2",
                            order.PreviousValue,
                            0,
                            0)
                    });

            ProgressionCompletionPlan exactPlan = Fixture.PlanCompletion(
                exact,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan compatiblePlan = Fixture.PlanCompletion(
                compatible,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan migrationPlan = Fixture.PlanCompletion(
                migration,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan unsupportedPlan = Fixture.PlanCompletion(
                unsupported,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan unsupportedCostPlan =
                Fixture.PlanCompletion(
                    unsupportedCost,
                    order,
                    order.EndTimestamp);
            ProgressionCompletionPlan unsupportedDurationPlan =
                Fixture.PlanCompletion(
                    unsupportedDuration,
                    order,
                    order.EndTimestamp);
            ProgressionCompletionPlan catalogReissuePlan =
                Fixture.PlanCompletion(
                    catalogReissue,
                    order,
                    order.EndTimestamp);
            ProgressionCompletionPlan removedPlan = Fixture.PlanCompletion(
                removed,
                order,
                order.EndTimestamp);
            ProgressionCompletionPlan rowVersionMismatchPlan =
                Fixture.PlanCompletion(
                    rowVersionMismatch,
                    order,
                    order.EndTimestamp);

            Assert.That(exactPlan.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                exactPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition.ExactCurrentSource));
            Assert.That(
                compatiblePlan.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                compatiblePlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .CompatibleCompleteUnderCommittedSnapshot));
            Assert.That(
                compatiblePlan.OrderSnapshot.CommittedCosts,
                Is.EqualTo(order.CommittedCosts));
            Assert.That(
                catalogReissuePlan.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                catalogReissuePlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .CompatibleCompleteUnderCommittedSnapshot));
            Assert.That(
                migrationPlan.Status,
                Is.EqualTo(ProgressionPlanStatus.MigrationRequired));
            Assert.That(
                migrationPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition.MigrationRequired));
            foreach (ProgressionCompletionPlan unsupportedSourcePlan in
                     new[]
                     {
                         unsupportedPlan,
                         unsupportedCostPlan,
                         unsupportedDurationPlan
                     })
            {
                Assert.That(
                    unsupportedSourcePlan.Status,
                    Is.EqualTo(ProgressionPlanStatus.UnsupportedVersion));
                Assert.That(
                    unsupportedSourcePlan.SourceDisposition,
                    Is.EqualTo(ProgressionOrderSourceDisposition
                        .UnsupportedVersion));
            }
            Assert.That(
                removedPlan.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                removedPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .DefinitionRemovedButLegacyOrderPreserved));
            Assert.That(removedPlan.TargetValue, Is.EqualTo(order.TargetValue));
            Assert.That(removedRow.ActiveCount, Is.EqualTo(order.PreviousValue));
            Assert.That(
                rowVersionMismatchPlan.Status,
                Is.EqualTo(ProgressionPlanStatus.MigrationRequired));
            Assert.That(
                rowVersionMismatchPlan.SourceDisposition,
                Is.EqualTo(ProgressionOrderSourceDisposition
                    .MigrationRequired));
            Assert.That(order.MaximumValue, Is.EqualTo(original.MaximumInventoryCount));
            Assert.That(
                order.InventoryCapacityPolicy,
                Is.EqualTo(original.InventoryCapacityPolicy));
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

            ProgressionOperationReceipt receipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(ready);
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
            Assert.That(
                replay.QuestProgressAmount,
                Is.EqualTo(ready.QuestProgressAmount));
            Assert.That(replay.PreviousValue, Is.EqualTo(ready.PreviousValue));
            Assert.That(replay.TargetValue, Is.EqualTo(ready.TargetValue));
            Assert.That(replay.CommitTimestamp, Is.EqualTo(ready.CommitTimestamp));
            Assert.That(replay.CommittedReceipt, Is.SameAs(receipt));

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
                Array.Empty<ProgressionDiagnostic>(),
                valid.ResearchDefinitions,
                valid.TroopDefinitions,
                valid.TimestampPolicy,
                valid.HasDefinitionSource);
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
                Array.Empty<ProgressionDiagnostic>(),
                valid.ResearchDefinitions,
                valid.TroopDefinitions,
                valid.TimestampPolicy,
                valid.HasDefinitionSource);
            var forgedMixed = new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                ProgressionCompatibilityStatus.Available,
                valid.CatalogSetId,
                valid.CatalogRevision,
                valid.StateRevision,
                new[] { valid.Research[0], null },
                Array.Empty<TroopProgressionSnapshot>(),
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>(),
                valid.ResearchDefinitions,
                valid.TroopDefinitions,
                valid.TimestampPolicy,
                valid.HasDefinitionSource);
            ProgressionStartPlan validStart = Fixture.PlanResearch(
                valid,
                Fixture.ResearchStart(valid, definition.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot validOrder = Fixture.Order(validStart);

            foreach (ProgressionCompatibilityResult forged in
                     new[] { forgedNull, forgedDuplicate, forgedMixed })
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

                ProgressionCompletionPlan completion = null;
                Assert.That(
                    () => completion = ProgressionOrderPlanner.PlanCompletion(
                        forged,
                        validOrder,
                        Fixture.Completion(forged, validOrder),
                        Fixture.NoReceipts,
                        Fixture.CompletionDependencies,
                        validOrder.EndTimestamp),
                    Throws.Nothing);
                Assert.That(
                    completion.Status,
                    Is.EqualTo(ProgressionPlanStatus.StateMalformed));
            }

            TroopProgressionDefinition troopDefinition =
                Fixture.Troop("fake.troop.forged-mixed");
            ProgressionCompatibilityResult validTraining =
                Fixture.TrainingCompatibility(
                    new[] { troopDefinition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan validTrainingStart = Fixture.PlanTraining(
                validTraining,
                Fixture.TrainingStart(
                    validTraining,
                    troopDefinition.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Food, 1000),
                100);
            ProgressionOrderSnapshot validTrainingOrder =
                Fixture.Order(validTrainingStart);
            var forgedTraining = new ProgressionCompatibilityResult(
                ProgressionDomain.Training,
                ProgressionCompatibilityStatus.Available,
                validTraining.CatalogSetId,
                validTraining.CatalogRevision,
                validTraining.StateRevision,
                Array.Empty<ResearchProgressionSnapshot>(),
                new[] { validTraining.Troops[0], null },
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>(),
                validTraining.ResearchDefinitions,
                validTraining.TroopDefinitions,
                validTraining.TimestampPolicy,
                validTraining.HasDefinitionSource);
            ProgressionCompletionPlan trainingCompletion = null;
            Assert.That(
                () => trainingCompletion =
                    ProgressionOrderPlanner.PlanCompletion(
                        forgedTraining,
                        validTrainingOrder,
                        Fixture.Completion(
                            forgedTraining,
                            validTrainingOrder),
                        Fixture.NoReceipts,
                        Fixture.CompletionDependencies,
                        validTrainingOrder.EndTimestamp),
                Throws.Nothing);
            Assert.That(
                trainingCompletion.Status,
                Is.EqualTo(ProgressionPlanStatus.StateMalformed));
        }

        [Test]
        public void ForgedAvailableAuthorityCannotInjectDefinitionsOrInitialState()
        {
            ResearchProgressionDefinition research =
                Fixture.Research("fake.research.forged-authority");
            ProgressionCompatibilityResult validResearch =
                Fixture.ResearchCompatibility(
                    new[] { research },
                    Array.Empty<ResearchProgressionStateRecord>());
            Assert.That(
                Fixture.PlanResearch(
                    validResearch,
                    Fixture.ResearchStart(
                        validResearch,
                        research.Identity.Id,
                        1),
                    Fixture.Economy(ResourceType.Gold, 1000),
                    Fixture.NoPrerequisites,
                    Fixture.NoReceipts,
                    Fixture.NoOrders,
                    100).Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            var attackerResearch = new ResearchProgressionDefinition(
                research.Identity,
                research.InitialLevel,
                research.MaximumLevel,
                new ProgressionCostProfile(
                    research.CostProfile.Identity,
                    new[]
                    {
                        new BuildingConstructionCost(ResourceType.Gold, 1)
                    },
                    research.CostProfile.MaximumAmountPerResource),
                new ProgressionDurationProfile(
                    research.DurationProfile.Identity,
                    1,
                    research.DurationProfile.MaximumSeconds,
                    false),
                research.Prerequisites,
                research.EffectProfiles);
            var attackerResearchSnapshot = new ResearchProgressionSnapshot(
                attackerResearch,
                attackerResearch.InitialLevel,
                ProgressionStateOrigin.EffectiveInitialUnpersisted,
                false,
                0);
            var wrongInitialResearchSnapshot =
                new ResearchProgressionSnapshot(
                    research,
                    research.InitialLevel + 1,
                    ProgressionStateOrigin.EffectiveInitialUnpersisted,
                    false,
                    0);
            var clonedResearch = new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                ProgressionCompatibilityStatus.Available,
                validResearch.CatalogSetId,
                validResearch.CatalogRevision,
                validResearch.StateRevision,
                validResearch.Research,
                validResearch.Troops,
                validResearch.PreservedResearchStates,
                validResearch.PreservedTroopStates,
                validResearch.Diagnostics,
                validResearch.ResearchDefinitions,
                validResearch.TroopDefinitions,
                validResearch.TimestampPolicy,
                validResearch.HasDefinitionSource);
            var detachedResearch = new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                ProgressionCompatibilityStatus.Available,
                validResearch.CatalogSetId,
                validResearch.CatalogRevision,
                validResearch.StateRevision,
                new[] { attackerResearchSnapshot },
                Array.Empty<TroopProgressionSnapshot>(),
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>(),
                validResearch.ResearchDefinitions,
                validResearch.TroopDefinitions,
                validResearch.TimestampPolicy,
                true);
            var replacedResearchSource = new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                ProgressionCompatibilityStatus.Available,
                validResearch.CatalogSetId,
                validResearch.CatalogRevision,
                validResearch.StateRevision,
                new[] { attackerResearchSnapshot },
                Array.Empty<TroopProgressionSnapshot>(),
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>(),
                new[] { attackerResearch },
                Array.Empty<TroopProgressionDefinition>(),
                validResearch.TimestampPolicy,
                true);
            var forgedResearchInitial = new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                ProgressionCompatibilityStatus.Available,
                validResearch.CatalogSetId,
                validResearch.CatalogRevision,
                validResearch.StateRevision,
                new[] { wrongInitialResearchSnapshot },
                Array.Empty<TroopProgressionSnapshot>(),
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>(),
                validResearch.ResearchDefinitions,
                validResearch.TroopDefinitions,
                validResearch.TimestampPolicy,
                true);
            ProgressionCompatibilityResult trustedDetachedResearch =
                Fixture.TrustedPlannerResult(
                    ProgressionDomain.Research,
                    validResearch.StateRevision,
                    new[] { attackerResearchSnapshot },
                    Array.Empty<TroopProgressionSnapshot>(),
                    Array.Empty<ResearchProgressionStateRecord>(),
                    Array.Empty<TroopProgressionStateRecord>(),
                    validResearch.ResearchDefinitions,
                    Array.Empty<TroopProgressionDefinition>());
            ProgressionCompatibilityResult trustedWrongResearchInitial =
                Fixture.TrustedPlannerResult(
                    ProgressionDomain.Research,
                    validResearch.StateRevision,
                    new[] { wrongInitialResearchSnapshot },
                    Array.Empty<TroopProgressionSnapshot>(),
                    Array.Empty<ResearchProgressionStateRecord>(),
                    Array.Empty<TroopProgressionStateRecord>(),
                    validResearch.ResearchDefinitions,
                    Array.Empty<TroopProgressionDefinition>());
            var mismatchedSavedResearchSnapshot =
                new ResearchProgressionSnapshot(
                    research,
                    1,
                    ProgressionStateOrigin.Saved,
                    false,
                    0);
            ProgressionCompatibilityResult trustedMismatchedResearchRaw =
                Fixture.TrustedPlannerResult(
                    ProgressionDomain.Research,
                    validResearch.StateRevision,
                    new[] { mismatchedSavedResearchSnapshot },
                    Array.Empty<TroopProgressionSnapshot>(),
                    new[]
                    {
                        new ResearchProgressionStateRecord(
                            research.Identity.Id,
                            research.Identity.ContentVersion,
                            0,
                            false,
                            0)
                    },
                    Array.Empty<TroopProgressionStateRecord>(),
                    validResearch.ResearchDefinitions,
                    Array.Empty<TroopProgressionDefinition>());

            foreach (var candidate in new[]
                     {
                         new
                         {
                             Compatibility = clonedResearch,
                             Target = 1
                         },
                         new
                         {
                             Compatibility = detachedResearch,
                             Target = 1
                         },
                         new
                         {
                             Compatibility = replacedResearchSource,
                             Target = 1
                         },
                         new
                         {
                             Compatibility = forgedResearchInitial,
                             Target = 2
                         },
                         new
                         {
                             Compatibility = trustedDetachedResearch,
                             Target = 1
                         },
                         new
                         {
                             Compatibility = trustedWrongResearchInitial,
                             Target = 2
                         },
                         new
                         {
                             Compatibility = trustedMismatchedResearchRaw,
                             Target = 2
                         }
                     })
            {
                ProgressionStartPlan plan = Fixture.PlanResearch(
                    candidate.Compatibility,
                    Fixture.ResearchStart(
                        candidate.Compatibility,
                        research.Identity.Id,
                        candidate.Target),
                    Fixture.Economy(ResourceType.Gold, 1000),
                    Fixture.NoPrerequisites,
                    Fixture.NoReceipts,
                    Fixture.NoOrders,
                    100);
                Assert.That(
                    plan.Status,
                    Is.EqualTo(ProgressionPlanStatus.StateMalformed));
                Assert.That(plan.CanCommit, Is.False);
            }

            TroopProgressionDefinition troop =
                Fixture.Troop("fake.troop.forged-authority");
            ProgressionCompatibilityResult validTraining =
                Fixture.TrainingCompatibility(
                    new[] { troop },
                    Array.Empty<TroopProgressionStateRecord>());
            Assert.That(
                Fixture.PlanTraining(
                    validTraining,
                    Fixture.TrainingStart(
                        validTraining,
                        troop.Identity.Id,
                        1),
                    Fixture.Economy(ResourceType.Food, 1000),
                    100).Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            var attackerTroop = new TroopProgressionDefinition(
                troop.Identity,
                troop.MaximumInventoryCount,
                troop.MaximumBatchCount,
                new ProgressionCostProfile(
                    troop.CostProfile.Identity,
                    new[]
                    {
                        new BuildingConstructionCost(ResourceType.Food, 1)
                    },
                    troop.CostProfile.MaximumAmountPerResource),
                troop.DurationProfile,
                troop.Prerequisites,
                troop.BattleProfile,
                troop.InventoryPolicy,
                troop.InventoryCapacityPolicy);
            var attackerTroopSnapshot = new TroopProgressionSnapshot(
                attackerTroop,
                0,
                0,
                0,
                ProgressionStateOrigin.EffectiveInitialUnpersisted);
            var nonzeroInitialTroopSnapshot = new TroopProgressionSnapshot(
                troop,
                1,
                1,
                1,
                ProgressionStateOrigin.EffectiveInitialUnpersisted);
            var clonedTraining = new ProgressionCompatibilityResult(
                ProgressionDomain.Training,
                ProgressionCompatibilityStatus.Available,
                validTraining.CatalogSetId,
                validTraining.CatalogRevision,
                validTraining.StateRevision,
                validTraining.Research,
                validTraining.Troops,
                validTraining.PreservedResearchStates,
                validTraining.PreservedTroopStates,
                validTraining.Diagnostics,
                validTraining.ResearchDefinitions,
                validTraining.TroopDefinitions,
                validTraining.TimestampPolicy,
                validTraining.HasDefinitionSource);
            var replacedTrainingSource = new ProgressionCompatibilityResult(
                ProgressionDomain.Training,
                ProgressionCompatibilityStatus.Available,
                validTraining.CatalogSetId,
                validTraining.CatalogRevision,
                validTraining.StateRevision,
                Array.Empty<ResearchProgressionSnapshot>(),
                new[] { attackerTroopSnapshot },
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>(),
                Array.Empty<ResearchProgressionDefinition>(),
                new[] { attackerTroop },
                validTraining.TimestampPolicy,
                true);
            var forgedTrainingInitial = new ProgressionCompatibilityResult(
                ProgressionDomain.Training,
                ProgressionCompatibilityStatus.Available,
                validTraining.CatalogSetId,
                validTraining.CatalogRevision,
                validTraining.StateRevision,
                Array.Empty<ResearchProgressionSnapshot>(),
                new[] { nonzeroInitialTroopSnapshot },
                Array.Empty<ResearchProgressionStateRecord>(),
                Array.Empty<TroopProgressionStateRecord>(),
                Array.Empty<ProgressionDiagnostic>(),
                Array.Empty<ResearchProgressionDefinition>(),
                validTraining.TroopDefinitions,
                validTraining.TimestampPolicy,
                true);
            ProgressionCompatibilityResult trustedDetachedTraining =
                Fixture.TrustedPlannerResult(
                    ProgressionDomain.Training,
                    validTraining.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    new[] { attackerTroopSnapshot },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    Array.Empty<TroopProgressionStateRecord>(),
                    Array.Empty<ResearchProgressionDefinition>(),
                    validTraining.TroopDefinitions);
            ProgressionCompatibilityResult trustedWrongTrainingInitial =
                Fixture.TrustedPlannerResult(
                    ProgressionDomain.Training,
                    validTraining.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    new[] { nonzeroInitialTroopSnapshot },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    Array.Empty<TroopProgressionStateRecord>(),
                    Array.Empty<ResearchProgressionDefinition>(),
                    validTraining.TroopDefinitions);
            var mismatchedSavedTroopSnapshot =
                new TroopProgressionSnapshot(
                    troop,
                    1,
                    0,
                    0,
                    ProgressionStateOrigin.Saved);
            ProgressionCompatibilityResult trustedMismatchedTrainingRaw =
                Fixture.TrustedPlannerResult(
                    ProgressionDomain.Training,
                    validTraining.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    new[] { mismatchedSavedTroopSnapshot },
                    Array.Empty<ResearchProgressionStateRecord>(),
                    new[]
                    {
                        new TroopProgressionStateRecord(
                            troop.Identity.Id,
                            troop.Identity.ContentVersion,
                            0,
                            0,
                            0)
                    },
                    Array.Empty<ResearchProgressionDefinition>(),
                    validTraining.TroopDefinitions);

            foreach (ProgressionCompatibilityResult forged in
                     new[]
                     {
                         clonedTraining,
                         replacedTrainingSource,
                         forgedTrainingInitial,
                         trustedDetachedTraining,
                         trustedWrongTrainingInitial,
                         trustedMismatchedTrainingRaw
                     })
            {
                ProgressionStartPlan plan = Fixture.PlanTraining(
                    forged,
                    Fixture.TrainingStart(
                        forged,
                        troop.Identity.Id,
                        1),
                    Fixture.Economy(ResourceType.Food, 1000),
                    100);
                Assert.That(
                    plan.Status,
                    Is.EqualTo(ProgressionPlanStatus.StateMalformed));
                Assert.That(plan.CanCommit, Is.False);
            }
        }

        [Test]
        public void ResearchCompletionRejectsPublicAvailableCloneWithoutPlannerProvenance()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.forged-completion");
            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    new[] { definition },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanResearch(
                compatibility,
                Fixture.ResearchStart(
                    compatibility,
                    definition.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);

            ProgressionCompletionPlan valid = Fixture.PlanCompletion(
                compatibility,
                order,
                order.EndTimestamp);
            Assert.That(valid.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(valid.CanCommit, Is.True);

            var publicClone = new ProgressionCompatibilityResult(
                compatibility.Domain,
                compatibility.Status,
                compatibility.CatalogSetId,
                compatibility.CatalogRevision,
                compatibility.StateRevision,
                compatibility.Research,
                compatibility.Troops,
                compatibility.PreservedResearchStates,
                compatibility.PreservedTroopStates,
                compatibility.Diagnostics,
                compatibility.ResearchDefinitions,
                compatibility.TroopDefinitions,
                compatibility.TimestampPolicy,
                compatibility.HasDefinitionSource);

            ProgressionCompletionPlan forged = Fixture.PlanCompletion(
                publicClone,
                order,
                order.EndTimestamp);
            Assert.That(
                forged.Status,
                Is.EqualTo(ProgressionPlanStatus.StateMalformed));
            Assert.That(forged.CanCommit, Is.False);
            Assert.That(
                forged.Diagnostics.Single().Code,
                Is.EqualTo(ProgressionDiagnosticCode.StateUnavailable));
        }

        [Test]
        public void TrainingCompletionRejectsPublicAvailableCloneWithoutPlannerProvenance()
        {
            TroopProgressionDefinition definition =
                Fixture.Troop("fake.troop.forged-completion");
            ProgressionCompatibilityResult compatibility =
                Fixture.TrainingCompatibility(
                    new[] { definition },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan start = Fixture.PlanTraining(
                compatibility,
                Fixture.TrainingStart(
                    compatibility,
                    definition.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Food, 1000),
                100);
            ProgressionOrderSnapshot order = Fixture.Order(start);

            ProgressionCompletionPlan valid = Fixture.PlanCompletion(
                compatibility,
                order,
                order.EndTimestamp);
            Assert.That(valid.Status, Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(valid.CanCommit, Is.True);

            var publicClone = new ProgressionCompatibilityResult(
                compatibility.Domain,
                compatibility.Status,
                compatibility.CatalogSetId,
                compatibility.CatalogRevision,
                compatibility.StateRevision,
                compatibility.Research,
                compatibility.Troops,
                compatibility.PreservedResearchStates,
                compatibility.PreservedTroopStates,
                compatibility.Diagnostics,
                compatibility.ResearchDefinitions,
                compatibility.TroopDefinitions,
                compatibility.TimestampPolicy,
                compatibility.HasDefinitionSource);

            ProgressionCompletionPlan forged = Fixture.PlanCompletion(
                publicClone,
                order,
                order.EndTimestamp);
            Assert.That(
                forged.Status,
                Is.EqualTo(ProgressionPlanStatus.StateMalformed));
            Assert.That(forged.CanCommit, Is.False);
            Assert.That(
                forged.Diagnostics.Single().Code,
                Is.EqualTo(ProgressionDiagnosticCode.StateUnavailable));
        }

        [Test]
        public void CompletionRejectsSemanticallyInconsistentNonTargetRows()
        {
            ResearchProgressionDefinition target =
                Fixture.Research("fake.research.semantic-target");
            ResearchProgressionDefinition unrelated =
                Fixture.Research("fake.research.semantic-unrelated");
            ProgressionCompatibilityResult research =
                Fixture.ResearchCompatibility(
                    new[] { target, unrelated },
                    Array.Empty<ResearchProgressionStateRecord>());
            ProgressionStartPlan researchStart = Fixture.PlanResearch(
                research,
                Fixture.ResearchStart(research, target.Identity.Id, 1),
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOrderSnapshot researchOrder =
                Fixture.Order(researchStart);
            ResearchProgressionSnapshot targetSnapshot =
                research.Research.Single(snapshot =>
                    string.Equals(
                        snapshot.Definition.Identity.Id,
                        target.Identity.Id,
                        StringComparison.Ordinal));
            ResearchProgressionSnapshot unrelatedSnapshot =
                research.Research.Single(snapshot =>
                    string.Equals(
                        snapshot.Definition.Identity.Id,
                        unrelated.Identity.Id,
                        StringComparison.Ordinal));
            Func<IEnumerable<ResearchProgressionSnapshot>,
                IEnumerable<ResearchProgressionStateRecord>,
                ProgressionCompatibilityResult> forgeResearch =
                (snapshots, states) => new ProgressionCompatibilityResult(
                    ProgressionDomain.Research,
                    ProgressionCompatibilityStatus.Available,
                    research.CatalogSetId,
                    research.CatalogRevision,
                    research.StateRevision,
                    snapshots,
                    Array.Empty<TroopProgressionSnapshot>(),
                    states,
                    Array.Empty<TroopProgressionStateRecord>(),
                    Array.Empty<ProgressionDiagnostic>(),
                    research.ResearchDefinitions,
                    research.TroopDefinitions,
                    research.TimestampPolicy,
                    research.HasDefinitionSource);
            var mismatchedState = new ResearchProgressionStateRecord(
                unrelated.Identity.Id,
                unrelated.Identity.ContentVersion,
                2,
                false,
                0);
            var mismatchedSnapshot = new ResearchProgressionSnapshot(
                unrelated,
                1,
                ProgressionStateOrigin.Saved,
                false,
                0);
            var activeLegacyState = new ResearchProgressionStateRecord(
                unrelated.Identity.Id,
                unrelated.Identity.ContentVersion,
                1,
                true,
                100);
            var overMaximumState = new ResearchProgressionStateRecord(
                unrelated.Identity.Id,
                unrelated.Identity.ContentVersion,
                unrelated.MaximumLevel + 1,
                false,
                0);
            var overMaximumSnapshot = new ResearchProgressionSnapshot(
                unrelated,
                unrelated.MaximumLevel + 1,
                ProgressionStateOrigin.Saved,
                false,
                0);
            var unknownState = new ResearchProgressionStateRecord(
                "fake.research.semantic-unknown",
                Fixture.ContentVersion,
                0,
                false,
                0);
            var targetState = new ResearchProgressionStateRecord(
                target.Identity.Id,
                target.Identity.ContentVersion,
                (int)researchOrder.PreviousValue,
                false,
                0);
            var activeUnrelatedDuringTargetDrift =
                new ProgressionCompatibilityResult(
                    ProgressionDomain.Research,
                    ProgressionCompatibilityStatus.MalformedState,
                    research.CatalogSetId,
                    research.CatalogRevision,
                    research.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    Array.Empty<TroopProgressionSnapshot>(),
                    new[] { targetState, activeLegacyState },
                    Array.Empty<TroopProgressionStateRecord>(),
                    new[]
                    {
                        new ProgressionDiagnostic(
                            ProgressionDiagnosticCode
                                .UnsupportedContentVersion,
                            ProgressionDomain.Research,
                            target.Identity.Id,
                            0)
                    },
                    research.ResearchDefinitions,
                    research.TroopDefinitions,
                    research.TimestampPolicy,
                    research.HasDefinitionSource);
            var crossDomainTargetDrift =
                new ProgressionCompatibilityResult(
                    ProgressionDomain.Research,
                    ProgressionCompatibilityStatus.MalformedState,
                    research.CatalogSetId,
                    research.CatalogRevision,
                    research.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    Array.Empty<TroopProgressionSnapshot>(),
                    new[] { targetState },
                    Array.Empty<TroopProgressionStateRecord>(),
                    new[]
                    {
                        new ProgressionDiagnostic(
                            ProgressionDiagnosticCode
                                .UnsupportedContentVersion,
                            ProgressionDomain.Training,
                            target.Identity.Id,
                            0)
                    },
                    research.ResearchDefinitions,
                    research.TroopDefinitions,
                    research.TimestampPolicy,
                    research.HasDefinitionSource);
            var invalidStatusTargetDrift =
                new ProgressionCompatibilityResult(
                    ProgressionDomain.Research,
                    (ProgressionCompatibilityStatus)999,
                    research.CatalogSetId,
                    research.CatalogRevision,
                    research.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    Array.Empty<TroopProgressionSnapshot>(),
                    new[] { targetState },
                    Array.Empty<TroopProgressionStateRecord>(),
                    new[]
                    {
                        new ProgressionDiagnostic(
                            ProgressionDiagnosticCode
                                .UnsupportedContentVersion,
                            ProgressionDomain.Research,
                            target.Identity.Id,
                            0)
                    },
                    research.ResearchDefinitions,
                    research.TroopDefinitions,
                    research.TimestampPolicy,
                    research.HasDefinitionSource);
            var emptyClaimedDefinitionSource =
                new ProgressionCompatibilityResult(
                    ProgressionDomain.Research,
                    ProgressionCompatibilityStatus.MalformedState,
                    research.CatalogSetId,
                    research.CatalogRevision,
                    research.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    Array.Empty<TroopProgressionSnapshot>(),
                    new[] { targetState },
                    Array.Empty<TroopProgressionStateRecord>(),
                    new[]
                    {
                        new ProgressionDiagnostic(
                            ProgressionDiagnosticCode
                                .PreservedUnknownFutureDefinition,
                            ProgressionDomain.Research,
                            target.Identity.Id,
                            0)
                    },
                    Array.Empty<ResearchProgressionDefinition>(),
                    Array.Empty<TroopProgressionDefinition>(),
                    research.TimestampPolicy,
                    true);
            ProgressionCompatibilityResult[] forgedResearch =
            {
                forgeResearch(
                    new[] { targetSnapshot, mismatchedSnapshot },
                    new[] { mismatchedState }),
                forgeResearch(
                    new[] { targetSnapshot, mismatchedSnapshot },
                    new[] { activeLegacyState }),
                forgeResearch(
                    new[] { targetSnapshot, overMaximumSnapshot },
                    new[] { overMaximumState }),
                forgeResearch(
                    new[] { targetSnapshot, unrelatedSnapshot },
                    new[] { unknownState }),
                forgeResearch(
                    new[] { targetSnapshot },
                    Array.Empty<ResearchProgressionStateRecord>()),
                forgeResearch(
                    new[] { unrelatedSnapshot },
                    new[] { targetState }),
                activeUnrelatedDuringTargetDrift,
                crossDomainTargetDrift,
                invalidStatusTargetDrift,
                emptyClaimedDefinitionSource,
                forgeResearch(
                    new[]
                    {
                        targetSnapshot,
                        new ResearchProgressionSnapshot(
                            unrelated,
                            unrelated.InitialLevel + 1,
                            ProgressionStateOrigin
                                .EffectiveInitialUnpersisted,
                            false,
                            0)
                    },
                    Array.Empty<ResearchProgressionStateRecord>())
            };

            foreach (ProgressionCompatibilityResult forged in forgedResearch)
            {
                ProgressionCompletionPlan completion = null;
                Assert.That(
                    () => completion =
                        ProgressionOrderPlanner.PlanCompletion(
                            forged,
                            researchOrder,
                            Fixture.Completion(forged, researchOrder),
                            Fixture.NoReceipts,
                            Fixture.CompletionDependencies,
                            researchOrder.EndTimestamp),
                    Throws.Nothing);
                Assert.That(
                    completion.Status,
                    Is.EqualTo(ProgressionPlanStatus.StateMalformed));
            }

            TroopProgressionDefinition troopTarget =
                Fixture.Troop("fake.troop.semantic-target");
            TroopProgressionDefinition troopUnrelated =
                Fixture.Troop(
                    "fake.troop.semantic-unrelated",
                    maximumInventory: 10,
                    maximumBatch: 10);
            ProgressionCompatibilityResult training =
                Fixture.TrainingCompatibility(
                    new[] { troopTarget, troopUnrelated },
                    Array.Empty<TroopProgressionStateRecord>());
            ProgressionStartPlan trainingStart = Fixture.PlanTraining(
                training,
                Fixture.TrainingStart(
                    training,
                    troopTarget.Identity.Id,
                    1),
                Fixture.Economy(ResourceType.Food, 1000),
                100);
            ProgressionOrderSnapshot trainingOrder =
                Fixture.Order(trainingStart);
            TroopProgressionSnapshot troopTargetSnapshot =
                training.Troops.Single(snapshot =>
                    string.Equals(
                        snapshot.Definition.Identity.Id,
                        troopTarget.Identity.Id,
                        StringComparison.Ordinal));
            TroopProgressionSnapshot troopUnrelatedSnapshot =
                training.Troops.Single(snapshot =>
                    string.Equals(
                        snapshot.Definition.Identity.Id,
                        troopUnrelated.Identity.Id,
                        StringComparison.Ordinal));
            Func<IEnumerable<TroopProgressionSnapshot>,
                IEnumerable<TroopProgressionStateRecord>,
                ProgressionCompatibilityResult> forgeTraining =
                (snapshots, states) => new ProgressionCompatibilityResult(
                    ProgressionDomain.Training,
                    ProgressionCompatibilityStatus.Available,
                    training.CatalogSetId,
                    training.CatalogRevision,
                    training.StateRevision,
                    Array.Empty<ResearchProgressionSnapshot>(),
                    snapshots,
                    Array.Empty<ResearchProgressionStateRecord>(),
                    states,
                    Array.Empty<ProgressionDiagnostic>(),
                    training.ResearchDefinitions,
                    training.TroopDefinitions,
                    training.TimestampPolicy,
                    training.HasDefinitionSource);
            var overflowingTroopState = new TroopProgressionStateRecord(
                troopUnrelated.Identity.Id,
                troopUnrelated.Identity.ContentVersion,
                troopUnrelated.MaximumInventoryCount,
                1,
                0);
            var troopTargetState = new TroopProgressionStateRecord(
                troopTarget.Identity.Id,
                troopTarget.Identity.ContentVersion,
                trainingOrder.PreviousValue,
                0,
                0);
            ProgressionCompatibilityResult[] forgedTraining =
            {
                forgeTraining(
                    new[]
                    {
                        troopTargetSnapshot,
                        new TroopProgressionSnapshot(
                            troopUnrelated,
                            troopUnrelated.MaximumInventoryCount,
                            1,
                            0,
                            ProgressionStateOrigin.Saved)
                    },
                    new[] { overflowingTroopState }),
                forgeTraining(
                    new[] { troopUnrelatedSnapshot },
                    new[] { troopTargetState })
            };

            foreach (ProgressionCompatibilityResult forged in forgedTraining)
            {
                ProgressionCompletionPlan trainingCompletion = null;
                Assert.That(
                    () => trainingCompletion =
                        ProgressionOrderPlanner.PlanCompletion(
                            forged,
                            trainingOrder,
                            Fixture.Completion(
                                forged,
                                trainingOrder),
                            Fixture.NoReceipts,
                            Fixture.CompletionDependencies,
                            trainingOrder.EndTimestamp),
                    Throws.Nothing);
                Assert.That(
                    trainingCompletion.Status,
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
                    },
                    catalogRevision: "fake.catalog.rev.2");
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
            ProgressionOperationReceipt receipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(result);
            ProgressionCompletionPlan replay =
                ProgressionOrderPlanner.PlanCompletion(
                    null,
                    null,
                    request,
                    new[] { receipt },
                    null,
                    0);

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
            Assert.That(receipt, Is.Not.Null);
            Assert.That(
                receipt.CommittedResult.CatalogRevision,
                Is.EqualTo(current.CatalogRevision));
            Assert.That(
                receipt.CommittedResult.ProgressionRevision,
                Is.EqualTo(current.StateRevision));
            Assert.That(
                receipt.CommittedResult.EconomyRevision,
                Is.EqualTo(currentEconomyRevision));
            Assert.That(
                receipt.CommittedResult.OrderCatalogRevision,
                Is.EqualTo(order.CatalogRevision));
            Assert.That(
                receipt.CommittedResult.OrderProgressionRevision,
                Is.EqualTo(order.ProgressionRevision));
            Assert.That(
                receipt.CommittedResult.OrderEconomyRevision,
                Is.EqualTo(order.EconomyRevision));
            Assert.That(
                replay.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(
                replay.OrderSnapshot.CatalogRevision,
                Is.EqualTo(order.CatalogRevision));
            Assert.That(
                replay.OrderSnapshot.ProgressionRevision,
                Is.EqualTo(order.ProgressionRevision));
            Assert.That(
                replay.OrderSnapshot.EconomyRevision,
                Is.EqualTo(order.EconomyRevision));
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
            ProgressionOperationReceipt receipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(ready);
            ProgressionCompletionPlan replay =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    completed,
                    request,
                    new[] { receipt },
                    Fixture.CompletionDependencies,
                    completed.EndTimestamp);
            ProgressionCompletionPlan replayWithoutLiveInputs =
                ProgressionOrderPlanner.PlanCompletion(
                    null,
                    null,
                    request,
                    new[] { receipt },
                    null,
                    0);

            Assert.That(
                missingReceipt.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
            Assert.That(
                replay.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(replay.PlanHash, Is.EqualTo(ready.PlanHash));
            Assert.That(
                replayWithoutLiveInputs.Status,
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(
                replayWithoutLiveInputs.CommittedReceipt,
                Is.SameAs(receipt));
            Assert.That(
                replayWithoutLiveInputs.TargetValue,
                Is.EqualTo(ready.TargetValue));
            Assert.That(
                replayWithoutLiveInputs.QuestProgressAmount,
                Is.EqualTo(ready.QuestProgressAmount));
        }

        [Test]
        public void CommittedReceiptsAreExactIndependentlyVerifiedAndKindBound()
        {
            ResearchProgressionDefinition definition =
                Fixture.Research("fake.research.committed-result");
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
            ProgressionCompletionPlan ready =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    request,
                    Fixture.NoReceipts,
                    Fixture.CompletionDependencies,
                    order.EndTimestamp);
            ProgressionOperationReceipt receipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(ready);
            ProgressionCompletionPlan replay =
                ProgressionOrderPlanner.PlanCompletion(
                    null,
                    null,
                    request,
                    new[] { receipt },
                    null,
                    0);

            Assert.That(receipt, Is.Not.Null);
            Assert.That(
                receipt.OperationKind,
                Is.EqualTo(ProgressionOperationKind.Completion));
            Assert.That(receipt.ResultHash, Has.Length.EqualTo(64));
            Assert.That(replay.Status, Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(replay.CommittedReceipt, Is.SameAs(receipt));
            Assert.That(replay.OrderSnapshot.OrderHash, Is.EqualTo(order.OrderHash));
            Assert.That(ready.CatalogRevision, Is.EqualTo(compatibility.CatalogRevision));
            Assert.That(replay.CatalogRevision, Is.EqualTo(ready.CatalogRevision));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.DefinitionSource,
                Is.SameAs(order.DefinitionSource));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.CatalogRevision,
                Is.EqualTo(order.CatalogRevision));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.PrerequisiteRevision,
                Is.EqualTo(order.PrerequisiteRevision));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.OrderCatalogSetId,
                Is.EqualTo(order.CatalogSetId));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.OrderCatalogRevision,
                Is.EqualTo(order.CatalogRevision));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.OrderProgressionRevision,
                Is.EqualTo(order.ProgressionRevision));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.OrderEconomyRevision,
                Is.EqualTo(order.EconomyRevision));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.CommitTimestamp,
                Is.EqualTo(ready.CommitTimestamp));
            Assert.That(
                replay.CommittedReceipt.CommittedResult.QuestProgressAmount,
                Is.EqualTo(ready.QuestProgressAmount));
            Assert.That(
                () => ((IList<BuildingConstructionCost>)receipt
                    .CommittedResult.Costs).Add(
                    new BuildingConstructionCost(ResourceType.Gold, 1)),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(ready.CommittedReceipt, Is.Null);

            ProgressionCommittedOperationResult corruptedResult =
                Fixture.CopyCommittedResult(
                    receipt.CommittedResult,
                    targetValue: receipt.CommittedResult.TargetValue + 1);
            var corruptedPayload = new ProgressionOperationReceipt(
                receipt.OperationId,
                receipt.SemanticHash,
                receipt.ResultHash,
                ProgressionOperationDurability.Committed,
                corruptedResult);
            var randomHash = new ProgressionOperationReceipt(
                receipt.OperationId,
                receipt.SemanticHash,
                new string('f', 64),
                ProgressionOperationDurability.Committed,
                receipt.CommittedResult);
            foreach (ProgressionOperationReceipt invalid in
                     new[] { corruptedPayload, randomHash })
            {
                ProgressionCompletionPlan rejected =
                    ProgressionOrderPlanner.PlanCompletion(
                        null,
                        null,
                        request,
                        new[] { invalid },
                        null,
                        0);
                Assert.That(
                    rejected.Status,
                    Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
                Assert.That(rejected.CommittedReceipt, Is.Null);
            }

            ProgressionStartRequest unrelatedRequest = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                operationId: "unrelated-start-operation",
                orderId: "unrelated-start-order");
            ProgressionStartPlan unrelatedStart = Fixture.PlanResearch(
                compatibility,
                unrelatedRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOperationReceipt unrelatedReceipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(
                    unrelatedStart,
                    Fixture.Order(unrelatedStart));
            var matchingOuterUnrelatedPayload =
                new ProgressionOperationReceipt(
                    receipt.OperationId,
                    receipt.SemanticHash,
                    unrelatedReceipt.ResultHash,
                    ProgressionOperationDurability.Committed,
                    unrelatedReceipt.CommittedResult);
            ProgressionCompletionPlan unrelatedPayloadRejected =
                ProgressionOrderPlanner.PlanCompletion(
                    null,
                    null,
                    request,
                    new[] { matchingOuterUnrelatedPayload },
                    null,
                    0);
            ProgressionCompletionPlan unrelatedDoesNotReplay =
                ProgressionOrderPlanner.PlanCompletion(
                    compatibility,
                    order,
                    request,
                    new[] { unrelatedReceipt },
                    Fixture.CompletionDependencies,
                    order.EndTimestamp);
            Assert.That(
                unrelatedPayloadRejected.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
            Assert.That(
                unrelatedPayloadRejected.CommittedReceipt,
                Is.Null);
            Assert.That(
                unrelatedDoesNotReplay.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(unrelatedDoesNotReplay.CommittedReceipt, Is.Null);

            ProgressionStartRequest wrongKindRequest = Fixture.ResearchStart(
                compatibility,
                definition.Identity.Id,
                1,
                operationId: order.CompletionOperationId,
                orderId: "wrong-kind-order");
            ProgressionStartPlan wrongKindStart = Fixture.PlanResearch(
                compatibility,
                wrongKindRequest,
                Fixture.Economy(ResourceType.Gold, 1000),
                Fixture.NoPrerequisites,
                Fixture.NoReceipts,
                Fixture.NoOrders,
                100);
            ProgressionOperationReceipt wrongKindReceipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(
                    wrongKindStart,
                    Fixture.Order(wrongKindStart));
            ProgressionCompletionPlan wrongKind =
                ProgressionOrderPlanner.PlanCompletion(
                    null,
                    null,
                    request,
                    new[] { wrongKindReceipt },
                    null,
                    0);
            Assert.That(
                wrongKind.Status,
                Is.EqualTo(ProgressionPlanStatus.CorrelationConflict));
            Assert.That(wrongKind.CommittedReceipt, Is.Null);

            var uncertainWithPayload = new ProgressionOperationReceipt(
                receipt.OperationId,
                receipt.SemanticHash,
                string.Empty,
                ProgressionOperationDurability.CommitUncertain,
                receipt.CommittedResult);
            ProgressionCompletionPlan invalidUncertain =
                ProgressionOrderPlanner.PlanCompletion(
                    null,
                    null,
                    request,
                    new[] { uncertainWithPayload },
                    null,
                    0);
            Assert.That(
                invalidUncertain.Status,
                Is.EqualTo(ProgressionPlanStatus.RecoveryRequired));
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
                order.OrderHash,
                order.MaximumValue,
                order.InventoryCapacityPolicy,
                order.TimestampPolicy,
                order.PrerequisiteRevision,
                order.CatalogRevision);

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
            ProgressionOperationReceipt receipt =
                ProgressionOrderPlanner.CreateCommittedReceipt(ready);
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
                Is.EqualTo(ProgressionPlanStatus.AlreadyCommitted));
            Assert.That(
                replayAgainstTamperedOrder.CommittedReceipt,
                Is.SameAs(receipt));
            Assert.That(
                replayAgainstTamperedOrder.TargetValue,
                Is.EqualTo(ready.TargetValue));
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
                Fixture.StartPolicyVersion,
                Fixture.NoPrerequisites.Revision);
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
            ResearchProgressionDefinition sourceRevisionDrift =
                Fixture.ReissueResearch(
                    alpha,
                    sourceRevision: "fake.source.rev.2",
                    hashCharacter: 'b');
            ResearchProgressionDefinition rawHashDrift =
                Fixture.ReissueResearch(
                    alpha,
                    sourceRevision: alpha.Identity.SourceRevision,
                    hashCharacter: 'c');
            ProgressionCompatibilityResult sourceRevisionCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { beta, sourceRevisionDrift },
                    states);
            ProgressionCompatibilityResult rawHashCompatibility =
                Fixture.ResearchCompatibility(
                    new[] { beta, rawHashDrift },
                    states);

            ResearchEffectSnapshot first =
                ProgressionOrderPlanner.BuildResearchEffectSnapshot(compatibility);
            ResearchEffectSnapshot second =
                ProgressionOrderPlanner.BuildResearchEffectSnapshot(compatibility);
            ResearchEffectSnapshot sourceRevisionSnapshot =
                ProgressionOrderPlanner.BuildResearchEffectSnapshot(
                    sourceRevisionCompatibility);
            ResearchEffectSnapshot rawHashSnapshot =
                ProgressionOrderPlanner.BuildResearchEffectSnapshot(
                    rawHashCompatibility);

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
            Assert.That(
                sourceRevisionCompatibility.StateRevision,
                Is.EqualTo(compatibility.StateRevision));
            Assert.That(
                rawHashCompatibility.StateRevision,
                Is.EqualTo(compatibility.StateRevision));
            Assert.That(
                sourceRevisionSnapshot.SnapshotHash,
                Is.Not.EqualTo(first.SnapshotHash));
            Assert.That(
                rawHashSnapshot.SnapshotHash,
                Is.Not.EqualTo(first.SnapshotHash));
            ResearchEffectReference alphaEffect = first.Effects.First(effect =>
                effect.ResearchDefinitionId == alpha.Identity.Id);
            Assert.That(alphaEffect.ResearchDefinition, Is.SameAs(alpha.Identity));
            Assert.That(
                alphaEffect.ResearchDefinition.SchemaVersion,
                Is.EqualTo(alpha.Identity.SchemaVersion));
            Assert.That(
                alphaEffect.ResearchDefinition.SourceRevision,
                Is.EqualTo(alpha.Identity.SourceRevision));
            Assert.That(
                alphaEffect.ResearchDefinition.RawSha256,
                Is.EqualTo(alpha.Identity.RawSha256));
            Assert.That(states[0].Level, Is.EqualTo(1));
            Assert.That(states[1].Level, Is.EqualTo(2));
            Assert.That(
                () => ((IList<ResearchEffectReference>)first.Effects).Add(
                    first.Effects[0]),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void MaximumResearchEffectMatrixProducesBoundedCanonicalHash()
        {
            const int definitionCount =
                ProgressionCompatibilityPlanner.MaximumDefinitions;
            const int effectsPerDefinition =
                ProgressionCompatibilityPlanner.MaximumEffectsPerResearch;
            string maximumSourceRevision = new string('r', 128);
            var definitions =
                new List<ResearchProgressionDefinition>(definitionCount);
            for (int definitionIndex = 0;
                 definitionIndex < definitionCount;
                 definitionIndex++)
            {
                string definitionId =
                    $"max.research.{definitionIndex:D3}";
                var effects =
                    new List<ProgressionSourceIdentity>(
                        effectsPerDefinition);
                for (int effectIndex = 0;
                     effectIndex < effectsPerDefinition;
                     effectIndex++)
                {
                    string prefix =
                        $"max.effect.{definitionIndex:D3}.{effectIndex:D2}.";
                    string effectId = prefix +
                        new string('x', 128 - prefix.Length);
                    effects.Add(Fixture.ProfileIdentity(
                        effectId,
                        maximumSourceRevision,
                        'b'));
                }

                definitions.Add(new ResearchProgressionDefinition(
                    Fixture.Identity(
                        definitionId,
                        ProgressionCompatibilityPlanner
                            .ResearchSchemaVersion,
                        maximumSourceRevision),
                    1,
                    1,
                    new ProgressionCostProfile(
                        Fixture.ProfileIdentity(
                            $"{definitionId}.cost",
                            maximumSourceRevision),
                        new[]
                        {
                            new BuildingConstructionCost(
                                ResourceType.Gold,
                                1)
                        },
                        1),
                    new ProgressionDurationProfile(
                        Fixture.ProfileIdentity(
                            $"{definitionId}.duration",
                            maximumSourceRevision),
                        1,
                        1,
                        false),
                    Array.Empty<ProgressionPrerequisite>(),
                    effects));
            }

            ProgressionCompatibilityResult compatibility =
                Fixture.ResearchCompatibility(
                    definitions,
                    Array.Empty<ResearchProgressionStateRecord>());
            ResearchEffectSnapshot snapshot =
                ProgressionOrderPlanner.BuildResearchEffectSnapshot(
                    compatibility);

            Assert.That(
                compatibility.Status,
                Is.EqualTo(ProgressionCompatibilityStatus.Available));
            Assert.That(
                compatibility.Research.Count,
                Is.EqualTo(definitionCount));
            Assert.That(
                snapshot.Status,
                Is.EqualTo(ProgressionPlanStatus.Ready));
            Assert.That(
                snapshot.Effects.Count,
                Is.EqualTo(definitionCount * effectsPerDefinition));
            Assert.That(snapshot.SnapshotHash, Does.Match("^[0-9a-f]{64}$"));
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
                4102444800,
                4102444799,
                4102444799);
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

        internal static ResearchProgressionDefinition ReissueResearch(
            ResearchProgressionDefinition source,
            string schemaVersion = null,
            string contentVersion = null,
            string sourceRevision = "fake.source.rev.2",
            char hashCharacter = 'b',
            bool reissueProfiles = false,
            string costSchemaVersion = null,
            string durationSchemaVersion = null)
        {
            ProgressionCostProfile cost = source.CostProfile;
            ProgressionDurationProfile duration = source.DurationProfile;
            if (reissueProfiles || costSchemaVersion != null)
            {
                cost = new ProgressionCostProfile(
                    new ProgressionSourceIdentity(
                        source.CostProfile.Identity.Id,
                        costSchemaVersion ??
                        source.CostProfile.Identity.SchemaVersion,
                        source.CostProfile.Identity.ContentVersion,
                        sourceRevision,
                        new string((char)(hashCharacter + 1), 64)),
                    source.CostProfile.UnitCosts,
                    source.CostProfile.MaximumAmountPerResource);
            }

            if (reissueProfiles || durationSchemaVersion != null)
            {
                duration = new ProgressionDurationProfile(
                    new ProgressionSourceIdentity(
                        source.DurationProfile.Identity.Id,
                        durationSchemaVersion ??
                        source.DurationProfile.Identity.SchemaVersion,
                        source.DurationProfile.Identity.ContentVersion,
                        sourceRevision,
                        new string((char)(hashCharacter + 2), 64)),
                    source.DurationProfile.UnitSeconds,
                    source.DurationProfile.MaximumSeconds,
                    source.DurationProfile.AllowsZeroDuration);
            }

            return new ResearchProgressionDefinition(
                new ProgressionSourceIdentity(
                    source.Identity.Id,
                    schemaVersion ?? source.Identity.SchemaVersion,
                    contentVersion ?? source.Identity.ContentVersion,
                    sourceRevision,
                    new string(hashCharacter, 64)),
                source.InitialLevel,
                source.MaximumLevel,
                cost,
                duration,
                source.Prerequisites,
                source.EffectProfiles);
        }

        internal static TroopProgressionDefinition ReissueTroop(
            TroopProgressionDefinition source,
            string schemaVersion = null,
            string contentVersion = null,
            string sourceRevision = "fake.source.rev.2",
            char hashCharacter = 'b',
            bool reissueProfiles = false,
            string costSchemaVersion = null,
            string durationSchemaVersion = null)
        {
            ProgressionCostProfile cost = source.CostProfile;
            ProgressionDurationProfile duration = source.DurationProfile;
            if (reissueProfiles || costSchemaVersion != null)
            {
                cost = new ProgressionCostProfile(
                    new ProgressionSourceIdentity(
                        source.CostProfile.Identity.Id,
                        costSchemaVersion ??
                        source.CostProfile.Identity.SchemaVersion,
                        source.CostProfile.Identity.ContentVersion,
                        sourceRevision,
                        new string((char)(hashCharacter + 1), 64)),
                    source.CostProfile.UnitCosts,
                    source.CostProfile.MaximumAmountPerResource);
            }

            if (reissueProfiles || durationSchemaVersion != null)
            {
                duration = new ProgressionDurationProfile(
                    new ProgressionSourceIdentity(
                        source.DurationProfile.Identity.Id,
                        durationSchemaVersion ??
                        source.DurationProfile.Identity.SchemaVersion,
                        source.DurationProfile.Identity.ContentVersion,
                        sourceRevision,
                        new string((char)(hashCharacter + 2), 64)),
                    source.DurationProfile.UnitSeconds,
                    source.DurationProfile.MaximumSeconds,
                    source.DurationProfile.AllowsZeroDuration);
            }

            return new TroopProgressionDefinition(
                new ProgressionSourceIdentity(
                    source.Identity.Id,
                    schemaVersion ?? source.Identity.SchemaVersion,
                    contentVersion ?? source.Identity.ContentVersion,
                    sourceRevision,
                    new string(hashCharacter, 64)),
                source.MaximumInventoryCount,
                source.MaximumBatchCount,
                cost,
                duration,
                source.Prerequisites,
                source.BattleProfile,
                source.InventoryPolicy,
                source.InventoryCapacityPolicy);
        }

        internal static ProgressionSourceIdentity ReissueIdentity(
            ProgressionSourceIdentity source,
            string sourceRevision,
            char hashCharacter)
        {
            return new ProgressionSourceIdentity(
                source.Id,
                source.SchemaVersion,
                source.ContentVersion,
                sourceRevision,
                new string(hashCharacter, 64));
        }

        internal static ProgressionCommittedOperationResult CopyCommittedResult(
            ProgressionCommittedOperationResult source,
            ProgressionOperationKind? operationKind = null,
            string orderId = null,
            long? targetValue = null,
            long? questProgressAmount = null)
        {
            return new ProgressionCommittedOperationResult(
                operationKind ?? source.OperationKind,
                source.OrderType,
                source.ProfileId,
                source.DefinitionSource,
                source.CostProfile,
                source.DurationProfile,
                orderId ?? source.OrderId,
                source.OperationId,
                source.StartOperationId,
                source.CompletionOperationId,
                source.CancellationOperationId,
                source.PreviousValue,
                targetValue ?? source.TargetValue,
                source.BatchCount,
                source.MaximumValue,
                questProgressAmount ?? source.QuestProgressAmount,
                source.InventoryCapacityPolicy,
                source.Costs,
                source.StartTimestamp,
                source.EndTimestamp,
                source.CommitTimestamp,
                source.TimestampPolicy,
                source.CatalogSetId,
                source.CatalogRevision,
                source.ProgressionRevision,
                source.EconomyRevision,
                source.PrerequisiteRevision,
                source.QuestRevision,
                source.OperationPolicyVersion,
                source.OrderPolicyVersion,
                source.OrderCatalogSetId,
                source.OrderCatalogRevision,
                source.OrderProgressionRevision,
                source.OrderEconomyRevision,
                source.OrderHash,
                source.SourceDisposition,
                source.SemanticHash,
                source.PlanHash);
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
            ProgressionTimestampPolicy timestampPolicy = null,
            string catalogRevision = null)
        {
            return ProgressionCompatibilityPlanner.BuildResearchCompatibility(
                CatalogSetId,
                catalogRevision ?? CatalogRevision,
                definitions,
                states,
                prerequisiteTargets,
                timestampPolicy ?? TimestampPolicy);
        }

        internal static ProgressionCompatibilityResult TrainingCompatibility(
            IEnumerable<TroopProgressionDefinition> definitions,
            IEnumerable<TroopProgressionStateRecord> states,
            IEnumerable<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets = null,
            ProgressionTimestampPolicy timestampPolicy = null,
            string catalogRevision = null)
        {
            return ProgressionCompatibilityPlanner.BuildTrainingCompatibility(
                CatalogSetId,
                catalogRevision ?? CatalogRevision,
                definitions,
                states,
                prerequisiteTargets,
                timestampPolicy ?? TimestampPolicy);
        }

        internal static ProgressionCompatibilityResult TrustedPlannerResult(
            ProgressionDomain domain,
            string stateRevision,
            IEnumerable<ResearchProgressionSnapshot> research,
            IEnumerable<TroopProgressionSnapshot> troops,
            IEnumerable<ResearchProgressionStateRecord> researchStates,
            IEnumerable<TroopProgressionStateRecord> troopStates,
            IEnumerable<ResearchProgressionDefinition> researchDefinitions,
            IEnumerable<TroopProgressionDefinition> troopDefinitions)
        {
            MethodInfo factory =
                typeof(ProgressionCompatibilityResult).GetMethod(
                    "CreatePlannerResult",
                    BindingFlags.Static | BindingFlags.NonPublic);
            if (factory == null)
            {
                throw new InvalidOperationException(
                    "Planner compatibility factory unavailable.");
            }

            return (ProgressionCompatibilityResult)factory.Invoke(
                null,
                new object[]
                {
                    domain,
                    ProgressionCompatibilityStatus.Available,
                    CatalogSetId,
                    CatalogRevision,
                    stateRevision,
                    research,
                    troops,
                    researchStates,
                    troopStates,
                    Array.Empty<ProgressionDiagnostic>(),
                    researchDefinitions,
                    troopDefinitions,
                    TimestampPolicy,
                    true
                });
        }

        internal static ProgressionStartRequest ResearchStart(
            ProgressionCompatibilityResult compatibility,
            string definitionId,
            int targetLevel,
            string operationId = "research-start-1",
            string expectedRevision = null,
            string orderId = "research-order-1",
            string expectedPrerequisiteRevision = null)
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
                StartPolicyVersion,
                expectedPrerequisiteRevision ?? NoPrerequisites.Revision);
        }

        internal static ProgressionStartRequest TrainingStart(
            ProgressionCompatibilityResult compatibility,
            string definitionId,
            long batchCount,
            string operationId = "training-start-1",
            string expectedRevision = null,
            string orderId = "training-order-1",
            string expectedPrerequisiteRevision = null)
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
                StartPolicyVersion,
                expectedPrerequisiteRevision ?? NoPrerequisites.Revision);
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
                order.OrderHash,
                order.MaximumValue,
                order.InventoryCapacityPolicy,
                order.TimestampPolicy,
                order.PrerequisiteRevision,
                order.CatalogRevision);
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

        internal static ProgressionCompletionPlan PlanCompletion(
            ProgressionCompatibilityResult compatibility,
            ProgressionOrderSnapshot order,
            long timestamp,
            IEnumerable<ProgressionOperationReceipt> receipts = null,
            ProgressionCompletionDependencySnapshot dependencies = null)
        {
            return ProgressionOrderPlanner.PlanCompletion(
                compatibility,
                order,
                Completion(compatibility, order),
                receipts ?? NoReceipts,
                dependencies ?? CompletionDependencies,
                timestamp);
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
