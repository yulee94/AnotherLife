using System;
using System.IO;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class RealmSelectionMigrationRecoveryTests
    {
        [Test]
        [Category("RealmDurabilityAcceptance")]
        public void LegacySelectedRealmMigratesToOneCommittedSchemaTwoRealm()
        {
            string root = NewRoot();
            try
            {
                WriteLegacyAuthority(root, RealmId.Eldergrove);

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.AreEqual(SaveLoadStatus.LoadedPrimary, service.LastLoadStatus);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    service.GetCurrentAuthority().Status);
                Assert.AreEqual(
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                    service.CurrentSave.SaveSchemaVersion);
                Assert.That(service.CurrentSave.ProfileId, Does.Match("^alp_[0-9a-f]{32}$"));
                Assert.AreEqual(RealmId.Eldergrove, service.CurrentSave.SelectedRealm);
                Assert.AreEqual("C1", service.CurrentSave.CurrentChapterId);
                Assert.AreEqual(10, service.CurrentSave.Resources.Find(
                    resource => resource.Type == ResourceType.Food).Amount);
                Assert.AreEqual(40, service.CurrentSave.Resources.Find(
                    resource => resource.Type == ResourceType.Gold).Amount);
                AssertLegacyCommit(service.CurrentSave, RealmId.Eldergrove);
                Assert.False(File.Exists(Path.Combine(root, "save.tmp.json")));
                Assert.False(File.Exists(Path.Combine(root, "save.previous.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyUnselectedProfileMigratesWithoutInventingARealm()
        {
            string root = NewRoot();
            try
            {
                WriteLegacyAuthority(root, RealmId.None);

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.AreEqual(RealmId.None, service.CurrentSave.SelectedRealm);
                Assert.AreEqual(
                    RealmSelectionCommitState.Uncommitted,
                    (RealmSelectionCommitState)service.CurrentSave
                        .RealmSelectionCommit.State);
                Assert.AreEqual(string.Empty, service.CurrentSave
                    .RealmSelectionCommit.TransactionId);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    service.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void RepeatedLoadKeepsTheSameMigratedRealmAndTransaction()
        {
            string root = NewRoot();
            try
            {
                WriteLegacyAuthority(root, RealmId.Stonehold);
                var first = new LocalSaveGameService(root);
                first.Load();
                string profileId = first.CurrentSave.ProfileId;
                string transactionId = first.CurrentSave
                    .RealmSelectionCommit.TransactionId;

                var second = new LocalSaveGameService(root);
                second.Load();

                Assert.AreEqual(profileId, second.CurrentSave.ProfileId);
                Assert.AreEqual(transactionId, second.CurrentSave
                    .RealmSelectionCommit.TransactionId);
                AssertLegacyCommit(second.CurrentSave, RealmId.Stonehold);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        [Category("RealmDurabilityAcceptance")]
        public void LegacyMigrationIgnoresStagedNonAuthoritativeMetadata()
        {
            string root = NewRoot();
            try
            {
                WriteLegacyAuthority(root, RealmId.Stonehold);
                SaveGameData staged = CurrentSave(
                    "alp_99999999999999999999999999999999",
                    RealmId.Umbral);
                PopulateCommittedRecord(staged, RealmId.Umbral);
                File.WriteAllText(
                    Path.Combine(root, "save.tmp.json"),
                    JsonUtility.ToJson(staged, true));

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.AreEqual(SaveLoadStatus.LoadedPrimary, service.LastLoadStatus);
                Assert.AreEqual(RealmId.Stonehold, service.CurrentSave.SelectedRealm);
                AssertLegacyCommit(service.CurrentSave, RealmId.Stonehold);
                Assert.False(File.Exists(Path.Combine(root, "save.tmp.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        [Category("RealmDurabilityAcceptance")]
        public void InterruptedCommittedInstallCompletesWithoutPublishingAnotherRealm()
        {
            string root = NewRoot();
            try
            {
                WriteLegacyAuthority(root, RealmId.Crownlands);
                var migrated = new LocalSaveGameService(root);
                migrated.Load();
                string committedJson = File.ReadAllText(Path.Combine(root, "save.json"));
                File.WriteAllText(Path.Combine(root, "save.tmp.json"), committedJson);

                var recovered = new LocalSaveGameService(root);
                recovered.Load();

                Assert.AreEqual(RealmId.Crownlands, recovered.CurrentSave.SelectedRealm);
                AssertLegacyCommit(recovered.CurrentSave, RealmId.Crownlands);
                Assert.False(File.Exists(Path.Combine(root, "save.tmp.json")));
                Assert.AreEqual(ProfileWriteAuthorityStatus.Writable,
                    recovered.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void PartialCommittedMetadataNeverPublishesSelectedRealm()
        {
            string root = NewRoot();
            try
            {
                SaveGameData partial = CurrentSave(
                    "alp_11111111111111111111111111111111", RealmId.Umbral);
                partial.RealmSelectionCommit.State =
                    (int)RealmSelectionCommitState.Committed;
                partial.RealmSelectionCommit.RealmId = RealmId.Umbral;
                WriteExactAuthority(root, partial);

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.Null(service.CurrentSave);
                Assert.AreEqual(SaveLoadStatus.RecoveryRequired, service.LastLoadStatus);
                Assert.AreEqual(ProfileWriteAuthorityStatus.RecoveryRequired,
                    service.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        [Category("RealmDurabilityAcceptance")]
        public void MalformedPrimaryRecoversOneCommittedRealmFromExactBackup()
        {
            string root = NewRoot();
            try
            {
                Directory.CreateDirectory(root);
                SaveGameData committed = CurrentSave(
                    "alp_22222222222222222222222222222222", RealmId.Stonehold);
                PopulateCommittedRecord(committed, RealmId.Stonehold);
                File.WriteAllText(Path.Combine(root, "save.json"), "{not-json");
                File.WriteAllText(Path.Combine(root, "save.backup.json"),
                    JsonUtility.ToJson(committed, true));

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.AreEqual(SaveLoadStatus.RecoveredFromBackup, service.LastLoadStatus);
                Assert.AreEqual(RealmId.Stonehold, service.CurrentSave.SelectedRealm);
                Assert.AreEqual(RealmSelectionCommitState.Committed,
                    (RealmSelectionCommitState)service.CurrentSave.RealmSelectionCommit.State);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void UncommittedPrimaryCannotHideCommittedBackupRealm()
        {
            string root = NewRoot();
            try
            {
                SaveGameData primary = CurrentSave(
                    "alp_66666666666666666666666666666666", RealmId.None);
                SaveGameData backup = CurrentSave(
                    primary.ProfileId, RealmId.Umbral);
                PopulateCommittedRecord(backup, RealmId.Umbral);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "save.json"),
                    JsonUtility.ToJson(primary, true));
                File.WriteAllText(Path.Combine(root, "save.backup.json"),
                    JsonUtility.ToJson(backup, true));

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.Null(service.CurrentSave);
                Assert.AreEqual(SaveLoadStatus.RecoveryRequired, service.LastLoadStatus);
                Assert.AreEqual(ProfileWriteAuthorityStatus.RecoveryRequired,
                    service.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ConflictingDurableRealmCandidatesRemainRecoveryRequired()
        {
            string root = NewRoot();
            try
            {
                SaveGameData primary = CurrentSave(
                    "alp_55555555555555555555555555555555", RealmId.Crownlands);
                PopulateCommittedRecord(primary, RealmId.Crownlands);
                SaveGameData backup = CurrentSave(
                    primary.ProfileId, RealmId.Umbral);
                PopulateCommittedRecord(backup, RealmId.Umbral);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "save.json"),
                    JsonUtility.ToJson(primary, true));
                File.WriteAllText(Path.Combine(root, "save.backup.json"),
                    JsonUtility.ToJson(backup, true));

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.Null(service.CurrentSave);
                Assert.AreEqual(SaveLoadStatus.RecoveryRequired, service.LastLoadStatus);
                Assert.AreEqual(ProfileWriteAuthorityStatus.RecoveryRequired,
                    service.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        [Category("RealmDurabilityAcceptance")]
        public void ReplayedCommittedRecordAndStaleSoftLockRemainOneRealm()
        {
            string root = NewRoot();
            try
            {
                SaveGameData committed = CurrentSave(
                    "alp_33333333333333333333333333333333", RealmId.Eldergrove);
                PopulateCommittedRecord(committed, RealmId.Eldergrove);
                WriteExactAuthority(root, committed);
                File.WriteAllText(Path.Combine(root, "save.lock"), "stale-owner");

                var service = new LocalSaveGameService(root);
                service.Load();

                Assert.AreEqual(RealmId.Eldergrove, service.CurrentSave.SelectedRealm);
                Assert.AreEqual("rsel_44444444444444444444444444444444",
                    service.CurrentSave.RealmSelectionCommit.TransactionId);
                Assert.AreEqual(ProfileWriteAuthorityStatus.Writable,
                    service.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static void AssertLegacyCommit(SaveGameData save, RealmId realm)
        {
            RealmSelectionCommitData commit = save.RealmSelectionCommit;
            Assert.AreEqual(
                RealmSelectionCommitState.Committed,
                (RealmSelectionCommitState)commit.State);
            Assert.AreEqual(realm, commit.RealmId);
            Assert.That(commit.TransactionId, Does.Match("^rsel_[0-9a-f]{32}$"));
            Assert.That(commit.IntentSha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.AreEqual(
                RealmSelectionCommitProvenance.LegacyMigration,
                (RealmSelectionCommitProvenance)commit.Provenance);
            Assert.AreEqual(1, commit.MigrationVersion);
            Assert.AreEqual(
                RealmSelectionCommitPublicationState.NotApplicable,
                (RealmSelectionCommitPublicationState)commit.PublicationState);
            Assert.AreEqual(string.Empty, commit.CommittedEventId);
        }

        private static void WriteLegacyAuthority(string root, RealmId realm)
        {
            Directory.CreateDirectory(root);
            var save = new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                ProfileInitializationVersion = SaveAuthorityTechnicalLimits
                    .LegacyProfileInitializationVersion,
                ProfileId = string.Empty,
                SelectedRealm = realm,
                RealmSelectionCommit = new RealmSelectionCommitData(),
                CurrentChapterId = "C1",
                LastSavedTimestamp = 1
            };
            save.Resources.Add(new ResourceData { Type = ResourceType.Food, Amount = 10 });
            save.Resources.Add(new ResourceData { Type = ResourceType.Wood, Amount = 20 });
            save.Resources.Add(new ResourceData { Type = ResourceType.Stone, Amount = 30 });
            save.Resources.Add(new ResourceData { Type = ResourceType.Gold, Amount = 40 });
            string json = JsonUtility.ToJson(save, true);
            File.WriteAllText(Path.Combine(root, "save.json"), json);
            File.WriteAllText(Path.Combine(root, "save.backup.json"), json);
        }

        private static SaveGameData CurrentSave(string profileId, RealmId realm)
        {
            var save = new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                ProfileInitializationVersion = SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion,
                ProfileId = profileId,
                SelectedRealm = realm,
                RealmSelectionCommit = new RealmSelectionCommitData(),
                CurrentChapterId = "C1",
                LastSavedTimestamp = 1
            };
            save.Resources.Add(new ResourceData { Type = ResourceType.Food, Amount = 10 });
            save.Resources.Add(new ResourceData { Type = ResourceType.Wood, Amount = 20 });
            save.Resources.Add(new ResourceData { Type = ResourceType.Stone, Amount = 30 });
            save.Resources.Add(new ResourceData { Type = ResourceType.Gold, Amount = 40 });
            save.Resources.Add(new ResourceData { Type = ResourceType.ManaStone, Amount = 0 });
            save.Resources.Add(new ResourceData { Type = ResourceType.Ore, Amount = 0 });
            return save;
        }

        private static void PopulateCommittedRecord(SaveGameData save, RealmId realm)
        {
            string canonical = realm == RealmId.Crownlands ? "crownlands" :
                realm == RealmId.Stonehold ? "stonehold" :
                realm == RealmId.Eldergrove ? "eldergrove" : "umbral";
            save.RealmSelectionCommit = new RealmSelectionCommitData
            {
                ContractVersion = RealmSelectionCommitData.CurrentContractVersion,
                State = (int)RealmSelectionCommitState.Committed,
                RealmId = realm,
                CanonicalRealmId = canonical,
                TransactionId = "rsel_44444444444444444444444444444444",
                IntentSha256 = new string('a', 64),
                CatalogAuthorityId = "al_realm_catalog",
                CatalogVersion = RealmCatalogRuntime.SupportedVersion,
                CommitRevision = 1,
                CommittedUnixTimeMilliseconds = 1000,
                Provenance = (int)RealmSelectionCommitProvenance.InitialSelection,
                SourceSaveSchemaVersion = SaveAuthorityTechnicalLimits
                    .IdentityAwareSaveSchemaVersion,
                PublicationState = (int)RealmSelectionCommitPublicationState.Delivered,
                CommittedEventId = "rsevt_44444444444444444444444444444444"
            };
        }

        private static void WriteExactAuthority(string root, SaveGameData save)
        {
            Directory.CreateDirectory(root);
            string json = JsonUtility.ToJson(save, true);
            File.WriteAllText(Path.Combine(root, "save.json"), json);
            File.WriteAllText(Path.Combine(root, "save.backup.json"), json);
        }

        private static string NewRoot() => Path.Combine(
            Path.GetTempPath(),
            "AnotherLife-RealmMigrationTests",
            Guid.NewGuid().ToString("N"));

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
