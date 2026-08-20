using System.IO;
using System.Linq;
using AL.Core.Scenes;
using NUnit.Framework;
using UnityEditor;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// DemoInitializer is an explicit editor/dev harness, never a player-facing entry.
    /// Boot stays build index 0. Production-reachable scenes must not host its chrome.
    /// </summary>
    public sealed class DemoInitializerRetirementTests
    {
        private const string DemoInitializerGuid = "0f9c4845538d65c458fb24ff536dcc97";
        private const string BootScenePath = "Assets/AL/Scenes/Boot.unity";
        private const string HarnessScenePath = "Assets/Test.unity";

        private static readonly string[] ProductionScenePaths =
        {
            "Assets/AL/Scenes/Boot.unity",
            "Assets/AL/Scenes/RealmSelection.unity",
            "Assets/AL/Scenes/CharacterCreation.unity",
            "Assets/AL/Scenes/ChampionArena.unity",
            "Assets/AL/Scenes/Kingdom.unity"
        };

        [Test]
        public void BootIsBuildIndexZeroAndEnabled()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.IsNotEmpty(scenes, "EditorBuildSettings must list production scenes.");
            Assert.AreEqual(BootScenePath, scenes[0].path, "Boot must remain build index 0.");
            Assert.IsTrue(scenes[0].enabled, "Boot must stay enabled.");
            Assert.That(
                scenes.Select(scene => scene.path),
                Does.Not.Contain(HarnessScenePath),
                "Demo harness Test.unity must not be in Build Settings.");
        }

        [Test]
        public void BootRemainsTheProductionEntry()
        {
            Assert.IsTrue(
                ProductionSceneDescriptor.TryGetBySceneName("Boot", out ProductionSceneRecord boot));
            Assert.AreEqual(ProductionSceneDescriptor.RoleProductionEntry, boot.Role);
            Assert.AreEqual(ProductionSceneDescriptor.BootSceneId, boot.SceneId);
            Assert.AreNotEqual("DemoInitializer", boot.SceneName);
            Assert.AreNotEqual("Test", boot.SceneName);
        }

        [Test]
        public void ProductionScenesRejectDemoInitializer()
        {
            Assert.IsFalse(ProductionDebugChrome.AllowsDemoInitializer("Boot"));
            Assert.IsFalse(ProductionDebugChrome.AllowsDemoInitializer("RealmSelection"));
            Assert.IsFalse(ProductionDebugChrome.AllowsDemoInitializer("CharacterCreation"));
            Assert.IsFalse(ProductionDebugChrome.AllowsDemoInitializer("ChampionArena"));
            Assert.IsFalse(ProductionDebugChrome.AllowsDemoInitializer("Kingdom"));
            Assert.IsTrue(ProductionDebugChrome.AllowsDemoInitializer("Test"));
            Assert.IsTrue(ProductionDebugChrome.AllowsDemoInitializer("DemoHarness"));
        }

        [Test]
        public void ProductionSceneYamlDoesNotReferenceDemoInitializer()
        {
            foreach (string path in ProductionScenePaths)
            {
                Assert.IsTrue(File.Exists(path), "Missing production scene: " + path);
                string yaml = File.ReadAllText(path);
                Assert.That(
                    yaml,
                    Does.Not.Contain(DemoInitializerGuid),
                    path + " must not attach DemoInitializer.");
                Assert.That(
                    yaml,
                    Does.Not.Contain("DebugUI_Canvas"),
                    path + " must not bake DemoInitializer chrome.");
            }
        }

        [Test]
        public void DemoHarnessSceneKeepsExplicitDemoInitializer()
        {
            Assert.IsTrue(File.Exists(HarnessScenePath), "Editor/dev harness scene missing.");
            string yaml = File.ReadAllText(HarnessScenePath);
            Assert.That(
                yaml,
                Does.Contain(DemoInitializerGuid),
                "Test.unity remains the explicit DemoInitializer harness.");
            Assert.IsTrue(ProductionDebugChrome.AllowsDemoInitializer("Test"));
        }
    }
}
