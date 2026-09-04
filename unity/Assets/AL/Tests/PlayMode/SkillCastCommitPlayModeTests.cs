using System.Collections;
using AL.ChampionMode.Control;
using AL.ChampionMode.Presentation;
using AL.ChampionMode.Skills;
using AL.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class SkillCastCommitPlayModeTests
    {
        [UnityTest]
        public IEnumerator PreCommitCancelLeavesManaCooldownAndHitStateUnchanged()
        {
            var host = new GameObject("SkillCastCommit_CancelHost");
            try
            {
                ChampionCombat combat = host.AddComponent<ChampionCombat>();
                SkillCaster caster = host.AddComponent<SkillCaster>();
                ChampionActionPresentation presentation =
                    host.GetComponent<ChampionActionPresentation>() ??
                    host.AddComponent<ChampionActionPresentation>();
                caster.ConfigureRealmContext(RealmId.Crownlands);
                yield return WaitUntilReady(caster);

                float manaBefore = combat.CurrentMana;
                Assert.That(caster.TryCastSkill(1), Is.True);
                Assert.That(caster.IsCasting, Is.True);
                Assert.That(combat.CurrentMana, Is.EqualTo(manaBefore));
                Assert.That(caster.GetCooldownRemaining(1), Is.EqualTo(0f));

                caster.CancelCurrentSkill();

                Assert.That(caster.IsCasting, Is.False);
                Assert.That(combat.CurrentMana, Is.EqualTo(manaBefore));
                Assert.That(caster.GetCooldownRemaining(1), Is.EqualTo(0f));
                Assert.That(presentation.CurrentSignal.Phase, Is.EqualTo(ChampionActionPhase.Interrupted));
            }
            finally
            {
                Object.Destroy(host);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CommitSpendsManaStartsCooldownAndEmitsCommitThenRecovery()
        {
            var host = new GameObject("SkillCastCommit_CommitHost");
            try
            {
                ChampionCombat combat = host.AddComponent<ChampionCombat>();
                SkillCaster caster = host.AddComponent<SkillCaster>();
                ChampionActionPresentation presentation =
                    host.GetComponent<ChampionActionPresentation>() ??
                    host.AddComponent<ChampionActionPresentation>();
                caster.ConfigureRealmContext(RealmId.Crownlands);
                yield return WaitUntilReady(caster);

                float manaBefore = combat.CurrentMana;
                float cost = caster.GetManaCost(1);
                Assert.That(caster.TryCastSkill(1), Is.True);

                float deadline = Time.realtimeSinceStartup + 2f;
                while (caster.IsCasting && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(caster.IsCasting, Is.False);
                Assert.That(combat.CurrentMana, Is.EqualTo(manaBefore - cost).Within(0.01f));
                Assert.That(caster.GetCooldownRemaining(1), Is.GreaterThan(0.05f));
                Assert.That(presentation.CurrentSignal.Kind, Is.EqualTo(ChampionActionKind.Skill));
                Assert.That(presentation.CurrentSignal.Phase, Is.EqualTo(ChampionActionPhase.Recovery));
                Assert.That(presentation.CurrentSignal.ActionId, Is.EqualTo("renewing_guard"));
            }
            finally
            {
                Object.Destroy(host);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedPoolHostCanReacquireWithoutMissingReference()
        {
            const string key = "VFX_SkillCastPoolTeardown";
            Assert.That(
                RuntimeVfxPool.TryGet(key, 2, () => new GameObject(key), out GameObject first),
                Is.True);
            RuntimeVfxPool.ReleaseAfter(key, first, 0f, 2);
            yield return null;
            yield return null;

            GameObject host = GameObject.Find("RuntimeVfxPool");
            Assert.That(host, Is.Not.Null);
            Object.Destroy(host);
            yield return null;

            Assert.That(
                RuntimeVfxPool.TryGet(key, 2, () => new GameObject(key), out GameObject second),
                Is.True);
            Assert.That(second, Is.Not.Null);
            RuntimeVfxPool.ReleaseAfter(key, second, 0f, 2);
            yield return null;
        }

        private static IEnumerator WaitUntilReady(SkillCaster caster)
        {
            const int maximumLoadFrames = 120;
            for (int frame = 0;
                 frame < maximumLoadFrames && caster.LoadoutState == SkillLoadoutState.Loading;
                 frame++)
            {
                yield return null;
            }

            Assert.That(caster.LoadoutState, Is.EqualTo(SkillLoadoutState.Ready));
        }
    }
}
