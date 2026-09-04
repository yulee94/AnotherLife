using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AL.Runtime")]
[assembly: InternalsVisibleTo("AL.EditMode.Tests")]
[assembly: InternalsVisibleTo("AL.SaveAuthority.Tests")]
[assembly: InternalsVisibleTo("AL.Nvs01.Persistence.Tests")]

namespace AL.Core.SaveAuthority.RuntimeBridge
{
    /// <summary>
    /// Trusted runtime-only receipt mint for profile-bound commits. There is no
    /// public activation, proof, or Writable-to-ownership factory; AL.Runtime
    /// may request a receipt only after it has already persisted and verified a
    /// schema-2 ledger.
    /// </summary>
    public static class ProfileBoundReceiptSupport
    {
        internal static ProfileMutationReceipt Committed(
            ulong publicationSequence,
            string profileId,
            string expectedGenerationFingerprint,
            string committedGenerationFingerprint,
            string committedAuthorityEpoch,
            string operationId,
            string resultId,
            string committedPayloadFingerprint)
        {
            return new ProfileMutationReceipt(
                ProfileMutationReceiptStatus.Committed,
                publicationSequence,
                profileId ?? string.Empty,
                expectedGenerationFingerprint ?? string.Empty,
                committedGenerationFingerprint ?? string.Empty,
                committedAuthorityEpoch ?? string.Empty,
                operationId ?? string.Empty,
                resultId ?? string.Empty,
                committedPayloadFingerprint ?? string.Empty,
                true,
                Array.Empty<string>());
        }

        internal static ProfileMutationReceipt Uncertain(
            ulong publicationSequence,
            string profileId,
            string expectedGenerationFingerprint,
            string operationId,
            string resultId,
            string diagnostic)
        {
            return new ProfileMutationReceipt(
                ProfileMutationReceiptStatus.CommitUncertain,
                publicationSequence,
                profileId ?? string.Empty,
                expectedGenerationFingerprint ?? string.Empty,
                string.Empty,
                string.Empty,
                operationId ?? string.Empty,
                resultId ?? string.Empty,
                string.Empty,
                true,
                new[]
                {
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? SaveAuthorityDiagnosticCodes.CommitUncertain
                        : diagnostic
                });
        }
    }
}
