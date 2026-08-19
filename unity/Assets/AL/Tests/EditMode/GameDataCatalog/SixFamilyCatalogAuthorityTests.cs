using System;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Data.Catalogs;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class SixFamilyCatalogAuthorityTests
    {
        [Test]
        public void PackagedSixFamilyCatalogLoadsAndProjectsLegacyAliases()
        {
            var root = ResolvePackagedRoot();
            var result = SixFamilyCatalogLoader.LoadFromDirectory(root);
            Assert.True(result.IsSuccess, SixFamilyCatalogLoader.FormatFailure(result));
            Assert.AreEqual(GameDataCatalogLoadStatus.LoadedPackaged, result.Status);
            Assert.AreEqual("six_family_catalog_set", result.Snapshot.CatalogSetId);
            CollectionAssert.AreEqual(
                GameDataSixFamilySchemas.FamilyOrder.ToArray(),
                result.Snapshot.Families.Select(family => family.Family).ToArray());

            var gameData = new LocalGameDataService(result.Snapshot);
            var realms = gameData.GetAllRealms().ToArray();
            Assert.AreEqual(4, realms.Length);
            Assert.NotNull(gameData.GetRealm(RealmId.Stonehold));
            Assert.NotNull(gameData.GetRealm(RealmId.Eldergrove));
            Assert.NotNull(gameData.GetRealm(RealmId.Crownlands));
            Assert.NotNull(gameData.GetRealm(RealmId.Umbral));

            var townHall = gameData.GetBuilding("TownHall");
            var townHallCanonical = gameData.GetBuilding("town_hall");
            Assert.NotNull(townHall);
            Assert.NotNull(townHallCanonical);
            Assert.AreEqual("TownHall", townHall.Id);
            Assert.AreEqual("town_hall", townHallCanonical.Id);
            Assert.AreEqual(10, townHall.MaxLevel);
            Assert.AreEqual(10, townHall.ConstructionLevels.Count);

            Assert.NotNull(gameData.GetTroop("Infantry"));
            Assert.NotNull(gameData.GetTroop("troop_infantry"));
            Assert.AreEqual(10, gameData.GetTroop("troop_infantry").BaseAttack);
            Assert.AreEqual(4, gameData.GetAllChampions().Count());
            Assert.NotNull(gameData.GetChampion("champion_stonehold_vanguard"));
            Assert.NotNull(gameData.GetChampion("Bronn Ironhide"));
            Assert.AreEqual(RealmId.Stonehold, gameData.GetChampion("champion_stonehold_vanguard").Realm);
            Assert.AreEqual(SubclassId.Vanguard, gameData.GetChampion("champion_stonehold_vanguard").Subclass);
            Assert.AreEqual(1250, gameData.GetChampion("champion_stonehold_vanguard").BaseStats.MaxHealth);
            Assert.NotNull(gameData.GetSkill("skill_realm_strike"));
            Assert.NotNull(gameData.GetSkill("Realm Strike"));
            Assert.AreEqual(150f, gameData.GetSkill("skill_realm_strike").Power);
            Assert.NotNull(gameData.GetSkill("skill_iron_bulwark"));
        }

        [Test]
        public void ParameterlessConstructorLoadsStreamingAssetsCatalog()
        {
            var gameData = new LocalGameDataService();
            Assert.AreEqual(4, gameData.GetAllRealms().Count());
            Assert.NotNull(gameData.GetBuilding("TownHall"));
            Assert.NotNull(gameData.GetBuilding("Watchtower"));
        }

        [Test]
        public void MissingCatalogSetProducesClearError()
        {
            var missingRoot = Path.Combine(Path.GetTempPath(), "al-six-family-missing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(missingRoot);
            try
            {
                var result = SixFamilyCatalogLoader.LoadFromDirectory(missingRoot);
                Assert.False(result.IsSuccess);
                Assert.AreEqual(GameDataCatalogLoadStatus.MissingManifest, result.Status);
                StringAssert.Contains("AL-GDC-MANIFEST-MISSING", SixFamilyCatalogLoader.FormatFailure(result));
            }
            finally
            {
                Directory.Delete(missingRoot, false);
            }
        }

        [Test]
        public void InvalidSourceModeIsRejectedWithClearDiagnostic()
        {
            var packaged = ResolvePackagedRoot();
            var scratch = Path.Combine(Path.GetTempPath(), "al-six-family-invalid-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            try
            {
                foreach (var name in Directory.GetFiles(packaged, "*.json"))
                {
                    File.Copy(name, Path.Combine(scratch, Path.GetFileName(name)), true);
                }

                var manifestPath = Path.Combine(scratch, SixFamilyCatalogLoader.ManifestFileName);
                var text = File.ReadAllText(manifestPath);
                File.WriteAllText(
                    manifestPath,
                    text.Replace("\"sourceMode\": \"generated\"", "\"sourceMode\": \"generated_migration\""));

                var result = SixFamilyCatalogLoader.LoadFromDirectory(scratch);
                Assert.False(result.IsSuccess);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code).ToArray(),
                    Does.Contain("AL-GDC-SOURCE-MODE"));
                StringAssert.Contains("AL-GDC-SOURCE-MODE", SixFamilyCatalogLoader.FormatFailure(result));
            }
            finally
            {
                Directory.Delete(scratch, true);
            }
        }

        private static string ResolvePackagedRoot()
        {
            var root = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                SixFamilyCatalogLoader.PackagedRelativeRoot);
            Assert.True(
                File.Exists(Path.Combine(root, SixFamilyCatalogLoader.ManifestFileName)),
                "Packaged six-family catalog-set.json must exist at " + root);
            return root;
        }
    }
}
