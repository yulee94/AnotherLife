using System;
using System.Collections.Generic;
using System.Text;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Loads one six-family envelope (realms, buildings, research, troops,
    /// champions, skills) through <see cref="GameDataSixFamilySchemas"/>.
    /// Missing or invalid JSON fails closed.
    /// </summary>
    public static class SixFamilyCatalogLoader
    {
        public const string SourceRevision = "t_5e063078";
        public const string ContentVersion = "1.0.0";

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly GameDataCatalogValidationPolicy Policy =
            new GameDataCatalogValidationPolicy(GameDataCatalogContract.DefaultGameId);
        private static readonly GameDataCatalogSchemaRegistry Schemas =
            GameDataSixFamilySchemas.CreateRegistry();

        public static bool TryLoad(
            string family,
            string json,
            out GameDataFamilyCatalogSnapshot snapshot,
            out string diagnosticCode)
        {
            snapshot = null;
            diagnosticCode = "AL-GDC-MISSING";
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            return TryLoad(family, Utf8.GetBytes(json), out snapshot, out diagnosticCode);
        }

        public static bool TryLoad(
            string family,
            byte[] utf8,
            out GameDataFamilyCatalogSnapshot snapshot,
            out string diagnosticCode)
        {
            snapshot = null;
            IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> set;
            if (!TryLoadSet(
                    new[] { new KeyValuePair<string, byte[]>(family, utf8) },
                    out set,
                    out diagnosticCode))
            {
                return false;
            }

            return set.TryGetValue(family, out snapshot) && snapshot != null;
        }

        public static bool TryLoadSet(
            IReadOnlyList<KeyValuePair<string, string>> families,
            out IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> snapshots,
            out string diagnosticCode)
        {
            snapshots = null;
            diagnosticCode = "AL-GDC-MISSING";
            if (families == null || families.Count == 0)
            {
                return false;
            }

            var encoded = new KeyValuePair<string, byte[]>[families.Count];
            for (var index = 0; index < families.Count; index++)
            {
                var pair = families[index];
                if (string.IsNullOrEmpty(pair.Value))
                {
                    return false;
                }

                encoded[index] = new KeyValuePair<string, byte[]>(pair.Key, Utf8.GetBytes(pair.Value));
            }

            return TryLoadSet(encoded, out snapshots, out diagnosticCode);
        }

        public static bool TryLoadSet(
            IReadOnlyList<KeyValuePair<string, byte[]>> families,
            out IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> snapshots,
            out string diagnosticCode)
        {
            snapshots = null;
            diagnosticCode = "AL-GDC-MISSING";
            if (families == null || families.Count == 0)
            {
                return false;
            }

            var artifacts = new List<string>();
            var inputs = new List<GameDataCatalogArtifactInput>(families.Count);
            for (var index = 0; index < families.Count; index++)
            {
                var pair = families[index];
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || pair.Value.Length == 0)
                {
                    return false;
                }

                if (!IsSixFamily(pair.Key))
                {
                    diagnosticCode = "AL-GDC-FAMILY-UNSUPPORTED";
                    return false;
                }

                var relativePath = pair.Key + ".v1.json";
                var catalogId = pair.Key + "_v1";
                var sha256 = GameDataCatalogValidator.ComputeSha256(pair.Value);
                artifacts.Add(BuildArtifact(pair.Key, catalogId, relativePath, sha256));
                inputs.Add(
                    new GameDataCatalogArtifactInput(
                        relativePath,
                        GameDataCatalogReadStatus.Succeeded,
                        pair.Value,
                        string.Empty));
            }

            var manifestBytes = Utf8.GetBytes(BuildManifest(artifacts));
            var manifestResult = GameDataCatalogValidator.ValidateManifest(manifestBytes, Policy);
            if (!manifestResult.IsAccepted)
            {
                diagnosticCode = FirstCode(manifestResult.Diagnostics, "AL-GDC-MANIFEST");
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            var result = GameDataCatalogValidator.ValidateCatalogSet(
                manifestResult.Manifest,
                inputs,
                Schemas,
                Policy,
                GameDataCatalogSourceKind.Packaged,
                now,
                now);
            if (!result.IsSuccess || result.Snapshot == null)
            {
                diagnosticCode = FirstCode(result.Diagnostics, "AL-GDC-INVALID");
                return false;
            }

            snapshots = result.Snapshot.FamiliesById;
            diagnosticCode = "AL-GDC-READY";
            return true;
        }

        public static bool IsSixFamily(string family)
        {
            for (var index = 0; index < GameDataSixFamilySchemas.FamilyOrder.Count; index++)
            {
                if (string.Equals(
                        GameDataSixFamilySchemas.FamilyOrder[index],
                        family,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FirstCode(
            IReadOnlyList<GameDataCatalogDiagnostic> diagnostics,
            string fallback)
        {
            if (diagnostics != null)
            {
                for (var index = 0; index < diagnostics.Count; index++)
                {
                    var code = diagnostics[index].Code;
                    if (!string.IsNullOrEmpty(code))
                    {
                        return code.StartsWith("AL-GDC-", StringComparison.Ordinal)
                            ? code
                            : "AL-GDC-" + code;
                    }
                }
            }

            return fallback;
        }

        private static string BuildArtifact(
            string family,
            string catalogId,
            string relativePath,
            string sha256)
        {
            return
                "{\"family\":\"" + family +
                "\",\"catalogId\":\"" + catalogId +
                "\",\"relativePath\":\"" + relativePath +
                "\",\"schemaVersion\":1,\"contentVersion\":\"" + ContentVersion +
                "\",\"required\":true,\"sha256\":\"" + sha256 +
                "\",\"mediaType\":\"application/json\",\"sourceMode\":\"authored\",\"sourceRevision\":\"" +
                SourceRevision +
                "\"}";
        }

        private static string BuildManifest(IReadOnlyList<string> artifacts)
        {
            var joined = new StringBuilder();
            for (var index = 0; index < artifacts.Count; index++)
            {
                if (index > 0)
                {
                    joined.Append(",\n    ");
                }

                joined.Append(artifacts[index]);
            }

            return
                "{\n" +
                "  \"gameId\":\"another-life\",\n" +
                "  \"catalogSetId\":\"catalog_set_six_family\",\n" +
                "  \"schemaVersion\":1,\n" +
                "  \"contentVersion\":\"" + ContentVersion + "\",\n" +
                "  \"minimumRuntimeCatalogVersion\":1,\n" +
                "  \"sourceRevision\":\"" + SourceRevision + "\",\n" +
                "  \"artifacts\":[\n    " +
                joined +
                "\n  ]\n" +
                "}\n";
        }
    }
}
