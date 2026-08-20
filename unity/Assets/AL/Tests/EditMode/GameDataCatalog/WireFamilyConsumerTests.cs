using System.IO;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Skills;
using AL.Data.Catalogs;
using AL.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class WireFamilyConsumerTests
    {
        [Test]
        public void PackagedRealmSpecializedProjectsFourStableRealmsAndAliases()
        {
            var json = ReadPackaged("realm_specialized.v1.json");
            var result = RealmCatalogRuntime.Parse(json);
            Assert.True(result.IsSuccess, result.TechnicalCode);
            Assert.AreEqual("AL-REALM-CATALOG-READY", result.TechnicalCode);
            Assert.AreEqual(RealmCatalogRuntime.SupportedVersion, result.Snapshot.Version);
            Assert.AreEqual(4, result.Snapshot.Realms.Count);
            Assert.AreEqual("crownlands", result.Snapshot.Realms[0].Id);
            Assert.AreEqual("stonehold", result.Snapshot.Realms[1].Id);
            Assert.AreEqual("eldergrove", result.Snapshot.Realms[2].Id);
            Assert.AreEqual("umbral", result.Snapshot.Realms[3].Id);

            GameDataFamilyCatalogSnapshot family;
            string code;
            Assert.True(WireFamilyCatalogLoader.TryLoad("realm_specialized", json, out family, out code), code);
            GameDataCatalogRecord resolved;
            Assert.True(WireFamilyCatalogLoader.TryGetRecord(family, "Crownlands", out resolved));
            Assert.AreEqual("crownlands", resolved.Id);
            Assert.True(WireFamilyCatalogLoader.TryGetRecord(family, "realm.lock.warning", out resolved));
            Assert.AreEqual("realm_lock_warning", resolved.Id);
        }

        [Test]
        public void PackagedCharacterCustomizationProjectsLegacyConsumerIds()
        {
            var json = ReadPackaged("character_customization.v1.json");
            CharacterCustomizationCatalogData catalog;
            Assert.True(CharacterCustomizationCatalog.TryParse(json, out catalog));
            Assert.NotNull(catalog.bodyPresets);
            Assert.True(catalog.bodyPresets.Any(preset => preset.id == "average"));
            Assert.False(catalog.bodyPresets.Any(preset => preset.id == "body_preset_average"));
            Assert.True(catalog.hairStyles.Length > 0);
            Assert.True(catalog.armorStyles.Length > 0);

            GameDataFamilyCatalogSnapshot family;
            string code;
            Assert.True(WireFamilyCatalogLoader.TryLoad("character_customization", json, out family, out code), code);
            GameDataCatalogRecord resolved;
            Assert.True(WireFamilyCatalogLoader.TryGetRecord(family, "average", out resolved));
            Assert.AreEqual("body_preset_average", resolved.Id);
            Assert.True(WireFamilyCatalogLoader.TryGetRecord(family, "body_preset.average", out resolved));
            Assert.AreEqual("body_preset_average", resolved.Id);
        }

        [Test]
        public void PackagedSkillWeatherProjectsLoadoutsInSlotOrder()
        {
            var json = ReadPackaged("skill_weather.v1.json");
            SkillLoadoutData[] loadouts;
            Assert.True(SkillLoadoutCatalog.TryParse(json, out loadouts));
            Assert.AreEqual(4, loadouts.Length);
            Assert.AreEqual(0, loadouts[0].slot);
            Assert.AreEqual("realm_strike", loadouts[0].id);
            Assert.AreEqual("Realm Strike", loadouts[0].displayName);
            Assert.AreEqual(150f, loadouts[0].power);
            Assert.AreEqual(3, loadouts[3].slot);
            Assert.AreEqual("warmaster_breaker", loadouts[3].id);

            GameDataFamilyCatalogSnapshot family;
            string code;
            Assert.True(WireFamilyCatalogLoader.TryLoad("skill_weather", json, out family, out code), code);
            GameDataCatalogRecord resolved;
            Assert.True(WireFamilyCatalogLoader.TryGetRecord(family, "Realm Strike", out resolved));
            Assert.AreEqual("realm_strike", resolved.Id);
        }

        [Test]
        public void NestedLegacyShapesAndSkipFamiliesStayUnwired()
        {
            CharacterCustomizationCatalogData customization;
            SkillLoadoutData[] loadouts;
            Assert.False(CharacterCustomizationCatalog.TryParse(
                File.ReadAllText(PackagedPath("al_character_customization_catalog.json")),
                out customization));
            Assert.False(SkillLoadoutCatalog.TryParse(
                File.ReadAllText(PackagedPath("al_skill_weather_catalog.json")),
                out loadouts));
            Assert.False(RealmCatalogRuntime.Parse(
                File.ReadAllText(PackagedPath("al_realm_catalog.json"))).IsSuccess);

            GameDataFamilyCatalogSnapshot family;
            string code;
            Assert.False(WireFamilyCatalogLoader.TryLoad(
                "notification_content",
                File.ReadAllText(PackagedPath("al_notification_content_catalog.json")),
                out family,
                out code));
            Assert.AreEqual("AL-GDC-FAMILY-UNSUPPORTED", code);
            Assert.IsFalse(File.Exists(PackagedPath("catalog-set.json")));
            Assert.IsFalse(File.Exists(PackagedPath("realms.v1.json")));
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
