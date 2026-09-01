#!/usr/bin/env python3
"""Generate and validate AnotherLife's approved deterministic scene manifests.

The committed manifests are the reviewed scene-set boundary. Generation is explicit;
normal validation never rewrites them. Any new or removed scene therefore fails closed
until DEC-SCENE-DELIVERY-001 is reopened and the owner approves a replacement set.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Iterable, NamedTuple

SCHEMA_VERSION = 1
DECISION_ID = "DEC-SCENE-DELIVERY-001"
UNITY_ROOT = Path("unity")
ASSETS_ROOT = UNITY_ROOT / "Assets"
CATALOG_PATH = ASSETS_ROOT / "AL/StreamingAssets/GameData/al_world_streaming_catalog.json"
BUILD_SETTINGS_PATH = UNITY_ROOT / "ProjectSettings/EditorBuildSettings.asset"
PROJECT_VERSION_PATH = UNITY_ROOT / "ProjectSettings/ProjectVersion.txt"
PACKAGE_MANIFEST_PATH = UNITY_ROOT / "Packages/manifest.json"
PACKAGES_LOCK_PATH = UNITY_ROOT / "Packages/packages-lock.json"
ADDRESSABLES_ROOT = ASSETS_ROOT / "AddressableAssetsData"
ENABLED_MANIFEST_PATH = ASSETS_ROOT / "AL/StreamingAssets/GameData/al_enabled_scene_manifest.v1.json"
GENERATED_MANIFEST_PATH = ASSETS_ROOT / "AL/StreamingAssets/GameData/al_generated_scene_manifest.v1.json"
GENERATED_ROOT = "Assets/AL/Worlds/Generated/"

REQUIRED_ENABLED_PATHS = (
    "Assets/AL/Scenes/Boot.unity",
    "Assets/AL/Scenes/RealmSelection.unity",
    "Assets/AL/Scenes/CharacterCreation.unity",
    "Assets/AL/Scenes/ChampionArena.unity",
    "Assets/AL/Scenes/Kingdom.unity",
)

DIRECT_CLASSIFICATION = {
    "Assets/AL/Scenes/Boot.unity": ("al_scene_boot", "production_entry", "bootstrap"),
    "Assets/AL/Scenes/RealmSelection.unity": (
        "al_scene_realm_selection",
        "onboarding_selection",
        "onboarding",
    ),
    "Assets/AL/Scenes/CharacterCreation.unity": (
        "al_scene_character_creation",
        "onboarding_creation",
        "onboarding",
    ),
    "Assets/AL/Scenes/ChampionArena.unity": (
        "al_scene_champion_arena",
        "first_session_gameplay",
        "champion_mode",
    ),
    "Assets/AL/Scenes/Kingdom.unity": ("al_scene_kingdom", "production_hub", "kingdom"),
}

GUID_PATTERN = re.compile(rb"\bguid:\s*([0-9a-f]{32})\b")
META_GUID_PATTERN = re.compile(rb"(?m)^guid:\s*([0-9a-f]{32})\s*$")
ADDRESSABLE_ENTRY_PATTERN = re.compile(
    r"m_GUID:\s*([0-9a-f]{32})\s*\r?\n\s*m_Address:\s*([^\r\n]+)"
)


class ManifestError(RuntimeError):
    """A stable fail-closed validation result suitable for CI diagnostics."""

    def __init__(self, code: str, detail: str):
        super().__init__(f"{code}: {detail}")
        self.code = code
        self.detail = detail


class ValidationResult(NamedTuple):
    enabled_count: int
    generated_count: int
    excluded_count: int
    accounted_count: int
    enabled_manifest_sha256: str
    generated_manifest_sha256: str


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _sha256_file(path: Path) -> str:
    try:
        return _sha256_bytes(path.read_bytes())
    except FileNotFoundError as error:
        raise ManifestError("MISSING_REQUIRED_SCENE", str(path)) from error


def _canonical_compact(value: object) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def _canonical_pretty(value: object) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def _asset_path(root: Path, path: Path) -> str:
    return path.relative_to(root / UNITY_ROOT).as_posix()


def _physical_path(root: Path, asset_path: str) -> Path:
    return root / UNITY_ROOT / Path(asset_path)


def discover_scene_paths(root: Path) -> list[str]:
    assets = root / ASSETS_ROOT
    return sorted(
        (_asset_path(root, path) for path in assets.rglob("*.unity")),
        key=str,
    )


def _parse_build_settings(root: Path) -> list[dict]:
    path = root / BUILD_SETTINGS_PATH
    if not path.is_file():
        raise ManifestError("MISSING_BUILD_SETTINGS", str(BUILD_SETTINGS_PATH))
    entries: list[dict] = []
    current: dict | None = None
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        stripped = raw_line.strip()
        if stripped.startswith("- enabled:"):
            if current is not None:
                entries.append(current)
            current = {"enabled": stripped.split(":", 1)[1].strip() == "1"}
        elif current is not None and stripped.startswith("path:"):
            current["path"] = stripped.split(":", 1)[1].strip()
        elif current is not None and stripped.startswith("guid:"):
            current["guid"] = stripped.split(":", 1)[1].strip()
    if current is not None:
        entries.append(current)
    return entries


def _guid_from_meta(meta_path: Path) -> str:
    try:
        match = META_GUID_PATTERN.search(meta_path.read_bytes())
    except FileNotFoundError as error:
        raise ManifestError("MISSING_META", str(meta_path)) from error
    if match is None:
        raise ManifestError("MISSING_META_GUID", str(meta_path))
    return match.group(1).decode("ascii")


def _build_guid_index(root: Path) -> dict[str, str]:
    index: dict[str, str] = {}
    for meta_path in sorted((root / ASSETS_ROOT).rglob("*.meta")):
        match = META_GUID_PATTERN.search(meta_path.read_bytes())
        if match is None:
            continue
        guid = match.group(1).decode("ascii")
        asset = meta_path.with_suffix("")
        asset_path = _asset_path(root, asset)
        previous = index.get(guid)
        if previous is not None and previous != asset_path:
            raise ManifestError(
                "DUPLICATE_IDENTITY",
                f"asset GUID {guid} is shared by {previous} and {asset_path}",
            )
        index[guid] = asset_path
    return index


def _dependency_records(root: Path, scene_path: str, guid_index: dict[str, str]) -> list[dict]:
    scene_bytes = _physical_path(root, scene_path).read_bytes()
    scene_guid = _guid_from_meta(_physical_path(root, scene_path + ".meta"))
    dependency_guids = sorted(
        {
            match.decode("ascii")
            for match in GUID_PATTERN.findall(scene_bytes)
            if match != b"0" * 32 and match.decode("ascii") != scene_guid
        }
    )
    records: list[dict] = []
    for guid in dependency_guids:
        dependency_path = guid_index.get(guid)
        if dependency_path is None:
            scheme = "unity-builtin" if guid.startswith("0000000000000000") else "unity-external"
            records.append(
                {
                    "assetPath": f"{scheme}://{guid}",
                    "guid": guid,
                    "sha256": "resolved_by_pinned_unity_and_package_lock",
                }
            )
            continue
        physical = _physical_path(root, dependency_path)
        if not physical.is_file():
            raise ManifestError(
                "UNRESOLVED_DEPENDENCY",
                f"{scene_path} dependency is not a file: {dependency_path}",
            )
        records.append(
            {
                "assetPath": dependency_path,
                "guid": guid,
                "sha256": _sha256_file(physical),
            }
        )
    return records


def _file_identity(root: Path, scene_path: str, guid_index: dict[str, str]) -> dict:
    physical = _physical_path(root, scene_path)
    meta = _physical_path(root, scene_path + ".meta")
    if not physical.is_file():
        raise ManifestError("MISSING_REQUIRED_SCENE", scene_path)
    dependencies = _dependency_records(root, scene_path, guid_index)
    return {
        "assetPath": scene_path,
        "dependencies": dependencies,
        "dependencyProjectionSha256": _sha256_bytes(_canonical_compact(dependencies)),
        "guid": _guid_from_meta(meta),
        "metaSha256": _sha256_file(meta),
        "sceneName": Path(scene_path).stem,
        "sceneSha256": _sha256_file(physical),
    }


def _excluded_classification(scene_path: str) -> tuple[str, str, str]:
    if scene_path == "Assets/Test.unity":
        return "al_scene_test_representative", "representative_test_only", "qa"
    if scene_path == "Assets/AL/Scenes/InnerRealmWorld.unity":
        return "al_scene_inner_realm_world_legacy", "legacy_monolithic_world", "world_streaming"
    if scene_path.startswith("Assets/AL/Scenes/Prototype/Terrestrials/"):
        return (
            "excluded_" + Path(scene_path).stem.lower(),
            "terrestrial_representative_prototype",
            "terrestrials",
        )
    if scene_path.startswith("Assets/AL/Scenes/Prototypes/"):
        stem = re.sub(r"(?<!^)(?=[A-Z])", "_", Path(scene_path).stem).lower()
        return "excluded_" + stem, "architecture_review_prototype", "architecture"
    raise ManifestError(
        "SCENE_SET_REVIEW_REQUIRED",
        f"unclassified non-generated scene requires owner review: {scene_path}",
    )


def _with_fingerprint(payload: dict, record_key: str) -> dict:
    payload["contentFingerprintSha256"] = _sha256_bytes(
        _canonical_compact(payload[record_key])
    )
    return payload


def _delivery_dependency_identity(root: Path) -> dict:
    project_version = (root / PROJECT_VERSION_PATH).read_text(encoding="utf-8")
    match = re.search(r"(?m)^m_EditorVersion:\s*(\S+)\s*$", project_version)
    unity_version = match.group(1) if match else ""
    package_manifest = json.loads(
        (root / PACKAGE_MANIFEST_PATH).read_text(encoding="utf-8")
    )
    package_lock = json.loads((root / PACKAGES_LOCK_PATH).read_text(encoding="utf-8"))
    addressables_version = package_manifest.get("dependencies", {}).get(
        "com.unity.addressables"
    )
    locked_version = package_lock.get("dependencies", {}).get(
        "com.unity.addressables", {}
    ).get("version")
    if (
        unity_version != "6000.3.22f1"
        or addressables_version != "2.9.1"
        or locked_version != "2.9.1"
    ):
        raise ManifestError(
            "DELIVERY_DEPENDENCY_DRIFT",
            "requires Unity 6000.3.22f1 and com.unity.addressables 2.9.1 in manifest and lock",
        )
    return {
        "addressablesVersion": addressables_version,
        "packagesLockSha256": _sha256_file(root / PACKAGES_LOCK_PATH),
        "unityVersion": unity_version,
    }


def generate_manifests(root: Path) -> tuple[dict, dict]:
    root = root.resolve()
    delivery_dependencies = _delivery_dependency_identity(root)
    guid_index = _build_guid_index(root)
    scene_paths = discover_scene_paths(root)
    build_entries = _parse_build_settings(root)
    if len(build_entries) != len(REQUIRED_ENABLED_PATHS):
        raise ManifestError(
            "UNEXPECTED_ENABLED_SCENE",
            f"Build Settings contains {len(build_entries)} entries; expected 5",
        )
    actual_enabled_paths = tuple(entry.get("path", "") for entry in build_entries)
    if actual_enabled_paths != REQUIRED_ENABLED_PATHS or not all(
        entry.get("enabled", False) for entry in build_entries
    ):
        missing = sorted(set(REQUIRED_ENABLED_PATHS) - set(actual_enabled_paths))
        if missing:
            raise ManifestError("MISSING_REQUIRED_SCENE", ", ".join(missing))
        raise ManifestError(
            "UNEXPECTED_ENABLED_SCENE",
            "Build Settings must contain the approved five enabled scenes in exact order",
        )

    enabled_records: list[dict] = []
    for build_index, scene_path in enumerate(REQUIRED_ENABLED_PATHS):
        scene_id, purpose, owner = DIRECT_CLASSIFICATION[scene_path]
        record = _file_identity(root, scene_path, guid_index)
        if record["guid"] != build_entries[build_index].get("guid"):
            raise ManifestError("DUPLICATE_IDENTITY", f"Build Settings GUID drift: {scene_path}")
        record.update(
            {
                "buildIndex": build_index,
                "ownership": {"domain": owner},
                "purpose": purpose,
                "reachability": {
                    "entry": build_index == 0,
                    "mode": "direct_build_scene_transition",
                },
                "sceneId": scene_id,
                "shippingStatus": "direct_build",
            }
        )
        enabled_records.append(record)

    catalog_bytes = (root / CATALOG_PATH).read_bytes()
    catalog = json.loads(catalog_bytes.decode("utf-8"))
    generated_records: list[dict] = []
    catalog_paths: set[str] = set()
    chunk_ids: set[str] = set()
    for dimension in catalog.get("dimensions", []):
        dimension_id = dimension["id"]
        for world in dimension.get("worlds", []):
            world_id = world["id"]
            for chunk in world.get("chunks", []):
                scene_path = chunk["scenePath"]
                chunk_id = chunk["id"]
                if scene_path in catalog_paths or chunk_id in chunk_ids:
                    raise ManifestError(
                        "DUPLICATE_IDENTITY", f"catalog duplicate: {chunk_id} / {scene_path}"
                    )
                catalog_paths.add(scene_path)
                chunk_ids.add(chunk_id)
                record = _file_identity(root, scene_path, guid_index)
                discrepancy = "none"
                if world["usage"] == "dragon_cave" and world["accessPolicy"] != "public_unrestricted":
                    discrepancy = "approved_public_unrestricted_intent_differs_from_catalog"
                record.update(
                    {
                        "addressables": {
                            "address": f"scene/{world_id}/{chunk_id}",
                            "buildPath": "LocalBuildPath",
                            "group": f"AL.World.{world_id}",
                            "loadPath": "LocalLoadPath",
                            "remoteDelivery": False,
                        },
                        "chunkId": chunk_id,
                        "ownership": {
                            "dimensionId": dimension_id,
                            "sourceAccessPolicy": world["accessPolicy"],
                            "usage": world["usage"],
                            "variantBindingStatus": world["variantBindingStatus"],
                            "worldId": world_id,
                        },
                        "policyDiscrepancy": discrepancy,
                        "purpose": chunk["blockoutArchetype"],
                        "reachability": {
                            "isWorldSeed": chunk_id == world["seedChunkId"],
                            "mode": "catalog_seed_or_same_world_additive_neighbor",
                            "neighborChunkIds": sorted(chunk.get("neighbors", [])),
                        },
                        "shippingStatus": "local_addressable",
                    }
                )
                generated_records.append(record)
    generated_records.sort(key=lambda record: record["chunkId"])

    actual_generated_paths = {
        path for path in scene_paths if path.startswith(GENERATED_ROOT) and "/Previews/" not in path
    }
    if catalog_paths != actual_generated_paths:
        missing = sorted(catalog_paths - actual_generated_paths)
        extra = sorted(actual_generated_paths - catalog_paths)
        raise ManifestError(
            "SCENE_SET_REVIEW_REQUIRED",
            f"generated scene/catalog drift; missing={missing}, extra={extra}",
        )
    if len(generated_records) != 78:
        raise ManifestError(
            "MISSING_REQUIRED_SCENE",
            f"catalog contains {len(generated_records)} generated scenes; expected 78",
        )

    non_generated_paths = sorted(set(scene_paths) - catalog_paths)
    excluded_paths = sorted(set(non_generated_paths) - set(REQUIRED_ENABLED_PATHS))
    excluded_records: list[dict] = []
    for scene_path in excluded_paths:
        scene_id, purpose, owner = _excluded_classification(scene_path)
        record = _file_identity(root, scene_path, guid_index)
        record.update(
            {
                "ownership": {"domain": owner},
                "purpose": purpose,
                "reachability": {"entry": False, "mode": "not_runtime_reachable"},
                "sceneId": scene_id,
                "shippingStatus": "non_shipping",
            }
        )
        excluded_records.append(record)

    enabled_manifest = _with_fingerprint(
        {
            "decisionId": DECISION_ID,
            "deliveryDependencies": delivery_dependencies,
            "deliveryStrategy": {
                "contentOnlyUpdatesEnabled": False,
                "generatedScenes": "local_addressables_by_world",
                "remoteCatalogsEnabled": False,
                "shellScenes": "direct_build_inclusion",
            },
            "enabledScenes": enabled_records,
            "excludedScenes": excluded_records,
            "knownSceneCount": len(scene_paths),
            "schemaVersion": SCHEMA_VERSION,
        },
        "enabledScenes",
    )
    enabled_manifest["accountingFingerprintSha256"] = _sha256_bytes(
        _canonical_compact(enabled_records + excluded_records)
    )

    generated_manifest = _with_fingerprint(
        {
            "catalogSha256": _sha256_bytes(catalog_bytes),
            "catalogVersion": catalog["version"],
            "decisionId": DECISION_ID,
            "deliveryDependencies": delivery_dependencies,
            "generatedScenes": generated_records,
            "grouping": "one_local_addressables_group_per_world_instance",
            "localAddressablesVersion": "2.9.1",
            "remoteCatalogsEnabled": False,
            "schemaVersion": SCHEMA_VERSION,
        },
        "generatedScenes",
    )
    return enabled_manifest, generated_manifest


def render_manifests(enabled: dict, generated: dict) -> dict[Path, bytes]:
    return {
        ENABLED_MANIFEST_PATH: _canonical_pretty(enabled),
        GENERATED_MANIFEST_PATH: _canonical_pretty(generated),
    }


def _assert_unique(records: Iterable[dict], fields: Iterable[str]) -> None:
    records = list(records)
    for field in fields:
        values = [record.get(field) for record in records]
        if len(values) != len(set(values)):
            raise ManifestError("DUPLICATE_IDENTITY", f"duplicate {field}")


def validate_scene_accounting(
    enabled: dict, generated: dict, actual_scene_paths: Iterable[str]
) -> None:
    records = (
        enabled.get("enabledScenes", [])
        + enabled.get("excludedScenes", [])
        + generated.get("generatedScenes", [])
    )
    accounted = [record.get("assetPath", "") for record in records]
    actual = list(actual_scene_paths)
    if set(accounted) != set(actual) or len(accounted) != len(actual):
        raise ManifestError(
            "SCENE_SET_REVIEW_REQUIRED",
            "scene inventory differs from the owner-approved 103-scene set",
        )


def validate_addressables_configuration(generated: dict, root: Path) -> None:
    settings_path = root / ADDRESSABLES_ROOT / "AddressableAssetSettings.asset"
    groups_path = root / ADDRESSABLES_ROOT / "AssetGroups"
    if not settings_path.is_file() or not groups_path.is_dir():
        raise ManifestError(
            "ADDRESSABLE_CONFIGURATION_DRIFT", "Addressables settings/groups are missing"
        )
    settings_text = settings_path.read_text(encoding="utf-8")
    if (
        "m_BuildRemoteCatalog: 0" not in settings_text
        or "m_DisableCatalogUpdateOnStart: 1" not in settings_text
    ):
        raise ManifestError(
            "ADDRESSABLE_CONFIGURATION_DRIFT",
            "remote catalogs or runtime catalog updates are enabled",
        )

    expected_records = generated.get("generatedScenes", [])
    expected_groups = {
        record["addressables"]["group"] for record in expected_records
    }
    expected_entries = {
        (
            record["guid"],
            record["addressables"]["group"],
            record["addressables"]["address"],
        )
        for record in expected_records
    }
    actual_groups: set[str] = set()
    actual_entries: set[tuple[str, str, str]] = set()
    group_files = sorted(groups_path.glob("AL.World.*.asset"))
    for group_path in group_files:
        text = group_path.read_text(encoding="utf-8")
        name_match = re.search(r"(?m)^\s*m_Name:\s*(AL\.World\.\S+)\s*$", text)
        if name_match is None:
            raise ManifestError(
                "ADDRESSABLE_CONFIGURATION_DRIFT", f"group name missing: {group_path}"
            )
        group_name = name_match.group(1)
        actual_groups.add(group_name)
        for guid, address in ADDRESSABLE_ENTRY_PATTERN.findall(text):
            actual_entries.add((guid, group_name, address.strip()))

    content_update_schemas = list(
        (groups_path / "Schemas").glob("*ContentUpdateGroupSchema.asset")
    )
    if (
        actual_groups != expected_groups
        or actual_entries != expected_entries
        or len(group_files) != 11
        or len(actual_entries) != 78
        or content_update_schemas
    ):
        raise ManifestError(
            "ADDRESSABLE_CONFIGURATION_DRIFT",
            "local group membership, address, label policy, or content-update schema differs from the manifest",
        )


def validate_manifest_payloads(enabled: dict, generated: dict, root: Path) -> None:
    enabled_records = enabled.get("enabledScenes", [])
    generated_records = generated.get("generatedScenes", [])
    excluded_records = enabled.get("excludedScenes", [])

    enabled_paths = [record.get("assetPath") for record in enabled_records]
    missing = [path for path in REQUIRED_ENABLED_PATHS if path not in enabled_paths]
    if missing:
        raise ManifestError("MISSING_REQUIRED_SCENE", ", ".join(missing))
    if enabled_paths != list(REQUIRED_ENABLED_PATHS) or len(enabled_records) != 5:
        raise ManifestError(
            "UNEXPECTED_ENABLED_SCENE", "approved direct scene list/order changed"
        )
    if [record.get("buildIndex") for record in enabled_records] != list(range(5)):
        raise ManifestError("NONDETERMINISTIC_ORDER", "direct scene build indexes changed")
    if [record.get("chunkId") for record in generated_records] != sorted(
        record.get("chunkId") for record in generated_records
    ):
        raise ManifestError("NONDETERMINISTIC_ORDER", "generated scenes are not ordinal by chunkId")

    all_records = enabled_records + excluded_records + generated_records
    _assert_unique(all_records, ("assetPath", "guid"))
    _assert_unique(enabled_records + excluded_records, ("sceneId",))
    _assert_unique(generated_records, ("chunkId",))
    addresses = [record.get("addressables", {}).get("address") for record in generated_records]
    if len(addresses) != len(set(addresses)):
        raise ManifestError("DUPLICATE_IDENTITY", "duplicate Addressables address")

    validate_scene_accounting(enabled, generated, discover_scene_paths(root))
    expected_enabled, expected_generated = generate_manifests(root)
    if enabled != expected_enabled or generated != expected_generated:
        raise ManifestError(
            "HASH_DRIFT",
            "manifest identities, dependencies, ordering, classifications, or hashes differ from current inputs",
        )


def validate_repository(root: Path) -> ValidationResult:
    root = root.resolve()
    enabled_path = root / ENABLED_MANIFEST_PATH
    generated_path = root / GENERATED_MANIFEST_PATH
    if not enabled_path.is_file() or not generated_path.is_file():
        raise ManifestError(
            "MANIFEST_MISSING",
            "run scene_content_manifest.py --generate only after owner-approved scene-set review",
        )
    enabled_bytes = enabled_path.read_bytes()
    generated_bytes = generated_path.read_bytes()
    try:
        enabled = json.loads(enabled_bytes.decode("utf-8"))
        generated = json.loads(generated_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ManifestError("MANIFEST_INVALID", str(error)) from error
    if enabled_bytes != _canonical_pretty(enabled) or generated_bytes != _canonical_pretty(generated):
        raise ManifestError("NONDETERMINISTIC_ORDER", "manifests are not canonical UTF-8/LF JSON")
    validate_manifest_payloads(enabled, generated, root)
    validate_addressables_configuration(generated, root)
    return ValidationResult(
        enabled_count=len(enabled["enabledScenes"]),
        generated_count=len(generated["generatedScenes"]),
        excluded_count=len(enabled["excludedScenes"]),
        accounted_count=enabled["knownSceneCount"],
        enabled_manifest_sha256=_sha256_bytes(enabled_bytes),
        generated_manifest_sha256=_sha256_bytes(generated_bytes),
    )


def write_manifests(root: Path) -> ValidationResult:
    enabled, generated = generate_manifests(root)
    for relative_path, data in render_manifests(enabled, generated).items():
        path = root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    return validate_repository(root)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--generate", action="store_true")
    mode.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    try:
        result = write_manifests(args.root) if args.generate else validate_repository(args.root)
    except ManifestError as error:
        print(str(error), file=sys.stderr)
        return 1
    print(
        "SCENE_MANIFEST_VALID "
        f"enabled={result.enabled_count} generated={result.generated_count} "
        f"excluded={result.excluded_count} accounted={result.accounted_count} "
        f"enabled_sha256={result.enabled_manifest_sha256} "
        f"generated_sha256={result.generated_manifest_sha256}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
