using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Core.Scenes;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class FirstFightCatalogTests
    {
        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            SliceRunState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            SliceRunState.Reset();
        }

        [Test]
        public void ResolvesCatalogPlayerOpponentAndSpecialWithoutFabricating()
        {
            var data = new LocalGameDataService();
            SkillLoadoutData[] skills = CreateCompleteCatalogLoadout();
            Assert.IsTrue(
                SkillLoadoutCatalog.TryCreateSnapshot(
                    skills,
                    out SkillLoadoutSnapshot snapshot));

            Assert.IsTrue(
                FirstFightCatalog.TryResolveSnapshot(
                    data,
                    null,
                    RealmId.Stonehold,
                    snapshot,
                    out FirstFightLoadout loadout,
                    out string diagnostic));

            Assert.AreEqual(FirstFightCatalog.ReadyCode, diagnostic);
            Assert.AreEqual("champion_stonehold_vanguard", loadout.PlayerId);
            Assert.AreEqual(1250, loadout.PlayerMaxHealth);
            Assert.AreEqual(80, loadout.PlayerMaxMana);
            Assert.AreEqual(55, loadout.PlayerAttack);
            Assert.AreEqual("champion_crownlands_sharpshooter", loadout.OpponentId);
            Assert.AreEqual(900, loadout.OpponentMaxHealth);
            Assert.AreEqual(62, loadout.OpponentAttack);
            Assert.AreEqual(FirstSessionChampionStart.SpecialSkillId, loadout.SpecialSkillId);
            Assert.AreEqual(150f, loadout.SpecialPower);
            AL.VerticalSlice.SliceChampionProfile catalogDefault =
                AL.VerticalSlice.SliceChampionProfile.CreateDefault();
            Assert.AreEqual(catalogDefault.Id, loadout.PlayerId);
            Assert.AreEqual(catalogDefault.MaxHealth, loadout.PlayerMaxHealth);
            Assert.AreNotEqual(AL.VerticalSlice.SliceOpponentProfile.CreateDefault().MaxHealth, loadout.OpponentMaxHealth);
        }

        [Test]
        public void ConfirmedChampionMustExistInCatalog()
        {
            var data = new LocalGameDataService();
            SliceRunState.ConfirmChampion(new ChampionState
            {
                Id = "champion_not_in_catalog",
                DisplayName = "Invented",
                MaxHealth = 9999,
                MaxMana = 999,
                Attack = 999
            });

            Assert.IsFalse(
                FirstFightCatalog.TryResolve(
                    data,
                    SliceRunState.Champion,
                    RealmId.Stonehold,
                    CreateCompleteCatalogLoadout(),
                    out _,
                    out string diagnostic));
            Assert.That(diagnostic, Does.StartWith(FirstFightCatalog.PlayerMissingCode));
        }

        [Test]
        public void MissingSpecialFailsClosed()
        {
            var data = new LocalGameDataService();
            SkillLoadoutData[] skills = CreateCompleteCatalogLoadout();
            skills[0].id = "missing_realm_strike";
            Assert.IsFalse(
                FirstFightCatalog.TryResolve(
                    data,
                    null,
                    RealmId.Stonehold,
                    skills,
                    out _,
                    out string diagnostic));
            Assert.AreEqual(FirstFightCatalog.SpecialMissingCode, diagnostic);
        }

        [Test]
        public void MissingGameDataFailsClosed()
        {
            Assert.IsFalse(
                FirstFightCatalog.TryResolve(
                    null,
                    null,
                    RealmId.Stonehold,
                    CreateCompleteCatalogLoadout(),
                    out _,
                    out string diagnostic));
            Assert.AreEqual(FirstFightCatalog.MissingCode, diagnostic);
        }

        [Test]
        public void ChampionCombatRejectsNonCatalogZeroStats()
        {
            var host = new GameObject("FirstFightCombatHost");
            try
            {
                var combat = host.AddComponent<ChampionCombat>();
                Assert.IsFalse(combat.ApplyCatalogStats(0f, 80f, 55f));
                Assert.IsTrue(combat.ApplyCatalogStats(1250f, 80f, 55f));
                Assert.AreEqual(1250f, combat.MaxHealth);
                Assert.AreEqual(55f, combat.GetAttackDamage());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FirstSessionDoesNotAutoStartFightBeforeC1Guardian()
        {
            Assert.IsFalse(FirstSessionChampionStart.AutoStartFirstFight);
            Assert.IsTrue(FirstSessionChampionStart.ShouldRunProofOfWorth);
            Assert.IsFalse(FirstSessionChampionStart.AutoStartEncounterIntro);
            Assert.AreEqual(0, FirstSessionChampionStart.ResolveDummyBudget(16));
            Assert.AreEqual(0, FirstSessionChampionStart.ResolveBotBudget(40));
            Assert.AreEqual("BossDummy", FirstSessionChampionStart.OpponentObjectName);
            Assert.AreEqual("EncounterClearPanel", FirstSessionChampionStart.WinPanelName);
            Assert.AreEqual("DefeatRetryPanel", FirstSessionChampionStart.LosePanelName);
            Assert.AreEqual("PlayerFrame", FirstSessionChampionStart.PlayerFrameName);
            Assert.AreEqual("CombatHotbar", FirstSessionChampionStart.HotbarName);
            Assert.AreEqual("BossTargetLock", FirstSessionChampionStart.TargetLockName);
            Assert.AreEqual("realm_strike", FirstSessionChampionStart.SpecialSkillId);
            Assert.That(FirstSessionChampionStart.LandingFeedCopy, Does.Contain("basic attack"));
            Assert.That(FirstSessionChampionStart.LandingFeedCopy, Does.Contain("Realm Strike"));
        }

        [Test]
        public void EncounterHarnessDoesNotAutoStartTheFirstSessionFight()
        {
            FirstSessionChampionStart.EnableEncounterHarness();
            Assert.IsFalse(FirstSessionChampionStart.AutoStartFirstFight);
            Assert.IsTrue(FirstSessionChampionStart.AutoStartEncounterIntro);
        }

        [Test]
        public void ProductionBootPathDoesNotLaunchGreyboxSim()
        {
            Assert.IsTrue(
                ProductionSceneDescriptor.TryGetById(
                    ProductionSceneDescriptor.BootSceneId,
                    out ProductionSceneRecord boot));
            Assert.AreEqual("Boot", boot.SceneName);
            Assert.AreEqual("AL.UI.BootController", boot.RequiredControllerType);
            Assert.AreNotEqual("DemoInitializer", boot.SceneName);
            Assert.AreNotEqual("AL.VerticalSlice.Combat.GreyboxCombatEncounter", boot.RequiredControllerType);

            bool landsOnRealmSelect = false;
            foreach (SceneTransition transition in boot.TransitionTargets)
            {
                Assert.AreNotEqual("DemoInitializer", transition.SerializedValue);
                if (transition.TargetSceneId == ProductionSceneDescriptor.RealmSelectionSceneId)
                {
                    landsOnRealmSelect = true;
                    Assert.AreEqual(TransitionStatus.Active, transition.Status);
                }
            }

            Assert.IsTrue(landsOnRealmSelect, "Fresh Boot must go to RealmSelection, not greybox.");
            Assert.AreEqual("ChampionArena", FirstUserBootDestinationResolver.GameplaySceneName);
            Assert.AreNotEqual("Kingdom", FirstUserBootDestinationResolver.GameplaySceneName);
            Assert.AreNotEqual("DemoInitializer", FirstUserBootDestinationResolver.GameplaySceneName);
        }

        [Test]
        public void FirstSessionCreateStillLandsOnChampionArenaNotKingdomGreybox()
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
                }

                Assert.AreNotEqual("Kingdom", transition.SerializedValue);
                Assert.AreNotEqual("DemoInitializer", transition.SerializedValue);
            }

            Assert.NotNull(arena);
            Assert.AreEqual("ChampionArena", arena.SerializedValue);
        }

        private static SkillLoadoutData[] CreateCompleteCatalogLoadout()
        {
            return new[]
            {
                CreateSkill(0, "realm_strike", "Realm Strike", "melee_damage", "realm_slash", 4f, 20f, 0.05f, 2.6f, 150f, 0.72f),
                CreateSkill(1, "renewing_guard", "Renewing Guard", "self_heal_guard", "renewing_guard", 8f, 30f, 0.35f, 0f, 180f, 0f),
                CreateSkill(2, "warzone_burst", "Warzone Burst", "area_damage", "warzone_shockwave", 10f, 45f, 0.45f, 4.2f, 115f, 0.72f),
                CreateSkill(3, "warmaster_breaker", "Warmaster Breaker", "elite_break_damage", "warmaster_breaker", 14f, 60f, 0.65f, 3.4f, 260f, 0.72f)
            };
        }

        private static SkillLoadoutData CreateSkill(
            int slot,
            string id,
            string displayName,
            string role,
            string vfxKey,
            float cooldown,
            float mana,
            float castTime,
            float range,
            float power,
            float botMultiplier)
        {
            return new SkillLoadoutData
            {
                slot = slot,
                id = id,
                displayName = displayName,
                role = role,
                vfxKey = vfxKey,
                cooldownSeconds = cooldown,
                manaCost = mana,
                castTimeSeconds = castTime,
                rangeMeters = range,
                power = power,
                botDamageMultiplier = botMultiplier
            };
        }
    }
}
