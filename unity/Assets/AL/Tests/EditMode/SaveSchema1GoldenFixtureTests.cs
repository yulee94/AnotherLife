using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AL.Data.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class SaveSchema1GoldenFixtureTests
    {
        private const string FixtureDirectoryName = "SaveSchema1";
        private const string ManifestFileName = "manifest.json";

        [Serializable]
        public sealed class GoldenFixtureManifest
        {
            public int manifestVersion;
            public string saveFormatId;
            public int currentSchemaVersion;
            public int[] supportedUpgradeSources;
            public string futureSchemaPolicy;
            public string bothInvalidPolicy;
            public GoldenFixtureEntry[] fixtures;
        }

        [Serializable]
        public sealed class GoldenFixtureEntry
        {
            public string id;
            public string kind;
            public string file;
            public string sha256;
            public int sourceSchemaVersion;
            public int expectedSchemaVersion;
            public string expectedLoadStatus;
        }

        [Test]
        public void ManifestLimitsMigrationToPreSchemaAndFailsClosedElsewhere()
        {
            GoldenFixtureManifest manifest = LoadManifest();

            Assert.AreEqual(1, manifest.manifestVersion);
            Assert.AreEqual(SaveGameData.CurrentSaveFormatId, manifest.saveFormatId);
            Assert.AreEqual(SaveGameData.CurrentSaveSchemaVersion, manifest.currentSchemaVersion);
            CollectionAssert.AreEqual(new[] { 0, 1 }, manifest.supportedUpgradeSources);
            Assert.AreEqual("preserve_read_only", manifest.futureSchemaPolicy);
            Assert.AreEqual("preserve_read_only", manifest.bothInvalidPolicy);
        }

        [Test]
        public void ManifestPinsEveryCheckedInFixtureBySha256()
        {
            GoldenFixtureManifest manifest = LoadManifest();

            Assert.That(manifest.fixtures, Is.Not.Null.And.Length.EqualTo(4));
            foreach (GoldenFixtureEntry fixture in manifest.fixtures)
            {
                Assert.That(fixture.id, Is.Not.Null.And.Not.Empty);
                Assert.That(fixture.kind, Is.Not.Null.And.Not.Empty);
                Assert.That(fixture.file, Is.Not.Null.And.Not.Empty);
                Assert.That(fixture.sha256, Does.Match("^[0-9a-f]{64}$"));
                Assert.AreEqual(
                    fixture.sha256,
                    ComputeSha256Hex(ReadFixture(fixture.file)),
                    fixture.id);
            }
        }

        [Test]
        public void PreSchemaGoldenMigratesWithoutLosingApprovedProgress()
        {
            string root = CreateRoot();
            try
            {
                byte[] legacyBytes = ReadFixture("pre-schema-v0.json");
                File.WriteAllBytes(Path.Combine(root, "save.json"), legacyBytes);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual("LoadedPrimary", Status(service));
                Assert.That(
                    (string)GetProperty(service, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-PRESCHEMA-MIGRATED"));
                SaveGameData migrated = CurrentSave(service);
                Assert.NotNull(migrated);
                Assert.AreEqual(SaveGameData.CurrentSaveFormatId, migrated.SaveFormatId);
                Assert.AreEqual(SaveGameData.CurrentSaveSchemaVersion, migrated.SaveSchemaVersion);
                Assert.AreEqual(
                    SaveGameData.CurrentProfileInitializationVersion,
                    migrated.ProfileInitializationVersion);
                Assert.AreEqual("Crownlands", migrated.SelectedRealm.ToString());
                Assert.AreEqual(4300, migrated.WarzoneCredits);
                Assert.AreEqual(
                    1033546L,
                    migrated.Resources.Single(resource => resource.Type.ToString() == "Food").Amount);

                string[] quarantines = Directory.GetFiles(root, "save.json.corrupt-*");
                Assert.AreEqual(1, quarantines.Length);
                CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(quarantines[0]));
                AssertCurrentMetadata(Path.Combine(root, "save.json"));
                AssertCurrentMetadata(Path.Combine(root, "save.backup.json"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void MigratedGoldenRetryIsIdempotentAndByteStable()
        {
            string root = CreateRoot();
            try
            {
                File.WriteAllBytes(
                    Path.Combine(root, "save.json"),
                    ReadFixture("pre-schema-v0.json"));
                object first = CreateSaveService(root);
                Invoke(first, "Load");
                Assert.AreEqual("LoadedPrimary", Status(first));
                Dictionary<string, byte[]> committed = SnapshotDirectory(root);

                object retry = CreateSaveService(root);
                Invoke(retry, "Load");

                Assert.AreEqual("LoadedPrimary", Status(retry));
                AssertDirectoryUnchanged(root, committed);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void Schema1GoldenRoundTripPreservesProfileData()
        {
            string root = CreateRoot();
            try
            {
                File.WriteAllBytes(
                    Path.Combine(root, "save.json"),
                    ReadFixture("current-schema-v1.json"));
                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual("MigratedSchemaOne", Status(service));
                SaveGameData loaded = CurrentSave(service);
                Assert.NotNull(loaded);
                Assert.AreEqual("Eldergrove", loaded.SelectedRealm.ToString());
                Assert.AreEqual(731, loaded.WarzoneCredits);
                Assert.AreEqual(42L, loaded.Resources.Single(
                    resource => resource.Type.ToString() == "ManaStone").Amount);

                InvokeLifecycleSave(service);
                Assert.AreEqual(
                    "SavedPrimary",
                    GetProperty(service, "LastSaveStatus").ToString());
                object reloadedService = CreateSaveService(root);
                Invoke(reloadedService, "Load");
                SaveGameData reloaded = CurrentSave(reloadedService);
                Assert.NotNull(reloaded);
                Assert.AreEqual("Eldergrove", reloaded.SelectedRealm.ToString());
                Assert.AreEqual(731, reloaded.WarzoneCredits);
                Assert.AreEqual(42L, reloaded.Resources.Single(
                    resource => resource.Type.ToString() == "ManaStone").Amount);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void FutureMapDisclosureExtensionRemainsReadOnlyAndByteExact()
        {
            string root = CreateRoot();
            try
            {
                string current = Encoding.UTF8.GetString(
                    ReadFixture("current-schema-v1.json"));
                string futureMapState = current.Replace(
                    "  \"WarzoneCredits\": 731,",
                    "  \"MapDisclosure\": {\n" +
                    "    \"Version\": 2,\n" +
                    "    \"FutureField\": true\n" +
                    "  },\n" +
                    "  \"WarzoneCredits\": 731,");
                Assert.AreNotEqual(current, futureMapState);
                byte[] futureBytes = Encoding.UTF8.GetBytes(futureMapState);
                File.WriteAllBytes(Path.Combine(root, "save.json"), futureBytes);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual("LoadedPrimaryWithPreservedUnknown", Status(service));
                Assert.Null(CurrentSave(service));
                Assert.That(
                    (string)GetProperty(service, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-PRIMARY-PRESERVED-UNKNOWN"));
                CollectionAssert.AreEqual(
                    futureBytes,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void TruncatedPrimaryRecoversFromGoldenBackupAndPreservesExactEvidence()
        {
            string root = CreateRoot();
            try
            {
                byte[] truncated = ReadFixture("truncated-schema-v1.txt");
                byte[] backup = ReadFixture("current-schema-v1.json");
                File.WriteAllBytes(Path.Combine(root, "save.json"), truncated);
                File.WriteAllBytes(Path.Combine(root, "save.backup.json"), backup);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual("MigratedSchemaOne", Status(service));
                CollectionAssert.AreEqual(
                    backup,
                    File.ReadAllBytes(Path.Combine(root, "save.backup.json")));
                string[] quarantines = Directory.GetFiles(root, "save.json.corrupt-*");
                Assert.AreEqual(1, quarantines.Length);
                CollectionAssert.AreEqual(truncated, File.ReadAllBytes(quarantines[0]));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void RecoveryMarkerChecksumMismatchFailsClosedWithoutMutation()
        {
            string root = CreateRoot();
            try
            {
                ArrangeCompletedCorruptionRecovery(root);
                string witnessPath = Path.Combine(root, "save.profile-migration.v1");
                Assert.True(File.Exists(witnessPath));
                File.WriteAllText(witnessPath, "{ \"ProfileId\": \"tampered\" }");
                Dictionary<string, byte[]> tamperedEvidence = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual("RecoveryRequired", Status(service));
                Assert.Null(CurrentSave(service));
                Assert.That(
                    (string)GetProperty(service, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-SCHEMA-CORRELATION-CONFLICT"));
                AssertDirectoryUnchanged(root, tamperedEvidence);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void FutureSchemaGoldenRemainsAuthoritativeReadOnlyWithoutDowngrade()
        {
            string root = CreateRoot();
            try
            {
                byte[] future = ReadFixture("future-schema-v2.json");
                byte[] current = ReadFixture("current-schema-v1.json");
                File.WriteAllBytes(Path.Combine(root, "save.json"), future);
                File.WriteAllBytes(Path.Combine(root, "save.backup.json"), current);
                Dictionary<string, byte[]> before = SnapshotDirectory(root);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual("LoadedForwardSchemaReadOnly", Status(service));
                Assert.Null(CurrentSave(service));
                object disposition = GetProperty(service, "LastLoadDisposition");
                Assert.AreEqual(
                    "Primary",
                    GetProperty(disposition, "SelectedSource").ToString());
                Assert.False((bool)GetProperty(disposition, "IsWritable"));
                Assert.False((bool)GetProperty(disposition, "DiskChanged"));
                AssertDirectoryUnchanged(root, before);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static void ArrangeCompletedCorruptionRecovery(string root)
        {
            File.WriteAllBytes(
                Path.Combine(root, "save.json"),
                ReadFixture("truncated-schema-v1.txt"));
            File.WriteAllBytes(
                Path.Combine(root, "save.backup.json"),
                ReadFixture("current-schema-v1.json"));
            object recovery = CreateSaveService(root);
            Invoke(recovery, "Load");
            Assert.AreEqual("MigratedSchemaOne", Status(recovery));
            Assert.True(File.Exists(Path.Combine(root, "save.profile-migration.v1")));
        }

        private static GoldenFixtureManifest LoadManifest()
        {
            string json = File.ReadAllText(FixturePath(ManifestFileName));
            GoldenFixtureManifest manifest =
                JsonUtility.FromJson<GoldenFixtureManifest>(json);
            Assert.NotNull(manifest);
            return manifest;
        }

        private static byte[] ReadFixture(string fileName) =>
            File.ReadAllBytes(FixturePath(fileName));

        private static string FixturePath(string fileName) =>
            Path.Combine(
                Application.dataPath,
                "AL",
                "Tests",
                "EditMode",
                "Fixtures",
                FixtureDirectoryName,
                fileName);

        private static string ComputeSha256Hex(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] digest = sha256.ComputeHash(bytes);
            var result = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                result.Append(value.ToString("x2"));
            }

            return result.ToString();
        }

        private static void AssertCurrentMetadata(string path)
        {
            SaveGameData save = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
            Assert.NotNull(save);
            Assert.AreEqual(SaveGameData.CurrentSaveFormatId, save.SaveFormatId);
            Assert.AreEqual(SaveGameData.CurrentSaveSchemaVersion, save.SaveSchemaVersion);
            Assert.AreEqual(
                SaveGameData.CurrentProfileInitializationVersion,
                save.ProfileInitializationVersion);
        }

        private static object CreateSaveService(string root)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor);
            return constructor.Invoke(new object[] { root });
        }

        private static SaveGameData CurrentSave(object service) =>
            (SaveGameData)GetProperty(service, "CurrentSave");

        private static string Status(object service) =>
            GetProperty(service, "LastLoadStatus").ToString();

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

        private static void InvokeLifecycleSave(object service)
        {
            Type containment = service.GetType().Assembly.GetType(
                "AL.Services.Local.ProfileMutationContainment",
                throwOnError: true);
            MethodInfo method = containment.GetMethod(
                "InvokeLifecycleSave",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(null, new[] { service });
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
            Assert.NotNull(target);
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-Schema1GoldenTests",
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

        private static Dictionary<string, byte[]> SnapshotDirectory(string root) =>
            Directory.GetFiles(root)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    Path.GetFileName,
                    File.ReadAllBytes,
                    StringComparer.Ordinal);

        private static void AssertDirectoryUnchanged(
            string root,
            IReadOnlyDictionary<string, byte[]> expected)
        {
            Dictionary<string, byte[]> actual = SnapshotDirectory(root);
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys);
            foreach (KeyValuePair<string, byte[]> entry in expected)
            {
                CollectionAssert.AreEqual(
                    entry.Value,
                    actual[entry.Key],
                    entry.Key);
            }
        }
    }
}
