#!/usr/bin/env python3
"""Run and verify fail-closed realm-slice evidence captures."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import re
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any, Callable


DEFAULT_POLICY = Path(__file__).with_name("realm_slice_evidence_policy.v1.json")
DEFAULT_SCENARIOS = Path(__file__).with_name("realm_slice_scenarios.v1.json")
DEFAULT_SCHEMA = Path(__file__).parents[2] / "unity/SharedContracts/realm-slice-evidence-manifest.schema.json"
CANONICAL_POLICY_SHA256 = "29548f74d1b2dcb64dc82250043c0933cb900203413dea77992abfec6fb1fce2"
REQUIRED_QA_CONTRACTS = (
    "unit", "integration", "play-mode", "build-smoke", "scene-manifest",
    "content-manifest", "save-round-trip", "save-migration",
    "save-downgrade-rejection", "save-corruption-recovery",
    "save-crash-recovery", "packaged-narrative",
)
SOURCE_PATHS = {
    "enabled": "unity/Assets/AL/StreamingAssets/GameData/al_enabled_scene_manifest.v1.json",
    "generated": "unity/Assets/AL/StreamingAssets/GameData/al_generated_scene_manifest.v1.json",
    "world": "unity/Assets/AL/StreamingAssets/GameData/al_world_streaming_catalog.json",
    "narrative": "unity/Assets/AL/StreamingAssets/GameData/al_main_quest_line_runtime.v1.json",
    "save": "unity/Assets/AL/Tests/EditMode/Fixtures/SaveSchema1/manifest.json",
}


class RealmSliceEvidenceError(RuntimeError):
    """A harness configuration, execution, or evidence-integrity failure."""


def is_missing_value(value: Any) -> bool:
    if value is None:
        return True
    if isinstance(value, str):
        normalized = value.strip().casefold()
        return (
            not normalized
            or normalized in {"tbd", "unknown", "unset", "n/a", "na"}
            or normalized.startswith("replace-")
        )
    return False


def _nested_value(payload: dict[str, Any], dotted_path: str) -> Any:
    value: Any = payload
    for part in dotted_path.split("."):
        if not isinstance(value, dict):
            return None
        value = value.get(part)
    return value


def metric_satisfies_pass(
    policy: dict[str, Any], envelope: dict[str, Any], name: str, value: Any
) -> bool:
    semantics = policy["metricSemantics"]
    if is_missing_value(value):
        return False
    if name in semantics["falseOnPass"]:
        return value is False
    if name in semantics["zeroOnPass"]:
        return not isinstance(value, bool) and isinstance(value, (int, float)) and value == 0
    if name in semantics["positiveOnPass"]:
        return not isinstance(value, bool) and isinstance(value, (int, float)) and value > 0
    if name in semantics["rangeOnPass"]:
        bounds = semantics["rangeOnPass"][name]
        return (
            not isinstance(value, bool)
            and isinstance(value, (int, float))
            and math.isfinite(value)
            and value > bounds["minimumExclusive"]
            and value <= bounds["maximum"]
        )
    if name in semantics["envelopeMatches"]:
        return value == _nested_value(envelope, semantics["envelopeMatches"][name])
    if name in semantics["minimumOnPass"]:
        return (
            not isinstance(value, bool)
            and isinstance(value, (int, float))
            and math.isfinite(value)
            and value >= semantics["minimumOnPass"][name]
        )
    typed_non_empty = semantics["nonEmptyOnPass"]
    if name in typed_non_empty["nonEmptyStringArray"]:
        return (
            isinstance(value, list)
            and bool(value)
            and all(isinstance(item, str) and not is_missing_value(item) for item in value)
        )
    if name in typed_non_empty["positiveNumberMap"]:
        return (
            isinstance(value, dict)
            and bool(value)
            and all(
                isinstance(key, str)
                and not is_missing_value(key)
                and not isinstance(item, bool)
                and isinstance(item, (int, float))
                and math.isfinite(item)
                and item > 0
                for key, item in value.items()
            )
        )
    if name in typed_non_empty["nonNegativeInteger"]:
        return not isinstance(value, bool) and isinstance(value, int) and value >= 0
    if name in typed_non_empty["sha256"]:
        return isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) is not None
    if name in typed_non_empty["nonEmptyString"]:
        return isinstance(value, str) and not is_missing_value(value)
    if name.endswith(tuple(semantics["trueSuffixes"])):
        return value is True
    if name.endswith(tuple(semantics["positiveSuffixes"])):
        return (
            not isinstance(value, bool)
            and isinstance(value, (int, float))
            and math.isfinite(value)
            and value > 0
        )
    if name.endswith(semantics["resultSuffix"]):
        return value is True or value == "PASS"
    if name.endswith("Count"):
        return not isinstance(value, bool) and isinstance(value, int) and value >= 0
    return False


def invalid_pass_metrics(
    policy: dict[str, Any],
    envelope: dict[str, Any],
    check: dict[str, Any],
    metrics: dict[str, Any],
) -> list[str]:
    invalid = {
        name for name in check["metrics"]
        if name not in metrics or not metric_satisfies_pass(policy, envelope, name, metrics.get(name))
    }
    for left, right in policy["metricSemantics"]["equalPairsOnPass"]:
        if left in check["metrics"] and right in check["metrics"] and metrics.get(left) != metrics.get(right):
            invalid.update((left, right))
    return sorted(invalid)


def invalid_bound_metrics(
    policy: dict[str, Any],
    envelope: dict[str, Any],
    check: dict[str, Any],
    metrics: dict[str, Any],
) -> list[str]:
    """Validate expected/identity metrics that remain invariant even on a failed run."""
    semantics = policy["metricSemantics"]
    invalid = {
        name for name, dotted_path in semantics["envelopeMatches"].items()
        if name in check["metrics"] and metrics.get(name) != _nested_value(envelope, dotted_path)
    }
    expected_metrics = envelope.get("scenarioExpectedMetrics", {})
    if isinstance(expected_metrics, dict):
        invalid.update(
            name for name, expected in expected_metrics.items()
            if name in check["metrics"] and metrics.get(name) != expected
        )
    return sorted(invalid)


def canonical_json(payload: Any) -> bytes:
    return (json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path: Path, *, normalize_lf: bool = False) -> str:
    payload = Path(path).read_bytes()
    if normalize_lf:
        payload = payload.replace(b"\r\n", b"\n")
    return sha256_bytes(payload)


def digest_document(payload: dict[str, Any], digest_field: str) -> str:
    unsigned = copy.deepcopy(payload)
    unsigned.pop(digest_field, None)
    return sha256_bytes(canonical_json(unsigned))


def validate_manifest_schema(manifest: dict[str, Any], schema_path: Path = DEFAULT_SCHEMA) -> None:
    try:
        from jsonschema import Draft202012Validator, FormatChecker
    except ImportError as error:
        raise RealmSliceEvidenceError("RSQ_SCHEMA_DEPENDENCY: install jsonschema") from error
    schema = _load_json(schema_path, "RSQ_MANIFEST_SCHEMA")
    Draft202012Validator.check_schema(schema)
    errors = sorted(
        Draft202012Validator(schema, format_checker=FormatChecker()).iter_errors(manifest),
        key=lambda item: list(item.absolute_path),
    )
    if errors:
        path = ".".join(str(value) for value in errors[0].absolute_path) or "$"
        raise RealmSliceEvidenceError(f"RSQ_MANIFEST_SCHEMA: {path}: {errors[0].message}")


def _load_json(path: Path, code: str) -> dict[str, Any]:
    def reject_non_finite(value: str) -> None:
        raise ValueError(f"non-finite JSON number {value}")

    try:
        payload = json.loads(
            Path(path).read_text(encoding="utf-8"), parse_constant=reject_non_finite
        )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
        raise RealmSliceEvidenceError(f"{code}: {path}: {error}") from error
    if not isinstance(payload, dict):
        raise RealmSliceEvidenceError(f"{code}: JSON object required: {path}")
    return payload


def _verify_document_digest(payload: dict[str, Any], field: str, code: str) -> None:
    recorded = payload.get(field)
    if not isinstance(recorded, str) or recorded != digest_document(payload, field):
        raise RealmSliceEvidenceError(f"{code}: {field} is missing or invalid")


def load_source_evidence(
    repo_root: Path,
    build_manifest_path: Path,
    qa_report_path: Path,
    platform_metadata_path: Path,
    save_fixture_id: str,
) -> dict[str, Any]:
    """Validate and bind build, QA, catalogs, save fixture, platform, and Player."""
    repo_root = Path(repo_root).resolve()
    build = _load_json(build_manifest_path, "RSQ_BUILD_MANIFEST")
    _verify_document_digest(build, "manifestSha256", "RSQ_BUILD_MANIFEST")
    if build.get("status") != "succeeded" or build.get("target") != "windows64-development":
        raise RealmSliceEvidenceError("RSQ_BUILD_STATUS: successful Windows build required")
    source = build.get("source")
    artifacts = build.get("artifacts")
    if not isinstance(source, dict) or not isinstance(artifacts, dict):
        raise RealmSliceEvidenceError("RSQ_BUILD_METADATA: source/artifact metadata missing")
    if source.get("trackedInputsDirty") is not False:
        raise RealmSliceEvidenceError("RSQ_BUILD_DIRTY: clean tracked build inputs required")
    source_revision = source.get("sourceRevision")
    if not isinstance(source_revision, str) or not re.fullmatch(r"[0-9a-f]{40}", source_revision):
        raise RealmSliceEvidenceError("RSQ_BUILD_METADATA: source revision invalid")
    artifact_tree = artifacts.get("reproducibleTreeSha256")
    if not isinstance(artifact_tree, str) or not re.fullmatch(r"[0-9a-f]{64}", artifact_tree):
        raise RealmSliceEvidenceError("RSQ_BUILD_METADATA: artifact tree identity invalid")
    if artifacts.get("smoke", {}).get("status") != "passed":
        raise RealmSliceEvidenceError("RSQ_BUILD_SMOKE: structural smoke did not pass")
    artifact_root = Path(str(artifacts.get("root") or "")).resolve()
    file_rows = artifacts.get("files")
    if not isinstance(file_rows, list):
        raise RealmSliceEvidenceError("RSQ_BUILD_ARTIFACT: file inventory missing")
    actual_paths = sorted(
        (path for path in artifact_root.rglob("*") if path.is_file()),
        key=lambda path: path.relative_to(artifact_root).as_posix().encode("utf-8"),
    )
    if not actual_paths or any(path.is_symlink() for path in actual_paths):
        raise RealmSliceEvidenceError("RSQ_BUILD_ARTIFACT: artifact root is empty or symlinked")
    actual_rows: list[dict[str, Any]] = []
    tree = bytearray()
    for path in actual_paths:
        relative = path.relative_to(artifact_root).as_posix()
        size = path.stat().st_size
        digest = sha256_file(path)
        actual_rows.append({"path": relative, "bytes": size, "sha256": digest})
        tree.extend(f"{relative}\0{size}\0{digest}\n".encode("utf-8"))
    recorded_rows = [
        {"path": row.get("path"), "bytes": row.get("bytes"), "sha256": row.get("sha256")}
        for row in file_rows if isinstance(row, dict)
    ]
    if (
        recorded_rows != actual_rows
        or artifacts.get("treeSha256") != sha256_bytes(bytes(tree))
        or artifacts.get("fileCount", len(actual_rows)) != len(actual_rows)
        or artifacts.get("totalBytes", sum(row["bytes"] for row in actual_rows))
        != sum(row["bytes"] for row in actual_rows)
    ):
        raise RealmSliceEvidenceError("RSQ_BUILD_ARTIFACT: artifact inventory or tree digest mismatch")
    player_entry = next((row for row in actual_rows if row["path"] == "AnotherLifeUnity.exe"), None)
    player = artifact_root / "AnotherLifeUnity.exe"
    if (
        not isinstance(player_entry, dict)
        or not player.is_file()
        or player.is_symlink()
        or player.read_bytes()[:2] != b"MZ"
        or player.stat().st_size < 64
        or not (artifact_root / "AnotherLifeUnity_Data/globalgamemanagers").is_file()
    ):
        raise RealmSliceEvidenceError("RSQ_PLAYER_IDENTITY: packaged Player is missing or mismatched")

    report = _load_json(qa_report_path, "RSQ_QA_REPORT")
    _verify_document_digest(report, "reportSha256", "RSQ_QA_REPORT")
    provenance = report.get("provenance")
    contracts = report.get("contracts")
    if (
        report.get("profile") != "full"
        or report.get("status") != "passed"
        or not isinstance(provenance, dict)
        or provenance.get("sourceDirty") is not False
    ):
        raise RealmSliceEvidenceError("RSQ_QA_STATUS: clean passing full QA required")
    if (
        not isinstance(contracts, list)
        or [row.get("id") for row in contracts if isinstance(row, dict)] != list(REQUIRED_QA_CONTRACTS)
        or any(row.get("status") != "passed" for row in contracts)
    ):
        raise RealmSliceEvidenceError("RSQ_QA_COVERAGE: all 12 deterministic QA contracts must pass")
    if (
        provenance.get("sourceRevision") != source_revision
        or provenance.get("build", {}).get("manifestSha256") != build["manifestSha256"]
        or provenance.get("build", {}).get("artifactTreeSha256") != artifact_tree
    ):
        raise RealmSliceEvidenceError("RSQ_MIXED_BUILD: QA and build identities differ")

    required_paths = {name: repo_root / relative for name, relative in SOURCE_PATHS.items()}
    if any(not path.is_file() for path in required_paths.values()):
        missing = sorted(name for name, path in required_paths.items() if not path.is_file())
        raise RealmSliceEvidenceError(f"RSQ_CATALOG_MISSING: {missing}")
    expected_catalogs = {
        "enabledSceneManifestSha256": sha256_file(required_paths["enabled"]),
        "sceneCatalogSha256": sha256_file(required_paths["generated"]),
        "contentCatalogSha256": sha256_file(required_paths["world"]),
        "narrativeCatalogSha256": sha256_file(required_paths["narrative"], normalize_lf=True),
    }
    actual_catalogs = {
        "enabledSceneManifestSha256": provenance.get("scene", {}).get("enabledManifestSha256"),
        "sceneCatalogSha256": provenance.get("scene", {}).get("generatedManifestSha256"),
        "contentCatalogSha256": provenance.get("content", {}).get("worldCatalogSha256"),
        "narrativeCatalogSha256": provenance.get("content", {}).get("narrativeCatalogSha256"),
    }
    if expected_catalogs != actual_catalogs:
        raise RealmSliceEvidenceError("RSQ_CATALOG_IDENTITY: QA/catalog hashes differ")

    save_manifest_path = required_paths["save"]
    if provenance.get("save", {}).get("fixtureManifestSha256") != sha256_file(save_manifest_path):
        raise RealmSliceEvidenceError("RSQ_SAVE_FIXTURE_MANIFEST: QA/save manifest hashes differ")
    save_manifest = _load_json(save_manifest_path, "RSQ_SAVE_FIXTURE_MANIFEST")
    fixture = next(
        (row for row in save_manifest.get("fixtures", []) if row.get("id") == save_fixture_id),
        None,
    )
    if not isinstance(fixture, dict):
        raise RealmSliceEvidenceError(f"RSQ_SAVE_FIXTURE_ID: unknown fixture {save_fixture_id}")
    fixture_file = str(fixture.get("file") or "")
    if not fixture_file or PurePosixPath(fixture_file).name != fixture_file:
        raise RealmSliceEvidenceError("RSQ_SAVE_FIXTURE_PATH: fixture path must remain in its directory")
    fixture_path = save_manifest_path.parent / fixture_file
    if not fixture_path.is_file() or sha256_file(fixture_path) != fixture.get("sha256"):
        raise RealmSliceEvidenceError("RSQ_SAVE_FIXTURE_HASH: fixture is missing or mismatched")
    save_evidence = {
        "id": fixture["id"],
        "sha256": fixture["sha256"],
        "fixtureManifestSha256": sha256_file(save_manifest_path),
        "formatId": save_manifest.get("saveFormatId"),
        "sourceSchemaVersion": fixture.get("sourceSchemaVersion"),
        "expectedSchemaVersion": fixture.get("expectedSchemaVersion"),
        "schemaDisposition": fixture.get("expectedLoadStatus"),
    }

    platform_metadata = _load_json(platform_metadata_path, "RSQ_PLATFORM_METADATA")
    required_platform = {
        "platform", "deviceId", "osVersion", "graphicsApi", "qualityPreset", "viewport",
        "renderScale", "refreshRate", "thresholdSetId", "captureTool", "captureToolVersion",
    }
    if required_platform - set(platform_metadata):
        raise RealmSliceEvidenceError(
            f"RSQ_PLATFORM_METADATA: missing {sorted(required_platform - set(platform_metadata))}"
        )
    viewport = platform_metadata.get("viewport")
    identity_fields = (
        "deviceId", "osVersion", "graphicsApi", "qualityPreset", "thresholdSetId",
        "captureTool", "captureToolVersion",
    )
    invalid_identity = any(is_missing_value(platform_metadata.get(field)) for field in identity_fields)
    if (
        platform_metadata.get("platform") != "WindowsPlayer"
        or invalid_identity
        or not isinstance(viewport, dict)
        or not isinstance(viewport.get("width"), int)
        or not isinstance(viewport.get("height"), int)
        or viewport["width"] <= 0
        or viewport["height"] <= 0
        or not isinstance(platform_metadata.get("renderScale"), (int, float))
        or platform_metadata["renderScale"] <= 0
        or not isinstance(platform_metadata.get("refreshRate"), (int, float))
        or platform_metadata["refreshRate"] <= 0
    ):
        raise RealmSliceEvidenceError("RSQ_PLATFORM_METADATA: values are incomplete or unsupported")

    return {
        "build": {
            "buildId": "build-" + build["manifestSha256"][:20],
            "sourceRevision": source_revision,
            "sourceTreeSha256": source.get("sourceTreeSha256"),
            "manifestSha256": build["manifestSha256"],
            "artifactTreeSha256": artifact_tree,
            "target": build["target"],
        },
        "catalogs": expected_catalogs,
        "saveFixture": save_evidence,
        "platform": platform_metadata,
        "qa": {
            "runId": report.get("run", {}).get("id"),
            "reportSha256": report["reportSha256"],
            "profile": report["profile"],
            "status": report["status"],
        },
        "player": player,
    }


def derive_run_identity(envelope: dict[str, Any]) -> str:
    """Bind a rerun to every immutable trace dimension in its envelope."""
    return "rsq-run-" + sha256_bytes(canonical_json(envelope))[:24]


def validate_envelope(policy: dict[str, Any], envelope: dict[str, Any]) -> None:
    required = {
        "protocolId", "candidateId", "evidencePacketId", "realm", "realmOrdinal",
        "mode", "modeNamespace", "checkId", "scenarioId", "scenarioVersion",
        "sourceRevision", "buildId", "buildManifestSha256", "artifactTreeSha256",
        "locale", "inputClass", "accessibilityPreset", "platform", "deviceId",
        "fixtureVersion", "saveFixtureId", "saveFixtureSha256", "scenarioCatalogSha256",
        "scenarioDefinitionSha256", "scenarioExpectedMetrics", "seed", "logicalClockUtc",
        "rerunSequence",
    }
    missing = sorted(required - set(envelope))
    if missing:
        raise RealmSliceEvidenceError(f"RSQ_METADATA_MISSING: {missing}")
    realm = envelope["realm"]
    mode = envelope["mode"]
    if envelope["protocolId"] != policy["protocolId"]:
        raise RealmSliceEvidenceError("RSQ_PROTOCOL_ID: protocol identity changed")
    if realm not in policy["realmOrder"] or envelope["realmOrdinal"] != policy["realmOrder"].index(realm) + 1:
        raise RealmSliceEvidenceError("RSQ_REALM_ORDER: realm identity or ordinal is invalid")
    if mode not in policy["modeNamespaces"]:
        raise RealmSliceEvidenceError(f"RSQ_MODE_INVALID: {mode}")
    namespace = policy["modeNamespaces"][mode]
    if envelope["modeNamespace"] != namespace:
        raise RealmSliceEvidenceError("RSQ_MODE_NAMESPACE: mode and namespace disagree")
    candidate_prefix = f"RSQ-{realm}-{namespace}-"
    packet_prefix = f"RSQ-EV-{realm}-{namespace}-"
    candidate_value = str(envelope["candidateId"])
    packet_value = str(envelope["evidencePacketId"])
    if not candidate_value.startswith(candidate_prefix) or not re.fullmatch(
        r"[A-Za-z0-9._-]+-[0-9]+", candidate_value[len(candidate_prefix):]
    ):
        raise RealmSliceEvidenceError("RSQ_CANDIDATE_ID: identity does not match realm/mode")
    if not packet_value.startswith(packet_prefix) or not re.fullmatch(
        r"[A-Za-z0-9._-]+-[0-9]+", packet_value[len(packet_prefix):]
    ):
        raise RealmSliceEvidenceError("RSQ_PACKET_ID: identity does not match realm/mode")
    candidate_suffix = candidate_value[len(candidate_prefix):]
    packet_suffix = packet_value[len(packet_prefix):]
    if candidate_suffix != packet_suffix:
        raise RealmSliceEvidenceError("RSQ_PACKET_CANDIDATE: revision or rerun identity differs")
    checks = {item["id"]: item for item in policy["checksByMode"][mode]}
    check = checks.get(envelope["checkId"])
    if check is None:
        raise RealmSliceEvidenceError("RSQ_CHECK_ID: check does not belong to mode")
    expected_scenario = check["scenarioId"].format(realm=realm)
    if (
        envelope["scenarioId"] != expected_scenario
        or envelope["scenarioVersion"] != check["scenarioVersion"]
    ):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_ID: scenario identity changed")
    definition_digest, expected_metrics = scenario_definition_identity(
        policy, envelope["scenarioId"], envelope["scenarioVersion"], envelope["checkId"]
    )
    if (
        envelope["scenarioCatalogSha256"] != policy["scenarioCatalogSha256"]
        or envelope["scenarioDefinitionSha256"] != definition_digest
        or envelope["scenarioExpectedMetrics"] != expected_metrics
    ):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_DEFINITION: scenario semantics changed")
    allowed = {
        "locale": policy["locales"],
        "inputClass": policy["inputClasses"],
        "accessibilityPreset": policy["accessibilityPresets"],
    }
    for field, values in allowed.items():
        if envelope[field] not in values:
            raise RealmSliceEvidenceError(f"RSQ_{field.upper()}_INVALID: {envelope[field]}")
    platform_scope = envelope.get("platformScope", envelope.get("platform"))
    if platform_scope != policy["platformScope"]:
        raise RealmSliceEvidenceError("RSQ_PLATFORM_SCOPE: unsupported platform claim")
    if not isinstance(envelope["rerunSequence"], int) or envelope["rerunSequence"] < 1:
        raise RealmSliceEvidenceError("RSQ_RERUN_SEQUENCE: positive integer required")


def row_directory(evidence_root: Path, envelope: dict[str, Any]) -> Path:
    """Return the protocol-mandated non-overlapping row namespace."""
    safe_values = (
        envelope["candidateId"], envelope["realm"], envelope["modeNamespace"],
        envelope["locale"], envelope["checkId"],
    )
    if any(not value or Path(str(value)).name != str(value) for value in safe_values):
        raise RealmSliceEvidenceError("RSQ_OUTPUT_PATH: unsafe path token")
    return (
        Path(evidence_root).resolve()
        / envelope["candidateId"]
        / envelope["realm"]
        / envelope["modeNamespace"]
        / envelope["locale"]
        / envelope["checkId"]
        / envelope.get("runId", derive_run_identity(envelope))
    )


def load_policy(path: Path = DEFAULT_POLICY) -> dict[str, Any]:
    try:
        policy = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RealmSliceEvidenceError(f"RSQ_POLICY_INVALID: {path}: {error}") from error
    required = {
        "schemaVersion", "harnessId", "harnessVersion", "protocolId",
        "realmOrder", "modeNamespaces", "locales", "inputClasses",
        "accessibilityPresets", "defaultAccessibilityPresets", "scenarioCatalog",
        "scenarioCatalogSha256",
        "reviewAllowedSigners", "metricSemantics", "playerTimeoutSeconds", "checksByMode",
    }
    if policy.get("schemaVersion") != 1 or not required.issubset(policy):
        raise RealmSliceEvidenceError("RSQ_POLICY_INVALID: required fields are missing")
    if policy["realmOrder"] != ["Stonehold", "Eldergrove", "Crownlands", "Umbral"]:
        raise RealmSliceEvidenceError("RSQ_POLICY_REALM_ORDER: canonical order changed")
    if policy["modeNamespaces"] != {"Adventure3D": "3d", "Kingdom2_5D": "2_5d"}:
        raise RealmSliceEvidenceError("RSQ_POLICY_MODE_NAMESPACE: canonical modes changed")
    scenario_name = str(policy["scenarioCatalog"])
    if not scenario_name or Path(scenario_name).name != scenario_name:
        raise RealmSliceEvidenceError("RSQ_POLICY_SCENARIO: scenario catalog path is unsafe")
    policy["_scenarioCatalogPath"] = str(Path(path).resolve().parent / scenario_name)
    signer_name = str(policy["reviewAllowedSigners"])
    if not signer_name or Path(signer_name).name != signer_name:
        raise RealmSliceEvidenceError("RSQ_POLICY_REVIEW_TRUST_PATH: reviewAllowedSigners must be a filename")
    policy["_reviewAllowedSignersPath"] = str(Path(path).resolve().parent / signer_name)
    all_ids: list[str] = []
    for mode in policy["modeNamespaces"]:
        checks = policy.get("checksByMode", {}).get(mode)
        if not isinstance(checks, list) or len(checks) != 12:
            raise RealmSliceEvidenceError(f"RSQ_POLICY_CHECK_COVERAGE: {mode} requires 12 checks")
        ids = [str(item.get("id") or "") for item in checks if isinstance(item, dict)]
        if len(ids) != 12 or len(ids) != len(set(ids)) or any(not value for value in ids):
            raise RealmSliceEvidenceError(f"RSQ_POLICY_CHECK_ID: invalid ids for {mode}")
        for item in checks:
            if not item.get("scenarioId") or not item.get("scenarioVersion") or not item.get("structuredLog"):
                raise RealmSliceEvidenceError(f"RSQ_POLICY_SCENARIO: incomplete check {item.get('id')}")
            if not isinstance(item.get("metrics"), list) or not item["metrics"]:
                raise RealmSliceEvidenceError(f"RSQ_POLICY_METRICS: incomplete check {item.get('id')}")
        all_ids.extend(ids)
    if len(all_ids) != len(set(all_ids)):
        raise RealmSliceEvidenceError("RSQ_POLICY_CHECK_ID: check ids overlap between modes")
    if policy_digest(policy) != CANONICAL_POLICY_SHA256:
        raise RealmSliceEvidenceError("RSQ_POLICY_IDENTITY: policy differs from the harness-pinned contract")
    return policy


def expand_run_specs(policy: dict[str, Any], realm: str, mode: str) -> list[dict[str, Any]]:
    if realm not in policy["realmOrder"]:
        raise RealmSliceEvidenceError(f"RSQ_REALM_INVALID: {realm}")
    if mode not in policy["modeNamespaces"]:
        raise RealmSliceEvidenceError(f"RSQ_MODE_INVALID: {mode}")
    specs: list[dict[str, Any]] = []
    for check in policy["checksByMode"][mode]:
        presets = (
            policy["accessibilityPresets"]
            if check.get("allAccessibilityPresets")
            else policy["defaultAccessibilityPresets"]
        )
        for locale in policy["locales"]:
            for input_class in policy["inputClasses"]:
                for preset in presets:
                    specs.append({
                        "realm": realm,
                        "realmOrdinal": policy["realmOrder"].index(realm) + 1,
                        "mode": mode,
                        "modeNamespace": policy["modeNamespaces"][mode],
                        "checkId": check["id"],
                        "scenarioId": check["scenarioId"].format(realm=realm),
                        "scenarioVersion": check["scenarioVersion"],
                        "structuredLog": check["structuredLog"],
                        "requiredMetrics": list(check["metrics"]),
                        "requiredArtifacts": list(policy["requiredArtifacts"]) + (
                            list(policy["performanceArtifacts"]) if check.get("performance") else []
                        ),
                        "locale": locale,
                        "inputClass": input_class,
                        "accessibilityPreset": preset,
                    })
    return specs


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _find_check(policy: dict[str, Any], mode: str, check_id: str) -> dict[str, Any]:
    for check in policy.get("checksByMode", {}).get(mode, []):
        if check.get("id") == check_id:
            return check
    raise RealmSliceEvidenceError(f"RSQ_CHECK_ID: {check_id} does not belong to {mode}")


def scenario_catalog_identity(policy: dict[str, Any]) -> str:
    catalog_path = Path(policy.get("_scenarioCatalogPath", DEFAULT_SCENARIOS))
    try:
        actual_digest = sha256_file(catalog_path)
    except OSError as error:
        raise RealmSliceEvidenceError(f"RSQ_SCENARIO_CATALOG: {catalog_path}: {error}") from error
    if actual_digest != policy.get("scenarioCatalogSha256"):
        raise RealmSliceEvidenceError(
            "RSQ_SCENARIO_CATALOG_IDENTITY: catalog differs from the policy-pinned definition"
        )
    catalog = _load_json(catalog_path, "RSQ_SCENARIO_CATALOG")
    if (
        catalog.get("schemaVersion") != 1
        or catalog.get("fixtureVersion") != policy["fixtureVersion"]
        or catalog.get("seed") != policy["seed"]
        or catalog.get("logicalClockUtc") != policy["logicalClockUtc"]
    ):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_CATALOG: policy identity mismatch")
    scenario_rows = catalog.get("scenarios")
    if not isinstance(scenario_rows, list):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_CATALOG: scenarios must be an array")
    if any(not isinstance(row, dict) for row in scenario_rows):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_CATALOG: every scenario must be an object")
    actual = {(row.get("id"), row.get("version")) for row in scenario_rows}
    expected = {
        (check["scenarioId"].format(realm=realm), check["scenarioVersion"])
        for checks in policy["checksByMode"].values()
        for check in checks
        for realm in (
            policy["realmOrder"] if "{realm}" in check["scenarioId"] else [policy["realmOrder"][0]]
        )
    }
    if len(actual) != len(scenario_rows) or actual != expected:
        raise RealmSliceEvidenceError("RSQ_SCENARIO_CATALOG: scenario coverage mismatch")
    return actual_digest


def scenario_definition_identity(
    policy: dict[str, Any], scenario_id: str, scenario_version: str, check_id: str
) -> tuple[str, dict[str, Any]]:
    """Return the pinned scenario-row digest and exact expected metrics for one check."""
    scenario_catalog_identity(policy)
    catalog_path = Path(policy.get("_scenarioCatalogPath", DEFAULT_SCENARIOS))
    catalog = _load_json(catalog_path, "RSQ_SCENARIO_CATALOG")
    matches = [
        row for row in catalog["scenarios"]
        if row.get("id") == scenario_id and row.get("version") == scenario_version
    ]
    if len(matches) != 1:
        raise RealmSliceEvidenceError("RSQ_SCENARIO_DEFINITION: exact scenario row is unavailable")
    scenario = matches[0]
    expected_by_check = scenario.get("expectedMetricsByCheck", {})
    if not isinstance(expected_by_check, dict):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_DEFINITION: expected metric bindings are invalid")
    expected_metrics = expected_by_check.get(check_id, {})
    if not isinstance(expected_metrics, dict):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_DEFINITION: check metric bindings are invalid")
    return sha256_bytes(canonical_json(scenario)), copy.deepcopy(expected_metrics)


def normalize_envelope(policy: dict[str, Any], envelope: dict[str, Any]) -> dict[str, Any]:
    normalized = copy.deepcopy(envelope)
    for identity_field in ("operator", "independentReviewer"):
        if isinstance(normalized.get(identity_field), str):
            normalized[identity_field] = normalized[identity_field].strip()
    realm = normalized.get("realm")
    mode = normalized.get("mode")
    if realm not in policy["realmOrder"] or mode not in policy["modeNamespaces"]:
        raise RealmSliceEvidenceError("RSQ_ENVELOPE_SCOPE: realm or mode invalid")
    build = normalized.get("build", {})
    catalogs = normalized.get("catalogs", {})
    save = normalized.get("saveFixture", {})
    platform = normalized.get("platform", {})
    check = _find_check(policy, mode, str(normalized.get("checkId")))
    definition_digest, expected_metrics = scenario_definition_identity(
        policy,
        str(normalized.get("scenarioId")),
        str(normalized.get("scenarioVersion")),
        check["id"],
    )
    normalized.update({
        "protocolId": policy["protocolId"],
        "realmOrdinal": policy["realmOrder"].index(realm) + 1,
        "modeNamespace": policy["modeNamespaces"][mode],
        "sourceRevision": build.get("sourceRevision"),
        "buildId": build.get("buildId"),
        "buildManifestSha256": build.get("manifestSha256"),
        "artifactTreeSha256": build.get("artifactTreeSha256"),
        "sceneCatalogSha256": catalogs.get("sceneCatalogSha256"),
        "contentCatalogSha256": catalogs.get("contentCatalogSha256"),
        "narrativeCatalogSha256": catalogs.get("narrativeCatalogSha256"),
        "saveFixtureId": save.get("id"),
        "saveFixtureSha256": save.get("sha256"),
        "fixtureVersion": policy["fixtureVersion"],
        "scenarioCatalogSha256": scenario_catalog_identity(policy),
        "scenarioDefinitionSha256": definition_digest,
        "scenarioExpectedMetrics": expected_metrics,
        "seed": policy["seed"],
        "logicalClockUtc": policy["logicalClockUtc"],
        "platformScope": platform.get("platform"),
        "deviceId": platform.get("deviceId"),
        "rerunSequence": int(str(normalized.get("candidateId", "")).rsplit("-", 1)[-1]),
    })
    normalized["requiredArtifacts"] = list(policy["requiredArtifacts"]) + (
        list(policy["performanceArtifacts"]) if check.get("performance") else []
    )
    normalized["runId"] = derive_run_identity(normalized)
    return normalized


def allocate_run_directory(evidence_root: Path, envelope: dict[str, Any]) -> Path:
    run_root = row_directory(evidence_root, envelope)
    if run_root.exists():
        raise RealmSliceEvidenceError(f"RSQ_PATH_COLLISION: {run_root}")
    run_root.mkdir(parents=True, exist_ok=False)
    return run_root


def build_player_command(player: Path, envelope: dict[str, Any], raw_root: Path) -> list[str]:
    return [
        str(Path(player).resolve()), "--al-realm-slice-evidence",
        "--candidate-id", envelope["candidateId"],
        "--evidence-packet-id", envelope["evidencePacketId"],
        "--realm", envelope["realm"], "--mode", envelope["mode"],
        "--check-id", envelope["checkId"],
        "--scenario-id", envelope["scenarioId"],
        "--scenario-version", envelope["scenarioVersion"],
        "--scenario-catalog-sha256", envelope["scenarioCatalogSha256"],
        "--scenario-definition-sha256", envelope["scenarioDefinitionSha256"],
        "--seed", str(envelope["seed"]),
        "--logical-clock-utc", envelope["logicalClockUtc"],
        "--locale", envelope["locale"],
        "--input-class", envelope["inputClass"],
        "--accessibility-preset", envelope["accessibilityPreset"],
        "--evidence-output-root", str(raw_root.resolve()),
        "-logFile", str((raw_root / "Player.log").resolve()),
    ]


def _default_capture_runner(
    _policy: dict[str, Any],
    _envelope: dict[str, Any],
    command: list[str],
    raw_root: Path,
) -> int:
    with (raw_root / "harness.log").open("ab") as output:
        process = subprocess.run(
            command, cwd=Path(command[0]).parent, stdin=subprocess.DEVNULL,
            stdout=output, stderr=subprocess.STDOUT, check=False,
            timeout=int(_policy["playerTimeoutSeconds"]),
        )
    return process.returncode


def _capture_artifacts(
    evidence_root: Path,
    run_root: Path,
    envelope: dict[str, Any],
    check: dict[str, Any],
    collected_utc: str,
) -> tuple[list[dict[str, Any]], list[str]]:
    raw_root = run_root / "raw"
    candidates: list[tuple[str, Path]] = [
        ("request", raw_root / "run-envelope.json"),
        ("player_log", raw_root / "Player.log"),
        ("harness_log", raw_root / "harness.log"),
        ("structured_log", raw_root / check["structuredLog"]),
        ("result", raw_root / "result.json"),
    ]
    candidates.extend(("screenshots", path) for path in sorted((raw_root / "screenshots").glob("*")))
    candidates.extend(("video", path) for path in sorted((raw_root / "video").glob("*")))
    if check.get("performance"):
        candidates.extend(("telemetry", path) for path in sorted((raw_root / "telemetry").glob("*")))
        candidates.extend(("profiler", path) for path in sorted((raw_root / "profiler").glob("*")))
    rows: list[dict[str, Any]] = []
    counts: dict[str, int] = {}
    for role, path in candidates:
        if not path.is_file() or path.is_symlink() or path.stat().st_size <= 0:
            continue
        counts[role] = counts.get(role, 0) + 1
        rows.append({
            "id": f"{envelope['modeNamespace']}-{envelope['runId']}-{role}-{counts[role]:03d}",
            "role": role,
            "path": path.resolve().relative_to(evidence_root.resolve()).as_posix(),
            "bytes": path.stat().st_size,
            "sha256": sha256_file(path),
            "collectedUtc": collected_utc,
        })
    missing = [role for role in envelope["requiredArtifacts"] if counts.get(role, 0) == 0]
    return sorted(rows, key=lambda row: (row["role"], row["path"])), missing


def policy_digest(policy: dict[str, Any]) -> str:
    return sha256_bytes(canonical_json({key: value for key, value in policy.items() if not key.startswith("_")}))


def review_trust_path(policy: dict[str, Any]) -> Path:
    return Path(
        policy.get("_reviewAllowedSignersPath")
        or DEFAULT_POLICY.with_name(str(policy.get("reviewAllowedSigners", "")))
    )


def review_trust_digest(policy: dict[str, Any]) -> str | None:
    path = review_trust_path(policy)
    return sha256_file(path) if path.is_file() and not path.is_symlink() else None


def _parse_utc(value: Any, code: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise RealmSliceEvidenceError(f"{code}: UTC timestamp ending in Z required")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise RealmSliceEvidenceError(f"{code}: invalid UTC timestamp") from error
    return parsed


def validate_timing(manifest: dict[str, Any]) -> None:
    timing = manifest.get("timing")
    if not isinstance(timing, dict):
        raise RealmSliceEvidenceError("RSQ_TIMING: timing envelope missing")
    started = _parse_utc(timing.get("startedUtc"), "RSQ_TIMING")
    completed = _parse_utc(timing.get("completedUtc"), "RSQ_TIMING")
    if completed < started:
        raise RealmSliceEvidenceError("RSQ_TIMING: capture completed before it started")
    if manifest.get("technicalResult") in {"PASS", "FAIL"}:
        reviewed = _parse_utc(manifest.get("reviewedUtc"), "RSQ_REVIEW_TIMING")
        if reviewed < completed:
            raise RealmSliceEvidenceError("RSQ_REVIEW_TIMING: review predates capture completion")


def build_review_attestation(manifest: dict[str, Any]) -> dict[str, Any]:
    """Return the complete immutable manifest projection signed by the reviewer."""
    projection = copy.deepcopy(manifest)
    projection.pop("manifestSha256", None)
    projection.pop("reviewerSignature", None)
    return {"namespace": "anotherlife-rsq-v1", "manifest": projection}


def verify_review_signature(policy: dict[str, Any], manifest: dict[str, Any]) -> None:
    if manifest.get("signatureMethod") != "ssh-keygen-y":
        raise RealmSliceEvidenceError("RSQ_REVIEW_SIGNATURE_METHOD: expected ssh-keygen-y")
    reviewer = str(manifest.get("reviewer") or "").strip()
    operator = str(manifest.get("operator") or "").strip()
    expected_reviewer = str(manifest.get("independentReviewer") or "").strip()
    if reviewer.casefold() != expected_reviewer.casefold() or reviewer.casefold() == operator.casefold():
        raise RealmSliceEvidenceError("RSQ_REVIEW_INDEPENDENCE: reviewer identity is not independent")
    allowed_signers = review_trust_path(policy)
    if not allowed_signers.is_file() or allowed_signers.is_symlink():
        raise RealmSliceEvidenceError("RSQ_REVIEW_TRUST: allowed-signers file is unavailable")
    if manifest.get("reviewTrustSha256") != sha256_file(allowed_signers):
        raise RealmSliceEvidenceError("RSQ_REVIEW_TRUST: trust configuration digest differs")
    signature = manifest.get("reviewerSignature")
    if not isinstance(signature, str) or "BEGIN SSH SIGNATURE" not in signature:
        raise RealmSliceEvidenceError("RSQ_REVIEW_SIGNATURE: detached SSH signature is missing")
    attestation = canonical_json(build_review_attestation(manifest))
    signature_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w", encoding="utf-8", suffix=".sshsig", delete=False
        ) as signature_file:
            signature_file.write(signature)
            signature_path = Path(signature_file.name)
        verification = subprocess.run(
            [
                "ssh-keygen", "-Y", "verify", "-f", str(allowed_signers),
                "-I", reviewer, "-n", "anotherlife-rsq-v1", "-s", str(signature_path),
            ],
            input=attestation,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            check=False,
        )
    except OSError as error:
        raise RealmSliceEvidenceError(f"RSQ_REVIEW_SIGNATURE_TOOL: {error}") from error
    finally:
        if signature_path is not None:
            signature_path.unlink(missing_ok=True)
    if verification.returncode != 0:
        raise RealmSliceEvidenceError("RSQ_REVIEW_SIGNATURE: detached signature verification failed")


def prepare_review_manifest(
    policy: dict[str, Any], provisional: dict[str, Any], review: dict[str, Any]
) -> dict[str, Any]:
    if policy_digest(policy) != CANONICAL_POLICY_SHA256:
        raise RealmSliceEvidenceError("RSQ_POLICY_IDENTITY: policy differs from pinned contract")
    if provisional.get("technicalResult") != "FAIL_CLOSED":
        raise RealmSliceEvidenceError("RSQ_REVIEW_STATE: only provisional fail-closed captures can finalize")
    proposed = provisional.get("proposedTechnicalResult")
    if proposed not in {"PASS", "FAIL"}:
        raise RealmSliceEvidenceError("RSQ_REVIEW_STATE: capture has no complete proposed disposition")
    if provisional.get("missingArtifacts") != ["review.attestation"]:
        raise RealmSliceEvidenceError("RSQ_REVIEW_STATE: capture has unresolved evidence defects")
    reviewer = str(review.get("reviewer") or "").strip()
    if reviewer.casefold() != str(provisional.get("independentReviewer") or "").strip().casefold():
        raise RealmSliceEvidenceError("RSQ_REVIEW_INDEPENDENCE: reviewer identity differs")
    if reviewer.casefold() == str(provisional.get("operator") or "").strip().casefold():
        raise RealmSliceEvidenceError("RSQ_REVIEW_INDEPENDENCE: reviewer must differ from operator")
    if review.get("reviewerDisposition") != proposed:
        raise RealmSliceEvidenceError("RSQ_REVIEW_DISPOSITION: review and proposed result differ")
    trust_digest = review_trust_digest(policy)
    if trust_digest is None:
        raise RealmSliceEvidenceError("RSQ_REVIEW_TRUST: allowed-signers file is unavailable")
    final = copy.deepcopy(provisional)
    final.update({
        "policySha256": policy_digest(policy),
        "reviewTrustSha256": trust_digest,
        "executionState": "COMPLETE",
        "technicalResult": proposed,
        "reasonCode": provisional["proposedReasonCode"],
        "reviewer": reviewer,
        "reviewedUtc": review.get("reviewedUtc"),
        "reviewerDisposition": proposed,
        "reviewerSignature": "",
        "signatureMethod": "ssh-keygen-y",
        "supersedes": [provisional["manifestSha256"]],
        "missingArtifacts": [],
    })
    final.pop("manifestSha256", None)
    validate_timing(final)
    return final


def finalize_review(
    policy: dict[str, Any],
    evidence_root: Path,
    provisional: dict[str, Any],
    review: dict[str, Any],
    signature: str,
) -> dict[str, Any]:
    verify_manifest(evidence_root, provisional, policy)
    final = prepare_review_manifest(policy, provisional, review)
    final["reviewerSignature"] = signature
    final["manifestSha256"] = digest_document(final, "manifestSha256")
    validate_manifest_schema(final)
    verify_manifest(evidence_root, final, policy)
    output = row_directory(Path(evidence_root).resolve(), final) / "reviewed-manifest.json"
    if output.exists():
        raise RealmSliceEvidenceError(f"RSQ_PATH_COLLISION: {output}")
    output.write_bytes(canonical_json(final))
    return final


def run_capture(
    policy: dict[str, Any],
    envelope: dict[str, Any],
    evidence_root: Path,
    player: Path,
    *,
    capture_runner: Callable[[dict[str, Any], dict[str, Any], list[str], Path], int] = _default_capture_runner,
    utc_now: Callable[[], str] = utc_now,
) -> dict[str, Any]:
    """Run one packaged-Player row and emit a fail-closed immutable manifest."""
    if policy_digest(policy) != CANONICAL_POLICY_SHA256:
        raise RealmSliceEvidenceError("RSQ_POLICY_IDENTITY: policy differs from pinned contract")
    normalized = normalize_envelope(policy, envelope)
    validate_envelope(policy, normalized)
    operator = normalized.get("operator")
    reviewer = normalized.get("independentReviewer")
    if is_missing_value(operator) or is_missing_value(reviewer):
        raise RealmSliceEvidenceError("RSQ_REVIEW_IDENTITY: operator and reviewer are required")
    if str(operator).casefold() == str(reviewer).casefold():
        raise RealmSliceEvidenceError("RSQ_REVIEW_INDEPENDENCE: reviewer must differ from operator")
    evidence_root = Path(evidence_root).resolve()
    run_root = allocate_run_directory(evidence_root, normalized)
    raw_root = run_root / "raw"
    raw_root.mkdir()
    (raw_root / "run-envelope.json").write_bytes(canonical_json(normalized))
    command = build_player_command(player, normalized, raw_root)
    started_utc = utc_now()
    harness_log = raw_root / "harness.log"
    harness_log.write_bytes(canonical_json({"event": "launch", "startedUtc": started_utc, "command": command}))
    exit_code = -1
    runner_error = ""
    try:
        exit_code = capture_runner(policy, normalized, command, raw_root)
    except Exception as error:
        runner_error = f"{type(error).__name__}: {error}"
    completed_utc = utc_now()
    with harness_log.open("ab") as output:
        output.write(canonical_json({
            "event": "complete", "completedUtc": completed_utc,
            "exitCode": exit_code, "runnerError": runner_error,
        }))

    check = _find_check(policy, normalized["mode"], normalized["checkId"])
    artifacts, missing = _capture_artifacts(evidence_root, run_root, normalized, check, completed_utc)
    result: dict[str, Any] = {}
    result_path = raw_root / "result.json"
    if result_path.is_file():
        try:
            result = _load_json(result_path, "RSQ_RESULT")
        except RealmSliceEvidenceError:
            missing.append("result.valid_json")
    metrics = result.get("metrics")
    if not isinstance(metrics, dict):
        missing.append("result.metrics")
    else:
        invalid_bound = set(invalid_bound_metrics(policy, normalized, check, metrics))
        invalid_pass = (
            set(invalid_pass_metrics(policy, normalized, check, metrics))
            if result.get("technicalResult") == "PASS" else set()
        )
        missing.extend(
            f"result.metrics.{name}"
            for name in check["metrics"]
            if (
                name not in metrics
                or is_missing_value(metrics[name])
                or name in invalid_bound
                or name in invalid_pass
            )
        )
    for field in ("expectedResult", "observedResult", "reasonCode"):
        if is_missing_value(result.get(field)):
            missing.append(f"result.{field}")
    if result.get("scenarioDefinitionSha256") != normalized["scenarioDefinitionSha256"]:
        missing.append("result.scenarioDefinitionSha256")
    if exit_code != 0:
        missing.append(f"player.exit.{exit_code}")
    defect_ids = result.get("defectIds")
    if not isinstance(defect_ids, list) or any(not isinstance(value, str) or not value for value in defect_ids):
        missing.append("result.defectIds")
        defect_ids = []
    if result.get("technicalResult") == "PASS" and defect_ids:
        missing.append("result.pass_with_defects")
    if result.get("technicalResult") == "FAIL" and not defect_ids:
        missing.append("result.fail_without_defect")
    if result.get("executionState") != "COMPLETE":
        missing.append("result.executionState")
    if result.get("technicalResult") not in {"PASS", "FAIL"}:
        missing.append("result.technicalResult")
    missing = sorted(set(missing))
    proposed_result = result.get("technicalResult") if not missing and not runner_error else "FAIL_CLOSED"
    if proposed_result in {"PASS", "FAIL"}:
        missing = ["review.attestation"]
    manifest = {
        "schemaVersion": policy["schemaVersion"],
        "harnessId": policy["harnessId"],
        "harnessVersion": policy["harnessVersion"],
        "policySha256": policy_digest(policy),
        "reviewTrustSha256": None,
        "protocolId": normalized["protocolId"],
        "candidateId": normalized["candidateId"],
        "evidencePacketId": normalized["evidencePacketId"],
        "realm": normalized["realm"],
        "realmOrdinal": normalized["realmOrdinal"],
        "mode": normalized["mode"],
        "modeNamespace": normalized["modeNamespace"],
        "checkId": normalized["checkId"],
        "runId": normalized["runId"],
        "rerunSequence": normalized["rerunSequence"],
        "build": normalized["build"],
        "catalogs": normalized["catalogs"],
        "scenario": {
            "id": normalized["scenarioId"],
            "version": normalized["scenarioVersion"],
            "fixtureVersion": normalized["fixtureVersion"],
            "catalogSha256": normalized["scenarioCatalogSha256"],
            "definitionSha256": normalized["scenarioDefinitionSha256"],
            "seed": normalized["seed"],
            "logicalClockUtc": normalized["logicalClockUtc"],
        },
        "saveFixture": normalized["saveFixture"],
        "platform": normalized["platform"],
        "qa": normalized["qa"],
        "locale": normalized["locale"],
        "inputClass": normalized["inputClass"],
        "accessibilityPreset": normalized["accessibilityPreset"],
        "operator": normalized["operator"],
        "independentReviewer": normalized["independentReviewer"],
        "timing": {"startedUtc": started_utc, "completedUtc": completed_utc},
        "command": command,
        "executionState": "BLOCKED",
        "technicalResult": "FAIL_CLOSED",
        "proposedTechnicalResult": proposed_result,
        "proposedReasonCode": str(result.get("reasonCode") or "RSQ_RESULT_REASON_MISSING"),
        "expectedResult": str(result.get("expectedResult") or "required row result was not supplied"),
        "observedResult": str(result.get("observedResult") or runner_error or "required evidence is incomplete"),
        "reasonCode": (
            "RSQ_REVIEW_REQUIRED"
            if proposed_result in {"PASS", "FAIL"} else "RSQ_EVIDENCE_INCOMPLETE"
        ),
        "defectIds": defect_ids,
        "artifactIds": [artifact["id"] for artifact in artifacts],
        "reviewer": str(reviewer),
        "reviewedUtc": None,
        "reviewerDisposition": "FAIL_CLOSED",
        "reviewerSignature": "",
        "signatureMethod": "",
        "supersedes": [],
        "metrics": metrics if isinstance(metrics, dict) else {},
        "artifacts": artifacts,
        "missingArtifacts": missing,
    }
    manifest["manifestSha256"] = digest_document(manifest, "manifestSha256")
    validate_timing(manifest)
    validate_manifest_schema(manifest)
    if proposed_result in {"PASS", "FAIL"}:
        verify_manifest(evidence_root, manifest, policy)
    (run_root / "manifest.json").write_bytes(canonical_json(manifest))
    return manifest


def verify_manifest(
    evidence_root: Path,
    manifest: dict[str, Any],
    policy: dict[str, Any] | None = None,
) -> None:
    if manifest.get("manifestSha256") != digest_document(manifest, "manifestSha256"):
        raise RealmSliceEvidenceError("RSQ_MANIFEST_HASH: manifest digest mismatch")
    policy = policy or load_policy()
    if policy_digest(policy) != CANONICAL_POLICY_SHA256:
        raise RealmSliceEvidenceError("RSQ_POLICY_IDENTITY: policy differs from pinned contract")
    if manifest.get("protocolId") != policy.get("protocolId"):
        raise RealmSliceEvidenceError("RSQ_PROTOCOL_ID: manifest and policy differ")
    if (
        manifest.get("harnessId") != policy.get("harnessId")
        or manifest.get("harnessVersion") != policy.get("harnessVersion")
        or manifest.get("policySha256") != policy_digest(policy)
    ):
        raise RealmSliceEvidenceError("RSQ_POLICY_IDENTITY: harness or policy digest differs")
    validate_timing(manifest)
    mode = manifest.get("mode")
    namespace = manifest.get("modeNamespace")
    if namespace != policy.get("modeNamespaces", {}).get(mode):
        raise RealmSliceEvidenceError("RSQ_MODE_NAMESPACE: manifest mode/namespace mismatch")
    check = _find_check(policy, str(mode), str(manifest.get("checkId")))
    scenario = manifest.get("scenario")
    if not isinstance(scenario, dict):
        raise RealmSliceEvidenceError("RSQ_SCENARIO_ID: scenario envelope missing")
    identity_input = {
        field: manifest.get(field)
        for field in (
            "candidateId", "evidencePacketId", "realm", "mode", "checkId", "build",
            "catalogs", "saveFixture", "platform", "qa", "locale", "inputClass",
            "accessibilityPreset", "operator", "independentReviewer",
        )
    }
    identity_input["scenarioId"] = scenario.get("id")
    identity_input["scenarioVersion"] = scenario.get("version")
    normalized = normalize_envelope(policy, identity_input)
    validate_envelope(policy, normalized)
    if normalized["runId"] != manifest.get("runId"):
        raise RealmSliceEvidenceError("RSQ_RUN_ID: manifest run identity mismatch")
    if (
        normalized["realmOrdinal"] != manifest.get("realmOrdinal")
        or normalized["rerunSequence"] != manifest.get("rerunSequence")
    ):
        raise RealmSliceEvidenceError("RSQ_TRACE_ORDINAL: realm or rerun ordinal differs")
    expected_scenario = {
        "id": normalized["scenarioId"],
        "version": normalized["scenarioVersion"],
        "fixtureVersion": normalized["fixtureVersion"],
        "catalogSha256": normalized["scenarioCatalogSha256"],
        "definitionSha256": normalized["scenarioDefinitionSha256"],
        "seed": normalized["seed"],
        "logicalClockUtc": normalized["logicalClockUtc"],
    }
    if scenario != expected_scenario:
        raise RealmSliceEvidenceError("RSQ_SCENARIO_DEFINITION: manifest scenario semantics differ")
    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list):
        raise RealmSliceEvidenceError("RSQ_REQUIRED_ARTIFACT: artifact inventory missing")
    roles = [row.get("role") for row in artifacts if isinstance(row, dict)]
    required_roles = list(policy["requiredArtifacts"]) + (
        list(policy["performanceArtifacts"]) if check.get("performance") else []
    )
    absent_roles = [role for role in required_roles if role not in roles]
    if absent_roles:
        raise RealmSliceEvidenceError(f"RSQ_REQUIRED_ARTIFACT: missing roles {absent_roles}")
    ids = [row.get("id") for row in artifacts if isinstance(row, dict)]
    paths = [row.get("path") for row in artifacts if isinstance(row, dict)]
    if len(ids) != len(set(ids)) or len(paths) != len(set(paths)):
        raise RealmSliceEvidenceError("RSQ_ARTIFACT_COLLISION: duplicate artifact identity or path")
    if manifest.get("artifactIds") != ids:
        raise RealmSliceEvidenceError("RSQ_ARTIFACT_IDS: artifact references differ from inventory")
    metrics = manifest.get("metrics")
    if not isinstance(metrics, dict) or any(name not in metrics for name in check["metrics"]):
        raise RealmSliceEvidenceError("RSQ_REQUIRED_METRIC: check metrics are incomplete")
    invalid_bound = invalid_bound_metrics(policy, normalized, check, metrics)
    if invalid_bound:
        raise RealmSliceEvidenceError(f"RSQ_BOUND_METRIC: mismatched metrics {invalid_bound}")
    if manifest.get("technicalResult") == "PASS":
        invalid_metrics = invalid_pass_metrics(policy, manifest, check, metrics)
        if invalid_metrics:
            raise RealmSliceEvidenceError(f"RSQ_PASS_METRIC: failing metrics {invalid_metrics}")
        if manifest.get("defectIds"):
            raise RealmSliceEvidenceError("RSQ_PASS_DEFECT: passing row has unresolved defects")
    if manifest.get("technicalResult") == "FAIL" and not manifest.get("defectIds"):
        raise RealmSliceEvidenceError("RSQ_FAIL_DEFECT: completed failure lacks durable defect ID")
    if manifest.get("technicalResult") in {"PASS", "FAIL"} and (
        manifest.get("executionState") != "COMPLETE" or manifest.get("missingArtifacts")
    ):
        raise RealmSliceEvidenceError("RSQ_COMPLETED_INVALID: reviewed row is incomplete")
    if manifest.get("technicalResult") in {"PASS", "FAIL"} and (
        manifest.get("reviewer") != manifest.get("independentReviewer")
        or manifest.get("reviewerDisposition") != manifest.get("technicalResult")
        or is_missing_value(manifest.get("reviewedUtc"))
        or is_missing_value(manifest.get("reviewerSignature"))
        or is_missing_value(manifest.get("signatureMethod"))
    ):
        raise RealmSliceEvidenceError("RSQ_REVIEW_INVALID: passing row lacks independent attestation")
    if manifest.get("technicalResult") in {"PASS", "FAIL"}:
        verify_review_signature(policy, manifest)
    prefix = (
        Path(manifest["candidateId"]) / manifest["realm"] / namespace /
        manifest["locale"] / manifest["checkId"] / manifest["runId"]
    ).as_posix() + "/"
    root = Path(evidence_root).resolve()
    run_root = (root / prefix).resolve()
    row_prefix = PurePosixPath(prefix)
    exact_role_paths = {
        "request": PurePosixPath("raw/run-envelope.json"),
        "player_log": PurePosixPath("raw/Player.log"),
        "harness_log": PurePosixPath("raw/harness.log"),
        "structured_log": PurePosixPath("raw") / str(check["structuredLog"]),
        "result": PurePosixPath("raw/result.json"),
    }
    role_directories = {
        "screenshots": PurePosixPath("raw/screenshots"),
        "video": PurePosixPath("raw/video"),
        "telemetry": PurePosixPath("raw/telemetry"),
        "profiler": PurePosixPath("raw/profiler"),
    }
    verified_paths: list[Path] = []
    for artifact in artifacts:
        if not isinstance(artifact, dict):
            raise RealmSliceEvidenceError("RSQ_ARTIFACT_PATH: artifact entry is not an object")
        relative = artifact.get("path", "")
        pure_relative = PurePosixPath(relative) if isinstance(relative, str) else None
        if (
            pure_relative is None
            or pure_relative.is_absolute()
            or "\\" in relative
            or ".." in pure_relative.parts
            or relative != pure_relative.as_posix()
        ):
            raise RealmSliceEvidenceError("RSQ_ARTIFACT_PATH: artifact path is not normalized")
        if not relative.startswith(prefix):
            raise RealmSliceEvidenceError("RSQ_CROSS_MODE_ARTIFACT: artifact path outside row namespace")
        if not str(artifact.get("id", "")).startswith(namespace + "-"):
            raise RealmSliceEvidenceError("RSQ_CROSS_MODE_ARTIFACT: artifact ID outside mode namespace")
        role = artifact.get("role")
        try:
            row_relative = pure_relative.relative_to(row_prefix)
        except ValueError as error:
            raise RealmSliceEvidenceError("RSQ_ARTIFACT_ROLE_PATH: artifact path differs from row") from error
        if (
            role in exact_role_paths and row_relative != exact_role_paths[role]
        ) or (
            role in role_directories and row_relative.parent != role_directories[role]
        ) or role not in exact_role_paths | role_directories:
            raise RealmSliceEvidenceError(
                "RSQ_ARTIFACT_ROLE_PATH: artifact role does not match its canonical path"
            )
        path = (root / relative).resolve()
        try:
            path.relative_to(run_root)
        except ValueError as error:
            raise RealmSliceEvidenceError("RSQ_ARTIFACT_PATH: artifact escapes exact row root") from error
        if not path.is_file() or path.is_symlink():
            raise RealmSliceEvidenceError(f"RSQ_ARTIFACT_MISSING: {relative}")
        if any(path.samefile(existing) for existing in verified_paths):
            raise RealmSliceEvidenceError(
                "RSQ_ARTIFACT_COLLISION: multiple artifact rows resolve to one file"
            )
        verified_paths.append(path)
        if path.stat().st_size != artifact.get("bytes") or sha256_file(path) != artifact.get("sha256"):
            raise RealmSliceEvidenceError(f"RSQ_ARTIFACT_HASH: {relative}")

    result_artifacts = [row for row in artifacts if row["role"] == "result"]
    if len(result_artifacts) != 1:
        raise RealmSliceEvidenceError("RSQ_RESULT_BINDING: exactly one result artifact is required")
    result_path = (root / result_artifacts[0]["path"]).resolve()
    raw_result = _load_json(result_path, "RSQ_RESULT_BINDING")
    expected_technical_result = (
        manifest.get("proposedTechnicalResult")
        if manifest.get("technicalResult") == "FAIL_CLOSED"
        else manifest.get("technicalResult")
    )
    expected_result = {
        "executionState": "COMPLETE",
        "technicalResult": expected_technical_result,
        "expectedResult": manifest.get("expectedResult"),
        "observedResult": manifest.get("observedResult"),
        "reasonCode": manifest.get("proposedReasonCode"),
        "defectIds": manifest.get("defectIds"),
        "scenarioDefinitionSha256": scenario.get("definitionSha256"),
        "metrics": manifest.get("metrics"),
    }
    if any(raw_result.get(field) != value for field, value in expected_result.items()):
        raise RealmSliceEvidenceError(
            "RSQ_RESULT_BINDING: manifest disposition or observations differ from result.json"
        )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument("--allowed-signers", type=Path)
    subparsers = parser.add_subparsers(dest="command", required=True)
    matrix = subparsers.add_parser("matrix", help="Write the deterministic run cube for one mode")
    matrix.add_argument("--realm", required=True)
    matrix.add_argument("--mode", required=True)
    matrix.add_argument("--output", type=Path, required=True)

    capture = subparsers.add_parser("capture", help="Run one packaged-Player evidence row")
    for argument in (
        "repo-root", "build-manifest", "qa-report", "platform-metadata", "evidence-root",
    ):
        capture.add_argument(f"--{argument}", type=Path, required=True)
    for argument in (
        "save-fixture-id", "candidate-id", "evidence-packet-id", "realm", "mode", "check-id",
        "locale", "input-class", "accessibility-preset", "operator", "independent-reviewer",
    ):
        capture.add_argument(f"--{argument}", required=True)

    verify = subparsers.add_parser("verify", help="Verify a row manifest and every artifact")
    verify.add_argument("--evidence-root", type=Path, required=True)
    verify.add_argument("--manifest", type=Path, required=True)

    attestation = subparsers.add_parser("attestation", help="Write the finalized review payload to sign")
    attestation.add_argument("--evidence-root", type=Path, required=True)
    attestation.add_argument("--manifest", type=Path, required=True)
    attestation.add_argument("--review-metadata", type=Path, required=True)
    attestation.add_argument("--output", type=Path, required=True)

    finalize = subparsers.add_parser("finalize", help="Verify review signature and write reviewed manifest")
    finalize.add_argument("--evidence-root", type=Path, required=True)
    finalize.add_argument("--manifest", type=Path, required=True)
    finalize.add_argument("--review-metadata", type=Path, required=True)
    finalize.add_argument("--signature", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        policy = load_policy(args.policy)
        if args.allowed_signers is not None:
            policy["_reviewAllowedSignersPath"] = str(args.allowed_signers.resolve())
        if args.command == "matrix":
            runs = expand_run_specs(policy, args.realm, args.mode)
            payload = {
                "schemaVersion": 1,
                "protocolId": policy["protocolId"],
                "harnessId": policy["harnessId"],
                "harnessVersion": policy["harnessVersion"],
                "realm": args.realm,
                "mode": args.mode,
                "modeNamespace": policy["modeNamespaces"][args.mode],
                "runs": runs,
            }
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_bytes(canonical_json(payload))
            print(f"matrix={args.output}")
            print(f"runs={len(runs)}")
            return 0
        if args.command == "verify":
            manifest = _load_json(args.manifest, "RSQ_MANIFEST")
            verify_manifest(args.evidence_root, manifest, policy)
            validate_manifest_schema(manifest)
            print(f"verified={args.manifest}")
            print(f"manifest_sha256={manifest['manifestSha256']}")
            return 0
        if args.command == "attestation":
            manifest = _load_json(args.manifest, "RSQ_MANIFEST")
            review = _load_json(args.review_metadata, "RSQ_REVIEW_METADATA")
            verify_manifest(args.evidence_root, manifest, policy)
            candidate = prepare_review_manifest(policy, manifest, review)
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_bytes(canonical_json(build_review_attestation(candidate)))
            print(f"attestation={args.output}")
            return 0
        if args.command == "finalize":
            manifest = _load_json(args.manifest, "RSQ_MANIFEST")
            review = _load_json(args.review_metadata, "RSQ_REVIEW_METADATA")
            signature = args.signature.read_text(encoding="utf-8")
            final = finalize_review(policy, args.evidence_root, manifest, review, signature)
            final_path = row_directory(args.evidence_root, final) / "reviewed-manifest.json"
            print(f"reviewed_manifest={final_path}")
            print(f"technical_result={final['technicalResult']}")
            return 0

        source = load_source_evidence(
            args.repo_root, args.build_manifest, args.qa_report,
            args.platform_metadata, args.save_fixture_id,
        )
        check = _find_check(policy, args.mode, args.check_id)
        envelope = {
            "candidateId": args.candidate_id,
            "evidencePacketId": args.evidence_packet_id,
            "realm": args.realm,
            "mode": args.mode,
            "checkId": args.check_id,
            "scenarioId": check["scenarioId"].format(realm=args.realm),
            "scenarioVersion": check["scenarioVersion"],
            "build": source["build"],
            "catalogs": source["catalogs"],
            "saveFixture": source["saveFixture"],
            "platform": source["platform"],
            "qa": source["qa"],
            "locale": args.locale,
            "inputClass": args.input_class,
            "accessibilityPreset": args.accessibility_preset,
            "operator": args.operator,
            "independentReviewer": args.independent_reviewer,
        }
        if not args.operator.strip() or not args.independent_reviewer.strip():
            raise RealmSliceEvidenceError("RSQ_REVIEW_IDENTITY: operator and reviewer are required")
        if args.operator == args.independent_reviewer:
            raise RealmSliceEvidenceError("RSQ_REVIEW_INDEPENDENCE: reviewer must differ from operator")
        manifest = run_capture(policy, envelope, args.evidence_root, source["player"])
        manifest_path = row_directory(args.evidence_root, normalize_envelope(policy, envelope)) / "manifest.json"
        print(f"manifest={manifest_path}")
        print(f"technical_result={manifest['technicalResult']}")
        return 0 if manifest["proposedTechnicalResult"] in {"PASS", "FAIL"} else 1
    except (RealmSliceEvidenceError, OSError, ValueError) as error:
        print(f"realm-slice-evidence: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
