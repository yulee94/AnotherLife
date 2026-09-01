using System;
using System.Collections.Generic;
using System.Text;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Loads one flattened WIRE family envelope through the registered
    /// option-C schemas. SKIP families are not accepted.
    /// </summary>
    public static class WireFamilyCatalogLoader
    {
        public const string SourceRevision = "t_d4892ee5";
        public const string ContentVersion = "1.0.0";

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly GameDataCatalogValidationPolicy Policy =
            new GameDataCatalogValidationPolicy(GameDataCatalogContract.DefaultGameId);
        private static readonly GameDataCatalogSchemaRegistry Schemas =
            GameDataWireFamilySchemas.CreateRegistry();

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
            diagnosticCode = "AL-GDC-MISSING";
            if (string.IsNullOrEmpty(family) || utf8 == null || utf8.Length == 0)
            {
                return false;
            }

            if (!IsWiredFamily(family))
            {
                diagnosticCode = "AL-GDC-FAMILY-UNSUPPORTED";
                return false;
            }

            var relativePath = family + ".v1.json";
            var catalogId = family + "_v1";
            var sha256 = GameDataCatalogValidator.ComputeSha256(utf8);
            var manifestBytes = Utf8.GetBytes(BuildManifest(family, catalogId, relativePath, sha256));
            var manifestResult = GameDataCatalogValidator.ValidateManifest(manifestBytes, Policy);
            if (!manifestResult.IsAccepted)
            {
                diagnosticCode = FirstCode(manifestResult.Diagnostics, "AL-GDC-MANIFEST");
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            var result = GameDataCatalogValidator.ValidateCatalogSet(
                manifestResult.Manifest,
                new[]
                {
                    new GameDataCatalogArtifactInput(
                        relativePath,
                        GameDataCatalogReadStatus.Succeeded,
                        utf8,
                        string.Empty)
                },
                Schemas,
                Policy,
                GameDataCatalogSourceKind.Packaged,
                now,
                now);
            if (!result.IsSuccess ||
                result.Snapshot == null ||
                !result.Snapshot.FamiliesById.TryGetValue(family, out snapshot) ||
                snapshot == null)
            {
                snapshot = null;
                diagnosticCode = FirstCode(result.Diagnostics, "AL-GDC-INVALID");
                return false;
            }

            diagnosticCode = "AL-GDC-READY";
            return true;
        }

        public static bool IsWiredFamily(string family)
        {
            for (var index = 0; index < GameDataWireFamilySchemas.FamilyOrder.Count; index++)
            {
                if (string.Equals(
                        GameDataWireFamilySchemas.FamilyOrder[index],
                        family,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ConsumerId(GameDataCatalogRecord record)
        {
            string legacyId;
            return TryGetString(record, "legacy_id", out legacyId) ? legacyId : record.Id;
        }

        public static bool TryGetKind(GameDataCatalogRecord record, out string kind)
        {
            return TryGetString(record, "kind", out kind);
        }

        public static bool TryGetString(GameDataCatalogRecord record, string field, out string value)
        {
            value = null;
            GameDataValue raw;
            if (record == null || !record.TryGetField(field, out raw))
            {
                return false;
            }

            var text = raw as GameDataStringValue;
            if (text == null || string.IsNullOrEmpty(text.Value))
            {
                return false;
            }

            value = text.Value;
            return true;
        }

        public static bool TryGetBool(GameDataCatalogRecord record, string field, out bool value)
        {
            value = false;
            GameDataValue raw;
            if (record == null || !record.TryGetField(field, out raw))
            {
                return false;
            }

            var flag = raw as GameDataBooleanValue;
            if (flag == null)
            {
                return false;
            }

            value = flag.Value;
            return true;
        }

        public static bool TryGetInt(GameDataCatalogRecord record, string field, out int value)
        {
            value = 0;
            GameDataValue raw;
            if (record == null || !record.TryGetField(field, out raw))
            {
                return false;
            }

            var number = raw as GameDataNumberValue;
            long parsed;
            if (number == null || !number.TryGetInt64(out parsed) || parsed < int.MinValue || parsed > int.MaxValue)
            {
                return false;
            }

            value = (int)parsed;
            return true;
        }

        public static bool TryGetFloat(GameDataCatalogRecord record, string field, out float value)
        {
            value = 0f;
            GameDataValue raw;
            if (record == null || !record.TryGetField(field, out raw))
            {
                return false;
            }

            var number = raw as GameDataNumberValue;
            if (number == null)
            {
                return false;
            }

            value = (float)number.Value;
            return true;
        }

        public static bool TryGetStringArray(GameDataCatalogRecord record, string field, out string[] values)
        {
            values = null;
            GameDataArrayValue array;
            if (!TryGetArray(record, field, out array))
            {
                return false;
            }

            var copy = new string[array.Count];
            for (var index = 0; index < array.Count; index++)
            {
                var text = array.Items[index] as GameDataStringValue;
                if (text == null || string.IsNullOrEmpty(text.Value))
                {
                    return false;
                }

                copy[index] = text.Value;
            }

            values = copy;
            return true;
        }

        public static bool TryGetFloatArray(GameDataCatalogRecord record, string field, out float[] values)
        {
            values = null;
            GameDataArrayValue array;
            if (!TryGetArray(record, field, out array))
            {
                return false;
            }

            var copy = new float[array.Count];
            for (var index = 0; index < array.Count; index++)
            {
                var number = array.Items[index] as GameDataNumberValue;
                if (number == null)
                {
                    return false;
                }

                copy[index] = (float)number.Value;
            }

            values = copy;
            return true;
        }

        public static IList<GameDataCatalogRecord> RecordsOfKind(
            GameDataFamilyCatalogSnapshot family,
            string kind)
        {
            var matches = new List<GameDataCatalogRecord>();
            if (family == null || string.IsNullOrEmpty(kind))
            {
                return matches;
            }

            for (var index = 0; index < family.Records.Count; index++)
            {
                var record = family.Records[index];
                string recordKind;
                if (TryGetKind(record, out recordKind) &&
                    string.Equals(recordKind, kind, StringComparison.Ordinal))
                {
                    matches.Add(record);
                }
            }

            return matches;
        }

        public static bool TryGetRecord(
            GameDataFamilyCatalogSnapshot family,
            string id,
            out GameDataCatalogRecord record)
        {
            record = null;
            if (family == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (family.RecordsById.TryGetValue(id, out record))
            {
                return true;
            }

            GameDataCatalogAlias alias;
            return family.AliasesByLegacyId.TryGetValue(id, out alias) &&
                   family.RecordsById.TryGetValue(alias.CanonicalId, out record);
        }

        private static bool TryGetArray(
            GameDataCatalogRecord record,
            string field,
            out GameDataArrayValue array)
        {
            array = null;
            GameDataValue raw;
            if (record == null || !record.TryGetField(field, out raw))
            {
                return false;
            }

            array = raw as GameDataArrayValue;
            return array != null;
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

        private static string BuildManifest(
            string family,
            string catalogId,
            string relativePath,
            string sha256)
        {
            return
                "{\n" +
                "  \"gameId\":\"another-life\",\n" +
                "  \"catalogSetId\":\"catalog_set_wire_" + family + "\",\n" +
                "  \"schemaVersion\":1,\n" +
                "  \"contentVersion\":\"" + ContentVersion + "\",\n" +
                "  \"minimumRuntimeCatalogVersion\":1,\n" +
                "  \"sourceRevision\":\"" + SourceRevision + "\",\n" +
                "  \"artifacts\":[\n" +
                "    {\"family\":\"" + family +
                "\",\"catalogId\":\"" + catalogId +
                "\",\"relativePath\":\"" + relativePath +
                "\",\"schemaVersion\":1,\"contentVersion\":\"" + ContentVersion +
                "\",\"required\":true,\"sha256\":\"" + sha256 +
                "\",\"mediaType\":\"application/json\",\"sourceMode\":\"authored\",\"sourceRevision\":\"" +
                SourceRevision +
                "\"}\n" +
                "  ]\n" +
                "}\n";
        }
    }
}
