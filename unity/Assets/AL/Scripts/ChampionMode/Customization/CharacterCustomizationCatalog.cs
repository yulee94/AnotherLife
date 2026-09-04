using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode.Customization.Contracts;
using AL.Data.Catalogs;
using AL.Services.Local;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.ChampionMode.Customization
{
    [Serializable]
    public class CharacterCustomizationCatalogData
    {
        public string sourceCatalogId;
        public string sourceFamily;
        public int schemaVersion;
        public string sourceRevision;
        public string sourceSha256;
        public int sourceByteLength;
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
        public const string CatalogRelativePath = "GameData/character_customization.v1.json";

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

        public static bool TryParse(string json, out CharacterCustomizationCatalogData catalog)
        {
            catalog = null;
            GameDataFamilyCatalogSnapshot family;
            string diagnosticCode;
            if (!WireFamilyCatalogLoader.TryLoad("character_customization", json, out family, out diagnosticCode))
            {
                return false;
            }

            catalog = Project(family);
            return catalog != null &&
                   catalog.bodyPresets != null &&
                   catalog.bodyPresets.Length > 0 &&
                   catalog.hairStyles != null &&
                   catalog.hairStyles.Length > 0 &&
                   catalog.armorStyles != null &&
                   catalog.armorStyles.Length > 0;
        }

        public static bool TryParsePlannerCatalog(
            string json,
            out CustomizationCatalogSnapshot catalog,
            out IReadOnlyList<CustomizationDiagnostic> diagnostics)
        {
            catalog = null;
            diagnostics = Array.Empty<CustomizationDiagnostic>();
            if (!TryParse(json, out CharacterCustomizationCatalogData projected))
            {
                diagnostics = new[]
                {
                    new CustomizationDiagnostic(
                        "AL-CUS-PRODUCTION-SOURCE",
                        "catalog.source",
                        string.Empty)
                };
                return false;
            }

            return ProductionCustomizationCatalogAdapter.TryAdapt(
                projected,
                out catalog,
                out diagnostics);
        }

        private static CharacterCustomizationCatalogData Project(GameDataFamilyCatalogSnapshot family)
        {
            var catalog = new CharacterCustomizationCatalogData
            {
                sourceCatalogId = family.CatalogId,
                sourceFamily = family.Family,
                schemaVersion = family.SchemaVersion,
                sourceRevision = family.SourceRevision,
                sourceSha256 = family.Sha256,
                sourceByteLength = family.ByteLength,
                version = family.ContentVersion,
                game = "Another Life",
                characterSlots = ProjectSlots(family),
                bodyPresets = ProjectBodies(family),
                hairStyles = ProjectStyles(family, "hair_style"),
                armorStyles = ProjectStyles(family, "armor_style"),
                primaryColors = ProjectColors(family, "primary_color"),
                hairColors = ProjectColors(family, "hair_color"),
                skinColors = ProjectColors(family, "skin_color"),
                eyeColors = ProjectColors(family, "eye_color"),
                accentColors = ProjectColors(family, "accent_color"),
                faceMarks = ProjectStyles(family, "face_mark"),
                weaponStyles = ProjectStyles(family, "weapon_style"),
                offhandStyles = ProjectStyles(family, "offhand_style"),
                forgePresets = ProjectForges(family)
            };
            return catalog;
        }

        private static string[] ProjectSlots(GameDataFamilyCatalogSnapshot family)
        {
            var records = WireFamilyCatalogLoader.RecordsOfKind(family, "character_slot");
            var slots = new string[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                string slot;
                slots[index] = WireFamilyCatalogLoader.TryGetString(records[index], "slot", out slot)
                    ? slot
                    : WireFamilyCatalogLoader.ConsumerId(records[index]);
            }

            return slots;
        }

        private static BodyPresetData[] ProjectBodies(GameDataFamilyCatalogSnapshot family)
        {
            var records = WireFamilyCatalogLoader.RecordsOfKind(family, "body_preset");
            var bodies = new BodyPresetData[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                float[] scale;
                string displayName;
                WireFamilyCatalogLoader.TryGetString(record, "display_name", out displayName);
                WireFamilyCatalogLoader.TryGetFloatArray(record, "scale", out scale);
                bodies[index] = new BodyPresetData
                {
                    id = WireFamilyCatalogLoader.ConsumerId(record),
                    displayName = displayName,
                    scale = scale
                };
            }

            return bodies;
        }

        private static StyleOptionData[] ProjectStyles(GameDataFamilyCatalogSnapshot family, string kind)
        {
            var records = WireFamilyCatalogLoader.RecordsOfKind(family, kind);
            var styles = new StyleOptionData[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                string displayName;
                WireFamilyCatalogLoader.TryGetString(records[index], "display_name", out displayName);
                styles[index] = new StyleOptionData
                {
                    id = WireFamilyCatalogLoader.ConsumerId(records[index]),
                    displayName = displayName
                };
            }

            return styles;
        }

        private static ColorOptionData[] ProjectColors(GameDataFamilyCatalogSnapshot family, string kind)
        {
            var records = WireFamilyCatalogLoader.RecordsOfKind(family, kind);
            var colors = new ColorOptionData[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                string displayName;
                float[] rgb;
                WireFamilyCatalogLoader.TryGetString(records[index], "display_name", out displayName);
                WireFamilyCatalogLoader.TryGetFloatArray(records[index], "rgb", out rgb);
                colors[index] = new ColorOptionData
                {
                    id = WireFamilyCatalogLoader.ConsumerId(records[index]),
                    displayName = displayName,
                    rgb = rgb
                };
            }

            return colors;
        }

        private static ChampionForgePresetData[] ProjectForges(GameDataFamilyCatalogSnapshot family)
        {
            var records = WireFamilyCatalogLoader.RecordsOfKind(family, "forge_preset");
            var forges = new ChampionForgePresetData[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                string displayName;
                string summary;
                string bodyPresetId;
                string hairStyleId;
                string armorStyleId;
                string faceMarkId;
                string weaponStyleId;
                string offhandStyleId;
                float[] primaryColor;
                float[] hairColor;
                float[] skinColor;
                float[] eyeColor;
                float[] accentColor;
                bool capeEnabled;
                bool helmetEnabled;
                WireFamilyCatalogLoader.TryGetString(record, "display_name", out displayName);
                WireFamilyCatalogLoader.TryGetString(record, "summary", out summary);
                WireFamilyCatalogLoader.TryGetString(record, "body_preset_id", out bodyPresetId);
                WireFamilyCatalogLoader.TryGetString(record, "hair_style_id", out hairStyleId);
                WireFamilyCatalogLoader.TryGetString(record, "armor_style_id", out armorStyleId);
                WireFamilyCatalogLoader.TryGetString(record, "face_mark_id", out faceMarkId);
                WireFamilyCatalogLoader.TryGetString(record, "weapon_style_id", out weaponStyleId);
                WireFamilyCatalogLoader.TryGetString(record, "offhand_style_id", out offhandStyleId);
                WireFamilyCatalogLoader.TryGetFloatArray(record, "primary_color", out primaryColor);
                WireFamilyCatalogLoader.TryGetFloatArray(record, "hair_color", out hairColor);
                WireFamilyCatalogLoader.TryGetFloatArray(record, "skin_color", out skinColor);
                WireFamilyCatalogLoader.TryGetFloatArray(record, "eye_color", out eyeColor);
                WireFamilyCatalogLoader.TryGetFloatArray(record, "accent_color", out accentColor);
                WireFamilyCatalogLoader.TryGetBool(record, "cape_enabled", out capeEnabled);
                WireFamilyCatalogLoader.TryGetBool(record, "helmet_enabled", out helmetEnabled);
                forges[index] = new ChampionForgePresetData
                {
                    id = WireFamilyCatalogLoader.ConsumerId(record),
                    displayName = displayName,
                    summary = summary,
                    bodyPresetId = bodyPresetId,
                    hairStyleId = hairStyleId,
                    armorStyleId = armorStyleId,
                    faceMarkId = faceMarkId,
                    weaponStyleId = weaponStyleId,
                    offhandStyleId = offhandStyleId,
                    primaryColor = primaryColor,
                    hairColor = hairColor,
                    skinColor = skinColor,
                    eyeColor = eyeColor,
                    accentColor = accentColor,
                    capeEnabled = capeEnabled,
                    helmetEnabled = helmetEnabled
                };
            }

            return forges;
        }

        private static string BuildCatalogPath()
        {
            if (SixFamilyRuntimeCatalog.TryResolveGameDataDirectory(out string gameDataDirectory))
            {
                return Path.Combine(gameDataDirectory, Path.GetFileName(CatalogRelativePath));
            }

            return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + CatalogRelativePath;
        }
    }
}
