using System;
using System.Collections;
using System.IO;
using AL.Data.Catalogs;
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
        public const string CatalogRelativePath = "GameData/skill_weather.v1.json";

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

        public static bool TryParse(string json, out SkillLoadoutData[] loadouts)
        {
            loadouts = null;
            GameDataFamilyCatalogSnapshot family;
            string diagnosticCode;
            if (!WireFamilyCatalogLoader.TryLoad("skill_weather", json, out family, out diagnosticCode))
            {
                return false;
            }

            var records = WireFamilyCatalogLoader.RecordsOfKind(family, "skill_loadout");
            if (records.Count == 0)
            {
                return false;
            }

            loadouts = new SkillLoadoutData[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                int slot;
                string displayName;
                string role;
                string vfxKey;
                float cooldownSeconds;
                float manaCost;
                float castTimeSeconds;
                float rangeMeters;
                float power;
                float botDamageMultiplier;
                WireFamilyCatalogLoader.TryGetInt(record, "slot", out slot);
                WireFamilyCatalogLoader.TryGetString(record, "display_name", out displayName);
                WireFamilyCatalogLoader.TryGetString(record, "role", out role);
                WireFamilyCatalogLoader.TryGetString(record, "vfx_key", out vfxKey);
                WireFamilyCatalogLoader.TryGetFloat(record, "cooldown_seconds", out cooldownSeconds);
                WireFamilyCatalogLoader.TryGetFloat(record, "mana_cost", out manaCost);
                WireFamilyCatalogLoader.TryGetFloat(record, "cast_time_seconds", out castTimeSeconds);
                WireFamilyCatalogLoader.TryGetFloat(record, "range_meters", out rangeMeters);
                WireFamilyCatalogLoader.TryGetFloat(record, "power", out power);
                WireFamilyCatalogLoader.TryGetFloat(record, "bot_damage_multiplier", out botDamageMultiplier);
                loadouts[index] = new SkillLoadoutData
                {
                    slot = slot,
                    id = record.Id,
                    displayName = displayName,
                    role = role,
                    vfxKey = vfxKey,
                    cooldownSeconds = cooldownSeconds,
                    manaCost = manaCost,
                    castTimeSeconds = castTimeSeconds,
                    rangeMeters = rangeMeters,
                    power = power,
                    botDamageMultiplier = botDamageMultiplier
                };
            }

            Array.Sort(loadouts, (left, right) => left.slot.CompareTo(right.slot));
            return true;
        }

        private static string BuildCatalogPath()
        {
            return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + CatalogRelativePath;
        }
    }
}
