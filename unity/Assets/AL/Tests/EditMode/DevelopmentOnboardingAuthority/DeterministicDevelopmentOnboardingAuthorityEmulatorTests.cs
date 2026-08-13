#if UNITY_EDITOR && UNITY_INCLUDE_TESTS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AL.Editor.Development.OnboardingAuthority;
using NUnit.Framework;

namespace AL.Tests.EditMode.DevelopmentOnboardingAuthority
{
    public sealed class DeterministicDevelopmentOnboardingAuthorityEmulatorTests
    {
        private const string AuthorityInstance = "dev-instance-01";
        private const string ProjectionInstance = "dev-projection-01";

        [Test]
        public void FixedWidthValuesRejectWrongLengthsAndDefensivelyCopy()
        {
            Assert.Throws<ArgumentNullException>(() => new Commitment32(null));
            Assert.Throws<ArgumentException>(() => new Commitment32(new byte[31]));
            Assert.Throws<ArgumentException>(() => new Digest32(new byte[33]));

            var commitmentBytes = RepeatBytes(0x21);
            var digestBytes = RepeatBytes(0x31);
            var commitment = new Commitment32(commitmentBytes);
            var digest = new Digest32(digestBytes);
            commitmentBytes[0] = 0xff;
            digestBytes[0] = 0xff;
            Assert.AreEqual("21", commitment.ToHex().Substring(0, 2));
            Assert.AreEqual("31", digest.ToHex().Substring(0, 2));

            var commitmentCopy = commitment.ToArray();
            var digestCopy = digest.ToArray();
            commitmentCopy[1] = 0xff;
            digestCopy[1] = 0xff;
            Assert.AreEqual("2121", commitment.ToHex().Substring(0, 4));
            Assert.AreEqual("3131", digest.ToHex().Substring(0, 4));
        }

        [Test]
        public void CanonicalFrameSeparatesRawBytesStringsAndDelimiters()
        {
            var raw = DevelopmentFrameV1.Encode("AL.TEST.v1", new byte[] { 0x31 });
            var text = DevelopmentFrameV1.Encode("AL.TEST.v1", Encoding.UTF8.GetBytes("31"));
            var splitA = DevelopmentFrameV1.Encode(
                "AL.TEST.v1",
                Encoding.UTF8.GetBytes("a|b"),
                Encoding.UTF8.GetBytes("c"));
            var splitB = DevelopmentFrameV1.Encode(
                "AL.TEST.v1",
                Encoding.UTF8.GetBytes("a"),
                Encoding.UTF8.GetBytes("b|c"));

            CollectionAssert.AreNotEqual(raw, text);
            CollectionAssert.AreNotEqual(splitA, splitB);
            CollectionAssert.AreEqual(
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 42 },
                DevelopmentFrameV1.UInt64Bytes(42));
            Assert.IsTrue(DevelopmentFrameV1.TryDecode(raw, "AL.TEST.v1", 1, out var decoded));
            CollectionAssert.AreEqual(new byte[] { 0x31 }, decoded[0]);
        }

        [Test]
        public void ReceiptAndProjectionGoldenVectorsAreStable()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance);
            var request = Request(1, 2, 3, 4, 5, 0);
            var commit = authority.TryCommit(request);

            Assert.AreEqual(DevelopmentOnboardingCommitState.Committed, commit.State);
            Assert.AreEqual(
                "devrcpt_4a6a37e2927ec6367843066c8ffc8fe39cbb0f4385ea12775abd2356cb5c5ef7",
                commit.Receipt.ReceiptId);
            Assert.AreEqual(
                "32000474610ba82fd8aae546bf3edcb47c4b452600beadd150335806f4dbfe63",
                commit.Receipt.BodyDigest.ToHex());

            var verified = authority.Verify(commit.Receipt, request, commit.Receipt.Handle);
            var projection = new DeterministicDevelopmentLocalProjectionEmulator(ProjectionInstance);
            var projected = projection.TryProject(Commitment(6), 0, verified);
            Assert.AreEqual(DevelopmentProjectionState.Projected, projected.State);
            Assert.AreEqual(
                "devmarker_a491f78457252698c27004649352eedafc794bb07868f00fb1f0688fa0a466ac",
                projected.Marker.MarkerId);
            Assert.AreEqual(
                "b75d1a21bb4b8d7d666ceaa3220dd333d86f7d13f7998e6086e59d3837174dc0",
                projected.Marker.MarkerDigest.ToHex());
        }

        [Test]
        public void EquivalentInjectedInstancesProduceIdenticalReceiptsAndState()
        {
            var request = Request(10, 11, 12, 13, 14, 0);
            var left = new DeterministicDevelopmentOnboardingAuthorityEmulator("same-instance");
            var right = new DeterministicDevelopmentOnboardingAuthorityEmulator("same-instance");
            var other = new DeterministicDevelopmentOnboardingAuthorityEmulator("other-instance");

            var leftReceipt = left.TryCommit(request).Receipt;
            var rightReceipt = right.TryCommit(request).Receipt;
            var otherReceipt = other.TryCommit(request).Receipt;
            Assert.AreEqual(leftReceipt.ReceiptId, rightReceipt.ReceiptId);
            Assert.AreEqual(leftReceipt.BodyDigest, rightReceipt.BodyDigest);
            CollectionAssert.AreEqual(left.CaptureRetainedState(), right.CaptureRetainedState());
            Assert.AreNotEqual(leftReceipt.ReceiptId, otherReceipt.ReceiptId);
            CollectionAssert.AreNotEqual(left.CaptureRetainedState(), other.CaptureRetainedState());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" bad")]
        [TestCase("bad/slash")]
        public void InvalidInstanceIdsReject(string instanceId)
        {
            Assert.Throws<ArgumentException>(
                () => new DeterministicDevelopmentOnboardingAuthorityEmulator(instanceId));
            Assert.Throws<ArgumentException>(
                () => new DeterministicDevelopmentLocalProjectionEmulator(instanceId));
        }

        [Test]
        public void AvailabilityFixturesAreDeterministicBoundedAndCallerMutationProof()
        {
            var taken = new[] { Commitment(20) };
            var unavailable = new[] { Commitment(21) };
            var fixtures = new DevelopmentHandleAvailabilityFixtures(taken, unavailable);
            taken[0] = Commitment(22);
            unavailable[0] = Commitment(23);
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("fixtures", fixtures);

            Assert.AreEqual(
                DevelopmentHandleAvailabilityState.Taken,
                authority.CheckHandle(Commitment(1), Commitment(20), 0).State);
            Assert.AreEqual(
                DevelopmentHandleAvailabilityState.Unavailable,
                authority.CheckHandle(Commitment(1), Commitment(21), 0).State);
            Assert.AreEqual(
                DevelopmentHandleAvailabilityState.Available,
                authority.CheckHandle(Commitment(1), Commitment(22), 0).State);
            Assert.Throws<ArgumentException>(
                () => new DevelopmentHandleAvailabilityFixtures(
                    new[] { Commitment(24), Commitment(24) },
                    null));
            Assert.Throws<ArgumentException>(
                () => new DevelopmentHandleAvailabilityFixtures(
                    new[] { Commitment(25) },
                    new[] { Commitment(25) }));
            Assert.Throws<ArgumentException>(
                () => new DevelopmentHandleAvailabilityFixtures(
                    Enumerable.Range(1, 65).Select(value => Commitment((byte)value)),
                    null));
        }

        [Test]
        public void InvalidAndStaleAvailabilityAreZeroMutation()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance);
            var before = authority.CaptureRetainedState();
            Assert.AreEqual(
                DevelopmentHandleAvailabilityState.InvalidInput,
                authority.CheckHandle(default, Commitment(30), 0).State);
            Assert.AreEqual(
                DevelopmentHandleAvailabilityState.StaleGeneration,
                authority.CheckHandle(Commitment(31), Commitment(30), 1).State);
            CollectionAssert.AreEqual(before, authority.CaptureRetainedState());
        }

        [Test]
        public void FirstCommitBindsFullTupleAndReplayPrecedesGenerationAndCapacity()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance);
            var request = Request(31, 32, 33, 34, 35, 0);
            var first = authority.TryCommit(request);
            Assert.AreEqual(DevelopmentOnboardingCommitState.Committed, first.State);
            Assert.AreEqual(1UL, first.Receipt.CommittedGeneration);
            Assert.AreEqual(1UL, first.Receipt.AuthorityRevision);

            var replay = authority.TryCommit(request);
            Assert.AreEqual(DevelopmentOnboardingCommitState.ReplayCommitted, replay.State);
            Assert.AreSame(first.Receipt, replay.Receipt);
            Assert.AreEqual(
                DevelopmentHandleAvailabilityState.StaleGeneration,
                authority.CheckHandle(request.AuthorityScopeCommitment, Commitment(36), 0).State);
        }

        [TestCase("fingerprint")]
        [TestCase("core")]
        [TestCase("handle")]
        [TestCase("generation")]
        public void SameOperationAnyTupleDriftIsCollisionBeforeGeneration(string field)
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance);
            var original = Request(40, 41, 42, 43, 44, 0);
            Assert.AreEqual(DevelopmentOnboardingCommitState.Committed, authority.TryCommit(original).State);
            var before = authority.CaptureRetainedState();
            var changed = Mutate(original, field);

            var collision = authority.TryCommit(changed);
            Assert.AreEqual(DevelopmentOnboardingCommitState.Collision, collision.State);
            Assert.AreEqual(DevelopmentAuthorityFailure.Collision, collision.Failure);
            CollectionAssert.AreEqual(before, authority.CaptureRetainedState());
            Assert.AreEqual(
                DevelopmentOnboardingReconcileState.Collision,
                authority.Reconcile(changed).State);
        }

        [Test]
        public void UnseenStaleOperationAndUnavailableFixtureLeaveNoBinding()
        {
            var unavailable = Commitment(51);
            var fixtures = new DevelopmentHandleAvailabilityFixtures(null, new[] { unavailable });
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance, fixtures);
            var before = authority.CaptureRetainedState();

            Assert.AreEqual(
                DevelopmentOnboardingCommitState.StaleGeneration,
                authority.TryCommit(Request(50, 51, 52, 53, 54, 1)).State);
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.AuthorityUnavailable,
                authority.TryCommit(new DevelopmentOnboardingCommitRequest(
                    Commitment(50), Commitment(51), Commitment(52), Commitment(53), unavailable, 0)).State);
            CollectionAssert.AreEqual(before, authority.CaptureRetainedState());
        }

        [Test]
        public void TakenTerminalReplaysAndScopeIsolationIsNondisclosing()
        {
            var taken = Commitment(61);
            var fixtures = new DevelopmentHandleAvailabilityFixtures(new[] { taken }, null);
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance, fixtures);
            var request = new DevelopmentOnboardingCommitRequest(
                Commitment(60), Commitment(62), Commitment(63), Commitment(64), taken, 0);

            Assert.AreEqual(
                DevelopmentOnboardingCommitState.TerminalHandleTaken,
                authority.TryCommit(request).State);
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.ReplayTerminalHandleTaken,
                authority.TryCommit(request).State);
            Assert.AreEqual(
                DevelopmentOnboardingReconcileState.NotFound,
                authority.Reconcile(new DevelopmentOnboardingCommitRequest(
                    Commitment(65), request.OperationCommitment, request.SemanticRequestFingerprint,
                    request.OpaqueCompiledCoreDigest, request.NormalizedHandleCommitment, 0)).State);
        }

        [Test]
        public void ReceiptVerifierBindsCallerObjectAndEveryExpectedField()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance);
            var request = Request(70, 71, 72, 73, 74, 0);
            var stored = authority.TryCommit(request).Receipt;
            var valueClone = CloneReceipt(stored);
            Assert.IsTrue(authority.Verify(valueClone, request, stored.Handle).IsValid);

            var tamperedBody = new DevelopmentOnboardingAuthorityReceipt(
                stored.AuthorityInstanceId,
                stored.ReceiptId,
                stored.Request,
                stored.CommittedGeneration,
                stored.AuthorityRevision,
                stored.ContractVersion,
                new Digest32(RepeatBytes(0xee)));
            Assert.IsFalse(authority.Verify(tamperedBody, request, stored.Handle).IsValid);
            Assert.IsFalse(authority.Verify(valueClone, Mutate(request, "handle"), stored.Handle).IsValid);
            Assert.IsFalse(authority.Verify(
                valueClone,
                request,
                new DevelopmentReceiptHandle(
                    stored.AuthorityInstanceId,
                    stored.ContractVersion,
                    stored.ReceiptId,
                    new Digest32(RepeatBytes(0xef)))).IsValid);
        }

        [Test]
        public void ReceiptTamperMatrixFailsClosedWithoutSubstitution()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance);
            var request = Request(75, 76, 77, 78, 79, 0);
            var stored = authority.TryCommit(request).Receipt;
            var candidates = new[]
            {
                new DevelopmentOnboardingAuthorityReceipt("other-instance", stored.ReceiptId, stored.Request, 1, 1, stored.ContractVersion, stored.BodyDigest),
                new DevelopmentOnboardingAuthorityReceipt(stored.AuthorityInstanceId, "devrcpt_" + new string('a', 64), stored.Request, 1, 1, stored.ContractVersion, stored.BodyDigest),
                new DevelopmentOnboardingAuthorityReceipt(stored.AuthorityInstanceId, stored.ReceiptId, Mutate(stored.Request, "fingerprint"), 1, 1, stored.ContractVersion, stored.BodyDigest),
                new DevelopmentOnboardingAuthorityReceipt(stored.AuthorityInstanceId, stored.ReceiptId, stored.Request, 2, 1, stored.ContractVersion, stored.BodyDigest),
                new DevelopmentOnboardingAuthorityReceipt(stored.AuthorityInstanceId, stored.ReceiptId, stored.Request, 1, 2, stored.ContractVersion, stored.BodyDigest),
                new DevelopmentOnboardingAuthorityReceipt(stored.AuthorityInstanceId, stored.ReceiptId, stored.Request, 1, 1, "OTHER", stored.BodyDigest),
                new DevelopmentOnboardingAuthorityReceipt(stored.AuthorityInstanceId, stored.ReceiptId, stored.Request, 1, 1, stored.ContractVersion, new Digest32(RepeatBytes(0xf0)))
            };

            foreach (var candidate in candidates)
            {
                var result = authority.Verify(candidate, request, stored.Handle);
                Assert.IsFalse(result.IsValid, candidate.ReceiptId);
                Assert.IsNull(result.Receipt);
            }
        }

        [Test]
        public void VerifiedSuccessConstructionIsNotPublic()
        {
            AssertNoPublicSuccessFactory(typeof(VerifiedDevelopmentReceipt));
            AssertNoPublicSuccessFactory(typeof(VerifiedDevelopmentProjection));
        }

        [Test]
        public void ProjectionStoreIsIndependentAndUsesReplayBeforeCas()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator(AuthorityInstance);
            var request = Request(80, 81, 82, 83, 84, 0);
            var receipt = authority.TryCommit(request).Receipt;
            var verified = authority.Verify(receipt, request, receipt.Handle);
            var projection = new DeterministicDevelopmentLocalProjectionEmulator(ProjectionInstance);
            var authorityBefore = authority.CaptureRetainedState();
            var projectionBefore = projection.CaptureRetainedState();

            var first = projection.TryProject(Commitment(85), 0, verified);
            Assert.AreEqual(DevelopmentProjectionState.Projected, first.State);
            Assert.AreEqual(1UL, first.Marker.ResultingLocalProjectionRevision);
            var afterFirst = projection.CaptureRetainedState();
            var replay = projection.TryProject(Commitment(85), 0, verified);
            Assert.AreEqual(DevelopmentProjectionState.ReplayProjected, replay.State);
            Assert.AreSame(first.Marker, replay.Marker);
            CollectionAssert.AreEqual(afterFirst, projection.CaptureRetainedState());
            Assert.AreEqual(
                DevelopmentProjectionState.Collision,
                projection.TryProject(Commitment(85), 1, verified).State);
            CollectionAssert.AreEqual(afterFirst, projection.CaptureRetainedState());
            CollectionAssert.AreEqual(authorityBefore, authority.CaptureRetainedState());
            CollectionAssert.AreNotEqual(projectionBefore, projection.CaptureRetainedState());
        }

        [Test]
        public void ProjectionCasOwnershipAndMarkerVerificationFailClosed()
        {
            var authorityA = new DeterministicDevelopmentOnboardingAuthorityEmulator("authority-a");
            var authorityB = new DeterministicDevelopmentOnboardingAuthorityEmulator("authority-b");
            var requestA = Request(90, 91, 92, 93, 94, 0);
            var requestB = Request(95, 96, 97, 98, 99, 0);
            var receiptA = authorityA.TryCommit(requestA).Receipt;
            var receiptB = authorityB.TryCommit(requestB).Receipt;
            var verifiedA = authorityA.Verify(receiptA, requestA, receiptA.Handle);
            var verifiedB = authorityB.Verify(receiptB, requestB, receiptB.Handle);
            var projection = new DeterministicDevelopmentLocalProjectionEmulator(ProjectionInstance);

            var first = projection.TryProject(Commitment(100), 0, verifiedA);
            Assert.AreEqual(DevelopmentProjectionState.Projected, first.State);
            Assert.AreEqual(
                DevelopmentProjectionState.StaleRevision,
                projection.TryProject(Commitment(100), 0, verifiedB).State);
            Assert.AreEqual(
                DevelopmentProjectionState.ReceiptOwnedByOtherProfile,
                projection.TryProject(Commitment(101), 0, verifiedA).State);
            Assert.AreEqual(
                DevelopmentProjectionState.NotFound,
                projection.ReconcileProjection(Commitment(101), receiptA.Handle).State);

            Assert.IsTrue(projection.Verify(
                CloneMarker(first.Marker),
                Commitment(100),
                receiptA.Handle,
                0,
                first.Marker.Handle).IsValid);
            var tamperedMarker = new DevelopmentProjectionMarker(
                first.Marker.ProjectionInstanceId,
                first.Marker.MarkerId,
                first.Marker.LocalProfileScopeCommitment,
                first.Marker.ReceiptHandle,
                first.Marker.ExpectedLocalProjectionRevision,
                first.Marker.ResultingLocalProjectionRevision,
                first.Marker.MarkerRevision,
                first.Marker.ContractVersion,
                new Digest32(RepeatBytes(0xfa)));
            Assert.IsFalse(projection.Verify(
                tamperedMarker,
                Commitment(100),
                receiptA.Handle,
                0,
                first.Marker.Handle).IsValid);

            var marker = first.Marker;
            var markerMutations = new[]
            {
                NewMarker(marker, projectionInstanceId: "other-projection"),
                NewMarker(marker, markerId: "devmarker_" + new string('a', 64)),
                NewMarker(marker, localScope: Commitment(102)),
                NewMarker(marker, receiptHandle: new DevelopmentReceiptHandle(
                    "other-authority", marker.ReceiptHandle.ContractVersion,
                    marker.ReceiptHandle.ReceiptId, marker.ReceiptHandle.BodyDigest)),
                NewMarker(marker, receiptHandle: new DevelopmentReceiptHandle(
                    marker.ReceiptHandle.AuthorityInstanceId, marker.ReceiptHandle.ContractVersion,
                    "devrcpt_" + new string('b', 64), marker.ReceiptHandle.BodyDigest)),
                NewMarker(marker, receiptHandle: new DevelopmentReceiptHandle(
                    marker.ReceiptHandle.AuthorityInstanceId, marker.ReceiptHandle.ContractVersion,
                    marker.ReceiptHandle.ReceiptId, new Digest32(RepeatBytes(0xfb)))),
                NewMarker(marker, expectedRevision: 1),
                NewMarker(marker, resultingRevision: 2),
                NewMarker(marker, markerRevision: 2),
                NewMarker(marker, contractVersion: "DEVELOPMENT_EMULATOR_V2")
            };
            foreach (var mutation in markerMutations)
            {
                Assert.IsFalse(projection.Verify(
                    mutation,
                    Commitment(100),
                    receiptA.Handle,
                    0,
                    first.Marker.Handle).IsValid);
            }

            var expectedHandleMutations = new[]
            {
                new DevelopmentProjectionHandle(
                    "other-projection", marker.ContractVersion, marker.MarkerId, marker.MarkerDigest),
                new DevelopmentProjectionHandle(
                    marker.ProjectionInstanceId, marker.ContractVersion,
                    "devmarker_" + new string('c', 64), marker.MarkerDigest),
                new DevelopmentProjectionHandle(
                    marker.ProjectionInstanceId, marker.ContractVersion,
                    marker.MarkerId, new Digest32(RepeatBytes(0xfc)))
            };
            foreach (var expectedHandle in expectedHandleMutations)
            {
                Assert.IsFalse(projection.Verify(
                    CloneMarker(marker),
                    Commitment(100),
                    receiptA.Handle,
                    0,
                    expectedHandle).IsValid);
            }
        }

        [Test]
        public void ConcurrentExactCommitSerializesToOneMutation()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("race-exact");
            var request = Request(110, 111, 112, 113, 114, 0);
            var results = RunConcurrent(32, _ => authority.TryCommit(request));

            Assert.AreEqual(1, results.Count(result => result.State == DevelopmentOnboardingCommitState.Committed));
            Assert.AreEqual(31, results.Count(result => result.State == DevelopmentOnboardingCommitState.ReplayCommitted));
            Assert.AreEqual(1, results.Select(result => result.Receipt.ReceiptId).Distinct().Count());
        }

        [Test]
        public void ConcurrentCollisionHandleAndGenerationRacesRemainAtomic()
        {
            var collisionAuthority = new DeterministicDevelopmentOnboardingAuthorityEmulator("race-collision");
            var baseRequest = Request(115, 116, 117, 118, 119, 0);
            var collisionRequests = new[] { baseRequest, Mutate(baseRequest, "fingerprint") };
            var collisionResults = RunConcurrent(2, index => collisionAuthority.TryCommit(collisionRequests[index]));
            Assert.AreEqual(1, collisionResults.Count(result => result.State == DevelopmentOnboardingCommitState.Committed));
            Assert.AreEqual(1, collisionResults.Count(result => result.State == DevelopmentOnboardingCommitState.Collision));

            var handleAuthority = new DeterministicDevelopmentOnboardingAuthorityEmulator("race-handle");
            var sharedHandle = Commitment(120);
            var handleRequests = new[]
            {
                new DevelopmentOnboardingCommitRequest(Commitment(121), Commitment(122), Commitment(123), Commitment(124), sharedHandle, 0),
                new DevelopmentOnboardingCommitRequest(Commitment(125), Commitment(126), Commitment(127), Commitment(128), sharedHandle, 0)
            };
            var handleResults = RunConcurrent(2, index => handleAuthority.TryCommit(handleRequests[index]));
            Assert.AreEqual(1, handleResults.Count(result => result.State == DevelopmentOnboardingCommitState.Committed));
            Assert.AreEqual(1, handleResults.Count(result => result.State == DevelopmentOnboardingCommitState.TerminalHandleTaken));

            var scopeAuthority = new DeterministicDevelopmentOnboardingAuthorityEmulator("race-scope");
            var sameScope = Commitment(129);
            var scopeRequests = new[]
            {
                new DevelopmentOnboardingCommitRequest(sameScope, Commitment(130), Commitment(131), Commitment(132), Commitment(133), 0),
                new DevelopmentOnboardingCommitRequest(sameScope, Commitment(134), Commitment(135), Commitment(136), Commitment(137), 0)
            };
            var scopeResults = RunConcurrent(2, index => scopeAuthority.TryCommit(scopeRequests[index]));
            Assert.AreEqual(1, scopeResults.Count(result => result.State == DevelopmentOnboardingCommitState.Committed));
            Assert.AreEqual(1, scopeResults.Count(result => result.State == DevelopmentOnboardingCommitState.StaleGeneration));
        }

        [Test]
        public void ConcurrentProjectionSerializesToOneMarker()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("projection-race-authority");
            var request = Request(140, 141, 142, 143, 144, 0);
            var receipt = authority.TryCommit(request).Receipt;
            var verified = authority.Verify(receipt, request, receipt.Handle);
            var projection = new DeterministicDevelopmentLocalProjectionEmulator("projection-race");
            var results = RunConcurrent(32, _ => projection.TryProject(Commitment(145), 0, verified));
            Assert.AreEqual(1, results.Count(result => result.State == DevelopmentProjectionState.Projected));
            Assert.AreEqual(31, results.Count(result => result.State == DevelopmentProjectionState.ReplayProjected));
            Assert.AreEqual(1, results.Select(result => result.Marker.MarkerId).Distinct().Count());
        }

        [Test]
        public void ExactOperationCapacityRejectsNewMutationButAllowsReplay()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("capacity-authority");
            var requests = new List<DevelopmentOnboardingCommitRequest>();
            for (var index = 0; index < DevelopmentOnboardingAuthorityContracts.MaxOperationBindings; index++)
            {
                var request = RequestFromIndex(index);
                requests.Add(request);
                Assert.AreEqual(DevelopmentOnboardingCommitState.Committed, authority.TryCommit(request).State, index.ToString());
            }

            var before = authority.CaptureRetainedState();
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.CapacityUnavailable,
                authority.TryCommit(RequestFromIndex(64)).State);
            CollectionAssert.AreEqual(before, authority.CaptureRetainedState());
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.ReplayCommitted,
                authority.TryCommit(requests[0]).State);
        }

        [Test]
        public void PublicProductSurfaceContainsNoRegistrationOrPersistenceAuthority()
        {
            var productAssembly = typeof(DeterministicDevelopmentOnboardingAuthorityEmulator).Assembly;
            var exportedNames = productAssembly.GetExportedTypes()
                .Select(type => type.FullName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var joined = string.Join("\n", exportedNames);
            StringAssert.DoesNotContain("Bootloader", joined);
            StringAssert.DoesNotContain("SaveGameData", joined);
            StringAssert.DoesNotContain("ServiceLocator", joined);
            StringAssert.DoesNotContain("Kingdom", joined);
            StringAssert.DoesNotContain("ProfileId", joined);
        }

        private static void AssertNoPublicSuccessFactory(Type type)
        {
            Assert.IsEmpty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.IsFalse(type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(method => method.ReturnType == type));
        }

        private static DevelopmentOnboardingAuthorityReceipt CloneReceipt(
            DevelopmentOnboardingAuthorityReceipt receipt)
        {
            return new DevelopmentOnboardingAuthorityReceipt(
                receipt.AuthorityInstanceId,
                receipt.ReceiptId,
                receipt.Request,
                receipt.CommittedGeneration,
                receipt.AuthorityRevision,
                receipt.ContractVersion,
                new Digest32(receipt.BodyDigest.ToArray()));
        }

        private static DevelopmentProjectionMarker CloneMarker(DevelopmentProjectionMarker marker)
        {
            return new DevelopmentProjectionMarker(
                marker.ProjectionInstanceId,
                marker.MarkerId,
                marker.LocalProfileScopeCommitment,
                marker.ReceiptHandle,
                marker.ExpectedLocalProjectionRevision,
                marker.ResultingLocalProjectionRevision,
                marker.MarkerRevision,
                marker.ContractVersion,
                new Digest32(marker.MarkerDigest.ToArray()));
        }

        private static DevelopmentProjectionMarker NewMarker(
            DevelopmentProjectionMarker source,
            string projectionInstanceId = null,
            string markerId = null,
            Commitment32? localScope = null,
            DevelopmentReceiptHandle? receiptHandle = null,
            ulong? expectedRevision = null,
            ulong? resultingRevision = null,
            ulong? markerRevision = null,
            string contractVersion = null)
        {
            return new DevelopmentProjectionMarker(
                projectionInstanceId ?? source.ProjectionInstanceId,
                markerId ?? source.MarkerId,
                localScope ?? source.LocalProfileScopeCommitment,
                receiptHandle ?? source.ReceiptHandle,
                expectedRevision ?? source.ExpectedLocalProjectionRevision,
                resultingRevision ?? source.ResultingLocalProjectionRevision,
                markerRevision ?? source.MarkerRevision,
                contractVersion ?? source.ContractVersion,
                source.MarkerDigest);
        }

        private static DevelopmentOnboardingCommitRequest Mutate(
            DevelopmentOnboardingCommitRequest request,
            string field)
        {
            return new DevelopmentOnboardingCommitRequest(
                request.AuthorityScopeCommitment,
                request.OperationCommitment,
                field == "fingerprint" ? Commitment(240) : request.SemanticRequestFingerprint,
                field == "core" ? Commitment(241) : request.OpaqueCompiledCoreDigest,
                field == "handle" ? Commitment(242) : request.NormalizedHandleCommitment,
                field == "generation" ? request.ExpectedGeneration + 1 : request.ExpectedGeneration);
        }

        private static DevelopmentOnboardingCommitRequest Request(
            byte scope,
            byte operation,
            byte fingerprint,
            byte core,
            byte handle,
            ulong expectedGeneration)
        {
            return new DevelopmentOnboardingCommitRequest(
                Commitment(scope),
                Commitment(operation),
                Commitment(fingerprint),
                Commitment(core),
                Commitment(handle),
                expectedGeneration);
        }

        private static DevelopmentOnboardingCommitRequest RequestFromIndex(int index)
        {
            if (index < 0 || index > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Request(
                checked((byte)(1 + index)),
                checked((byte)(70 + index)),
                200,
                201,
                checked((byte)(135 + index)),
                0);
        }

        private static Commitment32 Commitment(byte value)
        {
            return new Commitment32(RepeatBytes(value));
        }

        private static byte[] RepeatBytes(byte value)
        {
            return Enumerable.Repeat(value, DevelopmentOnboardingAuthorityContracts.FixedBytesLength).ToArray();
        }

        private static T[] RunConcurrent<T>(int count, Func<int, T> action)
        {
            using (var gate = new ManualResetEventSlim(false))
            {
                var tasks = Enumerable.Range(0, count)
                    .Select(index => Task.Run(() =>
                    {
                        gate.Wait();
                        return action(index);
                    }))
                    .ToArray();
                gate.Set();
                Assert.IsTrue(Task.WaitAll(tasks, TimeSpan.FromSeconds(10)), "Concurrent fixture timed out.");
                return tasks.Select(task => task.Result).ToArray();
            }
        }
    }
}

#endif
