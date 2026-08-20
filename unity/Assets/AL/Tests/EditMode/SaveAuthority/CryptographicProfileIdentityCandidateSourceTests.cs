using System;
using System.Collections.Generic;
using AL.Core.SaveAuthority;
using NUnit.Framework;

namespace AL.Tests.EditMode.SaveAuthority
{
    public sealed class CryptographicProfileIdentityCandidateSourceTests
    {
        [Test]
        public void ProducesCanonicalUniqueNonZeroIdentities()
        {
            var source = new CryptographicProfileIdentityCandidateSource();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < 500; index++)
            {
                string candidate = source.GetCandidate(index + 1);

                Assert.IsTrue(
                    SaveAuthorityValidation.IsCanonicalProfileId(candidate),
                    $"Expected a canonical profile identity, got '{candidate}'.");
                Assert.IsTrue(
                    seen.Add(candidate),
                    "Expected a fresh, distinct identity on every call.");
            }
        }

        [Test]
        public void IgnoresAttemptNumberAndAlwaysReturnsACandidate()
        {
            var source = new CryptographicProfileIdentityCandidateSource();

            string first = source.GetCandidate(1);
            string eighth = source.GetCandidate(8);

            Assert.IsTrue(
                SaveAuthorityValidation.IsCanonicalProfileId(first));
            Assert.IsTrue(
                SaveAuthorityValidation.IsCanonicalProfileId(eighth));
            Assert.AreNotEqual(first, eighth);
        }
    }
}
