using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.MapDisclosure;
using AL.Data.Catalogs.WorldAtlas;
using AL.UI.DesignSystem;
using AL.UI.WorldMap;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.WorldMap
{
    public sealed class ProgressiveMapInterfaceTests
    {
        [Test]
        public void AwaitingAuthorityAndReconnectExposeNoCatalogContent()
        {
            MapDisclosureCatalogSnapshot catalog = LoadCatalog();
            var controller = new ProgressiveMapController(catalog);

            Assert.That(controller.Current.AuthorityState, Is.EqualTo(MapAuthorityPresentationState.Awaiting));
            Assert.That(controller.Current.Minimap.Items, Is.Empty);
            Assert.That(controller.Current.WorldMap.Items, Is.Empty);

            controller.ApplyAuthoritative(Snapshot(
                catalog,
                1,
                1,
                new[] { "feature_inner_crownlands" }));
            Assert.That(controller.Current.WorldMap.ItemIds, Does.Contain("feature_inner_crownlands"));

            controller.BeginReconnect();

            Assert.That(controller.Current.AuthorityState, Is.EqualTo(MapAuthorityPresentationState.Awaiting));
            Assert.That(controller.Current.RequiresRefresh, Is.True);
            Assert.That(controller.Current.Minimap.Items, Is.Empty);
            Assert.That(controller.Current.WorldMap.Items, Is.Empty);
        }

        [Test]
        public void CatalogRelationshipsPreventRouteObjectiveAndAllegianceLeaks()
        {
            MapDisclosureCatalogSnapshot catalog = LoadCatalog();
            var controller = new ProgressiveMapController(catalog);

            controller.ApplyAuthoritative(Snapshot(
                catalog,
                2,
                1,
                new[] { "feature_inner_crownlands" },
                new[] { "route_crownlands_to_accordant" },
                new[] { "map_objective_crossroads_control" },
                new[] { "allegiance_crownlands" }));

            Assert.That(controller.Current.WorldMap.ItemIds, Does.Contain("feature_inner_crownlands"));
            Assert.That(controller.Current.WorldMap.ItemIds, Does.Contain("allegiance_crownlands"));
            Assert.That(controller.Current.WorldMap.ItemIds, Does.Not.Contain("route_crownlands_to_accordant"));
            Assert.That(controller.Current.WorldMap.ItemIds, Does.Not.Contain("map_objective_crossroads_control"));

            controller.ApplyAuthoritative(Snapshot(
                catalog,
                2,
                2,
                new[]
                {
                    "feature_inner_crownlands",
                    "feature_warzone_crownlands_gate",
                    "feature_crossroads_bridges"
                },
                new[] { "route_crownlands_to_accordant" },
                new[] { "map_objective_crossroads_control" },
                new[] { "allegiance_crownlands" }));

            Assert.That(controller.Current.WorldMap.ItemIds, Does.Contain("route_crownlands_to_accordant"));
            Assert.That(controller.Current.WorldMap.ItemIds, Does.Contain("map_objective_crossroads_control"));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Contain("map_objective_crossroads_control"));
        }

        [Test]
        public void MinimapAndWorldMapUseOneProjectionAndHonorCatalogSurfaceRules()
        {
            MapDisclosureCatalogSnapshot catalog = LoadCatalog();
            var controller = new ProgressiveMapController(catalog);
            controller.ApplyAuthoritative(Snapshot(
                catalog,
                3,
                4,
                new[]
                {
                    "feature_inner_stonehold",
                    "feature_warzone_stonehold_gate",
                    "feature_crossroads_bridges"
                },
                objectives: new[] { "map_objective_crossroads_control" },
                allegiances: new[] { "allegiance_stonehold" }));

            MapSurfaceProjection minimap = controller.Current.Minimap;
            MapSurfaceProjection worldMap = controller.Current.WorldMap;
            string[] common = minimap.ItemIds.Intersect(worldMap.ItemIds).OrderBy(value => value).ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "feature_crossroads_bridges",
                    "feature_inner_stonehold",
                    "feature_warzone_stonehold_gate",
                    "map_objective_crossroads_control",
                    "allegiance_stonehold"
                },
                common);
            Assert.That(minimap.ItemIds, Does.Not.Contain("feature_accordant_isle"));
            Assert.That(worldMap.ItemIds, Does.Contain("feature_accordant_isle"));
            Assert.That(
                worldMap.Items.Single(item => item.Id == "allegiance_stonehold").NonColorShape,
                Is.EqualTo("anvil"));
            Assert.That(
                worldMap.Items.Single(item => item.Id == "allegiance_stonehold").AssetReference,
                Is.EqualTo("ui.realm_glyph.stonehold"));
        }

        [Test]
        public void NewerAuthorityCorrectionAndDelayedIndicatorUpdatesConvergeFailClosed()
        {
            MapDisclosureCatalogSnapshot catalog = LoadCatalog();
            var controller = new ProgressiveMapController(catalog);
            controller.ApplyAuthoritative(Snapshot(
                catalog,
                5,
                10,
                new[] { "feature_inner_crownlands" }));

            Assert.That(controller.ApplyIndicators(new MapIndicatorAuthoritySnapshot(
                5,
                10,
                new MapIndicator("player", "feature_inner_crownlands", "YOU", "chevron", new Vector2(0.4f, 0.5f)),
                new[]
                {
                    new MapIndicator("party_1", "feature_inner_crownlands", "PARTY 1", "diamond", new Vector2(0.45f, 0.55f))
                })), Is.True);
            Assert.That(controller.Current.Minimap.ItemIds, Does.Contain("player"));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Contain("party_1"));

            controller.ApplyAuthoritative(Snapshot(
                catalog,
                5,
                11,
                new[] { "feature_inner_stonehold" }));

            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("feature_inner_crownlands"));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("player"));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("party_1"));
            Assert.That(controller.ApplyIndicators(new MapIndicatorAuthoritySnapshot(
                5,
                10,
                new MapIndicator("late_player", "feature_inner_crownlands", "YOU", "chevron", new Vector2(0.4f, 0.5f)),
                Array.Empty<MapIndicator>())), Is.False);
            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("late_player"));

            MapDisclosureReconcileDisposition disposition = controller.ApplyAuthoritative(Snapshot(
                catalog,
                5,
                9,
                new[] { "feature_inner_crownlands" }));
            Assert.That(disposition, Is.EqualTo(MapDisclosureReconcileDisposition.StaleIgnored));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Contain("feature_inner_stonehold"));
        }

        [Test]
        public void CorrectingProjectionClearsDisclosureAndIndicatorsFailClosed()
        {
            MapDisclosureCatalogSnapshot catalog = LoadCatalog();
            var controller = new ProgressiveMapController(catalog);
            controller.ApplyAuthoritative(Snapshot(
                catalog,
                6,
                1,
                new[] { "feature_inner_crownlands" }));
            controller.ApplyIndicators(new MapIndicatorAuthoritySnapshot(
                6,
                1,
                new MapIndicator(
                    "player",
                    "feature_inner_crownlands",
                    "YOU",
                    "chevron",
                    Vector2.one * 0.5f),
                Array.Empty<MapIndicator>()));

            MapDisclosureReconcileDisposition disposition =
                controller.ApplyAuthoritative(Snapshot(
                    catalog,
                    6,
                    1,
                    new[] { "feature_inner_stonehold" }));

            Assert.That(disposition, Is.EqualTo(MapDisclosureReconcileDisposition.ConflictSuppressed));
            Assert.That(controller.Current.RequiresRefresh, Is.True);
            Assert.That(controller.Current.AuthorityState, Is.EqualTo(MapAuthorityPresentationState.Correcting));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("player"));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("feature_inner_crownlands"));
            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("feature_inner_stonehold"));
        }

        [TestCase(UiFormFactor.PhoneLandscape)]
        [TestCase(UiFormFactor.TabletLandscape)]
        [TestCase(UiFormFactor.Pc16By9)]
        [TestCase(UiFormFactor.PcUltrawide)]
        public void AuthoredLayoutKeepsMinimapTransitionsInsideSafeSideRail(UiFormFactor factor)
        {
            HudResponsiveCompositionSet compositions = HudResponsiveCompositionSet.LoadDefault();
            Assert.That(compositions.TryGet(factor, out HudCompositionDefinition composition), Is.True);
            Rect safeArea = HudLayoutProjection.ApplySafeAreaPadding(
                new Rect(96f, 48f, composition.ReferenceResolution.x - 192f, composition.ReferenceResolution.y - 96f),
                composition);

            MapSurfaceLayout standard = MapInterfaceLayout.Resolve(composition, safeArea, combatDense: false);
            MapSurfaceLayout combatDense = MapInterfaceLayout.Resolve(composition, safeArea, combatDense: true);

            Assert.That(safeArea.Contains(standard.MinimapRect.min), Is.True);
            Assert.That(safeArea.Contains(standard.MinimapRect.max), Is.True);
            Assert.That(safeArea.Contains(standard.ExpandedMinimapRect.min), Is.True);
            Assert.That(safeArea.Contains(standard.ExpandedMinimapRect.max), Is.True);
            Assert.That(standard.MinimapRect.Overlaps(standard.ProtectedScanRect), Is.False);
            Assert.That(standard.ExpandedMinimapRect.Overlaps(standard.ProtectedScanRect), Is.False);
            Assert.That(standard.ExpandedMinimapRect.height, Is.GreaterThan(standard.MinimapRect.height));
            Assert.That(combatDense.ExpandedMinimapRect, Is.EqualTo(combatDense.MinimapRect));
        }

        [Test]
        public void ZoomTransitionsChangeDetailWithoutChangingDisclosure()
        {
            MapDisclosureCatalogSnapshot catalog = LoadCatalog();
            var controller = new ProgressiveMapController(catalog);
            controller.ApplyAuthoritative(Snapshot(
                catalog,
                7,
                1,
                new[] { "feature_inner_umbral" }));
            string[] originalIds = controller.Current.WorldMap.ItemIds.ToArray();

            Assert.That(controller.SetZoom(0f), Is.EqualTo(MapDetailLevel.Local));
            Assert.That(controller.SetZoom(0.5f), Is.EqualTo(MapDetailLevel.Regional));
            Assert.That(controller.SetZoom(1f), Is.EqualTo(MapDetailLevel.Strategic));
            CollectionAssert.AreEqual(originalIds, controller.Current.WorldMap.ItemIds);
        }

        [Test]
        public void MalformedOrCollidingIndicatorsAreSuppressedFailClosed()
        {
            MapDisclosureCatalogSnapshot catalog = LoadCatalog();
            var controller = new ProgressiveMapController(catalog);
            controller.ApplyAuthoritative(Snapshot(
                catalog,
                8,
                2,
                new[] { "feature_inner_crownlands" }));

            Assert.That(controller.ApplyIndicators(new MapIndicatorAuthoritySnapshot(
                8,
                2,
                new MapIndicator(
                    "PLAYER!",
                    "feature_inner_crownlands",
                    new string('X', 100),
                    "chevron",
                    Vector2.one),
                new[]
                {
                    new MapIndicator(
                        "feature_inner_crownlands",
                        "feature_inner_crownlands",
                        "COLLISION",
                        "diamond",
                        Vector2.zero),
                    new MapIndicator(
                        "party_1",
                        "feature_inner_crownlands",
                        new string('P', 100),
                        "diamond",
                        Vector2.zero)
                })), Is.True);

            Assert.That(controller.Current.Minimap.ItemIds, Does.Not.Contain("PLAYER!"));
            Assert.That(
                controller.Current.Minimap.ItemIds.Count(value =>
                    value == "feature_inner_crownlands"),
                Is.EqualTo(1));
            Assert.That(
                controller.Current.Minimap.Items.Single(value => value.Id == "party_1").Label.Length,
                Is.EqualTo(48));
        }

        [TestCase(
            "jar:file:///data/app/base.apk!/assets",
            "jar:file:///data/app/base.apk!/assets/GameData/al_map_disclosure_catalog.json")]
        [TestCase(
            "https://cdn.example.invalid/game",
            "https://cdn.example.invalid/game/GameData/al_map_disclosure_catalog.json")]
        public void RuntimeDisclosureLocationPreservesStreamingAssetUri(
            string streamingAssetsRoot,
            string expected)
        {
            string resolved = WorldMapHost.ResolveCanonicalDisclosureLocation(
                dataPath: string.Empty,
                streamingAssetsRoot,
                isEditor: false);

            Assert.That(resolved, Is.EqualTo(expected));
            Assert.That(WorldMapHost.RequiresUriCatalogLoad(resolved), Is.True);
        }

        [Test]
        public void DisclosureFileReadFailureReturnsNullInsteadOfEscaping()
        {
            Assert.That(
                WorldMapHost.LoadDisclosureFromFile(
                    "unreadable-catalog.json",
                    _ => throw new IOException("denied")),
                Is.Null);
        }

        [Test]
        public void AccessibilityVariantsUseStaticMotionScaledTextAndHighContrast()
        {
            UiProductionDesignTokens tokens = UiProductionDesignTokens.LoadDefault();
            var profile = new MapAccessibilityProfile(
                new UiAccessibilitySettings(
                    textScale: 1.6f,
                    reducedMotion: true,
                    reducedFlash: true,
                    reducedVfx: true),
                highContrast: true);

            MapItemVisualTreatment treatment = MapInterfaceAccessibility.Resolve(
                tokens,
                MapDisplayItemKind.Allegiance,
                profile);

            Assert.That(treatment.TextScale, Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(treatment.Animate, Is.False);
            Assert.That(treatment.FlashOpacity, Is.LessThanOrEqualTo(0.08f));
            Assert.That(treatment.Color.a, Is.EqualTo(1f));
            Assert.That(
                Mathf.Max(treatment.Color.r, treatment.Color.g, treatment.Color.b),
                Is.GreaterThanOrEqualTo(0.9f));
        }

        [Test]
        public void RuntimeSurfacesRenderOnlyTheSharedAuthoritativeProjection()
        {
            WorldMapOverlay overlay = null;
            InnerRealmMinimapOverlay minimap = null;
            try
            {
                MapDisclosureCatalogSnapshot catalog = LoadCatalog();
                ProgressiveMapSession.Configure(catalog);
                WorldAtlasSnapshot atlas = LoadAtlas();
                overlay = WorldMapOverlay.Ensure(atlas);
                System.Reflection.MethodInfo activate = typeof(WorldMapOverlay).GetMethod(
                    "SetPresentationAuthority",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(activate, Is.Not.Null);
                activate.Invoke(overlay, new object[] { true });
                minimap = InnerRealmMinimapOverlay.Ensure(atlas);

                ProgressiveMapSession.ApplyAuthoritative(Snapshot(
                    catalog,
                    11,
                    3,
                    new[]
                    {
                        "feature_inner_stonehold",
                        "feature_warzone_stonehold_gate",
                        "feature_crossroads_bridges"
                    },
                    new[] { "route_stonehold_to_accordant" },
                    new[] { "map_objective_crossroads_control" },
                    new[] { "allegiance_stonehold" }));
                minimap.Bind(atlas);

                Assert.That(FindDeep(overlay.transform, "zone_inner_stonehold").activeSelf, Is.True);
                Assert.That(FindDeep(overlay.transform, "zone_inner_crownlands").activeSelf, Is.False);
                Assert.That(FindDeep(overlay.transform, "route_stonehold_to_accordant"), Is.Not.Null);
                Assert.That(FindDeep(overlay.transform, "map_objective_crossroads_control"), Is.Not.Null);
                Assert.That(minimap.VisibleMarkerIds, Does.Contain("map_objective_crossroads_control"));
                Assert.That(minimap.VisibleMarkerIds, Does.Not.Contain("feature_inner_crownlands"));

                RectTransform plate = FindDeep(minimap.transform, "MinimapPlate")
                    .GetComponent<RectTransform>();
                float compactHeight = plate.anchorMax.y - plate.anchorMin.y;
                ProgressiveMapSession.SetMinimapPresentation(expanded: true, combatDense: false);
                InvokeResponsiveLayout(minimap);
                float expandedHeight = plate.anchorMax.y - plate.anchorMin.y;
                Assert.That(expandedHeight, Is.GreaterThan(compactHeight));

                ProgressiveMapSession.SetMinimapPresentation(expanded: true, combatDense: true);
                InvokeResponsiveLayout(minimap);
                Assert.That(
                    plate.anchorMax.y - plate.anchorMin.y,
                    Is.EqualTo(compactHeight).Within(0.001f));

                ProgressiveMapSession.BeginReconnect();
                for (int i = 0; i < catalog.Features.Count; i++)
                {
                    MapDisclosureFeature feature = catalog.Features[i];
                    Assert.That(
                        FindDeep(overlay.transform, feature.Id),
                        Is.Null,
                        "Dynamic feature leaked while awaiting authority: " + feature.Id);
                    GameObject staticFeature = FindDeep(overlay.transform, feature.SourceId);
                    if (staticFeature != null)
                    {
                        Assert.That(
                            staticFeature.activeSelf,
                            Is.False,
                            "Static feature leaked while awaiting authority: " + feature.SourceId);
                    }
                }
            }
            finally
            {
                if (overlay != null)
                {
                    UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                }
                if (minimap != null)
                {
                    UnityEngine.Object.DestroyImmediate(minimap.gameObject);
                }
                ProgressiveMapSession.ResetForTests();
                WorldMapSession.ResetStatics();
            }
        }

        private static MapDisclosureCatalogSnapshot LoadCatalog()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_map_disclosure_catalog.json"));
            MapDisclosureLoadResult result = MapDisclosureCatalogLoader.Validate(bytes);
            Assert.That(result.IsAccepted, Is.True, string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            return result.Snapshot;
        }

        private static WorldAtlasSnapshot LoadAtlas()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);
            Assert.That(
                result.IsAccepted,
                Is.True,
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            return result.Snapshot;
        }

        private static GameObject FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root.gameObject;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void InvokeResponsiveLayout(InnerRealmMinimapOverlay minimap)
        {
            System.Reflection.MethodInfo method = typeof(InnerRealmMinimapOverlay).GetMethod(
                "ApplyResponsiveLayout",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(minimap, new object[] { true });
        }

        private static MapDisclosureAuthoritySnapshot Snapshot(
            MapDisclosureCatalogSnapshot catalog,
            long epoch,
            long revision,
            IEnumerable<string> features,
            IEnumerable<string> routes = null,
            IEnumerable<string> objectives = null,
            IEnumerable<string> allegiances = null)
        {
            return MapDisclosureAuthoritySnapshot.Create(
                catalog.Authority.SnapshotStateVersion,
                epoch,
                revision,
                catalog.Version,
                catalog.SourceSha256,
                features,
                routes ?? Array.Empty<string>(),
                objectives ?? Array.Empty<string>(),
                allegiances ?? Array.Empty<string>());
        }
    }
}
