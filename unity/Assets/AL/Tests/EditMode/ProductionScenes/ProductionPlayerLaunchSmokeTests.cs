using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// #150 isolated Player launch-smoke parser/process contract. Editor-tool types are reached by
    /// reflection because AL.EditMode.Tests cannot reference the predefined editor assembly.
    /// </summary>
    public sealed class ProductionPlayerLaunchSmokeTests
    {
        private const string SmokeTypeName = "AL.EditorTools.ProductionPlayerLaunchSmoke";
        private const string ProcessTypeName = "AL.EditorTools.ProductionPlayerProcessObservation";
        private const string IsolationTypeName = "AL.EditorTools.ProductionPlayerIsolationEvidence";

        [Test]
        public void ExactOrderedFreshProfileSequencePasses()
        {
            object result = Evaluate(ValidLog(), Process(), ValidIsolation());

            AssertResult(result, "Passed", "None");
            Assert.IsTrue(Bool(result, "IsolationAccepted"));
            Assert.IsTrue(Bool(result, "BootMarkerObserved"));
            Assert.IsTrue(Bool(result, "BootSequenceStartedObserved"));
            Assert.IsTrue(Bool(result, "FreshProfileBranchObserved"));
            Assert.IsTrue(Bool(result, "RealmSelectionMarkerObserved"));
            Assert.IsTrue(Bool(result, "TransitionPassed"));
            Assert.IsFalse(Bool(result, "ClaimsGracefulQuitOrSave"));
            Assert.AreEqual(4242, Convert.ToInt32(Property(result, "ProcessId")));
            Assert.IsTrue(Bool(result, "LogObserved"));
            Assert.IsTrue(Bool(result, "LogWasAbsentBeforeLaunch"));
            Assert.AreEqual(DateTimeKind.Utc, ((DateTime)Property(result, "ProcessStartedAtUtc")).Kind);
        }

        [Test]
        public void ExpectedMarkersStayBoundToExactDescriptorIdentity()
        {
            Assert.AreEqual(
                "[AL-SCENE-ACTIVE] id=al_scene_boot name=Boot path=Assets/AL/Scenes/Boot.unity role=production_entry version=223.1",
                StaticString("ExpectedBootMarkerLine"));
            Assert.AreEqual(
                "[AL-SCENE-ACTIVE] id=al_scene_realm_selection name=RealmSelection path=Assets/AL/Scenes/RealmSelection.unity role=onboarding_selection version=223.1",
                StaticString("ExpectedRealmSelectionMarkerLine"));
        }

        [Test]
        public void ExpectedPersistentDataIdentityStaysBoundToUnchangedPlayerSettings()
        {
            Assert.AreEqual("DefaultCompany", StaticString("ExpectedCompanyName"));
            Assert.AreEqual("AnotherLifeUnity", StaticString("ExpectedProductName"));
        }

        [Test]
        public void RealmSelectionBeforeBootFailsImmediately()
        {
            object result = Evaluate(StaticString("ExpectedRealmSelectionMarkerLine"), Process(), ValidIsolation());
            AssertResult(result, "Failed", "MarkerOrderInvalid");
        }

        [Test]
        public void RealmSelectionBeforeFreshBranchLogsFailsImmediately()
        {
            string log = Join(
                StaticString("ExpectedBootMarkerLine"),
                StaticString("ExpectedRealmSelectionMarkerLine"));

            object result = Evaluate(log, Process(), ValidIsolation());
            AssertResult(result, "Failed", "MarkerOrderInvalid");
        }

        [Test]
        public void MissingRealmSelectionRemainsRunningWhileProcessIsLive()
        {
            object result = Evaluate(LogThroughFreshBranch(), Process(), ValidIsolation());
            AssertResult(result, "Running", "None");
            Assert.That(String(result, "Diagnostic"), Does.Contain("missing RealmSelection"));
        }

        [Test]
        public void MissingRealmSelectionTimesOutDeterministically()
        {
            object result = Evaluate(LogThroughFreshBranch(), Process(timedOut: true), ValidIsolation());
            AssertResult(result, "TimedOut", "TimedOut");
            Assert.That(String(result, "Diagnostic"), Does.Contain("missing RealmSelection"));
        }

        [Test]
        public void ProcessExitBeforeRealmSelectionIsEarlyExit()
        {
            object result = Evaluate(LogThroughFreshBranch(), Process(hasExited: true), ValidIsolation());
            AssertResult(result, "EarlyExit", "ProcessExitedEarly");
        }

        [Test]
        public void ExternalTerminationBeforeRealmSelectionIsDistinctEarlyExit()
        {
            object result = Evaluate(
                LogThroughFreshBranch(),
                Process(hasExited: true, terminatedExternally: true),
                ValidIsolation());

            AssertResult(result, "EarlyExit", "ExternallyTerminatedEarly");
            Assert.IsTrue(Bool(result, "ExternalTerminationReported"));
            Assert.IsFalse(Bool(result, "ClaimsGracefulQuitOrSave"));
        }

        [TestCase("al_scene_kingdom", "Kingdom", "Assets/AL/Scenes/Kingdom.unity")]
        [TestCase("al_scene_test_representative", "Test", "Assets/Test.unity")]
        [TestCase("al_scene_champion_arena", "ChampionArena", "Assets/AL/Scenes/ChampionArena.unity")]
        public void WrongKingdomTestOrChampionMarkerFailsBeforeSuccess(string id, string name, string path)
        {
            string wrong = $"[AL-SCENE-ACTIVE] id={id} name={name} path={path} role=wrong version=223.1";
            string log = Join(LogThroughFreshBranch(), wrong, StaticString("ExpectedRealmSelectionMarkerLine"));

            object result = Evaluate(log, Process(), ValidIsolation());
            AssertResult(result, "Failed", "UnexpectedSceneMarker");
        }

        [Test]
        public void BootMarkerFieldDriftCannotCountAsBoot()
        {
            string drifted = StaticString("ExpectedBootMarkerLine").Replace("version=223.1", "version=stale");
            object result = Evaluate(drifted, Process(), ValidIsolation());
            AssertResult(result, "Failed", "MarkerMismatch");
        }

        [Test]
        public void ExplicitSceneMarkerMismatchFailsEvenAfterExpectedTransition()
        {
            string log = Join(ValidLog(), "[AL-SCENE-ACTIVE-MISMATCH] id=al_scene_realm_selection path=Wrong");
            object result = Evaluate(log, Process(), ValidIsolation());
            AssertResult(result, "Failed", "MarkerMismatch");
        }

        [TestCase("Scene 'RealmSelection' couldn't be loaded because it has not been added to the build settings")]
        [TestCase("Failed to load scene Assets/AL/Scenes/RealmSelection.unity")]
        [TestCase("RealmSelection is not a valid scene")]
        public void MissingOrInvalidSceneLogFails(string failureLine)
        {
            object result = Evaluate(Join(LogThroughFreshBranch(), failureLine), Process(), ValidIsolation());
            AssertResult(result, "Failed", "SceneLoadFailure");
        }

        [TestCase("[BOOT_STACK_LOAD_FAILED] save load failed")]
        [TestCase("[BOOT_STACK_PARTIAL_REGISTRY] incomplete services")]
        [TestCase("Bootloader initialization failed")]
        public void BootloaderInitializationOrLoadFailureFails(string failureLine)
        {
            object result = Evaluate(Join(LogThroughFreshBranch(), failureLine), Process(), ValidIsolation());
            AssertResult(result, "Failed", "BootloaderFailure");
        }

        [TestCase("ArgumentException: invalid scene")]
        [TestCase("MissingReferenceException: object missing")]
        [TestCase("MissingMethodException: method missing")]
        [TestCase("NullReferenceException: object reference")]
        [TestCase("Unhandled Exception: fatal")]
        [TestCase("Assertion failed: invariant")]
        public void SevereExceptionOrAssertionFails(string failureLine)
        {
            object result = Evaluate(Join(ValidLog(), failureLine), Process(), ValidIsolation());
            AssertResult(result, "Failed", "SevereException");
        }

        [TestCase("The referenced script on this Behaviour is missing!")]
        [TestCase("SerializationException: invalid data")]
        [TestCase("Failed to deserialize component")]
        public void MissingScriptOrSerializationFailureFails(string failureLine)
        {
            object result = Evaluate(Join(LogThroughFreshBranch(), failureLine), Process(), ValidIsolation());
            AssertResult(result, "Failed", "MissingScriptOrSerialization");
        }

        [Test]
        public void OrdinaryKnownLogsDoNotFailMerelyByExisting()
        {
            string log = Join(
                "Initialize engine version 2022.3.62f3",
                StaticString("ExpectedBootMarkerLine"),
                "Exception handling initialized successfully",
                "AL Boot Sequence Started...",
                "No Realm Selected. Transitioning to Realm Selection...",
                "UnloadTime: 1.234 ms",
                StaticString("ExpectedRealmSelectionMarkerLine"),
                "GfxDevice: creating device client");

            object result = Evaluate(log, Process(), ValidIsolation());
            AssertResult(result, "Passed", "None");
        }

        [Test]
        public void ExternalTerminationAfterSuccessIsReportedWithoutGracefulSaveClaim()
        {
            object result = Evaluate(
                ValidLog(),
                Process(hasExited: true, terminatedExternally: true),
                ValidIsolation());

            AssertResult(result, "Passed", "None");
            Assert.IsTrue(Bool(result, "ExternalTerminationReported"));
            Assert.IsTrue(Bool(result, "ProcessExited"));
            Assert.IsFalse(Bool(result, "ClaimsGracefulQuitOrSave"));

            IReadOnlyList<string> report = Strings(Property(result, "ReportLines"));
            Assert.That(report, Does.Contain("transition passed"));
            Assert.That(report, Does.Contain("process terminated externally for validation"));
            Assert.That(report, Does.Contain("no graceful quit/save claim"));
            Assert.That(report, Does.Contain("isolated profile may contain disposable test artifacts"));
            Assert.Throws<NotSupportedException>(() => ((IList)Property(result, "ReportLines")).Add("mutate"));
        }

        [Test]
        public void TimeoutCannotBeOverriddenByMarkersFoundAfterDeadline()
        {
            object result = Evaluate(ValidLog(), Process(timedOut: true), ValidIsolation());
            AssertResult(result, "TimedOut", "TimedOut");
        }

        [Test]
        public void UnattributedProcessExitAfterMarkersDoesNotBecomeAFalsePass()
        {
            object result = Evaluate(ValidLog(), Process(hasExited: true), ValidIsolation());
            AssertResult(result, "EarlyExit", "ProcessExitedEarly");
            Assert.IsFalse(Bool(result, "ClaimsGracefulQuitOrSave"));
        }

        [Test]
        public void ContradictoryExternalTerminationObservationFailsClosed()
        {
            object result = Evaluate(ValidLog(), Process(terminatedExternally: true), ValidIsolation());
            AssertResult(result, "Failed", "ProcessObservationInvalid");
        }

        [Test]
        public void MissingOrStaleRunBoundaryEvidenceFailsClosed()
        {
            AssertResult(
                Evaluate(ValidLog(), null, ValidIsolation()),
                "Failed",
                "ProcessObservationInvalid");
            AssertResult(
                Evaluate(ValidLog(), Process(processId: 0), ValidIsolation()),
                "Failed",
                "ProcessObservationInvalid");
            AssertResult(
                Evaluate(ValidLog(), Process(logWasAbsentBeforeLaunch: false), ValidIsolation()),
                "Failed",
                "ProcessObservationInvalid");
            AssertResult(
                Evaluate(ValidLog(), Process(logCreatedBeforeProcess: true), ValidIsolation()),
                "Failed",
                "ProcessObservationInvalid");
            AssertResult(
                Evaluate(ValidLog(), Process(observationBeforeLog: true), ValidIsolation()),
                "Failed",
                "ProcessObservationInvalid");
            AssertResult(
                Evaluate(ValidLog(), Process(defaultTimestamps: true), ValidIsolation()),
                "Failed",
                "ProcessObservationInvalid");
            AssertResult(
                Evaluate(ValidLog(), Process(nonUtcTimestamps: true), ValidIsolation()),
                "Failed",
                "ProcessObservationInvalid");
        }

        [Test]
        public void NoLogTimeoutOrEarlyExitCanBeReportedWithoutInventingCreationTime()
        {
            AssertResult(
                Evaluate(string.Empty, Process(timedOut: true, logObserved: false), ValidIsolation()),
                "TimedOut",
                "TimedOut");
            AssertResult(
                Evaluate(string.Empty, Process(hasExited: true, logObserved: false), ValidIsolation()),
                "EarlyExit",
                "ProcessExitedEarly");
        }

        [Test]
        public void MissingIsolationEvidenceFailsClosed()
        {
            object result = Evaluate(ValidLog(), Process(), null);
            AssertResult(result, "Failed", "IsolationEvidenceMissing");
            Assert.IsFalse(Bool(result, "IsolationAccepted"));
        }

        [Test]
        public void SameWindowsIdentityFailsIsolationGate()
        {
            object isolation = Isolation(
                developerIdentity: "DEVBOX\\developer",
                launchIdentity: "DEVBOX\\developer");
            object result = Evaluate(ValidLog(), Process(), isolation);
            AssertResult(result, "Failed", "IsolationEvidenceInvalid");
        }

        [Test]
        public void SameDeveloperLocalLowFailsIsolationGate()
        {
            string developerLocalLow = TestLocalLow("Developer");
            object isolation = Isolation(
                launchLocalLow: developerLocalLow,
                persistentData: Path.Combine(
                    developerLocalLow,
                    StaticString("ExpectedCompanyName"),
                    StaticString("ExpectedProductName")));
            object result = Evaluate(ValidLog(), Process(), isolation);
            AssertResult(result, "Failed", "IsolationEvidenceInvalid");
        }

        [Test]
        public void PersistentDataOutsideObservedLaunchLocalLowFailsIsolationGate()
        {
            object isolation = Isolation(persistentData: Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SmokeTests",
                "Unproven",
                StaticString("ExpectedProductName")));
            object result = Evaluate(ValidLog(), Process(), isolation);
            AssertResult(result, "Failed", "IsolationEvidenceInvalid");
        }

        [Test]
        public void PersistentDataDescendantOrDifferentPlayerIdentityFailsIsolationGate()
        {
            AssertResult(
                Evaluate(
                    ValidLog(),
                    Process(),
                    Isolation(persistentData: Path.Combine(ValidPersistentData(), "Nested"))),
                "Failed",
                "IsolationEvidenceInvalid");
            AssertResult(
                Evaluate(
                    ValidLog(),
                    Process(),
                    Isolation(persistentData: Path.Combine(
                        TestLocalLow("Smoke"),
                        "OtherCompany",
                        StaticString("ExpectedProductName")))),
                "Failed",
                "IsolationEvidenceInvalid");
        }

        [Test]
        public void NormalizedExactPersistentDataPathPassesIsolationGate()
        {
            object isolation = Isolation(
                launchLocalLow: TestLocalLow("Smoke") + Path.DirectorySeparatorChar,
                persistentData: ValidPersistentData() + Path.DirectorySeparatorChar);

            AssertResult(Evaluate(ValidLog(), Process(), isolation), "Passed", "None");
        }

        [Test]
        public void UnobservedIdentityProfileOrFreshnessFailsIsolationGate()
        {
            AssertResult(Evaluate(ValidLog(), Process(), Isolation(identityObserved: false)), "Failed", "IsolationEvidenceInvalid");
            AssertResult(Evaluate(ValidLog(), Process(), Isolation(profileObserved: false)), "Failed", "IsolationEvidenceInvalid");
            AssertResult(Evaluate(ValidLog(), Process(), Isolation(noSaveArtifacts: false)), "Failed", "IsolationEvidenceInvalid");
            AssertResult(Evaluate(ValidLog(), Process(), Isolation(physicalPathsVerified: false)), "Failed", "IsolationEvidenceInvalid");
            AssertResult(Evaluate(ValidLog(), Process(), Isolation(noReparsePoints: false)), "Failed", "IsolationEvidenceInvalid");
        }

        [Test]
        public void DeveloperLocalLowPathInPlayerLogFailsEvenWithIsolationEvidence()
        {
            string log = Join(
                StaticString("ExpectedBootMarkerLine"),
                "Unexpected read " + Path.Combine(
                    TestLocalLow("Developer"),
                    StaticString("ExpectedCompanyName"),
                    StaticString("ExpectedProductName"),
                    "save.json"));
            object result = Evaluate(log, Process(), ValidIsolation());
            AssertResult(result, "Failed", "DeveloperProfileAccess");
        }

        [Test]
        public void WarningInventoryIsImmutableAndDoesNotHidePassingTransition()
        {
            const string warning =
                "[AL-ECO-PRODUCTION-DEPENDENCY] TickProduction rejected with status RejectedDependencyUnavailable.";
            string log = Join(
                ValidLog(),
                warning,
                "UnityEngine.StackTraceUtility:ExtractStackTrace ()",
                "UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])",
                "UnityEngine.Logger:Log (UnityEngine.LogType,object)",
                "UnityEngine.Debug:LogWarning (object)",
                "AL.Economy.ProductionService:TickProduction ()",
                "(Filename: Assets/AL/Scripts/Economy/ProductionService.cs Line: 42)",
                warning,
                "UnityEngine.StackTraceUtility:ExtractStackTrace ()",
                "UnityEngine.Debug:LogWarning (object)");

            object result = Evaluate(log, Process(), ValidIsolation());

            AssertResult(result, "Passed", "None");
            Assert.AreEqual(1, Convert.ToInt32(Property(result, "WarningCount")));
            Assert.That(Strings(Property(result, "WarningLines")), Does.Contain(warning));
            Assert.That(Strings(Property(result, "ReportLines")), Does.Contain("warning: " + warning));
            Assert.Throws<NotSupportedException>(() => ((IList)Property(result, "WarningLines")).Add("mutate"));
        }

        [Test]
        public void OrphanWarningStackDoesNotClaimAnEarlierSceneMarkerAsAWarning()
        {
            object result = Evaluate(
                Join(ValidLog(), string.Empty, "UnityEngine.Debug:LogWarning (object)"),
                Process(),
                ValidIsolation());

            AssertResult(result, "Passed", "None");
            Assert.AreEqual(0, Convert.ToInt32(Property(result, "WarningCount")));
        }

        private static string ValidLog()
        {
            return Join(LogThroughFreshBranch(), StaticString("ExpectedRealmSelectionMarkerLine"));
        }

        private static string LogThroughFreshBranch()
        {
            return Join(
                StaticString("ExpectedBootMarkerLine"),
                "AL Boot Sequence Started...",
                "No Realm Selected. Transitioning to Realm Selection...");
        }

        private static string Join(params string[] lines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        private static object ValidIsolation()
        {
            return Isolation();
        }

        private static object Isolation(
            string developerIdentity = "DEVBOX\\developer",
            string launchIdentity = "SANDBOX\\smoke",
            string developerLocalLow = null,
            string launchLocalLow = null,
            string persistentData = null,
            bool identityObserved = true,
            bool profileObserved = true,
            bool noSaveArtifacts = true,
            bool physicalPathsVerified = true,
            bool noReparsePoints = true)
        {
            developerLocalLow = developerLocalLow ?? TestLocalLow("Developer");
            launchLocalLow = launchLocalLow ?? TestLocalLow("Smoke");
            persistentData = persistentData ?? Path.Combine(
                launchLocalLow,
                StaticString("ExpectedCompanyName"),
                StaticString("ExpectedProductName"));

            return Activator.CreateInstance(
                Runtime(IsolationTypeName),
                "disposable Windows test account",
                developerIdentity,
                launchIdentity,
                developerLocalLow,
                launchLocalLow,
                persistentData,
                identityObserved,
                profileObserved,
                noSaveArtifacts,
                physicalPathsVerified,
                noReparsePoints);
        }

        private static string TestLocalLow(string profileName)
        {
            return Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SmokeTests",
                profileName,
                "AppData",
                "LocalLow"));
        }

        private static string ValidPersistentData()
        {
            return Path.Combine(
                TestLocalLow("Smoke"),
                StaticString("ExpectedCompanyName"),
                StaticString("ExpectedProductName"));
        }

        private static object Process(
            bool hasExited = false,
            bool timedOut = false,
            bool terminatedExternally = false,
            int processId = 4242,
            bool logWasAbsentBeforeLaunch = true,
            bool logCreatedBeforeProcess = false,
            bool observationBeforeLog = false,
            bool defaultTimestamps = false,
            bool nonUtcTimestamps = false,
            bool logObserved = true)
        {
            DateTime processStartedAtUtc = new DateTime(2026, 7, 22, 1, 2, 3, DateTimeKind.Utc);
            DateTime logCreatedAtUtc = logObserved
                ? processStartedAtUtc.AddSeconds(logCreatedBeforeProcess ? -1 : 1)
                : default(DateTime);
            DateTime observedAtUtc = logObserved
                ? logCreatedAtUtc.AddSeconds(observationBeforeLog ? -1 : 5)
                : processStartedAtUtc.AddSeconds(5);
            if (defaultTimestamps)
            {
                processStartedAtUtc = default(DateTime);
                logCreatedAtUtc = default(DateTime);
                observedAtUtc = default(DateTime);
            }
            else if (nonUtcTimestamps)
            {
                processStartedAtUtc = DateTime.SpecifyKind(processStartedAtUtc, DateTimeKind.Unspecified);
                logCreatedAtUtc = DateTime.SpecifyKind(logCreatedAtUtc, DateTimeKind.Unspecified);
                observedAtUtc = DateTime.SpecifyKind(observedAtUtc, DateTimeKind.Unspecified);
            }

            return Activator.CreateInstance(
                Runtime(ProcessTypeName),
                hasExited,
                timedOut,
                terminatedExternally,
                processId,
                processStartedAtUtc,
                logCreatedAtUtc,
                observedAtUtc,
                logObserved,
                logWasAbsentBeforeLaunch);
        }

        private static object Evaluate(string log, object process, object isolation)
        {
            MethodInfo method = Runtime(SmokeTypeName).GetMethod(
                "Evaluate",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            return method.Invoke(null, new[] { log, process, isolation });
        }

        private static void AssertResult(object result, string status, string failure)
        {
            Assert.AreEqual(status, Property(result, "Status").ToString());
            Assert.AreEqual(failure, Property(result, "Failure").ToString());
        }

        private static string StaticString(string propertyName)
        {
            PropertyInfo property = Runtime(SmokeTypeName).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            if (property != null)
            {
                return (string)property.GetValue(null);
            }

            FieldInfo field = Runtime(SmokeTypeName).GetField(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(field);
            return (string)field.GetValue(null);
        }

        private static object Property(object target, string name)
        {
            Assert.NotNull(target);
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property, $"{target.GetType().Name}.{name} not found");
            return property.GetValue(target);
        }

        private static bool Bool(object target, string name)
        {
            return (bool)Property(target, name);
        }

        private static string String(object target, string name)
        {
            return (string)Property(target, name);
        }

        private static IReadOnlyList<string> Strings(object enumerable)
        {
            return ((IEnumerable)enumerable).Cast<object>().Select(value => value?.ToString() ?? string.Empty).ToList();
        }

        private static Type Runtime(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Expected type {fullName} in a loaded assembly.");
            return null;
        }

    }
}
