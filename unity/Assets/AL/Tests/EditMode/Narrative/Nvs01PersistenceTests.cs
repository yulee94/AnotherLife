using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01PersistenceTests
    {
        private const string ProfileId =
            "alp_11111111111111111111111111111111";
        private string _saveRoot;
        private Nvs01VerifiedCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-Nvs01PersistenceTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            _catalog = VerifiedCatalog();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_saveRoot) &&
                Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }
        }

        [TestCase("neutral", 0, "OFFERED", "", false, "", "None", 0)]
        [TestCase("offer-pending", 1, "OFFERED", "DLG_OMEN_1_OFFER", true, "", "None", 0)]
        [TestCase("offer-deferred", 2, "OFFERED", "", false, "", "None", 0)]
        [TestCase("accepted", 2, "TALK_TO_VALERIUS", "DLG_OMEN_1_START", true, "", "None", 0)]
        [TestCase("lore", 3, "TALK_TO_VALERIUS", "DLG_OMEN_1_LORE", true, "", "None", 0)]
        [TestCase("before-request", 4, "TALK_TO_VALERIUS", "DLG_OMEN_1_ARENA_START", false, "REQUEST_SKY_CASTLE_ARENA", "None", 0)]
        [TestCase("request-saved", 5, "INVESTIGATE_SKY_CASTLE", "DLG_OMEN_1_ARENA_START", false, "", "Requested", 0)]
        [TestCase("failure", 6, "FAILED", "DLG_OMEN_1_FAILURE", true, "", "Resolved", 0)]
        [TestCase("retry-ready", 7, "FAILED", "DLG_OMEN_1_FAILURE", false, "RETRY_SKY_CASTLE_ARENA", "Resolved", 0)]
        [TestCase("retry-requested", 8, "INVESTIGATE_SKY_CASTLE", "", false, "", "Requested", 0)]
        [TestCase("cancelled", 6, "INVESTIGATE_SKY_CASTLE", "", false, "RETRY_SKY_CASTLE_ARENA", "Resolved", 0)]
        [TestCase("unavailable", 6, "INVESTIGATE_SKY_CASTLE", "", false, "RETRY_SKY_CASTLE_ARENA", "Resolved", 0)]
        [TestCase("success-before-report", 6, "REPORT_TO_VALERIUS", "", false, "", "Resolved", 1)]
        [TestCase("during-report", 7, "REPORT_TO_VALERIUS", "DLG_OMEN_1_REPORT", true, "", "Resolved", 1)]
        [TestCase("abandon-reaccept", 5, "TALK_TO_VALERIUS", "DLG_OMEN_1_START", true, "", "None", 0)]
        public void CoveredPreConsequenceD16StatesRoundTripThroughProductionDisk(
            string stage,
            long expectedRevision,
            string expectedState,
            string expectedDialogue,
            bool expectedPendingChoice,
            string expectedPendingAction,
            string expectedEncounterStatus,
            int expectedIntentCount)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(
                service,
                _catalog,
                initialSnapshot: null);

            AdvanceTo(session, stage);

            Nvs01QuestSnapshot before = session.Runtime.Snapshot;
            string persistedProgress =
                JsonUtility.ToJson(service.CurrentSave.Nvs01Progress);
            AssertSnapshot(
                before,
                expectedRevision,
                expectedState,
                expectedDialogue,
                expectedPendingChoice,
                expectedPendingAction,
                expectedEncounterStatus,
                expectedIntentCount);
            AssertNoC4ConsequenceState(service.CurrentSave.Nvs01Progress);
            AssertMigrationRequired(service);

            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.NotNull(reloaded.CurrentSave, reloaded.LastLoadMessage);
            Assert.AreEqual(
                persistedProgress,
                JsonUtility.ToJson(reloaded.CurrentSave.Nvs01Progress));
            Assert.AreEqual(string.Empty, reloaded.CurrentSave.ProfileId);
            AssertNoC4ConsequenceState(reloaded.CurrentSave.Nvs01Progress);
            AssertMigrationRequired(reloaded);

            Nvs01QuestSnapshot resumed = Decode(
                reloaded.CurrentSave.Nvs01Progress,
                _catalog);
            AssertSnapshot(
                resumed,
                expectedRevision,
                expectedState,
                expectedDialogue,
                expectedPendingChoice,
                expectedPendingAction,
                expectedEncounterStatus,
                expectedIntentCount);

            var resumedSession = new RuntimeSession(
                reloaded,
                _catalog,
                resumed);
            AssertSnapshot(
                resumedSession.Runtime.Snapshot,
                expectedRevision,
                expectedState,
                expectedDialogue,
                expectedPendingChoice,
                expectedPendingAction,
                expectedEncounterStatus,
                expectedIntentCount);
        }

        [TestCase("artifact")]
        [TestCase("effect")]
        [TestCase("chapter")]
        public void NonemptyC4ConsequenceStateRemainsReadOnlyAndBytePreserved(
            string consequenceField)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "offer-pending");

            switch (consequenceField)
            {
                case "artifact":
                    service.CurrentSave.Nvs01Progress.AcquiredArtifactIds.Add(
                        "ARTIFACT_CELESTIAL_TEAR");
                    break;
                case "effect":
                    service.CurrentSave.Nvs01Progress.AppliedEffectKeys.Add(
                        "OMEN_1:REPORT_COMPLETE:v1");
                    break;
                case "chapter":
                    service.CurrentSave.Nvs01Progress.UnlockedChapterId =
                        "C1_CL";
                    break;
                default:
                    Assert.Fail("Unknown consequence fixture.");
                    break;
            }

            byte[] exactBytes = WriteCanonicalGenerations(service.CurrentSave);
            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.IsNull(reloaded.CurrentSave);
            Assert.NotNull(reloaded.ReadOnlyCandidateSnapshot);
            Assert.False(reloaded.LastLoadDisposition.IsWritable);
            CollectionAssert.AreEqual(
                exactBytes,
                File.ReadAllBytes(Path.Combine(_saveRoot, "save.json")));
            Assert.AreNotEqual(
                ProfileWriteAuthorityStatus.Writable,
                ((IProfileWriteAuthorityProvider)reloaded)
                    .GetCurrentAuthority().Status);
        }

        [Test]
        public void SchemaV1ProfileIdentityAndAuthorityCausalMetadataStayEmpty()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "during-report");

            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                service.CurrentSave.SaveSchemaVersion);
            Assert.AreEqual(string.Empty, service.CurrentSave.ProfileId);
            Assert.AreEqual(
                string.Empty,
                service.CurrentSave.Nvs01Progress.LastOperation
                    .ExpectedGenerationFingerprint);
            AssertMigrationRequired(service);

            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();
            Assert.AreEqual(string.Empty, reloaded.CurrentSave.ProfileId);
            Assert.AreEqual(
                string.Empty,
                reloaded.CurrentSave.Nvs01Progress.LastOperation
                    .ExpectedGenerationFingerprint);
            AssertMigrationRequired(reloaded);
        }

        [Test]
        public void SchemaV1NvsAdapterRejectsCallerMutatedProfileIdentity()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            string primaryPath = Path.Combine(_saveRoot, "save.json");
            byte[] exactCleanPrimary = File.ReadAllBytes(primaryPath);
            SaveGameData published = service.CurrentSave;
            service.CurrentSave.ProfileId = ProfileId;
            string objectBefore = JsonUtility.ToJson(published);
            var runtime = new Nvs01QuestRuntime(
                _catalog,
                null,
                CreateSaveCommitter(service, _catalog),
                () => "00000000-0000-0000-0000-000000000137");
            var command = new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                "00000000-0000-0000-0000-000000013701",
                Nvs01RuntimeContract.QuestId,
                runtime.Snapshot.StateId,
                runtime.Snapshot.Revision,
                "NPC_VALERIUS",
                "POST_REALM_PROLOGUE",
                0);
            var realm = new Nvs01RealmContext(
                Nvs01RealmContextStatus.CommittedValid,
                "crownlands");

            Nvs01CommandDisposition result = runtime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                realm);

            Assert.False(result.IsCommitted);
            Assert.NotNull(result.Diagnostic);
            Assert.AreSame(published, service.CurrentSave);
            Assert.AreEqual(objectBefore, JsonUtility.ToJson(published));
            CollectionAssert.AreEqual(
                exactCleanPrimary,
                File.ReadAllBytes(primaryPath));

            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();
            Assert.NotNull(reloaded.CurrentSave);
            Assert.AreEqual(string.Empty, reloaded.CurrentSave.ProfileId);
            Assert.True(
                reloaded.LastLoadDisposition.IsWritable,
                "Schema-v1 bytes can be semantically writable while profile mutation authority remains MigrationRequired.");
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.MigrationRequired,
                ((IProfileWriteAuthorityProvider)reloaded)
                    .GetCurrentAuthority().Status);
            CollectionAssert.AreEqual(
                exactCleanPrimary,
                File.ReadAllBytes(primaryPath));
        }

        [TestCase("profile")]
        [TestCase("realm")]
        [TestCase("schema")]
        [TestCase("source")]
        [TestCase("generation")]
        public void LegacyNvsAdapterRejectsWrongAuthorityWithoutPublishing(
            string mismatch)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            SaveGameData published = service.CurrentSave;
            string primaryPath = Path.Combine(_saveRoot, "save.json");

            switch (mismatch)
            {
                case "profile":
                    published.ProfileId = ProfileId;
                    break;
                case "realm":
                    published.SelectedRealm = RealmId.Stonehold;
                    break;
                case "schema":
                    published.SaveSchemaVersion =
                        SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion;
                    break;
                case "source":
                    typeof(LocalSaveGameService).GetField(
                            "_observedAuthoritySource",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(
                            service,
                            ProfileAuthoritySourceGeneration.Backup);
                    break;
                case "generation":
                    File.WriteAllText(primaryPath, "{ external generation drift");
                    break;
                default:
                    Assert.Fail("Unknown mismatch fixture.");
                    break;
            }

            string objectBefore = JsonUtility.ToJson(published);
            byte[] diskBefore = File.ReadAllBytes(primaryPath);
            var runtime = new Nvs01QuestRuntime(
                _catalog,
                null,
                CreateSaveCommitter(service, _catalog),
                () => "00000000-0000-0000-0000-000000000137");
            var command = new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                "00000000-0000-0000-0000-000000013700",
                Nvs01RuntimeContract.QuestId,
                runtime.Snapshot.StateId,
                runtime.Snapshot.Revision,
                "NPC_VALERIUS",
                "POST_REALM_PROLOGUE",
                0);
            var realm = new Nvs01RealmContext(
                Nvs01RealmContextStatus.CommittedValid,
                "crownlands");

            Nvs01CommandDisposition result = runtime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                realm);

            Assert.False(result.IsCommitted);
            Assert.NotNull(result.Diagnostic);
            Assert.AreSame(published, service.CurrentSave);
            Assert.AreEqual(objectBefore, JsonUtility.ToJson(published));
            CollectionAssert.AreEqual(diskBefore, File.ReadAllBytes(primaryPath));
        }

        [Test]
        public void SchemaV1ReloadedExactOperationUsesTheLegacyDuplicateBoundary()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var runtime = new Nvs01QuestRuntime(
                _catalog,
                null,
                CreateSaveCommitter(service, _catalog),
                () => "00000000-0000-0000-0000-000000000001");
            var command = new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                "00000000-0000-0000-0000-000000009001",
                Nvs01RuntimeContract.QuestId,
                runtime.Snapshot.StateId,
                runtime.Snapshot.Revision,
                "NPC_VALERIUS",
                "POST_REALM_PROLOGUE",
                0);
            var realm = new Nvs01RealmContext(
                Nvs01RealmContextStatus.CommittedValid,
                "crownlands");
            Nvs01CommandDisposition first = runtime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                realm);
            Assert.True(first.IsCommitted, first.Diagnostic?.Code);
            string primaryPath = Path.Combine(_saveRoot, "save.json");
            byte[] exactCommittedBytes = File.ReadAllBytes(primaryPath);

            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();
            Nvs01QuestSnapshot durable = Decode(
                reloaded.CurrentSave.Nvs01Progress,
                _catalog);
            var replayRuntime = new Nvs01QuestRuntime(
                _catalog,
                durable,
                CreateSaveCommitter(reloaded, _catalog),
                () => "00000000-0000-0000-0000-000000000002");
            Nvs01CommandDisposition replay = replayRuntime.SelectValerius(
                command,
                Nvs01InteractionKind.Offer,
                realm);

            Assert.AreEqual(Nvs01CommandStatus.Duplicate, replay.Status);
            CollectionAssert.AreEqual(
                exactCommittedBytes,
                File.ReadAllBytes(primaryPath));
            AssertMigrationRequired(reloaded);
        }

        [Test]
        public void BoundOperationMetadataRequiresCanonicalExpectedFingerprint()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "offer-pending");
            service.CurrentSave.Nvs01Progress.LastOperation
                .ExpectedGenerationFingerprint = "NOT-A-FINGERPRINT";

            WriteCanonicalGenerations(service.CurrentSave);
            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.IsNull(reloaded.CurrentSave);
            Assert.NotNull(reloaded.ReadOnlyCandidateSnapshot);
            Assert.False(reloaded.LastLoadDisposition.IsWritable);
        }

        [TestCase("forward-nvs")]
        [TestCase("forward-save")]
        public void ForwardPersistenceIsReadOnlyAndBytePreserved(
            string forwardKind)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "request-saved");
            if (string.Equals(forwardKind, "forward-nvs", StringComparison.Ordinal))
            {
                service.CurrentSave.Nvs01Progress.Version =
                    Nvs01ProgressData.CurrentVersion + 1;
            }
            else
            {
                service.CurrentSave.SaveSchemaVersion =
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion + 1;
            }

            byte[] exactBytes = WriteCanonicalGenerations(service.CurrentSave);
            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.IsNull(reloaded.CurrentSave);
            if (string.Equals(forwardKind, "forward-nvs", StringComparison.Ordinal))
            {
                Assert.NotNull(reloaded.ReadOnlyCandidateSnapshot);
            }
            else
            {
                Assert.IsNull(reloaded.ReadOnlyCandidateSnapshot);
                Assert.AreEqual(
                    SaveLoadStatus.LoadedForwardSchemaReadOnly,
                    reloaded.LastLoadStatus);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.ForwardSchemaReadOnly,
                    ((IProfileWriteAuthorityProvider)reloaded)
                        .GetCurrentAuthority().Status);
                StringAssert.Contains(
                    "AL-SAVE-FORWARD-SCHEMA-READ-ONLY",
                    reloaded.LastLoadMessage);
            }

            Assert.False(reloaded.LastLoadDisposition.IsWritable);
            CollectionAssert.AreEqual(
                exactBytes,
                File.ReadAllBytes(Path.Combine(_saveRoot, "save.json")));
            Assert.AreNotEqual(
                ProfileWriteAuthorityStatus.Writable,
                ((IProfileWriteAuthorityProvider)reloaded)
                    .GetCurrentAuthority().Status);
        }

        [Test]
        public void CorruptPrimaryReconcilesToTheExactPriorPreConsequenceBackup()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "request-saved");

            string primaryPath = Path.Combine(_saveRoot, "save.json");
            string backupPath = Path.Combine(_saveRoot, "save.backup.json");
            byte[] exactBackup = File.ReadAllBytes(backupPath);
            File.WriteAllText(primaryPath, "{ interrupted primary");

            LocalSaveGameService recovered = CreateSaveService(_saveRoot);
            recovered.Load();

            Assert.NotNull(recovered.CurrentSave, recovered.LastLoadMessage);
            Assert.AreEqual(
                SaveLoadStatus.RecoveredFromBackup,
                recovered.LastLoadStatus);
            CollectionAssert.AreEqual(
                exactBackup,
                File.ReadAllBytes(primaryPath));
            Nvs01QuestSnapshot resumed = Decode(
                recovered.CurrentSave.Nvs01Progress,
                _catalog);
            AssertSnapshot(
                resumed,
                4,
                "TALK_TO_VALERIUS",
                "DLG_OMEN_1_ARENA_START",
                false,
                "REQUEST_SKY_CASTLE_ARENA",
                "None",
                0);
            AssertNoC4ConsequenceState(recovered.CurrentSave.Nvs01Progress);
            AssertMigrationRequired(recovered);

            LocalSaveGameService secondLoad = CreateSaveService(_saveRoot);
            secondLoad.Load();
            Assert.NotNull(secondLoad.CurrentSave, secondLoad.LastLoadMessage);
            Assert.AreEqual(
                4,
                Decode(secondLoad.CurrentSave.Nvs01Progress, _catalog)
                    .Revision);
            AssertMigrationRequired(secondLoad);
        }

        private static void AdvanceTo(RuntimeSession session, string stage)
        {
            if (string.Equals(stage, "neutral", StringComparison.Ordinal))
                return;

            session.SelectValerius(Nvs01InteractionKind.Offer);
            if (string.Equals(stage, "offer-pending", StringComparison.Ordinal))
                return;

            if (string.Equals(stage, "offer-deferred", StringComparison.Ordinal))
            {
                session.Choice("choice.omen1.decline");
                return;
            }

            session.Choice("choice.omen1.accept");
            if (string.Equals(stage, "accepted", StringComparison.Ordinal))
                return;

            if (string.Equals(stage, "abandon-reaccept", StringComparison.Ordinal))
            {
                session.Abandon();
                session.SelectValerius(Nvs01InteractionKind.Offer);
                session.Choice("choice.omen1.accept");
                return;
            }

            if (string.Equals(stage, "lore", StringComparison.Ordinal))
            {
                session.Choice("choice.omen1.ask_more");
                return;
            }

            session.Choice("choice.omen1.investigate");
            session.Choice("choice.omen1.deploy");
            if (string.Equals(stage, "before-request", StringComparison.Ordinal))
                return;

            session.InvokePending();
            if (string.Equals(stage, "request-saved", StringComparison.Ordinal))
                return;

            if (string.Equals(stage, "success-before-report", StringComparison.Ordinal) ||
                string.Equals(stage, "during-report", StringComparison.Ordinal))
            {
                session.ApplyResult(NvsEncounterOutcome.Success);
                if (string.Equals(stage, "during-report", StringComparison.Ordinal))
                    session.SelectValerius(Nvs01InteractionKind.Report);
                return;
            }

            if (string.Equals(stage, "cancelled", StringComparison.Ordinal))
            {
                session.ApplyResult(NvsEncounterOutcome.Cancelled);
                return;
            }

            if (string.Equals(stage, "unavailable", StringComparison.Ordinal))
            {
                session.ApplyResult(NvsEncounterOutcome.Unavailable);
                return;
            }

            session.ApplyResult(NvsEncounterOutcome.Failure);
            if (string.Equals(stage, "failure", StringComparison.Ordinal))
                return;

            session.Choice("choice.omen1.retry");
            if (string.Equals(stage, "retry-ready", StringComparison.Ordinal))
                return;

            if (string.Equals(stage, "retry-requested", StringComparison.Ordinal))
            {
                session.InvokePending();
                return;
            }

            Assert.Fail("Unknown D16 persistence stage: " + stage);
        }

        private static void AssertSnapshot(
            Nvs01QuestSnapshot snapshot,
            long revision,
            string state,
            string dialogue,
            bool pendingChoice,
            string pendingAction,
            string encounterStatus,
            int intentCount)
        {
            Assert.NotNull(snapshot);
            Assert.AreEqual(revision, snapshot.Revision);
            Assert.AreEqual(state, snapshot.StateId);
            Assert.AreEqual(dialogue, snapshot.CurrentDialogueNodeId);
            Assert.AreEqual(pendingChoice, snapshot.PendingChoice);
            Assert.AreEqual(pendingAction, snapshot.PendingSemanticActionId);
            Assert.AreEqual(encounterStatus, snapshot.EncounterStatus.ToString());
            Assert.AreEqual(intentCount, snapshot.ConsequenceIntentIds.Count);
        }

        private static void AssertNoC4ConsequenceState(
            Nvs01ProgressData progress)
        {
            CollectionAssert.IsEmpty(progress.AcquiredArtifactIds);
            CollectionAssert.IsEmpty(progress.AppliedEffectKeys);
            Assert.AreEqual(string.Empty, progress.UnlockedChapterId);
        }

        private static void AssertMigrationRequired(
            LocalSaveGameService service)
        {
            ProfileWriteAuthoritySnapshot authority =
                ((IProfileWriteAuthorityProvider)service)
                .GetCurrentAuthority();
            Assert.NotNull(authority);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.MigrationRequired,
                authority.Status);
            Assert.AreEqual(string.Empty, authority.ProfileId);
            Assert.AreEqual(string.Empty, authority.AuthorityEpoch);
            Assert.AreEqual(
                string.Empty,
                authority.VerifiedGenerationFingerprint);
        }

        private Nvs01QuestSnapshot Decode(
            Nvs01ProgressData progress,
            Nvs01VerifiedCatalog catalog)
        {
            Type codec = typeof(Nvs01QuestRuntime).Assembly.GetType(
                "AL.Narrative.Nvs01.Nvs01ProgressCodec",
                throwOnError: true);
            MethodInfo method = codec.GetMethod(
                "TryDecode",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            object[] arguments = { progress, catalog, null, null };
            bool decoded = (bool)method.Invoke(null, arguments);
            Assert.True(
                decoded,
                arguments[3] == null
                    ? "NVS-01 progress decode failed."
                    : ((Nvs01RuntimeDiagnostic)arguments[3]).Code);
            return (Nvs01QuestSnapshot)arguments[2];
        }

        private static INvs01MutationCommitter CreateSaveCommitter(
            LocalSaveGameService service,
            Nvs01VerifiedCatalog catalog)
        {
            Assembly runtime = typeof(Nvs01QuestRuntime).Assembly;
            Type committer = runtime.GetType(
                "AL.Narrative.Nvs01.Nvs01SaveGameMutationCommitter",
                throwOnError: true);
            Type store = runtime.GetType(
                "AL.Services.Local.ISaveGameCandidateStore",
                throwOnError: true);
            ConstructorInfo constructor = committer.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { store, typeof(Nvs01VerifiedCatalog) },
                modifiers: null);
            Assert.NotNull(constructor);
            return (INvs01MutationCommitter)constructor.Invoke(
                new object[] { service, catalog });
        }

        private static LocalSaveGameService CreateSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root });
        }

        private byte[] WriteCanonicalGenerations(SaveGameData save)
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(
                JsonUtility.ToJson(save, true));
            File.WriteAllBytes(Path.Combine(_saveRoot, "save.json"), bytes);
            File.WriteAllBytes(
                Path.Combine(_saveRoot, "save.backup.json"),
                bytes);
            return bytes;
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

        private sealed class RuntimeSession
        {
            private readonly Nvs01RealmContext _realm =
                new Nvs01RealmContext(
                    Nvs01RealmContextStatus.CommittedValid,
                    "crownlands");
            private int _commandId = 1000;
            private int _runtimeGuidId = 1;

            internal RuntimeSession(
                LocalSaveGameService service,
                Nvs01VerifiedCatalog catalog,
                Nvs01QuestSnapshot initialSnapshot)
            {
                Runtime = new Nvs01QuestRuntime(
                    catalog,
                    initialSnapshot,
                    CreateSaveCommitter(service, catalog),
                    NextRuntimeGuid);
            }

            internal Nvs01QuestRuntime Runtime { get; }

            internal Nvs01CommandDisposition SelectValerius(
                Nvs01InteractionKind kind) =>
                RequireCommitted(Runtime.SelectValerius(
                    Command("NPC_VALERIUS", "POST_REALM_PROLOGUE"),
                    kind,
                    _realm));

            internal Nvs01CommandDisposition Choice(string choice) =>
                RequireCommitted(Runtime.SelectDialogueChoice(
                    Command("PLAYER", Runtime.Snapshot.CurrentDialogueNodeId),
                    choice));

            internal Nvs01CommandDisposition InvokePending() =>
                RequireCommitted(Runtime.InvokePendingSemanticAction(
                    Command(
                        "PLAYER",
                        Runtime.Snapshot.PendingSemanticActionId),
                    AllCapabilities(),
                    _realm));

            internal Nvs01CommandDisposition ApplyResult(
                NvsEncounterOutcome outcome)
            {
                NvsEncounterRequest request = Runtime.Snapshot.CurrentEncounter;
                Assert.NotNull(request);
                return RequireCommitted(Runtime.ApplyEncounterResult(
                    new NvsEncounterResult(
                        Nvs01RuntimeContract.ContractVersion,
                        request.CorrelationId,
                        request.QuestId,
                        request.HookId,
                        request.RealmId,
                        outcome,
                        request.GetEventId(outcome),
                        "snapshot-v1",
                        "snapshot://nvs01-persistence")));
            }

            internal Nvs01CommandDisposition Abandon() =>
                RequireCommitted(Runtime.Abandon(
                    Command("PLAYER", Nvs01RuntimeContract.QuestId),
                    false));

            private static Nvs01CommandDisposition RequireCommitted(
                Nvs01CommandDisposition disposition)
            {
                Assert.NotNull(disposition);
                Assert.True(
                    disposition.IsCommitted,
                    disposition.Diagnostic?.Code + ":" +
                    disposition.Diagnostic?.Message);
                return disposition;
            }

            private Nvs01CommandEnvelope Command(
                string actor,
                string context) =>
                new Nvs01CommandEnvelope(
                    Nvs01RuntimeContract.ContractVersion,
                    GuidText(_commandId++),
                    Nvs01RuntimeContract.QuestId,
                    Runtime.Snapshot.StateId,
                    Runtime.Snapshot.Revision,
                    actor,
                    context,
                    0);

            private string NextRuntimeGuid() => GuidText(_runtimeGuidId++);

            private static Nvs01CapabilitySnapshot AllCapabilities()
            {
                return new Nvs01CapabilitySnapshot(
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
            }

            private static string GuidText(int value) =>
                "00000000-0000-0000-0000-" +
                value.ToString("D12");
        }
    }
}
