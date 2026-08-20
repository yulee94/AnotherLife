using AL.ChampionMode.UI;
using AL.Data.Catalogs.WorldAtlas;
using AL.Input;
using System.IO;
using UnityEngine;
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
        private WorldMapOverlay _overlay;
        private bool _bound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        public static WorldMapHost EnsureForScene(Scene scene)
        {
            if (!IsWorldMapScene(scene.name))
            {
                return null;
            }

            WorldMapHost existing = FindObjectOfType<WorldMapHost>();
            if (existing != null)
            {
                existing.BindIfNeeded();
                return existing;
            }

            var go = new GameObject(WorldMapIds.HostName);
            WorldMapHost host = go.AddComponent<WorldMapHost>();
            host.BindIfNeeded();
            return host;
        }

        public static bool IsWorldMapScene(string sceneName)
        {
            return string.Equals(sceneName, WorldMapIds.ChampionArenaScene, System.StringComparison.Ordinal) ||
                   string.Equals(sceneName, WorldMapIds.InnerRealmWorldScene, System.StringComparison.Ordinal);
        }

        private void Update()
        {
            if (!_bound)
            {
                BindIfNeeded();
            }

            if (GameInput.WorldMapPressed())
            {
                ChampionHudSession hudSession = FindObjectOfType<ChampionHudSession>();
                if (!WorldMapSession.IsMapOpen && hudSession != null && hudSession.IsOpen)
                {
                    hudSession.CloseMenu();
                }

                WorldMapSession.ToggleMap();
            }
        }

        private void OnDestroy()
        {
            WorldMapSession.CloseAll();
            GameInput.SetGameplaySuppressed(false);
        }

        private void BindIfNeeded()
        {
            WorldAtlasSnapshot snapshot = LoadCanonicalSnapshot();
            if (snapshot == null)
            {
                return;
            }

            _overlay = WorldMapOverlay.Ensure(snapshot);
            _bound = _overlay != null;
        }

        internal static WorldAtlasSnapshot LoadCanonicalSnapshot()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json");
            if (!File.Exists(path))
            {
                return null;
            }

            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(File.ReadAllBytes(path));
            return result.IsAccepted ? result.Snapshot : null;
        }
    }
}
