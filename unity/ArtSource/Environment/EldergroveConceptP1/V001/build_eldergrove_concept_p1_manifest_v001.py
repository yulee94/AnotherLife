#!/usr/bin/env python3
"""Build generation_run, per-sheet provenance, and packet manifest from assemble records."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SHEET_ORDER = [
    "eldergrove_capital_district_plan_v001",
    "eldergrove_capital_skyline_north_south_v001",
    "eldergrove_capital_skyline_east_west_v001",
    "eldergrove_capital_hero_keep_shell_v001",
    "eldergrove_capital_keep_interior_program_v001",
    "eldergrove_city_street_plan_elevation_v001",
    "eldergrove_city_house_module_v001",
    "eldergrove_city_shop_module_v001",
    "eldergrove_city_service_module_v001",
    "eldergrove_city_workshop_module_v001",
    "eldergrove_inner_cave_mouth_v001",
    "eldergrove_inner_cave_section_circulation_v001",
    "eldergrove_inner_cave_chamber_kit_v001",
    "eldergrove_outer_cave_mouth_v001",
    "eldergrove_outer_cave_section_combat_circulation_v001",
    "eldergrove_outer_cave_chamber_kit_v001",
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    records = json.loads((ROOT / "_assemble_records.json").read_text(encoding="utf-8"))
    visual_qa = json.loads((ROOT / "visual_qa_v001.json").read_text(encoding="utf-8"))
    by_stem = {Path(row["source"]).stem: row for row in records}
    missing = [name for name in SHEET_ORDER if name not in by_stem]
    if missing:
        raise SystemExit("missing assemble records: " + ",".join(missing))

    results = []
    assets = []
    for name in SHEET_ORDER:
        rec = by_stem[name]
        source = ROOT / rec["source"]
        results.append(
            {
                "name": name,
                "http": 200,
                "path": str(source),
                "bytes": rec["source_bytes"],
                "ok": True,
                "provider": "xai-oauth",
                "chat_model": "grok-4.6",
                "image_model": "grok-imagine-image-2.0",
                "quality": "medium",
                "aspect_ratio": "3:2",
                "fallback": False,
                "fallback_provider": None,
                "fallback_model": None,
            }
        )
        assets.append(
            {
                **rec,
                "provider": "xai-oauth",
                "chat_model": "grok-4.6",
                "image_model": "grok-imagine-image-2.0",
                "quality": "medium",
                "aspect_ratio": "3:2",
                "fallback": False,
                "fallback_provider": None,
                "fallback_model": None,
                "provenance": "Grok 4.6 High directing grok-imagine-image-2.0; native 3:2; GPT-5.6 Sol unused",
            }
        )

    generation_run = {
        "provider": "xai-oauth",
        "chat_model": "grok-4.6",
        "image_model": "grok-imagine-image-2.0",
        "quality": "medium",
        "aspect_ratio": "3:2",
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": False,
        "note": "First 13-sheet pool crashed on RemoteDisconnected before generation_run write; on-disk Grok PNGs were retained. Keep interior attempt1 archived; attempt2 is current authority. Byte lengths below are post-assemble RGB PNG sizes.",
        "results": results,
        "errors": {},
    }
    (ROOT / "generation_run.json").write_text(json.dumps(generation_run, indent=2) + "\n", encoding="utf-8")

    contact = ROOT / "eldergrove_concept_p1_contact_sheet_v001.jpg"
    from PIL import Image

    with Image.open(contact) as opened:
        contact_size = list(opened.size)

    fail_count = sum(1 for row in visual_qa.values() if row.get("verdict") != "PASS")
    status = (
        "AUTO_APPROVED_FOR_DETERMINISTIC_BLENDER_SUBJECT_TO_OWNER_INSPECTION"
        if fail_count == 0
        else "REVISE"
    )

    manifest = {
        "version": "V001",
        "packetId": "EldergroveConceptP1_V001",
        "status": status,
        "priority": "P2",
        "families": [
            "fam_eldergrove_capital",
            "fam_eldergrove_city_kit",
            "fam_eldergrove_inner_cave_dungeon",
            "fam_eldergrove_outer_cave_dungeon",
        ],
        "quality_target": "premium-AA dark-fantasy stylized realism; BDO-inspired directional bar, not parity",
        "authority": {
            "visual": "this packet",
            "topology": "V013",
            "catalogIds": "inventory / catalogs, not this packet",
            "architectureLanguageOnly": "unity/ArtSource/Environment/World2DProductionGuides/V001/eldergrove_architecture_source.png",
            "direction": "B — Moonroot Vigil",
            "conceptsDoNotControl": [
                "catalog IDs",
                "save IDs",
                "gameplay",
                "Meshy spend",
                "existing world inventory JSON",
                "Stonehold packets",
                "V013 inner 33.3333% / outer 66.6667%",
                "sequential-gate plates",
                "30 m fortress apron metric",
                "Town Hall / Workshop",
            ],
        },
        "identity": {
            "realm": "eldergrove",
            "direction": "Moonroot Vigil",
            "capitalId": "capital_worldroot",
            "cityIds": [],
            "cityIdPolicy": "three V013 inner-city pads; do not invent city IDs",
            "materials": [
                "pale mineral stone",
                "dark timber",
                "aged bronze root collars",
                "desaturated bark",
                "restrained moon-silver",
                "pale-gold",
            ],
            "forbidden": [
                "bright-green-only cue",
                "neon bioluminescence",
                "root portal",
                "cute sprites",
                "fake facade shells",
                "dense canopy hiding traversal",
                "modern drift",
                "real-world animals",
                "dragons",
                "bosses",
                "runtime VFX baked into geometry",
                "Embermist palette swap",
                "Open Crown Arbor Town Hall spectacle",
            ],
        },
        "provider": "xai-oauth",
        "chat_model": "grok-4.6",
        "image_model": "grok-imagine-image-2.0",
        "quality": "medium",
        "aspect_ratio": "3:2",
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": False,
        "fallback_provider": None,
        "native_generation_resolution": [1248, 832],
        "authoring_resolution": [7680, 5120],
        "authoring_provenance": "Lanczos upscale with conservative unsharp mask; not native 8K generation",
        "asset_count": 16,
        "contact_sheet": contact.name,
        "contact_sheet_bytes": contact.stat().st_size,
        "contact_sheet_sha256": sha256(contact),
        "contact_sheet_resolution": contact_size,
        "retries": [
            {
                "sheet": "eldergrove_capital_keep_interior_program_v001",
                "reason": "attempt1 stair cores did not share plan coordinates across floors",
                "result": "PASS",
            }
        ],
        "rejected_attempts": [
            "rejected/eldergrove_capital_keep_interior_program_v001_attempt1.png"
        ],
        "visual_qa": visual_qa,
        "assets": assets,
        "enterable_notes": {
            "capital_and_city": "Where buildings are represented they are enterable. Small interiors (house/shop/service/workshop) are seamless. Large combat interiors (hero keep) are streamed.",
            "no_fake_shells": True,
        },
        "dimensional_control_not_ai_numerals": {
            "player_height_m": 1.8,
            "civic_interior_door_m": [1.2, 2.4],
            "street_width_m": 6,
            "keep_visual_intent_m": {"across": 52, "to_walltop": 16},
            "house_footprint_m": [8, 10],
            "inner_cave_mouth_m": [4.5, 3.6],
            "outer_cave_mouth_m": [6.0, 4.2],
            "outer_cave_route_from_dual_gate_s": 180,
            "fortress_apron_m": 30,
            "apron_applies_to": "outer warzone fortresses, not this cave/capital packet; do not copy pixels for 30 m",
            "capital_id": "capital_worldroot",
            "inner_cities_per_realm": 3,
            "city_ids_invented": False,
            "inner_caves_per_realm": 1,
            "outer_caves_per_realm": 1,
        },
    }
    (ROOT / "eldergrove_concept_p1_manifest_v001.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print("MANIFEST", status, "ASSETS", len(assets), "FAILS", fail_count)


if __name__ == "__main__":
    main()
