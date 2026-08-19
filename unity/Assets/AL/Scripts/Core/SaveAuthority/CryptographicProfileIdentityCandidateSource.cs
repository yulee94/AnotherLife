using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.Core.SaveAuthority
{
    /// <summary>
    /// Production profile-identity candidate source. Each call yields a fresh
    /// canonical <c>alp_&lt;32 lowercase hexadecimal characters&gt;</c> identity whose
    /// 128-bit payload is drawn from a platform-supported cryptographically
    /// strong random source. The payload is guaranteed nonzero (all-zero draws
    /// are re-rolled within a bounded window), so the result always satisfies
    /// <see cref="SaveAuthorityValidation.IsCanonicalProfileId"/>.
    ///
    /// Collision retry against recognized evidence and retained session
    /// identities is the responsibility of
    /// <see cref="ProfileIdentityMigrationPlanner"/>, which invokes this source
    /// at most <see cref="ProfileIdentityMigrationTechnicalLimits.MaximumIdentityAttempts"/>
    /// times and fails closed on exhaustion. This source itself performs no
    /// filesystem, service, or publication work.
    /// </summary>
    public sealed class CryptographicProfileIdentityCandidateSource :
        IProfileIdentityCandidateSource
    {
        public string GetCandidate(int attemptNumber)
        {
            try
            {
                using (RandomNumberGenerator random =
                       RandomNumberGenerator.Create())
                {
                    var bytes = new byte[16];
                    for (int attempt = 0;
                         attempt <
                         ProfileIdentityMigrationTechnicalLimits
                             .MaximumIdentityAttempts;
                         attempt++)
                    {
                        random.GetBytes(bytes);
                        if (IsAllZero(bytes))
                            continue;

                        return "alp_" + LowerHex(bytes);
                    }
                }
            }
            catch
            {
                // A hostile or unavailable random source must fail closed rather
                // than fall back to a predictable identity. The planner maps this
                // exception to a typed identity-source failure.
                throw;
            }

            throw new InvalidOperationException(
                "Profile identity generation exhausted its bounded nonzero window.");
        }

        private static bool IsAllZero(byte[] bytes)
        {
            for (int index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] != 0)
                    return false;
            }

            return true;
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
    }
}
