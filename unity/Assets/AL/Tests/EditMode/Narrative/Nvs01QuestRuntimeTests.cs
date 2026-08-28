using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01QuestRuntimeTests
    {
        private const string QuestId = "OMEN_1";
        private const string Offered = "OFFERED";
        private const string Talk = "TALK_TO_VALERIUS";
        private const string Investigate = "INVESTIGATE_SKY_CASTLE";
        private const string Failed = "FAILED";
        private const string Report = "REPORT_TO_VALERIUS";
        private const string Completed = "COMPLETED";

        private const string TalkObjective = "OBJ_OMEN_1_TALK";
        private const string ArenaObjective = "OBJ_OMEN_1_ARENA";
        private const string ReportObjective = "OBJ_OMEN_1_REPORT";

        private const string OfferDialogue = "DLG_OMEN_1_OFFER";
        private const string StartDialogue = "DLG_OMEN_1_START";
        private const string LoreDialogue = "DLG_OMEN_1_LORE";
        private const string GoDialogue = "DLG_OMEN_1_GO";
        private const string ArenaStartDialogue = "DLG_OMEN_1_ARENA_START";
        private const string FailureDialogue = "DLG_OMEN_1_FAILURE";
        private const string ReportDialogue = "DLG_OMEN_1_REPORT";
        private const string ReportConclusionDialogue = "DLG_OMEN_1_REPORT_CONCLUSION";

        private const string RequestArena = "REQUEST_SKY_CASTLE_ARENA";
        private const string RetryArena = "RETRY_SKY_CASTLE_ARENA";
        private const string ArenaHook = "HOOK_SKY_CASTLE_ARENA";
        private const string ArenaLocation = "LOCATION_SKY_CASTLE_MARKER";
        private const string DeployCapability = "ACTION_DEPLOY_CHAMPION";
        private const string ArenaSuccess = "EVENT_SKY_CASTLE_ARENA_SUCCESS";
        private const string ArenaFailure = "EVENT_SKY_CASTLE_ARENA_FAILURE";
        private const string ArenaCancelled = "EVENT_SKY_CASTLE_ARENA_CANCELLED";
        private const string ArenaUnavailable = "EVENT_SKY_CASTLE_ARENA_UNAVAILABLE";

        private const string TearIntent = "ACQUIRE_CELESTIAL_TEAR";
        private static readonly string[] ReportIntents =
        {
            "GRANT_GOLD_500",
            "GRANT_VALERIUS_AFFINITY_5",
            "COMPLETE_OMEN_1",
            "UNLOCK_REALM_CHAPTER_1"
        };

        private static object _verifiedCatalog;

        [Test]
        public void InitialOfferCanBeDeferredWithoutProgressAndAcceptedOnlyByExactChoice()
        {
            var fixture = new RuntimeFixture();
            object initial = fixture.Snapshot;
            AssertSnapshot(initial, 0, Offered, string.Empty, false, string.Empty, string.Empty, "None");
            AssertObjectives(initial, "Active", "Inactive", "Inactive");
            Assert.IsNull(Property(initial, "LastOperation"));
            CollectionAssert.IsEmpty(StringItems(Property(initial, "ConsequenceIntentIds")));
            Assert.AreSame(initial, fixture.Snapshot);
            Assert.AreSame(Property(VerifiedCatalog(), "Catalog"), Property(fixture.Runtime, "Catalog"));

            object offer = fixture.SelectValerius("Offer", "crownlands");
            AssertCommitted(offer, 1, Offered);
            AssertSnapshot(fixture.Snapshot, 1, Offered, OfferDialogue, true, string.Empty, "crownlands", "None");
            AssertObjectives(fixture.Snapshot, "Active", "Inactive", "Inactive");

            object beforeWrongChoice = fixture.Snapshot;
            object wrongChoice = fixture.Choice("choice.omen1.missing");
            AssertStatus(wrongChoice, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(beforeWrongChoice, fixture.Snapshot);

            object decline = fixture.Choice("choice.omen1.decline");
            AssertCommitted(decline, 2, Offered);
            AssertSnapshot(fixture.Snapshot, 2, Offered, string.Empty, false, string.Empty, "crownlands", "None");
            AssertObjectives(fixture.Snapshot, "Active", "Inactive", "Inactive");
            CollectionAssert.IsEmpty(StringItems(Property(decline, "ConsequenceIntentIds")));

            fixture.SelectValerius("Offer", "crownlands");
            object accepted = fixture.Choice("choice.omen1.accept");
            AssertCommitted(accepted, 4, Talk);
            AssertSnapshot(fixture.Snapshot, 4, Talk, StartDialogue, true, string.Empty, "crownlands", "None");
            AssertObjectives(fixture.Snapshot, "Completed", "Inactive", "Inactive");
        }

        [Test]
        public void DirectHappyPathUsesManualReportAndPublishesOnlyCatalogConsequenceIntents()
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToRequest(false, "crownlands");
            AssertSnapshot(fixture.Snapshot, 5, Investigate, ArenaStartDialogue, false, string.Empty, "crownlands", "Requested");

            object successResult = fixture.Result("Success", "arena-v1", "snapshot://one");
            object success = fixture.ApplyResult(successResult);
            AssertCommitted(success, 6, Report);
            AssertSnapshot(fixture.Snapshot, 6, Report, string.Empty, false, string.Empty, "crownlands", "Resolved");
            AssertObjectives(fixture.Snapshot, "Completed", "Completed", "Active");
            CollectionAssert.AreEqual(new[] { TearIntent }, StringItems(Property(success, "ConsequenceIntentIds")));
            CollectionAssert.AreEqual(new[] { TearIntent }, StringItems(Property(fixture.Snapshot, "ConsequenceIntentIds")));
            Assert.AreEqual("arena-v1", Property(fixture.Snapshot, "LastEncounterSnapshotVersion"));
            Assert.AreEqual("snapshot://one", Property(fixture.Snapshot, "LastEncounterSnapshotReference"));

            object wrongInteraction = fixture.SelectValerius("Offer", "crownlands");
            AssertStatus(wrongInteraction, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreEqual(6L, Property(fixture.Snapshot, "Revision"));

            fixture.SelectValerius("Report", "crownlands");
            AssertSnapshot(fixture.Snapshot, 7, Report, ReportDialogue, true, string.Empty, "crownlands", "Resolved");
            object conclusion = fixture.Choice("choice.omen1.present_tear");
            AssertCommitted(conclusion, 8, Completed);
            AssertSnapshot(fixture.Snapshot, 8, Completed, ReportConclusionDialogue, true, string.Empty, "crownlands", "Resolved");
            AssertObjectives(fixture.Snapshot, "Completed", "Completed", "Completed");
            CollectionAssert.AreEqual(ReportIntents, StringItems(Property(conclusion, "ConsequenceIntentIds")));
            CollectionAssert.AreEqual(new[] { TearIntent }.Concat(ReportIntents).ToArray(), StringItems(Property(fixture.Snapshot, "ConsequenceIntentIds")));

            object close = fixture.Choice("choice.omen1.continue");
            AssertCommitted(close, 9, Completed);
            AssertSnapshot(fixture.Snapshot, 9, Completed, string.Empty, false, string.Empty, "crownlands", "Resolved");
            CollectionAssert.IsEmpty(StringItems(Property(close, "ConsequenceIntentIds")));
        }

        [Test]
        public void LoreBranchAndArenaStartUseTheAuthoredTwoStepHandoff()
        {
            var fixture = new RuntimeFixture();
            fixture.SelectValerius("Offer", "crownlands");
            fixture.Choice("choice.omen1.accept");

            fixture.Choice("choice.omen1.ask_more");
            AssertSnapshot(fixture.Snapshot, 3, Talk, LoreDialogue, true, string.Empty, "crownlands", "None");
            fixture.Choice("choice.omen1.depart");
            AssertSnapshot(fixture.Snapshot, 4, Talk, GoDialogue, true, string.Empty, "crownlands", "None");
            fixture.Choice("choice.omen1.deploy");
            AssertSnapshot(fixture.Snapshot, 5, Talk, ArenaStartDialogue, false, RequestArena, "crownlands", "None");
            Assert.IsNull(Property(fixture.Snapshot, "CurrentEncounter"));
            Assert.AreEqual(0, fixture.GuidCalls, "The authored arena-start node must commit before request identity is generated.");

            object requestDisposition = fixture.InvokePending("crownlands");
            AssertCommitted(requestDisposition, 6, Investigate);
            AssertSnapshot(fixture.Snapshot, 6, Investigate, ArenaStartDialogue, false, string.Empty, "crownlands", "Requested");
            Assert.AreEqual(2, fixture.GuidCalls);
            Assert.NotNull(Property(requestDisposition, "EncounterRequest"));
        }

        [TestCase("crownlands")]
        [TestCase("stonehold")]
        [TestCase("eldergrove")]
        [TestCase("umbral")]
        public void EncounterRequestIsExactForEveryRealmAndDuplicatesReuseItsCorrelation(string realm)
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToArenaStart(false, realm);
            object command = fixture.Command("PLAYER", RequestArena);
            object capabilities = fixture.AllCapabilities();
            object realmContext = fixture.Realm("CommittedValid", realm);
            object disposition = Invoke(fixture.Runtime, "InvokePendingSemanticAction", command, capabilities, realmContext);
            AssertCommitted(disposition, 5, Investigate);

            object request = Property(disposition, "EncounterRequest");
            Assert.AreSame(request, Property(fixture.Snapshot, "CurrentEncounter"));
            Assert.AreEqual(1, Property(request, "ContractVersion"));
            Assert.AreEqual(GeneratedGuid(1), Property(request, "RequestId"));
            Assert.AreEqual(GeneratedGuid(2), Property(request, "CorrelationId"));
            Assert.AreNotEqual(Property(request, "RequestId"), Property(request, "CorrelationId"));
            Assert.AreEqual(QuestId, Property(request, "QuestId"));
            Assert.AreEqual(Investigate, Property(request, "StateId"));
            Assert.AreEqual(ArenaObjective, Property(request, "ObjectiveId"));
            Assert.AreEqual(ArenaHook, Property(request, "HookId"));
            Assert.AreEqual(ArenaLocation, Property(request, "LocationId"));
            Assert.AreEqual(realm, Property(request, "RealmId"));
            Assert.AreEqual(ArenaSuccess, Property(request, "SuccessEventId"));
            Assert.AreEqual(ArenaFailure, Property(request, "FailureEventId"));
            Assert.AreEqual(ArenaCancelled, Property(request, "CancelledEventId"));
            Assert.AreEqual(ArenaUnavailable, Property(request, "UnavailableEventId"));
            Assert.AreEqual("Kingdom", Property(request, "ReturnScene"));

            object committedSnapshot = fixture.Snapshot;
            int attempts = fixture.CommitAttempts;
            object exactDuplicate = Invoke(fixture.Runtime, "InvokePendingSemanticAction", command, capabilities, realmContext);
            AssertStatus(exactDuplicate, "Duplicate", "AL-NVS01-EVENT-DUPLICATE");
            Assert.AreSame(committedSnapshot, fixture.Snapshot);
            Assert.AreSame(request, Property(exactDuplicate, "EncounterRequest"));

            object logicalDuplicate = fixture.InvokePending(realm);
            AssertStatus(logicalDuplicate, "Duplicate", "AL-NVS01-EVENT-DUPLICATE");
            Assert.AreSame(committedSnapshot, fixture.Snapshot);
            Assert.AreSame(request, Property(logicalDuplicate, "EncounterRequest"));
            Assert.AreEqual(attempts, fixture.CommitAttempts);
            Assert.AreEqual(2, fixture.GuidCalls);
        }

        [TestCase("Crownlands", "crownlands")]
        [TestCase("Stonehold", "stonehold")]
        [TestCase("Eldergrove", "eldergrove")]
        [TestCase("Umbral", "umbral")]
        public void CommittedRealmAdapterEmitsOnlyCanonicalLaunchIds(string realm, string expectedLaunchId)
        {
            object context = AdaptRealmIdentity(
                EnumValue(RealmIdentityStatusType, "CommittedValid"),
                EnumValue(RealmIdType, realm),
                "0.1.0");

            Assert.AreEqual("CommittedValid", Property(context, "Status").ToString());
            Assert.AreEqual(expectedLaunchId, Property(context, "RealmId"));
            Assert.True((bool)Property(context, "IsCommittedValid"));
        }

        [Test]
        public void RealmAdapterFailsClosedForUnavailableUncommittedInvalidUndefinedAndStaleIdentity()
        {
            foreach (string status in new[] { "ProfileUnavailable", "CatalogUnavailable", "Uncommitted" })
            {
                object unavailable = AdaptRealmIdentity(
                    EnumValue(RealmIdentityStatusType, status),
                    EnumValue(RealmIdType, "Crownlands"),
                    "0.1.0");
                Assert.AreEqual("Unavailable", Property(unavailable, "Status").ToString(), status);
                Assert.AreEqual(string.Empty, Property(unavailable, "RealmId"), status);
            }

            foreach (object invalid in new[]
                     {
                         AdaptRealmIdentity(
                             EnumValue(RealmIdentityStatusType, "InvalidPersistedIdentity"),
                             EnumValue(RealmIdType, "Crownlands"),
                             "0.1.0"),
                         AdaptRealmIdentity(
                             EnumValue(RealmIdentityStatusType, "CommittedValid"),
                             EnumValue(RealmIdType, "None"),
                             "0.1.0"),
                         AdaptRealmIdentity(
                             EnumValue(RealmIdentityStatusType, "CommittedValid"),
                             Enum.ToObject(RealmIdType, 999),
                             "0.1.0"),
                         AdaptRealmIdentity(
                             Enum.ToObject(RealmIdentityStatusType, 999),
                             EnumValue(RealmIdType, "Crownlands"),
                             "0.1.0"),
                         AdaptRealmIdentity(
                             EnumValue(RealmIdentityStatusType, "CommittedValid"),
                             EnumValue(RealmIdType, "Crownlands"),
                             "0.0.9"),
                         AdaptRealmIdentity(
                             EnumValue(RealmIdentityStatusType, "CommittedValid"),
                             EnumValue(RealmIdType, "Crownlands"),
                             "0.1.0 ")
                     })
            {
                Assert.AreEqual("Invalid", Property(invalid, "Status").ToString());
                Assert.AreEqual(string.Empty, Property(invalid, "RealmId"));
                Assert.False((bool)Property(invalid, "IsCommittedValid"));
            }
        }

        [TestCase("CROWNLANDS")]
        [TestCase("Crownlands")]
        [TestCase("crownlandS")]
        [TestCase("unknown")]
        public void UppercaseMixedCaseAndUnknownLaunchRealmIdsFailClosed(string realm)
        {
            var fixture = new RuntimeFixture();
            object before = fixture.Snapshot;
            object rejected = fixture.SelectValerius("Offer", realm);

            AssertStatus(rejected, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(before, fixture.Snapshot);
            Assert.AreEqual(0, fixture.GuidCalls);
            Assert.AreEqual(0, fixture.CommitAttempts);
        }

        [Test]
        public void BlankOrExplicitlyInvalidLaunchRealmContextsFailClosed()
        {
            var fixture = new RuntimeFixture();
            Assert.Throws<ArgumentException>(() => fixture.Realm("CommittedValid", string.Empty));

            object before = fixture.Snapshot;
            object rejected = Invoke(
                fixture.Runtime,
                "SelectValerius",
                fixture.Command("NPC_VALERIUS", "POST_REALM_PROLOGUE"),
                EnumValue(InteractionKindType, "Offer"),
                fixture.Realm("Invalid", "crownlands"));

            AssertStatus(rejected, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(before, fixture.Snapshot);
        }

        [Test]
        public void MissingCapabilitiesOrRealmFailClosedBeforeRequestIdentityOrCommit()
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToArenaStart(false, "crownlands");
            object before = fixture.Snapshot;
            int attempts = fixture.CommitAttempts;

            foreach (string capability in RequiredCapabilities())
            {
                object command = fixture.Command("PLAYER", RequestArena);
                object unavailable = Invoke(
                    fixture.Runtime,
                    "InvokePendingSemanticAction",
                    command,
                    fixture.Capabilities(capability),
                    fixture.Realm("CommittedValid", "crownlands"));
                AssertStatus(unavailable, "DependencyUnavailable", "AL-NVS01-DEPENDENCY-UNAVAILABLE");
                Assert.AreSame(before, fixture.Snapshot, capability);
                Assert.AreEqual(0, fixture.GuidCalls, capability);
                Assert.AreEqual(attempts, fixture.CommitAttempts, capability);
            }

            object missingRealm = Invoke(
                fixture.Runtime,
                "InvokePendingSemanticAction",
                fixture.Command("PLAYER", RequestArena),
                fixture.AllCapabilities(),
                fixture.Realm("Unavailable", string.Empty));
            AssertStatus(missingRealm, "DependencyUnavailable", "AL-NVS01-DEPENDENCY-UNAVAILABLE");
            Assert.AreSame(before, fixture.Snapshot);

            object wrongRealm = Invoke(
                fixture.Runtime,
                "InvokePendingSemanticAction",
                fixture.Command("PLAYER", RequestArena),
                fixture.AllCapabilities(),
                fixture.Realm("CommittedValid", "stonehold"));
            AssertStatus(wrongRealm, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(before, fixture.Snapshot);

            object valid = fixture.InvokePending("crownlands");
            AssertCommitted(valid, 5, Investigate);
        }

        [TestCase("Success", Report, "Completed", "Completed", "Active", "", "", TearIntent)]
        [TestCase("Failure", Failed, "Completed", "Active", "Inactive", FailureDialogue, "", null)]
        [TestCase("Cancelled", Investigate, "Completed", "Active", "Inactive", "", RetryArena, null)]
        [TestCase("Unavailable", Investigate, "Completed", "Active", "Inactive", "", RetryArena, null)]
        public void EncounterOutcomeMatrixCommitsOnlyTheDeclaredStateAndIntents(
            string outcome,
            string state,
            string talkStatus,
            string arenaStatus,
            string reportStatus,
            string dialogue,
            string pendingAction,
            string expectedIntent)
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToRequest(false, "crownlands");
            object before = fixture.Snapshot;
            object request = Property(before, "CurrentEncounter");
            object result = fixture.Result(outcome, "snapshot-v1", "snapshot-ref-1");
            object disposition = fixture.ApplyResult(result);

            AssertCommitted(disposition, 6, state);
            Assert.AreNotSame(before, fixture.Snapshot);
            AssertSnapshot(fixture.Snapshot, 6, state, dialogue, outcome == "Failure", pendingAction, "crownlands", "Resolved");
            AssertObjectives(fixture.Snapshot, talkStatus, arenaStatus, reportStatus);
            Assert.IsNull(Property(fixture.Snapshot, "CurrentEncounter"));
            Assert.AreEqual(Property(request, "CorrelationId"), Property(fixture.Snapshot, "LastEncounterCorrelationId"));
            Assert.AreEqual(outcome, Property(fixture.Snapshot, "LastEncounterOutcome").ToString());
            Assert.AreEqual(Property(result, "EventId"), Property(fixture.Snapshot, "LastEncounterEventId"));
            Assert.AreEqual("snapshot-v1", Property(fixture.Snapshot, "LastEncounterSnapshotVersion"));
            Assert.AreEqual("snapshot-ref-1", Property(fixture.Snapshot, "LastEncounterSnapshotReference"));
            CollectionAssert.AreEqual(
                expectedIntent == null ? new string[0] : new[] { expectedIntent },
                StringItems(Property(disposition, "ConsequenceIntentIds")));
        }

        [Test]
        public void FailureRetryIsExplicitAndCreatesANewRequestOnlyAfterTheChoiceCommits()
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToRequest(false, "crownlands");
            string firstCorrelation = (string)Property(Property(fixture.Snapshot, "CurrentEncounter"), "CorrelationId");
            fixture.ApplyResult(fixture.Result("Failure"));

            object retryChoice = fixture.Choice("choice.omen1.retry");
            AssertCommitted(retryChoice, 7, Failed);
            AssertSnapshot(fixture.Snapshot, 7, Failed, FailureDialogue, false, RetryArena, "crownlands", "Resolved");
            Assert.IsNull(Property(fixture.Snapshot, "CurrentEncounter"));
            Assert.AreEqual(2, fixture.GuidCalls);

            object retryCommand = fixture.Command("PLAYER", RetryArena);
            object capabilities = fixture.AllCapabilities();
            object realm = fixture.Realm("CommittedValid", "crownlands");
            object retry = Invoke(
                fixture.Runtime,
                "InvokePendingSemanticAction",
                retryCommand,
                capabilities,
                realm);
            AssertCommitted(retry, 8, Investigate);
            AssertSnapshot(fixture.Snapshot, 8, Investigate, string.Empty, false, string.Empty, "crownlands", "Requested");
            object request = Property(fixture.Snapshot, "CurrentEncounter");
            Assert.AreEqual(GeneratedGuid(3), Property(request, "RequestId"));
            Assert.AreEqual(GeneratedGuid(4), Property(request, "CorrelationId"));
            Assert.AreNotEqual(firstCorrelation, Property(request, "CorrelationId"));
            CollectionAssert.IsEmpty(StringItems(Property(retry, "ConsequenceIntentIds")));

            object committedSnapshot = fixture.Snapshot;
            int attempts = fixture.CommitAttempts;
            object exactRetryDuplicate = Invoke(
                fixture.Runtime,
                "InvokePendingSemanticAction",
                retryCommand,
                capabilities,
                realm);
            AssertStatus(exactRetryDuplicate, "Duplicate", "AL-NVS01-EVENT-DUPLICATE");
            Assert.AreSame(committedSnapshot, fixture.Snapshot);
            Assert.AreSame(request, Property(exactRetryDuplicate, "EncounterRequest"));
            CollectionAssert.IsEmpty(StringItems(Property(exactRetryDuplicate, "ConsequenceIntentIds")));
            Assert.AreEqual(4, fixture.GuidCalls);
            Assert.AreEqual(attempts, fixture.CommitAttempts);
        }

        [Test]
        public void RetryRejectsAReusedPriorCorrelationDuringGenerationAndReconstruction()
        {
            var generated = new RuntimeFixture(
                null,
                new[] { GeneratedGuid(1), GeneratedGuid(2), GeneratedGuid(3), GeneratedGuid(2) });
            generated.AdvanceToRequest(false, "crownlands");
            generated.ApplyResult(generated.Result("Failure"));
            generated.Choice("choice.omen1.retry");
            object beforeRejectedRetry = generated.Snapshot;

            object rejectedRetry = generated.InvokePending("crownlands");
            AssertStatus(rejectedRetry, "Rejected", "AL-NVS01-TRANSITION-INVALID");
            Assert.AreSame(beforeRejectedRetry, generated.Snapshot);
            Assert.IsNull(Property(generated.Snapshot, "CurrentEncounter"));
            Assert.AreEqual(4, generated.GuidCalls);

            var persisted = new RuntimeFixture();
            persisted.AdvanceToRequest(false, "crownlands");
            persisted.ApplyResult(persisted.Result("Failure"));
            persisted.Choice("choice.omen1.retry");
            persisted.InvokePending("crownlands");
            object currentRequest = Property(persisted.Snapshot, "CurrentEncounter");
            object reusedCorrelationRequest = CloneRequestWithCorrelation(
                currentRequest,
                (string)Property(persisted.Snapshot, "LastEncounterCorrelationId"));
            object tampered = CloneSnapshot(
                persisted.Snapshot,
                "CurrentEncounter", reusedCorrelationRequest);

            AssertRuntimeReconstructionRejected(tampered);
        }

        [TestCase("Cancelled")]
        [TestCase("Unavailable")]
        public void CancelAndUnavailableExposeExplicitTechnicalReissueWithoutDialogue(string outcome)
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToRequest(false, "crownlands");
            object oldResult = fixture.Result(outcome);
            fixture.ApplyResult(oldResult);
            AssertSnapshot(fixture.Snapshot, 6, Investigate, string.Empty, false, RetryArena, "crownlands", "Resolved");
            Assert.IsNull(Property(fixture.Snapshot, "CurrentEncounter"));

            object reissue = fixture.InvokePending("crownlands");
            AssertCommitted(reissue, 7, Investigate);
            AssertSnapshot(fixture.Snapshot, 7, Investigate, string.Empty, false, string.Empty, "crownlands", "Requested");
            Assert.AreEqual(GeneratedGuid(4), Property(Property(fixture.Snapshot, "CurrentEncounter"), "CorrelationId"));

            object beforeLate = fixture.Snapshot;
            object late = fixture.ApplyResult(oldResult);
            AssertStatus(late, "Duplicate", "AL-NVS01-EVENT-DUPLICATE");
            Assert.AreSame(beforeLate, fixture.Snapshot);
            CollectionAssert.IsEmpty(StringItems(Property(late, "ConsequenceIntentIds")));
        }

        [Test]
        public void AbandonUsesAuthoritativeEncounterStateAndRetainsOnlyEarnedTombstones()
        {
            var clean = new RuntimeFixture();
            object cleanSnapshot = clean.Snapshot;
            object cleanAbandon = clean.Abandon(false);
            AssertStatus(cleanAbandon, "Duplicate", "AL-NVS01-EVENT-DUPLICATE");
            Assert.AreSame(cleanSnapshot, clean.Snapshot);

            var talk = new RuntimeFixture();
            talk.SelectValerius("Offer", "crownlands");
            talk.Choice("choice.omen1.accept");
            object reset = talk.Abandon(false);
            AssertCommitted(reset, 3, Offered);
            AssertSnapshot(talk.Snapshot, 3, Offered, string.Empty, false, string.Empty, "crownlands", "None");
            AssertObjectives(talk.Snapshot, "Active", "Inactive", "Inactive");

            var active = new RuntimeFixture();
            active.AdvanceToRequest(false, "crownlands");
            object activeSnapshot = active.Snapshot;
            object denied = active.Abandon(false);
            AssertStatus(denied, "Rejected", "AL-NVS01-TRANSITION-INVALID");
            Assert.AreSame(activeSnapshot, active.Snapshot);

            var failed = new RuntimeFixture();
            failed.AdvanceToRequest(false, "crownlands");
            failed.ApplyResult(failed.Result("Failure"));
            object failedSnapshot = failed.Snapshot;
            object assertionDenied = failed.Abandon(true);
            AssertStatus(assertionDenied, "Rejected", "AL-NVS01-TRANSITION-INVALID");
            Assert.AreSame(failedSnapshot, failed.Snapshot);
            failed.Abandon(false);
            Assert.AreEqual(Offered, Property(failed.Snapshot, "StateId"));
            Assert.AreEqual(string.Empty, Property(failed.Snapshot, "LastEncounterCorrelationId"));

            var earned = new RuntimeFixture();
            earned.AdvanceToRequest(false, "crownlands");
            earned.ApplyResult(earned.Result("Success"));
            earned.Abandon(false);
            AssertSnapshot(earned.Snapshot, 7, Offered, string.Empty, false, string.Empty, "crownlands", "None");
            CollectionAssert.AreEqual(new[] { TearIntent }, StringItems(Property(earned.Snapshot, "ConsequenceIntentIds")));
            AssertObjectives(earned.Snapshot, "Active", "Inactive", "Inactive");

            var terminal = new RuntimeFixture();
            terminal.AdvanceToCompleted();
            object terminalSnapshot = terminal.Snapshot;
            object terminalAbandon = terminal.Abandon(false);
            AssertStatus(terminalAbandon, "Rejected", "AL-NVS01-TRANSITION-INVALID");
            Assert.AreSame(terminalSnapshot, terminal.Snapshot);
        }

        [Test]
        public void ExactDuplicatePrecedesRevisionValidationButPayloadCollisionAndStaleCommandsReject()
        {
            var fixture = new RuntimeFixture();
            object command = fixture.Command("NPC_VALERIUS", "POST_REALM_PROLOGUE", timestamp: 9000);
            object realm = fixture.Realm("CommittedValid", "crownlands");
            object offer = Invoke(fixture.Runtime, "SelectValerius", command, EnumValue(InteractionKindType, "Offer"), realm);
            AssertCommitted(offer, 1, Offered);
            object committedSnapshot = fixture.Snapshot;
            int attempts = fixture.CommitAttempts;

            object duplicate = Invoke(fixture.Runtime, "SelectValerius", command, EnumValue(InteractionKindType, "Offer"), realm);
            AssertStatus(duplicate, "Duplicate", "AL-NVS01-EVENT-DUPLICATE");
            Assert.AreSame(committedSnapshot, fixture.Snapshot);
            CollectionAssert.IsEmpty(StringItems(Property(duplicate, "ConsequenceIntentIds")));

            object collision = Invoke(fixture.Runtime, "SelectValerius", command, EnumValue(InteractionKindType, "Report"), realm);
            AssertStatus(collision, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(committedSnapshot, fixture.Snapshot);

            object stale = fixture.Command(
                "PLAYER",
                OfferDialogue,
                expectedState: Offered,
                expectedRevision: 0,
                timestamp: 999999);
            object staleChoice = Invoke(fixture.Runtime, "SelectDialogueChoice", stale, "choice.omen1.accept");
            AssertStatus(staleChoice, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(committedSnapshot, fixture.Snapshot);

            object olderTimestamp = fixture.Command("PLAYER", OfferDialogue, timestamp: -1);
            object accepted = Invoke(fixture.Runtime, "SelectDialogueChoice", olderTimestamp, "choice.omen1.accept");
            AssertCommitted(accepted, 2, Talk);
            Assert.AreEqual(attempts + 1, fixture.CommitAttempts, "Diagnostic timestamps must not order commands.");
        }

        [Test]
        public void DuplicateLateAndMismatchedResultsNeverProgressOrRepublishIntents()
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToRequest(false, "crownlands");
            object request = Property(fixture.Snapshot, "CurrentEncounter");
            object activeSnapshot = fixture.Snapshot;

            foreach (object invalid in new[]
                     {
                         fixture.CreateResult(request, "Success", correlationId: GeneratedGuid(99)),
                         fixture.CreateResult(request, "Success", hookId: "HOOK_WRONG"),
                         fixture.CreateResult(request, "Success", realmId: "stonehold"),
                         fixture.CreateResult(request, "Success", eventId: ArenaFailure)
                     })
            {
                object rejected = fixture.ApplyResult(invalid);
                AssertStatus(rejected, "Rejected", "AL-NVS01-EVENT-MISMATCH");
                Assert.AreSame(activeSnapshot, fixture.Snapshot);
                CollectionAssert.IsEmpty(StringItems(Property(rejected, "ConsequenceIntentIds")));
            }

            object successResult = fixture.CreateResult(request, "Success", "snap-v1", "snap-ref-1");
            fixture.ApplyResult(successResult);
            fixture.SelectValerius("Report", "crownlands");
            object afterLaterCommand = fixture.Snapshot;

            object exactDuplicate = fixture.ApplyResult(successResult);
            AssertStatus(exactDuplicate, "Duplicate", "AL-NVS01-EVENT-DUPLICATE");
            Assert.AreSame(afterLaterCommand, fixture.Snapshot);
            CollectionAssert.IsEmpty(StringItems(Property(exactDuplicate, "ConsequenceIntentIds")));

            object changedSnapshotPayload = fixture.CreateResult(request, "Success", "snap-v1", "snap-ref-2");
            object snapshotMismatch = fixture.ApplyResult(changedSnapshotPayload);
            AssertStatus(snapshotMismatch, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(afterLaterCommand, fixture.Snapshot);

            object changedOutcome = fixture.CreateResult(request, "Failure");
            object outcomeMismatch = fixture.ApplyResult(changedOutcome);
            AssertStatus(outcomeMismatch, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(afterLaterCommand, fixture.Snapshot);

            object unknownCorrelation = fixture.CreateResult(request, "Success", correlationId: GeneratedGuid(100));
            object late = fixture.ApplyResult(unknownCorrelation);
            AssertStatus(late, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(afterLaterCommand, fixture.Snapshot);
        }

        [Test]
        public void PersistedSnapshotsRejectImpossibleDialogueAndResultTopology()
        {
            var failed = new RuntimeFixture();
            failed.AdvanceToRequest(false, "crownlands");
            failed.ApplyResult(failed.Result("Failure"));

            Assert.Throws<ArgumentException>(() => CloneSnapshot(
                failed.Snapshot,
                "PendingChoice", true,
                "PendingSemanticActionId", RetryArena));

            object missingFailureDialogue = CloneSnapshot(
                failed.Snapshot,
                "CurrentDialogueNodeId", string.Empty,
                "PendingChoice", false);
            AssertRuntimeReconstructionRejected(missingFailureDialogue);

            var talk = new RuntimeFixture();
            talk.SelectValerius("Offer", "crownlands");
            talk.Choice("choice.omen1.accept");
            object missingTalkDialogue = CloneSnapshot(
                talk.Snapshot,
                "CurrentDialogueNodeId", string.Empty,
                "PendingChoice", false);
            AssertRuntimeReconstructionRejected(missingTalkDialogue);

            var report = new RuntimeFixture();
            report.AdvanceToRequest(false, "crownlands");
            report.ApplyResult(report.Result("Success"));
            object failureMasqueradingAsReport = CloneSnapshot(
                report.Snapshot,
                "LastEncounterOutcome", EnumValue(EncounterOutcomeType, "Failure"),
                "LastEncounterEventId", ArenaFailure);
            AssertRuntimeReconstructionRejected(failureMasqueradingAsReport);
        }

        [Test]
        public void V002PacketHashAndRealmIdentitySnapshotsRequestsAndResultsFailClosed()
        {
            var progressed = new RuntimeFixture();
            progressed.SelectValerius("Offer", "crownlands");

            AssertRuntimeReconstructionRejected(CloneSnapshot(
                progressed.Snapshot,
                "PacketVersion", "omen1-a1-2026-07-22-v002"));
            AssertRuntimeReconstructionRejected(CloneSnapshot(
                progressed.Snapshot,
                "PacketSha256", "b22c166310617657cf9716f988e697d4c4992b4d1877b6fd4d0a3311af9a9a1f"));
            AssertRuntimeReconstructionRejected(CloneSnapshot(
                progressed.Snapshot,
                "CommittedRealmId", "CROWNLANDS"));

            var active = new RuntimeFixture();
            active.AdvanceToRequest(false, "crownlands");
            object request = Property(active.Snapshot, "CurrentEncounter");
            object staleRealmRequest = CloneRequestWithRealm(request, "CROWNLANDS");
            AssertRuntimeReconstructionRejected(CloneSnapshot(
                active.Snapshot,
                "CurrentEncounter", staleRealmRequest));

            object before = active.Snapshot;
            object staleRealmResult = active.CreateResult(request, "Success", realmId: "CROWNLANDS");
            object rejected = active.ApplyResult(staleRealmResult);
            AssertStatus(rejected, "Rejected", "AL-NVS01-EVENT-MISMATCH");
            Assert.AreSame(before, active.Snapshot);

            Assert.Throws<ArgumentOutOfRangeException>(() => CloneRequestWithContractVersion(request, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => active.CreateResult(request, "Success", contractVersion: 0));
        }

        [Test]
        public void RevisionOverflowAndInjectedCommitFailuresPreservePublishedSnapshotIdentity()
        {
            var seed = new RuntimeFixture();
            object maximumSnapshot = CloneSnapshotWithRevision(seed.Snapshot, long.MaxValue);
            var overflow = new RuntimeFixture(maximumSnapshot);
            object beforeOverflow = overflow.Snapshot;
            object overflowResult = overflow.SelectValerius("Offer", "crownlands");
            AssertStatus(overflowResult, "Rejected", "AL-NVS01-TRANSITION-INVALID");
            Assert.AreSame(beforeOverflow, overflow.Snapshot);
            Assert.AreEqual(0, overflow.CommitAttempts);
            Assert.AreEqual(0, overflow.GuidCalls);

            var requestFailure = new RuntimeFixture();
            requestFailure.AdvanceToArenaStart(false, "crownlands");
            object beforeRequest = requestFailure.Snapshot;
            object requestCommand = requestFailure.Command("PLAYER", RequestArena);
            requestFailure.FailNextCommit();
            object failedRequest = Invoke(
                requestFailure.Runtime,
                "InvokePendingSemanticAction",
                requestCommand,
                requestFailure.AllCapabilities(),
                requestFailure.Realm("CommittedValid", "crownlands"));
            AssertStatus(failedRequest, "CommitFailed", "AL-NVS01-SAVE-FAILED");
            Assert.AreSame(beforeRequest, requestFailure.Snapshot);
            Assert.IsNull(Property(failedRequest, "EncounterRequest"));
            CollectionAssert.IsEmpty(StringItems(Property(failedRequest, "ConsequenceIntentIds")));
            object retrySameCommand = Invoke(
                requestFailure.Runtime,
                "InvokePendingSemanticAction",
                requestCommand,
                requestFailure.AllCapabilities(),
                requestFailure.Realm("CommittedValid", "crownlands"));
            AssertCommitted(retrySameCommand, 5, Investigate);
            Assert.AreEqual(GeneratedGuid(3), Property(Property(retrySameCommand, "EncounterRequest"), "RequestId"));

            var resultFailure = new RuntimeFixture();
            resultFailure.AdvanceToRequest(false, "crownlands");
            object beforeResult = resultFailure.Snapshot;
            object success = resultFailure.Result("Success");
            resultFailure.FailNextCommit();
            object failedResult = resultFailure.ApplyResult(success);
            AssertStatus(failedResult, "CommitFailed", "AL-NVS01-SAVE-FAILED");
            Assert.AreSame(beforeResult, resultFailure.Snapshot);
            CollectionAssert.IsEmpty(StringItems(Property(failedResult, "ConsequenceIntentIds")));
            object committedResult = resultFailure.ApplyResult(success);
            AssertCommitted(committedResult, 6, Report);
            CollectionAssert.AreEqual(new[] { TearIntent }, StringItems(Property(committedResult, "ConsequenceIntentIds")));

            var reportFailure = new RuntimeFixture();
            reportFailure.AdvanceToReportDialogue();
            object beforeReport = reportFailure.Snapshot;
            object reportCommand = reportFailure.Command("PLAYER", ReportDialogue);
            reportFailure.FailNextCommit();
            object failedReport = Invoke(reportFailure.Runtime, "SelectDialogueChoice", reportCommand, "choice.omen1.present_tear");
            AssertStatus(failedReport, "CommitFailed", "AL-NVS01-SAVE-FAILED");
            Assert.AreSame(beforeReport, reportFailure.Snapshot);
            CollectionAssert.IsEmpty(StringItems(Property(failedReport, "ConsequenceIntentIds")));
            object committedReport = Invoke(reportFailure.Runtime, "SelectDialogueChoice", reportCommand, "choice.omen1.present_tear");
            AssertCommitted(committedReport, 8, Completed);
            CollectionAssert.AreEqual(ReportIntents, StringItems(Property(committedReport, "ConsequenceIntentIds")));
        }

        [Test]
        public void ChampionAdapterKeepsFreeEntryIsolatedAndMapsEveryBoundOutcome()
        {
            var freeFixture = new RuntimeFixture();
            object freeSnapshot = freeFixture.Snapshot;
            object freeAdapter = New(AdapterType);
            object[] freeBindArgs = { freeFixture.Runtime, null };
            Assert.False((bool)Invoke(freeAdapter, "TryBind", freeBindArgs));
            Assert.IsNull(freeBindArgs[1]);
            Assert.False((bool)Property(freeAdapter, "IsQuestEncounter"));
            Assert.True((bool)Property(freeAdapter, "CanUseFreeRetry"));
            Assert.IsNull(Property(freeAdapter, "Request"));
            Assert.IsNull(Invoke(freeAdapter, "PublishSuccess", string.Empty, string.Empty));
            Assert.IsNull(Invoke(freeAdapter, "PublishFailure"));
            Assert.IsNull(Invoke(freeAdapter, "PublishCancelled"));
            Assert.IsNull(Invoke(freeAdapter, "PublishUnavailable"));
            Assert.AreSame(freeSnapshot, freeFixture.Snapshot);

            foreach (string outcome in new[] { "Success", "Failure", "Cancelled", "Unavailable" })
            {
                var fixture = new RuntimeFixture();
                fixture.AdvanceToRequest(false, "crownlands");
                object request = Property(fixture.Snapshot, "CurrentEncounter");
                object adapter = New(AdapterType);
                object[] bindArgs = { fixture.Runtime, null };
                Assert.True((bool)Invoke(adapter, "TryBind", bindArgs), outcome);
                Assert.IsNull(bindArgs[1]);
                Assert.True((bool)Property(adapter, "IsQuestEncounter"));
                Assert.False((bool)Property(adapter, "CanUseFreeRetry"));
                Assert.AreSame(request, Property(adapter, "Request"));

                object disposition = Publish(adapter, outcome);
                string expectedState = outcome == "Success" ? Report : outcome == "Failure" ? Failed : Investigate;
                AssertCommitted(disposition, 6, expectedState);
                object committedSnapshot = fixture.Snapshot;
                object cached = Publish(adapter, outcome == "Success" ? "Failure" : "Success");
                Assert.AreSame(disposition, cached, outcome);
                Assert.AreSame(committedSnapshot, fixture.Snapshot, outcome);
            }
        }

        [Test]
        public void ChampionAdapterRetriesTheLockedResultAfterCommitFailureWithoutOutcomeSwap()
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToRequest(false, "crownlands");
            object before = fixture.Snapshot;
            object adapter = New(AdapterType);
            object[] bindArgs = { fixture.Runtime, null };
            Assert.True((bool)Invoke(adapter, "TryBind", bindArgs));

            fixture.FailNextCommit();
            object failed = Invoke(adapter, "PublishSuccess", "arena-v1", "snapshot://locked");
            AssertStatus(failed, "CommitFailed", "AL-NVS01-SAVE-FAILED");
            Assert.AreSame(before, fixture.Snapshot);

            object[] rebindArgs = { fixture.Runtime, null };
            Assert.True((bool)Invoke(adapter, "TryBind", rebindArgs));

            object retriedThroughDifferentMethod = Invoke(adapter, "PublishFailure");
            AssertCommitted(retriedThroughDifferentMethod, 6, Report);
            Assert.AreEqual("Success", Property(fixture.Snapshot, "LastEncounterOutcome").ToString());
            Assert.AreEqual("arena-v1", Property(fixture.Snapshot, "LastEncounterSnapshotVersion"));
            Assert.AreEqual("snapshot://locked", Property(fixture.Snapshot, "LastEncounterSnapshotReference"));
            CollectionAssert.AreEqual(new[] { TearIntent }, StringItems(Property(retriedThroughDifferentMethod, "ConsequenceIntentIds")));

            object cached = Invoke(adapter, "PublishUnavailable");
            Assert.AreSame(retriedThroughDifferentMethod, cached);
        }

        [Test]
        public void RuntimeContractsAreDeeplyImmutableBoundedAndFreeOfUnitySaveSceneOrTextAuthority()
        {
            var fixture = new RuntimeFixture();
            fixture.AdvanceToRequest(false, "crownlands");
            object snapshot = fixture.Snapshot;

            AssertReadOnlyList(Property(snapshot, "Objectives"));
            AssertReadOnlyList(Property(snapshot, "ConsequenceIntentIds"));
            AssertReadOnlyList(Property(NewDispositionForInspection(snapshot), "ConsequenceIntentIds"));

            var sourceCapabilities = RequiredCapabilities().ToDictionary(id => id, id => true, StringComparer.Ordinal);
            object capabilities = New(CapabilitySnapshotType, sourceCapabilities);
            sourceCapabilities.Clear();
            object availability = Property(capabilities, "Availability");
            Assert.AreEqual(7, ((IDictionary)availability).Count);
            AssertReadOnlyDictionary(availability);

            foreach (Type type in new[]
                     {
                         EncounterRequestType, EncounterResultType, CommandEnvelopeType, RealmContextType, RealmContextAdapterType,
                         CapabilitySnapshotType, ObjectiveSnapshotType, OperationReceiptType, SnapshotType,
                         DiagnosticType, DispositionType, MutationPlanType, QuestRuntimeType, AdapterType
                     })
            {
                Assert.False(typeof(UnityEngine.Object).IsAssignableFrom(type), type.FullName);
                Assert.False(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(property => property.CanWrite),
                    type.FullName + " exposes a mutable public property.");
            }

            object[] localizedArgs = { "quest.omen1.title", null };
            Assert.True((bool)Invoke(fixture.Runtime, "TryGetLocalizedText", localizedArgs));
            Assert.AreEqual(((IDictionary)Property(Property(fixture.Runtime, "Catalog"), "Localization"))["quest.omen1.title"], localizedArgs[1]);
            object[] missingArgs = { "quest.omen1.unknown", null };
            Assert.False((bool)Invoke(fixture.Runtime, "TryGetLocalizedText", missingArgs));

            string[] relativeFiles =
            {
                "AL/Scripts/Narrative/Nvs01/Contracts/NvsEncounterContracts.cs",
                "AL/Scripts/Narrative/Nvs01/INvs01QuestRuntime.cs",
                "AL/Scripts/Narrative/Nvs01/Nvs01QuestRuntime.cs",
                "AL/Scripts/ChampionMode/Narrative/Nvs01ChampionEncounterAdapter.cs"
            };
            string combinedSource = string.Join(
                "\n",
                relativeFiles.Select(path => File.ReadAllText(Path.Combine(Application.dataPath, path.Replace('/', Path.DirectorySeparatorChar)))));
            foreach (string prohibited in new[]
                     {
                         "UnityEngine", "MonoBehaviour", "SaveGameData", "ISaveGameService", "ServiceLocator",
                         "SceneManager", "LocalResourceService", "ReputationService", "ChangeAffinity(", "AddResource("
                     })
            {
                StringAssert.DoesNotContain(prohibited, combinedSource, prohibited);
            }

            foreach (DictionaryEntry localization in (IDictionary)Property(Property(fixture.Runtime, "Catalog"), "Localization"))
            {
                StringAssert.DoesNotContain(
                    (string)localization.Value,
                    combinedSource,
                    "Runtime source must not duplicate localization text for " + localization.Key + ".");
            }
        }

        private static object NewDispositionForInspection(object snapshot)
        {
            return New(DispositionType, EnumValue(CommandStatusType, "Rejected"), snapshot, null, null, new string[0]);
        }

        private static object Publish(object adapter, string outcome)
        {
            switch (outcome)
            {
                case "Success":
                    return Invoke(adapter, "PublishSuccess", "arena-v1", "snapshot://adapter");
                case "Failure":
                    return Invoke(adapter, "PublishFailure");
                case "Cancelled":
                    return Invoke(adapter, "PublishCancelled");
                case "Unavailable":
                    return Invoke(adapter, "PublishUnavailable");
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }

        private static void AssertCommitted(object disposition, long revision, string state)
        {
            AssertStatus(disposition, "Committed", null);
            Assert.AreEqual(revision, Property(Property(disposition, "Snapshot"), "Revision"));
            Assert.AreEqual(state, Property(Property(disposition, "Snapshot"), "StateId"));
            Assert.IsNull(Property(disposition, "Diagnostic"));
        }

        private static void AssertStatus(object disposition, string status, string diagnosticCode)
        {
            Assert.NotNull(disposition);
            Assert.AreEqual(status, Property(disposition, "Status").ToString(), DiagnosticSummary(Property(disposition, "Diagnostic")));
            object diagnostic = Property(disposition, "Diagnostic");
            if (diagnosticCode == null)
            {
                Assert.IsNull(diagnostic);
                return;
            }
            Assert.NotNull(diagnostic);
            Assert.AreEqual(diagnosticCode, Property(diagnostic, "Code"));
            Assert.AreEqual("omen1-a1-2026-08-13-v004", Property(diagnostic, "PacketVersion"));
            Assert.AreEqual(QuestId, Property(diagnostic, "QuestId"));
            Assert.False(string.IsNullOrWhiteSpace((string)Property(diagnostic, "StateId")));
        }

        private static string DiagnosticSummary(object diagnostic)
        {
            if (diagnostic == null) return string.Empty;
            return Property(diagnostic, "Code") + ": " + Property(diagnostic, "Message") +
                   " expected=" + Property(diagnostic, "Expected") + " actual=" + Property(diagnostic, "Actual");
        }

        private static void AssertSnapshot(
            object snapshot,
            long revision,
            string state,
            string dialogue,
            bool pendingChoice,
            string pendingAction,
            string realm,
            string encounterStatus)
        {
            Assert.AreEqual(revision, Property(snapshot, "Revision"));
            Assert.AreEqual(state, Property(snapshot, "StateId"));
            Assert.AreEqual(dialogue, Property(snapshot, "CurrentDialogueNodeId"));
            Assert.AreEqual(pendingChoice, Property(snapshot, "PendingChoice"));
            Assert.AreEqual(pendingAction, Property(snapshot, "PendingSemanticActionId"));
            Assert.AreEqual(realm, Property(snapshot, "CommittedRealmId"));
            Assert.AreEqual(encounterStatus, Property(snapshot, "EncounterStatus").ToString());
        }

        private static void AssertObjectives(object snapshot, string talk, string arena, string report)
        {
            var statuses = Items(Property(snapshot, "Objectives"))
                .ToDictionary(
                    item => (string)Property(item, "ObjectiveId"),
                    item => Property(item, "Status").ToString(),
                    StringComparer.Ordinal);
            Assert.AreEqual(talk, statuses[TalkObjective]);
            Assert.AreEqual(arena, statuses[ArenaObjective]);
            Assert.AreEqual(report, statuses[ReportObjective]);
            CollectionAssert.AreEqual(
                new[] { TalkObjective, ArenaObjective, ReportObjective },
                Items(Property(snapshot, "Objectives")).Select(item => Property(item, "ObjectiveId")).ToArray());
        }

        private static object CloneSnapshotWithRevision(object snapshot, long revision)
        {
            object lastOperation = Property(snapshot, "LastOperation");
            if (lastOperation != null)
            {
                lastOperation = New(
                    OperationReceiptType,
                    Property(lastOperation, "OperationId"),
                    Property(lastOperation, "PayloadFingerprint"),
                    Property(lastOperation, "Status"),
                    revision,
                    Property(lastOperation, "StateId"),
                    Property(lastOperation, "EventId"),
                    Property(lastOperation, "CorrelationId"));
            }
            else if (revision > 0)
            {
                lastOperation = New(
                    OperationReceiptType,
                    "00000000-0000-4000-8000-999999999999",
                    new string('a', 64),
                    EnumValue(CommandStatusType, "Committed"),
                    revision,
                    Property(snapshot, "StateId"),
                    "OVERFLOW_FIXTURE",
                    string.Empty);
            }

            string committedRealmId = (string)Property(snapshot, "CommittedRealmId");
            if (revision > 0 && committedRealmId.Length == 0) committedRealmId = "crownlands";

            return New(
                SnapshotType,
                Property(snapshot, "PacketVersion"),
                Property(snapshot, "PacketSha256"),
                Property(snapshot, "QuestId"),
                revision,
                Property(snapshot, "StateId"),
                Property(snapshot, "Objectives"),
                Property(snapshot, "CurrentDialogueNodeId"),
                Property(snapshot, "PendingChoice"),
                Property(snapshot, "PendingSemanticActionId"),
                committedRealmId,
                Property(snapshot, "EncounterStatus"),
                Property(snapshot, "CurrentEncounter"),
                Property(snapshot, "LastEncounterCorrelationId"),
                Property(snapshot, "LastEncounterOutcome"),
                Property(snapshot, "LastEncounterEventId"),
                Property(snapshot, "LastEncounterSnapshotVersion"),
                Property(snapshot, "LastEncounterSnapshotReference"),
                lastOperation,
                Property(snapshot, "ConsequenceIntentIds"));
        }

        private static object CloneSnapshot(object snapshot, params object[] replacements)
        {
            if (replacements == null || replacements.Length % 2 != 0)
                throw new ArgumentException("Snapshot replacements must be name/value pairs.", nameof(replacements));
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            for (var index = 0; index < replacements.Length; index += 2)
            {
                var name = replacements[index] as string;
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Snapshot replacement names must be non-empty.", nameof(replacements));
                values[name] = replacements[index + 1];
            }

            Func<string, object> read = name => values.ContainsKey(name) ? values[name] : Property(snapshot, name);
            return New(
                SnapshotType,
                read("PacketVersion"),
                read("PacketSha256"),
                read("QuestId"),
                read("Revision"),
                read("StateId"),
                read("Objectives"),
                read("CurrentDialogueNodeId"),
                read("PendingChoice"),
                read("PendingSemanticActionId"),
                read("CommittedRealmId"),
                read("EncounterStatus"),
                read("CurrentEncounter"),
                read("LastEncounterCorrelationId"),
                read("LastEncounterOutcome"),
                read("LastEncounterEventId"),
                read("LastEncounterSnapshotVersion"),
                read("LastEncounterSnapshotReference"),
                read("LastOperation"),
                read("ConsequenceIntentIds"));
        }

        private static object CloneRequestWithCorrelation(object request, string correlationId)
        {
            return New(
                EncounterRequestType,
                Property(request, "ContractVersion"),
                Property(request, "RequestId"),
                correlationId,
                Property(request, "QuestId"),
                Property(request, "StateId"),
                Property(request, "ObjectiveId"),
                Property(request, "HookId"),
                Property(request, "LocationId"),
                Property(request, "RealmId"),
                Property(request, "SuccessEventId"),
                Property(request, "FailureEventId"),
                Property(request, "CancelledEventId"),
                Property(request, "UnavailableEventId"),
                Property(request, "ReturnScene"));
        }

        private static object CloneRequestWithRealm(object request, string realmId)
        {
            return New(
                EncounterRequestType,
                Property(request, "ContractVersion"),
                Property(request, "RequestId"),
                Property(request, "CorrelationId"),
                Property(request, "QuestId"),
                Property(request, "StateId"),
                Property(request, "ObjectiveId"),
                Property(request, "HookId"),
                Property(request, "LocationId"),
                realmId,
                Property(request, "SuccessEventId"),
                Property(request, "FailureEventId"),
                Property(request, "CancelledEventId"),
                Property(request, "UnavailableEventId"),
                Property(request, "ReturnScene"));
        }

        private static object CloneRequestWithContractVersion(object request, int contractVersion)
        {
            return New(
                EncounterRequestType,
                contractVersion,
                Property(request, "RequestId"),
                Property(request, "CorrelationId"),
                Property(request, "QuestId"),
                Property(request, "StateId"),
                Property(request, "ObjectiveId"),
                Property(request, "HookId"),
                Property(request, "LocationId"),
                Property(request, "RealmId"),
                Property(request, "SuccessEventId"),
                Property(request, "FailureEventId"),
                Property(request, "CancelledEventId"),
                Property(request, "UnavailableEventId"),
                Property(request, "ReturnScene"));
        }

        private static object AdaptRealmIdentity(object status, object realmId, string catalogVersion)
        {
            object identity = New(
                RealmIdentitySnapshotType,
                status,
                realmId,
                catalogVersion,
                "AL-TEST-REALM-IDENTITY");
            return InvokeStatic(RealmContextAdapterType, "FromCommittedIdentity", identity);
        }

        private static void AssertRuntimeReconstructionRejected(object snapshot)
        {
            Assert.Throws<ArgumentException>(() => new RuntimeFixture(snapshot));
        }

        private static IEnumerable<string> RequiredCapabilities()
        {
            yield return ArenaLocation;
            yield return DeployCapability;
            yield return ArenaHook;
            yield return ArenaSuccess;
            yield return ArenaFailure;
            yield return ArenaCancelled;
            yield return ArenaUnavailable;
        }

        private static string[] StringItems(object collection) =>
            Items(collection).Select(item => (string)item).ToArray();

        private static object[] Items(object collection) =>
            ((IEnumerable)collection).Cast<object>().ToArray();

        private static void AssertReadOnlyList(object value)
        {
            var list = value as IList;
            Assert.NotNull(list, value.GetType().FullName);
            Assert.True(list.IsReadOnly, value.GetType().FullName);
            Assert.Throws<NotSupportedException>(() => list.Add(list.Count == 0 ? new object() : list[0]));
        }

        private static void AssertReadOnlyDictionary(object value)
        {
            var dictionary = value as IDictionary;
            Assert.NotNull(dictionary, value.GetType().FullName);
            Assert.True(dictionary.IsReadOnly, value.GetType().FullName);
            Assert.Throws<NotSupportedException>(() => dictionary.Add("new", true));
        }

        private static object VerifiedCatalog()
        {
            if (_verifiedCatalog != null) return _verifiedCatalog;
            string path = Path.Combine(
                Application.dataPath,
                "StreamingAssets/AL/Narrative/OMEN_1.catalog.json".Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), path);
            object result = InvokeStatic(ValidatorType, "ValidateCanonicalArtifact", File.ReadAllBytes(path));
            Assert.True((bool)Property(result, "IsAccepted"), string.Join("\n", Items(Property(result, "Diagnostics")).Select(DiagnosticSummary)));
            _verifiedCatalog = Property(result, "VerifiedCatalog");
            return _verifiedCatalog;
        }

        private static string GeneratedGuid(int value) =>
            "10000000-0000-4000-8000-" + value.ToString("D12");

        private static string OperationGuid(int value) =>
            "00000000-0000-4000-8000-" + value.ToString("D12");

        private static object EnumValue(Type type, string value) => Enum.Parse(type, value);

        private static object New(Type type, params object[] arguments)
        {
            try
            {
                return Activator.CreateInstance(
                    type,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    arguments,
                    null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static object Property(object target, string name)
        {
            Assert.NotNull(target, "Cannot inspect " + name + " on null.");
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, target.GetType().FullName + "." + name);
            return property.GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
            Assert.NotNull(method, target.GetType().FullName + "." + methodName + "(" + arguments.Length + ")");
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
            Assert.NotNull(method, type.FullName + "." + methodName + "(" + arguments.Length + ")");
            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static Type RuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, "Expected loaded runtime type " + fullName + ".");
            return type;
        }

        private static Type ValidatorType => RuntimeType("AL.Narrative.Nvs01.Nvs01CatalogValidator");
        private static Type QuestRuntimeType => RuntimeType("AL.Narrative.Nvs01.Nvs01QuestRuntime");
        private static Type CommitterType => RuntimeType("AL.Narrative.Nvs01.Nvs01InMemoryMutationCommitter");
        private static Type EncounterRequestType => RuntimeType("AL.Narrative.Nvs01.Contracts.NvsEncounterRequest");
        private static Type EncounterResultType => RuntimeType("AL.Narrative.Nvs01.Contracts.NvsEncounterResult");
        private static Type EncounterOutcomeType => RuntimeType("AL.Narrative.Nvs01.Contracts.NvsEncounterOutcome");
        private static Type CommandEnvelopeType => RuntimeType("AL.Narrative.Nvs01.Nvs01CommandEnvelope");
        private static Type InteractionKindType => RuntimeType("AL.Narrative.Nvs01.Nvs01InteractionKind");
        private static Type RealmContextType => RuntimeType("AL.Narrative.Nvs01.Nvs01RealmContext");
        private static Type RealmContextAdapterType => RuntimeType("AL.Narrative.Nvs01.Nvs01RealmContextAdapter");
        private static Type RealmContextStatusType => RuntimeType("AL.Narrative.Nvs01.Nvs01RealmContextStatus");
        private static Type RealmIdentitySnapshotType => RuntimeType("AL.RealmSelection.RealmIdentitySnapshot");
        private static Type RealmIdentityStatusType => RuntimeType("AL.RealmSelection.RealmIdentityStatus");
        private static Type RealmIdType => RuntimeType("AL.Core.RealmId");
        private static Type CapabilitySnapshotType => RuntimeType("AL.Narrative.Nvs01.Nvs01CapabilitySnapshot");
        private static Type ObjectiveSnapshotType => RuntimeType("AL.Narrative.Nvs01.Nvs01ObjectiveSnapshot");
        private static Type OperationReceiptType => RuntimeType("AL.Narrative.Nvs01.Nvs01OperationReceipt");
        private static Type SnapshotType => RuntimeType("AL.Narrative.Nvs01.Nvs01QuestSnapshot");
        private static Type DiagnosticType => RuntimeType("AL.Narrative.Nvs01.Nvs01RuntimeDiagnostic");
        private static Type DispositionType => RuntimeType("AL.Narrative.Nvs01.Nvs01CommandDisposition");
        private static Type CommandStatusType => RuntimeType("AL.Narrative.Nvs01.Nvs01CommandStatus");
        private static Type MutationPlanType => RuntimeType("AL.Narrative.Nvs01.Nvs01MutationPlan");
        private static Type AdapterType => RuntimeType("AL.ChampionMode.Narrative.Nvs01ChampionEncounterAdapter");

        private sealed class RuntimeFixture
        {
            private readonly Queue<string> _guids = new Queue<string>();
            private int _operationNumber;

            internal RuntimeFixture(object initialSnapshot = null, IEnumerable<string> generatedGuids = null)
            {
                if (generatedGuids == null)
                {
                    for (var index = 1; index <= 64; index++) _guids.Enqueue(GeneratedGuid(index));
                }
                else
                {
                    foreach (var guid in generatedGuids) _guids.Enqueue(guid);
                    for (var index = 65; index <= 128; index++) _guids.Enqueue(GeneratedGuid(index));
                }
                Committer = Activator.CreateInstance(CommitterType, true);
                var guidFactory = new Func<string>(NextGuid);
                ConstructorInfo constructor = QuestRuntimeType.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                    .Single(candidate => candidate.GetParameters().Length == 4);
                try
                {
                    Runtime = constructor.Invoke(new[] { VerifiedCatalog(), initialSnapshot, Committer, (object)guidFactory });
                }
                catch (TargetInvocationException exception)
                {
                    throw exception.InnerException ?? exception;
                }
            }

            internal object Runtime { get; }
            internal object Committer { get; }
            internal object Snapshot => Property(Runtime, "Snapshot");
            internal int GuidCalls { get; private set; }
            internal int CommitAttempts => (int)Property(Committer, "AttemptCount");

            internal object Command(
                string actor,
                string context,
                string operationId = null,
                string expectedState = null,
                long? expectedRevision = null,
                long? timestamp = null)
            {
                if (operationId == null) operationId = OperationGuid(++_operationNumber);
                return New(
                    CommandEnvelopeType,
                    1,
                    operationId,
                    QuestId,
                    expectedState ?? (string)Property(Snapshot, "StateId"),
                    expectedRevision ?? (long)Property(Snapshot, "Revision"),
                    actor,
                    context,
                    timestamp ?? _operationNumber * 1000L);
            }

            internal object Realm(string status, string realm) =>
                New(RealmContextType, EnumValue(RealmContextStatusType, status), realm);

            internal object AllCapabilities() => Capabilities(null);

            internal object Capabilities(string unavailable)
            {
                var values = RequiredCapabilities().ToDictionary(
                    id => id,
                    id => !string.Equals(id, unavailable, StringComparison.Ordinal),
                    StringComparer.Ordinal);
                return New(CapabilitySnapshotType, values);
            }

            internal object SelectValerius(string interaction, string realm)
            {
                return Invoke(
                    Runtime,
                    "SelectValerius",
                    Command("NPC_VALERIUS", "POST_REALM_PROLOGUE"),
                    EnumValue(InteractionKindType, interaction),
                    Realm("CommittedValid", realm));
            }

            internal object Choice(string choiceKey)
            {
                string node = (string)Property(Snapshot, "CurrentDialogueNodeId");
                return Invoke(Runtime, "SelectDialogueChoice", Command("PLAYER", node.Length == 0 ? QuestId : node), choiceKey);
            }

            internal object InvokePending(string realm)
            {
                string action = (string)Property(Snapshot, "PendingSemanticActionId");
                if (action.Length == 0) action = RequestArena;
                return Invoke(
                    Runtime,
                    "InvokePendingSemanticAction",
                    Command("PLAYER", action),
                    AllCapabilities(),
                    Realm("CommittedValid", realm));
            }

            internal object Abandon(bool encounterActive)
            {
                return Invoke(Runtime, "Abandon", Command("PLAYER", QuestId), encounterActive);
            }

            internal object Result(string outcome, string snapshotVersion = "", string snapshotReference = "")
            {
                return CreateResult(Property(Snapshot, "CurrentEncounter"), outcome, snapshotVersion, snapshotReference);
            }

            internal object CreateResult(
                object request,
                string outcome,
                string snapshotVersion = "",
                string snapshotReference = "",
                string correlationId = null,
                string hookId = null,
                string realmId = null,
                string eventId = null,
                int? contractVersion = null)
            {
                object outcomeValue = EnumValue(EncounterOutcomeType, outcome);
                if (eventId == null) eventId = (string)Invoke(request, "GetEventId", outcomeValue);
                return New(
                    EncounterResultType,
                    contractVersion ?? (int)Property(request, "ContractVersion"),
                    correlationId ?? Property(request, "CorrelationId"),
                    Property(request, "QuestId"),
                    hookId ?? Property(request, "HookId"),
                    realmId ?? Property(request, "RealmId"),
                    outcomeValue,
                    eventId,
                    snapshotVersion,
                    snapshotReference);
            }

            internal object ApplyResult(object result) => Invoke(Runtime, "ApplyEncounterResult", result);

            internal void AdvanceToArenaStart(bool lore, string realm)
            {
                AssertCommitted(SelectValerius("Offer", realm), 1, Offered);
                AssertCommitted(Choice("choice.omen1.accept"), 2, Talk);
                if (lore)
                {
                    AssertCommitted(Choice("choice.omen1.ask_more"), 3, Talk);
                    AssertCommitted(Choice("choice.omen1.depart"), 4, Talk);
                }
                else
                {
                    AssertCommitted(Choice("choice.omen1.investigate"), 3, Talk);
                }
                AssertCommitted(Choice("choice.omen1.deploy"), lore ? 5 : 4, Talk);
            }

            internal void AdvanceToRequest(bool lore, string realm)
            {
                AdvanceToArenaStart(lore, realm);
                AssertCommitted(InvokePending(realm), lore ? 6 : 5, Investigate);
            }

            internal void AdvanceToReportDialogue()
            {
                AdvanceToRequest(false, "crownlands");
                AssertCommitted(ApplyResult(Result("Success")), 6, Report);
                AssertCommitted(SelectValerius("Report", "crownlands"), 7, Report);
            }

            internal void AdvanceToCompleted()
            {
                AdvanceToReportDialogue();
                AssertCommitted(Choice("choice.omen1.present_tear"), 8, Completed);
            }

            internal void FailNextCommit()
            {
                Invoke(Committer, "FailNextCommitForTests", "SAVE-FAILED");
            }

            private string NextGuid()
            {
                GuidCalls++;
                return _guids.Dequeue();
            }
        }
    }
}
