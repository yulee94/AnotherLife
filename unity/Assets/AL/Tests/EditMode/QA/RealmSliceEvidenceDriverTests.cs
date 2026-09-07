using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AL.QA.RealmSlice;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.QA
{
    public sealed class RealmSliceEvidenceDriverTests
    {
        [Test]
        public void CommandLineSelectsExactHarnessRequest()
        {
            string output = Path.Combine(Path.GetTempPath(), "al-rsq-out");
            string catalogSha = new string('a', 64);
            string definitionSha = new string('b', 64);
            string[] arguments =
            {
                "AnotherLifeUnity.exe",
                "--al-realm-slice-evidence",
                "--candidate-id", "RSQ-Stonehold-3d-r2.4.0-1",
                "--evidence-packet-id", "RSQ-EV-Stonehold-3d-r2.4.0-1",
                "--realm", "Stonehold",
                "--mode", "Adventure3D",
                "--check-id", "RSQ-3D-REN-001",
                "--scenario-id", "RSQ-3D-ARRIVAL",
                "--scenario-version", "v1",
                "--scenario-catalog-sha256", catalogSha,
                "--scenario-definition-sha256", definitionSha,
                "--seed", "1618033988",
                "--logical-clock-utc", "2026-01-01T00:00:00Z",
                "--locale", "en-US",
                "--input-class", "keyboard_mouse",
                "--accessibility-preset", "default",
                "--evidence-output-root", output,
                "-logFile", Path.Combine(output, "Player.log")
            };

            Assert.That(RealmSliceEvidenceRequestParser.TryParse(
                arguments,
                out RealmSliceEvidenceRequest request,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(request.CandidateId, Is.EqualTo("RSQ-Stonehold-3d-r2.4.0-1"));
            Assert.That(request.EvidencePacketId, Is.EqualTo("RSQ-EV-Stonehold-3d-r2.4.0-1"));
            Assert.That(request.Realm, Is.EqualTo("Stonehold"));
            Assert.That(request.Mode, Is.EqualTo("Adventure3D"));
            Assert.That(request.ModeNamespace, Is.EqualTo("3d"));
            Assert.That(request.CheckId, Is.EqualTo("RSQ-3D-REN-001"));
            Assert.That(request.ScenarioId, Is.EqualTo("RSQ-3D-ARRIVAL"));
            Assert.That(request.ScenarioVersion, Is.EqualTo("v1"));
            Assert.That(request.ScenarioCatalogSha256, Is.EqualTo(catalogSha));
            Assert.That(request.ScenarioDefinitionSha256, Is.EqualTo(definitionSha));
            Assert.That(request.Seed, Is.EqualTo(1618033988L));
            Assert.That(request.Locale, Is.EqualTo("en-US"));
            Assert.That(request.InputClass, Is.EqualTo("keyboard_mouse"));
            Assert.That(request.AccessibilityPreset, Is.EqualTo("default"));
            Assert.That(request.EvidenceOutputRoot, Is.EqualTo(Path.GetFullPath(output)));
        }

        [Test]
        public void CommandLineRejectsUnknownAndMissingArguments()
        {
            string[] missing =
            {
                "AnotherLifeUnity.exe", "--al-realm-slice-evidence",
                "--realm", "Stonehold"
            };
            Assert.That(RealmSliceEvidenceRequestParser.TryParse(missing, out _, out string missingDiagnostic), Is.False);
            Assert.That(missingDiagnostic, Does.Contain("MISSING"));

            string[] unknown =
            {
                "AnotherLifeUnity.exe", "--al-realm-slice-evidence",
                "--candidate-id", "RSQ-Stonehold-3d-r2.4.0-1",
                "--evidence-packet-id", "RSQ-EV-Stonehold-3d-r2.4.0-1",
                "--realm", "Stonehold",
                "--mode", "Adventure3D",
                "--check-id", "RSQ-3D-REN-001",
                "--scenario-id", "RSQ-3D-ARRIVAL",
                "--scenario-version", "v1",
                "--scenario-catalog-sha256", new string('a', 64),
                "--scenario-definition-sha256", new string('b', 64),
                "--seed", "1618033988",
                "--logical-clock-utc", "2026-01-01T00:00:00Z",
                "--locale", "en-US",
                "--input-class", "keyboard_mouse",
                "--accessibility-preset", "default",
                "--evidence-output-root", Path.GetTempPath(),
                "--al-gs-run"
            };
            Assert.That(RealmSliceEvidenceRequestParser.TryParse(unknown, out _, out string unknownDiagnostic), Is.False);
            Assert.That(unknownDiagnostic, Is.EqualTo("AL-RSQ-RUNNER-ARGUMENT-UNKNOWN:--al-gs-run"));
        }

        [Test]
        public void SessionWritesModeIsolatedLayoutAndRequiredResultFields()
        {
            string root = NewRoot("layout");
            try
            {
                CatalogFiles files = LoadCanonicalCatalogs();
                RealmSliceEvidenceRequest request = RequestFor(
                    files,
                    root,
                    "Adventure3D",
                    "RSQ-3D-REN-001",
                    "RSQ-3D-ARRIVAL");
                var capture = new RecordingCapture();
                Assert.That(RealmSliceEvidenceSession.TryExecute(
                    request,
                    files.PolicyJson,
                    files.CatalogJson,
                    files.CatalogBytes,
                    null,
                    capture,
                    out RealmSliceEvidenceResult result,
                    out RealmSliceEvidenceLayout layout,
                    out string diagnostic), Is.True, diagnostic);
                Assert.That(result.TechnicalResult, Is.EqualTo("FAIL"));
                Assert.That(result.ExecutionState, Is.EqualTo("COMPLETE"));
                Assert.That(result.ExpectedResult, Is.Not.Null.And.Not.Empty);
                Assert.That(result.ObservedResult, Is.Not.Null.And.Not.Empty);
                Assert.That(result.ReasonCode, Is.EqualTo("RSQ_SCENARIO_EXECUTION_UNAVAILABLE"));
                Assert.That(result.DefectIds, Is.Not.Empty);
                Assert.That(result.ScenarioDefinitionSha256, Is.EqualTo(request.ScenarioDefinitionSha256));
                Assert.That(result.Metrics.ContainsKey("missingAssetCount"), Is.True);
                Assert.That(result.Metrics.ContainsKey("materialReadPass"), Is.True);
                Assert.That(File.Exists(layout.ResultPath), Is.True);
                Assert.That(File.Exists(layout.StructuredLogPath), Is.True);
                Assert.That(Path.GetFileName(layout.StructuredLogPath), Is.EqualTo("render.jsonl"));
                Assert.That(File.Exists(layout.StillPath), Is.True);
                Assert.That(File.Exists(layout.VideoPath), Is.True);
                Assert.That(Directory.Exists(layout.TelemetryDirectory), Is.False);
                Assert.That(layout.Contains(layout.StillPath), Is.True);
                Assert.That(layout.OutputRoot, Does.Not.Contain(Path.Combine("2_5d")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void Kingdom25DUsesIsolatedOutputAndDoesNotShareThreeDState()
        {
            string root3d = NewRoot("iso-3d");
            string root25d = NewRoot("iso-25d");
            try
            {
                CatalogFiles files = LoadCanonicalCatalogs();
                RealmSliceEvidenceRequest threeD = RequestFor(
                    files, root3d, "Adventure3D", "RSQ-3D-REN-001", "RSQ-3D-ARRIVAL");
                RealmSliceEvidenceRequest twoFive = RequestFor(
                    files, root25d, "Kingdom2_5D", "RSQ-2_5D-REN-001", "RSQ-2_5D-KINGDOM");
                Assert.That(threeD.ModeNamespace, Is.EqualTo("3d"));
                Assert.That(twoFive.ModeNamespace, Is.EqualTo("2_5d"));
                Assert.That(RealmSliceEvidenceSession.TryExecute(
                    threeD, files.PolicyJson, files.CatalogJson, files.CatalogBytes, null,
                    new RecordingCapture(), out RealmSliceEvidenceResult result3d,
                    out RealmSliceEvidenceLayout layout3d, out string diagnostic3d), Is.True, diagnostic3d);
                Assert.That(RealmSliceEvidenceSession.TryExecute(
                    twoFive, files.PolicyJson, files.CatalogJson, files.CatalogBytes, null,
                    new RecordingCapture(), out RealmSliceEvidenceResult result25d,
                    out RealmSliceEvidenceLayout layout25d, out string diagnostic25d), Is.True, diagnostic25d);
                Assert.That(layout3d.OutputRoot, Is.Not.EqualTo(layout25d.OutputRoot));
                Assert.That(File.Exists(layout3d.ResultPath), Is.True);
                Assert.That(File.Exists(layout25d.ResultPath), Is.True);
                Assert.That(File.ReadAllBytes(layout3d.ResultPath),
                    Is.Not.EqualTo(File.ReadAllBytes(layout25d.ResultPath)));
                Assert.That(result3d.Metrics.ContainsKey("missingAssetCount"), Is.True);
                Assert.That(result25d.Metrics.ContainsKey("snapshotPassCount"), Is.True);
                Assert.That(result3d.Metrics.ContainsKey("snapshotPassCount"), Is.False);
            }
            finally
            {
                DeleteRoot(root3d);
                DeleteRoot(root25d);
            }
        }

        [Test]
        public void HashBoundScenarioIdentityRejectsMismatchedDefinition()
        {
            string root = NewRoot("hash");
            try
            {
                CatalogFiles files = LoadCanonicalCatalogs();
                RealmSliceEvidenceRequest request = RequestFor(
                    files, root, "Adventure3D", "RSQ-3D-REN-001", "RSQ-3D-ARRIVAL");
                var mutated = new RealmSliceEvidenceRequest(
                    request.CandidateId,
                    request.EvidencePacketId,
                    request.Realm,
                    request.Mode,
                    request.CheckId,
                    request.ScenarioId,
                    request.ScenarioVersion,
                    request.ScenarioCatalogSha256,
                    new string('c', 64),
                    request.Seed,
                    request.LogicalClockUtc,
                    request.Locale,
                    request.InputClass,
                    request.AccessibilityPreset,
                    request.EvidenceOutputRoot,
                    request.LogFile);
                Assert.That(RealmSliceEvidenceSession.TryExecute(
                    mutated, files.PolicyJson, files.CatalogJson, files.CatalogBytes, null,
                    new RecordingCapture(), out RealmSliceEvidenceResult result,
                    out _, out string diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo("AL-RSQ-SCENARIO-DEFINITION"));
                Assert.That(result.TechnicalResult, Is.EqualTo("FAIL_CLOSED"));
                Assert.That(result.ExecutionState, Is.EqualTo("BLOCKED"));
                string shots = Path.Combine(root, "screenshots");
                Assert.That(
                    Directory.Exists(shots) ? Directory.GetFiles(shots, "*", SearchOption.AllDirectories).Length : 0,
                    Is.EqualTo(0));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void MissingCaptureFailsClosedWithoutFabricatedPassOrMedia()
        {
            string root = NewRoot("nocap");
            try
            {
                CatalogFiles files = LoadCanonicalCatalogs();
                RealmSliceEvidenceRequest request = RequestFor(
                    files, root, "Adventure3D", "RSQ-3D-REN-001", "RSQ-3D-ARRIVAL");
                Assert.That(RealmSliceEvidenceSession.TryExecute(
                    request, files.PolicyJson, files.CatalogJson, files.CatalogBytes, null,
                    new FailingCapture(), out RealmSliceEvidenceResult result,
                    out RealmSliceEvidenceLayout layout, out string diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo("AL-RSQ-CAPTURE-UNAVAILABLE"));
                Assert.That(result.TechnicalResult, Is.Not.EqualTo("PASS"));
                Assert.That(result.TechnicalResult, Is.EqualTo("FAIL_CLOSED"));
                Assert.That(File.Exists(layout.ResultPath), Is.True);
                Assert.That(File.Exists(layout.StillPath), Is.False);
                Assert.That(File.Exists(layout.VideoPath), Is.False);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void MissingSaveFixtureFailsClosedForSaveCheck()
        {
            string root = NewRoot("save");
            try
            {
                CatalogFiles files = LoadCanonicalCatalogs();
                RealmSliceEvidenceRequest request = RequestFor(
                    files, root, "Adventure3D", "RSQ-3D-SAVE-001", "RSQ-SAVE-CONTINUITY");
                Assert.That(RealmSliceEvidenceSession.TryExecute(
                    request, files.PolicyJson, files.CatalogJson, files.CatalogBytes, null,
                    new RecordingCapture(), out RealmSliceEvidenceResult result,
                    out _, out string diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo("AL-RSQ-SAVE-FIXTURE-MISSING"));
                Assert.That(result.TechnicalResult, Is.EqualTo("FAIL_CLOSED"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void PerformanceRowFailsClosedWhenCaptureCannotHonorPolicyFloors()
        {
            string root = NewRoot("perf");
            try
            {
                CatalogFiles files = LoadCanonicalCatalogs();
                RealmSliceEvidenceRequest request = RequestFor(
                    files, root, "Adventure3D", "RSQ-3D-PERF-001", "RSQ-3D-COMBAT");
                Assert.That(RealmSliceEvidenceSession.TryExecute(
                    request, files.PolicyJson, files.CatalogJson, files.CatalogBytes, null,
                    new RecordingCapture(), out RealmSliceEvidenceResult result,
                    out _, out string diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo("AL-RSQ-PERF-CAPTURE-UNAVAILABLE"));
                Assert.That(result.TechnicalResult, Is.EqualTo("FAIL_CLOSED"));
                Assert.That(RealmSliceEvidenceSession.MinimumWarmupSeconds, Is.GreaterThanOrEqualTo(30d));
                Assert.That(RealmSliceEvidenceSession.MinimumMeasuredSeconds, Is.GreaterThanOrEqualTo(300d));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void CanonicalJsonHashIsStableForScenarioIdentity()
        {
            var scenario = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["id"] = "RSQ-3D-ARRIVAL",
                ["version"] = "v1",
                ["mode"] = "Adventure3D"
            };
            string hex = RealmSliceEvidenceJson.CanonicalSha256(scenario);
            Assert.That(hex.Length, Is.EqualTo(64));
            Assert.That(hex, Is.EqualTo(RealmSliceEvidenceJson.CanonicalSha256(scenario)));
            Assert.That(hex, Does.Match("^[0-9a-f]{64}$"));
        }

        private static RealmSliceEvidenceRequest RequestFor(
            CatalogFiles files,
            string outputRoot,
            string mode,
            string checkId,
            string scenarioId)
        {
            Dictionary<string, object> catalog = RealmSliceEvidenceJson.ParseObject(files.CatalogJson);
            Dictionary<string, object> scenario = null;
            var rows = (IReadOnlyList<object>)catalog["scenarios"];
            foreach (object row in rows)
            {
                var map = (Dictionary<string, object>)row;
                if (string.Equals((string)map["id"], scenarioId, StringComparison.Ordinal) &&
                    string.Equals((string)map["version"], "v1", StringComparison.Ordinal))
                {
                    scenario = map;
                    break;
                }
            }

            Assert.That(scenario, Is.Not.Null, scenarioId);
            string definitionSha = RealmSliceEvidenceJson.CanonicalSha256(scenario);
            if (string.Equals(scenarioId, "RSQ-3D-ARRIVAL", StringComparison.Ordinal))
            {
                Assert.That(
                    definitionSha,
                    Is.EqualTo("3d35529e19a9a22864f84fbd943395eb3bcb3be06f205a79539c6fde04fe87ee"),
                    "C# canonical JSON must match the harness scenario_definition_identity digest");
            }
            string catalogSha = RealmSliceEvidenceJson.Sha256Hex(files.CatalogBytes);
            string ns = string.Equals(mode, "Kingdom2_5D", StringComparison.Ordinal) ? "2_5d" : "3d";
            return new RealmSliceEvidenceRequest(
                "RSQ-Stonehold-" + ns + "-r2.4.0-1",
                "RSQ-EV-Stonehold-" + ns + "-r2.4.0-1",
                "Stonehold",
                mode,
                checkId,
                scenarioId,
                "v1",
                catalogSha,
                definitionSha,
                1618033988L,
                "2026-01-01T00:00:00Z",
                "en-US",
                "keyboard_mouse",
                "default",
                outputRoot,
                Path.Combine(outputRoot, "Player.log"));
        }

        private static CatalogFiles LoadCanonicalCatalogs()
        {
            string gameData = Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData");
            string toolsQa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "qa"));
            string policyPath = FirstExisting(
                Path.Combine(gameData, RealmSliceEvidenceRequestParser.PolicyFileName),
                Path.Combine(toolsQa, RealmSliceEvidenceRequestParser.PolicyFileName));
            string catalogPath = FirstExisting(
                Path.Combine(gameData, RealmSliceEvidenceRequestParser.ScenarioCatalogFileName),
                Path.Combine(toolsQa, RealmSliceEvidenceRequestParser.ScenarioCatalogFileName));
            Assert.That(File.Exists(policyPath), Is.True, policyPath);
            Assert.That(File.Exists(catalogPath), Is.True, catalogPath);
            return new CatalogFiles
            {
                PolicyJson = File.ReadAllText(policyPath, new UTF8Encoding(false)),
                CatalogJson = File.ReadAllText(catalogPath, new UTF8Encoding(false)),
                CatalogBytes = File.ReadAllBytes(catalogPath)
            };
        }

        private static string FirstExisting(string preferred, string fallback)
        {
            return File.Exists(preferred) ? preferred : fallback;
        }

        private static string NewRoot(string token)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "al-rsq-" + token + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (Exception)
            {
            }
        }

        private sealed class CatalogFiles
        {
            public string PolicyJson;
            public string CatalogJson;
            public byte[] CatalogBytes;
        }

        private sealed class RecordingCapture : IRealmSliceEvidenceCapture
        {
            public bool TryCaptureStill(string outputPath, out string diagnostic)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
                File.WriteAllBytes(outputPath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 });
                diagnostic = string.Empty;
                return true;
            }

            public bool TryCaptureVideo(string outputPath, out string diagnostic)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
                File.WriteAllBytes(outputPath, Encoding.ASCII.GetBytes("RIFF"));
                diagnostic = string.Empty;
                return true;
            }

            public bool TryCapturePerformance(
                string telemetryPath,
                string profilerPath,
                double warmupSeconds,
                double measuredSeconds,
                out string diagnostic)
            {
                diagnostic = "AL-RSQ-PERF-CAPTURE-UNAVAILABLE";
                return false;
            }
        }

        private sealed class FailingCapture : IRealmSliceEvidenceCapture
        {
            public bool TryCaptureStill(string outputPath, out string diagnostic)
            {
                diagnostic = "camera unavailable";
                return false;
            }

            public bool TryCaptureVideo(string outputPath, out string diagnostic)
            {
                diagnostic = "video unavailable";
                return false;
            }

            public bool TryCapturePerformance(
                string telemetryPath,
                string profilerPath,
                double warmupSeconds,
                double measuredSeconds,
                out string diagnostic)
            {
                diagnostic = "perf unavailable";
                return false;
            }
        }
    }
}
