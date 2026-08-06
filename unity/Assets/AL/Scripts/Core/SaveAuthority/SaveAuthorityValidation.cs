using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AL.Core.SaveAuthority
{
    public static class ProfileWriteAuthorityProviderGuard
    {
        /// <summary>
        /// Reads the provider once and validates the complete Writable shape
        /// without allocating a normalized snapshot or diagnostic collection.
        /// Consumers must call this at each mutation boundary rather than
        /// caching the result.
        /// </summary>
        public static bool IsCurrentWritable(
            IProfileWriteAuthorityProvider provider)
        {
            if (provider == null)
                return false;

            try
            {
                ProfileWriteAuthoritySnapshot snapshot =
                    provider.GetCurrentAuthority();
                return snapshot != null &&
                       string.Equals(
                           snapshot.ContractVersion,
                           SaveAuthorityTechnicalLimits.ContractVersion,
                           StringComparison.Ordinal) &&
                       snapshot.Status ==
                           ProfileWriteAuthorityStatus.Writable &&
                       snapshot.HasSelectedSourceGeneration &&
                       IsWritableSourceWithoutAllocation(
                           snapshot.SelectedSourceGeneration) &&
                       SaveAuthorityValidation.IsCanonicalProfileId(
                           snapshot.ProfileId) &&
                       HasCanonicalEpochWithoutAllocation(
                           snapshot.AuthorityEpoch) &&
                       SaveAuthorityValidation.IsCanonicalSha256(
                           snapshot.VerifiedGenerationFingerprint) &&
                       snapshot.SaveSchemaVersion ==
                           SaveAuthorityTechnicalLimits
                               .IdentityAwareSaveSchemaVersion &&
                       snapshot.ProfileInitializationVersion ==
                           SaveAuthorityTechnicalLimits
                               .IdentityAwareProfileInitializationVersion &&
                       HasValidDiagnosticsWithoutAllocation(
                           snapshot.DiagnosticCodes);
            }
            catch
            {
                return false;
            }
        }

        public static ProfileWriteAuthoritySnapshot ReadOrUnavailable(
            IProfileWriteAuthorityProvider provider)
        {
            if (provider == null)
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderMissing);
            }

            ProfileWriteAuthoritySnapshot snapshot;
            try
            {
                snapshot = provider.GetCurrentAuthority();
            }
            catch
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderThrew);
            }

            if (snapshot == null)
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderNull);
            }

            return ValidateOrUnavailable(snapshot);
        }

        internal static ProfileWriteAuthoritySnapshot ValidateOrUnavailable(
            ProfileWriteAuthoritySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderNull);
            }

            if (!string.Equals(
                    snapshot.ContractVersion,
                    SaveAuthorityTechnicalLimits.ContractVersion,
                    StringComparison.Ordinal))
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderContract);
            }

            if (!Enum.IsDefined(
                    typeof(ProfileWriteAuthorityStatus),
                    snapshot.Status))
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderStatus);
            }

            if (!SaveAuthorityValidation.TryCanonicalizeDiagnostics(
                    snapshot.DiagnosticCodes,
                    out string[] diagnostics))
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderDiagnostics);
            }

            if (!SaveAuthorityValidation.IsSourceCoherent(
                    snapshot.HasSelectedSourceGeneration,
                    snapshot.SelectedSourceGeneration))
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderSource);
            }

            if (!HasValidStatusFields(snapshot, diagnostics.Length))
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderInvariants);
            }

            return new ProfileWriteAuthoritySnapshot(
                snapshot.ContractVersion,
                snapshot.Status,
                snapshot.ProfileId,
                snapshot.AuthorityEpoch,
                snapshot.VerifiedGenerationFingerprint,
                snapshot.SaveSchemaVersion,
                snapshot.ProfileInitializationVersion,
                snapshot.HasSelectedSourceGeneration,
                snapshot.SelectedSourceGeneration,
                diagnostics);
        }

        private static bool HasValidDiagnosticsWithoutAllocation(
            IReadOnlyList<string> diagnostics)
        {
            if (diagnostics == null ||
                diagnostics.Count >
                    SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes)
            {
                return false;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                string current = diagnostics[index];
                if (!SaveAuthorityValidation.IsDiagnosticCode(current))
                    return false;

                for (int prior = 0; prior < index; prior++)
                {
                    if (string.Equals(
                            diagnostics[prior],
                            current,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsWritableSourceWithoutAllocation(
            ProfileAuthoritySourceGeneration source) =>
            source == ProfileAuthoritySourceGeneration.Primary ||
            source == ProfileAuthoritySourceGeneration.Backup ||
            source == ProfileAuthoritySourceGeneration.Previous ||
            source == ProfileAuthoritySourceGeneration.Temp;

        private static bool HasCanonicalEpochWithoutAllocation(string value)
        {
            if (value == null ||
                value.Length !=
                    SaveAuthorityTechnicalLimits.AuthorityEpochCharacters)
            {
                return false;
            }

            bool nonceNonZero = false;
            bool counterNonZero = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!SaveAuthorityValidation.IsLowerHex(character))
                    return false;

                if (character == '0')
                    continue;

                if (index < 16)
                    nonceNonZero = true;
                else
                    counterNonZero = true;
            }

            return nonceNonZero && counterNonZero;
        }

        private static bool HasValidStatusFields(
            ProfileWriteAuthoritySnapshot snapshot,
            int diagnosticCount)
        {
            bool emptyAuthority =
                string.Equals(snapshot.ProfileId, string.Empty,
                    StringComparison.Ordinal) &&
                string.Equals(snapshot.AuthorityEpoch, string.Empty,
                    StringComparison.Ordinal) &&
                string.Equals(
                    snapshot.VerifiedGenerationFingerprint,
                    string.Empty,
                    StringComparison.Ordinal);

            if (snapshot.Status != ProfileWriteAuthorityStatus.Writable &&
                diagnosticCount == 0)
            {
                return false;
            }

            switch (snapshot.Status)
            {
                case ProfileWriteAuthorityStatus.Writable:
                    return snapshot.HasSelectedSourceGeneration &&
                           SaveAuthorityValidation.IsCanonicalProfileId(
                               snapshot.ProfileId) &&
                           AuthorityEpochAllocator.IsCanonical(
                               snapshot.AuthorityEpoch) &&
                           SaveAuthorityValidation.IsCanonicalSha256(
                               snapshot.VerifiedGenerationFingerprint) &&
                           snapshot.SaveSchemaVersion ==
                           SaveAuthorityTechnicalLimits
                               .IdentityAwareSaveSchemaVersion &&
                           snapshot.ProfileInitializationVersion ==
                           SaveAuthorityTechnicalLimits
                               .IdentityAwareProfileInitializationVersion;

                case ProfileWriteAuthorityStatus.MissingProfile:
                case ProfileWriteAuthorityStatus.Deleted:
                case ProfileWriteAuthorityStatus.Unavailable:
                    return emptyAuthority &&
                           snapshot.SaveSchemaVersion == 0 &&
                           snapshot.ProfileInitializationVersion == 0 &&
                           !snapshot.HasSelectedSourceGeneration;

                case ProfileWriteAuthorityStatus.MigrationRequired:
                    return emptyAuthority &&
                           snapshot.SaveSchemaVersion ==
                           SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
                           snapshot.ProfileInitializationVersion ==
                           SaveAuthorityTechnicalLimits
                               .LegacyProfileInitializationVersion &&
                           snapshot.HasSelectedSourceGeneration;

                case ProfileWriteAuthorityStatus.ForwardSchemaReadOnly:
                    return emptyAuthority &&
                           snapshot.SaveSchemaVersion > 0 &&
                           snapshot.ProfileInitializationVersion > 0 &&
                           snapshot.HasSelectedSourceGeneration &&
                           (snapshot.SaveSchemaVersion >
                            SaveAuthorityTechnicalLimits
                                .IdentityAwareSaveSchemaVersion ||
                            snapshot.SaveSchemaVersion ==
                            SaveAuthorityTechnicalLimits
                                .IdentityAwareSaveSchemaVersion &&
                            snapshot.ProfileInitializationVersion >
                            SaveAuthorityTechnicalLimits
                                .IdentityAwareProfileInitializationVersion);

                case ProfileWriteAuthorityStatus.DegradedReadOnly:
                    return emptyAuthority &&
                           snapshot.SaveSchemaVersion > 0 &&
                           snapshot.ProfileInitializationVersion > 0 &&
                           snapshot.HasSelectedSourceGeneration;

                case ProfileWriteAuthorityStatus.RecoveryRequired:
                case ProfileWriteAuthorityStatus.CommitUncertain:
                    return emptyAuthority &&
                           snapshot.SaveSchemaVersion >= 0 &&
                           snapshot.ProfileInitializationVersion >= 0 &&
                           !snapshot.HasSelectedSourceGeneration;

                default:
                    return false;
            }
        }
    }

    internal static class SaveAuthorityValidation
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        internal static bool IsCanonicalProfileId(string value)
        {
            if (value == null ||
                value.Length != SaveAuthorityTechnicalLimits.ProfileIdCharacters ||
                !value.StartsWith("alp_", StringComparison.Ordinal))
            {
                return false;
            }

            bool anyNonZero = false;
            for (int index = 4; index < value.Length; index++)
            {
                char character = value[index];
                if (!IsLowerHex(character))
                    return false;
                anyNonZero |= character != '0';
            }

            return anyNonZero;
        }

        internal static bool IsCanonicalSha256(string value)
        {
            if (value == null ||
                value.Length != SaveAuthorityTechnicalLimits.Sha256Characters)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (!IsLowerHex(value[index]))
                    return false;
            }

            return true;
        }

        internal static bool IsLowerHex(char value) =>
            value >= '0' && value <= '9' ||
            value >= 'a' && value <= 'f';

        internal static bool IsDiagnosticCode(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length >
                SaveAuthorityTechnicalLimits.MaximumDiagnosticCodeCharacters)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid =
                    character >= 'A' && character <= 'Z' ||
                    character >= '0' && character <= '9' ||
                    character == '_' ||
                    character == '.' ||
                    character == '-';
                if (!valid)
                    return false;
            }

            return true;
        }

        internal static bool TryCanonicalizeDiagnostics(
            IEnumerable<string> values,
            out string[] canonical)
        {
            canonical = Array.Empty<string>();
            if (values == null)
                return false;

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var captured = new List<string>(
                SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes);
            int count = 0;
            foreach (string value in values)
            {
                count++;
                if (count >
                    SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes)
                {
                    return false;
                }

                if (!IsDiagnosticCode(value) || !unique.Add(value))
                    return false;
                captured.Add(value);
            }

            captured.Sort(StringComparer.Ordinal);
            canonical = captured.ToArray();
            return true;
        }

        internal static bool IsSourceCoherent(
            bool hasSource,
            ProfileAuthoritySourceGeneration source)
        {
            if (!Enum.IsDefined(
                    typeof(ProfileAuthoritySourceGeneration),
                    source))
            {
                return false;
            }

            return hasSource
                ? source != ProfileAuthoritySourceGeneration.None
                : source == ProfileAuthoritySourceGeneration.None;
        }

        internal static bool IsBoundedOpaqueIdentity(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length >
                SaveAuthorityTechnicalLimits.MaximumOpaqueIdentityUtf8Bytes)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                    return false;
            }

            try
            {
                int byteCount = StrictUtf8.GetByteCount(value);
                return byteCount > 0 &&
                       byteCount <=
                       SaveAuthorityTechnicalLimits
                           .MaximumOpaqueIdentityUtf8Bytes;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        internal static bool IsBoundedStrictUtf8(
            string value,
            int maximumBytes)
        {
            if (value == null)
                return false;
            try
            {
                return StrictUtf8.GetByteCount(value) <= maximumBytes;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }
    }
}
