#!/usr/bin/env python3
"""Contract tests for the realm-slice evidence-capture harness."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import re
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("run_realm_slice_evidence.py")
POLICY = Path(__file__).with_name("realm_slice_evidence_policy.v1.json")
SCHEMA = Path(__file__).resolve().parents[2] / "unity/SharedContracts/realm-slice-evidence-manifest.schema.json"
SCENARIOS = Path(__file__).with_name("realm_slice_scenarios.v1.json")


def load_module():
    spec = importlib.util.spec_from_file_location("run_realm_slice_evidence", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RealmSlicePolicyTests(unittest.TestCase):
    def test_policy_keeps_modes_separate_and_expands_the_required_run_cube(self):
        module = load_module()
        policy = module.load_policy(POLICY)

        self.assertEqual(policy["protocolId"], "RSQ-PROTOCOL-v1.0.0")
        self.assertEqual(policy["modeNamespaces"], {
            "Adventure3D": "3d",
            "Kingdom2_5D": "2_5d",
        })
        self.assertEqual(len(policy["checksByMode"]["Adventure3D"]), 12)
        self.assertEqual(len(policy["checksByMode"]["Kingdom2_5D"]), 12)
        self.assertFalse(
            set(item["id"] for item in policy["checksByMode"]["Adventure3D"])
            & set(item["id"] for item in policy["checksByMode"]["Kingdom2_5D"])
        )

        three_d = module.expand_run_specs(policy, "Stonehold", "Adventure3D")
        two_five_d = module.expand_run_specs(policy, "Stonehold", "Kingdom2_5D")
        self.assertEqual(len(three_d), 72)
        self.assertEqual(len(two_five_d), 72)
        self.assertTrue(all(item["modeNamespace"] == "3d" for item in three_d))
        self.assertTrue(all(item["modeNamespace"] == "2_5d" for item in two_five_d))
        self.assertEqual(
            {item["locale"] for item in three_d},
            {"en-US", "ko-KR"},
        )
        self.assertEqual(
            {item["inputClass"] for item in three_d},
            {"keyboard_mouse", "controller"},
        )
        accessibility = [
            item for item in three_d if item["checkId"] == "RSQ-3D-ACC-001"
        ]
        self.assertEqual(
            {item["accessibilityPreset"] for item in accessibility},
            {
                "default",
                "text-200",
                "reduced-motion",
                "reduced-flash",
                "reduced-vfx",
                "audio-off-captions",
                "non-color",
            },
        )
        scenarios = json.loads(SCENARIOS.read_text(encoding="utf-8"))
        self.assertEqual(scenarios["fixtureVersion"], policy["fixtureVersion"])
        scenario_ids = {item["id"] for item in scenarios["scenarios"]}
        expected_ids = {
            item["scenarioId"].format(realm=realm)
            for checks in policy["checksByMode"].values()
            for item in checks
            for realm in (
                ["Stonehold", "Eldergrove", "Crownlands", "Umbral"]
                if "{realm}" in item["scenarioId"] else ["Stonehold"]
            )
        }
        self.assertEqual(scenario_ids, expected_ids)

    def test_shared_schema_is_strict_and_requires_traceability_and_artifacts(self):
        schema = json.loads(SCHEMA.read_text(encoding="utf-8"))

        self.assertEqual(schema["$schema"], "https://json-schema.org/draft/2020-12/schema")
        self.assertFalse(schema["additionalProperties"])
        for field in (
            "protocolId",
            "candidateId",
            "evidencePacketId",
            "realm",
            "mode",
            "modeNamespace",
            "build",
            "scenario",
            "platform",
            "artifacts",
            "technicalResult",
            "manifestSha256",
        ):
            self.assertIn(field, schema["required"])

    def test_every_required_metric_has_declared_pass_semantics(self):
        policy = json.loads(POLICY.read_text(encoding="utf-8"))
        semantics = policy["metricSemantics"]
        explicit = set().union(
            semantics["falseOnPass"], semantics["zeroOnPass"], semantics["positiveOnPass"],
            semantics["rangeOnPass"], semantics["envelopeMatches"],
            *(semantics["nonEmptyOnPass"].values()),
        )
        suffixes = tuple(
            semantics["trueSuffixes"]
            + semantics["positiveSuffixes"]
            + [semantics["resultSuffix"], "Count"]
        )
        required = {
            metric
            for checks in policy["checksByMode"].values()
            for check in checks
            for metric in check["metrics"]
        }
        self.assertEqual(sorted(name for name in required if name not in explicit and not name.endswith(suffixes)), [])

    def test_every_policy_check_id_matches_the_manifest_schema(self):
        schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
        pattern = re.compile(schema["properties"]["checkId"]["pattern"])
        policy = json.loads(POLICY.read_text(encoding="utf-8"))
        ids = [
            item["id"]
            for checks in policy["checksByMode"].values()
            for item in checks
        ]
        self.assertEqual(len(ids), 24)
        self.assertEqual([check_id for check_id in ids if pattern.fullmatch(check_id) is None], [])
        self.assertTrue(pattern.fullmatch("RSQ-3D-LOC-EN-001"))
        self.assertTrue(pattern.fullmatch("RSQ-2_5D-LOC-KO-001"))
        self.assertIsNone(pattern.fullmatch("RSQ-3D-LOC-ENGLISH-001"))

    def test_canonical_scenario_definitions_are_cryptographically_pinned(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        self.assertEqual(
            policy["scenarioCatalogSha256"],
            hashlib.sha256(SCENARIOS.read_bytes()).hexdigest(),
        )

        with tempfile.TemporaryDirectory() as temporary:
            tampered_path = Path(temporary) / SCENARIOS.name
            catalog = json.loads(SCENARIOS.read_text(encoding="utf-8"))
            catalog["scenarios"][0]["orderedAnchors"].reverse()
            tampered_path.write_bytes(module.canonical_json(catalog))
            policy["_scenarioCatalogPath"] = str(tampered_path)

            with self.assertRaisesRegex(module.RealmSliceEvidenceError, "SCENARIO_CATALOG_IDENTITY"):
                module.scenario_catalog_identity(policy)

            policy["_scenarioCatalogPath"] = str(Path(temporary) / "missing.json")
            with self.assertRaisesRegex(module.RealmSliceEvidenceError, "SCENARIO_CATALOG"):
                module.scenario_catalog_identity(policy)

    def test_non_empty_pass_metrics_reject_wrong_json_types(self):
        module = load_module()
        policy = json.loads(POLICY.read_text(encoding="utf-8"))
        envelope = {"saveFixture": {"sourceSchemaVersion": 0, "expectedSchemaVersion": 1}}

        for metric, invalid in (
            ("requiredAnchors", True),
            ("frameTimePercentiles", [16.6]),
            ("schemaBefore", "0"),
            ("stateDigestBefore", "not-a-sha256"),
            ("worldVisibleThresholdId", 7),
        ):
            with self.subTest(metric=metric):
                self.assertFalse(module.metric_satisfies_pass(policy, envelope, metric, invalid))

        for metric, valid in (
            ("requiredAnchors", ["slice_spawn"]),
            ("frameTimePercentiles", {"p95": 16.6}),
            ("schemaBefore", 0),
            ("stateDigestBefore", "a" * 64),
            ("worldVisibleThresholdId", "pc-high-60-v1"),
        ):
            with self.subTest(metric=metric):
                self.assertTrue(module.metric_satisfies_pass(policy, envelope, metric, valid))


class RealmSliceIdentityTests(unittest.TestCase):
    def setUp(self):
        self.module = load_module()
        self.policy = self.module.load_policy(POLICY)
        self.envelope = {
            "protocolId": "RSQ-PROTOCOL-v1.0.0",
            "candidateId": "RSQ-Stonehold-3d-r001-1",
            "evidencePacketId": "RSQ-EV-Stonehold-3d-r001-1",
            "realm": "Stonehold",
            "realmOrdinal": 1,
            "mode": "Adventure3D",
            "modeNamespace": "3d",
            "checkId": "RSQ-3D-NAV-001",
            "scenarioId": "RSQ-3D-ARRIVAL",
            "scenarioVersion": "v1",
            "sourceRevision": "a" * 40,
            "buildId": "build-001",
            "buildManifestSha256": "b" * 64,
            "artifactTreeSha256": "c" * 64,
            "locale": "ko-KR",
            "inputClass": "controller",
            "accessibilityPreset": "default",
            "platform": "WindowsPlayer",
            "deviceId": "device-pseudonym-001",
            "fixtureVersion": "rsq-fixtures-v1",
            "saveFixtureId": "pre_schema_v0_kingdom_progress",
            "saveFixtureSha256": "d" * 64,
            "seed": 1618033988,
            "logicalClockUtc": "2026-01-01T00:00:00Z",
            "rerunSequence": 1,
        }
        definition_digest, expected_metrics = self.module.scenario_definition_identity(
            self.policy,
            self.envelope["scenarioId"],
            self.envelope["scenarioVersion"],
            self.envelope["checkId"],
        )
        self.envelope.update({
            "scenarioCatalogSha256": self.policy["scenarioCatalogSha256"],
            "scenarioDefinitionSha256": definition_digest,
            "scenarioExpectedMetrics": expected_metrics,
        })

    def test_run_identity_is_stable_and_sensitive_to_every_trace_dimension(self):
        first = self.module.derive_run_identity(self.envelope)
        second = self.module.derive_run_identity(dict(self.envelope))

        self.assertEqual(first, second)
        self.assertRegex(first, r"^rsq-run-[0-9a-f]{24}$")
        for field in (
            "buildId", "realm", "mode", "checkId", "scenarioVersion", "locale",
            "inputClass", "accessibilityPreset", "platform", "deviceId",
            "fixtureVersion", "saveFixtureSha256", "rerunSequence",
        ):
            changed = dict(self.envelope)
            value = changed[field]
            changed[field] = value + "-changed" if isinstance(value, str) else value + 1
            self.assertNotEqual(first, self.module.derive_run_identity(changed), field)

    def test_mode_namespace_controls_the_non_overlapping_output_path(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            path = self.module.row_directory(root, self.envelope)
            expected = (
                root / self.envelope["candidateId"] / "Stonehold" / "3d" / "ko-KR"
                / "RSQ-3D-NAV-001" / self.module.derive_run_identity(self.envelope)
            )
            self.assertEqual(path, expected)

            wrong = dict(self.envelope)
            wrong["modeNamespace"] = "2_5d"
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "MODE_NAMESPACE"):
                self.module.validate_envelope(self.policy, wrong)

    def test_candidate_and_packet_identity_must_match_realm_and_mode_namespace(self):
        wrong = dict(self.envelope)
        wrong["candidateId"] = "RSQ-Stonehold-2_5d-r001-1"
        with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "CANDIDATE_ID"):
            self.module.validate_envelope(self.policy, wrong)

        wrong = dict(self.envelope)
        wrong["evidencePacketId"] = "RSQ-EV-Eldergrove-3d-r001-1"
        with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "PACKET_ID"):
            self.module.validate_envelope(self.policy, wrong)

        wrong = dict(self.envelope)
        wrong["evidencePacketId"] = "RSQ-EV-Stonehold-3d-r002-2"
        with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "PACKET_CANDIDATE"):
            self.module.validate_envelope(self.policy, wrong)


class RealmSliceSourceEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.module = load_module()

    def _write_source_fixture(self, root: Path):
        def write(relative: str, payload: bytes) -> Path:
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
            return path

        enabled = write("unity/Assets/AL/StreamingAssets/GameData/al_enabled_scene_manifest.v1.json", b"enabled\n")
        generated = write("unity/Assets/AL/StreamingAssets/GameData/al_generated_scene_manifest.v1.json", b"generated\n")
        world = write("unity/Assets/AL/StreamingAssets/GameData/al_world_streaming_catalog.json", b"world\n")
        narrative = write("unity/Assets/AL/StreamingAssets/GameData/al_main_quest_line_runtime.v1.json", b"narrative\n")
        fixture = write("unity/Assets/AL/Tests/EditMode/Fixtures/SaveSchema1/pre-schema-v0.json", b"save-fixture\n")
        fixture_manifest = {
            "manifestVersion": 1,
            "saveFormatId": "anotherlife.local-save",
            "currentSchemaVersion": 1,
            "fixtures": [{
                "id": "pre_schema_v0_kingdom_progress",
                "file": "pre-schema-v0.json",
                "sha256": hashlib.sha256(fixture.read_bytes()).hexdigest(),
                "sourceSchemaVersion": 0,
                "expectedSchemaVersion": 1,
                "expectedLoadStatus": "LoadedPrimary",
            }],
        }
        fixture_manifest_path = write(
            "unity/Assets/AL/Tests/EditMode/Fixtures/SaveSchema1/manifest.json",
            self.module.canonical_json(fixture_manifest),
        )
        player = write("build/AnotherLifeUnity.exe", b"MZ" + (b"\0" * 62))
        global_managers = write("build/AnotherLifeUnity_Data/globalgamemanagers", b"global-managers")
        artifact_files = []
        tree = bytearray()
        for artifact in (player, global_managers):
            relative = artifact.relative_to(player.parent).as_posix()
            size = artifact.stat().st_size
            digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
            artifact_files.append({"path": relative, "bytes": size, "sha256": digest})
            tree.extend(f"{relative}\0{size}\0{digest}\n".encode("utf-8"))
        build = {
            "schemaVersion": 1,
            "target": "windows64-development",
            "status": "succeeded",
            "source": {
                "sourceRevision": "a" * 40,
                "sourceTreeSha256": "b" * 64,
                "trackedInputsDirty": False,
            },
            "artifacts": {
                "root": str(player.parent),
                "treeSha256": hashlib.sha256(tree).hexdigest(),
                "reproducibleTreeSha256": "6" * 64,
                "smoke": {"status": "passed", "failures": []},
                "files": artifact_files,
            },
        }
        build["manifestSha256"] = self.module.digest_document(build, "manifestSha256")
        build_path = write("manifests/build.json", self.module.canonical_json(build))
        qa = {
            "profile": "full",
            "status": "passed",
            "provenance": {
                "sourceRevision": "a" * 40,
                "sourceDirty": False,
                "build": {
                    "manifestSha256": build["manifestSha256"],
                    "artifactTreeSha256": "6" * 64,
                },
                "scene": {
                    "enabledManifestSha256": hashlib.sha256(enabled.read_bytes()).hexdigest(),
                    "generatedManifestSha256": hashlib.sha256(generated.read_bytes()).hexdigest(),
                },
                "content": {
                    "worldCatalogSha256": hashlib.sha256(world.read_bytes()).hexdigest(),
                    "narrativeCatalogSha256": hashlib.sha256(narrative.read_bytes()).hexdigest(),
                },
                "save": {
                    "formatId": "anotherlife.local-save",
                    "schemaVersion": 1,
                    "fixtureManifestSha256": hashlib.sha256(fixture_manifest_path.read_bytes()).hexdigest(),
                },
            },
            "contracts": [
                {"id": name, "status": "passed"}
                for name in self.module.REQUIRED_QA_CONTRACTS
            ],
        }
        qa["reportSha256"] = self.module.digest_document(qa, "reportSha256")
        qa_path = write("qa/report.json", self.module.canonical_json(qa))
        platform = {
            "platform": "WindowsPlayer",
            "deviceId": "device-pseudonym-001",
            "osVersion": "Windows 11",
            "graphicsApi": "Direct3D11",
            "qualityPreset": "pc_high_60",
            "viewport": {"width": 1920, "height": 1080},
            "renderScale": 1.0,
            "refreshRate": 60,
            "thresholdSetId": "t_4a5b066c",
            "captureTool": "ffmpeg",
            "captureToolVersion": "7.1",
        }
        platform_path = write("platform.json", self.module.canonical_json(platform))
        return build_path, qa_path, platform_path, player

    def test_source_evidence_binds_clean_build_qa_catalog_fixture_platform_and_player(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            build, qa, platform, player = self._write_source_fixture(root)

            evidence = self.module.load_source_evidence(
                root, build, qa, platform, "pre_schema_v0_kingdom_progress"
            )

            self.assertEqual(evidence["build"]["sourceRevision"], "a" * 40)
            self.assertEqual(evidence["build"]["artifactTreeSha256"], "6" * 64)
            self.assertEqual(evidence["catalogs"]["contentCatalogSha256"], hashlib.sha256(b"world\n").hexdigest())
            self.assertEqual(evidence["saveFixture"]["id"], "pre_schema_v0_kingdom_progress")
            self.assertEqual(evidence["player"], player.resolve())
            self.assertEqual(evidence["platform"]["platform"], "WindowsPlayer")

    def test_source_evidence_fails_closed_on_dirty_or_missing_metadata(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            build, qa, platform, _ = self._write_source_fixture(root)
            build_payload = json.loads(build.read_text(encoding="utf-8"))
            build_payload["source"]["trackedInputsDirty"] = True
            build_payload["manifestSha256"] = self.module.digest_document(build_payload, "manifestSha256")
            build.write_bytes(self.module.canonical_json(build_payload))
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "BUILD_DIRTY"):
                self.module.load_source_evidence(root, build, qa, platform, "pre_schema_v0_kingdom_progress")

            build, qa, platform, _ = self._write_source_fixture(root)
            platform_payload = json.loads(platform.read_text(encoding="utf-8"))
            del platform_payload["graphicsApi"]
            platform.write_bytes(self.module.canonical_json(platform_payload))
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "PLATFORM_METADATA"):
                self.module.load_source_evidence(root, build, qa, platform, "pre_schema_v0_kingdom_progress")

            build, qa, platform, _ = self._write_source_fixture(root)
            platform_payload = json.loads(platform.read_text(encoding="utf-8"))
            platform_payload["qualityPreset"] = "TBD"
            platform.write_bytes(self.module.canonical_json(platform_payload))
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "PLATFORM_METADATA"):
                self.module.load_source_evidence(root, build, qa, platform, "pre_schema_v0_kingdom_progress")

            build, qa, platform, player = self._write_source_fixture(root)
            (player.parent / "AnotherLifeUnity_Data/globalgamemanagers").write_bytes(b"tampered")
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "BUILD_ARTIFACT"):
                self.module.load_source_evidence(root, build, qa, platform, "pre_schema_v0_kingdom_progress")

            build, qa, platform, _ = self._write_source_fixture(root)
            save_manifest_path = root / self.module.SOURCE_PATHS["save"]
            outside = save_manifest_path.parent.parent / "outside-save.json"
            outside.write_bytes(b"outside")
            save_manifest = json.loads(save_manifest_path.read_text(encoding="utf-8"))
            save_manifest["fixtures"][0]["file"] = "../outside-save.json"
            save_manifest["fixtures"][0]["sha256"] = hashlib.sha256(outside.read_bytes()).hexdigest()
            save_manifest_path.write_bytes(self.module.canonical_json(save_manifest))
            qa_payload = json.loads(qa.read_text(encoding="utf-8"))
            qa_payload["provenance"]["save"]["fixtureManifestSha256"] = hashlib.sha256(
                save_manifest_path.read_bytes()
            ).hexdigest()
            qa_payload["reportSha256"] = self.module.digest_document(qa_payload, "reportSha256")
            qa.write_bytes(self.module.canonical_json(qa_payload))
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "SAVE_FIXTURE_PATH"):
                self.module.load_source_evidence(root, build, qa, platform, "pre_schema_v0_kingdom_progress")


class RealmSliceCaptureRunTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._signer_directory = tempfile.TemporaryDirectory()
        signer_root = Path(cls._signer_directory.name)
        cls.signing_key = signer_root / "reviewer_ed25519"
        subprocess.run(
            ["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-f", str(cls.signing_key)],
            check=True,
            capture_output=True,
            text=True,
        )
        public_parts = cls.signing_key.with_suffix(".pub").read_text(encoding="utf-8").split()
        cls.allowed_signers = signer_root / "allowed_signers"
        cls.allowed_signers.write_text(
            f"reviewer-b {public_parts[0]} {public_parts[1]}\n",
            encoding="utf-8",
        )

    @classmethod
    def tearDownClass(cls):
        cls._signer_directory.cleanup()

    def setUp(self):
        self.module = load_module()
        self.policy = json.loads(POLICY.read_text(encoding="utf-8"))
        self.policy["_reviewAllowedSignersPath"] = self.allowed_signers

    def _envelope(self, mode: str) -> dict:
        namespace = self.policy["modeNamespaces"][mode]
        check = self.policy["checksByMode"][mode][0]
        envelope = {
            "candidateId": f"RSQ-Stonehold-{namespace}-r2.4.0-1",
            "evidencePacketId": f"RSQ-EV-Stonehold-{namespace}-r2.4.0-1",
            "realm": "Stonehold",
            "mode": mode,
            "checkId": check["id"],
            "scenarioId": check["scenarioId"],
            "scenarioVersion": check["scenarioVersion"],
            "build": {
                "buildId": "build-fixture",
                "sourceRevision": "a" * 40,
                "sourceTreeSha256": "b" * 64,
                "manifestSha256": "c" * 64,
                "artifactTreeSha256": "d" * 64,
                "target": "windows64-development",
            },
            "catalogs": {
                "enabledSceneManifestSha256": "e" * 64,
                "sceneCatalogSha256": "f" * 64,
                "contentCatalogSha256": "1" * 64,
                "narrativeCatalogSha256": "2" * 64,
            },
            "saveFixture": {
                "id": "pre_schema_v0_kingdom_progress",
                "sha256": "3" * 64,
                "fixtureManifestSha256": "4" * 64,
                "formatId": "anotherlife.local-save",
                "sourceSchemaVersion": 0,
                "expectedSchemaVersion": 1,
                "schemaDisposition": "LoadedPrimary",
            },
            "platform": {
                "platform": "WindowsPlayer",
                "deviceId": "device-pseudonym-001",
                "osVersion": "Windows 11",
                "graphicsApi": "Direct3D11",
                "qualityPreset": "pc_high_60",
                "viewport": {"width": 1920, "height": 1080},
                "renderScale": 1.0,
                "refreshRate": 60,
                "thresholdSetId": "t_4a5b066c",
                "captureTool": "ffmpeg",
                "captureToolVersion": "7.1",
            },
            "qa": {"runId": "qa-fixture", "reportSha256": "5" * 64, "profile": "full", "status": "passed"},
            "locale": "en-US",
            "inputClass": "keyboard_mouse",
            "accessibilityPreset": "default",
            "operator": "operator-a",
            "independentReviewer": "reviewer-b",
        }
        return envelope

    def _capture(self, policy, envelope, command, raw_root):
        check = next(row for row in policy["checksByMode"][envelope["mode"]] if row["id"] == envelope["checkId"])
        def passing_metric(name):
            if name == "liveSaveTouched":
                return False
            match_path = policy["metricSemantics"]["envelopeMatches"].get(name)
            if match_path:
                return self.module._nested_value(envelope, match_path)
            if name in policy["metricSemantics"]["minimumOnPass"]:
                return policy["metricSemantics"]["minimumOnPass"][name]
            if name in policy["metricSemantics"]["rangeOnPass"]:
                return policy["metricSemantics"]["rangeOnPass"][name]["maximum"]
            typed_non_empty = policy["metricSemantics"]["nonEmptyOnPass"]
            if name in typed_non_empty["nonEmptyStringArray"]:
                return ["fixture-value"]
            if name in typed_non_empty["positiveNumberMap"]:
                return {"p95": 16.6}
            if name in typed_non_empty["nonNegativeInteger"]:
                return 0
            if name in typed_non_empty["sha256"]:
                return "a" * 64
            if name in typed_non_empty["nonEmptyString"]:
                return "fixture-value"
            if name in policy["metricSemantics"]["positiveOnPass"]:
                return 1
            if name.endswith("Count"):
                return 0
            if name.endswith("Seconds") or "Duration" in name:
                return 1.0
            if name.endswith("Id"):
                return "fixture-id"
            if name.endswith("Sha256") or name.endswith("Hash"):
                return "a" * 64
            return True
        scenario_expected = envelope.get("scenarioExpectedMetrics", {})
        metrics = {
            name: scenario_expected.get(name, passing_metric(name))
            for name in check["metrics"]
        }
        for left, right in policy["metricSemantics"]["equalPairsOnPass"]:
            if left in metrics and right in metrics:
                if left in scenario_expected:
                    metrics[right] = scenario_expected[left]
                elif right in scenario_expected:
                    metrics[left] = scenario_expected[right]
        (raw_root / "screenshots").mkdir(parents=True)
        (raw_root / "video").mkdir(parents=True)
        (raw_root / "Player.log").write_text("packaged player\n", encoding="utf-8")
        (raw_root / check["structuredLog"]).write_text('{"event":"complete"}\n', encoding="utf-8")
        (raw_root / "screenshots" / "anchor.png").write_bytes(b"PNG-fixture")
        (raw_root / "video" / "continuous.mp4").write_bytes(b"MP4-fixture")
        result = {
            "executionState": "COMPLETE",
            "technicalResult": "PASS",
            "expectedResult": "all required rendering checks pass",
            "observedResult": "all required rendering checks passed",
            "reasonCode": "RSQ_OK",
            "defectIds": [],
            "scenarioDefinitionSha256": envelope["scenarioDefinitionSha256"],
            "metrics": metrics,
        }
        if check.get("performance"):
            (raw_root / "telemetry").mkdir(parents=True)
            (raw_root / "profiler").mkdir(parents=True)
            (raw_root / "telemetry" / "frames.json").write_text("{}\n", encoding="utf-8")
            (raw_root / "profiler" / "capture.raw").write_bytes(b"profiler-fixture")
        (raw_root / "result.json").write_bytes(self.module.canonical_json(result))
        return 0

    def _finalize(self, provisional, evidence_root):
        review = {
            "reviewer": provisional["independentReviewer"],
            "reviewedUtc": "2026-09-03T03:00:00Z",
            "reviewerDisposition": provisional["proposedTechnicalResult"],
        }
        candidate = self.module.prepare_review_manifest(self.policy, provisional, review)
        payload = evidence_root / "review-attestation.payload"
        payload.write_bytes(self.module.canonical_json(self.module.build_review_attestation(candidate)))
        subprocess.run(
            [
                "ssh-keygen", "-Y", "sign", "-q", "-f", str(self.signing_key),
                "-n", "anotherlife-rsq-v1", str(payload),
            ],
            check=True,
            capture_output=True,
            text=True,
        )
        signature_path = Path(f"{payload}.sig")
        signature = signature_path.read_text(encoding="utf-8")
        payload.unlink()
        signature_path.unlink()
        return self.module.finalize_review(
            self.policy, evidence_root, provisional, review, signature
        )

    def test_mode_isolated_capture_runs_emit_deterministic_valid_manifests(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            provisional_3d = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), root, Path("AnotherLifeUnity.exe"),
                capture_runner=self._capture, utc_now=clock,
            )
            clock = iter(("2026-09-03T02:00:00Z", "2026-09-03T02:01:00Z")).__next__
            provisional_2d = self.module.run_capture(
                self.policy, self._envelope("Kingdom2_5D"), root, Path("AnotherLifeUnity.exe"),
                capture_runner=self._capture, utc_now=clock,
            )
            self.assertEqual(provisional_3d["technicalResult"], "FAIL_CLOSED")
            self.assertIn("review.attestation", provisional_3d["missingArtifacts"])
            manifest_3d = self._finalize(provisional_3d, root)
            manifest_2d = self._finalize(provisional_2d, root)

            self.assertEqual(manifest_3d["technicalResult"], "PASS")
            self.assertEqual(manifest_2d["technicalResult"], "PASS")
            self.assertEqual(manifest_3d["reviewer"], "reviewer-b")
            self.assertEqual(
                manifest_3d["artifactIds"],
                [artifact["id"] for artifact in manifest_3d["artifacts"]],
            )
            self.assertEqual(
                manifest_3d["scenario"]["catalogSha256"],
                hashlib.sha256(SCENARIOS.read_bytes()).hexdigest(),
            )
            self.assertTrue(all("/3d/" in f"/{row['path']}/" for row in manifest_3d["artifacts"]))
            self.assertTrue(all("/2_5d/" in f"/{row['path']}/" for row in manifest_2d["artifacts"]))
            self.module.verify_manifest(root, manifest_3d, self.policy)
            self.module.verify_manifest(root, manifest_2d, self.policy)
            from jsonschema import Draft202012Validator, FormatChecker
            schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
            Draft202012Validator.check_schema(schema)
            Draft202012Validator(schema, format_checker=FormatChecker()).validate(manifest_3d)
            Draft202012Validator(schema, format_checker=FormatChecker()).validate(manifest_2d)

    def test_every_policy_check_emits_a_schema_valid_provisional_manifest(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            counter = {"i": 0}

            def utc_now():
                counter["i"] += 1
                return f"2026-09-03T01:{counter['i']:02d}:00Z"

            from jsonschema import Draft202012Validator, FormatChecker
            schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
            validator = Draft202012Validator(schema, format_checker=FormatChecker())
            captured = []
            for mode, checks in self.policy["checksByMode"].items():
                for check in checks:
                    envelope = self._envelope(mode)
                    envelope.update({
                        "checkId": check["id"],
                        "scenarioId": check["scenarioId"].format(realm="Stonehold"),
                        "scenarioVersion": check["scenarioVersion"],
                    })
                    manifest = self.module.run_capture(
                        self.policy, envelope, root, Path("AnotherLifeUnity.exe"),
                        capture_runner=self._capture, utc_now=utc_now,
                    )
                    with self.subTest(checkId=check["id"]):
                        self.assertEqual(manifest["technicalResult"], "FAIL_CLOSED")
                        self.assertEqual(manifest["proposedTechnicalResult"], "PASS")
                        self.assertEqual(manifest["checkId"], check["id"])
                        validator.validate(manifest)
                    captured.append(check["id"])
            self.assertEqual(len(captured), 24)
            self.assertIn("RSQ-3D-LOC-EN-001", captured)
            self.assertIn("RSQ-2_5D-LOC-KO-001", captured)

    def test_capture_fails_closed_when_required_media_is_missing(self):
        def missing_video(policy, envelope, command, raw_root):
            result = self._capture(policy, envelope, command, raw_root)
            (raw_root / "video" / "continuous.mp4").unlink()
            return result

        with tempfile.TemporaryDirectory() as temporary:
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), Path(temporary),
                Path("AnotherLifeUnity.exe"), capture_runner=missing_video, utc_now=clock,
            )
            self.assertEqual(manifest["technicalResult"], "FAIL_CLOSED")
            self.assertEqual(manifest["executionState"], "BLOCKED")
            self.assertEqual(manifest["reasonCode"], "RSQ_EVIDENCE_INCOMPLETE")
            self.assertIn("video", manifest["missingArtifacts"])

    def test_capture_rejects_non_independent_review_and_false_pass_with_defects(self):
        with tempfile.TemporaryDirectory() as temporary:
            envelope = self._envelope("Adventure3D")
            envelope["independentReviewer"] = f"  {envelope['operator'].upper()}  "
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "REVIEW_INDEPENDENCE"):
                self.module.run_capture(
                    self.policy, envelope, Path(temporary), Path("AnotherLifeUnity.exe"),
                    capture_runner=self._capture,
                )

        def contradictory(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["defectIds"] = ["RSQ-DEFECT-001"]
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), Path(temporary),
                Path("AnotherLifeUnity.exe"), capture_runner=contradictory, utc_now=clock,
            )
            self.assertEqual(manifest["technicalResult"], "FAIL_CLOSED")
            self.assertIn("result.pass_with_defects", manifest["missingArtifacts"])

    def test_result_placeholders_and_missing_metric_values_fail_closed(self):
        def placeholders(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["expectedResult"] = "TBD"
            first_metric = next(iter(result["metrics"]))
            result["metrics"][first_metric] = None
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), Path(temporary),
                Path("AnotherLifeUnity.exe"), capture_runner=placeholders, utc_now=clock,
            )
            self.assertEqual(manifest["technicalResult"], "FAIL_CLOSED")
            self.assertIn("result.expectedResult", manifest["missingArtifacts"])
            self.assertTrue(any(item.startswith("result.metrics.") for item in manifest["missingArtifacts"]))

        def false_metric(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["metrics"]["missingAssetCount"] = 1
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), Path(temporary),
                Path("AnotherLifeUnity.exe"), capture_runner=false_metric, utc_now=clock,
            )
            self.assertEqual(manifest["technicalResult"], "FAIL_CLOSED")
            self.assertIn("result.metrics.missingAssetCount", manifest["missingArtifacts"])

    def test_pass_metrics_must_match_the_pinned_scenario_definition(self):
        def noncanonical_anchors(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["metrics"]["requiredAnchors"] = ["not-a-canonical-anchor"]
            result["metrics"]["visitedAnchors"] = ["not-a-canonical-anchor"]
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            envelope = self._envelope("Adventure3D")
            check = next(
                item for item in self.policy["checksByMode"]["Adventure3D"]
                if item["id"] == "RSQ-3D-NAV-001"
            )
            envelope.update({
                "checkId": check["id"],
                "scenarioId": check["scenarioId"],
                "scenarioVersion": check["scenarioVersion"],
            })
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, envelope, Path(temporary), Path("AnotherLifeUnity.exe"),
                capture_runner=noncanonical_anchors, utc_now=clock,
            )

            self.assertEqual(manifest["technicalResult"], "FAIL_CLOSED")
            self.assertIn("result.metrics.requiredAnchors", manifest["missingArtifacts"])

        def failed_with_noncanonical_expected_anchors(policy, envelope, command, raw_root):
            status = noncanonical_anchors(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["technicalResult"] = "FAIL"
            result["defectIds"] = ["RSQ-DEFECT-NAV-001"]
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            envelope = self._envelope("Adventure3D")
            check = next(
                item for item in self.policy["checksByMode"]["Adventure3D"]
                if item["id"] == "RSQ-3D-NAV-001"
            )
            envelope.update({
                "checkId": check["id"],
                "scenarioId": check["scenarioId"],
                "scenarioVersion": check["scenarioVersion"],
            })
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, envelope, Path(temporary), Path("AnotherLifeUnity.exe"),
                capture_runner=failed_with_noncanonical_expected_anchors, utc_now=clock,
            )

            self.assertEqual(manifest["proposedTechnicalResult"], "FAIL_CLOSED")
            self.assertIn("result.metrics.requiredAnchors", manifest["missingArtifacts"])

    def test_performance_soak_and_percentile_values_are_bounded_and_finite(self):
        def invalid_performance(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["metrics"]["warmupSeconds"] = 0.000001
            result["metrics"]["measuredSeconds"] = 0.000001
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            envelope = self._envelope("Adventure3D")
            check = next(
                item for item in self.policy["checksByMode"]["Adventure3D"]
                if item["id"] == "RSQ-3D-PERF-001"
            )
            envelope.update({
                "checkId": check["id"],
                "scenarioId": check["scenarioId"],
                "scenarioVersion": check["scenarioVersion"],
            })
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, envelope, Path(temporary), Path("AnotherLifeUnity.exe"),
                capture_runner=invalid_performance, utc_now=clock,
            )

            self.assertIn("result.metrics.warmupSeconds", manifest["missingArtifacts"])
            self.assertIn("result.metrics.measuredSeconds", manifest["missingArtifacts"])

        def non_finite_percentile(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["metrics"]["frameTimePercentiles"] = {"p95": float("inf")}
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            envelope = self._envelope("Adventure3D")
            check = next(
                item for item in self.policy["checksByMode"]["Adventure3D"]
                if item["id"] == "RSQ-3D-PERF-001"
            )
            envelope.update({
                "checkId": check["id"],
                "scenarioId": check["scenarioId"],
                "scenarioVersion": check["scenarioVersion"],
            })
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, envelope, Path(temporary), Path("AnotherLifeUnity.exe"),
                capture_runner=non_finite_percentile, utc_now=clock,
            )

            self.assertIn("result.valid_json", manifest["missingArtifacts"])

    def test_save_schema_metrics_must_match_the_selected_fixture(self):
        def wrong_schema(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["metrics"].update({
                "fixtureId": envelope["saveFixture"]["id"],
                "schemaBefore": 999,
                "schemaAfter": 999,
            })
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            envelope = self._envelope("Adventure3D")
            check = next(
                item for item in self.policy["checksByMode"]["Adventure3D"]
                if item["id"] == "RSQ-3D-SAVE-001"
            )
            envelope.update({
                "checkId": check["id"],
                "scenarioId": check["scenarioId"],
                "scenarioVersion": check["scenarioVersion"],
            })
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, envelope, Path(temporary), Path("AnotherLifeUnity.exe"),
                capture_runner=wrong_schema, utc_now=clock,
            )

            self.assertIn("result.metrics.schemaBefore", manifest["missingArtifacts"])
            self.assertIn("result.metrics.schemaAfter", manifest["missingArtifacts"])

    def test_player_must_attest_the_exact_scenario_definition(self):
        def wrong_scenario_digest(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["scenarioDefinitionSha256"] = "0" * 64
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            manifest = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), Path(temporary),
                Path("AnotherLifeUnity.exe"), capture_runner=wrong_scenario_digest, utc_now=clock,
            )

            self.assertIn("result.scenarioDefinitionSha256", manifest["missingArtifacts"])

    def test_signed_manifest_cannot_override_the_inventoried_player_result(self):
        def failed_result(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result.update({
                "technicalResult": "FAIL",
                "reasonCode": "RSQ_REAL_DEFECT",
                "defectIds": ["RSQ-DEFECT-REAL-001"],
            })
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            provisional = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), root,
                Path("AnotherLifeUnity.exe"), capture_runner=failed_result, utc_now=clock,
            )
            self.assertEqual(provisional["proposedTechnicalResult"], "FAIL")

            forged = json.loads(json.dumps(provisional))
            forged.update({
                "proposedTechnicalResult": "PASS",
                "proposedReasonCode": "RSQ_OK",
                "defectIds": [],
            })
            forged["manifestSha256"] = self.module.digest_document(forged, "manifestSha256")
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "RESULT_BINDING"):
                self._finalize(forged, root)

            invented_result = json.loads(json.dumps(provisional))
            invented_result.update({
                "proposedTechnicalResult": "PASS",
                "proposedReasonCode": "RSQ_INVENTED_RESULT",
                "defectIds": [],
            })
            result_artifact = next(
                row for row in invented_result["artifacts"] if row["role"] == "result"
            )
            canonical_result_path = root / result_artifact["path"]
            substituted_result = json.loads(canonical_result_path.read_text(encoding="utf-8"))
            substituted_result.update({
                "technicalResult": "PASS",
                "reasonCode": "RSQ_INVENTED_RESULT",
                "defectIds": [],
            })
            alternate_result_path = canonical_result_path.with_name("invented-result.json")
            alternate_result_path.write_bytes(self.module.canonical_json(substituted_result))
            result_artifact.update({
                "path": alternate_result_path.resolve().relative_to(root.resolve()).as_posix(),
                "bytes": alternate_result_path.stat().st_size,
                "sha256": self.module.sha256_file(alternate_result_path),
            })
            invented_result["manifestSha256"] = self.module.digest_document(
                invented_result, "manifestSha256"
            )
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "ARTIFACT_ROLE_PATH"):
                self._finalize(invented_result, root)

    def test_existing_run_directory_is_a_collision_and_artifact_tamper_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            envelope = self._envelope("Adventure3D")
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            provisional = self.module.run_capture(
                self.policy, envelope, root, Path("AnotherLifeUnity.exe"),
                capture_runner=self._capture, utc_now=clock,
            )
            manifest = self._finalize(provisional, root)
            windows_alias = json.loads(json.dumps(provisional))
            harness = next(row for row in windows_alias["artifacts"] if row["role"] == "harness_log")
            video = next(row for row in windows_alias["artifacts"] if row["role"] == "video")
            prefix = video["path"].rsplit("/raw/", 1)[0]
            video.update({
                "path": prefix + "/raw/screenshots\\..\\harness.log",
                "bytes": harness["bytes"],
                "sha256": harness["sha256"],
            })
            windows_alias["manifestSha256"] = self.module.digest_document(
                windows_alias, "manifestSha256"
            )
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "ARTIFACT_PATH"):
                self.module.verify_manifest(root, windows_alias, self.policy)
            duplicate_target = json.loads(json.dumps(provisional))
            duplicate_harness = next(
                row for row in duplicate_target["artifacts"] if row["role"] == "harness_log"
            )
            duplicate_video = next(
                row for row in duplicate_target["artifacts"] if row["role"] == "video"
            )
            duplicate_video.update({
                "path": duplicate_harness["path"],
                "bytes": duplicate_harness["bytes"],
                "sha256": duplicate_harness["sha256"],
            })
            duplicate_target["manifestSha256"] = self.module.digest_document(
                duplicate_target, "manifestSha256"
            )
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "ARTIFACT_COLLISION"):
                self.module.verify_manifest(root, duplicate_target, self.policy)
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "PATH_COLLISION"):
                self.module.run_capture(
                    self.policy, envelope, root, Path("AnotherLifeUnity.exe"),
                    capture_runner=self._capture,
                )
            traversal = json.loads(json.dumps(manifest))
            prefix = traversal["artifacts"][0]["path"].rsplit("/raw/", 1)[0]
            traversal["artifacts"][0]["path"] = prefix + "/raw/../raw/harness.log"
            traversal["manifestSha256"] = self.module.digest_document(traversal, "manifestSha256")
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "REVIEW_SIGNATURE"):
                self.module.verify_manifest(root, traversal, self.policy)
            wrong_identity = json.loads(json.dumps(manifest))
            wrong_identity["candidateId"] = "RSQ-Eldergrove-3d-r2.4.0-1"
            wrong_identity["manifestSha256"] = self.module.digest_document(wrong_identity, "manifestSha256")
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "CANDIDATE_ID"):
                self.module.verify_manifest(root, wrong_identity, self.policy)
            wrong_artifact_ids = json.loads(json.dumps(manifest))
            wrong_artifact_ids["artifactIds"] = wrong_artifact_ids["artifactIds"][:-1]
            wrong_artifact_ids["manifestSha256"] = self.module.digest_document(
                wrong_artifact_ids, "manifestSha256"
            )
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "ARTIFACT_IDS"):
                self.module.verify_manifest(root, wrong_artifact_ids, self.policy)
            pass_with_defect = json.loads(json.dumps(manifest))
            pass_with_defect["defectIds"] = ["RSQ-DEFECT-001"]
            pass_with_defect["manifestSha256"] = self.module.digest_document(
                pass_with_defect, "manifestSha256"
            )
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "PASS_DEFECT"):
                self.module.verify_manifest(root, pass_with_defect, self.policy)
            wrong_policy = json.loads(POLICY.read_text(encoding="utf-8"))
            wrong_policy["protocolId"] = "RSQ-PROTOCOL-v9.9.9"
            wrong_policy["_scenarioCatalogPath"] = str(SCENARIOS)
            wrong_policy["_reviewAllowedSignersPath"] = str(self.allowed_signers)
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "POLICY_IDENTITY"):
                self.module.verify_manifest(root, manifest, wrong_policy)
            artifact = root / manifest["artifacts"][0]["path"]
            artifact.write_bytes(artifact.read_bytes() + b"tampered")
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "ARTIFACT_HASH"):
                self.module.verify_manifest(root, manifest, self.policy)

            logical = json.loads(json.dumps(manifest))
            logical["artifacts"] = [row for row in logical["artifacts"] if row["role"] != "video"]
            logical["manifestSha256"] = self.module.digest_document(logical, "manifestSha256")
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "REQUIRED_ARTIFACT"):
                self.module.verify_manifest(root, logical, self.policy)

    def test_invalid_review_signature_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            provisional = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), root,
                Path("AnotherLifeUnity.exe"), capture_runner=self._capture, utc_now=clock,
            )
            review = {
                "reviewer": "reviewer-b",
                "reviewedUtc": "2026-09-03T03:00:00Z",
                "reviewerDisposition": "PASS",
            }
            forged = "-----BEGIN SSH SIGNATURE-----\nforged\n-----END SSH SIGNATURE-----\n"
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "REVIEW_SIGNATURE"):
                self.module.finalize_review(self.policy, root, provisional, review, forged)
            self.assertEqual(list(root.rglob("reviewed-manifest.json")), [])

            stale_review = dict(review)
            stale_review["reviewedUtc"] = "2026-09-03T00:59:00Z"
            with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "REVIEW_TIMING"):
                self.module.prepare_review_manifest(self.policy, provisional, stale_review)

    def test_completed_fail_also_requires_and_verifies_independent_signature(self):
        def failed_check(policy, envelope, command, raw_root):
            status = self._capture(policy, envelope, command, raw_root)
            result_path = raw_root / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8"))
            result["technicalResult"] = "FAIL"
            result["reasonCode"] = "RSQ_OBSERVED_CONTRADICTION"
            result["defectIds"] = ["RSQ-DEFECT-001"]
            result_path.write_bytes(self.module.canonical_json(result))
            return status

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            provisional = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), root,
                Path("AnotherLifeUnity.exe"), capture_runner=failed_check, utc_now=clock,
            )
            self.assertEqual(provisional["proposedTechnicalResult"], "FAIL")
            final = self._finalize(provisional, root)
            self.assertEqual(final["technicalResult"], "FAIL")
            self.module.verify_manifest(root, final, self.policy)

    def test_internal_verification_failure_does_not_persist_a_pass_manifest(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            clock = iter(("2026-09-03T01:00:00Z", "2026-09-03T01:01:00Z")).__next__
            provisional = self.module.run_capture(
                self.policy, self._envelope("Adventure3D"), root,
                Path("AnotherLifeUnity.exe"), capture_runner=self._capture, utc_now=clock,
            )
            with mock.patch.object(
                self.module,
                "verify_manifest",
                side_effect=[
                    None,
                    self.module.RealmSliceEvidenceError("RSQ_INJECTED_VERIFY_FAILURE"),
                ],
            ):
                with self.assertRaisesRegex(self.module.RealmSliceEvidenceError, "INJECTED_VERIFY_FAILURE"):
                    self._finalize(provisional, root)
            self.assertEqual(list(root.rglob("reviewed-manifest.json")), [])


class RealmSliceCliTests(unittest.TestCase):
    def test_matrix_command_writes_the_mode_isolated_operator_inventory(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "matrix.json"
            status = module.main([
                "matrix", "--realm", "Stonehold", "--mode", "Adventure3D",
                "--output", str(output),
            ])
            payload = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(status, 0)
            self.assertEqual(payload["protocolId"], "RSQ-PROTOCOL-v1.0.0")
            self.assertEqual(payload["mode"], "Adventure3D")
            self.assertEqual(payload["modeNamespace"], "3d")
            self.assertEqual(len(payload["runs"]), 72)


if __name__ == "__main__":
    unittest.main()
