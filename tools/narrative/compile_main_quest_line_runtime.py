#!/usr/bin/env python3
"""Compile the authored main-quest packet into a packaged GameData runtime catalog."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path
from typing import Any

from test_main_quest_line_packet import PACKET_VERSION, load_packet_set

CATALOG_ID = "al_main_quest_line_runtime"
SCHEMA_VERSION = 1
FILE_NAME = "al_main_quest_line_runtime.v1.json"
RELATIVE_PATH = f"GameData/{FILE_NAME}"
DELIVERY_MECHANISM = "hybrid_local_streaming_assets_gamedata"
SOURCE_STATUS_WIRED = "canonical_narrative_source_complete_runtime_wired"
ENTRY_CHAPTER_ID = "CH00_FIRST_SIGNAL"
ENTRY_QUEST_ID = "OMEN_1"
ENTRY_SCENE = "Kingdom"
PROGRESS_EVENT = "QUEST_ACCEPTED"
PROGRESSED_STATE_ID = "TALK_TO_VALERIUS"
ACCEPT_CHOICE_KEY = "choice.omen1.accept"

RUNTIME_BINDINGS = {
    "OMEN_1": "nvs01_omen_1",
    "MQ_C1_PROOF_OF_WORTH": "proof_of_worth",
}

ENTRY_SCENES = {
    "CH00_FIRST_SIGNAL": "Kingdom",
    "CH01_PROOF_OF_WORTH": "ChampionArena",
}


def sha256_bytes(raw: bytes) -> str:
    return hashlib.sha256(raw.replace(b"\r\n", b"\n")).hexdigest()


def localized_text(value: dict[str, Any] | None) -> tuple[str, str]:
    if not isinstance(value, dict):
        return "", ""
    return str(value.get("key") or ""), str(value.get("text") or "")


def build_runtime_catalog(manifest: dict[str, Any], chapters: list[dict[str, Any]], source_manifest_sha256: str) -> dict[str, Any]:
    records: list[dict[str, Any]] = []
    critical_path: list[str] = []
    for chapter in chapters:
        main = chapter["mainQuest"]
        title_key, title_text = localized_text(chapter.get("title"))
        quest_id = str(main["id"])
        records.append(
            {
                "id": str(chapter["id"]),
                "order": int(chapter["order"]),
                "mainQuestId": quest_id,
                "playMode": str(chapter.get("playMode") or ""),
                "unlocksMainQuestId": str(main.get("unlocks") or ""),
                "runtimeBinding": RUNTIME_BINDINGS.get(quest_id, "catalog_quest"),
                "entryScene": ENTRY_SCENES.get(str(chapter["id"]), "Kingdom"),
                "titleKey": title_key,
                "titleText": title_text,
                "sideQuestIds": [str(side["id"]) for side in chapter.get("sideQuests") or []],
            }
        )
        critical_path.append(quest_id)

    return {
        "schemaVersion": SCHEMA_VERSION,
        "catalogId": CATALOG_ID,
        "packetId": str(manifest["packetId"]),
        "packetVersion": str(manifest["packetVersion"]),
        "sourceStatus": SOURCE_STATUS_WIRED,
        "deliveryMechanism": DELIVERY_MECHANISM,
        "relativePath": RELATIVE_PATH,
        "sourceManifestSha256": source_manifest_sha256,
        "entryChapterId": ENTRY_CHAPTER_ID,
        "entryQuestId": ENTRY_QUEST_ID,
        "entryScene": ENTRY_SCENE,
        "progressEvent": PROGRESS_EVENT,
        "progressedStateId": PROGRESSED_STATE_ID,
        "acceptChoiceKey": ACCEPT_CHOICE_KEY,
        "chapters": records,
        "criticalPath": critical_path,
    }


def canonical_json(payload: dict[str, Any]) -> bytes:
    text = json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False) + "\n"
    return text.encode("utf-8").replace(b"\r\n", b"\n")


def compile_runtime_catalog(manifest_path: Path) -> tuple[dict[str, Any], bytes, str]:
    raw_manifest = manifest_path.read_bytes()
    source_sha = sha256_bytes(raw_manifest)
    manifest, chapters = load_packet_set(manifest_path)
    if manifest.get("packetVersion") != PACKET_VERSION:
        raise ValueError("main-quest packet version drift")
    catalog = build_runtime_catalog(manifest, chapters, source_sha)
    encoded = canonical_json(catalog)
    return catalog, encoded, sha256_bytes(encoded)


def default_paths(repo_root: Path) -> tuple[Path, Path]:
    manifest = repo_root / "unity/Docs/Narrative/MainQuestLine/ANOTHERLIFE_MAIN_QUEST_LINE.packet.json"
    output = repo_root / "unity/Assets/AL/StreamingAssets/GameData" / FILE_NAME
    return manifest, output


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    manifest_path, output_path = default_paths(repo_root)
    if len(sys.argv) > 1:
        output_path = Path(sys.argv[1]).resolve()
    catalog, encoded, digest = compile_runtime_catalog(manifest_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(encoded)
    print(
        "Main quest runtime catalog compiled: "
        f"chapters={len(catalog['chapters'])} "
        f"sha256={digest} "
        f"path={output_path.as_posix()}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
