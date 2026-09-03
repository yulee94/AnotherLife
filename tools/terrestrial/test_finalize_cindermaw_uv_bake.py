import unittest
from pathlib import Path
import subprocess
import sys
import tempfile

from PIL import Image

from tools.terrestrial.finalize_cindermaw_uv_bake import (
    build_uv_bake_report,
    finalize_staged_tiles,
    ordered_tile_paths,
    portable_report_path,
)


class FinalizeCindermawUvBakeTests(unittest.TestCase):
    def test_report_paths_are_repo_relative(self):
        root = Path("D:/AnotherLife")
        self.assertEqual("unity/a.fbx", portable_report_path(root / "unity" / "a.fbx", root))

    def test_finalizes_staged_tile_paths_to_rgb_atlas(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            tile_paths = []
            for index, color in enumerate(((255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (255, 255, 0, 255))):
                path = root / f"{index}.png"
                Image.new("RGBA", (1, 1), color).save(path)
                tile_paths.append(path)
            output = root / "atlas.png"
            finalize_staged_tiles(tile_paths, 2, output)
            with Image.open(output) as image:
                self.assertEqual("RGB", image.mode)

    def test_cli_help_runs_from_script_path(self):
        script = Path(__file__).with_name("finalize_cindermaw_uv_bake.py")
        result = subprocess.run([sys.executable, str(script), "--help"], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)

    def test_orders_tiles_bottom_row_first(self):
        root = Path("atlas")
        self.assertEqual(
            [
                root / ".base_color_tiles" / "00_00.png",
                root / ".base_color_tiles" / "00_01.png",
                root / ".base_color_tiles" / "01_00.png",
                root / ".base_color_tiles" / "01_01.png",
            ],
            ordered_tile_paths(root, "base_color", 2),
        )

    def test_report_is_fail_closed_and_discloses_normal_rebuild(self):
        report = build_uv_bake_report(
            input_path="input.fbx",
            input_sha="a" * 64,
            output_path="output.fbx",
            output_sha="b" * 64,
            blend_path="source.blend",
            source_task_ids=["task"],
            metrics={
                "uvLayer": "UVMap_Clean",
                "uvFacesOutsideUnit": 0,
                "uvZeroAreaFaces": 0,
                "uvOverlappingFaces": 0,
                "nonManifoldEdgesBefore": 53,
                "nonManifoldEdgesAfter": 53,
                "polygonalProjectionBlockerResolved": True,
            },
            baked_maps=[],
        )
        self.assertIn("normal_detail_rebuild_required", report["status"])
        self.assertFalse(report["productionReady"])
        self.assertFalse(report["rigged"])
        self.assertEqual("Blocked", report["runtimeIntegrationState"])


if __name__ == "__main__":
    unittest.main()
