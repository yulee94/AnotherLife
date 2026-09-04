using System;
using System.IO;
using UnityEngine;

namespace AL.UI
{
    [Serializable]
    public sealed class LaunchCinematicCatalogPlatformFile
    {
        public string platform;
        public string streamingAssetsPath;
        public string container;
        public string codecProfile;
        public int width;
        public int height;
        public int framesPerSecond;
        public int frameCount;
        public float durationSeconds;
        public long byteLength;
        public string sha256;
        public float prepareTimeoutSeconds;
        public int skipEligibilityFrame;
        public bool encodePresent;
    }

    [Serializable]
    public sealed class LaunchCinematicCatalogFile
    {
        public string schema;
        public int version;
        public string cinematicId;
        public string authorityStatus;
        public bool approvedForProduction;
        public bool probeEvidenceApproved;
        public bool reducedMotionFallbackOnly;
        public bool ownerVisualApprovalRequired;
        public bool runtimeAuthority;
        public bool gameplayAuthority;
        public bool finalCinematicApproval;
        public LaunchCinematicCatalogPlatformFile[] platforms;
    }

    public static class LaunchCinematicCatalog
    {
        public const string FileName = "al_launch_cinematic_runtime.v1.json";

        public static string ResolveGameDataDirectory()
        {
            if (Application.isEditor)
            {
                return Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData");
            }

            return Path.Combine(
                (Application.streamingAssetsPath ?? string.Empty).TrimEnd('/', '\\'),
                "GameData");
        }

        public static string ResolveCatalogPath()
        {
            return Path.Combine(ResolveGameDataDirectory(), FileName);
        }

        public static bool TryLoadForPlatform(
            LaunchCinematicPlatform platform,
            out LaunchCinematicRuntimeRecord record)
        {
            record = null;
            string path = ResolveCatalogPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception)
            {
                return false;
            }

            return TryParseForPlatform(json, platform, out record);
        }

        public static bool TryParseForPlatform(
            string json,
            LaunchCinematicPlatform platform,
            out LaunchCinematicRuntimeRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            LaunchCinematicCatalogFile file;
            try
            {
                file = JsonUtility.FromJson<LaunchCinematicCatalogFile>(json);
            }
            catch (Exception)
            {
                return false;
            }

            if (file == null ||
                !string.Equals(file.schema, LaunchCinematicRuntimeRecord.ExpectedSchema, StringComparison.Ordinal) ||
                file.version != LaunchCinematicRuntimeRecord.ExpectedVersion ||
                file.platforms == null)
            {
                return false;
            }

            for (int i = 0; i < file.platforms.Length; i++)
            {
                LaunchCinematicCatalogPlatformFile row = file.platforms[i];
                if (row == null || !TryParsePlatform(row.platform, out LaunchCinematicPlatform parsed) || parsed != platform)
                {
                    continue;
                }

                record = Map(file, row, platform);
                return true;
            }

            return false;
        }

        public static LaunchCinematicPlatform CurrentBuildPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return LaunchCinematicPlatform.Android;
#else
            return LaunchCinematicPlatform.Desktop;
#endif
        }

        private static bool TryParsePlatform(string value, out LaunchCinematicPlatform platform)
        {
            if (string.Equals(value, "Desktop", StringComparison.Ordinal))
            {
                platform = LaunchCinematicPlatform.Desktop;
                return true;
            }

            if (string.Equals(value, "Android", StringComparison.Ordinal))
            {
                platform = LaunchCinematicPlatform.Android;
                return true;
            }

            platform = LaunchCinematicPlatform.Desktop;
            return false;
        }

        private static LaunchCinematicRuntimeRecord Map(
            LaunchCinematicCatalogFile file,
            LaunchCinematicCatalogPlatformFile row,
            LaunchCinematicPlatform platform)
        {
            return new LaunchCinematicRuntimeRecord
            {
                Schema = file.schema,
                Version = file.version,
                CinematicId = file.cinematicId,
                Platform = platform,
                StreamingAssetsPath = row.streamingAssetsPath ?? string.Empty,
                Container = string.IsNullOrWhiteSpace(row.container) ? "mp4" : row.container,
                CodecProfile = row.codecProfile ?? string.Empty,
                Width = row.width,
                Height = row.height,
                FramesPerSecond = row.framesPerSecond,
                FrameCount = row.frameCount,
                DurationSeconds = row.durationSeconds,
                ByteLength = row.byteLength,
                Sha256 = row.sha256 ?? string.Empty,
                PrepareTimeoutSeconds = row.prepareTimeoutSeconds,
                SkipEligibilityFrame = row.skipEligibilityFrame,
                ApprovedForProduction = file.approvedForProduction,
                ProbeEvidenceApproved = file.probeEvidenceApproved,
                ReducedMotionFallbackOnly = file.reducedMotionFallbackOnly
            };
        }
    }

    public static class LaunchCinematicBootBinding
    {
        public const string UnavailableReason = "approved-media-unavailable";

        public static string EstablishStaticFallback(
            LaunchCinematicLifecycle lifecycle,
            LaunchCinematicRuntimeRecord record,
            LaunchCinematicPlatform platform,
            bool releaseBuild,
            bool reducedMotion)
        {
            if (lifecycle == null)
            {
                return UnavailableReason;
            }

            lifecycle.MarkPreparing();
            var coordinator = new LaunchCinematicPlaybackCoordinator();
            coordinator.Begin(record, platform, releaseBuild, reducedMotion);
            lifecycle.FailToFallback(UnavailableReason);
            return lifecycle.TransitionReason;
        }
    }
}
