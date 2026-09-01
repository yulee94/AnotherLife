using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Scenes;
using AL.Data.Runtime;
using AL.UI.SharedMenu;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.SharedMenu
{
    public sealed class KingdomManagementUnlockTests
    {
        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            SharedMenuOverlay[] leftovers = Object.FindObjectsOfType<SharedMenuOverlay>();
            for (int i = 0; i < leftovers.Length; i++)
            {
                Object.DestroyImmediate(leftovers[i].gameObject);
            }
        }

        [Test]
        public void FreshSaveStaysLockedNarrativeWithRealCopyAndVisibleButton()
        {
            SaveGameData save = FreshIdentitySave(RealmId.Stonehold);
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(save);

            Assert.AreEqual(SharedMenuIds.KingdomManagementModule, state.ModuleId);
            Assert.AreEqual(SharedMenuAvailability.LockedNarrative, state.Availability);
            Assert.AreEqual(SharedMenuIds.ReasonLockedByNarrative, state.ReasonCode);
            Assert.IsTrue(state.Visible);
            Assert.IsFalse(state.CanInvoke);
            Assert.AreEqual(SharedMenuCopy.Title, state.Title);
            Assert.AreEqual(SharedMenuCopy.Locked, state.Detail);
            Assert.That(state.Detail, Does.Contain("Proof of Worth"));
            Assert.That(state.Detail, Does.Contain("lordship"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(state.Detail));

            SharedMenuOverlay overlay = SharedMenuOverlay.Ensure(state);
            Assert.IsNotNull(overlay.KingdomButton);
            Assert.AreEqual(SharedMenuIds.KingdomButtonName, overlay.KingdomButton.name);
            Assert.IsFalse(overlay.KingdomButton.interactable);
            Assert.AreEqual(SharedMenuCopy.Title, overlay.TitleLabel.text);
            Assert.AreEqual(SharedMenuCopy.Locked, overlay.DetailLabel.text);

            ModeSwitchResult enter = KingdomManagementUnlock.RequestSwitch(EnterKingdom(save));
            Assert.IsFalse(enter.Succeeded);
            Assert.AreEqual(SharedMenuIds.SwitchRejected, enter.Status);
            Assert.AreEqual(SharedMenuIds.SwitchRejectedDependency, enter.Failure);
            Assert.AreEqual(SharedMenuIds.AdventureScene, enter.DestinationScene);
        }

        [Test]
        public void OldSaveWithoutLordshipAndVictoryLeftoverStayLocked()
        {
            Assert.IsFalse(KingdomManagementUnlock.IsLordshipGranted(new SaveGameData()));

            var identityOnly = FreshIdentitySave(RealmId.Stonehold);
            Assert.IsFalse(KingdomManagementUnlock.IsLordshipGranted(identityOnly));
            Assert.AreEqual(
                SharedMenuAvailability.LockedNarrative,
                KingdomManagementUnlock.EvaluateKingdomManagement(identityOnly).Availability);

            var leftoverVictory = FreshIdentitySave(RealmId.Stonehold);
            leftoverVictory.ChampionCustomization.LastResultId = "ch01_proof_of_worth:victory";
            Assert.IsFalse(ProofOfWorthLordship.IsGranted(leftoverVictory));
            Assert.AreEqual(
                SharedMenuAvailability.LockedNarrative,
                KingdomManagementUnlock.EvaluateKingdomManagement(leftoverVictory).Availability);
        }

        [Test]
        public void AcceptedLordshipUnlocksSharedMenuAndRoundTrip()
        {
            SaveGameData save = FreshIdentitySave(RealmId.Eldergrove);
            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthIds.EldergroveVariantId));
            Assert.AreEqual("ch01_eldergrove", save.ChampionCustomization.LastResultId);
            Assert.IsTrue(KingdomManagementUnlock.IsLordshipGranted(save));

            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            Assert.AreEqual(SharedMenuAvailability.Available, state.Availability);
            Assert.IsTrue(state.CanInvoke);
            Assert.AreEqual(SharedMenuCopy.NewlyUnlocked, state.Detail);

            SharedMenuOverlay overlay = SharedMenuOverlay.Ensure(state);
            Assert.IsTrue(overlay.KingdomButton.interactable);
            Assert.AreEqual(SharedMenuCopy.Title, overlay.TitleLabel.text);

            ModeSwitchResult enter = KingdomManagementUnlock.RequestSwitch(EnterKingdom(save));
            Assert.IsTrue(enter.Succeeded);
            Assert.AreEqual(SharedMenuIds.Kingdom2_5D, enter.DestinationMode);
            Assert.AreEqual(SharedMenuIds.KingdomScene, enter.DestinationScene);

            ModeSwitchResult back = KingdomManagementUnlock.RequestSwitch(new ModeSwitchRequest(
                SharedMenuIds.Kingdom2_5D,
                SharedMenuIds.Adventure3D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu));
            Assert.IsTrue(back.Succeeded);
            Assert.AreEqual(SharedMenuIds.Adventure3D, back.DestinationMode);
            Assert.AreEqual(SharedMenuIds.AdventureScene, back.DestinationScene);
            Assert.AreNotEqual("Boot", back.DestinationScene);
            Assert.AreNotEqual("RealmSelection", back.DestinationScene);
        }

        [Test]
        public void CombatAndUnsafeRejectSwitchEvenAfterLordship()
        {
            SaveGameData save = FreshIdentitySave(RealmId.Umbral);
            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthIds.UmbralVariantId));

            SharedMenuModuleState combat = KingdomManagementUnlock.EvaluateKingdomManagement(
                save,
                inCombat: true);
            Assert.AreEqual(SharedMenuAvailability.BlockedTransient, combat.Availability);
            Assert.IsFalse(combat.CanInvoke);
            Assert.AreEqual(SharedMenuCopy.UnavailableCombat, combat.Detail);

            ModeSwitchResult combatSwitch = KingdomManagementUnlock.RequestSwitch(new ModeSwitchRequest(
                SharedMenuIds.Adventure3D,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: true,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu));
            Assert.IsFalse(combatSwitch.Succeeded);
            Assert.AreEqual(SharedMenuIds.SwitchRejectedState, combatSwitch.Failure);

            SharedMenuModuleState unsafeState = KingdomManagementUnlock.EvaluateKingdomManagement(
                save,
                unsafeContext: true);
            Assert.AreEqual(SharedMenuAvailability.BlockedTransient, unsafeState.Availability);
            Assert.AreEqual(SharedMenuCopy.UnavailableUnsafe, unsafeState.Detail);

            ModeSwitchResult unsafeSwitch = KingdomManagementUnlock.RequestSwitch(new ModeSwitchRequest(
                SharedMenuIds.Adventure3D,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: false,
                unsafeContext: true,
                SharedMenuIds.InputSharedMenu));
            Assert.IsFalse(unsafeSwitch.Succeeded);
            Assert.AreEqual(SharedMenuIds.SwitchRejectedState, unsafeSwitch.Failure);
        }

        [Test]
        public void ParallelUnlocksAndConstructionDockNeverModeSwitch()
        {
            SaveGameData save = FreshIdentitySave(RealmId.Crownlands);
            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthIds.CrownlandsVariantId));

            string[] banned =
            {
                SharedMenuIds.InputBoot,
                SharedMenuIds.InputDuel,
                SharedMenuIds.InputDemoInitializer,
                SharedMenuIds.InputConstructionDock
            };
            for (int i = 0; i < banned.Length; i++)
            {
                Assert.IsTrue(KingdomManagementUnlock.IsParallelUnlockAttempt(banned[i]), banned[i]);
                ModeSwitchResult result = KingdomManagementUnlock.RequestSwitch(new ModeSwitchRequest(
                    SharedMenuIds.Adventure3D,
                    SharedMenuIds.Kingdom2_5D,
                    save,
                    inCombat: false,
                    unsafeContext: false,
                    banned[i]));
                Assert.IsFalse(result.Succeeded, banned[i]);
                Assert.AreEqual(SharedMenuIds.SwitchRejectedDependency, result.Failure, banned[i]);
                Assert.AreEqual(SharedMenuIds.AdventureScene, result.DestinationScene, banned[i]);
            }

            Assert.IsFalse(FirstSessionChampionStart.AllowDebugKingdomLoad);
        }

        [Test]
        public void BootStaysInnerRealm3DEvenAfterLordship()
        {
            SaveGameData save = FreshIdentitySave(RealmId.Stonehold);
            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthIds.StoneholdVariantId));

            Assert.AreEqual(
                FirstUserBootDestinationResolver.GameplaySceneName,
                FirstUserBootDestinationResolver.ResolveSceneName(
                    save,
                    "RealmSelection",
                    gameplaySceneLoadable: true));
            Assert.AreNotEqual(
                "Kingdom",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    save,
                    "RealmSelection",
                    gameplaySceneLoadable: true));
            Assert.AreEqual("ChampionArena", FirstSessionChampionStart.DestinationSceneName);
            Assert.IsTrue(
                ProductionSceneDescriptor.TryGetById(
                    ProductionSceneDescriptor.ChampionArenaSceneId,
                    out ProductionSceneRecord arena));
            Assert.AreEqual("ChampionArena", arena.SceneName);
        }

        [Test]
        public void PersistSlotIsExistingLastResultIdNotANewSaveField()
        {
            Assert.IsNull(typeof(SaveGameData).GetField("LordshipUnlocked"));
            Assert.IsNull(typeof(SaveGameData).GetField("KingdomUnlocked"));
            Assert.NotNull(typeof(ChampionCustomizationState).GetField("LastResultId"));
            Assert.AreEqual("SaveGameData.ChampionCustomization", MvpLoopSaveCodec.PersistenceSlot);

            SaveGameData save = FreshIdentitySave(RealmId.Stonehold);
            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthIds.StoneholdVariantId));
            Assert.AreEqual(
                SharedMenuAvailability.Available,
                KingdomManagementUnlock.EvaluateKingdomManagement(save).Availability);
            Assert.AreEqual("ch01_stonehold", MvpLoopSaveCodec.Read(save).LastResultId);
        }

        private static SaveGameData FreshIdentitySave(RealmId realm)
        {
            return new SaveGameData
            {
                SelectedRealm = realm,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true,
                    LastResultId = string.Empty,
                    Username = "SharedMenuTester"
                }
            };
        }

        private static ModeSwitchRequest EnterKingdom(SaveGameData save)
        {
            return new ModeSwitchRequest(
                SharedMenuIds.Adventure3D,
                SharedMenuIds.Kingdom2_5D,
                save,
                inCombat: false,
                unsafeContext: false,
                SharedMenuIds.InputSharedMenu);
        }
    }
}
