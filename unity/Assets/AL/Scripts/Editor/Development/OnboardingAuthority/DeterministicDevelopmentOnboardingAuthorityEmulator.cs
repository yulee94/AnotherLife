#if !UNITY_EDITOR
#error DEVELOPMENT_EMULATOR_V1 is editor-only and must never compile into a Player.
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AL.Editor.Development.OnboardingAuthority
{
    internal enum DevelopmentAuthorityRecordState : ulong
    {
        Committed = 1,
        TerminalTakenFixture = 2,
        TerminalTakenClaim = 3
    }

    public sealed class DeterministicDevelopmentOnboardingAuthorityEmulator :
        IDevelopmentOnboardingAuthorityEmulator,
        IDevelopmentOnboardingReceiptVerifier
    {
        private sealed class OperationRecord
        {
            internal OperationRecord(
                DevelopmentOnboardingCommitRequest request,
                DevelopmentAuthorityRecordState state,
                DevelopmentOnboardingAuthorityReceipt receipt)
            {
                Request = request;
                State = state;
                Receipt = receipt;
            }

            internal DevelopmentOnboardingCommitRequest Request { get; }
            internal DevelopmentAuthorityRecordState State { get; }
            internal DevelopmentOnboardingAuthorityReceipt Receipt { get; }
        }

        private readonly object _gate = new object();
        private readonly string _instanceId;
        private readonly DevelopmentHandleAvailabilityFixtures _fixtures;
        private readonly Dictionary<string, OperationRecord> _operations;
        private readonly Dictionary<string, OperationRecord> _committedByScope;
        private readonly Dictionary<string, OperationRecord> _claimedByHandle;
        private readonly Dictionary<string, OperationRecord> _receiptsById;

        public DeterministicDevelopmentOnboardingAuthorityEmulator(
            string instanceId,
            DevelopmentHandleAvailabilityFixtures fixtures = null)
            : this(
                instanceId,
                fixtures ?? DevelopmentHandleAvailabilityFixtures.Empty,
                new Dictionary<string, OperationRecord>(StringComparer.Ordinal))
        {
        }

        private DeterministicDevelopmentOnboardingAuthorityEmulator(
            string instanceId,
            DevelopmentHandleAvailabilityFixtures fixtures,
            Dictionary<string, OperationRecord> operations)
        {
            DevelopmentInstanceId.Require(instanceId, nameof(instanceId));
            _instanceId = instanceId;
            _fixtures = fixtures ?? throw new ArgumentNullException(nameof(fixtures));
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _committedByScope = new Dictionary<string, OperationRecord>(StringComparer.Ordinal);
            _claimedByHandle = new Dictionary<string, OperationRecord>(StringComparer.Ordinal);
            _receiptsById = new Dictionary<string, OperationRecord>(StringComparer.Ordinal);
            RebuildIndexesOrThrow();
        }

        public string InstanceId => _instanceId;

        public DevelopmentHandleAvailability CheckHandle(
            Commitment32 authorityScopeCommitment,
            Commitment32 normalizedHandleCommitment,
            ulong expectedGeneration)
        {
            lock (_gate)
            {
                if (!IsNonzero(authorityScopeCommitment) || !IsNonzero(normalizedHandleCommitment))
                {
                    return new DevelopmentHandleAvailability(
                        DevelopmentHandleAvailabilityState.InvalidInput,
                        normalizedHandleCommitment,
                        DevelopmentAuthorityFailure.InvalidInput);
                }

                var currentGeneration = GetScopeGeneration(authorityScopeCommitment);
                if (expectedGeneration != currentGeneration)
                {
                    return new DevelopmentHandleAvailability(
                        DevelopmentHandleAvailabilityState.StaleGeneration,
                        normalizedHandleCommitment,
                        DevelopmentAuthorityFailure.StaleGeneration);
                }

                if (_fixtures.IsUnavailable(normalizedHandleCommitment))
                {
                    return new DevelopmentHandleAvailability(
                        DevelopmentHandleAvailabilityState.Unavailable,
                        normalizedHandleCommitment,
                        DevelopmentAuthorityFailure.AuthorityUnavailable);
                }

                if (_fixtures.IsTaken(normalizedHandleCommitment) ||
                    _claimedByHandle.ContainsKey(HandleKey(normalizedHandleCommitment)))
                {
                    return new DevelopmentHandleAvailability(
                        DevelopmentHandleAvailabilityState.Taken,
                        normalizedHandleCommitment,
                        DevelopmentAuthorityFailure.HandleTaken);
                }

                if (_claimedByHandle.Count >= DevelopmentOnboardingAuthorityContracts.MaxClaimedHandles)
                {
                    return new DevelopmentHandleAvailability(
                        DevelopmentHandleAvailabilityState.CapacityUnavailable,
                        normalizedHandleCommitment,
                        DevelopmentAuthorityFailure.CapacityUnavailable);
                }

                return new DevelopmentHandleAvailability(
                    DevelopmentHandleAvailabilityState.Available,
                    normalizedHandleCommitment,
                    DevelopmentAuthorityFailure.None);
            }
        }

        public DevelopmentOnboardingCommitResult TryCommit(DevelopmentOnboardingCommitRequest request)
        {
            lock (_gate)
            {
                if (!request.IsValid)
                {
                    return CommitFailure(
                        DevelopmentOnboardingCommitState.InvalidInput,
                        DevelopmentAuthorityFailure.InvalidInput);
                }

                var operationKey = OperationKey(request.AuthorityScopeCommitment, request.OperationCommitment);
                if (_operations.TryGetValue(operationKey, out var existing))
                {
                    if (!existing.Request.Equals(request))
                    {
                        return CommitFailure(
                            DevelopmentOnboardingCommitState.Collision,
                            DevelopmentAuthorityFailure.Collision);
                    }

                    return Replay(existing);
                }

                var currentGeneration = GetScopeGeneration(request.AuthorityScopeCommitment);
                if (request.ExpectedGeneration != currentGeneration)
                {
                    return CommitFailure(
                        DevelopmentOnboardingCommitState.StaleGeneration,
                        DevelopmentAuthorityFailure.StaleGeneration);
                }

                if (request.ExpectedGeneration == ulong.MaxValue)
                {
                    return CommitFailure(
                        DevelopmentOnboardingCommitState.GenerationOverflow,
                        DevelopmentAuthorityFailure.GenerationOverflow);
                }

                if (_fixtures.IsUnavailable(request.NormalizedHandleCommitment))
                {
                    return CommitFailure(
                        DevelopmentOnboardingCommitState.AuthorityUnavailable,
                        DevelopmentAuthorityFailure.AuthorityUnavailable);
                }

                if (_operations.Count >= DevelopmentOnboardingAuthorityContracts.MaxOperationBindings)
                {
                    return CommitFailure(
                        DevelopmentOnboardingCommitState.CapacityUnavailable,
                        DevelopmentAuthorityFailure.CapacityUnavailable);
                }

                var scopeKey = ScopeKey(request.AuthorityScopeCommitment);
                if (_committedByScope.ContainsKey(scopeKey))
                {
                    return CommitFailure(
                        DevelopmentOnboardingCommitState.ScopeAlreadyCommitted,
                        DevelopmentAuthorityFailure.ScopeAlreadyCommitted);
                }

                var handleKey = HandleKey(request.NormalizedHandleCommitment);
                if (_fixtures.IsTaken(request.NormalizedHandleCommitment))
                {
                    var terminal = new OperationRecord(
                        request,
                        DevelopmentAuthorityRecordState.TerminalTakenFixture,
                        null);
                    _operations.Add(operationKey, terminal);
                    return new DevelopmentOnboardingCommitResult(
                        DevelopmentOnboardingCommitState.TerminalHandleTaken,
                        DevelopmentAuthorityFailure.HandleTaken);
                }

                if (_claimedByHandle.ContainsKey(handleKey))
                {
                    var terminal = new OperationRecord(
                        request,
                        DevelopmentAuthorityRecordState.TerminalTakenClaim,
                        null);
                    _operations.Add(operationKey, terminal);
                    return new DevelopmentOnboardingCommitResult(
                        DevelopmentOnboardingCommitState.TerminalHandleTaken,
                        DevelopmentAuthorityFailure.HandleTaken);
                }

                if (_committedByScope.Count >= DevelopmentOnboardingAuthorityContracts.MaxCommittedReceipts ||
                    _claimedByHandle.Count >= DevelopmentOnboardingAuthorityContracts.MaxClaimedHandles)
                {
                    return CommitFailure(
                        DevelopmentOnboardingCommitState.CapacityUnavailable,
                        DevelopmentAuthorityFailure.CapacityUnavailable);
                }

                var receipt = BuildReceipt(request);
                var committed = new OperationRecord(request, DevelopmentAuthorityRecordState.Committed, receipt);
                _operations.Add(operationKey, committed);
                _committedByScope.Add(scopeKey, committed);
                _claimedByHandle.Add(handleKey, committed);
                _receiptsById.Add(receipt.ReceiptId, committed);

                return new DevelopmentOnboardingCommitResult(
                    DevelopmentOnboardingCommitState.Committed,
                    DevelopmentAuthorityFailure.None,
                    receipt);
            }
        }

        public DevelopmentOnboardingReconcileResult Reconcile(DevelopmentOnboardingCommitRequest request)
        {
            lock (_gate)
            {
                if (!request.IsValid)
                {
                    return new DevelopmentOnboardingReconcileResult(
                        DevelopmentOnboardingReconcileState.InvalidInput,
                        DevelopmentAuthorityFailure.InvalidInput);
                }

                if (!_operations.TryGetValue(
                        OperationKey(request.AuthorityScopeCommitment, request.OperationCommitment),
                        out var operation))
                {
                    return new DevelopmentOnboardingReconcileResult(
                        DevelopmentOnboardingReconcileState.NotFound,
                        DevelopmentAuthorityFailure.NotFound);
                }

                if (!operation.Request.Equals(request))
                {
                    return new DevelopmentOnboardingReconcileResult(
                        DevelopmentOnboardingReconcileState.Collision,
                        DevelopmentAuthorityFailure.Collision);
                }

                if (operation.State == DevelopmentAuthorityRecordState.Committed)
                {
                    return new DevelopmentOnboardingReconcileResult(
                        DevelopmentOnboardingReconcileState.Committed,
                        DevelopmentAuthorityFailure.None,
                        operation.Receipt);
                }

                return new DevelopmentOnboardingReconcileResult(
                    DevelopmentOnboardingReconcileState.TerminalHandleTaken,
                    DevelopmentAuthorityFailure.HandleTaken);
            }
        }

        public VerifiedDevelopmentReceipt Verify(
            DevelopmentOnboardingAuthorityReceipt candidateReceipt,
            DevelopmentOnboardingCommitRequest expectedRequest,
            DevelopmentReceiptHandle expectedHandle)
        {
            lock (_gate)
            {
                if (candidateReceipt == null || !expectedRequest.IsValid || !expectedHandle.IsValid)
                {
                    return VerifiedDevelopmentReceipt.FailureResult(DevelopmentAuthorityFailure.InvalidInput);
                }

                if (!ReceiptIsCanonical(candidateReceipt) ||
                    !candidateReceipt.Request.Equals(expectedRequest) ||
                    !candidateReceipt.Handle.Equals(expectedHandle))
                {
                    return VerifiedDevelopmentReceipt.FailureResult(DevelopmentAuthorityFailure.ReceiptMismatch);
                }

                var operationKey = OperationKey(
                    expectedRequest.AuthorityScopeCommitment,
                    expectedRequest.OperationCommitment);
                if (!_operations.TryGetValue(operationKey, out var operation) ||
                    operation.State != DevelopmentAuthorityRecordState.Committed ||
                    operation.Receipt == null)
                {
                    return VerifiedDevelopmentReceipt.FailureResult(DevelopmentAuthorityFailure.ReceiptNotCommitted);
                }

                if (!ReceiptEquals(candidateReceipt, operation.Receipt))
                {
                    return VerifiedDevelopmentReceipt.FailureResult(DevelopmentAuthorityFailure.ReceiptMismatch);
                }

                return VerifiedDevelopmentReceipt.Success(candidateReceipt);
            }
        }

        public byte[] CaptureRetainedState()
        {
            lock (_gate)
            {
                return EncodeRetainedState(_operations.Values);
            }
        }

        public static bool TryRestore(
            string instanceId,
            DevelopmentHandleAvailabilityFixtures fixtures,
            byte[] retainedState,
            out DeterministicDevelopmentOnboardingAuthorityEmulator emulator,
            out DevelopmentRetainedStateFailure failure)
        {
            emulator = null;
            fixtures = fixtures ?? DevelopmentHandleAvailabilityFixtures.Empty;
            if (!DevelopmentOnboardingAuthorityRetainedStateCodec.TryDecodeEnvelope(
                    retainedState,
                    DevelopmentRetainedStoreKind.Authority,
                    instanceId,
                    fixtures.FixtureDigest,
                    out var payload,
                    out failure))
            {
                return false;
            }

            if (!TryDecodeAuthorityPayload(instanceId, fixtures, payload, out var operations, out failure))
            {
                return false;
            }

            try
            {
                var candidate = new DeterministicDevelopmentOnboardingAuthorityEmulator(
                    instanceId,
                    fixtures,
                    operations);
                var recaptured = candidate.CaptureRetainedState();
                if (!ByteArraysEqual(retainedState, recaptured))
                {
                    failure = DevelopmentRetainedStateFailure.InvalidRecord;
                    return false;
                }

                emulator = candidate;
                failure = DevelopmentRetainedStateFailure.None;
                return true;
            }
            catch (ArgumentException)
            {
                failure = DevelopmentRetainedStateFailure.InvalidRecord;
                return false;
            }
            catch (InvalidOperationException)
            {
                failure = DevelopmentRetainedStateFailure.InvalidRecord;
                return false;
            }
        }

        private DevelopmentOnboardingCommitResult Replay(OperationRecord operation)
        {
            if (operation.State == DevelopmentAuthorityRecordState.Committed)
            {
                return new DevelopmentOnboardingCommitResult(
                    DevelopmentOnboardingCommitState.ReplayCommitted,
                    DevelopmentAuthorityFailure.None,
                    operation.Receipt);
            }

            return new DevelopmentOnboardingCommitResult(
                DevelopmentOnboardingCommitState.ReplayTerminalHandleTaken,
                DevelopmentAuthorityFailure.HandleTaken);
        }

        private static DevelopmentOnboardingCommitResult CommitFailure(
            DevelopmentOnboardingCommitState state,
            DevelopmentAuthorityFailure failure)
        {
            return new DevelopmentOnboardingCommitResult(state, failure);
        }

        private DevelopmentOnboardingAuthorityReceipt BuildReceipt(DevelopmentOnboardingCommitRequest request)
        {
            var committedGeneration = checked(request.ExpectedGeneration + 1);
            const ulong authorityRevision = 1;
            var receiptId = DevelopmentCanonicalDigest.ComputeReceiptId(_instanceId, request);
            var bodyDigest = DevelopmentCanonicalDigest.ComputeReceiptBodyDigest(
                _instanceId,
                receiptId,
                request,
                committedGeneration,
                authorityRevision);
            return new DevelopmentOnboardingAuthorityReceipt(
                _instanceId,
                receiptId,
                request,
                committedGeneration,
                authorityRevision,
                DevelopmentOnboardingAuthorityContracts.ContractVersion,
                bodyDigest);
        }

        private bool ReceiptIsCanonical(DevelopmentOnboardingAuthorityReceipt receipt)
        {
            if (!string.Equals(receipt.AuthorityInstanceId, _instanceId, StringComparison.Ordinal) ||
                !string.Equals(receipt.ContractVersion, DevelopmentOnboardingAuthorityContracts.ContractVersion, StringComparison.Ordinal) ||
                !receipt.Request.IsValid ||
                receipt.Request.ExpectedGeneration == ulong.MaxValue ||
                receipt.CommittedGeneration != receipt.Request.ExpectedGeneration + 1 ||
                receipt.AuthorityRevision != 1)
            {
                return false;
            }

            var expectedId = DevelopmentCanonicalDigest.ComputeReceiptId(_instanceId, receipt.Request);
            if (!string.Equals(receipt.ReceiptId, expectedId, StringComparison.Ordinal))
            {
                return false;
            }

            return receipt.BodyDigest == DevelopmentCanonicalDigest.ComputeReceiptBodyDigest(
                _instanceId,
                receipt.ReceiptId,
                receipt.Request,
                receipt.CommittedGeneration,
                receipt.AuthorityRevision);
        }

        private static bool ReceiptEquals(
            DevelopmentOnboardingAuthorityReceipt left,
            DevelopmentOnboardingAuthorityReceipt right)
        {
            return left != null && right != null &&
                   string.Equals(left.AuthorityInstanceId, right.AuthorityInstanceId, StringComparison.Ordinal) &&
                   string.Equals(left.ReceiptId, right.ReceiptId, StringComparison.Ordinal) &&
                   left.Request.Equals(right.Request) &&
                   left.CommittedGeneration == right.CommittedGeneration &&
                   left.AuthorityRevision == right.AuthorityRevision &&
                   string.Equals(left.ContractVersion, right.ContractVersion, StringComparison.Ordinal) &&
                   left.BodyDigest == right.BodyDigest;
        }

        private ulong GetScopeGeneration(Commitment32 scope)
        {
            return _committedByScope.ContainsKey(ScopeKey(scope)) ? 1UL : 0UL;
        }

        private void RebuildIndexesOrThrow()
        {
            if (_operations.Count > DevelopmentOnboardingAuthorityContracts.MaxOperationBindings)
            {
                throw new InvalidOperationException("Operation capacity exceeded.");
            }

            foreach (var pair in _operations)
            {
                var operation = pair.Value;
                if (operation == null || !operation.Request.IsValid || operation.Request.ExpectedGeneration != 0 ||
                    !string.Equals(
                        pair.Key,
                        OperationKey(operation.Request.AuthorityScopeCommitment, operation.Request.OperationCommitment),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid operation record.");
                }

                if (operation.State == DevelopmentAuthorityRecordState.Committed)
                {
                    if (operation.Receipt == null || !ReceiptIsCanonical(operation.Receipt) ||
                        !ReceiptEquals(operation.Receipt, BuildReceipt(operation.Request)))
                    {
                        throw new InvalidOperationException("Invalid committed receipt.");
                    }

                    _committedByScope.Add(ScopeKey(operation.Request.AuthorityScopeCommitment), operation);
                    _claimedByHandle.Add(HandleKey(operation.Request.NormalizedHandleCommitment), operation);
                    _receiptsById.Add(operation.Receipt.ReceiptId, operation);
                }
                else if (operation.Receipt != null ||
                         operation.State != DevelopmentAuthorityRecordState.TerminalTakenFixture &&
                         operation.State != DevelopmentAuthorityRecordState.TerminalTakenClaim)
                {
                    throw new InvalidOperationException("Invalid terminal operation.");
                }
            }

            if (_committedByScope.Count > DevelopmentOnboardingAuthorityContracts.MaxCommittedReceipts ||
                _claimedByHandle.Count > DevelopmentOnboardingAuthorityContracts.MaxClaimedHandles)
            {
                throw new InvalidOperationException("Committed capacity exceeded.");
            }

            foreach (var operation in _operations.Values)
            {
                var handle = operation.Request.NormalizedHandleCommitment;
                if (_fixtures.IsUnavailable(handle))
                {
                    throw new InvalidOperationException("Unavailable fixture cannot have a retained operation.");
                }

                if (operation.State == DevelopmentAuthorityRecordState.Committed && _fixtures.IsTaken(handle))
                {
                    throw new InvalidOperationException("Taken fixture cannot have a committed owner.");
                }

                if (operation.State == DevelopmentAuthorityRecordState.TerminalTakenFixture &&
                    !_fixtures.IsTaken(handle))
                {
                    throw new InvalidOperationException("Terminal fixture does not exist.");
                }

                if (operation.State == DevelopmentAuthorityRecordState.TerminalTakenClaim)
                {
                    if (_fixtures.IsTaken(handle) ||
                        !_claimedByHandle.TryGetValue(HandleKey(handle), out var owner) ||
                        owner.Request.AuthorityScopeCommitment == operation.Request.AuthorityScopeCommitment)
                    {
                        throw new InvalidOperationException("Terminal claim has no distinct committed owner.");
                    }
                }
            }
        }

        private byte[] EncodeRetainedState(IEnumerable<OperationRecord> operations)
        {
            var ordered = operations
                .OrderBy(record => record.Request.AuthorityScopeCommitment)
                .ThenBy(record => record.Request.OperationCommitment)
                .ToArray();
            var fields = new List<byte[]>
            {
                DevelopmentFrameV1.UInt64Bytes((ulong)ordered.Length)
            };
            for (var index = 0; index < ordered.Length; index++)
            {
                fields.Add(EncodeAuthorityRecord(ordered[index]));
            }

            var payload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityPayloadDomain,
                fields);
            return DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Authority,
                _instanceId,
                _fixtures.FixtureDigest,
                payload);
        }

        private static byte[] EncodeAuthorityRecord(OperationRecord operation)
        {
            var request = operation.Request;
            return DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.AuthorityRecordDomain,
                DevelopmentFrameV1.UInt64Bytes((ulong)operation.State),
                request.AuthorityScopeCommitment.ToArray(),
                request.OperationCommitment.ToArray(),
                request.SemanticRequestFingerprint.ToArray(),
                request.OpaqueCompiledCoreDigest.ToArray(),
                request.NormalizedHandleCommitment.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(request.ExpectedGeneration));
        }

        private static bool TryDecodeAuthorityPayload(
            string instanceId,
            DevelopmentHandleAvailabilityFixtures fixtures,
            byte[] payload,
            out Dictionary<string, OperationRecord> operations,
            out DevelopmentRetainedStateFailure failure)
        {
            operations = null;
            failure = DevelopmentRetainedStateFailure.InvalidRecord;
            if (!DevelopmentFrameV1.TryDecodeDynamic(
                    payload,
                    DevelopmentCanonicalDigest.AuthorityPayloadDomain,
                    DevelopmentOnboardingAuthorityContracts.MaxOperationBindings + 1,
                    out var fields) ||
                fields.Length == 0 ||
                !DevelopmentFrameV1.TryUInt64(fields[0], out var count) ||
                count > DevelopmentOnboardingAuthorityContracts.MaxOperationBindings ||
                fields.Length != checked((int)count + 1))
            {
                failure = DevelopmentRetainedStateFailure.InvalidCount;
                return false;
            }

            var decoded = new Dictionary<string, OperationRecord>(StringComparer.Ordinal);
            string previousKey = null;
            for (var index = 1; index < fields.Length; index++)
            {
                if (!DevelopmentFrameV1.TryDecode(
                        fields[index],
                        DevelopmentCanonicalDigest.AuthorityRecordDomain,
                        7,
                        out var recordFields) ||
                    !DevelopmentFrameV1.TryUInt64(recordFields[0], out var stateValue) ||
                    !TryCommitment(recordFields[1], out var scope) ||
                    !TryCommitment(recordFields[2], out var operation) ||
                    !TryCommitment(recordFields[3], out var fingerprint) ||
                    !TryCommitment(recordFields[4], out var core) ||
                    !TryCommitment(recordFields[5], out var handle) ||
                    !DevelopmentFrameV1.TryUInt64(recordFields[6], out var expectedGeneration) ||
                    expectedGeneration != 0 ||
                    stateValue < (ulong)DevelopmentAuthorityRecordState.Committed ||
                    stateValue > (ulong)DevelopmentAuthorityRecordState.TerminalTakenClaim)
                {
                    failure = DevelopmentRetainedStateFailure.InvalidRecord;
                    return false;
                }

                var request = new DevelopmentOnboardingCommitRequest(
                    scope,
                    operation,
                    fingerprint,
                    core,
                    handle,
                    expectedGeneration);
                if (!request.IsValid)
                {
                    failure = DevelopmentRetainedStateFailure.InvalidRecord;
                    return false;
                }

                var key = OperationKey(scope, operation);
                if (previousKey != null && string.CompareOrdinal(previousKey, key) >= 0)
                {
                    failure = string.Equals(previousKey, key, StringComparison.Ordinal)
                        ? DevelopmentRetainedStateFailure.DuplicateRecord
                        : DevelopmentRetainedStateFailure.InvalidOrder;
                    return false;
                }

                var state = (DevelopmentAuthorityRecordState)stateValue;
                DevelopmentOnboardingAuthorityReceipt receipt = null;
                if (state == DevelopmentAuthorityRecordState.Committed)
                {
                    var receiptId = DevelopmentCanonicalDigest.ComputeReceiptId(instanceId, request);
                    receipt = new DevelopmentOnboardingAuthorityReceipt(
                        instanceId,
                        receiptId,
                        request,
                        1,
                        1,
                        DevelopmentOnboardingAuthorityContracts.ContractVersion,
                        DevelopmentCanonicalDigest.ComputeReceiptBodyDigest(instanceId, receiptId, request, 1, 1));
                }
                else if (state == DevelopmentAuthorityRecordState.TerminalTakenFixture && !fixtures.IsTaken(handle))
                {
                    failure = DevelopmentRetainedStateFailure.OrphanRecord;
                    return false;
                }

                decoded.Add(key, new OperationRecord(request, state, receipt));
                previousKey = key;
            }

            operations = decoded;
            failure = DevelopmentRetainedStateFailure.None;
            return true;
        }

        private static bool TryCommitment(byte[] bytes, out Commitment32 value)
        {
            value = default;
            if (bytes == null || bytes.Length != DevelopmentOnboardingAuthorityContracts.FixedBytesLength)
            {
                return false;
            }

            value = new Commitment32(bytes);
            return !value.IsZero;
        }

        private static bool IsNonzero(Commitment32 value) => value.IsValid && !value.IsZero;
        private static string ScopeKey(Commitment32 scope) => scope.ToHex();
        private static string HandleKey(Commitment32 handle) => handle.ToHex();
        private static string OperationKey(Commitment32 scope, Commitment32 operation) => scope.ToHex() + operation.ToHex();

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            return FixedBytesEncoding.Equals(left, right);
        }
    }

    public sealed class DeterministicDevelopmentLocalProjectionEmulator :
        IDevelopmentLocalProjectionEmulator,
        IDevelopmentLocalProjectionVerifier
    {
        private sealed class MarkerRecord
        {
            internal MarkerRecord(DevelopmentProjectionMarker marker)
            {
                Marker = marker;
            }

            internal DevelopmentProjectionMarker Marker { get; }
        }

        private readonly object _gate = new object();
        private readonly string _instanceId;
        private readonly Dictionary<string, MarkerRecord> _markers;
        private readonly Dictionary<string, ulong> _profileRevisions;
        private readonly Dictionary<string, Commitment32> _receiptOwners;

        public DeterministicDevelopmentLocalProjectionEmulator(string instanceId)
            : this(instanceId, new Dictionary<string, MarkerRecord>(StringComparer.Ordinal))
        {
        }

        private DeterministicDevelopmentLocalProjectionEmulator(
            string instanceId,
            Dictionary<string, MarkerRecord> markers)
        {
            DevelopmentInstanceId.Require(instanceId, nameof(instanceId));
            _instanceId = instanceId;
            _markers = markers ?? throw new ArgumentNullException(nameof(markers));
            _profileRevisions = new Dictionary<string, ulong>(StringComparer.Ordinal);
            _receiptOwners = new Dictionary<string, Commitment32>(StringComparer.Ordinal);
            RebuildIndexesOrThrow();
        }

        public string InstanceId => _instanceId;

        public DevelopmentProjectionResult TryProject(
            Commitment32 localProfileScopeCommitment,
            ulong expectedLocalProjectionRevision,
            VerifiedDevelopmentReceipt verifiedReceipt)
        {
            lock (_gate)
            {
                if (!IsNonzero(localProfileScopeCommitment))
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.InvalidInput,
                        DevelopmentAuthorityFailure.InvalidInput);
                }

                if (verifiedReceipt == null || !verifiedReceipt.IsValid ||
                    verifiedReceipt.Receipt == null || !verifiedReceipt.Handle.IsValid)
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.InvalidReceipt,
                        DevelopmentAuthorityFailure.InvalidVerifiedReceipt);
                }

                var markerKey = MarkerKey(localProfileScopeCommitment, verifiedReceipt.Handle);
                if (_markers.TryGetValue(markerKey, out var existing))
                {
                    if (existing.Marker.ExpectedLocalProjectionRevision != expectedLocalProjectionRevision)
                    {
                        return ProjectionFailure(
                            DevelopmentProjectionState.Collision,
                            DevelopmentAuthorityFailure.Collision);
                    }

                    return ProjectionSuccess(DevelopmentProjectionState.ReplayProjected, existing.Marker);
                }

                var receiptKey = ReceiptKey(verifiedReceipt.Handle);
                if (_receiptOwners.TryGetValue(receiptKey, out var owner) && owner != localProfileScopeCommitment)
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.ReceiptOwnedByOtherProfile,
                        DevelopmentAuthorityFailure.ReceiptOwnedByOtherProfile);
                }

                var profileKey = ProfileKey(localProfileScopeCommitment);
                var currentRevision = _profileRevisions.TryGetValue(profileKey, out var revision) ? revision : 0UL;
                if (currentRevision != expectedLocalProjectionRevision)
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.StaleRevision,
                        DevelopmentAuthorityFailure.StaleProjectionRevision);
                }

                if (expectedLocalProjectionRevision == ulong.MaxValue)
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.RevisionOverflow,
                        DevelopmentAuthorityFailure.ProjectionRevisionOverflow);
                }

                if (_markers.Count >= DevelopmentOnboardingAuthorityContracts.MaxProjectionMarkers)
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.CapacityUnavailable,
                        DevelopmentAuthorityFailure.CapacityUnavailable);
                }

                var resultingRevision = expectedLocalProjectionRevision + 1;
                const ulong markerRevision = 1;
                var markerId = DevelopmentCanonicalDigest.ComputeProjectionMarkerId(
                    _instanceId,
                    localProfileScopeCommitment,
                    verifiedReceipt.Handle,
                    expectedLocalProjectionRevision);
                var markerDigest = DevelopmentCanonicalDigest.ComputeProjectionMarkerDigest(
                    _instanceId,
                    markerId,
                    localProfileScopeCommitment,
                    verifiedReceipt.Handle,
                    expectedLocalProjectionRevision,
                    resultingRevision,
                    markerRevision);
                var marker = new DevelopmentProjectionMarker(
                    _instanceId,
                    markerId,
                    localProfileScopeCommitment,
                    verifiedReceipt.Handle,
                    expectedLocalProjectionRevision,
                    resultingRevision,
                    markerRevision,
                    DevelopmentOnboardingAuthorityContracts.ContractVersion,
                    markerDigest);

                _markers.Add(markerKey, new MarkerRecord(marker));
                _receiptOwners.Add(receiptKey, localProfileScopeCommitment);
                _profileRevisions[profileKey] = resultingRevision;
                return ProjectionSuccess(DevelopmentProjectionState.Projected, marker);
            }
        }

        public DevelopmentProjectionResult ReconcileProjection(
            Commitment32 localProfileScopeCommitment,
            DevelopmentReceiptHandle receiptHandle)
        {
            lock (_gate)
            {
                if (!IsNonzero(localProfileScopeCommitment) || !receiptHandle.IsValid)
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.InvalidInput,
                        DevelopmentAuthorityFailure.InvalidInput);
                }

                if (!_markers.TryGetValue(MarkerKey(localProfileScopeCommitment, receiptHandle), out var record))
                {
                    return ProjectionFailure(
                        DevelopmentProjectionState.NotFound,
                        DevelopmentAuthorityFailure.NotFound);
                }

                return ProjectionSuccess(DevelopmentProjectionState.ReplayProjected, record.Marker);
            }
        }

        public VerifiedDevelopmentProjection Verify(
            DevelopmentProjectionMarker candidateMarker,
            Commitment32 expectedLocalProfileScopeCommitment,
            DevelopmentReceiptHandle expectedReceiptHandle,
            ulong expectedLocalProjectionRevision,
            DevelopmentProjectionHandle expectedMarkerHandle)
        {
            lock (_gate)
            {
                if (candidateMarker == null || !IsNonzero(expectedLocalProfileScopeCommitment) ||
                    !expectedReceiptHandle.IsValid)
                {
                    return VerifiedDevelopmentProjection.FailureResult(DevelopmentAuthorityFailure.InvalidInput);
                }

                if (!MarkerIsCanonical(candidateMarker) ||
                    candidateMarker.LocalProfileScopeCommitment != expectedLocalProfileScopeCommitment ||
                    !candidateMarker.ReceiptHandle.Equals(expectedReceiptHandle) ||
                    candidateMarker.ExpectedLocalProjectionRevision != expectedLocalProjectionRevision ||
                    !candidateMarker.Handle.Equals(expectedMarkerHandle))
                {
                    return VerifiedDevelopmentProjection.FailureResult(DevelopmentAuthorityFailure.ProjectionMismatch);
                }

                if (!_markers.TryGetValue(
                        MarkerKey(expectedLocalProfileScopeCommitment, expectedReceiptHandle),
                        out var stored) ||
                    !MarkerEquals(candidateMarker, stored.Marker))
                {
                    return VerifiedDevelopmentProjection.FailureResult(DevelopmentAuthorityFailure.ProjectionMismatch);
                }

                return VerifiedDevelopmentProjection.Success(candidateMarker);
            }
        }

        public byte[] CaptureRetainedState()
        {
            lock (_gate)
            {
                return EncodeRetainedState(_markers.Values);
            }
        }

        public static bool TryRestore(
            string instanceId,
            byte[] retainedState,
            out DeterministicDevelopmentLocalProjectionEmulator emulator,
            out DevelopmentRetainedStateFailure failure)
        {
            emulator = null;
            if (!DevelopmentOnboardingAuthorityRetainedStateCodec.TryDecodeEnvelope(
                    retainedState,
                    DevelopmentRetainedStoreKind.Projection,
                    instanceId,
                    null,
                    out var payload,
                    out failure))
            {
                return false;
            }

            if (!TryDecodeProjectionPayload(instanceId, payload, out var markers, out failure))
            {
                return false;
            }

            try
            {
                var candidate = new DeterministicDevelopmentLocalProjectionEmulator(instanceId, markers);
                if (!FixedBytesEncoding.Equals(retainedState, candidate.CaptureRetainedState()))
                {
                    failure = DevelopmentRetainedStateFailure.InvalidRecord;
                    return false;
                }

                emulator = candidate;
                failure = DevelopmentRetainedStateFailure.None;
                return true;
            }
            catch (ArgumentException)
            {
                failure = DevelopmentRetainedStateFailure.InvalidRecord;
                return false;
            }
            catch (InvalidOperationException)
            {
                failure = DevelopmentRetainedStateFailure.InvalidRecord;
                return false;
            }
        }

        private static DevelopmentProjectionResult ProjectionFailure(
            DevelopmentProjectionState state,
            DevelopmentAuthorityFailure failure)
        {
            return new DevelopmentProjectionResult(state, failure);
        }

        private static DevelopmentProjectionResult ProjectionSuccess(
            DevelopmentProjectionState state,
            DevelopmentProjectionMarker marker)
        {
            return new DevelopmentProjectionResult(
                state,
                DevelopmentAuthorityFailure.None,
                VerifiedDevelopmentProjection.Success(marker));
        }

        private bool MarkerIsCanonical(DevelopmentProjectionMarker marker)
        {
            if (!string.Equals(marker.ProjectionInstanceId, _instanceId, StringComparison.Ordinal) ||
                !string.Equals(marker.ContractVersion, DevelopmentOnboardingAuthorityContracts.ContractVersion, StringComparison.Ordinal) ||
                !IsNonzero(marker.LocalProfileScopeCommitment) || !marker.ReceiptHandle.IsValid ||
                marker.ExpectedLocalProjectionRevision == ulong.MaxValue ||
                marker.ResultingLocalProjectionRevision != marker.ExpectedLocalProjectionRevision + 1 ||
                marker.MarkerRevision != 1)
            {
                return false;
            }

            var expectedId = DevelopmentCanonicalDigest.ComputeProjectionMarkerId(
                _instanceId,
                marker.LocalProfileScopeCommitment,
                marker.ReceiptHandle,
                marker.ExpectedLocalProjectionRevision);
            if (!string.Equals(marker.MarkerId, expectedId, StringComparison.Ordinal))
            {
                return false;
            }

            return marker.MarkerDigest == DevelopmentCanonicalDigest.ComputeProjectionMarkerDigest(
                _instanceId,
                marker.MarkerId,
                marker.LocalProfileScopeCommitment,
                marker.ReceiptHandle,
                marker.ExpectedLocalProjectionRevision,
                marker.ResultingLocalProjectionRevision,
                marker.MarkerRevision);
        }

        private static bool MarkerEquals(DevelopmentProjectionMarker left, DevelopmentProjectionMarker right)
        {
            return left != null && right != null &&
                   string.Equals(left.ProjectionInstanceId, right.ProjectionInstanceId, StringComparison.Ordinal) &&
                   string.Equals(left.MarkerId, right.MarkerId, StringComparison.Ordinal) &&
                   left.LocalProfileScopeCommitment == right.LocalProfileScopeCommitment &&
                   left.ReceiptHandle.Equals(right.ReceiptHandle) &&
                   left.ExpectedLocalProjectionRevision == right.ExpectedLocalProjectionRevision &&
                   left.ResultingLocalProjectionRevision == right.ResultingLocalProjectionRevision &&
                   left.MarkerRevision == right.MarkerRevision &&
                   string.Equals(left.ContractVersion, right.ContractVersion, StringComparison.Ordinal) &&
                   left.MarkerDigest == right.MarkerDigest;
        }

        private void RebuildIndexesOrThrow()
        {
            if (_markers.Count > DevelopmentOnboardingAuthorityContracts.MaxProjectionMarkers)
            {
                throw new InvalidOperationException("Projection marker capacity exceeded.");
            }

            var byProfile = new Dictionary<string, List<DevelopmentProjectionMarker>>(StringComparer.Ordinal);
            foreach (var pair in _markers)
            {
                var marker = pair.Value?.Marker;
                if (marker == null || !MarkerIsCanonical(marker) ||
                    !string.Equals(pair.Key, MarkerKey(marker.LocalProfileScopeCommitment, marker.ReceiptHandle), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid projection marker.");
                }

                var receiptKey = ReceiptKey(marker.ReceiptHandle);
                if (_receiptOwners.TryGetValue(receiptKey, out var existingOwner) &&
                    existingOwner != marker.LocalProfileScopeCommitment)
                {
                    throw new InvalidOperationException("Receipt has multiple projection owners.");
                }

                _receiptOwners[receiptKey] = marker.LocalProfileScopeCommitment;
                var profileKey = ProfileKey(marker.LocalProfileScopeCommitment);
                if (!byProfile.TryGetValue(profileKey, out var markers))
                {
                    markers = new List<DevelopmentProjectionMarker>();
                    byProfile.Add(profileKey, markers);
                }

                markers.Add(marker);
            }

            foreach (var pair in byProfile)
            {
                var ordered = pair.Value.OrderBy(marker => marker.ExpectedLocalProjectionRevision).ToArray();
                for (var index = 0; index < ordered.Length; index++)
                {
                    if (ordered[index].ExpectedLocalProjectionRevision != (ulong)index ||
                        ordered[index].ResultingLocalProjectionRevision != (ulong)index + 1)
                    {
                        throw new InvalidOperationException("Projection revisions are not contiguous.");
                    }
                }

                _profileRevisions.Add(pair.Key, (ulong)ordered.Length);
            }
        }

        private byte[] EncodeRetainedState(IEnumerable<MarkerRecord> records)
        {
            var ordered = records
                .Select(record => record.Marker)
                .OrderBy(marker => marker.LocalProfileScopeCommitment)
                .ThenBy(marker => marker.ExpectedLocalProjectionRevision)
                .ThenBy(marker => ReceiptKey(marker.ReceiptHandle), StringComparer.Ordinal)
                .ToArray();
            var fields = new List<byte[]>
            {
                DevelopmentFrameV1.UInt64Bytes((ulong)ordered.Length)
            };
            for (var index = 0; index < ordered.Length; index++)
            {
                fields.Add(EncodeProjectionRecord(ordered[index]));
            }

            var payload = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.ProjectionPayloadDomain,
                fields);
            return DevelopmentOnboardingAuthorityRetainedStateCodec.EncodeEnvelope(
                DevelopmentRetainedStoreKind.Projection,
                _instanceId,
                null,
                payload);
        }

        private static byte[] EncodeProjectionRecord(DevelopmentProjectionMarker marker)
        {
            return DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.ProjectionRecordDomain,
                marker.LocalProfileScopeCommitment.ToArray(),
                DevelopmentFrameV1.Utf8(marker.ReceiptHandle.AuthorityInstanceId),
                DevelopmentFrameV1.Utf8(marker.ReceiptHandle.ContractVersion),
                DevelopmentFrameV1.Utf8(marker.ReceiptHandle.ReceiptId),
                marker.ReceiptHandle.BodyDigest.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(marker.ExpectedLocalProjectionRevision));
        }

        private static bool TryDecodeProjectionPayload(
            string instanceId,
            byte[] payload,
            out Dictionary<string, MarkerRecord> markers,
            out DevelopmentRetainedStateFailure failure)
        {
            markers = null;
            failure = DevelopmentRetainedStateFailure.InvalidRecord;
            if (!DevelopmentFrameV1.TryDecodeDynamic(
                    payload,
                    DevelopmentCanonicalDigest.ProjectionPayloadDomain,
                    DevelopmentOnboardingAuthorityContracts.MaxProjectionMarkers + 1,
                    out var fields) ||
                fields.Length == 0 ||
                !DevelopmentFrameV1.TryUInt64(fields[0], out var count) ||
                count > DevelopmentOnboardingAuthorityContracts.MaxProjectionMarkers ||
                fields.Length != checked((int)count + 1))
            {
                failure = DevelopmentRetainedStateFailure.InvalidCount;
                return false;
            }

            var decoded = new Dictionary<string, MarkerRecord>(StringComparer.Ordinal);
            string previousOrderKey = null;
            for (var index = 1; index < fields.Length; index++)
            {
                if (!DevelopmentFrameV1.TryDecode(
                        fields[index],
                        DevelopmentCanonicalDigest.ProjectionRecordDomain,
                        6,
                        out var recordFields) ||
                    !TryCommitment(recordFields[0], out var localScope) ||
                    !DevelopmentFrameV1.TryUtf8(recordFields[1], out var authorityInstanceId) ||
                    !DevelopmentInstanceId.IsValid(authorityInstanceId) ||
                    !DevelopmentFrameV1.TryUtf8(recordFields[2], out var contractVersion) ||
                    !string.Equals(contractVersion, DevelopmentOnboardingAuthorityContracts.ContractVersion, StringComparison.Ordinal) ||
                    !DevelopmentFrameV1.TryUtf8(recordFields[3], out var receiptId) ||
                    !DevelopmentRecordId.IsValid(receiptId, "devrcpt_") ||
                    recordFields[4] == null || recordFields[4].Length != DevelopmentOnboardingAuthorityContracts.FixedBytesLength ||
                    !DevelopmentFrameV1.TryUInt64(recordFields[5], out var expectedRevision) ||
                    expectedRevision == ulong.MaxValue)
                {
                    failure = DevelopmentRetainedStateFailure.InvalidRecord;
                    return false;
                }

                var receiptHandle = new DevelopmentReceiptHandle(
                    authorityInstanceId,
                    contractVersion,
                    receiptId,
                    new Digest32(recordFields[4]));
                if (!receiptHandle.IsValid)
                {
                    failure = DevelopmentRetainedStateFailure.InvalidRecord;
                    return false;
                }

                var resultingRevision = expectedRevision + 1;
                const ulong markerRevision = 1;
                var markerId = DevelopmentCanonicalDigest.ComputeProjectionMarkerId(
                    instanceId,
                    localScope,
                    receiptHandle,
                    expectedRevision);
                var marker = new DevelopmentProjectionMarker(
                    instanceId,
                    markerId,
                    localScope,
                    receiptHandle,
                    expectedRevision,
                    resultingRevision,
                    markerRevision,
                    DevelopmentOnboardingAuthorityContracts.ContractVersion,
                    DevelopmentCanonicalDigest.ComputeProjectionMarkerDigest(
                        instanceId,
                        markerId,
                        localScope,
                        receiptHandle,
                        expectedRevision,
                        resultingRevision,
                        markerRevision));

                var orderKey = ProfileKey(localScope) +
                               expectedRevision.ToString("D20", CultureInfo.InvariantCulture) +
                               ReceiptKey(receiptHandle);
                if (previousOrderKey != null && string.CompareOrdinal(previousOrderKey, orderKey) >= 0)
                {
                    failure = string.Equals(previousOrderKey, orderKey, StringComparison.Ordinal)
                        ? DevelopmentRetainedStateFailure.DuplicateRecord
                        : DevelopmentRetainedStateFailure.InvalidOrder;
                    return false;
                }

                var markerKey = MarkerKey(localScope, receiptHandle);
                if (decoded.ContainsKey(markerKey))
                {
                    failure = DevelopmentRetainedStateFailure.DuplicateRecord;
                    return false;
                }

                decoded.Add(markerKey, new MarkerRecord(marker));
                previousOrderKey = orderKey;
            }

            markers = decoded;
            failure = DevelopmentRetainedStateFailure.None;
            return true;
        }

        private static bool TryCommitment(byte[] bytes, out Commitment32 value)
        {
            value = default;
            if (bytes == null || bytes.Length != DevelopmentOnboardingAuthorityContracts.FixedBytesLength)
            {
                return false;
            }

            value = new Commitment32(bytes);
            return !value.IsZero;
        }

        private static bool IsNonzero(Commitment32 value) => value.IsValid && !value.IsZero;
        private static string ProfileKey(Commitment32 scope) => scope.ToHex();
        private static string ReceiptKey(DevelopmentReceiptHandle handle) =>
            handle.AuthorityInstanceId + "|" + handle.ContractVersion + "|" + handle.ReceiptId + "|" + handle.BodyDigest.ToHex();
        private static string MarkerKey(Commitment32 scope, DevelopmentReceiptHandle handle) =>
            ProfileKey(scope) + "|" + ReceiptKey(handle);
    }
}
