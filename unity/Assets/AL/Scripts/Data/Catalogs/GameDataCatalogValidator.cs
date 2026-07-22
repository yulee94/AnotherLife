using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Pure, fail-closed manifest and family validation. No runtime state is published here.
    /// </summary>
    public static class GameDataCatalogValidator
    {
        private static readonly string[] ManifestFields =
        {
            "gameId", "catalogSetId", "schemaVersion", "contentVersion",
            "minimumRuntimeCatalogVersion", "sourceRevision", "artifacts"
        };

        private static readonly string[] ArtifactFields =
        {
            "family", "catalogId", "relativePath", "schemaVersion", "contentVersion",
            "required", "sha256", "mediaType", "sourceMode", "sourceRevision"
        };

        private static readonly string[] EnvelopeFields =
        {
            "gameId", "catalogId", "family", "schemaVersion", "contentVersion",
            "sourceRevision", "records", "aliases"
        };

        private static readonly string[] AliasFields =
        {
            "legacyId", "canonicalId", "introducedVersion", "retirementVersion", "migrationIssue"
        };

        public static GameDataCatalogManifestValidationResult ValidateManifest(
            byte[] manifestBytes,
            GameDataCatalogValidationPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var collector = new DiagnosticCollector(policy.MaximumDiagnostics);
            if (manifestBytes == null)
            {
                collector.Add(
                    GameDataCatalogLoadStatus.MissingManifest,
                    "MANIFEST-MISSING",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "$",
                    "catalog.manifest.missing",
                    "The catalog-set manifest bytes are missing.",
                    "Package the reviewed manifest before loading game data.",
                    true,
                    true,
                    -1,
                    -1);
                return new GameDataCatalogManifestValidationResult(
                    GameDataCatalogLoadStatus.MissingManifest,
                    null,
                    collector.OrderedDiagnostics());
            }

            StrictJsonValue root;
            try
            {
                root = StrictJsonDocument.Parse(manifestBytes, policy.MaximumManifestBytes);
            }
            catch (StrictJsonException exception)
            {
                collector.Add(
                    GameDataCatalogLoadStatus.MalformedJson,
                    exception.Code,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    exception.Path,
                    "catalog.json.malformed",
                    exception.Message,
                    "Correct the strict UTF-8 JSON source.",
                    true,
                    true,
                    -1,
                    -1);
                return new GameDataCatalogManifestValidationResult(
                    GameDataCatalogLoadStatus.MalformedJson,
                    null,
                    collector.OrderedDiagnostics());
            }

            var rootObject = root as StrictJsonObject;
            if (rootObject == null)
            {
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidEnvelope,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "$",
                    "object",
                    root.Kind.ToString(),
                    -1,
                    -1);
                return ManifestFailure(collector);
            }

            CheckKnownFields(
                rootObject,
                ManifestFields,
                "$",
                GameDataCatalogLoadStatus.InvalidEnvelope,
                string.Empty,
                string.Empty,
                string.Empty,
                -1,
                -1,
                collector);

            var gameId = ReadString(rootObject, "gameId", "$", true, string.Empty, string.Empty, -1, -1, collector);
            var catalogSetId = ReadString(rootObject, "catalogSetId", "$", true, string.Empty, string.Empty, -1, -1, collector);
            var schemaVersion = ReadInt32(rootObject, "schemaVersion", "$", string.Empty, string.Empty, -1, -1, collector);
            var contentVersion = ReadString(rootObject, "contentVersion", "$", true, string.Empty, string.Empty, -1, -1, collector);
            var minimumRuntimeVersion = ReadInt32(
                rootObject,
                "minimumRuntimeCatalogVersion",
                "$",
                string.Empty,
                string.Empty,
                -1,
                -1,
                collector);
            var sourceRevision = ReadString(rootObject, "sourceRevision", "$", true, string.Empty, string.Empty, -1, -1, collector);

            if (!string.Equals(gameId, policy.ExpectedGameId, StringComparison.Ordinal))
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.InvalidEnvelope,
                    "GAME-ID",
                    catalogSetId,
                    string.Empty,
                    string.Empty,
                    "$.gameId",
                    "catalog.game_id.invalid",
                    "The game ID does not match the configured runtime identity.",
                    "Use the exact reviewed game ID.",
                    -1,
                    -1);
            }

            if (!GameDataCatalogIdentifiers.IsCanonicalStableId(catalogSetId))
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.InvalidEnvelope,
                    "CATALOG-SET-ID",
                    catalogSetId,
                    string.Empty,
                    string.Empty,
                    "$.catalogSetId",
                    "catalog.catalog_set_id.invalid",
                    "The catalog-set ID is not a canonical lower-snake-case identifier.",
                    "Provide a stable canonical ID.",
                    -1,
                    -1);
            }

            if (!policy.SupportsManifestVersion(schemaVersion))
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.UnsupportedVersion,
                    "MANIFEST-VERSION-UNSUPPORTED",
                    catalogSetId,
                    string.Empty,
                    string.Empty,
                    "$.schemaVersion",
                    "catalog.manifest.version_unsupported",
                    "The manifest schema version is not supported by this runtime.",
                    "Use a supported positive manifest version or update the runtime.",
                    -1,
                    -1);
            }

            if (minimumRuntimeVersion <= 0 || minimumRuntimeVersion > policy.RuntimeCatalogVersion)
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.UnsupportedVersion,
                    "RUNTIME-VERSION-UNSUPPORTED",
                    catalogSetId,
                    string.Empty,
                    string.Empty,
                    "$.minimumRuntimeCatalogVersion",
                    "catalog.runtime.version_unsupported",
                    "The manifest requires an unsupported runtime catalog version.",
                    "Lower the reviewed minimum or update the runtime.",
                    -1,
                    -1);
            }

            StrictJsonValue artifactsValue;
            var artifacts = new List<GameDataArtifactManifest>();
            if (!rootObject.TryGet("artifacts", out artifactsValue))
            {
                // The common required-field pass already emitted the deterministic diagnostic.
            }
            else
            {
                var artifactsArray = artifactsValue as StrictJsonArray;
                if (artifactsArray == null)
                {
                    collector.TypeError(
                        GameDataCatalogLoadStatus.InvalidEnvelope,
                        catalogSetId,
                        string.Empty,
                        string.Empty,
                        "$.artifacts",
                        "array",
                        artifactsValue.Kind.ToString(),
                        -1,
                        -1);
                }
                else
                {
                    if (artifactsArray.Items.Count == 0 || artifactsArray.Items.Count > policy.MaximumArtifacts)
                    {
                        collector.ValueError(
                            GameDataCatalogLoadStatus.InvalidEnvelope,
                            "ARTIFACT-COUNT",
                            catalogSetId,
                            string.Empty,
                            string.Empty,
                            "$.artifacts",
                            "catalog.manifest.artifact_count",
                            "The manifest artifact count is outside the bounded supported range.",
                            "Declare between one and the configured maximum number of artifacts.",
                            -1,
                            -1);
                    }

                    var familyIds = new HashSet<string>(StringComparer.Ordinal);
                    var catalogIds = new HashSet<string>(StringComparer.Ordinal);
                    var paths = new HashSet<string>(StringComparer.Ordinal);
                    for (var index = 0; index < artifactsArray.Items.Count && index < policy.MaximumArtifacts; index++)
                    {
                        var path = "$.artifacts[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                        var artifactObject = artifactsArray.Items[index] as StrictJsonObject;
                        if (artifactObject == null)
                        {
                            collector.TypeError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                catalogSetId,
                                string.Empty,
                                string.Empty,
                                path,
                                "object",
                                artifactsArray.Items[index].Kind.ToString(),
                                index,
                                -1);
                            continue;
                        }

                        CheckKnownFields(
                            artifactObject,
                            ArtifactFields,
                            path,
                            GameDataCatalogLoadStatus.InvalidEnvelope,
                            catalogSetId,
                            string.Empty,
                            string.Empty,
                            index,
                            -1,
                            collector);

                        var family = ReadString(artifactObject, "family", path, true, catalogSetId, string.Empty, index, -1, collector);
                        var catalogId = ReadString(artifactObject, "catalogId", path, true, catalogSetId, family, index, -1, collector);
                        var relativePath = ReadString(artifactObject, "relativePath", path, true, catalogSetId, family, index, -1, collector);
                        var artifactSchemaVersion = ReadInt32(artifactObject, "schemaVersion", path, catalogSetId, family, index, -1, collector);
                        var artifactContentVersion = ReadString(artifactObject, "contentVersion", path, true, catalogSetId, family, index, -1, collector);
                        var required = ReadBoolean(artifactObject, "required", path, catalogSetId, family, index, -1, collector);
                        var sha256 = ReadString(artifactObject, "sha256", path, true, catalogSetId, family, index, -1, collector);
                        var mediaType = ReadString(artifactObject, "mediaType", path, true, catalogSetId, family, index, -1, collector);
                        var sourceMode = ReadString(artifactObject, "sourceMode", path, true, catalogSetId, family, index, -1, collector);
                        var artifactSourceRevision = ReadString(artifactObject, "sourceRevision", path, true, catalogSetId, family, index, -1, collector);

                        if (!GameDataCatalogIdentifiers.IsCanonicalStableId(family))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "FAMILY-ID",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".family",
                                "catalog.family.invalid",
                                "The family is not a canonical lower-snake-case identifier.",
                                "Use a reviewed stable family ID.",
                                index,
                                -1);
                        }

                        if (!GameDataCatalogIdentifiers.IsCanonicalStableId(catalogId))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "CATALOG-ID",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".catalogId",
                                "catalog.id.invalid",
                                "The catalog ID is not a canonical lower-snake-case identifier.",
                                "Use a reviewed stable catalog ID.",
                                index,
                                -1);
                        }

                        if (artifactSchemaVersion <= 0)
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.UnsupportedVersion,
                                "FAMILY-VERSION-UNSUPPORTED",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".schemaVersion",
                                "catalog.family.version_unsupported",
                                "The family schema version must be positive and supported.",
                                "Use a supported family schema version.",
                                index,
                                -1);
                        }

                        if (!GameDataCatalogIdentifiers.IsCanonicalRelativeJsonPath(relativePath))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "ARTIFACT-PATH",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".relativePath",
                                "catalog.artifact.path_invalid",
                                "The artifact path is not a canonical relative JSON path.",
                                "Use a normalized path beneath the packaged game-data root.",
                                index,
                                -1);
                        }

                        if (!GameDataCatalogIdentifiers.IsLowerSha256(sha256))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "SHA256-FORMAT",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".sha256",
                                "catalog.artifact.hash_format",
                                "The artifact hash is not 64-character lower-case SHA-256 hex.",
                                "Regenerate the manifest from the exact packaged bytes.",
                                index,
                                -1);
                        }

                        if (!string.Equals(mediaType, GameDataCatalogContract.JsonMediaType, StringComparison.Ordinal))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "MEDIA-TYPE",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".mediaType",
                                "catalog.artifact.media_type",
                                "The artifact media type is unsupported.",
                                "Use the reviewed JSON media type.",
                                index,
                                -1);
                        }

                        if (!policy.SupportsSourceMode(sourceMode))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "SOURCE-MODE",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".sourceMode",
                                "catalog.artifact.source_mode",
                                "The artifact source mode is unsupported.",
                                "Use a reviewed authored or generated source mode.",
                                index,
                                -1);
                        }

                        if (!familyIds.Add(family))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "FAMILY-DUPLICATE",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".family",
                                "catalog.family.duplicate",
                                "The manifest selects more than one artifact for a family.",
                                "Select exactly one artifact per family.",
                                index,
                                -1);
                        }

                        if (!catalogIds.Add(catalogId))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "CATALOG-ID-DUPLICATE",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".catalogId",
                                "catalog.id.duplicate",
                                "The manifest contains a duplicate catalog ID.",
                                "Assign a unique catalog ID.",
                                index,
                                -1);
                        }

                        if (!paths.Add(relativePath))
                        {
                            collector.ValueError(
                                GameDataCatalogLoadStatus.InvalidEnvelope,
                                "ARTIFACT-PATH-DUPLICATE",
                                catalogId,
                                family,
                                string.Empty,
                                path + ".relativePath",
                                "catalog.artifact.path_duplicate",
                                "The manifest selects the same physical artifact more than once.",
                                "Use one unique packaged path per artifact.",
                                index,
                                -1);
                        }

                        artifacts.Add(new GameDataArtifactManifest(
                            family,
                            catalogId,
                            relativePath,
                            artifactSchemaVersion,
                            artifactContentVersion,
                            required,
                            sha256,
                            mediaType,
                            sourceMode,
                            artifactSourceRevision,
                            index));
                    }
                }
            }

            if (collector.HasBlockingDiagnostics)
            {
                return ManifestFailure(collector);
            }

            var manifest = new GameDataCatalogManifest(
                gameId,
                catalogSetId,
                schemaVersion,
                contentVersion,
                minimumRuntimeVersion,
                sourceRevision,
                artifacts,
                manifestBytes.Length,
                ComputeSha256(manifestBytes));
            return new GameDataCatalogManifestValidationResult(
                GameDataCatalogLoadStatus.LoadedPackaged,
                manifest,
                new GameDataCatalogDiagnostic[0]);
        }

        public static GameDataCatalogLoadResult ValidateCatalogSet(
            GameDataCatalogManifest manifest,
            IEnumerable<GameDataCatalogArtifactInput> artifactInputs,
            GameDataCatalogSchemaRegistry schemas,
            GameDataCatalogValidationPolicy policy,
            GameDataCatalogSourceKind sourceKind,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (schemas == null) throw new ArgumentNullException(nameof(schemas));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (!Enum.IsDefined(typeof(GameDataCatalogSourceKind), sourceKind))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
            }

            var collector = new DiagnosticCollector(policy.MaximumDiagnostics);
            var inputsByPath = new Dictionary<string, GameDataCatalogArtifactInput>(StringComparer.Ordinal);
            if (artifactInputs != null)
            {
                foreach (var input in artifactInputs)
                {
                    if (input == null) continue;
                    if (inputsByPath.ContainsKey(input.RelativePath))
                    {
                        collector.ValueError(
                            GameDataCatalogLoadStatus.ReadFailed,
                            "SOURCE-DUPLICATE",
                            manifest.CatalogSetId,
                            string.Empty,
                            string.Empty,
                            "$.artifacts",
                            "catalog.source.duplicate",
                            "The byte source returned more than one result for a path.",
                            "Return exactly one typed result per requested path.",
                            -1,
                            -1);
                    }
                    else
                    {
                        inputsByPath.Add(input.RelativePath, input);
                    }
                }
            }

            var candidates = new List<FamilyCandidate>();
            var candidatesByFamily = new Dictionary<string, FamilyCandidate>(StringComparer.Ordinal);
            var missingOptionalFamilies = new List<string>();
            long aggregateBytes = manifest.ByteLength;

            for (var artifactIndex = 0; artifactIndex < manifest.Artifacts.Count; artifactIndex++)
            {
                var descriptor = manifest.Artifacts[artifactIndex];
                GameDataCatalogFamilySchema schema;
                if (!schemas.TryGet(descriptor.Family, out schema))
                {
                    collector.ValueError(
                        GameDataCatalogLoadStatus.UnsupportedVersion,
                        "FAMILY-UNSUPPORTED",
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        "$.artifacts[" + artifactIndex.ToString(CultureInfo.InvariantCulture) + "].family",
                        "catalog.family.unsupported",
                        "No reviewed runtime schema is registered for this family.",
                        "Register an explicit family schema before claiming support.",
                        artifactIndex,
                        -1);
                    continue;
                }

                if (!schema.SupportsVersion(descriptor.SchemaVersion))
                {
                    collector.ValueError(
                        GameDataCatalogLoadStatus.UnsupportedVersion,
                        "FAMILY-VERSION-UNSUPPORTED",
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        "$.artifacts[" + artifactIndex.ToString(CultureInfo.InvariantCulture) + "].schemaVersion",
                        "catalog.family.version_unsupported",
                        "The selected family schema version is unsupported.",
                        "Use a registered family schema version or update the runtime.",
                        artifactIndex,
                        -1);
                    continue;
                }

                GameDataCatalogArtifactInput input;
                if (!inputsByPath.TryGetValue(descriptor.RelativePath, out input) ||
                    input.Status == GameDataCatalogReadStatus.NotFound)
                {
                    if (descriptor.Required)
                    {
                        collector.ValueError(
                            GameDataCatalogLoadStatus.MissingArtifact,
                            "ARTIFACT-MISSING",
                            descriptor.CatalogId,
                            descriptor.Family,
                            string.Empty,
                            "$.artifacts[" + artifactIndex.ToString(CultureInfo.InvariantCulture) + "].relativePath",
                            "catalog.artifact.missing",
                            "A required packaged artifact is missing.",
                            "Package the exact manifest-selected artifact.",
                            artifactIndex,
                            -1);
                    }
                    else
                    {
                        missingOptionalFamilies.Add(descriptor.Family);
                    }

                    continue;
                }

                if (input.Status != GameDataCatalogReadStatus.Succeeded || input.UnsafeBytes == null)
                {
                    collector.ValueError(
                        MapReadFailure(input.Status),
                        "ARTIFACT-READ-FAILED",
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        "$.artifacts[" + artifactIndex.ToString(CultureInfo.InvariantCulture) + "].relativePath",
                        "catalog.artifact.read_failed",
                        "The packaged artifact could not be read through the selected transport (" + input.FailureCode + ").",
                        "Inspect the typed source failure and package a readable artifact.",
                        artifactIndex,
                        -1);
                    continue;
                }

                aggregateBytes += input.ByteLength;
                if (input.ByteLength > policy.MaximumFamilyBytes || aggregateBytes > policy.MaximumAggregateBytes)
                {
                    collector.ValueError(
                        GameDataCatalogLoadStatus.InvalidEnvelope,
                        "ARTIFACT-SIZE",
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        "$",
                        "catalog.artifact.size",
                        "The artifact or aggregate catalog set exceeds the bounded byte budget.",
                        "Split or reduce catalog data before packaging.",
                        artifactIndex,
                        -1);
                    continue;
                }

                var actualHash = ComputeSha256(input.UnsafeBytes);
                if (!string.Equals(descriptor.Sha256, actualHash, StringComparison.Ordinal))
                {
                    collector.ValueError(
                        GameDataCatalogLoadStatus.HashMismatch,
                        "HASH-MISMATCH",
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        "$",
                        "catalog.artifact.hash_mismatch",
                        "The SHA-256 of the exact packaged bytes does not match the manifest.",
                        "Regenerate the artifact or manifest from the reviewed source.",
                        artifactIndex,
                        -1);
                    continue;
                }

                var beforeFamilyErrors = collector.Count;
                var candidate = ReadFamily(
                    manifest,
                    descriptor,
                    input.UnsafeBytes,
                    actualHash,
                    schema,
                    artifactIndex,
                    policy,
                    collector);
                if (candidate != null && collector.Count == beforeFamilyErrors)
                {
                    candidates.Add(candidate);
                    candidatesByFamily.Add(candidate.Descriptor.Family, candidate);
                }
            }

            ValidateCrossReferences(candidates, candidatesByFamily, collector);
            if (collector.HasBlockingDiagnostics)
            {
                return new GameDataCatalogLoadResult(
                    collector.PrimaryStatus,
                    null,
                    collector.OrderedDiagnostics(),
                    startedAtUtc,
                    completedAtUtc);
            }

            var familySnapshots = new List<GameDataFamilyCatalogSnapshot>();
            for (var index = 0; index < manifest.Artifacts.Count; index++)
            {
                FamilyCandidate candidate;
                if (candidatesByFamily.TryGetValue(manifest.Artifacts[index].Family, out candidate))
                {
                    familySnapshots.Add(candidate.ToSnapshot());
                }
            }

            var snapshot = new GameDataCatalogSetSnapshot(
                manifest.GameId,
                manifest.CatalogSetId,
                manifest.SchemaVersion,
                manifest.ContentVersion,
                manifest.SourceRevision,
                manifest.Sha256,
                sourceKind,
                0,
                manifest.Artifacts,
                familySnapshots,
                missingOptionalFamilies);
            var successDiagnostics = new List<GameDataCatalogDiagnostic>();
            if (sourceKind == GameDataCatalogSourceKind.DevelopmentFallback)
            {
                for (var index = 0; index < familySnapshots.Count; index++)
                {
                    var family = familySnapshots[index];
                    successDiagnostics.Add(new GameDataCatalogDiagnostic(
                        "DEVELOPMENT-FALLBACK",
                        GameDataDiagnosticSeverity.Warning,
                        family.CatalogId,
                        family.Family,
                        string.Empty,
                        "$",
                        "catalog.development_fallback",
                        "This family was loaded from an explicit development fallback source.",
                        "Package reviewed artifacts before production validation.",
                        false,
                        false,
                        index,
                        -1));
                }
            }

            return new GameDataCatalogLoadResult(
                sourceKind == GameDataCatalogSourceKind.Packaged
                    ? GameDataCatalogLoadStatus.LoadedPackaged
                    : GameDataCatalogLoadStatus.LoadedDevelopmentFallback,
                snapshot,
                successDiagnostics,
                startedAtUtc,
                completedAtUtc);
        }

        public static string ComputeSha256(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static FamilyCandidate ReadFamily(
            GameDataCatalogManifest manifest,
            GameDataArtifactManifest descriptor,
            byte[] bytes,
            string sha256,
            GameDataCatalogFamilySchema schema,
            int artifactOrder,
            GameDataCatalogValidationPolicy policy,
            DiagnosticCollector collector)
        {
            StrictJsonValue root;
            try
            {
                root = StrictJsonDocument.Parse(bytes, policy.MaximumFamilyBytes);
            }
            catch (StrictJsonException exception)
            {
                collector.Add(
                    GameDataCatalogLoadStatus.MalformedJson,
                    exception.Code,
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    exception.Path,
                    "catalog.json.malformed",
                    exception.Message,
                    "Correct the strict UTF-8 JSON source.",
                    true,
                    true,
                    artifactOrder,
                    -1);
                return null;
            }

            var rootObject = root as StrictJsonObject;
            if (rootObject == null)
            {
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidEnvelope,
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    "$",
                    "object",
                    root.Kind.ToString(),
                    artifactOrder,
                    -1);
                return null;
            }

            CheckKnownFields(
                rootObject,
                EnvelopeFields,
                "$",
                GameDataCatalogLoadStatus.InvalidEnvelope,
                descriptor.CatalogId,
                descriptor.Family,
                string.Empty,
                artifactOrder,
                -1,
                collector,
                "aliases");

            var gameId = ReadString(rootObject, "gameId", "$", true, descriptor.CatalogId, descriptor.Family, artifactOrder, -1, collector);
            var catalogId = ReadString(rootObject, "catalogId", "$", true, descriptor.CatalogId, descriptor.Family, artifactOrder, -1, collector);
            var family = ReadString(rootObject, "family", "$", true, descriptor.CatalogId, descriptor.Family, artifactOrder, -1, collector);
            var schemaVersion = ReadInt32(rootObject, "schemaVersion", "$", descriptor.CatalogId, descriptor.Family, artifactOrder, -1, collector);
            var contentVersion = ReadString(rootObject, "contentVersion", "$", true, descriptor.CatalogId, descriptor.Family, artifactOrder, -1, collector);
            var sourceRevision = ReadString(rootObject, "sourceRevision", "$", true, descriptor.CatalogId, descriptor.Family, artifactOrder, -1, collector);

            CheckExactIdentity(gameId, manifest.GameId, "$.gameId", "GAME-ID", descriptor, artifactOrder, collector);
            CheckExactIdentity(catalogId, descriptor.CatalogId, "$.catalogId", "CATALOG-ID-MISMATCH", descriptor, artifactOrder, collector);
            CheckExactIdentity(family, descriptor.Family, "$.family", "FAMILY-MISMATCH", descriptor, artifactOrder, collector);
            CheckExactIdentity(contentVersion, descriptor.ContentVersion, "$.contentVersion", "CONTENT-VERSION-MISMATCH", descriptor, artifactOrder, collector);
            CheckExactIdentity(sourceRevision, descriptor.SourceRevision, "$.sourceRevision", "SOURCE-REVISION-MISMATCH", descriptor, artifactOrder, collector);
            if (schemaVersion != descriptor.SchemaVersion || !schema.SupportsVersion(schemaVersion))
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.UnsupportedVersion,
                    "FAMILY-VERSION-UNSUPPORTED",
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    "$.schemaVersion",
                    "catalog.family.version_unsupported",
                    "The envelope schema version does not match a supported manifest selection.",
                    "Regenerate the manifest and family artifact from one reviewed schema.",
                    artifactOrder,
                    -1);
            }

            var records = ReadRecords(rootObject, descriptor, schema, artifactOrder, policy, collector);
            var aliases = ReadAliases(rootObject, descriptor, artifactOrder, policy, collector);
            ValidateAliases(records, aliases, descriptor, artifactOrder, collector);

            return new FamilyCandidate(descriptor, sha256, bytes.Length, records, aliases);
        }

        private static List<RecordCandidate> ReadRecords(
            StrictJsonObject root,
            GameDataArtifactManifest descriptor,
            GameDataCatalogFamilySchema schema,
            int artifactOrder,
            GameDataCatalogValidationPolicy policy,
            DiagnosticCollector collector)
        {
            var records = new List<RecordCandidate>();
            StrictJsonValue recordsValue;
            if (!root.TryGet("records", out recordsValue))
            {
                // The envelope required-field pass already emitted the deterministic diagnostic.
                return records;
            }

            var recordsArray = recordsValue as StrictJsonArray;
            if (recordsArray == null)
            {
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidEnvelope,
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    "$.records",
                    "array",
                    recordsValue.Kind.ToString(),
                    artifactOrder,
                    -1);
                return records;
            }

            if ((!schema.AllowEmptyRecords && recordsArray.Items.Count == 0) ||
                recordsArray.Items.Count > policy.MaximumRecordsPerFamily)
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.InvalidRecord,
                    "RECORD-COUNT",
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    "$.records",
                    "catalog.record.count",
                    "The family record count is outside its supported bounded range.",
                    "Provide the required bounded record set.",
                    artifactOrder,
                    -1);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < recordsArray.Items.Count && index < policy.MaximumRecordsPerFamily; index++)
            {
                var path = "$.records[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                var recordObject = recordsArray.Items[index] as StrictJsonObject;
                if (recordObject == null)
                {
                    collector.TypeError(
                        GameDataCatalogLoadStatus.InvalidRecord,
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        path,
                        "object",
                        recordsArray.Items[index].Kind.ToString(),
                        artifactOrder,
                        index);
                    continue;
                }

                CheckRecordFields(recordObject, schema, path, descriptor, artifactOrder, index, collector);
                var id = ReadString(
                    recordObject,
                    "id",
                    path,
                    true,
                    descriptor.CatalogId,
                    descriptor.Family,
                    artifactOrder,
                    index,
                    collector,
                    GameDataCatalogLoadStatus.InvalidRecord);
                if (!GameDataCatalogIdentifiers.IsCanonicalStableId(id))
                {
                    collector.ValueError(
                        GameDataCatalogLoadStatus.InvalidRecord,
                        "RECORD-ID",
                        descriptor.CatalogId,
                        descriptor.Family,
                        id,
                        path + ".id",
                        "catalog.record.id_invalid",
                        "The record ID is not a canonical lower-snake-case identifier.",
                        "Use a stable canonical ID and an explicit alias for legacy identity.",
                        artifactOrder,
                        index);
                }

                if (!ids.Add(id))
                {
                    collector.ValueError(
                        GameDataCatalogLoadStatus.InvalidRecord,
                        "RECORD-ID-DUPLICATE",
                        descriptor.CatalogId,
                        descriptor.Family,
                        id,
                        path + ".id",
                        "catalog.record.id_duplicate",
                        "The family contains a duplicate canonical record ID.",
                        "Keep exactly one record per canonical ID.",
                        artifactOrder,
                        index);
                }

                var fields = new List<KeyValuePair<string, GameDataValue>>();
                var references = new List<PendingReference>();
                for (var fieldIndex = 0; fieldIndex < schema.Fields.Count; fieldIndex++)
                {
                    var rule = schema.Fields[fieldIndex];
                    StrictJsonValue value;
                    if (!recordObject.TryGet(rule.Name, out value))
                    {
                        if (rule.Required)
                        {
                            collector.MissingField(
                                GameDataCatalogLoadStatus.InvalidRecord,
                                descriptor.CatalogId,
                                descriptor.Family,
                                id,
                                path + "." + rule.Name,
                                artifactOrder,
                                index);
                        }

                        continue;
                    }

                    var materialized = ValidateAndMaterialize(
                        value,
                        rule,
                        path + "." + rule.Name,
                        descriptor,
                        id,
                        artifactOrder,
                        index,
                        references,
                        collector);
                    if (materialized != null)
                    {
                        fields.Add(new KeyValuePair<string, GameDataValue>(rule.Name, materialized));
                    }
                }

                records.Add(new RecordCandidate(id, fields, references, index));
            }

            return records;
        }

        private static GameDataValue ValidateAndMaterialize(
            StrictJsonValue value,
            GameDataCatalogFieldRule rule,
            string path,
            GameDataArtifactManifest descriptor,
            string recordId,
            int artifactOrder,
            int recordOrder,
            List<PendingReference> references,
            DiagnosticCollector collector)
        {
            if (value is StrictJsonNull)
            {
                if (rule.AllowNull) return GameDataNullValue.Instance;
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidRecord,
                    descriptor.CatalogId,
                    descriptor.Family,
                    recordId,
                    path,
                    rule.Kind.ToString(),
                    GameDataValueKind.Null.ToString(),
                    artifactOrder,
                    recordOrder);
                return null;
            }

            if (value.Kind != rule.Kind)
            {
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidRecord,
                    descriptor.CatalogId,
                    descriptor.Family,
                    recordId,
                    path,
                    rule.Kind.ToString(),
                    value.Kind.ToString(),
                    artifactOrder,
                    recordOrder);
                return null;
            }

            var stringValue = value as StrictJsonString;
            if (stringValue != null)
            {
                if (rule.NonBlank && string.IsNullOrWhiteSpace(stringValue.Value))
                {
                    collector.FieldValueError(descriptor, recordId, path, "FIELD-BLANK", "A nonblank string is required.", artifactOrder, recordOrder);
                }
                if (rule.StableId && !GameDataCatalogIdentifiers.IsCanonicalStableId(stringValue.Value))
                {
                    collector.FieldValueError(descriptor, recordId, path, "FIELD-ID", "A canonical lower-snake-case ID is required.", artifactOrder, recordOrder);
                }
                if (!rule.IsAllowedString(stringValue.Value))
                {
                    collector.FieldValueError(descriptor, recordId, path, "FIELD-ENUM", "The string value is not in the reviewed supported set.", artifactOrder, recordOrder);
                }
                if (!string.IsNullOrEmpty(rule.ReferenceFamily) && !string.IsNullOrWhiteSpace(stringValue.Value))
                {
                    references.Add(new PendingReference(
                        descriptor.Family,
                        descriptor.CatalogId,
                        recordId,
                        path,
                        rule.ReferenceFamily,
                        stringValue.Value,
                        artifactOrder,
                        recordOrder));
                }

                return new GameDataStringValue(stringValue.Value);
            }

            var numberValue = value as StrictJsonNumber;
            if (numberValue != null)
            {
                long integer;
                if (rule.IntegerOnly && !long.TryParse(
                        numberValue.RawValue,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out integer))
                {
                    collector.FieldValueError(descriptor, recordId, path, "FIELD-INTEGER", "An exact 64-bit JSON integer is required.", artifactOrder, recordOrder);
                }
                if (rule.MinimumNumber.HasValue && numberValue.Value < rule.MinimumNumber.Value)
                {
                    collector.FieldValueError(descriptor, recordId, path, "FIELD-RANGE", "The numeric value is below the reviewed minimum.", artifactOrder, recordOrder);
                }
                if (rule.MaximumNumber.HasValue && numberValue.Value > rule.MaximumNumber.Value)
                {
                    collector.FieldValueError(descriptor, recordId, path, "FIELD-RANGE", "The numeric value exceeds the reviewed maximum.", artifactOrder, recordOrder);
                }

                return new GameDataNumberValue(numberValue.RawValue, numberValue.Value);
            }

            var booleanValue = value as StrictJsonBoolean;
            if (booleanValue != null) return new GameDataBooleanValue(booleanValue.Value);

            var arrayValue = value as StrictJsonArray;
            if (arrayValue != null)
            {
                if (arrayValue.Items.Count < rule.MinimumItems || arrayValue.Items.Count > rule.MaximumItems)
                {
                    collector.FieldValueError(descriptor, recordId, path, "FIELD-COUNT", "The array item count is outside the reviewed bounded range.", artifactOrder, recordOrder);
                }

                var items = new List<GameDataValue>();
                for (var index = 0; index < arrayValue.Items.Count && index < rule.MaximumItems; index++)
                {
                    var item = ValidateAndMaterialize(
                        arrayValue.Items[index],
                        rule.ItemRule,
                        path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        descriptor,
                        recordId,
                        artifactOrder,
                        recordOrder,
                        references,
                        collector);
                    if (item != null) items.Add(item);
                }

                return new GameDataArrayValue(items);
            }

            var objectValue = value as StrictJsonObject;
            if (objectValue != null)
            {
                CheckObjectRuleFields(objectValue, rule, path, descriptor, recordId, artifactOrder, recordOrder, collector);
                var properties = new List<KeyValuePair<string, GameDataValue>>();
                for (var index = 0; index < rule.ObjectFields.Count; index++)
                {
                    var childRule = rule.ObjectFields[index];
                    StrictJsonValue child;
                    if (!objectValue.TryGet(childRule.Name, out child))
                    {
                        if (childRule.Required)
                        {
                            collector.MissingField(
                                GameDataCatalogLoadStatus.InvalidRecord,
                                descriptor.CatalogId,
                                descriptor.Family,
                                recordId,
                                path + "." + childRule.Name,
                                artifactOrder,
                                recordOrder);
                        }

                        continue;
                    }

                    var materialized = ValidateAndMaterialize(
                        child,
                        childRule,
                        path + "." + childRule.Name,
                        descriptor,
                        recordId,
                        artifactOrder,
                        recordOrder,
                        references,
                        collector);
                    if (materialized != null)
                    {
                        properties.Add(new KeyValuePair<string, GameDataValue>(childRule.Name, materialized));
                    }
                }

                return new GameDataObjectValue(properties);
            }

            return null;
        }

        private static List<AliasCandidate> ReadAliases(
            StrictJsonObject root,
            GameDataArtifactManifest descriptor,
            int artifactOrder,
            GameDataCatalogValidationPolicy policy,
            DiagnosticCollector collector)
        {
            var aliases = new List<AliasCandidate>();
            StrictJsonValue aliasesValue;
            if (!root.TryGet("aliases", out aliasesValue)) return aliases;

            var aliasesArray = aliasesValue as StrictJsonArray;
            if (aliasesArray == null)
            {
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidRecord,
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    "$.aliases",
                    "array",
                    aliasesValue.Kind.ToString(),
                    artifactOrder,
                    -1);
                return aliases;
            }

            if (aliasesArray.Items.Count > policy.MaximumAliasesPerFamily)
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.InvalidRecord,
                    "ALIAS-COUNT",
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    "$.aliases",
                    "catalog.alias.count",
                    "The alias count exceeds the bounded supported maximum.",
                    "Reduce the alias table or split the migration.",
                    artifactOrder,
                    -1);
            }

            for (var index = 0; index < aliasesArray.Items.Count && index < policy.MaximumAliasesPerFamily; index++)
            {
                var path = "$.aliases[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                var aliasObject = aliasesArray.Items[index] as StrictJsonObject;
                if (aliasObject == null)
                {
                    collector.TypeError(
                        GameDataCatalogLoadStatus.InvalidRecord,
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        path,
                        "object",
                        aliasesArray.Items[index].Kind.ToString(),
                        artifactOrder,
                        index);
                    continue;
                }

                CheckKnownFields(
                    aliasObject,
                    AliasFields,
                    path,
                    GameDataCatalogLoadStatus.InvalidRecord,
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    artifactOrder,
                    index,
                    collector);
                var legacyId = ReadString(aliasObject, "legacyId", path, true, descriptor.CatalogId, descriptor.Family, artifactOrder, index, collector, GameDataCatalogLoadStatus.InvalidRecord);
                var canonicalId = ReadString(aliasObject, "canonicalId", path, true, descriptor.CatalogId, descriptor.Family, artifactOrder, index, collector, GameDataCatalogLoadStatus.InvalidRecord);
                var introducedVersion = ReadInt32(aliasObject, "introducedVersion", path, descriptor.CatalogId, descriptor.Family, artifactOrder, index, collector, GameDataCatalogLoadStatus.InvalidRecord);
                var retirementVersion = ReadNullableInt32(aliasObject, "retirementVersion", path, descriptor.CatalogId, descriptor.Family, artifactOrder, index, collector);
                var migrationIssue = ReadString(aliasObject, "migrationIssue", path, true, descriptor.CatalogId, descriptor.Family, artifactOrder, index, collector, GameDataCatalogLoadStatus.InvalidRecord);

                if (legacyId.Length > 256)
                {
                    collector.FieldValueError(descriptor, string.Empty, path + ".legacyId", "ALIAS-ID", "The legacy ID exceeds the bounded length.", artifactOrder, index);
                }
                if (!GameDataCatalogIdentifiers.IsCanonicalStableId(canonicalId))
                {
                    collector.FieldValueError(descriptor, string.Empty, path + ".canonicalId", "ALIAS-TARGET", "The alias target must be a canonical stable ID.", artifactOrder, index);
                }
                if (introducedVersion <= 0 || (retirementVersion.HasValue && retirementVersion.Value < introducedVersion))
                {
                    collector.FieldValueError(descriptor, string.Empty, path, "ALIAS-VERSION", "Alias introduction and retirement versions are inconsistent.", artifactOrder, index);
                }

                aliases.Add(new AliasCandidate(
                    new GameDataCatalogAlias(legacyId, canonicalId, introducedVersion, retirementVersion, migrationIssue),
                    index));
            }

            return aliases;
        }

        private static void ValidateAliases(
            IReadOnlyList<RecordCandidate> records,
            IReadOnlyList<AliasCandidate> aliases,
            GameDataArtifactManifest descriptor,
            int artifactOrder,
            DiagnosticCollector collector)
        {
            var canonicalIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < records.Count; index++) canonicalIds.Add(records[index].Id);

            var aliasMap = new Dictionary<string, AliasCandidate>(StringComparer.Ordinal);
            for (var index = 0; index < aliases.Count; index++)
            {
                var candidate = aliases[index];
                if (canonicalIds.Contains(candidate.Alias.LegacyId))
                {
                    collector.FieldValueError(
                        descriptor,
                        candidate.Alias.LegacyId,
                        "$.aliases[" + candidate.Order.ToString(CultureInfo.InvariantCulture) + "].legacyId",
                        "ALIAS-SHADOW",
                        "A legacy alias cannot shadow any canonical record ID.",
                        artifactOrder,
                        candidate.Order);
                }

                if (aliasMap.ContainsKey(candidate.Alias.LegacyId))
                {
                    collector.FieldValueError(
                        descriptor,
                        candidate.Alias.LegacyId,
                        "$.aliases[" + candidate.Order.ToString(CultureInfo.InvariantCulture) + "].legacyId",
                        "ALIAS-DUPLICATE",
                        "Each exact legacy ID may appear only once.",
                        artifactOrder,
                        candidate.Order);
                }
                else
                {
                    aliasMap.Add(candidate.Alias.LegacyId, candidate);
                }
            }

            for (var index = 0; index < aliases.Count; index++)
            {
                var candidate = aliases[index];
                if (aliasMap.ContainsKey(candidate.Alias.CanonicalId))
                {
                    var cycle = AliasPathReturnsTo(candidate.Alias.LegacyId, candidate.Alias.CanonicalId, aliasMap);
                    collector.FieldValueError(
                        descriptor,
                        candidate.Alias.LegacyId,
                        "$.aliases[" + candidate.Order.ToString(CultureInfo.InvariantCulture) + "].canonicalId",
                        cycle ? "ALIAS-CYCLE" : "ALIAS-CHAIN",
                        cycle ? "Alias cycles are prohibited." : "Alias chains are prohibited; targets must be canonical records.",
                        artifactOrder,
                        candidate.Order);
                }
                else if (!canonicalIds.Contains(candidate.Alias.CanonicalId))
                {
                    collector.FieldValueError(
                        descriptor,
                        candidate.Alias.LegacyId,
                        "$.aliases[" + candidate.Order.ToString(CultureInfo.InvariantCulture) + "].canonicalId",
                        "ALIAS-TARGET-MISSING",
                        "The alias target does not name a canonical record in this family.",
                        artifactOrder,
                        candidate.Order);
                }
            }
        }

        private static bool AliasPathReturnsTo(
            string start,
            string target,
            IReadOnlyDictionary<string, AliasCandidate> aliases)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { start };
            var current = target;
            while (aliases.ContainsKey(current))
            {
                if (!visited.Add(current)) return true;
                current = aliases[current].Alias.CanonicalId;
            }

            return string.Equals(current, start, StringComparison.Ordinal);
        }

        private static void ValidateCrossReferences(
            IReadOnlyList<FamilyCandidate> candidates,
            IReadOnlyDictionary<string, FamilyCandidate> candidatesByFamily,
            DiagnosticCollector collector)
        {
            for (var familyIndex = 0; familyIndex < candidates.Count; familyIndex++)
            {
                var family = candidates[familyIndex];
                for (var recordIndex = 0; recordIndex < family.Records.Count; recordIndex++)
                {
                    var record = family.Records[recordIndex];
                    for (var referenceIndex = 0; referenceIndex < record.References.Count; referenceIndex++)
                    {
                        var reference = record.References[referenceIndex];
                        FamilyCandidate targetFamily;
                        if (!candidatesByFamily.TryGetValue(reference.TargetFamily, out targetFamily) ||
                            !targetFamily.ContainsRecord(reference.TargetId))
                        {
                            collector.Add(
                                GameDataCatalogLoadStatus.CrossReferenceFailure,
                                "REFERENCE-MISSING",
                                reference.SourceCatalogId,
                                reference.SourceFamily,
                                reference.SourceRecordId,
                                reference.FieldPath,
                                "catalog.reference.missing",
                                "The exact canonical cross-family reference could not be resolved.",
                                "Package and validate the referenced canonical record.",
                                true,
                                true,
                                reference.ArtifactOrder,
                                reference.RecordOrder);
                        }
                    }
                }
            }
        }

        private static void CheckRecordFields(
            StrictJsonObject value,
            GameDataCatalogFamilySchema schema,
            string path,
            GameDataArtifactManifest descriptor,
            int artifactOrder,
            int recordOrder,
            DiagnosticCollector collector)
        {
            for (var index = 0; index < value.Properties.Count; index++)
            {
                var name = value.Properties[index].Name;
                GameDataCatalogFieldRule unused;
                if (!string.Equals(name, "id", StringComparison.Ordinal) && !schema.TryGetField(name, out unused))
                {
                    collector.UnknownField(
                        GameDataCatalogLoadStatus.InvalidRecord,
                        descriptor.CatalogId,
                        descriptor.Family,
                        string.Empty,
                        path + "." + name,
                        artifactOrder,
                        recordOrder);
                }
            }
        }

        private static void CheckObjectRuleFields(
            StrictJsonObject value,
            GameDataCatalogFieldRule rule,
            string path,
            GameDataArtifactManifest descriptor,
            string recordId,
            int artifactOrder,
            int recordOrder,
            DiagnosticCollector collector)
        {
            for (var index = 0; index < value.Properties.Count; index++)
            {
                GameDataCatalogFieldRule unused;
                if (!rule.TryGetObjectField(value.Properties[index].Name, out unused))
                {
                    collector.UnknownField(
                        GameDataCatalogLoadStatus.InvalidRecord,
                        descriptor.CatalogId,
                        descriptor.Family,
                        recordId,
                        path + "." + value.Properties[index].Name,
                        artifactOrder,
                        recordOrder);
                }
            }
        }

        private static void CheckKnownFields(
            StrictJsonObject value,
            IReadOnlyList<string> knownFields,
            string path,
            GameDataCatalogLoadStatus status,
            string catalogId,
            string family,
            string recordId,
            int artifactOrder,
            int recordOrder,
            DiagnosticCollector collector,
            string optionalField = null)
        {
            var known = new HashSet<string>(knownFields, StringComparer.Ordinal);
            for (var index = 0; index < value.Properties.Count; index++)
            {
                var name = value.Properties[index].Name;
                if (!known.Contains(name))
                {
                    collector.UnknownField(status, catalogId, family, recordId, path + "." + name, artifactOrder, recordOrder);
                }
            }

            for (var index = 0; index < knownFields.Count; index++)
            {
                var field = knownFields[index];
                StrictJsonValue unused;
                if (!string.Equals(field, optionalField, StringComparison.Ordinal) && !value.TryGet(field, out unused))
                {
                    collector.MissingField(status, catalogId, family, recordId, path + "." + field, artifactOrder, recordOrder);
                }
            }
        }

        private static string ReadString(
            StrictJsonObject value,
            string field,
            string path,
            bool requireNonBlank,
            string catalogId,
            string family,
            int artifactOrder,
            int recordOrder,
            DiagnosticCollector collector,
            GameDataCatalogLoadStatus status = GameDataCatalogLoadStatus.InvalidEnvelope)
        {
            StrictJsonValue fieldValue;
            if (!value.TryGet(field, out fieldValue)) return string.Empty;
            var stringValue = fieldValue as StrictJsonString;
            if (stringValue == null)
            {
                collector.TypeError(
                    status,
                    catalogId,
                    family,
                    string.Empty,
                    path + "." + field,
                    "string",
                    fieldValue.Kind.ToString(),
                    artifactOrder,
                    recordOrder);
                return string.Empty;
            }

            if (requireNonBlank && string.IsNullOrWhiteSpace(stringValue.Value))
            {
                collector.ValueError(
                    status,
                    "FIELD-BLANK",
                    catalogId,
                    family,
                    string.Empty,
                    path + "." + field,
                    "catalog.field.blank",
                    "A required string field is blank.",
                    "Provide the exact reviewed nonblank value.",
                    artifactOrder,
                    recordOrder);
            }

            return stringValue.Value;
        }

        private static int ReadInt32(
            StrictJsonObject value,
            string field,
            string path,
            string catalogId,
            string family,
            int artifactOrder,
            int recordOrder,
            DiagnosticCollector collector,
            GameDataCatalogLoadStatus status = GameDataCatalogLoadStatus.InvalidEnvelope)
        {
            StrictJsonValue fieldValue;
            if (!value.TryGet(field, out fieldValue)) return 0;
            var number = fieldValue as StrictJsonNumber;
            int parsed;
            if (number == null || !int.TryParse(number.RawValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
            {
                collector.TypeError(
                    status,
                    catalogId,
                    family,
                    string.Empty,
                    path + "." + field,
                    "32-bit JSON integer",
                    fieldValue.Kind.ToString(),
                    artifactOrder,
                    recordOrder);
                return 0;
            }

            return parsed;
        }

        private static int? ReadNullableInt32(
            StrictJsonObject value,
            string field,
            string path,
            string catalogId,
            string family,
            int artifactOrder,
            int recordOrder,
            DiagnosticCollector collector)
        {
            StrictJsonValue fieldValue;
            if (!value.TryGet(field, out fieldValue)) return null;
            if (fieldValue is StrictJsonNull) return null;
            var number = fieldValue as StrictJsonNumber;
            int parsed;
            if (number == null || !int.TryParse(number.RawValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
            {
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidRecord,
                    catalogId,
                    family,
                    string.Empty,
                    path + "." + field,
                    "null or 32-bit JSON integer",
                    fieldValue.Kind.ToString(),
                    artifactOrder,
                    recordOrder);
                return null;
            }

            return parsed;
        }

        private static bool ReadBoolean(
            StrictJsonObject value,
            string field,
            string path,
            string catalogId,
            string family,
            int artifactOrder,
            int recordOrder,
            DiagnosticCollector collector)
        {
            StrictJsonValue fieldValue;
            if (!value.TryGet(field, out fieldValue)) return false;
            var boolean = fieldValue as StrictJsonBoolean;
            if (boolean == null)
            {
                collector.TypeError(
                    GameDataCatalogLoadStatus.InvalidEnvelope,
                    catalogId,
                    family,
                    string.Empty,
                    path + "." + field,
                    "boolean",
                    fieldValue.Kind.ToString(),
                    artifactOrder,
                    recordOrder);
                return false;
            }

            return boolean.Value;
        }

        private static void CheckExactIdentity(
            string actual,
            string expected,
            string path,
            string code,
            GameDataArtifactManifest descriptor,
            int artifactOrder,
            DiagnosticCollector collector)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                collector.ValueError(
                    GameDataCatalogLoadStatus.InvalidEnvelope,
                    code,
                    descriptor.CatalogId,
                    descriptor.Family,
                    string.Empty,
                    path,
                    "catalog.envelope.identity_mismatch",
                    "The family envelope identity does not exactly match its manifest entry.",
                    "Regenerate the envelope and manifest from the same reviewed source.",
                    artifactOrder,
                    -1);
            }
        }

        private static GameDataCatalogManifestValidationResult ManifestFailure(DiagnosticCollector collector)
        {
            return new GameDataCatalogManifestValidationResult(
                collector.PrimaryStatus,
                null,
                collector.OrderedDiagnostics());
        }

        private static GameDataCatalogLoadStatus MapReadFailure(GameDataCatalogReadStatus status)
        {
            switch (status)
            {
                case GameDataCatalogReadStatus.Cancelled: return GameDataCatalogLoadStatus.Cancelled;
                case GameDataCatalogReadStatus.TimedOut: return GameDataCatalogLoadStatus.TimedOut;
                case GameDataCatalogReadStatus.Disposed: return GameDataCatalogLoadStatus.Disposed;
                case GameDataCatalogReadStatus.NotFound: return GameDataCatalogLoadStatus.MissingArtifact;
                default: return GameDataCatalogLoadStatus.ReadFailed;
            }
        }

        private sealed class FamilyCandidate
        {
            private readonly HashSet<string> recordIds;

            public FamilyCandidate(
                GameDataArtifactManifest descriptor,
                string sha256,
                int byteLength,
                List<RecordCandidate> records,
                List<AliasCandidate> aliases)
            {
                Descriptor = descriptor;
                Sha256 = sha256;
                ByteLength = byteLength;
                Records = records;
                Aliases = aliases;
                recordIds = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < records.Count; index++) recordIds.Add(records[index].Id);
            }

            public GameDataArtifactManifest Descriptor { get; }
            public string Sha256 { get; }
            public int ByteLength { get; }
            public List<RecordCandidate> Records { get; }
            public List<AliasCandidate> Aliases { get; }

            public bool ContainsRecord(string id)
            {
                return recordIds.Contains(id);
            }

            public GameDataFamilyCatalogSnapshot ToSnapshot()
            {
                var records = new List<GameDataCatalogRecord>();
                for (var index = 0; index < Records.Count; index++) records.Add(Records[index].ToRecord());
                var aliases = new List<GameDataCatalogAlias>();
                for (var index = 0; index < Aliases.Count; index++) aliases.Add(Aliases[index].Alias);
                return new GameDataFamilyCatalogSnapshot(
                    Descriptor.Family,
                    Descriptor.CatalogId,
                    Descriptor.SchemaVersion,
                    Descriptor.ContentVersion,
                    Descriptor.SourceRevision,
                    Sha256,
                    ByteLength,
                    records,
                    aliases);
            }
        }

        private sealed class RecordCandidate
        {
            public RecordCandidate(
                string id,
                List<KeyValuePair<string, GameDataValue>> fields,
                List<PendingReference> references,
                int order)
            {
                Id = id;
                Fields = fields;
                References = references;
                Order = order;
            }

            public string Id { get; }
            public List<KeyValuePair<string, GameDataValue>> Fields { get; }
            public List<PendingReference> References { get; }
            public int Order { get; }

            public GameDataCatalogRecord ToRecord()
            {
                return new GameDataCatalogRecord(Id, Fields);
            }
        }

        private sealed class AliasCandidate
        {
            public AliasCandidate(GameDataCatalogAlias alias, int order)
            {
                Alias = alias;
                Order = order;
            }

            public GameDataCatalogAlias Alias { get; }
            public int Order { get; }
        }

        private sealed class PendingReference
        {
            public PendingReference(
                string sourceFamily,
                string sourceCatalogId,
                string sourceRecordId,
                string fieldPath,
                string targetFamily,
                string targetId,
                int artifactOrder,
                int recordOrder)
            {
                SourceFamily = sourceFamily;
                SourceCatalogId = sourceCatalogId;
                SourceRecordId = sourceRecordId;
                FieldPath = fieldPath;
                TargetFamily = targetFamily;
                TargetId = targetId;
                ArtifactOrder = artifactOrder;
                RecordOrder = recordOrder;
            }

            public string SourceFamily { get; }
            public string SourceCatalogId { get; }
            public string SourceRecordId { get; }
            public string FieldPath { get; }
            public string TargetFamily { get; }
            public string TargetId { get; }
            public int ArtifactOrder { get; }
            public int RecordOrder { get; }
        }

        private sealed class DiagnosticCollector
        {
            private readonly int maximum;
            private readonly List<DiagnosticEntry> entries = new List<DiagnosticEntry>();
            private bool limitReported;

            public DiagnosticCollector(int maximum)
            {
                this.maximum = maximum;
            }

            public int Count => entries.Count;
            public bool HasBlockingDiagnostics => entries.Count > 0;

            public GameDataCatalogLoadStatus PrimaryStatus
            {
                get
                {
                    var ordered = OrderedEntries();
                    return ordered.Count == 0 ? GameDataCatalogLoadStatus.LoadedPackaged : ordered[0].Status;
                }
            }

            public void Add(
                GameDataCatalogLoadStatus status,
                string code,
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
                if (entries.Count >= maximum)
                {
                    if (!limitReported && entries.Count > 0)
                    {
                        limitReported = true;
                        var last = entries[entries.Count - 1];
                        entries[entries.Count - 1] = new DiagnosticEntry(
                            GameDataCatalogLoadStatus.InvalidEnvelope,
                            new GameDataCatalogDiagnostic(
                                "DIAGNOSTIC-LIMIT",
                                GameDataDiagnosticSeverity.Error,
                                last.Diagnostic.CatalogId,
                                last.Diagnostic.Family,
                                last.Diagnostic.RecordId,
                                last.Diagnostic.FieldPath,
                                "catalog.diagnostic.limit",
                                "Additional validation diagnostics were suppressed at the configured bound.",
                                "Correct reported failures before validating again.",
                                true,
                                true,
                                last.Diagnostic.ArtifactOrder,
                                last.Diagnostic.RecordOrder));
                    }
                    return;
                }

                entries.Add(new DiagnosticEntry(
                    status,
                    new GameDataCatalogDiagnostic(
                        code,
                        GameDataDiagnosticSeverity.Error,
                        catalogId,
                        family,
                        recordId,
                        fieldPath,
                        messageKey,
                        technicalMessage,
                        action,
                        blocksFamily,
                        blocksCatalogSet,
                        artifactOrder,
                        recordOrder)));
            }

            public void MissingField(
                GameDataCatalogLoadStatus status,
                string catalogId,
                string family,
                string recordId,
                string path,
                int artifactOrder,
                int recordOrder)
            {
                Add(status, "FIELD-MISSING", catalogId, family, recordId, path,
                    "catalog.field.missing", "A required field is missing.",
                    "Add the exact field required by the supported schema.", true, true, artifactOrder, recordOrder);
            }

            public void UnknownField(
                GameDataCatalogLoadStatus status,
                string catalogId,
                string family,
                string recordId,
                string path,
                int artifactOrder,
                int recordOrder)
            {
                Add(status, "FIELD-UNKNOWN", catalogId, family, recordId, path,
                    "catalog.field.unknown", "The current strict schema does not support this field.",
                    "Remove the field or introduce a reviewed schema version.", true, true, artifactOrder, recordOrder);
            }

            public void TypeError(
                GameDataCatalogLoadStatus status,
                string catalogId,
                string family,
                string recordId,
                string path,
                string expected,
                string actual,
                int artifactOrder,
                int recordOrder)
            {
                Add(status, "FIELD-TYPE", catalogId, family, recordId, path,
                    "catalog.field.type", "The JSON value has the wrong strict type (expected " + expected + ", actual " + actual + ").",
                    "Use the exact type required by the supported schema.", true, true, artifactOrder, recordOrder);
            }

            public void ValueError(
                GameDataCatalogLoadStatus status,
                string code,
                string catalogId,
                string family,
                string recordId,
                string path,
                string messageKey,
                string message,
                string action,
                int artifactOrder,
                int recordOrder)
            {
                Add(status, code, catalogId, family, recordId, path, messageKey, message, action,
                    true, true, artifactOrder, recordOrder);
            }

            public void FieldValueError(
                GameDataArtifactManifest descriptor,
                string recordId,
                string path,
                string code,
                string message,
                int artifactOrder,
                int recordOrder)
            {
                ValueError(GameDataCatalogLoadStatus.InvalidRecord, code, descriptor.CatalogId,
                    descriptor.Family, recordId, path, "catalog.record.invalid", message,
                    "Correct the record using the reviewed family schema.", artifactOrder, recordOrder);
            }

            public IReadOnlyList<GameDataCatalogDiagnostic> OrderedDiagnostics()
            {
                var ordered = OrderedEntries();
                var result = new List<GameDataCatalogDiagnostic>(ordered.Count);
                for (var index = 0; index < ordered.Count; index++) result.Add(ordered[index].Diagnostic);
                return ImmutableCollections.Freeze(result);
            }

            private List<DiagnosticEntry> OrderedEntries()
            {
                var ordered = new List<DiagnosticEntry>(entries);
                ordered.Sort(DiagnosticEntry.Compare);
                return ordered;
            }
        }

        private sealed class DiagnosticEntry
        {
            public DiagnosticEntry(GameDataCatalogLoadStatus status, GameDataCatalogDiagnostic diagnostic)
            {
                Status = status;
                Diagnostic = diagnostic;
            }

            public GameDataCatalogLoadStatus Status { get; }
            public GameDataCatalogDiagnostic Diagnostic { get; }

            public static int Compare(DiagnosticEntry left, DiagnosticEntry right)
            {
                var result = left.Diagnostic.ArtifactOrder.CompareTo(right.Diagnostic.ArtifactOrder);
                if (result != 0) return result;
                result = left.Diagnostic.RecordOrder.CompareTo(right.Diagnostic.RecordOrder);
                if (result != 0) return result;
                result = string.CompareOrdinal(left.Diagnostic.FieldPath, right.Diagnostic.FieldPath);
                if (result != 0) return result;
                return string.CompareOrdinal(left.Diagnostic.Code, right.Diagnostic.Code);
            }
        }
    }
}
