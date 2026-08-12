#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AL.EditorTools
{
    /// <summary>Deterministic local iOS Simulator export for the interactive launch-flow test.</summary>
    public static class IosSimulatorPlayerBuilder
    {
        private const string DefaultOutputRelativePath = "Builds/Validation/iOSSimulator";

        private static readonly string[] ScenePaths =
        {
            "Assets/AL/Scenes/Boot.unity",
            "Assets/AL/Scenes/RealmSelection.unity",
            "Assets/AL/Scenes/Kingdom.unity"
        };

        [MenuItem("Another Life/Build/iOS Simulator Test")]
        public static void BuildSimulatorTest()
        {
            if (!string.Equals(Application.unityVersion, "2022.3.62f3", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The iOS test export requires Unity 2022.3.62f3; actual: " +
                    Application.unityVersion);
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                throw new InvalidOperationException(
                    "The active build target must already be iOS. In batch mode pass -buildTarget iOS.");
            }

            string missingScene = ScenePaths.FirstOrDefault(path => !File.Exists(path));
            if (!string.IsNullOrEmpty(missingScene))
            {
                throw new FileNotFoundException("Required iOS test scene is missing.", missingScene);
            }

            string requestedOutput = Environment.GetEnvironmentVariable("AL_IOS_SIMULATOR_OUTPUT");
            string outputPath = Path.GetFullPath(string.IsNullOrWhiteSpace(requestedOutput)
                ? Path.Combine(Directory.GetCurrentDirectory(), DefaultOutputRelativePath)
                : requestedOutput);
            Directory.CreateDirectory(outputPath);

            iOSSdkVersion originalSdk = PlayerSettings.iOS.sdkVersion;
            AppleMobileArchitectureSimulator originalArchitecture =
                PlayerSettings.iOS.simulatorSdkArchitecture;

            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = ScenePaths,
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.Development
                });

                if (report == null || report.summary.result != BuildResult.Succeeded)
                {
                    string result = report == null ? "no report" : report.summary.result.ToString();
                    throw new InvalidOperationException("iOS Simulator export failed: " + result);
                }

                Debug.Log(
                    "[AL-IOS-SIMULATOR-EXPORT] " + outputPath +
                    " | bytes=" + report.summary.totalSize +
                    " | duration=" + report.summary.totalTime);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = originalSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = originalArchitecture;
            }
        }
    }
}
#endif
