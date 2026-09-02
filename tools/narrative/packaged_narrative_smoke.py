#!/usr/bin/env python3
"""Evaluate packaged narrative-path evidence against editor evidence and build manifests."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

REQUIRED_EVIDENCE_FIELDS = (
    "schemaVersion",
    "status",
    "reasonCode",
    "applicationIsEditor",
    "unityVersion",
    "buildGuid",
    "enabledSceneManifestSha256",
    "generatedSceneManifestSha256",
    "narrativeCatalogSha256",
    "narrativePacketVersion",
    "entryChapterId",
    "entryQuestId",
    "progressedQuestStateId",
    "resumedQuestStateId",
    "sceneSequence",
)

MATERIAL_COMPARISON_FIELDS = (
    "reasonCode",
    "enabledSceneManifestSha256",
    "generatedSceneManifestSha256",
    "narrativeCatalogSha256",
    "narrativePacketVersion",
    "entryChapterId",
    "entryQuestId",
    "progressedQuestStateId",
    "resumedQuestStateId",
)

PASS_STATUS = "passed"
PASS_REASON = "narrative_representative_path"
FAILURE_TOKENS = (
    "[AL-NARRATIVE-MISSING]",
    "[AL-NARRATIVE-FAILED]",
    "[AL-SCENE-ACTIVE-MISMATCH]",
    "NullReferenceException:",
    "MissingReferenceException:",
)


class NarrativeSmokeError(RuntimeError):
    pass


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise NarrativeSmokeError(f"JSON object required: {path}")
    return payload


def evaluate_player_log(player_log: str) -> dict[str, Any]:
    observed: list[str] = []
    for line in (player_log or "").splitlines():
        for token in FAILURE_TOKENS:
            if token in line:
                return {
                    "status": "stop_ship",
                    "reasonCode": "narrative_failure_token",
                    "failureToken": token,
                    "observedEvidence": observed,
                }
        if "[AL-NARRATIVE-ACTIVE]" in line:
            observed.append("[AL-NARRATIVE-ACTIVE]")
        elif "[AL-NARRATIVE-PROGRESS]" in line:
            observed.append("[AL-NARRATIVE-PROGRESS]")
        elif "[AL-NARRATIVE-RESUMED]" in line:
            observed.append("[AL-NARRATIVE-RESUMED]")
    if "[AL-NARRATIVE-ACTIVE]" not in observed:
        return {
            "status": "running",
            "reasonCode": "narrative_entry_missing",
            "observedEvidence": observed,
        }
    return {
        "status": "passed" if "[AL-NARRATIVE-RESUMED]" in observed else "running",
        "reasonCode": PASS_REASON if "[AL-NARRATIVE-RESUMED]" in observed else "narrative_evidence_incomplete",
        "observedEvidence": observed,
    }


def evaluate_evidence_document(evidence: dict[str, Any]) -> dict[str, Any]:
    missing = [field for field in REQUIRED_EVIDENCE_FIELDS if field not in evidence]
    if missing:
        return {
            "status": "stop_ship",
            "reasonCode": "narrative_evidence_missing_field",
            "missingFields": missing,
        }
    if evidence.get("schemaVersion") != 1:
        return {"status": "stop_ship", "reasonCode": "narrative_evidence_schema"}
    if evidence.get("status") != PASS_STATUS or evidence.get("reasonCode") != PASS_REASON:
        return {
            "status": "stop_ship",
            "reasonCode": str(evidence.get("reasonCode") or "narrative_evidence_failed"),
        }
    if evidence.get("entryChapterId") != "CH00_FIRST_SIGNAL":
        return {"status": "stop_ship", "reasonCode": "narrative_entry_chapter"}
    if evidence.get("entryQuestId") != "OMEN_1":
        return {"status": "stop_ship", "reasonCode": "narrative_entry_quest"}
    if evidence.get("progressedQuestStateId") != "TALK_TO_VALERIUS":
        return {"status": "stop_ship", "reasonCode": "narrative_progress_state"}
    if evidence.get("resumedQuestStateId") != evidence.get("progressedQuestStateId"):
        return {"status": "stop_ship", "reasonCode": "narrative_resume_mismatch"}
    scene_sequence = evidence.get("sceneSequence")
    if not isinstance(scene_sequence, list) or "Kingdom" not in scene_sequence:
        return {"status": "stop_ship", "reasonCode": "narrative_scene_sequence"}
    for field in (
        "enabledSceneManifestSha256",
        "generatedSceneManifestSha256",
        "narrativeCatalogSha256",
    ):
        value = str(evidence.get(field) or "")
        if len(value) != 64 or any(ch not in "0123456789abcdef" for ch in value):
            return {"status": "stop_ship", "reasonCode": "narrative_manifest_hash"}
    return {
        "status": "passed",
        "reasonCode": PASS_REASON,
        "isolatedSaveClaimed": bool(evidence.get("isolatedSaveClaimed", True)),
    }


def compare_editor_and_package(editor: dict[str, Any], packaged: dict[str, Any]) -> dict[str, Any]:
    editor_eval = evaluate_evidence_document(editor)
    packaged_eval = evaluate_evidence_document(packaged)
    if editor_eval["status"] != "passed":
        return {"status": "stop_ship", "reasonCode": "editor_evidence_invalid", "editor": editor_eval}
    if packaged_eval["status"] != "passed":
        return {"status": "stop_ship", "reasonCode": "packaged_evidence_invalid", "packaged": packaged_eval}
    if editor.get("applicationIsEditor") is not True:
        return {"status": "stop_ship", "reasonCode": "editor_evidence_not_editor"}
    if packaged.get("applicationIsEditor") is not False:
        return {"status": "stop_ship", "reasonCode": "packaged_evidence_not_player"}
    diverged = [
        field
        for field in MATERIAL_COMPARISON_FIELDS
        if editor.get(field) != packaged.get(field)
    ]
    if diverged:
        return {
            "status": "stop_ship",
            "reasonCode": "editor_package_divergence",
            "divergedFields": diverged,
        }
    return {
        "status": "passed",
        "reasonCode": "editor_package_equivalent",
        "divergedFields": [],
    }


def attach_build_identity(evidence: dict[str, Any], build_manifest: dict[str, Any] | None) -> dict[str, Any]:
    attached = dict(evidence)
    if build_manifest:
        attached["buildManifestSha256"] = str(
            build_manifest.get("manifestSha256")
            or build_manifest.get("sha256")
            or ""
        )
        attached["buildTarget"] = str(build_manifest.get("target") or "")
        attached["buildStatus"] = str(build_manifest.get("status") or "")
    return attached


def launch_windows_player(
    executable: Path,
    output: Path,
    timeout_seconds: int = 180,
    extra_args: list[str] | None = None,
) -> dict[str, Any]:
    if not executable.is_file():
        raise NarrativeSmokeError(f"player executable missing: {executable}")
    output.parent.mkdir(parents=True, exist_ok=True)
    log_path = output.with_suffix(".player.log")
    command = [
        str(executable),
        "-batchmode",
        "-nographics",
        "-logFile",
        str(log_path),
        "--al-narrative-smoke",
        "--al-narrative-output",
        str(output),
    ]
    if extra_args:
        command.extend(extra_args)
    started = time.time()
    completed = subprocess.run(command, timeout=timeout_seconds, check=False)
    player_log = log_path.read_text(encoding="utf-8", errors="replace") if log_path.is_file() else ""
    log_eval = evaluate_player_log(player_log)
    if not output.is_file():
        return {
            "status": "stop_ship",
            "reasonCode": "narrative_evidence_file_missing",
            "exitCode": completed.returncode,
            "durationSeconds": round(time.time() - started, 3),
            "log": log_eval,
        }
    evidence = load_json(output)
    evidence_eval = evaluate_evidence_document(evidence)
    status = "passed" if evidence_eval["status"] == "passed" and completed.returncode == 0 else "stop_ship"
    return {
        "status": status,
        "reasonCode": evidence_eval.get("reasonCode") if status == "passed" else (
            evidence_eval.get("reasonCode") if evidence_eval["status"] != "passed" else "narrative_player_exit"
        ),
        "exitCode": completed.returncode,
        "durationSeconds": round(time.time() - started, 3),
        "evidence": evidence,
        "log": log_eval,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, help="Packaged evidence JSON")
    parser.add_argument("--editor-evidence", type=Path)
    parser.add_argument("--build-manifest", type=Path)
    parser.add_argument("--player", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--timeout-seconds", type=int, default=180)
    args = parser.parse_args()

    try:
        if args.player:
            if args.output is None:
                raise NarrativeSmokeError("--output is required with --player")
            payload = launch_windows_player(args.player, args.output, args.timeout_seconds)
            if args.build_manifest and isinstance(payload.get("evidence"), dict):
                payload["evidence"] = attach_build_identity(
                    payload["evidence"], load_json(args.build_manifest)
                )
        elif args.evidence:
            evidence = load_json(args.evidence)
            if args.build_manifest:
                evidence = attach_build_identity(evidence, load_json(args.build_manifest))
            payload = evaluate_evidence_document(evidence)
            payload["evidence"] = evidence
            if args.editor_evidence:
                payload = compare_editor_and_package(load_json(args.editor_evidence), evidence)
        else:
            raise NarrativeSmokeError("pass --evidence or --player")
    except (NarrativeSmokeError, OSError, json.JSONDecodeError, subprocess.TimeoutExpired) as error:
        print(f"narrative-smoke: {error}", file=sys.stderr)
        return 2

    json.dump(payload, sys.stdout, indent=2, sort_keys=True)
    sys.stdout.write("\n")
    return 0 if payload.get("status") == "passed" else 2


if __name__ == "__main__":
    raise SystemExit(main())
