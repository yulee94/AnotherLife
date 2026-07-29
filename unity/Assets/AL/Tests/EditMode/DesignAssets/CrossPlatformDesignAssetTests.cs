using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        private const string ApprovedBrandIconGuid =
            "a97dbdb16fc334e758f5230fa20ca0b0";

        private const string IosBuildSupportUnavailableDiagnostic =
            "IOS_BUILD_SUPPORT_UNAVAILABLE";

        private const string IosIconQueryUnavailableDiagnostic =
            "IOS_ICON_QUERY_UNAVAILABLE";

        private const string AndroidAdaptiveForegroundPath =
            "Assets/AL/Art/Branding/AndroidAdaptive/" +
            "App_Icon_Android_Adaptive_Foreground_AL_432_v001.png";

        private const string AndroidAdaptiveBackgroundPath =
            "Assets/AL/Art/Branding/AndroidAdaptive/" +
            "App_Icon_Android_Adaptive_Background_432_v001.png";

        private const string AndroidMonochromePath =
            "Assets/AL/Art/Branding/AndroidAdaptive/" +
            "App_Icon_Android_Monochrome_AL_432_v001.png";

        private const int AdaptiveCanvasSize = 432;
        private const int AdaptiveSafeZoneStart = 84;
        private const int AdaptiveSafeZoneEnd = 347;

        private const string RuntimeExportFolder =
            "Assets/AL/Art/Heraldry/RuntimeExports";

        private static readonly string[] SerializedIosIconMatrix =
        {
            "0:iPhone:180x180",
            "0:iPhone:120x120",
            "0:iPad:167x167",
            "0:iPad:152x152",
            "0:iPad:76x76",
            "3:iPhone:120x120",
            "3:iPhone:80x80",
            "3:iPad:80x80",
            "3:iPad:40x40",
            "1:iPhone:87x87",
            "1:iPhone:58x58",
            "1:iPhone:29x29",
            "1:iPad:58x58",
            "1:iPad:29x29",
            "2:iPhone:60x60",
            "2:iPhone:40x40",
            "2:iPad:40x40",
            "2:iPad:20x20",
            "4:App Store:1024x1024"
        };

        private static readonly Regex SerializedIosIconPattern = new Regex(
            @"    - m_Textures:\r?\n" +
            @"      - \{fileID: 2800000, guid: (?<guid>[a-f0-9]{32}), type: 3\}\r?\n" +
            @"      m_Width: (?<width>\d+)\r?\n" +
            @"      m_Height: (?<height>\d+)\r?\n" +
            @"      m_Kind: (?<kind>\d+)\r?\n" +
            @"      m_SubKind: (?<subkind>[^\r\n]+)",
            RegexOptions.CultureInvariant);

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
        public void ApprovedBrandIconFillsEveryIosSlot()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(BrandIconPath);
            Assert.That(icon, Is.Not.Null);

            IosIconQueryResult query = QueryLiveIosIconSlots();
            if (query.Status == IosIconQueryStatus.BuildSupportUnavailable)
            {
                Assert.That(
                    query.DiagnosticCode,
                    Is.EqualTo(IosBuildSupportUnavailableDiagnostic));
                Assert.That(query.Slots, Is.Empty);
                return;
            }

            Assert.That(
                query.Status,
                Is.EqualTo(IosIconQueryStatus.Available),
                query.DiagnosticCode);
            Assert.That(query.DiagnosticCode, Is.Empty);

            foreach (IosLiveIconSlot slot in query.Slots)
            {
                Assert.That(slot.Icon.maxLayerCount, Is.EqualTo(1), slot.Label);
                Assert.That(slot.Icon.GetTexture(0), Is.SameAs(icon), slot.Label);
            }
        }

        [Test]
        public void SerializedProjectSettingsPinsEveryIosBrandIconSlot()
        {
            Assert.That(
                AssetDatabase.AssetPathToGUID(BrandIconPath),
                Is.EqualTo(ApprovedBrandIconGuid));

            string iosIconBlock = ReadSerializedIosIconBlock();
            MatchCollection matches = SerializedIosIconPattern.Matches(iosIconBlock);
            Assert.That(matches.Count, Is.GreaterThan(0), "Serialized iOS icon matrix is empty.");

            var actualMatrix = new List<string>(matches.Count);
            foreach (Match match in matches)
            {
                string slot =
                    $"{match.Groups["kind"].Value}:" +
                    $"{match.Groups["subkind"].Value}:" +
                    $"{match.Groups["width"].Value}x{match.Groups["height"].Value}";
                actualMatrix.Add(slot);

                Assert.That(
                    match.Groups["guid"].Value,
                    Is.EqualTo(ApprovedBrandIconGuid),
                    slot);
            }

            CollectionAssert.AreEqual(SerializedIosIconMatrix, actualMatrix);
        }

        [Test]
        public void AndroidAdaptiveIconLayersAreAssignedAndMaskSafe()
        {
            Texture2D foreground =
                AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidAdaptiveForegroundPath);
            Texture2D background =
                AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidAdaptiveBackgroundPath);
            Assert.That(foreground, Is.Not.Null);
            Assert.That(background, Is.Not.Null);

            AssertAndroidIconImporter(AndroidAdaptiveForegroundPath, true);
            AssertAndroidIconImporter(AndroidAdaptiveBackgroundPath, false);

            var verifiedSlots = new List<string>();

            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);

                foreach (PlatformIcon slot in slots.Where(slot => slot.maxLayerCount == 2))
                {
                    verifiedSlots.Add($"{kind}:{slot.width}x{slot.height}");
                    Assert.That(slot.GetTexture(0), Is.SameAs(background));
                    Assert.That(slot.GetTexture(1), Is.SameAs(foreground));
                }
            }

            Assert.That(verifiedSlots, Is.Not.Empty);

            Color32[] foregroundPixels = LoadSourcePixels(
                AndroidAdaptiveForegroundPath,
                AdaptiveCanvasSize,
                out int foregroundWidth,
                out int foregroundHeight);
            Assert.That(foregroundWidth, Is.EqualTo(AdaptiveCanvasSize));
            Assert.That(foregroundHeight, Is.EqualTo(AdaptiveCanvasSize));
            AssertAlphaBoundsInsideAdaptiveSafeZone(foregroundPixels);

            Color32[] backgroundPixels = LoadSourcePixels(
                AndroidAdaptiveBackgroundPath,
                AdaptiveCanvasSize,
                out _,
                out _);
            Assert.That(backgroundPixels.All(pixel => pixel.a == 255), Is.True);
        }

        [Test]
        public void AndroidMonochromeSourceMatchesAdaptiveForegroundSilhouette()
        {
            AssertAndroidIconImporter(AndroidMonochromePath, true);

            Color32[] foregroundPixels = LoadSourcePixels(
                AndroidAdaptiveForegroundPath,
                AdaptiveCanvasSize,
                out _,
                out _);
            Color32[] monochromePixels = LoadSourcePixels(
                AndroidMonochromePath,
                AdaptiveCanvasSize,
                out _,
                out _);

            Assert.That(monochromePixels.Length, Is.EqualTo(foregroundPixels.Length));
            for (int index = 0; index < monochromePixels.Length; index++)
            {
                Color32 pixel = monochromePixels[index];
                Assert.That(pixel.a, Is.EqualTo(foregroundPixels[index].a));
                if (pixel.a == 0)
                {
                    continue;
                }

                Assert.That(pixel.r, Is.EqualTo(255));
                Assert.That(pixel.g, Is.EqualTo(255));
                Assert.That(pixel.b, Is.EqualTo(255));
            }
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

        private static IosIconQueryResult QueryLiveIosIconSlots()
        {
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.iOS,
                    BuildTarget.iOS))
            {
                return new IosIconQueryResult(
                    IosIconQueryStatus.BuildSupportUnavailable,
                    IosBuildSupportUnavailableDiagnostic,
                    new List<IosLiveIconSlot>());
            }

            var slots = new List<IosLiveIconSlot>();
            foreach (PlatformIconKind kind in
                     PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                foreach (PlatformIcon icon in
                         PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind))
                {
                    slots.Add(new IosLiveIconSlot(
                        $"{kind}:{icon.width}x{icon.height}",
                        icon));
                }
            }

            if (slots.Count == 0)
            {
                return new IosIconQueryResult(
                    IosIconQueryStatus.IconQueryUnavailable,
                    IosIconQueryUnavailableDiagnostic,
                    slots);
            }

            return new IosIconQueryResult(
                IosIconQueryStatus.Available,
                string.Empty,
                slots);
        }

        private static string ReadSerializedIosIconBlock()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            Assert.That(projectRoot, Is.Not.Null, "Unity project root is unavailable.");

            string settingsPath = Path.Combine(
                projectRoot.FullName,
                "ProjectSettings",
                "ProjectSettings.asset");
            Assert.That(File.Exists(settingsPath), Is.True, settingsPath);

            string settings = File.ReadAllText(settingsPath);
            const string iosTargetMarker = "  - m_BuildTarget: iPhone";
            int start = settings.IndexOf(
                iosTargetMarker,
                System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), iosTargetMarker);

            int end = settings.IndexOf(
                "\n  m_BuildTargetBatching:",
                start,
                System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "Serialized iOS icon block is incomplete.");
            return settings.Substring(start, end - start);
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

        private static void AssertAndroidIconImporter(string assetPath, bool hasAlpha)
        {
            TextureImporter importer = RequireImporter(assetPath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.alphaSource, Is.EqualTo(
                hasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None));
            Assert.That(importer.alphaIsTransparency, Is.EqualTo(hasAlpha));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.maxTextureSize, Is.EqualTo(512));

            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings("Android");
            Assert.That(settings.overridden, Is.True);
            Assert.That(settings.maxTextureSize, Is.EqualTo(512));
            Assert.That(
                settings.format,
                Is.EqualTo(hasAlpha ? TextureImporterFormat.RGBA32 : TextureImporterFormat.RGB24));
        }

        private static Color32[] LoadSourcePixels(
            string assetPath,
            int expectedSize,
            out int width,
            out int height)
        {
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                Assert.That(source.LoadImage(bytes, false), Is.True);
                width = source.width;
                height = source.height;
                Assert.That(width, Is.EqualTo(expectedSize));
                Assert.That(height, Is.EqualTo(expectedSize));
                return source.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static void AssertAlphaBoundsInsideAdaptiveSafeZone(Color32[] pixels)
        {
            int minX = AdaptiveCanvasSize;
            int minY = AdaptiveCanvasSize;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < AdaptiveCanvasSize; y++)
            {
                for (int x = 0; x < AdaptiveCanvasSize; x++)
                {
                    if (pixels[y * AdaptiveCanvasSize + x].a == 0)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            Assert.That(maxX, Is.GreaterThanOrEqualTo(0), "Foreground must contain visible pixels.");
            Assert.That(minX, Is.GreaterThanOrEqualTo(AdaptiveSafeZoneStart));
            Assert.That(minY, Is.GreaterThanOrEqualTo(AdaptiveSafeZoneStart));
            Assert.That(maxX, Is.LessThanOrEqualTo(AdaptiveSafeZoneEnd));
            Assert.That(maxY, Is.LessThanOrEqualTo(AdaptiveSafeZoneEnd));
        }

        private static void AssertWhiteTransparentSource(string assetPath, int expectedSize)
        {
            Color32[] pixels = LoadSourcePixels(
                assetPath,
                expectedSize,
                out _,
                out _);
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
            Assert.That(pixels.Any(pixel => pixel.a > 0), Is.True);

            foreach (Color32 pixel in pixels.Where(pixel => pixel.a > 0))
            {
                Assert.That(pixel.r, Is.GreaterThanOrEqualTo(250));
                Assert.That(pixel.g, Is.GreaterThanOrEqualTo(250));
                Assert.That(pixel.b, Is.GreaterThanOrEqualTo(250));
            }
        }

        private enum IosIconQueryStatus
        {
            Available,
            BuildSupportUnavailable,
            IconQueryUnavailable
        }

        private sealed class IosIconQueryResult
        {
            public IosIconQueryResult(
                IosIconQueryStatus status,
                string diagnosticCode,
                List<IosLiveIconSlot> slots)
            {
                Status = status;
                DiagnosticCode = diagnosticCode;
                Slots = slots;
            }

            public IosIconQueryStatus Status { get; }

            public string DiagnosticCode { get; }

            public List<IosLiveIconSlot> Slots { get; }
        }

        private sealed class IosLiveIconSlot
        {
            public IosLiveIconSlot(string label, PlatformIcon icon)
            {
                Label = label;
                Icon = icon;
            }

            public string Label { get; }

            public PlatformIcon Icon { get; }
        }
    }
}
