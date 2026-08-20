using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.Kingdom;
using NUnit.Framework;

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
                ISaveGameService writer = CreateSaveService(root);
                writer.CreateNewSave(RealmId.Stonehold);
                Assert.IsTrue(
                    MvpLoopSaveAuthority.TryCommit(
                        writer,
                        new MvpLoopCommitRequest(
                            Guid.NewGuid().ToString("N"),
                            RealmId.Stonehold,
                            ClassFamily.Warrior,
                            true,
                            "ch01_stonehold",
                            string.Empty,
                            0)).Persisted);

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

                KingdomOneBuildResult replay = KingdomOneBuildCommand.TryExecute(
                    reader,
                    gameData);
                Assert.IsTrue(replay.Accepted, replay.Message);
                Assert.IsFalse(replay.Persisted, replay.Message);
                Assert.AreEqual(1, MvpLoopSaveCodec.Read(reader.CurrentSave).LastBuildLevel);
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
    }
}
