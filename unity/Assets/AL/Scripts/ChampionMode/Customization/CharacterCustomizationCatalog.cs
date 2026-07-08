using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.ChampionMode.Customization
{
    [Serializable]
    public class CharacterCustomizationCatalogData
    {
        public string version;
        public string game;
        public string[] characterSlots;
        public BodyPresetData[] bodyPresets;
        public StyleOptionData[] hairStyles;
        public StyleOptionData[] armorStyles;
        public ColorOptionData[] primaryColors;
        public ColorOptionData[] hairColors;
        public ColorOptionData[] skinColors;
        public ColorOptionData[] eyeColors;
        public ColorOptionData[] accentColors;
        public StyleOptionData[] faceMarks;
        public StyleOptionData[] weaponStyles;
        public StyleOptionData[] offhandStyles;
        public ChampionForgePresetData[] forgePresets;
    }

    [Serializable]
    public class BodyPresetData
    {
        public string id;
        public string displayName;
        public float[] scale;
    }

    [Serializable]
    public class StyleOptionData
    {
        public string id;
        public string displayName;
    }

    [Serializable]
    public class ColorOptionData
    {
        public string id;
        public string displayName;
        public float[] rgb;
    }

    [Serializable]
    public class ChampionForgePresetData
    {
        public string id;
        public string displayName;
        public string summary;
        public string bodyPresetId;
        public string hairStyleId;
        public string armorStyleId;
        public string faceMarkId;
        public string weaponStyleId;
        public string offhandStyleId;
        public float[] primaryColor;
        public float[] hairColor;
        public float[] skinColor;
        public float[] eyeColor;
        public float[] accentColor;
        public bool capeEnabled;
        public bool helmetEnabled;
    }

    public static class CharacterCustomizationCatalog
    {
        private const string CatalogRelativePath = "GameData/al_character_customization_catalog.json";

        public static bool TryLoad(out CharacterCustomizationCatalogData catalog)
        {
            catalog = null;

            string path = BuildCatalogPath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                return TryParse(File.ReadAllText(path), out catalog);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterCustomizationCatalog] Could not load shared customization catalog. Using runtime defaults. {ex.Message}");
                return false;
            }
        }

        public static IEnumerator LoadAsync(Action<CharacterCustomizationCatalogData> onLoaded)
        {
            if (TryLoad(out var fileCatalog))
            {
                onLoaded?.Invoke(fileCatalog);
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
                    Debug.LogWarning($"[CharacterCustomizationCatalog] Could not load shared customization catalog from StreamingAssets. Using runtime defaults. {request.error}");
                    onLoaded?.Invoke(null);
                    yield break;
                }

                onLoaded?.Invoke(TryParse(request.downloadHandler.text, out var webCatalog) ? webCatalog : null);
            }
        }

        private static bool TryParse(string json, out CharacterCustomizationCatalogData catalog)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            catalog = JsonUtility.FromJson<CharacterCustomizationCatalogData>(json);
            return catalog?.bodyPresets != null &&
                   catalog.bodyPresets.Length > 0 &&
                   catalog.hairStyles != null &&
                   catalog.hairStyles.Length > 0 &&
                   catalog.armorStyles != null &&
                   catalog.armorStyles.Length > 0;
        }

        private static string BuildCatalogPath()
        {
            return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + CatalogRelativePath;
        }
    }
}
