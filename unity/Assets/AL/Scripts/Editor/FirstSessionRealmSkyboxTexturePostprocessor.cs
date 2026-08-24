using System;
using UnityEditor;
using UnityEngine;

namespace AL.Editor
{
    public sealed class FirstSessionRealmSkyboxTexturePostprocessor : AssetPostprocessor
    {
        private const string Root =
            "Assets/AL/Art/Production/FirstUserOnboarding/Environment/";
        private const string Suffix = "_PanoramicSky_Meshy_v001.png";

        public override uint GetVersion()
        {
            return 1u;
        }

        private void OnPreprocessTexture()
        {
            if (!IsSkybox(assetPath))
            {
                return;
            }

            Configure((TextureImporter)assetImporter);
        }

        [MenuItem("Another Life/Build/Apply First Session Realm Skybox Import Settings")]
        public static void ApplyForCli()
        {
            foreach (string realm in new[] { "Stonehold", "Eldergrove", "Crownlands", "Umbral" })
            {
                string path = Root + realm + Suffix;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                {
                    throw new InvalidOperationException(
                        "Required first-session panoramic sky is missing: " + path);
                }

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.SaveAssets();
        }

        private static bool IsSkybox(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(Root, StringComparison.Ordinal) &&
                   path.EndsWith(Suffix, StringComparison.Ordinal);
        }

        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
            importer.anisoLevel = 2;
        }
    }
}
