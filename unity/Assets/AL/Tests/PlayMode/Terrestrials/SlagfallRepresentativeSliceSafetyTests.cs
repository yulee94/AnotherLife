#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AL.RealmWar.Territories.Runtime;
using AL.Terrestrials.Slagfall;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    public sealed class SlagfallRepresentativeSliceSafetyTests
    {
        private const string SlicePrefabPath =
            "Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/" +
            "Environment/Prefabs/Slagfall_RepresentativeSlice.prefab";
        private GameObject _instance;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_instance != null)
            {
                Object.Destroy(_instance);
            }

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RepresentativeSlicePassesDeterministicRepresentationGate()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(SlicePrefabPath);
            Assert.NotNull(prefab, SlicePrefabPath);
            _instance = Object.Instantiate(prefab);
            SlagfallRepresentativeSlice slice =
                _instance.GetComponent<SlagfallRepresentativeSlice>();
            Assert.NotNull(slice);

            int score = 0;
            int[] participantIds = slice.SyntheticCrowd
                .Select(participant => participant.GetInstanceID())
                .ToArray();

            if (slice.SyntheticCrowd.Count ==
                    TerritoryLoadDegradationPlanner
                        .SafeRepresentedUserCapacity &&
                slice.Controller.CurrentPlan.RepresentedCount == 100 &&
                slice.Controller.CurrentPlan.CulledCount == 0)
            {
                score += 20;
            }

            if (slice.Controller.CurrentLevel == TerritoryLoadLevel.Heavy &&
                slice.Slagwhistle.CurrentTier ==
                    TerritoryRenderTier.LowDetail &&
                slice.Slagwhistle.IsRepresented)
            {
                score += 15;
            }

            bool criticalContinuity = true;
            bool heavyRecovery = true;
            for (int cycle = 0; cycle < 24; cycle++)
            {
                slice.ApplySyntheticPressure(60f, 0.5f);
                criticalContinuity &=
                    slice.Controller.CurrentLevel ==
                        TerritoryLoadLevel.Critical &&
                    slice.Controller.CurrentPlan.RepresentedCount == 100 &&
                    slice.Controller.CurrentPlan.CulledCount == 0 &&
                    slice.Slagwhistle.CurrentTier ==
                        TerritoryRenderTier.Impostor &&
                    slice.Slagwhistle.IsRepresented;

                slice.ApplySyntheticPressure(16f, 3f);
                heavyRecovery &=
                    slice.Controller.CurrentLevel ==
                        TerritoryLoadLevel.Heavy &&
                    slice.Controller.CurrentPlan.RepresentedCount == 100 &&
                    slice.Controller.CurrentPlan.CulledCount == 0 &&
                    slice.Slagwhistle.CurrentTier ==
                        TerritoryRenderTier.LowDetail &&
                    slice.Slagwhistle.IsRepresented;
            }

            if (criticalContinuity)
            {
                score += 20;
            }

            if (heavyRecovery)
            {
                score += 15;
            }

            if (slice.SyntheticCrowd.All(
                    participant =>
                        participant.ActiveRepresentationCount == 1))
            {
                score += 10;
            }

            CollectionAssert.AreEqual(
                participantIds,
                slice.SyntheticCrowd
                    .Select(participant => participant.GetInstanceID())
                    .ToArray(),
                "Load shedding must not replace participant objects.");
            score += 10;

            slice.SetAccessibility(true, true);
            if (slice.EffectsOff &&
                slice.ReducedMotion &&
                slice.Slagwhistle.ReducedMotion &&
                slice.Slagwhistle.IsRepresented)
            {
                score += 10;
            }

            Assert.GreaterOrEqual(
                score,
                90,
                $"Slagfall deterministic representation score was {score}/100.");
            Assert.AreEqual(
                100,
                score,
                "All deterministic representation and degradation checks should pass.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SyntheticCrowdVisibilityRestoresAllOneHundredUsers()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(SlicePrefabPath);
            Assert.NotNull(prefab, SlicePrefabPath);
            _instance = Object.Instantiate(prefab);
            SlagfallRepresentativeSlice slice =
                _instance.GetComponent<SlagfallRepresentativeSlice>();
            Assert.NotNull(slice);

            slice.SetSyntheticCrowdActive(false);
            yield return null;
            Assert.AreEqual(
                0,
                slice.ActiveRepresentedSyntheticUserCount);

            slice.SetSyntheticCrowdActive(true);
            yield return null;
            Assert.AreEqual(
                TerritoryLoadDegradationPlanner
                    .SafeRepresentedUserCapacity,
                slice.ActiveRepresentedSyntheticUserCount);
            Assert.AreEqual(
                TerritoryLoadLevel.Heavy,
                slice.Controller.CurrentLevel);
            Assert.AreEqual(
                TerritoryRenderTier.LowDetail,
                slice.Slagwhistle.CurrentTier);
        }

        [UnityTest]
        public IEnumerator RepeatedEnterCancelAndExitReturnsToStableObjectCount()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(SlicePrefabPath);
            Assert.NotNull(prefab, SlicePrefabPath);

            int baselineSlices = CountLoadedSceneSlices();
            int baselineParticipants = CountLoadedSceneParticipants();

            for (int cycle = 0; cycle < 12; cycle++)
            {
                _instance = Object.Instantiate(prefab);
                yield return null;

                SlagfallRepresentativeSlice slice =
                    _instance.GetComponent<SlagfallRepresentativeSlice>();
                Assert.NotNull(slice);
                slice.ApplySyntheticPressure(60f, 0.5f);
                Assert.AreEqual(
                    TerritoryRenderTier.Impostor,
                    slice.Slagwhistle.CurrentTier);

                slice.CancelOptionalPresentation();
                Assert.AreEqual(
                    TerritoryRenderTier.LowDetail,
                    slice.Slagwhistle.CurrentTier);
                Assert.IsTrue(slice.Slagwhistle.IsRepresented);
                Assert.AreEqual(
                    TerritoryLoadDegradationPlanner
                        .SafeRepresentedUserCapacity,
                    slice.Controller.CurrentPlan.RepresentedCount);

                Object.Destroy(_instance);
                _instance = null;
                yield return null;

                Assert.AreEqual(
                    baselineSlices,
                    CountLoadedSceneSlices(),
                    $"Representative-slice root leaked after cycle {cycle}.");
                Assert.AreEqual(
                    baselineParticipants,
                    CountLoadedSceneParticipants(),
                    $"Synthetic participant leaked after cycle {cycle}.");
            }

            yield return Resources.UnloadUnusedAssets();
            Assert.AreEqual(baselineSlices, CountLoadedSceneSlices());
            Assert.AreEqual(
                baselineParticipants,
                CountLoadedSceneParticipants());
        }

        private static int CountLoadedSceneSlices()
        {
            return Resources
                .FindObjectsOfTypeAll<SlagfallRepresentativeSlice>()
                .Count(slice => slice.gameObject.scene.IsValid());
        }

        private static int CountLoadedSceneParticipants()
        {
            return Resources
                .FindObjectsOfTypeAll<TerritoryCrowdParticipant>()
                .Count(participant => participant.gameObject.scene.IsValid());
        }
    }
}
#endif
