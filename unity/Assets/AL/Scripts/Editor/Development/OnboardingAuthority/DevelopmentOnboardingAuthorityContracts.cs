#if !UNITY_EDITOR
#error DEVELOPMENT_EMULATOR_V1 is editor-only and must never compile into a Player.
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AL.Development.OnboardingAuthority.Emulator.Tests")]

namespace AL.Editor.Development.OnboardingAuthority
{
    public static class DevelopmentOnboardingAuthorityContracts
    {
        public const string ContractVersion = "DEVELOPMENT_EMULATOR_V1";
        public const int FixedBytesLength = 32;
        public const int MaxInstanceIdBytes = 64;
        public const int MaxOperationBindings = 64;
        public const int MaxCommittedReceipts = 64;
        public const int MaxClaimedHandles = 64;
        public const int MaxAvailabilityFixturesPerKind = 64;
        public const int MaxProjectionMarkers = 64;
        public const int MaxRetainedEnvelopeBytes = 262144;
    }

    public readonly struct Commitment32 : IEquatable<Commitment32>, IComparable<Commitment32>
    {
        private readonly byte[] _bytes;

        public Commitment32(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length != DevelopmentOnboardingAuthorityContracts.FixedBytesLength)
            {
                throw new ArgumentException("Commitment32 requires exactly 32 bytes.", nameof(bytes));
            }

            _bytes = (byte[])bytes.Clone();
        }

        public bool IsValid => _bytes != null && _bytes.Length == DevelopmentOnboardingAuthorityContracts.FixedBytesLength;

        public bool IsZero
        {
            get
            {
                if (!IsValid)
                {
                    return true;
                }

                var aggregate = 0;
                for (var index = 0; index < _bytes.Length; index++)
                {
                    aggregate |= _bytes[index];
                }

                return aggregate == 0;
            }
        }

        public byte[] ToArray()
        {
            return IsValid ? (byte[])_bytes.Clone() : Array.Empty<byte>();
        }

        public string ToHex()
        {
            return FixedBytesEncoding.ToHex(ToArray());
        }

        public bool Equals(Commitment32 other)
        {
            return FixedBytesEncoding.Equals(_bytes, other._bytes);
        }

        public override bool Equals(object obj)
        {
            return obj is Commitment32 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return FixedBytesEncoding.GetHashCode(_bytes);
        }

        public int CompareTo(Commitment32 other)
        {
            return FixedBytesEncoding.Compare(_bytes, other._bytes);
        }

        public override string ToString()
        {
            return ToHex();
        }

        public static bool operator ==(Commitment32 left, Commitment32 right) => left.Equals(right);
        public static bool operator !=(Commitment32 left, Commitment32 right) => !left.Equals(right);
    }

    public readonly struct Digest32 : IEquatable<Digest32>, IComparable<Digest32>
    {
        private readonly byte[] _bytes;

        public Digest32(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length != DevelopmentOnboardingAuthorityContracts.FixedBytesLength)
            {
                throw new ArgumentException("Digest32 requires exactly 32 bytes.", nameof(bytes));
            }

            _bytes = (byte[])bytes.Clone();
        }

        public bool IsValid => _bytes != null && _bytes.Length == DevelopmentOnboardingAuthorityContracts.FixedBytesLength;

        public bool IsZero
        {
            get
            {
                if (!IsValid)
                {
                    return true;
                }

                var aggregate = 0;
                for (var index = 0; index < _bytes.Length; index++)
                {
                    aggregate |= _bytes[index];
                }

                return aggregate == 0;
            }
        }

        public byte[] ToArray()
        {
            return IsValid ? (byte[])_bytes.Clone() : Array.Empty<byte>();
        }

        public string ToHex()
        {
            return FixedBytesEncoding.ToHex(ToArray());
        }

        public bool Equals(Digest32 other)
        {
            return FixedBytesEncoding.Equals(_bytes, other._bytes);
        }

        public override bool Equals(object obj)
        {
            return obj is Digest32 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return FixedBytesEncoding.GetHashCode(_bytes);
        }

        public int CompareTo(Digest32 other)
        {
            return FixedBytesEncoding.Compare(_bytes, other._bytes);
        }

        public override string ToString()
        {
            return ToHex();
        }

        public static bool operator ==(Digest32 left, Digest32 right) => left.Equals(right);
        public static bool operator !=(Digest32 left, Digest32 right) => !left.Equals(right);
    }

    internal static class FixedBytesEncoding
    {
        private const string HexAlphabet = "0123456789abcdef";

        internal static bool Equals(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return left != null;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }

        internal static int Compare(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                if (left == null && right == null)
                {
                    return 0;
                }

                return left == null ? -1 : 1;
            }

            var length = Math.Min(left.Length, right.Length);
            for (var index = 0; index < length; index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Length.CompareTo(right.Length);
        }

        internal static int GetHashCode(byte[] bytes)
        {
            if (bytes == null)
            {
                return 0;
            }

            unchecked
            {
                var hash = 17;
                for (var index = 0; index < bytes.Length; index++)
                {
                    hash = (hash * 31) + bytes[index];
                }

                return hash;
            }
        }

        internal static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var characters = new char[bytes.Length * 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = HexAlphabet[bytes[index] >> 4];
                characters[(index * 2) + 1] = HexAlphabet[bytes[index] & 0x0f];
            }

            return new string(characters);
        }
    }

    public enum DevelopmentAuthorityFailure
    {
        None = 0,
        InvalidInput = 1,
        Collision = 2,
        StaleGeneration = 3,
        GenerationOverflow = 4,
        CapacityUnavailable = 5,
        HandleTaken = 6,
        AuthorityUnavailable = 7,
        ScopeAlreadyCommitted = 8,
        NotFound = 9,
        ReceiptMismatch = 10,
        ReceiptNotCommitted = 11,
        InvalidVerifiedReceipt = 12,
        StaleProjectionRevision = 13,
        ProjectionRevisionOverflow = 14,
        ReceiptOwnedByOtherProfile = 15,
        ProjectionMismatch = 16,
        IntegrityFailure = 17
    }

    public enum DevelopmentHandleAvailabilityState
    {
        Available = 1,
        Taken = 2,
        Unavailable = 3,
        CapacityUnavailable = 4,
        InvalidInput = 5,
        StaleGeneration = 6
    }

    public sealed class DevelopmentHandleAvailability
    {
        internal DevelopmentHandleAvailability(
            DevelopmentHandleAvailabilityState state,
            Commitment32 handleCommitment,
            DevelopmentAuthorityFailure failure)
        {
            State = state;
            HandleCommitment = handleCommitment;
            Failure = failure;
        }

        public DevelopmentHandleAvailabilityState State { get; }
        public Commitment32 HandleCommitment { get; }
        public DevelopmentAuthorityFailure Failure { get; }
        public bool IsAvailable => State == DevelopmentHandleAvailabilityState.Available;
    }

    public sealed class DevelopmentHandleAvailabilityFixtures
    {
        private readonly ReadOnlyCollection<Commitment32> _taken;
        private readonly ReadOnlyCollection<Commitment32> _unavailable;
        private readonly HashSet<Commitment32> _takenSet;
        private readonly HashSet<Commitment32> _unavailableSet;

        public DevelopmentHandleAvailabilityFixtures(
            IEnumerable<Commitment32> takenHandles = null,
            IEnumerable<Commitment32> unavailableHandles = null)
        {
            var taken = CopyAndValidate(takenHandles, nameof(takenHandles));
            var unavailable = CopyAndValidate(unavailableHandles, nameof(unavailableHandles));
            _takenSet = new HashSet<Commitment32>(taken);
            _unavailableSet = new HashSet<Commitment32>(unavailable);

            if (_takenSet.Overlaps(_unavailableSet))
            {
                throw new ArgumentException("A handle cannot be both taken and unavailable.");
            }

            _taken = Array.AsReadOnly(taken);
            _unavailable = Array.AsReadOnly(unavailable);
            FixtureDigest = DevelopmentCanonicalDigest.ComputeAvailabilityFixtureDigest(taken, unavailable);
        }

        public static DevelopmentHandleAvailabilityFixtures Empty { get; } =
            new DevelopmentHandleAvailabilityFixtures();

        public IReadOnlyList<Commitment32> TakenHandles => _taken;
        public IReadOnlyList<Commitment32> UnavailableHandles => _unavailable;
        public Digest32 FixtureDigest { get; }

        internal bool IsTaken(Commitment32 handle) => _takenSet.Contains(handle);
        internal bool IsUnavailable(Commitment32 handle) => _unavailableSet.Contains(handle);

        private static Commitment32[] CopyAndValidate(
            IEnumerable<Commitment32> source,
            string argumentName)
        {
            var values = source == null ? Array.Empty<Commitment32>() : source.ToArray();
            if (values.Length > DevelopmentOnboardingAuthorityContracts.MaxAvailabilityFixturesPerKind)
            {
                throw new ArgumentException("Availability fixtures exceed the bounded capacity.", argumentName);
            }

            Array.Sort(values);
            for (var index = 0; index < values.Length; index++)
            {
                if (!values[index].IsValid || values[index].IsZero)
                {
                    throw new ArgumentException("Availability fixtures require nonzero Commitment32 values.", argumentName);
                }

                if (index > 0 && values[index - 1] == values[index])
                {
                    throw new ArgumentException("Availability fixtures cannot contain duplicates.", argumentName);
                }
            }

            return values;
        }
    }

    public readonly struct DevelopmentOnboardingCommitRequest : IEquatable<DevelopmentOnboardingCommitRequest>
    {
        public DevelopmentOnboardingCommitRequest(
            Commitment32 authorityScopeCommitment,
            Commitment32 operationCommitment,
            Commitment32 semanticRequestFingerprint,
            Commitment32 opaqueCompiledCoreDigest,
            Commitment32 normalizedHandleCommitment,
            ulong expectedGeneration)
        {
            AuthorityScopeCommitment = authorityScopeCommitment;
            OperationCommitment = operationCommitment;
            SemanticRequestFingerprint = semanticRequestFingerprint;
            OpaqueCompiledCoreDigest = opaqueCompiledCoreDigest;
            NormalizedHandleCommitment = normalizedHandleCommitment;
            ExpectedGeneration = expectedGeneration;
        }

        public Commitment32 AuthorityScopeCommitment { get; }
        public Commitment32 OperationCommitment { get; }
        public Commitment32 SemanticRequestFingerprint { get; }
        public Commitment32 OpaqueCompiledCoreDigest { get; }
        public Commitment32 NormalizedHandleCommitment { get; }
        public ulong ExpectedGeneration { get; }

        public bool IsValid =>
            AuthorityScopeCommitment.IsValid && !AuthorityScopeCommitment.IsZero &&
            OperationCommitment.IsValid && !OperationCommitment.IsZero &&
            SemanticRequestFingerprint.IsValid && !SemanticRequestFingerprint.IsZero &&
            OpaqueCompiledCoreDigest.IsValid && !OpaqueCompiledCoreDigest.IsZero &&
            NormalizedHandleCommitment.IsValid && !NormalizedHandleCommitment.IsZero;

        public bool Equals(DevelopmentOnboardingCommitRequest other)
        {
            return AuthorityScopeCommitment == other.AuthorityScopeCommitment &&
                   OperationCommitment == other.OperationCommitment &&
                   SemanticRequestFingerprint == other.SemanticRequestFingerprint &&
                   OpaqueCompiledCoreDigest == other.OpaqueCompiledCoreDigest &&
                   NormalizedHandleCommitment == other.NormalizedHandleCommitment &&
                   ExpectedGeneration == other.ExpectedGeneration;
        }

        public override bool Equals(object obj)
        {
            return obj is DevelopmentOnboardingCommitRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = AuthorityScopeCommitment.GetHashCode();
                hash = (hash * 397) ^ OperationCommitment.GetHashCode();
                hash = (hash * 397) ^ SemanticRequestFingerprint.GetHashCode();
                hash = (hash * 397) ^ OpaqueCompiledCoreDigest.GetHashCode();
                hash = (hash * 397) ^ NormalizedHandleCommitment.GetHashCode();
                hash = (hash * 397) ^ ExpectedGeneration.GetHashCode();
                return hash;
            }
        }
    }

    public enum DevelopmentOnboardingCommitState
    {
        Committed = 1,
        ReplayCommitted = 2,
        TerminalHandleTaken = 3,
        ReplayTerminalHandleTaken = 4,
        Collision = 5,
        InvalidInput = 6,
        CapacityUnavailable = 7,
        StaleGeneration = 8,
        GenerationOverflow = 9,
        ScopeAlreadyCommitted = 10,
        AuthorityUnavailable = 11
    }

    public sealed class DevelopmentOnboardingCommitResult
    {
        internal DevelopmentOnboardingCommitResult(
            DevelopmentOnboardingCommitState state,
            DevelopmentAuthorityFailure failure,
            DevelopmentOnboardingAuthorityReceipt receipt = null)
        {
            State = state;
            Failure = failure;
            Receipt = receipt;
        }

        public DevelopmentOnboardingCommitState State { get; }
        public DevelopmentAuthorityFailure Failure { get; }
        public DevelopmentOnboardingAuthorityReceipt Receipt { get; }
    }

    public enum DevelopmentOnboardingReconcileState
    {
        Committed = 1,
        TerminalHandleTaken = 2,
        Collision = 3,
        NotFound = 4,
        InvalidInput = 5
    }

    public sealed class DevelopmentOnboardingReconcileResult
    {
        internal DevelopmentOnboardingReconcileResult(
            DevelopmentOnboardingReconcileState state,
            DevelopmentAuthorityFailure failure,
            DevelopmentOnboardingAuthorityReceipt receipt = null)
        {
            State = state;
            Failure = failure;
            Receipt = receipt;
        }

        public DevelopmentOnboardingReconcileState State { get; }
        public DevelopmentAuthorityFailure Failure { get; }
        public DevelopmentOnboardingAuthorityReceipt Receipt { get; }
    }

    public readonly struct DevelopmentReceiptHandle : IEquatable<DevelopmentReceiptHandle>
    {
        public DevelopmentReceiptHandle(
            string authorityInstanceId,
            string contractVersion,
            string receiptId,
            Digest32 bodyDigest)
        {
            AuthorityInstanceId = authorityInstanceId;
            ContractVersion = contractVersion;
            ReceiptId = receiptId;
            BodyDigest = bodyDigest;
        }

        public string AuthorityInstanceId { get; }
        public string ContractVersion { get; }
        public string ReceiptId { get; }
        public Digest32 BodyDigest { get; }

        public bool IsValid =>
            DevelopmentInstanceId.IsValid(AuthorityInstanceId) &&
            string.Equals(ContractVersion, DevelopmentOnboardingAuthorityContracts.ContractVersion, StringComparison.Ordinal) &&
            DevelopmentRecordId.IsValid(ReceiptId, "devrcpt_") &&
            BodyDigest.IsValid && !BodyDigest.IsZero;

        public bool Equals(DevelopmentReceiptHandle other)
        {
            return string.Equals(AuthorityInstanceId, other.AuthorityInstanceId, StringComparison.Ordinal) &&
                   string.Equals(ContractVersion, other.ContractVersion, StringComparison.Ordinal) &&
                   string.Equals(ReceiptId, other.ReceiptId, StringComparison.Ordinal) &&
                   BodyDigest == other.BodyDigest;
        }

        public override bool Equals(object obj)
        {
            return obj is DevelopmentReceiptHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = AuthorityInstanceId == null ? 0 : StringComparer.Ordinal.GetHashCode(AuthorityInstanceId);
                hash = (hash * 397) ^ (ContractVersion == null ? 0 : StringComparer.Ordinal.GetHashCode(ContractVersion));
                hash = (hash * 397) ^ (ReceiptId == null ? 0 : StringComparer.Ordinal.GetHashCode(ReceiptId));
                hash = (hash * 397) ^ BodyDigest.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class DevelopmentOnboardingAuthorityReceipt
    {
        public DevelopmentOnboardingAuthorityReceipt(
            string authorityInstanceId,
            string receiptId,
            DevelopmentOnboardingCommitRequest request,
            ulong committedGeneration,
            ulong authorityRevision,
            string contractVersion,
            Digest32 bodyDigest)
        {
            AuthorityInstanceId = authorityInstanceId;
            ReceiptId = receiptId;
            Request = request;
            CommittedGeneration = committedGeneration;
            AuthorityRevision = authorityRevision;
            ContractVersion = contractVersion;
            BodyDigest = bodyDigest;
        }

        public string AuthorityInstanceId { get; }
        public string ReceiptId { get; }
        public DevelopmentOnboardingCommitRequest Request { get; }
        public ulong CommittedGeneration { get; }
        public ulong AuthorityRevision { get; }
        public string ContractVersion { get; }
        public Digest32 BodyDigest { get; }

        public DevelopmentReceiptHandle Handle => new DevelopmentReceiptHandle(
            AuthorityInstanceId,
            ContractVersion,
            ReceiptId,
            BodyDigest);
    }

    public sealed class VerifiedDevelopmentReceipt
    {
        private VerifiedDevelopmentReceipt(
            bool isValid,
            DevelopmentAuthorityFailure failure,
            DevelopmentOnboardingAuthorityReceipt receipt,
            DevelopmentReceiptHandle handle)
        {
            IsValid = isValid;
            Failure = failure;
            Receipt = receipt;
            Handle = handle;
        }

        public bool IsValid { get; }
        public DevelopmentAuthorityFailure Failure { get; }
        public DevelopmentOnboardingAuthorityReceipt Receipt { get; }
        public DevelopmentReceiptHandle Handle { get; }

        internal static VerifiedDevelopmentReceipt Success(
            DevelopmentOnboardingAuthorityReceipt receipt)
        {
            return new VerifiedDevelopmentReceipt(true, DevelopmentAuthorityFailure.None, receipt, receipt.Handle);
        }

        internal static VerifiedDevelopmentReceipt FailureResult(DevelopmentAuthorityFailure failure)
        {
            return new VerifiedDevelopmentReceipt(false, failure, null, default);
        }
    }

    public enum DevelopmentProjectionState
    {
        Projected = 1,
        ReplayProjected = 2,
        Collision = 3,
        InvalidInput = 4,
        InvalidReceipt = 5,
        StaleRevision = 6,
        RevisionOverflow = 7,
        ReceiptOwnedByOtherProfile = 8,
        CapacityUnavailable = 9,
        NotFound = 10
    }

    public readonly struct DevelopmentProjectionHandle : IEquatable<DevelopmentProjectionHandle>
    {
        public DevelopmentProjectionHandle(
            string projectionInstanceId,
            string contractVersion,
            string markerId,
            Digest32 markerDigest)
        {
            ProjectionInstanceId = projectionInstanceId;
            ContractVersion = contractVersion;
            MarkerId = markerId;
            MarkerDigest = markerDigest;
        }

        public string ProjectionInstanceId { get; }
        public string ContractVersion { get; }
        public string MarkerId { get; }
        public Digest32 MarkerDigest { get; }

        public bool Equals(DevelopmentProjectionHandle other)
        {
            return string.Equals(ProjectionInstanceId, other.ProjectionInstanceId, StringComparison.Ordinal) &&
                   string.Equals(ContractVersion, other.ContractVersion, StringComparison.Ordinal) &&
                   string.Equals(MarkerId, other.MarkerId, StringComparison.Ordinal) &&
                   MarkerDigest == other.MarkerDigest;
        }

        public override bool Equals(object obj)
        {
            return obj is DevelopmentProjectionHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ProjectionInstanceId == null ? 0 : StringComparer.Ordinal.GetHashCode(ProjectionInstanceId);
                hash = (hash * 397) ^ (ContractVersion == null ? 0 : StringComparer.Ordinal.GetHashCode(ContractVersion));
                hash = (hash * 397) ^ (MarkerId == null ? 0 : StringComparer.Ordinal.GetHashCode(MarkerId));
                hash = (hash * 397) ^ MarkerDigest.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class DevelopmentProjectionMarker
    {
        public DevelopmentProjectionMarker(
            string projectionInstanceId,
            string markerId,
            Commitment32 localProfileScopeCommitment,
            DevelopmentReceiptHandle receiptHandle,
            ulong expectedLocalProjectionRevision,
            ulong resultingLocalProjectionRevision,
            ulong markerRevision,
            string contractVersion,
            Digest32 markerDigest)
        {
            ProjectionInstanceId = projectionInstanceId;
            MarkerId = markerId;
            LocalProfileScopeCommitment = localProfileScopeCommitment;
            ReceiptHandle = receiptHandle;
            ExpectedLocalProjectionRevision = expectedLocalProjectionRevision;
            ResultingLocalProjectionRevision = resultingLocalProjectionRevision;
            MarkerRevision = markerRevision;
            ContractVersion = contractVersion;
            MarkerDigest = markerDigest;
        }

        public string ProjectionInstanceId { get; }
        public string MarkerId { get; }
        public Commitment32 LocalProfileScopeCommitment { get; }
        public DevelopmentReceiptHandle ReceiptHandle { get; }
        public ulong ExpectedLocalProjectionRevision { get; }
        public ulong ResultingLocalProjectionRevision { get; }
        public ulong MarkerRevision { get; }
        public string ContractVersion { get; }
        public Digest32 MarkerDigest { get; }

        public DevelopmentProjectionHandle Handle => new DevelopmentProjectionHandle(
            ProjectionInstanceId,
            ContractVersion,
            MarkerId,
            MarkerDigest);
    }

    public sealed class VerifiedDevelopmentProjection
    {
        private VerifiedDevelopmentProjection(
            bool isValid,
            DevelopmentAuthorityFailure failure,
            DevelopmentProjectionMarker marker)
        {
            IsValid = isValid;
            Failure = failure;
            Marker = marker;
            Handle = marker == null ? default : marker.Handle;
        }

        public bool IsValid { get; }
        public DevelopmentAuthorityFailure Failure { get; }
        public DevelopmentProjectionMarker Marker { get; }
        public DevelopmentProjectionHandle Handle { get; }

        internal static VerifiedDevelopmentProjection Success(DevelopmentProjectionMarker marker)
        {
            return new VerifiedDevelopmentProjection(true, DevelopmentAuthorityFailure.None, marker);
        }

        internal static VerifiedDevelopmentProjection FailureResult(DevelopmentAuthorityFailure failure)
        {
            return new VerifiedDevelopmentProjection(false, failure, null);
        }
    }

    public sealed class DevelopmentProjectionResult
    {
        internal DevelopmentProjectionResult(
            DevelopmentProjectionState state,
            DevelopmentAuthorityFailure failure,
            VerifiedDevelopmentProjection verifiedProjection = null)
        {
            State = state;
            Failure = failure;
            VerifiedProjection = verifiedProjection;
        }

        public DevelopmentProjectionState State { get; }
        public DevelopmentAuthorityFailure Failure { get; }
        public VerifiedDevelopmentProjection VerifiedProjection { get; }
        public DevelopmentProjectionMarker Marker => VerifiedProjection?.Marker;
    }

    public enum DevelopmentRetainedStateFailure
    {
        None = 0,
        NullOrEmpty = 1,
        Oversized = 2,
        InvalidEncoding = 3,
        InvalidFrame = 4,
        WrongKind = 5,
        WrongContract = 6,
        InstanceMismatch = 7,
        FixtureMismatch = 8,
        DigestMismatch = 9,
        InvalidCount = 10,
        InvalidOrder = 11,
        DuplicateRecord = 12,
        InvalidRecord = 13,
        OrphanRecord = 14,
        CapacityExceeded = 15,
        TrailingBytes = 16
    }

    public interface IDevelopmentOnboardingAuthorityEmulator
    {
        DevelopmentHandleAvailability CheckHandle(
            Commitment32 authorityScopeCommitment,
            Commitment32 normalizedHandleCommitment,
            ulong expectedGeneration);

        DevelopmentOnboardingCommitResult TryCommit(DevelopmentOnboardingCommitRequest request);

        DevelopmentOnboardingReconcileResult Reconcile(DevelopmentOnboardingCommitRequest request);

        byte[] CaptureRetainedState();
    }

    public interface IDevelopmentOnboardingReceiptVerifier
    {
        VerifiedDevelopmentReceipt Verify(
            DevelopmentOnboardingAuthorityReceipt candidateReceipt,
            DevelopmentOnboardingCommitRequest expectedRequest,
            DevelopmentReceiptHandle expectedHandle);
    }

    public interface IDevelopmentLocalProjectionEmulator
    {
        DevelopmentProjectionResult TryProject(
            Commitment32 localProfileScopeCommitment,
            ulong expectedLocalProjectionRevision,
            VerifiedDevelopmentReceipt verifiedReceipt);

        DevelopmentProjectionResult ReconcileProjection(
            Commitment32 localProfileScopeCommitment,
            DevelopmentReceiptHandle receiptHandle);

        byte[] CaptureRetainedState();
    }

    public interface IDevelopmentLocalProjectionVerifier
    {
        VerifiedDevelopmentProjection Verify(
            DevelopmentProjectionMarker candidateMarker,
            Commitment32 expectedLocalProfileScopeCommitment,
            DevelopmentReceiptHandle expectedReceiptHandle,
            ulong expectedLocalProjectionRevision,
            DevelopmentProjectionHandle expectedMarkerHandle);
    }

    internal static class DevelopmentInstanceId
    {
        internal static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > DevelopmentOnboardingAuthorityContracts.MaxInstanceIdBytes)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var allowed =
                    character >= 'a' && character <= 'z' ||
                    character >= 'A' && character <= 'Z' ||
                    character >= '0' && character <= '9' ||
                    character == '.' || character == '_' || character == '-';
                if (!allowed || index == 0 && (character == '.' || character == '_' || character == '-'))
                {
                    return false;
                }
            }

            return true;
        }

        internal static void Require(string value, string argumentName)
        {
            if (!IsValid(value))
            {
                throw new ArgumentException("Instance ID must be 1..64 strict ASCII identifier bytes.", argumentName);
            }
        }
    }

    internal static class DevelopmentRecordId
    {
        internal static bool IsValid(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var suffixLength = value.Length - prefix.Length;
            if (suffixLength != 64)
            {
                return false;
            }

            for (var index = prefix.Length; index < value.Length; index++)
            {
                var character = value[index];
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
