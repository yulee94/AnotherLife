using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        private const int ErrorInvalidParameter = 87;
        private const int ErrorPrivilegeNotHeld = 1314;
        private const int SymbolicLinkFlagAllowUnprivilegedCreate = 2;
        private string _saveRoot;
        private string _externalSentinelRoot;
        private string _ownedSymbolicLinkPath;
        private Nvs01VerifiedCatalog _catalog;

        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateSymbolicLinkW",
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool CreateSymbolicLinkW(
            string symbolicLinkPath,
            string targetPath,
            int flags);

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
            if (!string.IsNullOrEmpty(_ownedSymbolicLinkPath) &&
                File.Exists(_ownedSymbolicLinkPath))
            {
                File.Delete(_ownedSymbolicLinkPath);
            }

            if (!string.IsNullOrEmpty(_saveRoot) &&
                Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }

            if (!string.IsNullOrEmpty(_externalSentinelRoot) &&
                Directory.Exists(_externalSentinelRoot))
            {
                Directory.Delete(_externalSentinelRoot, true);
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
            Assert.That(reloaded.CurrentSave.ProfileId, Does.Match("^alp_[0-9a-f]{32}$"));
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

        [TestCase("offer-pending")]
        [TestCase("offer-deferred")]
        [TestCase("accepted")]
        [TestCase("lore")]
        [TestCase("before-request")]
        [TestCase("request-saved")]
        [TestCase("failure")]
        [TestCase("retry-ready")]
        [TestCase("retry-requested")]
        [TestCase("cancelled")]
        [TestCase("unavailable")]
        [TestCase("success-before-report")]
        [TestCase("during-report")]
        [TestCase("abandon-reaccept")]
        public void ExactRetainedV003D16StateMigratesOnlyPacketIdentityAndIsIdempotent(
            string stage)
        {
            Assert.AreEqual(
                "omen1-a1-2026-07-29-v003",
                Nvs01ProgressCodec.MigratablePacketVersion);
            Assert.AreEqual(
                "8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732",
                Nvs01ProgressCodec.MigratablePacketSha256);
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, stage);

            Nvs01ProgressData legacy = service.CurrentSave.Nvs01Progress;
            BindExactRetainedV003PacketIdentity(legacy);
            string exactLegacyProgress = JsonUtility.ToJson(legacy);
            long exactTimestamp = service.CurrentSave.LastSavedTimestamp;
            byte[] exactLegacyPrimary = new UTF8Encoding(false, true).GetBytes(
                JsonUtility.ToJson(service.CurrentSave, true));
            string primaryPath = Path.Combine(_saveRoot, "save.json");
            string backupPath = Path.Combine(_saveRoot, "save.backup.json");
            File.WriteAllBytes(primaryPath, exactLegacyPrimary);

            byte[] olderCurrentBackup = File.ReadAllBytes(backupPath);
            SaveGameData backupSave = JsonUtility.FromJson<SaveGameData>(
                new UTF8Encoding(false, true).GetString(olderCurrentBackup));
            Assert.Less(
                backupSave.Nvs01Progress.Revision,
                legacy.Revision,
                "The fixture must prove the exact migratable primary outranks an older clean backup.");
            Nvs01QuestSnapshot backupSnapshot = Decode(
                backupSave.Nvs01Progress,
                _catalog);
            Assert.AreEqual(
                Nvs01RuntimeContract.PacketVersion,
                backupSnapshot.PacketVersion,
                "The older neutral generation must decode through the current packet without rewriting its retained v0 bytes.");

            LocalSaveGameService migrated = CreateSaveService(_saveRoot);
            migrated.Load();

            Assert.NotNull(migrated.CurrentSave, migrated.LastLoadMessage);
            Assert.AreEqual(SaveLoadStatus.LoadedPrimary, migrated.LastLoadStatus);
            Assert.AreEqual(
                Nvs01RuntimeContract.PacketVersion,
                migrated.CurrentSave.Nvs01Progress.PacketVersion);
            Assert.AreEqual(
                Nvs01RuntimeContract.PacketSha256,
                migrated.CurrentSave.Nvs01Progress.PacketSha256);
            Assert.AreEqual(exactTimestamp, migrated.CurrentSave.LastSavedTimestamp);
            string migratedFingerprint =
                migrated.CurrentSave.Nvs01Progress.LastOperation
                    .ExpectedGenerationFingerprint ?? string.Empty;
            Assert.That(
                migratedFingerprint.Length == 0 ||
                System.Text.RegularExpressions.Regex.IsMatch(
                    migratedFingerprint,
                    "^[0-9a-f]{64}$"));
            Nvs01QuestSnapshot migratedSnapshot = Decode(
                migrated.CurrentSave.Nvs01Progress,
                _catalog);
            Assert.True(
                Nvs01QuestRuntime.TryValidateSnapshot(
                    migratedSnapshot,
                    out string sharedValidationError),
                sharedValidationError);
            Assert.AreEqual(
                legacy.Revision,
                migratedSnapshot.Revision,
                "The migrated clone must validate through the current v004 codec and catalog topology.");

            Nvs01ProgressData identityNeutralized =
                JsonUtility.FromJson<Nvs01ProgressData>(
                    JsonUtility.ToJson(migrated.CurrentSave.Nvs01Progress));
            identityNeutralized.PacketVersion =
                Nvs01ProgressCodec.MigratablePacketVersion;
            identityNeutralized.PacketSha256 =
                Nvs01ProgressCodec.MigratablePacketSha256;
            Assert.AreEqual(
                exactLegacyProgress,
                JsonUtility.ToJson(identityNeutralized),
                "Migration may change only packet version/hash; every D16 field and receipt must remain exact.");
            CollectionAssert.AreEqual(
                exactLegacyPrimary,
                File.ReadAllBytes(backupPath),
                "The existing atomic rotation must preserve the exact retained v003 primary as backup.");
            string[] migrationArchives = Directory.GetFiles(
                _saveRoot,
                "save.previous.json.migration-archive-*");
            Assert.AreEqual(1, migrationArchives.Length);
            CollectionAssert.AreEqual(
                olderCurrentBackup,
                File.ReadAllBytes(migrationArchives[0]),
                "Migration must retain the displaced pinned backup in its unique archive.");
            Assert.False(File.Exists(Path.Combine(_saveRoot, "save.tmp.json")));
            Assert.False(File.Exists(Path.Combine(_saveRoot, "save.previous.json")));

            byte[] exactMigratedPrimary = File.ReadAllBytes(primaryPath);
            byte[] exactMigratedBackup = File.ReadAllBytes(backupPath);
            string exactMigratedProgress =
                JsonUtility.ToJson(migrated.CurrentSave.Nvs01Progress);
            LocalSaveGameService repeated = CreateSaveService(_saveRoot);
            repeated.Load();

            Assert.NotNull(repeated.CurrentSave, repeated.LastLoadMessage);
            CollectionAssert.AreEqual(
                exactMigratedPrimary,
                File.ReadAllBytes(primaryPath),
                "A repeated load must be a migration no-op.");
            CollectionAssert.AreEqual(
                exactMigratedBackup,
                File.ReadAllBytes(backupPath));
            CollectionAssert.AreEqual(
                olderCurrentBackup,
                File.ReadAllBytes(migrationArchives[0]));
            Assert.AreEqual(
                exactMigratedProgress,
                JsonUtility.ToJson(repeated.CurrentSave.Nvs01Progress));
        }

        [TestCase("wrong-hash")]
        [TestCase("wrong-version")]
        [TestCase("wrong-quest")]
        [TestCase("future-packet")]
        [TestCase("malformed-state")]
        [TestCase("nonblank-generation")]
        public void NonExactV003EvidenceFailsClosedWithoutChangingBytes(
            string scenario)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "offer-pending");
            Nvs01ProgressData progress = service.CurrentSave.Nvs01Progress;
            progress.PacketVersion = Nvs01ProgressCodec.MigratablePacketVersion;
            progress.PacketSha256 = Nvs01ProgressCodec.MigratablePacketSha256;

            switch (scenario)
            {
                case "wrong-hash":
                    progress.PacketSha256 =
                        Nvs01ProgressCodec.MigratablePacketSha256
                            .Substring(0, 63) + "0";
                    break;
                case "wrong-version":
                    progress.PacketVersion = "omen1-a1-2026-07-29-v003-unknown";
                    break;
                case "wrong-quest":
                    progress.QuestId = "OMEN_UNKNOWN";
                    break;
                case "future-packet":
                    progress.PacketVersion = "omen1-a1-2099-01-01-v999";
                    break;
                case "malformed-state":
                    progress.StateId = string.Empty;
                    break;
                case "nonblank-generation":
                    progress.LastOperation.ExpectedGenerationFingerprint =
                        new string('a', 64);
                    break;
                default:
                    Assert.Fail("Unknown migration rejection fixture.");
                    break;
            }

            byte[] exactBytes = WriteCanonicalGenerations(service.CurrentSave);
            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.IsNull(reloaded.CurrentSave);
            Assert.NotNull(reloaded.LastLoadDisposition);
            Assert.False(reloaded.LastLoadDisposition.IsWritable);
            CollectionAssert.AreEqual(
                exactBytes,
                File.ReadAllBytes(Path.Combine(_saveRoot, "save.json")));
            CollectionAssert.AreEqual(
                exactBytes,
                File.ReadAllBytes(Path.Combine(_saveRoot, "save.backup.json")));
            Assert.False(File.Exists(Path.Combine(_saveRoot, "save.tmp.json")));
            Assert.False(File.Exists(Path.Combine(_saveRoot, "save.previous.json")));
        }

        [TestCase("unknown-state")]
        [TestCase("unknown-dialogue")]
        [TestCase("unknown-objective")]
        public void StructurallyValidButNonV004TopologyPrimaryLosesToCleanBackup(
            string scenario)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "offer-pending");
            Nvs01ProgressData progress = service.CurrentSave.Nvs01Progress;
            progress.PacketVersion = Nvs01ProgressCodec.MigratablePacketVersion;
            progress.PacketSha256 = Nvs01ProgressCodec.MigratablePacketSha256;

            switch (scenario)
            {
                case "unknown-state":
                    progress.StateId = "UNKNOWN_STRUCTURAL_STATE";
                    break;
                case "unknown-dialogue":
                    progress.CurrentDialogueNodeId = "DLG_UNKNOWN_STRUCTURAL";
                    break;
                case "unknown-objective":
                    progress.Objectives[0].ObjectiveId = "OBJ_UNKNOWN_STRUCTURAL";
                    break;
                default:
                    Assert.Fail("Unknown topology fixture.");
                    break;
            }

            string primaryPath = Path.Combine(_saveRoot, "save.json");
            string backupPath = Path.Combine(_saveRoot, "save.backup.json");
            byte[] invalidPrimary = new UTF8Encoding(false, true).GetBytes(
                JsonUtility.ToJson(service.CurrentSave, true));
            byte[] cleanBackup = File.ReadAllBytes(backupPath);
            File.WriteAllBytes(primaryPath, invalidPrimary);

            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.NotNull(reloaded.CurrentSave, reloaded.LastLoadMessage);
            Assert.AreEqual(
                SaveLoadStatus.RecoveredFromBackup,
                reloaded.LastLoadStatus);
            CollectionAssert.AreEqual(cleanBackup, File.ReadAllBytes(primaryPath));
            Assert.AreEqual(
                0,
                reloaded.CurrentSave.Nvs01Progress.Version,
                "Exact recovery must retain the older neutral v0 generation instead of silently rewriting it.");
            Nvs01QuestSnapshot recoveredSnapshot = Decode(
                reloaded.CurrentSave.Nvs01Progress,
                _catalog);
            Assert.AreEqual(
                Nvs01RuntimeContract.PacketVersion,
                recoveredSnapshot.PacketVersion,
                "The retained neutral v0 generation must decode through the current runtime packet contract.");
            AssertSnapshot(
                recoveredSnapshot,
                0,
                "OFFERED",
                string.Empty,
                false,
                string.Empty,
                "None",
                0);
            Assert.That(
                Directory.GetFiles(_saveRoot, "save.json.corrupt-*")
                    .Any(path => File.ReadAllBytes(path).SequenceEqual(invalidPrimary)),
                Is.True,
                "The topology-invalid v003 primary must survive as read-only quarantine evidence.");
        }

        [Test]
        public void MigrationWriteFailurePreservesExactOldAndRecoveryGenerations()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "during-report");
            BindExactRetainedV003PacketIdentity(service.CurrentSave.Nvs01Progress);

            string primaryPath = Path.Combine(_saveRoot, "save.json");
            string backupPath = Path.Combine(_saveRoot, "save.backup.json");
            byte[] exactLegacyPrimary = new UTF8Encoding(false, true).GetBytes(
                JsonUtility.ToJson(service.CurrentSave, true));
            File.WriteAllBytes(primaryPath, exactLegacyPrimary);
            byte[] exactRecoveryBackup = File.ReadAllBytes(backupPath);

            var failing = new LocalSaveGameService(
                _saveRoot,
                new FailingMigrationWriteFileOperations());
            failing.Load();

            Assert.IsNull(failing.CurrentSave);
            Assert.NotNull(failing.ReadOnlyCandidateSnapshot);
            Assert.AreEqual(SaveLoadStatus.RecoveryFailed, failing.LastLoadStatus);
            Assert.NotNull(failing.LastSaveDisposition);
            Assert.False(failing.LastSaveDisposition.MayHaveMutated);
            CollectionAssert.AreEqual(
                exactLegacyPrimary,
                File.ReadAllBytes(primaryPath));
            CollectionAssert.AreEqual(
                exactRecoveryBackup,
                File.ReadAllBytes(backupPath));
            Assert.False(File.Exists(Path.Combine(_saveRoot, "save.tmp.json")));
            Assert.False(File.Exists(Path.Combine(_saveRoot, "save.previous.json")));
        }

        [TestCase("save.tmp.json")]
        [TestCase("save.previous.json")]
        public void MigrationPreservesAuxiliaryGenerationAppearingAfterInventory(
            string auxiliaryFileName)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "during-report");
            BindExactRetainedV003PacketIdentity(service.CurrentSave.Nvs01Progress);

            string primaryPath = Path.Combine(_saveRoot, "save.json");
            string backupPath = Path.Combine(_saveRoot, "save.backup.json");
            string auxiliaryPath = Path.Combine(_saveRoot, auxiliaryFileName);
            byte[] exactLegacyPrimary = new UTF8Encoding(false, true).GetBytes(
                JsonUtility.ToJson(service.CurrentSave, true));
            byte[] exactBackup = File.ReadAllBytes(backupPath);
            byte[] concurrentEvidence = new UTF8Encoding(false, true).GetBytes(
                "{ concurrent auxiliary evidence");
            File.WriteAllBytes(primaryPath, exactLegacyPrimary);

            var raced = new LocalSaveGameService(
                _saveRoot,
                new AppearingMigrationAuxiliaryFileOperations(
                    auxiliaryPath,
                    concurrentEvidence));
            raced.Load();

            Assert.IsNull(raced.CurrentSave);
            Assert.NotNull(raced.ReadOnlyCandidateSnapshot);
            Assert.AreEqual(SaveLoadStatus.RecoveryFailed, raced.LastLoadStatus);
            CollectionAssert.AreEqual(
                exactLegacyPrimary,
                File.ReadAllBytes(primaryPath));
            CollectionAssert.AreEqual(exactBackup, File.ReadAllBytes(backupPath));
            CollectionAssert.AreEqual(
                concurrentEvidence,
                File.ReadAllBytes(auxiliaryPath));
        }

        [Test]
        public void MigrationArchivesForeignPreviousRaceAndWithholdsSuccess()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "during-report");
            BindExactRetainedV003PacketIdentity(service.CurrentSave.Nvs01Progress);

            string primaryPath = Path.Combine(_saveRoot, "save.json");
            string backupPath = Path.Combine(_saveRoot, "save.backup.json");
            string previousPath = Path.Combine(
                _saveRoot,
                "save.previous.json");
            byte[] exactLegacyPrimary = new UTF8Encoding(false, true).GetBytes(
                JsonUtility.ToJson(service.CurrentSave, true));
            byte[] foreignPrevious = new UTF8Encoding(false, true).GetBytes(
                "{ foreign previous race evidence");
            File.WriteAllBytes(primaryPath, exactLegacyPrimary);

            var operations =
                new ReplacingMigrationPreviousFileOperations(
                    previousPath,
                    foreignPrevious);
            var raced = new LocalSaveGameService(_saveRoot, operations);
            LogAssert.Expect(
                LogType.Error,
                "AL-SAVE-NVS01-MIGRATION-FAILED: The atomic v003-to-v004 rebind did not reach a twice-verified commit target; old generation and recovery evidence were preserved. AL-SAVE-BACKUP-CLEANUP-FAILED: Candidate and backup validated, but canonical residue or the preserved Stage 5 transaction marker did not reach its exact cleanup target.");
            raced.Load();

            Assert.True(operations.Injected);
            Assert.IsNull(raced.CurrentSave);
            Assert.NotNull(raced.ReadOnlyCandidateSnapshot);
            Assert.AreEqual(SaveLoadStatus.RecoveryFailed, raced.LastLoadStatus);
            Assert.NotNull(raced.LastSaveDisposition);
            Assert.AreEqual(
                SaveOperationStatus.CommitUncertain,
                raced.LastSaveDisposition.Status);
            Assert.True(raced.LastSaveDisposition.CandidatePrimaryVerified);
            Assert.True(raced.LastSaveDisposition.RequiredBackupVerified);
            Assert.False(raced.LastSaveDisposition.CleanupVerified);
            CollectionAssert.AreEqual(
                exactLegacyPrimary,
                File.ReadAllBytes(backupPath));
            Assert.False(File.Exists(previousPath));
            Assert.False(File.Exists(Path.Combine(_saveRoot, "save.tmp.json")));
            string[] migrationArchives = Directory.GetFiles(
                _saveRoot,
                "save.previous.json.migration-archive-*");
            Assert.AreEqual(1, migrationArchives.Length);
            CollectionAssert.AreEqual(
                foreignPrevious,
                File.ReadAllBytes(migrationArchives[0]),
                "A foreign generation that wins immediately before cleanup must survive in the non-overwriting archive.");

            LocalSaveGameService fresh = CreateSaveService(_saveRoot);
            fresh.Load();

            Assert.IsNull(fresh.CurrentSave);
            Assert.IsNull(fresh.ReadOnlyCandidateSnapshot);
            Assert.AreEqual(
                SaveLoadStatus.RecoveryRequired,
                fresh.LastLoadStatus);
            Assert.NotNull(fresh.LastLoadDisposition);
            Assert.False(fresh.LastLoadDisposition.IsWritable);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.RecoveryRequired,
                ((IProfileWriteAuthorityProvider)fresh)
                    .GetCurrentAuthority().Status);
            CollectionAssert.AreEqual(
                foreignPrevious,
                File.ReadAllBytes(migrationArchives[0]));
            CollectionAssert.AreEqual(
                exactLegacyPrimary,
                File.ReadAllBytes(backupPath));
        }

        [Test]
        [Platform("Win")]
        public void MigrationArchiveFileSymbolicLinkFailsClosedWithoutTouchingTarget()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            byte[] exactPrimary = File.ReadAllBytes(
                Path.Combine(_saveRoot, "save.json"));
            byte[] exactBackup = File.ReadAllBytes(
                Path.Combine(_saveRoot, "save.backup.json"));

            _externalSentinelRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-Nvs01PersistenceSentinelTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_externalSentinelRoot);
            string sentinelPath = Path.Combine(
                _externalSentinelRoot,
                "migration-archive-sentinel.bin");
            byte[] sentinelBytes = new UTF8Encoding(false, true).GetBytes(
                "NVS01 external symbolic-link sentinel");
            File.WriteAllBytes(sentinelPath, sentinelBytes);

            _ownedSymbolicLinkPath = Path.Combine(
                _saveRoot,
                "save.previous.json.migration-archive-" +
                Sha256Base64Url(sentinelBytes));
            bool created = CreateSymbolicLinkW(
                _ownedSymbolicLinkPath,
                sentinelPath,
                SymbolicLinkFlagAllowUnprivilegedCreate);
            int firstError = Marshal.GetLastWin32Error();
            int fallbackError = 0;
            if (!created && firstError == ErrorInvalidParameter)
            {
                created = CreateSymbolicLinkW(
                    _ownedSymbolicLinkPath,
                    sentinelPath,
                    0);
                fallbackError = Marshal.GetLastWin32Error();
            }

            if (!created &&
                (firstError == ErrorPrivilegeNotHeld ||
                 fallbackError == ErrorPrivilegeNotHeld))
            {
                Assert.Ignore(
                    "Windows file symbolic-link evidence requires Developer Mode or " +
                    "SeCreateSymbolicLinkPrivilege on this host.");
            }

            Assert.True(
                created,
                "Windows file symbolic-link creation is required evidence; " +
                $"CreateSymbolicLinkW failed with {firstError}/{fallbackError}.");
            Assert.That(
                File.GetAttributes(_ownedSymbolicLinkPath) &
                FileAttributes.ReparsePoint,
                Is.EqualTo(FileAttributes.ReparsePoint));
            CollectionAssert.AreEqual(
                sentinelBytes,
                File.ReadAllBytes(_ownedSymbolicLinkPath));

            LocalSaveGameService fresh = CreateSaveService(_saveRoot);
            fresh.Load();

            Assert.IsNull(fresh.CurrentSave);
            Assert.AreEqual(
                SaveLoadStatus.RecoveryRequired,
                fresh.LastLoadStatus);
            Assert.NotNull(fresh.LastLoadDisposition);
            Assert.AreEqual(
                "SAVE_NVS01_MIGRATION_ARCHIVE_UNSAFE",
                fresh.LastLoadDisposition.SelectorReason);
            Assert.False(fresh.LastLoadDisposition.IsWritable);
            Assert.False(fresh.LastLoadDisposition.IsRuntimeUsable);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.RecoveryRequired,
                ((IProfileWriteAuthorityProvider)fresh)
                    .GetCurrentAuthority().Status);
            Assert.True(File.Exists(sentinelPath));
            Assert.True(File.Exists(_ownedSymbolicLinkPath));
            Assert.That(
                File.GetAttributes(_ownedSymbolicLinkPath) &
                FileAttributes.ReparsePoint,
                Is.EqualTo(FileAttributes.ReparsePoint));
            CollectionAssert.AreEqual(
                sentinelBytes,
                File.ReadAllBytes(sentinelPath));
            CollectionAssert.AreEqual(
                sentinelBytes,
                File.ReadAllBytes(_ownedSymbolicLinkPath));
            CollectionAssert.AreEqual(
                exactPrimary,
                File.ReadAllBytes(Path.Combine(_saveRoot, "save.json")));
            CollectionAssert.AreEqual(
                exactBackup,
                File.ReadAllBytes(Path.Combine(
                    _saveRoot,
                    "save.backup.json")));
        }

        [TestCase("save.tmp.json")]
        [TestCase("save.previous.json")]
        public void MigrationDoesNotConsumeUnresolvedRecoveryGeneration(
            string recoveryFileName)
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "offer-pending");
            BindExactRetainedV003PacketIdentity(service.CurrentSave.Nvs01Progress);

            string primaryPath = Path.Combine(_saveRoot, "save.json");
            string backupPath = Path.Combine(_saveRoot, "save.backup.json");
            string recoveryPath = Path.Combine(_saveRoot, recoveryFileName);
            byte[] exactLegacyPrimary = new UTF8Encoding(false, true).GetBytes(
                JsonUtility.ToJson(service.CurrentSave, true));
            byte[] exactBackup = File.ReadAllBytes(backupPath);
            byte[] exactRecovery = new UTF8Encoding(false, true).GetBytes(
                "{ retained recovery evidence");
            File.WriteAllBytes(primaryPath, exactLegacyPrimary);
            File.WriteAllBytes(recoveryPath, exactRecovery);

            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.IsNull(reloaded.CurrentSave);
            SaveGameData readOnlyCandidate =
                reloaded.ReadOnlyCandidateSnapshot;
            Assert.NotNull(readOnlyCandidate);
            Assert.AreEqual(
                Nvs01ProgressCodec.MigratablePacketVersion,
                readOnlyCandidate.Nvs01Progress.PacketVersion);
            Assert.AreEqual(
                Nvs01ProgressCodec.MigratablePacketSha256,
                readOnlyCandidate.Nvs01Progress.PacketSha256);
            Assert.AreEqual(
                SaveLoadStatus.LoadedPrimaryDegraded,
                reloaded.LastLoadStatus);
            Assert.NotNull(reloaded.LastLoadDisposition);
            Assert.False(reloaded.LastLoadDisposition.IsWritable);
            Assert.False(reloaded.LastLoadDisposition.IsRuntimeUsable);
            CollectionAssert.AreEqual(
                exactLegacyPrimary,
                File.ReadAllBytes(primaryPath));
            CollectionAssert.AreEqual(
                exactBackup,
                File.ReadAllBytes(backupPath));
            CollectionAssert.AreEqual(
                exactRecovery,
                File.ReadAllBytes(recoveryPath));
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
        public void SchemaTwoProfileIdentityIsBoundAndReloadStable()
        {
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            var session = new RuntimeSession(service, _catalog, null);
            AdvanceTo(session, "during-report");

            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                service.CurrentSave.SaveSchemaVersion);
            Assert.That(service.CurrentSave.ProfileId, Does.Match("^alp_[0-9a-f]{32}$"));
            AssertMigrationRequired(service);
            string profileId = service.CurrentSave.ProfileId;

            LocalSaveGameService reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();
            Assert.AreEqual(profileId, reloaded.CurrentSave.ProfileId);
            AssertMigrationRequired(reloaded);
        }

        [Test]
        public void SchemaV1NvsAdapterRejectsCallerMutatedProfileIdentity()
        {
            LogAssert.ignoreFailingMessages = true;
            LocalSaveGameService service = CreateSaveService(_saveRoot);
            service.CreateNewSave(RealmId.Crownlands);
            string primaryPath = Path.Combine(_saveRoot, "save.json");
            byte[] exactCleanPrimary = File.ReadAllBytes(primaryPath);
            string originalProfileId = service.CurrentSave.ProfileId;
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
            Assert.AreEqual(originalProfileId, reloaded.CurrentSave.ProfileId);
            Assert.True(
                reloaded.LastLoadDisposition.IsWritable);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
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
            LogAssert.ignoreFailingMessages = true;
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
                    published.SaveSchemaVersion = 3;
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

        private static void BindExactRetainedV003PacketIdentity(
            Nvs01ProgressData progress)
        {
            progress.PacketVersion = Nvs01ProgressCodec.MigratablePacketVersion;
            progress.PacketSha256 = Nvs01ProgressCodec.MigratablePacketSha256;
            if (progress.LastOperation != null)
            {
                progress.LastOperation.ExpectedGenerationFingerprint =
                    string.Empty;
            }
        }

        private static void AssertMigrationRequired(
            LocalSaveGameService service)
        {
            ProfileWriteAuthoritySnapshot authority =
                ((IProfileWriteAuthorityProvider)service)
                .GetCurrentAuthority();
            Assert.NotNull(authority);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                authority.Status);
            Assert.That(authority.ProfileId, Does.Match("^alp_[0-9a-f]{32}$"));
            Assert.IsNotEmpty(authority.AuthorityEpoch);
            Assert.IsNotEmpty(authority.VerifiedGenerationFingerprint);
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

        private static string Sha256Base64Url(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return Convert.ToBase64String(
                    sha256.ComputeHash(bytes ?? Array.Empty<byte>()))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
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

        private sealed class FailingMigrationWriteFileOperations :
            ISaveFileOperations
        {
            private readonly ISaveFileOperations _inner =
                new SystemSaveFileOperations();

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path) =>
                _inner.CreateDirectory(path);
            public SaveFileReadResult ReadAllBytesBounded(
                string path,
                int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);
            public SaveFileWriteResult WriteAllTextDurable(
                string path,
                string contents) =>
                new SaveFileWriteResult(
                    false,
                    false,
                    "TEST_MIGRATION_WRITE_FAILED");
            public void Copy(
                string sourcePath,
                string destinationPath,
                bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);
            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);
            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);
            public void Delete(string path) => _inner.Delete(path);
            public IEnumerable<string> EnumerateFiles(
                string directoryPath,
                string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);
            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);
            public bool IsReparsePoint(string path) =>
                _inner.IsReparsePoint(path);
        }

        private sealed class AppearingMigrationAuxiliaryFileOperations :
            ISaveFileOperations
        {
            private readonly ISaveFileOperations _inner =
                new SystemSaveFileOperations();
            private readonly string _path;
            private readonly byte[] _bytes;
            private bool _appeared;

            internal AppearingMigrationAuxiliaryFileOperations(
                string path,
                byte[] bytes)
            {
                _path = path;
                _bytes = bytes;
            }

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path)
            {
                _inner.CreateDirectory(path);
                if (_appeared) return;
                File.WriteAllBytes(_path, _bytes);
                _appeared = true;
            }
            public SaveFileReadResult ReadAllBytesBounded(
                string path,
                int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);
            public SaveFileWriteResult WriteAllTextDurable(
                string path,
                string contents) =>
                _inner.WriteAllTextDurable(path, contents);
            public void Copy(
                string sourcePath,
                string destinationPath,
                bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);
            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);
            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);
            public void Delete(string path) => _inner.Delete(path);
            public IEnumerable<string> EnumerateFiles(
                string directoryPath,
                string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);
            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);
            public bool IsReparsePoint(string path) =>
                _inner.IsReparsePoint(path);
        }

        private sealed class ReplacingMigrationPreviousFileOperations :
            ISaveFileOperations
        {
            private readonly ISaveFileOperations _inner =
                new SystemSaveFileOperations();
            private readonly string _previousPath;
            private readonly byte[] _foreignBytes;

            internal ReplacingMigrationPreviousFileOperations(
                string previousPath,
                byte[] foreignBytes)
            {
                _previousPath = previousPath;
                _foreignBytes = foreignBytes;
            }

            internal bool Injected { get; private set; }

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path) =>
                _inner.CreateDirectory(path);
            public SaveFileReadResult ReadAllBytesBounded(
                string path,
                int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);
            public SaveFileWriteResult WriteAllTextDurable(
                string path,
                string contents) =>
                _inner.WriteAllTextDurable(path, contents);
            public void Copy(
                string sourcePath,
                string destinationPath,
                bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);
            public void Move(string sourcePath, string destinationPath)
            {
                if (!Injected &&
                    string.Equals(
                        sourcePath,
                        _previousPath,
                        StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(destinationPath).StartsWith(
                        "save.previous.json.migration-archive-",
                        StringComparison.Ordinal))
                {
                    File.Delete(sourcePath);
                    File.WriteAllBytes(sourcePath, _foreignBytes);
                    Injected = true;
                }

                _inner.Move(sourcePath, destinationPath);
            }
            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);
            public void Delete(string path) => _inner.Delete(path);
            public IEnumerable<string> EnumerateFiles(
                string directoryPath,
                string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);
            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);
            public bool IsReparsePoint(string path) =>
                _inner.IsReparsePoint(path);
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
