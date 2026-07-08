using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.ChampionMode.Skills
{
    [Serializable]
    public class SkillWeatherCatalogData
    {
        public string version;
        public SkillLoadoutData[] skillLoadouts;
    }

    [Serializable]
    public class SkillLoadoutData
    {
        public int slot;
        public string id;
        public string displayName;
        public string role;
        public string vfxKey;
        public float cooldownSeconds;
        public float manaCost;
        public float castTimeSeconds;
        public float rangeMeters;
        public float power;
        public float botDamageMultiplier;
    }

    public static class SkillLoadoutCatalog
    {
        private const string CatalogRelativePath = "GameData/al_skill_weather_catalog.json";

        public static bool TryLoad(out SkillLoadoutData[] loadouts)
        {
            loadouts = null;

            string path = BuildCatalogPath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                return TryParse(File.ReadAllText(path), out loadouts);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkillLoadoutCatalog] Could not load shared skill catalog. Using runtime defaults. {ex.Message}");
                return false;
            }
        }

        public static IEnumerator LoadAsync(Action<SkillLoadoutData[]> onLoaded)
        {
            if (TryLoad(out var fileLoadouts))
            {
                onLoaded?.Invoke(fileLoadouts);
                yield break;
            }

            string path = BuildCatalogPath();
            if (!path.Contains("://"))
            {
                onLoaded?.Invoke(null);
                yield break;
            }

            using (var request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif
                if (failed)
                {
                    Debug.LogWarning($"[SkillLoadoutCatalog] Could not load shared skill catalog from StreamingAssets. Using runtime defaults. {request.error}");
                    onLoaded?.Invoke(null);
                    yield break;
                }

                onLoaded?.Invoke(TryParse(request.downloadHandler.text, out var webLoadouts) ? webLoadouts : null);
            }
        }

        private static bool TryParse(string json, out SkillLoadoutData[] loadouts)
        {
            loadouts = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var catalog = JsonUtility.FromJson<SkillWeatherCatalogData>(json);
            if (catalog?.skillLoadouts == null || catalog.skillLoadouts.Length == 0)
            {
                return false;
            }

            loadouts = catalog.skillLoadouts;
            return true;
        }

        private static string BuildCatalogPath()
        {
            return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + CatalogRelativePath;
        }
    }
}
