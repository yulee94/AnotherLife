using System.Collections;
using AL.ChampionMode.Control;
using AL.ChampionMode.Skills;
using AL.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class SkillCasterLoadoutLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator ActiveCasterPublishesCompleteSnapshotAndRetainsItAcrossReactivation()
        {
            var host = new GameObject("SkillCasterLoadoutLifecyclePlayModeTests_Host");
            try
            {
                host.AddComponent<ChampionCombat>();
                SkillCaster caster = host.AddComponent<SkillCaster>();
                caster.ConfigureRealmContext(RealmId.Stonehold);

                const int maximumLoadFrames = 120;
                for (int frame = 0;
                     frame < maximumLoadFrames && caster.LoadoutState == SkillLoadoutState.Loading;
                     frame++)
                {
                    yield return null;
                }

                Assert.AreEqual(SkillLoadoutState.Ready, caster.LoadoutState);
                Assert.IsTrue(caster.IsLoadoutReady);
                Assert.IsTrue(caster.TryGetLoadoutSnapshot(out SkillLoadoutSnapshot snapshot));
                Assert.AreSame(snapshot, caster.LoadoutSnapshot);
                Assert.AreEqual(SkillLoadoutCatalog.RequiredSlotCount, snapshot.Count);
                Assert.AreEqual("realm_strike", caster.GetSkillId(0));
                Assert.AreEqual("Realm Strike", caster.GetSkillName(0));
                Assert.AreEqual("realm_slash", caster.GetSkillVfxKey(0));
                Assert.AreEqual(20f, caster.GetManaCost(0));
                Assert.IsFalse(caster.RetryLoadoutLoad(), "A ready caster must not start a duplicate load.");

                host.SetActive(false);
                yield return null;
                Assert.AreEqual(SkillLoadoutState.Ready, caster.LoadoutState);
                Assert.AreSame(snapshot, caster.LoadoutSnapshot);

                host.SetActive(true);
                yield return null;
                Assert.AreEqual(SkillLoadoutState.Ready, caster.LoadoutState);
                Assert.AreSame(snapshot, caster.LoadoutSnapshot);
            }
            finally
            {
                Object.Destroy(host);
            }

            yield return null;
        }
    }
}
