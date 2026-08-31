using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;

namespace AL.Data.Catalogs.MapDisclosure
{
    public static class MapDisclosureContract
    {
        public const string SupportedVersion = "1.0.0";
        public const string CatalogId = "al_map_disclosure_catalog";
        public const string FileName = CatalogId + ".json";
        public const string ServerOwner = "server";
        public const string StaleSnapshotPolicy = "ignore_lower_epoch_or_revision";
        public const string EqualRevisionConflictPolicy =
            "intersect_visible_identifiers_and_require_refresh";
        public const string CatalogMismatchPolicy =
            "suppress_all_and_require_refresh";
        public const string ReconnectPolicy =
            "suppress_until_authoritative_snapshot";
        public const int SupportedSnapshotStateVersion = 1;
        public const int MaximumBytes = 64 * 1024;
        public const int MaximumDiagnostics = 128;
    }

    public enum MapDisclosureLoadStatus
    {
        Accepted = 0,
        Rejected = 1,
        UnsupportedVersion = 2
    }

    public sealed class MapDisclosureDiagnostic
    {
        public MapDisclosureDiagnostic(string code, string path, string relatedId, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            RelatedId = relatedId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string RelatedId { get; }
        public string Message { get; }
        public string Fingerprint => string.Join("|", Code, Path, RelatedId, Message);
    }

    public sealed class MapDisclosureLoadResult
    {
        internal MapDisclosureLoadResult(
            MapDisclosureLoadStatus status,
            MapDisclosureCatalogSnapshot snapshot,
            IList<MapDisclosureDiagnostic> diagnostics)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostics = Freeze(diagnostics);
        }

        public MapDisclosureLoadStatus Status { get; }
        public MapDisclosureCatalogSnapshot Snapshot { get; }
        public IReadOnlyList<MapDisclosureDiagnostic> Diagnostics { get; }
        public bool IsAccepted => Status == MapDisclosureLoadStatus.Accepted && Snapshot != null;

        private static IReadOnlyList<T> Freeze<T>(IList<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }
    }

    public sealed class MapDisclosureAuthorityPolicy
    {
        internal MapDisclosureAuthorityPolicy(
            string owner,
            int snapshotStateVersion,
            string staleSnapshotPolicy,
            string equalRevisionConflictPolicy,
            string catalogMismatchPolicy,
            string reconnectPolicy)
        {
            Owner = owner;
            SnapshotStateVersion = snapshotStateVersion;
            StaleSnapshotPolicy = staleSnapshotPolicy;
            EqualRevisionConflictPolicy = equalRevisionConflictPolicy;
            CatalogMismatchPolicy = catalogMismatchPolicy;
            ReconnectPolicy = reconnectPolicy;
        }

        public string Owner { get; }
        public int SnapshotStateVersion { get; }
        public string StaleSnapshotPolicy { get; }
        public string EqualRevisionConflictPolicy { get; }
        public string CatalogMismatchPolicy { get; }
        public string ReconnectPolicy { get; }
    }

    public sealed class MapDisclosureVisibilityRule
    {
        internal MapDisclosureVisibilityRule(string id, string mode)
        {
            Id = id;
            Mode = mode;
        }

        public string Id { get; }
        public string Mode { get; }
    }

    public sealed class MapDisclosureFeature
    {
        internal MapDisclosureFeature(
            string id,
            string sourceType,
            string sourceId,
            string visibilityRuleId,
            IList<string> surfaces,
            string iconReference,
            string nonColorShape)
        {
            Id = id;
            SourceType = sourceType;
            SourceId = sourceId;
            VisibilityRuleId = visibilityRuleId;
            Surfaces = Freeze(surfaces);
            IconReference = iconReference;
            NonColorShape = nonColorShape;
        }

        public string Id { get; }
        public string SourceType { get; }
        public string SourceId { get; }
        public string VisibilityRuleId { get; }
        public IReadOnlyList<string> Surfaces { get; }
        public string IconReference { get; }
        public string NonColorShape { get; }

        private static IReadOnlyList<T> Freeze<T>(IList<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }
    }

    public sealed class MapDisclosureRoute
    {
        internal MapDisclosureRoute(
            string id,
            IList<string> featureIds,
            string visibilityRuleId,
            IList<string> surfaces,
            string nonColorShape)
        {
            Id = id;
            FeatureIds = Freeze(featureIds);
            VisibilityRuleId = visibilityRuleId;
            Surfaces = Freeze(surfaces);
            NonColorShape = nonColorShape;
        }

        public string Id { get; }
        public IReadOnlyList<string> FeatureIds { get; }
        public string VisibilityRuleId { get; }
        public IReadOnlyList<string> Surfaces { get; }
        public string NonColorShape { get; }

        private static IReadOnlyList<T> Freeze<T>(IList<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }
    }

    public sealed class MapDisclosureObjective
    {
        internal MapDisclosureObjective(
            string id,
            string sourceObjectiveId,
            string featureId,
            string visibilityRuleId,
            IList<string> surfaces,
            string iconReference,
            string nonColorShape)
        {
            Id = id;
            SourceObjectiveId = sourceObjectiveId;
            FeatureId = featureId;
            VisibilityRuleId = visibilityRuleId;
            Surfaces = Freeze(surfaces);
            IconReference = iconReference;
            NonColorShape = nonColorShape;
        }

        public string Id { get; }
        public string SourceObjectiveId { get; }
        public string FeatureId { get; }
        public string VisibilityRuleId { get; }
        public IReadOnlyList<string> Surfaces { get; }
        public string IconReference { get; }
        public string NonColorShape { get; }

        private static IReadOnlyList<T> Freeze<T>(IList<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }
    }

    public sealed class MapDisclosureAllegianceMarker
    {
        internal MapDisclosureAllegianceMarker(
            string id,
            string realmId,
            string featureId,
            string realmGlyphId,
            string visibilityRuleId,
            IList<string> surfaces,
            string nonColorShape)
        {
            Id = id;
            RealmId = realmId;
            FeatureId = featureId;
            RealmGlyphId = realmGlyphId;
            VisibilityRuleId = visibilityRuleId;
            Surfaces = Freeze(surfaces);
            NonColorShape = nonColorShape;
        }

        public string Id { get; }
        public string RealmId { get; }
        public string FeatureId { get; }
        public string RealmGlyphId { get; }
        public string VisibilityRuleId { get; }
        public IReadOnlyList<string> Surfaces { get; }
        public string NonColorShape { get; }

        private static IReadOnlyList<T> Freeze<T>(IList<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }
    }

    public sealed class MapDisclosureRealmGlyph
    {
        internal MapDisclosureRealmGlyph(
            string id,
            string realmId,
            string assetReference,
            string nonColorShape)
        {
            Id = id;
            RealmId = realmId;
            AssetReference = assetReference;
            NonColorShape = nonColorShape;
        }

        public string Id { get; }
        public string RealmId { get; }
        public string AssetReference { get; }
        public string NonColorShape { get; }
    }

    public sealed class MapDisclosureCatalogSnapshot
    {
        private readonly IReadOnlyDictionary<string, MapDisclosureVisibilityRule> _rulesById;
        private readonly IReadOnlyDictionary<string, MapDisclosureFeature> _featuresById;
        private readonly IReadOnlyDictionary<string, MapDisclosureRoute> _routesById;
        private readonly IReadOnlyDictionary<string, MapDisclosureObjective> _objectivesById;
        private readonly IReadOnlyDictionary<string, MapDisclosureAllegianceMarker> _allegiancesById;
        private readonly IReadOnlyDictionary<string, MapDisclosureRealmGlyph> _glyphsById;

        internal MapDisclosureCatalogSnapshot(
            string version,
            string sourceSha256,
            MapDisclosureAuthorityPolicy authority,
            IList<MapDisclosureVisibilityRule> visibilityRules,
            IList<MapDisclosureFeature> features,
            IList<MapDisclosureRoute> routes,
            IList<MapDisclosureObjective> objectives,
            IList<MapDisclosureAllegianceMarker> allegianceMarkers,
            IList<MapDisclosureRealmGlyph> realmGlyphs)
        {
            Version = version;
            SourceSha256 = sourceSha256;
            Authority = authority;
            VisibilityRules = Freeze(visibilityRules);
            Features = Freeze(features);
            Routes = Freeze(routes);
            Objectives = Freeze(objectives);
            AllegianceMarkers = Freeze(allegianceMarkers);
            RealmGlyphs = Freeze(realmGlyphs);
            _rulesById = Index(VisibilityRules, value => value.Id);
            _featuresById = Index(Features, value => value.Id);
            _routesById = Index(Routes, value => value.Id);
            _objectivesById = Index(Objectives, value => value.Id);
            _allegiancesById = Index(AllegianceMarkers, value => value.Id);
            _glyphsById = Index(RealmGlyphs, value => value.Id);
        }

        public string Version { get; }
        public string SourceSha256 { get; }
        public MapDisclosureAuthorityPolicy Authority { get; }
        public IReadOnlyList<MapDisclosureVisibilityRule> VisibilityRules { get; }
        public IReadOnlyList<MapDisclosureFeature> Features { get; }
        public IReadOnlyList<MapDisclosureRoute> Routes { get; }
        public IReadOnlyList<MapDisclosureObjective> Objectives { get; }
        public IReadOnlyList<MapDisclosureAllegianceMarker> AllegianceMarkers { get; }
        public IReadOnlyList<MapDisclosureRealmGlyph> RealmGlyphs { get; }

        public bool TryGetVisibilityRule(string id, out MapDisclosureVisibilityRule value)
        {
            return _rulesById.TryGetValue(id ?? string.Empty, out value);
        }

        public bool ContainsFeature(string id)
        {
            return _featuresById.ContainsKey(id ?? string.Empty);
        }

        public bool TryGetFeature(string id, out MapDisclosureFeature value)
        {
            return _featuresById.TryGetValue(id ?? string.Empty, out value);
        }

        public bool ContainsRoute(string id)
        {
            return _routesById.ContainsKey(id ?? string.Empty);
        }

        public bool TryGetRoute(string id, out MapDisclosureRoute value)
        {
            return _routesById.TryGetValue(id ?? string.Empty, out value);
        }

        public bool ContainsObjective(string id)
        {
            return _objectivesById.ContainsKey(id ?? string.Empty);
        }

        public bool TryGetObjective(string id, out MapDisclosureObjective value)
        {
            return _objectivesById.TryGetValue(id ?? string.Empty, out value);
        }

        public bool ContainsAllegianceMarker(string id)
        {
            return _allegiancesById.ContainsKey(id ?? string.Empty);
        }

        public bool TryGetAllegianceMarker(
            string id,
            out MapDisclosureAllegianceMarker value)
        {
            return _allegiancesById.TryGetValue(id ?? string.Empty, out value);
        }

        public bool TryGetRealmGlyph(string id, out MapDisclosureRealmGlyph value)
        {
            return _glyphsById.TryGetValue(id ?? string.Empty, out value);
        }

        private static IReadOnlyList<T> Freeze<T>(IList<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }

        private static IReadOnlyDictionary<string, T> Index<T>(
            IEnumerable<T> values,
            Func<T, string> keySelector)
        {
            return new ReadOnlyDictionary<string, T>(
                values.ToDictionary(keySelector, StringComparer.Ordinal));
        }
    }

    public static class MapDisclosureCatalogLoader
    {
        public static MapDisclosureLoadResult Validate(byte[] bytes)
        {
            var diagnostics = new List<MapDisclosureDiagnostic>();
            StrictJsonObject root;
            try
            {
                root = StrictJsonDocument.Parse(bytes, MapDisclosureContract.MaximumBytes)
                    as StrictJsonObject;
            }
            catch (StrictJsonException exception)
            {
                return Reject(
                    MapDisclosureLoadStatus.Rejected,
                    diagnostics,
                    "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                    exception.Path,
                    string.Empty,
                    exception.Code);
            }
            catch (Exception)
            {
                return Reject(
                    MapDisclosureLoadStatus.Rejected,
                    diagnostics,
                    "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                    "$",
                    string.Empty,
                    "parse_failed");
            }

            if (root == null)
            {
                return Reject(
                    MapDisclosureLoadStatus.Rejected,
                    diagnostics,
                    "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                    "$",
                    string.Empty,
                    "root_not_object");
            }

            RequireProperties(
                root,
                "$",
                diagnostics,
                "version",
                "catalogId",
                "idFormat",
                "authority",
                "visibilityRules",
                "realmGlyphs",
                "features",
                "routes",
                "objectives",
                "allegianceMarkers");

            string version = String(root, "version", "$", diagnostics);
            if (!string.Equals(
                    version,
                    MapDisclosureContract.SupportedVersion,
                    StringComparison.Ordinal))
            {
                return Reject(
                    MapDisclosureLoadStatus.UnsupportedVersion,
                    diagnostics,
                    "AL-MAP-DISCLOSURE-VERSION-UNSUPPORTED",
                    "$.version",
                    version,
                    "unsupported version");
            }

            RequireEqual(
                root,
                "catalogId",
                MapDisclosureContract.CatalogId,
                "$",
                diagnostics);
            RequireEqual(root, "idFormat", "lowercase_snake_case", "$", diagnostics);

            MapDisclosureAuthorityPolicy authority = ParseAuthority(root, diagnostics);
            List<MapDisclosureVisibilityRule> rules = ParseRules(root, diagnostics);
            List<MapDisclosureRealmGlyph> glyphs = ParseGlyphs(root, diagnostics);
            List<MapDisclosureFeature> features = ParseFeatures(root, diagnostics);
            List<MapDisclosureRoute> routes = ParseRoutes(root, diagnostics);
            List<MapDisclosureObjective> objectives = ParseObjectives(root, diagnostics);
            List<MapDisclosureAllegianceMarker> allegiances =
                ParseAllegiances(root, diagnostics);

            ValidateReferences(
                rules,
                glyphs,
                features,
                routes,
                objectives,
                allegiances,
                diagnostics);
            SortDiagnostics(diagnostics);
            if (diagnostics.Count != 0)
            {
                return new MapDisclosureLoadResult(
                    MapDisclosureLoadStatus.Rejected,
                    null,
                    diagnostics.Take(MapDisclosureContract.MaximumDiagnostics).ToArray());
            }

            string sourceSha256;
            using (SHA256 sha256 = SHA256.Create())
            {
                sourceSha256 = string.Concat(
                    sha256.ComputeHash(bytes)
                        .Select(value => value.ToString("x2")));
            }

            return new MapDisclosureLoadResult(
                MapDisclosureLoadStatus.Accepted,
                new MapDisclosureCatalogSnapshot(
                    version,
                    sourceSha256,
                    authority,
                    rules,
                    features,
                    routes,
                    objectives,
                    allegiances,
                    glyphs),
                diagnostics);
        }

        private static MapDisclosureAuthorityPolicy ParseAuthority(
            StrictJsonObject root,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            StrictJsonObject value = Object(root, "authority", "$", diagnostics);
            if (value == null)
            {
                return null;
            }

            RequireProperties(
                value,
                "$.authority",
                diagnostics,
                "owner",
                "snapshotStateVersion",
                "staleSnapshotPolicy",
                "equalRevisionConflictPolicy",
                "catalogMismatchPolicy",
                "reconnectPolicy");

            var policy = new MapDisclosureAuthorityPolicy(
                String(value, "owner", "$.authority", diagnostics),
                Integer(value, "snapshotStateVersion", "$.authority", diagnostics),
                String(value, "staleSnapshotPolicy", "$.authority", diagnostics),
                String(value, "equalRevisionConflictPolicy", "$.authority", diagnostics),
                String(value, "catalogMismatchPolicy", "$.authority", diagnostics),
                String(value, "reconnectPolicy", "$.authority", diagnostics));
            if (!string.Equals(
                    policy.Owner,
                    MapDisclosureContract.ServerOwner,
                    StringComparison.Ordinal) ||
                policy.SnapshotStateVersion !=
                    MapDisclosureContract.SupportedSnapshotStateVersion ||
                !string.Equals(
                    policy.StaleSnapshotPolicy,
                    MapDisclosureContract.StaleSnapshotPolicy,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    policy.EqualRevisionConflictPolicy,
                    MapDisclosureContract.EqualRevisionConflictPolicy,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    policy.CatalogMismatchPolicy,
                    MapDisclosureContract.CatalogMismatchPolicy,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    policy.ReconnectPolicy,
                    MapDisclosureContract.ReconnectPolicy,
                    StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-MAP-DISCLOSURE-AUTHORITY-INVALID",
                    "$.authority",
                    string.Empty,
                    "recognized server authority policies and a positive snapshot version are required");
            }

            return policy;
        }

        private static List<MapDisclosureVisibilityRule> ParseRules(
            StrictJsonObject root,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new List<MapDisclosureVisibilityRule>();
            ParseObjects(
                Array(root, "visibilityRules", "$", diagnostics),
                "$.visibilityRules",
                diagnostics,
                (item, path) =>
                {
                    RequireProperties(item, path, diagnostics, "id", "mode");
                    values.Add(new MapDisclosureVisibilityRule(
                        String(item, "id", path, diagnostics),
                        String(item, "mode", path, diagnostics)));
                });
            return values;
        }

        private static List<MapDisclosureRealmGlyph> ParseGlyphs(
            StrictJsonObject root,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new List<MapDisclosureRealmGlyph>();
            ParseObjects(
                Array(root, "realmGlyphs", "$", diagnostics),
                "$.realmGlyphs",
                diagnostics,
                (item, path) =>
                {
                    RequireProperties(
                        item,
                        path,
                        diagnostics,
                        "id",
                        "realmId",
                        "assetReference",
                        "nonColorShape");
                    values.Add(new MapDisclosureRealmGlyph(
                        String(item, "id", path, diagnostics),
                        String(item, "realmId", path, diagnostics),
                        String(item, "assetReference", path, diagnostics),
                        String(item, "nonColorShape", path, diagnostics)));
                });
            return values;
        }

        private static List<MapDisclosureFeature> ParseFeatures(
            StrictJsonObject root,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new List<MapDisclosureFeature>();
            ParseObjects(
                Array(root, "features", "$", diagnostics),
                "$.features",
                diagnostics,
                (item, path) =>
                {
                    RequireProperties(
                        item,
                        path,
                        diagnostics,
                        "id",
                        "sourceType",
                        "sourceId",
                        "visibilityRuleId",
                        "surfaces",
                        "iconReference",
                        "nonColorShape");
                    values.Add(new MapDisclosureFeature(
                        String(item, "id", path, diagnostics),
                        String(item, "sourceType", path, diagnostics),
                        String(item, "sourceId", path, diagnostics),
                        String(item, "visibilityRuleId", path, diagnostics),
                        Strings(item, "surfaces", path, diagnostics),
                        String(item, "iconReference", path, diagnostics),
                        String(item, "nonColorShape", path, diagnostics)));
                });
            return values;
        }

        private static List<MapDisclosureRoute> ParseRoutes(
            StrictJsonObject root,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new List<MapDisclosureRoute>();
            ParseObjects(
                Array(root, "routes", "$", diagnostics),
                "$.routes",
                diagnostics,
                (item, path) =>
                {
                    RequireProperties(
                        item,
                        path,
                        diagnostics,
                        "id",
                        "featureIds",
                        "visibilityRuleId",
                        "surfaces",
                        "nonColorShape");
                    values.Add(new MapDisclosureRoute(
                        String(item, "id", path, diagnostics),
                        Strings(item, "featureIds", path, diagnostics),
                        String(item, "visibilityRuleId", path, diagnostics),
                        Strings(item, "surfaces", path, diagnostics),
                        String(item, "nonColorShape", path, diagnostics)));
                });
            return values;
        }

        private static List<MapDisclosureObjective> ParseObjectives(
            StrictJsonObject root,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new List<MapDisclosureObjective>();
            ParseObjects(
                Array(root, "objectives", "$", diagnostics),
                "$.objectives",
                diagnostics,
                (item, path) =>
                {
                    RequireProperties(
                        item,
                        path,
                        diagnostics,
                        "id",
                        "sourceObjectiveId",
                        "featureId",
                        "visibilityRuleId",
                        "surfaces",
                        "iconReference",
                        "nonColorShape");
                    values.Add(new MapDisclosureObjective(
                        String(item, "id", path, diagnostics),
                        String(item, "sourceObjectiveId", path, diagnostics),
                        String(item, "featureId", path, diagnostics),
                        String(item, "visibilityRuleId", path, diagnostics),
                        Strings(item, "surfaces", path, diagnostics),
                        String(item, "iconReference", path, diagnostics),
                        String(item, "nonColorShape", path, diagnostics)));
                });
            return values;
        }

        private static List<MapDisclosureAllegianceMarker> ParseAllegiances(
            StrictJsonObject root,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new List<MapDisclosureAllegianceMarker>();
            ParseObjects(
                Array(root, "allegianceMarkers", "$", diagnostics),
                "$.allegianceMarkers",
                diagnostics,
                (item, path) =>
                {
                    RequireProperties(
                        item,
                        path,
                        diagnostics,
                        "id",
                        "realmId",
                        "featureId",
                        "realmGlyphId",
                        "visibilityRuleId",
                        "surfaces",
                        "nonColorShape");
                    values.Add(new MapDisclosureAllegianceMarker(
                        String(item, "id", path, diagnostics),
                        String(item, "realmId", path, diagnostics),
                        String(item, "featureId", path, diagnostics),
                        String(item, "realmGlyphId", path, diagnostics),
                        String(item, "visibilityRuleId", path, diagnostics),
                        Strings(item, "surfaces", path, diagnostics),
                        String(item, "nonColorShape", path, diagnostics)));
                });
            return values;
        }

        private static void ValidateReferences(
            IList<MapDisclosureVisibilityRule> rules,
            IList<MapDisclosureRealmGlyph> glyphs,
            IList<MapDisclosureFeature> features,
            IList<MapDisclosureRoute> routes,
            IList<MapDisclosureObjective> objectives,
            IList<MapDisclosureAllegianceMarker> allegiances,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            RequireNonEmpty(rules, "$.visibilityRules", diagnostics);
            RequireNonEmpty(glyphs, "$.realmGlyphs", diagnostics);
            RequireNonEmpty(features, "$.features", diagnostics);
            RequireNonEmpty(routes, "$.routes", diagnostics);
            RequireNonEmpty(objectives, "$.objectives", diagnostics);
            RequireNonEmpty(allegiances, "$.allegianceMarkers", diagnostics);
            HashSet<string> ruleIds = Unique(
                rules.Select(value => value.Id),
                "$.visibilityRules",
                diagnostics);
            foreach (MapDisclosureVisibilityRule rule in rules)
            {
                if (!string.Equals(rule.Mode, "always", StringComparison.Ordinal) &&
                    !string.Equals(rule.Mode, "discovered", StringComparison.Ordinal) &&
                    !string.Equals(rule.Mode, "active_objective", StringComparison.Ordinal) &&
                    !string.Equals(
                        rule.Mode,
                        "authoritative_allegiance",
                        StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-VISIBILITY-MODE-INVALID",
                        "$.visibilityRules",
                        rule.Id,
                        "recognized visibility mode required");
                }
            }
            var ruleModesById =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MapDisclosureVisibilityRule rule in rules)
            {
                if (!ruleModesById.ContainsKey(rule.Id))
                {
                    ruleModesById.Add(rule.Id, rule.Mode);
                }
            }
            HashSet<string> glyphIds = Unique(
                glyphs.Select(value => value.Id),
                "$.realmGlyphs",
                diagnostics);
            HashSet<string> featureIds = Unique(
                features.Select(value => value.Id),
                "$.features",
                diagnostics);
            Unique(routes.Select(value => value.Id), "$.routes", diagnostics);
            Unique(objectives.Select(value => value.Id), "$.objectives", diagnostics);
            Unique(
                allegiances.Select(value => value.Id),
                "$.allegianceMarkers",
                diagnostics);

            foreach (MapDisclosureFeature feature in features)
            {
                ValidateCanonicalField(
                    feature.SourceId,
                    "$.features",
                    feature.Id,
                    diagnostics);
                ValidateCanonicalField(
                    feature.NonColorShape,
                    "$.features",
                    feature.Id,
                    diagnostics);
                if (!string.Equals(
                        feature.SourceType,
                        "atlas_zone",
                        StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-SOURCE-TYPE-INVALID",
                        "$.features",
                        feature.Id,
                        "atlas_zone source type is required");
                }
                Reference(
                    ruleIds,
                    feature.VisibilityRuleId,
                    "$.features",
                    feature.Id,
                    diagnostics);
                ValidateRuleMode(
                    ruleModesById,
                    feature.VisibilityRuleId,
                    "$.features",
                    feature.Id,
                    diagnostics,
                    "always",
                    "discovered");
                ValidateSurfaces(feature.Surfaces, "$.features", feature.Id, diagnostics);
            }

            foreach (MapDisclosureRoute route in routes)
            {
                ValidateCanonicalField(
                    route.NonColorShape,
                    "$.routes",
                    route.Id,
                    diagnostics);
                Reference(
                    ruleIds,
                    route.VisibilityRuleId,
                    "$.routes",
                    route.Id,
                    diagnostics);
                ValidateRuleMode(
                    ruleModesById,
                    route.VisibilityRuleId,
                    "$.routes",
                    route.Id,
                    diagnostics,
                    "discovered");
                if (route.FeatureIds.Count < 2 ||
                    route.FeatureIds.Distinct(StringComparer.Ordinal).Count() !=
                    route.FeatureIds.Count)
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-ROUTE-INVALID",
                        "$.routes",
                        route.Id,
                        "at least two distinct feature references are required");
                }
                foreach (string featureId in route.FeatureIds)
                {
                    Reference(
                        featureIds,
                        featureId,
                        "$.routes",
                        route.Id,
                        diagnostics);
                }
                ValidateSurfaces(route.Surfaces, "$.routes", route.Id, diagnostics);
            }

            foreach (MapDisclosureObjective objective in objectives)
            {
                ValidateCanonicalField(
                    objective.SourceObjectiveId,
                    "$.objectives",
                    objective.Id,
                    diagnostics);
                ValidateCanonicalField(
                    objective.NonColorShape,
                    "$.objectives",
                    objective.Id,
                    diagnostics);
                Reference(
                    ruleIds,
                    objective.VisibilityRuleId,
                    "$.objectives",
                    objective.Id,
                    diagnostics);
                ValidateRuleMode(
                    ruleModesById,
                    objective.VisibilityRuleId,
                    "$.objectives",
                    objective.Id,
                    diagnostics,
                    "active_objective");
                Reference(
                    featureIds,
                    objective.FeatureId,
                    "$.objectives",
                    objective.Id,
                    diagnostics);
                ValidateSurfaces(
                    objective.Surfaces,
                    "$.objectives",
                    objective.Id,
                    diagnostics);
            }

            var glyphRealmById =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MapDisclosureRealmGlyph glyph in glyphs)
            {
                ValidateCanonicalField(
                    glyph.RealmId,
                    "$.realmGlyphs",
                    glyph.Id,
                    diagnostics);
                ValidateCanonicalField(
                    glyph.NonColorShape,
                    "$.realmGlyphs",
                    glyph.Id,
                    diagnostics);
                if (!glyphRealmById.ContainsKey(glyph.Id))
                {
                    glyphRealmById.Add(glyph.Id, glyph.RealmId);
                }
            }
            foreach (MapDisclosureAllegianceMarker marker in allegiances)
            {
                ValidateCanonicalField(
                    marker.RealmId,
                    "$.allegianceMarkers",
                    marker.Id,
                    diagnostics);
                ValidateCanonicalField(
                    marker.NonColorShape,
                    "$.allegianceMarkers",
                    marker.Id,
                    diagnostics);
                Reference(
                    ruleIds,
                    marker.VisibilityRuleId,
                    "$.allegianceMarkers",
                    marker.Id,
                    diagnostics);
                ValidateRuleMode(
                    ruleModesById,
                    marker.VisibilityRuleId,
                    "$.allegianceMarkers",
                    marker.Id,
                    diagnostics,
                    "authoritative_allegiance");
                Reference(
                    featureIds,
                    marker.FeatureId,
                    "$.allegianceMarkers",
                    marker.Id,
                    diagnostics);
                Reference(
                    glyphIds,
                    marker.RealmGlyphId,
                    "$.allegianceMarkers",
                    marker.Id,
                    diagnostics);
                if (glyphRealmById.TryGetValue(marker.RealmGlyphId, out string realmId) &&
                    !string.Equals(realmId, marker.RealmId, StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-GLYPH-REALM-MISMATCH",
                        "$.allegianceMarkers",
                        marker.Id,
                        "realm glyph must match marker realm");
                }
                ValidateSurfaces(
                    marker.Surfaces,
                    "$.allegianceMarkers",
                    marker.Id,
                    diagnostics);
            }
        }

        private static StrictJsonObject Object(
            StrictJsonObject owner,
            string name,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonObject result)
            {
                return result;
            }

            Add(
                diagnostics,
                "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "object required");
            return null;
        }

        private static StrictJsonArray Array(
            StrictJsonObject owner,
            string name,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonArray result)
            {
                return result;
            }

            Add(
                diagnostics,
                "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "array required");
            return null;
        }

        private static string String(
            StrictJsonObject owner,
            string name,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonString text &&
                !string.IsNullOrWhiteSpace(text.Value))
            {
                return text.Value;
            }

            Add(
                diagnostics,
                "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "nonblank string required");
            return string.Empty;
        }

        private static int Integer(
            StrictJsonObject owner,
            string name,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (owner != null &&
                owner.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonNumber number &&
                number.HasFiniteDoubleValue &&
                number.Value >= int.MinValue &&
                number.Value <= int.MaxValue &&
                Math.Truncate(number.Value) == number.Value)
            {
                return (int)number.Value;
            }

            Add(
                diagnostics,
                "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                path + "." + name,
                string.Empty,
                "integer required");
            return 0;
        }

        private static List<string> Strings(
            StrictJsonObject owner,
            string name,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new List<string>();
            StrictJsonArray array = Array(owner, name, path, diagnostics);
            if (array == null)
            {
                return values;
            }

            for (int index = 0; index < array.Items.Count; index++)
            {
                if (array.Items[index] is StrictJsonString item &&
                    !string.IsNullOrWhiteSpace(item.Value))
                {
                    values.Add(item.Value);
                }
                else
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                        path + "." + name + "[" + index + "]",
                        string.Empty,
                        "nonblank string required");
                }
            }

            return values;
        }

        private static void ParseObjects(
            StrictJsonArray array,
            string path,
            List<MapDisclosureDiagnostic> diagnostics,
            Action<StrictJsonObject, string> action)
        {
            if (array == null)
            {
                return;
            }

            for (int index = 0; index < array.Items.Count; index++)
            {
                if (array.Items[index] is StrictJsonObject item)
                {
                    action(item, path + "[" + index + "]");
                }
                else
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                        path + "[" + index + "]",
                        string.Empty,
                        "object required");
                }
            }
        }

        private static HashSet<string> Unique(
            IEnumerable<string> ids,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (!IsCanonicalId(id) || !values.Add(id))
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-ID-INVALID",
                        path,
                        id,
                        "lowercase snake-case unique id required");
                }
            }
            return values;
        }

        private static bool IsCanonicalId(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                !(value[0] >= 'a' && value[0] <= 'z') ||
                value[0] == '_' ||
                value[value.Length - 1] == '_')
            {
                return false;
            }

            bool priorUnderscore = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool underscore = character == '_';
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    !underscore ||
                    underscore && priorUnderscore)
                {
                    return false;
                }
                priorUnderscore = underscore;
            }
            return true;
        }

        private static void ValidateCanonicalField(
            string value,
            string path,
            string relatedId,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (!IsCanonicalId(value))
            {
                Add(
                    diagnostics,
                    "AL-MAP-DISCLOSURE-ID-INVALID",
                    path,
                    relatedId,
                    "lowercase snake-case id required");
            }
        }

        private static void ValidateRuleMode(
            IReadOnlyDictionary<string, string> ruleModesById,
            string ruleId,
            string path,
            string relatedId,
            List<MapDisclosureDiagnostic> diagnostics,
            params string[] allowedModes)
        {
            if (ruleModesById.TryGetValue(ruleId, out string mode) &&
                !allowedModes.Contains(mode, StringComparer.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-MAP-DISCLOSURE-VISIBILITY-MODE-INVALID",
                    path,
                    relatedId,
                    "visibility rule mode is not valid for this content family");
            }
        }

        private static void ValidateSurfaces(
            IReadOnlyList<string> surfaces,
            string path,
            string relatedId,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (surfaces.Count == 0 ||
                surfaces.Distinct(StringComparer.Ordinal).Count() != surfaces.Count ||
                surfaces.Any(value =>
                    !string.Equals(value, "minimap", StringComparison.Ordinal) &&
                    !string.Equals(value, "world_map", StringComparison.Ordinal)))
            {
                Add(
                    diagnostics,
                    "AL-MAP-DISCLOSURE-SURFACE-INVALID",
                    path,
                    relatedId,
                    "unique minimap/world_map surfaces are required");
            }
        }

        private static void Reference(
            ISet<string> ids,
            string id,
            string path,
            string relatedId,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (!ids.Contains(id))
            {
                Add(
                    diagnostics,
                    "AL-MAP-DISCLOSURE-REFERENCE-MISSING",
                    path,
                    relatedId,
                    "missing reference: " + id);
            }
        }

        private static void RequireNonEmpty<T>(
            IList<T> values,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            if (values.Count == 0)
            {
                Add(
                    diagnostics,
                    "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                    path,
                    string.Empty,
                    "at least one item is required");
            }
        }

        private static void RequireProperties(
            StrictJsonObject value,
            string path,
            List<MapDisclosureDiagnostic> diagnostics,
            params string[] expectedNames)
        {
            var expected = new HashSet<string>(
                expectedNames ?? System.Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (StrictJsonProperty property in value.Properties)
            {
                if (!expected.Contains(property.Name))
                {
                    Add(
                        diagnostics,
                        "AL-MAP-DISCLOSURE-SCHEMA-INVALID",
                        path + "." + property.Name,
                        string.Empty,
                        "additional property is not allowed");
                }
            }
        }

        private static void RequireEqual(
            StrictJsonObject owner,
            string name,
            string expected,
            string path,
            List<MapDisclosureDiagnostic> diagnostics)
        {
            string actual = String(owner, name, path, diagnostics);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    "AL-MAP-DISCLOSURE-SOURCE-MISMATCH",
                    path + "." + name,
                    actual,
                    "authority identity mismatch");
            }
        }

        private static void Add(
            List<MapDisclosureDiagnostic> diagnostics,
            string code,
            string path,
            string relatedId,
            string message)
        {
            diagnostics.Add(new MapDisclosureDiagnostic(code, path, relatedId, message));
        }

        private static void SortDiagnostics(List<MapDisclosureDiagnostic> diagnostics)
        {
            diagnostics.Sort((left, right) =>
            {
                int comparison = string.CompareOrdinal(left.Code, right.Code);
                if (comparison != 0)
                {
                    return comparison;
                }
                comparison = string.CompareOrdinal(left.Path, right.Path);
                return comparison != 0
                    ? comparison
                    : string.CompareOrdinal(left.RelatedId, right.RelatedId);
            });
        }

        private static MapDisclosureLoadResult Reject(
            MapDisclosureLoadStatus status,
            List<MapDisclosureDiagnostic> diagnostics,
            string code,
            string path,
            string relatedId,
            string message)
        {
            Add(diagnostics, code, path, relatedId, message);
            SortDiagnostics(diagnostics);
            return new MapDisclosureLoadResult(
                status,
                null,
                diagnostics.Take(MapDisclosureContract.MaximumDiagnostics).ToArray());
        }
    }
}
