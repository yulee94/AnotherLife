using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.RealmWar.Territories.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Territories
{
    public sealed class TerritoryPhaseBPlannerTests
    {
        [Test]
        public void BaselineCatalogIsStrictBoundedHashedAndImmutable()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();

            TerritoryCatalogValidationResult result = planner.ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Valid, result.Status);
            AssertLowerHash(result.CatalogSemanticHash);
            Assert.AreEqual(result.CatalogSemanticHash, planner.Catalog.Identity.RawSha256);
            Assert.AreEqual(5, planner.Catalog.Definitions.Count);
            Assert.AreEqual(1, planner.Catalog.RewardProfiles.Count);
            TerritoryCaptureRewardProfile reward = planner.Catalog.RewardProfiles.Single();
            Assert.AreEqual(TerritoryContractPlanner.CurrentRewardProfileId, reward.RewardProfileId);
            Assert.AreEqual(100, reward.WarzoneCredits);
            Assert.AreEqual(1, reward.QuestProgressDelta);
            Assert.Throws<NotSupportedException>(() => ((IList)planner.Catalog.Definitions).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList)result.Diagnostics).Clear());
        }

        [Test]
        public void CatalogRejectsMaxPlusOneDefinitionsWithCappedDiagnostics()
        {
            TerritoryCaptureRewardProfile reward = CurrentReward();
            TerritoryDefinition[] definitions = Enumerable.Range(0, TerritoryTechnicalLimits.MaximumDefinitions + 1)
                .Select(index => Definition("X" + index.ToString("D3"), "territory.test_" + index.ToString("D3")))
                .ToArray();
            TerritoryPhaseBCatalog catalog = Catalog(definitions, new[] { reward }, Array.Empty<TerritoryAliasDefinition>());

            TerritoryCatalogValidationResult result = new TerritoryPhaseBPlanner(catalog).ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, result.Status);
            AssertDiagnostic(result.Diagnostics, "DefinitionLimitExceeded");
            Assert.LessOrEqual(result.Diagnostics.Count, TerritoryTechnicalLimits.MaximumDiagnostics);
            Assert.True(result.Diagnostics.Any(item => item.Code == "DiagnosticLimitExceeded") || result.Diagnostics.Count < TerritoryTechnicalLimits.MaximumDiagnostics);
        }

        [Test]
        public void NullDuplicateAndPermutationCatalogDiagnosticsAreCanonical()
        {
            TerritoryDefinition first = Definition("T1", "territory.one");
            TerritoryDefinition duplicate = Definition("T1", "territory.one_duplicate");
            TerritoryDefinition second = Definition("T2", "territory.two");
            TerritoryDefinition[] forward = { second, null, duplicate, first };
            TerritoryDefinition[] reverse = forward.Reverse().ToArray();

            TerritoryCatalogValidationResult left = new TerritoryPhaseBPlanner(Catalog(forward, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>())).ValidateCatalog();
            TerritoryCatalogValidationResult right = new TerritoryPhaseBPlanner(Catalog(reverse, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>())).ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, left.Status);
            AssertDiagnostic(left.Diagnostics, "NullDefinition");
            AssertDiagnostic(left.Diagnostics, "DuplicateDefinitionId", "T1");
            CollectionAssert.AreEqual(DiagnosticKeys(left.Diagnostics), DiagnosticKeys(right.Diagnostics));
            Assert.AreEqual(left.CatalogSemanticHash, right.CatalogSemanticHash);
        }

        [Test]
        public void PolicyBearingDefinitionsFailClosedUntilTrustedEligibilityAuthorityExists()
        {
            var prerequisite = new TerritoryDefinition(
                "T1",
                "territory.one",
                RealmId.Stonehold,
                ResourceType.Stone,
                1,
                false,
                PlayableRealms,
                TerritoryContractPlanner.CurrentRewardProfileId,
                false,
                new[] { "T2" },
                Array.Empty<string>());
            var capability = new TerritoryDefinition(
                "T2",
                "territory.two",
                RealmId.Stonehold,
                ResourceType.Stone,
                1,
                false,
                PlayableRealms,
                TerritoryContractPlanner.CurrentRewardProfileId,
                false,
                Array.Empty<string>(),
                new[] { "capture.capability" });

            TerritoryCatalogValidationResult result = new TerritoryPhaseBPlanner(
                Catalog(
                    new[] { prerequisite, capability },
                    new[] { CurrentReward() },
                    Array.Empty<TerritoryAliasDefinition>())).ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, result.Status);
            AssertDiagnostic(result.Diagnostics, "PrerequisiteAuthorityUnavailable", "T1");
            AssertDiagnostic(result.Diagnostics, "CapabilityAuthorityUnavailable", "T2");
        }

        [Test]
        public void DiagnosticCandidateOverflowIsOrderIndependentAndFailsClosed()
        {
            string[] missing = Enumerable.Range(0, TerritoryTechnicalLimits.MaximumReferenceIds + 1)
                .Select(index => "missing-" + index.ToString("D3"))
                .ToArray();
            TerritoryDefinition[] forward = Enumerable.Range(0, TerritoryTechnicalLimits.MaximumDefinitions + 1)
                .Select(index => new TerritoryDefinition(
                    "D" + index.ToString("D3"),
                    "territory.d" + index.ToString("D3"),
                    RealmId.Stonehold,
                    ResourceType.Stone,
                    1,
                    false,
                    PlayableRealms,
                    TerritoryContractPlanner.CurrentRewardProfileId,
                    false,
                    missing,
                    Array.Empty<string>()))
                .ToArray();

            TerritoryCatalogValidationResult left = new TerritoryPhaseBPlanner(
                Catalog(forward, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>())).ValidateCatalog();
            TerritoryCatalogValidationResult right = new TerritoryPhaseBPlanner(
                Catalog(forward.Reverse(), new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>())).ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, left.Status);
            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, right.Status);
            CollectionAssert.AreEqual(DiagnosticKeys(left.Diagnostics), DiagnosticKeys(right.Diagnostics));
            Assert.AreEqual(1, left.Diagnostics.Count);
            AssertDiagnostic(left.Diagnostics, "DiagnosticLimitExceeded");
            Assert.AreEqual(TerritoryDiagnosticSeverity.Error, left.Diagnostics.Single().Severity);
        }

        [Test]
        public void CatalogRejectsNullDuplicateMissingAndUnsafeRewardProfiles()
        {
            TerritoryDefinition definition = Definition("T1", "territory.one");
            TerritoryCaptureRewardProfile current = CurrentReward();
            TerritoryCaptureRewardProfile duplicate = CurrentReward();
            TerritoryCaptureRewardProfile unsafeReward = new TerritoryCaptureRewardProfile(
                "unsafe",
                -1,
                "CaptureTerritory",
                -1);
            TerritoryDefinition missingReward = new TerritoryDefinition(
                "T2",
                "territory.two",
                RealmId.Eldergrove,
                ResourceType.Wood,
                40,
                false,
                PlayableRealms,
                "missing",
                false,
                Array.Empty<string>(),
                Array.Empty<string>());
            TerritoryPhaseBCatalog catalog = Catalog(
                new[] { definition, missingReward },
                new[] { current, null, duplicate, unsafeReward },
                Array.Empty<TerritoryAliasDefinition>());

            TerritoryCatalogValidationResult result = new TerritoryPhaseBPlanner(catalog).ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, result.Status);
            AssertDiagnostic(result.Diagnostics, "NullRewardProfile");
            AssertDiagnostic(result.Diagnostics, "DuplicateRewardProfileId", TerritoryContractPlanner.CurrentRewardProfileId);
            AssertDiagnostic(result.Diagnostics, "RewardCreditsOutOfRange", "unsafe");
            AssertDiagnostic(result.Diagnostics, "RewardQuestProgressOutOfRange", "unsafe");
            AssertDiagnostic(result.Diagnostics, "MissingRewardProfile", "T2");
        }

        [Test]
        public void CatalogRejectsBonusThatCannotBeSafelyAccumulatedAtMaximumCardinality()
        {
            TerritoryDefinition unsafeDefinition = new TerritoryDefinition(
                "T1",
                "territory.one",
                RealmId.Stonehold,
                ResourceType.Stone,
                long.MaxValue,
                true,
                PlayableRealms,
                TerritoryContractPlanner.CurrentRewardProfileId,
                false,
                Array.Empty<string>(),
                Array.Empty<string>());

            TerritoryCatalogValidationResult result = new TerritoryPhaseBPlanner(
                Catalog(new[] { unsafeDefinition }, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>()))
                .ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, result.Status);
            AssertDiagnostic(result.Diagnostics, "BonusAmountOutOfRange", "T1");
        }

        [Test]
        public void CatalogRejectsEmptyInventoryDisallowedInitialOwnerAndMissingPrerequisite()
        {
            TerritoryCatalogValidationResult empty = new TerritoryPhaseBPlanner(
                Catalog(
                    Array.Empty<TerritoryDefinition>(),
                    new[] { CurrentReward() },
                    Array.Empty<TerritoryAliasDefinition>()))
                .ValidateCatalog();
            var malformed = new TerritoryDefinition(
                "T1",
                "territory.one",
                RealmId.Stonehold,
                ResourceType.Stone,
                1,
                false,
                new[] { RealmId.Crownlands },
                TerritoryContractPlanner.CurrentRewardProfileId,
                false,
                new[] { "T404" },
                Array.Empty<string>());
            TerritoryCatalogValidationResult invalid = new TerritoryPhaseBPlanner(
                Catalog(
                    new[] { malformed },
                    new[] { CurrentReward() },
                    Array.Empty<TerritoryAliasDefinition>()))
                .ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, empty.Status);
            AssertDiagnostic(empty.Diagnostics, "EmptyDefinitionCatalog");
            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, invalid.Status);
            AssertDiagnostic(invalid.Diagnostics, "InitialOwnerNotAllowed", "T1");
            AssertDiagnostic(invalid.Diagnostics, "MissingPrerequisite", "T1");
        }

        [Test]
        public void CatalogHashIsPermutationStableForDuplicateTieRows()
        {
            TerritoryDefinition first = Definition("T1", "territory.same");
            var second = new TerritoryDefinition(
                "T1",
                "territory.same",
                RealmId.Eldergrove,
                ResourceType.Wood,
                40,
                true,
                PlayableRealms,
                TerritoryContractPlanner.CurrentRewardProfileId,
                false,
                Array.Empty<string>(),
                Array.Empty<string>());
            TerritoryCatalogValidationResult forward = new TerritoryPhaseBPlanner(
                Catalog(new[] { first, second }, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>()))
                .ValidateCatalog();
            TerritoryCatalogValidationResult reverse = new TerritoryPhaseBPlanner(
                Catalog(new[] { second, first }, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>()))
                .ValidateCatalog();

            Assert.AreEqual(TerritoryCatalogValidationStatus.Invalid, forward.Status);
            Assert.AreEqual(forward.CatalogSemanticHash, reverse.CatalogSemanticHash);
            CollectionAssert.AreEqual(DiagnosticKeys(forward.Diagnostics), DiagnosticKeys(reverse.Diagnostics));
        }

        [Test]
        public void DiagnosticFreezeStopsHostileEnumerationAndEmitsCanonicalLimitSentinel()
        {
            var hostile = new CountingDiagnosticsEnumerable();

            var result = new TerritoryCatalogValidationResult(
                TerritoryCatalogValidationStatus.Invalid,
                string.Empty,
                hostile);

            Assert.LessOrEqual(
                hostile.MoveNextCalls,
                TerritoryTechnicalLimits.MaximumDiagnosticCandidates + 2);
            AssertDiagnostic(result.Diagnostics, "DiagnosticLimitExceeded");
            CollectionAssert.AreEqual(
                result.Diagnostics.Select(item => item.Code).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                result.Diagnostics.Select(item => item.Code).ToArray());
        }

        [Test]
        public void DiagnosticFreezeUsesStructuralIdentityWhenFieldsContainDelimiter()
        {
            const string delimiter = "\u001f";
            var codeDelimited = new TerritoryDiagnostic(
                TerritoryDiagnosticSeverity.Error,
                "alpha" + delimiter + "beta",
                "gamma",
                "delta");
            var territoryDelimited = new TerritoryDiagnostic(
                TerritoryDiagnosticSeverity.Error,
                "alpha",
                "beta" + delimiter + "gamma",
                "delta");

            var result = new TerritoryCatalogValidationResult(
                TerritoryCatalogValidationStatus.Invalid,
                string.Empty,
                new[] { codeDelimited, territoryDelimited });

            Assert.AreEqual(2, result.Diagnostics.Count);
            Assert.AreEqual("alpha", result.Diagnostics[0].Code);
            Assert.AreEqual("beta" + delimiter + "gamma", result.Diagnostics[0].TerritoryId);
            Assert.AreEqual("alpha" + delimiter + "beta", result.Diagnostics[1].Code);
            Assert.AreEqual("gamma", result.Diagnostics[1].TerritoryId);
        }

        [Test]
        public void CanonicalHashUsesLengthFramingAndLowercaseSha256()
        {
            string left = TerritorySemanticHasher.HashFrames("ab", "c", "x|y");
            string right = TerritorySemanticHasher.HashFrames("a", "bc", "x", "y");

            AssertLowerHash(left);
            AssertLowerHash(right);
            Assert.AreNotEqual(left, right);
            Assert.AreEqual(left, TerritorySemanticHasher.HashFrames("ab", "c", "x|y"));

            string oversizedLeft = TerritorySemanticHasher.HashFrames(
                new string('x', TerritoryTechnicalLimits.MaximumHashFrameUtf8Bytes + 1));
            string oversizedRight = TerritorySemanticHasher.HashFrames(
                new string('y', TerritoryTechnicalLimits.MaximumHashFrameUtf8Bytes + 1));
            AssertLowerHash(oversizedLeft);
            AssertLowerHash(oversizedRight);
            Assert.AreNotEqual(oversizedLeft, oversizedRight);

            string maximumFrame = new string('m', TerritoryTechnicalLimits.MaximumHashFrameUtf8Bytes);
            string[] maximumFrames = Enumerable
                .Repeat(maximumFrame, TerritoryTechnicalLimits.MaximumHashFrames)
                .ToArray();
            string maximumFirst = TerritorySemanticHasher.HashFrames(maximumFrames);
            string maximumSecond = TerritorySemanticHasher.HashFrames(maximumFrames);

            AssertLowerHash(maximumFirst);
            Assert.AreEqual("435ddde6f03fdf4bfa7f4bf0a7433284592f16fdc831bd10f4cf7a40356c6543", maximumFirst);
            Assert.AreEqual(maximumFirst, maximumSecond);
        }

        [Test]
        public void ExistingCaptureStatusOrdinalsRemainStable()
        {
            Assert.AreEqual(0, (int)TerritoryCaptureStatus.Planned);
            Assert.AreEqual(1, (int)TerritoryCaptureStatus.NoChangeSameOwner);
            Assert.AreEqual(2, (int)TerritoryCaptureStatus.RejectedBlankId);
            Assert.AreEqual(3, (int)TerritoryCaptureStatus.RejectedUnknownTerritory);
            Assert.AreEqual(4, (int)TerritoryCaptureStatus.RejectedDomainMalformed);
            Assert.AreEqual(5, (int)TerritoryCaptureStatus.RejectedNoCommittedRealm);
            Assert.AreEqual(6, (int)TerritoryCaptureStatus.RejectedInvalidCapturer);
            Assert.AreEqual(7, (int)TerritoryCaptureStatus.RejectedUnauthorized);
            Assert.AreEqual(8, (int)TerritoryCaptureStatus.RejectedStaleOwner);
            Assert.AreEqual(9, (int)TerritoryCaptureStatus.RejectedStaleRevision);
            Assert.AreEqual(10, (int)TerritoryCaptureStatus.RejectedOverflow);
            Assert.Greater((int)TerritoryCaptureStatus.AlreadyCommittedReplay, 10);
        }

        [Test]
        public void PublicDefinitionAndIncomeEnumerablesAreBounded()
        {
            var definitions = new CountingDefinitionsEnumerable();
            var planner = new TerritoryContractPlanner(definitions);
            TerritoryQueryResult query = planner.BuildQuery(
                TerritoryContractPlanner.CreateCurrentBaselineStates(),
                RealmId.Stonehold);
            var contributions = new CountingIncomeEnumerable();
            var income = new TerritoryIncomeSnapshot(
                TerritoryIncomeStatus.Available,
                new string('a', 64),
                contributions,
                Array.Empty<TerritoryDiagnostic>());

            Assert.LessOrEqual(definitions.MoveNextCalls, TerritoryTechnicalLimits.MaximumDefinitions + 3);
            Assert.AreEqual(TerritoryQueryStatus.Unavailable, query.Status);
            AssertDiagnostic(query.Diagnostics, "DefinitionLimitExceeded");
            Assert.LessOrEqual(contributions.MoveNextCalls, TerritoryTechnicalLimits.MaximumStateRows + 3);
            Assert.AreEqual(TerritoryIncomeStatus.Unavailable, income.Status);
            Assert.IsEmpty(income.Contributions);
            AssertDiagnostic(income.Diagnostics, "IncomeContributionLimitExceeded");
        }

        [Test]
        public void NewProfileInitializationPlansExactBaselineOnce()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] raw = Array.Empty<TerritoryStateRecord>();
            string stateHash = TerritorySemanticHasher.HashStates(raw);
            var request = new TerritoryInitializationRequest(
                "territory-init-1",
                TerritoryInitializationMode.NewProfile,
                planner.Catalog.Identity,
                stateHash,
                false,
                true);

            TerritoryMigrationPlan plan = planner.PlanInitialization(request, raw, Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Planned, plan.Status);
            Assert.AreEqual(5, plan.OutputStates.Count);
            Assert.AreEqual(5, plan.Actions.Count);
            CollectionAssert.AreEqual(new[] { "T1", "T2", "T3", "T4", "T5" }, plan.OutputStates.Select(item => item.Id).ToArray());
            Assert.IsEmpty(plan.PreservedUnknownStates);
            AssertLowerHash(plan.SemanticHash);
            Assert.Throws<NotSupportedException>(() => ((IList)plan.OutputStates).Clear());
        }

        [Test]
        public void LegacyMigrationPreservesUnknownFutureRowsAndIsPermutationStable()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord unknown = new TerritoryStateRecord("T99", (RealmId)999, -5);
            TerritoryStateRecord[] forward = TerritoryContractPlanner.CreateCurrentBaselineStates().Concat(new[] { unknown }).ToArray();
            TerritoryStateRecord[] reverse = forward.Reverse().ToArray();
            var firstRequest = new TerritoryInitializationRequest(
                "territory-migrate-1",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(forward),
                false,
                false);
            var secondRequest = new TerritoryInitializationRequest(
                "territory-migrate-1",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(reverse),
                false,
                false);

            TerritoryMigrationPlan first = planner.PlanInitialization(firstRequest, forward, Array.Empty<TerritoryOperationReceipt>());
            TerritoryMigrationPlan second = planner.PlanInitialization(secondRequest, reverse, Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Planned, first.Status);
            Assert.AreEqual(first.SemanticHash, second.SemanticHash);
            Assert.AreEqual(6, first.OutputStates.Count);
            TerritoryStateRecord preserved = first.PreservedUnknownStates.Single();
            Assert.AreEqual("T99", preserved.Id);
            Assert.AreEqual((RealmId)999, preserved.Owner);
            Assert.AreEqual(-5, preserved.Revision);
            Assert.AreSame(unknown, forward.Last());
        }

        [Test]
        public void LegacyAliasMigrationNormalizesTargetAndPreservesSourceEvidence()
        {
            TerritoryDefinition target = Definition("T_NEW", "territory.new");
            var alias = new TerritoryAliasDefinition("T_OLD", "T_NEW", 1);
            TerritoryPhaseBPlanner planner = new TerritoryPhaseBPlanner(
                Catalog(new[] { target }, new[] { CurrentReward() }, new[] { alias }));
            var oldState = new TerritoryStateRecord("T_OLD", RealmId.Stonehold, 7);
            var request = new TerritoryInitializationRequest(
                "alias-migration",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(new[] { oldState }),
                false,
                false);

            TerritoryMigrationPlan plan = planner.PlanInitialization(
                request,
                new[] { oldState },
                Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Planned, plan.Status);
            TerritoryStateRecord normalized = plan.OutputStates.Single();
            Assert.AreEqual("T_NEW", normalized.Id);
            Assert.AreEqual(RealmId.Stonehold, normalized.Owner);
            Assert.AreEqual(7, normalized.Revision);
            Assert.AreSame(oldState, plan.PreservedUnknownStates.Single());
            Assert.AreEqual(TerritoryMigrationActionKind.MigrateAlias, plan.Actions.Single().Kind);
        }

        [Test]
        public void AliasAndActiveTargetCollisionRejectsWithoutSelectingEitherRow()
        {
            TerritoryDefinition target = Definition("T_NEW", "territory.new");
            var alias = new TerritoryAliasDefinition("T_OLD", "T_NEW", 1);
            TerritoryPhaseBPlanner planner = new TerritoryPhaseBPlanner(
                Catalog(new[] { target }, new[] { CurrentReward() }, new[] { alias }));
            TerritoryStateRecord[] raw =
            {
                new TerritoryStateRecord("T_OLD", RealmId.Stonehold, 7),
                new TerritoryStateRecord("T_NEW", RealmId.Stonehold, 8)
            };
            var request = new TerritoryInitializationRequest(
                "alias-collision",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(raw),
                false,
                false);

            TerritoryMigrationPlan plan = planner.PlanInitialization(
                request,
                raw,
                Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Rejected, plan.Status);
            AssertDiagnostic(plan.Diagnostics, "AliasedStateCollision", "T_NEW");
            Assert.IsEmpty(plan.Actions);
            Assert.IsEmpty(plan.OutputStates);
        }

        [Test]
        public void RejectedMigrationCarriesNoPartialActionableOutput()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] raw =
            {
                new TerritoryStateRecord("T1", RealmId.Stonehold, 0),
                new TerritoryStateRecord("T2", (RealmId)999, 0)
            };
            var request = new TerritoryInitializationRequest(
                "partial-rejection",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(raw),
                false,
                true);

            TerritoryMigrationPlan plan = planner.PlanInitialization(
                request,
                raw,
                Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Rejected, plan.Status);
            Assert.IsEmpty(plan.Actions);
            Assert.IsEmpty(plan.OutputStates);
            Assert.IsEmpty(plan.PreservedUnknownStates);
        }

        [Test]
        public void LegacyAmbiguousEmptyPrefersRicherCandidateAndRequiresExplicitInitialization()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            string hash = TerritorySemanticHasher.HashStates(Array.Empty<TerritoryStateRecord>());

            TerritoryMigrationPlan richer = planner.PlanInitialization(
                new TerritoryInitializationRequest("legacy-empty-1", TerritoryInitializationMode.Legacy, planner.Catalog.Identity, hash, true, true),
                Array.Empty<TerritoryStateRecord>(),
                Array.Empty<TerritoryOperationReceipt>());
            TerritoryMigrationPlan unauthorized = planner.PlanInitialization(
                new TerritoryInitializationRequest("legacy-empty-2", TerritoryInitializationMode.Legacy, planner.Catalog.Identity, hash, false, false),
                Array.Empty<TerritoryStateRecord>(),
                Array.Empty<TerritoryOperationReceipt>());
            TerritoryMigrationPlan authorized = planner.PlanInitialization(
                new TerritoryInitializationRequest("legacy-empty-3", TerritoryInitializationMode.Legacy, planner.Catalog.Identity, hash, false, true),
                Array.Empty<TerritoryStateRecord>(),
                Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.RequiresRicherCandidate, richer.Status);
            Assert.AreEqual(TerritoryMigrationStatus.Rejected, unauthorized.Status);
            AssertDiagnostic(unauthorized.Diagnostics, "InitializationNotAuthorized");
            Assert.AreEqual(TerritoryMigrationStatus.Planned, authorized.Status);
        }

        [Test]
        public void InitializationReplayConflictAndUncertaintyAreTypedAndNonMutating()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] raw = Array.Empty<TerritoryStateRecord>();
            string stateHash = TerritorySemanticHasher.HashStates(raw);
            var request = new TerritoryInitializationRequest("init-replay", TerritoryInitializationMode.NewProfile, planner.Catalog.Identity, stateHash, false, true);
            TerritoryMigrationPlan planned = planner.PlanInitialization(request, raw, Array.Empty<TerritoryOperationReceipt>());
            var committed = new TerritoryOperationReceipt(request.OperationId, planned.SemanticHash, TerritoryOperationDurability.Committed, planned.ResultId);
            var uncertain = new TerritoryOperationReceipt(request.OperationId, planned.SemanticHash, TerritoryOperationDurability.CommitUncertain, planned.ResultId);

            TerritoryMigrationPlan replay = planner.PlanInitialization(request, raw, new[] { committed });
            TerritoryMigrationPlan collision = planner.PlanInitialization(
                new TerritoryInitializationRequest(request.OperationId, TerritoryInitializationMode.Legacy, planner.Catalog.Identity, stateHash, false, true),
                raw,
                new[] { committed });
            TerritoryMigrationPlan unresolved = planner.PlanInitialization(request, raw, new[] { uncertain });

            Assert.AreEqual(TerritoryMigrationStatus.AlreadyCommittedReplay, replay.Status);
            Assert.AreSame(committed, replay.ExistingReceipt);
            Assert.AreEqual(TerritoryMigrationStatus.CorrelationConflict, collision.Status);
            Assert.AreEqual(TerritoryMigrationStatus.CommitUncertain, unresolved.Status);
        }

        [Test]
        public void RicherBackupPolicyCannotBypassExistingOperationCorrelation()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] raw = Array.Empty<TerritoryStateRecord>();
            string stateHash = TerritorySemanticHasher.HashStates(raw);
            var original = new TerritoryInitializationRequest(
                "legacy-richer-correlation",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                stateHash,
                false,
                true);
            TerritoryMigrationPlan planned = planner.PlanInitialization(
                original,
                raw,
                Array.Empty<TerritoryOperationReceipt>());
            var receipt = new TerritoryOperationReceipt(
                original.OperationId,
                planned.SemanticHash,
                TerritoryOperationDurability.Committed,
                planned.ResultId);
            var changed = new TerritoryInitializationRequest(
                original.OperationId,
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                stateHash,
                true,
                true);

            TerritoryMigrationPlan conflict = planner.PlanInitialization(
                changed,
                raw,
                new[] { receipt });
            TerritoryMigrationPlan richer = planner.PlanInitialization(
                changed,
                raw,
                Array.Empty<TerritoryOperationReceipt>());
            var richerReceipt = new TerritoryOperationReceipt(
                changed.OperationId,
                richer.SemanticHash,
                TerritoryOperationDurability.Committed,
                richer.ResultId);
            TerritoryMigrationPlan exactReplay = planner.PlanInitialization(
                changed,
                raw,
                new[] { richerReceipt });

            Assert.AreEqual(TerritoryMigrationStatus.CorrelationConflict, conflict.Status);
            AssertDiagnostic(conflict.Diagnostics, "CorrelationConflict");
            Assert.AreEqual(TerritoryMigrationStatus.RequiresRicherCandidate, richer.Status);
            Assert.AreEqual(TerritoryMigrationStatus.AlreadyCommittedReplay, exactReplay.Status);
            Assert.AreSame(richerReceipt, exactReplay.ExistingReceipt);
        }

        [Test]
        public void InitializationRejectsSameIdButStaleFullCatalogIdentity()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryCatalogIdentity current = planner.Catalog.Identity;
            var stale = new TerritoryCatalogIdentity(
                current.CatalogId,
                current.SchemaVersion,
                current.ContentVersion + 1,
                current.SourceRevision + "-stale",
                current.RawSha256);
            TerritoryStateRecord[] raw = Array.Empty<TerritoryStateRecord>();
            var request = new TerritoryInitializationRequest(
                "init-stale-catalog",
                TerritoryInitializationMode.NewProfile,
                stale,
                TerritorySemanticHasher.HashStates(raw),
                false,
                true);

            TerritoryMigrationPlan rejected = planner.PlanInitialization(
                request,
                raw,
                Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Rejected, rejected.Status);
            AssertDiagnostic(rejected.Diagnostics, "CatalogMismatch");
        }

        [Test]
        public void InitializationReplayPrecedesPostCommitStateValidation()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] original = Array.Empty<TerritoryStateRecord>();
            var request = new TerritoryInitializationRequest(
                "init-after-reload",
                TerritoryInitializationMode.NewProfile,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(original),
                false,
                true);
            TerritoryMigrationPlan planned = planner.PlanInitialization(
                request,
                original,
                Array.Empty<TerritoryOperationReceipt>());
            var receipt = new TerritoryOperationReceipt(
                request.OperationId,
                planned.SemanticHash,
                TerritoryOperationDurability.Committed,
                planned.ResultId);

            TerritoryMigrationPlan replay = planner.PlanInitialization(
                request,
                planned.OutputStates,
                new[] { receipt });

            Assert.AreEqual(TerritoryMigrationStatus.AlreadyCommittedReplay, replay.Status);
            Assert.AreSame(receipt, replay.ExistingReceipt);
            Assert.AreEqual(planned.ResultId, replay.ResultId);
            Assert.IsEmpty(replay.Actions);
        }

        [Test]
        public void InitializationRejectsNullDuplicateAndMaxPlusOneReceiptsDeterministically()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] raw = Array.Empty<TerritoryStateRecord>();
            var request = new TerritoryInitializationRequest(
                "receipt-hostile",
                TerritoryInitializationMode.NewProfile,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(raw),
                false,
                true);
            TerritoryOperationReceipt duplicate = new TerritoryOperationReceipt("other", new string('a', 64), TerritoryOperationDurability.Committed, "result");
            TerritoryOperationReceipt[] duplicateRows = { duplicate, null, duplicate };
            TerritoryOperationReceipt[] oversized = Enumerable.Range(0, TerritoryTechnicalLimits.MaximumReceipts + 1)
                .Select(index => new TerritoryOperationReceipt("op-" + index, new string('b', 64), TerritoryOperationDurability.Committed, "r-" + index))
                .ToArray();

            TerritoryMigrationPlan invalid = planner.PlanInitialization(request, raw, duplicateRows);
            TerritoryMigrationPlan tooMany = planner.PlanInitialization(request, raw, oversized);

            Assert.AreEqual(TerritoryMigrationStatus.Rejected, invalid.Status);
            AssertDiagnostic(invalid.Diagnostics, "NullReceipt");
            AssertDiagnostic(invalid.Diagnostics, "DuplicateReceiptOperationId", "other");
            Assert.AreEqual(TerritoryMigrationStatus.Rejected, tooMany.Status);
            AssertDiagnostic(tooMany.Diagnostics, "ReceiptLimitExceeded");
        }

        [Test]
        public void CaptureTransactionProducesImmutableTypedCommandsReceiptAndEventIdentities()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionPlan plan = PlanNeutralCapture(planner, query, "capture-1");

            Assert.AreEqual(TerritoryCaptureStatus.Planned, plan.Status);
            AssertLowerHash(plan.SemanticHash);
            Assert.True(plan.ResultId.StartsWith("territory-result-", StringComparison.Ordinal));
            Assert.True(plan.ReceiptId.StartsWith("territory-receipt-", StringComparison.Ordinal));
            Assert.True(plan.Event.EventId.StartsWith("territory-event-", StringComparison.Ordinal));
            Assert.AreEqual(100, plan.EconomyCommand.WarzoneCreditsDelta);
            Assert.AreEqual(1, plan.QuestCommand.ProgressDelta);
            Assert.AreEqual("CaptureTerritory", plan.QuestCommand.ProgressType);
            Assert.AreEqual(RealmId.None, plan.Event.PreviousOwner);
            Assert.AreEqual(RealmId.Crownlands, plan.Event.NewOwner);
            Assert.AreEqual(1, plan.Event.NewRevision);
            Assert.Throws<NotSupportedException>(() => ((IList)plan.Diagnostics).Clear());
        }

        [Test]
        public void NeutralForbiddenStateFailsQueryAndCustomRewardPlanRemainsInternallyConsistent()
        {
            TerritoryDefinition forbidden = Definition("T1", "territory.forbidden");
            TerritoryPhaseBPlanner forbiddenPlanner = new TerritoryPhaseBPlanner(
                Catalog(new[] { forbidden }, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>()));
            TerritoryQueryResult forbiddenQuery = forbiddenPlanner.BuildQuery(
                new[] { new TerritoryStateRecord("T1", RealmId.None, 0) },
                RealmId.Crownlands,
                ProfileSessionId);

            var customReward = new TerritoryCaptureRewardProfile("reward_custom", 7, "CustomCapture", 3);
            var neutral = new TerritoryDefinition(
                "T5",
                "territory.neutral",
                RealmId.None,
                ResourceType.Gold,
                10,
                false,
                PlayableRealms,
                customReward.RewardProfileId,
                true,
                Array.Empty<string>(),
                Array.Empty<string>());
            TerritoryPhaseBPlanner customPlanner = new TerritoryPhaseBPlanner(
                Catalog(new[] { neutral }, new[] { customReward }, Array.Empty<TerritoryAliasDefinition>()));
            TerritoryQueryResult customQuery = customPlanner.BuildQuery(
                new[] { new TerritoryStateRecord("T5", RealmId.None, 0) },
                RealmId.Crownlands,
                ProfileSessionId);
            TerritoryCaptureTransactionPlan customPlan = customPlanner.PlanCaptureTransaction(
                customQuery,
                NeutralCaptureRequest(customPlanner, customQuery, "custom-reward"),
                Array.Empty<TerritoryCaptureReceipt>());

            Assert.AreEqual(TerritoryQueryStatus.Unavailable, forbiddenQuery.Status);
            AssertDiagnostic(forbiddenQuery.Diagnostics, "OwnerForbidden", "T1");
            Assert.AreEqual(TerritoryCaptureStatus.Planned, customPlan.Status);
            Assert.AreEqual(7, customPlan.CapturePlan.WarzoneCreditsDelta);
            Assert.AreEqual(7, customPlan.EconomyCommand.WarzoneCreditsDelta);
            Assert.AreEqual(3, customPlan.CapturePlan.QuestProgressDelta);
            Assert.AreEqual(3, customPlan.QuestCommand.ProgressDelta);
        }

        [Test]
        public void CaptureExactReplayDoesNotTouchApplyTargetsAndConflictingReuseRejects()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionPlan first = PlanNeutralCapture(planner, query, "capture-replay");
            TerritoryCaptureReceipt receipt = CommitSuccessfully(planner, first);
            TerritoryCaptureTransactionPlan replay = planner.PlanCaptureTransaction(query, NeutralCaptureRequest(planner, query, "capture-replay"), new[] { receipt });
            TerritoryCaptureTransactionRequest different = CaptureRequest(planner, query, "capture-replay", "T3", RealmId.Crownlands, RealmId.Crownlands, 0);
            TerritoryCaptureTransactionPlan collision = planner.PlanCaptureTransaction(query, different, new[] { receipt });
            var candidate = new FakeCandidate();
            var economy = new FakeEconomy();
            var quest = new FakeQuest();

            TerritoryCaptureApplicationResult result = planner.ApplyCapture(replay, candidate, economy, quest);

            Assert.AreEqual(TerritoryCaptureStatus.AlreadyCommittedReplay, replay.Status);
            Assert.AreEqual(TerritoryApplyDisposition.Replayed, result.Disposition);
            Assert.AreSame(receipt, result.Receipt);
            Assert.AreEqual(0, candidate.TotalCalls + economy.TotalCalls + quest.TotalCalls);
            Assert.AreEqual(TerritoryCaptureStatus.CorrelationConflict, collision.Status);
        }

        [Test]
        public void CaptureReplayPrecedesAdvancedStateAndRejectsForgedReceiptIdentity()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult originalQuery = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionRequest request = NeutralCaptureRequest(
                planner,
                originalQuery,
                "capture-after-reload");
            TerritoryCaptureTransactionPlan planned = planner.PlanCaptureTransaction(
                originalQuery,
                request,
                Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureReceipt receipt = CommitSuccessfully(planner, planned);
            TerritoryStateRecord[] advancedStates = TerritoryContractPlanner.CreateCurrentBaselineStates()
                .Select(item => item.Id == "T5"
                    ? new TerritoryStateRecord("T5", RealmId.Crownlands, 1)
                    : item)
                .ToArray();
            TerritoryQueryResult advancedQuery = planner.BuildQuery(advancedStates, RealmId.Crownlands, ProfileSessionId);

            TerritoryCaptureTransactionPlan replay = planner.PlanCaptureTransaction(
                advancedQuery,
                request,
                new[] { receipt });
            var forged = new TerritoryCaptureReceipt(
                receipt.ReceiptId,
                receipt.OperationId,
                receipt.SemanticHash,
                receipt.Durability,
                receipt.ResultId,
                receipt.EventId,
                "T1",
                receipt.PreviousOwner,
                receipt.NewOwner,
                receipt.PreviousRevision,
                receipt.NewRevision,
                receipt.WarzoneCreditsDelta,
                receipt.QuestProgressDelta,
                new TerritoryCatalogIdentity(
                    receipt.CatalogId,
                    receipt.CatalogSchemaVersion,
                    receipt.CatalogContentVersion,
                    receipt.CatalogSourceRevision,
                    receipt.CatalogRawSha256),
                receipt.StateRevisionHash,
                receipt.ProfileSessionId,
                receipt.AuthorizationId,
                receipt.AuthorizationSourceResultId,
                receipt.AuthorizationSourceResultHash);
            TerritoryCaptureTransactionPlan rejected = planner.PlanCaptureTransaction(
                advancedQuery,
                request,
                new[] { forged });

            Assert.AreEqual(TerritoryCaptureStatus.AlreadyCommittedReplay, replay.Status);
            Assert.AreSame(receipt, replay.ExistingReceipt);
            Assert.AreEqual(TerritoryCaptureStatus.CorrelationConflict, rejected.Status);
            AssertDiagnostic(rejected.Diagnostics, "ReceiptIdentityMismatch", "T5");
        }

        [Test]
        public void CaptureReceiptLedgerRejectsImpossibleOwnershipAndRevisionRowsCanonically()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureReceipt valid = CommitSuccessfully(
                planner,
                PlanNeutralCapture(planner, query, "receipt-shape-source"));
            TerritoryCaptureReceipt invalidOwner = CloneReceipt(
                valid,
                "invalid-owner",
                RealmId.None);
            TerritoryCaptureReceipt invalidRevision = CloneReceipt(
                valid,
                "invalid-revision",
                null,
                valid.PreviousRevision);
            TerritoryCaptureTransactionRequest probe = NeutralCaptureRequest(
                planner,
                query,
                "receipt-shape-probe");

            TerritoryCaptureTransactionPlan forward = planner.PlanCaptureTransaction(
                query,
                probe,
                new[] { invalidOwner, invalidRevision });
            TerritoryCaptureTransactionPlan reverse = planner.PlanCaptureTransaction(
                query,
                probe,
                new[] { invalidRevision, invalidOwner });

            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, forward.Status);
            AssertDiagnostic(forward.Diagnostics, "MalformedReceipt");
            CollectionAssert.AreEqual(DiagnosticKeys(forward.Diagnostics), DiagnosticKeys(reverse.Diagnostics));
        }

        [Test]
        public void CaptureRejectsForgedQueryCatalogRealmAndStateHashAuthority()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult valid = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionRequest request = NeutralCaptureRequest(planner, valid, "capture-query-authority");
            var wrongCatalog = new TerritoryQueryResult(
                TerritoryQueryStatus.Available,
                "other_catalog",
                valid.StateRevisionHash,
                valid.CommittedProfileRealm,
                valid.Territories,
                Array.Empty<TerritoryDiagnostic>());
            var wrongRealm = new TerritoryQueryResult(
                TerritoryQueryStatus.Available,
                valid.CatalogId,
                valid.StateRevisionHash,
                RealmId.Stonehold,
                valid.Territories,
                Array.Empty<TerritoryDiagnostic>());
            TerritorySnapshot[] forgedSnapshots = valid.Territories
                .Select(item => item.State.Id == "T5"
                    ? new TerritorySnapshot(
                        item.Definition,
                        new TerritoryStateRecord("T5", RealmId.None, 99),
                        true)
                    : item)
                .ToArray();
            var wrongHash = new TerritoryQueryResult(
                TerritoryQueryStatus.Available,
                valid.CatalogId,
                valid.StateRevisionHash,
                valid.CommittedProfileRealm,
                forgedSnapshots,
                Array.Empty<TerritoryDiagnostic>());

            TerritoryCaptureTransactionPlan catalogRejected = planner.PlanCaptureTransaction(
                wrongCatalog,
                request,
                Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan realmRejected = planner.PlanCaptureTransaction(
                wrongRealm,
                request,
                Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan hashRejected = planner.PlanCaptureTransaction(
                wrongHash,
                request,
                Array.Empty<TerritoryCaptureReceipt>());

            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, catalogRejected.Status);
            AssertDiagnostic(catalogRejected.Diagnostics, "QueryCatalogMismatch", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, realmRejected.Status);
            AssertDiagnostic(realmRejected.Diagnostics, "QueryProfileRealmMismatch", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, hashRejected.Status);
            AssertDiagnostic(hashRejected.Diagnostics, "QueryStateHashMismatch", "T5");
        }

        [Test]
        public void CaptureRejectsSelfConsistentPublicQueryCloneWithoutPlannerProvenance()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult valid = Query(planner, RealmId.Crownlands);
            TerritorySnapshot[] forgedSnapshots = valid.Territories
                .Select(item => item.State.Id == "T5"
                    ? new TerritorySnapshot(
                        item.Definition,
                        new TerritoryStateRecord("T5", RealmId.None, 11),
                        true)
                    : item)
                .ToArray();
            string forgedHash = TerritorySemanticHasher.HashQueryStates(forgedSnapshots);
            var forged = new TerritoryQueryResult(
                TerritoryQueryStatus.Available,
                planner.Catalog.Identity,
                forgedHash,
                RealmId.Crownlands,
                ProfileSessionId,
                forgedSnapshots,
                Array.Empty<TerritoryDiagnostic>());
            TerritoryCaptureTransactionRequest request = CaptureRequest(
                planner,
                forged,
                "capture-forged-query",
                "T5",
                RealmId.Crownlands,
                RealmId.None,
                11);

            TerritoryCaptureTransactionPlan rejected = planner.PlanCaptureTransaction(
                forged,
                request,
                Array.Empty<TerritoryCaptureReceipt>());

            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, rejected.Status);
            AssertDiagnostic(rejected.Diagnostics, "QueryProvenanceMissing", "T5");
            Assert.False(rejected.Diagnostics.Any(item => item.Code == "QueryCatalogMismatch"));
            Assert.False(rejected.Diagnostics.Any(item => item.Code == "QueryProfileRealmMismatch"));
            Assert.False(rejected.Diagnostics.Any(item => item.Code == "QueryStateHashMismatch"));
            Assert.False(rejected.Diagnostics.Any(item => item.Code == "StaleStateRevision"));
        }

        [Test]
        public void QueryRejectsMissingOrUndefinedCommittedRealmAtTheAuthorityBoundary()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();

            TerritoryQueryResult none = planner.BuildQuery(
                TerritoryContractPlanner.CreateCurrentBaselineStates(),
                RealmId.None,
                ProfileSessionId);
            TerritoryQueryResult undefined = planner.BuildQuery(
                TerritoryContractPlanner.CreateCurrentBaselineStates(),
                (RealmId)999,
                ProfileSessionId);

            Assert.AreEqual(TerritoryQueryStatus.Unavailable, none.Status);
            AssertDiagnostic(none.Diagnostics, "NoCommittedRealm");
            Assert.AreEqual(TerritoryQueryStatus.Unavailable, undefined.Status);
            AssertDiagnostic(undefined.Diagnostics, "InvalidCommittedRealm");
        }

        [Test]
        public void UnknownFutureStateRemainsOpaqueUnsupportedAndExcludedFromBehavior()
        {
            TerritoryStateRecord unknown = new TerritoryStateRecord("T99", (RealmId)999, -5);
            TerritoryStateRecord[] states = TerritoryContractPlanner.CreateCurrentBaselineStates()
                .Concat(new[] { unknown })
                .ToArray();
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();

            TerritoryQueryResult query = planner.BuildQuery(
                states,
                RealmId.Crownlands,
                ProfileSessionId);
            TerritorySnapshot preserved = query.Territories.Single(item => item.State.Id == "T99");
            TerritoryCaptureTransactionPlan capture = planner.PlanCaptureTransaction(
                query,
                CaptureRequest(
                    planner,
                    query,
                    "capture-unknown-future",
                    "T99",
                    RealmId.Crownlands,
                    RealmId.None,
                    0),
                Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan knownCapture = planner.PlanCaptureTransaction(
                query,
                NeutralCaptureRequest(planner, query, "capture-known-with-future-row"),
                Array.Empty<TerritoryCaptureReceipt>());

            TerritoryContractPlanner incomePlanner = TerritoryContractPlanner.CreateCurrentBaseline();
            TerritoryQueryResult incomeQuery = incomePlanner.BuildQuery(states, RealmId.Crownlands);
            TerritoryIncomeSnapshot income = incomePlanner.PlanIncome(incomeQuery, RealmId.Crownlands);

            Assert.AreEqual(TerritoryQueryStatus.Available, query.Status);
            Assert.AreSame(unknown, preserved.State);
            Assert.False(preserved.IsSupported);
            Assert.IsNull(preserved.Definition);
            AssertDiagnostic(query.Diagnostics, "PreservedUnknownTerritory", "T99");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, capture.Status);
            Assert.IsNull(capture.EconomyCommand);
            Assert.IsNull(capture.QuestCommand);
            Assert.AreEqual(TerritoryCaptureStatus.Planned, knownCapture.Status);
            Assert.NotNull(knownCapture.EconomyCommand);
            Assert.AreEqual(TerritoryIncomeStatus.Available, income.Status);
            Assert.False(income.Contributions.Any(item => item.TerritoryId == "T99"));
        }

        [Test]
        public void CaptureAuthorityRejectsStaleCatalogSessionExpiredProductionAndReusedAuthorization()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionRequest original = NeutralCaptureRequest(
                planner,
                query,
                "capture-authority");
            TerritoryCatalogIdentity current = planner.Catalog.Identity;
            var staleIdentity = new TerritoryCatalogIdentity(
                current.CatalogId,
                current.SchemaVersion,
                current.ContentVersion + 1,
                current.SourceRevision + "-stale",
                current.RawSha256);
            var staleCatalog = new TerritoryCaptureTransactionRequest(
                original.CaptureRequest,
                staleIdentity,
                query.StateRevisionHash,
                ProfileSessionId,
                AuthorizationEvaluationUtcTicks);

            TerritoryCaptureAuthorization originalAuthorization = original.CaptureRequest.Authorization;
            var staleSessionAuthorization = new TerritoryCaptureAuthorization(
                originalAuthorization.AuthorizationId + "-session",
                TerritoryCaptureAuthorizationSource.FakeTestOutcome,
                "profile-session-2",
                originalAuthorization.TerritoryId,
                originalAuthorization.CapturerRealm,
                originalAuthorization.ExpectedPreviousOwner,
                originalAuthorization.ExpectedRevision,
                originalAuthorization.SourceResultId + "-session",
                TerritorySemanticHasher.HashFrames("session-2-source"),
                originalAuthorization.ExpiresAtUtcTicks,
                TerritoryAuthorizationUsePolicy.SingleUse);
            var staleSessionCapture = new TerritoryCaptureRequest(
                "capture-stale-session",
                "T5",
                RealmId.Crownlands,
                RealmId.Crownlands,
                RealmId.None,
                0,
                staleSessionAuthorization);
            var staleSession = new TerritoryCaptureTransactionRequest(
                staleSessionCapture,
                current,
                query.StateRevisionHash,
                "profile-session-2",
                AuthorizationEvaluationUtcTicks);
            var expired = new TerritoryCaptureTransactionRequest(
                original.CaptureRequest,
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                originalAuthorization.ExpiresAtUtcTicks);
            var productionAuthorization = new TerritoryCaptureAuthorization(
                originalAuthorization.AuthorizationId + "-production",
                TerritoryCaptureAuthorizationSource.BattleResult,
                ProfileSessionId,
                "T5",
                RealmId.Crownlands,
                RealmId.None,
                0,
                originalAuthorization.SourceResultId + "-production",
                TerritorySemanticHasher.HashFrames("production-source"),
                originalAuthorization.ExpiresAtUtcTicks,
                TerritoryAuthorizationUsePolicy.SingleUse);
            var production = new TerritoryCaptureTransactionRequest(
                new TerritoryCaptureRequest(
                    "capture-production-source",
                    "T5",
                    RealmId.Crownlands,
                    RealmId.Crownlands,
                    RealmId.None,
                    0,
                    productionAuthorization),
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                AuthorizationEvaluationUtcTicks);
            var missing = new TerritoryCaptureTransactionRequest(
                new TerritoryCaptureRequest(
                    "capture-missing-authorization",
                    "T5",
                    RealmId.Crownlands,
                    RealmId.Crownlands,
                    RealmId.None,
                    0,
                    null),
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                AuthorizationEvaluationUtcTicks);
            var malformed = new TerritoryCaptureTransactionRequest(
                new TerritoryCaptureRequest(
                    "capture-malformed-authorization",
                    "T5",
                    RealmId.Crownlands,
                    RealmId.Crownlands,
                    RealmId.None,
                    0,
                    new TerritoryCaptureAuthorization(
                        "legacy-shape-only",
                        "T5",
                        RealmId.Crownlands,
                        RealmId.None,
                        0)),
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                AuthorizationEvaluationUtcTicks);
            var zeroEvaluation = new TerritoryCaptureTransactionRequest(
                original.CaptureRequest,
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                0);

            TerritoryCaptureTransactionPlan originalPlan = planner.PlanCaptureTransaction(
                query,
                original,
                Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureReceipt committed = CommitSuccessfully(planner, originalPlan);
            var reusedCapture = new TerritoryCaptureRequest(
                "capture-authority-reuse",
                "T5",
                RealmId.Crownlands,
                RealmId.Crownlands,
                RealmId.None,
                0,
                originalAuthorization);
            var reused = new TerritoryCaptureTransactionRequest(
                reusedCapture,
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                AuthorizationEvaluationUtcTicks);
            var laterReplay = new TerritoryCaptureTransactionRequest(
                original.CaptureRequest,
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                originalAuthorization.ExpiresAtUtcTicks + 1);
            var conflictingSourceAuthorization = new TerritoryCaptureAuthorization(
                originalAuthorization.AuthorizationId + "-new-wrapper",
                TerritoryCaptureAuthorizationSource.FakeTestOutcome,
                ProfileSessionId,
                "T5",
                RealmId.Crownlands,
                RealmId.None,
                0,
                originalAuthorization.SourceResultId,
                TerritorySemanticHasher.HashFrames("changed-source-result"),
                originalAuthorization.ExpiresAtUtcTicks,
                TerritoryAuthorizationUsePolicy.SingleUse);
            var conflictingSource = new TerritoryCaptureTransactionRequest(
                new TerritoryCaptureRequest(
                    "capture-source-conflict",
                    "T5",
                    RealmId.Crownlands,
                    RealmId.Crownlands,
                    RealmId.None,
                    0,
                    conflictingSourceAuthorization),
                current,
                query.StateRevisionHash,
                ProfileSessionId,
                AuthorizationEvaluationUtcTicks);
            TerritoryCaptureReceipt duplicateLedgerReceipt = CloneReceipt(
                committed,
                "duplicate-ledger");
            TerritoryCaptureTransactionRequest ledgerProbe = NeutralCaptureRequest(
                planner,
                query,
                "capture-ledger-probe");

            TerritoryCaptureTransactionPlan staleCatalogResult = planner.PlanCaptureTransaction(query, staleCatalog, Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan staleSessionResult = planner.PlanCaptureTransaction(query, staleSession, Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan expiredResult = planner.PlanCaptureTransaction(query, expired, Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan productionResult = planner.PlanCaptureTransaction(query, production, Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan missingResult = planner.PlanCaptureTransaction(query, missing, Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan malformedResult = planner.PlanCaptureTransaction(query, malformed, Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan zeroEvaluationResult = planner.PlanCaptureTransaction(query, zeroEvaluation, Array.Empty<TerritoryCaptureReceipt>());
            TerritoryCaptureTransactionPlan reusedResult = planner.PlanCaptureTransaction(query, reused, new[] { committed });
            TerritoryCaptureTransactionPlan sourceConflictResult = planner.PlanCaptureTransaction(query, conflictingSource, new[] { committed });
            TerritoryCaptureTransactionPlan corruptLedgerResult = planner.PlanCaptureTransaction(
                query,
                ledgerProbe,
                new[] { committed, duplicateLedgerReceipt });
            TerritoryCaptureTransactionPlan replayResult = planner.PlanCaptureTransaction(query, laterReplay, new[] { committed });

            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, staleCatalogResult.Status);
            AssertDiagnostic(staleCatalogResult.Diagnostics, "CatalogMismatch", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, staleSessionResult.Status);
            AssertDiagnostic(staleSessionResult.Diagnostics, "QueryProfileSessionMismatch", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedUnauthorized, expiredResult.Status);
            AssertDiagnostic(expiredResult.Diagnostics, "ExpiredAuthorization", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedUnauthorized, productionResult.Status);
            AssertDiagnostic(productionResult.Diagnostics, "AuthorizationSourceUnavailable", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedUnauthorized, missingResult.Status);
            AssertDiagnostic(missingResult.Diagnostics, "MissingAuthorization", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedUnauthorized, malformedResult.Status);
            AssertDiagnostic(malformedResult.Diagnostics, "MalformedAuthorization", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedUnauthorized, zeroEvaluationResult.Status);
            AssertDiagnostic(zeroEvaluationResult.Diagnostics, "InvalidAuthorizationEvaluationTime", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedUnauthorized, reusedResult.Status);
            AssertDiagnostic(reusedResult.Diagnostics, "AuthorizationAlreadyUsed", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.CorrelationConflict, sourceConflictResult.Status);
            AssertDiagnostic(sourceConflictResult.Diagnostics, "AuthorizationSourceResultConflict", "T5");
            Assert.AreEqual(TerritoryCaptureStatus.RejectedDomainMalformed, corruptLedgerResult.Status);
            AssertDiagnostic(corruptLedgerResult.Diagnostics, "DuplicateReceiptAuthorizationId");
            AssertDiagnostic(corruptLedgerResult.Diagnostics, "DuplicateReceiptAuthorizationSourceResultId");
            Assert.AreEqual(TerritoryCaptureStatus.AlreadyCommittedReplay, replayResult.Status);
            Assert.AreSame(committed, replayResult.ExistingReceipt);
        }

        [Test]
        public void PhaseBQueryUsesSelectedCatalogIdentityAndRejectsOversizedStateIds()
        {
            TerritoryDefinition neutral = new TerritoryDefinition(
                "T5",
                "territory.neutral",
                RealmId.None,
                ResourceType.Gold,
                10,
                false,
                PlayableRealms,
                TerritoryContractPlanner.CurrentRewardProfileId,
                true,
                Array.Empty<string>(),
                Array.Empty<string>());
            TerritoryCaptureRewardProfile reward = CurrentReward();
            TerritoryCatalogIdentity identity = TerritoryPhaseBPlanner.CreateIdentity(
                "territory_custom_v1",
                1,
                1,
                "territory-custom-source-v1",
                new[] { neutral },
                new[] { reward },
                Array.Empty<TerritoryAliasDefinition>());
            TerritoryPhaseBPlanner custom = new TerritoryPhaseBPlanner(
                new TerritoryPhaseBCatalog(
                    identity,
                    new[] { neutral },
                    new[] { reward },
                    Array.Empty<TerritoryAliasDefinition>()));
            TerritoryQueryResult customQuery = custom.BuildQuery(
                new[] { new TerritoryStateRecord("T5", RealmId.None, 0) },
                RealmId.Crownlands,
                ProfileSessionId);
            TerritoryCaptureTransactionPlan customPlan = custom.PlanCaptureTransaction(
                customQuery,
                NeutralCaptureRequest(custom, customQuery, "custom-catalog-capture"),
                Array.Empty<TerritoryCaptureReceipt>());

            TerritoryPhaseBPlanner baseline = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] oversized = TerritoryContractPlanner.CreateCurrentBaselineStates()
                .Concat(new[]
                {
                    new TerritoryStateRecord(
                        new string('x', TerritoryTechnicalLimits.MaximumHashFrameUtf8Bytes + 1),
                        RealmId.Stonehold,
                        0)
                })
                .ToArray();
            TerritoryQueryResult rejected = baseline.BuildQuery(oversized, RealmId.Crownlands, ProfileSessionId);

            Assert.AreEqual(TerritoryQueryStatus.Available, customQuery.Status);
            Assert.AreEqual(identity.CatalogId, customQuery.CatalogId);
            Assert.AreEqual(TerritoryCaptureStatus.Planned, customPlan.Status);
            Assert.AreEqual(TerritoryQueryStatus.Unavailable, rejected.Status);
            AssertDiagnostic(rejected.Diagnostics, "InvalidStateId");
        }

        [Test]
        public void CaptureRejectsStaleStateHashAndRevisionOverflowBeforeCommands()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionRequest stale = NeutralCaptureRequest(planner, query, "capture-stale", new string('0', 64));
            TerritoryCaptureTransactionPlan stalePlan = planner.PlanCaptureTransaction(query, stale, Array.Empty<TerritoryCaptureReceipt>());

            TerritoryStateRecord[] overflowStates = TerritoryContractPlanner.CreateCurrentBaselineStates()
                .Select(item => item.Id == "T5" ? new TerritoryStateRecord("T5", RealmId.None, long.MaxValue) : item)
                .ToArray();
            TerritoryQueryResult overflowQuery = planner.BuildQuery(overflowStates, RealmId.Crownlands, ProfileSessionId);
            TerritoryCaptureTransactionPlan overflow = planner.PlanCaptureTransaction(
                overflowQuery,
                CaptureRequest(planner, overflowQuery, "capture-overflow", "T5", RealmId.Crownlands, RealmId.None, long.MaxValue),
                Array.Empty<TerritoryCaptureReceipt>());

            Assert.AreEqual(TerritoryCaptureStatus.RejectedStaleRevision, stalePlan.Status);
            Assert.IsNull(stalePlan.EconomyCommand);
            Assert.IsNull(stalePlan.QuestCommand);
            Assert.AreEqual(TerritoryCaptureStatus.RejectedOverflow, overflow.Status);
            Assert.IsNull(overflow.EconomyCommand);
        }

        [Test]
        public void CapturePlanIsStableAcrossStatePermutation()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] forward = TerritoryContractPlanner.CreateCurrentBaselineStates().ToArray();
            TerritoryStateRecord[] reverse = forward.Reverse().ToArray();
            TerritoryQueryResult leftQuery = planner.BuildQuery(forward, RealmId.Crownlands, ProfileSessionId);
            TerritoryQueryResult rightQuery = planner.BuildQuery(reverse, RealmId.Crownlands, ProfileSessionId);

            TerritoryCaptureTransactionPlan left = PlanNeutralCapture(planner, leftQuery, "capture-permutation");
            TerritoryCaptureTransactionPlan right = PlanNeutralCapture(planner, rightQuery, "capture-permutation");

            Assert.AreEqual(leftQuery.StateRevisionHash, rightQuery.StateRevisionHash);
            Assert.AreEqual(left.SemanticHash, right.SemanticHash);
            Assert.AreEqual(left.Event.EventId, right.Event.EventId);
        }

        [Test]
        public void SuccessfulFakeApplicationCommitsAllTypedEffectsOnce()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryCaptureTransactionPlan plan = PlanNeutralCapture(planner, Query(planner, RealmId.Crownlands), "apply-success");
            var candidate = new FakeCandidate();
            var economy = new FakeEconomy();
            var quest = new FakeQuest();

            TerritoryCaptureApplicationResult result = planner.ApplyCapture(plan, candidate, economy, quest);

            Assert.AreEqual(TerritoryApplyDisposition.Committed, result.Disposition);
            Assert.AreEqual(TerritoryOperationDurability.Committed, result.Receipt.Durability);
            Assert.AreSame(plan.Event, result.Event);
            Assert.AreEqual(planner.Catalog.Identity.CatalogId, result.Receipt.CatalogId);
            Assert.AreEqual(planner.Catalog.Identity.SchemaVersion, result.Receipt.CatalogSchemaVersion);
            Assert.AreEqual(planner.Catalog.Identity.ContentVersion, result.Receipt.CatalogContentVersion);
            Assert.AreEqual(planner.Catalog.Identity.SourceRevision, result.Receipt.CatalogSourceRevision);
            Assert.AreEqual(planner.Catalog.Identity.RawSha256, result.Receipt.CatalogRawSha256);
            Assert.AreEqual(ProfileSessionId, result.Receipt.ProfileSessionId);
            AssertLowerHash(result.Receipt.StateRevisionHash);
            AssertLowerHash(result.Receipt.AuthorizationSourceResultHash);
            Assert.AreEqual(1, candidate.OwnershipCalls);
            Assert.AreEqual(1, candidate.ReceiptCalls);
            Assert.AreEqual(1, candidate.OutboxCalls);
            Assert.AreEqual(1, candidate.CommitCalls);
            Assert.AreEqual(0, candidate.RollbackCalls);
            Assert.AreEqual(100, economy.AppliedCredits);
            Assert.AreEqual(1, quest.AppliedProgress);
        }

        [Test]
        public void RepeatedAndConcurrentPlansCommitRewardsExactlyOnceAgainstOneCandidate()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionPlan first = PlanNeutralCapture(planner, query, "apply-once-a");
            TerritoryCaptureTransactionPlan concurrent = PlanNeutralCapture(planner, query, "apply-once-b");
            var candidate = new FakeCandidate();
            var economy = new FakeEconomy();
            var quest = new FakeQuest();

            TerritoryCaptureApplicationResult committed = planner.ApplyCapture(first, candidate, economy, quest);
            TerritoryCaptureApplicationResult repeated = planner.ApplyCapture(first, candidate, economy, quest);
            TerritoryCaptureApplicationResult staleConcurrent = planner.ApplyCapture(concurrent, candidate, economy, quest);

            Assert.AreEqual(TerritoryApplyDisposition.Committed, committed.Disposition);
            Assert.AreEqual(TerritoryApplyDisposition.Rejected, repeated.Disposition);
            AssertDiagnostic(repeated.Diagnostics, "CandidateOwnershipRejected", "T5");
            Assert.AreEqual(TerritoryApplyDisposition.Rejected, staleConcurrent.Disposition);
            AssertDiagnostic(staleConcurrent.Diagnostics, "CandidateOwnershipRejected", "T5");
            Assert.AreEqual(100, economy.AppliedCredits);
            Assert.AreEqual(1, quest.AppliedProgress);
            Assert.AreEqual(1, candidate.CommitCalls);
        }

        [Test]
        public void LaterFailedOperationCannotRollbackEarlierCommittedRewards()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            var economy = new FakeEconomy();
            var quest = new FakeQuest();

            TerritoryCaptureApplicationResult first = planner.ApplyCapture(
                PlanNeutralCapture(planner, query, "apply-retained-a"),
                new FakeCandidate(),
                economy,
                quest);
            Assert.AreEqual(TerritoryApplyDisposition.Committed, first.Disposition);
            Assert.AreEqual(100, economy.AppliedCredits);
            Assert.AreEqual(1, quest.AppliedProgress);

            economy.ThrowApply = true;
            TerritoryCaptureApplicationResult economyFailure = planner.ApplyCapture(
                PlanNeutralCapture(planner, query, "apply-retained-b"),
                new FakeCandidate(),
                economy,
                quest);
            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, economyFailure.Disposition);
            Assert.AreEqual(100, economy.AppliedCredits);
            Assert.AreEqual(1, quest.AppliedProgress);

            economy.ThrowApply = false;
            quest.ThrowApply = true;
            TerritoryCaptureApplicationResult questFailure = planner.ApplyCapture(
                PlanNeutralCapture(planner, query, "apply-retained-c"),
                new FakeCandidate(),
                economy,
                quest);
            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, questFailure.Disposition);
            Assert.AreEqual(100, economy.AppliedCredits);
            Assert.AreEqual(1, quest.AppliedProgress);
        }

        [Test]
        public void RewardTargetsRejectCheckedCreditAndProgressOverflowBeforeCommit()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionPlan creditPlan = PlanNeutralCapture(planner, query, "apply-credit-overflow");
            var creditCandidate = new FakeCandidate();
            var creditEconomy = new FakeEconomy(int.MaxValue - 50);
            var creditQuest = new FakeQuest();

            TerritoryCaptureApplicationResult creditResult = planner.ApplyCapture(
                creditPlan,
                creditCandidate,
                creditEconomy,
                creditQuest);

            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, creditResult.Disposition);
            AssertDiagnostic(creditResult.Diagnostics, "EconomyApplyRejected", "T5");
            Assert.AreEqual(int.MaxValue - 50, creditEconomy.AppliedCredits);
            Assert.AreEqual(0, creditQuest.AppliedProgress);
            Assert.AreEqual(0, creditCandidate.CommitCalls);
            Assert.IsNull(creditResult.Event);

            TerritoryCaptureTransactionPlan questPlan = PlanNeutralCapture(planner, query, "apply-progress-overflow");
            var questCandidate = new FakeCandidate();
            var questEconomy = new FakeEconomy();
            var quest = new FakeQuest(int.MaxValue);

            TerritoryCaptureApplicationResult questResult = planner.ApplyCapture(
                questPlan,
                questCandidate,
                questEconomy,
                quest);

            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, questResult.Disposition);
            AssertDiagnostic(questResult.Diagnostics, "QuestApplyRejected", "T5");
            Assert.AreEqual(0, questEconomy.AppliedCredits);
            Assert.AreEqual(int.MaxValue, quest.AppliedProgress);
            Assert.AreEqual(0, questCandidate.CommitCalls);
            Assert.IsNull(questResult.Event);
        }

        [Test]
        public void EveryApplyPhaseHasTypedRejectedInvalidAndExceptionDiagnostics()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();

            AssertDiagnostic(
                ApplyScenario(planner, "phase-ownership-rejected", new FakeCandidate { OwnershipStatus = TerritoryApplyStepStatus.Rejected }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateOwnershipRejected");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-economy-rejected", new FakeCandidate(), new FakeEconomy { ApplyStatus = TerritoryApplyStepStatus.Rejected }, new FakeQuest()).Diagnostics,
                "EconomyApplyRejected");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-economy-invalid", new FakeCandidate(), new FakeEconomy { ApplyStatus = (TerritoryApplyStepStatus)999 }, new FakeQuest()).Diagnostics,
                "EconomyApplyInvalidStatus");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-quest-rejected", new FakeCandidate(), new FakeEconomy(), new FakeQuest { ApplyStatus = TerritoryApplyStepStatus.Rejected }).Diagnostics,
                "QuestApplyRejected");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-receipt-rejected", new FakeCandidate { ReceiptStatus = TerritoryApplyStepStatus.Rejected }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateReceiptRejected");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-outbox-rejected", new FakeCandidate { OutboxStatus = TerritoryApplyStepStatus.Rejected }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateOutboxRejected");

            AssertDiagnostic(
                ApplyScenario(planner, "phase-ownership-invalid", new FakeCandidate { OwnershipStatus = (TerritoryApplyStepStatus)999 }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateOwnershipInvalidStatus");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-quest-invalid", new FakeCandidate(), new FakeEconomy(), new FakeQuest { ApplyStatus = (TerritoryApplyStepStatus)999 }).Diagnostics,
                "QuestApplyInvalidStatus");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-receipt-invalid", new FakeCandidate { ReceiptStatus = (TerritoryApplyStepStatus)999 }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateReceiptInvalidStatus");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-outbox-invalid", new FakeCandidate { OutboxStatus = (TerritoryApplyStepStatus)999 }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateOutboxInvalidStatus");

            AssertDiagnostic(
                ApplyScenario(planner, "phase-ownership-exception", new FakeCandidate { ThrowOwnership = true }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateOwnershipException");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-economy-exception", new FakeCandidate(), new FakeEconomy { ThrowApply = true }, new FakeQuest()).Diagnostics,
                "EconomyApplyException");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-quest-exception", new FakeCandidate(), new FakeEconomy(), new FakeQuest { ThrowApply = true }).Diagnostics,
                "QuestApplyException");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-receipt-exception", new FakeCandidate { ThrowReceipt = true }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateReceiptException");
            AssertDiagnostic(
                ApplyScenario(planner, "phase-outbox-exception", new FakeCandidate { ThrowOutbox = true }, new FakeEconomy(), new FakeQuest()).Diagnostics,
                "CandidateOutboxException");

            TerritoryCaptureApplicationResult rollback = ApplyScenario(
                planner,
                "phase-multiple-rollback",
                new FakeCandidate { RollbackSucceeds = false },
                new FakeEconomy { RollbackSucceeds = false },
                new FakeQuest { ApplyStatus = TerritoryApplyStepStatus.Rejected });
            AssertDiagnostic(rollback.Diagnostics, "EconomyRollbackRejected");
            AssertDiagnostic(rollback.Diagnostics, "CandidateRollbackRejected");

            TerritoryCaptureApplicationResult questRollback = ApplyScenario(
                planner,
                "phase-quest-rollback-rejected",
                new FakeCandidate { OutboxStatus = TerritoryApplyStepStatus.Rejected },
                new FakeEconomy(),
                new FakeQuest { RollbackSucceeds = false });
            AssertDiagnostic(questRollback.Diagnostics, "QuestRollbackRejected");

            TerritoryCaptureApplicationResult rollbackExceptions = ApplyScenario(
                planner,
                "phase-rollback-exceptions",
                new FakeCandidate { OutboxStatus = TerritoryApplyStepStatus.Rejected, ThrowRollback = true },
                new FakeEconomy { ThrowRollback = true },
                new FakeQuest { ThrowRollback = true });
            AssertDiagnostic(rollbackExceptions.Diagnostics, "QuestRollbackException");
            AssertDiagnostic(rollbackExceptions.Diagnostics, "EconomyRollbackException");
            AssertDiagnostic(rollbackExceptions.Diagnostics, "CandidateRollbackException");

            TerritoryCaptureApplicationResult commitRejected = ApplyScenario(
                planner,
                "phase-commit-rejected",
                new FakeCandidate { CommitStatus = TerritoryCommitStatus.Rejected },
                new FakeEconomy(),
                new FakeQuest());
            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, commitRejected.Disposition);
            AssertDiagnostic(commitRejected.Diagnostics, "CommitRejected");

            TerritoryCaptureApplicationResult commitException = ApplyScenario(
                planner,
                "phase-commit-exception",
                new FakeCandidate { ThrowCommit = true },
                new FakeEconomy(),
                new FakeQuest());
            Assert.AreEqual(TerritoryApplyDisposition.CommitUncertain, commitException.Disposition);
            AssertDiagnostic(commitException.Diagnostics, "CommitException");
        }

        [Test]
        public void PreCommitQuestFailureRollsBackCandidateAndEconomy()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryCaptureTransactionPlan plan = PlanNeutralCapture(planner, Query(planner, RealmId.Crownlands), "apply-rollback");
            var candidate = new FakeCandidate();
            var economy = new FakeEconomy();
            var quest = new FakeQuest { ApplyStatus = TerritoryApplyStepStatus.Rejected };

            TerritoryCaptureApplicationResult result = planner.ApplyCapture(plan, candidate, economy, quest);

            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, result.Disposition);
            Assert.AreEqual(1, candidate.RollbackCalls);
            Assert.AreEqual(1, economy.RollbackCalls);
            Assert.AreEqual(0, economy.AppliedCredits);
            Assert.AreEqual(0, quest.AppliedProgress);
            Assert.IsNull(result.Event);
        }

        [Test]
        public void FailedRollbackReturnsCommitUncertainWithoutFalseEvent()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryQueryResult query = Query(planner, RealmId.Crownlands);
            TerritoryCaptureTransactionRequest request = NeutralCaptureRequest(planner, query, "apply-rollback-uncertain");
            TerritoryCaptureTransactionPlan plan = planner.PlanCaptureTransaction(
                query,
                request,
                Array.Empty<TerritoryCaptureReceipt>());
            var candidate = new FakeCandidate();
            var economy = new FakeEconomy { RollbackSucceeds = false };
            var quest = new FakeQuest { ApplyStatus = TerritoryApplyStepStatus.Rejected };

            TerritoryCaptureApplicationResult result = planner.ApplyCapture(plan, candidate, economy, quest);
            TerritoryCaptureTransactionPlan reconciliation = planner.PlanCaptureTransaction(
                query,
                request,
                new[] { result.Receipt });

            Assert.AreEqual(TerritoryApplyDisposition.CommitUncertain, result.Disposition);
            Assert.AreEqual(TerritoryOperationDurability.CommitUncertain, result.Receipt.Durability);
            Assert.IsNull(result.Event);
            AssertDiagnostic(result.Diagnostics, "RollbackUncertain");
            Assert.AreEqual(TerritoryCaptureStatus.CommitUncertain, reconciliation.Status);
            Assert.AreSame(result.Receipt, reconciliation.ExistingReceipt);
        }

        [Test]
        public void CandidateCommitUncertaintyIsNotBlindlyRolledBackOrPublished()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryCaptureTransactionPlan plan = PlanNeutralCapture(planner, Query(planner, RealmId.Crownlands), "apply-commit-uncertain");
            var candidate = new FakeCandidate { CommitStatus = TerritoryCommitStatus.Uncertain };
            var economy = new FakeEconomy();
            var quest = new FakeQuest();

            TerritoryCaptureApplicationResult result = planner.ApplyCapture(plan, candidate, economy, quest);

            Assert.AreEqual(TerritoryApplyDisposition.CommitUncertain, result.Disposition);
            Assert.AreEqual(0, candidate.RollbackCalls);
            Assert.AreEqual(0, economy.RollbackCalls);
            Assert.AreEqual(0, quest.RollbackCalls);
            Assert.AreEqual(TerritoryOperationDurability.CommitUncertain, result.Receipt.Durability);
            Assert.IsNull(result.Event);
            AssertDiagnostic(result.Diagnostics, "CommitUncertain");
        }

        [Test]
        public void PublicForgedPlanAndUndefinedCommitStatusFailClosedWithoutPublication()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryCaptureTransactionPlan valid = PlanNeutralCapture(
                planner,
                Query(planner, RealmId.Crownlands),
                "apply-provenance");
            var forged = new TerritoryCaptureTransactionPlan(
                valid.Status,
                valid.SemanticHash,
                valid.ResultId,
                valid.ReceiptId,
                valid.RewardProfileId,
                valid.CapturePlan,
                valid.EconomyCommand,
                valid.QuestCommand,
                valid.Event,
                valid.ExistingReceipt,
                valid.Diagnostics);
            var forgedCandidate = new FakeCandidate();
            var forgedEconomy = new FakeEconomy();
            var forgedQuest = new FakeQuest();

            TerritoryCaptureApplicationResult forgedResult = planner.ApplyCapture(
                forged,
                forgedCandidate,
                forgedEconomy,
                forgedQuest);
            var invalidCommitCandidate = new FakeCandidate
            {
                CommitStatus = (TerritoryCommitStatus)999
            };
            TerritoryCaptureApplicationResult invalidCommit = planner.ApplyCapture(
                valid,
                invalidCommitCandidate,
                new FakeEconomy(),
                new FakeQuest());

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, forgedResult.Disposition);
            AssertDiagnostic(forgedResult.Diagnostics, "PlannerProvenanceMissing", "T5");
            Assert.AreEqual(0, forgedCandidate.TotalCalls + forgedEconomy.TotalCalls + forgedQuest.TotalCalls);
            Assert.AreEqual(TerritoryApplyDisposition.CommitUncertain, invalidCommit.Disposition);
            Assert.IsNull(invalidCommit.Event);
            AssertDiagnostic(invalidCommit.Diagnostics, "InvalidCommitStatus", "T5");
        }

        [Test]
        public void MaximumUnknownPlusMissingMigrationRowsAreCompleteAndUntruncated()
        {
            TerritoryDefinition[] definitions = Enumerable
                .Range(0, TerritoryTechnicalLimits.MaximumDefinitions)
                .Select(index => Definition("K" + index.ToString("D3"), "territory.known_" + index.ToString("D3")))
                .ToArray();
            TerritoryPhaseBPlanner planner = new TerritoryPhaseBPlanner(
                Catalog(definitions, new[] { CurrentReward() }, Array.Empty<TerritoryAliasDefinition>()));
            TerritoryStateRecord[] unknown = Enumerable
                .Range(0, TerritoryTechnicalLimits.MaximumStateRows)
                .Select(index => new TerritoryStateRecord("U" + index.ToString("D3"), (RealmId)999, -1))
                .ToArray();
            var request = new TerritoryInitializationRequest(
                "migration-maximum",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(unknown),
                false,
                true);

            TerritoryMigrationPlan plan = planner.PlanInitialization(
                request,
                unknown,
                Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Planned, plan.Status);
            Assert.AreEqual(TerritoryTechnicalLimits.MaximumStateRows, plan.PreservedUnknownStates.Count);
            Assert.AreEqual(TerritoryTechnicalLimits.MaximumMigrationRows, plan.OutputStates.Count);
            Assert.AreEqual(TerritoryTechnicalLimits.MaximumMigrationRows, plan.Actions.Count);
            Assert.AreEqual(TerritoryTechnicalLimits.MaximumDefinitions, plan.OutputStates.Count(item => item.Id.StartsWith("K", StringComparison.Ordinal)));
        }

        [Test]
        public void OversizedStateIdAndNullStateCollectionRejectWithoutThrowingOrSeeding()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryStateRecord[] malformed =
            {
                new TerritoryStateRecord(new string('x', TerritoryTechnicalLimits.MaximumHashFrameUtf8Bytes + 1), RealmId.Stonehold, 0)
            };
            var malformedRequest = new TerritoryInitializationRequest(
                "migration-oversized-id",
                TerritoryInitializationMode.Legacy,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(malformed),
                false,
                false);
            var nullRequest = new TerritoryInitializationRequest(
                "migration-null-state",
                TerritoryInitializationMode.NewProfile,
                planner.Catalog.Identity,
                TerritorySemanticHasher.HashStates(Array.Empty<TerritoryStateRecord>()),
                false,
                true);

            TerritoryMigrationPlan malformedPlan = planner.PlanInitialization(
                malformedRequest,
                malformed,
                Array.Empty<TerritoryOperationReceipt>());
            TerritoryMigrationPlan nullPlan = planner.PlanInitialization(
                nullRequest,
                null,
                Array.Empty<TerritoryOperationReceipt>());

            Assert.AreEqual(TerritoryMigrationStatus.Rejected, malformedPlan.Status);
            AssertDiagnostic(malformedPlan.Diagnostics, "InvalidStateId");
            Assert.AreEqual(TerritoryMigrationStatus.Rejected, nullPlan.Status);
            AssertDiagnostic(nullPlan.Diagnostics, "NullStateCollection");
            Assert.IsEmpty(nullPlan.OutputStates);
        }

        [Test]
        public void MissingFakeDependencyRejectsBeforeAnyApply()
        {
            TerritoryPhaseBPlanner planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
            TerritoryCaptureTransactionPlan plan = PlanNeutralCapture(planner, Query(planner, RealmId.Crownlands), "apply-missing");
            var candidate = new FakeCandidate();
            var quest = new FakeQuest();

            TerritoryCaptureApplicationResult result = planner.ApplyCapture(plan, candidate, null, quest);

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
            Assert.AreEqual(0, candidate.TotalCalls + quest.TotalCalls);
            AssertDiagnostic(result.Diagnostics, "DependencyUnavailable");
        }

        private static TerritoryCaptureTransactionPlan PlanNeutralCapture(TerritoryPhaseBPlanner planner, TerritoryQueryResult query, string operationId)
        {
            return planner.PlanCaptureTransaction(query, NeutralCaptureRequest(planner, query, operationId), Array.Empty<TerritoryCaptureReceipt>());
        }

        private static TerritoryCaptureReceipt CommitSuccessfully(
            TerritoryPhaseBPlanner planner,
            TerritoryCaptureTransactionPlan plan)
        {
            TerritoryCaptureApplicationResult result = planner.ApplyCapture(
                plan,
                new FakeCandidate(),
                new FakeEconomy(),
                new FakeQuest());
            Assert.AreEqual(TerritoryApplyDisposition.Committed, result.Disposition);
            Assert.NotNull(result.Receipt);
            return result.Receipt;
        }

        private static TerritoryCaptureReceipt CloneReceipt(
            TerritoryCaptureReceipt source,
            string suffix,
            RealmId? newOwner = null,
            long? newRevision = null,
            string authorizationId = null,
            string authorizationSourceResultId = null,
            string authorizationSourceResultHash = null)
        {
            return new TerritoryCaptureReceipt(
                "receipt-" + suffix,
                "operation-" + suffix,
                source.SemanticHash,
                source.Durability,
                "result-" + suffix,
                "event-" + suffix,
                source.TerritoryId,
                source.PreviousOwner,
                newOwner ?? source.NewOwner,
                source.PreviousRevision,
                newRevision ?? source.NewRevision,
                source.WarzoneCreditsDelta,
                source.QuestProgressDelta,
                new TerritoryCatalogIdentity(
                    source.CatalogId,
                    source.CatalogSchemaVersion,
                    source.CatalogContentVersion,
                    source.CatalogSourceRevision,
                    source.CatalogRawSha256),
                source.StateRevisionHash,
                source.ProfileSessionId,
                authorizationId ?? source.AuthorizationId,
                authorizationSourceResultId ?? source.AuthorizationSourceResultId,
                authorizationSourceResultHash ?? source.AuthorizationSourceResultHash);
        }

        private static TerritoryCaptureApplicationResult ApplyScenario(
            TerritoryPhaseBPlanner planner,
            string operationId,
            FakeCandidate candidate,
            FakeEconomy economy,
            FakeQuest quest)
        {
            TerritoryCaptureTransactionPlan plan = PlanNeutralCapture(
                planner,
                Query(planner, RealmId.Crownlands),
                operationId);
            TerritoryCaptureApplicationResult result = planner.ApplyCapture(plan, candidate, economy, quest);
            if (result.Disposition == TerritoryApplyDisposition.Rejected ||
                result.Disposition == TerritoryApplyDisposition.RolledBack)
            {
                Assert.AreEqual(RealmId.None, candidate.CurrentOwner);
                Assert.AreEqual(0, candidate.CurrentRevision);
                Assert.AreEqual(0, economy.AppliedCredits);
                Assert.AreEqual(0, quest.AppliedProgress);
            }

            return result;
        }

        private static TerritoryCaptureTransactionRequest NeutralCaptureRequest(TerritoryPhaseBPlanner planner, TerritoryQueryResult query, string operationId, string expectedStateHash = null)
        {
            return CaptureRequest(planner, query, operationId, "T5", RealmId.Crownlands, RealmId.None, 0, expectedStateHash);
        }

        private static TerritoryCaptureTransactionRequest CaptureRequest(
            TerritoryPhaseBPlanner planner,
            TerritoryQueryResult query,
            string operationId,
            string territoryId,
            RealmId capturer,
            RealmId expectedOwner,
            long expectedRevision,
            string expectedStateHash = null)
        {
            var authorization = new TerritoryCaptureAuthorization(
                "auth-" + operationId,
                TerritoryCaptureAuthorizationSource.FakeTestOutcome,
                ProfileSessionId,
                territoryId,
                capturer,
                expectedOwner,
                expectedRevision,
                "source-result-" + operationId,
                TerritorySemanticHasher.HashFrames("fake-capture-outcome", operationId),
                AuthorizationEvaluationUtcTicks + TimeSpan.TicksPerHour,
                TerritoryAuthorizationUsePolicy.SingleUse);
            var request = new TerritoryCaptureRequest(
                operationId,
                territoryId,
                capturer,
                capturer,
                expectedOwner,
                expectedRevision,
                authorization);
            return new TerritoryCaptureTransactionRequest(
                request,
                planner.Catalog.Identity,
                expectedStateHash ?? query.StateRevisionHash,
                ProfileSessionId,
                AuthorizationEvaluationUtcTicks);
        }

        private static TerritoryQueryResult Query(TerritoryPhaseBPlanner planner, RealmId realm)
        {
            return planner.BuildQuery(
                TerritoryContractPlanner.CreateCurrentBaselineStates(),
                realm,
                ProfileSessionId);
        }

        private static TerritoryDefinition Definition(string id, string contentKey)
        {
            return new TerritoryDefinition(
                id,
                contentKey,
                RealmId.Stonehold,
                ResourceType.Stone,
                50,
                false,
                PlayableRealms,
                TerritoryContractPlanner.CurrentRewardProfileId,
                false,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static TerritoryCaptureRewardProfile CurrentReward()
        {
            return new TerritoryCaptureRewardProfile(
                TerritoryContractPlanner.CurrentRewardProfileId,
                TerritoryContractPlanner.CaptureWarzoneCreditReward,
                "CaptureTerritory",
                TerritoryContractPlanner.CaptureQuestProgressReward);
        }

        private static TerritoryPhaseBCatalog Catalog(
            IEnumerable<TerritoryDefinition> definitions,
            IEnumerable<TerritoryCaptureRewardProfile> rewards,
            IEnumerable<TerritoryAliasDefinition> aliases)
        {
            TerritoryDefinition[] definitionRows = definitions?.ToArray();
            TerritoryCaptureRewardProfile[] rewardRows = rewards?.ToArray();
            TerritoryAliasDefinition[] aliasRows = aliases?.ToArray();
            TerritoryCatalogIdentity identity = TerritoryPhaseBPlanner.CreateIdentity(
                TerritoryContractPlanner.CurrentCatalogId,
                1,
                1,
                "territory-source-v1",
                definitionRows,
                rewardRows,
                aliasRows);
            return new TerritoryPhaseBCatalog(identity, definitionRows, rewardRows, aliasRows);
        }

        private static string[] DiagnosticKeys(IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            return diagnostics.Select(item => item.Severity + "|" + item.Code + "|" + item.TerritoryId + "|" + item.Message).ToArray();
        }

        private static void AssertDiagnostic(IEnumerable<TerritoryDiagnostic> diagnostics, string code, string territoryId = null)
        {
            Assert.True(
                diagnostics.Any(item => item.Code == code && (territoryId == null || item.TerritoryId == territoryId)),
                "Expected diagnostic " + code + (territoryId == null ? string.Empty : " for " + territoryId) + ".");
        }

        private static void AssertLowerHash(string value)
        {
            Assert.AreEqual(64, value.Length);
            Assert.True(value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')));
        }

        private static readonly RealmId[] PlayableRealms =
        {
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Crownlands,
            RealmId.Umbral
        };

        private const string ProfileSessionId = "profile-session-1";
        private const long AuthorizationEvaluationUtcTicks = 638900000000000000L;

        private sealed class CountingDiagnosticsEnumerable : IEnumerable<TerritoryDiagnostic>
        {
            public int MoveNextCalls { get; private set; }

            public IEnumerator<TerritoryDiagnostic> GetEnumerator()
            {
                int index = 0;
                while (true)
                {
                    MoveNextCalls++;
                    yield return new TerritoryDiagnostic(
                        TerritoryDiagnosticSeverity.Error,
                        "Hostile" + index.ToString("D6"),
                        string.Empty,
                        "hostile");
                    index++;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class CountingDefinitionsEnumerable : IEnumerable<TerritoryDefinition>
        {
            public int MoveNextCalls { get; private set; }

            public IEnumerator<TerritoryDefinition> GetEnumerator()
            {
                int index = 0;
                while (true)
                {
                    MoveNextCalls++;
                    yield return Definition(
                        "bounded-" + index.ToString("D4"),
                        "territory.bounded_" + index.ToString("D4"));
                    index++;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class CountingIncomeEnumerable : IEnumerable<TerritoryIncomeContribution>
        {
            public int MoveNextCalls { get; private set; }

            public IEnumerator<TerritoryIncomeContribution> GetEnumerator()
            {
                int index = 0;
                while (true)
                {
                    MoveNextCalls++;
                    yield return new TerritoryIncomeContribution(
                        "income-" + index.ToString("D4"),
                        RealmId.Stonehold,
                        ResourceType.Stone,
                        1);
                    index++;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class FakeCandidate : ITerritoryCandidateApplyTarget
        {
            public TerritoryApplyStepStatus OwnershipStatus = TerritoryApplyStepStatus.Applied;
            public TerritoryApplyStepStatus ReceiptStatus = TerritoryApplyStepStatus.Applied;
            public TerritoryApplyStepStatus OutboxStatus = TerritoryApplyStepStatus.Applied;
            public TerritoryCommitStatus CommitStatus = TerritoryCommitStatus.Committed;
            public bool RollbackSucceeds = true;
            public bool ThrowOwnership;
            public bool ThrowReceipt;
            public bool ThrowOutbox;
            public bool ThrowCommit;
            public bool ThrowRollback;
            public RealmId CurrentOwner = RealmId.None;
            public long CurrentRevision;

            private readonly HashSet<string> _committedOperations = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _committedResults = new HashSet<string>(StringComparer.Ordinal);
            private TerritoryCaptureTransactionPlan _stagedPlan;
            private TerritoryCaptureReceipt _stagedReceipt;
            private TerritoryCaptureCommittedEvent _stagedEvent;

            public int OwnershipCalls { get; private set; }
            public int ReceiptCalls { get; private set; }
            public int OutboxCalls { get; private set; }
            public int CommitCalls { get; private set; }
            public int RollbackCalls { get; private set; }
            public int TotalCalls => OwnershipCalls + ReceiptCalls + OutboxCalls + CommitCalls + RollbackCalls;

            public TerritoryApplyStepStatus ApplyOwnership(TerritoryCaptureTransactionPlan plan)
            {
                OwnershipCalls++;
                if (ThrowOwnership)
                {
                    throw new InvalidOperationException("ownership");
                }

                if (OwnershipStatus != TerritoryApplyStepStatus.Applied)
                {
                    return OwnershipStatus;
                }

                if (plan?.CapturePlan == null ||
                    _committedOperations.Contains(plan.CapturePlan.OperationId) ||
                    _committedResults.Contains(plan.ResultId) ||
                    plan.CapturePlan.PreviousOwner != CurrentOwner ||
                    plan.CapturePlan.PreviousRevision != CurrentRevision)
                {
                    return TerritoryApplyStepStatus.Rejected;
                }

                _stagedPlan = plan;
                return OwnershipStatus;
            }

            public TerritoryApplyStepStatus ApplyReceipt(TerritoryCaptureReceipt receipt)
            {
                ReceiptCalls++;
                if (ThrowReceipt)
                {
                    throw new InvalidOperationException("receipt");
                }

                if (ReceiptStatus == TerritoryApplyStepStatus.Applied &&
                    _stagedPlan != null &&
                    string.Equals(receipt?.OperationId, _stagedPlan.CapturePlan.OperationId, StringComparison.Ordinal) &&
                    string.Equals(receipt?.ResultId, _stagedPlan.ResultId, StringComparison.Ordinal))
                {
                    _stagedReceipt = receipt;
                }

                return ReceiptStatus;
            }

            public TerritoryApplyStepStatus ApplyOutbox(TerritoryCaptureCommittedEvent committedEvent)
            {
                OutboxCalls++;
                if (ThrowOutbox)
                {
                    throw new InvalidOperationException("outbox");
                }

                if (OutboxStatus == TerritoryApplyStepStatus.Applied &&
                    _stagedPlan != null &&
                    string.Equals(committedEvent?.CaptureOperationId, _stagedPlan.CapturePlan.OperationId, StringComparison.Ordinal))
                {
                    _stagedEvent = committedEvent;
                }

                return OutboxStatus;
            }

            public TerritoryCommitStatus Commit(TerritoryCaptureTransactionPlan plan)
            {
                CommitCalls++;
                if (ThrowCommit)
                {
                    throw new InvalidOperationException("commit");
                }

                if (CommitStatus == TerritoryCommitStatus.Committed)
                {
                    if (!ReferenceEquals(plan, _stagedPlan) ||
                        _stagedReceipt == null ||
                        _stagedEvent == null)
                    {
                        return TerritoryCommitStatus.Rejected;
                    }

                    CurrentOwner = plan.CapturePlan.NewOwner;
                    CurrentRevision = plan.CapturePlan.NewRevision;
                    _committedOperations.Add(plan.CapturePlan.OperationId);
                    _committedResults.Add(plan.ResultId);
                    ClearStage();
                }

                return CommitStatus;
            }

            public bool Rollback(TerritoryCaptureTransactionPlan plan)
            {
                RollbackCalls++;
                if (ThrowRollback)
                {
                    throw new InvalidOperationException("rollback");
                }

                if (RollbackSucceeds)
                {
                    ClearStage();
                }

                return RollbackSucceeds;
            }

            private void ClearStage()
            {
                _stagedPlan = null;
                _stagedReceipt = null;
                _stagedEvent = null;
            }
        }

        private sealed class FakeEconomy : ITerritoryEconomyApplyTarget
        {
            public FakeEconomy(int initialCredits = 0)
            {
                AppliedCredits = initialCredits;
            }

            public TerritoryApplyStepStatus ApplyStatus = TerritoryApplyStepStatus.Applied;
            public bool RollbackSucceeds = true;
            public bool ThrowApply;
            public bool ThrowRollback;
            public int ApplyCalls { get; private set; }
            public int RollbackCalls { get; private set; }
            public int AppliedCredits { get; private set; }
            public int TotalCalls => ApplyCalls + RollbackCalls;
            private string _appliedOperationId;
            private int _appliedDelta;

            public TerritoryApplyStepStatus Apply(TerritoryEconomyCommand command)
            {
                ApplyCalls++;
                if (ThrowApply)
                {
                    throw new InvalidOperationException("economy-apply");
                }

                if (ApplyStatus == TerritoryApplyStepStatus.Applied)
                {
                    try
                    {
                        AppliedCredits = checked(AppliedCredits + command.WarzoneCreditsDelta);
                    }
                    catch (OverflowException)
                    {
                        return TerritoryApplyStepStatus.Rejected;
                    }

                    _appliedOperationId = command.OperationId;
                    _appliedDelta = command.WarzoneCreditsDelta;
                }

                return ApplyStatus;
            }

            public bool Rollback(TerritoryEconomyCommand command)
            {
                RollbackCalls++;
                if (ThrowRollback)
                {
                    throw new InvalidOperationException("economy-rollback");
                }

                if (RollbackSucceeds &&
                    string.Equals(_appliedOperationId, command.OperationId, StringComparison.Ordinal) &&
                    _appliedDelta == command.WarzoneCreditsDelta)
                {
                    AppliedCredits -= command.WarzoneCreditsDelta;
                    _appliedOperationId = null;
                    _appliedDelta = 0;
                }

                return RollbackSucceeds;
            }
        }

        private sealed class FakeQuest : ITerritoryQuestApplyTarget
        {
            public FakeQuest(int initialProgress = 0)
            {
                AppliedProgress = initialProgress;
            }

            public TerritoryApplyStepStatus ApplyStatus = TerritoryApplyStepStatus.Applied;
            public bool RollbackSucceeds = true;
            public bool ThrowApply;
            public bool ThrowRollback;
            public int ApplyCalls { get; private set; }
            public int RollbackCalls { get; private set; }
            public int AppliedProgress { get; private set; }
            public int TotalCalls => ApplyCalls + RollbackCalls;
            private string _appliedOperationId;
            private int _appliedDelta;

            public TerritoryApplyStepStatus Apply(TerritoryQuestCommand command)
            {
                ApplyCalls++;
                if (ThrowApply)
                {
                    throw new InvalidOperationException("quest-apply");
                }

                if (ApplyStatus == TerritoryApplyStepStatus.Applied)
                {
                    try
                    {
                        AppliedProgress = checked(AppliedProgress + command.ProgressDelta);
                    }
                    catch (OverflowException)
                    {
                        return TerritoryApplyStepStatus.Rejected;
                    }

                    _appliedOperationId = command.OperationId;
                    _appliedDelta = command.ProgressDelta;
                }

                return ApplyStatus;
            }

            public bool Rollback(TerritoryQuestCommand command)
            {
                RollbackCalls++;
                if (ThrowRollback)
                {
                    throw new InvalidOperationException("quest-rollback");
                }

                if (RollbackSucceeds &&
                    string.Equals(_appliedOperationId, command.OperationId, StringComparison.Ordinal) &&
                    _appliedDelta == command.ProgressDelta)
                {
                    AppliedProgress -= command.ProgressDelta;
                    _appliedOperationId = null;
                    _appliedDelta = 0;
                }

                return RollbackSucceeds;
            }
        }
    }
}
