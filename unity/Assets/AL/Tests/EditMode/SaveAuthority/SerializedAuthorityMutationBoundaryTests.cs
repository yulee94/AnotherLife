using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using AL.Core.SaveAuthority;

namespace AL.Tests.EditMode.SaveAuthority
{
    public sealed class SerializedAuthorityMutationBoundaryTests
    {
        private const string ProfileId =
            "alp_0123456789abcdef0123456789abcdef";
        private const string InitialEpoch =
            "0123456789abcdef0000000000000001";
        private const string InitialFingerprint =
            "1111111111111111111111111111111111111111111111111111111111111111";

        [Test]
        public void MissingBoundaryDependenciesCannotRetainWritableAuthority()
        {
            ProfileWriteAuthoritySnapshot authority =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    ProfileId,
                    InitialEpoch,
                    InitialFingerprint,
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());
            var candidate = new MutableCandidate
            {
                ProfileId = ProfileId,
                VerifiedGenerationFingerprint = InitialFingerprint
            };
            SerializedAuthorityMutationBoundary<MutableCandidate>[] boundaries =
            {
                SerializedAuthorityMutationBoundary<MutableCandidate>
                    .CreateForTesting(
                        authority,
                        candidate,
                        null,
                        new FakePersistence(),
                        new AuthorityEpochAllocator(
                            new IncrementingEpochSource()),
                        new RecordingReceiptSink(),
                        null,
                        new ProcessAuthorityMutationCoordinator()),
                SerializedAuthorityMutationBoundary<MutableCandidate>
                    .CreateForTesting(
                        authority,
                        candidate,
                        new MutableCandidateAdapter(),
                        null,
                        new AuthorityEpochAllocator(
                            new IncrementingEpochSource()),
                        new RecordingReceiptSink(),
                        null,
                        new ProcessAuthorityMutationCoordinator()),
                SerializedAuthorityMutationBoundary<MutableCandidate>
                    .CreateForTesting(
                        authority,
                        candidate,
                        new MutableCandidateAdapter(),
                        new FakePersistence(),
                        null,
                        new RecordingReceiptSink(),
                        null,
                        new ProcessAuthorityMutationCoordinator()),
                SerializedAuthorityMutationBoundary<MutableCandidate>
                    .CreateForTesting(
                        authority,
                        candidate,
                        new MutableCandidateAdapter(),
                        new FakePersistence(),
                        new AuthorityEpochAllocator(
                            new IncrementingEpochSource()),
                        null,
                        null,
                        new ProcessAuthorityMutationCoordinator())
            };

            foreach (
                SerializedAuthorityMutationBoundary<MutableCandidate>
                    boundary in boundaries)
            {
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Unavailable,
                    boundary.GetCurrentAuthority().Status);
                Assert.AreEqual(
                    ProfileMutationStatus.NotWritable,
                    boundary.TryMutate(
                        ProfileAuthorityExpectation.From(
                            boundary.GetCurrentAuthority()),
                        "operation:missing-dependency",
                        "result-missing-dependency",
                        _ => ProfileCandidatePreparation.Prepared()).Status);
            }
        }

        [Test]
        public void MigrationRequiredRejectsBeforeCallbackOrPersistence()
        {
            var adapter = new ForbiddenCandidateAccessAdapter();
            var fixture = Fixture.Create(
                authority:
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Primary,
                    new[] { "AL-SAVE-AUTH-MIGRATION-REQUIRED" }),
                adapter: adapter,
                initialCandidate: new MutableCandidate
                {
                    ProfileId = "hostile-legacy-candidate",
                    VerifiedGenerationFingerprint = "not-a-fingerprint",
                    Value = 99
                });
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                ProfileAuthorityExpectation.From(fixture.Boundary.GetCurrentAuthority()),
                "operation:legacy",
                "result-legacy",
                candidate =>
                {
                    callbacks++;
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.NotWritable,
                result.Status);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(0, adapter.AccessCount);
            Assert.IsNull(ReadPublishedCandidate(fixture.Boundary));
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.MigrationRequired,
                fixture.Boundary.GetCurrentAuthority().Status);
        }

        [Test]
        public void DurableWriterContextExposesNoSessionEpochOrSequence()
        {
            System.Reflection.MethodInfo[] persistenceMethods =
                typeof(IProfileMutationPersistence<MutableCandidate>)
                    .GetMethods();
            CollectionAssert.AreEquivalent(
                new[] { "PersistAndVerify", "RecheckAuthority" },
                persistenceMethods.Select(method => method.Name));
            foreach (System.Reflection.MethodInfo method
                     in persistenceMethods)
            {
                foreach (System.Reflection.ParameterInfo parameter
                         in method.GetParameters())
                {
                    Assert.IsTrue(
                        parameter.ParameterType ==
                            typeof(MutableCandidate) ||
                        parameter.ParameterType ==
                            typeof(ProfileMutationCommitContext));
                    Assert.IsFalse(
                        parameter.Name.IndexOf(
                            "Epoch",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        parameter.Name.IndexOf(
                            "Sequence",
                            StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            string[] propertyNames =
                typeof(ProfileMutationCommitContext)
                    .GetProperties(
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic)
                    .Select(property => property.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "ExpectedGenerationFingerprint",
                    "ProfileId"
                },
                propertyNames);
            string[] fieldNames =
                typeof(ProfileMutationCommitContext)
                    .GetFields(
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic)
                    .Select(field => field.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "<ExpectedGenerationFingerprint>k__BackingField",
                    "<ProfileId>k__BackingField"
                },
                fieldNames);
            System.Reflection.ConstructorInfo[] constructors =
                typeof(ProfileMutationCommitContext)
                    .GetConstructors(
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
            Assert.AreEqual(1, constructors.Length);
            System.Reflection.ParameterInfo[] constructorParameters =
                constructors[0].GetParameters();
            CollectionAssert.AreEqual(
                new[] { typeof(string), typeof(string) },
                constructorParameters.Select(
                    parameter => parameter.ParameterType));
            CollectionAssert.AreEqual(
                new[]
                {
                    "profileId",
                    "expectedGenerationFingerprint"
                },
                constructorParameters.Select(parameter => parameter.Name));
            Assert.IsFalse(
                propertyNames.Concat(fieldNames)
                    .Concat(constructorParameters.Select(
                        parameter => parameter.Name))
                    .Any(
                        name => name.IndexOf(
                                    "Epoch",
                                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf(
                                    "Sequence",
                                    StringComparison.OrdinalIgnoreCase) >= 0));

            var fixture = Fixture.Create();
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                Commit(fixture, 1).Status);
            Assert.NotNull(fixture.Persistence.LastCommitContext);
            Assert.AreEqual(
                ProfileId,
                fixture.Persistence.LastCommitContext.ProfileId);
            Assert.NotNull(fixture.Persistence.LastRecheckContext);
            Assert.AreEqual(
                ProfileId,
                fixture.Persistence.LastRecheckContext.ProfileId);
            Assert.AreEqual(
                InitialFingerprint,
                fixture.Persistence.LastCommitContext
                    .ExpectedGenerationFingerprint);
            Assert.AreEqual(
                InitialFingerprint,
                fixture.Persistence.LastRecheckContext
                    .ExpectedGenerationFingerprint);
        }

        [Test]
        public void ExactAuthorityTripleRejectsEachStaleOrMalformedMemberBeforeWork()
        {
            var fixture = Fixture.Create();
            ProfileWriteAuthoritySnapshot current =
                fixture.Boundary.GetCurrentAuthority();
            var staleExpectations = new[]
            {
                new ProfileAuthorityExpectation(
                    "alp_1123456789abcdef0123456789abcdef",
                    current.AuthorityEpoch,
                    current.VerifiedGenerationFingerprint),
                new ProfileAuthorityExpectation(
                    current.ProfileId,
                    "0123456789abcdef0000000000000003",
                    current.VerifiedGenerationFingerprint),
                new ProfileAuthorityExpectation(
                    current.ProfileId,
                    current.AuthorityEpoch,
                    "2222222222222222222222222222222222222222222222222222222222222222")
            };
            var malformedExpectations = new[]
            {
                (ProfileAuthorityExpectation)null,
                ProfileAuthorityExpectation.From(null),
                new ProfileAuthorityExpectation(
                    "not-a-profile",
                    current.AuthorityEpoch,
                    current.VerifiedGenerationFingerprint),
                new ProfileAuthorityExpectation(
                    current.ProfileId,
                    "not-an-epoch",
                    current.VerifiedGenerationFingerprint),
                new ProfileAuthorityExpectation(
                    current.ProfileId,
                    current.AuthorityEpoch,
                    "not-a-fingerprint")
            };
            int callbacks = 0;

            foreach (ProfileAuthorityExpectation expectation
                     in staleExpectations)
            {
                ProfileMutationResult result = fixture.Boundary.TryMutate(
                    expectation,
                    "operation:stale-triple",
                    "result-stale-triple",
                    _ =>
                    {
                        callbacks++;
                        return ProfileCandidatePreparation.Prepared();
                    });
                Assert.AreEqual(
                    ProfileMutationStatus.StaleAuthority,
                    result.Status);
            }

            foreach (ProfileAuthorityExpectation expectation
                     in malformedExpectations)
            {
                ProfileMutationResult result = fixture.Boundary.TryMutate(
                    expectation,
                    "operation:malformed-triple",
                    "result-malformed-triple",
                    _ =>
                    {
                        callbacks++;
                        return ProfileCandidatePreparation.Prepared();
                    });
                Assert.AreEqual(
                    ProfileMutationStatus.Unavailable,
                    result.Status);
            }

            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, fixture.Persistence.CheckCount);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(
                0,
                ReadPublishedCandidate(fixture.Boundary).Value);
        }

        [Test]
        public void InvalidOpaqueMutationIdentitiesRejectBeforeAnyDownstreamWork()
        {
            string[] invalidIdentities =
            {
                null,
                string.Empty,
                "control\u0001",
                "\ud800",
                new string('a', 257),
                new string('한', 86)
            };
            var epochSource = new IncrementingEpochSource();
            var fixture = Fixture.Create(
                allocator: new AuthorityEpochAllocator(epochSource));
            int callbacks = 0;

            foreach (string invalid in invalidIdentities)
            {
                ProfileMutationResult invalidOperation =
                    fixture.Boundary.TryMutate(
                        CurrentExpectation(fixture),
                        invalid,
                        "result-valid",
                        _ =>
                        {
                            callbacks++;
                            return ProfileCandidatePreparation.Prepared();
                        });
                ProfileMutationResult invalidResult =
                    fixture.Boundary.TryMutate(
                        CurrentExpectation(fixture),
                        "operation:valid",
                        invalid,
                        _ =>
                        {
                            callbacks++;
                            return ProfileCandidatePreparation.Prepared();
                        });

                Assert.AreEqual(
                    ProfileMutationStatus.Unavailable,
                    invalidOperation.Status);
                Assert.AreEqual(
                    ProfileMutationStatus.Unavailable,
                    invalidResult.Status);
            }

            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, fixture.Persistence.CheckCount);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(1, epochSource.CallCount);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                fixture.Boundary.GetCurrentAuthority().Status);

            ProfileMutationResult valid = Commit(fixture, 1);
            Assert.AreEqual(ProfileMutationStatus.Committed, valid.Status);
            Assert.AreEqual(1UL, valid.Receipt.PublicationSequence);
            Assert.AreEqual(
                "0123456789abcdef0000000000000003",
                valid.Receipt.CommittedAuthorityEpoch);
        }

        [Test]
        public void TwoSameSnapshotCallersCannotLoseAnUpdate()
        {
            var fixture = Fixture.Create();
            ProfileAuthorityExpectation original =
                ProfileAuthorityExpectation.From(
                    fixture.Boundary.GetCurrentAuthority());
            var callbackEntered = new ManualResetEventSlim(false);
            var releaseCallback = new ManualResetEventSlim(false);

            Task<ProfileMutationResult> first = Task.Run(() =>
                fixture.Boundary.TryMutate(
                    original,
                    "operation:first",
                    "result-first",
                    candidate =>
                    {
                        callbackEntered.Set();
                        Assert.IsTrue(
                            releaseCallback.Wait(TimeSpan.FromSeconds(5)));
                        candidate.Value++;
                        return ProfileCandidatePreparation.Prepared();
                    }));

            Assert.IsTrue(callbackEntered.Wait(TimeSpan.FromSeconds(5)));
            int secondCallbacks = 0;
            ProfileMutationResult overlapping = fixture.Boundary.TryMutate(
                original,
                "operation:overlap",
                "result-overlap",
                candidate =>
                {
                    secondCallbacks++;
                    return ProfileCandidatePreparation.Prepared();
                });
            Assert.AreEqual(ProfileMutationStatus.Busy, overlapping.Status);
            Assert.AreEqual(0, secondCallbacks);

            releaseCallback.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(ProfileMutationStatus.Committed, first.Result.Status);

            ProfileMutationResult stale = fixture.Boundary.TryMutate(
                original,
                "operation:stale",
                "result-stale",
                candidate =>
                {
                    secondCallbacks++;
                    return ProfileCandidatePreparation.Prepared();
                });
            Assert.AreEqual(ProfileMutationStatus.StaleAuthority, stale.Status);
            Assert.AreEqual(0, secondCallbacks);

            ProfileMutationResult retry = fixture.Boundary.TryMutate(
                ProfileAuthorityExpectation.From(
                    fixture.Boundary.GetCurrentAuthority()),
                "operation:retry",
                "result-retry",
                candidate =>
                {
                    secondCallbacks++;
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });
            Assert.AreEqual(ProfileMutationStatus.Committed, retry.Status);
            Assert.AreEqual(1, secondCallbacks);
            Assert.AreEqual(2, fixture.Persistence.PersistCount);
            Assert.AreEqual(2, fixture.Persistence.LastPersistedValue);
            Assert.AreEqual(
                2,
                ReadPublishedCandidate(fixture.Boundary).Value);
        }

        [Test]
        public void CallbackReentrancyIsBusyEvenOnSameThread()
        {
            var fixture = Fixture.Create();
            ProfileMutationResult nested = null;
            int nestedCallbacks = 0;

            ProfileMutationResult outer = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:outer",
                "result-outer",
                candidate =>
                {
                    nested = fixture.Boundary.TryMutate(
                        CurrentExpectation(fixture),
                        "operation:nested",
                        "result-nested",
                        nestedCandidate =>
                        {
                            nestedCallbacks++;
                            return ProfileCandidatePreparation.Prepared();
                        });
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.NotNull(nested);
            Assert.AreEqual(ProfileMutationStatus.Busy, nested.Status);
            Assert.AreEqual(0, nestedCallbacks);
            Assert.AreEqual(ProfileMutationStatus.Committed, outer.Status);
            Assert.AreEqual(1, fixture.Persistence.PersistCount);
        }

        [Test]
        public void CallbackExceptionReleasesSlotButConsumesSequenceAndEpoch()
        {
            var fixture = Fixture.Create();

            ProfileMutationResult failed = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:throws",
                "result-throws",
                _ => throw new InvalidOperationException("callback"));
            ProfileMutationResult retry = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:retry",
                "result-retry",
                candidate =>
                {
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.PreparationFailed,
                failed.Status);
            Assert.IsNull(failed.Receipt);
            Assert.AreEqual(ProfileMutationStatus.Committed, retry.Status);
            Assert.AreEqual(2UL, retry.Receipt.PublicationSequence);
            Assert.AreEqual(
                "0123456789abcdef0000000000000004",
                retry.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(1, fixture.Persistence.PersistCount);
        }

        [Test]
        public void PreparationAndPreMutationPersistenceRejectionsReleaseSlots()
        {
            var fixture = Fixture.Create();

            ProfileMutationResult preparationRejected =
                fixture.Boundary.TryMutate(
                    CurrentExpectation(fixture),
                    "operation:preparation-reject",
                    "result-preparation-reject",
                    _ => ProfileCandidatePreparation.Rejected(
                        "AL-SAVE-AUTH-PREPARATION-REJECTED"));
            fixture.Persistence.NextOutcome =
                FakePersistenceOutcome.Rejected;
            ProfileMutationResult persistenceRejected =
                fixture.Boundary.TryMutate(
                    CurrentExpectation(fixture),
                    "operation:persistence-reject",
                    "result-persistence-reject",
                    candidate =>
                    {
                        candidate.Value++;
                        return ProfileCandidatePreparation.Prepared();
                    });
            ProfileMutationResult committed = Commit(fixture, 3);

            Assert.AreEqual(
                ProfileMutationStatus.PreparationRejected,
                preparationRejected.Status);
            Assert.AreEqual(
                ProfileMutationStatus.PersistenceRejected,
                persistenceRejected.Status);
            Assert.IsNull(preparationRejected.Receipt);
            Assert.IsNull(persistenceRejected.Receipt);
            Assert.AreEqual(ProfileMutationStatus.Committed, committed.Status);
            Assert.AreEqual(3UL, committed.Receipt.PublicationSequence);
            Assert.AreEqual(
                "0123456789abcdef0000000000000005",
                committed.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(1, fixture.Sink.Receipts.Count);
        }

        [Test]
        public void InvalidFrozenCandidateRejectsBeforePersistenceAndRetryWorks()
        {
            var adapter = new RejectOnceCandidateAdapter();
            var fixture = Fixture.Create(adapter: adapter);

            ProfileMutationResult rejected = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:invalid-candidate",
                "result-invalid-candidate",
                candidate =>
                {
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });
            ProfileMutationResult retry = Commit(fixture, 2);

            Assert.AreEqual(
                ProfileMutationStatus.CandidateInvalid,
                rejected.Status);
            Assert.AreEqual(ProfileMutationStatus.Committed, retry.Status);
            Assert.AreEqual(1, fixture.Persistence.PersistCount);
            Assert.AreEqual(2UL, retry.Receipt.PublicationSequence);
        }

        [Test]
        public void ConstructionEpochExhaustionNeverPublishesWritable()
        {
            var source = new FixedEpochSource(
                Enumerable.Repeat(
                        "00000000000000000000000000000000",
                        SaveAuthorityTechnicalLimits
                            .MaximumEpochAllocationAttempts)
                    .ToArray());
            var fixture = Fixture.Create(
                allocator: new AuthorityEpochAllocator(source));
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                ProfileAuthorityExpectation.From(
                    fixture.Boundary.GetCurrentAuthority()),
                "operation:construction-epoch-exhausted",
                "result-construction-epoch-exhausted",
                _ =>
                {
                    callbacks++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(
                ProfileMutationStatus.NotWritable,
                result.Status);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, fixture.Persistence.CheckCount);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
        }

        [Test]
        public void ConstructionEpochFailureReleasesCoordinatorForReplacement()
        {
            var coordinator = new ProcessAuthorityMutationCoordinator();
            var source = new FixedEpochSource(
                Enumerable.Repeat(
                        "00000000000000000000000000000000",
                        SaveAuthorityTechnicalLimits
                            .MaximumEpochAllocationAttempts)
                    .Concat(
                        new[]
                        {
                            "0123456789abcdef0000000000000002",
                            "0123456789abcdef0000000000000003"
                        })
                    .ToArray());
            var allocator = new AuthorityEpochAllocator(source);

            var failed = Fixture.Create(
                allocator: allocator,
                coordinator: coordinator);
            var replacement = Fixture.Create(
                allocator: allocator,
                coordinator: coordinator);
            ProfileMutationResult committed = Commit(replacement, 1);

            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                failed.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                replacement.Boundary.GetCurrentAuthority().Status,
                string.Join(
                    ",",
                    replacement.Boundary.GetCurrentAuthority()
                        .DiagnosticCodes));
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                committed.Status);
            Assert.AreEqual(1UL, committed.Receipt.PublicationSequence);
            Assert.AreEqual(
                "0123456789abcdef0000000000000003",
                committed.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(0, failed.Persistence.CheckCount);
            Assert.AreEqual(0, failed.Persistence.PersistCount);
            Assert.AreEqual(0, failed.Sink.Receipts.Count);
            Assert.AreEqual(1, replacement.Persistence.PersistCount);
            Assert.AreEqual(1, replacement.Sink.Receipts.Count);
        }

        [Test]
        public void EpochExhaustionDoesNoCallbackPersistenceOrReceiptWork()
        {
            var invalidSource = new FixedEpochSource(
                new[]
                    {
                        "0123456789abcdef0000000000000002"
                    }
                    .Concat(
                        Enumerable.Repeat(
                            "00000000000000000000000000000000",
                            SaveAuthorityTechnicalLimits
                                .MaximumEpochAllocationAttempts))
                    .ToArray());
            var fixture = Fixture.Create(
                allocator: new AuthorityEpochAllocator(invalidSource));
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:epoch-exhausted",
                "result-epoch-exhausted",
                candidate =>
                {
                    callbacks++;
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.EpochUnavailable,
                result.Status);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(1, fixture.Persistence.CheckCount);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                fixture.Boundary.GetCurrentAuthority().Status);
        }

        [Test]
        public void PublicationSequenceExhaustionDoesNoDownstreamWork()
        {
            var source = new IncrementingEpochSource();
            var fixture = Fixture.Create(
                allocator: new AuthorityEpochAllocator(source),
                initialPublicationSequence: ulong.MaxValue);
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:sequence-exhausted",
                "result-sequence-exhausted",
                candidate =>
                {
                    callbacks++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.PublicationUnavailable,
                result.Status);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(1, source.CallCount);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                fixture.Boundary.GetCurrentAuthority().Status);
        }

        [TestCase("0123456789abcdef0000000000000002")]
        [TestCase("0123456789abcdef0000000000000001")]
        [TestCase("1123456789abcdef0000000000000003")]
        public void NonSuccessorEpochFailsClosedBeforeCallbackOrPersistence(
            string candidateEpoch)
        {
            var fixture = Fixture.Create(
                allocator: new AuthorityEpochAllocator(
                    new FixedEpochSource(
                        "0123456789abcdef0000000000000002",
                        candidateEpoch)));
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:epoch-regression",
                "result-epoch-regression",
                _ =>
                {
                    callbacks++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.EpochUnavailable,
                result.Status);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                fixture.Boundary.GetCurrentAuthority().Status);
        }

        [Test]
        public void ReceiptCapacityIncludesScheduledFifoAndBackpressuresAt64()
        {
            var scheduler = new ManualContinuationScheduler();
            var coordinator = new ProcessAuthorityMutationCoordinator(
                0,
                scheduler);
            var sink = new RecordingReceiptSink();
            var epochSource = new IncrementingEpochSource();
            var fixture = Fixture.Create(
                allocator: new AuthorityEpochAllocator(epochSource),
                sink: sink,
                coordinator: coordinator);

            for (int index = 1;
                 index <= SaveAuthorityTechnicalLimits.ReceiptCapacity;
                 index++)
            {
                Assert.AreEqual(
                    ProfileMutationStatus.Committed,
                    Commit(fixture, index).Status);
            }

            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.ReceiptCapacity,
                fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(1, scheduler.PendingCount);
            Assert.AreEqual(0, sink.Receipts.Count);
            int callbackCount = 0;
            int persistenceBefore = fixture.Persistence.PersistCount;
            int epochCallsBefore = epochSource.CallCount;
            ProfileMutationResult rejected = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:capacity",
                "result-capacity",
                candidate =>
                {
                    callbackCount++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.PublicationBackpressure,
                rejected.Status);
            Assert.AreEqual(0, callbackCount);
            Assert.AreEqual(persistenceBefore, fixture.Persistence.PersistCount);
            Assert.AreEqual(epochCallsBefore, epochSource.CallCount);
            Assert.IsNull(rejected.Receipt);

            scheduler.RunNext();
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            CollectionAssert.AreEqual(
                Enumerable.Range(
                        1,
                        SaveAuthorityTechnicalLimits.ReceiptCapacity)
                    .Select(value => (ulong)value)
                    .ToArray(),
                sink.Receipts.Select(receipt => receipt.PublicationSequence));

            ProfileMutationResult afterDrain = Commit(fixture, 100);
            Assert.AreEqual(ProfileMutationStatus.Committed, afterDrain.Status);
            Assert.AreEqual(65UL, afterDrain.Receipt.PublicationSequence);
            Assert.AreEqual(
                "0123456789abcdef0000000000000043",
                afterDrain.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(epochCallsBefore + 1, epochSource.CallCount);
            Assert.AreEqual(1, scheduler.PendingCount);
            scheduler.RunNext();
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
        }

        [Test]
        public void LaterCallerCanDrainFirstButReceiptOrderRemainsCommitOrder()
        {
            var pauseFirst = new ManualResetEventSlim(false);
            var releaseFirst = new ManualResetEventSlim(false);
            var sink = new RecordingReceiptSink();
            int releaseTimedOut = 0;
            var fixture = Fixture.Create(
                sink: sink,
                beforeDrainRequest: sequence =>
                {
                    if (sequence != 1)
                        return;
                    pauseFirst.Set();
                    if (!releaseFirst.Wait(TimeSpan.FromSeconds(5)))
                        Interlocked.Exchange(ref releaseTimedOut, 1);
                });

            Task<ProfileMutationResult> first = Task.Run(() => Commit(fixture, 1));
            Assert.IsTrue(pauseFirst.Wait(TimeSpan.FromSeconds(5)));

            ProfileMutationResult second = Commit(fixture, 2);
            Assert.AreEqual(ProfileMutationStatus.Committed, second.Status);
            CollectionAssert.AreEqual(
                new[] { 1UL, 2UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));

            releaseFirst.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, releaseTimedOut);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                first.Result.Status);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
        }

        [Test]
        public void DispatcherReleaseRaceUsesOneScheduledContinuation()
        {
            var releaseEntered = new ManualResetEventSlim(false);
            var releaseHook = new ManualResetEventSlim(false);
            var allDelivered = new ManualResetEventSlim(false);
            int hookCalls = 0;
            int releaseTimedOut = 0;
            var sink = new SignalingReceiptSink(2, allDelivered);
            var coordinator = new ProcessAuthorityMutationCoordinator(
                0,
                null,
                () =>
                {
                    if (Interlocked.Increment(ref hookCalls) != 1)
                        return;
                    releaseEntered.Set();
                    if (!releaseHook.Wait(TimeSpan.FromSeconds(5)))
                        Interlocked.Exchange(ref releaseTimedOut, 1);
                });
            var fixture = Fixture.Create(
                sink: sink,
                coordinator: coordinator);

            Task<ProfileMutationResult> first =
                Task.Run(() => Commit(fixture, 1));
            Assert.IsTrue(releaseEntered.Wait(TimeSpan.FromSeconds(5)));

            ProfileMutationResult second = Commit(fixture, 2);
            Assert.AreEqual(ProfileMutationStatus.Committed, second.Status);
            releaseHook.Set();

            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, releaseTimedOut);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                first.Result.Status);
            Assert.IsTrue(allDelivered.Wait(TimeSpan.FromSeconds(5)));
            CollectionAssert.AreEqual(
                new[] { 1UL, 2UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
        }

        [Test]
        public void SubscriberLeaseClosesSelectionToMutationRace()
        {
            var leaseEntered = new ManualResetEventSlim(false);
            var releaseLease = new ManualResetEventSlim(false);
            int leaseCount = 0;
            int releaseTimedOut = 0;
            var coordinator = new ProcessAuthorityMutationCoordinator(
                0,
                null,
                null,
                () =>
                {
                    if (Interlocked.Increment(ref leaseCount) != 1)
                        return;
                    leaseEntered.Set();
                    if (!releaseLease.Wait(TimeSpan.FromSeconds(5)))
                        Interlocked.Exchange(ref releaseTimedOut, 1);
                });
            var sink = new RecordingReceiptSink();
            var fixture = Fixture.Create(
                sink: sink,
                coordinator: coordinator);

            Task<ProfileMutationResult> first =
                Task.Run(() => Commit(fixture, 1));
            Assert.IsTrue(leaseEntered.Wait(TimeSpan.FromSeconds(5)));
            int callbacks = 0;
            ProfileMutationResult overlapping = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:subscriber-overlap",
                "result-subscriber-overlap",
                _ =>
                {
                    callbacks++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(ProfileMutationStatus.Busy, overlapping.Status);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(1, fixture.Persistence.PersistCount);

            releaseLease.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, releaseTimedOut);
            Assert.AreEqual(ProfileMutationStatus.Committed, first.Result.Status);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                Commit(fixture, 2).Status);

            CollectionAssert.AreEqual(
                new[] { 1UL, 2UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
        }

        [Test]
        public void QueuedSubscriberCannotRunWhileMutationCallbackOwnsGate()
        {
            var scheduler = new ManualContinuationScheduler();
            var coordinator = new ProcessAuthorityMutationCoordinator(
                0,
                scheduler);
            var sink = new RecordingReceiptSink();
            var fixture = Fixture.Create(
                sink: sink,
                coordinator: coordinator);

            ProfileMutationResult first = Commit(fixture, 1);
            Assert.AreEqual(ProfileMutationStatus.Committed, first.Status);
            Assert.AreEqual(1, scheduler.PendingCount);
            Assert.AreEqual(1, fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(0, sink.Receipts.Count);

            var callbackEntered = new ManualResetEventSlim(false);
            var releaseCallback = new ManualResetEventSlim(false);
            int callbackWaitTimedOut = 0;
            Task<ProfileMutationResult> second = Task.Run(
                () => fixture.Boundary.TryMutate(
                    CurrentExpectation(fixture),
                    "operation:queued-subscriber-overlap",
                    "result-queued-subscriber-overlap",
                    candidate =>
                    {
                        callbackEntered.Set();
                        fixture.Boundary.RequestReceiptDrain();
                        if (!releaseCallback.Wait(TimeSpan.FromSeconds(5)))
                        {
                            Interlocked.Exchange(
                                ref callbackWaitTimedOut,
                                1);
                        }
                        candidate.Value++;
                        return ProfileCandidatePreparation.Prepared();
                    }));

            Assert.IsTrue(callbackEntered.Wait(TimeSpan.FromSeconds(5)));
            scheduler.RunNext();
            Assert.AreEqual(0, scheduler.PendingCount);
            Assert.AreEqual(0, sink.Receipts.Count);
            Assert.AreEqual(1, fixture.Boundary.PendingReceiptCount);

            releaseCallback.Set();
            Assert.IsTrue(second.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, callbackWaitTimedOut);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                second.Result.Status);
            Assert.AreEqual(1, scheduler.PendingCount);
            Assert.AreEqual(0, sink.Receipts.Count);
            Assert.AreEqual(2, fixture.Boundary.PendingReceiptCount);

            scheduler.RunNext();
            CollectionAssert.AreEqual(
                new[] { 1UL, 2UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
        }

        [Test]
        public void ReceiptSubscriberReentrancyIsBusyAndRetryable()
        {
            var sink = new ReentrantReceiptSink();
            var fixture = Fixture.Create(sink: sink);
            sink.Reenter = () => Commit(fixture, 2);

            ProfileMutationResult first = Commit(fixture, 1);

            Assert.AreEqual(ProfileMutationStatus.Committed, first.Status);
            Assert.NotNull(sink.ReentryResult);
            Assert.AreEqual(
                ProfileMutationStatus.Busy,
                sink.ReentryResult.Status);
            Assert.AreEqual(1, fixture.Persistence.PersistCount);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                Commit(fixture, 2).Status);
            CollectionAssert.AreEqual(
                new[] { 1UL, 2UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));
        }

        [Test]
        public void ThrowingSchedulerRetainsReceiptForExplicitRetry()
        {
            var scheduler = new ThrowOnceThenInlineScheduler();
            var coordinator = new ProcessAuthorityMutationCoordinator(
                0,
                scheduler);
            var sink = new RecordingReceiptSink();
            var fixture = Fixture.Create(
                sink: sink,
                coordinator: coordinator);

            ProfileMutationResult committed = Commit(fixture, 1);

            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                committed.Status);
            Assert.AreEqual(1, fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(0, sink.Receipts.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    SaveAuthorityDiagnosticCodes
                        .ReceiptSchedulerUnavailable
                },
                fixture.Boundary.DispatcherDiagnosticCodes);

            fixture.Boundary.RequestReceiptDrain();

            Assert.AreEqual(2, scheduler.CallCount);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            CollectionAssert.AreEqual(
                new[] { 1UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));
        }

        [Test]
        public void ConcurrentRetryDuringFailedSchedulingNeedsNoThirdWakeup()
        {
            var scheduler = new BlockingFailOnceScheduler();
            var coordinator = new ProcessAuthorityMutationCoordinator(
                0,
                scheduler);
            var sink = new RecordingReceiptSink();
            var fixture = Fixture.Create(
                sink: sink,
                coordinator: coordinator);

            Task<ProfileMutationResult> commit =
                Task.Run(() => Commit(fixture, 1));
            Assert.IsTrue(
                scheduler.FirstCallEntered.Wait(TimeSpan.FromSeconds(5)));

            fixture.Boundary.RequestReceiptDrain();
            Assert.AreEqual(0, sink.Receipts.Count);
            scheduler.ReleaseFirstCall.Set();

            Assert.IsTrue(commit.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                commit.Result.Status);
            Assert.AreEqual(1, scheduler.CallCount);
            Assert.IsFalse(scheduler.WaitTimedOut);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            CollectionAssert.AreEqual(
                new[] { 1UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));
            CollectionAssert.AreEqual(
                new[]
                {
                    SaveAuthorityDiagnosticCodes
                        .ReceiptSchedulerUnavailable
                },
                fixture.Boundary.DispatcherDiagnosticCodes);
        }

        [Test]
        public void InlineSchedulerTrampolinesRepeatedContinuationRequests()
        {
            var scheduler =
                new InlineAuthorityReceiptContinuationScheduler();
            int remaining = 256;
            int depth = 0;
            int maximumDepth = 0;
            int calls = 0;
            Action continuation = null;
            continuation = () =>
            {
                depth++;
                maximumDepth = Math.Max(maximumDepth, depth);
                calls++;
                if (remaining-- > 0)
                    Assert.IsTrue(scheduler.TrySchedule(continuation));
                depth--;
            };

            Assert.IsTrue(scheduler.TrySchedule(continuation));

            Assert.AreEqual(257, calls);
            Assert.AreEqual(1, maximumDepth);
        }

        [Test]
        public void DeletionWaitsForReceiptDrainThenClearsCandidateIdentity()
        {
            var scheduler = new ManualContinuationScheduler();
            var coordinator = new ProcessAuthorityMutationCoordinator(
                0,
                scheduler);
            var sink = new RecordingReceiptSink();
            var fixture = Fixture.Create(
                sink: sink,
                coordinator: coordinator);
            ProfileMutationResult committed = Commit(fixture, 1);
            ProfileWriteAuthoritySnapshot deleted =
                ProfileWriteAuthoritySnapshotFactory.NonWritable(
                    ProfileWriteAuthorityStatus.Deleted,
                    0,
                    0,
                    false,
                    ProfileAuthoritySourceGeneration.None,
                    new[] { "AL-SAVE-AUTH-DELETED" });

            ProfileAuthorityTransitionResult pending =
                fixture.Boundary.TryRevokeAuthority(deleted);

            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                committed.Status);
            Assert.AreEqual(
                ProfileAuthorityTransitionStatus.PublicationPending,
                pending.Status);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(0, sink.Receipts.Count);

            scheduler.RunNext();
            ProfileAuthorityTransitionResult published =
                fixture.Boundary.TryRevokeAuthority(deleted);

            Assert.AreEqual(
                ProfileAuthorityTransitionStatus.Published,
                published.Status);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Deleted,
                fixture.Boundary.GetCurrentAuthority().Status);
            var candidateField =
                typeof(SerializedAuthorityMutationBoundary<MutableCandidate>)
                    .GetField(
                        "_publishedCandidate",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(candidateField);
            Assert.IsNull(candidateField.GetValue(fixture.Boundary));
            CollectionAssert.AreEqual(
                new[] { 1UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));

            fixture.Boundary.RequestReceiptDrain();
            CollectionAssert.AreEqual(
                new[] { 1UL },
                sink.Receipts.Select(receipt => receipt.PublicationSequence));
        }

        [Test]
        public void ReplacementWaitsForPriorDrainAndKeepsProcessSequence()
        {
            var coordinator = new ProcessAuthorityMutationCoordinator();
            var epochs = new AuthorityEpochAllocator(
                new IncrementingEpochSource());
            var blockingSink = new BlockingReceiptSink();
            var original = Fixture.Create(
                allocator: epochs,
                sink: blockingSink,
                coordinator: coordinator);

            Task<ProfileMutationResult> commit =
                Task.Run(() => Commit(original, 1));
            Assert.IsTrue(
                blockingSink.FirstEntered.Wait(TimeSpan.FromSeconds(5)));
            ProfileWriteAuthoritySnapshot replacementPrototype =
                original.Boundary.GetCurrentAuthority();
            MutableCandidate replacementCandidate =
                ReadPublishedCandidate(original.Boundary);
            Assert.AreEqual(
                "0123456789abcdef0000000000000003",
                replacementPrototype.AuthorityEpoch);
            Assert.AreEqual(1, replacementCandidate.Value);
            Assert.AreEqual(
                replacementPrototype.VerifiedGenerationFingerprint,
                replacementCandidate.VerifiedGenerationFingerprint);

            var premature = Fixture.Create(
                allocator: epochs,
                coordinator: coordinator);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                premature.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(
                ProfileMutationStatus.NotWritable,
                Commit(premature, 9).Status);
            Assert.AreEqual(
                AuthorityCoordinatorRetirementStatus.Busy,
                original.Boundary.TryRetire());

            blockingSink.ReleaseFirst.Set();
            Assert.IsTrue(commit.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(blockingSink.WaitTimedOut);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                commit.Result.Status);
            Assert.AreEqual(
                AuthorityCoordinatorRetirementStatus.Retired,
                original.Boundary.TryRetire());

            var staleCandidateReplacement = Fixture.Create(
                authority: replacementPrototype,
                allocator: epochs,
                persistence: original.Persistence,
                coordinator: coordinator,
                initialCandidate: new MutableCandidate
                {
                    ProfileId = replacementPrototype.ProfileId,
                    VerifiedGenerationFingerprint = InitialFingerprint,
                    Value = 0
                });
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                staleCandidateReplacement.Boundary
                    .GetCurrentAuthority().Status);

            var wrongAllocatorReplacement = Fixture.Create(
                authority: replacementPrototype,
                allocator: new AuthorityEpochAllocator(
                    new IncrementingEpochSource()),
                persistence: original.Persistence,
                coordinator: coordinator,
                initialCandidate: replacementCandidate);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                wrongAllocatorReplacement.Boundary
                    .GetCurrentAuthority().Status);

            var replacement = Fixture.Create(
                authority: replacementPrototype,
                allocator: epochs,
                persistence: original.Persistence,
                coordinator: coordinator,
                initialCandidate: replacementCandidate);
            Assert.AreEqual(
                "0123456789abcdef0000000000000004",
                replacement.Boundary.GetCurrentAuthority().AuthorityEpoch);
            ProfileMutationResult replacementCommit =
                replacement.Boundary.TryMutate(
                    CurrentExpectation(replacement),
                    "operation:relative-replacement",
                    "result-relative-replacement",
                    candidate =>
                    {
                        candidate.Value++;
                        return ProfileCandidatePreparation.Prepared();
                    });

            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                replacement.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                replacementCommit.Status);
            Assert.AreEqual(
                2UL,
                replacementCommit.Receipt.PublicationSequence);
            Assert.AreEqual(
                "0123456789abcdef0000000000000005",
                replacementCommit.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(
                2.ToString("x64", CultureInfo.InvariantCulture),
                replacementCommit.Receipt.CommittedGenerationFingerprint);
            Assert.AreEqual(2, original.Persistence.LastPersistedValue);
            Assert.AreEqual(
                2,
                ReadPublishedCandidate(replacement.Boundary).Value);
        }

        [Test]
        public void SubscriberExceptionIsIsolatedRecordedAndNeverReplayed()
        {
            var sink = new ThrowOnceReceiptSink();
            var fixture = Fixture.Create(sink: sink);

            ProfileMutationResult first = Commit(fixture, 1);
            ProfileMutationResult second = Commit(fixture, 2);
            fixture.Boundary.RequestReceiptDrain();

            Assert.AreEqual(ProfileMutationStatus.Committed, first.Status);
            Assert.AreEqual(ProfileMutationStatus.Committed, second.Status);
            CollectionAssert.AreEqual(
                new[] { 1UL, 2UL },
                sink.AttemptedSequences);
            CollectionAssert.AreEqual(
                new[] { SaveAuthorityDiagnosticCodes.ReceiptSinkThrew },
                fixture.Boundary.DispatcherDiagnosticCodes);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
        }

        [Test]
        public void PersistenceThrowPublishesCommitUncertainReceiptAndAuthority()
        {
            var persistence = new FakePersistence
            {
                ThrowOnPersist = true
            };
            var fixture = Fixture.Create(persistence: persistence);

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:uncertain",
                "결과-uncertain",
                candidate =>
                {
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(ProfileMutationStatus.CommitUncertain, result.Status);
            Assert.NotNull(result.Receipt);
            Assert.AreEqual(
                ProfileMutationReceiptStatus.CommitUncertain,
                result.Receipt.Status);
            Assert.IsTrue(result.Receipt.MayHaveMutated);
            Assert.AreEqual(InitialFingerprint,
                result.Receipt.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                string.Empty,
                result.Receipt.CommittedGenerationFingerprint);
            Assert.AreEqual(
                string.Empty,
                result.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.CommitUncertain,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(string.Empty,
                fixture.Boundary.GetCurrentAuthority().ProfileId);
            Assert.AreEqual(1, fixture.Sink.Receipts.Count);
            Assert.AreSame(result.Receipt, fixture.Sink.Receipts[0]);
        }

        [Test]
        public void ExplicitPostIoUncertaintyPublishesSameFailClosedShape()
        {
            var persistence = new FakePersistence
            {
                NextOutcome = FakePersistenceOutcome.CommitUncertain
            };
            var fixture = Fixture.Create(persistence: persistence);

            ProfileMutationResult result = Commit(fixture, 1);

            Assert.AreEqual(
                ProfileMutationStatus.CommitUncertain,
                result.Status);
            Assert.AreEqual(
                ProfileMutationReceiptStatus.CommitUncertain,
                result.Receipt.Status);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.CommitUncertain,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(string.Empty,
                result.Receipt.CommittedGenerationFingerprint);
            Assert.AreEqual(string.Empty,
                result.Receipt.CommittedAuthorityEpoch);
        }

        [Test]
        public void NullPostIoPersistenceResultIsCommitUncertain()
        {
            var persistence = new FakePersistence
            {
                ReturnNull = true
            };
            var fixture = Fixture.Create(persistence: persistence);

            ProfileMutationResult result = Commit(fixture, 1);

            Assert.AreEqual(
                ProfileMutationStatus.CommitUncertain,
                result.Status);
            Assert.NotNull(result.Receipt);
            Assert.IsTrue(result.Receipt.MayHaveMutated);
            Assert.AreEqual(1, fixture.Sink.Receipts.Count);
        }

        [Test]
        public void UnavailableInitialLedgerCheckDoesNoReservationOrCallback()
        {
            var persistence = new FakePersistence
            {
                InitialCheckUnavailable = true
            };
            var source = new IncrementingEpochSource();
            var fixture = Fixture.Create(
                persistence: persistence,
                allocator: new AuthorityEpochAllocator(source));
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:ledger-unavailable",
                "result-ledger-unavailable",
                _ =>
                {
                    callbacks++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(ProfileMutationStatus.Unavailable, result.Status);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(1, source.CallCount);
            Assert.AreEqual(0, persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
        }

        [Test]
        public void LedgerRecheckFaultMatrixFailsClosedBeforePersistence()
        {
            LedgerCheckFault[] faults =
            {
                LedgerCheckFault.ReturnsNull,
                LedgerCheckFault.Throws,
                LedgerCheckFault.UnknownStatus
            };

            foreach (int faultCheckNumber in new[] { 1, 2 })
            {
                foreach (LedgerCheckFault fault in faults)
                {
                    var persistence = new FakePersistence
                    {
                        FaultCheckNumber = faultCheckNumber,
                        CheckFault = fault
                    };
                    var fixture = Fixture.Create(
                        persistence: persistence);
                    int callbacks = 0;

                    ProfileMutationResult result =
                        fixture.Boundary.TryMutate(
                            CurrentExpectation(fixture),
                            "operation:recheck-fault",
                            "result-recheck-fault",
                            candidate =>
                            {
                                callbacks++;
                                candidate.Value++;
                                return ProfileCandidatePreparation
                                    .Prepared();
                            });

                    Assert.AreEqual(
                        ProfileMutationStatus.Unavailable,
                        result.Status);
                    Assert.AreEqual(
                        faultCheckNumber == 1 ? 0 : 1,
                        callbacks);
                    Assert.AreEqual(0, persistence.PersistCount);
                    Assert.AreEqual(0, fixture.Sink.Receipts.Count);
                    Assert.AreEqual(
                        ProfileWriteAuthorityStatus.Unavailable,
                        fixture.Boundary.GetCurrentAuthority().Status);
                }
            }
        }

        [Test]
        public void VerifiedRollbackRestoresOriginalAuthorityAndPublishesFailure()
        {
            var persistence = new FakePersistence
            {
                NextOutcome = FakePersistenceOutcome.VerifiedRollback
            };
            var fixture = Fixture.Create(persistence: persistence);
            ProfileWriteAuthoritySnapshot before =
                fixture.Boundary.GetCurrentAuthority();

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:rollback",
                "result-rollback",
                candidate =>
                {
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.VerifiedRollback,
                result.Status);
            Assert.AreSame(before, fixture.Boundary.GetCurrentAuthority());
            Assert.AreEqual(
                ProfileMutationReceiptStatus.VerifiedRollback,
                result.Receipt.Status);
            Assert.AreEqual(
                string.Empty,
                result.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(
                string.Empty,
                result.Receipt.CommittedGenerationFingerprint);
        }

        [Test]
        public void ExternalAuthorityDriftAfterPreparationRejectsBeforePersistence()
        {
            var persistence = new FakePersistence
            {
                StaleOnSecondCheck = true
            };
            var fixture = Fixture.Create(persistence: persistence);
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:drift",
                "result-drift",
                candidate =>
                {
                    callbacks++;
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.StaleAuthority,
                result.Status);
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(2, persistence.CheckCount);
            Assert.AreEqual(0, persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                fixture.Boundary.GetCurrentAuthority().Status);
        }

        [Test]
        public void LoadOrDeleteRevocationCannotRaceActiveCommit()
        {
            var fixture = Fixture.Create();
            var callbackEntered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            int releaseTimedOut = 0;

            Task<ProfileMutationResult> commit = Task.Run(() =>
                fixture.Boundary.TryMutate(
                    CurrentExpectation(fixture),
                    "operation:commit",
                    "result-commit",
                    candidate =>
                    {
                        callbackEntered.Set();
                        if (!release.Wait(TimeSpan.FromSeconds(5)))
                            Interlocked.Exchange(ref releaseTimedOut, 1);
                        candidate.Value++;
                        return ProfileCandidatePreparation.Prepared();
                    }));
            Assert.IsTrue(callbackEntered.Wait(TimeSpan.FromSeconds(5)));

            ProfileAuthorityTransitionResult racing =
                fixture.Boundary.TryRevokeAuthority(
                    ProfileWriteAuthoritySnapshotFactory.NonWritable(
                        ProfileWriteAuthorityStatus.Deleted,
                        0,
                        0,
                        false,
                        ProfileAuthoritySourceGeneration.None,
                        new[] { "AL-SAVE-AUTH-DELETED" }));
            Assert.AreEqual(ProfileAuthorityTransitionStatus.Busy, racing.Status);

            release.Set();
            Assert.IsTrue(commit.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, releaseTimedOut);
            Assert.AreEqual(
                ProfileMutationStatus.Committed,
                commit.Result.Status);
            Assert.AreEqual(1, fixture.Persistence.PersistCount);
            Assert.AreEqual(1, fixture.Sink.Receipts.Count);
            ProfileAuthorityTransitionResult after =
                fixture.Boundary.TryRevokeAuthority(
                    ProfileWriteAuthoritySnapshotFactory.NonWritable(
                        ProfileWriteAuthorityStatus.Deleted,
                        0,
                        0,
                        false,
                        ProfileAuthoritySourceGeneration.None,
                        new[] { "AL-SAVE-AUTH-DELETED" }));
            Assert.AreEqual(
                ProfileAuthorityTransitionStatus.Published,
                after.Status);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Deleted,
                fixture.Boundary.GetCurrentAuthority().Status);
        }

        [Test]
        public void CallbackAliasCannotChangeDetachedPersistedCandidate()
        {
            var fixture = Fixture.Create();
            MutableCandidate captured = null;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:alias",
                "result-alias",
                candidate =>
                {
                    candidate.Value = 41;
                    captured = candidate;
                    return ProfileCandidatePreparation.Prepared();
                });
            captured.Value = 999;

            Assert.AreEqual(ProfileMutationStatus.Committed, result.Status);
            Assert.AreEqual(41, fixture.Persistence.LastPersistedValue);
        }

        [Test]
        public void CallbackCannotReplaceCandidateProfileIdentity()
        {
            var fixture = Fixture.Create();

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:profile-swap",
                "result-profile-swap",
                candidate =>
                {
                    candidate.ProfileId =
                        "alp_1123456789abcdef0123456789abcdef";
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(
                ProfileMutationStatus.CandidateInvalid,
                result.Status);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
        }

        [Test]
        public void CommittedCandidateProfileSwapBecomesCommitUncertain()
        {
            var persistence = new FakePersistence
            {
                CommittedProfileIdOverride =
                    "alp_1123456789abcdef0123456789abcdef"
            };
            var fixture = Fixture.Create(persistence: persistence);

            ProfileMutationResult result = Commit(fixture, 1);

            Assert.AreEqual(
                ProfileMutationStatus.CommitUncertain,
                result.Status);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.CommitUncertain,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(
                ProfileMutationReceiptStatus.CommitUncertain,
                result.Receipt.Status);
        }

        [Test]
        public void CommittedCandidateGenerationMismatchBecomesCommitUncertain()
        {
            var persistence = new FakePersistence
            {
                CommittedCandidateGenerationFingerprintOverride =
                    "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
            };
            var fixture = Fixture.Create(persistence: persistence);

            ProfileMutationResult result = Commit(fixture, 1);

            Assert.AreEqual(
                ProfileMutationStatus.CommitUncertain,
                result.Status);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.CommitUncertain,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(1, persistence.PersistCount);
            Assert.AreEqual(1, fixture.Sink.Receipts.Count);
            Assert.AreEqual(
                ProfileMutationReceiptStatus.CommitUncertain,
                result.Receipt.Status);
            Assert.AreEqual(
                0,
                ReadPublishedCandidate(fixture.Boundary).Value);
        }

        [Test]
        public void MalformedCommittedVerificationOutputsFailClosedAtomically()
        {
            Action<FakePersistence>[] configurations =
            {
                persistence =>
                    persistence.CommittedGenerationFingerprintOverride =
                        string.Empty,
                persistence =>
                    persistence.CommittedGenerationFingerprintOverride =
                        "NOT-A-SHA256",
                persistence =>
                    persistence.CommittedPayloadFingerprintOverride =
                        string.Empty,
                persistence =>
                    persistence.CommittedPayloadFingerprintOverride =
                        "NOT-A-SHA256",
                persistence =>
                    persistence.CommittedSourceGeneration =
                        ProfileAuthoritySourceGeneration.None,
                persistence =>
                    persistence.CommittedSourceGeneration =
                        (ProfileAuthoritySourceGeneration)999,
                persistence =>
                    persistence.ReturnNullCommittedCandidate = true
            };

            foreach (Action<FakePersistence> configure in configurations)
            {
                var persistence = new FakePersistence();
                configure(persistence);
                var fixture = Fixture.Create(persistence: persistence);

                ProfileMutationResult result = Commit(fixture, 41);

                Assert.AreEqual(
                    ProfileMutationStatus.CommitUncertain,
                    result.Status);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.CommitUncertain,
                    fixture.Boundary.GetCurrentAuthority().Status);
                Assert.AreEqual(
                    ProfileMutationReceiptStatus.CommitUncertain,
                    result.Receipt.Status);
                Assert.AreEqual(
                    string.Empty,
                    result.Receipt.CommittedGenerationFingerprint);
                Assert.AreEqual(
                    string.Empty,
                    result.Receipt.CommittedAuthorityEpoch);
                Assert.AreEqual(1, fixture.Sink.Receipts.Count);
                Assert.AreEqual(
                    0,
                    ReadPublishedCandidate(fixture.Boundary)?.Value);
            }
        }

        [Test]
        public void PostIoCandidateCloneAndValidationFaultsFailClosed()
        {
            CandidateAdapterFault[] faults =
            {
                CandidateAdapterFault.CommittedCloneThrows,
                CandidateAdapterFault.CommittedValidationThrows
            };

            foreach (CandidateAdapterFault fault in faults)
            {
                var fixture = Fixture.Create(
                    adapter: new FaultingCandidateAdapter(fault));

                ProfileMutationResult result = Commit(fixture, 17);

                Assert.AreEqual(
                    ProfileMutationStatus.CommitUncertain,
                    result.Status);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.CommitUncertain,
                    fixture.Boundary.GetCurrentAuthority().Status);
                Assert.AreEqual(1, fixture.Sink.Receipts.Count);
                Assert.AreEqual(
                    0,
                    ReadPublishedCandidate(fixture.Boundary)?.Value);
            }
        }

        [Test]
        public void VerifiedCommittedSourceGenerationReplacesSelectedSource()
        {
            var persistence = new FakePersistence
            {
                CommittedSourceGeneration =
                    ProfileAuthoritySourceGeneration.Primary
            };
            ProfileWriteAuthoritySnapshot backupAuthority =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    ProfileId,
                    InitialEpoch,
                    InitialFingerprint,
                    ProfileAuthoritySourceGeneration.Backup,
                    Array.Empty<string>());
            var fixture = Fixture.Create(
                authority: backupAuthority,
                persistence: persistence);

            ProfileMutationResult result = Commit(fixture, 1);

            Assert.AreEqual(ProfileMutationStatus.Committed, result.Status);
            Assert.AreEqual(
                ProfileAuthoritySourceGeneration.Primary,
                fixture.Boundary.GetCurrentAuthority()
                    .SelectedSourceGeneration);
        }

        [Test]
        public void InvalidInitialCandidateCannotRetainWritableAuthority()
        {
            var fixture = Fixture.Create(
                initialCandidate: new MutableCandidate
                {
                    ProfileId =
                        "alp_1123456789abcdef0123456789abcdef",
                    VerifiedGenerationFingerprint = InitialFingerprint
                });

            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                fixture.Boundary.GetCurrentAuthority().Status);
            Assert.AreEqual(
                ProfileMutationStatus.NotWritable,
                Commit(fixture, 1).Status);
        }

        [Test]
        public void ExactReplayBuildsFreshSessionReceiptWithoutPublication()
        {
            var fixture = Fixture.Create();
            ProfileMutationResult committed =
                CommitWithReplayRecord(fixture, 1);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.ContractVersion,
                committed.Receipt.ContractVersion);
            Assert.AreEqual(
                ProfileMutationReceiptStatus.Committed,
                committed.Receipt.Status);
            Assert.AreEqual(ProfileId, committed.Receipt.ProfileId);
            Assert.AreEqual(
                InitialFingerprint,
                committed.Receipt.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                1.ToString("x64", CultureInfo.InvariantCulture),
                committed.Receipt.CommittedGenerationFingerprint);
            Assert.AreEqual(
                1001.ToString("x64", CultureInfo.InvariantCulture),
                committed.Receipt.CommittedPayloadFingerprint);
            Assert.AreEqual(
                "0123456789abcdef0000000000000003",
                committed.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(1UL, committed.Receipt.PublicationSequence);
            Assert.AreEqual("operation:001", committed.Receipt.OperationId);
            Assert.AreEqual("result-001", committed.Receipt.ResultId);
            Assert.IsTrue(committed.Receipt.MayHaveMutated);
            CollectionAssert.IsEmpty(committed.Receipt.DiagnosticCodes);
            int persistenceBefore = fixture.Persistence.PersistCount;
            int checksBeforeReplay = fixture.Persistence.CheckCount;
            ProfileWriteAuthoritySnapshot current =
                fixture.Boundary.GetCurrentAuthority();

            ProfileMutationResult replay = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                committed.Receipt.OperationId,
                committed.Receipt.ResultId,
                _ => ProfileCandidatePreparation.ExactReplay());

            Assert.AreEqual(ProfileMutationStatus.AlreadyCommitted, replay.Status);
            Assert.AreNotSame(committed.Receipt, replay.Receipt);
            Assert.AreEqual(2UL, replay.Receipt.PublicationSequence);
            Assert.AreEqual(current.ProfileId, replay.Receipt.ProfileId);
            Assert.AreEqual(
                committed.Receipt.ExpectedGenerationFingerprint,
                replay.Receipt.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                current.VerifiedGenerationFingerprint,
                replay.Receipt.CommittedGenerationFingerprint);
            Assert.AreEqual(
                current.AuthorityEpoch,
                replay.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(
                committed.Receipt.OperationId,
                replay.Receipt.OperationId);
            Assert.AreEqual(
                committed.Receipt.ResultId,
                replay.Receipt.ResultId);
            Assert.AreEqual(
                committed.Receipt.CommittedPayloadFingerprint,
                replay.Receipt.CommittedPayloadFingerprint);
            Assert.IsTrue(replay.Receipt.MayHaveMutated);
            CollectionAssert.IsEmpty(replay.Receipt.DiagnosticCodes);
            Assert.AreEqual(persistenceBefore, fixture.Persistence.PersistCount);
            Assert.AreEqual(
                checksBeforeReplay + 2,
                fixture.Persistence.CheckCount);
            Assert.AreEqual(1, fixture.Sink.Receipts.Count);

            ProfileMutationResult next = Commit(fixture, 2);
            Assert.AreEqual(3UL, next.Receipt.PublicationSequence);
            Assert.AreEqual(
                "0123456789abcdef0000000000000005",
                next.Receipt.CommittedAuthorityEpoch);
        }

        [Test]
        public void HistoricalExactReplaySurvivesLaterAuthorityPublications()
        {
            var fixture = Fixture.Create();
            ProfileMutationResult historical =
                CommitWithReplayRecord(fixture, 1);
            ProfileMutationResult later = Commit(fixture, 2);
            int persistenceBefore = fixture.Persistence.PersistCount;
            ProfileWriteAuthoritySnapshot current =
                fixture.Boundary.GetCurrentAuthority();

            ProfileMutationResult replay = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                historical.Receipt.OperationId,
                historical.Receipt.ResultId,
                _ => ProfileCandidatePreparation.ExactReplay());

            Assert.AreEqual(ProfileMutationStatus.Committed, later.Status);
            Assert.AreNotEqual(
                historical.Receipt.CommittedGenerationFingerprint,
                fixture.Boundary.GetCurrentAuthority()
                    .VerifiedGenerationFingerprint);
            Assert.AreNotEqual(
                historical.Receipt.CommittedAuthorityEpoch,
                fixture.Boundary.GetCurrentAuthority().AuthorityEpoch);
            Assert.AreEqual(
                ProfileMutationStatus.AlreadyCommitted,
                replay.Status);
            Assert.AreNotSame(historical.Receipt, replay.Receipt);
            Assert.AreEqual(3UL, replay.Receipt.PublicationSequence);
            Assert.AreEqual(
                historical.Receipt.ExpectedGenerationFingerprint,
                replay.Receipt.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                current.VerifiedGenerationFingerprint,
                replay.Receipt.CommittedGenerationFingerprint);
            Assert.AreEqual(
                current.AuthorityEpoch,
                replay.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(
                historical.Receipt.CommittedPayloadFingerprint,
                replay.Receipt.CommittedPayloadFingerprint);
            Assert.AreEqual(
                persistenceBefore,
                fixture.Persistence.PersistCount);
            Assert.AreEqual(2, fixture.Sink.Receipts.Count);
        }

        [Test]
        public void DurableReplayDriftDuringCallbackFailsClosed()
        {
            var persistence = new FakePersistence
            {
                StaleOnSecondCheck = false
            };
            const string operationId = "operation:replay-drift";
            const string resultId = "result-replay-drift";
            var fixture = Fixture.Create(
                persistence: persistence,
                initialCandidate: new MutableCandidate
                {
                    ProfileId = ProfileId,
                    VerifiedGenerationFingerprint = InitialFingerprint,
                    ReplayRecord = new MutableReplayRecord(
                        operationId,
                        resultId,
                        InitialFingerprint,
                        "2222222222222222222222222222222222222222222222222222222222222222")
                });
            int callbacks = 0;

            ProfileMutationResult replay = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                operationId,
                resultId,
                _ =>
                {
                    callbacks++;
                    persistence.StaleOnSecondCheck = true;
                    return ProfileCandidatePreparation.ExactReplay();
                });

            Assert.AreEqual(
                ProfileMutationStatus.StaleAuthority,
                replay.Status);
            Assert.IsNull(replay.Receipt);
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(2, persistence.CheckCount);
            Assert.AreEqual(0, persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                fixture.Boundary.GetCurrentAuthority().Status);
        }

        [Test]
        public void DurableReplayAfterRestartUsesFreshAuthorityNotStoredEpoch()
        {
            var original = Fixture.Create();
            ProfileMutationResult committed =
                CommitWithReplayRecord(original, 1);
            MutableCandidate persistedCandidate =
                ReadPublishedCandidate(original.Boundary);
            Assert.AreEqual(
                AuthorityCoordinatorRetirementStatus.Retired,
                original.Boundary.TryRetire());

            ProfileWriteAuthoritySnapshot restartAuthority =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    ProfileId,
                    "fedcba98765432100000000000000001",
                    committed.Receipt.CommittedGenerationFingerprint,
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());
            var restarted = Fixture.Create(
                authority: restartAuthority,
                allocator: new AuthorityEpochAllocator(
                    new FixedEpochSource(
                        "fedcba98765432100000000000000002",
                        "fedcba98765432100000000000000003")),
                coordinator: new ProcessAuthorityMutationCoordinator(),
                initialCandidate: persistedCandidate);

            ProfileMutationResult replay = restarted.Boundary.TryMutate(
                CurrentExpectation(restarted),
                committed.Receipt.OperationId,
                committed.Receipt.ResultId,
                _ => ProfileCandidatePreparation.ExactReplay());

            Assert.AreEqual(
                ProfileMutationStatus.AlreadyCommitted,
                replay.Status);
            Assert.AreEqual(1UL, replay.Receipt.PublicationSequence);
            Assert.AreEqual(ProfileId, replay.Receipt.ProfileId);
            Assert.AreEqual(
                committed.Receipt.ExpectedGenerationFingerprint,
                replay.Receipt.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                committed.Receipt.CommittedGenerationFingerprint,
                replay.Receipt.CommittedGenerationFingerprint);
            Assert.AreEqual(
                "fedcba98765432100000000000000002",
                replay.Receipt.CommittedAuthorityEpoch);
            Assert.AreNotEqual(
                committed.Receipt.CommittedAuthorityEpoch,
                replay.Receipt.CommittedAuthorityEpoch);
            Assert.AreEqual(
                committed.Receipt.CommittedPayloadFingerprint,
                replay.Receipt.CommittedPayloadFingerprint);
            Assert.AreEqual(2, restarted.Persistence.CheckCount);
            Assert.AreEqual(0, restarted.Persistence.PersistCount);
            Assert.AreEqual(0, restarted.Sink.Receipts.Count);
            Assert.AreEqual(0, restarted.Boundary.PendingReceiptCount);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                restarted.Boundary.GetCurrentAuthority().Status);
        }

        [Test]
        public void ReplayVerifierFaultsRejectWithoutDownstreamWork()
        {
            ReplayAdapterFault[] faults =
            {
                ReplayAdapterFault.ReplayCloneThrows,
                ReplayAdapterFault.PublishedValidationReturnsNull,
                ReplayAdapterFault.VerificationReturnsNull,
                ReplayAdapterFault.VerificationThrows
            };
            const string operationId = "operation:replay-verifier-fault";
            const string resultId = "result-replay-verifier-fault";

            foreach (ReplayAdapterFault fault in faults)
            {
                var fixture = Fixture.Create(
                    adapter: new ReplayFaultingCandidateAdapter(fault),
                    initialCandidate: new MutableCandidate
                    {
                        ProfileId = ProfileId,
                        VerifiedGenerationFingerprint = InitialFingerprint,
                        ReplayRecord = new MutableReplayRecord(
                            operationId,
                            resultId,
                            InitialFingerprint,
                            "2222222222222222222222222222222222222222222222222222222222222222")
                    });
                int callbacks = 0;

                ProfileMutationResult result =
                    fixture.Boundary.TryMutate(
                        CurrentExpectation(fixture),
                        operationId,
                        resultId,
                        _ =>
                        {
                            callbacks++;
                            return ProfileCandidatePreparation.ExactReplay();
                        });

                Assert.AreEqual(
                    ProfileMutationStatus.PreparationRejected,
                    result.Status);
                Assert.IsNull(result.Receipt);
                Assert.AreEqual(1, callbacks);
                Assert.AreEqual(1, fixture.Persistence.CheckCount);
                Assert.AreEqual(0, fixture.Persistence.PersistCount);
                Assert.AreEqual(0, fixture.Sink.Receipts.Count);
                Assert.AreEqual(0, fixture.Boundary.PendingReceiptCount);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    fixture.Boundary.GetCurrentAuthority().Status);
            }
        }

        [Test]
        public void MutationReceiptCopiesAndFreezesEveryDiagnosticField()
        {
            string[] diagnostics =
            {
                "AL-SAVE-AUTH-ZETA",
                "AL-SAVE-AUTH-ALPHA"
            };
            var receipt = new ProfileMutationReceipt(
                ProfileMutationReceiptStatus.VerifiedRollback,
                7,
                ProfileId,
                InitialFingerprint,
                string.Empty,
                string.Empty,
                "operation:receipt",
                "result-receipt",
                string.Empty,
                true,
                diagnostics);
            diagnostics[0] = "AL-SAVE-AUTH-MUTATED";

            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.ContractVersion,
                receipt.ContractVersion);
            Assert.AreEqual(
                ProfileMutationReceiptStatus.VerifiedRollback,
                receipt.Status);
            Assert.AreEqual(7UL, receipt.PublicationSequence);
            Assert.AreEqual(ProfileId, receipt.ProfileId);
            Assert.AreEqual(
                InitialFingerprint,
                receipt.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                "operation:receipt",
                receipt.OperationId);
            Assert.AreEqual("result-receipt", receipt.ResultId);
            Assert.IsTrue(receipt.MayHaveMutated);
            CollectionAssert.AreEqual(
                new[]
                {
                    "AL-SAVE-AUTH-ALPHA",
                    "AL-SAVE-AUTH-ZETA"
                },
                receipt.DiagnosticCodes);
            Assert.Throws<NotSupportedException>(
                () => ((IList<string>)receipt.DiagnosticCodes)[0] =
                    "AL-SAVE-AUTH-REPLACED");
        }

        [Test]
        public void ForgedReplayMarkerWithoutPublishedRecordIsRejected()
        {
            var fixture = Fixture.Create();
            int callbacks = 0;

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "operation:forged-replay",
                "result-forged-replay",
                candidate =>
                {
                    callbacks++;
                    candidate.ReplayRecord = new MutableReplayRecord(
                        "operation:forged-replay",
                        "result-forged-replay",
                        InitialFingerprint,
                        "2222222222222222222222222222222222222222222222222222222222222222");
                    return ProfileCandidatePreparation.ExactReplay();
                });

            Assert.AreEqual(
                ProfileMutationStatus.PreparationRejected,
                result.Status);
            Assert.IsNull(result.Receipt);
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(1, fixture.Persistence.CheckCount);
            Assert.AreEqual(0, fixture.Persistence.PersistCount);
            Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            Assert.IsNull(
                ReadPublishedCandidate(fixture.Boundary).ReplayRecord);
        }

        [Test]
        public void MalformedPublishedReplayRecordIsRejected()
        {
            const string operationId = "operation:malformed-replay";
            const string resultId = "result-malformed-replay";
            var malformedFingerprints = new[]
            {
                new[] { string.Empty, InitialFingerprint },
                new[] { "not-a-fingerprint", InitialFingerprint },
                new[] { InitialFingerprint, string.Empty },
                new[] { InitialFingerprint, "not-a-fingerprint" }
            };

            foreach (string[] fingerprints in malformedFingerprints)
            {
                var fixture = Fixture.Create(
                    initialCandidate: new MutableCandidate
                    {
                        ProfileId = ProfileId,
                        VerifiedGenerationFingerprint = InitialFingerprint,
                        ReplayRecord = new MutableReplayRecord(
                            operationId,
                            resultId,
                            fingerprints[0],
                            fingerprints[1])
                    });
                ProfileMutationResult result = fixture.Boundary.TryMutate(
                    CurrentExpectation(fixture),
                    operationId,
                    resultId,
                    _ => ProfileCandidatePreparation.ExactReplay());
                Assert.AreEqual(
                    ProfileMutationStatus.PreparationRejected,
                    result.Status);
                Assert.IsNull(result.Receipt);
                Assert.AreEqual(1, fixture.Persistence.CheckCount);
                Assert.AreEqual(0, fixture.Persistence.PersistCount);
                Assert.AreEqual(0, fixture.Sink.Receipts.Count);
            }
        }

        [Test]
        public void ReplayRecordCannotCrossProfileOrOperationIdentity()
        {
            const string operationId = "operation:bound-replay";
            const string resultId = "result-bound-replay";
            var crossProfile = Fixture.Create(
                initialCandidate: new MutableCandidate
                {
                    ProfileId =
                        "alp_1123456789abcdef0123456789abcdef",
                    VerifiedGenerationFingerprint = InitialFingerprint,
                    ReplayRecord = new MutableReplayRecord(
                        operationId,
                        resultId,
                        InitialFingerprint,
                        "2222222222222222222222222222222222222222222222222222222222222222")
                });
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                crossProfile.Boundary.GetCurrentAuthority().Status);
            int crossProfileCallbacks = 0;
            ProfileMutationResult crossProfileReplay =
                crossProfile.Boundary.TryMutate(
                    ProfileAuthorityExpectation.From(
                        crossProfile.Boundary.GetCurrentAuthority()),
                    operationId,
                    resultId,
                    _ =>
                    {
                        crossProfileCallbacks++;
                        return ProfileCandidatePreparation.ExactReplay();
                    });
            Assert.AreEqual(
                ProfileMutationStatus.NotWritable,
                crossProfileReplay.Status);
            Assert.AreEqual(0, crossProfileCallbacks);

            var bound = Fixture.Create(
                initialCandidate: new MutableCandidate
                {
                    ProfileId = ProfileId,
                    VerifiedGenerationFingerprint = InitialFingerprint,
                    ReplayRecord = new MutableReplayRecord(
                        operationId,
                        resultId,
                        InitialFingerprint,
                        "2222222222222222222222222222222222222222222222222222222222222222")
                });
            ProfileMutationResult alteredOperation =
                bound.Boundary.TryMutate(
                    CurrentExpectation(bound),
                    "operation:other",
                    resultId,
                    _ => ProfileCandidatePreparation.ExactReplay());
            ProfileMutationResult alteredResult =
                bound.Boundary.TryMutate(
                    CurrentExpectation(bound),
                    operationId,
                    "result-other",
                    _ => ProfileCandidatePreparation.ExactReplay());

            Assert.AreEqual(
                ProfileMutationStatus.PreparationRejected,
                alteredOperation.Status);
            Assert.AreEqual(
                ProfileMutationStatus.PreparationRejected,
                alteredResult.Status);
            Assert.AreEqual(2, bound.Persistence.CheckCount);
            Assert.AreEqual(0, bound.Persistence.PersistCount);
            Assert.AreEqual(0, bound.Sink.Receipts.Count);
        }

        [Test]
        public void OpaqueOperationAndResultIdentitiesPreserveBoundedMultibyteText()
        {
            var fixture = Fixture.Create();

            ProfileMutationResult result = fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                "작업:save-authority-01",
                "결과:save-authority-01",
                candidate =>
                {
                    candidate.Value++;
                    return ProfileCandidatePreparation.Prepared();
                });

            Assert.AreEqual(ProfileMutationStatus.Committed, result.Status);
            Assert.AreEqual(
                "작업:save-authority-01",
                result.Receipt.OperationId);
            Assert.AreEqual(
                "결과:save-authority-01",
                result.Receipt.ResultId);
        }

        private static ProfileMutationResult Commit(Fixture fixture, int value) =>
            fixture.Boundary.TryMutate(
                CurrentExpectation(fixture),
                $"operation:{value:D3}",
                $"result-{value:D3}",
                candidate =>
                {
                    candidate.Value = value;
                    return ProfileCandidatePreparation.Prepared();
                });

        private static ProfileMutationResult CommitWithReplayRecord(
            Fixture fixture,
            int value)
        {
            ProfileAuthorityExpectation expectation =
                CurrentExpectation(fixture);
            string operationId = $"operation:{value:D3}";
            string resultId = $"result-{value:D3}";
            string payloadFingerprint =
                1001.ToString("x64", CultureInfo.InvariantCulture);
            return fixture.Boundary.TryMutate(
                expectation,
                operationId,
                resultId,
                candidate =>
                {
                    candidate.Value = value;
                    candidate.ReplayRecord = new MutableReplayRecord(
                        operationId,
                        resultId,
                        expectation.ExpectedGenerationFingerprint,
                        payloadFingerprint);
                    return ProfileCandidatePreparation.Prepared();
                });
        }

        private static ProfileAuthorityExpectation CurrentExpectation(
            Fixture fixture) =>
            ProfileAuthorityExpectation.From(
                fixture.Boundary.GetCurrentAuthority());

        private static MutableCandidate ReadPublishedCandidate(
            SerializedAuthorityMutationBoundary<MutableCandidate> boundary)
        {
            var field =
                typeof(SerializedAuthorityMutationBoundary<MutableCandidate>)
                    .GetField(
                        "_publishedCandidate",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (MutableCandidate)field.GetValue(boundary);
        }

        private sealed class Fixture
        {
            private Fixture(
                SerializedAuthorityMutationBoundary<MutableCandidate> boundary,
                FakePersistence persistence,
                RecordingReceiptSink sink)
            {
                Boundary = boundary;
                Persistence = persistence;
                Sink = sink;
            }

            internal SerializedAuthorityMutationBoundary<MutableCandidate>
                Boundary { get; }
            internal FakePersistence Persistence { get; }
            internal RecordingReceiptSink Sink { get; }

            internal static Fixture Create(
                ProfileWriteAuthoritySnapshot authority = null,
                AuthorityEpochAllocator allocator = null,
                FakePersistence persistence = null,
                RecordingReceiptSink sink = null,
                ulong initialPublicationSequence = 0,
                Action<ulong> beforeDrainRequest = null,
                IProfileMutationCandidateAdapter<MutableCandidate> adapter = null,
                ProcessAuthorityMutationCoordinator coordinator = null,
                Action beforeDispatcherRelease = null,
                MutableCandidate initialCandidate = null)
            {
                persistence = persistence ?? new FakePersistence();
                sink = sink ?? new RecordingReceiptSink();
                adapter = adapter ?? new MutableCandidateAdapter();
                allocator = allocator ?? new AuthorityEpochAllocator(
                    new IncrementingEpochSource());
                authority = authority ??
                            ProfileWriteAuthoritySnapshotFactory.Writable(
                                ProfileId,
                                InitialEpoch,
                                InitialFingerprint,
                                ProfileAuthoritySourceGeneration.Primary,
                                Array.Empty<string>());
                coordinator = coordinator ??
                              new ProcessAuthorityMutationCoordinator(
                                  initialPublicationSequence,
                                  null,
                                  beforeDispatcherRelease);
                initialCandidate = initialCandidate ?? new MutableCandidate
                {
                    ProfileId = authority.ProfileId,
                    VerifiedGenerationFingerprint =
                        authority.VerifiedGenerationFingerprint
                };
                var boundary =
                    SerializedAuthorityMutationBoundary<MutableCandidate>
                        .CreateForTesting(
                        authority,
                        initialCandidate,
                        adapter,
                        persistence,
                        allocator,
                        sink,
                        beforeDrainRequest,
                        coordinator);
                return new Fixture(boundary, persistence, sink);
            }
        }

        private sealed class MutableCandidate
        {
            internal string ProfileId { get; set; }
            internal string VerifiedGenerationFingerprint { get; set; }
            internal int Value { get; set; }
            internal MutableReplayRecord ReplayRecord { get; set; }
        }

        private sealed class MutableReplayRecord
        {
            internal MutableReplayRecord(
                string operationId,
                string resultId,
                string expectedGenerationFingerprint,
                string committedPayloadFingerprint)
            {
                OperationId = operationId;
                ResultId = resultId;
                ExpectedGenerationFingerprint =
                    expectedGenerationFingerprint;
                CommittedPayloadFingerprint =
                    committedPayloadFingerprint;
            }

            internal string OperationId { get; }
            internal string ResultId { get; }
            internal string ExpectedGenerationFingerprint { get; }
            internal string CommittedPayloadFingerprint { get; }
        }

        private static ProfileMutationReplayVerification VerifyMutableReplay(
            MutableCandidate candidate,
            string expectedProfileId,
            string expectedGenerationFingerprint,
            string operationId,
            string resultId)
        {
            MutableReplayRecord record = candidate?.ReplayRecord;
            if (candidate == null ||
                !string.Equals(
                    candidate.ProfileId,
                    expectedProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    candidate.VerifiedGenerationFingerprint,
                    expectedGenerationFingerprint,
                    StringComparison.Ordinal) ||
                record == null ||
                !string.Equals(
                    record.OperationId,
                    operationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.ResultId,
                    resultId,
                    StringComparison.Ordinal))
            {
                return ProfileMutationReplayVerification.Invalid(
                    "AL-SAVE-AUTH-REPLAY-NOT-VERIFIED");
            }

            return ProfileMutationReplayVerification.Verified(
                record.ExpectedGenerationFingerprint,
                record.CommittedPayloadFingerprint);
        }

        private sealed class MutableCandidateAdapter :
            IProfileMutationCandidateAdapter<MutableCandidate>
        {
            public MutableCandidate Clone(MutableCandidate source) =>
                source == null
                    ? new MutableCandidate()
                    : new MutableCandidate
                    {
                        ProfileId = source.ProfileId,
                        VerifiedGenerationFingerprint =
                            source.VerifiedGenerationFingerprint,
                        Value = source.Value,
                        ReplayRecord = source.ReplayRecord
                    };

            public ProfileCandidateValidationResult ValidatePublished(
                MutableCandidate candidate,
                string expectedProfileId,
                string expectedGenerationFingerprint) =>
                candidate == null ||
                !string.Equals(
                    candidate.ProfileId,
                    expectedProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    candidate.VerifiedGenerationFingerprint,
                    expectedGenerationFingerprint,
                    StringComparison.Ordinal)
                    ? ProfileCandidateValidationResult.Invalid(
                        "AL-SAVE-AUTH-CANDIDATE-BINDING")
                    : ProfileCandidateValidationResult.Valid();

            public ProfileCandidateValidationResult Validate(
                MutableCandidate candidate,
                string expectedProfileId) =>
                candidate == null ||
                !string.Equals(
                    candidate.ProfileId,
                    expectedProfileId,
                    StringComparison.Ordinal)
                    ? ProfileCandidateValidationResult.Invalid(
                        "AL-SAVE-AUTH-CANDIDATE-NULL")
                    : ProfileCandidateValidationResult.Valid();

            public ProfileMutationReplayVerification VerifyReplay(
                MutableCandidate publishedCandidate,
                string expectedProfileId,
                string expectedGenerationFingerprint,
                string operationId,
                string resultId) =>
                VerifyMutableReplay(
                    publishedCandidate,
                    expectedProfileId,
                    expectedGenerationFingerprint,
                    operationId,
                    resultId);
        }

        private enum ReplayAdapterFault
        {
            ReplayCloneThrows,
            PublishedValidationReturnsNull,
            VerificationReturnsNull,
            VerificationThrows
        }

        private sealed class ReplayFaultingCandidateAdapter :
            IProfileMutationCandidateAdapter<MutableCandidate>
        {
            private readonly ReplayAdapterFault _fault;
            private readonly MutableCandidateAdapter _inner =
                new MutableCandidateAdapter();
            private int _cloneCount;
            private int _publishedValidationCount;

            internal ReplayFaultingCandidateAdapter(
                ReplayAdapterFault fault)
            {
                _fault = fault;
            }

            public MutableCandidate Clone(MutableCandidate source)
            {
                int call = Interlocked.Increment(ref _cloneCount);
                if (_fault == ReplayAdapterFault.ReplayCloneThrows &&
                    call == 3)
                {
                    throw new InvalidOperationException(
                        "replay clone fault");
                }

                return _inner.Clone(source);
            }

            public ProfileCandidateValidationResult ValidatePublished(
                MutableCandidate candidate,
                string expectedProfileId,
                string expectedGenerationFingerprint)
            {
                int call = Interlocked.Increment(
                    ref _publishedValidationCount);
                if (_fault ==
                        ReplayAdapterFault.PublishedValidationReturnsNull &&
                    call == 2)
                {
                    return null;
                }

                return _inner.ValidatePublished(
                    candidate,
                    expectedProfileId,
                    expectedGenerationFingerprint);
            }

            public ProfileCandidateValidationResult Validate(
                MutableCandidate candidate,
                string expectedProfileId) =>
                _inner.Validate(candidate, expectedProfileId);

            public ProfileMutationReplayVerification VerifyReplay(
                MutableCandidate publishedCandidate,
                string expectedProfileId,
                string expectedGenerationFingerprint,
                string operationId,
                string resultId)
            {
                if (_fault ==
                    ReplayAdapterFault.VerificationReturnsNull)
                {
                    return null;
                }
                if (_fault == ReplayAdapterFault.VerificationThrows)
                {
                    throw new InvalidOperationException(
                        "replay verification fault");
                }

                return _inner.VerifyReplay(
                    publishedCandidate,
                    expectedProfileId,
                    expectedGenerationFingerprint,
                    operationId,
                    resultId);
            }
        }

        private sealed class RejectOnceCandidateAdapter :
            IProfileMutationCandidateAdapter<MutableCandidate>
        {
            private int _validations;

            public MutableCandidate Clone(MutableCandidate source) =>
                source == null
                    ? new MutableCandidate()
                    : new MutableCandidate
                    {
                        ProfileId = source.ProfileId,
                        VerifiedGenerationFingerprint =
                            source.VerifiedGenerationFingerprint,
                        Value = source.Value,
                        ReplayRecord = source.ReplayRecord
                    };

            public ProfileCandidateValidationResult ValidatePublished(
                MutableCandidate candidate,
                string expectedProfileId,
                string expectedGenerationFingerprint) =>
                candidate != null &&
                string.Equals(
                    candidate.ProfileId,
                    expectedProfileId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    candidate.VerifiedGenerationFingerprint,
                    expectedGenerationFingerprint,
                    StringComparison.Ordinal)
                    ? ProfileCandidateValidationResult.Valid()
                    : ProfileCandidateValidationResult.Invalid(
                        "AL-SAVE-AUTH-CANDIDATE-BINDING");

            public ProfileCandidateValidationResult Validate(
                MutableCandidate candidate,
                string expectedProfileId) =>
                Interlocked.Increment(ref _validations) == 1
                    ? ProfileCandidateValidationResult.Invalid(
                        "AL-SAVE-AUTH-CANDIDATE-REJECTED")
                    : ProfileCandidateValidationResult.Valid();

            public ProfileMutationReplayVerification VerifyReplay(
                MutableCandidate publishedCandidate,
                string expectedProfileId,
                string expectedGenerationFingerprint,
                string operationId,
                string resultId) =>
                VerifyMutableReplay(
                    publishedCandidate,
                    expectedProfileId,
                    expectedGenerationFingerprint,
                    operationId,
                    resultId);
        }

        private sealed class ForbiddenCandidateAccessAdapter :
            IProfileMutationCandidateAdapter<MutableCandidate>
        {
            private int _accessCount;

            internal int AccessCount => Volatile.Read(ref _accessCount);

            public MutableCandidate Clone(MutableCandidate source)
            {
                Interlocked.Increment(ref _accessCount);
                throw new InvalidOperationException(
                    "non-writable candidate must not be cloned");
            }

            public ProfileCandidateValidationResult ValidatePublished(
                MutableCandidate candidate,
                string expectedProfileId,
                string expectedGenerationFingerprint)
            {
                Interlocked.Increment(ref _accessCount);
                throw new InvalidOperationException(
                    "non-writable candidate must not be validated");
            }

            public ProfileCandidateValidationResult Validate(
                MutableCandidate candidate,
                string expectedProfileId)
            {
                Interlocked.Increment(ref _accessCount);
                throw new InvalidOperationException(
                    "non-writable candidate must not be validated");
            }

            public ProfileMutationReplayVerification VerifyReplay(
                MutableCandidate publishedCandidate,
                string expectedProfileId,
                string expectedGenerationFingerprint,
                string operationId,
                string resultId)
            {
                Interlocked.Increment(ref _accessCount);
                throw new InvalidOperationException(
                    "non-writable candidate must not be replayed");
            }
        }

        private enum CandidateAdapterFault
        {
            CommittedCloneThrows,
            CommittedValidationThrows
        }

        private sealed class FaultingCandidateAdapter :
            IProfileMutationCandidateAdapter<MutableCandidate>
        {
            private readonly CandidateAdapterFault _fault;
            private int _cloneCount;
            private int _publishedValidationCount;

            internal FaultingCandidateAdapter(CandidateAdapterFault fault)
            {
                _fault = fault;
            }

            public MutableCandidate Clone(MutableCandidate source)
            {
                int call = Interlocked.Increment(ref _cloneCount);
                if (_fault == CandidateAdapterFault.CommittedCloneThrows &&
                    call == 4)
                {
                    throw new InvalidOperationException(
                        "committed clone fault");
                }

                return source == null
                    ? new MutableCandidate()
                    : new MutableCandidate
                    {
                        ProfileId = source.ProfileId,
                        VerifiedGenerationFingerprint =
                            source.VerifiedGenerationFingerprint,
                        Value = source.Value,
                        ReplayRecord = source.ReplayRecord
                    };
            }

            public ProfileCandidateValidationResult ValidatePublished(
                MutableCandidate candidate,
                string expectedProfileId,
                string expectedGenerationFingerprint)
            {
                int call = Interlocked.Increment(
                    ref _publishedValidationCount);
                if (_fault ==
                        CandidateAdapterFault.CommittedValidationThrows &&
                    call == 2)
                {
                    throw new InvalidOperationException(
                        "committed validation fault");
                }

                return candidate != null &&
                       string.Equals(
                           candidate.ProfileId,
                           expectedProfileId,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           candidate.VerifiedGenerationFingerprint,
                           expectedGenerationFingerprint,
                           StringComparison.Ordinal)
                    ? ProfileCandidateValidationResult.Valid()
                    : ProfileCandidateValidationResult.Invalid(
                        "AL-SAVE-AUTH-CANDIDATE-BINDING");
            }

            public ProfileCandidateValidationResult Validate(
                MutableCandidate candidate,
                string expectedProfileId)
            {
                return candidate != null &&
                       string.Equals(
                           candidate.ProfileId,
                           expectedProfileId,
                           StringComparison.Ordinal)
                    ? ProfileCandidateValidationResult.Valid()
                    : ProfileCandidateValidationResult.Invalid(
                        "AL-SAVE-AUTH-CANDIDATE-INVALID");
            }

            public ProfileMutationReplayVerification VerifyReplay(
                MutableCandidate publishedCandidate,
                string expectedProfileId,
                string expectedGenerationFingerprint,
                string operationId,
                string resultId) =>
                VerifyMutableReplay(
                    publishedCandidate,
                    expectedProfileId,
                    expectedGenerationFingerprint,
                    operationId,
                    resultId);
        }

        private enum FakePersistenceOutcome
        {
            Committed,
            Rejected,
            VerifiedRollback,
            CommitUncertain
        }

        private enum LedgerCheckFault
        {
            None,
            ReturnsNull,
            Throws,
            UnknownStatus
        }

        private sealed class FakePersistence :
            IProfileMutationPersistence<MutableCandidate>
        {
            private int _commitNumber;

            internal int CheckCount { get; private set; }
            internal int PersistCount { get; private set; }
            internal int LastPersistedValue { get; private set; }
            internal ProfileMutationCommitContext LastCommitContext
            {
                get;
                private set;
            }
            internal ProfileMutationCommitContext LastRecheckContext
            {
                get;
                private set;
            }
            internal bool StaleOnSecondCheck { get; set; }
            internal bool InitialCheckUnavailable { get; set; }
            internal int FaultCheckNumber { get; set; }
            internal LedgerCheckFault CheckFault { get; set; }
            internal bool ThrowOnPersist { get; set; }
            internal bool ReturnNull { get; set; }
            internal bool ReturnNullCommittedCandidate { get; set; }
            internal string CommittedProfileIdOverride { get; set; }
            internal string CommittedGenerationFingerprintOverride
            {
                get;
                set;
            }
            internal string
                CommittedCandidateGenerationFingerprintOverride
            {
                get;
                set;
            }
            internal string CommittedPayloadFingerprintOverride
            {
                get;
                set;
            }
            internal ProfileAuthoritySourceGeneration
                CommittedSourceGeneration { get; set; } =
                    ProfileAuthoritySourceGeneration.Primary;
            internal FakePersistenceOutcome NextOutcome { get; set; }

            public ProfilePersistenceAuthorityCheck RecheckAuthority(
                ProfileMutationCommitContext context)
            {
                CheckCount++;
                LastRecheckContext = context;
                if (FaultCheckNumber == CheckCount)
                {
                    switch (CheckFault)
                    {
                        case LedgerCheckFault.ReturnsNull:
                            return null;
                        case LedgerCheckFault.Throws:
                            throw new InvalidOperationException(
                                "ledger recheck fault");
                        case LedgerCheckFault.UnknownStatus:
                            return new ProfilePersistenceAuthorityCheck(
                                (ProfilePersistenceAuthorityStatus)999,
                                "AL-SAVE-AUTH-PERSISTENCE-UNKNOWN");
                    }
                }
                if (InitialCheckUnavailable && CheckCount == 1)
                {
                    return ProfilePersistenceAuthorityCheck.Unavailable(
                        "AL-SAVE-AUTH-PERSISTENCE-UNAVAILABLE");
                }
                return StaleOnSecondCheck && CheckCount % 2 == 0
                    ? ProfilePersistenceAuthorityCheck.Stale()
                    : ProfilePersistenceAuthorityCheck.Current();
            }

            public ProfileCandidatePersistenceResult<MutableCandidate>
                PersistAndVerify(
                    MutableCandidate candidate,
                    ProfileMutationCommitContext context)
            {
                PersistCount++;
                LastPersistedValue = candidate.Value;
                LastCommitContext = context;
                if (ThrowOnPersist)
                    throw new InvalidOperationException("post-I/O failure");
                if (ReturnNull)
                    return null;

                switch (NextOutcome)
                {
                    case FakePersistenceOutcome.Rejected:
                        NextOutcome = FakePersistenceOutcome.Committed;
                        return ProfileCandidatePersistenceResult<MutableCandidate>
                            .Rejected("AL-SAVE-AUTH-PERSISTENCE-REJECTED");
                    case FakePersistenceOutcome.VerifiedRollback:
                        NextOutcome = FakePersistenceOutcome.Committed;
                        return ProfileCandidatePersistenceResult<MutableCandidate>
                            .VerifiedRollback(
                                "AL-SAVE-AUTH-VERIFIED-ROLLBACK");
                    case FakePersistenceOutcome.CommitUncertain:
                        NextOutcome = FakePersistenceOutcome.Committed;
                        return ProfileCandidatePersistenceResult<MutableCandidate>
                            .CommitUncertain(
                                "AL-SAVE-AUTH-COMMIT-UNCERTAIN");
                    default:
                        int number = Interlocked.Increment(ref _commitNumber);
                        string committedGeneration =
                            CommittedGenerationFingerprintOverride ??
                            number.ToString(
                                "x64",
                                CultureInfo.InvariantCulture);
                        return ProfileCandidatePersistenceResult<MutableCandidate>
                            .Committed(
                                ReturnNullCommittedCandidate
                                    ? null
                                    : new MutableCandidate
                                    {
                                        ProfileId =
                                            CommittedProfileIdOverride ??
                                            candidate.ProfileId,
                                        VerifiedGenerationFingerprint =
                                            CommittedCandidateGenerationFingerprintOverride ??
                                            committedGeneration,
                                        Value = candidate.Value,
                                        ReplayRecord = candidate.ReplayRecord
                                    },
                                committedGeneration,
                                CommittedPayloadFingerprintOverride ??
                                    (number + 1000).ToString(
                                        "x64",
                                        CultureInfo.InvariantCulture),
                                CommittedSourceGeneration);
                }
            }
        }

        private class RecordingReceiptSink : IProfileMutationReceiptSink
        {
            private readonly object _gate = new object();
            private readonly List<ProfileMutationReceipt> _receipts =
                new List<ProfileMutationReceipt>();

            internal IReadOnlyList<ProfileMutationReceipt> Receipts
            {
                get
                {
                    lock (_gate)
                    {
                        return _receipts.ToArray();
                    }
                }
            }

            public virtual void Publish(ProfileMutationReceipt receipt)
            {
                lock (_gate)
                {
                    _receipts.Add(receipt);
                }
            }
        }

        private sealed class BlockingReceiptSink : RecordingReceiptSink
        {
            internal readonly ManualResetEventSlim FirstEntered =
                new ManualResetEventSlim(false);
            internal readonly ManualResetEventSlim ReleaseFirst =
                new ManualResetEventSlim(false);
            internal readonly ManualResetEventSlim AllDelivered =
                new ManualResetEventSlim(false);
            internal bool WaitTimedOut { get; private set; }

            public override void Publish(ProfileMutationReceipt receipt)
            {
                if (receipt.PublicationSequence == 1)
                {
                    FirstEntered.Set();
                    if (!ReleaseFirst.Wait(TimeSpan.FromSeconds(10)))
                        WaitTimedOut = true;
                }

                base.Publish(receipt);
                if (receipt.PublicationSequence ==
                    SaveAuthorityTechnicalLimits.ReceiptCapacity)
                {
                    AllDelivered.Set();
                }
            }
        }

        private sealed class ThrowOnceReceiptSink : RecordingReceiptSink
        {
            private int _attempts;
            internal readonly List<ulong> AttemptedSequences = new List<ulong>();

            public override void Publish(ProfileMutationReceipt receipt)
            {
                AttemptedSequences.Add(receipt.PublicationSequence);
                if (Interlocked.Increment(ref _attempts) == 1)
                    throw new InvalidOperationException("subscriber failure");
                base.Publish(receipt);
            }
        }

        private sealed class SignalingReceiptSink : RecordingReceiptSink
        {
            private readonly int _expected;
            private readonly ManualResetEventSlim _completed;

            internal SignalingReceiptSink(
                int expected,
                ManualResetEventSlim completed)
            {
                _expected = expected;
                _completed = completed;
            }

            public override void Publish(ProfileMutationReceipt receipt)
            {
                base.Publish(receipt);
                if (Receipts.Count >= _expected)
                    _completed.Set();
            }
        }

        private sealed class ReentrantReceiptSink : RecordingReceiptSink
        {
            private int _reentered;
            internal Func<ProfileMutationResult> Reenter { get; set; }
            internal ProfileMutationResult ReentryResult { get; private set; }

            public override void Publish(ProfileMutationReceipt receipt)
            {
                base.Publish(receipt);
                if (Interlocked.CompareExchange(
                        ref _reentered,
                        1,
                        0) == 0)
                {
                    ReentryResult = Reenter();
                }
            }
        }

        private sealed class ManualContinuationScheduler :
            IAuthorityReceiptContinuationScheduler
        {
            private readonly object _gate = new object();
            private readonly Queue<Action> _continuations =
                new Queue<Action>();
            private bool _overlappingScheduleRejected;

            internal int PendingCount
            {
                get
                {
                    lock (_gate)
                        return _continuations.Count;
                }
            }

            public bool TrySchedule(Action continuation)
            {
                lock (_gate)
                {
                    if (_continuations.Count >= 1)
                    {
                        _overlappingScheduleRejected = true;
                        return false;
                    }
                    _continuations.Enqueue(continuation);
                    return true;
                }
            }

            internal void RunNext()
            {
                Action continuation;
                lock (_gate)
                {
                    Assert.IsFalse(
                        _overlappingScheduleRejected,
                        "Only one continuation may be scheduled.");
                    continuation = _continuations.Dequeue();
                }
                continuation();
            }
        }

        private sealed class ThrowOnceThenInlineScheduler :
            IAuthorityReceiptContinuationScheduler
        {
            internal int CallCount { get; private set; }

            public bool TrySchedule(Action continuation)
            {
                CallCount++;
                if (CallCount == 1)
                {
                    throw new InvalidOperationException(
                        "scheduler failure");
                }

                continuation();
                return true;
            }
        }

        private sealed class BlockingFailOnceScheduler :
            IAuthorityReceiptContinuationScheduler
        {
            internal readonly ManualResetEventSlim FirstCallEntered =
                new ManualResetEventSlim(false);
            internal readonly ManualResetEventSlim ReleaseFirstCall =
                new ManualResetEventSlim(false);

            internal int CallCount { get; private set; }
            internal bool WaitTimedOut { get; private set; }

            public bool TrySchedule(Action continuation)
            {
                CallCount++;
                if (CallCount != 1)
                {
                    continuation();
                    return true;
                }

                FirstCallEntered.Set();
                if (!ReleaseFirstCall.Wait(TimeSpan.FromSeconds(5)))
                    WaitTimedOut = true;
                return false;
            }
        }

        private sealed class FixedEpochSource : IAuthorityEpochCandidateSource
        {
            private readonly Queue<string> _values;

            internal FixedEpochSource(params string[] values)
            {
                _values = new Queue<string>(values);
            }

            public bool TryGetNextCandidate(out string candidate)
            {
                if (_values.Count == 0)
                {
                    candidate = string.Empty;
                    return false;
                }

                candidate = _values.Dequeue();
                return true;
            }
        }

        private sealed class IncrementingEpochSource :
            IAuthorityEpochCandidateSource
        {
            private long _counter = 1;
            internal int CallCount { get; private set; }

            public bool TryGetNextCandidate(out string candidate)
            {
                CallCount++;
                ulong value = (ulong)Interlocked.Increment(ref _counter);
                candidate = "0123456789abcdef" +
                            value.ToString("x16", CultureInfo.InvariantCulture);
                return true;
            }
        }
    }
}
