using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Inventory contract for the production scene descriptor (#223 "Required tests" inventory family):
    /// five production scenes plus the representative Test scene, exact path/name/id/role/GUID,
    /// duplicate detection, and Test excluded from production build applicability.
    /// </summary>
    public sealed class ProductionSceneInventoryTests
    {
        private static readonly string[] ExpectedProductionIds =
        {
            "al_scene_boot",
            "al_scene_realm_selection",
            "al_scene_character_creation",
            "al_scene_kingdom",
            "al_scene_champion_arena"
        };

        [Test]
        public void DescriptorContainsFiveProductionScenesPlusTest()
        {
            IReadOnlyList<object> all = R.DescriptorAll();
            IReadOnlyList<object> production = R.DescriptorProduction();

            Assert.AreEqual(6, all.Count, "Descriptor must contain five production scenes plus the representative Test scene.");
            Assert.AreEqual(5, production.Count, "Descriptor must expose exactly five production scenes.");

            var productionIds = production.Select(r => R.PropString(r, "SceneId")).ToArray();
            Assert.That(productionIds, Is.EquivalentTo(ExpectedProductionIds));
        }

        [Test]
        public void ExactPathsNamesRolesForEachProductionScene()
        {
            AssertRecord("al_scene_boot", "Assets/AL/Scenes/Boot.unity", "Boot", "production_entry", "AL.UI.BootController");
            AssertRecord("al_scene_realm_selection", "Assets/AL/Scenes/RealmSelection.unity", "RealmSelection", "onboarding_selection", "AL.UI.RealmSelection.RealmSelectionController");
            AssertRecord("al_scene_character_creation", "Assets/AL/Scenes/CharacterCreation.unity", "CharacterCreation", "onboarding_creation", "AL.UI.CharacterCreation.CharacterCreationController");
            AssertRecord("al_scene_kingdom", "Assets/AL/Scenes/Kingdom.unity", "Kingdom", "production_hub", "AL.UI.Kingdom.KingdomSceneController");
            AssertRecord("al_scene_champion_arena", "Assets/AL/Scenes/ChampionArena.unity", "ChampionArena", "first_session_gameplay", "AL.ChampionMode.ChampionArenaSceneController");
        }

        [Test]
        public void SceneNamesMatchFileNamesExactCase()
        {
            foreach (object record in R.DescriptorAll())
            {
                string path = R.PropString(record, "AssetPath");
                string name = R.PropString(record, "SceneName");
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                Assert.AreEqual(name, fileName, $"Scene name must equal file name exactly for {R.PropString(record, "SceneId")}.");
            }
        }

        [Test]
        public void IdsPathsNamesAreUnique()
        {
            IReadOnlyList<object> all = R.DescriptorAll();
            AssertUnique(all.Select(r => R.PropString(r, "SceneId")), "scene id");
            AssertUnique(all.Select(r => R.PropString(r, "AssetPath")), "asset path");
            AssertUnique(all.Select(r => R.PropString(r, "SceneName")), "scene name");
            AssertUnique(all.Select(r => R.PropString(r, "AssetGuid")), "asset guid");
        }

        [Test]
        public void AssetGuidsAreThirtyTwoHexCharacters()
        {
            foreach (object record in R.DescriptorAll())
            {
                string guid = R.PropString(record, "AssetGuid");
                Assert.AreEqual(32, guid.Length, $"GUID for {R.PropString(record, "SceneId")} must be 32 hex chars.");
                Assert.IsTrue(guid.All(c => "0123456789abcdef".IndexOf(c) >= 0), $"GUID for {R.PropString(record, "SceneId")} must be lowercase hex.");
            }
        }

        [Test]
        public void TestSceneIsTestOnlyAndExcludedFromProduction()
        {
            object test = R.RecordById("al_scene_test_representative");
            Assert.NotNull(test, "Representative Test record must exist in inventory.");
            Assert.AreEqual("Assets/Test.unity", R.PropString(test, "AssetPath"));
            Assert.AreEqual("Test", R.PropString(test, "SceneName"));
            Assert.AreEqual("representative_test_only", R.PropString(test, "Role"));
            Assert.IsFalse(R.PropBool(test, "IsProductionScene"), "Test scene must not be a production scene.");
            Assert.IsFalse(R.PropBool(test, "HasStartupMarker"), "Test scene must not carry a startup marker.");
            Assert.IsEmpty(R.AsStrings(R.Prop(test, "BuildProfiles")), "Test scene must have no build profile.");

            Assert.That(R.DescriptorProduction().Select(r => R.PropString(r, "SceneId")),
                Has.None.EqualTo("al_scene_test_representative"));
        }

        [Test]
        public void ShellFoundationIsBootRealmCreateArenaKingdomInOrder()
        {
            var shell = R.DescriptorShellFoundation().Select(r => R.PropString(r, "SceneId")).ToArray();
            Assert.AreEqual(new[]
            {
                "al_scene_boot",
                "al_scene_realm_selection",
                "al_scene_character_creation",
                "al_scene_champion_arena",
                "al_scene_kingdom"
            }, shell);

            object champion = R.RecordById("al_scene_champion_arena");
            Assert.That(R.AsStrings(R.Prop(champion, "BuildProfiles")), Does.Contain("ShellFoundation"));
            Assert.AreEqual("committed_active", R.PropString(champion, "Status"));
            object create = R.RecordById("al_scene_character_creation");
            Assert.That(R.AsStrings(R.Prop(create, "BuildProfiles")), Does.Contain("ShellFoundation"));
            Assert.AreEqual("committed_active", R.PropString(create, "Status"));
        }

        [Test]
        public void EveryProductionSceneHasStartupMarkerIdEqualToSceneId()
        {
            foreach (object record in R.DescriptorProduction())
            {
                Assert.IsTrue(R.PropBool(record, "HasStartupMarker"));
                Assert.AreEqual(R.PropString(record, "SceneId"), R.PropString(record, "StartupMarkerId"));
            }
        }

        [Test]
        public void ValidatorInventoryDerivesOneToOneFromDescriptorProductionRecords()
        {
            var descriptorIds = R.DescriptorProduction()
                .Select(r => R.PropString(r, "SceneId"))
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .ToArray();

            object report = R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");
            var validatorIds = R.AsObjects(R.Prop(report, "Scenes"))
                .Select(s => R.PropString(s, "SceneId"))
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(descriptorIds, validatorIds,
                "Validator inventory must derive 1:1 from descriptor production records (no hard-coded list drift).");
            Assert.IsEmpty(R.AsStrings(R.Prop(report, "InventoryFailures")));
        }

        private static void AssertRecord(string sceneId, string path, string name, string role, string controller)
        {
            object record = R.RecordById(sceneId);
            Assert.NotNull(record, $"Missing descriptor record {sceneId}.");
            Assert.AreEqual(path, R.PropString(record, "AssetPath"), $"{sceneId} path");
            Assert.AreEqual(name, R.PropString(record, "SceneName"), $"{sceneId} name");
            Assert.AreEqual(role, R.PropString(record, "Role"), $"{sceneId} role");
            Assert.AreEqual(controller, R.PropString(record, "RequiredControllerType"), $"{sceneId} controller");
            Assert.IsTrue(R.PropBool(record, "IsProductionScene"), $"{sceneId} must be a production scene");
        }

        private static void AssertUnique(IEnumerable<string> values, string label)
        {
            var list = values.ToList();
            var duplicates = list.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(duplicates, $"Duplicate {label}(s): {string.Join(", ", duplicates)}");
        }
    }
}
