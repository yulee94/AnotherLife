using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class ProfileIdentityMigrationInstallationTests
    {
        [Test]
        public void AllMissingCreatePublishesSchemaTwoWritableIdentity()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                service.CreateNewSave(RealmId.Crownlands);

                Assert.AreEqual(SaveOperationStatus.SavedPrimary, service.LastSaveStatus);
                Assert.NotNull(service.CurrentSave);
                Assert.AreEqual(2, service.CurrentSave.SaveSchemaVersion);
                Assert.That(service.CurrentSave.ProfileId, Does.Match("^alp_[0-9a-f]{32}$"));
                Assert.AreNotEqual(
                    "alp_00000000000000000000000000000000",
                    service.CurrentSave.ProfileId);
                ProfileWriteAuthoritySnapshot authority = service.GetCurrentAuthority();
                Assert.AreEqual(ProfileWriteAuthorityStatus.Writable, authority.Status);
                Assert.AreEqual(service.CurrentSave.ProfileId, authority.ProfileId);
                Assert.IsFalse(ProfileMutationContainment.ProductionWriteActivationEnabled);
                Assert.AreEqual(
                    32,
                    ProfileMutationSurfaceCatalog.ProductionSurfaces.Count);
                Assert.AreEqual(
                    ProfileMutationSurfaceDisposition.NarrowProfileBoundOperation,
                    ProfileMutationSurfaceCatalog.ProductionSurfaces.Single(item =>
                        item.StableId == ProfileMutationSurfaceIds.RealmSelection).Disposition);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaOneGoldenMigratesAtomicallyThenExposesWritable()
        {
            string root = CreateRoot();
            try
            {
                byte[] predecessor = File.ReadAllBytes(
                    Path.Combine(
                        Application.dataPath,
                        "AL",
                        "Tests",
                        "EditMode",
                        "Fixtures",
                        "SaveSchema1",
                        "current-schema-v1.json"));
                File.WriteAllBytes(Path.Combine(root, "save.json"), predecessor);
                LocalSaveGameService service = CreateService(root);
                service.Load();

                Assert.AreEqual(SaveLoadStatus.MigratedSchemaOne, service.LastLoadStatus);
                Assert.NotNull(service.CurrentSave);
                Assert.AreEqual(2, service.CurrentSave.SaveSchemaVersion);
                Assert.AreEqual(RealmId.Eldergrove, service.CurrentSave.SelectedRealm);
                Assert.AreEqual(731, service.CurrentSave.WarzoneCredits);
                Assert.That(service.CurrentSave.ProfileId, Does.Match("^alp_[0-9a-f]{32}$"));
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    service.GetCurrentAuthority().Status);
                CollectionAssert.AreEqual(
                    predecessor,
                    File.ReadAllBytes(Path.Combine(root, "save.backup.json")));
                Assert.True(File.Exists(Path.Combine(root, "save.profile-migration.v1")));
                Assert.False(File.Exists(Path.Combine(root, "save.profile-migration.pending")));

                LocalSaveGameService restarted = CreateService(root);
                restarted.Load();
                Assert.AreEqual(SaveLoadStatus.MigratedSchemaOne, restarted.LastLoadStatus);
                Assert.AreEqual(
                    service.CurrentSave.ProfileId,
                    restarted.CurrentSave.ProfileId);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    restarted.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void MixedSchemaWithoutWitnessStaysRecoveryRequiredAndPreservesBothInvalid()
        {
            string root = CreateRoot();
            try
            {
                File.WriteAllBytes(
                    Path.Combine(root, "save.json"),
                    SchemaTwoBytes("alp_0123456789abcdef0123456789abcdef"));
                File.WriteAllBytes(
                    Path.Combine(root, "save.backup.json"),
                    SchemaOneBytes(RealmId.Umbral, 9));
                Dictionary<string, byte[]> before = Snapshot(root);
                LocalSaveGameService service = CreateService(root);
                service.Load();

                Assert.AreEqual(SaveLoadStatus.RecoveryRequired, service.LastLoadStatus);
                Assert.IsNull(service.CurrentSave);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.RecoveryRequired,
                    service.GetCurrentAuthority().Status);
                AssertDirectoryUnchanged(root, before);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ForwardDegradedAndMalformedRemainNonWritable()
        {
            string root = CreateRoot();
            try
            {
                SaveGameData future = JsonUtility.FromJson<SaveGameData>(
                    Encoding.UTF8.GetString(SchemaTwoBytes(
                        "alp_0123456789abcdef0123456789abcdef")));
                future.SaveSchemaVersion = 3;
                File.WriteAllBytes(
                    Path.Combine(root, "save.json"),
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(future, true)));
                LocalSaveGameService service = CreateService(root);
                service.Load();
                Assert.AreEqual(
                    SaveLoadStatus.LoadedForwardSchemaReadOnly,
                    service.LastLoadStatus);
                Assert.AreNotEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    service.GetCurrentAuthority().Status);

                File.WriteAllText(Path.Combine(root, "save.json"), "{");
                LocalSaveGameService malformed = CreateService(root);
                malformed.Load();
                Assert.AreNotEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    malformed.GetCurrentAuthority().Status);
                Assert.IsNull(malformed.CurrentSave);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void OrdinarySurfacesStayContainedUnderRealWritable()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                service.CreateNewSave(RealmId.Stonehold);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    service.GetCurrentAuthority().Status);
                byte[] diskBefore = File.ReadAllBytes(Path.Combine(root, "save.json"));
                long goldBefore = service.CurrentSave.Resources.Single(item =>
                    item.Type == ResourceType.Gold).Amount;

                var resources = new LocalResourceService(service);
                Assert.AreEqual(
                    EconomyMutationStatus.RejectedProfileNotWritable,
                    resources.TryAddResource(ResourceType.Gold, 1).Status);
                service.Save();
                Assert.AreEqual(goldBefore, service.CurrentSave.Resources.Single(item =>
                    item.Type == ResourceType.Gold).Amount);
                CollectionAssert.AreEqual(
                    diskBefore,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void BootloaderAcceptsMigratedSchemaOneWhenCurrentSaveExists()
        {
            MethodInfo method = typeof(AL.Core.Bootloader).GetMethod(
                "IsApprovedLoadSuccess",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var service = new ScriptedSaveService(
                new SaveGameData { ProfileId = "alp_0123456789abcdef0123456789abcdef" },
                SaveLoadStatus.MigratedSchemaOne);
            Assert.IsTrue((bool)method.Invoke(null, new object[] { service }));
            Assert.IsFalse(
                (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        new ScriptedSaveService(null, SaveLoadStatus.MigratedSchemaOne)
                    }));
            Assert.IsFalse(
                (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        new ScriptedSaveService(
                            new SaveGameData(),
                            SaveLoadStatus.RecoveryRequired)
                    }));
        }

        private static LocalSaveGameService CreateService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(new object[] { root });
        }

        private static byte[] SchemaOneBytes(RealmId realm, long food)
        {
            var save = new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = 1,
                ProfileInitializationVersion = 1,
                ProfileId = string.Empty,
                SelectedRealm = realm,
                CurrentChapterId = "C1",
                Resources = new List<ResourceData>
                {
                    new ResourceData { Type = ResourceType.Food, Amount = food },
                    new ResourceData { Type = ResourceType.Wood, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Stone, Amount = 500 },
                    new ResourceData { Type = ResourceType.Gold, Amount = 500 },
                    new ResourceData { Type = ResourceType.ManaStone, Amount = 150 },
                    new ResourceData { Type = ResourceType.Ore, Amount = 150 }
                }
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));
        }

        private static byte[] SchemaTwoBytes(string profileId)
        {
            var save = new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = 2,
                ProfileInitializationVersion = 1,
                ProfileId = profileId,
                SelectedRealm = RealmId.Crownlands,
                CurrentChapterId = "C1",
                Resources = new List<ResourceData>
                {
                    new ResourceData { Type = ResourceType.Food, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Wood, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Stone, Amount = 500 },
                    new ResourceData { Type = ResourceType.Gold, Amount = 500 },
                    new ResourceData { Type = ResourceType.ManaStone, Amount = 150 },
                    new ResourceData { Type = ResourceType.Ore, Amount = 150 }
                }
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-Schema2Install",
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

        private static Dictionary<string, byte[]> Snapshot(string root)
        {
            return Directory.GetFiles(root)
                .ToDictionary(
                    path => Path.GetFileName(path),
                    File.ReadAllBytes,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static void AssertDirectoryUnchanged(
            string root,
            Dictionary<string, byte[]> before)
        {
            Dictionary<string, byte[]> after = Snapshot(root);
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (string key in before.Keys)
            {
                CollectionAssert.AreEqual(before[key], after[key], key);
            }
        }

        private sealed class ScriptedSaveService : ISaveGameService
        {
            internal ScriptedSaveService(SaveGameData save, SaveLoadStatus status)
            {
                CurrentSave = save;
                LastLoadStatus = status;
            }

            public SaveGameData CurrentSave { get; }
            public SaveLoadStatus LastLoadStatus { get; }
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;
            public void Save() { }
            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) { }
            public void DeleteSave() { }
        }
    }
}
