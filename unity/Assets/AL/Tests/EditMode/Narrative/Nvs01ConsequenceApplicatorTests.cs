using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.ChampionMode.Encounter;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01ConsequenceApplicatorTests
    {
        private const string ProfileId =
            "alp_11111111111111111111111111111111";
        private const string AlternateProfileId =
            "alp_22222222222222222222222222222222";
        private const string AuthorityEpoch =
            "11111111111111110000000000000001";
        private const string Fingerprint =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string DependencyProviderId =
            "AL.TEST.NVS01.CONSEQUENCE.PROVIDER";
        private const string DependencyCatalogSetId =
            "another-life-test-authority";
        private const string DependencySourceFingerprint =
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

        private static Nvs01VerifiedCatalog _catalog;

        [TestCase("crownlands")]
        [TestCase("stonehold")]
        [TestCase("eldergrove")]
        [TestCase("umbral")]
        public void ArenaSuccessAppliesTearOnceAndNotifiesAfterCommit(string realmId)
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation(realmId);
            var harness = new ApplicatorHarness(mutation.Expected);
            Nvs01ConsequenceApplicationResult result =
                harness.Commit(mutation, ArenaDomain());

            Assert.AreEqual(
                Nvs01ConsequenceApplicationStatus.Applied,
                result.Status,
                result.DiagnosticCode);
            Assert.AreEqual(1, result.PersistAttemptCount);
            Assert.AreEqual(1, harness.Persistence.AttemptCount);
            Assert.AreEqual(1, harness.Notifications.Enqueued.Count);
            Assert.AreEqual(
                Nvs01ConsequenceDiagnosticCodes.EffectsCommittedNotificationId,
                harness.Notifications.Enqueued[0].NotificationId);
            SaveGameData published = harness.Persistence.LoadPublished();
            CollectionAssert.AreEqual(
                new[] { Nvs01ConsequenceContract.TearArtifactId },
                published.Nvs01Progress.AcquiredArtifactIds);
            CollectionAssert.AreEqual(
                new[] { Nvs01ConsequenceContract.TearConsequenceId },
                published.Nvs01Progress.AppliedEffectKeys);
            Assert.AreEqual(1, published.Nvs01Progress.ApplicationReceipts.Count);
            Assert.AreEqual(
                Nvs01ConsequenceContract.OathmarkTechnicalCurrencyId,
                published.Nvs01Progress.ApplicationReceipts[0].TechnicalCurrencyId);
            Assert.AreEqual(999, ReadGold(published));
            Assert.AreEqual("CH0_PROLOGUE", published.CurrentChapterId);
            Assert.AreEqual(10f, ReadValerius(published));
            Assert.AreEqual(
                Nvs01ConsequenceContract.OathmarkTechnicalCurrencyId,
                result.Fidelity.TechnicalCurrencyId);
            Assert.AreEqual(
                Nvs01ConsequenceContract.EncounterResultSnapshotVersion,
                result.Fidelity.EncounterSnapshotVersion);
        }

        [TestCase("crownlands", "C1_CL")]
        [TestCase("stonehold", "C1_SH")]
        [TestCase("eldergrove", "C1_EG")]
        [TestCase("umbral", "C1_UM")]
        public void ReportCompletionAtomicallyCreditsOathmarkAffinityAndChapter(
            string realmId,
            string expectedChapter)
        {
            var fixture = new RuntimeFixture();
            Nvs01MutationPlan arena = fixture.ArenaSuccessMutation(realmId);
            var harness = new ApplicatorHarness(arena.Expected);
            AssertApplied(harness.Commit(arena, ArenaDomain()));

            Nvs01MutationPlan report = fixture.ReportMutationFromCurrent();
            Nvs01ConsequenceDomainSnapshot domain =
                DomainFromPublished(harness.Persistence.LoadPublished(), report);
            harness.SeedExpected(report.Expected);
            Nvs01ConsequenceApplicationResult result =
                harness.Commit(report, domain);

            Assert.AreEqual(
                Nvs01ConsequenceApplicationStatus.Applied,
                result.Status,
                result.DiagnosticCode);
            SaveGameData published = harness.Persistence.LoadPublished();
            Assert.AreEqual(
                Nvs01ConsequenceContract.CompletedStateId,
                published.Nvs01Progress.StateId);
            Assert.AreEqual(2, published.Nvs01Progress.ApplicationReceipts.Count);
            Nvs01ConsequenceApplicationReceiptData reportReceipt =
                published.Nvs01Progress.ApplicationReceipts[1];
            Assert.AreEqual(
                Nvs01ConsequenceContract.OathmarkTechnicalCurrencyId,
                reportReceipt.TechnicalCurrencyId);
            Assert.AreEqual(1000, reportReceipt.PreviousGoldBalance);
            Assert.AreEqual(1500, reportReceipt.ResultingGoldBalance);
            Assert.AreEqual(15f, reportReceipt.ResultingValeriusAffinity);
            Assert.AreEqual(15f, ReadValerius(published));
            Assert.AreEqual(expectedChapter, published.CurrentChapterId);
            Assert.AreEqual(expectedChapter, published.Nvs01Progress.UnlockedChapterId);
            Assert.AreEqual(999, ReadGold(published));
            CollectionAssert.AreEqual(
                Nvs01ConsequenceContract.ExpectedConsequenceOrder,
                published.Nvs01Progress.AppliedEffectKeys);
            Assert.AreEqual(2, harness.Notifications.Enqueued.Count);
        }

        [Test]
        public void ExactArenaReplayDoesNotPersistOrNotifyAgain()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            var harness = new ApplicatorHarness(mutation.Expected);
            AssertApplied(harness.Commit(mutation, ArenaDomain()));
            Nvs01ConsequenceDomainSnapshot applied =
                DomainFromPublished(harness.Persistence.LoadPublished(), mutation);
            Nvs01MutationPlan replay = BindReplay(
                Nvs01MutationPlan.ForExactReplay(mutation.Candidate));

            Nvs01ConsequenceApplicationResult result =
                harness.Commit(replay, applied);

            Assert.AreEqual(
                Nvs01ConsequenceApplicationStatus.AlreadyApplied,
                result.Status,
                result.DiagnosticCode);
            Assert.AreEqual(0, result.PersistAttemptCount);
            Assert.AreEqual(1, harness.Persistence.AttemptCount);
            Assert.AreEqual(1, harness.Notifications.Enqueued.Count);
            Assert.AreEqual(1000, result.Receipt.ResultingGoldBalance);
        }

        [Test]
        public void LegacyGoldAndKingdomSubstitutionFailClosed()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation("crownlands");
            var harness = new ApplicatorHarness(mutation.Expected);

            AssertRejected(
                harness.Commit(
                    mutation,
                    ReportDomain(
                        mutation,
                        technicalCurrencyId: Nvs01ConsequenceContract
                            .ForbiddenLegacyGoldResourceId)),
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);
            AssertRejected(
                harness.Commit(
                    mutation,
                    ReportDomain(
                        mutation,
                        technicalCurrencyId: Nvs01ConsequenceContract
                            .ForbiddenKingdomResourceId)),
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);
            Assert.AreEqual(0, harness.Persistence.AttemptCount);
            Assert.AreEqual(0, harness.Notifications.Enqueued.Count);
            Assert.AreEqual(999, ReadGold(harness.Persistence.LoadPublished()));
        }

        [Test]
        public void MixedProfileAndEncounterAuthorityFailClosed()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            var harness = new ApplicatorHarness(mutation.Expected);
            harness.Persistence.LoadPublished().ProfileId = AlternateProfileId;
            harness = new ApplicatorHarness(
                mutation.Expected,
                published: IdentitySave(AlternateProfileId, mutation.Expected));

            AssertRejected(
                harness.Commit(mutation, ArenaDomain()),
                Nvs01ConsequenceDiagnosticCodes.MixedAuthority);
            Assert.AreEqual(0, harness.Persistence.AttemptCount);
        }

        [Test]
        public void ReportWithoutArenaLedgerFailsClosedAsPartial()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation("crownlands");
            var harness = new ApplicatorHarness(mutation.Expected);

            AssertRejected(
                harness.Commit(mutation, ReportDomain(mutation)),
                Nvs01ConsequenceDiagnosticCodes.PartialApplication);
            Assert.AreEqual(0, harness.Persistence.AttemptCount);
            CollectionAssert.IsEmpty(
                harness.Persistence.LoadPublished().Nvs01Progress.AcquiredArtifactIds);
        }

        [Test]
        public void PersistenceFailurePreservesPriorPublishedState()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            var harness = new ApplicatorHarness(mutation.Expected);
            harness.Persistence.FailNext = true;

            Nvs01ConsequenceApplicationResult result =
                harness.Commit(mutation, ArenaDomain());

            Assert.AreEqual(
                Nvs01ConsequenceApplicationStatus.PersistenceFailedPreviousPreserved,
                result.Status);
            Assert.AreEqual(
                Nvs01ConsequenceDiagnosticCodes.PersistFailed,
                result.DiagnosticCode);
            Assert.AreEqual(1, result.PersistAttemptCount);
            CollectionAssert.IsEmpty(
                harness.Persistence.LoadPublished().Nvs01Progress.AcquiredArtifactIds);
            Assert.AreEqual(0, harness.Notifications.Enqueued.Count);
        }

        [Test]
        public void NotificationFailureDoesNotRollBackCommittedEffects()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            var harness = new ApplicatorHarness(mutation.Expected);
            harness.Notifications.FailNext = true;

            Nvs01ConsequenceApplicationResult result =
                harness.Commit(mutation, ArenaDomain());

            Assert.AreEqual(
                Nvs01ConsequenceApplicationStatus.NotificationFailedAfterCommit,
                result.Status);
            Assert.AreEqual(
                Nvs01ConsequenceDiagnosticCodes.NotifyFailed,
                result.DiagnosticCode);
            CollectionAssert.AreEqual(
                new[] { Nvs01ConsequenceContract.TearArtifactId },
                harness.Persistence.LoadPublished().Nvs01Progress.AcquiredArtifactIds);
        }

        [Test]
        public void SchemaOneAuthorityFailsClosed()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            SaveGameData save = IdentitySave(ProfileId, mutation.Expected);
            save.SaveSchemaVersion =
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion;
            var harness = new ApplicatorHarness(mutation.Expected, save);

            AssertRejected(
                harness.Commit(mutation, ArenaDomain()),
                Nvs01ConsequenceDiagnosticCodes.AuthorityUnavailable);
        }

        [Test]
        public void ReplayFingerprintMismatchFailsClosed()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            var harness = new ApplicatorHarness(mutation.Expected);
            AssertApplied(harness.Commit(mutation, ArenaDomain()));
            SaveGameData published = harness.Persistence.LoadPublished();
            published.Nvs01Progress.ApplicationReceipts[0].PlanFingerprint =
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
            harness.Persistence.PersistAndVerify(published);
            Nvs01ConsequenceDomainSnapshot tampered =
                DomainFromPublished(harness.Persistence.LoadPublished(), mutation);
            Nvs01MutationPlan replay = BindReplay(
                Nvs01MutationPlan.ForExactReplay(mutation.Candidate));

            Nvs01ConsequenceApplicationResult result =
                harness.Commit(replay, tampered);

            Assert.AreEqual(
                Nvs01ConsequenceApplicationStatus.Rejected,
                result.Status);
            Assert.IsTrue(
                result.DiagnosticCode ==
                Nvs01ConsequenceDiagnosticCodes.ReplayFingerprintMismatch ||
                result.DiagnosticCode ==
                Nvs01ConsequenceDiagnosticCodes.PartialApplication ||
                result.DiagnosticCode ==
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed,
                result.DiagnosticCode);
        }

        private static Nvs01MutationPlan BindReplay(Nvs01MutationPlan source)
        {
            ProfileAuthorityExpectation expectation =
                ProfileAuthorityExpectation.From(Authority());
            Nvs01OperationReceipt operation = source.Candidate.LastOperation;
            var stamped = new Nvs01OperationReceipt(
                operation.OperationId,
                operation.PayloadFingerprint,
                operation.Status,
                operation.Revision,
                operation.StateId,
                operation.EventId,
                operation.CorrelationId,
                expectation.ExpectedGenerationFingerprint);
            return source.BindAuthority(
                expectation,
                new Nvs01QuestSnapshot(
                    source.Candidate.PacketVersion,
                    source.Candidate.PacketSha256,
                    source.Candidate.QuestId,
                    source.Candidate.Revision,
                    source.Candidate.StateId,
                    source.Candidate.Objectives.ToArray(),
                    source.Candidate.CurrentDialogueNodeId,
                    source.Candidate.PendingChoice,
                    source.Candidate.PendingSemanticActionId,
                    source.Candidate.CommittedRealmId,
                    source.Candidate.EncounterStatus,
                    source.Candidate.CurrentEncounter,
                    source.Candidate.LastEncounterCorrelationId,
                    source.Candidate.LastEncounterOutcome,
                    source.Candidate.LastEncounterEventId,
                    source.Candidate.LastEncounterSnapshotVersion,
                    source.Candidate.LastEncounterSnapshotReference,
                    stamped,
                    source.Candidate.ConsequenceIntentIds.ToArray()));
        }

        private static void AssertApplied(Nvs01ConsequenceApplicationResult result)
        {
            Assert.True(result.IsApplied, result.DiagnosticCode);
        }

        private static void AssertRejected(
            Nvs01ConsequenceApplicationResult result,
            string diagnostic)
        {
            Assert.AreEqual(
                Nvs01ConsequenceApplicationStatus.Rejected,
                result.Status);
            Assert.AreEqual(diagnostic, result.DiagnosticCode);
            Assert.AreEqual(0, result.PersistAttemptCount);
        }

        private static Nvs01ConsequencePlanningContext ArenaContext(
            Nvs01MutationPlan mutation,
            Nvs01ConsequenceDomainSnapshot domain = null,
            ProfileWriteAuthoritySnapshot authority = null)
        {
            return Context(
                mutation,
                domain ?? ArenaDomain(),
                authority);
        }

        private static Nvs01ConsequencePlanningContext ReportContext(
            Nvs01MutationPlan mutation,
            Nvs01ConsequenceDomainSnapshot domain,
            ProfileWriteAuthoritySnapshot authority = null)
        {
            return Context(mutation, domain, authority);
        }

        private static Nvs01ConsequencePlanningContext Context(
            Nvs01MutationPlan mutation,
            Nvs01ConsequenceDomainSnapshot domain,
            ProfileWriteAuthoritySnapshot authority)
        {
            ProfileWriteAuthoritySnapshot resolved = authority ?? Authority();
            var harness = new ConsequencePlannerHarness();
            Nvs01VerifiedConsequenceDependencies dependencies = harness.Capture(
                VerifiedCatalog(),
                resolved.ProfileId,
                resolved.VerifiedGenerationFingerprint,
                Capabilities(),
                domain,
                ChapterAuthority());
            return new Nvs01ConsequencePlanningContext(
                VerifiedCatalog(),
                mutation,
                resolved,
                ProfileAuthorityExpectation.From(resolved),
                mutation.Expected.Revision,
                new Nvs01ConsequenceReceiptExpectation(
                    mutation.Candidate.LastOperation?.OperationId,
                    mutation.Candidate.LastOperation?.PayloadFingerprint),
                dependencies);
        }

        private static Nvs01ConsequenceDomainSnapshot ArenaDomain() =>
            new Nvs01ConsequenceDomainSnapshot(
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                1000,
                10f,
                "CH0_PROLOGUE",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<Nvs01ConsequenceApplicationReceipt>());

        private static Nvs01ConsequenceDomainSnapshot ReportDomain(
            Nvs01MutationPlan mutation,
            string technicalCurrencyId = null)
        {
            string correlationId =
                mutation.Candidate.LastEncounterCorrelationId;
            return new Nvs01ConsequenceDomainSnapshot(
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                1000,
                10f,
                "CH0_PROLOGUE",
                new[] { Nvs01ConsequenceContract.TearArtifactId },
                new[]
                {
                    Nvs01ConsequenceContract.ArenaOperationPrefix + correlationId
                },
                new[] { Nvs01ConsequenceContract.TearConsequenceId },
                new[]
                {
                    ArenaReceipt(
                        mutation.Candidate.CommittedRealmId,
                        correlationId,
                        mutation.Expected.Revision - 1)
                },
                technicalCurrencyId);
        }

        private static Nvs01ConsequenceApplicationReceipt ArenaReceipt(
            string realmId,
            string correlationId,
            long expectedRevision)
        {
            return Nvs01ConsequenceApplicationReceipt.Create(
                Nvs01ConsequencePlanKind.ArenaSuccess,
                Nvs01ConsequenceContract.ArenaOperationPrefix + correlationId,
                ProfileId,
                Fingerprint,
                "11111111-1111-4111-8111-000000000001",
                Fingerprint,
                string.Empty,
                string.Empty,
                realmId,
                correlationId,
                expectedRevision,
                expectedRevision + 1,
                new[] { Nvs01ConsequenceContract.TearConsequenceId },
                string.Empty,
                1000,
                1000,
                10f,
                10f,
                "CH0_PROLOGUE",
                "CH0_PROLOGUE");
        }

        private static Nvs01ConsequenceDomainSnapshot DomainFromPublished(
            SaveGameData save,
            Nvs01MutationPlan mutation)
        {
            var receipts = new List<Nvs01ConsequenceApplicationReceipt>();
            IList<Nvs01ConsequenceApplicationReceiptData> rows =
                save.Nvs01Progress.ApplicationReceipts;
            for (int index = 0; index < rows.Count; index++)
            {
                receipts.Add(Nvs01ProgressCodec.DecodeReceipt(rows[index]));
            }

            return new Nvs01ConsequenceDomainSnapshot(
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                receipts.Count == 0
                    ? 1000
                    : receipts[receipts.Count - 1].ResultingGoldBalance,
                ReadValerius(save),
                save.CurrentChapterId,
                save.Nvs01Progress.AcquiredArtifactIds,
                save.Nvs01Progress.AppliedOperationIds,
                save.Nvs01Progress.AppliedEffectKeys,
                receipts);
        }

        private static Nvs01CapabilitySnapshot Capabilities()
        {
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (Nvs01ExternalCapability capability in
                     VerifiedCatalog().Catalog.ExternalCapabilities)
            {
                values[capability.Id] = true;
            }

            return new Nvs01CapabilitySnapshot(values);
        }

        private static Nvs01ChapterAuthoritySnapshot ChapterAuthority() =>
            new Nvs01ChapterAuthoritySnapshot(
                Nvs01ConsequenceDependencyStatus.Available,
                true,
                new List<Nvs01ChapterReference>
                {
                    new Nvs01ChapterReference("C1_CL", "crownlands", 1, false),
                    new Nvs01ChapterReference("C1_SH", "stonehold", 1, false),
                    new Nvs01ChapterReference("C1_EG", "eldergrove", 1, false),
                    new Nvs01ChapterReference("C1_UM", "umbral", 1, false),
                    new Nvs01ChapterReference("C2_CL", "crownlands", 2, false),
                    new Nvs01ChapterReference("C2_SH", "stonehold", 2, false),
                    new Nvs01ChapterReference("C2_EG", "eldergrove", 2, false),
                    new Nvs01ChapterReference("C2_UM", "umbral", 2, false)
                });

        private static ProfileWriteAuthoritySnapshot Authority() =>
            ProfileWriteAuthoritySnapshotFactory.Writable(
                ProfileId,
                AuthorityEpoch,
                Fingerprint,
                ProfileAuthoritySourceGeneration.Primary,
                Array.Empty<string>());

        private static Nvs01VerifiedCatalog VerifiedCatalog()
        {
            if (_catalog != null) return _catalog;
            string path = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "AL",
                "Narrative",
                "OMEN_1.catalog.json");
            Nvs01CatalogValidationResult result =
                Nvs01CatalogValidator.ValidateCanonicalArtifact(
                    File.ReadAllBytes(path));
            Assert.True(result.IsAccepted);
            _catalog = result.VerifiedCatalog;
            return _catalog;
        }

        private static SaveGameData IdentitySave(
            string profileId,
            Nvs01QuestSnapshot expected)
        {
            return new SaveGameData
            {
                SaveFormatId = SaveAuthorityTechnicalLimits.SaveFormatId,
                SaveSchemaVersion = SaveAuthorityTechnicalLimits
                    .IdentityAwareSaveSchemaVersion,
                ProfileInitializationVersion = SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion,
                ProfileId = profileId,
                SelectedRealm = RealmId.Crownlands,
                CurrentChapterId = "CH0_PROLOGUE",
                Resources = new List<ResourceData>
                {
                    new ResourceData { Type = ResourceType.Gold, Amount = 999 }
                },
                Reputation = new List<NpcAffinityData>
                {
                    new NpcAffinityData
                    {
                        NpcId = Nvs01ConsequenceContract.ValeriusNpcId,
                        Affinity = 10f
                    }
                },
                Nvs01Progress = Nvs01ProgressCodec.Encode(expected),
                LastSavedTimestamp = 1
            };
        }

        private static long ReadGold(SaveGameData save)
        {
            long total = 0;
            for (int index = 0; index < save.Resources.Count; index++)
            {
                if (save.Resources[index].Type == ResourceType.Gold)
                {
                    total += save.Resources[index].Amount;
                }
            }

            return total;
        }

        private static float ReadValerius(SaveGameData save)
        {
            for (int index = 0; index < save.Reputation.Count; index++)
            {
                if (string.Equals(
                        save.Reputation[index].NpcId,
                        Nvs01ConsequenceContract.ValeriusNpcId,
                        StringComparison.Ordinal))
                {
                    return save.Reputation[index].Affinity;
                }
            }

            return 0f;
        }

        private sealed class ApplicatorHarness
        {
            internal ApplicatorHarness(
                Nvs01QuestSnapshot expected,
                SaveGameData published = null)
            {
                Persistence = new InMemoryNvs01ConsequenceCandidatePersistence(
                    published ?? IdentitySave(ProfileId, expected));
                Notifications = new RecordingNvs01ConsequenceNotificationOutbox();
                Planner = new ConsequencePlannerHarness();
                Applicator = new Nvs01ConsequenceApplicator(
                    Planner.Planner,
                    Persistence,
                    Notifications);
            }

            internal InMemoryNvs01ConsequenceCandidatePersistence Persistence
            {
                get;
            }
            internal RecordingNvs01ConsequenceNotificationOutbox Notifications
            {
                get;
            }
            internal ConsequencePlannerHarness Planner { get; }
            internal Nvs01ConsequenceApplicator Applicator { get; }

            internal void SeedExpected(Nvs01QuestSnapshot expected)
            {
                SaveGameData current = Persistence.LoadPublished();
                Nvs01ProgressData ledger = current.Nvs01Progress;
                current.Nvs01Progress = Nvs01ProgressCodec.Encode(expected);
                Nvs01ProgressCodec.CopyConsequenceLedger(
                    current.Nvs01Progress,
                    ledger);
                Persistence.PersistAndVerify(current);
            }

            internal Nvs01ConsequenceApplicationResult Commit(
                Nvs01MutationPlan mutation,
                Nvs01ConsequenceDomainSnapshot domain,
                ProfileWriteAuthoritySnapshot authority = null)
            {
                ProfileWriteAuthoritySnapshot resolved = authority ?? Authority();
                Nvs01VerifiedConsequenceDependencies dependencies =
                    Planner.Capture(
                        VerifiedCatalog(),
                        resolved.ProfileId,
                        resolved.VerifiedGenerationFingerprint,
                        Capabilities(),
                        domain,
                        ChapterAuthority());
                var context = new Nvs01ConsequencePlanningContext(
                    VerifiedCatalog(),
                    mutation,
                    resolved,
                    ProfileAuthorityExpectation.From(resolved),
                    mutation.Expected.Revision,
                    new Nvs01ConsequenceReceiptExpectation(
                        mutation.Candidate.LastOperation?.OperationId,
                        mutation.Candidate.LastOperation?.PayloadFingerprint),
                    dependencies);
                return Applicator.Commit(context);
            }
        }

        private sealed class ConsequencePlannerHarness
        {
            private readonly TestDependencyProvider _provider;
            private readonly Nvs01ConsequenceDependencyAuthority _authority;

            internal ConsequencePlannerHarness()
            {
                _provider = new TestDependencyProvider();
                _authority = new Nvs01ConsequenceDependencyAuthority(
                    _provider,
                    DependencyProviderId,
                    DependencyCatalogSetId);
                Planner = new Nvs01ConsequencePlanner(_authority);
            }

            internal Nvs01ConsequencePlanner Planner { get; }

            internal Nvs01VerifiedConsequenceDependencies Capture(
                Nvs01VerifiedCatalog catalog,
                string profileId,
                string expectedGenerationFingerprint,
                Nvs01CapabilitySnapshot capabilities,
                Nvs01ConsequenceDomainSnapshot domain,
                Nvs01ChapterAuthoritySnapshot chapters)
            {
                var entries = new List<Nvs01ConsequenceReceiptAuthorityEntry>();
                for (int index = 0; index < domain.ApplicationReceipts.Count; index++)
                {
                    Nvs01ConsequenceApplicationReceipt receipt =
                        domain.ApplicationReceipts[index];
                    if (receipt == null) continue;
                    entries.Add(
                        new Nvs01ConsequenceReceiptAuthorityEntry(
                            receipt.OperationId,
                            receipt.PlanFingerprint));
                }

                _provider.Configure(
                    capabilities,
                    domain,
                    chapters,
                    new Nvs01ConsequenceReceiptAuthoritySnapshot(entries));
                Assert.True(
                    _authority.TryCapture(
                        catalog,
                        profileId,
                        expectedGenerationFingerprint,
                        out Nvs01VerifiedConsequenceDependencies result));
                return result;
            }
        }

        private sealed class TestDependencyProvider :
            INvs01ConsequenceDependencyProvider
        {
            private readonly Nvs01ConsequenceDependencyProviderIdentity _identity =
                new Nvs01ConsequenceDependencyProviderIdentity(
                    Nvs01ConsequenceContract.DependencyAuthorityContractVersion,
                    DependencyProviderId,
                    DependencyCatalogSetId,
                    "game-data-test-v1",
                    "source-test-v1",
                    DependencySourceFingerprint,
                    1);
            private Nvs01CapabilitySnapshot _capabilities;
            private Nvs01ConsequenceDomainSnapshot _domain;
            private Nvs01ChapterAuthoritySnapshot _chapters;
            private Nvs01ConsequenceReceiptAuthoritySnapshot _receiptAuthorities;

            internal void Configure(
                Nvs01CapabilitySnapshot capabilities,
                Nvs01ConsequenceDomainSnapshot domain,
                Nvs01ChapterAuthoritySnapshot chapters,
                Nvs01ConsequenceReceiptAuthoritySnapshot receiptAuthorities)
            {
                _capabilities = capabilities;
                _domain = domain;
                _chapters = chapters;
                _receiptAuthorities = receiptAuthorities;
            }

            public bool TryGetIdentity(
                out Nvs01ConsequenceDependencyProviderIdentity identity)
            {
                identity = _identity;
                return true;
            }

            public bool TryCapture(
                string profileId,
                string expectedGenerationFingerprint,
                out Nvs01ConsequenceDependencyProviderCapture capture)
            {
                capture = new Nvs01ConsequenceDependencyProviderCapture(
                    _identity,
                    profileId,
                    expectedGenerationFingerprint,
                    _capabilities,
                    _domain,
                    _chapters,
                    _receiptAuthorities);
                return true;
            }
        }

        private sealed class CaptureCommitter : INvs01MutationCommitter
        {
            internal Nvs01MutationPlan LastPlan { get; private set; }

            internal void Clear()
            {
                LastPlan = null;
            }

            public bool TryCommit(
                Nvs01MutationPlan plan,
                out Nvs01QuestSnapshot committed,
                out Nvs01RuntimeDiagnostic diagnostic)
            {
                LastPlan = plan;
                committed = plan.Candidate;
                diagnostic = null;
                return true;
            }
        }

        private sealed class RuntimeFixture
        {
            private readonly CaptureCommitter _committer = new CaptureCommitter();
            private int _guidCounter;

            internal RuntimeFixture()
            {
                Runtime = new Nvs01QuestRuntime(
                    VerifiedCatalog(),
                    _committer,
                    NextGuid);
            }

            private Nvs01QuestRuntime Runtime { get; }

            internal Nvs01MutationPlan ArenaSuccessMutation(string realmId)
            {
                AdvanceToRequest(realmId);
                NvsEncounterRequest request = Runtime.Snapshot.CurrentEncounter;
                var result = new NvsEncounterResult(
                    request.ContractVersion,
                    request.CorrelationId,
                    request.QuestId,
                    request.HookId,
                    request.RealmId,
                    NvsEncounterOutcome.Success,
                    request.SuccessEventId,
                    Nvs01ConsequenceContract.EncounterResultSnapshotVersion,
                    Nvs01ConsequenceContract.EncounterResultSnapshotReference);
                _committer.Clear();
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.ApplyEncounterResult(result).Status);
                return BindMutation(_committer.LastPlan);
            }

            internal Nvs01MutationPlan ReportMutation(string realmId)
            {
                ArenaSuccessMutation(realmId);
                return ReportMutationFromCurrent();
            }

            internal Nvs01MutationPlan ReportMutationFromCurrent()
            {
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.SelectValerius(
                        Command(
                            Nvs01ConsequenceContract.ValeriusNpcId,
                            "POST_REALM_PROLOGUE"),
                        Nvs01InteractionKind.Report,
                        new Nvs01RealmContext(
                            Nvs01RealmContextStatus.CommittedValid,
                            Runtime.Snapshot.CommittedRealmId)).Status);
                _committer.Clear();
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.SelectDialogueChoice(
                        Command(
                            "PLAYER",
                            Nvs01ConsequenceContract.ReportDialogueId),
                        "choice.omen1.present_tear").Status);
                return BindReportMutation(_committer.LastPlan);
            }

            private void AdvanceToRequest(string realmId)
            {
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.SelectValerius(
                        Command(
                            Nvs01ConsequenceContract.ValeriusNpcId,
                            "POST_REALM_PROLOGUE"),
                        Nvs01InteractionKind.Offer,
                        new Nvs01RealmContext(
                            Nvs01RealmContextStatus.CommittedValid,
                            realmId)).Status);
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.SelectDialogueChoice(
                        Command("PLAYER", "DLG_OMEN_1_OFFER"),
                        "choice.omen1.accept").Status);
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.SelectDialogueChoice(
                        Command("PLAYER", "DLG_OMEN_1_START"),
                        "choice.omen1.investigate").Status);
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.SelectDialogueChoice(
                        Command("PLAYER", "DLG_OMEN_1_GO"),
                        "choice.omen1.deploy").Status);
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    Runtime.InvokePendingSemanticAction(
                        Command("PLAYER", "REQUEST_SKY_CASTLE_ARENA"),
                        Capabilities(),
                        new Nvs01RealmContext(
                            Nvs01RealmContextStatus.CommittedValid,
                            realmId)).Status);
            }

            private Nvs01CommandEnvelope Command(string actorId, string contextId) =>
                new Nvs01CommandEnvelope(
                    Nvs01RuntimeContract.ContractVersion,
                    NextGuid(),
                    Nvs01ConsequenceContract.QuestId,
                    Runtime.Snapshot.StateId,
                    Runtime.Snapshot.Revision,
                    actorId,
                    contextId,
                    0);

            private static Nvs01MutationPlan BindMutation(Nvs01MutationPlan source)
            {
                ProfileAuthorityExpectation expectation =
                    ProfileAuthorityExpectation.From(Authority());
                Nvs01OperationReceipt operation = source.Candidate.LastOperation;
                var stamped = new Nvs01OperationReceipt(
                    operation.OperationId,
                    operation.PayloadFingerprint,
                    operation.Status,
                    operation.Revision,
                    operation.StateId,
                    operation.EventId,
                    operation.CorrelationId,
                    expectation.ExpectedGenerationFingerprint);
                return source.BindAuthority(
                    expectation,
                    CopyLastOperation(source.Candidate, stamped));
            }

            private static Nvs01MutationPlan BindReportMutation(
                Nvs01MutationPlan source)
            {
                Nvs01OperationReceipt prior = source.Expected.LastOperation;
                var stampedPrior = new Nvs01OperationReceipt(
                    prior.OperationId,
                    prior.PayloadFingerprint,
                    prior.Status,
                    prior.Revision,
                    prior.StateId,
                    prior.EventId,
                    prior.CorrelationId,
                    Fingerprint);
                var rebound = new Nvs01MutationPlan(
                    CopyLastOperation(source.Expected, stampedPrior),
                    source.Candidate,
                    source.TriggerEventId,
                    source.ConsequenceIntentIds.ToList());
                return BindMutation(rebound);
            }

            private static Nvs01QuestSnapshot CopyLastOperation(
                Nvs01QuestSnapshot source,
                Nvs01OperationReceipt operation)
            {
                return new Nvs01QuestSnapshot(
                    source.PacketVersion,
                    source.PacketSha256,
                    source.QuestId,
                    source.Revision,
                    source.StateId,
                    source.Objectives.ToArray(),
                    source.CurrentDialogueNodeId,
                    source.PendingChoice,
                    source.PendingSemanticActionId,
                    source.CommittedRealmId,
                    source.EncounterStatus,
                    source.CurrentEncounter,
                    source.LastEncounterCorrelationId,
                    source.LastEncounterOutcome,
                    source.LastEncounterEventId,
                    source.LastEncounterSnapshotVersion,
                    source.LastEncounterSnapshotReference,
                    operation,
                    source.ConsequenceIntentIds.ToArray());
            }

            private string NextGuid()
            {
                _guidCounter++;
                return "11111111-1111-4111-8111-" +
                       _guidCounter.ToString("x12");
            }
        }
    }
}
