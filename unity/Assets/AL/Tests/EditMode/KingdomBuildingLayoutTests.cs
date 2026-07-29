using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Data.Runtime;
using AL.Kingdom;
using AL.Kingdom.Visuals.Architecture;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class KingdomBuildingLayoutTests
    {
        private static readonly RealmId[] PlayableRealms =
        {
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Crownlands,
            RealmId.Umbral
        };

        [TestCase(RealmId.Stonehold)]
        [TestCase(RealmId.Eldergrove)]
        [TestCase(RealmId.Crownlands)]
        [TestCase(RealmId.Umbral)]
        public void PlayableRealmDeclaresUniqueStableSlotsAndCoordinates(RealmId realmId)
        {
            IReadOnlyList<KingdomBuildingSlotDefinition> slots =
                KingdomBuildingLayoutCatalog.GetSlots(realmId);

            Assert.That(slots, Has.Count.EqualTo(17));
            Assert.That(
                slots.Select(slot => slot.SlotId),
                Is.Unique,
                "Stable slot IDs must be unique within a realm layout.");
            Assert.That(
                slots.Select(slot => slot.BuildingId),
                Is.Unique,
                "A building definition may occupy only one fixed slot in this layout version.");
            Assert.That(
                slots.Select(slot => slot.GridPosition),
                Is.Unique,
                "Two stable slots must never occupy the same coordinate.");
            Assert.That(slots.All(slot => slot.RealmId == realmId), Is.True);
            Assert.That(
                slots.All(
                    slot =>
                        !string.IsNullOrWhiteSpace(slot.SlotId) &&
                        !string.IsNullOrWhiteSpace(slot.BuildingId) &&
                        slot.RotationQuarterTurns >= 0 &&
                        slot.RotationQuarterTurns <= 3),
                Is.True);
        }

        [Test]
        public void SlotIdentityAndBuildingBindingRemainStableAcrossRealms()
        {
            var baseline = KingdomBuildingLayoutCatalog
                .GetSlots(RealmId.Stonehold)
                .Select(slot => (slot.SlotId, slot.BuildingId))
                .ToArray();

            foreach (RealmId realmId in PlayableRealms.Skip(1))
            {
                var current = KingdomBuildingLayoutCatalog
                    .GetSlots(realmId)
                    .Select(slot => (slot.SlotId, slot.BuildingId))
                    .ToArray();
                CollectionAssert.AreEqual(baseline, current);
            }
        }

        [TestCase(RealmId.Stonehold)]
        [TestCase(RealmId.Eldergrove)]
        [TestCase(RealmId.Crownlands)]
        [TestCase(RealmId.Umbral)]
        public void TownHallOwnsStableCentralHeroSlot(RealmId realmId)
        {
            KingdomBuildingSlotDefinition townHall =
                KingdomBuildingLayoutCatalog
                    .GetSlots(realmId)
                    .Single(slot => slot.BuildingId == "TownHall");

            Assert.That(
                townHall.SlotId,
                Is.EqualTo("kingdom.slot.town-hall"));
            Assert.That(townHall.GridPosition, Is.EqualTo(Vector2Int.zero));
            Assert.That(townHall.RotationQuarterTurns, Is.Zero);
        }

        [Test]
        public void ResolutionUsesStableSlotsInsteadOfSaveListOrder()
        {
            var ordered = new List<BuildingState>
            {
                State("TownHall", 1),
                State("Farm", 3),
                State("Barracks", 2)
            };
            var reversed = ordered.AsEnumerable().Reverse().ToList();

            IReadOnlyList<KingdomBuildingPresentation> first =
                KingdomBuildingPresentationResolver.Resolve(
                    RealmId.Stonehold,
                    ordered);
            IReadOnlyList<KingdomBuildingPresentation> second =
                KingdomBuildingPresentationResolver.Resolve(
                    RealmId.Stonehold,
                    reversed);

            Assert.That(second, Has.Count.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Slot.SlotId, second[i].Slot.SlotId);
                Assert.AreEqual(first[i].Slot.GridPosition, second[i].Slot.GridPosition);
                Assert.AreEqual(first[i].BuildingId, second[i].BuildingId);
                Assert.AreEqual(first[i].ConfirmedLevel, second[i].ConfirmedLevel);
                Assert.AreEqual(first[i].Status, second[i].Status);
            }
        }

        [Test]
        public void MissingBuildingResolvesToUnbuiltWithoutCreatingSaveState()
        {
            var saveRows = new List<BuildingState>
            {
                State("TownHall", 1)
            };

            IReadOnlyList<KingdomBuildingPresentation> presentations =
                KingdomBuildingPresentationResolver.Resolve(
                    RealmId.Crownlands,
                    saveRows);

            Assert.That(saveRows, Has.Count.EqualTo(1));
            Assert.AreSame(saveRows[0], saveRows.Single());

            KingdomBuildingPresentation townHall =
                presentations.Single(item => item.BuildingId == "TownHall");
            Assert.AreEqual(KingdomBuildingPresentationStatus.Built, townHall.Status);
            Assert.AreEqual(1, townHall.ConfirmedLevel);

            KingdomBuildingPresentation farm =
                presentations.Single(item => item.BuildingId == "Farm");
            Assert.AreEqual(KingdomBuildingPresentationStatus.Unbuilt, farm.Status);
            Assert.AreEqual(0, farm.ConfirmedLevel);
            Assert.False(farm.IsUpgrading);
            Assert.That(farm.DiagnosticCode, Is.Empty);
        }

        [Test]
        public void DuplicateBuildingStateFailsVisiblyWithoutChoosingARecord()
        {
            var saveRows = new[]
            {
                State("Farm", 2),
                State("Farm", 7)
            };

            KingdomBuildingPresentation farm =
                KingdomBuildingPresentationResolver
                    .Resolve(RealmId.Eldergrove, saveRows)
                    .Single(item => item.BuildingId == "Farm");

            Assert.AreEqual(KingdomBuildingPresentationStatus.InvalidState, farm.Status);
            Assert.AreEqual(0, farm.ConfirmedLevel);
            Assert.AreEqual(
                KingdomBuildingPresentationResolver.DuplicateStateDiagnostic,
                farm.DiagnosticCode);
        }

        [TestCase(-1)]
        [TestCase(11)]
        public void OutOfRangeLevelFailsVisibly(int level)
        {
            KingdomBuildingPresentation workshop =
                KingdomBuildingPresentationResolver
                    .Resolve(
                        RealmId.Umbral,
                        new[] { State("Workshop", level) })
                    .Single(item => item.BuildingId == "Workshop");

            Assert.AreEqual(
                KingdomBuildingPresentationStatus.InvalidState,
                workshop.Status);
            Assert.AreEqual(
                KingdomBuildingPresentationResolver.InvalidLevelDiagnostic,
                workshop.DiagnosticCode);
        }

        [Test]
        public void ContradictoryUpgradeTimerFailsVisibly()
        {
            BuildingState farm = State("Farm", 1);
            farm.IsUpgrading = true;
            farm.UpgradeCompleteTimestamp = 0;

            KingdomBuildingPresentation presentation =
                KingdomBuildingPresentationResolver
                    .Resolve(RealmId.Stonehold, new[] { farm })
                    .Single(item => item.BuildingId == "Farm");

            Assert.AreEqual(
                KingdomBuildingPresentationStatus.InvalidState,
                presentation.Status);
            Assert.AreEqual(
                KingdomBuildingPresentationResolver.InvalidTimerDiagnostic,
                presentation.DiagnosticCode);
        }

        [Test]
        public void KingdomVisualizerNeverCallsStateSeedingBuildingGetter()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Kingdom",
                "Visuals",
                "KingdomVisualizer.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Not.Contain(".GetBuildingState("));
            Assert.That(
                source,
                Does.Contain("KingdomBuildingPresentationResolver.Resolve"));
        }

        [Test]
        public void DefaultCatalogRendersProductionTownHallAndRetainsStateMarkers()
        {
            DestroyIfPresent("Kingdom_CityBoard");
            var engineObject = new GameObject("KingdomBuildingLayoutTests.Engine");

            try
            {
                var engine = engineObject.AddComponent<CityLayoutEngine>();
                var states = new[]
                {
                    State("TownHall", 1),
                    State("Farm", 2),
                    State("Farm", 3)
                };
                IReadOnlyList<KingdomBuildingPresentation> presentations =
                    KingdomBuildingPresentationResolver.Resolve(
                        RealmId.Crownlands,
                        states);

                engine.AutoPlaceBuildings(RealmId.Crownlands, presentations);

                GameObject board = GameObject.Find("Kingdom_CityBoard");
                Assert.That(board, Is.Not.Null);

                KingdomBuildingPresentation townHall =
                    presentations.Single(item => item.BuildingId == "TownHall");
                Transform townHallRoot = AssertPresentationRoot(
                    engine,
                    board.transform,
                    townHall,
                    "ProductionModel",
                    "Lv 1");
                Assert.That(
                    townHallRoot
                        .Cast<Transform>()
                        .Count(child => child.name == "ProductionModel"),
                    Is.EqualTo(1));
                Assert.That(townHallRoot.Find("Base"), Is.Null);

                Transform productionModel =
                    townHallRoot.Find("ProductionModel");
                KingdomBuildingLevelModel levelModel =
                    productionModel.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(levelModel, Is.Not.Null);
                Assert.That(
                    levelModel.AppliedLevel,
                    Is.EqualTo(townHall.ConfirmedLevel));

                KingdomBuildingSelectable selectable =
                    productionModel.GetComponent<KingdomBuildingSelectable>();
                Assert.That(selectable, Is.Not.Null);
                Assert.That(levelModel.SelectionCollider, Is.Not.Null);
                Assert.That(levelModel.SelectionCollider.enabled, Is.True);

                AssertPresentationRoot(
                    engine,
                    board.transform,
                    presentations.Single(item => item.BuildingId == "Barracks"),
                    "ReservedSiteMarker",
                    "UNBUILT");
                AssertPresentationRoot(
                    engine,
                    board.transform,
                    presentations.Single(item => item.BuildingId == "Farm"),
                    "InvalidSiteMarker",
                    "DATA!");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
                DestroyIfPresent("Kingdom_CityBoard");
            }
        }

        [Test]
        public void NullModelCatalogRendersBuiltTownHallFallback()
        {
            DestroyIfPresent("Kingdom_CityBoard");
            var engineObject = new GameObject("KingdomBuildingLayoutTests.FallbackEngine");

            try
            {
                var engine = engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(null);
                KingdomBuildingPresentation townHall =
                    KingdomBuildingPresentationResolver
                        .Resolve(
                            RealmId.Crownlands,
                            new[] { State("TownHall", 4) })
                        .Single(item => item.BuildingId == "TownHall");

                engine.AutoPlaceBuildings(
                    RealmId.Crownlands,
                    new[] { townHall });

                GameObject board = GameObject.Find("Kingdom_CityBoard");
                Assert.That(board, Is.Not.Null);
                Transform root = AssertPresentationRoot(
                    engine,
                    board.transform,
                    townHall,
                    "Base",
                    "Lv 4");
                Assert.That(root.Find("ProductionModel"), Is.Null);
                Assert.That(
                    root
                        .Cast<Transform>()
                        .Count(child => child.name == "ProductionModel"),
                    Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
                DestroyIfPresent("Kingdom_CityBoard");
            }
        }

        [Test]
        public void RebuildingProductionTownHallTwiceDoesNotDuplicateRootsOrModels()
        {
            DestroyIfPresent("Kingdom_CityBoard");
            var engineObject = new GameObject("KingdomBuildingLayoutTests.RebuildEngine");

            try
            {
                var engine = engineObject.AddComponent<CityLayoutEngine>();
                KingdomBuildingPresentation townHall =
                    KingdomBuildingPresentationResolver
                        .Resolve(
                            RealmId.Crownlands,
                            new[] { State("TownHall", 3) })
                        .Single(item => item.BuildingId == "TownHall");
                Transform previousRoot = null;
                GameObject previousModel = null;

                for (int rebuild = 0; rebuild < 2; rebuild++)
                {
                    engine.AutoPlaceBuildings(
                        RealmId.Crownlands,
                        new[] { townHall });

                    GameObject board = GameObject.Find("Kingdom_CityBoard");
                    Assert.That(board, Is.Not.Null);
                    if (rebuild > 0)
                    {
                        Assert.That(
                            previousRoot == null,
                            Is.True,
                            "Refresh must destroy the previous building root.");
                        Assert.That(
                            previousModel == null,
                            Is.True,
                            "Refresh must destroy the previous production model.");
                    }

                    Assert.That(
                        board.transform
                            .Cast<Transform>()
                            .Count(
                                child =>
                                    child.name ==
                                    "Building_" + townHall.Slot.SlotId),
                        Is.EqualTo(1));

                    Transform root = AssertPresentationRoot(
                        engine,
                        board.transform,
                        townHall,
                        "ProductionModel",
                        "Lv 3");
                    Assert.That(
                        root
                            .Cast<Transform>()
                            .Count(child => child.name == "ProductionModel"),
                        Is.EqualTo(1));
                    Assert.That(
                        root.Find("ProductionModel")
                            .GetComponent<KingdomBuildingLevelModel>()
                            .AppliedLevel,
                        Is.EqualTo(townHall.ConfirmedLevel));
                    previousRoot = root;
                    previousModel = root.Find("ProductionModel").gameObject;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
                DestroyIfPresent("Kingdom_CityBoard");
            }
        }

        private static Transform AssertPresentationRoot(
            CityLayoutEngine engine,
            Transform board,
            KingdomBuildingPresentation presentation,
            string requiredChild,
            string requiredLabel)
        {
            Transform root = board.Find("Building_" + presentation.Slot.SlotId);
            Assert.That(root, Is.Not.Null, presentation.Slot.SlotId);
            Transform visual = root.Find(requiredChild);
            Assert.That(visual, Is.Not.Null, requiredChild);
            Assert.AreEqual(
                engine.GridToWorld(presentation.Slot.GridPosition),
                root.position);
            Assert.That(
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        root.eulerAngles.y,
                        presentation.Slot.RotationQuarterTurns * 90f)),
                Is.LessThan(0.01f));

            TextMesh label = root.GetComponentInChildren<TextMesh>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Does.Contain(requiredLabel));

            var selectable =
                visual.GetComponent<KingdomBuildingSelectable>();
            Assert.That(
                selectable,
                Is.Not.Null,
                $"{requiredChild} must expose the stable selection contract.");
            return root;
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        private static BuildingState State(string buildingId, int level)
        {
            return new BuildingState
            {
                BuildingId = buildingId,
                Level = level,
                IsUpgrading = false,
                UpgradeCompleteTimestamp = 0
            };
        }
    }
}
