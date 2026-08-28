using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Control;
using AL.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class ChampionBasicAttackReceiptTests
    {
        [UnityTest]
        public IEnumerator BasicAttackUsesOwnedAudioChannel()
        {
            GameObject existingAudioRoot = GameObject.Find("ChampionRuntimeCombatAudio");
            var champion = new GameObject("OwnedBasicAttackAudioChampion");
            try
            {
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);

                Assert.That(controller.RequestBasicAttack(), Is.True);

                AudioSource source = champion.GetComponentInChildren<AudioSource>(true);
                Assert.That(source, Is.Not.Null);
                Assert.That(source.isPlaying, Is.True);
            }
            finally
            {
                Object.Destroy(champion);
                if (existingAudioRoot == null)
                {
                    GameObject createdAudioRoot = GameObject.Find("ChampionRuntimeCombatAudio");
                    if (createdAudioRoot != null)
                    {
                        Object.Destroy(createdAudioRoot);
                    }
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingChampionRetiresItsOwnedBasicAttackWhiff()
        {
            var existingWhiffIds = new HashSet<int>(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate => candidate.name == "VFX_Runtime_BasicAttack_Whiff")
                    .Select(candidate => candidate.gameObject.GetInstanceID()));
            var champion = new GameObject("CancelledBasicAttackVfxChampion");
            try
            {
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                Assert.That(controller.RequestBasicAttack(), Is.True);

                GameObject whiff = null;
                float deadline = Time.realtimeSinceStartup + 1f;
                while (whiff == null && Time.realtimeSinceStartup < deadline)
                {
                    whiff = Object.FindObjectsByType<Transform>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .Where(candidate =>
                            candidate.name == "VFX_Runtime_BasicAttack_Whiff" &&
                            !existingWhiffIds.Contains(candidate.gameObject.GetInstanceID()))
                        .Select(candidate => candidate.gameObject)
                        .FirstOrDefault();
                    yield return null;
                }

                Assert.That(whiff, Is.Not.Null);
                Assert.That(whiff.activeInHierarchy, Is.True);

                controller.enabled = false;

                Assert.That(whiff.activeInHierarchy, Is.False);
                yield return new WaitForSeconds(0.25f);
            }
            finally
            {
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingChampionDestroysItsOwnedBasicAttackFloatingText()
        {
            var existingTextIds = new HashSet<int>(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Where(candidate => candidate.name == "VFX_Runtime_FloatingCombatText")
                    .Select(candidate => candidate.gameObject.GetInstanceID()));
            var champion = new GameObject("CancelledBasicAttackTextChampion");
            try
            {
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                Assert.That(controller.RequestBasicAttack(), Is.True);

                GameObject floatingText = null;
                float deadline = Time.realtimeSinceStartup + 3f;
                while (floatingText == null && Time.realtimeSinceStartup < deadline)
                {
                    floatingText = Object.FindObjectsByType<Transform>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None)
                        .Where(candidate =>
                            candidate.name == "VFX_Runtime_FloatingCombatText" &&
                            !existingTextIds.Contains(candidate.gameObject.GetInstanceID()))
                        .Select(candidate => candidate.gameObject)
                        .FirstOrDefault();
                    yield return null;
                }

                Assert.That(floatingText, Is.Not.Null);
                controller.enabled = false;
                yield return null;

                Assert.That(floatingText == null, Is.True);
            }
            finally
            {
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ConcurrentChampionDodgesUseIndependentOwnedAudioChannels()
        {
            GameObject existingAudioRoot = GameObject.Find("ChampionRuntimeCombatAudio");
            var first = new GameObject("FirstOwnedDodgeAudioChampion");
            var second = new GameObject("SecondOwnedDodgeAudioChampion");
            try
            {
                ChampionController firstController = first.AddComponent<ChampionController>();
                ChampionController secondController = second.AddComponent<ChampionController>();
                firstController.ConfigureRealmContext(RealmId.Crownlands);
                secondController.ConfigureRealmContext(RealmId.Stonehold);

                firstController.RequestDodge();
                secondController.RequestDodge();
                yield return null;

                AudioSource firstSource = first.GetComponentInChildren<AudioSource>(true);
                AudioSource secondSource = second.GetComponentInChildren<AudioSource>(true);
                Assert.That(firstSource, Is.Not.Null);
                Assert.That(secondSource, Is.Not.Null);
                Assert.That(firstSource, Is.Not.SameAs(secondSource));
                Assert.That(firstSource.isPlaying, Is.True);
                Assert.That(secondSource.isPlaying, Is.True);
            }
            finally
            {
                Object.Destroy(first);
                Object.Destroy(second);
                if (existingAudioRoot == null)
                {
                    GameObject createdAudioRoot = GameObject.Find("ChampionRuntimeCombatAudio");
                    if (createdAudioRoot != null)
                    {
                        Object.Destroy(createdAudioRoot);
                    }
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingChampionStopsOnlyItsOwnedAudioChannel()
        {
            GameObject existingAudioRoot = GameObject.Find("ChampionRuntimeCombatAudio");
            var first = new GameObject("FirstCancelledDodgeAudioChampion");
            var second = new GameObject("SecondLiveDodgeAudioChampion");
            try
            {
                ChampionController firstController = first.AddComponent<ChampionController>();
                ChampionController secondController = second.AddComponent<ChampionController>();
                firstController.ConfigureRealmContext(RealmId.Crownlands);
                secondController.ConfigureRealmContext(RealmId.Stonehold);

                firstController.RequestDodge();
                secondController.RequestDodge();
                AudioSource firstSource = first.GetComponentInChildren<AudioSource>(true);
                AudioSource secondSource = second.GetComponentInChildren<AudioSource>(true);
                Assert.That(firstSource, Is.Not.Null);
                Assert.That(secondSource, Is.Not.Null);
                Assert.That(firstSource.isPlaying, Is.True);
                Assert.That(secondSource.isPlaying, Is.True);

                firstController.enabled = false;

                Assert.That(firstSource.isPlaying, Is.False);
                Assert.That(secondSource.isPlaying, Is.True);
            }
            finally
            {
                Object.Destroy(first);
                Object.Destroy(second);
                if (existingAudioRoot == null)
                {
                    GameObject createdAudioRoot = GameObject.Find("ChampionRuntimeCombatAudio");
                    if (createdAudioRoot != null)
                    {
                        Object.Destroy(createdAudioRoot);
                    }
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingChampionRetiresOnlyItsOwnedDodgeTrail()
        {
            var existingTrailIds = new HashSet<int>(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate => candidate.name == "VFX_Runtime_DodgeTrail")
                    .Select(candidate => candidate.gameObject.GetInstanceID()));
            var first = new GameObject("FirstCancelledDodgeVfxChampion");
            var second = new GameObject("SecondLiveDodgeVfxChampion");
            first.transform.position = new Vector3(-8f, 0f, 0f);
            second.transform.position = new Vector3(8f, 0f, 0f);
            try
            {
                ChampionController firstController = first.AddComponent<ChampionController>();
                ChampionController secondController = second.AddComponent<ChampionController>();
                firstController.ConfigureRealmContext(RealmId.Crownlands);
                secondController.ConfigureRealmContext(RealmId.Stonehold);

                firstController.RequestDodge();
                secondController.RequestDodge();
                GameObject[] createdTrails = Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate =>
                        candidate.name == "VFX_Runtime_DodgeTrail" &&
                        !existingTrailIds.Contains(candidate.gameObject.GetInstanceID()))
                    .Select(candidate => candidate.gameObject)
                    .ToArray();
                Assert.That(createdTrails, Has.Length.EqualTo(2));
                GameObject firstTrail = createdTrails
                    .OrderBy(candidate =>
                        Vector3.Distance(candidate.transform.position, first.transform.position))
                    .First();
                GameObject secondTrail = createdTrails.Single(candidate => candidate != firstTrail);
                Assert.That(firstTrail.activeInHierarchy, Is.True);
                Assert.That(secondTrail.activeInHierarchy, Is.True);

                firstController.enabled = false;

                Assert.That(firstTrail.activeInHierarchy, Is.False);
                Assert.That(secondTrail.activeInHierarchy, Is.True);
                yield return new WaitForSeconds(0.4f);
            }
            finally
            {
                Object.Destroy(first);
                Object.Destroy(second);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingChampionRetiresItsOwnedConfirmedHitImpact()
        {
            var existingImpactIds = new HashSet<int>(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate => candidate.name == "VFX_Runtime_RoyalStrike")
                    .Select(candidate => candidate.gameObject.GetInstanceID()));
            var champion = new GameObject("CancelledConfirmedHitVfxChampion");
            try
            {
                ChampionController controller = champion.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                Assert.That(
                    controller.TryBindEditorBasicAttackResolver(new ConfirmedHitResolver()),
                    Is.True);
                Assert.That(controller.RequestBasicAttack(), Is.True);

                GameObject impact = null;
                float deadline = Time.realtimeSinceStartup + 1f;
                while (impact == null && Time.realtimeSinceStartup < deadline)
                {
                    impact = Object.FindObjectsByType<Transform>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .Where(candidate =>
                            candidate.name == "VFX_Runtime_RoyalStrike" &&
                            !existingImpactIds.Contains(candidate.gameObject.GetInstanceID()))
                        .Select(candidate => candidate.gameObject)
                        .FirstOrDefault();
                    yield return null;
                }

                Assert.That(impact, Is.Not.Null);
                Assert.That(impact.activeInHierarchy, Is.True);

                controller.enabled = false;

                Assert.That(impact.activeInHierarchy, Is.False);
                yield return new WaitForSeconds(0.5f);
            }
            finally
            {
                Object.Destroy(champion);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator OnlyAnAcceptedBasicAttackPublishesAReceipt()
        {
            var champion = new GameObject("AcceptedAttackReceiptChampion");
            ChampionController controller =
                champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            var observedCount = 0;
            ChampionBasicAttackReceipt observed = default;
            controller.BasicAttackAccepted += receipt =>
            {
                observedCount++;
                observed = receipt;
            };

            Assert.That(controller.LastBasicAttackReceipt.Sequence, Is.Zero);
            Assert.That(controller.RequestBasicAttack(), Is.True);
            Assert.That(observedCount, Is.EqualTo(1));
            Assert.That(observed.Sequence, Is.EqualTo(1));
            Assert.That(
                controller.LastBasicAttackReceipt.Sequence,
                Is.EqualTo(observed.Sequence));

            Assert.That(
                controller.RequestBasicAttack(),
                Is.False,
                "An overlapping command is rejected and must not look accepted to tutorial progression.");
            Assert.That(observedCount, Is.EqualTo(1));
            Assert.That(controller.LastBasicAttackReceipt.Sequence, Is.EqualTo(1));

            Object.Destroy(champion);
            yield return null;
        }

        private sealed class ConfirmedHitResolver : IChampionBasicAttackResolver
        {
            public bool TryResolve(
                ChampionBasicAttackContext context,
                out ChampionBasicAttackResolution resolution)
            {
                resolution = new ChampionBasicAttackResolution(
                    ChampionBasicAttackResolutionKind.Hit,
                    context.HitCenter,
                    "HIT");
                return true;
            }
        }
    }
}
