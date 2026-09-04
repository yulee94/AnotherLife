using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01ProfileAuthorityPersistenceTests
    {
        private const string ProfileA =
            "alp_11111111111111111111111111111111";
        private const string ProfileB =
            "alp_22222222222222222222222222222222";
        private const string EpochA =
            "11111111111111110000000000000001";
        private const string EpochB =
            "11111111111111110000000000000002";
        private const string EpochC =
            "11111111111111110000000000000003";
        private const string EpochD =
            "11111111111111110000000000000004";
        private const string EpochE =
            "11111111111111110000000000000005";
        private static readonly string FingerprintA =
            ComputeEnvelopeFingerprint(
                JsonUtility.ToJson(IdentityAwareCandidate(ProfileA)),
                string.Empty);
        private const string FingerprintC =
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

        private Nvs01VerifiedCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = VerifiedCatalog();
        }

        [Test]
        public void CommitAndExactReplayBindTheCurrentTripleWithoutRewritingCausality()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA));
            var committer = new Nvs01SaveGameMutationCommitter(
                store,
                _catalog);

            Nvs01CommandDisposition first = Offer(committer);

            Assert.True(first.IsCommitted, first.Diagnostic?.Code);
            Assert.AreEqual(1, store.BoundCalls);
            Assert.AreEqual(0, store.LegacyCalls);
            AssertExpectation(
                store.Expectations[0],
                ProfileA,
                EpochB,
                FingerprintA);
            Assert.AreEqual(ProfileA, store.CallbackProfilesBefore[0]);
            Assert.AreEqual(ProfileA, store.CallbackProfilesAfter[0]);
            Assert.AreEqual(
                FingerprintA,
                store.Durable.Nvs01Progress.LastOperation
                    .ExpectedGenerationFingerprint);
            Assert.AreEqual(
                FingerprintA,
                first.Snapshot.LastOperation.ExpectedGenerationFingerprint);
            Assert.AreEqual(EpochC, store.LastReceipt.CommittedAuthorityEpoch);
            Nvs01OperationReceiptData durableReceipt =
                store.Durable.Nvs01Progress.LastOperation;
            Assert.AreEqual(
                durableReceipt.OperationId,
                store.LastReceipt.OperationId);
            Assert.AreEqual(
                durableReceipt.EventId,
                store.LastReceipt.ResultId);
            Assert.AreEqual(
                durableReceipt.PayloadFingerprint,
                store.LastReceipt.CommittedPayloadFingerprint);
            Assert.AreNotEqual(
                durableReceipt.EventId,
                durableReceipt.PayloadFingerprint,
                "Result identity and payload identity must remain distinct.");
            string serializedCandidate =
                JsonUtility.ToJson(store.Durable.Nvs01Progress);
            StringAssert.DoesNotContain(
                "\"AuthorityEpoch\"",
                serializedCandidate,
                "NVS01 process-local authority epochs must never enter NVS01 progress save bytes.");
            string entireSave = JsonUtility.ToJson(store.Durable);
            foreach (string processEpoch in new[] { EpochA, EpochB, EpochC })
            {
                StringAssert.DoesNotContain(processEpoch, entireSave,
                    "Process-local save authority tokens must not leak into any persisted domain.");
            }

            string committedFingerprint =
                store.Authority.VerifiedGenerationFingerprint;

            ScriptedProfileBoundStore restarted = store.Restarted(
                Writable(ProfileA, EpochC, committedFingerprint));
            var replayCommitter = new Nvs01SaveGameMutationCommitter(
                restarted,
                _catalog);
            Nvs01CommandDisposition replay = Offer(replayCommitter);

            Assert.True(replay.IsCommitted, replay.Diagnostic?.Code);
            Assert.AreEqual(1, restarted.BoundCalls);
            AssertExpectation(
                restarted.Expectations[0],
                ProfileA,
                EpochD,
                committedFingerprint);
            Assert.AreEqual(ProfileA, restarted.CallbackProfilesBefore[0]);
            Assert.AreEqual(ProfileA, restarted.CallbackProfilesAfter[0]);
            Assert.AreEqual(
                FingerprintA,
                replay.Snapshot.LastOperation
                    .ExpectedGenerationFingerprint,
                "Exact replay must adopt the already-persisted precommit witness.");
            Assert.AreEqual(
                FingerprintA,
                restarted.Durable.Nvs01Progress.LastOperation
                    .ExpectedGenerationFingerprint);
            Assert.AreEqual(1, store.DurableWriteCount);
            Assert.AreEqual(0, restarted.DurableWriteCount);
            Assert.AreEqual(EpochD, restarted.Authority.AuthorityEpoch);
            Assert.AreEqual(
                committedFingerprint,
                restarted.Authority.VerifiedGenerationFingerprint);
            Assert.AreEqual(
                0,
                typeof(ProfileMutationReceipt).GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public).Length,
                "Callback code cannot mint a trusted authority receipt.");
        }

        [Test]
        public void AlreadyBoundPlanCannotBeReboundOrReachTheStore()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA));
            var capture = new Nvs01InMemoryMutationCommitter();
            Assert.True(Offer(capture).IsCommitted);
            Nvs01MutationPlan unbound = capture.LastPlan;
            ProfileAuthorityExpectation expectation =
                ProfileAuthorityExpectation.From(store.Authority);
            Nvs01QuestSnapshot stamped = StampExpectedGeneration(
                unbound.Candidate,
                expectation.ExpectedGenerationFingerprint);
            Nvs01MutationPlan bound = unbound.BindAuthority(
                expectation,
                stamped);

            Assert.Throws<InvalidOperationException>(
                () => bound.BindAuthority(expectation, bound.Candidate));

            var committer = new Nvs01SaveGameMutationCommitter(
                store,
                _catalog);
            bool committed = committer.TryCommit(
                bound,
                out Nvs01QuestSnapshot published,
                out Nvs01RuntimeDiagnostic diagnostic);

            Assert.False(committed);
            Assert.AreSame(bound.Expected, published);
            Assert.AreEqual("AL-NVS01-SAVE-READ-ONLY", diagnostic.Code);
            Assert.AreEqual(0, store.BoundCalls);
            Assert.AreEqual(0, store.CallbackCalls);
            Assert.AreEqual(0, store.DurableWriteCount);
        }

        [Test]
        public void SameRuntimeImmediateReplayRechecksAuthorityWithoutAWrite()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA));
            var committer = new Nvs01SaveGameMutationCommitter(store, _catalog);
            Nvs01QuestRuntime runtime = CreateRuntime(committer, null);
            Nvs01CommandEnvelope command = OfferCommand(runtime);

            Nvs01CommandDisposition first = runtime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                CommittedRealm());
            string committedFingerprint =
                store.Authority.VerifiedGenerationFingerprint;
            Nvs01CommandDisposition replay = runtime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                CommittedRealm());

            Assert.True(first.IsCommitted, first.Diagnostic?.Code);
            Assert.AreEqual(Nvs01CommandStatus.Duplicate, replay.Status);
            Assert.AreEqual(2, store.BoundCalls);
            Assert.AreEqual(2, store.CallbackCalls);
            Assert.AreEqual(1, store.DurableWriteCount);
            AssertExpectation(
                store.Expectations[1],
                ProfileA,
                EpochC,
                committedFingerprint);
        }

        [TestCase("current")]
        [TestCase("stale")]
        [TestCase("revoked")]
        public void RehydratedImmediateReplayRequiresCurrentAuthority(
            string authorityState)
        {
            var original = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA));
            var originalCommitter = new Nvs01SaveGameMutationCommitter(
                original,
                _catalog);
            Nvs01QuestRuntime originalRuntime = CreateRuntime(
                originalCommitter,
                null);
            Nvs01CommandEnvelope command = OfferCommand(originalRuntime);
            Assert.True(originalRuntime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                CommittedRealm()).IsCommitted);
            string committedFingerprint =
                original.Authority.VerifiedGenerationFingerprint;
            Nvs01QuestSnapshot durable = Decode(
                original.Durable.Nvs01Progress);

            ScriptedProfileBoundStore restarted = original.Restarted(
                Writable(ProfileA, EpochC, committedFingerprint));
            if (string.Equals(
                    authorityState,
                    "stale",
                    StringComparison.Ordinal))
            {
                restarted.AuthorityAfterNextRead = Writable(
                    ProfileA,
                    EpochE,
                    committedFingerprint);
            }
            else if (string.Equals(
                         authorityState,
                         "revoked",
                         StringComparison.Ordinal))
            {
                restarted.ReportedAuthorityOverride =
                    ProfileWriteAuthoritySnapshotFactory.Unavailable(
                        "AL-SAVE-AUTH-REVOKED-TEST");
            }

            var replayCommitter = new Nvs01SaveGameMutationCommitter(
                restarted,
                _catalog);
            Nvs01QuestRuntime replayRuntime = CreateRuntime(
                replayCommitter,
                durable);
            Nvs01CommandDisposition replay = replayRuntime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                CommittedRealm());

            bool current = string.Equals(
                authorityState,
                "current",
                StringComparison.Ordinal);
            Assert.AreEqual(
                current
                    ? Nvs01CommandStatus.Duplicate
                    : Nvs01CommandStatus.CommitFailed,
                replay.Status,
                replay.Diagnostic?.Code);
            Assert.AreEqual(current ? 1 : 0, restarted.CallbackCalls);
            Assert.AreEqual(
                string.Equals(authorityState, "revoked", StringComparison.Ordinal)
                    ? 0
                    : 1,
                restarted.BoundCalls);
            Assert.AreEqual(0, restarted.DurableWriteCount);
            if (current)
            {
                AssertExpectation(
                    restarted.Expectations[0],
                    ProfileA,
                    EpochD,
                    committedFingerprint);
            }
        }

        [Test]
        public void RetainedRequestAndResultDuplicatesFailClosedWhenAuthorityIsRevoked()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA));
            var committer = new Nvs01SaveGameMutationCommitter(store, _catalog);
            Nvs01QuestRuntime runtime = CreateRuntime(committer, null);

            RequireCommitted(runtime.SelectValerius(
                Command(runtime, 1101, "NPC_VALERIUS", "POST_REALM_PROLOGUE"),
                Nvs01InteractionKind.Offer,
                CommittedRealm()));
            RequireCommitted(runtime.SelectDialogueChoice(
                Command(
                    runtime,
                    1102,
                    "PLAYER",
                    runtime.Snapshot.CurrentDialogueNodeId),
                "choice.omen1.accept"));
            RequireCommitted(runtime.SelectDialogueChoice(
                Command(
                    runtime,
                    1103,
                    "PLAYER",
                    runtime.Snapshot.CurrentDialogueNodeId),
                "choice.omen1.investigate"));
            RequireCommitted(runtime.SelectDialogueChoice(
                Command(
                    runtime,
                    1104,
                    "PLAYER",
                    runtime.Snapshot.CurrentDialogueNodeId),
                "choice.omen1.deploy"));
            RequireCommitted(runtime.InvokePendingSemanticAction(
                Command(
                    runtime,
                    1105,
                    "PLAYER",
                    runtime.Snapshot.PendingSemanticActionId),
                AllCapabilities(),
                CommittedRealm()));

            int requestWrites = store.DurableWriteCount;
            int requestBoundCalls = store.BoundCalls;
            store.ReportedAuthorityOverride =
                ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    "AL-SAVE-AUTH-REVOKED-REQUEST-TEST");
            Nvs01CommandDisposition retainedRequest =
                runtime.InvokePendingSemanticAction(
                    Command(
                        runtime,
                        1106,
                        "PLAYER",
                        runtime.Snapshot.LastOperation.EventId),
                    AllCapabilities(),
                    CommittedRealm());
            Assert.AreEqual(
                Nvs01CommandStatus.CommitFailed,
                retainedRequest.Status);
            Assert.AreEqual(requestWrites, store.DurableWriteCount);
            Assert.AreEqual(requestBoundCalls, store.BoundCalls);

            store.ReportedAuthorityOverride = null;
            NvsEncounterRequest request = runtime.Snapshot.CurrentEncounter;
            var result = new NvsEncounterResult(
                Nvs01RuntimeContract.ContractVersion,
                request.CorrelationId,
                request.QuestId,
                request.HookId,
                request.RealmId,
                NvsEncounterOutcome.Success,
                request.GetEventId(NvsEncounterOutcome.Success),
                "snapshot-v1",
                "snapshot://nvs01-authority-test");
            RequireCommitted(runtime.ApplyEncounterResult(result));

            int resultWrites = store.DurableWriteCount;
            int resultBoundCalls = store.BoundCalls;
            store.ReportedAuthorityOverride =
                ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    "AL-SAVE-AUTH-REVOKED-RESULT-TEST");
            Nvs01CommandDisposition retainedResult =
                runtime.ApplyEncounterResult(result);

            Assert.AreEqual(
                Nvs01CommandStatus.CommitFailed,
                retainedResult.Status);
            Assert.AreEqual(resultWrites, store.DurableWriteCount);
            Assert.AreEqual(resultBoundCalls, store.BoundCalls);

            store.ReportedAuthorityOverride = null;
            RequireCommitted(runtime.SelectValerius(
                Command(
                    runtime,
                    1107,
                    "NPC_VALERIUS",
                    "POST_REALM_PROLOGUE"),
                Nvs01InteractionKind.Report,
                CommittedRealm()));
            int postReportWrites = store.DurableWriteCount;
            int postReportBoundCalls = store.BoundCalls;
            store.ReportedAuthorityOverride =
                ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    "AL-SAVE-AUTH-REVOKED-LATE-RESULT-TEST");

            Nvs01CommandDisposition boundedLateNoOp =
                runtime.ApplyEncounterResult(result);

            Assert.AreEqual(
                Nvs01CommandStatus.Duplicate,
                boundedLateNoOp.Status);
            Assert.AreEqual(postReportWrites, store.DurableWriteCount);
            Assert.AreEqual(postReportBoundCalls, store.BoundCalls);
            CollectionAssert.IsEmpty(boundedLateNoOp.ConsequenceIntentIds);
        }

        [Test]
        public void CanonicalButUnwitnessedReplayCausalityIsRejected()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA));
            var committer = new Nvs01SaveGameMutationCommitter(store, _catalog);
            Assert.True(Offer(committer).IsCommitted);
            Assert.AreEqual(1, store.DurableWriteCount);

            string tamperedFingerprint =
                store.RewriteDurableExpectedGenerationFingerprint(
                    FingerprintC);

            ScriptedProfileBoundStore restarted = store.Restarted(
                Writable(ProfileA, EpochC, tamperedFingerprint));
            var replayCommitter = new Nvs01SaveGameMutationCommitter(
                restarted,
                _catalog);
            Nvs01CommandDisposition replay = Offer(replayCommitter);

            Assert.AreEqual(Nvs01CommandStatus.CommitFailed, replay.Status);
            Assert.AreEqual(1, restarted.BoundCalls);
            Assert.AreEqual(1, store.DurableWriteCount);
            Assert.AreEqual(0, restarted.DurableWriteCount);
            AssertExpectation(
                restarted.Expectations[0],
                ProfileA,
                EpochD,
                tamperedFingerprint);
        }

        [Test]
        public void TamperedDurableBytesWithAStaleGenerationFingerprintAreRejected()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA));
            var committer = new Nvs01SaveGameMutationCommitter(store, _catalog);
            Assert.True(Offer(committer).IsCommitted);
            string committedFingerprint =
                store.Authority.VerifiedGenerationFingerprint;

            store.RewriteDurableExpectedGenerationFingerprint(FingerprintC);
            ScriptedProfileBoundStore restarted = store.Restarted(
                Writable(ProfileA, EpochC, committedFingerprint));
            var replayCommitter = new Nvs01SaveGameMutationCommitter(
                restarted,
                _catalog);

            Nvs01CommandDisposition replay = Offer(replayCommitter);

            Assert.AreEqual(Nvs01CommandStatus.CommitFailed, replay.Status);
            Assert.AreEqual(0, restarted.BoundCalls);
            Assert.AreEqual(0, restarted.DurableWriteCount);
            Assert.AreNotEqual(
                ProfileWriteAuthorityStatus.Writable,
                restarted.Authority.Status);
        }

        [Test]
        public void FaultBeforeInstallPreservesPriorAndRestartRetriesOnce()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA))
            {
                FailNextBeforeInstall = true
            };
            var committer = new Nvs01SaveGameMutationCommitter(store, _catalog);

            Nvs01CommandDisposition faulted = Offer(committer);

            Assert.AreEqual(Nvs01CommandStatus.CommitFailed, faulted.Status);
            Assert.AreEqual(0, store.DurableWriteCount);
            Assert.AreEqual(0, store.Durable.Nvs01Progress.Revision);

            ScriptedProfileBoundStore restarted = store.Restarted(
                Writable(ProfileA, EpochB, FingerprintA));
            var retryCommitter = new Nvs01SaveGameMutationCommitter(
                restarted,
                _catalog);
            Nvs01CommandDisposition retried = Offer(retryCommitter);

            Assert.True(retried.IsCommitted, retried.Diagnostic?.Code);
            Assert.AreEqual(1, restarted.DurableWriteCount);
            Assert.AreEqual(1, restarted.Durable.Nvs01Progress.Revision);
            CollectionAssert.IsEmpty(
                restarted.Durable.Nvs01Progress.AcquiredArtifactIds);
            CollectionAssert.IsEmpty(
                restarted.Durable.Nvs01Progress.AppliedEffectKeys);
            Assert.AreEqual(
                string.Empty,
                restarted.Durable.Nvs01Progress.UnlockedChapterId);
        }

        [Test]
        public void InstalledButUncertainCommitReconcilesByExactReplayAfterRestart()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA))
            {
                CommitUncertainNextAfterInstall = true
            };
            var committer = new Nvs01SaveGameMutationCommitter(store, _catalog);

            Nvs01CommandDisposition uncertain = Offer(committer);

            Assert.AreEqual(Nvs01CommandStatus.CommitFailed, uncertain.Status);
            Assert.AreEqual(1, store.DurableWriteCount);
            Assert.AreEqual(1, store.Durable.Nvs01Progress.Revision);

            Assert.AreEqual(
                ProfileWriteAuthorityStatus.CommitUncertain,
                store.Authority.Status);
            string committedFingerprint = store.PersistedGenerationFingerprint;
            ScriptedProfileBoundStore restarted = store.Restarted(
                Writable(ProfileA, EpochC, committedFingerprint));
            var reconcileCommitter = new Nvs01SaveGameMutationCommitter(
                restarted,
                _catalog);
            Nvs01CommandDisposition reconciled = Offer(reconcileCommitter);

            Assert.True(reconciled.IsCommitted, reconciled.Diagnostic?.Code);
            Assert.AreEqual(1, store.DurableWriteCount);
            Assert.AreEqual(0, restarted.DurableWriteCount);
            Assert.AreEqual(1, restarted.BoundCalls);
            AssertExpectation(
                restarted.Expectations[0],
                ProfileA,
                EpochD,
                committedFingerprint);
            Assert.AreEqual(
                FingerprintA,
                reconciled.Snapshot.LastOperation
                    .ExpectedGenerationFingerprint);
        }

        [TestCase("profile")]
        [TestCase("epoch")]
        [TestCase("fingerprint")]
        public void AnyStaleAuthorityDimensionRejectsBeforeTheCandidateCallback(
            string changedDimension)
        {
            ProfileWriteAuthoritySnapshot changed;
            switch (changedDimension)
            {
                case "profile":
                    changed = Writable(ProfileB, EpochB, FingerprintA);
                    break;
                case "epoch":
                    changed = Writable(ProfileA, EpochC, FingerprintA);
                    break;
                case "fingerprint":
                    changed = Writable(ProfileA, EpochB, FingerprintC);
                    break;
                default:
                    Assert.Fail("Unknown stale-authority fixture.");
                    return;
            }

            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA))
            {
                AuthorityAfterNextRead = changed
            };
            var committer = new Nvs01SaveGameMutationCommitter(
                store,
                _catalog);

            Nvs01CommandDisposition result = Offer(committer);

            Assert.AreEqual(Nvs01CommandStatus.CommitFailed, result.Status);
            Assert.AreEqual(1, store.BoundCalls);
            Assert.AreEqual(0, store.CallbackCalls);
            AssertExpectation(
                store.Expectations[0],
                ProfileA,
                EpochB,
                FingerprintA);
            Assert.AreEqual(0, store.Durable.Nvs01Progress.Revision);
        }

        [TestCase("before")]
        [TestCase("after")]
        public void ProfileChangesAcrossTheCandidateBoundaryFailClosed(
            string timing)
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA))
            {
                TamperProfileBeforeCallback =
                    string.Equals(timing, "before", StringComparison.Ordinal),
                TamperProfileAfterCallback =
                    string.Equals(timing, "after", StringComparison.Ordinal)
            };
            var committer = new Nvs01SaveGameMutationCommitter(
                store,
                _catalog);

            Nvs01CommandDisposition result = Offer(committer);

            Assert.AreEqual(Nvs01CommandStatus.CommitFailed, result.Status);
            Assert.AreEqual(1, store.CallbackCalls);
            Assert.AreEqual(0, store.DurableWriteCount);
            Assert.AreEqual(ProfileA, store.Durable.ProfileId);
        }

        [Test]
        public void AuthorityDriftOnTheFinalRecheckPreventsPersistence()
        {
            var store = new ScriptedProfileBoundStore(
                IdentityAwareCandidate(ProfileA),
                Writable(ProfileA, EpochA, FingerprintA))
            {
                StaleOnFinalRecheck = true
            };
            var committer = new Nvs01SaveGameMutationCommitter(store, _catalog);

            Nvs01CommandDisposition result = Offer(committer);

            Assert.AreEqual(Nvs01CommandStatus.CommitFailed, result.Status);
            Assert.AreEqual(1, store.CallbackCalls);
            Assert.AreEqual(0, store.DurableWriteCount);
            Assert.AreEqual(0, store.Durable.Nvs01Progress.Revision);
            Assert.AreNotEqual(
                ProfileWriteAuthorityStatus.Writable,
                store.Authority.Status);
        }

        private Nvs01CommandDisposition Offer(
            INvs01MutationCommitter committer)
        {
            Nvs01QuestRuntime runtime = CreateRuntime(committer, null);
            return runtime.SelectValerius(
                OfferCommand(runtime),
                Nvs01InteractionKind.Offer,
                CommittedRealm());
        }

        private Nvs01QuestRuntime CreateRuntime(
            INvs01MutationCommitter committer,
            Nvs01QuestSnapshot initialSnapshot)
        {
            int nextGuid = 1;
            return new Nvs01QuestRuntime(
                _catalog,
                initialSnapshot,
                committer,
                () => "00000000-0000-0000-0000-" +
                      (nextGuid++).ToString("D12"));
        }

        private static Nvs01CommandEnvelope OfferCommand(
            Nvs01QuestRuntime runtime) =>
            new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                "00000000-0000-0000-0000-000000001000",
                Nvs01RuntimeContract.QuestId,
                runtime.Snapshot.StateId,
                runtime.Snapshot.Revision,
                "NPC_VALERIUS",
                "POST_REALM_PROLOGUE",
                0);

        private static Nvs01RealmContext CommittedRealm() =>
            new Nvs01RealmContext(
                Nvs01RealmContextStatus.CommittedValid,
                "crownlands");

        private static Nvs01CommandEnvelope Command(
            Nvs01QuestRuntime runtime,
            int operationId,
            string actorId,
            string contextId) =>
            new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                "00000000-0000-0000-0000-" +
                operationId.ToString("D12"),
                Nvs01RuntimeContract.QuestId,
                runtime.Snapshot.StateId,
                runtime.Snapshot.Revision,
                actorId,
                contextId,
                0);

        private static Nvs01CapabilitySnapshot AllCapabilities() =>
            new Nvs01CapabilitySnapshot(
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["LOCATION_SKY_CASTLE_MARKER"] = true,
                    ["ACTION_DEPLOY_CHAMPION"] = true,
                    ["HOOK_SKY_CASTLE_ARENA"] = true,
                    ["EVENT_SKY_CASTLE_ARENA_SUCCESS"] = true,
                    ["EVENT_SKY_CASTLE_ARENA_FAILURE"] = true,
                    ["EVENT_SKY_CASTLE_ARENA_CANCELLED"] = true,
                    ["EVENT_SKY_CASTLE_ARENA_UNAVAILABLE"] = true
                });

        private static void RequireCommitted(
            Nvs01CommandDisposition disposition)
        {
            Assert.True(
                disposition?.IsCommitted == true,
                disposition?.Diagnostic?.Code);
        }

        private Nvs01QuestSnapshot Decode(Nvs01ProgressData progress)
        {
            bool decoded = Nvs01ProgressCodec.TryDecode(
                progress,
                _catalog,
                out Nvs01QuestSnapshot snapshot,
                out Nvs01RuntimeDiagnostic diagnostic);
            Assert.True(decoded, diagnostic?.Code);
            return snapshot;
        }

        private Nvs01QuestSnapshot StampExpectedGeneration(
            Nvs01QuestSnapshot candidate,
            string expectedGenerationFingerprint)
        {
            Nvs01ProgressData encoded = Nvs01ProgressCodec.Encode(candidate);
            encoded.LastOperation.ExpectedGenerationFingerprint =
                expectedGenerationFingerprint;
            bool decoded = Nvs01ProgressCodec.TryDecode(
                encoded,
                _catalog,
                out Nvs01QuestSnapshot stamped,
                out Nvs01RuntimeDiagnostic diagnostic);
            Assert.True(decoded, diagnostic?.Code);
            return stamped;
        }

        private static SaveGameData IdentityAwareCandidate(string profileId)
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
                CurrentChapterId = "C1_CL",
                LastSavedTimestamp = 1
            };
        }

        private static ProfileWriteAuthoritySnapshot Writable(
            string profileId,
            string epoch,
            string fingerprint) =>
            ProfileWriteAuthoritySnapshotFactory.Writable(
                profileId,
                epoch,
                fingerprint,
                ProfileAuthoritySourceGeneration.Primary,
                Array.Empty<string>());

        private static void AssertExpectation(
            CapturedExpectation actual,
            string profileId,
            string epoch,
            string fingerprint)
        {
            Assert.AreEqual(profileId, actual.ProfileId);
            Assert.AreEqual(epoch, actual.AuthorityEpoch);
            Assert.AreEqual(fingerprint, actual.ExpectedGenerationFingerprint);
        }

        private static Nvs01VerifiedCatalog VerifiedCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                Nvs01CatalogContract.StreamingAssetsRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            Nvs01CatalogValidationResult validation =
                Nvs01CatalogValidator.ValidateCanonicalArtifact(
                    File.ReadAllBytes(path));
            Assert.True(
                validation.IsAccepted,
                string.Join(
                    Environment.NewLine,
                    validation.Diagnostics.Select(item => item.Code)));
            return validation.VerifiedCatalog;
        }

        private sealed class CapturedExpectation
        {
            internal CapturedExpectation(
                ProfileAuthorityExpectation source)
            {
                ProfileId = source.ProfileId;
                AuthorityEpoch = source.AuthorityEpoch;
                ExpectedGenerationFingerprint =
                    source.ExpectedGenerationFingerprint;
            }

            internal string ProfileId { get; }
            internal string AuthorityEpoch { get; }
            internal string ExpectedGenerationFingerprint { get; }
        }

        [Serializable]
        private sealed class TrustedReceiptRecord
        {
            public string OperationId;
            public string ResultId;
            public string PayloadFingerprint;
            public string ProfileId;
            public string ExpectedGenerationFingerprint;

            internal TrustedReceiptRecord(
                string operationId,
                string resultId,
                string payloadFingerprint,
                string profileId,
                string expectedGenerationFingerprint)
            {
                OperationId = operationId;
                ResultId = resultId;
                PayloadFingerprint = payloadFingerprint;
                ProfileId = profileId;
                ExpectedGenerationFingerprint =
                    expectedGenerationFingerprint;
            }
        }

        private sealed class CoreStoreState
        {
            private string _durableCandidateJson;
            private string _durableReceiptJson;

            internal SaveGameData Durable { get; private set; }
            internal string CurrentFingerprint;
            internal int DurableWriteCount;

            internal string PersistedGenerationFingerprint =>
                ComputeEnvelopeFingerprint(
                    _durableCandidateJson,
                    _durableReceiptJson);

            internal static CoreStoreState Initial(
                SaveGameData durable,
                string currentFingerprint)
            {
                string candidateJson = JsonUtility.ToJson(CloneSave(durable));
                return new CoreStoreState
                {
                    Durable = JsonUtility.FromJson<SaveGameData>(candidateJson),
                    _durableCandidateJson = candidateJson,
                    _durableReceiptJson = string.Empty,
                    CurrentFingerprint = currentFingerprint
                };
            }

            internal bool IsPersistedCandidate(SaveGameData candidate) =>
                candidate != null &&
                string.Equals(
                    JsonUtility.ToJson(candidate),
                    _durableCandidateJson,
                    StringComparison.Ordinal);

            internal TrustedReceiptRecord ReadPersistedReceipt() =>
                string.IsNullOrEmpty(_durableReceiptJson)
                    ? null
                    : JsonUtility.FromJson<TrustedReceiptRecord>(
                        _durableReceiptJson);

            internal void Install(
                SaveGameData candidate,
                TrustedReceiptRecord receipt)
            {
                _durableCandidateJson = JsonUtility.ToJson(candidate);
                _durableReceiptJson = JsonUtility.ToJson(receipt);
                Durable = JsonUtility.FromJson<SaveGameData>(
                    _durableCandidateJson);
                CurrentFingerprint = PersistedGenerationFingerprint;
                DurableWriteCount++;
            }

            internal string RewriteExpectedGenerationFingerprint(
                string expectedGenerationFingerprint)
            {
                SaveGameData tampered = CloneSave(Durable);
                tampered.Nvs01Progress.LastOperation
                    .ExpectedGenerationFingerprint =
                    expectedGenerationFingerprint;
                _durableCandidateJson = JsonUtility.ToJson(tampered);
                Durable = JsonUtility.FromJson<SaveGameData>(
                    _durableCandidateJson);
                return PersistedGenerationFingerprint;
            }

            internal CoreStoreState RestartedCopy()
            {
                return new CoreStoreState
                {
                    Durable = JsonUtility.FromJson<SaveGameData>(
                        _durableCandidateJson),
                    _durableCandidateJson = _durableCandidateJson,
                    _durableReceiptJson = _durableReceiptJson
                };
            }
        }

        private sealed class ScriptedProfileBoundStore :
            IProfileBoundSaveGameCandidateStore
        {
            private readonly CoreStoreState _state;
            private readonly CoreCandidatePersistence _persistence;
            private readonly SerializedAuthorityMutationBoundary<SaveGameData>
                _boundary;
            private ProfileWriteAuthoritySnapshot _staleAfterRead;

            internal ScriptedProfileBoundStore(
                SaveGameData durable,
                ProfileWriteAuthoritySnapshot initialAuthority)
                : this(
                    CoreStoreState.Initial(
                        durable,
                        initialAuthority.VerifiedGenerationFingerprint),
                    initialAuthority)
            {
            }

            private ScriptedProfileBoundStore(
                CoreStoreState state,
                ProfileWriteAuthoritySnapshot initialAuthority)
            {
                _state = state;
                var adapter = new CoreCandidateAdapter(_state);
                _persistence = new CoreCandidatePersistence(_state);
                _boundary = SerializedAuthorityMutationBoundary<SaveGameData>
                    .CreateForTesting(
                        initialAuthority,
                        _state.Durable,
                        adapter,
                        _persistence,
                        new AuthorityEpochAllocator(
                            new IncrementingEpochSource(
                                initialAuthority.AuthorityEpoch)),
                        new IgnoringReceiptSink(),
                        null,
                        new ProcessAuthorityMutationCoordinator());
            }

            internal SaveGameData Durable => _state.Durable;
            internal ProfileWriteAuthoritySnapshot Authority =>
                _boundary.GetCurrentAuthority();
            internal ProfileMutationReceipt LastReceipt { get; private set; }
            internal string PersistedGenerationFingerprint =>
                _state.PersistedGenerationFingerprint;
            internal ProfileWriteAuthoritySnapshot AuthorityAfterNextRead { get; set; }
            internal ProfileWriteAuthoritySnapshot ReportedAuthorityOverride { get; set; }
            internal bool FailNextBeforeInstall
            {
                get => _persistence.FailNextBeforeInstall;
                set => _persistence.FailNextBeforeInstall = value;
            }
            internal bool CommitUncertainNextAfterInstall
            {
                get => _persistence.CommitUncertainNextAfterInstall;
                set => _persistence.CommitUncertainNextAfterInstall = value;
            }
            internal bool StaleOnFinalRecheck
            {
                get => _persistence.StaleOnFinalRecheck;
                set => _persistence.StaleOnFinalRecheck = value;
            }
            internal bool TamperProfileBeforeCallback { get; set; }
            internal bool TamperProfileAfterCallback { get; set; }
            internal int LegacyCalls { get; private set; }
            internal int BoundCalls { get; private set; }
            internal int CallbackCalls { get; private set; }
            internal int DurableWriteCount => _state.DurableWriteCount;
            internal List<CapturedExpectation> Expectations { get; } =
                new List<CapturedExpectation>();
            internal List<string> CallbackProfilesBefore { get; } =
                new List<string>();
            internal List<string> CallbackProfilesAfter { get; } =
                new List<string>();

            internal string RewriteDurableExpectedGenerationFingerprint(
                string expectedGenerationFingerprint) =>
                _state.RewriteExpectedGenerationFingerprint(
                    expectedGenerationFingerprint);

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                if (ReportedAuthorityOverride != null)
                    return ReportedAuthorityOverride;
                ProfileWriteAuthoritySnapshot reported =
                    _boundary.GetCurrentAuthority();
                if (AuthorityAfterNextRead != null)
                {
                    _staleAfterRead = AuthorityAfterNextRead;
                    AuthorityAfterNextRead = null;
                }
                return reported;
            }

            public SaveCandidateCommitResult TryCommitCandidate(
                Func<SaveGameData, SaveCandidateMutationPreparation>
                    prepareCandidate)
            {
                LegacyCalls++;
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Rejected,
                    Durable,
                    "Legacy path is forbidden for a Writable profile.");
            }

            public ProfileBoundSaveCandidateCommitResult TryCommitCandidate(
                ProfileAuthorityExpectation expectation,
                string operationId,
                string resultId,
                Func<SaveGameData, SaveCandidateMutationPreparation>
                    prepareCandidate)
            {
                BoundCalls++;
                Expectations.Add(new CapturedExpectation(expectation));
                if (_staleAfterRead != null &&
                    !Matches(_staleAfterRead, expectation))
                {
                    return Map(
                        new ProfileMutationResult(
                            ProfileMutationStatus.StaleAuthority,
                            null,
                            "AL-SAVE-AUTH-STALE"));
                }

                ProfileMutationResult result = _boundary.TryMutate(
                    expectation,
                    operationId,
                    resultId,
                    candidate =>
                    {
                        if (TamperProfileBeforeCallback)
                            candidate.ProfileId = ProfileB;
                        CallbackProfilesBefore.Add(candidate.ProfileId);
                        CallbackCalls++;
                        SaveCandidateMutationPreparation preparation =
                            prepareCandidate(candidate);
                        CallbackProfilesAfter.Add(candidate.ProfileId);
                        if (TamperProfileAfterCallback)
                            candidate.ProfileId = ProfileB;
                        if (!string.Equals(
                                candidate.ProfileId,
                                expectation.ProfileId,
                                StringComparison.Ordinal))
                        {
                            return ProfileCandidatePreparation.Rejected(
                                "AL-NVS01-SAVE-AUTHORITY-CONFLICT");
                        }
                        switch (preparation.Disposition)
                        {
                            case SaveCandidateMutationDisposition.Prepared:
                                return ProfileCandidatePreparation.Prepared();
                            case SaveCandidateMutationDisposition.Duplicate:
                                return ProfileCandidatePreparation.ExactReplay();
                            default:
                                return ProfileCandidatePreparation.Rejected(
                                    "AL-NVS01-SAVE-CANDIDATE-REJECTED");
                        }
                    });
                return Map(result);
            }

            internal ScriptedProfileBoundStore Restarted(
                ProfileWriteAuthoritySnapshot reconciledAuthority)
            {
                CoreStoreState restarted = _state.RestartedCopy();
                restarted.CurrentFingerprint =
                    reconciledAuthority.VerifiedGenerationFingerprint;
                return new ScriptedProfileBoundStore(
                    restarted,
                    reconciledAuthority);
            }

            private ProfileBoundSaveCandidateCommitResult Map(
                ProfileMutationResult result)
            {
                SaveCandidateCommitOutcome outcome;
                ProfileMutationReceipt receipt = null;
                switch (result.Status)
                {
                    case ProfileMutationStatus.Committed:
                        outcome = SaveCandidateCommitOutcome.Committed;
                        receipt = result.Receipt;
                        break;
                    case ProfileMutationStatus.AlreadyCommitted:
                        outcome = SaveCandidateCommitOutcome.Duplicate;
                        receipt = result.Receipt;
                        break;
                    case ProfileMutationStatus.VerifiedRollback:
                        outcome = SaveCandidateCommitOutcome.PreviousPreserved;
                        break;
                    case ProfileMutationStatus.CommitUncertain:
                        outcome = SaveCandidateCommitOutcome.CommitUncertain;
                        break;
                    case ProfileMutationStatus.NotWritable:
                        outcome = SaveCandidateCommitOutcome.ReadOnly;
                        break;
                    default:
                        outcome = SaveCandidateCommitOutcome.Rejected;
                        break;
                }
                LastReceipt = receipt;
                return new ProfileBoundSaveCandidateCommitResult(
                    new SaveCandidateCommitResult(
                        outcome,
                        Durable,
                        result.DiagnosticCode),
                    receipt);
            }

            private static bool Matches(
                ProfileWriteAuthoritySnapshot current,
                ProfileAuthorityExpectation expectation) =>
                current != null && expectation != null &&
                string.Equals(
                    current.ProfileId,
                    expectation.ProfileId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    current.AuthorityEpoch,
                    expectation.AuthorityEpoch,
                    StringComparison.Ordinal) &&
                string.Equals(
                    current.VerifiedGenerationFingerprint,
                    expectation.ExpectedGenerationFingerprint,
                    StringComparison.Ordinal);
        }

        private sealed class CoreCandidateAdapter :
            IProfileMutationCandidateAdapter<SaveGameData>
        {
            private readonly CoreStoreState _state;

            internal CoreCandidateAdapter(CoreStoreState state)
            {
                _state = state;
            }

            public SaveGameData Clone(SaveGameData source) =>
                CloneSave(source);

            public ProfileCandidateValidationResult ValidatePublished(
                SaveGameData candidate,
                string expectedProfileId,
                string expectedGenerationFingerprint)
            {
                bool valid = candidate != null &&
                    string.Equals(
                        candidate.ProfileId,
                        expectedProfileId,
                        StringComparison.Ordinal) &&
                    _state.IsPersistedCandidate(candidate) &&
                    string.Equals(
                        _state.CurrentFingerprint,
                        expectedGenerationFingerprint,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        _state.PersistedGenerationFingerprint,
                        expectedGenerationFingerprint,
                        StringComparison.Ordinal);
                return valid
                    ? ProfileCandidateValidationResult.Valid()
                    : ProfileCandidateValidationResult.Invalid(
                        "AL-NVS01-SAVE-PUBLISHED-BINDING");
            }

            public ProfileCandidateValidationResult Validate(
                SaveGameData candidate,
                string expectedProfileId) =>
                candidate != null &&
                string.Equals(
                    candidate.ProfileId,
                    expectedProfileId,
                    StringComparison.Ordinal)
                    ? ProfileCandidateValidationResult.Valid()
                    : ProfileCandidateValidationResult.Invalid(
                        "AL-NVS01-SAVE-CANDIDATE-BINDING");

            public ProfileMutationReplayVerification VerifyReplay(
                SaveGameData publishedCandidate,
                string expectedProfileId,
                string expectedGenerationFingerprint,
                string operationId,
                string resultId)
            {
                TrustedReceiptRecord record =
                    _state.ReadPersistedReceipt();
                Nvs01OperationReceiptData operation = publishedCandidate?
                    .Nvs01Progress?.LastOperation;
                if (record == null ||
                    operation == null ||
                    !string.Equals(
                        publishedCandidate.ProfileId,
                        expectedProfileId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        _state.CurrentFingerprint,
                        expectedGenerationFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        _state.PersistedGenerationFingerprint,
                        expectedGenerationFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        record.ProfileId,
                        expectedProfileId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        record.OperationId,
                        operationId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        record.ResultId,
                        resultId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        operation.OperationId,
                        record.OperationId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        operation.EventId,
                        record.ResultId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        operation.PayloadFingerprint,
                        record.PayloadFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        operation.ExpectedGenerationFingerprint,
                        record.ExpectedGenerationFingerprint,
                        StringComparison.Ordinal))
                {
                    return ProfileMutationReplayVerification.Invalid(
                        "AL-NVS01-SAVE-REPLAY-NOT-VERIFIED");
                }

                return ProfileMutationReplayVerification.Verified(
                    record.ExpectedGenerationFingerprint,
                    record.PayloadFingerprint);
            }
        }

        private sealed class CoreCandidatePersistence :
            IProfileMutationPersistence<SaveGameData>
        {
            private readonly CoreStoreState _state;
            private int _checkCount;

            internal CoreCandidatePersistence(CoreStoreState state)
            {
                _state = state;
            }

            internal bool FailNextBeforeInstall { get; set; }
            internal bool CommitUncertainNextAfterInstall { get; set; }
            internal bool StaleOnFinalRecheck { get; set; }

            public ProfilePersistenceAuthorityCheck RecheckAuthority(
                ProfileMutationCommitContext context)
            {
                _checkCount++;
                if (StaleOnFinalRecheck && _checkCount == 2)
                    return ProfilePersistenceAuthorityCheck.Stale();
                return context != null &&
                       string.Equals(
                           context.ProfileId,
                           _state.Durable.ProfileId,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           context.ExpectedGenerationFingerprint,
                           _state.CurrentFingerprint,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           _state.CurrentFingerprint,
                           _state.PersistedGenerationFingerprint,
                           StringComparison.Ordinal)
                    ? ProfilePersistenceAuthorityCheck.Current()
                    : ProfilePersistenceAuthorityCheck.Stale();
            }

            public ProfileCandidatePersistenceResult<SaveGameData>
                PersistAndVerify(
                    SaveGameData candidate,
                    ProfileMutationCommitContext context)
            {
                if (FailNextBeforeInstall)
                {
                    FailNextBeforeInstall = false;
                    return ProfileCandidatePersistenceResult<SaveGameData>
                        .Rejected("AL-NVS01-SAVE-INJECTED-BEFORE-INSTALL");
                }

                Nvs01OperationReceiptData operation = candidate?
                    .Nvs01Progress?.LastOperation;
                if (operation == null ||
                    !string.Equals(
                        candidate.ProfileId,
                        context.ProfileId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        operation.ExpectedGenerationFingerprint,
                        context.ExpectedGenerationFingerprint,
                        StringComparison.Ordinal))
                {
                    return ProfileCandidatePersistenceResult<SaveGameData>
                        .Rejected("AL-NVS01-SAVE-CANDIDATE-INVALID");
                }

                var record = new TrustedReceiptRecord(
                    operation.OperationId,
                    operation.EventId,
                    operation.PayloadFingerprint,
                    context.ProfileId,
                    context.ExpectedGenerationFingerprint);
                _state.Install(candidate, record);
                if (CommitUncertainNextAfterInstall)
                {
                    CommitUncertainNextAfterInstall = false;
                    return ProfileCandidatePersistenceResult<SaveGameData>
                        .CommitUncertain(
                            "AL-NVS01-SAVE-INJECTED-COMMIT-UNCERTAIN");
                }

                return ProfileCandidatePersistenceResult<SaveGameData>
                    .Committed(
                        CloneSave(_state.Durable),
                        _state.CurrentFingerprint,
                        operation.PayloadFingerprint,
                        ProfileAuthoritySourceGeneration.Primary);
            }
        }

        private sealed class IncrementingEpochSource :
            IAuthorityEpochCandidateSource
        {
            private readonly string _nonce;
            private ulong _counter;

            internal IncrementingEpochSource(string initialEpoch)
            {
                _nonce = initialEpoch.Substring(0, 16);
                _counter = Convert.ToUInt64(
                    initialEpoch.Substring(16, 16),
                    16);
            }

            public bool TryGetNextCandidate(out string candidate)
            {
                _counter = checked(_counter + 1);
                candidate = _nonce + _counter.ToString("x16");
                return true;
            }
        }

        private sealed class IgnoringReceiptSink : IProfileMutationReceiptSink
        {
            public void Publish(ProfileMutationReceipt receipt)
            {
            }
        }

        private static string ComputeEnvelopeFingerprint(
            string candidateJson,
            string receiptJson)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(
                (candidateJson ?? string.Empty) + "\n" +
                (receiptJson ?? string.Empty));
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
                digest = sha256.ComputeHash(bytes);

            var canonical = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
                canonical.Append(value.ToString("x2"));
            return canonical.ToString();
        }

        private static SaveGameData CloneSave(SaveGameData source) =>
            JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(source));
    }
}
