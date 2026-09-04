#!/usr/bin/env python3
"""Validate the tracked non-runtime realm-creature production-source packet."""

from __future__ import annotations

import argparse
import json
from pathlib import Path, PurePosixPath
from typing import Any

from PIL import Image

from tools.terrestrial.promote_realm_creature_source_packet import (
    PacketError,
    file_record,
    has_owner_tier_texture_packet,
    validate_promoted_files,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = REPO_ROOT / "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/realm_creature_3d_source_manifest_v001.json"
DEFAULT_APPROVAL_MANIFEST = REPO_ROOT / "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/realm_creature_2d_approval_manifest_v002.json"
DEFAULT_ART_ROOT = REPO_ROOT / "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"


def validate_approval_packet(approval: dict[str, Any], repo_root: Path) -> list[str]:
    diagnostics: list[str] = []
    entries = approval.get("entries")
    if (
        approval.get("rosterCount") != 21
        or approval.get("approvedCount") != 21
        or approval.get("blockedCount") != 0
        or not isinstance(entries, list)
        or len(entries) != 21
    ):
        return ["2D approval packet must contain exactly 21 approved and zero blocked entries"]
    ids = [entry.get("id") for entry in entries]
    if None in ids or len(set(ids)) != 21:
        diagnostics.append("2D approval IDs must be unique and nonblank")
    paths: set[str] = set()
    for entry in entries:
        source_id = entry.get("id", "<unknown>")
        if entry.get("status") != "APPROVED_2D":
            diagnostics.append(f"{source_id}: 2D status must equal APPROVED_2D")
        sources = entry.get("sources")
        if not isinstance(sources, list) or not sources:
            diagnostics.append(f"{source_id}: at least one approved 2D source is required")
            continue
        for source in sources:
            value = source.get("path") if isinstance(source, dict) else None
            parsed = PurePosixPath(value) if isinstance(value, str) else None
            invalid = (
                parsed is None
                or not value
                or "\\" in value
                or parsed.is_absolute()
                or ".." in parsed.parts
                or ":" in parsed.parts[0]
            )
            if invalid:
                diagnostics.append(f"{source_id}: 2D source path must be repository-relative: {value}")
                continue
            if value in paths:
                diagnostics.append(f"{source_id}: duplicate 2D source path: {value}")
                continue
            paths.add(value)
            path = repo_root / value
            if not path.is_file():
                diagnostics.append(f"{source_id}: approved 2D source is missing: {value}")
                continue
            actual = file_record(path, repo_root)
            if actual["bytes"] != source.get("bytes"):
                diagnostics.append(f"{source_id}: 2D source byte-length mismatch: {value}")
            if actual["sha256"] != source.get("sha256"):
                diagnostics.append(f"{source_id}: 2D source hash mismatch: {value}")
            try:
                with Image.open(path) as image:
                    image.verify()
                with Image.open(path) as image:
                    dimensions = list(image.size)
                    media_format = image.format
            except OSError:
                diagnostics.append(f"{source_id}: 2D source is not a renderable image: {value}")
                continue
            if media_format != "PNG":
                diagnostics.append(f"{source_id}: 2D source media type must be PNG: {value}")
            if dimensions != source.get("dimensions"):
                diagnostics.append(f"{source_id}: 2D source dimension mismatch: {value}")
    return diagnostics


def validate_packet(packet: dict[str, Any], art_root: Path) -> list[str]:
    diagnostics: list[str] = []
    if packet.get("schemaVersion") != 1:
        diagnostics.append("schemaVersion must equal 1")
    if packet.get("packetId") != "anotherlife-realm-creature-production-source":
        diagnostics.append("packetId is invalid")
    readiness = packet.get("readiness") or {}
    if readiness.get("runtimeIntegrationState") != "Blocked":
        diagnostics.append("packet runtimeIntegrationState must remain Blocked")
    models = packet.get("models")
    if not isinstance(models, list) or len(models) != 21:
        diagnostics.append("models must contain exactly 21 entries")
        return diagnostics
    ids = [model.get("modelId") for model in models]
    if None in ids or len(set(ids)) != 21:
        diagnostics.append("modelId values must be unique and nonblank")
    summary = packet.get("summary") or {}
    structural_pass = sum("pass" in str(model.get("status", "")) for model in models)
    if summary.get("approved2D") != 21:
        diagnostics.append("summary.approved2D must equal 21")
    if summary.get("structuralPass") != structural_pass:
        diagnostics.append("summary.structuralPass does not match model statuses")
    blocked = sum(bool(model.get("blocker")) for model in models)
    if summary.get("blocked3D") != blocked:
        diagnostics.append("summary.blocked3D does not match model blockers")
    owner_tier_texture_packets = sum(
        "normal_detail_rebuild_required" not in model.get("status", "")
        and has_owner_tier_texture_packet(
            [texture for texture in model.get("textures", []) if isinstance(texture, dict)]
        )
        for model in models
    )
    if (
        summary.get("ownerTierTexturePackets") != owner_tier_texture_packets
        or summary.get("belowOwnerTierTexturePackets") != 21 - owner_tier_texture_packets
    ):
        diagnostics.append("summary owner-tier texture coverage does not match texture dimensions")
    if summary.get("runtimeIntegrationState") != "Blocked":
        diagnostics.append("summary runtimeIntegrationState must remain Blocked")

    records: list[dict[str, Any]] = []
    record_paths: set[str] = set()
    for model in models:
        model_id = model.get("modelId", "<unknown>")
        if model.get("runtimeIntegrationState") != "Blocked":
            diagnostics.append(f"{model_id}: runtimeIntegrationState must remain Blocked")
        if model.get("productionReady") is not False:
            diagnostics.append(f"{model_id}: productionReady must be false")
        if model.get("rigged") is not False:
            diagnostics.append(f"{model_id}: rigged must be false in this packet")
        textures = model.get("textures")
        if not isinstance(textures, list):
            diagnostics.append(f"{model_id}: at least one texture record is required")
        elif not textures and "texture_rebuild_required" not in model.get("status", ""):
            diagnostics.append(f"{model_id}: at least one texture record is required")
        if not isinstance(model.get("meshyTaskIds"), list) or not model["meshyTaskIds"]:
            diagnostics.append(f"{model_id}: at least one Meshy task ID is required")
        model_records: list[dict[str, Any]] = []
        for field in ("selectedSource", "review"):
            if isinstance(model.get(field), dict):
                model_records.append(model[field])
            else:
                diagnostics.append(f"{model_id}: {field} record is required")
        model_records.extend(item for item in model.get("textures", []) if isinstance(item, dict))
        for record in model_records:
            value = record.get("path")
            parsed = PurePosixPath(value) if isinstance(value, str) else None
            invalid = (
                parsed is None
                or not value
                or "\\" in value
                or parsed.is_absolute()
                or ".." in parsed.parts
                or ":" in parsed.parts[0]
            )
            if invalid:
                diagnostics.append(f"{model_id}: asset path must be a relative packet path: {value}")
                continue
            if value in record_paths:
                diagnostics.append(f"{model_id}: duplicate asset path: {value}")
                continue
            record_paths.add(value)
            records.append(record)
    try:
        validate_promoted_files(art_root, records)
    except PacketError as exc:
        diagnostics.append(str(exc))
    return diagnostics


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--approval-manifest", type=Path, default=DEFAULT_APPROVAL_MANIFEST)
    parser.add_argument("--art-root", type=Path, default=DEFAULT_ART_ROOT)
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    args = parser.parse_args()
    packet = json.loads(args.manifest.read_text(encoding="utf-8"))
    approval = json.loads(args.approval_manifest.read_text(encoding="utf-8"))
    diagnostics = validate_approval_packet(approval, args.repo_root)
    diagnostics.extend(validate_packet(packet, args.art_root))
    if diagnostics:
        for item in diagnostics:
            print(f"ERROR: {item}")
        return 1
    print(json.dumps({"status": "PASS", "approved2D": len(approval["entries"]), "models": len(packet["models"]), "runtimeIntegrationState": "Blocked"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
