using System.Collections.Generic;
using System.IO;
using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Scenes;
using AL.Data.Runtime;
using AL.UI.SharedMenu;
using AL.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Tests.EditMode.SharedMenu
{
    public sealed class CrossModeSceneSwitchTests
    {
        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            CrossModeSession.Reset();
            SharedMenuOverlay[] leftovers = Object.FindObjectsOfType<SharedMenuOverlay>();
            for (int i = 0; i < leftovers.Length; i++)
            {
                Object.DestroyImmediate(leftovers[i].gameObject);
            }

            SharedMenuModeSwitchHost[] hosts = Object.FindObjectsOfType<SharedMenuModeSwitchHost>();
            for (int i = 0; i < hosts.Length; i++)
            {
                Object.DestroyImmediate(hosts[i].gameObject);
            }
        }

        [Test]
        public void RoundTripScenesAreInShellFoundationAndBuildSettings()
        {
            string[] loaded =
            {
                SharedMenuIds.AdventureScene,
                SharedMenuIds.KingdomScene
            };

            string buildSettings = File.ReadAllText(
                Path.Combine("ProjectSettings", "EditorBuildSettings.asset"));

            for (int i = 0; i < loaded.Length; i++)
            {
                Assert.IsTrue(
                    ProductionSceneDescriptor.TryGetBySceneName(loaded[i], out ProductionSceneRecord record),
                    loaded[i]);
                Assert.IsTrue(record.IsInShellFoundation, loaded[i] + " ShellFoundation");
                Assert.AreEqual(ProductionSceneDescriptor.StatusCommittedActive, record.Status, loaded[i]);
                Assert.IsTrue(CrossModeSceneSwitch.IsShellLoadable(loaded[i]), loaded[i] + " loadable");
                Assert.That(buildSettings, Does.Contain("/" + loaded[i] + ".unity"));
            }

            Assert.AreEqual("ChampionArena", FirstSessionChampionStart.DestinationSceneName);
            Assert.AreEqual(
                "AL.ChampionMode.ChampionArenaSceneController",
                CrossModeSceneSwitch.ReusedAdventureController);
            Assert.AreEqual(
                "AL.UI.Kingdom.KingdomSceneController",
                CrossModeSceneSwitch.ReusedKingdomController);
            Assert.IsFalse(CrossModeSceneSwitch.IsShellLoadable(SharedMenuIds.WarzoneScene));
            Assert.IsFalse(CrossModeSceneSwitch.IsShellLoadable(SharedMenuIds.InnerRealmWorldScene));
        }

        [Test]
        public void UncommittedIdentityRejectsSwitchEvenWithLordshipLeftover()
        {
            var save = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = false,
                    LastResultId = ProofOfWorthIds.StoneholdVariantId
                }
            };

            CrossModeSwitchPlan plan = CrossModeSceneSwitch.Plan(
                SharedMenuIds.AdventureScene,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);

            Assert.IsFalse(plan.ShouldLoad);
            Assert.AreEqual(SharedMenuIds.SwitchRejected, plan.Status);
            Assert.AreEqual(SharedMenuIds.SwitchRejectedDependency, plan.Failure);
            Assert.AreEqual(SharedMenuIds.AdventureScene, plan.DestinationScene);
        }

        [Test]
        public void SharedMenuEntersKingdomAndReturnsToSameInnerRealmSession()
        {
            SaveGameData save = LordshipSave(RealmId.Eldergrove);
            string loaded = null;
            LoadSceneMode? mode = null;

            CrossModeSwitchPlan enter = CrossModeSceneSwitch.Plan(
                SharedMenuIds.AdventureScene,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);

            Assert.IsTrue(enter.Succeeded);
            Assert.IsTrue(enter.ShouldLoad);
            Assert.AreEqual(SharedMenuIds.Kingdom2_5D, enter.ToMode);
            Assert.AreEqual(SharedMenuIds.KingdomScene, enter.DestinationScene);
            Assert.AreEqual(LoadSceneMode.Single, enter.LoadMode);
            Assert.AreEqual(SharedMenuIds.LoadModeSingle, enter.LoadModeName);
            Assert.IsTrue(CrossModeSceneSwitch.TryCommit(enter, (scene, loadMode) =>
            {
                loaded = scene;
                mode = loadMode;
            }));
            Assert.AreEqual(SharedMenuIds.KingdomScene, loaded);
            Assert.AreEqual(LoadSceneMode.Single, mode);

            CrossModeSwitchPlan back = CrossModeSceneSwitch.Plan(
                SharedMenuIds.KingdomScene,
                SharedMenuIds.Adventure3D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);

            Assert.IsTrue(back.Succeeded);
            Assert.IsTrue(back.ShouldLoad);
            Assert.AreEqual(SharedMenuIds.Adventure3D, back.ToMode);
            Assert.AreEqual(SharedMenuIds.AdventureScene, back.DestinationScene);
            Assert.AreNotEqual(SharedMenuIds.BootScene, back.DestinationScene);
            Assert.AreNotEqual(SharedMenuIds.RealmSelectionScene, back.DestinationScene);
            Assert.AreNotEqual(SharedMenuIds.WarzoneScene, back.DestinationScene);
            Assert.AreEqual(LoadSceneMode.Single, back.LoadMode);
            Assert.IsTrue(CrossModeSession.HasActiveRoundTrip);
        }

        [Test]
        public void CombatUnsafeAndParallelInputsNeverLoad()
        {
            SaveGameData save = LordshipSave(RealmId.Umbral);

            CrossModeSwitchPlan combat = CrossModeSceneSwitch.Plan(
                SharedMenuIds.AdventureScene,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: true,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);
            Assert.IsFalse(combat.ShouldLoad);
            Assert.AreEqual(SharedMenuIds.SwitchRejectedState, combat.Failure);

            CrossModeSwitchPlan unsafePlan = CrossModeSceneSwitch.Plan(
                SharedMenuIds.AdventureScene,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: false,
                unsafeContext: true,
                SharedMenuIds.InputSharedMenu);
            Assert.IsFalse(unsafePlan.ShouldLoad);
            Assert.AreEqual(SharedMenuIds.SwitchRejectedState, unsafePlan.Failure);

            string[] banned =
            {
                SharedMenuIds.InputBoot,
                SharedMenuIds.InputDuel,
                SharedMenuIds.InputDemoInitializer,
                SharedMenuIds.InputConstructionDock
            };
            for (int i = 0; i < banned.Length; i++)
            {
                CrossModeSwitchPlan plan = CrossModeSceneSwitch.Plan(
                    SharedMenuIds.AdventureScene,
                    SharedMenuIds.Kingdom2_5D,
                    save,
                    inCombat: false,
                    unsafeContext: false,
                    banned[i]);
                Assert.IsFalse(plan.ShouldLoad, banned[i]);
                Assert.AreEqual(SharedMenuIds.AdventureScene, plan.DestinationScene, banned[i]);
            }

            CrossModeSwitchPlan boot = CrossModeSceneSwitch.Plan(
                SharedMenuIds.BootScene,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);
            Assert.IsFalse(boot.ShouldLoad);
            Assert.IsTrue(CrossModeSceneSwitch.IsForbiddenReturnScene(SharedMenuIds.BootScene));
            Assert.IsTrue(CrossModeSceneSwitch.IsForbiddenReturnScene(SharedMenuIds.WarzoneScene));
        }

        [Test]
        public void AlreadyInModeAndWorldsAreMutuallyExclusive()
        {
            SaveGameData save = LordshipSave(RealmId.Crownlands);
            CrossModeSwitchPlan already = CrossModeSceneSwitch.Plan(
                SharedMenuIds.AdventureScene,
                SharedMenuIds.Adventure3D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);
            Assert.AreEqual(SharedMenuIds.AlreadyInMode, already.Status);
            Assert.IsFalse(already.ShouldLoad);
            Assert.AreEqual(LoadSceneMode.Single, already.LoadMode);

            Assert.IsFalse(
                CrossModeSceneSwitch.TryCommit(already, (scene, mode) =>
                    Assert.Fail("already-in-mode must not load " + scene)));
        }

        [Test]
        public void PrivateKingdomMapListsOnlyInnerCastleAndAreas()
        {
            IReadOnlyList<string> ids = PrivateKingdomInnerDestinations.EnumerateCastleAndAreas(RealmId.Stonehold);
            Assert.AreEqual(3, ids.Count);
            Assert.AreEqual("poi_zone_inner_stonehold_capital", ids[0]);
            Assert.AreEqual("poi_zone_inner_stonehold_outpost_a", ids[1]);
            Assert.AreEqual("poi_zone_inner_stonehold_outpost_b", ids[2]);
            Assert.IsFalse(PrivateKingdomInnerDestinations.ContainsForbidden(ids));
            Assert.IsFalse(PrivateKingdomInnerDestinations.IsAllowed("warzone_center_unplayable"));
            Assert.IsFalse(PrivateKingdomInnerDestinations.IsAllowed("zone_outer_stonehold"));
            Assert.IsFalse(PrivateKingdomInnerDestinations.IsAllowed("zone_accordant_isle"));
            Assert.IsTrue(FirstSessionInnerRealmSpawn.IsForbiddenDestination("Kingdom", "warzone_center_unplayable"));
        }

        [Test]
        public void HostOpensSharedMenuAndCommitsThroughPlanner()
        {
            SaveGameData save = LordshipSave(RealmId.Stonehold);
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            SharedMenuOverlay overlay = SharedMenuOverlay.Ensure(state);
            overlay.BindInvoke(() => { });
            Assert.IsTrue(overlay.KingdomButton.interactable);
            Assert.AreEqual(SharedMenuIds.KingdomButtonName, overlay.KingdomButton.name);

            SharedMenuModeSwitchHost host = SharedMenuModeSwitchHost.EnsureForSceneName(
                SharedMenuIds.AdventureScene);
            Assert.IsNotNull(host);
            Assert.AreEqual(SharedMenuIds.HostName, host.name);
            Assert.IsNull(SharedMenuModeSwitchHost.EnsureForSceneName(SharedMenuIds.BootScene));

            string loaded = null;
            CrossModeSwitchPlan plan = CrossModeSceneSwitch.Plan(
                SharedMenuIds.AdventureScene,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);
            Assert.IsTrue(CrossModeSceneSwitch.TryCommit(plan, (scene, mode) =>
            {
                loaded = scene;
                Assert.AreEqual(LoadSceneMode.Single, mode);
            }));
            Assert.AreEqual(SharedMenuIds.KingdomScene, loaded);
        }

        private static SaveGameData LordshipSave(RealmId realm)
        {
            var save = new SaveGameData
            {
                SelectedRealm = realm,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true,
                    LastResultId = string.Empty
                }
            };
            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthLordship.ResolveMarkId(realm)));
            return save;
        }
    }
}
