using System;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Encounter;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionEncounterRuntimeMigrationTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }

            FirstSessionChampionStart.ResetToFirstSessionLanding();
        }

        [Test]
        public void PublishedC2SnapshotIsConsumedOnceByRuntimeHostWithoutRewardMutation()
        {
            ChampionEncounterLoadPlan loaded = LoadPublishedPlan();
            var host = new RecordingHost();

            ChampionEncounterRuntimePlan first =
                ChampionEncounterRuntimeGateway.Apply(loaded, host);
            ChampionEncounterRuntimePlan second =
                ChampionEncounterRuntimeGateway.Apply(loaded, host);

            Assert.That(first.Status, Is.EqualTo(ChampionEncounterRuntimeStatus.Applied));
            Assert.That(first.Receipt, Is.SameAs(loaded.Receipt));
            Assert.That(first.Receipt.ActorId, Is.EqualTo("champion_stonehold_vanguard"));
            Assert.That(first.Receipt.CasterId, Is.EqualTo("champion_stonehold_vanguard"));
            Assert.That(first.Receipt.BossId, Is.EqualTo("boss_stonehold_guardian"));
            Assert.That(first.Receipt.LoadoutId, Is.EqualTo("loadout.wire.v1"));
            Assert.That(
                first.Receipt.SlotIds,
                Is.EqualTo(ChampionEncounterSourceSet.AuthoredWireSlotOrder));
            Assert.That(second.Status, Is.EqualTo(ChampionEncounterRuntimeStatus.Applied));
            Assert.That(host.CallCount, Is.EqualTo(2));
            Assert.That(host.LastReceipt, Is.SameAs(loaded.Receipt));
            Assert.That(host.MutatedRewards, Is.False);
        }

        [Test]
        public void HybridAndUnavailableLoadPlansRejectWithoutHostMutation()
        {
            var host = new RecordingHost();
            ChampionEncounterLoadPlan unavailable =
                ChampionEncounterProductionLoadPath.StartFromCommittedRealm(
                    RealmId.Stonehold,
                    new RecordingApplication(),
                    new List<ChampionEncounterLoadReceipt>());
            ChampionEncounterLoadPlan hybrid = ChampionEncounterLoadGateway.Start(
                Request(),
                HybridPublishedSource(),
                new RecordingApplication(),
                new List<ChampionEncounterLoadReceipt>());

            ChampionEncounterRuntimePlan unavailableApply =
                ChampionEncounterRuntimeGateway.Apply(unavailable, host);
            ChampionEncounterRuntimePlan hybridApply =
                ChampionEncounterRuntimeGateway.Apply(hybrid, host);
            ChampionEncounterRuntimePlan missing =
                ChampionEncounterRuntimeGateway.Apply(null, host);

            Assert.That(
                unavailableApply.Status,
                Is.EqualTo(ChampionEncounterRuntimeStatus.CatalogUnavailable));
            Assert.That(
                unavailableApply.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.CatalogUnavailableCode));
            Assert.That(
                hybridApply.Status,
                Is.EqualTo(ChampionEncounterRuntimeStatus.HybridRejected));
            Assert.That(
                hybridApply.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.HybridSourceCode));
            Assert.That(
                missing.Status,
                Is.EqualTo(ChampionEncounterRuntimeStatus.InvalidInput));
            Assert.That(host.CallCount, Is.Zero);
            Assert.That(unavailableApply.Receipt, Is.Null);
            Assert.That(hybridApply.Receipt, Is.Null);
        }

        [Test]
        public void InvalidNonFiniteCombatAndBossInputRejectsWithoutMutation()
        {
            _host = new GameObject("ChampionEncounterRuntimeMigrationTests_Stats");
            ChampionCombat combat = _host.AddComponent<ChampionCombat>();
            BossDummyAI boss = _host.AddComponent<BossDummyAI>();
            float healthBefore = combat.MaxHealth;
            float bossHealthBefore = boss.MaxHealth;
            string bossNameBefore = boss.BossName;

            Assert.That(combat.ApplyCatalogStats(float.NaN, 80f, 55f), Is.False);
            Assert.That(combat.ApplyCatalogStats(1250f, float.PositiveInfinity, 55f), Is.False);
            Assert.That(combat.ApplyCatalogStats(1250f, 80f, float.NegativeInfinity), Is.False);
            Assert.That(combat.MaxHealth, Is.EqualTo(healthBefore));
            Assert.That(combat.MaxMana, Is.EqualTo(100f));

            Assert.That(boss.ApplyCatalogStats("", "Overlay", 900f, 62f), Is.False);
            Assert.That(boss.ApplyCatalogStats("boss_x", "X", float.NaN, 62f), Is.False);
            Assert.That(boss.ApplyCatalogStats("boss_x", "X", 900f, float.PositiveInfinity), Is.False);
            Assert.That(boss.MaxHealth, Is.EqualTo(bossHealthBefore));
            Assert.That(boss.BossName, Is.EqualTo(bossNameBefore));
        }

        [Test]
        public void UncommittedRealmDoesNotSelectAnotherChampion()
        {
            var data = new LocalGameDataService();
            Assert.That(
                FirstFightCatalog.TryResolve(
                    data,
                    null,
                    RealmId.None,
                    CreateCompleteCatalogLoadout(),
                    out FirstFightLoadout loadout,
                    out string diagnostic),
                Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(FirstFightCatalog.UncommittedRealmCode));
            Assert.That(diagnostic, Is.Not.EqualTo(FirstFightCatalog.ReadyCode));
        }

        [Test]
        public void CasterConsumesC2ReceiptWithoutCatalogOverlay()
        {
            _host = new GameObject("ChampionEncounterRuntimeMigrationTests_Caster");
            _host.SetActive(false);
            _host.AddComponent<ChampionCombat>();
            SkillCaster caster = _host.AddComponent<SkillCaster>();
            ChampionEncounterLoadPlan loaded = LoadPublishedPlan();

            Assert.That(caster.TryApplyEncounterLoad(null), Is.False);
            Assert.That(caster.ProductionEncounterLoad, Is.Null);
            Assert.That(caster.TryApplyEncounterLoad(loaded.Receipt), Is.True);
            Assert.That(caster.ProductionEncounterLoad, Is.SameAs(loaded.Receipt));
            Assert.That(caster.IsLoadoutReady, Is.False);
            Assert.That(caster.LoadoutSnapshot, Is.Null);
            Assert.That(caster.TryCastSkill(0), Is.False);
        }

        [Test]
        public void FirstSessionPracticeAndProductionSourcesStayFailClosed()
        {
            string firstFight = ReadSource("FirstFightCatalog.cs");
            string caster = ReadSource("Skills", "SkillCaster.cs");
            string boss = ReadSource("AI", "BossDummyAI.cs");
            string controller = ReadSource("ChampionArenaSceneController.cs");
            string runtime = ReadSource("Encounter", "ChampionEncounterRuntimeGateway.cs");
            string production = ReadSource("Encounter", "ChampionEncounterProductionLoadPath.cs");

            Assert.That(firstFight, Does.Not.Contain("FindFirstValid"));
            Assert.That(firstFight, Does.Not.Contain("AuthoritativeQuest"));
            Assert.That(caster, Does.Not.Contain("ApplySkillLoadouts"));
            Assert.That(boss, Does.Not.Contain("RollLoot"));
            Assert.That(boss, Does.Not.Contain("IBossLootService"));
            Assert.That(boss, Does.Contain("event Action Defeated"));
            Assert.That(
                controller,
                Does.Contain("ChampionEncounterRuntimeGateway.Apply"));
            Assert.That(controller, Does.Contain("Defeated +="));
            Assert.That(controller, Does.Not.Contain("LootRolled"));
            Assert.That(runtime, Does.Not.Contain("ISaveGameService"));
            Assert.That(runtime, Does.Not.Contain("LocalBossLootService"));
            Assert.That(production, Does.Not.Contain("BossLoot"));
            Assert.That(FirstSessionChampionStart.IsEncounterHarness, Is.False);
            Assert.That(FirstSessionChampionStart.AutoStartFirstFight, Is.False);
        }

        private static ChampionEncounterLoadPlan LoadPublishedPlan()
        {
            var application = new RecordingApplication();
            var receipts = new List<ChampionEncounterLoadReceipt>();
            ChampionEncounterLoadPlan loaded = ChampionEncounterLoadGateway.Start(
                Request(),
                ValidPublishedSource(),
                application,
                receipts);
            Assert.That(loaded.Status, Is.EqualTo(ChampionEncounterLoadStatus.Loaded));
            receipts.Add(loaded.Receipt);
            return loaded;
        }

        private static ChampionEncounterLoadRequest Request()
        {
            return new ChampionEncounterLoadRequest(
                "encounter.stonehold.guardian",
                "stonehold",
                "champion_stonehold_vanguard",
                "champion_stonehold_vanguard",
                "boss_stonehold_guardian",
                "loadout.wire.v1",
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                ChampionEncounterSourceSet.CurrentSourceSetSha256,
                ChampionEncounterSourceSet.AuthoredWireSlotOrder);
        }

        private static ChampionEncounterSourceSet ValidPublishedSource()
        {
            return ChampionEncounterSourceSet.PublishedForTests(
                ChampionEncounterSourceSet.CurrentAuthorityId,
                ChampionEncounterSourceSet.CurrentAuthorityRevision,
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                ChampionEncounterSourceSet.CurrentSourceSetSha256,
                "source-r1",
                "stonehold",
                "champion_stonehold_vanguard",
                "champion_stonehold_vanguard",
                "boss_stonehold_guardian",
                "loadout.wire.v1",
                ChampionEncounterSourceSet.AuthoredWireSlotOrder);
        }

        private static ChampionEncounterSourceSet HybridPublishedSource()
        {
            return ChampionEncounterSourceSet.PublishedForTests(
                ChampionEncounterSourceSet.CurrentAuthorityId,
                ChampionEncounterSourceSet.CurrentAuthorityRevision,
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                ChampionEncounterSourceSet.CurrentSourceSetSha256,
                "source-hybrid",
                "stonehold",
                "champion_stonehold_vanguard",
                "champion_stonehold_vanguard",
                "boss_stonehold_guardian",
                "loadout.wire.v1",
                new[]
                {
                    "skill_iron_bulwark",
                    "skill_shield_slam",
                    "realm_strike",
                    "renewing_guard"
                });
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

        private static string ReadSource(params string[] parts)
        {
            var segments = new List<string>
            {
                Application.dataPath,
                "AL",
                "Scripts",
                "ChampionMode"
            };
            segments.AddRange(parts);
            string path = Path.Combine(segments.ToArray());
            Assert.That(File.Exists(path), Is.True, path);
            return File.ReadAllText(path);
        }

        private sealed class RecordingHost : IChampionEncounterRuntimeHost
        {
            public int CallCount { get; private set; }
            public ChampionEncounterLoadReceipt LastReceipt { get; private set; }
            public bool MutatedRewards { get; private set; }

            public bool TryBind(ChampionEncounterLoadReceipt receipt)
            {
                CallCount++;
                LastReceipt = receipt;
                return true;
            }
        }

        private sealed class RecordingApplication : IChampionEncounterApplication
        {
            public bool TryApply(ChampionEncounterLoadSnapshot snapshot)
            {
                return true;
            }
        }
    }
}
