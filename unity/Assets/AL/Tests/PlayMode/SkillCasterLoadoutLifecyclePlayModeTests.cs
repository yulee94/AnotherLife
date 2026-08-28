using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public IEnumerator UnownedPooledReuseCannotRetainPriorCombatOwner()
        {
            Assembly runtimeAssembly = typeof(SkillEffectFactory).Assembly;
            System.Type ownershipType = runtimeAssembly.GetType(
                "AL.ChampionMode.Skills.CombatEffectOwnership",
                throwOnError: true);
            MethodInfo begin = ownershipType.GetMethod(
                "Begin",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo retire = ownershipType.GetMethod(
                "Retire",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(begin, Is.Not.Null);
            Assert.That(retire, Is.Not.Null);

            var owner = new GameObject("PriorCombatEffectOwner");
            GameObject effect = null;
            const string poolKey = "VFX_OwnershipReuseRegression";
            try
            {
                using ((System.IDisposable)begin.Invoke(null, new object[] { owner }))
                {
                    Assert.That(
                        RuntimeVfxPool.TryGet(
                            poolKey,
                            maxActive: 2,
                            () => new GameObject(poolKey),
                            out effect),
                        Is.True);
                }
                Assert.That(effect, Is.Not.Null);
                RuntimeVfxPool.ReleaseAfter(poolKey, effect, 0f, maxPoolSize: 2);
                yield return null;
                yield return null;

                Assert.That(
                    RuntimeVfxPool.TryGet(
                        poolKey,
                        maxActive: 2,
                        () => new GameObject(poolKey),
                        out GameObject unownedReuse),
                    Is.True);
                Assert.That(unownedReuse, Is.SameAs(effect));
                Assert.That(unownedReuse.activeSelf, Is.True);

                retire.Invoke(null, new object[] { owner });
                yield return null;

                Assert.That(
                    unownedReuse.activeSelf,
                    Is.True,
                    "An unowned pooled reuse must not be retired with its prior owner.");
                RuntimeVfxPool.ReleaseAfter(poolKey, unownedReuse, 0f, maxPoolSize: 2);
                yield return null;
                yield return null;
                effect = null;
            }
            finally
            {
                if (effect != null && effect.activeSelf)
                {
                    RuntimeVfxPool.ReleaseAfter(poolKey, effect, 0f, maxPoolSize: 2);
                }
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [UnityTest]
        public IEnumerator DisablingCasterRetiresItsOwnedCastPreview()
        {
            var existingRingIds = new HashSet<int>(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate => candidate.name == "VFX_Runtime_SkillCastRing")
                    .Select(candidate => candidate.gameObject.GetInstanceID()));
            var host = new GameObject("CancelledOwnedSkillPreviewChampion");
            try
            {
                host.AddComponent<ChampionCombat>();
                SkillCaster caster = host.AddComponent<SkillCaster>();
                caster.ConfigureRealmContext(RealmId.Crownlands);
                const int maximumLoadFrames = 120;
                for (int frame = 0;
                     frame < maximumLoadFrames && caster.LoadoutState == SkillLoadoutState.Loading;
                     frame++)
                {
                    yield return null;
                }

                yield return null;
                Assert.That(caster.LoadoutState, Is.EqualTo(SkillLoadoutState.Ready));
                Assert.That(caster.TryCastSkill(0), Is.True);
                GameObject castRing = Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate =>
                        candidate.name == "VFX_Runtime_SkillCastRing" &&
                        !existingRingIds.Contains(candidate.gameObject.GetInstanceID()))
                    .Select(candidate => candidate.gameObject)
                    .FirstOrDefault();
                Assert.That(castRing, Is.Not.Null);
                Assert.That(castRing.activeInHierarchy, Is.True);

                caster.enabled = false;

                Assert.That(castRing.activeInHierarchy, Is.False);
                yield return new WaitForSeconds(0.4f);
            }
            finally
            {
                Object.Destroy(host);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CancellingCurrentSkillRetiresItsOwnedCastPreview()
        {
            var existingRingIds = new HashSet<int>(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate => candidate.name == "VFX_Runtime_SkillCastRing")
                    .Select(candidate => candidate.gameObject.GetInstanceID()));
            var host = new GameObject("DirectlyCancelledOwnedSkillPreviewChampion");
            try
            {
                host.AddComponent<ChampionCombat>();
                SkillCaster caster = host.AddComponent<SkillCaster>();
                caster.ConfigureRealmContext(RealmId.Crownlands);
                yield return null;
                Assert.That(caster.TryCastSkill(0), Is.True);
                GameObject castRing = Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate =>
                        candidate.name == "VFX_Runtime_SkillCastRing" &&
                        !existingRingIds.Contains(candidate.gameObject.GetInstanceID()))
                    .Select(candidate => candidate.gameObject)
                    .FirstOrDefault();
                Assert.That(castRing, Is.Not.Null);
                Assert.That(castRing.activeInHierarchy, Is.True);

                caster.CancelCurrentSkill();

                Assert.That(castRing.activeInHierarchy, Is.False);
                yield return new WaitForSeconds(0.4f);
            }
            finally
            {
                Object.Destroy(host);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingCasterRetiresItsOwnedResolvedSkillEffect()
        {
            var existingSlashIds = new HashSet<int>(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate => candidate.name == "VFX_Runtime_RealmSlash")
                    .Select(candidate => candidate.gameObject.GetInstanceID()));
            var host = new GameObject("CancelledOwnedResolvedSkillChampion");
            try
            {
                host.AddComponent<ChampionCombat>();
                SkillCaster caster = host.AddComponent<SkillCaster>();
                caster.ConfigureRealmContext(RealmId.Crownlands);
                yield return null;
                Assert.That(caster.TryCastSkill(0), Is.True);

                GameObject slash = null;
                float deadline = Time.realtimeSinceStartup + 2f;
                while (slash == null && Time.realtimeSinceStartup < deadline)
                {
                    slash = Object.FindObjectsByType<Transform>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .Where(candidate =>
                            candidate.name == "VFX_Runtime_RealmSlash" &&
                            !existingSlashIds.Contains(candidate.gameObject.GetInstanceID()))
                        .Select(candidate => candidate.gameObject)
                        .FirstOrDefault();
                    yield return null;
                }

                Assert.That(slash, Is.Not.Null);
                Assert.That(slash.activeInHierarchy, Is.True);

                caster.enabled = false;

                Assert.That(slash.activeInHierarchy, Is.False);
                yield return new WaitForSeconds(0.4f);
            }
            finally
            {
                Object.Destroy(host);
            }

            yield return null;
        }

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
