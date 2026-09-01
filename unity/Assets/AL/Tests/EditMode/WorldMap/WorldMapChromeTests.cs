using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldAtlas;
using AL.UI.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.WorldMap
{
    public sealed class WorldMapChromeTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            AL.ChampionMode.UI.ChampionHudCameraGate.Reset();
            WorldMapSession.ResetStatics();
            ResetGameInput();
        }

        [TearDown]
        public void TearDown()
        {
            AL.ChampionMode.UI.ChampionHudCameraGate.Reset();
            WorldMapSession.ResetStatics();
            AL.Input.GameInput.SetGameplaySuppressed(false);
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    WorldMapOverlay overlay = _spawned[i].GetComponent<WorldMapOverlay>();
                    if (overlay != null)
                    {
                        InvokeLifecycle(overlay, "OnDestroy");
                    }
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            DestroyAll<WorldMapHost>();
            DestroyAll<WorldMapOverlay>();
            DestroyAll<InnerRealmMinimapOverlay>();
            _spawned.Clear();
            ResetGameInput();
        }

        [Test]
        public void PresentationShowsFourCornerInnersWithoutPlayableWarzone()
        {
            WorldMapPresentation presentation = LoadPresentation();

            Assert.That(presentation.TopologyId, Is.EqualTo("topology_launch_world_ring_v001"));
            Assert.That(presentation.AtlasPlacementResolved, Is.False);
            Assert.That(presentation.PlacementStatus, Is.EqualTo(WorldMapIds.PlacementProposalStatus));
            Assert.That(presentation.DrawsPlayableWarzone, Is.False);
            Assert.That(presentation.Inners.Count, Is.EqualTo(4));
            Assert.That(presentation.AccordantIsle.Id, Is.EqualTo(WorldMapIds.AccordantIsleZoneId));
            Assert.That(presentation.AccordantIsle.Label, Is.EqualTo(WorldMapIds.DisplayAccordantIsle));

            string[] expectedZones =
            {
                "zone_inner_stonehold",
                "zone_inner_eldergrove",
                "zone_inner_crownlands",
                "zone_inner_umbral"
            };
            CollectionAssert.AreEquivalent(expectedZones, presentation.Inners.Select(inner => inner.InnerAtlasZoneId).ToArray());

            WorldMapInnerRealm stonehold = presentation.Inners.First(inner => inner.RealmId == "stonehold");
            WorldMapInnerRealm eldergrove = presentation.Inners.First(inner => inner.RealmId == "eldergrove");
            WorldMapInnerRealm crownlands = presentation.Inners.First(inner => inner.RealmId == "crownlands");
            WorldMapInnerRealm umbral = presentation.Inners.First(inner => inner.RealmId == "umbral");

            Assert.That(stonehold.Capital.Uv.X, Is.LessThan(0.5f));
            Assert.That(stonehold.Capital.Uv.Y, Is.LessThan(0.5f));
            Assert.That(eldergrove.Capital.Uv.X, Is.LessThan(0.5f));
            Assert.That(eldergrove.Capital.Uv.Y, Is.GreaterThan(0.5f));
            Assert.That(crownlands.Capital.Uv.X, Is.GreaterThan(0.5f));
            Assert.That(crownlands.Capital.Uv.Y, Is.GreaterThan(0.5f));
            Assert.That(umbral.Capital.Uv.X, Is.GreaterThan(0.5f));
            Assert.That(umbral.Capital.Uv.Y, Is.LessThan(0.5f));

            foreach (WorldMapSettlement settlement in presentation.VisibleSettlements())
            {
                Assert.That(presentation.ContainsWarzoneDestination(settlement.Id), Is.False, settlement.Id);
                Assert.That(
                    new[] { WorldMapIds.DisplayCapital, WorldMapIds.DisplayOutpostA, WorldMapIds.DisplayOutpostB },
                    Does.Contain(settlement.Label));
            }
        }

        [Test]
        public void OverlayOpensAndClosesFromSession()
        {
            WorldAtlasSnapshot snapshot = LoadSnapshot();
            WorldMapOverlay overlay = EnsureStandaloneOverlay(snapshot);
            _spawned.Add(overlay.gameObject);

            GameObject mapVeil = FindDeep(overlay.transform, "WorldMap_Veil");
            Assert.That(mapVeil, Is.Not.Null);
            Assert.That(WorldMapSession.IsMapOpen, Is.False);
            Assert.That(mapVeil.activeSelf, Is.False);

            WorldMapSession.OpenMap();
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(mapVeil.activeSelf, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);
            Assert.That(
                AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen,
                Is.True);

            Assert.That(FindDeep(overlay.transform, "zone_inner_stonehold"), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, "wall_stonehold_inner"), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, WorldMapIds.CapitalPoiId("zone_inner_stonehold")), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, WorldMapIds.AccordantIsleZoneId), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, "zone_warzone_stonehold_gate"), Is.Null);
            Assert.That(FindDeep(overlay.transform, "bridge_ring_01_02_01"), Is.Null);

            string dump = DumpText(overlay.transform);
            Assert.That(dump, Does.Contain(WorldMapIds.DisplayCapital));
            Assert.That(dump, Does.Contain(WorldMapIds.DisplayOutpostA));
            Assert.That(dump, Does.Contain("Stonehold"));
            Assert.That(dump, Does.Not.Contain("Crownspire"));
            Assert.That(dump, Does.Not.Contain("Stormwright"));
            Assert.That(dump, Does.Not.Contain("KingdomSceneController"));

            WorldMapSession.CloseMap();
            Assert.That(WorldMapSession.IsMapOpen, Is.False);
            Assert.That(mapVeil.activeSelf, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
            Assert.That(
                AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen,
                Is.False);
        }

        [Test]
        public void DisablingOpenWorldMapHidesSurfaceBeforeReleasingViewOwnership()
        {
            WorldMapOverlay overlay = EnsureStandaloneOverlay(LoadSnapshot());
            _spawned.Add(overlay.gameObject);
            GameObject mapVeil = FindDeep(overlay.transform, "WorldMap_Veil");
            WorldMapSession.OpenMap();
            Assert.That(mapVeil.activeInHierarchy, Is.True);
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);

            overlay.enabled = false;
            InvokeLifecycle(overlay, "OnDisable");

            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(mapVeil.activeInHierarchy, Is.False);
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
        }

        [Test]
        public void DisablingWorldMapReleasesOnlyItsGameplaySuppressionOwnership()
        {
            System.Reflection.MethodInfo acquire = typeof(AL.Input.GameInput).GetMethod(
                "AcquireGameplaySuppression",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public);
            Assert.That(acquire, Is.Not.Null);
            var externalOwnership = (System.IDisposable)acquire.Invoke(
                null,
                new object[] { "external-modal" });
            try
            {
                WorldMapOverlay overlay = EnsureStandaloneOverlay(LoadSnapshot());
                _spawned.Add(overlay.gameObject);
                WorldMapSession.OpenMap();

                InvokeLifecycle(overlay, "OnDisable");

                Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);
            }
            finally
            {
                externalOwnership?.Dispose();
            }

            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
        }

        [Test]
        public void WorldMapSessionResetReleasesLiveOverlayOwnership()
        {
            WorldMapOverlay overlay = EnsureStandaloneOverlay(LoadSnapshot());
            _spawned.Add(overlay.gameObject);
            WorldMapSession.OpenMap();
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);
            Assert.That(
                AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen,
                Is.True);

            WorldMapSession.ResetStatics();

            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
            Assert.That(
                AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen,
                Is.False);
        }

        [Test]
        public void WorldMapSessionResetKeepsLiveOverlayConnectedForReopen()
        {
            WorldMapOverlay overlay = EnsureStandaloneOverlay(LoadSnapshot());
            _spawned.Add(overlay.gameObject);
            GameObject mapVeil = FindDeep(overlay.transform, "WorldMap_Veil");
            WorldMapSession.OpenMap();

            WorldMapSession.ResetStatics();
            WorldMapSession.OpenMap();

            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(mapVeil.activeSelf, Is.True);
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);
        }

        [Test]
        public void DestroyedWorldMapOverlayCannotReacquireOwnershipOnLaterOpen()
        {
            WorldMapOverlay overlay = EnsureStandaloneOverlay(LoadSnapshot());
            GameObject overlayRoot = overlay.gameObject;
            _spawned.Add(overlayRoot);
            WorldMapSession.OpenMap();
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);

            InvokeLifecycle(overlay, "OnDestroy");
            Object.DestroyImmediate(overlayRoot);
            WorldMapSession.CloseMap();
            WorldMapSession.OpenMap();

            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
        }

        [Test]
        public void ResetStaticsPrunesDestroyedOverlaySubscribersBeforeReopen()
        {
            WorldMapOverlay overlay = EnsureStandaloneOverlay(LoadSnapshot());
            GameObject overlayRoot = overlay.gameObject;
            _spawned.Add(overlayRoot);
            WorldMapSession.OpenMap();
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.True);

            Object.DestroyImmediate(overlayRoot);
            AL.ChampionMode.UI.ChampionHudCameraGate.Reset();
            ResetGameInput();
            WorldMapSession.ResetStatics();
            WorldMapSession.OpenMap();

            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
        }

        [Test]
        public void ReenablingIntentionallyOpenWorldMapReacquiresViewOwnership()
        {
            WorldMapOverlay overlay = EnsureStandaloneOverlay(LoadSnapshot());
            _spawned.Add(overlay.gameObject);
            WorldMapSession.OpenMap();
            InvokeLifecycle(overlay, "OnDisable");
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.False);

            EnsureStandaloneOverlay(LoadSnapshot());

            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);
        }

        [Test]
        public void DestroyingOldWorldMapHostPreservesReplacementOwnership()
        {
            var oldRoot = new GameObject("OldWorldMapHost");
            var replacementRoot = new GameObject("ReplacementWorldMapHost");
            _spawned.Add(oldRoot);
            _spawned.Add(replacementRoot);
            WorldMapHost oldHost = oldRoot.AddComponent<WorldMapHost>();
            WorldMapHost replacement = replacementRoot.AddComponent<WorldMapHost>();
            InvokeHostLifecycle(oldHost, "BindIfNeeded");
            InvokeHostLifecycle(replacement, "BindIfNeeded");
            WorldMapOverlay overlay = Object.FindObjectOfType<WorldMapOverlay>();
            InnerRealmMinimapOverlay minimap = Object.FindObjectOfType<InnerRealmMinimapOverlay>();
            if (overlay != null)
            {
                _spawned.Add(overlay.gameObject);
            }
            if (minimap != null)
            {
                _spawned.Add(minimap.gameObject);
            }

            WorldMapSession.OpenMap();
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);

            InvokeHostLifecycle(oldHost, "OnDestroy");

            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);

            InvokeHostLifecycle(replacement, "OnDestroy");

            Assert.That(WorldMapSession.IsMapOpen, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
        }

        [Test]
        public void DisablingAuthoritativeWorldMapHostPromotesOlderBoundHost()
        {
            var oldRoot = new GameObject("OlderBoundWorldMapHost");
            var replacementRoot = new GameObject("DisabledAuthoritativeWorldMapHost");
            _spawned.Add(oldRoot);
            _spawned.Add(replacementRoot);
            WorldMapHost oldHost = oldRoot.AddComponent<WorldMapHost>();
            WorldMapHost replacement = replacementRoot.AddComponent<WorldMapHost>();
            InvokeHostLifecycle(oldHost, "BindIfNeeded");
            InvokeHostLifecycle(replacement, "BindIfNeeded");
            WorldMapSession.OpenMap();

            replacement.enabled = false;
            InvokeHostLifecycle(replacement, "OnDisable");

            System.Reflection.FieldInfo authority = typeof(WorldMapHost).GetField(
                "_authoritativeHost",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(authority, Is.Not.Null);
            Assert.That(authority.GetValue(null), Is.SameAs(oldHost));
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);
        }

        [Test]
        public void BoundWorldMapHostRebuildsDestroyedSurfacesBeforeInput()
        {
            var hostRoot = new GameObject("SurfaceRecoveryWorldMapHost");
            _spawned.Add(hostRoot);
            WorldMapHost host = hostRoot.AddComponent<WorldMapHost>();
            InvokeHostLifecycle(host, "BindIfNeeded");
            WorldMapOverlay oldMap = Object.FindObjectOfType<WorldMapOverlay>();
            InnerRealmMinimapOverlay oldMinimap =
                Object.FindObjectOfType<InnerRealmMinimapOverlay>();
            Assert.That(oldMap, Is.Not.Null);
            Assert.That(oldMinimap, Is.Not.Null);
            InvokeLifecycle(oldMap, "OnDestroy");
            Object.DestroyImmediate(oldMap.gameObject);
            Object.DestroyImmediate(oldMinimap.gameObject);

            InvokeHostLifecycle(host, "Update");

            Assert.That(Object.FindObjectOfType<WorldMapOverlay>(), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<InnerRealmMinimapOverlay>(), Is.Not.Null);
        }

        [Test]
        public void BoundWorldMapHostRepairsDisabledSurfaceBeforeInput()
        {
            var hostRoot = new GameObject("DisabledSurfaceRecoveryWorldMapHost");
            _spawned.Add(hostRoot);
            WorldMapHost host = hostRoot.AddComponent<WorldMapHost>();
            InvokeHostLifecycle(host, "BindIfNeeded");
            WorldMapOverlay overlay = Object.FindObjectOfType<WorldMapOverlay>();
            Assert.That(overlay, Is.Not.Null);
            GameObject mapVeil = FindDeep(overlay.transform, "WorldMap_Veil");
            WorldMapSession.OpenMap();

            overlay.enabled = false;
            InvokeLifecycle(overlay, "OnDisable");
            Assert.That(mapVeil.activeInHierarchy, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);

            InvokeHostLifecycle(host, "Update");

            Assert.That(overlay.enabled, Is.True);
            Assert.That(mapVeil.activeInHierarchy, Is.True);
            Assert.That(AL.ChampionMode.UI.ChampionHudCameraGate.CursorModeOpen, Is.True);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);
        }

        [Test]
        public void HostCanonicalAtlasPathUsesEstablishedResolverBeforePackagedFallback()
        {
            System.Reflection.MethodInfo resolve = typeof(WorldMapHost).GetMethod(
                "ResolveCanonicalAtlasPath",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null);

            int resolverCalls = 0;
            var establishedResolver = new System.Func<string>(() =>
            {
                resolverCalls++;
                return "C:/resolved/GameData";
            });
            string resolved = (string)resolve.Invoke(
                null,
                new object[] { establishedResolver, "C:/packaged/StreamingAssets" });
            Assert.That(resolverCalls, Is.EqualTo(1));
            Assert.That(
                resolved.Replace('\\', '/'),
                Is.EqualTo("C:/resolved/GameData/al_world_atlas_narrative_catalog.json"));

            string fallback = (string)resolve.Invoke(
                null,
                new object[]
                {
                    new System.Func<string>(() => null),
                    "C:/packaged/StreamingAssets/"
                });
            Assert.That(
                fallback.Replace('\\', '/'),
                Is.EqualTo("C:/packaged/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
        }

        [Test]
        public void HostRecognizes3DScenesOnlyAndWritesContactSheet()
        {
            Assert.That(WorldMapHost.IsWorldMapScene("ChampionArena"), Is.True);
            Assert.That(WorldMapHost.IsWorldMapScene("InnerRealmWorld"), Is.True);
            Assert.That(WorldMapHost.IsWorldMapScene("Kingdom"), Is.False);
            Assert.That(WorldMapHost.IsWorldMapScene("Boot"), Is.False);

            string path = Path.Combine(Application.dataPath, "../Logs/t_9d7be35a-world-map.png");
            string written = WorldMapContactSheet.WritePng(LoadPresentation(), Path.GetFullPath(path));
            Assert.That(File.Exists(written), Is.True);
            Assert.That(new FileInfo(written).Length, Is.GreaterThan(8 * 1024));
        }

        private static WorldMapPresentation LoadPresentation()
        {
            return WorldMapPresentation.FromSnapshot(LoadSnapshot());
        }

        private static WorldMapOverlay EnsureStandaloneOverlay(WorldAtlasSnapshot snapshot)
        {
            WorldMapOverlay overlay = WorldMapOverlay.Ensure(snapshot);
            System.Reflection.MethodInfo activate = typeof(WorldMapOverlay).GetMethod(
                "SetPresentationAuthority",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(activate, Is.Not.Null);
            activate.Invoke(overlay, new object[] { true });
            return overlay;
        }

        private static WorldAtlasSnapshot LoadSnapshot()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);
            Assert.That(result.IsAccepted, Is.True, string.Join("\n", result.Diagnostics.Select(item => item.Fingerprint)));
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

        private static string DumpText(Transform root)
        {
            var parts = new List<string>();
            Collect(root, parts);
            return string.Join("\n", parts);
        }

        private static void InvokeLifecycle(WorldMapOverlay overlay, string methodName)
        {
            System.Reflection.MethodInfo method = typeof(WorldMapOverlay).GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(overlay, null);
        }

        private static void InvokeHostLifecycle(WorldMapHost host, string methodName)
        {
            System.Reflection.MethodInfo method = typeof(WorldMapHost).GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(host, null);
        }

        private static void ResetGameInput()
        {
            System.Reflection.MethodInfo reset = typeof(AL.Input.GameInput).GetMethod(
                "ResetStatics",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, null);
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] components = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene.IsValid())
                {
                    if (component is WorldMapOverlay overlay)
                    {
                        InvokeLifecycle(overlay, "OnDestroy");
                    }
                    Object.DestroyImmediate(component.gameObject);
                }
            }
        }

        private static void Collect(Transform node, List<string> parts)
        {
            parts.Add(node.name);
            Text[] texts = node.GetComponents<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                parts.Add(texts[i].text);
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Collect(node.GetChild(i), parts);
            }
        }
    }
}
