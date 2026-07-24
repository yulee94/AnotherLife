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

        public const string AndroidAdaptiveForegroundAssetPath =
            "Assets/AL/Art/Branding/AndroidAdaptive/" +
            "App_Icon_Android_Adaptive_Foreground_AL_432_v001.png";

        public const string AndroidAdaptiveBackgroundAssetPath =
            "Assets/AL/Art/Branding/AndroidAdaptive/" +
            "App_Icon_Android_Adaptive_Background_432_v001.png";

        public const string AndroidMonochromeAssetPath =
            "Assets/AL/Art/Branding/AndroidAdaptive/" +
            "App_Icon_Android_Monochrome_AL_432_v001.png";

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
            ConfigureAndroidAdaptiveIcons();
            ConfigureHeraldrySprites();
            AssignWindowsIcon();
            AssignCompatibleAndroidIcons();
            AssignAndroidAdaptiveIcons();

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[AL-DESIGN-ASSETS] Applied shared Android/Windows import settings, " +
                "including Android adaptive foreground/background layers.");
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

        private static void ConfigureAndroidAdaptiveIcons()
        {
            ConfigureAndroidIconLayer(AndroidAdaptiveForegroundAssetPath, true);
            ConfigureAndroidIconLayer(AndroidAdaptiveBackgroundAssetPath, false);
            ConfigureAndroidIconLayer(AndroidMonochromeAssetPath, true);
        }

        private static void ConfigureAndroidIconLayer(string assetPath, bool hasAlpha)
        {
            TextureImporter importer = RequireTextureImporter(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = hasAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = hasAlpha;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(
                CreatePlatformSettings(
                    "Android",
                    512,
                    hasAlpha ? TextureImporterFormat.RGBA32 : TextureImporterFormat.RGB24));
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
            int maxSize,
            TextureImporterFormat format = TextureImporterFormat.RGBA32)
        {
            return new TextureImporterPlatformSettings
            {
                name = platformName,
                overridden = true,
                maxTextureSize = maxSize,
                format = format,
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

        private static void AssignAndroidAdaptiveIcons()
        {
            Texture2D foreground = RequireTexture(AndroidAdaptiveForegroundAssetPath);
            Texture2D background = RequireTexture(AndroidAdaptiveBackgroundAssetPath);

            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                if (slots.Length == 0 || slots.Any(slot => slot.maxLayerCount != 2))
                {
                    continue;
                }

                foreach (PlatformIcon slot in slots)
                {
                    slot.SetTexture(background, 0);
                    slot.SetTexture(foreground, 1);
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
            return RequireTexture(BrandIconAssetPath);
        }

        private static Texture2D RequireTexture(string assetPath)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                throw new InvalidOperationException(
                    $"Required design texture is missing at {assetPath}.");
            }

            return texture;
        }
    }
}
