using System;
using System.IO;
using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class SkillLoadoutAuthorityTests
    {
        [Test]
        public void PackagedCatalogPublishesExactCompleteFourSlotSnapshot()
        {
            string json = ReadPackagedSkillCatalog();

            Assert.IsTrue(SkillLoadoutCatalog.TryParseSnapshot(json, out SkillLoadoutSnapshot snapshot));
            Assert.AreEqual(SkillLoadoutCatalog.RequiredSlotCount, snapshot.Count);
            AssertSlot(snapshot, 0, "realm_strike", "Realm Strike", "realm_slash", 150f);
            AssertSlot(snapshot, 1, "renewing_guard", "Renewing Guard", "renewing_guard", 180f);
            AssertSlot(snapshot, 2, "warzone_burst", "Warzone Burst", "warzone_shockwave", 115f);
            AssertSlot(snapshot, 3, "warmaster_breaker", "Warmaster Breaker", "warmaster_breaker", 260f);
        }

        [Test]
        public void ReorderedJsonRecordsRejectWholeSnapshotWithoutRepair()
        {
            string reordered = SwapRecordObjects(
                ReadPackagedSkillCatalog(),
                "realm_strike",
                "renewing_guard");

            Assert.IsFalse(SkillLoadoutCatalog.TryParseSnapshot(reordered, out _));
        }

        [Test]
        public void SnapshotIsDetachedFromMutableCatalogRows()
        {
            SkillLoadoutData[] rows = CreateValidRows();
            Assert.IsTrue(SkillLoadoutCatalog.TryCreateSnapshot(rows, out SkillLoadoutSnapshot snapshot));

            rows[0].id = "tampered";
            rows[0].displayName = "Tampered";
            rows[0].power = 999f;
            rows[0] = null;

            AssertSlot(snapshot, 0, "realm_strike", "Realm Strike", "realm_slash", 150f);
        }

        [Test]
        public void MissingSlotRejectsWholeSnapshot()
        {
            SkillLoadoutData[] valid = CreateValidRows();
            var missing = new[] { valid[0], valid[1], valid[2] };

            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(missing, out _));
        }

        [Test]
        public void DuplicateSlotRejectsWholeSnapshot()
        {
            SkillLoadoutData[] rows = CreateValidRows();
            rows[2].slot = 1;

            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(rows, out _));
        }

        [Test]
        public void DuplicateIdentityRejectsWholeSnapshot()
        {
            SkillLoadoutData[] rows = CreateValidRows();
            rows[3].id = rows[2].id;

            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(rows, out _));
        }

        [Test]
        public void ReorderedIdentitiesRejectWholeSnapshot()
        {
            SkillLoadoutData[] rows = CreateValidRows();
            string first = rows[0].id;
            rows[0].id = rows[1].id;
            rows[1].id = first;

            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(rows, out _));
        }

        [Test]
        public void PartialOrNonFiniteRowRejectsWholeSnapshot()
        {
            SkillLoadoutData[] partial = CreateValidRows();
            partial[2].vfxKey = string.Empty;
            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(partial, out _));

            SkillLoadoutData[] nonFinite = CreateValidRows();
            nonFinite[2].power = float.NaN;
            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(nonFinite, out _));
        }

        [Test]
        public void ContradictoryBehaviorOrVfxIdentityRejectsWholeSnapshot()
        {
            SkillLoadoutData[] wrongRole = CreateValidRows();
            wrongRole[0].role = "self_heal_guard";
            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(wrongRole, out _));

            SkillLoadoutData[] wrongVfx = CreateValidRows();
            wrongVfx[0].vfxKey = "warmaster_breaker";
            Assert.IsFalse(SkillLoadoutCatalog.TryCreateSnapshot(wrongVfx, out _));
        }

        [TestCase(0, "renewing_guard")]
        [TestCase(2, "realm_strike")]
        [TestCase(2, "warzone_burst")]
        public void FirstFightRejectsAnythingExceptExactSpecialIdentityAndSlot(int slot, string id)
        {
            var data = new LocalGameDataService();
            SkillLoadoutData[] skills = CreateValidRows();
            skills[0].slot = slot;
            skills[0].id = id;

            Assert.IsFalse(FirstFightCatalog.TryResolve(
                data,
                null,
                RealmId.Stonehold,
                skills,
                out _,
                out string diagnostic));
            Assert.AreEqual(FirstFightCatalog.SpecialMissingCode, diagnostic);
        }

        [Test]
        public void FirstFightArrayBoundaryRejectsPartialNonFiniteAndContradictoryRows()
        {
            var data = new LocalGameDataService();
            AssertFirstFightRejects(
                data,
                new[] { CreateValidRows()[0] },
                "A partial loadout must never become first-fight authority.");

            SkillLoadoutData[] nan = CreateValidRows();
            nan[0].power = float.NaN;
            AssertFirstFightRejects(data, nan, "NaN power must fail closed.");

            SkillLoadoutData[] infinity = CreateValidRows();
            infinity[0].power = float.PositiveInfinity;
            AssertFirstFightRejects(data, infinity, "Infinite power must fail closed.");

            SkillLoadoutData[] wrongRole = CreateValidRows();
            wrongRole[0].role = "self_heal_guard";
            AssertFirstFightRejects(data, wrongRole, "Contradictory role must fail closed.");

            SkillLoadoutData[] wrongVfx = CreateValidRows();
            wrongVfx[0].vfxKey = "warzone_shockwave";
            AssertFirstFightRejects(data, wrongVfx, "Contradictory VFX must fail closed.");

            SkillLoadoutData[] duplicate = CreateValidRows();
            duplicate[3].id = duplicate[2].id;
            AssertFirstFightRejects(data, duplicate, "Duplicate identity must fail closed.");
        }

        [Test]
        public void CasterRejectsInputUntilValidatedSnapshotIsReady()
        {
            var host = new GameObject("SkillLoadoutAuthorityTests_UnreadyCaster");
            host.SetActive(false);
            try
            {
                host.AddComponent<ChampionCombat>();
                SkillCaster caster = host.AddComponent<SkillCaster>();
                caster.ConfigureRealmContext(RealmId.Stonehold);

                Assert.AreEqual(SkillLoadoutState.Loading, caster.LoadoutState);
                Assert.IsFalse(caster.IsLoadoutReady);
                Assert.IsNull(caster.LoadoutSnapshot);
                Assert.IsFalse(caster.TryGetLoadoutSnapshot(out _));
                Assert.AreEqual("Loading", caster.GetSkillName(0));
                Assert.IsFalse(caster.TryCastSkill(0));
                Assert.IsFalse(caster.IsCasting);
                Assert.IsFalse(caster.RetryLoadoutLoad(), "Inactive casters must not start URI/file work.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertFirstFightRejects(
            LocalGameDataService data,
            SkillLoadoutData[] rows,
            string message)
        {
            Assert.IsFalse(
                FirstFightCatalog.TryResolve(
                    data,
                    null,
                    RealmId.Stonehold,
                    rows,
                    out _,
                    out string diagnostic),
                message);
            Assert.AreEqual(FirstFightCatalog.SpecialMissingCode, diagnostic, message);
        }

        private static string ReadPackagedSkillCatalog()
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "skill_weather.v1.json"));
        }

        private static string SwapRecordObjects(string json, string firstId, string secondId)
        {
            string first = ExtractFlatRecordObject(json, firstId);
            string second = ExtractFlatRecordObject(json, secondId);
            const string marker = "__AL_SKILL_RECORD_SWAP_MARKER__";
            Assert.That(json, Does.Not.Contain(marker));
            return json.Replace(first, marker).Replace(second, first).Replace(marker, second);
        }

        private static string ExtractFlatRecordObject(string json, string id)
        {
            int idIndex = json.IndexOf("\"id\": \"" + id + "\"", StringComparison.Ordinal);
            Assert.That(idIndex, Is.GreaterThanOrEqualTo(0), "Missing fixture record " + id);
            int start = json.LastIndexOf('{', idIndex);
            int end = json.IndexOf("\n    }", idIndex, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return json.Substring(start, end - start + "\n    }".Length);
        }

        private static void AssertSlot(
            SkillLoadoutSnapshot snapshot,
            int slot,
            string id,
            string displayName,
            string vfxKey,
            float power)
        {
            Assert.IsTrue(snapshot.TryGetSlot(slot, out SkillLoadoutSlot value));
            Assert.AreEqual(slot, value.Slot);
            Assert.AreEqual(id, value.Id);
            Assert.AreEqual(displayName, value.DisplayName);
            Assert.AreEqual(vfxKey, value.VfxKey);
            Assert.AreEqual(power, value.Power);
        }

        private static SkillLoadoutData[] CreateValidRows()
        {
            return new[]
            {
                CreateRow(0, "realm_strike", "Realm Strike", "melee_damage", "realm_slash", 4f, 20f, 0.05f, 2.6f, 150f, 0.72f),
                CreateRow(1, "renewing_guard", "Renewing Guard", "self_heal_guard", "renewing_guard", 8f, 30f, 0.35f, 0f, 180f, 0f),
                CreateRow(2, "warzone_burst", "Warzone Burst", "area_damage", "warzone_shockwave", 10f, 45f, 0.45f, 4.2f, 115f, 0.72f),
                CreateRow(3, "warmaster_breaker", "Warmaster Breaker", "elite_break_damage", "warmaster_breaker", 14f, 60f, 0.65f, 3.4f, 260f, 0.72f)
            };
        }

        private static SkillLoadoutData CreateRow(
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
