using AL.ChampionMode.UI;
using AL.Data.Catalogs.MapDisclosure;
using AL.Data.Catalogs.WorldAtlas;
using AL.Input;
using AL.UI.SharedMenu;
using AL.Services.Local;
using AL.World;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace AL.UI.WorldMap
{
    /// <summary>
    /// Attaches the open-map chrome to 3D scenes and routes M / Shared Menu / Esc.
    /// Does not edit ChampionArenaSceneController (hotspot).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class WorldMapHost : MonoBehaviour
    {
        private const string AtlasFileName = "al_world_atlas_narrative_catalog.json";
        private static WorldMapHost _authoritativeHost;
        private static readonly List<WorldMapHost> BoundHosts = new List<WorldMapHost>();
#if UNITY_INCLUDE_TESTS
        internal static bool ForceSnapshotLoadFailureForTests;
#endif
        private WorldMapOverlay _overlay;
        private InnerRealmMinimapOverlay _minimap;
        private bool _bound;
        private bool _catalogLoadInFlight;
        private bool _catalogLoadFailed;
        private int _catalogLoadGeneration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            int unloadedHandle = scene.handle;
            for (int i = BoundHosts.Count - 1; i >= 0; i--)
            {
                WorldMapHost candidate = BoundHosts[i];
                if (candidate == null || candidate.gameObject.scene.handle == unloadedHandle)
                {
                    BoundHosts.RemoveAt(i);
                }
            }

            if (_authoritativeHost == null ||
                _authoritativeHost.gameObject.scene.handle == unloadedHandle)
            {
                _authoritativeHost = null;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.IsValid() && loaded.isLoaded && IsWorldMapScene(loaded.name))
                {
                    EnsureForScene(loaded);
                }
            }

            ElectAuthority();
            if (_authoritativeHost == null)
            {
                WorldMapSession.CloseAll();
            }
        }

        public static WorldMapHost EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !IsWorldMapScene(scene.name))
            {
                return null;
            }

            WorldMapHost selected = FindBestHostInScene(scene);
            if (selected == null)
            {
                var go = new GameObject(WorldMapIds.HostName);
                SceneManager.MoveGameObjectToScene(go, scene);
                selected = go.AddComponent<WorldMapHost>();
            }

            selected.BindIfNeeded();
            return selected;
        }

        public static bool IsWorldMapScene(string sceneName)
        {
            return string.Equals(sceneName, WorldMapIds.ChampionArenaScene, StringComparison.Ordinal) ||
                   string.Equals(sceneName, WorldMapIds.InnerRealmWorldScene, StringComparison.Ordinal);
        }

        private void Update()
        {
            if (!SurfaceReferencesAlive())
            {
                BindIfNeeded();
            }

            if (_authoritativeHost != this)
            {
                return;
            }

            ProcessWorldMapInput(GameInput.WorldMapPressed());
        }

        private void ProcessWorldMapInput(bool worldMapPressed)
        {
            if (!worldMapPressed || _authoritativeHost != this)
            {
                return;
            }

            if (!WorldMapSession.IsMapOpen)
            {
                ChampionHudSession.CloseActiveMenu();
                SharedMenuModeSwitchHost.CloseActiveMenus();
            }

            WorldMapSession.ToggleMap();
        }

        private void OnEnable()
        {
            _catalogLoadFailed = false;
            BindIfNeeded();
        }

        private void OnDisable()
        {
            _catalogLoadGeneration++;
            _catalogLoadInFlight = false;
            _overlay?.SetPresentationAuthority(false);
            RelinquishAuthority();
        }

        private void OnDestroy()
        {
            _overlay?.SetPresentationAuthority(false);
            RelinquishAuthority();
            _bound = false;
            DestroyLocalSurfacesIfOrphaned();
        }

        private void BindIfNeeded()
        {
            Scene ownerScene = gameObject.scene;
            if (!ownerScene.IsValid() || !ownerScene.isLoaded || !isActiveAndEnabled)
            {
                return;
            }

            if (SurfaceReferencesAlive())
            {
                RegisterBoundHost();
                return;
            }
            if (_catalogLoadInFlight || _catalogLoadFailed)
            {
                return;
            }

            if (!Application.isEditor &&
                RequiresUriCatalogLoad(Application.streamingAssetsPath))
            {
                StartUriCatalogLoad(ownerScene);
                return;
            }

            _overlay?.SetPresentationAuthority(false);
            try
            {
                WorldAtlasSnapshot snapshot = LoadCanonicalSnapshot();
                if (snapshot == null)
                {
                    FailBinding();
                    return;
                }

                MapDisclosureCatalogSnapshot disclosure = LoadCanonicalDisclosure();
                if (disclosure == null)
                {
                    FailBinding();
                    return;
                }
                if (CompleteBinding(snapshot, disclosure, ownerScene))
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("World-map surface binding failed: " + exception.Message);
            }

            FailBinding();
        }

        private bool CompleteBinding(
            WorldAtlasSnapshot snapshot,
            MapDisclosureCatalogSnapshot disclosure,
            Scene ownerScene)
        {
            ProgressiveMapSession.Configure(disclosure);
            _overlay = WorldMapOverlay.Ensure(snapshot, ownerScene);
            _minimap = EnsureMinimapForScene(snapshot, ownerScene);
            _bound = _overlay != null && _minimap != null &&
                     _overlay.IsSurfaceHealthy() && _minimap.IsSurfaceHealthy();
            if (!_bound)
            {
                return false;
            }

            RegisterBoundHost();
            return true;
        }

        private void StartUriCatalogLoad(Scene ownerScene)
        {
            _catalogLoadInFlight = true;
            int generation = ++_catalogLoadGeneration;
            string atlasLocation = ResolveRuntimeCatalogLocation(
                Application.streamingAssetsPath,
                AtlasFileName);
            string disclosureLocation = ResolveCanonicalDisclosureLocation(
                Application.dataPath,
                Application.streamingAssetsPath,
                isEditor: false);
            StartCoroutine(LoadCatalogsFromUris(
                ownerScene,
                atlasLocation,
                disclosureLocation,
                generation));
        }

        private IEnumerator LoadCatalogsFromUris(
            Scene ownerScene,
            string atlasLocation,
            string disclosureLocation,
            int generation)
        {
            byte[] atlasBytes;
            using (UnityWebRequest request = UnityWebRequest.Get(atlasLocation))
            {
                yield return request.SendWebRequest();
                if (!IsCurrentCatalogLoad(generation))
                {
                    yield break;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    FinishUriLoadFailure(
                        generation,
                        "World atlas catalog could not be read: " + request.error);
                    yield break;
                }
                atlasBytes = request.downloadHandler.data;
            }

            WorldAtlasLoadResult atlasResult = WorldAtlasTopologyLoader.Validate(atlasBytes);
            if (!atlasResult.IsAccepted)
            {
                FinishUriLoadFailure(
                    generation,
                    "World atlas catalog was rejected: " +
                    string.Join("; ", atlasResult.Diagnostics.Select(value => value.Fingerprint)));
                yield break;
            }

            byte[] disclosureBytes;
            using (UnityWebRequest request = UnityWebRequest.Get(disclosureLocation))
            {
                yield return request.SendWebRequest();
                if (!IsCurrentCatalogLoad(generation))
                {
                    yield break;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    FinishUriLoadFailure(
                        generation,
                        "Map disclosure catalog could not be read: " + request.error);
                    yield break;
                }
                disclosureBytes = request.downloadHandler.data;
            }

            MapDisclosureLoadResult disclosureResult =
                MapDisclosureCatalogLoader.Validate(disclosureBytes);
            if (!disclosureResult.IsAccepted)
            {
                FinishUriLoadFailure(
                    generation,
                    "Map disclosure catalog was rejected: " +
                    string.Join("; ", disclosureResult.Diagnostics.Select(value => value.Fingerprint)));
                yield break;
            }

            if (!IsCurrentCatalogLoad(generation))
            {
                yield break;
            }
            _catalogLoadInFlight = false;
            if (!ownerScene.IsValid() || !ownerScene.isLoaded ||
                !CompleteBinding(atlasResult.Snapshot, disclosureResult.Snapshot, ownerScene))
            {
                FailBinding();
            }
        }

        private bool IsCurrentCatalogLoad(int generation)
        {
            return generation == _catalogLoadGeneration && isActiveAndEnabled;
        }

        private void FinishUriLoadFailure(int generation, string message)
        {
            if (!IsCurrentCatalogLoad(generation))
            {
                return;
            }
            _catalogLoadInFlight = false;
            _catalogLoadFailed = true;
            Debug.LogWarning(message);
            FailBinding();
        }

        private void FailBinding()
        {
            _bound = false;
            _overlay?.SetPresentationAuthority(false);
            RelinquishAuthority();
        }

        private bool SurfaceReferencesAlive()
        {
            if (!_bound || _overlay == null || _minimap == null ||
                !_overlay.IsSurfaceHealthy() || !_minimap.IsSurfaceHealthy())
            {
                return false;
            }

            Scene ownerScene = gameObject.scene;
            return ownerScene.IsValid() && ownerScene.isLoaded &&
                   _overlay.gameObject.scene == ownerScene &&
                   _minimap.gameObject.scene == ownerScene;
        }

        private void RegisterBoundHost()
        {
            if (!BoundHosts.Contains(this))
            {
                BoundHosts.Add(this);
            }

            ElectAuthority();
        }

        private void RelinquishAuthority()
        {
            BoundHosts.Remove(this);
            if (_authoritativeHost == this)
            {
                _authoritativeHost = null;
            }

            ElectAuthority();
            if (_authoritativeHost == null)
            {
                WorldMapSession.CloseAll();
            }
        }

        private void DestroyLocalSurfacesIfOrphaned()
        {
            Scene ownerScene = gameObject.scene;
            for (int i = 0; i < BoundHosts.Count; i++)
            {
                WorldMapHost candidate = BoundHosts[i];
                if (candidate != null && candidate != this &&
                    candidate.gameObject.scene == ownerScene)
                {
                    return;
                }
            }

            DestroySurface(_overlay != null ? _overlay.gameObject : null);
            DestroySurface(_minimap != null ? _minimap.gameObject : null);
            _overlay = null;
            _minimap = null;
        }

        private static void DestroySurface(GameObject surface)
        {
            if (surface == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(surface);
            }
            else
            {
                DestroyImmediate(surface);
            }
        }

        private static void ElectAuthority()
        {
            WorldMapHost selected = null;
            for (int i = BoundHosts.Count - 1; i >= 0; i--)
            {
                WorldMapHost candidate = BoundHosts[i];
                if (candidate == null)
                {
                    BoundHosts.RemoveAt(i);
                    continue;
                }

                if (!candidate.isActiveAndEnabled || !candidate.SurfaceReferencesAlive())
                {
                    continue;
                }

                if (selected == null || CompareAuthority(candidate, selected) > 0)
                {
                    selected = candidate;
                }
            }

            WorldMapOverlay selectedOverlay = selected != null ? selected._overlay : null;
            WorldMapOverlay[] overlays = Resources.FindObjectsOfTypeAll<WorldMapOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                WorldMapOverlay overlay = overlays[i];
                if (overlay != null && overlay != selectedOverlay &&
                    overlay.gameObject.scene.IsValid())
                {
                    overlay.SetPresentationAuthority(false);
                }
            }

            _authoritativeHost = selected;
            selectedOverlay?.SetPresentationAuthority(true);
        }

        private static int CompareAuthority(WorldMapHost left, WorldMapHost right)
        {
            bool leftSupported = IsWorldMapScene(left.gameObject.scene.name);
            bool rightSupported = IsWorldMapScene(right.gameObject.scene.name);
            int supportedComparison = leftSupported.CompareTo(rightSupported);
            if (supportedComparison != 0)
            {
                return supportedComparison;
            }

            int leftSceneHandle = left.gameObject.scene.handle;
            int rightSceneHandle = right.gameObject.scene.handle;
            int sceneComparison = leftSceneHandle == rightSceneHandle
                ? 0
                : leftSceneHandle < rightSceneHandle ? -1 : 1;
            return sceneComparison != 0
                ? sceneComparison
                : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private static WorldMapHost FindBestHostInScene(Scene scene)
        {
            WorldMapHost selected = null;
            WorldMapHost[] hosts = Resources.FindObjectsOfTypeAll<WorldMapHost>();
            for (int i = 0; i < hosts.Length; i++)
            {
                WorldMapHost candidate = hosts[i];
                if (candidate == null || candidate.gameObject.scene != scene ||
                    !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                if (selected == null || CompareAuthority(candidate, selected) > 0)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private static InnerRealmMinimapOverlay EnsureMinimapForScene(
            WorldAtlasSnapshot snapshot,
            Scene scene)
        {
            InnerRealmMinimapOverlay[] overlays =
                Resources.FindObjectsOfTypeAll<InnerRealmMinimapOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                InnerRealmMinimapOverlay candidate = overlays[i];
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    candidate.EnsureSurfaceHealthy(snapshot);
                    return candidate;
                }
            }

            var root = new GameObject(InnerRealmMinimapOverlay.RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            InnerRealmMinimapOverlay created =
                root.AddComponent<InnerRealmMinimapOverlay>();
            created.EnsureSurfaceHealthy(snapshot);
            return created;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
#if UNITY_INCLUDE_TESTS
            ForceSnapshotLoadFailureForTests = false;
#endif
            WorldMapOverlay[] overlays = Resources.FindObjectsOfTypeAll<WorldMapOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                WorldMapOverlay overlay = overlays[i];
                if (overlay != null && overlay.gameObject.scene.IsValid())
                {
                    overlay.SetPresentationAuthority(false);
                }
            }

            _authoritativeHost = null;
            BoundHosts.Clear();
            ProgressiveMapSession.ResetForTests();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        internal static WorldAtlasSnapshot LoadCanonicalSnapshot()
        {
#if UNITY_INCLUDE_TESTS
            if (ForceSnapshotLoadFailureForTests)
            {
                return null;
            }
#endif
            return FirstSessionInnerRealmSpawn.TryLoadCanonicalSnapshot(
                out WorldAtlasSnapshot snapshot)
                ? snapshot
                : null;
        }

        internal static string ResolveCanonicalAtlasPath(
            Func<string> establishedGameDataResolver,
            string streamingAssetsPath)
        {
            string gameDataDirectory = establishedGameDataResolver?.Invoke();
            if (string.IsNullOrEmpty(gameDataDirectory))
            {
                gameDataDirectory = Path.Combine(
                    (streamingAssetsPath ?? string.Empty).TrimEnd('/', '\\'),
                    "GameData");
            }

            return Path.Combine(gameDataDirectory, AtlasFileName);
        }

        internal static MapDisclosureCatalogSnapshot LoadCanonicalDisclosure()
        {
            string path = ResolveCanonicalDisclosureLocation(
                Application.dataPath,
                Application.streamingAssetsPath,
                Application.isEditor);
            if (!File.Exists(path))
            {
                Debug.LogWarning("Map disclosure catalog is missing: " + path);
                return null;
            }
            return LoadDisclosureFromFile(path, File.ReadAllBytes);
        }

        internal static MapDisclosureCatalogSnapshot LoadDisclosureFromFile(
            string path,
            Func<string, byte[]> readBytes)
        {
            try
            {
                if (readBytes == null)
                {
                    Debug.LogWarning("Map disclosure catalog is missing: " + path);
                    return null;
                }

                MapDisclosureLoadResult result =
                    MapDisclosureCatalogLoader.Validate(readBytes(path));
                if (!result.IsAccepted)
                {
                    Debug.LogWarning(
                        "Map disclosure catalog was rejected: " +
                        string.Join("; ", result.Diagnostics.Select(value => value.Fingerprint)));
                    return null;
                }
                return result.Snapshot;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                Debug.LogWarning(
                    "Map disclosure catalog could not be read: " + exception.Message);
                return null;
            }
        }

        internal static string ResolveCanonicalDisclosurePath(
            string dataPath,
            string streamingAssetsPath,
            bool isEditor)
        {
            return ResolveCanonicalDisclosureLocation(
                dataPath,
                streamingAssetsPath,
                isEditor);
        }

        internal static string ResolveCanonicalDisclosureLocation(
            string dataPath,
            string streamingAssetsPath,
            bool isEditor)
        {
            if (isEditor)
            {
                return Path.Combine(
                    dataPath ?? string.Empty,
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    MapDisclosureContract.FileName);
            }
            return ResolveRuntimeCatalogLocation(
                streamingAssetsPath,
                MapDisclosureContract.FileName);
        }

        internal static string ResolveRuntimeCatalogLocation(
            string streamingAssetsRoot,
            string fileName)
        {
            string root = (streamingAssetsRoot ?? string.Empty).TrimEnd('/', '\\');
            if (RequiresUriCatalogLoad(root))
            {
                return root + "/GameData/" + fileName;
            }
            return Path.Combine(root, "GameData", fileName);
        }

        internal static bool RequiresUriCatalogLoad(string location)
        {
            return !string.IsNullOrEmpty(location) &&
                   (location.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                    location.StartsWith("jar:", StringComparison.OrdinalIgnoreCase));
        }
    }
}
