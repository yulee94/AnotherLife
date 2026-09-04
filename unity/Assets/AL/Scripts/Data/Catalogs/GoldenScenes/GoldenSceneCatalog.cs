using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using AL.Data.Catalogs;

namespace AL.Benchmarks.GoldenScenes
{
    public static class GoldenSceneCatalogContract
    {
        public const string FileName = "al_golden_scene_catalog.json";
        public const string SupportedVersion = "1.0.0";
        public const string CatalogId = "al_golden_scene_catalog";
        public const string AuthorityStatus = "post_mvp_benchmark_configuration";
        public const string LayoutFingerprint =
            "25804b632d5ffbab372cf33b6435ca0fe5b1ac4705865115270caed42e100ef1";
        public const int MaximumBytes = 256 * 1024;
        public const int MaximumDiagnostics = 128;
    }

    public enum GoldenSceneCatalogLoadStatus
    {
        Accepted,
        Rejected,
        UnsupportedVersion
    }

    public sealed class GoldenSceneCatalogDiagnostic
    {
        internal GoldenSceneCatalogDiagnostic(string code, string path, string message)
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

    public sealed class GoldenSceneCatalogLoadResult
    {
        internal GoldenSceneCatalogLoadResult(
            GoldenSceneCatalogLoadStatus status,
            GoldenSceneCatalog catalog,
            string catalogFingerprint,
            IList<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            Status = status;
            Catalog = catalog;
            CatalogFingerprint = catalogFingerprint ?? string.Empty;
            Diagnostics = Array.AsReadOnly(
                (diagnostics ?? Array.Empty<GoldenSceneCatalogDiagnostic>()).ToArray());
        }

        public GoldenSceneCatalogLoadStatus Status { get; }
        public GoldenSceneCatalog Catalog { get; }
        public string CatalogFingerprint { get; }
        public IReadOnlyList<GoldenSceneCatalogDiagnostic> Diagnostics { get; }
        public bool IsAccepted =>
            Status == GoldenSceneCatalogLoadStatus.Accepted && Catalog != null;
    }

    public readonly struct GoldenSceneVector3 : IEquatable<GoldenSceneVector3>
    {
        public GoldenSceneVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public bool Equals(GoldenSceneVector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object value)
        {
            return value is GoldenSceneVector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                return (hash * 397) ^ Z.GetHashCode();
            }
        }
    }

    public sealed class GoldenSceneCameraAnchor
    {
        internal GoldenSceneCameraAnchor(
            string id,
            GoldenSceneVector3 position,
            GoldenSceneVector3 eulerAngles,
            string projection,
            float fieldOfViewDegrees,
            float orthographicSize,
            float nearClipMeters,
            float farClipMeters)
        {
            Id = id;
            Position = position;
            EulerAngles = eulerAngles;
            Projection = projection;
            FieldOfViewDegrees = fieldOfViewDegrees;
            OrthographicSize = orthographicSize;
            NearClipMeters = nearClipMeters;
            FarClipMeters = farClipMeters;
        }

        public string Id { get; }
        public GoldenSceneVector3 Position { get; }
        public GoldenSceneVector3 EulerAngles { get; }
        public string Projection { get; }
        public float FieldOfViewDegrees { get; }
        public float OrthographicSize { get; }
        public float NearClipMeters { get; }
        public float FarClipMeters { get; }
        public bool IsOrthographic =>
            string.Equals(Projection, "orthographic", StringComparison.Ordinal);
    }

    public sealed class GoldenSceneQualityPreset
    {
        internal GoldenSceneQualityPreset(
            string id,
            string revision,
            int targetFrameRate,
            float renderScale,
            float shadowDistanceMeters,
            float lodBias,
            int textureMipmapLimit,
            int pixelLightCount,
            float vfxDensity)
        {
            Id = id;
            Revision = revision;
            TargetFrameRate = targetFrameRate;
            RenderScale = renderScale;
            ShadowDistanceMeters = shadowDistanceMeters;
            LodBias = lodBias;
            TextureMipmapLimit = textureMipmapLimit;
            PixelLightCount = pixelLightCount;
            VfxDensity = vfxDensity;
        }

        public string Id { get; }
        public string Revision { get; }
        public int TargetFrameRate { get; }
        public float RenderScale { get; }
        public float ShadowDistanceMeters { get; }
        public float LodBias { get; }
        public int TextureMipmapLimit { get; }
        public int PixelLightCount { get; }
        public float VfxDensity { get; }
    }

    public sealed class GoldenSceneDefinition
    {
        private readonly IReadOnlyDictionary<string, GoldenSceneCameraAnchor> anchorsById;

        internal GoldenSceneDefinition(
            string id,
            string revision,
            string scenarioId,
            string unitySceneId,
            string unitySceneName,
            int seed,
            string defaultAnchorId,
            IList<string> qualityPresetIds,
            IList<GoldenSceneCameraAnchor> anchors)
        {
            Id = id;
            Revision = revision;
            ScenarioId = scenarioId;
            UnitySceneId = unitySceneId;
            UnitySceneName = unitySceneName;
            Seed = seed;
            DefaultAnchorId = defaultAnchorId;
            QualityPresetIds = Array.AsReadOnly((qualityPresetIds ?? Array.Empty<string>()).ToArray());
            Anchors = Array.AsReadOnly((anchors ?? Array.Empty<GoldenSceneCameraAnchor>()).ToArray());
            anchorsById = new ReadOnlyDictionary<string, GoldenSceneCameraAnchor>(
                Anchors.GroupBy(anchor => anchor.Id, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
        }

        public string Id { get; }
        public string Revision { get; }
        public string ScenarioId { get; }
        public string UnitySceneId { get; }
        public string UnitySceneName { get; }
        public int Seed { get; }
        public string DefaultAnchorId { get; }
        public IReadOnlyList<string> QualityPresetIds { get; }
        public IReadOnlyList<GoldenSceneCameraAnchor> Anchors { get; }

        public bool TryGetAnchor(string id, out GoldenSceneCameraAnchor anchor)
        {
            return anchorsById.TryGetValue(id ?? string.Empty, out anchor);
        }
    }

    public sealed class GoldenSceneCatalog
    {
        private readonly IReadOnlyDictionary<string, GoldenSceneDefinition> scenesById;
        private readonly IReadOnlyDictionary<string, GoldenSceneQualityPreset> presetsById;

        internal GoldenSceneCatalog(
            string version,
            IList<GoldenSceneQualityPreset> qualityPresets,
            IList<GoldenSceneDefinition> scenes)
        {
            Version = version;
            QualityPresets = Array.AsReadOnly(
                (qualityPresets ?? Array.Empty<GoldenSceneQualityPreset>()).ToArray());
            Scenes = Array.AsReadOnly((scenes ?? Array.Empty<GoldenSceneDefinition>()).ToArray());
            scenesById = new ReadOnlyDictionary<string, GoldenSceneDefinition>(
                Scenes.GroupBy(scene => scene.Id, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
            presetsById = new ReadOnlyDictionary<string, GoldenSceneQualityPreset>(
                QualityPresets.GroupBy(preset => preset.Id, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
        }

        public string Version { get; }
        public IReadOnlyList<GoldenSceneQualityPreset> QualityPresets { get; }
        public IReadOnlyList<GoldenSceneDefinition> Scenes { get; }

        public bool TryGetScene(string id, out GoldenSceneDefinition scene)
        {
            return scenesById.TryGetValue(id ?? string.Empty, out scene);
        }

        public bool TryGetQualityPreset(string id, out GoldenSceneQualityPreset preset)
        {
            return presetsById.TryGetValue(id ?? string.Empty, out preset);
        }
    }

    public sealed class GoldenSceneSetup
    {
        internal GoldenSceneSetup(
            GoldenSceneDefinition scene,
            GoldenSceneCameraAnchor anchor,
            GoldenSceneQualityPreset qualityPreset,
            int seed,
            string configurationFingerprint)
        {
            Scene = scene;
            Anchor = anchor;
            QualityPreset = qualityPreset;
            Seed = seed;
            ConfigurationFingerprint = configurationFingerprint;
        }

        public GoldenSceneDefinition Scene { get; }
        public GoldenSceneCameraAnchor Anchor { get; }
        public GoldenSceneQualityPreset QualityPreset { get; }
        public int Seed { get; }
        public string ConfigurationFingerprint { get; }
        public string SceneId => Scene.Id;
        public string UnitySceneName => Scene.UnitySceneName;
        public string SceneRevision => Scene.Revision;
    }

    public static class GoldenSceneConfigurationResolver
    {
        public static bool TryResolve(
            GoldenSceneCatalog catalog,
            string sceneId,
            string anchorId,
            string qualityPresetId,
            int? seedOverride,
            out GoldenSceneSetup setup,
            out string diagnosticCode)
        {
            setup = null;
            if (catalog == null)
            {
                diagnosticCode = "AL-GS-CATALOG-MISSING";
                return false;
            }

            if (!catalog.TryGetScene(sceneId, out GoldenSceneDefinition scene))
            {
                diagnosticCode = "AL-GS-SCENE-MISSING:" + (sceneId ?? string.Empty);
                return false;
            }

            if (!scene.TryGetAnchor(anchorId, out GoldenSceneCameraAnchor anchor))
            {
                diagnosticCode = "AL-GS-ANCHOR-MISSING:" + scene.Id + ":" +
                    (anchorId ?? string.Empty);
                return false;
            }

            if (!scene.QualityPresetIds.Contains(qualityPresetId, StringComparer.Ordinal) ||
                !catalog.TryGetQualityPreset(qualityPresetId, out GoldenSceneQualityPreset preset))
            {
                diagnosticCode = "AL-GS-QUALITY-PRESET-MISSING:" + scene.Id + ":" +
                    (qualityPresetId ?? string.Empty);
                return false;
            }

            int seed = seedOverride ?? scene.Seed;
            if (seed < 0)
            {
                diagnosticCode = "AL-GS-SEED-INVALID:" + seed.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            string fingerprint = GoldenSceneHash.ComputeSha256(
                scene.Id,
                scene.Revision,
                scene.ScenarioId,
                scene.UnitySceneId,
                scene.UnitySceneName,
                seed.ToString(CultureInfo.InvariantCulture),
                anchor.Id,
                Float(anchor.Position.X), Float(anchor.Position.Y), Float(anchor.Position.Z),
                Float(anchor.EulerAngles.X), Float(anchor.EulerAngles.Y), Float(anchor.EulerAngles.Z),
                anchor.Projection,
                Float(anchor.FieldOfViewDegrees), Float(anchor.OrthographicSize),
                Float(anchor.NearClipMeters), Float(anchor.FarClipMeters),
                preset.Id, preset.Revision,
                preset.TargetFrameRate.ToString(CultureInfo.InvariantCulture),
                Float(preset.RenderScale), Float(preset.ShadowDistanceMeters),
                Float(preset.LodBias),
                preset.TextureMipmapLimit.ToString(CultureInfo.InvariantCulture),
                preset.PixelLightCount.ToString(CultureInfo.InvariantCulture),
                Float(preset.VfxDensity));
            setup = new GoldenSceneSetup(scene, anchor, preset, seed, fingerprint);
            diagnosticCode = "AL-GS-SETUP-READY";
            return true;
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public static class GoldenSceneCatalogLoader
    {
        private static readonly string[] ExpectedScenes =
            { "GS-01", "GS-02", "GS-03", "GS-04", "GS-05" };
        private static readonly string[] ExpectedPresets =
            { "android_floor_30", "balanced_60", "pc_high_60" };

        public static GoldenSceneCatalogLoadResult Validate(byte[] bytes)
        {
            var diagnostics = new List<GoldenSceneCatalogDiagnostic>();
            string fingerprint = bytes == null ? string.Empty : GoldenSceneHash.ComputeSha256(bytes);
            StrictJsonObject root;
            try
            {
                root = StrictJsonDocument.Parse(bytes, GoldenSceneCatalogContract.MaximumBytes)
                    as StrictJsonObject;
            }
            catch (StrictJsonException error)
            {
                return Reject(GoldenSceneCatalogLoadStatus.Rejected, fingerprint, diagnostics,
                    "AL-GS-SCHEMA-INVALID", error.Path, error.Code);
            }
            catch (Exception)
            {
                return Reject(GoldenSceneCatalogLoadStatus.Rejected, fingerprint, diagnostics,
                    "AL-GS-SCHEMA-INVALID", "$", "parse_failed");
            }

            if (root == null)
            {
                return Reject(GoldenSceneCatalogLoadStatus.Rejected, fingerprint, diagnostics,
                    "AL-GS-SCHEMA-INVALID", "$", "root_not_object");
            }

            Allowed(root, "$", new[]
            {
                "version", "catalogId", "authorityStatus", "layoutFingerprint",
                "qualityPresets", "scenes"
            }, diagnostics);
            string version = RequiredString(root, "version", "$", diagnostics);
            if (!string.Equals(version, GoldenSceneCatalogContract.SupportedVersion,
                    StringComparison.Ordinal))
            {
                return Reject(GoldenSceneCatalogLoadStatus.UnsupportedVersion, fingerprint,
                    diagnostics, "AL-GS-VERSION-UNSUPPORTED", "$.version", version);
            }

            RequireEqual(RequiredString(root, "catalogId", "$", diagnostics),
                GoldenSceneCatalogContract.CatalogId, "$.catalogId", diagnostics);
            RequireEqual(RequiredString(root, "authorityStatus", "$", diagnostics),
                GoldenSceneCatalogContract.AuthorityStatus, "$.authorityStatus", diagnostics);
            string declaredLayoutFingerprint = RequiredString(
                root, "layoutFingerprint", "$", diagnostics);
            RequireEqual(declaredLayoutFingerprint,
                GoldenSceneCatalogContract.LayoutFingerprint,
                "$.layoutFingerprint", diagnostics);

            var presets = new List<GoldenSceneQualityPreset>();
            ParseObjects(RequiredArray(root, "qualityPresets", "$", diagnostics),
                "$.qualityPresets", diagnostics,
                (value, path) => presets.Add(ParsePreset(value, path, diagnostics)));
            var scenes = new List<GoldenSceneDefinition>();
            ParseObjects(RequiredArray(root, "scenes", "$", diagnostics),
                "$.scenes", diagnostics,
                (value, path) => scenes.Add(ParseScene(value, path, diagnostics)));

            ValidatePresets(presets, diagnostics);
            ValidateScenes(scenes, presets, diagnostics);
            ValidateLayoutFingerprint(scenes, declaredLayoutFingerprint, diagnostics);
            diagnostics.Sort((left, right) =>
                string.CompareOrdinal(left.Fingerprint, right.Fingerprint));
            if (diagnostics.Count != 0)
            {
                return new GoldenSceneCatalogLoadResult(GoldenSceneCatalogLoadStatus.Rejected,
                    null, fingerprint,
                    diagnostics.Take(GoldenSceneCatalogContract.MaximumDiagnostics).ToArray());
            }

            return new GoldenSceneCatalogLoadResult(GoldenSceneCatalogLoadStatus.Accepted,
                new GoldenSceneCatalog(version, presets, scenes), fingerprint, diagnostics);
        }

        private static GoldenSceneQualityPreset ParsePreset(
            StrictJsonObject value,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            Allowed(value, path, new[]
            {
                "id", "revision", "targetFrameRate", "renderScale", "shadowDistanceMeters",
                "lodBias", "textureMipmapLimit", "pixelLightCount", "vfxDensity"
            }, diagnostics);
            return new GoldenSceneQualityPreset(
                RequiredString(value, "id", path, diagnostics),
                RequiredString(value, "revision", path, diagnostics),
                RequiredInteger(value, "targetFrameRate", path, diagnostics),
                RequiredNumber(value, "renderScale", path, diagnostics, 0.5d, 1d),
                RequiredNumber(value, "shadowDistanceMeters", path, diagnostics, 0d, 200d),
                RequiredNumber(value, "lodBias", path, diagnostics, 0.25d, 4d),
                RequiredInteger(value, "textureMipmapLimit", path, diagnostics),
                RequiredInteger(value, "pixelLightCount", path, diagnostics),
                RequiredNumber(value, "vfxDensity", path, diagnostics, 0.1d, 2d));
        }

        private static GoldenSceneDefinition ParseScene(
            StrictJsonObject value,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            Allowed(value, path, new[]
            {
                "id", "revision", "scenarioId", "unitySceneId", "unitySceneName", "seed",
                "defaultAnchorId", "qualityPresetIds", "anchors"
            }, diagnostics);
            var qualityPresetIds = new List<string>();
            ParseStrings(RequiredArray(value, "qualityPresetIds", path, diagnostics),
                path + ".qualityPresetIds", diagnostics, qualityPresetIds);
            var anchors = new List<GoldenSceneCameraAnchor>();
            ParseObjects(RequiredArray(value, "anchors", path, diagnostics),
                path + ".anchors", diagnostics,
                (anchor, anchorPath) =>
                    anchors.Add(ParseAnchor(anchor, anchorPath, diagnostics)));
            return new GoldenSceneDefinition(
                RequiredString(value, "id", path, diagnostics),
                RequiredString(value, "revision", path, diagnostics),
                RequiredString(value, "scenarioId", path, diagnostics),
                RequiredString(value, "unitySceneId", path, diagnostics),
                RequiredString(value, "unitySceneName", path, diagnostics),
                RequiredInteger(value, "seed", path, diagnostics),
                RequiredString(value, "defaultAnchorId", path, diagnostics),
                qualityPresetIds,
                anchors);
        }

        private static GoldenSceneCameraAnchor ParseAnchor(
            StrictJsonObject value,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            Allowed(value, path, new[]
            {
                "id", "position", "eulerAngles", "projection", "fieldOfViewDegrees",
                "orthographicSize", "nearClipMeters", "farClipMeters"
            }, diagnostics);
            return new GoldenSceneCameraAnchor(
                RequiredString(value, "id", path, diagnostics),
                RequiredVector3(value, "position", path, diagnostics, -100000d, 100000d),
                RequiredVector3(value, "eulerAngles", path, diagnostics, -360d, 360d),
                RequiredString(value, "projection", path, diagnostics),
                RequiredNumber(value, "fieldOfViewDegrees", path, diagnostics, 1d, 179d),
                RequiredNumber(value, "orthographicSize", path, diagnostics, 0d, 10000d, true),
                RequiredNumber(value, "nearClipMeters", path, diagnostics, 0d, 10d, true),
                RequiredNumber(value, "farClipMeters", path, diagnostics, 10d, 100000d, true));
        }

        private static void ValidatePresets(
            IList<GoldenSceneQualityPreset> presets,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            ValidateOrderedIds(presets.Select(item => item.Id).ToArray(), ExpectedPresets,
                "$.qualityPresets", "AL-GS-QUALITY-PRESETS-INCOMPLETE", diagnostics);
            for (int index = 0; index < presets.Count; index++)
            {
                GoldenSceneQualityPreset preset = presets[index];
                string path = "$.qualityPresets[" + index + "]";
                RequireRevision(preset.Revision, path + ".revision", diagnostics);
                if (preset.TargetFrameRate != 30 && preset.TargetFrameRate != 60)
                {
                    Add(diagnostics, "AL-GS-QUALITY-RANGE-INVALID",
                        path + ".targetFrameRate", "target frame rate must be 30 or 60");
                }
                Range(preset.RenderScale, 0.5f, 1f, path + ".renderScale",
                    "AL-GS-QUALITY-RANGE-INVALID", diagnostics);
                Range(preset.ShadowDistanceMeters, 0f, 200f,
                    path + ".shadowDistanceMeters", "AL-GS-QUALITY-RANGE-INVALID", diagnostics);
                Range(preset.LodBias, 0.25f, 4f, path + ".lodBias",
                    "AL-GS-QUALITY-RANGE-INVALID", diagnostics);
                Range(preset.TextureMipmapLimit, 0, 3, path + ".textureMipmapLimit",
                    "AL-GS-QUALITY-RANGE-INVALID", diagnostics);
                Range(preset.PixelLightCount, 0, 8, path + ".pixelLightCount",
                    "AL-GS-QUALITY-RANGE-INVALID", diagnostics);
                Range(preset.VfxDensity, 0.1f, 2f, path + ".vfxDensity",
                    "AL-GS-QUALITY-RANGE-INVALID", diagnostics);
            }
        }

        private static void ValidateScenes(
            IList<GoldenSceneDefinition> scenes,
            IList<GoldenSceneQualityPreset> presets,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            ValidateOrderedIds(scenes.Select(item => item.Id).ToArray(), ExpectedScenes,
                "$.scenes", "AL-GS-SCENES-INCOMPLETE", diagnostics);
            var presetIds = new HashSet<string>(presets.Select(item => item.Id),
                StringComparer.Ordinal);
            for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
            {
                GoldenSceneDefinition scene = scenes[sceneIndex];
                string path = "$.scenes[" + sceneIndex + "]";
                RequireRevision(scene.Revision, path + ".revision", diagnostics);
                RequireStableId(scene.ScenarioId, path + ".scenarioId", diagnostics);
                RequireStableId(scene.UnitySceneId, path + ".unitySceneId", diagnostics);
                if (string.IsNullOrWhiteSpace(scene.UnitySceneName) ||
                    scene.UnitySceneName.Length > 128)
                {
                    Add(diagnostics, "AL-GS-SCENE-NAME-INVALID",
                        path + ".unitySceneName", "scene name must contain 1 to 128 characters");
                }
                Range(scene.Seed, 0, int.MaxValue, path + ".seed",
                    "AL-GS-SEED-INVALID", diagnostics);
                if (!scene.QualityPresetIds.SequenceEqual(
                        ExpectedPresets, StringComparer.Ordinal) ||
                    scene.QualityPresetIds.Any(id => !presetIds.Contains(id)))
                {
                    Add(diagnostics, "AL-GS-QUALITY-PRESET-REFERENCE-INVALID",
                        path + ".qualityPresetIds",
                        "every canonical preset is required once in canonical order");
                }

                if (scene.Anchors.Count == 0 || scene.Anchors.Count > 32)
                {
                    Add(diagnostics, "AL-GS-ANCHORS-MISSING", path + ".anchors",
                        "between 1 and 32 anchors are required");
                    continue;
                }

                if (!string.Equals(scene.Anchors[0].Id, scene.DefaultAnchorId,
                        StringComparison.Ordinal))
                {
                    Add(diagnostics, "AL-GS-DEFAULT-ANCHOR-ORDER-INVALID",
                        path + ".anchors[0].id",
                        "default anchor must be the first anchor");
                }

                var anchorIds = new HashSet<string>(StringComparer.Ordinal);
                for (int anchorIndex = 0; anchorIndex < scene.Anchors.Count; anchorIndex++)
                {
                    GoldenSceneCameraAnchor anchor = scene.Anchors[anchorIndex];
                    string anchorPath = path + ".anchors[" + anchorIndex + "]";
                    RequireStableId(anchor.Id, anchorPath + ".id", diagnostics);
                    if (!anchorIds.Add(anchor.Id))
                    {
                        Add(diagnostics, "AL-GS-ANCHOR-DUPLICATE", anchorPath + ".id",
                            anchor.Id);
                    }
                    ValidateAnchor(anchor, anchorPath, diagnostics);
                }

                if (!anchorIds.Contains(scene.DefaultAnchorId))
                {
                    Add(diagnostics, "AL-GS-DEFAULT-ANCHOR-MISSING",
                        path + ".defaultAnchorId", scene.DefaultAnchorId);
                }
            }
        }

        private static void ValidateLayoutFingerprint(
            IEnumerable<GoldenSceneDefinition> scenes,
            string declaredFingerprint,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            var values = new List<string>();
            foreach (GoldenSceneDefinition scene in scenes)
            {
                values.Add(scene.Id);
                values.Add(scene.DefaultAnchorId);
                values.AddRange(scene.Anchors.Select(anchor => anchor.Id));
            }

            string computedFingerprint = GoldenSceneHash.ComputeSha256(values.ToArray());
            if (!string.Equals(declaredFingerprint, computedFingerprint, StringComparison.Ordinal))
            {
                Add(diagnostics, "AL-GS-LAYOUT-FINGERPRINT-MISMATCH",
                    "$.layoutFingerprint", computedFingerprint);
            }
        }

        private static void ValidateAnchor(
            GoldenSceneCameraAnchor anchor,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (!string.Equals(anchor.Projection, "perspective", StringComparison.Ordinal) &&
                !string.Equals(anchor.Projection, "orthographic", StringComparison.Ordinal))
            {
                Add(diagnostics, "AL-GS-ANCHOR-PROJECTION-INVALID", path + ".projection",
                    anchor.Projection);
            }
            Range(anchor.FieldOfViewDegrees, 1f, 179f, path + ".fieldOfViewDegrees",
                "AL-GS-ANCHOR-FOV-INVALID", diagnostics);
            Range(anchor.OrthographicSize, float.Epsilon, 10000f,
                path + ".orthographicSize", "AL-GS-ANCHOR-SIZE-INVALID", diagnostics);
            Range(anchor.NearClipMeters, float.Epsilon, 10f,
                path + ".nearClipMeters", "AL-GS-ANCHOR-CLIP-INVALID", diagnostics);
            Range(anchor.FarClipMeters, 10f, 100000f,
                path + ".farClipMeters", "AL-GS-ANCHOR-CLIP-INVALID", diagnostics);
            if (anchor.FarClipMeters <= anchor.NearClipMeters)
            {
                Add(diagnostics, "AL-GS-ANCHOR-CLIP-INVALID", path,
                    "far clip must be greater than near clip");
            }
            ValidateVector(anchor.Position, path + ".position", -100000f, 100000f,
                diagnostics);
            ValidateVector(anchor.EulerAngles, path + ".eulerAngles", -360f, 360f,
                diagnostics);
        }

        private static void ValidateVector(
            GoldenSceneVector3 vector,
            string path,
            float minimum,
            float maximum,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (vector.X < minimum || vector.X > maximum ||
                vector.Y < minimum || vector.Y > maximum ||
                vector.Z < minimum || vector.Z > maximum)
            {
                Add(diagnostics, "AL-GS-ANCHOR-VECTOR-INVALID", path,
                    "vector component is outside the supported range");
            }
        }

        private static void ValidateOrderedIds(
            IReadOnlyList<string> actual,
            IReadOnlyList<string> expected,
            string path,
            string code,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (actual.Count != expected.Count ||
                !actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                Add(diagnostics, code, path,
                    "required order: " + string.Join(",", expected));
            }
        }

        private static void ParseObjects(
            StrictJsonArray array,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics,
            Action<StrictJsonObject, string> add)
        {
            if (array == null) return;
            for (int index = 0; index < array.Items.Count; index++)
            {
                if (array.Items[index] is StrictJsonObject value)
                {
                    add(value, path + "[" + index + "]");
                }
                else
                {
                    Add(diagnostics, "AL-GS-OBJECT-REQUIRED", path + "[" + index + "]",
                        "object is required");
                }
            }
        }

        private static void ParseStrings(
            StrictJsonArray array,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics,
            ICollection<string> values)
        {
            if (array == null) return;
            for (int index = 0; index < array.Items.Count; index++)
            {
                if (array.Items[index] is StrictJsonString value &&
                    !string.IsNullOrWhiteSpace(value.Value))
                {
                    values.Add(value.Value);
                }
                else
                {
                    Add(diagnostics, "AL-GS-STRING-REQUIRED", path + "[" + index + "]",
                        "non-empty string is required");
                }
            }
        }

        private static GoldenSceneVector3 RequiredVector3(
            StrictJsonObject parent,
            string name,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics,
            double minimum,
            double maximum)
        {
            StrictJsonArray array = RequiredArray(parent, name, path, diagnostics);
            string vectorPath = path + "." + name;
            if (array == null || array.Items.Count != 3)
            {
                Add(diagnostics, "AL-GS-VECTOR3-REQUIRED", vectorPath,
                    "exactly three finite numbers are required");
                return default;
            }

            float[] components = new float[3];
            for (int index = 0; index < components.Length; index++)
            {
                if (array.Items[index] is StrictJsonNumber number &&
                    number.HasFiniteDoubleValue &&
                    TryParseExactNumber(number.RawValue, out ExactJsonDecimal exact) &&
                    IsWithinBounds(exact, minimum, maximum, false) &&
                    IsOnMicroUnitGrid(exact))
                {
                    components[index] = (float)number.Value;
                }
                else
                {
                    Add(diagnostics, "AL-GS-VECTOR3-REQUIRED",
                        vectorPath + "[" + index + "]", "finite number is required");
                }
            }
            return new GoldenSceneVector3(components[0], components[1], components[2]);
        }

        private static void Allowed(
            StrictJsonObject value,
            string path,
            IEnumerable<string> allowedNames,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (value == null) return;
            var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (StrictJsonProperty property in value.Properties)
            {
                if (!allowed.Contains(property.Name))
                {
                    Add(diagnostics, "AL-GS-PROPERTY-UNKNOWN", path + "." + property.Name,
                        "unknown property");
                }
            }
        }

        private static StrictJsonArray RequiredArray(
            StrictJsonObject parent,
            string name,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonArray array)
            {
                return array;
            }
            Add(diagnostics, "AL-GS-ARRAY-REQUIRED", path + "." + name,
                "array is required");
            return null;
        }

        private static string RequiredString(
            StrictJsonObject parent,
            string name,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonString text && !string.IsNullOrWhiteSpace(text.Value))
            {
                return text.Value;
            }
            Add(diagnostics, "AL-GS-STRING-REQUIRED", path + "." + name,
                "non-empty string is required");
            return string.Empty;
        }

        private static float RequiredNumber(
            StrictJsonObject parent,
            string name,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics,
            double minimum,
            double maximum,
            bool exclusiveMinimum = false)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonNumber number && number.HasFiniteDoubleValue &&
                TryParseExactNumber(number.RawValue, out ExactJsonDecimal exact) &&
                IsWithinBounds(exact, minimum, maximum, exclusiveMinimum) &&
                IsOnMicroUnitGrid(exact))
            {
                return (float)number.Value;
            }
            Add(diagnostics, "AL-GS-NUMBER-REQUIRED", path + "." + name,
                "finite number is required");
            return 0f;
        }

        private static int RequiredInteger(
            StrictJsonObject parent,
            string name,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (parent != null && parent.TryGet(name, out StrictJsonValue value) &&
                value is StrictJsonNumber number &&
                TryParseExactNumber(number.RawValue, out ExactJsonDecimal exact) &&
                TryGetInteger(exact, out int integer))
            {
                return integer;
            }
            Add(diagnostics, "AL-GS-INTEGER-REQUIRED", path + "." + name,
                "integer is required");
            return 0;
        }

        private readonly struct ExactJsonDecimal
        {
            internal ExactJsonDecimal(BigInteger significand, int scale)
            {
                Significand = significand;
                Scale = scale;
            }

            internal BigInteger Significand { get; }
            internal int Scale { get; }
        }

        private static bool TryParseExactNumber(string rawValue, out ExactJsonDecimal exact)
        {
            exact = default;
            if (string.IsNullOrEmpty(rawValue)) return false;

            int exponentIndex = rawValue.IndexOfAny(new[] { 'e', 'E' });
            string mantissa = exponentIndex < 0 ? rawValue : rawValue.Substring(0, exponentIndex);
            int exponent = 0;
            if (exponentIndex >= 0 && (!int.TryParse(
                    rawValue.Substring(exponentIndex + 1),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out exponent)))
            {
                return false;
            }

            bool negative = mantissa[0] == '-';
            int start = negative ? 1 : 0;
            int decimalIndex = mantissa.IndexOf('.');
            int fractionalDigits = decimalIndex < 0 ? 0 : mantissa.Length - decimalIndex - 1;
            string digits = decimalIndex < 0
                ? mantissa.Substring(start)
                : mantissa.Substring(start, decimalIndex - start) + mantissa.Substring(decimalIndex + 1);
            if (!BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture,
                    out BigInteger significand))
            {
                return false;
            }

            if (negative) significand = BigInteger.Negate(significand);
            if (significand.IsZero)
            {
                exact = new ExactJsonDecimal(BigInteger.Zero, 0);
                return true;
            }
            int scale = fractionalDigits - exponent;
            if (scale < -128 || scale > 128) return false;
            exact = new ExactJsonDecimal(significand, scale);
            return true;
        }

        private static bool IsWithinBounds(
            ExactJsonDecimal value,
            double minimum,
            double maximum,
            bool exclusiveMinimum)
        {
            TryParseExactNumber(minimum.ToString("R", CultureInfo.InvariantCulture),
                out ExactJsonDecimal minimumValue);
            TryParseExactNumber(maximum.ToString("R", CultureInfo.InvariantCulture),
                out ExactJsonDecimal maximumValue);
            int minimumComparison = Compare(value, minimumValue);
            return (exclusiveMinimum ? minimumComparison > 0 : minimumComparison >= 0) &&
                   Compare(value, maximumValue) <= 0;
        }

        private static int Compare(ExactJsonDecimal left, ExactJsonDecimal right)
        {
            int commonScale = Math.Max(left.Scale, right.Scale);
            BigInteger scaledLeft = left.Significand *
                BigInteger.Pow(10, commonScale - left.Scale);
            BigInteger scaledRight = right.Significand *
                BigInteger.Pow(10, commonScale - right.Scale);
            return scaledLeft.CompareTo(scaledRight);
        }

        private static bool IsOnMicroUnitGrid(ExactJsonDecimal value)
        {
            if (value.Scale <= 6) return true;
            BigInteger divisor = BigInteger.Pow(10, value.Scale - 6);
            return value.Significand % divisor == BigInteger.Zero;
        }

        private static bool TryGetInteger(ExactJsonDecimal value, out int integer)
        {
            BigInteger normalized = value.Significand;
            if (value.Scale > 0)
            {
                BigInteger divisor = BigInteger.Pow(10, value.Scale);
                if (normalized % divisor != BigInteger.Zero)
                {
                    integer = 0;
                    return false;
                }
                normalized /= divisor;
            }
            else if (value.Scale < 0)
            {
                normalized *= BigInteger.Pow(10, -value.Scale);
            }

            if (normalized < int.MinValue || normalized > int.MaxValue)
            {
                integer = 0;
                return false;
            }
            integer = (int)normalized;
            return true;
        }

        private static void RequireEqual(
            string actual,
            string expected,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                Add(diagnostics, "AL-GS-VALUE-INVALID", path, "expected " + expected);
        }

        private static void RequireStableId(
            string value,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z' ||
                value.Any(character =>
                    (character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') && character != '_'))
            {
                Add(diagnostics, "AL-GS-ID-INVALID", path,
                    "lowercase snake-case identifier is required");
            }
        }

        private static void RequireRevision(
            string value,
            string path,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(value) || value[0] < '1' || value[0] > '9' ||
                value.Any(character => character < '0' || character > '9'))
            {
                Add(diagnostics, "AL-GS-REVISION-INVALID", path,
                    "positive decimal revision is required");
            }
        }

        private static void Range(
            float value,
            float minimum,
            float maximum,
            string path,
            string code,
            List<GoldenSceneCatalogDiagnostic> diagnostics)
        {
            if (value < minimum || value > maximum)
                Add(diagnostics, code, path, "value is outside the supported range");
        }

        private static GoldenSceneCatalogLoadResult Reject(
            GoldenSceneCatalogLoadStatus status,
            string fingerprint,
            List<GoldenSceneCatalogDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            Add(diagnostics, code, path, message);
            return new GoldenSceneCatalogLoadResult(status, null, fingerprint, diagnostics);
        }

        private static void Add(
            List<GoldenSceneCatalogDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            if (diagnostics.Count < GoldenSceneCatalogContract.MaximumDiagnostics)
                diagnostics.Add(new GoldenSceneCatalogDiagnostic(code, path, message));
        }
    }

    public static class GoldenSceneHash
    {
        public static string ComputeSha256(params string[] fields)
        {
            return ComputeSha256(Encoding.UTF8.GetBytes(
                string.Join("\u001f", fields ?? Array.Empty<string>())));
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes ?? Array.Empty<byte>());
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
