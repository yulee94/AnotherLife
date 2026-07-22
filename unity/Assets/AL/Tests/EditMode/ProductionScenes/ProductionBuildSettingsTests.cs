using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// #150 ShellFoundation contract: exact committed Build Settings, strict failure classifications,
    /// exact Windows64 Development build intent, report classification, and ordered launch-log evidence.
    /// </summary>
    public sealed class ProductionBuildSettingsTests
    {
        [Test]
        public void CurrentBuildSettingsAreExactShellFoundationAndValidateClean()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.AreEqual(3, scenes.Length);
            Assert.AreEqual(new[]
            {
                "Assets/AL/Scenes/Boot.unity",
                "Assets/AL/Scenes/RealmSelection.unity",
                "Assets/AL/Scenes/Kingdom.unity"
            }, scenes.Select(scene => scene.path).ToArray());
            Assert.IsTrue(scenes.All(scene => scene.enabled));
            Assert.IsFalse(scenes.Any(scene => scene.path == "Assets/Test.unity"));
            Assert.IsFalse(scenes.Any(scene => scene.path.EndsWith("/ChampionArena.unity", StringComparison.Ordinal)));

            object report = R.StaticMethod(R.BuildSettingsValidatorType, "ValidateCurrent");
            Assert.IsTrue(R.PropBool(report, "IsValid"), R.Invoke(report, "Summarize").ToString());
            Assert.AreEqual(new[] { "Valid" }, Outcomes(report));
        }

        [Test]
        public void EmptyBuildSettingsFail()
        {
            AssertOutcome(Validate(), "EmptyBuildSettings");
        }

        [Test]
        public void MissingBootAndWrongIndexZeroFail()
        {
            object report = Validate(Entry(RealmPath), Entry(KingdomPath));
            AssertOutcome(report, "WrongEntryScene");
            AssertOutcome(report, "MissingRequiredScene");
        }

        [Test]
        public void MissingRealmSelectionOrKingdomFails()
        {
            AssertOutcome(Validate(Entry(BootPath), Entry(KingdomPath)), "MissingRequiredScene");
            AssertOutcome(Validate(Entry(BootPath), Entry(RealmPath)), "MissingRequiredScene");
        }

        [Test]
        public void WrongOrderFails()
        {
            object report = Validate(Entry(BootPath), Entry(KingdomPath), Entry(RealmPath));
            Assert.IsFalse(R.PropBool(report, "IsValid"));
            AssertOutcome(report, "UnexpectedScene");
        }

        [Test]
        public void DisabledRequiredOrStaleEntryFails()
        {
            object requiredDisabled = Validate(Entry(BootPath), Entry(RealmPath, enabled: false), Entry(KingdomPath));
            AssertOutcome(requiredDisabled, "DisabledStaleScene");

            object staleDisabled = Validate(
                Entry(BootPath), Entry(RealmPath), Entry(KingdomPath),
                Entry("Assets/Other/Legacy.unity", enabled: false));
            AssertOutcome(staleDisabled, "DisabledStaleScene");
            AssertOutcome(staleDisabled, "UnexpectedScene");
        }

        [Test]
        public void TestOrChampionListedFailsWithSpecificOutcome()
        {
            object test = Validate(Entry(BootPath), Entry(RealmPath), Entry(KingdomPath), Entry("Assets/Test.unity"));
            AssertOutcome(test, "TestSceneEnabled");

            object champion = Validate(
                Entry(BootPath), Entry(RealmPath), Entry(KingdomPath),
                Entry("Assets/AL/Scenes/ChampionArena.unity"));
            AssertOutcome(champion, "DeferredSceneEnabled");
        }

        [Test]
        public void MissingPathFails()
        {
            object report = Validate(Entry(BootPath), Entry(RealmPath, assetExists: false), Entry(KingdomPath));
            AssertOutcome(report, "MissingPath");
        }

        [Test]
        public void DuplicatePathAndDuplicateNameFail()
        {
            object duplicatePath = Validate(Entry(BootPath), Entry(BootPath), Entry(RealmPath), Entry(KingdomPath));
            AssertOutcome(duplicatePath, "DuplicatePath");
            AssertOutcome(duplicatePath, "DuplicateName");

            object duplicateName = Validate(
                Entry(BootPath), Entry(RealmPath), Entry(KingdomPath), Entry("Assets/Legacy/Boot.unity"));
            AssertOutcome(duplicateName, "DuplicateName");
        }

        [Test]
        public void GuidMismatchFails()
        {
            object report = Validate(
                Entry(BootPath, resolvedGuid: "00000000000000000000000000000000"),
                Entry(RealmPath),
                Entry(KingdomPath));
            AssertOutcome(report, "GuidMismatch");
        }

        [Test]
        public void ExactShellFoundationEntriesPassPureValidation()
        {
            object report = Validate(Entry(BootPath), Entry(RealmPath), Entry(KingdomPath));
            Assert.IsTrue(R.PropBool(report, "IsValid"), R.Invoke(report, "Summarize").ToString());
            Assert.AreEqual(new[] { "Valid" }, Outcomes(report));
        }

        [Test]
        public void WindowsBuildOptionsAreExactAndExcludeTestAndChampion()
        {
            object options = R.StaticMethod(R.PlayerBuilderType, "CreateWindows64DevelopmentOptions");
            Assert.AreEqual("StandaloneWindows64", R.Prop(options, "target").ToString());
            Assert.AreEqual(BuildOptions.Development, (BuildOptions)R.Prop(options, "options"));
            Assert.AreEqual(new[] { BootPath, RealmPath, KingdomPath }, (string[])R.Prop(options, "scenes"));

            string output = (string)R.Prop(options, "locationPathName");
            Assert.That(output.Replace('\\', '/'), Does.EndWith("/unity/Builds/Validation/Windows64/AnotherLifeUnity.exe"));

            string intent = R.StaticMethod(R.PlayerBuilderType, "DescribeBuildIntent").ToString();
            Assert.That(intent, Does.Contain("Assets/Test.unity"));
            Assert.That(intent, Does.Contain("Assets/AL/Scenes/ChampionArena.unity"));
            Assert.That(intent, Does.Contain("excluded=["));
        }

        [Test]
        public void BuildReportClassificationRequiresSuccessAndBothOutputParts()
        {
            Assert.AreEqual("Succeeded", Classify(BuildResult.Succeeded, true, true));
            Assert.AreEqual("MissingOutput", Classify(BuildResult.Succeeded, false, true));
            Assert.AreEqual("MissingOutput", Classify(BuildResult.Succeeded, true, false));
            Assert.AreEqual("BuildFailed", Classify(BuildResult.Failed, true, true));
            Assert.AreEqual("BuildFailed", Classify(BuildResult.Cancelled, true, true));
            Assert.AreEqual("BuildFailed", Classify(BuildResult.Unknown, true, true));
        }

        [Test]
        public void PreflightValidatesRepositoryBeforeAnyBuildCall()
        {
            object preflight = R.StaticMethod(R.PlayerBuilderType, "ValidatePreflight");
            object settings = R.Prop(preflight, "BuildSettings");
            Assert.IsTrue(R.PropBool(settings, "IsValid"), R.Invoke(settings, "Summarize").ToString());
            Assert.IsTrue(R.PropBool(preflight, "UnityVersionValid"));
            Assert.IsTrue(R.PropBool(preflight, "CompilationValid"));
            Assert.IsTrue(R.PropBool(preflight, "OutputPathValid"));
            Assert.IsTrue(R.PropBool(preflight, "OutputPathIgnored"));

            // Platform modules are intentionally environmental: Windows CI passes; a Mac editor without
            // Windows support fails closed before BuildPipeline.BuildPlayer.
            if (!R.PropBool(preflight, "BuildTargetSupported"))
            {
                Assert.IsFalse(R.PropBool(preflight, "IsValid"));
                Assert.That(R.AsStrings(R.Prop(preflight, "Failures")),
                    Has.Some.Contains("StandaloneWindows64 build support is not installed"));
            }
        }

        [Test]
        public void PreflightFailurePreventsBuildAndRemovesStaleOutput()
        {
            EditorBuildSettingsScene[] before = EditorBuildSettings.scenes;
            int buildCalls = 0;
            Func<BuildPlayerOptions, UnityEditor.Build.Reporting.BuildReport> buildOverride = options =>
            {
                buildCalls++;
                return null;
            };
            R.SetStaticField(R.PlayerBuilderType, "BuildPlayerOverride", buildOverride);

            string output = (string)R.StaticProperty(R.PlayerBuilderType, "OutputPath");
            string directory = Path.GetDirectoryName(output);
            string stale = Path.Combine(directory, "stale-output-sentinel.txt");
            Directory.CreateDirectory(directory);
            File.WriteAllText(stale, "stale");

            try
            {
                EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
                object result = R.StaticMethod(R.PlayerBuilderType, "BuildWindows64DevelopmentPlayer");
                Assert.AreEqual("PreflightFailed", R.Prop(result, "Status").ToString());
                Assert.AreEqual(0, buildCalls, "BuildPipeline seam must not run after a preflight failure.");
                Assert.IsFalse(File.Exists(stale), "Exact validation output must be cleaned before preflight evidence is emitted.");
                Assert.IsFalse(File.Exists(output), "A stale executable must not survive a failed preflight.");
                Assert.IsTrue(File.Exists(Path.Combine(directory, "PlayerBuildWindows64.summary.json")));
            }
            finally
            {
                EditorBuildSettings.scenes = before;
                R.SetStaticField(R.PlayerBuilderType, "BuildPlayerOverride", null);
            }
        }

        [Test]
        public void OrderedFreshProfileLaunchLogPassesAndAllowsExternalTermination()
        {
            object result = ValidateLaunch(ValidLaunchLog, processExited: true, isolatedProfileVerified: true);
            Assert.IsTrue(R.PropBool(result, "IsValid"));
            Assert.IsTrue(R.PropBool(result, "ExternalTerminationAllowed"));
            Assert.That(R.PropString(result, "TerminationDisposition"), Does.Contain("no graceful quit/save claim"));
        }

        [Test]
        public void LaunchLogWrongOrderOrMissingSecondMarkerFails()
        {
            string wrongOrder = RealmMarker + "\n" + BootMarker + "\nAL Boot Sequence Started...\n" +
                                "No Realm Selected. Transitioning to Realm Selection...";
            AssertLaunchOutcome(ValidateLaunch(wrongOrder, false, true), "WrongOrder");

            string earlyExit = BootMarker + "\nAL Boot Sequence Started...\n" +
                               "No Realm Selected. Transitioning to Realm Selection...";
            object missing = ValidateLaunch(earlyExit, true, true);
            AssertLaunchOutcome(missing, "MissingRealmSelectionMarker");
            AssertLaunchOutcome(missing, "ProcessExitedEarly");
        }

        [Test]
        public void LaunchLogSevereErrorWrongBranchOrUnverifiedProfileFails()
        {
            AssertLaunchOutcome(ValidateLaunch(ValidLaunchLog + "\nNullReferenceException", false, true), "SevereLog");
            AssertLaunchOutcome(ValidateLaunch(ValidLaunchLog + "\n[AL-SCENE-ACTIVE] id=al_scene_kingdom name=Kingdom", false, true), "WrongProfileBranch");
            AssertLaunchOutcome(ValidateLaunch(ValidLaunchLog, false, false), "ProfileIsolationFailed");
        }

        [Test]
        public void OrdinaryWarningsDoNotFailLaunchEvidence()
        {
            object result = ValidateLaunch(ValidLaunchLog + "\nWarning: optional cosmetic asset was not preloaded.", false, true);
            Assert.IsTrue(R.PropBool(result, "IsValid"));
        }

        private const string BootPath = "Assets/AL/Scenes/Boot.unity";
        private const string RealmPath = "Assets/AL/Scenes/RealmSelection.unity";
        private const string KingdomPath = "Assets/AL/Scenes/Kingdom.unity";
        private const string BootMarker =
            "[AL-SCENE-ACTIVE] id=al_scene_boot name=Boot path=Assets/AL/Scenes/Boot.unity role=production_entry version=223.1";
        private const string RealmMarker =
            "[AL-SCENE-ACTIVE] id=al_scene_realm_selection name=RealmSelection path=Assets/AL/Scenes/RealmSelection.unity role=onboarding_selection version=223.1";
        private const string ValidLaunchLog = BootMarker + "\nAL Boot Sequence Started...\n" +
                                              "No Realm Selected. Transitioning to Realm Selection...\n" + RealmMarker;

        private static object Entry(
            string path,
            bool enabled = true,
            string serializedGuid = null,
            string resolvedGuid = null,
            bool assetExists = true)
        {
            object record = R.DescriptorAll().FirstOrDefault(item => R.PropString(item, "AssetPath") == path);
            string expectedGuid = record == null ? "11111111111111111111111111111111" : R.PropString(record, "AssetGuid");
            return Activator.CreateInstance(
                R.Runtime(R.BuildSceneEntryType),
                path,
                enabled,
                serializedGuid ?? expectedGuid,
                resolvedGuid ?? expectedGuid,
                assetExists);
        }

        private static object Validate(params object[] entries)
        {
            Type entryType = R.Runtime(R.BuildSceneEntryType);
            Array typed = Array.CreateInstance(entryType, entries.Length);
            for (int index = 0; index < entries.Length; index++)
            {
                typed.SetValue(entries[index], index);
            }

            MethodInfo method = R.Runtime(R.BuildSettingsValidatorType).GetMethod(
                "ValidateEntries",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            return method.Invoke(null, new object[] { typed });
        }

        private static string[] Outcomes(object report)
        {
            return R.AsObjects(R.Prop(report, "Outcomes")).Select(item => item.ToString()).ToArray();
        }

        private static void AssertOutcome(object report, string expected)
        {
            Assert.That(Outcomes(report), Has.Member(expected), R.Invoke(report, "Summarize").ToString());
        }

        private static string Classify(BuildResult result, bool executableExists, bool dataDirectoryExists)
        {
            return R.StaticMethod(
                R.PlayerBuilderType,
                "ClassifyBuildResult",
                result,
                executableExists,
                dataDirectoryExists).ToString();
        }

        private static object ValidateLaunch(string log, bool processExited, bool isolatedProfileVerified)
        {
            return R.StaticMethod(
                R.PlayerLaunchValidatorType,
                "Evaluate",
                log,
                processExited,
                isolatedProfileVerified);
        }

        private static void AssertLaunchOutcome(object result, string expected)
        {
            string[] outcomes = R.AsObjects(R.Prop(result, "Outcomes")).Select(item => item.ToString()).ToArray();
            Assert.That(outcomes, Has.Member(expected));
            Assert.IsFalse(R.PropBool(result, "IsValid"));
        }
    }
}
