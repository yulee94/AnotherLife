using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.UI;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed class BootLoadPreSchemaMigrationTests
    {
        private const string CurrentSaveFormatId = "anotherlife.local-save";
        private const string PreSchemaKingdomJson =
            "{" +
            "\"SelectedRealm\":3," +
            "\"Resources\":[{\"Type\":0,\"Amount\":1033546}," +
            "{\"Type\":1,\"Amount\":517898}," +
            "{\"Type\":2,\"Amount\":259349}," +
            "{\"Type\":3,\"Amount\":130473}]," +
            "\"Buildings\":[],\"Troops\":[],\"Researches\":[]," +
            "\"Quests\":[{\"QuestId\":\"Q1\",\"CurrentValue\":0," +
            "\"IsCompleted\":false,\"IsClaimed\":false}]," +
            "\"Reputation\":[],\"FactionReputations\":[]," +
            "\"LordPersona\":{\"Warlord\":0,\"Diplomat\":0,\"Sage\":0,\"Rogue\":0}," +
            "\"Territories\":[],\"RealmGems\":[]," +
            "\"Wishgate\":{\"IsEarned\":false,\"EarnReason\":\"\"," +
            "\"LastRewardId\":\"\",\"LastRewardChosenTimestamp\":0}," +
            "\"CurrentChapterId\":\"C1\"," +
            "\"Warmaster\":{\"EquippedSetId\":\"\",\"UnlockedSetIds\":[]," +
            "\"PurchasedPieceIds\":[],\"IsTrueWarmaster\":false," +
            "\"Level\":0,\"Experience\":0}," +
            "\"ChampionCustomization\":{\"BodyPresetId\":\"average\"," +
            "\"HairStyleId\":\"short\",\"ArmorStyleId\":\"realm_basic\"," +
            "\"FaceMarkId\":\"none\",\"WeaponStyleId\":\"sword\"," +
            "\"OffhandStyleId\":\"shield\",\"PrimaryR\":0.2,\"PrimaryG\":0.4," +
            "\"PrimaryB\":1.0,\"HairR\":0.08,\"HairG\":0.06,\"HairB\":0.04," +
            "\"CapeEnabled\":true,\"HelmetEnabled\":false}," +
            "\"WarzoneCredits\":4300,\"LastSavedTimestamp\":1784868853}";

        [Test]
        public void PreSchemaPrimaryMigratesAndPassesCurrentSaveSchemaGate()
        {
            string root = NewTempRoot();
            try
            {
                string primaryPath = Path.Combine(root, "save.json");
                File.WriteAllText(primaryPath, PreSchemaKingdomJson);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual(
                    "LoadedPrimary",
                    GetProperty(service, "LastLoadStatus").ToString());
                object currentSave = GetProperty(service, "CurrentSave");
                Assert.NotNull(currentSave);
                Assert.AreEqual(CurrentSaveFormatId, GetField(currentSave, "SaveFormatId"));
                Assert.AreEqual(1, GetField(currentSave, "SaveSchemaVersion"));
                Assert.AreEqual(1, GetField(currentSave, "ProfileInitializationVersion"));
                Assert.AreEqual("Crownlands", GetField(currentSave, "SelectedRealm").ToString());
                Assert.AreEqual(4300, GetField(currentSave, "WarzoneCredits"));

                IList resources = (IList)GetField(currentSave, "Resources");
                object food = resources.Cast<object>().Single(row =>
                    GetField(row, "Type").ToString() == "Food");
                Assert.AreEqual(
                    1033546L,
                    GetField(food, "Amount"),
                    "Existing kingdom resources must survive migration.");

                Assert.True(
                    InvokeIsCurrentSaveSchema(currentSave),
                    "BootLoadReadinessProbe.IsCurrentSaveSchema must accept the migrated save.");

                LaunchBootLoadEvidence evidence = BuildEvidence(service, currentSave);
                Assert.True(
                    evidence.IsWellFormed,
                    "Migrated metadata must be publishable as boot-load evidence.");

                var coordinator = new LaunchReadinessCoordinator();
                Assert.True(coordinator.TryPublishBootLoad(evidence));
                Assert.AreEqual(
                    LaunchReadinessState.WaitingForRequiredCatalogs,
                    coordinator.Snapshot.State);

                Assert.AreEqual(
                    "Ready",
                    ProbeBootLoad(service),
                    "A migrated current-schema primary must unblock boot.");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void IsCurrentSaveSchemaRejectsUnstampedPreSchemaSave()
        {
            var save = new SaveGameData
            {
                SelectedRealm = RealmId.Crownlands,
                WarzoneCredits = 4300,
                CurrentChapterId = "C1"
            };

            Assert.False(
                InvokeIsCurrentSaveSchema(save),
                "The original deadlock state — loaded kingdom data with empty schema — must stay rejected.");
            Assert.False(InvokeIsCurrentSaveSchema(null));
        }

        [Test]
        public void UnmigratablePreSchemaDoesNotReturnLoadedPrimaryAndUnblocksBoot()
        {
            string root = NewTempRoot();
            var fileSystem = new SavePersistenceRegressionTests.ScriptedSaveFileOperations();
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.Files[primaryPath] = PreSchemaKingdomJson;
            fileSystem.DurableWriteObserver = (path, _) =>
            {
                if (string.Equals(path, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    fileSystem.GetDurableWriteCount(path) == 1)
                {
                    fileSystem.WriteFailuresBeforeMutation.Add(path);
                }
                else
                {
                    fileSystem.WriteFailuresBeforeMutation.Remove(path);
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            string status = GetProperty(service, "LastLoadStatus").ToString();
            Assert.That(
                status,
                Is.Not.StartsWith("LoadedPrimary"),
                "An unmigratable pre-schema primary must never be reported as a loaded primary.");
            Assert.AreEqual("CreatedNewAfterUnrecoverableCorruption", status);

            object currentSave = GetProperty(service, "CurrentSave");
            Assert.NotNull(currentSave);
            Assert.True(
                InvokeIsCurrentSaveSchema(currentSave),
                "The replacement profile must pass BootLoadReadinessProbe.IsCurrentSaveSchema.");
            Assert.AreEqual(
                "Ready",
                ProbeBootLoad(service),
                "Replacement after unmigratable primary must unblock boot instead of deadlocking.");
        }

        [Test]
        public void MalformedPrimaryDoesNotReturnLoadedPrimaryOrDeadlockBoot()
        {
            string root = NewTempRoot();
            try
            {
                File.WriteAllText(Path.Combine(root, "save.json"), "{ this is not valid save json");

                object service = CreateSaveService(root);
                InvokeAllowingFailureLogs(service, "Load");

                string status = GetProperty(service, "LastLoadStatus").ToString();
                Assert.That(
                    status,
                    Is.Not.StartsWith("LoadedPrimary"),
                    "Malformed bytes must not be treated as a loaded primary.");

                object currentSave = GetProperty(service, "CurrentSave");
                if (currentSave != null)
                {
                    Assert.True(
                        InvokeIsCurrentSaveSchema(currentSave),
                        "Any published replacement after malformed bytes must already be current-schema.");
                    Assert.AreEqual("Ready", ProbeBootLoad(service));
                }
                else
                {
                    Assert.AreEqual(
                        "Unavailable",
                        ProbeBootLoad(service),
                        "A failed malformed load must surface Unavailable, not hang Pending.");
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static string ProbeBootLoad(object service)
        {
            ISaveGameService saveService = (ISaveGameService)service;
            IDictionary locator = GetLocatorServices();
            object previousSave = locator.Contains(typeof(ISaveGameService))
                ? locator[typeof(ISaveGameService)]
                : null;
            Type markerInterface = GetRuntimeType("AL.Core.IOfflineServiceStackMarker");
            object previousMarker = locator.Contains(markerInterface)
                ? locator[markerInterface]
                : null;

            try
            {
                object marker = CreateSucceededStackMarker(saveService);
                ServiceLocator.Register(saveService);
                typeof(ServiceLocator)
                    .GetMethod("Register")
                    .MakeGenericMethod(markerInterface)
                    .Invoke(null, new[] { marker });

                Type probeType = GetRuntimeType("AL.UI.BootLoadReadinessProbe");
                MethodInfo tryCapture = probeType.GetMethod(
                    "TryCapture",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(tryCapture);
                object[] args = { 1, null };
                object status = tryCapture.Invoke(null, args);
                return status.ToString();
            }
            finally
            {
                RestoreLocator(locator, typeof(ISaveGameService), previousSave);
                RestoreLocator(locator, markerInterface, previousMarker);
            }
        }

        private static object CreateSucceededStackMarker(ISaveGameService saveService)
        {
            Type markerType = GetRuntimeType("AL.Core.LocalOfflineServiceStackMarker");
            var expected = new Dictionary<Type, object>
            {
                { typeof(ISaveGameService), saveService }
            };
            ConstructorInfo constructor = markerType.GetConstructor(
                new[]
                {
                    typeof(int),
                    typeof(string),
                    typeof(IReadOnlyDictionary<Type, object>),
                    typeof(object),
                    typeof(object)
                });
            Assert.NotNull(constructor);
            object marker = constructor.Invoke(
                new object[] { 1, "boot-gate-test", expected, saveService, null });
            Invoke(marker, "TryClaimRuntimeOwner", "boot-test");
            Invoke(marker, "TryBeginLoad", "boot-test");
            Invoke(marker, "MarkLoadSucceeded", "boot-test");
            return marker;
        }

        private static bool InvokeIsCurrentSaveSchema(object save)
        {
            Type probeType = GetRuntimeType("AL.UI.BootLoadReadinessProbe");
            MethodInfo method = probeType.GetMethod(
                "IsCurrentSaveSchema",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method, "Expected BootLoadReadinessProbe.IsCurrentSaveSchema.");
            return (bool)method.Invoke(null, new[] { save });
        }

        private static LaunchBootLoadEvidence BuildEvidence(object service, object currentSave)
        {
            return new LaunchBootLoadEvidence(
                1,
                "boot-gate-test",
                1,
                (SaveLoadStatus)GetProperty(service, "LastLoadStatus"),
                (int)GetField(currentSave, "SaveSchemaVersion"),
                (int)GetField(currentSave, "ProfileInitializationVersion"));
        }

        private static string NewTempRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-BootGateTests",
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

        private static object CreateSaveService(string root)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor, "Expected the testable persistence-path constructor.");
            return constructor.Invoke(new object[] { root });
        }

        private static object CreateSaveService(string root, object fileOperations)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            Type fileOperationsType = GetRuntimeType("AL.Services.Local.ISaveFileOperations");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), fileOperationsType },
                null);
            Assert.NotNull(constructor, "Expected the testable file-operations constructor.");
            return constructor.Invoke(new[] { root, fileOperations });
        }

        private static object CreateFileOperationsProxy(
            SavePersistenceRegressionTests.ScriptedSaveFileOperations fileSystem)
        {
            Type interfaceType = GetRuntimeType("AL.Services.Local.ISaveFileOperations");
            MethodInfo createMethod = typeof(System.Reflection.DispatchProxy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Create" && method.GetGenericArguments().Length == 2);
            object proxy = createMethod
                .MakeGenericMethod(
                    interfaceType,
                    typeof(SavePersistenceRegressionTests.ScriptedSaveFileOperationsProxy))
                .Invoke(null, null);
            ((SavePersistenceRegressionTests.ScriptedSaveFileOperationsProxy)proxy).FileSystem =
                fileSystem;
            return proxy;
        }

        private static object InvokeAllowingFailureLogs(
            object target,
            string methodName,
            params object[] args)
        {
            bool priorIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                return Invoke(target, methodName, args);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = priorIgnore;
            }
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
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
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            return field.GetValue(target);
        }

        private static IDictionary GetLocatorServices()
        {
            FieldInfo field = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (IDictionary)field.GetValue(null);
        }

        private static void RestoreLocator(IDictionary locator, Type key, object previous)
        {
            if (previous != null)
            {
                locator[key] = previous;
                return;
            }

            if (locator.Contains(key))
            {
                locator.Remove(key);
            }
        }
    }
}
