using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AL.Core.SaveAuthority
{
    public enum AuthorityArtifactRole
    {
        Primary = 1,
        Backup = 2,
        Temp = 3,
        CanonicalPrevious = 4,
        LegacyPrevious = 5,
        RecoveryWitness = 6
    }

    public enum AuthorityArtifactDisposition
    {
        Missing = 1,
        VerifiedExact = 2
    }

    public enum VerifiedAuthorityLedgerState
    {
        CanonicalCurrent = 1,
        RecoveredCurrent = 2,
        VerifiedRollback = 3
    }

    public sealed class SerializedAuthorityArtifactIdentity
    {
        public SerializedAuthorityArtifactIdentity(
            AuthorityArtifactRole role,
            AuthorityArtifactDisposition disposition,
            long byteCount,
            string sha256)
        {
            Role = role;
            Disposition = disposition;
            ByteCount = byteCount;
            Sha256 = sha256;
        }

        public AuthorityArtifactRole Role { get; }
        public AuthorityArtifactDisposition Disposition { get; }
        public long ByteCount { get; }
        public string Sha256 { get; }
    }

    public sealed class VerifiedGenerationFingerprintFrame
    {
        public VerifiedGenerationFingerprintFrame(
            string profileId,
            string saveFormatId,
            int saveSchemaVersion,
            int profileInitializationVersion,
            ProfileAuthoritySourceGeneration selectedSourceGeneration,
            VerifiedAuthorityLedgerState durableLedgerState,
            SerializedAuthorityArtifactIdentity primary,
            SerializedAuthorityArtifactIdentity backup,
            SerializedAuthorityArtifactIdentity temp,
            SerializedAuthorityArtifactIdentity canonicalPrevious,
            SerializedAuthorityArtifactIdentity legacyPrevious,
            SerializedAuthorityArtifactIdentity recoveryWitness)
        {
            ProfileId = profileId;
            SaveFormatId = saveFormatId;
            SaveSchemaVersion = saveSchemaVersion;
            ProfileInitializationVersion = profileInitializationVersion;
            SelectedSourceGeneration = selectedSourceGeneration;
            DurableLedgerState = durableLedgerState;
            Primary = primary;
            Backup = backup;
            Temp = temp;
            CanonicalPrevious = canonicalPrevious;
            LegacyPrevious = legacyPrevious;
            RecoveryWitness = recoveryWitness;
        }

        public string ProfileId { get; }
        public string SaveFormatId { get; }
        public int SaveSchemaVersion { get; }
        public int ProfileInitializationVersion { get; }
        public ProfileAuthoritySourceGeneration SelectedSourceGeneration { get; }
        public VerifiedAuthorityLedgerState DurableLedgerState { get; }
        public SerializedAuthorityArtifactIdentity Primary { get; }
        public SerializedAuthorityArtifactIdentity Backup { get; }
        public SerializedAuthorityArtifactIdentity Temp { get; }
        public SerializedAuthorityArtifactIdentity CanonicalPrevious { get; }
        public SerializedAuthorityArtifactIdentity LegacyPrevious { get; }
        public SerializedAuthorityArtifactIdentity RecoveryWitness { get; }
    }

    public enum VerifiedGenerationFingerprintStatus
    {
        Unavailable = 0,
        Computed = 1,
        Invalid = 2
    }

    public sealed class VerifiedGenerationFingerprintResult
    {
        private readonly IReadOnlyList<string> _diagnosticCodes;

        internal VerifiedGenerationFingerprintResult(
            VerifiedGenerationFingerprintStatus status,
            string value,
            int canonicalFrameByteCount,
            IEnumerable<string> diagnosticCodes)
        {
            Status = status;
            Value = value ?? string.Empty;
            CanonicalFrameByteCount = canonicalFrameByteCount;
            _diagnosticCodes = new ReadOnlyCollection<string>(
                new List<string>(diagnosticCodes ?? Array.Empty<string>())
                    .ToArray());
        }

        public VerifiedGenerationFingerprintStatus Status { get; }
        public string Value { get; }
        public int CanonicalFrameByteCount { get; }
        public IReadOnlyList<string> DiagnosticCodes => _diagnosticCodes;
    }

    public static class VerifiedGenerationFingerprint
    {
        private const string DomainTag =
            "anotherlife.verified-generation-fingerprint";
        private const uint FrameSchema = 1;
        private const uint ArtifactCount = 6;

        public static VerifiedGenerationFingerprintResult Compute(
            VerifiedGenerationFingerprintFrame frame)
        {
            if (frame == null)
            {
                return Result(
                    VerifiedGenerationFingerprintStatus.Unavailable,
                    string.Empty,
                    0,
                    SaveAuthorityDiagnosticCodes.FingerprintFrameMissing);
            }

            string[] diagnostics = Validate(frame);
            if (diagnostics.Length > 0)
            {
                return new VerifiedGenerationFingerprintResult(
                    VerifiedGenerationFingerprintStatus.Invalid,
                    string.Empty,
                    0,
                    diagnostics);
            }

            try
            {
                byte[] bytes = Encode(frame);
                byte[] digest;
                using (SHA256 sha256 = SHA256.Create())
                {
                    digest = sha256.ComputeHash(bytes);
                }

                return new VerifiedGenerationFingerprintResult(
                    VerifiedGenerationFingerprintStatus.Computed,
                    LowerHex(digest),
                    bytes.Length,
                    Array.Empty<string>());
            }
            catch
            {
                return Result(
                    VerifiedGenerationFingerprintStatus.Unavailable,
                    string.Empty,
                    0,
                    SaveAuthorityDiagnosticCodes.FingerprintUnavailable);
            }
        }

        internal static string EncodeCanonicalFrameHexForTesting(
            VerifiedGenerationFingerprintFrame frame)
        {
            if (Validate(frame).Length != 0)
                return string.Empty;
            return LowerHex(Encode(frame));
        }

        private static string[] Validate(
            VerifiedGenerationFingerprintFrame frame)
        {
            var diagnostics = new SortedSet<string>(StringComparer.Ordinal);
            if (!SaveAuthorityValidation.IsCanonicalProfileId(frame.ProfileId) ||
                !string.Equals(
                    frame.SaveFormatId,
                    SaveAuthorityTechnicalLimits.SaveFormatId,
                    StringComparison.Ordinal) ||
                frame.SaveSchemaVersion <= 0 ||
                frame.ProfileInitializationVersion <= 0 ||
                !SaveAuthorityValidation.IsSourceCoherent(
                    true,
                    frame.SelectedSourceGeneration) ||
                !Enum.IsDefined(
                    typeof(VerifiedAuthorityLedgerState),
                    frame.DurableLedgerState))
            {
                diagnostics.Add(
                    SaveAuthorityDiagnosticCodes.FingerprintFields);
            }

            ValidateArtifact(
                frame.Primary,
                AuthorityArtifactRole.Primary,
                diagnostics);
            ValidateArtifact(
                frame.Backup,
                AuthorityArtifactRole.Backup,
                diagnostics);
            ValidateArtifact(
                frame.Temp,
                AuthorityArtifactRole.Temp,
                diagnostics);
            ValidateArtifact(
                frame.CanonicalPrevious,
                AuthorityArtifactRole.CanonicalPrevious,
                diagnostics);
            ValidateArtifact(
                frame.LegacyPrevious,
                AuthorityArtifactRole.LegacyPrevious,
                diagnostics);
            ValidateArtifact(
                frame.RecoveryWitness,
                AuthorityArtifactRole.RecoveryWitness,
                diagnostics);
            return new List<string>(diagnostics).ToArray();
        }

        private static void ValidateArtifact(
            SerializedAuthorityArtifactIdentity artifact,
            AuthorityArtifactRole requiredRole,
            ISet<string> diagnostics)
        {
            if (artifact == null ||
                artifact.Role != requiredRole ||
                !Enum.IsDefined(
                    typeof(AuthorityArtifactRole),
                    artifact.Role) ||
                !Enum.IsDefined(
                    typeof(AuthorityArtifactDisposition),
                    artifact.Disposition) ||
                artifact.ByteCount < 0)
            {
                diagnostics.Add(
                    SaveAuthorityDiagnosticCodes.FingerprintArtifact);
                return;
            }

            bool valid;
            switch (artifact.Disposition)
            {
                case AuthorityArtifactDisposition.Missing:
                    valid = artifact.ByteCount == 0 &&
                            string.Equals(
                                artifact.Sha256,
                                string.Empty,
                                StringComparison.Ordinal);
                    break;
                case AuthorityArtifactDisposition.VerifiedExact:
                    valid = SaveAuthorityValidation.IsCanonicalSha256(
                        artifact.Sha256);
                    break;
                default:
                    valid = false;
                    break;
            }

            if (!valid)
            {
                diagnostics.Add(
                    SaveAuthorityDiagnosticCodes.FingerprintArtifact);
            }
        }

        private static byte[] Encode(
            VerifiedGenerationFingerprintFrame frame)
        {
            using (var stream = new MemoryStream(512))
            {
                var writer = new CanonicalWriter(stream);
                writer.WriteString(DomainTag);
                writer.WriteUInt32(FrameSchema);
                writer.WriteUInt32(
                    SaveAuthorityTechnicalLimits
                        .AuthorityContractNumericVersion);
                writer.WriteString(frame.ProfileId);
                writer.WriteString(frame.SaveFormatId);
                writer.WriteInt32(frame.SaveSchemaVersion);
                writer.WriteInt32(frame.ProfileInitializationVersion);
                writer.WriteUInt32(
                    (uint)frame.SelectedSourceGeneration);
                writer.WriteUInt32((uint)frame.DurableLedgerState);
                writer.WriteUInt32(ArtifactCount);
                WriteArtifact(writer, frame.Primary);
                WriteArtifact(writer, frame.Backup);
                WriteArtifact(writer, frame.Temp);
                WriteArtifact(writer, frame.CanonicalPrevious);
                WriteArtifact(writer, frame.LegacyPrevious);
                WriteArtifact(writer, frame.RecoveryWitness);
                return stream.ToArray();
            }
        }

        private static void WriteArtifact(
            CanonicalWriter writer,
            SerializedAuthorityArtifactIdentity artifact)
        {
            writer.WriteUInt32((uint)artifact.Role);
            writer.WriteUInt32((uint)artifact.Disposition);
            writer.WriteUInt64((ulong)artifact.ByteCount);
            bool present =
                artifact.Disposition ==
                AuthorityArtifactDisposition.VerifiedExact;
            writer.WriteByte(present ? (byte)1 : (byte)0);
            if (present)
                writer.WriteBytes(DecodeHex(artifact.Sha256));
        }

        private static byte[] DecodeHex(string value)
        {
            var bytes = new byte[value.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = byte.Parse(
                    value.Substring(index * 2, 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static string LowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(
                    bytes[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static VerifiedGenerationFingerprintResult Result(
            VerifiedGenerationFingerprintStatus status,
            string value,
            int byteCount,
            string diagnostic) =>
            new VerifiedGenerationFingerprintResult(
                status,
                value,
                byteCount,
                new[] { diagnostic });

        private sealed class CanonicalWriter
        {
            private static readonly UTF8Encoding StrictUtf8 =
                new UTF8Encoding(false, true);
            private readonly Stream _stream;

            internal CanonicalWriter(Stream stream)
            {
                _stream = stream;
            }

            internal void WriteByte(byte value) => _stream.WriteByte(value);

            internal void WriteBytes(byte[] bytes) =>
                _stream.Write(bytes, 0, bytes.Length);

            internal void WriteString(string value)
            {
                byte[] bytes = StrictUtf8.GetBytes(value);
                WriteUInt32((uint)bytes.Length);
                WriteBytes(bytes);
            }

            internal void WriteInt32(int value) =>
                WriteUInt32(unchecked((uint)value));

            internal void WriteUInt32(uint value)
            {
                _stream.WriteByte((byte)(value >> 24));
                _stream.WriteByte((byte)(value >> 16));
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)value);
            }

            internal void WriteUInt64(ulong value)
            {
                _stream.WriteByte((byte)(value >> 56));
                _stream.WriteByte((byte)(value >> 48));
                _stream.WriteByte((byte)(value >> 40));
                _stream.WriteByte((byte)(value >> 32));
                _stream.WriteByte((byte)(value >> 24));
                _stream.WriteByte((byte)(value >> 16));
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)value);
            }
        }
    }

    public interface IAuthorityEpochCandidateSource
    {
        bool TryGetNextCandidate(out string candidate);
    }

    public enum AuthorityEpochAllocationStatus
    {
        Unavailable = 0,
        Allocated = 1
    }

    public sealed class AuthorityEpochAllocationResult
    {
        private readonly IReadOnlyList<string> _diagnosticCodes;

        internal AuthorityEpochAllocationResult(
            AuthorityEpochAllocationStatus status,
            string authorityEpoch,
            IEnumerable<string> diagnosticCodes)
        {
            Status = status;
            AuthorityEpoch = authorityEpoch ?? string.Empty;
            _diagnosticCodes = new ReadOnlyCollection<string>(
                new List<string>(diagnosticCodes ?? Array.Empty<string>())
                    .ToArray());
        }

        public AuthorityEpochAllocationStatus Status { get; }
        public string AuthorityEpoch { get; }
        public IReadOnlyList<string> DiagnosticCodes => _diagnosticCodes;
    }

    public sealed class AuthorityEpochAllocator
    {
        private static readonly AuthorityEpochAllocator Shared =
            new AuthorityEpochAllocator(
                new ProcessAuthorityEpochCandidateSource());

        private readonly object _gate = new object();
        private readonly IAuthorityEpochCandidateSource _source;
        private bool _allocating;
        private bool _hasAccepted;
        private ulong _processNonce;
        private ulong _lastCounter;

        public AuthorityEpochAllocator(
            IAuthorityEpochCandidateSource source)
        {
            _source = source;
        }

        public static AuthorityEpochAllocator ProcessLocal => Shared;

        public AuthorityEpochAllocationResult Allocate()
        {
            lock (_gate)
            {
                if (_allocating)
                {
                    return Unavailable(
                        SaveAuthorityDiagnosticCodes.EpochReentrant);
                }

                if (_source == null || _hasAccepted &&
                    _lastCounter == ulong.MaxValue)
                {
                    return Unavailable(
                        SaveAuthorityDiagnosticCodes.EpochSourceUnavailable);
                }

                _allocating = true;
                try
                {
                    for (int attempt = 0;
                         attempt <
                         SaveAuthorityTechnicalLimits
                             .MaximumEpochAllocationAttempts;
                         attempt++)
                    {
                        string candidate;
                        bool available;
                        try
                        {
                            available = _source.TryGetNextCandidate(
                                out candidate);
                        }
                        catch
                        {
                            return Unavailable(
                                SaveAuthorityDiagnosticCodes
                                    .EpochSourceUnavailable);
                        }

                        if (!available)
                        {
                            return Unavailable(
                                SaveAuthorityDiagnosticCodes
                                    .EpochSourceUnavailable);
                        }

                        if (!TryParse(
                                candidate,
                                out ulong nonce,
                                out ulong counter))
                        {
                            continue;
                        }

                        if (_hasAccepted &&
                            (nonce != _processNonce ||
                             counter <= _lastCounter))
                        {
                            continue;
                        }

                        _hasAccepted = true;
                        _processNonce = nonce;
                        _lastCounter = counter;
                        return new AuthorityEpochAllocationResult(
                            AuthorityEpochAllocationStatus.Allocated,
                            candidate,
                            Array.Empty<string>());
                    }

                    return Unavailable(
                        SaveAuthorityDiagnosticCodes
                            .EpochCandidateExhausted);
                }
                finally
                {
                    _allocating = false;
                }
            }
        }

        public static bool IsCanonical(string value) =>
            TryParse(value, out _, out _);

        internal static bool IsStrictSuccessor(
            string current,
            string candidate) =>
            TryParse(current, out ulong currentNonce, out ulong currentCounter) &&
            TryParse(candidate, out ulong candidateNonce, out ulong candidateCounter) &&
            currentNonce == candidateNonce &&
            candidateCounter > currentCounter;

        private static bool TryParse(
            string value,
            out ulong nonce,
            out ulong counter)
        {
            nonce = 0;
            counter = 0;
            if (value == null ||
                value.Length !=
                SaveAuthorityTechnicalLimits.AuthorityEpochCharacters)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (!SaveAuthorityValidation.IsLowerHex(value[index]))
                    return false;
            }

            if (!ulong.TryParse(
                    value.Substring(0, 16),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out nonce) ||
                !ulong.TryParse(
                    value.Substring(16, 16),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out counter))
            {
                return false;
            }

            return nonce != 0 && counter != 0;
        }

        private static AuthorityEpochAllocationResult Unavailable(
            string diagnostic) =>
            new AuthorityEpochAllocationResult(
                AuthorityEpochAllocationStatus.Unavailable,
                string.Empty,
                new[] { diagnostic });
    }

    internal sealed class ProcessAuthorityEpochCandidateSource :
        IAuthorityEpochCandidateSource
    {
        private readonly object _gate = new object();
        private readonly ulong _processNonce;
        private readonly bool _available;
        private ulong _counter;

        internal ProcessAuthorityEpochCandidateSource()
        {
            try
            {
                using (RandomNumberGenerator random =
                       RandomNumberGenerator.Create())
                {
                    var bytes = new byte[8];
                    for (int attempt = 0;
                         attempt <
                         SaveAuthorityTechnicalLimits
                             .MaximumEpochAllocationAttempts;
                         attempt++)
                    {
                        random.GetBytes(bytes);
                        ulong nonce = ReadUInt64BigEndian(bytes);
                        if (nonce == 0)
                            continue;
                        _processNonce = nonce;
                        _available = true;
                        return;
                    }
                }
            }
            catch
            {
                _available = false;
            }
        }

        public bool TryGetNextCandidate(out string candidate)
        {
            lock (_gate)
            {
                if (!_available || _counter == ulong.MaxValue)
                {
                    candidate = string.Empty;
                    return false;
                }

                _counter++;
                candidate =
                    _processNonce.ToString(
                        "x16",
                        CultureInfo.InvariantCulture) +
                    _counter.ToString(
                        "x16",
                        CultureInfo.InvariantCulture);
                return true;
            }
        }

        private static ulong ReadUInt64BigEndian(byte[] bytes) =>
            (ulong)bytes[0] << 56 |
            (ulong)bytes[1] << 48 |
            (ulong)bytes[2] << 40 |
            (ulong)bytes[3] << 32 |
            (ulong)bytes[4] << 24 |
            (ulong)bytes[5] << 16 |
            (ulong)bytes[6] << 8 |
            bytes[7];
    }
}
