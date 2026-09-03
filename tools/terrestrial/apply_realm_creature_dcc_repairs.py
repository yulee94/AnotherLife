#!/usr/bin/env python3
"""Apply verified DCC repair outputs to the realm-creature source manifest."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Sequence

from PIL import Image


def update_repaired_source_record(
    record: dict[str, Any],
    *,
    selected_source: dict[str, Any],
    textures: list[dict[str, Any]] | None,
    review: dict[str, Any],
    status: str,
    task_ids: Sequence[str],
) -> None:
    record["selectedSource"] = selected_source
    if textures is not None:
        record["textures"] = textures
    record["review"] = review
    record["status"] = status
    record["blocker"] = None
    record["meshyTaskIds"] = list(dict.fromkeys([*record.get("meshyTaskIds", []), *task_ids]))
    record["rigged"] = False
    record["runtimeIntegrationState"] = "Blocked"
    record["productionReady"] = False


def _owner_tier(model: dict[str, Any]) -> bool:
    if "normal_detail_rebuild_required" in model.get("status", ""):
        return False
    dimensions: dict[str, list[int]] = {}
    for item in model.get("textures", []):
        name = Path(item["path"]).name
        size = item.get("dimensions")
        if not isinstance(size, list) or len(size) != 2:
            continue
        if name not in dimensions or size[0] * size[1] > dimensions[name][0] * dimensions[name][1]:
            dimensions[name] = size
    return (
        dimensions.get("base_color.png") == [8192, 8192]
        and dimensions.get("normal.png") == [4096, 4096]
        and dimensions.get("roughness.png") == [4096, 4096]
        and dimensions.get("metallic.png") == [4096, 4096]
    )


def recompute_summary(models: Sequence[dict[str, Any]]) -> dict[str, int]:
    owner_tier = sum(_owner_tier(model) for model in models)
    return {
        "structuralPass": sum("pass" in model.get("status", "") for model in models),
        "blocked3D": sum(bool(model.get("blocker")) for model in models),
        "ownerTierTexturePackets": owner_tier,
        "belowOwnerTierTexturePackets": len(models) - owner_tier,
    }


def apply_summary_counts(manifest: dict[str, Any]) -> None:
    counts = recompute_summary(manifest["models"])
    manifest["summary"] = {
        "approved2D": manifest["summary"]["approved2D"],
        **counts,
        "runtimeIntegrationState": manifest["summary"]["runtimeIntegrationState"],
    }
    manifest["qualityBar"]["ownerTierTexturePackets"] = counts["ownerTierTexturePackets"]
    manifest["qualityBar"]["belowOwnerTierTexturePackets"] = counts["belowOwnerTierTexturePackets"]


def apply_packet_revision(manifest: dict[str, Any], created_at_utc: str) -> None:
    manifest["sourceVersion"] = "al-rcreature-2026-09-03-v002"
    manifest["createdAtUtc"] = created_at_utc
    manifest["qualityBar"]["coverageDisposition"] = (
        "Hollowbark Stalker and Reliquary Basilisk retain complete owner-tier authoring maps. "
        "Cindermaw has clean 8K/4K rebakes but remains below tier because its rejected ray bake was "
        "replaced with a neutral normal fallback pending authored microdetail. Mere-Root and Crownstep "
        "require topology-matched texture rebuilds; all other lower-tier packets remain review sources."
    )
    additions = [
        "Approved DCC geometry repairs and structural re-audit for five blocked packets",
        "Cindermaw Meshy-7 retexture, triangulated non-overlapping UV rebuild, and tiled 8K/4K rebake",
        "Independent hash, UV, schema, and fail-closed readiness validation",
    ]
    existing = manifest["provenance"].get("editingSteps", [])
    manifest["provenance"]["editingSteps"] = list(dict.fromkeys([*existing, *additions]))
    manifest["provenance"]["editableSourceAvailability"] = (
        "FBX and image maps are retained for all selected sources; versioned Blender repair files and "
        "hash-bound DCC reports are retained for the remediated packets. Rigs remain undelivered."
    )


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _file_record(root: Path, relative: str, image: bool = False) -> dict[str, Any]:
    path = root / relative
    record: dict[str, Any] = {
        "path": relative.replace("\\", "/"),
        "bytes": path.stat().st_size,
        "sha256": _sha256(path),
    }
    if image:
        with Image.open(path) as opened:
            record["dimensions"] = list(opened.size)
        record["mediaType"] = "image/png"
    return record


REPAIRS: dict[str, dict[str, Any]] = {
    "boss_eldergrove_mere_root_leviathan": {
        "model": "Models/boss_eldergrove_mere_root_leviathan/boss_eldergrove_mere_root_leviathan_source_v002.fbx",
        "textures": [],
        "review": "Review/boss_eldergrove_mere_root_leviathan_threequarter_v002.png",
        "status": "clean_geometry_pass_texture_rebuild_required",
        "tasks": ["01a06522-ebd2-7564-958e-e974edb6b370"],
        "report": "DCCReports/boss_eldergrove_mere_root_leviathan_geometry_v002.json",
    },
    "boss_crownlands_meridian_tempest_roc": {
        "model": "Models/boss_crownlands_meridian_tempest_roc/boss_crownlands_meridian_tempest_roc_source_v002.fbx",
        "textures": None,
        "review": "Review/boss_crownlands_meridian_tempest_roc_threequarter_v002.png",
        "status": "clean_geometry_pass_texture_uplift_required",
        "tasks": [],
        "report": "DCCReports/boss_crownlands_meridian_tempest_roc_geometry_v002.json",
    },
    "elite_eldergrove_sunmane_thornstag": {
        "model": "Models/elite_eldergrove_sunmane_thornstag/elite_eldergrove_sunmane_thornstag_source_v001.fbx",
        "textures": None,
        "review": "Review/elite_eldergrove_sunmane_thornstag_threequarter.png",
        "status": "clean_geometry_pass_texture_uplift_required",
        "tasks": [],
        "report": "DCCReports/elite_eldergrove_sunmane_thornstag_geometry_audit_v002.json",
    },
    "elite_crownlands_crownstep": {
        "model": "Models/elite_crownlands_crownstep/elite_crownlands_crownstep_source_v002.fbx",
        "textures": [],
        "review": "Review/elite_crownlands_crownstep_threequarter_v002.png",
        "status": "clean_geometry_pass_texture_rebuild_required",
        "tasks": ["01a06522-f510-75a3-b779-280aa4393c34"],
        "report": "DCCReports/elite_crownlands_crownstep_geometry_v002.json",
    },
    "elite_umbral_cindermaw_salamander": {
        "model": "Models/elite_umbral_cindermaw_salamander/elite_umbral_cindermaw_salamander_source_v003.fbx",
        "textures": [
            "Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_v003/ao.png",
            "Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_v003/base_color.png",
            "Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_v003/metallic.png",
            "Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_v003/normal.png",
            "Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_v003/roughness.png",
        ],
        "review": "Review/elite_umbral_cindermaw_salamander_threequarter_v003.png",
        "status": "clean_geometry_pass_uv_bake_complete_normal_detail_rebuild_required",
        "tasks": ["01a06569-2956-73a2-a51e-bade35802fba"],
        "report": "DCCReports/elite_umbral_cindermaw_salamander_uv_bake_v003.json",
    },
}


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--packet-root", type=Path, required=True)
    parser.add_argument("--docs-root", type=Path, required=True)
    parser.add_argument("--created-at-utc", default="2026-09-03T05:06:40Z")
    args = parser.parse_args(argv)
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    by_id = {record["modelId"]: record for record in manifest["models"]}
    if set(REPAIRS) - set(by_id):
        raise RuntimeError(f"manifest lacks repair records: {sorted(set(REPAIRS) - set(by_id))}")

    for model_id, repair in REPAIRS.items():
        report_path = args.docs_root / repair["report"]
        report = json.loads(report_path.read_text(encoding="utf-8"))
        if report.get("diagnostics"):
            raise RuntimeError(f"{model_id} DCC report has diagnostics: {report['diagnostics']}")
        selected = _file_record(args.packet_root, repair["model"])
        if report.get("outputSha256") != selected["sha256"]:
            raise RuntimeError(f"{model_id} report/output SHA-256 mismatch")
        texture_spec = repair["textures"]
        textures = None if texture_spec is None else [_file_record(args.packet_root, path, image=True) for path in texture_spec]
        review = _file_record(args.packet_root, repair["review"], image=True)
        update_repaired_source_record(
            by_id[model_id],
            selected_source=selected,
            textures=textures,
            review=review,
            status=repair["status"],
            task_ids=repair["tasks"],
        )

    apply_summary_counts(manifest)
    apply_packet_revision(manifest, args.created_at_utc)
    args.manifest.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest["summary"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(__import__("sys").argv[1:]))
