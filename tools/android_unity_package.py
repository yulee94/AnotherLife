#!/usr/bin/env python3
"""Build, verify, and stage Unity's generated Android package inputs.

This script intentionally keeps Unity's generated Gradle project outside the
host Gradle graph. It assembles with the generated wrapper, validates the AAR
boundary, atomically stages the matching debug/release artifact and inventory,
and verifies that the final host APK retained the required Unity runtime.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
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
UNITY_PLAYER_DESCRIPTOR = "Lcom/unity3d/player/UnityPlayer;"
REQUIRED_APK_ENTRIES = (
    "AndroidManifest.xml",
    "classes.dex",
    "assets/bin/Data/globalgamemanagers",
    "lib/arm64-v8a/libmain.so",
    "lib/arm64-v8a/libunity.so",
    "lib/arm64-v8a/libil2cpp.so",
)
DEX_ENTRY_PATTERN = re.compile(r"classes(?:[2-9]|[1-9][0-9]+)?\.dex\Z")


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


def _archive_entries(
    archive: zipfile.ZipFile,
    package_kind: str,
) -> dict[str, zipfile.ZipInfo]:
    entries: dict[str, zipfile.ZipInfo] = {}
    for info in archive.infolist():
        name = info.filename.replace("\\", "/")
        if name.startswith("/") or ".." in Path(name).parts:
            raise PackageError(f"unsafe {package_kind} entry: {name}")
        if name in entries:
            raise PackageError(f"duplicate {package_kind} entry: {name}")
        entries[name] = info
    return entries


def _validated_entries(archive: zipfile.ZipFile) -> dict[str, zipfile.ZipInfo]:
    entries = _archive_entries(archive, "AAR")
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


def _checked_dex_region(
    data: bytes,
    offset: int,
    count: int,
    item_size: int,
    label: str,
) -> None:
    if offset < 0 or count < 0 or item_size <= 0:
        raise PackageError(f"invalid {label} region in DEX")
    size = count * item_size
    if offset > len(data) or size > len(data) - offset:
        raise PackageError(f"out-of-bounds {label} region in DEX")


def _dex_string(data: bytes, offset: int) -> bytes:
    if offset < 0 or offset >= len(data):
        raise PackageError("out-of-bounds string data in DEX")
    cursor = offset
    for _ in range(5):
        if cursor >= len(data):
            raise PackageError("truncated string length in DEX")
        value = data[cursor]
        cursor += 1
        if value & 0x80 == 0:
            break
    else:
        raise PackageError("oversized string length in DEX")
    end = data.find(b"\0", cursor)
    if end < 0:
        raise PackageError("unterminated string data in DEX")
    return data[cursor:end]


def _dex_declares_class(name: str, data: bytes, descriptor: str) -> bool:
    if len(data) < 112 or data[:4] != b"dex\n" or data[7] != 0:
        raise PackageError(f"{name} is not a supported DEX file")
    file_size, header_size, endian_tag = struct.unpack_from("<III", data, 32)
    if file_size != len(data) or header_size != 112 or endian_tag != 0x12345678:
        raise PackageError(f"{name} has an invalid DEX header")

    string_count, string_offset = struct.unpack_from("<II", data, 56)
    type_count, type_offset = struct.unpack_from("<II", data, 64)
    class_count, class_offset = struct.unpack_from("<II", data, 96)
    _checked_dex_region(data, string_offset, string_count, 4, "string IDs")
    _checked_dex_region(data, type_offset, type_count, 4, "type IDs")
    _checked_dex_region(data, class_offset, class_count, 32, "class definitions")

    expected = descriptor.encode("ascii")
    for index in range(class_count):
        class_index = struct.unpack_from("<I", data, class_offset + (index * 32))[0]
        if class_index >= type_count:
            raise PackageError(f"{name} has an invalid class type index")
        descriptor_index = struct.unpack_from("<I", data, type_offset + (class_index * 4))[0]
        if descriptor_index >= string_count:
            raise PackageError(f"{name} has an invalid descriptor string index")
        descriptor_offset = struct.unpack_from(
            "<I", data, string_offset + (descriptor_index * 4)
        )[0]
        if _dex_string(data, descriptor_offset) == expected:
            return True
    return False


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


def inspect_apk(source: Path, variant: str) -> dict[str, object]:
    if variant not in SUPPORTED_VARIANTS:
        raise PackageError(f"unsupported variant {variant!r}; expected debug or release")
    if not source.is_file():
        raise PackageError(f"APK does not exist: {source}")
    try:
        with zipfile.ZipFile(source) as archive:
            entries = _archive_entries(archive, "APK")
            missing = [name for name in REQUIRED_APK_ENTRIES if name not in entries]
            if missing:
                raise PackageError("missing required APK entries: " + ", ".join(missing))
            empty = [name for name in REQUIRED_APK_ENTRIES if entries[name].file_size <= 0]
            if empty:
                raise PackageError("empty required APK entries: " + ", ".join(empty))

            abi_names = sorted(
                {
                    name.split("/")[1]
                    for name in entries
                    if name.startswith("lib/") and len(name.split("/")) >= 3
                }
            )
            unsupported = [abi for abi in abi_names if abi not in SUPPORTED_ABIS]
            if unsupported:
                raise PackageError("unsupported ABI directories in APK: " + ", ".join(unsupported))
            if abi_names != list(SUPPORTED_ABIS):
                raise PackageError(f"APK ABI set must be {list(SUPPORTED_ABIS)}; found {abi_names}")

            unity_entries = []
            for name in REQUIRED_APK_ENTRIES[2:]:
                data = archive.read(name)
                if name.startswith("lib/"):
                    _verify_elf(name, data)
                unity_entries.append(
                    {"path": name, "bytes": len(data), "sha256": sha256_bytes(data)}
                )

            dex_names = sorted(name for name in entries if DEX_ENTRY_PATTERN.fullmatch(name))
            if not dex_names:
                raise PackageError("APK contains no classes DEX entries")
            declares_unity_player = False
            for name in dex_names:
                if _dex_declares_class(name, archive.read(name), UNITY_PLAYER_DESCRIPTOR):
                    declares_unity_player = True
            if not declares_unity_player:
                raise PackageError(f"APK DEX files do not declare {UNITY_PLAYER_DESCRIPTOR}")

            return {
                "schemaVersion": 1,
                "variant": variant,
                "abis": abi_names,
                "apk": {
                    "path": source.name,
                    "bytes": source.stat().st_size,
                    "sha256": sha256_file(source),
                },
                "unityEntries": unity_entries,
                "dexEntries": dex_names,
                "unityPlayerClass": UNITY_PLAYER_DESCRIPTOR,
            }
    except zipfile.BadZipFile as error:
        raise PackageError(f"invalid APK ZIP: {source}") from error


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


def verify_packaged_apk(
    source: Path,
    artifacts_root: Path,
    variant: str,
    expected_repository_sha: str | None = None,
    expected_unity_version: str | None = None,
) -> dict[str, object]:
    aar_path, inventory_path = verify_staged(
        artifacts_root,
        variant,
        expected_repository_sha,
        expected_unity_version,
    )
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    report = inspect_apk(source, variant)

    staged_entries = {item["path"]: item for item in inventory["requiredEntries"]}
    packaged_entries = {item["path"]: item for item in report["unityEntries"]}
    identity_pairs = {
        "assets/bin/Data/globalgamemanagers": "assets/bin/Data/globalgamemanagers",
        "jni/arm64-v8a/libmain.so": "lib/arm64-v8a/libmain.so",
        "jni/arm64-v8a/libunity.so": "lib/arm64-v8a/libunity.so",
        "jni/arm64-v8a/libil2cpp.so": "lib/arm64-v8a/libil2cpp.so",
    }
    for aar_entry, apk_entry in identity_pairs.items():
        staged = staged_entries[aar_entry]
        packaged = packaged_entries[apk_entry]
        if (
            packaged["bytes"] != staged["bytes"]
            or packaged["sha256"] != staged["sha256"]
        ):
            raise PackageError(
                f"APK entry {apk_entry} does not match staged AAR entry {aar_entry}"
            )

    report["sourceAar"] = {
        **inventory["aar"],
        "repositorySha": inventory["repositorySha"],
        "unityVersion": inventory["unityVersion"],
        "stagedPath": str(aar_path),
    }
    return report


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
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--source-aar", type=Path, help="verify/stage an existing AAR instead of assembling")
    mode.add_argument("--verify-only", action="store_true", help="verify an already staged AAR and inventory")
    mode.add_argument("--verify-apk", type=Path, help="verify a final host APK against its staged AAR")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    args = parser.parse_args(argv)
    try:
        if args.verify_apk:
            report = verify_packaged_apk(
                args.verify_apk.resolve(),
                args.artifacts_dir.resolve(),
                args.variant,
                repository_sha(args.repo_root.resolve()),
                args.unity_version,
            )
            print(f"verified_apk={args.verify_apk.resolve()}")
            print(f"apk_sha256={report['apk']['sha256']}")
            print(f"source_aar_sha256={report['sourceAar']['sha256']}")
            print(f"unity_player_class={report['unityPlayerClass']}")
            return 0
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
