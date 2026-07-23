using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AL.EditorTools
{
    public static class CrossPlatformDesignAssetSetup
    {
        public const string BrandIconAssetPath =
            "Assets/AL/Art/App_Icon_Mystic_Medieval_AL.png";

        private const string RuntimeExportFolder =
            "Assets/AL/Art/Heraldry/RuntimeExports";

        private static readonly string[] Realms =
        {
            "Stonehold",
            "Eldergrove",
            "Crownlands",
            "Umbral"
        };

        [MenuItem("Another Life/Design/Apply Cross-Platform Asset Settings")]
        public static void Apply()
        {
            ConfigureBrandIcon();
            ConfigureHeraldrySprites();
            AssignWindowsIcon();
            AssignCompatibleAndroidIcons();

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[AL-DESIGN-ASSETS] Applied shared Android/Windows import settings. " +
                "Android adaptive foreground/background art remains intentionally unassigned.");
        }

        private static void ConfigureBrandIcon()
        {
            TextureImporter importer = RequireTextureImporter(BrandIconAssetPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureHeraldrySprites()
        {
            foreach (string realm in Realms)
            {
                ConfigureHeraldrySprite(
                    $"{RuntimeExportFolder}/S_ArcaneAxis_{realm}_Flat_256_v001.png",
                    256);
                ConfigureHeraldrySprite(
                    $"{RuntimeExportFolder}/S_ArcaneAxis_{realm}_Micro_32_v001.png",
                    32);
            }
        }

        private static void ConfigureHeraldrySprite(string assetPath, int maxSize)
        {
            TextureImporter importer = RequireTextureImporter(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);

            importer.SetPlatformTextureSettings(CreatePlatformSettings("Android", maxSize));
            importer.SetPlatformTextureSettings(CreatePlatformSettings("Standalone", maxSize));
            importer.SaveAndReimport();
        }

        private static TextureImporterPlatformSettings CreatePlatformSettings(
            string platformName,
            int maxSize)
        {
            return new TextureImporterPlatformSettings
            {
                name = platformName,
                overridden = true,
                maxTextureSize = maxSize,
                format = TextureImporterFormat.RGBA32,
                textureCompression = TextureImporterCompression.Uncompressed,
                compressionQuality = 100,
                crunchedCompression = false,
                allowsAlphaSplitting = false
            };
        }

        private static void AssignWindowsIcon()
        {
            Texture2D icon = RequireBrandIcon();
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
            PlayerSettings.SetIcons(
                NamedBuildTarget.Standalone,
                Enumerable.Repeat(icon, sizes.Length).ToArray(),
                IconKind.Application);
        }

        private static void AssignCompatibleAndroidIcons()
        {
            Texture2D icon = RequireBrandIcon();

            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                if (slots.Length == 0 || slots.Any(slot => slot.maxLayerCount != 1))
                {
                    continue;
                }

                foreach (PlatformIcon slot in slots)
                {
                    slot.SetTexture(icon, 0);
                }

                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }
        }

        private static TextureImporter RequireTextureImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Texture importer is missing for {assetPath}.");
            }

            return importer;
        }

        private static Texture2D RequireBrandIcon()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(BrandIconAssetPath);
            if (icon == null)
            {
                throw new InvalidOperationException(
                    $"Approved brand icon is missing at {BrandIconAssetPath}.");
            }

            return icon;
        }
    }
}
