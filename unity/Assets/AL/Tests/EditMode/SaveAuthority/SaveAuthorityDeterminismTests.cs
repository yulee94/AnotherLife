using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using AL.Core.SaveAuthority;

namespace AL.Tests.EditMode.SaveAuthority
{
    public sealed class SaveAuthorityDeterminismTests
    {
        private const string ProfileId =
            "alp_0123456789abcdef0123456789abcdef";
        private const string PrimarySha =
            "c74e4ec22d9fc4801c697450bd2dea62f29cc26e92ff0d09094b54ca4ad342c9";
        private const string BackupSha =
            "ef559255ad2e1da313c8b6f8399ea9ea2cf75ce45601eec50a600da412a2f6c7";
        private const string RetainedFrameHex =
            "0000002b616e6f746865726c6966652e76657269666965642d67656e65726174696f6e2d66696e6765727072696e74000000010000000100000024616c705f303132333435363738396162636465663031323334353637383961626364656600000016616e6f746865726c6966652e6c6f63616c2d7361766500000002000000010000000100000001000000060000000100000002000000000000007b01c74e4ec22d9fc4801c697450bd2dea62f29cc26e92ff0d09094b54ca4ad342c90000000200000002000000000000006401ef559255ad2e1da313c8b6f8399ea9ea2cf75ce45601eec50a600da412a2f6c70000000300000001000000000000000000000000040000000100000000000000000000000005000000010000000000000000000000000600000001000000000000000000";
        private const string RetainedFingerprint =
            "ed971c70bd45f745c9cfdb19d7019bca1f2ac6e3108a7a6f1b1bad11f1eeae49";

        [Test]
        public void RetainedIndependentVectorMatchesExactFrameAndDigest()
        {
            VerifiedGenerationFingerprintFrame frame = RetainedFrame();
            VerifiedGenerationFingerprintResult result =
                VerifiedGenerationFingerprint.Compute(frame);

            Assert.AreEqual(
                VerifiedGenerationFingerprintStatus.Computed,
                result.Status);
            Assert.AreEqual(RetainedFingerprint, result.Value);
            Assert.AreEqual(307, result.CanonicalFrameByteCount);
            Assert.AreEqual(
                RetainedFrameHex,
                VerifiedGenerationFingerprint.EncodeCanonicalFrameHexForTesting(
                    frame));
            CollectionAssert.IsEmpty(result.DiagnosticCodes);
        }

        [Test]
        public void ExactReloadFrameReproducesExactFingerprintAcrossCultures()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                var values = new List<string>();
                foreach (string cultureName in new[] { "en-US", "ko-KR", "tr-TR" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(cultureName);
                    CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
                    values.Add(
                        VerifiedGenerationFingerprint.Compute(RetainedFrame()).Value);
                }

                CollectionAssert.AreEqual(
                    new[]
                    {
                        RetainedFingerprint,
                        RetainedFingerprint,
                        RetainedFingerprint
                    },
                    values);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Test]
        public void EveryBoundScalarAndArtifactChangeChangesFingerprint()
        {
            string baseline =
                VerifiedGenerationFingerprint.Compute(RetainedFrame()).Value;
            var variants = new[]
            {
                Frame(profileId: "alp_1123456789abcdef0123456789abcdef"),
                Frame(schema: 3),
                Frame(initialization: 2),
                Frame(source: ProfileAuthoritySourceGeneration.Backup),
                Frame(ledgerState: VerifiedAuthorityLedgerState.RecoveredCurrent),
                Frame(primary: Present(
                    AuthorityArtifactRole.Primary,
                    124,
                    PrimarySha)),
                Frame(primary: Present(
                    AuthorityArtifactRole.Primary,
                    123,
                    "d74e4ec22d9fc4801c697450bd2dea62f29cc26e92ff0d09094b54ca4ad342c9")),
                Frame(backup: Missing(AuthorityArtifactRole.Backup)),
                Frame(temp: Present(
                    AuthorityArtifactRole.Temp,
                    1,
                    BackupSha)),
                Frame(canonicalPrevious: Present(
                    AuthorityArtifactRole.CanonicalPrevious,
                    2,
                    BackupSha)),
                Frame(legacyPrevious: Present(
                    AuthorityArtifactRole.LegacyPrevious,
                    3,
                    BackupSha)),
                Frame(recoveryWitness: Present(
                    AuthorityArtifactRole.RecoveryWitness,
                    4,
                    BackupSha))
            };

            foreach (VerifiedGenerationFingerprintFrame variant in variants)
            {
                VerifiedGenerationFingerprintResult result =
                    VerifiedGenerationFingerprint.Compute(variant);
                Assert.AreEqual(
                    VerifiedGenerationFingerprintStatus.Computed,
                    result.Status);
                Assert.AreNotEqual(baseline, result.Value);
            }
        }

        [Test]
        public void MalformedFrameFailsClosedWithBoundedCanonicalDiagnostics()
        {
            var invalid = new VerifiedGenerationFingerprintFrame(
                "not-a-profile",
                "anotherlife.local-save",
                -1,
                -1,
                (ProfileAuthoritySourceGeneration)999,
                (VerifiedAuthorityLedgerState)999,
                new SerializedAuthorityArtifactIdentity(
                    (AuthorityArtifactRole)999,
                    (AuthorityArtifactDisposition)999,
                    -1,
                    "UPPER"),
                null,
                null,
                null,
                null,
                null);

            VerifiedGenerationFingerprintResult result =
                VerifiedGenerationFingerprint.Compute(invalid);

            Assert.AreEqual(
                VerifiedGenerationFingerprintStatus.Invalid,
                result.Status);
            Assert.AreEqual(string.Empty, result.Value);
            Assert.LessOrEqual(
                result.DiagnosticCodes.Count,
                SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes);
            CollectionAssert.AreEqual(
                result.DiagnosticCodes.OrderBy(code => code, StringComparer.Ordinal),
                result.DiagnosticCodes);
            Assert.AreEqual(
                result.DiagnosticCodes.Count,
                result.DiagnosticCodes.Distinct(StringComparer.Ordinal).Count());
        }

        [TestCase(0, "")]
        [TestCase(1, "")]
        [TestCase(1, "UPPER")]
        [TestCase(-1, PrimarySha)]
        public void IncoherentArtifactIdentityIsRejected(
            long byteCount,
            string sha)
        {
            SerializedAuthorityArtifactIdentity artifact =
                new SerializedAuthorityArtifactIdentity(
                    AuthorityArtifactRole.Primary,
                    AuthorityArtifactDisposition.VerifiedExact,
                    byteCount,
                    sha);

            VerifiedGenerationFingerprintResult result =
                VerifiedGenerationFingerprint.Compute(Frame(primary: artifact));

            Assert.AreEqual(
                VerifiedGenerationFingerprintStatus.Invalid,
                result.Status);
            Assert.AreEqual(string.Empty, result.Value);
        }

        [Test]
        public void NullFrameIsTypedUnavailable()
        {
            VerifiedGenerationFingerprintResult result =
                VerifiedGenerationFingerprint.Compute(null);

            Assert.AreEqual(
                VerifiedGenerationFingerprintStatus.Unavailable,
                result.Status);
            Assert.AreEqual(string.Empty, result.Value);
            CollectionAssert.AreEqual(
                new[] { SaveAuthorityDiagnosticCodes.FingerprintFrameMissing },
                result.DiagnosticCodes);
        }

        [TestCase("0123456789abcdef0000000000000001", true)]
        [TestCase("ffffffffffffffffffffffffffffffff", true)]
        [TestCase("00000000000000000000000000000000", false)]
        [TestCase("00000000000000000000000000000001", false)]
        [TestCase("0123456789abcdef0000000000000000", false)]
        [TestCase("0123456789ABCDEF0000000000000001", false)]
        [TestCase("0123456789abcdef-000000000000001", false)]
        [TestCase("0123456789abcdef000000000000001", false)]
        [TestCase("0123456789abcdef000000000000000g", false)]
        public void EpochCanonicalityIsExact(string candidate, bool expected)
        {
            Assert.AreEqual(
                expected,
                AuthorityEpochAllocator.IsCanonical(candidate));
        }

        [Test]
        public void ScriptedAllocatorRejectsRepeatRegressionNonceChangeThenAdvances()
        {
            var source = new ScriptedEpochSource(
                "0123456789abcdef0000000000000001",
                "0123456789abcdef0000000000000001",
                "0123456789abcdef0000000000000000",
                "1123456789abcdef0000000000000002",
                "0123456789abcdef0000000000000003");
            var allocator = new AuthorityEpochAllocator(source);

            AuthorityEpochAllocationResult first = allocator.Allocate();
            AuthorityEpochAllocationResult second = allocator.Allocate();

            Assert.AreEqual(AuthorityEpochAllocationStatus.Allocated, first.Status);
            Assert.AreEqual(
                "0123456789abcdef0000000000000001",
                first.AuthorityEpoch);
            Assert.AreEqual(AuthorityEpochAllocationStatus.Allocated, second.Status);
            Assert.AreEqual(
                "0123456789abcdef0000000000000003",
                second.AuthorityEpoch);
            Assert.AreEqual(5, source.CallCount);
        }

        [Test]
        public void EightRejectedCandidatesExhaustExactlyEightAttempts()
        {
            var source = new ScriptedEpochSource(
                Enumerable.Repeat(
                        "00000000000000000000000000000000",
                        SaveAuthorityTechnicalLimits.MaximumEpochAllocationAttempts)
                    .ToArray());
            var allocator = new AuthorityEpochAllocator(source);

            AuthorityEpochAllocationResult result = allocator.Allocate();

            Assert.AreEqual(
                AuthorityEpochAllocationStatus.Unavailable,
                result.Status);
            Assert.AreEqual(string.Empty, result.AuthorityEpoch);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.MaximumEpochAllocationAttempts,
                source.CallCount);
            CollectionAssert.AreEqual(
                new[] { SaveAuthorityDiagnosticCodes.EpochCandidateExhausted },
                result.DiagnosticCodes);
        }

        [Test]
        public void MaximumCounterIsIssuedOnceThenOverflowFailsClosed()
        {
            var source = new ScriptedEpochSource(
                "0123456789abcdeffffffffffffffffe",
                "0123456789abcdefffffffffffffffff");
            var allocator = new AuthorityEpochAllocator(source);

            AuthorityEpochAllocationResult beforeMax = allocator.Allocate();
            AuthorityEpochAllocationResult atMax = allocator.Allocate();
            AuthorityEpochAllocationResult overflow = allocator.Allocate();

            Assert.AreEqual(
                "0123456789abcdeffffffffffffffffe",
                beforeMax.AuthorityEpoch);
            Assert.AreEqual(
                "0123456789abcdefffffffffffffffff",
                atMax.AuthorityEpoch);
            Assert.AreEqual(
                AuthorityEpochAllocationStatus.Unavailable,
                overflow.Status);
            Assert.AreEqual(string.Empty, overflow.AuthorityEpoch);
        }

        [Test]
        public void SourceThrowFailsClosedWithoutEscaping()
        {
            AuthorityEpochAllocationResult result =
                new AuthorityEpochAllocator(new ThrowingEpochSource()).Allocate();

            Assert.AreEqual(
                AuthorityEpochAllocationStatus.Unavailable,
                result.Status);
            CollectionAssert.AreEqual(
                new[] { SaveAuthorityDiagnosticCodes.EpochSourceUnavailable },
                result.DiagnosticCodes);
        }

        [Test]
        public void ReentrantEpochSourceFailsInnerAllocationWithoutDeadlock()
        {
            var source = new ReentrantEpochSource();
            var allocator = new AuthorityEpochAllocator(source);
            source.Allocator = allocator;

            AuthorityEpochAllocationResult outer = allocator.Allocate();

            Assert.AreEqual(
                AuthorityEpochAllocationStatus.Allocated,
                outer.Status);
            Assert.AreEqual(
                "0123456789abcdef0000000000000001",
                outer.AuthorityEpoch);
            Assert.NotNull(source.InnerResult);
            Assert.AreEqual(
                AuthorityEpochAllocationStatus.Unavailable,
                source.InnerResult.Status);
            CollectionAssert.AreEqual(
                new[] { SaveAuthorityDiagnosticCodes.EpochReentrant },
                source.InnerResult.DiagnosticCodes);
        }

        [Test]
        public void OneAllocatorAcrossServiceReplacementsNeverReusesEpoch()
        {
            var source = new ScriptedEpochSource(
                "0123456789abcdef0000000000000001",
                "0123456789abcdef0000000000000002",
                "0123456789abcdef0000000000000003");
            var allocator = new AuthorityEpochAllocator(source);

            string serviceOne = allocator.Allocate().AuthorityEpoch;
            string reloadedService = allocator.Allocate().AuthorityEpoch;
            string replacementService = allocator.Allocate().AuthorityEpoch;

            CollectionAssert.AreEqual(
                new[]
                {
                    "0123456789abcdef0000000000000001",
                    "0123456789abcdef0000000000000002",
                    "0123456789abcdef0000000000000003"
                },
                new[] { serviceOne, reloadedService, replacementService });
        }

        [Test]
        public void ConcurrentAllocationIsUniqueAndStrictlyIncreasing()
        {
            const int count = 64;
            var source = new IncrementingEpochSource(0x8123456789abcdefUL);
            var allocator = new AuthorityEpochAllocator(source);
            var results = new ConcurrentBag<string>();
            var start = new ManualResetEventSlim(false);
            Task[] tasks = Enumerable.Range(0, count)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait(TimeSpan.FromSeconds(5));
                    results.Add(allocator.Allocate().AuthorityEpoch);
                }))
                .ToArray();

            start.Set();
            Assert.IsTrue(Task.WaitAll(tasks, TimeSpan.FromSeconds(10)));

            string[] ordered = results
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Assert.AreEqual(count, ordered.Length);
            Assert.AreEqual(count, ordered.Distinct(StringComparer.Ordinal).Count());
            for (int index = 0; index < ordered.Length; index++)
            {
                Assert.AreEqual(
                    $"8123456789abcdef{index + 1:x16}",
                    ordered[index]);
            }
        }

        private static VerifiedGenerationFingerprintFrame RetainedFrame() =>
            Frame();

        private static VerifiedGenerationFingerprintFrame Frame(
            string profileId = ProfileId,
            int schema = 2,
            int initialization = 1,
            ProfileAuthoritySourceGeneration source =
                ProfileAuthoritySourceGeneration.Primary,
            VerifiedAuthorityLedgerState ledgerState =
                VerifiedAuthorityLedgerState.CanonicalCurrent,
            SerializedAuthorityArtifactIdentity primary = null,
            SerializedAuthorityArtifactIdentity backup = null,
            SerializedAuthorityArtifactIdentity temp = null,
            SerializedAuthorityArtifactIdentity canonicalPrevious = null,
            SerializedAuthorityArtifactIdentity legacyPrevious = null,
            SerializedAuthorityArtifactIdentity recoveryWitness = null) =>
            new VerifiedGenerationFingerprintFrame(
                profileId,
                SaveAuthorityTechnicalLimits.SaveFormatId,
                schema,
                initialization,
                source,
                ledgerState,
                primary ?? Present(
                    AuthorityArtifactRole.Primary,
                    123,
                    PrimarySha),
                backup ?? Present(
                    AuthorityArtifactRole.Backup,
                    100,
                    BackupSha),
                temp ?? Missing(AuthorityArtifactRole.Temp),
                canonicalPrevious ?? Missing(
                    AuthorityArtifactRole.CanonicalPrevious),
                legacyPrevious ?? Missing(
                    AuthorityArtifactRole.LegacyPrevious),
                recoveryWitness ?? Missing(
                    AuthorityArtifactRole.RecoveryWitness));

        private static SerializedAuthorityArtifactIdentity Present(
            AuthorityArtifactRole role,
            long byteCount,
            string sha) =>
            new SerializedAuthorityArtifactIdentity(
                role,
                AuthorityArtifactDisposition.VerifiedExact,
                byteCount,
                sha);

        private static SerializedAuthorityArtifactIdentity Missing(
            AuthorityArtifactRole role) =>
            new SerializedAuthorityArtifactIdentity(
                role,
                AuthorityArtifactDisposition.Missing,
                0,
                string.Empty);

        private sealed class ScriptedEpochSource : IAuthorityEpochCandidateSource
        {
            private readonly Queue<string> _values;

            internal ScriptedEpochSource(params string[] values)
            {
                _values = new Queue<string>(values);
            }

            internal int CallCount { get; private set; }

            public bool TryGetNextCandidate(out string candidate)
            {
                CallCount++;
                if (_values.Count == 0)
                {
                    candidate = string.Empty;
                    return false;
                }

                candidate = _values.Dequeue();
                return true;
            }
        }

        private sealed class ThrowingEpochSource : IAuthorityEpochCandidateSource
        {
            public bool TryGetNextCandidate(out string candidate)
            {
                candidate = string.Empty;
                throw new InvalidOperationException("source failure");
            }
        }

        private sealed class ReentrantEpochSource :
            IAuthorityEpochCandidateSource
        {
            internal AuthorityEpochAllocator Allocator { get; set; }
            internal AuthorityEpochAllocationResult InnerResult { get; private set; }

            public bool TryGetNextCandidate(out string candidate)
            {
                InnerResult = Allocator.Allocate();
                candidate = "0123456789abcdef0000000000000001";
                return true;
            }
        }

        private sealed class IncrementingEpochSource :
            IAuthorityEpochCandidateSource
        {
            private readonly ulong _nonce;
            private long _counter;

            internal IncrementingEpochSource(ulong nonce)
            {
                _nonce = nonce;
            }

            public bool TryGetNextCandidate(out string candidate)
            {
                long counter = Interlocked.Increment(ref _counter);
                candidate = _nonce.ToString("x16", CultureInfo.InvariantCulture) +
                            ((ulong)counter).ToString(
                                "x16",
                                CultureInfo.InvariantCulture);
                return true;
            }
        }
    }
}
