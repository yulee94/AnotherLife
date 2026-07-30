#!/usr/bin/env python3
"""Generate or verify the unwired Phase C9A realm shadow artifact."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any

import test_phase_c_six_family_building_v003_technical_source as phase_c_v003


ERROR_PREFIX = "Phase C9A realm shadow generation failed"
GAME_ID = "another-life"
CATALOG_ID = "realms_phase_c9a_shadow_v1"
CONTENT_VERSION = "0.1.0-shadow.1"
SOURCE_CANDIDATE_ID = (
    "game-data-phase-c-six-family-technical-source-2026-07-29-v003"
)
REALM_SOURCE_CANDIDATE_ID = (
    "game-data-phase-c-six-family-technical-source-2026-07-29-v002"
)
ARTIFACT_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/Shadow/"
    "realm-family-shadow-v001.json"
)
EVIDENCE_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/Shadow/"
    "realm-family-shadow-v001.evidence.json"
)
GENERATOR_PATH = (
    "tools/game-data/generate_phase_c_realm_shadow_artifact.py"
)
SPECIALIZED_CATALOG_PATH = (
    "unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json"
)
CONTENT_MAP_PATH = (
    "unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json"
)
LEGACY_SERVICE_PATH = (
    "unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs"
)
V002_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source-v002.json"
)
V003_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source-v003.json"
)
REALM_REFERENCE_PATH = (
    "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
    "GameDataRealmReferences.cs"
)
CAPABILITY_PROFILE_PATH = (
    "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
    "GameDataRealmCapabilityProfiles.cs"
)
SIX_FAMILY_SCHEMA_PATH = (
    "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
    "GameDataSixFamilySchemas.cs"
)
FORBIDDEN_OUTPUTS = [
    "unity/Assets/AL/StreamingAssets/GameData/catalog-set.json",
    "unity/Assets/Resources/GameData/catalog-set.json",
    "unity/Docs/GameDataCatalog/PhaseC/Generated/catalog-set.json",
    "unity/Assets/AL/StreamingAssets/GameData/Catalogs/realms.json",
    "unity/Assets/Resources/GameData/Catalogs/realms.json",
]
PINNED_INPUTS = [
    {
        "role": "six_family_technical_source_v003",
        "path": V003_PATH,
        "sourceRevision": "779e7363fca9ffed9e412f43cc74b20665fa4e9c",
        "rawSha256": (
            "984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439"
        ),
    },
    {
        "role": "realm_technical_source_v002",
        "path": V002_PATH,
        "sourceRevision": "d219472073bee9fcd420d0cac1d94412019b865b",
        "rawSha256": (
            "60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685"
        ),
    },
    {
        "role": "realm_content_map",
        "path": CONTENT_MAP_PATH,
        "sourceRevision": "963c4bc6e6db8ae2b87d363ceb229519e97f13b0",
        "rawSha256": (
            "8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353"
        ),
    },
    {
        "role": "specialized_realm_catalog",
        "path": SPECIALIZED_CATALOG_PATH,
        "sourceRevision": "2119c89bfa985a0a3e273042cf086a99a49b45b0",
        "rawSha256": (
            "33321936662b98f9c18edf4122ad163053d1aff3017b06556cad694420e9e8d8"
        ),
    },
    {
        "role": "legacy_realm_service",
        "path": LEGACY_SERVICE_PATH,
        "sourceRevision": "efd64249c96761d2c0f1e0097c4402d46231c09a",
        "rawSha256": (
            "7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9"
        ),
    },
    {
        "role": "realm_reference_registry",
        "path": REALM_REFERENCE_PATH,
        "sourceRevision": "f44e22cd3a0e334062f1ef8e487ffca1ecba6261",
        "rawSha256": (
            "4bb8457c9831756a8cf6c2ddf3f14a5fd5c51866370c870cb074a53313bbdf4f"
        ),
    },
    {
        "role": "realm_capability_profile_registry",
        "path": CAPABILITY_PROFILE_PATH,
        "sourceRevision": "59b99aa94e45d5564e373b8824e2aa3b30b8a754",
        "rawSha256": (
            "8413f45a32cad1bf71107c0c6cea18e4c8e86b7f8191a19ff0bcc0875e89b427"
        ),
    },
    {
        "role": "six_family_schema",
        "path": SIX_FAMILY_SCHEMA_PATH,
        "sourceRevision": "a2e6a9a0dddfb7522d880d4db9d17222adcbbffe",
        "rawSha256": (
            "3c759d9ea2f1b2d6aca53d1e5f213bf0edb057eb0751bf3c9bfe9ae94b15d9bb"
        ),
    },
]


class GenerationError(RuntimeError):
    """Raised when generation cannot prove the reviewed source contract."""


def fail(message: str) -> None:
    raise GenerationError(f"{ERROR_PREFIX}: {message}")


def sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains duplicate property {key!r}")
        result[key] = value
    return result


def canonical_json(value: dict[str, Any]) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, separators=(",", ": "))
        + "\n"
    ).encode("utf-8")


def load_json(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    if not path.is_file():
        fail(f"{label} is missing: {path}")
    raw = path.read_bytes()
    if not raw:
        fail(f"{label} is empty")
    if raw.startswith(b"\xef\xbb\xbf"):
        fail(f"{label} must be UTF-8 without a BOM")
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        fail(f"{label} is not valid UTF-8: {error}")
    try:
        value = json.loads(
            text,
            object_pairs_hook=strict_object,
            parse_constant=lambda token: fail(
                f"{label} contains non-finite number {token!r}"
            ),
        )
    except json.JSONDecodeError as error:
        fail(f"{label} is not strict JSON: {error}")
    if not isinstance(value, dict):
        fail(f"{label} root must be an object")
    return value, raw


def git_blob(repo_root: Path, revision: str, relative_path: str) -> bytes:
    result = subprocess.run(
        ["git", "cat-file", "blob", f"{revision}:{relative_path}"],
        cwd=repo_root,
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        fail(
            f"Git could not read {revision}:{relative_path}: "
            f"{result.stderr.decode('utf-8', errors='replace').strip()}"
        )
    return result.stdout


def validate_pinned_input(repo_root: Path, source: dict[str, str]) -> None:
    source_file = repo_root / source["path"]
    if not source_file.is_file():
        fail(f"pinned input is missing: {source['path']}")
    current = source_file.read_bytes()
    committed = git_blob(
        repo_root,
        source["sourceRevision"],
        source["path"],
    )
    if sha256(committed) != source["rawSha256"]:
        fail(f"pinned Git blob hash drifted for {source['path']}")
    if current != committed:
        fail(f"working input differs from pinned bytes: {source['path']}")


def family_by_name(source: dict[str, Any], family_name: str) -> dict[str, Any]:
    families = source.get("families")
    if not isinstance(families, list):
        fail("technical source families must be an array")
    matches = [
        family
        for family in families
        if isinstance(family, dict) and family.get("family") == family_name
    ]
    if len(matches) != 1:
        fail(f"technical source must contain one exact {family_name!r} family")
    return matches[0]


def validate_source_chain(
    repo_root: Path,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    for source in PINNED_INPUTS:
        validate_pinned_input(repo_root, source)

    try:
        v003, _, _ = phase_c_v003.validate_candidate(
            repo_root,
            repo_root / V003_PATH,
        )
        _, v002 = phase_c_v003.load_ancestors(repo_root)
    except phase_c_v003.ValidationError as error:
        fail(str(error))

    if v003.get("candidateId") != SOURCE_CANDIDATE_ID:
        fail("v003 candidate identity drifted")
    if v003.get("productionEligible") is not False:
        fail("v003 must remain production-ineligible")
    approval = v003.get("approval")
    if not isinstance(approval, dict) or approval.get("runtimeAuthority") != "unchanged":
        fail("v003 runtime authority must remain unchanged")
    if len(v003.get("blockingIds", [])) != 26:
        fail("v003 must retain exactly 26 effective blockers")

    realm_family = family_by_name(v003, "realms")
    expected_realm_family = {
        "family": "realms",
        "requiredForProduction": True,
        "artifactDisposition": "ready_for_non_production_shadow_generation",
        "inherits": {
            "candidateId": REALM_SOURCE_CANDIDATE_ID,
            "family": "realms",
            "fields": [
                "mappings",
                "unavailableAnchors",
                "resolvedBlockingIds",
            ],
        },
        "blockingIds": [],
    }
    if realm_family != expected_realm_family:
        fail("v003 realm shadow-generation disposition drifted")

    v002_realm = family_by_name(v002, "realms")
    mappings = v002_realm.get("mappings")
    if not isinstance(mappings, list) or len(mappings) != 4:
        fail("v002 realm source must contain exactly four mappings")
    if v002_realm.get("unavailableAnchors") != []:
        fail("v002 realm source must retain zero unavailable anchors")
    if v002_realm.get("blockingIds") != []:
        fail("v002 realm source must remain blocker-free")
    return v003, mappings


def validate_content_map(
    repo_root: Path,
    mappings: list[dict[str, Any]],
) -> dict[str, str]:
    content_map, _ = load_json(
        repo_root / CONTENT_MAP_PATH,
        "realm content map",
    )
    realm_family = family_by_name(content_map, "realms")
    entries = realm_family.get("entries")
    if not isinstance(entries, list) or len(entries) != 4:
        fail("realm content map must contain exactly four entries")

    content_by_key: dict[str, str] = {}
    anchor_refs: dict[str, list[str]] = {}
    for entry in entries:
        if not isinstance(entry, dict):
            fail("realm content-map entries must be objects")
        content = entry.get("content")
        if not isinstance(content, list) or len(content) != 2:
            fail("each realm content entry must contain name and description")
        keys: list[str] = []
        for row in content:
            if not isinstance(row, dict):
                fail("realm content rows must be objects")
            key = row.get("key")
            source_text = row.get("sourceText")
            if not isinstance(key, str) or not isinstance(source_text, str):
                fail("realm content rows require string key and sourceText")
            if key in content_by_key:
                fail(f"duplicate realm content reference: {key}")
            content_by_key[key] = source_text
            keys.append(key)
        anchor_refs[entry.get("technicalAnchor", "")] = keys

    for mapping in mappings:
        anchor = mapping.get("technicalAnchor")
        refs = mapping.get("contentRefs")
        if anchor_refs.get(anchor) != refs:
            fail(f"content-reference order drifted for {anchor}")
        for content_ref in refs:
            if content_ref not in content_by_key:
                fail(f"unresolved content reference: {content_ref}")

    legacy_source = (repo_root / LEGACY_SERVICE_PATH).read_text(encoding="utf-8")
    for mapping in mappings:
        legacy_name = mapping["technicalAnchor"].removeprefix("RealmId.")
        name_text = content_by_key[mapping["contentRefs"][0]]
        description_text = content_by_key[mapping["contentRefs"][1]]
        escaped_name = name_text.replace("\\", "\\\\").replace('"', '\\"')
        description_parts = description_text.rsplit("\n\n", 1)
        if len(description_parts) != 2:
            fail(f"legacy description shape drifted for {mapping['canonicalId']}")
        escaped_description = (
            description_parts[0]
            .replace("\\", "\\\\")
            .replace('"', '\\"')
            .replace("\n", "\\n")
        )
        escaped_perk = (
            description_parts[1]
            .replace("\\", "\\\\")
            .replace('"', '\\"')
            .replace("\n", "\\n")
        )
        expected_call = (
            f'CreateFallbackRealm(RealmId.{legacy_name}, "{escaped_name}", '
            f'"{escaped_description}", "{escaped_perk}");'
        )
        if expected_call not in legacy_source:
            fail(
                "content source no longer matches the exact legacy realm "
                f"definition for {mapping['canonicalId']}"
            )
    return content_by_key


def validate_specialized_catalog(
    repo_root: Path,
    mappings: list[dict[str, Any]],
    content_by_key: dict[str, str],
) -> dict[str, Any]:
    specialized, _ = load_json(
        repo_root / SPECIALIZED_CATALOG_PATH,
        "specialized realm catalog",
    )
    expected_order = [mapping["canonicalId"] for mapping in mappings]
    if specialized.get("version") != "0.1.0":
        fail("specialized realm catalog version drifted")
    if specialized.get("catalogId") != "al_realm_catalog":
        fail("specialized realm catalog identity drifted")
    if specialized.get("realmOrder") != expected_order:
        fail("specialized realm authored order drifted")
    realms = specialized.get("realms")
    if not isinstance(realms, list) or len(realms) != 4:
        fail("specialized realm catalog must contain exactly four realms")

    for index, mapping in enumerate(mappings):
        realm = realms[index]
        expected = {
            "id": mapping["canonicalId"],
            "legacyRuntimeId": mapping["technicalAnchor"].removeprefix(
                "RealmId."
            ),
            "peopleName": content_by_key[mapping["contentRefs"][0]],
            "innerRealmId": mapping["innerRealmId"],
            "mainGateId": mapping["mainGateId"],
            "outerWarzoneId": mapping["outerWarzoneId"],
        }
        for field, value in expected.items():
            if realm.get(field) != value:
                fail(
                    f"specialized comparison drifted at realms[{index}].{field}"
                )
        realm_gem_ids = realm.get("realmGemIds")
        if not isinstance(realm_gem_ids, list) or len(realm_gem_ids) != 2:
            fail(f"specialized realm gem references drifted at record {index}")
    return specialized


def build_artifact(mappings: list[dict[str, Any]]) -> dict[str, Any]:
    records: list[dict[str, Any]] = []
    aliases: list[dict[str, Any]] = []
    for mapping in mappings:
        mapping_aliases = mapping.get("aliases")
        if mapping_aliases != []:
            fail(
                f"realm aliases must remain empty for {mapping['canonicalId']}"
            )
        content_refs = mapping["contentRefs"]
        records.append(
            {
                "id": mapping["canonicalId"],
                "legacy_realm_id": mapping["technicalAnchor"].removeprefix(
                    "RealmId."
                ),
                "legacy_realm_value": mapping["legacyEnumValue"],
                "name_ref": content_refs[0],
                "description_ref": content_refs[1],
                "inner_realm_id": mapping["innerRealmId"],
                "main_gate_id": mapping["mainGateId"],
                "outer_warzone_id": mapping["outerWarzoneId"],
                "rare_resource_id": mapping["rareResourceId"],
                "capability_profile_ids": mapping["capabilityProfileIds"],
                "asset_ref": mapping["assetRef"],
            }
        )

    return {
        "gameId": GAME_ID,
        "catalogId": CATALOG_ID,
        "family": "realms",
        "schemaVersion": 1,
        "contentVersion": CONTENT_VERSION,
        "sourceRevision": SOURCE_CANDIDATE_ID,
        "records": records,
        "aliases": aliases,
    }


def validate_generator_revision(
    repo_root: Path,
    generator_revision: str,
) -> str:
    generator_file = repo_root / GENERATOR_PATH
    current = generator_file.read_bytes()
    committed = git_blob(repo_root, generator_revision, GENERATOR_PATH)
    if current != committed:
        fail(
            "generator working bytes differ from the requested generator revision"
        )
    return sha256(current)


def build_evidence(
    artifact_raw: bytes,
    generator_revision: str,
    generator_raw_sha256: str,
    mappings: list[dict[str, Any]],
    specialized: dict[str, Any],
) -> dict[str, Any]:
    realm_order = [mapping["canonicalId"] for mapping in mappings]
    diagnostics = [
        {
            "code": "AL-C9A-REALM-SHADOW-MATCH",
            "severity": "information",
            "realmId": mapping["canonicalId"],
            "fieldPath": f"$.records[{index}]",
            "message": (
                "Common identity, legacy identity, and world-boundary fields "
                "match the specialized authored realm."
            ),
        }
        for index, mapping in enumerate(mappings)
    ]
    diagnostics.extend(
        [
            {
                "code": "AL-C9A-SPECIALIZED-SCOPE-RETAINED",
                "severity": "information",
                "realmId": "",
                "fieldPath": "$",
                "message": (
                    "Selection policy, narrative continuity, and Realm Gem "
                    "references remain specialized-only authority."
                ),
            },
            {
                "code": "AL-C9A-RUNTIME-AUTHORITY-UNCHANGED",
                "severity": "information",
                "realmId": "",
                "fieldPath": "$",
                "message": (
                    "The shadow artifact has no manifest, loader registration, "
                    "store publication, or gameplay consumer."
                ),
            },
        ]
    )

    return {
        "schemaVersion": 1,
        "evidenceId": "realm-family-shadow-evidence-2026-07-30-v001",
        "purpose": "non_production_shadow_validation",
        "productionEligible": False,
        "runtimeAuthority": "unchanged",
        "consumerActivation": "none",
        "sourceCandidate": {
            "candidateId": SOURCE_CANDIDATE_ID,
            "rawSha256": PINNED_INPUTS[0]["rawSha256"],
            "inheritedRealmCandidateId": REALM_SOURCE_CANDIDATE_ID,
            "realmDisposition": "ready_for_non_production_shadow_generation",
            "effectiveRealmBlockingIds": [],
            "effectiveGlobalBlockingCount": 26,
        },
        "artifact": {
            "path": ARTIFACT_PATH,
            "catalogId": CATALOG_ID,
            "family": "realms",
            "schemaVersion": 1,
            "contentVersion": CONTENT_VERSION,
            "sourceRevision": SOURCE_CANDIDATE_ID,
            "rawSha256": sha256(artifact_raw),
            "recordCount": 4,
            "aliasCount": 0,
        },
        "generator": {
            "path": GENERATOR_PATH,
            "sourceRevision": generator_revision,
            "rawSha256": generator_raw_sha256,
            "command": (
                "python3 "
                f"{GENERATOR_PATH} --write "
                f"--generator-revision {generator_revision}"
            ),
        },
        "inputs": PINNED_INPUTS,
        "comparison": {
            "specializedCatalogPath": SPECIALIZED_CATALOG_PATH,
            "specializedCatalogId": specialized["catalogId"],
            "specializedCatalogVersion": specialized["version"],
            "matchingRealmOrder": realm_order,
            "comparedFields": [
                {"shadow": "id", "specialized": "id"},
                {
                    "shadow": "legacy_realm_id",
                    "specialized": "legacyRuntimeId",
                },
                {
                    "shadow": "name_ref (resolved)",
                    "specialized": "peopleName",
                },
                {
                    "shadow": "inner_realm_id",
                    "specialized": "innerRealmId",
                },
                {
                    "shadow": "main_gate_id",
                    "specialized": "mainGateId",
                },
                {
                    "shadow": "outer_warzone_id",
                    "specialized": "outerWarzoneId",
                },
            ],
            "commonOnlyFields": [
                "legacy_realm_value",
                "name_ref",
                "description_ref",
                "rare_resource_id",
                "capability_profile_ids",
                "asset_ref",
            ],
            "specializedAuthorityRetained": [
                "selectionPolicy",
                "narrativeContinuity",
                "realmGemIds",
            ],
        },
        "diagnostics": diagnostics,
        "forbiddenOutputs": FORBIDDEN_OUTPUTS,
    }


def resolve_generator_revision(
    repo_root: Path,
    requested_revision: str | None,
) -> str:
    if requested_revision:
        return requested_revision
    result = subprocess.run(
        ["git", "log", "-1", "--format=%H", "--", GENERATOR_PATH],
        cwd=repo_root,
        check=False,
        capture_output=True,
        text=True,
    )
    revision = result.stdout.strip()
    if result.returncode != 0 or not revision:
        fail(
            "the generator must be committed before evidence can pin its revision"
        )
    return revision


def validate_forbidden_outputs(repo_root: Path) -> None:
    for relative_path in FORBIDDEN_OUTPUTS:
        if (repo_root / relative_path).exists():
            fail(f"forbidden production output exists: {relative_path}")


def write_exact(target: Path, raw: bytes) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(target.name + ".tmp")
    temporary.write_bytes(raw)
    os.replace(temporary, target)


def check_exact(target: Path, expected: bytes, label: str) -> None:
    if not target.is_file():
        fail(f"committed {label} is missing: {target}")
    actual = target.read_bytes()
    if actual != expected:
        fail(
            f"committed {label} drifted: expected {sha256(expected)}, "
            f"found {sha256(actual)}"
        )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group()
    action.add_argument(
        "--write",
        action="store_true",
        help="write only the two reviewed non-production output paths",
    )
    action.add_argument(
        "--validate-inputs",
        action="store_true",
        help="validate pinned inputs and preview the artifact hash without evidence",
    )
    parser.add_argument(
        "--generator-revision",
        help="exact committed revision containing the current generator bytes",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    try:
        validate_forbidden_outputs(repo_root)
        _, mappings = validate_source_chain(repo_root)
        content_by_key = validate_content_map(repo_root, mappings)
        specialized = validate_specialized_catalog(
            repo_root,
            mappings,
            content_by_key,
        )
        artifact = build_artifact(mappings)
        artifact_raw = canonical_json(artifact)

        if args.validate_inputs:
            print("PASS: all pinned realm shadow inputs are exact")
            print(
                "PASS: four authored-order realm records resolve exact content "
                "and specialized comparison fields"
            )
            print(f"PASS: preview artifact raw SHA-256 {sha256(artifact_raw)}")
            return 0

        generator_revision = resolve_generator_revision(
            repo_root,
            args.generator_revision,
        )
        generator_raw_sha256 = validate_generator_revision(
            repo_root,
            generator_revision,
        )
        evidence = build_evidence(
            artifact_raw,
            generator_revision,
            generator_raw_sha256,
            mappings,
            specialized,
        )
        evidence_raw = canonical_json(evidence)

        if args.write:
            write_exact(repo_root / ARTIFACT_PATH, artifact_raw)
            write_exact(repo_root / EVIDENCE_PATH, evidence_raw)
        else:
            check_exact(
                repo_root / ARTIFACT_PATH,
                artifact_raw,
                "realm shadow artifact",
            )
            check_exact(
                repo_root / EVIDENCE_PATH,
                evidence_raw,
                "realm shadow evidence",
            )
    except GenerationError as error:
        print(error, file=sys.stderr)
        return 1

    print("PASS: realm shadow artifact and evidence are deterministic")
    print("PASS: four exact realm records and zero aliases are retained")
    print("PASS: specialized realm authority remains live and unchanged")
    print(f"PASS: artifact raw SHA-256 {sha256(artifact_raw)}")
    print(f"PASS: evidence raw SHA-256 {sha256(evidence_raw)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
