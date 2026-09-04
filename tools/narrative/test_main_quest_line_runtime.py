#!/usr/bin/env python3
"""Fail-closed tests for the packaged main-quest runtime catalog."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from compile_main_quest_line_runtime import (
    CATALOG_ID,
    ENTRY_CHAPTER_ID,
    ENTRY_QUEST_ID,
    FILE_NAME,
    PACKET_VERSION,
    PROGRESSED_STATE_ID,
    SOURCE_STATUS_WIRED,
    compile_runtime_catalog,
    default_paths,
    sha256_bytes,
)
from test_main_quest_line_packet import load_packet_set


REPO_ROOT = Path(__file__).resolve().parents[2]


class MainQuestLineRuntimeCatalogTests(unittest.TestCase):
    def test_committed_catalog_matches_compiler_and_source_packet(self) -> None:
        manifest_path, committed_path = default_paths(REPO_ROOT)
        self.assertTrue(committed_path.is_file(), committed_path)
        catalog, encoded, digest = compile_runtime_catalog(manifest_path)
        committed = committed_path.read_bytes().replace(b"\r\n", b"\n")
        self.assertEqual(encoded, committed)
        self.assertEqual(digest, sha256_bytes(committed))
        self.assertEqual(catalog["catalogId"], CATALOG_ID)
        self.assertEqual(catalog["packetVersion"], PACKET_VERSION)
        self.assertEqual(catalog["sourceStatus"], SOURCE_STATUS_WIRED)
        self.assertEqual(catalog["entryChapterId"], ENTRY_CHAPTER_ID)
        self.assertEqual(catalog["entryQuestId"], ENTRY_QUEST_ID)
        self.assertEqual(catalog["progressedStateId"], PROGRESSED_STATE_ID)
        self.assertEqual(len(catalog["chapters"]), 15)
        self.assertEqual(catalog["criticalPath"][0], "OMEN_1")
        self.assertEqual(catalog["criticalPath"][1], "MQ_C1_PROOF_OF_WORTH")
        self.assertEqual(catalog["chapters"][0]["runtimeBinding"], "nvs01_omen_1")
        self.assertEqual(catalog["chapters"][1]["runtimeBinding"], "proof_of_worth")
        self.assertEqual(catalog["chapters"][0]["entryScene"], "Kingdom")
        self.assertEqual(catalog["chapters"][1]["entryScene"], "ChampionArena")
        manifest, chapters = load_packet_set(manifest_path)
        self.assertEqual(manifest["packetId"], catalog["packetId"])
        self.assertEqual([chapter["id"] for chapter in chapters], [row["id"] for row in catalog["chapters"]])

    def test_missing_component_fails_closed(self) -> None:
        manifest_path, _ = default_paths(REPO_ROOT)
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        with tempfile.TemporaryDirectory() as temp:
            temp_root = Path(temp)
            packet_dir = temp_root / "unity/Docs/Narrative/MainQuestLine"
            packet_dir.mkdir(parents=True)
            broken = dict(manifest)
            broken["components"] = list(manifest["components"])
            broken["components"][0] = dict(manifest["components"][0])
            broken["components"][0]["path"] = "unity/Docs/Narrative/MainQuestLine/missing.json"
            packet_path = packet_dir / "ANOTHERLIFE_MAIN_QUEST_LINE.packet.json"
            packet_path.write_text(json.dumps(broken), encoding="utf-8")
            with self.assertRaises(Exception):
                compile_runtime_catalog(packet_path)

    def test_file_name_is_the_shipping_gamedata_leaf(self) -> None:
        self.assertEqual(FILE_NAME, "al_main_quest_line_runtime.v1.json")


if __name__ == "__main__":
    unittest.main()
