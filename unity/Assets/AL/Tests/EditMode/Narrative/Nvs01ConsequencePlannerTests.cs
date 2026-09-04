using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AL.ChampionMode.Encounter;
using AL.Core.SaveAuthority;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01ConsequencePlannerTests
    {
        private const string ProfileId =
            "alp_11111111111111111111111111111111";
        private const string AlternateProfileId =
            "alp_22222222222222222222222222222222";
        private const string AuthorityEpoch =
            "11111111111111110000000000000001";
        private const string AlternateEpoch =
            "22222222222222220000000000000001";
        private const string Fingerprint =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AlternateFingerprint =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string ThirdFingerprint =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        private const string DependencyProviderId =
            "AL.TEST.NVS01.CONSEQUENCE.PROVIDER";
        private const string DependencyCatalogSetId =
            "another-life-test-authority";
        private const string DependencySourceFingerprint =
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

        private static Nvs01VerifiedCatalog _catalog;
        private static readonly ConsequencePlannerHarness
            Nvs01ConsequencePlanner = new ConsequencePlannerHarness();

        [Test]
        public void ContractPinsCanonicalV004ConsequenceOrderAndRealmMapping()
        {
            Assert.AreEqual(
                "omen1-a1-2026-08-13-v004",
                Nvs01ConsequenceContract.PacketVersion);
            Assert.AreEqual(
                "25a5170334fca571abe1035eacf448955e8eab1124ff08643f7d16be9a1b69dd",
                Nvs01ConsequenceContract.PacketSha256);
            Assert.AreEqual(8247, Nvs01ConsequenceContract.PacketByteLength);
            Assert.AreEqual(
                "oathmark",
                Nvs01ConsequenceContract.OathmarkTechnicalCurrencyId);
            Assert.AreEqual(
                500,
                Nvs01ConsequenceContract.OathmarkAmount);
            Assert.AreEqual(
                "RESOURCE_GOLD",
                Nvs01ConsequenceContract.CatalogGoldTargetId);
            Assert.AreEqual(
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                Nvs01ConsequenceContract.EncounterResultSnapshotVersion);
            Assert.AreEqual(
                ChampionEncounterSourceSet.CurrentSourceSetSha256,
                Nvs01ConsequenceContract.EncounterResultSnapshotReference);
            Assert.False(
                Nvs01ConsequenceContract.IsAuthoritativeOathmarkCurrency(
                    Nvs01ConsequenceContract.ForbiddenLegacyGoldResourceId));
            Assert.True(
                Nvs01ConsequenceContract.IsForbiddenCurrencySubstitution(
                    Nvs01ConsequenceContract.ForbiddenKingdomResourceId));
            CollectionAssert.AreEqual(
                new[]
                {
                    "ACQUIRE_CELESTIAL_TEAR",
                    "GRANT_GOLD_500",
                    "GRANT_VALERIUS_AFFINITY_5",
                    "COMPLETE_OMEN_1",
                    "UNLOCK_REALM_CHAPTER_1"
                },
                Nvs01ConsequenceContract.ExpectedConsequenceOrder);
            AssertReadOnly(
                Nvs01ConsequenceContract.ExpectedConsequenceOrder);

            Assert.AreEqual(
                "C1_CL",
                Nvs01ConsequenceContract.ChapterForRealm("crownlands"));
            Assert.AreEqual(
                "C1_SH",
                Nvs01ConsequenceContract.ChapterForRealm("stonehold"));
            Assert.AreEqual(
                "C1_EG",
                Nvs01ConsequenceContract.ChapterForRealm("eldergrove"));
            Assert.AreEqual(
                "C1_UM",
                Nvs01ConsequenceContract.ChapterForRealm("umbral"));
            Assert.AreEqual(
                string.Empty,
                Nvs01ConsequenceContract.ChapterForRealm("CROWNLANDS"));
        }

        [TestCase("crownlands")]
        [TestCase("stonehold")]
        [TestCase("eldergrove")]
        [TestCase("umbral")]
        public void RuntimeIssuedArenaMutationPlansOneTearOperation(
            string realmId)
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation(realmId);
            Nvs01ConsequencePlanningResult result =
                Nvs01ConsequencePlanner.Plan(Context(mutation));

            AssertReady(result, Nvs01ConsequencePlanKind.ArenaSuccess);
            Nvs01ConsequencePlan plan = result.Plan;
            string correlationId =
                mutation.Expected.CurrentEncounter.CorrelationId;
            Assert.AreEqual(
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                correlationId,
                plan.OperationId);
            Assert.AreEqual(ProfileId, plan.ProfileId);
            Assert.AreEqual(AuthorityEpoch, plan.AuthorityEpoch);
            Assert.AreEqual(Fingerprint, plan.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                mutation.Expected.Revision,
                plan.ExpectedQuestRevision);
            Assert.AreEqual(
                mutation.Candidate.Revision,
                plan.CandidateQuestRevision);
            Assert.AreEqual(realmId, plan.RealmId);
            Assert.AreEqual(correlationId, plan.CorrelationId);
            Assert.AreEqual(
                Nvs01ConsequenceContract.ReportStateId,
                plan.NextStateId);
            Assert.AreEqual(1000, plan.ResultingGoldBalance);
            Assert.AreEqual(10f, plan.ResultingValeriusAffinity);
            Assert.AreEqual("CH0_PROLOGUE", plan.ResultingChapterId);
            Assert.AreEqual(1, plan.Operations.Count);
            AssertOperation(
                plan.Operations[0],
                Nvs01ConsequenceContract.TearConsequenceId,
                Nvs01ConsequenceMutationKind.AcquireArtifact,
                Nvs01ConsequenceContract.TearArtifactId,
                0,
                Nvs01ConsequenceContract.TearArtifactId);
        }

        [TestCase("crownlands", "C1_CL")]
        [TestCase("stonehold", "C1_SH")]
        [TestCase("eldergrove", "C1_EG")]
        [TestCase("umbral", "C1_UM")]
        public void RuntimeIssuedReportMutationPlansOneAtomicFourEffectOperation(
            string realmId,
            string expectedChapterId)
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation(realmId);
            Nvs01ConsequencePlanningResult result =
                Nvs01ConsequencePlanner.Plan(Context(mutation));

            AssertReady(result, Nvs01ConsequencePlanKind.ReportCompletion);
            Nvs01ConsequencePlan plan = result.Plan;
            Assert.AreEqual(
                Nvs01ConsequenceContract.ReportOperationId,
                plan.OperationId);
            Assert.AreEqual(
                mutation.Expected.LastEncounterCorrelationId,
                plan.CorrelationId);
            Assert.AreEqual(
                Nvs01ConsequenceContract.CompletedStateId,
                plan.NextStateId);
            Assert.AreEqual(1500, plan.ResultingGoldBalance);
            Assert.AreEqual(15f, plan.ResultingValeriusAffinity);
            Assert.AreEqual(expectedChapterId, plan.ResultingChapterId);
            CollectionAssert.AreEqual(
                Nvs01ConsequenceContract.ExpectedConsequenceOrder.Skip(1),
                plan.Operations.Select(row => row.ConsequenceId));
            AssertOperation(
                plan.Operations[0],
                Nvs01ConsequenceContract.GoldConsequenceId,
                Nvs01ConsequenceMutationKind.CreditResource,
                Nvs01ConsequenceContract.OathmarkTechnicalCurrencyId,
                500,
                string.Empty);
            AssertOperation(
                plan.Operations[1],
                Nvs01ConsequenceContract.AffinityConsequenceId,
                Nvs01ConsequenceMutationKind.AdjustAffinity,
                Nvs01ConsequenceContract.ValeriusNpcId,
                5,
                string.Empty);
            AssertOperation(
                plan.Operations[2],
                Nvs01ConsequenceContract.CompletionConsequenceId,
                Nvs01ConsequenceMutationKind.CompleteQuest,
                Nvs01ConsequenceContract.QuestId,
                0,
                Nvs01ConsequenceContract.CompletedStateId);
            AssertOperation(
                plan.Operations[3],
                Nvs01ConsequenceContract.ChapterConsequenceId,
                Nvs01ConsequenceMutationKind.UnlockChapter,
                Nvs01ConsequenceContract.AbstractChapterId,
                0,
                expectedChapterId);
            AssertReadOnly(plan.Operations);
            Assert.AreEqual(
                Nvs01ConsequenceContract.OathmarkTechnicalCurrencyId,
                result.Plan.ApplicationReceipt.TechnicalCurrencyId);
        }

        [Test]
        public void LegacyGoldAndKingdomResourceSubstitutionFailClosed()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation("crownlands");

            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            technicalCurrencyId: Nvs01ConsequenceContract
                                .ForbiddenLegacyGoldResourceId))),
                Nvs01ConsequencePlanningStatus.RejectedDependencyMalformed,
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);

            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            technicalCurrencyId: Nvs01ConsequenceContract
                                .ForbiddenKingdomResourceId))),
                Nvs01ConsequencePlanningStatus.RejectedDependencyMalformed,
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);
        }

        [Test]
        public void CatalogOathmarkTargetForgeryFailsClosedAgainstV004Source()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01VerifiedCatalog forged =
                ForgedGoldTargetCatalog(
                    Nvs01ConsequenceContract.OathmarkTechnicalCurrencyId);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(mutation, catalog: forged)),
                Nvs01ConsequencePlanningStatus.RejectedContractMismatch,
                Nvs01ConsequenceDiagnosticCodes.ContractMismatch);
        }

        [Test]
        public void ArenaSuccessRejectsNonCatalogBackedEncounterResultIdentity()
        {
            Nvs01MutationPlan unbound =
                new RuntimeFixture().ArenaSuccessMutation(
                    "crownlands",
                    bindAuthority: false);
            Nvs01QuestSnapshot forgedCandidate = CopySnapshot(
                unbound.Candidate,
                snapshotVersion: "arena-v1",
                snapshotReference: "snapshot://authoritative");
            AssertTransitionRejected(
                BindMutation(
                    new Nvs01MutationPlan(
                        unbound.Expected,
                        forgedCandidate,
                        unbound.TriggerEventId,
                        unbound.ConsequenceIntentIds.ToList())));
        }

        [Test]
        public void MixedCatalogProfileEncounterAuthorityFailClosed()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01ConsequencePlanningContext baseline = Context(mutation);

            ProfileWriteAuthoritySnapshot schemaOne =
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    With(baseline, authority: schemaOne)),
                Nvs01ConsequencePlanningStatus.RejectedAuthorityUnavailable,
                Nvs01ConsequenceDiagnosticCodes.AuthorityUnavailable);

            var wrongHash = new Nvs01VerifiedCatalog(
                VerifiedCatalog().Catalog,
                Nvs01ConsequenceContract.PacketByteLength,
                AlternateFingerprint);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(mutation, catalog: wrongHash)),
                Nvs01ConsequencePlanningStatus.RejectedContractMismatch,
                Nvs01ConsequenceDiagnosticCodes.ContractMismatch);

            Nvs01MutationPlan unbound =
                new RuntimeFixture().ArenaSuccessMutation(
                    "crownlands",
                    bindAuthority: false);
            Nvs01QuestSnapshot mixedEncounter = CopySnapshot(
                unbound.Candidate,
                snapshotVersion: ChampionEncounterSourceSet
                    .CurrentSourceSetVersion,
                snapshotReference: AlternateFingerprint);
            AssertTransitionRejected(
                BindMutation(
                    new Nvs01MutationPlan(
                        unbound.Expected,
                        mixedEncounter,
                        unbound.TriggerEventId,
                        unbound.ConsequenceIntentIds.ToList())));
        }

        [Test]
        public void ReportAcceptsArenaReceiptFromPriorSaveGeneration()
        {
            ProfileWriteAuthoritySnapshot reportAuthority = Authority(
                fingerprint: AlternateFingerprint);
            Nvs01MutationPlan report = BindReportMutation(
                new RuntimeFixture().ReportMutation(
                    "crownlands",
                    bindAuthority: false),
                reportAuthority,
                Fingerprint);
            Nvs01ConsequenceDomainSnapshot domain = ReportDomain(report);

            Assert.AreEqual(
                Fingerprint,
                domain.ApplicationReceipts[0]
                    .ExpectedGenerationFingerprint);
            Assert.AreEqual(
                AlternateFingerprint,
                report.Candidate.LastOperation
                    .ExpectedGenerationFingerprint);

            Nvs01ConsequencePlanningResult result =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        report,
                        authority: reportAuthority,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                reportAuthority),
                        domain: domain));

            AssertReady(
                result,
                Nvs01ConsequencePlanKind.ReportCompletion);
            Assert.AreEqual(
                AlternateFingerprint,
                result.Plan.ApplicationReceipt
                    .ExpectedGenerationFingerprint);
        }

        [Test]
        public void ExactReplaysPreserveHistoricalReceiptsAcrossAtoBtoC()
        {
            AdvancedReceiptChain chain = BuildAdvancedReceiptChain();

            Assert.AreEqual(
                Fingerprint,
                chain.ArenaReplay.Expected.LastOperation
                    .ExpectedGenerationFingerprint);
            Assert.AreEqual(
                AlternateFingerprint,
                chain.ArenaReplay.Candidate.LastOperation
                    .ExpectedGenerationFingerprint);
            AssertAlreadyApplied(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ArenaReplay,
                        authority: chain.GenerationB,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationB),
                        domain: chain.ArenaApplied)));

            Assert.AreEqual(
                AlternateFingerprint,
                chain.ReportReplay.Expected.LastOperation
                    .ExpectedGenerationFingerprint);
            Assert.AreEqual(
                ThirdFingerprint,
                chain.ReportReplay.Candidate.LastOperation
                    .ExpectedGenerationFingerprint);
            Assert.AreEqual(
                chain.ArenaReceipt.PlanFingerprint,
                chain.ReportReceipt.PredecessorReceiptFingerprint);
            Assert.AreEqual(
                Fingerprint,
                chain.ReportReceipt
                    .PredecessorExpectedGenerationFingerprint);
            AssertAlreadyApplied(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportReplay,
                        authority: chain.GenerationC,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationC),
                        domain: chain.ReportApplied)));
        }

        [Test]
        public void NonReplayReportRecoveryRejectsJointlyReissuedChain()
        {
            AdvancedReceiptChain chain = BuildAdvancedReceiptChain();
            Nvs01ConsequenceDomainSnapshot validRecoveryDomain = CopyDomain(
                chain.BeforeReport,
                receipts: new[]
                {
                    chain.ArenaReceipt,
                    chain.ReportReceipt
                });
            Nvs01ConsequencePlanningResult valid =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportMutation,
                        authority: chain.GenerationB,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationB),
                        domain: validRecoveryDomain));
            AssertPartial(valid);
            AssertReceiptEqual(chain.ReportReceipt, valid.RecoveryReceipt);

            JointlyReissueReportChain(
                chain,
                out Nvs01ConsequenceApplicationReceipt forgedArena,
                out Nvs01ConsequenceApplicationReceipt forgedReport);
            Nvs01ConsequenceDomainSnapshot forgedRecoveryDomain = CopyDomain(
                chain.BeforeReport,
                receipts: new[] { forgedArena, forgedReport });

            AssertPartialWithoutRecovery(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportMutation,
                        authority: chain.GenerationB,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationB),
                        domain: forgedRecoveryDomain)));
        }

        [Test]
        public void ReplayReportRecoveryRejectsJointlyReissuedChain()
        {
            AdvancedReceiptChain chain = BuildAdvancedReceiptChain();
            string[] partialEffects = chain.ReportApplied.AppliedEffectKeys
                .Take(
                    chain.ReportApplied.AppliedEffectKeys.Count - 1)
                .ToArray();
            Nvs01ConsequenceDomainSnapshot validRecoveryDomain = CopyDomain(
                chain.ReportApplied,
                effects: partialEffects);
            Nvs01ConsequencePlanningResult valid =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportReplay,
                        authority: chain.GenerationC,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationC),
                        domain: validRecoveryDomain));
            AssertPartial(valid);
            AssertReceiptEqual(chain.ReportReceipt, valid.RecoveryReceipt);

            JointlyReissueReportChain(
                chain,
                out Nvs01ConsequenceApplicationReceipt forgedArena,
                out Nvs01ConsequenceApplicationReceipt forgedReport);
            Nvs01ConsequenceDomainSnapshot forgedRecoveryDomain = CopyDomain(
                chain.ReportApplied,
                effects: partialEffects,
                receipts: new[] { forgedArena, forgedReport });

            AssertPartialWithoutRecovery(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportReplay,
                        authority: chain.GenerationC,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationC),
                        domain: forgedRecoveryDomain)));
        }

        [Test]
        public void ControlCharacterDelimiterShiftRejectsBeforeHashing()
        {
            const string correlationId =
                "55555555-5555-4555-8555-555555555555";

            Assert.Throws<ArgumentException>(
                () => EncounterResult(
                    correlationId,
                    "arena\u001fsegment",
                    "snapshot"));
            Assert.Throws<ArgumentException>(
                () => EncounterResult(
                    correlationId,
                    "arena",
                    "segment\u001fsnapshot"));

            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01ConsequencePlanningResult result =
                Nvs01ConsequencePlanner.Plan(Context(mutation));
            AssertReady(result, Nvs01ConsequencePlanKind.ArenaSuccess);
            Assert.AreEqual(
                mutation.Candidate.LastOperation.PayloadFingerprint,
                result.Plan.ApplicationReceipt.CausalPayloadFingerprint);
        }

        [Test]
        public void ForgedHistoricalReceiptContextAndSuccessorAuthorityReject()
        {
            AdvancedReceiptChain chain = BuildAdvancedReceiptChain();
            const string otherOperation =
                "44444444-4444-4444-8444-444444444444";
            const string otherCorrelation =
                "55555555-5555-4555-8555-555555555555";

            foreach (Nvs01ConsequenceApplicationReceipt forged in new[]
                     {
                         ReissueReceipt(
                             chain.ReportReceipt,
                             causalOperationId: otherOperation),
                         ReissueReceipt(
                             chain.ReportReceipt,
                             causalPayloadFingerprint:
                                 DependencySourceFingerprint),
                         ReissueReceipt(
                             chain.ReportReceipt,
                             expectedGenerationFingerprint: Fingerprint),
                         ReissueReceipt(
                             chain.ReportReceipt,
                             predecessorExpectedGenerationFingerprint:
                                 ThirdFingerprint),
                         ReissueReceipt(
                             chain.ReportReceipt,
                             profileId: AlternateProfileId),
                         ReissueReceipt(
                             chain.ReportReceipt,
                             correlationId: otherCorrelation),
                         ReissueReceipt(
                             chain.ReportReceipt,
                             expectedQuestRevision:
                                 chain.ReportReceipt
                                     .ExpectedQuestRevision - 1)
                     })
            {
                AssertPartialWithoutRecovery(
                    Nvs01ConsequencePlanner.Plan(
                        Context(
                            chain.ReportReplay,
                            authority: chain.GenerationC,
                            expectedAuthority:
                                ProfileAuthorityExpectation.From(
                                    chain.GenerationC),
                            domain: CopyDomain(
                                chain.ReportApplied,
                                receipts: new[]
                                {
                                    chain.ArenaReceipt,
                                    forged
                                }))));
            }

            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportReplay,
                        authority: chain.GenerationC,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationC),
                        domain: CopyDomain(
                            chain.ReportApplied,
                            receipts: new[]
                            {
                                chain.ArenaReceipt,
                                ReissueReceipt(
                                    chain.ReportReceipt,
                                    realmId: "stonehold")
                            }))),
                Nvs01ConsequencePlanningStatus.RejectedDependencyMalformed,
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);

            foreach (Nvs01ConsequenceApplicationReceipt forgedArena in new[]
                     {
                         ReissueReceipt(
                             chain.ArenaReceipt,
                             causalOperationId: otherOperation),
                         ReissueReceipt(
                             chain.ArenaReceipt,
                             causalPayloadFingerprint:
                                 DependencySourceFingerprint)
                     })
            {
                AssertPartialWithoutRecovery(
                    Nvs01ConsequencePlanner.Plan(
                        Context(
                            chain.ReportMutation,
                            authority: chain.GenerationB,
                            expectedAuthority:
                                ProfileAuthorityExpectation.From(
                                    chain.GenerationB),
                            domain: CopyDomain(
                                chain.BeforeReport,
                                receipts: new[] { forgedArena }))));
            }

            Nvs01ConsequenceApplicationReceipt forgedArenaGeneration =
                ReissueReceipt(
                    chain.ArenaReceipt,
                    expectedGenerationFingerprint: ThirdFingerprint);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportMutation,
                        authority: chain.GenerationB,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationB),
                        domain: CopyDomain(
                            chain.BeforeReport,
                            receipts: new[] { forgedArenaGeneration }),
                        receiptAuthorities:
                            chain.BeforeReportAuthorities)),
                Nvs01ConsequencePlanningStatus.RejectedDependencyMalformed,
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportReplay,
                        authority: chain.GenerationC,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationC),
                        domain: CopyDomain(
                            chain.ReportApplied,
                            receipts: new[]
                            {
                                forgedArenaGeneration,
                                chain.ReportReceipt
                            }),
                        receiptAuthorities:
                            chain.ReportAppliedAuthorities)),
                Nvs01ConsequencePlanningStatus.RejectedDependencyMalformed,
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);

            AssertPartialWithoutRecovery(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportReplay,
                        authority: chain.GenerationC,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationC),
                        domain: CopyDomain(
                            chain.ReportApplied,
                            receipts: new[]
                            {
                                chain.ArenaReceipt,
                                ReissueReceipt(
                                    chain.ReportReceipt,
                                    predecessorReceiptFingerprint:
                                        ThirdFingerprint)
                            }))));

            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ArenaReplay,
                        authority: chain.GenerationC,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationC),
                        domain: chain.ArenaApplied)),
                Nvs01ConsequencePlanningStatus.RejectedStaleAuthority,
                Nvs01ConsequenceDiagnosticCodes.StaleAuthority);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        chain.ReportReplay,
                        authority: chain.GenerationB,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(
                                chain.GenerationB),
                        domain: chain.ReportApplied)),
                Nvs01ConsequencePlanningStatus.RejectedStaleAuthority,
                Nvs01ConsequenceDiagnosticCodes.StaleAuthority);
        }

        [TestCase("", "C1_CL", true)]
        [TestCase("C1", "C1_CL", true)]
        [TestCase("CH0_PROLOGUE", "C1_CL", true)]
        [TestCase("C_OMEN", "C1_CL", true)]
        [TestCase("C1_CL", "C1_CL", true)]
        [TestCase("C2_CL", "C2_CL", true)]
        [TestCase("C1_SH", "", false)]
        [TestCase("UNKNOWN", "", false)]
        [TestCase("FUTURE_CL", "", false)]
        public void ChapterCompatibilityMatrixIsFailClosedAndNonRegressing(
            string currentChapterId,
            string expectedChapterId,
            bool expectedReady)
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation("crownlands");
            Nvs01ConsequenceDomainSnapshot domain = ReportDomain(
                mutation,
                currentChapterId);
            Nvs01ConsequencePlanningResult result =
                Nvs01ConsequencePlanner.Plan(
                    Context(mutation, domain: domain));

            if (expectedReady)
            {
                AssertReady(
                    result,
                    Nvs01ConsequencePlanKind.ReportCompletion);
                Assert.AreEqual(
                    expectedChapterId,
                    result.Plan.ResultingChapterId);
            }
            else
            {
                AssertRejected(
                    result,
                    Nvs01ConsequencePlanningStatus
                        .RejectedChapterIncompatible,
                    Nvs01ConsequenceDiagnosticCodes.ChapterIncompatible);
            }
        }

        [Test]
        public void MissingDuplicateWrongRealmAndForwardChapterAuthorityRejects()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation("crownlands");

            var missing = ChapterRows()
                .Where(row => row.ChapterId != "C1_CL")
                .ToList();
            AssertChapterRejected(mutation, ChapterAuthority(missing));

            var duplicateId = ChapterRows();
            duplicateId.Add(
                new Nvs01ChapterReference(
                    "C1_CL", "crownlands", 2, false));
            AssertChapterRejected(mutation, ChapterAuthority(duplicateId));

            var duplicateOrder = ChapterRows();
            duplicateOrder.Add(
                new Nvs01ChapterReference(
                    "OTHER_CL", "crownlands", 1, false));
            AssertChapterRejected(mutation, ChapterAuthority(duplicateOrder));

            var wrongRealm = ChapterRows();
            wrongRealm[0] = new Nvs01ChapterReference(
                "C1_CL", "stonehold", 1, false);
            AssertChapterRejected(mutation, ChapterAuthority(wrongRealm));

            var forward = ChapterRows();
            forward[0] = new Nvs01ChapterReference(
                "C1_CL", "crownlands", 1, true);
            AssertChapterRejected(mutation, ChapterAuthority(forward));

            var nullRow = ChapterRows();
            nullRow.Add(null);
            AssertChapterRejected(mutation, ChapterAuthority(nullRow));
        }

        [Test]
        public void RuntimeTopologyCorruptionAndTriggerRelabelingReject()
        {
            Nvs01MutationPlan valid =
                new RuntimeFixture().ArenaSuccessMutation(
                    "crownlands",
                    bindAuthority: false);
            Nvs01QuestSnapshot candidateWithoutObjectives = CopySnapshot(
                valid.Candidate,
                objectives: new List<Nvs01ObjectiveSnapshot>());
            var corrupted = new Nvs01MutationPlan(
                valid.Expected,
                candidateWithoutObjectives,
                valid.TriggerEventId,
                valid.ConsequenceIntentIds.ToList());
            AssertTransitionRejected(BindMutation(corrupted));

            NvsEncounterRequest request = valid.Expected.CurrentEncounter;
            var wrongRoute = new NvsEncounterRequest(
                request.ContractVersion,
                request.RequestId,
                request.CorrelationId,
                request.QuestId,
                request.StateId,
                request.ObjectiveId,
                request.HookId,
                request.LocationId,
                request.RealmId,
                request.SuccessEventId,
                request.FailureEventId,
                request.CancelledEventId,
                request.UnavailableEventId,
                "OtherScene");
            Nvs01QuestSnapshot expectedWrongRoute = CopySnapshot(
                valid.Expected,
                currentEncounter: wrongRoute,
                replaceEncounter: true);
            var routeCorruption = new Nvs01MutationPlan(
                expectedWrongRoute,
                valid.Candidate,
                valid.TriggerEventId,
                valid.ConsequenceIntentIds.ToList());
            AssertTransitionRejected(BindMutation(routeCorruption));

            var relabeled = new Nvs01MutationPlan(
                valid.Expected,
                valid.Candidate,
                Nvs01ConsequenceContract.ReportConclusionEventId,
                valid.ConsequenceIntentIds.ToList());
            AssertTransitionRejected(BindMutation(relabeled));
        }

        [Test]
        public void CanonicalCatalogProvenanceRejectsHashAndConsequenceForgery()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            var wrongHash = new Nvs01VerifiedCatalog(
                VerifiedCatalog().Catalog,
                Nvs01ConsequenceContract.PacketByteLength,
                AlternateFingerprint);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(mutation, catalog: wrongHash)),
                Nvs01ConsequencePlanningStatus.RejectedContractMismatch,
                Nvs01ConsequenceDiagnosticCodes.ContractMismatch);

            Nvs01VerifiedCatalog forgedTopology =
                ForgedConsequenceCatalog("OTHER_ARTIFACT");
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(mutation, catalog: forgedTopology)),
                Nvs01ConsequencePlanningStatus.RejectedContractMismatch,
                Nvs01ConsequenceDiagnosticCodes.ContractMismatch);
        }

        [Test]
        public void AuthorityIdentityEpochFingerprintAndRevisionDriftReject()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01ConsequencePlanningContext baseline = Context(mutation);
            ProfileWriteAuthoritySnapshot migration =
                ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    With(baseline, authority: migration)),
                Nvs01ConsequencePlanningStatus
                    .RejectedAuthorityUnavailable,
                Nvs01ConsequenceDiagnosticCodes.AuthorityUnavailable);

            foreach (ProfileWriteAuthoritySnapshot stale in new[]
                     {
                         Authority(AlternateProfileId),
                         Authority(epoch: AlternateEpoch),
                         Authority(fingerprint: AlternateFingerprint)
                     })
            {
                AssertRejected(
                    Nvs01ConsequencePlanner.Plan(
                        With(
                            baseline,
                            expectedAuthority:
                                ProfileAuthorityExpectation.From(stale))),
                    Nvs01ConsequencePlanningStatus.RejectedStaleAuthority,
                    Nvs01ConsequenceDiagnosticCodes.StaleAuthority);
            }

            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    With(
                        baseline,
                        expectedQuestRevision:
                            mutation.Expected.Revision - 1)),
                Nvs01ConsequencePlanningStatus
                    .RejectedStaleQuestRevision,
                    Nvs01ConsequenceDiagnosticCodes.StaleQuestRevision);
        }

        [Test]
        public void DependencyAuthorityRejectsPlausibleCopiesAndForeignIssuer()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01ConsequencePlanningContext baseline = Context(mutation);
            Nvs01VerifiedConsequenceDependencies trusted =
                baseline.Dependencies;
            var copied = new Nvs01VerifiedConsequenceDependencies(
                trusted.ProviderIdentity,
                trusted.CatalogId,
                trusted.PacketVersion,
                trusted.PacketSha256,
                trusted.PacketByteLength,
                trusted.ProfileId,
                trusted.ExpectedGenerationFingerprint,
                trusted.Capabilities,
                trusted.Domain,
                trusted.Chapters,
                trusted.ReceiptAuthorities,
                trusted.AuthorityFingerprint);
            AssertDependencyAuthorityRejected(
                Nvs01ConsequencePlanner,
                WithDependencies(baseline, copied));

            var foreign = new ConsequencePlannerHarness();
            Nvs01VerifiedConsequenceDependencies foreignDependencies =
                foreign.Capture(
                    VerifiedCatalog(),
                    ProfileId,
                    Fingerprint,
                    baseline.Capabilities,
                    baseline.Domain,
                    baseline.Chapters);
            AssertDependencyAuthorityRejected(
                Nvs01ConsequencePlanner,
                WithDependencies(baseline, foreignDependencies));
        }

        [Test]
        public void DependencyAuthorityRejectsStaleMetadataAndCapabilitySet()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01ConsequencePlanningContext baseline = Context(mutation);
            var provider = new TestDependencyProvider();
            var harness = new ConsequencePlannerHarness(provider);
            Nvs01VerifiedConsequenceDependencies captured = harness.Capture(
                VerifiedCatalog(),
                ProfileId,
                Fingerprint,
                baseline.Capabilities,
                baseline.Domain,
                baseline.Chapters);
            Nvs01ConsequencePlanningContext issuedContext =
                WithDependencies(baseline, captured);

            foreach (Nvs01ConsequenceDependencyProviderIdentity drift in new[]
                     {
                         DependencyIdentity(contractVersion: 2),
                         DependencyIdentity(providerId: "OTHER_PROVIDER"),
                         DependencyIdentity(catalogSetId: "other-set"),
                         DependencyIdentity(contentVersion: "game-data-test-v2"),
                         DependencyIdentity(sourceRevision: "source-test-v2"),
                         DependencyIdentity(
                             sourceFingerprint: AlternateFingerprint),
                         DependencyIdentity(providerRevision: 2)
                     })
            {
                provider.SetIdentity(drift);
                AssertDependencyAuthorityRejected(harness, issuedContext);
            }

            provider.SetIdentity(DependencyIdentity());
            var missingValues = new Dictionary<string, bool>(
                baseline.Capabilities.Availability,
                StringComparer.Ordinal);
            missingValues.Remove(Nvs01ConsequenceContract.TearArtifactId);
            var extraValues = new Dictionary<string, bool>(
                baseline.Capabilities.Availability,
                StringComparer.Ordinal)
            {
                ["FAKE_CAPABILITY"] = true
            };
            foreach (Nvs01CapabilitySnapshot capabilities in new[]
                     {
                         new Nvs01CapabilitySnapshot(missingValues),
                         new Nvs01CapabilitySnapshot(extraValues)
                     })
            {
                provider.Configure(
                    capabilities,
                    baseline.Domain,
                    baseline.Chapters);
                var authority = new Nvs01ConsequenceDependencyAuthority(
                    provider,
                    DependencyProviderId,
                    DependencyCatalogSetId);
                Assert.False(
                    authority.TryCapture(
                        VerifiedCatalog(),
                        ProfileId,
                        Fingerprint,
                        out Nvs01VerifiedConsequenceDependencies rejected));
                Assert.IsNull(rejected);
            }
        }

        [Test]
        public void DependencyAuthorityRejectsOversizedCaptureBeforeIssuance()
        {
            string oversized = new string(
                'X',
                Nvs01RuntimeContract.MaximumIdentifierLength + 1);
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            var provider = new TestDependencyProvider();
            var authority = new Nvs01ConsequenceDependencyAuthority(
                provider,
                DependencyProviderId,
                DependencyCatalogSetId);

            provider.Configure(
                Capabilities(),
                ArenaDomain(currentChapterId: oversized),
                ChapterAuthority(ChapterRows()));
            Assert.False(
                authority.TryCapture(
                    VerifiedCatalog(),
                    ProfileId,
                    Fingerprint,
                    out Nvs01VerifiedConsequenceDependencies domainRejected));
            Assert.IsNull(domainRejected);

            provider.Configure(
                Capabilities(),
                ArenaDomain(),
                ChapterAuthority(
                    new[]
                    {
                        new Nvs01ChapterReference(
                            oversized,
                            "crownlands",
                            1,
                            false)
                    }));
            Assert.False(
                authority.TryCapture(
                    VerifiedCatalog(),
                    ProfileId,
                    Fingerprint,
                    out Nvs01VerifiedConsequenceDependencies chapterRejected));
            Assert.IsNull(chapterRejected);

            Nvs01ConsequenceDomainSnapshot applied =
                ArenaAppliedDomain(mutation);
            provider.Configure(
                Capabilities(),
                CopyDomain(
                    applied,
                    receipts: new[]
                    {
                        ReissueReceipt(
                            applied.ApplicationReceipts[0],
                            causalPayloadFingerprint: oversized)
                    }),
                ChapterAuthority(ChapterRows()));
            Assert.False(
                authority.TryCapture(
                    VerifiedCatalog(),
                    ProfileId,
                    Fingerprint,
                    out Nvs01VerifiedConsequenceDependencies receiptRejected));
            Assert.IsNull(receiptRejected);
        }

        [Test]
        public void UnboundArenaReportAndReplayPlansRejectAuthority()
        {
            Nvs01MutationPlan arena =
                new RuntimeFixture().ArenaSuccessMutation(
                    "crownlands",
                    bindAuthority: false);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(Context(arena)),
                Nvs01ConsequencePlanningStatus.RejectedStaleAuthority,
                Nvs01ConsequenceDiagnosticCodes.StaleAuthority);

            Nvs01MutationPlan report =
                new RuntimeFixture().ReportMutation(
                    "crownlands",
                    bindAuthority: false);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(Context(report)),
                Nvs01ConsequencePlanningStatus.RejectedStaleAuthority,
                Nvs01ConsequenceDiagnosticCodes.StaleAuthority);

            Nvs01MutationPlan bound =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01MutationPlan replay =
                Nvs01MutationPlan.ForExactReplay(bound.Candidate);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(Context(replay)),
                Nvs01ConsequencePlanningStatus.RejectedStaleAuthority,
                Nvs01ConsequenceDiagnosticCodes.StaleAuthority);
        }

        [Test]
        public void BoundForgedCausalOperationAndFingerprintRejectEveryPath()
        {
            Nvs01MutationPlan arena =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            AssertTransitionRejected(
                ForgeMutation(
                    arena,
                    operationId:
                        "22222222-2222-4222-8222-222222222222"));
            Nvs01MutationPlan forgedArenaFingerprint = ForgeMutation(
                arena,
                payloadFingerprint: AlternateFingerprint);
            AssertTransitionRejected(forgedArenaFingerprint);
            AssertTransitionRejected(
                BindMutation(
                    Nvs01MutationPlan.ForExactReplay(
                        forgedArenaFingerprint.Candidate)));

            Nvs01MutationPlan report =
                new RuntimeFixture().ReportMutation("crownlands");
            Nvs01ConsequenceReceiptExpectation trusted =
                ReceiptExpectationFor(report);
            Nvs01MutationPlan forgedReportOperation = ForgeMutation(
                report,
                operationId: "33333333-3333-4333-8333-333333333333");
            AssertTransitionRejected(
                forgedReportOperation,
                trusted);
            Nvs01MutationPlan forgedReportFingerprint = ForgeMutation(
                report,
                payloadFingerprint: AlternateFingerprint);
            AssertTransitionRejected(
                forgedReportFingerprint,
                trusted);
            AssertTransitionRejected(
                BindMutation(
                    Nvs01MutationPlan.ForExactReplay(
                        forgedReportOperation.Candidate)),
                trusted);
        }

        [TestCase(
            "Unavailable",
            "RejectedDependencyUnavailable")]
        [TestCase(
            "Missing",
            "RejectedDependencyUnavailable")]
        [TestCase(
            "Duplicate",
            "RejectedDependencyMalformed")]
        [TestCase(
            "Malformed",
            "RejectedDependencyMalformed")]
        public void TypedDefinitionFailuresNeverPlan(
            string dependencyStatusName,
            string expectedStatusName)
        {
            Nvs01ConsequenceDependencyStatus dependencyStatus =
                (Nvs01ConsequenceDependencyStatus)Enum.Parse(
                    typeof(Nvs01ConsequenceDependencyStatus),
                    dependencyStatusName);
            Nvs01ConsequencePlanningStatus expectedStatus =
                (Nvs01ConsequencePlanningStatus)Enum.Parse(
                    typeof(Nvs01ConsequencePlanningStatus),
                    expectedStatusName);

            Nvs01MutationPlan arena =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01ConsequenceDomainSnapshot arenaDomain = ArenaDomain(
                artifactStatus: dependencyStatus);
            AssertDependencyRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(arena, domain: arenaDomain)),
                expectedStatus);

            Nvs01MutationPlan report =
                new RuntimeFixture().ReportMutation("crownlands");
            Nvs01ConsequenceDomainSnapshot reportDomain = ReportDomain(
                report,
                goldStatus: dependencyStatus);
            AssertDependencyRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(report, domain: reportDomain)),
                expectedStatus);
            reportDomain = ReportDomain(
                report,
                affinityStatus: dependencyStatus);
            AssertDependencyRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(report, domain: reportDomain)),
                expectedStatus);
        }

        [Test]
        public void MissingCapabilitiesAndIncompleteChapterAuthorityReject()
        {
            Nvs01MutationPlan arena =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        arena,
                        capabilities: Capabilities(
                            Nvs01ConsequenceContract.TearArtifactId))),
                Nvs01ConsequencePlanningStatus
                    .RejectedDependencyUnavailable,
                Nvs01ConsequenceDiagnosticCodes.DependencyUnavailable);

            Nvs01MutationPlan report =
                new RuntimeFixture().ReportMutation("crownlands");
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        report,
                        capabilities: Capabilities(
                            Nvs01ConsequenceContract.AbstractChapterId))),
                Nvs01ConsequencePlanningStatus
                    .RejectedDependencyUnavailable,
                Nvs01ConsequenceDiagnosticCodes.DependencyUnavailable);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        report,
                        chapters: new Nvs01ChapterAuthoritySnapshot(
                            Nvs01ConsequenceDependencyStatus.Available,
                            false,
                            ChapterRows()))),
                Nvs01ConsequencePlanningStatus
                    .RejectedDependencyUnavailable,
                Nvs01ConsequenceDiagnosticCodes.DependencyUnavailable);
        }

        [Test]
        public void MalformedDomainAndBoundOverrunsRejectDeterministically()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            AssertDomainMalformed(mutation, ArenaDomain(gold: -1));
            AssertDomainMalformed(mutation, ArenaDomain(affinity: float.NaN));
            AssertDomainMalformed(
                mutation,
                ArenaDomain(affinity: float.PositiveInfinity));
            AssertDomainMalformed(mutation, ArenaDomain(affinity: 101));
            AssertDomainMalformed(
                mutation,
                ArenaDomain(currentChapterId: " bad "));
            AssertDomainMalformed(
                mutation,
                new Nvs01ConsequenceDomainSnapshot(
                    Nvs01ConsequenceDependencyStatus.Available,
                    Nvs01ConsequenceDependencyStatus.Available,
                    Nvs01ConsequenceDependencyStatus.Available,
                    0,
                    0,
                    string.Empty,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<Nvs01ConsequenceApplicationReceipt>()));
            AssertDomainMalformed(
                mutation,
                ArenaDomain(
                    artifacts: new[] { "A", "A" }));
            AssertDomainMalformed(
                mutation,
                ArenaDomain(
                    operations: new[] { "A", "A" }));

            var oversized = new List<string>();
            for (int index = 0;
                 index <=
                 Nvs01ConsequenceContract.MaximumAppliedOperationCount;
                 index++)
            {
                oversized.Add("OP_" + index);
            }
            AssertCaptureRejected(
                ArenaDomain(operations: oversized));

            var oversizedChapters = ChapterRows();
            while (oversizedChapters.Count <=
                   Nvs01ConsequenceContract.MaximumChapterDefinitionCount)
            {
                int index = oversizedChapters.Count;
                oversizedChapters.Add(
                    new Nvs01ChapterReference(
                        "EXTRA_" + index,
                        "crownlands",
                        index + 1,
                        false));
            }
            AssertCaptureRejected(
                ArenaDomain(),
                ChapterAuthority(oversizedChapters));
        }

        [Test]
        public void ExactReplayIsNoOpAndPartialLedgerStateRejects()
        {
            Nvs01MutationPlan arena =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01MutationPlan arenaReplay =
                BindMutation(
                    Nvs01MutationPlan.ForExactReplay(arena.Candidate));
            Nvs01ConsequencePlanningResult arenaResult =
                Nvs01ConsequencePlanner.Plan(Context(arenaReplay));
            AssertAlreadyApplied(arenaResult);

            Nvs01ConsequenceDomainSnapshot arenaPartial = ArenaDomain(
                artifacts: Array.Empty<string>(),
                operations: new[]
                {
                    Nvs01ConsequenceContract.ArenaOperationPrefix +
                    arena.Candidate.LastEncounterCorrelationId
                });
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(arenaReplay, domain: arenaPartial)),
                Nvs01ConsequencePlanningStatus.RejectedPartialApplication,
                Nvs01ConsequenceDiagnosticCodes.PartialApplication);

            Nvs01MutationPlan report =
                new RuntimeFixture().ReportMutation("crownlands");
            Nvs01MutationPlan reportReplay =
                BindMutation(
                    Nvs01MutationPlan.ForExactReplay(report.Candidate));
            AssertAlreadyApplied(
                Nvs01ConsequencePlanner.Plan(Context(reportReplay)));

            Nvs01ConsequenceDomainSnapshot reportPartial = ReportDomain(
                reportReplay,
                operations: new[]
                {
                    Nvs01ConsequenceContract.ArenaOperationPrefix +
                    report.Candidate.LastEncounterCorrelationId
                });
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(reportReplay, domain: reportPartial)),
                Nvs01ConsequencePlanningStatus.RejectedPartialApplication,
                Nvs01ConsequenceDiagnosticCodes.PartialApplication);
        }

        [Test]
        public void EffectLedgerAndApplicationReceiptAreAllOrNone()
        {
            Nvs01MutationPlan arena =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            AssertPartial(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        arena,
                        domain: ArenaDomain(
                            effects: new[]
                            {
                                Nvs01ConsequenceContract.TearConsequenceId
                            }))));

            Nvs01MutationPlan report =
                new RuntimeFixture().ReportMutation("crownlands");
            Nvs01ConsequenceDomainSnapshot beforeReport =
                ReportDomain(report);
            AssertPartial(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        report,
                        domain: CopyDomain(
                            beforeReport,
                            effects: Array.Empty<string>()))));
            AssertPartial(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        report,
                        domain: CopyDomain(
                            beforeReport,
                            effects: new[]
                            {
                                Nvs01ConsequenceContract.TearConsequenceId,
                                Nvs01ConsequenceContract.GoldConsequenceId
                            }))));

            Nvs01MutationPlan replay = BindMutation(
                Nvs01MutationPlan.ForExactReplay(report.Candidate));
            Nvs01ConsequenceDomainSnapshot complete =
                ReportAppliedDomain(replay);
            AssertAlreadyApplied(
                Nvs01ConsequencePlanner.Plan(
                    Context(replay, domain: complete)));

            foreach (string effect in
                     Nvs01ConsequenceContract.ExpectedConsequenceOrder)
            {
                string[] partialEffects = complete.AppliedEffectKeys
                    .Where(value => !string.Equals(
                        value,
                        effect,
                        StringComparison.Ordinal))
                    .ToArray();
                Nvs01ConsequencePlanningResult partial =
                    Nvs01ConsequencePlanner.Plan(
                        Context(
                            replay,
                            domain: CopyDomain(
                                complete,
                                effects: partialEffects)));
                AssertPartial(partial);
                Assert.NotNull(partial.RecoveryReceipt);
                Assert.AreEqual(
                    Nvs01ConsequenceContract.ReportOperationId,
                    partial.RecoveryReceipt.OperationId);
            }

            Nvs01ConsequenceDomainSnapshot laterLegitimateValues =
                CopyDomain(
                    complete,
                    gold: complete.GoldBalance + 250,
                    affinity: complete.ValeriusAffinity - 3);
            AssertAlreadyApplied(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        replay,
                        domain: laterLegitimateValues)));

            Nvs01ConsequenceApplicationReceipt reportReceipt =
                complete.ApplicationReceipts[1];
            Nvs01ConsequenceApplicationReceipt corrupted =
                CloneReceipt(
                    reportReceipt,
                    planFingerprint: AlternateFingerprint);
            AssertDomainMalformed(
                replay,
                CopyDomain(
                    complete,
                    receipts: new[]
                    {
                        complete.ApplicationReceipts[0],
                        corrupted
                    }));
        }

        [Test]
        public void PartialRecoveryNeverSurfacesContextMismatchedReceipt()
        {
            Nvs01MutationPlan arena =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            string correlationId =
                arena.Candidate.LastEncounterCorrelationId;
            string arenaOperationId =
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                correlationId;
            Nvs01ConsequenceApplicationReceipt arenaReceipt = ArenaReceipt(
                "crownlands",
                correlationId,
                arena.Candidate.Revision,
                1000,
                10,
                "CH0_PROLOGUE");
            Nvs01ConsequenceDomainSnapshot arenaApplied = ArenaDomain(
                artifacts: new[]
                {
                    Nvs01ConsequenceContract.TearArtifactId
                },
                operations: new[] { arenaOperationId },
                effects: new[]
                {
                    Nvs01ConsequenceContract.TearConsequenceId
                },
                receipts: new[]
                {
                    ReissueReceipt(
                        arenaReceipt,
                        profileId: AlternateProfileId)
                });
            AssertPartialWithoutRecovery(
                Nvs01ConsequencePlanner.Plan(
                    Context(arena, domain: arenaApplied)));

            Nvs01MutationPlan report =
                new RuntimeFixture().ReportMutation("crownlands");
            Nvs01ConsequenceDomainSnapshot beforeReport = ReportDomain(report);
            AssertPartialWithoutRecovery(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        report,
                        domain: CopyDomain(
                            beforeReport,
                            receipts: new[]
                            {
                                ReissueReceipt(
                                    beforeReport.ApplicationReceipts[0],
                                    realmId: "stonehold")
                            }))));

            Nvs01MutationPlan arenaReplay = BindMutation(
                Nvs01MutationPlan.ForExactReplay(arena.Candidate));
            Nvs01ConsequenceDomainSnapshot arenaReplayDomain =
                ArenaAppliedDomain(arenaReplay);
            AssertPartialWithoutRecovery(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        arenaReplay,
                        domain: CopyDomain(
                            arenaReplayDomain,
                            receipts: new[]
                            {
                                ReissueReceipt(
                                    arenaReplayDomain.ApplicationReceipts[0],
                                    expectedQuestRevision:
                                        arenaReplay.Candidate.Revision)
                            }))));

            Nvs01MutationPlan reportReplay = BindMutation(
                Nvs01MutationPlan.ForExactReplay(report.Candidate));
            Nvs01ConsequenceDomainSnapshot reportReplayDomain =
                ReportAppliedDomain(reportReplay);
            foreach (Nvs01ConsequenceApplicationReceipt mismatched in new[]
                     {
                         ReissueReceipt(
                             reportReplayDomain.ApplicationReceipts[1],
                             correlationId:
                                 "44444444-4444-4444-8444-444444444444"),
                         ReissueReceipt(
                             reportReplayDomain.ApplicationReceipts[1],
                             expectedGenerationFingerprint:
                                 AlternateFingerprint)
                     })
            {
                AssertPartialWithoutRecovery(
                    Nvs01ConsequencePlanner.Plan(
                        Context(
                            reportReplay,
                            domain: CopyDomain(
                                reportReplayDomain,
                                receipts: new[]
                                {
                                    reportReplayDomain
                                        .ApplicationReceipts[0],
                                    mismatched
                                }))));
            }
        }

        [Test]
        public void GoldAndAffinityArithmeticIsCheckedWithoutClamping()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation("crownlands");
            Nvs01ConsequencePlanningResult maximum =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            gold: long.MaxValue - 500,
                            affinity: 95)));
            AssertReady(maximum, Nvs01ConsequencePlanKind.ReportCompletion);
            Assert.AreEqual(
                long.MaxValue,
                maximum.Plan.ResultingGoldBalance);
            Assert.AreEqual(
                100f,
                maximum.Plan.ResultingValeriusAffinity);

            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            gold: long.MaxValue - 499))),
                Nvs01ConsequencePlanningStatus.RejectedOverflow,
                Nvs01ConsequenceDiagnosticCodes.Overflow);
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            affinity: 95.0001f))),
                Nvs01ConsequencePlanningStatus.RejectedOverflow,
                Nvs01ConsequenceDiagnosticCodes.Overflow);
        }

        [Test]
        public void CapturedInputsAndRepeatedPlansAreImmutableAndDeterministic()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation("crownlands");
            var artifacts = new List<string>
            {
                Nvs01ConsequenceContract.TearArtifactId
            };
            var operations = new List<string>
            {
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                mutation.Expected.LastEncounterCorrelationId
            };
            var chapterRows = ChapterRows();
            var domain = ReportDomain(
                mutation,
                artifacts: artifacts,
                operations: operations);
            var chapters = ChapterAuthority(chapterRows);
            Nvs01ConsequencePlanningContext context = Context(
                mutation,
                domain: domain,
                chapters: chapters);

            artifacts.Clear();
            operations.Clear();
            chapterRows.Clear();

            Nvs01ConsequencePlanningResult first =
                Nvs01ConsequencePlanner.Plan(context);
            Nvs01ConsequencePlanningResult second =
                Nvs01ConsequencePlanner.Plan(context);
            AssertReady(first, Nvs01ConsequencePlanKind.ReportCompletion);
            AssertReady(second, Nvs01ConsequencePlanKind.ReportCompletion);
            AssertPlanEqual(first.Plan, second.Plan);
            AssertReadOnly(domain.AcquiredArtifactIds);
            AssertReadOnly(domain.AppliedOperationIds);
            AssertReadOnly(domain.AppliedEffectKeys);
            AssertReadOnly(domain.ApplicationReceipts);
            AssertReadOnly(chapters.Chapters);
            AssertReadOnly(context.ReceiptAuthorities.Entries);
            AssertReadOnly(first.Plan.Operations);
            AssertReadOnly(first.Plan.ApplicationReceipt.EffectKeys);

            var reversedRows = ChapterRows();
            reversedRows.Reverse();
            Nvs01ConsequencePlanningResult reversed =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            currentChapterId: "C2_CL"),
                        chapters: ChapterAuthority(reversedRows)));
            Nvs01ConsequencePlanningResult forward =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            currentChapterId: "C2_CL"),
                        chapters: ChapterAuthority(ChapterRows())));
            AssertPlanEqual(forward.Plan, reversed.Plan);
        }

        [Test]
        public void PlannerHasNoPublicReceiptRelabelOrUnityObjectSurface()
        {
            Assert.False(typeof(Nvs01ConsequencePlanner).IsPublic);
            Assert.Zero(
                typeof(Nvs01ConsequencePlanningContext)
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .Length);
            Assert.Zero(
                typeof(Nvs01ConsequenceDomainSnapshot)
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .Length);
            Assert.Zero(
                typeof(Nvs01ChapterAuthoritySnapshot)
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .Length);
            MethodInfo entry =
                typeof(AL.Narrative.Nvs01.Nvs01ConsequencePlanner).GetMethod(
                "Plan",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(entry);
            CollectionAssert.AreEqual(
                new[] { typeof(Nvs01ConsequencePlanningContext) },
                entry.GetParameters().Select(parameter => parameter.ParameterType));

            foreach (Type type in new[]
                     {
                         typeof(Nvs01ConsequencePlanningContext),
                         typeof(Nvs01VerifiedConsequenceDependencies),
                         typeof(Nvs01ConsequenceDependencyProviderIdentity),
                         typeof(Nvs01ConsequenceDependencyProviderCapture),
                         typeof(Nvs01ConsequenceReceiptAuthoritySnapshot),
                         typeof(Nvs01ConsequenceReceiptAuthorityEntry),
                         typeof(Nvs01ConsequenceReceiptExpectation),
                         typeof(Nvs01ConsequenceDomainSnapshot),
                         typeof(Nvs01ChapterAuthoritySnapshot),
                         typeof(Nvs01ConsequenceApplicationReceipt),
                         typeof(Nvs01ConsequencePlan),
                         typeof(Nvs01ConsequenceOperation),
                         typeof(Nvs01ConsequencePlanningResult)
                     })
            {
                Assert.False(typeof(UnityEngine.Object).IsAssignableFrom(type));
                Assert.False(
                    type.GetProperties(
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.Instance)
                        .Any(property => property.SetMethod != null),
                    type.FullName + " must remain immutable.");
            }
        }

        [Test]
        public void EveryMissingContextInputRejectsWithoutThrowing()
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ArenaSuccessMutation("crownlands");
            Nvs01ConsequencePlanningContext baseline = Context(mutation);
            var missing = new[]
            {
                new Nvs01ConsequencePlanningContext(
                    null,
                    baseline.QuestMutation,
                    baseline.Authority,
                    baseline.ExpectedAuthority,
                    baseline.ExpectedQuestRevision,
                    baseline.ReceiptExpectation,
                    baseline.Dependencies),
                new Nvs01ConsequencePlanningContext(
                    baseline.Catalog,
                    null,
                    baseline.Authority,
                    baseline.ExpectedAuthority,
                    baseline.ExpectedQuestRevision,
                    baseline.ReceiptExpectation,
                    baseline.Dependencies),
                new Nvs01ConsequencePlanningContext(
                    baseline.Catalog,
                    baseline.QuestMutation,
                    null,
                    baseline.ExpectedAuthority,
                    baseline.ExpectedQuestRevision,
                    baseline.ReceiptExpectation,
                    baseline.Dependencies),
                new Nvs01ConsequencePlanningContext(
                    baseline.Catalog,
                    baseline.QuestMutation,
                    baseline.Authority,
                    null,
                    baseline.ExpectedQuestRevision,
                    baseline.ReceiptExpectation,
                    baseline.Dependencies),
                new Nvs01ConsequencePlanningContext(
                    baseline.Catalog,
                    baseline.QuestMutation,
                    baseline.Authority,
                    baseline.ExpectedAuthority,
                    baseline.ExpectedQuestRevision,
                    baseline.ReceiptExpectation,
                    null)
            };

            foreach (Nvs01ConsequencePlanningContext context in missing)
            {
                AssertRejected(
                    Nvs01ConsequencePlanner.Plan(context),
                    Nvs01ConsequencePlanningStatus.RejectedMissingInput,
                    Nvs01ConsequenceDiagnosticCodes.MissingInput);
            }
        }

        private static Nvs01ConsequencePlanningContext Context(
            Nvs01MutationPlan mutation,
            Nvs01VerifiedCatalog catalog = null,
            ProfileWriteAuthoritySnapshot authority = null,
            ProfileAuthorityExpectation expectedAuthority = null,
            long? expectedQuestRevision = null,
            Nvs01ConsequenceReceiptExpectation receiptExpectation = null,
            Nvs01CapabilitySnapshot capabilities = null,
            Nvs01ConsequenceDomainSnapshot domain = null,
            Nvs01ChapterAuthoritySnapshot chapters = null,
            Nvs01ConsequenceReceiptAuthoritySnapshot
                receiptAuthorities = null)
        {
            ProfileWriteAuthoritySnapshot resolvedAuthority =
                authority ?? Authority();
            string dependencyProfileId =
                Nvs01AuthorityGuard.IsCanonicalProfileId(
                    resolvedAuthority.ProfileId)
                    ? resolvedAuthority.ProfileId
                    : ProfileId;
            string dependencyFingerprint =
                Nvs01AuthorityGuard.IsCanonicalSha256(
                    resolvedAuthority.VerifiedGenerationFingerprint)
                    ? resolvedAuthority.VerifiedGenerationFingerprint
                    : Fingerprint;
            Nvs01VerifiedConsequenceDependencies dependencies =
                Nvs01ConsequencePlanner.Capture(
                    VerifiedCatalog(),
                    dependencyProfileId,
                    dependencyFingerprint,
                    capabilities ?? Capabilities(),
                    domain ?? DomainFor(mutation),
                    chapters ?? ChapterAuthority(ChapterRows()),
                    receiptAuthorities);
            return new Nvs01ConsequencePlanningContext(
                catalog ?? VerifiedCatalog(),
                mutation,
                resolvedAuthority,
                expectedAuthority ??
                ProfileAuthorityExpectation.From(resolvedAuthority),
                expectedQuestRevision ?? mutation.Expected.Revision,
                receiptExpectation ?? ReceiptExpectationFor(mutation),
                dependencies);
        }

        private static Nvs01ConsequencePlanningContext With(
            Nvs01ConsequencePlanningContext source,
            ProfileWriteAuthoritySnapshot authority = null,
            ProfileAuthorityExpectation expectedAuthority = null,
            long? expectedQuestRevision = null) =>
            new Nvs01ConsequencePlanningContext(
                source.Catalog,
                source.QuestMutation,
                authority ?? source.Authority,
                expectedAuthority ?? source.ExpectedAuthority,
                expectedQuestRevision ?? source.ExpectedQuestRevision,
                source.ReceiptExpectation,
                source.Dependencies);

        private static Nvs01ConsequencePlanningContext WithDependencies(
            Nvs01ConsequencePlanningContext source,
            Nvs01VerifiedConsequenceDependencies dependencies) =>
            new Nvs01ConsequencePlanningContext(
                source.Catalog,
                source.QuestMutation,
                source.Authority,
                source.ExpectedAuthority,
                source.ExpectedQuestRevision,
                source.ReceiptExpectation,
                dependencies);

        private static Nvs01ConsequenceDomainSnapshot DomainFor(
            Nvs01MutationPlan mutation)
        {
            if (string.Equals(
                    mutation.TriggerEventId,
                    Nvs01ConsequenceContract.ArenaSuccessEventId,
                    StringComparison.Ordinal))
            {
                return mutation.IsReplayVerification
                    ? ArenaAppliedDomain(mutation)
                    : ArenaDomain();
            }

            return mutation.IsReplayVerification
                ? ReportAppliedDomain(mutation)
                : ReportDomain(mutation);
        }

        private static Nvs01ConsequenceDomainSnapshot ArenaDomain(
            Nvs01ConsequenceDependencyStatus artifactStatus =
                Nvs01ConsequenceDependencyStatus.Available,
            long gold = 1000,
            float affinity = 10,
            string currentChapterId = "CH0_PROLOGUE",
            IList<string> artifacts = null,
            IList<string> operations = null,
            IList<string> effects = null,
            IList<Nvs01ConsequenceApplicationReceipt> receipts = null) =>
            new Nvs01ConsequenceDomainSnapshot(
                artifactStatus,
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                gold,
                affinity,
                currentChapterId,
                artifacts ?? Array.Empty<string>(),
                operations ?? Array.Empty<string>(),
                effects ?? Array.Empty<string>(),
                receipts ??
                    Array.Empty<Nvs01ConsequenceApplicationReceipt>());

        private static Nvs01ConsequenceDomainSnapshot ArenaAppliedDomain(
            Nvs01MutationPlan mutation,
            long gold = 1000,
            float affinity = 10,
            string currentChapterId = "CH0_PROLOGUE")
        {
            string correlationId =
                mutation.Candidate.LastEncounterCorrelationId;
            string operationId =
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                correlationId;
            return ArenaDomain(
                gold: gold,
                affinity: affinity,
                currentChapterId: currentChapterId,
                artifacts: new[]
                {
                    Nvs01ConsequenceContract.TearArtifactId
                },
                operations: new[] { operationId },
                effects: new[]
                {
                    Nvs01ConsequenceContract.TearConsequenceId
                },
                receipts: new[]
                {
                    ArenaReceipt(
                        mutation.Candidate.CommittedRealmId,
                        correlationId,
                        mutation.Candidate.Revision,
                        gold,
                        affinity,
                        currentChapterId)
                });
        }

        private static Nvs01ConsequenceDomainSnapshot ReportDomain(
            Nvs01MutationPlan mutation,
            string currentChapterId = "",
            long gold = 1000,
            float affinity = 10,
            Nvs01ConsequenceDependencyStatus artifactStatus =
                Nvs01ConsequenceDependencyStatus.Available,
            Nvs01ConsequenceDependencyStatus goldStatus =
                Nvs01ConsequenceDependencyStatus.Available,
            Nvs01ConsequenceDependencyStatus affinityStatus =
                Nvs01ConsequenceDependencyStatus.Available,
            IList<string> artifacts = null,
            IList<string> operations = null,
            IList<string> effects = null,
            IList<Nvs01ConsequenceApplicationReceipt> receipts = null,
            string technicalCurrencyId = null)
        {
            string correlationId =
                mutation.Candidate.LastEncounterCorrelationId;
            string arenaOperationId =
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                correlationId;
            return new Nvs01ConsequenceDomainSnapshot(
                artifactStatus,
                goldStatus,
                affinityStatus,
                gold,
                affinity,
                currentChapterId,
                artifacts ?? new[]
                {
                    Nvs01ConsequenceContract.TearArtifactId
                },
                operations ?? new[] { arenaOperationId },
                effects ?? new[]
                {
                    Nvs01ConsequenceContract.TearConsequenceId
                },
                receipts ?? new[]
                {
                    ArenaReceipt(
                        mutation.Candidate.CommittedRealmId,
                        correlationId,
                        mutation.Expected.Revision - 1,
                        gold,
                        affinity,
                        currentChapterId)
                },
                technicalCurrencyId);
        }

        private static Nvs01ConsequenceDomainSnapshot ReportAppliedDomain(
            Nvs01MutationPlan mutation,
            long gold = 1500,
            float affinity = 15,
            string currentChapterId = null)
        {
            string realmId = mutation.Candidate.CommittedRealmId;
            string correlationId =
                mutation.Candidate.LastEncounterCorrelationId;
            string chapter = currentChapterId ??
                             Nvs01ConsequenceContract.ChapterForRealm(
                                 realmId);
            string arenaOperationId =
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                correlationId;
            Nvs01ConsequenceApplicationReceipt arenaReceipt = ArenaReceipt(
                realmId,
                correlationId,
                mutation.Candidate.Revision - 2,
                gold - Nvs01ConsequenceContract.OathmarkAmount,
                affinity - Nvs01ConsequenceContract.AffinityAmount,
                string.Empty);
            Nvs01ConsequenceApplicationReceipt reportReceipt = ReportReceipt(
                realmId,
                correlationId,
                mutation.Expected.LastOperation.OperationId,
                mutation.Expected.LastOperation.PayloadFingerprint,
                arenaReceipt.PlanFingerprint,
                arenaReceipt.ExpectedGenerationFingerprint,
                mutation.Expected.LastOperation
                    .ExpectedGenerationFingerprint,
                mutation.Candidate.Revision - 1,
                gold - Nvs01ConsequenceContract.OathmarkAmount,
                gold,
                affinity - Nvs01ConsequenceContract.AffinityAmount,
                affinity,
                string.Empty,
                chapter);
            return new Nvs01ConsequenceDomainSnapshot(
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                Nvs01ConsequenceDependencyStatus.Available,
                gold,
                affinity,
                chapter,
                new[] { Nvs01ConsequenceContract.TearArtifactId },
                new[]
                {
                    arenaOperationId,
                    Nvs01ConsequenceContract.ReportOperationId
                },
                Nvs01ConsequenceContract.ExpectedConsequenceOrder.ToArray(),
                new[] { arenaReceipt, reportReceipt });
        }

        private static Nvs01ConsequenceApplicationReceipt ArenaReceipt(
            string realmId,
            string correlationId,
            long candidateQuestRevision,
            long gold,
            float affinity,
            string chapterId) =>
            Nvs01ConsequenceApplicationReceipt.Create(
                Nvs01ConsequencePlanKind.ArenaSuccess,
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                correlationId,
                ProfileId,
                Fingerprint,
                correlationId,
                ArenaResultFingerprint(realmId, correlationId),
                string.Empty,
                string.Empty,
                realmId,
                correlationId,
                candidateQuestRevision - 1,
                candidateQuestRevision,
                new[] { Nvs01ConsequenceContract.TearConsequenceId },
                string.Empty,
                gold,
                gold,
                affinity,
                affinity,
                chapterId,
                chapterId);

        private static Nvs01ConsequenceApplicationReceipt ReportReceipt(
            string realmId,
            string correlationId,
            string causalOperationId,
            string causalPayloadFingerprint,
            string predecessorReceiptFingerprint,
            string predecessorExpectedGenerationFingerprint,
            string expectedGenerationFingerprint,
            long expectedQuestRevision,
            long previousGold,
            long resultingGold,
            float previousAffinity,
            float resultingAffinity,
            string previousChapter,
            string resultingChapter) =>
            Nvs01ConsequenceApplicationReceipt.Create(
                Nvs01ConsequencePlanKind.ReportCompletion,
                Nvs01ConsequenceContract.ReportOperationId,
                ProfileId,
                expectedGenerationFingerprint,
                causalOperationId,
                causalPayloadFingerprint,
                predecessorReceiptFingerprint,
                predecessorExpectedGenerationFingerprint,
                realmId,
                correlationId,
                expectedQuestRevision,
                expectedQuestRevision + 1,
                Nvs01ConsequenceContract.ExpectedConsequenceOrder
                    .Skip(1)
                    .ToArray(),
                Nvs01ConsequenceContract.ChapterForRealm(realmId),
                previousGold,
                resultingGold,
                previousAffinity,
                resultingAffinity,
                previousChapter,
                resultingChapter);

        private static string ArenaResultFingerprint(
            string realmId,
            string correlationId) =>
            TestFingerprint(
                "ApplyEncounterResult",
                correlationId,
                Nvs01ConsequenceContract.QuestId,
                Nvs01ConsequenceContract.ArenaHookId,
                realmId,
                NvsEncounterOutcome.Success.ToString(),
                Nvs01ConsequenceContract.ArenaSuccessEventId,
                Nvs01ConsequenceContract.EncounterResultSnapshotVersion,
                Nvs01ConsequenceContract.EncounterResultSnapshotReference);

        private static string TestFingerprint(params string[] parts)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(
                        string.Join("\u001f", parts ?? Array.Empty<string>())));
                return BitConverter.ToString(hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static Nvs01ConsequenceReceiptAuthoritySnapshot
            ReceiptAuthoritiesFor(Nvs01ConsequenceDomainSnapshot domain)
        {
            if (domain == null) return null;
            var entries = new List<
                Nvs01ConsequenceReceiptAuthorityEntry>(
                    domain.ApplicationReceipts.Count);
            for (int index = 0;
                 index < domain.ApplicationReceipts.Count;
                 index++)
            {
                Nvs01ConsequenceApplicationReceipt receipt =
                    domain.ApplicationReceipts[index];
                entries.Add(
                    receipt == null
                        ? null
                        : new Nvs01ConsequenceReceiptAuthorityEntry(
                            receipt.OperationId,
                            receipt.PlanFingerprint));
            }

            return new Nvs01ConsequenceReceiptAuthoritySnapshot(entries);
        }

        private static Nvs01ConsequenceReceiptExpectation
            ReceiptExpectationFor(Nvs01MutationPlan mutation) =>
            new Nvs01ConsequenceReceiptExpectation(
                mutation.Candidate.LastOperation?.OperationId,
                mutation.Candidate.LastOperation?.PayloadFingerprint);

        private static ProfileWriteAuthoritySnapshot Authority(
            string profileId = ProfileId,
            string epoch = AuthorityEpoch,
            string fingerprint = Fingerprint) =>
            ProfileWriteAuthoritySnapshotFactory.Writable(
                profileId,
                epoch,
                fingerprint,
                ProfileAuthoritySourceGeneration.Primary,
                Array.Empty<string>());

        private static Nvs01CapabilitySnapshot Capabilities(
            string unavailableId = null)
        {
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (Nvs01ExternalCapability capability in
                     VerifiedCatalog().Catalog.ExternalCapabilities)
            {
                values.Add(
                    capability.Id,
                    !string.Equals(
                        capability.Id,
                        unavailableId,
                        StringComparison.Ordinal));
            }

            return new Nvs01CapabilitySnapshot(values);
        }

        private static Nvs01ChapterAuthoritySnapshot ChapterAuthority(
            IList<Nvs01ChapterReference> rows,
            Nvs01ConsequenceDependencyStatus status =
                Nvs01ConsequenceDependencyStatus.Available,
            bool isComplete = true) =>
            new Nvs01ChapterAuthoritySnapshot(status, isComplete, rows);

        private static List<Nvs01ChapterReference> ChapterRows() =>
            new List<Nvs01ChapterReference>
            {
                new Nvs01ChapterReference(
                    "C1_CL", "crownlands", 1, false),
                new Nvs01ChapterReference(
                    "C1_SH", "stonehold", 1, false),
                new Nvs01ChapterReference(
                    "C1_EG", "eldergrove", 1, false),
                new Nvs01ChapterReference(
                    "C1_UM", "umbral", 1, false),
                new Nvs01ChapterReference(
                    "C2_CL", "crownlands", 2, false),
                new Nvs01ChapterReference(
                    "C2_SH", "stonehold", 2, false),
                new Nvs01ChapterReference(
                    "C2_EG", "eldergrove", 2, false),
                new Nvs01ChapterReference(
                    "C2_UM", "umbral", 2, false),
                new Nvs01ChapterReference(
                    "FUTURE_CL", "crownlands", 3, true)
            };

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
            Assert.True(
                result.IsAccepted,
                string.Join(
                    "\n",
                    result.Diagnostics.Select(diagnostic =>
                        diagnostic.Code + ":" + diagnostic.Path)));
            _catalog = result.VerifiedCatalog;
            return _catalog;
        }

        private static Nvs01VerifiedCatalog ForgedConsequenceCatalog(
            string tearTarget)
        {
            Nvs01Catalog source = VerifiedCatalog().Catalog;
            var consequences = source.Consequences
                .Select(consequence => new Nvs01Consequence(
                    consequence.Id,
                    consequence.Target,
                    consequence.Trigger,
                    consequence.Repeatability,
                    consequence.Retained,
                    consequence.Amount))
                .ToList();
            Nvs01Consequence tear = consequences[0];
            consequences[0] = new Nvs01Consequence(
                tear.Id,
                tearTarget,
                tear.Trigger,
                tear.Repeatability,
                tear.Retained,
                tear.Amount);
            var forged = new Nvs01Catalog(
                source.SchemaVersion,
                source.PacketVersion,
                source.MilestoneId,
                source.QuestId,
                source.TitleKey,
                source.DescriptionKey,
                source.Approval,
                source.Placement,
                source.Speaker,
                source.States.ToList(),
                source.Objectives.ToList(),
                source.Dialogue.ToList(),
                source.Transitions.ToList(),
                source.ExternalCapabilities.ToList(),
                consequences,
                source.Abandonment,
                source.Localization.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value,
                    StringComparer.Ordinal));
            return new Nvs01VerifiedCatalog(
                forged,
                Nvs01ConsequenceContract.PacketByteLength,
                Nvs01ConsequenceContract.PacketSha256);
        }

        private static Nvs01VerifiedCatalog ForgedGoldTargetCatalog(
            string goldTarget) =>
            ForgedConsequenceCatalogAt(1, goldTarget);

        private static Nvs01VerifiedCatalog ForgedConsequenceCatalogAt(
            int index,
            string target)
        {
            Nvs01Catalog source = VerifiedCatalog().Catalog;
            var consequences = source.Consequences
                .Select(consequence => new Nvs01Consequence(
                    consequence.Id,
                    consequence.Target,
                    consequence.Trigger,
                    consequence.Repeatability,
                    consequence.Retained,
                    consequence.Amount))
                .ToList();
            Nvs01Consequence row = consequences[index];
            consequences[index] = new Nvs01Consequence(
                row.Id,
                target,
                row.Trigger,
                row.Repeatability,
                row.Retained,
                row.Amount);
            var forged = new Nvs01Catalog(
                source.SchemaVersion,
                source.PacketVersion,
                source.MilestoneId,
                source.QuestId,
                source.TitleKey,
                source.DescriptionKey,
                source.Approval,
                source.Placement,
                source.Speaker,
                source.States.ToList(),
                source.Objectives.ToList(),
                source.Dialogue.ToList(),
                source.Transitions.ToList(),
                source.ExternalCapabilities.ToList(),
                consequences,
                source.Abandonment,
                source.Localization.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value,
                    StringComparer.Ordinal));
            return new Nvs01VerifiedCatalog(
                forged,
                Nvs01ConsequenceContract.PacketByteLength,
                Nvs01ConsequenceContract.PacketSha256);
        }

        private static Nvs01QuestSnapshot CopySnapshot(
            Nvs01QuestSnapshot source,
            IList<Nvs01ObjectiveSnapshot> objectives = null,
            NvsEncounterRequest currentEncounter = null,
            bool replaceEncounter = false,
            Nvs01OperationReceipt lastOperation = null,
            bool replaceLastOperation = false,
            string snapshotVersion = null,
            string snapshotReference = null) =>
            new Nvs01QuestSnapshot(
                source.PacketVersion,
                source.PacketSha256,
                source.QuestId,
                source.Revision,
                source.StateId,
                objectives ?? source.Objectives.ToList(),
                source.CurrentDialogueNodeId,
                source.PendingChoice,
                source.PendingSemanticActionId,
                source.CommittedRealmId,
                source.EncounterStatus,
                replaceEncounter ? currentEncounter : source.CurrentEncounter,
                source.LastEncounterCorrelationId,
                source.LastEncounterOutcome,
                source.LastEncounterEventId,
                snapshotVersion ?? source.LastEncounterSnapshotVersion,
                snapshotReference ?? source.LastEncounterSnapshotReference,
                replaceLastOperation ? lastOperation : source.LastOperation,
                source.ConsequenceIntentIds.ToList());

        private static Nvs01ConsequenceDomainSnapshot CopyDomain(
            Nvs01ConsequenceDomainSnapshot source,
            long? gold = null,
            float? affinity = null,
            string currentChapterId = null,
            IList<string> artifacts = null,
            IList<string> operations = null,
            IList<string> effects = null,
            IList<Nvs01ConsequenceApplicationReceipt> receipts = null) =>
            new Nvs01ConsequenceDomainSnapshot(
                source.ArtifactDefinitionStatus,
                source.GoldDefinitionStatus,
                source.AffinityDefinitionStatus,
                gold ?? source.GoldBalance,
                affinity ?? source.ValeriusAffinity,
                currentChapterId ?? source.CurrentChapterId,
                artifacts ?? source.AcquiredArtifactIds.ToArray(),
                operations ?? source.AppliedOperationIds.ToArray(),
                effects ?? source.AppliedEffectKeys.ToArray(),
                receipts ?? source.ApplicationReceipts.ToArray(),
                source.TechnicalCurrencyId);

        private static Nvs01ConsequenceApplicationReceipt CloneReceipt(
            Nvs01ConsequenceApplicationReceipt source,
            string planFingerprint = null) =>
            new Nvs01ConsequenceApplicationReceipt(
                source.ContractVersion,
                source.Kind,
                source.OperationId,
                source.ProfileId,
                source.ExpectedGenerationFingerprint,
                source.CausalOperationId,
                source.CausalPayloadFingerprint,
                source.PredecessorReceiptFingerprint,
                source.PredecessorExpectedGenerationFingerprint,
                source.RealmId,
                source.CorrelationId,
                source.ExpectedQuestRevision,
                source.CandidateQuestRevision,
                source.EffectKeys.ToArray(),
                source.TargetChapterId,
                source.PreviousGoldBalance,
                source.ResultingGoldBalance,
                source.PreviousValeriusAffinity,
                source.ResultingValeriusAffinity,
                source.PreviousChapterId,
                source.ResultingChapterId,
                planFingerprint ?? source.PlanFingerprint,
                source.TechnicalCurrencyId);

        private static Nvs01ConsequenceApplicationReceipt ReissueReceipt(
            Nvs01ConsequenceApplicationReceipt source,
            string profileId = null,
            string expectedGenerationFingerprint = null,
            string causalOperationId = null,
            string causalPayloadFingerprint = null,
            string predecessorReceiptFingerprint = null,
            string predecessorExpectedGenerationFingerprint = null,
            string realmId = null,
            string correlationId = null,
            long? expectedQuestRevision = null)
        {
            long expectedRevision =
                expectedQuestRevision ?? source.ExpectedQuestRevision;
            return Nvs01ConsequenceApplicationReceipt.Create(
                source.Kind,
                source.OperationId,
                profileId ?? source.ProfileId,
                expectedGenerationFingerprint ??
                source.ExpectedGenerationFingerprint,
                causalOperationId ?? source.CausalOperationId,
                causalPayloadFingerprint ??
                source.CausalPayloadFingerprint,
                predecessorReceiptFingerprint ??
                source.PredecessorReceiptFingerprint,
                predecessorExpectedGenerationFingerprint ??
                source.PredecessorExpectedGenerationFingerprint,
                realmId ?? source.RealmId,
                correlationId ?? source.CorrelationId,
                expectedRevision,
                expectedRevision + 1,
                source.EffectKeys.ToArray(),
                source.TargetChapterId,
                source.PreviousGoldBalance,
                source.ResultingGoldBalance,
                source.PreviousValeriusAffinity,
                source.ResultingValeriusAffinity,
                source.PreviousChapterId,
                source.ResultingChapterId,
                source.TechnicalCurrencyId);
        }

        private static Nvs01MutationPlan ForgeMutation(
            Nvs01MutationPlan source,
            string operationId = null,
            string payloadFingerprint = null)
        {
            Nvs01OperationReceipt operation =
                source.Candidate.LastOperation;
            Assert.NotNull(operation);
            var forgedOperation = new Nvs01OperationReceipt(
                operationId ?? operation.OperationId,
                payloadFingerprint ?? operation.PayloadFingerprint,
                operation.Status,
                operation.Revision,
                operation.StateId,
                operation.EventId,
                operation.CorrelationId);
            Nvs01QuestSnapshot forgedCandidate = CopySnapshot(
                source.Candidate,
                lastOperation: forgedOperation,
                replaceLastOperation: true);
            return BindMutation(
                new Nvs01MutationPlan(
                    source.Expected,
                    forgedCandidate,
                    source.TriggerEventId,
                    source.ConsequenceIntentIds.ToList()));
        }

        private static Nvs01MutationPlan BindMutation(
            Nvs01MutationPlan source,
            ProfileWriteAuthoritySnapshot authority = null)
        {
            ProfileWriteAuthoritySnapshot resolvedAuthority =
                authority ?? Authority();
            ProfileAuthorityExpectation expectation =
                ProfileAuthorityExpectation.From(resolvedAuthority);
            Nvs01OperationReceipt operation = source.Candidate.LastOperation;
            Assert.NotNull(operation);
            var stampedOperation = new Nvs01OperationReceipt(
                operation.OperationId,
                operation.PayloadFingerprint,
                operation.Status,
                operation.Revision,
                operation.StateId,
                operation.EventId,
                operation.CorrelationId,
                expectation.ExpectedGenerationFingerprint);
            Nvs01QuestSnapshot stampedCandidate = CopySnapshot(
                source.Candidate,
                lastOperation: stampedOperation,
                replaceLastOperation: true);
            return source.BindAuthority(expectation, stampedCandidate);
        }

        private static Nvs01MutationPlan BindReportMutation(
            Nvs01MutationPlan source,
            ProfileWriteAuthoritySnapshot currentAuthority,
            string predecessorExpectedGenerationFingerprint)
        {
            Assert.NotNull(source?.Expected?.LastOperation);
            Nvs01OperationReceipt prior = source.Expected.LastOperation;
            var stampedPrior = new Nvs01OperationReceipt(
                prior.OperationId,
                prior.PayloadFingerprint,
                prior.Status,
                prior.Revision,
                prior.StateId,
                prior.EventId,
                prior.CorrelationId,
                predecessorExpectedGenerationFingerprint);
            Nvs01QuestSnapshot stampedExpected = CopySnapshot(
                source.Expected,
                lastOperation: stampedPrior,
                replaceLastOperation: true);
            var stampedSource = new Nvs01MutationPlan(
                stampedExpected,
                source.Candidate,
                source.TriggerEventId,
                source.ConsequenceIntentIds.ToList());
            return BindMutation(stampedSource, currentAuthority);
        }

        private static void AssertTransitionRejected(
            Nvs01MutationPlan mutation,
            Nvs01ConsequenceReceiptExpectation receiptExpectation = null)
        {
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        receiptExpectation: receiptExpectation)),
                Nvs01ConsequencePlanningStatus
                    .RejectedQuestTransitionMismatch,
                Nvs01ConsequenceDiagnosticCodes.QuestTransitionMismatch);
        }

        private static void AssertChapterRejected(
            Nvs01MutationPlan mutation,
            Nvs01ChapterAuthoritySnapshot chapters)
        {
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(mutation, chapters: chapters)),
                Nvs01ConsequencePlanningStatus
                    .RejectedChapterIncompatible,
                Nvs01ConsequenceDiagnosticCodes.ChapterIncompatible);
        }

        private static void AssertDomainMalformed(
            Nvs01MutationPlan mutation,
            Nvs01ConsequenceDomainSnapshot domain)
        {
            AssertRejected(
                Nvs01ConsequencePlanner.Plan(
                    Context(mutation, domain: domain)),
                Nvs01ConsequencePlanningStatus
                    .RejectedDependencyMalformed,
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);
        }

        private static void AssertCaptureRejected(
            Nvs01ConsequenceDomainSnapshot domain,
            Nvs01ChapterAuthoritySnapshot chapters = null)
        {
            var provider = new TestDependencyProvider();
            provider.Configure(
                Capabilities(),
                domain,
                chapters ?? ChapterAuthority(ChapterRows()));
            var authority = new Nvs01ConsequenceDependencyAuthority(
                provider,
                DependencyProviderId,
                DependencyCatalogSetId);

            Assert.False(
                authority.TryCapture(
                    VerifiedCatalog(),
                    ProfileId,
                    Fingerprint,
                    out Nvs01VerifiedConsequenceDependencies rejected));
            Assert.IsNull(rejected);
        }

        private static void AssertDependencyRejected(
            Nvs01ConsequencePlanningResult result,
            Nvs01ConsequencePlanningStatus expectedStatus)
        {
            string diagnostic = expectedStatus ==
                Nvs01ConsequencePlanningStatus.RejectedDependencyUnavailable
                ? Nvs01ConsequenceDiagnosticCodes.DependencyUnavailable
                : Nvs01ConsequenceDiagnosticCodes.DependencyMalformed;
            AssertRejected(result, expectedStatus, diagnostic);
        }

        private static void AssertDependencyAuthorityRejected(
            ConsequencePlannerHarness harness,
            Nvs01ConsequencePlanningContext context)
        {
            AssertRejected(
                harness.Plan(context),
                Nvs01ConsequencePlanningStatus.RejectedDependencyMalformed,
                Nvs01ConsequenceDiagnosticCodes.DependencyMalformed);
        }

        private static void AssertPartial(
            Nvs01ConsequencePlanningResult result)
        {
            AssertRejected(
                result,
                Nvs01ConsequencePlanningStatus.RejectedPartialApplication,
                Nvs01ConsequenceDiagnosticCodes.PartialApplication);
        }

        private static void AssertPartialWithoutRecovery(
            Nvs01ConsequencePlanningResult result)
        {
            AssertPartial(result);
            Assert.IsNull(result.RecoveryReceipt);
        }

        private static void AssertOperation(
            Nvs01ConsequenceOperation operation,
            string consequenceId,
            Nvs01ConsequenceMutationKind kind,
            string targetId,
            long amount,
            string value)
        {
            Assert.AreEqual(consequenceId, operation.ConsequenceId);
            Assert.AreEqual(kind, operation.Kind);
            Assert.AreEqual(targetId, operation.TargetId);
            Assert.AreEqual(amount, operation.Amount);
            Assert.AreEqual(value, operation.Value);
        }

        private static void AssertReady(
            Nvs01ConsequencePlanningResult result,
            Nvs01ConsequencePlanKind expectedKind)
        {
            Assert.AreEqual(
                Nvs01ConsequencePlanningStatus.Ready,
                result.Status,
                result.DiagnosticCode);
            Assert.True(result.IsReady);
            Assert.AreEqual(string.Empty, result.DiagnosticCode);
            Assert.NotNull(result.Plan);
            Assert.AreEqual(expectedKind, result.Plan.Kind);
            Assert.NotNull(result.Plan.ApplicationReceipt);
            Assert.True(
                result.Plan.ApplicationReceipt.HasCanonicalFingerprint());
            Assert.IsNull(result.RecoveryReceipt);
        }

        private static void AssertAlreadyApplied(
            Nvs01ConsequencePlanningResult result)
        {
            Assert.AreEqual(
                Nvs01ConsequencePlanningStatus.AlreadyApplied,
                result.Status,
                result.DiagnosticCode);
            Assert.False(result.IsReady);
            Assert.AreEqual(string.Empty, result.DiagnosticCode);
            Assert.IsNull(result.Plan);
            Assert.IsNull(result.RecoveryReceipt);
        }

        private static void AssertRejected(
            Nvs01ConsequencePlanningResult result,
            Nvs01ConsequencePlanningStatus expectedStatus,
            string expectedDiagnostic)
        {
            Assert.NotNull(result);
            Assert.AreEqual(expectedStatus, result.Status);
            Assert.False(result.IsReady);
            Assert.IsNull(result.Plan);
            Assert.AreEqual(expectedDiagnostic, result.DiagnosticCode);
        }

        private static void AssertPlanEqual(
            Nvs01ConsequencePlan expected,
            Nvs01ConsequencePlan actual)
        {
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.AreEqual(expected.Kind, actual.Kind);
            Assert.AreEqual(expected.OperationId, actual.OperationId);
            Assert.AreEqual(expected.ProfileId, actual.ProfileId);
            Assert.AreEqual(expected.AuthorityEpoch, actual.AuthorityEpoch);
            Assert.AreEqual(
                expected.ExpectedGenerationFingerprint,
                actual.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                expected.ExpectedQuestRevision,
                actual.ExpectedQuestRevision);
            Assert.AreEqual(
                expected.CandidateQuestRevision,
                actual.CandidateQuestRevision);
            Assert.AreEqual(expected.RealmId, actual.RealmId);
            Assert.AreEqual(expected.CorrelationId, actual.CorrelationId);
            Assert.AreEqual(expected.NextStateId, actual.NextStateId);
            Assert.AreEqual(
                expected.ResultingGoldBalance,
                actual.ResultingGoldBalance);
            Assert.AreEqual(
                expected.ResultingValeriusAffinity,
                actual.ResultingValeriusAffinity);
            Assert.AreEqual(
                expected.ResultingChapterId,
                actual.ResultingChapterId);
            CollectionAssert.AreEqual(
                expected.Operations.Select(OperationIdentity),
                actual.Operations.Select(OperationIdentity));
            AssertReceiptEqual(
                expected.ApplicationReceipt,
                actual.ApplicationReceipt);
        }

        private static void AssertReceiptEqual(
            Nvs01ConsequenceApplicationReceipt expected,
            Nvs01ConsequenceApplicationReceipt actual)
        {
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.AreEqual(expected.ContractVersion, actual.ContractVersion);
            Assert.AreEqual(expected.Kind, actual.Kind);
            Assert.AreEqual(expected.OperationId, actual.OperationId);
            Assert.AreEqual(expected.ProfileId, actual.ProfileId);
            Assert.AreEqual(
                expected.ExpectedGenerationFingerprint,
                actual.ExpectedGenerationFingerprint);
            Assert.AreEqual(
                expected.CausalOperationId,
                actual.CausalOperationId);
            Assert.AreEqual(
                expected.CausalPayloadFingerprint,
                actual.CausalPayloadFingerprint);
            Assert.AreEqual(
                expected.PredecessorReceiptFingerprint,
                actual.PredecessorReceiptFingerprint);
            Assert.AreEqual(
                expected.PredecessorExpectedGenerationFingerprint,
                actual.PredecessorExpectedGenerationFingerprint);
            Assert.AreEqual(expected.RealmId, actual.RealmId);
            Assert.AreEqual(expected.CorrelationId, actual.CorrelationId);
            Assert.AreEqual(
                expected.ExpectedQuestRevision,
                actual.ExpectedQuestRevision);
            Assert.AreEqual(
                expected.CandidateQuestRevision,
                actual.CandidateQuestRevision);
            CollectionAssert.AreEqual(expected.EffectKeys, actual.EffectKeys);
            Assert.AreEqual(
                expected.TargetChapterId,
                actual.TargetChapterId);
            Assert.AreEqual(
                expected.PreviousGoldBalance,
                actual.PreviousGoldBalance);
            Assert.AreEqual(
                expected.ResultingGoldBalance,
                actual.ResultingGoldBalance);
            Assert.AreEqual(
                expected.PreviousValeriusAffinity,
                actual.PreviousValeriusAffinity);
            Assert.AreEqual(
                expected.ResultingValeriusAffinity,
                actual.ResultingValeriusAffinity);
            Assert.AreEqual(
                expected.PreviousChapterId,
                actual.PreviousChapterId);
            Assert.AreEqual(
                expected.ResultingChapterId,
                actual.ResultingChapterId);
            Assert.AreEqual(
                expected.TechnicalCurrencyId,
                actual.TechnicalCurrencyId);
            Assert.AreEqual(
                expected.PlanFingerprint,
                actual.PlanFingerprint);
            Assert.True(actual.HasCanonicalFingerprint());
        }

        private static string OperationIdentity(
            Nvs01ConsequenceOperation operation) =>
            operation.ConsequenceId + "|" + operation.Kind + "|" +
            operation.TargetId + "|" + operation.Amount + "|" +
            operation.Value;

        private static void AssertReadOnly<T>(IReadOnlyList<T> values)
        {
            var list = values as IList;
            Assert.NotNull(list);
            Assert.True(list.IsReadOnly);
            object value = list.Count > 0 ? list[0] : new object();
            Assert.Throws<NotSupportedException>(() => list.Add(value));
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

        private sealed class ConsequencePlannerHarness
        {
            private readonly TestDependencyProvider _provider;
            private readonly Nvs01ConsequenceDependencyAuthority _authority;
            private readonly AL.Narrative.Nvs01.Nvs01ConsequencePlanner
                _planner;

            internal ConsequencePlannerHarness(
                TestDependencyProvider provider = null)
            {
                _provider = provider ?? new TestDependencyProvider();
                _authority = new Nvs01ConsequenceDependencyAuthority(
                    _provider,
                    DependencyProviderId,
                    DependencyCatalogSetId);
                _planner =
                    new AL.Narrative.Nvs01.Nvs01ConsequencePlanner(
                        _authority);
            }

            internal Nvs01ConsequencePlanningResult Plan(
                Nvs01ConsequencePlanningContext context) =>
                _planner.Plan(context);

            internal Nvs01VerifiedConsequenceDependencies Capture(
                Nvs01VerifiedCatalog catalog,
                string profileId,
                string expectedGenerationFingerprint,
                Nvs01CapabilitySnapshot capabilities,
                Nvs01ConsequenceDomainSnapshot domain,
                Nvs01ChapterAuthoritySnapshot chapters,
                Nvs01ConsequenceReceiptAuthoritySnapshot
                    receiptAuthorities = null)
            {
                _provider.Configure(
                    capabilities,
                    domain,
                    chapters,
                    receiptAuthorities ?? ReceiptAuthoritiesFor(domain));
                Assert.True(
                    _authority.TryCapture(
                        catalog,
                        profileId,
                        expectedGenerationFingerprint,
                        out Nvs01VerifiedConsequenceDependencies result));
                Assert.NotNull(result);
                return result;
            }

            internal TestDependencyProvider Provider => _provider;
        }

        private sealed class TestDependencyProvider :
            INvs01ConsequenceDependencyProvider
        {
            private Nvs01ConsequenceDependencyProviderIdentity _identity;
            private Nvs01CapabilitySnapshot _capabilities;
            private Nvs01ConsequenceDomainSnapshot _domain;
            private Nvs01ChapterAuthoritySnapshot _chapters;
            private Nvs01ConsequenceReceiptAuthoritySnapshot
                _receiptAuthorities;

            internal TestDependencyProvider(
                Nvs01ConsequenceDependencyProviderIdentity identity = null)
            {
                _identity = identity ?? DependencyIdentity();
            }

            internal void Configure(
                Nvs01CapabilitySnapshot capabilities,
                Nvs01ConsequenceDomainSnapshot domain,
                Nvs01ChapterAuthoritySnapshot chapters,
                Nvs01ConsequenceReceiptAuthoritySnapshot
                    receiptAuthorities = null)
            {
                _capabilities = capabilities;
                _domain = domain;
                _chapters = chapters;
                _receiptAuthorities =
                    receiptAuthorities ?? ReceiptAuthoritiesFor(domain);
            }

            internal void SetIdentity(
                Nvs01ConsequenceDependencyProviderIdentity identity)
            {
                _identity = identity;
            }

            public bool TryGetIdentity(
                out Nvs01ConsequenceDependencyProviderIdentity identity)
            {
                identity = _identity;
                return identity != null;
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
                return _identity != null;
            }
        }

        private static Nvs01ConsequenceDependencyProviderIdentity
            DependencyIdentity(
                int contractVersion =
                    Nvs01ConsequenceContract
                        .DependencyAuthorityContractVersion,
                string providerId = DependencyProviderId,
                string catalogSetId = DependencyCatalogSetId,
                string contentVersion = "game-data-test-v1",
                string sourceRevision = "source-test-v1",
                string sourceFingerprint = DependencySourceFingerprint,
                long providerRevision = 1) =>
            new Nvs01ConsequenceDependencyProviderIdentity(
                contractVersion,
                providerId,
                catalogSetId,
                contentVersion,
                sourceRevision,
                sourceFingerprint,
                providerRevision);

        private static NvsEncounterResult EncounterResult(
            string correlationId,
            string snapshotVersion,
            string snapshotReference) =>
            new NvsEncounterResult(
                Nvs01RuntimeContract.ContractVersion,
                correlationId,
                Nvs01ConsequenceContract.QuestId,
                Nvs01ConsequenceContract.ArenaHookId,
                "crownlands",
                NvsEncounterOutcome.Success,
                Nvs01ConsequenceContract.ArenaSuccessEventId,
                snapshotVersion,
                snapshotReference);

        private static void JointlyReissueReportChain(
            AdvancedReceiptChain chain,
            out Nvs01ConsequenceApplicationReceipt forgedArena,
            out Nvs01ConsequenceApplicationReceipt forgedReport)
        {
            forgedArena = ReissueReceipt(
                chain.ArenaReceipt,
                causalOperationId:
                    "44444444-4444-4444-8444-444444444444");
            forgedReport = ReissueReceipt(
                chain.ReportReceipt,
                predecessorReceiptFingerprint:
                    forgedArena.PlanFingerprint);
        }

        private static AdvancedReceiptChain BuildAdvancedReceiptChain()
        {
            var fixture = new RuntimeFixture();
            Nvs01MutationPlan reportUnbound = fixture.ReportMutation(
                "crownlands",
                bindAuthority: false);
            Nvs01MutationPlan arenaMutation = fixture.LastArenaMutation;
            Assert.NotNull(arenaMutation);

            Nvs01ConsequencePlanningResult arena =
                Nvs01ConsequencePlanner.Plan(Context(arenaMutation));
            AssertReady(arena, Nvs01ConsequencePlanKind.ArenaSuccess);
            Nvs01ConsequenceApplicationReceipt arenaReceipt =
                arena.Plan.ApplicationReceipt;

            ProfileWriteAuthoritySnapshot generationB = Authority(
                fingerprint: AlternateFingerprint);
            Nvs01MutationPlan arenaReplay = BindMutation(
                Nvs01MutationPlan.ForExactReplay(
                    arenaMutation.Candidate),
                generationB);
            Nvs01ConsequenceDomainSnapshot arenaApplied = CopyDomain(
                ArenaAppliedDomain(arenaReplay),
                receipts: new[] { arenaReceipt });

            Nvs01MutationPlan reportMutation = BindReportMutation(
                reportUnbound,
                generationB,
                Fingerprint);
            Nvs01ConsequenceDomainSnapshot beforeReport = CopyDomain(
                ReportDomain(reportMutation),
                receipts: new[] { arenaReceipt });
            Nvs01ConsequencePlanningResult report =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        reportMutation,
                        authority: generationB,
                        expectedAuthority:
                            ProfileAuthorityExpectation.From(generationB),
                        domain: beforeReport));
            AssertReady(report, Nvs01ConsequencePlanKind.ReportCompletion);
            Nvs01ConsequenceApplicationReceipt reportReceipt =
                report.Plan.ApplicationReceipt;

            ProfileWriteAuthoritySnapshot generationC = Authority(
                fingerprint: ThirdFingerprint);
            Nvs01MutationPlan reportReplay = BindMutation(
                Nvs01MutationPlan.ForExactReplay(
                    reportMutation.Candidate),
                generationC);
            Nvs01ConsequenceDomainSnapshot reportApplied = CopyDomain(
                ReportAppliedDomain(reportReplay),
                receipts: new[] { arenaReceipt, reportReceipt });
            Nvs01ConsequenceReceiptAuthoritySnapshot beforeAuthorities =
                ReceiptAuthoritiesFor(beforeReport);
            Nvs01ConsequenceReceiptAuthoritySnapshot appliedAuthorities =
                ReceiptAuthoritiesFor(reportApplied);

            return new AdvancedReceiptChain(
                generationB,
                generationC,
                arenaReplay,
                arenaApplied,
                arenaReceipt,
                reportMutation,
                beforeReport,
                beforeAuthorities,
                reportReplay,
                reportApplied,
                appliedAuthorities,
                reportReceipt);
        }

        private sealed class AdvancedReceiptChain
        {
            internal AdvancedReceiptChain(
                ProfileWriteAuthoritySnapshot generationB,
                ProfileWriteAuthoritySnapshot generationC,
                Nvs01MutationPlan arenaReplay,
                Nvs01ConsequenceDomainSnapshot arenaApplied,
                Nvs01ConsequenceApplicationReceipt arenaReceipt,
                Nvs01MutationPlan reportMutation,
                Nvs01ConsequenceDomainSnapshot beforeReport,
                Nvs01ConsequenceReceiptAuthoritySnapshot beforeAuthorities,
                Nvs01MutationPlan reportReplay,
                Nvs01ConsequenceDomainSnapshot reportApplied,
                Nvs01ConsequenceReceiptAuthoritySnapshot appliedAuthorities,
                Nvs01ConsequenceApplicationReceipt reportReceipt)
            {
                GenerationB = generationB;
                GenerationC = generationC;
                ArenaReplay = arenaReplay;
                ArenaApplied = arenaApplied;
                ArenaReceipt = arenaReceipt;
                ReportMutation = reportMutation;
                BeforeReport = beforeReport;
                BeforeReportAuthorities = beforeAuthorities;
                ReportReplay = reportReplay;
                ReportApplied = reportApplied;
                ReportAppliedAuthorities = appliedAuthorities;
                ReportReceipt = reportReceipt;
            }

            internal ProfileWriteAuthoritySnapshot GenerationB { get; }
            internal ProfileWriteAuthoritySnapshot GenerationC { get; }
            internal Nvs01MutationPlan ArenaReplay { get; }
            internal Nvs01ConsequenceDomainSnapshot ArenaApplied { get; }
            internal Nvs01ConsequenceApplicationReceipt ArenaReceipt
            {
                get;
            }
            internal Nvs01MutationPlan ReportMutation { get; }
            internal Nvs01ConsequenceDomainSnapshot BeforeReport { get; }
            internal Nvs01ConsequenceReceiptAuthoritySnapshot
                BeforeReportAuthorities { get; }
            internal Nvs01MutationPlan ReportReplay { get; }
            internal Nvs01ConsequenceDomainSnapshot ReportApplied { get; }
            internal Nvs01ConsequenceReceiptAuthoritySnapshot
                ReportAppliedAuthorities { get; }
            internal Nvs01ConsequenceApplicationReceipt ReportReceipt
            {
                get;
            }
        }

        private sealed class RuntimeFixture
        {
            private readonly CaptureCommitter _committer =
                new CaptureCommitter();
            private int _guidCounter;

            internal RuntimeFixture()
            {
                Runtime = new Nvs01QuestRuntime(
                    VerifiedCatalog(),
                    _committer,
                    NextGuid);
            }

            private Nvs01QuestRuntime Runtime { get; }

            internal Nvs01MutationPlan LastArenaMutation { get; private set; }

            internal Nvs01MutationPlan ArenaSuccessMutation(
                string realmId,
                bool bindAuthority = true)
            {
                AdvanceToRequest(realmId);
                NvsEncounterRequest request = Runtime.Snapshot.CurrentEncounter;
                Assert.NotNull(request);
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
                AssertCommitted(Runtime.ApplyEncounterResult(result));
                Assert.NotNull(_committer.LastPlan);
                LastArenaMutation = bindAuthority
                    ? BindMutation(_committer.LastPlan)
                    : _committer.LastPlan;
                return LastArenaMutation;
            }

            internal Nvs01MutationPlan ReportMutation(
                string realmId,
                bool bindAuthority = true)
            {
                ArenaSuccessMutation(realmId);
                AssertCommitted(
                    Runtime.SelectValerius(
                        Command(
                            Nvs01ConsequenceContract.ValeriusNpcId,
                            "POST_REALM_PROLOGUE"),
                        Nvs01InteractionKind.Report,
                        Realm(realmId)));
                _committer.Clear();
                AssertCommitted(
                    Runtime.SelectDialogueChoice(
                        Command(
                            "PLAYER",
                            Nvs01ConsequenceContract.ReportDialogueId),
                        "choice.omen1.present_tear"));
                Assert.NotNull(_committer.LastPlan);
                return bindAuthority
                    ? BindReportMutation(
                        _committer.LastPlan,
                        Authority(),
                        Fingerprint)
                    : _committer.LastPlan;
            }

            private void AdvanceToRequest(string realmId)
            {
                AssertCommitted(
                    Runtime.SelectValerius(
                        Command(
                            Nvs01ConsequenceContract.ValeriusNpcId,
                            "POST_REALM_PROLOGUE"),
                        Nvs01InteractionKind.Offer,
                        Realm(realmId)));
                AssertCommitted(
                    Runtime.SelectDialogueChoice(
                        Command("PLAYER", "DLG_OMEN_1_OFFER"),
                        "choice.omen1.accept"));
                AssertCommitted(
                    Runtime.SelectDialogueChoice(
                        Command("PLAYER", "DLG_OMEN_1_START"),
                        "choice.omen1.investigate"));
                AssertCommitted(
                    Runtime.SelectDialogueChoice(
                        Command("PLAYER", "DLG_OMEN_1_GO"),
                        "choice.omen1.deploy"));
                AssertCommitted(
                    Runtime.InvokePendingSemanticAction(
                        Command("PLAYER", "REQUEST_SKY_CASTLE_ARENA"),
                        Capabilities(),
                        Realm(realmId)));
            }

            private Nvs01CommandEnvelope Command(
                string actorId,
                string contextId) =>
                new Nvs01CommandEnvelope(
                    Nvs01RuntimeContract.ContractVersion,
                    NextGuid(),
                    Nvs01ConsequenceContract.QuestId,
                    Runtime.Snapshot.StateId,
                    Runtime.Snapshot.Revision,
                    actorId,
                    contextId,
                    0);

            private static Nvs01RealmContext Realm(string realmId) =>
                new Nvs01RealmContext(
                    Nvs01RealmContextStatus.CommittedValid,
                    realmId);

            private string NextGuid()
            {
                _guidCounter++;
                string suffix = _guidCounter.ToString("x12");
                return "11111111-1111-4111-8111-" + suffix;
            }

            private static void AssertCommitted(
                Nvs01CommandDisposition disposition)
            {
                Assert.AreEqual(
                    Nvs01CommandStatus.Committed,
                    disposition.Status,
                    disposition.Diagnostic?.Code);
            }
        }

        [TestCase("crownlands", "C1_CL", "C2_CL")]
        [TestCase("stonehold", "C1_SH", "C2_SH")]
        [TestCase("eldergrove", "C1_EG", "C2_EG")]
        [TestCase("umbral", "C1_UM", "C2_UM")]
        public void LaterChapterPreservesCurrentButTargetsExactRealmC1(
            string realmId,
            string targetC1,
            string currentC2)
        {
            Nvs01MutationPlan mutation =
                new RuntimeFixture().ReportMutation(realmId);
            Nvs01ConsequencePlanningResult result =
                Nvs01ConsequencePlanner.Plan(
                    Context(
                        mutation,
                        domain: ReportDomain(
                            mutation,
                            currentChapterId: currentC2)));

            AssertReady(
                result,
                Nvs01ConsequencePlanKind.ReportCompletion);
            Assert.AreEqual(currentC2, result.Plan.ResultingChapterId);
            Nvs01ConsequenceOperation unlock = result.Plan.Operations[3];
            Assert.AreEqual(
                Nvs01ConsequenceContract.ChapterConsequenceId,
                unlock.ConsequenceId);
            Assert.AreEqual(targetC1, unlock.Value);
            Assert.AreEqual(
                targetC1,
                result.Plan.ApplicationReceipt.TargetChapterId);
            Assert.AreEqual(
                currentC2,
                result.Plan.ApplicationReceipt.ResultingChapterId);
        }
    }
}
