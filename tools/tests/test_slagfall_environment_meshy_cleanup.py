import hashlib
import importlib.util
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path, PurePosixPath
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[2]
MODEL_ROOT = (
    REPO_ROOT
    / "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Environment/Models"
)
REPORT_PATH = (
    REPO_ROOT
    / "unity/ArtSource/Terrestrials/Stonehold/SlagfallQuarry/Environment/Meshy"
    / "slagfall_environment_meshy_cleanup_v001.json"
)
EXECUTION_RECORD_PATH = (
    REPO_ROOT
    / "unity/Docs/AI/Meshy/meshy_execution_slagfall_environment_2026-08-31_v001.json"
)
CLEANUP_SCRIPT_PATH = REPO_ROOT / "tools/slagfall_environment_meshy_cleanup.py"

FAMILY_IDS = (
    "irregular_fracture_raft",
    "broken_fracture_raft",
    "undercut_extraction_ledge",
    "talus_apron",
    "collapsed_gallery_mouth",
    "diagonal_fault_slab",
    "braided_runoff_pool",
    "iron_soil_wedge",
)


class SlagfallEnvironmentMeshyCleanupTests(unittest.TestCase):
    def test_cleanup_report_writer_emits_byte_stable_lf_json(self):
        module_name = "_slagfall_environment_meshy_cleanup_test"
        spec = importlib.util.spec_from_file_location(module_name, CLEANUP_SCRIPT_PATH)
        self.assertIsNotNone(spec)
        self.assertIsNotNone(spec.loader)
        module = importlib.util.module_from_spec(spec)
        stub_modules = {
            module_name: module,
            "bmesh": types.ModuleType("bmesh"),
            "bpy": types.ModuleType("bpy"),
        }
        with mock.patch.dict(sys.modules, stub_modules):
            spec.loader.exec_module(module)

        writer = getattr(module, "write_json_lf", None)
        self.assertIsNotNone(writer)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "report.json"
            writer(output, {"line": "one", "value": 1})
            self.assertEqual(
                b'{\n  "line": "one",\n  "value": 1\n}\n',
                output.read_bytes(),
            )

    def test_production_package_contains_eight_models(self):
        expected = [
            MODEL_ROOT / f"tdf_prop_stonehold_slagfall_{family_id}_v001.fbx"
            for family_id in FAMILY_IDS
        ]

        missing = [str(path) for path in expected if not path.is_file()]
        self.assertEqual([], missing)

    def test_cleanup_report_uses_repository_relative_locators(self):
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        locators = [report["master_blend"]]
        locators.extend(atlas["path"] for atlas in report["atlases"].values())
        for family in report["families"]:
            locators.extend((family["source"], family["output"]))

        self.assertEqual(20, len(locators))
        for locator in locators:
            with self.subTest(locator=locator):
                self.assertNotRegex(locator, r"^[A-Za-z]:[\\/]")
                self.assertNotIn("\\", locator)
                self.assertTrue((REPO_ROOT / locator).is_file())

    def test_execution_record_verifies_every_hashed_artifact(self):
        record = json.loads(EXECUTION_RECORD_PATH.read_text(encoding="utf-8"))

        def hashed_artifacts(value):
            if isinstance(value, dict):
                if {"locator", "sha256", "byteLength"}.issubset(value):
                    yield value
                for child in value.values():
                    yield from hashed_artifacts(child)
            elif isinstance(value, list):
                for child in value:
                    yield from hashed_artifacts(child)

        artifacts = list(hashed_artifacts(record))
        self.assertEqual(31, len(artifacts))
        for artifact in artifacts:
            locator = artifact["locator"]
            with self.subTest(locator=locator):
                payload = (REPO_ROOT / locator).read_bytes()
                if Path(locator).suffix == ".json":
                    self.assertNotIn(b"\r\n", payload)
                self.assertEqual(artifact["byteLength"], len(payload))
                self.assertEqual(
                    artifact["sha256"],
                    hashlib.sha256(payload).hexdigest(),
                )

    def test_execution_record_captures_cleanup_texture_inputs(self):
        record = json.loads(EXECUTION_RECORD_PATH.read_text(encoding="utf-8"))
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        manifest_record = record["selectedTextureInputs"]
        manifest_path = REPO_ROOT / manifest_record["manifest"]["locator"]
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        texture_names = (
            "base_color.png",
            "normal.png",
            "metallic.png",
            "roughness.png",
        )
        expected = set()
        for family in report["families"]:
            source = PurePosixPath(family["source"])
            texture_root = source.parent / f"{source.stem}_textures"
            expected.update(str(texture_root / name) for name in texture_names)

        actual = {
            texture["locator"]
            for family in manifest["families"]
            for texture in family["textureInputs"]
        }
        self.assertEqual(8, manifest_record["familyCount"])
        self.assertEqual(32, manifest_record["textureInputCount"])
        self.assertEqual(8, manifest["familyCount"])
        self.assertEqual(32, manifest["textureInputCount"])
        self.assertEqual(expected, actual)
        for family in manifest["families"]:
            for texture in family["textureInputs"]:
                locator = texture["locator"]
                with self.subTest(texture=locator):
                    payload = (REPO_ROOT / locator).read_bytes()
                    self.assertEqual(texture["byteLength"], len(payload))
                    self.assertEqual(
                        texture["sha256"],
                        hashlib.sha256(payload).hexdigest(),
                    )


if __name__ == "__main__":
    unittest.main()
