using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AL.Editor
{
    public sealed class FirstSessionPbrTexturePostprocessor : AssetPostprocessor
    {
        private const string PacketRoot =
            "Assets/AL/Art/Production/FirstUserOnboarding/";

        [MenuItem("Another Life/Build/Apply First Session PBR Texture Settings")]
        public static void ApplyForCli()
        {
            foreach (string path in RequiredTexturePaths())
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.SaveAssets();
        }

        private void OnPreprocessTexture()
        {
            if (!IsPacketPbrTexture(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            string fileName = Path.GetFileName(assetPath);
            bool normal = fileName.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0;
            bool packed = fileName.IndexOf(
                              "metallic_smoothness",
                              StringComparison.OrdinalIgnoreCase) >= 0 ||
                          fileName.IndexOf(
                              "MetallicSmoothness",
                              StringComparison.OrdinalIgnoreCase) >= 0;
            bool linearData = normal || packed ||
                              fileName.IndexOf("metallic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              fileName.IndexOf("roughness", StringComparison.OrdinalIgnoreCase) >= 0;

            importer.textureType = normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.sRGBTexture = !linearData;
            importer.alphaSource = packed
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        public override uint GetVersion()
        {
            return 2u;
        }

        private static bool IsPacketPbrTexture(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith(PacketRoot, StringComparison.Ordinal))
            {
                return false;
            }

            string fileName = Path.GetFileName(path);
            bool supportedName =
                string.Equals(fileName, "base_color.png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "emission.png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "normal.png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "metallic.png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "roughness.png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "metallic_smoothness.png", StringComparison.OrdinalIgnoreCase);
            if (path.Contains("_textures/") && supportedName)
            {
                return true;
            }

            return path.Contains("Environment/Neutral_Covenant_Flagstone_") &&
                   (fileName.Contains("Albedo") ||
                    fileName.Contains("Normal") ||
                    fileName.Contains("Metallic") ||
                    fileName.Contains("Roughness"));
        }

        private static IEnumerable<string> RequiredTexturePaths()
        {
            string[] roots =
            {
                PacketRoot + "Characters/Crownlands_Champion_Male_Base_Meshy6_v001_textures/",
                PacketRoot + "Characters/Crownlands_Champion_Female_Base_Meshy6_v001_textures/",
                PacketRoot + "Enemies/Covenant_Sentinel_Meshy6_v001_textures/",
                PacketRoot + "Environment/Stonehold_CapitalHall_Meshy6_v001_textures/",
                PacketRoot + "Environment/Eldergrove_CapitalHall_Meshy6_v001_textures/",
                PacketRoot + "Environment/Crownlands_CapitalHall_Meshy6_v001_textures/",
                PacketRoot + "Environment/Umbral_CapitalHall_Meshy6_v001_textures/"
            };
            string[] names =
            {
                "base_color.png",
                "emission.png",
                "normal.png",
                "metallic.png",
                "roughness.png",
                "metallic_smoothness.png"
            };

            foreach (string root in roots)
            {
                foreach (string name in names)
                {
                    yield return root + name;
                }
            }

            string floorRoot = PacketRoot + "Environment/Neutral_Covenant_Flagstone_";
            yield return floorRoot + "Albedo_Meshy_v001.png";
            yield return floorRoot + "Normal_Derived_v001.png";
            yield return floorRoot + "Metallic_Derived_v001.png";
            yield return floorRoot + "Roughness_Derived_v001.png";
            yield return floorRoot + "MetallicSmoothness_Derived_v001.png";
        }
    }
}
