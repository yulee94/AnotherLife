#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using AL.Core.Scenes;

namespace AL.EditorTools
{
    /// <summary>Terminal and non-terminal outcomes for the #150 Player launch smoke.</summary>
    public enum ProductionPlayerLaunchSmokeStatus
    {
        Running,
        Passed,
        Failed,
        TimedOut,
        EarlyExit
    }

    /// <summary>Stable machine-readable reason for a non-passing launch-smoke result.</summary>
    public enum ProductionPlayerLaunchSmokeFailure
    {
        None,
        IsolationEvidenceMissing,
        IsolationEvidenceInvalid,
        ProcessObservationInvalid,
        DeveloperProfileAccess,
        MarkerMismatch,
        MarkerOrderInvalid,
        UnexpectedSceneMarker,
        SceneLoadFailure,
        BootloaderFailure,
        SevereException,
        MissingScriptOrSerialization,
        TimedOut,
        ProcessExitedEarly,
        ExternallyTerminatedEarly
    }

    /// <summary>
    /// Caller-observed process state. This type deliberately does not launch or terminate a process;
    /// an external harness owns the Windows process and supplies observations to the pure evaluator.
    /// </summary>
    public sealed class ProductionPlayerProcessObservation
    {
        public ProductionPlayerProcessObservation(
            bool hasExited,
            bool timedOut,
            bool terminatedExternally,
            int processId,
            DateTime processStartedAtUtc,
            DateTime logCreatedAtUtc,
            DateTime observedAtUtc,
            bool logObserved,
            bool logWasAbsentBeforeLaunch)
        {
            HasExited = hasExited;
            TimedOut = timedOut;
            TerminatedExternally = terminatedExternally;
            ProcessId = processId;
            ProcessStartedAtUtc = processStartedAtUtc;
            LogCreatedAtUtc = logCreatedAtUtc;
            ObservedAtUtc = observedAtUtc;
            LogObserved = logObserved;
            LogWasAbsentBeforeLaunch = logWasAbsentBeforeLaunch;
        }

        public bool HasExited { get; }
        public bool TimedOut { get; }
        public bool TerminatedExternally { get; }
        public int ProcessId { get; }
        public DateTime ProcessStartedAtUtc { get; }
        public DateTime LogCreatedAtUtc { get; }
        public DateTime ObservedAtUtc { get; }
        public bool LogObserved { get; }
        public bool LogWasAbsentBeforeLaunch { get; }
    }

    /// <summary>
    /// Observed, caller-supplied isolation evidence. Environment-variable overrides and undocumented
    /// Player arguments are intentionally not represented: #150 accepts only a separately observed
    /// Windows identity/profile whose LocalLow and Player data path cannot resolve to the developer's.
    /// </summary>
    public sealed class ProductionPlayerIsolationEvidence
    {
        public ProductionPlayerIsolationEvidence(
            string isolationMethod,
            string developerWindowsIdentity,
            string launchWindowsIdentity,
            string developerLocalLowPath,
            string launchLocalLowPath,
            string launchPersistentDataPath,
            bool launchIdentityObserved,
            bool launchProfileObserved,
            bool noAnotherLifeSaveArtifactsObservedBeforeLaunch,
            bool physicalProfilePathsVerifiedDistinct,
            bool launchProfileChainHasNoReparsePoints)
        {
            IsolationMethod = isolationMethod ?? string.Empty;
            DeveloperWindowsIdentity = developerWindowsIdentity ?? string.Empty;
            LaunchWindowsIdentity = launchWindowsIdentity ?? string.Empty;
            DeveloperLocalLowPath = developerLocalLowPath ?? string.Empty;
            LaunchLocalLowPath = launchLocalLowPath ?? string.Empty;
            LaunchPersistentDataPath = launchPersistentDataPath ?? string.Empty;
            LaunchIdentityObserved = launchIdentityObserved;
            LaunchProfileObserved = launchProfileObserved;
            NoAnotherLifeSaveArtifactsObservedBeforeLaunch = noAnotherLifeSaveArtifactsObservedBeforeLaunch;
            PhysicalProfilePathsVerifiedDistinct = physicalProfilePathsVerifiedDistinct;
            LaunchProfileChainHasNoReparsePoints = launchProfileChainHasNoReparsePoints;
        }

        public string IsolationMethod { get; }
        public string DeveloperWindowsIdentity { get; }
        public string LaunchWindowsIdentity { get; }
        public string DeveloperLocalLowPath { get; }
        public string LaunchLocalLowPath { get; }
        public string LaunchPersistentDataPath { get; }
        public bool LaunchIdentityObserved { get; }
        public bool LaunchProfileObserved { get; }
        public bool NoAnotherLifeSaveArtifactsObservedBeforeLaunch { get; }
        public bool PhysicalProfilePathsVerifiedDistinct { get; }
        public bool LaunchProfileChainHasNoReparsePoints { get; }
    }

    /// <summary>Immutable evidence result returned by <see cref="ProductionPlayerLaunchSmoke"/>.</summary>
    public sealed class ProductionPlayerLaunchSmokeResult
    {
        internal ProductionPlayerLaunchSmokeResult(
            ProductionPlayerLaunchSmokeStatus status,
            ProductionPlayerLaunchSmokeFailure failure,
            string summary,
            string diagnostic,
            bool isolationAccepted,
            bool bootMarkerObserved,
            bool bootSequenceStartedObserved,
            bool freshProfileBranchObserved,
            bool realmSelectionMarkerObserved,
            ProductionPlayerProcessObservation process,
            IEnumerable<string> warningLines = null)
        {
            Status = status;
            Failure = failure;
            Summary = summary ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
            IsolationAccepted = isolationAccepted;
            BootMarkerObserved = bootMarkerObserved;
            BootSequenceStartedObserved = bootSequenceStartedObserved;
            FreshProfileBranchObserved = freshProfileBranchObserved;
            RealmSelectionMarkerObserved = realmSelectionMarkerObserved;
            ProcessExited = process != null && process.HasExited;
            ExternalTerminationReported = process != null && process.TerminatedExternally;
            ProcessId = process?.ProcessId ?? 0;
            ProcessStartedAtUtc = process?.ProcessStartedAtUtc ?? default(DateTime);
            LogCreatedAtUtc = process?.LogCreatedAtUtc ?? default(DateTime);
            ObservedAtUtc = process?.ObservedAtUtc ?? default(DateTime);
            LogObserved = process != null && process.LogObserved;
            LogWasAbsentBeforeLaunch = process != null && process.LogWasAbsentBeforeLaunch;
            TransitionPassed = status == ProductionPlayerLaunchSmokeStatus.Passed;
            ClaimsGracefulQuitOrSave = false;
            WarningLines = new ReadOnlyCollection<string>((warningLines ?? Array.Empty<string>())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .ToList());
            ReportLines = BuildReportLines(status, summary, process, WarningLines);
            _process = process;
        }

        public ProductionPlayerLaunchSmokeStatus Status { get; }
        public ProductionPlayerLaunchSmokeFailure Failure { get; }
        public string Summary { get; }
        public string Diagnostic { get; }
        public bool IsolationAccepted { get; }
        public bool BootMarkerObserved { get; }
        public bool BootSequenceStartedObserved { get; }
        public bool FreshProfileBranchObserved { get; }
        public bool RealmSelectionMarkerObserved { get; }
        public bool TransitionPassed { get; }
        public bool ProcessExited { get; }
        public bool ExternalTerminationReported { get; }
        public int ProcessId { get; }
        public DateTime ProcessStartedAtUtc { get; }
        public DateTime LogCreatedAtUtc { get; }
        public DateTime ObservedAtUtc { get; }
        public bool LogObserved { get; }
        public bool LogWasAbsentBeforeLaunch { get; }
        public int WarningCount => WarningLines.Count;
        public IReadOnlyList<string> WarningLines { get; }

        /// <summary>
        /// Always false. An externally terminated launch smoke cannot establish graceful quit or save.
        /// </summary>
        public bool ClaimsGracefulQuitOrSave { get; }

        public IReadOnlyList<string> ReportLines { get; }

        private readonly ProductionPlayerProcessObservation _process;

        internal ProductionPlayerLaunchSmokeResult WithWarnings(IEnumerable<string> warningLines)
        {
            return new ProductionPlayerLaunchSmokeResult(
                Status,
                Failure,
                Summary,
                Diagnostic,
                IsolationAccepted,
                BootMarkerObserved,
                BootSequenceStartedObserved,
                FreshProfileBranchObserved,
                RealmSelectionMarkerObserved,
                _process,
                warningLines);
        }

        private static IReadOnlyList<string> BuildReportLines(
            ProductionPlayerLaunchSmokeStatus status,
            string summary,
            ProductionPlayerProcessObservation process,
            IReadOnlyList<string> warningLines)
        {
            var lines = new List<string>();
            if (status == ProductionPlayerLaunchSmokeStatus.Passed)
            {
                lines.Add("transition passed");
                if (process != null && process.TerminatedExternally)
                {
                    lines.Add("process terminated externally for validation");
                }
                else if (process != null && process.HasExited)
                {
                    lines.Add("process exited after transition; termination was not reported as external");
                }
                else
                {
                    lines.Add("process remains running; external validation termination not yet reported");
                }

                lines.Add("isolated profile may contain disposable test artifacts");
            }
            else
            {
                lines.Add(summary ?? string.Empty);
            }

            foreach (string warning in warningLines ?? Array.Empty<string>())
            {
                lines.Add("warning: " + warning);
            }

            if (process != null)
            {
                lines.Add("process id: " + process.ProcessId.ToString(CultureInfo.InvariantCulture));
                lines.Add("process started UTC: " + process.ProcessStartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                lines.Add("log observed: " + process.LogObserved.ToString());
                if (process.LogObserved)
                {
                    lines.Add("log created UTC: " + process.LogCreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                }
                lines.Add("observed UTC: " + process.ObservedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                lines.Add("log absent before launch: " + process.LogWasAbsentBeforeLaunch.ToString());
            }

            lines.Add("no graceful quit/save claim");
            return new ReadOnlyCollection<string>(lines);
        }
    }

    /// <summary>
    /// Pure, deterministic evaluator for #150's isolated Windows Player log/process evidence. It does
    /// not read files, inspect environment variables, launch a Player, terminate a process, or mutate
    /// Unity state. Callers must independently observe isolation and process state, then supply the
    /// complete log snapshot. Every unsupported or contradictory condition fails closed.
    /// </summary>
    public static class ProductionPlayerLaunchSmoke
    {
        public const string ExpectedCompanyName = "DefaultCompany";
        public const string ExpectedProductName = "AnotherLifeUnity";
        public const string BootSequenceStarted = "AL Boot Sequence Started...";
        public const string FreshProfileBranch = "No Realm Selected. Transitioning to Realm Selection...";

        private const string ActiveMarkerPrefix = "[AL-SCENE-ACTIVE]";
        private const string MarkerMismatchToken = "[AL-SCENE-ACTIVE-MISMATCH]";

        private static readonly string ExpectedBootMarker = BuildMarker(ProductionSceneDescriptor.ShellFoundationOrdered[0]);
        private static readonly string ExpectedRealmSelectionMarker = BuildMarker(ProductionSceneDescriptor.ShellFoundationOrdered[1]);

        private static readonly string[] SceneLoadFailureTokens =
        {
            "has not been added to the build settings",
            "hasn't been added to the build settings",
            "couldn't be loaded",
            "could not be loaded because it has not been added",
            "Failed to load scene",
            "not a valid scene"
        };

        private static readonly string[] BootloaderFailureTokens =
        {
            "[BOOT_STACK_LOAD_FAILED]",
            "[BOOT_STACK_PUBLICATION_FAILED]",
            "[BOOT_STACK_PARTIAL_REGISTRY]",
            "[BOOT_STACK_MARKER_INCONSISTENT]",
            "[BOOT_STACK_RUNTIME_OWNER_REJECTED]",
            "[BOOT_STACK_RUNTIME_DRIFT]",
            "[BOOT_STACK_LOAD_IN_PROGRESS]",
            "Bootloader initialization failed",
            "Bootloader save load failed"
        };

        private static readonly string[] SevereExceptionTokens =
        {
            "Exception:",
            "ArgumentException:",
            "MissingReferenceException:",
            "MissingMethodException:",
            "NullReferenceException:",
            "AssertionException:",
            "Unhandled Exception",
            "Unhandled exception",
            "Assertion failed",
            "Assert failed",
            "[ASSERT]"
        };

        private static readonly string[] MissingScriptOrSerializationTokens =
        {
            "The referenced script",
            "The referenced script on this Behaviour is missing",
            "Missing Mono Script",
            "SerializationException:",
            "Failed to deserialize",
            "Error while deserializing",
            "A scripted object (probably",
            "Could not produce class with ID"
        };

        public static string ExpectedBootMarkerLine => ExpectedBootMarker;
        public static string ExpectedRealmSelectionMarkerLine => ExpectedRealmSelectionMarker;

        public static ProductionPlayerLaunchSmokeResult Evaluate(
            string playerLog,
            ProductionPlayerProcessObservation process,
            ProductionPlayerIsolationEvidence isolation)
        {
            return EvaluateCore(playerLog, process, isolation).WithWarnings(CollectWarnings(playerLog));
        }

        /// <summary>
        /// Evaluates a complete log snapshot and the caller-observed process/isolation state.
        /// Incomplete evidence is Running only while the process remains live and within its deadline.
        /// </summary>
        private static ProductionPlayerLaunchSmokeResult EvaluateCore(
            string playerLog,
            ProductionPlayerProcessObservation process,
            ProductionPlayerIsolationEvidence isolation)
        {
            if (process == null)
            {
                return Result(
                    ProductionPlayerLaunchSmokeStatus.Failed,
                    ProductionPlayerLaunchSmokeFailure.ProcessObservationInvalid,
                    "launch smoke failed: process observation is required",
                    "process observation was null",
                    false,
                    0,
                    null);
            }

            if (process.TerminatedExternally && !process.HasExited)
            {
                return Result(
                    ProductionPlayerLaunchSmokeStatus.Failed,
                    ProductionPlayerLaunchSmokeFailure.ProcessObservationInvalid,
                    "launch smoke failed: contradictory process observation",
                    "external termination was reported while the process was still observed running",
                    false,
                    0,
                    process);
            }

            if (!TryValidateProcessObservation(process, out string processFailure))
            {
                return Result(
                    ProductionPlayerLaunchSmokeStatus.Failed,
                    ProductionPlayerLaunchSmokeFailure.ProcessObservationInvalid,
                    "launch smoke failed: process/log run-boundary evidence was not accepted",
                    processFailure,
                    false,
                    0,
                    process);
            }

            if (!TryValidateIsolation(isolation, out string isolationFailure))
            {
                ProductionPlayerLaunchSmokeFailure code = isolation == null
                    ? ProductionPlayerLaunchSmokeFailure.IsolationEvidenceMissing
                    : ProductionPlayerLaunchSmokeFailure.IsolationEvidenceInvalid;
                return Result(
                    ProductionPlayerLaunchSmokeStatus.Failed,
                    code,
                    "launch smoke failed: isolated Windows profile evidence was not accepted",
                    isolationFailure,
                    false,
                    0,
                    process);
            }

            int sequenceState = 0;
            using (var reader = new StringReader(playerLog ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (ContainsDeveloperProfilePath(line, isolation.DeveloperLocalLowPath))
                    {
                        return Result(
                            ProductionPlayerLaunchSmokeStatus.Failed,
                            ProductionPlayerLaunchSmokeFailure.DeveloperProfileAccess,
                            "launch smoke failed: developer profile access was observed",
                            Truncate(line),
                            true,
                            sequenceState,
                            process);
                    }

                    if (line.IndexOf(MarkerMismatchToken, StringComparison.Ordinal) >= 0)
                    {
                        return Result(
                            ProductionPlayerLaunchSmokeStatus.Failed,
                            ProductionPlayerLaunchSmokeFailure.MarkerMismatch,
                            "launch smoke failed: production scene marker reported a path/name mismatch",
                            Truncate(line),
                            true,
                            sequenceState,
                            process);
                    }

                    if (ContainsAny(line, SceneLoadFailureTokens))
                    {
                        return Result(
                            ProductionPlayerLaunchSmokeStatus.Failed,
                            ProductionPlayerLaunchSmokeFailure.SceneLoadFailure,
                            "launch smoke failed: a scene was missing or could not be loaded",
                            Truncate(line),
                            true,
                            sequenceState,
                            process);
                    }

                    if (ContainsAny(line, BootloaderFailureTokens))
                    {
                        return Result(
                            ProductionPlayerLaunchSmokeStatus.Failed,
                            ProductionPlayerLaunchSmokeFailure.BootloaderFailure,
                            "launch smoke failed: Bootloader initialization/load integrity failed",
                            Truncate(line),
                            true,
                            sequenceState,
                            process);
                    }

                    if (ContainsAny(line, MissingScriptOrSerializationTokens))
                    {
                        return Result(
                            ProductionPlayerLaunchSmokeStatus.Failed,
                            ProductionPlayerLaunchSmokeFailure.MissingScriptOrSerialization,
                            "launch smoke failed: missing-script or serialization evidence was observed",
                            Truncate(line),
                            true,
                            sequenceState,
                            process);
                    }

                    if (ContainsAny(line, SevereExceptionTokens))
                    {
                        return Result(
                            ProductionPlayerLaunchSmokeStatus.Failed,
                            ProductionPlayerLaunchSmokeFailure.SevereException,
                            "launch smoke failed: severe exception/assert evidence was observed",
                            Truncate(line),
                            true,
                            sequenceState,
                            process);
                    }

                    int markerIndex = line.IndexOf(ActiveMarkerPrefix, StringComparison.Ordinal);
                    if (markerIndex >= 0)
                    {
                        string marker = line.Substring(markerIndex).Trim();
                        if (string.Equals(marker, ExpectedBootMarker, StringComparison.Ordinal))
                        {
                            if (sequenceState != 0)
                            {
                                return OrderedFailure("Boot marker was duplicated or appeared out of order", line, sequenceState, process);
                            }

                            sequenceState = 1;
                            continue;
                        }

                        if (string.Equals(marker, ExpectedRealmSelectionMarker, StringComparison.Ordinal))
                        {
                            if (sequenceState != 3)
                            {
                                return OrderedFailure("RealmSelection marker appeared before the complete Boot fresh-profile sequence", line, sequenceState, process);
                            }

                            sequenceState = 4;
                            continue;
                        }

                        string sceneId = ReadMarkerField(marker, "id");
                        ProductionPlayerLaunchSmokeFailure markerFailure =
                            IsKnownProhibitedScene(sceneId)
                                ? ProductionPlayerLaunchSmokeFailure.UnexpectedSceneMarker
                                : ProductionPlayerLaunchSmokeFailure.MarkerMismatch;
                        string markerSummary = markerFailure == ProductionPlayerLaunchSmokeFailure.UnexpectedSceneMarker
                            ? "launch smoke failed: Kingdom/Test/Champion or another wrong scene marker was observed"
                            : "launch smoke failed: a Boot/RealmSelection marker did not exactly match the production descriptor";
                        return Result(
                            ProductionPlayerLaunchSmokeStatus.Failed,
                            markerFailure,
                            markerSummary,
                            Truncate(line),
                            true,
                            sequenceState,
                            process);
                    }

                    if (line.IndexOf(BootSequenceStarted, StringComparison.Ordinal) >= 0)
                    {
                        if (sequenceState != 1)
                        {
                            return OrderedFailure("Boot sequence-start log was duplicated or appeared out of order", line, sequenceState, process);
                        }

                        sequenceState = 2;
                        continue;
                    }

                    if (line.IndexOf(FreshProfileBranch, StringComparison.Ordinal) >= 0)
                    {
                        if (sequenceState != 2)
                        {
                            return OrderedFailure("fresh-profile branch log was duplicated or appeared out of order", line, sequenceState, process);
                        }

                        sequenceState = 3;
                    }
                }
            }

            if (process.TimedOut)
            {
                return Result(
                    ProductionPlayerLaunchSmokeStatus.TimedOut,
                    ProductionPlayerLaunchSmokeFailure.TimedOut,
                    "launch smoke timed out; transition evidence cannot be accepted after the deadline",
                    sequenceState == 4 ? "timeout was reported despite a complete marker sequence" : MissingEvidence(sequenceState),
                    true,
                    sequenceState,
                    process);
            }

            if (sequenceState == 4)
            {
                if (process.HasExited && !process.TerminatedExternally)
                {
                    return Result(
                        ProductionPlayerLaunchSmokeStatus.EarlyExit,
                        ProductionPlayerLaunchSmokeFailure.ProcessExitedEarly,
                        "launch smoke process exited without the required external-validation termination attribution",
                        "transition markers were complete, but the process exit was not reported as externally controlled",
                        true,
                        sequenceState,
                        process);
                }

                return Result(
                    ProductionPlayerLaunchSmokeStatus.Passed,
                    ProductionPlayerLaunchSmokeFailure.None,
                    "transition passed",
                    string.Empty,
                    true,
                    sequenceState,
                    process);
            }

            if (process.HasExited)
            {
                ProductionPlayerLaunchSmokeFailure failure = process.TerminatedExternally
                    ? ProductionPlayerLaunchSmokeFailure.ExternallyTerminatedEarly
                    : ProductionPlayerLaunchSmokeFailure.ProcessExitedEarly;
                return Result(
                    ProductionPlayerLaunchSmokeStatus.EarlyExit,
                    failure,
                    "launch smoke process exited before the complete Boot to RealmSelection sequence",
                    MissingEvidence(sequenceState),
                    true,
                    sequenceState,
                    process);
            }

            return Result(
                ProductionPlayerLaunchSmokeStatus.Running,
                ProductionPlayerLaunchSmokeFailure.None,
                "launch smoke is still running and awaiting required ordered evidence",
                MissingEvidence(sequenceState),
                true,
                sequenceState,
                process);
        }

        private static bool TryValidateProcessObservation(
            ProductionPlayerProcessObservation process,
            out string failure)
        {
            if (process.ProcessId <= 0)
            {
                failure = "a positive launched Player process id is required";
                return false;
            }

            if (!process.LogWasAbsentBeforeLaunch)
            {
                failure = "the Player log was not observed absent before process launch";
                return false;
            }

            if (process.ProcessStartedAtUtc.Kind != DateTimeKind.Utc ||
                process.ObservedAtUtc.Kind != DateTimeKind.Utc ||
                process.ProcessStartedAtUtc == default(DateTime) ||
                process.ObservedAtUtc == default(DateTime))
            {
                failure = "process start and observation timestamps must be non-default UTC values";
                return false;
            }

            if (process.ObservedAtUtc < process.ProcessStartedAtUtc)
            {
                failure = "process observation predates the launched process";
                return false;
            }

            if (!process.LogObserved)
            {
                if (process.LogCreatedAtUtc != default(DateTime))
                {
                    failure = "a log creation timestamp was supplied although no Player log was observed";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            if (process.LogCreatedAtUtc.Kind != DateTimeKind.Utc ||
                process.LogCreatedAtUtc == default(DateTime))
            {
                failure = "an observed Player log requires a non-default UTC creation timestamp";
                return false;
            }

            if (process.LogCreatedAtUtc < process.ProcessStartedAtUtc)
            {
                failure = "Player log creation predates the launched process";
                return false;
            }

            if (process.ObservedAtUtc < process.LogCreatedAtUtc)
            {
                failure = "process observation predates Player log creation";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryValidateIsolation(ProductionPlayerIsolationEvidence isolation, out string failure)
        {
            if (isolation == null)
            {
                failure = "isolation evidence was null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(isolation.IsolationMethod))
            {
                failure = "isolation method was not recorded";
                return false;
            }

            if (!isolation.LaunchIdentityObserved || !isolation.LaunchProfileObserved)
            {
                failure = "launch identity and profile must both be observed in the isolated execution context";
                return false;
            }

            if (string.IsNullOrWhiteSpace(isolation.DeveloperWindowsIdentity) ||
                string.IsNullOrWhiteSpace(isolation.LaunchWindowsIdentity))
            {
                failure = "developer and launch Windows identities are required";
                return false;
            }

            if (string.Equals(
                    isolation.DeveloperWindowsIdentity.Trim(),
                    isolation.LaunchWindowsIdentity.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                failure = "launch Windows identity equals the developer Windows identity";
                return false;
            }

            if (!TryCanonicalAbsolutePath(isolation.DeveloperLocalLowPath, out string developerLocalLow) ||
                !TryCanonicalAbsolutePath(isolation.LaunchLocalLowPath, out string launchLocalLow) ||
                !TryCanonicalAbsolutePath(isolation.LaunchPersistentDataPath, out string launchPersistentData))
            {
                failure = "developer LocalLow, launch LocalLow, and launch persistent-data paths must be absolute and valid";
                return false;
            }

            if (string.Equals(developerLocalLow, launchLocalLow, StringComparison.OrdinalIgnoreCase))
            {
                failure = "launch LocalLow path equals the developer LocalLow path";
                return false;
            }

            if (!string.Equals(WindowsFileName(developerLocalLow), "LocalLow", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(WindowsFileName(launchLocalLow), "LocalLow", StringComparison.OrdinalIgnoreCase))
            {
                failure = "developer and launch profile paths must resolve to observed LocalLow directories";
                return false;
            }

            if (!IsPathInside(launchPersistentData, launchLocalLow))
            {
                failure = "launch persistent-data path is not beneath the observed isolated LocalLow path";
                return false;
            }

            string expectedPersistentData = CombineWindowsPath(
                launchLocalLow,
                ExpectedCompanyName,
                ExpectedProductName);
            if (!string.Equals(
                    launchPersistentData,
                    expectedPersistentData,
                    StringComparison.OrdinalIgnoreCase))
            {
                failure = "launch persistent-data path does not match the unchanged Player company/product identity";
                return false;
            }

            if (IsPathInside(launchPersistentData, developerLocalLow))
            {
                failure = "launch persistent-data path resolves beneath the developer LocalLow path";
                return false;
            }

            if (!isolation.NoAnotherLifeSaveArtifactsObservedBeforeLaunch)
            {
                failure = "absence of pre-existing AnotherLife save artifacts was not observed before launch";
                return false;
            }

            if (!isolation.PhysicalProfilePathsVerifiedDistinct)
            {
                failure = "developer and launch profile paths were not verified as physically distinct";
                return false;
            }

            if (!isolation.LaunchProfileChainHasNoReparsePoints)
            {
                failure = "launch LocalLow/persistent-data chain contains or may contain a reparse point";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static IReadOnlyList<string> CollectWarnings(string playerLog)
        {
            string[] lines = (playerLog ?? string.Empty)
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            var warnings = new List<string>();
            for (int index = 0; index < lines.Length; index++)
            {
                string line = (lines[index] ?? string.Empty).Trim();
                if (line.IndexOf("Debug:LogWarning", StringComparison.Ordinal) >= 0)
                {
                    for (int prior = index - 1; prior >= 0; prior--)
                    {
                        string candidate = (lines[prior] ?? string.Empty).Trim();
                        if (candidate.Length == 0)
                        {
                            break;
                        }

                        if (IsWarningStackLine(candidate))
                        {
                            continue;
                        }

                        if (candidate.Length > 0)
                        {
                            warnings.Add(Truncate(candidate));
                        }

                        break;
                    }
                }
                else if (line.IndexOf("warning:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         line.StartsWith("[Warning]", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(Truncate(line));
                }
            }

            return new ReadOnlyCollection<string>(warnings
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .ToList());
        }

        private static bool IsWarningStackLine(string line)
        {
            return line.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                   line.StartsWith("AL.", StringComparison.Ordinal) ||
                   line.StartsWith("(Filename:", StringComparison.Ordinal) ||
                   line.StartsWith("at ", StringComparison.Ordinal);
        }

        private static ProductionPlayerLaunchSmokeResult OrderedFailure(
            string reason,
            string line,
            int sequenceState,
            ProductionPlayerProcessObservation process)
        {
            return Result(
                ProductionPlayerLaunchSmokeStatus.Failed,
                ProductionPlayerLaunchSmokeFailure.MarkerOrderInvalid,
                "launch smoke failed: required fresh-profile evidence was out of order",
                reason + ": " + Truncate(line),
                true,
                sequenceState,
                process);
        }

        private static ProductionPlayerLaunchSmokeResult Result(
            ProductionPlayerLaunchSmokeStatus status,
            ProductionPlayerLaunchSmokeFailure failure,
            string summary,
            string diagnostic,
            bool isolationAccepted,
            int sequenceState,
            ProductionPlayerProcessObservation process)
        {
            return new ProductionPlayerLaunchSmokeResult(
                status,
                failure,
                summary,
                diagnostic,
                isolationAccepted,
                sequenceState >= 1,
                sequenceState >= 2,
                sequenceState >= 3,
                sequenceState >= 4,
                process);
        }

        private static string BuildMarker(ProductionSceneRecord scene)
        {
            return ActiveMarkerPrefix +
                   " id=" + scene.SceneId +
                   " name=" + scene.SceneName +
                   " path=" + scene.AssetPath +
                   " role=" + scene.Role +
                   " version=" + ProductionSceneDescriptor.SourceVersion;
        }

        private static bool ContainsAny(string line, IReadOnlyList<string> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (line.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDeveloperProfilePath(string line, string developerLocalLowPath)
        {
            if (!TryCanonicalAbsolutePath(developerLocalLowPath, out string canonical))
            {
                return true;
            }

            if (line.IndexOf(canonical, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string alternate = canonical.Replace('\\', '/');
            return line.IndexOf(alternate, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsKnownProhibitedScene(string sceneId)
        {
            return string.Equals(sceneId, ProductionSceneDescriptor.KingdomSceneId, StringComparison.Ordinal) ||
                   string.Equals(sceneId, ProductionSceneDescriptor.ChampionArenaSceneId, StringComparison.Ordinal) ||
                   string.Equals(sceneId, ProductionSceneDescriptor.TestSceneId, StringComparison.Ordinal) ||
                   (!string.Equals(sceneId, ProductionSceneDescriptor.BootSceneId, StringComparison.Ordinal) &&
                    !string.Equals(sceneId, ProductionSceneDescriptor.RealmSelectionSceneId, StringComparison.Ordinal));
        }

        private static string ReadMarkerField(string marker, string fieldName)
        {
            string token = fieldName + "=";
            int start = marker.IndexOf(token, StringComparison.Ordinal);
            if (start < 0)
            {
                return string.Empty;
            }

            start += token.Length;
            int end = marker.IndexOf(' ', start);
            return end < 0 ? marker.Substring(start) : marker.Substring(start, end - start);
        }

        private static bool TryCanonicalAbsolutePath(string value, out string canonical)
        {
            canonical = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().Replace('/', '\\');
            if (normalized.IndexOf('\0') >= 0)
            {
                return false;
            }

            string root;
            int segmentStart;
            if (normalized.Length >= 3 &&
                char.IsLetter(normalized[0]) &&
                normalized[1] == ':' &&
                normalized[2] == '\\')
            {
                root = char.ToUpperInvariant(normalized[0]) + ":\\";
                segmentStart = 3;
            }
            else if (normalized.StartsWith("\\\\", StringComparison.Ordinal))
            {
                string[] uncParts = normalized.Substring(2)
                    .Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (uncParts.Length < 2 ||
                    !IsValidWindowsPathSegment(uncParts[0]) ||
                    !IsValidWindowsPathSegment(uncParts[1]))
                {
                    return false;
                }

                root = "\\\\" + uncParts[0] + "\\" + uncParts[1] + "\\";
                int serverEnd = normalized.IndexOf('\\', 2);
                int shareEnd = serverEnd < 0
                    ? -1
                    : normalized.IndexOf('\\', serverEnd + 1);
                segmentStart = shareEnd < 0 ? normalized.Length : shareEnd + 1;
            }
            else
            {
                return false;
            }

            var segments = new List<string>();
            string remainder = segmentStart >= normalized.Length
                ? string.Empty
                : normalized.Substring(segmentStart);
            string[] candidates = remainder.Split(
                new[] { '\\' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < candidates.Length; index++)
            {
                string segment = candidates[index];
                if (segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count == 0)
                    {
                        return false;
                    }

                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }

                if (!IsValidWindowsPathSegment(segment))
                {
                    return false;
                }

                segments.Add(segment);
            }

            canonical = segments.Count == 0
                ? root
                : root + string.Join("\\", segments);
            return true;
        }

        private static bool IsPathInside(string candidate, string root)
        {
            if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rootWithSeparator = root.EndsWith("\\", StringComparison.Ordinal)
                ? root
                : root + "\\";
            return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static string WindowsFileName(string canonicalPath)
        {
            if (string.IsNullOrEmpty(canonicalPath))
            {
                return string.Empty;
            }

            string withoutTrailingSeparator = canonicalPath.TrimEnd('\\');
            int separator = withoutTrailingSeparator.LastIndexOf('\\');
            return separator < 0
                ? withoutTrailingSeparator
                : withoutTrailingSeparator.Substring(separator + 1);
        }

        private static string CombineWindowsPath(string root, params string[] segments)
        {
            string combined = root.EndsWith("\\", StringComparison.Ordinal)
                ? root
                : root + "\\";
            return combined + string.Join("\\", segments);
        }

        private static bool IsValidWindowsPathSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment) ||
                segment.EndsWith(" ", StringComparison.Ordinal) ||
                segment.EndsWith(".", StringComparison.Ordinal))
            {
                return false;
            }

            const string invalid = "<>:\"|?*";
            for (int index = 0; index < segment.Length; index++)
            {
                char character = segment[index];
                if (character < 32 || invalid.IndexOf(character) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string MissingEvidence(int sequenceState)
        {
            switch (sequenceState)
            {
                case 0:
                    return "missing Boot scene marker";
                case 1:
                    return "missing Boot sequence-start log";
                case 2:
                    return "missing fresh-profile branch log";
                case 3:
                    return "missing RealmSelection scene marker";
                default:
                    return string.Empty;
            }
        }

        private static string Truncate(string value)
        {
            const int maxLength = 512;
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength) + "...";
        }
    }
}
#endif
