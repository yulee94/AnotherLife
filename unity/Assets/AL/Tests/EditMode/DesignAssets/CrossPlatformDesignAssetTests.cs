using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AL.Tests.EditMode.DesignAssets
{
    public sealed class CrossPlatformDesignAssetTests
    {
        private const string BrandIconPath =
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

        [Test]
        public void ApprovedBrandIconIsSharedAndAssignedToWindows()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(BrandIconPath);
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.width, Is.EqualTo(1024));
            Assert.That(icon.height, Is.EqualTo(1024));

            TextureImporter importer = RequireImporter(BrandIconPath);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);

            Texture2D[] windowsIcons =
                PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application);
            Assert.That(windowsIcons, Is.Not.Empty);
            Assert.That(windowsIcons, Has.All.SameAs(icon));
        }

        [Test]
        public void ApprovedBrandIconFillsEverySingleLayerAndroidSlot()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(BrandIconPath);
            var verifiedKinds = new List<string>();

            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                if (slots.Length == 0 || slots.Any(slot => slot.maxLayerCount != 1))
                {
                    continue;
                }

                verifiedKinds.Add(kind.ToString());
                Assert.That(slots.Select(slot => slot.GetTexture(0)), Has.All.SameAs(icon));
            }

            Assert.That(verifiedKinds, Is.Not.Empty);
        }

        [Test]
        public void AndroidAdaptiveIconSlotsStayUnassignedUntilLayerArtIsApproved()
        {
            var verifiedSlots = new List<string>();

            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);

                foreach (PlatformIcon slot in slots.Where(slot => slot.maxLayerCount > 1))
                {
                    verifiedSlots.Add($"{kind}:{slot.width}x{slot.height}");

                    for (int layer = 0; layer < slot.maxLayerCount; layer++)
                    {
                        Assert.That(
                            slot.GetTexture(layer),
                            Is.Null,
                            $"{kind} {slot.width}x{slot.height} layer {layer} must remain unassigned.");
                    }
                }
            }

            Assert.That(verifiedSlots, Is.Not.Empty);
        }

        [TestCaseSource(nameof(RuntimeSpriteCases))]
        public void HeraldryRuntimeExportIsTintableAndPlatformNeutral(
            string assetPath,
            int expectedSize)
        {
            TextureImporter importer = RequireImporter(assetPath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            Assert.That(textureSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.maxTextureSize, Is.EqualTo(expectedSize));

            AssertPlatform(importer, "Android", expectedSize);
            AssertPlatform(importer, "Standalone", expectedSize);
            AssertWhiteTransparentSource(assetPath, expectedSize);
        }

        private static IEnumerable<TestCaseData> RuntimeSpriteCases()
        {
            foreach (string realm in Realms)
            {
                yield return new TestCaseData(
                        $"{RuntimeExportFolder}/S_ArcaneAxis_{realm}_Flat_256_v001.png",
                        256)
                    .SetName($"{realm}_flat_sprite_is_cross_platform");
                yield return new TestCaseData(
                        $"{RuntimeExportFolder}/S_ArcaneAxis_{realm}_Micro_32_v001.png",
                        32)
                    .SetName($"{realm}_micro_sprite_is_cross_platform");
            }
        }

        private static TextureImporter RequireImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, $"Missing texture importer for {assetPath}.");
            return importer;
        }

        private static void AssertPlatform(
            TextureImporter importer,
            string platform,
            int expectedSize)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            Assert.That(settings.overridden, Is.True, $"{platform} override is required.");
            Assert.That(settings.maxTextureSize, Is.EqualTo(expectedSize));
            Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.RGBA32));
        }

        private static void AssertWhiteTransparentSource(string assetPath, int expectedSize)
        {
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                Assert.That(source.LoadImage(bytes, false), Is.True);
                Assert.That(source.width, Is.EqualTo(expectedSize));
                Assert.That(source.height, Is.EqualTo(expectedSize));

                Color32[] pixels = source.GetPixels32();
                Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
                Assert.That(pixels.Any(pixel => pixel.a > 0), Is.True);

                foreach (Color32 pixel in pixels.Where(pixel => pixel.a > 0))
                {
                    Assert.That(pixel.r, Is.GreaterThanOrEqualTo(250));
                    Assert.That(pixel.g, Is.GreaterThanOrEqualTo(250));
                    Assert.That(pixel.b, Is.GreaterThanOrEqualTo(250));
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
    }
}
