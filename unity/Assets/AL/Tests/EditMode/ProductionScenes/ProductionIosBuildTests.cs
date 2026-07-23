using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    public sealed class ProductionIosBuildTests
    {
        [Test]
        public void IosDevelopmentOptionsUseExactShellFoundationProfile()
        {
            object options = R.StaticMethod(R.IosBuilderType, "CreateIosDevelopmentOptions");
            Assert.AreEqual("iOS", R.Prop(options, "target").ToString());
            Assert.AreEqual(BuildOptions.Development, (BuildOptions)R.Prop(options, "options"));
            Assert.AreEqual(new[] { BootPath, RealmPath, KingdomPath }, (string[])R.Prop(options, "scenes"));

            string output = (string)R.Prop(options, "locationPathName");
            Assert.That(output.Replace('\\', '/'), Does.EndWith("/unity/Builds/Validation/iOS/Xcode"));
        }

        [Test]
        public void PreflightValidatesScenesModuleOutputAndRecordsEnvironmentNotices()
        {
            object preflight = R.StaticMethod(R.IosBuilderType, "ValidatePreflight");
            object settings = R.Prop(preflight, "BuildSettings");
            Assert.IsTrue(R.PropBool(settings, "IsValid"), R.Invoke(settings, "Summarize").ToString());
            Assert.IsTrue(R.PropBool(preflight, "UnityVersionValid"));
            Assert.IsTrue(R.PropBool(preflight, "CompilationValid"));
            Assert.IsTrue(R.PropBool(preflight, "OutputPathValid"));
            Assert.IsTrue(R.PropBool(preflight, "OutputPathIgnored"));
            Assert.IsTrue(R.PropBool(preflight, "BundleIdentifierValid"));
            Assert.IsTrue(R.PropBool(preflight, "TargetOsVersionValid"));

            if (!R.PropBool(preflight, "IosModuleAvailable"))
            {
                Assert.IsFalse(R.PropBool(preflight, "IsValid"));
                Assert.That(R.AsStrings(R.Prop(preflight, "Failures")),
                    Has.Some.Contains("iOS Build Support is not installed"));
            }

            if (!R.PropBool(preflight, "XcodeInstalled"))
            {
                Assert.That(R.AsStrings(R.Prop(preflight, "Notices")),
                    Has.Some.Contains("Full Xcode is not installed"));
            }
        }

        [Test]
        public void SigningSnapshotIsReadOnlyAndReportsCurrentReadiness()
        {
            string settingsPath = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset"));
            byte[] before = File.ReadAllBytes(settingsPath);

            object signing = R.StaticMethod(R.IosBuilderType, "CurrentSigningSnapshot");
            object options = R.StaticMethod(R.IosBuilderType, "CreateIosDevelopmentOptions");
            object preflight = R.StaticMethod(R.IosBuilderType, "ValidatePreflight");

            Assert.NotNull(options);
            Assert.NotNull(preflight);
            Assert.AreEqual("com.yulee94.anotherlife", R.PropString(signing, "BundleIdentifier"));
            Assert.IsNotEmpty(R.PropString(signing, "BundleVersion"));
            Assert.AreEqual("14.0", R.PropString(signing, "TargetOsVersion"));
            Assert.AreEqual(before, File.ReadAllBytes(settingsPath),
                "iOS inspection/export planning must not mutate Player Settings.");
        }

        [Test]
        public void ExportClassificationRequiresSuccessAndCompleteXcodeProject()
        {
            Assert.AreEqual("Succeeded", Classify(BuildResult.Succeeded, true));
            Assert.AreEqual("MissingXcodeProject", Classify(BuildResult.Succeeded, false));
            Assert.AreEqual("ExportFailed", Classify(BuildResult.Failed, true));
            Assert.AreEqual("ExportFailed", Classify(BuildResult.Cancelled, true));
            Assert.AreEqual("ExportFailed", Classify(BuildResult.Unknown, true));
        }

        [Test]
        public void PreflightFailurePreventsExportAndPreservesLastXcodeOutput()
        {
            EditorBuildSettingsScene[] before = EditorBuildSettings.scenes;
            int buildCalls = 0;
            Func<BuildPlayerOptions, UnityEditor.Build.Reporting.BuildReport> buildOverride = options =>
            {
                buildCalls++;
                return null;
            };
            R.SetStaticField(R.IosBuilderType, "BuildPlayerOverride", buildOverride);

            string output = (string)R.StaticProperty(R.IosBuilderType, "OutputPath");
            string stale = Path.Combine(output, "stale-xcode-sentinel.txt");
            Directory.CreateDirectory(output);
            File.WriteAllText(stale, "stale");

            try
            {
                EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
                object result = R.StaticMethod(R.IosBuilderType, "ExportIosDevelopment");
                Assert.AreEqual("PreflightFailed", R.Prop(result, "Status").ToString());
                Assert.AreEqual(0, buildCalls, "BuildPipeline seam must not run after a preflight failure.");
                Assert.IsTrue(File.Exists(stale), "A failed preflight must preserve the last successful Xcode export.");
                Assert.IsTrue(File.Exists(Path.Combine(
                    Path.GetDirectoryName(output) ?? string.Empty,
                    "IosDevelopmentExport.summary.json")));
            }
            finally
            {
                EditorBuildSettings.scenes = before;
                R.SetStaticField(R.IosBuilderType, "BuildPlayerOverride", null);
                if (File.Exists(stale))
                {
                    File.Delete(stale);
                }
            }
        }

        [Test]
        public void ExportIntentIsSigningNeutralAndExcludesTestAndChampion()
        {
            string intent = R.StaticMethod(R.IosBuilderType, "DescribeExportIntent").ToString();
            Assert.That(intent, Does.Contain("target=iOS"));
            Assert.That(intent, Does.Contain("options=Development"));
            Assert.That(intent, Does.Contain("signingMutation=false"));
            Assert.That(intent, Does.Contain("Assets/Test.unity"));
            Assert.That(intent, Does.Contain("Assets/AL/Scenes/ChampionArena.unity"));
            Assert.That(intent, Does.Contain("excluded=["));
        }

        private const string BootPath = "Assets/AL/Scenes/Boot.unity";
        private const string RealmPath = "Assets/AL/Scenes/RealmSelection.unity";
        private const string KingdomPath = "Assets/AL/Scenes/Kingdom.unity";

        private static string Classify(BuildResult result, bool requiredProjectFilesExist)
        {
            return R.StaticMethod(
                R.IosBuilderType,
                "ClassifyExport",
                result,
                requiredProjectFilesExist).ToString();
        }
    }
}
