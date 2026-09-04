using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Strict schema and packaged-path contract for the non-wired Warmaster identity catalog.
    /// Purchase values, progression, entitlements, save state, and runtime activation are out of scope.
    /// </summary>
    public static class WarmasterTechnicalCatalogContract
    {
        public const string Family = "warmaster";
        public const int SchemaVersion = 1;
        public const int RequiredSetCount = 1;
        public const int RequiredPieceCount = 10;
        public const string ManifestRelativePath = "al_warmaster_technical_catalog_set.json";

        private static readonly string[] ApprovedNameReferences =
        {
            "warmaster.piece.bannercloak.name",
            "warmaster.piece.command_breastplate.name",
            "warmaster.piece.conquest_medallion.name",
            "warmaster.piece.final_war_plate.name",
            "warmaster.piece.marchwarden_greaves.name",
            "warmaster.piece.oathbound_helm.name",
            "warmaster.piece.siege_gauntlets.name",
            "warmaster.piece.standard_bearer_sash.name",
            "warmaster.piece.vanguard_pauldrons.name",
            "warmaster.piece.war_council_belt.name",
            "warmaster.set.true_warmaster.name"
        };

        private static readonly string[] ApprovedSummaryReferences =
        {
            "warmaster.piece.bannercloak.summary",
            "warmaster.piece.command_breastplate.summary",
            "warmaster.piece.conquest_medallion.summary",
            "warmaster.piece.final_war_plate.summary",
            "warmaster.piece.marchwarden_greaves.summary",
            "warmaster.piece.oathbound_helm.summary",
            "warmaster.piece.siege_gauntlets.summary",
            "warmaster.piece.standard_bearer_sash.summary",
            "warmaster.piece.vanguard_pauldrons.summary",
            "warmaster.piece.war_council_belt.summary",
            "warmaster.set.true_warmaster.summary"
        };

        public static GameDataCatalogSchemaRegistry CreateRegistry()
        {
            var referenceItem = new GameDataCatalogFieldRule(
                "$item",
                GameDataValueKind.String,
                true,
                nonBlank: true,
                stableId: true,
                referenceFamily: Family);
            var schema = new GameDataCatalogFamilySchema(
                Family,
                new[] { SchemaVersion },
                new[]
                {
                    new GameDataCatalogFieldRule(
                        "kind",
                        GameDataValueKind.String,
                        true,
                        nonBlank: true,
                        allowedStringValues: new[] { "piece", "set" }),
                    new GameDataCatalogFieldRule(
                        "owner_set_id",
                        GameDataValueKind.String,
                        true,
                        nonBlank: true,
                        stableId: true,
                        referenceFamily: Family),
                    new GameDataCatalogFieldRule(
                        "name_ref",
                        GameDataValueKind.String,
                        true,
                        nonBlank: true,
                        allowedStringValues: ApprovedNameReferences),
                    new GameDataCatalogFieldRule(
                        "summary_ref",
                        GameDataValueKind.String,
                        true,
                        nonBlank: true,
                        allowedStringValues: ApprovedSummaryReferences),
                    new GameDataCatalogFieldRule(
                        "piece_ids",
                        GameDataValueKind.Array,
                        true,
                        minimumItems: 0,
                        maximumItems: RequiredPieceCount,
                        itemRule: referenceItem)
                },
                allowEmptyRecords: false,
                recordConstraints: new[]
                {
                    new GameDataCatalogRecordConstraint(
                        "warmaster_record_shape",
                        "piece_ids",
                        "WARMASTER-RECORD-SHAPE",
                        "Warmaster set and piece records must use the reviewed identity-only shape.",
                        HasApprovedRecordShape)
                });
            return new GameDataCatalogSchemaRegistry(new[] { schema });
        }

        private static bool? HasApprovedRecordShape(
            string recordId,
            IReadOnlyDictionary<string, GameDataValue> fields)
        {
            string kind;
            string ownerSetId;
            string nameReference;
            string summaryReference;
            GameDataArrayValue pieceIds;
            if (!TryString(fields, "kind", out kind) ||
                !TryString(fields, "owner_set_id", out ownerSetId) ||
                !TryString(fields, "name_ref", out nameReference) ||
                !TryString(fields, "summary_ref", out summaryReference) ||
                !TryArray(fields, "piece_ids", out pieceIds))
            {
                return null;
            }

            if (!MatchesApprovedContent(recordId, nameReference, summaryReference))
            {
                return false;
            }

            if (string.Equals(kind, "set", StringComparison.Ordinal))
            {
                return string.Equals(ownerSetId, recordId, StringComparison.Ordinal) &&
                       pieceIds.Count == RequiredPieceCount;
            }

            return string.Equals(kind, "piece", StringComparison.Ordinal) &&
                   !string.Equals(ownerSetId, recordId, StringComparison.Ordinal) &&
                   pieceIds.Count == 0;
        }

        private static bool MatchesApprovedContent(
            string recordId,
            string nameReference,
            string summaryReference)
        {
            string expectedStem;
            switch (recordId)
            {
                case "prototype_true_warmaster":
                    expectedStem = "warmaster.set.true_warmaster";
                    break;
                case "warmaster_piece_01":
                    expectedStem = "warmaster.piece.oathbound_helm";
                    break;
                case "warmaster_piece_02":
                    expectedStem = "warmaster.piece.vanguard_pauldrons";
                    break;
                case "warmaster_piece_03":
                    expectedStem = "warmaster.piece.command_breastplate";
                    break;
                case "warmaster_piece_04":
                    expectedStem = "warmaster.piece.siege_gauntlets";
                    break;
                case "warmaster_piece_05":
                    expectedStem = "warmaster.piece.standard_bearer_sash";
                    break;
                case "warmaster_piece_06":
                    expectedStem = "warmaster.piece.war_council_belt";
                    break;
                case "warmaster_piece_07":
                    expectedStem = "warmaster.piece.marchwarden_greaves";
                    break;
                case "warmaster_piece_08":
                    expectedStem = "warmaster.piece.bannercloak";
                    break;
                case "warmaster_piece_09":
                    expectedStem = "warmaster.piece.conquest_medallion";
                    break;
                case "warmaster_piece_10":
                    expectedStem = "warmaster.piece.final_war_plate";
                    break;
                default:
                    return false;
            }

            return string.Equals(nameReference, expectedStem + ".name", StringComparison.Ordinal) &&
                   string.Equals(summaryReference, expectedStem + ".summary", StringComparison.Ordinal);
        }

        internal static bool TryString(
            IReadOnlyDictionary<string, GameDataValue> fields,
            string fieldName,
            out string value)
        {
            value = null;
            GameDataValue raw;
            if (fields == null || !fields.TryGetValue(fieldName, out raw))
            {
                return false;
            }

            var text = raw as GameDataStringValue;
            if (text == null || string.IsNullOrWhiteSpace(text.Value))
            {
                return false;
            }

            value = text.Value;
            return true;
        }

        internal static bool TryArray(
            IReadOnlyDictionary<string, GameDataValue> fields,
            string fieldName,
            out GameDataArrayValue value)
        {
            value = null;
            GameDataValue raw;
            if (fields == null || !fields.TryGetValue(fieldName, out raw))
            {
                return false;
            }

            value = raw as GameDataArrayValue;
            return value != null;
        }

        internal static bool TryStringArray(
            IReadOnlyDictionary<string, GameDataValue> fields,
            string fieldName,
            out string[] values)
        {
            values = null;
            GameDataArrayValue array;
            if (!TryArray(fields, fieldName, out array))
            {
                return false;
            }

            var copy = new string[array.Count];
            for (var index = 0; index < array.Count; index++)
            {
                var text = array.Items[index] as GameDataStringValue;
                if (text == null || string.IsNullOrWhiteSpace(text.Value))
                {
                    return false;
                }

                copy[index] = text.Value;
            }

            values = copy;
            return true;
        }
    }

    public sealed class WarmasterTechnicalSetDefinition
    {
        internal WarmasterTechnicalSetDefinition(
            string id,
            string nameReference,
            string summaryReference,
            IEnumerable<string> pieceIds)
        {
            Id = id;
            NameReference = nameReference;
            SummaryReference = summaryReference;
            PieceIds = Freeze(pieceIds);
        }

        public string Id { get; }
        public string NameReference { get; }
        public string SummaryReference { get; }
        public IReadOnlyList<string> PieceIds { get; }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
        {
            return new ReadOnlyCollection<string>(new List<string>(values));
        }
    }

    public sealed class WarmasterTechnicalPieceDefinition
    {
        internal WarmasterTechnicalPieceDefinition(
            string id,
            string setId,
            string nameReference,
            string summaryReference)
        {
            Id = id;
            SetId = setId;
            NameReference = nameReference;
            SummaryReference = summaryReference;
        }

        public string Id { get; }
        public string SetId { get; }
        public string NameReference { get; }
        public string SummaryReference { get; }
    }

    public sealed class WarmasterTechnicalCatalogSnapshot
    {
        private readonly IReadOnlyDictionary<string, WarmasterTechnicalSetDefinition> setsById;
        private readonly IReadOnlyDictionary<string, WarmasterTechnicalPieceDefinition> piecesById;

        private WarmasterTechnicalCatalogSnapshot(
            GameDataCatalogSetSnapshot source,
            GameDataFamilyCatalogSnapshot family,
            IEnumerable<WarmasterTechnicalSetDefinition> sets,
            IEnumerable<WarmasterTechnicalPieceDefinition> pieces)
        {
            GameId = source.GameId;
            CatalogSetId = source.CatalogSetId;
            CatalogId = family.CatalogId;
            SchemaVersion = family.SchemaVersion;
            ContentVersion = family.ContentVersion;
            SourceRevision = family.SourceRevision;
            ManifestSha256 = source.ManifestSha256;
            CatalogSha256 = family.Sha256;
            ByteLength = family.ByteLength;

            var orderedSets = new List<WarmasterTechnicalSetDefinition>(sets);
            orderedSets.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            Sets = new ReadOnlyCollection<WarmasterTechnicalSetDefinition>(orderedSets);
            var setIndex = new SortedDictionary<string, WarmasterTechnicalSetDefinition>(StringComparer.Ordinal);
            foreach (WarmasterTechnicalSetDefinition definition in orderedSets)
            {
                setIndex.Add(definition.Id, definition);
            }
            setsById = new ReadOnlyDictionary<string, WarmasterTechnicalSetDefinition>(setIndex);

            var orderedPieces = new List<WarmasterTechnicalPieceDefinition>(pieces);
            orderedPieces.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            Pieces = new ReadOnlyCollection<WarmasterTechnicalPieceDefinition>(orderedPieces);
            var pieceIndex = new SortedDictionary<string, WarmasterTechnicalPieceDefinition>(StringComparer.Ordinal);
            foreach (WarmasterTechnicalPieceDefinition definition in orderedPieces)
            {
                pieceIndex.Add(definition.Id, definition);
            }
            piecesById = new ReadOnlyDictionary<string, WarmasterTechnicalPieceDefinition>(pieceIndex);
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string CatalogId { get; }
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string ManifestSha256 { get; }
        public string CatalogSha256 { get; }
        public int ByteLength { get; }
        public IReadOnlyList<WarmasterTechnicalSetDefinition> Sets { get; }
        public IReadOnlyList<WarmasterTechnicalPieceDefinition> Pieces { get; }
        public IReadOnlyDictionary<string, WarmasterTechnicalSetDefinition> SetsById => setsById;
        public IReadOnlyDictionary<string, WarmasterTechnicalPieceDefinition> PiecesById => piecesById;

        internal static bool TryCreate(
            GameDataCatalogSetSnapshot source,
            out WarmasterTechnicalCatalogSnapshot catalog,
            out GameDataCatalogDiagnostic diagnostic)
        {
            catalog = null;
            diagnostic = null;
            GameDataFamilyCatalogSnapshot family;
            if (source == null ||
                !source.FamiliesById.TryGetValue(WarmasterTechnicalCatalogContract.Family, out family) ||
                family == null)
            {
                diagnostic = StructureDiagnostic(
                    string.Empty,
                    "$",
                    "The loaded catalog set does not contain the required Warmaster family.");
                return false;
            }

            if (family.Aliases.Count != 0)
            {
                diagnostic = StructureDiagnostic(
                    string.Empty,
                    "$.aliases",
                    "The first Warmaster technical catalog does not permit aliases.");
                return false;
            }

            var sets = new List<WarmasterTechnicalSetDefinition>();
            var pieces = new List<WarmasterTechnicalPieceDefinition>();
            for (var index = 0; index < family.Records.Count; index++)
            {
                GameDataCatalogRecord record = family.Records[index];
                string kind;
                string ownerSetId;
                string nameReference;
                string summaryReference;
                string[] pieceIds;
                if (!WarmasterTechnicalCatalogContract.TryString(record.Fields, "kind", out kind) ||
                    !WarmasterTechnicalCatalogContract.TryString(record.Fields, "owner_set_id", out ownerSetId) ||
                    !WarmasterTechnicalCatalogContract.TryString(record.Fields, "name_ref", out nameReference) ||
                    !WarmasterTechnicalCatalogContract.TryString(record.Fields, "summary_ref", out summaryReference) ||
                    !WarmasterTechnicalCatalogContract.TryStringArray(record.Fields, "piece_ids", out pieceIds))
                {
                    diagnostic = StructureDiagnostic(
                        record.Id,
                        "$.records[" + index + "]",
                        "The Warmaster record could not be converted to an immutable typed definition.");
                    return false;
                }

                if (string.Equals(kind, "set", StringComparison.Ordinal))
                {
                    sets.Add(new WarmasterTechnicalSetDefinition(
                        record.Id,
                        nameReference,
                        summaryReference,
                        pieceIds));
                }
                else if (string.Equals(kind, "piece", StringComparison.Ordinal))
                {
                    pieces.Add(new WarmasterTechnicalPieceDefinition(
                        record.Id,
                        ownerSetId,
                        nameReference,
                        summaryReference));
                }
                else
                {
                    diagnostic = StructureDiagnostic(
                        record.Id,
                        "$.records[" + index + "].kind",
                        "The Warmaster record kind is unsupported.");
                    return false;
                }
            }

            if (sets.Count != WarmasterTechnicalCatalogContract.RequiredSetCount ||
                pieces.Count != WarmasterTechnicalCatalogContract.RequiredPieceCount)
            {
                diagnostic = StructureDiagnostic(
                    string.Empty,
                    "$.records",
                    "The Warmaster technical catalog must contain exactly one set and ten pieces.");
                return false;
            }

            WarmasterTechnicalSetDefinition set = sets[0];
            var membership = new HashSet<string>(StringComparer.Ordinal);
            foreach (string pieceId in set.PieceIds)
            {
                if (!membership.Add(pieceId))
                {
                    diagnostic = StructureDiagnostic(
                        set.Id,
                        "$.records.piece_ids",
                        "Warmaster set membership contains a duplicate piece ID.");
                    return false;
                }
            }

            foreach (WarmasterTechnicalPieceDefinition piece in pieces)
            {
                if (!string.Equals(piece.SetId, set.Id, StringComparison.Ordinal) ||
                    !membership.Contains(piece.Id))
                {
                    diagnostic = StructureDiagnostic(
                        piece.Id,
                        "$.records.owner_set_id",
                        "Every Warmaster piece must belong to, and be listed by, the sole reviewed set.");
                    return false;
                }
            }

            catalog = new WarmasterTechnicalCatalogSnapshot(source, family, sets, pieces);
            return true;
        }

        private static GameDataCatalogDiagnostic StructureDiagnostic(
            string recordId,
            string fieldPath,
            string message)
        {
            return new GameDataCatalogDiagnostic(
                "WARMASTER-STRUCTURE",
                GameDataDiagnosticSeverity.Error,
                "warmaster_technical_v1",
                WarmasterTechnicalCatalogContract.Family,
                recordId,
                fieldPath,
                "catalog.warmaster.structure",
                message,
                "Restore the reviewed one-set, ten-piece technical identity catalog.",
                true,
                true,
                0,
                -1);
        }
    }

    public sealed class WarmasterTechnicalCatalogLoadResult
    {
        internal WarmasterTechnicalCatalogLoadResult(
            GameDataCatalogLoadStatus status,
            WarmasterTechnicalCatalogSnapshot catalog,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics)
        {
            Status = status;
            Catalog = catalog;
            Diagnostics = new ReadOnlyCollection<GameDataCatalogDiagnostic>(
                new List<GameDataCatalogDiagnostic>(diagnostics ?? new GameDataCatalogDiagnostic[0]));
        }

        public GameDataCatalogLoadStatus Status { get; }
        public WarmasterTechnicalCatalogSnapshot Catalog { get; }
        public IReadOnlyList<GameDataCatalogDiagnostic> Diagnostics { get; }
        public bool IsSuccess =>
            (Status == GameDataCatalogLoadStatus.LoadedPackaged ||
             Status == GameDataCatalogLoadStatus.LoadedDevelopmentFallback) &&
            Catalog != null;
    }

    public sealed class WarmasterTechnicalCatalogLoader
    {
        private readonly GameDataCatalogLoader loader;

        public WarmasterTechnicalCatalogLoader()
        {
            loader = new GameDataCatalogLoader(
                new GameDataCatalogValidationPolicy(GameDataCatalogContract.DefaultGameId),
                WarmasterTechnicalCatalogContract.CreateRegistry());
        }

        public IGameDataCatalogLoadOperation BeginLoad(
            IGameDataCatalogSource source,
            GameDataCatalogSourceKind sourceKind,
            Action<WarmasterTechnicalCatalogLoadResult> completion)
        {
            if (completion == null) throw new ArgumentNullException(nameof(completion));
            return loader.BeginLoad(
                source,
                WarmasterTechnicalCatalogContract.ManifestRelativePath,
                sourceKind,
                result =>
                {
                    WarmasterTechnicalCatalogLoadResult converted;
                    try
                    {
                        converted = Convert(result);
                    }
                    catch (Exception exception)
                    {
                        converted = ConversionFailure(exception);
                    }

                    completion(converted);
                });
        }

        private static WarmasterTechnicalCatalogLoadResult Convert(
            GameDataCatalogLoadResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!result.IsSuccess)
            {
                return new WarmasterTechnicalCatalogLoadResult(
                    result.Status,
                    null,
                    result.Diagnostics);
            }

            WarmasterTechnicalCatalogSnapshot catalog;
            GameDataCatalogDiagnostic diagnostic;
            if (!WarmasterTechnicalCatalogSnapshot.TryCreate(
                    result.Snapshot,
                    out catalog,
                    out diagnostic))
            {
                return new WarmasterTechnicalCatalogLoadResult(
                    GameDataCatalogLoadStatus.InvalidRecord,
                    null,
                    new[] { diagnostic });
            }

            return new WarmasterTechnicalCatalogLoadResult(
                result.Status,
                catalog,
                result.Diagnostics);
        }

        private static WarmasterTechnicalCatalogLoadResult ConversionFailure(
            Exception exception)
        {
            var diagnostic = new GameDataCatalogDiagnostic(
                "WARMASTER-CONVERSION",
                GameDataDiagnosticSeverity.Error,
                "warmaster_technical_v1",
                WarmasterTechnicalCatalogContract.Family,
                string.Empty,
                "$.records",
                "catalog.warmaster.conversion",
                "Typed Warmaster catalog conversion failed (" +
                exception.GetType().Name + ").",
                "Restore the reviewed identity catalog and inspect conversion diagnostics.",
                true,
                true,
                0,
                -1);
            return new WarmasterTechnicalCatalogLoadResult(
                GameDataCatalogLoadStatus.InvalidRecord,
                null,
                new[] { diagnostic });
        }
    }

    public enum WarmasterTechnicalDefinitionKind
    {
        None = 0,
        Set = 1,
        Piece = 2
    }

    public enum WarmasterTechnicalCatalogQueryStatus
    {
        Found = 0,
        UnknownDefinition = 1,
        CatalogUnavailable = 2
    }

    public sealed class WarmasterTechnicalCatalogQueryResult
    {
        internal WarmasterTechnicalCatalogQueryResult(
            WarmasterTechnicalCatalogQueryStatus status,
            string requestedId,
            WarmasterTechnicalDefinitionKind kind,
            WarmasterTechnicalSetDefinition set,
            WarmasterTechnicalPieceDefinition piece)
        {
            Status = status;
            RequestedId = requestedId ?? string.Empty;
            Kind = kind;
            Set = set;
            Piece = piece;
        }

        public WarmasterTechnicalCatalogQueryStatus Status { get; }
        public string RequestedId { get; }
        public WarmasterTechnicalDefinitionKind Kind { get; }
        public WarmasterTechnicalSetDefinition Set { get; }
        public WarmasterTechnicalPieceDefinition Piece { get; }
    }

    /// <summary>
    /// Pure read-only resolver. It never invents a definition, creates player state,
    /// applies transactions, or registers itself as production authority.
    /// </summary>
    public sealed class WarmasterTechnicalCatalogResolver
    {
        private readonly WarmasterTechnicalCatalogSnapshot catalog;

        public WarmasterTechnicalCatalogResolver(WarmasterTechnicalCatalogLoadResult loadResult)
        {
            catalog = loadResult != null && loadResult.IsSuccess
                ? loadResult.Catalog
                : null;
        }

        public WarmasterTechnicalCatalogQueryResult Resolve(string id)
        {
            string requestedId = id ?? string.Empty;
            if (catalog == null)
            {
                return new WarmasterTechnicalCatalogQueryResult(
                    WarmasterTechnicalCatalogQueryStatus.CatalogUnavailable,
                    requestedId,
                    WarmasterTechnicalDefinitionKind.None,
                    null,
                    null);
            }

            WarmasterTechnicalSetDefinition set;
            if (catalog.SetsById.TryGetValue(requestedId, out set))
            {
                return new WarmasterTechnicalCatalogQueryResult(
                    WarmasterTechnicalCatalogQueryStatus.Found,
                    requestedId,
                    WarmasterTechnicalDefinitionKind.Set,
                    set,
                    null);
            }

            WarmasterTechnicalPieceDefinition piece;
            if (catalog.PiecesById.TryGetValue(requestedId, out piece))
            {
                return new WarmasterTechnicalCatalogQueryResult(
                    WarmasterTechnicalCatalogQueryStatus.Found,
                    requestedId,
                    WarmasterTechnicalDefinitionKind.Piece,
                    null,
                    piece);
            }

            return new WarmasterTechnicalCatalogQueryResult(
                WarmasterTechnicalCatalogQueryStatus.UnknownDefinition,
                requestedId,
                WarmasterTechnicalDefinitionKind.None,
                null,
                null);
        }
    }
}
