#!/usr/bin/env python3
"""Build, verify, and stage Unity's generated Android AAR.

This script intentionally keeps Unity's generated Gradle project outside the
host Gradle graph. It assembles with the generated wrapper, validates the AAR
boundary, then atomically stages the matching debug/release artifact and a
machine-readable inventory for the Android host.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

SUPPORTED_VARIANTS = ("debug", "release")
SUPPORTED_ABIS = ("arm64-v8a",)
BUILD_PROFILES = {
    "debug": {
        "development": True,
        "il2CppCompilerConfiguration": "Debug",
        "managedStrippingLevel": "Minimal",
    },
    "release": {
        "development": False,
        "il2CppCompilerConfiguration": "Release",
        "managedStrippingLevel": "Medium",
    },
}
REQUIRED_ENTRIES = (
    "AndroidManifest.xml",
    "classes.jar",
    "assets/bin/Data/globalgamemanagers",
    "jni/arm64-v8a/libmain.so",
    "jni/arm64-v8a/libunity.so",
    "jni/arm64-v8a/libil2cpp.so",
    "proguard.txt",
)
UNITY_PLAYER_CLASS = "com/unity3d/player/UnityPlayer.class"


class PackageError(RuntimeError):
    """A packaging contract violation."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _validated_entries(archive: zipfile.ZipFile) -> dict[str, zipfile.ZipInfo]:
    entries: dict[str, zipfile.ZipInfo] = {}
    for info in archive.infolist():
        name = info.filename.replace("\\", "/")
        if name.startswith("/") or ".." in Path(name).parts:
            raise PackageError(f"unsafe AAR entry: {name}")
        if name in entries:
            raise PackageError(f"duplicate AAR entry: {name}")
        entries[name] = info
    missing = [name for name in REQUIRED_ENTRIES if name not in entries]
    if missing:
        raise PackageError("missing required AAR entries: " + ", ".join(missing))
    empty = [name for name in REQUIRED_ENTRIES if entries[name].file_size <= 0]
    if empty:
        raise PackageError("empty required AAR entries: " + ", ".join(empty))
    return entries


def _verify_elf(name: str, data: bytes) -> None:
    if len(data) < 20 or data[:4] != b"\x7fELF":
        raise PackageError(f"{name} is not an ELF file")
    if data[4] != 2 or data[5] not in (1, 2):
        raise PackageError(f"{name} must be a 64-bit ELF file")
    endian = "<" if data[5] == 1 else ">"
    machine = struct.unpack_from(endian + "H", data, 18)[0]
    if machine != 183:
        raise PackageError(f"{name} must target AArch64 (EM_AARCH64=183); found {machine}")


def inspect_aar(source: Path) -> tuple[list[dict[str, object]], list[str]]:
    if not source.is_file():
        raise PackageError(f"AAR does not exist: {source}")
    try:
        with zipfile.ZipFile(source) as archive:
            entries = _validated_entries(archive)
            abi_names = sorted(
                {
                    name.split("/")[1]
                    for name in entries
                    if name.startswith("jni/") and len(name.split("/")) >= 3
                }
            )
            unsupported = [abi for abi in abi_names if abi not in SUPPORTED_ABIS]
            if unsupported:
                raise PackageError("unsupported ABI directories in AAR: " + ", ".join(unsupported))
            if abi_names != list(SUPPORTED_ABIS):
                raise PackageError(f"AAR ABI set must be {list(SUPPORTED_ABIS)}; found {abi_names}")

            for name in (
                "jni/arm64-v8a/libmain.so",
                "jni/arm64-v8a/libunity.so",
                "jni/arm64-v8a/libil2cpp.so",
            ):
                _verify_elf(name, archive.read(name))

            classes_data = archive.read("classes.jar")
            try:
                from io import BytesIO

                with zipfile.ZipFile(BytesIO(classes_data)) as classes:
                    class_names = {item.filename for item in classes.infolist()}
            except zipfile.BadZipFile as error:
                raise PackageError("classes.jar is not a valid ZIP/JAR") from error
            if UNITY_PLAYER_CLASS not in class_names:
                raise PackageError(f"classes.jar is missing {UNITY_PLAYER_CLASS}")

            inventory = []
            for name in REQUIRED_ENTRIES:
                data = archive.read(name)
                inventory.append({"path": name, "bytes": len(data), "sha256": sha256_bytes(data)})
            return inventory, abi_names
    except zipfile.BadZipFile as error:
        raise PackageError(f"invalid AAR ZIP: {source}") from error


def verify_staged(
    artifacts_root: Path,
    variant: str,
    expected_repository_sha: str | None = None,
    expected_unity_version: str | None = None,
) -> tuple[Path, Path]:
    if variant not in SUPPORTED_VARIANTS:
        raise PackageError(f"unsupported variant {variant!r}; expected debug or release")
    target_dir = artifacts_root / variant
    target = target_dir / f"unityLibrary-{variant}.aar"
    inventory_path = target_dir / "inventory.json"
    required, abis = inspect_aar(target)
    if not inventory_path.is_file():
        raise PackageError(f"inventory does not exist: {inventory_path}")
    try:
        inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError) as error:
        raise PackageError(f"invalid inventory: {inventory_path}") from error
    expected_hash = sha256_file(target)
    if inventory.get("schemaVersion") != 1 or inventory.get("variant") != variant:
        raise PackageError(f"inventory schema/variant mismatch: {inventory_path}")
    if expected_repository_sha and inventory.get("repositorySha") != expected_repository_sha:
        raise PackageError(f"inventory repository SHA mismatch: {inventory_path}")
    if expected_unity_version and inventory.get("unityVersion") != expected_unity_version:
        raise PackageError(f"inventory Unity version mismatch: {inventory_path}")
    if inventory.get("buildProfile") != BUILD_PROFILES[variant]:
        raise PackageError(f"inventory build profile mismatch: {inventory_path}")
    if inventory.get("abis") != abis:
        raise PackageError(f"inventory ABI mismatch: {inventory_path}")
    aar = inventory.get("aar") or {}
    if aar.get("path") != target.name or aar.get("bytes") != target.stat().st_size:
        raise PackageError(f"inventory AAR identity mismatch: {inventory_path}")
    if aar.get("sha256") != expected_hash:
        raise PackageError(f"Unity AAR is stale or differs from its inventory: {target}")
    if inventory.get("requiredEntries") != required:
        raise PackageError(f"inventory required-entry mismatch: {inventory_path}")
    return target, inventory_path


def verify_and_stage(
    source: Path,
    artifacts_root: Path,
    variant: str,
    repository_sha: str,
    unity_version: str,
) -> tuple[Path, Path]:
    if variant not in SUPPORTED_VARIANTS:
        raise PackageError(f"unsupported variant {variant!r}; expected debug or release")
    required, abis = inspect_aar(source)
    target_dir = artifacts_root / variant
    target_dir.mkdir(parents=True, exist_ok=True)
    target = target_dir / f"unityLibrary-{variant}.aar"
    inventory_path = target_dir / "inventory.json"

    with tempfile.NamedTemporaryFile(dir=target_dir, delete=False) as temporary:
        temporary_path = Path(temporary.name)
        with source.open("rb") as input_stream:
            shutil.copyfileobj(input_stream, temporary)
    os.replace(temporary_path, target)

    payload = {
        "schemaVersion": 1,
        "repositorySha": repository_sha,
        "unityVersion": unity_version,
        "variant": variant,
        "abis": abis,
        "minimumApi": 24,
        "scriptingBackend": "IL2CPP",
        "buildProfile": BUILD_PROFILES[variant],
        "aar": {
            "path": target.name,
            "bytes": target.stat().st_size,
            "sha256": sha256_file(target),
        },
        "requiredEntries": required,
    }
    inventory_temp = inventory_path.with_suffix(".json.tmp")
    inventory_temp.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(inventory_temp, inventory_path)
    return target, inventory_path


def assemble(export_dir: Path, variant: str) -> Path:
    wrapper = export_dir / ("gradlew.bat" if os.name == "nt" else "gradlew")
    if not wrapper.is_file():
        raise PackageError(f"generated Unity Gradle wrapper is missing: {wrapper}")
    task = f":unityLibrary:assemble{variant.capitalize()}"
    command = [str(wrapper), ":unityLibrary:clean", task, "--no-daemon", "--stacktrace"]
    if os.name != "nt" and not os.access(wrapper, os.X_OK):
        command.insert(0, "sh")
    subprocess.run(command, cwd=export_dir, check=True)
    output = export_dir / "unityLibrary" / "build" / "outputs" / "aar" / f"unityLibrary-{variant}.aar"
    if not output.is_file():
        raise PackageError(f"Gradle succeeded but expected AAR is missing: {output}")
    return output


def repository_sha(repo_root: Path) -> str:
    result = subprocess.run(
        ["git", "rev-parse", "HEAD"], cwd=repo_root, check=True, text=True, capture_output=True
    )
    return result.stdout.strip()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--variant", required=True, choices=SUPPORTED_VARIANTS)
    parser.add_argument("--export-dir", type=Path, default=Path("unity/Builds/AndroidExport"))
    parser.add_argument("--artifacts-dir", type=Path, default=Path("unity/Builds/AndroidArtifacts"))
    parser.add_argument("--unity-version", default="2022.3.62f3")
    parser.add_argument("--source-aar", type=Path, help="verify/stage an existing AAR instead of assembling")
    parser.add_argument("--verify-only", action="store_true", help="verify an already staged AAR and inventory")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    args = parser.parse_args(argv)
    try:
        if args.verify_only:
            target, inventory = verify_staged(
                args.artifacts_dir.resolve(),
                args.variant,
                repository_sha(args.repo_root.resolve()),
                args.unity_version,
            )
        else:
            source = args.source_aar or assemble(args.export_dir.resolve(), args.variant)
            target, inventory = verify_and_stage(
                source.resolve(),
                args.artifacts_dir.resolve(),
                args.variant,
                repository_sha(args.repo_root.resolve()),
                args.unity_version,
            )
        print(f"staged={target}")
        print(f"inventory={inventory}")
        print(f"sha256={sha256_file(target)}")
        return 0
    except (PackageError, subprocess.CalledProcessError, OSError) as error:
        print(f"android-unity-package: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
