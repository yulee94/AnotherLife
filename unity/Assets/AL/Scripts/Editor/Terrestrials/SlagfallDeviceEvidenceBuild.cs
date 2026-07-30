using System;
using System.IO;
using System.Linq;
using AL.Terrestrials.Slagfall;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Editor.Terrestrials
{
    public static class SlagfallDeviceEvidenceBuild
    {
        private const string TemporaryScenePath =
            "Assets/AL/Scenes/Prototype/Terrestrials/" +
            "SlagfallDeviceEvidence.generated.unity";

        [MenuItem(
            "Another Life/Terrestrials/Device Evidence/" +
            "Build Android Mobile Low")]
        public static void BuildAndroidMobileLow()
        {
            Build(
                BuildTarget.Android,
                SlagfallEvidenceLane.MobileLow,
                ResolveDefaultOutput(
                    BuildTarget.Android,
                    SlagfallEvidenceLane.MobileLow),
                false,
                false);
        }

        [MenuItem(
            "Another Life/Terrestrials/Device Evidence/" +
            "Build Android Mobile Standard")]
        public static void BuildAndroidMobileStandard()
        {
            Build(
                BuildTarget.Android,
                SlagfallEvidenceLane.MobileStandard,
                ResolveDefaultOutput(
                    BuildTarget.Android,
                    SlagfallEvidenceLane.MobileStandard),
                false,
                false);
        }

        [MenuItem(
            "Another Life/Terrestrials/Device Evidence/" +
            "Build Host Desktop Standard")]
        public static void BuildHostDesktopStandard()
        {
            BuildTarget target = ResolveHostDesktopTarget();
            Build(
                target,
                SlagfallEvidenceLane.DesktopStandard,
                ResolveDefaultOutput(
                    target,
                    SlagfallEvidenceLane.DesktopStandard),
                false,
                false);
        }

        public static void BuildFromCommandLine()
        {
            string[] arguments =
                Environment.GetCommandLineArgs();
            BuildTarget target = ParseTarget(
                ReadArgument(arguments, "-slagfallEvidenceTarget"));
            SlagfallEvidenceLane lane = ParseLane(
                ReadArgument(arguments, "-slagfallEvidenceLane"));
            string output =
                ReadArgument(
                    arguments,
                    "-slagfallEvidenceOutput") ??
                ResolveDefaultOutput(target, lane);
            bool effectsOff =
                HasFlag(arguments, "-slagfallEffectsOff");
            bool reducedMotion =
                HasFlag(arguments, "-slagfallReducedMotion");
            Build(
                target,
                lane,
                Path.GetFullPath(output),
                effectsOff,
                reducedMotion);
        }

        public static BuildReport Build(
            BuildTarget target,
            SlagfallEvidenceLane lane,
            string outputPath,
            bool effectsOff,
            bool reducedMotion)
        {
            ValidateLaneForTarget(target, lane);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException(
                    "A device-evidence build output path is required.",
                    nameof(outputPath));
            }

            ValidateBuildInputs();
            string absoluteOutput = Path.GetFullPath(outputPath);
            EnsureOutputParent(absoluteOutput, target);

            string previousIosTarget =
                PlayerSettings.iOS.targetOSVersionString;
            try
            {
                CreateTemporaryEvidenceScene(
                    lane,
                    effectsOff,
                    reducedMotion);
                if (target == BuildTarget.iOS)
                {
                    PlayerSettings.iOS.targetOSVersionString =
                        "15.0";
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { TemporaryScenePath },
                    locationPathName = absoluteOutput,
                    target = target,
                    options =
                        BuildOptions.Development |
                        BuildOptions.AllowDebugging
                };
                BuildReport report =
                    BuildPipeline.BuildPlayer(options);
                WriteManifest(
                    report,
                    target,
                    lane,
                    absoluteOutput,
                    effectsOff,
                    reducedMotion);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        "Slagfall evidence Player failed: " +
                        $"{report.summary.result}; " +
                        $"errors={report.summary.totalErrors}; " +
                        $"warnings={report.summary.totalWarnings}");
                }

                Debug.Log(
                    "SLAGFALL_EVIDENCE_BUILD_COMPLETE " +
                    $"target={target} " +
                    $"lane={SlagfallEvidenceContract.StableId(lane)} " +
                    $"bytes={report.summary.totalSize} " +
                    $"output={absoluteOutput}");
                return report;
            }
            finally
            {
                if (target == BuildTarget.iOS)
                {
                    PlayerSettings.iOS.targetOSVersionString =
                        previousIosTarget;
                }
                AssetDatabase.DeleteAsset(TemporaryScenePath);
                AssetDatabase.Refresh();
            }
        }

        private static void CreateTemporaryEvidenceScene(
            SlagfallEvidenceLane lane,
            bool effectsOff,
            bool reducedMotion)
        {
            Scene scene = EditorSceneManager.OpenScene(
                SlagfallRepresentativeSliceBuilder.ScenePath,
                OpenSceneMode.Additive);
            try
            {
                SlagfallDeviceEvidenceRunner runner = scene
                    .GetRootGameObjects()
                    .SelectMany(
                        root =>
                            root.GetComponentsInChildren<
                                SlagfallDeviceEvidenceRunner>(true))
                    .SingleOrDefault();
                SlagfallRepresentativeSlice slice = scene
                    .GetRootGameObjects()
                    .SelectMany(
                        root =>
                            root.GetComponentsInChildren<
                                SlagfallRepresentativeSlice>(true))
                    .SingleOrDefault();
                if (runner == null || slice == null)
                {
                    throw new InvalidOperationException(
                        "The Slagfall evidence scene is missing its runner or representative slice.");
                }

                runner.Configure(
                    slice,
                    lane,
                    SlagfallEvidenceContract.MinimumRunSeconds,
                    effectsOff,
                    reducedMotion);
                EditorUtility.SetDirty(runner);
                if (!EditorSceneManager.SaveScene(
                    scene,
                    TemporaryScenePath,
                    true))
                {
                    throw new InvalidOperationException(
                        "The temporary Slagfall evidence scene could not be saved.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateBuildInputs()
        {
            SlagfallRepresentativeSliceProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    SlagfallRepresentativeSliceProfile>(
                    SlagfallRepresentativeSliceBuilder.ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "The committed Slagfall representative profile is invalid: profile_missing");
            }

            if (!profile.Validate(out string diagnostic))
            {
                throw new InvalidOperationException(
                    "The committed Slagfall representative profile is invalid: " +
                    diagnostic);
            }

            SceneAsset scene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    SlagfallRepresentativeSliceBuilder.ScenePath);
            if (scene == null)
            {
                throw new InvalidOperationException(
                    "The committed Slagfall evidence scene is missing.");
            }

            bool sceneInNormalPlayer =
                EditorBuildSettings.scenes.Any(
                    entry =>
                        entry.enabled &&
                        string.Equals(
                            entry.path,
                            SlagfallRepresentativeSliceBuilder.ScenePath,
                            StringComparison.Ordinal));
            if (sceneInNormalPlayer)
            {
                throw new InvalidOperationException(
                    "The Slagfall evidence scene entered normal Player build settings.");
            }
        }

        private static void WriteManifest(
            BuildReport report,
            BuildTarget target,
            SlagfallEvidenceLane lane,
            string outputPath,
            bool effectsOff,
            bool reducedMotion)
        {
            var manifest = new SlagfallEvidenceBuildManifest
            {
                schemaVersion =
                    "slagfall-evidence-build-v001",
                sourceVersion =
                    SlagfallSourceAuthority.SourceVersion,
                createdUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                buildTarget = target.ToString(),
                evidenceLane =
                    SlagfallEvidenceContract.StableId(lane),
                scene =
                    SlagfallRepresentativeSliceBuilder.ScenePath,
                sceneExcludedFromNormalBuildSettings =
                    !EditorBuildSettings.scenes.Any(
                        entry =>
                            entry.enabled &&
                            string.Equals(
                                entry.path,
                                SlagfallRepresentativeSliceBuilder
                                    .ScenePath,
                                StringComparison.Ordinal)),
                intendedDurationSeconds =
                    SlagfallEvidenceContract.MinimumRunSeconds,
                effectsOff = effectsOff,
                reducedMotion = reducedMotion,
                developmentBuild = true,
                outputPath = outputPath,
                buildResult = report.summary.result.ToString(),
                totalBytes = report.summary.totalSize.ToString(),
                totalErrors = (int)report.summary.totalErrors,
                totalWarnings = (int)report.summary.totalWarnings,
                iosMinimumVersion =
                    target == BuildTarget.iOS
                        ? "15.0"
                        : "not_applicable"
            };
            File.WriteAllText(
                outputPath + ".build.json",
                JsonUtility.ToJson(manifest, true));
        }

        private static void ValidateLaneForTarget(
            BuildTarget target,
            SlagfallEvidenceLane lane)
        {
            if (lane == SlagfallEvidenceLane.MobileLow &&
                target != BuildTarget.Android)
            {
                throw new ArgumentException(
                    "The mobile_low evidence lane requires a constrained Android GLES3 or Vulkan device.");
            }

            bool mobileTarget =
                target == BuildTarget.Android ||
                target == BuildTarget.iOS;
            bool mobileLane =
                lane == SlagfallEvidenceLane.MobileLow ||
                lane == SlagfallEvidenceLane.MobileStandard;
            if (mobileTarget != mobileLane)
            {
                throw new ArgumentException(
                    $"Evidence lane {lane} does not match build target {target}.");
            }
        }

        private static BuildTarget ParseTarget(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "android":
                    return BuildTarget.Android;
                case "ios":
                    return BuildTarget.iOS;
                case "desktop":
                case null:
                case "":
                    return ResolveHostDesktopTarget();
                case "windows":
                case "windows64":
                    return BuildTarget.StandaloneWindows64;
                case "macos":
                case "osx":
                    return BuildTarget.StandaloneOSX;
                case "linux":
                case "linux64":
                    return BuildTarget.StandaloneLinux64;
                default:
                    throw new ArgumentException(
                        $"Unknown Slagfall evidence target '{value}'.");
            }
        }

        private static SlagfallEvidenceLane ParseLane(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "mobile_low":
                    return SlagfallEvidenceLane.MobileLow;
                case "mobile_standard":
                    return SlagfallEvidenceLane.MobileStandard;
                case "desktop_low":
                    return SlagfallEvidenceLane.DesktopLow;
                case "desktop_standard":
                case null:
                case "":
                    return SlagfallEvidenceLane.DesktopStandard;
                default:
                    throw new ArgumentException(
                        $"Unknown Slagfall evidence lane '{value}'.");
            }
        }

        private static BuildTarget ResolveHostDesktopTarget()
        {
#if UNITY_EDITOR_WIN
            return BuildTarget.StandaloneWindows64;
#elif UNITY_EDITOR_LINUX
            return BuildTarget.StandaloneLinux64;
#else
            return BuildTarget.StandaloneOSX;
#endif
        }

        private static string ResolveDefaultOutput(
            BuildTarget target,
            SlagfallEvidenceLane lane)
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SlagfallEvidence",
                SlagfallEvidenceContract.StableId(lane));
            switch (target)
            {
                case BuildTarget.Android:
                    return Path.Combine(folder, "SlagfallEvidence.apk");
                case BuildTarget.iOS:
                    return Path.Combine(folder, "SlagfallEvidence-iOS");
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(folder, "SlagfallEvidence.exe");
                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(folder, "SlagfallEvidence");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(folder, "SlagfallEvidence.app");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(target),
                        target,
                        "Unsupported Slagfall evidence target.");
            }
        }

        private static void EnsureOutputParent(
            string outputPath,
            BuildTarget target)
        {
            string directory =
                target == BuildTarget.iOS
                    ? outputPath
                    : Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string ReadArgument(
            string[] arguments,
            string name)
        {
            for (int index = 0;
                index < arguments.Length - 1;
                index++)
            {
                if (string.Equals(
                    arguments[index],
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }
            return null;
        }

        private static bool HasFlag(
            string[] arguments,
            string name)
        {
            return arguments.Any(
                argument =>
                    string.Equals(
                        argument,
                        name,
                        StringComparison.OrdinalIgnoreCase));
        }

        [Serializable]
        private sealed class SlagfallEvidenceBuildManifest
        {
            public string schemaVersion;
            public string sourceVersion;
            public string createdUtc;
            public string unityVersion;
            public string buildTarget;
            public string evidenceLane;
            public string scene;
            public bool sceneExcludedFromNormalBuildSettings;
            public float intendedDurationSeconds;
            public bool effectsOff;
            public bool reducedMotion;
            public bool developmentBuild;
            public string outputPath;
            public string buildResult;
            public string totalBytes;
            public int totalErrors;
            public int totalWarnings;
            public string iosMinimumVersion;
        }
    }
}
