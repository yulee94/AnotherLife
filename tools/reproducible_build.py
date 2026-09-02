#!/usr/bin/env python3
"""Fail-closed reproducible Unity build inventory, runner, and comparer.

The canonical manifest digest is SHA-256 over canonical JSON with the
``manifestSha256`` member omitted. The adjacent ``.sha256`` file is suitable as
the input to a detached signing system; this tool never invents a signature.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import ntpath
import os
import re
import shutil
import stat
import subprocess
import sys
import time
from contextlib import contextmanager, nullcontext
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Iterable


DEFAULT_POLICY = Path(__file__).parent / "builds/reproducible_build_policy.json"
HASH_BUFFER = 1024 * 1024
ANDROID_TARGETS = {
    "android-unity-library-debug",
    "android-unity-library-release",
}


class BuildContractError(RuntimeError):
    """A reproducibility, safety, compatibility, or evidence contract failure."""


def canonical_json(payload: Any) -> bytes:
    return (json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(HASH_BUFFER), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_policy(path: Path = DEFAULT_POLICY) -> dict[str, Any]:
    try:
        policy = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise BuildContractError(f"invalid reproducible-build policy: {path}: {error}") from error
    if policy.get("schemaVersion") != 1 or not isinstance(policy.get("targets"), dict):
        raise BuildContractError("reproducible-build policy schema/targets are invalid")
    return policy


def read_project_editor(project_dir: Path) -> dict[str, str]:
    path = Path(project_dir) / "ProjectSettings/ProjectVersion.txt"
    text = path.read_text(encoding="utf-8")
    version = re.search(r"^m_EditorVersion:\s*(\S+)\s*$", text, re.MULTILINE)
    revision = re.search(r"^m_EditorVersionWithRevision:\s*\S+\s+\(([^)]+)\)\s*$", text, re.MULTILINE)
    if not version or not revision:
        raise BuildContractError(f"ProjectVersion.txt is incomplete: {path}")
    return {"version": version.group(1), "revision": revision.group(1)}


def evaluate_android_compatibility(
    policy: dict[str, Any], project_editor: dict[str, str]
) -> dict[str, Any]:
    target = policy["deferredAndroid"]
    exporter_version = target["legacyExporterEditor"]
    project_version = project_editor["version"]
    return {
        "status": "deferred",
        "reasonCode": "mobile_deferred_pc_first",
        "mayLaunchExporter": False,
        "projectEditor": project_version,
        "exporterEditor": exporter_version,
        "approvedFutureEditor": target["approvedEditor"],
        "deferredTask": target["task"],
        "remediation": (
            "Mobile is owned by the deferred task. Install Android support for the canonical "
            "Unity editor and use that same editor with the pinned target API. Never open this "
            "Unity 6 project in the legacy Unity 2022.3 exporter."
        ),
    }


def _run(command: list[str], cwd: Path, *, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        cwd=cwd,
        check=check,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def _git(repo_root: Path, arguments: list[str]) -> str:
    result = _run(["git", *arguments], repo_root)
    return result.stdout.strip()


def _git_paths(repo_root: Path, roots: Iterable[str]) -> list[str]:
    result = subprocess.run(
        ["git", "ls-files", "-z", "--", *roots],
        cwd=repo_root,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    return sorted(
        (item.decode("utf-8", errors="strict").replace("\\", "/") for item in result.stdout.split(b"\0") if item),
        key=lambda value: value.encode("utf-8"),
    )


def _tree_inventory(repo_root: Path, relative_paths: Iterable[str]) -> tuple[dict[str, Any], str]:
    inventory: dict[str, Any] = {}
    lines = bytearray()
    for relative in sorted(set(relative_paths), key=lambda value: value.encode("utf-8")):
        path = repo_root / relative
        if not path.is_file() or path.is_symlink():
            raise BuildContractError(f"tracked build input is missing or symlinked: {relative}")
        size = path.stat().st_size
        digest = sha256_file(path)
        inventory[relative] = {"bytes": size, "sha256": digest}
        lines.extend(relative.encode("utf-8"))
        lines.extend(b"\0")
        lines.extend(str(size).encode("ascii"))
        lines.extend(b"\0")
        lines.extend(digest.encode("ascii"))
        lines.extend(b"\n")
    return inventory, sha256_bytes(bytes(lines))


def _parse_scenes(path: Path) -> list[dict[str, Any]]:
    text = path.read_text(encoding="utf-8")
    pattern = re.compile(
        r"- enabled:\s*(\d+)\s*\r?\n\s*path:\s*([^\r\n]+)\s*\r?\n\s*guid:\s*([0-9a-f]+)",
        re.MULTILINE,
    )
    scenes = [
        {"enabled": match.group(1) == "1", "path": match.group(2).strip(), "guid": match.group(3)}
        for match in pattern.finditer(text)
    ]
    if not scenes:
        raise BuildContractError(f"no scenes parsed from {path}")
    if len({scene["path"] for scene in scenes}) != len(scenes):
        raise BuildContractError("duplicate Build Settings scene path")
    return scenes


def _project_settings(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")

    def scalar(name: str, default: str = "") -> str:
        match = re.search(rf"^\s*{re.escape(name)}:\s*(.*?)\s*$", text, re.MULTILINE)
        return match.group(1) if match else default

    return {
        "bundleVersion": scalar("bundleVersion"),
        "androidMinimumSdkSerialized": int(scalar("AndroidMinSdkVersion", "0")),
        "androidTargetSdkSerialized": int(scalar("AndroidTargetSdkVersion", "0")),
        "androidTargetArchitecturesSerialized": int(scalar("AndroidTargetArchitectures", "0")),
        "androidUseCustomKeystore": scalar("androidUseCustomKeystore", "0") == "1",
        "customGradleTemplates": {
            "main": scalar("useCustomMainGradleTemplate", "0") == "1",
            "base": scalar("useCustomBaseGradleTemplate", "0") == "1",
            "properties": scalar("useCustomGradlePropertiesTemplate", "0") == "1",
            "settings": scalar("useCustomGradleSettingsTemplate", "0") == "1",
        },
    }


def collect_source_inventory(repo_root: Path, policy: dict[str, Any]) -> dict[str, Any]:
    repo_root = Path(repo_root).resolve()
    project_editor = read_project_editor(repo_root / "unity")
    if project_editor != policy["projectEditor"]:
        raise BuildContractError(
            f"project editor drift: expected {policy['projectEditor']}, actual {project_editor}"
        )
    source_inputs = policy["sourceInputs"]
    tracked = _git_paths(repo_root, source_inputs["trackedRoots"])
    for relative in source_inputs.get("explicitFiles", []):
        normalized = Path(relative).as_posix()
        if not (repo_root / normalized).is_file():
            raise BuildContractError(f"explicit build input is missing: {normalized}")
        tracked.append(normalized)
    tracked = sorted(set(tracked), key=lambda value: value.encode("utf-8"))
    input_files, source_tree_hash = _tree_inventory(repo_root, tracked)
    scenes = _parse_scenes(repo_root / "unity/ProjectSettings/EditorBuildSettings.asset")
    content_paths = [
        path
        for path in tracked
        if any(path == root or path.startswith(root.rstrip("/") + "/") for root in policy["sourceInputs"]["contentRoots"])
    ]
    content_paths.extend(
        "unity/" + scene["path"] for scene in scenes if scene["enabled"]
    )
    content_files, content_tree_hash = _tree_inventory(repo_root, content_paths)
    status = _git(
        repo_root,
        [
            "status",
            "--porcelain",
            "--untracked-files=all",
            "--",
            *source_inputs["trackedRoots"],
            *source_inputs.get("explicitFiles", []),
        ],
    )
    return {
        "sourceRevision": _git(repo_root, ["rev-parse", "HEAD"]),
        "trackedInputsDirty": bool(status),
        "sourceTreeSha256": source_tree_hash,
        "contentTreeSha256": content_tree_hash,
        "projectEditor": project_editor,
        "projectSettings": _project_settings(repo_root / "unity/ProjectSettings/ProjectSettings.asset"),
        "scenes": scenes,
        "inputFiles": input_files,
        "contentFiles": content_files,
    }


def manifest_source_summary(source: dict[str, Any]) -> dict[str, Any]:
    """Select the complete traceability fields embedded in every build manifest."""
    return {
        "sourceRevision": source["sourceRevision"],
        "sourceTreeSha256": source["sourceTreeSha256"],
        "trackedInputsDirty": source["trackedInputsDirty"],
        "projectEditor": source["projectEditor"],
        "projectSettings": source["projectSettings"],
        "inputFiles": source["inputFiles"],
    }


def _canonical_windows_path(value: str) -> str:
    normalized = ntpath.normpath((value or "").strip().replace("/", "\\"))
    if not ntpath.isabs(normalized):
        return ""
    return normalized.rstrip("\\").casefold()


def evaluate_windows_launch_smoke(
    player_log: str,
    isolation: dict[str, Any],
    launch_policy: dict[str, Any],
) -> dict[str, Any]:
    required = {
        "developerIdentity",
        "launchIdentity",
        "developerLocalLow",
        "launchLocalLow",
        "launchPersistentDataPath",
        "freshProfile",
        "profileChainHasNoReparsePoints",
    }
    if not isinstance(isolation, dict) or not required.issubset(isolation):
        return {
            "status": "stop_ship",
            "reasonCode": "isolation_evidence_missing",
            "observedEvidence": [],
            "isolatedProfileClaimed": False,
        }
    claimed = bool(isolation.get("isolatedProfileClaimed"))
    if claimed:
        return {
            "status": "stop_ship",
            "reasonCode": "isolated_profile_not_claimed",
            "observedEvidence": [],
            "isolatedProfileClaimed": True,
        }
    if isolation["developerIdentity"].strip().casefold() != isolation["launchIdentity"].strip().casefold():
        return {
            "status": "stop_ship",
            "reasonCode": "launch_identity_not_current_user",
            "observedEvidence": [],
            "isolatedProfileClaimed": False,
        }
    launch_local_low = _canonical_windows_path(isolation["launchLocalLow"])
    launch_persistent = _canonical_windows_path(isolation["launchPersistentDataPath"])
    expected_persistent = _canonical_windows_path(ntpath.join(
        isolation["launchLocalLow"],
        launch_policy["companyName"],
        launch_policy["productName"],
    ))
    if (
        not launch_local_low
        or not launch_persistent
        or ntpath.basename(launch_local_low).casefold() != "locallow"
        or launch_persistent != expected_persistent
    ):
        return {
            "status": "stop_ship",
            "reasonCode": "launch_persistent_path_invalid",
            "observedEvidence": [],
            "isolatedProfileClaimed": False,
        }
    if not isolation["profileChainHasNoReparsePoints"]:
        return {
            "status": "stop_ship",
            "reasonCode": "launch_profile_reparse_point",
            "observedEvidence": [],
            "isolatedProfileClaimed": False,
        }

    ordered = launch_policy["orderedEvidence"]
    observed: list[str] = []
    next_index = 0
    for line in (player_log or "").splitlines():
        for failure_token in launch_policy["failureTokens"]:
            if failure_token in line:
                return {
                    "status": "stop_ship",
                    "reasonCode": "launch_failure_token",
                    "failureToken": failure_token,
                    "observedEvidence": observed,
                    "isolatedProfileClaimed": False,
                }
        present = [index for index, token in enumerate(ordered) if token in line]
        if present:
            if present[0] != next_index:
                return {
                    "status": "stop_ship",
                    "reasonCode": "launch_evidence_out_of_order",
                    "observedEvidence": observed,
                    "isolatedProfileClaimed": False,
                }
            observed.append(ordered[next_index])
            next_index += 1
        elif "[AL-SCENE-ACTIVE]" in line:
            return {
                "status": "stop_ship",
                "reasonCode": "unexpected_scene_marker",
                "observedEvidence": observed,
                "isolatedProfileClaimed": False,
            }
    if next_index != len(ordered):
        return {
            "status": "running",
            "reasonCode": "launch_evidence_incomplete",
            "missingEvidence": ordered[next_index],
            "observedEvidence": observed,
            "isolatedProfileClaimed": False,
        }
    return {
        "status": "passed",
        "reasonCode": "boot_to_realm_selection",
        "observedEvidence": observed,
        "isolatedProfileClaimed": False,
    }


def should_send_explicit_continue(launch_result: dict[str, Any], launch_policy: dict[str, Any]) -> bool:
    if launch_result.get("status") != "running":
        return False
    ordered = launch_policy.get("orderedEvidence") or []
    if len(ordered) < 2:
        return False
    observed = launch_result.get("observedEvidence") or []
    return observed == ordered[:2] and launch_result.get("missingEvidence") == ordered[2]


def _foreground_window_for_pid(pid: int) -> bool:
    import ctypes
    from ctypes import wintypes

    user32 = ctypes.windll.user32
    kernel32 = ctypes.windll.kernel32
    found = []

    @ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    def callback(hwnd, _lparam):
        process_id = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(process_id))
        if process_id.value == pid and user32.IsWindowVisible(hwnd) and user32.GetWindow(hwnd, 4) == 0:
            found.append(hwnd)
            return False
        return True

    user32.EnumWindows(callback, 0)
    if not found:
        return False
    hwnd = found[0]
    if user32.GetForegroundWindow() == hwnd:
        return True
    fg = user32.GetForegroundWindow()
    fg_thread = user32.GetWindowThreadProcessId(fg, None)
    cur_thread = kernel32.GetCurrentThreadId()
    user32.AttachThreadInput(cur_thread, fg_thread, True)
    user32.SetWindowPos(hwnd, -1, 0, 0, 0, 0, 0x0003)
    user32.BringWindowToTop(hwnd)
    result = bool(user32.SetForegroundWindow(hwnd))
    user32.AttachThreadInput(cur_thread, fg_thread, False)
    return result


def send_keyboard_enter(pid: int | None = None) -> None:
    import ctypes

    if pid is not None:
        _foreground_window_for_pid(pid)
    user32 = ctypes.windll.user32
    INPUT_KEYBOARD = 1
    KEYEVENTF_KEYUP = 0x0002

    class KeyBdInput(ctypes.Structure):
        _fields_ = [
            ("wVk", ctypes.c_ushort),
            ("wScan", ctypes.c_ushort),
            ("dwFlags", ctypes.c_uint),
            ("time", ctypes.c_uint),
            ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong)),
        ]

    class Input(ctypes.Structure):
        class _I(ctypes.Union):
            _fields_ = [("ki", KeyBdInput)]

        _anonymous_ = ("i",)
        _fields_ = [("type", ctypes.c_uint), ("i", _I)]

    extra = ctypes.c_ulong(0)
    down = Input(type=INPUT_KEYBOARD, ki=KeyBdInput(0x0D, 0, 0, 0, ctypes.pointer(extra)))
    up = Input(type=INPUT_KEYBOARD, ki=KeyBdInput(0x0D, 0, KEYEVENTF_KEYUP, 0, ctypes.pointer(extra)))
    user32.SendInput(1, ctypes.byref(down), ctypes.sizeof(Input))
    time.sleep(0.05)
    user32.SendInput(1, ctypes.byref(up), ctypes.sizeof(Input))


@contextmanager
def temporary_empty_persistent_data(persistent_data: Path):
    persistent_data = Path(persistent_data)
    parent = persistent_data.parent
    backup = parent / f"{persistent_data.name}.pre-smoke"
    attributes = 0
    if persistent_data.exists():
        attributes = getattr(os.lstat(persistent_data), "st_file_attributes", 0)
        if persistent_data.is_symlink() or attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
            raise BuildContractError(f"refusing symlink/reparse-point save overlay: {persistent_data}")
    if backup.exists():
        raise BuildContractError(f"pre-smoke save backup already exists: {backup}")
    preserved = False
    if persistent_data.exists():
        persistent_data.rename(backup)
        preserved = True
    persistent_data.mkdir(parents=True, exist_ok=True)
    try:
        yield {
            "freshProfile": True,
            "userSavePreserved": preserved,
            "backupPath": str(backup) if preserved else None,
        }
    finally:
        if persistent_data.exists():
            shutil.rmtree(persistent_data)
        if preserved:
            backup.rename(persistent_data)


def current_windows_identity() -> str:
    result = _run(["whoami.exe"], Path.cwd())
    identity = result.stdout.strip()
    if not identity:
        raise BuildContractError("Windows identity could not be observed")
    return identity


def current_windows_local_low() -> str:
    profile = os.environ.get("USERPROFILE", "").strip()
    if not profile:
        raise BuildContractError("USERPROFILE is unavailable; launch profile cannot be observed")
    return str(Path(profile) / "AppData/LocalLow")


def _path_chain_has_no_reparse_points(path: Path) -> bool:
    cursor = Path(path)
    existing: list[Path] = []
    while True:
        if cursor.exists():
            existing.append(cursor)
        parent = cursor.parent
        if parent == cursor:
            break
        cursor = parent
    for item in existing:
        try:
            attributes = getattr(os.lstat(item), "st_file_attributes", 0)
        except OSError:
            return False
        if item.is_symlink() or attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
            return False
    return True


def _load_verified_manifest(path: Path) -> dict[str, Any]:
    payload = json.loads(Path(path).read_text(encoding="utf-8"))
    recorded = payload.get("manifestSha256")
    unsigned = copy.deepcopy(payload)
    unsigned.pop("manifestSha256", None)
    actual = sha256_bytes(canonical_json(unsigned))
    if not isinstance(recorded, str) or recorded != actual:
        raise BuildContractError(f"manifest digest is missing or invalid: {path}")
    return payload


def _write_launch_evidence(
    destination: Path,
    build_manifest_path: Path,
    build_manifest: dict[str, Any],
    isolation: dict[str, Any],
    launch_result: dict[str, Any],
    *,
    process: dict[str, Any] | None = None,
    player_log: Path | None = None,
) -> dict[str, Any]:
    payload = {
        "schemaVersion": 1,
        "target": "windows64-development-launch-smoke",
        "status": "succeeded" if launch_result.get("status") == "passed" else "stop_ship",
        "buildManifest": {
            "path": Path(build_manifest_path).resolve().as_posix(),
            "manifestSha256": build_manifest["manifestSha256"],
            "sourceRevision": build_manifest.get("source", {}).get("sourceRevision"),
            "sourceTreeSha256": build_manifest.get("source", {}).get("sourceTreeSha256"),
            "artifactTreeSha256": build_manifest.get("artifacts", {}).get("treeSha256"),
        },
        "isolation": isolation,
        "launchResult": launch_result,
        "process": process or {},
        "playerLog": {
            "path": player_log.resolve().as_posix() if player_log else None,
            "sha256": sha256_file(player_log) if player_log and player_log.is_file() else None,
        },
    }
    return write_signed_ready_manifest(payload, destination)


def run_windows_launch_smoke(
    policy: dict[str, Any],
    build_manifest_path: Path,
    evidence_output: Path,
    *,
    developer_identity: str,
    developer_local_low: str,
    player_log_path: Path | None = None,
    continue_sender: Callable[[int], None] | None = None,
    overlay_persistent_data: bool = True,
) -> dict[str, Any]:
    build_manifest_path = Path(build_manifest_path).resolve()
    evidence_output = Path(evidence_output).resolve()
    build_manifest = _load_verified_manifest(build_manifest_path)
    if build_manifest.get("target") != "windows64-development" or build_manifest.get("status") != "succeeded":
        raise BuildContractError("launch smoke requires a successful windows64-development build manifest")

    launch_policy = policy["launchSmoke"]
    launch_identity = current_windows_identity()
    launch_local_low = Path(current_windows_local_low()).resolve()
    persistent_data = launch_local_low / launch_policy["companyName"] / launch_policy["productName"]
    fresh_profile = not persistent_data.exists() or not any(persistent_data.rglob("*"))
    isolation = {
        "method": "current_authenticated_user",
        "isolatedProfileClaimed": False,
        "isolationWaiver": "owner-2026-09-02-current-user-boot-to-realm-selection",
        "developerIdentity": developer_identity,
        "launchIdentity": launch_identity,
        "developerLocalLow": developer_local_low,
        "launchLocalLow": str(launch_local_low),
        "launchPersistentDataPath": str(persistent_data),
        "freshProfile": fresh_profile,
        "profileChainHasNoReparsePoints": _path_chain_has_no_reparse_points(launch_local_low),
    }
    preflight = evaluate_windows_launch_smoke("", isolation, launch_policy)
    if preflight["status"] == "stop_ship":
        return _write_launch_evidence(
            evidence_output,
            build_manifest_path,
            build_manifest,
            isolation,
            preflight,
        )

    artifacts = build_manifest.get("artifacts", {})
    artifact_root = Path(artifacts.get("root", ""))
    executable = artifact_root / "AnotherLifeUnity.exe"
    executable_entry = next(
        (item for item in artifacts.get("files", []) if item.get("path") == "AnotherLifeUnity.exe"),
        None,
    )
    if (
        not executable.is_file()
        or not executable_entry
        or executable_entry.get("sha256") != sha256_file(executable)
    ):
        raise BuildContractError("Player executable is missing or does not match the build manifest")

    player_log = Path(player_log_path or evidence_output.with_suffix(".player.log")).resolve()
    if player_log.exists():
        raise BuildContractError(f"launch-smoke Player log must be absent before launch: {player_log}")
    player_log.parent.mkdir(parents=True, exist_ok=True)
    started = datetime.now(timezone.utc)
    launch_result: dict[str, Any] = {
        "status": "running",
        "reasonCode": "launch_evidence_incomplete",
        "observedEvidence": [],
    }
    externally_terminated = False
    continue_attempts = 0
    last_continue = 0.0
    sender = continue_sender or (
        send_keyboard_enter if launch_policy.get("continueControl") == "keyboard_enter" else None
    )
    process = None
    overlay_cm = (
        temporary_empty_persistent_data(persistent_data)
        if overlay_persistent_data
        else nullcontext()
    )
    with overlay_cm as overlay:
        if overlay:
            isolation["freshProfile"] = overlay["freshProfile"]
            isolation["userSavePreserved"] = overlay["userSavePreserved"]
            isolation["persistentOverlay"] = True
        process = subprocess.Popen(
            [
                str(executable),
                "-logFile",
                str(player_log),
                "-screen-fullscreen",
                "0",
                "-screen-width",
                "1280",
                "-screen-height",
                "720",
            ],
            cwd=artifact_root,
        )
        deadline = time.monotonic() + int(launch_policy["timeoutSeconds"])
        try:
            while time.monotonic() < deadline:
                log_text = player_log.read_text(encoding="utf-8", errors="replace") if player_log.is_file() else ""
                launch_result = evaluate_windows_launch_smoke(log_text, isolation, launch_policy)
                if launch_result["status"] in {"passed", "stop_ship"}:
                    break
                if process.poll() is not None:
                    launch_result = {
                        **launch_result,
                        "status": "stop_ship",
                        "reasonCode": "player_exited_before_transition",
                    }
                    break
                if sender is not None and should_send_explicit_continue(launch_result, launch_policy):
                    now = time.monotonic()
                    if now - last_continue >= 2.0:
                        sender(process.pid)
                        continue_attempts += 1
                        last_continue = now
                time.sleep(0.25)
            else:
                launch_result = {
                    **launch_result,
                    "status": "stop_ship",
                    "reasonCode": "launch_timeout",
                }
        finally:
            if process.poll() is None:
                externally_terminated = True
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)
    ended = datetime.now(timezone.utc)
    if player_log.is_file():
        final_result = evaluate_windows_launch_smoke(
            player_log.read_text(encoding="utf-8", errors="replace"),
            isolation,
            launch_policy,
        )
        if final_result["status"] == "stop_ship" or launch_result["status"] == "running":
            launch_result = final_result
    process_evidence = {
        "processId": process.pid,
        "startedAtUtc": started.isoformat(),
        "endedAtUtc": ended.isoformat(),
        "exitCode": process.returncode,
        "externallyTerminated": externally_terminated,
        "logWasAbsentBeforeLaunch": True,
        "continueControl": launch_policy.get("continueControl"),
        "continueAttempts": continue_attempts,
    }
    return _write_launch_evidence(
        evidence_output,
        build_manifest_path,
        build_manifest,
        isolation,
        launch_result,
        process=process_evidence,
        player_log=player_log,
    )


def _normalize_artifact(path: Path, relative: str, size: int, digest: str) -> dict[str, Any]:
    entry = {
        "path": relative,
        "bytes": size,
        "sha256": digest,
        "reproducibleBytes": size,
        "reproducibleSha256": digest,
        "normalization": [],
    }
    if relative != "AnotherLifeUnity_Data/boot.config":
        return entry

    text = path.read_text(encoding="utf-8")
    normalized_lines = []
    match_count = 0
    for line in text.splitlines(keepends=True):
        ending = "\r\n" if line.endswith("\r\n") else "\n" if line.endswith("\n") else ""
        content = line[: -len(ending)] if ending else line
        prefix = "player-connection-guid="
        if content.startswith(prefix):
            value = content[len(prefix):]
            if not value.isdecimal():
                raise BuildContractError("boot.config player-connection-guid must be decimal")
            match_count += 1
            content = prefix + "<normalized>"
        normalized_lines.append(content + ending)
    if match_count != 1:
        raise BuildContractError("boot.config must contain exactly one player-connection-guid")
    normalized = "".join(normalized_lines).encode("utf-8")
    entry["reproducibleBytes"] = len(normalized)
    entry["reproducibleSha256"] = sha256_bytes(normalized)
    entry["normalization"] = ["player-connection-guid"]
    return entry


def _artifact_entries(root: Path) -> tuple[list[dict[str, Any]], str, str]:
    if not root.is_dir() or root.is_symlink():
        raise BuildContractError(f"artifact root is missing or symlinked: {root}")
    entries = []
    tree = bytearray()
    reproducible_tree = bytearray()
    for path in sorted(
        (item for item in root.rglob("*") if item.is_file()),
        key=lambda item: item.relative_to(root).as_posix().encode("utf-8"),
    ):
        if path.is_symlink():
            raise BuildContractError(f"artifact symlink is forbidden: {path}")
        relative = path.relative_to(root).as_posix()
        size = path.stat().st_size
        digest = sha256_file(path)
        entry = _normalize_artifact(path, relative, size, digest)
        entries.append(entry)
        tree.extend(relative.encode("utf-8"))
        tree.extend(b"\0")
        tree.extend(str(size).encode("ascii"))
        tree.extend(b"\0")
        tree.extend(digest.encode("ascii"))
        tree.extend(b"\n")
        reproducible_tree.extend(relative.encode("utf-8"))
        reproducible_tree.extend(b"\0")
        reproducible_tree.extend(str(entry["reproducibleBytes"]).encode("ascii"))
        reproducible_tree.extend(b"\0")
        reproducible_tree.extend(entry["reproducibleSha256"].encode("ascii"))
        reproducible_tree.extend(b"\n")
    if not entries:
        raise BuildContractError(f"artifact root contains no files: {root}")
    return entries, sha256_bytes(bytes(tree)), sha256_bytes(bytes(reproducible_tree))


def _smoke_windows(root: Path) -> dict[str, Any]:
    failures = []
    executable = root / "AnotherLifeUnity.exe"
    global_managers = root / "AnotherLifeUnity_Data/globalgamemanagers"
    if not executable.is_file():
        failures.append("Windows Player executable is missing")
    elif executable.read_bytes()[:2] != b"MZ":
        failures.append("Windows Player executable lacks the PE MZ signature")
    elif executable.stat().st_size < 64:
        failures.append("Windows Player executable is too small")
    if not global_managers.is_file() or global_managers.stat().st_size <= 0:
        failures.append("AnotherLifeUnity_Data/globalgamemanagers is missing or empty")
    return {"status": "failed" if failures else "passed", "failures": failures}


def _smoke_android(root: Path) -> dict[str, Any]:
    failures = []
    aar_files = sorted(root.glob("unityLibrary-*.aar"))
    inventory = root / "inventory.json"
    if len(aar_files) != 1 or aar_files[0].stat().st_size <= 0:
        failures.append("exactly one non-empty staged unityLibrary AAR is required")
    if not inventory.is_file() or inventory.stat().st_size <= 0:
        failures.append("staged Android inventory.json is missing or empty")
    return {"status": "failed" if failures else "passed", "failures": failures}


def inspect_artifacts(root: Path, target: str) -> dict[str, Any]:
    root = Path(root).resolve()
    entries, tree_hash, reproducible_tree_hash = _artifact_entries(root)
    if target == "windows64-development":
        smoke = _smoke_windows(root)
    elif target in ANDROID_TARGETS:
        smoke = _smoke_android(root)
    else:
        raise BuildContractError(f"unsupported target: {target}")
    return {
        "root": root.as_posix(),
        "fileCount": len(entries),
        "totalBytes": sum(item["bytes"] for item in entries),
        "treeSha256": tree_hash,
        "reproducibleTotalBytes": sum(item["reproducibleBytes"] for item in entries),
        "reproducibleTreeSha256": reproducible_tree_hash,
        "files": entries,
        "smoke": smoke,
    }


def write_signed_ready_manifest(payload: dict[str, Any], destination: Path) -> dict[str, Any]:
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    unsigned = copy.deepcopy(payload)
    unsigned.pop("manifestSha256", None)
    digest = sha256_bytes(canonical_json(unsigned))
    signed_ready = copy.deepcopy(unsigned)
    signed_ready["manifestSha256"] = digest
    temporary = destination.with_name(destination.name + ".tmp")
    temporary.write_bytes(canonical_json(signed_ready))
    os.replace(temporary, destination)
    sidecar = destination.with_suffix(destination.suffix + ".sha256")
    sidecar.write_text(f"{digest}  {destination.name}\n", encoding="ascii")
    return signed_ready


def _without_normalized(payload: dict[str, Any]) -> dict[str, Any]:
    normalized = copy.deepcopy(payload)
    normalized.pop("run", None)
    normalized.pop("manifestSha256", None)
    artifacts = normalized.get("artifacts")
    if isinstance(artifacts, dict) and "reproducibleTreeSha256" in artifacts:
        artifacts.pop("totalBytes", None)
        artifacts.pop("treeSha256", None)
        for entry in artifacts.get("files", []):
            if "reproducibleSha256" in entry:
                entry.pop("bytes", None)
                entry.pop("sha256", None)
    return normalized


def _normalizations(*payloads: dict[str, Any]) -> list[str]:
    result = ["run", "manifestSha256"]
    declared = set()
    for payload in payloads:
        artifacts = payload.get("artifacts", {})
        for entry in artifacts.get("files", []):
            for field in entry.get("normalization", []):
                declared.add(f'{entry.get("path", "<unknown>")}:{field}')
    result.extend(sorted(declared, key=lambda value: value.encode("utf-8")))
    return result


def _differences(left: Any, right: Any, prefix: str = "") -> list[str]:
    if type(left) is not type(right):
        return [prefix or "<root>"]
    if isinstance(left, dict):
        differences = []
        for key in sorted(set(left) | set(right)):
            path = f"{prefix}.{key}" if prefix else key
            if key not in left or key not in right:
                differences.append(path)
            else:
                differences.extend(_differences(left[key], right[key], path))
        return differences
    if isinstance(left, list):
        if len(left) != len(right):
            return [prefix]
        differences = []
        for index, (left_item, right_item) in enumerate(zip(left, right)):
            differences.extend(_differences(left_item, right_item, f"{prefix}[{index}]"))
        return differences
    return [] if left == right else [prefix]


def compare_manifests(first: dict[str, Any], second: dict[str, Any]) -> dict[str, Any]:
    if first == second:
        return {"status": "identical", "normalization": [], "differences": []}
    left = _without_normalized(first)
    right = _without_normalized(second)
    differences = _differences(left, right)
    if not differences:
        return {
            "status": "normalized_equivalent",
            "normalization": _normalizations(first, second),
            "differences": [],
        }
    return {
        "status": "stop_ship",
        "normalization": _normalizations(first, second),
        "differences": differences,
    }


def preflight_build(
    repo_root: Path,
    policy: dict[str, Any],
    target: str,
    *,
    actual_unity_version: str,
    process_launcher: Callable[[list[str]], Any] | None = None,
) -> dict[str, Any]:
    if target not in policy["targets"]:
        return {"status": "stop_ship", "reasonCode": "unsupported_target"}
    expected = policy["targets"][target]["unityVersion"]
    if actual_unity_version != expected:
        return {
            "status": "stop_ship",
            "reasonCode": "unity_version_mismatch",
            "expected": expected,
            "actual": actual_unity_version,
        }
    if target in ANDROID_TARGETS:
        compatibility = evaluate_android_compatibility(policy, read_project_editor(Path(repo_root) / "unity"))
        if compatibility["status"] != "supported":
            return compatibility
    result = {"status": "ready", "reasonCode": "preflight_passed"}
    if process_launcher is not None:
        process_launcher([])
    return result


def probe_unity_version(unity_exe: Path) -> str:
    result = _run([str(Path(unity_exe).resolve()), "-version"], Path.cwd())
    version = (result.stdout or result.stderr).strip().splitlines()
    if not version:
        raise BuildContractError(f"Unity did not report a version: {unity_exe}")
    return version[-1].strip()


def _property(path: Path, name: str) -> str | None:
    if not path.is_file():
        return None
    match = re.search(
        rf"^{re.escape(name)}\s*=\s*\"?([^\"\r\n]+)\"?\s*$",
        path.read_text(encoding="utf-8", errors="replace"),
        re.MULTILINE,
    )
    return match.group(1).strip() if match else None


def collect_toolchain(repo_root: Path, unity_exe: Path, actual_version: str) -> dict[str, Any]:
    policy_files = {
        "gradleWrapperProperties": repo_root / "gradle/wrapper/gradle-wrapper.properties",
        "androidVersionCatalog": repo_root / "gradle/libs.versions.toml",
        "unityPackageLock": repo_root / "unity/Packages/packages-lock.json",
    }
    editor = Path(unity_exe).resolve().parent
    android = editor / "Data/PlaybackEngines/AndroidPlayer"
    sdk = android / "SDK"
    embedded_android = {
        "available": android.is_dir(),
        "jdkVersion": _property(android / "OpenJDK/release", "JAVA_VERSION"),
        "jdkImplementor": _property(android / "OpenJDK/release", "IMPLEMENTOR"),
        "ndkRevision": _property(android / "NDK/source.properties", "Pkg.Revision"),
        "buildTools": sorted(
            (path.parent.name for path in (sdk / "build-tools").glob("*/source.properties")),
            key=lambda value: value.encode("utf-8"),
        ),
        "platforms": sorted(
            (path.parent.name for path in (sdk / "platforms").glob("*/source.properties")),
            key=lambda value: value.encode("utf-8"),
        ),
        "platformToolsRevision": _property(sdk / "platform-tools/source.properties", "Pkg.Revision"),
    }
    wrapper_text = policy_files["gradleWrapperProperties"].read_text(encoding="utf-8")
    wrapper_match = re.search(r"gradle-([0-9][0-9.]*)-(?:bin|all)\.zip", wrapper_text)
    catalog_text = policy_files["androidVersionCatalog"].read_text(encoding="utf-8")
    agp_match = re.search(r'^(?:agp|androidGradlePlugin)\s*=\s*"([^"]+)"', catalog_text, re.MULTILINE)
    return {
        "unityVersion": actual_version,
        "unityExecutable": Path(unity_exe).resolve().as_posix(),
        "python": sys.version.split()[0],
        "embeddedAndroid": embedded_android,
        "hostGradle": {
            "wrapperVersion": wrapper_match.group(1) if wrapper_match else None,
            "androidGradlePluginVersion": agp_match.group(1) if agp_match else None,
        },
        "files": {
            name: {"path": path.relative_to(repo_root).as_posix(), "sha256": sha256_file(path)}
            for name, path in policy_files.items()
        },
    }


def _assert_clean_tracked_inputs(repo_root: Path, policy: dict[str, Any]) -> None:
    source_inputs = policy["sourceInputs"]
    status = _git(
        repo_root,
        [
            "status",
            "--porcelain",
            "--untracked-files=all",
            "--",
            *source_inputs["trackedRoots"],
            *source_inputs.get("explicitFiles", []),
        ],
    )
    if status:
        raise BuildContractError("build inputs are dirty; commit or restore them before a reproducibility run")


def _remove_guarded_directory(repo_root: Path, relative: str) -> None:
    root = repo_root.resolve()
    requested = root / relative
    if requested.exists():
        attributes = getattr(os.lstat(requested), "st_file_attributes", 0)
        if requested.is_symlink() or attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
            raise BuildContractError(f"refusing symlink/reparse-point cleanup: {requested}")
    candidate = requested.resolve()
    if candidate == root or root not in candidate.parents:
        raise BuildContractError(f"refusing unguarded cleanup: {candidate}")
    if candidate.exists():
        shutil.rmtree(candidate)


def clean_target_outputs(repo_root: Path, target_policy: dict[str, Any]) -> None:
    for relative in target_policy.get("cleanInputs", []):
        _remove_guarded_directory(repo_root, relative)


def create_android_package_command(
    repo_root: Path,
    target_policy: dict[str, Any],
    unity_version: str,
) -> list[str]:
    repo_root = Path(repo_root).resolve()
    return [
        sys.executable,
        str(repo_root / "tools/android_unity_package.py"),
        "--variant",
        target_policy["buildProfile"],
        "--export-dir",
        str(repo_root / target_policy["exportRoot"]),
        "--artifacts-dir",
        str((repo_root / target_policy["artifactRoot"]).parent),
        "--unity-version",
        unity_version,
        "--repo-root",
        str(repo_root),
    ]


def run_build(
    repo_root: Path,
    policy: dict[str, Any],
    target: str,
    unity_exe: Path,
    manifest_path: Path,
    *,
    clean_library: bool,
) -> dict[str, Any]:
    repo_root = Path(repo_root).resolve()
    actual_version = probe_unity_version(unity_exe)
    preflight = preflight_build(repo_root, policy, target, actual_unity_version=actual_version)
    if preflight["status"] != "ready":
        source = collect_source_inventory(repo_root, policy)
        payload = {
            "schemaVersion": 1,
            "target": target,
            "status": "stop_ship",
            "stopShip": preflight,
            "authorityReferences": policy["authorityReferences"],
            "source": manifest_source_summary(source),
            "toolchain": collect_toolchain(repo_root, unity_exe, actual_version),
            "settings": policy["targets"][target],
            "scenes": source["scenes"],
            "content": {
                "treeSha256": source["contentTreeSha256"],
                "files": source["contentFiles"],
            },
            "run": {"startedAtUtc": datetime.now(timezone.utc).isoformat(), "endedAtUtc": datetime.now(timezone.utc).isoformat()},
        }
        return write_signed_ready_manifest(payload, manifest_path)
    _assert_clean_tracked_inputs(repo_root, policy)
    target_policy = policy["targets"][target]
    clean_target_outputs(repo_root, target_policy)
    if clean_library:
        _remove_guarded_directory(repo_root, "unity/Library")
    started = datetime.now(timezone.utc)
    log_path = repo_root / f"unity/Logs/Reproducible-{target}.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    command = [
        str(Path(unity_exe).resolve()),
        "-batchmode",
        "-quit",
        "-nographics",
        "-projectPath",
        str(repo_root / "unity"),
    ]
    if target in ANDROID_TARGETS:
        command.extend(["-buildTarget", "Android"])
    command.extend(["-executeMethod", target_policy["executeMethod"], "-logFile", str(log_path)])
    completed = subprocess.run(command, cwd=repo_root, check=False)
    ended = datetime.now(timezone.utc)
    if completed.returncode != 0:
        raise BuildContractError(f"Unity build failed with exit code {completed.returncode}; log={log_path}")
    artifact_root = repo_root / target_policy["artifactRoot"]
    if target in ANDROID_TARGETS:
        package_command = create_android_package_command(repo_root, target_policy, actual_version)
        subprocess.run(package_command, cwd=repo_root, check=True)
    source = collect_source_inventory(repo_root, policy)
    artifacts = inspect_artifacts(artifact_root, target)
    payload = {
        "schemaVersion": 1,
        "target": target,
        "status": "succeeded" if artifacts["smoke"]["status"] == "passed" else "stop_ship",
        "authorityReferences": policy["authorityReferences"],
        "source": manifest_source_summary(source),
        "toolchain": collect_toolchain(repo_root, unity_exe, actual_version),
        "settings": target_policy,
        "scenes": source["scenes"],
        "content": {"treeSha256": source["contentTreeSha256"], "files": source["contentFiles"]},
        "artifacts": artifacts,
        "run": {
            "startedAtUtc": started.isoformat(),
            "endedAtUtc": ended.isoformat(),
            "host": os.environ.get("COMPUTERNAME", ""),
            "unityLog": log_path.as_posix(),
            "cleanLibrary": clean_library,
        },
    }
    return write_signed_ready_manifest(payload, manifest_path)


def _print(payload: Any) -> None:
    print(json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    subparsers = parser.add_subparsers(dest="command", required=True)

    inventory_parser = subparsers.add_parser("inventory")
    inventory_parser.add_argument("--output", type=Path)

    preflight_parser = subparsers.add_parser("preflight")
    preflight_parser.add_argument("--target", required=True)
    preflight_parser.add_argument("--unity-exe", required=True, type=Path)

    build_parser = subparsers.add_parser("build")
    build_parser.add_argument("--target", required=True)
    build_parser.add_argument("--unity-exe", required=True, type=Path)
    build_parser.add_argument("--manifest", required=True, type=Path)
    build_parser.add_argument("--clean-library", action="store_true")

    compare_parser = subparsers.add_parser("compare")
    compare_parser.add_argument("first", type=Path)
    compare_parser.add_argument("second", type=Path)
    compare_parser.add_argument("--output", type=Path)

    launch_parser = subparsers.add_parser("launch-smoke")
    launch_parser.add_argument("--build-manifest", required=True, type=Path)
    launch_parser.add_argument("--output", required=True, type=Path)
    launch_parser.add_argument("--developer-identity")
    launch_parser.add_argument("--developer-local-low")
    launch_parser.add_argument("--player-log", type=Path)

    args = parser.parse_args(argv)
    repo_root = args.repo_root.resolve()
    try:
        policy = load_policy(args.policy)
        if args.command == "inventory":
            source = collect_source_inventory(repo_root, policy)
            payload = {
                "schemaVersion": 1,
                "status": "inventory_only",
                "authorityReferences": policy["authorityReferences"],
                "source": source,
                "androidCompatibility": evaluate_android_compatibility(policy, source["projectEditor"]),
            }
            if args.output:
                payload = write_signed_ready_manifest(payload, args.output)
            _print(payload)
            return 0
        if args.command == "preflight":
            actual = probe_unity_version(args.unity_exe)
            payload = preflight_build(repo_root, policy, args.target, actual_unity_version=actual)
            _print(payload)
            return 0 if payload["status"] == "ready" else 2
        if args.command == "build":
            payload = run_build(
                repo_root,
                policy,
                args.target,
                args.unity_exe,
                args.manifest,
                clean_library=args.clean_library,
            )
            _print(payload)
            return 0 if payload.get("status") == "succeeded" else 2
        if args.command == "compare":
            first = json.loads(args.first.read_text(encoding="utf-8"))
            second = json.loads(args.second.read_text(encoding="utf-8"))
            payload = compare_manifests(first, second)
            if args.output:
                args.output.parent.mkdir(parents=True, exist_ok=True)
                args.output.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            _print(payload)
            return 0 if payload["status"] in {"identical", "normalized_equivalent"} else 2
        if args.command == "launch-smoke":
            payload = run_windows_launch_smoke(
                policy,
                args.build_manifest,
                args.output,
                developer_identity=args.developer_identity or current_windows_identity(),
                developer_local_low=args.developer_local_low or current_windows_local_low(),
                player_log_path=args.player_log,
            )
            _print(payload)
            return 0 if payload.get("status") == "succeeded" else 2
        raise BuildContractError(f"unsupported command: {args.command}")
    except (BuildContractError, OSError, subprocess.CalledProcessError, json.JSONDecodeError) as error:
        print(f"reproducible-build: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
