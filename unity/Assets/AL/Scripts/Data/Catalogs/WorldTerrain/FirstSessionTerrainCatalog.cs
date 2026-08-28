using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Data.Catalogs.WorldTerrain
{
    public static class FirstSessionTerrainContract
    {
        public const string FileName = "al_first_session_terrain_catalog.json";
        public const string SupportedVersion = "0.1.0";
        public const string CatalogId = "al_first_session_terrain_catalog";
        public const string AuthorityStatus = "mvp_procedural_replaceable";
        public const int MaximumBytes = 32 * 1024;
        public const int MaximumDiagnostics = 64;
    }

    public enum FirstSessionTerrainLoadStatus
    {
        Accepted,
        Rejected,
        UnsupportedVersion
    }

    public sealed class FirstSessionTerrainDiagnostic
    {
        internal FirstSessionTerrainDiagnostic(string code, string path, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
        public string Fingerprint => string.Join("|", Code, Path, Message);
    }

    public sealed class FirstSessionTerrainLoadResult
    {
        internal FirstSessionTerrainLoadResult(
            FirstSessionTerrainLoadStatus status,
            FirstSessionTerrainProfile profile,
            IList<FirstSessionTerrainDiagnostic> diagnostics)
        {
            Status = status;
            Profile = profile;
            Diagnostics = Array.AsReadOnly(
                (diagnostics ?? Array.Empty<FirstSessionTerrainDiagnostic>()).ToArray());
        }

        public FirstSessionTerrainLoadStatus Status { get; }
        public FirstSessionTerrainProfile Profile { get; }
        public IReadOnlyList<FirstSessionTerrainDiagnostic> Diagnostics { get; }
        public bool IsAccepted =>
            Status == FirstSessionTerrainLoadStatus.Accepted && Profile != null;
    }

    public sealed class FirstSessionTerrainPlacement
    {
        internal FirstSessionTerrainPlacement(
            string anchor,
            string verticalDatum,
            float landmarkFrontOffsetMeters)
        {
            Anchor = anchor;
            VerticalDatum = verticalDatum;
            LandmarkFrontOffsetMeters = landmarkFrontOffsetMeters;
        }

        public string Anchor { get; }
        public string VerticalDatum { get; }
        public float LandmarkFrontOffsetMeters { get; }
    }

    public sealed class FirstSessionTerrainDimensions
    {
        internal FirstSessionTerrainDimensions(
            float sizeXMeters,
            float heightMeters,
            float sizeZMeters,
            int heightmapResolution,
            int alphamapResolution,
            int baseMapResolution)
        {
            SizeXMeters = sizeXMeters;
            HeightMeters = heightMeters;
            SizeZMeters = sizeZMeters;
            HeightmapResolution = heightmapResolution;
            AlphamapResolution = alphamapResolution;
            BaseMapResolution = baseMapResolution;
        }

        public float SizeXMeters { get; }
        public float HeightMeters { get; }
        public float SizeZMeters { get; }
        public int HeightmapResolution { get; }
        public int AlphamapResolution { get; }
        public int BaseMapResolution { get; }
    }

    public sealed class FirstSessionTerrainGeneration
    {
        internal FirstSessionTerrainGeneration(
            string algorithm,
            int seed,
            float baseHeightMeters,
            float safeCourtyardRadiusMeters,
            float rimRiseMeters,
            float noiseAmplitudeMeters,
            float noiseCycles)
        {
            Algorithm = algorithm;
            Seed = seed;
            BaseHeightMeters = baseHeightMeters;
            SafeCourtyardRadiusMeters = safeCourtyardRadiusMeters;
            RimRiseMeters = rimRiseMeters;
            NoiseAmplitudeMeters = noiseAmplitudeMeters;
            NoiseCycles = noiseCycles;
        }

        public string Algorithm { get; }
        public int Seed { get; }
        public float BaseHeightMeters { get; }
        public float SafeCourtyardRadiusMeters { get; }
        public float RimRiseMeters { get; }
        public float NoiseAmplitudeMeters { get; }
        public float NoiseCycles { get; }
    }

    public sealed class FirstSessionTerrainSurface
    {
        internal FirstSessionTerrainSurface(
            string materialRole,
            int textureResolution,
            int checkerCellsPerTile,
            int gridLinePixels,
            float tileSizeMeters,
            string baseColor,
            string alternateColor,
            string gridColor,
            float normalScale,
            float metallic,
            float smoothness,
            float heightmapPixelError,
            float baseMapDistanceMeters)
        {
            MaterialRole = materialRole;
            TextureResolution = textureResolution;
            CheckerCellsPerTile = checkerCellsPerTile;
            GridLinePixels = gridLinePixels;
            TileSizeMeters = tileSizeMeters;
            BaseColor = baseColor;
            AlternateColor = alternateColor;
            GridColor = gridColor;
            NormalScale = normalScale;
            Metallic = metallic;
            Smoothness = smoothness;
            HeightmapPixelError = heightmapPixelError;
            BaseMapDistanceMeters = baseMapDistanceMeters;
        }

        public string MaterialRole { get; }
        public int TextureResolution { get; }
        public int CheckerCellsPerTile { get; }
        public int GridLinePixels { get; }
        public float TileSizeMeters { get; }
        public string BaseColor { get; }
        public string AlternateColor { get; }
        public string GridColor { get; }
        public float NormalScale { get; }
        public float Metallic { get; }
        public float Smoothness { get; }
        public float HeightmapPixelError { get; }
        public float BaseMapDistanceMeters { get; }
    }

    public sealed class FirstSessionTerrainCollision
    {
        internal FirstSessionTerrainCollision(
            float boundaryInsetMeters,
            float boundaryThicknessMeters,
            float boundaryHeightMeters,
            float landmarkProxyThicknessMeters,
            float landmarkEntranceWidthMeters,
            float landmarkHeightFraction,
            float landmarkMinimumHeightMeters,
            float landmarkMaximumHeightMeters)
        {
            BoundaryInsetMeters = boundaryInsetMeters;
            BoundaryThicknessMeters = boundaryThicknessMeters;
            BoundaryHeightMeters = boundaryHeightMeters;
            LandmarkProxyThicknessMeters = landmarkProxyThicknessMeters;
            LandmarkEntranceWidthMeters = landmarkEntranceWidthMeters;
            LandmarkHeightFraction = landmarkHeightFraction;
            LandmarkMinimumHeightMeters = landmarkMinimumHeightMeters;
            LandmarkMaximumHeightMeters = landmarkMaximumHeightMeters;
        }

        public float BoundaryInsetMeters { get; }
        public float BoundaryThicknessMeters { get; }
        public float BoundaryHeightMeters { get; }
        public float LandmarkProxyThicknessMeters { get; }
        public float LandmarkEntranceWidthMeters { get; }
        public float LandmarkHeightFraction { get; }
        public float LandmarkMinimumHeightMeters { get; }
        public float LandmarkMaximumHeightMeters { get; }
    }

    public sealed class FirstSessionTerrainNavigation
    {
        internal FirstSessionTerrainNavigation(
            string walkableSurfaceName,
            string collisionCollectionName,
            string exclusionPrefix,
            string linkSocketPrefix,
            float traversalProbeRadiusMeters)
        {
            WalkableSurfaceName = walkableSurfaceName;
            CollisionCollectionName = collisionCollectionName;
            ExclusionPrefix = exclusionPrefix;
            LinkSocketPrefix = linkSocketPrefix;
            TraversalProbeRadiusMeters = traversalProbeRadiusMeters;
        }

        public string WalkableSurfaceName { get; }
        public string CollisionCollectionName { get; }
        public string ExclusionPrefix { get; }
        public string LinkSocketPrefix { get; }
        public float TraversalProbeRadiusMeters { get; }
    }

    public sealed class FirstSessionTerrainProfile
    {
        internal FirstSessionTerrainProfile(
            string id,
            string sourceMode,
            string replacementSocketId,
            string futureBakeContract,
            FirstSessionTerrainPlacement placement,
            FirstSessionTerrainDimensions dimensions,
            FirstSessionTerrainGeneration generation,
            FirstSessionTerrainSurface surface,
            FirstSessionTerrainCollision collision,
            FirstSessionTerrainNavigation navigation)
        {
            Id = id;
            SourceMode = sourceMode;
            ReplacementSocketId = replacementSocketId;
            FutureBakeContract = futureBakeContract;
            Placement = placement;
            Dimensions = dimensions;
            Generation = generation;
            Surface = surface;
            Collision = collision;
            Navigation = navigation;
        }

        public string Id { get; }
        public string SourceMode { get; }
        public string ReplacementSocketId { get; }
        public string FutureBakeContract { get; }
        public FirstSessionTerrainPlacement Placement { get; }
        public FirstSessionTerrainDimensions Dimensions { get; }
        public FirstSessionTerrainGeneration Generation { get; }
        public FirstSessionTerrainSurface Surface { get; }
        public FirstSessionTerrainCollision Collision { get; }
        public FirstSessionTerrainNavigation Navigation { get; }
    }

    public static class FirstSessionTerrainCatalogLoader
    {
        public static FirstSessionTerrainLoadResult Validate(byte[] bytes)
        {
            var diagnostics = new List<FirstSessionTerrainDiagnostic>();
            StrictJsonObject root;
            try
            {
                root = StrictJsonDocument.Parse(
                    bytes,
                    FirstSessionTerrainContract.MaximumBytes) as StrictJsonObject;
            }
            catch (StrictJsonException error)
            {
                return Reject(
                    FirstSessionTerrainLoadStatus.Rejected,
                    diagnostics,
                    "AL-TERRAIN-SCHEMA-INVALID",
                    error.Path,
                    error.Code);
            }
            catch (Exception)
            {
                return Reject(
                    FirstSessionTerrainLoadStatus.Rejected,
                    diagnostics,
                    "AL-TERRAIN-SCHEMA-INVALID",
                    "$",
                    "parse_failed");
            }

            if (root == null)
            {
                return Reject(
                    FirstSessionTerrainLoadStatus.Rejected,
                    diagnostics,
                    "AL-TERRAIN-SCHEMA-INVALID",
                    "$",
                    "root_not_object");
            }

            Allowed(root, "$", new[]
            {
                "version", "catalogId", "authorityStatus", "profile"
            }, diagnostics);
            string version = RequiredString(root, "version", "$", diagnostics);
            if (!string.Equals(
                    version,
                    FirstSessionTerrainContract.SupportedVersion,
                    StringComparison.Ordinal))
            {
                return Reject(
                    FirstSessionTerrainLoadStatus.UnsupportedVersion,
                    diagnostics,
                    "AL-TERRAIN-VERSION-UNSUPPORTED",
                    "$.version",
                    version);
            }

            RequireEqual(
                RequiredString(root, "catalogId", "$", diagnostics),
                FirstSessionTerrainContract.CatalogId,
                "$.catalogId",
                diagnostics);
            RequireEqual(
                RequiredString(root, "authorityStatus", "$", diagnostics),
                FirstSessionTerrainContract.AuthorityStatus,
                "$.authorityStatus",
                diagnostics);

            StrictJsonObject profileValue = RequiredObject(root, "profile", "$", diagnostics);
            FirstSessionTerrainProfile profile = ParseProfile(profileValue, diagnostics);
            ValidateProfile(profile, diagnostics);
            diagnostics.Sort((left, right) =>
                string.CompareOrdinal(left.Fingerprint, right.Fingerprint));
            if (diagnostics.Count != 0)
            {
                return new FirstSessionTerrainLoadResult(
                    FirstSessionTerrainLoadStatus.Rejected,
                    null,
                    diagnostics.Take(FirstSessionTerrainContract.MaximumDiagnostics).ToArray());
            }

            return new FirstSessionTerrainLoadResult(
                FirstSessionTerrainLoadStatus.Accepted,
                profile,
                diagnostics);
        }

        private static FirstSessionTerrainProfile ParseProfile(
            StrictJsonObject value,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            const string path = "$.profile";
            if (value == null)
            {
                return null;
            }

            Allowed(value, path, new[]
            {
                "id", "sourceMode", "replacementSocketId", "futureBakeContract",
                "placement", "dimensions", "generation", "surface", "collision",
                "navigation"
            }, diagnostics);
            return new FirstSessionTerrainProfile(
                RequiredString(value, "id", path, diagnostics),
                RequiredString(value, "sourceMode", path, diagnostics),
                RequiredString(value, "replacementSocketId", path, diagnostics),
                RequiredString(value, "futureBakeContract", path, diagnostics),
                ParsePlacement(RequiredObject(value, "placement", path, diagnostics), diagnostics),
                ParseDimensions(RequiredObject(value, "dimensions", path, diagnostics), diagnostics),
                ParseGeneration(RequiredObject(value, "generation", path, diagnostics), diagnostics),
                ParseSurface(RequiredObject(value, "surface", path, diagnostics), diagnostics),
                ParseCollision(RequiredObject(value, "collision", path, diagnostics), diagnostics),
                ParseNavigation(RequiredObject(value, "navigation", path, diagnostics), diagnostics));
        }

        private static FirstSessionTerrainPlacement ParsePlacement(
            StrictJsonObject value,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            const string path = "$.profile.placement";
            if (value == null) return null;
            Allowed(value, path, new[]
            {
                "anchor", "verticalDatum", "landmarkFrontOffsetMeters"
            }, diagnostics);
            return new FirstSessionTerrainPlacement(
                RequiredString(value, "anchor", path, diagnostics),
                RequiredString(value, "verticalDatum", path, diagnostics),
                RequiredNumber(value, "landmarkFrontOffsetMeters", path, diagnostics));
        }

        private static FirstSessionTerrainDimensions ParseDimensions(
            StrictJsonObject value,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            const string path = "$.profile.dimensions";
            if (value == null) return null;
            Allowed(value, path, new[]
            {
                "sizeXMeters", "heightMeters", "sizeZMeters", "heightmapResolution",
                "alphamapResolution", "baseMapResolution"
            }, diagnostics);
            return new FirstSessionTerrainDimensions(
                RequiredNumber(value, "sizeXMeters", path, diagnostics),
                RequiredNumber(value, "heightMeters", path, diagnostics),
                RequiredNumber(value, "sizeZMeters", path, diagnostics),
                RequiredInteger(value, "heightmapResolution", path, diagnostics),
                RequiredInteger(value, "alphamapResolution", path, diagnostics),
                RequiredInteger(value, "baseMapResolution", path, diagnostics));
        }

        private static FirstSessionTerrainGeneration ParseGeneration(
            StrictJsonObject value,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            const string path = "$.profile.generation";
            if (value == null) return null;
            Allowed(value, path, new[]
            {
                "algorithm", "seed", "baseHeightMeters", "safeCourtyardRadiusMeters",
                "rimRiseMeters", "noiseAmplitudeMeters", "noiseCycles"
            }, diagnostics);
            return new FirstSessionTerrainGeneration(
                RequiredString(value, "algorithm", path, diagnostics),
                RequiredInteger(value, "seed", path, diagnostics),
                RequiredNumber(value, "baseHeightMeters", path, diagnostics),
                RequiredNumber(value, "safeCourtyardRadiusMeters", path, diagnostics),
                RequiredNumber(value, "rimRiseMeters", path, diagnostics),
                RequiredNumber(value, "noiseAmplitudeMeters", path, diagnostics),
                RequiredNumber(value, "noiseCycles", path, diagnostics));
        }

        private static FirstSessionTerrainSurface ParseSurface(
            StrictJsonObject value,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            const string path = "$.profile.surface";
            if (value == null) return null;
            Allowed(value, path, new[]
            {
                "materialRole", "textureResolution", "checkerCellsPerTile",
                "gridLinePixels", "tileSizeMeters", "baseColor", "alternateColor",
                "gridColor", "normalScale", "metallic", "smoothness",
                "heightmapPixelError", "baseMapDistanceMeters"
            }, diagnostics);
            return new FirstSessionTerrainSurface(
                RequiredString(value, "materialRole", path, diagnostics),
                RequiredInteger(value, "textureResolution", path, diagnostics),
                RequiredInteger(value, "checkerCellsPerTile", path, diagnostics),
                RequiredInteger(value, "gridLinePixels", path, diagnostics),
                RequiredNumber(value, "tileSizeMeters", path, diagnostics),
                RequiredString(value, "baseColor", path, diagnostics),
                RequiredString(value, "alternateColor", path, diagnostics),
                RequiredString(value, "gridColor", path, diagnostics),
                RequiredNumber(value, "normalScale", path, diagnostics),
                RequiredNumber(value, "metallic", path, diagnostics),
                RequiredNumber(value, "smoothness", path, diagnostics),
                RequiredNumber(value, "heightmapPixelError", path, diagnostics),
                RequiredNumber(value, "baseMapDistanceMeters", path, diagnostics));
        }

        private static FirstSessionTerrainCollision ParseCollision(
            StrictJsonObject value,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            const string path = "$.profile.collision";
            if (value == null) return null;
            Allowed(value, path, new[]
            {
                "boundaryInsetMeters", "boundaryThicknessMeters", "boundaryHeightMeters",
                "landmarkProxyThicknessMeters", "landmarkEntranceWidthMeters",
                "landmarkHeightFraction", "landmarkMinimumHeightMeters",
                "landmarkMaximumHeightMeters"
            }, diagnostics);
            return new FirstSessionTerrainCollision(
                RequiredNumber(value, "boundaryInsetMeters", path, diagnostics),
                RequiredNumber(value, "boundaryThicknessMeters", path, diagnostics),
                RequiredNumber(value, "boundaryHeightMeters", path, diagnostics),
                RequiredNumber(value, "landmarkProxyThicknessMeters", path, diagnostics),
                RequiredNumber(value, "landmarkEntranceWidthMeters", path, diagnostics),
                RequiredNumber(value, "landmarkHeightFraction", path, diagnostics),
                RequiredNumber(value, "landmarkMinimumHeightMeters", path, diagnostics),
                RequiredNumber(value, "landmarkMaximumHeightMeters", path, diagnostics));
        }

        private static FirstSessionTerrainNavigation ParseNavigation(
            StrictJsonObject value,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            const string path = "$.profile.navigation";
            if (value == null) return null;
            Allowed(value, path, new[]
            {
                "walkableSurfaceName", "collisionCollectionName", "exclusionPrefix",
                "linkSocketPrefix", "traversalProbeRadiusMeters"
            }, diagnostics);
            return new FirstSessionTerrainNavigation(
                RequiredString(value, "walkableSurfaceName", path, diagnostics),
                RequiredString(value, "collisionCollectionName", path, diagnostics),
                RequiredString(value, "exclusionPrefix", path, diagnostics),
                RequiredString(value, "linkSocketPrefix", path, diagnostics),
                RequiredNumber(value, "traversalProbeRadiusMeters", path, diagnostics));
        }

        private static void ValidateProfile(
            FirstSessionTerrainProfile profile,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (profile == null || profile.Placement == null || profile.Dimensions == null ||
                profile.Generation == null || profile.Surface == null ||
                profile.Collision == null || profile.Navigation == null)
            {
                Add(diagnostics, "AL-TERRAIN-PROFILE-INCOMPLETE", "$.profile",
                    "all terrain profile sections are required");
                return;
            }

            RequireStableId(profile.Id, "$.profile.id", diagnostics);
            RequireStableId(profile.ReplacementSocketId,
                "$.profile.replacementSocketId", diagnostics);
            RequireEqual(profile.SourceMode, "runtime_procedural_mvp",
                "$.profile.sourceMode", diagnostics);
            RequireEqual(profile.FutureBakeContract,
                "terrain_data_height_slope_biome_splat_v1",
                "$.profile.futureBakeContract", diagnostics);
            RequireEqual(profile.Placement.Anchor, "capital_position_centered",
                "$.profile.placement.anchor", diagnostics);
            RequireEqual(profile.Placement.VerticalDatum, "capital_ground_plane",
                "$.profile.placement.verticalDatum", diagnostics);
            Range(profile.Placement.LandmarkFrontOffsetMeters, 8f, 32f,
                "$.profile.placement.landmarkFrontOffsetMeters", diagnostics);

            FirstSessionTerrainDimensions dimensions = profile.Dimensions;
            Range(dimensions.SizeXMeters, 64f, 512f,
                "$.profile.dimensions.sizeXMeters", diagnostics);
            Range(dimensions.HeightMeters, 4f, 128f,
                "$.profile.dimensions.heightMeters", diagnostics);
            Range(dimensions.SizeZMeters, 64f, 512f,
                "$.profile.dimensions.sizeZMeters", diagnostics);
            RequirePowerOfTwoPlusOne(dimensions.HeightmapResolution,
                "$.profile.dimensions.heightmapResolution", diagnostics);
            RequirePowerOfTwo(dimensions.AlphamapResolution, 16, 128,
                "$.profile.dimensions.alphamapResolution", diagnostics);
            RequirePowerOfTwo(dimensions.BaseMapResolution, 16, 256,
                "$.profile.dimensions.baseMapResolution", diagnostics);

            FirstSessionTerrainGeneration generation = profile.Generation;
            RequireEqual(generation.Algorithm, "safe_courtyard_rim_v1",
                "$.profile.generation.algorithm", diagnostics);
            Range(generation.Seed, 0, int.MaxValue,
                "$.profile.generation.seed", diagnostics);
            Range(generation.BaseHeightMeters, 0.25f, 32f,
                "$.profile.generation.baseHeightMeters", diagnostics);
            Range(generation.SafeCourtyardRadiusMeters, 16f, 96f,
                "$.profile.generation.safeCourtyardRadiusMeters", diagnostics);
            Range(generation.RimRiseMeters, 0f, 16f,
                "$.profile.generation.rimRiseMeters", diagnostics);
            Range(generation.NoiseAmplitudeMeters, 0f, 4f,
                "$.profile.generation.noiseAmplitudeMeters", diagnostics);
            Range(generation.NoiseCycles, 0f, 12f,
                "$.profile.generation.noiseCycles", diagnostics);
            if (generation.BaseHeightMeters + generation.RimRiseMeters +
                generation.NoiseAmplitudeMeters >= dimensions.HeightMeters)
            {
                Add(diagnostics, "AL-TERRAIN-HEIGHT-RANGE-INVALID",
                    "$.profile.generation",
                    "generated height must stay below terrain height range");
            }

            float minimumHalfExtent = Math.Min(
                dimensions.SizeXMeters,
                dimensions.SizeZMeters) * 0.5f;
            if (generation.SafeCourtyardRadiusMeters >= minimumHalfExtent - 4f)
            {
                Add(diagnostics, "AL-TERRAIN-SAFE-RADIUS-INVALID",
                    "$.profile.generation.safeCourtyardRadiusMeters",
                    "safe courtyard must leave at least four meters for its outer rim");
            }

            FirstSessionTerrainSurface surface = profile.Surface;
            RequireEqual(surface.MaterialRole, "procedural_debug_grid",
                "$.profile.surface.materialRole", diagnostics);
            RequirePowerOfTwo(surface.TextureResolution, 8, 128,
                "$.profile.surface.textureResolution", diagnostics);
            Range(surface.CheckerCellsPerTile, 2, 16,
                "$.profile.surface.checkerCellsPerTile", diagnostics);
            Range(surface.GridLinePixels, 1, 4,
                "$.profile.surface.gridLinePixels", diagnostics);
            if (surface.TextureResolution % Math.Max(1, surface.CheckerCellsPerTile) != 0 ||
                surface.GridLinePixels >=
                surface.TextureResolution / Math.Max(1, surface.CheckerCellsPerTile))
            {
                Add(diagnostics, "AL-TERRAIN-GRID-INVALID", "$.profile.surface",
                    "checker cells must divide the texture and retain visible cell interiors");
            }
            Range(surface.TileSizeMeters, 1f, 32f,
                "$.profile.surface.tileSizeMeters", diagnostics);
            RequireColor(surface.BaseColor, "$.profile.surface.baseColor", diagnostics);
            RequireColor(surface.AlternateColor,
                "$.profile.surface.alternateColor", diagnostics);
            RequireColor(surface.GridColor, "$.profile.surface.gridColor", diagnostics);
            Range(surface.NormalScale, 0f, 2f,
                "$.profile.surface.normalScale", diagnostics);
            Range(surface.Metallic, 0f, 1f,
                "$.profile.surface.metallic", diagnostics);
            Range(surface.Smoothness, 0f, 1f,
                "$.profile.surface.smoothness", diagnostics);
            Range(surface.HeightmapPixelError, 1f, 64f,
                "$.profile.surface.heightmapPixelError", diagnostics);
            Range(surface.BaseMapDistanceMeters, 16f, 2048f,
                "$.profile.surface.baseMapDistanceMeters", diagnostics);

            FirstSessionTerrainCollision collision = profile.Collision;
            Range(collision.BoundaryInsetMeters, 0f, 8f,
                "$.profile.collision.boundaryInsetMeters", diagnostics);
            Range(collision.BoundaryThicknessMeters, 0.01f, 8f,
                "$.profile.collision.boundaryThicknessMeters", diagnostics);
            Range(collision.BoundaryHeightMeters, 2f, 24f,
                "$.profile.collision.boundaryHeightMeters", diagnostics);
            Range(collision.LandmarkProxyThicknessMeters, 0.01f, 4f,
                "$.profile.collision.landmarkProxyThicknessMeters", diagnostics);
            Range(collision.LandmarkEntranceWidthMeters, 0f, 16f,
                "$.profile.collision.landmarkEntranceWidthMeters", diagnostics);
            Range(collision.LandmarkHeightFraction, 0.01f, 1f,
                "$.profile.collision.landmarkHeightFraction", diagnostics);
            Range(collision.LandmarkMinimumHeightMeters, 1f, 12f,
                "$.profile.collision.landmarkMinimumHeightMeters", diagnostics);
            Range(collision.LandmarkMaximumHeightMeters, 2f, 24f,
                "$.profile.collision.landmarkMaximumHeightMeters", diagnostics);
            if (collision.LandmarkMaximumHeightMeters <
                collision.LandmarkMinimumHeightMeters)
            {
                Add(diagnostics, "AL-TERRAIN-LANDMARK-HEIGHT-INVALID",
                    "$.profile.collision",
                    "landmark maximum proxy height must not be below its minimum");
            }

            FirstSessionTerrainNavigation navigation = profile.Navigation;
            RequireEqual(navigation.WalkableSurfaceName,
                "NAV_FirstSessionCapitalTerrain",
                "$.profile.navigation.walkableSurfaceName", diagnostics);
            RequireEqual(navigation.CollisionCollectionName, "AL_COLLISION",
                "$.profile.navigation.collisionCollectionName", diagnostics);
            RequireEqual(navigation.ExclusionPrefix, "NAVEX_",
                "$.profile.navigation.exclusionPrefix", diagnostics);
            RequireEqual(navigation.LinkSocketPrefix, "SOCKET_NAVLINK_",
                "$.profile.navigation.linkSocketPrefix", diagnostics);
            Range(navigation.TraversalProbeRadiusMeters, 8f, 64f,
                "$.profile.navigation.traversalProbeRadiusMeters", diagnostics);
            if (navigation.TraversalProbeRadiusMeters >
                generation.SafeCourtyardRadiusMeters - 2f)
            {
                Add(diagnostics, "AL-TERRAIN-TRAVERSAL-PROBE-INVALID",
                    "$.profile.navigation.traversalProbeRadiusMeters",
                    "traversal probe must remain inside the flat safe courtyard");
            }
        }

        private static void Allowed(
            StrictJsonObject value,
            string path,
            IEnumerable<string> allowedNames,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (StrictJsonProperty property in value.Properties)
            {
                if (!allowed.Contains(property.Name))
                {
                    Add(diagnostics, "AL-TERRAIN-PROPERTY-UNKNOWN",
                        path + "." + property.Name, "unknown property");
                }
            }
        }

        private static StrictJsonObject RequiredObject(
            StrictJsonObject parent,
            string name,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonObject objectValue)
            {
                return objectValue;
            }

            Add(diagnostics, "AL-TERRAIN-OBJECT-REQUIRED", path + "." + name,
                "object is required");
            return null;
        }

        private static string RequiredString(
            StrictJsonObject parent,
            string name,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonString stringValue &&
                !string.IsNullOrWhiteSpace(stringValue.Value))
            {
                return stringValue.Value;
            }

            Add(diagnostics, "AL-TERRAIN-STRING-REQUIRED", path + "." + name,
                "non-empty string is required");
            return string.Empty;
        }

        private static float RequiredNumber(
            StrictJsonObject parent,
            string name,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonNumber number && number.HasFiniteDoubleValue &&
                number.Value >= -float.MaxValue && number.Value <= float.MaxValue)
            {
                return (float)number.Value;
            }

            Add(diagnostics, "AL-TERRAIN-NUMBER-REQUIRED", path + "." + name,
                "finite number is required");
            return 0f;
        }

        private static int RequiredInteger(
            StrictJsonObject parent,
            string name,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonNumber number && number.HasFiniteDoubleValue &&
                number.Value >= int.MinValue && number.Value <= int.MaxValue &&
                Math.Abs(number.Value - Math.Round(number.Value)) < 0.0001d)
            {
                return (int)number.Value;
            }

            Add(diagnostics, "AL-TERRAIN-INTEGER-REQUIRED", path + "." + name,
                "integer is required");
            return 0;
        }

        private static void RequireEqual(
            string actual,
            string expected,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                Add(diagnostics, "AL-TERRAIN-VALUE-INVALID", path,
                    "expected " + expected);
            }
        }

        private static void RequireStableId(
            string value,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z' ||
                value.Any(character =>
                    (character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') && character != '_'))
            {
                Add(diagnostics, "AL-TERRAIN-ID-INVALID", path,
                    "lowercase snake-case identifier is required");
            }
        }

        private static void RequireColor(
            string value,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            bool valid = value != null && value.Length == 7 && value[0] == '#';
            for (int index = 1; valid && index < value.Length; index++)
            {
                char character = value[index];
                valid = (character >= '0' && character <= '9') ||
                        (character >= 'A' && character <= 'F');
            }
            if (!valid)
            {
                Add(diagnostics, "AL-TERRAIN-COLOR-INVALID", path,
                    "uppercase #RRGGBB color is required");
            }
        }

        private static void RequirePowerOfTwoPlusOne(
            int value,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            int power = value - 1;
            if (value < 33 || value > 513 || power <= 0 ||
                (power & (power - 1)) != 0)
            {
                Add(diagnostics, "AL-TERRAIN-RESOLUTION-INVALID", path,
                    "heightmap resolution must be a supported power-of-two plus one");
            }
        }

        private static void RequirePowerOfTwo(
            int value,
            int minimum,
            int maximum,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (value < minimum || value > maximum || value <= 0 ||
                (value & (value - 1)) != 0)
            {
                Add(diagnostics, "AL-TERRAIN-RESOLUTION-INVALID", path,
                    "supported power-of-two resolution is required");
            }
        }

        private static void Range(
            float value,
            float minimum,
            float maximum,
            string path,
            List<FirstSessionTerrainDiagnostic> diagnostics)
        {
            if (value < minimum || value > maximum)
            {
                Add(diagnostics, "AL-TERRAIN-RANGE-INVALID", path,
                    "value is outside the supported range");
            }
        }

        private static FirstSessionTerrainLoadResult Reject(
            FirstSessionTerrainLoadStatus status,
            List<FirstSessionTerrainDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            Add(diagnostics, code, path, message);
            return new FirstSessionTerrainLoadResult(status, null, diagnostics);
        }

        private static void Add(
            List<FirstSessionTerrainDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            if (diagnostics.Count < FirstSessionTerrainContract.MaximumDiagnostics)
            {
                diagnostics.Add(new FirstSessionTerrainDiagnostic(code, path, message));
            }
        }
    }
}
