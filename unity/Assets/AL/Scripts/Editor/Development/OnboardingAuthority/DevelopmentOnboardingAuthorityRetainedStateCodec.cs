#if !UNITY_EDITOR
#error DEVELOPMENT_EMULATOR_V1 is editor-only and must never compile into a Player.
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Editor.Development.OnboardingAuthority
{
    internal enum DevelopmentRetainedStoreKind : byte
    {
        Authority = 1,
        Projection = 2
    }

    internal static class DevelopmentCanonicalDigest
    {
        internal const string ReceiptIdDomain = "AL.DEV.ONBOARDING.RECEIPT.ID.v1";
        internal const string ReceiptBodyDomain = "AL.DEV.ONBOARDING.RECEIPT.BODY.v1";
        internal const string ProjectionIdDomain = "AL.DEV.ONBOARDING.PROJECTION.ID.v1";
        internal const string ProjectionMarkerDomain = "AL.DEV.ONBOARDING.PROJECTION.MARKER.v1";
        internal const string FixtureDomain = "AL.DEV.ONBOARDING.AVAILABILITY.FIXTURES.v1";
        internal const string AuthorityPayloadDomain = "AL.DEV.ONBOARDING.AUTHORITY.RETAINED.v1";
        internal const string AuthorityRecordDomain = "AL.DEV.ONBOARDING.AUTHORITY.RECORD.v1";
        internal const string ProjectionPayloadDomain = "AL.DEV.ONBOARDING.PROJECTION.RETAINED.v1";
        internal const string ProjectionRecordDomain = "AL.DEV.ONBOARDING.PROJECTION.RECORD.v1";
        internal const string EnvelopeDomain = "AL.DEV.ONBOARDING.RETAINED.ENVELOPE.v1";
        internal const string EnvelopeDigestDomain = "AL.DEV.ONBOARDING.RETAINED.ENVELOPE.DIGEST.v1";

        internal static Digest32 ComputeAvailabilityFixtureDigest(
            IReadOnlyList<Commitment32> taken,
            IReadOnlyList<Commitment32> unavailable)
        {
            var fields = new List<byte[]>
            {
                DevelopmentFrameV1.UInt64Bytes((ulong)taken.Count)
            };

            for (var index = 0; index < taken.Count; index++)
            {
                fields.Add(taken[index].ToArray());
            }

            fields.Add(DevelopmentFrameV1.UInt64Bytes((ulong)unavailable.Count));
            for (var index = 0; index < unavailable.Count; index++)
            {
                fields.Add(unavailable[index].ToArray());
            }

            return Hash(DevelopmentFrameV1.Encode(FixtureDomain, fields));
        }

        internal static string ComputeReceiptId(
            string instanceId,
            DevelopmentOnboardingCommitRequest request)
        {
            var frame = DevelopmentFrameV1.Encode(
                ReceiptIdDomain,
                DevelopmentFrameV1.Utf8(DevelopmentOnboardingAuthorityContracts.ContractVersion),
                DevelopmentFrameV1.Utf8(instanceId),
                request.AuthorityScopeCommitment.ToArray(),
                request.OperationCommitment.ToArray(),
                request.SemanticRequestFingerprint.ToArray(),
                request.OpaqueCompiledCoreDigest.ToArray(),
                request.NormalizedHandleCommitment.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(request.ExpectedGeneration));
            return "devrcpt_" + Hash(frame).ToHex();
        }

        internal static Digest32 ComputeReceiptBodyDigest(
            string instanceId,
            string receiptId,
            DevelopmentOnboardingCommitRequest request,
            ulong committedGeneration,
            ulong authorityRevision)
        {
            return Hash(DevelopmentFrameV1.Encode(
                ReceiptBodyDomain,
                DevelopmentFrameV1.Utf8(DevelopmentOnboardingAuthorityContracts.ContractVersion),
                DevelopmentFrameV1.Utf8(instanceId),
                DevelopmentFrameV1.Utf8(receiptId),
                request.AuthorityScopeCommitment.ToArray(),
                request.OperationCommitment.ToArray(),
                request.SemanticRequestFingerprint.ToArray(),
                request.OpaqueCompiledCoreDigest.ToArray(),
                request.NormalizedHandleCommitment.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(request.ExpectedGeneration),
                DevelopmentFrameV1.UInt64Bytes(committedGeneration),
                DevelopmentFrameV1.UInt64Bytes(authorityRevision)));
        }

        internal static string ComputeProjectionMarkerId(
            string projectionInstanceId,
            Commitment32 localProfileScopeCommitment,
            DevelopmentReceiptHandle receiptHandle,
            ulong expectedLocalRevision)
        {
            var frame = DevelopmentFrameV1.Encode(
                ProjectionIdDomain,
                DevelopmentFrameV1.Utf8(DevelopmentOnboardingAuthorityContracts.ContractVersion),
                DevelopmentFrameV1.Utf8(projectionInstanceId),
                localProfileScopeCommitment.ToArray(),
                DevelopmentFrameV1.Utf8(receiptHandle.ReceiptId),
                receiptHandle.BodyDigest.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(expectedLocalRevision));
            return "devmarker_" + Hash(frame).ToHex();
        }

        internal static Digest32 ComputeProjectionMarkerDigest(
            string projectionInstanceId,
            string markerId,
            Commitment32 localProfileScopeCommitment,
            DevelopmentReceiptHandle receiptHandle,
            ulong expectedLocalRevision,
            ulong resultingLocalRevision,
            ulong markerRevision)
        {
            return Hash(DevelopmentFrameV1.Encode(
                ProjectionMarkerDomain,
                DevelopmentFrameV1.Utf8(DevelopmentOnboardingAuthorityContracts.ContractVersion),
                DevelopmentFrameV1.Utf8(projectionInstanceId),
                DevelopmentFrameV1.Utf8(markerId),
                localProfileScopeCommitment.ToArray(),
                DevelopmentFrameV1.Utf8(receiptHandle.AuthorityInstanceId),
                DevelopmentFrameV1.Utf8(receiptHandle.ReceiptId),
                receiptHandle.BodyDigest.ToArray(),
                DevelopmentFrameV1.UInt64Bytes(expectedLocalRevision),
                DevelopmentFrameV1.UInt64Bytes(resultingLocalRevision),
                DevelopmentFrameV1.UInt64Bytes(markerRevision)));
        }

        internal static Digest32 Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return new Digest32(algorithm.ComputeHash(bytes));
            }
        }
    }

    internal static class DevelopmentFrameV1
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static byte[] Encode(string domain, params byte[][] fields)
        {
            return Encode(domain, (IReadOnlyList<byte[]>)fields);
        }

        internal static byte[] Encode(string domain, IReadOnlyList<byte[]> fields)
        {
            if (!IsAsciiDomain(domain))
            {
                throw new ArgumentException("FrameV1 domain must be strict printable ASCII.", nameof(domain));
            }

            if (fields == null || fields.Count > ushort.MaxValue)
            {
                throw new ArgumentException("FrameV1 field count is invalid.", nameof(fields));
            }

            using (var stream = new MemoryStream())
            {
                var domainBytes = Encoding.ASCII.GetBytes(domain);
                stream.Write(domainBytes, 0, domainBytes.Length);
                stream.WriteByte(0);
                for (var index = 0; index < fields.Count; index++)
                {
                    WriteUInt16(stream, checked((ushort)(index + 1)));
                    var field = fields[index];
                    if (field == null)
                    {
                        WriteUInt32(stream, uint.MaxValue);
                        continue;
                    }

                    WriteUInt32(stream, checked((uint)field.Length));
                    stream.Write(field, 0, field.Length);
                }

                return stream.ToArray();
            }
        }

        internal static bool TryDecode(
            byte[] frame,
            string expectedDomain,
            int exactFieldCount,
            out byte[][] fields)
        {
            fields = null;
            if (frame == null || frame.Length == 0 || !IsAsciiDomain(expectedDomain) || exactFieldCount < 0)
            {
                return false;
            }

            var domainBytes = Encoding.ASCII.GetBytes(expectedDomain);
            if (frame.Length < domainBytes.Length + 1)
            {
                return false;
            }

            for (var index = 0; index < domainBytes.Length; index++)
            {
                if (frame[index] != domainBytes[index])
                {
                    return false;
                }
            }

            if (frame[domainBytes.Length] != 0)
            {
                return false;
            }

            var offset = domainBytes.Length + 1;
            var decoded = new byte[exactFieldCount][];
            for (var fieldIndex = 0; fieldIndex < exactFieldCount; fieldIndex++)
            {
                if (!TryReadUInt16(frame, ref offset, out var ordinal) || ordinal != fieldIndex + 1 ||
                    !TryReadUInt32(frame, ref offset, out var length))
                {
                    return false;
                }

                if (length == uint.MaxValue)
                {
                    decoded[fieldIndex] = null;
                    continue;
                }

                if (length > int.MaxValue || offset > frame.Length - (int)length)
                {
                    return false;
                }

                var value = new byte[(int)length];
                Buffer.BlockCopy(frame, offset, value, 0, value.Length);
                offset += value.Length;
                decoded[fieldIndex] = value;
            }

            if (offset != frame.Length)
            {
                return false;
            }

            fields = decoded;
            return true;
        }

        internal static bool TryDecodeDynamic(
            byte[] frame,
            string expectedDomain,
            int maximumFieldCount,
            out byte[][] fields)
        {
            fields = null;
            if (frame == null || frame.Length == 0 || !IsAsciiDomain(expectedDomain) || maximumFieldCount < 0)
            {
                return false;
            }

            var domainBytes = Encoding.ASCII.GetBytes(expectedDomain);
            if (frame.Length < domainBytes.Length + 1)
            {
                return false;
            }

            for (var index = 0; index < domainBytes.Length; index++)
            {
                if (frame[index] != domainBytes[index])
                {
                    return false;
                }
            }

            if (frame[domainBytes.Length] != 0)
            {
                return false;
            }

            var offset = domainBytes.Length + 1;
            var decoded = new List<byte[]>();
            while (offset < frame.Length)
            {
                if (decoded.Count >= maximumFieldCount ||
                    !TryReadUInt16(frame, ref offset, out var ordinal) || ordinal != decoded.Count + 1 ||
                    !TryReadUInt32(frame, ref offset, out var length))
                {
                    return false;
                }

                if (length == uint.MaxValue)
                {
                    decoded.Add(null);
                    continue;
                }

                if (length > int.MaxValue || offset > frame.Length - (int)length)
                {
                    return false;
                }

                var value = new byte[(int)length];
                Buffer.BlockCopy(frame, offset, value, 0, value.Length);
                offset += value.Length;
                decoded.Add(value);
            }

            fields = decoded.ToArray();
            return true;
        }

        internal static byte[] Utf8(string value)
        {
            if (value == null)
            {
                return null;
            }

            return StrictUtf8.GetBytes(value);
        }

        internal static bool TryUtf8(byte[] bytes, out string value)
        {
            value = null;
            if (bytes == null)
            {
                return false;
            }

            try
            {
                value = StrictUtf8.GetString(bytes);
                return FixedBytesEncoding.Equals(bytes, StrictUtf8.GetBytes(value));
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        internal static byte[] UInt64Bytes(ulong value)
        {
            return new[]
            {
                (byte)(value >> 56),
                (byte)(value >> 48),
                (byte)(value >> 40),
                (byte)(value >> 32),
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            };
        }

        internal static bool TryUInt64(byte[] bytes, out ulong value)
        {
            value = 0;
            if (bytes == null || bytes.Length != 8)
            {
                return false;
            }

            for (var index = 0; index < bytes.Length; index++)
            {
                value = (value << 8) | bytes[index];
            }

            return true;
        }

        internal static void WriteUInt16(Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        internal static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        internal static bool TryReadUInt16(byte[] bytes, ref int offset, out ushort value)
        {
            value = 0;
            if (bytes == null || offset < 0 || offset > bytes.Length - 2)
            {
                return false;
            }

            value = (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
            offset += 2;
            return true;
        }

        internal static bool TryReadUInt32(byte[] bytes, ref int offset, out uint value)
        {
            value = 0;
            if (bytes == null || offset < 0 || offset > bytes.Length - 4)
            {
                return false;
            }

            value = ((uint)bytes[offset] << 24) |
                    ((uint)bytes[offset + 1] << 16) |
                    ((uint)bytes[offset + 2] << 8) |
                    bytes[offset + 3];
            offset += 4;
            return true;
        }

        private static bool IsAsciiDomain(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] < 0x21 || value[index] > 0x7e)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static class DevelopmentOnboardingAuthorityRetainedStateCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ALDOEV1\0");

        internal static byte[] EncodeEnvelope(
            DevelopmentRetainedStoreKind kind,
            string instanceId,
            Digest32? fixtureDigest,
            byte[] payload)
        {
            DevelopmentInstanceId.Require(instanceId, nameof(instanceId));
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var payloadDigest = DevelopmentCanonicalDigest.Hash(payload);
            var body = DevelopmentFrameV1.Encode(
                DevelopmentCanonicalDigest.EnvelopeDomain,
                DevelopmentFrameV1.UInt64Bytes((ulong)kind),
                DevelopmentFrameV1.Utf8(DevelopmentOnboardingAuthorityContracts.ContractVersion),
                DevelopmentFrameV1.Utf8(instanceId),
                fixtureDigest.HasValue ? fixtureDigest.Value.ToArray() : null,
                payloadDigest.ToArray(),
                payload);
            var envelopeDigest = DevelopmentCanonicalDigest.Hash(
                DevelopmentFrameV1.Encode(DevelopmentCanonicalDigest.EnvelopeDigestDomain, body));

            using (var stream = new MemoryStream())
            {
                stream.Write(Magic, 0, Magic.Length);
                DevelopmentFrameV1.WriteUInt32(stream, checked((uint)body.Length));
                stream.Write(body, 0, body.Length);
                var digestBytes = envelopeDigest.ToArray();
                stream.Write(digestBytes, 0, digestBytes.Length);
                var result = stream.ToArray();
                if (result.Length > DevelopmentOnboardingAuthorityContracts.MaxRetainedEnvelopeBytes)
                {
                    throw new InvalidOperationException("Retained envelope exceeds the fixed development bound.");
                }

                return result;
            }
        }

        internal static bool TryDecodeEnvelope(
            byte[] bytes,
            DevelopmentRetainedStoreKind expectedKind,
            string expectedInstanceId,
            Digest32? expectedFixtureDigest,
            out byte[] payload,
            out DevelopmentRetainedStateFailure failure)
        {
            payload = null;
            failure = DevelopmentRetainedStateFailure.None;
            if (bytes == null || bytes.Length == 0)
            {
                failure = DevelopmentRetainedStateFailure.NullOrEmpty;
                return false;
            }

            if (bytes.Length > DevelopmentOnboardingAuthorityContracts.MaxRetainedEnvelopeBytes)
            {
                failure = DevelopmentRetainedStateFailure.Oversized;
                return false;
            }

            if (!DevelopmentInstanceId.IsValid(expectedInstanceId))
            {
                failure = DevelopmentRetainedStateFailure.InstanceMismatch;
                return false;
            }

            var minimumLength = Magic.Length + 4 + 1 + DevelopmentOnboardingAuthorityContracts.FixedBytesLength;
            if (bytes.Length < minimumLength)
            {
                failure = DevelopmentRetainedStateFailure.InvalidFrame;
                return false;
            }

            for (var index = 0; index < Magic.Length; index++)
            {
                if (bytes[index] != Magic[index])
                {
                    failure = DevelopmentRetainedStateFailure.InvalidFrame;
                    return false;
                }
            }

            var offset = Magic.Length;
            if (!DevelopmentFrameV1.TryReadUInt32(bytes, ref offset, out var bodyLength) ||
                bodyLength > int.MaxValue ||
                bodyLength > bytes.Length - offset - DevelopmentOnboardingAuthorityContracts.FixedBytesLength)
            {
                failure = DevelopmentRetainedStateFailure.InvalidFrame;
                return false;
            }

            if (offset + (int)bodyLength + DevelopmentOnboardingAuthorityContracts.FixedBytesLength != bytes.Length)
            {
                failure = DevelopmentRetainedStateFailure.TrailingBytes;
                return false;
            }

            var body = new byte[(int)bodyLength];
            Buffer.BlockCopy(bytes, offset, body, 0, body.Length);
            offset += body.Length;
            var storedEnvelopeDigest = new byte[DevelopmentOnboardingAuthorityContracts.FixedBytesLength];
            Buffer.BlockCopy(bytes, offset, storedEnvelopeDigest, 0, storedEnvelopeDigest.Length);
            var computedEnvelopeDigest = DevelopmentCanonicalDigest.Hash(
                DevelopmentFrameV1.Encode(DevelopmentCanonicalDigest.EnvelopeDigestDomain, body));
            if (computedEnvelopeDigest != new Digest32(storedEnvelopeDigest))
            {
                failure = DevelopmentRetainedStateFailure.DigestMismatch;
                return false;
            }

            if (!DevelopmentFrameV1.TryDecode(
                    body,
                    DevelopmentCanonicalDigest.EnvelopeDomain,
                    6,
                    out var fields) ||
                !DevelopmentFrameV1.TryUInt64(fields[0], out var kindValue) ||
                kindValue != (ulong)expectedKind)
            {
                failure = DevelopmentRetainedStateFailure.WrongKind;
                return false;
            }

            if (!DevelopmentFrameV1.TryUtf8(fields[1], out var contractVersion) ||
                !string.Equals(contractVersion, DevelopmentOnboardingAuthorityContracts.ContractVersion, StringComparison.Ordinal))
            {
                failure = DevelopmentRetainedStateFailure.WrongContract;
                return false;
            }

            if (!DevelopmentFrameV1.TryUtf8(fields[2], out var instanceId) ||
                !string.Equals(instanceId, expectedInstanceId, StringComparison.Ordinal))
            {
                failure = DevelopmentRetainedStateFailure.InstanceMismatch;
                return false;
            }

            if (expectedFixtureDigest.HasValue)
            {
                if (fields[3] == null || fields[3].Length != DevelopmentOnboardingAuthorityContracts.FixedBytesLength ||
                    expectedFixtureDigest.Value != new Digest32(fields[3]))
                {
                    failure = DevelopmentRetainedStateFailure.FixtureMismatch;
                    return false;
                }
            }
            else if (fields[3] != null)
            {
                failure = DevelopmentRetainedStateFailure.FixtureMismatch;
                return false;
            }

            if (fields[4] == null || fields[4].Length != DevelopmentOnboardingAuthorityContracts.FixedBytesLength ||
                fields[5] == null || DevelopmentCanonicalDigest.Hash(fields[5]) != new Digest32(fields[4]))
            {
                failure = DevelopmentRetainedStateFailure.DigestMismatch;
                return false;
            }

            payload = (byte[])fields[5].Clone();
            return true;
        }
    }
}
