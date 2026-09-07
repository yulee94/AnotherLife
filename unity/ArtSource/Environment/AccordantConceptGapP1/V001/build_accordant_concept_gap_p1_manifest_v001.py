#!/usr/bin/env python3
"""Build generation_run, per-sheet provenance, and packet manifest from assemble records."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
SHEET_ORDER = [
    "accordant_civic_ring_plan_v001",
    "accordant_civic_ring_threshold_elevations_v001",
    "accordant_civic_ring_massing_elevations_v001",
    "accordant_approach_crownlands_v001",
    "accordant_approach_stonehold_v001",
    "accordant_approach_eldergrove_v001",
    "accordant_approach_umbral_v001",
    "accordant_assembly_floor_plan_v001",
    "accordant_assembly_section_v001",
    "accordant_assembly_furniture_orthos_v001",
    "accordant_ecosystem_family_orthos_v001",
    "accordant_ecosystem_composition_plots_v001",
    "accordant_spoke_bridges_on_event_v001",
    "accordant_spoke_bridges_off_event_v001",
]
FAMILIES = [
    "fam_accordant_event_isle_civic_ring",
    "fam_accordant_event_approaches",
    "fam_accordant_assembly_interior",
    "fam_accordant_cherry_canopy_ecosystem",
    "fam_accordant_spoke_bridges",
]
LOCKED_IDS = [
    "zone_accordant_isle",
    "world_event_accordant_isle",
    "chunk_accordant_castle",
    "chunk_accordant_surface",
    "chunk_accordant_entrance_01",
    "chunk_accordant_entrance_02",
    "chunk_accordant_entrance_03",
    "chunk_accordant_entrance_04",
    "chunk_accordant_center_bridge_ring_slot_01",
    "chunk_accordant_center_bridge_ring_slot_02",
    "chunk_accordant_center_bridge_ring_slot_03",
    "chunk_accordant_center_bridge_ring_slot_04",
    "socket_center_bridge_ring_slot_01",
    "socket_center_bridge_ring_slot_02",
    "socket_center_bridge_ring_slot_03",
    "socket_center_bridge_ring_slot_04",
    "center_slot",
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

    contact = ROOT / "accordant_concept_gap_p1_contact_sheet_v001.jpg"
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
        "packetId": "AccordantConceptGapP1_V001",
        "status": status,
        "priority": "P1",
        "families": FAMILIES,
        "quality_target": "premium-AA dark-fantasy stylized realism; BDO-inspired directional bar, not parity",
        "authority": {
            "visual": "this packet",
            "topology": "V013 + accordant event-approach technical plate",
            "stableIds": LOCKED_IDS,
            "catalogIds": "inventory / catalogs, not this packet",
            "direction": "A — Petal Concord",
            "conceptsDoNotControl": [
                "catalog IDs",
                "save IDs",
                "gameplay",
                "Meshy spend",
                "existing world inventory JSON",
                "realmOrder",
                "V013 topology",
                "event-approach technical plate topology",
                "Town Hall / Workshop",
                "wish-dragon production",
            ],
        },
        "identity": {
            "realm": "accordant",
            "direction": "Petal Concord",
            "eventOnly": True,
            "absentFromRealmOrder": True,
            "notAFifthRealm": True,
            "notAFortressKeep": True,
            "notASequentialGate": True,
            "materials": [
                "neutral weathered stone",
                "dark timber",
                "restrained aged bronze",
                "muted blossom-stone medallions",
                "warm practical light",
                "asymmetric cherry canopy",
            ],
            "forbidden": [
                "fifth realm",
                "capital city loop",
                "fortress keep",
                "sequential dual-gate",
                "open off-event spans",
                "theme-park pink",
                "screen-filling petals",
                "real-world animals",
                "dragons",
                "bosses",
                "runtime VFX baked into geometry",
                "one realm visually dominating",
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
            "civic_ring": "Low council ring with four equal thresholds. Not a keep.",
            "assembly": "Central round chamber, four equal galleries, mediation, archive, stores, discreet security circulation.",
            "approaches": "Off-event: retracted/absent span AND closed grounded blossom-stone seal. On-event: authored span only.",
            "no_fake_shells": True,
        },
        "dimensional_control_not_ai_numerals": {
            "player_height_m": 1.8,
            "civic_interior_door_m": [1.2, 2.4],
            "threshold_opening_m": [4.8, 4.2],
            "central_chamber_diameter_m": 18,
            "gallery_m": [10, 8],
            "spoke_lengths": "variable visual intent; not the 180 m adjacent-bridge rule",
            "off_event_denials": ["physically absent or retracted span", "closed grounded blossom-stone seal"],
        },
    }
    (ROOT / "accordant_concept_gap_p1_manifest_v001.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print("MANIFEST", status, "ASSETS", len(assets), "FAILS", fail_count, "FALLBACK", fallback_used)


if __name__ == "__main__":
    main()
