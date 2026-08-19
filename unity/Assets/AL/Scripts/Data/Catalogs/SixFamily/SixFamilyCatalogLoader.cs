using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Synchronous packaged load of the six-family catalog-set. Missing or invalid
    /// JSON fails closed with the validator's diagnostic codes.
    /// </summary>
    public static class SixFamilyCatalogLoader
    {
        public const string ManifestFileName = "catalog-set.json";
        public const string PackagedRelativeRoot = "GameData";

        public static GameDataCatalogLoadResult LoadFromDirectory(string catalogRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(catalogRootDirectory))
            {
                throw new ArgumentException(
                    "A catalog root directory is required.",
                    nameof(catalogRootDirectory));
            }

            var policy = new GameDataCatalogValidationPolicy(GameDataCatalogContract.DefaultGameId);
            var schemas = GameDataSixFamilySchemas.CreateRegistry();
            var startedAtUtc = DateTimeOffset.UtcNow;

            string root;
            try
            {
                root = Path.GetFullPath(catalogRootDirectory);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return Fail(
                    GameDataCatalogLoadStatus.ReadFailed,
                    "CATALOG-ROOT-INVALID",
                    "The six-family catalog root path is invalid.",
                    "Point the loader at unity/Assets/StreamingAssets/GameData/.",
                    startedAtUtc);
            }

            var manifestPath = Path.Combine(root, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return Fail(
                    GameDataCatalogLoadStatus.MissingManifest,
                    "MANIFEST-MISSING",
                    "The six-family catalog-set.json was not found at '" + root + "'.",
                    "Package the reviewed six-family catalog-set under StreamingAssets/GameData/.",
                    startedAtUtc);
            }

            byte[] manifestBytes;
            try
            {
                manifestBytes = File.ReadAllBytes(manifestPath);
            }
            catch (Exception exception)
            {
                return Fail(
                    GameDataCatalogLoadStatus.ReadFailed,
                    "MANIFEST-READ-FAILED",
                    "The six-family catalog-set.json could not be read (" + exception.GetType().Name + ").",
                    "Inspect file permissions and package the reviewed catalog-set.",
                    startedAtUtc);
            }

            var manifestResult = GameDataCatalogValidator.ValidateManifest(manifestBytes, policy);
            if (!manifestResult.IsAccepted)
            {
                return new GameDataCatalogLoadResult(
                    manifestResult.Status,
                    null,
                    manifestResult.Diagnostics,
                    startedAtUtc,
                    DateTimeOffset.UtcNow);
            }

            var inputs = new List<GameDataCatalogArtifactInput>(manifestResult.Manifest.Artifacts.Count);
            foreach (var artifact in manifestResult.Manifest.Artifacts)
            {
                inputs.Add(ReadArtifact(root, artifact.RelativePath));
            }

            return GameDataCatalogValidator.ValidateCatalogSet(
                manifestResult.Manifest,
                inputs,
                schemas,
                policy,
                GameDataCatalogSourceKind.Packaged,
                startedAtUtc,
                DateTimeOffset.UtcNow);
        }

        public static GameDataCatalogSetSnapshot LoadRequiredSnapshot(string catalogRootDirectory)
        {
            var result = LoadFromDirectory(catalogRootDirectory);
            if (result.IsSuccess)
            {
                return result.Snapshot;
            }

            throw new InvalidOperationException(FormatFailure(result));
        }

        public static string FormatFailure(GameDataCatalogLoadResult result)
        {
            if (result == null)
            {
                return "Six-family catalog load failed with no result.";
            }

            var builder = new StringBuilder();
            builder.Append("Six-family catalog load failed (");
            builder.Append(result.Status);
            builder.Append(").");
            var diagnostics = result.Diagnostics;
            if (diagnostics == null || diagnostics.Count == 0)
            {
                return builder.ToString();
            }

            var limit = Math.Min(diagnostics.Count, 8);
            for (var index = 0; index < limit; index++)
            {
                var diagnostic = diagnostics[index];
                builder.Append(" [");
                builder.Append(diagnostic.Code);
                builder.Append("] ");
                builder.Append(diagnostic.TechnicalMessage);
                if (!string.IsNullOrEmpty(diagnostic.Action))
                {
                    builder.Append(" ");
                    builder.Append(diagnostic.Action);
                }
            }

            if (diagnostics.Count > limit)
            {
                builder.Append(" (+");
                builder.Append(diagnostics.Count - limit);
                builder.Append(" more)");
            }

            return builder.ToString();
        }

        private static GameDataCatalogArtifactInput ReadArtifact(string root, string relativePath)
        {
            string fullPath;
            try
            {
                var platformRelative = (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                fullPath = Path.GetFullPath(Path.Combine(root, platformRelative));
            }
            catch (Exception)
            {
                return new GameDataCatalogArtifactInput(
                    relativePath,
                    GameDataCatalogReadStatus.ReadFailed,
                    null,
                    "invalid_path");
            }

            var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? root
                : root + Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(rootPrefix, comparison))
            {
                return new GameDataCatalogArtifactInput(
                    relativePath,
                    GameDataCatalogReadStatus.ReadFailed,
                    null,
                    "path_escape");
            }

            if (!File.Exists(fullPath))
            {
                return new GameDataCatalogArtifactInput(
                    relativePath,
                    GameDataCatalogReadStatus.NotFound,
                    null,
                    "not_found");
            }

            try
            {
                return new GameDataCatalogArtifactInput(
                    relativePath,
                    GameDataCatalogReadStatus.Succeeded,
                    File.ReadAllBytes(fullPath),
                    string.Empty);
            }
            catch (Exception)
            {
                return new GameDataCatalogArtifactInput(
                    relativePath,
                    GameDataCatalogReadStatus.ReadFailed,
                    null,
                    "read_failed");
            }
        }

        private static GameDataCatalogLoadResult Fail(
            GameDataCatalogLoadStatus status,
            string code,
            string message,
            string action,
            DateTimeOffset startedAtUtc)
        {
            var diagnostic = new GameDataCatalogDiagnostic(
                code,
                GameDataDiagnosticSeverity.Error,
                string.Empty,
                string.Empty,
                string.Empty,
                "$",
                "catalog.six_family.load_failed",
                message,
                action,
                true,
                true,
                -1,
                -1);
            return new GameDataCatalogLoadResult(
                status,
                null,
                new[] { diagnostic },
                startedAtUtc,
                DateTimeOffset.UtcNow);
        }
    }
}
