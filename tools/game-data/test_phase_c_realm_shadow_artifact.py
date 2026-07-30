#!/usr/bin/env python3
"""Validate deterministic, unwired Phase C9A realm shadow outputs."""

from __future__ import annotations

import argparse
import copy
import sys
from pathlib import Path
from typing import Any, Callable

import generate_phase_c_realm_shadow_artifact as generator


ERROR_PREFIX = "Phase C9A realm shadow validation failed"
ARTIFACT_KEYS = [
    "gameId",
    "catalogId",
    "family",
    "schemaVersion",
    "contentVersion",
    "sourceRevision",
    "records",
    "aliases",
]
RECORD_KEYS = [
    "id",
    "legacy_realm_id",
    "legacy_realm_value",
    "name_ref",
    "description_ref",
    "inner_realm_id",
    "main_gate_id",
    "outer_warzone_id",
    "rare_resource_id",
    "capability_profile_ids",
    "asset_ref",
]
EVIDENCE_KEYS = [
    "schemaVersion",
    "evidenceId",
    "purpose",
    "productionEligible",
    "runtimeAuthority",
    "consumerActivation",
    "sourceCandidate",
    "artifact",
    "generator",
    "inputs",
    "comparison",
    "diagnostics",
    "forbiddenOutputs",
]


class ValidationError(RuntimeError):
    """Raised when committed shadow outputs drift from reviewed generation."""


def fail(message: str) -> None:
    raise ValidationError(f"{ERROR_PREFIX}: {message}")


def assert_keys(
    value: dict[str, Any],
    expected: list[str],
    json_path: str,
) -> None:
    if list(value) != expected:
        fail(
            f"{json_path} expected ordered properties {expected}, "
            f"found {list(value)}"
        )


def validate_artifact(
    artifact: dict[str, Any],
    expected: dict[str, Any],
) -> None:
    assert_keys(artifact, ARTIFACT_KEYS, "$")
    for field in ARTIFACT_KEYS:
        if field in {"records", "aliases"}:
            continue
        if artifact[field] != expected[field]:
            fail(f"artifact {field} drifted")

    records = artifact.get("records")
    expected_records = expected["records"]
    if not isinstance(records, list) or len(records) != 4:
        fail("artifact must contain exactly four records")
    for index, record in enumerate(records):
        if not isinstance(record, dict):
            fail(f"$.records[{index}] must be an object")
        assert_keys(record, RECORD_KEYS, f"$.records[{index}]")
        expected_record = expected_records[index]
        for field in RECORD_KEYS:
            if record[field] != expected_record[field]:
                fail(f"$.records[{index}].{field} drifted")

    if artifact.get("aliases") != []:
        fail("artifact aliases must remain exactly empty")


def validate_evidence(
    evidence: dict[str, Any],
    expected: dict[str, Any],
    artifact_raw: bytes,
) -> None:
    assert_keys(evidence, EVIDENCE_KEYS, "$")
    if evidence != expected:
        fail("machine-readable shadow evidence drifted")
    artifact_row = evidence["artifact"]
    if artifact_row["rawSha256"] != generator.sha256(artifact_raw):
        fail("evidence artifact hash does not match committed raw bytes")
    if evidence["productionEligible"] is not False:
        fail("shadow evidence must remain production-ineligible")
    if evidence["runtimeAuthority"] != "unchanged":
        fail("shadow evidence must retain unchanged runtime authority")
    if evidence["consumerActivation"] != "none":
        fail("shadow evidence must retain zero consumer activation")
    if evidence["sourceCandidate"]["effectiveGlobalBlockingCount"] != 26:
        fail("shadow evidence must retain all 26 global blockers")

    diagnostics = evidence["diagnostics"]
    expected_codes = [
        "AL-C9A-REALM-SHADOW-MATCH",
        "AL-C9A-REALM-SHADOW-MATCH",
        "AL-C9A-REALM-SHADOW-MATCH",
        "AL-C9A-REALM-SHADOW-MATCH",
        "AL-C9A-SPECIALIZED-SCOPE-RETAINED",
        "AL-C9A-RUNTIME-AUTHORITY-UNCHANGED",
    ]
    if [item.get("code") for item in diagnostics] != expected_codes:
        fail("comparison diagnostic order drifted")
    if [item.get("realmId") for item in diagnostics[:4]] != [
        "crownlands",
        "stonehold",
        "eldergrove",
        "umbral",
    ]:
        fail("realm comparison diagnostic order drifted")


def validate_no_runtime_consumer(repo_root: Path) -> None:
    needle = generator.CATALOG_ID.encode("utf-8")
    roots = [
        repo_root / "unity/Assets/AL/Scripts",
        repo_root / "android/app/src",
    ]
    unexpected: list[str] = []
    for root in roots:
        if not root.exists():
            continue
        for source_file in sorted(path for path in root.rglob("*") if path.is_file()):
            try:
                if needle in source_file.read_bytes():
                    unexpected.append(str(source_file.relative_to(repo_root)))
            except OSError as error:
                fail(f"could not inspect runtime source {source_file}: {error}")
    if unexpected:
        fail(
            "shadow catalog identity appears in runtime/Android consumers: "
            + ", ".join(unexpected)
        )


def validate_committed_outputs(
    repo_root: Path,
) -> tuple[dict[str, Any], dict[str, Any], bytes, bytes]:
    _, mappings = generator.validate_source_chain(repo_root)
    content_by_key = generator.validate_content_map(repo_root, mappings)
    specialized = generator.validate_specialized_catalog(
        repo_root,
        mappings,
        content_by_key,
    )
    expected_artifact = generator.build_artifact(mappings)
    artifact_raw_first = generator.canonical_json(expected_artifact)
    artifact_raw_second = generator.canonical_json(
        generator.build_artifact(copy.deepcopy(mappings))
    )
    if artifact_raw_first != artifact_raw_second:
        fail("two clean artifact generations produced different bytes")

    artifact, committed_artifact_raw = generator.load_json(
        repo_root / generator.ARTIFACT_PATH,
        "committed realm shadow artifact",
    )
    if committed_artifact_raw != artifact_raw_first:
        fail("committed artifact bytes differ from deterministic generation")
    if generator.canonical_json(artifact) != committed_artifact_raw:
        fail("committed artifact is not canonical deterministic JSON")
    validate_artifact(artifact, expected_artifact)

    evidence, committed_evidence_raw = generator.load_json(
        repo_root / generator.EVIDENCE_PATH,
        "committed realm shadow evidence",
    )
    generator_revision = evidence.get("generator", {}).get("sourceRevision")
    if not isinstance(generator_revision, str) or not generator_revision:
        fail("evidence generator revision is missing")
    generator_raw_sha256 = generator.validate_generator_revision(
        repo_root,
        generator_revision,
    )
    expected_evidence = generator.build_evidence(
        artifact_raw_first,
        generator_revision,
        generator_raw_sha256,
        mappings,
        specialized,
    )
    evidence_raw_first = generator.canonical_json(expected_evidence)
    evidence_raw_second = generator.canonical_json(
        generator.build_evidence(
            artifact_raw_second,
            generator_revision,
            generator_raw_sha256,
            copy.deepcopy(mappings),
            copy.deepcopy(specialized),
        )
    )
    if evidence_raw_first != evidence_raw_second:
        fail("two clean evidence generations produced different bytes")
    if committed_evidence_raw != evidence_raw_first:
        fail("committed evidence bytes differ from deterministic generation")
    if generator.canonical_json(evidence) != committed_evidence_raw:
        fail("committed evidence is not canonical deterministic JSON")
    validate_evidence(evidence, expected_evidence, committed_artifact_raw)

    generator.validate_forbidden_outputs(repo_root)
    validate_no_runtime_consumer(repo_root)
    return artifact, evidence, committed_artifact_raw, committed_evidence_raw


def run_negative_fixtures(
    artifact: dict[str, Any],
    evidence: dict[str, Any],
) -> int:
    expected_artifact = copy.deepcopy(artifact)
    expected_evidence = copy.deepcopy(evidence)
    artifact_mutations: list[
        tuple[str, Callable[[dict[str, Any]], None]]
    ] = [
        (
            "authored order",
            lambda value: value["records"].reverse(),
        ),
        (
            "legacy enum value",
            lambda value: value["records"][0].__setitem__(
                "legacy_realm_value",
                1,
            ),
        ),
        (
            "content reference",
            lambda value: value["records"][0].__setitem__(
                "name_ref",
                "realm.stonehold.name",
            ),
        ),
        (
            "world reference",
            lambda value: value["records"][0].__setitem__(
                "main_gate_id",
                "gate_stonehold_faultline",
            ),
        ),
        (
            "rare resource relation",
            lambda value: value["records"][0].__setitem__(
                "rare_resource_id",
                "deep_ore",
            ),
        ),
        (
            "capability profile relation",
            lambda value: value["records"][0].__setitem__(
                "capability_profile_ids",
                ["battle_realm_stonehold"],
            ),
        ),
        (
            "asset reference relation",
            lambda value: value["records"][0].__setitem__(
                "asset_ref",
                expected_artifact["records"][1]["asset_ref"],
            ),
        ),
        (
            "unexpected alias",
            lambda value: value.__setitem__(
                "aliases",
                [
                    {
                        "legacyId": "Crownlands",
                        "canonicalId": "crownlands",
                        "introducedVersion": 1,
                        "retirementVersion": None,
                        "migrationIssue": "#183",
                    }
                ],
            ),
        ),
    ]
    evidence_mutations: list[
        tuple[str, Callable[[dict[str, Any]], None]]
    ] = [
        (
            "production eligibility",
            lambda value: value.__setitem__("productionEligible", True),
        ),
        (
            "runtime authority",
            lambda value: value.__setitem__("runtimeAuthority", "shadow"),
        ),
        (
            "consumer activation",
            lambda value: value.__setitem__("consumerActivation", "realm_ui"),
        ),
        (
            "global blocker count",
            lambda value: value["sourceCandidate"].__setitem__(
                "effectiveGlobalBlockingCount",
                25,
            ),
        ),
        (
            "diagnostic order",
            lambda value: value["diagnostics"].reverse(),
        ),
        (
            "artifact hash",
            lambda value: value["artifact"].__setitem__(
                "rawSha256",
                "0" * 64,
            ),
        ),
    ]

    count = 0
    for name, mutate in artifact_mutations:
        changed = copy.deepcopy(artifact)
        mutate(changed)
        try:
            validate_artifact(changed, expected_artifact)
        except ValidationError:
            count += 1
            continue
        fail(f"negative artifact fixture unexpectedly passed: {name}")

    artifact_raw = generator.canonical_json(artifact)
    for name, mutate in evidence_mutations:
        changed = copy.deepcopy(evidence)
        mutate(changed)
        try:
            validate_evidence(changed, expected_evidence, artifact_raw)
        except ValidationError:
            count += 1
            continue
        fail(f"negative evidence fixture unexpectedly passed: {name}")
    return count


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--run-negative-fixtures",
        action="store_true",
        help="prove fourteen representative artifact/evidence drifts fail closed",
    )
    parser.add_argument(
        "--require-production-eligible",
        action="store_true",
        help="validate first, then prove production use is refused",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    try:
        artifact, evidence, artifact_raw, evidence_raw = (
            validate_committed_outputs(repo_root)
        )
        negative_count = 0
        if args.run_negative_fixtures:
            negative_count = run_negative_fixtures(artifact, evidence)
        if args.require_production_eligible:
            fail(
                "production use refused: the shadow has no manifest or "
                "consumer activation and the v003 source retains 26 blockers"
            )
    except (ValidationError, generator.GenerationError) as error:
        print(error, file=sys.stderr)
        return 1

    print(
        "PASS: two clean generations produced identical artifact and "
        "evidence bytes"
    )
    print(
        "PASS: four authored-order records, zero aliases, exact content, "
        "and specialized comparisons are retained"
    )
    print(
        "PASS: no production outputs or runtime/Android consumers use the "
        "shadow catalog identity"
    )
    if args.run_negative_fixtures:
        print(f"PASS: {negative_count} negative fixtures failed closed")
    print(f"PASS: artifact raw SHA-256 {generator.sha256(artifact_raw)}")
    print(f"PASS: evidence raw SHA-256 {generator.sha256(evidence_raw)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
