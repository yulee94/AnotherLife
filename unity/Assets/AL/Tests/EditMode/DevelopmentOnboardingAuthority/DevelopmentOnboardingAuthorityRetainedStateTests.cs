#if UNITY_EDITOR && UNITY_INCLUDE_TESTS

using System;
using System.Collections.Generic;
using System.Linq;
using AL.Editor.Development.OnboardingAuthority;
using NUnit.Framework;

namespace AL.Tests.EditMode.DevelopmentOnboardingAuthority
{
    public sealed class DevelopmentOnboardingAuthorityRetainedStateTests
    {
        [Test]
        public void EmptyAuthorityAndProjectionRoundTripByteIdentically()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("empty-authority");
            var projection = new DeterministicDevelopmentLocalProjectionEmulator("empty-projection");
            var authorityBytes = authority.CaptureRetainedState();
            var projectionBytes = projection.CaptureRetainedState();

            Assert.IsTrue(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "empty-authority",
                DevelopmentHandleAvailabilityFixtures.Empty,
                authorityBytes,
                out var restoredAuthority,
                out var authorityFailure), authorityFailure.ToString());
            Assert.IsTrue(DeterministicDevelopmentLocalProjectionEmulator.TryRestore(
                "empty-projection",
                projectionBytes,
                out var restoredProjection,
                out var projectionFailure), projectionFailure.ToString());
            CollectionAssert.AreEqual(authorityBytes, restoredAuthority.CaptureRetainedState());
            CollectionAssert.AreEqual(projectionBytes, restoredProjection.CaptureRetainedState());
            CollectionAssert.AreEqual(authorityBytes, authority.CaptureRetainedState());
            CollectionAssert.AreEqual(projectionBytes, projection.CaptureRetainedState());
        }

        [Test]
        public void PopulatedAuthorityRestorePreservesReceiptReplayTerminalAndFixtures()
        {
            var taken = Commitment(20);
            var fixtures = new DevelopmentHandleAvailabilityFixtures(new[] { taken }, null);
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("restore-authority", fixtures);
            var committedRequest = Request(1, 2, 3, 4, 5);
            var committed = authority.TryCommit(committedRequest);
            var fixtureRequest = new DevelopmentOnboardingCommitRequest(
                Commitment(6), Commitment(7), Commitment(8), Commitment(9), taken, 0);
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.TerminalHandleTaken,
                authority.TryCommit(fixtureRequest).State);
            var dynamicClaimRequest = new DevelopmentOnboardingCommitRequest(
                Commitment(10), Commitment(11), Commitment(12), Commitment(13), committedRequest.NormalizedHandleCommitment, 0);
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.TerminalHandleTaken,
                authority.TryCommit(dynamicClaimRequest).State);
            var bytes = authority.CaptureRetainedState();

            Assert.IsTrue(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "restore-authority",
                fixtures,
                bytes,
                out var restored,
                out var failure), failure.ToString());
            CollectionAssert.AreEqual(bytes, restored.CaptureRetainedState());
            var replay = restored.TryCommit(committedRequest);
            Assert.AreEqual(DevelopmentOnboardingCommitState.ReplayCommitted, replay.State);
            Assert.AreEqual(committed.Receipt.ReceiptId, replay.Receipt.ReceiptId);
            Assert.IsTrue(restored.Verify(CloneReceipt(committed.Receipt), committedRequest, committed.Receipt.Handle).IsValid);
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.ReplayTerminalHandleTaken,
                restored.TryCommit(fixtureRequest).State);
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.ReplayTerminalHandleTaken,
                restored.TryCommit(dynamicClaimRequest).State);
        }

        [Test]
        public void PopulatedProjectionRestorePreservesMarkersRevisionsAndReplay()
        {
            var authorityA = new DeterministicDevelopmentOnboardingAuthorityEmulator("projection-source-a");
            var authorityB = new DeterministicDevelopmentOnboardingAuthorityEmulator("projection-source-b");
            var requestA = Request(21, 22, 23, 24, 25);
            var requestB = Request(26, 27, 28, 29, 30);
            var receiptA = authorityA.TryCommit(requestA).Receipt;
            var receiptB = authorityB.TryCommit(requestB).Receipt;
            var verifiedA = authorityA.Verify(receiptA, requestA, receiptA.Handle);
            var verifiedB = authorityB.Verify(receiptB, requestB, receiptB.Handle);
            var projection = new DeterministicDevelopmentLocalProjectionEmulator("restore-projection");
            var profile = Commitment(31);
            var markerA = projection.TryProject(profile, 0, verifiedA).Marker;
            var markerB = projection.TryProject(profile, 1, verifiedB).Marker;
            var bytes = projection.CaptureRetainedState();

            Assert.IsTrue(DeterministicDevelopmentLocalProjectionEmulator.TryRestore(
                "restore-projection",
                bytes,
                out var restored,
                out var failure), failure.ToString());
            CollectionAssert.AreEqual(bytes, restored.CaptureRetainedState());
            Assert.AreEqual(
                DevelopmentProjectionState.ReplayProjected,
                restored.TryProject(profile, 0, verifiedA).State);
            Assert.AreEqual(
                DevelopmentProjectionState.ReplayProjected,
                restored.TryProject(profile, 1, verifiedB).State);
            Assert.AreEqual(
                markerA.MarkerId,
                restored.ReconcileProjection(profile, receiptA.Handle).Marker.MarkerId);
            Assert.AreEqual(
                markerB.MarkerId,
                restored.ReconcileProjection(profile, receiptB.Handle).Marker.MarkerId);
        }

        [Test]
        public void RestoreRejectsWrongInstanceFixturesAndCrossKindWithoutObject()
        {
            var fixtures = new DevelopmentHandleAvailabilityFixtures(new[] { Commitment(40) }, null);
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("kind-authority", fixtures);
            var projection = new DeterministicDevelopmentLocalProjectionEmulator("kind-projection");
            var authorityBytes = authority.CaptureRetainedState();
            var projectionBytes = projection.CaptureRetainedState();

            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "wrong-authority",
                fixtures,
                authorityBytes,
                out var wrongInstance,
                out var wrongInstanceFailure));
            Assert.IsNull(wrongInstance);
            Assert.AreEqual(DevelopmentRetainedStateFailure.InstanceMismatch, wrongInstanceFailure);

            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "kind-authority",
                DevelopmentHandleAvailabilityFixtures.Empty,
                authorityBytes,
                out var wrongFixtures,
                out var wrongFixtureFailure));
            Assert.IsNull(wrongFixtures);
            Assert.AreEqual(DevelopmentRetainedStateFailure.FixtureMismatch, wrongFixtureFailure);

            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "kind-authority",
                fixtures,
                projectionBytes,
                out var authorityFromProjection,
                out _));
            Assert.IsNull(authorityFromProjection);
            Assert.IsFalse(DeterministicDevelopmentLocalProjectionEmulator.TryRestore(
                "kind-projection",
                authorityBytes,
                out var projectionFromAuthority,
                out _));
            Assert.IsNull(projectionFromAuthority);
        }

        [Test]
        public void EveryTruncationTrailingByteAndDigestFlipFailClosed()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("corrupt-authority");
            authority.TryCommit(Request(41, 42, 43, 44, 45));
            var bytes = authority.CaptureRetainedState();

            for (var length = 0; length < bytes.Length; length++)
            {
                var truncated = bytes.Take(length).ToArray();
                Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                    "corrupt-authority",
                    DevelopmentHandleAvailabilityFixtures.Empty,
                    truncated,
                    out var restored,
                    out _), "Unexpected success at truncation " + length);
                Assert.IsNull(restored);
            }

            var trailing = bytes.Concat(new byte[] { 0 }).ToArray();
            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "corrupt-authority",
                DevelopmentHandleAvailabilityFixtures.Empty,
                trailing,
                out var trailingResult,
                out var trailingFailure));
            Assert.IsNull(trailingResult);
            Assert.AreEqual(DevelopmentRetainedStateFailure.TrailingBytes, trailingFailure);

            var flipped = (byte[])bytes.Clone();
            flipped[flipped.Length - 1] ^= 0x01;
            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "corrupt-authority",
                DevelopmentHandleAvailabilityFixtures.Empty,
                flipped,
                out var flippedResult,
                out var flippedFailure));
            Assert.IsNull(flippedResult);
            Assert.AreEqual(DevelopmentRetainedStateFailure.DigestMismatch, flippedFailure);
        }

        [Test]
        public void ValidEnvelopeWithDuplicateOrUnknownAuthorityRecordFailsInnerValidation()
        {
            var fixtures = DevelopmentHandleAvailabilityFixtures.Empty;
            var request = Request(50, 51, 52, 53, 54);
            var record = EncodeAuthorityRecord(request, DevelopmentAuthorityRecordState.Committed);
            var duplicatePayload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityPayloadDomain,
                DevelopmentFrameV1.UInt64Bytes(2),
                record,
                record);
            var duplicateEnvelope = DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Authority,
                "malicious-authority",
                fixtures.FixtureDigest,
                duplicatePayload);
            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "malicious-authority",
                fixtures,
                duplicateEnvelope,
                out var duplicate,
                out var duplicateFailure));
            Assert.IsNull(duplicate);
            Assert.AreEqual(DevelopmentRetainedStateFailure.DuplicateRecord, duplicateFailure);

            var unknownRecord = EncodeAuthorityRecord(request, (DevelopmentAuthorityRecordState)99);
            var unknownPayload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityPayloadDomain,
                DevelopmentFrameV1.UInt64Bytes(1),
                unknownRecord);
            var unknownEnvelope = DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Authority,
                "malicious-authority",
                fixtures.FixtureDigest,
                unknownPayload);
            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "malicious-authority",
                fixtures,
                unknownEnvelope,
                out var unknown,
                out var unknownFailure));
            Assert.IsNull(unknown);
            Assert.AreEqual(DevelopmentRetainedStateFailure.InvalidRecord, unknownFailure);
        }

        [Test]
        public void ValidEnvelopeWithOrphanClaimOrOverCapacityFailsAtomically()
        {
            var fixtures = DevelopmentHandleAvailabilityFixtures.Empty;
            var orphanRecord = EncodeAuthorityRecord(
                Request(60, 61, 62, 63, 64),
                DevelopmentAuthorityRecordState.TerminalTakenClaim);
            var orphanPayload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityPayloadDomain,
                DevelopmentFrameV1.UInt64Bytes(1),
                orphanRecord);
            var orphanEnvelope = DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Authority,
                "orphan-authority",
                fixtures.FixtureDigest,
                orphanPayload);
            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "orphan-authority",
                fixtures,
                orphanEnvelope,
                out var orphan,
                out var orphanFailure));
            Assert.IsNull(orphan);
            Assert.AreEqual(DevelopmentRetainedStateFailure.InvalidRecord, orphanFailure);

            var fields = new List<byte[]> { DevelopmentFrameV1.UInt64Bytes(65) };
            for (var index = 0; index < 65; index++)
            {
                fields.Add(EncodeAuthorityRecord(RequestFromIndex(index), DevelopmentAuthorityRecordState.Committed));
            }

            var overCapacityPayload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityPayloadDomain,
                fields);
            var overCapacityEnvelope = DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Authority,
                "over-capacity",
                fixtures.FixtureDigest,
                overCapacityPayload);
            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "over-capacity",
                fixtures,
                overCapacityEnvelope,
                out var overCapacity,
                out _));
            Assert.IsNull(overCapacity);
        }

        [Test]
        public void RestoreRejectsFixtureAndSameScopeClaimStatesThatCommitCanNeverProduce()
        {
            var taken = Commitment(180);
            var unavailable = Commitment(181);
            var fixtures = new DevelopmentHandleAvailabilityFixtures(
                new[] { taken },
                new[] { unavailable });

            AssertAuthorityRestoreRejected(
                "impossible-taken-commit",
                fixtures,
                EncodeAuthorityRecord(
                    new DevelopmentOnboardingCommitRequest(
                        Commitment(1), Commitment(2), Commitment(3), Commitment(4), taken, 0),
                    DevelopmentAuthorityRecordState.Committed));
            AssertAuthorityRestoreRejected(
                "impossible-unavailable-commit",
                fixtures,
                EncodeAuthorityRecord(
                    new DevelopmentOnboardingCommitRequest(
                        Commitment(5), Commitment(6), Commitment(7), Commitment(8), unavailable, 0),
                    DevelopmentAuthorityRecordState.Committed));
            AssertAuthorityRestoreRejected(
                "impossible-unavailable-terminal",
                fixtures,
                EncodeAuthorityRecord(
                    new DevelopmentOnboardingCommitRequest(
                        Commitment(9), Commitment(10), Commitment(11), Commitment(12), unavailable, 0),
                    DevelopmentAuthorityRecordState.TerminalTakenClaim));

            var claimedHandle = Commitment(182);
            var committed = EncodeAuthorityRecord(
                new DevelopmentOnboardingCommitRequest(
                    Commitment(13), Commitment(14), Commitment(15), Commitment(16), claimedHandle, 0),
                DevelopmentAuthorityRecordState.Committed);
            var sameScopeClaim = EncodeAuthorityRecord(
                new DevelopmentOnboardingCommitRequest(
                    Commitment(13), Commitment(17), Commitment(18), Commitment(19), claimedHandle, 0),
                DevelopmentAuthorityRecordState.TerminalTakenClaim);
            AssertAuthorityRestoreRejected(
                "impossible-same-scope-claim",
                fixtures,
                committed,
                sameScopeClaim);
        }

        [Test]
        public void RestoreCopiesCallerEnvelopeAndSubsequentMutationCannotChangeState()
        {
            var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("copy-authority");
            var request = Request(70, 71, 72, 73, 74);
            authority.TryCommit(request);
            var bytes = authority.CaptureRetainedState();
            var original = (byte[])bytes.Clone();
            Assert.IsTrue(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                "copy-authority",
                DevelopmentHandleAvailabilityFixtures.Empty,
                bytes,
                out var restored,
                out var failure), failure.ToString());
            Array.Fill(bytes, (byte)0xff);
            CollectionAssert.AreEqual(original, restored.CaptureRetainedState());
            Assert.AreEqual(
                DevelopmentOnboardingCommitState.ReplayCommitted,
                restored.TryCommit(request).State);
        }

        [Test]
        public void ProjectionCapacityIsExactAndReplayRemainsAvailableAtCapacity()
        {
            var projection = new DeterministicDevelopmentLocalProjectionEmulator("projection-capacity");
            DevelopmentProjectionResult first = null;
            VerifiedDevelopmentReceipt firstVerified = null;
            for (var index = 0; index < DevelopmentOnboardingAuthorityContracts.MaxProjectionMarkers; index++)
            {
                var authority = new DeterministicDevelopmentOnboardingAuthorityEmulator("projection-source-" + index);
                var request = RequestFromIndex(index);
                var receipt = authority.TryCommit(request).Receipt;
                var verified = authority.Verify(receipt, request, receipt.Handle);
                var result = projection.TryProject(Commitment((byte)(1 + index)), 0, verified);
                Assert.AreEqual(DevelopmentProjectionState.Projected, result.State, index.ToString());
                if (index == 0)
                {
                    first = result;
                    firstVerified = verified;
                }
            }

            var before = projection.CaptureRetainedState();
            var overflowAuthority = new DeterministicDevelopmentOnboardingAuthorityEmulator("projection-source-overflow");
            var overflowRequest = RequestFromIndex(64);
            var overflowReceipt = overflowAuthority.TryCommit(overflowRequest).Receipt;
            var overflowVerified = overflowAuthority.Verify(overflowReceipt, overflowRequest, overflowReceipt.Handle);
            Assert.AreEqual(
                DevelopmentProjectionState.CapacityUnavailable,
                projection.TryProject(Commitment(100), 0, overflowVerified).State);
            CollectionAssert.AreEqual(before, projection.CaptureRetainedState());
            Assert.AreEqual(
                DevelopmentProjectionState.ReplayProjected,
                projection.TryProject(
                    first.Marker.LocalProfileScopeCommitment,
                    first.Marker.ExpectedLocalProjectionRevision,
                    firstVerified).State);
        }

        [Test]
        public void ValidProjectionEnvelopeRejectsDuplicateOrderRevisionAndCrossProfileOwnership()
        {
            var firstHandle = SyntheticReceiptHandle('a', 210);
            var secondHandle = SyntheticReceiptHandle('b', 211);
            var scopeA = Commitment(210);
            var scopeB = Commitment(211);
            var firstRecord = EncodeProjectionRecord(scopeA, firstHandle, 0);

            AssertProjectionRestoreRejected(
                "projection-duplicate",
                firstRecord,
                firstRecord);
            AssertProjectionRestoreRejected(
                "projection-order",
                EncodeProjectionRecord(scopeA, secondHandle, 1),
                firstRecord);
            AssertProjectionRestoreRejected(
                "projection-revision-gap",
                EncodeProjectionRecord(scopeA, firstHandle, 1));
            AssertProjectionRestoreRejected(
                "projection-cross-profile-owner",
                firstRecord,
                EncodeProjectionRecord(scopeB, firstHandle, 0));
        }

        [Test]
        public void BoundedArbitraryInputNeverThrowsOrPublishesPartialObject()
        {
            var random = new Random(17477);
            for (var index = 0; index < 256; index++)
            {
                var bytes = new byte[index];
                random.NextBytes(bytes);
                Assert.DoesNotThrow(() =>
                {
                    var success = DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                        "fuzz-authority",
                        DevelopmentHandleAvailabilityFixtures.Empty,
                        bytes,
                        out var restored,
                        out _);
                    if (!success)
                    {
                        Assert.IsNull(restored);
                    }
                });
            }
        }

        private static byte[] EncodeAuthorityRecord(
            DevelopmentOnboardingCommitRequest request,
            DevelopmentAuthorityRecordState state)
        {
            return DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityRecordDomain,
                DevelopmentFrameV1.UInt64Bytes((ulong)state),
                request.AuthorityScopeCommitment.ToArray(),
                request.OperationCommitment.ToArray(),
                request.SemanticRequestFingerprint.ToArray(),
                request.OpaqueCompiledCoreDigest.ToArray(),
                request.NormalizedHandleCommitment.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(request.ExpectedGeneration));
        }

        private static byte[] EncodeProjectionRecord(
            Commitment32 localScope,
            DevelopmentReceiptHandle receiptHandle,
            ulong expectedRevision)
        {
            return DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.ProjectionRecordDomain,
                localScope.ToArray(),
                DevelopmentFrameV1.Utf8(receiptHandle.AuthorityInstanceId),
                DevelopmentFrameV1.Utf8(receiptHandle.ContractVersion),
                DevelopmentFrameV1.Utf8(receiptHandle.ReceiptId),
                receiptHandle.BodyDigest.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(expectedRevision));
        }

        private static DevelopmentReceiptHandle SyntheticReceiptHandle(char receiptHex, byte digestByte)
        {
            return new DevelopmentReceiptHandle(
                "synthetic-authority",
                DevelopmentOnboardingAuthorityContracts.ContractVersion,
                "devrcpt_" + new string(receiptHex, 64),
                new Digest32(Enumerable.Repeat(digestByte, 32).ToArray()));
        }

        private static void AssertAuthorityRestoreRejected(
            string instanceId,
            DevelopmentHandleAvailabilityFixtures fixtures,
            params byte[][] records)
        {
            var fields = new List<byte[]> { DevelopmentFrameV1.UInt64Bytes((ulong)records.Length) };
            fields.AddRange(records);
            var payload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityPayloadDomain,
                fields);
            var envelope = DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Authority,
                instanceId,
                fixtures.FixtureDigest,
                payload);

            Assert.IsFalse(DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                instanceId,
                fixtures,
                envelope,
                out var restored,
                out var failure));
            Assert.IsNull(restored);
            Assert.AreEqual(DevelopmentRetainedStateFailure.InvalidRecord, failure);
        }

        private static void AssertProjectionRestoreRejected(string instanceId, params byte[][] records)
        {
            var fields = new List<byte[]> { DevelopmentFrameV1.UInt64Bytes((ulong)records.Length) };
            fields.AddRange(records);
            var payload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.ProjectionPayloadDomain,
                fields);
            var envelope = DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Projection,
                instanceId,
                null,
                payload);

            Assert.IsFalse(DeterministicDevelopmentLocalProjectionEmulator.TryRestore(
                instanceId,
                envelope,
                out var restored,
                out _));
            Assert.IsNull(restored);
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

        private static DevelopmentOnboardingCommitRequest Request(
            byte scope,
            byte operation,
            byte fingerprint,
            byte core,
            byte handle)
        {
            return new DevelopmentOnboardingCommitRequest(
                Commitment(scope),
                Commitment(operation),
                Commitment(fingerprint),
                Commitment(core),
                Commitment(handle),
                0);
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
                checked((byte)(135 + index)));
        }

        private static Commitment32 Commitment(byte value)
        {
            return new Commitment32(
                Enumerable.Repeat(value, DevelopmentOnboardingAuthorityContracts.FixedBytesLength).ToArray());
        }
    }
}

#endif
