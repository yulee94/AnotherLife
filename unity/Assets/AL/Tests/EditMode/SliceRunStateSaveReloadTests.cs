using System;
using System.IO;
using AL.Slice;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public class SliceRunStateSaveReloadTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "AnotherLife-SliceRunState", Guid.NewGuid().ToString("N"));
            RunStateSession.Reset();
            RunStateStore.ClearMemory();
        }

        [TearDown]
        public void TearDown()
        {
            RunStateSession.Reset();
            RunStateStore.ClearMemory();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private static RunState BuildCompleteRun()
        {
            RunState state = RunState.CreateDefault();
            state.phase = SlicePhase.Complete;
            state.realm.realmId = "Eldergrove";
            state.realm.realmName = "Eldergrove Elves";
            state.character.id = "champion_1";
            state.character.displayName = "Aelthra";
            state.character.className = "Mage";
            state.character.subclassId = "Archmage";
            state.character.stats.Add(new StatEntry { key = "health", value = 120 });
            state.character.stats.Add(new StatEntry { key = "attack", value = 18 });
            state.character.loadout.Add("fireball");
            state.combat.outcome = SliceOutcome.Win;
            state.combat.completed = true;
            state.combat.opponentId = "ferrum";
            state.combat.opponentName = "Ferrum the Iron Dragon";
            state.combat.rounds = 4;
            state.combat.rewards.Add(new ResourceEntry { type = "Gold", amount = 500 });
            state.kingdom.buildPerformed = true;
            state.kingdom.lastBuildAction = "Farm:1->2";
            state.kingdom.buildings.Add(new BuildingEntry { id = "Farm", level = 2 });
            state.kingdom.budget.Add(new ResourceEntry { type = "Stone", amount = 320 });
            return state;
        }

        [Test]
        public void SaveThenLoadRoundTripsAllFourStages()
        {
            RunState original = BuildCompleteRun();

            RunStateSaveResult saveResult = RunStateStore.Save(original, _root);
            Assert.AreEqual(RunStateSaveStatus.Saved, saveResult.Status, saveResult.Message);
            Assert.IsTrue(saveResult.PersistedToDisk);
            Assert.IsTrue(File.Exists(saveResult.FilePath));

            RunStateLoadResult loadResult = RunStateStore.Load(_root);
            Assert.AreEqual(RunStateLoadStatus.Loaded, loadResult.Status, loadResult.Message);
            Assert.IsNotNull(loadResult.State);

            RunState loaded = loadResult.State;
            Assert.AreEqual("Eldergrove", loaded.realm.realmId);
            Assert.IsTrue(loaded.realm.IsSelected);
            Assert.AreEqual("Eldergrove", loaded.realm.RealmIdValue.ToString());
            Assert.AreEqual("champion_1", loaded.character.id);
            Assert.AreEqual("Aelthra", loaded.character.displayName);
            Assert.AreEqual("Mage", loaded.character.className);
            Assert.AreEqual(SliceOutcome.Win, loaded.combat.outcome);
            Assert.IsTrue(loaded.combat.Won);
            Assert.AreEqual(4, loaded.combat.rounds);
            Assert.AreEqual(500L, loaded.combat.rewards[0].amount);
            Assert.IsTrue(loaded.kingdom.buildPerformed);
            Assert.AreEqual(1, loaded.kingdom.buildings.Count);
            Assert.AreEqual(2, loaded.kingdom.buildings[0].level);
        }

        [Test]
        public void JsonRoundTripPreservesNestedState()
        {
            RunState original = BuildCompleteRun();
            string json = original.ToJson(prettyPrint: true);

            RunState restored = RunState.FromJson(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(original.realm.realmId, restored.realm.realmId);
            Assert.AreEqual(original.character.stats.Count, restored.character.stats.Count);
            Assert.AreEqual(original.character.loadout[0], restored.character.loadout[0]);
            Assert.AreEqual(original.combat.rewards[0].amount, restored.combat.rewards[0].amount);
            Assert.AreEqual(original.kingdom.budget[0].type, restored.kingdom.budget[0].type);
        }

        [Test]
        public void LoadWithNoFileRecoversFromMemory()
        {
            RunState original = BuildCompleteRun();
            RunStateStore.Save(original, _root);

            string emptyDir = Path.Combine(_root, "empty");
            Directory.CreateDirectory(emptyDir);

            RunStateLoadResult result = RunStateStore.Load(emptyDir);

            Assert.AreEqual(RunStateLoadStatus.RecoveredFromMemory, result.Status, result.Message);
            Assert.IsNotNull(result.State);
            Assert.AreEqual("Eldergrove", result.State.realm.realmId);
        }

        [Test]
        public void LoadWithNoFileAndNoMemoryReturnsNotFound()
        {
            RunStateLoadResult result = RunStateStore.Load(_root);

            Assert.AreEqual(RunStateLoadStatus.NotFound, result.Status);
            Assert.IsNull(result.State);
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void CloneIsIndependentOfSource()
        {
            RunState original = BuildCompleteRun();
            RunState clone = original.Clone();
            clone.realm.realmId = "Crownlands";

            Assert.AreEqual("Eldergrove", original.realm.realmId);
            Assert.AreEqual("Crownlands", clone.realm.realmId);
        }

        [Test]
        public void SessionSetClonesSoCallersCannotMutateLiveState()
        {
            RunState state = BuildCompleteRun();
            RunStateSession.Set(state);
            state.realm.realmId = "Umbral";

            Assert.AreEqual("Eldergrove", RunStateSession.Current.realm.realmId);
        }

        [Test]
        public void SaveAfterKingdomBuildStampsPhaseAndPersists()
        {
            RunStateSession.Set(BuildCompleteRun());
            RunStateSession.Current.kingdom.buildPerformed = true;

            RunStateSaveResult result = RunStateSavePoints.SaveAfterKingdomBuild(_root);

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.AreEqual(SlicePhase.Complete, RunStateSession.Current.phase);
            Assert.Greater(RunStateSession.Current.savedAtUnixSeconds, 0);
            Assert.IsTrue(File.Exists(result.FilePath));
        }

        [Test]
        public void SaveAfterKingdomBuildOnInProgressSliceKeepsKingdomPhase()
        {
            RunState state = BuildCompleteRun();
            state.kingdom.buildPerformed = false;
            RunStateSession.Set(state);

            RunStateSaveResult result = RunStateSavePoints.SaveAfterKingdomBuild(_root);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SlicePhase.KingdomBuild, RunStateSession.Current.phase);
        }

        [Test]
        public void LoadFromMainOrPauseRestoresSession()
        {
            RunState original = BuildCompleteRun();
            RunStateStore.Save(original, _root);
            RunStateSession.Reset();

            RunStateLoadResult result = RunStateSavePoints.LoadFromMainOrPause(_root);

            Assert.IsTrue(result.Succeeded);
            Assert.IsNotNull(RunStateSession.Current);
            Assert.AreEqual("Eldergrove", RunStateSession.Current.realm.realmId);
        }

        [Test]
        public void SaveWithNullStateReturnsNoState()
        {
            RunStateSaveResult result = RunStateStore.Save(null, _root);

            Assert.AreEqual(RunStateSaveStatus.NoState, result.Status);
            Assert.IsFalse(result.Succeeded);
        }
    }
}
