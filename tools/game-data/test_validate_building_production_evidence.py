#!/usr/bin/env python3
"""Source-only building evidence regressions; never mint or activate catalogs."""

from __future__ import annotations

import importlib.util
import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
VALIDATOR = Path(__file__).with_name("validate_building_production_evidence.py")


class BuildingProductionEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        spec = importlib.util.spec_from_file_location("building_evidence", VALIDATOR)
        cls.target = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.target)
        cls.packet = cls.target.authority.load_ledger_file(ROOT / cls.target.DEFAULT_PACKET_PATH)

    def test_current_packet_preserves_all_building_gaps(self) -> None:
        self.assertTrue(VALIDATOR.is_file(), "building evidence validator is missing")
        spec = importlib.util.spec_from_file_location("building_evidence", VALIDATOR)
        target = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(target)
        result = target.validate_file(ROOT / target.DEFAULT_PACKET_PATH, ROOT)
        self.assertEqual(15, result["buildingCount"])
        self.assertEqual(8, result["modelTupleCount"])
        self.assertEqual(15, result["missingProductionCount"])
        self.assertEqual(13, result["missingModelCount"])
        self.assertEqual(26, len(result["blockingIds"]))
        self.assertIs(result["productionEligible"], False)
        self.assertEqual([], result["outputPaths"])
        self.assertEqual([], result["activationTargets"])

    def test_rejects_source_and_authority_mutations(self) -> None:
        mutations = {
            "schema_bool": lambda p: p.update(schemaVersion=True),
            "eligible": lambda p: p.update(productionEligible=True),
            "resolved": lambda p: p["resolvedBlockerIds"].append("buildings.production_profiles"),
            "missing_blocker": lambda p: p["blockers"].pop(),
            "untyped_blocker": lambda p: p["blockers"][0].update(type="asset"),
            "missing_building": lambda p: p["buildings"].pop(),
            "reordered": lambda p: p["buildings"].reverse(),
            "duplicate": lambda p: p["buildings"].append(copy.deepcopy(p["buildings"][0])),
            "unknown_anchor": lambda p: p["buildings"][0].update(canonicalId="ManaShrine"),
            "alias": lambda p: p["buildings"][0]["progressionEvidence"].update(legacyBuildingId="townhall"),
            "level": lambda p: p["buildings"][0]["progressionEvidence"].update(maxLevel=11),
            "cost": lambda p: p["buildings"][0]["progressionEvidence"].update(costProfileId="fake"),
            "duration": lambda p: p["buildings"][0]["progressionEvidence"].update(durationProfileId="fake"),
            "prerequisite": lambda p: p["buildings"][0]["progressionEvidence"].update(prerequisiteProfileId="fake"),
            "realm": lambda p: p["buildings"][0]["progressionEvidence"].update(realmEligibilityProfileId="fake"),
            "fake_zero_profile": lambda p: p["buildings"][0].update(productionProfile={"outputs": [], "rate": 0}),
            "wire_promotion": lambda p: p["buildings"][0].update(productionProfile="resource_output"),
            "test_rate_promotion": lambda p: p["buildings"][1].update(productionProfile={"ratePerLevelPerSecond": 2.0}),
            "missing_fields": lambda p: p["buildings"][0].update(missingProductionFields=[]),
            "dimension_collapse": lambda p: p["buildings"][0].update(assetDimension="common"),
            "missing_model": lambda p: p["buildings"][0]["modelEvidence"].pop(),
            "guid": lambda p: p["buildings"][0]["modelEvidence"][0]["asset_ref"].update(guid="0" * 32),
            "sha": lambda p: p["buildings"][0]["modelEvidence"][0]["asset_ref"].update(sha256="0" * 64),
            "path": lambda p: p["buildings"][0]["modelEvidence"][0]["asset_ref"].update(path="../../outside"),
            "source_path": lambda p: p["sources"][0].update(path="../../outside"),
            "source_hash": lambda p: p["sources"][0].update(rawSha256="0" * 64),
            "source_revision": lambda p: p["sources"][0].update(sourceRevision="HEAD"),
            "missing_source": lambda p: p["sources"].pop(),
            "source_classification": lambda p: p["productionSourceObservation"].update(testFixtures="accepted"),
            "output": lambda p: p["generationGate"].update(outputPaths=["catalog-set.json"]),
            "activation": lambda p: p["generationGate"].update(activationTargets=["runtime"]),
            "unknown_key": lambda p: p.update(approval="approved"),
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label):
                candidate = copy.deepcopy(self.packet)
                mutate(candidate)
                with self.assertRaisesRegex(self.target.ValidationError, "exact pinned evidence"):
                    self.target.validate_packet(candidate, ROOT)

    def test_absence_cannot_be_promoted_to_a_zero_output_profile(self) -> None:
        candidate = copy.deepcopy(self.packet)
        candidate["buildings"][0]["productionProfile"] = {"outputs": [], "rate": 0}
        with self.assertRaisesRegex(self.target.ValidationError, "exact pinned evidence"):
            self.target.validate_packet(candidate, ROOT)

    def test_strict_json_and_checkout_portability(self) -> None:
        raw = self.target.authority.canonical_json(self.packet)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "packet.json"
            path.write_bytes(raw.replace(b"\n", b"\r\n"))
            self.assertEqual(self.packet, self.target.authority.load_ledger_file(path))
            for bad in (b"\xef\xbb\xbf" + raw, b"[]\n", b'{"a":1,"a":2}', b'{"a":NaN}', raw.replace(b"\n", b"\r", 1)):
                with self.subTest(raw=bad[:30]):
                    path.write_bytes(bad)
                    with self.assertRaises(self.target.ValidationError):
                        self.target.authority.load_ledger_file(path)

    def test_new_profile_evidence_requires_review_not_automatic_promotion(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / self.target.GAME_DATA
            target.mkdir(parents=True)
            self.target.verify_profile_absence(root)
            (target / "new-profile.json").write_text('{"catalogId":"kingdom_production_profile_v1"}', encoding="utf-8")
            with self.assertRaisesRegex(self.target.ValidationError, "new production profile evidence"):
                self.target.verify_profile_absence(root)

    def test_source_reader_rejects_missing_git_blob_and_checkout_drift(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "source.json"
            good = subprocess.CompletedProcess([], 0, b"{}\n", b"")
            with mock.patch.object(self.target.subprocess, "run", return_value=good):
                with self.assertRaisesRegex(self.target.ValidationError, "current source unavailable"):
                    self.target.read_source(root, "source.json")
                path.write_bytes(b"{}\r\n")
                self.assertEqual(b"{}\n", self.target.read_source(root, "source.json"))
                path.write_bytes(b"[]\n")
                with self.assertRaisesRegex(self.target.ValidationError, "current source drift"):
                    self.target.read_source(root, "source.json")
            with mock.patch.object(self.target.subprocess, "run", return_value=subprocess.CompletedProcess([], 1, b"", b"missing")):
                with self.assertRaisesRegex(self.target.ValidationError, "pinned Git source missing"):
                    self.target.read_source(root, "source.json")

    def test_model_provenance_checks_hash_and_meta_guid(self) -> None:
        candidate = copy.deepcopy(self.packet)
        candidate["buildings"][0]["modelEvidence"][0]["asset_ref"]["sha256"] = "0" * 64
        with self.assertRaisesRegex(self.target.ValidationError, "model SHA-256 mismatch"):
            self.target.verify_model_sources(candidate, ROOT)
        candidate = copy.deepcopy(self.packet)
        candidate["buildings"][0]["modelEvidence"][0]["asset_ref"]["guid"] = "0" * 32
        with self.assertRaisesRegex(self.target.ValidationError, "model GUID mismatch"):
            self.target.verify_model_sources(candidate, ROOT)

    def test_cli_is_deterministic_and_refuses_production_without_writes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "packet.json"
            raw = self.target.authority.canonical_json(self.packet)
            path.write_bytes(raw)
            command = [sys.executable, str(VALIDATOR), "--packet", str(path)]
            first = subprocess.run(command, cwd=directory, capture_output=True, check=False)
            second = subprocess.run(command + ["--require-production-eligible"], cwd=directory, capture_output=True, check=False)
            self.assertEqual(0, first.returncode, first.stderr)
            self.assertEqual(2, second.returncode, second.stderr)
            self.assertEqual(first.stdout, second.stdout)
            report = json.loads(first.stdout)
            self.assertEqual([], report["outputPaths"])
            self.assertEqual([], report["activationTargets"])
            self.assertEqual([path], list(Path(directory).iterdir()))
            self.assertEqual(raw, path.read_bytes())
            path.write_text("{}\n", encoding="utf-8")
            invalid = subprocess.run(command + ["--require-production-eligible"], cwd=directory, capture_output=True, check=False)
            self.assertEqual(1, invalid.returncode)
            self.assertEqual(b"", invalid.stdout)

    def test_required_ci_exercises_building_source_and_strict_gate(self) -> None:
        workflow = (ROOT / ".github/workflows/quality-gates.yml").read_text(encoding="utf-8")
        self.assertIn("python tools/game-data/test_validate_building_production_evidence.py", workflow)
        self.assertIn("python tools/game-data/validate_building_production_evidence.py --require-production-eligible", workflow)


if __name__ == "__main__":
    unittest.main()
