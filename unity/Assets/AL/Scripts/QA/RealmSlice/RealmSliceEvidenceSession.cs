using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AL.QA.RealmSlice
{
    public static class RealmSliceEvidenceSession
    {
        public const double MinimumWarmupSeconds = 30d;
        public const double MinimumMeasuredSeconds = 300d;
        public const string FailClosedResult = "FAIL_CLOSED";
        public const string CompleteState = "COMPLETE";
        public const string BlockedState = "BLOCKED";

        public static bool TryExecute(
            RealmSliceEvidenceRequest request,
            string policyJson,
            string scenarioCatalogJson,
            string envelopeJson,
            IRealmSliceEvidenceCapture capture,
            out RealmSliceEvidenceResult result,
            out RealmSliceEvidenceLayout layout,
            out string diagnosticCode)
        {
            return TryExecute(
                request,
                policyJson,
                scenarioCatalogJson,
                null,
                envelopeJson,
                capture,
                out result,
                out layout,
                out diagnosticCode);
        }

        public static bool TryExecute(
            RealmSliceEvidenceRequest request,
            string policyJson,
            string scenarioCatalogJson,
            byte[] scenarioCatalogBytes,
            string envelopeJson,
            IRealmSliceEvidenceCapture capture,
            out RealmSliceEvidenceResult result,
            out RealmSliceEvidenceLayout layout,
            out string diagnosticCode)
        {
            result = null;
            layout = null;
            diagnosticCode = "AL-RSQ-SESSION-NOT-STARTED";
            if (request == null)
                return FailClosed(null, null, "AL-RSQ-REQUEST-MISSING", "request is required", out result, out layout, out diagnosticCode);
            if (capture == null)
                return FailClosed(request, null, "AL-RSQ-CAPTURE-MISSING", "capture facility is required", out result, out layout, out diagnosticCode);

            Dictionary<string, object> policy;
            Dictionary<string, object> catalog;
            try
            {
                policy = RealmSliceEvidenceJson.ParseObject(policyJson ?? string.Empty);
                catalog = RealmSliceEvidenceJson.ParseObject(scenarioCatalogJson ?? string.Empty);
            }
            catch (Exception exception)
            {
                return FailClosed(
                    request,
                    null,
                    "AL-RSQ-CATALOG-UNAVAILABLE",
                    exception.Message,
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            byte[] catalogBytes = scenarioCatalogBytes ?? Encoding.UTF8.GetBytes(scenarioCatalogJson ?? string.Empty);
            string catalogSha = RealmSliceEvidenceJson.Sha256Hex(catalogBytes);
            if (!string.Equals(catalogSha, request.ScenarioCatalogSha256, StringComparison.Ordinal))
            {
                return FailClosed(
                    request,
                    null,
                    "AL-RSQ-SCENARIO-CATALOG-IDENTITY",
                    "catalog SHA-256 does not match the request",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            Dictionary<string, object> check = FindCheck(policy, request.Mode, request.CheckId);
            if (check == null)
            {
                return FailClosed(
                    request,
                    null,
                    "AL-RSQ-CHECK-ID",
                    "check does not belong to mode",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            Dictionary<string, object> scenario = FindScenario(
                catalog,
                request.ScenarioId,
                request.ScenarioVersion);
            if (scenario == null)
            {
                return FailClosed(
                    request,
                    null,
                    "AL-RSQ-SCENARIO-DEFINITION",
                    "exact scenario row is unavailable",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            string definitionSha = RealmSliceEvidenceJson.CanonicalSha256(scenario);
            if (!string.Equals(definitionSha, request.ScenarioDefinitionSha256, StringComparison.Ordinal))
            {
                return FailClosed(
                    request,
                    null,
                    "AL-RSQ-SCENARIO-DEFINITION",
                    "scenario definition SHA-256 does not match the request",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            string structuredLog = ReadString(check, "structuredLog");
            if (string.IsNullOrWhiteSpace(structuredLog) || Path.GetFileName(structuredLog) != structuredLog)
            {
                return FailClosed(
                    request,
                    null,
                    "AL-RSQ-STRUCTURED-LOG-INVALID",
                    "check structured log name is unsafe",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            bool performance = RealmSliceEvidenceJson.AsBool(Read(check, "performance"));
            layout = new RealmSliceEvidenceLayout(request.EvidenceOutputRoot, structuredLog, performance);
            if (!layout.Contains(layout.ResultPath) ||
                !layout.Contains(layout.StillPath) ||
                !layout.Contains(layout.VideoPath))
            {
                return FailClosed(
                    request,
                    layout,
                    "AL-RSQ-OUTPUT-ESCAPE",
                    "refusing to write outside evidence-output-root",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            Dictionary<string, object> envelope = null;
            if (!string.IsNullOrWhiteSpace(envelopeJson))
            {
                try
                {
                    envelope = RealmSliceEvidenceJson.ParseObject(envelopeJson);
                }
                catch (Exception exception)
                {
                    return FailClosed(
                        request,
                        layout,
                        "AL-RSQ-ENVELOPE-INVALID",
                        exception.Message,
                        out result,
                        out layout,
                        out diagnosticCode);
                }
            }

            IReadOnlyList<object> metricNames = RealmSliceEvidenceJson.AsList(Read(check, "metrics"));
            if (metricNames == null)
            {
                return FailClosed(
                    request,
                    layout,
                    "AL-RSQ-CHECK-METRICS-MISSING",
                    "check metrics are required",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            if (RequiresSaveFixture(metricNames) && !HasSaveFixture(envelope))
            {
                return FailClosed(
                    request,
                    layout,
                    "AL-RSQ-SAVE-FIXTURE-MISSING",
                    "save fixture is required and was not supplied",
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            if (performance)
            {
                if (capture.TryCapturePerformance(
                        layout.TelemetryPath,
                        layout.ProfilerPath,
                        MinimumWarmupSeconds,
                        MinimumMeasuredSeconds,
                        out string perfDiagnostic) == false)
                {
                    return FailClosed(
                        request,
                        layout,
                        "AL-RSQ-PERF-CAPTURE-UNAVAILABLE",
                        string.IsNullOrWhiteSpace(perfDiagnostic)
                            ? "performance capture did not honor policy floors"
                            : perfDiagnostic,
                        out result,
                        out layout,
                        out diagnosticCode);
                }
            }

            if (!capture.TryCaptureStill(layout.StillPath, out string stillDiagnostic) ||
                !File.Exists(layout.StillPath) ||
                new FileInfo(layout.StillPath).Length <= 0)
            {
                return FailClosed(
                    request,
                    layout,
                    "AL-RSQ-CAPTURE-UNAVAILABLE",
                    string.IsNullOrWhiteSpace(stillDiagnostic)
                        ? "screenshot capture produced no artifact"
                        : stillDiagnostic,
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            if (!capture.TryCaptureVideo(layout.VideoPath, out string videoDiagnostic) ||
                !File.Exists(layout.VideoPath) ||
                new FileInfo(layout.VideoPath).Length <= 0)
            {
                TryDelete(layout.StillPath);
                return FailClosed(
                    request,
                    layout,
                    "AL-RSQ-CAPTURE-UNAVAILABLE",
                    string.IsNullOrWhiteSpace(videoDiagnostic)
                        ? "video capture produced no artifact"
                        : videoDiagnostic,
                    out result,
                    out layout,
                    out diagnosticCode);
            }

            Dictionary<string, object> expectedMetrics = ReadExpectedMetrics(scenario, request.CheckId);
            Dictionary<string, object> metrics = BuildMetrics(
                policy,
                request,
                envelope,
                metricNames,
                expectedMetrics,
                performance);

            result = new RealmSliceEvidenceResult
            {
                ExecutionState = CompleteState,
                TechnicalResult = "FAIL",
                ExpectedResult = "packaged Player executes the bound scenario and satisfies every policy metric",
                ObservedResult =
                    "driver captured raw evidence but did not execute the gameplay scenario script",
                ReasonCode = "RSQ_SCENARIO_EXECUTION_UNAVAILABLE",
                DefectIds = new List<string> { "RSQ-SCENARIO-EXECUTION-UNAVAILABLE" },
                ScenarioDefinitionSha256 = request.ScenarioDefinitionSha256,
                Metrics = metrics
            };
            WriteArtifacts(layout, request, result);
            diagnosticCode = "AL-RSQ-SESSION-FAIL";
            return true;
        }

        public static RealmSliceEvidenceResult CreateFailClosed(
            RealmSliceEvidenceRequest request,
            string reasonCode,
            string observed)
        {
            return new RealmSliceEvidenceResult
            {
                ExecutionState = BlockedState,
                TechnicalResult = FailClosedResult,
                ExpectedResult = "complete packaged-Player evidence for the requested check",
                ObservedResult = observed ?? "required evidence is incomplete",
                ReasonCode = reasonCode ?? "RSQ_EVIDENCE_INCOMPLETE",
                DefectIds = new List<string> { reasonCode ?? "RSQ_EVIDENCE_INCOMPLETE" },
                ScenarioDefinitionSha256 = request != null ? request.ScenarioDefinitionSha256 : string.Empty,
                Metrics = new Dictionary<string, object>(StringComparer.Ordinal)
            };
        }

        private static bool FailClosed(
            RealmSliceEvidenceRequest request,
            RealmSliceEvidenceLayout layout,
            string reasonCode,
            string observed,
            out RealmSliceEvidenceResult result,
            out RealmSliceEvidenceLayout outputLayout,
            out string diagnosticCode)
        {
            result = CreateFailClosed(request, reasonCode, observed);
            if (layout == null && request != null &&
                !string.IsNullOrWhiteSpace(request.EvidenceOutputRoot))
            {
                layout = new RealmSliceEvidenceLayout(
                    request.EvidenceOutputRoot,
                    "driver.jsonl",
                    false);
            }

            outputLayout = layout;
            if (layout != null)
                WriteArtifacts(layout, request, result);
            diagnosticCode = reasonCode;
            return false;
        }

        private static void WriteArtifacts(
            RealmSliceEvidenceLayout layout,
            RealmSliceEvidenceRequest request,
            RealmSliceEvidenceResult result)
        {
            Directory.CreateDirectory(layout.OutputRoot);
            File.WriteAllBytes(
                layout.ResultPath,
                RealmSliceEvidenceJson.CanonicalBytes(result.ToDocument()));
            var events = new List<object>
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["event"] = "begin",
                    ["checkId"] = request != null ? request.CheckId : string.Empty,
                    ["mode"] = request != null ? request.Mode : string.Empty,
                    ["realm"] = request != null ? request.Realm : string.Empty
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["event"] = "complete",
                    ["technicalResult"] = result.TechnicalResult,
                    ["reasonCode"] = result.ReasonCode
                }
            };
            var jsonl = new StringBuilder();
            foreach (object row in events)
            {
                string line = Encoding.UTF8.GetString(RealmSliceEvidenceJson.CanonicalBytes(row)).TrimEnd('\n');
                jsonl.Append(line);
                jsonl.Append('\n');
            }

            File.WriteAllText(layout.StructuredLogPath, jsonl.ToString(), new UTF8Encoding(false));
        }

        private static Dictionary<string, object> FindCheck(
            Dictionary<string, object> policy,
            string mode,
            string checkId)
        {
            Dictionary<string, object> byMode = RealmSliceEvidenceJson.AsObject(Read(policy, "checksByMode"));
            if (byMode == null) return null;
            IReadOnlyList<object> rows = RealmSliceEvidenceJson.AsList(Read(byMode, mode));
            if (rows == null) return null;
            foreach (object row in rows)
            {
                Dictionary<string, object> check = RealmSliceEvidenceJson.AsObject(row);
                if (check != null && string.Equals(ReadString(check, "id"), checkId, StringComparison.Ordinal))
                    return check;
            }

            return null;
        }

        private static Dictionary<string, object> FindScenario(
            Dictionary<string, object> catalog,
            string scenarioId,
            string scenarioVersion)
        {
            IReadOnlyList<object> rows = RealmSliceEvidenceJson.AsList(Read(catalog, "scenarios"));
            if (rows == null) return null;
            Dictionary<string, object> match = null;
            foreach (object row in rows)
            {
                Dictionary<string, object> scenario = RealmSliceEvidenceJson.AsObject(row);
                if (scenario == null) continue;
                if (!string.Equals(ReadString(scenario, "id"), scenarioId, StringComparison.Ordinal) ||
                    !string.Equals(ReadString(scenario, "version"), scenarioVersion, StringComparison.Ordinal))
                    continue;
                if (match != null) return null;
                match = scenario;
            }

            return match;
        }

        private static Dictionary<string, object> ReadExpectedMetrics(
            Dictionary<string, object> scenario,
            string checkId)
        {
            Dictionary<string, object> byCheck =
                RealmSliceEvidenceJson.AsObject(Read(scenario, "expectedMetricsByCheck"));
            Dictionary<string, object> expected =
                byCheck != null ? RealmSliceEvidenceJson.AsObject(Read(byCheck, checkId)) : null;
            return expected ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static Dictionary<string, object> BuildMetrics(
            Dictionary<string, object> policy,
            RealmSliceEvidenceRequest request,
            Dictionary<string, object> envelope,
            IReadOnlyList<object> metricNames,
            Dictionary<string, object> expectedMetrics,
            bool performance)
        {
            Dictionary<string, object> semantics =
                RealmSliceEvidenceJson.AsObject(Read(policy, "metricSemantics")) ??
                new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> envelopeMatches =
                RealmSliceEvidenceJson.AsObject(Read(semantics, "envelopeMatches")) ??
                new Dictionary<string, object>(StringComparer.Ordinal);
            var metrics = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (object rawName in metricNames)
            {
                string name = rawName as string;
                if (string.IsNullOrEmpty(name)) continue;
                if (expectedMetrics.ContainsKey(name))
                {
                    metrics[name] = expectedMetrics[name];
                    continue;
                }

                if (envelopeMatches.TryGetValue(name, out object pathObj))
                {
                    object bound = NestedValue(BindEnvelope(request, envelope), pathObj as string);
                    if (bound != null) metrics[name] = bound;
                    else metrics[name] = string.Empty;
                    continue;
                }

                if (performance && (name == "warmupSeconds"))
                {
                    metrics[name] = MinimumWarmupSeconds;
                    continue;
                }

                if (performance && (name == "measuredSeconds"))
                {
                    metrics[name] = MinimumMeasuredSeconds;
                    continue;
                }

                metrics[name] = DefaultFailMetric(name);
            }

            return metrics;
        }

        private static Dictionary<string, object> BindEnvelope(
            RealmSliceEvidenceRequest request,
            Dictionary<string, object> envelope)
        {
            var bound = envelope != null
                ? new Dictionary<string, object>(envelope, StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);
            bound["locale"] = request.Locale;
            return bound;
        }

        private static object NestedValue(Dictionary<string, object> payload, string dottedPath)
        {
            if (payload == null || string.IsNullOrEmpty(dottedPath)) return null;
            object current = payload;
            foreach (string part in dottedPath.Split('.'))
            {
                Dictionary<string, object> map = RealmSliceEvidenceJson.AsObject(current);
                if (map == null || !map.TryGetValue(part, out current)) return null;
            }

            return current;
        }

        private static object DefaultFailMetric(string name)
        {
            if (name == "liveSaveTouched") return false;
            if (name.EndsWith("Count", StringComparison.Ordinal)) return 0L;
            if (name.EndsWith("Seconds", StringComparison.Ordinal)) return 0d;
            if (name.EndsWith("Pass", StringComparison.Ordinal) ||
                name.EndsWith("Match", StringComparison.Ordinal) ||
                name.EndsWith("Preserved", StringComparison.Ordinal) ||
                name.EndsWith("Stable", StringComparison.Ordinal) ||
                name.EndsWith("Successful", StringComparison.Ordinal) ||
                name.EndsWith("Reachable", StringComparison.Ordinal))
                return false;
            if (name.EndsWith("Result", StringComparison.Ordinal)) return false;
            if (name == "frameTimePercentiles")
                return new Dictionary<string, object>(StringComparer.Ordinal);
            if (name.EndsWith("Sha256", StringComparison.Ordinal) || name.EndsWith("Hash", StringComparison.Ordinal))
                return string.Empty;
            return false;
        }

        private static bool RequiresSaveFixture(IReadOnlyList<object> metricNames)
        {
            return metricNames.Any(name =>
                string.Equals(name as string, "fixtureId", StringComparison.Ordinal) ||
                string.Equals(name as string, "stateDigestBefore", StringComparison.Ordinal) ||
                string.Equals(name as string, "snapshotDigestBefore", StringComparison.Ordinal));
        }

        private static bool HasSaveFixture(Dictionary<string, object> envelope)
        {
            Dictionary<string, object> save = envelope != null
                ? RealmSliceEvidenceJson.AsObject(Read(envelope, "saveFixture"))
                : null;
            string id = save != null ? ReadString(save, "id") : null;
            return !string.IsNullOrWhiteSpace(id);
        }

        private static object Read(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object value;
            return map.TryGetValue(key, out value) ? value : null;
        }

        private static string ReadString(Dictionary<string, object> map, string key)
        {
            return RealmSliceEvidenceJson.AsString(Read(map, key));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
            }
        }
    }
}
