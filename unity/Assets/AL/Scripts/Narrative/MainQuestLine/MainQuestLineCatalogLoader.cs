using System;
using System.IO;
using UnityEngine;

namespace AL.Narrative.MainQuestLine
{
    public static class MainQuestLineCatalogLoader
    {
        public static bool TryLoadCanonical(
            out MainQuestLineCatalog catalog,
            out MainQuestLineDiagnostic diagnostic)
        {
            return TryLoadFromPath(ResolveCatalogPath(), out catalog, out diagnostic);
        }

        public static bool TryLoadFromPath(
            string path,
            out MainQuestLineCatalog catalog,
            out MainQuestLineDiagnostic diagnostic)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING",
                    "Packaged main-quest runtime catalog is missing.",
                    MainQuestLineContract.RelativePath,
                    string.IsNullOrWhiteSpace(path) ? "blank" : path);
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING",
                    "Packaged main-quest runtime catalog could not be read.",
                    "readable catalog",
                    exception.GetType().Name);
                return false;
            }

            return MainQuestLineCatalog.TryParse(bytes, out catalog, out diagnostic);
        }

        public static bool TryLoadFromBytes(
            byte[] bytes,
            out MainQuestLineCatalog catalog,
            out MainQuestLineDiagnostic diagnostic)
        {
            return MainQuestLineCatalog.TryParse(bytes, out catalog, out diagnostic);
        }

        public static string ResolveGameDataDirectory()
        {
            if (Application.isEditor)
            {
                return Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData");
            }

            return Path.Combine(
                (Application.streamingAssetsPath ?? string.Empty).TrimEnd('/', '\\'),
                "GameData");
        }

        public static string ResolveCatalogPath()
        {
            return Path.Combine(ResolveGameDataDirectory(), MainQuestLineContract.FileName);
        }

        public static string ResolveNvs01CatalogPath()
        {
            if (Application.isEditor)
            {
                return Path.Combine(
                    Application.dataPath,
                    "StreamingAssets",
                    "AL",
                    "Narrative",
                    "OMEN_1.catalog.json");
            }

            return Path.Combine(
                (Application.streamingAssetsPath ?? string.Empty).TrimEnd('/', '\\'),
                "AL",
                "Narrative",
                "OMEN_1.catalog.json");
        }

        public static string HashFileOrEmpty(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return string.Empty;
            }

            return MainQuestLineCatalog.ComputeSha256(File.ReadAllBytes(path));
        }
    }
}
