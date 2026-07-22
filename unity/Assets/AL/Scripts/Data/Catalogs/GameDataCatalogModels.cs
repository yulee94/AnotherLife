using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Bounds and stable identifiers for the non-wired game-data catalog foundation.
    /// Production content and service registration are intentionally supplied by later phases.
    /// </summary>
    public static class GameDataCatalogContract
    {
        public const string DefaultGameId = "another-life";
        public const int SupportedManifestSchemaVersion = 1;
        public const int RuntimeCatalogVersion = 1;
        public const int MaximumManifestBytes = 64 * 1024;
        public const int MaximumFamilyBytes = 512 * 1024;
        public const int MaximumAggregateBytes = 4 * 1024 * 1024;
        public const int MaximumArtifacts = 32;
        public const int MaximumRecordsPerFamily = 4096;
        public const int MaximumAliasesPerFamily = 4096;
        public const int MaximumDiagnostics = 256;
        public const int MaximumJsonDepth = 32;
        public const int MaximumJsonNodes = 65536;
        public const int MaximumStringLength = 16384;
        public const int MaximumPropertiesPerObject = 256;
        public const int MaximumItemsPerArray = 8192;
        public const string JsonMediaType = "application/json";
        public const string DiagnosticPrefix = "AL-GDC-";
    }

    public enum GameDataCatalogLifecycleStatus
    {
        Uninitialized = 0,
        Loading = 1,
        Ready = 2,
        ReadyWithOptionalGaps = 3,
        DevelopmentFallback = 4,
        Unavailable = 5,
        Invalid = 6,
        UnsupportedVersion = 7,
        Disposed = 8
    }

    public enum GameDataCatalogLoadStatus
    {
        LoadedPackaged = 0,
        LoadedDevelopmentFallback = 1,
        MissingManifest = 2,
        MissingArtifact = 3,
        ReadFailed = 4,
        MalformedJson = 5,
        InvalidEnvelope = 6,
        UnsupportedVersion = 7,
        HashMismatch = 8,
        InvalidRecord = 9,
        CrossReferenceFailure = 10,
        Cancelled = 11,
        TimedOut = 12,
        Disposed = 13
    }

    public enum GameDataCatalogSourceKind
    {
        Packaged = 0,
        DevelopmentFallback = 1
    }

    public enum GameDataQueryStatus
    {
        Found = 0,
        AliasResolved = 1,
        OptionalAbsent = 2,
        UnknownId = 3,
        CatalogPending = 4,
        CatalogUnavailable = 5,
        CatalogInvalid = 6,
        UnsupportedVersion = 7,
        // Reserved for a future reviewed per-record diagnostic query. Phase B rejects an
        // invalid whole set before publication and therefore returns CatalogInvalid instead.
        RecordInvalid = 8,
        // Reserved for a future partial/reference query contract. Phase B cross-reference
        // failures reject the whole set before publication and therefore return CatalogInvalid.
        ReferenceUnavailable = 9
    }

    public enum GameDataDiagnosticSeverity
    {
        Information = 0,
        Warning = 1,
        Error = 2
    }

    public enum GameDataValueKind
    {
        Null = 0,
        String = 1,
        Number = 2,
        Boolean = 3,
        Object = 4,
        Array = 5
    }

    public enum GameDataCatalogReadStatus
    {
        Succeeded = 0,
        NotFound = 1,
        ReadFailed = 2,
        Cancelled = 3,
        TimedOut = 4,
        Disposed = 5
    }

    public sealed class GameDataCatalogDiagnostic
    {
        public GameDataCatalogDiagnostic(
            string code,
            GameDataDiagnosticSeverity severity,
            string catalogId,
            string family,
            string recordId,
            string fieldPath,
            string messageKey,
            string technicalMessage,
            string action,
            bool blocksFamily,
            bool blocksCatalogSet,
            int artifactOrder,
            int recordOrder)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A diagnostic code is required.", nameof(code));
            }

            Code = code.StartsWith(GameDataCatalogContract.DiagnosticPrefix, StringComparison.Ordinal)
                ? code
                : GameDataCatalogContract.DiagnosticPrefix + code;
            Severity = severity;
            CatalogId = catalogId ?? string.Empty;
            Family = family ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
            MessageKey = messageKey ?? string.Empty;
            TechnicalMessage = technicalMessage ?? string.Empty;
            Action = action ?? string.Empty;
            BlocksFamily = blocksFamily;
            BlocksCatalogSet = blocksCatalogSet;
            ArtifactOrder = artifactOrder;
            RecordOrder = recordOrder;
        }

        public string Code { get; }
        public GameDataDiagnosticSeverity Severity { get; }
        public string CatalogId { get; }
        public string Family { get; }
        public string RecordId { get; }
        public string FieldPath { get; }
        public string MessageKey { get; }
        public string TechnicalMessage { get; }
        public string Action { get; }
        public bool BlocksFamily { get; }
        public bool BlocksCatalogSet { get; }
        public int ArtifactOrder { get; }
        public int RecordOrder { get; }

        public string Fingerprint => string.Join(
            "|",
            Code,
            Severity.ToString(),
            CatalogId,
            Family,
            RecordId,
            FieldPath,
            MessageKey,
            Action,
            BlocksFamily ? "1" : "0",
            BlocksCatalogSet ? "1" : "0");
    }

    public sealed class GameDataArtifactManifest
    {
        internal GameDataArtifactManifest(
            string family,
            string catalogId,
            string relativePath,
            int schemaVersion,
            string contentVersion,
            bool required,
            string sha256,
            string mediaType,
            string sourceMode,
            string sourceRevision,
            int manifestOrder)
        {
            Family = family;
            CatalogId = catalogId;
            RelativePath = relativePath;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Required = required;
            Sha256 = sha256;
            MediaType = mediaType;
            SourceMode = sourceMode;
            SourceRevision = sourceRevision;
            ManifestOrder = manifestOrder;
        }

        public string Family { get; }
        public string CatalogId { get; }
        public string RelativePath { get; }
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public bool Required { get; }
        public string Sha256 { get; }
        public string MediaType { get; }
        public string SourceMode { get; }
        public string SourceRevision { get; }
        public int ManifestOrder { get; }
    }

    public sealed class GameDataCatalogManifest
    {
        internal GameDataCatalogManifest(
            string gameId,
            string catalogSetId,
            int schemaVersion,
            string contentVersion,
            int minimumRuntimeCatalogVersion,
            string sourceRevision,
            IEnumerable<GameDataArtifactManifest> artifacts,
            int byteLength,
            string sha256)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            MinimumRuntimeCatalogVersion = minimumRuntimeCatalogVersion;
            SourceRevision = sourceRevision;
            Artifacts = ImmutableCollections.Freeze(artifacts);
            ByteLength = byteLength;
            Sha256 = sha256;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public int MinimumRuntimeCatalogVersion { get; }
        public string SourceRevision { get; }
        public IReadOnlyList<GameDataArtifactManifest> Artifacts { get; }
        public int ByteLength { get; }
        public string Sha256 { get; }
    }

    public sealed class GameDataCatalogAlias
    {
        internal GameDataCatalogAlias(
            string legacyId,
            string canonicalId,
            int introducedVersion,
            int? retirementVersion,
            string migrationIssue)
        {
            LegacyId = legacyId;
            CanonicalId = canonicalId;
            IntroducedVersion = introducedVersion;
            RetirementVersion = retirementVersion;
            MigrationIssue = migrationIssue;
        }

        public string LegacyId { get; }
        public string CanonicalId { get; }
        public int IntroducedVersion { get; }
        public int? RetirementVersion { get; }
        public string MigrationIssue { get; }
    }

    public abstract class GameDataValue
    {
        internal GameDataValue(GameDataValueKind kind)
        {
            Kind = kind;
        }

        public GameDataValueKind Kind { get; }
    }

    public sealed class GameDataNullValue : GameDataValue
    {
        public static readonly GameDataNullValue Instance = new GameDataNullValue();

        private GameDataNullValue()
            : base(GameDataValueKind.Null)
        {
        }
    }

    public sealed class GameDataStringValue : GameDataValue
    {
        internal GameDataStringValue(string value)
            : base(GameDataValueKind.String)
        {
            Value = value;
        }

        public string Value { get; }
    }

    public sealed class GameDataNumberValue : GameDataValue
    {
        internal GameDataNumberValue(string rawValue, double value)
            : base(GameDataValueKind.Number)
        {
            RawValue = rawValue;
            Value = value;
        }

        public string RawValue { get; }
        public double Value { get; }

        public bool TryGetInt64(out long value)
        {
            return long.TryParse(RawValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }
    }

    public sealed class GameDataBooleanValue : GameDataValue
    {
        internal GameDataBooleanValue(bool value)
            : base(GameDataValueKind.Boolean)
        {
            Value = value;
        }

        public bool Value { get; }
    }

    public sealed class GameDataArrayValue : GameDataValue, IEnumerable<GameDataValue>
    {
        internal GameDataArrayValue(IEnumerable<GameDataValue> items)
            : base(GameDataValueKind.Array)
        {
            Items = ImmutableCollections.Freeze(items);
        }

        public IReadOnlyList<GameDataValue> Items { get; }
        public int Count => Items.Count;

        public IEnumerator<GameDataValue> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class GameDataObjectValue : GameDataValue, IEnumerable<KeyValuePair<string, GameDataValue>>
    {
        internal GameDataObjectValue(IEnumerable<KeyValuePair<string, GameDataValue>> properties)
            : base(GameDataValueKind.Object)
        {
            Properties = ImmutableCollections.FreezeSortedDictionary(properties);
        }

        public IReadOnlyDictionary<string, GameDataValue> Properties { get; }

        public bool TryGetValue(string name, out GameDataValue value)
        {
            return Properties.TryGetValue(name ?? string.Empty, out value);
        }

        public IEnumerator<KeyValuePair<string, GameDataValue>> GetEnumerator()
        {
            return Properties.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class GameDataCatalogRecord
    {
        internal GameDataCatalogRecord(string id, IEnumerable<KeyValuePair<string, GameDataValue>> fields)
        {
            Id = id;
            Fields = ImmutableCollections.FreezeSortedDictionary(fields);
        }

        public string Id { get; }
        public IReadOnlyDictionary<string, GameDataValue> Fields { get; }

        public bool TryGetField(string fieldName, out GameDataValue value)
        {
            return Fields.TryGetValue(fieldName ?? string.Empty, out value);
        }
    }

    public sealed class GameDataFamilyCatalogSnapshot
    {
        private readonly IReadOnlyDictionary<string, GameDataCatalogRecord> recordsById;
        private readonly IReadOnlyDictionary<string, GameDataCatalogAlias> aliasesByLegacyId;

        internal GameDataFamilyCatalogSnapshot(
            string family,
            string catalogId,
            int schemaVersion,
            string contentVersion,
            string sourceRevision,
            string sha256,
            int byteLength,
            IEnumerable<GameDataCatalogRecord> records,
            IEnumerable<GameDataCatalogAlias> aliases)
        {
            Family = family;
            CatalogId = catalogId;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            SourceRevision = sourceRevision;
            Sha256 = sha256;
            ByteLength = byteLength;

            var orderedRecords = new List<GameDataCatalogRecord>(records ?? new GameDataCatalogRecord[0]);
            orderedRecords.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            Records = ImmutableCollections.Freeze(orderedRecords);
            recordsById = ImmutableCollections.IndexSorted(Records, item => item.Id);

            var orderedAliases = new List<GameDataCatalogAlias>(aliases ?? new GameDataCatalogAlias[0]);
            orderedAliases.Sort((left, right) => string.CompareOrdinal(left.LegacyId, right.LegacyId));
            Aliases = ImmutableCollections.Freeze(orderedAliases);
            aliasesByLegacyId = ImmutableCollections.IndexSorted(Aliases, item => item.LegacyId);
        }

        public string Family { get; }
        public string CatalogId { get; }
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string Sha256 { get; }
        public int ByteLength { get; }
        public IReadOnlyList<GameDataCatalogRecord> Records { get; }
        public IReadOnlyList<GameDataCatalogAlias> Aliases { get; }
        public IReadOnlyDictionary<string, GameDataCatalogRecord> RecordsById => recordsById;
        public IReadOnlyDictionary<string, GameDataCatalogAlias> AliasesByLegacyId => aliasesByLegacyId;
    }

    public sealed class GameDataCatalogSetSnapshot
    {
        private readonly IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> familiesById;
        private readonly IReadOnlyDictionary<string, GameDataArtifactManifest> artifactsByFamily;
        private readonly HashSet<string> missingOptionalSet;

        internal GameDataCatalogSetSnapshot(
            string gameId,
            string catalogSetId,
            int schemaVersion,
            string contentVersion,
            string sourceRevision,
            string manifestSha256,
            GameDataCatalogSourceKind sourceKind,
            long revision,
            IEnumerable<GameDataArtifactManifest> artifacts,
            IEnumerable<GameDataFamilyCatalogSnapshot> families,
            IEnumerable<string> missingOptionalFamilies)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            SourceRevision = sourceRevision;
            ManifestSha256 = manifestSha256;
            SourceKind = sourceKind;
            Revision = revision;
            Artifacts = ImmutableCollections.Freeze(artifacts);
            artifactsByFamily = ImmutableCollections.IndexSorted(Artifacts, item => item.Family);
            Families = ImmutableCollections.Freeze(families);
            familiesById = ImmutableCollections.IndexSorted(Families, item => item.Family);

            var gaps = new List<string>(missingOptionalFamilies ?? new string[0]);
            MissingOptionalFamilies = ImmutableCollections.Freeze(gaps);
            missingOptionalSet = new HashSet<string>(gaps, StringComparer.Ordinal);
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string ManifestSha256 { get; }
        public GameDataCatalogSourceKind SourceKind { get; }
        public long Revision { get; }
        public IReadOnlyList<GameDataArtifactManifest> Artifacts { get; }
        public IReadOnlyList<GameDataFamilyCatalogSnapshot> Families { get; }
        public IReadOnlyList<string> MissingOptionalFamilies { get; }
        public IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> FamiliesById => familiesById;

        public GameDataCatalogQueryResult QueryRecord(string family, string id)
        {
            var requestedFamily = family ?? string.Empty;
            var requestedId = id ?? string.Empty;
            GameDataFamilyCatalogSnapshot catalog;
            if (!familiesById.TryGetValue(requestedFamily, out catalog))
            {
                if (missingOptionalSet.Contains(requestedFamily))
                {
                    GameDataArtifactManifest optionalArtifact;
                    artifactsByFamily.TryGetValue(requestedFamily, out optionalArtifact);
                    return GameDataCatalogQueryResult.Empty(
                        GameDataQueryStatus.OptionalAbsent,
                        requestedFamily,
                        requestedId,
                        null,
                        optionalArtifact);
                }

                return GameDataCatalogQueryResult.Empty(GameDataQueryStatus.CatalogUnavailable, requestedFamily, requestedId);
            }

            GameDataCatalogRecord record;
            if (catalog.RecordsById.TryGetValue(requestedId, out record))
            {
                return GameDataCatalogQueryResult.Found(
                    GameDataQueryStatus.Found,
                    requestedFamily,
                    requestedId,
                    requestedId,
                    record,
                    catalog,
                    new GameDataCatalogDiagnostic[0]);
            }

            GameDataCatalogAlias alias;
            if (catalog.AliasesByLegacyId.TryGetValue(requestedId, out alias) &&
                catalog.RecordsById.TryGetValue(alias.CanonicalId, out record))
            {
                var diagnostic = new GameDataCatalogDiagnostic(
                    "QUERY-ALIAS-RESOLVED",
                    GameDataDiagnosticSeverity.Information,
                    catalog.CatalogId,
                    catalog.Family,
                    record.Id,
                    "$.requestedId",
                    "catalog.query.alias_resolved",
                    "The exact legacy ID resolved through the declared alias table.",
                    "Persist the canonical ID through the owning migration path.",
                    false,
                    false,
                    -1,
                    -1);
                return GameDataCatalogQueryResult.Found(
                    GameDataQueryStatus.AliasResolved,
                    requestedFamily,
                    requestedId,
                    alias.CanonicalId,
                    record,
                    catalog,
                    new[] { diagnostic });
            }

            return GameDataCatalogQueryResult.Empty(
                GameDataQueryStatus.UnknownId,
                requestedFamily,
                requestedId,
                null,
                null,
                catalog);
        }

        internal GameDataCatalogSetSnapshot WithRevision(long revision)
        {
            return new GameDataCatalogSetSnapshot(
                GameId,
                CatalogSetId,
                SchemaVersion,
                ContentVersion,
                SourceRevision,
                ManifestSha256,
                SourceKind,
                revision,
                Artifacts,
                Families,
                MissingOptionalFamilies);
        }
    }

    public sealed class GameDataCatalogQueryResult
    {
        private GameDataCatalogQueryResult(
            GameDataQueryStatus status,
            string family,
            string requestedId,
            string canonicalId,
            GameDataCatalogRecord record,
            GameDataFamilyCatalogSnapshot catalog,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics,
            GameDataArtifactManifest artifact = null)
        {
            Status = status;
            Family = family ?? string.Empty;
            RequestedId = requestedId ?? string.Empty;
            CanonicalId = canonicalId ?? string.Empty;
            Record = record;
            CatalogId = catalog != null ? catalog.CatalogId : artifact == null ? string.Empty : artifact.CatalogId;
            ContentVersion = catalog != null ? catalog.ContentVersion : artifact == null ? string.Empty : artifact.ContentVersion;
            SourceRevision = catalog != null ? catalog.SourceRevision : artifact == null ? string.Empty : artifact.SourceRevision;
            Diagnostics = ImmutableCollections.Freeze(diagnostics);
        }

        public GameDataQueryStatus Status { get; }
        public string Family { get; }
        public string RequestedId { get; }
        public string CanonicalId { get; }
        public GameDataCatalogRecord Record { get; }
        public string CatalogId { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public IReadOnlyList<GameDataCatalogDiagnostic> Diagnostics { get; }
        public bool HasRecord => Record != null;

        internal static GameDataCatalogQueryResult Found(
            GameDataQueryStatus status,
            string family,
            string requestedId,
            string canonicalId,
            GameDataCatalogRecord record,
            GameDataFamilyCatalogSnapshot catalog,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics)
        {
            return new GameDataCatalogQueryResult(
                status,
                family,
                requestedId,
                canonicalId,
                record,
                catalog,
                diagnostics,
                null);
        }

        internal static GameDataCatalogQueryResult Empty(
            GameDataQueryStatus status,
            string family,
            string requestedId,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics = null,
            GameDataArtifactManifest artifact = null,
            GameDataFamilyCatalogSnapshot catalog = null)
        {
            return new GameDataCatalogQueryResult(
                status,
                family,
                requestedId,
                string.Empty,
                null,
                catalog,
                diagnostics,
                artifact);
        }
    }

    public sealed class GameDataCatalogManifestValidationResult
    {
        internal GameDataCatalogManifestValidationResult(
            GameDataCatalogLoadStatus status,
            GameDataCatalogManifest manifest,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics)
        {
            Status = status;
            Manifest = manifest;
            Diagnostics = ImmutableCollections.Freeze(diagnostics);
        }

        public GameDataCatalogLoadStatus Status { get; }
        public GameDataCatalogManifest Manifest { get; }
        public IReadOnlyList<GameDataCatalogDiagnostic> Diagnostics { get; }
        public bool IsAccepted => Manifest != null && Diagnostics.Count == 0;
    }

    public sealed class GameDataCatalogLoadResult
    {
        internal GameDataCatalogLoadResult(
            GameDataCatalogLoadStatus status,
            GameDataCatalogSetSnapshot snapshot,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostics = ImmutableCollections.Freeze(diagnostics);
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
        }

        public GameDataCatalogLoadStatus Status { get; }
        public GameDataCatalogSetSnapshot Snapshot { get; }
        public IReadOnlyList<GameDataCatalogDiagnostic> Diagnostics { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset CompletedAtUtc { get; }
        public bool IsSuccess =>
            (Status == GameDataCatalogLoadStatus.LoadedPackaged ||
             Status == GameDataCatalogLoadStatus.LoadedDevelopmentFallback) &&
            Snapshot != null;

        internal GameDataCatalogLoadResult WithSnapshot(GameDataCatalogSetSnapshot snapshot)
        {
            return new GameDataCatalogLoadResult(Status, snapshot, Diagnostics, StartedAtUtc, CompletedAtUtc);
        }
    }

    public sealed class GameDataCatalogArtifactInput
    {
        public GameDataCatalogArtifactInput(
            string relativePath,
            GameDataCatalogReadStatus status,
            byte[] bytes,
            string failureCode)
        {
            if (!Enum.IsDefined(typeof(GameDataCatalogReadStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            RelativePath = relativePath ?? string.Empty;
            Status = status;
            privateBytes = bytes == null ? null : (byte[])bytes.Clone();
            FailureCode = status == GameDataCatalogReadStatus.Succeeded
                ? string.Empty
                : GameDataCatalogFailureCodes.SafeOrDefault(failureCode, status);
        }

        private readonly byte[] privateBytes;

        public string RelativePath { get; }
        public GameDataCatalogReadStatus Status { get; }
        public string FailureCode { get; }
        public int ByteLength => privateBytes == null ? 0 : privateBytes.Length;

        public byte[] CopyBytes()
        {
            return privateBytes == null ? null : (byte[])privateBytes.Clone();
        }

        internal byte[] UnsafeBytes => privateBytes;
    }

    public sealed class GameDataCatalogServiceState
    {
        internal GameDataCatalogServiceState(
            GameDataCatalogLifecycleStatus status,
            GameDataCatalogSetSnapshot snapshot,
            bool isLoading,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            Status = status;
            IsLoading = isLoading;
            CatalogSetId = snapshot == null ? string.Empty : snapshot.CatalogSetId;
            SchemaVersion = snapshot == null ? 0 : snapshot.SchemaVersion;
            ContentVersion = snapshot == null ? string.Empty : snapshot.ContentVersion;
            SourceRevision = snapshot == null ? string.Empty : snapshot.SourceRevision;
            SourceKind = snapshot == null ? GameDataCatalogSourceKind.Packaged : snapshot.SourceKind;
            Revision = snapshot == null ? 0 : snapshot.Revision;
            LoadedArtifactIds = snapshot == null
                ? ImmutableCollections.Freeze(new string[0])
                : ImmutableCollections.Freeze(MapCatalogIds(snapshot.Families));
            MissingOptionalFamilies = snapshot == null
                ? ImmutableCollections.Freeze(new string[0])
                : ImmutableCollections.Freeze(snapshot.MissingOptionalFamilies);
            Diagnostics = ImmutableCollections.Freeze(diagnostics);
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
        }

        public GameDataCatalogLifecycleStatus Status { get; }
        public bool IsLoading { get; }
        public string CatalogSetId { get; }
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public GameDataCatalogSourceKind SourceKind { get; }
        public long Revision { get; }
        public IReadOnlyList<string> LoadedArtifactIds { get; }
        public IReadOnlyList<string> MissingOptionalFamilies { get; }
        public IReadOnlyList<GameDataCatalogDiagnostic> Diagnostics { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset CompletedAtUtc { get; }

        private static IEnumerable<string> MapCatalogIds(IReadOnlyList<GameDataFamilyCatalogSnapshot> families)
        {
            for (var index = 0; index < families.Count; index++)
            {
                yield return families[index].CatalogId;
            }
        }
    }

    internal static class ImmutableCollections
    {
        public static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values)
        {
            var copy = values == null ? new List<T>() : new List<T>(values);
            return Array.AsReadOnly(copy.ToArray());
        }

        public static IReadOnlyDictionary<string, T> FreezeSortedDictionary<T>(
            IEnumerable<KeyValuePair<string, T>> values)
        {
            var copy = new SortedDictionary<string, T>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (var pair in values)
                {
                    copy.Add(pair.Key, pair.Value);
                }
            }

            return new ReadOnlyDictionary<string, T>(copy);
        }

        public static IReadOnlyDictionary<string, T> IndexSorted<T>(
            IEnumerable<T> values,
            Func<T, string> keySelector)
        {
            var copy = new SortedDictionary<string, T>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                copy.Add(keySelector(value), value);
            }

            return new ReadOnlyDictionary<string, T>(copy);
        }
    }
}
