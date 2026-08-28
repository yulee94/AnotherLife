"""Integration tests for the portable AnotherLife Blender source validator."""

from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[2]
VALIDATOR = REPOSITORY_ROOT / "tools" / "blender" / "validate_al_asset_sources.py"
EXPORTER = REPOSITORY_ROOT / "tools" / "blender" / "export_al_asset_candidate.py"
LANDMARK_AUTHORER = (
    REPOSITORY_ROOT / "tools" / "blender" / "author_neutral_terrain_landmark_kit.py"
)
REVIEW_RENDERER = (
    REPOSITORY_ROOT
    / "tools"
    / "blender"
    / "render_al_asset_review_contact_sheet.py"
)
TRANSFORM_ANALYZER = (
    REPOSITORY_ROOT / "tools" / "blender" / "analyze_al_transform_normalization.py"
)
REMEDIATOR = REPOSITORY_ROOT / "tools" / "blender" / "remediate_al_asset_sources.py"
MANIFEST = (
    REPOSITORY_ROOT / "unity" / "ArtSource" / "al_blender_source_validation.v1.json"
)
HALL_SOURCE_ID = "neutral-covenant-hall-working-v001"
SLAGWHISTLE_SOURCE_ID = "slagwhistle-burrower-working-v001"
LANDMARK_KIT_SOURCE_ID = "neutral-covenant-terrain-landmark-kit-working-v001"
LANDMARK_KIT_SEMANTIC_SHA256 = (
    "f7c3f2b9ea440ba236b36463661a47f9a79600a7785126de2c831c3bc749cad6"
)
HALL_TECHNICAL_OBJECTS = (
    "COL_NeutralCovenantHall_Walkable_00",
    "NAV_NeutralCovenantHall_Walkable_00",
    "SOCKET_Entrance_00",
)
HALL_TECHNICAL_COLLECTIONS = ("AL_COLLISION", "AL_NAVIGATION", "AL_SOCKETS")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


class BlenderSourceValidatorIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.blender = shutil.which("blender")
        if cls.blender is None:
            raise unittest.SkipTest("Blender executable is not available")

    def _run(
        self,
        source_id: str,
        *,
        manifest: Path = MANIFEST,
        fail_on_gaps: bool = False,
    ) -> tuple[subprocess.CompletedProcess[str], dict]:
        with tempfile.TemporaryDirectory(prefix="al-blender-validator-") as temp_dir:
            output = Path(temp_dir) / "report.json"
            command = [
                self.blender,
                "--background",
                "--python-exit-code",
                "1",
                "--python",
                str(VALIDATOR),
                "--",
                "--manifest",
                str(manifest),
                "--source",
                source_id,
                "--output",
                str(output),
            ]
            if fail_on_gaps:
                command.append("--fail-on-gaps")
            completed = subprocess.run(
                command,
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            report = json.loads(output.read_text(encoding="utf-8"))
            return completed, report

    def test_neutral_hall_reopens_with_exact_footprint_and_no_hard_errors(self) -> None:
        completed, report = self._run(HALL_SOURCE_ID, fail_on_gaps=True)

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        source = report["sources"][0]
        self.assertEqual([], source["errors"])
        self.assertEqual([], source["gaps"])
        self.assertEqual(
            3200, source["metrics"]["lods"]["runtime-candidate"]["triangles"]
        )
        self.assertEqual({"X": 8.0, "Z": 12.0}, source["metrics"]["dimensions"])
        self.assertAlmostEqual(0.0, source["metrics"]["ground"]["actual"], places=6)
        helpers = source["metrics"]["promotionObjects"]
        self.assertEqual(set(HALL_TECHNICAL_OBJECTS), set(helpers))
        self.assertEqual("MESH", helpers[HALL_TECHNICAL_OBJECTS[0]]["type"])
        self.assertEqual(8, helpers[HALL_TECHNICAL_OBJECTS[0]]["vertices"])
        self.assertEqual(6, helpers[HALL_TECHNICAL_OBJECTS[0]]["polygons"])
        self.assertEqual("MESH", helpers[HALL_TECHNICAL_OBJECTS[1]]["type"])
        self.assertEqual(4, helpers[HALL_TECHNICAL_OBJECTS[1]]["vertices"])
        self.assertEqual(1, helpers[HALL_TECHNICAL_OBJECTS[1]]["polygons"])
        self.assertAlmostEqual(
            1.0,
            helpers[HALL_TECHNICAL_OBJECTS[1]]["minimumUpwardNormal"],
            places=6,
        )
        self.assertEqual("EMPTY", helpers[HALL_TECHNICAL_OBJECTS[2]]["type"])
        collections = source["metrics"]["promotionCollections"]
        self.assertEqual(set(HALL_TECHNICAL_COLLECTIONS), set(collections))
        for details in collections.values():
            self.assertEqual(HALL_SOURCE_ID, details["assetSourceId"])
            self.assertEqual(1, details["schemaVersion"])
            self.assertGreater(details["users"], 0)

    def test_neutral_landmark_kit_has_reducing_lod_families_and_helpers(
        self,
    ) -> None:
        completed, report = self._run(LANDMARK_KIT_SOURCE_ID, fail_on_gaps=True)

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        source = report["sources"][0]
        self.assertEqual("candidate_valid", source["status"])
        self.assertEqual("review-candidate", source["approvalState"])
        self.assertEqual([], source["errors"])
        self.assertEqual([], source["gaps"])
        expected_lods = {
            "path-beacon": [736, 220, 48],
            "trail-post": [412, 132, 24],
            "boundary-wall": [692, 176, 12],
        }
        lod_metrics = source["metrics"]["lods"]
        for family, expected_triangles in expected_lods.items():
            family_metrics = [
                details
                for details in lod_metrics.values()
                if details["family"] == family
            ]
            self.assertEqual(
                expected_triangles,
                [details["triangles"] for details in family_metrics],
            )
            self.assertIsNone(family_metrics[0]["ratioToPrevious"])
            self.assertTrue(
                all(
                    0.0 < details["ratioToPrevious"] < 1.0
                    for details in family_metrics[1:]
                )
            )

        pivots = source["metrics"]["pivots"]
        self.assertEqual(15, len(pivots))
        self.assertTrue(all(location == [0.0, 0.0, 0.0] for location in pivots.values()))
        helpers = source["metrics"]["promotionObjects"]
        self.assertEqual(10, len(helpers))
        self.assertEqual(
            3, sum(details["type"] == "MESH" and name.startswith("COL_") for name, details in helpers.items())
        )
        self.assertEqual(
            3, sum(details["type"] == "MESH" and name.startswith("NAVEX_") for name, details in helpers.items())
        )
        self.assertEqual(
            4, sum(details["type"] == "EMPTY" and name.startswith("SOCKET_") for name, details in helpers.items())
        )
        self.assertEqual(
            {"AL_RENDER", "AL_COLLISION", "AL_NAVIGATION", "AL_SOCKETS"},
            set(source["metrics"]["promotionCollections"]),
        )

    def test_slagwhistle_reopens_inside_lod0_rig_and_weight_budgets(self) -> None:
        completed, report = self._run("slagwhistle-burrower-working-v001")

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        source = report["sources"][0]
        self.assertEqual([], source["errors"])
        self.assertEqual(9200, source["metrics"]["lods"]["LOD0"]["triangles"])
        self.assertEqual(38, source["metrics"]["armature"]["deformBones"])
        self.assertEqual(4, source["metrics"]["armature"]["maxInfluencesPerVertex"])

    def test_character_audit_reports_keyed_action_and_per_mesh_weight_risk(
        self,
    ) -> None:
        completed, report = self._run(
            "crownlands-champion-male-base-working-v001"
        )

        self.assertEqual(1, completed.returncode, completed.stdout + completed.stderr)
        source = report["sources"][0]
        action = source["metrics"]["actionDetails"]["ChampionWalk"]
        self.assertEqual([1.0, 32.0], action["frameRange"])
        self.assertEqual(249, action["fCurves"])
        self.assertEqual(7468, action["keyframes"])
        self.assertEqual(["Armature"], action["assignedObjects"])
        skin = source["metrics"]["armature"]["perSkinnedObject"]["ChampionBase"]
        self.assertEqual(10, skin["maximumInfluences"])
        self.assertEqual(2412, skin["verticesOverInfluenceLimit"])
        self.assertAlmostEqual(
            0.206957,
            skin["prunePreview"]["maximumDiscardedWeight"],
            places=6,
        )

    def test_fail_on_gaps_distinguishes_promotion_from_mvp_iteration(self) -> None:
        completed, report = self._run(SLAGWHISTLE_SOURCE_ID, fail_on_gaps=True)

        self.assertEqual(2, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("candidate_with_gaps", report["status"])
        self.assertGreater(report["summary"]["promotionGaps"], 0)
        self.assertEqual(0, report["summary"]["errors"])

    def test_hash_drift_is_a_hard_error(self) -> None:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        manifest["sources"] = [
            source
            for source in manifest["sources"]
            if source["id"] == "neutral-covenant-hall-working-v001"
        ]
        manifest["sources"][0]["sha256"] = "0" * 64

        with tempfile.TemporaryDirectory(prefix="al-blender-manifest-") as temp_dir:
            drifted_manifest = Path(temp_dir) / "manifest.json"
            drifted_manifest.write_text(
                json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
            )
            completed, report = self._run(
                "neutral-covenant-hall-working-v001", manifest=drifted_manifest
            )

        self.assertEqual(1, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("invalid", report["status"])
        codes = {error["code"] for error in report["sources"][0]["errors"]}
        self.assertIn("source_hash_mismatch", codes)

    def test_manifest_cross_references_reject_unknown_export_lod(self) -> None:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        manifest["sources"] = [
            source
            for source in manifest["sources"]
            if source["id"] == "neutral-covenant-hall-working-v001"
        ]
        manifest["sources"][0]["exportSets"][0]["objectsFromLods"] = ["missing-lod"]

        with tempfile.TemporaryDirectory(prefix="al-blender-manifest-") as temp_dir:
            invalid_manifest = Path(temp_dir) / "manifest.json"
            invalid_manifest.write_text(
                json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
            )
            completed, report = self._run(
                "neutral-covenant-hall-working-v001", manifest=invalid_manifest
            )

        self.assertEqual(1, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("invalid", report["status"])
        codes = {error["code"] for error in report["globalErrors"]}
        self.assertIn("manifest_export_set_lod", codes)

    def _export_hall(
        self, output: Path, *, allow_promotion_gaps: bool = False
    ) -> subprocess.CompletedProcess[str]:
        command = [
            self.blender,
            "--background",
            "--python-exit-code",
            "1",
            "--python",
            str(EXPORTER),
            "--",
            "--source",
            "neutral-covenant-hall-working-v001",
            "--export-set",
            "mvp-render",
            "--output",
            str(output),
        ]
        if allow_promotion_gaps:
            command.append("--allow-promotion-gaps")
        return subprocess.run(
            command,
            cwd=REPOSITORY_ROOT,
            check=False,
            capture_output=True,
            text=True,
            timeout=30,
        )

    def test_export_blocks_promotion_gaps_by_default(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-blender-export-") as temp_dir:
            output = Path(temp_dir) / "slagwhistle_v001.glb"
            completed = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(EXPORTER),
                    "--",
                    "--source",
                    SLAGWHISTLE_SOURCE_ID,
                    "--export-set",
                    "review-lod0-rig",
                    "--output",
                    str(output),
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            receipt = json.loads(
                output.with_suffix(".glb.receipt.json").read_text(encoding="utf-8")
            )

            self.assertEqual(
                2, completed.returncode, completed.stdout + completed.stderr
            )
            self.assertFalse(output.exists())
            self.assertEqual("blocked_promotion_gaps", receipt["status"])

    def test_export_requires_source_version_in_artifact_name(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-blender-export-") as temp_dir:
            output = Path(temp_dir) / "unversioned-hall.glb"
            completed = self._export_hall(output, allow_promotion_gaps=True)

            self.assertEqual(
                4, completed.returncode, completed.stdout + completed.stderr
            )
            self.assertFalse(output.exists())
            self.assertIn("source version token", completed.stderr)

    def test_review_export_round_trips_and_is_byte_deterministic(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-blender-export-") as temp_dir:
            root = Path(temp_dir)
            first = root / "first" / "hall_v001.glb"
            second = root / "second" / "hall_v001.glb"
            first_completed = self._export_hall(first)
            second_completed = self._export_hall(second)
            first_receipt = json.loads(
                first.with_suffix(".glb.receipt.json").read_text(encoding="utf-8")
            )
            second_receipt = json.loads(
                second.with_suffix(".glb.receipt.json").read_text(encoding="utf-8")
            )

            self.assertEqual(
                0,
                first_completed.returncode,
                first_completed.stdout + first_completed.stderr,
            )
            self.assertEqual(
                0,
                second_completed.returncode,
                second_completed.stdout + second_completed.stderr,
            )
            self.assertEqual(first.read_bytes(), second.read_bytes())
            self.assertEqual(
                first_receipt["artifact"]["sha256"],
                second_receipt["artifact"]["sha256"],
            )
            self.assertEqual("review_export_valid", first_receipt["status"])
            self.assertTrue(first_receipt["promotionEligible"])
            self.assertTrue(first_receipt["roundTrip"]["passed"])
            self.assertEqual(10, first_receipt["roundTrip"]["importedMeshObjects"])
            self.assertEqual(3200, first_receipt["roundTrip"]["importedTriangles"])

    def test_hall_remediation_is_fail_closed_deterministic_and_idempotent(
        self,
    ) -> None:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        source = next(
            item for item in manifest["sources"] if item["id"] == HALL_SOURCE_ID
        )
        source_path = REPOSITORY_ROOT / source["path"]
        art_source_root = REPOSITORY_ROOT / "unity" / "ArtSource"
        with tempfile.TemporaryDirectory(
            prefix=".al-blender-remediation-", dir=art_source_root
        ) as temp_dir:
            temp_root = Path(temp_dir)
            working_source = temp_root / source_path.name
            shutil.copy2(source_path, working_source)
            strip_expression = (
                "import bpy; "
                f"objects={list(HALL_TECHNICAL_OBJECTS)!r}; "
                f"collections={list(HALL_TECHNICAL_COLLECTIONS)!r}; "
                "[(bpy.data.objects.remove(obj, do_unlink=True)) "
                "for name in objects if (obj := bpy.data.objects.get(name))]; "
                "[(bpy.data.collections.remove(collection)) "
                "for name in collections "
                "if (collection := bpy.data.collections.get(name))]; "
                "bpy.context.preferences.filepaths.save_version=0; "
                "bpy.ops.wm.save_as_mainfile("
                f"filepath={str(working_source)!r}, check_existing=False, compress=True)"
            )
            stripped = subprocess.run(
                [
                    self.blender,
                    "--background",
                    str(working_source),
                    "--python-expr",
                    strip_expression,
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            self.assertEqual(0, stripped.returncode, stripped.stdout + stripped.stderr)

            manifest["sources"] = [source]
            source["path"] = working_source.relative_to(REPOSITORY_ROOT).as_posix()
            source["sha256"] = _sha256(working_source)
            working_manifest = temp_root / "manifest.json"
            working_manifest.write_text(
                json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
            )
            first_report_path = temp_root / "first-remediation.json"
            first = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--factory-startup",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(REMEDIATOR),
                    "--",
                    "--manifest",
                    str(working_manifest),
                    "--source",
                    HALL_SOURCE_ID,
                    "--output",
                    str(first_report_path),
                    "--apply",
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            first_report = json.loads(first_report_path.read_text(encoding="utf-8"))
            self.assertEqual(0, first.returncode, first.stdout + first.stderr)
            self.assertEqual("applied_valid", first_report["status"])
            self.assertTrue(first_report["renderSnapshotUnchanged"])
            self.assertEqual([], first_report["validationAfter"]["errors"])
            self.assertEqual([], first_report["validationAfter"]["promotionGaps"])
            self.assertNotEqual(
                first_report["sourceSha256Before"],
                first_report["sourceSha256After"],
            )

            source["sha256"] = first_report["sourceSha256After"]
            working_manifest.write_text(
                json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
            )
            validation, validation_report = self._run(
                HALL_SOURCE_ID,
                manifest=working_manifest,
                fail_on_gaps=True,
            )
            self.assertEqual(
                0,
                validation.returncode,
                validation.stdout + validation.stderr,
            )
            self.assertEqual("candidate_valid", validation_report["status"])

            second_report_path = temp_root / "second-remediation.json"
            second = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--factory-startup",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(REMEDIATOR),
                    "--",
                    "--manifest",
                    str(working_manifest),
                    "--source",
                    HALL_SOURCE_ID,
                    "--output",
                    str(second_report_path),
                    "--apply",
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            second_report = json.loads(
                second_report_path.read_text(encoding="utf-8")
            )
            self.assertEqual(0, second.returncode, second.stdout + second.stderr)
            self.assertEqual("already_compliant", second_report["status"])
            self.assertEqual(
                first_report["sourceSha256After"],
                second_report["sourceSha256After"],
            )

    def test_remediator_refuses_character_source_without_objective_plan(self) -> None:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        source = next(
            item
            for item in manifest["sources"]
            if item["id"] == "crownlands-champion-male-base-working-v001"
        )
        source_path = REPOSITORY_ROOT / source["path"]
        hash_before = _sha256(source_path)
        with tempfile.TemporaryDirectory(prefix="al-blender-remediation-") as temp_dir:
            report_path = Path(temp_dir) / "blocked.json"
            completed = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--factory-startup",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(REMEDIATOR),
                    "--",
                    "--source",
                    source["id"],
                    "--output",
                    str(report_path),
                    "--apply",
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            report = json.loads(report_path.read_text(encoding="utf-8"))

        self.assertEqual(2, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("blocked_no_objective_plan", report["status"])
        self.assertEqual(hash_before, _sha256(source_path))

    def test_transform_preview_blocks_animation_and_socket_breakage(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-blender-transform-") as temp_dir:
            report_path = Path(temp_dir) / "male-transform-preview.json"
            completed = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(TRANSFORM_ANALYZER),
                    "--",
                    "--source",
                    "crownlands-champion-male-base-working-v001",
                    "--output",
                    str(report_path),
                    "--require-safe",
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            report = json.loads(report_path.read_text(encoding="utf-8"))

        self.assertEqual(2, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("manual_rebake_required", report["status"])
        self.assertFalse(report["safeToAutomate"])
        self.assertGreater(report["deltas"]["maximumPoseMatrixComponent"], 1.0)
        self.assertGreater(report["deltas"]["maximumSocketMatrixComponent"], 10.0)
        self.assertGreater(report["deltas"]["maximumEvaluatedVertexMeters"], 1.0)
        self.assertTrue(report["deltas"]["evaluatedVertexCountsMatch"])
        self.assertLess(report["deltas"]["maximumShapeKeyVertexMeters"], 1e-5)

    def test_creature_rig_export_checks_artifact_not_importer_display_helper(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(prefix="al-blender-export-") as temp_dir:
            output = Path(temp_dir) / "slagwhistle_v001.glb"
            completed = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(EXPORTER),
                    "--",
                    "--source",
                    "slagwhistle-burrower-working-v001",
                    "--export-set",
                    "review-lod0-rig",
                    "--output",
                    str(output),
                    "--allow-promotion-gaps",
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            receipt = json.loads(
                output.with_suffix(".glb.receipt.json").read_text(encoding="utf-8")
            )

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertTrue(receipt["roundTrip"]["passed"])
        self.assertEqual(9200, receipt["roundTrip"]["importedTriangles"])
        self.assertEqual(1, receipt["roundTrip"]["importedMeshObjects"])
        self.assertEqual(
            ["Icosphere"], receipt["roundTrip"]["importerHelperMeshesIgnored"]
        )
        self.assertTrue(receipt["roundTrip"]["artifactMeshSetMatch"])
        self.assertTrue(receipt["roundTrip"]["artifactNodesIncludeSelection"])

    def test_landmark_authorer_is_semantically_deterministic_and_fail_closed(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(prefix="al-landmark-authoring-") as temp_dir:
            root = Path(temp_dir)
            receipts = []
            source_paths = []
            commands = []
            for label in ("first", "second"):
                source_path = root / label / "landmark_kit_v001.blend"
                receipt_path = root / label / "landmark_kit_v001.receipt.json"
                command = [
                    self.blender,
                    "--background",
                    "--factory-startup",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(LANDMARK_AUTHORER),
                    "--",
                    "--output",
                    str(source_path),
                    "--receipt",
                    str(receipt_path),
                ]
                completed = subprocess.run(
                    command,
                    cwd=REPOSITORY_ROOT,
                    check=False,
                    capture_output=True,
                    text=True,
                    timeout=30,
                )
                self.assertEqual(
                    0, completed.returncode, completed.stdout + completed.stderr
                )
                source_paths.append(source_path)
                receipts.append(json.loads(receipt_path.read_text(encoding="utf-8")))
                commands.append(command)

            semantic_hashes = [receipt["semantic"]["sha256"] for receipt in receipts]
            self.assertEqual(
                [LANDMARK_KIT_SEMANTIC_SHA256, LANDMARK_KIT_SEMANTIC_SHA256],
                semantic_hashes,
            )
            self.assertTrue(all(not receipt["runtimeAuthority"] for receipt in receipts))
            self.assertTrue(
                all(receipt["approvalState"] == "review-candidate" for receipt in receipts)
            )
            expected_lods = {
                "path-beacon": [736, 220, 48],
                "trail-post": [412, 132, 24],
                "boundary-wall": [692, 176, 12],
            }
            for receipt in receipts:
                self.assertEqual(
                    expected_lods,
                    {
                        family: [lod["triangles"] for lod in lods]
                        for family, lods in receipt["lods"].items()
                    },
                )
                semantic_objects = receipt["semantic"]["objects"]
                self.assertEqual(19, len(semantic_objects))
                self.assertTrue(
                    all(
                        details["type"] in {"MESH", "EMPTY"}
                        for details in semantic_objects.values()
                    )
                )
            self.assertTrue(all(len(receipt["sourceSha256"]) == 64 for receipt in receipts))

            hash_before = _sha256(source_paths[0])
            refused = subprocess.run(
                commands[0],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            self.assertEqual(4, refused.returncode, refused.stdout + refused.stderr)
            self.assertEqual(hash_before, _sha256(source_paths[0]))
            self.assertIn("refusing to overwrite", refused.stderr)

    def test_review_candidate_export_round_trips_but_cannot_promote(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-landmark-export-") as temp_dir:
            output = Path(temp_dir) / "path_beacon_review_v001.glb"
            completed = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(EXPORTER),
                    "--",
                    "--source",
                    LANDMARK_KIT_SOURCE_ID,
                    "--export-set",
                    "path-beacon-review",
                    "--output",
                    str(output),
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            receipt = json.loads(
                output.with_suffix(".glb.receipt.json").read_text(encoding="utf-8")
            )

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("review_export_valid", receipt["status"])
        self.assertEqual("review-candidate", receipt["approvalState"])
        self.assertFalse(receipt["approvalAllowsPromotion"])
        self.assertFalse(receipt["promotionEligible"])
        self.assertTrue(receipt["roundTrip"]["passed"])
        self.assertEqual(5, receipt["roundTrip"]["importedMeshObjects"])
        self.assertEqual(1018, receipt["roundTrip"]["importedTriangles"])

    def test_contact_sheet_renderer_validates_framing_and_refuses_promotion(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(prefix="al-landmark-review-") as temp_dir:
            root = Path(temp_dir)
            lod_output = root / "landmark_lods_v001.png"
            technical_output = root / "landmark_technical_v001.png"
            receipt_path = root / "landmark_contact_v001.receipt.json"
            completed = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--factory-startup",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(REVIEW_RENDERER),
                    "--",
                    "--source",
                    LANDMARK_KIT_SOURCE_ID,
                    "--output",
                    str(lod_output),
                    "--technical-output",
                    str(technical_output),
                    "--receipt",
                    str(receipt_path),
                    "--width",
                    "320",
                    "--height",
                    "180",
                    "--samples",
                    "1",
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))

            self.assertEqual(
                0, completed.returncode, completed.stdout + completed.stderr
            )
            self.assertEqual(b"\x89PNG\r\n\x1a\n", lod_output.read_bytes()[:8])
            self.assertEqual(
                b"\x89PNG\r\n\x1a\n", technical_output.read_bytes()[:8]
            )
            self.assertEqual("candidate_valid", receipt["validation"]["status"])
            self.assertFalse(receipt["promotionEligible"])
            self.assertEqual(9, len(receipt["lodLayout"]))
            self.assertEqual(3, len(receipt["technicalLayout"]))
            for framing in receipt["framing"].values():
                self.assertTrue(framing["passed"])
                bounds = framing["contentCameraBounds"]
                self.assertGreaterEqual(
                    bounds["minimumX"], -framing["cameraHalfWidth"]
                )
                self.assertLessEqual(
                    bounds["maximumX"], framing["cameraHalfWidth"]
                )
                self.assertGreaterEqual(
                    bounds["minimumY"], -framing["cameraHalfHeight"]
                )
                self.assertLessEqual(
                    bounds["maximumY"], framing["cameraHalfHeight"]
                )

            refused = subprocess.run(
                [
                    self.blender,
                    "--background",
                    "--factory-startup",
                    "--python-exit-code",
                    "1",
                    "--python",
                    str(REVIEW_RENDERER),
                    "--",
                    "--source",
                    LANDMARK_KIT_SOURCE_ID,
                    "--output",
                    str(lod_output),
                    "--technical-output",
                    str(technical_output),
                    "--receipt",
                    str(receipt_path),
                ],
                cwd=REPOSITORY_ROOT,
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            self.assertEqual(4, refused.returncode, refused.stdout + refused.stderr)
            self.assertIn("refusing to overwrite", refused.stderr)


if __name__ == "__main__":
    unittest.main()
