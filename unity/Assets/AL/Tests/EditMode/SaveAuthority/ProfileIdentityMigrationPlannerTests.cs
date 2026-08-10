using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.Core.SaveAuthority;
using NUnit.Framework;

namespace AL.Tests.EditMode.SaveAuthority
{
    public sealed class ProfileIdentityMigrationPlannerTests
    {
        private const string ProfileA =
            "alp_0123456789abcdef0123456789abcdef";
        private const string ProfileB =
            "alp_1123456789abcdef0123456789abcdef";
        private const string ProfileC =
            "alp_2123456789abcdef0123456789abcdef";
        private const string PredecessorHash =
            "1111111111111111111111111111111111111111111111111111111111111111";
        private const string CandidateHash =
            "2222222222222222222222222222222222222222222222222222222222222222";
        private const string WitnessHash =
            "3333333333333333333333333333333333333333333333333333333333333333";

        [Test]
        public void CoherentSchemaOneEvidenceProducesExactMigrationPlan()
        {
            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(ProfileAuthoritySourceGeneration.Backup);
            var identity = new FixedIdentitySource(ProfileA);
            var projection = new ValidProjectionSource();

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        LegacyEvidence(predecessor),
                        identity,
                        projection));

            Assert.AreEqual(
                ProfileIdentityMigrationPlanStatus.MigrationReady,
                plan.Status);
            Assert.AreEqual(
                ProfileIdentityMigrationMode.MigrateSchemaOne,
                plan.Mode);
            Assert.AreEqual(ProfileA, plan.ProfileId);
            Assert.AreEqual(1, plan.IdentityAttemptCount);
            Assert.AreEqual(1, identity.CallCount);
            Assert.AreEqual(1, projection.CallCount);
            AssertGenerationEqual(predecessor, plan.Predecessor);
            Assert.AreEqual(ProfileA, plan.Candidate.ProfileId);
            Assert.AreEqual(CandidateHash, plan.Candidate.Sha256);
            Assert.AreEqual(ProfileA, plan.Witness.ProfileId);
            Assert.AreEqual(WitnessHash, plan.Witness.Sha256);
            Assert.AreEqual(
                ProfileIdentityMigrationTechnicalLimits.ContractVersion,
                plan.Witness.ContractVersion);
            Assert.AreEqual(
                ProfileAuthoritySourceGeneration.Backup,
                plan.Witness.SelectedLegacySourceGeneration);
            Assert.AreEqual(0, plan.DiagnosticCodes.Count);
        }

        [Test]
        public void AllMissingEvidenceProducesDistinctCreationPlan()
        {
            var identity = new FixedIdentitySource(ProfileA);
            var projection = new ValidProjectionSource();

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(AllMissingEvidence(), identity, projection));

            Assert.AreEqual(
                ProfileIdentityMigrationPlanStatus.CreationReady,
                plan.Status);
            Assert.AreEqual(
                ProfileIdentityMigrationMode.CreateNewProfile,
                plan.Mode);
            Assert.AreEqual(ProfileA, plan.ProfileId);
            Assert.IsFalse(plan.Predecessor.IsPresent);
            Assert.IsTrue(plan.Candidate.IsPresent);
            Assert.IsFalse(plan.Witness.IsPresent);
            Assert.AreEqual(1, identity.CallCount);
            Assert.AreEqual(1, projection.CallCount);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("alp_00000000000000000000000000000000")]
        [TestCase("alp_0123456789ABCDEF0123456789abcdef")]
        [TestCase("ALP_0123456789abcdef0123456789abcdef")]
        [TestCase("alp-0123456789abcdef0123456789abcdef")]
        [TestCase("alp_0123456789abcdef0123456789abcde")]
        [TestCase("alp_0123456789abcdef0123456789abcdef0")]
        [TestCase("alp_0123456789abcdef0123456789abcdeg")]
        public void InvalidIdentityCandidateRejectsWithoutProjection(
            string candidate)
        {
            var identity = new FixedIdentitySource(candidate);
            var projection = new ValidProjectionSource();

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(AllMissingEvidence(), identity, projection));

            AssertRejected(
                plan,
                ProfileIdentityMigrationDiagnosticCodes.IdentityCandidateInvalid);
            Assert.AreEqual(1, identity.CallCount);
            Assert.AreEqual(0, projection.CallCount);
        }

        [Test]
        public void RetiredCollisionsRetryAndEighthAttemptCanSucceed()
        {
            string[] candidates = Enumerable.Repeat(ProfileA, 7)
                .Concat(new[] { ProfileB })
                .ToArray();
            var identity = new FixedIdentitySource(candidates);
            var projection = new ValidProjectionSource();

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        identity,
                        projection,
                        new[] { ProfileA }));

            Assert.AreEqual(
                ProfileIdentityMigrationPlanStatus.CreationReady,
                plan.Status);
            Assert.AreEqual(ProfileB, plan.ProfileId);
            Assert.AreEqual(8, plan.IdentityAttemptCount);
            Assert.AreEqual(8, identity.CallCount);
            CollectionAssert.AreEqual(
                Enumerable.Range(1, 8),
                identity.AttemptNumbers);
        }

        [Test]
        public void EightCollisionsExhaustWithoutProjection()
        {
            var identity = new FixedIdentitySource(
                Enumerable.Repeat(ProfileA, 8).ToArray());
            var projection = new ValidProjectionSource();

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        identity,
                        projection,
                        new[] { ProfileA }));

            AssertRejected(
                plan,
                ProfileIdentityMigrationDiagnosticCodes.IdentityExhausted);
            Assert.AreEqual(8, plan.IdentityAttemptCount);
            Assert.AreEqual(8, identity.CallCount);
            Assert.AreEqual(0, projection.CallCount);
        }

        [TestCase(ProfileIdentityMigrationEvidenceState.ForwardSchema)]
        [TestCase(ProfileIdentityMigrationEvidenceState.Degraded)]
        [TestCase(ProfileIdentityMigrationEvidenceState.Malformed)]
        [TestCase(ProfileIdentityMigrationEvidenceState.Inaccessible)]
        [TestCase(ProfileIdentityMigrationEvidenceState.RecoveryRequired)]
        [TestCase(ProfileIdentityMigrationEvidenceState.CommitUncertain)]
        [TestCase(ProfileIdentityMigrationEvidenceState.RepairRequiresDataChange)]
        [TestCase(ProfileIdentityMigrationEvidenceState.BothInvalid)]
        [TestCase(ProfileIdentityMigrationEvidenceState.IdentityConflict)]
        public void UnsupportedEvidenceRejectsBeforeProviders(
            ProfileIdentityMigrationEvidenceState state)
        {
            var identity = new FixedIdentitySource(ProfileA);
            var projection = new ValidProjectionSource();
            var evidence = new ProfileIdentityMigrationEvidence(
                state,
                false,
                ProfileAuthoritySourceGeneration.None,
                ProfileIdentityGenerationExpectation.Missing(),
                Array.Empty<string>());

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(evidence, identity, projection));

            AssertRejected(
                plan,
                ProfileIdentityMigrationDiagnosticCodes.EvidenceUnsupported);
            Assert.AreEqual(0, identity.CallCount);
            Assert.AreEqual(0, projection.CallCount);
        }

        [Test]
        public void InvalidOrConflictingEligibleEvidenceRejectsBeforeProviders()
        {
            ProfileIdentityMigrationEvidence[] invalid =
            {
                new ProfileIdentityMigrationEvidence(
                    ProfileIdentityMigrationEvidenceState.AllMissing,
                    true,
                    ProfileAuthoritySourceGeneration.Primary,
                    LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary),
                    Array.Empty<string>()),
                new ProfileIdentityMigrationEvidence(
                    ProfileIdentityMigrationEvidenceState.CoherentSchemaOne,
                    false,
                    ProfileAuthoritySourceGeneration.None,
                    ProfileIdentityGenerationExpectation.Missing(),
                    Array.Empty<string>()),
                new ProfileIdentityMigrationEvidence(
                    ProfileIdentityMigrationEvidenceState.CoherentSchemaOne,
                    true,
                    ProfileAuthoritySourceGeneration.Backup,
                    LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary),
                    Array.Empty<string>()),
                new ProfileIdentityMigrationEvidence(
                    ProfileIdentityMigrationEvidenceState.CoherentSchemaOne,
                    true,
                    ProfileAuthoritySourceGeneration.Primary,
                    LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary),
                    new[] { ProfileC }),
                new ProfileIdentityMigrationEvidence(
                    ProfileIdentityMigrationEvidenceState.AllMissing,
                    false,
                    ProfileAuthoritySourceGeneration.None,
                    ProfileIdentityGenerationExpectation.Missing(),
                    new[] { ProfileC })
            };

            foreach (ProfileIdentityMigrationEvidence evidence in invalid)
            {
                var identity = new FixedIdentitySource(ProfileA);
                var projection = new ValidProjectionSource();
                ProfileIdentityMigrationPlan plan =
                    ProfileIdentityMigrationPlanner.Plan(
                        Request(evidence, identity, projection));

                AssertRejected(
                    plan,
                    ProfileIdentityMigrationDiagnosticCodes.EvidenceInvalid);
                Assert.AreEqual(0, identity.CallCount);
                Assert.AreEqual(0, projection.CallCount);
            }
        }

        [Test]
        public void DuplicateInvalidOverflowAndFaultingIdentitySetsFailClosed()
        {
            ProfileIdentityMigrationPlan nullSetPlan =
                ProfileIdentityMigrationPlanner.Plan(
                    new ProfileIdentityMigrationRequest(
                        AllMissingEvidence(),
                        null,
                        new FixedIdentitySource(ProfileB),
                        new ValidProjectionSource()));
            AssertRejected(
                nullSetPlan,
                ProfileIdentityMigrationDiagnosticCodes.IdentitySetInvalid);

            IEnumerable<string>[] invalidSets =
            {
                new[] { ProfileA, ProfileA },
                new[] { ProfileA, "profile-invalid" },
                Enumerable.Range(
                        0,
                        ProfileIdentityMigrationTechnicalLimits
                            .MaximumRetainedIdentityCount + 1)
                    .Select(ProfileForIndex),
                new FaultingEnumerable<string>(
                    new InvalidOperationException("hostile identities"))
            };

            foreach (IEnumerable<string> invalidSet in invalidSets)
            {
                var identity = new FixedIdentitySource(ProfileB);
                var projection = new ValidProjectionSource();
                ProfileIdentityMigrationPlan plan =
                    ProfileIdentityMigrationPlanner.Plan(
                        Request(
                            AllMissingEvidence(),
                            identity,
                            projection,
                            invalidSet));

                AssertRejected(
                    plan,
                    ProfileIdentityMigrationDiagnosticCodes.IdentitySetInvalid);
                Assert.AreEqual(0, identity.CallCount);
                Assert.AreEqual(0, projection.CallCount);
            }
        }

        [Test]
        public void ReorderedIdentityInputsAndRepeatedPlanningAreDeterministic()
        {
            string[] forward = { ProfileB, ProfileA };
            string[] reverse = forward.Reverse().ToArray();

            ProfileIdentityMigrationPlan first =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        new FixedIdentitySource(ProfileA, ProfileB, ProfileC),
                        new ValidProjectionSource(),
                        forward));
            ProfileIdentityMigrationPlan second =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        new FixedIdentitySource(ProfileA, ProfileB, ProfileC),
                        new ValidProjectionSource(),
                        reverse));
            ProfileIdentityMigrationRequest repeatRequest = Request(
                AllMissingEvidence(),
                new FixedIdentitySource(ProfileA, ProfileB, ProfileC),
                new ValidProjectionSource(),
                forward);
            ProfileIdentityMigrationPlan repeatedOne =
                ProfileIdentityMigrationPlanner.Plan(repeatRequest);
            ProfileIdentityMigrationPlan repeatedTwo =
                ProfileIdentityMigrationPlanner.Plan(repeatRequest);

            AssertPlanEqual(first, second);
            AssertPlanEqual(repeatedOne, repeatedTwo);
            CollectionAssert.AreEqual(
                new[] { ProfileB, ProfileA },
                forward,
                "Planner must not reorder caller input.");
        }

        [Test]
        public void NullAndHostileProvidersFailClosed()
        {
            AssertRejected(
                ProfileIdentityMigrationPlanner.Plan(null),
                ProfileIdentityMigrationDiagnosticCodes.RequestMissing);

            AssertRejected(
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        null,
                        new ValidProjectionSource())),
                ProfileIdentityMigrationDiagnosticCodes.IdentitySourceMissing);

            AssertRejected(
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        new ThrowingIdentitySource(),
                        new ValidProjectionSource())),
                ProfileIdentityMigrationDiagnosticCodes.IdentitySourceThrew);

            AssertRejected(
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        new FixedIdentitySource(ProfileA),
                        null)),
                ProfileIdentityMigrationDiagnosticCodes.ProjectionSourceMissing);

            AssertRejected(
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        new FixedIdentitySource(ProfileA),
                        new ThrowingProjectionSource())),
                ProfileIdentityMigrationDiagnosticCodes.ProjectionSourceThrew);
        }

        [Test]
        public void ProjectionMustPinExactCreationAndMigrationAuthority()
        {
            ProfileIdentityMigrationProjection[] invalidCreation =
            {
                null,
                new ProfileIdentityMigrationProjection(
                    LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary),
                    CurrentCandidate(ProfileA),
                    ProfileIdentityMigrationWitnessExpectation.Missing()),
                new ProfileIdentityMigrationProjection(
                    ProfileIdentityGenerationExpectation.Missing(),
                    CurrentCandidate(ProfileB),
                    ProfileIdentityMigrationWitnessExpectation.Missing()),
                new ProfileIdentityMigrationProjection(
                    ProfileIdentityGenerationExpectation.Missing(),
                    CurrentCandidate(ProfileA),
                    MigrationWitness(
                        ProfileAuthoritySourceGeneration.Primary,
                        ProfileA))
            };

            foreach (ProfileIdentityMigrationProjection projectionValue in
                     invalidCreation)
            {
                AssertProjectionRejected(
                    AllMissingEvidence(),
                    projectionValue);
            }

            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(ProfileAuthoritySourceGeneration.Backup);
            ProfileIdentityMigrationProjection[] invalidMigration =
            {
                null,
                new ProfileIdentityMigrationProjection(
                    LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary),
                    CurrentCandidate(ProfileA),
                    MigrationWitness(
                        ProfileAuthoritySourceGeneration.Backup,
                        ProfileA)),
                new ProfileIdentityMigrationProjection(
                    predecessor,
                    CurrentCandidate(ProfileB),
                    MigrationWitness(
                        ProfileAuthoritySourceGeneration.Backup,
                        ProfileA)),
                new ProfileIdentityMigrationProjection(
                    predecessor,
                    CurrentCandidate(ProfileA),
                    ProfileIdentityMigrationWitnessExpectation.Missing()),
                new ProfileIdentityMigrationProjection(
                    predecessor,
                    CurrentCandidate(ProfileA),
                    MigrationWitness(
                        ProfileAuthoritySourceGeneration.Primary,
                        ProfileA)),
                new ProfileIdentityMigrationProjection(
                    predecessor,
                    CurrentCandidate(ProfileA),
                    new ProfileIdentityMigrationWitnessExpectation(
                        true,
                        ProfileIdentityMigrationTechnicalLimits.ContractVersion,
                        "migration.operation.001",
                        ProfileAuthoritySourceGeneration.Backup,
                        predecessor.ByteCount,
                        predecessor.Sha256,
                        ProfileA,
                        SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                        SaveAuthorityTechnicalLimits
                            .IdentityAwareProfileInitializationVersion,
                        998,
                        CandidateHash,
                        222,
                        WitnessHash))
            };

            foreach (ProfileIdentityMigrationProjection projectionValue in
                     invalidMigration)
            {
                AssertProjectionRejected(
                    LegacyEvidence(predecessor),
                    projectionValue);
            }
        }

        [Test]
        public void SuccessfulPlanIsDetachedFromProviderAndCallerCollections()
        {
            string[] retired = { ProfileB };
            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary);
            var source = new MutableProjectionSource();

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        LegacyEvidence(predecessor),
                        new FixedIdentitySource(ProfileA),
                        source,
                        retired));

            retired[0] = ProfileC;
            source.ProfileId = ProfileC;
            source.CandidateHash = WitnessHash;

            Assert.AreEqual(ProfileA, plan.ProfileId);
            Assert.AreEqual(ProfileA, plan.Candidate.ProfileId);
            Assert.AreEqual(CandidateHash, plan.Candidate.Sha256);
            Assert.AreEqual(ProfileA, plan.Witness.ProfileId);
        }

        [Test]
        public void MaximumRetainedSetRemainsBoundedAndUsesOneAttempt()
        {
            string[] retained = Enumerable.Range(
                    1,
                    ProfileIdentityMigrationTechnicalLimits
                        .MaximumRetainedIdentityCount)
                .Select(ProfileForIndex)
                .ToArray();
            string unique = "alp_ffffffffffffffffffffffffffffffff";
            var identity = new FixedIdentitySource(unique);

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        AllMissingEvidence(),
                        identity,
                        new ValidProjectionSource(),
                        retained));

            Assert.AreEqual(
                ProfileIdentityMigrationPlanStatus.CreationReady,
                plan.Status);
            Assert.AreEqual(unique, plan.ProfileId);
            Assert.AreEqual(1, identity.CallCount);
        }

        [Test]
        public void ExactOneMibibyteArtifactCapIsAcceptedForAllThreeRoles()
        {
            long cap = ProfileIdentityMigrationTechnicalLimits
                .MaximumArtifactByteCount;
            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(
                    ProfileAuthoritySourceGeneration.Primary,
                    cap);
            ProfileIdentityGenerationExpectation candidate =
                CurrentCandidate(ProfileA, cap);
            var projection = new ProfileIdentityMigrationProjection(
                predecessor,
                candidate,
                MigrationWitness(
                    ProfileAuthoritySourceGeneration.Primary,
                    ProfileA,
                    cap,
                    cap,
                    cap));

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        LegacyEvidence(predecessor),
                        new FixedIdentitySource(ProfileA),
                        new FixedProjectionSource(projection)));

            Assert.AreEqual(1024L * 1024L, cap);
            Assert.AreEqual(
                ProfileIdentityMigrationPlanStatus.MigrationReady,
                plan.Status);
            Assert.AreEqual(cap, plan.Predecessor.ByteCount);
            Assert.AreEqual(cap, plan.Candidate.ByteCount);
            Assert.AreEqual(cap, plan.Witness.ByteCount);
        }

        [Test]
        public void CapPlusOnePredecessorRejectsBeforeBothProviders()
        {
            long overCap = ProfileIdentityMigrationTechnicalLimits
                .MaximumArtifactByteCount + 1;
            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(
                    ProfileAuthoritySourceGeneration.Primary,
                    overCap);
            var identity = new FixedIdentitySource(ProfileA);
            var projection = new ValidProjectionSource();

            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        LegacyEvidence(predecessor),
                        identity,
                        projection));

            AssertRejected(
                plan,
                ProfileIdentityMigrationDiagnosticCodes.EvidenceInvalid);
            Assert.AreEqual(0, identity.CallCount);
            Assert.AreEqual(0, projection.CallCount);
        }

        [Test]
        public void CapPlusOneCandidateRejectsExactProjection()
        {
            long overCap = ProfileIdentityMigrationTechnicalLimits
                .MaximumArtifactByteCount + 1;
            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary);
            ProfileIdentityGenerationExpectation candidate =
                CurrentCandidate(ProfileA, overCap);
            var projection = new ProfileIdentityMigrationProjection(
                predecessor,
                candidate,
                MigrationWitness(
                    ProfileAuthoritySourceGeneration.Primary,
                    ProfileA,
                    predecessor.ByteCount,
                    overCap,
                    222));

            AssertProjectionRejected(
                LegacyEvidence(predecessor),
                projection);
        }

        [Test]
        public void CapPlusOneWitnessRejectsExactProjection()
        {
            long overCap = ProfileIdentityMigrationTechnicalLimits
                .MaximumArtifactByteCount + 1;
            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(ProfileAuthoritySourceGeneration.Primary);
            var projection = new ProfileIdentityMigrationProjection(
                predecessor,
                CurrentCandidate(ProfileA),
                MigrationWitness(
                    ProfileAuthoritySourceGeneration.Primary,
                    ProfileA,
                    predecessor.ByteCount,
                    999,
                    overCap));

            AssertProjectionRejected(
                LegacyEvidence(predecessor),
                projection);
        }

        private static ProfileIdentityMigrationRequest Request(
            ProfileIdentityMigrationEvidence evidence,
            IProfileIdentityCandidateSource identity,
            IProfileIdentityMigrationProjectionSource projection,
            IEnumerable<string> retired = null) =>
            new ProfileIdentityMigrationRequest(
                evidence,
                retired ?? Array.Empty<string>(),
                identity,
                projection);

        private static ProfileIdentityMigrationEvidence AllMissingEvidence() =>
            new ProfileIdentityMigrationEvidence(
                ProfileIdentityMigrationEvidenceState.AllMissing,
                false,
                ProfileAuthoritySourceGeneration.None,
                ProfileIdentityGenerationExpectation.Missing(),
                Array.Empty<string>());

        private static ProfileIdentityMigrationEvidence LegacyEvidence(
            ProfileIdentityGenerationExpectation predecessor) =>
            new ProfileIdentityMigrationEvidence(
                ProfileIdentityMigrationEvidenceState.CoherentSchemaOne,
                true,
                predecessor.SourceGeneration,
                predecessor,
                Array.Empty<string>());

        private static ProfileIdentityGenerationExpectation LegacyPredecessor(
            ProfileAuthoritySourceGeneration source,
            long byteCount = 777) =>
            ProfileIdentityGenerationExpectation.Exact(
                source,
                byteCount,
                PredecessorHash,
                string.Empty,
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                SaveAuthorityTechnicalLimits.LegacyProfileInitializationVersion);

        private static ProfileIdentityGenerationExpectation CurrentCandidate(
            string profileId,
            long byteCount = 999) =>
            ProfileIdentityGenerationExpectation.Exact(
                ProfileAuthoritySourceGeneration.Primary,
                byteCount,
                CandidateHash,
                profileId,
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion);

        private static ProfileIdentityMigrationWitnessExpectation
            MigrationWitness(
                ProfileAuthoritySourceGeneration source,
                string profileId,
                long predecessorByteCount = 777,
                long candidateByteCount = 999,
                long witnessByteCount = 222)
        {
            ProfileIdentityGenerationExpectation predecessor =
                LegacyPredecessor(source, predecessorByteCount);
            return new ProfileIdentityMigrationWitnessExpectation(
                true,
                ProfileIdentityMigrationTechnicalLimits.ContractVersion,
                "migration.operation.001",
                source,
                predecessor.ByteCount,
                predecessor.Sha256,
                profileId,
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion,
                candidateByteCount,
                CandidateHash,
                witnessByteCount,
                WitnessHash);
        }

        private static string ProfileForIndex(int index) =>
            "alp_" + index.ToString("x32");

        private static void AssertProjectionRejected(
            ProfileIdentityMigrationEvidence evidence,
            ProfileIdentityMigrationProjection projection)
        {
            ProfileIdentityMigrationPlan plan =
                ProfileIdentityMigrationPlanner.Plan(
                    Request(
                        evidence,
                        new FixedIdentitySource(ProfileA),
                        new FixedProjectionSource(projection)));

            AssertRejected(
                plan,
                ProfileIdentityMigrationDiagnosticCodes.ProjectionInvalid);
        }

        private static void AssertRejected(
            ProfileIdentityMigrationPlan plan,
            string diagnostic)
        {
            Assert.NotNull(plan);
            Assert.AreEqual(
                ProfileIdentityMigrationPlanStatus.Rejected,
                plan.Status);
            Assert.AreEqual(ProfileIdentityMigrationMode.None, plan.Mode);
            Assert.AreEqual(string.Empty, plan.ProfileId);
            Assert.IsFalse(plan.Predecessor.IsPresent);
            Assert.IsFalse(plan.Candidate.IsPresent);
            Assert.IsFalse(plan.Witness.IsPresent);
            CollectionAssert.AreEqual(
                new[] { diagnostic },
                plan.DiagnosticCodes);
        }

        private static void AssertPlanEqual(
            ProfileIdentityMigrationPlan expected,
            ProfileIdentityMigrationPlan actual)
        {
            Assert.AreEqual(expected.Status, actual.Status);
            Assert.AreEqual(expected.Mode, actual.Mode);
            Assert.AreEqual(expected.ProfileId, actual.ProfileId);
            Assert.AreEqual(
                expected.IdentityAttemptCount,
                actual.IdentityAttemptCount);
            AssertGenerationEqual(expected.Predecessor, actual.Predecessor);
            AssertGenerationEqual(expected.Candidate, actual.Candidate);
            Assert.AreEqual(expected.Witness.IsPresent, actual.Witness.IsPresent);
            Assert.AreEqual(expected.Witness.ProfileId, actual.Witness.ProfileId);
            Assert.AreEqual(expected.Witness.Sha256, actual.Witness.Sha256);
            CollectionAssert.AreEqual(
                expected.DiagnosticCodes,
                actual.DiagnosticCodes);
        }

        private static void AssertGenerationEqual(
            ProfileIdentityGenerationExpectation expected,
            ProfileIdentityGenerationExpectation actual)
        {
            Assert.AreEqual(expected.IsPresent, actual.IsPresent);
            Assert.AreEqual(expected.SourceGeneration, actual.SourceGeneration);
            Assert.AreEqual(expected.ByteCount, actual.ByteCount);
            Assert.AreEqual(expected.Sha256, actual.Sha256);
            Assert.AreEqual(expected.ProfileId, actual.ProfileId);
            Assert.AreEqual(expected.SaveSchemaVersion, actual.SaveSchemaVersion);
            Assert.AreEqual(
                expected.ProfileInitializationVersion,
                actual.ProfileInitializationVersion);
        }

        private sealed class FixedIdentitySource :
            IProfileIdentityCandidateSource
        {
            private readonly string[] _values;

            internal FixedIdentitySource(params string[] values)
            {
                _values = values;
            }

            internal int CallCount { get; private set; }
            internal List<int> AttemptNumbers { get; } = new List<int>();

            public string GetCandidate(int attemptNumber)
            {
                CallCount++;
                AttemptNumbers.Add(attemptNumber);
                int index = Math.Min(attemptNumber - 1, _values.Length - 1);
                return index < 0 ? null : _values[index];
            }
        }

        private sealed class ThrowingIdentitySource :
            IProfileIdentityCandidateSource
        {
            public string GetCandidate(int attemptNumber) =>
                throw new InvalidOperationException("hostile identity source");
        }

        private class ValidProjectionSource :
            IProfileIdentityMigrationProjectionSource
        {
            internal int CallCount { get; private set; }

            public virtual ProfileIdentityMigrationProjection CreateProjection(
                ProfileIdentityMigrationMode mode,
                string profileId,
                ProfileIdentityMigrationEvidence evidence)
            {
                CallCount++;
                if (mode == ProfileIdentityMigrationMode.CreateNewProfile)
                {
                    return new ProfileIdentityMigrationProjection(
                        ProfileIdentityGenerationExpectation.Missing(),
                        CurrentCandidate(profileId),
                        ProfileIdentityMigrationWitnessExpectation.Missing());
                }

                return new ProfileIdentityMigrationProjection(
                    evidence.SelectedPredecessor,
                    CurrentCandidate(profileId),
                    MigrationWitness(
                        evidence.SelectedSourceGeneration,
                        profileId));
            }
        }

        private sealed class FixedProjectionSource :
            IProfileIdentityMigrationProjectionSource
        {
            private readonly ProfileIdentityMigrationProjection _projection;

            internal FixedProjectionSource(
                ProfileIdentityMigrationProjection projection)
            {
                _projection = projection;
            }

            public ProfileIdentityMigrationProjection CreateProjection(
                ProfileIdentityMigrationMode mode,
                string profileId,
                ProfileIdentityMigrationEvidence evidence) => _projection;
        }

        private sealed class ThrowingProjectionSource :
            IProfileIdentityMigrationProjectionSource
        {
            public ProfileIdentityMigrationProjection CreateProjection(
                ProfileIdentityMigrationMode mode,
                string profileId,
                ProfileIdentityMigrationEvidence evidence) =>
                throw new InvalidOperationException("hostile projection source");
        }

        private sealed class MutableProjectionSource :
            IProfileIdentityMigrationProjectionSource
        {
            internal string ProfileId { get; set; } = ProfileA;
            internal string CandidateHash { get; set; } =
                ProfileIdentityMigrationPlannerTests.CandidateHash;

            public ProfileIdentityMigrationProjection CreateProjection(
                ProfileIdentityMigrationMode mode,
                string profileId,
                ProfileIdentityMigrationEvidence evidence)
            {
                ProfileId = profileId;
                var candidate = ProfileIdentityGenerationExpectation.Exact(
                    ProfileAuthoritySourceGeneration.Primary,
                    999,
                    CandidateHash,
                    ProfileId,
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                    SaveAuthorityTechnicalLimits
                        .IdentityAwareProfileInitializationVersion);
                return new ProfileIdentityMigrationProjection(
                    evidence.SelectedPredecessor,
                    candidate,
                    MigrationWitness(
                        evidence.SelectedSourceGeneration,
                        ProfileId));
            }
        }

        private sealed class FaultingEnumerable<T> : IEnumerable<T>
        {
            private readonly Exception _exception;

            internal FaultingEnumerable(Exception exception)
            {
                _exception = exception;
            }

            public IEnumerator<T> GetEnumerator() => throw _exception;
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
