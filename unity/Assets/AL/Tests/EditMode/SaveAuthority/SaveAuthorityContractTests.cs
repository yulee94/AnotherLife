using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using AL.Core.SaveAuthority;

namespace AL.Tests.EditMode.SaveAuthority
{
    public sealed class SaveAuthorityContractTests
    {
        private const string ProfileId =
            "alp_0123456789abcdef0123456789abcdef";
        private const string Epoch =
            "0123456789abcdef0000000000000001";
        private const string Fingerprint =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Test]
        public void WritableFactoryPublishesExactImmutableAuthority()
        {
            string[] diagnostics =
            {
                "AL-SAVE-AUTH-ZETA",
                "AL-SAVE-AUTH-ALPHA"
            };

            ProfileWriteAuthoritySnapshot snapshot =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    ProfileId,
                    Epoch,
                    Fingerprint,
                    ProfileAuthoritySourceGeneration.Primary,
                    diagnostics);
            diagnostics[0] = "AL-SAVE-AUTH-MUTATED";

            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.ContractVersion,
                snapshot.ContractVersion);
            Assert.AreEqual(ProfileWriteAuthorityStatus.Writable, snapshot.Status);
            Assert.AreEqual(ProfileId, snapshot.ProfileId);
            Assert.AreEqual(Epoch, snapshot.AuthorityEpoch);
            Assert.AreEqual(
                Fingerprint,
                snapshot.VerifiedGenerationFingerprint);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                snapshot.SaveSchemaVersion);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.IdentityAwareProfileInitializationVersion,
                snapshot.ProfileInitializationVersion);
            Assert.IsTrue(snapshot.HasSelectedSourceGeneration);
            Assert.AreEqual(
                ProfileAuthoritySourceGeneration.Primary,
                snapshot.SelectedSourceGeneration);
            CollectionAssert.AreEqual(
                new[]
                {
                    "AL-SAVE-AUTH-ALPHA",
                    "AL-SAVE-AUTH-ZETA"
                },
                snapshot.DiagnosticCodes);
            Assert.IsNull(
                typeof(ProfileWriteAuthoritySnapshot).GetProperty("IsWritable"));
        }

        [Test]
        public void EveryNonWritableStatusRequiresAStableReason()
        {
            foreach (ProfileWriteAuthorityStatus status in Enum.GetValues(
                         typeof(ProfileWriteAuthorityStatus)))
            {
                if (status == ProfileWriteAuthorityStatus.Writable)
                    continue;

                ProfileWriteAuthoritySnapshot raw = ValidRawFor(status);
                ProfileWriteAuthoritySnapshot missingReason = Unchecked(
                    raw.ContractVersion,
                    raw.Status,
                    raw.ProfileId,
                    raw.AuthorityEpoch,
                    raw.VerifiedGenerationFingerprint,
                    raw.SaveSchemaVersion,
                    raw.ProfileInitializationVersion,
                    raw.HasSelectedSourceGeneration,
                    raw.SelectedSourceGeneration,
                    Array.Empty<string>());

                AssertUnavailable(
                    ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                        new FixedProvider(missingReason)),
                    SaveAuthorityDiagnosticCodes.ProviderInvariants);
            }
        }

        [Test]
        public void LegacySchemaCanOnlyRemainMigrationRequired()
        {
            ProfileWriteAuthoritySnapshot forgedWritable = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.Writable,
                ProfileId,
                Epoch,
                Fingerprint,
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                SaveAuthorityTechnicalLimits.LegacyProfileInitializationVersion,
                true,
                ProfileAuthoritySourceGeneration.Primary,
                Array.Empty<string>());

            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(forgedWritable)),
                SaveAuthorityDiagnosticCodes.ProviderInvariants);

            ProfileWriteAuthoritySnapshot migration =
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Backup,
                    new[] { "AL-SAVE-AUTH-MIGRATION-REQUIRED" });

            Assert.AreEqual(
                ProfileWriteAuthorityStatus.MigrationRequired,
                migration.Status);
            Assert.AreEqual(string.Empty, migration.ProfileId);
            Assert.AreEqual(string.Empty, migration.AuthorityEpoch);
            Assert.AreEqual(
                string.Empty,
                migration.VerifiedGenerationFingerprint);
            Assert.AreEqual(1, migration.SaveSchemaVersion);
            Assert.AreEqual(1, migration.ProfileInitializationVersion);
            Assert.AreEqual(
                ProfileAuthoritySourceGeneration.Backup,
                migration.SelectedSourceGeneration);
        }

        [TestCase(ProfileWriteAuthorityStatus.MissingProfile, 0, 0, false)]
        [TestCase(ProfileWriteAuthorityStatus.MigrationRequired, 1, 1, true)]
        [TestCase(ProfileWriteAuthorityStatus.ForwardSchemaReadOnly, 3, 1, true)]
        [TestCase(ProfileWriteAuthorityStatus.ForwardSchemaReadOnly, 2, 2, true)]
        [TestCase(ProfileWriteAuthorityStatus.DegradedReadOnly, 1, 1, true)]
        [TestCase(ProfileWriteAuthorityStatus.DegradedReadOnly, 2, 1, true)]
        [TestCase(ProfileWriteAuthorityStatus.RecoveryRequired, 2, 1, false)]
        [TestCase(ProfileWriteAuthorityStatus.CommitUncertain, 2, 1, false)]
        [TestCase(ProfileWriteAuthorityStatus.Deleted, 0, 0, false)]
        [TestCase(ProfileWriteAuthorityStatus.Unavailable, 0, 0, false)]
        public void ValidNonWritableMatrixSurvivesGuard(
            ProfileWriteAuthorityStatus status,
            int schema,
            int initialization,
            bool hasSource)
        {
            ProfileWriteAuthoritySnapshot raw = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                status,
                string.Empty,
                string.Empty,
                string.Empty,
                schema,
                initialization,
                hasSource,
                hasSource
                    ? ProfileAuthoritySourceGeneration.Primary
                    : ProfileAuthoritySourceGeneration.None,
                new[] { "AL-SAVE-AUTH-REASON" });

            ProfileWriteAuthoritySnapshot guarded =
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(raw));

            Assert.AreEqual(status, guarded.Status);
            Assert.AreEqual(schema, guarded.SaveSchemaVersion);
            Assert.AreEqual(initialization, guarded.ProfileInitializationVersion);
        }

        [TestCase(null, SaveAuthorityDiagnosticCodes.ProviderMissing)]
        public void MissingProviderFailsClosed(
            IProfileWriteAuthorityProvider provider,
            string expectedCode)
        {
            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(provider),
                expectedCode);
        }

        [Test]
        public void ThrowingAndNullProvidersFailClosedWithoutLeakingPriorFields()
        {
            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new ThrowingProvider()),
                SaveAuthorityDiagnosticCodes.ProviderThrew);
            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(null)),
                SaveAuthorityDiagnosticCodes.ProviderNull);
        }

        [Test]
        public void ProviderRejectionPrecedenceIsDeterministic()
        {
            ProfileWriteAuthoritySnapshot malformed = Unchecked(
                "wrong_contract",
                (ProfileWriteAuthorityStatus)999,
                "player-secret",
                "UPPERCASE",
                "not-a-fingerprint",
                -1,
                -1,
                true,
                (ProfileAuthoritySourceGeneration)999,
                new[]
                {
                    "bad diagnostic",
                    "bad diagnostic"
                });

            for (int index = 0; index < 8; index++)
            {
                AssertUnavailable(
                    ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                        new FixedProvider(malformed)),
                    SaveAuthorityDiagnosticCodes.ProviderContract);
            }
        }

        [Test]
        public void UnknownStatusAndSourceFailClosed()
        {
            ProfileWriteAuthoritySnapshot unknownStatus = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                (ProfileWriteAuthorityStatus)999,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                false,
                ProfileAuthoritySourceGeneration.None,
                new[] { "AL-SAVE-AUTH-UNKNOWN" });
            ProfileWriteAuthoritySnapshot unknownSource = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.MigrationRequired,
                string.Empty,
                string.Empty,
                string.Empty,
                1,
                1,
                true,
                (ProfileAuthoritySourceGeneration)999,
                new[] { "AL-SAVE-AUTH-UNKNOWN" });

            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(unknownStatus)),
                SaveAuthorityDiagnosticCodes.ProviderStatus);
            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(unknownSource)),
                SaveAuthorityDiagnosticCodes.ProviderSource);
        }

        [TestCase("alp_00000000000000000000000000000000")]
        [TestCase("ALP_0123456789abcdef0123456789abcdef")]
        [TestCase("alp_0123456789abcdef0123456789abcdeF")]
        [TestCase("alp_0123456789abcdef0123456789abcde")]
        [TestCase("alp_0123456789abcdef0123456789abcdef0")]
        [TestCase("alp_0123456789abcdef0123456789abcdeg")]
        [TestCase("alp_0123456789abcdef0123456789abcde한")]
        public void NonCanonicalProfileIdsCannotBecomeWritable(string profileId)
        {
            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(
                        Unchecked(
                            SaveAuthorityTechnicalLimits.ContractVersion,
                            ProfileWriteAuthorityStatus.Writable,
                            profileId,
                            Epoch,
                            Fingerprint,
                            2,
                            1,
                            true,
                            ProfileAuthoritySourceGeneration.Primary,
                            Array.Empty<string>()))),
                SaveAuthorityDiagnosticCodes.ProviderInvariants);
        }

        [TestCase("00000000000000000000000000000000")]
        [TestCase("0123456789ABCDEF0000000000000001")]
        [TestCase("0123456789abcdef000000000000001")]
        [TestCase("0123456789abcdef000000000000000g")]
        public void NonCanonicalEpochsCannotBecomeWritable(string epoch)
        {
            ProfileWriteAuthoritySnapshot raw = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.Writable,
                ProfileId,
                epoch,
                Fingerprint,
                2,
                1,
                true,
                ProfileAuthoritySourceGeneration.Primary,
                Array.Empty<string>());

            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(raw)),
                SaveAuthorityDiagnosticCodes.ProviderInvariants);
        }

        [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeF")]
        [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]
        [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")]
        public void NonCanonicalFingerprintsCannotBecomeWritable(string fingerprint)
        {
            ProfileWriteAuthoritySnapshot raw = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.Writable,
                ProfileId,
                Epoch,
                fingerprint,
                2,
                1,
                true,
                ProfileAuthoritySourceGeneration.Primary,
                Array.Empty<string>());

            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(raw)),
                SaveAuthorityDiagnosticCodes.ProviderInvariants);
        }

        [Test]
        public void DiagnosticsRejectInvalidDuplicateAndOverflowWithoutTruncation()
        {
            var overflow = Enumerable.Range(
                    0,
                    SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes + 1)
                .Select(index => $"AL-SAVE-AUTH-{index:D2}")
                .ToArray();

            AssertDiagnosticFailure(new[] { "lowercase" });
            AssertDiagnosticFailure(new[] { "AL-SAVE-AUTH-SPACE HERE" });
            AssertDiagnosticFailure(new[] { "AL-SAVE-AUTH-한" });
            AssertDiagnosticFailure(new[] { new string('A', 97) });
            AssertDiagnosticFailure(new[] { "AL-SAVE-AUTH-X", "AL-SAVE-AUTH-X" });
            AssertDiagnosticFailure(overflow);
        }

        [Test]
        public void MaximumDiagnosticsArePermutationIndependent()
        {
            string[] forward = Enumerable.Range(
                    0,
                    SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes)
                .Select(index => $"AL-SAVE-AUTH-{index:D2}")
                .ToArray();
            string[] reverse = forward.Reverse().ToArray();

            ProfileWriteAuthoritySnapshot first =
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(
                        Unchecked(
                            SaveAuthorityTechnicalLimits.ContractVersion,
                            ProfileWriteAuthorityStatus.Unavailable,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            0,
                            0,
                            false,
                            ProfileAuthoritySourceGeneration.None,
                            forward)));
            ProfileWriteAuthoritySnapshot second =
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(
                        Unchecked(
                            SaveAuthorityTechnicalLimits.ContractVersion,
                            ProfileWriteAuthorityStatus.Unavailable,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            0,
                            0,
                            false,
                            ProfileAuthoritySourceGeneration.None,
                            reverse)));

            CollectionAssert.AreEqual(forward, first.DiagnosticCodes);
            CollectionAssert.AreEqual(
                first.DiagnosticCodes,
                second.DiagnosticCodes);
        }

        [Test]
        public void DiagnosticValidationEnumeratesAtMostLimitPlusOne()
        {
            var hostile = new CountingDiagnosticEnumerable(4096);
            ProfileWriteAuthoritySnapshot raw = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.Unavailable,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                false,
                ProfileAuthoritySourceGeneration.None,
                hostile);

            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes + 1,
                hostile.MoveNextCount);
            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(raw)),
                SaveAuthorityDiagnosticCodes.ProviderDiagnostics);
        }

        [TestCase(DiagnosticEnumerationFault.GetEnumerator)]
        [TestCase(DiagnosticEnumerationFault.MoveNext)]
        [TestCase(DiagnosticEnumerationFault.Current)]
        [TestCase(DiagnosticEnumerationFault.Dispose)]
        public void SnapshotFactoriesFailClosedOnHostileDiagnosticEnumeration(
            DiagnosticEnumerationFault fault)
        {
            AssertUnavailable(
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    ProfileId,
                    Epoch,
                    Fingerprint,
                    ProfileAuthoritySourceGeneration.Primary,
                    new FaultingDiagnosticEnumerable(fault)),
                SaveAuthorityDiagnosticCodes.ProviderDiagnostics);
            AssertUnavailable(
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Primary,
                    new FaultingDiagnosticEnumerable(fault)),
                SaveAuthorityDiagnosticCodes.ProviderDiagnostics);
            AssertUnavailable(
                ProfileWriteAuthoritySnapshotFactory.NonWritable(
                    ProfileWriteAuthorityStatus.Unavailable,
                    0,
                    0,
                    false,
                    ProfileAuthoritySourceGeneration.None,
                    new FaultingDiagnosticEnumerable(fault)),
                SaveAuthorityDiagnosticCodes.ProviderDiagnostics);
        }

        [Test]
        public void OpaqueIdentityValidationIsStrictUtf8AndBoundedAt256Bytes()
        {
            string exactAscii = new string('a', 256);
            string oversizedAscii = new string('a', 257);
            string exactMultibyte = new string('한', 84) + "abcd";
            string oversizedMultibyte = exactMultibyte + "e";

            Assert.IsTrue(
                SaveAuthorityValidation.IsBoundedOpaqueIdentity(exactAscii));
            Assert.IsFalse(
                SaveAuthorityValidation.IsBoundedOpaqueIdentity(
                    oversizedAscii));
            Assert.IsTrue(
                SaveAuthorityValidation.IsBoundedOpaqueIdentity(
                    exactMultibyte));
            Assert.IsFalse(
                SaveAuthorityValidation.IsBoundedOpaqueIdentity(
                    oversizedMultibyte));
            Assert.IsFalse(
                SaveAuthorityValidation.IsBoundedOpaqueIdentity(
                    "operation\nidentity"));
            Assert.IsFalse(
                SaveAuthorityValidation.IsBoundedOpaqueIdentity("\ud800"));
            Assert.IsFalse(
                SaveAuthorityValidation.IsBoundedOpaqueIdentity(
                    new string('x', 4096)));
        }

        [Test]
        public void NonWritableAuthorityNeverLeaksClaimedWritableFields()
        {
            ProfileWriteAuthoritySnapshot raw = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.CommitUncertain,
                ProfileId,
                Epoch,
                Fingerprint,
                2,
                1,
                false,
                ProfileAuthoritySourceGeneration.None,
                new[] { "AL-SAVE-AUTH-COMMIT-UNCERTAIN" });

            ProfileWriteAuthoritySnapshot guarded =
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(raw));

            AssertUnavailable(
                guarded,
                SaveAuthorityDiagnosticCodes.ProviderInvariants);
            Assert.AreEqual(string.Empty, guarded.ProfileId);
            Assert.AreEqual(string.Empty, guarded.AuthorityEpoch);
            Assert.AreEqual(
                string.Empty,
                guarded.VerifiedGenerationFingerprint);
        }

        private static void AssertDiagnosticFailure(IEnumerable<string> diagnostics)
        {
            ProfileWriteAuthoritySnapshot raw = Unchecked(
                SaveAuthorityTechnicalLimits.ContractVersion,
                ProfileWriteAuthorityStatus.Unavailable,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                false,
                ProfileAuthoritySourceGeneration.None,
                diagnostics);

            AssertUnavailable(
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(
                    new FixedProvider(raw)),
                SaveAuthorityDiagnosticCodes.ProviderDiagnostics);
        }

        private static ProfileWriteAuthoritySnapshot ValidRawFor(
            ProfileWriteAuthorityStatus status)
        {
            switch (status)
            {
                case ProfileWriteAuthorityStatus.MissingProfile:
                case ProfileWriteAuthorityStatus.Deleted:
                case ProfileWriteAuthorityStatus.Unavailable:
                    return Unchecked(
                        SaveAuthorityTechnicalLimits.ContractVersion,
                        status,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        0,
                        0,
                        false,
                        ProfileAuthoritySourceGeneration.None,
                        new[] { "AL-SAVE-AUTH-REASON" });
                case ProfileWriteAuthorityStatus.MigrationRequired:
                    return Unchecked(
                        SaveAuthorityTechnicalLimits.ContractVersion,
                        status,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        1,
                        1,
                        true,
                        ProfileAuthoritySourceGeneration.Primary,
                        new[] { "AL-SAVE-AUTH-REASON" });
                case ProfileWriteAuthorityStatus.ForwardSchemaReadOnly:
                    return Unchecked(
                        SaveAuthorityTechnicalLimits.ContractVersion,
                        status,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        3,
                        1,
                        true,
                        ProfileAuthoritySourceGeneration.Primary,
                        new[] { "AL-SAVE-AUTH-REASON" });
                case ProfileWriteAuthorityStatus.DegradedReadOnly:
                    return Unchecked(
                        SaveAuthorityTechnicalLimits.ContractVersion,
                        status,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        2,
                        1,
                        true,
                        ProfileAuthoritySourceGeneration.Primary,
                        new[] { "AL-SAVE-AUTH-REASON" });
                case ProfileWriteAuthorityStatus.RecoveryRequired:
                case ProfileWriteAuthorityStatus.CommitUncertain:
                    return Unchecked(
                        SaveAuthorityTechnicalLimits.ContractVersion,
                        status,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        2,
                        1,
                        false,
                        ProfileAuthoritySourceGeneration.None,
                        new[] { "AL-SAVE-AUTH-REASON" });
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static ProfileWriteAuthoritySnapshot Unchecked(
            string contractVersion,
            ProfileWriteAuthorityStatus status,
            string profileId,
            string epoch,
            string fingerprint,
            int schema,
            int initialization,
            bool hasSource,
            ProfileAuthoritySourceGeneration source,
            IEnumerable<string> diagnostics) =>
            new ProfileWriteAuthoritySnapshot(
                contractVersion,
                status,
                profileId,
                epoch,
                fingerprint,
                schema,
                initialization,
                hasSource,
                source,
                diagnostics);

        private static void AssertUnavailable(
            ProfileWriteAuthoritySnapshot snapshot,
            string diagnostic)
        {
            Assert.NotNull(snapshot);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Unavailable,
                snapshot.Status);
            Assert.AreEqual(string.Empty, snapshot.ProfileId);
            Assert.AreEqual(string.Empty, snapshot.AuthorityEpoch);
            Assert.AreEqual(
                string.Empty,
                snapshot.VerifiedGenerationFingerprint);
            Assert.AreEqual(0, snapshot.SaveSchemaVersion);
            Assert.AreEqual(0, snapshot.ProfileInitializationVersion);
            Assert.IsFalse(snapshot.HasSelectedSourceGeneration);
            Assert.AreEqual(
                ProfileAuthoritySourceGeneration.None,
                snapshot.SelectedSourceGeneration);
            CollectionAssert.AreEqual(
                new[] { diagnostic },
                snapshot.DiagnosticCodes);
        }

        private sealed class FixedProvider : IProfileWriteAuthorityProvider
        {
            private readonly ProfileWriteAuthoritySnapshot _snapshot;

            internal FixedProvider(ProfileWriteAuthoritySnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority() => _snapshot;
        }

        private sealed class ThrowingProvider : IProfileWriteAuthorityProvider
        {
            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                throw new InvalidOperationException("provider failure");
            }
        }

        private sealed class CountingDiagnosticEnumerable : IEnumerable<string>
        {
            private readonly int _count;

            internal CountingDiagnosticEnumerable(int count)
            {
                _count = count;
            }

            internal int MoveNextCount { get; private set; }

            public IEnumerator<string> GetEnumerator()
            {
                for (int index = 0; index < _count; index++)
                {
                    MoveNextCount++;
                    yield return $"AL-SAVE-AUTH-{index:D4}";
                }
            }

            System.Collections.IEnumerator
                System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public enum DiagnosticEnumerationFault
        {
            GetEnumerator,
            MoveNext,
            Current,
            Dispose
        }

        private sealed class FaultingDiagnosticEnumerable :
            IEnumerable<string>
        {
            private readonly DiagnosticEnumerationFault _fault;

            internal FaultingDiagnosticEnumerable(
                DiagnosticEnumerationFault fault)
            {
                _fault = fault;
            }

            public IEnumerator<string> GetEnumerator()
            {
                if (_fault == DiagnosticEnumerationFault.GetEnumerator)
                {
                    throw new InvalidOperationException(
                        "hostile GetEnumerator");
                }

                return new FaultingDiagnosticEnumerator(_fault);
            }

            System.Collections.IEnumerator
                System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class FaultingDiagnosticEnumerator :
            IEnumerator<string>
        {
            private readonly DiagnosticEnumerationFault _fault;
            private bool _moved;

            internal FaultingDiagnosticEnumerator(
                DiagnosticEnumerationFault fault)
            {
                _fault = fault;
            }

            public string Current
            {
                get
                {
                    if (_fault == DiagnosticEnumerationFault.Current)
                    {
                        throw new InvalidOperationException(
                            "hostile Current");
                    }

                    return "AL-SAVE-AUTH-HOSTILE";
                }
            }

            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (_fault == DiagnosticEnumerationFault.MoveNext)
                    throw new InvalidOperationException("hostile MoveNext");
                if (_fault == DiagnosticEnumerationFault.Current && !_moved)
                {
                    _moved = true;
                    return true;
                }

                return false;
            }

            public void Reset() =>
                throw new NotSupportedException();

            public void Dispose()
            {
                if (_fault == DiagnosticEnumerationFault.Dispose)
                    throw new InvalidOperationException("hostile Dispose");
            }
        }
    }
}
