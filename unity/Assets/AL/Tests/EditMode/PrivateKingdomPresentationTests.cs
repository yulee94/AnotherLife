using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Data.Runtime;
using AL.Kingdom;
using AL.Kingdom.Visuals;
using AL.Kingdom.Visuals.Architecture;
using AL.UI.Kingdom;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class PrivateKingdomPresentationTests
    {
        private const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset";

        private static readonly RealmId[] Realms =
        {
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Crownlands,
            RealmId.Umbral
        };

        [TearDown]
        public void TearDown()
        {
            DestroyIfPresent(PrivateKingdomCityPresenter.RootName);
            DestroyIfPresent("PrivateKingdomPresentationTests.Host");
        }

        [Test]
        public void EveryRealmHasDistinctProductionTownHallAndWorkshopFamilies()
        {
            KingdomBuildingModelCatalog catalog = LoadCatalog();
            Assert.That(catalog.Validate(out string diagnostic), Is.True, diagnostic);

            string[] hallIds = Realms.Select(realm => Entry(catalog, realm, "TownHall").ModelId).ToArray();
            string[] workshopIds = Realms.Select(realm => Entry(catalog, realm, "Workshop").ModelId).ToArray();

            Assert.That(hallIds, Is.Unique);
            Assert.That(workshopIds, Is.Unique);
            Assert.That(
                Realms.All(realm => Entry(catalog, realm, "TownHall").Prefab != null),
                Is.True);
            Assert.That(
                Realms.All(realm => Entry(catalog, realm, "Workshop").Prefab != null),
                Is.True);
        }

        [TestCase(RealmId.Stonehold)]
        [TestCase(RealmId.Eldergrove)]
        [TestCase(RealmId.Crownlands)]
        [TestCase(RealmId.Umbral)]
        public void PrivateCityBuildsDenseRealmArchitectureWithoutStrategicBoardObjects(
            RealmId realm)
        {
            var host = new GameObject("PrivateKingdomPresentationTests.Host");
            var presenter = host.AddComponent<PrivateKingdomCityPresenter>();
            SetField(presenter, "_modelCatalog", LoadCatalog());

            InvokeRebuild(presenter, realm, null);

            Transform city = host.transform.Find(PrivateKingdomCityPresenter.RootName);
            Assert.That(city, Is.Not.Null);
            Assert.That(
                presenter.ArchitectureInstanceCount,
                Is.EqualTo(PrivateKingdomCityPresenter.SetDressingBuildingCount + 1));
            Assert.That(
                city.Find(PrivateKingdomCityPresenter.GroundRootName)
                    .GetComponentsInChildren<MeshRenderer>(true),
                Is.Not.Empty);
            Assert.That(
                city.Find(PrivateKingdomCityPresenter.ArchitectureRootName)
                    .GetComponentsInChildren<KingdomBuildingLevelModel>(true),
                Has.Length.EqualTo(PrivateKingdomCityPresenter.SetDressingBuildingCount + 1));

            string[] names = city.GetComponentsInChildren<Transform>(true)
                .Select(item => item.name.ToLowerInvariant())
                .ToArray();
            foreach (string forbidden in new[]
                     {
                         "warzone", "territory", "outpost", "outerrealm",
                         "tacticalgrid", "placeholder", "reservedsite"
                     })
            {
                Assert.That(names.All(name => !name.Contains(forbidden)), Is.True, forbidden);
            }

            Assert.That(presenter.TownHallConstructed, Is.False);
            Assert.That(
                names.Any(name => name.Contains("townhall_constructionpreview")),
                Is.True);
        }

        [TestCase(RealmId.Stonehold)]
        [TestCase(RealmId.Eldergrove)]
        [TestCase(RealmId.Crownlands)]
        [TestCase(RealmId.Umbral)]
        public void ConfirmedOneBuildReplacesPreviewWithBuiltTownHallAndBeacon(
            RealmId realm)
        {
            var host = new GameObject("PrivateKingdomPresentationTests.Host");
            var presenter = host.AddComponent<PrivateKingdomCityPresenter>();
            SetField(presenter, "_modelCatalog", LoadCatalog());
            KingdomBuildingPresentation townHall =
                KingdomBuildingPresentationResolver.Resolve(
                        realm,
                        new[]
                        {
                            new BuildingState
                            {
                                BuildingId = "TownHall",
                                Level = 1,
                                IsUpgrading = false,
                                UpgradeCompleteTimestamp = 0
                            }
                        })
                    .Single(item => item.BuildingId == "TownHall");

            InvokeRebuild(presenter, realm, townHall);

            Transform hall = host.transform
                .Find(PrivateKingdomCityPresenter.RootName)
                .Find(PrivateKingdomCityPresenter.ArchitectureRootName)
                .Find(PrivateKingdomCityPresenter.TownHallRootName);
            Assert.That(presenter.TownHallConstructed, Is.True);
            Assert.That(hall, Is.Not.Null);
            Assert.That(hall.Find("PrivateKingdom_TownHallBeacon"), Is.Not.Null);
            Assert.That(
                hall.GetComponent<KingdomBuildingLevelModel>().AppliedLevel,
                Is.EqualTo(1));
        }

        [Test]
        public void PresenterRestoresSceneAmbientLightingWhenRemoved()
        {
            UnityEngine.Rendering.AmbientMode originalMode = RenderSettings.ambientMode;
            Color originalLight = RenderSettings.ambientLight;
            var host = new GameObject("PrivateKingdomPresentationTests.Host");
            var presenter = host.AddComponent<PrivateKingdomCityPresenter>();
            SetField(presenter, "_modelCatalog", LoadCatalog());
            InvokeRebuild(presenter, RealmId.Stonehold, null);

            UnityEngine.Object.DestroyImmediate(host);

            Assert.That(RenderSettings.ambientMode, Is.EqualTo(originalMode));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(originalLight));
        }

        [Test]
        public void ProductionPresenterUsesNoPrimitiveOrStrategicTerritoryConstruction()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Kingdom",
                "Visuals",
                "PrivateKingdomCityPresenter.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("GameObject.CreatePrimitive"));
            Assert.That(source, Does.Not.Contain("PrimitiveType"));
            Assert.That(source, Does.Not.Contain("ITerritoryService"));
            Assert.That(source, Does.Not.Contain("IWarzone"));
            Assert.That(source, Does.Contain("KingdomBuildingPresentationResolver.Resolve"));
            Assert.That(source, Does.Contain("KingdomBuildingModelCatalog.LoadDefault"));
        }

        [Test]
        public void ProductHudExposesConstructionMapAndSharedMenuWithoutCommandDeck()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "UI",
                "Kingdom",
                "KingdomSceneController.cs");
            string source = File.ReadAllText(path);
            string startToken = "private void BuildPrivateKingdomRuntimeUi()";
            string endToken = "private void RefreshDistrictsPanel";
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            string productHud = source.Substring(start, end - start);

            Assert.That(productHud, Does.Contain("ConstructionDock"));
            Assert.That(productHud, Does.Contain("PrivateKingdomTimerStrip"));
            Assert.That(productHud, Does.Contain("PrivateKingdomTimerText"));
            Assert.That(productHud, Does.Not.Contain("OWNER-ONLY CASTLE DOMAIN"));
            Assert.That(source, Does.Contain("PrivateKingdomMapPreview"));
            Assert.That(source, Does.Contain("PrivateKingdomDock"));
            Assert.That(productHud, Does.Not.Contain("CommandDeck"));
            Assert.That(productHud, Does.Not.Contain("Duel"));
            Assert.That(productHud, Does.Not.Contain("DemoInitializer"));
            Assert.That(source, Does.Contain("keyboard.bKey.wasPressedThisFrame"));
            Assert.That(source, Does.Contain("PrivateKingdomInnerDestinations"));
            Assert.That(source, Does.Contain("SharedMenuModeSwitchHost.EnsureForScene"));
            Assert.That(productHud, Does.Not.Contain("+ destinations[0]"));
            Assert.That(productHud, Does.Not.Contain("+ destinations[1]"));
            Assert.That(productHud, Does.Not.Contain("+ destinations[2]"));
            Assert.That(productHud, Does.Contain("realmName + \" Castle\\n\""));
        }

        [Test]
        public void TimerStripAlwaysShowsReadyActiveOrCompleteState()
        {
            Assert.That(
                PrivateKingdomHudTimer.Format(Array.Empty<BuildingState>(), 1000),
                Is.EqualTo("BUILD TIMER\nREADY"));
            Assert.That(
                PrivateKingdomHudTimer.Format(
                    new[]
                    {
                        new BuildingState
                        {
                            BuildingId = "TownHall",
                            IsUpgrading = true,
                            UpgradeCompleteTimestamp = 1125
                        }
                    },
                    1000),
                Is.EqualTo("TOWN HALL TIMER\n02:05"));
            Assert.That(
                PrivateKingdomHudTimer.Format(
                    new[]
                    {
                        new BuildingState
                        {
                            BuildingId = "TownHall",
                            IsUpgrading = true,
                            UpgradeCompleteTimestamp = 999
                        }
                    },
                    1000),
                Is.EqualTo("BUILD TIMER\nCOMPLETE"));
        }

        private static KingdomBuildingModelCatalog LoadCatalog()
        {
            KingdomBuildingModelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<KingdomBuildingModelCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        private static KingdomBuildingModelEntry Entry(
            KingdomBuildingModelCatalog catalog,
            RealmId realm,
            string buildingId)
        {
            Assert.That(catalog.TryGetEntry(realm, buildingId, out KingdomBuildingModelEntry entry), Is.True);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.IsConfigured, Is.True);
            return entry;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void InvokeRebuild(
            PrivateKingdomCityPresenter presenter,
            RealmId realm,
            KingdomBuildingPresentation townHall)
        {
            MethodInfo method = typeof(PrivateKingdomCityPresenter).GetMethod(
                "Rebuild",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, new object[] { realm, townHall });
        }

        private static void DestroyIfPresent(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
