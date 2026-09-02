#!/usr/bin/env python3
"""Fail-closed launcher for the AnotherLife Blender rig cleanup pipeline."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any

sys.dont_write_bytecode = True
SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[1]
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from al_rig_pipeline_contract import (
    DEFAULT_MANIFEST,
    load_json,
    sha256_file,
    stable_json_bytes,
    validate_generated_sidecar,
    validate_manifest,
)

PIPELINE_SCRIPT = SCRIPT_DIR / "al_rig_cleanup_pipeline.py"
VALIDATION_REPORT = (
    REPO_ROOT / "unity/ArtSource/RigPipeline/al_rig_cleanup_validation_report.v1.json"
)
DEFAULT_WINDOWS_BLENDER = Path(
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"
)


class LauncherError(RuntimeError):
    """A launcher or repeatability check failed closed."""


def find_blender(explicit: str | None) -> Path:
    candidates = [
        Path(explicit) if explicit else None,
        Path(os.environ["AL_BLENDER_EXECUTABLE"])
        if os.environ.get("AL_BLENDER_EXECUTABLE")
        else None,
        Path(found) if (found := shutil.which("blender")) else None,
        DEFAULT_WINDOWS_BLENDER,
    ]
    for candidate in candidates:
        if candidate is not None and candidate.is_file():
            return candidate.resolve()
    raise LauncherError("Blender 5.2 executable not found; set AL_BLENDER_EXECUTABLE")


def select_assets(manifest: dict[str, Any], requested: str) -> list[dict[str, Any]]:
    if requested == "all":
        return manifest["assets"]
    matches = [asset for asset in manifest["assets"] if asset["id"] == requested]
    if len(matches) != 1:
        raise LauncherError(f"Unknown asset id: {requested}")
    return matches


def run_blender(
    blender: Path,
    command: str,
    asset: dict[str, Any],
) -> None:
    invocation = [
        str(blender),
        "--background",
        "--python-exit-code",
        "2",
        "--python",
        str(PIPELINE_SCRIPT),
        "--",
        command,
        "--asset",
        asset["id"],
        "--repo-root",
        str(REPO_ROOT),
    ]
    completed = subprocess.run(invocation, cwd=REPO_ROOT, check=False)
    if completed.returncode != 0:
        raise LauncherError(
            f"Blender {command} failed for {asset['id']} with exit code {completed.returncode}"
        )


def artifact_hashes(asset: dict[str, Any]) -> dict[str, str]:
    sidecar = load_json(REPO_ROOT / asset["output"]["sidecarPath"])
    hashes = {
        "blendContentSignature": sidecar["output"]["blendContentSignature"]
    }
    sidecar_path = REPO_ROOT / asset["output"]["sidecarPath"]
    receipt_path = REPO_ROOT / asset["output"]["fbxReceiptPath"]
    fbx_path = REPO_ROOT / asset["output"]["fbxPath"]
    for path in (sidecar_path, receipt_path, fbx_path):
        if not path.is_file():
            raise LauncherError(f"Missing artifact: {path.relative_to(REPO_ROOT)}")
    hashes["sidecarPath"] = sha256_file(sidecar_path)
    receipt = load_json(receipt_path)
    normalized_receipt = json.loads(json.dumps(receipt))
    normalized_receipt.get("export", {}).pop("sha256", None)
    receipt_signature = hashlib.sha256(stable_json_bytes(normalized_receipt)).hexdigest()
    hashes["fbxReceiptSemanticSignature"] = receipt_signature
    if asset["minimumSourceActions"]:
        hashes["fbxSemanticSignature"] = receipt_signature
    else:
        hashes["fbxPath"] = sha256_file(fbx_path)
    return hashes


def validate_artifacts(manifest: dict[str, Any], assets: list[dict[str, Any]]) -> None:
    for asset in assets:
        sidecar = load_json(REPO_ROOT / asset["output"]["sidecarPath"])
        validate_generated_sidecar(sidecar, asset)
        receipt = load_json(REPO_ROOT / asset["output"]["fbxReceiptPath"])
        fbx = REPO_ROOT / asset["output"]["fbxPath"]
        issues = []
        if receipt.get("status") != "export_valid":
            issues.append("receipt status")
        if receipt.get("roundTrip", {}).get("errors"):
            issues.append("round-trip errors")
        if receipt.get("export", {}).get("sha256") != sha256_file(fbx):
            issues.append("FBX hash")
        if receipt.get("source", {}).get("skeletonSignature") != sidecar[
            "skeleton"
        ]["signature"]:
            issues.append("skeleton signature")
        if receipt.get("source", {}).get("blendContentSignature") != sidecar[
            "output"
        ]["blendContentSignature"]:
            issues.append("blend content signature")
        if issues:
            raise LauncherError(f"Artifact validation failed for {asset['id']}: {issues}")


def validate_sidecars(assets: list[dict[str, Any]]) -> None:
    for asset in assets:
        sidecar = load_json(REPO_ROOT / asset["output"]["sidecarPath"])
        validate_generated_sidecar(sidecar, asset)


def verify_repeatability(
    blender: Path,
    manifest: dict[str, Any],
    assets: list[dict[str, Any]],
) -> dict[str, dict[str, str]]:
    before = {asset["id"]: artifact_hashes(asset) for asset in assets}
    for asset in assets:
        run_blender(blender, "build", asset)
        run_blender(blender, "preflight", asset)
        run_blender(blender, "export", asset)
    after = {asset["id"]: artifact_hashes(asset) for asset in assets}
    differences = {
        asset_id: {
            field: f"{before[asset_id][field]} != {after[asset_id][field]}"
            for field in before[asset_id]
            if before[asset_id][field] != after[asset_id][field]
        }
        for asset_id in before
    }
    differences = {key: value for key, value in differences.items() if value}
    if differences:
        raise LauncherError(f"Repeatability mismatch: {json.dumps(differences, sort_keys=True)}")
    return after


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "command",
        choices=("validate", "build", "preflight", "export", "repeatability", "all"),
    )
    parser.add_argument("--asset", default="all")
    parser.add_argument("--blender")
    return parser.parse_args()


def main() -> int:
    args = parse_arguments()
    manifest = load_json(DEFAULT_MANIFEST)
    assets = select_assets(manifest, args.asset)
    summary: dict[str, Any] = {"command": args.command, "assets": [row["id"] for row in assets]}

    if args.command in {"validate", "all"}:
        summary["manifest"] = validate_manifest(REPO_ROOT)
    if args.command == "validate":
        validate_artifacts(manifest, assets)
        summary["artifacts"] = "valid"
    else:
        blender = find_blender(args.blender)
        summary["blender"] = str(blender)
        if args.command in {"build", "all"}:
            for asset in assets:
                run_blender(blender, "build", asset)
        if args.command in {"preflight", "all"}:
            for asset in assets:
                run_blender(blender, "preflight", asset)
        if args.command in {"export", "all"}:
            for asset in assets:
                run_blender(blender, "export", asset)
        if args.command in {"repeatability", "all"}:
            summary["repeatability"] = verify_repeatability(
                blender, manifest, assets
            )
            report = {
                "schemaVersion": 1,
                "pipelineId": manifest["pipelineId"],
                "status": "validated",
                "determinismPolicy": manifest["determinismPolicy"],
                "assets": summary["repeatability"],
            }
            VALIDATION_REPORT.parent.mkdir(parents=True, exist_ok=True)
            VALIDATION_REPORT.write_text(
                json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8"
            )
            summary["report"] = str(VALIDATION_REPORT.relative_to(REPO_ROOT)).replace(
                "\\", "/"
            )
        if args.command in {"build", "preflight"}:
            validate_sidecars(assets)
            summary["artifacts"] = "sidecars_valid"
        else:
            validate_artifacts(manifest, assets)
            summary["artifacts"] = "valid"

    print("AL_RIG_LAUNCHER_PASS " + json.dumps(summary, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except LauncherError as error:
        print(f"AL_RIG_LAUNCHER_FAIL {error}", file=sys.stderr)
        raise SystemExit(2) from error
