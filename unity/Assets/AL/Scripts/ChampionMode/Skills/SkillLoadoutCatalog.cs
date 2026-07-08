using System;
using System.IO;
using UnityEngine;

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

            string path = Path.Combine(Application.streamingAssetsPath, CatalogRelativePath);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                var catalog = JsonUtility.FromJson<SkillWeatherCatalogData>(json);
                if (catalog?.skillLoadouts == null || catalog.skillLoadouts.Length == 0)
                {
                    return false;
                }

                loadouts = catalog.skillLoadouts;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkillLoadoutCatalog] Could not load shared skill catalog. Using runtime defaults. {ex.Message}");
                return false;
            }
        }
    }
}
