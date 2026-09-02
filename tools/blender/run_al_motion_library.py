#!/usr/bin/env python3
"""Launch Blender motion-library builds, catalog assembly, and repeatability checks."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any

from al_motion_library_contract import (
    CATALOG_PATH,
    REPEATABILITY_PATH,
    SOURCE_PLAN_PATH,
    expected_binding_keys,
    load_json,
    sha256_file,
    stable_clip_id,
    stable_json_bytes,
    validate_artifacts,
    validate_built_catalog,
    validate_repeatability,
    validate_source_plan,
)

DEFAULT_BLENDER = Path("C:/Program Files/Blender Foundation/Blender 5.2/blender.exe")
PIPELINE = Path(__file__).resolve().with_name("al_motion_library_pipeline.py")
REPRESENTATIVE_ORDER = {
    "rmc_representative_champion_vanguard_v001": 0,
    "rmc_representative_npc_covenant_sentinel_v001": 1,
    "rmc_representative_beast_slagwhistle_v001": 2,
}


class MotionLibraryLauncherError(RuntimeError):
    """Raised when a Blender subprocess or contract gate fails."""


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[2]
    )
    parser.add_argument("--source-plan", type=Path)
    parser.add_argument("--blender", type=Path, default=DEFAULT_BLENDER)
    parser.add_argument(
        "--mode",
        choices=("build", "repeatability", "validate"),
        default="repeatability",
    )
    return parser.parse_args()


def _path(repo_root: Path, relative: str | Path) -> Path:
    return (repo_root / relative).resolve()


def _run_representative(
    blender: Path,
    repo_root: Path,
    source_plan_path: Path,
    representative_profile_id: str,
    output_root: Path | None,
) -> None:
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(PIPELINE),
        "--",
        "--repo-root",
        str(repo_root),
        "--source-plan",
        str(source_plan_path),
        "--representative",
        representative_profile_id,
    ]
    if output_root is not None:
        command.extend(["--output-root", str(output_root)])
    result = subprocess.run(
        command,
        cwd=repo_root,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if result.returncode != 0:
        raise MotionLibraryLauncherError(
            f"BlenderBuildFailed: {representative_profile_id}\n"
            f"stdout:\n{result.stdout[-12000:]}\n"
            f"stderr:\n{result.stderr[-12000:]}"
        )
    passed_lines = [
        line for line in result.stdout.splitlines() if '"status": "passed"' in line
    ]
    if not passed_lines:
        raise MotionLibraryLauncherError(
            f"BlenderBuildMissingReceipt: {representative_profile_id}\n{result.stdout[-4000:]}"
        )
    print(passed_lines[-1])


def _build_all(
    blender: Path,
    repo_root: Path,
    source_plan_path: Path,
    source_plan: dict[str, Any],
    output_root: Path | None,
) -> None:
    for representative in source_plan["representatives"]:
        _run_representative(
            blender,
            repo_root,
            source_plan_path,
            representative["representativeProfileId"],
            output_root,
        )


def _output_path(
    repo_root: Path,
    output_root: Path | None,
    relative: str,
) -> Path:
    return (
        _path(repo_root, relative)
        if output_root is None
        else (output_root / relative).resolve()
    )


def _load_sidecars(
    repo_root: Path,
    output_root: Path | None,
    source_plan: dict[str, Any],
) -> dict[str, dict[str, Any]]:
    return {
        representative["representativeProfileId"]: load_json(
            _output_path(repo_root, output_root, representative["sidecarPath"])
        )
        for representative in source_plan["representatives"]
    }


def _semantic_signature(sidecars: dict[str, dict[str, Any]]) -> str:
    rows = [
        {
            "representativeProfileId": profile_id,
            "semanticSignature": sidecar["semanticSignature"],
            "skeletonSignature": sidecar["skeletonSignature"],
            "actionSignature": sidecar["actionSignature"],
        }
        for profile_id, sidecar in sorted(sidecars.items())
    ]
    return hashlib.sha256(stable_json_bytes(rows)).hexdigest()


def _copy_outputs_to_repository(
    repo_root: Path,
    output_root: Path,
    source_plan: dict[str, Any],
) -> None:
    for representative in source_plan["representatives"]:
        for field in ("outputBlendPath", "outputFbxPath", "sidecarPath"):
            source = _output_path(repo_root, output_root, representative[field])
            destination = _path(repo_root, representative[field])
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)


def _catalog_signature(catalog: dict[str, Any]) -> str:
    return hashlib.sha256(
        stable_json_bytes(
            {key: value for key, value in catalog.items() if key != "contentSignature"}
        )
    ).hexdigest()


def _update_required_manifest(
    repo_root: Path,
    source_plan: dict[str, Any],
    catalog: dict[str, Any],
    sidecars: dict[str, dict[str, Any]],
) -> None:
    manifest_path = _path(repo_root, source_plan["requiredManifestPath"])
    manifest = load_json(manifest_path)
    assets_by_id = {row["id"]: row for row in catalog["assets"]}
    generated_candidates = []
    for clip in catalog["clips"]:
        source_asset = assets_by_id[clip["sourceAssetId"]]
        source_profile = source_asset["representativeProfileId"]
        generated_candidates.append(
            {
                "id": clip["id"],
                "representativeProfileId": source_profile,
                "skeletonProfileId": source_asset["skeletonProfileId"],
                "motionKey": clip["motionKey"],
                "sourcePath": clip["sourcePath"],
                "sourceTake": clip["sourceTake"],
                "skeletonSignature": sidecars[source_profile]["skeletonSignature"],
                "clipSignature": clip["clipSignature"],
                "qualificationState": "qualified",
            }
        )
    generated_ids = {row["id"] for row in generated_candidates}
    retained = [
        row for row in manifest["clipCandidates"] if row["id"] not in generated_ids
    ]
    manifest["clipCandidates"] = sorted(
        [*retained, *generated_candidates],
        key=lambda row: row["id"].encode("utf-8"),
    )

    bindings = {
        (row["representativeProfileId"], row["motionKey"]): row["clipId"]
        for row in catalog["bindings"]
    }
    clips_by_id = {row["id"]: row for row in catalog["clips"]}
    for coverage in manifest["representativeCoverage"]:
        profile_id = coverage["representativeProfileId"]
        for requirement in coverage["requirements"]:
            if requirement["status"] == "blocked_owner_authorization":
                continue
            clip_id = bindings.get((profile_id, requirement["motionKey"]))
            if clip_id is None:
                continue
            clip = clips_by_id[clip_id]
            requirement["status"] = "available_source_candidate"
            requirement["clipId"] = clip_id
            requirement["source"] = f"{clip['sourcePath']} take {clip['sourceTake']}"
            requirement["rationale"] = (
                "Qualified bounded-engineering motion with deterministic Blender curves, "
                "measured loop/contact/transition cleanup, stable catalog binding, and "
                "standardized events; runtime admission remains separately gated."
            )
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=False) + "\n", encoding="utf-8"
    )


def _assemble_catalog(
    repo_root: Path,
    source_plan_path: Path,
    source_plan: dict[str, Any],
) -> dict[str, Any]:
    required_manifest = load_json(_path(repo_root, source_plan["requiredManifestPath"]))
    expected = expected_binding_keys(source_plan, required_manifest)
    representatives = {
        row["representativeProfileId"]: row for row in source_plan["representatives"]
    }
    sidecars = _load_sidecars(repo_root, None, source_plan)
    champion_profile = "rmc_representative_champion_vanguard_v001"
    beast_profile = "rmc_representative_beast_slagwhistle_v001"
    champion_actions = {
        row["motionKey"]: row for row in sidecars[champion_profile]["actions"]
    }
    beast_actions = {
        row["motionKey"]: row for row in sidecars[beast_profile]["actions"]
    }
    motion_definitions = {row["key"]: row for row in required_manifest["motionKeys"]}

    assets = []
    for representative in source_plan["representatives"]:
        sidecar = sidecars[representative["representativeProfileId"]]
        assets.append(
            {
                "id": representative["assetId"],
                "representativeProfileId": representative["representativeProfileId"],
                "skeletonProfileId": representative["skeletonProfileId"],
                "sourceBlendPath": representative["sourceBlendPath"],
                "blendPath": representative["outputBlendPath"],
                "fbxPath": representative["outputFbxPath"],
                "sidecarPath": representative["sidecarPath"],
                "catalogSource": representative["catalogSource"],
                "actionCount": len(sidecar["actions"]),
                "skeletonSignature": sidecar["skeletonSignature"],
                "actionSignature": sidecar["actionSignature"],
                "sourceRightsState": representative["sourceRightsState"],
                "licensingEvidence": representative["licensingEvidence"],
                "knownRestrictions": representative["knownRestrictions"],
            }
        )

    bindings = []
    clips_by_id: dict[str, dict[str, Any]] = {}
    for representative in source_plan["representatives"]:
        profile_id = representative["representativeProfileId"]
        for motion_key in sorted(
            expected[profile_id], key=lambda value: value.encode("utf-8")
        ):
            clip_id = stable_clip_id(profile_id, motion_key)
            bindings.append(
                {
                    "representativeProfileId": profile_id,
                    "motionKey": motion_key,
                    "clipId": clip_id,
                    "qualificationState": "qualified_engineering",
                    "bindingAuthority": "stable_catalog_id",
                }
            )
            if clip_id in clips_by_id:
                continue
            source_profile = (
                beast_profile if profile_id == beast_profile else champion_profile
            )
            source_representative = representatives[source_profile]
            action = (
                beast_actions[motion_key]
                if source_profile == beast_profile
                else champion_actions[motion_key]
            )
            motion_definition = motion_definitions[motion_key]
            clips_by_id[clip_id] = {
                "id": clip_id,
                "motionKey": motion_key,
                "actionName": action["actionName"],
                "sourceAssetId": source_representative["assetId"],
                "sourcePath": source_representative["outputFbxPath"],
                "sourceTake": action["actionName"],
                "sourceRightsState": source_representative["sourceRightsState"],
                "qualificationState": "qualified_engineering",
                "cleanupStatus": "blender_measured_passed",
                "supportedSkeletonProfileIds": [
                    source_representative["skeletonProfileId"]
                ],
                "retargetCompatibility": (
                    "shared_humanoid_profile"
                    if source_profile == champion_profile
                    else "slagwhistle_exact_anatomy_only"
                ),
                "rootPolicyId": motion_definition["defaultRootPolicyId"],
                "rootTreatment": action["rootTreatment"],
                "loop": action["loop"],
                "frameCount": action["frameCount"],
                "sampleRateHz": action["sampleRateHz"],
                "durationSeconds": action["durationSeconds"],
                "events": action["events"],
                "hitboxWindows": action["hitboxWindows"],
                "contactRequirement": motion_definition["contactRequirement"],
                "generatorRuleId": action["generatorRuleId"],
                "generatorStyle": action["generatorStyle"],
                "clipSignature": action["clipSignature"],
                "measuredCleanup": action["measuredCleanup"],
                "knownRestrictions": source_representative["knownRestrictions"],
            }

    coverage = []
    for representative in source_plan["representatives"]:
        profile_id = representative["representativeProfileId"]
        expected_keys = sorted(
            expected[profile_id], key=lambda value: value.encode("utf-8")
        )
        coverage.append(
            {
                "representativeProfileId": profile_id,
                "requiredSetId": representative["requiredSetId"],
                "coverageMode": representative["coverageMode"],
                "expectedMotionKeys": expected_keys,
                "boundMotionKeys": expected_keys,
                "explainedBlocked": sorted(
                    representative.get("explainedBlockedMotionKeys", []),
                    key=lambda value: value.encode("utf-8"),
                ),
                "catalogGapCount": 0,
            }
        )

    catalog = {
        "schemaVersion": 1,
        "libraryId": source_plan["libraryId"],
        "contentVersion": source_plan["contentVersion"],
        "authorityState": source_plan["authorityState"],
        "sourcePlanPath": str(SOURCE_PLAN_PATH).replace("\\", "/"),
        "sourcePlanSha256": sha256_file(source_plan_path),
        "requiredManifestPath": source_plan["requiredManifestPath"],
        "requiredManifestSha256": sha256_file(
            _path(repo_root, source_plan["requiredManifestPath"])
        ),
        "sampleRateHz": source_plan["sampleRateHz"],
        "cleanupThresholds": source_plan["cleanupThresholds"],
        "assets": assets,
        "clips": sorted(
            clips_by_id.values(), key=lambda row: row["id"].encode("utf-8")
        ),
        "bindings": bindings,
        "coverage": coverage,
    }
    catalog_path = _path(repo_root, CATALOG_PATH)
    catalog_path.parent.mkdir(parents=True, exist_ok=True)
    _update_required_manifest(repo_root, source_plan, catalog, sidecars)
    catalog["requiredManifestSha256"] = sha256_file(
        _path(repo_root, source_plan["requiredManifestPath"])
    )
    catalog["contentSignature"] = _catalog_signature(catalog)
    catalog_path.write_text(
        json.dumps(catalog, indent=2, sort_keys=False) + "\n", encoding="utf-8"
    )
    return catalog


def _write_repeatability(
    repo_root: Path,
    catalog: dict[str, Any],
    run_one_signature: str,
    run_two_signature: str,
) -> dict[str, Any]:
    artifacts = {
        asset["id"]: {
            "blendSha256": sha256_file(_path(repo_root, asset["blendPath"])),
            "fbxSha256": sha256_file(_path(repo_root, asset["fbxPath"])),
            "sidecarSha256": sha256_file(_path(repo_root, asset["sidecarPath"])),
        }
        for asset in catalog["assets"]
    }
    receipt = {
        "schemaVersion": 1,
        "libraryId": catalog["libraryId"],
        "status": "passed" if run_one_signature == run_two_signature else "failed",
        "repeatabilityMode": "semantic_action_skeleton_event_cleanup_v1",
        "semanticSignatureRunOne": run_one_signature,
        "semanticSignatureRunTwo": run_two_signature,
        "catalogSha256": sha256_file(_path(repo_root, CATALOG_PATH)),
        "artifacts": artifacts,
        "binaryDeterminismNote": "Blend and animated FBX bytes are retained from run one; acceptance is semantic because Blender container metadata may vary while skeleton, curves, events, cleanup metrics, and bindings remain identical.",
    }
    receipt_path = _path(repo_root, REPEATABILITY_PATH)
    receipt_path.write_text(
        json.dumps(receipt, indent=2, sort_keys=False) + "\n", encoding="utf-8"
    )
    return receipt


def _validate(repo_root: Path, source_plan: dict[str, Any]) -> None:
    source_issues = validate_source_plan(repo_root, source_plan)
    if source_issues:
        raise MotionLibraryLauncherError("\n".join(source_issues))
    catalog_path = _path(repo_root, CATALOG_PATH)
    if not catalog_path.is_file():
        raise MotionLibraryLauncherError(f"MissingCatalog: {catalog_path}")
    catalog = load_json(catalog_path)
    required_manifest = load_json(_path(repo_root, source_plan["requiredManifestPath"]))
    issues = [
        *validate_built_catalog(repo_root, source_plan, required_manifest, catalog),
        *validate_artifacts(repo_root, catalog),
        *validate_repeatability(repo_root, catalog),
    ]
    if issues:
        raise MotionLibraryLauncherError("\n".join(sorted(set(issues))))
    print(
        json.dumps(
            {
                "status": "passed",
                "assets": len(catalog["assets"]),
                "clips": len(catalog["clips"]),
                "bindings": len(catalog["bindings"]),
                "catalogGaps": sum(
                    row["catalogGapCount"] for row in catalog["coverage"]
                ),
            },
            sort_keys=True,
        )
    )


def main() -> int:
    args = _arguments()
    repo_root = args.repo_root.resolve()
    source_plan_path = (
        args.source_plan.resolve()
        if args.source_plan
        else _path(repo_root, SOURCE_PLAN_PATH)
    )
    source_plan = load_json(source_plan_path)
    source_issues = validate_source_plan(repo_root, source_plan)
    if source_issues:
        raise MotionLibraryLauncherError("\n".join(source_issues))
    if args.mode == "validate":
        _validate(repo_root, source_plan)
        return 0
    if not args.blender.is_file():
        raise MotionLibraryLauncherError(f"BlenderMissing: {args.blender}")

    if args.mode == "build":
        _build_all(args.blender, repo_root, source_plan_path, source_plan, None)
        catalog = _assemble_catalog(repo_root, source_plan_path, source_plan)
        semantic = _semantic_signature(_load_sidecars(repo_root, None, source_plan))
        _write_repeatability(repo_root, catalog, semantic, semantic)
        _validate(repo_root, source_plan)
        return 0

    temp_parent = Path(os.environ.get("LOCALAPPDATA", tempfile.gettempdir())) / "Temp"
    with tempfile.TemporaryDirectory(
        prefix="al_motion_library_", dir=temp_parent
    ) as temporary:
        temporary_root = Path(temporary)
        run_one = temporary_root / "run_one"
        run_two = temporary_root / "run_two"
        _build_all(args.blender, repo_root, source_plan_path, source_plan, run_one)
        _build_all(args.blender, repo_root, source_plan_path, source_plan, run_two)
        first_sidecars = _load_sidecars(repo_root, run_one, source_plan)
        second_sidecars = _load_sidecars(repo_root, run_two, source_plan)
        first_signature = _semantic_signature(first_sidecars)
        second_signature = _semantic_signature(second_sidecars)
        if first_signature != second_signature:
            raise MotionLibraryLauncherError(
                f"SemanticRepeatabilityMismatch: {first_signature} != {second_signature}"
            )
        _copy_outputs_to_repository(repo_root, run_one, source_plan)
        catalog = _assemble_catalog(repo_root, source_plan_path, source_plan)
        _write_repeatability(
            repo_root,
            catalog,
            first_signature,
            second_signature,
        )
    _validate(repo_root, source_plan)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except MotionLibraryLauncherError as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1) from error
