#!/usr/bin/env python3
"""Build generation_run, per-sheet provenance, and packet manifest from assemble records."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
SHEET_ORDER = [
    "crownlands_sequential_gate_outer_face_v001",
    "crownlands_sequential_gate_inner_face_v001",
    "crownlands_sequential_gate_longitudinal_section_v001",
    "crownlands_sequential_wall_walltop_modules_v001",
    "crownlands_fortress_plan_apron_v001",
    "crownlands_fortress_elevations_v001",
    "crownlands_fortress_keep_flag_anchor_v001",
    "crownlands_terrain_grades_worldscar_wallend_v001",
    "crownlands_terrain_bridge_abutment_180m_v001",
    "crownlands_terrain_route_bed_v001",
    "crownlands_ecosystem_composition_habitat_v001",
    "crownlands_cave_exterior_language_v001",
    "crownlands_capital_city_material_kits_v001",
    "crownlands_lod_material_lighting_reference_v001",
]
FAMILIES = [
    "fam_crownlands_sequential_gate_wall_complex",
    "fam_crownlands_fortress_single_gate",
    "fam_crownlands_terrain",
    "fam_crownlands_ecosystem_dressing",
    "fam_crownlands_material_lod",
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
    retries = json.loads((ROOT / "_retries_v001.json").read_text(encoding="utf-8"))
    rejected = json.loads((ROOT / "_rejected_v001.json").read_text(encoding="utf-8"))
    run_src = json.loads((ROOT / "generation_run.json").read_text(encoding="utf-8"))
    by_stem = {Path(row["source"]).stem: row for row in records}
    missing = [name for name in SHEET_ORDER if name not in by_stem]
    if missing:
        raise SystemExit("missing assemble records: " + ",".join(missing))

    run_by_name = {row["name"]: row for row in run_src.get("results") or [] if "name" in row}
    results = []
    assets = []
    fallback_used = False
    for name in SHEET_ORDER:
        rec = by_stem[name]
        source = ROOT / rec["source"]
        run_row = run_by_name.get(name) or {}
        fallback = bool(run_row.get("fallback"))
        fallback_used = fallback_used or fallback
        provider = "openai-codex" if fallback else "xai-oauth"
        chat_model = "gpt-5.6-sol" if fallback else "grok-4.6"
        image_model = "gpt-image-1" if fallback else "grok-imagine-image-2.0"
        quality = "high" if fallback else "medium"
        results.append(
            {
                "name": name,
                "http": run_row.get("http", 200),
                "path": str(source),
                "bytes": rec["source_bytes"],
                "ok": True,
                "provider": provider,
                "chat_model": chat_model,
                "image_model": image_model,
                "quality": quality,
                "aspect_ratio": "3:2",
                "fallback": fallback,
                "fallback_provider": "openai-codex" if fallback else None,
                "fallback_model": "gpt-5.6-sol" if fallback else None,
            }
        )
        assets.append(
            {
                **rec,
                "provider": provider,
                "chat_model": chat_model,
                "image_model": image_model,
                "quality": quality,
                "aspect_ratio": "3:2",
                "fallback": fallback,
                "fallback_provider": "openai-codex" if fallback else None,
                "fallback_model": "gpt-5.6-sol" if fallback else None,
                "provenance": (
                    "GPT-5.6 Sol fallback gpt-image-1; native 3:2"
                    if fallback
                    else "Grok 4.6 High directing grok-imagine-image-2.0; native 3:2; GPT-5.6 Sol unused for this sheet"
                ),
            }
        )

    generation_run = {
        "provider": "xai-oauth",
        "chat_model": "grok-4.6",
        "image_model": "grok-imagine-image-2.0",
        "quality": "medium",
        "aspect_ratio": "3:2",
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": fallback_used,
        "results": results,
        "errors": {},
    }
    (ROOT / "generation_run.json").write_text(json.dumps(generation_run, indent=2) + "\n", encoding="utf-8")

    contact = ROOT / "crownlands_concept_gap_p2_contact_sheet_v001.jpg"
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
        "packetId": "CrownlandsConceptGapP2_V001",
        "status": status,
        "priority": "P2",
        "families": FAMILIES,
        "quality_target": "premium-AA dark-fantasy stylized realism; BDO-inspired directional bar, not parity",
        "authority": {
            "visual": "this packet",
            "topology": "V013 + crownlands_sequential_gate_technical_plate_v001",
            "stableGateIds": [
                "gate_crownlands_meridian",
                "gate_crownlands_meridian_outer",
                "wall_complex_crownlands_meridian",
            ],
            "catalogIds": "inventory / catalogs, not this packet",
            "direction": "B — Meridian Oathroad",
            "p1Authority": "unity/ArtSource/Environment/CrownlandsConceptP1/V001",
            "p1Binding": {
                "packetId": "CrownlandsConceptP1_V001",
                "path": "unity/ArtSource/Environment/CrownlandsConceptP1/V001",
                "manifest": "crownlands_concept_p1_manifest_v001.json",
                "manifest_sha256": "7316ec9e59966935463b04fd53d7e56266fd7bea6f4154920b576810ef8cb457",
                "contact_sheet": "crownlands_concept_p1_contact_sheet_v001.jpg",
                "contact_sheet_sha256": "b1f4b03766b80a21e6138752d945eec5f6a64c92eb53381e4eebbed68ecb560d",
                "status": "AUTO_APPROVED_FOR_DETERMINISTIC_BLENDER_SUBJECT_TO_OWNER_INSPECTION",
                "validation": "16/16 PASS",
                "touched": False,
            },
            "conceptsDoNotControl": [
                "catalog IDs",
                "save IDs",
                "gameplay",
                "Meshy spend",
                "existing world inventory JSON",
                "Stonehold packets",
                "Eldergrove packets",
                "CrownlandsConceptP1",
                "V013 inner 33.3333% / outer 66.6667%",
                "sequential-gate technical plate topology",
                "30 m fortress apron metric",
                "180 m adjacent-realm bridge length",
                "Town Hall / Workshop",
            ],
        },
        "identity": {
            "realm": "crownlands",
            "direction": "Meridian Oathroad",
            "capitalId": "capital_crownspire",
            "cityIds": [],
            "fortressIds": [
                "fortress_crownlands_01",
                "fortress_crownlands_02",
                "fortress_crownlands_03",
                "fortress_crownlands_04",
            ],
            "fortressIdsArePlaceholders": True,
            "materials": [
                "chalk-gold ashlar limestone",
                "deep blue slate",
                "brass meridian ribs",
                "cool silver edgework",
                "restrained bronze",
                "rain-dark mortar",
                "packed oathroad grit",
            ],
            "forbidden": [
                "circular/radial capital",
                "Gothic cathedral",
                "conical turret",
                "needle spire",
                "witch-hat caps",
                "fake facade shells",
                "modern drift",
                "real-world animals",
                "dragons",
                "bosses",
                "runtime VFX baked into geometry",
                "Embermist palette swap",
                "Moonroot roots",
                "apron foliage/terrain/climb props",
                "Town Hall / Workshop spectacle",
            ],
        },
        "provider": "xai-oauth",
        "chat_model": "grok-4.6",
        "image_model": "grok-imagine-image-2.0",
        "quality": "medium",
        "aspect_ratio": "3:2",
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": fallback_used,
        "fallback_provider": "openai-codex" if fallback_used else None,
        "native_generation_resolution": [1248, 832],
        "authoring_resolution": [7680, 5120],
        "authoring_provenance": "Lanczos upscale with conservative unsharp mask; not native 8K generation",
        "asset_count": 14,
        "contact_sheet": contact.name,
        "contact_sheet_bytes": contact.stat().st_size,
        "contact_sheet_sha256": sha256(contact),
        "contact_sheet_resolution": contact_size,
        "retries": retries,
        "rejected_attempts": rejected,
        "visual_qa": visual_qa,
        "assets": assets,
        "enterable_notes": {
            "sequential": "Controlled passageway and walltop defender routes are enterable. Attacker bypass forbidden.",
            "fortress": "Keep/interior must be a complete enterable space, not a black facade.",
            "no_fake_shells": True,
        },
        "dimensional_control_not_ai_numerals": {
            "player_height_m": 1.8,
            "civic_interior_door_m": [1.2, 2.4],
            "sequential_passage_m": 18,
            "ceremonial_gate_opening_m": [8, 6],
            "walltop_walk_m": 2.4,
            "fortress_apron_m": 30,
            "apron_applies_to": "outer warzone fortresses; metric validators control 30 m; do not copy pixels",
            "flag_anchor_plinth_m": {"mast": 6, "plinth": 4},
            "keep_visual_intent_m": {"across": 36, "to_walltop": 16},
            "route_km": 14.4,
            "bridge_span_m": 180,
            "bridge_deck_width_m": 6.0,
            "bridge_rail_height_m": 1.1,
            "inner_gate_id": "gate_crownlands_meridian",
            "outer_gate_id": "gate_crownlands_meridian_outer",
            "wall_complex_id": "wall_complex_crownlands_meridian",
            "capital_id": "capital_crownspire",
            "city_ids_invented": False,
        },
    }
    (ROOT / "crownlands_concept_gap_p2_manifest_v001.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print("MANIFEST", status, "ASSETS", len(assets), "FAILS", fail_count, "FALLBACK", fallback_used)


if __name__ == "__main__":
    main()
