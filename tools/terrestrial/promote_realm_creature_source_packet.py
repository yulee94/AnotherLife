#!/usr/bin/env python3
"""Promote reviewed realm-creature sources into a tracked, fail-closed packet."""

from __future__ import annotations

import hashlib
import shutil
from pathlib import Path
from typing import Any


class PacketError(ValueError):
    """Raised when source state cannot be promoted honestly."""


def _selected_path(model: dict[str, Any]) -> str | None:
    return model.get("file") or model.get("bestBase")


def has_owner_tier_texture_packet(records: list[dict[str, Any]]) -> bool:
    expected = {
        "base_color.png": [8192, 8192],
        "normal.png": [4096, 4096],
        "roughness.png": [4096, 4096],
        "metallic.png": [4096, 4096],
    }
    dimensions = {
        str(record.get("path", "")).rsplit("/", 1)[-1]: record.get("dimensions")
        for record in records
        if "/runtime_2k/" not in str(record.get("path", ""))
    }
    return all(dimensions.get(name) == size for name, size in expected.items())


def validate_input_manifests(
    approval: dict[str, Any],
    readiness: dict[str, Any],
    source_to_model: dict[str, str],
) -> dict[str, Any]:
    approval_entries = approval.get("entries")
    if (
        approval.get("rosterCount") != 21
        or approval.get("approvedCount") != 21
        or approval.get("blockedCount") != 0
        or not isinstance(approval_entries, list)
        or len(approval_entries) != 21
    ):
        raise PacketError("source packet requires exactly 21 approved 2D entries")

    source_ids = {entry.get("id") for entry in approval_entries}
    if None in source_ids or set(source_to_model) != source_ids:
        raise PacketError("source-to-model mapping must cover every approved 2D entry")

    models = readiness.get("models")
    if readiness.get("rosterCount") != 21 or not isinstance(models, list) or len(models) != 21:
        raise PacketError("3D readiness manifest must contain exactly 21 models")
    model_by_id = {model.get("id"): model for model in models}
    if None in model_by_id or len(model_by_id) != 21:
        raise PacketError("3D model IDs must be unique and nonblank")
    if set(source_to_model.values()) != set(model_by_id):
        raise PacketError("source-to-model mapping must cover every 3D model")
    if any(not _selected_path(model) for model in models):
        raise PacketError("every 3D entry must select a file or bestBase")

    structural_pass = sum("pass" in str(model.get("status", "")) for model in models)
    blocked = sum(bool(model.get("blocker")) for model in models)
    return {
        "approved2D": 21,
        "structuralPass": structural_pass,
        "blocked3D": blocked,
        "runtimeIntegrationState": "Blocked",
    }


def file_record(path: Path, root: Path) -> dict[str, Any]:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return {
        "path": path.relative_to(root).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": digest.hexdigest(),
    }


def copy_asset(source: Path, destination: Path, packet_root: Path) -> dict[str, Any]:
    if not source.is_file():
        raise PacketError(f"selected source asset is missing: {source}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    return file_record(destination, packet_root)


def validate_promoted_files(packet_root: Path, records: list[dict[str, Any]]) -> None:
    for record in records:
        path = packet_root / record["path"]
        if not path.is_file():
            raise PacketError(f"promoted file is missing: {record['path']}")
        actual = file_record(path, packet_root)
        if actual["bytes"] != record.get("bytes"):
            raise PacketError(f"byte-length mismatch: {record['path']}")
        if actual["sha256"] != record.get("sha256"):
            raise PacketError(f"hash mismatch: {record['path']}")
