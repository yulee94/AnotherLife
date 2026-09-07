using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.Kingdom;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed class KingdomOneBuildPersistenceTests
    {
        [Test]
        public void OneBuildUsesCatalogTownHallAndDoesNotUnlockDeferredCommands()
        {
            Assert.AreEqual("TownHall", KingdomOneBuildCommand.BuildingId);
            Assert.AreEqual("town_hall", KingdomOneBuildCommand.CatalogBuildingId);
            Assert.AreEqual(
                KingdomOneBuildCommand.BuildingId,
                MvpLoopSaveCodec.DefaultOneBuildId);
            Assert.AreEqual(
                KingdomCommandPolicy.TownHallUpgrade,
                "building.town_hall.upgrade");

            var gameData = new LocalGameDataService();
            Assert.NotNull(
                gameData.GetBuilding(KingdomOneBuildCommand.BuildingId),
                "Live construction id TownHall must resolve from the packaged catalog.");

            object context = CreateContext(
                hasCommittedRealm: true,
                capabilities: CreateCapabilities());
            object townHall = Resolve(KingdomCommandPolicy.TownHallUpgrade, context);
            object duel = Resolve(KingdomCommandPolicy.GreyboxDuel, context);
            Assert.IsTrue(IsInteractable(townHall));
            Assert.IsTrue(IsInteractable(duel));

            foreach (string commandId in new[]
                     {
                         KingdomCommandPolicy.FarmUpgrade,
                         KingdomCommandPolicy.ChampionDeploy,
                         KingdomCommandPolicy.BorderlandsCapture,
                         KingdomCommandPolicy.WarmasterPurchase,
                         KingdomCommandPolicy.ArmorResearch
                     })
            {
                Assert.IsFalse(
                    IsInteractable(Resolve(commandId, context)),
                    commandId);
            }
        }

        [Test]
        public void TownHallCommandPersistsThroughISaveGameServiceAndReloads()
        {
            string root = NewRoot();
            try
            {
                ISaveGameService writer =
                    ProfileBoundKingdomTestFixture.CreateLordshipSave(
                        root,
                        RealmId.Stonehold,
                        ClassFamily.Warrior);

                var gameData = new LocalGameDataService();
                KingdomOneBuildResult constructed = KingdomOneBuildCommand.TryExecute(
                    writer,
                    gameData);
                Assert.IsTrue(constructed.Accepted, constructed.Message);
                Assert.IsTrue(constructed.Persisted, constructed.Message);
                Assert.AreEqual("TownHall", constructed.BuildingId);
                Assert.AreEqual("town_hall", constructed.CatalogBuildingId);
                Assert.AreEqual(1, constructed.Level);

                ISaveGameService reader = CreateSaveService(root);
                reader.Load();
                MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(reader.CurrentSave);
                Assert.AreEqual("TownHall", snapshot.LastBuildId);
                Assert.AreEqual(1, snapshot.LastBuildLevel);
                Assert.AreEqual(RealmId.Stonehold, snapshot.Realm);
                Assert.AreEqual(ClassFamily.Warrior, snapshot.ClassFamily);
                Assert.AreEqual("ch01_stonehold", snapshot.LastResultId);
                Assert.IsTrue(
                    reader.CurrentSave.Buildings.Any(building =>
                        building != null &&
                        building.BuildingId == "TownHall" &&
                        building.Level == 1));
                byte[] afterConstruct = File.ReadAllBytes(
                    Path.Combine(root, "save.json"));

                KingdomOneBuildResult replay = KingdomOneBuildCommand.TryExecute(
                    reader,
                    gameData);
                Assert.IsTrue(replay.Accepted, replay.Message);
                Assert.IsFalse(replay.Persisted, replay.Message);
                Assert.AreEqual(1, MvpLoopSaveCodec.Read(reader.CurrentSave).LastBuildLevel);
                CollectionAssert.AreEqual(
                    afterConstruct,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoTownHallRequiresEarnedLordshipWithoutChangingDisk()
        {
            string root = NewRoot();
            try
            {
                LocalSaveGameService save =
                    ProfileBoundKingdomTestFixture.CreateIdentitySave(
                        root,
                        RealmId.Stonehold,
                        ClassFamily.Warrior);
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));

                KingdomOneBuildResult rejected = KingdomOneBuildCommand.TryExecute(
                    save,
                    new LocalGameDataService());

                Assert.That(rejected.Accepted, Is.False, rejected.Message);
                Assert.That(rejected.Persisted, Is.False, rejected.Message);
                Assert.That(
                    MvpLoopSaveCodec.Read(save.CurrentSave).LastBuildId,
                    Is.EqualTo(string.Empty));
                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ProfileBoundTownHallRootRejectsDifferentRealmWithoutChangingDisk()
        {
            string root = NewRoot();
            try
            {
                LocalSaveGameService save =
                    ProfileBoundKingdomTestFixture.CreateLordshipSave(
                        root,
                        RealmId.Stonehold,
                        ClassFamily.Warrior);
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));

                SaveCandidateCommitResult rejected =
                    ((IProfileBoundKingdomOneBuildCandidateStore)save)
                    .TryCommitProfileBoundKingdomOneBuild(
                        new KingdomOneBuildCommitRequest(
                            Guid.NewGuid().ToString("N"),
                            RealmId.Crownlands));

                Assert.That(
                    rejected.Outcome,
                    Is.Not.EqualTo(SaveCandidateCommitOutcome.Committed)
                        .And.Not.EqualTo(SaveCandidateCommitOutcome.Duplicate));
                Assert.That(
                    MvpLoopSaveCodec.Read(save.CurrentSave).LastBuildId,
                    Is.EqualTo(string.Empty));
                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoReplayStillConsultsProfileBoundAuthority()
        {
            var save = new TrackingProfileBoundKingdomSaveService();

            KingdomOneBuildResult replay = KingdomOneBuildCommand.TryExecute(
                save,
                new LocalGameDataService());

            Assert.That(replay.Accepted, Is.True, replay.Message);
            Assert.That(replay.Persisted, Is.False, replay.Message);
            Assert.That(replay.Level, Is.EqualTo(1));
            Assert.That(save.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void SchemaTwoTownHallLevelTwoIsRejectedWithoutChangingDisk()
        {
            string root = NewRoot();
            try
            {
                LocalSaveGameService save =
                    ProfileBoundKingdomTestFixture.CreateLordshipSave(
                        root,
                        RealmId.Stonehold,
                        ClassFamily.Warrior);
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));
                save.CurrentSave.Buildings.Add(new BuildingState
                {
                    BuildingId = KingdomOneBuildCommand.BuildingId,
                    Level = 2
                });
                SaveGameData candidate = JsonUtility.FromJson<SaveGameData>(
                    JsonUtility.ToJson(save.CurrentSave));
                Assert.That(
                    KingdomOneBuildSaveCodec.PrepareCandidate(
                        candidate,
                        new KingdomOneBuildCommitRequest(
                            Guid.NewGuid().ToString("N"),
                            RealmId.Stonehold),
                        out string prepareMessage),
                    Is.EqualTo(KingdomOneBuildPrepareDisposition.Rejected),
                    prepareMessage);
                LogAssert.Expect(
                    LogType.Error,
                    "AL-SAVE-TYPED-PRIMARY-GENERATION-CONFLICT: The published legacy " +
                    "save could not be pinned to an exact primary generation.");

                KingdomOneBuildResult rejected = KingdomOneBuildCommand.TryExecute(
                    save,
                    new LocalGameDataService());

                Assert.That(rejected.Accepted, Is.False, rejected.Message);
                Assert.That(rejected.Persisted, Is.False, rejected.Message);
                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static object CreateCapabilities()
        {
            return Activator.CreateInstance(
                RequiredType("AL.UI.Kingdom.KingdomCommandCapabilities"),
                false,
                false,
                false,
                false,
                false,
                false,
                false);
        }

        private static object CreateContext(bool hasCommittedRealm, object capabilities)
        {
            return Activator.CreateInstance(
                RequiredType("AL.UI.Kingdom.KingdomCommandContext"),
                hasCommittedRealm,
                capabilities);
        }

        private static object Resolve(string id, object context)
        {
            return RequiredType("AL.UI.Kingdom.KingdomCommandPolicy")
                .GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { id, context });
        }

        private static Type RequiredType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, typeName);
            return type;
        }

        private static bool IsInteractable(object descriptor)
        {
            return (bool)descriptor.GetType()
                .GetProperty("IsInteractable", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(descriptor);
        }

        private static ISaveGameService CreateSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor, "Expected the testable persistence-path constructor.");
            return (ISaveGameService)constructor.Invoke(new object[] { root });
        }

        private static string NewRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-KingdomOneBuildTests",
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

        private sealed class TrackingProfileBoundKingdomSaveService :
            ISaveGameService,
            IProfileBoundKingdomOneBuildCandidateStore
        {
            internal TrackingProfileBoundKingdomSaveService()
            {
                CurrentSave = new SaveGameData
                {
                    SaveFormatId = SaveGameData.CurrentSaveFormatId,
                    SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
                    ProfileInitializationVersion =
                        SaveGameData.CurrentProfileInitializationVersion,
                    ProfileId = "11111111-1111-4111-8111-111111111111",
                    SelectedRealm = RealmId.Stonehold,
                    ChampionCustomization = new ChampionCustomizationState
                    {
                        ClassFamilyId = "warrior",
                        IdentityConfirmed = true,
                        LastResultId = "ch01_stonehold",
                        Username = "KingdomTester"
                    },
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            BuildingId = KingdomOneBuildCommand.BuildingId,
                            Level = KingdomOneBuildCommand.CompletedLevel
                        }
                    }
                };
            }

            public SaveGameData CurrentSave { get; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;
            internal int CommitCount { get; private set; }

            SaveCandidateCommitResult
                IProfileBoundKingdomOneBuildCandidateStore
                    .TryCommitProfileBoundKingdomOneBuild(
                        KingdomOneBuildCommitRequest request)
            {
                CommitCount++;
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Duplicate,
                    CurrentSave,
                    string.Empty);
            }

            public void Save()
            {
            }

            public void Load()
            {
            }

            public bool HasSave() => true;

            public void CreateNewSave(RealmId realmId)
            {
            }

            public void DeleteSave()
            {
            }
        }
    }
}
