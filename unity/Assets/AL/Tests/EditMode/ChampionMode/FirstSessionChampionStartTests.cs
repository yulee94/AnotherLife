using AL.ChampionMode;
using AL.Core.Scenes;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class FirstSessionChampionStartTests
    {
        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
        }

        [Test]
        public void DefaultLandingIsFirstSessionNotEncounterHarness()
        {
            Assert.IsTrue(FirstSessionChampionStart.IsFirstSessionLanding);
            Assert.IsFalse(FirstSessionChampionStart.IsEncounterHarness);
            Assert.IsFalse(FirstSessionChampionStart.AllowDebugKingdomLoad);
            Assert.IsFalse(FirstSessionChampionStart.ShowAppearanceRack);
            Assert.IsFalse(FirstSessionChampionStart.AutoStartEncounterIntro);
            Assert.IsTrue(FirstSessionChampionStart.ShouldRunFirstWorldEntryTutorial);
            Assert.IsFalse(FirstSessionChampionStart.AutoStartFirstFight);
            Assert.IsTrue(FirstSessionChampionStart.ShouldRunProofOfWorth);
        }

        [Test]
        public void FirstSessionSuppressesCrowdAndKeepsHudNames()
        {
            Assert.AreEqual(0, FirstSessionChampionStart.ResolveDummyBudget(16));
            Assert.AreEqual(0, FirstSessionChampionStart.ResolveBotBudget(40));
            Assert.AreEqual("ChampionArena", FirstSessionChampionStart.DestinationSceneName);
            Assert.AreEqual("PlayerFrame", FirstSessionChampionStart.PlayerFrameName);
            Assert.AreEqual("CombatHotbar", FirstSessionChampionStart.HotbarName);
            Assert.AreEqual("BossTargetLock", FirstSessionChampionStart.TargetLockName);
            Assert.AreEqual("SharedMenuButton", FirstSessionChampionStart.SharedMenuButtonName);
            Assert.AreEqual("QuestHudSlot", FirstSessionChampionStart.QuestHudSlotName);
        }

        [Test]
        public void EnvironmentRootAndPlaqueAreLabelledTemporary()
        {
            Assert.That(FirstSessionChampionStart.EnvironmentRootName, Does.Contain("TEMPORARY"));
            Assert.That(FirstSessionChampionStart.EnvironmentRootName, Is.EqualTo("InnerRealmWorld_TEMPORARY"));
            Assert.That(FirstSessionChampionStart.TemporaryPlaqueCopy, Does.Contain("TEMPORARY"));
            Assert.That(FirstSessionChampionStart.TemporaryPlaqueCopy, Does.Contain("Capital"));
            Assert.That(FirstSessionChampionStart.AtmosphereName, Does.Contain("TEMPORARY"));
            Assert.AreEqual("Arena_TEMPORARY", FirstSessionChampionStart.LabelTemporary("Arena"));
            Assert.AreEqual("Wall_TEMPORARY", FirstSessionChampionStart.LabelTemporary("Wall_TEMPORARY"));
        }

        [Test]
        public void EncounterHarnessRestoresCrowdAndDebugKingdom()
        {
            FirstSessionChampionStart.EnableEncounterHarness();
            Assert.IsTrue(FirstSessionChampionStart.AllowDebugKingdomLoad);
            Assert.AreEqual(16, FirstSessionChampionStart.ResolveDummyBudget(16));
            Assert.AreEqual(40, FirstSessionChampionStart.ResolveBotBudget(40));
            Assert.IsTrue(FirstSessionChampionStart.ShowAppearanceRack);
            Assert.IsTrue(FirstSessionChampionStart.AutoStartEncounterIntro);
            Assert.IsFalse(FirstSessionChampionStart.AutoStartFirstFight);
            Assert.IsFalse(FirstSessionChampionStart.ShouldRunProofOfWorth);
        }

        [Test]
        public void CreateLandsOnChampionArenaNotKingdom()
        {
            Assert.IsTrue(
                ProductionSceneDescriptor.TryGetById(
                    ProductionSceneDescriptor.CharacterCreationSceneId,
                    out ProductionSceneRecord create));
            SceneTransition arena = null;
            foreach (SceneTransition transition in create.TransitionTargets)
            {
                if (transition.TargetSceneId == ProductionSceneDescriptor.ChampionArenaSceneId)
                {
                    arena = transition;
                    break;
                }
            }

            Assert.NotNull(arena, "CharacterCreation must advance to ChampionArena.");
            Assert.AreEqual(TransitionStatus.Active, arena.Status);
            Assert.AreEqual("ChampionArena", arena.SerializedValue);
            Assert.AreNotEqual("Kingdom", arena.SerializedValue);
            Assert.AreNotEqual("DemoInitializer", arena.SerializedValue);
        }

        [Test]
        public void ChampionArenaIsShellFoundationFirstSessionDestination()
        {
            Assert.IsTrue(
                ProductionSceneDescriptor.TryGetById(
                    ProductionSceneDescriptor.ChampionArenaSceneId,
                    out ProductionSceneRecord arena));
            Assert.IsTrue(arena.IsInShellFoundation);
            Assert.AreEqual("ChampionArena", arena.SceneName);
            Assert.AreEqual(FirstSessionChampionStart.DestinationSceneName, arena.SceneName);
        }
    }
}
