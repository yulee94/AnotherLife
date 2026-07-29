using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed class SaveCandidateInventoryIntegrationTests
    {
        private const string CurrentSaveFormatId = "anotherlife.local-save";
        private const int MaximumSaveBytes = 1024 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [Test]
        public void InjectedLimitAcceptsExactPrimaryAndRejectsOneByteOverWithoutMutation()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                File.Delete(Path.Combine(root, "save.backup.json"));
                byte[] originalPrimary = File.ReadAllBytes(primaryPath);
                Assert.That(originalPrimary.Length, Is.GreaterThan(1));
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object exactLimitService = CreateSaveService(
                    root,
                    CreateSemanticPolicy(originalPrimary.Length));
                Invoke(exactLimitService, "Load");

                Assert.AreEqual(
                    "LoadedPrimary",
                    GetProperty(exactLimitService, "LastLoadStatus").ToString());
                Assert.NotNull(GetProperty(exactLimitService, "CurrentSave"));
                object exactDisposition = GetProperty(exactLimitService, "LastLoadDisposition");
                object exactPrimarySummary = FindSummary(exactDisposition, "Primary");
                Assert.AreEqual("Read", GetProperty(exactPrimarySummary, "ReadDisposition").ToString());
                Assert.AreEqual(
                    originalPrimary.LongLength,
                    GetProperty(exactPrimarySummary, "ObservedByteCount"));
                AssertDirectoryUnchanged(root, originalDirectory);

                object smallerLimitService = CreateSaveService(
                    root,
                    CreateSemanticPolicy(originalPrimary.Length - 1));
                Invoke(smallerLimitService, "Load");

                Assert.AreEqual(
                    "RecoveryRequired",
                    GetProperty(smallerLimitService, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(smallerLimitService, "CurrentSave"));
                object smallerDisposition = GetProperty(
                    smallerLimitService,
                    "LastLoadDisposition");
                object smallerPrimarySummary = FindSummary(smallerDisposition, "Primary");
                Assert.AreEqual(
                    "Oversize",
                    GetProperty(smallerPrimarySummary, "ReadDisposition").ToString());
                Assert.AreEqual(
                    "Unknown",
                    GetProperty(smallerDisposition, "SelectedSource").ToString());
                Assert.AreEqual(
                    "SAVE_SELECT_OVERSIZE_PRIMARY_RECOVERY_REQUIRED",
                    GetProperty(smallerDisposition, "SelectorReason"));
                Assert.False((bool)GetProperty(smallerDisposition, "DiskChanged"));
                CollectionAssert.AreEqual(originalPrimary, File.ReadAllBytes(primaryPath));
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void OversizePrimaryDoesNotFallBackOrMutateAnyGeneration()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] validBackup = File.ReadAllBytes(backupPath);
                var oversizePrimary = new byte[MaximumSaveBytes + 1];
                for (var index = 0; index < oversizePrimary.Length; index++)
                {
                    oversizePrimary[index] = (byte)'X';
                }

                File.WriteAllBytes(primaryPath, oversizePrimary);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Assert.True((bool)Invoke(service, "HasSave"));
                Invoke(service, "Load");

                Assert.AreEqual(
                    "RecoveryRequired",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(service, "CurrentSave"));
                object disposition = GetProperty(service, "LastLoadDisposition");
                Assert.AreEqual(
                    "Unknown",
                    GetProperty(disposition, "SelectedSource").ToString());
                Assert.AreEqual(
                    "SAVE_SELECT_OVERSIZE_PRIMARY_RECOVERY_REQUIRED",
                    GetProperty(disposition, "SelectorReason"));
                Assert.False((bool)GetProperty(disposition, "DiskChanged"));
                Assert.AreEqual(
                    "Oversize",
                    GetProperty(
                        FindSummary(disposition, "Primary"),
                        "ReadDisposition").ToString());
                object backupSummary = FindSummary(disposition, "Backup");
                Assert.AreEqual(
                    "Read",
                    GetProperty(backupSummary, "ReadDisposition").ToString());
                Assert.AreEqual(
                    "Valid",
                    GetProperty(backupSummary, "SemanticOutcome").ToString());
                CollectionAssert.AreEqual(oversizePrimary, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(validBackup, File.ReadAllBytes(backupPath));
                AssertDirectoryUnchanged(root, originalDirectory);
                Assert.False(
                    Directory.GetFiles(root)
                        .Any(path => Path.GetFileName(path).Contains(".corrupt-")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ForwardPrimaryRemainsAuthoritativeReadOnlyOverValidBackup()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] currentPrimary = File.ReadAllBytes(primaryPath);
                byte[] validBackup = File.ReadAllBytes(backupPath);
                byte[] forwardPrimary = CreateForwardSchemaBytes(currentPrimary);
                File.WriteAllBytes(primaryPath, forwardPrimary);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedForwardSchemaReadOnly",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(service, "CurrentSave"));
                object disposition = GetProperty(service, "LastLoadDisposition");
                Assert.AreEqual(
                    "Primary",
                    GetProperty(disposition, "SelectedSource").ToString());
                Assert.AreEqual(
                    "SAVE_SELECT_FORWARD_PRIMARY_READ_ONLY",
                    GetProperty(disposition, "SelectorReason"));
                Assert.False((bool)GetProperty(disposition, "IsWritable"));
                Assert.False((bool)GetProperty(disposition, "IsRuntimeUsable"));
                Assert.False((bool)GetProperty(disposition, "OfflineProgressApplied"));
                Assert.False((bool)GetProperty(disposition, "DiskChanged"));
                Assert.AreEqual(
                    "ForwardSchemaReadOnly",
                    GetProperty(
                        FindSummary(disposition, "Primary"),
                        "SemanticOutcome").ToString());
                Assert.AreEqual(
                    "Valid",
                    GetProperty(
                        FindSummary(disposition, "Backup"),
                        "SemanticOutcome").ToString());

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("^AL-SAVE-READ-ONLY-DISPOSITION:"));
                Invoke(service, "Save");
                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(service, "LastSaveStatus").ToString());
                CollectionAssert.AreEqual(forwardPrimary, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(validBackup, File.ReadAllBytes(backupPath));
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void NullQuestRowRemainsExactRawEvidenceAcrossLoadAndRejectedSave()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                File.Delete(Path.Combine(root, "save.backup.json"));

                string currentJson = StrictUtf8.GetString(File.ReadAllBytes(primaryPath));
                var questsPattern = new Regex("\"Quests\"\\s*:\\s*\\[\\]");
                Assert.True(
                    questsPattern.IsMatch(currentJson),
                    "Expected the generated current save to contain an empty quest array.");
                byte[] rawNullQuestPrimary = StrictUtf8.GetBytes(
                    questsPattern.Replace(
                        currentJson,
                        "\"Quests\": [null]",
                        1));
                File.WriteAllBytes(primaryPath, rawNullQuestPrimary);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedPrimaryDegraded",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(service, "CurrentSave"));
                object disposition = GetProperty(service, "LastLoadDisposition");
                Assert.AreEqual(
                    "Primary",
                    GetProperty(disposition, "SelectedSource").ToString());
                Assert.False((bool)GetProperty(disposition, "IsWritable"));
                Assert.False((bool)GetProperty(disposition, "IsRuntimeUsable"));
                Assert.False((bool)GetProperty(disposition, "DiskChanged"));
                Assert.True((bool)GetProperty(disposition, "RawEvidencePreserved"));

                object primarySummary = FindSummary(disposition, "Primary");
                Assert.AreEqual(
                    "DegradedMalformed",
                    GetProperty(primarySummary, "SemanticOutcome").ToString());
                Assert.That(
                    GetProperty(primarySummary, "DisabledDomains").ToString(),
                    Does.Contain("Quests"));
                CollectionAssert.Contains(
                    ((IEnumerable)GetProperty(primarySummary, "DiagnosticCodes"))
                        .Cast<object>()
                        .Select(code => code.ToString())
                        .ToArray(),
                    "SAVE_QUEST_ROW_NULL");
                AssertDirectoryUnchanged(root, originalDirectory);

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("^AL-SAVE-READ-ONLY-DISPOSITION:"));
                Invoke(service, "Save");

                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(service, "LastSaveStatus").ToString());
                CollectionAssert.AreEqual(
                    rawNullQuestPrimary,
                    File.ReadAllBytes(primaryPath));
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ForwardBackupUsesSourceNeutralReadOnlyDisposition()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] forwardBackup = CreateForwardSchemaBytes(
                    File.ReadAllBytes(primaryPath));
                File.Delete(primaryPath);
                File.WriteAllBytes(backupPath, forwardBackup);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedForwardSchemaReadOnly",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(service, "CurrentSave"));
                Assert.That(
                    (string)GetProperty(service, "LastLoadMessage"),
                    Does.Not.Contain("primary schema"));
                object disposition = GetProperty(service, "LastLoadDisposition");
                Assert.AreEqual(
                    "Backup",
                    GetProperty(disposition, "SelectedSource").ToString());
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ValidPrimaryRunsButCannotOverwriteForwardBackupEvidence()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] forwardBackup = CreateForwardSchemaBytes(
                    File.ReadAllBytes(primaryPath));
                File.WriteAllBytes(backupPath, forwardBackup);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedPrimary",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.NotNull(GetProperty(service, "CurrentSave"));
                object disposition = GetProperty(service, "LastLoadDisposition");
                Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
                Assert.False((bool)GetProperty(disposition, "IsWritable"));
                Assert.AreEqual(
                    "ForwardSchemaReadOnly",
                    GetProperty(
                        FindSummary(disposition, "Backup"),
                        "SemanticOutcome").ToString());

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("^AL-SAVE-READ-ONLY-DISPOSITION:"));
                Invoke(service, "Save");

                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(service, "LastSaveStatus").ToString());
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void TempOnlyCandidateRequiresRecoveryAndRemainsUntouched()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                string tempPath = Path.Combine(root, "save.tmp.json");
                byte[] validBytes = File.ReadAllBytes(primaryPath);
                File.Delete(primaryPath);
                File.Delete(backupPath);
                File.WriteAllBytes(tempPath, validBytes);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "RecoveryRequired",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(service, "CurrentSave"));
                object disposition = GetProperty(service, "LastLoadDisposition");
                Assert.AreEqual(
                    "Unknown",
                    GetProperty(disposition, "SelectedSource").ToString());
                Assert.AreEqual(
                    "SAVE_SELECT_NONE",
                    GetProperty(disposition, "SelectorReason"));
                Assert.False((bool)GetProperty(disposition, "DiskChanged"));
                object tempSummary = FindSummary(disposition, "Temp");
                Assert.AreEqual(
                    "Read",
                    GetProperty(tempSummary, "ReadDisposition").ToString());
                Assert.AreEqual(
                    "Valid",
                    GetProperty(tempSummary, "SemanticOutcome").ToString());
                Assert.True(File.Exists(tempPath));
                Assert.False(File.Exists(primaryPath));
                Assert.False(File.Exists(backupPath));
                CollectionAssert.AreEqual(validBytes, File.ReadAllBytes(tempPath));
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void OversizeSerializedCurrentSaveFailsBeforeAnyCanonicalFileMutation()
        {
            string root = CreateRoot();

            try
            {
                object service = CreateCurrentProfile(root);
                string tempPath = Path.Combine(root, "save.tmp.json");
                string previousPath = Path.Combine(root, "save.previous.json");
                File.WriteAllBytes(tempPath, StrictUtf8.GetBytes("temp-sentinel"));
                File.WriteAllBytes(previousPath, StrictUtf8.GetBytes("previous-sentinel"));

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(
                    currentSave,
                    "CurrentChapterId",
                    new string('X', MaximumSaveBytes + 4096));
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("^AL-SAVE-CANDIDATE-TOO-LARGE:"));
                Invoke(service, "Save");

                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(service, "LastSaveStatus").ToString());
                Assert.That(
                    (string)GetProperty(service, "LastSaveMessage"),
                    Does.StartWith("AL-SAVE-CANDIDATE-TOO-LARGE:"));
                AssertDirectoryUnchanged(root, originalDirectory);
                Assert.False(
                    Directory.GetFiles(root)
                        .Any(path => Path.GetFileName(path).Contains(".corrupt-")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void OrdinaryLoadPublishesFourPathFreeCandidateSummaries()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedPrimary",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.NotNull(GetProperty(service, "CurrentSave"));
                object disposition = GetProperty(service, "LastLoadDisposition");
                List<object> summaries = GetSummaries(disposition);
                Assert.AreEqual(4, summaries.Count);
                CollectionAssert.AreEqual(
                    new[] { "Primary", "Backup", "Previous", "Temp" },
                    summaries
                        .Select(summary => GetProperty(summary, "Source").ToString())
                        .ToArray());

                foreach (object summary in summaries)
                {
                    Assert.False(
                        summary.GetType()
                            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                            .Any(property =>
                                property.Name.IndexOf(
                                    "Path",
                                    StringComparison.OrdinalIgnoreCase) >= 0),
                        "Candidate summaries must not expose filesystem paths.");

                    foreach (object code in (IEnumerable)GetProperty(
                                 summary,
                                 "DiagnosticCodes"))
                    {
                        string diagnosticCode = code?.ToString() ?? string.Empty;
                        Assert.That(diagnosticCode, Does.Not.Contain(root));
                        Assert.That(diagnosticCode, Does.Not.Contain("save.json"));
                    }
                }

                Assert.AreEqual(
                    "Primary",
                    GetProperty(disposition, "SelectedSource").ToString());
                Assert.AreEqual(
                    "SAVE_SELECT_SUPPORTED_PRIMARY",
                    GetProperty(disposition, "SelectorReason"));
                Assert.True((bool)GetProperty(disposition, "IsWritable"));
                Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
                Assert.False((bool)GetProperty(disposition, "OfflineProgressApplied"));
                Assert.False((bool)GetProperty(disposition, "DiskChanged"));
                Assert.True((bool)GetProperty(disposition, "RawEvidencePreserved"));
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ExistingWritableHandleMakesPrimaryUnreadableInsteadOfTorn()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                using (var writer = new FileStream(
                           primaryPath,
                           FileMode.Open,
                           FileAccess.ReadWrite,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    object service = CreateSaveService(root);
                    LogAssert.Expect(
                        LogType.Error,
                        new Regex("^AL-SAVE-PRIMARY-UNREADABLE:"));
                    Invoke(service, "Load");

                    Assert.AreEqual(
                        "RecoveryFailed",
                        GetProperty(service, "LastLoadStatus").ToString());
                    Assert.Null(GetProperty(service, "CurrentSave"));
                    Assert.False(
                        (bool)GetProperty(
                            GetProperty(service, "LastLoadDisposition"),
                            "IsRuntimeUsable"));
                }

                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void BoundedReaderDeniesPathRenameWhileHandleIsOpen()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                string renamedPath = Path.Combine(root, "renamed-save.json");
                Type operationsType = GetRuntimeType(
                    "AL.Services.Local.SystemSaveFileOperations");
                MethodInfo openMethod = operationsType.GetMethod(
                    "OpenBoundedReadStream",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(openMethod);

                using (var stream = (Stream)openMethod.Invoke(
                           null,
                           new object[] { primaryPath }))
                {
                    Assert.Throws<IOException>(() =>
                        File.Move(primaryPath, renamedPath));
                }

                Assert.True(File.Exists(primaryPath));
                Assert.False(File.Exists(renamedPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyCompatiblePrimaryExposesOnlyNeutralReadOnlySnapshot()
        {
            string root = CreateRoot();

            try
            {
                string primaryPath = Path.Combine(root, "save.json");
                byte[] legacyBytes = StrictUtf8.GetBytes(LegacyCompatibleJson());
                File.WriteAllBytes(primaryPath, legacyBytes);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedPrimaryNormalized",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(service, "CurrentSave"));
                object snapshot = GetProperty(service, "ReadOnlyCandidateSnapshot");
                Assert.NotNull(snapshot);
                Assert.That((string)GetField(snapshot, "SaveFormatId"), Is.Null.Or.Empty);
                Assert.AreEqual(0, GetField(snapshot, "SaveSchemaVersion"));
                Assert.NotNull(GetField(snapshot, "Reputation"));
                Assert.NotNull(GetField(snapshot, "FactionReputations"));
                Assert.NotNull(GetField(snapshot, "LordPersona"));
                Assert.NotNull(GetField(snapshot, "OwnedEquipment"));

                IList resources = (IList)GetField(snapshot, "Resources");
                object mana = resources.Cast<object>().Single(row =>
                    GetField(row, "Type").ToString() == "ManaStone");
                object ore = resources.Cast<object>().Single(row =>
                    GetField(row, "Type").ToString() == "Ore");
                Assert.AreEqual(0L, GetField(mana, "Amount"));
                Assert.AreEqual(0L, GetField(ore, "Amount"));
                SetField(snapshot, "CurrentChapterId", "MUTATED_DIAGNOSTIC_COPY");
                object secondSnapshot = GetProperty(
                    service,
                    "ReadOnlyCandidateSnapshot");
                Assert.AreEqual("C1", GetField(secondSnapshot, "CurrentChapterId"));
                CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(primaryPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void CurrentPrimaryMissingNvs01ProgressLoadsWritableWithNeutralDefault()
        {
            string root = CreateRoot();

            try
            {
                CreateCurrentProfile(root);
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] withoutNvs01 = RemoveNvs01Progress(
                    File.ReadAllBytes(primaryPath));
                File.WriteAllBytes(primaryPath, withoutNvs01);
                File.WriteAllBytes(backupPath, withoutNvs01);
                Dictionary<string, byte[]> originalDirectory =
                    SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedPrimary",
                    GetProperty(service, "LastLoadStatus").ToString());
                object currentSave = GetProperty(service, "CurrentSave");
                Assert.NotNull(currentSave);
                Assert.Null(GetProperty(service, "ReadOnlyCandidateSnapshot"));
                object progress = GetField(currentSave, "Nvs01Progress");
                Assert.NotNull(progress);
                Assert.AreEqual(0, GetField(progress, "Version"));

                object disposition = GetProperty(
                    service,
                    "LastLoadDisposition");
                Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
                Assert.True((bool)GetProperty(disposition, "IsWritable"));
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void MultibyteSerializedPayloadUsesUtf8ByteLimitBeforeMutation()
        {
            string root = CreateRoot();

            try
            {
                object service = CreateCurrentProfile(root);
                object currentSave = GetProperty(service, "CurrentSave");
                string multibyteChapter = new string('界', MaximumSaveBytes / 3 + 1024);
                Assert.Less(multibyteChapter.Length, MaximumSaveBytes);
                Assert.Greater(
                    StrictUtf8.GetByteCount(multibyteChapter),
                    MaximumSaveBytes);
                SetField(currentSave, "CurrentChapterId", multibyteChapter);
                Dictionary<string, byte[]> originalDirectory = SnapshotDirectory(root);

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("^AL-SAVE-CANDIDATE-TOO-LARGE:"));
                Invoke(service, "Save");

                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(service, "LastSaveStatus").ToString());
                AssertDirectoryUnchanged(root, originalDirectory);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static object CreateCurrentProfile(string root)
        {
            object service = CreateSaveService(root);
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "Eldergrove"));
            Assert.True(File.Exists(Path.Combine(root, "save.json")));
            Assert.True(File.Exists(Path.Combine(root, "save.backup.json")));
            return service;
        }

        private static object CreateSaveService(
            string root,
            SaveSemanticValidationPolicy semanticPolicy = null)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            if (semanticPolicy == null)
            {
                ConstructorInfo pathConstructor = serviceType
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(constructor =>
                    {
                        ParameterInfo[] parameters = constructor.GetParameters();
                        return parameters.Length == 1 &&
                               parameters[0].ParameterType == typeof(string);
                    });
                return pathConstructor.Invoke(new object[] { root });
            }

            Type operationsType = GetRuntimeType(
                "AL.Services.Local.SystemSaveFileOperations");
            object operations = Activator.CreateInstance(operationsType, true);
            ConstructorInfo injectedConstructor = serviceType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(constructor =>
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    return parameters.Length == 3 &&
                           parameters[0].ParameterType == typeof(string) &&
                           parameters[2].ParameterType ==
                           typeof(SaveSemanticValidationPolicy);
                });
            return injectedConstructor.Invoke(
                new[] { (object)root, operations, semanticPolicy });
        }

        private static SaveSemanticValidationPolicy CreateSemanticPolicy(
            int maximumInputBytes)
        {
            var authority = new SaveSemanticValidationAuthority(
                RuntimeEnumValues("AL.Core.RealmId"),
                RuntimeEnumValues("AL.Core.ResourceType"),
                new[] { 0, 1, 2, 3 },
                new[] { 0, 1, 2, 3, 4, 5 },
                RuntimeEnumValues("AL.Core.TroopType"),
                RuntimeEnumValues("AL.Core.EquipmentSlot"),
                Array.Empty<SaveSemanticQuestRule>(),
                new[]
                {
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.Chapter,
                        "C1"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.BodyPreset,
                        "average"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.HairStyle,
                        "short"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.ArmorStyle,
                        "realm_basic"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.FaceMark,
                        "none"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.WeaponStyle,
                        "sword"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.OffhandStyle,
                        "shield")
                });
            return new SaveSemanticValidationPolicy(
                CurrentSaveFormatId,
                1,
                1,
                authority,
                maximumInputBytes);
        }

        private static int[] RuntimeEnumValues(string typeName) =>
            Enum.GetValues(GetRuntimeType(typeName))
                .Cast<object>()
                .Select(Convert.ToInt32)
                .ToArray();

        private static byte[] CreateForwardSchemaBytes(byte[] currentBytes)
        {
            string currentJson = StrictUtf8.GetString(currentBytes);
            var schemaPattern = new Regex("\"SaveSchemaVersion\"\\s*:\\s*1");
            Assert.True(
                schemaPattern.IsMatch(currentJson),
                "Expected the generated current save to contain schema version 1.");
            string forwardJson = schemaPattern.Replace(
                currentJson,
                "\"SaveSchemaVersion\": 2",
                1);
            return StrictUtf8.GetBytes(forwardJson);
        }

        private static byte[] RemoveNvs01Progress(byte[] currentBytes)
        {
            string json = StrictUtf8.GetString(currentBytes);
            const string property = "\"Nvs01Progress\":";
            int propertyStart = json.IndexOf(
                property,
                StringComparison.Ordinal);
            Assert.That(propertyStart, Is.GreaterThanOrEqualTo(0));

            int valueStart = propertyStart + property.Length;
            while (valueStart < json.Length &&
                   char.IsWhiteSpace(json[valueStart]))
            {
                valueStart++;
            }

            Assert.That(valueStart, Is.LessThan(json.Length));
            Assert.AreEqual('{', json[valueStart]);

            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var index = valueStart; index < json.Length; index++)
            {
                char character = json[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == '{')
                {
                    depth++;
                }
                else if (character == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int propertyEnd = index + 1;
                        while (propertyEnd < json.Length &&
                               char.IsWhiteSpace(json[propertyEnd]))
                        {
                            propertyEnd++;
                        }

                        Assert.That(propertyEnd, Is.LessThan(json.Length));
                        Assert.AreEqual(',', json[propertyEnd]);
                        return StrictUtf8.GetBytes(
                            json.Remove(
                                propertyStart,
                                propertyEnd - propertyStart + 1));
                    }
                }
            }

            Assert.Fail("The generated NVS-01 progress object was not closed.");
            return Array.Empty<byte>();
        }

        private static string LegacyCompatibleJson()
        {
            return "{" +
                   "\"SelectedRealm\":1," +
                   "\"Resources\":[{\"Type\":0,\"Amount\":1000}," +
                   "{\"Type\":1,\"Amount\":1000}," +
                   "{\"Type\":2,\"Amount\":500}," +
                   "{\"Type\":3,\"Amount\":500}]," +
                   "\"Buildings\":[],\"Troops\":[],\"Researches\":[]," +
                   "\"Quests\":null,\"Territories\":[],\"RealmGems\":[]," +
                   "\"Wishgate\":{\"IsEarned\":false,\"EarnReason\":\"\"," +
                   "\"LastRewardId\":\"\",\"LastRewardChosenTimestamp\":0}," +
                   "\"CurrentChapterId\":\"C1\"," +
                   "\"Warmaster\":{\"EquippedSetId\":\"\"," +
                   "\"UnlockedSetIds\":[],\"PurchasedPieceIds\":[]," +
                   "\"IsTrueWarmaster\":false,\"Level\":0,\"Experience\":0}," +
                   "\"ChampionCustomization\":{\"BodyPresetId\":\"average\"," +
                   "\"HairStyleId\":\"short\",\"ArmorStyleId\":\"realm_basic\"," +
                   "\"PrimaryR\":0.2,\"PrimaryG\":0.4,\"PrimaryB\":1.0," +
                   "\"HairR\":0.08,\"HairG\":0.06,\"HairB\":0.04," +
                   "\"CapeEnabled\":true,\"HelmetEnabled\":false}," +
                   "\"WarzoneCredits\":0,\"LastSavedTimestamp\":123}";
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveCandidateInventoryTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static Dictionary<string, byte[]> SnapshotDirectory(string root)
        {
            return Directory.GetFiles(root)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetFileName(path),
                    path => File.ReadAllBytes(path),
                    StringComparer.Ordinal);
        }

        private static void AssertDirectoryUnchanged(
            string root,
            IReadOnlyDictionary<string, byte[]> expected)
        {
            Dictionary<string, byte[]> actual = SnapshotDirectory(root);
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys);
            foreach (KeyValuePair<string, byte[]> file in expected)
            {
                CollectionAssert.AreEqual(
                    file.Value,
                    actual[file.Key],
                    $"File changed unexpectedly: {file.Key}");
            }
        }

        private static List<object> GetSummaries(object disposition)
        {
            return ((IEnumerable)GetProperty(disposition, "CandidateSummaries"))
                .Cast<object>()
                .ToList();
        }

        private static object FindSummary(object disposition, string sourceName)
        {
            object summary = GetSummaries(disposition)
                .Single(candidate =>
                    string.Equals(
                        GetProperty(candidate, "Source").ToString(),
                        sourceName,
                        StringComparison.Ordinal));
            return summary;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"Expected method {methodName}.");
            return method.Invoke(target, args);
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Expected loaded runtime type {typeName}.");
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            Assert.NotNull(target, $"Cannot read property {name} from null.");
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            Assert.NotNull(target, $"Cannot read field {name} from null.");
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            field.SetValue(target, value);
        }
    }
}
