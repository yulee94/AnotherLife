#!/usr/bin/env python3
"""Apply verified DCC repair outputs to the realm-creature source manifest."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Sequence

from PIL import Image

from tools.terrestrial.repair_cindermaw_uv_bake import validate_uv_bake_report
from tools.terrestrial.repair_realm_creature_geometry import portable_report_path, validate_repair_report


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


def _reported_repo_file(
    report: dict[str, Any],
    *,
    path_field: str,
    sha_field: str | None,
    repo_root: Path,
    diagnostics: list[str],
) -> None:
    value = report.get(path_field)
    if not isinstance(value, str) or not value or Path(value).is_absolute():
        diagnostics.append(f"report {path_field} must be a repo-relative path")
        return
    path = repo_root / value
    try:
        portable_report_path(path, repo_root)
    except ValueError:
        diagnostics.append(f"report {path_field} escapes repository")
        return
    if not path.is_file():
        diagnostics.append(f"report {path_field} does not exist: {value}")
        return
    if sha_field is not None and report.get(sha_field) != _sha256(path):
        diagnostics.append(f"report {sha_field} does not match {path_field}")


def validate_baked_map_bindings(
    reported: Sequence[dict[str, Any]],
    expected_by_path: dict[str, dict[str, Any]],
) -> list[str]:
    diagnostics: list[str] = []
    seen_paths: set[str] = set()
    duplicate_paths: set[str] = set()
    seen_names: set[str] = set()
    duplicate_names: set[str] = set()
    for index, entry in enumerate(reported):
        if (
            not isinstance(entry, dict)
            or not isinstance(entry.get("path"), str)
            or not isinstance(entry.get("name"), str)
        ):
            diagnostics.append(f"malformed baked-map record: index {index}")
            continue
        path = entry.get("path")
        if isinstance(path, str):
            (duplicate_paths if path in seen_paths else seen_paths).add(path)
        name = entry.get("name")
        if isinstance(name, str):
            (duplicate_names if name in seen_names else seen_names).add(name)
    diagnostics.extend(f"duplicate baked-map path: {path}" for path in sorted(duplicate_paths))
    diagnostics.extend(f"duplicate baked-map name: {name}" for name in sorted(duplicate_names))
    reported_by_path = {
        entry.get("path"): entry
        for entry in reported
        if isinstance(entry, dict) and isinstance(entry.get("path"), str)
    }
    if set(reported_by_path) != set(expected_by_path):
        diagnostics.append("baked-map path set does not match promoted textures")
    for path, expected in expected_by_path.items():
        actual = reported_by_path.get(path)
        if actual is None:
            continue
        expected_name = Path(path).stem
        if actual.get("name") != expected_name:
            diagnostics.append(f"baked-map name mismatch: {path}")
        if expected_name == "normal" and actual.get("provenance") != "neutral_tangent":
            diagnostics.append("normal baked-map provenance must be neutral_tangent")
        if actual.get("sha256") != expected.get("sha256"):
            diagnostics.append(f"baked-map SHA-256 mismatch: {path}")
        if actual.get("dimensions") != expected.get("dimensions"):
            diagnostics.append(f"baked-map dimensions mismatch: {path}")
    return diagnostics


def validate_repair_evidence(
    *,
    model_id: str,
    repair: dict[str, Any],
    report: dict[str, Any],
    selected_source: dict[str, Any],
    textures: list[dict[str, Any]] | None,
    packet_root: Path,
    repo_root: Path,
) -> list[str]:
    diagnostics: list[str] = []
    if report.get("modelId") != model_id:
        diagnostics.append("report modelId mismatch")
    if report.get("status") != repair["status"]:
        diagnostics.append("report status mismatch")
    if report.get("diagnostics") != []:
        diagnostics.append("report diagnostics must be an explicit empty list")
    if report.get("productionReady") is not False:
        diagnostics.append("report productionReady must remain false")
    if report.get("rigged") is not False:
        diagnostics.append("report rigged must remain false")
    if report.get("runtimeIntegrationState") != "Blocked":
        diagnostics.append("report runtimeIntegrationState must remain Blocked")
    source_task_ids = report.get("sourceTaskIds")
    if not isinstance(source_task_ids, list) or not source_task_ids:
        diagnostics.append("report sourceTaskIds must be a non-empty list")
    elif set(repair["tasks"]) - set(source_task_ids):
        diagnostics.append("report sourceTaskIds omit required repair tasks")

    expected_output = portable_report_path(packet_root / repair["model"], repo_root)
    if report.get("output") != expected_output:
        diagnostics.append("report output path mismatch")
    if report.get("outputSha256") != selected_source.get("sha256"):
        diagnostics.append("report output SHA-256 mismatch")
    _reported_repo_file(
        report,
        path_field="input",
        sha_field="inputSha256",
        repo_root=repo_root,
        diagnostics=diagnostics,
    )
    _reported_repo_file(
        report,
        path_field="editableBlend",
        sha_field=None,
        repo_root=repo_root,
        diagnostics=diagnostics,
    )

    if model_id == "elite_umbral_cindermaw_salamander":
        diagnostics.extend(validate_uv_bake_report(report))
        expected_maps: dict[str, dict[str, Any]] = {}
        for record in textures or []:
            expected_maps[portable_report_path(packet_root / record["path"], repo_root)] = record
        diagnostics.extend(validate_baked_map_bindings(report.get("bakedMaps", []), expected_maps))
    else:
        diagnostics.extend(validate_repair_report(report))
    return list(dict.fromkeys(diagnostics))


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
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--created-at-utc", default="2026-09-03T05:06:40Z")
    args = parser.parse_args(argv)
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    by_id = {record["modelId"]: record for record in manifest["models"]}
    if set(REPAIRS) - set(by_id):
        raise RuntimeError(f"manifest lacks repair records: {sorted(set(REPAIRS) - set(by_id))}")

    for model_id, repair in REPAIRS.items():
        report_path = args.docs_root / repair["report"]
        report = json.loads(report_path.read_text(encoding="utf-8"))
        selected = _file_record(args.packet_root, repair["model"])
        texture_spec = repair["textures"]
        textures = None if texture_spec is None else [_file_record(args.packet_root, path, image=True) for path in texture_spec]
        review = _file_record(args.packet_root, repair["review"], image=True)
        evidence_diagnostics = validate_repair_evidence(
            model_id=model_id,
            repair=repair,
            report=report,
            selected_source=selected,
            textures=textures,
            packet_root=args.packet_root,
            repo_root=args.repo_root,
        )
        if evidence_diagnostics:
            raise RuntimeError(f"{model_id} DCC evidence failed: {evidence_diagnostics}")
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
