using System;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class ChampionRealmContextTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void ResolverRequiresCommittedValidIdentityInsteadOfCurrentRealmFallback()
        {
            var service = new FakeRealmService(
                RealmId.Crownlands,
                new RealmIdentitySnapshot(
                    RealmIdentityStatus.Uncommitted,
                    RealmId.Crownlands,
                    "0.1.0",
                    "AL-REALM-UNCOMMITTED"));

            ChampionRealmContextResult result = ChampionRealmContext.Resolve(service);

            Assert.That(result.Status, Is.EqualTo(ChampionRealmContextStatus.IdentityUnavailable));
            Assert.That(result.RealmId, Is.EqualTo(RealmId.None));
            Assert.That(result.IdentityStatus, Is.EqualTo(RealmIdentityStatus.Uncommitted));
            Assert.That(result.TechnicalCode, Is.EqualTo(ChampionRealmContext.IdentityUnavailableCode));
        }

        [Test]
        public void ResolverReturnsExactCommittedRealm()
        {
            var service = new FakeRealmService(
                RealmId.Umbral,
                new RealmIdentitySnapshot(
                    RealmIdentityStatus.CommittedValid,
                    RealmId.Umbral,
                    "0.1.0",
                    "AL-REALM-COMMITTED-VALID"));

            ChampionRealmContextResult result = ChampionRealmContext.Resolve(service);

            Assert.That(result.IsAvailable, Is.True);
            Assert.That(result.RealmId, Is.EqualTo(RealmId.Umbral));
            Assert.That(result.TechnicalCode, Is.EqualTo(ChampionRealmContext.ReadyCode));
        }

        [TestCase(null)]
        [TestCase("throw")]
        public void ResolverReturnsTypedUnavailableResultForMissingServiceOrIdentity(string mode)
        {
            IRealmService service = mode == null ? null : new ThrowingRealmService();

            ChampionRealmContextResult result = ChampionRealmContext.Resolve(service);

            Assert.That(result.Status, Is.EqualTo(ChampionRealmContextStatus.ServiceUnavailable));
            Assert.That(result.RealmId, Is.EqualTo(RealmId.None));
            Assert.That(result.TechnicalCode, Is.EqualTo(ChampionRealmContext.ServiceUnavailableCode));
        }

        [Test]
        public void SkillCastWithoutRealmContextDoesNotSpendMana()
        {
            _root = new GameObject("ChampionRealmContextTests_Skill");
            ChampionCombat combat = _root.AddComponent<ChampionCombat>();
            SkillCaster caster = _root.AddComponent<SkillCaster>();
            float manaBefore = combat.CurrentMana;

            bool accepted = caster.TryCastSkill(0);

            Assert.That(accepted, Is.False);
            Assert.That(combat.CurrentMana, Is.EqualTo(manaBefore));
            Assert.That(caster.IsCasting, Is.False);
        }

        [Test]
        public void ConfiguredActorRealmCannotBeReplacedByAnotherRealm()
        {
            _root = new GameObject("ChampionRealmContextTests_Actor");
            SkillCaster caster = _root.AddComponent<SkillCaster>();

            caster.ConfigureRealmContext(RealmId.Eldergrove);
            caster.ConfigureRealmContext(RealmId.Crownlands);

            Assert.That(ReadField<RealmId>(caster, "_realmId"), Is.EqualTo(RealmId.Eldergrove));
        }

        [Test]
        public void BossDamageIsIgnoredWithoutRealmContext()
        {
            _root = new GameObject("ChampionRealmContextTests_Boss");
            BossDummyAI boss = _root.AddComponent<BossDummyAI>();
            WriteField(boss, "_currentHealth", 1200f);

            boss.TakeDamage(100f);

            Assert.That(boss.CurrentHealth, Is.EqualTo(1200f));
            Assert.That(boss.IsDead, Is.False);
        }

        [Test]
        public void BotWithUnavailablePlayerRealmStaysDisabledAndPreservesNone()
        {
            _root = new GameObject("ChampionRealmContextTests_Bot");
            BotChampionAI bot = _root.AddComponent<BotChampionAI>();

            bot.Configure(RealmId.Stonehold, RealmId.None, null, Vector3.zero, 24f, 1f);

            Assert.That(bot.enabled, Is.False);
            Assert.That(bot.IsAlive, Is.False);
            Assert.That(ReadField<RealmId>(bot, "_playerRealm"), Is.EqualTo(RealmId.None));
        }

        [Test]
        public void BotWithValidRealmsBecomesActiveWithoutChangingAllegiance()
        {
            _root = new GameObject("ChampionRealmContextTests_ValidBot");
            BotChampionAI bot = _root.AddComponent<BotChampionAI>();

            bot.Configure(RealmId.Stonehold, RealmId.Eldergrove, null, Vector3.zero, 24f, 1f);

            Assert.That(bot.enabled, Is.True);
            Assert.That(bot.IsAlive, Is.True);
            Assert.That(bot.RealmId, Is.EqualTo(RealmId.Stonehold));
            Assert.That(ReadField<RealmId>(bot, "_playerRealm"), Is.EqualTo(RealmId.Eldergrove));
        }

        [TestCase(RealmId.None)]
        [TestCase((RealmId)999)]
        public void NormalizeRejectsUnavailableOrUndefinedRealm(RealmId realmId)
        {
            Assert.That(ChampionRealmContext.Normalize(realmId), Is.EqualTo(RealmId.None));
        }

        private static T ReadField<T>(object target, string name)
        {
            return (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void WriteField<T>(object target, string name, T value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private sealed class FakeRealmService : IRealmService
        {
            public FakeRealmService(RealmId currentRealmId, RealmIdentitySnapshot identity)
            {
                CurrentRealmId = currentRealmId;
                Identity = identity;
            }

            public RealmId CurrentRealmId { get; }
            public RealmDefinition CurrentRealm => null;
            public RealmIdentitySnapshot Identity { get; }
            public RealmSelectionResult TrySelectRealm(RealmSelectionRequest request) => default;
            public void SelectRealm(RealmId id) { }
        }

        private sealed class ThrowingRealmService : IRealmService
        {
            public RealmId CurrentRealmId => RealmId.Crownlands;
            public RealmDefinition CurrentRealm => null;
            public RealmIdentitySnapshot Identity => throw new InvalidOperationException("identity unavailable");
            public RealmSelectionResult TrySelectRealm(RealmSelectionRequest request) => default;
            public void SelectRealm(RealmId id) { }
        }
    }
}
