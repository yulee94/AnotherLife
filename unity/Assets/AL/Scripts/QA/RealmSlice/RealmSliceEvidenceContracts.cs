using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AL.QA.RealmSlice
{
    public sealed class RealmSliceEvidenceRequest
    {
        public RealmSliceEvidenceRequest(
            string candidateId,
            string evidencePacketId,
            string realm,
            string mode,
            string checkId,
            string scenarioId,
            string scenarioVersion,
            string scenarioCatalogSha256,
            string scenarioDefinitionSha256,
            long seed,
            string logicalClockUtc,
            string locale,
            string inputClass,
            string accessibilityPreset,
            string evidenceOutputRoot,
            string logFile)
        {
            CandidateId = candidateId;
            EvidencePacketId = evidencePacketId;
            Realm = realm;
            Mode = mode;
            CheckId = checkId;
            ScenarioId = scenarioId;
            ScenarioVersion = scenarioVersion;
            ScenarioCatalogSha256 = scenarioCatalogSha256;
            ScenarioDefinitionSha256 = scenarioDefinitionSha256;
            Seed = seed;
            LogicalClockUtc = logicalClockUtc;
            Locale = locale;
            InputClass = inputClass;
            AccessibilityPreset = accessibilityPreset;
            EvidenceOutputRoot = evidenceOutputRoot;
            LogFile = logFile ?? string.Empty;
        }

        public string CandidateId { get; }
        public string EvidencePacketId { get; }
        public string Realm { get; }
        public string Mode { get; }
        public string CheckId { get; }
        public string ScenarioId { get; }
        public string ScenarioVersion { get; }
        public string ScenarioCatalogSha256 { get; }
        public string ScenarioDefinitionSha256 { get; }
        public long Seed { get; }
        public string LogicalClockUtc { get; }
        public string Locale { get; }
        public string InputClass { get; }
        public string AccessibilityPreset { get; }
        public string EvidenceOutputRoot { get; }
        public string LogFile { get; }
        public string ModeNamespace =>
            string.Equals(Mode, "Kingdom2_5D", StringComparison.Ordinal) ? "2_5d" : "3d";
    }

    public sealed class RealmSliceEvidenceResult
    {
        public string ExecutionState { get; set; }
        public string TechnicalResult { get; set; }
        public string ExpectedResult { get; set; }
        public string ObservedResult { get; set; }
        public string ReasonCode { get; set; }
        public List<string> DefectIds { get; set; }
        public string ScenarioDefinitionSha256 { get; set; }
        public Dictionary<string, object> Metrics { get; set; }

        public Dictionary<string, object> ToDocument()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["executionState"] = ExecutionState ?? string.Empty,
                ["technicalResult"] = TechnicalResult ?? string.Empty,
                ["expectedResult"] = ExpectedResult ?? string.Empty,
                ["observedResult"] = ObservedResult ?? string.Empty,
                ["reasonCode"] = ReasonCode ?? string.Empty,
                ["defectIds"] = (DefectIds ?? new List<string>()).Cast<object>().ToList(),
                ["scenarioDefinitionSha256"] = ScenarioDefinitionSha256 ?? string.Empty,
                ["metrics"] = Metrics ?? new Dictionary<string, object>(StringComparer.Ordinal)
            };
        }
    }

    public sealed class RealmSliceEvidenceLayout
    {
        public RealmSliceEvidenceLayout(string outputRoot, string structuredLogFileName, bool performance)
        {
            OutputRoot = Path.GetFullPath(outputRoot);
            StructuredLogPath = Path.Combine(OutputRoot, structuredLogFileName);
            ScreenshotsDirectory = Path.Combine(OutputRoot, "screenshots");
            VideoDirectory = Path.Combine(OutputRoot, "video");
            ResultPath = Path.Combine(OutputRoot, "result.json");
            TelemetryDirectory = Path.Combine(OutputRoot, "telemetry");
            ProfilerDirectory = Path.Combine(OutputRoot, "profiler");
            Performance = performance;
            StillPath = Path.Combine(ScreenshotsDirectory, "anchor.png");
            VideoPath = Path.Combine(VideoDirectory, "continuous.avi");
            TelemetryPath = Path.Combine(TelemetryDirectory, "frames.json");
            ProfilerPath = Path.Combine(ProfilerDirectory, "capture.raw");
        }

        public string OutputRoot { get; }
        public string StructuredLogPath { get; }
        public string ScreenshotsDirectory { get; }
        public string VideoDirectory { get; }
        public string ResultPath { get; }
        public string TelemetryDirectory { get; }
        public string ProfilerDirectory { get; }
        public string StillPath { get; }
        public string VideoPath { get; }
        public string TelemetryPath { get; }
        public string ProfilerPath { get; }
        public bool Performance { get; }

        public bool Contains(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string full = Path.GetFullPath(path);
            string prefix = OutputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(full, OutputRoot, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class RealmSliceEvidenceRequestParser
    {
        public const string EnableArgument = "--al-realm-slice-evidence";
        public const string PolicyFileName = "realm_slice_evidence_policy.v1.json";
        public const string ScenarioCatalogFileName = "realm_slice_scenarios.v1.json";

        private static readonly HashSet<string> ValueArguments = new HashSet<string>(
            new[]
            {
                "--candidate-id",
                "--evidence-packet-id",
                "--realm",
                "--mode",
                "--check-id",
                "--scenario-id",
                "--scenario-version",
                "--scenario-catalog-sha256",
                "--scenario-definition-sha256",
                "--seed",
                "--logical-clock-utc",
                "--locale",
                "--input-class",
                "--accessibility-preset",
                "--evidence-output-root",
                "-logFile"
            },
            StringComparer.Ordinal);

        public static bool IsRequested(IEnumerable<string> arguments)
        {
            return arguments != null && arguments.Any(argument =>
                string.Equals(argument, EnableArgument, StringComparison.Ordinal));
        }

        public static bool TryParse(
            string[] arguments,
            out RealmSliceEvidenceRequest request,
            out string diagnosticCode)
        {
            request = null;
            if (arguments == null || !IsRequested(arguments))
                return Fail("AL-RSQ-RUNNER-NOT-REQUESTED", out diagnosticCode);

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index] ?? string.Empty;
                if (index == 0 && !argument.StartsWith("-", StringComparison.Ordinal)) continue;
                if (string.Equals(argument, EnableArgument, StringComparison.Ordinal)) continue;
                if (string.IsNullOrEmpty(argument) || argument[0] != '-') continue;
                if (!ValueArguments.Contains(argument))
                    return Fail("AL-RSQ-RUNNER-ARGUMENT-UNKNOWN:" + argument, out diagnosticCode);
                if (index + 1 >= arguments.Length ||
                    (arguments[index + 1] ?? string.Empty).StartsWith("-", StringComparison.Ordinal))
                    return Fail("AL-RSQ-RUNNER-ARGUMENT-VALUE-MISSING:" + argument, out diagnosticCode);
                if (values.ContainsKey(argument))
                    return Fail("AL-RSQ-RUNNER-ARGUMENT-DUPLICATE:" + argument, out diagnosticCode);
                values.Add(argument, arguments[++index] ?? string.Empty);
            }

            if (!TryRequired(values, "--candidate-id", "AL-RSQ-RUNNER-CANDIDATE-ID-MISSING", out string candidate, out diagnosticCode) ||
                !TryRequired(values, "--evidence-packet-id", "AL-RSQ-RUNNER-PACKET-ID-MISSING", out string packet, out diagnosticCode) ||
                !TryRequired(values, "--realm", "AL-RSQ-RUNNER-REALM-MISSING", out string realm, out diagnosticCode) ||
                !TryRequired(values, "--mode", "AL-RSQ-RUNNER-MODE-MISSING", out string mode, out diagnosticCode) ||
                !TryRequired(values, "--check-id", "AL-RSQ-RUNNER-CHECK-ID-MISSING", out string checkId, out diagnosticCode) ||
                !TryRequired(values, "--scenario-id", "AL-RSQ-RUNNER-SCENARIO-ID-MISSING", out string scenarioId, out diagnosticCode) ||
                !TryRequired(values, "--scenario-version", "AL-RSQ-RUNNER-SCENARIO-VERSION-MISSING", out string scenarioVersion, out diagnosticCode) ||
                !TrySha(values, "--scenario-catalog-sha256", "AL-RSQ-RUNNER-CATALOG-SHA256-INVALID", out string catalogSha, out diagnosticCode) ||
                !TrySha(values, "--scenario-definition-sha256", "AL-RSQ-RUNNER-DEFINITION-SHA256-INVALID", out string definitionSha, out diagnosticCode) ||
                !TryRequired(values, "--logical-clock-utc", "AL-RSQ-RUNNER-CLOCK-MISSING", out string clock, out diagnosticCode) ||
                !TryRequired(values, "--locale", "AL-RSQ-RUNNER-LOCALE-MISSING", out string locale, out diagnosticCode) ||
                !TryRequired(values, "--input-class", "AL-RSQ-RUNNER-INPUT-CLASS-MISSING", out string inputClass, out diagnosticCode) ||
                !TryRequired(values, "--accessibility-preset", "AL-RSQ-RUNNER-ACCESSIBILITY-MISSING", out string accessibility, out diagnosticCode) ||
                !TryRequired(values, "--evidence-output-root", "AL-RSQ-RUNNER-OUTPUT-MISSING", out string output, out diagnosticCode))
                return false;

            if (!IsSafeToken(candidate) || !candidate.StartsWith("RSQ-", StringComparison.Ordinal))
                return Fail("AL-RSQ-RUNNER-CANDIDATE-ID-INVALID", out diagnosticCode);
            if (!IsSafeToken(packet) || !packet.StartsWith("RSQ-EV-", StringComparison.Ordinal))
                return Fail("AL-RSQ-RUNNER-PACKET-ID-INVALID", out diagnosticCode);
            if (!IsKnownRealm(realm))
                return Fail("AL-RSQ-RUNNER-REALM-INVALID", out diagnosticCode);
            if (!string.Equals(mode, "Adventure3D", StringComparison.Ordinal) &&
                !string.Equals(mode, "Kingdom2_5D", StringComparison.Ordinal))
                return Fail("AL-RSQ-RUNNER-MODE-INVALID", out diagnosticCode);
            if (!clock.EndsWith("Z", StringComparison.Ordinal) || clock.Length < 20)
                return Fail("AL-RSQ-RUNNER-CLOCK-INVALID", out diagnosticCode);

            string seedText;
            if (!values.TryGetValue("--seed", out seedText) ||
                !long.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seed))
                return Fail("AL-RSQ-RUNNER-SEED-INVALID", out diagnosticCode);

            string fullOutput;
            try
            {
                fullOutput = Path.GetFullPath(output);
            }
            catch (Exception)
            {
                return Fail("AL-RSQ-RUNNER-OUTPUT-INVALID", out diagnosticCode);
            }

            values.TryGetValue("-logFile", out string logFile);
            request = new RealmSliceEvidenceRequest(
                candidate,
                packet,
                realm,
                mode,
                checkId,
                scenarioId,
                scenarioVersion,
                catalogSha,
                definitionSha,
                seed,
                clock,
                locale,
                inputClass,
                accessibility,
                fullOutput,
                logFile ?? string.Empty);
            diagnosticCode = "AL-RSQ-RUNNER-REQUEST-READY";
            return true;
        }

        private static bool TryRequired(
            IReadOnlyDictionary<string, string> values,
            string name,
            string diagnostic,
            out string value,
            out string diagnosticCode)
        {
            value = null;
            diagnosticCode = null;
            if (!values.TryGetValue(name, out value) || string.IsNullOrWhiteSpace(value))
                return Fail(diagnostic, out diagnosticCode);
            return true;
        }

        private static bool TrySha(
            IReadOnlyDictionary<string, string> values,
            string name,
            string diagnostic,
            out string value,
            out string diagnosticCode)
        {
            value = null;
            diagnosticCode = null;
            if (!values.TryGetValue(name, out value) || !IsSha256(value))
                return Fail(diagnostic, out diagnosticCode);
            return true;
        }

        private static bool Fail(string diagnostic, out string diagnosticCode)
        {
            diagnosticCode = diagnostic;
            return false;
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                    return false;
            }

            return true;
        }

        private static bool IsSafeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
                return false;
            return Path.GetFileName(value) == value;
        }

        private static bool IsKnownRealm(string realm)
        {
            return string.Equals(realm, "Stonehold", StringComparison.Ordinal) ||
                   string.Equals(realm, "Eldergrove", StringComparison.Ordinal) ||
                   string.Equals(realm, "Crownlands", StringComparison.Ordinal) ||
                   string.Equals(realm, "Umbral", StringComparison.Ordinal);
        }
    }

    public interface IRealmSliceEvidenceCapture
    {
        bool TryCaptureStill(string outputPath, out string diagnostic);
        bool TryCaptureVideo(string outputPath, out string diagnostic);
        bool TryCapturePerformance(
            string telemetryPath,
            string profilerPath,
            double warmupSeconds,
            double measuredSeconds,
            out string diagnostic);
    }
}
