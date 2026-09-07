#!/usr/bin/env python3
"""Build generation_run, per-sheet provenance, and packet manifest from assemble records."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SHEET_ORDER = [
    "umbral_capital_district_plan_v001",
    "umbral_capital_skyline_north_south_v001",
    "umbral_capital_skyline_east_west_v001",
    "umbral_capital_keep_shell_v001",
    "umbral_capital_keep_section_v001",
    "umbral_capital_keep_ground_plan_v001",
    "umbral_capital_keep_upper_circulation_v001",
    "umbral_city_street_grammar_v001",
    "umbral_city_dwelling_shell_v001",
    "umbral_city_dwelling_interior_v001",
    "umbral_city_market_service_public_hall_kit_v001",
    "umbral_inner_cave_mouth_v001",
    "umbral_inner_cave_section_circulation_v001",
    "umbral_outer_cave_mouth_v001",
    "umbral_outer_cave_loop_choke_section_v001",
    "umbral_cave_chamber_fitting_module_v001",
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
    prior_run = {}
    run_path = ROOT / "generation_run.json"
    if run_path.is_file():
        try:
            prior_run = json.loads(run_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            prior_run = {}
    prior_by_name = {row["name"]: row for row in prior_run.get("results", []) if "name" in row}
    by_stem = {Path(row["source"]).stem: row for row in records}
    missing = [name for name in SHEET_ORDER if name not in by_stem]
    if missing:
        raise SystemExit("missing assemble records: " + ",".join(missing))

    results = []
    assets = []
    for name in SHEET_ORDER:
        rec = by_stem[name]
        source = ROOT / rec["source"]
        prior = prior_by_name.get(name) or {}
        fallback = bool(prior.get("fallback"))
        provider = prior.get("provider") or ("openai-codex" if fallback else "xai-oauth")
        chat_model = prior.get("chat_model") or ("gpt-5.6-sol" if fallback else "grok-4.6")
        image_model = prior.get("image_model") or ("gpt-image-1" if fallback else "grok-imagine-image-2.0")
        quality = prior.get("quality") or ("high" if fallback else "medium")
        results.append(
            {
                "name": name,
                "http": prior.get("http") or 200,
                "path": str(source),
                "bytes": rec["source_bytes"],
                "ok": True,
                "provider": provider,
                "chat_model": chat_model,
                "image_model": image_model,
                "quality": quality,
                "aspect_ratio": "3:2",
                "fallback": fallback,
                "fallback_provider": prior.get("fallback_provider") if fallback else None,
                "fallback_model": prior.get("fallback_model") if fallback else None,
            }
        )
        provenance = (
            "GPT-5.6 Sol / gpt-image-1 fallback because Grok returned no answer; native 3:2"
            if fallback
            else "Grok 4.6 High directing grok-imagine-image-2.0; native 3:2; GPT-5.6 Sol unused"
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
                "fallback_provider": prior.get("fallback_provider") if fallback else None,
                "fallback_model": prior.get("fallback_model") if fallback else None,
                "provenance": provenance,
            }
        )

    fallback_used = any(row.get("fallback") for row in results)
    rejected_dir = ROOT / "rejected"
    rejected = sorted(
        f"rejected/{p.name}" for p in rejected_dir.glob("*.png")
    ) if rejected_dir.is_dir() else []
    retries_path = ROOT / "_retries.json"
    retries = json.loads(retries_path.read_text(encoding="utf-8")) if retries_path.is_file() else []

    generation_run = {
        "provider": "xai-oauth",
        "chat_model": "grok-4.6",
        "image_model": "grok-imagine-image-2.0",
        "quality": "medium",
        "aspect_ratio": "3:2",
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": fallback_used,
        "note": "Byte lengths below are post-assemble RGB PNG sizes. Per-sheet fallback flags are authoritative.",
        "results": results,
        "errors": {},
    }
    run_path.write_text(json.dumps(generation_run, indent=2) + "\n", encoding="utf-8")

    contact = ROOT / "umbral_concept_p1_contact_sheet_v001.jpg"
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
        "packetId": "UmbralConceptP1_V001",
        "status": status,
        "priority": "P4",
        "families": [
            "fam_umbral_capital",
            "fam_umbral_city_kit",
            "fam_umbral_inner_cave_dungeon",
            "fam_umbral_outer_cave_dungeon",
        ],
        "quality_target": "premium-AA dark-fantasy stylized realism; BDO-inspired directional bar, not parity",
        "authority": {
            "visual": "this packet",
            "topology": "V013",
            "catalogIds": "inventory / catalogs, not this packet",
            "architectureLanguageOnly": "unity/ArtSource/Environment/World2DProductionGuides/V001/umbral_architecture_source.png",
            "direction": "B — Three-Fault Ashvein",
            "conceptsDoNotControl": [
                "catalog IDs",
                "save IDs",
                "gameplay",
                "Meshy spend",
                "existing world inventory JSON",
                "Stonehold packets",
                "Eldergrove packets",
                "Crownlands packets",
                "V013 inner 33.3333% / outer 66.6667%",
                "sequential-gate plates",
                "30 m fortress apron metric",
                "Town Hall / Workshop",
            ],
        },
        "identity": {
            "realm": "umbral",
            "direction": "Three-Fault Ashvein",
            "capitalId": "capital_veilspire",
            "cityIds": [],
            "cityIdPolicy": "three V013 inner-city pads; do not invent city IDs",
            "materials": [
                "black basalt",
                "graphite ashlar",
                "matte soot",
                "smoked-glass slits",
                "ash-timber yokes",
                "arched X-braces",
                "three-fault structural seams",
            ],
            "forbidden": [
                "Gothic cathedral",
                "needle spires",
                "crystal-city drift",
                "pagoda spectacle",
                "violet fog / portal language",
                "color-only variants",
                "fake facade shells",
                "inaccessible interiors",
                "copied proprietary BDO forms",
                "modern drift",
                "real-world animals",
                "dragons",
                "bosses",
                "runtime VFX baked into geometry",
                "Embermist palette swap",
                "Moonroot Vigil root collars",
                "Meridian Oathroad chalk-gold / blue slate",
                "Town Hall / Workshop redesign",
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
        "asset_count": 16,
        "contact_sheet": contact.name,
        "contact_sheet_bytes": contact.stat().st_size,
        "contact_sheet_sha256": sha256(contact),
        "contact_sheet_resolution": contact_size,
        "retries": retries,
        "rejected_attempts": rejected,
        "visual_qa": visual_qa,
        "assets": assets,
        "enterable_notes": {
            "capital_and_city": "Where buildings are represented they are enterable. Small interiors (dwelling/market/service/public-hall) are seamless. Large combat interiors (hero keep) are streamed.",
            "no_fake_shells": True,
            "public_hall_opening_m": [2.5, 3.0],
            "town_hall_workshop_untouched": True,
        },
        "dimensional_control_not_ai_numerals": {
            "player_height_m": 1.8,
            "civic_interior_door_m": [1.2, 2.4],
            "public_hall_opening_m": [2.5, 3.0],
            "street_width_m": 6,
            "keep_visual_intent_m": {"across": 54, "to_walltop": 16},
            "dwelling_footprint_m": [8, 10],
            "inner_cave_mouth_m": [4.5, 3.6],
            "outer_cave_mouth_m": [6.0, 4.2],
            "outer_cave_route_from_dual_gate_s": 180,
            "fortress_apron_m": 30,
            "apron_applies_to": "outer warzone fortresses, not this cave/capital packet; do not copy pixels for 30 m",
            "capital_id": "capital_veilspire",
            "inner_cities_per_realm": 3,
            "city_ids_invented": False,
            "inner_caves_per_realm": 1,
            "outer_caves_per_realm": 1,
        },
        "out_of_scope_p2": [
            "gate / sequential dual-gate plates",
            "fortress / 30 m apron diagrams",
            "terrain / landform sheets",
            "ecosystem dressing",
        ],
    }
    (ROOT / "umbral_concept_p1_manifest_v001.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print("MANIFEST", status, "ASSETS", len(assets), "FAILS", fail_count, "FALLBACK", fallback_used)


if __name__ == "__main__":
    main()
