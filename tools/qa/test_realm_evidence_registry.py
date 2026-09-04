#!/usr/bin/env python3
"""Contract tests for the sequential realm evidence registry."""

from __future__ import annotations

import importlib.util
import copy
import hashlib
import hmac
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("realm_evidence_registry.py")
POLICY = Path(__file__).with_name("realm_evidence_registry_policy.v1.json")
HARNESS_POLICY = Path(__file__).with_name("realm_slice_evidence_policy.v1.json")
REPO_ROOT = Path(__file__).resolve().parents[2]
PACK_SCHEMA = REPO_ROOT / "unity/SharedContracts/realm-slice-evidence-pack.schema.json"
MANIFEST_SCHEMA = REPO_ROOT / "unity/SharedContracts/realm-slice-evidence-manifest.schema.json"
REGISTRY_SCHEMA = REPO_ROOT / "unity/SharedContracts/realm-slice-evidence-registry.schema.json"


def load_module():
    spec = importlib.util.spec_from_file_location("realm_evidence_registry", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load {SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RealmEvidenceRegistryTests(unittest.TestCase):
    SIGNING_KEY = b"test-evidence-owner-secret"
    OWNER_KEY = b"test-game-owner-secret"
    EVIDENCE_KEY_ID = "anotherlife-evidence-owner-v1"
    OWNER_KEY_ID = "anotherlife-game-owner-v1"

    @classmethod
    def setUpClass(cls):
        cls.registry_module = load_module()
        cls.harness, cls.harness_policy = cls.registry_module.load_harness_policy()
        cls._signer_directory = tempfile.TemporaryDirectory()
        cls._artifact_directory = tempfile.TemporaryDirectory()
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
        cls.harness_policy["_reviewAllowedSignersPath"] = str(cls.allowed_signers)
        original_expand = cls.harness.expand_run_specs

        def reduced_expand(policy, realm, mode):
            seen = set()
            reduced = []
            for spec in original_expand(policy, realm, mode):
                if spec["checkId"] in seen:
                    continue
                seen.add(spec["checkId"])
                reduced.append(spec)
            return reduced

        cls.harness.expand_run_specs = reduced_expand
        cls.full_cube_size = len(original_expand(cls.harness_policy, "Stonehold", "Adventure3D"))
        cls.artifact_root = Path(cls._artifact_directory.name)
        cls.pack_3d = cls._build_pack(
            cls.artifact_root,
            realm="Stonehold",
            mode="Adventure3D",
            sequence="1",
            qa_run="qa-Stonehold-3d-001",
            clock_start=3600,
        )
        cls.pack_2_5d = None
        cls.pack_3d_too_early = None
        cls.pack_3d_replacement = None

    @classmethod
    def tearDownClass(cls):
        cls._artifact_directory.cleanup()
        cls._signer_directory.cleanup()

    @classmethod
    def _ensure_pack_2_5d(cls):
        if cls.pack_2_5d is None:
            cls.pack_2_5d = cls._build_pack(
                cls.artifact_root,
                realm="Stonehold",
                mode="Kingdom2_5D",
                sequence="1",
                qa_run="qa-Stonehold-2_5d-001",
                clock_start=7200,
            )
        return cls.pack_2_5d

    @classmethod
    def _ensure_pack_3d_replacement(cls):
        if cls.pack_3d_replacement is None:
            cls.pack_3d_replacement = cls._build_pack(
                cls.artifact_root,
                realm="Stonehold",
                mode="Adventure3D",
                sequence="2",
                qa_run="qa-Stonehold-3d-002",
                clock_start=15000,
                supersedes="RSQ-EV-Stonehold-3d-candidate-1",
                reviewed_utc="2026-09-03T04:20:00Z",
                signed_utc="2026-09-03T04:25:00Z",
                valid_until="2026-10-03T04:25:00Z",
            )
        return cls.pack_3d_replacement

    def setUp(self):
        self.module = self.registry_module
        self.policy = self.module.load_policy(POLICY)

    def _keys(self):
        return {
            self.EVIDENCE_KEY_ID: self.SIGNING_KEY,
            self.OWNER_KEY_ID: self.OWNER_KEY,
        }

    @classmethod
    def _envelope(cls, mode, realm, check, sequence, qa_run, locale, input_class, preset):
        namespace = cls.harness_policy["modeNamespaces"][mode]
        return {
            "candidateId": f"RSQ-{realm}-{namespace}-candidate-{sequence}",
            "evidencePacketId": f"RSQ-EV-{realm}-{namespace}-candidate-{sequence}",
            "realm": realm,
            "mode": mode,
            "checkId": check["id"],
            "scenarioId": check["scenarioId"].format(realm=realm),
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
            "qa": {
                "runId": qa_run,
                "reportSha256": "5" * 64,
                "profile": "full",
                "status": "passed",
            },
            "locale": locale,
            "inputClass": input_class,
            "accessibilityPreset": preset,
            "operator": "operator-a",
            "independentReviewer": "reviewer-b",
        }

    @classmethod
    def _capture(cls, policy, envelope, command, raw_root):
        check = next(
            row for row in policy["checksByMode"][envelope["mode"]] if row["id"] == envelope["checkId"]
        )

        def passing_metric(name):
            if name == "liveSaveTouched":
                return False
            match_path = policy["metricSemantics"]["envelopeMatches"].get(name)
            if match_path:
                return cls.harness._nested_value(envelope, match_path)
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
        (raw_root / "screenshots").mkdir(parents=True, exist_ok=True)
        (raw_root / "video").mkdir(parents=True, exist_ok=True)
        (raw_root / "Player.log").write_text("packaged player\n", encoding="utf-8")
        (raw_root / check["structuredLog"]).write_text('{"event":"complete"}\n', encoding="utf-8")
        (raw_root / "screenshots" / "anchor.png").write_bytes(b"PNG-fixture")
        (raw_root / "video" / "continuous.mp4").write_bytes(b"MP4-fixture")
        if check.get("performance"):
            (raw_root / "telemetry").mkdir(parents=True, exist_ok=True)
            (raw_root / "profiler").mkdir(parents=True, exist_ok=True)
            (raw_root / "telemetry" / "frames.json").write_text("{}\n", encoding="utf-8")
            (raw_root / "profiler" / "capture.raw").write_bytes(b"profiler-fixture")
        result = {
            "executionState": "COMPLETE",
            "technicalResult": "PASS",
            "expectedResult": "all required checks pass",
            "observedResult": "all required checks passed",
            "reasonCode": "RSQ_OK",
            "defectIds": [],
            "scenarioDefinitionSha256": envelope["scenarioDefinitionSha256"],
            "metrics": metrics,
        }
        (raw_root / "result.json").write_bytes(cls.harness.canonical_json(result))
        return 0

    @classmethod
    def _finalize(cls, provisional, evidence_root, reviewed_utc):
        review = {
            "reviewer": provisional["independentReviewer"],
            "reviewedUtc": reviewed_utc,
            "reviewerDisposition": provisional["proposedTechnicalResult"],
        }
        candidate = cls.harness.prepare_review_manifest(cls.harness_policy, provisional, review)
        payload = evidence_root / f"review-attestation-{provisional['runId']}.payload"
        payload.write_bytes(cls.harness.canonical_json(cls.harness.build_review_attestation(candidate)))
        subprocess.run(
            [
                "ssh-keygen", "-Y", "sign", "-q", "-f", str(cls.signing_key),
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
        return cls.harness.finalize_review(
            cls.harness_policy, evidence_root, provisional, review, signature
        )

    @classmethod
    def _build_pack(
        cls,
        root,
        realm,
        mode,
        sequence,
        qa_run,
        clock_start,
        supersedes=None,
        reviewed_utc="2026-09-03T03:00:00Z",
        signed_utc="2026-09-03T03:15:00Z",
        valid_until="2026-10-03T03:15:00Z",
    ):
        module = cls.registry_module
        policy = cls.harness_policy
        counter = {"i": clock_start}

        def utc_now():
            counter["i"] += 1
            hours, rem = divmod(counter["i"], 3600)
            minutes, seconds = divmod(rem, 60)
            return f"2026-09-03T{hours:02d}:{minutes:02d}:{seconds:02d}Z"

        rows = []
        for spec in cls.harness.expand_run_specs(policy, realm, mode):
            check = next(item for item in policy["checksByMode"][mode] if item["id"] == spec["checkId"])
            envelope = cls._envelope(
                mode, realm, check, sequence, qa_run,
                spec["locale"], spec["inputClass"], spec["accessibilityPreset"],
            )
            provisional = cls.harness.run_capture(
                policy, envelope, root, Path("AnotherLifeUnity.exe"),
                capture_runner=cls._capture, utc_now=utc_now,
            )
            rows.append(cls._finalize(provisional, root, reviewed_utc))
        namespace = policy["modeNamespaces"][mode]
        packet = {
            "schemaVersion": 1,
            "protocolId": "RSQ-PROTOCOL-v1.0.0",
            "packetId": f"RSQ-EV-{realm}-{namespace}-candidate-{sequence}",
            "candidateId": f"RSQ-{realm}-{namespace}-candidate-{sequence}",
            "realm": realm,
            "realmOrdinal": ["Stonehold", "Eldergrove", "Crownlands", "Umbral"].index(realm) + 1,
            "mode": mode,
            "modeNamespace": namespace,
            "evidenceOwner": "evidence-owner",
            "evidenceOwnerKeyId": cls.EVIDENCE_KEY_ID,
            "independentReviewer": "reviewer-b",
            "signatureMethod": "hmac-sha256-v1",
            "signedUtc": signed_utc,
            "validUntilUtc": valid_until,
            "rowManifests": rows,
            "supersedes": supersedes,
        }
        return module.sign_evidence_pack(packet, cls.SIGNING_KEY)

    def _ingest(self, registry, packet, now="2026-09-03T03:20:00Z", root=None):
        return self.module.ingest_evidence_pack(
            registry,
            self.policy,
            packet,
            self._keys(),
            now,
            root or self.artifact_root,
            self.harness,
            self.harness_policy,
        )

    def _decision(self, registry, realm, kind, mode=None, action="APPROVE"):
        realm_state = registry["realms"][realm]
        packet_refs = []
        modes = [mode] if mode else list(self.policy["modes"])
        for mode_name in modes:
            packet = realm_state["modes"][mode_name]["currentPacket"]
            if packet is not None:
                packet_refs.append(copy.deepcopy(packet))
        decision_scope = mode or kind.lower()
        decision = {
            "schemaVersion": 1,
            "protocolId": self.policy["protocolId"],
            "decisionId": f"RSQ-OWNER-{kind}-{realm}-{decision_scope}-001",
            "realm": realm,
            "kind": kind,
            "mode": mode,
            "action": action,
            "owner": "game-owner",
            "ownerKeyId": self.OWNER_KEY_ID,
            "authorityTaskId": "t_owner_gate",
            "authorityEventId": "event-001",
            "packetRefs": packet_refs,
            "baselineId": f"RSQ-BASELINE-{realm}-{mode or 'creative'}-001",
            "limitations": ["no release approval inferred"],
            "signedUtc": "2026-09-03T03:30:00Z",
            "signatureMethod": self.policy["signatureMethod"],
            "supersedes": None,
        }
        return self.module.sign_owner_decision(decision, self.OWNER_KEY)

    def _signed_transition(self, record):
        signed = copy.deepcopy(record)
        signed.update({
            "authorizedBy": "game-owner",
            "ownerKeyId": self.OWNER_KEY_ID,
            "signatureMethod": self.policy["signatureMethod"],
            "signedUtc": record["occurredUtc"],
        })
        return self.module.sign_transition_record(signed, self.OWNER_KEY)

    def _resign_last_event(self, registry, signing_key):
        resigned = copy.deepcopy(registry)
        event = resigned["events"][-1]
        event.pop("eventSha256", None)
        event.pop("eventSignature", None)
        event["resultStateSha256"] = self.module._registry_state_sha256(resigned)
        digest = self.module.sha256_bytes(self.module.canonical_json(self.module._event_signing_material(event)))
        event["eventSignature"] = hmac.new(
            signing_key, digest.encode("ascii"), hashlib.sha256
        ).hexdigest()
        event["eventSha256"] = self.module.sha256_bytes(self.module.canonical_json(event))
        resigned.pop("registrySha256", None)
        resigned["registrySha256"] = self.module.sha256_bytes(self.module.canonical_json(resigned))
        return resigned

    def _record_decision(self, registry, decision, now="2026-09-03T03:31:00Z"):
        return self.module.record_owner_decision(
            registry,
            self.policy,
            decision,
            self._keys(),
            now,
            harness=self.harness,
            harness_policy=self.harness_policy,
        )

    def _approve_realm(self, registry, realm):
        for mode in self.policy["modes"]:
            registry = self._record_decision(
                registry,
                self._decision(registry, realm, "MODE", mode=mode),
            )
        registry = self._record_decision(
            registry,
            self._decision(registry, realm, "CREATIVE_VISUAL"),
        )
        return self._record_decision(
            registry,
            self._decision(
                registry,
                realm,
                "AUTHORIZATION",
                action=self.policy["advancementActions"][realm],
            ),
        )

    def test_policy_models_strict_order_and_four_separate_gate_states(self):
        self.assertEqual(
            self.policy["realmOrder"],
            ["Stonehold", "Eldergrove", "Crownlands", "Umbral"],
        )
        self.assertEqual(set(self.policy["modes"]), {"Adventure3D", "Kingdom2_5D"})
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        self.assertEqual(registry["activeRealm"], "Stonehold")
        self.assertEqual(registry["realms"]["Stonehold"]["entryGate"], "OPEN")
        self.assertEqual(registry["realms"]["Eldergrove"]["entryGate"], "CLOSED")
        self.assertEqual(
            set(registry["realms"]["Stonehold"]),
            {"entryGate", "modes", "creativeVisual", "ownerAuthorization"},
        )
        self.assertEqual(
            set(registry["realms"]["Stonehold"]["modes"]),
            {"Adventure3D", "Kingdom2_5D"},
        )

    def test_shared_schemas_cover_signatures_separate_modes_and_audit_history(self):
        pack_schema = json.loads(PACK_SCHEMA.read_text(encoding="utf-8"))
        manifest_schema = json.loads(MANIFEST_SCHEMA.read_text(encoding="utf-8"))
        registry_schema = json.loads(REGISTRY_SCHEMA.read_text(encoding="utf-8"))

        self.assertFalse(pack_schema["additionalProperties"])
        self.assertIn("evidenceOwnerSignature", pack_schema["required"])
        self.assertIn("rowManifests", pack_schema["required"])
        self.assertEqual(pack_schema["properties"]["rowManifests"]["minItems"], 72)
        self.assertEqual(self.full_cube_size, 72)
        self.assertEqual(
            pack_schema["properties"]["rowManifests"]["items"]["$ref"],
            "realm-slice-evidence-manifest.schema.json",
        )
        self.assertIn("reviewerSignature", manifest_schema["required"])
        self.assertIn("manifestSha256", manifest_schema["required"])
        self.assertEqual(
            set(pack_schema["properties"]["mode"]["enum"]),
            {"Adventure3D", "Kingdom2_5D"},
        )
        self.assertFalse(registry_schema["additionalProperties"])
        self.assertIn("events", registry_schema["required"])
        self.assertIn("reopens", registry_schema["required"])
        self.assertIn("rollbacks", registry_schema["required"])
        self.assertIn("registrySha256", registry_schema["required"])
        self.assertIn("policySha256", registry_schema["required"])
        self.assertIn("eventSignature", registry_schema["$defs"]["event"]["required"])

    def test_cli_initializes_and_verifies_a_durable_registry(self):
        with tempfile.TemporaryDirectory() as temporary:
            registry_path = Path(temporary) / "registry.json"
            self.assertEqual(
                self.module.main([
                    "--policy", str(POLICY),
                    "init",
                    "--registry", str(registry_path),
                    "--created-utc", "2026-09-03T00:00:00Z",
                ]),
                0,
            )
            registry = json.loads(registry_path.read_text(encoding="utf-8"))
            self.assertEqual(registry["activeRealm"], "Stonehold")
            self.assertEqual(
                self.module.main([
                    "--policy", str(POLICY),
                    "verify",
                    "--registry", str(registry_path),
                ]),
                0,
            )
            with self.assertRaisesRegex(self.module.RealmEvidenceError, "already exists"):
                self.module.initialize_registry_file(
                    registry_path,
                    self.policy,
                    "2026-09-03T00:00:00Z",
                )

    def test_signed_mode_pack_is_ingested_but_later_realm_is_blocked(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        stonehold = copy.deepcopy(self.pack_3d)
        save_id, save_digest = self.module._packet_save_fixture(stonehold)
        self.assertEqual(save_id, "pre_schema_v0_kingdom_progress")
        self.assertEqual(len(stonehold["rowManifests"]), 12)
        updated = self._ingest(registry, stonehold)
        self.assertEqual(
            updated["realms"]["Stonehold"]["modes"]["Adventure3D"]["qualification"],
            "QUALIFIED",
        )
        self.assertEqual(len(updated["evidencePackets"]), 1)
        self.assertEqual(len(updated["events"]), 1)
        self.assertEqual(registry["events"], [], "registry updates must be copy-on-write")

        eldergrove = copy.deepcopy(self.pack_3d)
        eldergrove["realm"] = "Eldergrove"
        eldergrove["realmOrdinal"] = 2
        eldergrove["packetId"] = "RSQ-EV-Eldergrove-3d-candidate-1"
        eldergrove["candidateId"] = "RSQ-Eldergrove-3d-candidate-1"
        eldergrove = self.module.sign_evidence_pack(eldergrove, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "realm gate is closed"):
            self._ingest(updated, eldergrove)

    def test_unsigned_and_stale_packs_are_rejected(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        unsigned = copy.deepcopy(self.pack_3d)
        unsigned.pop("evidenceOwnerSignature")
        with self.assertRaises(self.module.RealmEvidenceError):
            self._ingest(registry, unsigned)

        stale = copy.deepcopy(self.pack_3d)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "stale"):
            self._ingest(registry, stale, now="2026-10-04T03:15:00Z")

    def test_forged_detached_row_signature_is_independently_rejected(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        forged = copy.deepcopy(self.pack_3d)
        forged["rowManifests"][0]["reviewerSignature"] = (
            "-----BEGIN SSH SIGNATURE-----\nforged\n-----END SSH SIGNATURE-----\n"
        )
        forged["rowManifests"][0]["manifestSha256"] = self.harness.digest_document(
            forged["rowManifests"][0], "manifestSha256"
        )
        forged = self.module.sign_evidence_pack(forged, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "signature"):
            self._ingest(registry, forged)

    def test_policy_event_state_and_append_only_history_are_authenticated(self):
        initial = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        self.assertEqual(initial["policySha256"], self.module.policy_sha256(self.policy))
        updated = self._ingest(initial, copy.deepcopy(self.pack_3d))
        self.assertIn("eventSignature", updated["events"][0])
        self.assertTrue(
            self.module.verify_registry(
                updated, self.policy, self._keys(),
                harness=self.harness, harness_policy=self.harness_policy,
            )
        )

        tampered = copy.deepcopy(updated)
        tampered["realms"]["Stonehold"]["modes"]["Adventure3D"]["ownerApproval"] = "APPROVED"
        tampered.pop("registrySha256")
        tampered["registrySha256"] = self.module.sha256_bytes(self.module.canonical_json(tampered))
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "authenticated state"):
            self.module.verify_registry(
                tampered, self.policy, self._keys(),
                harness=self.harness, harness_policy=self.harness_policy,
            )

        impossible = self._resign_last_event(tampered, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "impossible"):
            self.module.verify_registry(
                impossible, self.policy, self._keys(),
                harness=self.harness, harness_policy=self.harness_policy,
            )

        record_tamper = copy.deepcopy(updated)
        packet_id = "RSQ-EV-Stonehold-3d-candidate-1"
        record_tamper["evidencePackets"][packet_id]["rowManifests"][0]["build"]["buildId"] = "forged-build"
        record_tamper = self._resign_last_event(record_tamper, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "signature"):
            self.module.verify_registry(
                record_tamper, self.policy, self._keys(),
                harness=self.harness, harness_policy=self.harness_policy,
            )

        with self.assertRaisesRegex(self.module.RealmEvidenceError, "append-only"):
            self.module.verify_append_only(updated, initial)

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_path = root / "base-registry.json"
            candidate_path = root / "candidate-registry.json"
            self.module._write_registry_file(base_path, initial)
            self.module._write_registry_file(candidate_path, updated)
            self.assertEqual(
                self.module.main([
                    "--policy", str(POLICY),
                    "verify-append-only",
                    "--base", str(base_path),
                    "--registry", str(candidate_path),
                ]),
                2,
            )

    def test_zero_event_registry_rejects_unrecorded_state_rewrite(self):
        base = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        current = copy.deepcopy(base)
        current["activeRealm"] = "Eldergrove"
        current["realms"]["Stonehold"]["entryGate"] = "CLOSED"
        current["realms"]["Eldergrove"]["entryGate"] = "OPEN"
        current.pop("registrySha256")
        current["registrySha256"] = self.module.sha256_bytes(self.module.canonical_json(current))

        self.assertTrue(self.module.verify_registry(current, self.policy))
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "without an appended event"):
            self.module.verify_append_only(base, current)

    def test_impossible_empty_mode_statuses_are_rejected(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        for field, value in (
            ("ownerApproval", "BOGUS"),
            ("contentPath", "ENABLED_APPROVED"),
        ):
            tampered = copy.deepcopy(registry)
            tampered["realms"]["Stonehold"]["modes"]["Adventure3D"][field] = value
            tampered.pop("registrySha256")
            tampered["registrySha256"] = self.module.sha256_bytes(self.module.canonical_json(tampered))
            with self.assertRaisesRegex(self.module.RealmEvidenceError, "mode state"):
                self.module.verify_registry(tampered, self.policy)

    def test_cross_mode_artifact_identity_is_rejected(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        packet = copy.deepcopy(self.pack_3d)
        packet["rowManifests"][0]["artifacts"][0]["path"] = "2_5d/foreign.log"
        packet = self.module.sign_evidence_pack(packet, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "merged or mismatched"):
            self._ingest(registry, packet)

    def test_stale_evidence_cannot_receive_owner_approval(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        registry = self._ingest(registry, copy.deepcopy(self.pack_3d))
        decision = self._decision(registry, "Stonehold", "MODE", mode="Adventure3D")
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "stale"):
            self.module.record_owner_decision(
                registry,
                self.policy,
                decision,
                self._keys(),
                "2026-10-04T03:15:00Z",
                harness=self.harness,
                harness_policy=self.harness_policy,
            )

    def test_owner_decision_supersession_and_pack_replacement_require_explicit_history(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        registry = self._ingest(registry, copy.deepcopy(self.pack_3d))
        first = self._decision(registry, "Stonehold", "MODE", mode="Adventure3D")
        registry = self._record_decision(registry, first)

        second = self._decision(registry, "Stonehold", "MODE", mode="Adventure3D")
        second["decisionId"] = "RSQ-OWNER-MODE-Stonehold-Adventure3D-002"
        second = self.module.sign_owner_decision(second, self.OWNER_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "supersede"):
            self._record_decision(registry, second, now="2026-09-03T03:32:00Z")

        replacement = copy.deepcopy(self._ensure_pack_3d_replacement())
        replacement["supersedes"] = "RSQ-EV-Stonehold-3d-candidate-1"
        replacement = self.module.sign_evidence_pack(replacement, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "reopen"):
            self._ingest(registry, replacement, now="2026-09-03T04:30:00Z")

    def test_incomplete_review_row_is_rejected(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        packet = copy.deepcopy(self.pack_3d)
        packet["rowManifests"][0].pop("reviewedUtc")
        packet = self.module.sign_evidence_pack(packet, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "check is incomplete"):
            self._ingest(registry, packet)

        missing_row = copy.deepcopy(self.pack_3d)
        missing_row["rowManifests"] = missing_row["rowManifests"][:-1]
        missing_row = self.module.sign_evidence_pack(missing_row, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "incomplete or merged"):
            self._ingest(registry, missing_row)

    def test_mismatched_mode_identity_is_rejected_even_when_resigned(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        packet = copy.deepcopy(self.pack_3d)
        packet["packetId"] = "RSQ-EV-Stonehold-2_5d-candidate-1"
        packet["candidateId"] = "RSQ-Stonehold-2_5d-candidate-1"
        packet = self.module.sign_evidence_pack(packet, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "mode identity"):
            self._ingest(registry, packet)

    def test_all_four_owner_decisions_are_required_before_next_realm_opens(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        registry = self._ingest(registry, copy.deepcopy(self.pack_3d))
        registry = self._ingest(registry, copy.deepcopy(self._ensure_pack_2_5d()))

        premature = self._decision(
            registry, "Stonehold", "AUTHORIZATION", action="ADVANCE_TO_ELDERGROVE"
        )
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "prior owner approvals"):
            self._record_decision(registry, premature)

        for mode in self.policy["modes"]:
            registry = self._record_decision(
                registry,
                self._decision(registry, "Stonehold", "MODE", mode=mode),
            )
        registry = self._record_decision(
            registry,
            self._decision(registry, "Stonehold", "CREATIVE_VISUAL"),
        )
        registry = self._record_decision(
            registry,
            self._decision(
                registry, "Stonehold", "AUTHORIZATION", action="ADVANCE_TO_ELDERGROVE"
            ),
        )

        self.assertEqual(registry["realms"]["Stonehold"]["entryGate"], "APPROVED")
        self.assertEqual(registry["realms"]["Eldergrove"]["entryGate"], "OPEN")
        self.assertEqual(registry["activeRealm"], "Eldergrove")
        self.assertEqual(registry["realms"]["Stonehold"]["creativeVisual"]["status"], "APPROVED")
        self.assertEqual(registry["realms"]["Stonehold"]["ownerAuthorization"]["status"], "APPROVED")
        self.assertEqual(len(registry["events"]), 6)

    def test_scoped_reopen_and_rollback_preserve_unaffected_mode_evidence_and_history(self):
        registry = self.module.create_registry(self.policy, "2026-09-03T00:00:00Z")
        registry = self._ingest(registry, copy.deepcopy(self.pack_3d))
        registry = self._ingest(registry, copy.deepcopy(self._ensure_pack_2_5d()))
        registry = self._approve_realm(registry, "Stonehold")

        reopened = self.module.reopen_scope(
            registry,
            self.policy,
            self._signed_transition({
                "reopenId": "RSQ-REOPEN-Stonehold-001",
                "trigger": "realm_art",
                "realm": "Stonehold",
                "affectedModes": ["Adventure3D"],
                "dependentRealms": [],
                "impactReason": "Stonehold 3D material source changed; 2.5D does not consume it.",
                "authorityTaskId": "t_art_change",
                "authorityEventId": "event-reopen-001",
                "occurredUtc": "2026-09-03T04:00:00Z",
            }),
            self._keys(),
            "2026-09-03T04:00:00Z",
            harness=self.harness,
            harness_policy=self.harness_policy,
        )
        three_d = reopened["realms"]["Stonehold"]["modes"]["Adventure3D"]
        two_five_d = reopened["realms"]["Stonehold"]["modes"]["Kingdom2_5D"]
        self.assertEqual(three_d["qualification"], "REOPENED")
        self.assertEqual(three_d["ownerApproval"], "REOPENED")
        self.assertTrue(three_d["rerunRequired"])
        self.assertEqual(three_d["contentPath"], "DISABLED_PENDING_RERUN")
        self.assertEqual(two_five_d["qualification"], "QUALIFIED")
        self.assertEqual(two_five_d["ownerApproval"], "APPROVED")
        self.assertEqual(two_five_d["contentPath"], "ENABLED_APPROVED")
        self.assertEqual(reopened["realms"]["Stonehold"]["creativeVisual"]["status"], "REOPENED")
        self.assertEqual(reopened["realms"]["Stonehold"]["ownerAuthorization"]["status"], "SUSPENDED")
        self.assertEqual(reopened["realms"]["Eldergrove"]["entryGate"], "SUSPENDED")
        self.assertEqual(len(reopened["evidencePackets"]), 2)
        self.assertEqual(len(reopened["ownerDecisions"]), 4)

        save_id, save_digest = self.module._packet_save_fixture(
            registry["evidencePackets"]["RSQ-EV-Stonehold-3d-candidate-1"]
        )
        rolled_back = self.module.record_rollback(
            reopened,
            self.policy,
            self._signed_transition({
                "rollbackId": "RSQ-ROLLBACK-Stonehold-001",
                "realm": "Stonehold",
                "affectedModes": ["Adventure3D"],
                "reason": "Contain the reopened 3D path pending a complete replacement pack.",
                "executedBy": "rollback-operator",
                "preserveEvidence": True,
                "preserveSaves": True,
                "disableOnlyAffectedPaths": True,
                "authorityTaskId": "t_art_change",
                "authorityEventId": "event-rollback-001",
                "baselineRefs": [{
                    "mode": "Adventure3D",
                    "baselineId": "RSQ-BASELINE-Stonehold-Adventure3D-001",
                    "packetId": "RSQ-EV-Stonehold-3d-candidate-1",
                    "manifestSha256": registry["realms"]["Stonehold"]["modes"]["Adventure3D"]["currentPacket"]["manifestSha256"],
                    "ownerDecisionId": "RSQ-OWNER-MODE-Stonehold-Adventure3D-001",
                }],
                "saveSnapshots": [{
                    "mode": "Adventure3D",
                    "saveFixtureId": save_id,
                    "saveFixtureSha256": save_digest,
                }],
                "occurredUtc": "2026-09-03T04:05:00Z",
            }),
            self._keys(),
            "2026-09-03T04:05:00Z",
            self.artifact_root,
            harness=self.harness,
            harness_policy=self.harness_policy,
        )
        rolled_three_d = rolled_back["realms"]["Stonehold"]["modes"]["Adventure3D"]
        self.assertEqual(rolled_three_d["contentPath"], "ROLLED_BACK_TO_APPROVED_BASELINE")
        self.assertTrue(rolled_three_d["rerunRequired"])
        self.assertEqual(
            rolled_back["rollbacks"]["RSQ-ROLLBACK-Stonehold-001"]["targets"][0]["baselineId"],
            "RSQ-BASELINE-Stonehold-Adventure3D-001",
        )
        self.assertTrue(
            self.module.verify_registry(
                rolled_back, self.policy, self._keys(),
                harness=self.harness, harness_policy=self.harness_policy,
            )
        )

        replacement = copy.deepcopy(self._ensure_pack_3d_replacement())
        replacement["supersedes"] = None
        replacement = self.module.sign_evidence_pack(replacement, self.SIGNING_KEY)
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "supersede"):
            self._ingest(rolled_back, replacement, now="2026-09-03T04:30:00Z")

        replacement = copy.deepcopy(self._ensure_pack_3d_replacement())
        replacement["supersedes"] = "RSQ-EV-Stonehold-3d-candidate-1"
        replacement = self.module.sign_evidence_pack(replacement, self.SIGNING_KEY)
        rerun = self._ingest(rolled_back, replacement, now="2026-09-03T04:30:00Z")
        self.assertEqual(
            rerun["realms"]["Stonehold"]["modes"]["Adventure3D"]["qualification"],
            "QUALIFIED",
        )
        self.assertTrue(rerun["realms"]["Stonehold"]["modes"]["Adventure3D"]["rerunRequired"])
        self.assertEqual(
            rerun["realms"]["Stonehold"]["modes"]["Kingdom2_5D"]["ownerApproval"],
            "APPROVED",
        )

        tampered = copy.deepcopy(rolled_back)
        tampered["events"][0]["kind"] = "HISTORY_REWRITTEN"
        tampered_without_digest = copy.deepcopy(tampered)
        tampered_without_digest.pop("registrySha256")
        tampered["registrySha256"] = self.module.sha256_bytes(
            self.module.canonical_json(tampered_without_digest)
        )
        with self.assertRaisesRegex(self.module.RealmEvidenceError, "hash chain"):
            self.module.verify_registry(
                tampered, self.policy, self._keys(),
                harness=self.harness, harness_policy=self.harness_policy,
            )


if __name__ == "__main__":
    unittest.main()
