using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Data.Catalogs;
using AL.Data.Definitions;
using AL.Services.Local;
using AL.VerticalSlice;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class SixFamilyProductionCatalogTests
    {
        [Test]
        public void PackagedFamiliesValidateThroughSixFamilySchemas()
        {
            IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> families;
            Assert.True(
                SixFamilyCatalogLoader.TryLoadSet(
                    new[]
                    {
                        new KeyValuePair<string, string>("realms", ReadPackaged("realms.json")),
                        new KeyValuePair<string, string>("buildings", ReadPackaged("buildings.json")),
                        new KeyValuePair<string, string>("champions", ReadPackaged("champions.json")),
                        new KeyValuePair<string, string>("skills", ReadPackaged("skills.json"))
                    },
                    out families,
                    out var code),
                code);

            Assert.AreEqual(4, families["realms"].Records.Count);
            Assert.AreEqual("crownlands", families["realms"].Records[0].Id);
            Assert.AreEqual(15, families["buildings"].Records.Count);
            GameDataCatalogRecord farm;
            Assert.True(WireFamilyCatalogLoader.TryGetRecord(families["buildings"], "Farm", out farm));
            Assert.AreEqual("farm", farm.Id);
            Assert.AreEqual(4, families["champions"].Records.Count);
            GameDataCatalogRecord vanguard;
            Assert.True(WireFamilyCatalogLoader.TryGetRecord(families["champions"], "champion_stonehold_vanguard", out vanguard));
            Assert.AreEqual("champion_stonehold_vanguard", vanguard.Id);
            Assert.AreEqual(8, families["skills"].Records.Count);
        }

        [Test]
        public void LocalGameDataServiceReadsPackagedCatalogsAndResolvesLegacyBuildingIds()
        {
            var gameData = new LocalGameDataService();
            var realms = gameData.GetAllRealms().ToArray();
            Assert.AreEqual(4, realms.Length);
            Assert.NotNull(gameData.GetRealm(RealmId.Stonehold));
            Assert.AreEqual("Stonehold Dwarves", gameData.GetRealm(RealmId.Stonehold).RealmName);

            BuildingDefinition farm = gameData.GetBuilding("Farm");
            BuildingDefinition farmSnake = gameData.GetBuilding("farm");
            Assert.NotNull(farm);
            Assert.AreSame(farm, farmSnake);
            Assert.AreEqual(10, farm.MaxLevel);
            Assert.AreEqual(10, farm.ConstructionLevels.Count);

            ChampionDefinition[] champions = gameData.GetAllChampions().ToArray();
            Assert.AreEqual(4, champions.Length);
            ChampionDefinition vanguard = gameData.GetChampion("champion_stonehold_vanguard");
            Assert.NotNull(vanguard);
            Assert.AreEqual("Bronn Ironhide", vanguard.DisplayName);
            Assert.AreEqual(RealmId.Stonehold, vanguard.Realm);
            Assert.AreEqual(1250, vanguard.BaseStats.MaxHealth);
            Assert.AreEqual(55, vanguard.BaseStats.Attack);
        }

        [Test]
        public void MissingCatalogDirectoryFailsClosed()
        {
            SixFamilyRuntimeSnapshot snapshot;
            string code;
            Assert.False(
                SixFamilyRuntimeCatalog.TryLoadFromDirectory(
                    Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData_MISSING"),
                    out snapshot,
                    out code));
            Assert.IsNull(snapshot);
            Assert.That(code, Does.StartWith("AL-GDC-"));
        }

        [Test]
        public void CreateDefaultResolvesCatalogRecord()
        {
            SliceChampionProfile profile = SliceChampionProfile.CreateDefault();
            Assert.AreEqual("champion_stonehold_vanguard", profile.Id);
            Assert.AreEqual("Bronn Ironhide", profile.DisplayName);
            Assert.AreEqual(1250, profile.MaxHealth);
            Assert.AreEqual(55, profile.AttackPower);
            Assert.AreEqual(90, profile.SpecialPower);
        }

        [Test]
        public void HardcodedArchetypeBlockIsGone()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Services",
                "Local",
                "LocalGameDataService.cs"));
            Assert.That(source, Does.Not.Contain("InitializeChampionArchetypes"));
            Assert.That(source, Does.Not.Contain("CreateFallbackRealm"));
            Assert.That(source, Does.Not.Contain("new ChampionBaseStats"));
        }

        private static string ReadPackaged(string fileName)
        {
            return File.ReadAllText(PackagedPath(fileName));
        }

        private static string PackagedPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                fileName));
        }
    }
}
