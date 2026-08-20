using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.ChampionMode.Customization;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class ProfileMutationContainmentFoundationTests
    {
        [Test]
        public void ProductionContainmentCatalogIsExecutableVersionedAndClosed()
        {
            Assembly runtime = typeof(LocalSaveGameService).Assembly;
            Type catalog = runtime.GetType(
                "AL.Services.Local.ProfileMutationSurfaceCatalog",
                throwOnError: false);

            Assert.NotNull(
                catalog,
                "A closed executable production mutation-surface catalog is required.");
            Assert.That(
                catalog.GetProperty(
                    "ContractVersion",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                catalog.GetMethod(
                    "ValidateProductionCoverage",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
        }

        [Test]
        public void ProductionCatalogExactlyCoversBootDormantAndIndirectSurfaces()
        {
            Type offlineStack = typeof(Bootloader).Assembly.GetType(
                "AL.Core.OfflineServiceStack",
                throwOnError: true);
            var required = (IEnumerable<Type>)offlineStack.GetField(
                    "RequiredServiceTypes",
                    BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);

            ProfileMutationCatalogValidationResult exact =
                ProfileMutationSurfaceCatalog.ValidateProductionCoverage(
                    required);
            Assert.True(exact.IsValid, string.Join("\n", exact.Diagnostics));

            ProfileMutationSurfaceDescriptor[] descriptors =
                ProfileMutationSurfaceCatalog.ProductionSurfaces.ToArray();
            Assert.That(descriptors.Count(item => item.IsBootRegistration), Is.EqualTo(21));
            Assert.That(
                descriptors.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(descriptors.Length));
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    ProfileMutationSurfaceIds.SideQuest,
                    ProfileMutationSurfaceIds.ChampionCustomization,
                    ProfileMutationSurfaceIds.ChampionArena,
                    ProfileMutationSurfaceIds.DemoInitializer,
                    ProfileMutationSurfaceIds.KingdomNvs01,
                    ProfileMutationSurfaceIds.RealmSelection,
                    ProfileMutationSurfaceIds.Nvs01Progress,
                    ProfileMutationSurfaceIds.DeleteSave
                },
                descriptors.Select(item => item.StableId).ToArray());
            Assert.That(
                descriptors.Single(item =>
                    item.StableId == ProfileMutationSurfaceIds.SideQuest).Disposition,
                Is.EqualTo(ProfileMutationSurfaceDisposition.Dormant));
            Assert.That(
                descriptors.Single(item =>
                    item.StableId == ProfileMutationSurfaceIds.DeleteSave).Disposition,
                Is.EqualTo(ProfileMutationSurfaceDisposition.Dormant));
        }

        [Test]
        public void ProductionCatalogRejectsMissingDuplicateUnknownAndNullRegistrations()
        {
            Type offlineStack = typeof(Bootloader).Assembly.GetType(
                "AL.Core.OfflineServiceStack",
                throwOnError: true);
            Type[] exact = ((IEnumerable<Type>)offlineStack.GetField(
                    "RequiredServiceTypes",
                    BindingFlags.Public | BindingFlags.Static)
                .GetValue(null)).ToArray();

            Assert.False(ProfileMutationSurfaceCatalog
                .ValidateProductionCoverage(exact.Skip(1)).IsValid);
            Assert.False(ProfileMutationSurfaceCatalog
                .ValidateProductionCoverage(exact.Concat(new[] { exact[0] })).IsValid);
            Assert.False(ProfileMutationSurfaceCatalog
                .ValidateProductionCoverage(exact.Concat(new[] { typeof(string) })).IsValid);
            Assert.False(ProfileMutationSurfaceCatalog
                .ValidateProductionCoverage(exact.Concat(new Type[] { null })).IsValid);
            Assert.False(ProfileMutationSurfaceCatalog
                .ValidateProductionCoverage(null).IsValid);
        }

        [Test]
        public void HardLatchRejectsForgedWritableAuthorityBeforeReadingCurrentSave()
        {
            var forged = new ThrowingWritableSaveService();
            Type containment = typeof(ProfileMutationContainment);
            MethodInfo mutable = containment.GetMethod(
                "TryGetMutableSave",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo lifecycle = containment.GetMethod(
                "CanInvokeLifecycleSave",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo delete = containment.GetMethod(
                "CanInvokeDeleteSave",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(mutable);
            Assert.NotNull(lifecycle);
            Assert.NotNull(delete);
            object[] arguments =
            {
                forged,
                ProfileMutationSurfaceIds.BossLoot,
                null
            };

            Assert.False(ProfileMutationContainment.ProductionWriteActivationEnabled);
            Assert.False((bool)mutable.Invoke(null, arguments));
            Assert.IsNull(arguments[2]);
            Assert.True((bool)lifecycle.Invoke(null, new object[] { forged }));
            Assert.False((bool)delete.Invoke(null, new object[] { forged }));
            Assert.That(forged.CurrentSaveReadCount, Is.Zero);
            Assert.That(forged.AuthorityReadCount, Is.Zero);
        }

        [Test]
        public void DeleteSaveIsContainedBeforeFileOrRuntimeMutation()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-ContainmentTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ConstructorInfo constructor = typeof(LocalSaveGameService)
                    .GetConstructor(
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        binder: null,
                        types: new[] { typeof(string) },
                        modifiers: null);
                Assert.NotNull(constructor);
                var service = (LocalSaveGameService)constructor.Invoke(
                    new object[] { root });
                service.CreateNewSave(RealmId.Crownlands);
                SaveGameData published = service.CurrentSave;
                string objectBefore = JsonUtility.ToJson(published);
                SaveLoadStatus loadStatusBefore = service.LastLoadStatus;
                string loadMessageBefore = service.LastLoadMessage;
                SaveOperationStatus saveStatusBefore = service.LastSaveStatus;
                string saveMessageBefore = service.LastSaveMessage;
                var filesBefore = Directory.GetFiles(root)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToDictionary(
                        path => Path.GetFileName(path),
                        File.ReadAllBytes,
                        StringComparer.Ordinal);

                service.DeleteSave();

                Assert.AreSame(published, service.CurrentSave);
                Assert.AreEqual(objectBefore, JsonUtility.ToJson(published));
                Assert.AreEqual(loadStatusBefore, service.LastLoadStatus);
                Assert.AreEqual(loadMessageBefore, service.LastLoadMessage);
                Assert.AreEqual(saveStatusBefore, service.LastSaveStatus);
                Assert.AreEqual(saveMessageBefore, service.LastSaveMessage);
                var filesAfter = Directory.GetFiles(root)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToDictionary(
                        path => Path.GetFileName(path),
                        File.ReadAllBytes,
                        StringComparer.Ordinal);
                CollectionAssert.AreEquivalent(filesBefore.Keys, filesAfter.Keys);
                foreach (string fileName in filesBefore.Keys)
                {
                    CollectionAssert.AreEqual(
                        filesBefore[fileName],
                        filesAfter[fileName],
                        fileName);
                }
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void ManualSaveIsContainedBeforeNormalizationSerializationOrIo()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-ContainmentTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ConstructorInfo constructor = typeof(LocalSaveGameService)
                    .GetConstructor(
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        binder: null,
                        types: new[] { typeof(string) },
                        modifiers: null);
                Assert.NotNull(constructor);
                var service = (LocalSaveGameService)constructor.Invoke(
                    new object[] { root });
                service.CreateNewSave(RealmId.Crownlands);
                SaveGameData published = service.CurrentSave;
                Assert.NotNull(published);
                string primary = Path.Combine(root, "save.json");
                string backup = Path.Combine(root, "save.backup.json");
                byte[] primaryBefore = File.ReadAllBytes(primary);
                byte[] backupBefore = File.ReadAllBytes(backup);
                published.Troops = null;
                string objectBefore = JsonUtility.ToJson(published);

                service.Save();

                Assert.AreSame(published, service.CurrentSave);
                Assert.AreEqual(objectBefore, JsonUtility.ToJson(published));
                Assert.IsNull(published.Troops, "Containment must precede default normalization.");
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primary));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backup));
                Assert.AreEqual(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    service.LastSaveStatus);
                StringAssert.StartsWith(
                    "AL-SAVE-MANUAL-WRITE-CONTAINED:",
                    service.LastSaveMessage);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void LifecyclePersistWritesWhileManualSaveStaysContained()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-ContainmentTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ConstructorInfo constructor = typeof(LocalSaveGameService)
                    .GetConstructor(
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        binder: null,
                        types: new[] { typeof(string) },
                        modifiers: null);
                Assert.NotNull(constructor);
                var service = (LocalSaveGameService)constructor.Invoke(
                    new object[] { root });
                service.CreateNewSave(RealmId.Crownlands);
                Assert.NotNull(service.CurrentSave);
                string primary = Path.Combine(root, "save.json");
                Assert.True(File.Exists(primary));

                MethodInfo invokeLifecycle = typeof(ProfileMutationContainment).GetMethod(
                    "InvokeLifecycleSave",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(invokeLifecycle);
                invokeLifecycle.Invoke(null, new object[] { service });

                Assert.AreEqual(
                    SaveOperationStatus.SavedPrimary,
                    service.LastSaveStatus,
                    service.LastSaveMessage);
                Assert.False(ProfileMutationContainment.ProductionWriteActivationEnabled);

                service.Save();
                Assert.AreEqual(
                    SaveOperationStatus.SaveFailedPreviousPreserved,
                    service.LastSaveStatus);
                StringAssert.StartsWith(
                    "AL-SAVE-MANUAL-WRITE-CONTAINED:",
                    service.LastSaveMessage);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SchemaOneAuthorityUsesOnlyNarrowRealmAndNvsAdapters()
        {
            Assembly runtime = typeof(LocalSaveGameService).Assembly;
            Assert.NotNull(runtime.GetType(
                "AL.Services.Local.ILegacyRealmSelectionCandidateStore",
                throwOnError: false));
            Assert.NotNull(runtime.GetType(
                "AL.Services.Local.INvs01LegacyCandidateStore",
                throwOnError: false));

            string source = ReadScript(
                "Services/Local/ISaveGameCandidateStore.cs");
            StringAssert.Contains(
                "ILegacyRealmSelectionCandidateStore",
                source);
            StringAssert.Contains("INvs01LegacyCandidateStore", source);
        }

        [Test]
        public void LifecycleAndCustomizationSurfacesDeclareContainmentBeforeMutation()
        {
            string bootloader = ReadScript("Core/Bootloader.cs");
            string customization = ReadScript(
                "ChampionMode/Customization/ChampionCustomizationController.cs");

            StringAssert.Contains(
                "ProfileMutationContainment",
                bootloader,
                "Pause and quit must consult the hard containment boundary.");
            StringAssert.Contains(
                "TryGetMutableSave",
                customization,
                "Customization writers must gate before retrieving mutable save state.");
            StringAssert.Contains(
                "GetPresentationSnapshot",
                customization,
                "Read-time normalization must operate on a detached presentation snapshot.");
        }

        [Test]
        public void ChampionMutationIsContainedAndPresentationNormalizationIsDetached()
        {
            FieldInfo servicesField = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(servicesField);
            var services = (IDictionary<Type, object>)servicesField.GetValue(null);
            bool hadPrior = services.TryGetValue(
                typeof(ISaveGameService),
                out object priorService);
            var backing = new SaveGameData
            {
                ChampionCustomization = new ChampionCustomizationState
                {
                    BodyPresetId = "backing_invalid_body",
                    HairStyleId = "backing_invalid_hair",
                    ArmorStyleId = "backing_invalid_armor",
                    FaceMarkId = "backing_invalid_face",
                    WeaponStyleId = "backing_invalid_weapon",
                    OffhandStyleId = "backing_invalid_offhand",
                    PrimaryR = 0.11f,
                    PrimaryG = 0.22f,
                    PrimaryB = 0.33f,
                    CapeEnabled = true,
                    HelmetEnabled = false
                }
            };
            var save = new MigrationRequiredTrackingSaveService(backing);
            GameObject owner = null;

            try
            {
                ServiceLocator.Register<ISaveGameService>(save);
                owner = new GameObject("ChampionContainmentBehaviorTest");
                var controller =
                    owner.AddComponent<ChampionCustomizationController>();
                string backingBefore = JsonUtility.ToJson(backing);

                MethodInfo mutableBoundary =
                    typeof(ChampionCustomizationController).GetMethod(
                        "TryGetMutableSave",
                        BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(mutableBoundary);
                object[] mutableArguments = { null, null };
                Assert.False(ProfileMutationContainment
                    .ProductionWriteActivationEnabled);
                Assert.False((bool)mutableBoundary.Invoke(
                    null,
                    mutableArguments));
                Assert.IsNull(mutableArguments[1]);

                controller.CycleBodyPreset();

                Assert.AreEqual(0, save.CurrentSaveReadCount);
                Assert.AreEqual(0, save.AuthorityReadCount);
                Assert.AreEqual(0, save.SaveCallCount);
                Assert.AreEqual(backingBefore, JsonUtility.ToJson(backing));

                MethodInfo presentationBoundary =
                    typeof(ChampionCustomizationController).GetMethod(
                        "GetPresentationSnapshot",
                        BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo normalize =
                    typeof(ChampionCustomizationController).GetMethod(
                        "NormalizeState",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(presentationBoundary);
                Assert.NotNull(normalize);
                var snapshot = (ChampionCustomizationState)
                    presentationBoundary.Invoke(null, null);
                Assert.NotNull(snapshot);
                Assert.AreNotSame(backing.ChampionCustomization, snapshot);
                Assert.AreEqual(1, save.CurrentSaveReadCount);

                snapshot.BodyPresetId = "detached_invalid_body";
                snapshot.HairStyleId = "detached_invalid_hair";
                snapshot.ArmorStyleId = "detached_invalid_armor";
                snapshot.FaceMarkId = "detached_invalid_face";
                snapshot.WeaponStyleId = "detached_invalid_weapon";
                snapshot.OffhandStyleId = "detached_invalid_offhand";
                snapshot.PrimaryR = 0.99f;
                snapshot.CapeEnabled = false;
                normalize.Invoke(controller, new object[] { snapshot });

                Assert.AreNotEqual(
                    "detached_invalid_body",
                    snapshot.BodyPresetId);
                Assert.AreNotEqual(
                    "detached_invalid_hair",
                    snapshot.HairStyleId);
                Assert.AreNotEqual(
                    "detached_invalid_armor",
                    snapshot.ArmorStyleId);
                Assert.AreEqual(backingBefore, JsonUtility.ToJson(backing));
                Assert.AreEqual(
                    "backing_invalid_body",
                    backing.ChampionCustomization.BodyPresetId);
                Assert.AreEqual(0.11f, backing.ChampionCustomization.PrimaryR);
                Assert.True(backing.ChampionCustomization.CapeEnabled);
                Assert.AreEqual(0, save.SaveCallCount);
                Assert.AreEqual(0, save.AuthorityReadCount);
            }
            finally
            {
                if (owner != null)
                {
                    UnityEngine.Object.DestroyImmediate(owner);
                }

                if (hadPrior)
                {
                    services[typeof(ISaveGameService)] = priorService;
                }
                else
                {
                    services.Remove(typeof(ISaveGameService));
                }
            }
        }

        private static string ReadScript(string relativePath)
        {
            string fullPath = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), fullPath);
            return File.ReadAllText(fullPath);
        }

        private sealed class ThrowingWritableSaveService :
            ISaveGameService,
            IProfileWriteAuthorityProvider
        {
            internal int CurrentSaveReadCount;
            internal int AuthorityReadCount;

            public SaveGameData CurrentSave
            {
                get
                {
                    CurrentSaveReadCount++;
                    throw new InvalidOperationException("CurrentSave must not be read.");
                }
            }

            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                AuthorityReadCount++;
                return ProfileWriteAuthoritySnapshotFactory.Writable(
                    "00000000-0000-0000-0000-000000000137",
                    new string('a', 32),
                    new string('b', 64),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());
            }

            public void Save() => throw new InvalidOperationException();
            public void Load() => throw new InvalidOperationException();
            public bool HasSave() => true;
            public void CreateNewSave(RealmId realmId) =>
                throw new InvalidOperationException();
            public void DeleteSave() => throw new InvalidOperationException();
        }

        private sealed class MigrationRequiredTrackingSaveService :
            ISaveGameService,
            IProfileWriteAuthorityProvider
        {
            private readonly SaveGameData _backing;

            internal MigrationRequiredTrackingSaveService(
                SaveGameData backing)
            {
                _backing = backing ?? throw new ArgumentNullException(
                    nameof(backing));
            }

            internal int CurrentSaveReadCount { get; private set; }
            internal int AuthorityReadCount { get; private set; }
            internal int SaveCallCount { get; private set; }

            public SaveGameData CurrentSave
            {
                get
                {
                    CurrentSaveReadCount++;
                    return _backing;
                }
            }

            public SaveLoadStatus LastLoadStatus =>
                SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus =>
                SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                AuthorityReadCount++;
                return ProfileWriteAuthoritySnapshotFactory
                    .MigrationRequired(
                        ProfileAuthoritySourceGeneration.Primary,
                        Array.Empty<string>());
            }

            public void Save()
            {
                SaveCallCount++;
            }

            public void Load() => throw new InvalidOperationException();
            public bool HasSave() => true;
            public void CreateNewSave(RealmId realmId) =>
                throw new InvalidOperationException();
            public void DeleteSave() =>
                throw new InvalidOperationException();
        }
    }
}
