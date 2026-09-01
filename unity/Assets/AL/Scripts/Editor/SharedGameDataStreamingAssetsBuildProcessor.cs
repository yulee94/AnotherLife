#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build;

namespace AL.EditorTools
{
    public sealed class SharedGameDataStreamingAssetsBuildProcessor : BuildPlayerProcessor
    {
        internal const string DestinationPath = "GameData";
        private const int MaximumCatalogCount = 64;

        public override int callbackOrder => -1000;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            if (buildPlayerContext == null)
            {
                throw new ArgumentNullException(nameof(buildPlayerContext));
            }

            KeyValuePair<string, string>[] registrations =
                ResolveCatalogRegistrations(UnityEngine.Application.dataPath);
            for (int i = 0; i < registrations.Length; i++)
            {
                buildPlayerContext.AddAdditionalPathToStreamingAssets(
                    registrations[i].Key,
                    registrations[i].Value);
            }
        }

        internal static string ResolveSourceDirectory(string assetsRoot)
        {
            if (string.IsNullOrWhiteSpace(assetsRoot))
            {
                throw new ArgumentException("An Assets root is required.", nameof(assetsRoot));
            }

            return Path.GetFullPath(Path.Combine(assetsRoot, "AL", "StreamingAssets", DestinationPath));
        }

        internal static string[] ResolveCatalogFiles(string assetsRoot)
        {
            string source = ResolveSourceDirectory(assetsRoot);
            if (!Directory.Exists(source))
            {
                throw new BuildFailedException("Shared GameData source directory is missing: " + source);
            }

            string duplicateRoot = Path.GetFullPath(Path.Combine(assetsRoot, "StreamingAssets", DestinationPath));
            if (Directory.Exists(duplicateRoot))
            {
                throw new BuildFailedException(
                    "Duplicate GameData StreamingAssets root is not allowed: " + duplicateRoot);
            }

            string[] catalogs = Directory
                .GetFiles(source, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (catalogs.Length == 0 || catalogs.Length > MaximumCatalogCount)
            {
                throw new BuildFailedException(
                    "Shared GameData catalog count must be within 1.." + MaximumCatalogCount + ".");
            }

            return catalogs;
        }

        internal static KeyValuePair<string, string>[] ResolveCatalogRegistrations(string assetsRoot)
        {
            string[] catalogs = ResolveCatalogFiles(assetsRoot);
            return catalogs
                .Select(path => new KeyValuePair<string, string>(
                    path,
                    DestinationPath + "/" + Path.GetFileName(path)))
                .ToArray();
        }
    }
}
#endif
