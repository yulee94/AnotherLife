using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Runtime-supported policy. Family support is injected explicitly through a schema registry;
    /// syntactically valid unknown families can therefore never claim implemented support.
    /// </summary>
    public sealed class GameDataCatalogValidationPolicy
    {
        private readonly HashSet<int> supportedManifestVersions;
        private readonly HashSet<string> supportedSourceModes;

        public GameDataCatalogValidationPolicy(
            string expectedGameId,
            IEnumerable<int> supportedManifestVersions = null,
            int runtimeCatalogVersion = GameDataCatalogContract.RuntimeCatalogVersion,
            IEnumerable<string> supportedSourceModes = null)
        {
            if (string.IsNullOrWhiteSpace(expectedGameId))
            {
                throw new ArgumentException("An exact expected game ID is required.", nameof(expectedGameId));
            }

            if (runtimeCatalogVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeCatalogVersion));
            }

            ExpectedGameId = expectedGameId;
            RuntimeCatalogVersion = runtimeCatalogVersion;
            this.supportedManifestVersions = new HashSet<int>(
                supportedManifestVersions ?? new[] { GameDataCatalogContract.SupportedManifestSchemaVersion });
            this.supportedSourceModes = new HashSet<string>(
                supportedSourceModes ?? new[] { "authored", "generated" },
                StringComparer.Ordinal);

            if (this.supportedManifestVersions.Count == 0)
            {
                throw new ArgumentException("At least one positive manifest schema version is required.", nameof(supportedManifestVersions));
            }
            foreach (var version in this.supportedManifestVersions)
            {
                if (version <= 0)
                {
                    throw new ArgumentException("Manifest schema versions must be positive.", nameof(supportedManifestVersions));
                }
            }
        }

        public string ExpectedGameId { get; }
        public int RuntimeCatalogVersion { get; }
        public int MaximumManifestBytes => GameDataCatalogContract.MaximumManifestBytes;
        public int MaximumFamilyBytes => GameDataCatalogContract.MaximumFamilyBytes;
        public int MaximumAggregateBytes => GameDataCatalogContract.MaximumAggregateBytes;
        public int MaximumArtifacts => GameDataCatalogContract.MaximumArtifacts;
        public int MaximumRecordsPerFamily => GameDataCatalogContract.MaximumRecordsPerFamily;
        public int MaximumAliasesPerFamily => GameDataCatalogContract.MaximumAliasesPerFamily;
        public int MaximumDiagnostics => GameDataCatalogContract.MaximumDiagnostics;

        public bool SupportsManifestVersion(int version)
        {
            return version > 0 && supportedManifestVersions.Contains(version);
        }

        public bool SupportsSourceMode(string sourceMode)
        {
            return sourceMode != null && supportedSourceModes.Contains(sourceMode);
        }
    }

    public sealed class GameDataCatalogFieldRule
    {
        private readonly IReadOnlyDictionary<string, GameDataCatalogFieldRule> objectFieldsByName;
        private readonly HashSet<string> allowedStringSet;

        public GameDataCatalogFieldRule(
            string name,
            GameDataValueKind kind,
            bool required,
            bool allowNull = false,
            bool nonBlank = false,
            bool stableId = false,
            bool integerOnly = false,
            double? minimumNumber = null,
            double? maximumNumber = null,
            int minimumItems = 0,
            int maximumItems = GameDataCatalogContract.MaximumItemsPerArray,
            string referenceFamily = null,
            IEnumerable<string> allowedStringValues = null,
            IEnumerable<GameDataCatalogFieldRule> objectFields = null,
            GameDataCatalogFieldRule itemRule = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A field-rule name is required.", nameof(name));
            }

            if (minimumItems < 0 || maximumItems < minimumItems || maximumItems > GameDataCatalogContract.MaximumItemsPerArray)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumItems));
            }

            if (minimumNumber.HasValue && maximumNumber.HasValue && minimumNumber.Value > maximumNumber.Value)
            {
                throw new ArgumentException("The minimum number cannot exceed the maximum number.");
            }

            if (!string.IsNullOrEmpty(referenceFamily) && kind != GameDataValueKind.String)
            {
                throw new ArgumentException("Reference rules must target string values.", nameof(referenceFamily));
            }

            if (integerOnly && kind != GameDataValueKind.Number)
            {
                throw new ArgumentException("Integer-only applies only to number fields.", nameof(integerOnly));
            }

            if (kind == GameDataValueKind.Array && itemRule == null)
            {
                throw new ArgumentException("Array rules require an item rule.", nameof(itemRule));
            }

            if (kind != GameDataValueKind.Array && itemRule != null)
            {
                throw new ArgumentException("Only array rules may define an item rule.", nameof(itemRule));
            }

            Name = name;
            Kind = kind;
            Required = required;
            AllowNull = allowNull;
            NonBlank = nonBlank;
            StableId = stableId;
            IntegerOnly = integerOnly;
            MinimumNumber = minimumNumber;
            MaximumNumber = maximumNumber;
            MinimumItems = minimumItems;
            MaximumItems = maximumItems;
            ReferenceFamily = referenceFamily ?? string.Empty;
            ItemRule = itemRule;

            var fields = new SortedDictionary<string, GameDataCatalogFieldRule>(StringComparer.Ordinal);
            if (objectFields != null)
            {
                foreach (var field in objectFields)
                {
                    if (field == null) throw new ArgumentException("Object field rules cannot contain null.", nameof(objectFields));
                    fields.Add(field.Name, field);
                }
            }

            if (kind != GameDataValueKind.Object && fields.Count > 0)
            {
                throw new ArgumentException("Only object rules may define child fields.", nameof(objectFields));
            }

            objectFieldsByName = new ReadOnlyDictionary<string, GameDataCatalogFieldRule>(fields);
            ObjectFields = ImmutableCollections.Freeze(fields.Values);

            allowedStringSet = new HashSet<string>(StringComparer.Ordinal);
            var orderedAllowedStrings = new SortedSet<string>(StringComparer.Ordinal);
            if (allowedStringValues != null)
            {
                foreach (var value in allowedStringValues)
                {
                    if (value == null) throw new ArgumentException("Allowed string values cannot contain null.", nameof(allowedStringValues));
                    if (!allowedStringSet.Add(value)) throw new ArgumentException("Allowed string values must be unique.", nameof(allowedStringValues));
                    orderedAllowedStrings.Add(value);
                }
            }

            AllowedStringValues = ImmutableCollections.Freeze(orderedAllowedStrings);
        }

        public string Name { get; }
        public GameDataValueKind Kind { get; }
        public bool Required { get; }
        public bool AllowNull { get; }
        public bool NonBlank { get; }
        public bool StableId { get; }
        public bool IntegerOnly { get; }
        public double? MinimumNumber { get; }
        public double? MaximumNumber { get; }
        public int MinimumItems { get; }
        public int MaximumItems { get; }
        public string ReferenceFamily { get; }
        public IReadOnlyList<string> AllowedStringValues { get; }
        public IReadOnlyList<GameDataCatalogFieldRule> ObjectFields { get; }
        public GameDataCatalogFieldRule ItemRule { get; }

        internal bool TryGetObjectField(string name, out GameDataCatalogFieldRule rule)
        {
            return objectFieldsByName.TryGetValue(name ?? string.Empty, out rule);
        }

        internal bool IsAllowedString(string value)
        {
            return allowedStringSet.Count == 0 || allowedStringSet.Contains(value);
        }
    }

    public sealed class GameDataCatalogRecordConstraint
    {
        private readonly Func<string, IReadOnlyDictionary<string, GameDataValue>, bool?> evaluator;

        public GameDataCatalogRecordConstraint(
            string name,
            string fieldName,
            string diagnosticCode,
            string message,
            Func<string, IReadOnlyDictionary<string, GameDataValue>, bool?> evaluator)
        {
            if (!GameDataCatalogIdentifiers.IsCanonicalStableId(name))
            {
                throw new ArgumentException(
                    "A record-constraint name must be a canonical lower-snake-case ID.",
                    nameof(name));
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentException(
                    "A record constraint must identify its diagnostic field.",
                    nameof(fieldName));
            }

            if (string.IsNullOrWhiteSpace(diagnosticCode))
            {
                throw new ArgumentException(
                    "A record constraint requires a stable diagnostic code.",
                    nameof(diagnosticCode));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "A record constraint requires a diagnostic message.",
                    nameof(message));
            }

            Name = name;
            FieldName = fieldName;
            DiagnosticCode = diagnosticCode;
            Message = message;
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public string Name { get; }
        public string FieldName { get; }
        public string DiagnosticCode { get; }
        public string Message { get; }

        internal bool? Evaluate(
            string recordId,
            IReadOnlyDictionary<string, GameDataValue> fields)
        {
            return evaluator(recordId, fields);
        }
    }

    public sealed class GameDataCatalogFamilySchema
    {
        private readonly HashSet<int> supportedVersions;
        private readonly IReadOnlyDictionary<string, GameDataCatalogFieldRule> fieldsByName;

        public GameDataCatalogFamilySchema(
            string family,
            IEnumerable<int> supportedVersions,
            IEnumerable<GameDataCatalogFieldRule> fields,
            bool allowEmptyRecords = false,
            IEnumerable<GameDataCatalogRecordConstraint> recordConstraints = null)
        {
            if (!GameDataCatalogIdentifiers.IsCanonicalStableId(family))
            {
                throw new ArgumentException("A family must be a canonical lower-snake-case ID.", nameof(family));
            }

            this.supportedVersions = new HashSet<int>(supportedVersions ?? new int[0]);
            if (this.supportedVersions.Count == 0)
            {
                throw new ArgumentException("A family schema must support at least one version.", nameof(supportedVersions));
            }

            foreach (var version in this.supportedVersions)
            {
                if (version <= 0) throw new ArgumentException("Family schema versions must be positive.", nameof(supportedVersions));
            }

            var index = new SortedDictionary<string, GameDataCatalogFieldRule>(StringComparer.Ordinal);
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    if (field == null) throw new ArgumentException("Family field rules cannot contain null.", nameof(fields));
                    if (string.Equals(field.Name, "id", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("The canonical id field is provided by the common envelope.", nameof(fields));
                    }

                    index.Add(field.Name, field);
                }
            }

            Family = family;
            Fields = ImmutableCollections.Freeze(index.Values);
            fieldsByName = new ReadOnlyDictionary<string, GameDataCatalogFieldRule>(index);
            AllowEmptyRecords = allowEmptyRecords;

            var constraints = new List<GameDataCatalogRecordConstraint>();
            var constraintNames = new HashSet<string>(StringComparer.Ordinal);
            if (recordConstraints != null)
            {
                foreach (var constraint in recordConstraints)
                {
                    if (constraint == null)
                    {
                        throw new ArgumentException(
                            "Record constraints cannot contain null.",
                            nameof(recordConstraints));
                    }

                    if (!index.ContainsKey(constraint.FieldName))
                    {
                        throw new ArgumentException(
                            "A record constraint must target a declared schema field.",
                            nameof(recordConstraints));
                    }

                    if (!constraintNames.Add(constraint.Name))
                    {
                        throw new ArgumentException(
                            "Record-constraint names must be unique.",
                            nameof(recordConstraints));
                    }

                    constraints.Add(constraint);
                }
            }

            RecordConstraints = ImmutableCollections.Freeze(constraints);
        }

        public string Family { get; }
        public IReadOnlyList<GameDataCatalogFieldRule> Fields { get; }
        public bool AllowEmptyRecords { get; }
        public IReadOnlyList<GameDataCatalogRecordConstraint> RecordConstraints { get; }

        public bool SupportsVersion(int version)
        {
            return version > 0 && supportedVersions.Contains(version);
        }

        internal bool TryGetField(string name, out GameDataCatalogFieldRule rule)
        {
            return fieldsByName.TryGetValue(name ?? string.Empty, out rule);
        }
    }

    public sealed class GameDataCatalogSchemaRegistry
    {
        private readonly IReadOnlyDictionary<string, GameDataCatalogFamilySchema> schemasByFamily;

        public GameDataCatalogSchemaRegistry(IEnumerable<GameDataCatalogFamilySchema> schemas)
        {
            var index = new SortedDictionary<string, GameDataCatalogFamilySchema>(StringComparer.Ordinal);
            if (schemas != null)
            {
                foreach (var schema in schemas)
                {
                    if (schema == null) throw new ArgumentException("Schema registry cannot contain null.", nameof(schemas));
                    index.Add(schema.Family, schema);
                }
            }

            schemasByFamily = new ReadOnlyDictionary<string, GameDataCatalogFamilySchema>(index);
            Schemas = ImmutableCollections.Freeze(index.Values);
        }

        public IReadOnlyList<GameDataCatalogFamilySchema> Schemas { get; }

        public bool TryGet(string family, out GameDataCatalogFamilySchema schema)
        {
            return schemasByFamily.TryGetValue(family ?? string.Empty, out schema);
        }
    }

    public static class GameDataCatalogIdentifiers
    {
        public static bool IsCanonicalStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128 || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            var previousUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                var isLower = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                if (isLower || isDigit)
                {
                    previousUnderscore = false;
                    continue;
                }

                if (character != '_' || previousUnderscore || index == value.Length - 1)
                {
                    return false;
                }

                previousUnderscore = true;
            }

            return true;
        }

        public static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsCanonicalRelativeJsonPath(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 256 ||
                value[0] == '/' || value[value.Length - 1] == '/' ||
                value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0 ||
                value.IndexOf('%') >= 0 || value.IndexOf('?') >= 0 || value.IndexOf('#') >= 0 ||
                !value.EndsWith(".json", StringComparison.Ordinal))
            {
                return false;
            }

            var segments = value.Split('/');
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                var segment = segments[segmentIndex];
                if (segment.Length == 0 || segment == "." || segment == "..") return false;
                for (var index = 0; index < segment.Length; index++)
                {
                    var character = segment[index];
                    var allowed = (character >= 'a' && character <= 'z') ||
                                  (character >= 'A' && character <= 'Z') ||
                                  (character >= '0' && character <= '9') ||
                                  character == '_' || character == '-' || character == '.';
                    if (!allowed) return false;
                }
            }

            return true;
        }
    }
}
