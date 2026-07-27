using System.Linq;
using AL.Core;
using AL.Data.Runtime;
using AL.Kingdom;
using AL.Kingdom.Visuals.Architecture;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class KingdomBuildingConfirmedLevelTransitionTests
    {
        private const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";

        [SetUp]
        public void SetUp()
        {
            DestroyIfPresent("Kingdom_CityBoard");
        }

        [TearDown]
        public void TearDown()
        {
            DestroyIfPresent("Kingdom_CityBoard");
        }

        [TestCase(
            RealmId.Stonehold,
            "stonehold.workshop",
            "stonehold")]
        [TestCase(
            RealmId.Eldergrove,
            "eldergrove.atelier",
            "eldergrove")]
        [TestCase(
            RealmId.Crownlands,
            "crownlands.stormwright",
            "crownlands")]
        [TestCase(
            RealmId.Umbral,
            "umbral.veilwright",
            "umbral")]
        public void PackagedWorkshopBindingsOwnRealmMotionGrammar(
            RealmId realmId,
            string expectedProfileId,
            string expectedRealmId)
        {
            KingdomBuildingModelEntry entry = LoadCatalog().Entries.Single(
                candidate =>
                    candidate.RealmId == realmId &&
                    candidate.BuildingId == "Workshop");

            Assert.That(entry.RealmMotionProfile, Is.Not.Null);
            Assert.That(entry.RealmMotionProfile.IsConfigured, Is.True);
            Assert.That(entry.HasCompatibleRealmMotionProfile, Is.True);
            Assert.That(
                entry.RealmMotionProfile.ProfileId,
                Is.EqualTo(expectedProfileId));
            Assert.That(
                entry.RealmMotionProfile.RealmId,
                Is.EqualTo(expectedRealmId));
        }

        [Test]
        public void MismatchedRealmMotionGrammarInvalidatesBinding()
        {
            KingdomBuildingModelEntry stonehold = Entry(RealmId.Stonehold);
            KingdomBuildingModelEntry umbral = Entry(RealmId.Umbral);
            var invalid = new KingdomBuildingModelEntry(
                stonehold.ModelId,
                stonehold.RealmId,
                stonehold.BuildingId,
                stonehold.Prefab,
                umbral.RealmMotionProfile,
                stonehold.StrategicBoardScale,
                stonehold.MinimumLevel,
                stonehold.MaximumLevel);

            Assert.That(invalid.HasCompatibleRealmMotionProfile, Is.False);
            Assert.That(invalid.IsConfigured, Is.False);
        }

        [Test]
        public void TrackerDoesNotReplayOnFirstObservationOrSameLevel()
        {
            var tracker = new KingdomBuildingConfirmedLevelTracker();

            Assert.That(
                tracker.Observe(
                    RealmId.Umbral,
                    "kingdom.slot.workshop",
                    6,
                    false,
                    true),
                Is.False);
            Assert.That(
                tracker.Observe(
                    RealmId.Umbral,
                    "kingdom.slot.workshop",
                    6,
                    false,
                    true),
                Is.False);
        }

        [Test]
        public void TrackerRequestsOnlyAdjacentConfirmedCompletion()
        {
            var tracker = new KingdomBuildingConfirmedLevelTracker();
            tracker.Observe(
                RealmId.Stonehold,
                "kingdom.slot.workshop",
                5,
                false,
                true);
            tracker.Observe(
                RealmId.Stonehold,
                "kingdom.slot.workshop",
                5,
                true,
                true);

            Assert.That(
                tracker.Observe(
                    RealmId.Stonehold,
                    "kingdom.slot.workshop",
                    6,
                    false,
                    true),
                Is.True);
            Assert.That(
                tracker.Observe(
                    RealmId.Stonehold,
                    "kingdom.slot.workshop",
                    8,
                    false,
                    true),
                Is.False);
        }

        [Test]
        public void TrackerKeepsRealmAndSlotIdentityIndependent()
        {
            var tracker = new KingdomBuildingConfirmedLevelTracker();
            tracker.Observe(
                RealmId.Crownlands,
                "kingdom.slot.workshop",
                4,
                false,
                true);

            Assert.That(
                tracker.Observe(
                    RealmId.Umbral,
                    "kingdom.slot.workshop",
                    5,
                    false,
                    true),
                Is.False);
            Assert.That(
                tracker.Observe(
                    RealmId.Crownlands,
                    "kingdom.slot.workshop",
                    5,
                    false,
                    true),
                Is.True);
        }

        [Test]
        public void TrackerCanAnimateFirstBuiltLevelAfterObservedReservedPlot()
        {
            var tracker = new KingdomBuildingConfirmedLevelTracker();
            tracker.Observe(
                RealmId.Eldergrove,
                "kingdom.slot.workshop",
                0,
                false,
                true);
            tracker.Observe(
                RealmId.Eldergrove,
                "kingdom.slot.workshop",
                0,
                true,
                true);

            Assert.That(
                tracker.Observe(
                    RealmId.Eldergrove,
                    "kingdom.slot.workshop",
                    1,
                    false,
                    true),
                Is.True);
        }

        [Test]
        public void InvalidSnapshotClearsTransitionHistory()
        {
            var tracker = new KingdomBuildingConfirmedLevelTracker();
            tracker.Observe(
                RealmId.Umbral,
                "kingdom.slot.workshop",
                5,
                false,
                true);
            Assert.That(
                tracker.Observe(
                    RealmId.Umbral,
                    "kingdom.slot.workshop",
                    0,
                    false,
                    false),
                Is.False);
            Assert.That(
                tracker.Observe(
                    RealmId.Umbral,
                    "kingdom.slot.workshop",
                    6,
                    false,
                    true),
                Is.False);
        }

        [Test]
        public void TransitionMovesOnlyNewlyConfirmedDeltaAndSettles()
        {
            KingdomBuildingModelEntry entry = Entry(RealmId.Umbral);
            GameObject instance = Object.Instantiate(entry.Prefab);
            try
            {
                KingdomBuildingLevelModel model =
                    instance.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(model.ApplyConfirmedLevel(6), Is.True);
                Transform target =
                    instance.transform.Find("LOD0/L06_Delta");
                Transform settled =
                    instance.transform.Find("LOD0/L05_Delta");
                Vector3 targetPosition = target.localPosition;
                Vector3 settledPosition = settled.localPosition;
                var transition =
                    instance.AddComponent<
                        KingdomBuildingConfirmedLevelTransition>();

                Assert.That(
                    transition.Configure(
                        model,
                        entry.RealmMotionProfile,
                        6,
                        false),
                    Is.True);
                Assert.That(transition.AnimatedObjectCount, Is.EqualTo(4));
                Assert.That(target.localPosition, Is.Not.EqualTo(targetPosition));
                Assert.That(settled.localPosition, Is.EqualTo(settledPosition));

                transition.Evaluate(1f);
                Assert.That(target.localPosition, Is.EqualTo(targetPosition));
                Assert.That(settled.localPosition, Is.EqualTo(settledPosition));
                Assert.That(transition.IsAnimating, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ReducedMotionSettlesImmediatelyWithoutAnimation()
        {
            KingdomBuildingModelEntry entry = Entry(RealmId.Eldergrove);
            GameObject instance = Object.Instantiate(entry.Prefab);
            try
            {
                KingdomBuildingLevelModel model =
                    instance.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(model.ApplyConfirmedLevel(7), Is.True);
                Transform target =
                    instance.transform.Find("LOD0/L07_Delta");
                Vector3 targetPosition = target.localPosition;
                var transition =
                    instance.AddComponent<
                        KingdomBuildingConfirmedLevelTransition>();

                Assert.That(
                    transition.Configure(
                        model,
                        entry.RealmMotionProfile,
                        7,
                        true),
                    Is.True);
                Assert.That(transition.ReducedMotion, Is.True);
                Assert.That(transition.IsAnimating, Is.False);
                Assert.That(target.localPosition, Is.EqualTo(targetPosition));
                transition.Evaluate(0.5f);
                Assert.That(target.localPosition, Is.EqualTo(targetPosition));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LiveLayoutAnimatesOnceAfterConfirmedAdjacentLevel()
        {
            var engineObject = new GameObject("ConfirmedLevel.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());

                engine.AutoPlaceBuildings(
                    RealmId.Crownlands,
                    new[] { ResolveWorkshop(RealmId.Crownlands, 5) });
                Assert.That(
                    RequireProduction().GetComponent<
                        KingdomBuildingConfirmedLevelTransition>(),
                    Is.Null);

                engine.AutoPlaceBuildings(
                    RealmId.Crownlands,
                    new[] { ResolveWorkshop(RealmId.Crownlands, 6) });
                KingdomBuildingConfirmedLevelTransition transition =
                    RequireProduction().GetComponent<
                        KingdomBuildingConfirmedLevelTransition>();
                Assert.That(transition, Is.Not.Null);
                Assert.That(transition.ConfirmedLevel, Is.EqualTo(6));
                Assert.That(transition.IsAnimating, Is.True);

                engine.AutoPlaceBuildings(
                    RealmId.Crownlands,
                    new[] { ResolveWorkshop(RealmId.Crownlands, 6) });
                Assert.That(
                    RequireProduction().GetComponent<
                        KingdomBuildingConfirmedLevelTransition>(),
                    Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(engineObject);
            }
        }

        [Test]
        public void NewLayoutEngineLoadsConfirmedLevelSettled()
        {
            var engineObject = new GameObject("FreshLoad.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                engine.AutoPlaceBuildings(
                    RealmId.Stonehold,
                    new[] { ResolveWorkshop(RealmId.Stonehold, 8) });

                Assert.That(
                    RequireProduction().GetComponent<
                        KingdomBuildingConfirmedLevelTransition>(),
                    Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(engineObject);
            }
        }

        private static KingdomBuildingModelEntry Entry(RealmId realmId)
        {
            return LoadCatalog().Entries.Single(
                candidate =>
                    candidate.RealmId == realmId &&
                    candidate.BuildingId == "Workshop");
        }

        private static KingdomBuildingModelCatalog LoadCatalog()
        {
            KingdomBuildingModelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    KingdomBuildingModelCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        private static KingdomBuildingPresentation ResolveWorkshop(
            RealmId realmId,
            int level)
        {
            var state = new BuildingState
            {
                BuildingId = "Workshop",
                Level = level,
                IsUpgrading = false,
                UpgradeCompleteTimestamp = 0
            };
            return KingdomBuildingPresentationResolver
                .Resolve(realmId, new[] { state })
                .Single(item => item.BuildingId == "Workshop");
        }

        private static GameObject RequireProduction()
        {
            GameObject board = GameObject.Find("Kingdom_CityBoard");
            Assert.That(board, Is.Not.Null);
            Transform building = board.transform.Find(
                "Building_kingdom.slot.workshop");
            Assert.That(building, Is.Not.Null);
            Transform production = building.Find("ProductionModel");
            Assert.That(production, Is.Not.Null);
            return production.gameObject;
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }
    }
}
